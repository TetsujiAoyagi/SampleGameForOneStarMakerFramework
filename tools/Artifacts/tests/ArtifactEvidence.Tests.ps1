param([string]$ResultPath='',[string]$Case='*',[switch]$MissingE1SourceProbe)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
. (Join-Path $PSScriptRoot 'EvidenceReaderFixture.ps1')
$registered=@('v2-roundtrip','reader-hash','handoff-identity-substitution','unsupported-schema','selection-path','one-put-lost-response','readback-failure','receipt-atomic-failure','secret-exception-output','fresh-child-fetch','legacy-cli-network-zero','put-reconcile-no-reput','put-recovery-child-protected','config-invalid-path','config-invalid-path-other-commands','deployment-receipt-missing','selection-invalid-path','invalid-path-no-leak','cli-relative-selection')
# Path-diagnostic cases: a rejected input must stop before the Workflow guard (which
# creates the task directory), the Storage catalog and the transport.
function Assert-EvidenceNotStarted($Env,[string]$Label){
    Assert-EvidenceTest ($Env.Counts.Count -eq 0) ($Label+' network')
    Assert-EvidenceTest (-not [IO.Directory]::Exists([IO.Path]::Combine($Env.Root,'catalog'))) ($Label+' catalog')
    Assert-EvidenceTest (-not [IO.Directory]::Exists([IO.Path]::Combine($Env.Root,'workflow',('a'*64),'tasks','unguarded'))) ($Label+' guard')
}
function New-EvidenceBadInputs($Env,$Fixture){
    $bad=[IO.Path]::Combine($Env.Root,'bad-input-marker');[IO.Directory]::CreateDirectory($bad)|Out-Null
    $broken=[IO.Path]::Combine($bad,'broken.json');[IO.File]::WriteAllText($broken,'{"DUMMY_SECRET":',[Text.UTF8Encoding]::new($false))
    $config=Get-Content -LiteralPath $Env.ConfigPath -Raw|ConvertFrom-Json -AsHashtable
    $short=[ordered]@{};foreach($k in $config.Keys){if($k -cne 'policy'){$short[$k]=$config[$k]}};$shortPath=[IO.Path]::Combine($bad,'short.json');Write-EvidenceTestJson $shortPath $short
    $hex=[ordered]@{}+$config;$hex.deploymentId='zz';$hexPath=[IO.Path]::Combine($bad,'hex.json');Write-EvidenceTestJson $hexPath $hex
    $value=[ordered]@{}+$config;$value.profile='other';$valuePath=[IO.Path]::Combine($bad,'value.json');Write-EvidenceTestJson $valuePath $value
    $sel=[ordered]@{}+$Fixture.Selection;$sel.taskId='unguarded';$selPath=[IO.Path]::Combine($bad,'selection.json');Write-EvidenceTestJson $selPath $sel
    $selShort=[ordered]@{}+$sel;$selShort.Remove('base');$selShortPath=[IO.Path]::Combine($bad,'selection-short.json');Write-EvidenceTestJson $selShortPath $selShort
    return [pscustomobject]@{Relative='bad-input-marker\relative.json';Missing=[IO.Path]::Combine($bad,'missing.json');Broken=$broken;Short=$shortPath;Hex=$hexPath;Value=$valuePath;Selection=$selPath;SelectionShort=$selShortPath}
}
$registered+=@('stage-local-fetch-child','stage-local-fetch-reject','stage-unresolved-refused','stage-source-publish','stage-changes-patch','stage-catalog-read','remote-fetch-codes-unchanged')
function ConvertTo-EvidenceTestReference($Ref){return 'osm-evidence-v2:'+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json $Ref -Compress))).TrimEnd('=').Replace('+','-').Replace('/','_')}
function Invoke-EvidenceTestStage($Env,[string]$SelectionPath){$s=Invoke-EvidenceStage $Env.ConfigPath $SelectionPath;Assert-EvidenceTest ($s.status -ceq 'passed' -and $s.locality -ceq 'local' -and $s.reference.StartsWith('osm-evidence-v2:')) ('stage failed '+$s.reasonCode);return $s}
function Invoke-EvidenceTestGit([string]$Repo,[string[]]$GitArgs){$out=& git -C $Repo @GitArgs;if($LASTEXITCODE -ne 0){throw ('git failed: '+($GitArgs -join ' '))};return $out}
# Relative path -> SHA-256 of every file under a checkout, excluding git metadata.
function Get-EvidenceTestTreeHashes([string]$Dir){$h=@{};foreach($f in Get-ChildItem -LiteralPath $Dir -Recurse -File -Force){$rel=[IO.Path]::GetRelativePath($Dir,$f.FullName).Replace('\','/');if($rel -cmatch '\A\.git(/|\z)'){continue};$h[$rel]=(Get-FileHash -LiteralPath $f.FullName).Hash};return $h}
# Compares base, base+changes.patch and the recorded file set. Each file must be
# either recorded with the patched content, deleted by the patch, or unchanged.
function Get-EvidenceTestPatchProblems($Base,$Applied,$Recorded,[string[]]$Deleted){
    $problems=[Collections.Generic.List[string]]::new()
    foreach($k in $Recorded.Keys){if($Applied[$k] -cne $Recorded[$k]){$problems.Add('recorded-differs:'+$k)}}
    foreach($k in $Applied.Keys){if(-not $Recorded.ContainsKey($k) -and $Base[$k] -cne $Applied[$k]){$problems.Add('unrecorded-change:'+$k)}}
    foreach($k in $Deleted){if($Applied.ContainsKey($k) -or $Recorded.ContainsKey($k) -or -not $Base.ContainsKey($k)){$problems.Add('deletion:'+$k)}}
    foreach($k in $Base.Keys){if(-not $Applied.ContainsKey($k) -and $k -cnotin $Deleted){$problems.Add('unrecorded-deletion:'+$k)}}
    return ,$problems.ToArray()
}
$registered+=@('promote-roundtrip','promote-refusals','promote-already-promoted','promote-lock','promote-lost-response','promote-intent-reuse','promote-stop-endpoints','promote-fetch-pending','promote-protection-expiry','promote-persist-order','promote-atomic-failure','promote-cli')
# Promotion cases change module hooks and clocks; each one starts and ends from defaults.
function Reset-EvidencePromoteHooks {
    $now={[DateTimeOffset]::UtcNow}
    Set-EvidenceTestValue EvidenceApplication FaultHook $null;Set-EvidenceTestValue EvidenceApplication Clock $now;Set-EvidenceTestValue EvidenceCleanup ProcessHook $null;Set-EvidenceTestValue EvidenceCleanup Clock $now;Set-EvidenceTestValue TaskLifecycle Clock $now;Set-EvidenceTestValue EvidencePaths WriteHook $null
}
function New-EvidencePromoteFixture {$e=New-EvidenceTestEnvironment;$f=New-EvidenceReaderFixture $e.Root;$sp=[IO.Path]::Combine($e.Root,'selection.json');Write-EvidenceTestJson $sp $f.Selection;$s=Invoke-EvidenceTestStage $e $sp;return [pscustomobject]@{Env=$e;Fixture=$f;Selection=$sp;Stage=$s}}
function Get-EvidencePutCount($Env){return [int](@($Env.Counts.GetEnumerator()|Where-Object {$_.Key.StartsWith('put ',[StringComparison]::Ordinal)}|ForEach-Object Value)|Measure-Object -Sum).Sum}
function Get-EvidenceTestSha([byte[]]$Bytes){return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()}
# Reads the catalog as it is on disk while a promote in this process holds the guard.
function Read-EvidenceTestDisk($Env){return Read-EvidenceTask $Env.Config fixture ([pscustomobject]@{RepositoryId='a'*64;TaskId='fixture'})}
function Get-EvidenceTestArtifact($Task,$Env,[string]$Reference){$id=(Read-EvidenceReference $Reference $Env.Config).artifactId;return @($Task.artifacts|Where-Object artifactId -CEQ $id)[0]}
# The I3 fields that promotion and its recovery must leave untouched.
function Get-EvidencePromoteIdentity($A){return ConvertTo-Json -Compress -Depth 8 -InputObject @($A.artifactId,$A.key,$A.packageSha256,$A.manifestSha256,$A.base,$A.head,$A.packageBytes,$A.entries,$A.taskEndedAt,$A.deleteEligibleAt,$A.protections)}
function Set-EvidenceTestLostPut($Env,[bool]$Stored){$inner=$Env.Transport;$counts=$Env.Counts;Set-EvidenceTestValue ArtifactApplication TransportHook ({param($g,$req)if($req.Operation -ceq 'put'){if($Stored){$null=& $inner $g $req}else{$null=$counts.AddOrUpdate('put '+$req.Key,1,{param($k,$v)$v+1})};throw 'response-lost'};& $inner $g $req}.GetNewClosure())}
function Edit-EvidenceTestByte([string]$Path){$f=[IO.FileStream]::new($Path,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);try{$f.Position=[long]($f.Length/2);$b=$f.ReadByte();$f.Position=[long]($f.Length/2);$f.WriteByte([byte](($b+1)%256))}finally{$f.Dispose()}}
function Complete-EvidenceTestTask($Env,[DateTimeOffset]$At){Set-EvidenceTestValue TaskLifecycle Clock {$At}.GetNewClosure();$g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$null=Invoke-WorkflowTaskTransition ('a'*64) fixture ended 1 promote-end completed $g}finally{$g.Dispose()}}
function Set-EvidenceTestNow([DateTimeOffset]$At){Set-EvidenceTestValue EvidenceCleanup Clock {$At}.GetNewClosure();Set-EvidenceTestValue EvidenceApplication Clock {$At}.GetNewClosure()}
# Simulates the promote process dying at a fault point: the catalog is put back to
# its bytes at that moment, undoing what the in-process failure handling wrote.
# The promote's own process then counts as stopped for the next preflight.
function Invoke-EvidenceTestPromoteStop($Env,$Stage,[string]$Point){
    $catalog=[IO.Path]::Combine((Get-EvidenceTaskDirectory $Env.Config fixture),'catalog.json');$saved=@{bytes=$null}
    Set-EvidenceTestValue EvidenceApplication FaultHook ({param($p,$a)if($p -ceq $Point){$saved.bytes=[IO.File]::ReadAllBytes($catalog);throw 'simulated-stop'}}.GetNewClosure())
    try{$r=Invoke-EvidencePromote $Env.ConfigPath $Stage.reference $Stage.packageSha256}finally{Set-EvidenceTestValue EvidenceApplication FaultHook $null}
    Assert-EvidenceTest ($null -ne $saved.bytes -and $r.status -ceq 'failed') ('stop '+$Point+' '+$r.reasonCode)
    [IO.File]::WriteAllBytes($catalog,$saved.bytes);Set-EvidenceTestValue EvidenceCleanup ProcessHook {param($identity)$true}
}
$registered+=@('promote-recovery-resend','promote-resend-same-call','promote-failed-recovery','promote-recovery-protection','promote-fetch-source','promote-resend-refusals','promote-resend-persist-order')
# Rebuilds the failed shape that the previous runtime wrote when a recovery GET of a
# promotion stopped after write A returned 404: a recovery staging whose operation
# was cleared, and the artifact failed with its putIntent and no operation. The
# stopped promote's readback staging (i) keeps its operation, as that runtime left
# it, (ii) is gone with the recovery staging, as after the seven-day rule, or (iii)
# has its operation cleared.
function New-EvidenceRecoverableFailed([string]$Variant){
    $x=New-EvidencePromoteFixture;$e=$x.Env;Invoke-EvidenceTestPromoteStop $e $x.Stage 'promote-after-put-pending'
    $readback=(Read-EvidenceTestDisk $e).artifacts[0].operation.path;$recovery=& @(Get-EvidenceTestModules EvidenceCleanup)[0] {New-ArtifactOperation}
    Edit-EvidenceTestTask $e {param($t)$a=$t.artifacts[0]
        $t.staging+=[pscustomobject]@{path=$recovery;createdAt=[DateTimeOffset]::UtcNow.ToString('o');origin='storage-copy';artifactId=$a.artifactId;adopted=$false;adoptionPending=$false;operation=$null;deleteIntent=$null;protections=@()}
        $a.state='failed';$a.operation=$null
        if($Variant -ceq 'ii'){$t.staging=@($t.staging|Where-Object {$_.path -cne $readback -and $_.path -cne $recovery})}elseif($Variant -ceq 'iii'){foreach($s in $t.staging){$s.operation=$null}}
    }
    if($Variant -ceq 'ii'){foreach($dir in @($readback,$recovery)){[IO.Directory]::Delete($dir,$true)}}
    $d=(Read-EvidenceTestDisk $e).artifacts[0];Assert-EvidenceTest ($d.state -ceq 'failed' -and $null -ne $d.putIntent -and $null -eq $d.receipt) ('failed fixture '+$Variant)
    return [pscustomobject]@{Env=$e;Stage=$x.Stage;Selection=$x.Selection;Readback=$readback;Key=$d.key;Zip=[IO.File]::ReadAllBytes((Get-EvidenceStagedZip $d).path)}
}
# A staged artifact holding a putIntent: a promote stopped after write A whose next fetch saw 404.
function New-EvidenceResendFixture {
    $x=New-EvidencePromoteFixture;$e=$x.Env;Invoke-EvidenceTestPromoteStop $e $x.Stage 'promote-after-put-pending'
    $f=Invoke-EvidenceFetch $e.ConfigPath $x.Stage.reference $x.Stage.packageSha256;$d=(Read-EvidenceTestDisk $e).artifacts[0]
    Assert-EvidenceTest ($f.status -ceq 'passed' -and $d.state -ceq 'staged' -and $null -ne $d.putIntent) ('resend fixture '+$f.reasonCode)
    return [pscustomobject]@{Env=$e;Stage=$x.Stage;Selection=$x.Selection;IntentPath=(Resolve-EvidenceStoredPath $d.putIntent.path)}
}
function Get-EvidenceTestKeyCalls($Env,[string]$Key){return [int](@($Env.Counts.GetEnumerator()|Where-Object {$_.Key.EndsWith(' '+$Key,[StringComparison]::Ordinal)}|ForEach-Object Value)|Measure-Object -Sum).Sum}
function Assert-EvidencePromoteRefused($Env,[string]$Reference,[string]$Hash,[string]$Code,[string]$Label){
    $before=Get-EvidencePutCount $Env;$r=Invoke-EvidencePromote $Env.ConfigPath $Reference $Hash
    Assert-EvidenceTest ($r.status -ceq 'failed' -and $r.reasonCode -ceq $Code -and $null -eq $r.reference -and (Get-EvidencePutCount $Env) -eq $before) ($Label+': '+$r.reasonCode)
}
function Invoke-EvidenceTestChildProcess($Env,[string[]]$ChildArgs,[string]$Script=''){
    $info=[Diagnostics.ProcessStartInfo]::new('pwsh');$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    $prefix=if($Script){@('-NoProfile','-File',$Script)}else{@('-NoProfile','-File',(Join-Path $PSScriptRoot 'EvidenceTestChild.ps1'),'-Root',$Env.Root,'-ConfigPath',$Env.ConfigPath)}
    foreach($arg in $prefix+$ChildArgs){$info.ArgumentList.Add($arg)}
    $proc=[Diagnostics.Process]::new();$proc.StartInfo=$info
    try{$null=$proc.Start();$out=$proc.StandardOutput.ReadToEndAsync();$err=$proc.StandardError.ReadToEndAsync();Assert-EvidenceTest ($proc.WaitForExit(30000)) 'child bounded';Assert-EvidenceTest ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),3000)) 'child pipes'
        $lines=@($out.Result -split "`r?`n"|Where-Object {$_.StartsWith('{')});return [pscustomobject]@{Exit=$proc.ExitCode;Json=if($lines.Count){$lines[-1]|ConvertFrom-Json}else{$null};Out=$out.Result;Err=$err.Result}}finally{$proc.Dispose()}
}
function Invoke-EvidencePromoteCase([string]$Name){
    Reset-EvidencePromoteHooks
    try{
        switch($Name){
            'promote-roundtrip'{
                # S1/AC1: same reference and hash, one PUT of the staged bytes, append-only copies, one adopted staging, remote fetch in another process.
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$t0=Read-EvidenceTestTask $e;$a0=$t0.artifacts[0];$identity=Get-EvidencePromoteIdentity $a0;$copies=@($a0.ownedCopies|ForEach-Object path);$adopted=@($t0.staging|Where-Object adopted|ForEach-Object path)
                Assert-EvidenceTest ((Invoke-EvidenceInspect $e.ConfigPath fixture).artifacts[0].locality -ceq 'local') 'locality before'
                $p=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256
                Assert-EvidenceTest ($p.status -ceq 'passed' -and $p.reasonCode -ceq 'complete' -and $p.reference -ceq $s.reference -and $p.packageSha256 -ceq $s.packageSha256 -and $p.locality -ceq 'promoted' -and $p.residue.remote -ceq 'ready' -and $p.receipt.sha256 -cmatch '\A[0-9a-f]{64}\z') ('promote '+$p.reasonCode)
                Assert-EvidenceTest ((Get-EvidencePutCount $e) -eq 1 -and (Get-EvidenceTestSha $e.Objects[$a0.key]) -ceq $s.packageSha256) 'put bytes'
                Assert-EvidenceTest (-not (ConvertTo-Json $p -Depth 10).Contains($e.Root)) 'path in output'
                $t1=Read-EvidenceTestTask $e;$a1=$t1.artifacts[0]
                Assert-EvidenceTest ($a1.state -ceq 'ready' -and $a1.origin -ceq 'stage' -and (Get-EvidencePromoteIdentity $a1) -ceq $identity -and (New-EvidenceReference $e.Config $a1) -ceq $s.reference) 'identity changed'
                $after=@($a1.ownedCopies|ForEach-Object path);Assert-EvidenceTest (@($copies|Where-Object {$_ -cnotin $after}).Count -eq 0) 'owned copy dropped'
                $newly=@($t1.staging|Where-Object {$_.adopted -and $_.path -cnotin $adopted});Assert-EvidenceTest ($newly.Count -eq 1 -and $newly[0].artifactId -ceq $a1.artifactId -and $null -eq $newly[0].operation -and @($t0.staging|Where-Object path -CEQ $newly[0].path).Count -eq 0) 'adopted staging'
                Assert-EvidenceTest ((Invoke-EvidenceInspect $e.ConfigPath fixture).artifacts[0].locality -ceq 'promoted') 'locality after'
                [IO.File]::WriteAllBytes([IO.Path]::Combine($e.Root,'remote.zip'),$e.Objects[$a0.key])
                $c=Invoke-EvidenceTestChildProcess $e @('-Reference',$s.reference,'-Sha256',$s.packageSha256);Assert-EvidenceTest ($c.Exit -eq 0 -and $c.Json.status -ceq 'passed') ('child fetch '+$c.Out+$c.Err)
                foreach($n in $x.Fixture.Files){Assert-EvidenceTest ((Get-FileHash -LiteralPath ([IO.Path]::Combine($c.Json.outputPath,$n))).Hash -ceq (Get-FileHash -LiteralPath ([IO.Path]::Combine($x.Fixture.Source,$n))).Hash) ('entry differs '+$n)}
            }
            'promote-refusals'{
                # AC2: every refusal from the CLI entry with preflight intact, and no PUT.
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage
                foreach($f in @('base','head','manifestSha256','packageBytes')){$r=Read-EvidenceReference $s.reference $e.Config;$r.$f=switch($f){'base'{'1'*40}'head'{'2'*40}'manifestSha256'{'3'*64}'packageBytes'{[long]($r.packageBytes+1)}};Assert-EvidencePromoteRefused $e (ConvertTo-EvidenceTestReference $r) $s.packageSha256 'identity-mismatch' ('(h) '+$f)}
                Assert-EvidencePromoteRefused $e $s.reference ('0'*64) 'identity-mismatch' '(h) hash'
                $r=Read-EvidenceReference $s.reference $e.Config;$r.key=$r.key.Replace('bundle.zip','other.zip');Assert-EvidencePromoteRefused $e (ConvertTo-EvidenceTestReference $r) $s.packageSha256 'invalid-reference' '(i) key'
                $r=Read-EvidenceReference $s.reference $e.Config;$r.artifactId='e'*32;$r.key=Get-EvidenceKey $e.Config fixture $r.artifactId;Assert-EvidencePromoteRefused $e (ConvertTo-EvidenceTestReference $r) $s.packageSha256 'catalog-missing' '(g)'
                Edit-EvidenceTestTask $e {param($t)$t.artifacts[0].state='deleted'};Assert-EvidencePromoteRefused $e $s.reference $s.packageSha256 'expired' '(f)'
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$a=(Read-EvidenceTestTask $e).artifacts[0]
                $intentPath=[IO.Path]::Combine((Get-EvidenceTaskDirectory $e.Config fixture),'intents',$a.artifactId+'.json');$sha=Write-EvidenceAtomic $intentPath ([pscustomobject]@{schemaVersion=2;artifactId=$a.artifactId;key=$a.key;packageBytes=$a.packageBytes;packageSha256=$a.packageSha256;createdAt=$a.createdAt;process=Get-EvidenceProcessIdentity}) -Immutable
                Edit-EvidenceTestTask $e {param($t)$t.artifacts[0].putIntent=$null;$t.artifacts[0].receipt=[pscustomobject]@{path=$intentPath;sha256=$sha}};Assert-EvidencePromoteRefused $e $s.reference $s.packageSha256 'invalid-source' '(d) receipt'
                # (d) a staged artifact whose recorded intent matches its canonical file is sent once with that intent (resend after a 404).
                $y=New-EvidencePromoteFixture;$ya=(Read-EvidenceTestTask $y.Env).artifacts[0];$yPath=[IO.Path]::Combine((Get-EvidenceTaskDirectory $y.Env.Config fixture),'intents',$ya.artifactId+'.json');$ySha=Write-EvidenceAtomic $yPath ([pscustomobject]@{schemaVersion=2;artifactId=$ya.artifactId;key=$ya.key;packageBytes=$ya.packageBytes;packageSha256=$ya.packageSha256;createdAt=$ya.createdAt;process=Get-EvidenceProcessIdentity}) -Immutable
                Edit-EvidenceTestTask $y.Env {param($t)$t.artifacts[0].putIntent=[pscustomobject]@{path=$yPath;sha256=$ySha}};$q=Invoke-EvidencePromote $y.Env.ConfigPath $y.Stage.reference $y.Stage.packageSha256;$yd=(Read-EvidenceTestTask $y.Env).artifacts[0]
                Assert-EvidenceTest ($q.status -ceq 'passed' -and $q.reasonCode -ceq 'complete' -and (Get-EvidencePutCount $y.Env) -eq 1 -and (Get-EvidenceTestSha ([IO.File]::ReadAllBytes($yPath))) -ceq $ySha -and $yd.putIntent.sha256 -ceq $ySha -and $yd.state -ceq 'ready') ('(d) putIntent '+$q.reasonCode)
                # (c) a lost response with no object: the next promote's own preflight sees 404, returns it to staged and sends once more in that call.
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;Set-EvidenceTestLostPut $e $false;$null=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;Set-EvidenceTestValue ArtifactApplication TransportHook $e.Transport
                $q=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($q.status -ceq 'passed' -and $q.reasonCode -ceq 'complete' -and (Get-EvidencePutCount $e) -eq 2) ('(c) '+$q.reasonCode);Assert-EvidenceTest ((Read-EvidenceTestTask $e).artifacts[0].state -ceq 'ready') '(c) state'
                # (a)(b)(e) publish-origin artifacts, each resolved by the ordinary network preflight first.
                foreach($mode in @('ready','lost-missing','lost-stored','lost-other','delete-missing','delete-other')){
                    $e=New-EvidenceTestEnvironment;$fx=New-EvidenceReaderFixture $e.Root;$sp=[IO.Path]::Combine($e.Root,'selection.json');Write-EvidenceTestJson $sp $fx.Selection
                    if($mode -like 'lost-*'){Set-EvidenceTestLostPut $e ($mode -cne 'lost-missing')}
                    $null=Invoke-EvidencePublish $e.ConfigPath $sp;Set-EvidenceTestValue ArtifactApplication TransportHook $e.Transport;$a=(Read-EvidenceTestTask $e).artifacts[0];$ref=New-EvidenceReference $e.Config $a
                    if($mode -ceq 'lost-other' -or $mode -ceq 'delete-other'){$e.Objects[$a.key]=[byte[]](1..64)}
                    if($mode -like 'delete-*'){
                        Edit-EvidenceTestTask $e {param($t)$t.artifacts[0].state='delete-pending';$t.artifacts[0].deleteIntent=[pscustomobject]@{key=$t.artifacts[0].key;generation=$t.generation;packageSha256=$t.artifacts[0].packageSha256;process=[pscustomobject]@{pid=2147483647;startedAt='2000-01-01T00:00:00.0000000Z'};createdAt=[DateTimeOffset]::UtcNow.ToString('o')}}
                        if($mode -ceq 'delete-missing'){$unused=$null;$null=$e.Objects.TryRemove($a.key,[ref]$unused)}
                    }
                    $code=switch($mode){'lost-other'{'unresolved-put'}'delete-missing'{'expired'}'delete-other'{'unresolved-delete'}default{'invalid-source'}}
                    Assert-EvidencePromoteRefused $e $ref $a.packageSha256 $code ('publish '+$mode)
                    $state=(Read-EvidenceTestTask $e).artifacts[0].state;$expected=switch($mode){'lost-missing'{'failed'}'lost-other'{'put-pending'}'delete-missing'{'deleted'}'delete-other'{'delete-pending'}default{'ready'}}
                    Assert-EvidenceTest ($state -ceq $expected) ('publish '+$mode+' state '+$state)
                }
            }
            'promote-already-promoted'{
                # S3/AC3: a promoted artifact answers already-promoted with the first output, active or ended.
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$s2=Invoke-EvidenceTestStage $e $x.Selection
                $first=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($first.status -ceq 'passed') ('first '+$first.reasonCode)
                $shape={param($o)ConvertTo-Json -Compress -Depth 6 -InputObject @($o.status,$o.reference,$o.packageSha256,$o.locality,$o.receipt,$o.residue.remote)}
                $again=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256
                Assert-EvidenceTest ($again.reasonCode -ceq 'already-promoted' -and (& $shape $again) -ceq (& $shape $first) -and (Get-EvidencePutCount $e) -eq 1) ('active again '+$again.reasonCode)
                Assert-EvidencePromoteRefused $e $s.reference ('0'*64) 'identity-mismatch' 'promoted hash'
                Complete-EvidenceTestTask $e ([DateTimeOffset]::UtcNow)
                $ended=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256
                Assert-EvidenceTest ($ended.reasonCode -ceq 'already-promoted' -and (& $shape $ended) -ceq (& $shape $first) -and (Get-EvidencePutCount $e) -eq 1) ('ended again '+$ended.reasonCode)
                Assert-EvidencePromoteRefused $e $s2.reference $s2.packageSha256 'resume-required' 'ended staged'
            }
            'promote-lock'{
                # I2/AC4: changed bytes are refused before or under the lock, the lock holds through PUT, nothing is repackaged.
                $flip=${function:Edit-EvidenceTestByte}
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;& $flip (Get-EvidenceStagedZip (Read-EvidenceTestTask $e).artifacts[0]).path
                Assert-EvidencePromoteRefused $e $s.reference $s.packageSha256 'changed-copy' 'changed before'
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$zip=(Get-EvidenceStagedZip (Read-EvidenceTestTask $e).artifacts[0]).path
                Set-EvidenceTestValue EvidenceApplication FaultHook ({param($p,$a)if($p -ceq 'promote-before-lock'){& $flip $zip}}.GetNewClosure())
                Assert-EvidencePromoteRefused $e $s.reference $s.packageSha256 'identity-mismatch' 'changed before lock';Set-EvidenceTestValue EvidenceApplication FaultHook $null
                $a=(Read-EvidenceTestDisk $e).artifacts[0];Assert-EvidenceTest ($a.state -ceq 'staged' -and $null -eq $a.putIntent -and -not [IO.File]::Exists([IO.Path]::Combine((Get-EvidenceTaskDirectory $e.Config fixture),'intents',$a.artifactId+'.json'))) 'lock refusal left state'
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$zip=(Get-EvidenceStagedZip (Read-EvidenceTestTask $e).artifacts[0]).path;$probe=@{denied=$false};$inner=$e.Transport
                Set-EvidenceTestValue ArtifactApplication TransportHook ({param($g,$req)if($req.Operation -ceq 'put'){try{$w=[IO.FileStream]::new($zip,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::ReadWrite);$w.Dispose()}catch [IO.IOException]{$probe.denied=$true}};& $inner $g $req}.GetNewClosure())
                $calls=@{n=0};$bps=@(Set-PSBreakpoint -Command Initialize-EvidencePackage,Copy-EvidenceSnapshot -Action ({$calls.n++}.GetNewClosure()))
                try{$null=Invoke-EvidenceTestStage $e $x.Selection;$control=$calls.n;$calls.n=0;$p=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256}finally{$bps|Remove-PSBreakpoint}
                Assert-EvidenceTest ($p.status -ceq 'passed' -and $probe.denied) ('lock not held '+$p.reasonCode)
                Assert-EvidenceTest ($control -gt 0 -and $calls.n -eq 0) ('repackaged '+$control+'/'+$calls.n)
                $m=@(Get-EvidenceTestModules EvidenceApplication)[0];$source=& $m {${function:Invoke-EvidencePromote}.ToString()+${function:Send-EvidenceStagedPackage}.ToString()+${function:Complete-EvidencePromoteFailure}.ToString()}
                foreach($n in @('CreateVerified','Initialize-EvidencePackage','Copy-EvidenceSnapshot')){Assert-EvidenceTest (-not $source.Contains($n)) ('promote calls '+$n)}
            }
            'promote-lost-response'{
                # S2/AC5: a lost PUT response is never resent in the same call; recovery reaches ready, or a 404 returns it to staged and the next promote sends it again.
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$before=@((Read-EvidenceTestTask $e).artifacts[0].ownedCopies|ForEach-Object path)
                Set-EvidenceTestLostPut $e $true;$p=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;Set-EvidenceTestValue ArtifactApplication TransportHook $e.Transport
                $a=(Read-EvidenceTestTask $e).artifacts[0]
                Assert-EvidenceTest ($p.status -ceq 'failed' -and $p.reasonCode -ceq 'unconfirmed' -and $p.residue.remote -ceq 'may-have-been-sent' -and $a.state -ceq 'put-pending' -and $null -ne $a.putIntent -and (Get-EvidencePutCount $e) -eq 1) ('lost '+$p.reasonCode)
                $f=Invoke-EvidenceFetch $e.ConfigPath $s.reference $s.packageSha256;$a=(Read-EvidenceTestTask $e).artifacts[0];$after=@($a.ownedCopies|ForEach-Object path)
                Assert-EvidenceTest ($f.status -ceq 'passed' -and $a.state -ceq 'ready' -and $null -ne $a.receipt -and @($before|Where-Object {$_ -cnotin $after}).Count -eq 0) ('recovered '+$f.reasonCode)
                $q=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($q.reasonCode -ceq 'already-promoted' -and (Get-EvidencePutCount $e) -eq 1) ('after recovery '+$q.reasonCode)
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$u=Invoke-EvidenceUse $e.ConfigPath $s.reference 'reviewer' ([DateTimeOffset]::UtcNow.AddHours(1).ToString('o')) 'review';Assert-EvidenceTest ($u.status -ceq 'passed') 'use'
                $a0=(Read-EvidenceTestTask $e).artifacts[0];$before=@($a0.ownedCopies|ForEach-Object path);$zip=(Get-EvidenceStagedZip $a0).path
                Set-EvidenceTestLostPut $e $false;$null=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;Set-EvidenceTestValue ArtifactApplication TransportHook $e.Transport
                $q=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($q.status -ceq 'passed') ('failed promote '+$q.reasonCode);$a=(Read-EvidenceTestTask $e).artifacts[0];$after=@($a.ownedCopies|ForEach-Object path)
                Assert-EvidenceTest ($a.state -ceq 'ready' -and $null -ne $a.putIntent -and [IO.File]::Exists($zip) -and @($before|Where-Object {$_ -cnotin $after}).Count -eq 0 -and @($a.protections).Count -eq 1 -and (Get-EvidencePutCount $e) -eq 2) 'failed kept'
                $end=[DateTimeOffset]::UtcNow;Complete-EvidenceTestTask $e $end;Set-EvidenceTestNow $end.AddDays(31)
                Assert-EvidenceTest ((Invoke-EvidenceInspect $e.ConfigPath fixture).artifacts[0].policy.reasonCode -ceq 'expired') 'failed policy'
                $c=Invoke-EvidenceCleanup $e.ConfigPath;$item=@($c.items|Where-Object {$_.artifactId -ceq $a.artifactId})
                # The artifact's item comes first; the unadopted stagings of the two promotes also meet the seven-day rule.
                Assert-EvidenceTest ($item[0].reasonCode -ceq 'deleted' -and $e.Counts['delete '+$a.key] -eq 1 -and -not $e.Objects.ContainsKey($a.key) -and (Read-EvidenceTestTask $e).artifacts[0].state -ceq 'deleted') 'failed cleaned'
            }
            'promote-intent-reuse'{
                # S4/AC6: an intent written before write A is reused exactly; any mismatch refuses; a receipt alone is not ready.
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$a=(Read-EvidenceTestTask $e).artifacts[0];$intentPath=[IO.Path]::Combine((Get-EvidenceTaskDirectory $e.Config fixture),'intents',$a.artifactId+'.json')
                Invoke-EvidenceTestPromoteStop $e $s 'promote-after-intent-file';$d=(Read-EvidenceTestDisk $e).artifacts[0]
                Assert-EvidenceTest ($d.state -ceq 'staged' -and $null -eq $d.putIntent -and [IO.File]::Exists($intentPath) -and (Get-EvidencePutCount $e) -eq 0) 'stopped after intent'
                $f=Invoke-EvidenceFetch $e.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($f.status -ceq 'passed' -and (Get-EvidencePutCount $e) -eq 0) ('local fetch '+$f.reasonCode)
                $h=Get-EvidenceTestSha ([IO.File]::ReadAllBytes($intentPath));$p=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;$d=(Read-EvidenceTestDisk $e).artifacts[0]
                Assert-EvidenceTest ($p.status -ceq 'passed' -and (Get-EvidencePutCount $e) -eq 1 -and (Get-EvidenceTestSha ([IO.File]::ReadAllBytes($intentPath))) -ceq $h -and $d.putIntent.sha256 -ceq $h -and $d.state -ceq 'ready') ('reuse '+$p.reasonCode)
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$a=(Read-EvidenceTestTask $e).artifacts[0];$intentPath=[IO.Path]::Combine((Get-EvidenceTaskDirectory $e.Config fixture),'intents',$a.artifactId+'.json')
                Invoke-EvidenceTestPromoteStop $e $s 'promote-after-intent-file';$original=[IO.File]::ReadAllText($intentPath)
                foreach($variant in @('schemaVersion','artifactId','key','packageBytes','packageSha256','missing','unreadable')){
                    $v=$original|ConvertFrom-Json -AsHashtable
                    switch($variant){'schemaVersion'{$v.schemaVersion=3}'artifactId'{$v.artifactId='e'*32}'key'{$v.key=$v.key+'x'}'packageBytes'{$v.packageBytes=$v.packageBytes+1}'packageSha256'{$v.packageSha256='0'*64}'missing'{$v.Remove('createdAt')}}
                    $text=if($variant -ceq 'unreadable'){'{"schemaVersion":'}else{ConvertTo-Json $v -Compress -Depth 8};[IO.File]::WriteAllText($intentPath,$text,[Text.UTF8Encoding]::new($false))
                    Assert-EvidencePromoteRefused $e $s.reference $s.packageSha256 'unresolved-put' ('intent '+$variant);Assert-EvidenceTest ((Read-EvidenceTestDisk $e).artifacts[0].state -ceq 'staged') ('intent state '+$variant)
                }
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$a=(Read-EvidenceTestTask $e).artifacts[0];$receiptPath=[IO.Path]::Combine((Get-EvidenceTaskDirectory $e.Config fixture),'receipts',$a.artifactId+'.json')
                Invoke-EvidenceTestPromoteStop $e $s 'promote-after-receipt-file';$d=(Read-EvidenceTestDisk $e).artifacts[0]
                Assert-EvidenceTest ($d.state -ceq 'put-pending' -and $null -eq $d.receipt -and [IO.File]::Exists($receiptPath) -and (Get-EvidencePutCount $e) -eq 1) 'receipt file alone'
                $f=Invoke-EvidenceFetch $e.ConfigPath $s.reference $s.packageSha256;$d=(Read-EvidenceTestTask $e).artifacts[0]
                Assert-EvidenceTest ($f.status -ceq 'passed' -and $d.state -ceq 'ready' -and $d.receipt.sha256 -ceq (Get-EvidenceTestSha ([IO.File]::ReadAllBytes($receiptPath)))) ('receipt reuse '+$f.reasonCode)
                $q=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($q.reasonCode -ceq 'already-promoted' -and (Get-EvidencePutCount $e) -eq 1) ('after receipt '+$q.reasonCode)
            }
            'promote-stop-endpoints'{
                # AC6/R2-01: a stop after write A, then each recovery outcome, the same task's other commands, and expiry cleanup.
                foreach($mode in @('match','missing','other')){
                    $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$a0=(Read-EvidenceTestTask $e).artifacts[0];$zip=(Get-EvidenceStagedZip $a0).path;$zipBytes=[IO.File]::ReadAllBytes($zip);$before=@($a0.ownedCopies|ForEach-Object path)
                    Invoke-EvidenceTestPromoteStop $e $s 'promote-after-put-pending';$d=Read-EvidenceTestDisk $e;$op=$d.artifacts[0].operation
                    Assert-EvidenceTest ($d.artifacts[0].state -ceq 'put-pending' -and $null -ne $op -and @($d.staging|Where-Object {$_.path -ceq $op.path -and $null -ne $_.operation -and -not $_.adopted}).Count -eq 1 -and (Get-EvidencePutCount $e) -eq 0) ($mode+' stopped')
                    if($mode -ceq 'match'){$e.Objects[$a0.key]=$zipBytes}elseif($mode -ceq 'other'){$e.Objects[$a0.key]=[byte[]](1..64)}
                    $adoptedBefore=@($d.staging|Where-Object adopted|ForEach-Object path)
                    $f=Invoke-EvidenceFetch $e.ConfigPath $s.reference $s.packageSha256;$t=Read-EvidenceTestTask $e;$a=$t.artifacts[0];$stage=@($t.staging|Where-Object path -CEQ $op.path)[0];$after=@($a.ownedCopies|ForEach-Object path)
                    $newlyAdopted=@($t.staging|Where-Object {$_.adopted -and $_.path -cnotin $adoptedBefore})
                    Assert-EvidenceTest (@($before|Where-Object {$_ -cnotin $after}).Count -eq 0 -and (Get-EvidencePromoteIdentity $a) -ceq (Get-EvidencePromoteIdentity $a0)) ($mode+' copies')
                    switch($mode){
                        # Only this recovery's staging and the fetch's own download staging become adopted; the stopped promote's readback staging does not.
                        'match'{Assert-EvidenceTest ($f.status -ceq 'passed' -and $a.state -ceq 'ready' -and $null -eq $a.operation -and -not $stage.adopted -and $null -eq $stage.operation -and $newlyAdopted.Count -eq 2 -and @($newlyAdopted|Where-Object {$null -ne $_.operation}).Count -eq 0) ('match recovery '+$f.reasonCode)}
                        'missing'{Assert-EvidenceTest ($f.status -ceq 'passed' -and $a.state -ceq 'staged' -and $null -ne $a.putIntent -and $null -eq $a.operation -and -not $stage.adopted -and $null -eq $stage.operation) ('missing recovery '+$f.reasonCode)}
                        'other'{Assert-EvidenceTest ($f.status -ceq 'failed' -and $a.state -ceq 'put-pending') ('other recovery '+$f.reasonCode);Assert-EvidencePromoteRefused $e $s.reference $s.packageSha256 'unresolved-put' 'other promote'}
                    }
                    $s2=Invoke-EvidenceStage $e.ConfigPath $x.Selection
                    if($mode -ceq 'other'){Assert-EvidenceTest ($s2.reasonCode -ceq 'unresolved-put') ('other stage '+$s2.reasonCode);continue}
                    Assert-EvidenceTest ($s2.status -ceq 'passed') ($mode+' stage '+$s2.reasonCode)
                    $f2=Invoke-EvidenceFetch $e.ConfigPath $s2.reference $s2.packageSha256;$u2=Invoke-EvidenceUse $e.ConfigPath $s2.reference 'reviewer' ([DateTimeOffset]::UtcNow.AddHours(1).ToString('o')) 'review'
                    Assert-EvidenceTest ($f2.status -ceq 'passed' -and $u2.status -ceq 'passed') ($mode+' same task '+$f2.reasonCode+'/'+$u2.reasonCode)
                    $id2=(Read-EvidenceReference $s2.reference $e.Config).artifactId;$end=[DateTimeOffset]::UtcNow;Complete-EvidenceTestTask $e $end;Set-EvidenceTestNow $end.AddDays(30).AddSeconds(1)
                    $dry=Invoke-EvidenceCleanup $e.ConfigPath $true;$run=Invoke-EvidenceCleanup $e.ConfigPath
                    $code={param($r,$id)@($r.items|Where-Object artifactId -CEQ $id|Where-Object {$_.reasonCode -cnotlike 'staging-*' -and $_.reasonCode -cne 'operation-pending'}|ForEach-Object reasonCode) -join ','}
                    if($mode -ceq 'match'){Assert-EvidenceTest ((& $code $dry $a.artifactId) -ceq 'expired' -and (& $code $run $a.artifactId).Split(',')[0] -ceq 'deleted' -and -not $e.Objects.ContainsKey($a0.key)) ('match cleanup '+(& $code $run $a.artifactId))}
                    # A staged artifact with a putIntent may have been sent, so expiry deletes it remotely: one DELETE, then the absence GET.
                    else{Assert-EvidenceTest ((& $code $dry $a.artifactId) -ceq 'expired' -and (& $code $run $a.artifactId).Split(',')[0] -ceq 'deleted' -and $e.Counts['delete '+$a0.key] -eq 1 -and $e.Counts['get '+$a0.key] -eq 2 -and -not [IO.File]::Exists($zip) -and (Read-EvidenceTestTask $e).artifacts[0].state -ceq 'deleted') ('missing cleanup '+(& $code $run $a.artifactId))}
                    # The stopped promote's readback staging was never adopted, so the seven-day rule removes it once the artifact is no longer staged.
                    Assert-EvidenceTest (@((Read-EvidenceTestTask $e).staging|Where-Object path -CEQ $op.path).Count -eq 0 -and -not [IO.Directory]::Exists($op.path)) ($mode+' readback staging kept')
                    Assert-EvidenceTest ((& $code $dry $id2) -ceq 'expired' -and (& $code $run $id2) -ceq 'deleted') ($mode+' staged cleanup '+(& $code $run $id2))
                    Set-EvidenceTestValue EvidenceCleanup ProcessHook $null;Reset-EvidencePromoteHooks
                }
            }
            'promote-fetch-pending'{
                # AC7: fetching while a promotion is put-pending resolves it first, as for publish.
                foreach($stored in @($true,$false)){
                    $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;Set-EvidenceTestLostPut $e $stored;$null=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;Set-EvidenceTestValue ArtifactApplication TransportHook $e.Transport
                    $f=Invoke-EvidenceFetch $e.ConfigPath $s.reference $s.packageSha256;$state=(Read-EvidenceTestTask $e).artifacts[0].state
                    if($stored){Assert-EvidenceTest ($f.status -ceq 'passed' -and $state -ceq 'ready') ('pending match '+$f.reasonCode)}else{Assert-EvidenceTest ($f.status -ceq 'passed' -and $state -ceq 'staged') ('pending missing '+$f.reasonCode)}
                }
            }
            'promote-protection-expiry'{
                # I7/AC8: a protection taken while staged survives promotion and holds deletion past the expiry.
                $T=[DateTimeOffset]::FromUnixTimeSeconds([DateTimeOffset]::UtcNow.ToUnixTimeSeconds());Set-EvidenceTestNow $T
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;Set-EvidenceTestNow $T
                $u=Invoke-EvidenceUse $e.ConfigPath $s.reference 'reviewer' ($T.AddDays(30).AddHours(1).ToString('o')) 'review';Assert-EvidenceTest ($u.status -ceq 'passed') ('use '+$u.reasonCode)
                $p=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;$a=(Read-EvidenceTestTask $e).artifacts[0]
                Assert-EvidenceTest ($p.status -ceq 'passed' -and $a.state -ceq 'ready' -and @($a.protections|Where-Object id -CEQ $u.protection).Count -eq 1) ('promote '+$p.reasonCode)
                Complete-EvidenceTestTask $e $T;$i=Invoke-EvidenceInspect $e.ConfigPath fixture
                Assert-EvidenceTest ([DateTimeOffset]::Parse($i.artifacts[0].deleteEligibleAt) -eq $T.AddDays(30)) 'deleteEligibleAt'
                Set-EvidenceTestNow $T.AddDays(30).AddSeconds(1);$c=Invoke-EvidenceCleanup $e.ConfigPath
                Assert-EvidenceTest (@($c.items|Where-Object {$_.artifactId -ceq $a.artifactId -and $_.reasonCode -ceq 'in-use'}).Count -eq 1 -and -not $e.Counts.ContainsKey('delete '+$a.key) -and $e.Objects.ContainsKey($a.key)) 'protected deleted'
                $r=Invoke-EvidenceRelease $e.ConfigPath $s.reference $u.protection;Assert-EvidenceTest ($r.status -ceq 'passed') 'release'
                $c=Invoke-EvidenceCleanup $e.ConfigPath
                Assert-EvidenceTest (@($c.items|Where-Object {$_.artifactId -ceq $a.artifactId -and $_.reasonCode -ceq 'deleted'}).Count -eq 1 -and $e.Counts['delete '+$a.key] -eq 1 -and -not $e.Objects.ContainsKey($a.key)) 'released not deleted'
            }
            'promote-persist-order'{
                # I4/I5/AC12: the PUT sees put-pending with the canonical intent on disk; ready and the receipt wait for readback.
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$a=(Read-EvidenceTestTask $e).artifacts[0];$dir=Get-EvidenceTaskDirectory $e.Config fixture
                $intentPath=[IO.Path]::Combine($dir,'intents',$a.artifactId+'.json');$receiptPath=[IO.Path]::Combine($dir,'receipts',$a.artifactId+'.json')
                $cfg=$e.Config;$readDisk={(Read-EvidenceTask $cfg fixture ([pscustomobject]@{RepositoryId='a'*64;TaskId='fixture'})).artifacts[0]}.GetNewClosure();$probe=@{put=$false;read=$false};$inner=$e.Transport;$innerRead=$e.Readback
                Set-EvidenceTestValue ArtifactApplication TransportHook ({param($g,$req)if($req.Operation -ceq 'put'){$d=& $readDisk;$probe.put=$d.state -ceq 'put-pending' -and $null -ne $d.putIntent -and $d.putIntent.path -ceq $intentPath -and [IO.File]::Exists($intentPath) -and [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($intentPath))).ToLowerInvariant() -ceq $d.putIntent.sha256 -and $null -eq $d.receipt};& $inner $g $req}.GetNewClosure())
                Set-EvidenceTestValue ArtifactApplication ReadbackHook ({param($op,$c,$gen,$key,$bytes,$hash)$d=& $readDisk;$probe.read=$d.state -ceq 'put-pending' -and $null -eq $d.receipt -and -not [IO.File]::Exists($receiptPath);& $innerRead $op $c $gen $key $bytes $hash}.GetNewClosure())
                $p=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($p.status -ceq 'passed' -and $probe.put -and $probe.read) ('order '+$p.reasonCode+' put='+$probe.put+' read='+$probe.read)
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$a=(Read-EvidenceTestTask $e).artifacts[0];$receiptPath=[IO.Path]::Combine((Get-EvidenceTaskDirectory $e.Config fixture),'receipts',$a.artifactId+'.json')
                Set-EvidenceTestValue ArtifactApplication ReadbackHook {throw 'reader-failed'}
                $p=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;$d=(Read-EvidenceTestTask $e).artifacts[0]
                Assert-EvidenceTest ($p.status -ceq 'failed' -and $p.reasonCode -ceq 'unconfirmed' -and $null -eq $p.reference -and $d.state -ceq 'put-pending' -and $null -eq $d.receipt -and -not [IO.File]::Exists($receiptPath)) ('unread ready '+$p.reasonCode)
            }
            'promote-atomic-failure'{
                # I5/AC13: a failed rename around write A, write B or the intent file; disk is read after catch and finally.
                foreach($case in @(@('a-before','promote-after-intent-file','catalog.json','before-rename'),@('a-after','promote-after-intent-file','catalog.json','after-rename'),@('b-before','promote-after-receipt-file','catalog.json','before-rename'),@('b-after','promote-after-receipt-file','catalog.json','after-rename'),@('intent-before','promote-before-lock','\intents\','before-rename'))){
                    $label=$case[0];$arm=$case[1];$match=$case[2];$phase=$case[3]
                    $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$a=(Read-EvidenceTestTask $e).artifacts[0];$dir=Get-EvidenceTaskDirectory $e.Config fixture;$state=@{armed=$false;fired=$false}
                    Set-EvidenceTestValue EvidenceApplication FaultHook ({param($p,$x)if($p -ceq $arm){$state.armed=$true}}.GetNewClosure())
                    Set-EvidenceTestValue EvidencePaths WriteHook ({param($st,$path)if($state.armed -and -not $state.fired -and $st -ceq $phase -and $path.Contains($match)){$state.fired=$true;throw 'crash'}}.GetNewClosure())
                    try{$p=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256}finally{Set-EvidenceTestValue EvidencePaths WriteHook $null;Set-EvidenceTestValue EvidenceApplication FaultHook $null}
                    $d=(Read-EvidenceTestTask $e).artifacts[0];$puts=Get-EvidencePutCount $e;Assert-EvidenceTest $state.fired ($label+' not fired')
                    switch($label){
                        'a-before'{Assert-EvidenceTest ($p.status -ceq 'failed' -and $d.state -ceq 'staged' -and $null -eq $d.putIntent -and $puts -eq 0) $label}
                        'a-after'{Assert-EvidenceTest ($p.status -ceq 'failed' -and $p.reasonCode -ceq 'unconfirmed' -and $d.state -ceq 'put-pending' -and $null -eq $d.operation -and $puts -eq 0) $label}
                        'b-before'{Assert-EvidenceTest ($p.status -ceq 'failed' -and $d.state -ceq 'put-pending' -and $null -eq $d.receipt -and [IO.File]::Exists([IO.Path]::Combine($dir,'receipts',$a.artifactId+'.json')) -and $puts -eq 1) $label}
                        'b-after'{Assert-EvidenceTest ($p.status -ceq 'passed' -and $p.reasonCode -ceq 'complete' -and $d.state -ceq 'ready' -and $puts -eq 1) $label}
                        'intent-before'{Assert-EvidenceTest ($p.status -ceq 'failed' -and $d.state -ceq 'staged' -and -not [IO.File]::Exists([IO.Path]::Combine($dir,'intents',$a.artifactId+'.json')) -and $puts -eq 0) $label}
                    }
                    $q=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;$d=(Read-EvidenceTestTask $e).artifacts[0]
                    switch($label){
                        'a-after'{Assert-EvidenceTest ($q.status -ceq 'passed' -and $q.reasonCode -ceq 'complete' -and $d.state -ceq 'ready' -and (Get-EvidencePutCount $e) -eq 1) ($label+' recovery '+$q.reasonCode)}
                        {$_ -cin @('b-before','b-after')}{Assert-EvidenceTest ($q.reasonCode -ceq 'already-promoted' -and $d.state -ceq 'ready' -and (Get-EvidencePutCount $e) -eq 1) ($label+' again '+$q.reasonCode)}
                        default{Assert-EvidenceTest ($q.status -ceq 'passed' -and $q.reasonCode -ceq 'complete' -and $d.state -ceq 'ready' -and (Get-EvidencePutCount $e) -eq 1) ($label+' again '+$q.reasonCode)}
                    }
                }
            }
            'promote-recovery-resend'{
                # S1/AC1: a stop after write A, a fetch whose recovery sees 404 returns staged with the same intent and copies, then one resend reaches ready.
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$t0=Read-EvidenceTestTask $e;$a0=$t0.artifacts[0];$identity=Get-EvidencePromoteIdentity $a0;$copies=@($a0.ownedCopies|ForEach-Object path)
                Invoke-EvidenceTestPromoteStop $e $s 'promote-after-put-pending';$d=Read-EvidenceTestDisk $e;$intent=$d.artifacts[0].putIntent;$intentPath=Resolve-EvidenceStoredPath $intent.path;$intentSha=Get-EvidenceTestSha ([IO.File]::ReadAllBytes($intentPath));$readback=$d.artifacts[0].operation.path;$known=@($d.staging|ForEach-Object path)
                Assert-EvidenceTest ($d.artifacts[0].state -ceq 'put-pending' -and $intentSha -ceq $intent.sha256 -and (Get-EvidencePutCount $e) -eq 0) 'stopped after write A'
                $f=Invoke-EvidenceFetch $e.ConfigPath $s.reference $s.packageSha256;$t=Read-EvidenceTestTask $e;$a=$t.artifacts[0]
                $rb=@($t.staging|Where-Object path -CEQ $readback);$recovery=@($t.staging|Where-Object {$_.path -cnotin $known -and -not $_.adopted})
                Assert-EvidenceTest ($f.status -ceq 'passed' -and $a.state -ceq 'staged' -and $a.putIntent.path -ceq $intent.path -and $a.putIntent.sha256 -ceq $intentSha -and (Get-EvidenceTestSha ([IO.File]::ReadAllBytes($intentPath))) -ceq $intentSha) ('404 to staged '+$f.reasonCode)
                Assert-EvidenceTest ((Get-EvidencePromoteIdentity $a) -ceq $identity -and @($copies|Where-Object {$_ -cnotin @($a.ownedCopies|ForEach-Object path)}).Count -eq 0 -and $null -eq $a.operation) 'identity or copies changed'
                Assert-EvidenceTest ($rb.Count -eq 1 -and $null -eq $rb[0].operation -and -not $rb[0].adopted -and $recovery.Count -eq 1 -and $null -eq $recovery[0].operation) 'staging after 404'
                $touches=Get-EvidenceRemoteTouches $e;$f2=Invoke-EvidenceFetch $e.ConfigPath $s.reference $s.packageSha256
                Assert-EvidenceTest ($f2.status -ceq 'passed' -and (Get-EvidenceRemoteTouches $e) -eq $touches) ('second fetch not local '+$f2.reasonCode)
                $p=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;$a=(Read-EvidenceTestTask $e).artifacts[0];$receipt=[IO.File]::ReadAllText((Resolve-EvidenceStoredPath $a.receipt.path))|ConvertFrom-Json
                Assert-EvidenceTest ($p.status -ceq 'passed' -and $p.reasonCode -ceq 'complete' -and (Get-EvidencePutCount $e) -eq 1 -and $a.state -ceq 'ready' -and (Get-EvidenceTestSha ([IO.File]::ReadAllBytes($intentPath))) -ceq $intentSha -and $receipt.putIntentSha256 -ceq $intentSha -and (Get-EvidencePromoteIdentity $a) -ceq $identity) ('resend '+$p.reasonCode)
                $gets=$e.Counts['get '+$a0.key];$f3=Invoke-EvidenceFetch $e.ConfigPath $s.reference $s.packageSha256
                Assert-EvidenceTest ($f3.status -ceq 'passed' -and $e.Counts['get '+$a0.key] -eq $gets+1) ('remote fetch '+$f3.reasonCode)
            }
            'promote-resend-same-call'{
                # AC2: (a) a lost response with no object: one PUT per call, the next call recovers 404 and sends once; (b) with the object stored, recovery alone reaches ready.
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;Set-EvidenceTestLostPut $e $false
                $p=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;Set-EvidenceTestValue ArtifactApplication TransportHook $e.Transport
                Assert-EvidenceTest ($p.status -ceq 'failed' -and (Get-EvidencePutCount $e) -eq 1 -and (Read-EvidenceTestTask $e).artifacts[0].state -ceq 'put-pending') ('lost call '+$p.reasonCode)
                $q=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256
                Assert-EvidenceTest ($q.status -ceq 'passed' -and $q.reasonCode -ceq 'complete' -and (Get-EvidencePutCount $e) -eq 2 -and (Read-EvidenceTestTask $e).artifacts[0].state -ceq 'ready') ('resend call '+$q.reasonCode)
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;Set-EvidenceTestLostPut $e $true
                $null=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;Set-EvidenceTestValue ArtifactApplication TransportHook $e.Transport
                $q=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256
                Assert-EvidenceTest ($q.status -ceq 'passed' -and $q.reasonCode -ceq 'already-promoted' -and (Get-EvidencePutCount $e) -eq 1) ('stored call '+$q.reasonCode)
            }
            'promote-failed-recovery'{
                # S2/AC3 (a)(b)(c)(e): a recoverable failed promotion is resolved only by its own promote or fetch; an unresolved one stops nobody else.
                foreach($variant in @('i','ii','iii')){
                    $f=New-EvidenceRecoverableFailed $variant;$e=$f.Env;$s=$f.Stage;$key=$f.Key
                    $p=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;$a=(Read-EvidenceTestTask $e).artifacts[0]
                    Assert-EvidenceTest ($p.status -ceq 'passed' -and $p.reasonCode -ceq 'complete' -and (Get-EvidencePutCount $e) -eq 1 -and $a.state -ceq 'ready') ($variant+' (a) '+$p.reasonCode)
                    $f=New-EvidenceRecoverableFailed $variant;$e=$f.Env;$s=$f.Stage;$key=$f.Key
                    $r=Invoke-EvidenceFetch $e.ConfigPath $s.reference $s.packageSha256;$a=(Read-EvidenceTestTask $e).artifacts[0]
                    Assert-EvidenceTest ($r.status -ceq 'passed' -and $a.state -ceq 'staged' -and $null -ne $a.putIntent -and (Get-EvidenceTestKeyCalls $e $key) -eq 1) ($variant+' (b) 404 '+$r.reasonCode)
                    $f=New-EvidenceRecoverableFailed $variant;$e=$f.Env;$s=$f.Stage;$key=$f.Key;$e.Objects[$key]=$f.Zip;$adoptedBefore=@((Read-EvidenceTestTask $e).staging|Where-Object adopted|ForEach-Object path)
                    $r=Invoke-EvidenceFetch $e.ConfigPath $s.reference $s.packageSha256;$t=Read-EvidenceTestTask $e;$a=$t.artifacts[0];$newly=@($t.staging|Where-Object {$_.adopted -and $_.path -cnotin $adoptedBefore})
                    Assert-EvidenceTest ($r.status -ceq 'passed' -and $a.state -ceq 'ready' -and $null -ne $a.receipt -and $newly.Count -eq 2 -and @($t.staging|Where-Object {$_.path -ceq $f.Readback -and $_.adopted}).Count -eq 0 -and $e.Counts['get '+$key] -eq 2) ($variant+' (b) match '+$r.reasonCode)
                    $f=New-EvidenceRecoverableFailed $variant;$e=$f.Env;$s=$f.Stage;$key=$f.Key;$e.Objects[$key]=[byte[]](1..64)
                    Assert-EvidencePromoteRefused $e $s.reference $s.packageSha256 'unresolved-put' ($variant+' (c) promote')
                    $r=Invoke-EvidenceFetch $e.ConfigPath $s.reference $s.packageSha256;$a=(Read-EvidenceTestTask $e).artifacts[0]
                    Assert-EvidenceTest ($r.status -ceq 'failed' -and $r.reasonCode -ceq 'unconfirmed' -and $null -eq $r.outputPath -and $a.state -ceq 'failed' -and (Get-EvidencePutCount $e) -eq 0) ($variant+' (c) fetch '+$r.reasonCode)
                }
                # (e) with (c)'s failed in the task, every command on the other artifacts succeeds and never touches the failed key.
                $failedKey=$key;$failedId=$a.artifactId;$snapshot={param($x)ConvertTo-Json -Compress -Depth 8 -InputObject @($x.state,$x.putIntent,$x.receipt,$x.ownedCopies,$x.protections)}
                # A publish-origin failed and a stage-origin failed without putIntent sit in the same task and must not change either.
                $s3=Invoke-EvidenceTestStage $e $f.Selection;$plainId=(Read-EvidenceReference $s3.reference $e.Config).artifactId;Edit-EvidenceTestTask $e {param($t)@($t.artifacts|Where-Object artifactId -CEQ $plainId)[0].state='failed'}
                $s2=Invoke-EvidenceTestStage $e $f.Selection
                # The lost publish stays put-pending until the next network preflight turns it into a publish-origin failed.
                Set-EvidenceTestLostPut $e $false;$lost=Invoke-EvidencePublish $e.ConfigPath $f.Selection;Set-EvidenceTestValue ArtifactApplication TransportHook $e.Transport;$publishFailedId=@((Read-EvidenceTestTask $e).artifacts|Where-Object {$_.state -ceq 'put-pending'})[0].artifactId
                $before=@{};$t=Read-EvidenceTestTask $e;foreach($id in @($failedId,$plainId)){$before[$id]=& $snapshot @($t.artifacts|Where-Object artifactId -CEQ $id)[0]};$calls=Get-EvidenceTestKeyCalls $e $failedKey
                $pub=Invoke-EvidencePublish $e.ConfigPath $f.Selection;$publishFailed=@((Read-EvidenceTestTask $e).artifacts|Where-Object artifactId -CEQ $publishFailedId)[0];$before[$publishFailedId]=& $snapshot $publishFailed;$publishCalls=Get-EvidenceTestKeyCalls $e $publishFailed.key
                $local=Invoke-EvidenceFetch $e.ConfigPath $s2.reference $s2.packageSha256;$u=Invoke-EvidenceUse $e.ConfigPath $s2.reference reviewer ([DateTimeOffset]::UtcNow.AddHours(1).ToString('o')) review;$rel=Invoke-EvidenceRelease $e.ConfigPath $s2.reference $u.protection
                $prom=Invoke-EvidencePromote $e.ConfigPath $s2.reference $s2.packageSha256;$remote=Invoke-EvidenceFetch $e.ConfigPath $s2.reference $s2.packageSha256;$s4=Invoke-EvidenceStage $e.ConfigPath $f.Selection
                Assert-EvidenceTest ($lost.status -ceq 'failed' -and $publishFailed.state -ceq 'failed' -and @($pub,$local,$u,$rel,$prom,$remote,$s4|Where-Object status -CNE 'passed').Count -eq 0) ('(e) other commands '+(@($pub,$local,$u,$rel,$prom,$remote,$s4|ForEach-Object reasonCode) -join ','))
                Assert-EvidencePromoteRefused $e $s3.reference $s3.packageSha256 'invalid-source' '(e) failed without intent';$plain=Invoke-EvidenceFetch $e.ConfigPath $s3.reference $s3.packageSha256;Assert-EvidenceTest ($plain.reasonCode -ceq 'identity-mismatch') ('(e) fetch failed without intent '+$plain.reasonCode)
                $t=Read-EvidenceTestTask $e;foreach($id in @($failedId,$plainId,$publishFailedId)){Assert-EvidenceTest ((& $snapshot @($t.artifacts|Where-Object artifactId -CEQ $id)[0]) -ceq $before[$id]) ('(e) failed changed '+$id)}
                Assert-EvidenceTest ((Get-EvidenceTestKeyCalls $e $failedKey) -eq $calls -and (Get-EvidenceTestKeyCalls $e $publishFailed.key) -eq $publishCalls) '(e) failed key touched'
                # (i) with the stopped promote's process still alive, neither command registers, fetches or changes anything.
                $f=New-EvidenceRecoverableFailed 'i';$e=$f.Env;$s=$f.Stage;$key=(Read-EvidenceReference $s.reference $e.Config).key;Set-EvidenceTestValue EvidenceCleanup ProcessHook {param($identity)$false}
                $catalog=[IO.Path]::Combine((Get-EvidenceTaskDirectory $e.Config fixture),'catalog.json');$content={$o=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($catalog)) -DateKind String;$o.generation=0;ConvertTo-Json -InputObject $o -Depth 24 -Compress}.GetNewClosure();$c0=& $content
                Assert-EvidencePromoteRefused $e $s.reference $s.packageSha256 'operation-pending' '(i) running promote'
                $r=Invoke-EvidenceFetch $e.ConfigPath $s.reference $s.packageSha256
                Assert-EvidenceTest ($r.status -ceq 'failed' -and (Get-EvidenceTestKeyCalls $e $key) -eq 0 -and (& $content) -ceq $c0) ('(i) running '+$r.reasonCode)
            }
            'promote-recovery-protection'{
                # I7/AC7: a protection taken while staged survives the stop and the 404 return to staged, and holds the remote deletion past expiry.
                $T=[DateTimeOffset]::FromUnixTimeSeconds([DateTimeOffset]::UtcNow.ToUnixTimeSeconds());Set-EvidenceTestNow $T
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;Set-EvidenceTestNow $T;$key=(Read-EvidenceReference $s.reference $e.Config).key
                $u=Invoke-EvidenceUse $e.ConfigPath $s.reference reviewer ($T.AddDays(30).AddHours(1).ToString('o')) review;Assert-EvidenceTest ($u.status -ceq 'passed') ('use '+$u.reasonCode)
                Invoke-EvidenceTestPromoteStop $e $s 'promote-after-put-pending';$f=Invoke-EvidenceFetch $e.ConfigPath $s.reference $s.packageSha256;$a=(Read-EvidenceTestTask $e).artifacts[0]
                Assert-EvidenceTest ($f.status -ceq 'passed' -and $a.state -ceq 'staged' -and @($a.protections|Where-Object id -CEQ $u.protection).Count -eq 1) ('staged protection '+$f.reasonCode)
                Complete-EvidenceTestTask $e $T;Set-EvidenceTestNow $T.AddDays(30).AddSeconds(1);$c=Invoke-EvidenceCleanup $e.ConfigPath
                Assert-EvidenceTest (@($c.items|Where-Object {$_.artifactId -ceq $a.artifactId -and $_.reasonCode -ceq 'in-use'}).Count -eq 1 -and -not $e.Counts.ContainsKey('delete '+$key) -and (Read-EvidenceTestTask $e).artifacts[0].state -ceq 'staged') 'protected staged deleted'
                $r=Invoke-EvidenceRelease $e.ConfigPath $s.reference $u.protection;Assert-EvidenceTest ($r.status -ceq 'passed') 'release'
                $c=Invoke-EvidenceCleanup $e.ConfigPath
                Assert-EvidenceTest (@($c.items|Where-Object artifactId -CEQ $a.artifactId)[0].reasonCode -ceq 'deleted' -and $e.Counts['delete '+$key] -eq 1 -and (Read-EvidenceTestTask $e).artifacts[0].state -ceq 'deleted') 'released staged kept'
            }
            'promote-fetch-source'{
                # S4/I8/AC8: the source follows the state after preflight's recovery; a failed recovery is unconfirmed with nothing fetched and no fallback.
                foreach($mode in @('match','missing','other')){
                    $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$zip=[IO.File]::ReadAllBytes((Get-EvidenceStagedZip (Read-EvidenceTestTask $e).artifacts[0]).path);$key=(Read-EvidenceReference $s.reference $e.Config).key
                    Invoke-EvidenceTestPromoteStop $e $s 'promote-after-put-pending';if($mode -ceq 'match'){$e.Objects[$key]=$zip}elseif($mode -ceq 'other'){$e.Objects[$key]=[byte[]](1..64)}
                    $f=Invoke-EvidenceFetch $e.ConfigPath $s.reference $s.packageSha256;$state=(Read-EvidenceTestTask $e).artifacts[0].state;$gets=$e.Counts['get '+$key]
                    switch($mode){
                        'match'{Assert-EvidenceTest ($f.status -ceq 'passed' -and $state -ceq 'ready' -and $gets -eq 2) ('match '+$f.reasonCode)}
                        'missing'{Assert-EvidenceTest ($f.status -ceq 'passed' -and $state -ceq 'staged' -and $gets -eq 1) ('missing '+$f.reasonCode)}
                        'other'{Assert-EvidenceTest ($f.status -ceq 'failed' -and $f.reasonCode -ceq 'unconfirmed' -and $null -eq $f.outputPath -and $state -ceq 'put-pending' -and $gets -eq 1) ('other '+$f.reasonCode)}
                    }
                }
            }
            'promote-resend-refusals'{
                # I3/AC10: a resend whose intent file or record disagrees is refused before any PUT; receipt and ended tasks keep their codes.
                $x=New-EvidenceResendFixture;$e=$x.Env;$s=$x.Stage;$text=[IO.File]::ReadAllText($x.IntentPath);[IO.File]::WriteAllText($x.IntentPath,$text+' ',[Text.UTF8Encoding]::new($false))
                Assert-EvidencePromoteRefused $e $s.reference $s.packageSha256 'unresolved-put' '(a) intent sha'
                foreach($field in @('key','packageSha256','artifactId','packageBytes','schemaVersion')){
                    $x=New-EvidenceResendFixture;$e=$x.Env;$s=$x.Stage;$v=[IO.File]::ReadAllText($x.IntentPath)|ConvertFrom-Json -AsHashtable
                    switch($field){'key'{$v.key=$v.key+'x'}'packageSha256'{$v.packageSha256='0'*64}'artifactId'{$v.artifactId='e'*32}'packageBytes'{$v.packageBytes=$v.packageBytes+1}'schemaVersion'{$v.schemaVersion=3}}
                    [IO.File]::WriteAllText($x.IntentPath,(ConvertTo-Json $v -Compress -Depth 8),[Text.UTF8Encoding]::new($false));$sha=Get-EvidenceTestSha ([IO.File]::ReadAllBytes($x.IntentPath))
                    Edit-EvidenceTestTask $e {param($t)$t.artifacts[0].putIntent.sha256=$sha}
                    Assert-EvidencePromoteRefused $e $s.reference $s.packageSha256 'unresolved-put' ('(b) '+$field)
                }
                $x=New-EvidenceResendFixture;$e=$x.Env;$s=$x.Stage;[IO.File]::Delete($x.IntentPath)
                Assert-EvidencePromoteRefused $e $s.reference $s.packageSha256 'unconfirmed' '(c) intent missing'
                $x=New-EvidenceResendFixture;$e=$x.Env;$s=$x.Stage;Edit-EvidenceTestTask $e {param($t)$t.artifacts[0].receipt=[pscustomobject]@{path=$t.artifacts[0].putIntent.path;sha256=$t.artifacts[0].putIntent.sha256}}
                Assert-EvidencePromoteRefused $e $s.reference $s.packageSha256 'invalid-source' 'receipt'
                $x=New-EvidenceResendFixture;$e=$x.Env;$s=$x.Stage;Complete-EvidenceTestTask $e ([DateTimeOffset]::UtcNow)
                Assert-EvidencePromoteRefused $e $s.reference $s.packageSha256 'resume-required' 'ended'
            }
            'promote-resend-persist-order'{
                # I5/AC11: the resend PUT sees put-pending with the same intent on disk; ready and the receipt wait for readback, and a failed readback leaves put-pending.
                $x=New-EvidenceResendFixture;$e=$x.Env;$s=$x.Stage;$a=(Read-EvidenceTestTask $e).artifacts[0];$sha=$a.putIntent.sha256;$receiptPath=[IO.Path]::Combine((Get-EvidenceTaskDirectory $e.Config fixture),'receipts',$a.artifactId+'.json')
                $cfg=$e.Config;$readDisk={(Read-EvidenceTask $cfg fixture ([pscustomobject]@{RepositoryId='a'*64;TaskId='fixture'})).artifacts[0]}.GetNewClosure();$probe=@{put=$false;read=$false};$inner=$e.Transport;$innerRead=$e.Readback
                Set-EvidenceTestValue ArtifactApplication TransportHook ({param($g,$req)if($req.Operation -ceq 'put'){$d=& $readDisk;$probe.put=$d.state -ceq 'put-pending' -and $d.putIntent.sha256 -ceq $sha -and $null -eq $d.receipt};& $inner $g $req}.GetNewClosure())
                Set-EvidenceTestValue ArtifactApplication ReadbackHook ({param($op,$c,$gen,$key,$bytes,$hash)$d=& $readDisk;$probe.read=$d.state -ceq 'put-pending' -and $null -eq $d.receipt -and -not [IO.File]::Exists($receiptPath);& $innerRead $op $c $gen $key $bytes $hash}.GetNewClosure())
                $p=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($p.status -ceq 'passed' -and $probe.put -and $probe.read) ('order '+$p.reasonCode+' put='+$probe.put+' read='+$probe.read)
                $x=New-EvidenceResendFixture;$e=$x.Env;$s=$x.Stage;Set-EvidenceTestValue ArtifactApplication ReadbackHook {throw 'reader-failed'}
                $p=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;$d=(Read-EvidenceTestTask $e).artifacts[0]
                Assert-EvidenceTest ($p.status -ceq 'failed' -and $d.state -ceq 'put-pending' -and $null -eq $d.receipt -and (Get-EvidencePutCount $e) -eq 1) ('unread resend '+$p.reasonCode)
            }
            'promote-cli'{
                # The CLI grammar: `evidence promote` reaches the command; the bare form is not a command.
                $x=New-EvidencePromoteFixture;$e=$x.Env;$s=$x.Stage;$p=Invoke-EvidencePromote $e.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($p.status -ceq 'passed') 'parent promote'
                $c=Invoke-EvidenceTestChildProcess $e @('-Reference',$s.reference,'-Sha256',$s.packageSha256,'-Action','promote')
                Assert-EvidenceTest ($c.Exit -eq 0 -and $c.Json.reasonCode -ceq 'already-promoted' -and $c.Json.locality -ceq 'promoted' -and $c.Err.Length -eq 0 -and -not $c.Out.Contains($e.Root)) ('cli promote '+$c.Out+$c.Err)
                $c=Invoke-EvidenceTestChildProcess $e @('promote','--profile','osm','--config',$e.ConfigPath,'--reference',$s.reference,'--sha256',$s.packageSha256) (Join-Path $PSScriptRoot '../../artifacts.ps1')
                Assert-EvidenceTest ($c.Exit -eq 1 -and $c.Json.reasonCode -ceq 'unsupported-command') ('bare promote '+$c.Out)
            }
        }
    }finally{Reset-EvidencePromoteHooks}
}
$selected=if($Case -ceq '*'){$registered}else{@($Case.Split(','))};$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
foreach($caseName in $selected){
    $executed.Add($caseName)
    try{
        Assert-EvidenceTest ($caseName -cin $registered) 'unknown-case';$env=New-EvidenceTestEnvironment;$fixture=New-EvidenceReaderFixture $env.Root;$selectionPath=[IO.Path]::Combine($env.Root,'selection.json');Write-EvidenceTestJson $selectionPath $fixture.Selection
        switch($caseName){
            'unsupported-schema'{$old=$fixture.Selection;$old.schemaVersion=1;Write-EvidenceTestJson $selectionPath $old;$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'failed' -and $env.Counts.Count -eq 0) 'legacy network';break}
            'selection-path'{$fixture.Selection.files=@('../outside');Write-EvidenceTestJson $selectionPath $fixture.Selection;$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'failed' -and $env.Counts.Count -eq 0) 'path network';break}
            'config-invalid-path'{
                $b=New-EvidenceBadInputs $env $fixture
                foreach($path in @($b.Relative,$b.Missing,$b.Broken,$b.Short,$b.Hex)){$p=Invoke-EvidencePublish $path $b.Selection;Assert-EvidenceTest ($p.status -ceq 'failed' -and $p.reasonCode -ceq 'invalid-evidence-config' -and $p.residue.remote -ceq 'pre-put') ('config code '+$p.reasonCode)}
                $p=Invoke-EvidencePublish $b.Value $b.Selection;Assert-EvidenceTest ($p.reasonCode -ceq 'unsupported-schema') ('value code '+$p.reasonCode)
                Assert-EvidenceNotStarted $env 'config';break
            }
            'config-invalid-path-other-commands'{
                $b=New-EvidenceBadInputs $env $fixture;$codes=@()
                foreach($path in @($b.Relative,$b.Broken)){
                    $codes+=(Invoke-EvidenceFetch $path 'osm-evidence-v2:x' ('0'*64)).reasonCode
                    $codes+=(Invoke-EvidenceInspect $path 'unguarded').reasonCode
                    $codes+=(Invoke-EvidenceUse $path 'osm-evidence-v2:x' 'reviewer' ([DateTimeOffset]::UtcNow.AddHours(1).ToString('o')) 'review').reasonCode
                    $codes+=(Invoke-EvidenceRelease $path 'osm-evidence-v2:x' ('a'*32)).reasonCode
                }
                Assert-EvidenceTest (@($codes|Where-Object {$_ -cne 'invalid-evidence-config'}).Count -eq 0) ('other codes '+($codes -join ','))
                Assert-EvidenceNotStarted $env 'other';break
            }
            'deployment-receipt-missing'{
                $b=New-EvidenceBadInputs $env $fixture;$receipt=[IO.Path]::Combine($env.Root,'deployments',('d'*32)+'.json');$saved=[IO.File]::ReadAllText($receipt)
                [IO.File]::Delete($receipt);$codes=@((Invoke-EvidencePublish $env.ConfigPath $b.Selection).reasonCode,(Invoke-EvidenceInspect $env.ConfigPath 'unguarded').reasonCode)
                $short=$saved|ConvertFrom-Json -AsHashtable;$short.Remove('ownerObservationId');Write-EvidenceTestJson $receipt $short;$codes+=(Invoke-EvidencePublish $env.ConfigPath $b.Selection).reasonCode
                Assert-EvidenceTest (@($codes|Where-Object {$_ -cne 'unknown-deployment'}).Count -eq 0) ('receipt codes '+($codes -join ','))
                Assert-EvidenceNotStarted $env 'receipt';break
            }
            'selection-invalid-path'{
                $b=New-EvidenceBadInputs $env $fixture
                foreach($path in @($b.Relative,$b.Missing,$b.Broken,$b.SelectionShort)){$p=Invoke-EvidencePublish $env.ConfigPath $path;Assert-EvidenceTest ($p.status -ceq 'failed' -and $p.reasonCode -ceq 'invalid-selection' -and $p.residue.remote -ceq 'pre-put') ('selection code '+$p.reasonCode)}
                Assert-EvidenceNotStarted $env 'selection';break
            }
            'invalid-path-no-leak'{
                $b=New-EvidenceBadInputs $env $fixture
                $out=@((Invoke-EvidencePublish $b.Relative $b.Selection),(Invoke-EvidencePublish $b.Broken $b.Selection),(Invoke-EvidencePublish $env.ConfigPath $b.Broken),(Invoke-EvidencePublish $env.ConfigPath $b.Relative),(Invoke-EvidenceInspect $b.Broken 'unguarded'))|ConvertTo-Json -Depth 10
                foreach($marker in @('bad-input-marker','DUMMY_SECRET',$env.Root)){Assert-EvidenceTest (-not $out.Contains($marker)) 'leaked input'};break
            }
            'cli-relative-selection'{
                $info=[Diagnostics.ProcessStartInfo]::new('pwsh');$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
                foreach($arg in @('-NoProfile','-File',(Join-Path $PSScriptRoot 'EvidenceTestChild.ps1'),'-Root',$env.Root,'-ConfigPath',$env.ConfigPath,'-Action','publish','-Selection','bad-input-marker\relative.json')){$info.ArgumentList.Add($arg)}
                $proc=[Diagnostics.Process]::new();$proc.StartInfo=$info;try{$null=$proc.Start();$out=$proc.StandardOutput.ReadToEndAsync();$err=$proc.StandardError.ReadToEndAsync();Assert-EvidenceTest ($proc.WaitForExit(15000)) 'cli bounded';Assert-EvidenceTest ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),3000)) 'cli pipes'
                    $json=@($out.Result -split "`r?`n"|Where-Object {$_.StartsWith('{')})[-1]|ConvertFrom-Json;Assert-EvidenceTest ($proc.ExitCode -eq 1 -and $json.reasonCode -ceq 'invalid-selection' -and $json.residue.remote -ceq 'pre-put' -and $err.Result.Length -eq 0 -and -not $out.Result.Contains('bad-input-marker')) ('cli '+$proc.ExitCode+' '+$out.Result+$err.Result)}finally{$proc.Dispose()}
                Assert-EvidenceNotStarted $env 'cli';break
            }
            'stage-local-fetch-child'{
                # S1: a separate process holding only the reference and hash reads every entry, with all remote paths fatal.
                $s=Invoke-EvidenceTestStage $env $selectionPath;Assert-EvidenceTest ((Get-EvidenceRemoteTouches $env) -eq 0) 'stage touched remote'
                $a=(Read-EvidenceTestTask $env).artifacts[0]
                Assert-EvidenceTest ($a.state -ceq 'staged' -and $a.origin -ceq 'stage' -and $null -eq $a.receipt -and $null -eq $a.putIntent -and $a.key -ceq (Read-EvidenceReference $s.reference $env.Config).key -and $a.packageSha256 -ceq $s.packageSha256) 'staged record'
                $info=[Diagnostics.ProcessStartInfo]::new('pwsh');$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
                foreach($arg in @('-NoProfile','-File',(Join-Path $PSScriptRoot 'EvidenceTestChild.ps1'),'-Root',$env.Root,'-ConfigPath',$env.ConfigPath,'-Reference',$s.reference,'-Sha256',$s.packageSha256,'-Action','fetch-offline')){$info.ArgumentList.Add($arg)}
                $proc=[Diagnostics.Process]::new();$proc.StartInfo=$info
                try{$null=$proc.Start();$out=$proc.StandardOutput.ReadToEndAsync();$err=$proc.StandardError.ReadToEndAsync();Assert-EvidenceTest ($proc.WaitForExit(30000)) 'child bounded';Assert-EvidenceTest ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),3000)) 'child pipes'
                    Assert-EvidenceTest ($proc.ExitCode -eq 0) ('offline child fetch '+$out.Result+$err.Result);$json=@($out.Result -split "`r?`n"|Where-Object {$_.StartsWith('{')})[-1]|ConvertFrom-Json}finally{$proc.Dispose()}
                foreach($name in $fixture.Files){Assert-EvidenceTest ((Get-FileHash -LiteralPath ([IO.Path]::Combine($json.outputPath,$name))).Hash -ceq (Get-FileHash -LiteralPath ([IO.Path]::Combine($fixture.Source,$name))).Hash) ('entry differs '+$name)}
                Assert-EvidenceTest ((Get-EvidenceRemoteTouches $env) -eq 0) 'parent remote touched';break
            }
            'stage-local-fetch-reject'{
                # I2: every rejection is final and never falls back to the remote store.
                $s=Invoke-EvidenceTestStage $env $selectionPath
                Edit-EvidenceTestTask $env {param($t)$t.artifacts[0].packageSha256='0'*64};$f=Invoke-EvidenceFetch $env.ConfigPath $s.reference $s.packageSha256
                Assert-EvidenceTest ($f.status -ceq 'failed' -and $f.reasonCode -ceq 'identity-mismatch' -and $null -eq $f.outputPath) ('hash rewrite '+$f.reasonCode)
                Edit-EvidenceTestTask $env {param($t)$t.artifacts[0].packageSha256=$s.packageSha256}
                $g=Enter-WorkflowTaskGuard ('a'*64) 'other';try{$null=Invoke-WorkflowTaskTransition ('a'*64) 'other' 'started' 0 'other-start' '' $g}finally{$g.Dispose()}
                $ref=Read-EvidenceReference $s.reference $env.Config;$ref.taskId='other';$ref.key=Get-EvidenceKey $env.Config 'other' $ref.artifactId
                $f=Invoke-EvidenceFetch $env.ConfigPath (ConvertTo-EvidenceTestReference $ref) $s.packageSha256;Assert-EvidenceTest ($f.status -ceq 'failed' -and $f.reasonCode -ceq 'catalog-missing') ('other task '+$f.reasonCode)
                [IO.File]::Delete((Get-EvidenceStagedZip (Read-EvidenceTestTask $env).artifacts[0]).path)
                $f=Invoke-EvidenceFetch $env.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($f.status -ceq 'failed' -and $f.reasonCode -ceq 'changed-copy' -and $null -eq $f.outputPath) ('zip missing '+$f.reasonCode)
                Assert-EvidenceTest ((Get-EvidenceRemoteTouches $env) -eq 0) 'rejection touched remote';break
            }
            'stage-unresolved-refused'{
                # I1: an unfinished put or delete in the task is refused, never resolved, by stage and local fetch.
                $s=Invoke-EvidenceTestStage $env $selectionPath
                $inner=$env.Transport;$hook={param($g,$req)$r=& $inner $g $req;if($req.Operation -ceq 'put'){throw 'response-lost'};$r}.GetNewClosure();Set-EvidenceTestValue ArtifactApplication TransportHook $hook
                $p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'failed' -and @((Read-EvidenceTestTask $env).artifacts|Where-Object state -CEQ 'put-pending').Count -eq 1) 'put-pending fixture'
                $before=Get-EvidenceRemoteTouches $env
                $x=Invoke-EvidenceStage $env.ConfigPath $selectionPath;Assert-EvidenceTest ($x.status -ceq 'failed' -and $x.reasonCode -ceq 'unresolved-put') ('stage put '+$x.reasonCode)
                $f=Invoke-EvidenceFetch $env.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($f.status -ceq 'failed' -and $f.reasonCode -ceq 'unresolved-put') ('fetch put '+$f.reasonCode)
                Assert-EvidenceTest ((Get-EvidenceRemoteTouches $env) -eq $before) 'put resolved over network'
                $env=New-EvidenceTestEnvironment;$fixture=New-EvidenceReaderFixture $env.Root;Write-EvidenceTestJson $selectionPath $fixture.Selection
                $p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'passed') 'ready fixture';$s=Invoke-EvidenceTestStage $env $selectionPath
                $readyId=(Read-EvidenceReference $p.reference $env.Config).artifactId
                Edit-EvidenceTestTask $env {param($t)$r=@($t.artifacts|Where-Object artifactId -CEQ $readyId)[0];$r.state='delete-pending';$r.deleteIntent=[pscustomobject]@{key=$r.key;generation=$t.generation;packageSha256=$r.packageSha256;process=[pscustomobject]@{pid=2147483647;startedAt='2000-01-01T00:00:00.0000000Z'};createdAt=[DateTimeOffset]::UtcNow.ToString('o')}}
                $before=Get-EvidenceRemoteTouches $env
                $x=Invoke-EvidenceStage $env.ConfigPath $selectionPath;Assert-EvidenceTest ($x.status -ceq 'failed' -and $x.reasonCode -ceq 'unresolved-delete') ('stage delete '+$x.reasonCode)
                $f=Invoke-EvidenceFetch $env.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($f.status -ceq 'failed' -and $f.reasonCode -ceq 'unresolved-delete') ('fetch delete '+$f.reasonCode)
                Assert-EvidenceTest ((Get-EvidenceRemoteTouches $env) -eq $before -and $env.Objects.Count -eq 1) 'delete resolved over network';break
            }
            'stage-source-publish'{
                # I5: publishing from a staged staging makes another artifact; the staged one is never sent.
                $s=Invoke-EvidenceTestStage $env $selectionPath;$t=Read-EvidenceTestTask $env;$a=$t.artifacts[0];$stagePath=@($t.staging|Where-Object artifactId -CEQ $a.artifactId)[0].path;$staged=ConvertTo-Json $a -Depth 24 -Compress
                $sourcePath=[IO.Path]::Combine($env.Root,'source-selection.json');Write-EvidenceTestJson $sourcePath ([ordered]@{schemaVersion=2;purpose='evidence';taskId='fixture';root=$stagePath;files=@($fixture.Files|ForEach-Object{'snapshot/'+$_});base=$fixture.Selection.base;head=$fixture.Selection.head})
                $p=Invoke-EvidencePublish $env.ConfigPath $sourcePath;Assert-EvidenceTest ($p.status -ceq 'passed') ('source publish '+$p.reasonCode)
                $t=Read-EvidenceTestTask $env;$other=@($t.artifacts|Where-Object artifactId -CNE $a.artifactId)
                Assert-EvidenceTest ($other.Count -eq 1 -and $other[0].state -ceq 'ready' -and (Get-EvidenceArtifactOrigin $other[0]) -ceq 'publish' -and $p.reference -cne $s.reference) 'separate artifact'
                Assert-EvidenceTest ((ConvertTo-Json @($t.artifacts|Where-Object artifactId -CEQ $a.artifactId)[0] -Depth 24 -Compress) -ceq $staged) 'staged artifact changed'
                Assert-EvidenceTest (-not $env.Counts.ContainsKey('put '+$a.key) -and @($env.Counts.Keys|Where-Object {$_ -like 'put *'}).Count -eq 1) 'staged key sent';break
            }
            'stage-changes-patch'{
                # S1/AC11: a working tree with added, modified, deleted, untracked and binary files.
                $repo=[IO.Path]::Combine($env.Root,'repo');[IO.Directory]::CreateDirectory($repo)|Out-Null
                $null=Invoke-EvidenceTestGit $repo @('init','-q');foreach($c in @(@('user.email','fixture@example.invalid'),@('user.name','fixture'),@('core.autocrlf','false'))){$null=Invoke-EvidenceTestGit $repo (@('config')+$c)}
                foreach($n in @('keep.txt','mod.txt','del.txt')){[IO.File]::WriteAllText([IO.Path]::Combine($repo,$n),"base $n`n")};[IO.File]::WriteAllBytes([IO.Path]::Combine($repo,'bin.dat'),[byte[]](0..255))
                $null=Invoke-EvidenceTestGit $repo @('add','-A');$null=Invoke-EvidenceTestGit $repo @('commit','-qm','base');$base=(Invoke-EvidenceTestGit $repo @('rev-parse','HEAD')).Trim()
                [IO.File]::WriteAllText([IO.Path]::Combine($repo,'mod.txt'),"changed`n");[IO.File]::WriteAllText([IO.Path]::Combine($repo,'add.txt'),"added`n");$null=Invoke-EvidenceTestGit $repo @('add','add.txt')
                [IO.File]::Delete([IO.Path]::Combine($repo,'del.txt'));[IO.Directory]::CreateDirectory([IO.Path]::Combine($repo,'new'))|Out-Null;[IO.File]::WriteAllText([IO.Path]::Combine($repo,'new','u.txt'),"untracked`n");[IO.File]::WriteAllBytes([IO.Path]::Combine($repo,'bin.dat'),[byte[]](255..0))
                $indexHash=(Get-FileHash -LiteralPath ([IO.Path]::Combine($repo,'.git','index'))).Hash
                # The README procedure: a temporary index, never the real one.
                $reviewInput=[IO.Path]::Combine($env.Root,'review-input');[IO.Directory]::CreateDirectory($reviewInput)|Out-Null;$patch=[IO.Path]::Combine($reviewInput,'changes.patch')
                $env:GIT_INDEX_FILE=[IO.Path]::Combine($env.Root,'review.index')
                try{$null=Invoke-EvidenceTestGit $repo @('read-tree',$base);$null=Invoke-EvidenceTestGit $repo @('add','-A');$null=Invoke-EvidenceTestGit $repo @('diff','--cached','--binary','--no-color','--no-renames',"--output=$patch",$base)
                    $changed=@(Invoke-EvidenceTestGit $repo @('diff','--cached','--name-only','--no-renames','--diff-filter=d',$base));$deleted=@(Invoke-EvidenceTestGit $repo @('diff','--cached','--name-only','--no-renames','--diff-filter=D',$base))}
                finally{Remove-Item Env:GIT_INDEX_FILE}
                Assert-EvidenceTest ((Get-FileHash -LiteralPath ([IO.Path]::Combine($repo,'.git','index'))).Hash -ceq $indexHash) 'real index touched'
                Assert-EvidenceTest ((($changed|Sort-Object) -join ',') -ceq 'add.txt,bin.dat,mod.txt,new/u.txt' -and ($deleted -join ',') -ceq 'del.txt') ('change set '+($changed -join ',')+' / '+($deleted -join ','))
                [IO.File]::WriteAllText([IO.Path]::Combine($reviewInput,'base.txt'),$base+"`n");foreach($n in $changed){$to=[IO.Path]::Combine($reviewInput,'files',$n);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to))|Out-Null;[IO.File]::Copy([IO.Path]::Combine($repo,$n),$to)}
                Write-EvidenceTestJson $selectionPath ([ordered]@{schemaVersion=2;purpose='evidence';taskId='fixture';root=$reviewInput;files=@(@('base.txt','changes.patch')+@($changed|ForEach-Object{'files/'+$_}));base=$base;head=$base})
                $s=Invoke-EvidenceTestStage $env $selectionPath;$f=Invoke-EvidenceFetch $env.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($f.status -ceq 'passed') ('local fetch '+$f.reasonCode)
                $fetchedBase=([IO.File]::ReadAllText([IO.Path]::Combine($f.outputPath,'base.txt'))).Trim()
                $baseDir=[IO.Path]::Combine($env.Root,'check-base');$applied=[IO.Path]::Combine($env.Root,'check-applied')
                foreach($dir in @($baseDir,$applied)){& git clone -q -c core.autocrlf=false $repo $dir;if($LASTEXITCODE){throw 'clone'};$null=Invoke-EvidenceTestGit $dir @('checkout','-q',$fetchedBase)}
                $null=Invoke-EvidenceTestGit $applied @('apply','--binary',[IO.Path]::Combine($f.outputPath,'changes.patch'))
                $recorded=Get-EvidenceTestTreeHashes ([IO.Path]::Combine($f.outputPath,'files'));$baseHashes=Get-EvidenceTestTreeHashes $baseDir;$appliedHashes=Get-EvidenceTestTreeHashes $applied
                $problems=Get-EvidenceTestPatchProblems $baseHashes $appliedHashes $recorded $deleted;Assert-EvidenceTest ($problems.Count -eq 0) ('patch mismatch '+($problems -join ','))
                Assert-EvidenceTest ($baseHashes['keep.txt'] -ceq $appliedHashes['keep.txt'] -and -not $recorded.ContainsKey('keep.txt')) 'unchanged file recorded'
                # The comparison must notice a recorded set that forgot one changed file.
                $short=@{}+$recorded;$short.Remove('bin.dat');Assert-EvidenceTest ((Get-EvidenceTestPatchProblems $baseHashes $appliedHashes $short $deleted) -ccontains 'unrecorded-change:bin.dat') 'omission not detected';break
            }
            'remote-fetch-codes-unchanged'{
                # I4: a missing copy of a ready artifact still reports the remote fetch's old generic code.
                $p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'passed') 'publish'
                [IO.File]::Delete((Read-EvidenceTestTask $env).artifacts[0].ownedCopies[0].path)
                $f=Invoke-EvidenceFetch $env.ConfigPath $p.reference $p.packageSha256;Assert-EvidenceTest ($f.status -ceq 'failed' -and $f.reasonCode -ceq 'unconfirmed') ('remote code '+$f.reasonCode);break
            }
            'stage-catalog-read'{
                # The catalog reader: optional origin, staged without receipt, and stage-origin deletion without receipt.
                $null=Invoke-EvidenceTestStage $env $selectionPath
                Edit-EvidenceTestTask $env {param($t)$t.artifacts[0].state='deleted'};Assert-EvidenceTest ((Read-EvidenceTestTask $env).artifacts[0].state -ceq 'deleted') 'stage deletion unread'
                $rejected=$null;Edit-EvidenceTestTask $env {param($t)$t.artifacts[0].PSObject.Properties.Remove('origin')};try{$null=Read-EvidenceTestTask $env}catch{$rejected=$_.Exception.Message};Assert-EvidenceTest ($rejected -ceq 'receipt-missing') ('origin-less deletion '+$rejected)
                $catalog=[IO.Path]::Combine((Get-EvidenceTaskDirectory $env.Config fixture),'catalog.json');$doc=ConvertFrom-Json ([IO.File]::ReadAllText($catalog)) -DateKind String;$doc.artifacts[0]|Add-Member -NotePropertyName origin -NotePropertyValue 'remote';$doc.artifacts[0].state='staged';Write-EvidenceTestJson $catalog $doc
                $rejected=$null;try{$null=Read-EvidenceTestTask $env}catch{$rejected=$_.Exception.Message};Assert-EvidenceTest ($rejected -ceq 'corrupt-state') ('unknown origin '+$rejected);break
            }
            {$_ -like 'promote-*'}{Invoke-EvidencePromoteCase $caseName;break}
            'one-put-lost-response'{$inner=$env.Transport;$hook={param($g,$req)$r=& $inner $g $req;if($req.Operation -ceq 'put'){throw 'response-lost'};$r}.GetNewClosure();Set-EvidenceTestValue ArtifactApplication TransportHook $hook;$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'failed' -and @($env.Counts.Values|Where-Object {$_ -gt 1}).Count -eq 0 -and $env.Objects.Count -eq 1) 'one-put';break}
            'readback-failure'{Set-EvidenceTestValue ArtifactApplication ReadbackHook {throw 'reader-failed'};$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'failed' -and $null -eq $p.reference) 'unverified success';break}
            'receipt-atomic-failure'{Set-EvidenceTestValue EvidencePaths WriteHook {param($stage,$path)if($stage -ceq 'before-rename' -and $path -like '*receipts*'){throw 'crash'}};$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'failed' -and $null -eq $p.reference) 'receipt success';break}
            'secret-exception-output'{Set-EvidenceTestValue ArtifactApplication TransportHook {throw 'DUMMY_SECRET'};$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ((ConvertTo-Json $p -Depth 10) -cnotmatch 'DUMMY_SECRET') 'secret output';break}
            'legacy-cli-network-zero'{
                $info=[Diagnostics.ProcessStartInfo]::new('pwsh');$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
                foreach($arg in @('-NoProfile','-File',(Join-Path $PSScriptRoot '../../artifacts.ps1'),'evidence','commit','--candidate','dummy','--candidate-sha256',('a'*64),'--settings-after','dummy','--settings-after-sha256',('a'*64))){$info.ArgumentList.Add($arg)}
                $proc=[Diagnostics.Process]::new();$proc.StartInfo=$info;try{$null=$proc.Start();$out=$proc.StandardOutput.ReadToEndAsync();$err=$proc.StandardError.ReadToEndAsync();Assert-EvidenceTest ($proc.WaitForExit(10000)) 'legacy bounded';Assert-EvidenceTest ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),3000)) 'legacy pipes';Assert-EvidenceTest ($proc.ExitCode -eq 1 -and $out.Result -match 'unsupported-command' -and $env.Counts.Count -eq 0) 'legacy accepted'}finally{$proc.Dispose()};break
            }
            'put-recovery-child-protected'{
                $inner=$env.Transport;$hook={param($g,$req)$r=& $inner $g $req;if($req.Operation -ceq 'put'){throw 'response-lost'};$r}.GetNewClosure();Set-EvidenceTestValue ArtifactApplication TransportHook $hook;$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath
                $readback={param($op,$c,$gen,$key,$bytes,$hash)Write-EvidenceTestJson ([IO.Path]::Combine($op,'readback-process.json')) ([pscustomobject]@{process=Get-EvidenceProcessIdentity;exited=$false;pipesClosed=$false});throw 'unconfirmed-child'}.GetNewClosure();Set-EvidenceTestValue ArtifactApplication ReadbackHook $readback
                $g=Enter-WorkflowTaskGuard ('a'*64) fixture
                try{
                    $rejected=$false;try{$null=Assert-EvidenceTransitionSafe $env.Config fixture $g}catch{$rejected=$true};Assert-EvidenceTest $rejected 'unknown recovery child accepted'
                    $task=Read-EvidenceTask $env.Config fixture $g;$artifact=$task.artifacts[0];Assert-EvidenceTest ($null -ne $artifact.operation -and [IO.File]::Exists($artifact.operation.childMarker)) 'recovery child ownership missing'
                    $stage=@($task.staging|Where-Object path -CEQ $artifact.operation.path);Assert-EvidenceTest ($stage.Count -eq 1 -and -not $stage[0].adopted) 'recovery stage missing'
                    $stopped=[pscustomobject]@{pid=2147483647;startedAt='2000-01-01T00:00:00.0000000Z'};$artifact.operation.process=$stopped;$stage[0].operation.process=$stopped;$null=Write-EvidenceTask $env.Config fixture $task $task.generation $g;$recoveryPath=$stage[0].path
                }finally{$g.Dispose()}
                $before=($env.Counts.Values|Measure-Object -Sum).Sum;$later=[DateTimeOffset]::UtcNow.AddDays(8);Set-EvidenceTestValue EvidenceCleanup Clock {$later}.GetNewClosure();$clean=Invoke-EvidenceCleanup $env.ConfigPath
                Assert-EvidenceTest ($clean.status -ceq 'pending' -and $env.Objects.Count -eq 1 -and [IO.Directory]::Exists($recoveryPath) -and ($env.Counts.Values|Measure-Object -Sum).Sum -eq $before) 'unknown recovery child released or retried'
                break
            }
            'put-reconcile-no-reput'{
                $inner=$env.Transport;$hook={param($g,$req)$r=& $inner $g $req;if($req.Operation -ceq 'put'){throw 'response-lost'};$r}.GetNewClosure();Set-EvidenceTestValue ArtifactApplication TransportHook $hook;$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath
                $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$null=Assert-EvidenceTransitionSafe $env.Config fixture $g;$task=Read-EvidenceTask $env.Config fixture $g;Assert-EvidenceTest ($task.artifacts[0].state -ceq 'ready' -and @($env.Counts.GetEnumerator()|Where-Object {$_.Key -like 'put *' -and $_.Value -gt 1}).Count -eq 0) 'recovery reput';foreach($owned in $task.artifacts[0].ownedCopies){Assert-EvidenceTest (@($task.staging|Where-Object {$owned.path.StartsWith($_.path+[IO.Path]::DirectorySeparatorChar,[StringComparison]::Ordinal)}).Count -gt 0) 'put recovery ownership missing'}}finally{$g.Dispose()};break
            }
            default{
                $p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'passed') ('publish failed '+$p.reasonCode)
                if($caseName -ceq 'fresh-child-fetch'){
                    $ref=Read-EvidenceReference $p.reference $env.Config;[IO.File]::WriteAllBytes([IO.Path]::Combine($env.Root,'remote.zip'),$env.Objects[$ref.key])
                    $info=[Diagnostics.ProcessStartInfo]::new('pwsh');$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
                    foreach($arg in @('-NoProfile','-File',(Join-Path $PSScriptRoot 'EvidenceTestChild.ps1'),'-Root',$env.Root,'-ConfigPath',$env.ConfigPath,'-Reference',$p.reference,'-Sha256',$p.packageSha256)){$info.ArgumentList.Add($arg)}
                    $proc=[Diagnostics.Process]::new();$proc.StartInfo=$info;try{$null=$proc.Start();$out=$proc.StandardOutput.ReadToEndAsync();$err=$proc.StandardError.ReadToEndAsync();Assert-EvidenceTest ($proc.WaitForExit(15000)) 'child bounded';Assert-EvidenceTest ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),3000)) 'child pipes';Assert-EvidenceTest ($proc.ExitCode -eq 0 -and $out.Result -match '"status":"passed"') ('child fetch '+$err.Result+$out.Result)}finally{$proc.Dispose()};break
                }
                $hash=if($caseName -ceq 'reader-hash'){'0'*64}else{$p.packageSha256};$reference=$p.reference
                if($caseName -ceq 'handoff-identity-substitution'){$ref=Read-EvidenceReference $reference $env.Config;$ref.head='9'*40;$reference='osm-evidence-v2:'+ [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json $ref -Compress))).TrimEnd('=').Replace('+','-').Replace('/','_')}
                $f=Invoke-EvidenceFetch $env.ConfigPath $reference $hash
                if($caseName -ceq 'v2-roundtrip'){Assert-EvidenceTest ($f.status -ceq 'passed') ('fetch failed '+$f.reasonCode);foreach($name in $fixture.Files){Assert-EvidenceTest ((Get-FileHash -LiteralPath ([IO.Path]::Combine($f.outputPath,$name))).Hash -ceq (Get-FileHash -LiteralPath ([IO.Path]::Combine($fixture.Source,$name))).Hash) 'inner hash'}}else{Assert-EvidenceTest ($f.status -ceq 'failed' -and $null -eq $f.outputPath) 'reader accepted substitution'}
            }
        }
    }catch{$failed.Add($caseName);[Console]::Error.WriteLine($caseName+': '+$_.Exception.Message)}
    finally{Set-EvidenceTestValue EvidencePaths WriteHook $null}
}
Complete-EvidenceTestSuite $ResultPath $registered $selected $executed $failed
[Console]::WriteLine("ArtifactEvidence $($executed.Count)/$($registered.Count), failed $($failed.Count)")
