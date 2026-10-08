Set-StrictMode -Version Latest

# This is an invocation-local deadline, never a persisted policy clock or CLI input.
$script:Budget=$null
function Assert-EvidenceBudget { if($script:Budget -and (& $script:Budget.Remaining) -le 0){throw 'evidence-deadline'} }
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'EvidenceContract.psm1')
$script:TestRoot=$null
$script:WriteHook=$null
function Get-EvidenceRoot {
    $root=if($script:TestRoot){[IO.Path]::GetFullPath($script:TestRoot)}else{[IO.Path]::Combine([Environment]::GetFolderPath('LocalApplicationData'),'OneStarMaker','Artifacts','evidence-v2')}
    Assert-NoArtifactReparse $root
    return $root
}
function Get-EvidenceTaskDirectory($Config,[string]$TaskId){
    Assert-Hex $Config.Data.repositoryId 64;Assert-Hex $Config.Data.deploymentId 32;Assert-EvidenceTaskId $TaskId
    return [IO.Path]::Combine((Get-EvidenceRoot),$Config.Data.deploymentId,$Config.Data.repositoryId,'tasks',$TaskId)
}
function Initialize-EvidenceDirectory([string]$Path){
    $root=Get-EvidenceRoot
    if(-not (Test-ArtifactWithin $Path $root) -and $Path -cne $root){throw 'invalid-evidence-path'}
    Assert-NoArtifactReparse $Path
    $current=$root
    foreach($p in @('')+[IO.Path]::GetRelativePath($root,$Path).Split([IO.Path]::DirectorySeparatorChar)){
        if($p -and $p -ne '.'){$current=[IO.Path]::Combine($current,$p)}
        if(-not [IO.Directory]::Exists($current)){[IO.Directory]::CreateDirectory($current)|Out-Null;Set-ArtifactAcl $current $true}else{Assert-ArtifactAcl $current $true}
    }
}
function Write-EvidenceAtomic([string]$Path,$Value,[switch]$Immutable){
    Assert-EvidenceBudget
    Initialize-EvidenceDirectory ([IO.Path]::GetDirectoryName($Path));Assert-NoArtifactReparse $Path
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $Value -Compress -Depth 32))
    Assert-EvidenceBudget
    if($bytes.Length -lt 1 -or $bytes.Length -gt 1MB){throw 'record-limit'}
    $pending=$Path+'.'+[Guid]::NewGuid().ToString('N')+'.pending'
    if($script:WriteHook){& $script:WriteHook 'before-write' $Path}
    $f=[IO.FileStream]::new($pending,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{Assert-EvidenceBudget;$f.Write($bytes);$f.Flush($true);Assert-EvidenceBudget}finally{$f.Dispose()};Set-ArtifactAcl $pending $false
    if($script:WriteHook){& $script:WriteHook 'before-rename' $Path}
    Assert-EvidenceBudget;[IO.File]::Move($pending,$Path,(-not $Immutable));if($script:WriteHook){& $script:WriteHook 'after-rename' $Path};Assert-EvidenceBudget;Assert-ArtifactAcl $Path $false
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}
Export-ModuleMember -Function Get-EvidenceRoot,Get-EvidenceTaskDirectory,Initialize-EvidenceDirectory,Write-EvidenceAtomic
