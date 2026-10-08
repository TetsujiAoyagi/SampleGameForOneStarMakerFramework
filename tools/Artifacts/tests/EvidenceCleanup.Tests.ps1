param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
. (Join-Path $PSScriptRoot 'EvidenceReaderFixture.ps1')
$registered=@('delete-success','delete-timeout','delete-forbidden','delete-crash-reconcile','delete-after-remote-reconcile','cleanup-partial','active-marker-protected','use-delete-race','delete-use-race','resume-delete-race','delete-resume-race','source-marker-protected','reparse-protected','staging-cleanup','staging-transfer-protected','dry-run-no-delete','fair-object-cursor','staging-adoption-success','child-not-stopped','corrupt-catalog-non-delete','stopped-operation-cleanup','recovery-staging-cleanup','corrupt-lifecycle-projection','staging-fair-cursor','staging-result-accounting','deadline-request-and-guard','deadline-atomic-ack-retry','old-alias-cleanup','old-alias-fetch','old-alias-child-eof-blocked','old-alias-marker-unresolved','old-alias-copy-acl-blocked','missing-owned-copy','noncanonical-owned-copy')
$registered+=@('fresh-process-cleanup-contract')
$selected=if($Case -ceq '*'){$registered}else{@($Case.Split(','))};$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
function Start-EvidenceChild($Env,$Publish,[string]$Action,[string]$Signal='',[string]$Now=''){
    $info=[Diagnostics.ProcessStartInfo]::new('pwsh');$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($arg in @('-NoProfile','-File',(Join-Path $PSScriptRoot 'EvidenceTestChild.ps1'),'-Root',$Env.Root,'-ConfigPath',$Env.ConfigPath,'-Reference',$Publish.reference,'-Sha256',$Publish.packageSha256,'-Action',$Action,'-Signal',$Signal,'-Now',$Now)){$info.ArgumentList.Add($arg)}
    $p=[Diagnostics.Process]::new();$p.StartInfo=$info;$null=$p.Start();return [pscustomobject]@{Process=$p;Out=$p.StandardOutput.ReadToEndAsync();Err=$p.StandardError.ReadToEndAsync()}
}
function Complete-EvidenceChild($Child){Assert-EvidenceTest ($Child.Process.WaitForExit(15000)) 'child unfinished';Assert-EvidenceTest ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($Child.Out,$Child.Err),5000)) 'child pipes';return [pscustomobject]@{exit=$Child.Process.ExitCode;output=$Child.Out.Result;error=$Child.Err.Result}}
function Test-FreshCleanupContract($Env){
    $repoRoot=[IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'../../..'));$source=[IO.Path]::Combine($Env.Root,'runtime-source')
    $files=@(& rg --files ([IO.Path]::Combine($repoRoot,'tools','Artifacts')) ([IO.Path]::Combine($repoRoot,'tools','Workflow')) | Where-Object {$_ -match '\.(ps1|psm1)$' -and $_ -cnotmatch '[\\/](tests|artifacts|bin|obj)[\\/]'} | ForEach-Object {[IO.Path]::GetRelativePath($repoRoot,$_).Replace('\','/')})+@('tools/workflow-task.ps1','tools/artifacts.ps1');$binaries=@()
    foreach($file in $files){$destination=[IO.Path]::Combine($source,$file);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))|Out-Null;[IO.File]::Copy([IO.Path]::Combine($repoRoot,$file),$destination)}
    foreach($binary in @('Probe/R2RouteTransport','Packaging/ArtifactPackaging','Transport/R2ArtifactTransport')){foreach($extension in @('.dll','.deps.json')){
        $relative='tools/Artifacts/'+$binary+$extension;$destination=[IO.Path]::Combine($source,$relative);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))|Out-Null;[IO.File]::WriteAllText($destination,'offline-metadata-binary');$files+=@($relative);$binaries+=@(@{path=$relative;sha256=(Get-FileHash -LiteralPath $destination).Hash.ToLowerInvariant()})
    }}
    $roots=[ordered]@{workflow=[IO.Path]::Combine($Env.Root,'workflow');evidence=[IO.Path]::Combine($Env.Root,'catalog');deployments=[IO.Path]::Combine($Env.Root,'deployments');transfers=[IO.Path]::Combine($Env.Root,'transfers');runtime=[IO.Path]::Combine($Env.Root,'runtime')};$credential=[IO.Path]::Combine($Env.Root,'credentials')
    foreach($directory in @($roots.Values)+@($credential)){[IO.Directory]::CreateDirectory($directory)|Out-Null;Set-ArtifactAcl $directory $true}
    $schedule=Import-Module (Join-Path $PSScriptRoot '../EvidenceSchedule.psm1') -Force -PassThru
    $inputs=@{root=$source;head='e'*40;files=@($files|ForEach-Object {$_.Replace('\','/')});binaries=$binaries}
    try{
        & $schedule {param($r,$i)$script:BindingRootsHook={$r}.GetNewClosure();$script:TestRoot=$r.runtime;$script:SourceHook={$i}.GetNewClosure();$script:SchedulerHook={param($a,$identity)@{installed=$false}}} $roots $inputs
        $runtime=Invoke-EvidenceScheduleInstall $Env.ConfigPath
    }finally{& $schedule {$script:BindingRootsHook=$null;$script:TestRoot=$null;$script:SourceHook=$null;$script:SchedulerHook=$null}}
    $childPath=[IO.Path]::Combine($Env.Root,'cleanup-contract-child.ps1')
    [IO.File]::WriteAllText($childPath,@'
param($Runtime,$Hash,$Credential)
$ErrorActionPreference='Stop';$WarningPreference='SilentlyContinue'
$schedule=Import-Module ([IO.Path]::Combine($Runtime,'tools','Artifacts','EvidenceSchedule.psm1')) -PassThru
$manifest=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText([IO.Path]::Combine($Runtime,'runtime.json'))) -AsHashtable -DateKind String
$roots=[ordered]@{};foreach($role in $manifest.storageBinding.roles.Keys){$roots[$role]=$manifest.storageBinding.roles[$role].logicalPath}
& $schedule {param($r,$c)$script:BindingRootsHook={$r}.GetNewClosure();$script:CredentialRootHook={$c}.GetNewClosure()} $roots $Credential
$null=Initialize-EvidenceRuntimeContext $Runtime $Hash
Import-Module ([IO.Path]::Combine($Runtime,'tools','Artifacts','EvidenceStateStore.psm1'))
$cleanup=Import-Module ([IO.Path]::Combine($Runtime,'tools','Artifacts','EvidenceCleanup.psm1')) -PassThru
if(Get-Command Read-EvidenceConfig -ErrorAction SilentlyContinue){throw 'global contract import hides dependency'}
$contract=& $cleanup {Get-Command Read-EvidenceConfig,Assert-EvidenceTaskId -ErrorAction Stop}
if(@($contract|Where-Object {$_.ModuleName -cne 'EvidenceContract'}).Count){throw 'cleanup contract owner mismatch'}
foreach($module in @(Get-Module -All|Where-Object Name -eq 'ArtifactApplication')){& $module {$script:TransportHook={throw 'offline-network-forbidden'}}}
$result=Invoke-EvidenceCleanup ([IO.Path]::Combine($Runtime,'config.json')) $true @('fixture')
if($result.status -cne 'passed' -or $result.blocked -ne 0 -or $result.deleted -ne 0 -or $result.skipped -lt 1){throw ('isolated cleanup failed '+(ConvertTo-Json $result -Compress -Depth 5))}
@{status='passed';importOrder=@('EvidenceSchedule','Initialize-EvidenceRuntimeContext','EvidenceStateStore','EvidenceCleanup');globalContractImported=$false;blocked=$result.blocked;deleted=$result.deleted;skipped=$result.skipped}|ConvertTo-Json -Compress
'@)
    $info=[Diagnostics.ProcessStartInfo]::new([Diagnostics.Process]::GetCurrentProcess().MainModule.FileName);$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($arg in @('-NoProfile','-File',$childPath,'-Runtime',$runtime.runtime,'-Hash',$runtime.manifestSha256,'-Credential',$credential)){$info.ArgumentList.Add($arg)}
    $process=[Diagnostics.Process]::Start($info)
    try{$out=$process.StandardOutput.ReadToEndAsync();$err=$process.StandardError.ReadToEndAsync();Assert-EvidenceTest ($process.WaitForExit(15000)) 'cleanup dependency child deadline';Assert-EvidenceTest ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),5000)) 'cleanup dependency child pipes';Assert-EvidenceTest ($process.ExitCode -eq 0) ('cleanup dependency child failed '+$out.Result+$err.Result);$observation=$out.Result|ConvertFrom-Json;Assert-EvidenceTest ($observation.status -ceq 'passed') 'cleanup dependency observation';if($ResultPath){Write-EvidenceTestJson ($ResultPath+'.fresh-child.json') $observation}}
    finally{if(-not $process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function Set-EvidenceAliasFixture($Env){
    Import-Module (Join-Path $PSScriptRoot '../../Workflow/WindowsStorePaths.psm1')
    $roles=@{}
    foreach($name in @('workflow','evidence','deployments','transfers','runtime')){
        $directory=if($name -ceq 'evidence'){'catalog'}else{$name}
        $physical=[IO.Path]::Combine($Env.Root,$directory)
        if(-not [IO.Directory]::Exists($physical)){[IO.Directory]::CreateDirectory($physical)|Out-Null}
        Set-ArtifactAcl $physical $true
        $identity=Get-WindowsStorePathIdentity $physical
        $roles[$name]=@{logicalPath=[IO.Path]::Combine($Env.Root,'invisible-alias',$directory);physicalPath=$identity.physicalPath;volumeSerial=$identity.volumeSerial;fileId=$identity.fileId}
    }
    $catalog=[IO.Path]::Combine((Get-EvidenceTaskDirectory $Env.Config fixture),'catalog.json')
    $task=(ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($catalog)) -DateKind String)
    foreach($artifact in $task.artifacts){
        foreach($field in @('receipt','putIntent')){if($artifact.$field){$artifact.$field.path=$artifact.$field.path.Replace($roles.evidence.physicalPath,$roles.evidence.logicalPath)}}
        foreach($copy in $artifact.ownedCopies){$copy.path=$copy.path.Replace($roles.transfers.physicalPath,$roles.transfers.logicalPath)}
        if($artifact.operation){$artifact.operation.path=$artifact.operation.path.Replace($roles.transfers.physicalPath,$roles.transfers.logicalPath);if($artifact.operation.childMarker){$artifact.operation.childMarker=$artifact.operation.childMarker.Replace($roles.transfers.physicalPath,$roles.transfers.logicalPath)}}
    }
    foreach($stage in $task.staging){$stage.path=$stage.path.Replace($roles.transfers.physicalPath,$roles.transfers.logicalPath);if($stage.operation){$stage.operation.path=$stage.operation.path.Replace($roles.transfers.physicalPath,$roles.transfers.logicalPath);if($stage.operation.childMarker){$stage.operation.childMarker=$stage.operation.childMarker.Replace($roles.transfers.physicalPath,$roles.transfers.logicalPath)}}}
    Write-EvidenceTestJson $catalog $task
    Set-EvidenceTestValue EvidencePaths TestRoot $null;Set-EvidenceTestValue EvidenceContract DeploymentRoot $null;Set-EvidenceTestValue ArtifactPaths TestRoot $null;Set-EvidenceTestValue TaskEventStore TestRoot $null
    [AppDomain]::CurrentDomain.SetData('OneStarMaker.Evidence.StorageBinding.v1',(ConvertTo-Json -InputObject @{schemaVersion=1;ownerSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value;roles=$roles} -Compress -Depth 8))
    return [pscustomobject]@{Roles=$roles;Catalog=$catalog;Task=$task}
}
foreach($caseName in $selected){$executed.Add($caseName);$child=$null;$signal=$null;try{
    Assert-EvidenceTest ($caseName -cin $registered) 'unknown-case';$env=New-EvidenceTestEnvironment;$fixture=New-EvidenceReaderFixture $env.Root;$selection=[IO.Path]::Combine($env.Root,'selection.json');Write-EvidenceTestJson $selection $fixture.Selection
    $p=Invoke-EvidencePublish $env.ConfigPath $selection;Assert-EvidenceTest ($p.status -ceq 'passed') 'publish';if($caseName -ceq 'fair-object-cursor'){$second=Invoke-EvidencePublish $env.ConfigPath $selection;Assert-EvidenceTest ($second.status -ceq 'passed') 'second publish'};$ref=Read-EvidenceReference $p.reference $env.Config;[IO.File]::WriteAllBytes([IO.Path]::Combine($env.Root,'remote.zip'),$env.Objects[$ref.key])
    $endAt=[DateTimeOffset]::Parse('2026-01-01T00:00:00.0000000+00:00');Set-EvidenceTestValue TaskLifecycle Clock {$endAt}.GetNewClosure();$g=Enter-WorkflowTaskGuard ('a'*64) fixture
    try{if($caseName -cnotin @('active-marker-protected','staging-adoption-success')){$null=Invoke-WorkflowTaskTransition ('a'*64) fixture ended 1 fixture-end completed $g};$t=Sync-EvidenceTaskGuarded $env.Config fixture $g;$a=$t.artifacts[0]}finally{$g.Dispose()}
    $now=$endAt.AddDays(30);Set-EvidenceTestValue EvidenceCleanup Clock {$now}.GetNewClosure();Set-EvidenceTestValue EvidenceApplication Clock {$now}.GetNewClosure()
    switch($caseName){
        'fresh-process-cleanup-contract'{try{Test-FreshCleanupContract $env}finally{Assert-EvidenceTest ([IO.Path]::GetFullPath($env.Root).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\osm-evidence-v2-',[StringComparison]::OrdinalIgnoreCase)) 'cleanup fixture escaped Temp';[IO.Directory]::Delete($env.Root,$true)};continue}
        {$_ -cin @('old-alias-cleanup','old-alias-fetch','old-alias-child-eof-blocked','old-alias-marker-unresolved','old-alias-copy-acl-blocked')}{
            $receiptPath=$a.receipt.path;$receiptHash=(Get-FileHash -LiteralPath $receiptPath).Hash;$intentPath=$a.putIntent.path;$intentHash=(Get-FileHash -LiteralPath $intentPath).Hash
            if($caseName -cin @('old-alias-child-eof-blocked','old-alias-marker-unresolved','old-alias-copy-acl-blocked')){
                $operation=$p.residue.localPath;$marker=[IO.Path]::Combine($operation,'readback-process.json')
                Write-EvidenceTestJson $marker ([pscustomobject]@{process=@{pid=2147483647;startedAt='2000-01-01T00:00:00.0000000Z'};exited=$true;pipesClosed=$false});Set-ArtifactAcl $marker $false
                $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$task=Read-EvidenceTask $env.Config fixture $g;$task.artifacts[0].operation=@{process=@{pid=2147483647;startedAt='2000-01-01T00:00:00.0000000Z'};path=$operation;childMarker=$marker};$null=Write-EvidenceTask $env.Config fixture $task $task.generation $g}finally{$g.Dispose()}
            }
            if($caseName -ceq 'old-alias-copy-acl-blocked'){
                $badCopy=$a.ownedCopies[0].path;$badHash=(Get-FileHash -LiteralPath $badCopy).Hash
                $acl=[IO.FileSystemAclExtensions]::GetAccessControl([IO.FileInfo]::new($badCopy));$acl.SetAccessRuleProtection($false,$true);[IO.FileSystemAclExtensions]::SetAccessControl([IO.FileInfo]::new($badCopy),$acl)
            }
            $alias=Set-EvidenceAliasFixture $env
            if($caseName -ceq 'old-alias-marker-unresolved'){
                $task=(ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($alias.Catalog)) -DateKind String);$task.artifacts[0].operation.childMarker=[IO.Path]::Combine($env.Root,'outside','readback-process.json');Write-EvidenceTestJson $alias.Catalog $task
            }
            $before=($env.Counts.Values|Measure-Object -Sum).Sum
            if($caseName -ceq 'old-alias-fetch'){
                $fetched=Invoke-EvidenceFetch $env.ConfigPath $p.reference $p.packageSha256
                Assert-EvidenceTest ($fetched.status -ceq 'passed' -and $fetched.outputPath.StartsWith($alias.Roles.transfers.physicalPath+'\')) 'fresh copy was not physical'
                $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$task=Read-EvidenceTask $env.Config fixture $g;Assert-EvidenceTest ($task.artifacts[0].receipt.path -ceq $alias.Task.artifacts[0].receipt.path -and $task.artifacts[0].ownedCopies[-1].path.StartsWith($alias.Roles.transfers.physicalPath+'\')) 'alias receipt rewritten or new copy alias'}finally{$g.Dispose()}
            }else{
                $clean=Invoke-EvidenceCleanup $env.ConfigPath
                if($caseName -ceq 'old-alias-cleanup'){Assert-EvidenceTest ($clean.status -ceq 'passed' -and $clean.deleted -eq 1 -and $env.Objects.Count -eq 0) 'alias cleanup failed'}
                else{Assert-EvidenceTest ($clean.status -ceq 'pending' -and $clean.deleted -eq 0 -and $env.Objects.Count -eq 1 -and ($env.Counts.Values|Measure-Object -Sum).Sum -eq $before) 'unknown marker reached network/delete'}
            }
            if($caseName -ceq 'old-alias-copy-acl-blocked'){Assert-EvidenceTest ([IO.File]::Exists($badCopy) -and (Get-FileHash -LiteralPath $badCopy).Hash -ceq $badHash -and -not [IO.FileSystemAclExtensions]::GetAccessControl([IO.FileInfo]::new($badCopy)).AreAccessRulesProtected) 'bad ACL copy repaired or removed'}
            Assert-EvidenceTest ((Get-FileHash -LiteralPath $receiptPath).Hash -ceq $receiptHash -and (Get-FileHash -LiteralPath $intentPath).Hash -ceq $intentHash) 'immutable receipt/intent changed'
            continue
        }
        {$_ -cin @('missing-owned-copy','noncanonical-owned-copy')}{
            $copy=$a.ownedCopies[0];$source=$copy.path
            if($caseName -ceq 'missing-owned-copy'){[IO.File]::Delete($source)}else{
                $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$task=Read-EvidenceTask $env.Config fixture $g;$task.artifacts[0].ownedCopies[0].path=[IO.Path]::Combine([IO.Path]::GetDirectoryName($source),'..',[IO.Path]::GetFileName($source));$null=Write-EvidenceTask $env.Config fixture $task $task.generation $g}finally{$g.Dispose()}
            }
            $before=($env.Counts.Values|Measure-Object -Sum).Sum;$clean=Invoke-EvidenceCleanup $env.ConfigPath
            Assert-EvidenceTest ($clean.status -ceq 'pending' -and $clean.deleted -eq 0 -and $env.Objects.Count -eq 1 -and ($env.Counts.Values|Measure-Object -Sum).Sum -eq $before) 'unknown owned path reached network/delete'
            continue
        }
        'deadline-request-and-guard'{
            $state=@{remaining=1000L};$left={$state.remaining}.GetNewClosure();$budget=[pscustomobject]@{Remaining=$left;HardRemaining=$left};$waits=[Collections.Generic.List[int]]::new();$store=(Get-Command Enter-WorkflowTaskGuard).Module
            $hold=Enter-WorkflowTaskGuard ('a'*64) fixture
            try{
                & $store {param($b,$h)$script:Budget=$b;$script:GuardWaitHook=$h} $budget {param($ms)$waits.Add($ms);$state.remaining-=$ms}.GetNewClosure()
                $rejected=$false;try{$unexpected=Enter-WorkflowTaskGuard ('a'*64) fixture;$unexpected.Dispose()}catch{$rejected=$true}
                Assert-EvidenceTest ($rejected -and $state.remaining -eq 0 -and ($waits|Measure-Object -Sum).Sum -eq 1000) 'guard restarted thirty-second allowance'
            }finally{& $store {$script:Budget=$null;$script:GuardWaitHook=$null};$hold.Dispose()}
            $state.remaining=1000;$captured=[Collections.Generic.List[long]]::new();$inner=$env.Transport
            $hook={param($gen,$req)$captured.Add([long]$req.DeadlineMilliseconds);if($req.Operation -ceq 'delete'){$state.remaining=0;return [pscustomobject]@{Operation='delete';Generation=$gen;StatusClass='timeout';HttpStatus=$null;S3Code=$null;Bytes=$null;Sha256=$null;StatusObserved=$false;TimedOut=$true;Redirected=$false}};& $inner $gen $req}.GetNewClosure()
            Set-EvidenceTestValue ArtifactApplication TransportHook $hook;Set-EvidenceTestValue EvidenceCleanup Budget $budget
            $clean=Invoke-EvidenceCleanup $env.ConfigPath;Assert-EvidenceTest ($clean.status -ceq 'pending' -and $captured.Count -eq 1 -and $captured[0] -eq 1000 -and $env.Objects.Count -eq 1) 'request acquired fresh network allowance'
            Set-EvidenceTestValue EvidenceCleanup Budget $null;$g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$task=Read-EvidenceTask $env.Config fixture $g;Assert-EvidenceTest ($null -ne $task.artifacts[0].deleteIntent -and $task.artifacts[0].state -ceq 'delete-pending') 'deadline lost durable intent'}finally{$g.Dispose()}
            Set-EvidenceTestValue ArtifactApplication Budget $budget;$timer=[Diagnostics.Stopwatch]::StartNew();$rejected=$false;try{$req=New-ArtifactRequest $env.Config delete $ref.key $null $null $timer;$null=Invoke-ArtifactNetwork ('f'*32) $req}catch{$rejected=$true};Assert-EvidenceTest ($rejected -and $captured.Count -eq 1) 'delete started after deadline'
            $state.remaining=1000;$rejected=$false;try{$null=Invoke-ArtifactReadback $p.residue.localPath $env.Config ('f'*32) $ref.key $a.packageBytes $a.packageSha256 $timer}catch{$rejected=$true};Assert-EvidenceTest ($rejected -and $captured.Count -eq 1) 'readback started without stop reservation'
            Set-EvidenceTestValue ArtifactApplication Budget $null
            continue
        }
        'deadline-atomic-ack-retry'{
            $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$task=Read-EvidenceTask $env.Config fixture $g;$oldGeneration=$task.generation;$null=Invoke-WorkflowTaskTransition ('a'*64) fixture resumed 2 deadline-resume '' $g}finally{$g.Dispose()}
            $state=@{remaining=1000L};$left={$state.remaining}.GetNewClosure();$budget=[pscustomobject]@{Remaining=$left;HardRemaining=$left};Set-EvidenceTestValue EvidenceStateStore Budget $budget
            Set-EvidenceTestValue EvidencePaths WriteHook {param($point,$path)if($point -ceq 'after-rename'){$state.remaining=0}}.GetNewClosure()
            $sync=Invoke-EvidenceSync $env.ConfigPath fixture;Assert-EvidenceTest ($sync.status -ceq 'pending') 'expired commit reported delivery success'
            Set-EvidenceTestValue EvidencePaths WriteHook $null;Set-EvidenceTestValue EvidenceStateStore Budget $null
            $g=Enter-WorkflowTaskGuard ('a'*64) fixture
            try{$task=Read-EvidenceTask $env.Config fixture $g;$source=Read-WorkflowTaskState ('a'*64) fixture $g;Assert-EvidenceTest ($task.generation -eq $oldGeneration+1 -and -not $source.outbox[2].delivered -and $source.events[1].occurredAt -ceq $endAt.ToString('o')) 'expired commit lost state or acknowledged new event'}finally{$g.Dispose()}
            $sync=Invoke-EvidenceSync $env.ConfigPath fixture;Assert-EvidenceTest ($sync.status -ceq 'passed') 'deadline acknowledgement retry failed'
            $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$task=Read-EvidenceTask $env.Config fixture $g;$source=Read-WorkflowTaskState ('a'*64) fixture $g;Assert-EvidenceTest ($task.status -ceq 'active' -and $source.outbox[2].delivered -and $source.events[1].occurredAt -ceq $endAt.ToString('o')) 'retry rewrote original end clock'}finally{$g.Dispose()}
            continue
        }
        'staging-result-accounting'{
            $stages=@();$outside=[IO.Path]::Combine($env.Root,'accounting-outside');[IO.Directory]::CreateDirectory($outside)|Out-Null;[IO.File]::WriteAllText([IO.Path]::Combine($outside,'marker'),'keep')
            foreach($mode in @('expired','retained','child','reparse')){
                $path=[IO.Path]::Combine($env.Root,'transfers',[Guid]::NewGuid().ToString('N'));[IO.Directory]::CreateDirectory($path)|Out-Null;Set-ArtifactAcl $path $true;[IO.File]::WriteAllText([IO.Path]::Combine($path,'failure.log'),'storage bytes')
                $operation=$null
                if($mode -ceq 'child'){$marker=[IO.Path]::Combine($path,'readback-process.json');Write-EvidenceTestJson $marker ([pscustomobject]@{process=Get-EvidenceProcessIdentity;exited=$false;pipesClosed=$false});Set-ArtifactAcl $marker $false;$operation=[pscustomobject]@{process=[pscustomobject]@{pid=2147483647;startedAt='2000-01-01T00:00:00.0000000Z'};childMarker=$marker;path=$path}}
                if($mode -ceq 'reparse'){New-Item -ItemType Junction -Path ([IO.Path]::Combine($path,'junction')) -Target $outside|Out-Null}
                $stages+=[pscustomobject]@{path=$path;createdAt=$(if($mode -ceq 'retained'){$now}else{$now.AddDays(-7)}).ToString('o');origin='storage-copy';artifactId=$null;adopted=$false;adoptionPending=$false;operation=$operation;deleteIntent=$null;protections=@()}
            }
            $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$t=Read-EvidenceTask $env.Config fixture $g;$t.artifacts=@();$t.staging=$stages;$t.cleanupCursor=$null;$null=Write-EvidenceTask $env.Config fixture $t $t.generation $g}finally{$g.Dispose()}
            $before=($env.Counts.Values|Measure-Object -Sum).Sum;$dry=Invoke-EvidenceCleanup $env.ConfigPath $true
            Assert-EvidenceTest ($dry.items.Count -eq 4 -and $dry.deleted -eq 0) 'dry stage accounting';foreach($stage in $stages){Assert-EvidenceTest ([IO.Directory]::Exists($stage.path)) 'dry stage removed'}
            $clean=Invoke-EvidenceCleanup $env.ConfigPath
            Assert-EvidenceTest ($clean.status -ceq 'pending' -and $clean.items.Count -eq 4 -and $clean.deleted -eq 1 -and $clean.skipped -eq 1 -and $clean.blocked -eq 2) 'stage outcome accounting'
            foreach($item in $clean.items){Assert-EvidenceTest ((@($item.PSObject.Properties.Name|Sort-Object) -join ',') -ceq 'artifactId,reasonCode,taskId' -and $null -eq $item.artifactId) 'stage item schema changed'}
            Assert-EvidenceTest ([IO.Directory]::Exists($stages[2].path) -and [IO.File]::Exists([IO.Path]::Combine($outside,'marker')) -and ($env.Counts.Values|Measure-Object -Sum).Sum -eq $before) 'stage safety or network isolation'
            # Three real owned staging directories progress over independent one-object
            # jobs; each result reports precisely the consumed reservation and remainder.
            $stages=@(1..3|ForEach-Object{$path=[IO.Path]::Combine($env.Root,'transfers',[Guid]::NewGuid().ToString('N'));[IO.Directory]::CreateDirectory($path)|Out-Null;Set-ArtifactAcl $path $true;[pscustomobject]@{path=$path;createdAt=$now.AddDays(-7).ToString('o');origin='storage-copy';artifactId=$null;adopted=$false;adoptionPending=$false;operation=$null;deleteIntent=$null;protections=@()}})
            $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$t=Read-EvidenceTask $env.Config fixture $g;$t.staging=$stages;$t.cleanupCursor=$null;$null=Write-EvidenceTask $env.Config fixture $t $t.generation $g}finally{$g.Dispose()}
            foreach($remaining in @(2,1,0)){$clean=Invoke-EvidenceCleanup $env.ConfigPath $false @() 1;Assert-EvidenceTest ($clean.items.Count -eq 1 -and $clean.deleted -eq 1 -and $clean.remaining -eq $remaining -and $clean.status -ceq $(if($remaining){'pending'}else{'passed'})) 'stage bounded cursor accounting'}
            foreach($stage in $stages){Assert-EvidenceTest (-not [IO.Directory]::Exists($stage.path)) 'stage cursor starvation'}
            foreach($name in $fixture.Files){Assert-EvidenceTest ([IO.File]::Exists([IO.Path]::Combine($fixture.Source,$name))) 'stage accounting deleted source'}
            continue
        }
        'stopped-operation-cleanup'{
            $g=Enter-WorkflowTaskGuard ('a'*64) fixture
            try{$t=Read-EvidenceTask $env.Config fixture $g;$t.artifacts[0].operation=[pscustomobject]@{process=[pscustomobject]@{pid=2147483647;startedAt='2000-01-01T00:00:00.0000000Z'};childMarker=$null;path=$p.residue.localPath};$null=Write-EvidenceTask $env.Config fixture $t $t.generation $g}finally{$g.Dispose()}
            $first=Invoke-EvidenceCleanup $env.ConfigPath;$second=Invoke-EvidenceCleanup $env.ConfigPath
            Assert-EvidenceTest ($first.status -ceq 'passed' -and $first.deleted -eq 1 -and $second.status -ceq 'passed' -and $env.Objects.Count -eq 0) 'stopped fetch retained forever'
            $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{Assert-EvidenceTest ($null -eq (Read-EvidenceTask $env.Config fixture $g).artifacts[0].operation) 'stopped operation not saved'}finally{$g.Dispose()}
            continue
        }
        'recovery-staging-cleanup'{
            Set-EvidenceTestValue EvidenceCleanup FaultHook {param($stage,$a)if($stage -ceq 'after-intent'){throw 'simulated-crash'}}
            $first=Invoke-EvidenceCleanup $env.ConfigPath;Assert-EvidenceTest ($first.status -ceq 'pending') 'intent crash not pending'
            Set-EvidenceTestValue EvidenceCleanup FaultHook $null;Set-EvidenceTestValue EvidenceCleanup ProcessHook {$true}
            $before=@([IO.Directory]::EnumerateDirectories([IO.Path]::Combine($env.Root,'transfers')))
            $clean=Invoke-EvidenceCleanup $env.ConfigPath;Assert-EvidenceTest ($clean.status -ceq 'passed' -and $env.Objects.Count -eq 0) 'delete reconciliation failed'
            $new=@([IO.Directory]::EnumerateDirectories([IO.Path]::Combine($env.Root,'transfers'))|Where-Object {$_ -cnotin $before});Assert-EvidenceTest ($new.Count -eq 2) 'expected reconcile and absence operations'
            $g=Enter-WorkflowTaskGuard ('a'*64) fixture
            try{$t=Read-EvidenceTask $env.Config fixture $g;foreach($path in $new){Assert-EvidenceTest (@($t.staging|Where-Object path -CEQ $path).Count -eq 1) 'recovery directory unregistered'};Assert-EvidenceTest ([IO.File]::Exists([IO.Path]::Combine($new[0],'reconcile.bin')) -or [IO.File]::Exists([IO.Path]::Combine($new[1],'reconcile.bin'))) 'recovery did not contain real bytes'}finally{$g.Dispose()}
            $now=$now.AddDays(8);Set-EvidenceTestValue EvidenceCleanup Clock {$now}.GetNewClosure();$later=Invoke-EvidenceCleanup $env.ConfigPath
            Assert-EvidenceTest ($later.status -ceq 'passed') 'recovery stage cleanup failed';foreach($path in $new){Assert-EvidenceTest (-not [IO.Directory]::Exists($path)) 'recovery bytes survived seven-day rule'}
            foreach($name in $fixture.Files){Assert-EvidenceTest ([IO.File]::Exists([IO.Path]::Combine($fixture.Source,$name))) 'recovery deleted source'}
            continue
        }
        'corrupt-lifecycle-projection'{
            # Parse-valid changes to either projection, not only malformed JSON, must
            # prevent any remote request before the authoritative thirty-day deadline.
            $catalog=[IO.Path]::Combine((Get-EvidenceTaskDirectory $env.Config fixture),'catalog.json');$original=[IO.File]::ReadAllText($catalog)
            foreach($mutation in @('both-clock','task-clock','artifact-clock','task-reason','artifact-reason','artifact-version','event-hash','event-list')){
                $t=$original|ConvertFrom-Json -DateKind String
                if($mutation -cin @('both-clock','task-clock')){$t.taskEndedAt='2025-01-01T00:00:00.0000000+00:00';$t.deleteEligibleAt='2025-01-31T00:00:00.0000000+00:00'}
                if($mutation -cin @('both-clock','artifact-clock')){$t.artifacts[0].taskEndedAt='2025-01-01T00:00:00.0000000+00:00';$t.artifacts[0].deleteEligibleAt='2025-01-31T00:00:00.0000000+00:00'}
                if($mutation -ceq 'task-reason'){$t.endReason='cancelled'}
                if($mutation -ceq 'artifact-reason'){$t.artifacts[0].endReason='cancelled'}
                if($mutation -ceq 'artifact-version'){$t.artifacts[0].appliedEventVersion=1}
                if($mutation -ceq 'event-hash'){$t.events[0].sha256='0'*64}
                if($mutation -ceq 'event-list'){$t.events+= $t.events[0]}
                Write-EvidenceTestJson $catalog $t;$now=$endAt.AddDays(1);Set-EvidenceTestValue EvidenceCleanup Clock {$now}.GetNewClosure();$beforeRequests=($env.Counts.Values|Measure-Object -Sum).Sum
                $clean=Invoke-EvidenceCleanup $env.ConfigPath
                Assert-EvidenceTest ($clean.status -ceq 'pending' -and $clean.deleted -eq 0 -and $env.Objects.Count -eq 1 -and ($env.Counts.Values|Measure-Object -Sum).Sum -eq $beforeRequests) ('corrupt projection accepted '+$mutation)
            }
            [IO.File]::WriteAllText($catalog,$original);$g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$source=Read-WorkflowTaskState ('a'*64) fixture $g;Assert-EvidenceTest ($source.taskEndedAt -ceq $endAt.ToString('o') -and $source.endReason -ceq 'completed') 'workflow clock changed'}finally{$g.Dispose()}
            continue
        }
        'staging-fair-cursor'{
            $g=Enter-WorkflowTaskGuard ('a'*64) fixture
            try{
                $t=Read-EvidenceTask $env.Config fixture $g;$template=$t.artifacts[0];$receiptTemplate=[IO.File]::ReadAllText($template.receipt.path)|ConvertFrom-Json -DateKind String;$t.artifacts=@();$t.staging=@()
                foreach($number in 1..3){
                    $copy=ConvertTo-Json -InputObject $template -Depth 24|ConvertFrom-Json -DateKind String;$copy.artifactId=$number.ToString('x').PadLeft(32,'0');$copy.key=Get-EvidenceKey $env.Config fixture $copy.artifactId;$copy.state='deleted';$copy.ownedCopies=@();$copy.putIntent=$null
                    $receipt=ConvertTo-Json -InputObject $receiptTemplate -Depth 24|ConvertFrom-Json -DateKind String;$receipt.artifactId=$copy.artifactId;$receipt.key=$copy.key;$receiptPath=[IO.Path]::Combine((Get-EvidenceTaskDirectory $env.Config fixture),'receipts',$copy.artifactId+'.json');$receiptHash=Write-EvidenceAtomic $receiptPath $receipt -Immutable;$copy.receipt=[pscustomobject]@{path=$receiptPath;sha256=$receiptHash};$t.artifacts+= $copy
                }
                $stagePath=[IO.Path]::Combine($env.Root,'transfers',[Guid]::NewGuid().ToString('N'));[IO.Directory]::CreateDirectory($stagePath)|Out-Null;Set-ArtifactAcl $stagePath $true;[IO.File]::WriteAllText([IO.Path]::Combine($stagePath,'failure.log'),'failed storage bytes')
                $stage=[pscustomobject]@{path=$stagePath;createdAt=$now.AddDays(-7).ToString('o');origin='storage-copy';artifactId=$null;adopted=$false;adoptionPending=$false;operation=$null;deleteIntent=$null;protections=@()};$t.staging=@($stage);$null=Write-EvidenceTask $env.Config fixture $t $t.generation $g
            }finally{$g.Dispose()}
            $first=Invoke-EvidenceCleanup $env.ConfigPath $false @() 3;$second=Invoke-EvidenceCleanup $env.ConfigPath $false @() 1
            # Three tombstones fill the first job's object cap. The next job
            # must continue at staging instead of walking those tombstones again.
            # Scheduler tests separately cover the global 100-object upper bound.
            if($ResultPath){Write-EvidenceTestJson ($ResultPath+'.fair.json') ([ordered]@{first=$first;second=$second;stagingExists=[IO.Directory]::Exists($stagePath)})}
            Assert-EvidenceTest ($first.remaining -eq 1 -and $first.items.Count -eq 3 -and $first.deleted -eq 0 -and [IO.Directory]::Exists([IO.Path]::Combine($env.Root,'transfers')) -and $second.deleted -eq 1 -and -not [IO.Directory]::Exists($stagePath)) 'tombstones starved staging'
            foreach($name in $fixture.Files){Assert-EvidenceTest ([IO.File]::Exists([IO.Path]::Combine($fixture.Source,$name))) 'fair cursor changed source'}
            # A failed first staging tree must also yield the next bounded job.
            $paths=@(1..2|ForEach-Object{$path=[IO.Path]::Combine($env.Root,'transfers',[Guid]::NewGuid().ToString('N'));[IO.Directory]::CreateDirectory($path)|Out-Null;Set-ArtifactAcl $path $true;$path});$stages=@($paths|ForEach-Object{[pscustomobject]@{path=$_;createdAt=$now.AddDays(-7).ToString('o');origin='storage-copy';artifactId=$null;adopted=$false;adoptionPending=$false;operation=$null;deleteIntent=$null;protections=@()}})
            $ordered=@($stages|Sort-Object {[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($_.path))).ToLowerInvariant()});$outside=[IO.Path]::Combine($env.Root,'outside-stage');[IO.Directory]::CreateDirectory($outside)|Out-Null;[IO.File]::WriteAllText([IO.Path]::Combine($outside,'marker'),'keep');New-Item -ItemType Junction -Path ([IO.Path]::Combine($ordered[0].path,'junction')) -Target $outside|Out-Null
            $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$t=Read-EvidenceTask $env.Config fixture $g;$t.artifacts=@();$t.staging=$stages;$t.cleanupCursor=$null;$null=Write-EvidenceTask $env.Config fixture $t $t.generation $g}finally{$g.Dispose()}
            $first=Invoke-EvidenceCleanup $env.ConfigPath $false @() 1;$second=Invoke-EvidenceCleanup $env.ConfigPath $false @() 1
            Assert-EvidenceTest ($first.blocked -eq 1 -and $second.deleted -eq 1 -and [IO.Directory]::Exists($ordered[0].path) -and -not [IO.Directory]::Exists($ordered[1].path) -and [IO.File]::Exists([IO.Path]::Combine($outside,'marker'))) 'failed stage cursor did not advance'
            continue
        }
        'staging-adoption-success'{
            $owned=[IO.Path]::Combine($env.Root,'transfers',[Guid]::NewGuid().ToString('N'));[IO.Directory]::CreateDirectory($owned)|Out-Null;Set-ArtifactAcl $owned $true;[IO.File]::WriteAllText([IO.Path]::Combine($owned,'failure.log'),'ordinary failure log')
            $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$t=Read-EvidenceTask $env.Config fixture $g;$t.staging+=[pscustomobject]@{path=$owned;createdAt=$now.AddDays(-7).ToString('o');origin='storage-copy';artifactId=$null;adopted=$false;adoptionPending=$false;operation=$null;deleteIntent=$null;protections=@()};$null=Write-EvidenceTask $env.Config fixture $t $t.generation $g}finally{$g.Dispose()}
            $adoptSelection=[ordered]@{schemaVersion=2;purpose='evidence';taskId='fixture';root=$owned;files=@('failure.log');base='b'*40;head='c'*40};$adoptPath=[IO.Path]::Combine($env.Root,'adopt.json');Write-EvidenceTestJson $adoptPath $adoptSelection;$adopt=Invoke-EvidencePublish $env.ConfigPath $adoptPath;Assert-EvidenceTest ($adopt.status -ceq 'passed') 'adoption publish'
            $clean=Invoke-EvidenceCleanup $env.ConfigPath;Assert-EvidenceTest (-not [IO.Directory]::Exists($owned)) 'old staging cleanup';$fetch=Invoke-EvidenceFetch $env.ConfigPath $adopt.reference $adopt.packageSha256;Assert-EvidenceTest ($fetch.status -ceq 'passed' -and [IO.File]::ReadAllText([IO.Path]::Combine($fetch.outputPath,'failure.log')) -ceq 'ordinary failure log') 'adopted evidence gone';break
        }
        'child-not-stopped'{
            $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$t=Read-EvidenceTask $env.Config fixture $g;$childMarker=[IO.Path]::Combine($env.Root,'child.json');Write-EvidenceTestJson $childMarker ([pscustomobject]@{process=Get-EvidenceProcessIdentity;exited=$false;pipesClosed=$false});$t.artifacts[0].operation=[pscustomobject]@{process=@{pid=2147483647;startedAt='2000-01-01T00:00:00.0000000Z'};childMarker=$childMarker;path=$p.residue.localPath};$null=Write-EvidenceTask $env.Config fixture $t $t.generation $g;$safe=$false;try{$null=Assert-EvidenceTransitionSafe $env.Config fixture $g;$safe=$true}catch{};Assert-EvidenceTest (-not $safe) 'live child released';Assert-EvidenceTest ((Read-WorkflowTaskState ('a'*64) fixture $g).version -eq 2) 'unknown changed version'}finally{$g.Dispose()}
            $use=Invoke-EvidenceUse $env.ConfigPath $p.reference reader $now.AddHours(1).ToString('o') review;Assert-EvidenceTest ($use.status -ceq 'failed') 'live child use';$clean=Invoke-EvidenceCleanup $env.ConfigPath;Assert-EvidenceTest ($clean.status -ceq 'pending' -and $clean.deleted -eq 0 -and $env.Objects.Count -eq 1) 'live child cleanup';break
        }
        'corrupt-catalog-non-delete'{$catalog=[IO.Path]::Combine((Get-EvidenceTaskDirectory $env.Config fixture),'catalog.json');[IO.File]::WriteAllText($catalog,'{broken')}
        'delete-timeout'{$inner=$env.Transport;$hook={param($gen,$req)if($req.Operation -ceq 'delete'){return [pscustomobject]@{Operation='delete';Generation=$gen;StatusClass='timeout';HttpStatus=$null;S3Code=$null;Bytes=$null;Sha256=$null;StatusObserved=$false;TimedOut=$true;Redirected=$false}};& $inner $gen $req}.GetNewClosure();Set-EvidenceTestValue ArtifactApplication TransportHook $hook}
        'delete-forbidden'{$inner=$env.Transport;$hook={param($gen,$req)if($req.Operation -ceq 'delete'){return [pscustomobject]@{Operation='delete';Generation=$gen;StatusClass='forbidden';HttpStatus=403;S3Code='AccessDenied';Bytes=$null;Sha256=$null;StatusObserved=$true;TimedOut=$false;Redirected=$false}};& $inner $gen $req}.GetNewClosure();Set-EvidenceTestValue ArtifactApplication TransportHook $hook}
        {$_ -cin @('delete-crash-reconcile','delete-after-remote-reconcile')}{$faultStage=if($caseName -ceq 'delete-crash-reconcile'){'after-intent'}else{'after-remote'};Set-EvidenceTestValue EvidenceCleanup FaultHook {param($stage,$a)if($stage -ceq $faultStage){throw 'simulated-crash'}}.GetNewClosure()}
        'cleanup-partial'{$copy=$a.ownedCopies[0];[IO.File]::AppendAllText($copy.path,'changed')}
        'use-delete-race'{$use=Invoke-EvidenceUse $env.ConfigPath $p.reference reader $now.AddHours(1).ToString('o') review;Assert-EvidenceTest ($use.status -ceq 'passed') 'use';$child=Start-EvidenceChild $env $p cleanup '' $now.ToString('o');$c=Complete-EvidenceChild $child;Assert-EvidenceTest ($c.exit -eq 0 -and $c.output -match 'in-use') 'use-first cleanup';break}
        'resume-delete-race'{$g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$null=Assert-EvidenceTransitionSafe $env.Config fixture $g;$null=Invoke-WorkflowTaskTransition ('a'*64) fixture resumed 2 fixture-resume '' $g}finally{$g.Dispose()};$child=Start-EvidenceChild $env $p cleanup '' $now.ToString('o');$c=Complete-EvidenceChild $child;Assert-EvidenceTest ($c.exit -eq 0 -and $c.output -match 'active/end-not-recorded') 'resume-first cleanup';break}
        {$_ -cin @('delete-use-race','delete-resume-race')}{$name='Local\OSM-Evidence-Race-'+[Guid]::NewGuid().ToString('N');$signal=[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,$name);$inner=$env.Transport;$action=if($caseName -ceq 'delete-use-race'){'use'}else{'resume'};$state=@{child=$null};$hook={param($gen,$req)if($req.Operation -ceq 'delete'){$state.child=Start-EvidenceChild $env $p $action $name $now.ToString('o');if(-not $signal.WaitOne(10000)){throw 'child barrier missing'}};& $inner $gen $req}.GetNewClosure();Set-EvidenceTestValue ArtifactApplication TransportHook $hook}
        'reparse-protected'{$target=[IO.Path]::Combine($env.Root,'outside');[IO.Directory]::CreateDirectory($target)|Out-Null;[IO.File]::WriteAllText([IO.Path]::Combine($target,'marker'),'keep');$link=[IO.Path]::Combine($env.Root,'transfers','junction');New-Item -ItemType Junction -Path $link -Target $target|Out-Null;$g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$t=Read-EvidenceTask $env.Config fixture $g;$t.artifacts[0].ownedCopies+= [pscustomobject]@{path=[IO.Path]::Combine($link,'marker');bytes=4;sha256=(Get-FileHash -LiteralPath ([IO.Path]::Combine($target,'marker'))).Hash.ToLowerInvariant();origin='storage-copy'};$null=Write-EvidenceTask $env.Config fixture $t $t.generation $g}finally{$g.Dispose()}}
        {$_ -cin @('staging-cleanup','staging-transfer-protected')}{$stagePath=[IO.Path]::Combine($env.Root,'transfers',[Guid]::NewGuid().ToString('N'));[IO.Directory]::CreateDirectory($stagePath)|Out-Null;Set-ArtifactAcl $stagePath $true;[IO.File]::WriteAllText([IO.Path]::Combine($stagePath,'log'),'failure');$g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$t=Read-EvidenceTask $env.Config fixture $g;$t.staging+=[pscustomobject]@{path=$stagePath;createdAt=$now.AddDays(-7).ToString('o');origin='storage-copy';artifactId=$null;adopted=$false;adoptionPending=$false;operation=if($caseName -ceq 'staging-transfer-protected'){@{process=Get-EvidenceProcessIdentity;childMarker=$null;path=$stagePath}}else{$null};deleteIntent=$null;protections=@()};$null=Write-EvidenceTask $env.Config fixture $t $t.generation $g}finally{$g.Dispose()}}
        'fair-object-cursor'{$g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$t=Read-EvidenceTask $env.Config fixture $g;$first=@($t.artifacts|Sort-Object artifactId)[0];$first.protections=@([pscustomobject]@{owner='owner';consumer='blocked';reason='review';createdAt=$endAt.ToString('o');until=$now.AddDays(1).ToString('o');releasedAt=$null});$null=Write-EvidenceTask $env.Config fixture $t $t.generation $g}finally{$g.Dispose()}}
    }
    if($caseName -cin @('use-delete-race','resume-delete-race','staging-adoption-success','child-not-stopped','stopped-operation-cleanup','recovery-staging-cleanup','corrupt-lifecycle-projection','staging-fair-cursor','staging-result-accounting','deadline-request-and-guard','deadline-atomic-ack-retry','old-alias-cleanup','old-alias-fetch','old-alias-child-eof-blocked','old-alias-marker-unresolved','old-alias-copy-acl-blocked','missing-owned-copy','noncanonical-owned-copy','fresh-process-cleanup-contract')){continue}
    $r=Invoke-EvidenceCleanup $env.ConfigPath ($caseName -ceq 'dry-run-no-delete') @() $(if($caseName -ceq 'fair-object-cursor'){1}else{100})
    switch($caseName){
        {$_ -cin @('old-alias-cleanup','old-alias-fetch','old-alias-child-eof-blocked','old-alias-marker-unresolved','old-alias-copy-acl-blocked')}{
            $receiptPath=$a.receipt.path;$receiptHash=(Get-FileHash -LiteralPath $receiptPath).Hash;$intentPath=$a.putIntent.path;$intentHash=(Get-FileHash -LiteralPath $intentPath).Hash
            if($caseName -cin @('old-alias-child-eof-blocked','old-alias-marker-unresolved','old-alias-copy-acl-blocked')){
                $operation=$p.residue.localPath;$marker=[IO.Path]::Combine($operation,'readback-process.json')
                Write-EvidenceTestJson $marker ([pscustomobject]@{process=@{pid=2147483647;startedAt='2000-01-01T00:00:00.0000000Z'};exited=$true;pipesClosed=$false});Set-ArtifactAcl $marker $false
                $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$task=Read-EvidenceTask $env.Config fixture $g;$task.artifacts[0].operation=@{process=@{pid=2147483647;startedAt='2000-01-01T00:00:00.0000000Z'};path=$operation;childMarker=$marker};$null=Write-EvidenceTask $env.Config fixture $task $task.generation $g}finally{$g.Dispose()}
            }
            if($caseName -ceq 'old-alias-copy-acl-blocked'){
                $badCopy=$a.ownedCopies[0].path;$badHash=(Get-FileHash -LiteralPath $badCopy).Hash
                $acl=[IO.FileSystemAclExtensions]::GetAccessControl([IO.FileInfo]::new($badCopy));$acl.SetAccessRuleProtection($false,$true);[IO.FileSystemAclExtensions]::SetAccessControl([IO.FileInfo]::new($badCopy),$acl)
            }
            $alias=Set-EvidenceAliasFixture $env
            if($caseName -ceq 'old-alias-marker-unresolved'){
                $task=(ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($alias.Catalog)) -DateKind String);$task.artifacts[0].operation.childMarker=[IO.Path]::Combine($env.Root,'outside','readback-process.json');Write-EvidenceTestJson $alias.Catalog $task
            }
            $before=($env.Counts.Values|Measure-Object -Sum).Sum
            if($caseName -ceq 'old-alias-fetch'){
                $fetched=Invoke-EvidenceFetch $env.ConfigPath $p.reference $p.packageSha256
                Assert-EvidenceTest ($fetched.status -ceq 'passed' -and $fetched.outputPath.StartsWith($alias.Roles.transfers.physicalPath+'\')) 'fresh copy was not physical'
                $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$task=Read-EvidenceTask $env.Config fixture $g;Assert-EvidenceTest ($task.artifacts[0].receipt.path -ceq $alias.Task.artifacts[0].receipt.path -and $task.artifacts[0].ownedCopies[-1].path.StartsWith($alias.Roles.transfers.physicalPath+'\')) 'alias receipt rewritten or new copy alias'}finally{$g.Dispose()}
            }else{
                $clean=Invoke-EvidenceCleanup $env.ConfigPath
                if($caseName -ceq 'old-alias-cleanup'){Assert-EvidenceTest ($clean.status -ceq 'passed' -and $clean.deleted -eq 1 -and $env.Objects.Count -eq 0) 'alias cleanup failed'}
                else{Assert-EvidenceTest ($clean.status -ceq 'pending' -and $clean.deleted -eq 0 -and $env.Objects.Count -eq 1 -and ($env.Counts.Values|Measure-Object -Sum).Sum -eq $before) 'unknown marker reached network/delete'}
            }
            if($caseName -ceq 'old-alias-copy-acl-blocked'){Assert-EvidenceTest ([IO.File]::Exists($badCopy) -and (Get-FileHash -LiteralPath $badCopy).Hash -ceq $badHash -and -not [IO.FileSystemAclExtensions]::GetAccessControl([IO.FileInfo]::new($badCopy)).AreAccessRulesProtected) 'bad ACL copy repaired or removed'}
            Assert-EvidenceTest ((Get-FileHash -LiteralPath $receiptPath).Hash -ceq $receiptHash -and (Get-FileHash -LiteralPath $intentPath).Hash -ceq $intentHash) 'immutable receipt/intent changed'
            continue
        }
        {$_ -cin @('missing-owned-copy','noncanonical-owned-copy')}{
            $copy=$a.ownedCopies[0];$source=$copy.path
            if($caseName -ceq 'missing-owned-copy'){[IO.File]::Delete($source)}else{
                $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$task=Read-EvidenceTask $env.Config fixture $g;$task.artifacts[0].ownedCopies[0].path=[IO.Path]::Combine([IO.Path]::GetDirectoryName($source),'..',[IO.Path]::GetFileName($source));$null=Write-EvidenceTask $env.Config fixture $task $task.generation $g}finally{$g.Dispose()}
            }
            $before=($env.Counts.Values|Measure-Object -Sum).Sum;$clean=Invoke-EvidenceCleanup $env.ConfigPath
            Assert-EvidenceTest ($clean.status -ceq 'pending' -and $clean.deleted -eq 0 -and $env.Objects.Count -eq 1 -and ($env.Counts.Values|Measure-Object -Sum).Sum -eq $before) 'unknown owned path reached network/delete'
            continue
        }
        {$_ -cin @('delete-timeout','delete-forbidden')}{Assert-EvidenceTest ($r.status -ceq 'pending' -and $env.Objects.Count -eq 1) 'delete uncertainty success'}
        {$_ -cin @('delete-crash-reconcile','delete-after-remote-reconcile')}{Assert-EvidenceTest ($r.status -ceq 'pending') 'crash success';Set-EvidenceTestValue EvidenceCleanup FaultHook $null;Set-EvidenceTestValue EvidenceCleanup ProcessHook {$true};$again=Invoke-EvidenceCleanup $env.ConfigPath;Assert-EvidenceTest ($again.status -ceq 'passed' -and $env.Objects.Count -eq 0) 'reconciliation'}
        'cleanup-partial'{Assert-EvidenceTest ($r.status -ceq 'pending' -and $env.Objects.Count -eq 1 -and [IO.File]::Exists($copy.path)) 'partial local'}
        'active-marker-protected'{Assert-EvidenceTest ($r.deleted -eq 0 -and $env.Objects.Count -eq 1) 'active deleted'}
        'corrupt-catalog-non-delete'{Assert-EvidenceTest ($r.status -ceq 'pending' -and $env.Objects.Count -eq 1) 'corrupt delete'}
        'dry-run-no-delete'{Assert-EvidenceTest ($r.deleted -eq 0 -and $env.Objects.Count -eq 1) 'dry mutation'}
        'delete-use-race'{$child=$state.child;$c=Complete-EvidenceChild $child;Assert-EvidenceTest ($r.deleted -eq 1 -and $c.exit -eq 1 -and $c.output -match 'payload-deleted') ('delete-first use '+(ConvertTo-Json $r -Compress -Depth 8)+' '+(ConvertTo-Json $c -Compress))}
        'delete-resume-race'{$child=$state.child;$c=Complete-EvidenceChild $child;Assert-EvidenceTest ($r.deleted -eq 1 -and $c.exit -eq 0 -and $c.output -match '"version":3' -and $c.output -match $ref.artifactId) 'delete-first resume'}
        'source-marker-protected'{foreach($name in $fixture.Files){Assert-EvidenceTest ([IO.File]::Exists([IO.Path]::Combine($fixture.Source,$name))) 'source deleted'}}
        'reparse-protected'{Assert-EvidenceTest ([IO.File]::Exists([IO.Path]::Combine($target,'marker'))) 'reparse traversed';Assert-EvidenceTest ($r.status -ceq 'pending') 'reparse ignored'}
        'staging-cleanup'{Assert-EvidenceTest (-not [IO.Directory]::Exists($stagePath)) 'staging remained'}
        'staging-transfer-protected'{Assert-EvidenceTest ([IO.Directory]::Exists($stagePath)) 'live staging deleted'}
        'fair-object-cursor'{$r2=Invoke-EvidenceCleanup $env.ConfigPath $false @() 1;Assert-EvidenceTest ($r2.deleted -eq 1) ('cursor starved first='+(ConvertTo-Json $r -Compress -Depth 6)+' second='+(ConvertTo-Json $r2 -Compress -Depth 6))}
        default{Assert-EvidenceTest ($r.status -ceq 'passed' -and $r.deleted -eq 1 -and $env.Objects.Count -eq 0) 'delete success'}
    }
}catch{$failed.Add($caseName);[Console]::Error.WriteLine($caseName+': '+$_.Exception.Message)}finally{[AppDomain]::CurrentDomain.SetData('OneStarMaker.Evidence.StorageBinding.v1',$null);Set-EvidenceTestValue EvidencePaths WriteHook $null;Set-EvidenceTestValue EvidenceCleanup Budget $null;Set-EvidenceTestValue EvidenceStateStore Budget $null;Set-EvidenceTestValue ArtifactApplication Budget $null;Set-EvidenceTestValue EvidenceCleanup FaultHook $null;Set-EvidenceTestValue EvidenceCleanup ProcessHook $null;if($signal){$signal.Dispose()};if($child){$child.Process.Dispose()}}}
Complete-EvidenceTestSuite $ResultPath $registered $selected $executed $failed
[Console]::WriteLine("EvidenceCleanup $($executed.Count), failed $($failed.Count)")
