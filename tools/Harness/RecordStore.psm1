# Git 外の現行入力と、一度書いたら上書きしない記録を置く。
# 保存先は同一マシンの LocalApplicationData で、clone だけでは戻らない。
# 更新されるのは CURRENT だけ。仕様・run・入力・受領は id 付きの不変ファイル。
# 更新は task.lock の排他と revision の一致で直列化し、直前版を CURRENT.previous に残す。
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-JsonHash([object]$Value) {
    $json = ConvertTo-Json -InputObject $Value -Depth 40 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Get-RepositoryIdentity([string]$Repo) {
    # SSH と HTTPS、末尾の .git、大文字小文字を同じ github.com/owner/repo に畳む。
    # 別 worktree でも保存先 id が割れないようにする。GitHub 以外の origin は受けない。
    $remote = (& git -C $Repo remote get-url origin 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($remote)) { throw 'origin がありません。GitHub の origin を設定してから実行してください。' }
    $value = $remote.Trim()
    if ($value -match '^git@github\.com:([^/]+)/([^/]+?)(?:\.git)?/?$') {
        $canonical = "github.com/$($Matches[1])/$($Matches[2])"
    } elseif ($value -match '^https://(?:[^@/]+@)?github\.com/([^/]+)/([^/]+?)(?:\.git)?/?$') {
        $canonical = "github.com/$($Matches[1])/$($Matches[2])"
    } else { throw 'origin の形式に未対応です。GitHub HTTPS または SSH の origin を確認してください。' }
    $canonical = $canonical.ToLowerInvariant()
    $id = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical))).ToLowerInvariant()
    return [pscustomobject]@{ Canonical = $canonical; Id = $id }
}

function Get-TaskDirectory([string]$Repo, [string]$Task, [string]$StoreRoot = '') {
    # task-id をディレクトリ名に使う。英数字とハイフン以外は、保存先からの逸脱として拒否する。
    if ($Task -cnotmatch '^[a-z0-9][a-z0-9-]{0,63}$') { throw 'task-id は小文字英数字とハイフンのみ、64文字以内にしてください。' }
    $identity = Get-RepositoryIdentity $Repo
    if ([string]::IsNullOrWhiteSpace($StoreRoot)) {
        $StoreRoot = [IO.Path]::Combine([Environment]::GetFolderPath('LocalApplicationData'), 'OneStarMaker', 'Harness')
    }
    return [IO.Path]::Combine($StoreRoot, $identity.Id, 'tasks', $Task)
}

function Initialize-Task([string]$TaskDirectory, [string]$Task, [string]$Owner, [object]$Identity, [object]$Spec) {
    # registration は新規作成のみ。途中で落ちて CURRENT が無い task は、init の上書きも直前版の復旧もできない。
    if ([IO.Directory]::Exists($TaskDirectory)) { throw 'taskは登録済みです。status/currentを使い、欠落時はrestoreしてください。' }
    [IO.Directory]::CreateDirectory($TaskDirectory) | Out-Null
    $registration = [ordered]@{ schemaVersion = 1; taskId = $Task; repoId = $Identity.Id; owner = $Owner; createdAt = [DateTimeOffset]::UtcNow.ToString('o') }
    $regPath = [IO.Path]::Combine($TaskDirectory, 'registration.json')
    $stream = [IO.FileStream]::new($regPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $bytes = [Text.Encoding]::UTF8.GetBytes((ConvertTo-Json $registration)); $stream.Write($bytes); $stream.Flush($true) } finally { $stream.Dispose() }
    $specRecord = Write-NewRecord $TaskDirectory 'specifications' $Spec
    $current = [ordered]@{ schemaVersion = 1; repoId = $Identity.Id; taskId = $Task; revision = 1; owner = $Owner; phase = 'B'; updatedAt = [DateTimeOffset]::UtcNow.ToString('o'); specId = $specRecord.Id; candidateHead = $Spec.base; changeDescription = ''; unresolved = @(); blockers = @(); nextAction = 'pilotのsidecarとHarnessを実装する'; adoptedRuns = @(); selectedInputId = $null; judgmentInputId = $null; gateReceiptId = $null; references = @() }
    [IO.File]::WriteAllText([IO.Path]::Combine($TaskDirectory, 'CURRENT.json'), (ConvertTo-Json -InputObject $current -Depth 40), [Text.UTF8Encoding]::new($false))
    return $specRecord
}

function Publish-Handoff([string]$TaskDirectory, [object]$Current, [object]$InputBody, [string]$Task, [string]$To, [string]$RunId, [string]$Head, [string]$SpecHash, [string]$Stage) {
    # 入力と受領を書いてから CURRENT を更新する。revision が合わず更新が失敗しても、選択はディスクに残らない。
    # レビュー参照は30日。close は終了の明示が無ければ、この参照があるため止まる。
    $input = Write-NewRecord $TaskDirectory 'inputs' $InputBody
    $receipt = Write-NewRecord $TaskDirectory 'receipts' ([ordered]@{ ready = $true; taskId = $Task; to = $To; inputId = $input.Id; inputHash = $input.Hash; runId = $RunId; head = $Head; specHash = $SpecHash })
    $Current.selectedInputId = $input.Id
    if ($Stage -ceq 'judgment') { $Current.judgmentInputId = $input.Id }
    $Current.gateReceiptId = $receipt.Id
    $Current.phase = $To
    $Current.references = @($Current.references) + @([ordered]@{ referenceId = [Guid]::NewGuid().ToString('N'); consumerId = $To; purpose = 'review'; runId = $RunId; owner = $Current.owner; expiresAt = [DateTimeOffset]::UtcNow.AddDays(30).ToString('o'); releasedAt = $null })
    $Current.nextAction = "固定入力 $($input.Id) から${To}を開始する"
    [void](Write-Current $TaskDirectory $Current $Current.revision)
    return [pscustomobject]@{ Input = $input; Receipt = $receipt }
}

function Publish-BlindHandoff([string]$TaskDirectory, [object]$Current, [object]$JudgmentInput, [string]$Task, [string]$RunId, [string]$Head, [string]$SpecHash) {
    # CBlindは判定入力を再生成しない。同じ入力への再取得は受領と参照を増やさない。
    if ($Current.phase -ceq 'CBlind' -and $Current.gateReceiptId) {
        $existing = Read-Record $TaskDirectory 'receipts' $Current.gateReceiptId
        if ($existing.content.to -cne 'CBlind' -or $existing.content.inputId -cne $JudgmentInput.id -or $existing.content.inputHash -cne $JudgmentInput.hash) { throw 'CBlindの既存受領が判定入力と一致しません。' }
        return $existing
    }
    $receipt = Write-NewRecord $TaskDirectory 'receipts' ([ordered]@{ ready = $true; taskId = $Task; to = 'CBlind'; inputId = $JudgmentInput.id; inputHash = $JudgmentInput.hash; runId = $RunId; head = $Head; specHash = $SpecHash })
    $Current.gateReceiptId = $receipt.Id
    $Current.phase = 'CBlind'
    $Current.references = @($Current.references) + @([ordered]@{ referenceId = [Guid]::NewGuid().ToString('N'); consumerId = 'CBlind'; purpose = 'review'; runId = $RunId; owner = $Current.owner; expiresAt = [DateTimeOffset]::UtcNow.AddDays(30).ToString('o'); releasedAt = $null })
    $Current.nextAction = "固定入力 $($JudgmentInput.id) からCBlindを開始する"
    [void](Write-Current $TaskDirectory $Current $Current.revision)
    return $receipt
}

function Write-NewRecord([string]$TaskDirectory, [string]$Kind, [object]$Content, [string]$Id = '') {
    # CreateNew なので同じ id は二度書けない。hash は本文だけで、後から改ざんすると読取時に落ちる。
    if ([string]::IsNullOrWhiteSpace($Id)) { $Id = [Guid]::NewGuid().ToString('N') }
    if ($Id -cnotmatch '^[a-zA-Z0-9-]+$') { throw 'record id が不正です。' }
    $folder = [IO.Path]::Combine($TaskDirectory, $Kind)
    [IO.Directory]::CreateDirectory($folder) | Out-Null
    $path = [IO.Path]::Combine($folder, "$Id.json")
    $envelope = [ordered]@{ schemaVersion = 1; id = $Id; kind = $Kind; hash = (Get-JsonHash $Content); content = $Content }
    $json = ConvertTo-Json -InputObject $envelope -Depth 40
    $stream = [IO.FileStream]::new($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($json)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    } finally { $stream.Dispose() }
    return [pscustomobject]@{ Id = $Id; Hash = $envelope.hash; Path = $path }
}

function Start-Run([string]$TaskDirectory, [string]$Id, [string]$Head, [string]$StartedAt) {
    # 完了ファイルが無い中断は進行中のまま残す。成功した run として読まれることを防ぐ。
    $folder = [IO.Path]::Combine($TaskDirectory, 'in-progress')
    [IO.Directory]::CreateDirectory($folder) | Out-Null
    $path = [IO.Path]::Combine($folder, "$Id.json")
    $stream = [IO.FileStream]::new($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject ([ordered]@{ id=$Id; head=$Head; startedAt=$StartedAt; state='in-progress' })))
        $stream.Write($bytes)
        $stream.Flush($true)
    } finally { $stream.Dispose() }
}

function Finish-Run([string]$TaskDirectory, [object]$Run) {
    # 不変の run を書いてから進行中ファイルを消す。書込前に落ちた実行は完了 record にならない。
    $path = [IO.Path]::Combine($TaskDirectory, 'in-progress', "$($Run.id).json")
    if (-not [IO.File]::Exists($path)) { throw '開始中runの状態がありません。' }
    $record = Write-NewRecord $TaskDirectory 'runs' $Run $Run.id
    [IO.File]::Delete($path)
    return $record
}

function Read-Record([string]$TaskDirectory, [string]$Kind, [string]$Id) {
    # 保存時の hash を本文から再計算する。一致しないファイルは改ざんとみなし、中身を採用しない。
    if ($Id -cnotmatch '^[a-zA-Z0-9-]+$') { throw 'record id が不正です。' }
    $path = [IO.Path]::Combine($TaskDirectory, $Kind, "$Id.json")
    if (-not [IO.File]::Exists($path)) { throw "固定recordがありません: $Kind/$Id。statusで参照を確認してください。" }
    $record = Get-Content -LiteralPath $path -Raw -Encoding utf8 | ConvertFrom-Json -Depth 40 -DateKind String
    if ($record.kind -cne $Kind -or $record.id -cne $Id -or (Get-JsonHash $record.content) -cne $record.hash) {
        throw "固定recordの整合性がありません: $Kind/$Id。restore候補を確認してください。"
    }
    return $record
}

function Test-TaskClosed([string]$TaskDirectory) {
    $closedPath = [IO.Path]::Combine($TaskDirectory, 'closed.json')
    if (-not [IO.File]::Exists($closedPath)) { return $false }
    $closed = Get-Content -LiteralPath $closedPath -Raw -Encoding utf8 | ConvertFrom-Json -DateKind String
    if ($closed.kind -cne 'closed' -or -not $closed.closedAt -or -not $closed.outcome) { throw 'closed.jsonが破損しています。修復判断まで更新できません。' }
    return $true
}

function Read-Current([string]$TaskDirectory) {
    $registrationPath = [IO.Path]::Combine($TaskDirectory, 'registration.json')
    if (-not [IO.File]::Exists($registrationPath)) {
        throw 'CURRENTが見つかりません: statusでtask-idを確認し、既存taskはrestore、新規作業だけinitを実行してください。'
    }
    $registration = Get-Content -LiteralPath $registrationPath -Raw -Encoding utf8 | ConvertFrom-Json -DateKind String
    $path = [IO.Path]::Combine($TaskDirectory, 'CURRENT.json')
    if (-not [IO.File]::Exists($path)) { throw 'CURRENTが見つかりません: statusでtask-idを確認し、既存taskはrestore、新規作業だけinitを実行してください。' }
    $current = Get-Content -LiteralPath $path -Raw -Encoding utf8 | ConvertFrom-Json -Depth 40 -DateKind String
    if ($current.schemaVersion -ne 1 -or $current.revision -lt 1 -or -not $current.specId -or $current.taskId -cne $registration.taskId -or $current.repoId -cne $registration.repoId) { throw 'CURRENTが破損またはtask識別不一致です。restoreで直前版を確認してください。' }
    return $current
}

function Write-Current([string]$TaskDirectory, [object]$Current, [int]$ExpectedRevision) {
    # 排他のあとで close と revision を見なす。待ちの間に閉じられた更新は書かない。
    # 直前版を残してから置き換える。凍結した仕様 id と task 識別は、この更新では変えられない。
    if (Test-TaskClosed $TaskDirectory) { throw 'taskはclose済みです。新しいtaskをinitしてください。' }
    $lock = [IO.Path]::Combine($TaskDirectory, 'task.lock')
    $held = [IO.FileStream]::new($lock, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        if (Test-TaskClosed $TaskDirectory) { throw 'taskはclose済みです。新しいtaskをinitしてください。' }
        $old = Read-Current $TaskDirectory
        if ($old.revision -ne $ExpectedRevision) { throw 'CURRENTのrevisionが古いです。currentを読み直してください。' }
        if ($Current.specId -cne $old.specId -or $Current.taskId -cne $old.taskId -or $Current.repoId -cne $old.repoId) { throw '凍結仕様またはtask識別はCURRENT更新で変更できません。' }
        $Current.revision = $ExpectedRevision + 1
        $Current.updatedAt = [DateTimeOffset]::UtcNow.ToString('o')
        $path = [IO.Path]::Combine($TaskDirectory, 'CURRENT.json')
        $previous = [IO.Path]::Combine($TaskDirectory, 'CURRENT.previous')
        $temp = [IO.Path]::Combine($TaskDirectory, 'CURRENT.' + [Guid]::NewGuid().ToString('N') + '.tmp')
        [IO.File]::WriteAllText($temp, (ConvertTo-Json -InputObject $Current -Depth 40), [Text.UTF8Encoding]::new($false))
        [IO.File]::Copy($path, $previous, $true)
        [IO.File]::Move($temp, $path, $true)
    } finally { $held.Dispose() }
    return $Current
}

function Restore-Current([string]$TaskDirectory) {
    # CURRENT が欠落した場合だけ直前版を選ぶ。存在するファイルの読取失敗まで
    # 握りつぶすと、新しい参照を検査せずに巻き戻してしまう。
    if (Test-TaskClosed $TaskDirectory) { throw 'taskはclose済みです。新しいtaskをinitしてください。' }
    $previous = [IO.Path]::Combine($TaskDirectory, 'CURRENT.previous')
    if (-not [IO.File]::Exists($previous)) { throw '復旧できる直前版がありません。' }
    $candidate = Get-Content -LiteralPath $previous -Raw -Encoding utf8 | ConvertFrom-Json -Depth 40 -DateKind String
    $registration = Get-Content -LiteralPath ([IO.Path]::Combine($TaskDirectory, 'registration.json')) -Raw -Encoding utf8 | ConvertFrom-Json -DateKind String
    if ($candidate.schemaVersion -ne 1 -or $candidate.taskId -cne $registration.taskId -or $candidate.repoId -cne $registration.repoId -or -not $candidate.specId) { throw '復旧候補のtaskまたは仕様が一致しません。' }
    [void](Read-Record $TaskDirectory 'specifications' $candidate.specId)
    foreach ($adopted in @($candidate.adoptedRuns)) { [void](Read-Record $TaskDirectory 'runs' $adopted.id) }
    # 採用欄以外のreview/defect参照も復旧候補の一部。対象runが欠落・改変されていれば戻さない。
    foreach ($ref in @($candidate.references)) {
        if (-not $ref.referenceId -or -not $ref.owner -or -not $ref.runId -or -not $ref.expiresAt) { throw '復旧候補の参照が不完全です。' }
        [void]([DateTimeOffset]::Parse($ref.expiresAt))
        [void](Read-Record $TaskDirectory 'runs' $ref.runId)
    }
    $lock = [IO.Path]::Combine($TaskDirectory, 'task.lock')
    $held = [IO.FileStream]::new($lock, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        if (Test-TaskClosed $TaskDirectory) { throw 'taskはclose済みです。' }
        $revision = $candidate.revision
        if ([IO.File]::Exists([IO.Path]::Combine($TaskDirectory, 'CURRENT.json'))) {
            $current = Read-Current $TaskDirectory
            if ($current.specId -cne $candidate.specId) { throw '復旧候補の凍結仕様が現在と異なります。' }
            $oldRefs = @($candidate.references)
            foreach ($ref in @($current.references)) {
                $matching = @($oldRefs | Where-Object referenceId -eq $ref.referenceId)
                if ($matching.Count -ne 1 -or (Get-JsonHash $matching[0]) -cne (Get-JsonHash $ref)) { throw '復旧候補が現行参照を失います。参照所有者と復旧方針を確認してください。' }
            }
            $revision = [Math]::Max($revision, $current.revision)
        }
        # 戻した revision は現行と直前版の大きいほうより 1 進める。古い番号の再適用を防ぐ。
        $candidate.revision = $revision + 1
        $candidate.updatedAt = [DateTimeOffset]::UtcNow.ToString('o')
        $candidate.selectedInputId = $null
        $candidate.judgmentInputId = $null
        $candidate.gateReceiptId = $null
        $candidate.phase = 'B'
        $candidate.nextAction = '復旧後の候補headとrunを再照合してください。'
        $target = [IO.Path]::Combine($TaskDirectory, 'CURRENT.json')
        $temp = [IO.Path]::Combine($TaskDirectory, 'CURRENT.' + [Guid]::NewGuid().ToString('N') + '.tmp')
        [IO.File]::WriteAllText($temp, (ConvertTo-Json -InputObject $candidate -Depth 40), [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temp, $target, $true)
        [IO.File]::Delete($previous)
    } finally { $held.Dispose() }
    return $candidate
}

function Write-Closed([string]$TaskDirectory, [int]$ExpectedRevision, [scriptblock]$BuildRecord, [scriptblock]$BeforeDisplayWrite = $null) {
    # closed.json は新規作成のみ。排他のあとで revision を見なし、古い close が終端ファイルを作らないようにする。
    $lock = [IO.Path]::Combine($TaskDirectory, 'task.lock')
    $held = [IO.FileStream]::new($lock, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        if (Test-TaskClosed $TaskDirectory) { throw 'taskはclose済みです。' }
        $inProgress = [IO.Path]::Combine($TaskDirectory, 'in-progress')
        if ([IO.Directory]::Exists($inProgress) -and @([IO.Directory]::EnumerateFiles($inProgress, '*.json')).Count -gt 0) {
            throw '未完了runがあります。完了recordまたは失敗recordを確定してからcloseしてください。'
        }
        $current = Read-Current $TaskDirectory
        if ($current.revision -ne $ExpectedRevision) { throw 'close前にCURRENTが更新されました。currentを読み直してください。' }
        $record = & $BuildRecord $current
        $path = [IO.Path]::Combine($TaskDirectory, 'closed.json')
        $temp = [IO.Path]::Combine($TaskDirectory, 'closed.' + [Guid]::NewGuid().ToString('N') + '.tmp')
        $stream = [IO.FileStream]::new($temp, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try {
            $bytes = [Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $record -Depth 40))
            $stream.Write($bytes)
            $stream.Flush($true)
        } finally { $stream.Dispose() }
        # 完全に書けた一時ファイルだけを終端名へ昇格する。存在済みの終端は上書きしない。
        [IO.File]::Move($temp, $path)
        # 終端recordが正本。CURRENT表示が書けなくてもcloseを取り消さない。
        try {
            if ($BeforeDisplayWrite) { & $BeforeDisplayWrite }
            $current.phase = 'closed'
            $current.nextAction = 'taskはclose済みです。新しいtaskをinitしてください。'
            $current.revision = $ExpectedRevision + 1
            $current.updatedAt = [DateTimeOffset]::UtcNow.ToString('o')
            $displayTemp = [IO.Path]::Combine($TaskDirectory, 'CURRENT.' + [Guid]::NewGuid().ToString('N') + '.tmp')
            [IO.File]::WriteAllText($displayTemp, (ConvertTo-Json -InputObject $current -Depth 40), [Text.UTF8Encoding]::new($false))
            [IO.File]::Move($displayTemp, [IO.Path]::Combine($TaskDirectory, 'CURRENT.json'), $true)
        } catch { Write-Warning "closed.jsonは確定しましたがCURRENT表示の更新に失敗しました: $($_.Exception.Message)" }
        return $record
    } finally { $held.Dispose() }
}

function Add-AdoptedRun([string]$TaskDirectory, [object]$Current, [string]$RunId, [string]$Reason, [string]$SpecHash, [string]$RetainUntil = '') {
    if (-not $RunId -or -not $Reason) { throw 'adoptには-RunIdと-Reasonが必要です。' }
    $record = Read-Record $TaskDirectory 'runs' $RunId
    if ($record.content.specHash -cne $SpecHash) { throw '別仕様のrunは採用できません。' }
    if (@($Current.adoptedRuns | Where-Object id -eq $RunId).Count -gt 0) { throw '同じrunは採用済みです。' }
    $now = [DateTimeOffset]::UtcNow
    $expiry = if ($RetainUntil) { [DateTimeOffset]::Parse($RetainUntil).ToUniversalTime() } else { $now.AddDays(30) }
    if ($expiry -le $now) { throw 'retainUntilは将来の時刻が必要です。' }
    $Current.adoptedRuns = @($Current.adoptedRuns) + @([ordered]@{ id = $RunId; reason = $Reason; adoptedAt = $now.ToString('o') })
    $Current.references = @($Current.references) + @([ordered]@{ referenceId = [Guid]::NewGuid().ToString('N'); consumerId = $Current.taskId; purpose = 'adopted-run'; runId = $RunId; owner = $Current.owner; expiresAt = $expiry.ToString('o'); releasedAt = $null })
    return Write-Current $TaskDirectory $Current $Current.revision
}

function Edit-Reference([string]$TaskDirectory, [object]$Current, [string]$Action, [string]$Owner, [string]$RunId, [string]$ConsumerId, [string]$Purpose, [string]$ExpiresAt, [string]$ReferenceId) {
    if (-not $Owner) { throw 'referenceには-Ownerが必要です。' }
    if ($Action -ceq 'Add') {
        if (-not $RunId -or -not $ConsumerId -or -not $ExpiresAt) { throw '参照追加には-RunId、-ConsumerId、-ExpiresAtが必要です。' }
        [void](Read-Record $TaskDirectory 'runs' $RunId)
        $expiry = [DateTimeOffset]::Parse($ExpiresAt).ToUniversalTime()
        if ($expiry -le [DateTimeOffset]::UtcNow) { throw 'expiresAtは将来の有限時刻が必要です。' }
        $ReferenceId = [Guid]::NewGuid().ToString('N')
        $Current.references = @($Current.references) + @([ordered]@{ referenceId = $ReferenceId; consumerId = $ConsumerId; purpose = $Purpose; runId = $RunId; owner = $Owner; expiresAt = $expiry.ToString('o'); releasedAt = $null })
    } else {
        if (-not $ReferenceId) { throw '解放・延長には-ReferenceIdが必要です。' }
        $matches = @($Current.references | Where-Object referenceId -eq $ReferenceId)
        if ($matches.Count -ne 1 -or $matches[0].owner -cne $Owner -or $matches[0].releasedAt) { throw '所有する有効参照が見つかりません。' }
        if ($Action -ceq 'Release') { $matches[0].releasedAt = [DateTimeOffset]::UtcNow.ToString('o') }
        else {
            if (-not $ExpiresAt) { throw '延長には-ExpiresAtが必要です。' }
            $expiry = [DateTimeOffset]::Parse($ExpiresAt).ToUniversalTime()
            if ($expiry -le [DateTimeOffset]::UtcNow) { throw 'expiresAtは将来の有限時刻が必要です。' }
            $matches[0].expiresAt = $expiry.ToString('o')
        }
    }
    [void](Write-Current $TaskDirectory $Current $Current.revision)
    return $ReferenceId
}

function Set-RunCurrent([string]$TaskDirectory, [object]$Current, [string]$Head, [string]$RunId) {
    $Current.candidateHead = $Head
    $Current.nextAction = "run $RunId の結果を確認する"
    return Write-Current $TaskDirectory $Current $Current.revision
}

function Set-CurrentNotes([string]$TaskDirectory, [object]$Current, [int]$ExpectedRevision, [bool]$HasNextAction, [string]$NextAction, [bool]$HasUnresolved, [string[]]$Unresolved, [bool]$HasBlockers, [string[]]$Blockers) {
    if ($ExpectedRevision -ne $Current.revision) { throw '更新には一致する-ExpectedRevisionが必要です。currentを読み直してください。' }
    if ($HasNextAction) { $Current.nextAction = $NextAction }
    if ($HasUnresolved) { $Current.unresolved = @($Unresolved | Where-Object { $_ }) }
    if ($HasBlockers) { $Current.blockers = @($Blockers | Where-Object { $_ }) }
    return Write-Current $TaskDirectory $Current $ExpectedRevision
}

Export-ModuleMember -Function Get-JsonHash,Get-RepositoryIdentity,Get-TaskDirectory,Initialize-Task,Publish-Handoff,Publish-BlindHandoff,Write-NewRecord,Read-Record,Test-TaskClosed,Read-Current,Write-Current,Restore-Current,Write-Closed,Start-Run,Finish-Run,Add-AdoptedRun,Edit-Reference,Set-RunCurrent,Set-CurrentNotes
