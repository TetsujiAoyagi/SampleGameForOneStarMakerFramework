param(
    [Parameter(Mandatory=$true)][string]$Mode,[Parameter(Mandatory=$true)][string]$Config,
    [string]$Source,[string]$ArtifactsRoot,[string]$Reference,[string]$Sha256,[string]$ContentSet,[string]$Target,[string]$Revision,
    [string]$StoreRoot,[string]$TransferRoot,[string]$DeploymentRoot,[string]$RemoteRoot,[string]$OwnedRoots,
    [string]$ReadyEvent='',[string]$GoEvent='',[string]$StageReadyEvent='',[string]$StageGoEvent=''
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# 実CLIのgrammarと終了コード、別processからのfetch、同時公開のlockを検査するための子。
# 通信はoffline（file-backedの遠隔directory）に限る。待機は親のsignalだけで、時間待機は使わない。
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
. (Join-Path $PSScriptRoot 'ContentTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../ContentApplication.psm1')
Set-EvidenceTestValue ContentStore TestRoot $StoreRoot
$null=Enable-EvidenceTestStoreRoot ([IO.Path]::GetDirectoryName($TransferRoot))
Set-EvidenceTestValue ArtifactPaths TestRoot $TransferRoot
Set-EvidenceTestValue EvidenceContract DeploymentRoot $DeploymentRoot
Set-EvidenceTestValue BuildContract OwnedRootsForTest @($OwnedRoots.Split('|'))
Set-EvidenceTestValue ContentTransfer Clock {[long]0}
$barrier=if($ReadyEvent){@{Ready=[Threading.EventWaitHandle]::OpenExisting($ReadyEvent);Go=[Threading.EventWaitHandle]::OpenExisting($GoEvent)}}else{$null}
$transport={param($gen,$request)
    $path=Get-RemotePath $RemoteRoot $request.Key;$status=200;$class='success';$code=$null;$bytes=$null;$hash=$null
    if($request.Operation -ceq 'put'){
        [IO.File]::WriteAllText([IO.Path]::Combine($RemoteRoot,'put-attempt-'+[Guid]::NewGuid().ToString('N')+'.txt'),$request.Key)
        # 最初のPUTの直前（lock保持中）で親に知らせ、親のGoまで進まない。
        if($barrier){$null=$barrier.Ready.Set();if(-not $barrier.Go.WaitOne(120000)){throw 'barrier-timeout'}}
        [IO.File]::WriteAllBytes($path,[IO.File]::ReadAllBytes($request.SourcePath))
    }elseif($request.Operation -ceq 'get'){
        [IO.File]::WriteAllText([IO.Path]::Combine($RemoteRoot,'get-attempt-'+[Guid]::NewGuid().ToString('N')+'.txt'),$request.Key)
        if(-not [IO.File]::Exists($path)){$status=404;$class='not-found';$code='NoSuchKey'}
        else{$data=[IO.File]::ReadAllBytes($path);[IO.File]::WriteAllBytes($request.DestinationPath,$data);$bytes=[long]$data.Length;$hash=Get-Sha $data}
    }else{throw 'offline-delete-forbidden'}
    [pscustomobject]@{Operation=$request.Operation;Generation=$gen;HttpStatus=$status;StatusClass=$class;S3Code=$code;Bytes=$bytes;Sha256=$hash;StatusObserved=$true;TimedOut=$false;Redirected=$false}
}.GetNewClosure()
$readback={param($op,$cfg,$gen,$key,$bytes,$hash)
    $path=Get-RemotePath $RemoteRoot $key
    Get-ReadResult $gen $key $(if([IO.File]::Exists($path)){[IO.File]::ReadAllBytes($path)}else{$null}) $bytes $hash
}.GetNewClosure()
Set-EvidenceTestValue ArtifactApplication TransportHook $transport
Set-EvidenceTestValue ArtifactApplication ReadbackHook $readback
foreach($m in @(Get-EvidenceTestModules ContentTransfer)){& $m {function script:Get-CredentialStatus {param($profile)[pscustomobject]@{Generation=('f'*32)}}}}
if($StageReadyEvent){
    # 退避のrename直前で両processを揃える（signal同期。時間待機は使わない）。
    $sr=[Threading.EventWaitHandle]::OpenExisting($StageReadyEvent);$sg=[Threading.EventWaitHandle]::OpenExisting($StageGoEvent)
    Set-EvidenceTestValue BuildStage StageHook ({param($phase,$staging)if($phase -ceq 'before-rename'){$null=$sr.Set();if(-not $sg.WaitOne(120000)){throw 'barrier-timeout'}}}.GetNewClosure())
}
$cli=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts.ps1'))
switch($Mode){
    'collect'{if($ArtifactsRoot){& $cli content collect --profile osm --config $Config --source $Source --artifacts-root $ArtifactsRoot}else{& $cli content collect --profile osm --config $Config --source $Source}}
    'inspect'{& $cli content inspect --profile osm --config $Config --content-set $ContentSet --target $Target --revision $Revision}
    'fetch'{& $cli content fetch --profile osm --config $Config --reference $Reference --sha256 $Sha256}
    default{exit 1}
}
if($null -eq $LASTEXITCODE -or $LASTEXITCODE -notin @(0,1)){exit 1}
exit ([int]$LASTEXITCODE)
