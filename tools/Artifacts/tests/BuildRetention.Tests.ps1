param([string]$ResultPath = '', [string]$Case = '*')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# 既存のassert／結果保存だけを再利用し、Evidenceのstoreや実環境は生成しない。
# policyへ渡すものはこのscript内の非秘密メモリfixtureに限定する。
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../BuildRetentionPolicy.psm1')

$registered = @('ordinary-limit', 'protected-counts', 'failed-incomplete', 'protected-over-limit',
    'boundary-counts', 'ordinal-ties', 'invalid-snapshot', 'invalid-n', 'determinism')
$selected = @(if ($Case -ceq '*') { $registered } else { ([string]$Case).Split(',') })
# @()は単一caseでも配列を保つために必要。未知名や空指定は成功扱いせず、
# 下の実行結果へ失敗として残し、選択集合と実行集合を照合する。
$executed = [Collections.Generic.List[string]]::new()
$failed = [Collections.Generic.List[string]]::new()

function New-BuildRow([string]$Id, [DateTimeOffset]$At, [bool]$InUse = $false) {
    return [pscustomobject]@{buildId = $Id; publishState = 'succeeded'; publishedAt = $At; inUse = $InUse}
}

function Assert-Ids($Actual, [string[]]$Expected, [string]$Label) {
    # ID集合だけでなく、0/1件時の配列形状と決定的な順序も公開契約として検証する。
    Assert-EvidenceTest ($Actual -is [array]) "$Label is not an array"
    Assert-EvidenceTest ($Actual.Count -eq $Expected.Count) "$Label count"
    for ($i = 0; $i -lt $Expected.Count; $i++) {
        Assert-EvidenceTest ([StringComparer]::Ordinal.Equals($Actual[$i], $Expected[$i])) "$Label order"
    }
}

function Assert-Decision($Decision, [string]$Reason, [string[]]$Retained,
    [string[]]$Candidates, [string[]]$HeldIds, [string[]]$HeldReasons, [long]$Excess,
    [string[]]$SuccessfulIds) {
    Assert-EvidenceTest ($Decision -is [pscustomobject] -and $Decision.status -ceq 'evaluated') 'not evaluated'
    Assert-EvidenceTest ($Decision.reasonCode -ceq $Reason -and $Decision.excessCount -is [long] -and
        $Decision.excessCount -eq $Excess) 'reason or excess'
    Assert-Ids $Decision.retainedBuildIds $Retained 'retained'
    Assert-Ids $Decision.deleteCandidateBuildIds $Candidates 'candidates'
    Assert-EvidenceTest ($Decision.heldBuilds -is [array] -and $Decision.heldBuilds.Count -eq $HeldIds.Count) 'held count'
    for ($i = 0; $i -lt $HeldIds.Count; $i++) {
        Assert-EvidenceTest ($Decision.heldBuilds[$i].buildId -ceq $HeldIds[$i] -and
            $Decision.heldBuilds[$i].reasonCode -ceq $HeldReasons[$i]) 'held contents'
    }
    # 三分類の重複と、保持／候補への非成功Buildの混入を別々に検出する。
    $allIds = @($Decision.retainedBuildIds) + @($Decision.deleteCandidateBuildIds) +
        @($Decision.heldBuilds | ForEach-Object { $_.buildId })
    $unique = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($id in $allIds) { Assert-EvidenceTest ($unique.Add($id)) 'duplicate output ID' }
    $success = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($id in $SuccessfulIds) { $null = $success.Add($id) }
    Assert-EvidenceTest (($Retained.Count + $Candidates.Count) -eq $success.Count) 'success partition count'
    foreach ($id in @($Decision.retainedBuildIds) + @($Decision.deleteCandidateBuildIds)) {
        Assert-EvidenceTest ($success.Contains($id)) 'non-success in decision'
    }
}

function Assert-Blocked($Decision, [string]$Reason) {
    # 不正snapshotでは途中までの候補も出さない。超過0と未判定nullも区別する。
    Assert-EvidenceTest ($Decision.status -ceq 'blocked' -and $Decision.reasonCode -ceq $Reason) 'block reason'
    Assert-Ids $Decision.retainedBuildIds @() 'blocked retained'
    Assert-Ids $Decision.deleteCandidateBuildIds @() 'blocked candidates'
    Assert-Ids $Decision.heldBuilds @() 'blocked held'
    Assert-EvidenceTest ($null -eq $Decision.excessCount) 'blocked excess'
}

# 実時計を使わず、A→B→Cが新しい順になる固定UTCで境界を作る。
$t = [DateTimeOffset]::Parse('2026-01-01T00:00:00+00:00')
$a = New-BuildRow 'A' $t.AddHours(3)
$b = New-BuildRow 'B' $t.AddHours(2)
$c = New-BuildRow 'C' $t.AddHours(1)
$ids = @('A', 'B', 'C')
foreach ($caseName in $selected) {
    $executed.Add($caseName)
    try {
        Assert-EvidenceTest ($caseName -cin $registered) 'unknown-case'
        switch -CaseSensitive ($caseName) {
            'ordinary-limit' {
                $d = Get-BuildRetentionDecision 'sample/windows/dev' @($a, $b, $c) 2
                Assert-Decision $d 'candidates-found' @('A', 'B') @('C') @() @() 0 $ids
            }
            'protected-counts' {
                # 古い利用中CもN枠内に数える。最新A/BとCの計3件保持なら契約違反。
                $protectedC = New-BuildRow 'C' $t.AddHours(1) $true
                $d = Get-BuildRetentionDecision 'sample/windows/dev' @($a, $b, $protectedC) 2
                Assert-Decision $d 'candidates-found' @('A', 'C') @('B') @() @() 0 $ids
            }
            'failed-incomplete' {
                # 失敗・未完了の不要fieldは、欠落／不正／未来時刻でも成功枠に影響しない。
                $protectedC = New-BuildRow 'C' $t.AddHours(1) $true
                $variants = [Collections.Generic.List[object[]]]::new()
                $variants.Add([object[]]@([pscustomobject]@{buildId='F';publishState='failed'},
                    [pscustomobject]@{buildId='G';publishState='incomplete'}))
                $variants.Add([object[]]@([pscustomobject]@{buildId='F';publishState='failed';publishedAt=$null;inUse=$null},
                    [pscustomobject]@{buildId='G';publishState='incomplete';publishedAt='bad';inUse='false'}))
                $variants.Add([object[]]@([pscustomobject]@{buildId='F';publishState='failed';publishedAt=$t.AddYears(10);inUse=1},
                    [pscustomobject]@{buildId='G';publishState='incomplete';publishedAt=3;inUse=@()}))
                foreach ($pair in $variants) {
                    $d = Get-BuildRetentionDecision 'sample/windows/dev' @($a, $b, $protectedC, $pair[0], $pair[1]) 2
                    Assert-Decision $d 'candidates-found' @('A', 'C') @('B') @('F', 'G') @('publish-failed', 'publish-incomplete') 0 $ids
                }
            }
            'protected-over-limit' {
                # 保護数>Nでは超過を報告し、候補の有無によらず保護を維持する。
                $rows = @((New-BuildRow 'A' $t.AddHours(3) $true),
                    (New-BuildRow 'B' $t.AddHours(2) $true),
                    (New-BuildRow 'C' $t.AddHours(1) $true))
                $d = Get-BuildRetentionDecision 'sample/windows/dev' @($rows + (New-BuildRow 'D' $t)) 2
                Assert-Decision $d 'protected-over-limit' @('A','B','C') @('D') @() @() 1 @('A','B','C','D')
                $d = Get-BuildRetentionDecision 'sample/windows/dev' $rows 2
                Assert-Decision $d 'protected-over-limit' @('A','B','C') @() @() @() 1 $ids
            }
            'boundary-counts' {
                # 空・1件・N件・失敗だけを比較し、PowerShellの単一値展開も捕捉する。
                $f = [pscustomobject]@{buildId='F';publishState='failed'}
                $variants = [Collections.Generic.List[object[]]]::new()
                $variants.Add([object[]]@())
                $variants.Add([object[]]@($a))
                $variants.Add([object[]]@($a,$b))
                $variants.Add([object[]]@($f))
                foreach ($rows in $variants) {
                    $d = Get-BuildRetentionDecision 'sample/windows/dev' ([object[]]$rows) 2
                    $expected = @($rows | Where-Object { $_.publishState -ceq 'succeeded' } |
                        ForEach-Object { $_.buildId })
                    $held = @()
                    $heldReasons = @()
                    if ($rows.Count -eq 1 -and $rows[0].publishState -ceq 'failed') {
                        $held = @('F')
                        $heldReasons = @('publish-failed')
                    }
                    Assert-Decision $d 'within-limit' $expected @() $held $heldReasons 0 $expected
                }
            }
            'ordinal-ties' {
                # 同時刻の保持順と候補の逆順、大小文字、culture依存を切り分ける。
                $same = @((New-BuildRow 'B' $t), (New-BuildRow 'A' $t), (New-BuildRow 'C' $t))
                $d = Get-BuildRetentionDecision 'sample/windows/dev' ([object[]]@($same[0],$same[1])) 1
                Assert-Decision $d 'candidates-found' @('A') @('B') @() @() 0 @('A','B')
                $d = Get-BuildRetentionDecision 'sample/windows/dev' $same 1
                Assert-Decision $d 'candidates-found' @('A') @('C','B') @() @() 0 $ids
                $lower = @((New-BuildRow 'a' $t), (New-BuildRow 'A' $t))
                $originalCulture = [Globalization.CultureInfo]::CurrentCulture
                try {
                    foreach ($culture in @('en-US','tr-TR')) {
                        [Globalization.CultureInfo]::CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo($culture)
                        $d = Get-BuildRetentionDecision 'sample/windows/dev' $lower 1
                        Assert-Decision $d 'candidates-found' @('A') @('a') @() @() 0 @('A','a')
                    }
                } finally { [Globalization.CultureInfo]::CurrentCulture = $originalCulture }
            }
            'invalid-snapshot' {
                # Cの状態だけが不明でも、既知のA/Bを含めて判定全体を保留する。
                foreach ($value in @($null, 'false')) {
                    $bad = [pscustomobject]@{buildId='C';publishState='succeeded';publishedAt=$t;inUse=$value}
                    Assert-Blocked (Get-BuildRetentionDecision 'sample/windows/dev' @($a,$b,$bad) 2) 'invalid-build-snapshot'
                }
                $bad = [pscustomobject]@{buildId='C';publishState='unknown'}
                Assert-Blocked (Get-BuildRetentionDecision 'sample/windows/dev' @($a,$b,$bad) 2) 'invalid-build-snapshot'
            }
            'invalid-n' {
                # 暗黙変換の拒否、複数不正時の理由優先順、必要fieldの欠落を確認する。
                foreach ($n in @([object]0, [object](-1), [object]2.5, [object]'2', [object]$true, $null)) {
                    Assert-Blocked (Get-BuildRetentionDecision 'sample/windows/dev' @($a) $n) 'invalid-keep-count'
                }
                Assert-Blocked (Get-BuildRetentionDecision '' @($a) 2) 'invalid-series-id'
                Assert-Blocked (Get-BuildRetentionDecision 'sample/windows/dev' $null 2) 'invalid-build-list'
                Assert-Blocked (Get-BuildRetentionDecision '' $null 0) 'invalid-keep-count'
                Assert-Blocked (Get-BuildRetentionDecision '' $null 2) 'invalid-series-id'
                Assert-Blocked (Get-BuildRetentionDecision 'sample/windows/dev' @($a,$a) 2) 'invalid-build-snapshot'
                foreach ($bad in @($null,
                    [pscustomobject]@{publishState='succeeded';publishedAt=$t;inUse=$false},
                    [pscustomobject]@{buildId='C';publishState='succeeded';inUse=$false},
                    [pscustomobject]@{buildId='C';publishState='succeeded';publishedAt='2026-01-01';inUse=$false},
                    [pscustomobject]@{buildId='C';publishState='succeeded';publishedAt=$t.ToOffset([TimeSpan]::FromHours(1));inUse=$false},
                    [pscustomobject]@{buildId='C';publishedAt=$t;inUse=$false})) {
                    Assert-Blocked (Get-BuildRetentionDecision 'sample/windows/dev' @($a,$bad) 2) 'invalid-build-snapshot'
                }
            }
            'determinism' {
                # 全24順列×2cultureとInt64最大値で、順序と件数計算の安定性を見る。
                # 呼出前後の入力に加え、返値の書換えが元の行へ伝播しないことも確認する。
                $f = [pscustomobject]@{buildId='F';publishState='failed';publishedAt='ignored';inUse=$null}
                $rows = [object[]]@($a,$b,$c,$f)
                $before = ConvertTo-Json -InputObject $rows -Depth 8 -Compress
                $originalCulture = [Globalization.CultureInfo]::CurrentCulture
                try {
                    foreach ($culture in @('en-US','tr-TR')) {
                        [Globalization.CultureInfo]::CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo($culture)
                        foreach ($i in 0..3) { foreach ($j in 0..3) {
                            foreach ($k in 0..3) { foreach ($l in 0..3) {
                                $indices = [Collections.Generic.HashSet[int]]::new()
                                foreach ($index in @($i,$j,$k,$l)) { $null = $indices.Add($index) }
                                if ($indices.Count -ne 4) { continue }
                                $ordered = [object[]]@($rows[$i],$rows[$j],$rows[$k],$rows[$l])
                                $d = Get-BuildRetentionDecision 'sample/windows/dev' $ordered ([long]::MaxValue)
                                Assert-Decision $d 'within-limit' $ids @() @('F') @('publish-failed') 0 $ids
                            } }
                        }
                        }
                    }
                } finally { [Globalization.CultureInfo]::CurrentCulture = $originalCulture }
                Assert-EvidenceTest ((ConvertTo-Json -InputObject $rows -Depth 8 -Compress) -ceq $before) 'input mutated'
                $d.retainedBuildIds[0] = 'changed'
                $d.heldBuilds[0].buildId = 'changed'
                Assert-EvidenceTest ($a.buildId -ceq 'A' -and $f.buildId -ceq 'F') 'output aliases input'
                $bad = [object[]]@($a,$null)
                $beforeBad = ConvertTo-Json -InputObject $bad -Depth 8 -Compress
                Assert-Blocked (Get-BuildRetentionDecision 'sample/windows/dev' $bad 2) 'invalid-build-snapshot'
                Assert-EvidenceTest ((ConvertTo-Json -InputObject $bad -Depth 8 -Compress) -ceq $beforeBad) 'bad input mutated'
            }
        }
    } catch {
        $failed.Add($caseName)
        [Console]::Error.WriteLine($caseName + ': ' + $_.Exception.Message)
    }
}

if ($selected.Count -eq 0 -or $selected.Count -ne $executed.Count -or
    ($Case -ceq '*' -and $selected.Count -ne $registered.Count)) {
    $failed.Add('selection')
}
Complete-EvidenceTestSuite $ResultPath $registered $selected $executed $failed
[Console]::WriteLine("BuildRetention $($executed.Count), failed $($failed.Count)")
