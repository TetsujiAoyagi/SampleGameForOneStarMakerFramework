Set-StrictMode -Version Latest

# This is an invocation-local deadline, never a persisted policy clock or CLI input.
$script:Budget=$null
function Assert-EvidenceBudget { if($script:Budget -and (& $script:Budget.Remaining) -le 0){throw 'evidence-deadline'} }
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'EvidenceContract.psm1')
Import-Module (Join-Path $PSScriptRoot '../Workflow/WindowsStorePaths.psm1')
$script:TestRoot=$null
$script:WriteHook=$null
function Get-EvidenceRoot {
    $logical=if($script:TestRoot){[IO.Path]::GetFullPath($script:TestRoot)}else{[IO.Path]::Combine([Environment]::GetFolderPath('LocalApplicationData'),'OneStarMaker','Artifacts','evidence-v2')}
    # A scheduled invocation binds this store before any adapter opens a task.
    # The process slot survives module reimport; it never comes from CLI or env.
    $binding=[AppDomain]::CurrentDomain.GetData('OneStarMaker.Evidence.StorageBinding.v1')
    if($binding){
        $context=ConvertFrom-Json -InputObject $binding -AsHashtable -Depth 10
        if($context.schemaVersion -ne 1 -or $context.ownerSid -cne [Security.Principal.WindowsIdentity]::GetCurrent().User.Value -or -not $context.roles.ContainsKey('evidence')){throw 'evidence-root-unavailable'}
        $role=$context.roles.evidence
        $physical=Assert-WindowsStorePathIdentity $role.physicalPath $role
        Assert-NoArtifactReparse $physical;Assert-ArtifactAcl $physical $true
        return $physical
    }
    Assert-NoArtifactReparse $logical
    if(-not $script:TestRoot -and [IO.Directory]::Exists($logical)){
        $identity=Get-WindowsStorePathIdentity $logical
        if(-not $identity.isDirectory){throw 'evidence-root-unavailable'}
        Assert-NoArtifactReparse $identity.physicalPath
        return $identity.physicalPath
    }
    return $logical
}
# This resolver changes only the I/O address. It leaves the immutable receipt
# and catalog path bytes intact, and cannot remap source or another role.
function Resolve-EvidenceStoredPath([string]$Path,[switch]$AllowMissing){
    if([string]::IsNullOrWhiteSpace($Path) -or -not [IO.Path]::IsPathFullyQualified($Path) -or $Path -match '(^|[\\/])\.\.?([\\/]|$)|[\x00-\x1f]'){throw 'invalid-evidence-path'}
    $full=[IO.Path]::GetFullPath($Path)
    if($full -cne $Path -or $Path.Contains('/')){throw 'invalid-evidence-path'}
    foreach($segment in $full.Substring([IO.Path]::GetPathRoot($full).Length).Split([char[]]@('\','/'),[StringSplitOptions]::RemoveEmptyEntries)){if($segment.Contains(':') -or $segment -cne $segment.TrimEnd(' ','.')){throw 'invalid-evidence-path'}}
    $root=Get-EvidenceRoot
    $logical=if($script:TestRoot){[IO.Path]::GetFullPath($script:TestRoot)}else{[IO.Path]::Combine([Environment]::GetFolderPath('LocalApplicationData'),'OneStarMaker','Artifacts','evidence-v2')}
    $binding=[AppDomain]::CurrentDomain.GetData('OneStarMaker.Evidence.StorageBinding.v1')
    if($binding){$logical=(ConvertFrom-Json -InputObject $binding -AsHashtable -Depth 10).roles.evidence.logicalPath}
    $parent=if(Test-ArtifactWithin $full $root){$root}elseif(Test-ArtifactWithin $full $logical){$logical}else{throw 'invalid-evidence-path'}
    $relative=[IO.Path]::GetRelativePath($parent,$full)
    if($relative -ceq '.'){throw 'invalid-evidence-path'}
    $target=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$relative))
    if(-not (Test-ArtifactWithin $target $root)){throw 'invalid-evidence-path'}
    Assert-NoArtifactReparse $target;Assert-ArtifactAcl $root $true
    if([IO.File]::Exists($target) -or [IO.Directory]::Exists($target)){
        $actual=Get-WindowsStorePathIdentity $target
        if($actual.physicalPath -ine $target -or -not (Test-ArtifactWithin $actual.physicalPath $root)){throw 'invalid-evidence-path'}
        $current=$root
        foreach($segment in $relative.Split([IO.Path]::DirectorySeparatorChar)){$current=[IO.Path]::Combine($current,$segment);Assert-ArtifactAcl $current ([IO.Directory]::Exists($current))}
        # When the alias is visible, prove it names this file rather than a
        # different overlay entry. An invisible alias in an OS job uses its binding.
        if($full -ine $target -and ([IO.File]::Exists($full) -or [IO.Directory]::Exists($full))){
            $alias=Get-WindowsStorePathIdentity $full
            if($alias.volumeSerial -cne $actual.volumeSerial -or $alias.fileId -cne $actual.fileId){throw 'invalid-evidence-path'}
        }
        return $actual.physicalPath
    }
    if(-not $AllowMissing){throw 'evidence-path-missing'}
    $existing=[IO.Path]::GetDirectoryName($target)
    while($existing -and (Test-ArtifactWithin $existing $root)){
        if([IO.Directory]::Exists($existing)){Assert-ArtifactAcl $existing $true}
        if($existing -ceq $root){break};$existing=[IO.Path]::GetDirectoryName($existing)
    }
    return $target
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
Export-ModuleMember -Function Resolve-EvidenceStoredPath,Get-EvidenceRoot,Get-EvidenceTaskDirectory,Initialize-EvidenceDirectory,Write-EvidenceAtomic
