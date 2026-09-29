# 固定した base/head の Git 差分と、ローカルの offline suite を採取する。
# 成否は exit code だけではなく、case 集合、監査の1行、実ロードした DLL の hash で残す。
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../GatePolicy.psm1') -Global

function Invoke-Process([string]$FileName, [string[]]$Arguments, [string]$WorkingDirectory, [int]$TimeoutSeconds = 900) {
    # 標準出力は待ってから読むとパイプが埋まり、プロセスが戻らない。先に非同期で読む。
    # 期限超過はプロセスツリーを止める。Kill の失敗は、その直前に終了した競合として無視する。
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $FileName
    $info.WorkingDirectory = $WorkingDirectory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { [void]$info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $clock = [Diagnostics.Stopwatch]::StartNew()
    try {
        if (-not $process.Start()) { throw "processを起動できません: $FileName" }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try { $process.Kill($true) } catch {}
            [void]$process.WaitForExit(5000)
            throw "processが期限を超えました: $FileName"
        }
        [void]$stdout.Wait(5000); [void]$stderr.Wait(5000)
        $clock.Stop()
        return [pscustomobject]@{ ExitCode = $process.ExitCode; Stdout = $stdout.Result; Stderr = $stderr.Result; DurationMs = $clock.ElapsedMilliseconds }
    } finally { $process.Dispose() }
}

function Assert-RunPayload([string]$TaskDirectory, [object]$Run) {
    # ログの相対パスが task ディレクトリの外を指せないようにする。
    # logHash は各ファイル hash を並びのまま連結した値の hash。順番が変わると不一致になる。
    foreach ($step in @($Run.steps)) {
        $hashes = [Collections.Generic.List[string]]::new()
        foreach ($relative in @($step.logs)) {
            $path = [IO.Path]::GetFullPath([IO.Path]::Combine($TaskDirectory, $relative))
            if (-not $path.StartsWith(([IO.Path]::GetFullPath($TaskDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase) -or -not [IO.File]::Exists($path)) { throw "必須生ログが欠落しています: $relative" }
            $hashes.Add((Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash)
        }
        if ($hashes.Count -eq 0) { throw "必須生ログがありません: $($step.name)" }
        $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($hashes -join '|')))).ToLowerInvariant()
        if ($actual -cne $step.logHash) { throw "生ログhashが一致しません: $($step.name)" }
        if ($step.binaryPath) {
            if (-not [IO.File]::Exists($step.loadedPath)) { throw '実ロードDLLが見つかりません。' }
            $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $step.loadedPath).Hash.ToLowerInvariant()
            if ($hash -cne $step.loadedHash -or $hash -cne $step.binaryHashAfter) { throw '実ロードDLLがrun後に変わりました。' }
            if (@($step.dependencies).Count -eq 0) { throw '依存DLLのmanifestがありません。' }
            foreach ($dependency in @($step.dependencies | Where-Object status -eq 'loaded')) {
                if (-not [IO.File]::Exists($dependency.path) -or (Get-FileHash -Algorithm SHA256 -LiteralPath $dependency.path).Hash.ToLowerInvariant() -cne $dependency.hash) { throw "依存DLLがrun後に変わりました: $($dependency.name)" }
            }
        }
    }
}

function Get-GitScope([string]$Repo, [string]$Base, [string]$Head) {
    # 短縮 SHA と、候補の祖先でない base は受けない。作業ツリーの汚れは未追跡ファイルも含める。
    if ($Base -cnotmatch '^[a-f0-9]{40}$' -or $Head -cnotmatch '^[a-f0-9]{40}$') { throw 'base/headは完全なcommit SHAが必要です。' }
    foreach ($sha in @($Base, $Head)) {
        $check = Invoke-Process 'git' @('-C', $Repo, 'cat-file', '-t', $sha) $Repo 15
        if ($check.ExitCode -ne 0 -or $check.Stdout.Trim() -cne 'commit') { throw "Git commitがありません: $sha" }
    }
    $ancestor = Invoke-Process 'git' @('-C', $Repo, 'merge-base', '--is-ancestor', $Base, $Head) $Repo 15
    if ($ancestor.ExitCode -ne 0) { throw '凍結baseは候補headの祖先ではありません。' }
    # rename は状態・旧パス・新パスの3項になる。2項として読むとパスを取り違える。
    # --no-renames で畳まず、追加・変更・削除・タイプ変更以外は未対応として拒否する。
    $diff = Invoke-Process 'git' @('-C', $Repo, 'diff', '--name-status', '-z', '--no-renames', $Base, $Head, '--') $Repo 30
    if ($diff.ExitCode -ne 0) { throw 'Git差分を取得できません。' }
    $parts = @($diff.Stdout.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries))
    if ($parts.Count % 2 -ne 0) { throw 'Git name-status差分が不正です。' }
    $paths = [Collections.Generic.List[string]]::new()
    for ($i = 0; $i -lt $parts.Count; $i += 2) {
        if ($parts[$i] -cnotmatch '^[AMDT]$') { throw "未対応のGit差分種別です: $($parts[$i])" }
        $paths.Add($parts[$i + 1])
    }
    $dirty = Invoke-Process 'git' @('-C', $Repo, 'status', '--porcelain=v1', '--untracked-files=all') $Repo 30
    if ($dirty.ExitCode -ne 0) { throw 'Git作業ツリーを確認できません。' }
    return [pscustomobject]@{ Base = $Base; Head = $Head; Paths = @($paths); Dirty = @($dirty.Stdout -split "`n" | Where-Object { $_.Trim() }) }
}

function Test-GeneratedEvidencePath([string]$Path) {
    # 実行が生成するログ、結果、Phase の RESULT。製品データや通常のソースは証拠パスに含めない。
    $path = $Path.Replace('\', '/')
    if ($path -match '(^|/)TestResults/' -or $path -match '^tools/Harness/(runs|payload|inputs|receipts|specifications)/') { return $true }
    if ($path -match '^docs/handoff/.*(RESULT|REVISION).*\.md$') { return $true }
    if ($path -match '^artifacts/(bs2b|bs4|evidence|harness)/' -or $path -match '^tools/Artifacts/Probe/artifacts/') { return $true }
    return $false
}

function Find-GeneratedEvidenceAdds([string]$Repo, [string]$Base, [string]$Head) {
    # 最終差分だけだと、追加したあと削除した証拠が見えなくなる。commit ごとに追加を見る。
    # まだ commit していない index の追加も、同じ拒否にする。
    [void](Get-GitScope $Repo $Base $Head)
    $found = [Collections.Generic.List[object]]::new()
    $history = Invoke-Process 'git' @('-C', $Repo, 'rev-list', "$Base..$Head") $Repo 30
    if ($history.ExitCode -ne 0) { throw '候補commitの履歴を取得できません。' }
    $commits = @($history.Stdout -split "`n" | Where-Object { $_.Trim() })
    foreach ($commit in $commits) {
        $nameStatus = Invoke-Process 'git' @('-C', $Repo, 'diff-tree', '-m', '--root', '--no-commit-id', '--name-status', '-r', '-z', $commit.Trim()) $Repo 30
        if ($nameStatus.ExitCode -ne 0) { throw "commitを検査できません: $commit" }
        $pieces = @($nameStatus.Stdout.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries))
        if ($pieces.Count % 2 -ne 0) { throw "commitのname-statusが不正です: $commit" }
        for ($i = 0; $i -lt $pieces.Count; $i += 2) {
            if ($pieces[$i] -ceq 'A' -and (Test-GeneratedEvidencePath $pieces[$i + 1])) {
                $found.Add([pscustomobject]@{ source = $commit.Trim(); path = $pieces[$i + 1] })
            }
        }
    }
    $index = Invoke-Process 'git' @('-C', $Repo, 'diff', '--cached', '--name-status', '-z', '--diff-filter=A') $Repo 30
    if ($index.ExitCode -ne 0) { throw 'indexを検査できません。' }
    $indexPieces = @($index.Stdout.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries))
    if ($indexPieces.Count % 2 -ne 0) { throw 'indexのname-statusが不正です。' }
    $indexAdds = 0
    for ($i = 0; $i -lt $indexPieces.Count; $i += 2) {
        $indexAdds++
        if (Test-GeneratedEvidencePath $indexPieces[$i + 1]) { $found.Add([pscustomobject]@{ source = 'index'; path = $indexPieces[$i + 1] }) }
    }
    return [pscustomobject]@{ findings = @($found); commits = $commits.Count; indexAdds = $indexAdds }
}

function Invoke-LocalStep([string]$Repo, [string]$TaskDirectory, [string]$RunId, [string]$Name) {
    $payload = [IO.Path]::Combine($TaskDirectory, 'payload', $RunId)
    [IO.Directory]::CreateDirectory($payload) | Out-Null
    $sidecar = [IO.Path]::Combine($payload, "$Name.cases.json")
    $args = @()
    $binary = $null
    $binaryBefore = $null
    $binaryAfter = $null
    $loadedPath = $null
    $loadedHash = $null
    $moduleVersionId = $null
    $dependencies = @()
    $auditResult = $null
    $commands = [Collections.Generic.List[object]]::new()
    switch ($Name) {
        'artifacts-local' {
            $output = [IO.Path]::Combine($payload, 'build')
            # --no-restore は、実行の途中でパッケージ取得を始めないため。未復元なら build が失敗する。
            $build = Invoke-Process 'dotnet' @('build', [IO.Path]::Combine($Repo, 'tools/Artifacts/Probe/R2RouteTransport.csproj'), '-c', 'Release', '-o', $output, '--no-restore', '--nologo') $Repo 600
            $commands.Add(@('dotnet', 'build', 'tools/Artifacts/Probe/R2RouteTransport.csproj', '-c', 'Release', '-o', '<run-payload>/build', '--no-restore', '--nologo'))
            [IO.File]::WriteAllText([IO.Path]::Combine($payload, 'build.log'), $build.Stdout + $build.Stderr)
            # build に失敗した時点で case は無い。未実行の suite を成功件数に数えない。
            if ($build.ExitCode -ne 0) { return [pscustomobject]@{ name = $Name; kind = 'test'; status = 'failed'; exitCode = $build.ExitCode; registered = @(); selected = @(); executed = @(); logHash = (Get-FileHash -Algorithm SHA256 ([IO.Path]::Combine($payload, 'build.log'))).Hash.ToLowerInvariant(); durationMs = $build.DurationMs } }
            $binary = [IO.Path]::Combine($output, 'R2RouteTransport.dll')
            if (-not [IO.File]::Exists($binary)) { throw 'build後のDLLがありません。' }
            $binaryBefore = (Get-FileHash -Algorithm SHA256 $binary).Hash.ToLowerInvariant()
            $scripts = @('Credentials', 'RouteProof', 'R2RouteTransport')
        }
        'harness-local' { $scripts = @('Harness') }
        'contract-audit' { $scripts = @('ContractAudit') }
        'docs-audit' { $scripts = @('DocsAudit') }
        default { throw "未知のstepです: $Name" }
    }
    $registered = [Collections.Generic.List[string]]::new()
    $selected = [Collections.Generic.List[string]]::new()
    $executed = [Collections.Generic.List[string]]::new()
    $totalMs = 0L
    $exitCode = 0
    $logPaths = [Collections.Generic.List[string]]::new()
    if ($Name -ceq 'artifacts-local') { $logPaths.Add([IO.Path]::Combine($payload, 'build.log')) }
    foreach ($scriptName in $scripts) {
        $caseFile = [IO.Path]::Combine($payload, "$scriptName.cases.json")
        switch ($scriptName) {
            'Credentials' { $scriptPath = [IO.Path]::Combine($Repo, 'tools/Artifacts/tests/Credentials.Tests.ps1'); $args = @('-NoProfile', '-File', $scriptPath, '-ResultPath', $caseFile) }
            'RouteProof' { $scriptPath = [IO.Path]::Combine($Repo, 'tools/Artifacts/tests/RouteProof.Tests.ps1'); $args = @('-NoProfile', '-File', $scriptPath, '-ResultPath', $caseFile) }
            'R2RouteTransport' { $scriptPath = [IO.Path]::Combine($Repo, 'tools/Artifacts/tests/R2RouteTransport.Tests.ps1'); $args = @('-NoProfile', '-File', $scriptPath, '-AssemblyPath', $binary, '-ResultPath', $caseFile) }
            'Harness' { $scriptPath = [IO.Path]::Combine($Repo, 'tools/Harness/tests/Harness.Tests.ps1'); $args = @('-NoProfile', '-File', $scriptPath, '-ResultPath', $caseFile) }
            'ContractAudit' { $scriptPath = [IO.Path]::Combine($Repo, 'tools/contract-audit.ps1'); $args = @('-NoProfile', '-File', $scriptPath, '-HarnessTask', (Split-Path $TaskDirectory -Leaf)) }
            'DocsAudit' { $scriptPath = [IO.Path]::Combine($Repo, 'tools/docs-audit.ps1'); $args = @('-NoProfile', '-File', $scriptPath) }
        }
        $run = Invoke-Process 'pwsh' $args $Repo 600
        $commands.Add(@('pwsh', '-NoProfile', '-File', ([IO.Path]::GetRelativePath($Repo, $scriptPath))))
        $log = [IO.Path]::Combine($payload, "$scriptName.log")
        [IO.File]::WriteAllText($log, $run.Stdout + $run.Stderr)
        $logPaths.Add($log)
        $totalMs += $run.DurationMs
        if ($run.ExitCode -ne 0) { $exitCode = $run.ExitCode }
        if ($Name -like '*audit') {
            # exit 0 でも、この1行が無い、または files=0 / errors>0 なら失敗。
            # 契約検査は適用 task のときだけ applicable8 を要求し、未指定の緑を流用しない。
            $marker = @($run.Stdout -split "`n" | Where-Object { $_ -match '^AUDIT_RESULT ' })
            if ($marker.Count -ne 1 -or $marker[0] -notmatch 'files=(\d+) errors=(\d+) warnings=(\d+) checks=([0-9,]+)') { $exitCode = 1 }
            else {
                $auditResult = [pscustomobject]@{ files = [int]$Matches[1]; errors = [int]$Matches[2]; warnings = [int]$Matches[3]; checks = $Matches[4]; applicable8 = ($marker[0] -match 'applicable8=True') }
                if ($auditResult.files -eq 0 -or $auditResult.errors -gt 0 -or ($Name -ceq 'contract-audit' -and -not $auditResult.applicable8)) { $exitCode = 1 }
            }
        }
        if ($Name -like '*local') {
            if (-not [IO.File]::Exists($caseFile)) { $exitCode = 1; continue }
            $cases = Get-Content -LiteralPath $caseFile -Raw | ConvertFrom-Json
            foreach ($case in @($cases.registered)) { $registered.Add("$scriptName/$case") }
            foreach ($case in @($cases.selected)) { $selected.Add("$scriptName/$case") }
            foreach ($case in @($cases.executed)) { $executed.Add("$scriptName/$case") }
            if (@($cases.failed).Count -gt 0) { $exitCode = 1 }
            if ($scriptName -ceq 'R2RouteTransport') {
                # 依頼した DLL と、実際に読んだパス・実行前後の hash が一致するときだけ成功。
                # ファイルとして載った依存だけ hash を再確認する。メモリ上の依存にパスは無い。
                $loadedPath = $cases.loadedPath
                $loadedHash = $cases.loadedHashAfter
                $moduleVersionId = $cases.moduleVersionId
                if ($loadedPath -cne $binary -or $cases.loadedHashBefore -cne $binaryBefore -or $loadedHash -cne $binaryBefore -or -not $moduleVersionId) { $exitCode = 1 }
                $dependencies = @($cases.dependencies)
                if ($dependencies.Count -eq 0) { $exitCode = 1 }
                foreach ($dependency in $dependencies) {
                    if ($dependency.status -ceq 'loaded') {
                        if (-not [IO.File]::Exists($dependency.path) -or (Get-FileHash -Algorithm SHA256 -LiteralPath $dependency.path).Hash.ToLowerInvariant() -cne $dependency.hash) { $exitCode = 1 }
                    }
                }
            }
        }
    }
    if ($binary) {
        # 実行中に DLL が差し替わったら、この step は失敗にする。
        $binaryAfter = (Get-FileHash -Algorithm SHA256 $binary).Hash.ToLowerInvariant()
        if ($binaryBefore -cne $binaryAfter) { $exitCode = 1 }
    }
    if ($Name -like '*local') {
        # 集合不一致は run 全体を中断せず、この step を失敗として残す。他 step のログを失わない。
        try { Assert-CaseSets @($registered) @($selected) @($executed) $Name }
        catch { $exitCode = 1 }
    }
    $logHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes((@($logPaths | ForEach-Object { (Get-FileHash -Algorithm SHA256 $_).Hash }) -join '|')))).ToLowerInvariant()
    return [pscustomobject]@{ name = $Name; kind = $(if ($Name -like '*local') { 'test' } else { 'audit' }); status = $(if ($exitCode -eq 0) { 'passed' } else { 'failed' }); exitCode = $exitCode; argv = @($commands); cwd = $Repo; registered = @($registered); selected = @($selected); executed = @($executed); audit = $auditResult; logHash = $logHash; logs = @($logPaths | ForEach-Object { [IO.Path]::GetRelativePath($TaskDirectory, $_) }); binaryPath = $binary; binaryHashBefore = $binaryBefore; binaryHashAfter = $binaryAfter; loadedPath = $loadedPath; loadedHash = $loadedHash; moduleVersionId = $moduleVersionId; dependencies = @($dependencies); durationMs = $totalMs }
}

Export-ModuleMember -Function Invoke-Process,Get-GitScope,Test-GeneratedEvidencePath,Find-GeneratedEvidenceAdds,Invoke-LocalStep,Assert-RunPayload
