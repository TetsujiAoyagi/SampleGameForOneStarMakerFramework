param([string] $ResultPath = '',[string] $Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Sidecarは実際にこのprocessでロードしたassemblyだけを記録する。build出力の推測を代用しない。
function Get-LoadedArtifactBinaries {
    foreach($name in @('ArtifactPackaging','R2ArtifactTransport')) {
        $assembly=[AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name -ceq $name}|Select-Object -First 1
        if(-not $assembly){continue}
        $dependencies=@($assembly.GetReferencedAssemblies()|ForEach-Object {
            $reference=$_;$loaded=[AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name -ceq $reference.Name}|Select-Object -First 1
            if(-not $loaded){[pscustomobject]@{name=$reference.Name;status='not-loaded';path=$null;sha256=$null}}
            elseif(-not $loaded.Location){[pscustomobject]@{name=$reference.Name;status='in-memory';path=$null;sha256=$null}}
            else{[pscustomobject]@{name=$reference.Name;status='loaded';path=$loaded.Location;sha256=(Get-FileHash -LiteralPath $loaded.Location -Algorithm SHA256).Hash.ToLowerInvariant()}}
        })
        [pscustomobject]@{path=$assembly.Location;sha256=(Get-FileHash -LiteralPath $assembly.Location -Algorithm SHA256).Hash.ToLowerInvariant();moduleVersionId=$assembly.ManifestModule.ModuleVersionId.ToString();dependencies=$dependencies}
    }
}
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
. (Join-Path $PSScriptRoot 'EvidenceReaderFixture.ps1')
$cases=@('transport-config','credential-callback-dto','publish-fetch-roundtrip','wrong-outer-hash','network-write-refusal','readback-closed-mismatch','readback-child-protocol','observation-persistence-fail-closed','readback-stop-budget','strict-config-and-reference','source-traversal-network-zero','legacy-config-reference-commit-network-zero','download-bytes-mismatch','readback-prelaunch-deadline','readback-start-false','readback-start-throw','readback-started-before-pid','readback-late-output-owned-cleanup')
$selected=@(if($Case -ceq '*'){$cases}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new();$script:transportIdentity=$null
function Assert([bool]$Condition,[string]$Message){Assert-EvidenceTest $Condition $Message}
function New-Observation([string]$Operation,[string]$Generation,[int]$Status,[string]$Class,[string]$Code,[long]$Bytes=0,[string]$Hash=''){
    [pscustomobject]@{Operation=$Operation;Generation=$Generation;HttpStatus=$Status;StatusClass=$Class;S3Code=if($Code){$Code}else{$null};Bytes=if($Operation -eq 'get'){$Bytes}else{$null};Sha256=if($Operation -eq 'get'){$Hash}else{$null};StatusObserved=$true;TimedOut=$false;Redirected=$false}
}
function Assert-FailedPublish($Value){Assert ($Value.status -ceq 'failed' -and $null -eq $Value.reference -and $null -eq $Value.receipt -and $null -eq $Value.packageSha256) 'failed publish returned a ready identity'}
function Get-NetworkCount($Environment){[int](@($Environment.Counts.Values)|Measure-Object -Sum).Sum}
foreach($case in $selected){
    $env=$null
    try{
        Assert ($case -cin $cases) 'unknown case'
        $env=New-EvidenceTestEnvironment
        $reader=New-EvidenceReaderFixture $env.Root
        $input=[IO.Path]::Combine($env.Root,'selection.json');Write-EvidenceTestJson $input $reader.Selection
        $fixture=@{Work=$env.Root;Config=$env.ConfigPath;Input=$input}
        $app=Get-EvidenceTestModules 'ArtifactApplication'|Select-Object -First 1
        $store=Get-EvidenceTestModules 'CredentialStore'|Select-Object -First 1
        switch($case){
                'transport-config' {
                    $dll=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Transport/artifacts/transport/R2ArtifactTransport.dll'))
                    Assert ([IO.File]::Exists($dll)) 'dedicated R2 transport DLL missing'
                    $assembly=[Reflection.Assembly]::LoadFrom($dll)
                    Assert ($assembly.Location.Equals($dll,[StringComparison]::OrdinalIgnoreCase)) 'wrong transport DLL loaded'
                    $script:transportIdentity=[ordered]@{loadedPath=$assembly.Location
                        loadedHashBefore=(Get-FileHash -Algorithm SHA256 -LiteralPath $dll).Hash.ToLowerInvariant()
                        moduleVersionId=$assembly.ManifestModule.ModuleVersionId.ToString()}
                    $type=$assembly.GetType('OneStarMaker.Artifacts.Transport.R2ArtifactTransport',$true)
                    $factory=$type.GetMethod('CreateClient',[Reflection.BindingFlags]'NonPublic,Static')
                    $client=$factory.Invoke($null,[object[]]@('DUMMY_ID','DUMMY_SECRET','https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'))
                    try {
                        Assert (-not $client.Config.AllowAutoRedirect -and $client.Config.MaxErrorRetry -eq 0) 'transport redirect/retry config'
                        Assert ($client.Config.HttpClientFactory.GetType().Name -ceq 'RejectRedirectFactory') 'redirect guard factory absent'
                    } finally { $client.Dispose() }
                    $invalid=$type.GetMethod('Execute').Invoke($null,[object[]]@('DUMMY_ID','DUMMY_SECRET','https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com','list','probe/locked/invalid',$null,$null,1000))
                    Assert ($invalid.StatusClass -ceq 'invalid-input') 'generic operation unexpectedly enabled'
                }
                'credential-callback-dto' {
                    $dto=New-Observation 'get' ('a'*32) 200 'success' '' 3 ('b'*64)
                    $accepted=& $store {
                        param($value)
                        $record=[pscustomobject]@{AccessKeyId='DUMMY_CALLBACK_ID';SecretAccessKey='DUMMY_CALLBACK_SECRET';Generation=('a'*32)}
                        $callback={param($id,$secret,$generation,$request) $value}.GetNewClosure()
                        Invoke-RestrictedTransport $record $callback @{Operation='get'}
                    } $dto
                    Assert ($accepted.StatusObserved -eq $true -and $accepted.Sha256 -ceq ('b'*64)) 'closed production DTO refused'
                    $unsafe=[pscustomobject]@{Operation='get';StatusObserved=$true;SdkResponse='not-safe'}
                    $rejected=$false
                    try { & $store {param($value) $record=[pscustomobject]@{AccessKeyId='DUMMY_ID';SecretAccessKey='DUMMY_SECRET';Generation=('a'*32)}; $callback={param($id,$secret,$generation,$request) $value}.GetNewClosure(); Invoke-RestrictedTransport $record $callback @{Operation='get'}} $unsafe | Out-Null }
                    catch { $rejected=$true }
                    Assert $rejected 'SDK response field accepted'
                }
            'publish-fetch-roundtrip' {
                $p=Invoke-EvidencePublish $fixture.Config $fixture.Input
                Assert ($p.status -ceq 'passed' -and $p.reference -and $p.receipt) ('v2 publish failed: '+(ConvertTo-Json $p -Compress -Depth 8))
                $receipt=Get-Content -LiteralPath $p.receipt.path -Raw|ConvertFrom-Json
                Assert ($receipt.schemaVersion -eq 2 -and $receipt.taskId -ceq 'fixture' -and $receipt.packageSha256 -ceq $p.packageSha256 -and $receipt.readback.childStatus -ceq 'passed') 'v2 receipt identity/readback missing'
                Assert ($receipt.entries.Count -eq 13 -and $env.Objects.Count -eq 1 -and (Get-NetworkCount $env) -eq 1) 'publish did not use one immutable PUT'
                $f=Invoke-EvidenceFetch $fixture.Config $p.reference $p.packageSha256
                Assert ($f.status -ceq 'passed') 'v2 fetch failed'
                foreach($name in $reader.Files){Assert ((Get-FileHash -LiteralPath ([IO.Path]::Combine($reader.Source,$name))).Hash -ceq (Get-FileHash -LiteralPath ([IO.Path]::Combine($f.outputPath,$name))).Hash) ('fetched file mismatch: '+$name)}
            }
            'wrong-outer-hash' {
                $p=Invoke-EvidencePublish $fixture.Config $fixture.Input;Assert ($p.status -ceq 'passed') 'publish fixture failed';$before=Get-NetworkCount $env
                $f=Invoke-EvidenceFetch $fixture.Config $p.reference ('0'*64)
                Assert ($f.status -ceq 'failed' -and $null -eq $f.outputPath -and (Get-NetworkCount $env) -eq $before) 'wrong caller hash reached network or ready'
            }
            'network-write-refusal' {
                Set-EvidenceTestValue 'ArtifactApplication' 'TransportHook' {param($g,$r) New-Observation 'put' $g 403 'forbidden' 'ObjectLockedByBucketPolicy'}
                $p=Invoke-EvidencePublish $fixture.Config $fixture.Input;Assert-FailedPublish $p
                Assert ($p.residue.remote -ceq 'may-have-been-sent') 'failed PUT lost its uncertainty'
                $inspection=Invoke-EvidenceInspect $fixture.Config 'fixture';Assert (@($inspection.artifacts|Where-Object state -CEQ 'ready').Count -eq 0) 'refused PUT was catalog ready'
            }
            'readback-closed-mismatch' {
                Set-EvidenceTestValue 'ArtifactApplication' 'ReadbackHook' {param($op,$c,$g,$k,$b,$h)[pscustomobject]@{operation='readback';generation=$g;key=$k;bytes=$b;sha256='0'*64;childExit=1;childStatus='failed';observedAt=[DateTimeOffset]::UtcNow.ToString('o')}}
                $p=Invoke-EvidencePublish $fixture.Config $fixture.Input;Assert-FailedPublish $p
                $inspection=Invoke-EvidenceInspect $fixture.Config 'fixture';Assert (@($inspection.artifacts|Where-Object state -CEQ 'ready').Count -eq 0) 'mismatched child readback made ready'
            }
            'observation-persistence-fail-closed' {
                $script:receiptRefused=$false
                Set-EvidenceTestValue 'EvidencePaths' 'WriteHook' {param($stage,$path)if($stage -ceq 'before-rename' -and $path -match '[\\/]receipts[\\/]'){$script:receiptRefused=$true;throw 'receipt persistence fixture refusal'}}
                $p=Invoke-EvidencePublish $fixture.Config $fixture.Input;Assert-FailedPublish $p
                Assert ($script:receiptRefused -and $env.Objects.Count -eq 1) 'receipt persistence fault was not reached after PUT/readback'
                $inspection=Invoke-EvidenceInspect $fixture.Config 'fixture';Assert (@($inspection.artifacts|Where-Object state -CEQ 'ready').Count -eq 0) 'unpersisted receipt made ready'
            }
                'readback-stop-budget' {
                    $script:clockValues=[Collections.Generic.Queue[long]]::new()
                    $script:clockValues.Enqueue(0L); $script:clockValues.Enqueue(0L)
                    $script:clockValues.Enqueue([long]([Diagnostics.Stopwatch]::Frequency*14))
                    $script:exitBudget=0; $script:pipeBudget=0; $script:stopped=$false
                    $stop={ $script:stopped=$true }
                    $exit={param($ms) $script:exitBudget=$ms; $true}
                    $pipes={param($ms) $script:pipeBudget=$ms; $true}
                    $clock={ $script:clockValues.Dequeue() }
                    $remaining={ 20000 }
                    $expired=$false
                    try { & $app {param($s,$e,$p,$c,$r) Invoke-ReadbackStop $s $e $p $c $r} $stop $exit $pipes $clock $remaining } catch { $expired=$true }
                    Assert ($expired -and $script:stopped -and $script:exitBudget -eq 15000 -and
                        $script:pipeBudget -le 1000 -and $script:pipeBudget -gt 0) 'stop and pipe waits used separate budgets'
                    $script:stopped=$false; $script:exitBudget=0; $script:pipeBudget=0
                    $script:clockValues=[Collections.Generic.Queue[long]]::new(); $script:clockValues.Enqueue(0L)
                    $zero={ 0 }
                    $expired=$false
                    try { & $app {param($s,$e,$p,$c,$r) Invoke-ReadbackStop $s $e $p $c $r} $stop $exit $pipes $clock $zero } catch { $expired=$true }
                    Assert ($expired -and $script:stopped -and $script:exitBudget -eq 0 -and $script:pipeBudget -eq 0) 'zero budget skipped child stop or extended wait'
                    $script:stopped=$false; $script:exitBudget=0; $script:pipeBudget=0
                    $script:clockValues=[Collections.Generic.Queue[long]]::new()
                    $script:clockValues.Enqueue(0L); $script:clockValues.Enqueue(0L); $script:clockValues.Enqueue(0L)
                    $script:remainingValues=[Collections.Generic.Queue[int]]::new()
                    $script:remainingValues.Enqueue(500); $script:remainingValues.Enqueue(500); $script:remainingValues.Enqueue(200)
                    $limited={ $script:remainingValues.Dequeue() }
                    $expired=$false
                    try { & $app {param($s,$e,$p,$c,$r) Invoke-ReadbackStop $s $e $p $c $r} $stop $exit $pipes $clock $limited } catch { $expired=$true }
                    Assert ($expired -and $script:stopped -and $script:exitBudget -eq 500 -and $script:pipeBudget -eq 200) 'operation budget was not shared with stop and pipes'
                }
                {$_ -cin @('readback-prelaunch-deadline','readback-start-false','readback-start-throw','readback-started-before-pid')} {
                    $state=@{remaining=20000L;hard=21000L;pid=0;launches=0};$left={$state.remaining}.GetNewClosure();$hard={$state.hard}.GetNewClosure()
                    $budget=[pscustomobject]@{Remaining=$left;HardRemaining=$hard}
                    $mode=$case;$launch={param($point,$process)
                        if($point -ceq 'before-start' -and $mode -ceq 'readback-prelaunch-deadline'){$state.remaining=0;$state.hard=1000;return}
                        if($point -ceq 'start'){$state.launches++;if($mode -ceq 'readback-start-false'){return $false};if($mode -ceq 'readback-start-throw'){throw 'launch-unknown'};return $process.Start()}
                        if($point -ceq 'after-start'){$state.pid=$process.Id;throw 'pid-journal-unconfirmed'}
                    }.GetNewClosure()
                    $child=[IO.Path]::Combine($fixture.Work,'marker-child.ps1');[IO.File]::WriteAllText($child,'exit 0',[Text.UTF8Encoding]::new($false))
                    $defaultChild=& $app {$script:ReadbackChildPath};$operationRoot=& $app {New-ArtifactOperation};$marker=[IO.Path]::Combine($operationRoot,'readback-process.json')
                    try{
                        & $app {param($b,$h,$p)$script:Budget=$b;$script:ReadbackHook=$null;$script:ReadbackLaunchHook=$h;$script:ReadbackChildPath=$p} $budget $launch $child
                        $rejected=$false;try{Invoke-ArtifactReadback $operationRoot $env.Config ('e'*32) (Get-EvidenceKey $env.Config fixture ('d'*32)) 12 ('a'*64) ([Diagnostics.Stopwatch]::StartNew())|Out-Null}catch{$rejected=$true}
                        Assert $rejected 'failed launch returned readback success'
                        $terminal=Get-Content -LiteralPath $marker -Raw|ConvertFrom-Json
                        $known=$mode -cin @('readback-prelaunch-deadline','readback-start-false')
                        Assert ($terminal.process.pid -eq 0 -and $terminal.exited -eq $known -and $terminal.pipesClosed -eq $known) 'non-launch certainty or unknown launch protection lost'
                        if($known){Assert-ArtifactAcl $marker $false}
                        Assert (($mode -cne 'readback-prelaunch-deadline' -or $state.launches -eq 0) -and ($mode -cne 'readback-started-before-pid' -or $state.pid -gt 0)) 'boundary launch was not exercised'
                        $cleanup=(Get-Command Assert-EvidenceTransitionSafe).Module
                        $operation=[pscustomobject]@{process=[pscustomobject]@{pid=2147483647;startedAt='2000-01-01T00:00:00.0000000Z'};childMarker=$marker}
                        $protected=$false;try{& $cleanup {param($o)Assert-EvidenceOperationStopped $o} $operation}catch{$protected=$true}
                        Assert ($protected -ne $known) 'cleanup cannot distinguish confirmed non-launch and unknown child'
                        Assert ((Get-NetworkCount $env) -eq 0) 'marker repair performed network work'
                        Assert (@([IO.Directory]::EnumerateFiles($operationRoot,'readback-process.json.*.pending')).Count -eq 0) 'marker repair did not commit atomically'
                    }finally{& $app {param($h,$p)$script:Budget=$null;$script:ReadbackLaunchHook=$null;$script:ReadbackHook=$h;$script:ReadbackChildPath=$p} $env.Readback $defaultChild}
                }
                'readback-late-output-owned-cleanup' {
                    $child=[IO.Path]::Combine($fixture.Work,'late-file-child.ps1')
                    $source=@'
param([string] $RequestPath)
$data=Get-Content -Raw -LiteralPath $RequestPath|ConvertFrom-Json
[IO.File]::Copy((Join-Path $PSScriptRoot 'offline-child-remote.zip'),$data.destination,$false)
$acl=[IO.FileSystemAclExtensions]::GetAccessControl([IO.FileInfo]::new($data.destination))
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'late-file-acl-observation.json'),(ConvertTo-Json @{protected=$acl.AreAccessRulesProtected;path=$data.destination} -Compress))
$bytes=[IO.FileInfo]::new($data.destination).Length;$hash=(Get-FileHash -LiteralPath $data.destination).Hash.ToLowerInvariant()
$value=[ordered]@{schemaVersion=1;operation='get';generation=$data.generation;key=$data.key;bytes=$bytes;sha256=$hash;httpStatus=200;statusClass='success';s3Code=$null;statusObserved=$true;timedOut=$false;redirected=$false;status='passed'}
[Console]::WriteLine((ConvertTo-Json -InputObject $value -Compress));exit 0
'@
                    [IO.File]::WriteAllText($child,$source,[Text.UTF8Encoding]::new($false))
                    $inner=$env.Transport;$remote=[IO.Path]::Combine($fixture.Work,'offline-child-remote.zip')
                    $transport={param($gen,$req)$r=& $inner $gen $req;if($req.Operation -ceq 'put'){[IO.File]::Copy($req.SourcePath,$remote,$false)};$r}.GetNewClosure()
                    $defaultChild=& $app {$script:ReadbackChildPath}
                    try{
                        Set-EvidenceTestValue ArtifactApplication TransportHook $transport
                        Set-EvidenceTestValue ArtifactApplication ReadbackHook $null;Set-EvidenceTestValue ArtifactApplication ReadbackChildPath $child
                        $failures=[Collections.Generic.List[string]]::new();Set-EvidenceTestValue EvidenceApplication FailureHook {param($e)$failures.Add($e.Exception.Message+' @ '+$e.ScriptStackTrace)}.GetNewClosure()
                        $p=Invoke-EvidencePublish $fixture.Config $fixture.Input;Assert ($p.status -ceq 'passed') ('late-file child publish failed: '+($failures -join ', '))
                        $observation=Get-Content -LiteralPath ([IO.Path]::Combine($fixture.Work,'late-file-acl-observation.json')) -Raw|ConvertFrom-Json
                        Assert (-not $observation.protected -and [IO.File]::Exists($observation.path)) 'child did not exercise inherited late output ACL'
                        Assert-ArtifactAcl $observation.path $false
                        $endAt=[DateTimeOffset]::Parse('2026-01-01T00:00:00.0000000+00:00');Set-EvidenceTestValue TaskLifecycle Clock {$endAt}.GetNewClosure()
                        $g=Enter-WorkflowTaskGuard $env.Config.Data.repositoryId fixture
                        try{$t=Read-EvidenceTask $env.Config fixture $g;Assert (@($t.artifacts[0].ownedCopies|Where-Object path -CEQ $observation.path).Count -eq 1) 'late output was not storage-owned';$null=Invoke-WorkflowTaskTransition $env.Config.Data.repositoryId fixture ended 1 late-file-end completed $g;$null=Sync-EvidenceTaskGuarded $env.Config fixture $g}finally{$g.Dispose()}
                        $now=$endAt.AddDays(30);Set-EvidenceTestValue EvidenceCleanup Clock {$now}.GetNewClosure()
                        $clean=Invoke-EvidenceCleanup $fixture.Config
                        Assert ($clean.status -ceq 'passed' -and $clean.deleted -eq 1 -and $clean.blocked -eq 0 -and -not [IO.File]::Exists($observation.path) -and $env.Objects.Count -eq 0) 'confirmed remote deletion left ACL-rejected child copy'
                        $g=Enter-WorkflowTaskGuard $env.Config.Data.repositoryId fixture;try{$t=Read-EvidenceTask $env.Config fixture $g;Assert ($t.artifacts[0].ownedCopies.Count -eq 0 -and $t.artifacts[0].state -ceq 'deleted') 'owned cleanup retained late output'}finally{$g.Dispose()}
                        Assert ([IO.File]::Exists($remote) -and [IO.File]::Exists([IO.Path]::Combine($reader.Selection.root,$reader.Selection.files[0]))) 'cleanup changed source or unrelated fixture file'
                    }finally{Set-EvidenceTestValue ArtifactApplication ReadbackHook $env.Readback;Set-EvidenceTestValue ArtifactApplication ReadbackChildPath $defaultChild;Set-EvidenceTestValue EvidenceApplication FailureHook $null;Set-EvidenceTestValue TaskLifecycle Clock {[DateTimeOffset]::UtcNow};Set-EvidenceTestValue EvidenceCleanup Clock {[DateTimeOffset]::UtcNow}}
                }
                'readback-child-protocol' {
                    $child=[IO.Path]::Combine($fixture.Work,'synthetic-child.ps1')
                    $source=@'
param([string] $RequestPath)
$data=Get-Content -Raw -LiteralPath $RequestPath | ConvertFrom-Json
$value=[ordered]@{schemaVersion=1;operation='get';generation=$data.generation;key=$data.key
    bytes=[long]12;sha256=('0'*64);httpStatus=403;statusClass='forbidden';s3Code='AccessDenied'
    statusObserved=$true;timedOut=$false;redirected=$false;status='failed'}
[Console]::WriteLine((ConvertTo-Json -InputObject $value -Compress))
exit 1
'@
                    [IO.File]::WriteAllText($child,$source,[Text.UTF8Encoding]::new($false))
                    $defaultChild=& $app { $script:ReadbackChildPath }
                    $operationRoot=& $app { New-ArtifactOperation }
                    try {
                        & $app {param($path) $script:ReadbackHook=$null; $script:ReadbackChildPath=$path} $child
                        $config=$env.Config
                        $timer=[Diagnostics.Stopwatch]::StartNew()
                        $key=Get-EvidenceKey $env.Config 'fixture' ('d'*32)
                        $actual=& $app {param($root,$cfg,$gen,$name,$watch) Invoke-ArtifactReadback $root $cfg $gen $name 12 ('a'*64) $watch} $operationRoot $config ('e'*32) $key $timer
                        Assert ($actual.childExit -eq 1 -and $actual.childStatus -ceq 'failed' -and
                            $actual.httpStatus -eq 403 -and $actual.s3Code -ceq 'AccessDenied' -and
                            $actual.bytes -eq 12 -and $actual.sha256 -ceq ('0'*64)) 'closed child status tuple lost'
                        $terminal=Get-Content -LiteralPath ([IO.Path]::Combine($operationRoot,'readback-process.json')) -Raw|ConvertFrom-Json
                        Assert ($terminal.exited -and $terminal.pipesClosed) 'child process/pipes terminal confirmation missing'
                    } finally { & $app {param($hook,$path) $script:ReadbackHook=$hook; $script:ReadbackChildPath=$path} $env.Readback $defaultChild }
                }
            'strict-config-and-reference' {
                $raw=[IO.File]::ReadAllText($fixture.Config)
                [IO.File]::WriteAllText($fixture.Config,$raw.Replace('"schemaVersion":2','"schemaVersion":2,"schemaVersion":2'))
                $p=Invoke-EvidencePublish $fixture.Config $fixture.Input;Assert-FailedPublish $p;Assert ((Get-NetworkCount $env) -eq 0) 'duplicate config reached network'
                [IO.File]::WriteAllText($fixture.Config,$raw,[Text.UTF8Encoding]::new($false))
                $p=Invoke-EvidencePublish $fixture.Config $fixture.Input;Assert ($p.status -ceq 'passed') 'publish fixture failed'
                $ref=Read-EvidenceReference $p.reference $env.Config;$ref.head='9'*40
                $text='osm-evidence-v2:'+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json $ref -Compress))).TrimEnd('=').Replace('+','-').Replace('/','_')
                $before=Get-NetworkCount $env;$f=Invoke-EvidenceFetch $fixture.Config $text $p.packageSha256
                Assert ($f.status -ceq 'failed' -and $f.reasonCode -ceq 'identity-mismatch' -and (Get-NetworkCount $env) -eq $before) 'altered reference reached network'
            }
            'source-traversal-network-zero' {
                $reader.Selection.files=@('../outside.txt');Write-EvidenceTestJson $fixture.Input $reader.Selection
                $p=Invoke-EvidencePublish $fixture.Config $fixture.Input;Assert-FailedPublish $p
                Assert ((Get-NetworkCount $env) -eq 0) 'source traversal reached network'
            }
            'legacy-config-reference-commit-network-zero' {
                $raw=[IO.File]::ReadAllText($fixture.Config)
                [IO.File]::WriteAllText($fixture.Config,$raw.Replace('"schemaVersion":2','"schemaVersion":1'),[Text.UTF8Encoding]::new($false))
                $p=Invoke-EvidencePublish $fixture.Config $fixture.Input;Assert-FailedPublish $p
                Assert ($p.reasonCode -ceq 'unsupported-schema' -and (Get-NetworkCount $env) -eq 0) 'legacy config reached network'
                [IO.File]::WriteAllText($fixture.Config,$raw,[Text.UTF8Encoding]::new($false))
                $f=Invoke-EvidenceFetch $fixture.Config 'osm-artifact-v1:e30' ('0'*64)
                Assert ($f.status -ceq 'failed' -and $f.reasonCode -ceq 'unsupported-schema' -and (Get-NetworkCount $env) -eq 0) 'legacy reference reached network'
                # The public parser rejects commit before importing an application or opening credentials/network.
                $proc=[Diagnostics.Process]::new();$info=$proc.StartInfo;$info.FileName=(Get-Process -Id $PID).Path;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
                foreach($arg in @('-NoProfile','-File',[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts.ps1')),'evidence','commit','--profile','osm','--config',$fixture.Config)){$info.ArgumentList.Add($arg)}
                try{
                    $null=$proc.Start();$stdout=$proc.StandardOutput.ReadToEndAsync();$stderr=$proc.StandardError.ReadToEndAsync()
                    if(-not $proc.WaitForExit(15000)){$proc.Kill($true);$null=$proc.WaitForExit(5000);throw 'legacy CLI child timeout'}
                    Assert ($stdout.Wait(3000) -and $stderr.Wait(3000)) 'legacy CLI pipes timeout'
                    Assert ($proc.ExitCode -eq 1 -and ($stdout.Result+$stderr.Result).Contains('unsupported-command') -and (Get-NetworkCount $env) -eq 0) 'legacy commit was not rejected before network'
                }finally{$proc.Dispose()}
            }
            'download-bytes-mismatch' {
                $p=Invoke-EvidencePublish $fixture.Config $fixture.Input;Assert ($p.status -ceq 'passed') 'publish fixture failed'
                $inner=$env.Transport
                $corrupt={param($gen,$request)$dto=& $inner $gen $request;if($request.Operation -ceq 'get' -and $request.DestinationPath){$bytes=[IO.File]::ReadAllBytes($request.DestinationPath);$bytes[$bytes.Length-1]=$bytes[$bytes.Length-1] -bxor 1;[IO.File]::WriteAllBytes($request.DestinationPath,$bytes)};$dto}.GetNewClosure()
                Set-EvidenceTestValue 'ArtifactApplication' 'TransportHook' $corrupt
                $f=Invoke-EvidenceFetch $fixture.Config $p.reference $p.packageSha256
                Assert ($f.status -ceq 'failed' -and $null -eq $f.outputPath) 'lying download DTO bypassed actual byte hash'
            }
        }
    }catch{$failed.Add($case+': '+$_.Exception.GetType().Name+': '+$_.Exception.Message)}
    finally{
        $executed.Add($case)
        Set-EvidenceTestValue 'ArtifactApplication' 'TransportHook' $null;Set-EvidenceTestValue 'ArtifactApplication' 'ReadbackHook' $null;Set-EvidenceTestValue 'EvidencePaths' 'WriteHook' $null
        if($env){$target=[IO.Path]::GetFullPath($env.Root);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath());if(-not $target.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($target) -cnotmatch '\Aosm-evidence-v2-[0-9a-f]{32}\z'){throw 'unsafe fixture cleanup path'};if([IO.Directory]::Exists($target)){[IO.Directory]::Delete($target,$true)}}
    }
}
$after=if($script:transportIdentity){(Get-FileHash -LiteralPath $script:transportIdentity.loadedPath).Hash.ToLowerInvariant()}else{$null}
if($script:transportIdentity -and $script:transportIdentity.loadedHashBefore -cne $after){$failed.Add('Transport DLL changed during tests.')}
if($ResultPath){Write-EvidenceTestJson $ResultPath ([ordered]@{registered=$cases;selected=$selected;executed=@($executed);failed=@($failed);transport=$script:transportIdentity;loadedHashAfter=$after;binaries=@(Get-LoadedArtifactBinaries)})}
foreach($case in $executed){if(-not @($failed|Where-Object{$_.StartsWith($case+':',[StringComparison]::Ordinal)}).Count){"PASS $case"}};foreach($item in $failed){"FAIL $item"}
"Cases: $($cases.Count); executed: $($executed.Count); failed: $($failed.Count)"
if($failed.Count -or $executed.Count -ne $selected.Count -or -not $selected.Count){exit 1}
