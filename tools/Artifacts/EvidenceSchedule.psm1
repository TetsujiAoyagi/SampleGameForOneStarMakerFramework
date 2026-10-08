Set-StrictMode -Version Latest

# This is an invocation-local deadline, never a persisted policy clock or CLI input.
$script:Budget=$null
$script:ElapsedClock={ [Diagnostics.Stopwatch]::GetTimestamp() }
function Assert-EvidenceBudget { if($script:Budget -and (& $script:Budget.Remaining) -le 0){throw 'evidence-deadline'} }

function New-EvidenceLocalBudget([long]$Limit,$Parent=$null){
    $clock=$script:ElapsedClock;$started=[long](& $clock);$frequency=[Diagnostics.Stopwatch]::Frequency
    $hard={ $left=$Limit-[long](([long](& $clock)-$started)*1000.0/$frequency);if($Parent){$left=[Math]::Min($left,[long](& $Parent.HardRemaining))};return [Math]::Max(0,$left) }.GetNewClosure()
    # Reserve one second inside each phase for the final small job journal. Sync
    # never borrows cleanup time; a caller's remaining deadline can only shrink.
    $remaining={ return [Math]::Max(0,([long](& $hard)-1000)) }.GetNewClosure()
    return [pscustomobject]@{Remaining=$remaining;HardRemaining=$hard}
}
function Set-EvidenceBudgetScopes([string[]]$Names,$Budget){
    $paths=@{'Invoke-EvidenceSync'='EvidenceStateStore.psm1';'Invoke-EvidenceCleanup'='EvidenceCleanup.psm1';'Sync-EvidenceTaskGuarded'='EvidenceStateStore.psm1';'Enter-WorkflowTaskGuard'='../Workflow/TaskEventStore.psm1';'Get-WorkflowTasks'='../Workflow/TaskEventStore.psm1';'Write-EvidenceAtomic'='EvidencePaths.psm1';'Get-Remaining'='ArtifactApplication.psm1'}
    $seen=[Collections.Generic.HashSet[object]]::new();$owners=[Collections.Generic.List[object]]::new()
    foreach($name in $Names){
        $owner=(Get-Command $name -ErrorAction Stop).Module;$expected=[IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,$paths[$name]))
        if(-not $owner -or $owner.Path -ine $expected){throw 'budget-owner-unavailable'}
        if($seen.Add($owner)){$owners.Add($owner)}
    }
    $scopes=[Collections.Generic.List[object]]::new()
    try{foreach($owner in $owners){$previous=& $owner {$script:Budget};$scopes.Add([pscustomobject]@{Owner=$owner;Previous=$previous});& $owner {param($b)$script:Budget=$b} $Budget}}
    catch{Restore-EvidenceBudgetScopes $scopes.ToArray();throw}
    return ,$scopes.ToArray()
}
function Restore-EvidenceBudgetScopes($Scopes){
    for($i=@($Scopes).Count-1;$i -ge 0;$i--){& $Scopes[$i].Owner {param($b)$script:Budget=$b} $Scopes[$i].Previous}
}
Import-Module (Join-Path $PSScriptRoot 'EvidenceContract.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot '../Workflow/TaskEventStore.psm1')
$script:TestRoot=$null
$script:SchedulerHook=$null
$script:TaskServiceHook=$null
$script:SourceHook=$null
$script:SyncHook=$null
$script:CleanupHook=$null
$script:Clock={ [DateTimeOffset]::UtcNow }
function Get-EvidenceRuntimeRoot {
    if($script:TestRoot){return [IO.Path]::GetFullPath($script:TestRoot)}
    return [IO.Path]::Combine([Environment]::GetFolderPath('LocalApplicationData'),'OneStarMaker','Artifacts','runtime')
}
function Get-EvidenceScheduleName([string]$RepositoryId){return 'OSM-Evidence-Cleanup-'+$RepositoryId.Substring(0,12)}
# The work callback stops before the hard deadline so the small final journal
# can consume the reserved second; it cannot open new task or network work.
function Assert-EvidenceJournalBudget {if($script:Budget -and (& $script:Budget.HardRemaining) -le 0){throw 'evidence-deadline'}}
function Write-EvidenceScheduleJson([string]$Path,$Value){
    Assert-EvidenceJournalBudget
    Assert-NoArtifactReparse $Path
    $temp=$Path+'.'+[Guid]::NewGuid().ToString('N')+'.tmp'
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $Value -Compress -Depth 30))
    $stream=[IO.FileStream]::new($temp,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{Set-ArtifactAcl $temp $false;Assert-EvidenceJournalBudget;$stream.Write($bytes);$stream.Flush($true);Assert-EvidenceJournalBudget}finally{$stream.Dispose()}
    try{Assert-EvidenceJournalBudget;[IO.File]::Move($temp,$Path,$true);Assert-EvidenceJournalBudget}finally{if([IO.File]::Exists($temp)){[IO.File]::Delete($temp)}}
}
function Get-EvidenceRuntimeInputs {
    if($script:SourceHook){return & $script:SourceHook}
    $repo=[IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'../..'))
    $head=(& git -C $repo rev-parse HEAD).Trim()
    if($LASTEXITCODE -ne 0 -or $head -cnotmatch '^[a-f0-9]{40}$' -or @(& git -C $repo status --porcelain --untracked-files=no).Count){throw 'runtime-source-uncommitted'}
    $paths=@(& git -C $repo ls-files -- tools/Artifacts tools/Workflow)
    $paths=@($paths | Where-Object {$_ -cmatch '\.(?:ps1|psm1)$' -and $_ -cnotmatch '/(?:tests|artifacts|bin|obj)/'})
    $paths+=@('tools/workflow-task.ps1','tools/artifacts.ps1')
    $binaries=@()
    foreach($target in @(@('Probe','R2RouteTransport','probe'),@('Packaging','ArtifactPackaging','package'),@('Transport','R2ArtifactTransport','transport'))){
        $output=[IO.Path]::Combine($repo,'tools','Artifacts',$target[0],'artifacts',$target[2])
        if(-not [IO.File]::Exists([IO.Path]::Combine($output,$target[1]+'.dll'))){$output=[IO.Path]::Combine($repo,'tools','Artifacts',$target[0],'bin','Release','net8.0')}
        foreach($extension in @('.dll','.deps.json')){
            $primary=[IO.Path]::Combine($output,$target[1]+$extension)
            if(-not [IO.File]::Exists($primary)){throw 'scheduler-runtime-missing'}
            $binaries+=@{path=[IO.Path]::GetRelativePath($repo,$primary).Replace('\','/');sha256=(Get-FileHash -LiteralPath $primary -Algorithm SHA256).Hash.ToLowerInvariant()}
        }
        $paths+=@([IO.Directory]::EnumerateFiles($output) | Where-Object {$_ -match '\.(?:dll|json)$'} | ForEach-Object {[IO.Path]::GetRelativePath($repo,$_).Replace('\','/')})
    }
    return @{root=$repo;head=$head;files=@($paths | Sort-Object -Unique);binaries=$binaries}
}
function Assert-EvidenceRuntime([string]$Runtime,[string]$ExpectedManifestHash=''){
    Assert-EvidenceBudget
    Assert-NoArtifactReparse $Runtime;Assert-ArtifactAcl $Runtime $true
    $path=[IO.Path]::Combine($Runtime,'runtime.json')
    Assert-ArtifactAcl $path $false
    if($ExpectedManifestHash -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $ExpectedManifestHash){throw 'runtime-hash-mismatch'}
    $manifest=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($path)) -AsHashtable -DateKind String -Depth 30
    if($manifest.schemaVersion -ne 1 -or $manifest.implementationHead -cnotmatch '^[a-f0-9]{40}$' -or $manifest.repositoryId -cnotmatch '^[a-f0-9]{64}$' -or @($manifest.binaries).Count -ne 6 -or $manifest.ownerSid -cne [Security.Principal.WindowsIdentity]::GetCurrent().User.Value){throw 'runtime-identity-mismatch'}
    $seen=@{}
    foreach($file in $manifest.files){
        Assert-EvidenceBudget
        if($file.path -match '(^/|(^|/)\.\.(/|$)|:|\\)' -or $seen.ContainsKey($file.path)){throw 'runtime-path-invalid'}
        $seen[$file.path]=$true;$full=[IO.Path]::GetFullPath([IO.Path]::Combine($Runtime,$file.path))
        if(-not (Test-ArtifactWithin $full $Runtime)){throw 'runtime-path-invalid'}
        Assert-NoArtifactReparse $full;Assert-ArtifactAcl $full $false
        if(-not [IO.File]::Exists($full) -or [IO.FileInfo]::new($full).Length -ne $file.bytes -or (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant() -cne $file.sha256){throw 'runtime-hash-mismatch'}
    }
    $binaryNames=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($binary in $manifest.binaries){
        Assert-EvidenceBudget
        $name=[IO.Path]::GetFileName($binary.path)
        if($name -cnotin @('R2RouteTransport.dll','R2RouteTransport.deps.json','ArtifactPackaging.dll','ArtifactPackaging.deps.json','R2ArtifactTransport.dll','R2ArtifactTransport.deps.json') -or -not $binaryNames.Add($name)){throw 'runtime-binary-mismatch'}
        $file=@($manifest.files | Where-Object path -CEQ $binary.path)
        if($file.Count -ne 1 -or $file[0].sha256 -cne $binary.sha256){throw 'runtime-binary-mismatch'}
    }
    $config=Read-EvidenceConfig ([IO.Path]::Combine($Runtime,'config.json')) 'osm'
    if($config.Data.repositoryId -cne $manifest.repositoryId -or $config.Data.deploymentId -cne $manifest.deploymentId -or $config.Hash -cne $manifest.configSha256){throw 'runtime-config-mismatch'}
    return $manifest
}
# TaskPath registration does not create its parent folder. Only the exact dedicated
# folder's missing HRESULT permits creation; permissions and unknown errors stop.
# Existing folder security and unrelated tasks are never rewritten.
function Initialize-EvidenceSchedulerFolder {
    $service=$null;$root=$null;$folder=$null
    try{
        $service=if($script:TaskServiceHook){& $script:TaskServiceHook}else{New-Object -ComObject 'Schedule.Service'}
        $service.Connect()
        try{$folder=$service.GetFolder('\OneStarMaker')}
        catch{
            if($_.Exception.GetBaseException().HResult -ne -2147024894){throw}
            $root=$service.GetFolder('\');$folder=$root.CreateFolder('OneStarMaker',$null)
        }
    }finally{
        foreach($owned in @($folder,$root,$service)){if($owned -and [Runtime.InteropServices.Marshal]::IsComObject($owned)){$null=[Runtime.InteropServices.Marshal]::ReleaseComObject($owned)}}
    }
}
function Invoke-EvidenceScheduler([string]$Action,$Identity){
    if($script:SchedulerHook){return & $script:SchedulerHook $Action $Identity}
    $existing=Get-ScheduledTask -TaskName $Identity.name -TaskPath '\OneStarMaker\' -ErrorAction SilentlyContinue
    if($Action -eq 'status'){
        if(-not $existing){return @{installed=$false}}
        $info=Get-ScheduledTaskInfo -InputObject $existing
        return @{installed=$true;execute=$existing.Actions[0].Execute;arguments=$existing.Actions[0].Arguments;userId=$existing.Principal.UserId;logonType=$(if([string]$existing.Principal.LogonType -in @('Interactive','InteractiveToken')){'InteractiveToken'}else{[string]$existing.Principal.LogonType});runLevel=[string]$existing.Principal.RunLevel;lastRun=$info.LastRunTime;nextRun=$info.NextRunTime;lastResult=$info.LastTaskResult}
    }
    if($existing){
        if($existing.Actions.Count -ne 1 -or $existing.Actions[0].Execute -cne $Identity.execute -or $existing.Actions[0].Arguments -cne $Identity.arguments -or $existing.Principal.UserId -cne $Identity.ownerSid -or [string]$existing.Principal.LogonType -cnotin @('Interactive','InteractiveToken') -or [string]$existing.Principal.RunLevel -cne 'Limited'){throw 'scheduler-identity-mismatch'}
        return @{installed=$true;unchanged=$true}
    }
    Initialize-EvidenceSchedulerFolder
    # Variable names ignore case; assigning $action would coerce CIM into typed $Action.
    $scheduledAction=New-ScheduledTaskAction -Execute $Identity.execute -Argument $Identity.arguments -WorkingDirectory $Identity.runtime
    $principal=New-ScheduledTaskPrincipal -UserId $Identity.ownerSid -LogonType Interactive -RunLevel Limited
    $triggers=@((New-ScheduledTaskTrigger -Daily -At '03:00'),(New-ScheduledTaskTrigger -AtLogOn -User $Identity.ownerSid))
    $settings=New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit ([TimeSpan]::FromMinutes(10))
    Register-ScheduledTask -TaskName $Identity.name -TaskPath '\OneStarMaker\' -Action $scheduledAction -Principal $principal -Trigger $triggers -Settings $settings -Description ('OSM Evidence runtime '+$Identity.manifestSha256) | Out-Null
    return @{installed=$true;unchanged=$false}
}
function Get-EvidenceScheduleIdentity([string]$Runtime,$Manifest){
    $pwsh=[Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
    $hash=(Get-FileHash -LiteralPath ([IO.Path]::Combine($Runtime,'runtime.json')) -Algorithm SHA256).Hash.ToLowerInvariant()
    $entry=[IO.Path]::Combine($Runtime,'tools','Artifacts','EvidenceCleanupJob.ps1')
    return @{name=(Get-EvidenceScheduleName $Manifest.repositoryId);ownerSid=$Manifest.ownerSid;runtime=$Runtime;execute=$pwsh;arguments=('-NoProfile -NonInteractive -WindowStyle Hidden -File "'+$entry+'" -Runtime "'+$Runtime+'" -ManifestSha256 '+$hash);manifestSha256=$hash}
}
function Invoke-EvidenceScheduleInstall([string]$ConfigPath){
    $config=Read-EvidenceConfig $ConfigPath 'osm';$runtimeInput=Get-EvidenceRuntimeInputs
    $root=Get-EvidenceRuntimeRoot;Assert-NoArtifactReparse $root
    if(-not [IO.Directory]::Exists($root)){[IO.Directory]::CreateDirectory($root)|Out-Null;Set-ArtifactAcl $root $true}
    Assert-ArtifactAcl $root $true
    $runtime=[IO.Path]::Combine($root,$runtimeInput.head)
    if([IO.Directory]::Exists($runtime)){
        $manifest=Assert-EvidenceRuntime $runtime
        if($manifest.repositoryId -cne $config.Data.repositoryId -or $manifest.configSha256 -cne $config.Hash){throw 'runtime-identity-mismatch'}
    }else{
        $temporary=[IO.Path]::Combine($root,[Guid]::NewGuid().ToString('N')+'.staging')
        [IO.Directory]::CreateDirectory($temporary)|Out-Null;Set-ArtifactAcl $temporary $true
        $files=[Collections.Generic.List[object]]::new()
        foreach($relative in @($runtimeInput.files)+@('config.json')){
            $source=if($relative -eq 'config.json'){$ConfigPath}else{[IO.Path]::Combine($runtimeInput.root,$relative)}
            Assert-NoArtifactReparse $source
            $destination=[IO.Path]::GetFullPath([IO.Path]::Combine($temporary,$relative))
            if(-not (Test-ArtifactWithin $destination $temporary) -or $destination -ceq $temporary){throw 'runtime-path-invalid'}
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))|Out-Null
            [IO.File]::Copy($source,$destination,$false);Set-ArtifactAcl $destination $false
            $files.Add([ordered]@{path=$relative;bytes=[IO.FileInfo]::new($destination).Length;sha256=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()})
        }
        foreach($directory in [IO.Directory]::EnumerateDirectories($temporary,'*',[IO.SearchOption]::AllDirectories)){Set-ArtifactAcl $directory $true}
        $manifest=[ordered]@{schemaVersion=1;implementationHead=$runtimeInput.head;repositoryId=$config.Data.repositoryId;deploymentId=$config.Data.deploymentId;configSha256=$config.Hash;ownerSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value;files=@($files);binaries=$runtimeInput.binaries}
        Write-EvidenceScheduleJson ([IO.Path]::Combine($temporary,'runtime.json')) $manifest
        $null=Assert-EvidenceRuntime $temporary
        [IO.Directory]::Move($temporary,$runtime)
    }
    $identity=Get-EvidenceScheduleIdentity $runtime $manifest
    $existing=Invoke-EvidenceScheduler 'status' $identity
    if($existing.installed -and ($existing.execute -cne $identity.execute -or $existing.arguments -cne $identity.arguments -or $existing.userId -cne $identity.ownerSid -or $existing.logonType -cne 'InteractiveToken' -or $existing.runLevel -cne 'Limited')){throw 'scheduler-identity-mismatch'}
    $result=Invoke-EvidenceScheduler 'install' $identity
    Write-EvidenceScheduleJson ([IO.Path]::Combine($root,$config.Data.repositoryId+'.schedule.json')) $identity
    return @{status='passed';runtime=$runtime;manifestSha256=$identity.manifestSha256;scheduler=$result}
}
function Get-EvidenceScheduleStatus([string]$ConfigPath){
    $config=Read-EvidenceConfig $ConfigPath 'osm';$path=[IO.Path]::Combine((Get-EvidenceRuntimeRoot),$config.Data.repositoryId+'.schedule.json')
    Assert-NoArtifactReparse $path
    if(-not [IO.File]::Exists($path)){return @{status='pending';reasonCode='scheduler-not-installed'}}
    Assert-ArtifactAcl $path $false
    $identity=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($path)) -AsHashtable -DateKind String
    $manifest=Assert-EvidenceRuntime $identity.runtime $identity.manifestSha256
    if($manifest.configSha256 -cne $config.Hash){throw 'runtime-config-mismatch'}
    $scheduler=Invoke-EvidenceScheduler 'status' $identity
    $statePath=[IO.Path]::Combine($identity.runtime,'job-state.json')
    $state=if([IO.File]::Exists($statePath)){Assert-ArtifactAcl $statePath $false;ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($statePath)) -AsHashtable -DateKind String}else{$null}
    return @{status='passed';scheduler=$scheduler;job=$state;runtime=$identity.runtime;manifestSha256=$identity.manifestSha256}
}
function Get-EvidenceFairOrder([string[]]$Ids,[string]$After){
    $sorted=@($Ids | Sort-Object -Unique)
    return @($sorted | Where-Object {$_ -cgt $After})+@($sorted | Where-Object {$_ -cle $After})
}
function Invoke-EvidenceScheduledJob([string]$Runtime,[string]$ManifestSha256){
    $jobBudget=New-EvidenceLocalBudget 600000;$priorBudget=$script:Budget;$script:Budget=$jobBudget
    try{
    $manifest=Assert-EvidenceRuntime $Runtime $ManifestSha256
    $lockPath=[IO.Path]::Combine($Runtime,'job.lock');Assert-NoArtifactReparse $lockPath
    try{$lock=[IO.FileStream]::new($lockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch [IO.IOException]{return @{status='busy'}}
    try{
        Set-ArtifactAcl $lockPath $false
        $path=[IO.Path]::Combine($Runtime,'job-state.json')
        $state=if([IO.File]::Exists($path)){Assert-ArtifactAcl $path $false;ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($path)) -AsHashtable -DateKind String}else{@{syncCursor='';cleanupCursor='';lastRun=$null;deleted=0;skipped=0;blocked=0;status='ok'}}
        $configPath=[IO.Path]::Combine($Runtime,'config.json')
        $taskScopes=Set-EvidenceBudgetScopes @('Get-WorkflowTasks') $jobBudget
        try{$tasks=@(Get-WorkflowTasks $manifest.repositoryId)}finally{Restore-EvidenceBudgetScopes $taskScopes}
        $syncBudget=New-EvidenceLocalBudget 120000 $jobBudget;$count=0;$blocked=0;$skipped=0;$deleted=0;$partial=$false
        foreach($task in (Get-EvidenceFairOrder $tasks $state.syncCursor)){
            if($count -ge 100 -or (& $syncBudget.Remaining) -le 0){$partial=$true;break}
            $state.syncCursor=$task;$count++;Write-EvidenceScheduleJson $path $state
            $scopes=@()
            try{if($script:SyncHook){$result=& $script:SyncHook $configPath $task $syncBudget}else{$scopes=Set-EvidenceBudgetScopes @('Invoke-EvidenceSync') $syncBudget;$result=Invoke-EvidenceSync $configPath $task};if($result.status -ne 'passed'){$blocked++}}catch{$blocked++}finally{Restore-EvidenceBudgetScopes $scopes}
            Write-EvidenceScheduleJson $path $state
        }
        # Cleanup has its own clock and persisted cursor. A broken outbox cannot consume
        # its eight minute reservation or keep the first failed task at the front.
        $cleanupBudget=New-EvidenceLocalBudget 480000 $jobBudget;$count=0
        foreach($task in (Get-EvidenceFairOrder $tasks $state.cleanupCursor)){
            if($count -ge 100 -or (& $cleanupBudget.Remaining) -le 0){$partial=$true;break}
            $state.cleanupCursor=$task;Write-EvidenceScheduleJson $path $state
            $scopes=@()
            try{
                if($script:CleanupHook){$result=& $script:CleanupHook $configPath $task (100-$count) $cleanupBudget}else{$scopes=Set-EvidenceBudgetScopes @('Invoke-EvidenceCleanup') $cleanupBudget;$result=Invoke-EvidenceCleanup $configPath $false @($task) (100-$count)}
                $deleted+=$result.deleted;$skipped+=$result.skipped;$blocked+=$result.blocked
                # Every artifact and staging attempt consumes the same global budget.
                # Counters also cover a conservative cost when a partial result has no
                # item (for example, failure before acquiring a task's catalog).
                $processed=[Math]::Max(@($result.items).Count,([int]$result.deleted+[int]$result.skipped+[int]$result.blocked))
                $count+=[Math]::Max(1,$processed)
                if($result.status -cne 'passed' -or $result.remaining -gt 0){$partial=$true}
            }catch{$blocked++;$count++}finally{Restore-EvidenceBudgetScopes $scopes}
            Write-EvidenceScheduleJson $path $state
        }
        $state.lastRun=(& $script:Clock).ToUniversalTime().ToString('o');$state.deleted=$deleted;$state.skipped=$skipped;$state.blocked=$blocked;$state.status=if($blocked -or $partial){'partial'}else{'ok'}
        Write-EvidenceScheduleJson $path $state
        return $state
    }finally{$lock.Dispose()}
    }finally{$script:Budget=$priorBudget}
}
Export-ModuleMember -Function Invoke-EvidenceScheduleInstall,Get-EvidenceScheduleStatus,Invoke-EvidenceScheduledJob
