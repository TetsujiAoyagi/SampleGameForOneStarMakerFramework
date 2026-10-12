param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
foreach($name in @('BuildCleanup','ContentCleanup','BuildApplication','ContentApplication','SeriesRetirementStore','BuildRetentionPolicy','BuildStore','BuildContract','ContentContract','ContentStore','ContentDeletionStore','ArtifactApplication','ArtifactAcl','ArtifactPaths','Transport/R2ArtifactTransport')){Import-Module (Join-Path $PSScriptRoot "../$name.psm1")}
. (Join-Path $PSScriptRoot 'ContentTestSupport.ps1')
# 系列の退役（退役記録・退役の選別・build／content retire・退役後のpublish／collect／use／cleanup・照会）をofflineで検証する。
# 成功記録は非秘密の合成receipt（Contentはcollectと同じ関数）で作り、R2はremote directory、時計・通信・予算・上限は注入する。
# builds-v1・contents-v1・retirements-v1・ArtifactsRootは全てfixture内の注入rootで、実storeに触れない。
# 並行は別processをsignalで同期し、時間待機を使わない。実R2での実証（AC15）は判定Cの別証拠として扱う。
$registered=@('record-store','record-race','policy-table','dry-run-no-writes','build-retire-complete','record-before-delete','build-resume',
    'build-after-retire','build-collect-retired','build-boundary','build-lock','build-publish-race',
    'content-retire','content-resume','content-after-retire','content-boundary','cli-grammar')
$selected=@(if($Case -ceq '*'){$registered}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
$repo='a'*64;$proj='samplegame';$tgt='windows-x64-player';$set='bs4-spring-full'
$d1='chat:20261012T1230Z:series-retirement:retired';$d2='github:TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/1/retire'
$baseNow=[DateTimeOffset]::new(2026,10,12,0,0,0,[TimeSpan]::Zero)
$savedRoot=[Environment]::GetEnvironmentVariable('OSM_ARTIFACTS_ROOT')
function Assert([bool]$Condition,[string]$Message){Assert-EvidenceTest $Condition $Message}
function global:New-Response([string]$Op,[string]$Gen,$Http,[string]$Class,$S3=$null){
    [pscustomobject]@{Operation=$Op;Generation=$Gen;HttpStatus=$Http;StatusClass=$Class;S3Code=$S3;Bytes=$null;Sha256=$null;StatusObserved=$true;TimedOut=$false;Redirected=$false}
}
function New-RetireFixture {
    $root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-retire-test-'+[Guid]::NewGuid().ToString('N'));[IO.Directory]::CreateDirectory($root)|Out-Null
    $o=[ordered]@{};foreach($n in @('transfers','builds-v1','deployments','workflow','evidence','credentials','contents-v1','retirements-v1')){$o[$n]=[IO.Path]::Combine($root,$n)}
    Set-EvidenceTestValue ArtifactPaths TestRoot $o.transfers;Set-EvidenceTestValue BuildStore TestRoot $o['builds-v1'];Set-EvidenceTestValue ContentStore TestRoot $o['contents-v1']
    Set-EvidenceTestValue SeriesRetirementStore TestRoot $o['retirements-v1'];Set-EvidenceTestValue EvidenceContract DeploymentRoot $o.deployments
    Set-EvidenceTestValue BuildContract OwnedRootsForTest @($o.Values)
    [IO.Directory]::CreateDirectory($o.deployments)|Out-Null
    $ev=[ordered]@{schemaVersion=2;profile='osm';endpoint=('https://'+('e'*32)+'.r2.cloudflarestorage.com');bucket='osm-artifacts';repositoryId=$repo;prefix='development/evidence/v2/';policy='task-end-30d-v1';deploymentId=('d'*32)}
    $evPath=[IO.Path]::Combine($o.deployments,('d'*32)+'.config.json');Write-EvidenceTestJson $evPath $ev;$evHash=(Get-FileHash -LiteralPath $evPath).Hash.ToLowerInvariant()
    Write-EvidenceTestJson ([IO.Path]::Combine($o.deployments,('d'*32)+'.json')) ([ordered]@{schemaVersion=2;deploymentId=('d'*32);repositoryId=$repo;configSha256=$evHash;baselineRecordId='fixture';baselineSha256=('b'*64);ownerObservationId='fixture';ownerObservationSha256=('c'*64)})
    $cfgDir=New-ArtifactOperation
    $buildConfig=[IO.Path]::Combine($cfgDir,'build-config.json');$contentConfig=[IO.Path]::Combine($cfgDir,'content-config.json')
    Write-PrivateJson $buildConfig ([ordered]@{schemaVersion=1;purpose='build';profile='osm';evidenceConfigPath=$evPath;evidenceConfigSha256=$evHash;repositoryId=$repo;prefix='development/builds/v1/';policy='build-publish-v1'})
    Write-PrivateJson $contentConfig ([ordered]@{schemaVersion=1;purpose='content';profile='osm';evidenceConfigPath=$evPath;evidenceConfigSha256=$evHash;repositoryId=$repo;prefix='development/content/v1/';policy='content-publish-v1'})
    $f=[pscustomobject]@{Root=$root;Repo=[IO.Path]::Combine($root,'repo');O=$o;CfgDir=$cfgDir;BuildConfig=$buildConfig;ContentConfig=$contentConfig;ArtifactsRoot=[IO.Path]::Combine($root,'artifacts-root');Remote=[IO.Path]::Combine($root,'remote');Source=[IO.Path]::Combine($root,'source')
        Now=$baseNow;Hook=$null;Log=[Collections.Generic.List[string]]::new();BuildData=$null;ContentData=$null}
    foreach($d in @($f.ArtifactsRoot,$f.Remote,$f.Source)){[IO.Directory]::CreateDirectory($d)|Out-Null}
    [IO.File]::WriteAllText([IO.Path]::Combine($f.Source,'sample.bin'),'safe-build-payload')
    # 全経路が同じfile-backed remoteを使う。実transportと同じ入口判定を通し、印の無いDELETEや他種別のkeyを拒否する。
    $network={param($gen,$request)
        $f.Log.Add($request.Operation+' '+$request.Key)
        if(-not (Test-R2ArtifactRequestAllowed $request)){throw 'transport-rejected'}
        $path=Get-RemotePath $f.Remote $request.Key
        if($f.Hook){$custom=& $f.Hook $gen $request $path;if($null -ne $custom){return $custom}}
        $status=200;$class='success';$code=$null;$bytes=$null;$sha=$null
        if($request.Operation -ceq 'delete'){[IO.File]::Delete($path);$status=204}
        elseif($request.Operation -ceq 'get'){
            if([IO.File]::Exists($path)){$data=[IO.File]::ReadAllBytes($path);if($request.DestinationPath){[IO.File]::WriteAllBytes($request.DestinationPath,$data)};$bytes=[long]$data.Length;$sha=Get-Sha $data}
            else{$status=404;$class='not-found';$code='NoSuchKey'}
        }else{[IO.File]::WriteAllBytes($path,[IO.File]::ReadAllBytes($request.SourcePath))}
        [pscustomobject]@{Operation=$request.Operation;Generation=$gen;HttpStatus=$status;StatusClass=$class;S3Code=$code;Bytes=$bytes;Sha256=$sha;StatusObserved=$true;TimedOut=$false;Redirected=$false}
    }.GetNewClosure()
    $readback={param($op,$cfg,$gen,$key,$bytes,$hash)
        $path=Get-RemotePath $f.Remote $key;Get-ReadResult $gen $key $(if([IO.File]::Exists($path)){[IO.File]::ReadAllBytes($path)}else{$null}) $bytes $hash
    }.GetNewClosure()
    $clock={$f.Now}.GetNewClosure()
    foreach($m in @('BuildCleanup','BuildApplication','ContentCleanup')){Set-EvidenceTestValue $m NetworkHook $network}
    foreach($m in @('BuildCleanup','BuildApplication','ContentCleanup','ContentApplication')){Set-EvidenceTestValue $m Clock $clock}
    Set-EvidenceTestValue BuildApplication ReadbackHook $readback
    Set-EvidenceTestValue ArtifactApplication TransportHook $network;Set-EvidenceTestValue ArtifactApplication ReadbackHook $readback
    Set-EvidenceTestValue ContentTransfer Clock ({[long]0})
    foreach($name in @('BuildCleanup','BuildApplication','ContentCleanup','ContentTransfer')){foreach($m in @(Get-EvidenceTestModules $name)){& $m {function script:Get-CredentialStatus {param($profile)[pscustomobject]@{Generation=('f'*32)}}}}}
    Import-ArtifactPackage
    $f.BuildData=Read-BuildConfig $buildConfig;$f.ContentData=Read-ContentConfig $contentConfig
    return $f
}
# --- Build ---
function Get-BuildKey2([string]$Id,[string]$Configuration='il2cpp-high'){"development/builds/v1/$repo/$proj/$tgt/$Configuration/$Id/bundle.zip"}
# 成功記録（intent+receipt）を合成する（Read-BuildReceiptの全検査を通る形）。R2側のobjectも置く。
function New-Build($F,[DateTimeOffset]$At,[string]$Configuration='il2cpp-high',[switch]$IntentOnly){
    $id=[Guid]::NewGuid().ToString('N');$dir=Get-BuildDirectory $repo $id $true;$key=Get-BuildKey2 $id $Configuration
    $payload=[Text.Encoding]::UTF8.GetBytes('payload-'+$id);[IO.File]::WriteAllBytes((Get-RemotePath $F.Remote $key),$payload)
    $intent=[ordered]@{schemaVersion=1;purpose='build';buildId=$id;repositoryId=$repo;project=$proj;target=$tgt;configuration=$Configuration;seriesId="$repo/$proj/$tgt/$Configuration";key=$key;configSha256=$F.BuildData.Hash;evidenceConfigSha256=$F.BuildData.EvidenceConfigSha256;packageBytes=[long]$payload.Length;packageSha256=(Get-Sha $payload);manifestSha256=('2'*64);entries=@([pscustomobject]@{path='sample.bin';bytes=[long]5;sha256=('1'*64)});operationPath=('C:\fixture\'+('3'*32))}
    $written=Write-BuildImmutable $dir 'intent.json' $intent
    if($IntentOnly){return $id}
    $receipt=[ordered]@{};foreach($k in $intent.Keys){if($k -cne 'operationPath'){$receipt[$k]=$intent[$k]}}
    $receipt.intentSha256=$written.Sha256
    $receipt.readback=[ordered]@{operation='readback';generation=('f'*32);key=$key;bytes=[long]$payload.Length;sha256=(Get-Sha $payload);childExit=0;httpStatus=200;statusClass='success';s3Code=$null;statusObserved=$true;timedOut=$false;redirected=$false;childStatus='passed';observedAt=$At.ToString('o')}
    $receipt.publishState='succeeded';$receipt.publishedAt=$At.ToString('o')
    $null=Write-BuildImmutable $dir 'receipt.json' $receipt
    return $id
}
# 新しい順にcount件。最初が最新、最後が最古。
function New-Builds($F,[int]$Count,[string]$Configuration='il2cpp-high'){@(for($i=0;$i -lt $Count;$i++){New-Build $F ($F.Now.AddMinutes(-(1+$i))) $Configuration})}
function Get-BuildDir2([string]$Id){[IO.Path]::Combine((Get-BuildRoot),$repo,$Id)}
function Add-Use([string]$Id,[DateTimeOffset]$Created,[DateTimeOffset]$Until){
    $p=[Guid]::NewGuid().ToString('N')
    $null=Write-BuildImmutable (Get-BuildDir2 $Id) "protections/$p.use.json" ([ordered]@{schemaVersion=1;buildId=$Id;id=$p;owner=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value;consumer='fixture-user';reason='fixture';createdAt=$Created.ToString('o');until=$Until.ToString('o')})
}
# 実publish（zipを作る）。selectionはbuild-configと同じoperationに置く（publishの要求）。
function Write-Selection($F,[string]$Configuration='il2cpp-high'){
    $path=[IO.Path]::Combine($F.CfgDir,'build-selection-'+[Guid]::NewGuid().ToString('N')+'.json')
    Write-PrivateJson $path ([ordered]@{schemaVersion=1;purpose='build';root=$F.Source;files=@('sample.bin');project=$proj;target=$tgt;configuration=$Configuration;buildId=[Guid]::NewGuid().ToString('N')})
    return $path
}
function Publish-Build($F){$r=Invoke-BuildPublish $F.BuildConfig (Write-Selection $F);Assert ($r.status -ceq 'passed') "publish: $($r.reasonCode)";$F.Now=$F.Now.AddMinutes(1);return $r}
# build collect用の偽Player（BuildCollect suiteと同じ形）: Player本体・content/（manifestと一致）・build-receipt.json。
function New-CollectPlayer($F,[string]$Name){
    $player=[IO.Path]::Combine($F.Repo,'artifacts','bs4','players',$Name);$transport=[IO.Path]::Combine($F.Repo,'artifacts','bs4','transport',$Name)
    Write-Bytes ([IO.Path]::Combine($player,'SampleGame.exe')) ([Text.Encoding]::UTF8.GetBytes('fixture-player'))
    Write-Bytes ([IO.Path]::Combine($player,'SampleGame_Data','StreamingAssets','bs4-runtime.json')) ([Text.Encoding]::UTF8.GetBytes('{"content":{"relativeDirectory":"content"}}'))
    $b=[Text.Encoding]::UTF8.GetBytes("content-$Name");Write-Bytes ([IO.Path]::Combine($player,'content','a.cf')) $b;Write-Bytes ([IO.Path]::Combine($transport,'content','a.cf')) $b
    $manifest=[ordered]@{version=1;product='OneStarMaker';contentSet=$set;revision=$Name;target='StandaloneWindows64-Player';unityVersion='6000.6.0f1';rootSchemaVersion=2;playerConfigSchemaVersion=1;files=@([ordered]@{path='a.cf';size=$b.Length;sha256=(Get-Sha $b)});sourceFiles=@()}
    $mbytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $manifest -Depth 8));Write-Bytes ([IO.Path]::Combine($transport,'transport.json')) $mbytes
    $receipt=[ordered]@{identity=$Name;target='StandaloneWindows64-Player';contentReportPath='C:\fixture\outcome.json';transportPath=$transport;transportManifestSha256=(Get-Sha $mbytes);playerBuildGuid=('2'*32);backend='IL2CPP';stripping='High';scenes=@('Assets/UICommon.unity');bootstrapSceneName='UICommon';checkedRootGuids=@('3'*32)}
    Write-Bytes ([IO.Path]::Combine($player,'build-receipt.json')) ([Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $receipt -Depth 8)))
    return $player
}
function Test-Remote($F,[string]$Key){[IO.File]::Exists((Get-RemotePath $F.Remote $Key))}
function Retire-Build($F,[switch]$DryRun,[string]$Decision=$d1,[string]$Configuration='il2cpp-high'){Invoke-BuildCleanup $F.BuildConfig $proj $tgt $Configuration ([bool]$DryRun) $true $Decision}
function Clean-Build($F,[switch]$DryRun,[string]$Configuration='il2cpp-high'){Invoke-BuildCleanup $F.BuildConfig $proj $tgt $Configuration ([bool]$DryRun)}
# --- Content ---
function New-Rev($F,[DateTimeOffset]$At,[string]$ContentSet=$set,[int]$Files=2){
    $rev=$At.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff",[Globalization.CultureInfo]::InvariantCulture)+'Z-'+[Guid]::NewGuid().ToString('N')
    $items=@(1..$Files|ForEach-Object {[pscustomobject]@{path="f$_.cf";data=[Text.Encoding]::UTF8.GetBytes("payload-$rev-$_")}})
    $manifest=[ordered]@{version=1;contentSet=$ContentSet;revision=$rev;target='StandaloneWindows64-Player';files=@($items|ForEach-Object {[ordered]@{path=$_.path;size=$_.data.Length;sha256=(Get-Sha $_.data)}})}
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $manifest -Depth 8));$m=ConvertFrom-ContentManifest $bytes
    $dir=Get-ContentDirectory $repo $ContentSet $tgt $rev $true
    Set-Remote $F (Get-ContentKey $F.ContentData $ContentSet $tgt $rev) $bytes
    foreach($i in $items){Set-Remote $F (Get-ContentKey $F.ContentData $ContentSet $tgt $rev $i.path) $i.data}
    $intent=New-ContentIntent $F.ContentData $m;$sha=Set-ContentIntent $dir $intent
    $l=Enter-ContentPublishLock $dir;$l.Dispose()
    Write-ContentReceipt $dir (New-ContentReceipt $intent $sha $At.ToString('o'))
    return $rev
}
function New-Revs($F,[int]$Count,[string]$ContentSet=$set,[int]$Files=2){@(for($i=0;$i -lt $Count;$i++){New-Rev $F ($F.Now.AddMinutes(-(1+$i))) $ContentSet $Files})}
function Get-RevKeys2($F,[string]$Rev,[string]$ContentSet=$set){
    $i=Get-Content -LiteralPath ([IO.Path]::Combine((Get-ContentDirectory $repo $ContentSet $tgt $Rev),'intent.json')) -Raw|ConvertFrom-Json -Depth 8 -DateKind String
    return @(@(Get-ContentKey $F.ContentData $ContentSet $tgt $Rev)+@($i.entries|ForEach-Object {Get-ContentKey $F.ContentData $ContentSet $tgt $Rev $_.path}))
}
function Get-Left($F,[string]$Rev){@(Get-RevKeys2 $F $Rev|Where-Object {Test-Remote $F $_}).Count}
function Add-Player($F,[string]$Rev){$p=[IO.Path]::Combine($F.ArtifactsRoot,'players',$proj,$tgt,'il2cpp-high',$Rev.Substring($Rev.Length-32));[IO.Directory]::CreateDirectory($p)|Out-Null;return $p}
function Retire-Content($F,[switch]$DryRun,[string]$Decision=$d1,[string]$ContentSet=$set){Invoke-ContentCleanup $F.ContentConfig $ContentSet $tgt $F.ArtifactsRoot ([bool]$DryRun) $true $Decision}
function Clean-Content($F,[switch]$DryRun,[string]$ContentSet=$set){Invoke-ContentCleanup $F.ContentConfig $ContentSet $tgt $F.ArtifactsRoot ([bool]$DryRun)}
# --- 共通 ---
function Get-RecordPath($F,[string]$Kind,[string]$Leaf){[IO.Path]::Combine($F.O['retirements-v1'],$repo,$Kind,$Leaf)}
function Get-BuildRecord($F,[string]$Configuration='il2cpp-high'){Get-RecordPath $F 'build' "$proj~$tgt~$Configuration.json"}
function Edit-Json([string]$Path,[scriptblock]$Change){$v=Get-Content -LiteralPath $Path -Raw|ConvertFrom-Json -Depth 32 -DateKind String;& $Change $v;[IO.File]::WriteAllText($Path,(ConvertTo-Json -InputObject $v -Compress -Depth 32),[Text.UTF8Encoding]::new($false))}
function Get-Sent($F,[string]$Op,[string]$Prefix=''){@($F.Log|Where-Object {$_.StartsWith("$Op $Prefix",[StringComparison]::Ordinal)}).Count}
# rootの下の全fileの相対path→SHA-256。前後で比べ、既存fileの不変と、増えたfileの種類を確かめる。
function Get-FileMap([string]$Root){$m=@{};if([IO.Directory]::Exists($Root)){foreach($p in [IO.Directory]::EnumerateFiles($Root,'*',[IO.SearchOption]::AllDirectories)){$m[[IO.Path]::GetRelativePath($Root,$p)]=(Get-FileHash -LiteralPath $p).Hash}};return $m}
function Assert-Unchanged($Before,$After,[string]$AllowNew,[string]$Label){
    foreach($k in $Before.Keys){Assert ($After.ContainsKey($k) -and $After[$k] -ceq $Before[$k]) "$Label changed: $k"}
    foreach($k in $After.Keys){if(-not $Before.ContainsKey($k)){Assert ($AllowNew -and $k -match $AllowNew) "$Label new file: $k"}}
}
function Get-Fields($Result){@($Result.PSObject.Properties.Name|Sort-Object) -join ','}
$buildFields='candidates,deleted,deleting,excessCount,foreignInvalid,held,keepCount,mode,outcomes,reasonCode,remaining,requests,retained,seriesId,status'
$contentFields='candidates,deleted,deleting,excessCount,held,keepCount,mode,outcomes,protected,reasonCode,remaining,requests,retained,seriesId,status'
function Get-Leak($Result,$F){$json=ConvertTo-Json -InputObject $Result -Compress -Depth 20;return $json.Contains($F.Root) -or $json.Contains('https://') -or $json.Contains('r2.cloudflarestorage')}
function Test-Unknown([scriptblock]$Read){try{$null=& $Read;return $false}catch{return $_.Exception.Message -ceq 'retirement-unknown'}}
function Get-RetireChildArgs($F,[string]$Cli='',[string]$WriteRecord='',[string]$Decision='',[string]$NetworkMode='normal',[string]$Barrier='',[string]$Ready='',[string]$Go='',[int]$KeepCount=10){
    return @('-NoProfile','-File',[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'SeriesRetirementChild.ps1')),'-CliArgs',$Cli,'-WriteRecord',$WriteRecord,'-Decision',$Decision,
        '-BuildRoot',$F.O['builds-v1'],'-ContentRoot',$F.O['contents-v1'],'-RetireRoot',$F.O['retirements-v1'],'-TransferRoot',$F.O.transfers,'-DeploymentRoot',$F.O.deployments,'-RemoteRoot',$F.Remote,'-OwnedRoots',(@($F.O.Values) -join '|'),
        '-Now',$F.Now.ToString('o'),'-KeepCount',[string]$KeepCount,'-NetworkMode',$NetworkMode,'-Barrier',$Barrier,'-ReadyEvent',$Ready,'-GoEvent',$Go)
}
function ConvertFrom-ChildJson($Run){$line=@($Run.Output.Trim() -split "`r?`n"|Where-Object {$_ -ne ''})[-1];return $line|ConvertFrom-Json -Depth 20 -DateKind String}
function New-Event([string]$Name){[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,$Name)}
function Wait-Event($Event,$Job){if(-not $Event.WaitOne(60000)){try{$Job.Process.Kill($true)}catch{};throw 'child-not-ready'}}
function Get-BuildCli($F,[string]$Action,[string]$Configuration='il2cpp-high',[string[]]$Extra=@()){(@('build',$Action,'--profile','osm','--config',$F.BuildConfig,'--project',$proj,'--target',$tgt,'--configuration',$Configuration)+$Extra) -join '|'}

foreach($caseName in $selected){
    $fixture=$null
    try{
        Assert ($caseName -cin $registered) 'unknown-case'
        [Environment]::SetEnvironmentVariable('OSM_ARTIFACTS_ROOT',$null)
        $fixture=New-RetireFixture;$f=$fixture
        switch -CaseSensitive ($caseName){
            # AC2: 記録の書込み・再読取、不変（2回目の書込みは最初の記録を保つ）、3状態、各破損でretirement-unknown。
            'record-store'{
                $tok=@($proj,$tgt,'il2cpp-high')
                Assert ((Read-SeriesRetirement 'build' $repo $tok).State -ceq 'none') 'root absent is not none'
                Assert (-not [IO.Directory]::Exists($f.O['retirements-v1'])) 'read created root'
                $w=Write-SeriesRetirement 'build' $repo $tok $d1 $f.Now
                Assert ($w.State -ceq 'retired' -and $w.Record.decision -ceq $d1 -and $w.Record.retiredAt -ceq $f.Now.ToString('o') -and $w.Record.seriesId -ceq "$repo/$proj/$tgt/il2cpp-high") 'written record'
                Assert ((Get-Fields $w.Record) -ceq 'configuration,decision,kind,owner,project,repositoryId,retiredAt,schemaVersion,seriesId,target') "record fields: $(Get-Fields $w.Record)"
                Assert ($w.Record.owner -ceq [Security.Principal.WindowsIdentity]::GetCurrent().User.Value) 'owner SID'
                $path=Get-BuildRecord $f;Assert-ArtifactAcl $path $false;Assert-ArtifactAcl ([IO.Path]::GetDirectoryName($path)) $true;Assert-ArtifactAcl $f.O['retirements-v1'] $true
                $bytes=[IO.File]::ReadAllBytes($path)
                $w2=Write-SeriesRetirement 'build' $repo $tok $d2 $f.Now.AddHours(1)
                Assert ($w2.Record.decision -ceq $d1 -and $w2.Record.retiredAt -ceq $f.Now.ToString('o')) 'second write replaced the record'
                Assert ([Convert]::ToHexString([IO.File]::ReadAllBytes($path)) -ceq [Convert]::ToHexString($bytes)) 'record bytes changed'
                Assert (@([IO.Directory]::EnumerateFiles([IO.Path]::GetDirectoryName($path))).Count -eq 1) 'temp left after lost race'
                Assert ((Read-SeriesRetirement 'content' $repo @($set,$tgt)).State -ceq 'none') 'content kind shares the record'
                Assert ((Read-SeriesRetirement 'build' $repo @($proj,$tgt,'mono-debug')).State -ceq 'none') 'other series shares the record'
                foreach($bad in @('bad decision','','-x',('a'*129),"a`n",'a;b')){$code=try{$null=Write-SeriesRetirement 'build' $repo @($proj,$tgt,'c-bad') $bad $f.Now;'ok'}catch{$_.Exception.Message};Assert ($code -ceq 'invalid-decision') "decision accepted: [$bad]"}
                Assert (-not [IO.File]::Exists((Get-BuildRecord $f 'c-bad'))) 'invalid decision wrote'
                # 各破損: 系列ごとに1件書いてから壊し、読めば不明になる。
                $mutations=[ordered]@{
                    'extra-field'={param($p)Edit-Json $p {param($v)$v|Add-Member -NotePropertyName extra -NotePropertyValue 1}}
                    'kind'={param($p)Edit-Json $p {param($v)$v.kind='content'}}
                    'schema'={param($p)Edit-Json $p {param($v)$v.schemaVersion=2}}
                    'decision'={param($p)Edit-Json $p {param($v)$v.decision='bad decision'}}
                    'retiredAt'={param($p)Edit-Json $p {param($v)$v.retiredAt='2026-10-12'}}
                    'owner'={param($p)Edit-Json $p {param($v)$v.owner='S-1-5-18'}}
                    'token'={param($p)Edit-Json $p {param($v)$v.configuration='other'}}
                    'other-series'={param($p)[IO.File]::WriteAllBytes($p,$bytes)}
                    'not-json'={param($p)[IO.File]::WriteAllText($p,'x')}
                    'acl'={param($p)$own=[IO.File]::ReadAllBytes($p);[IO.File]::Delete($p);[IO.File]::WriteAllBytes($p,$own)}
                    'directory'={param($p)[IO.File]::Delete($p);[IO.Directory]::CreateDirectory($p)|Out-Null}
                }
                $n=0
                foreach($name in $mutations.Keys){
                    $n++;$c="c$n";$null=Write-SeriesRetirement 'build' $repo @($proj,$tgt,$c) $d1 $f.Now
                    & $mutations[$name] (Get-BuildRecord $f $c)
                    Assert (Test-Unknown {Read-SeriesRetirement 'build' $repo @($proj,$tgt,$c)}) "corruption read as state: $name"
                }
                # 階層のreparse（junction）とroot自体がfileのとき。
                $elsewhere=[IO.Path]::Combine($f.Root,'elsewhere');[IO.Directory]::CreateDirectory($elsewhere)|Out-Null
                $junction=[IO.Path]::Combine($f.O['retirements-v1'],$repo,'content')
                $null=New-Item -ItemType Junction -Path $junction -Target $elsewhere
                try{Assert (Test-Unknown {Read-SeriesRetirement 'content' $repo @($set,$tgt)}) 'reparse read as state'}
                finally{[IO.Directory]::Delete($junction)}
                $fileRoot=[IO.Path]::Combine($f.Root,'root-file');[IO.File]::WriteAllText($fileRoot,'x');Set-EvidenceTestValue SeriesRetirementStore TestRoot $fileRoot
                Assert (Test-Unknown {Read-SeriesRetirement 'build' $repo $tok}) 'root file read as state'
            }
            # AC2・AC3: 2つのprocessが同時に書くと、renameに勝った1件だけが残り、両者が同じ記録を返す。
            'record-race'{
                $tag='osm-retire-'+[Guid]::NewGuid().ToString('N');$go=New-Event "$tag-go";$r1=New-Event "$tag-r1";$r2=New-Event "$tag-r2"
                $a=Invoke-Child (Get-RetireChildArgs $f -WriteRecord "build:$proj,$tgt,il2cpp-high" -Decision $d1 -Ready "$tag-r1" -Go "$tag-go")
                $b=Invoke-Child (Get-RetireChildArgs $f -WriteRecord "build:$proj,$tgt,il2cpp-high" -Decision $d2 -Ready "$tag-r2" -Go "$tag-go")
                Wait-Event $r1 $a;Wait-Event $r2 $b
                $null=$go.Set();$ra=Finish-Child $a;$rb=Finish-Child $b
                $oa=ConvertFrom-ChildJson $ra;$ob=ConvertFrom-ChildJson $rb
                Assert ($ra.Exit -eq 0 -and $rb.Exit -eq 0 -and $oa.state -ceq 'retired' -and $ob.state -ceq 'retired') "race results: $($ra.Output) $($rb.Output)"
                Assert ($oa.decision -ceq $ob.decision -and $oa.decision -cin @($d1,$d2)) 'race returned different records'
                Assert ((Read-SeriesRetirement 'build' $repo @($proj,$tgt,'il2cpp-high')).Record.decision -ceq $oa.decision) 'stored record differs'
                Assert (@([IO.Directory]::EnumerateFiles([IO.Path]::GetDirectoryName((Get-BuildRecord $f)))).Count -eq 1) 'temp left after race'
            }
            # AC4: 退役の選別は純粋関数。成功・保護中・未完了・失敗の混在、全順序、空、不正入力。件数保持の契約は変えない。
            'policy-table'{
                $t=$baseNow
                $row={param($id,$state,$min,$use)if($state -ceq 'succeeded'){[pscustomobject]@{buildId=$id;publishState=$state;publishedAt=$t.AddMinutes(-$min);inUse=$use}}else{[pscustomobject]@{buildId=$id;publishState=$state}}}
                $rows=[object[]]@((& $row 'A' 'succeeded' 1 $false),(& $row 'B' 'succeeded' 2 $true),(& $row 'C' 'incomplete'),(& $row 'D' 'succeeded' 3 $false),(& $row 'E' 'failed'),(& $row 'F' 'succeeded' 1 $false))
                $x=Get-SeriesRetirementDecision 's' $rows
                Assert ($x.status -ceq 'evaluated' -and $x.reasonCode -ceq 'candidates-found' -and $x.excessCount -eq 0 -and $x.retainedBuildIds.Count -eq 0) 'decision status'
                Assert ((@($x.deleteCandidateBuildIds) -join ',') -ceq 'D,F,A') "candidate order: $(@($x.deleteCandidateBuildIds) -join ',')"
                Assert ((@($x.heldBuilds|ForEach-Object {"$($_.buildId):$($_.reasonCode)"}) -join ',') -ceq 'B:protected,C:publish-incomplete,E:publish-failed') 'held classification'
                Assert ((Get-SeriesRetirementDecision 's' ([object[]]@())).reasonCode -ceq 'retired') 'empty series'
                $h=Get-SeriesRetirementDecision 's' ([object[]]@((& $row 'B' 'succeeded' 2 $true)))
                Assert ($h.reasonCode -ceq 'retired-held' -and $h.deleteCandidateBuildIds.Count -eq 0 -and $h.heldBuilds.Count -eq 1) 'protected only'
                Assert ((Get-SeriesRetirementDecision $null $rows).reasonCode -ceq 'invalid-series-id') 'series id'
                Assert ((Get-SeriesRetirementDecision 's' ([Collections.Generic.List[object]]::new())).reasonCode -ceq 'invalid-build-list') 'list type'
                foreach($bad in @(([pscustomobject]@{buildId='X';publishState='succeeded';publishedAt=$t}),([pscustomobject]@{buildId='X';publishState='succeeded';publishedAt=$t.ToOffset([TimeSpan]::FromHours(9));inUse=$false}),([pscustomobject]@{buildId='A';publishState='incomplete'}),([pscustomobject]@{buildId='X';publishState='unknown'}))){
                    $b=Get-SeriesRetirementDecision 's' ([object[]]($rows+@($bad)))
                    Assert ($b.status -ceq 'blocked' -and $b.reasonCode -ceq 'invalid-build-snapshot' -and $b.deleteCandidateBuildIds.Count -eq 0 -and $null -eq $b.excessCount) 'invalid row not blocked'
                }
                # 件数保持の契約（KeepCount >= 1、N内の保護優先）は変わらない。
                Assert ((Get-BuildRetentionDecision 's' $rows 0).reasonCode -ceq 'invalid-keep-count') 'KeepCount 0 accepted'
                $k=Get-BuildRetentionDecision 's' ([object[]]@((& $row 'A' 'succeeded' 1 $false),(& $row 'B' 'succeeded' 2 $false),(& $row 'C' 'succeeded' 3 $true))) 2
                Assert ((@($k.retainedBuildIds) -join ',') -ceq 'A,C' -and (@($k.deleteCandidateBuildIds) -join ',') -ceq 'B') 'retention semantics changed'
            }
            # AC7: dry-runはlockも退役記録も書かず、networkに触れず、退役後に行う選別を返す。件数保持の結果の形は変えない。
            'dry-run-no-writes'{
                $ids=New-Builds $f 3;$revs=New-Revs $f 2
                $store=Get-FileMap $f.O['builds-v1'];$cstore=Get-FileMap $f.O['contents-v1']
                $x=Retire-Build $f -DryRun
                Assert ($x.status -ceq 'passed' -and $x.reasonCode -ceq 'candidates-found' -and $x.mode -ceq 'retirement' -and $null -eq $x.retirement -and $null -eq $x.keepCount) "build dry-run: $(ConvertTo-Json $x -Compress -Depth 6)"
                Assert ((@($x.candidates.buildIds) -join ',') -ceq (@($ids[2],$ids[1],$ids[0]) -join ',') -and $x.remaining -eq 3) 'dry-run candidates'
                Assert ((Get-Fields $x) -ceq (@(($buildFields+',retirement,unattributed').Split(',')|Sort-Object) -join ',')) "dry-run fields: $(Get-Fields $x)"
                $y=Retire-Content $f -DryRun
                Assert ($y.status -ceq 'passed' -and $y.mode -ceq 'retirement' -and $null -eq $y.retirement -and $y.candidates.count -eq 2) 'content dry-run'
                Assert ((Get-Fields $y) -ceq (@(($contentFields+',retirement').Split(',')|Sort-Object) -join ',')) "content dry-run fields: $(Get-Fields $y)"
                Assert ($f.Log.Count -eq 0) 'dry-run sent'
                Assert (-not [IO.Directory]::Exists($f.O['retirements-v1'])) 'dry-run wrote a record'
                Assert-Unchanged $store (Get-FileMap $f.O['builds-v1']) '' 'build store';Assert-Unchanged $cstore (Get-FileMap $f.O['contents-v1']) '' 'content store'
                # 退役していない系列の件数清掃は従来の結果の形に mode='retention' を足しただけ。
                Assert ((Get-Fields (Clean-Build $f -DryRun)) -ceq $buildFields -and (Get-Fields (Clean-Content $f -DryRun)) -ceq $contentFields) 'retention result shape changed'
                Assert ((Clean-Build $f -DryRun).mode -ceq 'retention' -and (Clean-Content $f -DryRun).mode -ceq 'retention') 'retention mode field'
            }
            # S1・S2・S3・AC3・AC6・AC7・I7: 保護中を残してretired-held、期限切れ後にretired、再実行は送信0、記録は最初のまま。
            'build-retire-complete'{
                $ids=New-Builds $f 4;Add-Use $ids[0] $f.Now.AddMinutes(-1) $f.Now.AddDays(1)
                $x=Retire-Build $f
                Assert ($x.status -ceq 'passed' -and $x.reasonCode -ceq 'retired-held' -and $x.mode -ceq 'retirement' -and $null -eq $x.keepCount) "first retire: $(ConvertTo-Json $x -Compress -Depth 6)"
                Assert ($x.retirement.decision -ceq $d1 -and $x.retirement.retiredAt -ceq $f.Now.ToString('o')) 'retirement field'
                Assert ($x.deleted.count -eq 3 -and $x.held.count -eq 1 -and $x.held.builds[0].buildId -ceq $ids[0] -and $x.held.builds[0].reasonCode -ceq 'protected') 'held protected'
                Assert ((@($x.outcomes|ForEach-Object buildId) -join ',') -ceq (@($ids[3],$ids[2],$ids[1]) -join ',')) 'not oldest first'
                Assert ($x.requests.delete -eq 3 -and $x.requests.get -eq 3) 'requests'
                Assert ((Test-Remote $f (Get-BuildKey2 $ids[0])) -and -not (Test-Remote $f (Get-BuildKey2 $ids[1])) -and -not (Test-Remote $f (Get-BuildKey2 $ids[3]))) 'remote objects'
                Assert ((Get-Fields $x) -ceq (@(($buildFields+',retirement,unattributed').Split(',')|Sort-Object) -join ',')) "fields: $(Get-Fields $x)"
                Assert (-not (Get-Leak $x $f)) 'result leaked path or endpoint'
                foreach($id in $ids[1..3]){Assert (@([IO.Directory]::EnumerateFiles([IO.Path]::Combine((Get-BuildDir2 $id),'deletion'))|Where-Object {$_ -like '*.deleted.json'}).Count -eq 1) 'deleted record'}
                $record=[IO.File]::ReadAllBytes((Get-BuildRecord $f))
                # 保護中の間の再実行（別の指示ID）: 送信0、記録・decisionは最初のまま。
                $f.Now=$f.Now.AddHours(1);$y=Retire-Build $f -Decision $d2
                Assert ($y.reasonCode -ceq 'retired-held' -and $y.requests.delete -eq 0 -and $y.requests.get -eq 0 -and $y.retirement.decision -ceq $d1 -and $y.retirement.retiredAt -ceq $baseNow.ToString('o')) 'rerun while held'
                # 保護の期限切れ後: 残りが消えてretired。
                $f.Now=$f.Now.AddDays(2);$z=Retire-Build $f -Decision $d2
                Assert ($z.status -ceq 'passed' -and $z.reasonCode -ceq 'retired' -and $z.deleted.count -eq 4 -and $z.held.count -eq 0 -and $z.requests.delete -eq 1 -and -not (Test-Remote $f (Get-BuildKey2 $ids[0]))) "after expiry: $($z.reasonCode)"
                $again=Retire-Build $f -Decision $d2
                Assert ($again.reasonCode -ceq 'retired' -and $again.requests.delete -eq 0 -and $again.requests.get -eq 0) 'completed rerun sent'
                Assert ([Convert]::ToHexString([IO.File]::ReadAllBytes((Get-BuildRecord $f))) -ceq [Convert]::ToHexString($record)) 'record rewritten'
                # 退役後に現れた未完了（receiptの無いBuild）は消さずにretired-held（publish-incomplete）。
                $inc=New-Build $f $f.Now -IntentOnly
                $w=Retire-Build $f
                Assert ($w.reasonCode -ceq 'retired-held' -and $w.requests.delete -eq 0 -and @($w.held.builds|Where-Object {$_.buildId -ceq $inc -and $_.reasonCode -ceq 'publish-incomplete'}).Count -eq 1 -and (Test-Remote $f (Get-BuildKey2 $inc))) 'incomplete held'
                Assert ($w.unattributed.count -eq 0) 'attributed incomplete counted as unattributed'
                # 系列を特定できない未完了（予約だけ・receiptも読めないBuild）は判定に数えず、heldにも混ぜず、unattributedに件数だけ出す。
                # retiredとheld.countが食い違わない。件数保持では従来どおりheldに入る。
                $rv=[IO.Path]::Combine((Get-BuildRoot),$repo,('9'*32)+'.reserved');[IO.File]::WriteAllBytes($rv,[byte[]]@());Set-ArtifactAcl $rv $false
                $null=Remove-Item -LiteralPath (Get-BuildDir2 $inc) -Recurse -Force;Remove-Item -LiteralPath ((Get-BuildDir2 $inc)+".reserved") -Force -ErrorAction SilentlyContinue
                $u=Retire-Build $f
                Assert ($u.status -ceq 'passed' -and $u.reasonCode -ceq 'retired' -and $u.held.count -eq 0 -and $u.unattributed.count -eq 1 -and $u.requests.delete -eq 0) "unattributed: $(ConvertTo-Json $u -Compress -Depth 6)"
                $k=Clean-Build $f
                Assert ($k.mode -ceq 'retirement' -and $k.unattributed.count -eq 1 -and $k.held.count -eq 0) 'cleanup after retire unattributed'
            }
            # AC3・I1・I3・AC8: 退役記録が確定する前・不明なときは何も送らない。cleanupも件数保持へ戻らない。
            'record-before-delete'{
                $ids=New-Builds $f 2
                Set-EvidenceTestValue SeriesRetirementStore FaultHook ({param($p)throw 'fault'})
                $x=Retire-Build $f
                Set-EvidenceTestValue SeriesRetirementStore FaultHook $null
                Assert ($x.status -ceq 'failed' -and $f.Log.Count -eq 0 -and -not [IO.File]::Exists((Get-BuildRecord $f))) 'sent before the record'
                Assert (@([IO.Directory]::EnumerateFiles([IO.Path]::GetDirectoryName((Get-BuildRecord $f)))).Count -eq 0) 'temp left after failed write'
                foreach($id in $ids){Assert (-not [IO.Directory]::Exists([IO.Path]::Combine((Get-BuildDir2 $id),'deletion'))) 'deletion record before the retirement record'}
                $null=Write-SeriesRetirement 'build' $repo @($proj,$tgt,'il2cpp-high') $d1 $f.Now;Edit-Json (Get-BuildRecord $f) {param($v)$v.kind='content'}
                $before=[IO.File]::ReadAllBytes((Get-BuildRecord $f))
                foreach($run in @({Retire-Build $f},{Clean-Build $f},{Clean-Build $f -DryRun},{Retire-Build $f -DryRun})){
                    $r=& $run;Assert ($r.status -ceq 'blocked' -and $r.reasonCode -ceq 'retirement-unknown' -and $r.requests.delete -eq 0) "unknown record not blocked: $($r.reasonCode)"
                }
                Assert ($f.Log.Count -eq 0 -and [Convert]::ToHexString([IO.File]::ReadAllBytes((Get-BuildRecord $f))) -ceq [Convert]::ToHexString($before)) 'unknown record overwritten or sent'
                # Contentも同じ。
                $revs=New-Revs $f 1;$null=Write-SeriesRetirement 'content' $repo @($set,$tgt) $d1 $f.Now;[IO.File]::WriteAllText((Get-RecordPath $f 'content' "$set~$tgt.json"),'x')
                foreach($run in @({Retire-Content $f},{Clean-Content $f})){$r=& $run;Assert ($r.status -ceq 'blocked' -and $r.reasonCode -ceq 'retirement-unknown') 'content unknown record not blocked'}
                Assert ($f.Log.Count -eq 0) 'content sent with unknown record'
            }
            # S3・AC6: 途中で止まった退役（403・予算・DELETE上限）は次の実行が既存の状態遷移表の再開から確定させる。
            'build-resume'{
                $ids=New-Builds $f 3
                $f.Hook={param($g,$r,$p)if($r.Operation -ceq 'delete'){New-Response 'delete' $g 403 'forbidden' 'AccessDenied'}}
                $x=Retire-Build $f
                Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'delete-pending' -and $x.deleting.count -eq 1 -and $x.deleted.count -eq 0 -and [IO.File]::Exists((Get-BuildRecord $f))) "403: $($x.reasonCode)"
                $f.Hook=$null;$y=Retire-Build $f
                Assert ($y.reasonCode -ceq 'retired' -and $y.deleted.count -eq 3) "resume after 403: $($y.reasonCode)"
                $p=New-Builds $f 2 'probe-b'
                Set-EvidenceTestValue BuildCleanup BudgetHook ({param($point)if($point -ceq 'get'){throw 'budget-exceeded'}})
                $x=Retire-Build $f -Configuration 'probe-b'
                Set-EvidenceTestValue BuildCleanup BudgetHook $null
                Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'budget-exceeded') "budget: $($x.reasonCode)"
                $y=Retire-Build $f -Configuration 'probe-b'
                Assert ($y.reasonCode -ceq 'retired' -and $y.deleted.count -eq 2) 'resume after budget'
                $q=New-Builds $f 3 'probe-c';Set-EvidenceTestValue BuildCleanup DeleteLimit 2
                $x=Retire-Build $f -Configuration 'probe-c'
                Set-EvidenceTestValue BuildCleanup DeleteLimit 20
                Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'delete-limit' -and $x.remaining -eq 1 -and $x.deleted.count -eq 2) "limit: $($x.reasonCode)"
                $y=Retire-Build $f -Configuration 'probe-c'
                Assert ($y.reasonCode -ceq 'retired' -and $y.deleted.count -eq 3 -and $y.requests.delete -eq 1) 'resume after limit'
            }
            # AC8・AC9・AC10: 退役後のcleanupは退役の選別、publish・useはseries-retired、inspect・fetchは従来どおり。
            'build-after-retire'{
                $old=Publish-Build $f;$kept=Publish-Build $f
                Assert ((Invoke-BuildUse $f.BuildConfig $kept.buildId 'fixture-user' $f.Now.AddDays(1).ToString('o') 'keep').status -ceq 'passed') 'use before retire'
                $x=Retire-Build $f
                Assert ($x.reasonCode -ceq 'retired-held' -and $x.deleted.count -eq 1) 'retire'
                $c=Clean-Build $f
                Assert ($c.status -ceq 'passed' -and $c.mode -ceq 'retirement' -and $c.reasonCode -ceq 'retired-held' -and $c.retirement.decision -ceq $d1 -and $c.requests.delete -eq 0 -and $null -eq $c.keepCount) 'cleanup after retire'
                Assert ((Invoke-BuildInspect $f.BuildConfig $old.buildId).reasonCode -ceq 'payload-deleted') 'inspect deleted'
                Assert ((Invoke-BuildInspect $f.BuildConfig $kept.buildId).status -ceq 'passed') 'inspect protected'
                Assert ((Invoke-BuildFetch $f.BuildConfig $old.reference $old.packageSha256).status -ceq 'failed') 'fetch deleted succeeded'
                Assert ((Invoke-BuildFetch $f.BuildConfig $kept.reference $kept.packageSha256).status -ceq 'passed') 'fetch protected failed'
                $f.Log.Clear();$builds=Get-FileMap $f.O['builds-v1']
                $p=Invoke-BuildPublish $f.BuildConfig (Write-Selection $f)
                Assert ($p.status -ceq 'failed' -and $p.reasonCode -ceq 'series-retired' -and $null -eq $p.reference) "publish: $($p.reasonCode)"
                $u=Invoke-BuildUse $f.BuildConfig $kept.buildId 'fixture-user' $f.Now.AddDays(1).ToString('o') 'again'
                Assert ($u.status -ceq 'failed' -and $u.reasonCode -ceq 'series-retired') "use: $($u.reasonCode)"
                Assert ((Get-Sent $f 'put') -eq 0) 'PUT after retire'
                Assert-Unchanged $builds (Get-FileMap $f.O['builds-v1']) '\\locks\\' 'build store after rejected publish/use'
                # 記録が不明ならpublish・useはretirement-unknown（送信・書込みなし）。
                Edit-Json (Get-BuildRecord $f) {param($v)$v.schemaVersion=9}
                Assert ((Invoke-BuildPublish $f.BuildConfig (Write-Selection $f)).reasonCode -ceq 'retirement-unknown') 'publish with unknown record'
                Assert ((Invoke-BuildUse $f.BuildConfig $kept.buildId 'fixture-user' $f.Now.AddDays(1).ToString('o') 'x').reasonCode -ceq 'retirement-unknown') 'use with unknown record'
                Assert ((Get-Sent $f 'put') -eq 0) 'PUT with unknown record'
                Assert-Unchanged $builds (Get-FileMap $f.O['builds-v1']) '\\locks\\' 'build store after unknown record'
                # 他系列は影響を受けない。
                $other=Invoke-BuildPublish $f.BuildConfig (Write-Selection $f 'mono-debug')
                Assert ($other.status -ceq 'passed') 'other series publish blocked'
            }
            # AC9: build collectは退避・既存成功の応答より前に止まる（existingも返さない）。
            'build-collect-retired'{
                $identity='20260924T202227427Z-0123456789abcdef0123456789abcdef'
                $player=New-CollectPlayer $f $identity
                $c1=Invoke-BuildCollect $f.BuildConfig $player $f.ArtifactsRoot
                Assert ($c1.status -ceq 'passed' -and $c1.publish -ceq 'published') "first collect: $($c1.reasonCode)"
                $again=Invoke-BuildCollect $f.BuildConfig $player $f.ArtifactsRoot
                Assert ($again.publish -ceq 'existing') 'existing before retire'
                $x=Retire-Build $f
                Assert ($x.reasonCode -ceq 'retired' -and $x.deleted.count -eq 1) 'retire collected'
                $f.Log.Clear();$art=Get-FileMap $f.ArtifactsRoot;$builds=Get-FileMap $f.O['builds-v1']
                $c2=Invoke-BuildCollect $f.BuildConfig $player $f.ArtifactsRoot
                Assert ($c2.status -ceq 'failed' -and $c2.reasonCode -ceq 'series-retired' -and $null -eq $c2.publish -and $null -eq $c2.reference) "collect existing: $($c2.reasonCode) $($c2.publish)"
                $player2=New-CollectPlayer $f '20260924T202227428Z-fedcba9876543210fedcba9876543210'
                $c3=Invoke-BuildCollect $f.BuildConfig $player2 $f.ArtifactsRoot
                Assert ($c3.status -ceq 'failed' -and $c3.reasonCode -ceq 'series-retired' -and $null -eq $c3.stage) "collect new: $($c3.reasonCode)"
                Assert ($f.Log.Count -eq 0) 'collect sent after retire'
                Assert-Unchanged $art (Get-FileMap $f.ArtifactsRoot) '' 'ArtifactsRoot';Assert-Unchanged $builds (Get-FileMap $f.O['builds-v1']) '' 'build store'
                Edit-Json (Get-BuildRecord $f) {param($v)$v.owner='S-1-5-18'}
                Assert ((Invoke-BuildCollect $f.BuildConfig $player2 $f.ArtifactsRoot).reasonCode -ceq 'retirement-unknown') 'collect with unknown record'
            }
            # AC11・AC12: 指定系列の成功記録から作ったkey以外へDELETEを送らず、他系列・既存記録・ArtifactsRootを変えない。
            'build-boundary'{
                $ids=New-Builds $f 3;Add-Use $ids[0] $f.Now.AddMinutes(-1) $f.Now.AddMinutes(30)
                $null=New-Builds $f 3 'mono-debug';$null=New-Build $f $f.Now 'mono-debug' -IntentOnly
                Write-Bytes ([IO.Path]::Combine($f.ArtifactsRoot,'players',$proj,$tgt,'il2cpp-high',$ids[1],'SampleGame.exe')) ([byte[]]@(1,2,3))
                Write-Bytes ([IO.Path]::Combine($f.ArtifactsRoot,'content',$set,$tgt,'x','transport.json')) ([byte[]]@(4))
                $otherDry=ConvertTo-Json (Clean-Build $f -DryRun 'mono-debug') -Compress -Depth 8
                $expected=@($ids|ForEach-Object {(Get-Content -LiteralPath ([IO.Path]::Combine((Get-BuildDir2 $_),'receipt.json')) -Raw|ConvertFrom-Json -DateKind String).key})
                $store=Get-FileMap $f.O['builds-v1'];$art=Get-FileMap $f.ArtifactsRoot
                $x=Retire-Build $f;$f.Now=$f.Now.AddHours(1);$y=Retire-Build $f;$z=Clean-Build $f
                Assert ($x.reasonCode -ceq 'retired-held' -and $y.reasonCode -ceq 'retired' -and $z.mode -ceq 'retirement') 'retire sequence'
                $deletes=@($f.Log|Where-Object {$_.StartsWith('delete ',[StringComparison]::Ordinal)}|ForEach-Object {$_.Substring(7)})
                Assert ($deletes.Count -eq 3 -and @($deletes|Where-Object {$_ -cnotin $expected}).Count -eq 0) "DELETE outside the series: $($deletes -join ',')"
                Assert (@($f.Log|Where-Object {$_ -like '*/mono-debug/*' -or $_ -like '*development/content/*' -or $_ -like '*development/evidence/*'}).Count -eq 0) 'other key touched'
                Assert ((ConvertTo-Json (Clean-Build $f -DryRun 'mono-debug') -Compress -Depth 8) -ceq $otherDry) 'other series dry-run changed'
                Assert-Unchanged $store (Get-FileMap $f.O['builds-v1']) '\\deletion\\[0-9]+\.(intent|sent|deleted)\.json$|^[0-9a-f]{64}\\locks\\[^\\]+\.lock$|\\access\.lock$' 'build store'
                Assert-Unchanged $art (Get-FileMap $f.ArtifactsRoot) '' 'ArtifactsRoot'
            }
            # AC3: 系列lockを他processが持つ間はcleanup-in-progressで、退役記録も書かない。
            'build-lock'{
                $ids=New-Builds $f 3
                $tag='osm-retire-'+[Guid]::NewGuid().ToString('N');$ready=New-Event "$tag-ready";$go=New-Event "$tag-go"
                $job=Invoke-Child (Get-RetireChildArgs $f -Cli (Get-BuildCli $f 'cleanup') -KeepCount 1 -Barrier 'delete' -Ready "$tag-ready" -Go "$tag-go")
                try{
                    Wait-Event $ready $job
                    $x=Retire-Build $f
                    Assert ($x.status -ceq 'blocked' -and $x.reasonCode -ceq 'cleanup-in-progress' -and -not [IO.File]::Exists((Get-BuildRecord $f))) "lock not respected: $($x.reasonCode)"
                }finally{$null=$go.Set()}
                $run=Finish-Child $job
                Assert ($run.Exit -eq 0) "child cleanup: $($run.Output)"
                $y=Retire-Build $f
                Assert ($y.reasonCode -ceq 'retired' -and $y.deleted.count -eq 3) 'retire after lock'
            }
            # AC13: 退役記録の確定より前に始まったpublishは止めず、成功すれば次の退役で候補になる。
            'build-publish-race'{
                $ids=New-Builds $f 1;$sel=Write-Selection $f;$newId=(Get-Content -LiteralPath $sel -Raw|ConvertFrom-Json).buildId
                $tag='osm-retire-'+[Guid]::NewGuid().ToString('N');$ready=New-Event "$tag-ready";$go=New-Event "$tag-go"
                $job=Invoke-Child (Get-RetireChildArgs $f -Cli (@('build','publish','--profile','osm','--config',$f.BuildConfig,'--selection',$sel) -join '|') -Barrier 'put' -Ready "$tag-ready" -Go "$tag-go")
                try{
                    Wait-Event $ready $job
                    $x=Retire-Build $f
                    Assert ($x.reasonCode -ceq 'retired-held' -and $x.deleted.count -eq 1 -and @($x.held.builds|Where-Object {$_.buildId -ceq $newId -and $_.reasonCode -ceq 'publish-incomplete'}).Count -eq 1) "retire during publish: $(ConvertTo-Json $x.held -Compress -Depth 4)"
                }finally{$null=$go.Set()}
                $run=Finish-Child $job
                Assert ($run.Exit -eq 0 -and (ConvertFrom-ChildJson $run).status -ceq 'passed') "publish started before the record: $($run.Output)"
                $f.Now=$f.Now.AddMinutes(1);$y=Retire-Build $f
                Assert ($y.reasonCode -ceq 'retired' -and $y.deleted.count -eq 2 -and -not (Test-Remote $f (Get-BuildKey2 $newId))) 'raced publish not retired'
                $late=Invoke-BuildPublish $f.BuildConfig (Write-Selection $f)
                Assert ($late.reasonCode -ceq 'series-retired') 'publish after the record'
            }
            # S1・S2・S3・AC3・AC6・AC7（Content）: 保持中Playerのrevisionを残してretired-held、保護が外れた後にretired。
            'content-retire'{
                $revs=New-Revs $f 3;$player=Add-Player $f $revs[0]
                $x=Retire-Content $f
                Assert ($x.status -ceq 'passed' -and $x.reasonCode -ceq 'retired-held' -and $x.mode -ceq 'retirement' -and $x.retirement.decision -ceq $d1) "content retire: $(ConvertTo-Json $x -Compress -Depth 6)"
                Assert ($x.deleted.count -eq 2 -and $x.held.count -eq 1 -and $x.held.revisions[0] -ceq $revs[0] -and $x.protected.count -eq 1) 'content held'
                Assert ((@($x.outcomes|ForEach-Object revision) -join ',') -ceq (@($revs[2],$revs[1]) -join ',')) 'content not oldest first'
                Assert ($x.requests.delete -eq 6 -and $x.requests.get -eq 10) "content requests: $($x.requests.delete) $($x.requests.get)"
                Assert ((Get-Left $f $revs[0]) -eq 3 -and (Get-Left $f $revs[1]) -eq 0 -and (Get-Left $f $revs[2]) -eq 0) 'content remote'
                Assert ((Get-Fields $x) -ceq (@(($contentFields+',retirement').Split(',')|Sort-Object) -join ',')) "content fields: $(Get-Fields $x)"
                Assert (-not (Get-Leak $x $f)) 'content result leaked'
                [IO.Directory]::Delete($player,$true)
                $f.Now=$f.Now.AddHours(1);$y=Retire-Content $f -Decision $d2
                Assert ($y.reasonCode -ceq 'retired' -and $y.deleted.count -eq 3 -and $y.retirement.decision -ceq $d1 -and (Get-Left $f $revs[0]) -eq 0) "content after release: $($y.reasonCode)"
                $again=Retire-Content $f
                Assert ($again.reasonCode -ceq 'retired' -and $again.requests.delete -eq 0 -and $again.requests.get -eq 0) 'content rerun sent'
            }
            # S3・AC6（Content）: 確定点の後で止まった退役は、次の実行が残りobjectのGETから続けて消し切る。
            'content-resume'{
                $revs=New-Revs $f 2
                $target=(Get-RevKeys2 $f $revs[1])[1]
                $f.Hook={param($g,$r,$p)if($r.Operation -ceq 'delete' -and $r.Key -ceq $target){New-Response 'delete' $g 403 'forbidden' 'AccessDenied'}}.GetNewClosure()
                $x=Retire-Content $f
                Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'delete-pending' -and $x.deleting.count -eq 1 -and $x.deleted.count -eq 0) "after-commit stop: $($x.reasonCode)"
                Assert ([IO.File]::Exists([IO.Path]::Combine((Get-ContentDirectory $repo $set $tgt $revs[1]),'deletion','1.committed.json'))) 'commit point not recorded'
                $f.Hook=$null;$y=Retire-Content $f
                Assert ($y.reasonCode -ceq 'retired' -and $y.deleted.count -eq 2 -and (Get-Left $f $revs[1]) -eq 0 -and (Get-Left $f $revs[0]) -eq 0) "content resume: $($y.reasonCode)"
            }
            # AC8・AC9・AC10（Content）: 退役後のcleanupは退役の選別、collectはseries-retired（existingも返さない）、inspect・fetchは従来どおり。
            'content-after-retire'{
                $a=New-ContentSource $f '20260924T202227427Z-0123456789abcdef0123456789abcdef' -Seed 'a'
                $b=New-ContentSource $f '20260924T202227428Z-fedcba9876543210fedcba9876543210' -Seed 'b'
                $ca=Invoke-ContentCollect $f.ContentConfig $a.Dir $f.ArtifactsRoot;$f.Now=$f.Now.AddMinutes(1)
                $cb=Invoke-ContentCollect $f.ContentConfig $b.Dir $f.ArtifactsRoot;$f.Now=$f.Now.AddMinutes(1)
                Assert ($ca.status -ceq 'passed' -and $cb.status -ceq 'passed') "collect: $($ca.reasonCode) $($cb.reasonCode)"
                $null=Add-Player $f $b.Revision
                $x=Retire-Content $f
                Assert ($x.reasonCode -ceq 'retired-held' -and $x.deleted.count -eq 1 -and $x.deleted.revisions[0] -ceq $a.Revision) 'content retire'
                $c=Clean-Content $f
                Assert ($c.status -ceq 'passed' -and $c.mode -ceq 'retirement' -and $c.requests.delete -eq 0 -and $c.reasonCode -ceq 'retired-held') 'content cleanup after retire'
                Assert ((Invoke-ContentInspect $f.ContentConfig $set $tgt $a.Revision).reasonCode -ceq 'payload-deleted') 'content inspect deleted'
                Assert ((Invoke-ContentInspect $f.ContentConfig $set $tgt $b.Revision).status -ceq 'passed') 'content inspect protected'
                Assert ((Invoke-ContentFetch $f.ContentConfig $ca.reference $a.ManifestSha).status -ceq 'failed') 'content fetch deleted succeeded'
                Assert ((Invoke-ContentFetch $f.ContentConfig $cb.reference $b.ManifestSha).status -ceq 'passed') 'content fetch protected failed'
                $f.Log.Clear();$art=Get-FileMap $f.ArtifactsRoot;$store=Get-FileMap $f.O['contents-v1']
                $r1=Invoke-ContentCollect $f.ContentConfig $b.Dir $f.ArtifactsRoot
                Assert ($r1.status -ceq 'failed' -and $r1.reasonCode -ceq 'series-retired' -and $null -eq $r1.publish -and $null -eq $r1.reference) "collect existing: $($r1.reasonCode) $($r1.publish)"
                $n=New-ContentSource $f '20260924T202227429Z-00112233445566778899aabbccddeeff' -Seed 'n'
                $r2=Invoke-ContentCollect $f.ContentConfig $n.Dir $f.ArtifactsRoot
                Assert ($r2.status -ceq 'failed' -and $r2.reasonCode -ceq 'series-retired' -and $null -eq $r2.stage) "collect new: $($r2.reasonCode)"
                Assert ((Get-Sent $f 'put') -eq 0 -and $f.Log.Count -eq 0) 'collect sent after retire'
                Assert-Unchanged $art (Get-FileMap $f.ArtifactsRoot) '' 'ArtifactsRoot';Assert-Unchanged $store (Get-FileMap $f.O['contents-v1']) '' 'content store'
                [IO.File]::WriteAllText((Get-RecordPath $f 'content' "$set~$tgt.json"),'{}')
                Assert ((Invoke-ContentCollect $f.ContentConfig $n.Dir $f.ArtifactsRoot).reasonCode -ceq 'retirement-unknown') 'collect with unknown record'
                Assert ($f.Log.Count -eq 0) 'collect sent with unknown record'
            }
            # AC11・AC12（Content）: 他のcontentSet・Build・Evidenceのkeyへ送らず、既存記録・ArtifactsRoot・builds-v1を変えない。
            'content-boundary'{
                $revs=New-Revs $f 3;$null=New-Revs $f 2 'other-set'
                $null=Get-BuildDirectory $repo ('9'*32) $true
                Write-Bytes ([IO.Path]::Combine($f.ArtifactsRoot,'content',$set,$tgt,$revs[0],'transport.json')) ([byte[]]@(1))
                $otherDry=ConvertTo-Json (Clean-Content $f -DryRun 'other-set') -Compress -Depth 8
                $expected=@($revs|ForEach-Object {Get-RevKeys2 $f $_})
                $store=Get-FileMap $f.O['contents-v1'];$art=Get-FileMap $f.ArtifactsRoot;$builds=Get-FileMap $f.O['builds-v1']
                $x=Retire-Content $f;$z=Clean-Content $f
                Assert ($x.reasonCode -ceq 'retired' -and $z.mode -ceq 'retirement' -and $z.requests.delete -eq 0) 'content retire sequence'
                $deletes=@($f.Log|Where-Object {$_.StartsWith('delete ',[StringComparison]::Ordinal)}|ForEach-Object {$_.Substring(7)})
                Assert ($deletes.Count -eq 9 -and @($deletes|Where-Object {$_ -cnotin $expected}).Count -eq 0) "content DELETE outside the series: $($deletes -join ',')"
                Assert (@($f.Log|Where-Object {$_ -like '*/other-set/*' -or $_ -like '*development/builds/*' -or $_ -like '*development/evidence/*'}).Count -eq 0) 'content other key touched'
                Assert ((ConvertTo-Json (Clean-Content $f -DryRun 'other-set') -Compress -Depth 8) -ceq $otherDry) 'other content series dry-run changed'
                Assert-Unchanged $store (Get-FileMap $f.O['contents-v1']) '\\deletion\\[0-9]+\.(intent|sent|committed|deleted)\.json$|\\cleanup\.lock$' 'content store'
                Assert-Unchanged $art (Get-FileMap $f.ArtifactsRoot) '' 'ArtifactsRoot';Assert-Unchanged $builds (Get-FileMap $f.O['builds-v1']) '' 'builds-v1'
            }
            # AC1・CLI: 実CLIのgrammar（1系列・--decision必須）、入力検査の順序、終了コード（0/1/2）を別processで通す。
            'cli-grammar'{
                $ids=New-Builds $f 2;$null=New-Builds $f 1 'probe-x';$revs=New-Revs $f 1
                $missing=[IO.Path]::Combine($f.Root,'missing.json')
                $code={param($cli)$r=Invoke-Child (Get-RetireChildArgs $f -Cli ($cli -join '|'));$j=Finish-Child $r;return @{Exit=$j.Exit;Out=$j.Output;Json=$(try{ConvertFrom-ChildJson $j}catch{$null})}}
                foreach($cli in @(
                    @('build','retire','--profile','osm','--config',$f.BuildConfig,'--project',$proj,'--target',$tgt,'--configuration','il2cpp-high'),
                    @('build','retire','--profile','osm','--config',$f.BuildConfig,'--project',$proj,'--target',$tgt,'--configuration','il2cpp-high','--decision',$d1,'--keep','5'),
                    @('build','retire','--profile','osm','--config',$f.BuildConfig,'--project',$proj,'--target',$tgt,'--configuration','il2cpp-high','--decision',$d1,'--dry-run','--dry-run'),
                    @('build','inspect','--profile','osm','--config',$f.BuildConfig,'--build-id',$ids[0],'--decision',$d1),
                    @('content','retire','--profile','osm','--config',$f.ContentConfig,'--content-set',$set,'--target',$tgt,'--artifacts-root',$f.ArtifactsRoot),
                    @('content','collect','--profile','osm','--config',$f.ContentConfig,'--source',$f.Root,'--dry-run'))){
                    $r=& $code $cli;Assert ($r.Exit -eq 1 -and $r.Out.Contains('unsupported-command')) "grammar accepted: $($cli -join ' ')"
                }
                # 指示ID・tokenはconfigを読む前に検査する（configが無くても理由はinvalid-decision／invalid-build-identity）。
                foreach($bad in @('bad decision','-x',('a'*129),'a;b')){
                    $r=& $code @('build','retire','--profile','osm','--config',$missing,'--project',$proj,'--target',$tgt,'--configuration','il2cpp-high','--decision',$bad)
                    Assert ($r.Exit -eq 1 -and $r.Json.reasonCode -ceq 'invalid-decision') "decision [$bad]: $($r.Out)"
                }
                $r=& $code @('build','retire','--profile','osm','--config',$missing,'--project','Bad','--target',$tgt,'--configuration','il2cpp-high','--decision',$d1)
                Assert ($r.Exit -eq 1 -and $r.Json.reasonCode -ceq 'invalid-build-identity') 'invalid token'
                $r=& $code @('content','retire','--profile','osm','--config',$missing,'--content-set',$set,'--target','linux-player','--decision',$d1,'--artifacts-root',$f.ArtifactsRoot)
                Assert ($r.Exit -eq 1 -and $r.Json.reasonCode -ceq 'unsupported-series') 'content target'
                $r=& $code @('content','retire','--profile','osm','--config',$missing,'--content-set',$set,'--target',$tgt,'--decision','bad decision','--artifacts-root',$f.ArtifactsRoot)
                Assert ($r.Exit -eq 1 -and $r.Json.reasonCode -ceq 'invalid-decision') 'content decision'
                $r=& $code @('content','retire','--profile','osm','--config',$f.ContentConfig,'--content-set',$set,'--target',$tgt,'--decision',$d1)
                Assert ($r.Exit -eq 1 -and $r.Json.reasonCode -ceq 'artifacts-root-not-configured') 'content without ArtifactsRoot'
                Assert ($f.Log.Count -eq 0 -and -not [IO.Directory]::Exists($f.O['retirements-v1'])) 'rejected input touched records or network'
                # dry-runはexit 0で記録を書かない。実行はretiredでexit 0、403はpartialでexit 2、不明はblockedでexit 1。
                $r=& $code @('build','retire','--profile','osm','--config',$f.BuildConfig,'--project',$proj,'--target',$tgt,'--configuration','il2cpp-high','--decision',$d1,'--dry-run')
                Assert ($r.Exit -eq 0 -and $r.Json.mode -ceq 'retirement' -and $r.Json.candidates.count -eq 2 -and -not [IO.File]::Exists((Get-BuildRecord $f))) "dry-run CLI: $($r.Out)"
                $r=& $code @('build','retire','--profile','osm','--config',$f.BuildConfig,'--project',$proj,'--target',$tgt,'--configuration','il2cpp-high','--decision',$d1)
                Assert ($r.Exit -eq 0 -and $r.Json.reasonCode -ceq 'retired' -and $r.Json.retirement.decision -ceq $d1) "retire CLI: $($r.Out)"
                $j=Finish-Child (Invoke-Child (Get-RetireChildArgs $f -Cli (Get-BuildCli $f 'retire' 'probe-x' @('--decision',$d1)) -NetworkMode 'forbidden'))
                Assert ($j.Exit -eq 2 -and (ConvertFrom-ChildJson $j).status -ceq 'partial') "partial CLI: $($j.Output)"
                $r=& $code @('content','retire','--profile','osm','--config',$f.ContentConfig,'--content-set',$set,'--target',$tgt,'--decision',$d1,'--artifacts-root',$f.ArtifactsRoot)
                Assert ($r.Exit -eq 0 -and $r.Json.reasonCode -ceq 'retired') "content retire CLI: $($r.Out)"
                Edit-Json (Get-BuildRecord $f) {param($v)$v.kind='x'}
                $r=& $code @('build','cleanup','--profile','osm','--config',$f.BuildConfig,'--project',$proj,'--target',$tgt,'--configuration','il2cpp-high')
                Assert ($r.Exit -eq 1 -and $r.Json.status -ceq 'blocked' -and $r.Json.reasonCode -ceq 'retirement-unknown') "blocked CLI: $($r.Out)"
                Assert (-not $r.Out.Contains($f.Root) -and -not $r.Out.Contains('https://')) 'CLI output leaked'
            }
        }
    }catch{$failed.Add($caseName+': '+$_.Exception.GetType().Name+': '+$_.Exception.Message)}
    finally{
        $executed.Add($caseName)
        foreach($m in @('BuildCleanup','ContentCleanup')){foreach($n in @('NetworkHook','BudgetHook','StepHook')){Set-EvidenceTestValue $m $n $null}}
        Set-EvidenceTestValue BuildCleanup DeleteLimit 20;Set-EvidenceTestValue SeriesRetirementStore FaultHook $null
        foreach($n in @('NetworkHook','ReadbackHook','FaultHook','FailureHook')){Set-EvidenceTestValue BuildApplication $n $null}
        Set-EvidenceTestValue BuildStore FaultHook $null;Set-EvidenceTestValue SeriesRetirementStore TestRoot $null
        Clear-ContentFixture $null
        if($fixture){$target=[IO.Path]::GetFullPath($fixture.Root);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath());if(-not $target.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($target) -cnotmatch '\Aosm-retire-test-[0-9a-f]{32}\z'){throw 'unsafe-test-cleanup'};if([IO.Directory]::Exists($target)){[IO.Directory]::Delete($target,$true)}}
    }
}
[Environment]::SetEnvironmentVariable('OSM_ARTIFACTS_ROOT',$savedRoot)
if($selected.Count -eq 0 -or $selected.Count -ne $executed.Count){$failed.Add('selection')}
if($ResultPath){Write-EvidenceTestJson $ResultPath ([ordered]@{registered=$registered;selected=$selected;executed=@($executed);failed=@($failed);binaries=@(Get-EvidenceTestBinaries)})}
foreach($item in $failed){[Console]::Error.WriteLine($item)}
[Console]::WriteLine("SeriesRetirement $($executed.Count), failed $($failed.Count)")
if($failed.Count){exit 1}
