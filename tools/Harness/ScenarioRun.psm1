Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 実機の検証の run の永続する状態を所有する: 名前と RunId の検査、run の root と印、run の lease、
# 続きの実行の照合、試行の番号、record（driver.jsonl）、証拠の保存、外部 root、管理する file。
# 「何を書いてよいか・消してよいか」の判断をこの module に集める。git も process も扱わない。
#
# 所有の規則（tools/Harness/README.md の「実機の検証の scenario」）:
# - run の root は、この module がその run で作って印を置いた物だけを書き込みと削除の対象にする。
# - 印は「無ければ作る」の 1 回の操作（CreateNew）で作り、既にあれば新しい run にしない。
# - 削除は、印のある外部 root の下だけで、reparse point を辿らない。

$script:NamePattern = '^[A-Za-z0-9]+([._-][A-Za-z0-9]+)*$'
$script:ReservedDeviceNames = @('CON', 'PRN', 'AUX', 'NUL', 'CONIN$', 'CONOUT$') + @(1..9 | ForEach-Object { "COM$_"; "LPT$_" }) + @('COM¹', 'COM²', 'COM³', 'LPT¹', 'LPT²', 'LPT³')
$script:RunMarkerName = 'scenario-run.json'
$script:RunLeaseName = 'scenario-run.lease'
$script:ExternalMarkerName = 'scenario-external.json'
$script:ExternalRemovedName = 'scenario-external-removed.json'
$script:RecordName = 'driver.jsonl'
$script:Utf8 = [Text.UTF8Encoding]::new($false)

function Assert-ScenarioName {
    # scenario・checkout・用途の directory の名前と RunId は path の成分になる。区切り、`.`・`..`、
    # 末尾の `.`、Windows の予約された device の名前（拡張子付きを含む）を拒み、決めた親の外へ出さない。
    param([AllowNull()] [AllowEmptyString()] [string]$Value, [Parameter(Mandatory)] [string]$Kind, [int]$MaxLength = 32)
    if ([string]::IsNullOrEmpty($Value)) { throw "$Kind が空です" }
    if ($Value.Length -gt $MaxLength) { throw "$Kind は $MaxLength 文字までです: $Value" }
    if ($Value -cnotmatch $script:NamePattern) { throw "$Kind に使えない文字か形があります（英数字と、間の . _ - だけ）: $Value" }
    $stem = $Value.Split('.')[0]
    foreach ($reserved in $script:ReservedDeviceNames) {
        if ([string]::Equals($stem, $reserved, [StringComparison]::OrdinalIgnoreCase)) { throw "$Kind に Windows の予約名は使えません: $Value" }
    }
}

function Test-ScenarioPathUnder {
    param([string]$Parent, [string]$Target)
    $p = [IO.Path]::GetFullPath($Parent).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $t = [IO.Path]::GetFullPath($Target)
    return $t.StartsWith($p, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-ScenarioNoReparsePoint {
    # Parent から Target までの、存在する各成分（Parent 自身を含む）が reparse point でないことを確かめる。
    # junction で親を差し替えて、決めた親の外を書かせ・消させないため。
    param([Parameter(Mandatory)] [string]$Parent, [Parameter(Mandatory)] [string]$Target)
    $parentFull = [IO.Path]::GetFullPath($Parent).TrimEnd('\', '/')
    $targetFull = [IO.Path]::GetFullPath($Target).TrimEnd('\', '/')
    if (-not ($targetFull -ieq $parentFull) -and -not (Test-ScenarioPathUnder $parentFull $targetFull)) { throw "path が決めた親の外にあります: $targetFull（親 $parentFull）" }
    $current = $parentFull
    $relative = $targetFull.Substring($parentFull.Length).TrimStart('\', '/')
    $parts = @($parentFull) + @(if ($relative) { $relative.Split([char[]]@('\', '/')) })
    for ($i = 0; $i -lt $parts.Count; $i++) {
        if ($i -gt 0) { $current = Join-Path $current $parts[$i] }
        if ([IO.File]::Exists($current) -or [IO.Directory]::Exists($current)) {
            $attributes = [IO.File]::GetAttributes($current)
            if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "path の途中に reparse point があります: $current" }
        }
    }
}

function Write-ScenarioNewFile {
    # 既にある file は上書きしない（CreateNew）。同じ試行の同じ名前を 2 回書けないことはここで決まる。
    param([string]$Path, [byte[]]$Bytes)
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($Bytes, 0, $Bytes.Length) } finally { $stream.Dispose() }
}

function ConvertTo-ScenarioBytes {
    # 配列を返す時は単項の , で包む（空の配列が $null に、byte[] が object[] に化けないように）。
    param([AllowNull()] [object]$Content)
    if ($null -eq $Content) { return ,([byte[]]@()) }
    if ($Content -is [byte[]]) { return ,$Content }
    if ($Content -is [Collections.IEnumerable] -and $Content -isnot [string]) { return ,($script:Utf8.GetBytes((@($Content) -join "`n"))) }
    return ,($script:Utf8.GetBytes([string]$Content))
}

function Get-ScenarioSha256([byte[]]$Bytes) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}

function ConvertTo-ScenarioCanonical {
    # 印の照合に使う、key の順に依らない文字列。
    param([AllowNull()] [object]$Value)
    return (ConvertTo-Json -InputObject $Value -Compress -Depth 8)
}

function Open-ScenarioRun {
    # run を始める（新規）か、続ける。返す object は run の lease（排他で開いた file）を持ち、
    # Close-ScenarioRun で離す。process が落ちれば OS が離す。
    # 新規: root が既にあれば拒む。root を作り、lease を CreateNew で取ってから印を書く。
    # 続き: lease を排他で開き、印の全ての欄が今回と同じ時だけ通す。
    param(
        [Parameter(Mandatory)] [string]$Root,
        [Parameter(Mandatory)] [string]$Scenario,
        [Parameter(Mandatory)] [string]$RunId,
        [Parameter(Mandatory)] [ValidateSet('run', 'dry-run')] [string]$Mode,
        [AllowNull()] [AllowEmptyString()] [string]$Commit,
        [Parameter(Mandatory)] [Collections.IDictionary]$Checkouts,
        [bool]$Continue = $false,
        [string]$ExternalDrive = ''
    )
    Assert-ScenarioName $Scenario 'scenario の名前'
    Assert-ScenarioName $RunId 'RunId'
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $identity = [ordered]@{ schemaVersion=1; scenario=$Scenario; runId=$RunId; mode=$Mode; commit=$(if ($Commit) { $Commit } else { $null }); checkouts=[ordered]@{} }
    foreach ($key in $Checkouts.Keys) { $identity.checkouts[[string]$key] = $Checkouts[$key] }
    $markerPath = Join-Path $rootFull $script:RunMarkerName
    $leasePath = Join-Path $rootFull $script:RunLeaseName
    $lease = $null
    $continued = $false
    if ([IO.Directory]::Exists($rootFull) -or [IO.File]::Exists($rootFull)) {
        if (-not $Continue) { throw "run の root が既にあります。同じ RunId で続けるには -Only を付けます: $rootFull" }
        if (([IO.File]::GetAttributes($rootFull) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "run の root が reparse point です: $rootFull" }
        if (-not [IO.File]::Exists($markerPath) -or -not [IO.File]::Exists($leasePath)) { throw "run の root に基盤の印がありません（基盤が作った run ではありません）: $rootFull" }
        try { $lease = [IO.File]::Open($leasePath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
        catch [IO.IOException] { throw "同じ run を別の呼び出しが使っています: $rootFull" }
        try {
            $saved = ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($markerPath)) -AsHashtable
            $savedText = ConvertTo-ScenarioCanonical ([ordered]@{ schemaVersion=$saved.schemaVersion; scenario=$saved.scenario; runId=$saved.runId; mode=$saved.mode; commit=$saved.commit; checkouts=$(
                $map = [ordered]@{}; foreach ($k in @($saved.checkouts.Keys | Sort-Object)) { $map[$k] = $saved.checkouts[$k] }; $map) })
            $currentMap = [ordered]@{}; foreach ($k in @($identity.checkouts.Keys | Sort-Object)) { $currentMap[$k] = $identity.checkouts[$k] }
            $currentText = ConvertTo-ScenarioCanonical ([ordered]@{ schemaVersion=1; scenario=$Scenario; runId=$RunId; mode=$Mode; commit=$identity.commit; checkouts=$currentMap })
            if ($savedText -cne $currentText) { throw "run の印が今回の実行と合いません（名前・RunId・本番か dry-run か・対象の commit・checkout の場所のどれかが違う）。印: $savedText 今回: $currentText" }
        } catch { $lease.Dispose(); throw }
        $continued = $true
    } else {
        [void][IO.Directory]::CreateDirectory($rootFull)
        try { $lease = [IO.File]::Open($leasePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
        catch [IO.IOException] { throw "同じ RunId の run を別の呼び出しが始めています: $rootFull" }
        try { Write-ScenarioNewFile $markerPath ($script:Utf8.GetBytes((ConvertTo-Json -InputObject $identity -Depth 8))) }
        catch { $lease.Dispose(); throw }
    }
    $drive = if ($ExternalDrive) { $ExternalDrive } else { [IO.Path]::GetPathRoot($rootFull) }
    $externalParent = [IO.Path]::Combine($drive, 'ovr', $Scenario)
    return [pscustomobject]@{
        Root=$rootFull; Scenario=$Scenario; RunId=$RunId; Mode=$Mode; Commit=$identity.commit; Continued=$continued
        RecordPath=(Join-Path $rootFull $script:RecordName); Lease=$lease
        ExternalParent=$externalParent; ExternalRoot=(Join-Path $externalParent $RunId)
    }
}

function Close-ScenarioRun {
    param([Parameter(Mandatory)] [psobject]$Run)
    if ($null -ne $Run.Lease) { $Run.Lease.Dispose(); $Run.Lease = $null }
}

function Add-ScenarioRecord {
    # driver.jsonl へ 1 行を足す。utc が無ければ足す。書けなければ例外にする（記録の欠けを黙らない）。
    param([Parameter(Mandatory)] [psobject]$Run, [Parameter(Mandatory)] [Collections.IDictionary]$Record)
    if (-not $Record.Contains('utc')) { $Record['utc'] = [DateTime]::UtcNow.ToString('o') }
    $line = ConvertTo-Json -InputObject $Record -Compress -Depth 8
    [IO.File]::AppendAllText($Run.RecordPath, $line + [Environment]::NewLine, $script:Utf8)
}

function New-ScenarioAttempt {
    # 同じ run で同じ case を走らせた回の番号。基盤が保存する証拠は試行ごとの directory に分ける。
    # run の lease を持つ呼び出しだけが呼ぶので、番号の取り合いは起きない。
    param([Parameter(Mandatory)] [psobject]$Run, [Parameter(Mandatory)] [string]$Case)
    Assert-ScenarioName $Case 'case の名前'
    $caseDirectory = [IO.Path]::Combine($Run.Root, 'base', $Case)
    $number = 1
    if ([IO.Directory]::Exists($caseDirectory)) {
        $existing = @([IO.Directory]::GetDirectories($caseDirectory) | ForEach-Object { [IO.Path]::GetFileName($_) } | Where-Object { $_ -cmatch '^[1-9][0-9]*$' } | ForEach-Object { [int]$_ })
        if ($existing.Count -gt 0) { $number = ([Linq.Enumerable]::Max([int[]]$existing)) + 1 }
    }
    $directory = Join-Path $caseDirectory ([string]$number)
    Assert-ScenarioNoReparsePoint $Run.Root $directory
    [void][IO.Directory]::CreateDirectory($directory)
    return [pscustomobject]@{ Number=$number; BaseDirectory=$directory; EvidenceDirectory=([IO.Path]::Combine($Run.Root, 'evidence', $Case, [string]$number)) }
}

function Save-ScenarioBaseFile {
    # 基盤が自分で保存する原記録（git の原出力、eval の code と原応答など）。
    param([Parameter(Mandatory)] [psobject]$Run, [Parameter(Mandatory)] [psobject]$Attempt, [Parameter(Mandatory)] [string]$Name, [AllowNull()] [object]$Content)
    $path = Join-Path $Attempt.BaseDirectory $Name
    Assert-ScenarioNoReparsePoint $Run.Root $path
    Write-ScenarioNewFile $path (ConvertTo-ScenarioBytes $Content)
    return $path
}

function Save-ScenarioEvidenceFile {
    # scenario が保存する証拠。同じ case の同じ試行の中で、同じ名前を上書きしない（2 つ目は拒む）。
    param([Parameter(Mandatory)] [psobject]$Run, [Parameter(Mandatory)] [psobject]$Attempt, [Parameter(Mandatory)] [string]$Name,
        [AllowNull()] [object]$Content, [string]$SourcePath = '')
    Assert-ScenarioName $Name '証拠の名前' -MaxLength 64
    $path = Join-Path $Attempt.EvidenceDirectory $Name
    Assert-ScenarioNoReparsePoint $Run.Root $path
    if ([IO.File]::Exists($path) -or [IO.Directory]::Exists($path)) { throw "同じ試行に同じ名前の証拠があります: $Name" }
    $bytes = if ($SourcePath) { [IO.File]::ReadAllBytes([IO.Path]::GetFullPath($SourcePath)) } else { ConvertTo-ScenarioBytes $Content }
    Write-ScenarioNewFile $path $bytes
    return $path
}

function Get-ScenarioExternalDirectory {
    # run の外部 root（<drive>\ovr\<scenario>\<RunId>）の下の用途の directory を返す。
    # Create が真なら、無ければ作り、外部 root の直下に印を置く（製品の cache の root の中に印を置かない）。
    # 既にある外部 root は、印がこの run の物の時だけ使う。片付けた外部 root は同じ run で作り直さない。
    param([Parameter(Mandatory)] [psobject]$Run, [Parameter(Mandatory)] [string]$Purpose, [bool]$Create = $true)
    Assert-ScenarioName $Purpose '用途の directory の名前'
    $directory = Join-Path $Run.ExternalRoot $Purpose
    if (-not $Create) { return $directory }
    if ([IO.File]::Exists((Join-Path $Run.Root $script:ExternalRemovedName))) { throw "この run の外部 root は片付け済みです。作り直しません: $($Run.ExternalRoot)" }
    $markerPath = Join-Path $Run.ExternalRoot $script:ExternalMarkerName
    $expected = [ordered]@{ scenario=$Run.Scenario; runId=$Run.RunId; runRoot=$Run.Root }
    [void][IO.Directory]::CreateDirectory($Run.ExternalParent)
    Assert-ScenarioNoReparsePoint $Run.ExternalParent $Run.ExternalRoot
    if ([IO.Directory]::Exists($Run.ExternalRoot)) {
        Assert-ScenarioExternalMarker $Run
    } else {
        [void][IO.Directory]::CreateDirectory($Run.ExternalRoot)
        Write-ScenarioNewFile $markerPath ($script:Utf8.GetBytes((ConvertTo-Json -InputObject $expected -Compress)))
    }
    Assert-ScenarioNoReparsePoint $Run.ExternalParent $directory
    [void][IO.Directory]::CreateDirectory($directory)
    return $directory
}

function Assert-ScenarioExternalMarker {
    param([Parameter(Mandatory)] [psobject]$Run)
    $markerPath = Join-Path $Run.ExternalRoot $script:ExternalMarkerName
    if (-not [IO.File]::Exists($markerPath)) { throw "外部 root に基盤の印がありません（この run が作った物ではありません）: $($Run.ExternalRoot)" }
    $saved = ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($markerPath)) -AsHashtable
    if ($saved.scenario -cne $Run.Scenario -or $saved.runId -cne $Run.RunId -or -not ([string]$saved.runRoot -ieq $Run.Root)) {
        throw "外部 root の印が別の run の物です: $($Run.ExternalRoot)"
    }
}

function Remove-ScenarioTreeNoFollow {
    # reparse point は中へ辿らず、link そのものだけを外す。
    param([string]$Path)
    $attributes = [IO.File]::GetAttributes($Path)
    if (($attributes -band [IO.FileAttributes]::Directory) -eq 0) {
        if (($attributes -band [IO.FileAttributes]::ReadOnly) -ne 0) { [IO.File]::SetAttributes($Path, $attributes -band -bnot [IO.FileAttributes]::ReadOnly) }
        [IO.File]::Delete($Path); return
    }
    if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { [IO.Directory]::Delete($Path, $false); return }
    foreach ($entry in [IO.Directory]::GetFileSystemEntries($Path)) { Remove-ScenarioTreeNoFollow $entry }
    [IO.Directory]::Delete($Path, $false)
}

function Remove-ScenarioExternalRoot {
    # この run の外部 root を消す。削除の直前に、決めた親の下にあること、印がこの run の物であること、
    # 親から対象までのどの成分も reparse point でないことを確かめ直す。無ければ何もしない。
    param([Parameter(Mandatory)] [psobject]$Run)
    $removedPath = Join-Path $Run.Root $script:ExternalRemovedName
    if (-not [IO.Directory]::Exists($Run.ExternalRoot)) { return $false }
    if (-not (Test-ScenarioPathUnder $Run.ExternalParent $Run.ExternalRoot)) { throw "外部 root が決めた親の外にあります: $($Run.ExternalRoot)" }
    Assert-ScenarioNoReparsePoint $Run.ExternalParent $Run.ExternalRoot
    Assert-ScenarioExternalMarker $Run
    # 片付けた事実を先に残す。削除が途中で失敗しても、同じ run で作り直さない。
    if (-not [IO.File]::Exists($removedPath)) { Write-ScenarioNewFile $removedPath ($script:Utf8.GetBytes((ConvertTo-Json -InputObject ([ordered]@{ externalRoot=$Run.ExternalRoot; utc=[DateTime]::UtcNow.ToString('o') }) -Compress))) }
    Remove-ScenarioTreeNoFollow $Run.ExternalRoot
    return $true
}

function Resolve-ScenarioManagedPath {
    # 管理する file の path は checkout の中に留まり、途中に reparse point が無いこと。
    param([string]$CheckoutRoot, [string]$RelativePath)
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) { throw "管理する file は checkout からの相対 path で指します: $RelativePath" }
    foreach ($segment in $RelativePath.Split([char[]]@('\', '/'))) { if ($segment -eq '..' -or $segment -eq '.' -or $segment -eq '') { throw "管理する file の path に使えない成分があります: $RelativePath" } }
    $rootFull = [IO.Path]::GetFullPath($CheckoutRoot).TrimEnd('\', '/')
    $full = [IO.Path]::GetFullPath((Join-Path $rootFull $RelativePath))
    if (-not (Test-ScenarioPathUnder $rootFull $full)) { throw "管理する file が checkout の外を指します: $RelativePath" }
    Assert-ScenarioNoReparsePoint $rootFull $full
    return $full
}

function Read-ScenarioManagedIndex {
    param([string]$RunRoot, [string]$Checkout)
    $indexPath = [IO.Path]::Combine($RunRoot, 'managed', $Checkout, 'index.json')
    if (-not [IO.File]::Exists($indexPath)) { return [pscustomobject]@{ Path=$indexPath; Entries=[Collections.Generic.List[object]]::new() } }
    $entries = [Collections.Generic.List[object]]::new()
    foreach ($item in @(ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($indexPath)) -AsHashtable)) { if ($null -ne $item) { $entries.Add($item) } }
    return [pscustomobject]@{ Path=$indexPath; Entries=$entries }
}

function Save-ScenarioManagedIndex {
    param([psobject]$Index)
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Index.Path))
    $temp = $Index.Path + '.tmp'
    [IO.File]::WriteAllText($temp, (ConvertTo-Json -InputObject @($Index.Entries.ToArray()) -Depth 6), $script:Utf8)
    [IO.File]::Move($temp, $Index.Path, $true)
}

function Get-ScenarioFileSha256OrMissing([string]$Path) {
    if (-not [IO.File]::Exists($Path)) { return 'missing' }
    return Get-ScenarioSha256 ([IO.File]::ReadAllBytes($Path))
}

function Write-ScenarioManagedFile {
    # checkout の中の ignore される file を書く。初めて書く file は、書く前の bytes（有無と内容）を
    # run の root に保存し、書いた後の SHA-256 を記録する。寿命は run（driver の呼び出しをまたぐ）。
    # 2 回目以降は、今の bytes が基盤の書いた物と違えば上書きせずに止まる。
    # checkout の予約は呼び出し側（Scenario.psm1）が、これより先に置く。
    param([Parameter(Mandatory)] [psobject]$Run, [Parameter(Mandatory)] [string]$Checkout, [Parameter(Mandatory)] [string]$CheckoutRoot,
        [Parameter(Mandatory)] [string]$RelativePath, [Parameter(Mandatory)] [AllowEmptyCollection()] [byte[]]$Bytes)
    Assert-ScenarioName $Checkout 'checkout の名前'
    $full = Resolve-ScenarioManagedPath $CheckoutRoot $RelativePath
    $normalized = $RelativePath.Replace('\', '/')
    $index = Read-ScenarioManagedIndex $Run.Root $Checkout
    $entry = $null
    foreach ($item in $index.Entries) { if ([string]$item.path -ceq $normalized) { $entry = $item } }
    if ($null -eq $entry) {
        $existed = [IO.File]::Exists($full)
        $beforeName = '{0}.before' -f $index.Entries.Count
        if ($existed) {
            $beforePath = [IO.Path]::Combine($Run.Root, 'managed', $Checkout, $beforeName)
            Assert-ScenarioNoReparsePoint $Run.Root $beforePath
            Write-ScenarioNewFile $beforePath ([IO.File]::ReadAllBytes($full))
        }
        $entry = [ordered]@{ path=$normalized; existed=$existed; before=$(if ($existed) { $beforeName } else { $null }); beforeSha256=(Get-ScenarioFileSha256OrMissing $full); writtenSha256=$null }
        $index.Entries.Add($entry)
        # 書く前の記録を先に確定する。書いた後に落ちても、予約の解除が元へ戻せる。
        Save-ScenarioManagedIndex $index
    } else {
        $current = Get-ScenarioFileSha256OrMissing $full
        if ($current -cne [string]$entry.writtenSha256) { throw "管理する file が基盤の書いた後に変わっています。上書きしません: $normalized" }
    }
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($full))
    [IO.File]::WriteAllBytes($full, $Bytes)
    $entry.writtenSha256 = Get-ScenarioSha256 $Bytes
    Save-ScenarioManagedIndex $index
    return [pscustomobject]@{ Path=$full; BeforeSha256=$entry.beforeSha256; WrittenSha256=$entry.writtenSha256 }
}

function Get-ScenarioManagedFiles {
    # run の root にある書く前の記録。RunRoot だけで読めるので、予約の解除（run の外の入口）も使う。
    param([Parameter(Mandatory)] [string]$RunRoot, [Parameter(Mandatory)] [string]$Checkout)
    $index = Read-ScenarioManagedIndex $RunRoot $Checkout
    return @($index.Entries.ToArray())
}

function Assert-ScenarioManagedFilesUnchanged {
    # 続きの実行の時に、管理する file が基盤の書いた物のままであることを確かめる。
    param([Parameter(Mandatory)] [string]$RunRoot, [Parameter(Mandatory)] [string]$Checkout, [Parameter(Mandatory)] [string]$CheckoutRoot)
    foreach ($entry in (Get-ScenarioManagedFiles $RunRoot $Checkout)) {
        if ($null -eq $entry.writtenSha256) { continue }
        $full = Resolve-ScenarioManagedPath $CheckoutRoot ([string]$entry.path)
        if ((Get-ScenarioFileSha256OrMissing $full) -cne [string]$entry.writtenSha256) { throw "管理する file が基盤の書いた後に変わっています: $($entry.path)" }
    }
}

function Restore-ScenarioManagedFiles {
    # 書く前の記録のとおりに戻す（元から無かった file は消し、あった file は保存した bytes に戻す）。
    # 今の bytes が基盤の書いた物と違う時と、書く前の記録が無い時は、何も変えずに止まる。
    # 全てを先に確かめてから戻すので、途中で止まって一部だけ戻ることは無い。
    param([Parameter(Mandatory)] [string]$RunRoot, [Parameter(Mandatory)] [string]$Checkout, [Parameter(Mandatory)] [string]$CheckoutRoot)
    Assert-ScenarioName $Checkout 'checkout の名前'
    $runFull = [IO.Path]::GetFullPath($RunRoot)
    $index = Read-ScenarioManagedIndex $runFull $Checkout
    if (-not [IO.File]::Exists($index.Path)) { throw "書く前の記録が run の root にありません: $($index.Path)" }
    $plan = foreach ($entry in $index.Entries) {
        $full = Resolve-ScenarioManagedPath $CheckoutRoot ([string]$entry.path)
        $current = Get-ScenarioFileSha256OrMissing $full
        $expected = if ($null -eq $entry.writtenSha256) { [string]$entry.beforeSha256 } else { [string]$entry.writtenSha256 }
        if ($current -cne $expected -and $current -cne [string]$entry.beforeSha256) { throw "管理する file が基盤の書いた後に変わっています。戻しません: $($entry.path)" }
        $beforeBytes = $null
        if ($entry.existed) {
            $beforePath = [IO.Path]::Combine($runFull, 'managed', $Checkout, [string]$entry.before)
            if (-not [IO.File]::Exists($beforePath)) { throw "書く前の bytes が run の root にありません: $($entry.path)" }
            $beforeBytes = [IO.File]::ReadAllBytes($beforePath)
            if ((Get-ScenarioSha256 $beforeBytes) -cne [string]$entry.beforeSha256) { throw "保存した書く前の bytes が記録と合いません: $($entry.path)" }
        }
        [pscustomobject]@{ Full=$full; Existed=[bool]$entry.existed; Bytes=$beforeBytes; Path=[string]$entry.path }
    }
    $restored = [Collections.Generic.List[string]]::new()
    foreach ($item in @($plan)) {
        if ($item.Existed) { [IO.File]::WriteAllBytes($item.Full, $item.Bytes) }
        elseif ([IO.File]::Exists($item.Full)) { [IO.File]::Delete($item.Full) }
        $restored.Add($item.Path)
    }
    # 戻した後は記録を「書いていない」状態にする。同じ run がもう一度書けば、また書く前から始まる。
    $index.Entries.Clear()
    Save-ScenarioManagedIndex $index
    return @($restored.ToArray())
}

Export-ModuleMember -Function Assert-ScenarioName, Assert-ScenarioNoReparsePoint, Open-ScenarioRun, Close-ScenarioRun, Add-ScenarioRecord,
    New-ScenarioAttempt, Save-ScenarioBaseFile, Save-ScenarioEvidenceFile, Get-ScenarioExternalDirectory, Remove-ScenarioExternalRoot,
    Write-ScenarioManagedFile, Get-ScenarioManagedFiles, Assert-ScenarioManagedFilesUnchanged, Restore-ScenarioManagedFiles
