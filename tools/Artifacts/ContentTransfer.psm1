Set-StrictMode -Version Latest
# fresh processのCLIでも同じ関数解決になるよう、使用するmoduleを直接importする。
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactApplication.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentContract.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentStage.psm1')
Import-Module (Join-Path $PSScriptRoot 'Credentials/CredentialStore.psm1')
# object単位の転送の所有者。呼出し単位で状態を持たず、operationの所有者はContentApplication。
# 通信とreadbackの注入点はArtifactApplication側の既存hookを使う。ここの注入点は経過時間を返す時計だけで、
# offline検証専用。公開CLIから差し替えさせない。
$script:Clock={[long]([Diagnostics.Stopwatch]::GetTimestamp()*1000.0/[Diagnostics.Stopwatch]::Frequency)}
$script:ActiveBudget=$null

# 公開 300+8×N 秒、取得 300+2×N 秒（Nはobject数）。1 objectの照合は子process 2回で約3.6 sかかり、
# 式はSpring実測見積もりの約2.6倍の余裕を持つ。
function Get-ContentBudgetMs([string]$Kind,[int]$ObjectCount){
    if($Kind -cnotin @('publish','fetch') -or $ObjectCount -lt 1){throw 'invalid-content-budget'}
    return [long]((300+$(if($Kind -ceq 'publish'){8}else{2})*$ObjectCount)*1000)
}
# 予算はArtifactApplicationのmodule scopeの$script:Budgetへ同じ形（Remaining/HardRemaining）で設定し、
# object単位のdeadlineと実行中の読戻しにも効かせる。Exit-ContentBudgetで必ず復元する（try/finallyで対にする）。
# Evidenceの関数には依存しない。
function Enter-ContentBudget([long]$LimitMs){
    $clock=$script:Clock;$start=[long](& $clock)
    $left={[Math]::Max(0,$LimitMs-([long](& $clock)-$start))}.GetNewClosure()
    $budget=[pscustomobject]@{Remaining=$left;HardRemaining=$left}
    $owner=(Get-Command Invoke-ArtifactNetwork -ErrorAction Stop).Module
    $scope=[pscustomobject]@{Owner=$owner;Previous=(& $owner {$script:Budget});PreviousActive=$script:ActiveBudget}
    & $owner {param($b)$script:Budget=$b} $budget;$script:ActiveBudget=$budget
    return $scope
}
function Exit-ContentBudget($Scope){
    if($null -eq $Scope){return}
    & $Scope.Owner {param($b)$script:Budget=$b} $Scope.Previous;$script:ActiveBudget=$Scope.PreviousActive
}
function Assert-ContentBudget{if($script:ActiveBudget -and (& $script:ActiveBudget.Remaining) -le 0){throw 'budget-exceeded'}}
# ArtifactApplicationの例外文面（pathや内部情報を含みうる）は、予算切れだけを区別し、残りは
# 呼出しごとの固定語へ丸める。公開reasonに例外本文を出さない。
function Invoke-ContentGuarded([scriptblock]$Action,[string]$Failure){
    try{return & $Action}catch{
        $m=$_.Exception.Message
        if($m -cin @('evidence-deadline','Artifact operation expired.')){throw 'budget-exceeded'}
        throw $Failure
    }
}
function Get-ContentField($Object,[string]$Name){
    $p=if($null -ne $Object){$Object.PSObject.Properties[$Name]}else{$null}
    if($p){return $p.Value};return $null
}
# 読戻しは1 objectごとの子directory（32hex名+operation.lock）で行う。既存のoperationの前提を満たすので
# Invoke-ArtifactReadbackは変えず、Protect-ArtifactTreeの走査をobject単位に限れる（二乗コストを避ける）。
function New-ContentObjectDirectory([string]$Operation){
    $dir=[IO.Path]::Combine($Operation,[Guid]::NewGuid().ToString('N'))
    Assert-NoArtifactReparse $dir
    if([IO.Directory]::Exists($dir) -or [IO.File]::Exists($dir)){throw 'content-store-unavailable'}
    [IO.Directory]::CreateDirectory($dir)|Out-Null;Set-ArtifactAcl $dir $true
    $marker=[IO.Path]::Combine($dir,'operation.lock')
    $stream=[IO.FileStream]::new($marker,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{$stream.Flush($true)}finally{$stream.Dispose()};Set-ArtifactAcl $marker $false
    return $dir
}
# 子processの観測を match/missing/mismatch/unconfirmed に分ける。404は「送ってよい」手掛かりで、
# 確定ではない（送った後は必ず別processで読み戻す）。403・timeout・応答不明は全てunconfirmedで止める。
function Get-ContentRemoteState($Read,[string]$Generation,[string]$Key,[long]$Bytes,[string]$Hash){
    if(Test-ArtifactReadback $Read $Generation $Key $Bytes $Hash){return 'match'}
    if($null -eq $Read -or (Get-ContentField $Read 'operation') -cne 'readback' -or (Get-ContentField $Read 'generation') -cne $Generation -or (Get-ContentField $Read 'key') -cne $Key){return 'unconfirmed'}
    if((Get-ContentField $Read 'statusObserved') -ne $true -or (Get-ContentField $Read 'timedOut') -ne $false -or (Get-ContentField $Read 'redirected') -ne $false -or (Get-ContentField $Read 'childStatus') -cne 'failed'){return 'unconfirmed'}
    $http=Get-ContentField $Read 'httpStatus';$class=Get-ContentField $Read 'statusClass';$seenBytes=Get-ContentField $Read 'bytes';$seenSha=Get-ContentField $Read 'sha256'
    if($http -eq 404 -and $class -ceq 'not-found' -and (Get-ContentField $Read 's3Code') -ceq 'NoSuchKey' -and $null -eq $seenBytes -and $null -eq $seenSha){return 'missing'}
    if($http -eq 200 -and $class -ceq 'success' -and $null -ne $seenBytes -and $null -ne $seenSha){return 'mismatch'}
    return 'unconfirmed'
}
function Test-ContentRemoteObject($Ctx,[string]$Key,[long]$Bytes,[string]$Hash){
    Assert-ContentBudget
    $dir=New-ContentObjectDirectory $Ctx.Operation;$timer=[Diagnostics.Stopwatch]::StartNew();$Ctx.State.Gets++
    $read=Invoke-ContentGuarded {Invoke-ArtifactReadback $dir $Ctx.Config $Ctx.Generation $Key $Bytes $Hash $timer} 'object-unconfirmed'
    return Get-ContentRemoteState $read $Ctx.Generation $Key $Bytes $Hash
}
# 資格情報のgenerationはrun開始時に固定する。途中でrotationされたら以降の通信が失敗して止まる。
function Get-ContentGeneration{return (Get-CredentialStatus 'osm').Generation}
function New-ContentTransferContext($Config,[string]$Operation,[string]$Snapshot,$Manifest){
    $state=@{Remote='pre-put';Gets=0;Puts=0;Sent=0;Verified=0}
    return [pscustomobject]@{Config=$Config;Operation=$Operation;Snapshot=$Snapshot;Manifest=$Manifest;Generation=(Get-ContentGeneration);State=$state}
}
# 公開の最初に遠隔のtransport.jsonを子processで照合する。存在してhashが違えば何も送らずに止まる。
# 他のwriterが既にこのrevisionを別内容で置いている（上書きしてはいけない）状態を、送信前に検出する。
function Confirm-ContentRemoteManifest($Ctx){
    $m=$Ctx.Manifest;$key=Get-ContentKey $Ctx.Config $m.ContentSet $m.TargetToken $m.Revision
    $state=Test-ContentRemoteObject $Ctx $key $m.ManifestBytes $m.Sha256
    if($state -ceq 'mismatch'){throw 'revision-conflict'}
    if($state -cnotin @('match','missing')){throw 'object-unconfirmed'}
    if($state -ceq 'match'){$Ctx.State.Verified++}
    return $state
}
# 1 objectの経路: 照合→（404なら）直前の再照合とPUT→子processの読戻し。初回か続行かで経路を変えない。
# PUTの応答が不明になったkeyはこのrunで再送せず、例外でrun全体を止める（次のrunの照合から再開する）。
function Publish-ContentObject($Ctx,[string]$Key,$Entry,[string]$Known=''){
    $state=if($Known){$Known}else{Test-ContentRemoteObject $Ctx $Key $Entry.bytes $Entry.sha256}
    if($state -ceq 'match'){$Ctx.State.Verified++;return}
    if($state -ceq 'mismatch'){throw 'object-conflict'}
    if($state -cne 'missing'){throw 'object-unconfirmed'}
    Assert-ContentSnapshotFile $Ctx.Snapshot $Entry
    Assert-ContentBudget
    # 呼出し前から存在不明として扱う。応答が失われても、再送やrollback DELETEをしない。
    $Ctx.State.Remote='may-have-been-sent';$Ctx.State.Puts++;$timer=[Diagnostics.Stopwatch]::StartNew()
    $source=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($Ctx.Snapshot,$Entry.path)
    $put=Invoke-ContentGuarded {Invoke-ArtifactNetwork $Ctx.Generation (New-ArtifactRequest $Ctx.Config 'put' $Key $source $null $timer)} 'put-unconfirmed'
    if(-not (Test-ArtifactSuccess $put 'put' $Ctx.Generation)){throw 'put-unconfirmed'}
    if((Test-ContentRemoteObject $Ctx $Key $Entry.bytes $Entry.sha256) -cne 'match'){throw 'readback-unconfirmed'}
    $Ctx.State.Sent++
}
# content/*をmanifestの順に置き、transport.jsonは同じrunで全content/*の照合が通った後にだけ置く。
# $ManifestStateは最初の照合結果で、matchなら再送せず、missingなら二重のGETをせず直接置く。
function Publish-ContentObjects($Ctx,[string]$ManifestState){
    $m=$Ctx.Manifest
    foreach($f in $m.Files){
        Assert-ContentBudget
        Publish-ContentObject $Ctx (Get-ContentKey $Ctx.Config $m.ContentSet $m.TargetToken $m.Revision $f.path) ([pscustomobject]@{path="content/$($f.path)";bytes=$f.bytes;sha256=$f.sha256})
    }
    Assert-ContentBudget
    if($ManifestState -ceq 'missing'){
        Publish-ContentObject $Ctx (Get-ContentKey $Ctx.Config $m.ContentSet $m.TargetToken $m.Revision) ([pscustomobject]@{path='transport.json';bytes=$m.ManifestBytes;sha256=$m.Sha256}) 'missing'
    }
}
# 取得は1 objectずつGETし、transportの観測値だけでなく書かれたfileを自分で再hashする。
# ExpectedBytes/ExpectedHashを省くとtransport.jsonのように呼出側が突合する（hashは独立に渡された値と比べる）。
function Receive-ContentObject($Config,[string]$Generation,[string]$Key,[string]$Destination,[long]$ExpectedBytes=-1,[string]$ExpectedHash=''){
    Assert-ContentBudget
    if([IO.File]::Exists($Destination) -or [IO.Directory]::Exists($Destination)){throw 'download-unconfirmed'}
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Destination))|Out-Null
    $timer=[Diagnostics.Stopwatch]::StartNew()
    $r=Invoke-ContentGuarded {Invoke-ArtifactNetwork $Generation (New-ArtifactRequest $Config 'get' $Key $null $Destination $timer)} 'download-unconfirmed'
    if(-not (Test-ArtifactSuccess $r 'get' $Generation) -or -not [IO.File]::Exists($Destination)){throw 'download-unconfirmed'}
    $stream=[IO.FileStream]::new($Destination,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    try{$bytes=$stream.Length;$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()}finally{$stream.Dispose()}
    $seenBytes=Get-ContentField $r 'Bytes';$seenHash=Get-ContentField $r 'Sha256'
    if($bytes -lt 1 -or ($null -ne $seenBytes -and $seenBytes -ne $bytes) -or ($seenHash -and $seenHash -cne $hash)){throw 'download-unconfirmed'}
    if(($ExpectedBytes -ge 0 -and $bytes -ne $ExpectedBytes) -or ($ExpectedHash -and $hash -cne $ExpectedHash)){throw 'download-unconfirmed'}
    return [pscustomobject]@{Bytes=[long]$bytes;Sha256=$hash}
}
Export-ModuleMember -Function Assert-ContentBudget,Get-ContentGeneration,Get-ContentBudgetMs,Enter-ContentBudget,Exit-ContentBudget,New-ContentTransferContext,Confirm-ContentRemoteManifest,Publish-ContentObjects,Receive-ContentObject
