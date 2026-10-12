Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactDeletion.psm1')
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
# 件数清掃の削除試行（deletion/）と利用中保護（protections/）も同じ不変書込みに載せる。
# 許可名を1箇所に置くのは、ACL検査と既存名の非置換を記録の種類ごとに二重に持たないため。
# 許可名の表は記録の種別（Kind: build／content）ごとに分ける。Contentの削除試行だけが確定点（committed）
# を持ち、Buildの集合へ混ぜるとBuildにも書けてしまう。Buildの許可集合はContentの追加で変えない。
# BeforeCommitはrenameの直前に呼ぶ。呼出側の予算判定等が例外なら確定させない。
function Write-BuildImmutable([string]$Directory,[string]$Name,$Value,[scriptblock]$BeforeCommit=$null,[string]$Kind='build'){
    $allowed=switch -CaseSensitive ($Kind){
        'build'{$Name -cin @('intent.json','receipt.json') -or
            $Name -cmatch '\Adeletion/[1-9][0-9]{0,5}\.(?:intent|sent|deleted|abandoned)\.json\z' -or
            $Name -cmatch '\Aprotections/[0-9a-f]{32}\.(?:use|release)\.json\z'}
        'content'{$Name -cin @('intent.json','receipt.json') -or
            $Name -cmatch '\Adeletion/[1-9][0-9]{0,5}\.(?:intent|sent|committed|abandoned|deleted)\.json\z'}
        default{$false}
    }
    if(-not $allowed){throw 'build-store-unavailable'}
    Assert-ArtifactAcl $Directory $true
    $parent=$Directory;$leaf=$Name
    if($Name.Contains('/')){
        $parent=[IO.Path]::Combine($Directory,$Name.Split('/')[0]);$leaf=$Name.Split('/')[1]
        Assert-NoArtifactReparse $parent
        if([IO.File]::Exists($parent)){throw 'build-store-unavailable'}
        if(-not [IO.Directory]::Exists($parent)){[IO.Directory]::CreateDirectory($parent)|Out-Null;Set-ArtifactAcl $parent $true}else{Assert-ArtifactAcl $parent $true}
    }
    $final=[IO.Path]::Combine($parent,$leaf)
    if([IO.File]::Exists($final) -or [IO.Directory]::Exists($final)){throw 'already-exists'}
    $json=ConvertTo-Json -InputObject $Value -Compress -Depth 24
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes($json)
    if($bytes.Length -lt 1 -or $bytes.Length -gt 1MB){throw 'build-store-unavailable'}
    $temp=[IO.Path]::Combine($parent,$leaf+'.'+[Guid]::NewGuid().ToString('N')+'.tmp')
    $stream=$null
    try{
        if($script:FaultHook){& $script:FaultHook 'before-write' $Name}
        $stream=[IO.FileStream]::new($temp,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        Set-ArtifactAcl $temp $false
        if($script:FaultHook){& $script:FaultHook 'before-flush' $Name}
        $stream.Write($bytes,0,$bytes.Length);$stream.Flush($true);$stream.Dispose();$stream=$null
        Assert-ArtifactAcl $temp $false
        if($script:FaultHook){& $script:FaultHook 'before-rename' $Name}
        if($BeforeCommit){& $BeforeCommit}
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
# 検査の本体はここ1箇所。MatchConfig=$falseは件数清掃・保護用の読取で、今のbuild-config
# とのhash照合2項目だけを外す（configのbytesが変わっても過去の成功を数え損ねないため）。
# 両hashが64 hexであることは同じ位置で検査する。
function Read-BuildSuccessCore($Config,[string]$BuildId,[bool]$MatchConfig){
    $dir=Get-BuildDirectory $Config.Data.repositoryId $BuildId
    $path=[IO.Path]::Combine($dir,'receipt.json')
    if(-not [IO.File]::Exists($path)){throw 'receipt-missing'}
    Assert-ArtifactAcl $path $false
    $item=Read-ArtifactJson $path;$v=$item.Data
    Assert-Fields $v @('schemaVersion','purpose','buildId','repositoryId','project','target','configuration','seriesId','key','configSha256','evidenceConfigSha256','packageBytes','packageSha256','manifestSha256','entries','intentSha256','readback','publishState','publishedAt')
    if($v.schemaVersion -isnot [long] -or $v.schemaVersion -ne 1 -or $v.purpose -cne 'build' -or
        $v.buildId -cne $BuildId -or $v.repositoryId -cne $Config.Data.repositoryId -or
        ($MatchConfig -and ($v.configSha256 -cne $Config.Hash -or $v.evidenceConfigSha256 -cne $Config.EvidenceConfigSha256)) -or
        (-not $MatchConfig -and ($v.configSha256 -isnot [string] -or $v.configSha256 -cnotmatch '\A[0-9a-f]{64}\z' -or
            $v.evidenceConfigSha256 -isnot [string] -or $v.evidenceConfigSha256 -cnotmatch '\A[0-9a-f]{64}\z')) -or
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
function Read-BuildReceipt($Config,[string]$BuildId){return Read-BuildSuccessCore $Config $BuildId $true}
function Read-BuildSuccessRecord($Config,[string]$BuildId){return Read-BuildSuccessCore $Config $BuildId $false}
# 成功検査に落ちた記録の所属を判定するためだけの読取。所属5 fieldが揃い、
# 自身の系列規則と整合する場合だけ返す。成功の根拠には使わない。
function Read-BuildRecordIdentity([string]$RepositoryId,[string]$BuildId,[string]$Name){
    if($Name -cnotin @('intent.json','receipt.json')){throw 'build-store-unavailable'}
    $path=[IO.Path]::Combine((Get-BuildDirectory $RepositoryId $BuildId),$Name)
    if(-not [IO.File]::Exists($path)){throw 'record-missing'}
    Assert-ArtifactAcl $path $false
    $v=(Read-ArtifactJson $path).Data;$r=[ordered]@{}
    if($null -eq $v -or $v.GetType() -ne [System.Management.Automation.PSCustomObject]){throw 'record-invalid'}
    foreach($n in @('buildId','seriesId','project','target','configuration')){$p=$v.PSObject.Properties[$n];if($null -eq $p -or $p.Value -isnot [string]){throw 'record-invalid'};$r[$n]=$p.Value}
    if($r.buildId -cne $BuildId -or $r.seriesId -cne (Get-BuildSeries $RepositoryId $r.project $r.target $r.configuration)){throw 'record-invalid'}
    return [pscustomobject]$r
}
# --- 件数清掃の削除試行（deletion/<n>.(intent|sent|deleted|abandoned).json） ---
# 観測はTest-ArtifactSuccess/Test-ArtifactMissingを記録だけから再評価できる非秘密の組で、
# endpoint・応答本文・例外本文は持たない。ここでは形と記録同士の結び付きを検査し、削除済み・取下げの
# 根拠となる観測の検査はContentと共通の Assert-ArtifactObservation（ArtifactDeletion）に任せる。
function Assert-BuildDeletionHeader($v,[string[]]$Fields,[int]$Attempt,[string]$IntentSha256){
    Assert-Fields $v $Fields
    if($v.schemaVersion -isnot [long] -or $v.schemaVersion -ne 1 -or $v.attempt -isnot [long] -or $v.attempt -ne $Attempt -or
        ($IntentSha256 -and $v.intentSha256 -cne $IntentSha256)){throw 'delete-record-invalid'}
}
# 状態はdeletion/の記録だけで決まり、build-configにも成功receiptにも依存しない
# （旧configでpublishしたBuildでも、inspect/collectが削除状態を先に判定できるように）。
# 試行nは1からの連番。未解決・削除済みになれるのは最後の試行だけで、それより前は全て取下げ済み。
# 規則に合わない記録は補わず、削除状態を判定できないものとして例外にする。
function Read-BuildDeletionState([string]$RepositoryId,[string]$BuildId){
    $del=[IO.Path]::Combine((Get-BuildDirectory $RepositoryId $BuildId),'deletion')
    $state=[pscustomobject]@{State='none';Next=1;Key=$null;PackageSha256=$null;Pending=$null}
    try{
        Assert-NoArtifactReparse $del
        if([IO.File]::Exists($del)){throw 'delete-record-invalid'}
        if(-not [IO.Directory]::Exists($del)){return $state}
        Assert-ArtifactAcl $del $true
        if(@([IO.Directory]::EnumerateDirectories($del)).Count){throw 'delete-record-invalid'}
        $records=@{};$max=0
        foreach($file in [IO.Directory]::EnumerateFiles($del)){
            $name=[IO.Path]::GetFileName($file)
            # rename前に止まった書込みのtempは記録ではない（何も確定していない）。
            if($name -cmatch '\A[1-9][0-9]{0,5}\.(?:intent|sent|deleted|abandoned)\.json\.[0-9a-f]{32}\.tmp\z'){continue}
            $m=[regex]::Match($name,'\A([1-9][0-9]{0,5})\.(intent|sent|deleted|abandoned)\.json\z')
            if(-not $m.Success){throw 'delete-record-invalid'}
            Assert-ArtifactAcl $file $false
            $n=[int]$m.Groups[1].Value;if(-not $records.ContainsKey($n)){$records[$n]=@{}}
            $records[$n][$m.Groups[2].Value]=Read-ArtifactJson $file;$max=[Math]::Max($max,$n)
        }
        for($n=1;$n -le $max;$n++){
            if(-not $records.ContainsKey($n) -or -not $records[$n].ContainsKey('intent')){throw 'delete-record-invalid'}
            $r=$records[$n];$i=$r.intent.Data
            Assert-BuildDeletionHeader $i @('schemaVersion','attempt','buildId','key','packageSha256','intentAt') $n $null
            if($i.buildId -cne $BuildId -or $i.key -isnot [string] -or $i.intentAt -isnot [string] -or
                -not $i.key.StartsWith("development/builds/v1/$RepositoryId/",[StringComparison]::Ordinal) -or
                -not $i.key.EndsWith("/$BuildId/bundle.zip",[StringComparison]::Ordinal)){throw 'delete-record-invalid'}
            Assert-Hex $i.packageSha256 64;$null=Assert-Utc $i.intentAt
            if($n -eq 1){$state.Key=$i.key;$state.PackageSha256=$i.packageSha256}
            elseif($i.key -cne $state.Key -or $i.packageSha256 -cne $state.PackageSha256){throw 'delete-record-invalid'}
            $sent=$null
            if($r.ContainsKey('sent')){
                Assert-BuildDeletionHeader $r.sent.Data @('schemaVersion','attempt','intentSha256','observation') $n $r.intent.Sha256
                Assert-ArtifactObservation $r.sent.Data.observation 'delete' $i.key;$sent=$r.sent.Sha256
            }
            if($r.ContainsKey('deleted') -and $r.ContainsKey('abandoned')){throw 'delete-record-invalid'}
            if($r.ContainsKey('deleted')){
                $v=$r.deleted.Data
                Assert-BuildDeletionHeader $v @('schemaVersion','attempt','intentSha256','sentSha256','deleteObservation','getObservation') $n $r.intent.Sha256
                if(($sent -and ($v.deleteObservation -cne 'recorded' -or $v.sentSha256 -cne $sent)) -or
                    (-not $sent -and ($v.deleteObservation -cne 'unknown' -or $null -ne $v.sentSha256))){throw 'delete-record-invalid'}
                Assert-ArtifactObservation $v.getObservation 'get' $i.key 'missing'
            }
            if($r.ContainsKey('abandoned')){
                Assert-BuildDeletionHeader $r.abandoned.Data @('schemaVersion','attempt','intentSha256','getObservation') $n $r.intent.Sha256
                Assert-ArtifactObservation $r.abandoned.Data.getObservation 'get' $i.key 'present'
            }
            if($n -lt $max -and -not $r.ContainsKey('abandoned')){throw 'delete-record-invalid'}
            if($n -eq $max -and $r.ContainsKey('deleted')){$state.State='deleted'}
            elseif($n -eq $max -and -not $r.ContainsKey('abandoned')){$state.State='deleting';$state.Pending=[pscustomobject]@{Attempt=$n;IntentSha256=$r.intent.Sha256;SentSha256=$sent}}
        }
        $state.Next=$max+1
    }catch{throw 'delete-record-invalid'}
    return $state
}
# --- 利用中保護（protections/<id>.use.json と .release.json） ---
# 追記だけの記録で、解除は.release.jsonを足す。値の形と期限の検査はBuildProtectionが持ち、
# ここは名前・ACL・useとreleaseの対応だけを見る。
function Read-BuildProtectionRecords([string]$RepositoryId,[string]$BuildId){
    $root=[IO.Path]::Combine((Get-BuildDirectory $RepositoryId $BuildId),'protections')
    try{
        Assert-NoArtifactReparse $root
        if([IO.File]::Exists($root)){throw 'protection-unknown'}
        if(-not [IO.Directory]::Exists($root)){return @()}
        Assert-ArtifactAcl $root $true
        if(@([IO.Directory]::EnumerateDirectories($root)).Count){throw 'protection-unknown'}
        $uses=@{};$releases=@{}
        foreach($file in [IO.Directory]::EnumerateFiles($root)){
            $name=[IO.Path]::GetFileName($file)
            if($name -cmatch '\A[0-9a-f]{32}\.(?:use|release)\.json\.[0-9a-f]{32}\.tmp\z'){continue}
            $m=[regex]::Match($name,'\A([0-9a-f]{32})\.(use|release)\.json\z')
            if(-not $m.Success){throw 'protection-unknown'}
            Assert-ArtifactAcl $file $false
            $data=(Read-ArtifactJson $file).Data
            if($m.Groups[2].Value -ceq 'use'){$uses[$m.Groups[1].Value]=$data}else{$releases[$m.Groups[1].Value]=$data}
        }
        foreach($id in $releases.Keys){if(-not $uses.ContainsKey($id)){throw 'protection-unknown'}}
        return @($uses.Keys|Sort-Object -CaseSensitive|ForEach-Object {[pscustomobject]@{Id=$_;Use=$uses[$_];Release=$releases[$_]}})
    }catch{throw 'protection-unknown'}
}
# --- 排他 ---
# lockはFileShareを指定して開いたhandleそのもの（.NETのFileStreamは子processへ継承されない）。
# process終了でOSが解放するので、異常終了しても永続の予約は残らない。
# 取れないときは待たずに'lock-busy'を投げ、呼出側が競合の理由語へ写す。
function Test-BuildSharingViolation($Exception){
    $e=$Exception;while($e -and $e -isnot [IO.IOException] -and $e.InnerException){$e=$e.InnerException}
    return $e -is [IO.IOException] -and ($e.HResult -band 0xFFFF) -in @(32,33)
}
function Open-BuildLockFile([string]$Path,[IO.FileShare]$Share){
    Assert-NoArtifactReparse $Path
    if(-not [IO.File]::Exists($Path)){
        # 作成競合に負けた側は既存fileを開く。作成者がACLを付け終える前に開いた場合は
        # 下のACL検査で失敗させ、推測で進まない。
        $created=$false
        try{$s=[IO.FileStream]::new($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None);$s.Dispose();$created=$true}catch{if(-not [IO.File]::Exists($Path)){throw}}
        if($created){Set-ArtifactAcl $Path $false}
    }
    try{$handle=[IO.FileStream]::new($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,$Share)}catch{if(Test-BuildSharingViolation $_.Exception){throw 'lock-busy'};throw}
    try{Assert-ArtifactAcl $Path $false}catch{$handle.Dispose();throw}
    return $handle
}
# 系列lockはcleanupとuseが取る。系列ごとに独立して進められるよう、repo全体では取らない。
function Enter-BuildSeriesLock([string]$RepositoryId,[string]$Project,[string]$Target,[string]$Configuration){
    $null=Get-BuildSeries $RepositoryId $Project $Target $Configuration
    $repo=[IO.Path]::Combine((Get-BuildRoot),$RepositoryId);Assert-NoArtifactReparse $repo
    if(-not [IO.Directory]::Exists($repo)){throw 'build-store-unavailable'}
    Assert-ArtifactAcl $repo $true
    $locks=[IO.Path]::Combine($repo,'locks');Assert-NoArtifactReparse $locks
    if(-not [IO.Directory]::Exists($locks)){[IO.Directory]::CreateDirectory($locks)|Out-Null;Set-ArtifactAcl $locks $true}else{Assert-ArtifactAcl $locks $true}
    return Open-BuildLockFile ([IO.Path]::Combine($locks,"$Project~$Target~$Configuration.lock")) ([IO.FileShare]::None)
}
# Build単位のaccess lock。fetch/inspect/collectは共有（ReadWrite）、cleanupは削除意図の
# 書込みから試行の終了まで排他（None）で持つ。作成時のreserved.lockは予約の意味なので流用せず、
# 既存Buildにも後から作れるよう「無ければCreateNew、あればOpen」で扱う。
function Enter-BuildAccessLock([string]$RepositoryId,[string]$BuildId,[bool]$Exclusive){
    $dir=Get-BuildDirectory $RepositoryId $BuildId
    if(-not [IO.Directory]::Exists($dir)){throw 'build-store-unavailable'}
    $share=if($Exclusive){[IO.FileShare]::None}else{[IO.FileShare]::ReadWrite}
    return Open-BuildLockFile ([IO.Path]::Combine($dir,'access.lock')) $share
}
Export-ModuleMember -Function Get-BuildRoot,Get-BuildDirectory,Write-BuildImmutable,Read-BuildReceipt,Read-BuildSuccessRecord,Read-BuildRecordIdentity,Read-BuildDeletionState,Read-BuildProtectionRecords,Enter-BuildSeriesLock,Enter-BuildAccessLock
