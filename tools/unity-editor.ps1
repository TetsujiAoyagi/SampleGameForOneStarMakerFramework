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
    [Console]::Error.WriteLine('Usage: tools\unity-editor.cmd status | command <name> [args] | pipeline list [args]')
    exit 2
}

$primaryCommand = $UnityArguments[0].ToLowerInvariant()
$isAllowed = $primaryCommand -eq 'status' -or $primaryCommand -eq 'command'
if ($primaryCommand -eq 'pipeline') {
    $isAllowed = $UnityArguments.Count -ge 2 -and $UnityArguments[1].ToLowerInvariant() -eq 'list'
}

if (-not $isAllowed) {
    [Console]::Error.WriteLine("'$($UnityArguments -join ' ')' is not an already-open Editor operation. Use Unity CLI directly with normal approval for install, update, open, run, test, build, MCP setup, and other lifecycle commands.")
    exit 2
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'unity'
if (-not (Test-Path -LiteralPath (Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt') -PathType Leaf)) {
    throw "Unity project was not found at '$projectPath'."
}

$unityCli = Resolve-UnityCli
$env:UNITY_PROJECT_PATH = $projectPath
$env:UNITY_NO_BANNER = '1'
$env:UNITY_NO_PAGER = '1'

& $unityCli @UnityArguments
exit $LASTEXITCODE
