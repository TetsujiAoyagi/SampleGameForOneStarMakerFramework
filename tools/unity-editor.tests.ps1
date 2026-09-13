$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$wrapper = Join-Path $PSScriptRoot 'unity-editor.ps1'
$cmdWrapper = Join-Path $PSScriptRoot 'unity-editor.cmd'
$expectedProject = Join-Path $repositoryRoot 'unity'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("osm-unity-editor-" + [Guid]::NewGuid().ToString('N'))
$fakeBin = Join-Path $testRoot 'bin'
$capturePath = Join-Path $testRoot 'capture.json'

function Assert-Equal($Expected, $Actual, [string] $Message) {
    if ($Expected -ne $Actual) {
        throw "$Message Expected='$Expected' Actual='$Actual'"
    }
}

function Invoke-Wrapper([string[]] $Arguments, [switch] $ThroughCmd) {
    Remove-Item -LiteralPath $capturePath -Force -ErrorAction SilentlyContinue
    $env:OSM_UNITY_EDITOR_TEST_CAPTURE = $capturePath
    if ($ThroughCmd) {
        & $cmdWrapper @Arguments 2>$null
    }
    else {
        & pwsh -NoLogo -NoProfile -File $wrapper @Arguments 2>$null
    }

    $exitCode = $LASTEXITCODE
    $capture = if (Test-Path -LiteralPath $capturePath) {
        $lines = @(Get-Content -LiteralPath $capturePath)
        [pscustomobject]@{
            currentDirectory = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($lines[0]))
            arguments = @($lines | Select-Object -Skip 1 | ForEach-Object {
                [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($_))
            })
        }
    }
    else {
        $null
    }

    [pscustomobject]@{ ExitCode = $exitCode; Capture = $capture }
}

New-Item -ItemType Directory -Path $fakeBin -Force | Out-Null
$fakeCliSource = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class Program
{
    public static int Main(string[] args)
    {
        var lines = new List<string>();
        lines.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(Environment.CurrentDirectory)));
        foreach (var argument in args)
            lines.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(argument)));
        File.WriteAllLines(Environment.GetEnvironmentVariable("OSM_UNITY_EDITOR_TEST_CAPTURE"), lines);
        return 0;
    }
}
'@
$fakeProject = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>unity</AssemblyName>
  </PropertyGroup>
</Project>
'@
Set-Content -LiteralPath (Join-Path $testRoot 'Program.cs') -Value $fakeCliSource
Set-Content -LiteralPath (Join-Path $testRoot 'fake-unity.csproj') -Value $fakeProject
$nugetConfig = Join-Path $testRoot 'NuGet.Config'
Set-Content -LiteralPath $nugetConfig -Value '<configuration><packageSources><clear /></packageSources></configuration>'
$originalAppData = $env:APPDATA
$env:APPDATA = $testRoot
try {
    & dotnet build (Join-Path $testRoot 'fake-unity.csproj') --output $fakeBin --nologo --verbosity quiet --configfile $nugetConfig | Out-Null
}
finally {
    $env:APPDATA = $originalAppData
}
if ($LASTEXITCODE -ne 0) { throw 'Failed to build the fake Unity CLI used by wrapper tests.' }

$originalPath = $env:PATH
try {
    $env:PATH = "$fakeBin;$originalPath"

    foreach ($denied in @(
        @(), @('open'), @('run'), @('test'), @('install'), @('mcp'), @('pipeline'),
        @('pipeline', 'install'), @('command', 'editor_play', '--runtime', 'Player'),
        @('command', 'eval', 'return 1;', '--project-path=C:\other'),
        @('--runtime-path', 'player-port', 'command', 'console')
    )) {
        $result = Invoke-Wrapper -Arguments $denied
        Assert-Equal 2 $result.ExitCode "Denied invocation '$($denied -join ' ')' did not fail with usage exit code."
        Assert-Equal $null $result.Capture "Denied invocation '$($denied -join ' ')' reached Unity CLI."
    }

    foreach ($allowed in @(
        @('status'),
        @('--json', 'status'),
        @('--format', 'json', 'command', 'console', '--tail', '5'),
        @('pipeline', 'list')
    )) {
        $result = Invoke-Wrapper -Arguments $allowed
        Assert-Equal 0 $result.ExitCode "Allowed invocation '$($allowed -join ' ')' failed."
        Assert-Equal $expectedProject $result.Capture.currentDirectory 'Wrapper did not set the Unity project as cwd.'
        if ($allowed -contains 'status' -or $allowed -contains 'command') {
            $projectIndex = [Array]::IndexOf([object[]]$result.Capture.arguments, '--project-path')
            if ($projectIndex -lt 0) { throw "Allowed invocation '$($allowed -join ' ')' did not inject --project-path." }
            Assert-Equal $expectedProject $result.Capture.arguments[$projectIndex + 1] 'Injected project path was incorrect.'
        }
    }

    $evalCode = 'return "a b";'
    $evalBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($evalCode))
    $eval = Invoke-Wrapper -ThroughCmd -Arguments @('eval', $evalBase64)
    Assert-Equal 0 $eval.ExitCode '.cmd eval invocation failed.'
    Assert-Equal $evalCode $eval.Capture.arguments[4] '.cmd did not preserve the decoded eval argument.'
    Assert-Equal $expectedProject $eval.Capture.currentDirectory '.cmd wrapper did not set the Unity project as cwd.'
}
finally {
    $env:PATH = $originalPath
    Remove-Item Env:OSM_UNITY_EDITOR_TEST_CAPTURE -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output 'unity-editor wrapper tests passed.'
