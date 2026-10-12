Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
# 件数清掃の削除試行が残す観測の形と、その検査の所有者（BuildとContentが同じものを使う）。
# 記録だけから Test-ArtifactSuccess／Test-ArtifactMissing を再評価する前提なので、写像と検査の条件を
# 系列の種類ごとに持たない。状態を持たず、時計・network・storeに触れない（観測時刻は呼出側が渡す）。
# ArtifactApplicationをimportしないのは、記録を読むstoreに通信・資格情報の依存を持ち込まないため。
# 検査は判定関数を呼ばず、同じ条件を記録のfieldに対して判定する。
$script:StatusClasses=@('success','unauthorized','forbidden','not-found','too-many-requests','server-error','redirect','other','timeout','transport-error','invalid-input','size-limit')

# transportの返値を非秘密の固定fieldへ写す。操作名・generationの不一致や型・値域の外れた値は
# 「観測できなかった」へ倒して記録する。判定はこの記録から行うので、記録だけから再評価した
# Test-ArtifactSuccess/Test-ArtifactMissingの結果は、送信時の判定と常に一致する。
function ConvertTo-ArtifactObservation($Result,[string]$Operation,[string]$Key,[string]$Generation,[string]$ObservedAt){
    $o=[ordered]@{operation=$Operation;key=$Key;generation=$Generation;httpStatus=$null;statusClass=$null;s3Code=$null;statusObserved=$false;timedOut=$false;redirected=$false;observedAt=$ObservedAt}
    if($null -eq $Result){return $o}
    $value={param($n)$p=$Result.PSObject.Properties[$n];if($p){$p.Value}else{$null}}
    $http=& $value 'HttpStatus';$class=& $value 'StatusClass';$s3=& $value 'S3Code'
    $flags=@((& $value 'StatusObserved'),(& $value 'TimedOut'),(& $value 'Redirected'))
    if((& $value 'Operation') -cne $Operation -or (& $value 'Generation') -cne $Generation -or
        ($null -ne $http -and (($http -isnot [int] -and $http -isnot [long]) -or $http -lt 100 -or $http -gt 599)) -or
        ($null -ne $class -and $class -cnotin $script:StatusClasses) -or
        ($null -ne $s3 -and ($s3 -isnot [string] -or $s3 -cnotmatch '\A[A-Za-z][A-Za-z0-9]{0,63}\z')) -or
        @($flags|Where-Object {$_ -isnot [bool]}).Count){return $o}
    $o.httpStatus=if($null -eq $http){$null}else{[long]$http};$o.statusClass=$class;$o.s3Code=$s3
    $o.statusObserved=$flags[0];$o.timedOut=$flags[1];$o.redirected=$flags[2]
    return $o
}
function ConvertFrom-ArtifactObservation($o){
    return [pscustomobject]@{Operation=$o.operation;Generation=$o.generation;HttpStatus=$o.httpStatus;StatusClass=$o.statusClass;S3Code=$o.s3Code;StatusObserved=$o.statusObserved;TimedOut=$o.timedOut;Redirected=$o.redirected}
}
# 記録した観測の形を検査する。Outcome: 'missing' は削除済み（確定）の根拠（Test-ArtifactMissing と同じ
# 404 NoSuchKey の条件）、'present' は取下げの根拠（Test-ArtifactSuccess と同じ検証済み 2xx の条件）を、
# 読取時にも記録だけから再評価する。根拠に合わない記録を受け入れると、消えたobjectを成功に数える・
# 残るobjectを削除済みと扱う。
function Assert-ArtifactObservation($o,[string]$Operation,[string]$Key,[string]$Outcome='any'){
    Assert-Fields $o @('operation','key','generation','httpStatus','statusClass','s3Code','statusObserved','timedOut','redirected','observedAt')
    if($o.operation -cne $Operation -or $o.key -cne $Key -or
        ($null -ne $o.generation -and $o.generation -cnotmatch '\A[0-9a-f]{32}\z') -or
        ($null -ne $o.httpStatus -and ($o.httpStatus -isnot [long] -or $o.httpStatus -lt 100 -or $o.httpStatus -gt 599)) -or
        ($null -ne $o.statusClass -and $o.statusClass -cnotin $script:StatusClasses) -or
        ($null -ne $o.s3Code -and $o.s3Code -cnotmatch '\A[A-Za-z][A-Za-z0-9]{0,63}\z') -or
        $o.statusObserved -isnot [bool] -or $o.timedOut -isnot [bool] -or $o.redirected -isnot [bool] -or $o.observedAt -isnot [string]){throw 'delete-record-invalid'}
    $null=Assert-Utc $o.observedAt
    $clean=$null -ne $o.generation -and $o.statusObserved -and -not $o.timedOut -and -not $o.redirected
    if($Outcome -ceq 'missing' -and -not ($clean -and $o.httpStatus -eq 404 -and $o.statusClass -ceq 'not-found' -and $o.s3Code -ceq 'NoSuchKey')){throw 'delete-record-invalid'}
    if($Outcome -ceq 'present' -and -not ($clean -and $null -ne $o.httpStatus -and $o.httpStatus -ge 200 -and $o.httpStatus -le 299 -and $o.statusClass -ceq 'success')){throw 'delete-record-invalid'}
}
Export-ModuleMember -Function ConvertTo-ArtifactObservation,ConvertFrom-ArtifactObservation,Assert-ArtifactObservation
