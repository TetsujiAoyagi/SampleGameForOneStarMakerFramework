Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
. (Join-Path $repo 'tools/run-tests.ps1')
$failed = [Collections.Generic.List[string]]::new(); $executed = 0
function Assert($condition,[string]$message) { if (-not $condition) { throw $message } }
function Run([string]$name,[scriptblock]$body) {
    $script:executed++
    try { & $body; [Console]::WriteLine("PASS $name") }
    catch { $script:failed.Add("$name : $($_.Exception.Message)"); [Console]::Error.WriteLine("FAIL $name : $($_.Exception.Message)") }
}
function New-Fixture([string]$id,[string]$project,[int]$expectedPid) {
    $hash = 'A' * 64
    $fixture = [ordered]@{
        schemaVersion=1; recordKind='unity-test-observation'; invocationId=$id; projectPath=$project; processId=$expectedPid; platform='EditMode'
        status='complete'; failure=@(); sealed=$true; sealSequence=9; sequence=9
        clock=[ordered]@{status='observed';frequency=1000;runStartedTicks=9007199254740993L;runFinishedTicks=9007199254741993L;runStartedUtc='2026-10-01T00:00:00Z';runFinishedUtc='2026-10-01T00:00:01Z';durationMs=1000.0}
        domains=@([ordered]@{ordinal=0;id='domain-a';bootstrapUtc='2026-10-01T00:00:00Z';bootstrapTicks=9007199254740000L;reloadBeforeUtc='';reloadBeforeTicks=0})
        selected=@([ordered]@{id='1';name='A';fullname='Suite.A';uniqueName='Tests/Suite/A';assemblyName='OneStarMaker.Tests.TestObservation.Editor';runState='Runnable'})
        events=@(
            [ordered]@{sequence=3;domainOrdinal=0;kind='started';id='1';result='';label='';utc='2026-10-01T00:00:00Z';ticks=9007199254740994L},
            [ordered]@{sequence=4;domainOrdinal=0;kind='finished';id='1';result='Passed';label='';utc='2026-10-01T00:00:01Z';ticks=9007199254741992L})
        results=@([ordered]@{id='1';name='A';fullname='Suite.A';result='Passed';label=''})
        assemblies=@(
            [ordered]@{domainOrdinal=0;observedAtUtc='2026-10-01T00:00:00Z';ticks=9007199254740993L;fullName='OneStarMaker.Editor.TestObservation, Version=1.0.0.0';location='C:/observer.dll';loadedModuleVersionId='mvid';diskSha256=$hash;status='observed';failure=''},
            [ordered]@{domainOrdinal=0;observedAtUtc='2026-10-01T00:00:00Z';ticks=9007199254740993L;fullName='OneStarMaker.Tests.TestObservation.Editor, Version=1.0.0.0';location='C:/tests.dll';loadedModuleVersionId='mvid';diskSha256=$hash;status='observed';failure=''},
            [ordered]@{domainOrdinal=0;observedAtUtc='2026-10-01T00:00:00Z';ticks=9007199254740993L;fullName='UnityEditor.TestRunner, Version=1.0.0.0';location='C:/editor.dll';loadedModuleVersionId='mvid';diskSha256=$hash;status='observed';failure=''},
            [ordered]@{domainOrdinal=0;observedAtUtc='2026-10-01T00:00:00Z';ticks=9007199254740993L;fullName='UnityEngine.TestRunner, Version=1.0.0.0';location='C:/engine.dll';loadedModuleVersionId='mvid';diskSha256=$hash;status='observed';failure=''})
    }
    Add-CaptureGroup $fixture 0 $fixture.clock.runFinishedUtc $fixture.clock.runFinishedTicks
    return $fixture
}
function Add-CaptureGroup($state,[int]$ordinal,[string]$utc,[long]$ticks) {
    # 同じロード済み集合でも、必須地点ごとの独立した capture stamp を持つ fixture にする。
    $template=@($state.assemblies | Where-Object { $_.domainOrdinal -eq 0 -and [long]$_.ticks -eq [long]$state.clock.runStartedTicks })
    foreach($assembly in $template) {
        $copy=ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $assembly) -AsHashtable
        $copy.domainOrdinal=$ordinal; $copy.observedAtUtc=$utc; $copy.ticks=$ticks
        $state.assemblies += $copy
    }
}
$xml = "<test-run result='Passed' total='1' passed='1' failed='0' skipped='0' inconclusive='0'><test-case id='1' name='A' fullname='Suite.A' result='Passed'/></test-run>"
$failedXml = "<test-run result='Failed' total='1' passed='0' failed='1' skipped='0' inconclusive='0'><test-case id='1' name='A' fullname='Suite.A' result='Failed' label='Error'/></test-run>"
function Policy($state,[string]$id,[string]$project,[int]$expectedPid,[AllowNull()][object]$progress=$null,[AllowNull()][object]$log='',[string]$xmlText=$xml) {
    $terminal = ConvertTo-Json -InputObject $state -Depth 32
    if ($null -eq $progress) { $progress = $terminal }
    return Get-UnityObservationResult -XmlText $xmlText -ObservationText $terminal -ProgressText $progress -LogText $log -ExpectedInvocation $id -ExpectedProject $project -ExpectedPid $expectedPid -ExpectedPath "C:/run/$id/observation.json"
}
$id = [guid]::NewGuid().ToString(); $project='C:/project'; $expectedPid=1234
Run 'complete state and int64 clock' {
    $state=New-Fixture $id $project $expectedPid; $result=Policy $state $id $project $expectedPid
    Assert ($result.status -ceq 'complete' -and [long]$result.clock.runStartedTicks -eq 9007199254740993L) 'valid observation failed or ticks rounded'
}
Run 'closed v1 top-level schema rejects unapproved flags' {
    $state=New-Fixture $id $project $expectedPid
    $expected=@('schemaVersion','recordKind','invocationId','projectPath','processId','platform','status','failure','sealed','sealSequence','sequence','clock','domains','selected','events','results','assemblies')
    Assert ($state.Count -eq $expected.Count -and @($expected | Where-Object { -not $state.Contains($_) }).Count -eq 0) 'fixture is not the frozen v1 schema'
    $state.runStarted=$true; $state.runFinished=$true
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'unapproved progress flags passed closed schema'
    $state=New-Fixture $id $project $expectedPid; $state['Count']=$expected.Count
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'Count key hid unknown top-level key'
}
Run 'closed v1 nested schema rejects unknown or missing keys' {
    $state=New-Fixture $id $project $expectedPid; $state.clock.extra='unfrozen'
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'unknown clock key passed'
    $state=New-Fixture $id $project $expectedPid; $state.clock['Count']=7
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'Count key hid unknown clock key'
    $state=New-Fixture $id $project $expectedPid; $state.domains[0].Remove('bootstrapUtc')
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'missing domain key passed'
    $state=New-Fixture $id $project $expectedPid; $state.selected[0].extra='unfrozen'
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'unknown selected key passed'
    $state=New-Fixture $id $project $expectedPid; $state.events[0].extra='unfrozen'
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'unknown event key passed'
    $state=New-Fixture $id $project $expectedPid; $state.results[0].extra='unfrozen'
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'unknown result key passed'
    $state=New-Fixture $id $project $expectedPid; $state.assemblies[0].extra='unfrozen'
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'unknown assembly key passed'
}
Run 'failed XML outcome remains separate from observation completeness' {
    $state=New-Fixture $id $project $expectedPid
    $state.results[0].result='Failed'; $state.results[0].label='Error'
    $state.events[1].result='Failed'; $state.events[1].label='Error'
    Assert ((Policy $state $id $project $expectedPid $null '' $failedXml).status -ceq 'complete') 'executed failed leaf made observation invalid'
    $xmlPolicy=Get-UnityTestResult -XmlText $failedXml -LogText 'fixture log' -ProcessExitCode 0
    Assert ($xmlPolicy.status -ceq 'failed') 'failed XML passed test policy'
    $state.status='incomplete'; $state.failure=@('実行 leaf の callback が欠測しました: 1'); $state.events=@()
    $result=Policy $state $id $project $expectedPid $null '' $failedXml
    Assert ($result.status -ceq 'incomplete' -and @($result.failure).Count -gt 0) 'nonexecuted failed leaf lost incomplete classification'
}
Run 'wrong invocation and PID' {
    $state=New-Fixture $id $project $expectedPid
    Assert ((Policy $state ([guid]::NewGuid().ToString()) $project $expectedPid).status -ceq 'invalid') 'wrong invocation passed'
    Assert ((Policy $state $id $project 4321).status -ceq 'invalid') 'wrong PID passed'
}
Run 'missing seal and stale progress' {
    $state=New-Fixture $id $project $expectedPid; $state.sealed=$false
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'unsealed state passed'
    $state.sealed=$true; $stale=ConvertTo-Json -InputObject $state -Depth 32; $state.sequence++
    Assert ((Policy $state $id $project $expectedPid $stale).status -ceq 'invalid') 'stale progress passed'
}
Run 'missing callback with passed XML' {
    $state=New-Fixture $id $project $expectedPid; $state.events=@($state.events | Where-Object kind -ne 'started')
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'missing callback passed'
}
Run 'duplicate attempt and changed assembly' {
    $state=New-Fixture $id $project $expectedPid; $state.events += (ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $state.events[0]) -AsHashtable); $state.events[-1].sequence=5
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'duplicate attempt passed'
    $state=New-Fixture $id $project $expectedPid; $second=ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $state.assemblies[0]) -AsHashtable; $second.diskSha256='B'*64; $state.assemblies += $second
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'assembly change passed'
}
Run 'error log veto and absent terminal' {
    $state=New-Fixture $id $project $expectedPid
    Assert ((Policy $state $id $project $expectedPid $null 'OSM_OBSERVATION_ERROR test').status -ceq 'invalid') 'error log passed'
    $result=Get-UnityObservationResult -XmlText $xml -ObservationText $null -ProgressText $null -LogText '' -ExpectedInvocation $id -ExpectedProject $project -ExpectedPid $expectedPid
    Assert ($result.status -ceq 'incomplete') 'missing terminal was not incomplete'
}
Run 'reload domain sequence' {
    $state=New-Fixture $id $project $expectedPid
    $state.domains[0].reloadBeforeUtc='2026-10-01T00:00:00Z'; $state.domains[0].reloadBeforeTicks=9007199254741200L
    $state.domains += [ordered]@{ordinal=1;id='domain-b';bootstrapUtc='2026-10-01T00:00:00Z';bootstrapTicks=9007199254741201L;reloadBeforeUtc='';reloadBeforeTicks=0}
    $state.events[1].domainOrdinal=1
    foreach($assembly in $state.assemblies) { if([long]$assembly.ticks -eq [long]$state.clock.runFinishedTicks){$assembly.domainOrdinal=1} }
    Add-CaptureGroup $state 1 $state.domains[1].bootstrapUtc $state.domains[1].bootstrapTicks
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'complete') 'normal reload rejected'
    $state.domains[0].reloadBeforeUtc=''
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'missing reload boundary passed'
}
Run 'required assembly capture groups cannot be borrowed' {
    $state=New-Fixture $id $project $expectedPid
    $state.assemblies=@($state.assemblies | Where-Object { [long]$_.ticks -ne [long]$state.clock.runFinishedTicks })
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'runfinish capture missing but accepted'
    $state=New-Fixture $id $project $expectedPid
    $state.assemblies=@($state.assemblies | Where-Object { [long]$_.ticks -ne [long]$state.clock.runStartedTicks })
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'runstart capture missing but accepted'
    $state=New-Fixture $id $project $expectedPid
    $state.domains[0].reloadBeforeUtc='2026-10-01T00:00:00Z'; $state.domains[0].reloadBeforeTicks=9007199254741200L
    $state.domains += [ordered]@{ordinal=1;id='domain-b';bootstrapUtc='2026-10-01T00:00:00Z';bootstrapTicks=9007199254741201L;reloadBeforeUtc='';reloadBeforeTicks=0}
    foreach($assembly in $state.assemblies) { if([long]$assembly.ticks -eq [long]$state.clock.runFinishedTicks){$assembly.domainOrdinal=1} }
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'reload resume capture missing but accepted'
}
Run 'reload after RunFinished is never complete' {
    $state=New-Fixture $id $project $expectedPid
    $finish=[long]$state.clock.runFinishedTicks
    $state.domains[0].reloadBeforeUtc='2026-10-01T00:00:02Z'; $state.domains[0].reloadBeforeTicks=$finish+10
    $state.domains += [ordered]@{ordinal=1;id='domain-after-finish';bootstrapUtc='2026-10-01T00:00:02Z';bootstrapTicks=$finish+11;reloadBeforeUtc='';reloadBeforeTicks=0}
    $state.sequence+=2; $state.sealSequence=$state.sequence
    Assert ((Policy $state $id $project $expectedPid).status -ceq 'invalid') 'late reload passed ordinary policy'
    $json=ConvertTo-Json -InputObject $state -Depth 32
    $strict=Get-UnityObservationResult -XmlText $xml -ObservationText $json -ProgressText $json -LogText '' -ExpectedInvocation $id -ExpectedProject $project -ExpectedPid $expectedPid -ExpectedPath "C:/run/$id/observation.json" -RequireReload $true
    Assert ($strict.status -ceq 'invalid') 'late reload passed required-reload policy'
}

# process seam は Unity を起動せず、required の argv/PID/step/logHash と非対応 platform を検査する。
$root=Join-Path ([IO.Path]::GetTempPath()) ('osm-h2b-test-'+[guid]::NewGuid())
try {
    [void][IO.Directory]::CreateDirectory($root)
    $testProject=Join-Path $root 'project with space/unity'; [void][IO.Directory]::CreateDirectory((Join-Path $testProject 'ProjectSettings'))
    [IO.File]::WriteAllText((Join-Path $testProject 'ProjectSettings/ProjectVersion.txt'),'m_EditorVersion: 6000.6.0f1')
    $exe=Join-Path $root 'Unity Folder/Unity.exe'; [void][IO.Directory]::CreateDirectory((Split-Path $exe)); [IO.File]::WriteAllText($exe,'fixture')
    $out=Join-Path $root 'output with space'
    $fake={ param($binary,$argv,$cwd)
        $obs=$argv[[array]::IndexOf($argv,'-osmTestObservation')+1]
        $rid=$argv[[array]::IndexOf($argv,'-osmTestInvocation')+1]
        $projectArg=$argv[[array]::IndexOf($argv,'-projectPath')+1]
        $state=New-Fixture $rid $projectArg 1234
        $json=ConvertTo-Json -InputObject $state -Depth 32
        [IO.File]::WriteAllText($obs,$json); [IO.File]::WriteAllText((Join-Path (Split-Path $obs) 'observation-progress.json'),$json)
        [IO.File]::WriteAllText($argv[[array]::IndexOf($argv,'-testResults')+1],$xml)
        [IO.File]::WriteAllText($argv[[array]::IndexOf($argv,'-logFile')+1],'fixture log')
        return [pscustomobject]@{Id=1234;StartedAt='now';EndedAt='later';DurationMs=1000;ExitCode=0;Failure=''}
    }
    Run 'runner emits v2 with PID and ordered hashes' {
        $r=Invoke-UnityTestRun -ProjectPath $testProject -UnityExe $exe -OutputRoot $out -ObserveUnity $true -ProcessInvoker $fake
        $step=Get-Content $r.StepPath -Raw | ConvertFrom-Json
        Assert ($r.ExitCode -eq 0 -and $step.schemaVersion -eq 2 -and $step.process.id -eq 1234 -and $step.observation.status -ceq 'complete') 'runner required failed'
        Assert ($step.logs.Count -eq 4 -and $step.logs[2] -ceq 'observation-progress.json' -and $step.logs[3] -ceq 'observation.json') 'log order wrong'
        Assert ($step.argv -contains '-osmTestInvocation' -and $step.argv -contains '-osmTestObservation') 'observer arguments missing'
    }
    $failedProcess={ param($binary,$argv,$cwd)
        $obs=$argv[[array]::IndexOf($argv,'-osmTestObservation')+1]
        $rid=$argv[[array]::IndexOf($argv,'-osmTestInvocation')+1]
        $projectArg=$argv[[array]::IndexOf($argv,'-projectPath')+1]
        $state=New-Fixture $rid $projectArg 1234
        $state.results[0].result='Failed'; $state.results[0].label='Error'
        $state.events[1].result='Failed'; $state.events[1].label='Error'
        if ($script:nonexecutedFailure) {
            $state.status='incomplete'; $state.failure=@('実行 leaf の callback が欠測しました: 1'); $state.events=@()
        }
        $json=ConvertTo-Json -InputObject $state -Depth 32
        [IO.File]::WriteAllText($obs,$json); [IO.File]::WriteAllText((Join-Path (Split-Path $obs) 'observation-progress.json'),$json)
        [IO.File]::WriteAllText($argv[[array]::IndexOf($argv,'-testResults')+1],$failedXml)
        [IO.File]::WriteAllText($argv[[array]::IndexOf($argv,'-logFile')+1],'fixture log')
        return [pscustomobject]@{Id=1234;StartedAt='now';EndedAt='later';DurationMs=1000;ExitCode=0;Failure=''}
    }
    Run 'runner keeps failed XML and complete observation distinct' {
        $script:nonexecutedFailure=$false
        $r=Invoke-UnityTestRun -ProjectPath $testProject -UnityExe $exe -OutputRoot $out -ObserveUnity $true -ProcessInvoker $failedProcess
        $step=Get-Content $r.StepPath -Raw | ConvertFrom-Json
        Assert ($r.ExitCode -eq 1 -and $step.status -ceq 'failed' -and $step.observation.status -ceq 'complete') 'failed XML and complete observation were conflated'
    }
    Run 'runner keeps nonexecuted failed observation incomplete' {
        $script:nonexecutedFailure=$true
        $r=Invoke-UnityTestRun -ProjectPath $testProject -UnityExe $exe -OutputRoot $out -ObserveUnity $true -ProcessInvoker $failedProcess
        $step=Get-Content $r.StepPath -Raw | ConvertFrom-Json
        Assert ($r.ExitCode -eq 1 -and $step.status -ceq 'failed' -and $step.observation.status -ceq 'incomplete') 'nonexecuted failed observation was not incomplete'
    }
    Run 'unsupported platform never starts process' {
        $script:called=$false
        $noStart={ param($binary,$argv,$cwd) $script:called=$true; throw 'must not run' }
        $r=Invoke-UnityTestRun -ProjectPath $testProject -UnityExe $exe -OutputRoot $out -Platform 'PlayMode' -ObserveUnity $true -ProcessInvoker $noStart
        $step=Get-Content $r.StepPath -Raw | ConvertFrom-Json
        Assert ($r.ExitCode -eq 1 -and $step.schemaVersion -eq 2 -and -not $script:called) 'unsupported platform started'
    }
} finally {
    $resolved=[IO.Path]::GetFullPath($root); $temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolved.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notlike 'osm-h2b-test-*') { throw 'unsafe cleanup' }
    if ([IO.Directory]::Exists($resolved)) { [IO.Directory]::Delete($resolved,$true) }
}
[Console]::WriteLine("Unity observation offline: $executed executed, $($failed.Count) failed")
if ($failed.Count -gt 0) { exit 1 }
