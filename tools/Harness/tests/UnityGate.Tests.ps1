param([string]$ResultPath = '', [string]$Filter = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../Adapters/UnityChecks.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../UnityGatePolicy.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../Adapters/UnityTestOutput.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../ApprovedSpecifications.psm1') -Force
$registered=[Collections.Generic.List[string]]::new(); $selected=[Collections.Generic.List[string]]::new(); $executed=[Collections.Generic.List[string]]::new(); $failed=[Collections.Generic.List[string]]::new()
function Run([string]$Name,[scriptblock]$Body) {
    $script:registered.Add($Name)
    if ($Filter -and $Name -notlike "*$Filter*") { return }
    $script:selected.Add($Name); $script:executed.Add($Name)
    try { & $Body; [Console]::WriteLine("PASS $Name") }
    catch { $script:failed.Add("$Name : $($_.Exception.Message)"); [Console]::Error.WriteLine("FAIL $Name : $($_.Exception.Message)") }
}
function Reject([scriptblock]$Body,[string]$Message) {
    $blocked=$false
    try { & $Body } catch { $blocked=$true }
    if (-not $blocked) { throw $Message }
}
function RejectDtoType([scriptblock]$Body,[string]$Message) {
    try { & $Body } catch {
        if ($_.Exception.Message.Contains('JSON型')) { return }
        throw "$Message : 型検査より前に別理由で拒否されました: $($_.Exception.Message)"
    }
    throw "$Message : 受理されました"
}
function Assert([bool]$Ok,[string]$Message) { if (-not $Ok) { throw $Message } }
$spec=[pscustomobject]@{testPolicy='unity-pilot-gates-v1';recordPolicy='external-current-v1'}
$path=@('tools/Harness/Adapters/UnityChecks.psm1')
Run 'discovery requires fixed offline and limited Unity' {
    $names=@(Get-UnityRequiredSteps $spec $path 'discovery')
    Assert ($names.Count -eq 7 -and $names -ccontains 'unity-editmode-limited' -and $names -cnotcontains 'unity-editmode-full') 'discovery集合不正'
}
Run 'judgment requires fixed offline and full Unity' {
    $names=@(Get-UnityRequiredSteps $spec $path 'judgment')
    Assert ($names.Count -eq 8 -and $names -ccontains 'unity-editmode-full' -and $names -ccontains 'artifacts-local' -and $names -cnotcontains 'unity-editmode-limited') 'judgment集合不正'
}
Run 'scope and empty change fail closed' {
    Reject { Get-UnityRequiredSteps $spec @() 'discovery' } 'empty accepted'
    Reject { Get-UnityRequiredSteps $spec @('unity/Assets/Other.cs') 'discovery' } 'outside scope accepted'
}
Run 'fixed eleven names are unique' {
    $names=@(Get-UnityLimitedCases)
    Assert ($names.Count -eq 11 -and @($names | Sort-Object -Unique).Count -eq 11) '11件の重複または不足'
}
function New-Cases {
    $names=@(Get-UnityLimitedCases)
    return [pscustomobject]@{Cases=@($names | ForEach-Object {[pscustomobject]@{fullname=$_;result='Passed'}}); Selected=@($names | ForEach-Object {[pscustomobject]@{fullname=$_;assemblyName='OneStarMaker.Tests.TestObservation.Editor'}}); Counts=[pscustomobject]@{total=11;executed=11;skipped=0}}
}
Run 'limited exact eleven passes' {
    $f=New-Cases
    Assert-UnityCaseSet 'limited-v1' $f.Cases $f.Counts ([pscustomobject]@{selected=$f.Selected})
}
Run 'limited missing extra and duplicate fail' {
    $f=New-Cases; $f.Selected[0].fullname=$f.Selected[1].fullname
    Reject { Assert-UnityCaseSet 'limited-v1' $f.Cases $f.Counts ([pscustomobject]@{selected=$f.Selected}) } 'duplicate accepted'
    $f=New-Cases; $f.Selected[0].fullname='Other.Extra'
    Reject { Assert-UnityCaseSet 'limited-v1' $f.Cases $f.Counts ([pscustomobject]@{selected=$f.Selected}) } 'extra accepted'
}
Run 'full includes eleven but allows parameterized duplicate elsewhere' {
    $f=New-Cases
    $f.Cases+=@([pscustomobject]@{fullname='Parameterized.Case';result='Passed'},[pscustomobject]@{fullname='Parameterized.Case';result='Passed'})
    $f.Selected+=@([pscustomobject]@{fullname='Parameterized.Case';assemblyName='Other.Tests'},[pscustomobject]@{fullname='Parameterized.Case';assemblyName='Other.Tests'})
    $f.Counts.total=13; $f.Counts.executed=13
    Assert-UnityCaseSet 'full-v1' $f.Cases $f.Counts ([pscustomobject]@{selected=$f.Selected})
}
Run 'skip and inconclusive fail independently' {
    $f=New-Cases; $f.Counts.skipped=1
    Reject { Assert-UnityCaseSet 'limited-v1' $f.Cases $f.Counts ([pscustomobject]@{selected=$f.Selected}) } 'skip count accepted'
    $f=New-Cases; $f.Cases[0].result='Inconclusive'
    Reject { Assert-UnityCaseSet 'limited-v1' $f.Cases $f.Counts ([pscustomobject]@{selected=$f.Selected}) } 'inconclusive accepted'
}
Run 'marker allows ordinary output and path spaces' {
    $module=Get-Module UnityChecks
    $root=Join-Path ([IO.Path]::GetTempPath()) ('h2c marker '+[Guid]::NewGuid().ToString())
    $guid=[Guid]::NewGuid().ToString(); $dir=Join-Path $root $guid
    try {
        [IO.Directory]::CreateDirectory($dir)|Out-Null
        $step=Join-Path $dir 'step.json'; [IO.File]::WriteAllText($step,'{}')
        $result=& $module {param($out,$err,$root) Get-UnityMarker $out $err $root} "ordinary`nUNITY_TEST_RESULT $step`nother" '' $root
        Assert ($result -ceq $step) 'marker path spaces lost'
        Reject { & $module {param($out,$err,$root) Get-UnityMarker $out $err $root} "UNITY_TEST_RESULT $step`nUNITY_TEST_RESULT $step" '' $root } 'duplicate marker accepted'
        Reject { & $module {param($out,$err,$root) Get-UnityMarker $out $err $root} "UNITY_TEST_RESULT $step" "UNITY_TEST_RESULT $step" $root } 'stderr marker accepted'
    } finally { if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)} }
}
Run 'child drains large stdout and stderr before exit' {
    $module=Get-Module UnityChecks
    $root=Join-Path ([IO.Path]::GetTempPath()) ('h2c-child-'+[Guid]::NewGuid().ToString())
    try {
        [IO.Directory]::CreateDirectory($root)|Out-Null
        $script=Join-Path $root 'flood.ps1'
        [IO.File]::WriteAllText($script, '[Console]::Out.Write(("o" * 200000)); [Console]::Error.Write(("e" * 200000))')
        $result=& $module {param($repo,$argv) Invoke-UnityChild $repo $argv 'addressables'} $root @('-NoProfile','-File',$script)
        Assert ($result.ExitCode -eq 0 -and $result.Stdout.Length -eq 200000 -and $result.Stderr.Length -eq 200000 -and $result.OutputComplete) 'child pipe incomplete'
    } finally { if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)} }
}
function New-PayloadFixture([string]$Root) {
    $repo=Join-Path $Root 'repo'; $task=Join-Path $Root 'task'; $runId='fixture-run'; $guid=[Guid]::NewGuid().ToString()
    $directory=Join-Path $task "payload/$runId/unity/$guid"
    [IO.Directory]::CreateDirectory($directory)|Out-Null
    $names=@(Get-UnityLimitedCases); $selected=@(); $results=@(); $events=@(); $xmlCases=@(); $caseRaw=@()
    for($i=0;$i -lt $names.Count;$i++){
        $id=[string]($i+1); $name=$names[$i].Split('.')[-1]
        $selected+= [ordered]@{id=$id;name=$name;fullname=$names[$i];uniqueName="Tests/$name";assemblyName='OneStarMaker.Tests.TestObservation.Editor';runState='Runnable'}
        $results+= [ordered]@{id=$id;name=$name;fullname=$names[$i];result='Passed';label=''}
        $events+= [ordered]@{sequence=($i*2+1);domainOrdinal=0;kind='started';id=$id;result='';label='';utc='2026-10-01T00:00:00Z';ticks=(1001+$i*2)}
        $events+= [ordered]@{sequence=($i*2+2);domainOrdinal=0;kind='finished';id=$id;result='Passed';label='';utc='2026-10-01T00:00:00Z';ticks=(1002+$i*2)}
        $xmlCases+="<test-case id='$id' name='$name' fullname='$($names[$i])' result='Passed'/>"
        $caseRaw+= [ordered]@{id=$id;name=$name;fullname=$names[$i];result='Passed';label='';reason=''}
    }
    $xml="<test-run result='Passed' total='11' passed='11' failed='0' skipped='0' inconclusive='0' start-time='start' end-time='end' duration='1'><test-suite>$(($xmlCases -join ''))</test-suite></test-run>"
    $clock=[ordered]@{status='observed';frequency=1000;runStartedTicks=1000;runFinishedTicks=2000;runStartedUtc='2026-10-01T00:00:00Z';runFinishedUtc='2026-10-01T00:00:01Z';durationMs=1000.0}
    $assemblies=@()
    foreach($point in @(@(1000,'2026-10-01T00:00:00Z'),@(2000,'2026-10-01T00:00:01Z'))){
        foreach($assemblyName in @('OneStarMaker.Editor.TestObservation','UnityEditor.TestRunner','UnityEngine.TestRunner','OneStarMaker.Tests.TestObservation.Editor')){
            $assemblies+= [ordered]@{domainOrdinal=0;observedAtUtc=$point[1];ticks=$point[0];fullName="$assemblyName, Version=1.0.0.0";location="C:/$assemblyName.dll";loadedModuleVersionId='mvid';diskSha256=('A'*64);status='observed';failure=''}
        }
    }
    $state=[ordered]@{schemaVersion=1;recordKind='unity-test-observation';invocationId=$guid;projectPath=(Join-Path $repo 'unity');processId=1234;platform='EditMode';status='complete';failure=@();sealed=$true;sealSequence=24;sequence=24;clock=$clock;domains=@([ordered]@{ordinal=0;id='domain';bootstrapUtc='2026-10-01T00:00:00Z';bootstrapTicks=900;reloadBeforeUtc='';reloadBeforeTicks=0});selected=$selected;events=$events;results=$results;assemblies=$assemblies}
    $xmlPath=Join-Path $directory 'results.xml'; $logPath=Join-Path $directory 'unity.log'; $progressPath=Join-Path $directory 'observation-progress.json'; $obsPath=Join-Path $directory 'observation.json'; $stepPath=Join-Path $directory 'step.json'
    [IO.File]::WriteAllText($xmlPath,$xml); [IO.File]::WriteAllText($logPath,'log')
    $stateJson=ConvertTo-Json -InputObject $state -Depth 64; [IO.File]::WriteAllText($progressPath,$stateJson); [IO.File]::WriteAllText($obsPath,$stateJson)
    $module=Get-Module UnityChecks
    $observed=& $module {param($xml,$obs,$log,$guid,$repo,$pid,$path) Get-UnityObservationResult -XmlText $xml -ObservationText $obs -ProgressText $obs -LogText $log -ExpectedInvocation $guid -ExpectedProject (Join-Path $repo 'unity') -ExpectedPid $pid -ExpectedPath $path} $xml $stateJson 'log' $guid $repo 1234 $obsPath
    Assert ($observed.status -ceq 'complete') "fixture observation invalid: $($observed.failure -join ';')"
    $rawNames=@('results.xml','unity.log','observation-progress.json','observation.json')
    $hashes=@($rawNames | ForEach-Object {(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $directory $_)).Hash})
    $rawLogHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($hashes -join '|')))).ToLowerInvariant()
    $argv=@('-nographics','-batchmode','-projectPath',(Join-Path $repo 'unity'),'-runTests','-testPlatform','EditMode','-testResults',$xmlPath,'-logFile',$logPath,'-testFilter','OneStarMaker.Tests.Editor.TestObservation.ObservationStateTests','-osmTestInvocation',$guid,'-osmTestObservation',(Get-UnityObservationArgumentPath $obsPath))
    $raw=[ordered]@{schemaVersion=2;recordKind='unity-test-step';invocationId=$guid;name='unity-tests';kind='test';status='passed';exitCode=0;failure=@();argv=$argv;cwd=$repo;projectPath=(Join-Path $repo 'unity');platform='EditMode';filter='OneStarMaker.Tests.Editor.TestObservation.ObservationStateTests';withGraphics=$false;unity=[ordered]@{executablePath=[IO.Path]::GetFullPath('D:\UnityEditor/6000.6.0f1/Editor/Unity.exe');requestedVersion='6000.6.0f1';executableVersion='6000.6.0f1'};process=[ordered]@{startedAt='2026-10-01T00:00:00Z';endedAt='2026-10-01T00:00:01Z';exitCode=0;id=1234};durationMs=1000;timing=[ordered]@{processDurationMs=1000;xmlDurationSeconds=1.0;xmlStartedAt='start';xmlEndedAt='end';unityMarkers=[ordered]@{status='observed';clock=$clock;path=$obsPath};loadedAssemblies=[ordered]@{status='observed';assemblies=$assemblies;path=$obsPath}};counts=[ordered]@{total=11;passed=11;failed=0;skipped=0;inconclusive=0;executed=11};cases=$caseRaw;compileErrors=@();logs=$rawNames;logHash=$rawLogHash;observation=[ordered]@{required=$true;path=$obsPath;sha256=((Get-FileHash $obsPath -Algorithm SHA256).Hash.ToLowerInvariant());progressPath=$progressPath;progressSha256=((Get-FileHash $progressPath -Algorithm SHA256).Hash.ToLowerInvariant());status='complete';failure=@()}}
    [IO.File]::WriteAllText($stepPath,(ConvertTo-Json -InputObject $raw -Depth 64))
    $out=Join-Path $task "payload/$runId/runner.stdout.log"; $err=Join-Path $task "payload/$runId/runner.stderr.log"
    [IO.File]::WriteAllText($out,"normal`nUNITY_TEST_RESULT $stepPath`n"); [IO.File]::WriteAllText($err,'')
    $roles=@('runner-stdout','runner-stderr','runner-step','xml','unity-log','progress','observation')
    $paths=@($out,$err,$stepPath,$xmlPath,$logPath,$progressPath,$obsPath)
    $payloads=@(); $logs=@(); $outerHashes=@()
    for($i=0;$i -lt $paths.Count;$i++){$relative=[IO.Path]::GetRelativePath($task,$paths[$i]);$payloads+= [ordered]@{role=$roles[$i];path=$relative;sha256=((Get-FileHash $paths[$i] -Algorithm SHA256).Hash.ToLowerInvariant())};$logs+=$relative;$outerHashes+=(Get-FileHash $paths[$i] -Algorithm SHA256).Hash}
    $outerHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($outerHashes -join '|')))).ToLowerInvariant()
    $childArgs=@('-NoProfile','-File',(Join-Path $repo 'tools/run-tests.ps1'),'-ObserveUnity','-OutputRoot',(Join-Path $task "payload/$runId/unity"),'-Filter','OneStarMaker.Tests.Editor.TestObservation.ObservationStateTests')
    $outer=[pscustomobject]@{name='unity-editmode-limited';kind='test';adapterKind='unity-test-v2';status='passed';exitCode=0;failure=@();argv=@([ordered]@{executable='pwsh';arguments=$childArgs});cwd=$repo;durationMs=1100;timing=[ordered]@{runnerWallMs=1000;collectionMs=100};logs=$logs;logHash=$outerHash;profile='limited-v1';runner=[ordered]@{exitCode=0;stdoutPath=$payloads[0].path;stdoutSha256=$payloads[0].sha256;stderrPath=$payloads[1].path;stderrSha256=$payloads[1].sha256;environment=[ordered]@{SAMPLEGAME_CONTENT__RUNTIMEMODE='addressables'}};result=[ordered]@{stepPath=$payloads[2].path;stepSha256=$payloads[2].sha256;invocationId=$guid};payloads=$payloads;observedAssemblies=[ordered]@{path=$payloads[6].path;sha256=$payloads[6].sha256}}
    return [pscustomobject]@{Repo=$repo;Task=$task;RunId=$runId;Outer=$outer;Spec=[pscustomobject]@{profile=[pscustomobject]@{limitedFilter='OneStarMaker.Tests.Editor.TestObservation.ObservationStateTests'}};Paths=$paths}
}
function Sync-PayloadFixture($Fixture) {
    $outer=$Fixture.Outer
    for($i=0;$i -lt $Fixture.Paths.Count;$i++){$outer.payloads[$i].sha256=(Get-FileHash $Fixture.Paths[$i] -Algorithm SHA256).Hash.ToLowerInvariant()}
    $hashes=@($Fixture.Paths | ForEach-Object {(Get-FileHash $_ -Algorithm SHA256).Hash})
    $outer.logHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($hashes -join '|')))).ToLowerInvariant()
    $outer.runner.stdoutSha256=$outer.payloads[0].sha256; $outer.runner.stderrSha256=$outer.payloads[1].sha256
    $outer.result.stepSha256=$outer.payloads[2].sha256; $outer.observedAssemblies.sha256=$outer.payloads[6].sha256
}
Run 'saved successful payload rechecks and tampering fails' {
    $root=Join-Path ([IO.Path]::GetTempPath()) ('h2c-payload-'+[Guid]::NewGuid().ToString())
    try {
        $f=New-PayloadFixture $root
        Assert (Assert-UnityPayload $f.Repo $f.Task $f.RunId $f.Outer $f.Spec) 'valid payload rejected'
        [IO.File]::AppendAllText($f.Paths[4],'tamper')
        Reject { Assert-UnityPayload $f.Repo $f.Task $f.RunId $f.Outer $f.Spec } 'tampered log accepted'
    } finally { if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)} }
}
Run 'long observer argv retains normal payload paths' {
    if (-not [OperatingSystem]::IsWindows()) { return }
    $root=Join-Path ([IO.Path]::GetTempPath()) (('h2c-long-'+[Guid]::NewGuid().ToString()) + '\' + ('x' * 70))
    try {
        $f=New-PayloadFixture $root
        $raw=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($f.Paths[2])) -AsHashtable -DateKind String -Depth 64
        $index=[array]::IndexOf([array]$raw.argv,'-osmTestObservation')+1
        Assert ($raw.argv[$index].StartsWith('\\?\',[StringComparison]::Ordinal)) 'long observer argv was not extended'
        Assert (-not $raw.observation.path.StartsWith('\\?\',[StringComparison]::Ordinal)) 'raw payload path was rewritten'
        Assert (Assert-UnityPayload $f.Repo $f.Task $f.RunId $f.Outer $f.Spec) 'long observer argv failed recheck'
    } finally { if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)} }
}
Run 'raw schema, identity and skip tampering fail after transport rehash' {
    $root=Join-Path ([IO.Path]::GetTempPath()) ('h2c-matrix-'+[Guid]::NewGuid().ToString())
    try {
        $f=New-PayloadFixture $root
        $original=[IO.File]::ReadAllText($f.Paths[2])
        foreach($mutation in @('schema','pid','project','skip','unknown','filter')){
            $raw=ConvertFrom-Json -InputObject $original -AsHashtable -Depth 64
            switch($mutation){
                'schema' {$raw.schemaVersion=1}
                'pid' {$raw.process.id=9876}
                'project' {$raw.projectPath='C:/foreign/unity'}
                'skip' {$raw.counts.skipped=1}
                'unknown' {$raw.extra='unapproved'}
                'filter' {$raw.filter='Other.Filter'}
            }
            [IO.File]::WriteAllText($f.Paths[2],(ConvertTo-Json -InputObject $raw -Depth 64))
            Sync-PayloadFixture $f
            Reject { Assert-UnityPayload $f.Repo $f.Task $f.RunId $f.Outer $f.Spec } "$mutation accepted"
        }
    } finally { if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)} }
}
Run 'marker and missing observation fail after transport rehash' {
    $root=Join-Path ([IO.Path]::GetTempPath()) ('h2c-matrix-'+[Guid]::NewGuid().ToString())
    try {
        $f=New-PayloadFixture $root
        [IO.File]::WriteAllText($f.Paths[0],'ordinary output only')
        Sync-PayloadFixture $f
        Reject { Assert-UnityPayload $f.Repo $f.Task $f.RunId $f.Outer $f.Spec } 'missing marker accepted'
        [IO.File]::WriteAllText($f.Paths[0],"UNITY_TEST_RESULT $($f.Paths[2])")
        [IO.File]::Delete($f.Paths[6])
        Reject { Assert-UnityPayload $f.Repo $f.Task $f.RunId $f.Outer $f.Spec } 'missing observation accepted'
    } finally { if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)} }
}
Run 'outer profile and raw role duplication fail' {
    $root=Join-Path ([IO.Path]::GetTempPath()) ('h2c-matrix-'+[Guid]::NewGuid().ToString())
    try {
        $f=New-PayloadFixture $root
        $f.Outer.profile='full-v1'
        Reject { Assert-UnityPayload $f.Repo $f.Task $f.RunId $f.Outer $f.Spec } 'wrong profile accepted'
        $f.Outer.profile='limited-v1'; $f.Outer.payloads[1].role='runner-stdout'
        Reject { Assert-UnityPayload $f.Repo $f.Task $f.RunId $f.Outer $f.Spec } 'duplicate role accepted'
    } finally { if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)} }
}
Run 'outer argv unknown key and observation summary contradiction fail' {
    $root=Join-Path ([IO.Path]::GetTempPath()) ('h2c-matrix-'+[Guid]::NewGuid().ToString())
    try {
        $f=New-PayloadFixture $root
        $f.Outer.argv[0].unexpected='unapproved'
        Reject { Assert-UnityPayload $f.Repo $f.Task $f.RunId $f.Outer $f.Spec } 'unknown argv key accepted'
        $f.Outer.argv[0].Remove('unexpected')
        $raw=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($f.Paths[2])) -AsHashtable -DateKind String -Depth 64
        $raw.observation.failure=@('inconsistent summary')
        [IO.File]::WriteAllText($f.Paths[2],(ConvertTo-Json -InputObject $raw -Depth 64)); Sync-PayloadFixture $f
        Reject { Assert-UnityPayload $f.Repo $f.Task $f.RunId $f.Outer $f.Spec } 'contradictory observation failure accepted'
    } finally { if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)} }
}
Run 'raw observer argv path cannot claim a different spelling' {
    $root=Join-Path ([IO.Path]::GetTempPath()) ('h2c-matrix-'+[Guid]::NewGuid().ToString())
    try {
        $f=New-PayloadFixture $root
        $raw=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($f.Paths[2])) -AsHashtable -DateKind String -Depth 64
        $index=[array]::IndexOf([array]$raw.argv,'-osmTestObservation')+1
        $raw.argv[$index]='\\?\' + $raw.argv[$index]
        [IO.File]::WriteAllText($f.Paths[2],(ConvertTo-Json -InputObject $raw -Depth 64)); Sync-PayloadFixture $f
        Reject { Assert-UnityPayload $f.Repo $f.Task $f.RunId $f.Outer $f.Spec } 'unapproved observer argv path accepted'
    } finally { if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)} }
}
Run 'observation scalar types reject true schema and numeric seal after rehash' {
    $root=Join-Path ([IO.Path]::GetTempPath()) ('h2c-matrix-'+[Guid]::NewGuid().ToString())
    try {
        $f=New-PayloadFixture $root
        $original=[IO.File]::ReadAllText($f.Paths[6])
        foreach($mutation in @('schemaVersion','sealed')){
            $state=ConvertFrom-Json -InputObject $original -AsHashtable -DateKind String -Depth 64
            if($mutation -ceq 'schemaVersion'){$state.schemaVersion=$true}else{$state.sealed=1}
            $text=ConvertTo-Json -InputObject $state -Depth 64
            [IO.File]::WriteAllText($f.Paths[5],$text);[IO.File]::WriteAllText($f.Paths[6],$text)
            $raw=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($f.Paths[2])) -AsHashtable -DateKind String -Depth 64
            $raw.observation.progressSha256=(Get-FileHash $f.Paths[5] -Algorithm SHA256).Hash.ToLowerInvariant()
            $raw.observation.sha256=(Get-FileHash $f.Paths[6] -Algorithm SHA256).Hash.ToLowerInvariant()
            $hashes=@($raw.logs | ForEach-Object {(Get-FileHash (Join-Path (Split-Path $f.Paths[2]) $_) -Algorithm SHA256).Hash})
            $raw.logHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($hashes -join '|')))).ToLowerInvariant()
            [IO.File]::WriteAllText($f.Paths[2],(ConvertTo-Json -InputObject $raw -Depth 64));Sync-PayloadFixture $f
            Reject { Assert-UnityPayload $f.Repo $f.Task $f.RunId $f.Outer $f.Spec } "coerced $mutation accepted"
        }
    } finally { if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)} }
}
Run 'closed outer and raw success DTO reject coerced scalars and containers' {
    $root=Join-Path ([IO.Path]::GetTempPath()) ('h2c-matrix-'+[Guid]::NewGuid().ToString())
    try {
        $f=New-PayloadFixture $root
        $originalRaw=[IO.File]::ReadAllText($f.Paths[2])
        $originalOuter=ConvertTo-Json -InputObject $f.Outer -Depth 64
        $outerMutations=[ordered]@{
            name={ $outer.name=@('unity-editmode-limited') }; status={ $outer.status=@('passed') }
            exitCode={ $outer.exitCode=$true }; durationMs={ $outer.durationMs=1.0 }
            failure={ $outer.failure='reason' }; logs={ $outer.logs='path' }
            argvExecutable={ $outer.argv[0].executable=@('pwsh') }
            runnerExit={ $outer.runner.exitCode=$true }; runnerPath={ $outer.runner.stdoutPath=@($outer.runner.stdoutPath) }
            environment={ $outer.runner.environment.SAMPLEGAME_CONTENT__RUNTIMEMODE=@('addressables') }
            timing={ $outer.timing.runnerWallMs=$true }; result={ $outer.result.invocationId=@($outer.result.invocationId) }
            payloads={ $outer.payloads='payload' }; observation={ $outer.observedAssemblies.path=@($outer.observedAssemblies.path) }
        }
        foreach($entry in $outerMutations.GetEnumerator()) {
            $outer=ConvertFrom-Json -InputObject $originalOuter -AsHashtable -DateKind String -Depth 64
            & $entry.Value
            $f.Outer=$outer
            RejectDtoType { Assert-UnityPayload $f.Repo $f.Task $f.RunId $f.Outer $f.Spec } "outer $($entry.Key)"
        }
        $f.Outer=ConvertFrom-Json -InputObject $originalOuter -AsHashtable -DateKind String -Depth 64
        $rawMutations=[ordered]@{
            schemaVersion={ $raw.schemaVersion=$true }; recordKind={ $raw.recordKind=@('unity-test-step') }
            status={ $raw.status=@('passed') }; processStartedAt={ $raw.process.startedAt=$true }
            processExit={ $raw.process.exitCode=$true }; durationMs={ $raw.durationMs=1.0 }
            argv={ $raw.argv='-nographics' }; failure={ $raw.failure='reason' }
            unity={ $raw.unity.requestedVersion=@('6000.6.0f1') }
            xmlDuration={ $raw.timing.xmlDurationSeconds=$true }; clock={ $raw.timing.unityMarkers.clock.frequency=$true }
            assemblies={ $raw.timing.loadedAssemblies.assemblies[0].ticks=$true }
            counts={ $raw.counts.total=$true }; cases={ $raw.cases[0].fullname=@($raw.cases[0].fullname) }
            compileErrors={ $raw.compileErrors='error' }; logs={ $raw.logs='results.xml' }
            observationRequired={ $raw.observation.required=1 }; observationPath={ $raw.observation.path=@($raw.observation.path) }
            observationFailure={ $raw.observation.failure='reason' }
        }
        foreach($entry in $rawMutations.GetEnumerator()) {
            $raw=ConvertFrom-Json -InputObject $originalRaw -AsHashtable -DateKind String -Depth 64
            & $entry.Value
            [IO.File]::WriteAllText($f.Paths[2],(ConvertTo-Json -InputObject $raw -Depth 64)); Sync-PayloadFixture $f
            RejectDtoType { Assert-UnityPayload $f.Repo $f.Task $f.RunId $f.Outer $f.Spec } "raw $($entry.Key)"
        }
    } finally { if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)} }
}
Run 'adapter collects one fake child invocation and valid raw' {
    $root=Join-Path ([IO.Path]::GetTempPath()) ('h2c-adapter-'+[Guid]::NewGuid().ToString())
    $module=Get-Module UnityChecks
    try {
        $global:h2cFixtureFactory={param($location) New-PayloadFixture $location}.GetNewClosure()
        $global:h2cFixtureRoot=$root
        & $module {
            Set-Item -Path Function:script:Invoke-UnityChild -Value {
                param([string]$Repo,[string[]]$Arguments,[string]$EnvironmentValue)
                $fixture=& $global:h2cFixtureFactory $global:h2cFixtureRoot
                $global:h2cFixtureStep=$fixture.Paths[2]
                return [pscustomobject]@{ExitCode=0;Stdout="normal`nUNITY_TEST_RESULT $($fixture.Paths[2])`n";Stderr='';WallMs=1000;OutputComplete=$true}
            }
        }
        $repo=Join-Path $root 'repo'; $task=Join-Path $root 'task'; $testSpec=[pscustomobject]@{profile=[pscustomobject]@{limitedFilter='OneStarMaker.Tests.Editor.TestObservation.ObservationStateTests'}}
        $step=Invoke-UnityStep $repo $task 'fixture-run' 'unity-editmode-limited' $testSpec
        Assert ($step.status -ceq 'passed' -and $step.exitCode -eq 0 -and @($step.payloads).Count -eq 7) "adapter failed: $($step.failure -join ';')"
    } finally {
        Remove-Variable h2cFixtureFactory,h2cFixtureRoot,h2cFixtureStep -Scope Global -ErrorAction SilentlyContinue
        if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)}
    }
}
Run 'approved Unity revisions bind exact task and specification' {
    Import-Module (Join-Path $PSScriptRoot '../GatePolicy.psm1') -Force
    $approvalModule=(Get-Module GatePolicy).NestedModules | Where-Object Name -EQ 'ApprovedSpecifications'
    $originalLookup=& $approvalModule { (Get-Command Get-ApprovedSpecification).ScriptBlock }
    $fixtureText='portable Unity approval fixture'
    $fixtureHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($fixtureText))).ToLowerInvariant()
    $oldEntry=& $approvalModule { Get-ApprovedSpecification 'h2c-unity-gate' }
    $newEntry=& $approvalModule { Get-ApprovedSpecification 'h2c-unity-gate-r2' }
    Assert ($oldEntry.textSha256 -ceq '19d008f64ecae1e17ed5c3e071ca681480cde24ce49089f6a6b198892b743946' -and $oldEntry.base -ceq '2c29c99806788406551affba6cc795e67e614748') 'old approval values changed'
    Assert ($newEntry.textSha256 -ceq '6fa8801ca35cd99a16679313e8737e64f03a2b7e9f35b784f90dc2a3f0f6914f' -and $newEntry.base -ceq '9828feb56e91e8907f350392bc0cde95b7921779' -and $newEntry.title -ceq 'H2C-UNITY-GATE-R2' -and $newEntry.minimum -ceq '凍結仕様h2c-a3-v2のM1〜M5を満たす') 'r2 approval values changed'
    Assert (($oldEntry.profile | ConvertTo-Json -Depth 20 -Compress) -ceq ($newEntry.profile | ConvertTo-Json -Depth 20 -Compress) -and ($oldEntry.scope | ConvertTo-Json -Depth 20 -Compress) -ceq ($newEntry.scope | ConvertTo-Json -Depth 20 -Compress)) 'Unity approval scope/profile diverged'
    $required=@(Get-RequiredSteps $spec $path 'discovery')
    $steps=@($required | ForEach-Object {
        if($_ -like '*local'){[pscustomobject]@{name=$_;status='passed';registered=@('case');selected=@('case');executed=@('case')}}
        else{[pscustomobject]@{name=$_;status='passed'}}
    })
    try {
      # 実entryの固定値を先に検査し、ignored A3を要しない本文だけをmodule内で差し替える。
      $global:unityApprovalFixtureHash=$fixtureHash
      & $approvalModule {
          Set-Item Function:script:Get-ApprovedSpecification -Value {
              param([string]$Task)
              $entry=switch -CaseSensitive ($Task) {
                  'h2c-unity-gate' { $script:oldFixtureEntry }
                  'h2c-unity-gate-r2' { $script:newFixtureEntry }
                  default { throw "承認されていないtaskです: $Task" }
              }
              $copy=ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $entry -Depth 30) -AsHashtable -Depth 30
              $copy.textSha256=$global:unityApprovalFixtureHash
              return $copy
          }
      }
      & $approvalModule {param($old,$new) $script:oldFixtureEntry=$old; $script:newFixtureEntry=$new} $oldEntry $newEntry
      $specs=@{}
      foreach($task in @('h2c-unity-gate','h2c-unity-gate-r2')) {
        $candidate=[pscustomobject](& $approvalModule {param($task,$text) New-ApprovedSpecification $task $text} $task $fixtureText)
        $candidate | Add-Member -NotePropertyName specHash -NotePropertyValue "hash-$task"
        $candidate | Add-Member -NotePropertyName id -NotePropertyValue "spec-$task"
        & $approvalModule {param($task,$candidate) Assert-ApprovedSpecification $task $candidate} $task $candidate
        $specs[$task]=$candidate
      }
      foreach($task in @('h2c-unity-gate','h2c-unity-gate-r2')) {
        $candidate=$specs[$task]
        $run=[pscustomobject]@{taskId=$task;suiteVersion=$candidate.testPolicy;specId=$candidate.id;specHash=$candidate.specHash;base=$candidate.base;head='head';stage='discovery';status='passed';dirtyBefore=@();dirtyAfter=@();steps=$steps}
        Assert-RunForGate $run $candidate 'head' $required
        $run.dirtyBefore=@(' M file'); Reject { Assert-RunForGate $run $candidate 'head' $required } 'dirty accepted'; $run.dirtyBefore=@()
        Reject { Assert-RunForGate $run $candidate 'old-head' $required } 'old head accepted'
        $other=if($task -ceq 'h2c-unity-gate'){'h2c-unity-gate-r2'}else{'h2c-unity-gate'}
        Reject { & $approvalModule {param($task,$candidate) Assert-ApprovedSpecification $task $candidate} $other $candidate } 'cross-task specification accepted'
        $run.taskId=$other; Reject { Assert-RunForGate $run $candidate 'head' $required } 'cross-task run accepted'; $run.taskId=$task
        $run.taskId='unknown-task'; Reject { Assert-RunForGate $run $candidate 'head' $required } 'unknown task accepted'; $run.taskId=$task
        foreach($field in @('base','text','testPolicy','recordPolicy','profile','scope')) {
            $altered=$candidate | ConvertTo-Json -Depth 30 | ConvertFrom-Json -Depth 30
            switch($field) {
                'base' {$altered.base='other-base'}
                'text' {$altered.text+=' changed'}
                'testPolicy' {$altered.testPolicy='local-gates-v1'}
                'recordPolicy' {$altered.recordPolicy='other-policy'}
                'profile' {$altered.profile.limitedFilter='Other.Filter'}
                'scope' {$altered.scope=@('tools/Harness/**')}
            }
            Reject { & $approvalModule {param($task,$candidate) Assert-ApprovedSpecification $task $candidate} $task $altered } "$field mutation accepted"
        }
        foreach($field in @('base','text','profile','scope')) {
            $altered=$candidate | ConvertTo-Json -Depth 30 | ConvertFrom-Json -AsHashtable -Depth 30
            $altered.Remove($field)
            Reject { & $approvalModule {param($task,$candidate) Assert-ApprovedSpecification $task $candidate} $task $altered } "$field missing accepted"
        }
        $run.base='other-base'; Reject { Assert-RunForGate $run $candidate 'head' $required } 'run base mutation accepted'
        $run.base=$candidate.base; $run.specHash='other-hash'; Reject { Assert-RunForGate $run $candidate 'head' $required } 'run hash mutation accepted'
        $run.specHash=$candidate.specHash; $run.steps=@($steps | Select-Object -Skip 1); Reject { Assert-RunForGate $run $candidate 'head' $required } 'missing step accepted'
        $run.steps=$steps; $run.stage='judgment'; Reject { Assert-RunForGate $run $candidate 'head' $required } 'full-to-discovery accepted'
      }
    } finally {
        & $approvalModule {param($original) Set-Item Function:script:Get-ApprovedSpecification -Value $original; Remove-Variable oldFixtureEntry,newFixtureEntry -Scope Script -ErrorAction SilentlyContinue} $originalLookup
        Remove-Variable unityApprovalFixtureHash -Scope Global -ErrorAction SilentlyContinue
    }
}
[Console]::WriteLine("Unity gate offline: $($executed.Count) executed, $($failed.Count) failed")
if ($ResultPath) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($ResultPath),(ConvertTo-Json -InputObject ([ordered]@{registered=@($registered);selected=@($selected);executed=@($executed);failed=@($failed)}) -Depth 6)) }
if($failed.Count -gt 0){exit 1}
