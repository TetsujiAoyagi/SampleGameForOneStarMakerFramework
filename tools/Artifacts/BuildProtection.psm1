Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
# Buildの利用中保護の形と有効判定だけを持つ純粋な規則。file・store・時計に触れず、nowは呼出側が渡す。
# 記録の形はEvidenceの保護（owner/consumer/reason/createdAt/有限のuntil/releasedAt）に揃える。
# 「読めない」は「保護なし」と区別して unknown を返し、呼出側が系列を消さない側へ倒す。
$script:MaximumSpan=[TimeSpan]::FromDays(90)

# 記録1件（storeが返す {Id; Use; Release}）を検査する。期限が最長90日で有限であることも
# 書込み時だけでなく読取時に確かめ、手で置かれた無期限の保護を有効扱いしない。
function Test-BuildProtectionRecord($Record,[string]$BuildId){
    try{
        $u=$Record.Use
        Assert-Fields $u @('schemaVersion','buildId','id','owner','consumer','reason','createdAt','until')
        if($u.schemaVersion -isnot [long] -or $u.schemaVersion -ne 1 -or $u.buildId -cne $BuildId -or $u.id -cne $Record.Id -or
            $u.id -cnotmatch '\A[0-9a-f]{32}\z' -or $u.owner -isnot [string] -or $u.owner -cnotmatch '\AS-1-[0-9]+(?:-[0-9]+){1,14}\z' -or
            $u.consumer -isnot [string] -or $u.consumer -cnotmatch '\A[A-Za-z0-9][A-Za-z0-9-]{0,63}\z' -or
            $u.reason -isnot [string] -or [string]::IsNullOrWhiteSpace($u.reason) -or $u.reason.Length -gt 512 -or $u.reason -cmatch '[\x00-\x1f]' -or
            $u.createdAt -isnot [string] -or $u.until -isnot [string]){return $false}
        $created=Assert-Utc $u.createdAt;$until=Assert-Utc $u.until
        if($until -le $created -or ($until-$created) -gt $script:MaximumSpan){return $false}
        if($null -ne $Record.Release){
            $r=$Record.Release
            Assert-Fields $r @('schemaVersion','buildId','id','releasedAt')
            if($r.schemaVersion -isnot [long] -or $r.schemaVersion -ne 1 -or $r.buildId -cne $BuildId -or $r.id -cne $u.id -or $r.releasedAt -isnot [string]){return $false}
            if((Assert-Utc $r.releasedAt) -lt $created){return $false}
        }
        return $true
    }catch{return $false}
}
# 1 Buildの保護集合の状態: protected / unprotected / unknown。
# 未解除の記録は now < until の間有効。createdAt > now（use後に時計が巻き戻った）も有効とし、
# 巻戻りで保護が先に切れて消されることを防ぐ。1件でも読めなければ集合全体を unknown にする。
function Get-BuildProtectionState($Records,[string]$BuildId,[DateTimeOffset]$Now){
    $active=$false
    foreach($record in @($Records)){
        if(-not (Test-BuildProtectionRecord $record $BuildId)){return 'unknown'}
        if($null -eq $record.Release){
            $created=Assert-Utc $record.Use.createdAt;$until=Assert-Utc $record.Use.until
            if($Now -lt $until -or $created -gt $Now){$active=$true}
        }
    }
    if($active){return 'protected'};return 'unprotected'
}
Export-ModuleMember -Function Test-BuildProtectionRecord,Get-BuildProtectionState
