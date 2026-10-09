param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../BuildApplication.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactPaths.psm1')
# 9群は正常系だけでなく、送信前拒否・応答不明・成功commit前後・fresh processを分ける。
# fixture成功は実Build保存の代用にせず、実R2の限定往復は別の操作証拠として扱う。
$registered=@('snapshot-roundtrip','input-and-size-reject','snapshot-conflict','put-unconfirmed','readback-failure','receipt-commit-failure','fetch-integrity','existing-success-preserved','fresh-process-entrypoints')
$selected=@(if($Case -ceq '*'){$registered}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
function Assert([bool]$Condition,[string]$Message){Assert-EvidenceTest $Condition $Message}
function Write-PrivateJson([string]$Path,$Value){Write-EvidenceTestJson $Path $Value;Set-ArtifactAcl $Path $false}
function New-BuildFixture {
    $root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-build-test-'+[Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($root)|Out-Null
    Set-EvidenceTestValue ArtifactPaths TestRoot ([IO.Path]::Combine($root,'transfers'))
    Set-EvidenceTestValue BuildStore TestRoot ([IO.Path]::Combine($root,'builds-v1'))
    Set-EvidenceTestValue EvidenceContract DeploymentRoot ([IO.Path]::Combine($root,'deployments'))
    Set-EvidenceTestValue BuildContract OwnedRootsForTest @([IO.Path]::Combine($root,'transfers'),[IO.Path]::Combine($root,'builds-v1'),[IO.Path]::Combine($root,'deployments'),[IO.Path]::Combine($root,'workflow'),[IO.Path]::Combine($root,'evidence'),[IO.Path]::Combine($root,'credentials'))
    $deployment=[IO.Path]::Combine($root,'deployments');[IO.Directory]::CreateDirectory($deployment)|Out-Null
    $ev=[ordered]@{schemaVersion=2;profile='osm';endpoint=('https://'+('e'*32)+'.r2.cloudflarestorage.com');bucket='osm-artifacts';repositoryId=('a'*64);prefix='development/evidence/v2/';policy='task-end-30d-v1';deploymentId=('d'*32)}
    $evPath=[IO.Path]::Combine($deployment,('d'*32)+'.config.json');Write-EvidenceTestJson $evPath $ev
    $evHash=(Get-FileHash -LiteralPath $evPath).Hash.ToLowerInvariant()
    Write-EvidenceTestJson ([IO.Path]::Combine($deployment,('d'*32)+'.json')) ([ordered]@{schemaVersion=2;deploymentId=('d'*32);repositoryId=('a'*64);configSha256=$evHash;baselineRecordId='fixture';baselineSha256=('b'*64);ownerObservationId='fixture';ownerObservationSha256=('c'*64)})
    $operation=New-ArtifactOperation
    $configPath=[IO.Path]::Combine($operation,'build-config.json')
    Write-PrivateJson $configPath ([ordered]@{schemaVersion=1;purpose='build';profile='osm';evidenceConfigPath=$evPath;evidenceConfigSha256=$evHash;repositoryId=('a'*64);prefix='development/builds/v1/';policy='build-publish-v1'})
    $source=[IO.Path]::Combine($root,'source');[IO.Directory]::CreateDirectory($source)|Out-Null
    $remote=[IO.Path]::Combine($root,'remote');[IO.Directory]::CreateDirectory($remote)|Out-Null
    [IO.File]::WriteAllText([IO.Path]::Combine($source,'sample.bin'),'safe-build-payload')
    $selectionPath=[IO.Path]::Combine($operation,'build-selection.json')
    $selection=[ordered]@{schemaVersion=1;purpose='build';root=$source;files=@('sample.bin');project='samplegame';target='windows-x64-player';configuration='il2cpp-high';buildId=[Guid]::NewGuid().ToString('N')}
    Write-PrivateJson $selectionPath $selection
    $objects=[Collections.Concurrent.ConcurrentDictionary[string,byte[]]]::new()
    $counts=[Collections.Concurrent.ConcurrentDictionary[string,int]]::new()
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
    return [pscustomobject]@{Root=$root;Operation=$operation;DeploymentRoot=$deployment;TransferRoot=[IO.Path]::Combine($root,'transfers');StoreRoot=[IO.Path]::Combine($root,'builds-v1');RemoteRoot=$remote;EvidenceConfigPath=$evPath;Config=$configPath;SelectionPath=$selectionPath;Selection=$selection;Source=$source;Objects=$objects;Counts=$counts;Network=$network;Readback=$readback}
}
function New-Id($Fixture){$Fixture.Selection.buildId=[Guid]::NewGuid().ToString('N');Write-PrivateJson $Fixture.SelectionPath $Fixture.Selection}
function Get-ReceiptPath($Fixture){[IO.Path]::Combine($Fixture.StoreRoot,'a'*64,$Fixture.Selection.buildId,'receipt.json')}
function Get-PutCount($Fixture){[int](@($Fixture.Counts.Keys|Where-Object { $_.StartsWith('put ',[StringComparison]::Ordinal) }).Count)}
function Get-TotalPuts($Fixture){[int](@($Fixture.Counts.GetEnumerator()|Where-Object {$_.Key.StartsWith('put ',[StringComparison]::Ordinal)}|ForEach-Object {$_.Value}|Measure-Object -Sum).Sum)}
function Assert-Failure($Result){Assert ($Result.status -ceq 'failed' -and $null -eq $Result.reference -and $null -eq $Result.packageSha256 -and $null -eq $Result.receiptPath) 'failure exposed success'}
function Assert-FetchFailure($Result){Assert ($Result.status -ceq 'failed' -and $null -eq $Result.outputPath) 'fetch exposed unverified output'}
function Get-OfflineChildArgs($Fixture,[string]$Mode){
    return @('-NoProfile','-File',[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'BuildPublishChild.ps1')),'-Mode',$Mode,'-Config',$Fixture.Config,'-Selection',$Fixture.SelectionPath,'-StoreRoot',$Fixture.StoreRoot,'-TransferRoot',$Fixture.TransferRoot,'-DeploymentRoot',$Fixture.DeploymentRoot,'-RemoteRoot',$Fixture.RemoteRoot)
}
function Get-TreeFingerprint([string]$Root){
    if(-not [IO.Directory]::Exists($Root)){return 'absent'}
    return (@([IO.Directory]::EnumerateFiles($Root,'*',[IO.SearchOption]::AllDirectories)|Sort-Object|ForEach-Object{[IO.Path]::GetRelativePath($Root,$_)+'|'+(Get-FileHash -LiteralPath $_).Hash}) -join "`n")
}
function Rewrite-BuildZip([byte[]]$Archive,[string]$Mode){
    $input=[IO.MemoryStream]::new($Archive,$false);$output=[IO.MemoryStream]::new()
    try{
        $source=[IO.Compression.ZipArchive]::new($input,[IO.Compression.ZipArchiveMode]::Read,$false)
        $destination=[IO.Compression.ZipArchive]::new($output,[IO.Compression.ZipArchiveMode]::Create,$true)
        try{
            foreach($entry in $source.Entries){
                $stream=$entry.Open();$buffer=[IO.MemoryStream]::new()
                try{$stream.CopyTo($buffer);$data=$buffer.ToArray()}finally{$stream.Dispose();$buffer.Dispose()}
                if($Mode -ceq 'manifest' -and $entry.FullName -ceq 'manifest.json'){
                    $v=[Text.Encoding]::UTF8.GetString($data)|ConvertFrom-Json -Depth 12;$v.project='other-project'
                    $data=[Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $v -Compress -Depth 12))
                }
                if($Mode -ceq 'entry' -and $entry.FullName -ceq 'payload/sample.bin'){$data[0]=$data[0] -bxor 1}
                $new=$destination.CreateEntry($entry.FullName,[IO.Compression.CompressionLevel]::NoCompression)
                $writer=$new.Open();try{$writer.Write($data,0,$data.Length)}finally{$writer.Dispose()}
            }
        }finally{$destination.Dispose();$source.Dispose()}
        return $output.ToArray()
    }finally{$input.Dispose();$output.Dispose()}
}
function Invoke-Child([string[]]$ChildArguments){
    $p=[Diagnostics.Process]::new();$p.StartInfo.FileName=(Get-Process -Id $PID).Path;$p.StartInfo.UseShellExecute=$false;$p.StartInfo.CreateNoWindow=$true;$p.StartInfo.RedirectStandardOutput=$true;$p.StartInfo.RedirectStandardError=$true
    foreach($a in $ChildArguments){$p.StartInfo.ArgumentList.Add($a)}
    try{$null=$p.Start();$out=$p.StandardOutput.ReadToEndAsync();$err=$p.StandardError.ReadToEndAsync();if(-not $p.WaitForExit(15000)){$p.Kill($true);$null=$p.WaitForExit(5000);throw 'child-timeout'};Assert ($out.Wait(3000) -and $err.Wait(3000)) 'child-pipes';return @{Exit=$p.ExitCode;Output=$out.Result;Error=$err.Result}}finally{$p.Dispose()}
}
function Start-Child([string[]]$ChildArguments){
    $p=[Diagnostics.Process]::new();$p.StartInfo.FileName=(Get-Process -Id $PID).Path;$p.StartInfo.UseShellExecute=$false;$p.StartInfo.CreateNoWindow=$true;$p.StartInfo.RedirectStandardOutput=$true;$p.StartInfo.RedirectStandardError=$true
    foreach($a in $ChildArguments){$p.StartInfo.ArgumentList.Add($a)}
    $null=$p.Start()
    return @{Process=$p;Output=$p.StandardOutput.ReadToEndAsync();Error=$p.StandardError.ReadToEndAsync()}
}
function Finish-Child($Job){
    try{
        if(-not $Job.Process.WaitForExit(30000)){$Job.Process.Kill($true);$null=$Job.Process.WaitForExit(5000);throw 'child-timeout'}
        Assert ($Job.Output.Wait(3000) -and $Job.Error.Wait(3000)) 'child-pipes'
        return @{Exit=$Job.Process.ExitCode;Output=$Job.Output.Result;Error=$Job.Error.Result}
    }finally{$Job.Process.Dispose()}
}
foreach($caseName in $selected){
    $fixture=$null
    try{
        Assert ($caseName -cin $registered) 'unknown-case'
        $fixture=New-BuildFixture
        switch -CaseSensitive ($caseName){
            'snapshot-roundtrip'{
                $p=Invoke-BuildPublish $fixture.Config $fixture.SelectionPath;Assert ($p.status -ceq 'passed') 'publish failed'
                [IO.File]::WriteAllText([IO.Path]::Combine($fixture.Source,'sample.bin'),'changed')
                $i=Invoke-BuildInspect $fixture.Config $fixture.Selection.buildId
                Assert ($i.status -ceq 'passed' -and $i.reference -ceq $p.reference -and $i.packageSha256 -ceq $p.packageSha256) 'inspect mismatch'
                $f=Invoke-BuildFetch $fixture.Config $p.reference $p.packageSha256
                Assert ($f.status -ceq 'passed' -and [IO.File]::ReadAllText([IO.Path]::Combine($f.outputPath,'sample.bin')) -ceq 'safe-build-payload') 'fetch mismatch'
                Assert ((Get-TotalPuts $fixture) -eq 1) 'PUT count'
            }
            'input-and-size-reject'{
                foreach($variant in @('empty','duplicate','case','traversal','schema','series','limit','over-512')){
                    New-Id $fixture
                    $original=$fixture.Selection.files;$oldSchema=$fixture.Selection.schemaVersion;$oldProject=$fixture.Selection.project
                    switch($variant){'empty'{$fixture.Selection.files=@()};'duplicate'{$fixture.Selection.files=@('sample.bin','sample.bin')};'case'{$fixture.Selection.files=@('sample.bin','SAMPLE.BIN')};'traversal'{$fixture.Selection.files=@('../sample.bin')};'schema'{$fixture.Selection.schemaVersion=3};'series'{$fixture.Selection.project='Bad'};'limit'{Set-EvidenceTestValue BuildApplication SnapshotLimit 1};'over-512'{$fixture.Selection.files=@(1..513|ForEach-Object {'file-'+$_+'.bin'})}}
                    Write-PrivateJson $fixture.SelectionPath $fixture.Selection
                    $r=Invoke-BuildPublish $fixture.Config $fixture.SelectionPath;Assert-Failure $r
                    $fixture.Selection.files=$original;$fixture.Selection.schemaVersion=$oldSchema;$fixture.Selection.project=$oldProject
                    Set-EvidenceTestValue BuildApplication SnapshotLimit 240MB
                }
                $owned=[IO.Path]::Combine($fixture.StoreRoot,'other-build');[IO.Directory]::CreateDirectory($owned)|Out-Null
                [IO.File]::WriteAllText([IO.Path]::Combine($owned,'receipt.json'),'fixture-owned')
                $fixture.Selection.root=$owned;$fixture.Selection.files=@('receipt.json');New-Id $fixture
                Assert-Failure (Invoke-BuildPublish $fixture.Config $fixture.SelectionPath)
                $fixture.Selection.root=$fixture.Source;$fixture.Selection.files=@('sample.bin');New-Id $fixture
                $outside=[IO.Path]::Combine($fixture.Root,'outside');[IO.Directory]::CreateDirectory($outside)|Out-Null
                [IO.File]::WriteAllText([IO.Path]::Combine($outside,'outside.bin'),'outside')
                $junction=[IO.Path]::Combine($fixture.Source,'junction')
                $null=New-Item -ItemType Junction -Path $junction -Target $outside
                try{Assert-Failure (Invoke-BuildPublish $fixture.Config $fixture.SelectionPath)}finally{Remove-Item -LiteralPath $junction -Force}
                Assert ((Get-TotalPuts $fixture) -eq 0) 'invalid input reached network'
            }
            'snapshot-conflict'{
                [IO.File]::WriteAllText([IO.Path]::Combine($fixture.Source,'extra.bin'),'extra')
                $fixture.Selection.files=@('sample.bin');Write-PrivateJson $fixture.SelectionPath $fixture.Selection
                Assert-Failure (Invoke-BuildPublish $fixture.Config $fixture.SelectionPath)
                [IO.File]::Delete([IO.Path]::Combine($fixture.Source,'extra.bin'))
                $writer=[IO.FileStream]::new([IO.Path]::Combine($fixture.Source,'sample.bin'),[IO.FileMode]::Open,[IO.FileAccess]::Write,[IO.FileShare]::None)
                try{New-Id $fixture;Assert-Failure (Invoke-BuildPublish $fixture.Config $fixture.SelectionPath)}finally{$writer.Dispose()}
                $op=New-ArtifactOperation;$snap=[IO.Path]::Combine($op,'snapshot');[IO.Directory]::CreateDirectory($snap)|Out-Null
                [IO.File]::WriteAllText([IO.Path]::Combine($snap,'sample.bin'),'tampered')
                $expected=[OneStarMaker.Artifacts.Packaging.PackageFile]::new();$expected.Path='sample.bin';$expected.Bytes=18;$expected.Sha256='0'*64
                $codec=[OneStarMaker.Artifacts.Packaging.BuildPackage]::new(('a'*64),'samplegame','windows-x64-player','il2cpp-high',[Guid]::NewGuid().ToString('N'))
                $rejected=$false;try{$null=$codec.CreateVerified($op,$snap,[OneStarMaker.Artifacts.Packaging.PackageFile[]]@($expected),60000)}catch{$rejected=$true}
                Assert $rejected 'modified snapshot accepted'
                Assert ((Get-TotalPuts $fixture) -eq 0) 'conflict reached network'
                New-Id $fixture
                Set-EvidenceTestValue BuildApplication PackageCreatedHook {param($path)$data=[IO.File]::ReadAllBytes($path);$data[$data.Length-1]=$data[$data.Length-1] -bxor 1;[IO.File]::WriteAllBytes($path,$data)}
                Assert-Failure (Invoke-BuildPublish $fixture.Config $fixture.SelectionPath)
                Assert ((Get-TotalPuts $fixture) -eq 0) 'changed ZIP reached network before final validation'
                Set-EvidenceTestValue BuildApplication PackageCreatedHook $null
                New-Id $fixture;$state=[pscustomobject]@{tamperRejected=$false}
                $attempt={param($phase,$intent)if($phase -ceq 'before-put'){$path=[IO.Path]::Combine($intent.operationPath,'bundle.zip');try{[IO.File]::WriteAllBytes($path,[byte[]]@(1,2,3))}catch{$state.tamperRejected=$true}}}.GetNewClosure()
                Set-EvidenceTestValue BuildApplication FaultHook $attempt
                $p=Invoke-BuildPublish $fixture.Config $fixture.SelectionPath
                Assert ($p.status -ceq 'passed' -and $state.tamperRejected -and (Get-TotalPuts $fixture) -eq 1) 'ZIP read lock not held through PUT'
            }
            'put-unconfirmed'{
                $inner=$fixture.Network
                $lost={param($gen,$request)$null=& $inner $gen $request;throw 'response-lost'}.GetNewClosure()
                Set-EvidenceTestValue BuildApplication NetworkHook $lost
                $r=Invoke-BuildPublish $fixture.Config $fixture.SelectionPath;Assert-Failure $r
                Assert ((Get-TotalPuts $fixture) -eq 1 -and -not [IO.File]::Exists((Get-ReceiptPath $fixture)) -and $r.residue.remote -ceq 'may-have-been-sent') 'ambiguous PUT handling'
            }
            'readback-failure'{
                foreach($mode in @('error','hash','exit','pipe')){
                    New-Id $fixture;$inner=$fixture.Readback
                    $bad={param($op,$cfg,$gen,$key,$bytes,$hash)$v=& $inner $op $cfg $gen $key $bytes $hash;switch($mode){'error'{$v.childStatus='failed'};'hash'{$v.sha256='0'*64};'exit'{$v.childExit=1};'pipe'{$v.childStatus='unconfirmed'}};return $v}.GetNewClosure()
                    Set-EvidenceTestValue BuildApplication ReadbackHook $bad
                    $r=Invoke-BuildPublish $fixture.Config $fixture.SelectionPath;Assert-Failure $r
                    Assert (-not [IO.File]::Exists((Get-ReceiptPath $fixture))) 'readback wrote receipt'
                }
                Assert ((Get-TotalPuts $fixture) -eq 4) 'readback PUT count'
            }
            # rename前の失敗と、rename後に返答だけ失った状態は別物。
            # 親の成功値を子へ横渡しせず、事前selectionだけで正本へ戻れることを検査する。
            'receipt-commit-failure'{
                foreach($phase in @('before-write','before-flush','before-rename','after-rename')){
                    New-Id $fixture
                    $hook={param($where,$name)if($name -ceq 'receipt.json' -and $where -ceq $phase){if($phase -ceq 'after-rename'){throw 'after-rename-unconfirmed'};throw 'injected-receipt-failure'}}.GetNewClosure()
                    Set-EvidenceTestValue BuildStore FaultHook $hook
                    $r=Invoke-BuildPublish $fixture.Config $fixture.SelectionPath;Assert-Failure $r
                    $i=Invoke-BuildInspect $fixture.Config $fixture.Selection.buildId
                    if($phase -ceq 'after-rename'){
                        Assert ($r.reasonCode -ceq 'commit-unconfirmed' -and $i.status -ceq 'passed') 'lost response not inspectable'
                        $fresh=Invoke-Child (Get-OfflineChildArgs $fixture 'inspect')
                        Assert ($fresh.Exit -eq 0) 'fresh public inspect failed'
                        $observed=$fresh.Output.Trim()|ConvertFrom-Json -Depth 12 -DateKind String
                        $receipt=Get-Content -LiteralPath (Get-ReceiptPath $fixture) -Raw|ConvertFrom-Json -Depth 12 -DateKind String
                        Assert ($observed.reference -ceq $i.reference -and $observed.packageSha256 -ceq $receipt.packageSha256 -and $observed.publishedAt -ceq $receipt.publishedAt) 'fresh inspect identity changed'
                        $again=Invoke-BuildPublish $fixture.Config $fixture.SelectionPath;Assert-Failure $again
                    }else{Assert ($i.status -ceq 'failed') 'precommit failure became success'}
                    Set-EvidenceTestValue BuildStore FaultHook $null
                }
                Assert ((Get-TotalPuts $fixture) -eq 4) 'receipt failures repeated PUT'
                New-Id $fixture
                $simultaneous=Get-OfflineChildArgs $fixture 'publish'
                $one=Start-Child $simultaneous;$two=Start-Child $simultaneous
                $first=Finish-Child $one;$second=Finish-Child $two
                $outcomes=@($first,$second)
                Assert ((@($outcomes|Where-Object {$_.Exit -eq 0}).Count -eq 1) -and (@($outcomes|Where-Object {$_.Exit -eq 1}).Count -eq 1)) 'simultaneous publish did not select one owner'
                Assert (@([IO.Directory]::EnumerateFiles($fixture.RemoteRoot,'put-attempt-*.txt')).Count -eq 1 -and @([IO.Directory]::EnumerateFiles($fixture.RemoteRoot,'*.bin')).Count -eq 1) 'simultaneous publish attempted multiple PUTs'
            }
            'fetch-integrity'{
                $p=Invoke-BuildPublish $fixture.Config $fixture.SelectionPath;Assert ($p.status -ceq 'passed') 'publish failed'
                Assert-FetchFailure (Invoke-BuildFetch $fixture.Config $p.reference ('0'*64))
                Assert-FetchFailure (Invoke-BuildFetch $fixture.Config ('osm-evidence-v2:'+$p.reference.Substring(13)) $p.packageSha256)
                Import-Module (Join-Path $PSScriptRoot '../EvidenceContract.psm1')
                $foreignAccepted=$false
                try{$null=Read-EvidenceReference $p.reference (Read-EvidenceConfig $fixture.EvidenceConfigPath);$foreignAccepted=$true}catch{}
                Assert (-not $foreignAccepted) 'Build reference accepted as Evidence'
                Import-Module (Join-Path $PSScriptRoot '../Transport/R2ArtifactTransport.psm1')
                Assert ([IO.File]::Exists([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Transport/artifacts/transport/R2ArtifactTransport.dll')))) 'transport DLL missing'
                $request=@{Endpoint=('https://'+('e'*32)+'.r2.cloudflarestorage.com');Operation='get';Key='development/builds/v1/outside/bundle.zip';SourcePath=$null;DestinationPath=$null;DeadlineMilliseconds=1000}
                $accepted=$false;try{$null=Invoke-R2ArtifactOperation 'DUMMY_ID' 'DUMMY_SECRET' ('f'*32) $request;$accepted=$true}catch{}
                Assert (-not $accepted) 'outside Build key accepted'
                $key=$fixture.Objects.Keys|Select-Object -First 1
                $request.Key=$key;$request.Operation='delete'
                $accepted=$false;try{$null=Invoke-R2ArtifactOperation 'DUMMY_ID' 'DUMMY_SECRET' ('f'*32) $request;$accepted=$true}catch{}
                Assert (-not $accepted) 'Build DELETE accepted'
                $original=$fixture.Objects[$key]
                foreach($mode in @('manifest','entry')){
                    $changed=Rewrite-BuildZip $original $mode;$fixture.Objects[$key]=$changed
                    $changedHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($changed)).ToLowerInvariant()
                    Assert-FetchFailure (Invoke-BuildFetch $fixture.Config $p.reference $changedHash)
                }
                $fixture.Objects[$key]=$original
            }
            'existing-success-preserved'{
                $monitors=@('workflow','evidence','credentials')
                foreach($name in $monitors){$dir=[IO.Path]::Combine($fixture.Root,$name);[IO.Directory]::CreateDirectory($dir)|Out-Null;[IO.File]::WriteAllText([IO.Path]::Combine($dir,'sentinel.txt'),$name)}
                $before=@{};foreach($name in $monitors){$before[$name]=Get-TreeFingerprint ([IO.Path]::Combine($fixture.Root,$name))}
                $p=Invoke-BuildPublish $fixture.Config $fixture.SelectionPath;Assert ($p.status -ceq 'passed') 'first publish failed'
                $old=Get-ReceiptPath $fixture;$hash=(Get-FileHash -LiteralPath $old).Hash
                $key=$fixture.Objects.Keys|Select-Object -First 1;$packageHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($fixture.Objects[$key])).ToLowerInvariant()
                New-Id $fixture;Set-EvidenceTestValue BuildApplication NetworkHook {throw 'second-put-failed'}
                Assert-Failure (Invoke-BuildPublish $fixture.Config $fixture.SelectionPath)
                Assert ((Get-FileHash -LiteralPath $old).Hash -ceq $hash -and (Get-PutCount $fixture) -eq 1) 'old receipt changed'
                Assert ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($fixture.Objects[$key])).ToLowerInvariant() -ceq $packageHash) 'old remote package changed'
                foreach($name in $monitors){Assert ((Get-TreeFingerprint ([IO.Path]::Combine($fixture.Root,$name))) -ceq $before[$name]) "unrelated $name store changed"}
                Assert (-not @($fixture.Counts.Keys|Where-Object {$_.StartsWith('delete ',[StringComparison]::Ordinal)}).Count) 'DELETE called'
            }
            # 親がglobal importした関数やmockだけで成立したように見せない。
            # 実CLIのgrammar・終了コード・別processのreaderを非秘密fixtureで通す。
            'fresh-process-entrypoints'{
                # 各入口を独立pwshの実CLIで通す。publish時の読戻しも別childが保存bytesを読む。
                $published=Invoke-Child (Get-OfflineChildArgs $fixture 'publish')
                Assert ($published.Exit -eq 0) 'fresh CLI publish failed'
                $p=$published.Output.Trim()|ConvertFrom-Json -Depth 12 -DateKind String
                Assert ($p.status -ceq 'passed' -and $p.reference -and $p.packageSha256) 'fresh publish identity missing'
                Assert (@([IO.Directory]::EnumerateFiles($fixture.RemoteRoot,'put-attempt-*.txt')).Count -eq 1) 'fresh CLI made multiple PUT attempts'
                $inspected=Invoke-Child (Get-OfflineChildArgs $fixture 'inspect')
                Assert ($inspected.Exit -eq 0) 'fresh CLI inspect failed'
                $i=$inspected.Output.Trim()|ConvertFrom-Json -Depth 12 -DateKind String
                Assert ($i.reference -ceq $p.reference -and $i.packageSha256 -ceq $p.packageSha256) 'fresh inspect mismatch'
                $fetched=Invoke-Child ((Get-OfflineChildArgs $fixture 'fetch')+@('-Reference',$p.reference,'-Sha256',$p.packageSha256))
                Assert ($fetched.Exit -eq 0) 'fresh CLI fetch failed'
                $f=$fetched.Output.Trim()|ConvertFrom-Json -Depth 12 -DateKind String
                Assert ($f.status -ceq 'passed' -and [IO.File]::ReadAllText([IO.Path]::Combine($f.outputPath,'sample.bin')) -ceq 'safe-build-payload') 'fresh fetch bytes mismatch'
                $cli=Invoke-Child @('-NoProfile','-File',[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts.ps1')),'build','delete','--profile','osm','--config',$fixture.Config)
                Assert ($cli.Exit -eq 1 -and $cli.Output.Contains('unsupported-command')) 'Build DELETE grammar accepted'
            }
        }
    }catch{$failed.Add($caseName+': '+$_.Exception.GetType().Name+': '+$_.Exception.Message)}
    finally{
        $executed.Add($caseName)
        foreach($name in @('NetworkHook','ReadbackHook','FaultHook','FailureHook','PackageCreatedHook')){Set-EvidenceTestValue BuildApplication $name $null}
        Set-EvidenceTestValue BuildStore FaultHook $null;Set-EvidenceTestValue BuildApplication SnapshotLimit 240MB
        if($fixture){$target=[IO.Path]::GetFullPath($fixture.Root);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath());if(-not $target.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($target) -cnotmatch '\Aosm-build-test-[0-9a-f]{32}\z'){throw 'unsafe-test-cleanup'};if([IO.Directory]::Exists($target)){[IO.Directory]::Delete($target,$true)}}
    }
}
if($selected.Count -eq 0 -or $selected.Count -ne $executed.Count){$failed.Add('selection')}
if($ResultPath){Write-EvidenceTestJson $ResultPath ([ordered]@{registered=$registered;selected=$selected;executed=@($executed);failed=@($failed);binaries=@(Get-EvidenceTestBinaries)})}
foreach($item in $failed){[Console]::Error.WriteLine($item)}
[Console]::WriteLine("BuildPublish $($executed.Count), failed $($failed.Count)")
if($failed.Count){exit 1}
