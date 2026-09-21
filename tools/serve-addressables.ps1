<#
.SYNOPSIS
  Retired. Old Addressables HTTP serving is no longer the usual path.

.DESCRIPTION
  Content Delivery HTTP install replaced serve-addressables.ps1.
  This script fails closed and does not start an HTTP server.
#>

param(
    [int]$Port = 8080,
    [string]$Root = "unity/ServerData"
)

$ErrorActionPreference = "Stop"
Write-Error "serve-addressables.ps1 is retired. Use Tools/OSM/Content Delivery HTTP install instead of serving Addressables ServerData."
exit 1
