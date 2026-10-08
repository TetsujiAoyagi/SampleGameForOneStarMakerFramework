param([string] $ResultPath='')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$lifecycle=Import-Module (Join-Path $PSScriptRoot '../TaskLifecycle.psm1') -Force -PassThru
$store=$lifecycle.NestedModules | Where-Object Name -eq 'TaskEventStore' | Select-Object -Last 1
Import-Module $store
$root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-workflow-'+[Guid]::NewGuid().ToString('N'))
$repo='a'*64
$cases=@('event-idempotent','event-gap','resume-stale-end','decision-conflict','generation-cas','atomic-before-commit','finalization-decision-crash','finalization-event-crash','receipt-crash','ack-idempotent','trusted-recovery-time','guard-identity','state-corrupt','reparse-rejected','guard-cross-process','pending-decision-conflict','duplicate-json-property','public-resume-deleted-payload','public-resume-deleted-delivery-pending','fresh-contract-config-discovery','public-real-module-start-status-retry-end-resume')
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
function Assert([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
function Reject([scriptblock]$Action){$threw=$false;try{&$Action|Out-Null}catch{$threw=$true};Assert $threw 'expected rejection'}
$utc=[DateTimeOffset]::Parse('2026-01-01T00:00:00+00:00')
function Test-PublicResume([string]$Mode,[int]$ExpectedExit){
    $callsPath=[IO.Path]::Combine($root,'cli-'+$Mode+'.calls')
    $info=[Diagnostics.ProcessStartInfo]::new([Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)
    $info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($argument in @('-NoProfile','-File',(Join-Path $PSScriptRoot 'WorkflowCliFixture.ps1'),'-Entry',(Join-Path $PSScriptRoot '../../workflow-task.ps1'),'-CallsPath',$callsPath,'-Mode',$Mode)){$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info)
    try{
        $out=$process.StandardOutput.ReadToEndAsync();$err=$process.StandardError.ReadToEndAsync()
        Assert ($process.WaitForExit(10000)) 'public CLI child deadline'
        Assert ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),3000)) 'public CLI pipes'
        Assert ($process.ExitCode -eq $ExpectedExit) ('public CLI exit '+$err.Result+$out.Result)
        $json=$out.Result|ConvertFrom-Json -DateKind String
        Assert ($json.status -ceq $(if($Mode -ceq 'pending'){'recorded/delivery-pending'}else{'payload-deleted'}) -and $json.reasonCode -ceq 'payload-deleted') 'public CLI tombstone outcome'
        Assert (@($json.deletedArtifacts).Count -eq 2 -and $json.deletedArtifacts[0] -ceq ('a'*32) -and $json.deletedArtifacts[1] -ceq ('b'*32)) 'public CLI lost or duplicated payload IDs'
        Assert ($json.result.version -eq 3 -and $json.result.kind -ceq 'resumed') 'public CLI lost committed event'
        $calls=@([IO.File]::ReadAllLines($callsPath))
        Assert (@($calls|Where-Object {$_ -like 'import:*'}).Count -eq 5 -and @($calls|Where-Object {$_ -ceq 'preflight'}).Count -eq 2 -and @($calls|Where-Object {$_ -ceq 'sync'}).Count -eq 2 -and @($calls|Where-Object {$_ -ceq 'transition'}).Count -eq 1 -and $calls -ccontains 'dispose') 'public CLI did not use isolated boundaries'
    }finally{if(-not $process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function Test-RealModuleBoundary([string]$Mode){
    $work=[IO.Path]::Combine($root,'real-modules-'+$Mode)
    $info=[Diagnostics.ProcessStartInfo]::new([Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)
    $info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($argument in @('-NoProfile','-File',(Join-Path $PSScriptRoot 'WorkflowModuleFixture.ps1'),'-Entry',(Join-Path $PSScriptRoot '../../workflow-task.ps1'),'-Root',$work,'-Mode',$Mode)){$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info)
    try{
        $out=$process.StandardOutput.ReadToEndAsync();$err=$process.StandardError.ReadToEndAsync()
        Assert ($process.WaitForExit(15000)) 'real module child deadline'
        Assert ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),3000)) 'real module child pipes'
        Assert ($process.ExitCode -eq 0) ('real module child failed '+$err.Result+$out.Result)
        $observation=$out.Result|ConvertFrom-Json -DateKind String
        Assert ($observation.status -ceq 'passed' -and $observation.mode -ceq $Mode) 'real module result invalid'
        Assert (@($observation.explicitImports).Count -eq $(if($Mode -ceq 'contract'){1}else{5})) 'real module import scope changed'
        if($Mode -ceq 'contract'){Assert ($observation.registryCount -eq 1) 'real config registry count'}
        else{Assert ($observation.startExit -eq 2 -and $observation.statusExit -eq 0 -and $observation.retryExit -eq 0 -and $observation.finalStatusExit -eq 0 -and $observation.endExit -eq 0 -and $observation.resumeExit -eq 0 -and $observation.deliveryPending -eq 0 -and $observation.version -eq 3) 'real public CLI lifecycle result'}
    }finally{if(-not $process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
try {
    & $store {param($r)$script:TestRoot=$r} $root
    & $lifecycle {param($u)$script:Clock={$u}.GetNewClosure()} $utc
    foreach($case in $cases){
        $guard=$null
        try {
            $guard=Enter-WorkflowTaskGuard $repo $case
            $started=Invoke-WorkflowTaskTransition $repo $case 'started' 0 'start' '' $guard
            switch($case){
                'event-idempotent'{
                    $end=Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'done' 'completed' $guard
                    & $lifecycle {param($u)$script:Clock={$u}.GetNewClosure()} $utc.AddDays(100)
                    $retry=Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'done' 'completed' $guard
                    Assert ($end.eventId -ceq $retry.eventId -and $retry.occurredAt -ceq $end.occurredAt -and $retry.version -eq 2) 'retry changed event'
                }
                'event-gap'{
                    Reject {Invoke-WorkflowTaskTransition $repo $case 'ended' 3 'done' 'completed' $guard}
                    Assert ((Read-WorkflowTaskState $repo $case $guard).version -eq 1) 'version gap committed'
                }
                'resume-stale-end'{
                    $old=Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'old-end' 'completed' $guard
                    $null=Invoke-WorkflowTaskTransition $repo $case 'resumed' 2 'resume' '' $guard
                    $null=Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'old-end' 'completed' $guard
                    Assert ((Read-WorkflowTaskState $repo $case $guard).status -eq 'active') 'stale end changed current status'
                    $new=Invoke-WorkflowTaskTransition $repo $case 'ended' 3 'new-end' 'cancelled' $guard
                    Assert ($new.version -eq 4) 'new end version'
                }
                'decision-conflict'{
                    $null=Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'same' 'completed' $guard
                    Reject {Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'same' 'cancelled' $guard}
                }
                'generation-cas'{
                    $state=Read-WorkflowTaskState $repo $case $guard
                    Reject {Write-WorkflowTaskState $repo $case $state ($state.generation-1) $guard}
                }
                'atomic-before-commit'{
                    & $store {$script:Fault={throw 'fault'}}
                    Reject {Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'failed' 'completed' $guard}
                    & $store {$script:Fault=$null}
                    Assert ((Read-WorkflowTaskState $repo $case $guard).version -eq 1) 'precommit state changed'
                }
                'finalization-decision-crash'{
                    & $lifecycle {$script:Fault={param($p)if($p -eq 'after-decision'){throw 'fault'}}}
                    Reject {Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'finish' 'completed' $guard}
                    $status=Get-WorkflowTaskStatus $repo $case $guard
                    Assert ($status.status -eq 'pending-finalization' -and $status.version -eq 1) 'decision guessed end'
                    & $lifecycle {$script:Fault=$null}
                    $end=Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'finish' 'completed' $guard
                    Assert ($end.version -eq 2) 'decision recovery failed'
                }
                'finalization-event-crash'{
                    & $lifecycle {$script:Fault={param($p)if($p -eq 'after-event'){throw 'fault'}}}
                    Reject {Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'finish' 'completed' $guard}
                    $old=(Read-WorkflowTaskState $repo $case $guard).events[1]
                    & $lifecycle {$script:Fault=$null}
                    & $lifecycle {param($u)$script:Clock={$u}.GetNewClosure()} $utc.AddDays(100)
                    $end=Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'finish' 'completed' $guard
                    Assert ($old.occurredAt -ceq $end.occurredAt -and (Get-WorkflowTaskStatus $repo $case $guard).status -eq 'ended') 'event recovery reset time'
                }
                'receipt-crash'{
                    & $lifecycle {$script:Fault={param($p)if($p -eq 'after-receipt'){throw 'fault'}}}
                    Reject {Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'finish' 'completed' $guard}
                    & $lifecycle {$script:Fault=$null}
                    $end=Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'finish' 'completed' $guard
                    Assert ((Read-WorkflowTaskState $repo $case $guard).version -eq 2) 'receipt retry duplicated'
                }
                'ack-idempotent'{
                    Complete-WorkflowTaskDelivery $repo $case $started.eventId 1 $guard
                    $generation=(Read-WorkflowTaskState $repo $case $guard).generation
                    Complete-WorkflowTaskDelivery $repo $case $started.eventId 1 $guard
                    Assert ((Read-WorkflowTaskState $repo $case $guard).generation -eq $generation) 'ack duplicate wrote'
                    Reject {Complete-WorkflowTaskDelivery $repo $case ('0'*32) 1 $guard}
                }
                'trusted-recovery-time'{
                    $record=@{decisionId='trusted-end';kind='ended';endReason='completed';expectedVersion=1;instructionAt='2025-12-31T23:00:00+00:00';finalizedAt='2026-01-01T00:01:00+00:00'}
                    $end=Import-WorkflowFinalization $repo $case $record $guard
                    Assert ([DateTimeOffset]::Parse($end.occurredAt) -eq [DateTimeOffset]::Parse($record.finalizedAt)) 'trusted UTC replaced'
                }
                'guard-identity'{Reject {Read-WorkflowTaskState $repo 'another-task' $guard}}
                'state-corrupt'{
                    $state=Read-WorkflowTaskState $repo $case $guard;$state.outbox[0].version=2
                    Reject {Write-WorkflowTaskState $repo $case $state $state.generation $guard}
                }
                'guard-cross-process'{
                    $quoted=$guard.Handle.Name.Replace("'","''")
                    $command="try{"+'$s=[IO.FileStream]::new('+"'"+$quoted+"',"+'[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);$s.Dispose();exit 1}catch [IO.IOException]{exit 0}'
                    $info=[Diagnostics.ProcessStartInfo]::new();$info.FileName=[Diagnostics.Process]::GetCurrentProcess().MainModule.FileName;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
                    $info.ArgumentList.Add('-NoProfile');$info.ArgumentList.Add('-EncodedCommand');$info.ArgumentList.Add([Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command)))
                    $child=[Diagnostics.Process]::Start($info)
                    try{if(-not $child.WaitForExit(5000)){$child.Kill($true);throw 'guard child timeout'};Assert ($child.ExitCode -eq 0) 'another process opened guard'}finally{$child.Dispose()}
                }
                'pending-decision-conflict'{
                    & $lifecycle {$script:Fault={param($p)if($p -eq 'after-decision'){throw 'fault'}}}
                    Reject {Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'first' 'completed' $guard}
                    & $lifecycle {$script:Fault=$null}
                    Reject {Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'another' 'completed' $guard}
                    $null=Invoke-WorkflowTaskTransition $repo $case 'ended' 1 'first' 'completed' $guard
                }
                'duplicate-json-property'{
                    $path=[IO.Path]::Combine($guard.Path,'state.json')
                    $raw=[IO.File]::ReadAllText($path).Replace('"schemaVersion":1','"schemaVersion":1,"schemaVersion":1')
                    [IO.File]::WriteAllText($path,$raw)
                    Reject {Read-WorkflowTaskState $repo $case $guard}
                }
                'fresh-contract-config-discovery'{Test-RealModuleBoundary 'contract'}
                'public-real-module-start-status-retry-end-resume'{Test-RealModuleBoundary 'cli'}
                'public-resume-deleted-payload'{Test-PublicResume 'complete' 0}
                'public-resume-deleted-delivery-pending'{Test-PublicResume 'pending' 2}
                'reparse-rejected'{
                    $path=[IO.Path]::Combine($root,'junction');$outside=[IO.Path]::Combine($root,'outside')
                    [IO.Directory]::CreateDirectory($outside)|Out-Null
                    New-Item -ItemType Junction -Path $path -Target $outside | Out-Null
                    try{Reject {& $store {param($p)Assert-WorkflowPath $p} $path}}finally{[IO.Directory]::Delete($path)}
                }
            }
            $executed.Add($case)
        }catch{$failed.Add($case+': '+$_.Exception.Message+' '+$_.ScriptStackTrace)}
        finally{
            if($guard){$guard.Dispose()}
            & $store {$script:Fault=$null}
            & $lifecycle {$script:Fault=$null}
            & $lifecycle {param($u)$script:Clock={$u}.GetNewClosure()} $utc
        }
    }
    if($ResultPath){[IO.File]::WriteAllText($ResultPath,(ConvertTo-Json -Depth 8 -InputObject ([ordered]@{registered=$cases;selected=$cases;executed=@($executed);failed=@($failed)})),[Text.UTF8Encoding]::new($false))}
    foreach($name in $executed){"PASS $name"};foreach($name in $failed){"FAIL $name"}
    "Cases: $($cases.Count); passed: $($executed.Count); failed: $($failed.Count)"
    if($failed.Count -or $executed.Count -ne $cases.Count){exit 1}
}finally{
    & $store {$script:TestRoot=$null;$script:Fault=$null}
    & $lifecycle {$script:Clock={[DateTimeOffset]::UtcNow};$script:Fault=$null}
    if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)}
}
