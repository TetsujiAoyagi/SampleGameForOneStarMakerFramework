Set-StrictMode -Version Latest

# Decides whether this process's LocalApplicationData writes are redirected by
# packaged-app virtualization (for example into a package LocalCache). When they
# are, a write under LocalApplicationData lands in a second, private copy and
# silently splits what lives there.
#
# Used only by store plan / consolidate, which read a package copy and must run
# non-virtualized. The three interactive CLIs no longer call it: the store root
# moved out of AppData (WindowsStoreRoot.psm1 decides the root, the migration
# marker and the former root's residue), so a probe under LocalApplicationData
# says nothing about writes to the store.
#
# Where a virtualized write lands depends on the path: a new file inside a
# directory that exists only on the physical side goes to the physical side, so a
# probe inside any directory that may already exist (a fixed probe area, a
# leftover of another call, the store itself) can report 'not-virtualized' inside
# a virtualized process. Each call therefore creates its own directory with a
# fresh name directly under LocalApplicationData, probes only inside it, and
# removes only that directory. It never opens, creates or removes any other
# directory, including the former fixed area OneStarMaker.StoreProbe.
#
# Package identity is deliberately not consulted: a process without identity was
# observed virtualized and one with identity was observed not virtualized.
# Importing this module performs no file I/O; the probe runs only when
# Get-OsmStoreContext is called.
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
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern bool CreateDirectoryW(string path,IntPtr security);
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
  // Unlike Directory.CreateDirectory, this fails when the name already exists
  // (directory or file) and never creates missing parents, so success proves
  // that this call created the directory.
  public static void CreateNewDirectory(string path) {
   if(!CreateDirectoryW(path,IntPtr.Zero)) throw new IOException("store-context-unknown");
  }
 }
}
'@
}

$script:ProbeDirectoryPrefix='OneStarMaker.StoreProbe.'
# Private offline seams; never set from CLI arguments or the environment.
# ProbeIo replaces all five OS operations together. LocalRootForTest replaces only
# the LocalApplicationData root, so tests can run the native operations on a
# temporary directory. Production uses the native operations below.
$script:ProbeIo=$null
$script:LocalRootForTest=$null
$script:NativeIo=@{
    CreateDirectory={param([string]$Path)[OneStarMaker.Workflow.StoreContextProbe]::CreateNewDirectory($Path)}
    # CreateNew never creates the parent: if this call's directory has vanished the
    # open fails instead of re-creating it somewhere the call did not observe.
    # DeleteOnClose lets the OS remove the probe file even on an exception path.
    Open={param([string]$Path)[IO.FileStream]::new($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None,1,[IO.FileOptions]::DeleteOnClose)}
    FinalPath={param($Handle)[OneStarMaker.Workflow.StoreContextProbe]::FinalPath($Handle)}
    Close={param($Handle)$Handle.Dispose()}
    # Non-recursive: removes the directory only while it is empty.
    RemoveDirectory={param([string]$Path)[IO.Directory]::Delete($Path,$false)}
}

# Returns 'not-virtualized', 'virtualized' or 'unknown'. Any failure to create the
# directory, or to create, query or close the probe, is 'unknown'; callers must
# fail closed on both of the latter values. The result is not cached: each
# invocation probes again in a new directory.
function Get-OsmStoreContext {
    $io=if($script:ProbeIo){$script:ProbeIo}else{$script:NativeIo}
    $handle=$null;$final=$null;$logical=$null;$closed=$true;$directory=$null
    try {
        $local=if($script:LocalRootForTest){[string]$script:LocalRootForTest}else{[Environment]::GetFolderPath('LocalApplicationData')}
        if([string]::IsNullOrEmpty($local) -or -not [IO.Path]::IsPathFullyQualified($local)){return 'unknown'}
        $candidate=[IO.Path]::GetFullPath([IO.Path]::Combine($local,$script:ProbeDirectoryPrefix+[Guid]::NewGuid().ToString('N')))
        # A name that already exists was not created by this call; probing inside
        # it could report the side where someone else's directory lives.
        & $io.CreateDirectory $candidate
        $directory=$candidate
        $logical=[IO.Path]::Combine($directory,[Guid]::NewGuid().ToString('N')+'.probe')
        $handle=& $io.Open $logical
        $final=[string](& $io.FinalPath $handle)
    } catch { $final=$null }
    finally {
        if($null -ne $handle){try{& $io.Close $handle}catch{$closed=$false}}
        # Only the directory this call created, and only while empty. A failure
        # leaves an empty, uniquely named directory that no later call reuses, so
        # it never affects a decision. Leftovers of other calls are not cleaned:
        # a prefix-wide removal could delete a concurrent call's live directory.
        if($null -ne $directory){try{& $io.RemoveDirectory $directory}catch{}}
    }
    if($null -eq $directory -or -not $closed -or [string]::IsNullOrEmpty($final)){return 'unknown'}
    # A virtualized write resolves to a different physical location than the
    # logical path that was opened; a non-virtualized one resolves to itself.
    if([string]::Equals($final.TrimEnd('\'),$logical,[StringComparison]::OrdinalIgnoreCase)){return 'not-virtualized'}
    return 'virtualized'
}
Export-ModuleMember -Function Get-OsmStoreContext
