Set-StrictMode -Version Latest
function Get-EvidenceTestModules { param([string]$Name) $seen=[Collections.Generic.HashSet[object]]::new();$q=[Collections.Generic.Queue[object]]::new();foreach($m in @(Get-Module -All)){$q.Enqueue($m)};while($q.Count){$m=$q.Dequeue();if(-not $seen.Add($m)){continue};if($m.Name -ceq $Name){$m};foreach($n in $m.NestedModules){$q.Enqueue($n)}} }
function Set-EvidenceTestValue([string]$Module,[string]$Name,$Value){foreach($m in @(Get-EvidenceTestModules $Module)){& $m {param($n,$v)Set-Variable -Name $n -Value $v -Scope Script} $Name $Value}}
function Write-EvidenceTestJson([string]$Path,$Value){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))|Out-Null;[IO.File]::WriteAllText($Path,(ConvertTo-Json -InputObject $Value -Compress -Depth 24),[Text.UTF8Encoding]::new($false))}
function Assert-EvidenceTest([bool]$Condition,[string]$Message){if(-not $Condition){throw $Message}}
function New-EvidenceTestEnvironment {
    Import-Module (Join-Path $PSScriptRoot '../EvidenceApplication.psm1');Import-Module (Join-Path $PSScriptRoot '../EvidenceCleanup.psm1');Import-Module (Join-Path $PSScriptRoot '../../Workflow/TaskLifecycle.psm1');Import-Module (Join-Path $PSScriptRoot '../../Workflow/TaskEventStore.psm1');Import-Module (Join-Path $PSScriptRoot '../ArtifactAcl.psm1');Import-Module (Join-Path $PSScriptRoot '../EvidenceStateStore.psm1');Import-Module (Join-Path $PSScriptRoot '../EvidenceContract.psm1');Import-Module (Join-Path $PSScriptRoot '../EvidenceRetentionPolicy.psm1');Import-Module (Join-Path $PSScriptRoot '../EvidencePaths.psm1');Import-Module (Join-Path $PSScriptRoot '../ArtifactApplication.psm1')
    $root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-evidence-v2-'+[Guid]::NewGuid().ToString('N'));[IO.Directory]::CreateDirectory($root)|Out-Null
    Set-EvidenceTestValue 'TaskEventStore' 'TestRoot' ([IO.Path]::Combine($root,'workflow'));Set-EvidenceTestValue 'EvidencePaths' 'TestRoot' ([IO.Path]::Combine($root,'catalog'));Set-EvidenceTestValue 'ArtifactPaths' 'TestRoot' ([IO.Path]::Combine($root,'transfers'));Set-EvidenceTestValue 'EvidenceContract' 'DeploymentRoot' ([IO.Path]::Combine($root,'deployments'))
    $config=[ordered]@{schemaVersion=2;profile='osm';endpoint='https://'+('e'*32)+'.r2.cloudflarestorage.com';bucket='osm-artifacts';repositoryId='a'*64;prefix='development/evidence/v2/';policy='task-end-30d-v1';deploymentId='d'*32};$path=[IO.Path]::Combine($root,'deployments',('d'*32)+'.config.json');Write-EvidenceTestJson $path $config
    $hash=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant();Write-EvidenceTestJson ([IO.Path]::Combine($root,'deployments',('d'*32)+'.json')) ([ordered]@{schemaVersion=2;deploymentId='d'*32;repositoryId='a'*64;configSha256=$hash;baselineRecordId='fixture';baselineSha256='b'*64;ownerObservationId='fixture';ownerObservationSha256='c'*64})
    Set-EvidenceTestValue 'CredentialStore' 'TestRoot' ([IO.Path]::Combine($root,'credentials'))
    # Only the status read is replaced; no credential bytes exist in this fixture.
    foreach($m in @(Get-EvidenceTestModules 'EvidenceApplication')+@(Get-EvidenceTestModules 'EvidenceCleanup')){& $m {function script:Get-CredentialStatus {param($p)[pscustomobject]@{Generation='f'*32}}}}
    $objects=[Collections.Concurrent.ConcurrentDictionary[string,byte[]]]::new();$counts=[Collections.Concurrent.ConcurrentDictionary[string,int]]::new()
    $transport={param($gen,$request)
        $name=$request.Operation+' '+$request.Key;$counts.AddOrUpdate($name,1,{param($k,$v)$v+1})|Out-Null
        $status=200;$code=$null;$bytes=$null;$sha=$null;$class='success'
        if($request.Operation -ceq 'put'){if($objects.ContainsKey($request.Key)){throw 'duplicate-put'};$objects[$request.Key]=[IO.File]::ReadAllBytes($request.SourcePath)}
        elseif($request.Operation -ceq 'delete'){$unused=$null;$null=$objects.TryRemove($request.Key,[ref]$unused);$status=204}
        else{if(-not $objects.ContainsKey($request.Key)){$status=404;$class='not-found';$code='NoSuchKey'}else{$data=$objects[$request.Key];if($request.DestinationPath){[IO.File]::WriteAllBytes($request.DestinationPath,$data)};$bytes=[long]$data.Length;$sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant()}}
        [pscustomobject]@{Operation=$request.Operation;Generation=$gen;HttpStatus=$status;StatusClass=$class;S3Code=$code;Bytes=$bytes;Sha256=$sha;StatusObserved=$true;TimedOut=$false;Redirected=$false}
    }.GetNewClosure()
    $read={param($op,$c,$gen,$key,$bytes,$hash)$data=$objects[$key];$sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant();[pscustomobject]@{operation='readback';generation=$gen;key=$key;bytes=[long]$data.Length;sha256=$sha;childExit=0;childStatus='passed';observedAt=[DateTimeOffset]::UtcNow.ToString('o')}}.GetNewClosure()
    Set-EvidenceTestValue 'ArtifactApplication' 'TransportHook' $transport;Set-EvidenceTestValue 'ArtifactApplication' 'ReadbackHook' $read
    $g=Enter-WorkflowTaskGuard $config.repositoryId 'fixture';try{$null=Invoke-WorkflowTaskTransition $config.repositoryId 'fixture' 'started' 0 'fixture-start' '' $g}finally{$g.Dispose()}
    return [pscustomobject]@{Root=$root;ConfigPath=$path;Config=Read-EvidenceConfig $path;Objects=$objects;Counts=$counts;Transport=$transport;Readback=$read}
}
function Get-EvidenceTestBinaries {
    foreach($name in @('ArtifactPackaging','R2ArtifactTransport')){
        $assembly=[AppDomain]::CurrentDomain.GetAssemblies()|Where-Object{$_.GetName().Name -ceq $name}|Select-Object -First 1
        if($assembly){
            $deps=@($assembly.GetReferencedAssemblies()|ForEach-Object{
                $reference=$_;$dep=[AppDomain]::CurrentDomain.GetAssemblies()|Where-Object{$_.GetName().Name -ceq $reference.Name}|Select-Object -First 1
                [pscustomobject]@{name=$reference.Name;path=if($dep -and $dep.Location){$dep.Location}else{$null};sha256=if($dep -and $dep.Location){(Get-FileHash -LiteralPath $dep.Location).Hash.ToLowerInvariant()}else{$null}}
            })
            [pscustomobject]@{path=$assembly.Location;sha256=(Get-FileHash -LiteralPath $assembly.Location).Hash.ToLowerInvariant();moduleVersionId=$assembly.ManifestModule.ModuleVersionId.ToString();dependencies=$deps}
        }
    }
}
function Complete-EvidenceTestSuite([string]$ResultPath,[string[]]$Registered,[string[]]$Selected,$Executed,$Failed){
    if($ResultPath){Write-EvidenceTestJson $ResultPath ([ordered]@{registered=$Registered;selected=$Selected;executed=@($Executed);failed=@($Failed);binaries=@(Get-EvidenceTestBinaries)})}
    if($Failed.Count){throw 'Evidence suite failed.'}
}
