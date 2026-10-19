Set-StrictMode -Version Latest
# Shared by the offline suites that must pass the store-root gate: a temporary
# stand-in for the UserProfile Known Folder holding the migration marker, an
# empty stand-in for the former LocalApplicationData root, and the private seams
# of WindowsStoreRoot.psm1 pointed at them. Nothing here touches the real store,
# the real former root or the real Known Folders: every base must be below the
# temp directory, and a base outside it is refused.
function Get-StoreRootTestModules {
    $seen=[Collections.Generic.HashSet[object]]::new();$queue=[Collections.Generic.Queue[object]]::new()
    foreach($m in @(Get-Module -All)){$queue.Enqueue($m)}
    while($queue.Count){$m=$queue.Dequeue();if(-not $seen.Add($m)){continue};if($m.Name -ceq 'WindowsStoreRoot'){$m};foreach($n in $m.NestedModules){$queue.Enqueue($n)}}
}
function Import-StoreRootTestModule { return Import-Module ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../WindowsStoreRoot.psm1'))) -PassThru }
function Set-StoreRootTestSeams([string]$Base,[string]$LegacyBase,$MarkerIo=$null,$LegacyQuery=$null){
    $modules=@(Get-StoreRootTestModules);if($modules.Count -eq 0){$modules=@(Import-StoreRootTestModule)}
    foreach($m in $modules){& $m {param($b,$l,$i,$q)$script:BaseForTest=$b;$script:LegacyBaseForTest=$l;$script:MarkerIoForTest=$i;$script:LegacyQueryForTest=$q} $Base $LegacyBase $MarkerIo $LegacyQuery}
}
function Clear-StoreRootTestSeams { foreach($m in @(Get-StoreRootTestModules)){& $m {$script:BaseForTest=$null;$script:LegacyBaseForTest=$null;$script:MarkerIoForTest=$null;$script:LegacyQueryForTest=$null}} }
function Assert-StoreRootTestTemp([string]$Path){
    $temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    if(-not [IO.Path]::GetFullPath($Path).StartsWith($temp+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'store-root fixture outside temp'}
}
function New-StoreRootTestMarkerBytes([hashtable]$Override=@{}){
    $document=[ordered]@{schemaVersion=1;kind='osm-store-root';ownerSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value;createdAt=[DateTimeOffset]::UtcNow.ToString('o')}
    foreach($key in $Override.Keys){if($null -eq $Override[$key]){$document.Remove($key)}else{$document[$key]=$Override[$key]}}
    return [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $document -Compress))
}
# Writes <Base>\OneStarMaker\store-root.json the way the migration will: whole
# bytes to a temporary name, flushed, then renamed into place. Returns the root.
function New-StoreRootTestMarker([string]$Base,[byte[]]$Bytes=$null,[hashtable]$Override=@{}){
    Assert-StoreRootTestTemp $Base
    $root=[IO.Path]::Combine($Base,'OneStarMaker');[IO.Directory]::CreateDirectory($root)|Out-Null
    if($null -eq $Bytes){$Bytes=New-StoreRootTestMarkerBytes $Override}
    $pending=[IO.Path]::Combine($root,[Guid]::NewGuid().ToString('N')+'.pending');$marker=[IO.Path]::Combine($root,'store-root.json')
    $stream=[IO.FileStream]::new($pending,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{$stream.Write($Bytes,0,$Bytes.Length);$stream.Flush($true)}finally{$stream.Dispose()}
    [IO.File]::Move($pending,$marker,$true)
    return $root
}
# One fixture: <Work>\store-root\userprofile (with the marker unless -WithoutMarker)
# and <Work>\store-root\legacy (an empty former LocalApplicationData), both wired
# into the resolver's seams. Base and LegacyBase are spelled as the OS reports
# them, so a marker's physical path equals its logical path.
function New-StoreRootTestBase([string]$Work,[switch]$WithoutMarker){
    Assert-StoreRootTestTemp $Work
    $module=Import-StoreRootTestModule
    $base=[IO.Path]::Combine($Work,'store-root','userprofile');$legacy=[IO.Path]::Combine($Work,'store-root','legacy')
    foreach($directory in @($base,$legacy)){[IO.Directory]::CreateDirectory($directory)|Out-Null}
    $base=& $module {param($p)(Get-WindowsStorePathIdentity $p).physicalPath} $base
    $legacy=& $module {param($p)(Get-WindowsStorePathIdentity $p).physicalPath} $legacy
    $root=[IO.Path]::Combine($base,'OneStarMaker')
    # A child process sharing a parent's fixture reuses the marker the parent wrote.
    if(-not $WithoutMarker -and -not [IO.File]::Exists([IO.Path]::Combine($root,'store-root.json'))){$null=New-StoreRootTestMarker $base}
    Set-StoreRootTestSeams $base $legacy
    return [pscustomobject]@{Base=$base;LegacyBase=$legacy;Root=$root;LegacyRoot=[IO.Path]::Combine($legacy,'OneStarMaker');Module=$module}
}
# Names and attributes of every entry below a path, plus size and write time of
# files; equal before and after means nothing below it was created, removed or
# written (directory write times are left out: NTFS may report a directory's
# time from before its latest child was added). A missing path is ''.
function Get-StoreRootTestTree([string]$Path){
    if(-not [IO.Directory]::Exists($Path)){return ''}
    return (@(Get-ChildItem -LiteralPath $Path -Recurse -Force|Sort-Object FullName|ForEach-Object {$_.FullName+'|'+$_.Attributes+$(if($_ -is [IO.FileInfo]){'|'+$_.LastWriteTimeUtc.Ticks+'|'+$_.Length}else{''})}) -join "`n")
}
# The native marker operations with every call logged ('open:<path>',
# 'final-path', 'read', 'close'); $Final and $Open may replace single steps.
function New-StoreRootTestMarkerIo($Log,[scriptblock]$Open=$null,[scriptblock]$Final=$null){
    $native=& (@(Get-StoreRootTestModules)[0]) {$script:NativeMarkerIo}
    return @{
        Open={param([string]$Path)$Log.Add('open:'+$Path);if($Open){& $Open $Path}else{& $native.Open $Path}}.GetNewClosure()
        FinalPath={param($Handle)$Log.Add('final-path');if($Final){& $Final $Handle}else{& $native.FinalPath $Handle}}.GetNewClosure()
        Read={param($Handle)$Log.Add('read');& $native.Read $Handle}.GetNewClosure()
        Close={param($Handle)$Log.Add('close');& $native.Close $Handle}.GetNewClosure()
    }
}
# The native former-root query with every call logged ('query:<path>=<status>');
# $Inject maps a path to a status that replaces the native answer.
function New-StoreRootTestLegacyQuery($Log,[hashtable]$Inject=@{}){
    return {param([string]$Path)
        $status=$null;foreach($key in $Inject.Keys){if([string]::Equals($key,$Path,[StringComparison]::OrdinalIgnoreCase)){$status=$Inject[$key]}}
        if($null -eq $status){$status=[string][OneStarMaker.Workflow.StoreRootNative]::Query($Path)}
        $Log.Add('query:'+$Path+'='+$status);$status}.GetNewClosure()
}
