param([Parameter(Mandatory=$true)][string]$StoreBase,[Parameter(ValueFromRemainingArguments=$true)][string[]]$CliArgs)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# Runs the public artifacts.ps1 in a fresh process that passes the store-root
# gate: the resolver's seams point at a temporary, migrated store root below
# $StoreBase (under temp). For cases that exercise only the entry's grammar or
# its output hygiene and never reach a store module.
. (Join-Path $PSScriptRoot '../../Workflow/tests/StoreRootTestSupport.ps1')
$null=New-StoreRootTestBase $StoreBase
& (Join-Path $PSScriptRoot '../../artifacts.ps1') @CliArgs
exit $LASTEXITCODE
