param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    # catchがstdoutのreasonCodeへ通してよい固定code。schedule分岐だけが値を入れる。
    $publicFailures=@()
    # 仮想化されたAppDataでストアへ書くと、別の実体へ黙って分裂する。引数の解釈・
    # store moduleのimport・資格情報の読取りより前に判定し、判定不能も止める。
    try{Import-Module (Join-Path $PSScriptRoot 'Workflow/WindowsStoreContext.psm1');$storeContext=Get-OsmStoreContext}catch{$storeContext='unknown'}
    if($storeContext -cne 'not-virtualized'){[Console]::WriteLine((ConvertTo-Json -Compress -InputObject ([ordered]@{status='failed';reasonCode=$(if($storeContext -ceq 'virtualized'){'virtualized-store'}else{'store-context-unknown'})})));exit 1}
    if ($Arguments.Count -lt 4) { throw 'Invalid command.' }
    # BuildはEvidenceと独立したgrammar。成功記録の寿命と引数の意味を混同しない。
    # オプション名の字種を許しても、必要fieldの完全一致で未知引数を拒否する。
    if($Arguments[0] -ceq 'build'){
        if($Arguments.Count -lt 6 -or $Arguments[2] -cne '--profile' -or $Arguments[3] -cne 'osm'){throw 'unsupported-command'}
        $buildAction=$Arguments[1];$buildOpts=@{};$buildDry=$false
        for($j=2;$j -lt $Arguments.Count;$j+=2){
            # 値を取らない--dry-runはcleanupだけ。件数Nや削除対象を選ぶ引数は設けない。
            if($Arguments[$j] -ceq '--dry-run'){if($buildDry -or $buildAction -cne 'cleanup'){throw 'unsupported-command'};$buildDry=$true;$j--;continue}
            if($j+1 -ge $Arguments.Count -or $Arguments[$j] -cnotmatch '\A--[a-z0-9-]+\z' -or $buildOpts.ContainsKey($Arguments[$j])){throw 'unsupported-command'}
            $buildOpts[$Arguments[$j]]=$Arguments[$j+1]
        }
        $needed=switch($buildAction){
            'publish'{@('--profile','--config','--selection')}
            'fetch'{@('--profile','--config','--reference','--sha256')}
            'inspect'{@('--profile','--config','--build-id')}
            # --artifacts-rootだけは省略でき、省略時はOSM_ARTIFACTS_ROOTを読む（既定値は無い）。
            'collect'{if($buildOpts.ContainsKey('--artifacts-root')){@('--profile','--config','--source','--artifacts-root')}else{@('--profile','--config','--source')}}
            'cleanup'{@('--profile','--config','--project','--target','--configuration')}
            'use'{@('--profile','--config','--build-id','--consumer','--until','--reason')}
            'release'{@('--profile','--config','--build-id','--protection')}
            default{throw 'unsupported-command'}
        }
        if((($buildOpts.Keys|Sort-Object) -join ',') -cne (($needed|Sort-Object) -join ',')){throw 'unsupported-command'}
        if($buildAction -ceq 'cleanup'){Import-Module (Join-Path $PSScriptRoot 'Artifacts/BuildCleanup.psm1')}
        else{Import-Module (Join-Path $PSScriptRoot 'Artifacts/BuildApplication.psm1')}
        # inspect/collectは、削除状態を確認した共有access lockの保持中に結果を出力する。
        $printed=@{done=$false}
        $emit={param($o)[Console]::WriteLine((ConvertTo-Json -InputObject $o -Compress -Depth 20));$printed.done=$true}.GetNewClosure()
        $outcome=switch($buildAction){
            'publish'{Invoke-BuildPublish $buildOpts['--config'] $buildOpts['--selection']}
            'fetch'{Invoke-BuildFetch $buildOpts['--config'] $buildOpts['--reference'] $buildOpts['--sha256']}
            'inspect'{Invoke-BuildInspect $buildOpts['--config'] $buildOpts['--build-id'] $emit}
            'collect'{Invoke-BuildCollect $buildOpts['--config'] $buildOpts['--source'] $buildOpts['--artifacts-root'] $emit}
            'cleanup'{Invoke-BuildCleanup $buildOpts['--config'] $buildOpts['--project'] $buildOpts['--target'] $buildOpts['--configuration'] $buildDry}
            'use'{Invoke-BuildUse $buildOpts['--config'] $buildOpts['--build-id'] $buildOpts['--consumer'] $buildOpts['--until'] $buildOpts['--reason']}
            'release'{Invoke-BuildRelease $buildOpts['--config'] $buildOpts['--build-id'] $buildOpts['--protection']}
        }
        if(-not $printed.done){[Console]::WriteLine((ConvertTo-Json -InputObject $outcome -Compress -Depth 20))}
        # cleanupのpartial（未解決の試行・残りの候補あり）は2。次回の実行で続きを処理する。
        if($outcome.status -ceq 'passed'){exit 0};if($outcome.status -ceq 'partial'){exit 2};exit 1
    }
    # ContentもBuild/Evidenceと独立したgrammar。保存単位（revision丸ごと・file単位object）の意味を混同しない。
    if($Arguments[0] -ceq 'content'){
        if($Arguments.Count -lt 6 -or $Arguments[2] -cne '--profile' -or $Arguments[3] -cne 'osm'){throw 'unsupported-command'}
        $contentAction=$Arguments[1];$contentOpts=@{};$contentDry=$false
        for($j=2;$j -lt $Arguments.Count;$j+=2){
            # 値を取らない--dry-runはcleanupだけ。件数Nや削除対象を選ぶ引数は設けない。
            if($Arguments[$j] -ceq '--dry-run'){if($contentDry -or $contentAction -cne 'cleanup'){throw 'unsupported-command'};$contentDry=$true;$j--;continue}
            if($j+1 -ge $Arguments.Count -or $Arguments[$j] -cnotmatch '\A--[a-z0-9-]+\z' -or $contentOpts.ContainsKey($Arguments[$j])){throw 'unsupported-command'}
            $contentOpts[$Arguments[$j]]=$Arguments[$j+1]
        }
        $needed=switch($contentAction){
            'fetch'{@('--profile','--config','--reference','--sha256')}
            'inspect'{@('--profile','--config','--content-set','--target','--revision')}
            # --artifacts-rootだけは省略でき、省略時はOSM_ARTIFACTS_ROOTを読む（既定値は無い）。
            'collect'{if($contentOpts.ContainsKey('--artifacts-root')){@('--profile','--config','--source','--artifacts-root')}else{@('--profile','--config','--source')}}
            # cleanupも保護源のArtifactsRootを同じ規則で受ける（既定値は無く、無ければ何も読まない）。
            'cleanup'{if($contentOpts.ContainsKey('--artifacts-root')){@('--profile','--config','--content-set','--target','--artifacts-root')}else{@('--profile','--config','--content-set','--target')}}
            default{throw 'unsupported-command'}
        }
        if((($contentOpts.Keys|Sort-Object) -join ',') -cne (($needed|Sort-Object) -join ',')){throw 'unsupported-command'}
        if($contentAction -ceq 'cleanup'){Import-Module (Join-Path $PSScriptRoot 'Artifacts/ContentCleanup.psm1')}
        else{Import-Module (Join-Path $PSScriptRoot 'Artifacts/ContentApplication.psm1')}
        $outcome=switch($contentAction){
            'fetch'{Invoke-ContentFetch $contentOpts['--config'] $contentOpts['--reference'] $contentOpts['--sha256']}
            'inspect'{Invoke-ContentInspect $contentOpts['--config'] $contentOpts['--content-set'] $contentOpts['--target'] $contentOpts['--revision']}
            'collect'{Invoke-ContentCollect $contentOpts['--config'] $contentOpts['--source'] $contentOpts['--artifacts-root']}
            'cleanup'{Invoke-ContentCleanup $contentOpts['--config'] $contentOpts['--content-set'] $contentOpts['--target'] $contentOpts['--artifacts-root'] $contentDry}
        }
        [Console]::WriteLine((ConvertTo-Json -InputObject $outcome -Compress -Depth 20))
        # cleanupのpartial（未解決の試行・残りの候補あり）は2。次回の実行で続きを処理する。
        if($outcome.status -ceq 'passed'){exit 0};if($outcome.status -ceq 'partial'){exit 2};exit 1
    }
    # 分裂したストアの合流もBuild/Content/Evidenceと独立したgrammar。planは両ストアを変更せず
    # manifest候補を新規fileへ書く。consolidateはownerが承認したmanifestのsha256を必須にする。
    if($Arguments[0] -ceq 'store'){
        $storeAction=$Arguments[1];$storeOpts=@{};$storeDry=$false
        for($j=2;$j -lt $Arguments.Count;$j+=2){
            if($Arguments[$j] -ceq '--dry-run'){if($storeDry -or $storeAction -cne 'consolidate'){throw 'unsupported-command'};$storeDry=$true;$j--;continue}
            if($j+1 -ge $Arguments.Count -or $Arguments[$j] -cnotmatch '\A--[a-z0-9-]+\z' -or $storeOpts.ContainsKey($Arguments[$j])){throw 'unsupported-command'}
            $storeOpts[$Arguments[$j]]=$Arguments[$j+1]
        }
        $needed=switch($storeAction){'plan'{@('--source-root','--output')}'consolidate'{@('--manifest','--manifest-sha256')}default{throw 'unsupported-command'}}
        if((($storeOpts.Keys|Sort-Object) -join ',') -cne (($needed|Sort-Object) -join ',')){throw 'unsupported-command'}
        Import-Module (Join-Path $PSScriptRoot 'Artifacts/StoreConsolidation.psm1')
        $outcome=if($storeAction -ceq 'plan'){Invoke-StoreConsolidationPlan $storeOpts['--source-root'] $storeOpts['--output']}else{Invoke-StoreConsolidation $storeOpts['--manifest'] $storeOpts['--manifest-sha256'] $storeDry}
        [Console]::WriteLine((ConvertTo-Json -InputObject $outcome -Compress -Depth 20))
        # 保留・変更検出の単位が残るpartialは2。全体完了を返さない。
        if($outcome.status -ceq 'passed'){exit 0};if($outcome.status -ceq 'partial'){exit 2};exit 1
    }
    if ($Arguments[0] -ceq 'credentials' -and $Arguments[1] -cin @('set','status','remove')) {
        # Existing local credential grammar and exit meanings remain unchanged.
        if ($Arguments[2] -cne '--profile' -or $Arguments[3] -cne 'osm') { throw 'Invalid command.' }
        $action = $Arguments[1]
        $replace = $false
        if ($action -eq 'set') {
            if ($Arguments.Count -eq 5 -and $Arguments[4] -ceq '--replace') { $replace = $true }
            elseif ($Arguments.Count -ne 4) { throw 'Invalid command.' }
        } elseif ($Arguments.Count -ne 4) { throw 'Invalid command.' }
        Import-Module (Join-Path $PSScriptRoot 'Artifacts/Credentials/CredentialCommands.psm1') -Force
        switch ($action) {
            'set' {
                $outcome = Invoke-CredentialSet 'osm' $replace
                if ($outcome.Outcome -eq 'committed with cleanup pending') {
                    [Console]::Error.WriteLine('Committed locally; cleanup pending. R2 connectivity unverified.')
                    exit 2
                }
                [Console]::WriteLine('Credential committed locally; R2 connectivity unverified.')
            }
            'status' { Invoke-CredentialStatus 'osm' | ForEach-Object { [Console]::WriteLine($_) } }
            'remove' { [Console]::WriteLine((Invoke-CredentialRemove 'osm')) }
        }
        exit 0
    }
    $command=$Arguments[0];$action=if($command -ceq 'evidence'){$Arguments[1]}else{$command}
    $offset=if($command -cin @('evidence','credentials')){2}else{1}
    if($action -ceq 'schedule'){$action='schedule-'+$Arguments[2];$offset=3}
    $opts=@{};$dry=$false
    for($i=$offset;$i -lt $Arguments.Count;$i++){
        $flag=$Arguments[$i]
        if($flag -ceq '--dry-run'){if($dry){throw 'unsupported-command'};$dry=$true;continue}
        if($flag -cnotmatch '\A--[a-z0-9-]+\z' -or $opts.ContainsKey($flag) -or $i+1 -ge $Arguments.Count){throw 'unsupported-command'}
        $opts[$flag]=$Arguments[++$i]
    }
    if($command -ceq 'credentials'){
        # Rotation grammar keeps the existing credential semantics.
        Import-Module (Join-Path $PSScriptRoot 'Artifacts/Credentials/CredentialRotation.psm1') -Force
        if($Arguments[1] -ceq 'rotate' -and $Arguments.Count -eq 6 -and $Arguments[2] -ceq '--profile' -and $Arguments[3] -ceq 'osm' -and $Arguments[4] -ceq '--config'){$outcome=Invoke-CredentialRotation $Arguments[5]}
        elseif($Arguments[1] -ceq 'confirm-revocation' -and $Arguments.Count -eq 10 -and $Arguments[2] -ceq '--profile' -and $Arguments[3] -ceq 'osm' -and $Arguments[4] -ceq '--config' -and $Arguments[6] -ceq '--generation' -and $Arguments[8] -ceq '--evidence'){$outcome=Invoke-CredentialRevocationConfirmation $Arguments[5] $Arguments[7] $Arguments[9]}
        else{throw 'unsupported-command'}
    }else{
        if($opts['--profile'] -cne 'osm'){throw 'unsupported-command'}
        $expected=switch($action){
            # stageはpublishと同じselectionを受け、ローカル固定で止まる（送信しない）。
            {$_ -cin @('publish','stage')}{if($command -cne 'evidence'){throw 'unsupported-command'};@('--profile','--config','--selection')}
            'fetch'{@('--profile','--config','--reference','--sha256')}
            'inspect'{@('--profile','--config','--task')}
            'use'{@('--profile','--config','--reference','--consumer','--until','--reason')}
            'release'{@('--profile','--config','--reference','--protection')}
            {$_ -cin @('cleanup','schedule-install','schedule-status')}{@('--profile','--config')}
            'reset'{@('--profile','--manifest','--manifest-sha256','--owner-verification','--owner-verification-sha256')}
            default{throw 'unsupported-command'}
        }
        if((($opts.Keys|Sort-Object) -join ',') -cne (($expected|Sort-Object) -join ',') -or ($dry -and $action -cnotin @('cleanup','reset'))){throw 'unsupported-command'}
        Import-Module (Join-Path $PSScriptRoot 'Artifacts/EvidenceApplication.psm1')
        # scheduleは既存のOS taskを修復・上書きしない設計なので、ownerが原因（identity不一致・
        # 無効化・未登録・config/runtime不一致・未build）を読めなければ復旧できない。module が
        # throwする固定codeのうちREADMEに意味を書いた9件だけを通す。config読取の失敗は従来どおり伏せる。
        if($action -cin @('schedule-install','schedule-status')){$publicFailures=@('scheduler-identity-mismatch','scheduler-disabled','scheduler-not-installed','scheduler-query-failed','scheduler-runtime-missing','runtime-source-uncommitted','runtime-identity-mismatch','runtime-hash-mismatch','runtime-config-mismatch')}
        switch($action){
            'publish'{$outcome=Invoke-EvidencePublish $opts['--config'] $opts['--selection']}
            'stage'{$outcome=Invoke-EvidenceStage $opts['--config'] $opts['--selection']}
            'fetch'{$outcome=Invoke-EvidenceFetch $opts['--config'] $opts['--reference'] $opts['--sha256']}
            'inspect'{$outcome=Invoke-EvidenceInspect $opts['--config'] $opts['--task']}
            'use'{$outcome=Invoke-EvidenceUse $opts['--config'] $opts['--reference'] $opts['--consumer'] $opts['--until'] $opts['--reason']}
            'release'{$outcome=Invoke-EvidenceRelease $opts['--config'] $opts['--reference'] $opts['--protection']}
            'cleanup'{Import-Module (Join-Path $PSScriptRoot 'Artifacts/EvidenceCleanup.psm1');$outcome=Invoke-EvidenceCleanup $opts['--config'] $dry}
            'schedule-install'{Import-Module (Join-Path $PSScriptRoot 'Artifacts/EvidenceSchedule.psm1');$outcome=Invoke-EvidenceScheduleInstall $opts['--config']}
            'schedule-status'{Import-Module (Join-Path $PSScriptRoot 'Artifacts/EvidenceSchedule.psm1');$outcome=Get-EvidenceScheduleStatus $opts['--config']}
            'reset'{Import-Module (Join-Path $PSScriptRoot 'Artifacts/EvidenceReset.psm1');$outcome=Invoke-EvidenceReset $opts['--manifest'] $opts['--manifest-sha256'] $opts['--owner-verification'] $opts['--owner-verification-sha256'] 'osm' $dry}
        }
    }
    [Console]::WriteLine((ConvertTo-Json -InputObject $outcome -Compress -Depth 20))
    if ($outcome.status -ceq 'passed') { exit 0 }
    if ($outcome.status -ceq 'pending') { exit 2 }
    exit 1
} catch {
    # 例外文字列・stack trace には秘密やパスが混ざり得るため、一般診断だけ表示する。
    # 通すのはallow-listの固定codeに完全一致（大文字小文字を区別）する場合だけで、部分一致で通さない。
    if($_.Exception.Message -ceq 'unsupported-command'){[Console]::WriteLine('{"status":"failed","reasonCode":"unsupported-command"}')}
    elseif($_.Exception.Message -cin $publicFailures){[Console]::WriteLine((ConvertTo-Json -Compress -InputObject ([ordered]@{status='failed';reasonCode=$_.Exception.Message})))}
    else{[Console]::Error.WriteLine('Artifact operation failed.')}
    exit 1
}
