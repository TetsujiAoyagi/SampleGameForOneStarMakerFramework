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

# 行の検証と全順序は、件数保持と退役の両方の選別が共有する（行の形と順序の知識を1箇所に保つ）。
# 不正ならReasonに理由語を、正しければ整列済みの成功（新しい順）と保留（IDのOrdinal昇順）を返す。
function Read-BuildRetentionRows($SeriesId, $Builds) {
    if ($SeriesId -isnot [string] -or [string]::IsNullOrWhiteSpace($SeriesId)) {
        return @{Reason = 'invalid-series-id'}
    }
    if ($null -eq $Builds -or $Builds.GetType() -ne [object[]]) {
        return @{Reason = 'invalid-build-list'}
    }

    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $successful = [Collections.Generic.List[object]]::new()
    $held = [Collections.Generic.List[object]]::new()
    # 全行の検証が終わるまで候補を確定しない。不正な1行を無視すると、
    # 成功件数や利用中保護を少なく数える恐れがあるため、全体を空のblockedへ戻す。
    foreach ($row in $Builds) {
        if ($null -eq $row -or $row -isnot [pscustomobject]) {
            return @{Reason = 'invalid-build-snapshot'}
        }
        $idField = Get-BuildDataProperty $row 'buildId'
        $stateField = Get-BuildDataProperty $row 'publishState'
        if ($null -eq $idField -or $null -eq $stateField -or
            $idField.Value -isnot [string] -or [string]::IsNullOrWhiteSpace($idField.Value) -or
            $stateField.Value -isnot [string] -or -not $seen.Add($idField.Value)) {
            return @{Reason = 'invalid-build-snapshot'}
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
                    return @{Reason = 'invalid-build-snapshot'}
                }
                if ($timeField.Value.Offset -ne [TimeSpan]::Zero) {
                    return @{Reason = 'invalid-build-snapshot'}
                }
                $successful.Add([pscustomobject]@{
                    buildId = $id
                    utcTicks = $timeField.Value.UtcTicks
                    inUse = $useField.Value
                })
                break
            }
            default {
                return @{Reason = 'invalid-build-snapshot'}
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
    return @{Reason = $null; Successful = $successful; Held = $held}
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
    $rows = Read-BuildRetentionRows $SeriesId $Builds
    if ($rows.Reason) {
        return New-BuildRetentionResult 'blocked' $rows.Reason @() @() @() $null
    }
    $successful = $rows.Successful
    $held = $rows.Held

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

# 系列の退役の選別。Nを使わず、保護中でない成功を全て候補（古い順）にし、保護中の成功は
# reasonCode 'protected'、失敗・未完了はこれまでどおりの理由で保留する（退役は保護を外さない）。
# Get-BuildRetentionDecisionにN=0を渡して退役を表さないのは、件数清掃で0を渡す誤りを止める
# 入口検査（KeepCount >= 1）を外すことになるため。行の形・全順序・blockedの返し方は同じ。
# reasonCode: candidates-found（候補あり）／retired-held（候補は無く保留が残る）／retired（どちらも無い）。
function Get-SeriesRetirementDecision {
    param($SeriesId, $Builds)

    $rows = Read-BuildRetentionRows $SeriesId $Builds
    if ($rows.Reason) {
        return New-BuildRetentionResult 'blocked' $rows.Reason @() @() @() $null
    }
    $candidates = [Collections.Generic.List[string]]::new()
    $held = [Collections.Generic.List[object]]::new()
    foreach ($item in $rows.Held) { $held.Add($item) }
    foreach ($build in $rows.Successful) {
        if ($build.inUse) {
            $held.Add([pscustomobject]@{buildId = $build.buildId; reasonCode = 'protected'})
        } else {
            $candidates.Add($build.buildId)
        }
    }
    # 候補は全順序（新しい順）の逆で、削除は最古から。保留は種類によらずIDのOrdinal昇順。
    $candidates.Reverse()
    $held.Sort([Comparison[object]]{
        param($a, $b)
        return [StringComparer]::Ordinal.Compare($a.buildId, $b.buildId)
    })
    $reason = if ($candidates.Count -gt 0) { 'candidates-found' }
              elseif ($held.Count -gt 0) { 'retired-held' }
              else { 'retired' }
    return New-BuildRetentionResult 'evaluated' $reason @() $candidates.ToArray() $held.ToArray() ([long]0)
}

Export-ModuleMember -Function Get-BuildRetentionDecision, Get-SeriesRetirementDecision
