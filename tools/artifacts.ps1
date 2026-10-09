param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    if ($Arguments.Count -lt 4) { throw 'Invalid command.' }
    if ($Arguments[0] -ceq 'credentials' -and $Arguments[1] -cin @('set','status','remove')) {
        # Existing local credential grammar and exit meanings remain unchanged.
        if ($Arguments[2] -cne '--profile' -or $Arguments[3] -cne 'osm') { throw 'Invalid command.' }
        $action = $Arguments[1]
        $replace = $false
        if ($action -eq 'set') {
            if ($Arguments.Count -eq 5 -and $Arguments[4] -ceq '--replace') { $replace = $true }
            elseif ($Arguments.Count -ne 4) { throw 'Invalid command.' }
        } elseif ($Arguments.Count -ne 4) { throw 'Invalid command.' }
        Import-Module (Join-Path $PSScriptRoot 'Artifacts/Credentials/CredentialCommands.psm1') -Force
        switch ($action) {
            'set' {
                $outcome = Invoke-CredentialSet 'osm' $replace
                if ($outcome.Outcome -eq 'committed with cleanup pending') {
                    [Console]::Error.WriteLine('Committed locally; cleanup pending. R2 connectivity unverified.')
                    exit 2
                }
                [Console]::WriteLine('Credential committed locally; R2 connectivity unverified.')
            }
            'status' { Invoke-CredentialStatus 'osm' | ForEach-Object { [Console]::WriteLine($_) } }
            'remove' { [Console]::WriteLine((Invoke-CredentialRemove 'osm')) }
        }
        exit 0
    }
    $command=$Arguments[0];$action=if($command -ceq 'evidence'){$Arguments[1]}else{$command}
    $offset=if($command -cin @('evidence','credentials')){2}else{1}
    if($action -ceq 'schedule'){$action='schedule-'+$Arguments[2];$offset=3}
    $opts=@{};$dry=$false
    for($i=$offset;$i -lt $Arguments.Count;$i++){
        $flag=$Arguments[$i]
        if($flag -ceq '--dry-run'){if($dry){throw 'unsupported-command'};$dry=$true;continue}
        if($flag -cnotmatch '\A--[a-z0-9-]+\z' -or $opts.ContainsKey($flag) -or $i+1 -ge $Arguments.Count){throw 'unsupported-command'}
        $opts[$flag]=$Arguments[++$i]
    }
    if($command -ceq 'credentials'){
        # Rotation grammar keeps the existing credential semantics.
        Import-Module (Join-Path $PSScriptRoot 'Artifacts/Credentials/CredentialRotation.psm1') -Force
        if($Arguments[1] -ceq 'rotate' -and $Arguments.Count -eq 6 -and $Arguments[2] -ceq '--profile' -and $Arguments[3] -ceq 'osm' -and $Arguments[4] -ceq '--config'){$outcome=Invoke-CredentialRotation $Arguments[5]}
        elseif($Arguments[1] -ceq 'confirm-revocation' -and $Arguments.Count -eq 10 -and $Arguments[2] -ceq '--profile' -and $Arguments[3] -ceq 'osm' -and $Arguments[4] -ceq '--config' -and $Arguments[6] -ceq '--generation' -and $Arguments[8] -ceq '--evidence'){$outcome=Invoke-CredentialRevocationConfirmation $Arguments[5] $Arguments[7] $Arguments[9]}
        else{throw 'unsupported-command'}
    }else{
        if($opts['--profile'] -cne 'osm'){throw 'unsupported-command'}
        $expected=switch($action){
            'publish'{if($command -cne 'evidence'){throw 'unsupported-command'};@('--profile','--config','--selection')}
            'fetch'{@('--profile','--config','--reference','--sha256')}
            'inspect'{@('--profile','--config','--task')}
            'use'{@('--profile','--config','--reference','--consumer','--until','--reason')}
            'release'{@('--profile','--config','--reference','--protection')}
            {$_ -cin @('cleanup','schedule-install','schedule-status')}{@('--profile','--config')}
            'reset'{@('--profile','--manifest','--manifest-sha256','--owner-verification','--owner-verification-sha256')}
            default{throw 'unsupported-command'}
        }
        if((($opts.Keys|Sort-Object) -join ',') -cne (($expected|Sort-Object) -join ',') -or ($dry -and $action -cnotin @('cleanup','reset'))){throw 'unsupported-command'}
        Import-Module (Join-Path $PSScriptRoot 'Artifacts/EvidenceApplication.psm1')
        switch($action){
            'publish'{$outcome=Invoke-EvidencePublish $opts['--config'] $opts['--selection']}
            'fetch'{$outcome=Invoke-EvidenceFetch $opts['--config'] $opts['--reference'] $opts['--sha256']}
            'inspect'{$outcome=Invoke-EvidenceInspect $opts['--config'] $opts['--task']}
            'use'{$outcome=Invoke-EvidenceUse $opts['--config'] $opts['--reference'] $opts['--consumer'] $opts['--until'] $opts['--reason']}
            'release'{$outcome=Invoke-EvidenceRelease $opts['--config'] $opts['--reference'] $opts['--protection']}
            'cleanup'{Import-Module (Join-Path $PSScriptRoot 'Artifacts/EvidenceCleanup.psm1');$outcome=Invoke-EvidenceCleanup $opts['--config'] $dry}
            'schedule-install'{Import-Module (Join-Path $PSScriptRoot 'Artifacts/EvidenceSchedule.psm1');$outcome=Invoke-EvidenceScheduleInstall $opts['--config']}
            'schedule-status'{Import-Module (Join-Path $PSScriptRoot 'Artifacts/EvidenceSchedule.psm1');$outcome=Get-EvidenceScheduleStatus $opts['--config']}
            'reset'{Import-Module (Join-Path $PSScriptRoot 'Artifacts/EvidenceReset.psm1');$outcome=Invoke-EvidenceReset $opts['--manifest'] $opts['--manifest-sha256'] $opts['--owner-verification'] $opts['--owner-verification-sha256'] 'osm' $dry}
        }
    }
    [Console]::WriteLine((ConvertTo-Json -InputObject $outcome -Compress -Depth 20))
    if ($outcome.status -ceq 'passed') { exit 0 }
    if ($outcome.status -ceq 'pending') { exit 2 }
    exit 1
} catch {
    # 例外文字列・stack trace には秘密やパスが混ざり得るため、一般診断だけ表示する。
    if($_.Exception.Message -ceq 'unsupported-command'){[Console]::WriteLine('{"status":"failed","reasonCode":"unsupported-command"}')}else{[Console]::Error.WriteLine('Artifact operation failed.')}
    exit 1
}
