Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'Credentials/CredentialStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'Transport/R2ArtifactTransport.psm1')
$script:TestRoot=$null
$script:TransportHook=$null
$script:FaultHook=$null
$script:Clock={ [DateTimeOffset]::UtcNow }
function Get-EvidenceResetRoot {
    if($script:TestRoot){return [IO.Path]::GetFullPath($script:TestRoot)}
    return [IO.Path]::Combine([Environment]::GetFolderPath('LocalApplicationData'),'OneStarMaker','Artifacts')
}
function Assert-ResetFields($Value,[string[]]$Fields){
    if($Value -isnot [Collections.IDictionary] -or @($Value.Keys).Count -ne $Fields.Count){throw 'reset-schema-invalid'}
    foreach($field in $Fields){if(-not $Value.Contains($field)){throw 'reset-schema-invalid'}}
}
function Assert-ResetJsonProperties($Element) {
    if($Element.ValueKind -eq [Text.Json.JsonValueKind]::Object){
        $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach($property in $Element.EnumerateObject()){if(-not $seen.Add($property.Name)){throw 'reset-schema-invalid'};Assert-ResetJsonProperties $property.Value}
    }elseif($Element.ValueKind -eq [Text.Json.JsonValueKind]::Array){foreach($item in $Element.EnumerateArray()){Assert-ResetJsonProperties $item}}
}
function Read-ResetJson([string]$Path,[string]$Hash){
    if($Hash -cnotmatch '^[a-f0-9]{64}$'){throw 'reset-hash-invalid'}
    Assert-NoArtifactReparse $Path
    if(-not [IO.File]::Exists($Path) -or [IO.FileInfo]::new($Path).Length -gt 1MB -or (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $Hash){throw 'reset-hash-mismatch'}
    $document=$null
    try{$document=[Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($Path));Assert-ResetJsonProperties $document.RootElement}catch{throw 'reset-schema-invalid'}finally{if($document){$document.Dispose()}}
    try{return ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($Path)) -AsHashtable -DateKind String -Depth 30}catch{throw 'reset-schema-invalid'}
}
function Get-ResetKnownKeys {
    $repo='4d2d1728f1db77f50605a10da0bbf10fd31443c381c4f312658134fffb850b3b'
    $e1="evidence/first-use/$repo/caed8bae55c033673b75532dc650f0a3a3cedc48/64c42fc4d5d24fa580af7cc12d3a3c92/"
    $e2="evidence/first-use/$repo/896eaea8a912248333704c80549d46ecf20c02c6/1495786d921949fca6e92edbef21010e/"
    return @(($e1+'bundle.zip'),($e2+'bundle.zip'),($e1+'protection-witness.txt'),($e2+'protection-witness.txt'),'probe/unlocked/8b4830f6345d462f9db4ce07b47e5240/existing.txt','probe/locked/8b4830f6345d462f9db4ce07b47e5240/existing.txt')
}
function Get-ResetRuleTuple([string]$TargetId){
    switch($TargetId){
        'continuity-w'{return @{ruleId='retention_continuity_w_8b4830f6345d462f9db4ce07b47e5240';enabled=$true;prefix='probe/unlocked/8b4830f6345d462f9db4ce07b47e5240/existing.txt';condition=@{type='Indefinite';retentionSeconds=$null}}}
        'continuity-l'{return @{ruleId='retention_continuity_l_8b4830f6345d462f9db4ce07b47e5240';enabled=$true;prefix='probe/locked/8b4830f6345d462f9db4ce07b47e5240/existing.txt';condition=@{type='Indefinite';retentionSeconds=$null}}}
        'first-use-age30d'{return @{ruleId=$null;enabled=$true;prefix='evidence/first-use/';condition=@{type='Age';retentionSeconds=2592000}}}
        'probe-age1d'{return @{ruleId=$null;enabled=$true;prefix='probe/locked/';condition=@{type='Age';retentionSeconds=86400}}}
        default{throw 'reset-rule-out-of-scope'}
    }
}
function Assert-ResetTuple($Tuple,$Expected){
    Assert-ResetFields $Tuple @('enabled','prefix','condition')
    Assert-ResetFields $Tuple.condition @('type','retentionSeconds')
    if($Tuple.enabled -isnot [bool] -or $Tuple.enabled -ne $Expected.enabled -or $Tuple.prefix -cne $Expected.prefix -or $Tuple.condition.type -cne $Expected.condition.type -or ($null -ne $Expected.condition.retentionSeconds -and $Tuple.condition.retentionSeconds -isnot [int] -and $Tuple.condition.retentionSeconds -isnot [long]) -or $Tuple.condition.retentionSeconds -cne $Expected.condition.retentionSeconds){throw 'reset-rule-tuple-mismatch'}
}
function Assert-ResetUtc($Value){
    $utc=[DateTimeOffset]::MinValue
    if($Value -isnot [string] -or -not [DateTimeOffset]::TryParse($Value,[ref]$utc)){throw 'reset-time-invalid'}
}
function Test-ResetOverlap([string]$Path,[string]$Other){return (Test-ArtifactWithin $Path $Other) -or (Test-ArtifactWithin $Other $Path)}
function Assert-EvidenceResetManifest($Manifest){
    Assert-ResetFields $Manifest @('schemaVersion','purpose','repositoryId','endpoint','bucket','deploymentId','observedAt','frozenAt','objects','localTargets','rules','excludedKeys','excludedPaths','preservedRules')
    if(($Manifest.schemaVersion -isnot [int] -and $Manifest.schemaVersion -isnot [long]) -or $Manifest.schemaVersion -ne 1 -or $Manifest.purpose -cne 'development-reset-v1' -or $Manifest.repositoryId -cne '4d2d1728f1db77f50605a10da0bbf10fd31443c381c4f312658134fffb850b3b' -or $Manifest.endpoint -cnotmatch '^https://[a-f0-9]{32}\.r2\.cloudflarestorage\.com$' -or $Manifest.bucket -cne 'osm-artifacts' -or $Manifest.deploymentId -cnotmatch '^[a-f0-9]{32}$'){throw 'reset-identity-invalid'}
    Assert-ResetUtc $Manifest.observedAt;Assert-ResetUtc $Manifest.frozenAt
    foreach($name in @('objects','localTargets','rules','excludedKeys','excludedPaths','preservedRules')){if($Manifest[$name] -isnot [array]){throw 'reset-schema-invalid'}}
    $keys=Get-ResetKnownKeys;$seen=@{}
    foreach($object in $Manifest.objects){
        Assert-ResetFields $object @('key','expectedBytes','expectedHash','etag','observedExists','relatedRuleIds')
        if($object.key -cnotin $keys -or $seen.ContainsKey($object.key) -or $object.observedExists -isnot [bool] -or $object.expectedBytes -isnot [long] -and $object.expectedBytes -isnot [int] -or $object.expectedBytes -lt 0 -or $object.expectedBytes -gt 256MB -or ($object.expectedHash -cnotmatch '^[a-f0-9]{64}$' -and $object.etag -cnotmatch '^[a-fA-F0-9]{32}(?:-[0-9]+)?$')){throw 'reset-object-out-of-scope'}
        if($object.key -cin $Manifest.excludedKeys){throw 'reset-exclusion-conflict'}
        $seen[$object.key]=$true
    }
    $ruleIds=@{}
    foreach($rule in $Manifest.rules){
        Assert-ResetFields $rule @('targetId','selector','ruleId','enabled','prefix','condition','approvedMatchingScope','excludedKeys','action')
        $known=Get-ResetRuleTuple $rule.targetId
        Assert-ResetTuple @{enabled=$rule.enabled;prefix=$rule.prefix;condition=$rule.condition} $known
        if($ruleIds.ContainsKey($rule.targetId) -or $rule.ruleId -cne $known.ruleId -or $rule.selector -cne $(if($known.ruleId){'exact-id'}else{'exact-tuple'}) -or $rule.action -cne 'remove'){throw 'reset-rule-out-of-scope'}
        foreach($key in $rule.approvedMatchingScope){if($key -cnotin $keys -or -not $key.StartsWith($rule.prefix,[StringComparison]::Ordinal)){throw 'reset-rule-scope-mismatch'}}
        foreach($key in $rule.excludedKeys){if($key -cin $rule.approvedMatchingScope){throw 'reset-exclusion-conflict'}}
        $ruleIds[$rule.targetId]=$true
    }
    foreach($object in $Manifest.objects){foreach($id in $object.relatedRuleIds){if(-not $ruleIds.ContainsKey($id)){throw 'reset-rule-reference-invalid'}}}
    $root=Get-EvidenceResetRoot;$seenPaths=@()
    $knownPaths=@('evidence-ledgers/8dd797d6e2f14258978185eef15dade2/ledger.json','evidence-ledgers/767d073dec92416281d9b03f7e58f527/ledger.json','evidence-retention/8fe65074daf5379c4a311ec5166b21ff/retention.json','evidence-handoffs/d79fb66106844337b4c1a0f0c311d2e6/reader-input.json','transfers/5a2bd180c4c04112bd1d2de71bc6ebb2/ready','transfers/c0e929c6b74a4b4eaddd9300cf447376/ready','review-evidence/evidence-first-use-judgment-04a488a71a464d1fb405d002b33745ac','review-evidence/evidence-first-use-judgment-reader-62f39c71ebb34b49a434c29105752e27')
    foreach($target in $Manifest.localTargets){
        Assert-ResetFields $target @('relativePath','resolvedPath','ownerTask','kind','observedExists','entries','source','inUse')
        if($target.relativePath -cnotin $knownPaths -or $target.source -isnot [bool] -or $target.source -or $target.inUse -isnot [bool] -or $target.inUse -or $target.observedExists -isnot [bool] -or $target.ownerTask -cnotmatch '^[a-z0-9][a-z0-9-]{0,63}$' -or $target.kind -cnotin @('old-evidence-tool-record','storage-fetch-copy','old-evidence-tool-output')){throw 'reset-local-out-of-scope'}
        $full=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$target.relativePath))
        if(-not $full.Equals($target.resolvedPath,[StringComparison]::OrdinalIgnoreCase) -or -not (Test-ArtifactWithin $full $root) -or $full.Equals($root,[StringComparison]::OrdinalIgnoreCase)){throw 'reset-path-invalid'}
        Assert-NoArtifactReparse $full
        foreach($prior in $seenPaths){if(Test-ResetOverlap $full $prior){throw 'reset-duplicate-path'}}
        foreach($excluded in $Manifest.excludedPaths){if(Test-ResetOverlap $full $excluded){throw 'reset-exclusion-conflict'}}
        $seenPaths+=@($full);$entryPaths=@{}
        foreach($entry in $target.entries){
            Assert-ResetFields $entry @('path','bytes','sha256')
            if($entry.path -isnot [string] -or $entry.path -cmatch '(^/|:|\\|(^|/)\.\.(/|$))' -or $entryPaths.ContainsKey($entry.path.ToLowerInvariant()) -or ($entry.bytes -isnot [int] -and $entry.bytes -isnot [long]) -or $entry.bytes -lt 0 -or $entry.sha256 -cnotmatch '^[a-f0-9]{64}$'){throw 'reset-entry-invalid'}
            $entryPaths[$entry.path.ToLowerInvariant()]=$true
        }
    }
    return $Manifest
}
function Assert-EvidenceResetVerification($Receipt,$Manifest,[string]$ManifestHash){
    Assert-ResetFields $Receipt @('schemaVersion','purpose','frozenManifestSha256','verifications')
    if(($Receipt.schemaVersion -isnot [int] -and $Receipt.schemaVersion -isnot [long]) -or $Receipt.schemaVersion -ne 1 -or $Receipt.purpose -cne 'owner-rule-verification' -or $Receipt.frozenManifestSha256 -cne $ManifestHash -or @($Receipt.verifications).Count -ne @($Manifest.rules).Count){throw 'reset-verification-invalid'}
    $targets=@{};$actualIds=@{}
    foreach($verification in $Receipt.verifications){
        Assert-ResetFields $verification @('targetId','actualRuleId','matchedTuple','excludesUnaffected','observer','observedAt','statement')
        $rule=@($Manifest.rules | Where-Object targetId -CEQ $verification.targetId)
        if($rule.Count -ne 1 -or $targets.ContainsKey($verification.targetId) -or $actualIds.ContainsKey($verification.actualRuleId) -or $verification.actualRuleId -cnotmatch '^[a-zA-Z0-9_.-]{1,128}$' -or $verification.excludesUnaffected -isnot [bool] -or -not $verification.excludesUnaffected -or [string]::IsNullOrWhiteSpace($verification.observer) -or [string]::IsNullOrWhiteSpace($verification.statement)){throw 'reset-verification-invalid'}
        if($rule[0].ruleId -and $rule[0].ruleId -cne $verification.actualRuleId){throw 'reset-rule-id-mismatch'}
        Assert-ResetTuple $verification.matchedTuple (Get-ResetRuleTuple $verification.targetId)
        Assert-ResetUtc $verification.observedAt
        $targets[$verification.targetId]=$true;$actualIds[$verification.actualRuleId]=$true
    }
}
function Write-ResetState([string]$Path,$Value){
    Assert-NoArtifactReparse $Path
    $temp=$Path+'.'+[Guid]::NewGuid().ToString('N')+'.tmp'
    $stream=[IO.FileStream]::new($temp,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{Set-ArtifactAcl $temp $false;$bytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -Depth 30 -Compress -InputObject $Value));$stream.Write($bytes);$stream.Flush($true)}finally{$stream.Dispose()}
    try{[IO.File]::Move($temp,$Path,$true)}finally{if([IO.File]::Exists($temp)){[IO.File]::Delete($temp)}}
}
# The cutover record contains observations of the already authorized old-entry shutdown
# and owner rule changes; it neither grants a new deletion approval nor changes the manifest.
function Register-EvidenceResetCutover([string]$ManifestHash,[string]$RecordPath,[string]$RecordHash){
    $record=Read-ResetJson $RecordPath $RecordHash
    Assert-ResetFields $record @('schemaVersion','purpose','frozenManifestSha256','oldEntryDisabled','childrenStopped','rulesChanged','observer','observedAt','statement')
    if($record.schemaVersion -ne 1 -or $record.purpose -cne 'reset-cutover-observation' -or $record.frozenManifestSha256 -cne $ManifestHash -or $record.oldEntryDisabled -isnot [bool] -or -not $record.oldEntryDisabled -or $record.childrenStopped -isnot [bool] -or -not $record.childrenStopped -or $record.rulesChanged -isnot [bool] -or -not $record.rulesChanged -or -not $record.observer -or -not $record.statement){throw 'reset-cutover-incomplete'}
    Assert-ResetUtc $record.observedAt
    $directory=[IO.Path]::Combine((Get-EvidenceResetRoot),'resets',$ManifestHash)
    Assert-NoArtifactReparse $directory
    if(-not [IO.Directory]::Exists($directory)){[IO.Directory]::CreateDirectory($directory)|Out-Null;Set-ArtifactAcl $directory $true}
    $path=[IO.Path]::Combine($directory,'cutover.json')
    if([IO.File]::Exists($path)){if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $RecordHash){throw 'reset-cutover-conflict'};return}
    [IO.File]::Copy($RecordPath,$path,$false);Set-ArtifactAcl $path $false
    Write-ResetState ([IO.Path]::Combine($directory,'cutover-reference.json')) @{sha256=$RecordHash}
}
function Get-ResetLocalEntries($Target){
    $path=$Target.resolvedPath;Assert-NoArtifactReparse $path
    if([IO.File]::Exists($path)){return @(@{path=[IO.Path]::GetFileName($path);bytes=[IO.FileInfo]::new($path).Length;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()})}
    if(-not [IO.Directory]::Exists($path)){return @()}
    $files=[Collections.Generic.List[object]]::new();$queue=[Collections.Generic.Queue[string]]::new();$queue.Enqueue($path)
    while($queue.Count){
        $directory=$queue.Dequeue();Assert-NoArtifactReparse $directory
        foreach($entry in [IO.Directory]::EnumerateFileSystemEntries($directory)){
            Assert-NoArtifactReparse $entry
            if([IO.Directory]::Exists($entry)){$queue.Enqueue($entry)}else{$files.Add(@{path=[IO.Path]::GetRelativePath($path,$entry).Replace('\','/');bytes=[IO.FileInfo]::new($entry).Length;sha256=(Get-FileHash -LiteralPath $entry -Algorithm SHA256).Hash.ToLowerInvariant()})}
        }
    }
    return @($files)
}
function Assert-ResetLocalUnchanged($Target){
    $actual=@(Get-ResetLocalEntries $Target)
    if($actual.Count -eq 0 -and -not [IO.File]::Exists($Target.resolvedPath) -and -not [IO.Directory]::Exists($Target.resolvedPath)){return}
    if($actual.Count -ne @($Target.entries).Count){throw 'reset-local-changed'}
    foreach($entry in $actual){$expected=@($Target.entries | Where-Object path -CEQ $entry.path);if($expected.Count -ne 1 -or $expected[0].bytes -ne $entry.bytes -or $expected[0].sha256 -cne $entry.sha256){throw 'reset-local-changed'}}
}
function Invoke-ResetTransport([string]$Profile,$Manifest,[string]$Operation,[string]$Key,[string]$Destination,[int]$Deadline){
    $request=@{Endpoint=$Manifest.endpoint;Operation=$Operation;Key=$Key;SourcePath='';DestinationPath=$Destination;DeadlineMilliseconds=$Deadline;ResetAuthorized=$true}
    if($script:TransportHook){return & $script:TransportHook $request}
    $callback={param($id,$secret,$generation,$details)Invoke-R2ArtifactOperation $id $secret $generation $details}
    return Invoke-CredentialTransport $Profile $callback $request
}
function Test-ResetMissing($Observation){return $Observation.Operation -ceq 'get' -and $Observation.HttpStatus -eq 404 -and $Observation.StatusClass -ceq 'not-found' -and $Observation.S3Code -ceq 'NoSuchKey' -and $Observation.StatusObserved -and -not $Observation.TimedOut -and -not $Observation.Redirected}
function Test-ResetSuccess($Observation,[string]$Operation){return $Observation.Operation -ceq $Operation -and $Observation.HttpStatus -ge 200 -and $Observation.HttpStatus -le 299 -and $Observation.StatusClass -ceq 'success' -and $Observation.StatusObserved -and -not $Observation.TimedOut -and -not $Observation.Redirected}
function Invoke-EvidenceReset([string]$ManifestPath,[string]$ManifestSha256,[string]$OwnerVerificationPath,[string]$OwnerVerificationSha256,[string]$Profile='osm',[bool]$DryRun=$false){
    if($Profile -cne 'osm'){throw 'reset-profile-invalid'}
    $manifest=Assert-EvidenceResetManifest (Read-ResetJson $ManifestPath $ManifestSha256)
    $verification=Read-ResetJson $OwnerVerificationPath $OwnerVerificationSha256
    Assert-EvidenceResetVerification $verification $manifest $ManifestSha256
    if($DryRun){return @{status='passed';reasonCode='dry-run';objects=@($manifest.objects).Count;localTargets=@($manifest.localTargets).Count;networkCalls=0}}
    $directory=[IO.Path]::Combine((Get-EvidenceResetRoot),'resets',$ManifestSha256)
    Assert-NoArtifactReparse $directory;Assert-ArtifactAcl $directory $true
    $cutoverPath=[IO.Path]::Combine($directory,'cutover.json');$referencePath=[IO.Path]::Combine($directory,'cutover-reference.json')
    Assert-ArtifactAcl $referencePath $false
    $reference=ConvertFrom-Json -AsHashtable -InputObject ([IO.File]::ReadAllText($referencePath))
    $cutover=Read-ResetJson $cutoverPath $reference.sha256
    if($cutover.frozenManifestSha256 -cne $ManifestSha256 -or -not $cutover.oldEntryDisabled -or -not $cutover.childrenStopped -or -not $cutover.rulesChanged){throw 'reset-cutover-incomplete'}
    $lockPath=[IO.Path]::Combine($directory,'reset.lock');Assert-NoArtifactReparse $lockPath
    try{$lock=[IO.FileStream]::new($lockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch [IO.IOException]{throw 'reset-busy'}
    try{
        Set-ArtifactAcl $lockPath $false
        $path=[IO.Path]::Combine($directory,'receipt.json')
        $state=if([IO.File]::Exists($path)){Assert-ArtifactAcl $path $false;ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($path)) -AsHashtable -DateKind String -Depth 30}else{@{schemaVersion=1;manifestSha256=$ManifestSha256;ownerVerificationSha256=$OwnerVerificationSha256;items=@()}}
        if($state.manifestSha256 -cne $ManifestSha256 -or $state.ownerVerificationSha256 -cne $OwnerVerificationSha256){throw 'reset-receipt-conflict'}
        foreach($target in $manifest.localTargets){Assert-ResetLocalUnchanged $target}
        $watch=[Diagnostics.Stopwatch]::StartNew();$blocked=0;$deleted=0;$skipped=0
        foreach($item in @($manifest.objects)+@($manifest.localTargets)){
            $remote=$item.Contains('key');$id=if($remote){$item.key}else{$item.relativePath}
            $done=@($state.items | Where-Object {$_.target -ceq $id -and $_.status -eq 'deleted'})
            if($done.Count){$skipped++;continue}
            $operationId=[Guid]::NewGuid().ToString('N');$observation=$null
            try{
                if($remote){
                    if($watch.Elapsed.TotalSeconds -ge 600){throw 'reset-budget-exhausted'}
                    $download=[IO.Path]::Combine($directory,$operationId+'.readback')
                    # GET requires an unoccupied CreateNew destination before HTTP.
                    # Retain the identity download while giving the absence probe
                    # its own tool-owned path, so local bytes cannot hide GET 404.
                    $confirmation=[IO.Path]::Combine($directory,$operationId+'.absence')
                    try{
                        $deadline=[Math]::Min(120000,600000-[int]$watch.ElapsedMilliseconds)
                        $observation=Invoke-ResetTransport $Profile $manifest 'get' $item.key $download $deadline
                        if(-not (Test-ResetMissing $observation)){
                            if(-not (Test-ResetSuccess $observation 'get') -or $observation.Bytes -ne $item.expectedBytes){throw 'reset-object-changed-or-unresolved'}
                            # Hash and ETag are the frozen identity alternatives. Prefer the
                            # valid expected SHA-256 when supplied, otherwise require ETag.
                            if($item.expectedHash -cmatch '^[a-f0-9]{64}$'){
                                if($observation.Sha256 -cne $item.expectedHash){throw 'reset-object-changed-or-unresolved'}
                            }else{
                                if($null -eq $observation.PSObject.Properties['ETag'] -and $observation -isnot [Collections.IDictionary]){throw 'reset-object-changed-or-unresolved'}
                                if(-not $observation.ETag -or $observation.ETag.ToLowerInvariant() -cne $item.etag.ToLowerInvariant()){throw 'reset-object-changed-or-unresolved'}
                            }
                            $delete=Invoke-ResetTransport $Profile $manifest 'delete' $item.key '' ([Math]::Min(120000,600000-[int]$watch.ElapsedMilliseconds))
                            if(-not (Test-ResetSuccess $delete 'delete')){throw 'reset-delete-unresolved'}
                            if($script:FaultHook){& $script:FaultHook 'after-remote-delete'}
                            $observation=Invoke-ResetTransport $Profile $manifest 'get' $item.key $confirmation ([Math]::Min(120000,600000-[int]$watch.ElapsedMilliseconds))
                            if(-not (Test-ResetMissing $observation)){throw 'reset-delete-unresolved'}
                        }
                    }finally{foreach($temporary in @($download,$confirmation)){if([IO.File]::Exists($temporary)){Assert-NoArtifactReparse $temporary;[IO.File]::Delete($temporary)}}}
                }else{
                    Assert-ResetLocalUnchanged $item
                    if($script:FaultHook){& $script:FaultHook 'before-local-delete'}
                    Assert-NoArtifactReparse $item.resolvedPath
                    # The resolved exact target is checked against its frozen relative allowlist.
                    # A root or ancestor never reaches Remove-Item, even for recursive targets.
                    if([IO.File]::Exists($item.resolvedPath) -or [IO.Directory]::Exists($item.resolvedPath)){Remove-Item -LiteralPath $item.resolvedPath -Recurse -Force -ErrorAction Stop}
                    if([IO.File]::Exists($item.resolvedPath) -or [IO.Directory]::Exists($item.resolvedPath)){throw 'reset-local-delete-unresolved'}
                }
                $state.items+=@(@{operationId=$operationId;target=$id;kind=$(if($remote){'remote'}else{'local'});status='deleted';reasonCode=$null;deletedAt=(& $script:Clock).ToUniversalTime().ToString('o');confirmedGet404=[bool]$remote});$deleted++
            }catch{
                $state.items+=@(@{operationId=$operationId;target=$id;kind=$(if($remote){'remote'}else{'local'});status='pending';reasonCode='reset-item-unresolved';deletedAt=$null;confirmedGet404=$false});$blocked++
            }
            Write-ResetState $path $state
        }
        return @{status=$(if($blocked){'pending'}else{'passed'});deleted=$deleted;skipped=$skipped;blocked=$blocked;receiptPath=$path;manifestSha256=$ManifestSha256}
    }finally{$lock.Dispose()}
}
Export-ModuleMember -Function Invoke-EvidenceReset
