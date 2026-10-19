Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'WindowsStoreRoot.psm1')
Import-Module (Join-Path $PSScriptRoot 'WindowsStoreContext.psm1')

# One-time move of the store from the former root (<LocalApplicationData default
# location>\OneStarMaker) to the store root (<UserProfile>\OneStarMaker), and the
# only writer of the migration marker. It is deleted before its pull request is
# merged: after the move nothing in the repository writes a marker again.
#
# What it may do, and nothing more:
#   - rename each owned role directory (Artifacts, Workflow, Harness) to the store
#     root on the same volume. Never a copy: the credential store must not be read,
#     so a rename that cannot happen (another volume, an existing target, a file in
#     use) stops the run instead of falling back to copying;
#   - put a tombstone (an empty regular file with the role's name) in the former
#     root after each rename, and for a role that never existed. The resolver reads
#     a regular file there as absence, and the old code's CreateDirectory fails on
#     it, so an old writer that runs again cannot bring the role back;
#   - create the store root directory, keeping the ACL it inherits from UserProfile;
#   - write the marker once every role in the former root is a tombstone.
# It never opens a file below a role for content, never touches the former root's
# parent, RevisionLocks or any other name, and never rolls anything back: a failed
# run stops with a fixed code and the same command, run again, continues from the
# state it finds (see the table in Get-MigrationRoleAction).
#
# Every exception is folded into a fixed reasonCode: .NET messages carry absolute
# paths, and the result must hold only codes, role names and counts.
if (-not ('OneStarMaker.Workflow.StoreRootMigrationNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace OneStarMaker.Workflow {
 public static class StoreRootMigrationNative {
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern bool MoveFileExW(string source,string target,uint flags);
  static string Native(string path) { return path.Length>=260 ? @"\\?\"+path : path; }
  // Flags 0: no MOVEFILE_COPY_ALLOWED (a move to another volume fails with
  // ERROR_NOT_SAME_DEVICE instead of copying) and no MOVEFILE_REPLACE_EXISTING
  // (an existing target fails instead of being replaced). Returns 0 or the
  // Win32 error, never a message.
  public static int Move(string source,string target) {
   return MoveFileExW(Native(source),Native(target),0) ? 0 : Marshal.GetLastWin32Error();
  }
 }
}
'@
}

$script:StoreDirectoryName='OneStarMaker'
$script:MarkerFileName='store-root.json'
# The same three names the resolver checks under the former root, in move order.
$script:OwnedRoleNames=@('Artifacts','Workflow','Harness')
$script:ReasonCodes=@('store-root-precondition','virtualized-store','store-context-unknown','legacy-redirected','different-volume','role-split','role-unknown','rename-failed','count-mismatch','tombstone-failed','marker-write-failed','marker-not-accepted','migration-failed')
# Private offline seams; never set from CLI arguments or the environment (the
# entry script takes no arguments). Tests set them through the module scope.
# BaseForTest / LegacyBaseForTest replace the two Known Folder values and must be
# set to the same values as the resolver's own seams. ResolvedLocalForTest
# replaces the per-user LocalApplicationData value compared with the default.
# StoreContextForTest replaces the result of Get-OsmStoreContext. QueryForTest
# replaces the metadata query of a role path. RenameForTest, TombstoneForTest and
# MarkerIoForTest (Write, Rename) replace the writing operations to inject failures.
$script:BaseForTest=$null
$script:LegacyBaseForTest=$null
$script:ResolvedLocalForTest=$null
$script:StoreContextForTest=$null
$script:QueryForTest=$null
$script:RenameForTest=$null
$script:TombstoneForTest=$null
$script:MarkerIoForTest=$null
$script:NativeRename={param([string]$Source,[string]$Target)[OneStarMaker.Workflow.StoreRootMigrationNative]::Move($Source,$Target)}
# CreateNew: an existing name, file or directory, is never overwritten.
$script:NativeTombstone={param([string]$Path)[IO.FileStream]::new($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None).Dispose()}
$script:NativeMarkerIo=@{
    Write={param([string]$Path,[byte[]]$Bytes)
        $stream=[IO.FileStream]::new($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try{$stream.Write($Bytes,0,$Bytes.Length);$stream.Flush($true)}finally{$stream.Dispose()}}
    Rename={param([string]$Source,[string]$Target)[OneStarMaker.Workflow.StoreRootMigrationNative]::Move($Source,$Target)}
}

function Stop-Migration([string]$Code){ throw [InvalidOperationException]::new($Code) }
function Get-MigrationBase {
    $base=if($script:BaseForTest){[string]$script:BaseForTest}else{[Environment]::GetFolderPath('UserProfile')}
    if([string]::IsNullOrWhiteSpace($base) -or -not [IO.Path]::IsPathFullyQualified($base)){Stop-Migration 'store-root-precondition'}
    return [IO.Path]::GetFullPath($base).TrimEnd('\')
}
function Get-MigrationLegacyBase {
    $base=if($script:LegacyBaseForTest){[string]$script:LegacyBaseForTest}else{[OneStarMaker.Workflow.StoreRootNative]::LocalAppDataDefault()}
    if([string]::IsNullOrWhiteSpace($base) -or -not [IO.Path]::IsPathFullyQualified($base)){Stop-Migration 'store-root-precondition'}
    return [IO.Path]::GetFullPath($base).TrimEnd('\')
}
# The resolver also checks the per-user LocalApplicationData when it differs from
# the default; this program moves only the default one, so a redirected host
# would keep a former store the move never sees. Stop there instead.
function Assert-MigrationNotRedirected([string]$LegacyBase){
    $resolved=if($script:ResolvedLocalForTest){[string]$script:ResolvedLocalForTest}elseif($script:LegacyBaseForTest){$LegacyBase}else{[Environment]::GetFolderPath('LocalApplicationData')}
    if([string]::IsNullOrWhiteSpace($resolved) -or -not [IO.Path]::IsPathFullyQualified($resolved)){Stop-Migration 'legacy-redirected'}
    if(-not [string]::Equals([IO.Path]::GetFullPath($resolved).TrimEnd('\'),$LegacyBase,[StringComparison]::OrdinalIgnoreCase)){Stop-Migration 'legacy-redirected'}
}
function Get-MigrationStoreContext { if($script:StoreContextForTest){return [string]$script:StoreContextForTest};return [string](Get-OsmStoreContext) }
function Invoke-MigrationQuery([string]$Path){
    if($script:QueryForTest){return [string](& $script:QueryForTest $Path)}
    return [string][OneStarMaker.Workflow.StoreRootNative]::Query($Path)
}
# Metadata only: the length of a regular file that Query already reported as a
# file (not a reparse point), so nothing is opened for content.
function Get-MigrationFileLength([string]$Path){ return [IO.FileInfo]::new($Path).Length }
# Files, directories and total file size below a role. Reparse points are skipped
# and never followed, so a junction inside a role cannot pull another tree into
# the count; the same function counts before and after the rename.
function Measure-MigrationTree([string]$Path){
    $options=[IO.EnumerationOptions]::new();$options.RecurseSubdirectories=$true
    $options.AttributesToSkip=[IO.FileAttributes]::ReparsePoint;$options.IgnoreInaccessible=$false
    $files=[long]0;$directories=[long]0;$bytes=[long]0
    foreach($entry in [IO.DirectoryInfo]::new($Path).EnumerateFileSystemInfos('*',$options)){
        if($entry -is [IO.FileInfo]){$files++;$bytes+=$entry.Length}else{$directories++}
    }
    return [ordered]@{files=$files;directories=$directories;bytes=$bytes}
}
function Test-MigrationSameCount($Before,$After){ return $Before.files -eq $After.files -and $Before.directories -eq $After.directories -and $Before.bytes -eq $After.bytes }
# The state of one role as the pair (former root, store root), decided before
# anything is changed:
#   directory / absent       -> move: count, rename, tombstone, count again
#   absent / absent          -> initiallyAbsent: tombstone only, nothing created in the store root
#   absent / directory       -> tombstone: a previous run stopped after the rename
#   tombstone / directory    -> alreadyMoved
#   tombstone / absent       -> initiallyAbsent (a previous run placed the tombstone)
#   non-empty regular file   -> role-unknown (only a 0-byte file is a tombstone)
#   directory / directory    -> role-split
#   anything else (reparse point, denied, error, a regular file in the store root) -> role-unknown
# Reading absent / directory as "stopped after the rename" is not proven by any
# record: the store on this host can be rebuilt, so the program accepts that.
function Get-MigrationRoleAction([string]$Legacy,[string]$Target){
    $old=Invoke-MigrationQuery $Legacy;$new=Invoke-MigrationQuery $Target
    if($old -ceq 'file'){
        if((Get-MigrationFileLength $Legacy) -ne 0){Stop-Migration 'role-unknown'}
        if($new -ceq 'directory'){return 'alreadyMoved'}
        if($new -ceq 'absent'){return 'initiallyAbsent'}
        Stop-Migration 'role-unknown'
    }
    if($old -ceq 'directory' -and $new -ceq 'directory'){Stop-Migration 'role-split'}
    if($old -ceq 'directory' -and $new -ceq 'absent'){return 'move'}
    if($old -ceq 'absent' -and $new -ceq 'absent'){return 'placeTombstone'}
    if($old -ceq 'absent' -and $new -ceq 'directory'){return 'tombstone'}
    Stop-Migration 'role-unknown'
}
function New-MigrationTombstone([string]$Path){
    try{if($script:TombstoneForTest){& $script:TombstoneForTest $Path}else{& $script:NativeTombstone $Path}}catch{Stop-Migration 'tombstone-failed'}
}
function Move-MigrationRole([string]$Legacy,[string]$Target){
    $code=0
    try{$code=[int]$(if($script:RenameForTest){& $script:RenameForTest $Legacy $Target}else{& $script:NativeRename $Legacy $Target})}catch{Stop-Migration 'rename-failed'}
    if($code -eq 17){Stop-Migration 'different-volume'}
    if($code -ne 0){Stop-Migration 'rename-failed'}
}
function Get-MigrationMarkerBytes {
    $document=[ordered]@{schemaVersion=1;kind='osm-store-root';ownerSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value;createdAt=[DateTimeOffset]::UtcNow.ToString('o')}
    return [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $document -Compress))
}
# Whole bytes to a fresh name in the store root, flushed, then renamed without
# replacing: the marker appears complete or not at all, and an existing marker is
# never overwritten. On any failure the temporary name is removed and no marker
# is left behind.
function Publish-MigrationMarker([string]$Root){
    $io=if($script:MarkerIoForTest){$script:MarkerIoForTest}else{$script:NativeMarkerIo}
    $marker=[IO.Path]::Combine($Root,$script:MarkerFileName)
    $pending=[IO.Path]::Combine($Root,'.store-root.'+[Guid]::NewGuid().ToString('N')+'.pending')
    try{
        & $io.Write $pending (Get-MigrationMarkerBytes)
        $code=[int](& $io.Rename $pending $marker)
        if($code -ne 0){throw 'marker-rename'}
    }catch{
        try{if([IO.File]::Exists($pending)){[IO.File]::Delete($pending)}}catch{}
        Stop-Migration 'marker-write-failed'
    }
}
function New-MigrationResult([string]$Status,[string]$Code,$Roles){
    return [ordered]@{status=$Status;reasonCode=$Code;roles=@($Roles)}
}

# Runs the whole migration once and returns {status, reasonCode, roles}. Each
# role entry has its name, the action taken and the counts before and after
# (null where there was nothing to count). Never throws.
function Invoke-OsmStoreRootMigration {
    $roles=[Collections.Generic.List[object]]::new()
    try{
        # A virtualized process would see the package's private copy of the former
        # root and move that instead of the real one.
        $context=Get-MigrationStoreContext
        if($context -ceq 'virtualized'){Stop-Migration 'virtualized-store'}
        if($context -cne 'not-virtualized'){Stop-Migration 'store-context-unknown'}
        $base=Get-MigrationBase;$legacyBase=Get-MigrationLegacyBase
        Assert-MigrationNotRedirected $legacyBase
        $root=[IO.Path]::Combine($base,$script:StoreDirectoryName);$legacyRoot=[IO.Path]::Combine($legacyBase,$script:StoreDirectoryName)
        if(-not [string]::Equals([IO.Path]::GetPathRoot($root),[IO.Path]::GetPathRoot($legacyRoot),[StringComparison]::OrdinalIgnoreCase)){Stop-Migration 'different-volume'}
        # Only "not migrated yet" leads to the move. A root the resolver accepts is
        # already done; every other answer (a broken or foreign marker, a marker
        # with a role still in the former root, an unusable root) is left exactly
        # as it is for the owner.
        $resolved=$null
        try{$resolved=[string](Get-OsmStoreRoot)}catch{$resolved=$_.Exception.Message;if($resolved -cne 'store-not-migrated'){Stop-Migration 'store-root-precondition'}}
        if($resolved -cne 'store-not-migrated'){
            if([string]::Equals($resolved,$root,[StringComparison]::OrdinalIgnoreCase)){return New-MigrationResult 'passed' 'already-migrated' $roles}
            Stop-Migration 'store-root-precondition'
        }
        $plan=[ordered]@{}
        foreach($name in $script:OwnedRoleNames){$plan[$name]=Get-MigrationRoleAction ([IO.Path]::Combine($legacyRoot,$name)) ([IO.Path]::Combine($root,$name))}
        $rootStatus=Invoke-MigrationQuery $root
        if($rootStatus -ceq 'absent'){
            if((Invoke-MigrationQuery $base) -cne 'directory'){Stop-Migration 'store-root-precondition'}
            [IO.Directory]::CreateDirectory($root)|Out-Null
        }elseif($rootStatus -cne 'directory'){Stop-Migration 'store-root-precondition'}
        foreach($name in $script:OwnedRoleNames){
            $legacy=[IO.Path]::Combine($legacyRoot,$name);$target=[IO.Path]::Combine($root,$name)
            $entry=[ordered]@{name=$name;action=$plan[$name];before=$null;after=$null}
            $roles.Add($entry)
            switch($plan[$name]){
                'move'{
                    $entry.before=Measure-MigrationTree $legacy
                    Move-MigrationRole $legacy $target
                    New-MigrationTombstone $legacy
                    $entry.action='moved';$entry.after=Measure-MigrationTree $target
                    if(-not (Test-MigrationSameCount $entry.before $entry.after)){Stop-Migration 'count-mismatch'}
                }
                'tombstone'{New-MigrationTombstone $legacy;$entry.action='moved';$entry.after=Measure-MigrationTree $target}
                'placeTombstone'{New-MigrationTombstone $legacy;$entry.action='initiallyAbsent'}
                'alreadyMoved'{$entry.after=Measure-MigrationTree $target}
                'initiallyAbsent'{}
            }
        }
        # The marker only after every role in the former root is again seen as a
        # tombstone, whatever the steps above reported.
        foreach($name in $script:OwnedRoleNames){
            $legacy=[IO.Path]::Combine($legacyRoot,$name)
            if((Invoke-MigrationQuery $legacy) -cne 'file' -or (Get-MigrationFileLength $legacy) -ne 0){Stop-Migration 'role-unknown'}
        }
        Publish-MigrationMarker $root
        $accepted=try{[string](Get-OsmStoreRoot)}catch{''}
        if(-not [string]::Equals($accepted,$root,[StringComparison]::OrdinalIgnoreCase)){Stop-Migration 'marker-not-accepted'}
        return New-MigrationResult 'passed' 'migrated' $roles
    }catch{
        $message=$_.Exception.Message
        $code=if($message -cin $script:ReasonCodes){$message}else{'migration-failed'}
        return New-MigrationResult 'failed' $code $roles
    }
}
Export-ModuleMember -Function Invoke-OsmStoreRootMigration
