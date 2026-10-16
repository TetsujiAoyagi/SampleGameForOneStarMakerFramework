param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../BuildCleanup.psm1')
Import-Module (Join-Path $PSScriptRoot '../BuildApplication.psm1')
Import-Module (Join-Path $PSScriptRoot '../BuildStore.psm1')
Import-Module (Join-Path $PSScriptRoot '../BuildContract.psm1')
Import-Module (Join-Path $PSScriptRoot '../BuildProtection.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactApplication.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactPaths.psm1')
Import-Module (Join-Path $PSScriptRoot '../Transport/R2ArtifactTransport.psm1')
# Build件数清掃（catalog・保護・削除試行の状態遷移・照会・排他・transport入口・CLI）をofflineで検証する。
# 成功記録は非秘密の合成receiptで作り、R2はremote directory、時計・通信・予算・Nは注入する。
# 並行は別processをsignalで同期し、時間待機を使わない。実R2での実証は判定Cの別証拠として扱う。
$registered=@('catalog-classification','catalog-config-independent','catalog-corrupt-blocks','catalog-foreign-invalid','catalog-unknown-entry','catalog-clock-skew',
    'protection-rules','protection-use-release','dry-run-no-writes','delete-oldest-first','protected-over-limit','delete-failures-pending','get-unconfirmed',
    'resume-abandon-recompute','record-write-failures','budget-points','delete-limit','lock-contention','access-lock','reevaluate-after-lock',
    'inspect-collect-deleted','transport-allow-table','cli-grammar')
$selected=@(if($Case -ceq '*'){$registered}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
$repo='a'*64;$series=@('samplegame','windows-x64-player','il2cpp-high')
$baseNow=[DateTimeOffset]::new(2026,10,11,0,0,0,[TimeSpan]::Zero)
function Assert([bool]$Condition,[string]$Message){Assert-EvidenceTest $Condition $Message}
function Write-PrivateJson([string]$Path,$Value){Write-EvidenceTestJson $Path $Value;Set-ArtifactAcl $Path $false}
function Get-Sha([byte[]]$Bytes){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()}
function Get-RemotePath($F,[string]$Key){[IO.Path]::Combine($F.RemoteRoot,(Get-Sha ([Text.Encoding]::UTF8.GetBytes($Key)))+'.bin')}
function global:New-Response([string]$Op,[string]$Gen,$Http,[string]$Class,$S3=$null,[bool]$Observed=$true,[bool]$TimedOut=$false,[bool]$Redirected=$false){
    [pscustomobject]@{Operation=$Op;Generation=$Gen;HttpStatus=$Http;StatusClass=$Class;S3Code=$S3;Bytes=$null;Sha256=$null;StatusObserved=$Observed;TimedOut=$TimedOut;Redirected=$Redirected}
}
function New-CleanupFixture {
    $root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-build-test-'+[Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($root)|Out-Null
    $owned=@('transfers','builds-v1','deployments','workflow','evidence','credentials'|ForEach-Object {[IO.Path]::Combine($root,$_)})
    Set-EvidenceTestValue ArtifactPaths TestRoot $owned[0]
    Set-EvidenceTestValue BuildStore TestRoot $owned[1]
    Set-EvidenceTestValue EvidenceContract DeploymentRoot $owned[2]
    Set-EvidenceTestValue BuildContract OwnedRootsForTest $owned
    $deployment=$owned[2];[IO.Directory]::CreateDirectory($deployment)|Out-Null
    $ev=[ordered]@{schemaVersion=2;profile='osm';endpoint=('https://'+('e'*32)+'.r2.cloudflarestorage.com');bucket='osm-artifacts';repositoryId=$repo;prefix='development/evidence/v2/';policy='task-end-30d-v1';deploymentId=('d'*32)}
    $evPath=[IO.Path]::Combine($deployment,('d'*32)+'.config.json');Write-EvidenceTestJson $evPath $ev
    $evHash=(Get-FileHash -LiteralPath $evPath).Hash.ToLowerInvariant()
    Write-EvidenceTestJson ([IO.Path]::Combine($deployment,('d'*32)+'.json')) ([ordered]@{schemaVersion=2;deploymentId=('d'*32);repositoryId=$repo;configSha256=$evHash;baselineRecordId='fixture';baselineSha256=('b'*64);ownerObservationId='fixture';ownerObservationSha256=('c'*64)})
    $operation=New-ArtifactOperation
    $configPath=[IO.Path]::Combine($operation,'build-config.json')
    $configValue=[ordered]@{schemaVersion=1;purpose='build';profile='osm';evidenceConfigPath=$evPath;evidenceConfigSha256=$evHash;repositoryId=$repo;prefix='development/builds/v1/';policy='build-publish-v1'}
    Write-PrivateJson $configPath $configValue
    $remote=[IO.Path]::Combine($root,'remote');[IO.Directory]::CreateDirectory($remote)|Out-Null
    $counts=[Collections.Concurrent.ConcurrentDictionary[string,int]]::new()
    $state=[pscustomobject]@{Hook=$null;Now=$baseNow}
    $network={param($gen,$request)
        $counts.AddOrUpdate($request.Operation+' '+$request.Key,1,{param($k,$v)$v+1})|Out-Null
        $path=[IO.Path]::Combine($remote,[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($request.Key))).ToLowerInvariant()+'.bin')
        # 実transportと同じ入口判定を通す。印の無いBuild DELETEやContent DELETEはここで拒否される。
        if(-not (Test-R2ArtifactRequestAllowed $request)){throw 'transport-rejected'}
        if($state.Hook){$custom=& $state.Hook $gen $request $path;if($null -ne $custom){return $custom}}
        $status=200;$class='success';$code=$null;$bytes=$null;$sha=$null
        if($request.Operation -ceq 'delete'){[IO.File]::Delete($path);$status=204}
        elseif($request.Operation -ceq 'get'){
            if([IO.File]::Exists($path)){$data=[IO.File]::ReadAllBytes($path);[IO.File]::WriteAllBytes($request.DestinationPath,$data);$bytes=[long]$data.Length;$sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant()}
            else{$status=404;$class='not-found';$code='NoSuchKey'}
        }else{[IO.File]::WriteAllBytes($path,[IO.File]::ReadAllBytes($request.SourcePath))}
        return [pscustomobject]@{Operation=$request.Operation;Generation=$gen;HttpStatus=$status;StatusClass=$class;S3Code=$code;Bytes=$bytes;Sha256=$sha;StatusObserved=$true;TimedOut=$false;Redirected=$false}
    }.GetNewClosure()
    $readback={param($op,$cfg,$gen,$key,$bytes,$hash)
        $path=[IO.Path]::Combine($remote,[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($key))).ToLowerInvariant()+'.bin')
        $data=[IO.File]::ReadAllBytes($path);$actual=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant()
        return [pscustomobject]@{operation='readback';generation=$gen;key=$key;bytes=[long]$data.Length;sha256=$actual;childExit=0;httpStatus=200;statusClass='success';s3Code=$null;statusObserved=$true;timedOut=$false;redirected=$false;childStatus='passed';observedAt=$state.Now.ToString('o')}
    }.GetNewClosure()
    $clock={$state.Now}.GetNewClosure()
    Set-EvidenceTestValue BuildCleanup NetworkHook $network
    Set-EvidenceTestValue BuildApplication NetworkHook $network
    Set-EvidenceTestValue BuildApplication ReadbackHook $readback
    Set-EvidenceTestValue BuildCleanup Clock $clock
    Set-EvidenceTestValue BuildApplication Clock $clock
    foreach($name in @('BuildCleanup','BuildApplication')){foreach($module in @(Get-EvidenceTestModules $name)){& $module {function script:Get-CredentialStatus {param($profile)[pscustomobject]@{Generation=('f'*32)}}}}}
    Import-ArtifactPackage
    $config=Read-BuildConfig $configPath
    return [pscustomobject]@{Root=$root;Owned=$owned;StoreRoot=$owned[1];TransferRoot=$owned[0];DeploymentRoot=$owned[2];RemoteRoot=$remote;Config=$configPath;ConfigValue=$configValue;ConfigData=$config;Counts=$counts;State=$state}
}
# 成功記録（intent+receipt）を合成する。Read-BuildReceiptの全検査を通る形で、R2側のobjectも置く。
function New-FixtureBuild($F,[DateTimeOffset]$PublishedAt,[string]$Project='samplegame',[string]$Target='windows-x64-player',[string]$Configuration='il2cpp-high',[switch]$IntentOnly,[switch]$Bare,$Config=$null){
    if($null -eq $Config){$Config=$F.ConfigData}
    $id=[Guid]::NewGuid().ToString('N')
    $dir=Get-BuildDirectory $repo $id $true
    if($Bare){return $id}
    $key="development/builds/v1/$repo/$Project/$Target/$Configuration/$id/bundle.zip"
    $payload=[Text.Encoding]::UTF8.GetBytes('payload-'+$id);[IO.File]::WriteAllBytes((Get-RemotePath $F $key),$payload)
    $entries=@([pscustomobject]@{path='sample.bin';bytes=[long]5;sha256=('1'*64)})
    $intent=[ordered]@{schemaVersion=1;purpose='build';buildId=$id;repositoryId=$repo;project=$Project;target=$Target;configuration=$Configuration;seriesId="$repo/$Project/$Target/$Configuration";key=$key;configSha256=$Config.Hash;evidenceConfigSha256=$Config.EvidenceConfigSha256;packageBytes=[long]$payload.Length;packageSha256=(Get-Sha $payload);manifestSha256=('2'*64);entries=$entries;operationPath=('C:\fixture\'+('3'*32))}
    $written=Write-BuildImmutable $dir 'intent.json' $intent
    if($IntentOnly){return $id}
    $receipt=[ordered]@{}
    foreach($k in $intent.Keys){if($k -cne 'operationPath'){$receipt[$k]=$intent[$k]}}
    $receipt.intentSha256=$written.Sha256
    $receipt.readback=[ordered]@{operation='readback';generation=('f'*32);key=$key;bytes=[long]$payload.Length;sha256=(Get-Sha $payload);childExit=0;httpStatus=200;statusClass='success';s3Code=$null;statusObserved=$true;timedOut=$false;redirected=$false;childStatus='passed';observedAt=$PublishedAt.ToString('o')}
    $receipt.publishState='succeeded';$receipt.publishedAt=$PublishedAt.ToString('o')
    $null=Write-BuildImmutable $dir 'receipt.json' $receipt
    return $id
}
# 新しい順にcount件。最初が最新、最後が最古。
function New-Series($F,[int]$Count,[int]$StartMinutes=1){@(for($i=0;$i -lt $Count;$i++){New-FixtureBuild $F ($F.State.Now.AddMinutes(-($StartMinutes+$i)))})}
function global:Get-BuildDir([string]$Id){[IO.Path]::Combine((Get-BuildRoot),('a'*64),$Id)}
function Get-KeyOf([string]$Id){"development/builds/v1/$repo/samplegame/windows-x64-player/il2cpp-high/$Id/bundle.zip"}
function Test-Remote($F,[string]$Id){[IO.File]::Exists((Get-RemotePath $F (Get-KeyOf $Id)))}
function Get-DeletionFiles([string]$Id){$d=[IO.Path]::Combine((Get-BuildDir $Id),'deletion');if(-not [IO.Directory]::Exists($d)){return @()};@([IO.Directory]::EnumerateFiles($d)|ForEach-Object {[IO.Path]::GetFileName($_)}|Where-Object {$_ -notlike '*.tmp'}|Sort-Object)}
function Get-Count($F,[string]$Op,[string]$Prefix=''){[int](@($F.Counts.GetEnumerator()|Where-Object {$_.Key.StartsWith("$Op $Prefix",[StringComparison]::Ordinal)}|ForEach-Object {$_.Value}|Measure-Object -Sum).Sum)}
function Invoke-Cleanup($F,[switch]$DryRun,$Config=$null){if($null -eq $Config){$Config=$F.Config};Invoke-BuildCleanup $Config $series[0] $series[1] $series[2] ([bool]$DryRun)}
function Reset-Store($F,[string]$Name){Set-EvidenceTestValue BuildStore TestRoot ([IO.Path]::Combine($F.Root,'store-'+$Name));$F.StoreRoot=[IO.Path]::Combine($F.Root,'store-'+$Name)}
function Get-TreeFingerprint([string]$Root){
    if(-not [IO.Directory]::Exists($Root)){return 'absent'}
    $dirs=@([IO.Directory]::EnumerateDirectories($Root,'*',[IO.SearchOption]::AllDirectories)|ForEach-Object {'d|'+[IO.Path]::GetRelativePath($Root,$_)})
    $files=@([IO.Directory]::EnumerateFiles($Root,'*',[IO.SearchOption]::AllDirectories)|ForEach-Object {[IO.Path]::GetRelativePath($Root,$_)+'|'+(Get-FileHash -LiteralPath $_).Hash})
    return (@($dirs+$files|Sort-Object) -join "`n")
}
function Edit-Json([string]$Path,[scriptblock]$Change){$v=Get-Content -LiteralPath $Path -Raw|ConvertFrom-Json -Depth 32 -DateKind String;& $Change $v;[IO.File]::WriteAllText($Path,(ConvertTo-Json -InputObject $v -Compress -Depth 32),[Text.UTF8Encoding]::new($false))}
function global:Add-Protection($F,[string]$Id,[DateTimeOffset]$Created,[DateTimeOffset]$Until,[hashtable]$Override=@{}){
    $pid2=[Guid]::NewGuid().ToString('N')
    $use=[ordered]@{schemaVersion=1;buildId=$Id;id=$pid2;owner=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value;consumer='fixture-user';reason='fixture';createdAt=$Created.ToString('o');until=$Until.ToString('o')}
    foreach($k in $Override.Keys){$use[$k]=$Override[$k]}
    $null=Write-BuildImmutable (Get-BuildDir $Id) "protections/$pid2.use.json" $use
    return $pid2
}
function Add-PendingIntent([string]$Id,[int]$Attempt=1){
    $receipt=Get-Content -LiteralPath ([IO.Path]::Combine((Get-BuildDir $Id),'receipt.json')) -Raw|ConvertFrom-Json -Depth 32 -DateKind String
    return (Write-BuildImmutable (Get-BuildDir $Id) "deletion/$Attempt.intent.json" ([ordered]@{schemaVersion=1;attempt=$Attempt;buildId=$Id;key=$receipt.key;packageSha256=$receipt.packageSha256;intentAt=$baseNow.ToString('o')})).Sha256
}
# 形とhashの結び付きは正しいが、観測が閉じ方の根拠に合わない試行（記録の改ざん・破損の代わり）を作る。
function Write-FakeAttempt([string]$Id,[string]$Close,$Http,[string]$Class,$S3){
    $intentSha=Add-PendingIntent $Id
    $key=(Get-Content -LiteralPath ([IO.Path]::Combine((Get-BuildDir $Id),'receipt.json')) -Raw|ConvertFrom-Json -DateKind String).key
    $get=[ordered]@{operation='get';key=$key;generation=('f'*32);httpStatus=$Http;statusClass=$Class;s3Code=$S3;statusObserved=$true;timedOut=$false;redirected=$false;observedAt=$baseNow.ToString('o')}
    $v=if($Close -ceq 'deleted'){[ordered]@{schemaVersion=1;attempt=1;intentSha256=$intentSha;sentSha256=$null;deleteObservation='unknown';getObservation=$get}}else{[ordered]@{schemaVersion=1;attempt=1;intentSha256=$intentSha;getObservation=$get}}
    $null=Write-BuildImmutable (Get-BuildDir $Id) "deletion/1.$Close.json" $v
}
function Invoke-ModuleFunction([string]$Module,[scriptblock]$Script,$Argument){$m=@(Get-EvidenceTestModules $Module)[0];return & $m $Script $Argument}
function Get-ChildArgs($F,[string[]]$Cli,[int]$KeepCount=10,[string]$NetworkMode='normal',[string]$Barrier='',[string]$Ready='',[string]$Go=''){
    return @('-NoProfile','-File',[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'BuildCleanupChild.ps1')),'-CliArgs',($Cli -join '|'),'-StoreRoot',$F.StoreRoot,'-TransferRoot',$F.TransferRoot,'-DeploymentRoot',$F.DeploymentRoot,'-RemoteRoot',$F.RemoteRoot,'-OwnedRoots',($F.Owned -join '|'),'-Now',$F.State.Now.ToString('o'),'-KeepCount',[string]$KeepCount,'-NetworkMode',$NetworkMode,'-Barrier',$Barrier,'-ReadyEvent',$Ready,'-GoEvent',$Go)
}
function Get-CleanupCli($F,[switch]$DryRun){$a=@('build','cleanup','--profile','osm','--config',$F.Config,'--project',$series[0],'--target',$series[1],'--configuration',$series[2]);if($DryRun){$a+='--dry-run'};return $a}
function Start-Child([string[]]$ChildArguments){
    $p=[Diagnostics.Process]::new();$p.StartInfo.FileName=(Get-Process -Id $PID).Path;$p.StartInfo.UseShellExecute=$false;$p.StartInfo.CreateNoWindow=$true;$p.StartInfo.RedirectStandardOutput=$true;$p.StartInfo.RedirectStandardError=$true
    foreach($a in $ChildArguments){$p.StartInfo.ArgumentList.Add($a)}
    $null=$p.Start();return @{Process=$p;Output=$p.StandardOutput.ReadToEndAsync();Error=$p.StandardError.ReadToEndAsync()}
}
function Finish-Child($Job){
    try{
        if(-not $Job.Process.WaitForExit(60000)){$Job.Process.Kill($true);$null=$Job.Process.WaitForExit(5000);throw 'child-timeout'}
        Assert ($Job.Output.Wait(5000) -and $Job.Error.Wait(5000)) 'child-pipes'
        return @{Exit=$Job.Process.ExitCode;Output=$Job.Output.Result;Error=$Job.Error.Result}
    }finally{$Job.Process.Dispose()}
}
function Invoke-Child([string[]]$ChildArguments){Finish-Child (Start-Child $ChildArguments)}
function ConvertFrom-ChildJson($Run){$line=@($Run.Output.Trim() -split "`r?`n"|Where-Object {$_ -ne ''})[-1];return $line|ConvertFrom-Json -Depth 20 -DateKind String}
# 並行検査のbarrier。子がReadyを立てるまで待ち、親の操作後にGoで子を進める。
function New-Barrier{$n='osm-cleanup-'+[Guid]::NewGuid().ToString('N');return @{ReadyName=$n+'-ready';GoName=$n+'-go';Ready=[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,$n+'-ready');Go=[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,$n+'-go')}}
function Wait-Ready($Barrier,$Job){if(-not $Barrier.Ready.WaitOne(60000)){try{$Job.Process.Kill($true)}catch{};throw 'child-not-ready'}}
function Get-PublicLeak($Result,$F){$json=ConvertTo-Json -InputObject $Result -Compress -Depth 20;return $json.Contains($F.Root) -or $json.Contains('https://') -or $json.Contains('r2.cloudflarestorage')}

foreach($caseName in $selected){
    $fixture=$null
    try{
        Assert ($caseName -cin $registered) 'unknown-case'
        $fixture=New-CleanupFixture;$f=$fixture
        switch -CaseSensitive ($caseName){
            # AC2・AC5: 成功／未完了／系列不明／他系列の分類と、他系列を数えないこと。
            'catalog-classification'{
                $ids=New-Series $f 2
                $incomplete=New-FixtureBuild $f $f.State.Now -IntentOnly
                $null=New-FixtureBuild $f $f.State.Now -Bare
                [IO.File]::WriteAllBytes([IO.Path]::Combine((Get-BuildRoot),$repo,('9'*32)+'.reserved'),[byte[]]@());Set-ArtifactAcl ([IO.Path]::Combine((Get-BuildRoot),$repo,('9'*32)+'.reserved')) $false
                # receiptの無いdirectoryに削除・保護の記録だけが残っていても、系列不明の未完了として数える（AC5）。
                $orphan=New-FixtureBuild $f $f.State.Now -Bare;$pd=[IO.Path]::Combine((Get-BuildDir $orphan),'protections');[IO.Directory]::CreateDirectory($pd)|Out-Null;Set-ArtifactAcl $pd $true
                $foreign=New-FixtureBuild $f ($f.State.Now.AddMinutes(-30)) -Configuration 'mono-debug'
                $null=New-FixtureBuild $f $f.State.Now -Configuration 'mono-debug' -IntentOnly
                $d=Invoke-Cleanup $f -DryRun
                Assert ($d.status -ceq 'passed' -and $d.reasonCode -ceq 'within-limit' -and $d.keepCount -eq 10) 'dry-run status'
                Assert ($d.retained.count -eq 2 -and (@($d.retained.buildIds) -join ',') -ceq ($ids -join ',')) 'retained order'
                $held=@($d.held.builds)
                Assert ($d.held.count -eq 4 -and @($held|Where-Object {$_.buildId -ceq $incomplete -and $_.reasonCode -ceq 'publish-incomplete'}).Count -eq 1 -and @($held|Where-Object {$null -eq $_.buildId}).Count -eq 3) 'held classification'
                Assert (-not ((ConvertTo-Json $d -Depth 20).Contains($foreign))) 'foreign series counted'
                Set-EvidenceTestValue BuildCleanup KeepCount 1
                $d=Invoke-Cleanup $f -DryRun
                Assert ($d.candidates.count -eq 1 -and $d.candidates.buildIds[0] -ceq $ids[1] -and $d.reasonCode -ceq 'candidates-found') 'candidate'
            }
            # AC3: build-configのbytesが変わっても過去の成功を数える。inspectの既存判定は変えない。
            'catalog-config-independent'{
                $ids=New-Series $f 3
                $op=New-ArtifactOperation;$newConfig=[IO.Path]::Combine($op,'build-config.json')
                [IO.File]::WriteAllText($newConfig,(ConvertTo-Json -InputObject $f.ConfigValue -Depth 4),[Text.UTF8Encoding]::new($false));Set-ArtifactAcl $newConfig $false
                Assert ((Read-BuildConfig $newConfig).Hash -cne $f.ConfigData.Hash) 'config bytes unchanged'
                Set-EvidenceTestValue BuildCleanup KeepCount 2
                $d=Invoke-Cleanup $f -DryRun -Config $newConfig
                Assert ($d.status -ceq 'passed' -and $d.retained.count -eq 2 -and $d.candidates.buildIds[0] -ceq $ids[2]) 'old successes not counted with new config'
                Assert ((Invoke-BuildInspect $newConfig $ids[0]).reasonCode -ceq 'receipt-unavailable') 'inspect judgement changed'
                Assert ((Invoke-BuildInspect $f.Config $ids[0]).status -ceq 'passed') 'inspect old config'
                $x=Invoke-Cleanup $f -Config $newConfig
                Assert ($x.status -ceq 'passed' -and $x.deleted.count -eq 1 -and -not (Test-Remote $f $ids[2])) 'delete with new config'
            }
            # AC3・AC4(a)(b): 指定系列の記録の破損ごとに、何も書かず・送らずに系列をblockedにする。
            'catalog-corrupt-blocks'{
                $variants=[ordered]@{
                    'entries-type'={param($r,$i)Edit-Json $r {param($v)$v.entries='x'}}
                    'entries-duplicate'={param($r,$i)Edit-Json $r {param($v)$v.entries=@($v.entries[0],$v.entries[0])}}
                    'entries-size'={param($r,$i)Edit-Json $r {param($v)$v.entries[0].bytes=-1}}
                    'hash-format'={param($r,$i)Edit-Json $r {param($v)$v.packageSha256='XYZ'}}
                    'config-hash-format'={param($r,$i)Edit-Json $r {param($v)$v.configSha256='not-hex'}}
                    'readback-http'={param($r,$i)Edit-Json $r {param($v)$v.readback.httpStatus=500}}
                    'readback-status'={param($r,$i)Edit-Json $r {param($v)$v.readback.childStatus='failed'}}
                    'intent-mismatch'={param($r,$i)Edit-Json $i {param($v)$v.packageBytes=999}}
                    'receipt-unparsable'={param($r,$i)[IO.File]::WriteAllText($r,'{')}
                    'series-disagree'={param($r,$i)Edit-Json $r {param($v)$v.entries='x';$v.configuration='mono-debug';$v.seriesId=$v.seriesId.Replace('/il2cpp-high','/mono-debug')}}
                    'protection-unknown'={param($r,$i,$id)$null=Add-Protection $f $id $baseNow.AddDays(-1) $baseNow.AddDays(100)}
                    # receiptを失った指定系列のBuildに残る読めない保護も指定系列の記録（AC4(a)）。
                    'receipt-less-protection'={param($r,$i,$id)$null=Add-Protection $f $id $baseNow.AddDays(-1) $baseNow.AddDays(100);[IO.File]::Delete($r)}
                    # 削除済み・取下げの根拠に合わない観測（404の取下げ、200の削除済み）は読めない削除記録。
                    'abandoned-with-404'={param($r,$i,$id)Write-FakeAttempt $id 'abandoned' 404 'not-found' 'NoSuchKey'}
                    'deleted-with-200'={param($r,$i,$id)Write-FakeAttempt $id 'deleted' 200 'success' $null}
                    'delete-record-invalid'={param($r,$i,$id)$d=[IO.Path]::Combine((Get-BuildDir $id),'deletion');[IO.Directory]::CreateDirectory($d)|Out-Null;Set-ArtifactAcl $d $true;[IO.File]::WriteAllText([IO.Path]::Combine($d,'stray.json'),'{}');Set-ArtifactAcl ([IO.Path]::Combine($d,'stray.json')) $false}
                }
                Set-EvidenceTestValue BuildCleanup KeepCount 1
                foreach($name in $variants.Keys){
                    Reset-Store $f $name
                    $good=New-Series $f 2;$bad=New-FixtureBuild $f ($f.State.Now.AddMinutes(-10))
                    $dir=Get-BuildDir $bad
                    & $variants[$name] ([IO.Path]::Combine($dir,'receipt.json')) ([IO.Path]::Combine($dir,'intent.json')) $bad
                    $expected=switch($name){'protection-unknown'{'protection-unknown'};'receipt-less-protection'{'protection-unknown'};'delete-record-invalid'{'delete-record-invalid'};'abandoned-with-404'{'delete-record-invalid'};'deleted-with-200'{'delete-record-invalid'};default{'catalog-incomplete'}}
                    $before=Get-TreeFingerprint $f.StoreRoot;$deletes=Get-Count $f 'delete'
                    $d=Invoke-Cleanup $f -DryRun
                    Assert ($d.status -ceq 'blocked' -and $d.reasonCode -ceq $expected -and $d.candidates.count -eq 0) "dry-run not blocked: $name"
                    $x=Invoke-Cleanup $f
                    Assert ($x.status -ceq 'blocked' -and $x.reasonCode -ceq $expected -and (Get-Count $f 'delete') -eq $deletes) "execution not blocked: $name"
                    Assert (-not @(Get-DeletionFiles $good[1]).Count -and (Test-Remote $f $good[1])) "blocked series wrote or deleted: $name"
                    $after=@((Get-TreeFingerprint $f.StoreRoot) -split "`n"|Where-Object {$_ -notmatch '^d\|.*locks$' -and $_ -notmatch 'locks\\'}) -join "`n"
                    $beforeTrim=@($before -split "`n"|Where-Object {$_ -notmatch '^d\|.*locks$' -and $_ -notmatch 'locks\\'}) -join "`n"
                    Assert ($after -ceq $beforeTrim) "blocked series changed records: $name"
                }
            }
            # AC4: receiptとintentの両方が他系列で一致する破損だけは、止めずに件数を出す。
            'catalog-foreign-invalid'{
                $ids=New-Series $f 2
                $other=New-FixtureBuild $f ($f.State.Now.AddMinutes(-5)) -Configuration 'mono-debug'
                Edit-Json ([IO.Path]::Combine((Get-BuildDir $other),'receipt.json')) {param($v)$v.entries='x'}
                # 他系列の有効な成功Buildに残る読めない保護記録も、止めずに件数だけ出す。
                $otherOk=New-FixtureBuild $f ($f.State.Now.AddMinutes(-6)) -Configuration 'mono-debug';$null=Add-Protection $f $otherOk $baseNow.AddDays(-1) $baseNow.AddDays(100)
                Set-EvidenceTestValue BuildCleanup KeepCount 1
                $d=Invoke-Cleanup $f -DryRun
                Assert ($d.status -ceq 'passed' -and $d.foreignInvalid -eq 2 -and $d.candidates.buildIds[0] -ceq $ids[1]) 'foreign corruption blocked series'
                $x=Invoke-Cleanup $f
                Assert ($x.status -ceq 'passed' -and $x.foreignInvalid -eq 2 -and -not (Test-Remote $f $ids[1]) -and (Test-Remote $f $ids[0])) 'foreign corruption stopped deletion'
                # intentだけ指定系列を名乗る食い違いは系列不明として止める。
                Edit-Json ([IO.Path]::Combine((Get-BuildDir $other),'intent.json')) {param($v)$v.configuration='il2cpp-high';$v.seriesId=$v.seriesId.Replace('/mono-debug','/il2cpp-high')}
                Assert ((Invoke-Cleanup $f -DryRun).reasonCode -ceq 'catalog-incomplete') 'disagreeing records treated as foreign'
            }
            # AC4(c): 未知のentry・reparse・ACL不一致はblocked。
            'catalog-unknown-entry'{
                foreach($variant in @('stray-file','junction','acl','stray-dir','reserved-acl','locks-acl')){
                    Reset-Store $f $variant
                    $null=New-Series $f 2
                    $repoDir=[IO.Path]::Combine((Get-BuildRoot),$repo)
                    switch($variant){
                        'stray-file'{[IO.File]::WriteAllText([IO.Path]::Combine($repoDir,'stray.txt'),'x')}
                        'junction'{$target=[IO.Path]::Combine($f.Root,'outside-'+$variant);[IO.Directory]::CreateDirectory($target)|Out-Null;$null=New-Item -ItemType Junction -Path ([IO.Path]::Combine($repoDir,('8'*32))) -Target $target}
                        'acl'{[IO.Directory]::CreateDirectory([IO.Path]::Combine($repoDir,('7'*32)))|Out-Null}
                        # 既知の名前でもACLが専用所有でなければ止める（継承ACLのまま作る）。
                        'reserved-acl'{[IO.File]::WriteAllText([IO.Path]::Combine($repoDir,('6'*32)+'.reserved'),'')}
                        'locks-acl'{[IO.Directory]::CreateDirectory([IO.Path]::Combine($repoDir,'locks'))|Out-Null}
                        'stray-dir'{$p=[IO.Path]::Combine($repoDir,'other');[IO.Directory]::CreateDirectory($p)|Out-Null;Set-ArtifactAcl $p $true}
                    }
                    $d=Invoke-Cleanup $f -DryRun
                    Assert ($d.status -ceq 'blocked' -and $d.reasonCode -ceq 'catalog-incomplete') "unknown entry not blocked: $variant"
                    if($variant -ceq 'junction'){[IO.Directory]::Delete([IO.Path]::Combine($repoDir,('8'*32)))}
                }
            }
            # AC6: 未来時刻のpublishedAtは系列をclock-skewで止める。
            'catalog-clock-skew'{
                $null=New-Series $f 2;$null=New-FixtureBuild $f ($f.State.Now.AddHours(1))
                Set-EvidenceTestValue BuildCleanup KeepCount 1
                $x=Invoke-Cleanup $f
                Assert ($x.status -ceq 'blocked' -and $x.reasonCode -ceq 'clock-skew' -and (Get-Count $f 'delete') -eq 0) 'future publishedAt not blocked'
            }
            # AC8: 保護の有効判定の表（期限境界・巻戻り・release・破損）。
            'protection-rules'{
                $id='5'*32;$now=$baseNow;$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
                $make={param($created,$until,$released=$null,[hashtable]$o=@{})
                    $use=[ordered]@{schemaVersion=[long]1;buildId=$id;id=('6'*32);owner=$sid;consumer='user-1';reason='keep';createdAt=$created;until=$until};foreach($k in $o.Keys){$use[$k]=$o[$k]}
                    $rel=if($released){[pscustomobject][ordered]@{schemaVersion=[long]1;buildId=$id;id=('6'*32);releasedAt=$released}}else{$null}
                    [pscustomobject]@{Id=('6'*32);Use=[pscustomobject]$use;Release=$rel}}
                $t={param($d)$now.AddSeconds($d).ToString('o')}
                $rows=@(
                    @('active',@(& $make (& $t -60) (& $t 60)),'protected'),
                    @('until-boundary',@(& $make (& $t -60) (& $t 0)),'unprotected'),
                    @('expired',@(& $make (& $t -60) (& $t -1)),'unprotected'),
                    @('rewound-clock',@(& $make (& $t 3600) (& $t 7200)),'protected'),
                    @('released',@(& $make (& $t -60) (& $t 60) (& $t -1)),'unprotected'),
                    @('none',@(),'unprotected'),
                    @('created-after-until',@(& $make (& $t 60) (& $t 60)),'unknown'),
                    @('over-90-days',@(& $make (& $t -60) ($now.AddDays(90).AddSeconds(1).ToString('o'))),'unknown'),
                    @('exactly-90-days',@(& $make (& $t 0) ($now.AddDays(90).ToString('o'))),'protected'),
                    @('released-before-created',@(& $make (& $t -60) (& $t 60) (& $t -120)),'unknown'),
                    @('non-utc',@(& $make ($now.ToOffset([TimeSpan]::FromHours(9)).ToString('o')) (& $t 60)),'unknown'),
                    @('bad-consumer',@(& $make (& $t -60) (& $t 60) $null @{consumer='bad id'}),'unknown'),
                    @('other-build',@(& $make (& $t -60) (& $t 60) $null @{buildId=('4'*32)}),'unknown'),
                    @('one-bad-of-two',@((& $make (& $t -60) (& $t 60)),(& $make (& $t -60) (& $t 60) $null @{reason=''})),'unknown')
                )
                foreach($row in $rows){$state=Get-BuildProtectionState $row[1] $id $now;Assert ($state -ceq $row[2]) "protection rule $($row[0]): $state"}
            }
            # AC7: use/releaseの検査・追記だけの記録・保護中Buildの非削除と計数。
            'protection-use-release'{
                $ids=New-Series $f 3;Set-EvidenceTestValue BuildCleanup KeepCount 2
                $until=$f.State.Now.AddDays(1).ToString('o')
                foreach($bad in @(@($ids[2],'user',$f.State.Now.AddMinutes(-1).ToString('o'),'r'),@($ids[2],'user',$f.State.Now.AddDays(91).ToString('o'),'r'),@($ids[2],'bad id!',$until,'r'),@($ids[2],'user',$until,"a`nb"),@($ids[2],'user','2026-10-12','r'))){
                    $u=Invoke-BuildUse $f.Config $bad[0] $bad[1] $bad[2] $bad[3];Assert ($u.status -ceq 'failed' -and $u.reasonCode -ceq 'invalid-use') "invalid use accepted: $($bad[1]) $($bad[2])"
                }
                $incomplete=New-FixtureBuild $f $f.State.Now -IntentOnly
                Assert ((Invoke-BuildUse $f.Config $incomplete 'user' $until 'r').reasonCode -ceq 'build-not-retainable') 'use on incomplete'
                Assert ((Invoke-BuildUse $f.Config ('0'*32) 'user' $until 'r').reasonCode -ceq 'build-not-retainable') 'use on unknown'
                # temp書込み中に期限を過ぎた保護は、renameの直前に検査して確定させない。
                $short=$f.State.Now.AddMinutes(1).ToString('o');$state=$f.State
                Set-EvidenceTestValue BuildStore FaultHook ({param($where,$name)if($where -ceq 'before-rename' -and $name.StartsWith('protections/',[StringComparison]::Ordinal)){$state.Now=$state.Now.AddMinutes(2)}}.GetNewClosure())
                $late=Invoke-BuildUse $f.Config $ids[2] 'user-1' $short 'expires during write'
                Set-EvidenceTestValue BuildStore FaultHook $null;$f.State.Now=$baseNow
                Assert ($late.reasonCode -ceq 'invalid-use' -and @(Read-BuildProtectionRecords $repo $ids[2]).Count -eq 0) 'expired protection committed'
                # 書込み中に時計が巻き戻った場合も、rename時点でcreatedAtより前なら確定しない。
                Set-EvidenceTestValue BuildStore FaultHook ({param($where,$name)if($where -ceq 'before-rename' -and $name.StartsWith('protections/',[StringComparison]::Ordinal)){$state.Now=$state.Now.AddDays(-1)}}.GetNewClosure())
                $rewound=Invoke-BuildUse $f.Config $ids[2] 'user-1' $until 'clock rewinds during write'
                Set-EvidenceTestValue BuildStore FaultHook $null;$f.State.Now=$baseNow
                Assert ($rewound.reasonCode -ceq 'invalid-use' -and @(Read-BuildProtectionRecords $repo $ids[2]).Count -eq 0) 'protection committed after clock rewind'
                $u=Invoke-BuildUse $f.Config $ids[2] 'user-1' $until 'keep for test'
                Assert ($u.status -ceq 'passed' -and $u.protection -cmatch '\A[0-9a-f]{32}\z') 'use failed'
                $usePath=[IO.Path]::Combine((Get-BuildDir $ids[2]),'protections',$u.protection+'.use.json');$useHash=(Get-FileHash -LiteralPath $usePath).Hash
                $d=Invoke-Cleanup $f -DryRun
                Assert ($d.retained.count -eq 2 -and @($d.retained.buildIds) -contains $ids[2] -and $d.candidates.buildIds[0] -ceq $ids[1]) 'protected build not counted in N'
                $x=Invoke-Cleanup $f
                Assert ($x.status -ceq 'passed' -and (Test-Remote $f $ids[2]) -and -not (Test-Remote $f $ids[1])) 'protected build deleted'
                Assert ((Invoke-BuildUse $f.Config $ids[1] 'user' $until 'r').reasonCode -ceq 'build-not-retainable') 'use on deleted build'
                $null=Add-PendingIntent $ids[0]
                Assert ((Invoke-BuildUse $f.Config $ids[0] 'user' $until 'r').reasonCode -ceq 'build-not-retainable') 'use on deleting build'
                Assert ((Invoke-BuildRelease $f.Config $ids[2] ('0'*32)).reasonCode -ceq 'unknown-protection') 'unknown protection released'
                $f.State.Now=$baseNow.AddMinutes(-1)
                Assert ((Invoke-BuildRelease $f.Config $ids[2] $u.protection).reasonCode -ceq 'clock-skew') 'release before createdAt written'
                $f.State.Now=$baseNow.AddMinutes(1)
                Assert ((Invoke-BuildRelease $f.Config $ids[2] $u.protection).status -ceq 'passed') 'release failed'
                Assert ((Invoke-BuildRelease $f.Config $ids[2] $u.protection).status -ceq 'passed') 'release not idempotent'
                $files=@([IO.Directory]::EnumerateFiles([IO.Path]::Combine((Get-BuildDir $ids[2]),'protections'))|ForEach-Object {[IO.Path]::GetFileName($_)}|Where-Object {$_ -notlike '*.tmp'}|Sort-Object)
                Assert ($files.Count -eq 2 -and (Get-FileHash -LiteralPath $usePath).Hash -ceq $useHash) 'protection records not append-only'
                $records=@(Read-BuildProtectionRecords $repo $ids[2]);Assert ((Get-BuildProtectionState $records $ids[2] $f.State.Now) -ceq 'unprotected') 'release not effective'
            }
            # AC10: dry-runはlock・記録・networkに触れず、実行と同じ分類を返す。
            'dry-run-no-writes'{
                $ids=New-Series $f 3;Set-EvidenceTestValue BuildCleanup KeepCount 1
                $before=Get-TreeFingerprint $f.StoreRoot
                $d=Invoke-Cleanup $f -DryRun
                Assert ($d.status -ceq 'passed' -and $d.candidates.count -eq 2 -and $d.candidates.buildIds[0] -ceq $ids[2] -and $d.candidates.buildIds[1] -ceq $ids[1] -and $d.remaining -eq 2) 'dry-run classification'
                Assert ($d.requests.delete -eq 0 -and $d.requests.get -eq 0 -and $f.Counts.Count -eq 0) 'dry-run used network'
                Assert ((Get-TreeFingerprint $f.StoreRoot) -ceq $before -and -not [IO.Directory]::Exists([IO.Path]::Combine((Get-BuildRoot),$repo,'locks'))) 'dry-run wrote store'
            }
            # AC9・AC14・AC17・AC20と対象外の維持: 既定N=10で最古1件だけを削除し、再実行は送信0。
            'delete-oldest-first'{
                $ids=New-Series $f 11
                $artifactsRoot=[IO.Path]::Combine($f.Root,'artifacts-root','players','samplegame','windows-x64-player','il2cpp-high',$ids[10]);[IO.Directory]::CreateDirectory($artifactsRoot)|Out-Null;[IO.File]::WriteAllText([IO.Path]::Combine($artifactsRoot,'build-info.json'),'{}')
                $contents=[IO.Path]::Combine($f.Root,'contents-v1',$repo);[IO.Directory]::CreateDirectory($contents)|Out-Null;[IO.File]::WriteAllText([IO.Path]::Combine($contents,'receipt.json'),'{}')
                $outside=@{};foreach($p in @([IO.Path]::Combine($f.Root,'artifacts-root'),[IO.Path]::Combine($f.Root,'contents-v1'))){$outside[$p]=Get-TreeFingerprint $p}
                $x=Invoke-Cleanup $f
                Assert ($x.status -ceq 'passed' -and $x.reasonCode -ceq 'within-limit' -and $x.keepCount -eq 10) 'default N cleanup status'
                Assert (@($x.outcomes).Count -eq 1 -and $x.outcomes[0].buildId -ceq $ids[10] -and $x.outcomes[0].outcome -ceq 'deleted') 'oldest not deleted'
                Assert ($x.requests.delete -eq 1 -and $x.requests.get -eq 1 -and $x.deleted.count -eq 1 -and $x.deleted.buildIds[0] -ceq $ids[10] -and $x.retained.count -eq 10) 'counts'
                Assert (-not (Test-Remote $f $ids[10]) -and @($ids[0..9]|Where-Object {-not (Test-Remote $f $_)}).Count -eq 0) 'wrong remote objects'
                Assert (((Get-DeletionFiles $ids[10]) -join ',') -ceq '1.deleted.json,1.intent.json,1.sent.json') 'deletion records'
                Assert ([IO.File]::Exists([IO.Path]::Combine((Get-BuildDir $ids[10]),'receipt.json')) -and [IO.File]::Exists([IO.Path]::Combine((Get-BuildDir $ids[10]),'intent.json'))) 'local records removed'
                # 記録だけからの再評価: 削除済みはGETの404、送信観測は検証済み2xx。
                $del=Get-Content -LiteralPath ([IO.Path]::Combine((Get-BuildDir $ids[10]),'deletion','1.deleted.json')) -Raw|ConvertFrom-Json -Depth 8 -DateKind String
                $sent=Get-Content -LiteralPath ([IO.Path]::Combine((Get-BuildDir $ids[10]),'deletion','1.sent.json')) -Raw|ConvertFrom-Json -Depth 8 -DateKind String
                $g=Invoke-ModuleFunction BuildCleanup {param($o)ConvertFrom-BuildObservation $o} $del.getObservation
                $s=Invoke-ModuleFunction BuildCleanup {param($o)ConvertFrom-BuildObservation $o} $sent.observation
                Assert ((Test-ArtifactMissing $g $del.getObservation.generation) -and (Test-ArtifactSuccess $s 'delete' $sent.observation.generation) -and $del.deleteObservation -ceq 'recorded') 'records not re-evaluable'
                $again=Invoke-Cleanup $f
                Assert ($again.status -ceq 'passed' -and $again.requests.delete -eq 0 -and $again.requests.get -eq 0 -and $again.deleted.count -eq 1) 'rerun sent requests'
                Assert ((Get-Count $f 'delete' 'development/content/') -eq 0) 'Content DELETE sent'
                foreach($p in $outside.Keys){Assert ((Get-TreeFingerprint $p) -ceq $outside[$p]) 'outside layer changed'}
                $names=@($x.PSObject.Properties.Name|Sort-Object) -join ','
                Assert ($names -ceq 'candidates,deleted,deleting,excessCount,foreignInvalid,held,keepCount,mode,outcomes,reasonCode,remaining,requests,retained,seriesId,status') "result fields: $names"
                Assert (-not (Get-PublicLeak $x $f)) 'result leaked path or endpoint'
            }
            # 最低条件2: 保護だけでNを超える間はexcessCountを出して正常終了し、保護を外さない。
            'protected-over-limit'{
                $ids=New-Series $f 4;Set-EvidenceTestValue BuildCleanup KeepCount 2
                foreach($i in 0..2){$null=Add-Protection $f $ids[$i] $baseNow.AddMinutes(-1) $baseNow.AddDays(1)}
                $x=Invoke-Cleanup $f
                Assert ($x.status -ceq 'passed' -and $x.reasonCode -ceq 'protected-over-limit' -and $x.excessCount -eq 1 -and $x.retained.count -eq 3) 'over-limit result'
                Assert (@($ids[0..2]|Where-Object {-not (Test-Remote $f $_)}).Count -eq 0 -and -not (Test-Remote $f $ids[3])) 'protected deleted or unprotected kept'
                Assert ($x.outcomes[0].buildId -ceq $ids[3]) 'unprotected not deleted'
            }
            # AC15(a)・AC16: DELETEが検証済み2xxでなければ未解決で止め、後続を処理しない。次回はGETから。
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
                Set-EvidenceTestValue BuildCleanup KeepCount 1
                foreach($mode in $modes.Keys){
                    Reset-Store $f $mode;$f.Counts.Clear()
                    $ids=New-Series $f 3;$behavior=$modes[$mode]
                    $f.State.Hook={param($g,$r,$p)if($r.Operation -ceq 'delete'){& $behavior $g $r $p}}.GetNewClosure()
                    $x=Invoke-Cleanup $f
                    Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'delete-pending' -and $x.outcomes[0].buildId -ceq $ids[2] -and $x.outcomes[0].outcome -ceq 'delete-pending') "pending result: $mode"
                    Assert ($x.requests.delete -eq 1 -and $x.requests.get -eq 0 -and @($x.outcomes).Count -eq 1 -and -not @(Get-DeletionFiles $ids[1]).Count) "continued after failure: $mode"
                    Assert ($x.deleting.count -eq 1 -and $x.deleting.buildIds[0] -ceq $ids[2] -and $x.remaining -eq 1) "deleting not excluded from counts: $mode"
                    $expectSent=$mode -cnotin @('exception','lost-after-delete')
                    Assert ((@(Get-DeletionFiles $ids[2]) -contains '1.sent.json') -eq $expectSent) "sent record: $mode"
                    $f.State.Hook=$null
                    $y=Invoke-Cleanup $f
                    Assert ($y.status -ceq 'passed' -and $y.deleted.count -eq 2 -and -not (Test-Remote $f $ids[2]) -and -not (Test-Remote $f $ids[1]) -and (Test-Remote $f $ids[0])) "resume did not finish: $mode"
                    if($mode -ceq 'lost-after-delete'){
                        Assert ($y.requests.delete -eq 1 -and $y.outcomes[0].outcome -ceq 'deleted' -and ((Get-DeletionFiles $ids[2]) -join ',') -ceq '1.deleted.json,1.intent.json') 'resume after lost response'
                        $rec=Get-Content -LiteralPath ([IO.Path]::Combine((Get-BuildDir $ids[2]),'deletion','1.deleted.json')) -Raw|ConvertFrom-Json -Depth 8 -DateKind String
                        Assert ($rec.deleteObservation -ceq 'unknown' -and $null -eq $rec.sentSha256) 'unknown delete observation'
                    }else{
                        # objectが残っていた: 再開のGET 200で取下げ、今も候補なので新しい試行で消す。
                        Assert ($y.outcomes[0].outcome -ceq 'abandoned' -and $y.outcomes[1].buildId -ceq $ids[2] -and $y.outcomes[1].outcome -ceq 'deleted' -and $y.requests.delete -eq 2) "resume sequence: $mode"
                        Assert (@(Get-DeletionFiles $ids[2]) -contains '1.abandoned.json' -and @(Get-DeletionFiles $ids[2]) -contains '2.deleted.json') "attempt records: $mode"
                    }
                }
            }
            # AC15(b)・手順6: 確認GETが判定できなければ未解決。DELETE 2xx後もobjectが残れば取下げ、同じrunで再試行しない。
            'get-unconfirmed'{
                Set-EvidenceTestValue BuildCleanup KeepCount 1
                Reset-Store $f 'get-500';$ids=New-Series $f 3
                $f.State.Hook={param($g,$r,$p)if($r.Operation -ceq 'get'){New-Response 'get' $g 500 'server-error'}}
                $x=Invoke-Cleanup $f
                Assert ($x.status -ceq 'partial' -and $x.outcomes[0].outcome -ceq 'delete-pending' -and @($x.outcomes).Count -eq 1 -and ((Get-DeletionFiles $ids[2]) -join ',') -ceq '1.intent.json,1.sent.json') 'GET 500 not pending'
                Reset-Store $f 'get-generation';$ids=New-Series $f 3
                $f.State.Hook={param($g,$r,$p)if($r.Operation -ceq 'get'){New-Response 'get' ('e'*32) 200 'success'}elseif($r.Operation -ceq 'delete'){New-Response 'delete' $g 204 'success'}}
                $x=Invoke-Cleanup $f
                Assert ($x.status -ceq 'partial' -and $x.outcomes[0].outcome -ceq 'delete-pending' -and -not (@(Get-DeletionFiles $ids[2]) -contains '1.abandoned.json')) 'GET 200 with other generation abandoned'
                Reset-Store $f 'remains';$f.Counts.Clear();$ids=New-Series $f 3;$keep=Get-KeyOf $ids[2]
                $f.State.Hook={param($g,$r,$p)if($r.Operation -ceq 'delete' -and $r.Key -ceq $keep){New-Response 'delete' $g 204 'success'}}.GetNewClosure()
                $x=Invoke-Cleanup $f
                Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'delete-unconfirmed' -and $x.outcomes[0].outcome -ceq 'delete-unconfirmed' -and $x.outcomes[1].buildId -ceq $ids[1] -and $x.outcomes[1].outcome -ceq 'deleted') 'object remained handling'
                Assert ($x.requests.delete -eq 2 -and @($x.outcomes|Where-Object buildId -CEQ $ids[2]).Count -eq 1 -and (Test-Remote $f $ids[2]) -and $x.remaining -eq 1) 'retried in same run'
                Assert (((Get-DeletionFiles $ids[2]) -join ',') -ceq '1.abandoned.json,1.intent.json,1.sent.json') 'abandon records'
            }
            # 再開のGET 200で取下げた後、計算し直して候補でなければ新しい試行を始めず計数へ戻す。
            'resume-abandon-recompute'{
                $ids=New-Series $f 3;Set-EvidenceTestValue BuildCleanup KeepCount 2
                $f.State.Hook={param($g,$r,$p)if($r.Operation -ceq 'delete'){New-Response 'delete' $g 403 'forbidden'}}
                $x=Invoke-Cleanup $f;Assert ($x.status -ceq 'partial' -and $x.deleting.buildIds[0] -ceq $ids[2]) 'first run'
                $f.State.Hook=$null;Set-EvidenceTestValue BuildCleanup KeepCount 3
                $y=Invoke-Cleanup $f
                Assert ($y.status -ceq 'passed' -and $y.reasonCode -ceq 'within-limit' -and @($y.outcomes).Count -eq 1 -and $y.outcomes[0].outcome -ceq 'abandoned' -and $y.requests.delete -eq 0 -and $y.requests.get -eq 1 -and $y.retained.count -eq 3) 'abandoned build not returned to counts'
                Assert ((Test-Remote $f $ids[2]) -and ((Get-DeletionFiles $ids[2]) -join ',') -ceq '1.abandoned.json,1.intent.json,1.sent.json') 'abandon state'
                Set-EvidenceTestValue BuildCleanup KeepCount 2
                $z=Invoke-Cleanup $f
                Assert ($z.status -ceq 'passed' -and -not (Test-Remote $f $ids[2]) -and @(Get-DeletionFiles $ids[2]) -contains '2.deleted.json') 'new attempt after abandon'
            }
            # AC15(c): intent確定後の記録の書込み失敗は未解決のまま止め、次回の再開で確定させる。
            'record-write-failures'{
                Set-EvidenceTestValue BuildCleanup KeepCount 1
                foreach($spec in @(@('intent','before-rename'),@('intent','after-rename'),@('sent','before-rename'),@('deleted','before-rename'),@('abandoned','before-rename'))){
                    Reset-Store $f ($spec -join '-');$f.Counts.Clear();$ids=New-Series $f 3
                    $target="deletion/1.$($spec[0]).json";$phase=$spec[1]
                    Set-EvidenceTestValue BuildStore FaultHook ({param($where,$name)if($name -ceq $target -and $where -ceq $phase){throw 'injected-write-failure'}}.GetNewClosure())
                    if($spec[0] -ceq 'abandoned'){$keep=Get-KeyOf $ids[2];$f.State.Hook={param($g,$r,$p)if($r.Operation -ceq 'delete' -and $r.Key -ceq $keep){New-Response 'delete' $g 204 'success'}}.GetNewClosure()}
                    $x=Invoke-Cleanup $f
                    Set-EvidenceTestValue BuildStore FaultHook $null;$f.State.Hook=$null
                    Assert ($x.status -ceq 'partial' -and @($x.outcomes).Count -eq 1 -and $x.outcomes[0].outcome -ceq 'delete-pending') "write failure not pending: $($spec -join '-')"
                    $files=@(Get-DeletionFiles $ids[2])
                    switch($spec[0]){
                        'intent'{Assert ($x.requests.delete -eq 0 -and (($files -contains '1.intent.json') -eq ($phase -ceq 'after-rename'))) "intent failure sent DELETE: $phase"}
                        'sent'{Assert ($x.requests.delete -eq 1 -and $x.requests.get -eq 0 -and -not ($files -contains '1.sent.json')) 'sent failure continued'}
                        'deleted'{Assert ($x.requests.get -eq 1 -and -not ($files -contains '1.deleted.json') -and -not (Test-Remote $f $ids[2])) 'deleted failure'}
                        'abandoned'{Assert (-not ($files -contains '1.abandoned.json') -and (Test-Remote $f $ids[2])) 'abandoned failure'}
                    }
                    $y=Invoke-Cleanup $f
                    Assert ($y.status -ceq 'passed' -and -not (Test-Remote $f $ids[2]) -and -not (Test-Remote $f $ids[1]) -and $y.deleted.count -eq 2) "not recovered: $($spec -join '-')"
                }
            }
            # AC18: 表の予算判定点ごとに、超過なら書かず・送らずに未解決で止める。
            'budget-points'{
                Set-EvidenceTestValue BuildCleanup KeepCount 1
                foreach($point in @('intent','delete','sent','get','deleted','abandoned')){
                    Reset-Store $f $point;$f.Counts.Clear();$ids=New-Series $f 3
                    Set-EvidenceTestValue BuildCleanup BudgetHook ({param($at)if($at -ceq $point){throw 'budget-exceeded'}}.GetNewClosure())
                    if($point -ceq 'abandoned'){$keep=Get-KeyOf $ids[2];$f.State.Hook={param($g,$r,$p)if($r.Operation -ceq 'delete' -and $r.Key -ceq $keep){New-Response 'delete' $g 204 'success'}}.GetNewClosure()}
                    $x=Invoke-Cleanup $f
                    Set-EvidenceTestValue BuildCleanup BudgetHook $null;$f.State.Hook=$null
                    $files=@(Get-DeletionFiles $ids[2])
                    Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'budget-exceeded' -and $x.outcomes[0].outcome -ceq 'budget-exceeded' -and @($x.outcomes).Count -eq 1) "budget result: $point"
                    $expected=switch($point){'intent'{@(0,0,'')};'delete'{@(0,0,'1.intent.json')};'sent'{@(1,0,'1.intent.json')};'get'{@(1,0,'1.intent.json,1.sent.json')};'deleted'{@(1,1,'1.intent.json,1.sent.json')};'abandoned'{@(1,1,'1.intent.json,1.sent.json')}}
                    Assert ($x.requests.delete -eq $expected[0] -and $x.requests.get -eq $expected[1] -and ($files -join ',') -ceq $expected[2]) "budget point $point wrote or sent: $($files -join ',') $($x.requests.delete)/$($x.requests.get)"
                    $y=Invoke-Cleanup $f
                    Assert ($y.status -ceq 'passed' -and $y.deleted.count -eq 2) "budget not recovered: $point"
                }
                Reset-Store $f 'timer';$ids=New-Series $f 3
                Set-EvidenceTestValue BuildCleanup BudgetMilliseconds 0
                $x=Invoke-Cleanup $f
                Set-EvidenceTestValue BuildCleanup BudgetMilliseconds 600000
                Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'budget-exceeded' -and $x.requests.delete -eq 0 -and -not @(Get-DeletionFiles $ids[2]).Count) 'elapsed budget not enforced'
            }
            # AC18: 1回の実行のDELETEは取下げ後の新しい試行を含めて最大20回。残りはremaining。
            'delete-limit'{
                Set-EvidenceTestValue BuildCleanup KeepCount 1
                $ids=New-Series $f 23
                $null=Add-PendingIntent $ids[22]
                $x=Invoke-Cleanup $f
                Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'delete-limit' -and $x.requests.delete -eq 20 -and $x.requests.get -eq 21 -and $x.remaining -eq 2 -and $x.deleted.count -eq 20) "limit result $($x.requests.delete) $($x.remaining)"
                Assert ($x.outcomes[0].outcome -ceq 'abandoned' -and $x.outcomes[1].buildId -ceq $ids[22] -and $x.outcomes[1].outcome -ceq 'deleted') 'resumed retry not counted first'
                $y=Invoke-Cleanup $f
                Assert ($y.status -ceq 'passed' -and $y.requests.delete -eq 2 -and $y.deleted.count -eq 22 -and (Test-Remote $f $ids[0])) 'second run'
            }
            # AC11: 系列lockを別processが持つ間、cleanupとuseは待たずにcleanup-in-progress。
            'lock-contention'{
                $ids=New-Series $f 3;$heldProtection=Add-Protection $f $ids[0] $baseNow.AddMinutes(-1) $baseNow.AddDays(1)
                $b=New-Barrier
                try{
                    $job=Start-Child (Get-ChildArgs $f (Get-CleanupCli $f) 1 'normal' 'delete' $b.ReadyName $b.GoName)
                    Wait-Ready $b $job
                    $x=Invoke-Cleanup $f
                    Assert ($x.status -ceq 'blocked' -and $x.reasonCode -ceq 'cleanup-in-progress' -and $x.requests.delete -eq 0) 'second cleanup ran'
                    Assert ((Invoke-BuildUse $f.Config $ids[0] 'user' $f.State.Now.AddDays(1).ToString('o') 'r').reasonCode -ceq 'cleanup-in-progress') 'use ran during cleanup'
                    # releaseで保持枠が空くと候補が入れ替わるので、cleanup中のreleaseも待たずに止める。
                    Assert ((Invoke-BuildRelease $f.Config $ids[0] $heldProtection).reasonCode -ceq 'cleanup-in-progress') 'release ran during cleanup'
                    Assert ((Invoke-Cleanup $f -DryRun).status -ceq 'passed') 'dry-run took lock'
                    $null=$b.Go.Set();$done=Finish-Child $job
                    $child=ConvertFrom-ChildJson $done
                    Assert ($done.Exit -eq 0 -and $child.status -ceq 'passed' -and $child.deleted.count -eq 2) "child cleanup: $($done.Output)"
                }finally{$null=$b.Go.Set();$b.Ready.Dispose();$b.Go.Dispose()}
                $b=New-Barrier
                try{
                    $job=Start-Child (Get-ChildArgs $f @('build','use','--profile','osm','--config',$f.Config,'--build-id',$ids[0],'--consumer','child','--until',$f.State.Now.AddDays(1).ToString('o'),'--reason','child') 1 'normal' 'use-rename' $b.ReadyName $b.GoName)
                    Wait-Ready $b $job
                    $x=Invoke-Cleanup $f
                    Assert ($x.status -ceq 'blocked' -and $x.reasonCode -ceq 'cleanup-in-progress') 'cleanup ran during use'
                    $null=$b.Go.Set();$done=Finish-Child $job
                    Assert ($done.Exit -eq 0 -and (ConvertFrom-ChildJson $done).status -ceq 'passed') "child use: $($done.Output)"
                }finally{$null=$b.Go.Set();$b.Ready.Dispose();$b.Go.Dispose()}
            }
            # AC13・AC21: この PC の取得中は飛ばし、削除試行中の取得・照会は待たずに失敗する。
            'access-lock'{
                $ids=New-Series $f 3;Set-EvidenceTestValue BuildCleanup KeepCount 1
                $receipt=Read-BuildReceipt $f.ConfigData $ids[2];$reference=New-BuildReference $f.ConfigData $receipt.Data
                $b=New-Barrier
                try{
                    $job=Start-Child (Get-ChildArgs $f @('build','fetch','--profile','osm','--config',$f.Config,'--reference',$reference,'--sha256',$receipt.Data.packageSha256) 1 'normal' 'get' $b.ReadyName $b.GoName)
                    Wait-Ready $b $job
                    $x=Invoke-Cleanup $f
                    Assert ($x.status -ceq 'partial' -and $x.reasonCode -ceq 'in-transfer' -and $x.outcomes[0].buildId -ceq $ids[2] -and $x.outcomes[0].outcome -ceq 'in-transfer' -and $x.outcomes[1].buildId -ceq $ids[1] -and $x.outcomes[1].outcome -ceq 'deleted') 'in-transfer not skipped'
                    Assert ((Test-Remote $f $ids[2]) -and -not @(Get-DeletionFiles $ids[2]).Count) 'in-transfer build touched'
                    $null=$b.Go.Set();$null=Finish-Child $job
                }finally{$null=$b.Go.Set();$b.Ready.Dispose();$b.Go.Dispose()}
                $b=New-Barrier
                try{
                    $job=Start-Child (Get-ChildArgs $f (Get-CleanupCli $f) 1 'normal' 'delete' $b.ReadyName $b.GoName)
                    Wait-Ready $b $job
                    $gets=Get-Count $f 'get'
                    $fetch=Invoke-BuildFetch $f.Config $reference $receipt.Data.packageSha256
                    Assert ($fetch.status -ceq 'failed' -and $fetch.reasonCode -ceq 'delete-pending' -and (Get-Count $f 'get') -eq $gets) 'fetch did not fail during delete attempt'
                    $i=Invoke-BuildInspect $f.Config $ids[2]
                    Assert ($i.status -ceq 'failed' -and $i.reasonCode -ceq 'delete-pending' -and -not $i.PSObject.Properties['reference']) 'inspect during delete attempt'
                    $null=$b.Go.Set();$done=Finish-Child $job
                    Assert ($done.Exit -eq 0 -and -not (Test-Remote $f $ids[2])) "child cleanup: $($done.Output)"
                }finally{$null=$b.Go.Set();$b.Ready.Dispose();$b.Go.Dispose()}
                $i=Invoke-BuildInspect $f.Config $ids[2]
                Assert ($i.status -ceq 'failed' -and $i.reasonCode -ceq 'payload-deleted') 'inspect after delete'
                # 結果を呼出側へ渡す時点（CLIの出力）でも共有lockを保持し、削除意図を確定させない。
                $probe=[pscustomobject]@{busy=$null;status=$null}
                $emit={param($o)$probe.status=$o.status;try{$h=Enter-BuildAccessLock ('a'*64) $o.buildId $true;$h.Dispose();$probe.busy=$false}catch{$probe.busy=($_.Exception.Message -ceq 'lock-busy')}}.GetNewClosure()
                $i=Invoke-BuildInspect $f.Config $ids[0] $emit
                Assert ($i.status -ceq 'passed' -and $probe.status -ceq 'passed' -and $probe.busy -eq $true) 'inspect released the access lock before emitting'
                # 削除済みと答える場合も同じく、lockを保持したまま結果を渡す。
                $probe.busy=$null;$probe.status=$null;$target=$ids[2]
                $emitDeleted={param($o)$probe.status=$o.reasonCode;try{$h=Enter-BuildAccessLock ('a'*64) $target $true;$h.Dispose();$probe.busy=$false}catch{$probe.busy=($_.Exception.Message -ceq 'lock-busy')}}.GetNewClosure()
                $i=Invoke-BuildInspect $f.Config $ids[2] $emitDeleted
                Assert ($i.reasonCode -ceq 'payload-deleted' -and $probe.status -ceq 'payload-deleted' -and $probe.busy -eq $true) 'inspect released the access lock before emitting a deletion result'
            }
            # AC12: lock取得後に取り直したsnapshotで判断する（lock直前に付いた保護で候補が変わる）。
            'reevaluate-after-lock'{
                $ids=New-Series $f 3;Set-EvidenceTestValue BuildCleanup KeepCount 2
                $d=Invoke-Cleanup $f -DryRun;Assert ($d.candidates.buildIds[0] -ceq $ids[2]) 'dry-run candidate'
                $target=$ids[2]
                Set-EvidenceTestValue BuildCleanup StepHook ({param($point)if($point -ceq 'after-lock'){$null=Add-Protection $f $target $baseNow.AddMinutes(-1) $baseNow.AddDays(1)}}.GetNewClosure())
                $x=Invoke-Cleanup $f
                Assert ((Test-Remote $f $ids[2]) -and -not (Test-Remote $f $ids[1]) -and $x.status -ceq 'passed') 'pre-lock snapshot used'
            }
            # AC21: 削除済み・削除中のBuildをinspect/collectが成功として返さない（旧configでも）。
            'inspect-collect-deleted'{
                $old=New-FixtureBuild $f ($f.State.Now.AddMinutes(-10))
                $op=New-ArtifactOperation;$newConfig=[IO.Path]::Combine($op,'build-config.json')
                [IO.File]::WriteAllText($newConfig,(ConvertTo-Json -InputObject $f.ConfigValue -Depth 4),[Text.UTF8Encoding]::new($false));Set-ArtifactAcl $newConfig $false
                $null=New-Series $f 1;Set-EvidenceTestValue BuildCleanup KeepCount 1
                $x=Invoke-Cleanup $f -Config $newConfig;Assert ($x.deleted.buildIds[0] -ceq $old) 'old-config build not deleted'
                foreach($cfg in @($newConfig,$f.Config)){$i=Invoke-BuildInspect $cfg $old;Assert ($i.status -ceq 'failed' -and $i.reasonCode -ceq 'payload-deleted' -and -not $i.PSObject.Properties['reference']) 'deleted inspect'}
                $pending=New-FixtureBuild $f ($f.State.Now.AddMinutes(-20));$null=Add-PendingIntent $pending
                $i=Invoke-BuildInspect $f.Config $pending;Assert ($i.reasonCode -ceq 'delete-pending') 'pending inspect'
                # collect: 実際のcollect経路で保存したPlayerを消し、再collectが既存成功もPUTも返さないこと。
                Reset-Store $f 'collect'
                $identity='20260924T202227427Z-0123456789abcdef0123456789abcdef';$player=[IO.Path]::Combine($f.Root,'repo','artifacts','bs4','players',$identity);$transport=[IO.Path]::Combine($f.Root,'repo','artifacts','bs4','transport',$identity)
                foreach($pair in @(@('SampleGame.exe','fixture-player'),@('content\a.cf','content-a'))){$p=[IO.Path]::Combine($player,$pair[0]);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($p))|Out-Null;[IO.File]::WriteAllText($p,$pair[1])}
                $cbytes=[Text.Encoding]::UTF8.GetBytes('content-a');[IO.Directory]::CreateDirectory([IO.Path]::Combine($transport,'content'))|Out-Null;[IO.File]::WriteAllBytes([IO.Path]::Combine($transport,'content','a.cf'),$cbytes)
                $manifest=[ordered]@{version=1;product='OneStarMaker';contentSet='bs4-spring-full';revision=$identity;target='StandaloneWindows64-Player';unityVersion='6000.6.0f1';files=@([ordered]@{path='a.cf';size=$cbytes.Length;sha256=(Get-Sha $cbytes)})}
                $mbytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $manifest -Depth 8));[IO.File]::WriteAllBytes([IO.Path]::Combine($transport,'transport.json'),$mbytes)
                [IO.File]::WriteAllText([IO.Path]::Combine($player,'build-receipt.json'),(ConvertTo-Json -InputObject ([ordered]@{identity=$identity;target='StandaloneWindows64-Player';transportPath=$transport;transportManifestSha256=(Get-Sha $mbytes);playerBuildGuid=('2'*32);backend='IL2CPP';stripping='High'})))
                $artifacts=[IO.Path]::Combine($f.Root,'artifacts-root');[IO.Directory]::CreateDirectory($artifacts)|Out-Null
                $f.State.Now=$baseNow.AddHours(-1)
                $c=Invoke-BuildCollect $f.Config $player $artifacts;Assert ($c.status -ceq 'passed' -and $c.publish -ceq 'published') "collect failed: $($c.reasonCode)"
                $f.State.Now=$baseNow
                $null=New-Series $f 1
                $x=Invoke-Cleanup $f;Assert ($x.deleted.buildIds[0] -ceq '0123456789abcdef0123456789abcdef') 'collected build not deleted'
                $puts=Get-Count $f 'put'
                $c=Invoke-BuildCollect $f.Config $player $artifacts
                Assert ($c.status -ceq 'failed' -and $c.reasonCode -ceq 'payload-deleted' -and $null -eq $c.reference -and $null -eq $c.packageSha256 -and (Get-Count $f 'put') -eq $puts) 'collect returned deleted build'
            }
            # AC19: transport入口の許可表。印付きのBuild DELETEだけを新たに通す。
            'transport-allow-table'{
                $endpoint='https://'+('e'*32)+'.r2.cloudflarestorage.com';$buildKey=Get-KeyOf ('1'*32)
                $contentKey="development/content/v1/$repo/bs4-spring-full/windows-x64-player/20260924T202227427Z-$('0'*32)/transport.json"
                $rows=@(
                    @('build-delete-marked','delete',$buildKey,@{BuildRetentionDelete=$true},$true),
                    @('build-delete-unmarked','delete',$buildKey,@{},$false),
                    @('build-delete-false','delete',$buildKey,@{BuildRetentionDelete=$false},$false),
                    @('build-delete-string','delete',$buildKey,@{BuildRetentionDelete='True'},$false),
                    @('build-delete-int','delete',$buildKey,@{BuildRetentionDelete=1},$false),
                    @('content-delete-marked','delete',$contentKey,@{BuildRetentionDelete=$true},$false),
                    @('content-delete','delete',$contentKey,@{},$false),
                    @('build-get','get',$buildKey,@{},$true),
                    @('build-put','put',$buildKey,@{},$true),
                    @('evidence-delete','delete',"development/evidence/v2/$repo/x/bundle.zip",@{},$true),
                    @('first-use-delete','delete','evidence/first-use/x/bundle.zip',@{BuildRetentionDelete=$true},$false),
                    @('outside-build-key','delete','development/builds/v1/outside/bundle.zip',@{BuildRetentionDelete=$true},$false)
                )
                foreach($row in $rows){
                    $request=@{Endpoint=$endpoint;Operation=$row[1];Key=$row[2];SourcePath=$null;DestinationPath=$null;DeadlineMilliseconds=1000};foreach($k in $row[3].Keys){$request[$k]=$row[3][$k]}
                    Assert ((Test-R2ArtifactRequestAllowed $request) -eq $row[4]) "allow table: $($row[0])"
                }
                $request=@{Endpoint='https://example.com';Operation='delete';Key=$buildKey;BuildRetentionDelete=$true;DeadlineMilliseconds=1000};Assert (-not (Test-R2ArtifactRequestAllowed $request)) 'endpoint not checked'
                $request=@{Endpoint=$endpoint;Operation='delete';Key=$buildKey;BuildRetentionDelete=$true;DeadlineMilliseconds=0};Assert (-not (Test-R2ArtifactRequestAllowed $request)) 'deadline not checked'
                $accepted=$false;try{$null=Invoke-R2ArtifactOperation 'DUMMY_ID' 'DUMMY_SECRET' ('f'*32) @{Endpoint=$endpoint;Operation='delete';Key=$buildKey;SourcePath=$null;DestinationPath=$null;DeadlineMilliseconds=1000};$accepted=$true}catch{}
                Assert (-not $accepted) 'unmarked Build DELETE reached SDK'
            }
            # AC1・AC20・CLI: 実CLIのgrammar・終了コード（0/1/2）・use/releaseを別processで通す。
            'cli-grammar'{
                $ids=New-Series $f 3
                $run=Invoke-Child (Get-ChildArgs $f (Get-CleanupCli $f -DryRun) 1)
                $out=ConvertFrom-ChildJson $run
                Assert ($run.Exit -eq 0 -and $out.status -ceq 'passed' -and $out.candidates.count -eq 2) "dry-run CLI: $($run.Output)"
                $bad=Invoke-Child (Get-ChildArgs $f @('build','cleanup','--profile','osm','--config',([IO.Path]::Combine($f.Root,'missing.json')),'--project','Bad','--target',$series[1],'--configuration',$series[2]))
                Assert ($bad.Exit -eq 1 -and (ConvertFrom-ChildJson $bad).reasonCode -ceq 'invalid-build-identity') 'invalid token read config first'
                foreach($argsList in @(((Get-CleanupCli $f -DryRun)+'--dry-run'),((Get-CleanupCli $f)+@('--keep','5')),@('build','delete','--profile','osm','--config',$f.Config),@('build','inspect','--profile','osm','--config',$f.Config,'--build-id',$ids[0],'--dry-run'))){
                    $r=Invoke-Child (Get-ChildArgs $f $argsList)
                    Assert ($r.Exit -eq 1 -and $r.Output.Contains('unsupported-command')) "grammar accepted: $($argsList -join ' ')"
                }
                $partial=Invoke-Child (Get-ChildArgs $f (Get-CleanupCli $f) 1 'forbidden')
                Assert ($partial.Exit -eq 2 -and (ConvertFrom-ChildJson $partial).status -ceq 'partial') "partial exit: $($partial.Output)"
                $use=Invoke-Child (Get-ChildArgs $f @('build','use','--profile','osm','--config',$f.Config,'--build-id',$ids[0],'--consumer','cli-user','--until',$f.State.Now.AddDays(1).ToString('o'),'--reason','cli'))
                $u=ConvertFrom-ChildJson $use
                Assert ($use.Exit -eq 0 -and $u.status -ceq 'passed') "use CLI: $($use.Output)"
                $release=Invoke-Child (Get-ChildArgs $f @('build','release','--profile','osm','--config',$f.Config,'--build-id',$ids[0],'--protection',$u.protection))
                Assert ($release.Exit -eq 0 -and (ConvertFrom-ChildJson $release).reasonCode -ceq 'released') "release CLI: $($release.Output)"
                Assert (-not $run.Output.Contains($f.Root) -and -not $run.Output.Contains('https://')) 'CLI output leaked path or endpoint'
            }
        }
    }catch{$failed.Add($caseName+': '+$_.Exception.GetType().Name+': '+$_.Exception.Message)}
    finally{
        $executed.Add($caseName)
        foreach($name in @('NetworkHook','BudgetHook','StepHook')){Set-EvidenceTestValue BuildCleanup $name $null}
        Set-EvidenceTestValue BuildCleanup KeepCount 10;Set-EvidenceTestValue BuildCleanup BudgetMilliseconds 600000
        foreach($name in @('NetworkHook','ReadbackHook','FaultHook','FailureHook')){Set-EvidenceTestValue BuildApplication $name $null}
        Set-EvidenceTestValue BuildStore FaultHook $null
        if($fixture){$target=[IO.Path]::GetFullPath($fixture.Root);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath());if(-not $target.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($target) -cnotmatch '\Aosm-build-test-[0-9a-f]{32}\z'){throw 'unsafe-test-cleanup'};if([IO.Directory]::Exists($target)){[IO.Directory]::Delete($target,$true)}}
    }
}
if($selected.Count -eq 0 -or $selected.Count -ne $executed.Count){$failed.Add('selection')}
if($ResultPath){Write-EvidenceTestJson $ResultPath ([ordered]@{registered=$registered;selected=$selected;executed=@($executed);failed=@($failed);binaries=@(Get-EvidenceTestBinaries)})}
foreach($item in $failed){[Console]::Error.WriteLine($item)}
[Console]::WriteLine("BuildCleanup $($executed.Count), failed $($failed.Count)")
if($failed.Count){exit 1}
