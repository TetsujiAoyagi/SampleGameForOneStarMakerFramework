param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
. (Join-Path $PSScriptRoot 'EvidenceReaderFixture.ps1')
$registered=@('delete-success','delete-timeout','delete-forbidden','delete-crash-reconcile','delete-after-remote-reconcile','cleanup-partial','active-marker-protected','use-delete-race','delete-use-race','resume-delete-race','delete-resume-race','source-marker-protected','reparse-protected','staging-cleanup','staging-transfer-protected','dry-run-no-delete','fair-object-cursor','staging-adoption-success','child-not-stopped','corrupt-catalog-non-delete','stopped-operation-cleanup','recovery-staging-cleanup','corrupt-lifecycle-projection','staging-fair-cursor','staging-result-accounting','deadline-request-and-guard','deadline-atomic-ack-retry','old-alias-cleanup','old-alias-fetch','old-alias-child-eof-blocked','old-alias-marker-unresolved','old-alias-copy-acl-blocked','missing-owned-copy','noncanonical-owned-copy')
$registered+=@('fresh-process-cleanup-contract')
$registered+=@('staging-missing-report','staging-missing-drop','staging-missing-no-witness','staging-missing-race','staging-missing-refuse')
$stagedCases=@('staged-retention-boundary','staged-delete-stop-before-commit','staged-delete-stop-after-commit','staged-zip-missing-refused','staged-use-protection')
$registered+=$stagedCases
$promotedCases=@('deletion-route-table','local-delete-recheck','route-none-unchanged','promoted-reader-shapes','promoted-match-adoption','sent-staged-expiry','sent-staged-delete-stops','sent-staged-delete-write-failure','failed-recovery-cleanup','failed-recovery-cleanup-isolation','failed-recovery-cleanup-write-failure')
$registered+=$promotedCases
$selected=if($Case -ceq '*'){$registered}else{@($Case.Split(','))};$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
function Start-EvidenceChild($Env,$Publish,[string]$Action,[string]$Signal='',[string]$Now=''){
    $info=[Diagnostics.ProcessStartInfo]::new('pwsh');$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($arg in @('-NoProfile','-File',(Join-Path $PSScriptRoot 'EvidenceTestChild.ps1'),'-Root',$Env.Root,'-ConfigPath',$Env.ConfigPath,'-Reference',$Publish.reference,'-Sha256',$Publish.packageSha256,'-Action',$Action,'-Signal',$Signal,'-Now',$Now)){$info.ArgumentList.Add($arg)}
    $p=[Diagnostics.Process]::new();$p.StartInfo=$info;$null=$p.Start();return [pscustomobject]@{Process=$p;Out=$p.StandardOutput.ReadToEndAsync();Err=$p.StandardError.ReadToEndAsync()}
}
function Complete-EvidenceChild($Child){Assert-EvidenceTest ($Child.Process.WaitForExit(15000)) 'child unfinished';Assert-EvidenceTest ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($Child.Out,$Child.Err),5000)) 'child pipes';return [pscustomobject]@{exit=$Child.Process.ExitCode;output=$Child.Out.Result;error=$Child.Err.Result}}
# Missing-staging fixtures. A "missing" stage is catalogued but has no directory.
function Edit-FixtureTask($Env,[scriptblock]$Edit){$g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$t=Read-EvidenceTask $Env.Config fixture $g;& $Edit $t;$null=Write-EvidenceTask $Env.Config fixture $t $t.generation $g}finally{$g.Dispose()}}
function New-MissingStage([string]$Path,[DateTimeOffset]$CreatedAt){[pscustomobject]@{path=$Path;createdAt=$CreatedAt.ToString('o');origin='storage-copy';artifactId=$null;adopted=$false;adoptionPending=$false;operation=$null;deleteIntent=$null;protections=@()}}
function New-TransfersPath($Env){[IO.Path]::Combine($Env.Root,'transfers',[Guid]::NewGuid().ToString('N'))}
# Sync bumps `generation` on every read (existing behavior), so the content hash ignores that one field and covers everything else, staging entries included.
function Get-FixtureCatalogHash($Env,[string]$TaskId='fixture'){$p=[IO.Path]::Combine((Get-EvidenceTaskDirectory $Env.Config $TaskId),'catalog.json');if(-not [IO.File]::Exists($p)){return ''};$o=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($p)) -DateKind String;$o.generation=0;[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $o -Depth 24 -Compress))))}
function Get-FixtureArtifactsHash($Env){$p=[IO.Path]::Combine((Get-EvidenceTaskDirectory $Env.Config fixture),'catalog.json');[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject (ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($p)) -DateKind String).artifacts -Depth 24 -Compress))))}
function Get-FixtureStaging($Env){$g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{return ,@((Read-EvidenceTask $Env.Config fixture $g).staging)}finally{$g.Dispose()}}
# Every file under transfers with its hash: deleting or rewriting any registered or unregistered file changes it.
function Get-TransfersSignature($Env){@(Get-ChildItem -LiteralPath ([IO.Path]::Combine($Env.Root,'transfers')) -Recurse -Force -File|Sort-Object FullName|ForEach-Object{$_.FullName+'|'+(Get-FileHash -LiteralPath $_.FullName).Hash})-join "`n"}
function New-FixtureDirectory([string]$Path){[IO.Directory]::CreateDirectory($Path)|Out-Null;Set-ArtifactAcl $Path $true}
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
        # Installation now verifies its postcondition by reading the OS adapter.
        # Keep that state in this offline stub instead of reporting permanent absence.
        & $schedule {param($r,$i)$script:BindingRootsHook={$r}.GetNewClosure();$script:TestRoot=$r.runtime;$script:SourceHook={$i}.GetNewClosure();$script:OfflineSchedulerInstalled=$false;$script:SchedulerHook={param($a,$identity)if($a -ceq 'install'){$script:OfflineSchedulerInstalled=$true};@{installed=$script:OfflineSchedulerInstalled}}} $roots $inputs
        $runtime=Invoke-EvidenceScheduleInstall $Env.ConfigPath
    }finally{& $schedule {$script:BindingRootsHook=$null;$script:TestRoot=$null;$script:SourceHook=$null;$script:SchedulerHook=$null}}
    $childPath=[IO.Path]::Combine($Env.Root,'cleanup-contract-child.ps1')
    [IO.File]::WriteAllText($childPath,@'
param($Runtime,$Hash,$Credential,$StoreBase,$LegacyBase,$Support)
$ErrorActionPreference='Stop';$WarningPreference='SilentlyContinue'
$schedule=Import-Module ([IO.Path]::Combine($Runtime,'tools','Artifacts','EvidenceSchedule.psm1')) -PassThru
. $Support;Set-StoreRootTestSeams $StoreBase $LegacyBase
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
    foreach($arg in @('-NoProfile','-File',$childPath,'-Runtime',$runtime.runtime,'-Hash',$runtime.manifestSha256,'-Credential',$credential,'-StoreBase',$Env.StoreRoot.Base,'-LegacyBase',$Env.StoreRoot.LegacyBase,'-Support',(Join-Path $PSScriptRoot '../../Workflow/tests/StoreRootTestSupport.ps1'))){$info.ArgumentList.Add($arg)}
    $process=[Diagnostics.Process]::Start($info)
    try{$out=$process.StandardOutput.ReadToEndAsync();$err=$process.StandardError.ReadToEndAsync();Assert-EvidenceTest ($process.WaitForExit(15000)) 'cleanup dependency child deadline';Assert-EvidenceTest ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),5000)) 'cleanup dependency child pipes';Assert-EvidenceTest ($process.ExitCode -eq 0) ('cleanup dependency child failed '+$out.Result+$err.Result);$observation=$out.Result|ConvertFrom-Json;Assert-EvidenceTest ($observation.status -ceq 'passed') 'cleanup dependency observation';if($ResultPath){Write-EvidenceTestJson ($ResultPath+'.fresh-child.json') $observation}}
    finally{if(-not $process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
# Local staging cleanup. The task ends at a fixed time; the staging is created ten
# days earlier, so the seven-day staging rule alone would already delete it.
function Invoke-StagedCleanupCase([string]$CaseName,$Env,[string]$Selection){
    $endAt=[DateTimeOffset]::Parse('2026-01-01T00:00:00.0000000+00:00');$deadline=$endAt.AddDays(30);$created=$endAt.AddDays(-10)
    $setNow={param($at)Set-EvidenceTestValue EvidenceCleanup Clock {$at}.GetNewClosure();Set-EvidenceTestValue EvidenceApplication Clock {$at}.GetNewClosure()}
    & $setNow $created;$s=Invoke-EvidenceStage $Env.ConfigPath $Selection;Assert-EvidenceTest ($s.status -ceq 'passed') ('stage '+$s.reasonCode)
    Set-EvidenceTestValue TaskLifecycle Clock {$endAt}.GetNewClosure();$g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$null=Invoke-WorkflowTaskTransition ('a'*64) fixture ended 1 fixture-end completed $g}finally{$g.Dispose()}
    $t=Read-EvidenceTestTask $Env;$a=$t.artifacts[0];$paths=@($a.ownedCopies|ForEach-Object path);$stagePath=@($t.staging|Where-Object artifactId -CEQ $a.artifactId)[0].path
    $hashes={@($paths|ForEach-Object{if([IO.File]::Exists($_)){(Get-FileHash -LiteralPath $_).Hash}else{'missing'}}) -join ','}.GetNewClosure();$intact=& $hashes
    $state={(Read-EvidenceTestTask $Env).artifacts[0].state}.GetNewClosure();$gone={@($paths|Where-Object {[IO.File]::Exists($_)}).Count -eq 0}.GetNewClosure()
    switch($CaseName){
        'staged-retention-boundary'{
            & $setNow $deadline.AddDays(-1);$r=Invoke-EvidenceCleanup $Env.ConfigPath;$codes=@($r.items|ForEach-Object reasonCode)
            Assert-EvidenceTest ($codes -ccontains 'retained' -and $codes -ccontains 'operation-pending' -and $r.deleted -eq 0 -and (& $hashes) -ceq $intact -and [IO.Directory]::Exists($stagePath)) ('day before '+($codes -join ','))
            & $setNow $deadline;$before=Get-FixtureArtifactsHash $Env;$r=Invoke-EvidenceCleanup $Env.ConfigPath $true
            Assert-EvidenceTest (@($r.items|ForEach-Object reasonCode) -ccontains 'expired' -and $r.deleted -eq 0 -and (Get-FixtureArtifactsHash $Env) -ceq $before -and (& $hashes) -ceq $intact) 'dry run changed staged'
            $r=Invoke-EvidenceCleanup $Env.ConfigPath;Assert-EvidenceTest ($r.status -ceq 'passed' -and $r.deleted -eq 1 -and (& $state) -ceq 'deleted' -and (& $gone)) ('deadline '+(ConvertTo-Json $r -Compress -Depth 6))
            $r=Invoke-EvidenceCleanup $Env.ConfigPath;Assert-EvidenceTest ($r.status -ceq 'passed') 'after deletion'
        }
        'staged-delete-stop-before-commit'{
            & $setNow $deadline;Set-EvidenceTestValue EvidenceCleanup FaultHook {param($stage,$x)if($stage -ceq 'before-local-delete'){throw 'simulated-stop'}}
            $r=Invoke-EvidenceCleanup $Env.ConfigPath;Assert-EvidenceTest ($r.status -ceq 'pending' -and (& $state) -ceq 'staged' -and (& $hashes) -ceq $intact) 'stop before commit changed files'
            Set-EvidenceTestValue EvidenceCleanup FaultHook $null;$r=Invoke-EvidenceCleanup $Env.ConfigPath;Assert-EvidenceTest ($r.status -ceq 'passed' -and $r.deleted -eq 1 -and (& $state) -ceq 'deleted' -and (& $gone)) 'rerun after early stop'
        }
        'staged-delete-stop-after-commit'{
            & $setNow $deadline;Set-EvidenceTestValue EvidenceCleanup FaultHook {param($stage,$x)if($stage -ceq 'after-local-deleted'){throw 'simulated-stop'}}
            $r=Invoke-EvidenceCleanup $Env.ConfigPath;Assert-EvidenceTest ($r.status -ceq 'pending' -and (& $state) -ceq 'deleted' -and (& $hashes) -ceq $intact) 'commit point not persisted first'
            # The on-disk state of a stop after one file was removed and before the remaining list was written.
            Set-EvidenceTestValue EvidenceCleanup FaultHook $null;[IO.File]::Delete($paths[0])
            $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$null=Assert-EvidenceTransitionSafe $Env.Config fixture $g -NoNetwork;$null=Assert-EvidenceTransitionSafe $Env.Config fixture $g}finally{$g.Dispose()}
            $r=Invoke-EvidenceCleanup $Env.ConfigPath;$left=(Read-EvidenceTestTask $Env).artifacts[0]
            Assert-EvidenceTest ($r.status -ceq 'passed' -and (& $gone) -and $left.state -ceq 'deleted' -and @($left.ownedCopies).Count -eq 0) ('rerun after commit '+(ConvertTo-Json $r -Compress -Depth 6))
        }
        'staged-zip-missing-refused'{
            # Absence is progress only after `deleted` is persisted; a live staged artifact still refuses it.
            & $setNow $deadline;$zip=(Get-EvidenceStagedZip $a).path;[IO.File]::Delete($zip)
            $r=Invoke-EvidenceCleanup $Env.ConfigPath;Assert-EvidenceTest ($r.status -ceq 'pending' -and $r.deleted -eq 0 -and (& $state) -ceq 'staged' -and @($paths|Where-Object {$_ -cne $zip -and -not [IO.File]::Exists($_)}).Count -eq 0) 'missing zip tolerated'
        }
        'staged-use-protection'{
            & $setNow $deadline.AddDays(-1);$u=Invoke-EvidenceUse $Env.ConfigPath $s.reference reader $deadline.AddHours(1).ToString('o') review;Assert-EvidenceTest ($u.status -ceq 'passed') ('use '+$u.reasonCode)
            $i=Invoke-EvidenceInspect $Env.ConfigPath fixture;Assert-EvidenceTest ($i.artifacts[0].locality -ceq 'local' -and $i.artifacts[0].reference -ceq $s.reference -and $i.artifacts[0].packageSha256 -ceq $s.packageSha256) 'inspect staged'
            & $setNow $deadline.AddMinutes(30);$r=Invoke-EvidenceCleanup $Env.ConfigPath;Assert-EvidenceTest (@($r.items|ForEach-Object reasonCode) -ccontains 'in-use' -and $r.deleted -eq 0 -and (& $hashes) -ceq $intact) 'protected staged deleted'
            $rel=Invoke-EvidenceRelease $Env.ConfigPath $s.reference $u.protection;Assert-EvidenceTest ($rel.status -ceq 'passed') 'release'
            $r=Invoke-EvidenceCleanup $Env.ConfigPath;Assert-EvidenceTest ($r.deleted -eq 1 -and (& $gone)) 'released staged kept'
        }
    }
    Assert-EvidenceTest ((Get-EvidenceRemoteTouches $Env) -eq 0) 'staged cleanup touched remote'
    $null=Read-EvidenceTestTask $Env
}
# Promotion cleanup. Clocks are fixed: staging at T0, the task ends one hour later,
# and the deadline is that end plus 30 days. Each scenario builds its own store,
# because a new test environment points every module at a new root.
$script:PromotedT0=[DateTimeOffset]::Parse('2026-01-01T00:00:00.0000000+00:00')
function Set-PromotedNow([DateTimeOffset]$At){Set-EvidenceTestValue EvidenceCleanup Clock {$At}.GetNewClosure();Set-EvidenceTestValue EvidenceApplication Clock {$At}.GetNewClosure()}
function Reset-PromotedHooks {Set-PromotedNow $script:PromotedT0;Set-EvidenceTestValue TaskLifecycle Clock {[DateTimeOffset]::UtcNow};Set-EvidenceTestValue EvidenceApplication FaultHook $null;Set-EvidenceTestValue EvidenceCleanup FaultHook $null;Set-EvidenceTestValue EvidenceCleanup ProcessHook $null;Set-EvidenceTestValue EvidencePaths WriteHook $null}
function New-PromotedEnv {$e=New-EvidenceTestEnvironment;$f=New-EvidenceReaderFixture $e.Root;$sp=[IO.Path]::Combine($e.Root,'selection.json');Write-EvidenceTestJson $sp $f.Selection;return [pscustomobject]@{Env=$e;Selection=$sp}}
function Read-PromotedDisk($Env){return Read-EvidenceTask $Env.Config fixture ([pscustomobject]@{RepositoryId='a'*64;TaskId='fixture'})}
function Get-PromotedArtifact($Env,[string]$Id){return @((Read-PromotedDisk $Env).artifacts|Where-Object artifactId -CEQ $Id)[0]}
function Complete-PromotedTask {$at=$script:PromotedT0.AddHours(1);Set-EvidenceTestValue TaskLifecycle Clock {$at}.GetNewClosure();$g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$null=Invoke-WorkflowTaskTransition ('a'*64) fixture ended 1 fixture-end completed $g}finally{$g.Dispose()};return $at.AddDays(30)}
function New-PromotedStage($Env,[string]$Selection){
    $s=Invoke-EvidenceStage $Env.ConfigPath $Selection;Assert-EvidenceTest ($s.status -ceq 'passed') ('stage '+$s.reasonCode);$id=(Read-EvidenceReference $s.reference $Env.Config).artifactId;$a=Get-PromotedArtifact $Env $id;$zip=(Get-EvidenceStagedZip $a).path
    return [pscustomobject]@{Stage=$s;Id=$id;Key=$a.key;Zip=$zip;ZipBytes=[IO.File]::ReadAllBytes($zip);Readback=$null}
}
# A promote of $X that dies at $Point: the catalog is put back to its bytes at that
# moment and the promote's process counts as stopped from then on.
function Invoke-PromotedStop($Env,$X,[string]$Point){
    $catalog=[IO.Path]::Combine((Get-EvidenceTaskDirectory $Env.Config fixture),'catalog.json');$saved=@{bytes=$null}
    Set-EvidenceTestValue EvidenceApplication FaultHook ({param($p,$a)if($p -ceq $Point){$saved.bytes=[IO.File]::ReadAllBytes($catalog);throw 'simulated-stop'}}.GetNewClosure())
    try{$r=Invoke-EvidencePromote $Env.ConfigPath $X.Stage.reference $X.Stage.packageSha256}finally{Set-EvidenceTestValue EvidenceApplication FaultHook $null}
    Assert-EvidenceTest ($null -ne $saved.bytes -and $r.status -ceq 'failed') ('stop '+$Point);[IO.File]::WriteAllBytes($catalog,$saved.bytes);Set-EvidenceTestValue EvidenceCleanup ProcessHook {param($identity)$true}
    $X.Readback=(Get-PromotedArtifact $Env $X.Id).operation.path
}
# A staged artifact holding a putIntent: its promote stopped after write A and a fetch's recovery saw 404.
function New-PromotedSentStaged($Env,[string]$Selection){
    $x=New-PromotedStage $Env $Selection;Invoke-PromotedStop $Env $x 'promote-after-put-pending';$f=Invoke-EvidenceFetch $Env.ConfigPath $x.Stage.reference $x.Stage.packageSha256
    $a=Get-PromotedArtifact $Env $x.Id;Assert-EvidenceTest ($f.status -ceq 'passed' -and $a.state -ceq 'staged' -and $null -ne $a.putIntent) ('sent staged '+$f.reasonCode);return $x
}
# The failed shape the previous runtime wrote on that 404, in the three variants of the readback staging (see ArtifactEvidence.Tests.ps1).
function New-PromotedFailed($Env,[string]$Selection,[string]$Variant){
    $x=New-PromotedStage $Env $Selection;Invoke-PromotedStop $Env $x 'promote-after-put-pending';$readback=$x.Readback;$recovery=& @(Get-EvidenceTestModules EvidenceCleanup)[0] {New-ArtifactOperation}
    Edit-FixtureTask $Env {param($t)$a=@($t.artifacts|Where-Object artifactId -CEQ $x.Id)[0]
        $t.staging+=[pscustomobject]@{path=$recovery;createdAt=$script:PromotedT0.ToString('o');origin='storage-copy';artifactId=$a.artifactId;adopted=$false;adoptionPending=$false;operation=$null;deleteIntent=$null;protections=@()}
        $a.state='failed';$a.operation=$null
        if($Variant -ceq 'ii'){$t.staging=@($t.staging|Where-Object {$_.path -cne $readback -and $_.path -cne $recovery})}elseif($Variant -ceq 'iii'){foreach($s in @($t.staging|Where-Object artifactId -CEQ $a.artifactId)){$s.operation=$null}}
    }
    if($Variant -ceq 'ii'){foreach($dir in @($readback,$recovery)){[IO.Directory]::Delete($dir,$true)}}
    return $x
}
function Get-PromotedItem($Result,[string]$Id){return @($Result.items|Where-Object artifactId -CEQ $Id)}
# Arms a failure on the first catalog write that records $Id as deleted, judged from the bytes being written.
function Set-PromotedDeletedWriteFailure([string]$Id,[string]$Phase,$Probe){
    Set-EvidenceTestValue EvidencePaths WriteHook ({param($st,$path)
        if($Probe.fired -or $st -cne $Phase -or -not $path.EndsWith('\catalog.json')){return}
        $file=if($st -ceq 'before-rename'){@(Get-ChildItem -LiteralPath ([IO.Path]::GetDirectoryName($path)) -Filter 'catalog.json.*.pending'|Sort-Object LastWriteTimeUtc)[-1].FullName}else{$path}
        if(@(([IO.File]::ReadAllText($file)|ConvertFrom-Json).artifacts|Where-Object artifactId -CEQ $Id)[0].state -ceq 'deleted'){$Probe.fired=$true;throw 'simulated-write-failure'}
    }.GetNewClosure())
}
function Invoke-PromotedCleanupCase([string]$CaseName,$Env,[string]$Selection){
    Reset-PromotedHooks
    try{switch($CaseName){
        'deletion-route-table'{
            # AC6: the route for every state x origin x putIntent x receipt; only provably unsent staged artifacts are local.
            $m=@(Get-EvidenceTestModules EvidenceCleanup)[0]
            foreach($state in @('put-pending','ready','delete-pending','deleted','failed','staged')){foreach($origin in @('stage','publish','none')){foreach($intent in @($false,$true)){foreach($receipt in @($false,$true)){
                $a=[pscustomobject]@{state=$state;putIntent=if($intent){@{path='p';sha256='0'*64}}else{$null};receipt=if($receipt){@{path='r';sha256='1'*64}}else{$null}};if($origin -cne 'none'){$a|Add-Member -NotePropertyName origin -NotePropertyValue $origin}
                $expected=if($state -ceq 'ready'){'remote'}elseif($state -ceq 'staged' -and $origin -ceq 'stage' -and -not $receipt){if($intent){'remote'}else{'local'}}else{'none'}
                $actual=& $m {param($x)Get-EvidenceDeletionRoute $x} $a;Assert-EvidenceTest ($actual -ceq $expected) ("route $state/$origin/intent=$intent/receipt=$receipt -> $actual")
            }}}}
        }
        'local-delete-recheck'{
            # AC6: handed a staged artifact that may have been sent, the local deletion itself refuses and writes nothing.
            $x=New-PromotedSentStaged $Env $Selection;$deadline=Complete-PromotedTask;Set-PromotedNow $deadline
            $i=Invoke-EvidenceInspect $Env.ConfigPath fixture;Assert-EvidenceTest ($i.artifacts[0].policy.reasonCode -ceq 'expired') ('policy '+$i.artifacts[0].policy.reasonCode)
            $catalog=[IO.Path]::Combine((Get-EvidenceTaskDirectory $Env.Config fixture),'catalog.json');$before=[IO.File]::ReadAllBytes($catalog);$m=@(Get-EvidenceTestModules EvidenceCleanup)[0]
            $r=& $m {param($cfg)$g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$t=Read-EvidenceTask $cfg fixture $g;Invoke-EvidenceLocalDeleteGuarded $cfg $t $t.artifacts[0] $g}finally{$g.Dispose()}} $Env.Config
            Assert-EvidenceTest ($r -ceq 'unknown-state' -and [Linq.Enumerable]::SequenceEqual([byte[]]$before,[byte[]][IO.File]::ReadAllBytes($catalog)) -and [IO.File]::Exists($x.Zip)) ('local delete of sent staged '+$r)
        }
        'route-none-unchanged'{
            # I1/AC4: staged records without origin or from publish are unknown-state with route none: no DELETE and no local deletion.
            foreach($variant in @('no-origin','publish')){
                $p=New-PromotedEnv;$e=$p.Env;$x=New-PromotedStage $e $p.Selection
                Edit-FixtureTask $e {param($t)$a=$t.artifacts[0];if($variant -ceq 'no-origin'){$a.PSObject.Properties.Remove('origin')}else{$a.origin='publish'}}
                $deadline=Complete-PromotedTask;Set-PromotedNow $deadline;$m=@(Get-EvidenceTestModules EvidenceCleanup)[0];$a=Get-PromotedArtifact $e $x.Id
                Assert-EvidenceTest ((& $m {param($y)Get-EvidenceDeletionRoute $y} $a) -ceq 'none') ($variant+' route')
                $c=Invoke-EvidenceCleanup $e.ConfigPath;$a=Get-PromotedArtifact $e $x.Id
                Assert-EvidenceTest ((Get-PromotedItem $c $x.Id)[0].reasonCode -ceq 'unknown-state' -and $a.state -ceq 'staged' -and [IO.File]::Exists($x.Zip) -and (Get-EvidenceRemoteTouches $e) -eq 0) ($variant+' cleanup '+(ConvertTo-Json $c -Compress -Depth 5))
            }
            # Publish-origin recovery is unchanged: 404 -> failed, and a match adopts every staging of the artifact.
            foreach($stored in @($false,$true)){
                $p=New-PromotedEnv;$e=$p.Env;$inner=$e.Transport;$objects=$e.Objects;$counts=$e.Counts
                Set-EvidenceTestValue ArtifactApplication TransportHook ({param($g,$req)if($req.Operation -ceq 'put'){if($stored){$null=& $inner $g $req}else{$null=$counts.AddOrUpdate('put '+$req.Key,1,{param($k,$v)$v+1})};throw 'response-lost'};& $inner $g $req}.GetNewClosure())
                $pub=Invoke-EvidencePublish $e.ConfigPath $p.Selection;Set-EvidenceTestValue ArtifactApplication TransportHook $e.Transport;$id=(Read-PromotedDisk $e).artifacts[0].artifactId
                $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$null=Assert-EvidenceTransitionSafe $e.Config fixture $g}finally{$g.Dispose()};$t=Read-PromotedDisk $e
                if($stored){Assert-EvidenceTest ($t.artifacts[0].state -ceq 'ready' -and @($t.staging|Where-Object {$_.artifactId -ceq $id -and -not $_.adopted}).Count -eq 0 -and @($t.staging|Where-Object artifactId -CEQ $id).Count -ge 2) 'publish match adoption'}
                else{Assert-EvidenceTest ($pub.status -ceq 'failed' -and $t.artifacts[0].state -ceq 'failed') 'publish 404'}
            }
        }
        'promoted-reader-shapes'{
            # I9/AC9: every shape the previous runtime writes for stage-origin artifacts still reads, beside a publish artifact without origin.
            $plain=New-PromotedStage $Env $Selection
            $failed=@('i','ii','iii'|ForEach-Object {New-PromotedFailed $Env $Selection $_})
            $ready=New-PromotedStage $Env $Selection;$pr=Invoke-EvidencePromote $Env.ConfigPath $ready.Stage.reference $ready.Stage.packageSha256;Assert-EvidenceTest ($pr.status -ceq 'passed') 'promote ready'
            $pub=Invoke-EvidencePublish $Env.ConfigPath $Selection;Assert-EvidenceTest ($pub.status -ceq 'passed') 'publish'
            $pending=New-PromotedStage $Env $Selection;Invoke-PromotedStop $Env $pending 'promote-after-put-pending'
            $t=Read-PromotedDisk $Env;Assert-EvidenceTest (@($t.artifacts|ForEach-Object state) -join ',' -ceq 'staged,failed,failed,failed,ready,ready,put-pending') ('shapes '+(@($t.artifacts|ForEach-Object state) -join ','))
            Assert-EvidenceTest (@($t.artifacts|Where-Object {-not $_.PSObject.Properties['origin']}).Count -eq 1) 'publish without origin'
            $catalog=[IO.Path]::Combine((Get-EvidenceTaskDirectory $Env.Config fixture),'catalog.json');$original=[IO.File]::ReadAllText($catalog)
            $edit={param([scriptblock]$Change)$o=$original|ConvertFrom-Json -DateKind String;& $Change $o;Write-EvidenceTestJson $catalog $o;$r='read';try{$null=Read-PromotedDisk $Env}catch{$r=$_.Exception.Message};return $r}
            $intent=[pscustomobject]@{key=$ready.Key;generation=1;packageSha256='0'*64;process=[pscustomobject]@{pid=1;startedAt=$script:PromotedT0.ToString('o')};createdAt=$script:PromotedT0.ToString('o')}
            $find={param($o,$id)@($o.artifacts|Where-Object artifactId -CEQ $id)[0]}
            Assert-EvidenceTest ((& $edit {param($o)$a=& $find $o $ready.Id;$a.state='delete-pending';$a.deleteIntent=$intent}) -ceq 'read') 'delete-pending with receipt'
            Assert-EvidenceTest ((& $edit {param($o)$a=& $find $o $ready.Id;$a.state='deleted'}) -ceq 'read') 'deleted promoted'
            Assert-EvidenceTest ((& $edit {param($o)$a=& $find $o $plain.Id;$a.state='deleted'}) -ceq 'read') 'deleted staged'
            Assert-EvidenceTest ((& $edit {param($o)$a=& $find $o $failed[0].Id;$a.state='delete-pending';$a.deleteIntent=$intent}) -ceq 'read') 'delete-pending with intent and no receipt'
            Assert-EvidenceTest ((& $edit {param($o)$a=& $find $o $plain.Id;$a.state='delete-pending';$a.deleteIntent=$intent}) -ceq 'receipt-missing') 'delete-pending without intent or receipt'
            [IO.File]::WriteAllText($catalog,$original,[Text.UTF8Encoding]::new($false));$null=Read-PromotedDisk $Env
        }
        'promoted-match-adoption'{
            # AC5: a promotion's recovery match adopts only its own staging; the stopped promote's readback staging, with an unregistered file, meets the seven-day rule.
            $x=New-PromotedStage $Env $Selection;$t0=Read-PromotedDisk $Env;$original=@($t0.staging|Where-Object {$_.artifactId -ceq $x.Id -and $_.adopted}|ForEach-Object path)
            Invoke-PromotedStop $Env $x 'promote-after-put-pending';$marker=[IO.Path]::Combine($x.Readback,'readback-process.json')
            Write-EvidenceTestJson $marker ([pscustomobject]@{process=@{pid=2147483647;startedAt='2000-01-01T00:00:00.0000000Z'};exited=$true;pipesClosed=$true});Set-ArtifactAcl $marker $false
            $Env.Objects[$x.Key]=$x.ZipBytes;$known=@((Read-PromotedDisk $Env).staging|ForEach-Object path);Set-PromotedNow $script:PromotedT0.AddDays(8)
            $c=Invoke-EvidenceCleanup $Env.ConfigPath;$t=Read-PromotedDisk $Env;$a=@($t.artifacts|Where-Object artifactId -CEQ $x.Id)[0];$new=@($t.staging|Where-Object path -CNotIn $known)
            Assert-EvidenceTest ($a.state -ceq 'ready' -and $null -ne $a.receipt -and $new.Count -eq 1 -and $new[0].adopted -and $null -eq $new[0].operation) ('match '+(ConvertTo-Json $c -Compress -Depth 5))
            Assert-EvidenceTest (@($t.staging|Where-Object path -CEQ $x.Readback).Count -eq 0 -and -not [IO.Directory]::Exists($x.Readback)) 'readback staging kept'
            Assert-EvidenceTest (@($t.staging|Where-Object {$_.path -cin $original -and $_.adopted}).Count -eq $original.Count -and [IO.File]::Exists($x.Zip) -and @($a.ownedCopies|Where-Object path -CEQ $x.Zip).Count -eq 1) 'stage staging or zip changed'
        }
        'sent-staged-expiry'{
            # S3/I2/I7/AC6: a staged artifact with a putIntent is retained until the deadline, then deleted by DELETE and an absence GET; an unsent one beside it is deleted locally.
            $x=New-PromotedSentStaged $Env $Selection;$y=New-PromotedStage $Env $Selection;$deadline=Complete-PromotedTask
            $ops=[Collections.Generic.List[string]]::new();$inner=$Env.Transport;Set-EvidenceTestValue ArtifactApplication TransportHook ({param($g,$req)$ops.Add($req.Operation+' '+$req.Key);& $inner $g $req}.GetNewClosure())
            Set-PromotedNow $deadline.AddTicks(-1);$c=Invoke-EvidenceCleanup $Env.ConfigPath
            Assert-EvidenceTest ((Get-PromotedItem $c $x.Id)[0].reasonCode -ceq 'retained' -and $ops.Count -eq 0 -and (Get-PromotedArtifact $Env $x.Id).state -ceq 'staged') ('before deadline '+(ConvertTo-Json $c -Compress -Depth 5))
            Set-PromotedNow $deadline;$c=Invoke-EvidenceCleanup $Env.ConfigPath;$a=Get-PromotedArtifact $Env $x.Id;$b=Get-PromotedArtifact $Env $y.Id
            Assert-EvidenceTest ((Get-PromotedItem $c $x.Id)[0].reasonCode -ceq 'deleted' -and (@($ops) -join '|') -ceq ('delete '+$x.Key+'|get '+$x.Key) -and $a.state -ceq 'deleted' -and @($a.ownedCopies).Count -eq 0 -and -not [IO.File]::Exists($x.Zip)) ('deadline '+(@($ops) -join '|'))
            Assert-EvidenceTest ((Get-PromotedItem $c $y.Id)[0].reasonCode -ceq 'deleted' -and $b.state -ceq 'deleted' -and -not [IO.File]::Exists($y.Zip)) 'unsent staged'
        }
        'sent-staged-delete-stops'{
            # S3/I2/AC6 (a)(b)(d): a stop at each point of the remote deletion leaves delete-pending with the ZIP, and the next cleanup converges.
            foreach($scenario in @('after-intent:match','after-intent:missing','after-remote:missing','delete-fails:match','delete-fails:missing')){
                $point,$remote=$scenario.Split(':');$p=New-PromotedEnv;$e=$p.Env;$x=New-PromotedSentStaged $e $p.Selection;$deadline=Complete-PromotedTask;Set-PromotedNow $deadline
                if($point -ceq 'delete-fails'){$inner=$e.Transport;Set-EvidenceTestValue ArtifactApplication TransportHook ({param($g,$req)if($req.Operation -ceq 'delete'){throw 'delete-lost'};& $inner $g $req}.GetNewClosure())}
                else{Set-EvidenceTestValue EvidenceCleanup FaultHook ({param($stage,$a)if($stage -ceq $point){throw 'simulated-stop'}}.GetNewClosure())}
                $c=Invoke-EvidenceCleanup $e.ConfigPath;Set-EvidenceTestValue EvidenceCleanup FaultHook $null;Set-EvidenceTestValue ArtifactApplication TransportHook $e.Transport
                $a=Get-PromotedArtifact $e $x.Id
                Assert-EvidenceTest ($c.status -ceq 'pending' -and $a.state -ceq 'delete-pending' -and $null -eq $a.receipt -and $null -ne $a.putIntent -and [IO.File]::Exists($x.Zip)) ($scenario+' stopped '+$a.state)
                if($remote -ceq 'match'){$e.Objects[$x.Key]=$x.ZipBytes}else{$unused=$null;$null=$e.Objects.TryRemove($x.Key,[ref]$unused)}
                # Every persisted state of the artifact during the resume, with whether its ZIP existed then.
                $seen=[Collections.Generic.List[string]]::new();$id=$x.Id;$zip=$x.Zip
                Set-EvidenceTestValue EvidencePaths WriteHook ({param($st,$path)if($st -ceq 'after-rename' -and $path.EndsWith('\catalog.json')){$s=@(([IO.File]::ReadAllText($path)|ConvertFrom-Json).artifacts|Where-Object artifactId -CEQ $id)[0].state;if($seen.Count -eq 0 -or -not $seen[$seen.Count-1].StartsWith($s+':')){$seen.Add($s+':'+[IO.File]::Exists($zip))}}}.GetNewClosure())
                $deletes=[int]$e.Counts['delete '+$x.Key];try{$c=Invoke-EvidenceCleanup $e.ConfigPath}finally{Set-EvidenceTestValue EvidencePaths WriteHook $null};$a=Get-PromotedArtifact $e $x.Id;$trace=@($seen) -join ','
                Assert-EvidenceTest ($c.status -ceq 'passed' -and $a.state -ceq 'deleted' -and -not [IO.File]::Exists($x.Zip)) ($scenario+' resumed '+(ConvertTo-Json $c -Compress -Depth 5))
                # A match returns it to staged with its ZIP, and the same cleanup deletes it again with a new intent and DELETE; a 404 ends at deleted directly.
                if($remote -ceq 'match'){Assert-EvidenceTest ($trace -ceq 'delete-pending:True,staged:True,delete-pending:True,deleted:True' -and $e.Counts['delete '+$x.Key] -eq $deletes+1) ($scenario+' trace '+$trace)}
                else{Assert-EvidenceTest ($trace -ceq 'delete-pending:True,deleted:True' -and [int]$e.Counts['delete '+$x.Key] -eq $deletes) ($scenario+' trace '+$trace)}
                Reset-PromotedHooks
            }
        }
        'sent-staged-delete-write-failure'{
            # I2/AC6 (c): the write of `deleted` fails before or after its rename, in the deletion and in its reconciliation; disk delete-pending always keeps the ZIP, and the next cleanup converges.
            foreach($scenario in @('normal:before-rename','normal:after-rename','reconcile:before-rename','reconcile:after-rename','ready:before-rename')){
                $route,$phase=$scenario.Split(':');$p=New-PromotedEnv;$e=$p.Env
                if($route -ceq 'ready'){$x=New-PromotedStage $e $p.Selection;$r=Invoke-EvidencePromote $e.ConfigPath $x.Stage.reference $x.Stage.packageSha256;Assert-EvidenceTest ($r.status -ceq 'passed') 'promote'}else{$x=New-PromotedSentStaged $e $p.Selection}
                $deadline=Complete-PromotedTask;Set-PromotedNow $deadline;Set-EvidenceTestValue EvidenceCleanup ProcessHook {param($identity)$true}
                if($route -ceq 'reconcile'){Set-EvidenceTestValue EvidenceCleanup FaultHook {param($stage,$a)if($stage -ceq 'after-remote'){throw 'simulated-stop'}};$null=Invoke-EvidenceCleanup $e.ConfigPath;Set-EvidenceTestValue EvidenceCleanup FaultHook $null;Assert-EvidenceTest ((Get-PromotedArtifact $e $x.Id).state -ceq 'delete-pending') 'reconcile setup'}
                $probe=@{fired=$false};Set-PromotedDeletedWriteFailure $x.Id $phase $probe
                try{$null=Invoke-EvidenceCleanup $e.ConfigPath}finally{Set-EvidenceTestValue EvidencePaths WriteHook $null}
                $a=Get-PromotedArtifact $e $x.Id;$zip=[IO.File]::Exists($x.Zip);Assert-EvidenceTest $probe.fired ($scenario+' not fired')
                Assert-EvidenceTest (($a.state -ceq 'delete-pending' -and $zip) -or $a.state -ceq 'deleted') ($scenario+' unsafe '+$a.state+' zip='+$zip)
                [Console]::WriteLine('AC6c '+$scenario+' endpoint='+$a.state+' zip='+$zip)
                $c=Invoke-EvidenceCleanup $e.ConfigPath;$a=Get-PromotedArtifact $e $x.Id
                Assert-EvidenceTest ($c.status -ceq 'passed' -and $a.state -ceq 'deleted' -and -not [IO.File]::Exists($x.Zip) -and -not $e.Objects.ContainsKey($x.Key)) ($scenario+' converged '+(ConvertTo-Json $c -Compress -Depth 5))
                Reset-PromotedHooks
            }
        }
        'failed-recovery-cleanup'{
            # S2/AC3 (d): cleanup resolves a recoverable failed promotion as the commands do; an unresolved one is this item's unconfirmed-artifact.
            foreach($variant in @('i','ii','iii')){foreach($mode in @('missing','match','other')){
                $p=New-PromotedEnv;$e=$p.Env;$x=New-PromotedFailed $e $p.Selection $variant
                if($mode -ceq 'match'){$e.Objects[$x.Key]=$x.ZipBytes}elseif($mode -ceq 'other'){$e.Objects[$x.Key]=[byte[]](1..64)};$adopted=@((Read-PromotedDisk $e).staging|Where-Object adopted|ForEach-Object path)
                $c=Invoke-EvidenceCleanup $e.ConfigPath;$t=Read-PromotedDisk $e;$a=@($t.artifacts|Where-Object artifactId -CEQ $x.Id)[0];$item=(Get-PromotedItem $c $x.Id)[0].reasonCode;$newly=@($t.staging|Where-Object {$_.adopted -and $_.path -cnotin $adopted})
                switch($mode){
                    'missing'{Assert-EvidenceTest ($a.state -ceq 'staged' -and $null -ne $a.putIntent -and $newly.Count -eq 0) ("$variant/$mode "+$a.state)}
                    'match'{Assert-EvidenceTest ($a.state -ceq 'ready' -and $null -ne $a.receipt -and $newly.Count -eq 1 -and @($t.staging|Where-Object {$_.path -ceq $x.Readback -and $_.adopted}).Count -eq 0) ("$variant/$mode "+$a.state)}
                    'other'{Assert-EvidenceTest ($a.state -ceq 'failed' -and $item -ceq 'unconfirmed-artifact' -and @($e.Counts.Keys|Where-Object {$_ -like 'put *'}).Count -eq 0) ("$variant/$mode "+$a.state+' '+$item)}
                }
                Reset-PromotedHooks
            }}
            # With the stopped promote still running, cleanup neither registers nor fetches anything for it.
            $p=New-PromotedEnv;$e=$p.Env;$x=New-PromotedFailed $e $p.Selection 'i';Set-EvidenceTestValue EvidenceCleanup ProcessHook {param($identity)$false}
            $before=Get-FixtureArtifactsHash $e;$staging=ConvertTo-Json -Compress -Depth 8 -InputObject (Read-PromotedDisk $e).staging
            $c=Invoke-EvidenceCleanup $e.ConfigPath
            Assert-EvidenceTest ((Get-PromotedItem $c $x.Id)[0].reasonCode -ceq 'unconfirmed-artifact' -and -not $e.Counts.ContainsKey('get '+$x.Key) -and (Get-FixtureArtifactsHash $e) -ceq $before -and (ConvertTo-Json -Compress -Depth 8 -InputObject (Read-PromotedDisk $e).staging) -ceq $staging) 'running promote touched'
        }
        'failed-recovery-cleanup-isolation'{
            # I10/AC3 (d): an unresolvable failed item ends its task's run; items before it are done in the same run, those after it in the next, and it stays failed.
            $x=New-PromotedFailed $Env $Selection 'i';$Env.Objects[$x.Key]=[byte[]](1..64)
            $r=New-PromotedStage $Env $Selection;$pr=Invoke-EvidencePromote $Env.ConfigPath $r.Stage.reference $r.Stage.packageSha256;Assert-EvidenceTest ($pr.status -ceq 'passed') 'promote other'
            $u=New-PromotedStage $Env $Selection;$deadline=Complete-PromotedTask;Set-PromotedNow $deadline.AddSeconds(1)
            # The three artifact ids form a circular order; a stored cursor starts the run so that one other artifact comes before the failed item and one after it.
            $sorted=[Collections.Generic.List[string]]::new([string[]]@($x.Id,$r.Id,$u.Id));$sorted.Sort([StringComparer]::Ordinal);$at=$sorted.IndexOf($x.Id)
            $startCursor=switch($at){0{$sorted[1]}1{$null}2{$sorted[0]}};Edit-FixtureTask $Env {param($t)$t.cleanupCursor=$startCursor}
            $dry=Invoke-EvidenceCleanup $Env.ConfigPath $true;Assert-EvidenceTest (@(Get-PromotedItem $dry $x.Id|Where-Object reasonCode -CEQ 'unconfirmed-artifact').Count -eq 1 -and -not $Env.Counts.ContainsKey('get '+$x.Key)) 'dry run recovered'
            $first=Invoke-EvidenceCleanup $Env.ConfigPath
            # A failed recovery ends the run, so its item is the last one; the failed artifact's own unadopted staging may meet the seven-day rule before it.
            Assert-EvidenceTest ($first.items[-1].artifactId -ceq $x.Id -and $first.items[-1].reasonCode -ceq 'unconfirmed-artifact' -and $first.status -ceq 'pending' -and $first.remaining -ge 1) ('first run '+(ConvertTo-Json $first -Compress -Depth 5))
            $beforeFailed=switch($at){0{@($sorted[2])}1{@($sorted[0])}2{@($sorted[1])}};$checked=@{before=0;after=0}
            foreach($other in @($r,$u)){
                $state=(Get-PromotedArtifact $Env $other.Id).state
                if($other.Id -cin $beforeFailed){$checked.before++;Assert-EvidenceTest ($state -ceq 'deleted') ('before failed item '+$state)}else{$checked.after++;Assert-EvidenceTest ($state -cne 'deleted' -and [IO.File]::Exists($other.Zip)) ('after failed item '+$state)}
            }
            Assert-EvidenceTest ($checked.before -eq 1 -and $checked.after -eq 1) 'order not exercised'
            $second=Invoke-EvidenceCleanup $Env.ConfigPath
            Assert-EvidenceTest ((Get-PromotedArtifact $Env $r.Id).state -ceq 'deleted' -and (Get-PromotedArtifact $Env $u.Id).state -ceq 'deleted' -and (Get-PromotedArtifact $Env $x.Id).state -ceq 'failed' -and $second.items[-1].artifactId -ceq $x.Id -and $second.items[-1].reasonCode -ceq 'unconfirmed-artifact' -and -not $Env.Objects.ContainsKey($r.Key) -and [IO.File]::Exists($x.Zip)) ('second run '+(ConvertTo-Json $second -Compress -Depth 5))
        }
        'failed-recovery-cleanup-write-failure'{
            # AC3 (d): a recovery that fails after its match has changed memory leaves disk failed, writes nothing more of the task in that run, and the next run reaches ready.
            $x=New-PromotedFailed $Env $Selection 'i';$Env.Objects[$x.Key]=$x.ZipBytes;$u=New-PromotedStage $Env $Selection
            $probe=@{armed=$false;fired=$false};$innerRead=$Env.Readback
            Set-EvidenceTestValue ArtifactApplication ReadbackHook ({param($op,$c,$gen,$key,$bytes,$hash)$probe.armed=$true;& $innerRead $op $c $gen $key $bytes $hash}.GetNewClosure())
            Set-EvidenceTestValue EvidencePaths WriteHook ({param($st,$path)if($probe.armed -and -not $probe.fired -and $st -ceq 'before-rename' -and $path.EndsWith('\catalog.json')){$probe.fired=$true;throw 'simulated-write-failure'}}.GetNewClosure())
            try{$first=Invoke-EvidenceCleanup $Env.ConfigPath}finally{Set-EvidenceTestValue EvidencePaths WriteHook $null;Set-EvidenceTestValue ArtifactApplication ReadbackHook $Env.Readback}
            $a=Get-PromotedArtifact $Env $x.Id;$cursor=(Read-PromotedDisk $Env).cleanupCursor
            Assert-EvidenceTest ($probe.fired -and $a.state -ceq 'failed' -and $null -eq $a.receipt -and (Get-PromotedItem $first $x.Id)[0].reasonCode -ceq 'unconfirmed-artifact' -and $first.remaining -ge 1 -and $cursor -ceq $x.Id) ('first run '+$a.state+' '+(ConvertTo-Json $first -Compress -Depth 5))
            $second=Invoke-EvidenceCleanup $Env.ConfigPath;$a=Get-PromotedArtifact $Env $x.Id
            Assert-EvidenceTest ($a.state -ceq 'ready' -and $null -ne $a.receipt -and $second.blocked -eq 0) ('second run '+$a.state+' '+(ConvertTo-Json $second -Compress -Depth 5))
        }
    }}finally{Reset-PromotedHooks}
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
    if($caseName -cin $stagedCases){Invoke-StagedCleanupCase $caseName $env $selection;continue}
    if($caseName -cin $promotedCases){Invoke-PromotedCleanupCase $caseName $env $selection;continue}
    $p=Invoke-EvidencePublish $env.ConfigPath $selection;Assert-EvidenceTest ($p.status -ceq 'passed') 'publish';if($caseName -ceq 'fair-object-cursor'){$second=Invoke-EvidencePublish $env.ConfigPath $selection;Assert-EvidenceTest ($second.status -ceq 'passed') 'second publish'};$ref=Read-EvidenceReference $p.reference $env.Config;[IO.File]::WriteAllBytes([IO.Path]::Combine($env.Root,'remote.zip'),$env.Objects[$ref.key])
    $endAt=[DateTimeOffset]::Parse('2026-01-01T00:00:00.0000000+00:00');Set-EvidenceTestValue TaskLifecycle Clock {$endAt}.GetNewClosure();$g=Enter-WorkflowTaskGuard ('a'*64) fixture
    try{if($caseName -cnotin @('active-marker-protected','staging-adoption-success','staging-missing-report')){$null=Invoke-WorkflowTaskTransition ('a'*64) fixture ended 1 fixture-end completed $g};$t=Sync-EvidenceTaskGuarded $env.Config fixture $g;$a=$t.artifacts[0]}finally{$g.Dispose()}
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
        'staging-missing-report'{
            # AC1/AC10: the task stays active so publish can run; a missing leftover must be tolerated and counted.
            Assert-EvidenceTest ($p.stagingMissing -eq 0) 'clean task reported missing staging'
            $missing=New-TransfersPath $env;Edit-FixtureTask $env {param($t)$t.staging+=New-MissingStage $missing $now}
            $g=Enter-WorkflowTaskGuard ('a'*64) fixture
            try{$before=Get-FixtureCatalogHash $env;$residue=[pscustomobject]@{Observations=[Collections.Generic.List[object]]::new();Witnesses=0};$ids=@(Assert-EvidenceTransitionSafe $env.Config fixture $g -Residue $residue);$after=Get-FixtureCatalogHash $env}finally{$g.Dispose()}
            Assert-EvidenceTest ($residue.Observations.Count -eq 1 -and $residue.Observations[0].reasonCode -ceq 'staging-missing' -and $residue.Observations[0].key -ceq (Get-EvidenceStagingKey ([pscustomobject]@{path=$missing})) -and $residue.Witnesses -ge 1) 'observation or witness missing'
            Assert-EvidenceTest ($before -ceq $after) 'preflight rewrote catalog'
            # Witness counting must not leak into the pipeline: workflow-task reads that output as deleted artifact ids.
            Assert-EvidenceTest ($ids.Count -eq 0 -and $residue.Witnesses -ge 1) 'preflight output leaked witness counts'
            $dry=Invoke-EvidenceCleanup $env.ConfigPath $true @('fixture');$before=Get-FixtureCatalogHash $env
            Assert-EvidenceTest (@($dry.items|Where-Object reasonCode -CEQ 'unconfirmed').Count -eq 0 -and @($dry.items|Where-Object reasonCode -CEQ 'staging-missing').Count -eq 1 -and $dry.blocked -eq 0) ('dry-run did not report missing '+(ConvertTo-Json $dry -Compress -Depth 5))
            Assert-EvidenceTest ((Get-FixtureCatalogHash $env) -ceq $before) 'dry-run rewrote catalog'
            $again=Invoke-EvidencePublish $env.ConfigPath $selection;Assert-EvidenceTest ($again.status -ceq 'passed' -and $again.stagingMissing -eq 1) ('publish with missing staging '+$again.reasonCode)
            $fetched=Invoke-EvidenceFetch $env.ConfigPath $p.reference $p.packageSha256;Assert-EvidenceTest ($fetched.status -ceq 'passed' -and $fetched.stagingMissing -eq 1) 'fetch with missing staging'
            $used=Invoke-EvidenceUse $env.ConfigPath $p.reference reader $now.AddHours(1).ToString('o') review;Assert-EvidenceTest ($used.status -ceq 'passed' -and $used.stagingMissing -eq 1) 'use with missing staging'
            $sourceSelection=[ordered]@{}+$fixture.Selection;$sourceSelection.root=$missing;$sourcePath=[IO.Path]::Combine($env.Root,'missing-source.json');Write-EvidenceTestJson $sourcePath $sourceSelection
            $bad=Invoke-EvidencePublish $env.ConfigPath $sourcePath;Assert-EvidenceTest ($bad.status -ceq 'failed' -and $bad.reasonCode -ceq 'invalid-source' -and $bad.stagingMissing -eq 1) 'missing directory accepted as source'
            Set-EvidenceTestValue ArtifactApplication TransportHook {throw 'DUMMY_SECRET'}
            $lostPublish=Invoke-EvidencePublish $env.ConfigPath $selection;Assert-EvidenceTest ($lostPublish.status -ceq 'failed' -and $lostPublish.stagingMissing -eq 1) 'failed publish lost the count'
            continue
        }
        'staging-missing-drop'{
            # AC4: a missing entry leaves the catalog only at the seven-day boundary, with nothing deleted.
            $base=$endAt.AddDays(10);$tick=@{v=$base.AddSeconds(-1)};Set-EvidenceTestValue EvidenceCleanup Clock {$tick.v}.GetNewClosure()
            $missing=New-TransfersPath $env;$sentinel=New-TransfersPath $env;New-FixtureDirectory $sentinel;[IO.File]::WriteAllText([IO.Path]::Combine($sentinel,'keep.txt'),'keep')
            $g=Enter-WorkflowTaskGuard ('a'*64) other;try{$null=Invoke-WorkflowTaskTransition ('a'*64) other started 0 other-start '' $g;$null=Sync-EvidenceTaskGuarded $env.Config other $g}finally{$g.Dispose()}
            Edit-FixtureTask $env {param($t)$t.staging+=New-MissingStage $missing $base.AddSeconds(-604800)}
            $otherHash=Get-FixtureCatalogHash $env other;$artifactsHash=Get-FixtureArtifactsHash $env;$tree=Get-TransfersSignature $env
            $dry=Invoke-EvidenceCleanup $env.ConfigPath $true @('fixture');$beforeRun=Get-FixtureCatalogHash $env
            Assert-EvidenceTest (@($dry.items|Where-Object reasonCode -CEQ 'staging-missing').Count -eq 1) 'dry-run before expiry'
            $early=Invoke-EvidenceCleanup $env.ConfigPath $false @('fixture');Assert-EvidenceTest (@($early.items|Where-Object reasonCode -CEQ 'staging-missing').Count -eq 1 -and $early.deleted -eq 0 -and @((Get-FixtureStaging $env)|Where-Object path -CEQ $missing).Count -eq 1) 'entry dropped before expiry'
            $tick.v=$base
            $beforeRun=Get-FixtureCatalogHash $env;$dry=Invoke-EvidenceCleanup $env.ConfigPath $true @('fixture')
            Assert-EvidenceTest (@($dry.items|Where-Object reasonCode -CEQ 'staging-missing').Count -eq 1 -and (Get-FixtureCatalogHash $env) -ceq $beforeRun) 'dry-run at expiry changed state'
            $clean=Invoke-EvidenceCleanup $env.ConfigPath $false @('fixture')
            Assert-EvidenceTest (@($clean.items|Where-Object reasonCode -CEQ 'staging-missing-dropped').Count -eq 1 -and $clean.deleted -eq 0 -and $clean.blocked -eq 0 -and $clean.skipped -eq $clean.items.Count) ('drop accounting '+(ConvertTo-Json $clean -Compress -Depth 5))
            Assert-EvidenceTest (@((Get-FixtureStaging $env)|Where-Object path -CEQ $missing).Count -eq 0) 'expired entry kept'
            Assert-EvidenceTest ((Get-FixtureArtifactsHash $env) -ceq $artifactsHash -and (Get-TransfersSignature $env) -ceq $tree -and (Get-FixtureCatalogHash $env other) -ceq $otherHash -and $env.Objects.Count -eq 1) 'drop changed more than the entry'
            continue
        }
        'staging-missing-no-witness'{
            # AC8: with no registered path confirmed to exist on this store side, an expired entry stays.
            $base=$endAt.AddDays(10);Set-EvidenceTestValue EvidenceCleanup Clock {$base}.GetNewClosure()
            $missing=New-TransfersPath $env;Edit-FixtureTask $env {param($t)$t.artifacts=@();$t.staging=@(New-MissingStage $missing $base.AddSeconds(-604800));$t.cleanupCursor=$null}
            $clean=Invoke-EvidenceCleanup $env.ConfigPath $false @('fixture');Assert-EvidenceTest (@($clean.items|Where-Object reasonCode -CEQ 'staging-missing').Count -eq 1 -and $clean.blocked -eq 0 -and @((Get-FixtureStaging $env)|Where-Object path -CEQ $missing).Count -eq 1) 'entry dropped without a witness'
            $present=New-TransfersPath $env;New-FixtureDirectory $present;Edit-FixtureTask $env {param($t)$t.staging+=New-MissingStage $present $base}
            $clean=Invoke-EvidenceCleanup $env.ConfigPath $false @('fixture');Assert-EvidenceTest (@($clean.items|Where-Object reasonCode -CEQ 'staging-missing-dropped').Count -eq 1 -and [IO.Directory]::Exists($present)) 'witness did not allow the drop'
            continue
        }
        'staging-missing-race'{
            # AC7: a directory that appears after the missing verdict goes through ordinary deletion, never a bare drop.
            $base=$endAt.AddDays(10);Set-EvidenceTestValue EvidenceCleanup Clock {$base}.GetNewClosure()
            $missing=New-TransfersPath $env;Edit-FixtureTask $env {param($t)$t.staging+=New-MissingStage $missing $base.AddSeconds(-604800)}
            Set-EvidenceTestValue EvidenceCleanup FaultHook {param($point,$subject)if($point -ceq 'before-staging-drop'){New-FixtureDirectory $subject.path;[IO.File]::WriteAllText([IO.Path]::Combine($subject.path,'late.log'),'late')}}
            $clean=Invoke-EvidenceCleanup $env.ConfigPath $false @('fixture')
            Assert-EvidenceTest (@($clean.items|Where-Object reasonCode -CEQ 'deleted').Count -eq 1 -and $clean.deleted -eq 1 -and -not [IO.Directory]::Exists($missing) -and @((Get-FixtureStaging $env)|Where-Object path -CEQ $missing).Count -eq 0) ('late directory not deleted normally '+(ConvertTo-Json $clean -Compress -Depth 5))
            continue
        }
        'staging-missing-refuse'{
            # AC5/AC6: only a plain dormant leftover is tolerated. Every case below keeps one catalogued
            # stage and no artifact (unless stated), so a refusal can only come from the staging path.
            $base=$endAt.AddDays(10);Set-EvidenceTestValue EvidenceCleanup Clock {$base}.GetNewClosure()
            $transfers=[IO.Path]::Combine($env.Root,'transfers');$outside=[IO.Path]::Combine($env.Root,'refuse-outside');[IO.Directory]::CreateDirectory($outside)|Out-Null
            $made=[Collections.Generic.List[string]]::new()
            function Test-Refusal([string]$Label,[scriptblock]$Build,[bool]$KeepArtifacts=$false,[string]$PublishCode='unconfirmed'){
                $stage=& $Build
                Edit-FixtureTask $env {param($t)if(-not $KeepArtifacts){$t.artifacts=@()};$t.staging=@($stage);$t.cleanupCursor=$null}
                $dry=Invoke-EvidenceCleanup $env.ConfigPath $true @('fixture')
                Assert-EvidenceTest (@($dry.items|Where-Object reasonCode -CEQ 'unconfirmed').Count -ge 1 -and @($dry.items|Where-Object reasonCode -CEQ 'staging-missing').Count -eq 0) ($Label+' cleanup tolerated: '+(ConvertTo-Json $dry -Compress -Depth 5))
                $sel=Invoke-EvidencePublish $env.ConfigPath $selection;Assert-EvidenceTest ($sel.status -ceq 'failed' -and $sel.reasonCode -ceq $PublishCode) ($Label+' publish tolerated: '+$sel.reasonCode)
            }
            # These two need the published artifact, so they run before any case clears the artifact list.
            $artifactId=$a.artifactId
            Test-Refusal 'put-pending artifact' {$s=New-MissingStage (New-TransfersPath $env) $base;$s.artifactId=$artifactId;Edit-FixtureTask $env {param($t)$t.artifacts[0].state='put-pending'};$s} $true
            Test-Refusal 'artifact delete intent' {$s=New-MissingStage (New-TransfersPath $env) $base;$s.artifactId=$artifactId;Edit-FixtureTask $env {param($t)$t.artifacts[0].state='delete-pending';$t.artifacts[0].deleteIntent=[pscustomobject]@{key=$t.artifacts[0].key;generation=$t.generation;packageSha256=$t.artifacts[0].packageSha256;process=Get-EvidenceProcessIdentity;createdAt=$base.ToString('o')}};$s} $true
            # I6: a putIntent that no longer matches stays unresolved-put even beside a tolerable leftover.
            Test-Refusal 'unresolved put' {Edit-FixtureTask $env {param($t)$t.artifacts[0].putIntent.sha256='0'*64};New-MissingStage (New-TransfersPath $env) $base} $true 'unresolved-put'
            # AC6: an artifact copy registered under the stage path. The copy's file is absent, so the
            # artifact check refuses the whole task; the leftover must not be tolerated beside it.
            Test-Refusal 'owned copy under stage' {$s=New-MissingStage (New-TransfersPath $env) $base;$ownedPath=[IO.Path]::Combine($s.path,'late.bin');Edit-FixtureTask $env {param($t)$t.artifacts[0].ownedCopies[0].path=$ownedPath};$s} $true
            # Control: the same plain leftover is tolerated, so the later refusals are caused by their one difference.
            Edit-FixtureTask $env {param($t)$t.artifacts=@();$t.staging=@(New-MissingStage (New-TransfersPath $env) $base);$t.cleanupCursor=$null}
            $control=Invoke-EvidenceCleanup $env.ConfigPath $true @('fixture');Assert-EvidenceTest (@($control.items|Where-Object reasonCode -CEQ 'staging-missing').Count -eq 1 -and $control.blocked -eq 0) 'control not tolerated'
            $controlPublish=Invoke-EvidencePublish $env.ConfigPath $selection;Assert-EvidenceTest ($controlPublish.reasonCode -ceq 'resume-required' -and $controlPublish.stagingMissing -eq 1) ('control publish stopped in preflight '+$controlPublish.reasonCode)
            Test-Refusal 'non-hex leaf' {New-MissingStage ([IO.Path]::Combine($transfers,'not-a-staging-name')) $base}
            Test-Refusal 'outside root' {New-MissingStage ([IO.Path]::Combine($env.Root,'refuse-elsewhere',[Guid]::NewGuid().ToString('N'))) $base}
            Test-Refusal 'not a direct child' {New-MissingStage ([IO.Path]::Combine($transfers,[Guid]::NewGuid().ToString('N'),[Guid]::NewGuid().ToString('N'))) $base}
            Test-Refusal 'dangling junction' {$link=New-TransfersPath $env;$gone=[IO.Path]::Combine($env.Root,'refuse-gone-'+[Guid]::NewGuid().ToString('N'));[IO.Directory]::CreateDirectory($gone)|Out-Null;New-Item -ItemType Junction -Path $link -Target $gone|Out-Null;[IO.Directory]::Delete($gone);$made.Add($link);New-MissingStage $link $base}
            Test-Refusal 'live junction' {$link=New-TransfersPath $env;New-Item -ItemType Junction -Path $link -Target $outside|Out-Null;$made.Add($link);New-MissingStage $link $base}
            foreach($kind in @('SymbolicLink-file','SymbolicLink-directory')){
                $link=New-TransfersPath $env;$target=[IO.Path]::Combine($env.Root,'refuse-symlink-'+[Guid]::NewGuid().ToString('N'))
                try{if($kind -ceq 'SymbolicLink-directory'){[IO.Directory]::CreateDirectory($target)|Out-Null}else{[IO.File]::WriteAllText($target,'x')};New-Item -ItemType SymbolicLink -Path $link -Target $target|Out-Null}
                catch{[Console]::Error.WriteLine('NOTE staging-missing-refuse: '+$kind+' not creatable here, skipped');continue}
                if($kind -ceq 'SymbolicLink-directory'){[IO.Directory]::Delete($target)}else{[IO.File]::Delete($target)};$made.Add($link)
                $linkStage=New-MissingStage $link $base;Test-Refusal $kind {$linkStage}
            }
            Test-Refusal 'operation' {$s=New-MissingStage (New-TransfersPath $env) $base;$s.operation=[pscustomobject]@{process=Get-EvidenceProcessIdentity;childMarker=$null;path=$s.path};$s}
            Test-Refusal 'adoption pending' {$s=New-MissingStage (New-TransfersPath $env) $base;$s.adoptionPending=$true;$s}
            Test-Refusal 'adopted with operation' {$s=New-MissingStage (New-TransfersPath $env) $base;$s.adopted=$true;$s.operation=[pscustomobject]@{process=Get-EvidenceProcessIdentity;childMarker=$null;path=$s.path};$s}
            Test-Refusal 'protected' {$s=New-MissingStage (New-TransfersPath $env) $base;$s.protections=@([pscustomobject]@{id='p'});$s}
            Test-Refusal 'delete intent' {$s=New-MissingStage (New-TransfersPath $env) $base;$s.deleteIntent=[pscustomobject]@{key='k'};$s}
            Test-Refusal 'unknown artifact id' {$s=New-MissingStage (New-TransfersPath $env) $base;$s.artifactId='f'*32;$s}
            foreach($link in $made){try{[IO.Directory]::Delete($link)}catch{try{[IO.File]::Delete($link)}catch{}}}
            # The root itself: absent, or with an ACL the artifact store would never have created.
            Test-Refusal 'root absent' {[IO.Directory]::Delete($transfers,$true);New-MissingStage ([IO.Path]::Combine($transfers,[Guid]::NewGuid().ToString('N'))) $base}
            [IO.Directory]::CreateDirectory($transfers)|Out-Null;Set-ArtifactAcl $transfers $true
            Test-Refusal 'root ACL' {$acl=[IO.FileSystemAclExtensions]::GetAccessControl([IO.DirectoryInfo]::new($transfers));$acl.SetAccessRuleProtection($false,$true);[IO.FileSystemAclExtensions]::SetAccessControl([IO.DirectoryInfo]::new($transfers),$acl);New-MissingStage (New-TransfersPath $env) $base}
            Set-ArtifactAcl $transfers $true
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
    if($caseName -cin @('use-delete-race','resume-delete-race','staging-adoption-success','child-not-stopped','stopped-operation-cleanup','recovery-staging-cleanup','corrupt-lifecycle-projection','staging-fair-cursor','staging-result-accounting','deadline-request-and-guard','deadline-atomic-ack-retry','old-alias-cleanup','old-alias-fetch','old-alias-child-eof-blocked','old-alias-marker-unresolved','old-alias-copy-acl-blocked','missing-owned-copy','noncanonical-owned-copy','fresh-process-cleanup-contract','staging-missing-report','staging-missing-drop','staging-missing-no-witness','staging-missing-race','staging-missing-refuse')){continue}
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
