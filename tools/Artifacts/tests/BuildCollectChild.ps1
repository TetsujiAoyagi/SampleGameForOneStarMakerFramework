param(
    [Parameter(Mandatory=$true)][string]$Config,[Parameter(Mandatory=$true)][string]$Source,[string]$ArtifactsRoot='',
    [string]$StoreRoot,[string]$TransferRoot,[string]$DeploymentRoot,[string]$RemoteRoot,[string]$OwnedRoots,
    [string]$ReadyEvent='',[string]$GoEvent=''
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# 実CLIのgrammarと終了コードを別processで通すための子。通信はofflineのremote directoryに限る。
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../BuildApplication.psm1')
Set-EvidenceTestValue BuildStore TestRoot $StoreRoot
Set-EvidenceTestValue ArtifactPaths TestRoot $TransferRoot
Set-EvidenceTestValue EvidenceContract DeploymentRoot $DeploymentRoot
Set-EvidenceTestValue BuildContract OwnedRootsForTest @($OwnedRoots.Split('|'))
function Get-RemotePath([string]$ObjectKey){[IO.Path]::Combine($RemoteRoot,[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($ObjectKey))).ToLowerInvariant()+'.bin')}
$offlineTransport={param($gen,$request)
    $path=Get-RemotePath $request.Key
    if($request.Operation -ceq 'put'){
        [IO.File]::WriteAllText([IO.Path]::Combine($RemoteRoot,'put-attempt-'+[Guid]::NewGuid().ToString('N')+'.txt'),$request.Key)
        $data=[IO.File]::ReadAllBytes($request.SourcePath)
        $stream=[IO.FileStream]::new($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try{$stream.Write($data,0,$data.Length);$stream.Flush($true)}finally{$stream.Dispose()}
        $bytes=$null;$hash=$null
    }elseif($request.Operation -ceq 'get'){
        $data=[IO.File]::ReadAllBytes($path);[IO.File]::WriteAllBytes($request.DestinationPath,$data)
        $bytes=[long]$data.Length;$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant()
    }else{throw 'offline-delete-forbidden'}
    return [pscustomobject]@{Operation=$request.Operation;Generation=$gen;HttpStatus=200;StatusClass='success';S3Code=$null;Bytes=$bytes;Sha256=$hash;StatusObserved=$true;TimedOut=$false;Redirected=$false}
}.GetNewClosure()
$offlineReadback={param($op,$cfg,$gen,$objectKey,$bytes,$hash)
    $data=[IO.File]::ReadAllBytes((Get-RemotePath $objectKey));$actual=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant()
    return [pscustomobject]@{operation='readback';generation=$gen;key=$objectKey;bytes=[long]$data.Length;sha256=$actual;childExit=0;httpStatus=200;statusClass='success';s3Code=$null;statusObserved=$true;timedOut=$false;redirected=$false;childStatus=if($actual -ceq $hash){'passed'}else{'failed'};observedAt=[DateTimeOffset]::UtcNow.ToString('o')}
}.GetNewClosure()
Set-EvidenceTestValue BuildApplication NetworkHook $offlineTransport
Set-EvidenceTestValue BuildApplication ReadbackHook $offlineReadback
foreach($module in @(Get-EvidenceTestModules BuildApplication)){& $module {function script:Get-CredentialStatus {param($profile)[pscustomobject]@{Generation=('f'*32)}}}}
if($ReadyEvent){
    # 並行検査用のbarrier。両processがrename直前に揃うまで親のsignalで待ち、時間待機は使わない。
    $ready=[Threading.EventWaitHandle]::OpenExisting($ReadyEvent);$go=[Threading.EventWaitHandle]::OpenExisting($GoEvent)
    $barrier={param($phase,$staging)if($phase -ceq 'before-rename'){$null=$ready.Set();if(-not $go.WaitOne(60000)){throw 'barrier-timeout'}}}.GetNewClosure()
    Set-EvidenceTestValue BuildStage StageHook $barrier
}
$cli=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts.ps1'))
if($ArtifactsRoot){& $cli build collect --profile osm --config $Config --source $Source --artifacts-root $ArtifactsRoot}
else{& $cli build collect --profile osm --config $Config --source $Source}
if($null -eq $LASTEXITCODE -or $LASTEXITCODE -notin @(0,1)){exit 1}
exit ([int]$LASTEXITCODE)
