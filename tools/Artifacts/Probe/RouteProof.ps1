param(
    [string] $Endpoint,
    [switch] $Child,
    [ValidateSet('put','authenticated-get','unsigned-get','delete')][string] $Operation,
    [string] $Key,
    [string] $ExpectedHash,
    [int] $ExpectedBytes,
    [string] $PayloadBase64,
    [string] $RunId,
    [string] $LockRuleJson,
    [string] $ImplementationBase,
    [string] $ImplementationHead,
    [ValidateRange(0,30000)][int] $OperationBudgetMilliseconds = 30000,
    [long] $OperationStartTimestamp,
    [switch] $Library
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ObservationProperties = @(
    'Operation','HttpStatus','StatusClass','S3Code','ByteCount','BodySha256','PrefixSha256',
    'EofConfirmed','LimitReached','TimedOut','Redirected','Generation',
    'Method','TargetUri','HasAuthHeader','SignatureQueryPresent','TargetChangingQueryPresent'
)

# 実行とoffline試験で同じ主ループを使うため、時計と子process境界を注入可能にします。
$script:RouteProofClock = $null
$script:RouteProofChildRunner = $null
# child process全体の期限とtransport応答を別々に注入し、同じ主ループの停止境界を検査します。
$script:RouteProofTransport = $null
$script:RouteProofTaskWaiter = $null

function Test-Endpoint([string] $Value) { return $Value -cmatch '\Ahttps://[0-9a-f]{32}\.r2\.cloudflarestorage\.com\z' }
function Test-Hash([string] $Value) { return $Value -cmatch '\A[0-9a-f]{64}\z' }
function Test-RunId([string] $Value) { return $Value -cmatch '\A[0-9a-f]{32}\z' }
function Test-RouteProofCredentialStatus($Value) {
    # credential v1のEndpointはnull固定で、実probe endpointはCLI引数から別に受け取ります。
    return $null -ne $Value -and $Value.Profile -is [string] -and $Value.Profile -ceq 'osm' -and
        $Value.Bucket -is [string] -and $Value.Bucket -ceq 'osm-artifacts' -and $null -eq $Value.Endpoint -and
        $Value.Generation -is [string] -and $Value.Generation -cmatch '\A[0-9a-f]{32}\z'
}
function Test-Key([string] $Value, [string] $ExpectedRunId = '') {
    # childごとにrun-idとkeyの対応を検証し、別runへの誤書込みを拒否します。
    $validShape = $Value -cmatch '\Aprobe/(unlocked|locked)/[0-9a-f]{32}/[A-Za-z0-9._-]{1,64}\z'
    $leaf = if ($validShape) { ($Value -split '/')[-1] } else { '' }
    return $validShape -and $leaf -notin @('.','..') -and
        ([string]::IsNullOrEmpty($ExpectedRunId) -or $Value -match ('^probe/(unlocked|locked)/' + [regex]::Escape($ExpectedRunId) + '/')) -and
        $Value.IndexOfAny([char[]]([char]0..[char]31 + [char]127)) -lt 0
}

function Get-RouteElapsed {
    if ($null -ne $script:RouteProofClock) { return [TimeSpan](& $script:RouteProofClock) }
    return $script:RunClock.Elapsed
}

function Start-RouteClock {
    if ($null -ne $script:RouteProofClock) { return }
    $script:RunClock = [Diagnostics.Stopwatch]::StartNew()
}

function Invoke-RouteDelay([int] $Milliseconds) {
    if ($null -ne $script:RouteProofClock) { return }
    Start-Sleep -Milliseconds $Milliseconds
}

function Get-RemainingPipeWait([int] $ChildBudgetMilliseconds, [TimeSpan] $ChildStartedAt) {
    # Stopwatch基準と注入時計のどちらでも、子process開始後だけを子予算から差し引きます。
    $childElapsed = (Get-RouteElapsed) - $ChildStartedAt
    return [Math]::Max(0, [Math]::Min(45000, $ChildBudgetMilliseconds - [int]$childElapsed.TotalMilliseconds))
}

function Get-RouteProofChildBudgetSchedule([int] $ChildBudgetMilliseconds) {
    # HTTP結果を子がJSONで返す時間と、timeout時にprocess treeを止める時間を45秒枠から先に確保します。
    $total = [Math]::Max(0, [Math]::Min(45000, $ChildBudgetMilliseconds))
    $terminationReserve = if ($total -eq 0) { 0 } else { [Math]::Min(15000, [Math]::Max(1, [int]($total / 3))) }
    $processWait = [Math]::Max(0, $total - $terminationReserve)
    $resultReserve = [Math]::Min(1000, $processWait)
    $transportBudget = [Math]::Max(0, [Math]::Min(30000, $processWait - $resultReserve))
    return [pscustomobject]@{
        ProcessWaitMilliseconds = $processWait
        TransportBudgetMilliseconds = $transportBudget
        TerminationReserveMilliseconds = $terminationReserve
        ResultReserveMilliseconds = $resultReserve
    }
}

function Get-RemainingOperationBudget([int] $OperationBudgetMilliseconds, [double] $ElapsedMilliseconds) {
    # child起動後のmodule importやcredential読取も30秒のoperation期限に含め、transportへ残時間だけを渡します。
    if ($ElapsedMilliseconds -lt 0) { return 0 }
    return [Math]::Max(0, [Math]::Min(30000, $OperationBudgetMilliseconds - [int]$ElapsedMilliseconds))
}

function New-RouteProofCredentialCallback([long] $StartTimestamp, [int] $OperationBudgetMilliseconds, [scriptblock] $TransportInvoker) {
    # GetNewClosureはscript-local functionを取り込まないため、budget計算とtransportを先にscriptblockとして束縛します。
    $budgetCalculator = (Get-Command Get-RemainingOperationBudget -CommandType Function -ErrorAction Stop).ScriptBlock
    $childOperationStart = $StartTimestamp
    $childOperationBudget = $OperationBudgetMilliseconds
    $childTransportInvoker = $TransportInvoker
    return {
        param($id, $secret, $generation, $requestValue)
        # Storeのprofile読取後、親がprocessを起動した時点からの単調時間を差し引いて残りだけ渡します。
        $elapsed = [Diagnostics.Stopwatch]::GetElapsedTime($childOperationStart).TotalMilliseconds
        $requestValue.DeadlineMilliseconds = & $budgetCalculator $childOperationBudget $elapsed
        if ($requestValue.DeadlineMilliseconds -lt 1) { throw 'Operation deadline elapsed.' }
        & $childTransportInvoker $id $secret $generation $requestValue
    }.GetNewClosure()
}

function Wait-RouteProofPipeTask($Task, [int] $Milliseconds) {
    if ($Milliseconds -le 0) { return $false }
    if ($null -ne $script:RouteProofTaskWaiter) { return [bool](& $script:RouteProofTaskWaiter $Task $Milliseconds) }
    return [bool]$Task.Wait($Milliseconds)
}

function Wait-RouteProofPipes($StdoutTask, $StderrTask, [int] $ChildBudgetMilliseconds, [TimeSpan] $ChildStartedAt) {
    # stdoutが期限を使った分だけstderrの待機時間を再計算し、二つのpipeで同じ残予算を共有します。
    $remaining = Get-RemainingPipeWait $ChildBudgetMilliseconds $ChildStartedAt
    if (-not (Wait-RouteProofPipeTask $StdoutTask $remaining)) { return $false }
    $remaining = Get-RemainingPipeWait $ChildBudgetMilliseconds $ChildStartedAt
    return (Wait-RouteProofPipeTask $StderrTask $remaining)
}

function Test-RouteProofChildStopped([bool] $ParentExited, [bool] $StdoutClosed, [bool] $StderrClosed) {
    # redirected pipeのEOFが揃う前は、子孫がhandleを保持している可能性を除外できません。
    return $ParentExited -and $StdoutClosed -and $StderrClosed
}

function Stop-RouteProofChild([Diagnostics.Process] $Process, $StdoutTask, $StderrTask, [int] $ChildBudgetMilliseconds, [TimeSpan] $ChildStartedAt) {
    # 子は1操作だけを実行し、自身から別processを起動しません。終了時はprocess treeをkillし、
    # rootの終了だけでなく両pipeのEOFも揃った場合に限って回復DELETEを許可します。
    try { $Process.Kill($true) } catch { try { $Process.Kill() } catch { } }
    try {
        # kill後も同じ子process予算の残時間だけ終了とpipe EOFを待ち、固定の追加待機はしません。
        $remaining = Get-RemainingPipeWait $ChildBudgetMilliseconds $ChildStartedAt
        $parentExited = $Process.WaitForExit($remaining)
        $pipesClosed = Wait-RouteProofPipes $StdoutTask $StderrTask $ChildBudgetMilliseconds $ChildStartedAt
        return (Test-RouteProofChildStopped $parentExited $pipesClosed ($StdoutTask.IsCompleted -and $StderrTask.IsCompleted))
    }
    catch { return $false }
}

function Get-SafeCommitId([string] $Value) {
    if ($Value -cmatch '\A[0-9a-f]{40}\z') { return $Value }
    return $null
}

function Test-ChildPayload([string] $OperationName, [string] $RunIdValue, [string] $ExpectedHashValue, [int] $ExpectedLength, [byte[]] $Payload) {
    if ($OperationName -ne 'put') { return $null -eq $Payload }
    if ($null -eq $Payload -or $ExpectedLength -lt 1 -or $ExpectedLength -gt 1024 -or $Payload.Length -ne $ExpectedLength) { return $false }
    $actualHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Payload)).ToLowerInvariant()
    $fixture = [Text.Encoding]::ASCII.GetString($Payload)
    return $actualHash -ceq $ExpectedHashValue -and $fixture -cmatch ('\AOSM-ROUTE-PROOF:' + [regex]::Escape($RunIdValue) + ':(original|changed)\z')
}

# 実行経路とoffline試験が共有する唯一の12操作ループです。時刻と子process境界だけを差し替えます。
function Invoke-RouteProofLoop([string] $EndpointValue, [string] $RunIdValue, [string] $RuleJson) {
    $script:ActiveEndpoint = $EndpointValue
    $lockRule = Read-LockRule $RuleJson
    Start-RouteClock
    $unlockedKey = "probe/unlocked/$RunIdValue/object.txt"
    $lockedKey = "probe/locked/$RunIdValue/object.txt"
    # 親でkeyの全体をもう一度検査し、未検証文字列が結果へ出る経路を作りません。
    if (-not (Test-Key $unlockedKey $RunIdValue) -or -not (Test-Key $lockedKey $RunIdValue)) {
        throw 'Invalid run key.'
    }
    $original = [Text.Encoding]::ASCII.GetBytes("OSM-ROUTE-PROOF:${RunIdValue}:original")
    $changed = [Text.Encoding]::ASCII.GetBytes("OSM-ROUTE-PROOF:${RunIdValue}:changed")
    $originalHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($original)).ToLowerInvariant()
    $changedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($changed)).ToLowerInvariant()
    $records = [Collections.Generic.List[object]]::new()
    $classification = 'pass'; $unlockedCleanupRequired = $false; $unlockedRemoved = $false; $writerGeneration = $null
    $script:ChildTerminationConfirmed = $true
    $steps = @(
        @{ Op='put'; Key=$unlockedKey; Bytes=$original; Hash=$originalHash; Phase='unlocked-put' },
        @{ Op='authenticated-get'; Key=$unlockedKey; Bytes=$original; Hash=$originalHash; Phase='unlocked-authenticated-get' },
        @{ Op='unsigned-get'; Key=$unlockedKey; Bytes=$original; Hash=$originalHash; Phase='unsigned-get' },
        @{ Op='put'; Key=$unlockedKey; Bytes=$changed; Hash=$changedHash; Phase='unlocked-overwrite' },
        @{ Op='authenticated-get'; Key=$unlockedKey; Bytes=$changed; Hash=$changedHash; Phase='unlocked-overwrite-get' },
        @{ Op='delete'; Key=$unlockedKey; Bytes=$changed; Hash=$changedHash; Phase='unlocked-delete' },
        @{ Op='authenticated-get'; Key=$unlockedKey; Bytes=$changed; Hash=$changedHash; Phase='unlocked-delete-confirm' },
        @{ Op='put'; Key=$lockedKey; Bytes=$original; Hash=$originalHash; Phase='locked-put' },
        @{ Op='authenticated-get'; Key=$lockedKey; Bytes=$original; Hash=$originalHash; Phase='locked-authenticated-get' },
        @{ Op='put'; Key=$lockedKey; Bytes=$changed; Hash=$changedHash; Phase='locked-overwrite' },
        @{ Op='delete'; Key=$lockedKey; Bytes=$original; Hash=$originalHash; Phase='locked-delete' },
        @{ Op='authenticated-get'; Key=$lockedKey; Bytes=$original; Hash=$originalHash; Phase='locked-confirm' }
    )
    foreach ($step in $steps) {
        if ((Get-RouteElapsed) -ge [TimeSpan]::FromMinutes(5)) { $classification = 'inconclusive'; break }
        $payload = if ($step.Op -eq 'put') { [Convert]::ToBase64String($step.Bytes) } else { $null }
        $script:ActiveKey = $step.Key
        $observation = Invoke-ChildOperation $EndpointValue $step.Op $step.Key $step.Hash $step.Bytes.Length $payload $RunIdValue
        # 環境preflightで通信前に止まった場合を除き、PUT結果が不明でもremote objectを清掃対象にします。
        if ($step.Phase -eq 'unlocked-put' -and
            ($null -eq $observation -or $observation.StatusClass -ne 'environment-blocked')) {
            $unlockedCleanupRequired = $true
        }
        if ($null -ne $observation -and $step.Phase -eq 'unlocked-overwrite' -and $observation.StatusClass -eq 'too-many-requests') {
            Invoke-RouteDelay 1000
            $observation = Invoke-ChildOperation $EndpointValue $step.Op $step.Key $step.Hash $step.Bytes.Length $payload $RunIdValue
        } elseif ($null -ne $observation -and $step.Phase -in @('locked-overwrite','locked-delete') -and $observation.StatusClass -eq 'success') {
            Invoke-RouteDelay 1000
            $observation = Invoke-ChildOperation $EndpointValue $step.Op $step.Key $step.Hash $step.Bytes.Length $payload $RunIdValue
        }
        $passed = $false
        if ($null -ne $observation -and $observation.StatusClass -eq 'environment-blocked') {
            # 資格情報profileの不在・不整合は通信結果に混ぜず、開始条件不足として記録します。
            $classification = 'environment-blocked'
            $records.Add((New-ObservationRecord $classification $step.Phase $step.Op $null $step.Hash $step.Bytes.Length $null 'not-needed' $lockRule $script:Base $script:Head))
            break
        }
        if ($null -ne $observation) {
            if ($step.Phase -eq 'unsigned-get') { $passed = Test-UnsignedPrivacy $observation $step.Hash $step.Bytes.Length }
            elseif ($step.Phase -in @('locked-overwrite','locked-delete')) { $passed = Test-LockRejection $observation }
            elseif ($step.Phase -eq 'unlocked-delete-confirm') { $passed = $observation.StatusClass -eq 'not-found' -and $observation.S3Code -ceq 'NoSuchKey' }
            elseif ($step.Op -eq 'put') { $passed = $observation.StatusClass -eq 'success' -and $observation.HttpStatus -eq 200 }
            elseif ($step.Op -eq 'delete') { $passed = $observation.StatusClass -eq 'success' -and $observation.HttpStatus -eq 204 }
            else { $passed = Test-AuthenticatedMatch $observation $step.Hash $step.Bytes.Length $writerGeneration }
        }
        if ($passed -and $null -ne $writerGeneration -and $observation.Generation -cne $writerGeneration) {
            $passed = $false; $classification = 'inconclusive'
        } elseif ($passed -and $null -eq $writerGeneration) { $writerGeneration = $observation.Generation }
        if ($step.Phase -eq 'unlocked-delete-confirm' -and $passed) { $unlockedRemoved = $true }
        if (-not $passed) {
            if ($step.Phase -eq 'unsigned-get' -and $null -ne $observation -and $observation.PrefixSha256 -ceq $step.Hash) {
                # 一度のprefix一致は内容露出として記録しますが、再現条件を満たさないため能力failureへは確定しません。
                $classification = 'inconclusive'
            }
            else { $classification = 'inconclusive' }
            # PUT受理だけでは保持を証明しないため、途中失敗時のlock keyは明示的確認まで未確認にします。
            $records.Add((New-ObservationRecord $classification $step.Phase $step.Op $step.Key $step.Hash $step.Bytes.Length $observation 'unconfirmed' $lockRule $script:Base $script:Head))
            break
        }
        # locked keyは最終authenticated GETまで存在・hashを照合中なので、中間行で保持確定を先取りしません。
        $cleanup = if ($step.Phase -eq 'unlocked-delete') { 'delete-sent; awaiting-confirmation' } elseif ($step.Phase -eq 'unlocked-delete-confirm') { 'removed' } elseif ($step.Phase -like 'locked-*') { 'unconfirmed' } else { 'not-needed' }
        $records.Add((New-ObservationRecord 'pass' $step.Phase $step.Op $step.Key $step.Hash $step.Bytes.Length $observation $cleanup $lockRule $script:Base $script:Head))
    }
    if ($classification -eq 'pass' -and $records.Count -eq 12) {
        $records.Add((New-ObservationRecord 'pass' 'complete' 'route-proof' $null $null 0 $null $(if ($unlockedRemoved) { 'removed; locked object retained' } else { 'unconfirmed' }) $lockRule $script:Base $script:Head))
    } else {
        if ($unlockedCleanupRequired -and -not $unlockedRemoved) {
            if (-not $script:ChildTerminationConfirmed) {
                # 終了未確認のPUTが残る間は回復DELETEと競合し得るため、掃除を送らず未確認を維持します。
                $classification = 'inconclusive'
                $records.Add((New-ObservationRecord $classification 'recovery-cleanup' 'delete' $unlockedKey $changedHash $changed.Length $null 'unconfirmed; child termination unconfirmed' $lockRule $script:Base $script:Head))
            } else {
            # 本処理の5分期限とは別に合計30秒を確保し、DELETE応答だけでなくNoSuchKeyまで確認します。
            $cleanupDeadline = (Get-RouteElapsed) + [TimeSpan]::FromSeconds(30)
            $script:ActiveKey = $unlockedKey
            $cleanupObservation = Invoke-ChildOperation $EndpointValue 'delete' $unlockedKey $changedHash $changed.Length $null $RunIdValue $cleanupDeadline
            $records.Add((New-ObservationRecord $classification 'recovery-cleanup-delete' 'delete' $unlockedKey $changedHash $changed.Length $cleanupObservation 'unconfirmed' $lockRule $script:Base $script:Head))
            if ($null -ne $cleanupObservation -and $cleanupObservation.StatusClass -eq 'success' -and (Get-RouteElapsed) -lt $cleanupDeadline) {
                $confirmObservation = Invoke-ChildOperation $EndpointValue 'authenticated-get' $unlockedKey $changedHash $changed.Length $null $RunIdValue $cleanupDeadline
                $unlockedRemoved = $null -ne $confirmObservation -and $confirmObservation.StatusClass -eq 'not-found' -and $confirmObservation.S3Code -ceq 'NoSuchKey'
                $records.Add((New-ObservationRecord $classification 'recovery-cleanup-confirm' 'authenticated-get' $unlockedKey $changedHash $changed.Length $confirmObservation $(if ($unlockedRemoved) { 'removed' } else { 'unconfirmed' }) $lockRule $script:Base $script:Head))
            }
            if (-not $unlockedRemoved) { $classification = 'inconclusive' }
            }
        }
        $records.Add((New-ObservationRecord $classification 'complete' 'route-proof' $null $null 0 $null $(if ($unlockedRemoved) { 'removed' } else { 'unconfirmed' }) $lockRule $script:Base $script:Head))
    }
    [Array]::Clear($original, 0, $original.Length); [Array]::Clear($changed, 0, $changed.Length)
    $exit = switch ($classification) { 'pass' { 0 } 'provider-capability-failure' { 2 } 'environment-blocked' { 3 } default { 4 } }
    return [pscustomobject]@{ Records = $records; ExitCode = $exit }
}

function Get-StatusClass([int] $Status) {
    if ($Status -ge 200 -and $Status -le 299) { return 'success' }
    if ($Status -in @(301,302,303,307,308)) { return 'redirect' }
    if ($Status -eq 401) { return 'unauthorized' }
    if ($Status -eq 403) { return 'forbidden' }
    if ($Status -eq 404) { return 'not-found' }
    if ($Status -eq 429) { return 'too-many-requests' }
    if ($Status -ge 500 -and $Status -le 599) { return 'server-error' }
    return 'other'
}

function Assert-Observation($Observation) {
    $names = @($Observation.PSObject.Properties.Name)
    $actualNames = (($names | Sort-Object) -join ',')
    $expectedNames = (($script:ObservationProperties | Sort-Object) -join ',')
    if ($actualNames -cne $expectedNames) { throw 'Invalid transport observation.' }
    if ($Observation.Operation -notin @('put','authenticated-get','unsigned-get','delete') -or
        $Observation.StatusClass -notin @('success','unauthorized','forbidden','not-found','too-many-requests','server-error','redirect','other','timeout','transport-error','invalid-input','environment-blocked') -or
        $Observation.Generation -notmatch '\A[0-9a-f]{32}\z') { throw 'Invalid transport observation.' }
    foreach ($name in @('EofConfirmed','LimitReached','TimedOut','Redirected')) {
        if ($Observation.$name -isnot [bool]) { throw 'Invalid transport observation.' }
    }
    foreach ($name in @('HasAuthHeader','SignatureQueryPresent','TargetChangingQueryPresent')) {
        if ($Observation.$name -isnot [bool]) { throw 'Invalid transport observation.' }
    }
    if ($null -ne $Observation.Method -and $Observation.Method -isnot [string]) { throw 'Invalid transport observation.' }
    if ($null -ne $Observation.TargetUri -and $Observation.TargetUri -isnot [string]) { throw 'Invalid transport observation.' }
    if ($null -ne $Observation.HttpStatus -and (($Observation.HttpStatus -isnot [int] -and $Observation.HttpStatus -isnot [long]) -or $Observation.HttpStatus -lt 100 -or $Observation.HttpStatus -gt 599)) { throw 'Invalid transport observation.' }
    if ($null -ne $Observation.ByteCount -and (($Observation.ByteCount -isnot [int] -and $Observation.ByteCount -isnot [long]) -or $Observation.ByteCount -lt 0 -or $Observation.ByteCount -gt 8193)) { throw 'Invalid transport observation.' }
    foreach ($name in @('BodySha256','PrefixSha256')) {
        if ($null -ne $Observation.$name -and $Observation.$name -cnotmatch '\A[0-9a-f]{64}\z') { throw 'Invalid transport observation.' }
    }
    if ($null -ne $Observation.S3Code -and $Observation.S3Code -cnotmatch '\A[A-Za-z][A-Za-z0-9]{0,63}\z') { throw 'Invalid transport observation.' }
}

function Test-AuthenticatedMatch($Observation, [string] $Hash, [int] $Bytes, [string] $ExpectedGeneration = $null) {
    try { Assert-Observation $Observation } catch { return $false }
    return $Observation.StatusClass -eq 'success' -and $Observation.HttpStatus -eq 200 -and
        $Observation.EofConfirmed -and -not $Observation.LimitReached -and
        $Observation.ByteCount -eq $Bytes -and $Observation.BodySha256 -ceq $Hash -and
        $Observation.Generation -match '\A[0-9a-f]{32}\z' -and
        ($null -eq $ExpectedGeneration -or $Observation.Generation -ceq $ExpectedGeneration)
}

function Test-UnsignedPrivacy($Observation, [string] $Hash, [int] $Bytes) {
    try { Assert-Observation $Observation } catch { return $false }
    # 拒否statusだけを信用せず、非秘密の観測が意図したGET生成経路と一致するか確認します。
    $segments = @($script:ActiveKey -split '/' | ForEach-Object { [uri]::EscapeDataString($_) })
    $expectedUri = ([uri]($script:ActiveEndpoint + '/osm-artifacts/' + ($segments -join '/'))).AbsoluteUri
    if ($Observation.Method -cne 'GET' -or $Observation.TargetUri -cne $expectedUri -or
        $Observation.HasAuthHeader -or $Observation.SignatureQueryPresent -or $Observation.TargetChangingQueryPresent) { return $false }
    # status・class・S3 codeを同一応答の組として照合し、交差した矛盾値を拒否証拠にしません。
    # 400 InvalidArgumentも許可集合へ加えず、観測経路の妥当性を示せない場合はinconclusiveです。
    $pairedDenial = ($Observation.HttpStatus -eq 401 -and $Observation.StatusClass -ceq 'unauthorized' -and $Observation.S3Code -ceq 'Unauthorized') -or
        ($Observation.HttpStatus -eq 403 -and $Observation.StatusClass -ceq 'forbidden' -and $Observation.S3Code -ceq 'AccessDenied')
    return $pairedDenial -and $Observation.EofConfirmed -and
        -not $Observation.LimitReached -and -not $Observation.Redirected -and
        $null -ne $Observation.BodySha256 -and $Observation.BodySha256 -cne $Hash -and
        ($null -eq $Observation.PrefixSha256 -or $Observation.PrefixSha256 -cne $Hash)
}

function Test-LockRejection($Observation) {
    try { Assert-Observation $Observation } catch { return $false }
    return $Observation.HttpStatus -eq 403 -and $Observation.StatusClass -eq 'forbidden' -and
        $Observation.S3Code -ceq 'ObjectLockedByBucketPolicy' -and
        -not $Observation.TimedOut -and $Observation.EofConfirmed -and
        -not $Observation.LimitReached -and -not $Observation.Redirected
}

function New-ObservationRecord(
    [string] $Result,
    [string] $Phase,
    [string] $OperationName,
    [string] $KeyValue,
    [string] $ExpectedHashValue,
    [int] $ExpectedBytesValue,
    $Observation,
    [string] $Cleanup,
    $LockRule,
    [string] $Base,
    [string] $Head) {
    [pscustomobject][ordered]@{
        result = $Result; phase = $Phase; operation = $OperationName
        key = if ([string]::IsNullOrEmpty($KeyValue)) { $null } else { $KeyValue }
        expectedHash = $ExpectedHashValue
        observedHash = if ($null -eq $Observation) { $null } else { $Observation.BodySha256 }
        expectedBytes = $ExpectedBytesValue
        observedBytes = if ($null -eq $Observation) { $null } else { $Observation.ByteCount }
        prefixHash = if ($null -eq $Observation) { $null } else { $Observation.PrefixSha256 }
        eofConfirmed = if ($null -eq $Observation) { $false } else { [bool]$Observation.EofConfirmed }
        limitReached = if ($null -eq $Observation) { $false } else { [bool]$Observation.LimitReached }
        httpStatus = if ($null -eq $Observation) { $null } else { $Observation.HttpStatus }
        statusClass = if ($null -eq $Observation) { $null } else { $Observation.StatusClass }
        s3Code = if ($null -eq $Observation) { $null } else { $Observation.S3Code }
        generation = if ($null -eq $Observation) { $null } else { $Observation.Generation }
        lockRule = $LockRule; executedUtc = [DateTimeOffset]::UtcNow.ToString('o')
        cleanup = $Cleanup; base = $Base; head = $Head
    }
}

function Invoke-ChildOperation(
    [string] $EndpointValue,
    [string] $OperationName,
    [string] $KeyValue,
    [string] $ExpectedHashValue,
    [int] $Bytes,
    [string] $Payload,
    [string] $ExpectedRunId,
    [TimeSpan] $Deadline = [TimeSpan]::FromMinutes(5)) {
    $elapsed = Get-RouteElapsed
    if ($elapsed -ge $Deadline) { return $null }
    # 45秒の子上限と呼び出し側deadlineまでの短い方を、起動からpipe回収まで共有します。
    $remaining = [Math]::Min(45000, [int]($Deadline - $elapsed).TotalMilliseconds)
    if ($null -ne $script:RouteProofChildRunner -or $null -ne $script:RouteProofTransport) {
        try {
            # offlineも本番と同じ単一JSON行の境界を通し、PowerShell objectの直接受渡しを許しません。
            if ($null -ne $script:RouteProofChildRunner) {
                $childResult = & $script:RouteProofChildRunner $EndpointValue $OperationName $KeyValue $ExpectedHashValue $Bytes $Payload $ExpectedRunId $remaining $Deadline
            } else {
                $childResult = & $script:RouteProofTransport $EndpointValue $OperationName $KeyValue $ExpectedHashValue $Bytes $Payload $ExpectedRunId $remaining $Deadline
            }
            if ($null -eq $childResult) { return $null }
            if ($childResult -isnot [string]) { $childResult = $childResult | ConvertTo-Json -Compress -Depth 4 }
            $lines = @($childResult.Trim() -split "`r?`n" | Where-Object { $_ -ne '' })
            if ($lines.Count -ne 1) { return $null }
            $observation = $lines[0] | ConvertFrom-Json
            Assert-Observation $observation
            if ($observation.Operation -cne $OperationName) { return $null }
            return $observation
        } catch { return $null }
    }
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = 'pwsh'; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile','-File',$PSCommandPath,'-Endpoint',$EndpointValue,'-Child','-Operation',$OperationName,'-Key',$KeyValue,'-ExpectedHash',$ExpectedHashValue,'-ExpectedBytes',[string]$Bytes,'-RunId',$ExpectedRunId,'-ImplementationBase',$script:Base,'-ImplementationHead',$script:Head)) { [void]$info.ArgumentList.Add($argument) }
    if ($Payload) { [void]$info.ArgumentList.Add('-PayloadBase64'); [void]$info.ArgumentList.Add($Payload) }
    $budget = Get-RouteProofChildBudgetSchedule $remaining
    $operationStartTimestamp = [Diagnostics.Stopwatch]::GetTimestamp()
    [void]$info.ArgumentList.Add('-OperationStartTimestamp')
    [void]$info.ArgumentList.Add($operationStartTimestamp.ToString([Globalization.CultureInfo]::InvariantCulture))
    [void]$info.ArgumentList.Add('-OperationBudgetMilliseconds')
    [void]$info.ArgumentList.Add([string]$budget.TransportBudgetMilliseconds)
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
    $stdoutTask = $null; $stderrTask = $null
    try {
        if (-not $process.Start()) { return $null }
        $childStartedAt = Get-RouteElapsed
        # process起動に要した時間もrun予算から引き、45秒枠の外へ待機を延ばしません。
        $remaining = [Math]::Min($remaining, [Math]::Max(0, [int]($Deadline - (Get-RouteElapsed)).TotalMilliseconds))
        $terminationReserve = [Math]::Min(15000, [Math]::Max(1, [int]($remaining / 3)))
        $budget.ProcessWaitMilliseconds = [Math]::Min($budget.ProcessWaitMilliseconds, [Math]::Max(0, $remaining - $terminationReserve))
        # pipeを先に並行drainし、大きな出力で子が詰まることと終了後の同期ReadToEndを避けます。
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($budget.ProcessWaitMilliseconds)) {
            $script:ChildTerminationConfirmed = Stop-RouteProofChild $process $stdoutTask $stderrTask $remaining $childStartedAt
            return $null
        }
        # run全体の経過ではなく、この子を起動した後の経過を同じ子予算から差し引きます。
        if (-not (Wait-RouteProofPipes $stdoutTask $stderrTask $remaining $childStartedAt)) {
            $script:ChildTerminationConfirmed = Stop-RouteProofChild $process $stdoutTask $stderrTask $remaining $childStartedAt
            return $null
        }
        $stdout = $stdoutTask.GetAwaiter().GetResult().Trim()
        $stderr = $stderrTask.GetAwaiter().GetResult().Trim()
        if ($stderr -or [string]::IsNullOrWhiteSpace($stdout)) { return $null }
        $lines = @($stdout -split "`r?`n" | Where-Object { $_ -ne '' })
        if ($lines.Count -ne 1) { return $null }
        $observation = $lines[0] | ConvertFrom-Json
        Assert-Observation $observation
        if ($observation.Operation -cne $OperationName) { return $null }
        return $observation
    } catch {
        # 例外経路でも起動済みchildの生存を未確認のまま回復DELETEへ進ませません。
        try {
            if ($process.Id -gt 0 -and -not $process.HasExited) {
                $stopped = Stop-RouteProofChild $process $stdoutTask $stderrTask $remaining $childStartedAt
            } else { $stopped = $process.Id -gt 0 -and (Test-RouteProofChildStopped $true ($null -eq $stdoutTask -or $stdoutTask.IsCompleted) ($null -eq $stderrTask -or $stderrTask.IsCompleted)) }
            $script:ChildTerminationConfirmed = $stopped
        } catch { $script:ChildTerminationConfirmed = $false }
        return $null
    } finally { $process.Dispose() }
}

function Read-LockRule([string] $Json) {
    if ([string]::IsNullOrWhiteSpace($Json)) { throw 'Lock rule record unavailable.' }
    $rule = $Json | ConvertFrom-Json
    $names = @($rule.PSObject.Properties.Name)
    $expected = @('Prefix','Enabled','Kind','RetentionSeconds','RuleCount','DateRules','IndefiniteRules','WriterCanConfigure','LifecycleCompatible','BeforeHash')
    if ((($names | Sort-Object) -join ',') -cne (($expected | Sort-Object) -join ',') -or
        $rule.Prefix -isnot [string] -or $rule.Prefix -cne 'probe/locked/' -or $rule.Enabled -isnot [bool] -or -not $rule.Enabled -or
        $rule.Kind -isnot [string] -or $rule.Kind -cne 'Age' -or
        (($rule.RetentionSeconds -isnot [int]) -and ($rule.RetentionSeconds -isnot [long])) -or $rule.RetentionSeconds -lt 900 -or $rule.RetentionSeconds -gt 86400 -or
        (($rule.RuleCount -isnot [int]) -and ($rule.RuleCount -isnot [long])) -or $rule.RuleCount -ne 1 -or
        (($rule.DateRules -isnot [int]) -and ($rule.DateRules -isnot [long])) -or $rule.DateRules -ne 0 -or
        (($rule.IndefiniteRules -isnot [int]) -and ($rule.IndefiniteRules -isnot [long])) -or $rule.IndefiniteRules -ne 0 -or
        $rule.WriterCanConfigure -isnot [bool] -or $rule.WriterCanConfigure -or
        $rule.LifecycleCompatible -isnot [bool] -or -not $rule.LifecycleCompatible -or
        $rule.BeforeHash -isnot [string] -or
        $rule.BeforeHash -cnotmatch '\A[0-9a-f]{64}\z') {
        throw 'Lock rule record unavailable.'
    }
    return [pscustomobject][ordered]@{
        prefix = $rule.Prefix; enabled = [bool]$rule.Enabled; kind = $rule.Kind
        retentionSeconds = [int]$rule.RetentionSeconds; ruleCount = [int]$rule.RuleCount
        dateRules = [int]$rule.DateRules; indefiniteRules = [int]$rule.IndefiniteRules
        writerCanConfigure = [bool]$rule.WriterCanConfigure; lifecycleCompatible = [bool]$rule.LifecycleCompatible
        # 実際の事後設定はrun開始前には存在せず、外側の原記録で照合します。
        beforeHash = $rule.BeforeHash; afterHash = $null
    }
}

if ($Library) { return }

$base = $ImplementationBase; $head = $ImplementationHead
$script:Base = $base; $script:Head = $head

if ($Child) {
    try {
        # 親が起動直前に採った単調時計を受け取り、起動とmodule loadもoperation期限へ含めます。
        if (-not (Test-Endpoint $Endpoint) -or $ImplementationBase -cnotmatch '\A[0-9a-f]{40}\z' -or $ImplementationHead -cnotmatch '\A[0-9a-f]{40}\z' -or $Operation -notin @('put','authenticated-get','unsigned-get','delete') -or
            $OperationBudgetMilliseconds -lt 1 -or
            $OperationStartTimestamp -lt 1 -or
            ($Operation -eq 'put' -and ([string]::IsNullOrEmpty($PayloadBase64) -or $PayloadBase64.Length -gt 1368)) -or
            ($Operation -ne 'put' -and -not [string]::IsNullOrEmpty($PayloadBase64)) -or
            -not (Test-RunId $RunId) -or -not (Test-Key $Key $RunId) -or -not (Test-Hash $ExpectedHash) -or $ExpectedBytes -lt 1 -or $ExpectedBytes -gt 1024) { throw 'Invalid input.' }
        $payload = if ($PayloadBase64) { [Convert]::FromBase64String($PayloadBase64) } else { $null }
        if (-not (Test-ChildPayload $Operation $RunId $ExpectedHash $ExpectedBytes $payload)) { throw 'Invalid input.' }
        $transportModule = Import-Module (Join-Path $PSScriptRoot 'R2RouteTransport.psm1') -Force -PassThru
        $request = @{
            Endpoint = $Endpoint; Operation = $Operation; Key = $Key; Payload = $payload
            ExpectedBytes = $ExpectedBytes; DeadlineMilliseconds = $OperationBudgetMilliseconds
        }
        $transportInvoker = (Get-Command Invoke-R2RouteTransport -ErrorAction Stop).ScriptBlock
        $callback = New-RouteProofCredentialCallback $OperationStartTimestamp $OperationBudgetMilliseconds $transportInvoker
        $storeModule = Import-Module (Join-Path $PSScriptRoot '../Credentials/CredentialStore.psm1') -Force -PassThru
        try {
            # 通信前に既存profileを復号検査し、欠落や不一致を環境条件として明示します。
            $credentialStatus = Get-CredentialStatus 'osm'
            if (-not (Test-RouteProofCredentialStatus $credentialStatus)) {
                throw 'Credential profile does not match the requested route.'
            }
        } catch {
            $blocked = [pscustomobject][ordered]@{
                Operation = $Operation; HttpStatus = $null; StatusClass = 'environment-blocked'
                S3Code = $null; ByteCount = $null; BodySha256 = $null; PrefixSha256 = $null
                EofConfirmed = $false; LimitReached = $false; TimedOut = $false; Redirected = $false
                Generation = '00000000000000000000000000000000'
                Method = $null; TargetUri = $null; HasAuthHeader = $false
                SignatureQueryPresent = $false; TargetChangingQueryPresent = $false
            }
            [Console]::WriteLine(($blocked | ConvertTo-Json -Compress -Depth 4)); exit 3
        }
        $observation = Invoke-CredentialTransport 'osm' $callback $request
        Assert-Observation $observation
        [Console]::WriteLine(($observation | ConvertTo-Json -Compress -Depth 4))
        exit 0
    } catch {
        $fallback = [pscustomobject][ordered]@{
            Operation = if ($Operation) { $Operation } else { 'input' }; HttpStatus = $null; StatusClass = 'inconclusive'
            S3Code = $null; ByteCount = $null; BodySha256 = $null; PrefixSha256 = $null
            EofConfirmed = $false; LimitReached = $false; TimedOut = $false; Redirected = $false; Generation = '00000000000000000000000000000000'
            Method = $null; TargetUri = $null; HasAuthHeader = $false
            SignatureQueryPresent = $false; TargetChangingQueryPresent = $false
        }
        [Console]::WriteLine(($fallback | ConvertTo-Json -Compress -Depth 4)); exit 4
    }
}

try {
    if (-not (Test-Endpoint $Endpoint) -or $base -cnotmatch '\A[0-9a-f]{40}\z' -or $head -cnotmatch '\A[0-9a-f]{40}\z') { throw 'Invalid endpoint or implementation revision.' }
    if ($RunId) { if (-not (Test-RunId $RunId)) { throw 'Invalid run id.' } } else { $RunId = [Guid]::NewGuid().ToString('N') }
    $script:Base = $base; $script:Head = $head
    $script:ChildTerminationConfirmed = $true
    $result = Invoke-RouteProofLoop $Endpoint $RunId $LockRuleJson
    foreach ($record in $result.Records) { [Console]::WriteLine(($record | ConvertTo-Json -Compress -Depth 6)) }
    exit $result.ExitCode
} catch {
    # 入力エラー時は未検証のrevision文字列を外へ反射せず、検証済みIDかnullだけを記録します。
    $record = New-ObservationRecord 'environment-blocked' 'input' 'input' $null $null 0 $null 'not-needed' $null (Get-SafeCommitId $base) (Get-SafeCommitId $head)
    [Console]::WriteLine(($record | ConvertTo-Json -Compress -Depth 6)); exit 3
}
