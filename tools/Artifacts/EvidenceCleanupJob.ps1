param([Parameter(Mandatory=$true)][string]$Runtime,[Parameter(Mandatory=$true)][string]$ManifestSha256)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
try{
    # Hash the immutable runtime before executing any copied module. The entry
    # uses only built-in reads here; domain adapters come after native binding.
    $manifestPath=[IO.Path]::Combine($Runtime,'runtime.json')
    if($ManifestSha256 -cnotmatch '^[a-f0-9]{64}$' -or (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $ManifestSha256){throw 'runtime-hash-mismatch'}
    $manifest=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($manifestPath)) -AsHashtable -DateKind String -Depth 30
    if($manifest.schemaVersion -ne 2 -or $manifest.ownerSid -cne [Security.Principal.WindowsIdentity]::GetCurrent().User.Value -or $manifest.implementationHead -cnotmatch '^[a-f0-9]{40}$' -or [IO.Path]::GetFileName($Runtime) -cne $manifest.implementationHead -or [IO.Path]::GetFullPath($PSCommandPath) -ine [IO.Path]::Combine($Runtime,'tools','Artifacts','EvidenceCleanupJob.ps1')){throw 'runtime-identity-mismatch'}
    $seen=@{}
    foreach($file in $manifest.files){
        if($file.path -isnot [string] -or $file.path -match '(^/|(^|/)\.{1,2}(/|$)|:|\\)' -or $seen.ContainsKey($file.path)){throw 'runtime-path-invalid'}
        $seen[$file.path]=$true;$path=[IO.Path]::Combine($Runtime,$file.path)
        if([IO.FileInfo]::new($path).Length -ne $file.bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $file.sha256){throw 'runtime-hash-mismatch'}
    }
    foreach($required in @('config.json','tools/Artifacts/EvidenceCleanupJob.ps1','tools/Artifacts/EvidenceSchedule.psm1','tools/Artifacts/ArtifactAcl.psm1','tools/Workflow/WindowsStorePaths.psm1')){if(-not $seen.ContainsKey($required)){throw 'runtime-path-invalid'}}
    $schedule=Import-Module (Join-Path $PSScriptRoot 'EvidenceSchedule.psm1') -PassThru
    $null=Initialize-EvidenceRuntimeContext $Runtime $ManifestSha256
    Import-Module (Join-Path $PSScriptRoot 'EvidenceStateStore.psm1')
    Import-Module (Join-Path $PSScriptRoot 'EvidenceCleanup.psm1')
    $result=Invoke-EvidenceScheduledJob $Runtime $ManifestSha256
    $result | ConvertTo-Json -Depth 10
    if($result.status -in @('partial','busy')){exit 2}
}catch{
    [ordered]@{status='failed';reasonCode='scheduler-runtime-unavailable'} | ConvertTo-Json -Compress
    exit 1
}
