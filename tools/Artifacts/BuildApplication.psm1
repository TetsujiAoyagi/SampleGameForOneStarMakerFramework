Set-StrictMode -Version Latest
# fresh processのCLIでも同じ関数解決になるよう、使用するmoduleを直接importする。
# 親のテストprocessが先に読み込んだmoduleへ暗黙に依存しない。
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactApplication.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildContract.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildStage.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildProtection.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'SeriesRetirementStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'Credentials/CredentialStore.psm1')
# 注入点はoffline検証専用。公開CLIから時計・通信・上限・失敗境界を差し替えさせない。
# 通常操作では既存の資格情報generation固定と、期限付きのTransport/読戻しを使う。
$script:Clock={ [DateTimeOffset]::UtcNow }
$script:FaultHook=$null
$script:NetworkHook=$null
$script:ReadbackHook=$null
$script:FailureHook=$null
$script:SnapshotLimit=240MB
$script:PackageCreatedHook=$null

# filesは部分的なinclude指定ではない。未列挙fileを黙って追加・除外すると、
# 呼出側が選んだ1件と保存されたBuildの範囲がずれるため、集合全体を比較する。
function Assert-BuildInventory($Selection){
    $actual=@(Get-BuildInventory $Selection.root);$chosen=@($Selection.files|Sort-Object -CaseSensitive)
    if($actual.Count -ne $chosen.Count -or (($actual -join "`n") -cne ($chosen -join "`n"))){throw 'snapshot-conflict'}
}
# 元Buildは読取だけ。copy時に得たbytes/hashを以降の梱包・記録の基準にする。
# 成功時のsource handleは呼出側へ渡し、publishのfinallyで確実に解放する。
# これは保存時のsnapshotであり、UnityがBuildを生成した時点の原子性は保証しない。
function Copy-BuildSnapshot($Selection,[string]$Operation,[Diagnostics.Stopwatch]$Timer){
    Assert-BuildInventory $Selection
    $handles=[Collections.Generic.List[IO.FileStream]]::new()
    $snapshot=[IO.Path]::Combine($Operation,'snapshot');[IO.Directory]::CreateDirectory($snapshot)|Out-Null
    $files=[Collections.Generic.List[OneStarMaker.Artifacts.Packaging.PackageFile]]::new()
    try{
        # 先に全handleをFileShare.Readで確保し、途中の書換えと集合変更を拒否する。
        foreach($name in $Selection.files){
            $source=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($Selection.root,$name)
            $handle=[IO.FileStream]::new($source,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
            $handles.Add($handle)
        }
        Assert-BuildInventory $Selection
        $total=[long]0
        for($i=0;$i -lt $Selection.files.Count;$i++){
            $null=Get-Remaining $Timer 600000
            $name=$Selection.files[$i];$reader=$handles[$i]
            if($reader.Length -gt 256MB -or $total+$reader.Length -gt $script:SnapshotLimit){throw 'size-limit'}
            $target=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($snapshot,$name)
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
            $hash=[Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
            try{
                $writer=[IO.FileStream]::new($target,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
                try{
                    $bytes=[long]0;$buffer=[byte[]]::new(65536);$n=0
                    while(($n=$reader.Read($buffer,0,$buffer.Length)) -gt 0){
                        $null=Get-Remaining $Timer 600000;$bytes+=$n;$total+=$n
                        if($bytes -gt 256MB -or $total -gt $script:SnapshotLimit){throw 'size-limit'}
                        $writer.Write($buffer,0,$n);$hash.AppendData($buffer,0,$n)
                    }
                    if($bytes -ne $reader.Length){throw 'snapshot-conflict'}
                    $writer.Flush($true)
                }finally{$writer.Dispose()}
                $entry=[OneStarMaker.Artifacts.Packaging.PackageFile]::new()
                $entry.Path=$name;$entry.Bytes=$bytes;$entry.Sha256=[Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
                $files.Add($entry)
            }finally{$hash.Dispose()}
        }
        Assert-BuildInventory $Selection
        return @{Files=$files.ToArray();Handles=$handles}
    }catch{foreach($handle in $handles){$handle.Dispose()};throw}
}
function Invoke-BuildNetwork([string]$Generation,[hashtable]$Request){
    if($script:NetworkHook){return & $script:NetworkHook $Generation $Request}
    return Invoke-ArtifactNetwork $Generation $Request
}
# 退役した系列（退役記録がある系列）へは保存も保護もしない。記録を確認できなければ'retirement-unknown'で止める
# （読めない記録を「退役していない」と扱うと退役済み系列へpublishできてしまう）。publish・collectの確認は
# 送信・書込みの前の1回だけで、その時点で退役記録が無かったpublishは止めない。publishは系列lockを取らないので、
# 退役の確定と並行したpublishは成功しうる。成功すれば退役済み系列の成功記録になり、次の退役・cleanupで消える。
function Assert-BuildSeriesActive([string]$RepositoryId,[string]$Project,[string]$Target,[string]$Configuration){
    if((Read-SeriesRetirement 'build' $RepositoryId @($Project,$Target,$Configuration)).State -cne 'none'){throw 'series-retired'}
}
function Invoke-BuildReadback([string]$Operation,$Config,[string]$Generation,[string]$Key,[long]$Bytes,[string]$Hash,[Diagnostics.Stopwatch]$Timer){
    if($script:ReadbackHook){return & $script:ReadbackHook $Operation $Config $Generation $Key $Bytes $Hash}
    return Invoke-ArtifactReadback $Operation $Config $Generation $Key $Bytes $Hash $Timer
}
# 成功fieldは最後まで空に保つ。PUTやGETの成功だけで参照を返すと、
# 正本receiptがない成果物を成功Buildとして扱う呼出側が生まれてしまう。
function Invoke-BuildPublish([string]$ConfigPath,[string]$SelectionPath){
    $r=[ordered]@{status='failed';reasonCode='unconfirmed';reference=$null;packageSha256=$null;buildId=$null;receiptPath=$null;residue=[ordered]@{localPath=$null;remote='pre-put'}}
    $locks=$null;$packageLock=$null;$timer=[Diagnostics.Stopwatch]::StartNew()
    try{
        Import-ArtifactPackage
        $config=Read-BuildConfig $ConfigPath
        if([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($SelectionPath)) -ine [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ConfigPath))){throw 'invalid-selection'}
        $selection=Read-BuildSelection $SelectionPath
        $r.buildId=$selection.buildId
        $null=Get-BuildSeries $config.Data.repositoryId $selection.project $selection.target $selection.configuration
        Assert-BuildSeriesActive $config.Data.repositoryId $selection.project $selection.target $selection.configuration
        Assert-BuildInventory $selection
        # IDの予約はnetwork前に行う。同一IDの再試行・競合は送信を始める前に拒否する。
        $dir=Get-BuildDirectory $config.Data.repositoryId $selection.buildId $true
        $operation=New-ArtifactOperation;$r.residue.localPath=$operation
        $snapshot=Copy-BuildSnapshot $selection $operation $timer;$locks=$snapshot.Handles
        $codec=[OneStarMaker.Artifacts.Packaging.BuildPackage]::new($config.Data.repositoryId,$selection.project,$selection.target,$selection.configuration,$selection.buildId)
        $package=$codec.CreateVerified($operation,[IO.Path]::Combine($operation,'snapshot'),$snapshot.Files,(Get-Remaining $timer 600000))
        if($script:PackageCreatedHook){& $script:PackageCreatedHook $package.Path}
        # hash確定直後に読取lockを取得し、同じlock下の最終検証からPUT完了まで維持する。
        # lock取得前の書換えは次のExtractの外hash検証で拒否する。
        $packageLock=[IO.FileStream]::new($package.Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
        $verified=$codec.Extract($package.Path,$operation,$package.Sha256,$package.ManifestSha256,(Get-Remaining $timer 600000))
        if($verified.Files.Count -ne $snapshot.Files.Count){throw 'package-invalid'}
        Protect-ArtifactTree $operation
        $key=Get-BuildKey $config $selection.project $selection.target $selection.configuration $selection.buildId
        $entries=@($snapshot.Files|Sort-Object Path -CaseSensitive|ForEach-Object{[pscustomobject]@{path=$_.Path;bytes=$_.Bytes;sha256=$_.Sha256}})
        $intent=[ordered]@{schemaVersion=1;purpose='build';buildId=$selection.buildId;repositoryId=$config.Data.repositoryId;project=$selection.project;target=$selection.target;configuration=$selection.configuration;seriesId=$codec.SeriesId;key=$key;configSha256=$config.Hash;evidenceConfigSha256=$config.EvidenceConfigSha256;packageBytes=$package.Bytes;packageSha256=$package.Sha256;manifestSha256=$package.ManifestSha256;entries=$entries;operationPath=$operation}
        # PUT前に固定key・snapshot由来のhash・残置pathをintentへ永続化する。
        # intentは送信計画の記録であり、存在だけでは成功数に含めない。
        $written=Write-BuildImmutable $dir 'intent.json' $intent
        if($script:FaultHook){& $script:FaultHook 'before-put' $intent}
        # 送信後に応答だけ失われる可能性があるので、呼出し前から存在不明として扱う。
        # 同keyを自動再送したり、失敗を理由にremoteを削除したりしない。
        $generation=(Get-CredentialStatus 'osm').Generation;$r.residue.remote='may-have-been-sent'
        $put=Invoke-BuildNetwork $generation (New-ArtifactRequest $config 'put' $key $package.Path $null $timer)
        if(-not (Test-ArtifactSuccess $put 'put' $generation)){throw 'put-unconfirmed'}
        # 別processのGET結果を、ローカルで検証済みの同じZIPのbytes/hashへ結ぶ。
        # 子の終了・pipe EOFも含む観測を検査し、HTTP成功だけでは先へ進めない。
        $read=Invoke-BuildReadback $operation $config $generation $key $package.Bytes $package.Sha256 $timer
        if(-not (Test-ArtifactReadback $read $generation $key $package.Bytes $package.Sha256)){throw 'readback-unconfirmed'}
        # publishedAtは元Build生成時刻でもPUT時刻でもなく、成功commit用の固定UTC。
        # 読戻しを確認してからreceiptを確定し、正本を再読できた場合だけ成功を返す。
        $published=(& $script:Clock).ToUniversalTime().ToString('o')
        $receipt=[ordered]@{schemaVersion=1;purpose='build';buildId=$selection.buildId;repositoryId=$config.Data.repositoryId;project=$selection.project;target=$selection.target;configuration=$selection.configuration;seriesId=$codec.SeriesId;key=$key;configSha256=$config.Hash;evidenceConfigSha256=$config.EvidenceConfigSha256;packageBytes=$package.Bytes;packageSha256=$package.Sha256;manifestSha256=$package.ManifestSha256;entries=$entries;intentSha256=$written.Sha256;readback=$read;publishState='succeeded';publishedAt=$published}
        if($script:FaultHook){& $script:FaultHook 'before-receipt' $receipt}
        $null=Write-BuildImmutable $dir 'receipt.json' $receipt
        if($script:FaultHook){& $script:FaultHook 'after-receipt' $receipt}
        $committed=Read-BuildReceipt $config $selection.buildId
        $r.reference=New-BuildReference $config $committed.Data;$r.packageSha256=$committed.Data.packageSha256
        $r.receiptPath=$committed.Path;$r.residue.remote='verified';$r.status='passed';$r.reasonCode='complete'
    }catch{
        if($script:FailureHook){& $script:FailureHook $_}
        # 例外本文にはpathや通信の内部情報が入りうるため、公開reasonは許可語だけ。
        # 未分類はunconfirmedに留める。rename後の応答喪失はinspectで回復し、再PUTしない。
        $code=$_.Exception.Message
        if($code -cin @('already-exists','invalid-selection','invalid-source','snapshot-conflict','size-limit','put-unconfirmed','readback-unconfirmed','receipt-missing','receipt-invalid','invalid-build-config','series-retired','retirement-unknown')){$r.reasonCode=$code}
        if($code -cin @('after-rename-unconfirmed')){$r.reasonCode='commit-unconfirmed'}
    }finally{if($packageLock){$packageLock.Dispose()};if($locks){foreach($handle in $locks){$handle.Dispose()}}}
    return [pscustomobject]$r
}
# 呼出前にselectionへ保存したIDだけを再照会する。一覧探索や再送は行わない。
# receiptが未確定・破損なら成功へ補完せず、確定済みなら同じ参照/hash/時刻を返す。
# Emitを渡すと、結果を共有access lockの保持中に呼出側へ渡す（CLIはここで出力する）。
# 関数が戻ってからの出力ではlockが外れており、その間に削除意図が確定しうるため。
function Invoke-BuildInspect([string]$ConfigPath,[string]$BuildId,[scriptblock]$Emit=$null){
    $hold=@{Access=$null};$result=$null
    try{
        Import-ArtifactPackage;$config=Read-BuildConfig $ConfigPath;Assert-BuildId $BuildId
        Enter-BuildDeletionCheck $config $BuildId $hold
        $receipt=Read-BuildReceipt $config $BuildId
        $result=[pscustomobject]@{status='passed';reasonCode='complete';buildId=$BuildId;reference=(New-BuildReference $config $receipt.Data);packageSha256=$receipt.Data.packageSha256;receiptPath=$receipt.Path;publishedAt=$receipt.Data.publishedAt}
        if($Emit){& $Emit $result}
        return $result
    }catch{
        if($null -ne $result){throw}
        $result=if($_.Exception.Message -cin @('payload-deleted','delete-pending')){[pscustomobject]@{status='failed';reasonCode=$_.Exception.Message}}else{[pscustomobject]@{status='failed';reasonCode='receipt-unavailable'}}
        if($Emit){& $Emit $result}
        return $result
    }finally{if($hold.Access){$hold.Access.Dispose()}}
}
# 件数清掃でR2 objectを消した（または消している途中の）Buildを、照会が成功として返さないための判定。
# deletion/の記録だけで決まり、build-configに依存しないので、既存receiptの検査より先に行う。
# 共有のaccess lockを結果を返すまで呼出側が持ち続けるので、確認と結果の間に削除意図は確定しない
# （cleanupは削除意図の書込み前に同じlockを排他で取る）。返すのはその時点の状態。
# Build directoryが無ければ何もせず、続くreceipt検査に判定を任せる。
# 取ったlockは$Hold.Accessに置き、判定の結果（payload-deleted／delete-pending の例外を含む）を
# 返し終えるまで呼出側が保持して、finallyで解放する。失敗の結果もlockの外で返すと、
# 返す前に未解決の試行が取り下げられ、古い状態を返しうるため。
function Enter-BuildDeletionCheck($Config,[string]$BuildId,$Hold){
    if(-not [IO.Directory]::Exists((Get-BuildDirectory $Config.Data.repositoryId $BuildId))){return}
    try{$Hold.Access=Enter-BuildAccessLock $Config.Data.repositoryId $BuildId $false}catch{if($_.Exception.Message -ceq 'lock-busy'){throw 'delete-pending'};throw}
    $state=Read-BuildDeletionState $Config.Data.repositoryId $BuildId
    if($state.State -ceq 'deleted'){throw 'payload-deleted'}
    if($state.State -ceq 'deleting'){throw 'delete-pending'}
}
# 利用中保護（最長90日の有限期限）を付ける。成功記録はconfigのhashに依存せずに読み、
# 系列を決めてからその系列lockを取り、lockの下で成功記録と削除状態を読み直してから書く。
# lock前の判定だけで書くと、間に確定した削除意図のあるBuildへ保護を付けうるため。
# createdAtはlock内で書込みの直前に時計から取り、その時点で期限を検査する。
function Invoke-BuildUse([string]$ConfigPath,[string]$BuildId,[string]$Consumer,[string]$Until,[string]$Reason){
    $lock=$null
    try{
        Assert-BuildId $BuildId
        if($Consumer -cnotmatch '\A[A-Za-z0-9][A-Za-z0-9-]{0,63}\z' -or [string]::IsNullOrWhiteSpace($Reason) -or $Reason.Length -gt 512 -or $Reason -cmatch '[\x00-\x1f]'){throw 'invalid-use'}
        try{$null=Assert-Utc $Until}catch{throw 'invalid-use'}
        Import-ArtifactPackage;$config=Read-BuildConfig $ConfigPath;$repo=$config.Data.repositoryId
        try{$record=Read-BuildSuccessRecord $config $BuildId}catch{throw 'build-not-retainable'}
        try{$lock=Enter-BuildSeriesLock $repo $record.Data.project $record.Data.target $record.Data.configuration}catch{if($_.Exception.Message -ceq 'lock-busy'){throw 'cleanup-in-progress'};throw}
        try{$record=Read-BuildSuccessRecord $config $BuildId;$state=Read-BuildDeletionState $repo $BuildId}catch{throw 'build-not-retainable'}
        # 退役はretireが系列lockの下で確定させるので、同じlockの下で確かめる（間に確定した退役を見落とさない）。
        Assert-BuildSeriesActive $repo $record.Data.project $record.Data.target $record.Data.configuration
        if($state.State -cne 'none'){throw 'build-not-retainable'}
        $id=[Guid]::NewGuid().ToString('N');$now=(& $script:Clock).ToUniversalTime()
        $use=[pscustomobject][ordered]@{schemaVersion=[long]1;buildId=$BuildId;id=$id;owner=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value;consumer=$Consumer;reason=$Reason;createdAt=$now.ToString('o');until=$Until}
        if(-not (Test-BuildProtectionRecord ([pscustomobject]@{Id=$id;Use=$use;Release=$null}) $BuildId)){throw 'invalid-use'}
        # temp書込みの間に期限を過ぎた保護は確定させない（renameの直前に時計を読み直す）。
        # createdAtはrenameの直前の時刻とみなせる範囲でなければならない（時計の巻戻りも含めて、
        # その時点で createdAt <= now < until <= now+90日 を満たさなければ確定しない）。
        $clock=$script:Clock;$untilTime=Assert-Utc $Until
        $commit={$t=(& $clock).ToUniversalTime();if($t -lt $now -or $t -ge $untilTime -or $untilTime -gt $t.AddDays(90)){throw 'invalid-use'}}.GetNewClosure()
        $null=Write-BuildImmutable (Get-BuildDirectory $repo $BuildId) "protections/$id.use.json" $use $commit
        return [pscustomobject]@{status='passed';reasonCode='in-use';buildId=$BuildId;protection=$id;until=$Until}
    }catch{
        if($script:FailureHook){& $script:FailureHook $_}
        $code=$_.Exception.Message
        return [pscustomobject]@{status='failed';reasonCode=if($code -cin @('invalid-use','build-not-retainable','cleanup-in-progress','invalid-build-config','series-retired','retirement-unknown')){$code}else{'unconfirmed'}}
    }finally{if($lock){$lock.Dispose()}}
}
# 保護を外す。既存のuse記録は書き換えず、release記録を追記する。release済みなら何もしない。
# 系列lockを取る: 保護が外れると保持枠が空き、より新しいBuildが候補から外れることがある。
# lockなしで外すと、実行中のcleanupが外れる前のsnapshotの候補（今は保持すべきBuild）を消しうる。
# 時計がuseより前に戻っているとreleasedAt < createdAtの読めない記録になるので、書かずに止める。
function Invoke-BuildRelease([string]$ConfigPath,[string]$BuildId,[string]$Protection){
    $lock=$null
    try{
        Assert-BuildId $BuildId;Assert-Hex $Protection 32
        Import-ArtifactPackage;$config=Read-BuildConfig $ConfigPath;$repo=$config.Data.repositoryId
        try{$record=Read-BuildSuccessRecord $config $BuildId}catch{throw 'unknown-protection'}
        try{$lock=Enter-BuildSeriesLock $repo $record.Data.project $record.Data.target $record.Data.configuration}catch{if($_.Exception.Message -ceq 'lock-busy'){throw 'cleanup-in-progress'};throw}
        try{$records=@(Read-BuildProtectionRecords $repo $BuildId|Where-Object Id -CEQ $Protection)}catch{throw 'unknown-protection'}
        if($records.Count -ne 1 -or -not (Test-BuildProtectionRecord $records[0] $BuildId)){throw 'unknown-protection'}
        if($null -eq $records[0].Release){
            $now=(& $script:Clock).ToUniversalTime()
            if($now -lt (Assert-Utc $records[0].Use.createdAt)){throw 'clock-skew'}
            $null=Write-BuildImmutable (Get-BuildDirectory $repo $BuildId) "protections/$Protection.release.json" ([ordered]@{schemaVersion=1;buildId=$BuildId;id=$Protection;releasedAt=$now.ToString('o')})
        }
        return [pscustomobject]@{status='passed';reasonCode='released';buildId=$BuildId;protection=$Protection}
    }catch{
        if($script:FailureHook){& $script:FailureHook $_}
        $code=$_.Exception.Message
        return [pscustomobject]@{status='failed';reasonCode=if($code -cin @('unknown-protection','clock-skew','cleanup-in-progress','invalid-build-config')){$code}else{'unconfirmed'}}
    }finally{if($lock){$lock.Dispose()}}
}
# 参照文字列と独立した期待hashを両方要求する。取得したmanifest自身のhashを
# 信頼の起点にすると、別の有効ZIPへの差替えを検出できない。
# 展開先は毎回新規operationで、元Buildへの上書きや取得DLL/EXEの実行をしない。
function Invoke-BuildFetch([string]$ConfigPath,[string]$Reference,[string]$ExpectedHash){
    $r=[ordered]@{status='failed';reasonCode='unconfirmed';outputPath=$null;residue=[ordered]@{localPath=$null}};$timer=[Diagnostics.Stopwatch]::StartNew();$access=$null
    try{
        Assert-Hex $ExpectedHash 64;Import-ArtifactPackage;$config=Read-BuildConfig $ConfigPath
        $ref=Read-BuildReference $Reference $config
        # この PC に同じBuildの記録があれば、取得の開始から終了まで共有のaccess lockを持つ。
        # 件数清掃はこれを排他で開けないBuildを飛ばす（保護はこの PC の取得だけで、別PCは観測しない）。
        # 清掃がそのBuildの削除試行中なら待たずに失敗する。
        if([IO.Directory]::Exists((Get-BuildDirectory $config.Data.repositoryId $ref.buildId))){
            try{$access=Enter-BuildAccessLock $config.Data.repositoryId $ref.buildId $false}catch{if($_.Exception.Message -ceq 'lock-busy'){throw 'delete-pending'};throw}
        }
        $op=New-ArtifactOperation;$r.residue.localPath=$op
        $generation=(Get-CredentialStatus 'osm').Generation;$archive=[IO.Path]::Combine($op,'download.zip')
        $download=Invoke-BuildNetwork $generation (New-ArtifactRequest $config 'get' $ref.key $null $archive $timer)
        if(-not (Test-ArtifactSuccess $download 'get' $generation) -or $download.Bytes -ne $ref.packageBytes -or $download.Sha256 -cne $ExpectedHash){throw 'download-unconfirmed'}
        $codec=[OneStarMaker.Artifacts.Packaging.BuildPackage]::new($ref.repositoryId,$ref.project,$ref.target,$ref.configuration,$ref.buildId)
        $package=$codec.Extract($archive,$op,$ExpectedHash,$ref.manifestSha256,(Get-Remaining $timer 600000))
        Protect-ArtifactTree $op
        $r.outputPath=$package.Path;$r.status='passed';$r.reasonCode='complete'
    }catch{if($script:FailureHook){& $script:FailureHook $_};if($_.Exception.Message -cin @('download-unconfirmed','invalid-reference','delete-pending')){$r.reasonCode=$_.Exception.Message}}
    finally{if($access){$access.Dispose()}}
    return [pscustomobject]$r
}
function Write-BuildOperationFile([string]$Operation,[string]$Name,[byte[]]$Bytes){
    $path=[IO.Path]::Combine($Operation,$Name)
    $stream=[IO.FileStream]::new($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{$stream.Write($Bytes,0,$Bytes.Length);$stream.Flush($true)}finally{$stream.Dispose()}
    Set-ArtifactAcl $path $false;return $path
}
# BS4の配布形Player 1件を、人がJSONを書かずに退避・selection生成・既存publishへつなぐ。
# 退避（BuildStage）と成功確定（BuildStore/Invoke-BuildPublish）は別の所有者で、
# ここは順序と再実行時の分岐だけを持つ。#111の形式・検査・PUT規則は変えずにそのまま呼ぶ。
# 再実行: 退避済みなら退避を省き、成功receiptがあれば同じ参照を返してPUTしない。
# 予約だけ残る（応答不明の可能性がある）IDは自動で再送せず publish-unresolved で止める。
# Emitはinspectと同じく、既存成功の結果を共有access lockの保持中に呼出側へ渡すためのもの。
function Invoke-BuildCollect([string]$ConfigPath,[string]$Source,[string]$ArtifactsRoot,[scriptblock]$Emit=$null){
    $r=[ordered]@{status='failed';reasonCode='unconfirmed';reference=$null;packageSha256=$null;buildId=$null;stage=$null;outputPath=$null;publish=$null};$hold=@{Access=$null};$emitted=$false
    try{
        Import-ArtifactPackage
        # 設定とArtifactsRootの検査はファイル操作の前に済ませ、不足時は何も作らない。
        $config=Read-BuildConfig $ConfigPath
        if(-not [IO.Path]::IsPathFullyQualified($Source)){throw 'invalid-source'}
        $source=[IO.Path]::GetFullPath($Source).TrimEnd('\')
        $root=Resolve-BuildArtifactsRoot $ArtifactsRoot $source
        if(-not [IO.Directory]::Exists($source)){throw 'invalid-source'}
        $identity=[IO.Path]::GetFileName($source);$buildId=Get-BuildCollectBuildId $identity;$r.buildId=$buildId
        $inventory=@(Get-BuildInventory $source)
        Assert-BuildStageSource $source $inventory
        $receipt=ConvertFrom-BuildPlayerReceipt (Read-BuildStageBytes ([IO.Path]::Combine($source,'build-receipt.json'))) $identity
        $manifest=ConvertFrom-BuildTransportManifest (Read-BuildStageTransport $receipt.transportPath) $receipt
        $series=Get-BuildCollectSeries $receipt
        $null=Get-BuildSeries $config.Data.repositoryId $series.project $series.target $series.configuration
        # 退避（ArtifactsRootへの書込み）と既存成功の応答より前に確かめる。退役済みの系列ではexistingも返さない。
        Assert-BuildSeriesActive $config.Data.repositoryId $series.project $series.target $series.configuration
        $files=@($inventory|Where-Object {$_ -cne 'build-receipt.json'})
        # 上限は退避先に置く予定の集合（receipt除外・build-info追加）で、hash計算やcopyの前に見る。
        $sizes=@($files|ForEach-Object {[pscustomobject]@{path=$_;bytes=[IO.FileInfo]::new([OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($source,$_)).Length}})
        Assert-BuildCollectLimits ($sizes+@([pscustomobject]@{path='build-info.json';bytes=[long]0}))
        $entries=@(Get-BuildStageEntries $source $files)
        Assert-BuildContentManifest $entries $manifest
        $info=New-BuildInfoBytes $series $buildId $receipt $manifest
        $infoEntry=[pscustomobject]@{path='build-info.json';bytes=[long]$info.Length;sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($info)).ToLowerInvariant()}
        $expected=@($entries)+@($infoEntry)
        Assert-BuildCollectLimits $expected
        $destination=Get-BuildStageDestination $root $series $buildId
        $r.stage=Invoke-BuildStage $source $destination $entries $info $expected {param($dir)Get-BuildInventory $dir}
        $r.outputPath=$destination
        $dir=Get-BuildDirectory $config.Data.repositoryId $buildId
        if([IO.File]::Exists([IO.Path]::Combine($dir,'receipt.json'))){
            # 件数清掃で消した・消している途中のBuildを既存の成功として返さず、再PUTもしない。
            Enter-BuildDeletionCheck $config $buildId $hold
            $committed=Read-BuildReceipt $config $buildId
            # 同じbuildIdでも別内容の成功記録なら、この入力の成功として返さない。
            $want=@($expected|Sort-Object path -CaseSensitive|ForEach-Object {"$($_.path)|$($_.bytes)|$($_.sha256)"}) -join "`n"
            $have=@($committed.Data.entries|Sort-Object path -CaseSensitive|ForEach-Object {"$($_.path)|$($_.bytes)|$($_.sha256)"}) -join "`n"
            if($want -cne $have -or $committed.Data.project -cne $series.project -or $committed.Data.target -cne $series.target -or $committed.Data.configuration -cne $series.configuration){throw 'receipt-conflict'}
            $r.reference=New-BuildReference $config $committed.Data;$r.packageSha256=$committed.Data.packageSha256
            $r.publish='existing';$r.status='passed';$r.reasonCode='complete'
            $out=[pscustomobject]$r;if($Emit){& $Emit $out;$emitted=$true};return $out
        }
        if([IO.Directory]::Exists($dir) -or [IO.File]::Exists($dir+'.reserved')){throw 'publish-unresolved'}
        # selectionとconfigはpublishの要求どおり同じ新規private operationに置く。
        # 利用者のbuild-config原本は読むだけで、bytesの複写を今回の入力にする。
        $operation=New-ArtifactOperation
        $configCopy=Write-BuildOperationFile $operation 'build-config.json' ([IO.File]::ReadAllBytes($ConfigPath))
        $selection=[ordered]@{schemaVersion=1;purpose='build';root=$destination;files=@(Get-BuildInventory $destination);project=$series.project;target=$series.target;configuration=$series.configuration;buildId=$buildId}
        $selectionPath=Write-BuildOperationFile $operation 'build-selection.json' ([Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $selection -Compress -Depth 8)))
        $p=Invoke-BuildPublish $configCopy $selectionPath
        $r.publish='published';$r.reasonCode=$p.reasonCode
        if($p.status -ceq 'passed'){$r.reference=$p.reference;$r.packageSha256=$p.packageSha256;$r.status='passed'}
    }catch{
        if($script:FailureHook){& $script:FailureHook $_}
        # 例外本文はpathを含みうるため、公開reasonは許可語だけにする。
        $code=$_.Exception.Message
        if($code -cin @('artifacts-root-not-configured','invalid-artifacts-root','invalid-source','invalid-build-config','invalid-build-identity','invalid-player-receipt','transport-mismatch','content-mismatch','unsupported-series','size-limit','stage-conflict','stage-hash-mismatch','publish-unresolved','receipt-conflict','receipt-invalid','snapshot-conflict','payload-deleted','delete-pending','series-retired','retirement-unknown')){$r.reasonCode=$code}
        # 削除状態に関わる失敗も、確認に使ったlockを保持したまま呼出側へ渡す。
        if($hold.Access -and $Emit){$out=[pscustomobject]$r;& $Emit $out;$emitted=$true}
    }finally{if($hold.Access){$hold.Access.Dispose()}}
    $out=[pscustomobject]$r;if($Emit -and -not $emitted){& $Emit $out}
    return $out
}
Export-ModuleMember -Function Invoke-BuildPublish,Invoke-BuildFetch,Invoke-BuildInspect,Invoke-BuildCollect,Invoke-BuildUse,Invoke-BuildRelease
