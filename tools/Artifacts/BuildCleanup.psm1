Set-StrictMode -Version Latest
# fresh processのCLIでも同じ関数解決になるよう、使用するmoduleを直接importする。
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactApplication.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactDeletion.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildContract.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildCatalog.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildRetentionPolicy.psm1')
Import-Module (Join-Path $PSScriptRoot 'SeriesRetirementStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'Credentials/CredentialStore.psm1')
# Build系列1つの件数清掃（R2のobjectだけを消す。ローカルの記録・ArtifactsRoot・DIST cacheには触れない）。
# ここは順序だけを持つ。分類はBuildCatalog、選別はBuildRetentionPolicy、記録はBuildStoreが所有する。
# 系列の退役（build retire）と退役済みの系列もこの順序で動き、違いは退役記録の確認・書込みと選別だけ
# （lock・取り直し・再開・予算・集計を別のorchestratorで二重に持たない）。退役記録はSeriesRetirementStoreが所有する。
# 消し損ねは許し、消し過ぎは許さない: 判定できない・確認できないときは試行を未解決のまま残し、
# 次回の実行がGETから確定させる。削除済みの判定はGETの404 NoSuchKeyだけに依る。
#
# 注入点はoffline検証専用で、公開CLIから時計・通信・N・予算を差し替えさせない。
$script:Clock={[DateTimeOffset]::UtcNow}
$script:KeepCount=10        # 系列ごとのN。本スライスでは定数で、CLIやbuild-configからは変えない。
$script:DeleteLimit=20      # 1回の実行で送るDELETEの総数（取下げ後の新しい試行を含む）。
$script:BudgetMilliseconds=600000
$script:NetworkHook=$null
$script:BudgetHook=$null
$script:StepHook=$null
$script:Run=$null

function Assert-BuildCleanupBudget([string]$Point){
    if($script:BudgetHook){& $script:BudgetHook $Point}
    if($script:Run.Timer.ElapsedMilliseconds -ge $script:BudgetMilliseconds){throw 'budget-exceeded'}
}
function Invoke-BuildCleanupNetwork([string]$Generation,[hashtable]$Request){
    if($script:NetworkHook){return & $script:NetworkHook $Generation $Request}
    return Invoke-ArtifactNetwork $Generation $Request
}
# 観測の写像は ArtifactDeletion が所有する（Contentの件数清掃と同じ形）。ここは注入時計で観測時刻を
# 渡すだけ。ConvertFrom-BuildObservation は記録からの再評価を検証するテストがこの名前で呼ぶため残す。
function ConvertTo-BuildObservation($Result,[string]$Operation,[string]$Key,[string]$Generation){
    return ConvertTo-ArtifactObservation $Result $Operation $Key $Generation ((& $script:Clock).ToUniversalTime().ToString('o'))
}
function ConvertFrom-BuildObservation($o){return ConvertFrom-ArtifactObservation $o}
function Get-BuildCleanupGeneration {
    if(-not $script:Run.Generation){$script:Run.Generation=(Get-CredentialStatus 'osm').Generation}
    return $script:Run.Generation
}
# 1 Buildの1試行。手順と記録は HANDOFF の「削除試行の状態遷移」表に従う:
# 1 intent → 2 DELETE（試行ごとに高々1回）→ 3 sent → 4 確認GET → 5 deleted / 6 abandoned。
# Resumeは未解決の試行を手順4から続ける。access lockは手順1（再開なら手順4）から試行の終了まで持ち、
# この PC の fetch/inspect/collect と重ならないようにする。
# 返値: deleted / abandoned / delete-unconfirmed / delete-pending / budget-exceeded / in-transfer
function Invoke-BuildDeleteAttempt([string]$BuildId,$Build,[bool]$Resume){
    $run=$script:Run;$access=$null
    try{
        try{$access=Enter-BuildAccessLock $run.Config.Data.repositoryId $BuildId $true}catch{if($_.Exception.Message -ceq 'lock-busy'){return 'in-transfer'};throw}
        $dir=Get-BuildDirectory $run.Config.Data.repositoryId $BuildId
        $generation=Get-BuildCleanupGeneration
        if($Resume){$n=$Build.pending.Attempt;$intentSha=$Build.pending.IntentSha256;$sentSha=$Build.pending.SentSha256}
        else{
            $n=$Build.next
            $intent=[ordered]@{schemaVersion=1;attempt=$n;buildId=$BuildId;key=$Build.key;packageSha256=$Build.packageSha256;intentAt=(& $script:Clock).ToUniversalTime().ToString('o')}
            $intentSha=(Write-BuildImmutable $dir "deletion/$n.intent.json" $intent {Assert-BuildCleanupBudget 'intent'}).Sha256
            $sentSha=$null
            Assert-BuildCleanupBudget 'delete'
            $request=New-ArtifactRequest $run.Config 'delete' $Build.key $null $null $run.Timer
            $request.BuildRetentionDelete=$true
            # 送信を試みた時点で数える。応答が失われても送った可能性があり、同じrunで再送しない。
            $run.Result.requests.delete++;$run.DeleteCount++
            $response=$null;try{$response=Invoke-BuildCleanupNetwork $generation $request}catch{return 'delete-pending'}
            $observation=ConvertTo-BuildObservation $response 'delete' $Build.key $generation
            $sentSha=(Write-BuildImmutable $dir "deletion/$n.sent.json" ([ordered]@{schemaVersion=1;attempt=$n;intentSha256=$intentSha;observation=$observation}) {Assert-BuildCleanupBudget 'sent'}).Sha256
            # 検証済み2xx以外（403・429・5xx・timeout・redirect・generation変化を含む）は確認へ進まない。
            if(-not (Test-ArtifactSuccess (ConvertFrom-BuildObservation $observation) 'delete' $generation)){return 'delete-pending'}
        }
        Assert-BuildCleanupBudget 'get'
        # transportのGETは存在しないdestinationを要求する。objectが残っていた場合の取得先は
        # 新規のprivate operationで、ここでは読まず、清掃もしない（build fetchの残置と同じ扱い）。
        if(-not $run.Operation){$run.Operation=New-ArtifactOperation}
        $request=New-ArtifactRequest $run.Config 'get' $Build.key $null ([IO.Path]::Combine($run.Operation,'absence-'+[Guid]::NewGuid().ToString('N')+'.bin')) $run.Timer
        $run.Result.requests.get++
        $response=$null;try{$response=Invoke-BuildCleanupNetwork $generation $request}catch{return 'delete-pending'}
        $observation=ConvertTo-BuildObservation $response 'get' $Build.key $generation
        $seen=ConvertFrom-BuildObservation $observation
        if(Test-ArtifactMissing $seen $generation){
            $mark=if($sentSha){'recorded'}else{'unknown'}
            $null=Write-BuildImmutable $dir "deletion/$n.deleted.json" ([ordered]@{schemaVersion=1;attempt=$n;intentSha256=$intentSha;sentSha256=$sentSha;deleteObservation=$mark;getObservation=$observation}) {Assert-BuildCleanupBudget 'deleted'}
            return 'deleted'
        }
        if(Test-ArtifactSuccess $seen 'get' $generation){
            # objectが残っている。試行を閉じて成功行へ戻す。DELETE 2xxの直後に残った場合は、
            # 同じrunでは同じBuildに新しい試行を始めない（呼出側が除外する）。
            $null=Write-BuildImmutable $dir "deletion/$n.abandoned.json" ([ordered]@{schemaVersion=1;attempt=$n;intentSha256=$intentSha;getObservation=$observation}) {Assert-BuildCleanupBudget 'abandoned'}
            if($Resume){return 'abandoned'};return 'delete-unconfirmed'
        }
        return 'delete-pending'
    }catch{
        if($_.Exception.Message -cin @('budget-exceeded','Artifact operation expired.')){return 'budget-exceeded'}
        # intent確定後の記録の書込み失敗を含む。確定した記録だけが次回の再開点になる。
        return 'delete-pending'
    }finally{if($access){$access.Dispose()}}
}
function Get-BuildCleanupSnapshot {
    $run=$script:Run
    return Get-BuildSeriesSnapshot $run.Config $run.Project $run.Target $run.Configuration (& $script:Clock)
}
function Get-BuildCleanupDecision($Snapshot){
    $rows=[object[]]$Snapshot.rows.ToArray()
    $decision=if($script:Run.Retirement){Get-SeriesRetirementDecision $Snapshot.seriesId $rows}else{Get-BuildRetentionDecision $Snapshot.seriesId $rows $script:KeepCount}
    if($decision.status -cne 'evaluated'){throw 'catalog-incomplete'}
    return $decision
}
# 系列の退役記録を読み、どちらの選別で動くかを決める（retireは記録が無ければここで書く）。
# 退役済みの系列は、build cleanupでも件数保持ではなく退役の選別で動く（どちらを打っても同じ結果にする）。
# 記録が不明なら'retirement-unknown'の例外で止め、件数保持の選別へ戻らない（読めない記録を「退役していない」
# と扱うと、退役済み系列のBuildをN件残し続ける・退役の確定前に消す、のどちらにもなりうるため）。
# 書くのは系列lockの下（repoが無くlockを取れない場合は消すものが無いので、記録だけを書く）。既に退役済みなら
# 書かず、最初の記録のretiredAt・decisionをそのまま結果に出す。$Writeが偽（dry-run）なら何も書かない。
function Set-BuildCleanupMode($Result,[bool]$Retire,[string]$DecisionId,[bool]$Write){
    $run=$script:Run;$tokens=@($run.Project,$run.Target,$run.Configuration)
    $state=Read-SeriesRetirement 'build' $run.Config.Data.repositoryId $tokens
    if($state.State -ceq 'none' -and $Retire -and $Write){$state=Write-SeriesRetirement 'build' $run.Config.Data.repositoryId $tokens $DecisionId (& $script:Clock)}
    if($state.State -ceq 'none' -and -not $Retire){return}
    $run.Retirement=$true;$Result.mode='retirement';$Result.keepCount=$null
    $Result.retirement=if($state.Record){[pscustomobject][ordered]@{retiredAt=$state.Record.retiredAt;decision=$state.Record.decision}}else{$null}
}
function Set-BuildCleanupClassification($Result,$Snapshot,$Decision){
    $list={param([object[]]$ids)[pscustomobject][ordered]@{count=$ids.Count;buildIds=$ids}}
    $Result.excessCount=$Decision.excessCount
    $Result.retained=& $list @($Decision.retainedBuildIds)
    $Result.candidates=& $list @($Decision.deleteCandidateBuildIds)
    $held=@($Decision.heldBuilds)+@($Snapshot.held.ToArray())
    $Result.held=[pscustomobject][ordered]@{count=$held.Count;builds=@($held)}
    $Result.deleting=& $list @($Snapshot.deleting|ForEach-Object buildId)
    $Result.deleted=& $list @($Snapshot.deleted.ToArray())
    $Result.foreignInvalid=$Snapshot.foreignInvalid
}
# 結果は非秘密の固定fieldだけ。endpoint・ローカルpath・例外本文・SDK応答本文を出さない。
# status: passed（完了。within-limit・protected-over-limitを含む）/ partial（未解決・残りあり）/ blocked / failed
# 退役（$Retire、build retire）と退役済みの系列では、結果に mode='retirement' と retirement（retiredAt・decision）を
# 足し、keepCountをnullにする。passedの理由は retired（全件削除済み）／retired-held（保護中・未完了が残る）。
# 件数保持の結果のfieldは変えない（modeを出さないことが件数保持を表す）。
function Invoke-BuildCleanup([string]$ConfigPath,[string]$Project,[string]$Target,[string]$Configuration,[bool]$DryRun,[bool]$Retire=$false,[string]$DecisionId=''){
    $empty=[pscustomobject][ordered]@{count=0;buildIds=@()}
    $r=[ordered]@{status='failed';reasonCode='unconfirmed';seriesId=$null;keepCount=$script:KeepCount;excessCount=$null
        retained=$empty;candidates=$empty;held=[pscustomobject][ordered]@{count=0;builds=@()};deleting=$empty;deleted=$empty
        remaining=0;foreignInvalid=0;outcomes=@();requests=[ordered]@{delete=0;get=0}}
    if($Retire){$r.mode='retirement';$r.keepCount=$null;$r.retirement=$null}
    $lock=$null;$outcomes=[Collections.Generic.List[object]]::new()
    $script:Run=[pscustomobject]@{Config=$null;Project=$Project;Target=$Target;Configuration=$Configuration;Timer=[Diagnostics.Stopwatch]::StartNew();Result=$r;DeleteCount=0;Generation=$null;Operation=$null;Retirement=$false}
    try{
        # 系列tokenと指示IDは何も読む前に検査する。
        foreach($token in @($Project,$Target,$Configuration)){Assert-BuildToken $token}
        if($Retire){Assert-SeriesRetirementDecision $DecisionId}
        Import-ArtifactPackage
        $config=Read-BuildConfig $ConfigPath;$script:Run.Config=$config
        $r.seriesId=Get-BuildSeries $config.Data.repositoryId $Project $Target $Configuration
        if($DryRun){
            # lockを取らず、記録（退役記録を含む）にもnetworkにも触れない。実行時と同じcatalogとpolicyで分類だけ返す。
            Set-BuildCleanupMode $r $Retire $DecisionId $false
            $snapshot=Get-BuildCleanupSnapshot
            if($snapshot.status -ceq 'blocked'){$r.status='blocked';$r.reasonCode=$snapshot.reasonCode;return [pscustomobject]$r}
            $decision=Get-BuildCleanupDecision $snapshot;Set-BuildCleanupClassification $r $snapshot $decision
            $r.remaining=$r.candidates.count;$r.status='passed';$r.reasonCode=$decision.reasonCode;return [pscustomobject]$r
        }
        $storeAbsent=$false
        try{$lock=Enter-BuildSeriesLock $config.Data.repositoryId $Project $Target $Configuration}
        catch{if($_.Exception.Message -ceq 'lock-busy'){throw 'cleanup-in-progress'};if($_.Exception.Message -cne 'build-store-unavailable'){throw};$storeAbsent=$true}
        if($script:StepHook){& $script:StepHook 'after-lock'}
        # 退役記録の確認・書込みはlockの後、catalogの読取より前。記録が確定するまで何も送らない。
        Set-BuildCleanupMode $r $Retire $DecisionId $true
        $excluded=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal);$stop=$null
        # lockの後に取り直したsnapshotだけを使う。lock前やdry-runの結果では判断しない。
        $snapshot=Get-BuildCleanupSnapshot
        if($snapshot.status -ceq 'blocked'){$r.status='blocked';$r.reasonCode=$snapshot.reasonCode;return [pscustomobject]$r}
        # repoが無くlockを取れなかった場合、何も無いことを確かめて終える。
        if($storeAbsent -and ($snapshot.rows.Count -or $snapshot.deleting.Count -or $snapshot.deleted.Count -or $snapshot.held.Count)){throw 'unconfirmed'}
        # 未解決の試行を新しい候補より先に、古い順で確定させる。1件でも確定できなければ以降は進めない。
        foreach($build in @($snapshot.deleting|Sort-Object -Property @{Expression={$_.publishedAt}},@{Expression={$_.buildId}})){
            $outcome=Invoke-BuildDeleteAttempt $build.buildId $build $true
            $outcomes.Add([pscustomobject]@{buildId=$build.buildId;outcome=$outcome})
            if($outcome -cnotin @('deleted','abandoned')){$stop=$outcome;break}
        }
        while(-not $stop){
            # 1件ごとに取り直す。取下げで成功行に戻ったBuildも含めた完全なsnapshotで計算し直す。
            $snapshot=Get-BuildCleanupSnapshot
            if($snapshot.status -ceq 'blocked'){$r.status='blocked';$r.reasonCode=$snapshot.reasonCode;$r.outcomes=@($outcomes.ToArray());return [pscustomobject]$r}
            if($snapshot.deleting.Count){$stop='delete-pending';break}
            $decision=Get-BuildCleanupDecision $snapshot
            $todo=@($decision.deleteCandidateBuildIds|Where-Object {-not $excluded.Contains($_)})
            if(-not $todo.Count){break}
            if($script:Run.DeleteCount -ge $script:DeleteLimit){$stop='delete-limit';break}
            $id=$todo[0]
            $outcome=Invoke-BuildDeleteAttempt $id $snapshot.builds[$id] $false
            $outcomes.Add([pscustomobject]@{buildId=$id;outcome=$outcome})
            if($outcome -ceq 'deleted'){continue}
            # 取得中のBuildは飛ばして次の候補へ。DELETE後もobjectが残ったBuildは、このrunでは再試行しない。
            if($outcome -cin @('in-transfer','delete-unconfirmed')){$null=$excluded.Add($id);continue}
            $stop=$outcome;break
        }
        $r.outcomes=@($outcomes.ToArray())
        $final=Get-BuildCleanupSnapshot
        if($final.status -ceq 'blocked'){$r.status='blocked';$r.reasonCode=$final.reasonCode;return [pscustomobject]$r}
        $decision=Get-BuildCleanupDecision $final;Set-BuildCleanupClassification $r $final $decision
        $r.remaining=$r.candidates.count
        $first=@($outcomes|Where-Object {$_.outcome -cin @('in-transfer','delete-unconfirmed')}|Select-Object -First 1)
        if($stop){$r.status='partial';$r.reasonCode=$stop}
        elseif($r.deleting.count -or $r.remaining){$r.status='partial';$r.reasonCode=if($first.Count){$first[0].outcome}else{'delete-pending'}}
        else{$r.status='passed';$r.reasonCode=$decision.reasonCode}
    }catch{
        $r.outcomes=@($outcomes.ToArray());$code=$_.Exception.Message
        if($code -cin @('invalid-build-identity','invalid-build-config','cleanup-in-progress','invalid-decision','retirement-unknown')){$r.reasonCode=$code}
        if($code -cin @('cleanup-in-progress','retirement-unknown')){$r.status='blocked'}
    }finally{if($lock){$lock.Dispose()};$script:Run=$null}
    return [pscustomobject]$r
}
Export-ModuleMember -Function Invoke-BuildCleanup
