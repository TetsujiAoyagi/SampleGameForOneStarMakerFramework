param([string] $ResultPath = '')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot '../EvidenceApplication.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../EvidenceLedger.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../ArtifactProtectionPolicy.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../EvidencePaths.psm1') -Force
$evidenceApp=Get-Module EvidenceApplication
$ledgerModule=Get-Module EvidenceLedger
$app=$evidenceApp.NestedModules|Where-Object Name -eq ArtifactApplication|Select-Object -First 1
$paths=$app.NestedModules|Where-Object Name -eq ArtifactPaths|Select-Object -First 1
$evidenceAppPaths=$evidenceApp.NestedModules|Where-Object Name -eq ArtifactPaths|Select-Object -First 1
$store=$app.NestedModules|Where-Object Name -eq CredentialStore|Select-Object -First 1
$evidencePaths=$ledgerModule.NestedModules|Where-Object Name -eq EvidencePaths|Select-Object -First 1
$ledgerPaths=$ledgerModule.NestedModules|Where-Object Name -eq ArtifactPaths|Select-Object -First 1
$protection=$app.NestedModules|Where-Object Name -eq ArtifactProtection|Select-Object -First 1
$root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-evidence-test-'+[Guid]::NewGuid().ToString('N'))
$script:objects=@{}; $script:counts=@{}; $script:deny=''; $script:badRead=$false; $script:failureMode=''
$cases=[Collections.Generic.List[string]]::new(); $failed=[Collections.Generic.List[string]]::new()
$registered=@('policy-valid','policy-overlap','policy-expired','selection-extra','publish-candidate',
    'bundle-put-once','witness-lock-wrong','control-denied','bundle-existing','bundle-put-unconfirmed',
    'bundle-readback-mismatch','ledger-commit','post-settings-change','reader-hash',
    'capture-missing-bytes','capture-size-mismatch','capture-scope-missing','copy-byte-bound',
    'intent-write-failure','interrupted-after-intent','interrupted-after-put-intent',
    'send-before-response','interrupted-before-candidate','ledger-lock-timeout','ledger-control-hash',
    'ledger-bundle-hash','ledger-stale-after','ledger-intent-identity','ledger-partialwrite',
    'ledger-rename-failure','ledger-concurrent-write','handoff-identity-substitution',
    'dummy-secret-nonexposure','dummy-secret-failure-output')
function Assert([bool]$Condition,[string]$Message){if(-not $Condition){throw $Message}}
function Json([string]$Path,$Value){[IO.File]::WriteAllText($Path,(ConvertTo-Json -InputObject $Value -Compress -Depth 16),[Text.UTF8Encoding]::new($false))}
function Hash([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Rewrite-Receipt([string]$CandidatePath,[scriptblock]$Change){
    $candidate=Get-Content $CandidatePath -Raw|ConvertFrom-Json
    $receiptPath=$candidate.ledger.protectionReceiptPath
    $receipt=Get-Content $receiptPath -Raw|ConvertFrom-Json
    & $Change $receipt
    Json $receiptPath $receipt
    $candidate.ledger.protectionReceiptSha256=Hash $receiptPath
    Json $CandidatePath $candidate
}
function Observation([string]$Operation,[string]$Generation,[int]$Status,[string]$Class,[string]$Code,[long]$Bytes=0,[string]$Sha=''){
    [pscustomobject]@{Operation=$Operation;Generation=$Generation;HttpStatus=$Status;StatusClass=$Class
        S3Code=if($Code){$Code}else{$null};Bytes=if($Operation -eq 'get'){$Bytes}else{$null}
        Sha256=if($Operation -eq 'get'){$Sha}else{$null};StatusObserved=$true;TimedOut=$false;Redirected=$false}
}
$transport={param($generation,$request)
    $key=$request.Key;$script:counts[$request.Operation+' '+$key]=1+[int]$script:counts[$request.Operation+' '+$key]
    if($script:deny -ceq $request.Operation+' '+$key){return Observation $request.Operation $generation 500 'other' ''}
    if($script:failureMode -ceq 'bundle-existing' -and $request.Operation -ceq 'get' -and $key -like 'evidence/first-use/*/bundle.zip'){
        return Observation 'get' $generation 200 'success' '' 3 ('a'*64)
    }
    if($script:failureMode -ceq 'bundle-put-unconfirmed' -and $request.Operation -ceq 'put' -and $key -like 'evidence/first-use/*/bundle.zip'){
        return Observation 'put' $generation 500 'other' ''
    }
    if($script:failureMode -ceq 'dummy-secret-failure-output' -and $request.Operation -ceq 'put' -and
        $key -like 'evidence/first-use/*/protection-witness.txt') { throw 'DUMMY_EVIDENCE_SECRET' }
    if($script:failureMode -ceq 'control-denied' -and $request.Operation -ceq 'delete' -and $key -like 'probe/unlocked/*'){
        return Observation 'delete' $generation 403 'forbidden' ''
    }
    if($request.Operation -ceq 'put'){
        if($key -like 'evidence/first-use/*/protection-witness.txt' -and $script:objects.ContainsKey($key)){
            if($script:failureMode -ceq 'witness-lock-wrong'){return Observation 'put' $generation 403 'forbidden' ''}
            return Observation 'put' $generation 403 'forbidden' 'ObjectLockedByBucketPolicy'
        }
        if($key -like 'evidence/first-use/*/bundle.zip' -and $script:objects.ContainsKey($key)){throw 'second bundle PUT'}
        $script:objects[$key]=[IO.File]::ReadAllBytes($request.SourcePath)
        if($script:failureMode -ceq 'send-before-response' -and $key -like 'evidence/first-use/*/bundle.zip'){
            throw 'Simulated lost response after one sent bundle PUT.'
        }
        return Observation 'put' $generation 200 'success' ''
    }
    if($request.Operation -ceq 'delete'){
        if($key -like 'evidence/first-use/*'){return Observation 'delete' $generation 409 'other' 'ObjectLockedByBucketPolicy'}
        $script:objects.Remove($key);return Observation 'delete' $generation 204 'success' ''
    }
    if(-not $script:objects.ContainsKey($key)){return Observation 'get' $generation 404 'not-found' 'NoSuchKey'}
    $bytes=$script:objects[$key];$sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    if($request.DestinationPath){[IO.File]::WriteAllBytes($request.DestinationPath,$bytes)}
    return Observation 'get' $generation 200 'success' '' $bytes.Length $sha
}
$readback={param($operationRoot,$config,$generation,$key,$bytes,$sha)
    $data=$script:objects[$key];$actual=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant()
    if($script:badRead -or ($script:failureMode -ceq 'bundle-readback-mismatch' -and $key -like 'evidence/first-use/*/bundle.zip')){$actual='0'*64}
    [pscustomobject]@{operation='readback';generation=$generation;key=$key;bytes=[long]$data.Length;sha256=$actual
        childExit=if($actual -ceq $sha){0}else{1};childStatus=if($actual -ceq $sha){'passed'}else{'failed'}
        httpStatus=200;statusClass='success';s3Code=$null;statusObserved=$true;timedOut=$false;redirected=$false
        observedAt=[DateTimeOffset]::UtcNow.ToString('o')}
}
function Fixture([string]$Name){
    $dir=[IO.Path]::Combine($root,$Name);[IO.Directory]::CreateDirectory($dir)|Out-Null
    $source=[IO.Path]::Combine($dir,'source');[IO.Directory]::CreateDirectory($source)|Out-Null
    $endpoint='https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'
    $when=[DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('o')
    $settings=[IO.Path]::Combine($source,'settings-before.json')
    $state=[ordered]@{schemaVersion=1;endpoint=$endpoint;bucket='osm-artifacts';publicDevelopmentUrlEnabled=$false
        customDomainCount=0;writerCanConfigure=$false;bucketScopedWriter=$true;writerPermission='Object Read & Write'
        lockRules=@([ordered]@{prefix='probe/locked/';enabled=$true;mode='age';retentionSeconds=86400},
            [ordered]@{prefix='evidence/first-use/';enabled=$true;mode='age';retentionSeconds=2592000})
        lifecycleRules=@([ordered]@{type='abort-incomplete-multipart';prefix='';enabled=$true;days=7})
        observedAt=$when;observer='offline-test'}
    Json $settings $state
    $configPath=[IO.Path]::Combine($dir,'config.json')
    $head=(& git rev-parse HEAD).Trim()
    Json $configPath ([ordered]@{schemaVersion=1;profile='osm';purpose='evidence';endpoint=$endpoint
        bucket='osm-artifacts';repositoryId='4d2d1728f1db77f50605a10da0bbf10fd31443c381c4f312658134fffb850b3b'
        prefix='evidence/first-use/';retentionSeconds=2592000;observedAt=$when
        validUntil=[DateTimeOffset]::UtcNow.AddHours(1).ToString('o');settingsEvidencePath=$settings
        settingsEvidenceSha256=(Hash $settings)})
    foreach($name in @('settings-overview.jpg','settings-rules.jpg')){[IO.File]::WriteAllBytes([IO.Path]::Combine($source,$name),[byte[]]@(255,216,255,217))}
    Json ([IO.Path]::Combine($source,'capture.json')) ([ordered]@{schemaVersion=1;implementationHead=$head;bucket='osm-artifacts'
        page='settings';capturedAt=$when;observer='offline-test';tool='dummy'
        overviewBytes=4;overviewSha256=(Hash ([IO.Path]::Combine($source,'settings-overview.jpg')))
        rulesBytes=4;rulesSha256=(Hash ([IO.Path]::Combine($source,'settings-rules.jpg')))
        observationScope=[ordered]@{overview='bucket/private URL/domain visible in dummy image';rules='lock prefix, age and lifecycle visible in dummy image'}})
    $files=@(foreach($name in @('settings-before.json','settings-overview.jpg','settings-rules.jpg','capture.json')){
        $path=[IO.Path]::Combine($source,$name)
        [ordered]@{source=$name;path=$name;bytes=([IO.FileInfo]::new($path)).Length;sha256=(Hash $path)}
    })
    $selectionPath=[IO.Path]::Combine($dir,'selection.json')
    Json $selectionPath ([ordered]@{schemaVersion=1;kind='E2';root=$source
        producerBase='6d804ca637cf42fb876e602c8ccc0656cfbd255d';producerHead=$head
        sourceManifestSha256=$null;files=$files})
    return @{Config=$configPath;Selection=$selectionPath;Source=$source;Settings=$settings;Head=$head;State=$state}
}
try{
    & $paths {param($p)$script:TestRoot=$p} ([IO.Path]::Combine($root,'transfers'))
    & $evidenceAppPaths {param($p)$script:TestRoot=$p} ([IO.Path]::Combine($root,'transfers'))
    & $ledgerPaths {param($p)$script:TestRoot=$p} ([IO.Path]::Combine($root,'transfers'))
    & $store {param($p)$script:TestRoot=$p;$script:TestBase=$null} ([IO.Path]::Combine($root,'credentials'))
    & $evidencePaths {param($p)$script:TestRoot=$p} ([IO.Path]::Combine($root,'records'))
    & $app {param($t,$r)$script:TransportHook=$t;$script:ReadbackHook=$r} $transport $readback
    & $protection {$script:FaultHook=$null}
    $null=& $store {Write-CredentialRecord 'osm' 'DUMMY_EVIDENCE_ID' 'DUMMY_EVIDENCE_SECRET' $false}
    foreach($case in $registered){
        $script:objects=@{};$script:counts=@{};$script:deny='';$script:badRead=$false;$script:failureMode='';$stage=$null
        & $protection {$script:FaultHook=$null}
        & $evidencePaths {$script:WriteHook=$null}
        try{
            $f=Fixture $case
            switch($case){
                'policy-valid'{ $v=Read-EvidenceConfig $f.Config;Assert ($v.Data.purpose -ceq 'evidence') 'policy rejected valid config' }
                'policy-overlap'{
                    $f.State.lockRules+=@([ordered]@{prefix='evidence/first-use/extra/';enabled=$true;mode='date';retentionSeconds=$null})
                    Json $f.Settings $f.State
                    $cfg=(Get-Content $f.Config -Raw|ConvertFrom-Json);$cfg.settingsEvidenceSha256=Hash $f.Settings;Json $f.Config $cfg
                    $rejected=$false;try{$null=Read-EvidenceConfig $f.Config}catch{$rejected=$true};Assert $rejected 'overlap accepted'
                }
                'policy-expired'{
                    $cfg=Get-Content $f.Config -Raw|ConvertFrom-Json
                    $cfg.validUntil=[DateTimeOffset]::UtcNow.AddSeconds(-1).ToString('o');Json $f.Config $cfg
                    $rejected=$false;try{$null=Read-EvidenceConfig $f.Config}catch{$rejected=$true};Assert $rejected 'expired config accepted'
                }
                'selection-extra'{
                    $s=Get-Content $f.Selection -Raw|ConvertFrom-Json
                    $s.files+=@([ordered]@{source='extra.txt';path='extra.txt';bytes=1;sha256=('a'*64)})
                    Json $f.Selection $s
                    $v=Invoke-EvidencePublish $f.Config $f.Selection '6d804ca637cf42fb876e602c8ccc0656cfbd255d' $f.Head
                    Assert ($v.status -ceq 'failed' -and $script:counts.Count -eq 0) 'extra selection reached network'
                }
                'capture-missing-bytes'{
                    $path=[IO.Path]::Combine($f.Source,'capture.json');$capture=Get-Content $path -Raw|ConvertFrom-Json
                    $capture.PSObject.Properties.Remove('overviewBytes');Json $path $capture
                    $v=Invoke-EvidencePublish $f.Config $f.Selection '6d804ca637cf42fb876e602c8ccc0656cfbd255d' $f.Head
                    Assert ($v.status -ceq 'failed' -and $script:counts.Count -eq 0) 'capture without byte count accepted'
                }
                'capture-size-mismatch'{
                    $path=[IO.Path]::Combine($f.Source,'capture.json');$capture=Get-Content $path -Raw|ConvertFrom-Json
                    $capture.overviewBytes=5;Json $path $capture
                    $v=Invoke-EvidencePublish $f.Config $f.Selection '6d804ca637cf42fb876e602c8ccc0656cfbd255d' $f.Head
                    Assert ($v.status -ceq 'failed' -and $script:counts.Count -eq 0) 'capture mismatched byte count accepted'
                }
                'capture-scope-missing'{
                    $path=[IO.Path]::Combine($f.Source,'capture.json');$capture=Get-Content $path -Raw|ConvertFrom-Json
                    $capture.PSObject.Properties.Remove('observationScope');Json $path $capture
                    $v=Invoke-EvidencePublish $f.Config $f.Selection '6d804ca637cf42fb876e602c8ccc0656cfbd255d' $f.Head
                    Assert ($v.status -ceq 'failed' -and $script:counts.Count -eq 0) 'capture without scope accepted'
                }
                'copy-byte-bound'{
                    $source=[IO.Path]::Combine($f.Source,'oversized.txt');[IO.File]::WriteAllBytes($source,[byte[]]::new(2048))
                    $target=[IO.Path]::Combine([IO.Path]::GetDirectoryName($f.Config),'bounded-copy.txt')
                    $timer=[Diagnostics.Stopwatch]::StartNew();$rejected=$false
                    try{$null=& $evidenceApp {param($a,$b,$c,$d,$e,$f) Copy-EvidenceChecked $a $b $c $d $e $f} `
                        $f.Source 'oversized.txt' $target 1 ('0'*64) $timer}catch{$rejected=$true}
                    Assert ($rejected -and -not [IO.File]::Exists($target) -and $timer.ElapsedMilliseconds -lt 600000) 'source overrun copied before rejection'
                }
                'ledger-partialwrite'{
                    $id=[Guid]::NewGuid().ToString('N')
                    $hook={param($stage,$pending,$final)if($stage -ceq 'after-pending'){[IO.File]::WriteAllBytes($pending,[byte[]]@(1,2,3));throw 'dummy partial write'}}
                    & $evidencePaths {param($h)$script:WriteHook=$h} $hook
                    $rejected=$false;try{$null=& $evidencePaths {param($v) Write-EvidenceImmutable 'evidence-ledgers' $v 'ledger.json' @{schemaVersion=1}} $id}catch{$rejected=$true}
                    $dir=[IO.Path]::Combine($root,'records','evidence-ledgers',$id)
                    Assert ($rejected -and -not [IO.File]::Exists([IO.Path]::Combine($dir,'ledger.json')) -and
                        [IO.File]::Exists([IO.Path]::Combine($dir,'ledger.json.pending'))) 'partial ledger became committed'
                }
                'ledger-rename-failure'{
                    $id=[Guid]::NewGuid().ToString('N')
                    $hook={param($stage,$pending,$final)if($stage -ceq 'before-rename'){throw 'dummy rename failure'}}
                    & $evidencePaths {param($h)$script:WriteHook=$h} $hook
                    $rejected=$false;try{$null=& $evidencePaths {param($v) Write-EvidenceImmutable 'evidence-ledgers' $v 'ledger.json' @{schemaVersion=1}} $id}catch{$rejected=$true}
                    $dir=[IO.Path]::Combine($root,'records','evidence-ledgers',$id)
                    Assert ($rejected -and -not [IO.File]::Exists([IO.Path]::Combine($dir,'ledger.json')) -and
                        [IO.File]::Exists([IO.Path]::Combine($dir,'ledger.json.pending'))) 'failed rename committed ledger'
                }
                'ledger-concurrent-write'{
                    $id=[Guid]::NewGuid().ToString('N');$recordRoot=[IO.Path]::Combine($root,'records')
                    $modulePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../EvidencePaths.psm1'))
                    $outcomes=@(1..2|ForEach-Object -Parallel {
                        $m=Import-Module $using:modulePath -Force -PassThru
                        & $m {param($p)$script:TestRoot=$p} $using:recordRoot
                        try{$null=Write-EvidenceImmutable 'evidence-ledgers' $using:id 'ledger.json' @{schemaVersion=1};'committed'}
                        catch{'rejected'}
                    } -ThrottleLimit 2)
                    Assert (@($outcomes|Where-Object {$_ -ceq 'committed'}).Count -eq 1 -and
                        @($outcomes|Where-Object {$_ -ceq 'rejected'}).Count -eq 1) 'concurrent ledger writers both committed or both failed'
                }
                default{
                    if($case -cin @('intent-write-failure','interrupted-after-intent','interrupted-after-put-intent','interrupted-before-candidate')){
                        $stage=switch($case){
                            'intent-write-failure'{'before-intent'}
                            'interrupted-after-intent'{'after-intent'}
                            'interrupted-after-put-intent'{'after-put-intent'}
                            'interrupted-before-candidate'{'before-candidate'}
                        }
                        $fault={param($seen,$opRoot)
                            if($seen -ceq $stage){
                                if($seen -ceq 'before-intent'){[IO.Directory]::CreateDirectory([IO.Path]::Combine($opRoot,'intent.json'))|Out-Null}
                                else{throw 'dummy interruption'}
                            }
                        }.GetNewClosure()
                        & $protection {param($h)$script:FaultHook=$h} $fault
                    }
                    if($case -cin @('witness-lock-wrong','control-denied','bundle-existing','bundle-put-unconfirmed','bundle-readback-mismatch')){
                        $script:failureMode=$case
                    }
                    if($case -cin @('send-before-response','dummy-secret-failure-output')){$script:failureMode=$case}
                    if($case -ceq 'dummy-secret-failure-output'){
                        $captured=@(Invoke-EvidencePublish $f.Config $f.Selection '6d804ca637cf42fb876e602c8ccc0656cfbd255d' $f.Head 2>&1)
                        $v=$captured|Where-Object { $null -ne $_.PSObject.Properties['operationId'] }|Select-Object -Last 1
                        Assert (-not (($captured|Out-String).Contains('DUMMY_EVIDENCE_SECRET'))) 'dummy secret exposed in stdout/stderr'
                    }else{
                        $v=Invoke-EvidencePublish $f.Config $f.Selection '6d804ca637cf42fb876e602c8ccc0656cfbd255d' $f.Head
                    }
                    if($script:failureMode -or $stage){
                        Assert ($v.status -ceq 'failed' -and $null -eq $v.ledger) 'failure produced candidate'
                        $bundle=@($script:counts.Keys|Where-Object {$_ -like 'put evidence/first-use/*/bundle.zip'})
                        if($case -cin @('witness-lock-wrong','control-denied','bundle-existing','intent-write-failure',
                                'interrupted-after-intent','interrupted-after-put-intent','dummy-secret-failure-output')){Assert ($bundle.Count -eq 0) 'bundle PUT reached early failure'}
                        else{Assert ($bundle.Count -eq 1) 'bundle PUT did not occur exactly once'}
                        $opRoot=$v.residue.localPath
                        if($case -ceq 'intent-write-failure'){Assert ($script:counts.Count -eq 0) 'intent failure reached network'}
                        if($case -ceq 'dummy-secret-failure-output'){
                            $all=ConvertTo-Json -InputObject $v -Compress -Depth 20
                            $all+=[IO.File]::ReadAllText([IO.Path]::Combine($opRoot,'result.json'))
                            $all+=[IO.File]::ReadAllText([IO.Path]::Combine($opRoot,'protection-failure.json'))
                            Assert (-not $all.Contains('DUMMY_EVIDENCE_SECRET')) 'dummy secret exposed in local result'
                        }
                        if($case -ceq 'interrupted-after-intent'){Assert ($script:counts.Count -eq 0 -and
                            [IO.File]::Exists([IO.Path]::Combine($opRoot,'intent.json'))) 'post-intent interruption boundary lost'}
                        if($case -cin @('interrupted-after-put-intent','send-before-response','interrupted-before-candidate')){
                            Assert ([IO.File]::Exists([IO.Path]::Combine($opRoot,'put-intent.json')) -and
                                $v.residue.remote -ceq 'may-have-been-sent') 'PUT-intent residue missing'
                        }
                        Assert (@($script:counts.Keys|Where-Object {$_ -like 'delete evidence/first-use/*/bundle.zip'}).Count -eq 0) 'bundle DELETE attempted'
                        break
                    }
                    Assert ($v.status -ceq 'passed') "publish failed: $($v.reasonCode)"
                    $key=$v.ledger.key
                    Assert ($script:counts['put '+$key] -eq 1 -and -not $script:counts.ContainsKey('delete '+$key)) 'bundle destructive operation'
                    if($case -cin @('ledger-commit','post-settings-change','reader-hash','handoff-identity-substitution',
                            'ledger-lock-timeout','ledger-control-hash','ledger-bundle-hash','ledger-stale-after','ledger-intent-identity')){
                        $after=[IO.Path]::Combine([IO.Path]::GetDirectoryName($f.Config),'settings-after.json')
                        $f.State.observedAt=[DateTimeOffset]::UtcNow.ToString('o')
                        if($case -ceq 'post-settings-change'){$f.State.lockRules[1].retentionSeconds=86400}
                        if($case -ceq 'ledger-stale-after'){$f.State.observedAt='2020-01-01T00:00:00.0000000+00:00'}
                        Json $after $f.State
                        $candidate=[IO.Path]::Combine($v.residue.localPath,'result.json')
                        switch($case){
                            'ledger-lock-timeout'{Rewrite-Receipt $candidate {param($r)$r.observations[2].timedOut=$true}}
                            'ledger-control-hash'{Rewrite-Receipt $candidate {param($r)$r.observations[6].sha256='0'*64}}
                            'ledger-bundle-hash'{Rewrite-Receipt $candidate {param($r)$r.observations[11].sha256='0'*64}}
                            'ledger-intent-identity'{
                                $c=Get-Content $candidate -Raw|ConvertFrom-Json
                                $intent=Get-Content $c.ledger.intentPath -Raw|ConvertFrom-Json
                                $intent.controlKey='probe/unlocked/'+$v.operationId+'/substituted.txt'
                                Json $c.ledger.intentPath $intent;$c.ledger.intentSha256=Hash $c.ledger.intentPath
                                $pi=Get-Content $c.ledger.putIntentPath -Raw|ConvertFrom-Json
                                $pi.intentSha256=$c.ledger.intentSha256;Json $c.ledger.putIntentPath $pi
                                $c.ledger.putIntentSha256=Hash $c.ledger.putIntentPath
                                $receipt=Get-Content $c.ledger.protectionReceiptPath -Raw|ConvertFrom-Json
                                $receipt.intentSha256=$c.ledger.intentSha256;$receipt.putIntentSha256=$c.ledger.putIntentSha256
                                $receipt.controlKey=$intent.controlKey
                                foreach($i in @(5,6,7,8)){$receipt.observations[$i].key=$intent.controlKey}
                                Json $c.ledger.protectionReceiptPath $receipt
                                $c.ledger.protectionReceiptSha256=Hash $c.ledger.protectionReceiptPath
                                Json $candidate $c
                            }
                        }
                        $commit=Invoke-EvidenceCommit $candidate (Hash $candidate) $after (Hash $after)
                        if($case -cin @('post-settings-change','ledger-lock-timeout','ledger-control-hash',
                                'ledger-bundle-hash','ledger-stale-after','ledger-intent-identity')){
                            Assert ($commit.status -ceq 'failed') 'contradictory or stale evidence committed'
                        }
                        else{Assert ($commit.status -ceq 'passed') 'valid candidate refused'}
                        if($case -cin @('reader-hash','handoff-identity-substitution')){
                            $fixed=& $evidenceApp { ,$script:E1Fixed }
                            $e1Root=& $evidenceApp { $script:E1Root }
                            $e1Files=@(foreach($row in $fixed){[ordered]@{source=$row[0];path=('source/'+$row[0]);bytes=[long]$row[1];sha256=$row[2]}})
                            $e1Selection=[IO.Path]::Combine([IO.Path]::GetDirectoryName($f.Config),'e1-selection.json')
                            Json $e1Selection ([ordered]@{schemaVersion=1;kind='E1';root=$e1Root
                                producerBase='6d804ca637cf42fb876e602c8ccc0656cfbd255d';producerHead=$f.Head
                                sourceManifestSha256='b6e3dbf63c1bf925c36c966e64022bb6d1b14897f04aeb1af7af4082a2693ee4';files=$e1Files})
                            $e1=Invoke-EvidencePublish $f.Config $e1Selection 'fd7ebf932d230a522293dee72572cbdeeac1c8fa' 'caed8bae55c033673b75532dc650f0a3a3cedc48'
                            Assert ($e1.status -ceq 'passed') 'E1 fake publish failed'
                            $e1After=[IO.Path]::Combine([IO.Path]::GetDirectoryName($f.Config),'e1-settings-after.json')
                            $f.State.observedAt=[DateTimeOffset]::UtcNow.ToString('o');Json $e1After $f.State
                            $e1Candidate=[IO.Path]::Combine($e1.residue.localPath,'result.json')
                            $e1Ledger=Invoke-EvidenceCommit $e1Candidate (Hash $e1Candidate) $e1After (Hash $e1After)
                            Assert ($e1Ledger.status -ceq 'passed') 'E1 fake ledger failed'
                            $handoff=Write-EvidenceReaderInput $e1Ledger.ledgerPath $e1Ledger.ledgerSha256 `
                                $commit.ledgerPath $commit.ledgerSha256 $f.Config (Hash $f.Config)
                            $read=Read-EvidenceReaderInput $handoff.Path $handoff.Sha256
                            Assert ($read.items.Count -eq 2) 'reader handoff mismatch'
                            $reject=$false;try{$null=Read-EvidenceReaderInput $handoff.Path ('0'*64)}catch{$reject=$true}
                            Assert $reject 'reader accepted wrong prompt hash'
                            if($case -ceq 'handoff-identity-substitution'){
                                $substituted=[IO.Path]::Combine([IO.Path]::GetDirectoryName($f.Config),'substituted-reader-input.json')
                                $guide=Get-Content $handoff.Path -Raw|ConvertFrom-Json
                                $guide.items[0].packageSha256='0'*64;Json $substituted $guide
                                $reject=$false;try{$null=Read-EvidenceReaderInput $substituted (Hash $substituted)}catch{$reject=$true}
                                Assert $reject 'trusted but identity-substituted handoff accepted'
                            }
                            $closeEvent=[IO.Path]::Combine([IO.Path]::GetDirectoryName($f.Config),'close-event.json')
                            Json $closeEvent ([ordered]@{schemaVersion=1;owner='OSM maintainer'
                                ledgerIds=@($e1Ledger.ledgerId,$commit.ledgerId)
                                ledgerSha256=@($e1Ledger.ledgerSha256,$commit.ledgerSha256)
                                closedAt=[DateTimeOffset]::UtcNow.AddSeconds(-1).ToString('o')
                                consumerReferenceState='test-unreferenced';ownerInstructionReference='dummy-test-only'})
                            $close=Write-EvidenceCloseRecord $e1Ledger.ledgerPath $e1Ledger.ledgerSha256 `
                                $commit.ledgerPath $commit.ledgerSha256 $closeEvent (Hash $closeEvent)
                            Assert ([IO.File]::Exists($close.Path)) 'close record missing'
                            $reject=$false
                            try{$null=Write-EvidenceCloseRecord $e1Ledger.ledgerPath $e1Ledger.ledgerSha256 `
                                $commit.ledgerPath $commit.ledgerSha256 $closeEvent (Hash $closeEvent)}catch{$reject=$true}
                            Assert $reject 'duplicate close accepted'
                        }
                    }
                    if($case -ceq 'dummy-secret-nonexposure'){
                        $resultText=ConvertTo-Json -InputObject $v -Compress -Depth 20
                        $operationResultPath=[IO.Path]::Combine($v.residue.localPath,'result.json')
                        $receiptText=[IO.File]::ReadAllText($v.ledger.protectionReceiptPath)
                        $all=$resultText+[IO.File]::ReadAllText($operationResultPath)+$receiptText
                        Assert (-not $all.Contains('DUMMY_EVIDENCE_SECRET') -and -not $all.Contains('DUMMY_EVIDENCE_ID')) 'dummy secret exposed'
                    }
                }
            }
            $cases.Add($case);Write-Output "PASS $case"
        }catch{$failed.Add($case);Write-Output "FAIL $case $($_.Exception.Message)"}
    }
}finally{
    & $app {$script:TransportHook=$null;$script:ReadbackHook=$null}
    & $store {$script:TestRoot=$null;$script:TestBase=$null}
    & $paths {$script:TestRoot=$null}
    & $evidenceAppPaths {$script:TestRoot=$null}
    & $ledgerPaths {$script:TestRoot=$null}
    & $evidencePaths {$script:TestRoot=$null}
    $tmp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    $target=[IO.Path]::GetFullPath($root)
    if($target.StartsWith($tmp,[StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($target) -match '^osm-evidence-test-[0-9a-f]{32}$' -and
        [IO.Directory]::Exists($target)){[IO.Directory]::Delete($target,$true)}
}
if($ResultPath){
    $raw=[ordered]@{registered=$registered;selected=$registered;executed=@($cases.ToArray());failed=@($failed.ToArray())}
    [IO.File]::WriteAllText($ResultPath,(ConvertTo-Json -InputObject $raw -Depth 8),[Text.UTF8Encoding]::new($false))
}
Write-Output "Cases: $($cases.Count+$failed.Count); passed: $($cases.Count); failed: $($failed.Count)"
if($failed.Count){exit 1}
