[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $UnityArguments
)

$ErrorActionPreference = 'Stop'

function Resolve-UnityCli {
    $pathCommand = Get-Command unity -CommandType Application -ErrorAction SilentlyContinue
    if ($null -ne $pathCommand) {
        return $pathCommand.Source
    }

    if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        $installedPath = Join-Path $env:LOCALAPPDATA 'Unity\bin\unity.exe'
        if (Test-Path -LiteralPath $installedPath -PathType Leaf) {
            return $installedPath
        }
    }

    throw 'Unity CLI was not found on PATH or under %LOCALAPPDATA%\Unity\bin\unity.exe.'
}

if ($UnityArguments.Count -eq 0) {
    [Console]::Error.WriteLine('Usage: tools\unity-editor.cmd [--cd0-player-host] [global flags] status | command <name> [args] | eval <utf8-base64> | pipeline list [args]')
    exit 2
}

$useCd0PlayerHost = $UnityArguments[0].ToLowerInvariant() -eq '--cd0-player-host'
if ($useCd0PlayerHost) {
    if ($UnityArguments.Count -eq 1) {
        [Console]::Error.WriteLine('An allowed verb is required after --cd0-player-host.')
        exit 2
    }
    $UnityArguments = @($UnityArguments[1..($UnityArguments.Count - 1)])
}

$forbiddenConnectionFlags = @('--project-path', '--runtime', '--runtime-path')
foreach ($argument in $UnityArguments) {
    $normalizedArgument = $argument.ToLowerInvariant()
    if ($forbiddenConnectionFlags | Where-Object { $normalizedArgument -eq $_ -or $normalizedArgument.StartsWith("$_=") }) {
        [Console]::Error.WriteLine("Connection override '$argument' is forbidden. This wrapper always targets this repository's Unity Editor.")
        exit 2
    }
}

$globalFlagsWithoutValue = @(
    '--json', '--no-banner', '--no-pager', '--non-interactive', '--quiet', '--verbose',
    '--proxy-disable', '--log-proxy', '--no-log-proxy'
)
$globalFlagsWithValue = @('--format', '--proxy')
$globalArguments = [System.Collections.Generic.List[string]]::new()
$verbIndex = 0
while ($verbIndex -lt $UnityArguments.Count -and $UnityArguments[$verbIndex].StartsWith('-')) {
    $flag = $UnityArguments[$verbIndex].ToLowerInvariant()
    if ($globalFlagsWithoutValue -contains $flag) {
        $globalArguments.Add($UnityArguments[$verbIndex])
        $verbIndex++
        continue
    }

    if ($globalFlagsWithValue -contains $flag) {
        if ($verbIndex + 1 -ge $UnityArguments.Count) {
            [Console]::Error.WriteLine("Global flag '$($UnityArguments[$verbIndex])' requires a value.")
            exit 2
        }

        $globalArguments.Add($UnityArguments[$verbIndex])
        $globalArguments.Add($UnityArguments[$verbIndex + 1])
        $verbIndex += 2
        continue
    }

    [Console]::Error.WriteLine("Unsupported leading global flag '$($UnityArguments[$verbIndex])'.")
    exit 2
}

if ($verbIndex -ge $UnityArguments.Count) {
    [Console]::Error.WriteLine('An allowed verb is required after global flags.')
    exit 2
}

$primaryCommand = $UnityArguments[$verbIndex].ToLowerInvariant()
[string[]] $remainingArguments = @()
if ($verbIndex + 1 -lt $UnityArguments.Count) {
    $remainingArguments = @($UnityArguments[($verbIndex + 1)..($UnityArguments.Count - 1)])
}

$isAllowed = $primaryCommand -eq 'status' -or $primaryCommand -eq 'command' -or $primaryCommand -eq 'eval'
if ($primaryCommand -eq 'pipeline') {
    $isAllowed = $remainingArguments.Count -ge 1 -and $remainingArguments[0].ToLowerInvariant() -eq 'list'
}

if (-not $isAllowed) {
    [Console]::Error.WriteLine("'$($UnityArguments -join ' ')' is not an already-open Editor operation. Use Unity CLI directly with normal approval for install, update, open, run, test, build, MCP setup, and other lifecycle commands.")
    exit 2
}

if ($primaryCommand -eq 'eval') {
    if ($remainingArguments.Count -ne 1) {
        [Console]::Error.WriteLine('eval requires exactly one UTF-8 Base64 argument.')
        exit 2
    }

    try {
        $evalCode = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($remainingArguments[0]))
    }
    catch {
        [Console]::Error.WriteLine('eval argument is not valid Base64.')
        exit 2
    }

    $primaryCommand = 'command'
    $remainingArguments = @('eval', $evalCode)
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = if ($useCd0PlayerHost) {
    Join-Path $repositoryRoot 'artifacts\cd0\player-host\unity'
}
else {
    Join-Path $repositoryRoot 'unity'
}
if (-not (Test-Path -LiteralPath (Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt') -PathType Leaf)) {
    throw "Unity project was not found at '$projectPath'."
}

$unityCli = Resolve-UnityCli
$env:UNITY_NO_BANNER = '1'
$env:UNITY_NO_PAGER = '1'

$forwardArguments = [System.Collections.Generic.List[string]]::new()
$forwardArguments.AddRange([string[]]$globalArguments)
$forwardArguments.Add($primaryCommand)
if ($primaryCommand -eq 'status' -or $primaryCommand -eq 'command') {
    $forwardArguments.Add('--project-path')
    $forwardArguments.Add($projectPath)
}
$forwardArguments.AddRange([string[]]$remainingArguments)

Push-Location -LiteralPath $projectPath
try {
    & $unityCli @forwardArguments
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
