Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildStage.psm1')
Import-Module (Join-Path $PSScriptRoot 'ContentContract.psm1')
# 退避先 <ArtifactsRoot>/content/<contentSet>/<target>/<revision>/ の所有者。成功確定点（ContentStoreのreceipt）
# とは寿命が違うので、receiptや予約には触れない。退避先は削除しない。copy→hash照合→renameの規則は
# Invoke-BuildStageを再利用し、安全規則の所有者を2つにしない。

function Get-ContentStageDestination([string]$ArtifactsRoot,[string]$ContentSet,[string]$TargetToken,[string]$Revision){
    Assert-ContentToken $ContentSet;Assert-ContentTargetToken $TargetToken;Assert-ContentRevision $Revision
    return [IO.Path]::Combine($ArtifactsRoot,'content',$ContentSet,$TargetToken,$Revision)
}
# 退避先に置く全entry（transport.jsonとcontent/*）。manifestと実fileの集合・size・hashが
# 完全一致する場合だけ退避し、不足・余剰・不一致は公開前に拒否する。
function Get-ContentExpectedEntries($Manifest){
    $entries=[Collections.Generic.List[object]]::new()
    $entries.Add([pscustomobject]@{path='transport.json';bytes=[long]$Manifest.ManifestBytes;sha256=$Manifest.Sha256})
    foreach($f in $Manifest.Files){$entries.Add([pscustomobject]@{path="content/$($f.path)";bytes=$f.bytes;sha256=$f.sha256})}
    return $entries.ToArray()
}
function Assert-ContentSet([string]$Root,$Expected,[string]$Code){
    $want=@($Expected|ForEach-Object{$_.path}|Sort-Object -CaseSensitive)
    if((@(Get-BuildInventory $Root) -join "`n") -cne ($want -join "`n")){throw $Code}
}
function Assert-ContentEntries([string]$Root,$Expected,[string]$Code){
    $byPath=@{};foreach($e in $Expected){$byPath[$e.path]=$e}
    foreach($actual in @(Get-BuildStageEntries $Root @($Expected|ForEach-Object{$_.path}))){
        $e=$byPath[$actual.path]
        if($actual.bytes -ne $e.bytes -or $actual.sha256 -cne $e.sha256){throw $Code}
    }
}
function Invoke-ContentStage([string]$Source,[string]$ArtifactsRoot,$Manifest){
    $expected=@(Get-ContentExpectedEntries $Manifest)
    Assert-ContentSet $Source $expected 'content-mismatch'
    $entries=@(Get-BuildStageEntries $Source @($expected|ForEach-Object{$_.path}))
    Assert-ContentEntries $Source $expected 'content-mismatch'
    $destination=Get-ContentStageDestination $ArtifactsRoot $Manifest.ContentSet $Manifest.TargetToken $Manifest.Revision
    # 同じrevisionの初回同時公開では、renameに負けた側もstage-conflictを受け取る。退避先が期待と完全一致なら
    # 勝った側が同じ内容を置いただけなので既存として扱い、排他はlock（publish-in-progress）に任せる。
    try{$stage=Invoke-BuildStage $Source $destination $entries $null $expected {param($dir)Get-BuildInventory $dir}}
    catch{
        if($_.Exception.Message -cne 'stage-conflict'){throw}
        try{Assert-ContentSet $destination $expected 'stage-conflict';Assert-ContentEntries $destination $expected 'stage-conflict'}catch{throw 'stage-conflict'}
        $stage='existing'
    }
    return [pscustomobject]@{Stage=$stage;Path=$destination;Entries=$expected}
}
# 公開は退避先をprivate operationへsnapshotして行い、ArtifactsRootのfileを直接送らない。
# snapshotへ写す前に退避先を再照合し、写した後のfileも同じ規則で確かめる。
function Copy-ContentSnapshot([string]$Stage,[string]$Snapshot,$Expected){
    Assert-ContentSet $Stage $Expected 'stage-conflict';Assert-ContentEntries $Stage $Expected 'stage-conflict'
    foreach($e in $Expected){
        $target=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($Snapshot,$e.path)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
        [IO.File]::Copy([OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($Stage,$e.path),$target,$false)
    }
    Assert-ContentSet $Snapshot $Expected 'snapshot-changed';Assert-ContentEntries $Snapshot $Expected 'snapshot-changed'
}
# 各PUTの直前に、送るfileのsizeとSHA-256をmanifestの値へ再照合する（snapshot後の差替えを検出）。
function Assert-ContentSnapshotFile([string]$Snapshot,$Entry){
    $actual=@(Get-BuildStageEntries $Snapshot @($Entry.path))[0]
    if($actual.bytes -ne $Entry.bytes -or $actual.sha256 -cne $Entry.sha256){throw 'snapshot-changed'}
}
Export-ModuleMember -Function Get-ContentStageDestination,Get-ContentExpectedEntries,Assert-ContentSet,Invoke-ContentStage,Copy-ContentSnapshot,Assert-ContentSnapshotFile
