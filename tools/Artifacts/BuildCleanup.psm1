Set-StrictMode -Version Latest
# fresh processのCLIでも同じ関数解決になるよう、使用するmoduleを直接importする。
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactApplication.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildContract.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildCatalog.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildRetentionPolicy.psm1')
Import-Module (Join-Path $PSScriptRoot 'Credentials/CredentialStore.psm1')
# Build系列1つの件数清掃（R2のobjectだけを消す。ローカルの記録・ArtifactsRoot・DIST cacheには触れない）。
# ここは順序だけを持つ。分類はBuildCatalog、選別はBuildRetentionPolicy、記録はBuildStoreが所有する。
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
# transportの返値を非秘密の固定fieldへ写す。操作名・generationの不一致や型・値域の外れた値は
# 「観測できなかった」へ倒して記録する。判定はこの記録から行うので、記録だけから再評価した
# Test-ArtifactSuccess/Test-ArtifactMissingの結果は、送信時の判定と常に一致する。
function ConvertTo-BuildObservation($Result,[string]$Operation,[string]$Key,[string]$Generation){
    $o=[ordered]@{operation=$Operation;key=$Key;generation=$Generation;httpStatus=$null;statusClass=$null;s3Code=$null;statusObserved=$false;timedOut=$false;redirected=$false;observedAt=(& $script:Clock).ToUniversalTime().ToString('o')}
    if($null -eq $Result){return $o}
    $value={param($n)$p=$Result.PSObject.Properties[$n];if($p){$p.Value}else{$null}}
    $http=& $value 'HttpStatus';$class=& $value 'StatusClass';$s3=& $value 'S3Code'
    $flags=@((& $value 'StatusObserved'),(& $value 'TimedOut'),(& $value 'Redirected'))
    if((& $value 'Operation') -cne $Operation -or (& $value 'Generation') -cne $Generation -or
        ($null -ne $http -and (($http -isnot [int] -and $http -isnot [long]) -or $http -lt 100 -or $http -gt 599)) -or
        ($null -ne $class -and $class -cnotin @('success','unauthorized','forbidden','not-found','too-many-requests','server-error','redirect','other','timeout','transport-error','invalid-input','size-limit')) -or
        ($null -ne $s3 -and ($s3 -isnot [string] -or $s3 -cnotmatch '\A[A-Za-z][A-Za-z0-9]{0,63}\z')) -or
        @($flags|Where-Object {$_ -isnot [bool]}).Count){return $o}
    $o.httpStatus=if($null -eq $http){$null}else{[long]$http};$o.statusClass=$class;$o.s3Code=$s3
    $o.statusObserved=$flags[0];$o.timedOut=$flags[1];$o.redirected=$flags[2]
    return $o
}
function ConvertFrom-BuildObservation($o){
    return [pscustomobject]@{Operation=$o.operation;Generation=$o.generation;HttpStatus=$o.httpStatus;StatusClass=$o.statusClass;S3Code=$o.s3Code;StatusObserved=$o.statusObserved;TimedOut=$o.timedOut;Redirected=$o.redirected}
}
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
    $decision=Get-BuildRetentionDecision $Snapshot.seriesId ([object[]]$Snapshot.rows.ToArray()) $script:KeepCount
    if($decision.status -cne 'evaluated'){throw 'catalog-incomplete'}
    return $decision
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
function Invoke-BuildCleanup([string]$ConfigPath,[string]$Project,[string]$Target,[string]$Configuration,[bool]$DryRun){
    $empty=[pscustomobject][ordered]@{count=0;buildIds=@()}
    $r=[ordered]@{status='failed';reasonCode='unconfirmed';seriesId=$null;keepCount=$script:KeepCount;excessCount=$null
        retained=$empty;candidates=$empty;held=[pscustomobject][ordered]@{count=0;builds=@()};deleting=$empty;deleted=$empty
        remaining=0;foreignInvalid=0;outcomes=@();requests=[ordered]@{delete=0;get=0}}
    $lock=$null;$outcomes=[Collections.Generic.List[object]]::new()
    $script:Run=[pscustomobject]@{Config=$null;Project=$Project;Target=$Target;Configuration=$Configuration;Timer=[Diagnostics.Stopwatch]::StartNew();Result=$r;DeleteCount=0;Generation=$null;Operation=$null}
    try{
        # 系列tokenは何も読む前に検査する。
        foreach($token in @($Project,$Target,$Configuration)){Assert-BuildToken $token}
        Import-ArtifactPackage
        $config=Read-BuildConfig $ConfigPath;$script:Run.Config=$config
        $r.seriesId=Get-BuildSeries $config.Data.repositoryId $Project $Target $Configuration
        if($DryRun){
            # lockを取らず、記録にもnetworkにも触れない。実行時と同じcatalogとpolicyで分類だけ返す。
            $snapshot=Get-BuildCleanupSnapshot
            if($snapshot.status -ceq 'blocked'){$r.status='blocked';$r.reasonCode=$snapshot.reasonCode;return [pscustomobject]$r}
            $decision=Get-BuildCleanupDecision $snapshot;Set-BuildCleanupClassification $r $snapshot $decision
            $r.remaining=$r.candidates.count;$r.status='passed';$r.reasonCode=$decision.reasonCode;return [pscustomobject]$r
        }
        $storeAbsent=$false
        try{$lock=Enter-BuildSeriesLock $config.Data.repositoryId $Project $Target $Configuration}
        catch{if($_.Exception.Message -ceq 'lock-busy'){throw 'cleanup-in-progress'};if($_.Exception.Message -cne 'build-store-unavailable'){throw};$storeAbsent=$true}
        if($script:StepHook){& $script:StepHook 'after-lock'}
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
        if($code -cin @('invalid-build-identity','invalid-build-config','cleanup-in-progress')){$r.reasonCode=$code}
        if($code -ceq 'cleanup-in-progress'){$r.status='blocked'}
    }finally{if($lock){$lock.Dispose()};$script:Run=$null}
    return [pscustomobject]$r
}
Export-ModuleMember -Function Invoke-BuildCleanup
