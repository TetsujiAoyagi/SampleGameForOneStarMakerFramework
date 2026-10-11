Set-StrictMode -Version Latest
# fresh processのCLIでも同じ関数解決になるよう、使用するmoduleを直接importする。
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactApplication.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildStage.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentContract.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentStage.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentTransfer.psm1')
# collect/inspect/fetchの順序、private operationの作成、再実行の分岐、receiptの確定だけを持つ。
# operationは作るだけで削除しない（清掃は「未完了uploadとstaging」の後続）。
# 注入点はoffline検証専用。公開CLIから時計や失敗境界を差し替えさせない。
$script:Clock={[DateTimeOffset]::UtcNow}
$script:FailureHook=$null
# 公開reasonは許可語だけ。例外本文にはpathや通信の内部情報が入りうるため出さない。
# Build側の語は、Contentの語へ写して返す。未分類はunconfirmedに留める。
$script:Reasons=@('artifacts-root-not-configured','invalid-artifacts-root','invalid-source','invalid-content-config','invalid-content-identity','unsupported-series','unsupported-content','invalid-content-manifest','content-mismatch','size-limit','stage-conflict','stage-hash-mismatch','snapshot-changed','intent-conflict','revision-conflict','object-conflict','object-unconfirmed','put-unconfirmed','readback-unconfirmed','publish-in-progress','budget-exceeded','receipt-missing','receipt-invalid','commit-unconfirmed','content-store-unavailable','invalid-reference','manifest-mismatch','download-unconfirmed')
$script:ReasonMap=@{'snapshot-conflict'='invalid-source';'already-exists'='content-store-unavailable';'build-store-unavailable'='content-store-unavailable';'after-rename-unconfirmed'='commit-unconfirmed';'evidence-deadline'='budget-exceeded';'Artifact operation expired.'='budget-exceeded'}
function Get-ContentReason([string]$Code){
    if($Code -cin $script:Reasons){return $Code}
    if($script:ReasonMap.ContainsKey($Code)){return $script:ReasonMap[$Code]}
    return $null
}
function Read-ContentConfigBytes([string]$Path){
    if(-not [IO.Path]::IsPathFullyQualified($Path) -or -not [IO.File]::Exists($Path) -or [OneStarMaker.Artifacts.Packaging.PackagePolicy]::HasReparse($Path)){throw 'invalid-content-config'}
    $length=[IO.FileInfo]::new($Path).Length;if($length -lt 1 -or $length -gt 1MB){throw 'invalid-content-config'}
    return [IO.File]::ReadAllBytes($Path)
}
# 利用者のcontent-config原本は読むだけで、bytesの複写を今回の入力にする（読んだ後の差替えを避ける）。
function Write-ContentOperationFile([string]$Operation,[string]$Name,[byte[]]$Bytes){
    $path=[IO.Path]::Combine($Operation,$Name)
    $stream=[IO.FileStream]::new($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{$stream.Write($Bytes,0,$Bytes.Length);$stream.Flush($true)}finally{$stream.Dispose()}
    Set-ArtifactAcl $path $false;return $path
}
function Get-ContentCommitted($Dir,$Config,$Manifest){
    $c=Read-ContentReceipt $Dir $Config.Data.repositoryId $Manifest.ContentSet $Manifest.TargetToken $Manifest.Revision
    return $c
}
# 成功fieldは最後まで空に保つ。PUT・GETの成功だけでreferenceを返すと、正本receiptの無い成果物を
# 成功として扱う呼出側が生まれる。receiptのatomic renameだけが成功確定点。
# 再実行の優先順位（AC11）: (1)退避先の不一致(stage-conflict) → (2)成功receiptがあり同一入力ならexisting
# → (3)intentがあり違えばintent-conflict → (4)遠隔transport.jsonの不一致(revision-conflict) → (5)照合経路での続行。
# 「同一入力」はintent.jsonのbytes一致で判定する。実装は(2)と(3)を同じintent照合で先に評価する
# （違えばreceiptがあってもintent-conflictで、結果は上の番号順と同じ）。
function Invoke-ContentCollect([string]$ConfigPath,[string]$Source,[string]$ArtifactsRoot){
    $r=[ordered]@{status='failed';reasonCode='unconfirmed';reference=$null;manifestSha256=$null;contentSet=$null;target=$null;revision=$null;stage=$null;publish=$null;objects=$null;outputPath=$null;receiptPath=$null;residue=[ordered]@{localPath=$null;remote='pre-put'}}
    $lock=$null;$ctx=$null;$manifest=$null
    try{
        Import-ArtifactPackage
        # ArtifactsRootとsourceの検査はファイル操作の前に済ませ、不足時は何も作らない。
        if(-not [IO.Path]::IsPathFullyQualified($Source)){throw 'invalid-source'}
        $source=[IO.Path]::GetFullPath($Source).TrimEnd('\')
        $root=Resolve-BuildArtifactsRoot $ArtifactsRoot $source
        if(-not [IO.Directory]::Exists($source)){throw 'invalid-source'}
        $manifestBytes=Read-ContentManifestFile ([IO.Path]::Combine($source,'transport.json'))
        $manifest=ConvertFrom-ContentManifest $manifestBytes
        if([IO.Path]::GetFileName($source) -cne $manifest.Revision){throw 'invalid-content-identity'}
        $r.contentSet=$manifest.ContentSet;$r.target=$manifest.TargetToken;$r.revision=$manifest.Revision;$r.manifestSha256=$manifest.Sha256
        $configBytes=Read-ContentConfigBytes $ConfigPath
        $operation=New-ArtifactOperation;$r.residue.localPath=$operation
        $config=Read-ContentConfig (Write-ContentOperationFile $operation 'content-config.json' $configBytes)
        $stage=Invoke-ContentStage $source $root $manifest
        $r.stage=$stage.Stage;$r.outputPath=$stage.Path
        $dir=Get-ContentDirectory $config.Data.repositoryId $manifest.ContentSet $manifest.TargetToken $manifest.Revision $true
        $lock=Enter-ContentPublishLock $dir
        $intent=New-ContentIntent $config $manifest
        # 同一入力か（intent bytesの一致）。違えばintent-conflict。receiptの有無を見る前に判定する。
        $null=Test-ContentIntent $dir $intent
        if([IO.File]::Exists([IO.Path]::Combine($dir,'receipt.json'))){
            $committed=Get-ContentCommitted $dir $config $manifest
            $r.reference=New-ContentReference $config $committed.Data;$r.receiptPath=$committed.Path
            $r.publish='existing';$r.residue.remote='verified';$r.status='passed';$r.reasonCode='complete'
        }else{
            # 公開は退避先からprivate operationへsnapshotして行い、ArtifactsRootのfileを直接送らない。
            $snapshot=[IO.Path]::Combine($operation,'snapshot')
            Copy-ContentSnapshot $stage.Path $snapshot $stage.Entries
            Protect-ArtifactTree $operation
            $ctx=New-ContentTransferContext $config $operation $snapshot $manifest
            $r.publish='published'
            $scope=Enter-ContentBudget (Get-ContentBudgetMs 'publish' $manifest.ObjectCount)
            try{
                $state=Confirm-ContentRemoteManifest $ctx
                # intentは最初の送信前に固定する。遠隔照合（読取のみ）の後、最初のPUTの前。
                $ctx.State.IntentSha256=Set-ContentIntent $dir $intent
                Publish-ContentObjects $ctx $state
                # 全objectの照合が通った後でだけreceiptを確定し、正本を再読できた場合だけ成功を返す。
                # 予算（AC13）はreceiptのatomic renameの直前まで有効にし、超えていれば成功にしない。
                Assert-ContentBudget
                $receipt=New-ContentReceipt $intent $ctx.State.IntentSha256 ((& $script:Clock).ToUniversalTime().ToString('o'))
                Write-ContentReceipt $dir $receipt
            }finally{Exit-ContentBudget $scope}
            $committed=Get-ContentCommitted $dir $config $manifest
            $r.reference=New-ContentReference $config $committed.Data;$r.receiptPath=$committed.Path
            $r.residue.remote='verified';$r.status='passed';$r.reasonCode='complete'
        }
    }catch{
        if($script:FailureHook){& $script:FailureHook $_}
        $code=Get-ContentReason $_.Exception.Message
        if($code){$r.reasonCode=$code}
    }finally{
        if($lock){$lock.Dispose()}
        if($ctx){
            $s=$ctx.State;$r.objects=[ordered]@{total=$manifest.ObjectCount;sent=$s.Sent;verified=$s.Verified;puts=$s.Puts;gets=$s.Gets}
            if($r.status -cne 'passed'){$r.residue.remote=$s.Remote}
        }
    }
    return [pscustomobject]$r
}
# 呼出前に保存したidentityだけを再照会する。一覧探索や再送は行わず、遠隔のobjectも見ない。
# receiptが未確定・破損なら成功へ補完せず、確定済みなら同じreference/hash/時刻を返す。
function Invoke-ContentInspect([string]$ConfigPath,[string]$ContentSet,[string]$Target,[string]$Revision){
    try{
        $config=Read-ContentConfig $ConfigPath
        $dir=Get-ContentDirectory $config.Data.repositoryId $ContentSet $Target $Revision $false
        $c=Read-ContentReceipt $dir $config.Data.repositoryId $ContentSet $Target $Revision
        return [pscustomobject]@{status='passed';reasonCode='complete';contentSet=$ContentSet;target=$Target;revision=$Revision;reference=(New-ContentReference $config $c.Data);manifestSha256=$c.Data.manifestSha256;receiptPath=$c.Path;publishedAt=$c.Data.publishedAt}
    }catch{return [pscustomobject]@{status='failed';reasonCode='receipt-unavailable'}}
}
# referenceと独立に渡された期待hashの両方を要求する。取得したtransport.json自身のhashを信頼の
# 起点にすると、別の有効なContentへの差替えを検出できない。展開先は毎回新規のprivate operationで、
# ArtifactsRootには書かず、取得物は実行しない。
function Invoke-ContentFetch([string]$ConfigPath,[string]$Reference,[string]$ExpectedHash){
    $r=[ordered]@{status='failed';reasonCode='unconfirmed';outputPath=$null;manifestSha256=$null;objects=$null;residue=[ordered]@{localPath=$null}}
    try{
        try{Assert-Hex $ExpectedHash 64}catch{throw 'invalid-reference'}
        Import-ArtifactPackage
        $configBytes=Read-ContentConfigBytes $ConfigPath
        $operation=New-ArtifactOperation;$r.residue.localPath=$operation
        $config=Read-ContentConfig (Write-ContentOperationFile $operation 'content-config.json' $configBytes)
        $ref=Read-ContentReference $Reference $config
        if($ref.manifestSha256 -cne $ExpectedHash){throw 'manifest-mismatch'}
        $payload=[IO.Path]::Combine($operation,'payload')
        $scope=Enter-ContentBudget (Get-ContentBudgetMs 'fetch' $ref.objectCount)
        try{
            $generation=Get-ContentGeneration
            $m=Receive-ContentObject $config $generation (Get-ContentKey $config $ref.contentSet $ref.target $ref.revision) ([IO.Path]::Combine($payload,'transport.json'))
            # 期待hashと違えばほかのfileを取らない。
            if($m.Sha256 -cne $ExpectedHash){throw 'manifest-mismatch'}
            $manifest=ConvertFrom-ContentManifest (Read-ContentManifestFile ([IO.Path]::Combine($payload,'transport.json')))
            if($manifest.Sha256 -cne $ExpectedHash -or $manifest.ContentSet -cne $ref.contentSet -or $manifest.TargetToken -cne $ref.target -or $manifest.Revision -cne $ref.revision -or $manifest.ObjectCount -ne $ref.objectCount -or $manifest.TotalBytes -ne $ref.totalBytes){throw 'manifest-mismatch'}
            foreach($f in $manifest.Files){
                $destination=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($payload,"content/$($f.path)")
                $null=Receive-ContentObject $config $generation (Get-ContentKey $config $manifest.ContentSet $manifest.TargetToken $manifest.Revision $f.path) $destination $f.bytes $f.sha256
            }
            # 集合は完全一致。余剰・欠落があれば拒否する（各fileのsize/hashは取得時に照合済み）。
            Assert-ContentSet $payload (Get-ContentExpectedEntries $manifest) 'content-mismatch'
            Protect-ArtifactTree $operation
            # 予算（AC13）は最後の保護・検証の後、成功を返す直前まで有効にする。
            Assert-ContentBudget
        }finally{Exit-ContentBudget $scope}
        $r.outputPath=$payload;$r.manifestSha256=$ExpectedHash;$r.objects=[ordered]@{total=[int]$ref.objectCount}
        $r.status='passed';$r.reasonCode='complete'
    }catch{
        if($script:FailureHook){& $script:FailureHook $_}
        $code=Get-ContentReason $_.Exception.Message
        if($code){$r.reasonCode=$code}
    }
    return [pscustomobject]$r
}
Export-ModuleMember -Function Invoke-ContentCollect,Invoke-ContentInspect,Invoke-ContentFetch
