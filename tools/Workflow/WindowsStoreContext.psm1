Set-StrictMode -Version Latest

# Decides whether this process's LocalApplicationData writes are redirected by
# packaged-app virtualization (for example into a package LocalCache). When they
# are, a store write lands in a second, private copy of the store and silently
# splits it, so the interactive CLIs stop before touching the store at all.
#
# The only thing this module writes is one probe file inside the probe area, a
# sibling of %LOCALAPPDATA%\OneStarMaker that is never part of the store. Package
# identity is deliberately not consulted: a process without identity was observed
# virtualized and one with identity was observed not virtualized. Importing this
# module performs no file I/O; the probe runs only when Get-OsmStoreContext is called.
if (-not ('OneStarMaker.Workflow.StoreContextProbe' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace OneStarMaker.Workflow {
 public static class StoreContextProbe {
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle,StringBuilder path,uint size,uint flags);
  public static string FinalPath(FileStream stream) {
   var buffer=new StringBuilder(32768);
   uint length=GetFinalPathNameByHandleW(stream.SafeFileHandle,buffer,(uint)buffer.Capacity,0);
   if(length==0 || length>=buffer.Capacity) throw new IOException("store-context-unknown");
   string path=buffer.ToString();
   // A redirected or network location is not a local store; never guess.
   if(!path.StartsWith(@"\\?\") || path.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase))
    throw new IOException("store-context-unknown");
   return path.Substring(4);
  }
 }
}
'@
}

$script:ProbeAreaName='OneStarMaker.StoreProbe'
# Private offline seam replacing all three OS operations together; never set from
# CLI arguments or the environment. Production uses the native operations below.
$script:ProbeIo=$null
$script:NativeIo=@{
    # The area directory is outside the store and may be left behind. Each probe
    # file is unique and DeleteOnClose, so the OS removes it even on an exception
    # path; a probe left by a killed process is never cleaned by another caller.
    Open={param([string]$Path)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))|Out-Null
        [IO.FileStream]::new($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None,1,[IO.FileOptions]::DeleteOnClose)
    }
    FinalPath={param($Handle)[OneStarMaker.Workflow.StoreContextProbe]::FinalPath($Handle)}
    Close={param($Handle)$Handle.Dispose()}
}

# Returns 'not-virtualized', 'virtualized' or 'unknown'. Any failure to create,
# query or close the probe is 'unknown'; callers must fail closed on both of the
# latter values. The result is not cached: each invocation probes again.
function Get-OsmStoreContext {
    $io=if($script:ProbeIo){$script:ProbeIo}else{$script:NativeIo}
    $handle=$null;$final=$null;$logical=$null;$closed=$true
    try {
        $local=[Environment]::GetFolderPath('LocalApplicationData')
        if([string]::IsNullOrEmpty($local) -or -not [IO.Path]::IsPathFullyQualified($local)){return 'unknown'}
        $logical=[IO.Path]::GetFullPath([IO.Path]::Combine($local,$script:ProbeAreaName,[Guid]::NewGuid().ToString('N')+'.probe'))
        $handle=& $io.Open $logical
        $final=[string](& $io.FinalPath $handle)
    } catch { $final=$null }
    finally {
        if($null -ne $handle){try{& $io.Close $handle}catch{$closed=$false}}
    }
    if(-not $closed -or [string]::IsNullOrEmpty($final)){return 'unknown'}
    # A virtualized write resolves to a different physical location than the
    # logical path that was opened; a non-virtualized one resolves to itself.
    if([string]::Equals($final.TrimEnd('\'),$logical,[StringComparison]::OrdinalIgnoreCase)){return 'not-virtualized'}
    return 'virtualized'
}
Export-ModuleMember -Function Get-OsmStoreContext
