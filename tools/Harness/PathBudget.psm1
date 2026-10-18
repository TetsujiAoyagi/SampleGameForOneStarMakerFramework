Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows の path 予算。契約の正本は <repo>/tools/path-budget.json だけで、この module は
# 読み込み・検証・判定の関数だけを持つ。contract-audit（push 前の静的検査）と run-tests
# （Unity 起動前の検査）が同じ判定を使うために分けてある。状態は持たない。
#
# 長さは .NET の string.Length（UTF-16 code unit）で数える。repo 相対 path の区切りは `/`。
# 管理対象は git の tracked と、未追跡で ignore されていない file。Library/ などの ignore 対象、
# テストが動的に作る path、Unity native 内部のアクセスはこの module の保証の外にある。

# Win32 の MAX_PATH（260）は終端 NUL を含む。path 文字列として使えるのは 259 文字まで。
# Unity 同梱の Mono は File.OpenRead などでこれを超える path を開けない（\\?\ 接頭辞も
# C:\?\ へ書き換えられて使えない）ため、予算はこの値から逆算する。
$script:MaxAbsolutePathLength = 259
$script:BudgetKeys = @('schemaVersion', 'maxRepoRelativePathLength', 'vendorAllowlist')
$script:AllowlistRoot = 'unity/Assets/'

function Get-PathBudgetSourcePath {
    param([Parameter(Mandatory)] [string]$Root)
    return [IO.Path]::Combine([IO.Path]::GetFullPath($Root), 'tools', 'path-budget.json')
}

function Test-JsonInteger([AllowNull()] [object]$Value) {
    # ConvertFrom-Json は 150.0 や 1e2 を Double にする。整数の表記だけを受け付ける。
    return ($Value -is [int] -or $Value -is [long])
}

function Assert-AllowlistPrefix([AllowNull()] [object]$Prefix) {
    # allowlist を広げれば検査を骨抜きにできる。NuGet の展開物のような、Unity native だけが
    # import し自前のコードが開かない vendor の folder に限る形だけを受け付ける。
    if ($Prefix -isnot [string] -or $Prefix.Length -eq 0) { throw 'vendorAllowlist の要素は空でない文字列にする' }
    if ($Prefix.Contains('\')) { throw "vendorAllowlist は / 区切りにする: $Prefix" }
    if ($Prefix.Contains('..')) { throw "vendorAllowlist に .. を含めない: $Prefix" }
    if (-not $Prefix.StartsWith($script:AllowlistRoot, [StringComparison]::Ordinal)) { throw "vendorAllowlist は $($script:AllowlistRoot) で始める: $Prefix" }
    if (-not $Prefix.EndsWith('/', [StringComparison]::Ordinal)) { throw "vendorAllowlist の末尾は / にする: $Prefix" }
    if ($Prefix.Length -le $script:AllowlistRoot.Length) { throw "vendorAllowlist が広すぎる: $Prefix" }
    foreach ($segment in $Prefix.Substring($script:AllowlistRoot.Length).TrimEnd('/').Split('/')) {
        if ($segment.Length -eq 0 -or $segment -ceq '.') { throw "vendorAllowlist に空・. の区間を含めない: $Prefix" }
    }
}

function Read-PathBudget {
    # 欠落・不正は例外にする。呼び出し側は例外を違反として報告し、検査を飛ばして成功にしない。
    param([Parameter(Mandatory)] [string]$Root)
    $path = Get-PathBudgetSourcePath $Root
    if (-not [IO.File]::Exists($path)) { throw "path 予算の正本がありません: $path" }
    try {
        $json = ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($path)) -AsHashtable -Depth 8
    } catch {
        throw "path 予算の正本を JSON として読めません: $path ($($_.Exception.Message))"
    }
    if ($json -isnot [Collections.IDictionary]) { throw "path 予算の正本が object ではありません: $path" }
    # 未知の key を黙って無視すると、綴りを誤った設定が効かないまま緑になる。
    foreach ($key in @($json.Keys)) {
        if ($script:BudgetKeys -cnotcontains $key) { throw "path 予算の正本に未知の key があります: $key" }
    }
    foreach ($key in $script:BudgetKeys) {
        if (-not $json.Contains($key)) { throw "path 予算の正本に $key がありません" }
    }
    if (-not (Test-JsonInteger $json.schemaVersion) -or $json.schemaVersion -ne 1) { throw 'path 予算の schemaVersion は 1 にする' }
    $max = $json.maxRepoRelativePathLength
    # 上限 258 は、ルートに最低 1 文字を残すため（ルートの長さ + R <= 259）。
    if (-not (Test-JsonInteger $max) -or $max -lt 1 -or $max -gt ($script:MaxAbsolutePathLength - 1)) {
        throw "maxRepoRelativePathLength は 1〜$($script:MaxAbsolutePathLength - 1) の整数にする: $max"
    }
    $allowlist = $json.vendorAllowlist
    if ($allowlist -is [string] -or $allowlist -isnot [Collections.IList]) { throw 'vendorAllowlist は配列にする' }
    foreach ($prefix in $allowlist) { Assert-AllowlistPrefix $prefix }
    return [pscustomobject]@{
        SourcePath = $path
        MaxRepoRelativePathLength = [int]$max
        VendorAllowlist = @([string[]]$allowlist)
        MaxRootLength = $script:MaxAbsolutePathLength - [int]$max
    }
}

function Get-PathBudgetRootLength {
    # 末尾の区切りを除いた絶対 path + 区切り 1 文字。repo 相対 path を足すと絶対 path の長さになる。
    param([Parameter(Mandatory)] [string]$Root)
    $full = [IO.Path]::GetFullPath($Root)
    return $full.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar).Length + 1
}

function Test-PathBudgetRoot {
    # 違反の説明を返す。適合すれば $null。置き場所は指定せず、長さだけを判定する。
    param([Parameter(Mandatory)] [string]$Root, [Parameter(Mandatory)] [psobject]$Budget)
    $length = Get-PathBudgetRootLength $Root
    if ($length -le $Budget.MaxRootLength) { return $null }
    return "checkout のルートが path 予算を超えています: $([IO.Path]::GetFullPath($Root))（$length 文字 > $($Budget.MaxRootLength) = $($script:MaxAbsolutePathLength) - R）"
}

function Invoke-PathBudgetGit([string]$Root, [string[]]$Arguments) {
    # 非 ASCII の path を Console の符号化や core.quotepath の引用に左右されずに数えるため、
    # UTF-8 で直接読む。
    $info = [Diagnostics.ProcessStartInfo]::new('git')
    foreach ($argument in @('-C', $Root, '-c', 'core.quotepath=off') + $Arguments) { [void]$info.ArgumentList.Add($argument) }
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
    $process = [Diagnostics.Process]::Start($info)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEnd()
        $text = $stdout.GetAwaiter().GetResult()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "git $($Arguments -join ' ') に失敗しました: $Root $stderr" }
        return $text
    } finally { $process.Dispose() }
}

function Get-PathBudgetManagedPaths {
    # 管理対象 (1): git の tracked と、未追跡で ignore されていない file（repo 相対、/ 区切り）。
    # ルートが worktree の最上位でなければ相対 path の基点がずれるので、失敗にする。
    param([Parameter(Mandatory)] [string]$Root)
    $full = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $top = (Invoke-PathBudgetGit $full @('rev-parse', '--show-toplevel')).Trim()
    $topFull = [IO.Path]::GetFullPath($top).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $comparison = if ([OperatingSystem]::IsWindows()) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    if (-not [string]::Equals($full, $topFull, $comparison)) { throw "path 予算のルートが git の最上位ではありません: $full（最上位 $topFull）" }
    $text = Invoke-PathBudgetGit $full @('ls-files', '--cached', '--others', '--exclude-standard', '-z')
    return @($text.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries) | Sort-Object -Unique)
}

function Find-PathBudgetViolations {
    # R を超える repo 相対 path を、長い順に返す。vendor の allowlist の中だけは対象外。
    param([AllowEmptyCollection()] [string[]]$Paths, [Parameter(Mandatory)] [psobject]$Budget)
    $found = foreach ($path in $Paths) {
        if ($path.Length -le $Budget.MaxRepoRelativePathLength) { continue }
        $allowed = $false
        foreach ($prefix in $Budget.VendorAllowlist) {
            if ($path.StartsWith($prefix, [StringComparison]::Ordinal)) { $allowed = $true; break }
        }
        if (-not $allowed) { [pscustomobject]@{ Path = $path; Length = $path.Length } }
    }
    return @($found | Sort-Object -Property @{ Expression = 'Length'; Descending = $true }, @{ Expression = 'Path'; Descending = $false })
}

function Find-AbsolutePathBudgetViolations {
    # 259 文字を超える絶対 path を返す。
    param([AllowEmptyCollection()] [string[]]$Paths)
    return @(foreach ($path in $Paths) {
        if ($path.Length -gt $script:MaxAbsolutePathLength) { [pscustomobject]@{ Path = $path; Length = $path.Length } }
    })
}

function Get-PathBudgetLaunchViolations {
    # Unity 起動前の検査。予算の正本 → ルート → 管理対象 (1) → 出力 path の順に判定し、
    # 違反の説明を全て返す。空なら起動してよい。予算を読めなければ R に依存する判定は
    # できないが、出力 path の絶対長は予算に依存しないので続けて判定する。
    param([Parameter(Mandatory)] [string]$Root, [AllowEmptyCollection()] [string[]]$OutputPaths = @())
    $violations = [Collections.Generic.List[string]]::new()
    $budget = $null
    try { $budget = Read-PathBudget $Root } catch { $violations.Add($_.Exception.Message) }
    if ($null -ne $budget) {
        $rootViolation = Test-PathBudgetRoot $Root $budget
        if ($rootViolation) { $violations.Add($rootViolation) }
        try {
            foreach ($item in (Find-PathBudgetViolations (Get-PathBudgetManagedPaths $Root) $budget)) {
                $violations.Add("repo 相対 path が予算を超えています: $($item.Path)（$($item.Length) 文字 > R = $($budget.MaxRepoRelativePathLength)）")
            }
        } catch { $violations.Add($_.Exception.Message) }
    }
    foreach ($item in (Find-AbsolutePathBudgetViolations $OutputPaths)) {
        $violations.Add("出力 path が $($script:MaxAbsolutePathLength) 文字を超えています: $($item.Path)（$($item.Length) 文字）")
    }
    return @($violations.ToArray())
}

Export-ModuleMember -Function Get-PathBudgetSourcePath, Read-PathBudget, Get-PathBudgetRootLength, Test-PathBudgetRoot,
    Get-PathBudgetManagedPaths, Find-PathBudgetViolations, Find-AbsolutePathBudgetViolations, Get-PathBudgetLaunchViolations
