Set-StrictMode -Version Latest
# fresh processのCLIでも同じ関数解決になるよう、使用するmoduleを直接importする。
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactApplication.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactDeletion.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildStage.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildRetentionPolicy.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentContract.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentDeletionStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentCatalog.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentProtection.psm1')
Import-Module (Join-Path $PSScriptRoot 'SeriesRetirementStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'Credentials/CredentialStore.psm1')
# Content系列1つの件数清掃（R2のobjectだけを消す。receipt等のローカル記録・ArtifactsRoot・DIST cacheには触れない）。
# ここは順序だけを持つ。分類はContentCatalog、保護源はContentProtection、選別はBuildRetentionPolicy、
# 記録はContentDeletionStore、観測の形はArtifactDeletionが所有する。
# 消し損ねは許し、消し過ぎは許さない: 判定できない・確認できないときは試行を未解決のまま残し、
# 次回の実行が続きから確定させる。削除済みの判定はGETの404 NoSuchKeyだけに依る。
# BuildCleanupと手順本体を共有しないのは、Contentが複数objectで、確定点の後は取り下げられないため。
# 系列の退役（content retire）と退役済みの系列もこの順序で動き、違いは退役記録の確認・書込みと選別だけ。
#
# 注入点はoffline検証専用で、公開CLIから時計・通信・N・上限・予算を差し替えさせない。
$script:Clock={[DateTimeOffset]::UtcNow}
$script:KeepCount=10        # 系列ごとのN。定数で、CLIやcontent-configからは変えない。
$script:DeleteLimit=2048    # 1回の実行で送るDELETEの総数（全試行の合計）。
$script:BudgetMilliseconds=600000
$script:NetworkHook=$null
$script:BudgetHook=$null
$script:StepHook=$null
$script:Run=$null

function Assert-ContentCleanupBudget([string]$Point){
    if($script:BudgetHook){& $script:BudgetHook $Point}
    if($script:Run.Timer.ElapsedMilliseconds -ge $script:BudgetMilliseconds){throw 'budget-exceeded'}
}
# 送るDELETEごとに判定する。試行の途中で達したら、その試行は未解決のまま止まる（次回が続きから確定させる）。
function Assert-ContentCleanupDeleteLimit{if($script:Run.DeleteCount -ge $script:DeleteLimit){throw 'delete-limit'}}
function Get-ContentCleanupGeneration {
    if(-not $script:Run.Generation){$script:Run.Generation=(Get-CredentialStatus 'osm').Generation}
    return $script:Run.Generation
}
# 1回の送信。回数は送信を試みた時点で数える（応答が失われても送った可能性があり、同じrunで再送しない）。
# transportのGETは存在しないdestinationを要求する。objectが残っていた場合の取得先は新規のprivate operationで、
# ここでは読まず、清掃もしない（content fetchの残置と同じ扱い）。
# 例外（応答不明）は呼出側の試行を未解決にする。返すのは非秘密の固定fieldへ写した観測。
function Send-ContentCleanupRequest([string]$Operation,[string]$Key,[string]$Generation){
    $run=$script:Run;$destination=$null
    if($Operation -ceq 'get'){
        if(-not $run.Operation){$run.Operation=New-ArtifactOperation}
        $destination=[IO.Path]::Combine($run.Operation,'absence-'+[Guid]::NewGuid().ToString('N')+'.bin')
    }
    $request=New-ArtifactRequest $run.Config $Operation $Key $null $destination $run.Timer
    if($Operation -ceq 'delete'){$request.ContentRetentionDelete=$true;$run.Result.requests.delete++;$run.DeleteCount++}else{$run.Result.requests.get++}
    $response=if($script:NetworkHook){& $script:NetworkHook $Generation $request}else{Invoke-ArtifactNetwork $Generation $request}
    return ConvertTo-ArtifactObservation $response $Operation $Key $Generation ((& $script:Clock).ToUniversalTime().ToString('o'))
}
# 1 revisionの1試行。手順と記録は HANDOFF の「削除試行の状態遷移」表に従う:
# 1 intent → 2 transport.jsonのDELETE（試行ごとに高々1回）→ 3 sent → 4 確認GET →
# 5 committed（確定点）／6 abandoned → 7 残りobject（GET → 2xxならDELETE → GET）→ 8 deleted。
# 再開はcommittedがあれば手順7から（GETが先なので済んだkeyへDELETEを送らない）、無ければ手順4から。
# 呼出側がrevisionのpublish.lockを保持している間に呼ぶ。
# deleted.jsonの観測は、手順7を最後まで進めたこのrunの分だけを持つ。手順7が途中で止まったrunの観測は残さない:
# 残すには別の永続記録が要り、削除済みの判定は各keyの最後のGET 404だけで足りるため（再開は手順7の最初から
# 全keyをGETし直すので、完了したrunの観測だけで各keyの最後のGET 404が揃う）。
# 返値: deleted / abandoned / delete-unconfirmed / delete-pending / budget-exceeded / delete-limit
function Invoke-ContentDeleteAttempt([string]$Revision,$Info,[bool]$Resume){
    $run=$script:Run
    try{
        $dir=Get-ContentDirectory $run.Config.Data.repositoryId $run.ContentSet $run.Target $Revision
        $generation=Get-ContentCleanupGeneration;$keys=@($Info.keys)
        if($Resume){$n=$Info.pending.Attempt;$intentSha=$Info.pending.IntentSha256;$sentSha=$Info.pending.SentSha256;$committedSha=$Info.pending.CommittedSha256}
        else{
            $n=$Info.next;$committedSha=$null
            $intent=[ordered]@{schemaVersion=1;attempt=$n;revision=$Revision;manifestSha256=$Info.manifestSha256;publishIntentSha256=$Info.publishIntentSha256;keys=$keys;intentAt=(& $script:Clock).ToUniversalTime().ToString('o')}
            $intentSha=(Write-ContentDeletionRecord $dir "deletion/$n.intent.json" $intent {Assert-ContentCleanupBudget 'intent'}).Sha256
            $sentSha=$null
            Assert-ContentCleanupBudget 'delete';Assert-ContentCleanupDeleteLimit
            $observation=Send-ContentCleanupRequest 'delete' $keys[0] $generation
            $sentSha=(Write-ContentDeletionRecord $dir "deletion/$n.sent.json" ([ordered]@{schemaVersion=1;attempt=$n;intentSha256=$intentSha;observation=$observation}) {Assert-ContentCleanupBudget 'sent'}).Sha256
            # 検証済み2xx以外（403・429・5xx・timeout・redirect・generation変化を含む）は確認へ進まない。
            if(-not (Test-ArtifactSuccess (ConvertFrom-ArtifactObservation $observation) 'delete' $generation)){return 'delete-pending'}
        }
        if(-not $committedSha){
            Assert-ContentCleanupBudget 'get'
            $observation=Send-ContentCleanupRequest 'get' $keys[0] $generation;$seen=ConvertFrom-ArtifactObservation $observation
            if(Test-ArtifactSuccess $seen 'get' $generation){
                # 確定点の前でtransport.jsonが残っている: revisionは無傷なので試行を閉じて成功行へ戻す。
                # DELETE 2xxの直後に残った場合は、同じrunでは同じrevisionに新しい試行を始めない（呼出側が除外する）。
                $null=Write-ContentDeletionRecord $dir "deletion/$n.abandoned.json" ([ordered]@{schemaVersion=1;attempt=$n;intentSha256=$intentSha;getObservation=$observation}) {Assert-ContentCleanupBudget 'abandoned'}
                if($Resume){return 'abandoned'};return 'delete-unconfirmed'
            }
            if(-not (Test-ArtifactMissing $seen $generation)){return 'delete-pending'}
            $mark=if($sentSha){'recorded'}else{'unknown'}
            $committedSha=(Write-ContentDeletionRecord $dir "deletion/$n.committed.json" ([ordered]@{schemaVersion=1;attempt=$n;intentSha256=$intentSha;sentSha256=$sentSha;deleteObservation=$mark;getObservation=$observation}) {Assert-ContentCleanupBudget 'committed'}).Sha256
        }
        if($script:StepHook){& $script:StepHook 'after-commit'}
        # 確定点の後: revisionはもう取得できない（fetchはtransport.jsonから始まる）ので、候補・保護の判定に
        # 依らず残りを消し切る。取り下げず、判定できなければ未解決のまま止める。
        $observations=[Collections.Generic.List[object]]::new()
        foreach($key in @($keys|Select-Object -Skip 1)){
            Assert-ContentCleanupBudget 'object-get'
            $o=Send-ContentCleanupRequest 'get' $key $generation;$observations.Add($o)
            if(Test-ArtifactMissing (ConvertFrom-ArtifactObservation $o) $generation){continue}
            if(-not (Test-ArtifactSuccess (ConvertFrom-ArtifactObservation $o) 'get' $generation)){return 'delete-pending'}
            Assert-ContentCleanupBudget 'object-delete';Assert-ContentCleanupDeleteLimit
            $o=Send-ContentCleanupRequest 'delete' $key $generation;$observations.Add($o)
            if(-not (Test-ArtifactSuccess (ConvertFrom-ArtifactObservation $o) 'delete' $generation)){return 'delete-pending'}
            Assert-ContentCleanupBudget 'object-get'
            $o=Send-ContentCleanupRequest 'get' $key $generation;$observations.Add($o)
            # DELETE 2xxの後も残る（2xx）・判定できないときは未解決。確定点の後なので取り下げない。
            if(-not (Test-ArtifactMissing (ConvertFrom-ArtifactObservation $o) $generation)){return 'delete-pending'}
        }
        $null=Write-ContentDeletionRecord $dir "deletion/$n.deleted.json" ([ordered]@{schemaVersion=1;attempt=$n;committedSha256=$committedSha;observations=@($observations.ToArray())}) {Assert-ContentCleanupBudget 'deleted'}
        return 'deleted'
    }catch{
        $code=$_.Exception.Message
        if($code -cin @('budget-exceeded','Artifact operation expired.')){return 'budget-exceeded'}
        if($code -ceq 'delete-limit'){return 'delete-limit'}
        # 応答不明とintent確定後の記録の書込み失敗を含む。確定した記録だけが次回の再開点になる。
        return 'delete-pending'
    }
}
function Get-ContentCleanupSnapshot {
    $run=$script:Run
    try{$protected=Get-ContentProtectedBuildIds $run.Config.Data.repositoryId $run.ArtifactsRoot}
    catch{return [pscustomobject]@{status='blocked';reasonCode='protection-unknown'}}
    return Get-ContentSeriesSnapshot $run.Config $run.ContentSet $run.Target $protected (& $script:Clock)
}
function Get-ContentCleanupDecision($Snapshot){
    $rows=[object[]]$Snapshot.rows.ToArray()
    $decision=if($script:Run.Retirement){Get-SeriesRetirementDecision $Snapshot.seriesId $rows}else{Get-BuildRetentionDecision $Snapshot.seriesId $rows $script:KeepCount}
    if($decision.status -cne 'evaluated'){throw 'catalog-incomplete'}
    return $decision
}
# 系列の退役記録を読み、どちらの選別で動くかを決める（BuildCleanupの同名の手順と同じ規則）。記録が不明なら
# 'retirement-unknown'で止め、件数保持へ戻らない。retireは記録が無ければ系列lockの下で書く（系列directoryが無く
# lockを取れない場合は消すものが無いので記録だけを書く）。既に退役済みなら最初の記録を保つ。dry-runは書かない。
function Set-ContentCleanupMode($Result,[bool]$Retire,[string]$DecisionId,[bool]$Write){
    $run=$script:Run;$tokens=@($run.ContentSet,$run.Target)
    $state=Read-SeriesRetirement 'content' $run.Config.Data.repositoryId $tokens
    if($state.State -ceq 'none' -and $Retire -and $Write){$state=Write-SeriesRetirement 'content' $run.Config.Data.repositoryId $tokens $DecisionId (& $script:Clock)}
    if($state.State -ceq 'none' -and -not $Retire){return}
    $run.Retirement=$true;$Result.mode='retirement';$Result.keepCount=$null
    $Result.retirement=if($state.Record){[pscustomobject][ordered]@{retiredAt=$state.Record.retiredAt;decision=$state.Record.decision}}else{$null}
}
# revisionのpublish.lockを取り、手順1（再開なら該当手順）から試行の終了まで保持する。取れなければ
# collect/inspectが保持中なので飛ばす。lock fileは既存のものだけを開き、作らない（無ければcatalogと同じく
# 系列をblockedにする）。新しい試行は、lockの後に取り直したsnapshotと保護でなお候補の場合だけ始める
# （lockの前・dry-runの結果では始めない）。返値は試行の返値に加え revision-busy / not-candidate / blocked。
function Invoke-ContentRevisionAttempt([string]$Revision,$Pending){
    $run=$script:Run;$lock=$null
    try{
        try{$lock=Enter-ContentPublishLock (Get-ContentDirectory $run.Config.Data.repositoryId $run.ContentSet $run.Target $Revision) $false}
        catch{
            if($_.Exception.Message -ceq 'publish-in-progress'){return 'revision-busy'}
            if($_.Exception.Message -ceq 'publish-lock-missing'){$run.Blocked='catalog-incomplete';return 'blocked'}
            throw
        }
        if($script:StepHook){& $script:StepHook 'after-revision-lock'}
        if($Pending){return Invoke-ContentDeleteAttempt $Revision $Pending $true}
        $snapshot=Get-ContentCleanupSnapshot
        if($snapshot.status -ceq 'blocked'){$run.Blocked=$snapshot.reasonCode;return 'blocked'}
        if($snapshot.deleting.Count){return 'delete-pending'}
        if($Revision -cnotin @((Get-ContentCleanupDecision $snapshot).deleteCandidateBuildIds)){return 'not-candidate'}
        return Invoke-ContentDeleteAttempt $Revision $snapshot.revisions[$Revision] $false
    }catch{return 'delete-pending'}
    finally{if($lock){$lock.Dispose()}}
}
function Set-ContentCleanupClassification($Result,$Snapshot,$Decision){
    $list={param([object[]]$ids)[pscustomobject][ordered]@{count=$ids.Count;revisions=$ids}}
    $Result.excessCount=$Decision.excessCount
    $Result.retained=& $list @($Decision.retainedBuildIds)
    $Result.candidates=& $list @($Decision.deleteCandidateBuildIds)
    $Result.held=& $list @($Decision.heldBuilds|ForEach-Object buildId)
    $Result.protected=& $list @($Snapshot.protected.ToArray())
    $Result.deleting=& $list @($Snapshot.deleting|ForEach-Object revision)
    $Result.deleted=& $list @($Snapshot.deleted.ToArray())
}
# 結果は非秘密の固定fieldだけ。endpoint・ローカルpath・例外本文・SDK応答本文を出さない。
# status: passed（完了。within-limit・protected-over-limitを含む）/ partial（未解決・残りあり）/ blocked / failed
# 退役（$Retire、content retire）と退役済みの系列の結果は BuildCleanup と同じく mode・retirement を足し、
# keepCountをnullにする（passedの理由は retired／retired-held）。件数保持の結果のfieldは変えない。
function Invoke-ContentCleanup([string]$ConfigPath,[string]$ContentSet,[string]$Target,[string]$ArtifactsRoot,[bool]$DryRun,[bool]$Retire=$false,[string]$DecisionId=''){
    $empty=[pscustomobject][ordered]@{count=0;revisions=@()}
    $r=[ordered]@{status='failed';reasonCode='unconfirmed';seriesId=$null;keepCount=$script:KeepCount;excessCount=$null
        retained=$empty;candidates=$empty;held=$empty;protected=$empty;deleting=$empty;deleted=$empty
        remaining=0;outcomes=@();requests=[ordered]@{delete=0;get=0}}
    if($Retire){$r.mode='retirement';$r.keepCount=$null;$r.retirement=$null}
    $lock=$null;$outcomes=[Collections.Generic.List[object]]::new()
    $script:Run=[pscustomobject]@{Config=$null;ContentSet=$ContentSet;Target=$Target;ArtifactsRoot=$null;Timer=[Diagnostics.Stopwatch]::StartNew();Result=$r;DeleteCount=0;Generation=$null;Operation=$null;Blocked=$null;Retirement=$false}
    $blocked={param($code)$r.status='blocked';$r.reasonCode=$code;$r.outcomes=@($outcomes.ToArray());return [pscustomobject]$r}
    try{
        # 系列tokenと指示IDは何も読む前に検査する。保護源（ArtifactsRoot）を集められないなら何も読まない。
        Assert-ContentToken $ContentSet;Assert-ContentTargetToken $Target
        if($Retire){Assert-SeriesRetirementDecision $DecisionId}
        Import-ArtifactPackage
        $script:Run.ArtifactsRoot=Resolve-BuildArtifactsRoot $ArtifactsRoot ''
        $config=Read-ContentConfig $ConfigPath;$script:Run.Config=$config
        $r.seriesId=Get-ContentSeries $config.Data.repositoryId $ContentSet $Target
        if($DryRun){
            # lockを取らず、記録（退役記録を含む）にもnetworkにも触れない。実行時と同じcatalog・保護・policyで分類だけ返す。
            Set-ContentCleanupMode $r $Retire $DecisionId $false
            $snapshot=Get-ContentCleanupSnapshot
            if($snapshot.status -ceq 'blocked'){return & $blocked $snapshot.reasonCode}
            $decision=Get-ContentCleanupDecision $snapshot;Set-ContentCleanupClassification $r $snapshot $decision
            $r.remaining=$r.candidates.count;$r.status='passed';$r.reasonCode=$decision.reasonCode;return [pscustomobject]$r
        }
        $storeAbsent=$false
        try{$lock=Enter-ContentSeriesLock $config.Data.repositoryId $ContentSet $Target}
        catch{if($_.Exception.Message -ceq 'lock-busy'){throw 'cleanup-in-progress'};if($_.Exception.Message -cne 'content-store-unavailable'){throw};$storeAbsent=$true}
        if($script:StepHook){& $script:StepHook 'after-lock'}
        # 退役記録の確認・書込みはlockの後、catalogの読取より前。記録が確定するまで何も送らない。
        Set-ContentCleanupMode $r $Retire $DecisionId $true
        $excluded=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal);$stop=$null
        # lockの後に取り直したsnapshotだけを使う。lock前やdry-runの結果では判断しない。
        $snapshot=Get-ContentCleanupSnapshot
        if($snapshot.status -ceq 'blocked'){return & $blocked $snapshot.reasonCode}
        # 系列directoryが無くlockを取れなかった場合、何も無いことを確かめて終える。
        if($storeAbsent -and ($snapshot.rows.Count -or $snapshot.deleting.Count -or $snapshot.deleted.Count)){throw 'unconfirmed'}
        # 未解決の試行を新しい候補より先に、古い順で確定させる。1件でも確定できなければ以降は進めない。
        foreach($item in @($snapshot.deleting|Sort-Object -Property @{Expression={$_.publishedAt}},@{Expression={$_.revision}})){
            $outcome=Invoke-ContentRevisionAttempt $item.revision $item.info
            if($outcome -ceq 'blocked'){return & $blocked $script:Run.Blocked}
            $outcomes.Add([pscustomobject]@{revision=$item.revision;outcome=$outcome})
            if($outcome -cnotin @('deleted','abandoned')){$stop=$outcome;break}
        }
        while(-not $stop){
            # 1件ごとに取り直す。取下げで成功行に戻ったrevisionも含めた完全なsnapshotで計算し直す。
            $snapshot=Get-ContentCleanupSnapshot
            if($snapshot.status -ceq 'blocked'){return & $blocked $snapshot.reasonCode}
            if($snapshot.deleting.Count){$stop='delete-pending';break}
            $todo=@((Get-ContentCleanupDecision $snapshot).deleteCandidateBuildIds|Where-Object {-not $excluded.Contains($_)})
            if(-not $todo.Count){break}
            if($script:Run.DeleteCount -ge $script:DeleteLimit){$stop='delete-limit';break}
            $id=$todo[0]
            $outcome=Invoke-ContentRevisionAttempt $id $null
            if($outcome -ceq 'blocked'){return & $blocked $script:Run.Blocked}
            $outcomes.Add([pscustomobject]@{revision=$id;outcome=$outcome})
            if($outcome -ceq 'deleted'){continue}
            # 保持中のrevision・lock後に候補でなくなったrevisionは飛ばして次の候補へ。
            # DELETE後もtransport.jsonが残ったrevisionは、このrunでは再試行しない。
            if($outcome -cin @('revision-busy','not-candidate','delete-unconfirmed')){$null=$excluded.Add($id);continue}
            $stop=$outcome;break
        }
        $r.outcomes=@($outcomes.ToArray())
        $final=Get-ContentCleanupSnapshot
        if($final.status -ceq 'blocked'){return & $blocked $final.reasonCode}
        $decision=Get-ContentCleanupDecision $final;Set-ContentCleanupClassification $r $final $decision
        $r.remaining=$r.candidates.count
        $first=@($outcomes|Where-Object {$_.outcome -cin @('revision-busy','delete-unconfirmed')}|Select-Object -First 1)
        if($stop){$r.status='partial';$r.reasonCode=$stop}
        elseif($r.deleting.count -or $r.remaining){$r.status='partial';$r.reasonCode=if($first.Count){$first[0].outcome}else{'delete-pending'}}
        else{$r.status='passed';$r.reasonCode=$decision.reasonCode}
    }catch{
        $r.outcomes=@($outcomes.ToArray());$code=$_.Exception.Message
        if($code -cin @('invalid-content-identity','unsupported-series','artifacts-root-not-configured','invalid-artifacts-root','invalid-content-config','cleanup-in-progress','invalid-decision','retirement-unknown')){$r.reasonCode=$code}
        if($code -cin @('cleanup-in-progress','retirement-unknown')){$r.status='blocked'}
    }finally{if($lock){$lock.Dispose()};$script:Run=$null}
    return [pscustomobject]$r
}
Export-ModuleMember -Function Invoke-ContentCleanup
