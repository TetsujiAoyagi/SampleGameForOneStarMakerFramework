Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot '../Workflow/WindowsStorePaths.psm1')
Import-Module (Join-Path $PSScriptRoot '../Workflow/WindowsStoreRoot.psm1')
# 系列の退役記録の所有者（BuildとContentが同じものを使う）。ownerの明示指示（--decision）で退役した事実を、
# 系列ごとに1件の不変記録として残す。置き場は builds-v1／contents-v1 と別のroot
#   retirements-v1/<repositoryId>/<build|content>/<系列token を ~ で結んだ名前>.json
# にする。既存storeの中に置くと、未知entryで系列を止めるcatalogの規則を変えることになるため。
# 取消は無い: 書くのは「無い」ときの1回だけで、既存の記録を置き換え・削除しない。読めない記録を
# 「退役していない」と扱うと退役済み系列へpublishできてしまうので、呼出側は不明を fail-closed で扱う。
# 注入rootはoffline検証専用。公開CLIから差し替えさせない。
$script:TestRoot=$null
$script:FaultHook=$null
$script:TokenNames=@{build=@('project','target','configuration');content=@('contentSet','target')}
$script:Fields=@{
    build=@('schemaVersion','kind','repositoryId','seriesId','project','target','configuration','decision','retiredAt','owner')
    content=@('schemaVersion','kind','repositoryId','seriesId','contentSet','target','decision','retiredAt','owner')
}

# 指示IDはWorkflowのDecisionと同じ書式（chat:<UTC>:<taskId>:<kind>、github:<owner>/<repo>/pull/<n>/<fragment> 等）。
# 非秘密の識別子だけを受け、本文や秘密を記録へ入れさせない。末尾改行も拒否するため \A…\z で照合する。
function Assert-SeriesRetirementDecision([string]$Decision){
    if($Decision -cnotmatch '\A[a-zA-Z0-9][a-zA-Z0-9._:/-]{0,127}\z'){throw 'invalid-decision'}
}
# 系列tokenの検査は呼出側のcontract（Get-BuildSeries／Get-ContentSeries）が先に行う。ここでは
# path要素として安全な形と件数だけを確かめ、seriesIdを同じ規則（repositoryId/token/…）で組み立てる。
function Get-SeriesRetirementPath([string]$Kind,[string]$RepositoryId,[string[]]$Tokens){
    if($Kind -cnotin @('build','content') -or $Tokens.Count -ne $script:TokenNames[$Kind].Count){throw 'retirement-unknown'}
    Assert-Hex $RepositoryId 64
    foreach($t in $Tokens){if($t -cnotmatch '\A[a-z0-9][a-z0-9-]{0,63}\z'){throw 'retirement-unknown'}}
    return [pscustomobject]@{Parts=@($RepositoryId,$Kind);Leaf=($Tokens -join '~')+'.json';SeriesId="$RepositoryId/"+($Tokens -join '/')}
}
function Get-SeriesRetirementRoot {
    if(-not [OperatingSystem]::IsWindows()){throw 'retirement-unknown'}
    # 所在と移行済みの判定はresolverが持つ。注入rootだけがそれを迂回する。
    $root=if($script:TestRoot){[IO.Path]::GetFullPath($script:TestRoot)}else{[IO.Path]::Combine((Get-OsmStoreRoot),'Artifacts','retirements-v1')}
    Assert-NoArtifactReparse $root
    if([IO.Directory]::Exists($root)){$root=(Get-WindowsStorePathIdentity $root).physicalPath;Assert-NoArtifactReparse $root}
    return $root
}
# 状態: none（記録が無い。rootやrepoの階層が無いことも含む）／retired（検査を通った記録）。
# それ以外（形・field・値・ACL・reparse・読取の失敗、別系列の内容）は全て 'retirement-unknown' の例外。
function Read-SeriesRetirement([string]$Kind,[string]$RepositoryId,[string[]]$Tokens){
    $none=[pscustomobject]@{State='none';Record=$null}
    try{
        $p=Get-SeriesRetirementPath $Kind $RepositoryId $Tokens
        $path=Get-SeriesRetirementRoot
        if([IO.File]::Exists($path)){throw 'retirement-unknown'}
        if(-not [IO.Directory]::Exists($path)){return $none}
        Assert-ArtifactAcl $path $true
        foreach($part in $p.Parts){
            $path=[IO.Path]::Combine($path,$part);Assert-NoArtifactReparse $path
            if([IO.File]::Exists($path)){throw 'retirement-unknown'}
            if(-not [IO.Directory]::Exists($path)){return $none}
            Assert-ArtifactAcl $path $true
        }
        $file=[IO.Path]::Combine($path,$p.Leaf);Assert-NoArtifactReparse $file
        if([IO.Directory]::Exists($file)){throw 'retirement-unknown'}
        if(-not [IO.File]::Exists($file)){return $none}
        Assert-ArtifactAcl $file $false
        $v=(Read-ArtifactJson $file).Data
        Assert-Fields $v $script:Fields[$Kind]
        if($v.schemaVersion -isnot [long] -or $v.schemaVersion -ne 1 -or $v.kind -cne $Kind -or
            $v.repositoryId -cne $RepositoryId -or $v.seriesId -cne $p.SeriesId -or
            $v.decision -isnot [string] -or $v.retiredAt -isnot [string] -or
            $v.owner -cne [Security.Principal.WindowsIdentity]::GetCurrent().User.Value){throw 'retirement-unknown'}
        $names=$script:TokenNames[$Kind]
        for($i=0;$i -lt $names.Count;$i++){if($v.($names[$i]) -cne $Tokens[$i]){throw 'retirement-unknown'}}
        Assert-SeriesRetirementDecision $v.decision;$null=Assert-Utc $v.retiredAt
        return [pscustomobject]@{State='retired';Record=$v}
    }catch{throw 'retirement-unknown'}
}
# 記録が無いときだけ呼ぶ。同じdirectoryのtempへCreateNewで全量書き・flushし、存在しない最終名へmoveする
# （上書きしない）。同時に2つのprocessが書いた場合はmoveに勝った1件だけが残り、負けた側は自分のtempを消して
# 既存の記録を読み直す（最初の retiredAt・decision を変えない）。返すのは書込み後に読み直した状態。
function Write-SeriesRetirement([string]$Kind,[string]$RepositoryId,[string[]]$Tokens,[string]$Decision,[DateTimeOffset]$Now){
    Assert-SeriesRetirementDecision $Decision
    $p=Get-SeriesRetirementPath $Kind $RepositoryId $Tokens
    $dirs=@(Get-SeriesRetirementRoot);foreach($part in $p.Parts){$dirs+=[IO.Path]::Combine($dirs[-1],$part)}
    # 共有親（Artifacts）のACLは変更せず、この退役領域と子だけを専用所有にする。初回の同時作成では他processが
    # 作った直後のACL未設定を見うるので、作成は冪等にし、ACLが無ければ付ける（ContentStoreと同じ扱い）。
    if(-not [IO.Directory]::Exists([IO.Path]::GetDirectoryName($dirs[0]))){throw 'retirement-unknown'}
    foreach($dir in $dirs){
        Assert-NoArtifactReparse $dir
        [IO.Directory]::CreateDirectory($dir)|Out-Null;try{Assert-ArtifactAcl $dir $true}catch{Set-ArtifactAcl $dir $true}
    }
    $final=[IO.Path]::Combine($dirs[-1],$p.Leaf)
    $value=[ordered]@{schemaVersion=1;kind=$Kind;repositoryId=$RepositoryId;seriesId=$p.SeriesId}
    $names=$script:TokenNames[$Kind];for($i=0;$i -lt $names.Count;$i++){$value[$names[$i]]=$Tokens[$i]}
    $value.decision=$Decision;$value.retiredAt=$Now.ToUniversalTime().ToString('o');$value.owner=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $value -Compress))
    $temp=$final+'.'+[Guid]::NewGuid().ToString('N')+'.tmp';$stream=$null;$moved=$false
    try{
        $stream=[IO.FileStream]::new($temp,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        Set-ArtifactAcl $temp $false
        $stream.Write($bytes,0,$bytes.Length);$stream.Flush($true);$stream.Dispose();$stream=$null
        if($script:FaultHook){& $script:FaultHook 'before-rename'}
        try{[IO.File]::Move($temp,$final);$moved=$true}catch{if(-not [IO.File]::Exists($final)){throw}}
    }finally{
        if($stream){$stream.Dispose()}
        if(-not $moved -and [IO.File]::Exists($temp)){[IO.File]::Delete($temp)}
    }
    return Read-SeriesRetirement $Kind $RepositoryId $Tokens
}
Export-ModuleMember -Function Assert-SeriesRetirementDecision,Read-SeriesRetirement,Write-SeriesRetirement
