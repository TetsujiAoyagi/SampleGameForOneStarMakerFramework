param(
    [string]$CliArgs='',[string]$WriteRecord='',[string]$Decision='',
    [string]$BuildRoot,[string]$ContentRoot,[string]$RetireRoot,[string]$TransferRoot,[string]$DeploymentRoot,[string]$RemoteRoot,[string]$OwnedRoots,
    [string]$Now,[int]$KeepCount=10,[string]$NetworkMode='normal',
    [string]$Barrier='',[string]$ReadyEvent='',[string]$GoEvent=''
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# 系列の退役の並行検査で使う子process。通信はofflineのremote directoryに限り、全rootを親のfixtureへ向けてから読む。
# CliArgs: 実CLI（tools/artifacts.ps1）を通す。Barrierに通信の操作名（delete／put／get）を渡すと、その送信の直前で
#   親へReadyを通知し、親のGoまで待つ（系列lock・publishの途中を親から観測するため）。
# WriteRecord: '<kind>:<token>,<token>…' の退役記録を Write-SeriesRetirement で直接書く（記録の書込み競合の検査）。
#   rename直前で親へReadyを通知し、親のGoまで待つ。結果の状態とdecisionをJSONで出す。
# 時間待機は使わない（待つのはsignalだけで、上限はhangの検出用）。
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../SeriesRetirementStore.psm1')
Set-EvidenceTestValue SeriesRetirementStore TestRoot $RetireRoot
$ready=$null;$go=$null
if($ReadyEvent){$ready=[Threading.EventWaitHandle]::OpenExisting($ReadyEvent);$go=[Threading.EventWaitHandle]::OpenExisting($GoEvent)}
$fixed=[DateTimeOffset]::Parse($Now,[Globalization.CultureInfo]::InvariantCulture)
if($WriteRecord){
    $hook={param($phase)if($phase -ceq 'before-rename'){$null=$ready.Set();if(-not $go.WaitOne(60000)){throw 'barrier-timeout'}}}.GetNewClosure()
    Set-EvidenceTestValue SeriesRetirementStore FaultHook $hook
    $kind,$tokens=$WriteRecord.Split(':')
    try{
        $s=Write-SeriesRetirement $kind ('a'*64) @($tokens.Split(',')) $Decision $fixed
        [Console]::WriteLine((ConvertTo-Json -InputObject ([ordered]@{state=$s.State;decision=$s.Record.decision;retiredAt=$s.Record.retiredAt}) -Compress))
        exit 0
    }catch{[Console]::WriteLine((ConvertTo-Json -InputObject ([ordered]@{state='failed';decision=$_.Exception.Message}) -Compress));exit 1}
}
Import-Module (Join-Path $PSScriptRoot '../BuildCleanup.psm1')
Import-Module (Join-Path $PSScriptRoot '../BuildApplication.psm1')
Import-Module (Join-Path $PSScriptRoot '../ContentCleanup.psm1')
Import-Module (Join-Path $PSScriptRoot '../Transport/R2ArtifactTransport.psm1')
Set-EvidenceTestValue BuildStore TestRoot $BuildRoot
Set-EvidenceTestValue ContentStore TestRoot $ContentRoot
Set-EvidenceTestValue ArtifactPaths TestRoot $TransferRoot
Set-EvidenceTestValue EvidenceContract DeploymentRoot $DeploymentRoot
Set-EvidenceTestValue BuildContract OwnedRootsForTest @($OwnedRoots.Split('|'))
$clock={$fixed}.GetNewClosure()
foreach($m in @('BuildCleanup','BuildApplication','ContentCleanup')){Set-EvidenceTestValue $m Clock $clock}
Set-EvidenceTestValue BuildCleanup KeepCount $KeepCount
Set-EvidenceTestValue ContentCleanup KeepCount $KeepCount
$path={param($key)[IO.Path]::Combine($RemoteRoot,[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($key))).ToLowerInvariant()+'.bin')}
$network={param($gen,$request)
    if($Barrier -ceq $request.Operation){$null=$ready.Set();if(-not $go.WaitOne(60000)){throw 'barrier-timeout'}}
    if(-not (Test-R2ArtifactRequestAllowed $request)){throw 'transport-rejected'}
    $p=& $path $request.Key;$status=200;$class='success';$code=$null;$bytes=$null;$sha=$null
    if($request.Operation -ceq 'delete'){
        if($NetworkMode -ceq 'forbidden'){$status=403;$class='forbidden';$code='AccessDenied'}else{[IO.File]::Delete($p);$status=204}
    }elseif($request.Operation -ceq 'get'){
        if([IO.File]::Exists($p)){$data=[IO.File]::ReadAllBytes($p);[IO.File]::WriteAllBytes($request.DestinationPath,$data);$bytes=[long]$data.Length;$sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant()}
        else{$status=404;$class='not-found';$code='NoSuchKey'}
    }else{[IO.File]::WriteAllBytes($p,[IO.File]::ReadAllBytes($request.SourcePath))}
    return [pscustomobject]@{Operation=$request.Operation;Generation=$gen;HttpStatus=$status;StatusClass=$class;S3Code=$code;Bytes=$bytes;Sha256=$sha;StatusObserved=$true;TimedOut=$false;Redirected=$false}
}.GetNewClosure()
$readback={param($op,$cfg,$gen,$key,$bytes,$hash)
    $data=[IO.File]::ReadAllBytes((& $path $key));$actual=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant()
    return [pscustomobject]@{operation='readback';generation=$gen;key=$key;bytes=[long]$data.Length;sha256=$actual;childExit=0;httpStatus=200;statusClass='success';s3Code=$null;statusObserved=$true;timedOut=$false;redirected=$false;childStatus='passed';observedAt=$fixed.ToString('o')}
}.GetNewClosure()
foreach($m in @('BuildCleanup','BuildApplication','ContentCleanup')){Set-EvidenceTestValue $m NetworkHook $network}
Set-EvidenceTestValue BuildApplication ReadbackHook $readback
foreach($name in @('BuildCleanup','BuildApplication','ContentCleanup')){foreach($module in @(Get-EvidenceTestModules $name)){& $module {function script:Get-CredentialStatus {param($profile)[pscustomobject]@{Generation=('f'*32)}}}}}
$cli=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts.ps1'))
$arguments=@($CliArgs.Split('|'))
& $cli @arguments
if($null -eq $LASTEXITCODE -or $LASTEXITCODE -notin @(0,1,2)){exit 1}
exit ([int]$LASTEXITCODE)
