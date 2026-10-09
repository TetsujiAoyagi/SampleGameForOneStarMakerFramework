Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
# The policy reads snapshots only. All callers must obtain a fresh task snapshot under its guard.
function Get-EvidenceRetentionDecision($Task,$Artifact,[DateTimeOffset]$Now){
    $reason='unknown-state'
    try{
        if($null -eq $Task -or $null -eq $Artifact){return [pscustomobject]@{eligible=$false;reasonCode=$reason}}
        if($Artifact.state -ceq 'deleted'){$reason='payload-deleted'}
        elseif($Artifact.state -cne 'ready'){$reason='unconfirmed-artifact'}
        elseif($Task.status -ceq 'pending-gap' -or $Task.appliedEventVersion -ne $Artifact.appliedEventVersion){$reason='delivery-pending'}
        elseif($Task.status -cne 'ended' -or $null -eq $Task.taskEndedAt){$reason='active/end-not-recorded'}
        elseif($Artifact.deleteIntent -or $Artifact.operation){$reason='operation-pending'}
        elseif($Task.taskEndedAt -cne $Artifact.taskEndedAt -or $Task.deleteEligibleAt -cne $Artifact.deleteEligibleAt){$reason='unknown-state'}
        else{
            $ended=Assert-Utc $Task.taskEndedAt;$eligible=Assert-Utc $Task.deleteEligibleAt
            if($eligible -ne $ended.AddSeconds(2592000)){$reason='unknown-state'}
            else{
                $reason='expired'
                foreach($p in $Artifact.protections){
                    if($p.owner -isnot [string] -or $p.consumer -isnot [string] -or $p.reason -isnot [string]){$reason='unknown-use';break}
                    $created=Assert-Utc $p.createdAt;$until=Assert-Utc $p.until
                    if($until -le $created){$reason='unknown-use';break}
                    if($p.releasedAt){$released=Assert-Utc $p.releasedAt;if($released -lt $created){$reason='unknown-use';break}}
                    elseif($Now -lt $until){$reason='in-use';break}
                }
                if($reason -ceq 'expired' -and $Now -lt $eligible){$reason='retained'}
            }
        }
    }catch{$reason='unknown-state'}
    return [pscustomobject]@{eligible=($reason -ceq 'expired');reasonCode=$reason}
}
function Get-EvidenceStagingDecision($Staging,[DateTimeOffset]$Now){
    try{if($Staging.origin -cne 'storage-copy' -or $Staging.adopted -or $Staging.adoptionPending -or $Staging.operation -or $Staging.deleteIntent -or @($Staging.protections).Count){return [pscustomobject]@{eligible=$false;reasonCode='staging-protected'}}
        $at=Assert-Utc $Staging.createdAt;return [pscustomobject]@{eligible=($Now -ge $at.AddSeconds(604800));reasonCode=if($Now -ge $at.AddSeconds(604800)){'staging-expired'}else{'staging-retained'}}
    }catch{return [pscustomobject]@{eligible=$false;reasonCode='unknown-staging'}}
}
Export-ModuleMember -Function Get-EvidenceRetentionDecision,Get-EvidenceStagingDecision
