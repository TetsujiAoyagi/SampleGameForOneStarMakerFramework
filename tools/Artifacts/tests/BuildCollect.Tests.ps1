param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../BuildApplication.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactPaths.psm1')
# build collectの受け入れ条件（ArtifactsRoot・配布形・build-info・content照合・退避・再実行）を
# 非秘密の偽Player fixtureと注入transportで検証する。実Player/R2の往復は別の操作証拠として扱う。
$registered=@('collect-roundtrip','rerun-and-resume','artifacts-root-reject','source-reject','transport-and-content-reject','identity-and-series-reject','stage-integrity','concurrent-stage','fresh-process-cli','source-root-both-roots-rejected')
$selected=@(if($Case -ceq '*'){$registered}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
$identity='20260924T202227427Z-0123456789abcdef0123456789abcdef'
$buildId='0123456789abcdef0123456789abcdef'
function Assert([bool]$Condition,[string]$Message){Assert-EvidenceTest $Condition $Message}
function Write-PrivateJson([string]$Path,$Value){Write-EvidenceTestJson $Path $Value;Set-ArtifactAcl $Path $false}
function Get-Sha([byte[]]$Bytes){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()}
function Write-Bytes([string]$Path,[byte[]]$Bytes){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))|Out-Null;[IO.File]::WriteAllBytes($Path,$Bytes)}
function Write-Text([string]$Path,[string]$Text){Write-Bytes $Path ([Text.UTF8Encoding]::new($false).GetBytes($Text))}
# 偽Player: Player本体・Data・content/（manifestと一致）・build-receipt.json。transportは別directory。
function New-Player($Fixture,[string]$Name,[hashtable]$Override=@{}){
    $player=[IO.Path]::Combine($Fixture.Repo,'artifacts','bs4','players',$Name)
    $transport=[IO.Path]::Combine($Fixture.Repo,'artifacts','bs4','transport',$Name)
    Write-Text ([IO.Path]::Combine($player,'SampleGame.exe')) 'fixture-player'
    Write-Text ([IO.Path]::Combine($player,'SampleGame_Data','StreamingAssets','bs4-runtime.json')) '{"content":{"relativeDirectory":"content"}}'
    $content=[ordered]@{'a.cf'='content-a';'sub/b.resS'='content-b'}
    $files=foreach($k in $content.Keys){$b=[Text.Encoding]::UTF8.GetBytes($content[$k]);Write-Bytes ([IO.Path]::Combine($player,'content',$k.Replace('/','\'))) $b;Write-Bytes ([IO.Path]::Combine($transport,'content',$k.Replace('/','\'))) $b;[ordered]@{path=$k;size=$b.Length;sha256=(Get-Sha $b)}}
    $manifest=[ordered]@{version=1;product='OneStarMaker';contentSet='bs4-spring-full';revision=$Name;target='StandaloneWindows64-Player';unityVersion='6000.6.0f1';rootSchemaVersion=2;playerConfigSchemaVersion=1;files=@($files);sourceFiles=@()}
    foreach($k in @($Override.Keys|Where-Object {$_.StartsWith('manifest.')})){$manifest[$k.Substring(9)]=$Override[$k]}
    $mbytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $manifest -Depth 8))
    Write-Bytes ([IO.Path]::Combine($transport,'transport.json')) $mbytes
    $receipt=[ordered]@{identity=$Name;target='StandaloneWindows64-Player';contentReportPath='C:\fixture\outcome.json';transportPath=$transport;transportManifestSha256=(Get-Sha $mbytes);playerBuildGuid=('2'*32);backend='IL2CPP';stripping='High';scenes=@('Assets/UICommon.unity');bootstrapSceneName='UICommon';checkedRootGuids=@('3'*32)}
    foreach($k in @($Override.Keys|Where-Object {$_.StartsWith('receipt.')})){$receipt[$k.Substring(8)]=$Override[$k]}
    Write-Text ([IO.Path]::Combine($player,'build-receipt.json')) (ConvertTo-Json -InputObject $receipt -Depth 8)
    return $player
}
function New-CollectFixture {
    $root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-build-test-'+[Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($root)|Out-Null
    # The modules resolve the store root even with TestRoot set; pass the gate below $root.
    $null=Enable-EvidenceTestStoreRoot $root
    $owned=@('transfers','builds-v1','deployments','workflow','evidence','credentials'|ForEach-Object {[IO.Path]::Combine($root,$_)})
    Set-EvidenceTestValue ArtifactPaths TestRoot $owned[0]
    Set-EvidenceTestValue BuildStore TestRoot $owned[1]
    Set-EvidenceTestValue EvidenceContract DeploymentRoot $owned[2]
    Set-EvidenceTestValue BuildContract OwnedRootsForTest $owned
    $deployment=$owned[2];[IO.Directory]::CreateDirectory($deployment)|Out-Null
    $ev=[ordered]@{schemaVersion=2;profile='osm';endpoint=('https://'+('e'*32)+'.r2.cloudflarestorage.com');bucket='osm-artifacts';repositoryId=('a'*64);prefix='development/evidence/v2/';policy='task-end-30d-v1';deploymentId=('d'*32)}
    $evPath=[IO.Path]::Combine($deployment,('d'*32)+'.config.json');Write-EvidenceTestJson $evPath $ev
    $evHash=(Get-FileHash -LiteralPath $evPath).Hash.ToLowerInvariant()
    Write-EvidenceTestJson ([IO.Path]::Combine($deployment,('d'*32)+'.json')) ([ordered]@{schemaVersion=2;deploymentId=('d'*32);repositoryId=('a'*64);configSha256=$evHash;baselineRecordId='fixture';baselineSha256=('b'*64);ownerObservationId='fixture';ownerObservationSha256=('c'*64)})
    $operation=New-ArtifactOperation
    $configPath=[IO.Path]::Combine($operation,'build-config.json')
    Write-PrivateJson $configPath ([ordered]@{schemaVersion=1;purpose='build';profile='osm';evidenceConfigPath=$evPath;evidenceConfigSha256=$evHash;repositoryId=('a'*64);prefix='development/builds/v1/';policy='build-publish-v1'})
    $artifacts=[IO.Path]::Combine($root,'artifacts-root');[IO.Directory]::CreateDirectory($artifacts)|Out-Null
    $remote=[IO.Path]::Combine($root,'remote');[IO.Directory]::CreateDirectory($remote)|Out-Null
    $objects=[Collections.Concurrent.ConcurrentDictionary[string,byte[]]]::new();$counts=[Collections.Concurrent.ConcurrentDictionary[string,int]]::new()
    $network={param($gen,$request)
        $name=$request.Operation+' '+$request.Key;$counts.AddOrUpdate($name,1,{param($k,$v)$v+1})|Out-Null
        if($request.Operation -ceq 'delete'){throw 'delete-forbidden'}
        if($request.Operation -ceq 'put'){$objects[$request.Key]=[IO.File]::ReadAllBytes($request.SourcePath);$bytes=$null;$sha=$null}
        else{$data=$objects[$request.Key];[IO.File]::WriteAllBytes($request.DestinationPath,$data);$bytes=[long]$data.Length;$sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant()}
        return [pscustomobject]@{Operation=$request.Operation;Generation=$gen;HttpStatus=200;StatusClass='success';S3Code=$null;Bytes=$bytes;Sha256=$sha;StatusObserved=$true;TimedOut=$false;Redirected=$false}
    }.GetNewClosure()
    $readback={param($op,$cfg,$gen,$key,$bytes,$hash)
        $data=$objects[$key];$actual=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant()
        return [pscustomobject]@{operation='readback';generation=$gen;key=$key;bytes=[long]$data.Length;sha256=$actual;childExit=0;httpStatus=200;statusClass='success';s3Code=$null;statusObserved=$true;timedOut=$false;redirected=$false;childStatus='passed';observedAt=[DateTimeOffset]::UtcNow.ToString('o')}
    }.GetNewClosure()
    Set-EvidenceTestValue BuildApplication NetworkHook $network
    Set-EvidenceTestValue BuildApplication ReadbackHook $readback
    foreach($module in @(Get-EvidenceTestModules BuildApplication)){& $module {function script:Get-CredentialStatus {param($profile)[pscustomobject]@{Generation=('f'*32)}}}}
    $f=[pscustomobject]@{Root=$root;Repo=[IO.Path]::Combine($root,'repo');Owned=$owned;Config=$configPath;ArtifactsRoot=$artifacts;RemoteRoot=$remote;Objects=$objects;Counts=$counts;Network=$network;Player=$null}
    $f.Player=New-Player $f $identity
    return $f
}
function Get-Destination($Fixture,[string]$Id=$buildId){[IO.Path]::Combine($Fixture.ArtifactsRoot,'players','samplegame','windows-x64-player','il2cpp-high',$Id)}
function Get-TotalPuts($Fixture){[int](@($Fixture.Counts.GetEnumerator()|Where-Object {$_.Key.StartsWith('put ',[StringComparison]::Ordinal)}|ForEach-Object {$_.Value}|Measure-Object -Sum).Sum)}
function Get-TreeFingerprint([string]$Root){
    if(-not [IO.Directory]::Exists($Root)){return 'absent'}
    return (@([IO.Directory]::EnumerateFiles($Root,'*',[IO.SearchOption]::AllDirectories)|Sort-Object|ForEach-Object{[IO.Path]::GetRelativePath($Root,$_)+'|'+(Get-FileHash -LiteralPath $_).Hash}) -join "`n")
}
function Get-Entries([string]$Root){@([IO.Directory]::EnumerateFiles($Root,'*',[IO.SearchOption]::AllDirectories)|ForEach-Object {[IO.Path]::GetRelativePath($Root,$_).Replace('\','/')+'|'+(Get-FileHash -LiteralPath $_).Hash}|Sort-Object -CaseSensitive)}
function Assert-Rejected($Result,[string]$Code){Assert ($Result.status -ceq 'failed' -and $null -eq $Result.reference -and $null -eq $Result.packageSha256) "failure exposed success ($Code)";if($Code){Assert ($Result.reasonCode -ceq $Code) "expected $Code, got $($Result.reasonCode)"}}
function Invoke-Child([string[]]$ChildArguments,[hashtable]$Environment=@{}){
    $p=[Diagnostics.Process]::new();$p.StartInfo.FileName=(Get-Process -Id $PID).Path;$p.StartInfo.UseShellExecute=$false;$p.StartInfo.CreateNoWindow=$true;$p.StartInfo.RedirectStandardOutput=$true;$p.StartInfo.RedirectStandardError=$true
    foreach($a in $ChildArguments){$p.StartInfo.ArgumentList.Add($a)};foreach($k in $Environment.Keys){$p.StartInfo.Environment[$k]=$Environment[$k]}
    $null=$p.Start();return @{Process=$p;Output=$p.StandardOutput.ReadToEndAsync();Error=$p.StandardError.ReadToEndAsync()}
}
function Finish-Child($Job){
    try{
        if(-not $Job.Process.WaitForExit(60000)){$Job.Process.Kill($true);$null=$Job.Process.WaitForExit(5000);throw 'child-timeout'}
        Assert ($Job.Output.Wait(3000) -and $Job.Error.Wait(3000)) 'child-pipes'
        return @{Exit=$Job.Process.ExitCode;Output=$Job.Output.Result;Error=$Job.Error.Result}
    }finally{$Job.Process.Dispose()}
}
function Get-ChildArgs($Fixture,[string]$Source,[string]$ArtifactsRoot,[string]$Ready='',[string]$Go=''){
    $a=@('-NoProfile','-File',[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'BuildCollectChild.ps1')),'-Config',$Fixture.Config,'-Source',$Source,'-StoreRoot',$Fixture.Owned[1],'-TransferRoot',$Fixture.Owned[0],'-DeploymentRoot',$Fixture.Owned[2],'-RemoteRoot',$Fixture.RemoteRoot,'-OwnedRoots',($Fixture.Owned -join '|'))
    if($ArtifactsRoot){$a+=@('-ArtifactsRoot',$ArtifactsRoot)};if($Ready){$a+=@('-ReadyEvent',$Ready,'-GoEvent',$Go)}
    return $a
}
$savedRoot=[Environment]::GetEnvironmentVariable('OSM_ARTIFACTS_ROOT')
foreach($caseName in $selected){
    $fixture=$null
    try{
        Assert ($caseName -cin $registered) 'unknown-case'
        [Environment]::SetEnvironmentVariable('OSM_ARTIFACTS_ROOT',$null)
        $fixture=New-CollectFixture
        switch -CaseSensitive ($caseName){
            'collect-roundtrip'{
                $before=Get-TreeFingerprint $fixture.Player
                # 引数を省略した場合は、processが引き継いだOSM_ARTIFACTS_ROOTを使う。
                [Environment]::SetEnvironmentVariable('OSM_ARTIFACTS_ROOT',$fixture.ArtifactsRoot)
                $r=Invoke-BuildCollect $fixture.Config $fixture.Player ''
                Assert ($r.status -ceq 'passed' -and $r.stage -ceq 'created' -and $r.publish -ceq 'published' -and $r.buildId -ceq $buildId -and $r.reference -and $r.packageSha256) "collect failed: $($r.reasonCode)"
                Assert ((Get-TreeFingerprint $fixture.Player) -ceq $before) 'source changed'
                $dest=Get-Destination $fixture
                $want=@((Get-Entries $fixture.Player|Where-Object {-not $_.StartsWith('build-receipt.json|')})+@('build-info.json|'+(Get-FileHash -LiteralPath ([IO.Path]::Combine($dest,'build-info.json'))).Hash)|Sort-Object -CaseSensitive)
                Assert (((Get-Entries $dest) -join "`n") -ceq ($want -join "`n")) 'stage set mismatch'
                $infoText=[IO.File]::ReadAllText([IO.Path]::Combine($dest,'build-info.json'))
                $info=$infoText|ConvertFrom-Json -DateKind String
                Assert ((@($info.PSObject.Properties.Name) -join ',') -ceq 'schemaVersion,project,target,configuration,buildId,buildIdentity,contentSet,contentTarget,transportManifestSha256,unityVersion,playerBuildGuid') 'build-info fields'
                Assert ($info.schemaVersion -eq 1 -and $info.project -ceq 'samplegame' -and $info.target -ceq 'windows-x64-player' -and $info.configuration -ceq 'il2cpp-high' -and $info.buildId -ceq $buildId -and $info.buildIdentity -ceq $identity -and $info.contentSet -ceq 'bs4-spring-full' -and $info.contentTarget -ceq 'StandaloneWindows64-Player' -and $info.unityVersion -ceq '6000.6.0f1' -and $info.playerBuildGuid -ceq ('2'*32)) 'build-info values'
                Assert (-not $infoText.Contains(':\') -and -not $infoText.Contains([Environment]::UserName)) 'build-info carries local path or user'
                $fetched=Invoke-BuildFetch $fixture.Config $r.reference $r.packageSha256
                Assert ($fetched.status -ceq 'passed' -and ((Get-Entries $fetched.outputPath) -join "`n") -ceq ((Get-Entries $dest) -join "`n")) 'fetch does not match stage'
                Assert ((Get-TotalPuts $fixture) -eq 1) 'PUT count'
            }
            'rerun-and-resume'{
                $root=$fixture.ArtifactsRoot
                # 他の退避先と既存の成功receiptを置き、失敗・再実行で変わらないことを見る。
                $other=New-Player $fixture '20260920T095242451Z-58c64914669c431c918e78fea928ab65'
                $o=Invoke-BuildCollect $fixture.Config $other $root;Assert ($o.status -ceq 'passed') 'other collect failed'
                $otherDest=Get-Destination $fixture '58c64914669c431c918e78fea928ab65';$otherPrint=Get-TreeFingerprint $otherDest
                $otherReceipt=[IO.Path]::Combine($fixture.Owned[1],'a'*64,'58c64914669c431c918e78fea928ab65','receipt.json');$otherHash=(Get-FileHash -LiteralPath $otherReceipt).Hash
                $source=Get-TreeFingerprint $fixture.Player
                # 予約前のpublish失敗: storeの親が無いので予約できない。退避は残り、再実行で退避を省いてpublishする。
                $store=$fixture.Owned[1];Set-EvidenceTestValue BuildStore TestRoot ([IO.Path]::Combine($fixture.Root,'missing','builds-v1'))
                $first=Invoke-BuildCollect $fixture.Config $fixture.Player $root;Assert-Rejected $first $null
                Assert ($first.stage -ceq 'created' -and (Get-TotalPuts $fixture) -eq 1) 'pre-reservation failure'
                $stagePrint=Get-TreeFingerprint (Get-Destination $fixture)
                Set-EvidenceTestValue BuildStore TestRoot $store
                $second=Invoke-BuildCollect $fixture.Config $fixture.Player $root
                Assert ($second.status -ceq 'passed' -and $second.stage -ceq 'existing' -and $second.publish -ceq 'published' -and (Get-TotalPuts $fixture) -eq 2) 'resume after unreserved failure'
                Assert ((Get-TreeFingerprint (Get-Destination $fixture)) -ceq $stagePrint) 'stage changed by resume'
                # 成功receiptがあれば同じ参照・hashを返し、PUTしない。
                $third=Invoke-BuildCollect $fixture.Config $fixture.Player $root
                Assert ($third.status -ceq 'passed' -and $third.publish -ceq 'existing' -and $third.reference -ceq $second.reference -and $third.packageSha256 -ceq $second.packageSha256 -and (Get-TotalPuts $fixture) -eq 2) 'rerun with receipt'
                # 予約済み・receipt無し（PUT応答不明）は自動で再送しない。
                $late=New-Player $fixture '20260920T100415450Z-afcd528e7d0947a982789d6ec793d34f'
                Set-EvidenceTestValue BuildApplication NetworkHook {param($gen,$request)throw 'response-lost'}
                $lost=Invoke-BuildCollect $fixture.Config $late $root;Assert-Rejected $lost $null
                $latePrint=Get-TreeFingerprint (Get-Destination $fixture 'afcd528e7d0947a982789d6ec793d34f')
                Set-EvidenceTestValue BuildApplication NetworkHook $fixture.Network
                $puts=Get-TotalPuts $fixture
                $unresolved=Invoke-BuildCollect $fixture.Config $late $root;Assert-Rejected $unresolved 'publish-unresolved'
                Assert ((Get-TotalPuts $fixture) -eq $puts) 'unresolved reservation was re-sent'
                Assert ((Get-TreeFingerprint (Get-Destination $fixture 'afcd528e7d0947a982789d6ec793d34f')) -ceq $latePrint) 'stage changed after publish failure'
                Assert ((Get-TreeFingerprint $fixture.Player) -ceq $source -and (Get-TreeFingerprint $otherDest) -ceq $otherPrint -and (Get-FileHash -LiteralPath $otherReceipt).Hash -ceq $otherHash) 'unrelated state changed'
                Assert (-not @($fixture.Counts.Keys|Where-Object {$_.StartsWith('delete ',[StringComparison]::Ordinal)}).Count) 'DELETE called'
            }
            'artifacts-root-reject'{
                $ops=@([IO.Directory]::EnumerateDirectories($fixture.Owned[0])).Count
                Assert-Rejected (Invoke-BuildCollect $fixture.Config $fixture.Player '') 'artifacts-root-not-configured'
                Assert (@([IO.Directory]::EnumerateDirectories($fixture.Owned[0])).Count -eq $ops -and @([IO.Directory]::EnumerateFileSystemEntries($fixture.ArtifactsRoot)).Count -eq 0) 'unconfigured root created files'
                $outside=[IO.Path]::Combine($fixture.Root,'outside');[IO.Directory]::CreateDirectory($outside)|Out-Null
                $junction=[IO.Path]::Combine($fixture.Root,'junction-root');$null=New-Item -ItemType Junction -Path $junction -Target $outside
                $bad=@('relative\root',[IO.Path]::Combine($fixture.Root,'absent'),$junction,[IO.Path]::Combine($fixture.Player,'content'),$fixture.Repo,$fixture.Owned[0],$fixture.Owned[1],[IO.Path]::Combine($fixture.Owned[2],'child'),$fixture.Root)
                [IO.Directory]::CreateDirectory([IO.Path]::Combine($fixture.Owned[2],'child'))|Out-Null;[IO.Directory]::CreateDirectory($fixture.Owned[1])|Out-Null
                try{foreach($value in $bad){Assert-Rejected (Invoke-BuildCollect $fixture.Config $fixture.Player $value) 'invalid-artifacts-root'}}finally{Remove-Item -LiteralPath $junction -Force}
                Assert ([IO.Directory]::GetFileSystemEntries($outside).Count -eq 0 -and (Get-TotalPuts $fixture) -eq 0) 'rejected root was written'
            }
            'source-reject'{
                $root=$fixture.ArtifactsRoot
                $receipt=[IO.Path]::Combine($fixture.Player,'build-receipt.json');$saved=[IO.File]::ReadAllBytes($receipt)
                [IO.File]::Delete($receipt);Assert-Rejected (Invoke-BuildCollect $fixture.Config $fixture.Player $root) 'invalid-source';[IO.File]::WriteAllBytes($receipt,$saved)
                $backup=[IO.Path]::Combine($fixture.Player,'SampleGame_BackUpThisFolder_ButDontShipItWithYourGame','x.txt');Write-Text $backup 'backup'
                Assert-Rejected (Invoke-BuildCollect $fixture.Config $fixture.Player $root) 'invalid-source';[IO.Directory]::Delete([IO.Path]::GetDirectoryName($backup),$true)
                $work=[IO.Path]::Combine($fixture.Repo,'artifacts','bs4','work',$identity);Write-Text ([IO.Path]::Combine($work,'build-receipt.json')) '{}'
                Assert-Rejected (Invoke-BuildCollect $fixture.Config $work $root) 'invalid-source'
                Assert-Rejected (Invoke-BuildCollect $fixture.Config 'relative\player' $root) 'invalid-source'
                # 上限は退避の前に見る。sizeはSetLengthで作り、巨大なbytesを書かない。
                $many=[IO.Path]::Combine($fixture.Player,'SampleGame_Data','many');foreach($i in 1..510){Write-Text ([IO.Path]::Combine($many,"$i.txt")) 'x'}
                Assert-Rejected (Invoke-BuildCollect $fixture.Config $fixture.Player $root) 'size-limit';[IO.Directory]::Delete($many,$true)
                foreach($sizes in @(@(257MB),@(121MB,121MB))){
                    $paths=for($i=0;$i -lt $sizes.Count;$i++){$p=[IO.Path]::Combine($fixture.Player,"large-$i.bin");$s=[IO.FileStream]::new($p,[IO.FileMode]::CreateNew);try{$s.SetLength($sizes[$i])}finally{$s.Dispose()};$p}
                    Assert-Rejected (Invoke-BuildCollect $fixture.Config $fixture.Player $root) 'size-limit'
                    foreach($p in $paths){[IO.File]::Delete($p)}
                }
                $long=[IO.Path]::Combine($fixture.Player,('p'*120),('q'*121));Write-Text $long 'long'
                Assert-Rejected (Invoke-BuildCollect $fixture.Config $fixture.Player $root) $null;[IO.Directory]::Delete([IO.Path]::GetDirectoryName($long),$true)
                Assert (-not [IO.Directory]::Exists([IO.Path]::Combine($root,'players')) -and (Get-TotalPuts $fixture) -eq 0) 'rejected source was staged or sent'
            }
            'transport-and-content-reject'{
                $root=$fixture.ArtifactsRoot
                $cases=@(
                    @{name='hash';override=@{'receipt.transportManifestSha256'=('0'*64)};code='transport-mismatch'},
                    @{name='revision';override=@{'manifest.revision'='20260101T000000000Z-ffffffffffffffffffffffffffffffff'};code='transport-mismatch'},
                    @{name='target';override=@{'manifest.target'='Android-Player'};code='transport-mismatch'},
                    @{name='missing-field';override=@{'manifest.contentSet'=$null};code='transport-mismatch'}
                )
                $n=0
                foreach($c in $cases){
                    $n++;$name='20260924T00000000'+$n+'Z-'+($n.ToString()*32)
                    $p=New-Player $fixture $name $c.override
                    Assert-Rejected (Invoke-BuildCollect $fixture.Config $p $root) $c.code
                }
                $content=[IO.Path]::Combine($fixture.Player,'content','a.cf');$saved=[IO.File]::ReadAllBytes($content)
                [IO.File]::WriteAllText($content,'changed');Assert-Rejected (Invoke-BuildCollect $fixture.Config $fixture.Player $root) 'content-mismatch';[IO.File]::WriteAllBytes($content,$saved)
                $extra=[IO.Path]::Combine($fixture.Player,'content','extra.cf');Write-Text $extra 'extra'
                Assert-Rejected (Invoke-BuildCollect $fixture.Config $fixture.Player $root) 'content-mismatch';[IO.File]::Delete($extra)
                $missing=[IO.Path]::Combine($fixture.Player,'content','sub','b.resS');$saved=[IO.File]::ReadAllBytes($missing);[IO.File]::Delete($missing)
                Assert-Rejected (Invoke-BuildCollect $fixture.Config $fixture.Player $root) 'content-mismatch';[IO.File]::WriteAllBytes($missing,$saved)
                Assert (-not [IO.Directory]::Exists([IO.Path]::Combine($root,'players')) -and (Get-TotalPuts $fixture) -eq 0) 'rejected manifest was staged or sent'
            }
            'identity-and-series-reject'{
                $root=$fixture.ArtifactsRoot
                $badName=New-Player $fixture 'not-an-identity'
                Assert-Rejected (Invoke-BuildCollect $fixture.Config $badName $root) 'invalid-build-identity'
                $upper=New-Player $fixture '20260924T202227427Z-0123456789ABCDEF0123456789ABCDEF'
                Assert-Rejected (Invoke-BuildCollect $fixture.Config $upper $root) 'invalid-build-identity'
                $mismatch=New-Player $fixture '20260924T202227427Z-11111111111111111111111111111111' @{'receipt.identity'=$identity}
                Assert-Rejected (Invoke-BuildCollect $fixture.Config $mismatch $root) 'invalid-player-receipt'
                $n=2
                foreach($o in @(@{'receipt.target'='Android-Player';'manifest.target'='Android-Player'},@{'receipt.backend'='Mono2x'},@{'receipt.stripping'='Low'})){
                    $p=New-Player $fixture ('20260924T202227427Z-'+($n.ToString()*32)) $o;$n++
                    Assert-Rejected (Invoke-BuildCollect $fixture.Config $p $root) 'unsupported-series'
                }
                Assert (-not [IO.Directory]::Exists([IO.Path]::Combine($root,'players')) -and (Get-TotalPuts $fixture) -eq 0) 'rejected identity was staged or sent'
            }
            'stage-integrity'{
                $root=$fixture.ArtifactsRoot;$dest=Get-Destination $fixture;$parent=[IO.Path]::GetDirectoryName($dest)
                # 他の呼出しのstagingは触らない。
                $foreign=[IO.Path]::Combine($parent,'.'+$buildId+'.'+('9'*32)+'.staging');Write-Text ([IO.Path]::Combine($foreign,'sentinel.txt')) 'other'
                $source=Get-TreeFingerprint $fixture.Player
                # copy前に元が変わった（事前hashと読んだbytesの不一致）。
                $exe=[IO.Path]::Combine($fixture.Player,'SampleGame.exe');$saved=[IO.File]::ReadAllBytes($exe)
                Set-EvidenceTestValue BuildStage StageHook ({param($phase,$staging)if($phase -ceq 'before-copy'){[IO.File]::WriteAllText($exe,'swapped')}}.GetNewClosure())
                Assert-Rejected (Invoke-BuildCollect $fixture.Config $fixture.Player $root) 'stage-hash-mismatch'
                [IO.File]::WriteAllBytes($exe,$saved)
                # 書いたstagingが公開前に変わった。
                Set-EvidenceTestValue BuildStage StageHook {param($phase,$staging)if($phase -ceq 'before-verify'){[IO.File]::WriteAllText([IO.Path]::Combine($staging,'SampleGame.exe'),'tampered')}}
                Assert-Rejected (Invoke-BuildCollect $fixture.Config $fixture.Player $root) 'stage-hash-mismatch'
                Set-EvidenceTestValue BuildStage StageHook $null
                $left=@([IO.Directory]::EnumerateDirectories($parent)|ForEach-Object {[IO.Path]::GetFileName($_)})
                Assert ($left.Count -eq 1 -and $left[0] -ceq [IO.Path]::GetFileName($foreign) -and [IO.File]::Exists([IO.Path]::Combine($foreign,'sentinel.txt'))) 'own staging left or foreign staging touched'
                # 既存の退避先が別内容なら拒否し、上書きしない。
                Write-Text ([IO.Path]::Combine($dest,'SampleGame.exe')) 'different'
                $destPrint=Get-TreeFingerprint $dest
                Assert-Rejected (Invoke-BuildCollect $fixture.Config $fixture.Player $root) 'stage-conflict'
                Assert ((Get-TreeFingerprint $dest) -ceq $destPrint -and (Get-TreeFingerprint $fixture.Player) -ceq $source -and (Get-TotalPuts $fixture) -eq 0) 'conflicting stage changed'
            }
            'concurrent-stage'{
                $goName='osm-collect-'+[Guid]::NewGuid().ToString('N');$go=[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,$goName)
                $names=@(('osm-collect-'+[Guid]::NewGuid().ToString('N')),('osm-collect-'+[Guid]::NewGuid().ToString('N')))
                $readies=@($names|ForEach-Object {[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,$_)})
                try{
                    $jobs=[Collections.Generic.List[object]]::new()
                    foreach($name in $names){$jobs.Add((Invoke-Child (Get-ChildArgs $fixture $fixture.Player $fixture.ArtifactsRoot $name $goName)))}
                    # 両方がrename直前に到達したことをsignalで確認してから同時に進める。
                    foreach($e in $readies){Assert ($e.WaitOne(60000)) 'children did not reach rename'}
                    $null=$go.Set()
                    $outcomes=@($jobs|ForEach-Object {Finish-Child $_})
                }finally{$go.Dispose();foreach($e in $readies){$e.Dispose()}}
                $passed=@($outcomes|Where-Object {$_.Exit -eq 0});$lost=@($outcomes|Where-Object {$_.Exit -eq 1})
                Assert ($passed.Count -eq 1 -and $lost.Count -eq 1) "concurrent stage did not select one owner: $($outcomes|ForEach-Object {"[$($_.Exit)] "+$_.Output+$_.Error})"
                Assert (($lost[0].Output.Trim()|ConvertFrom-Json).reasonCode -ceq 'stage-conflict') 'loser reason'
                Assert (@([IO.Directory]::EnumerateFiles($fixture.RemoteRoot,'put-attempt-*.txt')).Count -eq 1) 'concurrent collect sent more than once'
                Assert (@([IO.Directory]::EnumerateDirectories([IO.Path]::GetDirectoryName((Get-Destination $fixture)))).Count -eq 1) 'loser staging left'
            }
            'source-root-both-roots-rejected'{
                # Without the injected roots the Build source boundary is the store root and
                # the former root (Artifacts and Workflow of each) and their parents.
                Import-Module (Join-Path $PSScriptRoot '../BuildContract.psm1')
                $fx=New-StoreRootTestBase $fixture.Root
                Set-EvidenceTestValue BuildContract OwnedRootsForTest @()
                try{
                    # The former root stays empty: any role directory there is a leftover the gate refuses
                    # (legacy-store-present), so its paths are checked as strings only.
                    foreach($role in @('Artifacts','Workflow')){[IO.Directory]::CreateDirectory([IO.Path]::Combine($fx.Root,$role,'inside'))|Out-Null}
                    foreach($bad in @([IO.Path]::Combine($fx.Root,'Artifacts','inside'),[IO.Path]::Combine($fx.Root,'Workflow','inside'),[IO.Path]::Combine($fx.LegacyRoot,'Artifacts','inside'),[IO.Path]::Combine($fx.LegacyRoot,'Workflow','inside'),$fx.Root,$fx.LegacyRoot,$fx.Base)){
                        $m=try{Assert-BuildSourceRoot $bad;'accepted'}catch{$_.Exception.Message}
                        Assert ($m -ceq 'invalid-source') ('source root accepted '+$bad+': '+$m)
                    }
                    $ok=[IO.Path]::Combine($fixture.Root,'player-source');[IO.Directory]::CreateDirectory($ok)|Out-Null
                    Assert-BuildSourceRoot $ok
                }finally{Set-EvidenceTestValue BuildContract OwnedRootsForTest $fixture.Owned;Clear-StoreRootTestSeams}
            }
            'fresh-process-cli'{
                $job=Invoke-Child (Get-ChildArgs $fixture $fixture.Player '') @{OSM_ARTIFACTS_ROOT=$fixture.ArtifactsRoot}
                $c=Finish-Child $job
                Assert ($c.Exit -eq 0) "fresh CLI collect failed: $($c.Output) $($c.Error)"
                $v=$c.Output.Trim()|ConvertFrom-Json -Depth 8 -DateKind String
                Assert ($v.status -ceq 'passed' -and $v.reference -and $v.packageSha256 -and $v.stage -ceq 'created') 'fresh collect identity missing'
                $again=Finish-Child (Invoke-Child (Get-ChildArgs $fixture $fixture.Player $fixture.ArtifactsRoot))
                $w=$again.Output.Trim()|ConvertFrom-Json -Depth 8 -DateKind String
                Assert ($again.Exit -eq 0 -and $w.publish -ceq 'existing' -and $w.reference -ceq $v.reference -and $w.packageSha256 -ceq $v.packageSha256) 'fresh rerun changed identity'
                Assert (@([IO.Directory]::EnumerateFiles($fixture.RemoteRoot,'put-attempt-*.txt')).Count -eq 1) 'fresh rerun sent again'
                $none=Finish-Child (Invoke-Child (Get-ChildArgs $fixture $fixture.Player ''))
                Assert ($none.Exit -eq 1 -and ($none.Output.Trim()|ConvertFrom-Json).reasonCode -ceq 'artifacts-root-not-configured') 'unconfigured fresh CLI'
                $cli=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'CliChild.ps1'))
                foreach($args2 in @(@('build','collect','--profile','osm','--config',$fixture.Config),@('build','collect','--profile','osm','--config',$fixture.Config,'--source',$fixture.Player,'--latest','yes'),@('build','collect','--profile','osm','--source',$fixture.Player,'--artifacts-root',$fixture.ArtifactsRoot))){
                    $g=Finish-Child (Invoke-Child (@('-NoProfile','-File',$cli,'-StoreBase',$fixture.Root)+$args2))
                    Assert ($g.Exit -eq 1 -and $g.Output.Contains('unsupported-command')) 'collect grammar accepted unknown or missing option'
                }
            }
        }
    }catch{$failed.Add($caseName+': '+$_.Exception.GetType().Name+': '+$_.Exception.Message+' (line '+$_.InvocationInfo.ScriptLineNumber+')')}
    finally{
        $executed.Add($caseName)
        foreach($name in @('NetworkHook','ReadbackHook','FaultHook','FailureHook','PackageCreatedHook')){Set-EvidenceTestValue BuildApplication $name $null}
        Set-EvidenceTestValue BuildStage StageHook $null;Set-EvidenceTestValue BuildStore FaultHook $null
        [Environment]::SetEnvironmentVariable('OSM_ARTIFACTS_ROOT',$savedRoot)
        if($fixture){$target=[IO.Path]::GetFullPath($fixture.Root);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath());if(-not $target.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($target) -cnotmatch '\Aosm-build-test-[0-9a-f]{32}\z'){throw 'unsafe-test-cleanup'};if([IO.Directory]::Exists($target)){[IO.Directory]::Delete($target,$true)}}
    }
}
if($selected.Count -eq 0 -or $selected.Count -ne $executed.Count){$failed.Add('selection')}
if($ResultPath){Write-EvidenceTestJson $ResultPath ([ordered]@{registered=$registered;selected=$selected;executed=@($executed);failed=@($failed);binaries=@(Get-EvidenceTestBinaries)})}
foreach($item in $failed){[Console]::Error.WriteLine($item)}
[Console]::WriteLine("BuildCollect $($executed.Count), failed $($failed.Count)")
if($failed.Count){exit 1}
