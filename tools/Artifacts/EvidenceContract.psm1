Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
# Registry discovery performs its own reparse check; do not rely on another caller's imports.
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot '../Workflow/WindowsStorePaths.psm1')
$script:DeploymentRoot = $null
function Get-EvidenceDeploymentRoot {
    $logical=[IO.Path]::Combine([Environment]::GetFolderPath('LocalApplicationData'),'OneStarMaker','Artifacts','deployments')
    $binding=[AppDomain]::CurrentDomain.GetData('OneStarMaker.Evidence.StorageBinding.v1')
    if($binding){
        $context=ConvertFrom-Json -InputObject $binding -AsHashtable -Depth 10
        if($context.schemaVersion -ne 1 -or $context.ownerSid -cne [Security.Principal.WindowsIdentity]::GetCurrent().User.Value -or -not $context.roles.ContainsKey('deployments')){throw 'deployment-root-unavailable'}
        $role=$context.roles.deployments
        $physical=Assert-WindowsStorePathIdentity $role.physicalPath $role
        Assert-NoArtifactReparse $physical;Assert-ArtifactAcl $physical $true
        return $physical
    }
    if($script:DeploymentRoot){return [IO.Path]::GetFullPath($script:DeploymentRoot)}
    Assert-NoArtifactReparse $logical
    if([IO.Directory]::Exists($logical)){
        $identity=Get-WindowsStorePathIdentity $logical
        if(-not $identity.isDirectory){throw 'deployment-root-unavailable'}
        Assert-NoArtifactReparse $identity.physicalPath
        return $identity.physicalPath
    }
    return $logical
}
function Assert-EvidenceTaskId([string] $TaskId) { if ($TaskId -cnotmatch '\A[a-z0-9][a-z0-9-]{0,63}\z') { throw 'invalid-task' } }
function Read-EvidenceConfig([string] $Path) {
    $item=Read-ArtifactJson $Path; $v=$item.Data
    if ($v.schemaVersion -isnot [long] -or $v.schemaVersion -ne 2) { throw 'unsupported-schema' }
    Assert-Fields $v @('schemaVersion','profile','endpoint','bucket','repositoryId','prefix','policy','deploymentId')
    if ($v.profile -cne 'osm' -or $v.bucket -cne 'osm-artifacts' -or $v.endpoint -cnotmatch '\Ahttps://[0-9a-f]{32}\.r2\.cloudflarestorage\.com\z' -or $v.prefix -cne 'development/evidence/v2/' -or $v.policy -cne 'task-end-30d-v1') { throw 'unsupported-schema' }
    Assert-Hex $v.repositoryId 64; Assert-Hex $v.deploymentId 32
    $root=Get-EvidenceDeploymentRoot
    $receipt=Read-ArtifactJson ([IO.Path]::Combine($root,$v.deploymentId+'.json'))
    Assert-Fields $receipt.Data @('schemaVersion','deploymentId','repositoryId','configSha256','baselineRecordId','baselineSha256','ownerObservationId','ownerObservationSha256')
    if($receipt.Data.schemaVersion -ne 2 -or $receipt.Data.deploymentId -cne $v.deploymentId -or $receipt.Data.repositoryId -cne $v.repositoryId -or $receipt.Data.configSha256 -cne $item.Sha256){throw 'unknown-deployment'}
    foreach($name in @('baselineSha256','ownerObservationSha256')){Assert-Hex $receipt.Data.$name 64}
    foreach($name in @('baselineRecordId','ownerObservationId')){if($receipt.Data.$name -cnotmatch '\A[A-Za-z0-9-]{1,128}\z'){throw 'unknown-deployment'}}
    return @{Data=$v;Hash=$item.Sha256;ProfileId=$v.deploymentId}
}
function Assert-EvidenceSelection($Selection,[string]$OwnedStagingRoot=$null) {
    if($Selection.schemaVersion -isnot [long] -or $Selection.schemaVersion -ne 2){throw 'unsupported-schema'}
    Assert-Fields $Selection @('schemaVersion','purpose','taskId','root','files','base','head')
    Assert-EvidenceTaskId $Selection.taskId; Assert-Hex $Selection.base 40; Assert-Hex $Selection.head 40
    if($Selection.purpose -cne 'evidence' -or $Selection.root -isnot [string] -or -not [IO.Path]::IsPathFullyQualified($Selection.root) -or $Selection.files -isnot [array] -or $Selection.files.Count -lt 1 -or $Selection.files.Count -gt 4096){throw 'invalid-selection'}
    $root=[IO.Path]::GetFullPath($Selection.root)
    if(-not [IO.Directory]::Exists($root) -or [OneStarMaker.Artifacts.Packaging.PackagePolicy]::HasReparse($root) -or ([OneStarMaker.Artifacts.Packaging.PackagePolicy]::IsProtectedPath($root) -and (-not $OwnedStagingRoot -or $root -cne [IO.Path]::GetFullPath($OwnedStagingRoot)))){throw 'invalid-source'}
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($file in $Selection.files){if($file -isnot [string] -or -not $seen.Add($file)){throw 'invalid-selection'}; $null=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Relative($file); $null=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($root,$file)}
}
function Get-EvidenceKey($Config,[string]$TaskId,[string]$ArtifactId){Assert-EvidenceTaskId $TaskId;Assert-Hex $ArtifactId 32;return "$($Config.Data.prefix)$($Config.Data.repositoryId)/$TaskId/$ArtifactId/bundle.zip"}
function Assert-EvidenceReference($v,$Config){
    Assert-Fields $v @('schemaVersion','deploymentId','repositoryId','taskId','artifactId','key','base','head','packageBytes','manifestSha256')
    if($v.schemaVersion -isnot [long] -or $v.schemaVersion -ne 2 -or $v.deploymentId -cne $Config.Data.deploymentId -or $v.repositoryId -cne $Config.Data.repositoryId){throw 'unsupported-schema'}
    Assert-EvidenceTaskId $v.taskId;Assert-Hex $v.artifactId 32;Assert-Hex $v.base 40;Assert-Hex $v.head 40;Assert-Hex $v.manifestSha256 64
    if($v.key -cne (Get-EvidenceKey $Config $v.taskId $v.artifactId) -or ($v.packageBytes -isnot [long] -and $v.packageBytes -isnot [int]) -or $v.packageBytes -lt 1 -or $v.packageBytes -gt 256MB){throw 'invalid-reference'}
}
function Read-EvidenceReference([string]$Text,$Config){
    if(-not $Text.StartsWith('osm-evidence-v2:',[StringComparison]::Ordinal) -or $Text.Length -gt 4096){throw 'unsupported-schema'}
    $s=$Text.Substring(16);if($s -cnotmatch '\A[A-Za-z0-9_-]+\z'){throw 'invalid-reference'}
    $b=$s.Replace('-','+').Replace('_','/');$b=$b.PadRight($b.Length+((4-$b.Length%4)%4),'=');$bytes=[Convert]::FromBase64String($b)
    if([Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+','-').Replace('/','_') -cne $s){throw 'invalid-reference'}
    $raw=[Text.UTF8Encoding]::new($false,$true).GetString($bytes);$doc=[Text.Json.JsonDocument]::Parse($raw)
    try{Assert-JsonDuplicates $doc.RootElement}finally{$doc.Dispose()}
    $v=$raw|ConvertFrom-Json -Depth 8 -DateKind String;Assert-EvidenceReference $v $Config;return $v
}
function New-EvidenceReference($Config,$Artifact){
    $v=[ordered]@{schemaVersion=2;deploymentId=$Config.Data.deploymentId;repositoryId=$Config.Data.repositoryId;taskId=$Artifact.taskId;artifactId=$Artifact.artifactId;key=$Artifact.key;base=$Artifact.base;head=$Artifact.head;packageBytes=$Artifact.packageBytes;manifestSha256=$Artifact.manifestSha256}
    $raw=ConvertTo-Json -InputObject $v -Compress;return 'osm-evidence-v2:'+ [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($raw)).TrimEnd('=').Replace('+','-').Replace('/','_')
}
Export-ModuleMember -Function Read-EvidenceConfig,Assert-EvidenceSelection,Assert-EvidenceTaskId,Read-EvidenceReference,New-EvidenceReference,Get-EvidenceKey
function Get-EvidenceConfigurations([string]$RepositoryId){
    Assert-Hex $RepositoryId 64
    $root=Get-EvidenceDeploymentRoot
    Assert-NoArtifactReparse $root
    if(-not [IO.Directory]::Exists($root)){return @()}
    foreach($path in [IO.Directory]::EnumerateFiles($root,'*.config.json')){try{$c=Read-EvidenceConfig $path;if($c.Data.repositoryId -ceq $RepositoryId){$path}}catch{throw 'unknown-deployment'}}
}
Export-ModuleMember -Function Read-EvidenceConfig,Assert-EvidenceSelection,Assert-EvidenceTaskId,Read-EvidenceReference,New-EvidenceReference,Get-EvidenceKey,Get-EvidenceConfigurations
