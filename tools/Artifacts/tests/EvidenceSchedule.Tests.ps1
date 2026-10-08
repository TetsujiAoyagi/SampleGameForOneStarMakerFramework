param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$module=Import-Module (Join-Path $PSScriptRoot '../EvidenceSchedule.psm1') -Force -PassThru
$contract=$module.NestedModules | Where-Object Name -eq 'EvidenceContract' | Select-Object -Last 1
$store=$module.NestedModules | Where-Object Name -eq 'TaskEventStore' | Select-Object -Last 1
$root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-schedule-'+[Guid]::NewGuid().ToString('N'))
$cases=@('scheduler-runtime-missing','runtime-hash-mismatch','runtime-config-mismatch','runtime-three-deps','scheduler-wrong-identity','scheduler-fixed-action','scheduler-duplicate','scheduler-fair-cursors','scheduler-sync-budget-cleanup-reserved','scheduler-next-logon-catchup','scheduler-reparse','scheduler-stage-global-budget','scheduler-stage-counter-budget','scheduler-cleanup-partial-status','scheduler-phase-deadlines','scheduler-age-clock-independent')
$selected=if($Case -ceq '*'){$cases}else{@($Case.Split(','))}
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
function Assert([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
function Reject([scriptblock]$Action){$rejected=$false;try{&$Action|Out-Null}catch{$rejected=$true};Assert $rejected 'expected rejection'}
function WriteJson([string]$Path,$Value){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))|Out-Null;[IO.File]::WriteAllText($Path,(ConvertTo-Json -Depth 30 -InputObject $Value),[Text.UTF8Encoding]::new($false));return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
$script:installed=$null;$script:installs=0;$script:wrong=$false;$script:sync=[Collections.Generic.List[string]]::new();$script:cleanup=[Collections.Generic.List[string]]::new();$script:time=[DateTimeOffset]::Parse('2026-01-01T00:00:00+00:00');$script:syncConsumesBudget=$false;$script:elapsed=0L
$scheduler={
    param($action,$identity)
    if($action -eq 'status'){
        if($script:wrong){return @{installed=$true;execute='foreign.exe';arguments='foreign';userId='foreign';logonType='Password';runLevel='Highest'}}
        if(-not $script:installed){return @{installed=$false}}
        return @{installed=$true;execute=$script:installed.execute;arguments=$script:installed.arguments;userId=$script:installed.ownerSid;logonType='InteractiveToken';runLevel='Limited';lastRun=$null;nextRun='03:00';lastResult=0}
    }
    $script:installs++;$script:installed=$identity;return @{installed=$true}
}
$clockHook={ $script:time };$elapsedHook={ $script:elapsed }
$syncHook={param($config,$task)$script:sync.Add($task);if($script:syncConsumesBudget){$script:time=$script:time.AddSeconds(120);$script:elapsed+=120*[Diagnostics.Stopwatch]::Frequency};throw 'offline delivery unavailable'}
$cleanupHook={param($config,$task,$maximum)$script:cleanup.Add($task);return @{status='passed';deleted=0;skipped=1;blocked=0;remaining=0;items=@([pscustomobject]@{taskId=$task;artifactId=$null;reasonCode='staging-retained'})}}
function Fixture([string]$Case){
    $work=[IO.Path]::Combine($root,$Case);$source=[IO.Path]::Combine($work,'source');$deployment=[IO.Path]::Combine($work,'deployments');$runtime=[IO.Path]::Combine($work,'runtime')
    [IO.Directory]::CreateDirectory($source)|Out-Null
    $repo='a'*64;$id='b'*32
    $config=[IO.Path]::Combine($work,'config.json')
    $hash=WriteJson $config @{schemaVersion=2;profile='osm';endpoint='https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com';bucket='osm-artifacts';repositoryId=$repo;prefix='development/evidence/v2/';policy='task-end-30d-v1';deploymentId=$id}
    $null=WriteJson ([IO.Path]::Combine($deployment,$id+'.json')) @{schemaVersion=2;deploymentId=$id;repositoryId=$repo;configSha256=$hash;baselineRecordId='offline-baseline';baselineSha256='c'*64;ownerObservationId='offline-owner';ownerObservationSha256='d'*64}
    $paths=@('tools/Artifacts/EvidenceCleanupJob.ps1','tools/Workflow/TaskLifecycle.psm1','tools/Workflow/TaskEventStore.psm1','tools/workflow-task.ps1');$binaries=@()
    foreach($assembly in @('Probe/R2RouteTransport','Packaging/ArtifactPackaging','Transport/R2ArtifactTransport')){
        foreach($extension in @('.dll','.deps.json')){
            $path='tools/Artifacts/'+$assembly+$extension;$paths+=@($path)
            $full=[IO.Path]::Combine($source,$path);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($full))|Out-Null;[IO.File]::WriteAllText($full,'offline-binary-fixture')
            $binaries+=@(@{path=$path;sha256=(Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()})
        }
    }
    foreach($path in $paths){$full=[IO.Path]::Combine($source,$path);if(-not [IO.File]::Exists($full)){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($full))|Out-Null;[IO.File]::WriteAllText($full,'offline-script-fixture')}}
    $inputs=@{root=$source;head='e'*40;files=$paths;binaries=$binaries}
    & $contract {param($r)$script:DeploymentRoot=$r} $deployment
    & $store {param($r)$script:TestRoot=$r} ([IO.Path]::Combine($work,'workflow'))
    & $module {param($r,$i,$s,$y,$c,$k,$e)$script:ElapsedClock=$e;$script:TestRoot=$r;$script:SourceHook={$i}.GetNewClosure();$script:SchedulerHook=$s;$script:SyncHook=$y;$script:CleanupHook=$c;$script:Clock=$k} $runtime $inputs $scheduler $syncHook $cleanupHook $clockHook $elapsedHook
    $script:installed=$null;$script:installs=0;$script:wrong=$false;$script:sync.Clear();$script:cleanup.Clear();$script:time=[DateTimeOffset]::Parse('2026-01-01T00:00:00+00:00');$script:syncConsumesBudget=$false;$script:elapsed=0L
    return @{config=$config;work=$work;source=$source;repo=$repo;inputs=$inputs}
}
function AddTasks($Fixture,[int]$Count){
    for($i=0;$i -lt $Count;$i++){
        $id='task-'+$i.ToString('D3')
        $guard=& $store {param($r,$t)Enter-WorkflowTaskGuard $r $t} $Fixture.repo $id
        $guard.Dispose()
    }
}
try{
    foreach($case in $selected){
        try{
            Assert ($case -cin $cases) 'unknown case';$fixture=Fixture $case
            switch($case){
                'scheduler-runtime-missing'{[IO.File]::Delete([IO.Path]::Combine($fixture.source,$fixture.inputs.files[-1]));Reject {Invoke-EvidenceScheduleInstall $fixture.config};Assert ($script:installs -eq 0) 'registered incomplete runtime'}
                'runtime-hash-mismatch'{
                    $installation=Invoke-EvidenceScheduleInstall $fixture.config;$file=[IO.Path]::Combine($installation.runtime,'tools/workflow-task.ps1');[IO.File]::WriteAllText($file,'changed')
                    Reject {Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256};Assert ($script:cleanup.Count -eq 0) 'cleanup after hash mismatch'
                }
                'runtime-config-mismatch'{$installation=Invoke-EvidenceScheduleInstall $fixture.config;[IO.File]::WriteAllText([IO.Path]::Combine($installation.runtime,'config.json'),'{}');Reject {Get-EvidenceScheduleStatus $fixture.config}}
                'runtime-three-deps'{
                    $installation=Invoke-EvidenceScheduleInstall $fixture.config
                    $manifest=ConvertFrom-Json -AsHashtable -InputObject ([IO.File]::ReadAllText([IO.Path]::Combine($installation.runtime,'runtime.json')))
                    Assert (@($manifest.binaries | Where-Object {$_.path -like '*.dll'}).Count -eq 3 -and @($manifest.binaries | Where-Object {$_.path -like '*.deps.json'}).Count -eq 3) 'missing binary identity'
                }
                'scheduler-wrong-identity'{$script:wrong=$true;Reject {Invoke-EvidenceScheduleInstall $fixture.config};Assert ($script:installs -eq 0) 'overwrote foreign task'}
                'scheduler-fixed-action'{
                    $installation=Invoke-EvidenceScheduleInstall $fixture.config
                    Assert ($script:installed.arguments -match '-NoProfile -NonInteractive -WindowStyle Hidden -File ' -and $script:installed.arguments -match $installation.manifestSha256 -and [IO.Path]::IsPathFullyQualified($script:installed.execute) -and $script:installed.arguments -notmatch '(Secret|AccessKey|password)') 'unsafe scheduler action'
                }
                'scheduler-duplicate'{
                    AddTasks $fixture 1;$installation=Invoke-EvidenceScheduleInstall $fixture.config
                    $lockPath=[IO.Path]::Combine($installation.runtime,'job.lock');$handle=[IO.FileStream]::new($lockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
                    try{Assert ((Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256).status -eq 'busy') 'duplicate not busy'}finally{$handle.Dispose()}
                }
                'scheduler-fair-cursors'{
                    AddTasks $fixture 105;$installation=Invoke-EvidenceScheduleInstall $fixture.config
                    $null=Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256
                    Assert ($script:sync.Count -eq 100 -and $script:cleanup.Count -eq 100) 'independent bounded phases'
                    $script:sync.Clear();$script:cleanup.Clear();$null=Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256
                    Assert ($script:sync[0] -ceq 'task-100' -and $script:cleanup[0] -ceq 'task-100') 'failed first item starved remainder'
                }
                'scheduler-sync-budget-cleanup-reserved'{
                    AddTasks $fixture 4;$installation=Invoke-EvidenceScheduleInstall $fixture.config;$script:syncConsumesBudget=$true
                    $null=Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256
                    Assert ($script:sync.Count -eq 1 -and $script:cleanup.Count -eq 4) 'outbox consumed cleanup reservation'
                }
                'scheduler-next-logon-catchup'{
                    AddTasks $fixture 1;$installation=Invoke-EvidenceScheduleInstall $fixture.config;$script:time=$script:time.AddDays(100)
                    $result=Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256
                    Assert ($result.lastRun -eq $script:time.ToString('o') -and $script:cleanup.Count -eq 1) 'missed days lost work'
                }
                'scheduler-phase-deadlines'{
                    AddTasks $fixture 2;$script:syncBudgets=[Collections.Generic.List[long]]::new();$script:cleanupBudgets=[Collections.Generic.List[long]]::new();$script:consumeSync=$true;$script:consumeCleanup=$true
                    $lateSync={param($config,$task,$budget)$script:sync.Add($task);$script:syncBudgets.Add([long](& $budget.Remaining));if($script:consumeSync){$script:consumeSync=$false;$script:elapsed+=119*[Diagnostics.Stopwatch]::Frequency};@{status='passed'}}
                    $lateCleanup={param($config,$task,$maximum,$budget)$script:cleanup.Add($task);$script:cleanupBudgets.Add([long](& $budget.Remaining));if($script:consumeCleanup){$script:consumeCleanup=$false;$script:elapsed+=479*[Diagnostics.Stopwatch]::Frequency};@{status='pending';deleted=0;skipped=1;blocked=0;remaining=1;items=@([pscustomobject]@{taskId=$task;artifactId=$null;reasonCode='deadline'})}}
                    & $module {param($s,$c)$script:SyncHook=$s;$script:CleanupHook=$c} $lateSync $lateCleanup
                    $installation=Invoke-EvidenceScheduleInstall $fixture.config;$result=Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256
                    Assert ($script:sync.Count -eq 1 -and $script:cleanup.Count -eq 1 -and $script:syncBudgets[0] -eq 119000 -and $script:cleanupBudgets[0] -eq 479000 -and $result.status -eq 'partial') 'phase deadline reset or reservation borrowed'
                    Assert ($result.syncCursor -ceq 'task-000' -and $result.cleanupCursor -ceq 'task-000' -and (Get-EvidenceScheduleStatus $fixture.config).job.lastRun -eq $script:time.ToString('o')) 'deadline cursor or final journal missing'
                    $script:sync.Clear();$script:cleanup.Clear();$result=Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256
                    Assert ($script:sync[0] -ceq 'task-001' -and $script:cleanup[0] -ceq 'task-001') 'deadline failed to yield next task'
                    # One second of runtime validation plus the two work phases can
                    # reach the job work deadline. Its hard reserve must still save.
                    $script:elapsed=0L;$script:elapsedCalls=0;$script:consumeSync=$true;$script:consumeCleanup=$true
                    $setupClock={if(++$script:elapsedCalls -eq 2){$script:elapsed+=[Diagnostics.Stopwatch]::Frequency};$script:elapsed}
                    & $module {param($e)$script:ElapsedClock=$e} $setupClock
                    $result=Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256
                    Assert ($script:elapsed -eq 599*[Diagnostics.Stopwatch]::Frequency -and $result.status -ceq 'partial' -and (Get-EvidenceScheduleStatus $fixture.config).job.lastRun -eq $script:time.ToString('o')) 'job reservation did not persist final partial journal'

                }
                'scheduler-age-clock-independent'{
                    AddTasks $fixture 2;$script:budgets=[Collections.Generic.List[long]]::new()
                    $ageOnly={param($config,$task,$budget)$script:time=$script:time.AddDays(30);$script:budgets.Add([long](& $budget.Remaining));@{status='passed'}}
                    & $module {param($s)$script:SyncHook=$s} $ageOnly
                    $installation=Invoke-EvidenceScheduleInstall $fixture.config;$result=Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256
                    Assert ($script:budgets.Count -eq 2 -and $script:budgets[0] -eq 119000 -and $script:budgets[1] -eq 119000 -and $result.status -eq 'ok') 'retention clock consumed operation deadline'
                }
                'scheduler-stage-global-budget'{
                    AddTasks $fixture 2;$script:stageRemaining=@{'task-000'=150;'task-001'=150};$script:budgets=[Collections.Generic.List[object]]::new()
                    $stages={param($config,$task,$maximum)
                        $script:budgets.Add([pscustomobject]@{task=$task;maximum=$maximum});$take=[Math]::Min($maximum,$script:stageRemaining[$task]);$script:stageRemaining[$task]-=$take
                        return @{status=if($script:stageRemaining[$task]){'pending'}else{'passed'};deleted=$take;skipped=0;blocked=0;remaining=$script:stageRemaining[$task];items=@(1..$take|ForEach-Object{[pscustomobject]@{taskId=$task;artifactId=$null;reasonCode='deleted'}})}
                    }
                    $passed={param($config,$task)@{status='passed'}};& $module {param($c,$s)$script:CleanupHook=$c;$script:SyncHook=$s} $stages $passed
                    $installation=Invoke-EvidenceScheduleInstall $fixture.config;$first=Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256
                    Assert ($first.deleted -eq 100 -and $first.status -eq 'partial' -and $script:budgets.Count -eq 1 -and $script:budgets[0].maximum -eq 100) 'stage budget exceeded or partial lost'
                    Assert ((Get-EvidenceScheduleStatus $fixture.config).job.status -eq 'partial') 'schedule status lost partial'
                    $second=Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256
                    Assert ($second.deleted -eq 100 -and $second.status -eq 'partial' -and $script:budgets[1].task -ceq 'task-001') 'stage task cursor starved next task'
                    $third=Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256
                    Assert ($third.deleted -eq 100 -and $third.status -eq 'ok' -and $script:stageRemaining['task-000'] -eq 0 -and $script:stageRemaining['task-001'] -eq 0 -and $script:budgets[2].maximum -eq 100 -and $script:budgets[3].maximum -eq 50) 'stage budget remainder or catchup failed'
                }
                'scheduler-stage-counter-budget'{
                    # Partial observations can precede item creation. Existing counters
                    # must still consume the reservation and prevent another 99 deletes.
                    AddTasks $fixture 2;$script:budgets=[Collections.Generic.List[int]]::new()
                    $stages={param($config,$task,$maximum)$script:budgets.Add($maximum);return @{status='passed';deleted=$maximum;skipped=0;blocked=0;remaining=0;items=@()}}
                    $passed={param($config,$task)@{status='passed'}};& $module {param($c,$s)$script:CleanupHook=$c;$script:SyncHook=$s} $stages $passed
                    $installation=Invoke-EvidenceScheduleInstall $fixture.config;$result=Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256
                    Assert ($result.deleted -eq 100 -and $result.status -eq 'partial' -and $script:budgets.Count -eq 1 -and $script:budgets[0] -eq 100) 'empty item result bypassed budget'
                }
                'scheduler-cleanup-partial-status'{
                    AddTasks $fixture 1;$passed={param($config,$task)@{status='passed'}}
                    $leftover={param($config,$task,$maximum)@{status='passed';deleted=0;skipped=1;blocked=0;remaining=1;items=@([pscustomobject]@{taskId=$task;artifactId=$null;reasonCode='staging-retained'})}}
                    & $module {param($c,$s)$script:CleanupHook=$c;$script:SyncHook=$s} $leftover $passed
                    $installation=Invoke-EvidenceScheduleInstall $fixture.config;$result=Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256
                    Assert ($result.status -eq 'partial' -and (Get-EvidenceScheduleStatus $fixture.config).job.status -eq 'partial') 'remaining hidden'
                    $pending={param($config,$task,$maximum)@{status='pending';deleted=0;skipped=1;blocked=0;remaining=0;items=@([pscustomobject]@{taskId=$task;artifactId=$null;reasonCode='staging-protected'})}}
                    & $module {param($c)$script:CleanupHook=$c} $pending
                    Assert ((Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256).status -eq 'partial') 'pending hidden'
                    & $module {param($c)$script:CleanupHook=$c} $cleanupHook
                    Assert ((Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256).status -eq 'ok') 'partial persisted after recovery'
                }
                'scheduler-reparse'{
                    $installation=Invoke-EvidenceScheduleInstall $fixture.config;$file=[IO.Path]::Combine($installation.runtime,'tools','Workflow')
                    [IO.Directory]::Delete($file,$true);$outside=[IO.Path]::Combine($fixture.work,'outside');[IO.Directory]::CreateDirectory($outside)|Out-Null
                    New-Item -ItemType Junction -Path $file -Target $outside|Out-Null
                    try{Reject {Invoke-EvidenceScheduledJob $installation.runtime $installation.manifestSha256}}finally{[IO.Directory]::Delete($file)}
                }
            }
            $executed.Add($case)
        }catch{$failed.Add($case+': '+$_.Exception.Message+' '+$_.ScriptStackTrace)}
    }
    if($ResultPath){[IO.File]::WriteAllText($ResultPath,(ConvertTo-Json -Depth 8 -InputObject ([ordered]@{registered=$cases;selected=$selected;executed=@($executed);failed=@($failed)})),[Text.UTF8Encoding]::new($false))}
    foreach($name in $executed){"PASS $name"};foreach($name in $failed){"FAIL $name"}
    "Cases: $($selected.Count)/$($cases.Count); passed: $($executed.Count); failed: $($failed.Count)"
    if($failed.Count -or $executed.Count -ne $selected.Count){exit 1}
}finally{
    & $module {$script:TestRoot=$null;$script:SourceHook=$null;$script:SchedulerHook=$null;$script:SyncHook=$null;$script:CleanupHook=$null;$script:Clock={[DateTimeOffset]::UtcNow};$script:ElapsedClock={[Diagnostics.Stopwatch]::GetTimestamp()}}
    & $contract {$script:DeploymentRoot=$null}; & $store {$script:TestRoot=$null}
    if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)}
}
