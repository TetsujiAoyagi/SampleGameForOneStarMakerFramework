<#
.SYNOPSIS
  Retired. Old Addressables remote rebuild is no longer the usual path.

.DESCRIPTION
  Content Directory build and Content Delivery replaced VariantRemoteBuildBatch.
  This script fails closed and does not pull, mutate Addressables groups, or write ServerData.
#>

param(
    [string]$UnityPath = "",
    [string]$ProjectPath = "unity",
    [string]$VariantProfile = "Assets/OneStarMaker/Editor/BuildProfiles/RemoteFull.asset"
)

$ErrorActionPreference = "Stop"
Write-Error "rebuild-remote.ps1 is retired. Use Tools/OSM/Content build and Content Delivery instead of Addressables remote catalog generation."
exit 1
