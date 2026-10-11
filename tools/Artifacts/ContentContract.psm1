Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'EvidenceContract.psm1')
# Contentの値の規則（系列・identity・key・reference・manifest・config）の所有者。
# Buildの規則とは独立に変わるので、BuildContractを共有せず並存させる（統合はContent分離で扱う）。
# 上限は暴走防止の暫定ガードで、Content固有の容量上限の決定ではない。
$script:ObjectLimit=1024
$script:TotalLimit=512MB
$script:FileLimit=256MB
$script:ManifestLimit=4MB
$script:KeyLimit=512
$script:ReferencePrefix='osm-content-v1:'

function Assert-ContentToken([string]$Value){if($Value -cnotmatch '\A[a-z0-9][a-z0-9-]{0,63}\z'){throw 'invalid-content-identity'}}
function Assert-ContentRevision([string]$Value){if($Value -cnotmatch '\A[0-9]{8}T[0-9]{9}Z-[0-9a-f]{32}\z'){throw 'invalid-content-identity'}}
# manifestのtargetはUnity側の名前、保存系列のtargetはtoken。表に無い値は近い値へ丸めず拒否し、
# 新platformは表への追加で受け止める。
function Get-ContentTargetToken([string]$Target){
    $token=switch -CaseSensitive ($Target){'StandaloneWindows64-Player'{'windows-x64-player'};default{$null}}
    if(-not $token){throw 'unsupported-series'}
    return $token
}
function Assert-ContentTargetToken([string]$Token){if($Token -cnotin @('windows-x64-player')){throw 'unsupported-series'}}
function Get-ContentSeries([string]$RepositoryId,[string]$ContentSet,[string]$TargetToken){
    Assert-Hex $RepositoryId 64;Assert-ContentToken $ContentSet;Assert-ContentTargetToken $TargetToken
    return "$RepositoryId/$ContentSet/$TargetToken"
}
# manifestのpathはPackagePolicy.Relativeの規則に加えて文字種を絞る。".."を含む名前は
# transport（C#）のkey検査が拒否するので、ここで先に弾いて送信途中の失敗にしない。
function Assert-ContentPath([string]$Path){
    try{$null=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Relative($Path)}catch{throw 'invalid-content-manifest'}
    if($Path -cnotmatch '\A[A-Za-z0-9._/-]+\z' -or $Path.Contains('..')){throw 'invalid-content-manifest'}
}
# 任意のkeyを入力に取らず、検証済みの値から導く。Pathが空ならtransport.json。
function Get-ContentKey($Config,[string]$ContentSet,[string]$TargetToken,[string]$Revision,[string]$Path=''){
    $null=Get-ContentSeries $Config.Data.repositoryId $ContentSet $TargetToken;Assert-ContentRevision $Revision
    $key="development/content/v1/$($Config.Data.repositoryId)/$ContentSet/$TargetToken/$Revision/"
    if($Path){Assert-ContentPath $Path;$key+="content/$Path"}else{$key+='transport.json'}
    if($key.Length -gt $script:KeyLimit){throw 'size-limit'}
    return $key
}
function Get-ContentProperty($Object,[string]$Name){
    $p=if($null -ne $Object -and $Object.GetType() -eq [System.Management.Automation.PSCustomObject]){$Object.PSObject.Properties[$Name]}else{$null}
    if($null -eq $p){throw 'invalid-content-manifest'}
    return ,$p.Value
}
# transport.jsonは専用の4 MiB readerで読む。既存の1 MiB readerは使わない（Spring実測で約330 KB、
# 4季節で0.5 MB前後の見積もり）。重複keyはArtifactCommandsと同じ方式で拒否する。
function Read-ContentManifestFile([string]$Path){
    if(-not [IO.Path]::IsPathFullyQualified($Path) -or -not [IO.File]::Exists($Path) -or
        [OneStarMaker.Artifacts.Packaging.PackagePolicy]::HasReparse($Path)){throw 'invalid-source'}
    $length=[IO.FileInfo]::new($Path).Length
    if($length -lt 1 -or $length -gt $script:ManifestLimit){throw 'invalid-content-manifest'}
    return [IO.File]::ReadAllBytes($Path)
}
# 使う値（contentSet・revision・target・files）だけを型検査する。未使用fieldの追加では止めない。
# 余剰・欠落の検査は呼出側がfile集合との突合で行う。
function ConvertFrom-ContentManifest([byte[]]$Bytes){
    if($Bytes.Length -lt 1 -or $Bytes.Length -gt $script:ManifestLimit){throw 'invalid-content-manifest'}
    try{
        $raw=[Text.UTF8Encoding]::new($false,$true).GetString($Bytes);$doc=[Text.Json.JsonDocument]::Parse($raw)
        try{Assert-JsonDuplicates $doc.RootElement}finally{$doc.Dispose()}
        $v=$raw|ConvertFrom-Json -Depth 32 -DateKind String
    }catch{throw 'invalid-content-manifest'}
    $contentSet=Get-ContentProperty $v 'contentSet';$revision=Get-ContentProperty $v 'revision';$target=Get-ContentProperty $v 'target';$files=Get-ContentProperty $v 'files'
    if($contentSet -isnot [string] -or $revision -isnot [string] -or $target -isnot [string] -or $files -isnot [array] -or $files.Count -lt 1){throw 'invalid-content-manifest'}
    Assert-ContentToken $contentSet;Assert-ContentRevision $revision;$token=Get-ContentTargetToken $target
    # object数はtransport.jsonを含めて数える。
    if($files.Count+1 -gt $script:ObjectLimit){throw 'size-limit'}
    $paths=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase);$dirs=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $entries=[Collections.Generic.List[object]]::new();$total=[long]0
    foreach($item in $files){
        $path=Get-ContentProperty $item 'path';$size=Get-ContentProperty $item 'size';$hash=Get-ContentProperty $item 'sha256'
        if($path -isnot [string] -or ($size -isnot [long] -and $size -isnot [int]) -or $hash -isnot [string]){throw 'invalid-content-manifest'}
        Assert-ContentPath $path
        try{Assert-Hex $hash 64}catch{throw 'invalid-content-manifest'}
        # 現行transportは1 byte未満のPUT・読戻しを拒否するため、0 byteは退避前に拒否する。
        if($size -eq 0){throw 'unsupported-content'}
        if($size -lt 0){throw 'invalid-content-manifest'}
        if($size -gt $script:FileLimit){throw 'size-limit'}
        $total+=$size;if($total -gt $script:TotalLimit){throw 'size-limit'}
        # 大文字小文字だけ違う重複と、fileとdirectoryの衝突（aとa/b）は別名として扱わず拒否する。
        if(-not $paths.Add($path) -or $dirs.Contains($path)){throw 'invalid-content-manifest'}
        $segments=$path.Split('/');$prefix=''
        for($i=0;$i -lt $segments.Count-1;$i++){$prefix+=$(if($i){'/'}else{''})+$segments[$i];if($paths.Contains($prefix)){throw 'invalid-content-manifest'};$null=$dirs.Add($prefix)}
        $entries.Add([pscustomobject]@{path=$path;bytes=[long]$size;sha256=$hash})
    }
    return [pscustomobject]@{ContentSet=$contentSet;Revision=$revision;Target=$target;TargetToken=$token;Sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant();ManifestBytes=[long]$Bytes.Length;Files=$entries.ToArray();TotalBytes=$total;ObjectCount=$entries.Count+1}
}
# opaqueな文字列でも復号後に所属・系列・hashを全て照合する。ここに含めるmanifestSha256は
# 取得側が照合に使うだけで、独立に渡された期待hashの代わりにはしない。
function Assert-ContentReference($v,$Config){
    Assert-Fields $v @('schemaVersion','purpose','repositoryId','contentSet','target','revision','seriesId','manifestSha256','objectCount','totalBytes')
    if($v.schemaVersion -isnot [long] -or $v.schemaVersion -ne 1 -or $v.purpose -cne 'content' -or $v.repositoryId -cne $Config.Data.repositoryId){throw 'invalid-reference'}
    foreach($name in @('repositoryId','contentSet','target','revision','seriesId','manifestSha256')){if($v.$name -isnot [string]){throw 'invalid-reference'}}
    try{
        $series=Get-ContentSeries $v.repositoryId $v.contentSet $v.target;Assert-ContentRevision $v.revision;Assert-Hex $v.manifestSha256 64
        if($v.seriesId -cne $series){throw 'invalid-reference'}
    }catch{throw 'invalid-reference'}
    foreach($name in @('objectCount','totalBytes')){if($v.$name -isnot [long] -and $v.$name -isnot [int]){throw 'invalid-reference'}}
    if($v.objectCount -lt 2 -or $v.objectCount -gt $script:ObjectLimit -or $v.totalBytes -lt 1 -or $v.totalBytes -gt $script:TotalLimit){throw 'invalid-reference'}
}
function New-ContentReference($Config,$Record){
    $v=[ordered]@{schemaVersion=[long]1;purpose='content';repositoryId=$Record.repositoryId;contentSet=$Record.contentSet;target=$Record.target;revision=$Record.revision;seriesId=$Record.seriesId;manifestSha256=$Record.manifestSha256;objectCount=[long]$Record.objectCount;totalBytes=[long]$Record.totalBytes}
    Assert-ContentReference ([pscustomobject]$v) $Config
    $raw=ConvertTo-Json -InputObject $v -Compress
    return $script:ReferencePrefix+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($raw)).TrimEnd('=').Replace('+','-').Replace('/','_')
}
function Read-ContentReference([string]$Text,$Config){
    if(-not $Text.StartsWith($script:ReferencePrefix,[StringComparison]::Ordinal) -or $Text.Length -gt 4096){throw 'invalid-reference'}
    $s=$Text.Substring($script:ReferencePrefix.Length)
    try{
        if($s -cnotmatch '\A[A-Za-z0-9_-]+\z'){throw 'invalid-reference'}
        $b=$s.Replace('-','+').Replace('_','/');$b=$b.PadRight($b.Length+((4-$b.Length%4)%4),'=')
        # 正規Base64url表現、厳密UTF-8、重複fieldの順に検査し、同じ内容に複数の表記が生まれる参照を認めない。
        $bytes=[Convert]::FromBase64String($b)
        if([Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+','-').Replace('/','_') -cne $s){throw 'invalid-reference'}
        $raw=[Text.UTF8Encoding]::new($false,$true).GetString($bytes);$doc=[Text.Json.JsonDocument]::Parse($raw)
        try{Assert-JsonDuplicates $doc.RootElement}finally{$doc.Dispose()}
        $v=$raw|ConvertFrom-Json -Depth 8 -DateKind String
        Assert-ContentReference $v $Config
    }catch{throw 'invalid-reference'}
    return $v
}
# content-configはbuild-configと同じ形の非秘密8 field。共有するのはRead-EvidenceConfigと
# Assert-ArtifactOperationFileだけで、8 fieldと許可値はBuildと独立に変わるためここで検査する。
function Read-ContentConfig([string]$Path){
    $full=[IO.Path]::GetFullPath($Path);$parent=[IO.Path]::GetDirectoryName($full)
    if(-not [IO.Path]::IsPathFullyQualified($Path) -or $full -cne $Path -or $parent -cnotmatch '[\\/][0-9a-f]{32}$'){throw 'invalid-content-config'}
    try{
        Assert-ArtifactOperationFile $parent $full
        $item=Read-ArtifactJson $full;$v=$item.Data
        Assert-Fields $v @('schemaVersion','purpose','profile','evidenceConfigPath','evidenceConfigSha256','repositoryId','prefix','policy')
        if($v.schemaVersion -isnot [long] -or $v.schemaVersion -ne 1 -or $v.purpose -cne 'content' -or
            $v.profile -cne 'osm' -or $v.prefix -cne 'development/content/v1/' -or
            $v.policy -cne 'content-publish-v1' -or $v.evidenceConfigPath -isnot [string] -or
            -not [IO.Path]::IsPathFullyQualified($v.evidenceConfigPath)){throw 'invalid-content-config'}
        Assert-Hex $v.repositoryId 64;Assert-Hex $v.evidenceConfigSha256 64
        $evidence=Read-EvidenceConfig $v.evidenceConfigPath
        if($evidence.Hash -cne $v.evidenceConfigSha256 -or $evidence.Data.repositoryId -cne $v.repositoryId){throw 'invalid-content-config'}
    }catch{throw 'invalid-content-config'}
    return @{Data=[pscustomobject]@{repositoryId=$v.repositoryId;endpoint=$evidence.Data.endpoint;bucket=$evidence.Data.bucket;prefix=$v.prefix};Hash=$item.Sha256;EvidenceConfigSha256=$evidence.Hash}
}
# intentは最初の送信前に固定する決定的な送信計画。operation pathや時刻は持たず、
# 同じ入力なら常に同じbytesになる（再実行時の「違えばintent-conflict」の前提）。
function New-ContentIntent($Config,$Manifest){
    $series=Get-ContentSeries $Config.Data.repositoryId $Manifest.ContentSet $Manifest.TargetToken
    return [ordered]@{schemaVersion=1;purpose='content';repositoryId=$Config.Data.repositoryId;seriesId=$series;contentSet=$Manifest.ContentSet;target=$Manifest.TargetToken;revision=$Manifest.Revision;configSha256=$Config.Hash;evidenceConfigSha256=$Config.EvidenceConfigSha256;manifestSha256=$Manifest.Sha256;objectCount=[long]$Manifest.ObjectCount;totalBytes=[long]$Manifest.TotalBytes;entries=@($Manifest.Files|ForEach-Object{[pscustomobject]@{path=$_.path;bytes=$_.bytes;sha256=$_.sha256}})}
}
function New-ContentReceipt($Intent,[string]$IntentSha256,[string]$PublishedAt){
    return [ordered]@{schemaVersion=1;purpose='content';repositoryId=$Intent.repositoryId;seriesId=$Intent.seriesId;contentSet=$Intent.contentSet;target=$Intent.target;revision=$Intent.revision;manifestSha256=$Intent.manifestSha256;objectCount=$Intent.objectCount;totalBytes=$Intent.totalBytes;intentSha256=$IntentSha256;publishState='succeeded';publishedAt=$PublishedAt}
}
Export-ModuleMember -Function Assert-ContentToken,Assert-ContentRevision,Get-ContentTargetToken,Assert-ContentTargetToken,Get-ContentSeries,Assert-ContentPath,Get-ContentKey,Read-ContentManifestFile,ConvertFrom-ContentManifest,New-ContentReference,Read-ContentReference,Read-ContentConfig,New-ContentIntent,New-ContentReceipt
