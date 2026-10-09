param(
    [Parameter(Mandatory=$true)][string]$Mode,
    [string]$Config,[string]$Selection,[string]$Reference,[string]$Sha256,
    [string]$StoreRoot,[string]$TransferRoot,[string]$DeploymentRoot,[string]$RemoteRoot,
    [string]$Key,[string]$Generation,[long]$ExpectedBytes,[string]$ExpectedSha256
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
function Get-RemotePath([string]$Remote,[string]$ObjectKey){
    if(-not [IO.Path]::IsPathFullyQualified($Remote) -or -not [IO.Directory]::Exists($Remote)){throw 'offline-root-unavailable'}
    $id=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($ObjectKey))).ToLowerInvariant()
    return [IO.Path]::Combine($Remote,$id+'.bin')
}
if($Mode -ceq 'readback'){
    try{
        $path=Get-RemotePath $RemoteRoot $Key
        $data=[IO.File]::ReadAllBytes($path)
        $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant()
        $passed=$data.Length -eq $ExpectedBytes -and $hash -ceq $ExpectedSha256
        [Console]::WriteLine((ConvertTo-Json -InputObject ([ordered]@{operation='readback';generation=$Generation;key=$Key;bytes=[long]$data.Length;sha256=$hash;childExit=0;httpStatus=200;statusClass='success';s3Code=$null;statusObserved=$true;timedOut=$false;redirected=$false;childStatus=if($passed){'passed'}else{'failed'};observedAt=[DateTimeOffset]::UtcNow.ToString('o')}) -Compress))
        if($passed){exit 0};exit 1
    }catch{exit 1}
}
if($Mode -cnotin @('publish','fetch','inspect')){exit 1}
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../BuildApplication.psm1')
Set-EvidenceTestValue BuildStore TestRoot $StoreRoot
Set-EvidenceTestValue ArtifactPaths TestRoot $TransferRoot
Set-EvidenceTestValue EvidenceContract DeploymentRoot $DeploymentRoot
Set-EvidenceTestValue BuildContract OwnedRootsForTest @($StoreRoot,$TransferRoot,$DeploymentRoot)
$offlineTransport={param($gen,$request)
    $path=Get-RemotePath $RemoteRoot $request.Key
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
$childPath=$PSCommandPath
$offlineReadback={param($op,$cfg,$gen,$objectKey,$bytes,$hash)
    $process=[Diagnostics.Process]::new();$process.StartInfo.FileName=(Get-Process -Id $PID).Path
    $process.StartInfo.UseShellExecute=$false;$process.StartInfo.CreateNoWindow=$true
    $process.StartInfo.RedirectStandardOutput=$true;$process.StartInfo.RedirectStandardError=$true
    foreach($a in @('-NoProfile','-File',$childPath,'-Mode','readback','-RemoteRoot',$RemoteRoot,'-Key',$objectKey,'-Generation',$gen,'-ExpectedBytes',[string]$bytes,'-ExpectedSha256',$hash)){$process.StartInfo.ArgumentList.Add($a)}
    try{
        $null=$process.Start();$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(-not $process.WaitForExit(15000)){$process.Kill($true);$null=$process.WaitForExit(5000);throw 'offline-readback-timeout'}
        if(-not $stdout.Wait(3000) -or -not $stderr.Wait(3000) -or $process.ExitCode -ne 0 -or $stderr.Result.Length -ne 0){throw 'offline-readback-unconfirmed'}
        $lines=@($stdout.Result.Trim() -split "`r?`n"|Where-Object {$_ -ne ''})
        if($lines.Count -ne 1){throw 'offline-readback-output'}
        return $lines[0]|ConvertFrom-Json -Depth 8 -DateKind String
    }finally{$process.Dispose()}
}.GetNewClosure()
Set-EvidenceTestValue BuildApplication NetworkHook $offlineTransport
Set-EvidenceTestValue BuildApplication ReadbackHook $offlineReadback
foreach($module in @(Get-EvidenceTestModules BuildApplication)){& $module {function script:Get-CredentialStatus {param($profile)[pscustomobject]@{Generation=('f'*32)}}}}
$cli=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts.ps1'))
$buildId=(Get-Content -LiteralPath $Selection -Raw|ConvertFrom-Json -Depth 8 -DateKind String).buildId
switch($Mode){
    'publish'{& $cli build publish --profile osm --config $Config --selection $Selection}
    'inspect'{& $cli build inspect --profile osm --config $Config --build-id $buildId}
    'fetch'{& $cli build fetch --profile osm --config $Config --reference $Reference --sha256 $Sha256}
}
if($null -eq $LASTEXITCODE -or $LASTEXITCODE -notin @(0,1)){exit 1}
exit ([int]$LASTEXITCODE)
