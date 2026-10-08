param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$module=Import-Module (Join-Path $PSScriptRoot '../EvidenceSchedule.psm1') -Force -PassThru
$contract=& $module {param($p)Import-Module $p -PassThru} (Join-Path $PSScriptRoot '../EvidenceContract.psm1')
$store=& $module {param($p)Import-Module $p -PassThru} (Join-Path $PSScriptRoot '../../Workflow/TaskEventStore.psm1')
$root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-schedule-'+[Guid]::NewGuid().ToString('N'))
$cases=@('scheduler-runtime-missing','runtime-hash-mismatch','runtime-config-mismatch','runtime-three-deps','scheduler-wrong-identity','scheduler-fixed-action','scheduler-duplicate','scheduler-fair-cursors','scheduler-sync-budget-cleanup-reserved','scheduler-next-logon-catchup','scheduler-reparse','scheduler-stage-global-budget','scheduler-stage-counter-budget','scheduler-cleanup-partial-status','scheduler-phase-deadlines','scheduler-age-clock-independent','scheduler-folder-missing','scheduler-folder-existing','scheduler-folder-denied','scheduler-folder-unknown','scheduler-adapter-identity-rejected','scheduler-action-cim-instance')
$cases+=@('runtime-binding-five-roles','runtime-binding-root-missing','runtime-binding-fileid-mismatch','runtime-binding-sid-mismatch','runtime-binding-acl-mismatch','runtime-binding-reparse','runtime-schema-one-rejected','runtime-context-force-import','runtime-context-switch-rejected','runtime-credential-missing','runtime-bound-guard-missing','runtime-binding-alias-physical')
$cases+=@('runtime-fresh-process-binding','runtime-fresh-process-fail-before-adapters')
$cases+=@('native-long-path-identity')
$registrationCases=@('missing','disabled','action','arguments','directory','principal','logon-type','run-level','daily-missing','daily-time','daily-interval','daily-disabled','daily-offset','daily-future','logon-missing','logon-owner','logon-disabled','trigger-extra','start-unavailable','instances','limit','description','enabled-unknown','export-failed','query-denied','info-failed','repetition','end-boundary','delay','equivalent','defaults','valid')
$cases+=@($registrationCases | ForEach-Object {'scheduler-registration-'+$_})
$cases+=@('scheduler-missing-cli','scheduler-descriptor-identity','scheduler-install-unconfirmed')
$selected=@(if($Case -ceq '*'){$cases}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
function Assert([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
function Reject([scriptblock]$Action){$rejected=$false;try{&$Action|Out-Null}catch{$rejected=$true};Assert $rejected 'expected rejection'}
function WriteJson([string]$Path,$Value){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))|Out-Null;[IO.File]::WriteAllText($Path,(ConvertTo-Json -Depth 30 -InputObject $Value),[Text.UTF8Encoding]::new($false));return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
$script:installed=$null;$script:installs=0;$script:wrong=$false;$script:sync=[Collections.Generic.List[string]]::new();$script:cleanup=[Collections.Generic.List[string]]::new();$script:time=[DateTimeOffset]::Parse('2026-01-01T00:00:00+00:00');$script:syncConsumesBudget=$false;$script:elapsed=0L
$scheduler={
    param($action,$identity)
    if($action -eq 'status'){
        if($script:wrong){throw 'scheduler-identity-mismatch'}
        if(-not $script:installed){return @{installed=$false}}
        return @{installed=$true;execute=$script:installed.execute;arguments=$script:installed.arguments;userId=$script:installed.ownerSid;logonType='InteractiveToken';runLevel='Limited';lastRun=$null;nextRun='03:00';lastResult=0}
    }
    $script:installs++;$script:installed=$identity;return @{installed=$true}
}
$clockHook={ $script:time };$elapsedHook={ $script:elapsed }
$syncHook={param($config,$task)$script:sync.Add($task);if($script:syncConsumesBudget){$script:time=$script:time.AddSeconds(120);$script:elapsed+=120*[Diagnostics.Stopwatch]::Frequency};throw 'offline delivery unavailable'}
$cleanupHook={param($config,$task,$maximum)$script:cleanup.Add($task);return @{status='passed';deleted=0;skipped=1;blocked=0;remaining=0;items=@([pscustomobject]@{taskId=$task;artifactId=$null;reasonCode='staging-retained'})}}
function Fixture([string]$Case){
    [AppDomain]::CurrentDomain.SetData('OneStarMaker.Evidence.StorageBinding.v1',$null);[AppDomain]::CurrentDomain.SetData('OneStarMaker.Evidence.RuntimeContext.v2',$null)
    $work=[IO.Path]::Combine($root,$Case);$source=[IO.Path]::Combine($work,'source');$deployment=[IO.Path]::Combine($work,'deployments');$runtime=[IO.Path]::Combine($work,'runtime')
    [IO.Directory]::CreateDirectory($source)|Out-Null
    $repo='a'*64;$id='b'*32
    $config=[IO.Path]::Combine($work,'config.json')
    $hash=WriteJson $config @{schemaVersion=2;profile='osm';endpoint='https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com';bucket='osm-artifacts';repositoryId=$repo;prefix='development/evidence/v2/';policy='task-end-30d-v1';deploymentId=$id}
    $null=WriteJson ([IO.Path]::Combine($deployment,$id+'.json')) @{schemaVersion=2;deploymentId=$id;repositoryId=$repo;configSha256=$hash;baselineRecordId='offline-baseline';baselineSha256='c'*64;ownerObservationId='offline-owner';ownerObservationSha256='d'*64}
    $paths=@('tools/Artifacts/EvidenceCleanupJob.ps1','tools/Artifacts/EvidenceSchedule.psm1','tools/Artifacts/ArtifactAcl.psm1','tools/Artifacts/EvidenceContract.psm1','tools/Artifacts/ArtifactCommands.psm1','tools/Artifacts/ArtifactPaths.psm1','tools/Workflow/WindowsStorePaths.psm1','tools/Workflow/TaskLifecycle.psm1','tools/Workflow/TaskEventStore.psm1','tools/workflow-task.ps1');$binaries=@()
    foreach($assembly in @('Probe/R2RouteTransport','Packaging/ArtifactPackaging','Transport/R2ArtifactTransport')){
        foreach($extension in @('.dll','.deps.json')){
            $path='tools/Artifacts/'+$assembly+$extension;$paths+=@($path)
            $full=[IO.Path]::Combine($source,$path);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($full))|Out-Null;[IO.File]::WriteAllText($full,'offline-binary-fixture')
            $binaries+=@(@{path=$path;sha256=(Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()})
        }
    }
    foreach($path in $paths){$full=[IO.Path]::Combine($source,$path);if(-not [IO.File]::Exists($full)){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($full))|Out-Null;[IO.File]::Copy([IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'../../..',$path)),$full)}}
    $inputs=@{root=$source;head='e'*40;files=$paths;binaries=$binaries}
    $roleRoots=[ordered]@{workflow=[IO.Path]::Combine($work,'workflow');evidence=[IO.Path]::Combine($work,'evidence');deployments=$deployment;transfers=[IO.Path]::Combine($work,'transfers');runtime=$runtime}
    $credential=[IO.Path]::Combine($work,'credentials')
    foreach($privateRoot in @($roleRoots.Values)+@($credential)){[IO.Directory]::CreateDirectory($privateRoot)|Out-Null;& $module {param($r)Set-ArtifactAcl $r $true} $privateRoot}
    & $contract {param($r)$script:DeploymentRoot=$r} $deployment
    & $store {param($r)$script:TestRoot=$r} ([IO.Path]::Combine($work,'workflow'))
    & $module {param($r,$i,$s,$y,$c,$k,$e)$script:ElapsedClock=$e;$script:TestRoot=$r;$script:SourceHook={$i}.GetNewClosure();$script:SchedulerHook=$s;$script:SyncHook=$y;$script:CleanupHook=$c;$script:Clock=$k} $runtime $inputs $scheduler $syncHook $cleanupHook $clockHook $elapsedHook
    & $module {param($r,$c)$script:BindingRootsHook={$r}.GetNewClosure();$script:CredentialRootHook={$c}.GetNewClosure()} $roleRoots $credential
    $script:installed=$null;$script:installs=0;$script:wrong=$false;$script:sync.Clear();$script:cleanup.Clear();$script:time=[DateTimeOffset]::Parse('2026-01-01T00:00:00+00:00');$script:syncConsumesBudget=$false;$script:elapsed=0L
    return @{config=$config;work=$work;source=$source;repo=$repo;inputs=$inputs;roles=$roleRoots;credential=$credential}
}
# Exercise the production OS adapter through command boundaries. Every OS entry
# is replaced in the module scope. One case creates only a real action CimInstance;
# folder operations and registration remain stubs.
function Invoke-OfflineSchedulerAdapter([string]$Mode,$RealAction=$null){
    $state=[pscustomobject]@{Mode=$Mode;Exists=($Mode -cin @('existing','typed-action'));ExpectedAction=$RealAction;ReceivedAction=$null;Calls=[Collections.Generic.List[string]]::new();FolderAcl='keep-acl';OtherTask='keep-task';Failed=$false;Registered=0}
    $rootFolder=[pscustomobject]@{State=$state}
    $rootFolder|Add-Member ScriptMethod CreateFolder {param($name,$security)$this.State.Calls.Add('create:'+ $name);if($name -cne 'OneStarMaker' -or $null -ne $security){throw 'wrong folder creation scope'};$this.State.Exists=$true;return [pscustomobject]@{Path='\OneStarMaker'}}
    $service=[pscustomobject]@{State=$state;Root=$rootFolder}
    $service|Add-Member ScriptMethod Connect {$this.State.Calls.Add('connect')}
    $service|Add-Member ScriptMethod GetFolder {param($path)
        $this.State.Calls.Add('get:'+ $path)
        if($path -ceq '\'){return $this.Root}
        if($path -cne '\OneStarMaker'){throw 'unexpected task folder'}
        if($this.State.Mode -ceq 'denied'){throw [UnauthorizedAccessException]::new('fixture denied')}
        if($this.State.Mode -ceq 'unknown'){throw [Runtime.InteropServices.COMException]::new('fixture unknown',-2147467259)}
        if(-not $this.State.Exists){throw [IO.FileNotFoundException]::new('fixture missing')}
        return [pscustomobject]@{Path='\OneStarMaker'}
    }
    $prior=& $module {$script:SchedulerHook}
    try{
        & $module {param($s,$v)
            $script:SchedulerHook=$null;$script:AdapterState=$s;$script:TaskServiceHook={$v}.GetNewClosure()
            function script:Get-ScheduledTask {param($TaskName,$TaskPath,$ErrorAction)
                $script:AdapterState.Calls.Add('query:'+ $TaskPath)
                if($script:AdapterState.Mode -ceq 'identity-rejected'){return [pscustomobject]@{Actions=@([pscustomobject]@{Execute='foreign.exe';Arguments='foreign'});Principal=[pscustomobject]@{UserId='foreign';LogonType='Interactive';RunLevel='Limited'}}}
            }
            function script:New-ScheduledTaskAction {param($Execute,$Argument,$WorkingDirectory)if($null -ne $script:AdapterState.ExpectedAction){return $script:AdapterState.ExpectedAction};@{Execute=$Execute;Argument=$Argument;WorkingDirectory=$WorkingDirectory}}
            function script:Export-ScheduledTask {param($TaskName,$TaskPath,$ErrorAction)'<Task />'}
            function script:New-ScheduledTaskPrincipal {param($UserId,$LogonType,$RunLevel)@{UserId=$UserId;LogonType=$LogonType;RunLevel=$RunLevel}}
            function script:New-ScheduledTaskTrigger {param([switch]$Daily,$At,[switch]$AtLogOn,$User)@{daily=$Daily;at=$At;atLogOn=$AtLogOn;user=$User}}
            function script:New-ScheduledTaskSettingsSet {param([switch]$StartWhenAvailable,$MultipleInstances,$ExecutionTimeLimit)@{start=$StartWhenAvailable;multiple=$MultipleInstances;limit=$ExecutionTimeLimit}}
            function script:Register-ScheduledTask {param($TaskName,$TaskPath,$Action,$Principal,$Trigger,$Settings,$Description)
                if(-not $script:AdapterState.Exists -or $TaskPath -cne '\OneStarMaker\' -or $TaskName -cne 'OSM-Evidence-Cleanup-aaaaaaaaaaaa' -or $Principal.LogonType -cne 'Interactive' -or $Principal.RunLevel -cne 'Limited' -or $Settings.limit -ne [TimeSpan]::FromMinutes(10)){throw 'wrong registration boundary'}
                if($null -ne $script:AdapterState.ExpectedAction){
                    if($Action -isnot [Microsoft.Management.Infrastructure.CimInstance] -or -not [object]::ReferenceEquals($Action,$script:AdapterState.ExpectedAction)){throw 'scheduler action converted or copied'}
                    $script:AdapterState.ReceivedAction=$Action
                }
                $script:AdapterState.Calls.Add('register:'+ $TaskPath);$script:AdapterState.Registered++
            }
        } $state $service
        $identity=@{name='OSM-Evidence-Cleanup-aaaaaaaaaaaa';execute='fixture-pwsh.exe';arguments='fixture';runtime='fixture-runtime';ownerSid='fixture-owner';manifestSha256='a'*64}
        try{& $module {param($i)Invoke-EvidenceScheduler install $i} $identity|Out-Null}catch{$state.Failed=$true}
    }finally{
        & $module {param($p)$script:SchedulerHook=$p;$script:TaskServiceHook=$null;$script:AdapterState=$null;foreach($name in @('Get-ScheduledTask','Export-ScheduledTask','New-ScheduledTaskAction','New-ScheduledTaskPrincipal','New-ScheduledTaskTrigger','New-ScheduledTaskSettingsSet','Register-ScheduledTask')){Remove-Item ('Function:script:'+ $name) -ErrorAction SilentlyContinue}} $prior
    }
    return $state
}
# OS definition fixture; every OS command is stubbed and writes fail the test.
function TaskXml($Identity){
    $escape={param($s)[Security.SecurityElement]::Escape($s)}
    return @"
<Task xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task" version="1.2">
 <RegistrationInfo><Description>OSM Evidence runtime $($Identity.manifestSha256)</Description></RegistrationInfo>
 <Principals><Principal id="Owner"><UserId>$($Identity.ownerSid)</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>
 <Actions Context="Owner"><Exec><Command>$(& $escape $Identity.execute)</Command><Arguments>$(& $escape $Identity.arguments)</Arguments><WorkingDirectory>$(& $escape $Identity.runtime)</WorkingDirectory></Exec></Actions>
 <Triggers><CalendarTrigger><StartBoundary>2020-01-01T03:00:00</StartBoundary><Enabled>true</Enabled><ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay></CalendarTrigger><LogonTrigger><Enabled>true</Enabled><UserId>$($Identity.ownerSid)</UserId></LogonTrigger></Triggers>
 <Settings><Enabled>true</Enabled><StartWhenAvailable>true</StartWhenAvailable><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><ExecutionTimeLimit>PT10M</ExecutionTimeLimit></Settings>
</Task>
"@
}
function WithTaskObservation($Identity,[string]$Xml,[string]$Mode,[scriptblock]$Check){
    $observation=@{identity=$Identity;xml=$Xml;mode=$Mode;writes=0}
    $prior=& $module {$script:SchedulerHook}
    try{
        & $module {param($o)
            $script:SchedulerHook=$null;$script:TaskObservation=$o
            function script:Get-ScheduledTask {param($TaskName,$TaskPath,$ErrorAction)
                if($script:TaskObservation.mode -ceq 'query-denied'){throw [UnauthorizedAccessException]::new('offline denied')}
                if($script:TaskObservation.mode -ceq 'missing'){return}
                $i=$script:TaskObservation.identity
                return [pscustomobject]@{Actions=@([pscustomobject]@{Execute=$i.execute;Arguments=$i.arguments;WorkingDirectory=$i.runtime});Principal=[pscustomobject]@{UserId=$i.ownerSid;LogonType='Interactive';RunLevel='Limited'}}
            }
            function script:Export-ScheduledTask {param($TaskName,$TaskPath,$ErrorAction)if($script:TaskObservation.mode -ceq 'export-failed'){throw 'offline export failed'};$script:TaskObservation.xml}
            function script:Get-ScheduledTaskInfo {param($InputObject,$ErrorAction)if($script:TaskObservation.mode -ceq 'info-failed'){throw 'offline info failed'};[pscustomobject]@{LastRunTime=$null;NextRunTime=$null;LastTaskResult=0}}
            foreach($name in @('Register-ScheduledTask','Set-ScheduledTask','Enable-ScheduledTask','Disable-ScheduledTask','Unregister-ScheduledTask')){
                Set-Item ('Function:script:'+ $name) {$script:TaskObservation.writes++;throw 'unexpected OS mutation'}
            }
        } $observation
        & $Check
        Assert ($observation.writes -eq 0) 'existing task was repaired'
    }finally{
        & $module {param($p)
            $script:SchedulerHook=$p;$script:TaskObservation=$null
            foreach($name in @('Get-ScheduledTask','Export-ScheduledTask','Get-ScheduledTaskInfo','Register-ScheduledTask','Set-ScheduledTask','Enable-ScheduledTask','Disable-ScheduledTask','Unregister-ScheduledTask')){Remove-Item ('Function:script:'+ $name) -ErrorAction SilentlyContinue}
        } $prior
    }
}
function AddTasks($Fixture,[int]$Count){
    for($i=0;$i -lt $Count;$i++){
        $id='task-'+$i.ToString('D3')
        $guard=& $store {param($r,$t)Enter-WorkflowTaskGuard $r $t} $Fixture.repo $id
        Assert ($guard.Path.StartsWith($Fixture.roles.workflow+'\',[StringComparison]::OrdinalIgnoreCase)) 'offline guard escaped fixture root'
        $guard.Dispose()
    }
}
try{
    foreach($case in $selected){
        try{
            Assert ($case -cin $cases) 'unknown case';$fixture=Fixture $case
            switch($case){
                'scheduler-descriptor-identity'{
                    $installation=Invoke-EvidenceScheduleInstall $fixture.config
                    $descriptor=Join-Path $fixture.roles.runtime ($fixture.repo+'.schedule.json')
                    $identity=Get-Content -Raw $descriptor | ConvertFrom-Json -AsHashtable
                    $identity.ownerSid='foreign-owner';$null=WriteJson $descriptor $identity
                    WithTaskObservation $identity (TaskXml $identity) 'valid' {Reject {Get-EvidenceScheduleStatus $fixture.config}}
                }
                'scheduler-install-unconfirmed'{
                    & $module {$script:SchedulerHook={param($a,$i)if($a -ceq 'install'){@{installed=$true}}else{@{installed=$false}}}}
                    Reject {Invoke-EvidenceScheduleInstall $fixture.config}
                    Assert (-not (Test-Path (Join-Path $fixture.roles.runtime ($fixture.repo+'.schedule.json')))) 'unconfirmed registration wrote success descriptor'
                }
                {$_ -clike 'scheduler-registration-*'}{
                    $installation=Invoke-EvidenceScheduleInstall $fixture.config;$identity=$script:installed
                    $mode=$case.Substring('scheduler-registration-'.Length);$xml=TaskXml $identity
                    switch($mode){
                        'disabled'{$xml=$xml.Replace('<Settings><Enabled>true','<Settings><Enabled>false')}
                        'action'{$xml=$xml.Replace('<Command>','<Command>foreign-')}
                        'arguments'{$xml=$xml.Replace('<Arguments>','<Arguments>foreign-')}
                        'directory'{$xml=$xml.Replace('<WorkingDirectory>','<WorkingDirectory>foreign-')}
                        'principal'{$xml=$xml.Replace('<UserId>'+ $identity.ownerSid,'<UserId>foreign')}
                        'logon-type'{$xml=$xml.Replace('InteractiveToken','Password')}
                        'run-level'{$xml=$xml.Replace('LeastPrivilege','HighestAvailable')}
                        'daily-missing'{$xml=$xml -replace '<CalendarTrigger>.*?</CalendarTrigger>',''}
                        'daily-time'{$xml=$xml.Replace('T03:00:00','T04:00:00')}
                        'daily-interval'{$xml=$xml.Replace('<DaysInterval>1','<DaysInterval>2')}
                        'daily-disabled'{$xml=$xml.Replace('<Enabled>true</Enabled><ScheduleByDay>','<Enabled>false</Enabled><ScheduleByDay>')}
                        'daily-offset'{$xml=$xml.Replace('2020-01-01T03:00:00',([datetime]::Parse('2020-01-01T04:00:00').ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')))}
                        'daily-future'{$xml=$xml.Replace('2020-01-01','2099-01-01')}
                        'logon-missing'{$xml=$xml -replace '<LogonTrigger>.*?</LogonTrigger>',''}
                        'logon-owner'{$xml=$xml.Replace('<LogonTrigger><Enabled>true</Enabled><UserId>','<LogonTrigger><Enabled>true</Enabled><UserId>foreign-')}
                        'logon-disabled'{$xml=$xml.Replace('<LogonTrigger><Enabled>true','<LogonTrigger><Enabled>false')}
                        'trigger-extra'{$xml=$xml.Replace('</Triggers>','<BootTrigger /></Triggers>')}
                        'start-unavailable'{$xml=$xml.Replace('<StartWhenAvailable>true</StartWhenAvailable>','')}
                        'instances'{$xml=$xml.Replace('IgnoreNew','Parallel')}
                        'limit'{$xml=$xml.Replace('PT10M','PT11M')}
                        'description'{$xml=$xml.Replace('OSM Evidence runtime '+$identity.manifestSha256,'OSM Evidence runtime '+('0'*64))}
                        'enabled-unknown'{$xml=$xml.Replace('<Settings><Enabled>true','<Settings><Enabled>unknown')}
                        'repetition'{$xml=$xml.Replace('</CalendarTrigger>','<Repetition><Interval>PT1M</Interval></Repetition></CalendarTrigger>')}
                        'end-boundary'{$xml=$xml.Replace('</CalendarTrigger>','<EndBoundary>2021-01-01T00:00:00</EndBoundary></CalendarTrigger>')}
                        'delay'{$xml=$xml.Replace('</LogonTrigger>','<Delay>PT1M</Delay></LogonTrigger>')}
                        'equivalent'{$xml=$xml.Replace('2020-01-01T03:00:00',([datetime]::Parse('2020-01-01T03:00:00').ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.0000000Z'))).Replace('PT10M','PT600S').Replace('<Enabled>true</Enabled>','<Enabled>1</Enabled>').Replace('<StartWhenAvailable>true','<StartWhenAvailable>1')}
                        'defaults'{$xml=$xml.Replace('<Enabled>true</Enabled>','').Replace('<RunLevel>LeastPrivilege</RunLevel>','').Replace('<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>','').Replace('</CalendarTrigger>','<RandomDelay>PT0S</RandomDelay></CalendarTrigger>').Replace('</LogonTrigger>','<Delay>PT0M</Delay></LogonTrigger>')}
                    }
                    $descriptor=Join-Path $fixture.roles.runtime ($fixture.repo+'.schedule.json');$before=(Get-FileHash $descriptor).Hash
                    WithTaskObservation $identity $xml $mode {
                        if($mode -cin @('valid','defaults','equivalent')){
                            Assert ((Get-EvidenceScheduleStatus $fixture.config).status -ceq 'passed') 'valid registration status rejected'
                            Assert ((Invoke-EvidenceScheduleInstall $fixture.config).status -ceq 'passed') 'valid registration install rejected'
                        }elseif($mode -ceq 'missing'){
                            $result=Get-EvidenceScheduleStatus $fixture.config
                            Assert ($result.status -cne 'passed' -and $result.reasonCode -ceq 'scheduler-not-installed') 'missing OS task succeeded'
                        }else{
                            Reject {Get-EvidenceScheduleStatus $fixture.config}
                            Reject {Invoke-EvidenceScheduleInstall $fixture.config}
                        }
                    }
                    Assert ((Get-FileHash $descriptor).Hash -ceq $before) 'descriptor changed for existing registration'
                }
                'scheduler-missing-cli'{
                    $installation=Invoke-EvidenceScheduleInstall $fixture.config
                    $child=Join-Path $fixture.work 'missing-cli.ps1'
                    [IO.File]::WriteAllText($child,@'
param($Repo,$RuntimeRoot,$DeploymentRoot,$Config)
$ErrorActionPreference='Stop'
$m=Import-Module (Join-Path $Repo 'tools/Artifacts/EvidenceSchedule.psm1') -PassThru
& $m {param($r,$d,$repo)
 $script:TestRoot=$r;$script:SchedulerHook={param($a,$i)@{installed=$false}}
 $manifest=Get-Content -Raw (Join-Path $r (('e'*40)+'/runtime.json')) | ConvertFrom-Json -AsHashtable
 $roots=@{};foreach($role in $manifest.storageBinding.roles.Keys){$roots[$role]=$manifest.storageBinding.roles[$role].logicalPath}
 $script:BindingRootsHook={$roots}.GetNewClosure()
 $c=Import-Module (Join-Path $repo 'tools/Artifacts/EvidenceContract.psm1') -PassThru
 & $c {param($root)$script:DeploymentRoot=$root} $d
} $RuntimeRoot $DeploymentRoot $Repo
& (Join-Path $Repo 'tools/artifacts.ps1') evidence schedule status --profile osm --config $Config
exit $LASTEXITCODE
'@)
                    $output=& pwsh -NoProfile -File $child -Repo ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))) -RuntimeRoot $fixture.roles.runtime -DeploymentRoot $fixture.roles.deployments -Config $fixture.config 2>&1
                    Assert ($LASTEXITCODE -eq 2 -and ($output -join '') -match 'scheduler-not-installed') ('missing OS task CLI did not return pending exit 2: '+$LASTEXITCODE+' '+($output -join ''))
                }
                'native-long-path-identity'{
                    $native=$module.NestedModules | Where-Object Name -eq 'WindowsStorePaths' | Select-Object -Last 1
                    Assert ($null -eq (& $native {$script:IdentityHook})) 'native case used metadata injection'
                    $read={param($p)& $native {param($path)Get-WindowsStorePathIdentity $path} $p}.GetNewClosure()
                    $same={param($p,$i)& $native {param($path,$identity)Assert-WindowsStorePathIdentity $path $identity} $p $i}.GetNewClosure()
                    $short=& $read $fixture.work
                    $shortAgain=& $read $short.physicalPath
                    Assert ($short.volumeSerial -ceq $shortAgain.volumeSerial -and $short.fileId -ceq $shortAgain.fileId -and $short.isDirectory) 'short native root identity changed'
                    $longDirectory=[IO.Path]::Combine($short.physicalPath,'long-native')
                    while($longDirectory.Length -le 280){$longDirectory=[IO.Path]::Combine($longDirectory,'directory-'+('x'*30))}
                    Assert ($longDirectory.StartsWith($short.physicalPath+'\',[StringComparison]::OrdinalIgnoreCase)) 'long fixture escaped temp scope'
                    [IO.Directory]::CreateDirectory($longDirectory)|Out-Null
                    $leaf=[IO.Path]::Combine($longDirectory,'receipt.json');[IO.File]::WriteAllText($leaf,'native metadata fixture')
                    $directory=& $read $longDirectory;$file=& $read $leaf
                    Assert ($directory.isDirectory -and -not $file.isDirectory -and $directory.volumeSerial -ceq $short.volumeSerial -and $file.volumeSerial -ceq $short.volumeSerial) 'long native type/volume mismatch'
                    Assert ($directory.physicalPath -ceq $longDirectory -and $file.physicalPath -ceq $leaf -and -not $file.physicalPath.StartsWith('\\')) 'extended notation escaped native boundary'
                    Assert ((& $same $longDirectory $directory) -ceq $longDirectory -and (& $same $leaf $file) -ceq $leaf) 'long native FileId changed'
                    foreach($unsafe in @('relative-path','\\server\share\receipt.json',('\\?\'+$leaf),('\\.\'+$leaf),($leaf+':stream'),[IO.Path]::Combine($longDirectory,'missing.json'))){Reject {& $read $unsafe}}
                    if($ResultPath){[IO.File]::WriteAllText($ResultPath+'.native-long-path.json',(ConvertTo-Json -Depth 6 -InputObject ([ordered]@{case=$case;fixtureRoot=$short.physicalPath;identityHookUsed=$false;directoryCharacters=$longDirectory.Length;fileCharacters=$leaf.Length;shortRoot=$short;directory=$directory;file=$file;unsafeRejected=@('relative','UNC','extended-input','device-input','ADS','missing')})),[Text.UTF8Encoding]::new($false))}
                }
                {$_ -cin @('runtime-fresh-process-binding','runtime-fresh-process-fail-before-adapters')}{
                    AddTasks $fixture 1;$installation=Invoke-EvidenceScheduleInstall $fixture.config
                    if($case -ceq 'runtime-fresh-process-fail-before-adapters'){[IO.Directory]::Delete($fixture.roles.transfers)}
                    $childPath=[IO.Path]::Combine($fixture.work,'child.ps1')
                    [IO.File]::WriteAllText($childPath,@'
param($Runtime,$Hash,$Credential,$Mode)
$ErrorActionPreference='Stop';$WarningPreference='SilentlyContinue'
$schedule=Import-Module ([IO.Path]::Combine($Runtime,'tools','Artifacts','EvidenceSchedule.psm1')) -PassThru
$before=@(Get-Module -All|Where-Object Name -in @('EvidenceContract','TaskEventStore'))
if($before.Count){throw 'adapters imported before validation'}
$manifest=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText([IO.Path]::Combine($Runtime,'runtime.json'))) -AsHashtable -DateKind String
$roots=[ordered]@{};foreach($role in $manifest.storageBinding.roles.Keys){$roots[$role]=$manifest.storageBinding.roles[$role].logicalPath}
& $schedule {param($r,$c)$script:BindingRootsHook={$r}.GetNewClosure();$script:CredentialRootHook={$c}.GetNewClosure()} $roots $Credential
if($Mode -ceq 'runtime-fresh-process-fail-before-adapters'){
 $failed=$false;try{$null=Initialize-EvidenceRuntimeContext $Runtime $Hash}catch{$failed=$true}
 if(-not $failed -or [AppDomain]::CurrentDomain.GetData('OneStarMaker.Evidence.StorageBinding.v1') -or @(Get-Module -All|Where-Object Name -in @('EvidenceContract','TaskEventStore')).Count){throw 'adapter/context work after missing root'}
}else{
 $null=Initialize-EvidenceRuntimeContext $Runtime $Hash
 $store=Import-Module ([IO.Path]::Combine($Runtime,'tools','Workflow','TaskEventStore.psm1')) -Force -PassThru
 $root=& $store {Get-WorkflowRoot}
 if($root -cne $manifest.storageBinding.roles.workflow.physicalPath){throw 'fresh root mismatch'}
}
@{status='passed';mode=$Mode}|ConvertTo-Json -Compress
'@)
                    $start=[Diagnostics.ProcessStartInfo]::new([Diagnostics.Process]::GetCurrentProcess().MainModule.FileName);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
                    foreach($arg in @('-NoProfile','-File',$childPath,'-Runtime',$installation.runtime,'-Hash',$installation.manifestSha256,'-Credential',$fixture.credential,'-Mode',$case)){$start.ArgumentList.Add($arg)}
                    $process=[Diagnostics.Process]::Start($start)
                    try{$out=$process.StandardOutput.ReadToEndAsync();$err=$process.StandardError.ReadToEndAsync();Assert ($process.WaitForExit(15000)) 'fresh child deadline';Assert ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),3000)) 'fresh child pipes';Assert ($process.ExitCode -eq 0) ('fresh child failed '+$out.Result+$err.Result);Assert (($out.Result|ConvertFrom-Json).status -ceq 'passed') 'fresh child missing observation'}finally{if(-not $process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
                }
                {$_ -clike 'runtime-binding-*' -or $_ -cin @('runtime-schema-one-rejected','runtime-context-force-import','runtime-context-switch-rejected','runtime-credential-missing','runtime-bound-guard-missing')}{
                    AddTasks $fixture 1;$installation=Invoke-EvidenceScheduleInstall $fixture.config
                    $manifestPath=[IO.Path]::Combine($installation.runtime,'runtime.json');$manifest=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($manifestPath)) -AsHashtable -DateKind String
                    $expectedHash=$installation.manifestSha256
                    switch($case){
                        'runtime-binding-five-roles'{Assert ($manifest.schemaVersion -eq 2 -and $manifest.storageBinding.schemaVersion -eq 1 -and @($manifest.storageBinding.roles.Keys).Count -eq 5) 'missing fixed binding'}
                        'runtime-binding-root-missing'{[IO.Directory]::Delete($fixture.roles.transfers)}
                        'runtime-binding-fileid-mismatch'{$manifest.storageBinding.roles.transfers.fileId='0'*32;$expectedHash=WriteJson $manifestPath $manifest}
                        'runtime-binding-sid-mismatch'{$manifest.storageBinding.ownerSid='S-1-5-18';$expectedHash=WriteJson $manifestPath $manifest}
                        'runtime-binding-acl-mismatch'{$acl=[IO.FileSystemAclExtensions]::GetAccessControl([IO.DirectoryInfo]::new($fixture.roles.transfers));$acl.SetAccessRuleProtection($false,$true);[IO.FileSystemAclExtensions]::SetAccessControl([IO.DirectoryInfo]::new($fixture.roles.transfers),$acl)}
                        'runtime-binding-reparse'{[IO.Directory]::Delete($fixture.roles.transfers);New-Item -ItemType Junction -Path $fixture.roles.transfers -Target $fixture.roles.evidence|Out-Null}
                        'runtime-schema-one-rejected'{$manifest.schemaVersion=1;$expectedHash=WriteJson $manifestPath $manifest}
                        'runtime-credential-missing'{[IO.Directory]::Delete($fixture.credential)}
                        'runtime-bound-guard-missing'{[IO.File]::Delete([IO.Path]::Combine($fixture.roles.workflow,$fixture.repo,'tasks','task-000','task.lock'))}
                        'runtime-binding-alias-physical'{
                            $aliases=[ordered]@{};foreach($role in $fixture.roles.Keys){$aliases[$role]=[IO.Path]::Combine($fixture.work,'absent-alias',$role);$manifest.storageBinding.roles[$role].logicalPath=$aliases[$role]}
                            & $module {param($a)$script:BindingRootsHook={$a}.GetNewClosure()} $aliases
                            $expectedHash=WriteJson $manifestPath $manifest
                        }
                    }
                    if($case -cin @('runtime-binding-five-roles','runtime-context-force-import','runtime-context-switch-rejected','runtime-binding-alias-physical')){
                        $null=Initialize-EvidenceRuntimeContext $installation.runtime $expectedHash
                        Assert ((& $store {Get-WorkflowRoot}) -ceq $fixture.roles.workflow) 'guard root diverged'
                        if($case -ceq 'runtime-context-force-import'){
                            $fresh=Import-Module (Join-Path $PSScriptRoot '../../Workflow/TaskEventStore.psm1') -Force -PassThru
                            Assert ((& $fresh {Get-WorkflowRoot}) -ceq $fixture.roles.workflow) 'Force import lost binding'
                            $guard=& $fresh {param($r)Enter-WorkflowTaskGuard $r 'task-000'} $fixture.repo
                            try{Assert ($guard.Handle.Name -ceq [IO.Path]::Combine($fixture.roles.workflow,$fixture.repo,'tasks','task-000','task.lock')) 'reimport guard diverged'}finally{$guard.Dispose()}
                        }elseif($case -ceq 'runtime-context-switch-rejected'){
                            $before=[AppDomain]::CurrentDomain.GetData('OneStarMaker.Evidence.StorageBinding.v1');$changed=ConvertFrom-Json -InputObject $before -AsHashtable;$changed.roles.transfers.logicalPath+='-other'
                            Reject {& $module {param($r,$h,$b)Set-EvidenceRuntimeContext $r $h $b} $installation.runtime $expectedHash $changed}
                            Assert ([AppDomain]::CurrentDomain.GetData('OneStarMaker.Evidence.StorageBinding.v1') -ceq $before) 'partially switched context'
                        }
                    }elseif($case -ceq 'runtime-bound-guard-missing'){
                        $null=Initialize-EvidenceRuntimeContext $installation.runtime $expectedHash
                        Reject {& $store {param($r)Enter-WorkflowTaskGuard $r 'task-000'} $fixture.repo}
                        Assert (-not [IO.File]::Exists([IO.Path]::Combine($fixture.roles.workflow,$fixture.repo,'tasks','task-000','task.lock'))) 'recreated missing guard'
                    }else{
                        Reject {Invoke-EvidenceScheduledJob $installation.runtime $expectedHash}
                        Assert ($script:sync.Count -eq 0 -and $script:cleanup.Count -eq 0 -and -not [IO.File]::Exists([IO.Path]::Combine($installation.runtime,'job-state.json'))) 'adapter work before fixed binding'
                        if($case -ceq 'runtime-binding-root-missing'){Assert (-not [IO.Directory]::Exists($fixture.roles.transfers)) 'repaired missing root'}
                        if($case -ceq 'runtime-credential-missing'){Assert (-not [IO.Directory]::Exists($fixture.credential)) 'created credential root'}
                    }
                    if($case -ceq 'runtime-binding-reparse'){[IO.Directory]::Delete($fixture.roles.transfers)}
                }
                {$_ -cin @('scheduler-folder-missing','scheduler-folder-existing','scheduler-folder-denied','scheduler-folder-unknown','scheduler-adapter-identity-rejected')}{
                    $mode=switch($case){'scheduler-folder-missing'{'missing'};'scheduler-folder-existing'{'existing'};'scheduler-folder-denied'{'denied'};'scheduler-folder-unknown'{'unknown'};default{'identity-rejected'}}
                    $adapter=Invoke-OfflineSchedulerAdapter $mode;$created=@($adapter.Calls|Where-Object {$_ -clike 'create:*'})
                    Assert ($adapter.FolderAcl -ceq 'keep-acl' -and $adapter.OtherTask -ceq 'keep-task') 'changed existing folder security or other task'
                    if($mode -ceq 'missing'){
                        Assert (-not $adapter.Failed -and $adapter.Registered -eq 1 -and $created.Count -eq 1 -and $created[0] -ceq 'create:OneStarMaker') 'missing exact folder was not created before registration'
                        Assert (($adapter.Calls -join '|') -ceq 'query:\OneStarMaker\|connect|get:\OneStarMaker|get:\|create:OneStarMaker|register:\OneStarMaker\') 'folder creation or registration order changed'
                    }elseif($mode -ceq 'existing'){Assert (-not $adapter.Failed -and $adapter.Registered -eq 1 -and $created.Count -eq 0 -and @($adapter.Calls|Where-Object {$_ -ceq 'get:\'}).Count -eq 0) 'rewrote existing folder'}
                    else{Assert ($adapter.Failed -and $adapter.Registered -eq 0 -and $created.Count -eq 0) 'registered after denied/unknown/identity failure';if($mode -ceq 'identity-rejected'){Assert (@($adapter.Calls|Where-Object {$_ -ceq 'connect'}).Count -eq 0) 'created folder before rejecting existing task identity'}}
                }
                'scheduler-action-cim-instance'{
                    # New-ScheduledTaskAction creates only an in-memory CIM value.
                    # Actual folder and Register-ScheduledTask APIs are never called.
                    $value=ScheduledTasks\New-ScheduledTaskAction -Execute 'fixture-pwsh.exe' -Argument 'fixture' -WorkingDirectory 'C:\fixture-runtime'
                    try{
                        Assert ($value -is [Microsoft.Management.Infrastructure.CimInstance] -and $value.CimClass.CimClassName -ceq 'MSFT_TaskExecAction') 'real task action CIM fixture unavailable'
                        $adapter=Invoke-OfflineSchedulerAdapter typed-action $value
                        Assert (-not $adapter.Failed -and $adapter.Registered -eq 1 -and $adapter.ReceivedAction -is [Microsoft.Management.Infrastructure.CimInstance] -and [object]::ReferenceEquals($value,$adapter.ReceivedAction)) 'action CIM type or instance changed before registration'
                        Assert ($adapter.FolderAcl -ceq 'keep-acl' -and $adapter.OtherTask -ceq 'keep-task' -and @($adapter.Calls|Where-Object {$_ -clike 'create:*'}).Count -eq 0) 'typed action changed existing folder or task'
                    }finally{$value.Dispose()}
                }
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
                    $lockPath=[IO.Path]::Combine($installation.runtime,'job.lock');[IO.File]::WriteAllText($lockPath,'');& $module {param($p)Set-ArtifactAcl $p $false} $lockPath;$handle=[IO.FileStream]::new($lockPath,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
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
    [AppDomain]::CurrentDomain.SetData('OneStarMaker.Evidence.StorageBinding.v1',$null);[AppDomain]::CurrentDomain.SetData('OneStarMaker.Evidence.RuntimeContext.v2',$null)
    & $module {$script:TestRoot=$null;$script:BindingRootsHook=$null;$script:CredentialRootHook=$null;$script:SourceHook=$null;$script:SchedulerHook=$null;$script:SyncHook=$null;$script:CleanupHook=$null;$script:Clock={[DateTimeOffset]::UtcNow};$script:ElapsedClock={[Diagnostics.Stopwatch]::GetTimestamp()}}
    & $contract {$script:DeploymentRoot=$null}; & $store {$script:TestRoot=$null}
    if([IO.Directory]::Exists($root)){Assert ([IO.Path]::GetFullPath($root).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\osm-schedule-',[StringComparison]::OrdinalIgnoreCase)) 'cleanup escaped temp fixture';[IO.Directory]::Delete($root,$true)}
}
