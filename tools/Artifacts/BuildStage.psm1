Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildContract.psm1')
# 退避先 <ArtifactsRoot>/players/... の所有者はこのmodule。publishの成功確定点
# （BuildStoreのreceipt rename）とは寿命が違うので、ここではreceiptや予約に触れない。
# 退避先はこのスライスでは削除しない。後始末するのは自分が作ったstagingだけ。
# 注入点はoffline検証専用。公開CLIから差し替えさせない。
$script:StageHook=$null

# ArtifactsRootは利用者が任意のローカルfolderを指定する。既定値を持たず、
# ここで作成もしない。存在しない値を作ると打ち間違いを新しい保存先として扱ってしまう。
# 環境変数は起動したprocessが引き継いだユーザー環境変数の値を読み、書き込まない。
function Resolve-BuildArtifactsRoot([string]$Explicit,[string]$Source){
    $value=if($Explicit){$Explicit}else{[Environment]::GetEnvironmentVariable('OSM_ARTIFACTS_ROOT')}
    if([string]::IsNullOrEmpty($value)){throw 'artifacts-root-not-configured'}
    if(-not [IO.Path]::IsPathFullyQualified($value)){throw 'invalid-artifacts-root'}
    $root=[IO.Path]::GetFullPath($value).TrimEnd('\')
    if(-not [IO.Directory]::Exists($root) -or [OneStarMaker.Artifacts.Packaging.PackagePolicy]::HasReparse($root) -or
        (Test-ArtifactWithin $root $Source) -or (Test-ArtifactWithin $Source $root)){throw 'invalid-artifacts-root'}
    # 所有store・Workflow・transfersとの親子拒否はpublishと同じ関数で行い、規則を二重化しない。
    try{Assert-BuildSourceRoot $root}catch{throw 'invalid-artifacts-root'}
    return $root
}
# 入力は配布形Playerの1 identityに限る。Unity生出力(work)やIL2CPP backupを含む集合は
# 上限を超えるうえ配布対象でもないので、退避・publishの前に拒否する。
function Assert-BuildStageSource([string]$Source,[string[]]$Inventory){
    if(-not [IO.Path]::IsPathFullyQualified($Source) -or -not [IO.Directory]::Exists($Source) -or
        [OneStarMaker.Artifacts.Packaging.PackagePolicy]::HasReparse($Source)){throw 'invalid-source'}
    $parts=@([IO.Path]::GetFullPath($Source).Split('\',[StringSplitOptions]::RemoveEmptyEntries))
    for($i=0;$i+2 -lt $parts.Count;$i++){if($parts[$i] -ieq 'artifacts' -and $parts[$i+1] -ieq 'bs4' -and $parts[$i+2] -ieq 'work'){throw 'invalid-source'}}
    if($Inventory -cnotcontains 'build-receipt.json'){throw 'invalid-source'}
    foreach($name in $Inventory){foreach($segment in $name.Split('/')){if($segment.EndsWith('_BackUpThisFolder_ButDontShipItWithYourGame',[StringComparison]::OrdinalIgnoreCase)){throw 'invalid-source'}}}
}
function Read-BuildStageBytes([string]$Path){
    if(-not [IO.Path]::IsPathFullyQualified($Path) -or -not [IO.File]::Exists($Path) -or
        [OneStarMaker.Artifacts.Packaging.PackagePolicy]::HasReparse($Path)){throw 'invalid-source'}
    $info=[IO.FileInfo]::new($Path);if($info.Length -lt 1 -or $info.Length -gt 1MB){throw 'invalid-source'}
    return [IO.File]::ReadAllBytes($Path)
}
# transport.jsonの場所はreceiptのtransportPathだけから決める。bytesの真正性は
# 呼出側がreceiptのmanifest hashと照合する。ここでは場所の安全性だけを見る。
function Read-BuildStageTransport([string]$TransportPath){
    if(-not [IO.Path]::IsPathFullyQualified($TransportPath) -or -not [IO.Directory]::Exists($TransportPath)){throw 'transport-mismatch'}
    try{return Read-BuildStageBytes ([IO.Path]::Combine($TransportPath,'transport.json'))}catch{throw 'transport-mismatch'}
}
function Get-BuildStageFileHash([string]$Path){
    $stream=[IO.FileStream]::new($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    try{return [pscustomobject]@{bytes=$stream.Length;sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()}}finally{$stream.Dispose()}
}
# 下位directoryへ入る前にreparseを拒否し、選択rootの外を走査しない。
# 内容の選別はしない。呼出側が明示した全file集合と一致することを後段で検査する。
function Get-BuildInventory([string]$Root){
    if([OneStarMaker.Artifacts.Packaging.PackagePolicy]::HasReparse($Root)){throw 'snapshot-conflict'}
    $names=[Collections.Generic.List[string]]::new();$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $pending=[Collections.Generic.Stack[string]]::new();$pending.Push($Root)
    while($pending.Count){
        $dir=$pending.Pop()
        foreach($item in [IO.Directory]::EnumerateFileSystemEntries($dir)){
            $attributes=[IO.File]::GetAttributes($item)
            if(($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'snapshot-conflict'}
            if(($attributes -band [IO.FileAttributes]::Directory) -ne 0){$pending.Push($item);continue}
            $relative=[IO.Path]::GetRelativePath($Root,$item).Replace([IO.Path]::DirectorySeparatorChar,'/')
            $null=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Relative($relative)
            if(-not $seen.Add($relative)){throw 'snapshot-conflict'}
            $names.Add($relative)
        }
    }
    return @($names.ToArray()|Sort-Object -CaseSensitive)
}
# 一覧はApplicationがGet-BuildInventory（上）で取って渡す。ここで別の走査規則を作らない。
function Get-BuildStageEntries([string]$Root,[string[]]$Files){
    foreach($name in $Files){
        $h=Get-BuildStageFileHash ([OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($Root,$name))
        [pscustomobject]@{path=$name;bytes=[long]$h.bytes;sha256=$h.sha256}
    }
}
function Get-BuildStageDestination([string]$ArtifactsRoot,$Series,[string]$BuildId){
    Assert-BuildId $BuildId
    return [IO.Path]::Combine($ArtifactsRoot,'players',$Series.project,$Series.target,$Series.configuration,$BuildId)
}
# 既存の退避先は「この入力から作るもの」と全file・size・hashが一致する場合だけ完了済みとする。
# 余分なfileや欠落があれば、別の入力または途中で変更された退避先として拒否する。
function Test-BuildStageMatch([string]$Directory,$Expected,[string[]]$Inventory){
    $want=@($Expected|Sort-Object path -CaseSensitive)
    if(($Inventory -join "`n") -cne (($want|ForEach-Object {$_.path}) -join "`n")){return $false}
    foreach($e in $want){
        $h=Get-BuildStageFileHash ([OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($Directory,$e.path))
        if($h.bytes -ne $e.bytes -or $h.sha256 -cne $e.sha256){return $false}
    }
    return $true
}
# copyで元を変えない。読んだbytesのhashを事前hashと照合し、書いたstagingも読み直して照合してから
# 同じ親directory内のrenameで公開する。renameは既存名を置換しないので、同じbuildIdの並行実行は
# 片方だけが公開でき、負けた側は自分のstagingを消して失敗を返す。
# InventoryReaderは退避先の一覧を取る関数で、Applicationから既存の走査規則を受け取る。
function Invoke-BuildStage([string]$Source,[string]$Destination,$SourceEntries,[byte[]]$InfoBytes,$Expected,[scriptblock]$InventoryReader){
    $parent=[IO.Path]::GetDirectoryName($Destination)
    # 既存の中間directoryがjunctionなら、作成前に止めてArtifactsRootの外へ書かない。
    if([OneStarMaker.Artifacts.Packaging.PackagePolicy]::HasReparse($parent)){throw 'stage-conflict'}
    [IO.Directory]::CreateDirectory($parent)|Out-Null
    if([OneStarMaker.Artifacts.Packaging.PackagePolicy]::HasReparse($parent)){throw 'stage-conflict'}
    if([IO.File]::Exists($Destination)){throw 'stage-conflict'}
    if([IO.Directory]::Exists($Destination)){
        if(Test-BuildStageMatch $Destination $Expected @(& $InventoryReader $Destination)){return 'existing'}
        throw 'stage-conflict'
    }
    $staging=[IO.Path]::Combine($parent,'.'+[IO.Path]::GetFileName($Destination)+'.'+[Guid]::NewGuid().ToString('N')+'.staging')
    [IO.Directory]::CreateDirectory($staging)|Out-Null
    $published=$false
    try{
        if($script:StageHook){& $script:StageHook 'before-copy' $staging}
        foreach($e in $SourceEntries){
            $target=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($staging,$e.path)
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
            $reader=[IO.FileStream]::new([OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($Source,$e.path),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
            try{
                $writer=[IO.FileStream]::new($target,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
                $hash=[Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
                try{
                    $buffer=[byte[]]::new(1MB);$bytes=[long]0;$n=0
                    while(($n=$reader.Read($buffer,0,$buffer.Length)) -gt 0){$writer.Write($buffer,0,$n);$hash.AppendData($buffer,0,$n);$bytes+=$n}
                    $writer.Flush($true)
                    if($bytes -ne $e.bytes -or [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant() -cne $e.sha256){throw 'stage-hash-mismatch'}
                }finally{$hash.Dispose();$writer.Dispose()}
            }finally{$reader.Dispose()}
        }
        # InfoBytesが$nullの呼出し（Content）はinfo fileを足さない。足す場合の挙動は従来どおり。
        if($null -ne $InfoBytes){
            $info=[IO.Path]::Combine($staging,'build-info.json')
            $stream=[IO.FileStream]::new($info,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
            try{$stream.Write($InfoBytes,0,$InfoBytes.Length);$stream.Flush($true)}finally{$stream.Dispose()}
        }
        if($script:StageHook){& $script:StageHook 'before-verify' $staging}
        # 書込み後に別handleで読み直す。copy中の差替えや書込み不良を公開前に検出する。
        if(-not (Test-BuildStageMatch $staging $Expected @(& $InventoryReader $staging))){throw 'stage-hash-mismatch'}
        if($script:StageHook){& $script:StageHook 'before-rename' $staging}
        try{[IO.Directory]::Move($staging,$Destination)}catch{throw 'stage-conflict'}
        $published=$true
        return 'created'
    }finally{
        if(-not $published -and [IO.Directory]::Exists($staging)){[IO.Directory]::Delete($staging,$true)}
    }
}
Export-ModuleMember -Function Resolve-BuildArtifactsRoot,Assert-BuildStageSource,Read-BuildStageBytes,Read-BuildStageTransport,Get-BuildInventory,Get-BuildStageEntries,Get-BuildStageDestination,Invoke-BuildStage
