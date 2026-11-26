param([string]$ResultPath = '', [string]$Filter = '')
# 実機の検証の基盤（run・checkout・case の順序・隔離と解除）の offline test。
# 基盤が返した成否だけでなく、実の file の bytes、git の状態、record と原記録の file を直接見る。
. (Join-Path $PSScriptRoot 'ScenarioTestSupport.ps1')

try {
    Run 'AC4 names reject dot, separators, reserved device names and length' {
        foreach ($bad in @('.', '..', 'a/b', 'a\b', 'a.', '.a', 'a..b', 'NUL', 'nul.txt', 'COM1', 'con', 'Lpt9.log', ('a' * 33), '', 'a b', 'a:b')) {
            $threw = $false
            try { Assert-ScenarioName $bad 'name' } catch { $threw = $true }
            Assert $threw "accepted bad name '$bad'"
        }
        foreach ($good in @('s42', 'X', 'run-1', 'a.b_c-d', ('a' * 32), 'NULL', 'COM10')) { Assert-ScenarioName $good 'name' }
    }

    Run 'AC9 prepared checkout is created detached with a marker, then reused and switched' {
        $fx = New-FixtureRepo
        $script:bodyCalls = 0
        $s = New-Scenario -Name 'demo' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Body { param($c) $script:bodyCalls++; $script:seenRoot = $c.CheckoutRoot; 'v' }
        $r = Invoke-Scenario $s -RunId 'r1'
        $root = Join-Path $fx.Place 'demo-X'
        Assert ($r.ExitCode -eq 0 -and $script:bodyCalls -eq 1 -and $script:seenRoot -ceq $root) 'first run'
        Assert ([IO.File]::Exists((Join-Path (Get-WorktreeGitDir $root) 'osm-scenario/checkout.json'))) 'marker missing'
        Assert ((& git -C $root symbolic-ref -q HEAD) -eq $null -and (Invoke-Git $root @('rev-parse', 'HEAD')).Trim() -ceq $fx.Head) 'not detached at commit'
        $start = @(Read-Records $r.RunRoot | Where-Object { $_.event -ceq 'run-start' })[0]
        Assert ($start.checkouts.X.action -ceq 'create' -and $start.checkouts.X.unityVersion -ceq '6000.6.9f1' -and $start.commit -ceq $fx.Head) 'run-start fields'
        $r2 = Invoke-Scenario $s -RunId 'r2'
        $start2 = @(Read-Records $r2.RunRoot | Where-Object { $_.event -ceq 'run-start' })[0]
        Assert ($r2.ExitCode -eq 0 -and $start2.checkouts.X.action -ceq 'use') 'reuse'
        Write-Text (Join-Path $fx.Repo 'data/new.txt') "n`n"
        Invoke-Git $fx.Repo @('add', '-A') | Out-Null
        Invoke-Git $fx.Repo @('commit', '-q', '-m', 'next') | Out-Null
        $next = (Invoke-Git $fx.Repo @('rev-parse', 'HEAD')).Trim()
        $r3 = Invoke-Scenario $s -RunId 'r3' -Commit $next
        $start3 = @(Read-Records $r3.RunRoot | Where-Object { $_.event -ceq 'run-start' })[0]
        Assert ($r3.ExitCode -eq 0 -and $start3.checkouts.X.action -ceq 'switch' -and (Invoke-Git $root @('rev-parse', 'HEAD')).Trim() -ceq $next) 'switch'
    }

    Run 'AC4 the same RunId without -Only is refused before the prepared checkout is switched' {
        $fx = New-FixtureRepo
        $s = New-Scenario -Name 'again' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X'
        $r1 = Invoke-Scenario $s -RunId 'r'
        $root = Join-Path $fx.Place 'again-X'
        Write-Text (Join-Path $fx.Repo 'data/new.txt') "n`n"
        Invoke-Git $fx.Repo @('add', '-A') | Out-Null
        Invoke-Git $fx.Repo @('commit', '-q', '-m', 'next') | Out-Null
        $next = (Invoke-Git $fx.Repo @('rev-parse', 'HEAD')).Trim()
        $lines = @(Read-Records $r1.RunRoot).Count
        $c = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -Commit $next }
        Assert ($r1.ExitCode -eq 0 -and $c.Result.ExitCode -eq 1 -and $c.Err -like '*run の root が既にあります*') "refusal: $($c.Err)"
        Assert ((Invoke-Git $root @('rev-parse', 'HEAD')).Trim() -ceq $fx.Head -and @(Read-Records $r1.RunRoot).Count -eq $lines) 'the refused run switched the checkout or wrote records'
    }

    Run 'AC9 creation path checks refuse outside place, occupied path and long root' {
        $fx = New-FixtureRepo
        $long = Join-Path $fx.Place ('x' * 120)
        Assert (@(Get-ScenarioCheckoutCreationProblems -Path $long -Place $fx.Place -BudgetRoot $fx.Repo) | Where-Object { $_ -like '*path 予算*' }) 'long root accepted'
        Assert (@(Get-ScenarioCheckoutCreationProblems -Path (Join-Path $fx.Fixture 'elsewhere') -Place $fx.Place -BudgetRoot $fx.Repo) | Where-Object { $_ -like '*置き場の外*' }) 'outside place accepted'
        [void][IO.Directory]::CreateDirectory((Join-Path $fx.Place 'busy'))
        Assert (@(Get-ScenarioCheckoutCreationProblems -Path (Join-Path $fx.Place 'busy') -Place $fx.Place -BudgetRoot $fx.Repo) | Where-Object { $_ -like '*既に何か*' }) 'occupied path accepted'
    }

    Run 'AC1 preflight refuses unsafe checkouts without writing anything' {
        $fx = New-FixtureRepo
        $good = New-ManualWorktree $fx 'good' -Register
        $s = New-Scenario -Name 'pre' -Checkout 'X', 'Y'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Patches 'patches/p1.patch' -Body { $script:preBody++ }
        $script:preBody = 0
        $runRoot = Get-RunRoot $fx 'pre' 'r'
        function Assert-Refused([hashtable]$Roots, [string]$Expected, [string]$Label, [string]$Commit = '') {
            $headBefore = (Invoke-Git $good @('rev-parse', 'HEAD')).Trim()
            $c = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -CheckoutRoots $Roots -Commit $Commit }
            Assert ($c.Result.ExitCode -eq 1) "$Label not refused"
            Assert ($c.Err -like "*$Expected*") "$Label reason: $($c.Err)"
            Assert (-not [IO.Directory]::Exists($runRoot)) "$Label wrote the run root"
            Assert ($script:preBody -eq 0) "$Label ran the body"
            Assert ((Read-Bytes (Join-Path $good 'data/value.txt')) -ceq "one`n" -and (Invoke-Git $good @('rev-parse', 'HEAD')).Trim() -ceq $headBefore) "$Label changed X"
            Assert (-not [IO.File]::Exists((Join-Path (Get-WorktreeGitDir $good) 'osm-scenario/in-use.json'))) "$Label placed in-use"
        }
        $unmarked = New-ManualWorktree $fx 'unmarked'
        Assert-Refused @{ X=$good; Y=$unmarked } '基盤の印がありません' 'unmarked'
        Assert-Refused @{ X=$good; Y=$fx.Repo } '主の worktree' 'main worktree'
        $branch = New-ManualWorktree $fx 'branch' -Branch 'topic'
        Set-ScenarioCheckoutMarker -Info (Get-ScenarioCheckoutInfo $branch) -CommonDir $fx.CommonDir -Origin 'test'
        Assert-Refused @{ X=$good; Y=$branch } 'branch を checkout' 'branch'
        $merge = New-ManualWorktree $fx 'merging' -Register
        Write-Text (Join-Path (Get-WorktreeGitDir $merge) 'MERGE_HEAD') $fx.Head
        Assert-Refused @{ X=$good; Y=$merge } 'MERGE_HEAD' 'merge in progress'
        $dirty = New-ManualWorktree $fx 'dirty' -Register
        Write-Text (Join-Path $dirty 'data/value.txt') "dirty`n"
        Assert-Refused @{ X=$good; Y=$dirty } 'tracked の変更' 'dirty tracked'
        $staged = New-ManualWorktree $fx 'staged' -Register
        Write-Text (Join-Path $staged 'data/value.txt') "staged`n"; Invoke-Git $staged @('add', '-A') | Out-Null
        Assert-Refused @{ X=$good; Y=$staged } 'tracked の変更' 'staged'
        $untracked = New-ManualWorktree $fx 'untracked' -Register
        Write-Text (Join-Path $untracked 'new.txt') 'x'
        Assert-Refused @{ X=$good; Y=$untracked } '未追跡' 'untracked'
        Assert-Refused @{ X=$good; Y=$good } '同じ物' 'same checkout'
        $quarantined = New-ManualWorktree $fx 'quarantined' -Register
        Set-ScenarioCheckoutInUse -Info (Get-ScenarioCheckoutInfo $quarantined) -Record ([ordered]@{ runId='old' })
        Assert-Refused @{ X=$good; Y=$quarantined } '使用中の記録' 'quarantined'
        $reserved = New-ManualWorktree $fx 'reserved' -Register
        Set-ScenarioCheckoutReservation -Info (Get-ScenarioCheckoutInfo $reserved) -Scenario 'other' -RunId 'z' -RunRoot 'C:\nowhere' -Checkout 'Y'
        Assert-Refused @{ X=$good; Y=$reserved } '別の run の予約' 'reserved by another run'
        $locked = New-ManualWorktree $fx 'locked' -Register
        $lockPath = Join-Path $locked 'unity/Temp/UnityLockfile'
        [void][IO.Directory]::CreateDirectory((Split-Path $lockPath))
        $stream = [IO.File]::Open($lockPath, [IO.FileMode]::Create, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        try { Assert-Refused @{ X=$good; Y=$locked } 'project lock' 'held lock' } finally { $stream.Dispose() }
        Assert ([IO.File]::Exists($lockPath)) 'lock file was deleted'
        $leased = New-ManualWorktree $fx 'leased' -Register
        $lease = Enter-ScenarioCheckoutLease -CommonDir $fx.CommonDir -Path $leased
        try { Assert-Refused @{ X=$good; Y=$leased } 'lease' 'lease held by another call' } finally { $lease.Dispose() }
        $other = New-FixtureRepo
        $foreign = New-ManualWorktree $other 'foreign' -Register
        Set-ScenarioSeam @{ RepoRoot=$fx.Repo }
        Assert-Refused @{ X=$good; Y=$foreign } '別の repo' 'other repository'
        Invoke-Git $fx.Repo @('commit', '-q', '--allow-empty', '-m', 'move') | Out-Null
        $moved = (Invoke-Git $fx.Repo @('rev-parse', 'HEAD')).Trim()
        $y = New-ManualWorktree $fx 'yok' -Register
        Assert-Refused @{ X=$good; Y=$y } '対象の commit' 'explicit head mismatch' $moved
    }

    Run 'AC1 pending creation path is exclusive' {
        $fx = New-FixtureRepo
        $s = New-Scenario -Name 'pend' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Body { 1 }
        $target = Join-Path $fx.Place 'pend-X'
        $lease = Enter-ScenarioCheckoutLease -CommonDir $fx.CommonDir -Path $target
        try {
            $c = Invoke-Captured { Invoke-Scenario $s -RunId 'r' }
            Assert ($c.Result.ExitCode -eq 1 -and $c.Err -like '*lease*' -and -not [IO.Directory]::Exists($target) -and -not [IO.Directory]::Exists((Get-RunRoot $fx 'pend' 'r'))) 'pending path not exclusive'
        } finally { $lease.Dispose() }
    }

    Run 'AC2 patch is applied for the body and restored, in-use placed first and cleared after' {
        $fx = New-FixtureRepo
        $x = New-ManualWorktree $fx 'x' -Register
        $script:seen = $null
        $s = New-Scenario -Name 'patch' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Patches 'patches/p1.patch', 'patches/p2.patch' -Body {
            param($c)
            $inUse = Join-Path (Get-WorktreeGitDir $c.CheckoutRoot) 'osm-scenario/in-use.json'
            $script:seen = @((Read-Bytes (Join-Path $c.CheckoutRoot 'data/value.txt')), (Read-Bytes (Join-Path $c.CheckoutRoot 'data/other.txt')), [IO.File]::Exists($inUse))
            Write-Text (Join-Path $c.CheckoutRoot 'scratch.txt') 'untracked'
            Write-Text (Join-Path $c.CheckoutRoot 'unity/Library/keep.txt') 'ignored'
            'body-value'
        }
        $r = Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x }
        Assert ($r.ExitCode -eq 0) 'exit'
        Assert ($script:seen[0] -ceq "two`n" -and $script:seen[1] -ceq "b`n" -and $script:seen[2]) "body saw $($script:seen -join '|')"
        Assert ((Read-Bytes (Join-Path $x 'data/value.txt')) -ceq "one`n" -and (Read-Bytes (Join-Path $x 'data/other.txt')) -ceq "a`n") 'not restored'
        Assert (-not [IO.File]::Exists((Join-Path $x 'scratch.txt')) -and [IO.File]::Exists((Join-Path $x 'unity/Library/keep.txt'))) 'untracked/ignored handling'
        Assert (-not [IO.File]::Exists((Join-Path (Get-WorktreeGitDir $x) 'osm-scenario/in-use.json'))) 'in-use not cleared'
        $records = Read-Records $r.RunRoot
        $events = @($records | ForEach-Object { if ($_.event -ceq 'in-use') { "in-use:$($_.state)" } else { $_.event } })
        Assert (($events -join ',') -ceq 'run-start,case-start,in-use:placed,restore,in-use:cleared,case-end') "events $($events -join ',')"
        $end = @($records | Where-Object { $_.event -ceq 'case-end' })[0]
        $restore = @($records | Where-Object { $_.event -ceq 'restore' })[0]
        Assert ($end.base -ceq 'ok' -and $end.body -ceq 'body-value' -and $end.head -ceq $fx.Head -and $restore.gitStatusSame -and $restore.headSame -and $restore.indexClean) 'record fields'
        $base = Join-Path $r.RunRoot 'base/a/1'
        Assert ((Read-Bytes (Join-Path $base 'restore-before.work.diff')) -like '*+two*' -and (Read-Bytes (Join-Path $base 'restore-before.status.txt')) -like '*scratch.txt*') 'pre-restore evidence'
        Assert ((Read-Bytes (Join-Path $base 'before.status.txt')) -ceq '' -and (Read-Bytes (Join-Path $base 'after.status.txt')) -ceq '' -and (Read-Bytes (Join-Path $base 'after.head.txt')) -ceq $fx.Head) 'before/after evidence'
    }

    Run 'AC2 a failing patch applies nothing, skips the body and stops' {
        $fx = New-FixtureRepo
        $x = New-ManualWorktree $fx 'x' -Register
        $script:calls = 0
        $s = New-Scenario -Name 'badpatch' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Patches 'patches/p2.patch', 'patches/bad.patch' -Body { $script:calls++ }
        Add-ScenarioCase $s -Name 'b' -Checkout 'X' -Body { $script:calls++ }
        $r = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x } }
        Assert ($r.Result.ExitCode -eq 1 -and $script:calls -eq 0) 'body ran or exit 0'
        Assert ((Read-Bytes (Join-Path $x 'data/other.txt')) -ceq "a`n") 'first patch left applied'
        $records = Read-Records $r.Result.RunRoot
        Assert (@($records | Where-Object { $_.event -ceq 'case-end' -and $_.base -ceq 'patch' }).Count -eq 1 -and @($records | Where-Object { $_.case -ceq 'b' }).Count -eq 0) 'patch failure record'
    }

    Run 'AC2 a body exception restores tracked and untracked files and stops the rest' {
        $fx = New-FixtureRepo
        $x = New-ManualWorktree $fx 'x' -Register
        $script:calls = 0
        $s = New-Scenario -Name 'throws' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Body { param($c) Write-Text (Join-Path $c.CheckoutRoot 'data/value.txt') "edited`n"; Write-Text (Join-Path $c.CheckoutRoot 'n/new.txt') 'x'; throw 'boom' }
        Add-ScenarioCase $s -Name 'b' -Checkout 'X' -Body { $script:calls++ }
        $r = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x } }
        Assert ($r.Result.ExitCode -eq 1 -and $script:calls -eq 0 -and $r.Err -like '*boom*') 'exception not reported or later case ran'
        Assert ((Read-Bytes (Join-Path $x 'data/value.txt')) -ceq "one`n" -and -not [IO.Directory]::Exists((Join-Path $x 'n'))) 'not restored'
        Assert ((Invoke-Git $x @('status', '--porcelain')) -eq $null) 'status not clean'
    }

    Run 'AC2 a restore that does not return to the previous state stops and keeps the in-use record' {
        $fx = New-FixtureRepo
        $x = New-ManualWorktree $fx 'x' -Register
        $script:calls = 0
        $s = New-Scenario -Name 'headmove' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Body { param($c) Invoke-Git $c.CheckoutRoot @('-c', 'advice.detachedHead=false', 'checkout', '-q', '--detach', 'HEAD~1') | Out-Null }
        Add-ScenarioCase $s -Name 'b' -Checkout 'X' -Body { $script:calls++ }
        $r = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x } }
        $records = Read-Records $r.Result.RunRoot
        $restore = @($records | Where-Object { $_.event -ceq 'restore' })[0]
        Assert ($r.Result.ExitCode -eq 1 -and $script:calls -eq 0 -and -not $restore.headSame) 'head move accepted'
        Assert ([IO.File]::Exists((Join-Path (Get-WorktreeGitDir $x) 'osm-scenario/in-use.json'))) 'in-use cleared after a bad restore'
    }

    Run 'AC2 an evidence save failure still restores and fails the case' {
        $fx = New-FixtureRepo
        $x = New-ManualWorktree $fx 'x' -Register
        $s = New-Scenario -Name 'evfail' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Patches 'patches/p1.patch' -Body { param($c) Write-Text (Join-Path $c.Attempt.BaseDirectory 'restore-before.status.txt') 'occupied' }
        $r = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x } }
        $end = @(Read-Records $r.Result.RunRoot | Where-Object { $_.event -ceq 'case-end' })[0]
        Assert ($r.Result.ExitCode -eq 1 -and $end.base -ceq 'evidence') "evidence failure base=$($end.base)"
        Assert ((Read-Bytes (Join-Path $x 'data/value.txt')) -ceq "one`n" -and -not [IO.File]::Exists((Join-Path (Get-WorktreeGitDir $x) 'osm-scenario/in-use.json'))) 'not restored after a save failure'
    }

    Run 'AC5 -Only with an unknown name or no case writes nothing' {
        $fx = New-FixtureRepo
        $x = New-ManualWorktree $fx 'x' -Register
        $s = New-Scenario -Name 'only' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Body { 1 }
        $c = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -Only 'zz' -CheckoutRoots @{ X=$x } }
        Assert ($c.Result.ExitCode -eq 1 -and $c.Err -like '*zz*' -and -not [IO.Directory]::Exists((Get-RunRoot $fx 'only' 'r'))) 'unknown -Only'
        $empty = New-Scenario -Name 'empty' -Checkout 'X'
        $c2 = Invoke-Captured { Invoke-Scenario $empty -RunId 'r' -CheckoutRoots @{ X=$x } }
        Assert ($c2.Result.ExitCode -eq 1 -and -not [IO.Directory]::Exists((Get-RunRoot $fx 'empty' 'r'))) 'zero cases'
        $threw = $false
        try { Add-ScenarioCase $s -Name 'c' -Checkout 'Q' -Body { } } catch { $threw = $true }
        Assert $threw 'undeclared checkout accepted at registration'
    }

    Run 'AC4 run root continuation, dry-run separation, commit change and concurrent start' {
        $fx = New-FixtureRepo
        $x = New-ManualWorktree $fx 'x' -Register
        $s = New-Scenario -Name 'cont' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Body { 'a' }
        Add-ScenarioCase $s -Name 'b' -Checkout 'X' -Body { 'b' }
        $r = Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x } -Commit $fx.Head
        Assert ($r.ExitCode -eq 0) 'first'
        $again = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x } -Commit $fx.Head }
        Assert ($again.Result.ExitCode -eq 1 -and $again.Err -like '*-Only*') 'rerun without -Only accepted'
        $cont = Invoke-Scenario $s -RunId 'r' -Only 'b' -CheckoutRoots @{ X=$x } -Commit $fx.Head
        $records = Read-Records $r.RunRoot
        Assert ($cont.ExitCode -eq 0 -and @($records | Where-Object { $_.event -ceq 'run-start' })[1].continued -and @($records | Where-Object { $_.event -ceq 'case-start' -and $_.case -ceq 'b' -and $_.attempt -eq 2 }).Count -eq 1) 'continuation attempt numbering'
        $dry = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -Only 'a' -DryRun -CheckoutRoots @{ X=$x } -Commit $fx.Head }
        Assert ($dry.Result.ExitCode -eq 1 -and $dry.Err -like '*印が今回の実行と合いません*') 'dry-run continued a production run'
        $d = Invoke-Scenario $s -RunId 'dry' -DryRun -CheckoutRoots @{ X=$x } -Commit $fx.Head
        $prod = Invoke-Captured { Invoke-Scenario $s -RunId 'dry' -Only 'a' -CheckoutRoots @{ X=$x } -Commit $fx.Head }
        Assert ($d.ExitCode -eq 0 -and $prod.Result.ExitCode -eq 1) 'production continued a dry-run root'
        Invoke-Git $fx.Repo @('commit', '-q', '--allow-empty', '-m', 'm') | Out-Null
        $moved = (Invoke-Git $fx.Repo @('rev-parse', 'HEAD')).Trim()
        Invoke-Git $x @('-c', 'advice.detachedHead=false', 'checkout', '-q', '--detach', $moved) | Out-Null
        $changed = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -Only 'a' -CheckoutRoots @{ X=$x } -Commit $moved }
        Assert ($changed.Result.ExitCode -eq 1) 'continued after the commit changed'
        $lease = [IO.File]::Open((Join-Path $r.RunRoot 'scenario-run.lease'), [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        try {
            Invoke-Git $x @('-c', 'advice.detachedHead=false', 'checkout', '-q', '--detach', $fx.Head) | Out-Null
            $busy = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -Only 'a' -CheckoutRoots @{ X=$x } -Commit $fx.Head }
            Assert ($busy.Result.ExitCode -eq 1 -and $busy.Err -like '*別の呼び出し*') 'concurrent continuation accepted'
        } finally { $lease.Dispose() }
    }

    Run 'AC4 external root is created by the first user, marked, removed without following junctions, never recreated' {
        $fx = New-FixtureRepo
        $x = New-ManualWorktree $fx 'x' -Register
        $outside = Join-Path $fx.Fixture 'outside'
        Write-Text (Join-Path $outside 'keep.txt') 'keep'
        $drive = Join-Path $fx.Fixture 'drive'
        Set-ScenarioSeam @{ ExternalBase=$drive }
        $s = New-Scenario -Name 'ext' -Checkout 'X'
        $script:ext = $null
        Add-ScenarioCase $s -Name 'make' -Body { param($c) $script:ext = Get-ScenarioDirectory $c -Purpose 'cache'; Write-Text (Join-Path $script:ext 'f.txt') 'x'; New-Item -ItemType Junction -Path (Join-Path $script:ext 'link') -Target $outside | Out-Null }
        Add-ScenarioCase $s -Name 'clean' -Body { param($c) Remove-ScenarioDirectories $c }
        Add-ScenarioCase $s -Name 'again' -Body { param($c) Get-ScenarioDirectory $c -Purpose 'cache' }
        # 既定の基点は run の root の drive。実の drive の直下へ書かないように、test は基点を fixture へ向け、
        # 既定の規則は書き込みなしで別に確かめる。
        $probe = Open-ScenarioRun -Root (Join-Path $fx.Fixture 'probe-run') -Scenario 's42' -RunId 'r9' -Mode 'dry-run' -Commit '' -Checkouts ([ordered]@{})
        try { Assert ($probe.ExternalRoot -ceq [IO.Path]::Combine([IO.Path]::GetPathRoot($fx.Fixture), 'ovr', 's42', 'r9')) "default external root $($probe.ExternalRoot)" } finally { Close-ScenarioRun $probe }
        $r = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x } }
        $expectedRoot = [IO.Path]::Combine($drive, 'ovr', 'ext', 'r')
        Assert ($script:ext -ceq (Join-Path $expectedRoot 'cache')) "external path $($script:ext)"
        Assert ([IO.File]::Exists((Join-Path $outside 'keep.txt'))) 'removal followed the junction'
        Assert (-not [IO.Directory]::Exists($expectedRoot)) 'external root not removed'
        $end = @(Read-Records $r.Result.RunRoot | Where-Object { $_.event -ceq 'case-end' -and $_.case -ceq 'again' })[0]
        Assert ($r.Result.ExitCode -eq 1 -and $end.base -ceq 'body' -and ($end.failure -join ' ') -like '*片付け済み*') 'external root was recreated'
        $s2 = New-Scenario -Name 'ext2' -Checkout 'X'
        Add-ScenarioCase $s2 -Name 'dry' -Body { param($c) $script:dryPath = Get-ScenarioDirectory $c -Purpose 'cache' }
        $script:dryPath = $null
        $d = Invoke-Scenario $s2 -RunId 'r' -DryRun -CheckoutRoots @{ X=$x }
        Assert ($d.ExitCode -eq 0 -and $script:dryPath -and -not [IO.Directory]::Exists((Split-Path $script:dryPath))) 'dry-run created the external root'
        $foreignRoot = [IO.Path]::Combine($drive, 'ovr', 'ext3', 'r')
        [void][IO.Directory]::CreateDirectory($foreignRoot)
        $s3 = New-Scenario -Name 'ext3' -Checkout 'X'
        Add-ScenarioCase $s3 -Name 'use' -Body { param($c) Get-ScenarioDirectory $c -Purpose 'cache' }
        $f = Invoke-Captured { Invoke-Scenario $s3 -RunId 'r' -CheckoutRoots @{ X=$x } }
        Assert ($f.Result.ExitCode -eq 1 -and $f.Err -like '*印がありません*' -and @([IO.Directory]::GetFileSystemEntries($foreignRoot)).Count -eq 0) 'unmarked external root used'
    }

    Run 'AC4 managed files keep before-bytes, reserve the checkout across runs and are restored on request' {
        $fx = New-FixtureRepo
        $y = New-ManualWorktree $fx 'y' -Register
        $settings = Join-Path $y 'unity/UserSettings/settings.json'
        Write-Text $settings '{"keep":1}'
        $sA = New-Scenario -Name 'own' -Checkout 'Y'
        Add-ScenarioCase $sA -Name 'write' -Body { param($c) Set-ScenarioFile $c -Checkout 'Y' -Path 'unity/UserSettings/settings.json' -Content '{"cache":"A"}'; Set-ScenarioFile $c -Checkout 'Y' -Path 'unity/UserSettings/new.json' -Content 'new' }
        Add-ScenarioCase $sA -Name 'cleanup' -Body { param($c) Reset-ScenarioFiles $c -Checkout 'Y' }
        Add-ScenarioCase $sA -Name 'tracked' -Body { param($c) Set-ScenarioFile $c -Checkout 'Y' -Path 'data/value.txt' -Content 'no' }
        Add-ScenarioCase $sA -Name 'escape' -Body { param($c) Set-ScenarioFile $c -Checkout 'Y' -Path '../outside.txt' -Content 'no' }
        $a = Invoke-Scenario $sA -RunId 'A' -Only 'write' -CheckoutRoots @{ Y=$y }
        $gitDir = Get-WorktreeGitDir $y
        Assert ($a.ExitCode -eq 0 -and (Read-Bytes $settings) -ceq '{"cache":"A"}' -and [IO.File]::Exists((Join-Path $gitDir 'osm-scenario/reservation.json'))) 'write or reservation'
        Assert ((Read-Bytes (Join-Path $a.RunRoot 'managed/Y/0.before')) -ceq '{"keep":1}') 'before bytes not saved'
        $sB = New-Scenario -Name 'own' -Checkout 'Y'
        Add-ScenarioCase $sB -Name 'write' -Body { param($c) Set-ScenarioFile $c -Checkout 'Y' -Path 'unity/UserSettings/settings.json' -Content '{"cache":"B"}' }
        $b = Invoke-Captured { Invoke-Scenario $sB -RunId 'B' -CheckoutRoots @{ Y=$y } }
        Assert ($b.Result.ExitCode -eq 1 -and $b.Err -like '*別の run の予約*' -and (Read-Bytes $settings) -ceq '{"cache":"A"}' -and -not [IO.Directory]::Exists((Get-RunRoot $fx 'own' 'B'))) 'run B was not refused'
        $a2 = Invoke-Scenario $sA -RunId 'A' -Only 'write' -CheckoutRoots @{ Y=$y }
        Assert ($a2.ExitCode -eq 0) 'run A could not continue'
        Write-Text $settings '{"cache":"human"}'
        $a3 = Invoke-Captured { Invoke-Scenario $sA -RunId 'A' -Only 'write' -CheckoutRoots @{ Y=$y } }
        Assert ($a3.Result.ExitCode -eq 1 -and (Read-Bytes $settings) -ceq '{"cache":"human"}') 'external change was overwritten'
        Write-Text $settings '{"cache":"A"}'
        $clean = Invoke-Scenario $sA -RunId 'A' -Only 'cleanup' -CheckoutRoots @{ Y=$y }
        Assert ($clean.ExitCode -eq 0 -and (Read-Bytes $settings) -ceq '{"keep":1}' -and -not [IO.File]::Exists((Join-Path $y 'unity/UserSettings/new.json')) -and -not [IO.File]::Exists((Join-Path $gitDir 'osm-scenario/reservation.json'))) 'cleanup did not restore'
        $b2 = Invoke-Scenario $sB -RunId 'B' -CheckoutRoots @{ Y=$y }
        Assert ($b2.ExitCode -eq 0 -and (Read-Bytes $settings) -ceq '{"cache":"B"}') 'run B did not pass after cleanup'
        $t = Invoke-Captured { Invoke-Scenario $sA -RunId 'A' -Only 'tracked' -CheckoutRoots @{ Y=$y } }
        Assert ($t.Result.ExitCode -eq 1) 'tracked file accepted as managed'
    }

    Run 'AC4 managed file paths cannot leave the checkout' {
        $fx = New-FixtureRepo
        $run = [pscustomobject]@{ Root=(Join-Path $fx.Fixture 'runroot') }
        [void][IO.Directory]::CreateDirectory($run.Root)
        foreach ($bad in @('../x.txt', 'C:\x.txt', 'a/../../x', '')) {
            $threw = $false
            try { [void](Write-ScenarioManagedFile -Run $run -Checkout 'Y' -CheckoutRoot $fx.Repo -RelativePath $bad -Bytes ([byte[]]@(1))) } catch { $threw = $true }
            Assert $threw "accepted $bad"
        }
        $junction = Join-Path $fx.Repo 'unity/UserSettings'
        New-Item -ItemType Junction -Path $junction -Target $fx.Fixture | Out-Null
        $threw = $false
        try { [void](Write-ScenarioManagedFile -Run $run -Checkout 'Y' -CheckoutRoot $fx.Repo -RelativePath 'unity/UserSettings/x.json' -Bytes ([byte[]]@(1))) } catch { $threw = $true }
        Assert ($threw -and -not [IO.File]::Exists((Join-Path $fx.Fixture 'x.json'))) 'wrote through a junction'
        [IO.Directory]::Delete($junction)
    }

    Run 'AC3 quarantine blocks every run and release passes only when the conditions hold' {
        $fx = New-FixtureRepo
        $x = New-ManualWorktree $fx 'x' -Register
        $info = Get-ScenarioCheckoutInfo $x
        Set-ScenarioCheckoutInUse -Info $info -Record ([ordered]@{ runId='crashed'; case='a' })
        Add-ScenarioCheckoutInUseProcess -Info $info -ProcessId 4242 -StartedAt '2026-11-26T00:00:00Z' -Kind 'editor'
        $s = New-Scenario -Name 'q' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Body { 1 }
        $c = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x } }
        Assert ($c.Result.ExitCode -eq 1 -and $c.Err -like '*使用中の記録*') 'quarantined checkout accepted'
        $project = Join-Path $x 'unity'
        $script:procs = @([pscustomobject]@{ ProcessId=99; ParentProcessId=1; CommandLine="Unity.exe -projectPath $project"; CreationDate=(Get-Date) })
        $query = { $script:procs }
        $r1 = Invoke-ScenarioCheckoutOperation -Operation release -Path $x -RepoRoot $fx.Repo -ProcessQuery $query
        Assert (-not $r1.Ok -and ($r1.Problems -join ' ') -like '*PID 99*' -and [IO.File]::Exists((Join-Path (Get-WorktreeGitDir $x) 'osm-scenario/in-use.json'))) 'release with a live project process'
        $script:procs = @([pscustomobject]@{ ProcessId=98; ParentProcessId=1; CommandLine='Unity.exe -projectPath C:\other\unity'; CreationDate=(Get-Date) })
        Write-Text (Join-Path $x 'data/value.txt') "patched`n"
        $r2 = Invoke-ScenarioCheckoutOperation -Operation release -Path $x -RepoRoot $fx.Repo -ProcessQuery $query
        Assert (-not $r2.Ok -and ($r2.Problems -join ' ') -like '*tracked の変更*') 'release while not restored'
        Invoke-Git $x @('checkout', '--', '.') | Out-Null
        $lease = Enter-ScenarioCheckoutLease -CommonDir $fx.CommonDir -Path $x
        try { $r3 = Invoke-ScenarioCheckoutOperation -Operation release -Path $x -RepoRoot $fx.Repo -ProcessQuery $query; Assert (-not $r3.Ok) 'release while leased' } finally { $lease.Dispose() }
        $r4 = Invoke-ScenarioCheckoutOperation -Operation release -Path $x -RepoRoot $fx.Repo -ProcessQuery $query
        Assert ($r4.Ok -and -not [IO.File]::Exists((Join-Path (Get-WorktreeGitDir $x) 'osm-scenario/in-use.json'))) "release refused: $($r4.Problems -join '; ')"
        $ok = Invoke-Scenario $s -RunId 'r2' -CheckoutRoots @{ X=$x }
        Assert ($ok.ExitCode -eq 0) 'run after release'
    }

    Run 'AC4 unreserve restores by the before-record and refuses without it' {
        $fx = New-FixtureRepo
        $y = New-ManualWorktree $fx 'y' -Register
        $s = New-Scenario -Name 'unres' -Checkout 'Y'
        Add-ScenarioCase $s -Name 'write' -Body { param($c) Set-ScenarioFile $c -Checkout 'Y' -Path 'unity/UserSettings/OSMDemo.json' -Content 'x' }
        $r = Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ Y=$y }
        $file = Join-Path $y 'unity/UserSettings/OSMDemo.json'
        Assert ($r.ExitCode -eq 0 -and [IO.File]::Exists($file)) 'write'
        $u = Invoke-ScenarioCheckoutOperation -Operation unreserve -Path $y -RepoRoot $fx.Repo
        Assert ($u.Ok -and -not [IO.File]::Exists($file) -and -not [IO.File]::Exists((Join-Path (Get-WorktreeGitDir $y) 'osm-scenario/reservation.json'))) "unreserve: $($u.Problems -join '; ')"
        Set-ScenarioCheckoutReservation -Info (Get-ScenarioCheckoutInfo $y) -Scenario 'ghost' -RunId 'g' -RunRoot (Join-Path $fx.Fixture 'missing-run') -Checkout 'Y'
        $u2 = Invoke-ScenarioCheckoutOperation -Operation unreserve -Path $y -RepoRoot $fx.Repo
        Assert (-not $u2.Ok -and ($u2.Problems -join ' ') -like '*書く前の記録*' -and [IO.File]::Exists((Join-Path (Get-WorktreeGitDir $y) 'osm-scenario/reservation.json'))) 'unreserve without a before-record'
        Set-ScenarioCheckoutInUse -Info (Get-ScenarioCheckoutInfo $y) -Record ([ordered]@{ runId='x' })
        $u3 = Invoke-ScenarioCheckoutOperation -Operation unreserve -Path $y -RepoRoot $fx.Repo
        Assert (-not $u3.Ok -and ($u3.Problems -join ' ') -like '*release*') 'unreserve before release'
    }

    Run 'AC9 register and remove follow the same checks and never force' {
        $fx = New-FixtureRepo
        $plain = New-ManualWorktree $fx 'plain'
        Write-Text (Join-Path $plain 'u.txt') 'x'
        $r1 = Invoke-ScenarioCheckoutOperation -Operation register -Path $plain -RepoRoot $fx.Repo
        Assert (-not $r1.Ok -and -not [IO.File]::Exists((Join-Path (Get-WorktreeGitDir $plain) 'osm-scenario/checkout.json'))) 'registered a dirty worktree'
        Remove-Item -LiteralPath (Join-Path $plain 'u.txt')
        $r2 = Invoke-ScenarioCheckoutOperation -Operation register -Path $plain -RepoRoot $fx.Repo
        Assert ($r2.Ok) "register: $($r2.Problems -join '; ')"
        $r3 = Invoke-ScenarioCheckoutOperation -Operation register -Path $fx.Repo -RepoRoot $fx.Repo
        Assert (-not $r3.Ok) 'registered the main worktree'
        Write-Text (Join-Path $plain 'unity/Library/cache.bin') 'ignored'
        Set-ScenarioCheckoutReservation -Info (Get-ScenarioCheckoutInfo $plain) -Scenario 's' -RunId 'r' -RunRoot 'C:\x' -Checkout 'X'
        $r4 = Invoke-ScenarioCheckoutOperation -Operation remove -Path $plain -RepoRoot $fx.Repo
        Assert (-not $r4.Ok -and [IO.Directory]::Exists($plain)) 'removed a reserved checkout'
        Clear-ScenarioCheckoutReservation -Info (Get-ScenarioCheckoutInfo $plain)
        $unmarked = New-ManualWorktree $fx 'unmarked2'
        $r5 = Invoke-ScenarioCheckoutOperation -Operation remove -Path $unmarked -RepoRoot $fx.Repo
        Assert (-not $r5.Ok -and [IO.Directory]::Exists($unmarked)) 'removed an unmarked checkout'
        $r6 = Invoke-ScenarioCheckoutOperation -Operation remove -Path $plain -RepoRoot $fx.Repo
        $list = (Invoke-Git $fx.Repo @('worktree', 'list', '--porcelain')) -join "`n"
        Assert ($r6.Ok -and -not [IO.Directory]::Exists($plain) -and $list -notlike "*plain*") "remove: $($r6.Problems -join '; ')"
    }

    Run 'AC9 remove holds the lease until the worktree is removed' {
        $fx = New-FixtureRepo
        $target = New-ManualWorktree $fx 'held' -Register
        # 外す操作の直前に、別の呼び出しとして同じ checkout の lease を取りに行く（取れたら、外す間に別の
        # 呼び出しが事前確認を通れることになる）。Remove-ScenarioCheckout をこの scope で上書きして観測する。
        $script:leaseTaken = $null
        function Remove-ScenarioCheckout {
            param([string]$MainWorktree, [string]$Root)
            try { $other = Enter-ScenarioCheckoutLease -CommonDir $fx.CommonDir -Path $Root; $other.Dispose(); $script:leaseTaken = $true }
            catch { $script:leaseTaken = $false }
            ScenarioCheckout\Remove-ScenarioCheckout -MainWorktree $MainWorktree -Root $Root
        }
        $r = Invoke-ScenarioCheckoutOperation -Operation remove -Path $target -RepoRoot $fx.Repo
        Assert ($r.Ok -and -not [IO.Directory]::Exists($target)) "remove: $($r.Problems -join '; ')"
        Assert ($script:leaseTaken -eq $false) 'another invocation took the lease while the checkout was being removed'
        $after = Enter-ScenarioCheckoutLease -CommonDir $fx.CommonDir -Path $target
        $after.Dispose()
    }

    Run 'AC5 After writes scenario lines without event after the restore' {
        $fx = New-FixtureRepo
        $x = New-ManualWorktree $fx 'x' -Register
        $s = New-Scenario -Name 'after' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Patches 'patches/p1.patch' -Body { @{ status='dry-run' } } -After {
            param($c, $result)
            Write-ScenarioRecord $c @{ step='a'; status=$result.BodyValue.status; gitStatusSame=$result.GitStatusSame; gitStatusBefore=@($result.GitStatusBefore); gitStatusAfter=@($result.GitStatusAfter); value=(Read-Bytes (Join-Path $c.CheckoutRoot 'data/value.txt')) }
        }
        $r = Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x }
        $line = @(Read-Records $r.RunRoot | Where-Object { $_.Contains('step') })[0]
        Assert ($r.ExitCode -eq 0 -and -not $line.Contains('event') -and $line.gitStatusSame -and $line.value -ceq "one`n" -and $line.status -ceq 'dry-run' -and $line.utc) 'scenario line'
    }

    Run 'Op16 AC5 evidence names are not overwritten within an attempt' {
        $fx = New-FixtureRepo
        $x = New-ManualWorktree $fx 'x' -Register
        $s = New-Scenario -Name 'ev' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Body { param($c) Save-ScenarioArtifact $c -Name 'publish.json' -Content 'one' | Out-Null; Save-ScenarioArtifact $c -Name 'publish.json' -Content 'two' }
        $r = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x } }
        $path = Join-Path $r.Result.RunRoot 'evidence/a/1/publish.json'
        Assert ($r.Result.ExitCode -eq 1 -and (Read-Bytes $path) -ceq 'one') 'second publish overwrote the first'
    }
} finally { Complete-ScenarioTestRun 'Scenario' }
if ($failed.Count -gt 0) { exit 1 }
