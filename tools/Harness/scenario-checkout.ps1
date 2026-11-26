<#
.SYNOPSIS
    実機の検証用の checkout を、実行の担当が登録・片付け・隔離の解除・予約の解除する入口。
.DESCRIPTION
    register  : 手で作った linked worktree（detached、clean、同じ repo）に基盤の印を置く。
    remove    : 印のある checkout を、clean で隔離も予約も無い時だけ `git worktree remove`（--force なし）で外す。
    release   : 隔離（使用中の記録が残った状態）を解除する。lock が保持されておらず、clean で、
                command line がその checkout の project を指す process が 1 つも無い時だけ記録を消す。
                process は CIM で読むだけで、止めない。先に Harness README の手順で checkout を手で戻す。
    unreserve : 放置された run の予約を外す。run の root にある書く前の記録のとおりに管理する file を
                戻せた時だけ予約を消す。隔離が残っていれば先に release を通す。
    4 つとも、その checkout の lease を取ってから行う（走っている run の checkout には触れない）。
.PARAMETER Operation
    register、remove、release、unreserve のどれか。
.PARAMETER Path
    対象の checkout の root。
#>
[CmdletBinding()]
param(
    [ValidateSet('register', 'remove', 'release', 'unreserve')] [string]$Operation = 'register',
    [string]$Path = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'UnityProcess.psm1')
Import-Module (Join-Path $PSScriptRoot 'ScenarioRun.psm1')
Import-Module (Join-Path $PSScriptRoot 'ScenarioCheckout.psm1')

function Invoke-ScenarioCheckoutOperation {
    # 結果の object（Ok と Problems）を返す。ProcessQuery と RepoRoot は offline test が差し替える。
    param(
        [Parameter(Mandatory)] [string]$Operation,
        [Parameter(Mandatory)] [string]$Path,
        [string]$RepoRoot = ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))),
        [scriptblock]$ProcessQuery = { Get-ScenarioSystemProcesses }
    )
    $repo = Get-ScenarioRepository $RepoRoot
    $root = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $problems = [Collections.Generic.List[string]]::new()
    $lease = $null
    try {
        $lease = Enter-ScenarioCheckoutLease -CommonDir $repo.CommonDir -Path $root
        $info = Get-ScenarioCheckoutInfo $root
        switch ($Operation) {
            'register' {
                if ($null -ne $info.Marker) { $problems.Add("既に基盤の印があります: $root"); break }
                foreach ($p in (Get-ScenarioCheckoutProblems -Info $info -CommonDir $repo.CommonDir -BudgetRoot $RepoRoot -RequireMarker $false)) { $problems.Add($p) }
                if ($problems.Count -eq 0) { Set-ScenarioCheckoutMarker -Info $info -CommonDir $repo.CommonDir -Origin 'registered' }
            }
            'remove' {
                foreach ($p in (Get-ScenarioCheckoutProblems -Info $info -CommonDir $repo.CommonDir -BudgetRoot $RepoRoot)) { $problems.Add($p) }
                if ($info.Exists -and $info.IsTop -and $null -ne $info.Reservation -and -not @($problems | Where-Object { $_ -like '*予約*' })) { $problems.Add("予約があります: $root") }
                if ($problems.Count -eq 0) {
                    # 外し終わるまで lease を持つ（離すと、その間に別の呼び出しが lease を取って事前確認を通れる）。
                    # lease の file は共通の git dir にあり、`git worktree remove` が消す worktree の git dir の外なので、
                    # 持ったまま外せる。
                    Remove-ScenarioCheckout -MainWorktree $repo.MainWorktree -Root $root
                }
            }
            'release' {
                if ($null -eq $info.Marker) { $problems.Add("基盤の印がありません: $root"); break }
                if ($null -eq $info.InUse) { $problems.Add("使用中の記録がありません（隔離されていません）: $root"); break }
                foreach ($p in (Get-ScenarioCheckoutProblems -Info $info -CommonDir $repo.CommonDir -BudgetRoot $RepoRoot)) {
                    # 使用中の記録そのものと、管理する file の予約は、この操作の拒否の理由にしない。
                    if ($p -like '使用中の記録が残っています*' -or $p -like '別の run の予約があります*') { continue }
                    $problems.Add($p)
                }
                foreach ($process in (Get-ScenarioProjectProcesses -Root $root -ProcessQuery $ProcessQuery)) {
                    $problems.Add("command line がこの checkout の project を指す process が残っています（PID $($process.ProcessId)）。run の物と確かめてから制約の手順で止めるか、確かめられなければ owner へ返します")
                }
                if ($problems.Count -eq 0) { Clear-ScenarioCheckoutInUse -Info $info }
            }
            'unreserve' {
                if ($null -eq $info.Marker) { $problems.Add("基盤の印がありません: $root"); break }
                if ($null -ne $info.InUse) { $problems.Add("隔離が残っています。先に release を通します: $root"); break }
                if ($null -eq $info.Reservation) { $problems.Add("予約がありません: $root"); break }
                try {
                    [void](Restore-ScenarioManagedFiles -RunRoot ([string]$info.Reservation.runRoot) -Checkout ([string]$info.Reservation.checkout) -CheckoutRoot $root)
                    Clear-ScenarioCheckoutReservation -Info $info
                } catch { $problems.Add($_.Exception.Message) }
            }
        }
    } catch { $problems.Add($_.Exception.Message) }
    finally { if ($null -ne $lease) { $lease.Dispose() } }
    return [pscustomobject]@{ Ok=($problems.Count -eq 0); Problems=@($problems.ToArray()); Operation=$Operation; Path=$root }
}

# dot-source は offline test 専用。
if ($MyInvocation.InvocationName -ne '.') {
    if (-not $Path) { [Console]::Error.WriteLine('-Path を指定します'); exit 2 }
    $result = Invoke-ScenarioCheckoutOperation -Operation $Operation -Path $Path
    foreach ($problem in $result.Problems) { [Console]::Error.WriteLine("[scenario-checkout] $problem") }
    if ($result.Ok) { [Console]::Out.WriteLine("[scenario-checkout] $Operation done: $($result.Path)"); exit 0 }
    exit 1
}
