Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactDeletion.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentContract.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentStore.psm1')
# Contentの件数清掃の記録（revisionの deletion/ 配下の削除試行）と系列lockの所有者。
# publishの記録（receipt・intent・publish.lock、ContentStore）とは変更理由が別なので分けて持つ。
# 記録は追記だけで、既存のreceipt・intentを書き換えない。
#
# 1 revisionは複数objectなので、Buildの試行（intent → sent → deleted／abandoned）に確定点を足す:
#   <n>.intent.json    消すkeyの一覧（transport.jsonが先頭）と、receiptとの結び付き
#   <n>.sent.json      transport.jsonへのDELETEの観測
#   <n>.committed.json transport.jsonのGET 404（確定点。これより後はrevisionを取得できない）
#   <n>.abandoned.json transport.jsonのGET 2xx（確定点の前に限り、revisionを無傷で戻す）
#   <n>.deleted.json   確定点の後、残りの全keyのGET 404まで消し切った観測
$script:RecordPattern='\A([1-9][0-9]{0,5})\.(intent|sent|committed|abandoned|deleted)\.json\z'

function Write-ContentDeletionRecord([string]$Directory,[string]$Name,$Value,[scriptblock]$BeforeCommit=$null){
    if($Name -cnotmatch '\Adeletion/'){throw 'content-store-unavailable'}
    return Write-BuildImmutable $Directory $Name $Value $BeforeCommit 'content'
}
function Assert-ContentDeletionHeader($v,[string[]]$Fields,[int]$Attempt,[string]$IntentSha256){
    Assert-Fields $v $Fields
    if($v.schemaVersion -isnot [long] -or $v.schemaVersion -ne 1 -or $v.attempt -isnot [long] -or $v.attempt -ne $Attempt -or
        ($IntentSha256 -and $v.intentSha256 -cne $IntentSha256)){throw 'delete-record-invalid'}
}
# 状態はdeletion/の記録だけで決まり、content-configにもreceiptにも依存しない（inspect/collectがreceiptの
# 検査より先に削除状態を判定できるように）。receiptとの一致（manifest・intentのhash、keyの一覧）は呼出側が見る。
# 試行nは1からの連番。未解決・削除済みになれるのは最後の試行だけで、それより前は全て取下げ済み。
# 規則に合わない記録は補わず、削除状態を判定できないものとして例外にする。
# State: none（記録なし、または最後が取下げ）/ deleting（未解決。Pending.CommittedSha256があれば確定点の後）/ deleted
function Read-ContentDeletionState([string]$RepositoryId,[string]$ContentSet,[string]$TargetToken,[string]$Revision){
    $state=[pscustomobject]@{State='none';Next=1;ManifestSha256=$null;PublishIntentSha256=$null;Keys=$null;Pending=$null}
    try{
        $prefix="development/content/v1/$RepositoryId/$ContentSet/$TargetToken/$Revision/"
        $del=[IO.Path]::Combine((Get-ContentDirectory $RepositoryId $ContentSet $TargetToken $Revision),'deletion')
        Assert-NoArtifactReparse $del
        if([IO.File]::Exists($del)){throw 'delete-record-invalid'}
        if(-not [IO.Directory]::Exists($del)){return $state}
        Assert-ArtifactAcl $del $true
        if(@([IO.Directory]::EnumerateDirectories($del)).Count){throw 'delete-record-invalid'}
        $records=@{};$max=0
        foreach($file in [IO.Directory]::EnumerateFiles($del)){
            $name=[IO.Path]::GetFileName($file)
            # rename前に止まった書込みのtempは記録ではない（何も確定していない）。
            if($name -cmatch '\A[1-9][0-9]{0,5}\.(?:intent|sent|committed|abandoned|deleted)\.json\.[0-9a-f]{32}\.tmp\z'){continue}
            $m=[regex]::Match($name,$script:RecordPattern)
            if(-not $m.Success){throw 'delete-record-invalid'}
            Assert-ArtifactAcl $file $false
            $n=[int]$m.Groups[1].Value;if(-not $records.ContainsKey($n)){$records[$n]=@{}}
            $records[$n][$m.Groups[2].Value]=Read-ArtifactJson $file;$max=[Math]::Max($max,$n)
        }
        $keyText=$null
        for($n=1;$n -le $max;$n++){
            if(-not $records.ContainsKey($n) -or -not $records[$n].ContainsKey('intent')){throw 'delete-record-invalid'}
            $r=$records[$n];$i=$r.intent.Data
            Assert-ContentDeletionHeader $i @('schemaVersion','attempt','revision','manifestSha256','publishIntentSha256','keys','intentAt') $n $null
            if($i.revision -cne $Revision -or $i.intentAt -isnot [string] -or $i.keys -isnot [array] -or $i.keys.Count -lt 2 -or $i.keys.Count -gt 1024 -or
                $i.keys[0] -cne $prefix+'transport.json'){throw 'delete-record-invalid'}
            Assert-Hex $i.manifestSha256 64;Assert-Hex $i.publishIntentSha256 64;$null=Assert-Utc $i.intentAt
            $unique=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            foreach($k in @($i.keys|Select-Object -Skip 1)){
                if($k -isnot [string] -or -not $k.StartsWith($prefix+'content/',[StringComparison]::Ordinal) -or $k.Length -gt 512 -or -not $unique.Add($k)){throw 'delete-record-invalid'}
            }
            # 全試行は同じrevisionの同じ送信計画を消す。試行ごとに一覧が違えば記録の混入として扱う。
            $text=ConvertTo-Json -InputObject @($i.keys) -Compress
            if($n -eq 1){$state.ManifestSha256=$i.manifestSha256;$state.PublishIntentSha256=$i.publishIntentSha256;$state.Keys=@($i.keys);$keyText=$text}
            elseif($i.manifestSha256 -cne $state.ManifestSha256 -or $i.publishIntentSha256 -cne $state.PublishIntentSha256 -or $text -cne $keyText){throw 'delete-record-invalid'}
            $manifestKey=$i.keys[0];$sent=$null;$committed=$null
            if($r.ContainsKey('sent')){
                Assert-ContentDeletionHeader $r.sent.Data @('schemaVersion','attempt','intentSha256','observation') $n $r.intent.Sha256
                Assert-ArtifactObservation $r.sent.Data.observation 'delete' $manifestKey;$sent=$r.sent.Sha256
            }
            if($r.ContainsKey('committed') -and $r.ContainsKey('abandoned')){throw 'delete-record-invalid'}
            if($r.ContainsKey('deleted') -and -not $r.ContainsKey('committed')){throw 'delete-record-invalid'}
            if($r.ContainsKey('committed')){
                $v=$r.committed.Data
                Assert-ContentDeletionHeader $v @('schemaVersion','attempt','intentSha256','sentSha256','deleteObservation','getObservation') $n $r.intent.Sha256
                if(($sent -and ($v.deleteObservation -cne 'recorded' -or $v.sentSha256 -cne $sent)) -or
                    (-not $sent -and ($v.deleteObservation -cne 'unknown' -or $null -ne $v.sentSha256))){throw 'delete-record-invalid'}
                Assert-ArtifactObservation $v.getObservation 'get' $manifestKey 'missing'
                $committed=$r.committed.Sha256
            }
            if($r.ContainsKey('abandoned')){
                Assert-ContentDeletionHeader $r.abandoned.Data @('schemaVersion','attempt','intentSha256','getObservation') $n $r.intent.Sha256
                Assert-ArtifactObservation $r.abandoned.Data.getObservation 'get' $manifestKey 'present'
            }
            if($r.ContainsKey('deleted')){
                $v=$r.deleted.Data
                Assert-ContentDeletionHeader $v @('schemaVersion','attempt','committedSha256','observations') $n $null
                if($v.committedSha256 -cne $committed -or $v.observations -isnot [array]){throw 'delete-record-invalid'}
                # 確定点の後の各keyは、最後の観測がGET 404であること（削除済みの判定はGET 404だけに依る）。
                $last=@{}
                foreach($o in $v.observations){
                    if($null -eq $o -or $o.GetType() -ne [System.Management.Automation.PSCustomObject] -or $null -eq $o.PSObject.Properties['operation'] -or $null -eq $o.PSObject.Properties['key'] -or
                        $o.operation -cnotin @('get','delete') -or $o.key -isnot [string] -or -not $unique.Contains($o.key)){throw 'delete-record-invalid'}
                    Assert-ArtifactObservation $o $o.operation $o.key;$last[$o.key]=$o
                }
                foreach($k in $unique){if(-not $last.ContainsKey($k)){throw 'delete-record-invalid'};Assert-ArtifactObservation $last[$k] 'get' $k 'missing'}
            }
            if($n -lt $max -and -not $r.ContainsKey('abandoned')){throw 'delete-record-invalid'}
            if($n -eq $max -and $r.ContainsKey('deleted')){$state.State='deleted'}
            elseif($n -eq $max -and -not $r.ContainsKey('abandoned')){$state.State='deleting';$state.Pending=[pscustomobject]@{Attempt=$n;IntentSha256=$r.intent.Sha256;SentSha256=$sent;CommittedSha256=$committed}}
        }
        $state.Next=$max+1
    }catch{throw 'delete-record-invalid'}
    return $state
}
# 系列lockは <target>/cleanup.lock を FileShare.None で開いたhandle（.NETのFileStreamは子processへ継承されない）。
# process終了でOSが解放するので、異常終了しても永続の予約は残らない。取れないときは待たずに'lock-busy'。
# 系列directoryが無ければ'content-store-unavailable'（呼出側は「何も無い」ことを確かめて終える）。
function Enter-ContentSeriesLock([string]$RepositoryId,[string]$ContentSet,[string]$TargetToken){
    $null=Get-ContentSeries $RepositoryId $ContentSet $TargetToken
    $path=Get-ContentRoot
    foreach($part in @($RepositoryId,$ContentSet,$TargetToken)){
        $path=[IO.Path]::Combine($path,$part);Assert-NoArtifactReparse $path
        if(-not [IO.Directory]::Exists($path)){throw 'content-store-unavailable'}
        Assert-ArtifactAcl $path $true
    }
    $lock=[IO.Path]::Combine($path,'cleanup.lock');Assert-NoArtifactReparse $lock
    if(-not [IO.File]::Exists($lock)){
        # 作成競合に負けた側は既存fileを開く。作成者がACLを付け終える前に開いた場合は下の検査で失敗させ、推測で進まない。
        $created=$false
        try{$s=[IO.FileStream]::new($lock,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None);$s.Dispose();$created=$true}catch{if(-not [IO.File]::Exists($lock)){throw}}
        if($created){Set-ArtifactAcl $lock $false}
    }
    try{$handle=[IO.FileStream]::new($lock,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)}catch [IO.IOException]{throw 'lock-busy'}
    try{Assert-ArtifactAcl $lock $false}catch{$handle.Dispose();throw}
    return $handle
}
Export-ModuleMember -Function Write-ContentDeletionRecord,Read-ContentDeletionState,Enter-ContentSeriesLock
