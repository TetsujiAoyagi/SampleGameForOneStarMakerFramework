param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$WarningPreference='SilentlyContinue'
# Consolidation I/O against two private temp stores. The "source" sits in a
# package LocalCache shape; the "real" store is a temp OneStarMaker root. The
# store-context decision is faked as not virtualized, so the host never matters,
# and no case reads or writes the real %LOCALAPPDATA%\OneStarMaker.
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
. (Join-Path $PSScriptRoot 'EvidenceReaderFixture.ps1')
Import-Module (Join-Path $PSScriptRoot '../StoreConsolidation.psm1') -Force
# A private instance: other modules re-import ArtifactAcl with -Force, which unloads
# any session copy; a removed module's commands still run through its own scope.
$aclModule=Import-Module (Join-Path $PSScriptRoot '../ArtifactAcl.psm1') -Force -PassThru
function Set-TestAcl([string]$Path,[bool]$Directory){& $aclModule {param($p,$d)Set-ArtifactAcl $p $d} $Path $Directory}
function Assert-TestAcl([string]$Path,[bool]$Directory){& $aclModule {param($p,$d)Assert-ArtifactAcl $p $d} $Path $Directory}
$repo='a'*64;$dep='d'*32;$op='0'*32
$cases=@('publish-skip-conflict','domain-readers-after-publish','acl-rule-and-reparse','excluded-never-opened','fault-file-write','fault-staged','fault-between-items','fault-before-receipt','concurrent-writer','hidden-until-workflow','run-guards','cli-exit-codes')
$selected=@(if($Case -ceq '*'){$cases}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
$base=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-consolidation-'+[Guid]::NewGuid().ToString('N'))
$trace=[Collections.Generic.List[string]]::new()
function Assert([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
function Set-Consolidation([string]$Name,$Value){Set-EvidenceTestValue StoreConsolidation $Name $Value}
$notVirtualized=@{Open={param($p)[pscustomobject]@{Path=$p}};FinalPath={param($h)$h.Path};Close={param($h)}}
function New-Stores([string]$Name){
    $root=[IO.Path]::Combine($base,$Name)
    $s=[pscustomobject]@{Root=$root;Real=[IO.Path]::Combine($root,'Local','OneStarMaker');Source=[IO.Path]::Combine($root,'Local','Packages','Fixture.Package_0000000000000','LocalCache','Local','OneStarMaker')}
    [IO.Directory]::CreateDirectory($s.Real)|Out-Null;[IO.Directory]::CreateDirectory($s.Source)|Out-Null
    Set-Consolidation TestRoot $s.Real;Set-Consolidation FaultHook $null
    Set-EvidenceTestValue WindowsStoreContext ProbeIo $notVirtualized
    $trace.Clear();Set-Consolidation OpenHook {param($k,$p)$trace.Add($k+'|'+$p)}.GetNewClosure()
    return $s
}
function Set-PrivateTree([string]$Path){
    if([IO.File]::Exists($Path)){Set-TestAcl $Path $false;return}
    Set-TestAcl $Path $true
    foreach($e in [IO.Directory]::EnumerateFileSystemEntries($Path,'*',[IO.SearchOption]::AllDirectories)){Set-TestAcl $e ([IO.Directory]::Exists($e))}
}
# Writes an item below a store root. Values are file contents keyed by relative path.
function Write-Item([string]$Root,[string]$Key,[Collections.IDictionary]$Files,[switch]$Private){
    $dir=[IO.Path]::Combine($Root,$Key);[IO.Directory]::CreateDirectory($dir)|Out-Null
    foreach($name in $Files.Keys){$p=[IO.Path]::Combine($dir,$name);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($p))|Out-Null;[IO.File]::WriteAllText($p,$Files[$name],[Text.UTF8Encoding]::new($false))}
    if($Private){Set-PrivateTree $dir}
}
function Write-Deployment([string]$Root,[switch]$Private,[string]$Marker='same'){
    $dir=[IO.Path]::Combine($Root,'Artifacts','deployments');[IO.Directory]::CreateDirectory($dir)|Out-Null
    foreach($name in @("$dep.json","$dep.config.json")){$p=[IO.Path]::Combine($dir,$name);[IO.File]::WriteAllText($p,'{"deployment":"'+$Marker+'"}',[Text.UTF8Encoding]::new($false));if($Private){Set-TestAcl $p $false}}
}
function Get-Snapshot([string]$Root){
    $map=[ordered]@{}
    if(-not [IO.Directory]::Exists($Root)){return $map}
    foreach($e in @([IO.Directory]::EnumerateFileSystemEntries($Root,'*',[IO.SearchOption]::AllDirectories)|Sort-Object)){
        $rel=[IO.Path]::GetRelativePath($Root,$e)
        $map[$rel]=if([IO.Directory]::Exists($e)){'dir'}else{(Get-FileHash -LiteralPath $e -Algorithm SHA256).Hash+'|'+[IO.File]::GetLastWriteTimeUtc($e).Ticks}
    }
    return $map
}
function Compare-Snapshot($Before,$After){return ((ConvertTo-Json -InputObject $Before -Compress) -ceq (ConvertTo-Json -InputObject $After -Compress))}
function Invoke-Plan($Stores){
    $out=[IO.Path]::Combine($Stores.Root,'manifest-'+[Guid]::NewGuid().ToString('N')+'.json')
    $r=Invoke-StoreConsolidationPlan $Stores.Source $out
    Assert ($r.status -ceq 'passed') ('plan failed '+$r.reasonCode)
    return [pscustomobject]@{Path=$out;Sha=$r.manifestSha256;Summary=$r.summary;Manifest=(Get-Content -LiteralPath $out -Raw|ConvertFrom-Json -AsHashtable)}
}
function Get-UnitOf($Plan,[string]$Key){foreach($u in $Plan.Manifest.units){foreach($i in $u.items){if($i.key -ceq $Key){return $u}}};throw ('no unit for '+$Key)}
function Get-Result($Outcome,[string]$UnitId){@($Outcome.units|Where-Object {$_.unitId -ceq $UnitId})[0].result}
function Get-Moves{@($trace|Where-Object {$_.StartsWith('move|')})}
function Assert-SameFiles([string]$From,[string]$To){
    $a=Get-Snapshot $From;$b=Get-Snapshot $To
    Assert ($a.Count -eq $b.Count) ('file count '+$From)
    foreach($k in $a.Keys){Assert ($b.Contains($k) -and ($a[$k].Split('|')[0]) -ceq ($b[$k].Split('|')[0])) ('content differs '+$k)}
}
# Three tasks and a deployment: A only in the source, B identical in both, C
# diverged (same event count, different Workflow history; different CURRENT).
function New-ClassificationFixture($Stores){
    $s=$Stores.Source;$r=$Stores.Real
    Write-Deployment $s;Write-Deployment $r -Private
    $catalog='{"artifacts":[{"receipt":{"path":"'+([IO.Path]::Combine($r,'Artifacts','evidence-v2',$dep,$repo,'tasks','alpha','receipts','x.json')).Replace('\','\\')+'"},"ownedCopies":[{"path":"'+([IO.Path]::Combine($r,'Artifacts','transfers',$op,'package.zip')).Replace('\','\\')+'"}]}]}'
    Write-Item $s "Artifacts\evidence-v2\$dep\$repo\tasks\alpha" ([ordered]@{'catalog.json'=$catalog;'receipts\x.json'='{"r":1}'})
    Write-Item $s "Artifacts\transfers\$op" ([ordered]@{'package.zip'='PK-binary';'operation.lock'=''})
    Write-Item $s "Workflow\$repo\tasks\alpha" ([ordered]@{'state.json'='{"version":1}';'task.lock'=''})
    Write-Item $s "Harness\$repo\tasks\alpha" ([ordered]@{'CURRENT.json'='{"revision":1}'})
    Write-Item $s "Workflow\$repo\tasks\beta" ([ordered]@{'state.json'='{"version":2}'})
    Write-Item $r "Workflow\$repo\tasks\beta" ([ordered]@{'state.json'='{"version":2}'}) -Private
    Write-Item $s "Workflow\$repo\tasks\gamma" ([ordered]@{'state.json'='{"version":3,"events":["codex"]}'})
    Write-Item $r "Workflow\$repo\tasks\gamma" ([ordered]@{'state.json'='{"version":3,"events":["local"]}'}) -Private
    Write-Item $s "Harness\$repo\tasks\gamma" ([ordered]@{'CURRENT.json'='{"revision":4,"by":"codex"}'})
    Write-Item $r "Harness\$repo\tasks\gamma" ([ordered]@{'CURRENT.json'='{"revision":4,"by":"local"}'}) -Private
}
function New-SimpleTask($Stores,[string]$Task='alpha'){
    $s=$Stores.Source;$r=$Stores.Real
    Write-Deployment $s;Write-Deployment $r -Private
    $catalog='{"ownedCopies":["'+([IO.Path]::Combine($r,'Artifacts','transfers',$op,'package.zip')).Replace('\','\\')+'"]}'
    Write-Item $s "Artifacts\transfers\$op" ([ordered]@{'package.zip'=('P'*4096)})
    Write-Item $s "Artifacts\evidence-v2\$dep\$repo\tasks\$Task" ([ordered]@{'catalog.json'=$catalog})
    Write-Item $s "Workflow\$repo\tasks\$Task" ([ordered]@{'state.json'='{"version":1}';'task.lock'=''})
}
# Produces genuine Workflow/Evidence/transfers records with the real modules at
# the real-store positions, then moves them away as a virtualized writer would.
function New-VirtualizedRecords($Stores){
    $r=$Stores.Real
    Import-Module (Join-Path $PSScriptRoot '../EvidenceApplication.psm1');Import-Module (Join-Path $PSScriptRoot '../EvidenceCleanup.psm1');Import-Module (Join-Path $PSScriptRoot '../../Workflow/TaskLifecycle.psm1');Import-Module (Join-Path $PSScriptRoot '../../Workflow/TaskEventStore.psm1');Import-Module (Join-Path $PSScriptRoot '../EvidenceStateStore.psm1');Import-Module (Join-Path $PSScriptRoot '../EvidenceContract.psm1');Import-Module (Join-Path $PSScriptRoot '../EvidencePaths.psm1');Import-Module (Join-Path $PSScriptRoot '../ArtifactApplication.psm1')
    Set-EvidenceTestValue TaskEventStore TestRoot ([IO.Path]::Combine($r,'Workflow'));Set-EvidenceTestValue EvidencePaths TestRoot ([IO.Path]::Combine($r,'Artifacts','evidence-v2'));Set-EvidenceTestValue ArtifactPaths TestRoot ([IO.Path]::Combine($r,'Artifacts','transfers'));Set-EvidenceTestValue EvidenceContract DeploymentRoot ([IO.Path]::Combine($r,'Artifacts','deployments'));Set-EvidenceTestValue CredentialStore TestRoot ([IO.Path]::Combine($Stores.Root,'credentials'))
    foreach($m in @(Get-EvidenceTestModules 'EvidenceApplication')+@(Get-EvidenceTestModules 'EvidenceCleanup')){& $m {function script:Get-CredentialStatus {param($p)[pscustomobject]@{Generation='f'*32}}}}
    $config=[ordered]@{schemaVersion=2;profile='osm';endpoint='https://'+('e'*32)+'.r2.cloudflarestorage.com';bucket='osm-artifacts';repositoryId=$repo;prefix='development/evidence/v2/';policy='task-end-30d-v1';deploymentId=$dep}
    $configPath=[IO.Path]::Combine($r,'Artifacts','deployments',$dep+'.config.json');Write-EvidenceTestJson $configPath $config
    Write-EvidenceTestJson ([IO.Path]::Combine($r,'Artifacts','deployments',$dep+'.json')) ([ordered]@{schemaVersion=2;deploymentId=$dep;repositoryId=$repo;configSha256=(Get-FileHash -LiteralPath $configPath).Hash.ToLowerInvariant();baselineRecordId='fixture';baselineSha256='b'*64;ownerObservationId='fixture';ownerObservationSha256='c'*64})
    $objects=[Collections.Concurrent.ConcurrentDictionary[string,byte[]]]::new()
    $transport={param($gen,$request)
        $status=200;$bytes=$null;$sha=$null;$class='success';$code=$null
        if($request.Operation -ceq 'put'){$objects[$request.Key]=[IO.File]::ReadAllBytes($request.SourcePath)}
        elseif(-not $objects.ContainsKey($request.Key)){$status=404;$class='not-found';$code='NoSuchKey'}
        else{$data=$objects[$request.Key];if($request.DestinationPath){[IO.File]::WriteAllBytes($request.DestinationPath,$data)};$bytes=[long]$data.Length;$sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant()}
        [pscustomobject]@{Operation=$request.Operation;Generation=$gen;HttpStatus=$status;StatusClass=$class;S3Code=$code;Bytes=$bytes;Sha256=$sha;StatusObserved=$true;TimedOut=$false;Redirected=$false}
    }.GetNewClosure()
    $read={param($o,$c,$gen,$key,$bytes,$hash)$data=$objects[$key];[pscustomobject]@{operation='readback';generation=$gen;key=$key;bytes=[long]$data.Length;sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant();childExit=0;childStatus='passed';observedAt=[DateTimeOffset]::UtcNow.ToString('o')}}.GetNewClosure()
    Set-EvidenceTestValue ArtifactApplication TransportHook $transport;Set-EvidenceTestValue ArtifactApplication ReadbackHook $read
    $g=Enter-WorkflowTaskGuard $repo 'fixture';try{$null=Invoke-WorkflowTaskTransition $repo 'fixture' 'started' 0 'fixture-start' '' $g}finally{$g.Dispose()}
    $fixture=New-EvidenceReaderFixture ([IO.Path]::Combine($Stores.Root,'worktree'));$selection=[IO.Path]::Combine($Stores.Root,'selection.json');Write-EvidenceTestJson $selection $fixture.Selection
    $published=Invoke-EvidencePublish $configPath $selection
    Assert ($published.status -ceq 'passed') ('fixture publish '+$published.reasonCode)
    # The virtualized writer's records exist only in its package copy.
    [IO.Directory]::Delete($Stores.Source);[IO.Directory]::Move($r,$Stores.Source);[IO.Directory]::CreateDirectory($r)|Out-Null
    return [pscustomobject]@{ConfigPath=$configPath;Reference=$published.reference}
}
try{
    foreach($case in $selected){
        try{
            Assert ($case -cin $cases) 'unknown case'
            $stores=New-Stores $case
            switch($case){
                'publish-skip-conflict'{
                    New-ClassificationFixture $stores
                    $before=Get-Snapshot $stores.Source
                    $plan=Invoke-Plan $stores
                    Assert ($plan.Summary.publish -eq 1 -and $plan.Summary.skip -eq 2 -and $plan.Summary.hold -eq 1) ('summary '+(ConvertTo-Json $plan.Summary -Compress))
                    $gammaBefore=Get-Snapshot ([IO.Path]::Combine($stores.Real,'Workflow',$repo,'tasks','gamma'))
                    $out=Invoke-StoreConsolidation $plan.Path $plan.Sha $false
                    Assert ($out.status -ceq 'partial') ('status '+$out.status+' '+$out.reasonCode)
                    $a=Get-UnitOf $plan "Workflow\$repo\tasks\alpha";$b=Get-UnitOf $plan "Workflow\$repo\tasks\beta";$c=Get-UnitOf $plan "Workflow\$repo\tasks\gamma"
                    Assert ((Get-Result $out $a.unitId) -ceq 'published' -and (Get-Result $out $b.unitId) -ceq 'complete' -and (Get-Result $out $c.unitId) -ceq 'hold') 'unit results'
                    Assert (@($a.items).Count -eq 4 -and $a.items[0].role -ceq 'transfers') 'alpha unit does not carry its operation'
                    foreach($k in @("Artifacts\transfers\$op","Artifacts\evidence-v2\$dep\$repo\tasks\alpha","Harness\$repo\tasks\alpha","Workflow\$repo\tasks\alpha")){Assert-SameFiles ([IO.Path]::Combine($stores.Source,$k)) ([IO.Path]::Combine($stores.Real,$k))}
                    Assert (Compare-Snapshot $gammaBefore (Get-Snapshot ([IO.Path]::Combine($stores.Real,'Workflow',$repo,'tasks','gamma')))) 'held unit target changed'
                    Assert ((Get-Content -LiteralPath ([IO.Path]::Combine($stores.Real,'Harness',$repo,'tasks','gamma','CURRENT.json')) -Raw).Contains('local')) 'held CURRENT replaced'
                    Assert (Compare-Snapshot $before (Get-Snapshot $stores.Source)) 'source changed'
                    Assert (@(Get-Moves|Where-Object {$_ -like '*gamma*'}).Count -eq 0) 'held unit moved'
                }
                'domain-readers-after-publish'{
                    $real=New-VirtualizedRecords $stores
                    $plan=Invoke-Plan $stores
                    Assert ($plan.Summary.hold -eq 0 -and $plan.Summary.publish -ge 2) ('real records held '+(ConvertTo-Json -Compress -Depth 6 @($plan.Manifest.units|ForEach-Object {$_.reasons})))
                    $out=Invoke-StoreConsolidation $plan.Path $plan.Sha $false
                    Assert ($out.status -ceq 'passed') ('real records '+(ConvertTo-Json -Compress -Depth 6 $out))
                    foreach($u in $plan.Manifest.units){foreach($i in $u.items){
                        $from=if($i.role -ceq 'deployment'){[IO.Path]::Combine($stores.Source,'Artifacts','deployments')}else{[IO.Path]::Combine($stores.Source,$i.key)}
                        $to=if($i.role -ceq 'deployment'){[IO.Path]::Combine($stores.Real,'Artifacts','deployments')}else{[IO.Path]::Combine($stores.Real,$i.key)}
                        foreach($f in $i.files){Assert ((Get-FileHash -LiteralPath ([IO.Path]::Combine($to,$f.path))).Hash.ToLowerInvariant() -ceq $f.sha256 -and (Get-FileHash -LiteralPath ([IO.Path]::Combine($from,$f.path))).Hash.ToLowerInvariant() -ceq $f.sha256) ('hash '+$f.path)}
                    }}
                    # The ordinary readers accept the consolidated copies in place.
                    $config=Read-EvidenceConfig $real.ConfigPath
                    $g=Enter-WorkflowTaskGuard $repo 'fixture'
                    try{
                        $state=Read-WorkflowTaskState $repo 'fixture' $g;Assert ($null -ne $state -and $state.version -eq 1) 'workflow reader'
                        $task=Read-EvidenceTask $config 'fixture' $g;Assert ($null -ne $task -and @($task.artifacts).Count -eq 1 -and $task.artifacts[0].state -ceq 'ready') 'evidence reader'
                    }finally{$g.Dispose()}
                    Assert (@(Get-WorkflowTasks $repo) -ccontains 'fixture') 'task not listed after publish'
                }
                'acl-rule-and-reparse'{
                    New-SimpleTask $stores
                    # Identical bytes, ordinary inherited ACL: not skipped.
                    Write-Item $stores.Source "Workflow\$repo\tasks\beta" ([ordered]@{'state.json'='{}'})
                    Write-Item $stores.Real "Workflow\$repo\tasks\beta" ([ordered]@{'state.json'='{}'})
                    # A reparse point inside a source item, and one in a real-store ancestor.
                    Write-Item $stores.Source "Workflow\$repo\tasks\delta" ([ordered]@{'state.json'='{}'})
                    $outside=[IO.Path]::Combine($stores.Root,'outside');[IO.Directory]::CreateDirectory($outside)|Out-Null
                    New-Item -ItemType Junction -Path ([IO.Path]::Combine($stores.Source,'Workflow',$repo,'tasks','delta','link')) -Target $outside|Out-Null
                    $other='b'*64
                    Write-Item $stores.Source "Workflow\$other\tasks\epsilon" ([ordered]@{'state.json'='{}'})
                    [IO.Directory]::CreateDirectory([IO.Path]::Combine($stores.Real,'Workflow'))|Out-Null
                    New-Item -ItemType Junction -Path ([IO.Path]::Combine($stores.Real,'Workflow',$other)) -Target $outside|Out-Null
                    $plan=Invoke-Plan $stores
                    Assert ((Get-UnitOf $plan "Workflow\$repo\tasks\beta").reasons -ccontains 'acl-mismatch') 'inherited ACL skipped'
                    Assert ((Get-UnitOf $plan "Workflow\$repo\tasks\delta").reasons -ccontains 'reparse') 'source reparse accepted'
                    Assert ((Get-UnitOf $plan "Workflow\$other\tasks\epsilon").reasons -ccontains 'reparse') 'target ancestor reparse accepted'
                    $out=Invoke-StoreConsolidation $plan.Path $plan.Sha $false
                    Assert ($out.status -ceq 'partial') 'held units did not make the run partial'
                    Assert (@([IO.Directory]::EnumerateFileSystemEntries($outside)).Count -eq 0) 'wrote through a junction'
                    foreach($k in @("Artifacts\transfers\$op","Artifacts\evidence-v2\$dep\$repo\tasks\alpha","Workflow\$repo\tasks\alpha")){
                        $p=[IO.Path]::Combine($stores.Real,$k);Assert-TestAcl $p $true
                        foreach($e in [IO.Directory]::EnumerateFileSystemEntries($p,'*',[IO.SearchOption]::AllDirectories)){Assert-TestAcl $e ([IO.Directory]::Exists($e))}
                    }
                    # Directories this run created carry the rule; pre-existing ancestors are left as they were.
                    foreach($p in @('Artifacts\evidence-v2','Artifacts\transfers')){Assert-TestAcl ([IO.Path]::Combine($stores.Real,$p)) $true}
                }
                'excluded-never-opened'{
                    New-SimpleTask $stores
                    $sentinels=@('Artifacts\credentials\osm\record.bin','Artifacts\builds-v1\b\receipt.json','Artifacts\contents-v1\c\x.json','Artifacts\runtime\h\config.json','Artifacts\evidence-ledgers\l\ledger.json','Artifacts\review-evidence\r\x.txt','RevisionLocks\lock.json','H2a Review\notes.md','WorktreeRecords\w.json')
                    foreach($rel in $sentinels){foreach($root in @($stores.Source,$stores.Real)){$p=[IO.Path]::Combine($root,$rel);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($p))|Out-Null;[IO.File]::WriteAllText($p,'sentinel')}}
                    $plan=Invoke-Plan $stores
                    $out=Invoke-StoreConsolidation $plan.Path $plan.Sha $false
                    Assert ($out.status -ceq 'passed') ('status '+$out.status)
                    foreach($rel in $sentinels){
                        $top=if($rel.StartsWith('Artifacts\')){'Artifacts\'+$rel.Split('\')[1]}else{$rel.Split('\')[0]}
                        foreach($root in @($stores.Source,$stores.Real)){
                            $dir=[IO.Path]::Combine($root,$top)
                            Assert (@($trace|Where-Object {$_.Substring($_.IndexOf('|')+1).StartsWith($dir,[StringComparison]::OrdinalIgnoreCase)}).Count -eq 0) ('touched '+$top)
                        }
                    }
                    foreach($name in @('credentials','builds-v1','RevisionLocks','H2a Review')){Assert (@($plan.Manifest.excluded|Where-Object {$_.key -like "*$name"}).Count -eq 1) ('not listed as excluded '+$name)}
                }
                {$_ -cin @('fault-file-write','fault-staged','fault-between-items','fault-before-receipt')}{
                    New-SimpleTask $stores
                    $before=Get-Snapshot $stores.Source
                    $plan=Invoke-Plan $stores
                    $stage=switch($case){'fault-file-write'{'file-write'}'fault-staged'{'staged'}'fault-between-items'{'published'}'fault-before-receipt'{'before-receipt'}}
                    $unit=Get-UnitOf $plan "Workflow\$repo\tasks\alpha"
                    # The receipt stage is reported per unit; the other stages per item.
                    $key=if($stage -ceq 'published'){"Artifacts\transfers\$op"}elseif($stage -ceq 'before-receipt'){$unit.unitId}else{"Workflow\$repo\tasks\alpha"}
                    Set-Consolidation FaultHook {param($s,$k)if($s -ceq $stage -and $k -ceq $key){throw 'injected-crash'}}.GetNewClosure()
                    $first=Invoke-StoreConsolidation $plan.Path $plan.Sha $false
                    Assert ($first.status -ceq 'partial') ('first run '+$first.status)
                    Assert ((Get-Result $first $unit.unitId) -ceq 'publish-failed') 'fault not reported'
                    $workflow=[IO.Path]::Combine($stores.Real,'Workflow',$repo,'tasks','alpha')
                    Assert ([IO.Directory]::Exists($workflow) -eq ($stage -ceq 'before-receipt')) 'Workflow visibility after the fault'
                    $movesBefore=@(Get-Moves)
                    Set-Consolidation FaultHook $null
                    $second=Invoke-StoreConsolidation $plan.Path $plan.Sha $false
                    Assert ($second.status -ceq 'passed') ('rerun '+(ConvertTo-Json -Compress -Depth 5 $second))
                    # Every destination is moved exactly once across both runs: nothing replaced.
                    $moves=@(Get-Moves);Assert (@($moves|Sort-Object -Unique).Count -eq $moves.Count) 'a destination was published twice'
                    if($stage -ceq 'before-receipt'){Assert ($moves.Count -eq $movesBefore.Count) 'complete unit republished'}
                    foreach($k in @("Artifacts\transfers\$op","Artifacts\evidence-v2\$dep\$repo\tasks\alpha","Workflow\$repo\tasks\alpha")){Assert-SameFiles ([IO.Path]::Combine($stores.Source,$k)) ([IO.Path]::Combine($stores.Real,$k))}
                    $run=[IO.Path]::Combine($stores.Real,'Artifacts','consolidation',$plan.Sha.Substring(0,32))
                    Assert ([IO.File]::Exists([IO.Path]::Combine($run,'receipts',$unit.unitId+'.json'))) 'receipt missing'
                    Assert (@([IO.Directory]::EnumerateFiles([IO.Path]::Combine($run,'staging'),'*',[IO.SearchOption]::AllDirectories)).Count -eq 0) 'staging left behind'
                    Assert (@($trace|Where-Object {$_.StartsWith('delete-staging|') -and -not $_.Substring(15).StartsWith($run,[StringComparison]::OrdinalIgnoreCase)}).Count -eq 0) 'deleted outside the run'
                    Assert (Compare-Snapshot $before (Get-Snapshot $stores.Source)) 'source changed'
                }
                'concurrent-writer'{
                    New-SimpleTask $stores
                    $plan=Invoke-Plan $stores
                    $workflow=[IO.Path]::Combine($stores.Real,'Workflow',$repo,'tasks','alpha')
                    # An interactive CLI's guard creates the task directory first.
                    Set-Consolidation FaultHook {param($s,$k)if($s -ceq 'before-publish' -and $k -clike 'Workflow\*'){[IO.Directory]::CreateDirectory($workflow)|Out-Null;[IO.File]::WriteAllText([IO.Path]::Combine($workflow,'task.lock'),'other-writer')}}.GetNewClosure()
                    $out=Invoke-StoreConsolidation $plan.Path $plan.Sha $false
                    Assert ($out.status -ceq 'partial' -and (Get-Result $out (Get-UnitOf $plan "Workflow\$repo\tasks\alpha").unitId) -ceq 'concurrent-writer') ('result '+(ConvertTo-Json -Compress -Depth 5 $out))
                    Assert (@([IO.Directory]::EnumerateFileSystemEntries($workflow)).Count -eq 1 -and [IO.File]::ReadAllText([IO.Path]::Combine($workflow,'task.lock')) -ceq 'other-writer') 'existing writer entry changed'
                }
                'hidden-until-workflow'{
                    New-SimpleTask $stores
                    Import-Module (Join-Path $PSScriptRoot '../../Workflow/TaskEventStore.psm1')
                    Set-EvidenceTestValue TaskEventStore TestRoot ([IO.Path]::Combine($stores.Real,'Workflow'))
                    $plan=Invoke-Plan $stores
                    $listed=[Collections.Generic.List[string]]::new()
                    Set-Consolidation FaultHook {param($s,$k)if($s -ceq 'published' -and $k -cnotlike 'Workflow\*'){$listed.Add((@(Get-WorkflowTasks $repo) -join ','))}}.GetNewClosure()
                    $out=Invoke-StoreConsolidation $plan.Path $plan.Sha $false
                    Assert ($out.status -ceq 'passed' -and $listed.Count -eq 2 -and @($listed|Where-Object {$_ -ne ''}).Count -eq 0) ('listed before Workflow: '+($listed -join ';'))
                    Assert (@(Get-WorkflowTasks $repo) -ccontains 'alpha') 'not listed after Workflow'
                    Set-EvidenceTestValue TaskEventStore TestRoot $null
                }
                'run-guards'{
                    New-SimpleTask $stores
                    $plan=Invoke-Plan $stores
                    $realBefore=Get-Snapshot $stores.Real
                    $dry=Invoke-StoreConsolidation $plan.Path $plan.Sha $true
                    Assert ($dry.status -ceq 'passed' -and $dry.dryRun -and @($dry.units|Where-Object verdict -CEQ 'publish').Count -eq 1 -and @($dry.units|Where-Object verdict -CEQ 'complete').Count -eq 1) 'dry-run verdicts'
                    Assert (Compare-Snapshot $realBefore (Get-Snapshot $stores.Real)) 'dry-run changed the store'
                    Assert ((Invoke-StoreConsolidation $plan.Path ('0'*64) $false).reasonCode -ceq 'manifest-hash-mismatch') 'hash not bound'
                    # A source change after approval stops that unit only.
                    [IO.File]::AppendAllText([IO.Path]::Combine($stores.Source,'Workflow',$repo,'tasks','alpha','state.json'),' ')
                    $changed=Invoke-StoreConsolidation $plan.Path $plan.Sha $true
                    Assert ((Get-Result $changed (Get-UnitOf $plan "Workflow\$repo\tasks\alpha").unitId) -ceq 'source-changed' -and $changed.status -ceq 'partial') 'source change missed'
                    $run=[IO.Path]::Combine($stores.Real,'Artifacts','consolidation',$plan.Sha.Substring(0,32));[IO.Directory]::CreateDirectory($run)|Out-Null
                    $lock=[IO.FileStream]::new([IO.Path]::Combine($run,'run.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
                    try{Assert ((Invoke-StoreConsolidation $plan.Path $plan.Sha $false).reasonCode -ceq 'consolidation-busy') 'second run not excluded'}finally{$lock.Dispose()}
                    Set-EvidenceTestValue WindowsStoreContext ProbeIo @{Open={param($p)[pscustomobject]@{Path=$p}};FinalPath={param($h)'C:\Packages\X\LocalCache\Local\'+[IO.Path]::GetFileName($h.Path)};Close={param($h)}}
                    Assert ((Invoke-StoreConsolidationPlan $stores.Source ([IO.Path]::Combine($stores.Root,'never.json'))).reasonCode -ceq 'virtualized-store') 'plan ran virtualized'
                    Assert (-not [IO.File]::Exists([IO.Path]::Combine($stores.Root,'never.json'))) 'virtualized plan wrote output'
                    Set-EvidenceTestValue WindowsStoreContext ProbeIo $notVirtualized
                    Assert ((Invoke-StoreConsolidationPlan ([IO.Path]::Combine($stores.Real,'Workflow')) ([IO.Path]::Combine($stores.Root,'inside.json'))).reasonCode -ceq 'invalid-source-root') 'source inside the store accepted'
                    Assert ((Invoke-StoreConsolidationPlan $stores.Source ([IO.Path]::Combine($stores.Real,'m.json'))).reasonCode -ceq 'invalid-output') 'output inside the store accepted'
                }
                'cli-exit-codes'{
                    New-ClassificationFixture $stores
                    # Fresh process: the entry reuses the already loaded modules with private roots.
                    $child=[IO.Path]::Combine($stores.Root,'cli-child.ps1')
                    $body="param([string]`$Mode,[string]`$A,[string]`$B)`nSet-StrictMode -Version Latest`n`$ErrorActionPreference='Stop'`n"+
                        "`$c=Import-Module '"+(Join-Path $PSScriptRoot '../../Workflow/WindowsStoreContext.psm1').Replace("'","''")+"' -PassThru;& `$c {`$script:ProbeIo=@{Open={param(`$p)[pscustomobject]@{Path=`$p}};FinalPath={param(`$h)`$h.Path};Close={param(`$h)}}}`n"+
                        "`$m=Import-Module '"+(Join-Path $PSScriptRoot '../StoreConsolidation.psm1').Replace("'","''")+"' -PassThru;& `$m {param(`$r)`$script:TestRoot=`$r} '"+$stores.Real.Replace("'","''")+"'`n"+
                        "`$entry='"+(Join-Path $PSScriptRoot '../../artifacts.ps1').Replace("'","''")+"'`n"+
                        "if(`$Mode -ceq 'plan'){& `$entry store plan --source-root `$A --output `$B}elseif(`$Mode -ceq 'run'){& `$entry store consolidate --manifest `$A --manifest-sha256 `$B}else{& `$entry store plan --source-root `$A}`nexit `$LASTEXITCODE"
                    [IO.File]::WriteAllText($child,$body,[Text.UTF8Encoding]::new($false))
                    function Invoke-Cli([string[]]$Arguments){
                        $info=[Diagnostics.ProcessStartInfo]::new([Diagnostics.Process]::GetCurrentProcess().MainModule.FileName);$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
                        foreach($a in @('-NoProfile','-File',$child)+$Arguments){$info.ArgumentList.Add($a)}
                        $p=[Diagnostics.Process]::Start($info);try{$o=$p.StandardOutput.ReadToEndAsync();$e=$p.StandardError.ReadToEndAsync();Assert ($p.WaitForExit(60000)) 'cli deadline';$null=[Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($o,$e),3000);return [pscustomobject]@{Exit=$p.ExitCode;Json=(@($o.Result -split "`r?`n"|Where-Object {$_.StartsWith('{')})[-1]|ConvertFrom-Json);Text=$o.Result+$e.Result}}finally{$p.Dispose()}
                    }
                    $out=[IO.Path]::Combine($stores.Root,'cli-manifest.json')
                    $planned=Invoke-Cli @('plan',$stores.Source,$out);Assert ($planned.Exit -eq 0 -and $planned.Json.status -ceq 'passed') ('cli plan '+$planned.Text)
                    Assert (-not $planned.Text.Contains($stores.Root)) 'cli plan printed a path'
                    $ran=Invoke-Cli @('run',$out,$planned.Json.manifestSha256);Assert ($ran.Exit -eq 2 -and $ran.Json.status -ceq 'partial') ('cli partial '+$ran.Text)
                    $bad=Invoke-Cli @('bad',$stores.Source,'');Assert ($bad.Exit -eq 1 -and $bad.Json.reasonCode -ceq 'unsupported-command') ('cli grammar '+$bad.Text)
                }
            }
            $executed.Add($case)
        }catch{$failed.Add($case+': '+$_.Exception.Message+' '+$_.ScriptStackTrace.Split("`n")[0])}
        finally{Set-Consolidation FaultHook $null;Set-Consolidation OpenHook $null;Set-Consolidation TestRoot $null;Set-EvidenceTestValue WindowsStoreContext ProbeIo $null}
    }
    if($ResultPath){[IO.File]::WriteAllText($ResultPath,(ConvertTo-Json -Depth 8 -InputObject ([ordered]@{registered=$cases;selected=$selected;executed=@($executed);failed=@($failed)})),[Text.UTF8Encoding]::new($false))}
    foreach($name in $executed){"PASS $name"};foreach($name in $failed){"FAIL $name"}
    "Cases: $($selected.Count)/$($cases.Count); passed: $($executed.Count); failed: $($failed.Count)"
    if($failed.Count -or $executed.Count -ne $selected.Count){exit 1}
}finally{
    # Junctions are removed as links first so the recursive delete never follows them.
    if([IO.Directory]::Exists($base)){
        foreach($e in @([IO.Directory]::EnumerateDirectories($base,'*',[IO.SearchOption]::AllDirectories)|Where-Object {[IO.Directory]::Exists($_) -and ([IO.File]::GetAttributes($_) -band [IO.FileAttributes]::ReparsePoint)})){[IO.Directory]::Delete($e)}
        [IO.Directory]::Delete($base,$true)
    }
}
