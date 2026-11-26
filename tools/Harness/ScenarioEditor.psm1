Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'UnityProcess.psm1')

# 窓のある Editor の session: 起動、ready、送る eval、読む eval、状態の待ち、終了の要求。
# Editor は Unity.exe の直接の起動だけで開き（-batchmode なし、窓あり）、その checkout の
# tools/unity-editor.cmd の eval で操作する（別の接続の実装を作らない）。
#
# eval の規則:
# - 基盤が送る全ての eval（送る、読む、終了の要求）は、先頭で Editor 自身の PID を、基盤が保持した PID と
#   比べ、違えば何もせずに wrong-target を返す。事前確認の後に別の Editor が同じ project を開いても、
#   その Editor を操作しない。
# - eval は呼び出しごとの印（GUID）を埋め、応答はその印と PID と値（UTF-8 の Base64）を返す。
#   読めたかどうかは、印と PID が合った応答だけで決める。CLI の終了コードや文言の分類に頼らない。
# - 送る eval は送り直さない（実行してから応答が timeout したのか、実行しなかったのかを区別できないため）。
#   成否は読む eval で状態を読んで確かめる。終了の要求だけは何度届いても結果が同じなので送り直してよい。
#
# 呼び出し側から受け取る物: Start（process の起動。UnityProcess の handle を返す）、Cli（checkout の root、
# 引数の列、期限の ms → ExitCode・Output・TimedOut）、Clock（Now()・Sleep(ms)）、Record（event と欄）、
# SaveRaw（名前と内容 → path）。この module は record も証拠の置き場も知らない。

$script:CliTimeoutMs = 60000

function New-ScenarioEvalCode {
    # body は C# の文の列で、最後に string を return する。
    param([Parameter(Mandatory)] [int]$ExpectedPid, [Parameter(Mandatory)] [string]$CallId, [Parameter(Mandatory)] [string]$Body)
    return @"
var __osmPid = System.Diagnostics.Process.GetCurrentProcess().Id;
if (__osmPid != $ExpectedPid) { return "OSMEVAL|$CallId|" + __osmPid + "|wrong-target|"; }
string __OsmBody() {
$Body
}
var __osmValue = __OsmBody() ?? "";
return "OSMEVAL|$CallId|" + __osmPid + "|ok|" + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(__osmValue));
"@
}

function ConvertFrom-ScenarioEvalResponse {
    # 応答の中から、この呼び出しの印を持つ行だけを読む。前の呼び出しの応答（別の印）は読まない。
    param([AllowNull()] [string]$Output, [Parameter(Mandatory)] [string]$CallId)
    $pattern = 'OSMEVAL\|' + [regex]::Escape($CallId) + '\|([0-9]+)\|(ok|wrong-target)\|([A-Za-z0-9+/=]*)'
    $match = [regex]::Match([string]$Output, $pattern)
    if (-not $match.Success) { return [pscustomobject]@{ Matched=$false; Pid=$null; Status=$null; Value=$null } }
    $value = $null
    if ($match.Groups[2].Value -ceq 'ok') { $value = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($match.Groups[3].Value)) }
    return [pscustomobject]@{ Matched=$true; Pid=[int]$match.Groups[1].Value; Status=$match.Groups[2].Value; Value=$value }
}

function New-ScenarioEditorSession {
    # 窓のある Editor を起動する。project lock を稼働中の Editor が持つ時は起動しない。持つ process の
    # いない lock の残骸は、消さずにそのまま起動する（Editor が自分で扱う）。
    param(
        [Parameter(Mandatory)] [scriptblock]$Start,
        [Parameter(Mandatory)] [scriptblock]$Cli,
        [Parameter(Mandatory)] [psobject]$Clock,
        [Parameter(Mandatory)] [scriptblock]$Record,
        [Parameter(Mandatory)] [scriptblock]$SaveRaw,
        [Parameter(Mandatory)] [string]$CheckoutRoot,
        [Parameter(Mandatory)] [string]$UnityExecutable,
        [Parameter(Mandatory)] [string]$LogPath,
        [string]$Name = 'editor'
    )
    $root = [IO.Path]::GetFullPath($CheckoutRoot).TrimEnd('\', '/')
    $project = Join-Path $root 'unity'
    $lock = Get-UnityProjectLockState $project
    if ($lock.Held) { throw "稼働中の Editor が project lock を持っているので起動しません: $($lock.Path)" }
    $argv = @('-projectPath', $project, '-logFile', $LogPath)
    $handle = & $Start $UnityExecutable ([string[]]$argv) $root @{ CreateWindow=$true; Environment=@{}; RedirectOutput=$false }
    & $Record 'editor-start' ([ordered]@{ pid=$handle.Id; startedUtc=$handle.StartedAt; argv=@($argv) })
    return [pscustomobject]@{
        Name=$Name; CheckoutRoot=$root; ProjectPath=$project; Handle=$handle; Pid=[int]$handle.Id; StartedUtc=$handle.StartedAt; LogPath=$LogPath
        Cli=$Cli; Clock=$Clock; Record=$Record; SaveRaw=$SaveRaw; Ready=$false; Calls=0; DryRun=$false
    }
}

function Invoke-ScenarioEditorCall {
    # 1 回の eval。code と原応答を保存し、印と PID で応答を読む。送り直しはしない。
    param([Parameter(Mandatory)] [psobject]$Session, [Parameter(Mandatory)] [string]$Kind, [Parameter(Mandatory)] [string]$Body, [bool]$RecordEvent = $true)
    $Session.Calls++
    $callId = [Guid]::NewGuid().ToString('N')
    $code = New-ScenarioEvalCode -ExpectedPid $Session.Pid -CallId $callId -Body $Body
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($code))
    $sentUtc = $Session.Clock.Now().ToString('o')
    $codePath = & $Session.SaveRaw ('{0}-eval-{1:D3}-{2}.cs' -f $Session.Name, $Session.Calls, $Kind) $code
    $cli = & $Session.Cli $Session.CheckoutRoot @('eval', $encoded) $script:CliTimeoutMs
    $raw = [ordered]@{ callId=$callId; kind=$Kind; sentUtc=$sentUtc; exitCode=$cli.ExitCode; timedOut=[bool]$cli.TimedOut; output=[string]$cli.Output }
    $responsePath = & $Session.SaveRaw ('{0}-eval-{1:D3}-{2}.response.json' -f $Session.Name, $Session.Calls, $Kind) (ConvertTo-Json -InputObject $raw -Depth 4)
    $parsed = ConvertFrom-ScenarioEvalResponse -Output ([string]$cli.Output) -CallId $callId
    $answered = $parsed.Matched -and $parsed.Status -ceq 'ok' -and $parsed.Pid -eq $Session.Pid
    if ($RecordEvent) {
        & $Session.Record 'eval' ([ordered]@{ kind=$Kind; callId=$callId; sentUtc=$sentUtc; evalPid=$parsed.Pid; answered=$answered; responsePath=$responsePath; codePath=$codePath })
    }
    return [pscustomobject]@{ CallId=$callId; Answered=$answered; WrongTarget=($parsed.Matched -and ($parsed.Status -ceq 'wrong-target' -or $parsed.Pid -ne $Session.Pid)); Pid=$parsed.Pid; Value=$parsed.Value; ResponsePath=$responsePath; TimedOut=[bool]$cli.TimedOut }
}

function Wait-ScenarioEditorReady {
    # ready は status の ready だけで決めず、PID の合った読む eval が通るまで待つ（import 中は 503 が続く）。
    # 起動した process が先に終われば、ready を待たずに起動の失敗にする。別の PID が応答したら起動の失敗にする。
    param([Parameter(Mandatory)] [psobject]$Session, [int]$TimeoutMs = 1800000, [int]$PollMs = 10000)
    $deadline = $Session.Clock.Now().AddMilliseconds($TimeoutMs)
    while ($true) {
        if ($Session.Handle.WaitForExit(0)) { throw "Editor（PID $($Session.Pid)）が ready の前に終わりました" }
        $call = Invoke-ScenarioEditorCall -Session $Session -Kind 'ready' -Body 'return "ready";' -RecordEvent $false
        if ($call.WrongTarget) { throw "起動した Editor（PID $($Session.Pid)）ではない Editor（PID $($call.Pid)）が応答しました" }
        if ($call.Answered) {
            $Session.Ready = $true
            & $Session.Record 'editor-ready' ([ordered]@{ pid=$Session.Pid; evalPid=$call.Pid; callId=$call.CallId; responsePath=$call.ResponsePath })
            return
        }
        if ($Session.Clock.Now() -ge $deadline) { throw "Editor（PID $($Session.Pid)）が期限までに ready になりませんでした" }
        $Session.Clock.Sleep($PollMs)
    }
}

function Read-ScenarioEditorEval {
    # 読む eval。一時的な失敗（接続できない、503、timeout、印の合わない応答）の後は読み直し、期限で止まる。
    param([Parameter(Mandatory)] [psobject]$Session, [Parameter(Mandatory)] [string]$Body, [int]$TimeoutMs = 60000, [int]$PollMs = 2000)
    $deadline = $Session.Clock.Now().AddMilliseconds($TimeoutMs)
    while ($true) {
        $call = Invoke-ScenarioEditorCall -Session $Session -Kind 'read' -Body $Body
        if ($call.WrongTarget) { throw "起動した Editor（PID $($Session.Pid)）ではない Editor（PID $($call.Pid)）が応答しました" }
        if ($call.Answered) { return $call.Value }
        if ($Session.Handle.WaitForExit(0)) { throw "Editor（PID $($Session.Pid)）が終わっています" }
        if ($Session.Clock.Now() -ge $deadline) { throw "読む eval が期限までに通りませんでした" }
        $Session.Clock.Sleep($PollMs)
    }
}

function Send-ScenarioEditorEval {
    # 送る eval。1 回だけ送り、完了を待たずに戻る。応答が無くても失敗にしない（状態を読んで確かめる）。
    param([Parameter(Mandatory)] [psobject]$Session, [Parameter(Mandatory)] [string]$Body)
    $call = Invoke-ScenarioEditorCall -Session $Session -Kind 'send' -Body $Body
    if ($call.WrongTarget) { throw "起動した Editor（PID $($Session.Pid)）ではない Editor（PID $($call.Pid)）が応答しました。何も実行していません" }
    return [pscustomobject]@{ Answered=$call.Answered; Value=$call.Value; CallId=$call.CallId; TimedOut=$call.TimedOut }
}

function Wait-ScenarioEditorEval {
    # 状態の待ち。読む eval を Until が真を返すまで繰り返し、期限で止まる。
    param([Parameter(Mandatory)] [psobject]$Session, [Parameter(Mandatory)] [string]$Body, [Parameter(Mandatory)] [scriptblock]$Until, [int]$TimeoutMs = 60000, [int]$PollMs = 2000)
    $deadline = $Session.Clock.Now().AddMilliseconds($TimeoutMs)
    while ($true) {
        $call = Invoke-ScenarioEditorCall -Session $Session -Kind 'read' -Body $Body
        if ($call.WrongTarget) { throw "起動した Editor（PID $($Session.Pid)）ではない Editor（PID $($call.Pid)）が応答しました" }
        if ($call.Answered -and [bool](& $Until $call.Value)) { return $call.Value }
        if ($Session.Handle.WaitForExit(0)) { throw "Editor（PID $($Session.Pid)）が終わっています" }
        if ($Session.Clock.Now() -ge $deadline) { throw "状態が期限までに条件を満たしませんでした" }
        $Session.Clock.Sleep($PollMs)
    }
}

function Stop-ScenarioEditorSession {
    # PID の照合を先頭に持つ EditorApplication.Exit(0) で終了を頼み、保持した handle の signal で確かめる。
    # 応答の error は失敗と読まない（終了の要求の応答は error になる）。handle が signal になるか期限が来るまで
    # 要求を送り直してよい。期限までに signal にならなければ Signaled=$false で返し、止めない。
    param([Parameter(Mandatory)] [psobject]$Session, [int]$TimeoutMs = 120000, [int]$PollMs = 5000)
    $deadline = $Session.Clock.Now().AddMilliseconds($TimeoutMs)
    while (-not $Session.Handle.WaitForExit(0)) {
        if ($Session.Clock.Now() -ge $deadline) { return [pscustomobject]@{ Signaled=$false; ExitCode=$null } }
        $call = Invoke-ScenarioEditorCall -Session $Session -Kind 'exit' -Body 'UnityEditor.EditorApplication.Exit(0); return "exit-requested";'
        if ($call.WrongTarget) { throw "終了の要求に、起動した Editor（PID $($Session.Pid)）ではない Editor（PID $($call.Pid)）が応答しました" }
        if ($Session.Handle.WaitForExit(0)) { break }
        $Session.Clock.Sleep($PollMs)
    }
    return [pscustomobject]@{ Signaled=$true; ExitCode=[int]$Session.Handle.GetExitCode() }
}

Export-ModuleMember -Function New-ScenarioEvalCode, ConvertFrom-ScenarioEvalResponse, New-ScenarioEditorSession, Wait-ScenarioEditorReady,
    Read-ScenarioEditorEval, Send-ScenarioEditorEval, Wait-ScenarioEditorEval, Stop-ScenarioEditorSession
