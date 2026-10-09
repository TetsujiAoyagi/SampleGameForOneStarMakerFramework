Set-StrictMode -Version Latest

function New-BuildRetentionResult($Status, $ReasonCode, $Retained, $Candidates, $Held, $Excess) {
    return [pscustomobject]@{
        status = $Status
        reasonCode = $ReasonCode
        retainedBuildIds = [object[]]$Retained
        deleteCandidateBuildIds = [object[]]$Candidates
        heldBuilds = [object[]]$Held
        excessCount = $Excess
    }
}

function Get-BuildDataProperty($Row, [string]$Name) {
    $property = $Row.PSObject.Properties[$Name]
    if ($null -eq $property -or $property.MemberType -ne [System.Management.Automation.PSMemberTypes]::NoteProperty) {
        return $null
    }
    return $property
}

# The caller owns a complete, single-series snapshot. This function only classifies it;
# a delete executor must obtain a fresh snapshot and make its own guarded decision.
function Get-BuildRetentionDecision {
    param($SeriesId, $Builds, $KeepCount)

    if (($KeepCount -isnot [int] -and $KeepCount -isnot [long]) -or $KeepCount -lt 1) {
        return New-BuildRetentionResult 'blocked' 'invalid-keep-count' @() @() @() $null
    }
    if ($SeriesId -isnot [string] -or [string]::IsNullOrWhiteSpace($SeriesId)) {
        return New-BuildRetentionResult 'blocked' 'invalid-series-id' @() @() @() $null
    }
    if ($null -eq $Builds -or $Builds.GetType() -ne [object[]]) {
        return New-BuildRetentionResult 'blocked' 'invalid-build-list' @() @() @() $null
    }

    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $successful = [Collections.Generic.List[object]]::new()
    $held = [Collections.Generic.List[object]]::new()
    foreach ($row in $Builds) {
        if ($null -eq $row -or $row -isnot [pscustomobject]) {
            return New-BuildRetentionResult 'blocked' 'invalid-build-snapshot' @() @() @() $null
        }
        $idField = Get-BuildDataProperty $row 'buildId'
        $stateField = Get-BuildDataProperty $row 'publishState'
        if ($null -eq $idField -or $null -eq $stateField -or
            $idField.Value -isnot [string] -or [string]::IsNullOrWhiteSpace($idField.Value) -or
            $stateField.Value -isnot [string] -or -not $seen.Add($idField.Value)) {
            return New-BuildRetentionResult 'blocked' 'invalid-build-snapshot' @() @() @() $null
        }

        $id = $idField.Value
        switch -CaseSensitive ($stateField.Value) {
            'failed' {
                $held.Add([pscustomobject]@{buildId = $id; reasonCode = 'publish-failed'})
                break
            }
            'incomplete' {
                $held.Add([pscustomobject]@{buildId = $id; reasonCode = 'publish-incomplete'})
                break
            }
            'succeeded' {
                $timeField = Get-BuildDataProperty $row 'publishedAt'
                $useField = Get-BuildDataProperty $row 'inUse'
                if ($null -eq $timeField -or $null -eq $useField -or
                    $timeField.Value -isnot [DateTimeOffset] -or $useField.Value -isnot [bool]) {
                    return New-BuildRetentionResult 'blocked' 'invalid-build-snapshot' @() @() @() $null
                }
                if ($timeField.Value.Offset -ne [TimeSpan]::Zero) {
                    return New-BuildRetentionResult 'blocked' 'invalid-build-snapshot' @() @() @() $null
                }
                $successful.Add([pscustomobject]@{
                    buildId = $id
                    utcTicks = $timeField.Value.UtcTicks
                    inUse = $useField.Value
                })
                break
            }
            default {
                return New-BuildRetentionResult 'blocked' 'invalid-build-snapshot' @() @() @() $null
            }
        }
    }

    # Sort only new records. An ordinal ID tie break keeps the answer independent of
    # input order and CurrentCulture, including for case-distinct IDs.
    $successful.Sort([Comparison[object]]{
        param($a, $b)
        if ($a.utcTicks -gt $b.utcTicks) { return -1 }
        if ($a.utcTicks -lt $b.utcTicks) { return 1 }
        return [StringComparer]::Ordinal.Compare($a.buildId, $b.buildId)
    })
    $held.Sort([Comparison[object]]{
        param($a, $b)
        return [StringComparer]::Ordinal.Compare($a.buildId, $b.buildId)
    })

    [long]$protectedCount = 0
    foreach ($build in $successful) {
        if ($build.inUse) { $protectedCount++ }
    }
    [long]$unprotectedAllowance = [Math]::Max([long]0, ([long]$KeepCount - $protectedCount))
    $retained = [Collections.Generic.List[string]]::new()
    $candidates = [Collections.Generic.List[string]]::new()
    foreach ($build in $successful) {
        if ($build.inUse) {
            $retained.Add($build.buildId)
        } elseif ($unprotectedAllowance -gt 0) {
            $retained.Add($build.buildId)
            $unprotectedAllowance--
        } else {
            $candidates.Add($build.buildId)
        }
    }
    $candidates.Reverse()
    [long]$excess = [Math]::Max([long]0, ($protectedCount - [long]$KeepCount))
    $reason = if ($excess -gt 0) { 'protected-over-limit' }
              elseif ($candidates.Count -gt 0) { 'candidates-found' }
              else { 'within-limit' }
    return New-BuildRetentionResult 'evaluated' $reason $retained.ToArray() $candidates.ToArray() $held.ToArray() $excess
}

Export-ModuleMember -Function Get-BuildRetentionDecision
