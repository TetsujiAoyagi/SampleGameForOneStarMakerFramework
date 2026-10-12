Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildStage.psm1')
# Contentの件数清掃で「保持中のPlayerが受け入れうるrevision」を守るため、保持中のPlayerのbuildIdを集める。
# build collect で退避したPlayerが受け入れるrevisionは、末尾32 hexがそのbuildIdのものに一意に決まる
# （PlayerのreceiptのidentityとPlayer同梱のtransport.jsonのrevisionが一致し、buildIdはその末尾32 hex）。
# 照合はentry名だけで行い、build-info.json等の中身を読まない（読めないfileで系列を止めず、消さない側に倒す）。
# 状態を持たず、何も書かない。保護源の規則はBuild・ArtifactsRoot側の変更で変わるので、catalogと分けて持つ。
#
# 保護源:
#  (a) builds-v1/<repositoryId>/ 直下の <32hex>（Build directory）と <32hex>.reserved（予約）。
#      除くのは Read-BuildDeletionState が deleted と読めたIDだけ。状態を読めないIDは保護に残す。
#      失敗・未完了・予約だけのBuildも数えるのは意図した過剰保護（publish途中のPlayerを守るため）。
#  (b) ArtifactsRootの players/<p>/<t>/<c>/ 直下の退避済みPlayerとstaging（配置の読取は所有者のBuildStage）。
#      名前が見えたIDは全て数える。
# 32 hexを取り出せない名前は寄与しない。root・repo・players/ と各階層の列挙そのものの失敗とreparseは
# 'protection-unknown'（見落としを保護なしと区別できないため）。repoや players/ が無いことは「保護なし」。
function Get-ContentProtectedBuildIds([string]$RepositoryId,[string]$ArtifactsRoot){
    $ids=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    try{
        $repo=[IO.Path]::Combine((Get-BuildRoot),$RepositoryId);Assert-NoArtifactReparse $repo
        if([IO.File]::Exists($repo)){throw 'protection-unknown'}
        $names=if([IO.Directory]::Exists($repo)){@([IO.Directory]::EnumerateFileSystemEntries($repo)|ForEach-Object {[IO.Path]::GetFileName($_)})}else{@()}
        $staged=@(Get-BuildStagedPlayerIds $ArtifactsRoot)
    }catch{throw 'protection-unknown'}
    foreach($name in $names){
        $m=[regex]::Match($name,'\A([0-9a-f]{32})(?:\.reserved)?\z')
        if(-not $m.Success -or $ids.Contains($m.Groups[1].Value)){continue}
        $deleted=$false
        try{$deleted=(Read-BuildDeletionState $RepositoryId $m.Groups[1].Value).State -ceq 'deleted'}catch{}
        if(-not $deleted){$null=$ids.Add($m.Groups[1].Value)}
    }
    foreach($id in $staged){$null=$ids.Add($id)}
    return ,$ids
}
Export-ModuleMember -Function Get-ContentProtectedBuildIds
