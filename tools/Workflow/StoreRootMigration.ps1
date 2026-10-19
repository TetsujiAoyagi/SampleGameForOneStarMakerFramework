Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# Moves the store to <UserProfile>\OneStarMaker and writes the migration marker,
# once, on this host (StoreRootMigration.psm1). Takes no arguments, so nothing
# from the command line or the environment can point it elsewhere. Prints one
# JSON line {status, reasonCode, roles} without any path; exit 0 only on passed.
try{
    Import-Module (Join-Path $PSScriptRoot 'StoreRootMigration.psm1')
    $result=Invoke-OsmStoreRootMigration
}catch{
    # A failure to load the module may carry a path in its message.
    $result=[ordered]@{status='failed';reasonCode='migration-failed';roles=@()}
}
ConvertTo-Json -InputObject $result -Depth 5 -Compress
if($result.status -cne 'passed'){exit 1}
