param([string]$ResultPath='')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
$module=Import-Module (Join-Path $PSScriptRoot '../EvidenceReset.psm1') -Force -PassThru
$root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-reset-'+[Guid]::NewGuid().ToString('N'))
$cases=@('strict-manifest','manifest-hash','foreign-key','foreign-identity','rule-tuple','owner-receipt-hash','owner-id-substitution','owner-tuple-substitution','owner-unknown-impact','owner-multiple-correspondence','source-excluded','in-use-excluded','root-path-excluded','nested-exclusion','reparse-path','local-changed','dry-run-network-zero','partial-idempotent-retry','crash-after-remote','secret-exception-output','cutover-missing','schema-type','duplicate-json-property','etag-only-reset','etag-mismatch','etag-missing','hash-identity-alternative','etag-observation-boundary')
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
function Assert([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
function Reject([scriptblock]$Action){$rejected=$false;try{&$Action|Out-Null}catch{$rejected=$true};Assert $rejected 'expected rejection'}
function Json([string]$Path,$Value){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))|Out-Null;[IO.File]::WriteAllText($Path,(ConvertTo-Json -Depth 30 -InputObject $Value),[Text.UTF8Encoding]::new($false));return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
$script:objects=@{};$script:calls=[Collections.Generic.List[string]]::new();$script:failKey='';$script:secretFault=$false;$script:etagMode='match'
$network={
    param($request)
    $script:calls.Add($request.Operation+':'+$request.Key)
    if($script:secretFault){throw 'DUMMY_SECRET_VALUE'}
    if($request.Key -ceq $script:failKey){return @{Operation=$request.Operation;HttpStatus=403;StatusClass='forbidden';S3Code='AccessDenied';StatusObserved=$true;TimedOut=$false;Redirected=$false;Bytes=$null;Sha256=$null}}
    if($request.Operation -eq 'delete'){$script:objects.Remove($request.Key);return @{Operation='delete';HttpStatus=204;StatusClass='success';S3Code=$null;StatusObserved=$true;TimedOut=$false;Redirected=$false;Bytes=$null;Sha256=$null}}
    if(-not $script:objects.ContainsKey($request.Key)){return @{Operation='get';HttpStatus=404;StatusClass='not-found';S3Code='NoSuchKey';StatusObserved=$true;TimedOut=$false;Redirected=$false;Bytes=$null;Sha256=$null}}
    $bytes=$script:objects[$request.Key];[IO.File]::WriteAllBytes($request.DestinationPath,$bytes)
    return @{Operation='get';HttpStatus=200;StatusClass='success';S3Code=$null;StatusObserved=$true;TimedOut=$false;Redirected=$false;Bytes=$bytes.Length;ETag=$(if($script:etagMode -ceq 'missing'){$null}elseif($script:etagMode -ceq 'mismatch'){'f'*32}else{'a'*32});Sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()}
}
function Fixture([string]$Case){
    $work=[IO.Path]::Combine($root,$Case);$artifact=[IO.Path]::Combine($work,'artifacts')
    [IO.Directory]::CreateDirectory($artifact)|Out-Null
    & $module {param($r,$t)$script:TestRoot=$r;$script:TransportHook=$t;$script:FaultHook=$null} $artifact $network
    $bytes=[Text.Encoding]::UTF8.GetBytes('non-secret-reset-fixture');$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    $keys=@(& $module {Get-ResetKnownKeys})
    $script:objects=@{};$script:calls.Clear();$script:failKey='';$script:secretFault=$false;$script:etagMode='match'
    $objects=@();foreach($key in $keys){$script:objects[$key]=$bytes;$objects+=@(@{key=$key;expectedBytes=$bytes.Length;expectedHash=$hash;etag=$null;observedExists=$true;relatedRuleIds=@()})}
    $rules=@();$verifications=@()
    foreach($id in @('continuity-w','continuity-l','first-use-age30d','probe-age1d')){
        $tuple=& $module {param($i)Get-ResetRuleTuple $i} $id
        $rules+=@(@{targetId=$id;selector=$(if($tuple.ruleId){'exact-id'}else{'exact-tuple'});ruleId=$tuple.ruleId;enabled=$tuple.enabled;prefix=$tuple.prefix;condition=$tuple.condition;approvedMatchingScope=@($keys | Where-Object {$_.StartsWith($tuple.prefix,[StringComparison]::Ordinal)});excludedKeys=@();action='remove'})
        $verifications+=@(@{targetId=$id;actualRuleId=$(if($tuple.ruleId){$tuple.ruleId}else{$id+'-actual'});matchedTuple=@{enabled=$tuple.enabled;prefix=$tuple.prefix;condition=$tuple.condition};excludesUnaffected=$true;observer='offline-owner';observedAt='2026-01-01T00:00:00+00:00';statement='offline owner observation'})
    }
    $relative='transfers/5a2bd180c4c04112bd1d2de71bc6ebb2/ready';$target=[IO.Path]::GetFullPath([IO.Path]::Combine($artifact,$relative))
    [IO.Directory]::CreateDirectory($target)|Out-Null;[IO.File]::WriteAllBytes([IO.Path]::Combine($target,'item.txt'),$bytes)
    $manifest=@{schemaVersion=1;purpose='development-reset-v1';repositoryId='4d2d1728f1db77f50605a10da0bbf10fd31443c381c4f312658134fffb850b3b';endpoint='https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com';bucket='osm-artifacts';deploymentId='d'*32;observedAt='2026-01-01T00:00:00+00:00';frozenAt='2026-01-01T00:00:00+00:00';objects=$objects;localTargets=@(@{relativePath=$relative;resolvedPath=$target;ownerTask='old-task';kind='storage-fetch-copy';observedExists=$true;entries=@(@{path='item.txt';bytes=$bytes.Length;sha256=$hash});source=$false;inUse=$false});rules=$rules;excludedKeys=@('development/evidence/v2/');excludedPaths=@([IO.Path]::Combine($artifact,'credentials'),[IO.Path]::Combine($artifact,'runtime'));preservedRules=@()}
    return @{work=$work;artifact=$artifact;manifest=$manifest;verification=@{schemaVersion=1;purpose='owner-rule-verification';frozenManifestSha256='';verifications=$verifications};target=$target}
}
function Freeze($Fixture,[bool]$Cutover=$true){
    $m=[IO.Path]::Combine($Fixture.work,'manifest.json');$hash=Json $m $Fixture.manifest
    $Fixture.verification.frozenManifestSha256=$hash;$v=[IO.Path]::Combine($Fixture.work,'owner.json');$vh=Json $v $Fixture.verification
    if($Cutover){
        $c=[IO.Path]::Combine($Fixture.work,'cutover.json');$ch=Json $c @{schemaVersion=1;purpose='reset-cutover-observation';frozenManifestSha256=$hash;oldEntryDisabled=$true;childrenStopped=$true;rulesChanged=$true;observer='offline-test';observedAt='2026-01-01T00:00:00+00:00';statement='original offline cutover observation'}
        & $module {param($h,$p,$ph)Register-EvidenceResetCutover $h $p $ph} $hash $c $ch
    }
    return @{manifestPath=$m;manifestHash=$hash;verificationPath=$v;verificationHash=$vh}
}
function Run($Frozen,[bool]$Dry=$false){return Invoke-EvidenceReset $Frozen.manifestPath $Frozen.manifestHash $Frozen.verificationPath $Frozen.verificationHash 'osm' $Dry}
try{
    foreach($case in $cases){
        try{
            $fixture=Fixture $case
            switch($case){
                'strict-manifest'{$f=Freeze $fixture;Assert ((Run $f).status -eq 'passed') 'reset failed'}
                'hash-identity-alternative'{
                    foreach($object in $fixture.manifest.objects){$object.etag='a'*32};$script:etagMode='missing'
                    Assert ((Run (Freeze $fixture)).status -eq 'passed') 'valid hash alternative rejected'
                }
                'etag-observation-boundary'{
                    $assemblyPath=Join-Path $PSScriptRoot '../Transport/artifacts/transport/R2ArtifactTransport.dll'
                    $assembly=[Reflection.Assembly]::LoadFrom($assemblyPath)
                    $type=$assembly.GetType('OneStarMaker.Artifacts.Transport.R2ArtifactTransport',$true)
                    $method=$type.GetMethod('NormalizeETag',[Reflection.BindingFlags]'NonPublic,Static')
                    $valid=$method.Invoke($null,[object[]]@(('"'+('A'*32)+'-2"')))
                    Assert ($valid -ceq (('a'*32)+'-2')) 'finite quoted etag not normalized'
                    foreach($unsafe in @('DUMMY_SECRET', (('a'*32)+"`r`nDUMMY_SECRET"), ('"'+('a'*32)+'"extra'), (('a'*32)+"`n"))){
                        Assert ($null -eq $method.Invoke($null,[object[]]@($unsafe))) 'unsafe SDK text crossed etag observation'
                    }
                }
                'etag-only-reset'{
                    foreach($object in $fixture.manifest.objects){$object.expectedHash=$null;$object.etag='a'*32}
                    $f=Freeze $fixture;Assert ((Run $f $true).status -eq 'passed' -and $script:calls.Count -eq 0) 'etag dry-run'
                    $result=Run $f;Assert ($result.status -eq 'passed' -and $result.deleted -eq 7 -and $script:objects.Count -eq 0) 'etag-only reset blocked'
                }
                'etag-mismatch'{
                    foreach($object in $fixture.manifest.objects){$object.expectedHash=$null;$object.etag='a'*32};$script:etagMode='mismatch'
                    $result=Run (Freeze $fixture);Assert ($result.status -eq 'pending' -and $result.blocked -eq 6 -and @($script:calls|Where-Object {$_ -like 'delete:*'}).Count -eq 0) 'mismatched etag deleted'
                }
                'etag-missing'{
                    foreach($object in $fixture.manifest.objects){$object.expectedHash=$null;$object.etag='a'*32};$script:etagMode='missing'
                    $result=Run (Freeze $fixture);Assert ($result.status -eq 'pending' -and $result.blocked -eq 6 -and @($script:calls|Where-Object {$_ -like 'delete:*'}).Count -eq 0) 'unknown etag deleted'
                }
                'manifest-hash'{$f=Freeze $fixture;$f.manifestHash='0'*64;Reject {Run $f}}
                'foreign-key'{$fixture.manifest.objects[0].key='probe/unlocked/another/control.txt';$f=Freeze $fixture;Reject {Run $f}}
                'foreign-identity'{$fixture.manifest.repositoryId='0'*64;$f=Freeze $fixture;Reject {Run $f}}
                'rule-tuple'{$fixture.manifest.rules[2].condition.retentionSeconds=86400;$f=Freeze $fixture;Reject {Run $f}}
                'owner-receipt-hash'{$f=Freeze $fixture;$f.verificationHash='0'*64;Reject {Run $f}}
                'owner-id-substitution'{$fixture.verification.verifications[0].actualRuleId='foreign-id';$f=Freeze $fixture;Reject {Run $f}}
                'owner-tuple-substitution'{$fixture.verification.verifications[0].matchedTuple.prefix='foreign/';$f=Freeze $fixture;Reject {Run $f}}
                'owner-unknown-impact'{$fixture.verification.verifications[0].excludesUnaffected=$null;$f=Freeze $fixture;Reject {Run $f}}
                'owner-multiple-correspondence'{$fixture.verification.verifications[1].targetId='continuity-w';$f=Freeze $fixture;Reject {Run $f}}
                'source-excluded'{$fixture.manifest.localTargets[0].source=$true;$f=Freeze $fixture;Reject {Run $f}}
                'in-use-excluded'{$fixture.manifest.localTargets[0].inUse=$true;$f=Freeze $fixture;Reject {Run $f}}
                'root-path-excluded'{$fixture.manifest.localTargets[0].relativePath='';$fixture.manifest.localTargets[0].resolvedPath=$fixture.artifact;$f=Freeze $fixture;Reject {Run $f}}
                'nested-exclusion'{$fixture.manifest.excludedPaths+=@([IO.Path]::Combine($fixture.target,'item.txt'));$f=Freeze $fixture;Reject {Run $f}}
                'reparse-path'{
                    [IO.Directory]::Delete($fixture.target,$true);$outside=[IO.Path]::Combine($fixture.work,'outside');[IO.Directory]::CreateDirectory($outside)|Out-Null
                    New-Item -ItemType Junction -Path $fixture.target -Target $outside|Out-Null
                    try{$f=Freeze $fixture;Reject {Run $f}}finally{[IO.Directory]::Delete($fixture.target)}
                }
                'local-changed'{$f=Freeze $fixture;[IO.File]::WriteAllText([IO.Path]::Combine($fixture.target,'extra.txt'),'new');Reject {Run $f};Assert ($script:calls.Count -eq 0) 'network before local validation'}
                'dry-run-network-zero'{$f=Freeze $fixture;Assert ((Run $f $true).networkCalls -eq 0 -and $script:calls.Count -eq 0) 'dry run network'}
                'partial-idempotent-retry'{
                    $f=Freeze $fixture;$key=$fixture.manifest.objects[0].key;$script:failKey=$key
                    $first=Run $f;Assert ($first.status -eq 'pending' -and $first.blocked -eq 1) 'partial result'
                    $script:failKey='';$script:calls.Clear()
                    $retry=Run $f;Assert ($retry.status -eq 'passed' -and $retry.skipped -eq 6 -and @($script:calls | Where-Object {$_ -notlike ('*'+$key)}).Count -eq 0) 'retry touched successful item'
                }
                'crash-after-remote'{
                    $f=Freeze $fixture
                    & $module {$script:FaultHook={param($phase)if($phase -eq 'after-remote-delete'){$script:FaultHook=$null;throw 'crash'}}}
                    $first=Run $f;Assert ($first.status -eq 'pending') 'crash was hidden'
                    $script:calls.Clear();$retry=Run $f;Assert ($retry.status -eq 'passed' -and @($script:calls | Where-Object {$_ -like 'delete:*'}).Count -eq 0) 'already absent key deleted again'
                }
                'secret-exception-output'{$f=Freeze $fixture;$script:secretFault=$true;$result=Run $f;Assert ((ConvertTo-Json -Depth 10 $result) -notmatch 'DUMMY_SECRET') 'exception leaked secret'}
                'schema-type'{$fixture.manifest.schemaVersion='1';$f=Freeze $fixture;Reject {Run $f};Assert ($script:calls.Count -eq 0) 'string schema reached network'}
                'duplicate-json-property'{
                    $f=Freeze $fixture;$raw=[IO.File]::ReadAllText($f.manifestPath).Replace('"schemaVersion": 1','"schemaVersion": 1, "schemaVersion": 1')
                    [IO.File]::WriteAllText($f.manifestPath,$raw,[Text.UTF8Encoding]::new($false));$f.manifestHash=(Get-FileHash -LiteralPath $f.manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
                    Reject {Run $f};Assert ($script:calls.Count -eq 0) 'duplicate schema reached network'
                }
                'cutover-missing'{$f=Freeze $fixture $false;Reject {Run $f};Assert ($script:calls.Count -eq 0) 'reset before cutover'}
            }
            $executed.Add($case)
        }catch{$failed.Add($case+': '+$_.Exception.Message+' '+$_.ScriptStackTrace)}
    }
    if($ResultPath){[IO.File]::WriteAllText($ResultPath,(ConvertTo-Json -Depth 8 -InputObject ([ordered]@{registered=$cases;selected=$cases;executed=@($executed);failed=@($failed);binaries=@(Get-EvidenceTestBinaries)})),[Text.UTF8Encoding]::new($false))}
    foreach($name in $executed){"PASS $name"};foreach($name in $failed){"FAIL $name"}
    "Cases: $($cases.Count); passed: $($executed.Count); failed: $($failed.Count)"
    if($failed.Count -or $executed.Count -ne $cases.Count){exit 1}
}finally{
    & $module {$script:TestRoot=$null;$script:TransportHook=$null;$script:FaultHook=$null}
    if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)}
}
