Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentContract.psm1')
Import-Module (Join-Path $PSScriptRoot '../Workflow/WindowsStorePaths.psm1')
# このstoreのownerはContent application。同user/同PCで成功を保持し、Workflow終了やEvidenceの30日清掃には
# 登録しない。Buildのbuilds-v1とは別rootで、同じ規則（専用ACL・reparse拒否）を別名で持つ。
# 注入rootとfault hookはoffline検証専用。公開CLIから差し替えさせない。
$script:TestRoot=$null
$script:FaultHook=$null

function Get-ContentRoot([bool]$Create=$false){
    if(-not [OperatingSystem]::IsWindows()){throw 'content-store-unavailable'}
    $local=[Environment]::GetFolderPath('LocalApplicationData')
    $root=if($script:TestRoot){[IO.Path]::GetFullPath($script:TestRoot)}else{[IO.Path]::Combine($local,'OneStarMaker','Artifacts','contents-v1')}
    Assert-NoArtifactReparse $root
    if($Create){
        # 共有親のACLは変更せず、このContent領域と子だけを専用所有にする。
        if(-not [IO.Directory]::Exists([IO.Path]::GetDirectoryName($root))){throw 'content-store-unavailable'}
        # 初回の同時作成では他processが先に作った直後のACL未設定を見うる。作成は冪等にし、ACLが無ければ付ける。
        [IO.Directory]::CreateDirectory($root)|Out-Null;try{Assert-ArtifactAcl $root $true}catch{Set-ArtifactAcl $root $true}
    }
    if([IO.Directory]::Exists($root)){
        $root=(Get-WindowsStorePathIdentity $root).physicalPath
        Assert-NoArtifactReparse $root;Assert-ArtifactAcl $root $true
    }
    return $root
}
# <root>/<repositoryId>/<contentSet>/<target>/<revision>/。各階層を専用ACLで作る。
# 予約markerは持たない。同時公開の排他は終了で解放されるlock（下）だけで行い、永続の予約は作らない。
function Get-ContentDirectory([string]$RepositoryId,[string]$ContentSet,[string]$TargetToken,[string]$Revision,[bool]$Create=$false){
    $null=Get-ContentSeries $RepositoryId $ContentSet $TargetToken;Assert-ContentRevision $Revision
    $path=Get-ContentRoot $Create
    foreach($part in @($RepositoryId,$ContentSet,$TargetToken,$Revision)){
        $path=[IO.Path]::Combine($path,$part);Assert-NoArtifactReparse $path
        if($Create){[IO.Directory]::CreateDirectory($path)|Out-Null;try{Assert-ArtifactAcl $path $true}catch{Set-ArtifactAcl $path $true}}
        elseif([IO.Directory]::Exists($path)){Assert-ArtifactAcl $path $true}
        else{throw 'receipt-missing'}
    }
    return $path
}
# FileShare.Noneで開いたhandleがlock。保持区間はintentの照合からreceiptの再読までで、processが
# 終われば自動で解放される。負けた側は待たずにpublish-in-progressで止まる。
function Enter-ContentPublishLock([string]$Directory){
    Assert-ArtifactAcl $Directory $true
    $path=[IO.Path]::Combine($Directory,'publish.lock')
    if(-not [IO.File]::Exists($path)){
        # 別processが同時に作った場合のCreateNew失敗は無視し、下の排他openで勝敗を決める。
        try{$c=[IO.FileStream]::new($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None);$c.Dispose();Set-ArtifactAcl $path $false}catch [IO.IOException]{}
    }
    try{$lock=[IO.FileStream]::new($path,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch [IO.IOException]{throw 'publish-in-progress'}
    try{try{Assert-ArtifactAcl $path $false}catch{Set-ArtifactAcl $path $false}}catch{$lock.Dispose();throw}
    return $lock
}
# intentは同じ入力なら同じbytesになる。既存と1 byteでも違えばintent-conflictで、書き換えない。
function Get-ContentIntentBytes($Intent){return [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $Intent -Compress -Depth 24))}
function Test-ContentIntent([string]$Directory,$Intent){
    $path=[IO.Path]::Combine($Directory,'intent.json')
    if(-not [IO.File]::Exists($path)){return $null}
    Assert-ArtifactAcl $path $false
    if([IO.FileInfo]::new($path).Length -gt 1MB){throw 'intent-conflict'}
    $have=[IO.File]::ReadAllBytes($path)
    $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($have)).ToLowerInvariant()
    if($hash -cne [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([byte[]](Get-ContentIntentBytes $Intent))).ToLowerInvariant()){throw 'intent-conflict'}
    return $hash
}
# 最初の送信前に固定する。既に同じintentがあれば書かずにそのhashを返す。
function Set-ContentIntent([string]$Directory,$Intent){
    $existing=Test-ContentIntent $Directory $Intent
    if($existing){return $existing}
    if($script:FaultHook){& $script:FaultHook 'before-intent' $Intent}
    return (Write-BuildImmutable $Directory 'intent.json' $Intent).Sha256
}
# receiptの存在だけでは成功ではない。intent・識別・hashを全て照合し、部分書込や別記録の混入を拒否する。
# 読取から成功時刻を書き直したり、足りない記録を作成したりしない。
function Read-ContentReceipt([string]$Directory,[string]$RepositoryId,[string]$ContentSet,[string]$TargetToken,[string]$Revision){
    $path=[IO.Path]::Combine($Directory,'receipt.json')
    if(-not [IO.File]::Exists($path)){throw 'receipt-missing'}
    Assert-ArtifactAcl $path $false
    $item=Read-ArtifactJson $path;$v=$item.Data
    Assert-Fields $v @('schemaVersion','purpose','repositoryId','seriesId','contentSet','target','revision','manifestSha256','objectCount','totalBytes','intentSha256','publishState','publishedAt')
    if($v.schemaVersion -isnot [long] -or $v.schemaVersion -ne 1 -or $v.purpose -cne 'content' -or $v.publishState -cne 'succeeded' -or
        $v.repositoryId -cne $RepositoryId -or $v.contentSet -cne $ContentSet -or $v.target -cne $TargetToken -or $v.revision -cne $Revision){throw 'receipt-invalid'}
    foreach($name in @('repositoryId','seriesId','contentSet','target','revision','manifestSha256','intentSha256','publishState','publishedAt')){if($v.$name -isnot [string]){throw 'receipt-invalid'}}
    if($v.seriesId -cne (Get-ContentSeries $RepositoryId $ContentSet $TargetToken)){throw 'receipt-invalid'}
    foreach($hash in @($v.manifestSha256,$v.intentSha256)){Assert-Hex $hash 64}
    $null=Assert-Utc $v.publishedAt
    foreach($name in @('objectCount','totalBytes')){if($v.$name -isnot [long] -and $v.$name -isnot [int]){throw 'receipt-invalid'}}
    if($v.objectCount -lt 2 -or $v.objectCount -gt 1024 -or $v.totalBytes -lt 1 -or $v.totalBytes -gt 512MB){throw 'receipt-invalid'}
    # receipt内のintent hashは送信前に固定したbytesとの結び付き。主要fieldも同一であることを確認する。
    $intentPath=[IO.Path]::Combine($Directory,'intent.json')
    if(-not [IO.File]::Exists($intentPath)){throw 'receipt-invalid'}
    Assert-ArtifactAcl $intentPath $false
    $intent=Read-ArtifactJson $intentPath
    if($intent.Sha256 -cne $v.intentSha256){throw 'receipt-invalid'}
    foreach($name in @('repositoryId','seriesId','contentSet','target','revision','manifestSha256','objectCount','totalBytes')){if($intent.Data.$name -cne $v.$name){throw 'receipt-invalid'}}
    return @{Path=$path;Data=$v;Sha256=$item.Sha256}
}
function Write-ContentReceipt([string]$Directory,$Receipt){
    if($script:FaultHook){& $script:FaultHook 'before-receipt' $Receipt}
    $null=Write-BuildImmutable $Directory 'receipt.json' $Receipt
    if($script:FaultHook){& $script:FaultHook 'after-receipt' $Receipt}
}
Export-ModuleMember -Function Get-ContentRoot,Get-ContentDirectory,Enter-ContentPublishLock,Test-ContentIntent,Set-ContentIntent,Read-ContentReceipt,Write-ContentReceipt
