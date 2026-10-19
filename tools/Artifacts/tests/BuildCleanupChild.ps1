param(
    [Parameter(Mandatory=$true)][string]$CliArgs,
    [string]$StoreRoot,[string]$TransferRoot,[string]$DeploymentRoot,[string]$RemoteRoot,[string]$OwnedRoots,
    [string]$Now,[int]$KeepCount=10,[string]$NetworkMode='normal',
    [string]$Barrier='',[string]$ReadyEvent='',[string]$GoEvent=''
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# 実CLIのgrammar・終了コード・lockの保持を別processで通すための子。通信はofflineのremote directoryに限る。
# Barrierを指定すると、その地点で親へReadyを通知し、親のGoまで待つ（時間待機は使わない）。
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../BuildCleanup.psm1')
Import-Module (Join-Path $PSScriptRoot '../BuildApplication.psm1')
Import-Module (Join-Path $PSScriptRoot '../Transport/R2ArtifactTransport.psm1')
Set-EvidenceTestValue BuildStore TestRoot $StoreRoot
$null=Enable-EvidenceTestStoreRoot ([IO.Path]::GetDirectoryName($TransferRoot))
Set-EvidenceTestValue ArtifactPaths TestRoot $TransferRoot
Set-EvidenceTestValue EvidenceContract DeploymentRoot $DeploymentRoot
Set-EvidenceTestValue BuildContract OwnedRootsForTest @($OwnedRoots.Split('|'))
$fixed=[DateTimeOffset]::Parse($Now,[Globalization.CultureInfo]::InvariantCulture)
$clock={$fixed}.GetNewClosure()
Set-EvidenceTestValue BuildCleanup Clock $clock
Set-EvidenceTestValue BuildApplication Clock $clock
Set-EvidenceTestValue BuildCleanup KeepCount $KeepCount
$ready=$null;$go=$null
if($ReadyEvent){$ready=[Threading.EventWaitHandle]::OpenExisting($ReadyEvent);$go=[Threading.EventWaitHandle]::OpenExisting($GoEvent)}
$network={param($gen,$request)
    if($Barrier -ceq $request.Operation){$null=$ready.Set();if(-not $go.WaitOne(60000)){throw 'barrier-timeout'}}
    if($request.Operation -ceq 'delete' -and -not (Test-R2ArtifactRequestAllowed $request)){throw 'transport-rejected'}
    $path=[IO.Path]::Combine($RemoteRoot,[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($request.Key))).ToLowerInvariant()+'.bin')
    $status=200;$class='success';$code=$null;$bytes=$null;$sha=$null
    if($request.Operation -ceq 'delete'){
        if($NetworkMode -ceq 'forbidden'){$status=403;$class='forbidden';$code='AccessDenied'}else{[IO.File]::Delete($path);$status=204}
    }elseif($request.Operation -ceq 'get'){
        if([IO.File]::Exists($path)){$data=[IO.File]::ReadAllBytes($path);[IO.File]::WriteAllBytes($request.DestinationPath,$data);$bytes=[long]$data.Length;$sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant()}
        else{$status=404;$class='not-found';$code='NoSuchKey'}
    }else{throw 'offline-put-forbidden'}
    return [pscustomobject]@{Operation=$request.Operation;Generation=$gen;HttpStatus=$status;StatusClass=$class;S3Code=$code;Bytes=$bytes;Sha256=$sha;StatusObserved=$true;TimedOut=$false;Redirected=$false}
}.GetNewClosure()
Set-EvidenceTestValue BuildCleanup NetworkHook $network
Set-EvidenceTestValue BuildApplication NetworkHook $network
if($Barrier -ceq 'use-rename'){
    $hook={param($phase,$name)if($phase -ceq 'before-rename' -and $name.StartsWith('protections/',[StringComparison]::Ordinal)){$null=$ready.Set();if(-not $go.WaitOne(60000)){throw 'barrier-timeout'}}}.GetNewClosure()
    Set-EvidenceTestValue BuildStore FaultHook $hook
}
foreach($name in @('BuildCleanup','BuildApplication')){foreach($module in @(Get-EvidenceTestModules $name)){& $module {function script:Get-CredentialStatus {param($profile)[pscustomobject]@{Generation=('f'*32)}}}}}
$cli=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts.ps1'))
$arguments=@($CliArgs.Split('|'))
& $cli @arguments
if($null -eq $LASTEXITCODE -or $LASTEXITCODE -notin @(0,1,2)){exit 1}
exit ([int]$LASTEXITCODE)
