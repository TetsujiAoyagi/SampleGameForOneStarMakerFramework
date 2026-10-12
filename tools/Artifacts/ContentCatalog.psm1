Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentContract.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentDeletionStore.psm1')
# この PC の contents-v1/<repositoryId>/<contentSet>/<target>/ を読み、1系列の保持policy入力行と、
# 削除中・削除済み・保護中のrevisionを作る。状態は持たず、何も書かない（dry-runと実行で同じ分類を使うため）。
# directoryが系列を持つので、他系列の記録はここに現れない。別PCのpublishと、receiptを失ったR2 objectも現れない。
#
# 止める範囲: 系列の記録を1件でも読めない・全件を見たと言えないときは系列全体をblockedにする
# （数え損ねると、残すべきrevisionを最古と誤認して消しうるため）。
# 保護（inUse）は呼出側が ContentProtection で集めたbuildIdの集合を渡し、revisionの末尾32 hexで照合する。

function New-ContentCatalogSnapshot([string]$SeriesId){
    return [pscustomobject]@{
        status='ready';reasonCode=$null;seriesId=$SeriesId
        rows=[Collections.Generic.List[object]]::new()       # policy入力（succeeded/incomplete）。buildIdにrevisionを入れる
        revisions=@{}                                         # 計数中の成功revision: 消すkeyの一覧・hash・次の試行番号
        protected=[Collections.Generic.List[string]]::new()  # 保護中（inUse）の成功revision
        deleting=[Collections.Generic.List[object]]::new()   # 未解決の削除試行があるrevision（成功にも保護にも数えない）
        deleted=[Collections.Generic.List[string]]::new()
    }
}
# 消すkeyの一覧はtransport.jsonを先頭に、publishのintentのentries順。intentはreceiptの検査で
# hashとobject数の結び付きを確かめた後に読むので、ここでは一覧の形と件数だけを確かめる。
function Get-ContentRevisionKeys($Config,[string]$Directory,[string]$ContentSet,[string]$TargetToken,[string]$Revision,$Receipt){
    $intent=(Read-ArtifactJson ([IO.Path]::Combine($Directory,'intent.json'))).Data
    $entries=$intent.PSObject.Properties['entries']
    if($null -eq $entries -or $entries.Value -isnot [array] -or $entries.Value.Count+1 -ne $Receipt.objectCount){throw 'receipt-invalid'}
    $keys=[Collections.Generic.List[string]]::new();$keys.Add((Get-ContentKey $Config $ContentSet $TargetToken $Revision))
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($e in $entries.Value){
        $p=if($null -ne $e -and $e.GetType() -eq [System.Management.Automation.PSCustomObject]){$e.PSObject.Properties['path']}else{$null}
        if($null -eq $p -or $p.Value -isnot [string] -or -not $seen.Add($p.Value)){throw 'receipt-invalid'}
        $keys.Add((Get-ContentKey $Config $ContentSet $TargetToken $Revision $p.Value))
    }
    return ,$keys.ToArray()
}
function Get-ContentSeriesSnapshot($Config,[string]$ContentSet,[string]$TargetToken,$ProtectedBuildIds,[DateTimeOffset]$Now){
    $repoId=$Config.Data.repositoryId
    $s=New-ContentCatalogSnapshot (Get-ContentSeries $repoId $ContentSet $TargetToken)
    $block={param($code)$s.status='blocked';$s.reasonCode=$code;foreach($list in @($s.rows,$s.protected,$s.deleting,$s.deleted)){$list.Clear()};$s.revisions=@{};return $s}
    try{
        $series=Get-ContentRoot
        foreach($part in @($repoId,$ContentSet,$TargetToken)){
            $series=[IO.Path]::Combine($series,$part);Assert-NoArtifactReparse $series
            if([IO.File]::Exists($series)){return & $block 'catalog-incomplete'}
            if(-not [IO.Directory]::Exists($series)){return $s}
            Assert-ArtifactAcl $series $true
        }
        $entries=@([IO.Directory]::EnumerateFileSystemEntries($series))
    }catch{return & $block 'catalog-incomplete'}
    # 系列直下で既知なのはrevision書式のdirectoryと、件数清掃の系列lock（cleanup.lock）だけ。
    $revisions=[Collections.Generic.List[string]]::new()
    foreach($path in $entries){
        $name=[IO.Path]::GetFileName($path)
        try{
            Assert-NoArtifactReparse $path
            if([IO.File]::Exists($path) -and $name -ceq 'cleanup.lock'){Assert-ArtifactAcl $path $false;continue}
            if([IO.Directory]::Exists($path) -and $name -cmatch '\A[0-9]{8}T[0-9]{9}Z-[0-9a-f]{32}\z'){$revisions.Add($name);continue}
        }catch{}
        return & $block 'catalog-incomplete'
    }
    foreach($rev in @($revisions|Sort-Object -CaseSensitive)){
        $dir=[IO.Path]::Combine($series,$rev)
        # revision directory内で既知なのは receipt.json・intent.json・publish.lock（file）と deletion/ だけ。
        try{
            Assert-ArtifactAcl $dir $true
            foreach($path in [IO.Directory]::EnumerateFileSystemEntries($dir)){
                $name=[IO.Path]::GetFileName($path);Assert-NoArtifactReparse $path
                if([IO.File]::Exists($path) -and $name -cin @('receipt.json','intent.json','publish.lock')){Assert-ArtifactAcl $path $false;continue}
                if([IO.Directory]::Exists($path) -and $name -ceq 'deletion'){continue}
                throw 'catalog-incomplete'
            }
        }catch{return & $block 'catalog-incomplete'}
        # receiptが無ければ成功ではない（intentの有無にかかわらず未完了として、Nに数えない）。
        if(-not [IO.File]::Exists([IO.Path]::Combine($dir,'receipt.json'))){$s.rows.Add([pscustomobject]@{buildId=$rev;publishState='incomplete'});continue}
        $receipt=$null;$keys=$null
        try{$receipt=(Read-ContentReceipt $dir $repoId $ContentSet $TargetToken $rev).Data;$keys=Get-ContentRevisionKeys $Config $dir $ContentSet $TargetToken $rev $receipt}catch{}
        if($null -eq $receipt -or $null -eq $keys){return & $block 'catalog-incomplete'}
        $published=Assert-Utc $receipt.publishedAt
        # 「最新」はpublishedAtの順。未来時刻があるなら時計がずれており、順序を信用できない。
        if($published -gt $Now){return & $block 'clock-skew'}
        # 削除の記録（取下げだけの試行を含む）は、このreceiptのmanifest・intentと消すkeyの一覧に結び付いていること。
        $deletion=$null;try{$deletion=Read-ContentDeletionState $repoId $ContentSet $TargetToken $rev}catch{}
        if($null -eq $deletion -or ($deletion.Next -gt 1 -and
            ($deletion.ManifestSha256 -cne $receipt.manifestSha256 -or $deletion.PublishIntentSha256 -cne $receipt.intentSha256 -or
            (@($deletion.Keys) -join "`n") -cne ($keys -join "`n")))){return & $block 'delete-record-invalid'}
        $info=[pscustomobject]@{keys=$keys;manifestSha256=$receipt.manifestSha256;publishIntentSha256=$receipt.intentSha256;next=$deletion.Next;publishedAt=$published;pending=$deletion.Pending}
        switch -CaseSensitive ($deletion.State){
            'deleted'{$s.deleted.Add($rev)}
            # 未解決の試行があるrevisionは成功にも保護にも数えない。次の実行が再開から確定させる。
            'deleting'{$s.deleting.Add([pscustomobject]@{revision=$rev;publishedAt=$published;info=$info})}
            default{
                $inUse=$ProtectedBuildIds.Contains($rev.Substring($rev.Length-32))
                $s.rows.Add([pscustomobject]@{buildId=$rev;publishState='succeeded';publishedAt=$published;inUse=$inUse})
                $s.revisions[$rev]=$info
                if($inUse){$s.protected.Add($rev)}
            }
        }
    }
    return $s
}
Export-ModuleMember -Function Get-ContentSeriesSnapshot
