param([Parameter(Mandatory=$true)][string]$Base,[Parameter(Mandatory=$true)][ValidateSet('write','read')][string]$Action)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# Host check for the store root (verification path V2): run once inside a
# packaged app (full access, same Windows user) with -Action write, and once from
# an ordinary process with -Action read, against the same temporary base the
# ordinary side created directly under the UserProfile Known Folder
# (<UserProfile>\OneStarMaker.HostCheck.<32 hex>, holding OneStarMaker\store-root.json).
# The resolver runs with its base seam replaced by that directory and its
# former-root seam pointed at an empty place beside it (V2 observes the marker,
# the base resolution and where a write lands; whether the real former root still
# holds a store is V1's question, not this one). The marker and ancestor checks
# are the production ones. Output is one
# JSON object; the two sides' values are compared offline (fixtures:
# tools/Workflow/tests/fixtures/storeroot-hostcheck-{match,mismatch}.json).
# Not a test suite and not a CLI: no store module is imported and nothing
# outside <base>\OneStarMaker\hostcheck is written.
$resolver=Import-Module (Join-Path $PSScriptRoot '../WindowsStoreRoot.psm1') -PassThru
$context=Import-Module (Join-Path $PSScriptRoot '../WindowsStoreContext.psm1') -PassThru
$userProfile=[Environment]::GetFolderPath('UserProfile')
if(-not [IO.Path]::GetFullPath($Base).StartsWith($userProfile.TrimEnd('\')+'\OneStarMaker.HostCheck.',[StringComparison]::OrdinalIgnoreCase)){throw 'base must be <UserProfile>\OneStarMaker.HostCheck.<hex>'}
$legacyBase=[IO.Path]::Combine([IO.Path]::GetFullPath($Base),'legacy')
& $resolver {param($b,$l)$script:BaseForTest=$b;$script:LegacyBaseForTest=$l} $Base $legacyBase
function Get-FinalPath([IO.FileStream]$Stream){return [OneStarMaker.Workflow.StoreRootNative]::FinalPath($Stream)}
$root=Get-OsmStoreRoot
$hostcheck=[IO.Path]::Combine($root,'hostcheck')
if($Action -ceq 'write'){
    [IO.Directory]::CreateDirectory($hostcheck)|Out-Null
    $logical=[IO.Path]::Combine($hostcheck,[Guid]::NewGuid().ToString('N')+'.txt')
    $stream=[IO.FileStream]::new($logical,[IO.FileMode]::CreateNew,[IO.FileAccess]::ReadWrite,[IO.FileShare]::Read)
    try{
        $bytes=[Text.UTF8Encoding]::new($false).GetBytes('hostcheck '+[DateTimeOffset]::UtcNow.ToString('o'));$stream.Write($bytes,0,$bytes.Length);$stream.Flush($true)
        $final=Get-FinalPath $stream
    }finally{$stream.Dispose()}
    # The raw probe result is recorded for the record only; it does not decide anything here.
    $storeContext=try{Get-OsmStoreContext}catch{'unknown'}
    ConvertTo-Json -Compress -InputObject ([ordered]@{action='write';userProfile=$userProfile;root=$root;written=[ordered]@{logicalPath=$logical;finalPath=$final};storeContext=$storeContext})
}else{
    $listing=@()
    if([IO.Directory]::Exists($hostcheck)){
        foreach($file in @([IO.Directory]::EnumerateFiles($hostcheck)|Sort-Object)){
            $stream=[IO.FileStream]::new($file,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
            try{$listing+=[ordered]@{name=[IO.Path]::GetFileName($file);logicalPath=$file;finalPath=(Get-FinalPath $stream)}}finally{$stream.Dispose()}
        }
    }
    # A packaged app's virtualized copy of the same relative location, if any.
    $local=[Environment]::GetFolderPath('LocalApplicationData');$relative=[IO.Path]::GetRelativePath($userProfile,$hostcheck)
    $hit=$false
    $packages=[IO.Path]::Combine($local,'Packages')
    if([IO.Directory]::Exists($packages)){
        foreach($package in [IO.Directory]::EnumerateDirectories($packages)){
            foreach($entry in $listing){if(Test-Path -LiteralPath ([IO.Path]::Combine($package,'LocalCache','Local',$relative,$entry.name))){$hit=$true}}
        }
    }
    ConvertTo-Json -Compress -Depth 5 -InputObject ([ordered]@{action='read';userProfile=$userProfile;root=$root;listing=@($listing);localCacheHit=$hit})
}
