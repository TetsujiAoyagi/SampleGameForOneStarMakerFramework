param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../ContentCleanup.psm1')
Import-Module (Join-Path $PSScriptRoot '../ContentApplication.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactApplication.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactDeletion.psm1')
Import-Module (Join-Path $PSScriptRoot '../ContentContract.psm1')
Import-Module (Join-Path $PSScriptRoot '../ContentStore.psm1')
Import-Module (Join-Path $PSScriptRoot '../ContentDeletionStore.psm1')
Import-Module (Join-Path $PSScriptRoot '../ContentProtection.psm1')
Import-Module (Join-Path $PSScriptRoot '../BuildStore.psm1')
Import-Module (Join-Path $PSScriptRoot '../BuildStage.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactPaths.psm1')
Import-Module (Join-Path $PSScriptRoot '../Transport/R2ArtifactTransport.psm1')
. (Join-Path $PSScriptRoot 'ContentTestSupport.ps1')
# Content件数清掃（catalog・保護・削除試行の状態遷移・照会・排他・transport入口・CLI）をofflineで検証する。
# 成功記録はcollectと同じ関数で作る非秘密の合成receiptで、R2はremote directory、時計・通信・予算・N・上限は注入する。
# builds-v1・contents-v1・ArtifactsRootは全てfixture内の注入rootで、実storeに触れない。
# 並行は別processをsignalで同期し、時間待機を使わない。実R2での実証は判定Cの別証拠として扱う。
$registered=@('record-kinds','catalog-classification','catalog-blocks','catalog-order','protection-sources','protection-blocks','protected-over-limit',
    'dry-run-no-writes','delete-oldest-first','delete-failures-pending','get-unconfirmed','resume-abandon-recompute','after-commit',
    'record-write-failures','budget-points','delete-limit','lock-contention','revision-busy','reevaluate-after-lock','inspect-collect-deleted',
    'transport-allow-table','cli-grammar','publish-lock-missing')
$selected=@(if($Case -ceq '*'){$registered}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
$repo='a'*64;$set='bs4-spring-full';$tgt='windows-x64-player'
$baseNow=[DateTimeOffset]::new(2026,10,11,0,0,0,[TimeSpan]::Zero)
$savedRoot=[Environment]::GetEnvironmentVariable('OSM_ARTIFACTS_ROOT')
function Assert([bool]$Condition,[string]$Message){Assert-EvidenceTest $Condition $Message}
function global:New-Response([string]$Op,[string]$Gen,$Http,[string]$Class,$S3=$null,[bool]$Observed=$true,[bool]$TimedOut=$false,[bool]$Redirected=$false){
    [pscustomobject]@{Operation=$Op;Generation=$Gen;HttpStatus=$Http;StatusClass=$Class;S3Code=$S3;Bytes=$null;Sha256=$null;StatusObserved=$Observed;TimedOut=$TimedOut;Redirected=$Redirected}
}
function global:Get-CleanupRemotePath([string]$Remote,[string]$Key){[IO.Path]::Combine($Remote,[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Key))).ToLowerInvariant()+'.bin')}
function New-CleanupFixture {
    $fx=New-ContentFixture
    # BuildStoreの注入rootを先に向ける（保護源 (a) の builds-v1 を実storeから読まない）。
    $fx.Builds=$fx.Owned[6];Set-EvidenceTestValue BuildStore TestRoot $fx.Builds
    $fx.CleanNow=$baseNow;$fx.Hook=$null
    $fx.CleanCounts=[Collections.Concurrent.ConcurrentDictionary[string,int]]::new();$fx.Log=[Collections.Generic.List[string]]::new()
    $network={param($gen,$request)
        $fx.CleanCounts.AddOrUpdate($request.Operation+' '+$request.Key,1,{param($k,$v)$v+1})|Out-Null
        $fx.Log.Add($request.Operation+' '+$request.Key)
        # 実transportと同じ入口判定を通す。印の無いContent DELETEや他種別のkeyはここで拒否される。
        if(-not (Test-R2ArtifactRequestAllowed $request)){throw 'transport-rejected'}
        $path=Get-CleanupRemotePath $fx.Remote $request.Key
        if($fx.Hook){$custom=& $fx.Hook $gen $request $path;if($null -ne $custom){return $custom}}
        $status=200;$class='success';$code=$null;$bytes=$null
        if($request.Operation -ceq 'delete'){[IO.File]::Delete($path);$status=204}
        elseif($request.Operation -ceq 'get'){
            if([IO.File]::Exists($path)){$data=[IO.File]::ReadAllBytes($path);[IO.File]::WriteAllBytes($request.DestinationPath,$data);$bytes=[long]$data.Length}
            else{$status=404;$class='not-found';$code='NoSuchKey'}
        }else{throw 'offline-put-forbidden'}
        [pscustomobject]@{Operation=$request.Operation;Generation=$gen;HttpStatus=$status;StatusClass=$class;S3Code=$code;Bytes=$bytes;Sha256=$null;StatusObserved=$true;TimedOut=$false;Redirected=$false}
    }.GetNewClosure()
    Set-EvidenceTestValue ContentCleanup NetworkHook $network
    Set-EvidenceTestValue ContentCleanup Clock ({$fx.CleanNow}.GetNewClosure())
    foreach($m in @(Get-EvidenceTestModules ContentCleanup)){& $m {function script:Get-CredentialStatus {param($profile)[pscustomobject]@{Generation=('f'*32)}}}}
    Import-ArtifactPackage
    $fx.ConfigData=Read-ContentConfig $fx.Config
    return $fx
}
# collectと同じ関数（manifest検査・intent・receipt）で成功記録を作り、R2側のobjectも置く。
function New-Revision($Fx,[DateTimeOffset]$PublishedAt,[string]$Revision='',[int]$Files=1,[switch]$IntentOnly,[switch]$Bare){
    if(-not $Revision){$Revision=$PublishedAt.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff",[Globalization.CultureInfo]::InvariantCulture)+'Z-'+[Guid]::NewGuid().ToString('N')}
    $items=@(1..$Files|ForEach-Object {[pscustomobject]@{path="f$_.cf";data=[Text.Encoding]::UTF8.GetBytes("payload-$Revision-$_")}})
    $manifest=[ordered]@{version=1;contentSet=$set;revision=$Revision;target='StandaloneWindows64-Player';files=@($items|ForEach-Object {[ordered]@{path=$_.path;size=$_.data.Length;sha256=(Get-Sha $_.data)}})}
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $manifest -Depth 8))
    $m=ConvertFrom-ContentManifest $bytes
    $dir=Get-ContentDirectory $repo $set $tgt $Revision $true
    if($Bare){return $Revision}
    Set-Remote $Fx (Get-ContentKey $Fx.ConfigData $set $tgt $Revision) $bytes
    foreach($i in $items){Set-Remote $Fx (Get-ContentKey $Fx.ConfigData $set $tgt $Revision $i.path) $i.data}
    $intent=New-ContentIntent $Fx.ConfigData $m;$sha=Set-ContentIntent $dir $intent
    $l=Enter-ContentPublishLock $dir;$l.Dispose()
    if($IntentOnly){return $Revision}
    Write-ContentReceipt $dir (New-ContentReceipt $intent $sha $PublishedAt.ToString('o'))
    return $Revision
}
# 新しい順にcount件。最初が最新、最後が最古。
function New-Series($Fx,[int]$Count,[int]$Files=1){@(for($i=0;$i -lt $Count;$i++){New-Revision $Fx ($Fx.CleanNow.AddMinutes(-(1+$i))) -Files $Files})}
function global:Get-RevDir([string]$Rev){Get-ContentDirectory ('a'*64) 'bs4-spring-full' 'windows-x64-player' $Rev}
function Get-RevKeys($Fx,[string]$Rev){
    $i=Get-Content -LiteralPath ([IO.Path]::Combine((Get-RevDir $Rev),'intent.json')) -Raw|ConvertFrom-Json -Depth 8 -DateKind String
    return @(@(Get-ContentKey $Fx.ConfigData $set $tgt $Rev)+@($i.entries|ForEach-Object {Get-ContentKey $Fx.ConfigData $set $tgt $Rev $_.path}))
}
function Get-RemoteLeft($Fx,[string]$Rev){@(Get-RevKeys $Fx $Rev|Where-Object {[IO.File]::Exists((Get-CleanupRemotePath $Fx.Remote $_))}).Count}
function Get-DeletionFiles([string]$Rev){$d=[IO.Path]::Combine((Get-RevDir $Rev),'deletion');if(-not [IO.Directory]::Exists($d)){return ''};(@([IO.Directory]::EnumerateFiles($d)|ForEach-Object {[IO.Path]::GetFileName($_)}|Where-Object {$_ -notlike '*.tmp'}|Sort-Object) -join ',')}
function Get-CleanCount($Fx,[string]$Op,[string]$Prefix=''){[int](@($Fx.CleanCounts.GetEnumerator()|Where-Object {$_.Key.StartsWith("$Op $Prefix",[StringComparison]::Ordinal)}|ForEach-Object {$_.Value}|Measure-Object -Sum).Sum)}
function Invoke-Cleanup($Fx,[switch]$DryRun,$Root=$null,[string]$ContentSet='bs4-spring-full',[string]$Target='windows-x64-player'){if($null -eq $Root){$Root=$Fx.ArtifactsRoot};Invoke-ContentCleanup $Fx.Config $ContentSet $Target $Root ([bool]$DryRun)}
function Reset-Store($Fx,[string]$Name){$Fx.Store=[IO.Path]::Combine($Fx.Root,'store-'+$Name);Set-EvidenceTestValue ContentStore TestRoot $Fx.Store}
function Get-Tree([string]$Root,[string[]]$Skip=@()){
    if(-not [IO.Directory]::Exists($Root)){return 'absent'}
    $items=@([IO.Directory]::EnumerateFiles($Root,'*',[IO.SearchOption]::AllDirectories)|ForEach-Object {[IO.Path]::GetRelativePath($Root,$_)}|Where-Object {$n=$_;-not @($Skip|Where-Object {$n -like $_}).Count}|Sort-Object|ForEach-Object {$_+'|'+(Get-FileHash -LiteralPath ([IO.Path]::Combine($Root,$_))).Hash})
    return ($items -join "`n")
}
# 保護源: (a) builds-v1 のBuild directory、(b) ArtifactsRoot の退避済みPlayer（またはstaging）。名前はrevisionの末尾32 hex。
function global:Add-BuildFor([string]$Rev){$null=Get-BuildDirectory ('a'*64) $Rev.Substring($Rev.Length-32) $true}
function global:Add-PlayerFor($Fx,[string]$Rev,[switch]$Staging){
    $id=$Rev.Substring($Rev.Length-32);$name=if($Staging){".$id.$([Guid]::NewGuid().ToString('N')).staging"}else{$id}
    [IO.Directory]::CreateDirectory([IO.Path]::Combine($Fx.ArtifactsRoot,'players','samplegame','windows-x64-player','il2cpp-high',$name))|Out-Null
}
# 形とhashの結び付きは正しいContentの削除試行を直接書く（記録の破損・途中停止の代わり）。
function Add-FakeAttempt($Fx,[string]$Rev,[string]$Close,$Http=404,[string]$Class='not-found',$S3='NoSuchKey',[hashtable]$Override=@{}){
    $dir=Get-RevDir $Rev;$receipt=Get-Content -LiteralPath ([IO.Path]::Combine($dir,'receipt.json')) -Raw|ConvertFrom-Json -DateKind String;$keys=Get-RevKeys $Fx $Rev
    $intent=[ordered]@{schemaVersion=1;attempt=1;revision=$Rev;manifestSha256=$receipt.manifestSha256;publishIntentSha256=$receipt.intentSha256;keys=$keys;intentAt=$baseNow.ToString('o')};foreach($k in $Override.Keys){$intent[$k]=$Override[$k]}
    $isha=(Write-ContentDeletionRecord $dir 'deletion/1.intent.json' $intent).Sha256
    $get=[ordered]@{operation='get';key=$keys[0];generation=('f'*32);httpStatus=$Http;statusClass=$Class;s3Code=$S3;statusObserved=$true;timedOut=$false;redirected=$false;observedAt=$baseNow.ToString('o')}
    $committed=$null
    if($Close -cin @('committed','deleted')){$committed=(Write-ContentDeletionRecord $dir 'deletion/1.committed.json' ([ordered]@{schemaVersion=1;attempt=1;intentSha256=$isha;sentSha256=$null;deleteObservation='unknown';getObservation=$get})).Sha256}
    if($Close -ceq 'abandoned'){$null=Write-ContentDeletionRecord $dir 'deletion/1.abandoned.json' ([ordered]@{schemaVersion=1;attempt=1;intentSha256=$isha;getObservation=$get})}
    # deletedは2件目以降のkeyのうち最初の1件だけを観測した記録（全keyの404が揃わない）。
    if($Close -ceq 'deleted'){$o=[ordered]@{operation='get';key=$keys[1];generation=('f'*32);httpStatus=404;statusClass='not-found';s3Code='NoSuchKey';statusObserved=$true;timedOut=$false;redirected=$false;observedAt=$baseNow.ToString('o')};$null=Write-ContentDeletionRecord $dir 'deletion/1.deleted.json' ([ordered]@{schemaVersion=1;attempt=1;committedSha256=$committed;observations=@($o)})}
}
function Read-Record([string]$Rev,[string]$Name){Get-Content -LiteralPath ([IO.Path]::Combine((Get-RevDir $Rev),'deletion',$Name)) -Raw|ConvertFrom-Json -Depth 8 -DateKind String}
function Get-ChildArgs2($Fx,[string]$Cli='',[string]$Hold='',[int]$KeepCount=10,[string]$NetworkMode='normal',[string]$Barrier='',[string]$Ready='',[string]$Go=''){
    return @('-NoProfile','-File',[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'ContentCleanupChild.ps1')),'-CliArgs',$Cli,'-HoldDirectory',$Hold,'-StoreRoot',$Fx.Owned[1],'-BuildRoot',$Fx.Builds,'-TransferRoot',$Fx.Owned[0],'-DeploymentRoot',$Fx.Owned[2],'-RemoteRoot',$Fx.Remote,'-OwnedRoots',($Fx.Owned -join '|'),'-Now',$Fx.CleanNow.ToString('o'),'-KeepCount',[string]$KeepCount,'-NetworkMode',$NetworkMode,'-Barrier',$Barrier,'-ReadyEvent',$Ready,'-GoEvent',$Go)
}
function Get-CleanupCli($Fx,[switch]$DryRun,[string]$ContentSet='bs4-spring-full',[string]$Target='windows-x64-player',[switch]$NoRoot){
    $a=@('content','cleanup','--profile','osm','--config',$Fx.Config,'--content-set',$ContentSet,'--target',$Target);if(-not $NoRoot){$a+=@('--artifacts-root',$Fx.ArtifactsRoot)};if($DryRun){$a+='--dry-run'};return ($a -join '|')
}
function ConvertFrom-ChildJson($Run){$line=@($Run.Output.Trim() -split "`r?`n"|Where-Object {$_ -ne ''})[-1];return $line|ConvertFrom-Json -Depth 20 -DateKind String}
function New-Barrier{$n='osm-content-cleanup-'+[Guid]::NewGuid().ToString('N');return @{ReadyName=$n+'-ready';GoName=$n+'-go';Ready=[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,$n+'-ready');Go=[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,$n+'-go')}}
function Wait-Ready($Barrier,$Job){if(-not $Barrier.Ready.WaitOne(60000)){try{$Job.Process.Kill($true)}catch{};throw 'child-not-ready'}}
function Get-PublicLeak($Result,$Fx){$json=ConvertTo-Json -InputObject $Result -Compress -Depth 20;return $json.Contains($Fx.Root) -or $json.Contains('https://') -or $json.Contains('r2.cloudflarestorage')}

foreach($caseName in $selected){
    $fixture=$null
    try{
        Assert ($caseName -cin $registered) 'unknown-case'
        [Environment]::SetEnvironmentVariable('OSM_ARTIFACTS_ROOT',$null)
        $fixture=New-CleanupFixture;$f=$fixture
        switch -CaseSensitive ($caseName){
            # 記録の種別ごとの許可名（Buildにcommittedは書けない）と、観測の写像・検査の表。
            'record-kinds'{
                $rev=New-Revision $f $baseNow.AddMinutes(-1);$dir=Get-RevDir $rev
                $kind={param($name,$k)try{$null=Write-BuildImmutable $dir $name ([ordered]@{x=1}) $null $k;'ok'}catch{$_.Exception.Message}}
                Assert ((& $kind 'deletion/1.committed.json' 'build') -ceq 'build-store-unavailable') 'Build kind accepted committed'
                Assert ((& $kind "protections/$('0'*32).use.json" 'content') -ceq 'build-store-unavailable') 'Content kind accepted protections'
                Assert ((& $kind 'deletion/1.intent.json' 'other') -ceq 'build-store-unavailable') 'unknown kind accepted'
                Assert ((& $kind 'deletion/1.committed.json' 'content') -ceq 'ok') 'Content kind rejected committed'
                foreach($n in @('intent','sent','deleted','abandoned')){Assert ((& $kind "deletion/7.$n.json" 'build') -ceq 'ok') "Build kind lost $n"}
                $thrown=$null;try{$null=Write-ContentDeletionRecord $dir 'receipt.json' @{x=1}}catch{$thrown=$_.Exception.Message};Assert ($thrown -ceq 'content-store-unavailable') 'deletion writer accepted receipt'
                $g='f'*32;$at=$baseNow.ToString('o')
                $rows=@(
                    @('missing',(New-Response 'get' $g 404 'not-found' 'NoSuchKey'),'get','missing'),
                    @('present',(New-Response 'get' $g 200 'success'),'get','present'),
                    @('op-mismatch',(New-Response 'delete' $g 404 'not-found' 'NoSuchKey'),'get','unobserved'),
                    @('gen-mismatch',(New-Response 'get' ('e'*32) 404 'not-found' 'NoSuchKey'),'get','unobserved'),
                    @('http-string',(New-Response 'get' $g '404' 'not-found' 'NoSuchKey'),'get','unobserved'),
                    @('bad-class',(New-Response 'get' $g 404 'gone' 'NoSuchKey'),'get','unobserved'),
                    @('flag-string',(New-Response 'get' $g 404 'not-found' 'NoSuchKey' $true $false $false|ForEach-Object {$_.TimedOut='no';$_}),'get','unobserved'),
                    @('null',$null,'get','unobserved')
                )
                foreach($row in $rows){
                    $o=ConvertTo-ArtifactObservation $row[1] $row[2] 'k' $g $at;$seen=ConvertFrom-ArtifactObservation $o
                    $actual=if(Test-ArtifactMissing $seen $g){'missing'}elseif(Test-ArtifactSuccess $seen 'get' $g){'present'}else{'unobserved'}
                    Assert ($actual -ceq $row[3] -and $o.observedAt -ceq $at) "observation table: $($row[0]) $actual"
                    # 記録を読み戻した形でも同じ判定になる（JSON往復で型が変わらない）。
                    $back=(ConvertTo-Json -InputObject $o -Compress)|ConvertFrom-Json -DateKind String
                    $ok={param($out)try{Assert-ArtifactObservation $back 'get' 'k' $out;$true}catch{$false}}
                    Assert ((& $ok 'missing') -eq ($row[3] -ceq 'missing') -and (& $ok 'present') -eq ($row[3] -ceq 'present') -and (& $ok 'any')) "record check: $($row[0])"
                }
                $o=(ConvertTo-Json -InputObject (ConvertTo-ArtifactObservation (New-Response 'get' $g 404 'not-found' 'NoSuchKey') 'get' 'k' $g 'not-a-time') -Compress)|ConvertFrom-Json -DateKind String
                $thrown=$false;try{Assert-ArtifactObservation $o 'get' 'k'}catch{$thrown=$true};Assert $thrown 'observedAt not checked'
            }
            # AC2・AC11: 成功／未完了／削除中（committedだけ）／削除済み／取下げだけの分類と、系列directory無し。
            'catalog-classification'{
                $revs=New-Series $f 4
                Set-EvidenceTestValue ContentCleanup KeepCount 3
                $x=Invoke-Cleanup $f;Assert ($x.status -ceq 'passed' -and $x.deleted.revisions[0] -ceq $revs[3]) "first delete: $($x.reasonCode)"
                Add-FakeAttempt $f $revs[2] 'committed'
                Add-FakeAttempt $f $revs[1] 'abandoned' 200 'success' $null
                $incomplete=New-Revision $f $f.CleanNow -IntentOnly;$bare=New-Revision $f $f.CleanNow -Bare
                $d=Invoke-Cleanup $f -DryRun
                Assert ($d.status -ceq 'passed' -and $d.reasonCode -ceq 'within-limit' -and $d.keepCount -eq 3) "dry-run: $($d.reasonCode)"
                Assert ((@($d.retained.revisions) -join ',') -ceq "$($revs[0]),$($revs[1])") 'retained (abandoned-only returns to rows)'
                Assert ($d.held.count -eq 2 -and @($d.held.revisions) -contains $incomplete -and @($d.held.revisions) -contains $bare) 'incomplete not held'
                Assert ($d.deleting.count -eq 1 -and $d.deleting.revisions[0] -ceq $revs[2] -and $d.deleted.count -eq 1 -and $d.deleted.revisions[0] -ceq $revs[3]) 'deleting/deleted'
                Set-EvidenceTestValue ContentCleanup KeepCount 1
                $d=Invoke-Cleanup $f -DryRun
                Assert ($d.candidates.count -eq 1 -and $d.candidates.revisions[0] -ceq $revs[1] -and $d.reasonCode -ceq 'candidates-found') 'deleting counted as success'
                $none=Invoke-Cleanup $f -DryRun -ContentSet 'other-set'
                Assert ($none.status -ceq 'passed' -and $none.reasonCode -ceq 'within-limit' -and $none.retained.count -eq 0) 'absent series dry-run'
                $none=Invoke-Cleanup $f -ContentSet 'other-set'
                Assert ($none.status -ceq 'passed' -and $none.reasonCode -ceq 'within-limit' -and -not [IO.Directory]::Exists([IO.Path]::Combine($f.Owned[1],$repo,'other-set'))) 'absent series run created store'
            }
            # AC2・AC3: 系列の記録を読めない・全件を見たと言えないときは、何も書かず・送らずにblocked。
            'catalog-blocks'{
                $variants=[ordered]@{
                    'stray-file-series'={param($r,$d,$t)[IO.File]::WriteAllText([IO.Path]::Combine($t,'stray.txt'),'x')}
                    'stray-dir-series'={param($r,$d,$t)$p=[IO.Path]::Combine($t,'other');[IO.Directory]::CreateDirectory($p)|Out-Null;Set-ArtifactAcl $p $true}
                    'junction-revision'={param($r,$d,$t)$o=[IO.Path]::Combine($f.Root,'outside-'+[Guid]::NewGuid().ToString('N'));[IO.Directory]::CreateDirectory($o)|Out-Null;$null=New-Item -ItemType Junction -Path ([IO.Path]::Combine($t,'20260101T000000000Z-'+('8'*32))) -Target $o}
                    'acl-revision'={param($r,$d,$t)[IO.Directory]::CreateDirectory([IO.Path]::Combine($t,'20260101T000000000Z-'+('7'*32)))|Out-Null}
                    'stray-in-revision'={param($r,$d,$t)$p=[IO.Path]::Combine($d,'extra.json');[IO.File]::WriteAllText($p,'{}');Set-ArtifactAcl $p $false}
                    'receipt-unparsable'={param($r,$d,$t)$p=[IO.Path]::Combine($d,'receipt.json');[IO.File]::WriteAllText($p,'{');Set-ArtifactAcl $p $false}
                    'intent-mismatch'={param($r,$d,$t)$p=[IO.Path]::Combine($d,'intent.json');[IO.File]::WriteAllText($p,([IO.File]::ReadAllText($p)).Replace('"objectCount":3','"objectCount":4'));Set-ArtifactAcl $p $false}
                    'lock-acl'={param($r,$d,$t)[IO.File]::WriteAllText([IO.Path]::Combine($t,'cleanup.lock'),'')}
                    'deletion-stray'={param($r,$d,$t)$p=[IO.Path]::Combine($d,'deletion');[IO.Directory]::CreateDirectory($p)|Out-Null;Set-ArtifactAcl $p $true;$q=[IO.Path]::Combine($p,'stray.json');[IO.File]::WriteAllText($q,'{}');Set-ArtifactAcl $q $false}
                    'deletion-other-manifest'={param($r,$d,$t)Add-FakeAttempt $f $r 'none' -Override @{manifestSha256=('0'*64)}}
                    'deletion-other-keys'={param($r,$d,$t)$k=Get-RevKeys $f $r;Add-FakeAttempt $f $r 'none' -Override @{keys=@($k[0],($k[1]+'x'))}}
                    'committed-with-200'={param($r,$d,$t)Add-FakeAttempt $f $r 'committed' 200 'success' $null}
                    'abandoned-with-404'={param($r,$d,$t)Add-FakeAttempt $f $r 'abandoned'}
                    'deleted-missing-key'={param($r,$d,$t)Add-FakeAttempt $f $r 'deleted'}
                    'clock-skew'={param($r,$d,$t)$null=New-Revision $f $f.CleanNow.AddHours(1)}
                }
                Set-EvidenceTestValue ContentCleanup KeepCount 1
                foreach($name in $variants.Keys){
                    Reset-Store $f $name;$f.CleanCounts.Clear()
                    $good=New-Series $f 2 2;$bad=New-Revision $f $f.CleanNow.AddMinutes(-10) -Files 2
                    $dir=Get-RevDir $bad;$series=[IO.Path]::GetDirectoryName($dir)
                    $null=& $variants[$name] $bad $dir $series
                    $expected=if($name -ceq 'clock-skew'){'clock-skew'}elseif($name -cmatch '\A(deletion-|committed-|abandoned-|deleted-)'){'delete-record-invalid'}else{'catalog-incomplete'}
                    $before=Get-Tree $f.Store @('*cleanup.lock')
                    $d=Invoke-Cleanup $f -DryRun
                    Assert ($d.status -ceq 'blocked' -and $d.reasonCode -ceq $expected -and $d.candidates.count -eq 0) "dry-run not blocked: $name $($d.reasonCode)"
                    $x=Invoke-Cleanup $f
                    # 系列lockのACLが不一致ならlockを取れず、読む前に失敗する（何も送らない点は同じ）。
                    if($name -ceq 'lock-acl'){Assert ($x.status -cin @('blocked','failed')) "lock-acl status: $($x.status)"}
                    else{Assert ($x.status -ceq 'blocked' -and $x.reasonCode -ceq $expected) "execution not blocked: $name $($x.reasonCode)"}
                    Assert ($f.CleanCounts.Count -eq 0 -and (Get-RemoteLeft $f $good[1]) -eq 3 -and (Get-DeletionFiles $good[1]) -ceq '') "blocked series sent or wrote: $name"
                    Assert ((Get-Tree $f.Store @('*cleanup.lock')) -ceq $before) "blocked series changed records: $name"
                    if($name -ceq 'junction-revision'){[IO.Directory]::Delete([IO.Path]::Combine($series,'20260101T000000000Z-'+('8'*32)))}
                }
            }
            # AC3: 「最新」はpublishedAtの降順、同時刻はrevisionのOrdinal昇順を新しい側とし、候補はその逆順。
            'catalog-order'{
                $t=$f.CleanNow.AddMinutes(-5);$stamp=$t.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff",[Globalization.CultureInfo]::InvariantCulture)+'Z-'
                $low=New-Revision $f $t ($stamp+('1'*32));$high=New-Revision $f $t ($stamp+('2'*32));$older=New-Revision $f $t.AddMinutes(-1)
                Set-EvidenceTestValue ContentCleanup KeepCount 1
                $d=Invoke-Cleanup $f -DryRun
                Assert ($d.retained.revisions[0] -ceq $low -and (@($d.candidates.revisions) -join ',') -ceq "$older,$high") "order: $(@($d.candidates.revisions) -join ',')"
                $x=Invoke-Cleanup $f
                Assert ((@($x.outcomes|ForEach-Object revision) -join ',') -ceq "$older,$high" -and (Get-RemoteLeft $f $low) -eq 2) 'deletion order'
            }
            # AC4: 保護源 (a)(b) の規則（削除済みBuildは除き、状態を読めないBuildは残す。stagingを含み、無関係な名前は無視）。
            'protection-sources'{
                $revs=New-Series $f 8
                Add-BuildFor $revs[1]
                # (a) .reserved だけのBuild。
                $id2=$revs[2].Substring($revs[2].Length-32);$res=[IO.Path]::Combine((Get-BuildRoot $true),$repo,$id2+'.reserved');[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($res))|Out-Null;[IO.File]::WriteAllText($res,'');Set-ArtifactAcl $res $false
                # (a) 削除済みと読めるBuildは保護しない。
                $id3=$revs[3].Substring($revs[3].Length-32);Add-BuildFor $revs[3];$bdir=Get-BuildDirectory $repo $id3
                $bkey="development/builds/v1/$repo/samplegame/windows-x64-player/il2cpp-high/$id3/bundle.zip"
                $bi=(Write-BuildImmutable $bdir 'deletion/1.intent.json' ([ordered]@{schemaVersion=1;attempt=1;buildId=$id3;key=$bkey;packageSha256=('1'*64);intentAt=$baseNow.ToString('o')})).Sha256
                $null=Write-BuildImmutable $bdir 'deletion/1.deleted.json' ([ordered]@{schemaVersion=1;attempt=1;intentSha256=$bi;sentSha256=$null;deleteObservation='unknown';getObservation=([ordered]@{operation='get';key=$bkey;generation=('f'*32);httpStatus=404;statusClass='not-found';s3Code='NoSuchKey';statusObserved=$true;timedOut=$false;redirected=$false;observedAt=$baseNow.ToString('o')})})
                # (a) 削除状態を読めないBuildは保護に残す。
                $id4=$revs[4].Substring($revs[4].Length-32);Add-BuildFor $revs[4];$dd=[IO.Path]::Combine((Get-BuildDirectory $repo $id4),'deletion');[IO.Directory]::CreateDirectory($dd)|Out-Null;Set-ArtifactAcl $dd $true;[IO.File]::WriteAllText([IO.Path]::Combine($dd,'stray.json'),'{}')
                # (b) 退避済みPlayer、staging。32 hexを取り出せない名前は寄与しない。
                Add-PlayerFor $f $revs[5];Add-PlayerFor $f $revs[6] -Staging
                $junk=[IO.Path]::Combine($f.ArtifactsRoot,'players','samplegame','windows-x64-player','il2cpp-high');[IO.File]::WriteAllText([IO.Path]::Combine($junk,'build-info.json'),'{}');[IO.Directory]::CreateDirectory([IO.Path]::Combine($junk,'.'+$revs[7].Substring($revs[7].Length-32)+'.tmp'))|Out-Null
                [IO.Directory]::CreateDirectory([IO.Path]::Combine($f.Builds,$repo,'locks'))|Out-Null
                $set1=Get-ContentProtectedBuildIds $repo $f.ArtifactsRoot
                $want=@(1,2,4,5,6|ForEach-Object {$revs[$_].Substring($revs[$_].Length-32)}|Sort-Object)
                Assert ((@($set1|Sort-Object) -join ',') -ceq ($want -join ',')) "protected set: $(@($set1|Sort-Object) -join ',')"
                Set-EvidenceTestValue ContentCleanup KeepCount 1
                $d=Invoke-Cleanup $f -DryRun
                Assert ($d.protected.count -eq 5 -and (@($d.candidates.revisions) -join ',') -ceq "$($revs[7]),$($revs[3]),$($revs[0])" -and $d.excessCount -eq 4 -and $d.reasonCode -ceq 'protected-over-limit') "dry-run protection: $(@($d.candidates.revisions) -join ',')"
                # builds-v1 のrepoと players/ が無いことは「保護なし」。
                Set-EvidenceTestValue BuildStore TestRoot ([IO.Path]::Combine($f.Root,'builds-empty'))
                [IO.Directory]::Delete([IO.Path]::Combine($f.ArtifactsRoot,'players'),$true)
                Assert ((Get-ContentProtectedBuildIds $repo $f.ArtifactsRoot).Count -eq 0) 'absent sources protected something'
                Set-EvidenceTestValue BuildStore TestRoot $f.Builds
            }
            # AC1・AC4: 保護源の列挙失敗・reparse は protection-unknown、ArtifactsRootが無ければ何も読まない。
            'protection-blocks'{
                $revs=New-Series $f 3;Set-EvidenceTestValue ContentCleanup KeepCount 1
                $outside=[IO.Path]::Combine($f.Root,'outside');[IO.Directory]::CreateDirectory($outside)|Out-Null
                $players=[IO.Path]::Combine($f.ArtifactsRoot,'players')
                $variants=[ordered]@{
                    'builds-repo-file'={$p=[IO.Path]::Combine((Get-BuildRoot $true),$repo);[IO.File]::WriteAllText($p,'x')}
                    'builds-root-acl'={Set-EvidenceTestValue BuildStore TestRoot ([IO.Path]::Combine($f.Root,'builds-noacl'));[IO.Directory]::CreateDirectory([IO.Path]::Combine($f.Root,'builds-noacl'))|Out-Null}
                    'players-file'={[IO.File]::WriteAllText($players,'x')}
                    'players-junction'={$null=New-Item -ItemType Junction -Path $players -Target $outside}
                    'project-junction'={[IO.Directory]::CreateDirectory($players)|Out-Null;$null=New-Item -ItemType Junction -Path ([IO.Path]::Combine($players,'samplegame')) -Target $outside}
                }
                foreach($name in $variants.Keys){
                    & $variants[$name]
                    $d=Invoke-Cleanup $f -DryRun;$x=Invoke-Cleanup $f
                    Assert ($d.status -ceq 'blocked' -and $d.reasonCode -ceq 'protection-unknown' -and $x.status -ceq 'blocked' -and $x.reasonCode -ceq 'protection-unknown' -and $f.CleanCounts.Count -eq 0) "not protection-unknown: $name $($x.reasonCode)"
                    Set-EvidenceTestValue BuildStore TestRoot $f.Builds
                    if([IO.File]::Exists([IO.Path]::Combine($f.Builds,$repo))){[IO.File]::Delete([IO.Path]::Combine($f.Builds,$repo))}
                    if([IO.File]::Exists($players)){[IO.File]::Delete($players)}elseif([IO.Directory]::Exists($players)){
                        $j=[IO.Path]::Combine($players,'samplegame');if([IO.Directory]::Exists($j)){[IO.Directory]::Delete($j)};[IO.Directory]::Delete($players)}
                }
                # ArtifactsRootが無い・不正なら、保護源を集められないので何も読まない（configのpathが不正でも先に止まる）。
                $missing=[IO.Path]::Combine($f.Root,'missing.json')
                $x=Invoke-ContentCleanup $missing $set $tgt '' $false
                Assert ($x.status -ceq 'failed' -and $x.reasonCode -ceq 'artifacts-root-not-configured' -and $f.CleanCounts.Count -eq 0) "unconfigured: $($x.reasonCode)"
                $x=Invoke-ContentCleanup $missing $set $tgt '' $true;Assert ($x.reasonCode -ceq 'artifacts-root-not-configured') 'unconfigured dry-run'
                $x=Invoke-ContentCleanup $f.Config $set $tgt ([IO.Path]::Combine($f.Root,'no-such-root')) $false;Assert ($x.reasonCode -ceq 'invalid-artifacts-root') 'missing root accepted'
                $x=Invoke-ContentCleanup $f.Config $set $tgt $f.Owned[1] $false;Assert ($x.reasonCode -ceq 'invalid-artifacts-root') 'owned store accepted as root'
                $x=Invoke-ContentCleanup $missing 'Bad' $tgt '' $false;Assert ($x.reasonCode -ceq 'invalid-content-identity') 'contentSet not checked first'
                $x=Invoke-ContentCleanup $missing $set 'linux' '' $false;Assert ($x.reasonCode -ceq 'unsupported-series') 'target not checked first'
                # ユーザー環境変数の値（processが引き継いだ値）を同じ規則で使う。
                [Environment]::SetEnvironmentVariable('OSM_ARTIFACTS_ROOT',$f.ArtifactsRoot)
                $x=Invoke-ContentCleanup $f.Config $set $tgt '' $true;Assert ($x.status -ceq 'passed' -and $x.candidates.count -eq 2) "environment root: $($x.reasonCode)"
                Assert ((Get-RemoteLeft $f $revs[2]) -eq 2) 'blocked run deleted'
            }
            # AC5・S2: 保護だけでNを超える間はexcessCountを出して正常終了し、保護を外さない。
            'protected-over-limit'{
                $revs=New-Series $f 4;Set-EvidenceTestValue ContentCleanup KeepCount 2
                Add-BuildFor $revs[0];Add-PlayerFor $f $revs[1];Add-PlayerFor $f $revs[2] -Staging
                $x=Invoke-Cleanup $f
                Assert ($x.status -ceq 'passed' -and $x.reasonCode -ceq 'protected-over-limit' -and $x.excessCount -eq 1 -and $x.retained.count -eq 3 -and $x.protected.count -eq 3) "over-limit: $($x.reasonCode)"
                Assert (@($revs[0..2]|Where-Object {(Get-RemoteLeft $f $_) -ne 2}).Count -eq 0 -and (Get-RemoteLeft $f $revs[3]) -eq 0 -and $x.outcomes[0].revision -ceq $revs[3]) 'protected deleted or unprotected kept'
            }
            # AC6: dry-runはlock・記録・networkに触れず、実行と同じ分類を返す。
            'dry-run-no-writes'{
                $revs=New-Series $f 3;Set-EvidenceTestValue ContentCleanup KeepCount 1
                $before=Get-Tree $f.Owned[1]
                $d=Invoke-Cleanup $f -DryRun
                Assert ($d.status -ceq 'passed' -and (@($d.candidates.revisions) -join ',') -ceq "$($revs[2]),$($revs[1])" -and $d.remaining -eq 2 -and $d.reasonCode -ceq 'candidates-found') 'dry-run classification'
                Assert ($d.requests.delete -eq 0 -and $d.requests.get -eq 0 -and $f.Log.Count -eq 0) 'dry-run used network'
                Assert ((Get-Tree $f.Owned[1]) -ceq $before -and -not [IO.File]::Exists([IO.Path]::Combine($f.Owned[1],$repo,$set,$tgt,'cleanup.lock'))) 'dry-run wrote store'
            }
            # S1・AC9・AC15・AC16: 既定N=10で最古1件の全objectだけを消し、記録から判定を再評価でき、再実行は送信0。
            'delete-oldest-first'{
                $revs=New-Series $f 11
                Add-BuildFor ('20260101T000000000Z-'+('3'*32));Add-PlayerFor $f ('20260101T000000000Z-'+('4'*32))
                $layers=@{contents=(Get-Tree $f.Owned[1] @('*\deletion\*','*cleanup.lock'));builds=(Get-Tree $f.Builds);artifacts=(Get-Tree $f.ArtifactsRoot)}
                $x=Invoke-Cleanup $f
                Assert ($x.status -ceq 'passed' -and $x.reasonCode -ceq 'within-limit' -and $x.keepCount -eq 10) "status: $($x.reasonCode)"
                Assert (@($x.outcomes).Count -eq 1 -and $x.outcomes[0].revision -ceq $revs[10] -and $x.outcomes[0].outcome -ceq 'deleted') 'oldest not deleted'
                Assert ($x.requests.delete -eq 2 -and $x.requests.get -eq 3 -and $x.deleted.count -eq 1 -and $x.deleted.revisions[0] -ceq $revs[10] -and $x.retained.count -eq 10) "counts: $($x.requests.delete)/$($x.requests.get)"
                $k=Get-RevKeys $f $revs[10]
                Assert (($f.Log -join "`n") -ceq (@("delete $($k[0])","get $($k[0])","get $($k[1])","delete $($k[1])","get $($k[1])") -join "`n")) "request order: $($f.Log -join ';')"
                Assert ((Get-RemoteLeft $f $revs[10]) -eq 0 -and @($revs[0..9]|Where-Object {(Get-RemoteLeft $f $_) -ne 2}).Count -eq 0) 'wrong remote objects'
                Assert ((Get-DeletionFiles $revs[10]) -ceq '1.committed.json,1.deleted.json,1.intent.json,1.sent.json') "deletion records: $(Get-DeletionFiles $revs[10])"
                # 記録だけからの再評価: 送信は検証済み2xx、確定点はGET 404、残りの各keyは最後の観測がGET 404。
                $sent=Read-Record $revs[10] '1.sent.json';$com=Read-Record $revs[10] '1.committed.json';$del=Read-Record $revs[10] '1.deleted.json';$g='f'*32
                Assert ((Test-ArtifactSuccess (ConvertFrom-ArtifactObservation $sent.observation) 'delete' $g) -and (Test-ArtifactMissing (ConvertFrom-ArtifactObservation $com.getObservation) $g) -and $com.deleteObservation -ceq 'recorded') 'records not re-evaluable'
                $obs=@($del.observations)
                Assert ($obs.Count -eq 3 -and (Test-ArtifactSuccess (ConvertFrom-ArtifactObservation $obs[0]) 'get' $g) -and (Test-ArtifactSuccess (ConvertFrom-ArtifactObservation $obs[1]) 'delete' $g) -and (Test-ArtifactMissing (ConvertFrom-ArtifactObservation $obs[2]) $g)) 'object observations'
                $fromRecords=@{delete=1+@($obs|Where-Object operation -CEQ 'delete').Count;get=1+@($obs|Where-Object operation -CEQ 'get').Count}
                Assert ($fromRecords.delete -eq $x.requests.delete -and $fromRecords.get -eq $x.requests.get) 'requests not reproducible from records'
                $again=Invoke-Cleanup $f
                Assert ($again.status -ceq 'passed' -and $again.requests.delete -eq 0 -and $again.requests.get -eq 0 -and $again.deleted.count -eq 1) 'rerun sent requests'
                Assert ((Get-CleanCount $f 'delete') -eq (Get-CleanCount $f 'delete' "development/content/v1/$repo/$set/$tgt/$($revs[10])/")) 'DELETE outside the candidate'
                Assert ((Get-Tree $f.Owned[1] @('*\deletion\*','*cleanup.lock')) -ceq $layers.contents -and (Get-Tree $f.Builds) -ceq $layers.builds -and (Get-Tree $f.ArtifactsRoot) -ceq $layers.artifacts) 'local records or other layers changed'
                $names=@($x.PSObject.Properties.Name|Sort-Object) -join ','
                Assert ($names -ceq 'candidates,deleted,deleting,excessCount,held,keepCount,outcomes,protected,reasonCode,remaining,requests,retained,seriesId,status') "result fields: $names"
                Assert (-not (Get-PublicLeak $x $f)) 'result leaked path or endpoint'
            }
            # AC10(a): transport.jsonのDELETEが検証済み2xxでなければ未解決で止め、後続を処理しない。次回はGETから。
            'delete-failures-pending'{
                $modes=[ordered]@{
                    '403'={param($g,$r,$p)New-Response 'delete' $g 403 'forbidden' 'AccessDenied'}
                    '401'={param($g,$r,$p)New-Response 'delete' $g 401 'unauthorized'}
                    '429'={param($g,$r,$p)New-Response 'delete' $g 429 'too-many-requests'}
                    '500'={param($g,$r,$p)New-Response 'delete' $g 500 'server-error'}
                    'timeout'={param($g,$r,$p)New-Response 'delete' $g $null 'timeout' $null $false $true}
                    'redirect'={param($g,$r,$p)New-Response 'delete' $g 301 'redirect' $null $true $false $true}
                    'generation'={param($g,$r,$p)New-Response 'delete' ('e'*32) 204 'success'}
                    'malformed'={param($g,$r,$p)New-Response 'get' $g 204 'success'}
                    'exception'={param($g,$r,$p)throw 'response-lost'}
                    'lost-after-delete'={param($g,$r,$p)[IO.File]::Delete($p);throw 'response-lost'}
                }
                Set-EvidenceTestValue ContentCleanup KeepCount 1
                foreach($mode in $modes.Keys){
                    Reset-Store $f $mode;$f.CleanCounts.Clear()
                    $revs=New-Series $f 3;$behavior=$modes[$mode]
                    $f.Hook={param($g,$r,$p)if($r.Operation -ceq 'delete'){& $behavior $g $r $p}}.GetNewClosure()
                    $x=Invoke-Cleanup $f
                    Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'delete-pending' -and @($x.outcomes).Count -eq 1 -and $x.outcomes[0].revision -ceq $revs[2] -and $x.outcomes[0].outcome -ceq 'delete-pending') "pending result: $mode $($x.reasonCode)"
                    Assert ($x.requests.delete -eq 1 -and $x.requests.get -eq 0 -and (Get-DeletionFiles $revs[1]) -ceq '') "continued after failure: $mode"
                    Assert ($x.deleting.count -eq 1 -and $x.deleting.revisions[0] -ceq $revs[2] -and $x.remaining -eq 1) "deleting not excluded from counts: $mode"
                    $expectSent=$mode -cnotin @('exception','lost-after-delete')
                    Assert (((Get-DeletionFiles $revs[2]) -ceq '1.intent.json,1.sent.json') -eq $expectSent) "sent record: $mode"
                    $f.Hook=$null
                    $y=Invoke-Cleanup $f
                    Assert ($y.status -ceq 'passed' -and $y.deleted.count -eq 2 -and (Get-RemoteLeft $f $revs[2]) -eq 0 -and (Get-RemoteLeft $f $revs[1]) -eq 0 -and (Get-RemoteLeft $f $revs[0]) -eq 2) "resume did not finish: $mode"
                    if($mode -ceq 'lost-after-delete'){
                        Assert ($y.outcomes[0].outcome -ceq 'deleted' -and $y.requests.delete -eq 3 -and (Get-DeletionFiles $revs[2]) -ceq '1.committed.json,1.deleted.json,1.intent.json') 'resume after lost response'
                        $c=Read-Record $revs[2] '1.committed.json';Assert ($c.deleteObservation -ceq 'unknown' -and $null -eq $c.sentSha256) 'unknown delete observation'
                    }else{
                        # transport.jsonが残っていた: 再開のGET 200で取下げ、今も候補なので新しい試行で消す。
                        Assert ($y.outcomes[0].outcome -ceq 'abandoned' -and $y.outcomes[1].revision -ceq $revs[2] -and $y.outcomes[1].outcome -ceq 'deleted' -and $y.requests.delete -eq 4) "resume sequence: $mode"
                        $first=if($expectSent){'1.abandoned.json,1.intent.json,1.sent.json'}else{'1.abandoned.json,1.intent.json'}
                        Assert ((Get-DeletionFiles $revs[2]) -ceq "$first,2.committed.json,2.deleted.json,2.intent.json,2.sent.json") "attempt records: $mode $(Get-DeletionFiles $revs[2])"
                    }
                }
            }
            # AC10(b)・手順6: 確認GETを判定できなければ未解決。DELETE 2xx後もtransport.jsonが残れば取下げ、同じrunで再試行しない。
            'get-unconfirmed'{
                Set-EvidenceTestValue ContentCleanup KeepCount 1
                foreach($v in @(@('get-500',{param($g,$r,$p)if($r.Operation -ceq 'get'){New-Response 'get' $g 500 'server-error'}}),@('get-generation',{param($g,$r,$p)if($r.Operation -ceq 'get'){New-Response 'get' ('e'*32) 200 'success'}}),@('get-timeout',{param($g,$r,$p)if($r.Operation -ceq 'get'){New-Response 'get' $g $null 'timeout' $null $false $true}}))){
                    Reset-Store $f $v[0];$revs=New-Series $f 3;$f.Hook=$v[1]
                    $x=Invoke-Cleanup $f;$f.Hook=$null
                    Assert ($x.status -ceq 'partial' -and $x.outcomes[0].outcome -ceq 'delete-pending' -and @($x.outcomes).Count -eq 1 -and (Get-DeletionFiles $revs[2]) -ceq '1.intent.json,1.sent.json') "unconfirmed GET: $($v[0])"
                }
                Reset-Store $f 'remains';$f.CleanCounts.Clear();$revs=New-Series $f 3;$keep=(Get-RevKeys $f $revs[2])[0]
                $f.Hook={param($g,$r,$p)if($r.Operation -ceq 'delete' -and $r.Key -ceq $keep){New-Response 'delete' $g 204 'success'}}.GetNewClosure()
                $x=Invoke-Cleanup $f;$f.Hook=$null
                Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'delete-unconfirmed' -and $x.outcomes[0].outcome -ceq 'delete-unconfirmed' -and $x.outcomes[1].revision -ceq $revs[1] -and $x.outcomes[1].outcome -ceq 'deleted') 'object remained handling'
                Assert ($x.requests.delete -eq 3 -and @($x.outcomes|Where-Object revision -CEQ $revs[2]).Count -eq 1 -and (Get-RemoteLeft $f $revs[2]) -eq 2 -and $x.remaining -eq 1) 'retried in same run'
                Assert ((Get-DeletionFiles $revs[2]) -ceq '1.abandoned.json,1.intent.json,1.sent.json') 'abandon records'
            }
            # 手順6の後: 再開のGET 200で取下げ、計算し直して候補でなければ新しい試行を始めず計数へ戻す。
            'resume-abandon-recompute'{
                $revs=New-Series $f 3;Set-EvidenceTestValue ContentCleanup KeepCount 2
                $f.Hook={param($g,$r,$p)if($r.Operation -ceq 'delete'){New-Response 'delete' $g 403 'forbidden'}}
                $x=Invoke-Cleanup $f;Assert ($x.status -ceq 'partial' -and $x.deleting.revisions[0] -ceq $revs[2]) 'first run'
                $f.Hook=$null;Set-EvidenceTestValue ContentCleanup KeepCount 3
                $y=Invoke-Cleanup $f
                Assert ($y.status -ceq 'passed' -and $y.reasonCode -ceq 'within-limit' -and @($y.outcomes).Count -eq 1 -and $y.outcomes[0].outcome -ceq 'abandoned' -and $y.requests.delete -eq 0 -and $y.requests.get -eq 1 -and $y.retained.count -eq 3) 'abandoned revision not returned to counts'
                Set-EvidenceTestValue ContentCleanup KeepCount 2
                $z=Invoke-Cleanup $f
                Assert ($z.status -ceq 'passed' -and (Get-RemoteLeft $f $revs[2]) -eq 0 -and (Get-DeletionFiles $revs[2]) -cmatch '2\.deleted\.json') 'new attempt after abandon'
            }
            # 手順5〜8・I3: 確定点の後は取り下げず、保護源が現れても消し切る。再開はGETから（済んだkeyへDELETEを送らない）。
            'after-commit'{
                Set-EvidenceTestValue ContentCleanup KeepCount 1
                # 確定点の後の残りobjectのGETを判定できない: 未解決。inspectは削除中、次回は保護源が現れていても消し切る。
                Reset-Store $f 'object-get';$revs=New-Series $f 2 2;$k=Get-RevKeys $f $revs[1]
                $f.Hook={param($g,$r,$p)if($r.Operation -ceq 'get' -and $r.Key -ceq $k[1]){New-Response 'get' $g 500 'server-error'}}.GetNewClosure()
                $x=Invoke-Cleanup $f;$f.Hook=$null
                Assert ($x.status -ceq 'partial' -and $x.outcomes[0].outcome -ceq 'delete-pending' -and (Get-DeletionFiles $revs[1]) -ceq '1.committed.json,1.intent.json,1.sent.json' -and (Get-RemoteLeft $f $revs[1]) -eq 2) 'object GET failure'
                Assert ($x.deleting.revisions[0] -ceq $revs[1] -and (Invoke-ContentInspect $f.Config $set $tgt $revs[1]).reasonCode -ceq 'delete-pending') 'committed-only not deleting'
                Add-BuildFor $revs[1];Add-PlayerFor $f $revs[1]
                $y=Invoke-Cleanup $f
                Assert ($y.status -ceq 'passed' -and $y.outcomes[0].outcome -ceq 'deleted' -and (Get-RemoteLeft $f $revs[1]) -eq 0 -and $y.requests.delete -eq 2) "protected after commit not finished: $($y.reasonCode)"
                # 2件目のobjectのDELETEが403: 次回は1件目をGET 404で済みとし、DELETEを送らない。
                Reset-Store $f 'object-delete';$f.Log.Clear();$revs=New-Series $f 2 2;$k=Get-RevKeys $f $revs[1]
                $f.Hook={param($g,$r,$p)if($r.Operation -ceq 'delete' -and $r.Key -ceq $k[2]){New-Response 'delete' $g 403 'forbidden'}}.GetNewClosure()
                $x=Invoke-Cleanup $f;$f.Hook=$null
                Assert ($x.status -ceq 'partial' -and $x.outcomes[0].outcome -ceq 'delete-pending' -and (Get-RemoteLeft $f $revs[1]) -eq 1) 'object DELETE failure'
                $f.Log.Clear();$y=Invoke-Cleanup $f
                Assert ($y.status -ceq 'passed' -and $y.requests.delete -eq 1 -and $y.requests.get -eq 3 -and -not ($f.Log -contains "delete $($k[1])") -and -not ($f.Log -contains "get $($k[0])")) "resume resent: $($f.Log -join ';')"
                $obs=@((Read-Record $revs[1] '1.deleted.json').observations);Assert ($obs.Count -eq 4 -and $obs[0].key -ceq $k[1] -and $obs[0].httpStatus -eq 404) 'deleted record of the finishing pass'
                # DELETE 2xxの後もobjectが残る: 確定点の後なので取り下げず、未解決で止める。
                Reset-Store $f 'object-remains';$revs=New-Series $f 2 1;$k=Get-RevKeys $f $revs[1]
                $f.Hook={param($g,$r,$p)if($r.Operation -ceq 'delete' -and $r.Key -ceq $k[1]){New-Response 'delete' $g 204 'success'}}.GetNewClosure()
                $x=Invoke-Cleanup $f;$f.Hook=$null
                Assert ($x.status -ceq 'partial' -and $x.outcomes[0].outcome -ceq 'delete-pending' -and (Get-DeletionFiles $revs[1]) -ceq '1.committed.json,1.intent.json,1.sent.json') 'remaining object abandoned after commit'
                # 同じrunで確定点の直後に保護源が現れても消し切る（取得できないrevisionを半端に残さない）。
                Reset-Store $f 'protect-after-commit';$revs=New-Series $f 2 2;$target=$revs[1]
                Set-EvidenceTestValue ContentCleanup StepHook ({param($point)if($point -ceq 'after-commit'){Add-PlayerFor $f $target}}.GetNewClosure())
                $x=Invoke-Cleanup $f;Set-EvidenceTestValue ContentCleanup StepHook $null
                Assert ($x.outcomes[0].outcome -ceq 'deleted' -and (Get-RemoteLeft $f $revs[1]) -eq 0) 'stopped after commit'
            }
            # AC10(c): intent確定後の記録の書込み失敗は未解決のまま止め、次回の再開で確定させる。
            'record-write-failures'{
                Set-EvidenceTestValue ContentCleanup KeepCount 1
                foreach($spec in @(@('intent','before-rename'),@('intent','after-rename'),@('sent','before-rename'),@('committed','before-rename'),@('abandoned','before-rename'),@('deleted','before-rename'))){
                    Reset-Store $f ($spec -join '-');$revs=New-Series $f 3
                    $target="deletion/1.$($spec[0]).json";$phase=$spec[1]
                    Set-EvidenceTestValue BuildStore FaultHook ({param($where,$name)if($name -ceq $target -and $where -ceq $phase){throw 'injected-write-failure'}}.GetNewClosure())
                    if($spec[0] -ceq 'abandoned'){$keep=(Get-RevKeys $f $revs[2])[0];$f.Hook={param($g,$r,$p)if($r.Operation -ceq 'delete' -and $r.Key -ceq $keep){New-Response 'delete' $g 204 'success'}}.GetNewClosure()}
                    $x=Invoke-Cleanup $f
                    Set-EvidenceTestValue BuildStore FaultHook $null;$f.Hook=$null
                    Assert ($x.status -ceq 'partial' -and @($x.outcomes).Count -eq 1 -and $x.outcomes[0].outcome -ceq 'delete-pending') "write failure not pending: $($spec -join '-')"
                    $files=Get-DeletionFiles $revs[2]
                    switch($spec[0]){
                        'intent'{Assert ($x.requests.delete -eq 0 -and (($files -ceq '1.intent.json') -eq ($phase -ceq 'after-rename'))) "intent failure sent DELETE: $phase"}
                        'sent'{Assert ($x.requests.delete -eq 1 -and $x.requests.get -eq 0 -and $files -ceq '1.intent.json') 'sent failure continued'}
                        'committed'{Assert ($x.requests.get -eq 1 -and $files -ceq '1.intent.json,1.sent.json' -and (Get-RemoteLeft $f $revs[2]) -eq 1) 'committed failure'}
                        'abandoned'{Assert ($files -ceq '1.intent.json,1.sent.json' -and (Get-RemoteLeft $f $revs[2]) -eq 2) 'abandoned failure'}
                        'deleted'{Assert ($x.requests.delete -eq 2 -and $x.requests.get -eq 3 -and $files -ceq '1.committed.json,1.intent.json,1.sent.json' -and (Get-RemoteLeft $f $revs[2]) -eq 0) 'deleted failure'}
                    }
                    $y=Invoke-Cleanup $f
                    Assert ($y.status -ceq 'passed' -and (Get-RemoteLeft $f $revs[2]) -eq 0 -and (Get-RemoteLeft $f $revs[1]) -eq 0 -and $y.deleted.count -eq 2) "not recovered: $($spec -join '-') $($y.reasonCode)"
                }
            }
            # AC12: 状態遷移表の予算判定点ごとに、超過なら書かず・送らずに未解決で止める。経過時間の予算も同じ。
            'budget-points'{
                Set-EvidenceTestValue ContentCleanup KeepCount 1
                foreach($point in @('intent','delete','sent','get','committed','abandoned','object-get','object-delete','deleted')){
                    Reset-Store $f $point;$revs=New-Series $f 3
                    Set-EvidenceTestValue ContentCleanup BudgetHook ({param($at)if($at -ceq $point){throw 'budget-exceeded'}}.GetNewClosure())
                    if($point -ceq 'abandoned'){$keep=(Get-RevKeys $f $revs[2])[0];$f.Hook={param($g,$r,$p)if($r.Operation -ceq 'delete' -and $r.Key -ceq $keep){New-Response 'delete' $g 204 'success'}}.GetNewClosure()}
                    $x=Invoke-Cleanup $f
                    Set-EvidenceTestValue ContentCleanup BudgetHook $null;$f.Hook=$null
                    $files=Get-DeletionFiles $revs[2]
                    Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'budget-exceeded' -and $x.outcomes[0].outcome -ceq 'budget-exceeded' -and @($x.outcomes).Count -eq 1) "budget result: $point"
                    $c='1.committed.json,1.intent.json,1.sent.json'
                    $expected=switch($point){'intent'{@(0,0,'')};'delete'{@(0,0,'1.intent.json')};'sent'{@(1,0,'1.intent.json')};'get'{@(1,0,'1.intent.json,1.sent.json')};'committed'{@(1,1,'1.intent.json,1.sent.json')};'abandoned'{@(1,1,'1.intent.json,1.sent.json')};'object-get'{@(1,1,$c)};'object-delete'{@(1,2,$c)};'deleted'{@(2,3,$c)}}
                    Assert ($x.requests.delete -eq $expected[0] -and $x.requests.get -eq $expected[1] -and $files -ceq $expected[2]) "budget point $point wrote or sent: $files $($x.requests.delete)/$($x.requests.get)"
                    $y=Invoke-Cleanup $f
                    Assert ($y.status -ceq 'passed' -and $y.deleted.count -eq 2) "budget not recovered: $point $($y.reasonCode)"
                }
                Reset-Store $f 'timer';$revs=New-Series $f 3
                Set-EvidenceTestValue ContentCleanup BudgetMilliseconds 0
                $x=Invoke-Cleanup $f
                Set-EvidenceTestValue ContentCleanup BudgetMilliseconds 600000
                Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'budget-exceeded' -and $x.requests.delete -eq 0 -and (Get-DeletionFiles $revs[2]) -ceq '') 'elapsed budget not enforced'
            }
            # AC12: DELETEの総数の上限。試行の開始時と各DELETEの直前に判定し、試行の途中で達したら未解決のまま止める。
            'delete-limit'{
                Set-EvidenceTestValue ContentCleanup KeepCount 1
                Reset-Store $f 'mid';$revs=New-Series $f 3
                Set-EvidenceTestValue ContentCleanup DeleteLimit 3
                $x=Invoke-Cleanup $f
                Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'delete-limit' -and $x.requests.delete -eq 3 -and $x.outcomes[1].revision -ceq $revs[1] -and $x.outcomes[1].outcome -ceq 'delete-limit' -and $x.deleting.revisions[0] -ceq $revs[1]) "mid-attempt limit: $($x.reasonCode) $($x.requests.delete)"
                Assert ((Get-DeletionFiles $revs[1]) -ceq '1.committed.json,1.intent.json,1.sent.json' -and (Get-RemoteLeft $f $revs[1]) -eq 1) 'mid-attempt state'
                Set-EvidenceTestValue ContentCleanup DeleteLimit 2048
                $y=Invoke-Cleanup $f;Assert ($y.status -ceq 'passed' -and $y.requests.delete -eq 1 -and (Get-RemoteLeft $f $revs[1]) -eq 0) 'resume after limit'
                Reset-Store $f 'start';$revs=New-Series $f 3
                Set-EvidenceTestValue ContentCleanup DeleteLimit 2
                $x=Invoke-Cleanup $f
                Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'delete-limit' -and $x.requests.delete -eq 2 -and $x.remaining -eq 1 -and @($x.outcomes).Count -eq 1 -and (Get-DeletionFiles $revs[1]) -ceq '') 'limit at attempt start'
                Set-EvidenceTestValue ContentCleanup DeleteLimit 2048
            }
            # AC7・AC14: 系列lockを別processが持つ間、cleanupは待たずにcleanup-in-progress。試行中のrevisionのinspectはpublish-in-progress。
            'lock-contention'{
                $revs=New-Series $f 3
                $b=New-Barrier
                try{
                    $job=Invoke-Child (Get-ChildArgs2 $f (Get-CleanupCli $f) '' 1 'normal' 'delete' $b.ReadyName $b.GoName)
                    Wait-Ready $b $job
                    $x=Invoke-Cleanup $f
                    Assert ($x.status -ceq 'blocked' -and $x.reasonCode -ceq 'cleanup-in-progress' -and $x.requests.delete -eq 0) "second cleanup ran: $($x.reasonCode)"
                    Assert ((Invoke-Cleanup $f -DryRun).status -ceq 'passed') 'dry-run took lock'
                    $i=Invoke-ContentInspect $f.Config $set $tgt $revs[2]
                    Assert ($i.status -ceq 'failed' -and $i.reasonCode -ceq 'publish-in-progress' -and -not $i.PSObject.Properties['reference']) "inspect during attempt: $($i.reasonCode)"
                    Assert ((Invoke-ContentInspect $f.Config $set $tgt $revs[0]).status -ceq 'passed') 'inspect of another revision'
                    $null=$b.Go.Set();$done=Finish-Child $job
                    $child=ConvertFrom-ChildJson $done
                    Assert ($done.Exit -eq 0 -and $child.status -ceq 'passed' -and $child.deleted.count -eq 2) "child cleanup: $($done.Output) $($done.Error)"
                }finally{$null=$b.Go.Set();$b.Ready.Dispose();$b.Go.Dispose()}
                Assert ((Invoke-ContentInspect $f.Config $set $tgt $revs[2]).reasonCode -ceq 'payload-deleted') 'inspect after delete'
            }
            # AC8: collect/inspectがpublish.lockを保持中のrevisionは revision-busy で飛ばし、触れない。
            'revision-busy'{
                $revs=New-Series $f 3;Set-EvidenceTestValue ContentCleanup KeepCount 1
                $b=New-Barrier
                try{
                    $job=Invoke-Child (Get-ChildArgs2 $f '' (Get-RevDir $revs[2]) 10 'normal' '' $b.ReadyName $b.GoName)
                    Wait-Ready $b $job
                    $x=Invoke-Cleanup $f
                    Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'revision-busy' -and $x.outcomes[0].revision -ceq $revs[2] -and $x.outcomes[0].outcome -ceq 'revision-busy' -and $x.outcomes[1].revision -ceq $revs[1] -and $x.outcomes[1].outcome -ceq 'deleted') "busy not skipped: $($x.reasonCode)"
                    Assert ((Get-RemoteLeft $f $revs[2]) -eq 2 -and (Get-DeletionFiles $revs[2]) -ceq '' -and $x.remaining -eq 1) 'busy revision touched'
                    $null=$b.Go.Set();$done=Finish-Child $job;Assert ($done.Exit -eq 0) "hold child: $($done.Error)"
                }finally{$null=$b.Go.Set();$b.Ready.Dispose();$b.Go.Dispose()}
                $y=Invoke-Cleanup $f;Assert ($y.status -ceq 'passed' -and (Get-RemoteLeft $f $revs[2]) -eq 0) 'busy revision not deleted later'
            }
            # AC7・I1: 系列lockの後、revisionのpublish.lockの後に取り直した保護で判断する（lock前・dry-runの結果を使わない）。
            'reevaluate-after-lock'{
                $revs=New-Series $f 3;Set-EvidenceTestValue ContentCleanup KeepCount 2
                $d=Invoke-Cleanup $f -DryRun;Assert ($d.candidates.revisions[0] -ceq $revs[2]) 'dry-run candidate'
                $target=$revs[2]
                Set-EvidenceTestValue ContentCleanup StepHook ({param($point)if($point -ceq 'after-lock'){Add-BuildFor $target}}.GetNewClosure())
                $x=Invoke-Cleanup $f;Set-EvidenceTestValue ContentCleanup StepHook $null
                Assert ($x.status -ceq 'passed' -and (Get-RemoteLeft $f $revs[2]) -eq 2 -and (Get-RemoteLeft $f $revs[1]) -eq 0) 'pre-lock snapshot used'
                Reset-Store $f 'revision-lock';$revs=New-Series $f 3;$target=$revs[2];$state=@{done=$false}
                Set-EvidenceTestValue ContentCleanup StepHook ({param($point)if($point -ceq 'after-revision-lock' -and -not $state.done){$state.done=$true;Add-PlayerFor $f $target -Staging}}.GetNewClosure())
                $x=Invoke-Cleanup $f;Set-EvidenceTestValue ContentCleanup StepHook $null
                Assert ($x.status -ceq 'passed' -and $x.outcomes[0].revision -ceq $revs[2] -and $x.outcomes[0].outcome -ceq 'not-candidate' -and $x.outcomes[1].outcome -ceq 'deleted' -and (Get-RemoteLeft $f $revs[2]) -eq 2 -and (Get-DeletionFiles $revs[2]) -ceq '') "pre-revision-lock snapshot used: $($x.reasonCode)"
            }
            # AC14・I5: 削除済み・削除中のrevisionをinspect/collectが成功として返さず、再PUTもしない。fetchは失敗する。
            'inspect-collect-deleted'{
                Set-EvidenceTestValue ContentApplication Clock ({$baseNow.AddMinutes(-30)}.GetNewClosure())
                $src=New-ContentSource $f $script:Identity @{} 2 'z'
                $c=Invoke-ContentCollect $f.Config $src.Dir $f.ArtifactsRoot;Assert ($c.status -ceq 'passed' -and $c.publish -ceq 'published') "collect failed: $($c.reasonCode)"
                $null=New-Revision $f $baseNow.AddMinutes(-1);Set-EvidenceTestValue ContentCleanup KeepCount 1
                $x=Invoke-Cleanup $f;Assert ($x.deleted.revisions[0] -ceq $src.Revision -and (Get-RemoteLeft $f $src.Revision) -eq 0) "collected revision not deleted: $($x.reasonCode)"
                $i=Invoke-ContentInspect $f.Config $set $tgt $src.Revision
                Assert ($i.status -ceq 'failed' -and $i.reasonCode -ceq 'payload-deleted' -and -not $i.PSObject.Properties['reference']) "deleted inspect: $($i.reasonCode)"
                $puts=Get-Count $f 'put'
                $again=Invoke-ContentCollect $f.Config $src.Dir $f.ArtifactsRoot
                Assert ($again.status -ceq 'failed' -and $again.reasonCode -ceq 'payload-deleted' -and $null -eq $again.reference -and (Get-Count $f 'put') -eq $puts) "collect returned deleted revision: $($again.reasonCode)"
                $fetch=Invoke-ContentFetch $f.Config $c.reference $c.manifestSha256;Assert ($fetch.status -ceq 'failed') 'fetch of deleted revision passed'
                $pending=New-Revision $f $baseNow.AddMinutes(-40);Add-FakeAttempt $f $pending 'committed'
                Assert ((Invoke-ContentInspect $f.Config $set $tgt $pending).reasonCode -ceq 'delete-pending') 'pending inspect'
                $broken=New-Revision $f $baseNow.AddMinutes(-41);$p=[IO.Path]::Combine((Get-RevDir $broken),'deletion');[IO.Directory]::CreateDirectory($p)|Out-Null;Set-ArtifactAcl $p $true;[IO.File]::WriteAllText([IO.Path]::Combine($p,'x.json'),'{}')
                $i=Invoke-ContentInspect $f.Config $set $tgt $broken;Assert ($i.reasonCode -ceq 'receipt-unavailable' -and -not $i.PSObject.Properties['reference']) 'unreadable deletion inspect'
                # 削除中のrevisionを同じsourceでcollectしても成功も再PUTも返さない。
                $src2=New-ContentSource $f '20260925T202227427Z-11111111111111111111111111111111' @{} 2 'y'
                $c2=Invoke-ContentCollect $f.Config $src2.Dir $f.ArtifactsRoot;Assert ($c2.status -ceq 'passed') 'second collect'
                Add-FakeAttempt $f $src2.Revision 'committed';$puts=Get-Count $f 'put'
                $again=Invoke-ContentCollect $f.Config $src2.Dir $f.ArtifactsRoot
                Assert ($again.reasonCode -ceq 'delete-pending' -and $null -eq $again.reference -and (Get-Count $f 'put') -eq $puts) "collect of deleting revision: $($again.reasonCode)"
                Assert ((Invoke-ContentInspect $f.Config $set $tgt (New-Revision $f $baseNow.AddMinutes(-2))).status -ceq 'passed') 'inspect of a live revision'
            }
            # AC16: cleanupとinspectはpublish.lockを作らない。成功revisionのlockが無ければ、cleanupは系列を
            # catalog-incompleteで止め、inspectはreceipt-unavailableで止まり、どちらもcontents-v1を変えない。
            'publish-lock-missing'{
                $revs=New-Series $f 3;Set-EvidenceTestValue ContentCleanup KeepCount 1
                # 先に通常の実行でcleanup.lockを作っておき、前後の比較を全fileで行えるようにする（候補なしで何も消さない）。
                Set-EvidenceTestValue ContentCleanup KeepCount 10;$null=Invoke-Cleanup $f;Set-EvidenceTestValue ContentCleanup KeepCount 1
                $lockPath=[IO.Path]::Combine((Get-RevDir $revs[1]),'publish.lock');[IO.File]::Delete($lockPath)
                $before=Get-Tree $f.Owned[1];$f.CleanCounts.Clear()
                $d=Invoke-Cleanup $f -DryRun;$x=Invoke-Cleanup $f
                Assert ($d.status -ceq 'blocked' -and $d.reasonCode -ceq 'catalog-incomplete' -and $x.status -ceq 'blocked' -and $x.reasonCode -ceq 'catalog-incomplete') "missing lock not blocked: $($x.reasonCode)"
                Assert ($f.CleanCounts.Count -eq 0 -and (Get-Tree $f.Owned[1]) -ceq $before -and -not [IO.File]::Exists($lockPath)) 'cleanup wrote the store or created publish.lock'
                $i=Invoke-ContentInspect $f.Config $set $tgt $revs[1]
                Assert ($i.status -ceq 'failed' -and $i.reasonCode -ceq 'receipt-unavailable' -and -not $i.PSObject.Properties['reference']) "inspect without lock: $($i.reasonCode)"
                Assert ((Get-Tree $f.Owned[1]) -ceq $before -and -not [IO.File]::Exists($lockPath)) 'inspect wrote the store or created publish.lock'
                # 系列lockの取得後（dry-runの後）にlockが消えた場合も、取り直したcatalogで作らずに止める。
                Reset-Store $f 'race';$revs=New-Series $f 3;$target=Get-RevDir $revs[2]
                Set-EvidenceTestValue ContentCleanup StepHook ({param($point)if($point -ceq 'after-lock'){[IO.File]::Delete([IO.Path]::Combine($target,'publish.lock'))}}.GetNewClosure())
                $x=Invoke-Cleanup $f;Set-EvidenceTestValue ContentCleanup StepHook $null
                Assert ($x.status -ceq 'blocked' -and $x.reasonCode -ceq 'catalog-incomplete' -and -not [IO.File]::Exists([IO.Path]::Combine($target,'publish.lock')) -and (Get-DeletionFiles $revs[2]) -ceq '') "lock removed after lock: $($x.reasonCode)"
                # collectの既存挙動は変えない（lockが無ければ作って進む）。
                $l=Enter-ContentPublishLock $target;$l.Dispose();Assert ([IO.File]::Exists([IO.Path]::Combine($target,'publish.lock'))) 'collect lock path changed'
            }
            # AC13: transport入口の許可表。Contentの印はContentのkeyのDELETEだけを開き、他の種別の扱いは変えない。
            'transport-allow-table'{
                $endpoint='https://'+('e'*32)+'.r2.cloudflarestorage.com'
                $p="development/content/v1/$repo/$set/$tgt/20260924T202227427Z-$('0'*32)/"
                $buildKey="development/builds/v1/$repo/samplegame/windows-x64-player/il2cpp-high/$('1'*32)/bundle.zip"
                $rows=@(
                    @('content-manifest-marked','delete',($p+'transport.json'),@{ContentRetentionDelete=$true},$true),
                    @('content-object-marked','delete',($p+'content/a/b.cf'),@{ContentRetentionDelete=$true},$true),
                    @('content-unmarked','delete',($p+'transport.json'),@{},$false),
                    @('content-false','delete',($p+'transport.json'),@{ContentRetentionDelete=$false},$false),
                    @('content-string','delete',($p+'transport.json'),@{ContentRetentionDelete='True'},$false),
                    @('content-int','delete',($p+'transport.json'),@{ContentRetentionDelete=1},$false),
                    @('content-build-mark','delete',($p+'transport.json'),@{BuildRetentionDelete=$true},$false),
                    @('build-content-mark','delete',$buildKey,@{ContentRetentionDelete=$true},$false),
                    @('build-both-marks','delete',$buildKey,@{ContentRetentionDelete=$true;BuildRetentionDelete=$true},$false),
                    @('build-marked','delete',$buildKey,@{BuildRetentionDelete=$true},$true),
                    @('evidence-content-mark','delete',"development/evidence/v2/$repo/x/bundle.zip",@{ContentRetentionDelete=$true},$false),
                    @('evidence-unmarked','delete',"development/evidence/v2/$repo/x/bundle.zip",@{},$true),
                    @('first-use-content-mark','delete','evidence/first-use/x/bundle.zip',@{ContentRetentionDelete=$true;ResetAuthorized=$true},$false),
                    @('content-get-marked','get',($p+'transport.json'),@{ContentRetentionDelete=$true},$true),
                    @('content-outside-shape','delete',($p+'other.json'),@{ContentRetentionDelete=$true},$false)
                )
                foreach($row in $rows){
                    $request=@{Endpoint=$endpoint;Operation=$row[1];Key=$row[2];SourcePath=$null;DestinationPath=$null;DeadlineMilliseconds=1000};foreach($k in $row[3].Keys){$request[$k]=$row[3][$k]}
                    Assert ((Test-R2ArtifactRequestAllowed $request) -eq $row[4]) "allow table: $($row[0])"
                }
                $accepted=$false;try{$null=Invoke-R2ArtifactOperation 'DUMMY_ID' 'DUMMY_SECRET' ('f'*32) @{Endpoint=$endpoint;Operation='delete';Key=($p+'transport.json');SourcePath=$null;DestinationPath=$null;DeadlineMilliseconds=1000};$accepted=$true}catch{}
                Assert (-not $accepted) 'unmarked Content DELETE reached SDK'
                # ContentCleanupだけが印を付ける（他のmoduleのsourceに印の名前が無い）。
                $owners=@(Get-ChildItem -LiteralPath ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))) -Recurse -Filter '*.psm1'|Where-Object {[IO.File]::ReadAllText($_.FullName).Contains('.ContentRetentionDelete=$true')}|ForEach-Object Name)
                Assert (($owners -join ',') -ceq 'ContentCleanup.psm1') "mark owners: $($owners -join ',')"
            }
            # AC1・AC15・CLI: 実CLIのgrammar・終了コード（0/1/2）を別processで通す。
            'cli-grammar'{
                $revs=New-Series $f 3
                $run=Finish-Child (Invoke-Child (Get-ChildArgs2 $f (Get-CleanupCli $f -DryRun) '' 1))
                $out=ConvertFrom-ChildJson $run
                Assert ($run.Exit -eq 0 -and $out.status -ceq 'passed' -and $out.candidates.count -eq 2) "dry-run CLI: $($run.Output) $($run.Error)"
                $missing=[IO.Path]::Combine($f.Root,'missing.json')
                $none=Finish-Child (Invoke-Child (Get-ChildArgs2 $f (@('content','cleanup','--profile','osm','--config',$missing,'--content-set',$set,'--target',$tgt) -join '|')))
                Assert ($none.Exit -eq 1 -and (ConvertFrom-ChildJson $none).reasonCode -ceq 'artifacts-root-not-configured') "unconfigured CLI: $($none.Output)"
                $bad=Finish-Child (Invoke-Child (Get-ChildArgs2 $f (@('content','cleanup','--profile','osm','--config',$missing,'--content-set','Bad','--target',$tgt,'--artifacts-root',$f.ArtifactsRoot) -join '|')))
                Assert ($bad.Exit -eq 1 -and (ConvertFrom-ChildJson $bad).reasonCode -ceq 'invalid-content-identity') 'invalid token read config first'
                $cfg=$f.Config;$root=$f.ArtifactsRoot
                foreach($argsList in @(
                    @('content','cleanup','--profile','osm','--config',$cfg,'--content-set',$set,'--target',$tgt,'--artifacts-root',$root,'--dry-run','--dry-run'),
                    @('content','cleanup','--profile','osm','--config',$cfg,'--content-set',$set,'--target',$tgt,'--keep','5'),
                    @('content','cleanup','--profile','osm','--config',$cfg,'--content-set',$set),
                    @('content','delete','--profile','osm','--config',$cfg,'--content-set',$set,'--target',$tgt),
                    @('content','inspect','--profile','osm','--config',$cfg,'--content-set',$set,'--target',$tgt,'--revision',$revs[0],'--dry-run'),
                    @('content','cleanup','--profile','other','--config',$cfg,'--content-set',$set,'--target',$tgt))){
                    $r=Finish-Child (Invoke-Child (Get-ChildArgs2 $f ($argsList -join '|')))
                    Assert ($r.Exit -eq 1 -and $r.Output.Contains('unsupported-command')) "grammar accepted: $($argsList -join ' ')"
                }
                $partial=Finish-Child (Invoke-Child (Get-ChildArgs2 $f (Get-CleanupCli $f) '' 1 'forbidden'))
                Assert ($partial.Exit -eq 2 -and (ConvertFrom-ChildJson $partial).status -ceq 'partial') "partial exit: $($partial.Output)"
                $done=Finish-Child (Invoke-Child (Get-ChildArgs2 $f (Get-CleanupCli $f) '' 1))
                Assert ($done.Exit -eq 0 -and (ConvertFrom-ChildJson $done).deleted.count -eq 2) "run exit: $($done.Output)"
                Assert (-not $run.Output.Contains($f.Root) -and -not $run.Output.Contains('https://') -and -not $done.Output.Contains($f.Root)) 'CLI output leaked path or endpoint'
            }
        }
    }catch{$failed.Add($caseName+': '+$_.Exception.GetType().Name+': '+$_.Exception.Message+' (line '+$_.InvocationInfo.ScriptLineNumber+')')}
    finally{
        $executed.Add($caseName)
        foreach($name in @('NetworkHook','BudgetHook','StepHook')){Set-EvidenceTestValue ContentCleanup $name $null}
        Set-EvidenceTestValue ContentCleanup KeepCount 10;Set-EvidenceTestValue ContentCleanup DeleteLimit 2048;Set-EvidenceTestValue ContentCleanup BudgetMilliseconds 600000
        Set-EvidenceTestValue ContentApplication Clock {[DateTimeOffset]::UtcNow}
        [Environment]::SetEnvironmentVariable('OSM_ARTIFACTS_ROOT',$savedRoot)
        Clear-ContentFixture $fixture
    }
}
if($selected.Count -eq 0 -or $selected.Count -ne $executed.Count){$failed.Add('selection')}
if($ResultPath){Write-EvidenceTestJson $ResultPath ([ordered]@{registered=$registered;selected=$selected;executed=@($executed);failed=@($failed);binaries=@(Get-EvidenceTestBinaries)})}
foreach($item in $failed){[Console]::Error.WriteLine($item)}
[Console]::WriteLine("ContentCleanup $($executed.Count), failed $($failed.Count)")
if($failed.Count){exit 1}
