Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildContract.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildProtection.psm1')
# この PC の builds-v1/<repositoryId>/ を読み、指定した1系列の保持policy入力行と、
# 削除中・削除済み・保留・他系列の破損件数を作る。状態は持たず、何も書かない（dry-runと
# 実行で同じ分類を使うため）。別PCのpublishと、receiptを失ったR2 objectはここに現れない。
#
# 止める範囲: 指定系列に属する記録、または系列を判定できない記録が読めなければ系列全体を
# blocked にする（数え損ねると、残すべきBuildを最古と誤認して消しうるため）。
# 他系列と確定できる記録の破損では止めず、件数だけ foreignInvalid に出す。

function New-BuildCatalogSnapshot([string]$SeriesId){
    return [pscustomobject]@{
        status='ready';reasonCode=$null;seriesId=$SeriesId
        rows=[Collections.Generic.List[object]]::new()      # policy入力（succeeded/incomplete）
        builds=@{}                                           # 計数中の成功Build: key・hash・次の試行番号
        held=[Collections.Generic.List[object]]::new()      # 系列不明の未完了（IDは出さない）
        deleting=[Collections.Generic.List[object]]::new()  # 未解決の削除試行がある成功Build
        deleted=[Collections.Generic.List[string]]::new()
        foreignInvalid=0
    }
}
function Get-BuildSeriesSnapshot($Config,[string]$Project,[string]$Target,[string]$Configuration,[DateTimeOffset]$Now){
    $repoId=$Config.Data.repositoryId
    $series=Get-BuildSeries $repoId $Project $Target $Configuration
    $s=New-BuildCatalogSnapshot $series
    $block={param($code)$s.status='blocked';$s.reasonCode=$code;foreach($list in @($s.rows,$s.held,$s.deleting,$s.deleted)){$list.Clear()};$s.builds=@{};return $s}
    try{
        $repo=[IO.Path]::Combine((Get-BuildRoot),$repoId);Assert-NoArtifactReparse $repo
        if([IO.File]::Exists($repo)){return & $block 'catalog-incomplete'}
        if(-not [IO.Directory]::Exists($repo)){return $s}
        Assert-ArtifactAcl $repo $true
        $entries=@([IO.Directory]::EnumerateFileSystemEntries($repo))
    }catch{return & $block 'catalog-incomplete'}
    # repo直下で既知なのは <32hex>/、<32hex>.reserved、locks/ だけ。未知のentryは
    # 記録の置き場が変わった・壊れた可能性があり、全件を見たと言えないので止める。
    $dirs=[Collections.Generic.List[string]]::new();$reserved=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($path in $entries){
        $name=[IO.Path]::GetFileName($path)
        try{Assert-NoArtifactReparse $path}catch{return & $block 'catalog-incomplete'}
        # 既知のentryもACLを検査する（repo直下のACL不一致は系列を止める条件）。Build directoryは下で検査する。
        if([IO.Directory]::Exists($path) -and $name -ceq 'locks'){try{Assert-ArtifactAcl $path $true}catch{return & $block 'catalog-incomplete'};continue}
        if([IO.Directory]::Exists($path) -and $name -cmatch '\A[0-9a-f]{32}\z'){$dirs.Add($name);continue}
        if([IO.File]::Exists($path) -and $name -cmatch '\A([0-9a-f]{32})\.reserved\z'){try{Assert-ArtifactAcl $path $false}catch{return & $block 'catalog-incomplete'};$null=$reserved.Add($name.Substring(0,32));continue}
        return & $block 'catalog-incomplete'
    }
    # 予約直後に止まりdirectoryが無いものは、receiptもintentも無い系列不明の未完了と同じ。
    foreach($id in $reserved){if(-not $dirs.Contains($id)){$s.held.Add([pscustomobject]@{buildId=$null;reasonCode='publish-incomplete'})}}
    foreach($id in @($dirs|Sort-Object -CaseSensitive)){
        $dir=[IO.Path]::Combine($repo,$id)
        try{Assert-ArtifactAcl $dir $true}catch{return & $block 'catalog-incomplete'}
        if([IO.File]::Exists([IO.Path]::Combine($dir,'receipt.json'))){
            $record=$null;try{$record=Read-BuildSuccessRecord $Config $id}catch{}
            if($null -eq $record){
                # 他系列と確定できるのは、receiptとintentの両方が読めて所属が一致し、それが
                # 指定系列と違う場合だけ。片方が読めない・食い違う・fieldが欠けるものは系列不明。
                $foreign=$false
                try{
                    $a=Read-BuildRecordIdentity $repoId $id 'receipt.json';$b=Read-BuildRecordIdentity $repoId $id 'intent.json'
                    $foreign=$a.seriesId -ceq $b.seriesId -and $a.project -ceq $b.project -and $a.target -ceq $b.target -and $a.configuration -ceq $b.configuration -and $a.seriesId -cne $series
                }catch{}
                if($foreign){$s.foreignInvalid++;continue}
                return & $block 'catalog-incomplete'
            }
            $v=$record.Data
            if($v.seriesId -cne $series){
                # 他系列と確定したBuildの削除・保護の記録の破損は、止めずに件数だけ出す。
                $readable=$false
                try{$null=Read-BuildDeletionState $repoId $id;$records=@(Read-BuildProtectionRecords $repoId $id);$readable=(Get-BuildProtectionState $records $id $Now) -cne 'unknown'}catch{}
                if(-not $readable){$s.foreignInvalid++}
                continue
            }
            $published=Assert-Utc $v.publishedAt
            # 「最新」はpublishedAtの順。未来時刻があるなら時計がずれており、順序を信用できない。
            if($published -gt $Now){return & $block 'clock-skew'}
            $deletion=$null;try{$deletion=Read-BuildDeletionState $repoId $id}catch{}
            if($null -eq $deletion -or ($deletion.State -cne 'none' -and ($deletion.Key -cne $v.key -or $deletion.PackageSha256 -cne $v.packageSha256))){return & $block 'delete-record-invalid'}
            $protection='unknown';try{$records=@(Read-BuildProtectionRecords $repoId $id);$protection=Get-BuildProtectionState $records $id $Now}catch{}
            if($protection -ceq 'unknown'){return & $block 'protection-unknown'}
            switch -CaseSensitive ($deletion.State){
                'deleted'{$s.deleted.Add($id)}
                # 未解決の試行があるBuildは成功にも保護にも数えない。次の実行が再開から確定させる。
                'deleting'{$s.deleting.Add([pscustomobject]@{buildId=$id;key=$v.key;packageSha256=$v.packageSha256;publishedAt=$published;pending=$deletion.Pending})}
                default{
                    $s.rows.Add([pscustomobject]@{buildId=$id;publishState='succeeded';publishedAt=$published;inUse=($protection -ceq 'protected')})
                    $s.builds[$id]=[pscustomobject]@{key=$v.key;packageSha256=$v.packageSha256;next=$deletion.Next}
                }
            }
            continue
        }
        # receiptが無ければ成功ではない（削除・保護の記録が残っていても数えない。成功として
        # 数えないことは消す件数を減らす側にしか働かない）。ただしintentが指定系列を示すなら、
        # 残っている削除・保護の記録は指定系列に属する記録なので、読めなければ止める（AC4(a)）。
        $intent=$null
        if([IO.File]::Exists([IO.Path]::Combine($dir,'intent.json'))){try{$intent=Read-BuildRecordIdentity $repoId $id 'intent.json'}catch{}}
        if($intent -and $intent.seriesId -ceq $series){
            $deletion=$null;try{$deletion=Read-BuildDeletionState $repoId $id}catch{}
            if($null -eq $deletion -or ($deletion.State -cne 'none' -and $deletion.Key -cne "development/builds/v1/$repoId/$Project/$Target/$Configuration/$id/bundle.zip")){return & $block 'delete-record-invalid'}
            $protection='unknown';try{$records=@(Read-BuildProtectionRecords $repoId $id);$protection=Get-BuildProtectionState $records $id $Now}catch{}
            if($protection -ceq 'unknown'){return & $block 'protection-unknown'}
        }
        if($intent){if($intent.seriesId -ceq $series){$s.rows.Add([pscustomobject]@{buildId=$id;publishState='incomplete'})};continue}
        $s.held.Add([pscustomobject]@{buildId=$null;reasonCode='publish-incomplete'})
    }
    return $s
}
Export-ModuleMember -Function Get-BuildSeriesSnapshot
