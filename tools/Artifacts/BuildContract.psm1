Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'EvidenceContract.psm1')
Import-Module (Join-Path $PSScriptRoot '../Workflow/WindowsStorePaths.psm1')
$script:OwnedRootsForTest=@()

# 系列はrepository内のproject/target/configurationで識別する。
# 正規化による別名の統合やbranch別の保存枠は導入せず、非正規入力を拒否する。
function Assert-BuildToken([string]$Value){if($Value -cnotmatch '\A[a-z0-9][a-z0-9-]{0,63}\z'){throw 'invalid-build-identity'}}
function Assert-BuildId([string]$Value){Assert-Hex $Value 32}
function Get-BuildSeries([string]$RepositoryId,[string]$Project,[string]$Target,[string]$Configuration){
    Assert-Hex $RepositoryId 64;foreach($v in @($Project,$Target,$Configuration)){Assert-BuildToken $v}
    return "$RepositoryId/$Project/$Target/$Configuration"
}
# 任意のremote keyを受け取らず、検証済みmetadataから一意に導く。
# 既存成功と異なるIDを使うことで、別publishの失敗が旧objectを置換しない。
function Get-BuildKey($Config,[string]$Project,[string]$Target,[string]$Configuration,[string]$BuildId){
    $null=Get-BuildSeries $Config.Data.repositoryId $Project $Target $Configuration;Assert-BuildId $BuildId
    return "development/builds/v1/$($Config.Data.repositoryId)/$Project/$Target/$Configuration/$BuildId/bundle.zip"
}
function Assert-BuildSourceRoot([string]$Root){
    if([OneStarMaker.Artifacts.Packaging.PackagePolicy]::HasReparse($Root) -or
        [OneStarMaker.Artifacts.Packaging.PackagePolicy]::IsProtectedPath($Root)){throw 'invalid-source'}
    $local=[Environment]::GetFolderPath('LocalApplicationData')
    # Known Folderの論理pathとnative実体pathの両方を拒否境界に含める。
    # 名前が無害でも、資格情報・Evidence・他taskの所有storeをBuildへ取り込ませない。
    $owned=@([IO.Path]::Combine($local,'OneStarMaker','Artifacts'),[IO.Path]::Combine($local,'OneStarMaker','Workflow'))+@($script:OwnedRootsForTest)
    foreach($logical in @([IO.Path]::Combine($local,'OneStarMaker','Artifacts'),[IO.Path]::Combine($local,'OneStarMaker','Workflow'))){
        if([IO.Directory]::Exists($logical)){$owned+=@(Get-WindowsStorePathIdentity $logical).physicalPath}
    }
    $owned+=@(Get-ArtifactRoot)
    # 所有領域の子だけでなく、その親をrootに選んで巻き込む入力も拒否する。
    foreach($boundary in $owned){if($boundary -and ((Test-ArtifactWithin $Root $boundary) -or (Test-ArtifactWithin $boundary $Root))){throw 'invalid-source'}}
}
function Read-BuildConfig([string]$Path){
    # 設定の親もprivate operationとして検査し、既存deploymentを読取専用で照合する。
    $full=[IO.Path]::GetFullPath($Path);$parent=[IO.Path]::GetDirectoryName($full)
    if(-not [IO.Path]::IsPathFullyQualified($Path) -or $full -cne $Path -or $parent -cnotmatch '[\\/][0-9a-f]{32}$'){throw 'invalid-build-config'}
    Assert-ArtifactOperationFile $parent $full
    $item=Read-ArtifactJson $full;$v=$item.Data
    Assert-Fields $v @('schemaVersion','purpose','profile','evidenceConfigPath','evidenceConfigSha256','repositoryId','prefix','policy')
    if($v.schemaVersion -isnot [long] -or $v.schemaVersion -ne 1 -or $v.purpose -cne 'build' -or
        $v.profile -cne 'osm' -or $v.prefix -cne 'development/builds/v1/' -or
        $v.policy -cne 'build-publish-v1' -or $v.evidenceConfigPath -isnot [string] -or
        -not [IO.Path]::IsPathFullyQualified($v.evidenceConfigPath)) {throw 'invalid-build-config'}
    Assert-Hex $v.repositoryId 64;Assert-Hex $v.evidenceConfigSha256 64
    # 既存deploymentの登録receipt/hash照合を再利用し、endpoint/bucketだけを借りる。
    # Evidenceのtask所有・終了時刻・30日期限をBuild設定へ継承しない。
    $evidence=Read-EvidenceConfig $v.evidenceConfigPath
    if($evidence.Hash -cne $v.evidenceConfigSha256 -or $evidence.Data.repositoryId -cne $v.repositoryId){throw 'invalid-build-config'}
    return @{Data=[pscustomobject]@{repositoryId=$v.repositoryId;endpoint=$evidence.Data.endpoint;bucket=$evidence.Data.bucket;prefix=$v.prefix};Hash=$item.Sha256;EvidenceConfigSha256=$evidence.Hash}
}
# IDは呼出側が先に保存したselectionから受け取る。応答が失われても同じIDで
# inspectできるよう、publish内部で未知のIDを発行する契約にはしない。
# 不明fieldや型違いは推測で補わず、通信前に入力全体を拒否する。
function Read-BuildSelection([string]$Path){
    $full=[IO.Path]::GetFullPath($Path);$parent=[IO.Path]::GetDirectoryName($full)
    if(-not [IO.Path]::IsPathFullyQualified($Path) -or $full -cne $Path -or $parent -cnotmatch '[\\/][0-9a-f]{32}$'){throw 'invalid-selection'}
    Assert-ArtifactOperationFile $parent $full
    $v=(Read-ArtifactJson $Path).Data
    Assert-Fields $v @('schemaVersion','purpose','root','files','project','target','configuration','buildId')
    if($v.schemaVersion -isnot [long] -or $v.schemaVersion -ne 1 -or $v.purpose -cne 'build' -or
        $v.root -isnot [string] -or -not [IO.Path]::IsPathFullyQualified($v.root) -or
        $v.files -isnot [array] -or $v.files.Count -lt 1 -or $v.files.Count -gt 512){throw 'invalid-selection'}
    foreach($token in @($v.project,$v.target,$v.configuration)){if($token -isnot [string]){throw 'invalid-selection'};Assert-BuildToken $token}
    if($v.buildId -isnot [string]){throw 'invalid-selection'};Assert-BuildId $v.buildId
    $root=[IO.Path]::GetFullPath($v.root)
    if(-not [IO.Directory]::Exists($root)){throw 'invalid-source'}
    Assert-BuildSourceRoot $root
    $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($file in $v.files){
        if($file -isnot [string] -or -not $names.Add($file)){throw 'invalid-selection'}
        $null=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Relative($file)
        $source=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($root,$file)
        if(-not [IO.File]::Exists($source)){throw 'invalid-source'}
    }
    return $v
}
# opaqueな文字列でも、復号後はpurpose・所属・固定key・サイズをすべて照合する。
# Evidence参照との混同や、見かけ上同じ系列から別keyを指す入力を許さない。
function Assert-BuildReference($v,$Config){
    Assert-Fields $v @('schemaVersion','purpose','repositoryId','project','target','configuration','seriesId','buildId','key','manifestSha256','packageBytes')
    if($v.schemaVersion -isnot [long] -or $v.schemaVersion -ne 1 -or $v.purpose -cne 'build' -or
        $v.repositoryId -cne $Config.Data.repositoryId){throw 'invalid-reference'}
    foreach($name in @('repositoryId','project','target','configuration','seriesId','buildId','key','manifestSha256')){if($v.$name -isnot [string]){throw 'invalid-reference'}}
    $series=Get-BuildSeries $v.repositoryId $v.project $v.target $v.configuration;Assert-BuildId $v.buildId;Assert-Hex $v.manifestSha256 64
    if($v.seriesId -cne $series -or $v.key -cne (Get-BuildKey $Config $v.project $v.target $v.configuration $v.buildId) -or
        ($v.packageBytes -isnot [long] -and $v.packageBytes -isnot [int]) -or $v.packageBytes -lt 1 -or $v.packageBytes -gt 256MB){throw 'invalid-reference'}
}
# packageSha256を参照へ内包して自動採用しない。期待hashは成功結果/信頼済み
# receiptから別fieldで渡し、参照と期待内容の両方を取得側が確認する。
# Int64はJSON readerのschemaVersion型と揃えるためで、数値文字列への緩和ではない。
function New-BuildReference($Config,$Record){
    $v=[ordered]@{schemaVersion=[long]1;purpose='build';repositoryId=$Record.repositoryId;project=$Record.project;target=$Record.target;configuration=$Record.configuration;seriesId=$Record.seriesId;buildId=$Record.buildId;key=$Record.key;manifestSha256=$Record.manifestSha256;packageBytes=$Record.packageBytes}
    Assert-BuildReference ([pscustomobject]$v) $Config
    $raw=ConvertTo-Json -InputObject $v -Compress
    return 'osm-build-v1:'+ [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($raw)).TrimEnd('=').Replace('+','-').Replace('/','_')
}
function Read-BuildReference([string]$Text,$Config){
    if(-not $Text.StartsWith('osm-build-v1:',[StringComparison]::Ordinal) -or $Text.Length -gt 4096){throw 'invalid-reference'}
    $s=$Text.Substring(13);if($s -cnotmatch '\A[A-Za-z0-9_-]+\z'){throw 'invalid-reference'}
    $b=$s.Replace('-','+').Replace('_','/');$b=$b.PadRight($b.Length+((4-$b.Length%4)%4),'=')
    # 正規Base64url表現、厳密UTF-8、重複JSON fieldの順に検査する。
    # 同じ内容に複数の表記や解釈が生まれる参照を、成功記録として持ち回らない。
    $bytes=[Convert]::FromBase64String($b)
    if([Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+','-').Replace('/','_') -cne $s){throw 'invalid-reference'}
    $raw=[Text.UTF8Encoding]::new($false,$true).GetString($bytes);$doc=[Text.Json.JsonDocument]::Parse($raw)
    try{Assert-JsonDuplicates $doc.RootElement}finally{$doc.Dispose()}
    $v=$raw|ConvertFrom-Json -Depth 8 -DateKind String;Assert-BuildReference $v $Config;return $v
}
Export-ModuleMember -Function Assert-BuildId,Get-BuildSeries,Get-BuildKey,Read-BuildConfig,Read-BuildSelection,New-BuildReference,Read-BuildReference
