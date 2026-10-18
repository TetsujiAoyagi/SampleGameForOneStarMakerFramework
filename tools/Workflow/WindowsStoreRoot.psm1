Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'WindowsStorePaths.psm1')

# Decides where the store is and whether it may be used. The store root is
# <UserProfile Known Folder>\OneStarMaker: outside AppData, so a packaged app's
# write virtualization does not split it. The former root under
# LocalApplicationData survives only as a read-only alias: its path string is
# used for prefix substitution of paths kept in immutable records and for the
# residue check below; nothing in it is ever opened for content, created, moved
# or removed by this module.
#
# Fail-closed order, decided again on every call (nothing is cached, so a
# process cannot keep using a root whose marker vanished, apart from the window
# between the check and the caller's I/O, which is accepted):
#   1. static rejection of the derived root (relative, UNC, inside the repo,
#      inside the OneDrive mirror),
#   2. a reparse point in the root or any ancestor,
#   3. the migration marker <root>\store-root.json (absent: store-not-migrated;
#      unreadable, malformed, foreign owner, or resolving to another physical
#      file: store-root-unavailable),
#   4. an owned role directory (Artifacts, Workflow, Harness) still visible under
#      the former root: legacy-store-present. A query that cannot prove absence
#      (access denied, reparse point, unknown error) is store-root-unavailable.
# Every failure is thrown as one of those three fixed codes and nothing else:
# callers print the message, so no path, package name or SID may travel in it.
#
# Known Folders are read through [Environment]::GetFolderPath, never through
# USERPROFILE / LOCALAPPDATA, which a caller could redirect. The private seams
# below exist for offline tests only and cannot be set from the CLI or the
# environment. Importing this module performs no file I/O.
if (-not ('OneStarMaker.Workflow.StoreRootNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace OneStarMaker.Workflow {
 public static class StoreRootNative {
  [StructLayout(LayoutKind.Sequential)] struct BasicInfo {
   public uint attributes, creationLow, creationHigh, accessLow, accessHigh,
    writeLow, writeHigh, volumeSerial, sizeHigh, sizeLow, links, indexHigh, indexLow;
  }
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern SafeFileHandle CreateFileW(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
  [DllImport("kernel32.dll", SetLastError=true)]
  static extern bool GetFileInformationByHandle(SafeFileHandle handle,out BasicInfo info);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle,StringBuilder path,uint size,uint flags);
  [DllImport("shell32.dll", CharSet=CharSet.Unicode)]
  static extern int SHGetKnownFolderPath(ref Guid id,uint flags,IntPtr token,out IntPtr path);
  const uint OpenExisting=3, BackupSemantics=0x02000000, OpenReparsePoint=0x00200000, ShareAll=7, GenericRead=0x80000000;
  static readonly Guid LocalAppData=new Guid("F1B32785-6FBA-4FCF-9D55-7B8E7F157091");
  // The LocalApplicationData Known Folder as the shell derives it from the
  // profile's registered location (KF_FLAG_DEFAULT_PATH | KF_FLAG_DONT_VERIFY).
  // Environment.GetFolderPath expands the registry template %USERPROFILE%\AppData\Local
  // with the process environment, so a caller could move it; this value cannot be.
  public static string LocalAppDataDefault() {
   Guid id=LocalAppData; IntPtr buffer;
   int result=SHGetKnownFolderPath(ref id,0x4400,IntPtr.Zero,out buffer);
   if(result!=0) throw new IOException("store-root-unavailable");
   try { return Marshal.PtrToStringUni(buffer); } finally { Marshal.FreeCoTaskMem(buffer); }
  }
  static string Native(string path) { return path.Length>=260 ? @"\\?\"+path : path; }
  // Metadata only: zero access, every sharing mode, the reparse point itself
  // rather than its target. Returns what the path is, or why that is unknown:
  // "absent", "directory", "file", "reparse", "denied" or "error". Unlike
  // Directory.Exists, an access failure is reported, never read as absence.
  public static string Query(string path) {
   var handle=CreateFileW(Native(path),0,ShareAll,IntPtr.Zero,OpenExisting,BackupSemantics|OpenReparsePoint,IntPtr.Zero);
   if(handle.IsInvalid) {
    int error=Marshal.GetLastWin32Error(); handle.Dispose();
    if(error==2 || error==3) return "absent";
    if(error==5) return "denied";
    return "error";
   }
   using(handle) {
    BasicInfo info;
    if(!GetFileInformationByHandle(handle,out info)) return "error";
    if((info.attributes & 0x400)!=0) return "reparse";
    return (info.attributes & 0x10)!=0 ? "directory" : "file";
   }
  }
  // Opens the marker file for reading, shared with other readers. A reparse
  // point or a directory at the marker path is an error, not a marker. Absence
  // is reported as FileNotFoundException so the caller can tell "not migrated"
  // from "cannot tell"; the message is a fixed code either way.
  public static FileStream OpenMarker(string path) {
   var handle=CreateFileW(Native(path),GenericRead,1,IntPtr.Zero,OpenExisting,OpenReparsePoint,IntPtr.Zero);
   if(handle.IsInvalid) {
    int error=Marshal.GetLastWin32Error(); handle.Dispose();
    if(error==2 || error==3) throw new FileNotFoundException("store-not-migrated");
    throw new IOException("store-root-unavailable");
   }
   BasicInfo info;
   if(!GetFileInformationByHandle(handle,out info) || (info.attributes & 0x400)!=0 || (info.attributes & 0x10)!=0) { handle.Dispose(); throw new IOException("store-root-unavailable"); }
   return new FileStream(handle,FileAccess.Read,1);
  }
  public static string FinalPath(FileStream stream) {
   var buffer=new StringBuilder(32768);
   uint length=GetFinalPathNameByHandleW(stream.SafeFileHandle,buffer,(uint)buffer.Capacity,0);
   if(length==0 || length>=buffer.Capacity) throw new IOException("store-root-unavailable");
   string path=buffer.ToString();
   if(!path.StartsWith(@"\\?\") || path.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase)) throw new IOException("store-root-unavailable");
   return path.Substring(4);
  }
 }
}
'@
}

$script:StoreDirectoryName='OneStarMaker'
$script:MarkerFileName='store-root.json'
$script:MarkerKind='osm-store-root'
$script:MarkerLimit=4096
# The top-level names whose presence under the former root means the old store
# is still alive. Only these three: their layout belongs to the domain modules.
$script:OwnedRoleNames=@('Artifacts','Workflow','Harness')
$script:OneDriveMirror='D:\OneDrive\OSM-Artifacts'
# Private offline seams; never set from CLI arguments or the environment.
# BaseForTest / LegacyBaseForTest replace only the two Known Folder values.
# MarkerIoForTest replaces the four marker operations together (open, final
# path, read, close) so a test can record what is opened and inject failures.
# LegacyQueryForTest replaces the metadata query of the former root.
$script:BaseForTest=$null
$script:LegacyBaseForTest=$null
$script:MarkerIoForTest=$null
$script:LegacyQueryForTest=$null
$script:NativeMarkerIo=@{
    Open={param([string]$Path)[OneStarMaker.Workflow.StoreRootNative]::OpenMarker($Path)}
    FinalPath={param($Handle)[OneStarMaker.Workflow.StoreRootNative]::FinalPath($Handle)}
    Read={param($Handle)
        $length=[int]$Handle.Length;$bytes=[byte[]]::new($length);$read=0
        while($read -lt $length){$n=$Handle.Read($bytes,$read,$length-$read);if($n -le 0){throw [IO.IOException]::new('store-root-unavailable')};$read+=$n}
        ,$bytes}
    Close={param($Handle)$Handle.Dispose()}
}

function Get-OsmStoreBase {
    $base=if($script:BaseForTest){[string]$script:BaseForTest}else{[Environment]::GetFolderPath('UserProfile')}
    if([string]::IsNullOrWhiteSpace($base) -or -not [IO.Path]::IsPathFullyQualified($base)){throw 'store-root-unavailable'}
    return [IO.Path]::GetFullPath($base).TrimEnd('\')
}
function Get-OsmRepositoryRoot { return [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..','..')).TrimEnd('\') }
function Test-OsmWithin([string]$Path,[string]$Root){
    $p=$Path.TrimEnd('\');$r=$Root.TrimEnd('\')
    return $p.Equals($r,[StringComparison]::OrdinalIgnoreCase) -or $p.StartsWith($r+'\',[StringComparison]::OrdinalIgnoreCase)
}
# The derived root without any I/O: the value the resolver would check.
function Get-OsmStoreRootCandidate { return [IO.Path]::Combine((Get-OsmStoreBase),$script:StoreDirectoryName) }
function Assert-OsmStaticRoot([string]$Root){
    if(-not [IO.Path]::IsPathFullyQualified($Root) -or $Root.StartsWith('\\') -or $Root.Contains('/')){throw 'store-root-unavailable'}
    if((Test-OsmWithin $Root (Get-OsmRepositoryRoot)) -or (Test-OsmWithin $Root $script:OneDriveMirror)){throw 'store-root-unavailable'}
}
function Invoke-OsmQuery([string]$Path){ return [string][OneStarMaker.Workflow.StoreRootNative]::Query($Path) }
# Walks every ancestor from the drive root. A reparse point anywhere, a plain
# file in the way, or a query that cannot decide stops the resolver; an absent
# segment ends the walk (nothing below it exists either).
function Assert-OsmAncestors([string]$Path){
    $part=[IO.Path]::GetPathRoot($Path)
    if([string]::IsNullOrEmpty($part)){throw 'store-root-unavailable'}
    foreach($segment in $Path.Substring($part.Length).Split('\',[StringSplitOptions]::RemoveEmptyEntries)){
        $part=[IO.Path]::Combine($part,$segment)
        $status=Invoke-OsmQuery $part
        if($status -ceq 'absent'){return}
        if($status -cne 'directory'){throw 'store-root-unavailable'}
    }
}
function Test-OsmMarkerDocument([byte[]]$Bytes){
    if($Bytes.Length -lt 1 -or $Bytes.Length -gt $script:MarkerLimit){return $false}
    if($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF){return $false}
    $text=[Text.UTF8Encoding]::new($false,$true).GetString($Bytes)
    $document=[Text.Json.JsonDocument]::Parse($text)
    try{
        $root=$document.RootElement
        if($root.ValueKind -ne [Text.Json.JsonValueKind]::Object){return $false}
        $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal);$count=0
        foreach($property in $root.EnumerateObject()){$count++;if(-not $seen.Add($property.Name)){return $false}}
        if($count -ne 4 -or -not $seen.Contains('schemaVersion') -or -not $seen.Contains('kind') -or -not $seen.Contains('ownerSid') -or -not $seen.Contains('createdAt')){return $false}
        $version=$root.GetProperty('schemaVersion');$value=[long]0
        if($version.ValueKind -ne [Text.Json.JsonValueKind]::Number -or -not $version.TryGetInt64([ref]$value) -or $value -ne 1){return $false}
        $kind=$root.GetProperty('kind')
        if($kind.ValueKind -ne [Text.Json.JsonValueKind]::String -or $kind.GetString() -cne $script:MarkerKind){return $false}
        $owner=$root.GetProperty('ownerSid')
        if($owner.ValueKind -ne [Text.Json.JsonValueKind]::String -or $owner.GetString() -cne [Security.Principal.WindowsIdentity]::GetCurrent().User.Value){return $false}
        $created=$root.GetProperty('createdAt');$stamp=[DateTimeOffset]::MinValue
        if($created.ValueKind -ne [Text.Json.JsonValueKind]::String -or -not [DateTimeOffset]::TryParse($created.GetString(),[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind,[ref]$stamp)){return $false}
        return $true
    }finally{$document.Dispose()}
}
# Reads the marker through the seam. Every .NET exception is translated here so
# that no message with a path reaches a caller.
function Assert-OsmMarker([string]$Root){
    $io=if($script:MarkerIoForTest){$script:MarkerIoForTest}else{$script:NativeMarkerIo}
    $path=[IO.Path]::Combine($Root,$script:MarkerFileName)
    $handle=$null;$code='store-root-unavailable'
    try{
        try{$handle=& $io.Open $path}
        catch [IO.FileNotFoundException]{$code='store-not-migrated';throw}
        catch [IO.DirectoryNotFoundException]{$code='store-not-migrated';throw}
        $final=[string](& $io.FinalPath $handle)
        if(-not [string]::Equals($final.TrimEnd('\'),$path,[StringComparison]::OrdinalIgnoreCase)){throw 'marker-alias'}
        $bytes=[byte[]](& $io.Read $handle)
        if(-not (Test-OsmMarkerDocument $bytes)){throw 'marker-invalid'}
    }catch{throw $code}
    finally{if($null -ne $handle){try{& $io.Close $handle}catch{}}}
}
# The former root is not opened for content: only the three owned role names are
# queried for metadata, and only absence (or a plain file of that name) lets the
# resolver continue. The residue check also looks at the per-user resolution of
# LocalApplicationData when it differs from the default (a redirected AppData
# host): an old store living there must stop the new code just the same.
function Assert-OsmLegacyAbsent {
    $roots=@(Get-OsmLegacyStoreRoot)
    if(-not $script:LegacyBaseForTest){
        $resolved=[Environment]::GetFolderPath('LocalApplicationData')
        if(-not [string]::IsNullOrWhiteSpace($resolved) -and [IO.Path]::IsPathFullyQualified($resolved)){
            $candidate=[IO.Path]::Combine([IO.Path]::GetFullPath($resolved).TrimEnd('\'),$script:StoreDirectoryName)
            if(-not [string]::Equals($candidate,$roots[0],[StringComparison]::OrdinalIgnoreCase)){$roots+=@($candidate)}
        }
    }
    foreach($legacy in $roots){
        foreach($role in $script:OwnedRoleNames){
            $path=[IO.Path]::Combine($legacy,$role)
            try{$status=[string]$(if($script:LegacyQueryForTest){& $script:LegacyQueryForTest $path}else{Invoke-OsmQuery $path})}catch{throw 'store-root-unavailable'}
            if($status -ceq 'directory'){throw 'legacy-store-present'}
            if($status -cne 'absent' -and $status -cne 'file'){throw 'store-root-unavailable'}
        }
    }
}
# Returns the store root, <UserProfile>\OneStarMaker, after every check above.
# Creates nothing and changes no ACL.
function Get-OsmStoreRoot {
    $root=Get-OsmStoreRootCandidate
    try{Assert-OsmStaticRoot $root;Assert-OsmAncestors $root}catch{throw 'store-root-unavailable'}
    Assert-OsmMarker $root
    Assert-OsmLegacyAbsent
    return $root
}
# The former root under LocalApplicationData, as a string only. Callers use it
# for prefix substitution and boundary checks; nothing here opens it. The base is
# the Known Folder's default location, which the process environment cannot move.
function Get-OsmLegacyStoreRoot {
    $base=if($script:LegacyBaseForTest){[string]$script:LegacyBaseForTest}else{try{[OneStarMaker.Workflow.StoreRootNative]::LocalAppDataDefault()}catch{throw 'store-root-unavailable'}}
    if([string]::IsNullOrWhiteSpace($base) -or -not [IO.Path]::IsPathFullyQualified($base)){throw 'store-root-unavailable'}
    return [IO.Path]::Combine([IO.Path]::GetFullPath($base).TrimEnd('\'),$script:StoreDirectoryName)
}
# Checks an explicit store root given to harness.ps1 -StoreRoot, which only
# tests use. The value may not name the former root, the store root or the
# repository, neither by spelling nor through a junction: the nearest existing
# ancestor is resolved to its physical path and the same rejection is applied
# there. No marker or residue check: the explicit root is the test's own.
function Assert-OsmExplicitStoreRoot([string]$Path){
    try{
        if([string]::IsNullOrWhiteSpace($Path) -or -not [IO.Path]::IsPathFullyQualified($Path) -or $Path.StartsWith('\\')){throw 'explicit'}
        $full=[IO.Path]::GetFullPath($Path).TrimEnd('\')
        if($full.StartsWith('\\')){throw 'explicit'}
        $boundaries=@((Get-OsmLegacyStoreRoot),(Get-OsmStoreRootCandidate),(Get-OsmRepositoryRoot))
        foreach($boundary in $boundaries){if(Test-OsmWithin $full $boundary){throw 'explicit'}}
        # The drive root keeps its trailing separator: 'C:' alone would be drive-relative.
        $part=[IO.Path]::GetPathRoot($full);if([string]::IsNullOrEmpty($part)){throw 'explicit'}
        $existing=$part;$suffix=''
        foreach($segment in $full.Substring($part.Length).Split('\',[StringSplitOptions]::RemoveEmptyEntries)){
            if($suffix){$suffix=[IO.Path]::Combine($suffix,$segment);continue}
            $candidate=[IO.Path]::Combine($existing,$segment)
            $status=Invoke-OsmQuery $candidate
            if($status -ceq 'directory'){$existing=$candidate;continue}
            if($status -ceq 'absent'){$suffix=$segment;continue}
            throw 'explicit'
        }
        $physical=(Get-WindowsStorePathIdentity $existing).physicalPath
        if($physical.Length -eq 2 -and $physical[1] -eq ':'){$physical+='\'}
        $resolved=if($suffix){[IO.Path]::Combine($physical,$suffix)}else{$physical}
        foreach($boundary in $boundaries){if(Test-OsmWithin $resolved $boundary){throw 'explicit'}}
    }catch{throw 'store-root-unavailable'}
}
Export-ModuleMember -Function Get-OsmStoreRoot,Get-OsmLegacyStoreRoot,Assert-OsmExplicitStoreRoot
