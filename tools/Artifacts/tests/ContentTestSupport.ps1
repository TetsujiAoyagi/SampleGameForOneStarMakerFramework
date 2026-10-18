Set-StrictMode -Version Latest
# Content 3 suite共通のoffline fixture。実R2・資格情報・ネットワークは使わない。
# 遠隔はfile-backedの偽storeで、同じstoreを親（in-process hook）と子process（child script）が共有する。
# 時間待機は使わず、予算は注入した時計（$Fx.Now）を進めて検査する。
$script:Identity='20260924T202227427Z-0123456789abcdef0123456789abcdef'
function Get-Sha([byte[]]$Bytes){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()}
function Write-Bytes([string]$Path,[byte[]]$Bytes){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))|Out-Null;[IO.File]::WriteAllBytes($Path,$Bytes)}
function Write-PrivateJson([string]$Path,$Value){Write-EvidenceTestJson $Path $Value;Set-ArtifactAcl $Path $false}
function Get-RemotePath([string]$Remote,[string]$ObjectKey){[IO.Path]::Combine($Remote,(Get-Sha ([Text.Encoding]::UTF8.GetBytes($ObjectKey)))+'.bin')}
function Get-ReadResult([string]$Gen,[string]$ObjectKey,$Data,[long]$Bytes,[string]$Hash){
    if($null -eq $Data){return [pscustomobject]@{operation='readback';generation=$Gen;key=$ObjectKey;bytes=$null;sha256=$null;childExit=1;httpStatus=404;statusClass='not-found';s3Code='NoSuchKey';statusObserved=$true;timedOut=$false;redirected=$false;childStatus='failed';observedAt=[DateTimeOffset]::UtcNow.ToString('o')}}
    $actual=Get-Sha $Data;$ok=$Data.Length -eq $Bytes -and $actual -ceq $Hash
    return [pscustomobject]@{operation='readback';generation=$Gen;key=$ObjectKey;bytes=[long]$Data.Length;sha256=$actual;childExit=$(if($ok){0}else{1});httpStatus=200;statusClass='success';s3Code=$null;statusObserved=$true;timedOut=$false;redirected=$false;childStatus=$(if($ok){'passed'}else{'failed'});observedAt=[DateTimeOffset]::UtcNow.ToString('o')}
}
# 偽のContent出力。名前は実Spring出力と同じ形（内容hash由来の平坦な32hex+.cf/.resS、BuildManifestHash.txt）。
function New-ContentSource($Fx,[string]$Revision=$script:Identity,[hashtable]$Override=@{},[int]$Count=6,[string]$Seed='a'){
    $dir=[IO.Path]::Combine($Fx.Repo,'artifacts','bs4','transport',$Revision)
    $names=[Collections.Generic.List[string]]::new();$files=[Collections.Generic.List[object]]::new()
    $all=@(1..$Count|ForEach-Object{@{name=(Get-Sha ([Text.Encoding]::UTF8.GetBytes("$Seed$_"))).Substring(0,32)+$(if($_ % 3 -eq 0){'.resS'}else{'.cf'});data=[Text.Encoding]::UTF8.GetBytes("content-$Seed-$_-"+('x'*($_*50)))}})
    $all+=@(@{name='BuildManifestHash.txt';data=[Text.Encoding]::UTF8.GetBytes("manifest-hash-$Seed")})
    foreach($f in $all){Write-Bytes ([IO.Path]::Combine($dir,'content',$f.name)) $f.data;$names.Add($f.name);$files.Add([ordered]@{path=$f.name;size=$f.data.Length;sha256=(Get-Sha $f.data)})}
    $manifest=[ordered]@{version=1;product='OneStarMaker';contentSet='bs4-spring-full';revision=$Revision;target='StandaloneWindows64-Player';unityVersion='6000.6.0f1';rootSchemaVersion=2;files=@($files);sourceFiles=@(1..20|ForEach-Object{[ordered]@{path="Assets/Source/$_.asset";sha256=(Get-Sha ([Text.Encoding]::UTF8.GetBytes("s$_")))}})}
    foreach($k in @($Override.Keys)){$manifest[$k]=$Override[$k]}
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $manifest -Depth 8))
    Write-Bytes ([IO.Path]::Combine($dir,'transport.json')) $bytes
    return [pscustomobject]@{Dir=$dir;Revision=$Revision;Names=$names.ToArray();ManifestBytes=$bytes;ManifestSha=(Get-Sha $bytes)}
}
function New-ContentFixture {
    $root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-content-test-'+[Guid]::NewGuid().ToString('N'));[IO.Directory]::CreateDirectory($root)|Out-Null;$null=Enable-EvidenceTestStoreRoot $root
    $owned=@('transfers','contents-v1','deployments','workflow','evidence','credentials','builds-v1'|ForEach-Object{[IO.Path]::Combine($root,$_)})
    Set-EvidenceTestValue ArtifactPaths TestRoot $owned[0];Set-EvidenceTestValue ContentStore TestRoot $owned[1]
    Set-EvidenceTestValue EvidenceContract DeploymentRoot $owned[2];Set-EvidenceTestValue BuildContract OwnedRootsForTest $owned
    [IO.Directory]::CreateDirectory($owned[2])|Out-Null
    $ev=[ordered]@{schemaVersion=2;profile='osm';endpoint=('https://'+('e'*32)+'.r2.cloudflarestorage.com');bucket='osm-artifacts';repositoryId=('a'*64);prefix='development/evidence/v2/';policy='task-end-30d-v1';deploymentId=('d'*32)}
    $evPath=[IO.Path]::Combine($owned[2],('d'*32)+'.config.json');Write-EvidenceTestJson $evPath $ev;$evHash=(Get-FileHash -LiteralPath $evPath).Hash.ToLowerInvariant()
    Write-EvidenceTestJson ([IO.Path]::Combine($owned[2],('d'*32)+'.json')) ([ordered]@{schemaVersion=2;deploymentId=('d'*32);repositoryId=('a'*64);configSha256=$evHash;baselineRecordId='fixture';baselineSha256=('b'*64);ownerObservationId='fixture';ownerObservationSha256=('c'*64)})
    $configPath=[IO.Path]::Combine((New-ArtifactOperation),'content-config.json')
    Write-PrivateJson $configPath ([ordered]@{schemaVersion=1;purpose='content';profile='osm';evidenceConfigPath=$evPath;evidenceConfigSha256=$evHash;repositoryId=('a'*64);prefix='development/content/v1/';policy='content-publish-v1'})
    $artifacts=[IO.Path]::Combine($root,'artifacts-root');$remote=[IO.Path]::Combine($root,'remote');foreach($d in @($artifacts,$remote)){[IO.Directory]::CreateDirectory($d)|Out-Null}
    $fx=@{Root=$root;Repo=[IO.Path]::Combine($root,'repo');Owned=$owned;Config=$configPath;EvidencePath=$evPath;EvidenceHash=$evHash;ArtifactsRoot=$artifacts;Remote=$remote;Now=[long]0;OnTransport=$null;OnReadback=$null;Counts=@{};Order=[Collections.Generic.List[string]]::new();ReadDirs=[Collections.Generic.List[string]]::new();Requests=[Collections.Generic.List[object]]::new()}
    $transport={param($gen,$request)
        $fx.Counts[$request.Operation]=1+$(if($fx.Counts.ContainsKey($request.Operation)){$fx.Counts[$request.Operation]}else{0})
        $fx.Order.Add($request.Operation+' '+$request.Key);$fx.Requests.Add($request)
        if($request.Operation -ceq 'delete'){throw 'delete-forbidden'}
        if($fx.OnTransport){$o=& $fx.OnTransport $request;if($null -ne $o){return $o}}
        $path=Get-RemotePath $fx.Remote $request.Key;$status=200;$class='success';$code=$null;$bytes=$null;$sha=$null
        if($request.Operation -ceq 'put'){[IO.File]::WriteAllBytes($path,[IO.File]::ReadAllBytes($request.SourcePath))}
        elseif(-not [IO.File]::Exists($path)){$status=404;$class='not-found';$code='NoSuchKey'}
        else{$data=[IO.File]::ReadAllBytes($path);[IO.File]::WriteAllBytes($request.DestinationPath,$data);$bytes=[long]$data.Length;$sha=Get-Sha $data}
        [pscustomobject]@{Operation=$request.Operation;Generation=$gen;HttpStatus=$status;StatusClass=$class;S3Code=$code;Bytes=$bytes;Sha256=$sha;StatusObserved=$true;TimedOut=$false;Redirected=$false}
    }.GetNewClosure()
    $readback={param($op,$cfg,$gen,$key,$bytes,$hash)
        $fx.ReadDirs.Add($op);$fx.Order.Add('readback '+$key)
        if($fx.OnReadback){$o=& $fx.OnReadback $key;if($null -ne $o){return $o}}
        $path=Get-RemotePath $fx.Remote $key
        Get-ReadResult $gen $key $(if([IO.File]::Exists($path)){[IO.File]::ReadAllBytes($path)}else{$null}) $bytes $hash
    }.GetNewClosure()
    Set-EvidenceTestValue ArtifactApplication TransportHook $transport;Set-EvidenceTestValue ArtifactApplication ReadbackHook $readback
    Set-EvidenceTestValue ContentTransfer Clock ({[long]$fx.Now}.GetNewClosure())
    foreach($m in @(Get-EvidenceTestModules ContentTransfer)){& $m {function script:Get-CredentialStatus {param($profile)[pscustomobject]@{Generation=('f'*32)}}}}
    return $fx
}
function Clear-ContentFixture($Fx){
    foreach($n in @('TransportHook','ReadbackHook')){Set-EvidenceTestValue ArtifactApplication $n $null}
    Set-EvidenceTestValue ContentStore FaultHook $null;Set-EvidenceTestValue BuildStore FaultHook $null;Set-EvidenceTestValue BuildStage StageHook $null;Set-EvidenceTestValue ContentApplication FailureHook $null
    if($Fx){$target=[IO.Path]::GetFullPath($Fx.Root);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath());if(-not $target.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($target) -cnotmatch '\Aosm-content-test-[0-9a-f]{32}\z'){throw 'unsafe-test-cleanup'};if([IO.Directory]::Exists($target)){[IO.Directory]::Delete($target,$true)}}
}
function Set-Remote($Fx,[string]$ObjectKey,[byte[]]$Bytes){[IO.File]::WriteAllBytes((Get-RemotePath $Fx.Remote $ObjectKey),$Bytes)}
function Get-Remote($Fx,[string]$ObjectKey){$p=Get-RemotePath $Fx.Remote $ObjectKey;if([IO.File]::Exists($p)){[IO.File]::ReadAllBytes($p)}else{$null}}
function Clear-Order($Fx){$Fx.Order.Clear();$Fx.ReadDirs.Clear();$Fx.Counts.Clear();$Fx.Requests.Clear()}
function Get-Count($Fx,[string]$Operation){if($Fx.Counts.ContainsKey($Operation)){[int]$Fx.Counts[$Operation]}else{0}}
function Get-TreeFingerprint([string]$Root){
    if(-not [IO.Directory]::Exists($Root)){return 'absent'}
    return (@([IO.Directory]::EnumerateFiles($Root,'*',[IO.SearchOption]::AllDirectories)|Sort-Object|ForEach-Object{[IO.Path]::GetRelativePath($Root,$_)+'|'+(Get-FileHash -LiteralPath $_).Hash}) -join "`n")
}
function Get-StageDir($Fx,[string]$Revision=$script:Identity){[IO.Path]::Combine($Fx.ArtifactsRoot,'content','bs4-spring-full','windows-x64-player',$Revision)}
function Get-StoreDir($Fx,[string]$Revision=$script:Identity){[IO.Path]::Combine($Fx.Owned[1],('a'*64),'bs4-spring-full','windows-x64-player',$Revision)}
function Get-Key([string]$Path,[string]$Revision=$script:Identity){'development/content/v1/'+('a'*64)+'/bs4-spring-full/windows-x64-player/'+$Revision+'/'+$Path}
function Assert-Failed($Result,[string]$Code){
    Assert-EvidenceTest ($Result.status -ceq 'failed' -and $null -eq $Result.reference) "failure exposed success ($Code)"
    if($Code){Assert-EvidenceTest ($Result.reasonCode -ceq $Code) "expected $Code, got $($Result.reasonCode)"}
}
function Invoke-Child([string[]]$ChildArguments,[hashtable]$Environment=@{}){
    $p=[Diagnostics.Process]::new();$p.StartInfo.FileName=(Get-Process -Id $PID).Path;$p.StartInfo.UseShellExecute=$false;$p.StartInfo.CreateNoWindow=$true;$p.StartInfo.RedirectStandardOutput=$true;$p.StartInfo.RedirectStandardError=$true
    foreach($a in $ChildArguments){$p.StartInfo.ArgumentList.Add($a)};foreach($k in $Environment.Keys){$p.StartInfo.Environment[$k]=$Environment[$k]}
    $null=$p.Start();return @{Process=$p;Output=$p.StandardOutput.ReadToEndAsync();Error=$p.StandardError.ReadToEndAsync()}
}
function Finish-Child($Job){
    try{
        if(-not $Job.Process.WaitForExit(120000)){$Job.Process.Kill($true);$null=$Job.Process.WaitForExit(5000);throw 'child-timeout'}
        Assert-EvidenceTest ($Job.Output.Wait(3000) -and $Job.Error.Wait(3000)) 'child-pipes'
        return @{Exit=$Job.Process.ExitCode;Output=$Job.Output.Result;Error=$Job.Error.Result}
    }finally{$Job.Process.Dispose()}
}
# 子processは同じroots・同じfile-backed remoteを共有して実CLIを通す。Ready/Goは並行検査のbarrier（signal同期）。
function Get-ChildArgs($Fx,[string]$Mode,[hashtable]$Extra=@{}){
    $a=@('-NoProfile','-File',[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'ContentChild.ps1')),'-Mode',$Mode,'-Config',$Fx.Config,'-StoreRoot',$Fx.Owned[1],'-TransferRoot',$Fx.Owned[0],'-DeploymentRoot',$Fx.Owned[2],'-RemoteRoot',$Fx.Remote,'-OwnedRoots',($Fx.Owned -join '|'))
    foreach($k in $Extra.Keys){if($Extra[$k]){$a+=@("-$k",[string]$Extra[$k])}}
    return $a
}
