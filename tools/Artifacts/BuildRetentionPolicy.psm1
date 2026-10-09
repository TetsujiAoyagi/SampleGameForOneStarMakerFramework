Set-StrictMode -Version Latest

function New-BuildRetentionResult($Status, $ReasonCode, $Retained, $Candidates, $Held, $Excess) {
    # 0件・1件でも分類先は必ず配列にする。呼出側が件数によって
    # null／単一値／配列を分岐せずに扱える返値の契約をここへ集約する。
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
    # snapshotは値を持つNotePropertyだけを受ける。ScriptProperty等を
    # 評価すると入力を読むだけで外部処理が走り、純粋policyの境界を越える。
    $property = $Row.PSObject.Properties[$Name]
    if ($null -eq $property -or $property.MemberType -ne [System.Management.Automation.PSMemberTypes]::NoteProperty) {
        return $null
    }
    return $property
}

# 呼出側は、明示した1系列の完全なsnapshotと、確定したpublish／利用状態を渡す。
# SeriesIdは識別子として受けるだけで、系列の生成・行の所属確認はここでは行わない。
# 返すのは保持対象・削除候補・保留理由であり、実DELETEの許可ではない。
# 実行側が削除直前の最新状態確認・排他・再試行を所有する。時計やstoreは参照しない。
function Get-BuildRetentionDecision {
    # 型付き引数による暗黙変換を避け、生の型を下で確認する。
    # 文字列の"2"や"false"を数値／boolへ読み替えて判定してはならない。
    param($SeriesId, $Builds, $KeepCount)

    # 複数の不正があっても理由はN→系列→一覧→行の順で一定にする。
    # N=0による全削除はこの契約に含めず、正のInt32/Int64だけを受ける。
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
    # 全行の検証が終わるまで候補を確定しない。不正な1行を無視すると、
    # 成功件数や利用中保護を少なく数える恐れがあるため、全体を空のblockedへ戻す。
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
        # 失敗・未完了は成功枠を消費せず、理由付きで保留する。
        # publishedAt/inUseは選別に不要なので読まない。欠落や不正でも、
        # 既存の成功Buildの判定を止める理由にはせず、後始末は別責務へ残す。
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
                # 成功行だけは時刻と利用状態が必須。時刻文字列の解釈や
                # timezoneの補完をせず、UTCのDateTimeOffsetと実boolを要求する。
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

    # 入力行を並べ替えず、必要な値を写した新規recordだけを整列する。
    # 成功は新しい順、同時刻はIDのOrdinal昇順。大小文字も区別して、
    # 入力順や実行環境のCurrentCultureに結果を左右させない。
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

    # 利用中は「最新N件とは別枠」ではなく、N件の保持枠を先に消費する。
    # 例: N=2、新しい順にA/B/CでCだけ利用中なら、残り1枠をAへ割り当てる。
    # A/Cが保持、Bが候補となる。保護だけでNを超えても保護は解除しない。
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
    # 保持は新しい順を維持し、候補だけを古い順へ反転する。
    # 同時刻の候補も上の全順序を反転するため、IDのOrdinal降順で一定になる。
    $candidates.Reverse()
    # 削除可能な分を候補にした後も残る、保護由来の超過だけを返す。
    # 候補の存在より保護超過の理由を優先し、N達成のための保護破りを促さない。
    [long]$excess = [Math]::Max([long]0, ($protectedCount - [long]$KeepCount))
    $reason = if ($excess -gt 0) { 'protected-over-limit' }
              elseif ($candidates.Count -gt 0) { 'candidates-found' }
              else { 'within-limit' }
    return New-BuildRetentionResult 'evaluated' $reason $retained.ToArray() $candidates.ToArray() $held.ToArray() $excess
}

Export-ModuleMember -Function Get-BuildRetentionDecision
