param(
    [string]$CliArgs='',[string]$HoldDirectory='',
    [string]$StoreRoot,[string]$BuildRoot,[string]$TransferRoot,[string]$DeploymentRoot,[string]$RemoteRoot,[string]$OwnedRoots,
    [string]$Now,[int]$KeepCount=10,[string]$NetworkMode='normal',
    [string]$Barrier='',[string]$ReadyEvent='',[string]$GoEvent=''
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# 実CLIのgrammar・終了コード・lockの保持を別processで通すための子。通信はofflineのremote directoryに限る。
# HoldDirectoryを指定すると、そのrevisionのpublish.lockを（collect/inspectの代わりに）保持して親のGoを待つ。
# Barrierを指定すると、その操作の送信直前で親へReadyを通知し、親のGoまで待つ（時間待機は使わない）。
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../ContentCleanup.psm1')
Import-Module (Join-Path $PSScriptRoot '../ContentStore.psm1')
Import-Module (Join-Path $PSScriptRoot '../Transport/R2ArtifactTransport.psm1')
# 実storeに触れないよう、全rootを親のfixtureへ向けてから何かを読む。
Set-EvidenceTestValue ContentStore TestRoot $StoreRoot
$null=Enable-EvidenceTestStoreRoot ([IO.Path]::GetDirectoryName($TransferRoot))
Set-EvidenceTestValue BuildStore TestRoot $BuildRoot
Set-EvidenceTestValue ArtifactPaths TestRoot $TransferRoot
Set-EvidenceTestValue EvidenceContract DeploymentRoot $DeploymentRoot
Set-EvidenceTestValue BuildContract OwnedRootsForTest @($OwnedRoots.Split('|'))
$ready=$null;$go=$null
if($ReadyEvent){$ready=[Threading.EventWaitHandle]::OpenExisting($ReadyEvent);$go=[Threading.EventWaitHandle]::OpenExisting($GoEvent)}
if($HoldDirectory){
    $lock=Enter-ContentPublishLock $HoldDirectory
    try{$null=$ready.Set();if(-not $go.WaitOne(60000)){exit 1}}finally{$lock.Dispose()}
    exit 0
}
$fixed=[DateTimeOffset]::Parse($Now,[Globalization.CultureInfo]::InvariantCulture)
Set-EvidenceTestValue ContentCleanup Clock ({$fixed}.GetNewClosure())
Set-EvidenceTestValue ContentCleanup KeepCount $KeepCount
$network={param($gen,$request)
    if($Barrier -ceq $request.Operation){$null=$ready.Set();if(-not $go.WaitOne(60000)){throw 'barrier-timeout'}}
    if(-not (Test-R2ArtifactRequestAllowed $request)){throw 'transport-rejected'}
    $path=[IO.Path]::Combine($RemoteRoot,[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($request.Key))).ToLowerInvariant()+'.bin')
    $status=200;$class='success';$code=$null;$bytes=$null
    if($request.Operation -ceq 'delete'){
        if($NetworkMode -ceq 'forbidden'){$status=403;$class='forbidden';$code='AccessDenied'}else{[IO.File]::Delete($path);$status=204}
    }elseif($request.Operation -ceq 'get'){
        if([IO.File]::Exists($path)){$data=[IO.File]::ReadAllBytes($path);[IO.File]::WriteAllBytes($request.DestinationPath,$data);$bytes=[long]$data.Length}
        else{$status=404;$class='not-found';$code='NoSuchKey'}
    }else{throw 'offline-put-forbidden'}
    return [pscustomobject]@{Operation=$request.Operation;Generation=$gen;HttpStatus=$status;StatusClass=$class;S3Code=$code;Bytes=$bytes;Sha256=$null;StatusObserved=$true;TimedOut=$false;Redirected=$false}
}.GetNewClosure()
Set-EvidenceTestValue ContentCleanup NetworkHook $network
foreach($module in @(Get-EvidenceTestModules ContentCleanup)){& $module {function script:Get-CredentialStatus {param($profile)[pscustomobject]@{Generation=('f'*32)}}}}
$cli=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts.ps1'))
$arguments=@($CliArgs.Split('|'))
& $cli @arguments
if($null -eq $LASTEXITCODE -or $LASTEXITCODE -notin @(0,1,2)){exit 1}
exit ([int]$LASTEXITCODE)
