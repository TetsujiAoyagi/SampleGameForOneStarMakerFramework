Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildContract.psm1')
Import-Module (Join-Path $PSScriptRoot '../Workflow/WindowsStorePaths.psm1')
# このstoreのownerはBuild application。同user/同PCで成功を保持し、
# Workflow終了やEvidenceの30日清掃には登録しない。注入rootはoffline専用。
$script:TestRoot=$null
$script:FaultHook=$null

function Get-BuildRoot([bool]$Create=$false){
    if(-not [OperatingSystem]::IsWindows()){throw 'build-store-unavailable'}
    $local=[Environment]::GetFolderPath('LocalApplicationData')
    $root=if($script:TestRoot){[IO.Path]::GetFullPath($script:TestRoot)}else{[IO.Path]::Combine($local,'OneStarMaker','Artifacts','builds-v1')}
    Assert-NoArtifactReparse $root
    if($Create){
        $shared=[IO.Path]::GetDirectoryName($root)
        if(-not [IO.Directory]::Exists($shared)){throw 'build-store-unavailable'}
        # 共有親のACLは変更せず、このBuild領域と子だけを専用所有にする。
        if(-not [IO.Directory]::Exists($root)){[IO.Directory]::CreateDirectory($root)|Out-Null;Set-ArtifactAcl $root $true}
    }
    if([IO.Directory]::Exists($root)){
        $root=(Get-WindowsStorePathIdentity $root).physicalPath
        Assert-NoArtifactReparse $root;Assert-ArtifactAcl $root $true
    }
    return $root
}
# directoryの存在確認だけでは同時publishを排除できないため、repo直下の
# CreateNew予約を先に確保する。予約後に失敗してもIDを自動的に再利用させない。
# 再利用すると、応答不明だったremote objectへ同じkeyで再送する恐れがある。
function Get-BuildDirectory([string]$RepositoryId,[string]$BuildId,[bool]$Create=$false){
    Assert-Hex $RepositoryId 64;Assert-BuildId $BuildId
    $root=Get-BuildRoot $Create
    $repo=[IO.Path]::Combine($root,$RepositoryId);$dir=[IO.Path]::Combine($repo,$BuildId)
    Assert-NoArtifactReparse $dir
    if($Create){
        if(-not [IO.Directory]::Exists($repo)){[IO.Directory]::CreateDirectory($repo)|Out-Null;Set-ArtifactAcl $repo $true}
        else{Assert-ArtifactAcl $repo $true}
        if([IO.Directory]::Exists($dir) -or [IO.File]::Exists($dir)){throw 'already-exists'}
        # IDはCreateNew markerで予約する。競合processが同じdirectoryへ進む前に拒否する。
        $reservation=[IO.Path]::Combine($repo,$BuildId+'.reserved')
        try{$claim=[IO.FileStream]::new($reservation,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)}catch{throw 'already-exists'}
        try{$claim.Flush($true)}finally{$claim.Dispose()};Set-ArtifactAcl $reservation $false
        [IO.Directory]::CreateDirectory($dir)|Out-Null;Set-ArtifactAcl $dir $true
        $marker=[IO.Path]::Combine($dir,'reserved.lock')
        $stream=[IO.FileStream]::new($marker,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try{$stream.Flush($true)}finally{$stream.Dispose()};Set-ArtifactAcl $marker $false
    }elseif([IO.Directory]::Exists($dir)){
        Assert-ArtifactAcl $repo $true;Assert-ArtifactAcl $dir $true
    }
    return $dir
}
# intent/receiptは同directoryのtempへ全量write・flushし、存在しない最終名へ移す。
# receiptのrenameだけが成功commit点。既存正本の置換や別catalogとの二重確定はしない。
# 失敗tempは残し、ここで他の成功記録やremote objectをrollback削除しない。
function Write-BuildImmutable([string]$Directory,[string]$Name,$Value){
    if($Name -cnotin @('intent.json','receipt.json')){throw 'build-store-unavailable'}
    Assert-ArtifactAcl $Directory $true
    $final=[IO.Path]::Combine($Directory,$Name)
    if([IO.File]::Exists($final) -or [IO.Directory]::Exists($final)){throw 'already-exists'}
    $json=ConvertTo-Json -InputObject $Value -Compress -Depth 24
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes($json)
    if($bytes.Length -lt 1 -or $bytes.Length -gt 1MB){throw 'build-store-unavailable'}
    $temp=[IO.Path]::Combine($Directory,$Name+'.'+[Guid]::NewGuid().ToString('N')+'.tmp')
    $stream=$null
    try{
        if($script:FaultHook){& $script:FaultHook 'before-write' $Name}
        $stream=[IO.FileStream]::new($temp,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        Set-ArtifactAcl $temp $false
        if($script:FaultHook){& $script:FaultHook 'before-flush' $Name}
        $stream.Write($bytes,0,$bytes.Length);$stream.Flush($true);$stream.Dispose();$stream=$null
        Assert-ArtifactAcl $temp $false
        if($script:FaultHook){& $script:FaultHook 'before-rename' $Name}
        # rename後の例外は「確定していない」とは限らない。呼出側は不明応答を返し、
        # 次のinspectが正本の存在と整合を検査して成功を回復する。
        [IO.File]::Move($temp,$final)
        if($script:FaultHook){& $script:FaultHook 'after-rename' $Name}
        Assert-ArtifactAcl $final $false
        return @{Path=$final;Sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()}
    }finally{if($stream){$stream.Dispose()}}
}
# ファイルが存在するだけでは成功ではない。現在の設定・ID・系列・key・全entry、
# 読戻し観測とintentのhashまで照合し、部分書込や別記録の混入を拒否する。
# 読取から成功時刻を書き直したり、足りない記録を作成したりしない。
function Read-BuildReceipt($Config,[string]$BuildId){
    $dir=Get-BuildDirectory $Config.Data.repositoryId $BuildId
    $path=[IO.Path]::Combine($dir,'receipt.json')
    if(-not [IO.File]::Exists($path)){throw 'receipt-missing'}
    Assert-ArtifactAcl $path $false
    $item=Read-ArtifactJson $path;$v=$item.Data
    Assert-Fields $v @('schemaVersion','purpose','buildId','repositoryId','project','target','configuration','seriesId','key','configSha256','evidenceConfigSha256','packageBytes','packageSha256','manifestSha256','entries','intentSha256','readback','publishState','publishedAt')
    if($v.schemaVersion -isnot [long] -or $v.schemaVersion -ne 1 -or $v.purpose -cne 'build' -or
        $v.buildId -cne $BuildId -or $v.repositoryId -cne $Config.Data.repositoryId -or
        $v.configSha256 -cne $Config.Hash -or $v.evidenceConfigSha256 -cne $Config.EvidenceConfigSha256 -or
        $v.publishState -cne 'succeeded'){throw 'receipt-invalid'}
    foreach($name in @('buildId','repositoryId','project','target','configuration','seriesId','key','configSha256','evidenceConfigSha256','packageSha256','manifestSha256','intentSha256','publishState','publishedAt')){if($v.$name -isnot [string]){throw 'receipt-invalid'}}
    $series=Get-BuildSeries $v.repositoryId $v.project $v.target $v.configuration
    if($v.seriesId -cne $series -or $v.key -cne (Get-BuildKey $Config $v.project $v.target $v.configuration $BuildId) -or
        ($v.packageBytes -isnot [long] -and $v.packageBytes -isnot [int]) -or $v.packageBytes -lt 1 -or $v.packageBytes -gt 256MB){throw 'receipt-invalid'}
    foreach($hash in @($v.packageSha256,$v.manifestSha256,$v.intentSha256)){Assert-Hex $hash 64}
    $null=Assert-Utc $v.publishedAt
    if($v.entries -isnot [array] -or $v.entries.Count -lt 1 -or $v.entries.Count -gt 512){throw 'receipt-invalid'}
    $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $total=[long]0
    foreach($entry in $v.entries){
        Assert-Fields $entry @('path','bytes','sha256')
        $name=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Relative($entry.path)
        if(-not $names.Add($name) -or ($entry.bytes -isnot [long] -and $entry.bytes -isnot [int]) -or $entry.bytes -lt 0 -or $entry.bytes -gt 256MB){throw 'receipt-invalid'}
        Assert-Hex $entry.sha256 64;$total+=$entry.bytes
        if($total -gt 240MB){throw 'receipt-invalid'}
    }
    Assert-Fields $v.readback @('operation','generation','key','bytes','sha256','childExit','httpStatus','statusClass','s3Code','statusObserved','timedOut','redirected','childStatus','observedAt')
    if($v.readback.statusObserved -isnot [bool] -or $v.readback.timedOut -isnot [bool] -or $v.readback.redirected -isnot [bool] -or
        $v.readback.operation -cne 'readback' -or $v.readback.key -cne $v.key -or
        $v.readback.bytes -ne $v.packageBytes -or $v.readback.sha256 -cne $v.packageSha256 -or
        $v.readback.childExit -ne 0 -or $v.readback.childStatus -cne 'passed' -or
        $v.readback.httpStatus -ne 200 -or $v.readback.statusClass -cne 'success' -or
        -not $v.readback.statusObserved -or $v.readback.timedOut -or $v.readback.redirected){throw 'receipt-invalid'}
    Assert-Hex $v.readback.generation 32;$null=Assert-Utc $v.readback.observedAt
    # receipt内のintent hashは送信前に固定したbytesとの結び付きである。
    # hashだけでなく主要metadata/entry集合も同一であることを確認する。
    $intentPath=[IO.Path]::Combine($dir,'intent.json');Assert-ArtifactAcl $intentPath $false
    $intent=Read-ArtifactJson $intentPath
    if($intent.Sha256 -cne $v.intentSha256){throw 'receipt-invalid'}
    Assert-Fields $intent.Data @('schemaVersion','purpose','buildId','repositoryId','project','target','configuration','seriesId','key','configSha256','evidenceConfigSha256','packageBytes','packageSha256','manifestSha256','entries','operationPath')
    if($intent.Data.schemaVersion -isnot [long] -or $intent.Data.schemaVersion -ne 1 -or $intent.Data.purpose -cne 'build' -or
        $intent.Data.operationPath -isnot [string] -or -not [IO.Path]::IsPathFullyQualified($intent.Data.operationPath) -or
        $intent.Data.operationPath -cnotmatch '[\\/][0-9a-f]{32}$'){throw 'receipt-invalid'}
    foreach($field in @('buildId','repositoryId','project','target','configuration','seriesId','key','configSha256','evidenceConfigSha256','packageBytes','packageSha256','manifestSha256')){
        if($intent.Data.$field -cne $v.$field){throw 'receipt-invalid'}
    }
    if((ConvertTo-Json -InputObject $intent.Data.entries -Compress -Depth 8) -cne (ConvertTo-Json -InputObject $v.entries -Compress -Depth 8)){throw 'receipt-invalid'}
    return @{Path=$path;Data=$v;Sha256=$item.Sha256}
}
Export-ModuleMember -Function Get-BuildRoot,Get-BuildDirectory,Write-BuildImmutable,Read-BuildReceipt
