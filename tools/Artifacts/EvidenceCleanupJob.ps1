param([Parameter(Mandatory=$true)][string]$Runtime,[Parameter(Mandatory=$true)][string]$ManifestSha256)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
try{
    # Validate every fixed byte before importing adapters that can reach storage.
    $schedule=Import-Module (Join-Path $PSScriptRoot 'EvidenceSchedule.psm1') -PassThru
    $null=& $schedule {param($r,$h)Assert-EvidenceRuntime $r $h} $Runtime $ManifestSha256
    Import-Module (Join-Path $PSScriptRoot 'EvidenceStateStore.psm1')
    Import-Module (Join-Path $PSScriptRoot 'EvidenceCleanup.psm1')
    $result=Invoke-EvidenceScheduledJob $Runtime $ManifestSha256
    $result | ConvertTo-Json -Depth 10
    if($result.status -in @('partial','busy')){exit 2}
}catch{
    [ordered]@{status='failed';reasonCode='scheduler-runtime-unavailable'} | ConvertTo-Json -Compress
    exit 1
}
