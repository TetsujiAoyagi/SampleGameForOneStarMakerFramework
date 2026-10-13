Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'StoreConsolidationPlan.psm1')
Import-Module (Join-Path $PSScriptRoot '../Workflow/WindowsStoreContext.psm1')
Import-Module (Join-Path $PSScriptRoot '../Workflow/WindowsStorePaths.psm1')

# One-time consolidation of a split store: records that a virtualized process
# wrote into its package copy (the source) are copied, bytes unchanged, into the
# real store at the same relative position. The source is only ever opened with
# read-only handles. Nothing in the real store is overwritten or deleted except
# this run's own unpublished staging. The rules live in StoreConsolidationPlan.
#
# A run is owned by its approved manifest: runId is derived from the manifest
# hash, so re-executing the same manifest resumes the same run directory
# (Artifacts\consolidation\<runId>: lock, manifest copy, journal, staging and
# one immutable receipt per finished unit) and converges without rewriting.

$script:TestRoot=$null      # Offline tests only: replaces the real store root.
$script:FaultHook=$null     # Offline tests only: param($Stage,$Key) may throw.
$script:OpenHook=$null      # Offline tests only: param($Kind,$Path) observes I/O.
$script:Clock={[DateTimeOffset]::UtcNow}
$script:Journal=$null
$script:ReasonCodes=@('virtualized-store','store-context-unknown','invalid-source-root','invalid-output','manifest-hash-mismatch','manifest-invalid','consolidation-busy','run-conflict','store-path-unavailable')
# Names are only listed, never opened, unless they are a consolidated role.
$script:TopLevel=@{'Workflow'='role';'Harness'='role';'Artifacts'='artifacts';'RevisionLocks'='revision-locks';'H2a Review'='not-owned';'H2b Review'='not-owned';'WorktreeRecords'='not-owned'}
$script:ArtifactsLevel=@{'evidence-v2'='role';'deployments'='role';'transfers'='role';'credentials'='credentials';'builds-v1'='out-of-scope-role';'contents-v1'='out-of-scope-role';'runtime'='out-of-scope-role';'evidence-handoffs'='not-owned';'evidence-ledgers'='not-owned';'evidence-retention'='not-owned';'resets'='not-owned';'review-evidence'='not-owned';'consolidation'='consolidation-run'}

function Get-ConsolidationLogicalRoot {
    if($script:TestRoot){return [IO.Path]::GetFullPath($script:TestRoot)}
    return [IO.Path]::Combine([Environment]::GetFolderPath('LocalApplicationData'),'OneStarMaker')
}
function Write-ConsolidationTrace([string]$Kind,[string]$Path){
    if($script:OpenHook){& $script:OpenHook $Kind $Path}
    if($script:Journal){
        $line=(ConvertTo-Json -Compress -InputObject ([ordered]@{at=(& $script:Clock).ToString('o');kind=$Kind;path=$Path}))+"`n"
        $stream=[IO.FileStream]::new($script:Journal,[IO.FileMode]::Append,[IO.FileAccess]::Write,[IO.FileShare]::Read)
        try{$bytes=[Text.UTF8Encoding]::new($false).GetBytes($line);$stream.Write($bytes,0,$bytes.Length);$stream.Flush($true)}finally{$stream.Dispose()}
    }
}
function Test-ConsolidationReparse([string]$Path){return ([IO.File]::GetAttributes($Path) -band [IO.FileAttributes]::ReparsePoint) -ne 0}
# Lists names with their attributes from the directory enumeration; no entry is opened.
function Get-ConsolidationEntries([string]$Path){
    Write-ConsolidationTrace 'list' $Path
    foreach($entry in [IO.DirectoryInfo]::new($Path).EnumerateFileSystemInfos()){
        [pscustomobject]@{Name=$entry.Name;Directory=(($entry.Attributes -band [IO.FileAttributes]::Directory) -ne 0);Reparse=(($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)}
    }
}
function Get-ConsolidationTextStart([byte[]]$Bytes,[int]$Count){return [Text.UTF8Encoding]::new($false).GetString($Bytes,0,$Count).TrimStart([char]0xFEFF,' ',"`t","`r","`n")}
# Read-only handle. JSON-looking content up to 16MB is returned for reference checks.
# A larger file is only hashed. If it is named or looks like a JSON record it is
# marked unchecked, so the plan holds it instead of publishing strings nobody examined.
function Read-ConsolidationFile([string]$Path){
    Write-ConsolidationTrace 'read' $Path
    try{
        $stream=[IO.FileStream]::new($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
        try{
            $length=$stream.Length;$json=$null;$unchecked=$false
            if($length -le 16MB){
                $bytes=[byte[]]::new($length);$read=0;while($read -lt $length){$n=$stream.Read($bytes,$read,$length-$read);if($n -le 0){throw 'short-read'};$read+=$n}
                $hash=[Security.Cryptography.SHA256]::HashData($bytes)
                $text=Get-ConsolidationTextStart $bytes $bytes.Length
                if($text.StartsWith('{') -or $text.StartsWith('[')){$json=$text}
            }else{
                $head=[byte[]]::new(4096);$n=$stream.Read($head,0,$head.Length);$text=Get-ConsolidationTextStart $head $n
                # A start that is only padding cannot be told apart from a record either.
                $unchecked=$Path.EndsWith('.json',[StringComparison]::OrdinalIgnoreCase) -or $text.Length -eq 0 -or $text.StartsWith('{') -or $text.StartsWith('[')
                $null=$stream.Seek(0,[IO.SeekOrigin]::Begin);$hash=[Security.Cryptography.SHA256]::HashData($stream)
            }
            return [pscustomobject]@{bytes=[long]$length;sha256=[Convert]::ToHexString($hash).ToLowerInvariant();json=$json;unchecked=$unchecked}
        }finally{$stream.Dispose()}
    }catch{return [pscustomobject]@{bytes=[long]-1;sha256='unreadable';json=$null;unchecked=$false}}
}
# Inventories one directory item: every file with its hash, every subdirectory.
# A reparse point anywhere marks the item and is neither followed nor opened.
function Get-ConsolidationTree([string]$Root,$Item,[switch]$WithJson){
    $pending=[Collections.Generic.Stack[string]]::new();$pending.Push('')
    while($pending.Count){
        $relative=$pending.Pop();$path=if($relative){[IO.Path]::Combine($Root,$relative)}else{$Root}
        foreach($entry in @(Get-ConsolidationEntries $path)){
            $child=if($relative){$relative+'\'+$entry.Name}else{$entry.Name}
            if($entry.Reparse){$Item.reparse=$true;continue}
            if($entry.Directory){$Item.directories+=$child;$pending.Push($child);continue}
            $file=Read-ConsolidationFile ([IO.Path]::Combine($Root,$child))
            $Item.files+=[pscustomobject]@{path=$child;bytes=$file.bytes;sha256=$file.sha256;json=$(if($WithJson){$file.json}else{$null});unchecked=($WithJson -and $file.unchecked)}
        }
    }
}
function New-ConsolidationItem([string]$Role,[string]$Key,[string]$Repository='',[string]$Task='',[string]$Deployment=''){
    return [pscustomobject]@{role=$Role;key=$Key;repositoryId=$Repository;taskId=$Task;deploymentId=$Deployment;files=@();directories=@();reparse=$false}
}
function Get-ConsolidationSourceInventory([string]$SourceRoot,[string]$LogicalRoot){
    $items=[Collections.Generic.List[object]]::new();$excluded=[Collections.Generic.List[object]]::new();$operations=@{}
    function Exclude([string]$Key,[string]$Reason){$excluded.Add([pscustomobject]@{key=$Key;reason=$Reason})}
    # Walks fixed name patterns below a role; any other name is listed as excluded.
    function Walk([string]$Prefix,[string[]]$Patterns,[scriptblock]$OnLeaf,[string[]]$Captured=@()){
        $path=[IO.Path]::Combine($SourceRoot,$Prefix)
        foreach($entry in @(Get-ConsolidationEntries $path)){
            $key=$Prefix+'\'+$entry.Name
            if(-not $entry.Directory -or $entry.Reparse -or $entry.Name -cnotmatch $Patterns[0]){Exclude $key $(if($entry.Reparse){'reparse'}else{'unrecognized'});continue}
            $next=if($Patterns[0] -ceq '\Atasks\z'){$Captured}else{$Captured+$entry.Name}
            if($Patterns.Count -eq 1){& $OnLeaf $key $next}else{Walk $key @($Patterns|Select-Object -Skip 1) $OnLeaf $next}
        }
    }
    $repo='\A[0-9a-f]{64}\z';$task='\A[a-z0-9][a-z0-9-]{0,63}\z';$id='\A[0-9a-f]{32}\z'
    foreach($top in @(Get-ConsolidationEntries $SourceRoot)){
        $class=$script:TopLevel[$top.Name]
        if(-not $top.Directory -or $top.Reparse -or -not $class -or $class -cin @('revision-locks','not-owned')){Exclude $top.Name $(if($top.Reparse){'reparse'}elseif($class){$class}else{'unrecognized'});continue}
        if($top.Name -ceq 'Workflow' -or $top.Name -ceq 'Harness'){
            $role=$top.Name.ToLowerInvariant()
            Walk $top.Name @($repo,'\Atasks\z',$task) {param($k,$c)$i=New-ConsolidationItem $role $k $c[0] $c[1];Get-ConsolidationTree ([IO.Path]::Combine($SourceRoot,$k)) $i -WithJson;$items.Add($i)}
            continue
        }
        if($top.Name -cne 'Artifacts'){Exclude $top.Name 'unrecognized';continue}
        foreach($area in @(Get-ConsolidationEntries ([IO.Path]::Combine($SourceRoot,'Artifacts')))){
            $key='Artifacts\'+$area.Name;$class=$script:ArtifactsLevel[$area.Name]
            if(-not $area.Directory -or $area.Reparse -or $class -cne 'role'){Exclude $key $(if($area.Reparse){'reparse'}elseif($class){$class}else{'unrecognized'});continue}
            switch($area.Name){
                'evidence-v2'{Walk $key @($id,$repo,'\Atasks\z',$task) {param($k,$c)$i=New-ConsolidationItem 'evidence' $k $c[1] $c[2] $c[0];Get-ConsolidationTree ([IO.Path]::Combine($SourceRoot,$k)) $i -WithJson;$items.Add($i)}}
                'deployments'{
                    $pairs=@{}
                    foreach($entry in @(Get-ConsolidationEntries ([IO.Path]::Combine($SourceRoot,$key)))){
                        if($entry.Directory -or $entry.Reparse -or $entry.Name -cnotmatch '\A([0-9a-f]{32})(?:\.config)?\.json\z'){Exclude ($key+'\'+$entry.Name) $(if($entry.Reparse){'reparse'}else{'unrecognized'});continue}
                        if(-not $pairs.ContainsKey($Matches[1])){$pairs[$Matches[1]]=[Collections.Generic.List[string]]::new()};$pairs[$Matches[1]].Add($entry.Name)
                    }
                    foreach($deployment in @($pairs.Keys|Sort-Object)){
                        $i=New-ConsolidationItem 'deployment' ($key+'\'+$deployment) '' '' $deployment
                        foreach($name in $pairs[$deployment]){$f=Read-ConsolidationFile ([IO.Path]::Combine($SourceRoot,$key,$name));$i.files+=[pscustomobject]@{path=$name;bytes=$f.bytes;sha256=$f.sha256;json=$f.json;unchecked=$f.unchecked}}
                        $items.Add($i)
                    }
                }
                'transfers'{
                    foreach($entry in @(Get-ConsolidationEntries ([IO.Path]::Combine($SourceRoot,$key)))){
                        if(-not $entry.Directory -or $entry.Reparse -or $entry.Name -cnotmatch $id){Exclude ($key+'\'+$entry.Name) $(if($entry.Reparse){'reparse'}else{'unrecognized'});continue}
                        $operations[$key+'\'+$entry.Name]=$true
                    }
                }
            }
        }
    }
    # Operations are opened only once a record references them (D4), repeating
    # because an operation's own records may reference further operations.
    $inventory=[pscustomobject]@{logicalRoot=$LogicalRoot;sourceRoot=$SourceRoot;items=$items;excluded=$excluded}
    do{
        $added=$false
        foreach($key in @(Get-ConsolidationTargetKeys ([pscustomobject]@{logicalRoot=$LogicalRoot;sourceRoot=$SourceRoot;items=@($items)}))){
            if(-not $operations.ContainsKey($key) -or $operations[$key] -isnot [bool]){continue}
            $i=New-ConsolidationItem 'transfers' $key;Get-ConsolidationTree ([IO.Path]::Combine($SourceRoot,$key)) $i -WithJson;$items.Add($i);$operations[$key]=$i;$added=$true
        }
    }while($added)
    foreach($key in @($operations.Keys|Sort-Object)){if($operations[$key] -is [bool]){Exclude $key 'unreferenced-transfer'}}
    $inventory.items=@($items);$inventory.excluded=@($excluded)
    return $inventory
}
function Test-ConsolidationAcl([string]$Path,[bool]$Directory){try{Assert-ArtifactAcl $Path $Directory;return $true}catch{return $false}}
# Observes one item in the real store: content, ACL rule and reparse points.
function Get-ConsolidationTargetObservation([string]$LogicalRoot,[string]$Key){
    $seen=[pscustomobject]@{exists=$false;files=@();directories=@();aclOk=$true;reparse=$false}
    $parts=$Key.Split('\');$current=$LogicalRoot
    for($n=0;$n -lt $parts.Count-1;$n++){$current=[IO.Path]::Combine($current,$parts[$n]);if(([IO.Directory]::Exists($current) -or [IO.File]::Exists($current)) -and (Test-ConsolidationReparse $current)){$seen.reparse=$true;return $seen}}
    # A deployment key names the receipt pair, not a path on disk.
    if($Key -cmatch '\AArtifacts\\deployments\\[0-9a-f]{32}\z'){
        $id=$parts[-1]
        foreach($name in @(($id+'.json'),($id+'.config.json'))){
            $path=[IO.Path]::Combine($current,$name)
            if(-not ([IO.File]::Exists($path) -or [IO.Directory]::Exists($path))){continue}
            $seen.exists=$true
            if(Test-ConsolidationReparse $path){$seen.reparse=$true;continue}
            $f=Read-ConsolidationFile $path;$seen.files+=[pscustomobject]@{path=$name;bytes=$f.bytes;sha256=$f.sha256}
            if(-not (Test-ConsolidationAcl $path $false)){$seen.aclOk=$false}
        }
        return $seen
    }
    $path=[IO.Path]::Combine($LogicalRoot,$Key)
    if([IO.File]::Exists($path)){$seen.exists=$true;$seen.files=@([pscustomobject]@{path='';bytes=-1;sha256='not-a-directory'});return $seen}
    if(-not [IO.Directory]::Exists($path)){return $seen}
    $seen.exists=$true
    if(Test-ConsolidationReparse $path){$seen.reparse=$true;return $seen}
    Get-ConsolidationTree $path $seen
    if(-not (Test-ConsolidationAcl $path $true)){$seen.aclOk=$false}
    foreach($d in $seen.directories){if(-not (Test-ConsolidationAcl ([IO.Path]::Combine($path,$d)) $true)){$seen.aclOk=$false}}
    foreach($f in $seen.files){if(-not (Test-ConsolidationAcl ([IO.Path]::Combine($path,$f.path)) $false)){$seen.aclOk=$false}}
    return $seen
}
function Assert-ConsolidationRoots([string]$SourceRoot,[string]$LogicalRoot){
    if([string]::IsNullOrWhiteSpace($SourceRoot) -or -not [IO.Path]::IsPathFullyQualified($SourceRoot) -or $SourceRoot.StartsWith('\\') -or [IO.Path]::GetFullPath($SourceRoot) -cne $SourceRoot.TrimEnd('\') -or -not [IO.Directory]::Exists($SourceRoot)){throw 'invalid-source-root'}
    if((Test-ArtifactWithin $SourceRoot $LogicalRoot) -or (Test-ArtifactWithin $LogicalRoot $SourceRoot)){throw 'invalid-source-root'}
    try{Assert-NoArtifactReparse $SourceRoot}catch{throw 'invalid-source-root'}
    try{Assert-NoArtifactReparse $LogicalRoot}catch{throw 'store-path-unavailable'}
}
function Get-ConsolidationCurrentPlan([string]$SourceRoot,[string]$LogicalRoot){
    $inventory=Get-ConsolidationSourceInventory $SourceRoot $LogicalRoot
    $target=@{};foreach($key in @(Get-ConsolidationTargetKeys $inventory)){$target[$key]=Get-ConsolidationTargetObservation $LogicalRoot $key}
    return New-ConsolidationPlan $inventory $target
}
function Assert-ConsolidationContext {
    $context=Get-OsmStoreContext
    if($context -ceq 'virtualized'){throw 'virtualized-store'};if($context -cne 'not-virtualized'){throw 'store-context-unknown'}
}
function ConvertTo-ConsolidationFailure($ErrorRecord){
    $message=$ErrorRecord.Exception.Message
    return [ordered]@{status='failed';reasonCode=$(if($message -cin $script:ReasonCodes){$message}else{'consolidation-failed'})}
}
# plan: inventories both stores without changing either and writes the manifest
# candidate to a new file outside both stores for the owner to review.
function Invoke-StoreConsolidationPlan([string]$SourceRoot,[string]$OutputPath){
    try{
        Assert-ConsolidationContext
        $logical=Get-ConsolidationLogicalRoot
        Assert-ConsolidationRoots $SourceRoot $logical
        if([string]::IsNullOrWhiteSpace($OutputPath) -or -not [IO.Path]::IsPathFullyQualified($OutputPath) -or (Test-ArtifactWithin $OutputPath $logical) -or (Test-ArtifactWithin $OutputPath $SourceRoot) -or [IO.File]::Exists($OutputPath) -or [IO.Directory]::Exists($OutputPath) -or -not [IO.Directory]::Exists([IO.Path]::GetDirectoryName($OutputPath))){throw 'invalid-output'}
        $plan=Get-ConsolidationCurrentPlan $SourceRoot $logical
        $bytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $plan -Depth 32))
        try{$stream=[IO.FileStream]::new($OutputPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)}catch{throw 'invalid-output'}
        try{$stream.Write($bytes,0,$bytes.Length);$stream.Flush($true)}finally{$stream.Dispose()}
        return [ordered]@{status='passed';reasonCode=$null;manifestSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant();summary=$plan.summary;excluded=@($plan.excluded).Count}
    }catch{return ConvertTo-ConsolidationFailure $_}
}
# Creates missing directories below the store root with the private ACL rule.
function Initialize-ConsolidationDirectory([string]$LogicalRoot,[string]$Path){
    if(-not (Test-ArtifactWithin $Path $LogicalRoot)){throw 'store-path-unavailable'}
    $current=$LogicalRoot
    foreach($part in @('')+@([IO.Path]::GetRelativePath($LogicalRoot,$Path).Split('\')|Where-Object {$_ -and $_ -cne '.'})){
        if($part){$current=[IO.Path]::Combine($current,$part)}
        if([IO.Directory]::Exists($current)){if(Test-ConsolidationReparse $current){throw 'store-path-unavailable'};continue}
        Write-ConsolidationTrace 'create-directory' $current
        [IO.Directory]::CreateDirectory($current)|Out-Null;Set-ArtifactAcl $current $true
    }
}
# Run records (the manifest copy and unit receipts) appear only complete: written
# under the run's own staging, flushed, then renamed into place without replacing.
# An interrupted write leaves staging that the next execution of the same manifest
# discards, never a truncated record that would block or fake completion.
function Write-ConsolidationRecord([string]$RunDirectory,[string]$Path,[byte[]]$Bytes,[string]$Key){
    $pending=[IO.Path]::Combine($RunDirectory,'staging','records',[Guid]::NewGuid().ToString('N'))
    Write-ConsolidationTrace 'write' $pending
    $stream=[IO.FileStream]::new($pending,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{
        $half=[int][Math]::Floor($Bytes.Length/2);$stream.Write($Bytes,0,$half)
        if($script:FaultHook){& $script:FaultHook 'record-write' $Key}
        $stream.Write($Bytes,$half,$Bytes.Length-$half);$stream.Flush($true)
    }finally{$stream.Dispose()}
    Set-ArtifactAcl $pending $false
    Write-ConsolidationTrace 'record' $Path
    [IO.File]::Move($pending,$Path,$false)
}
# Completion is a receipt that reads back as this run's record of this unit.
function Test-ConsolidationReceipt([string]$Path,[string]$RunId,[string]$ManifestSha256,[string]$UnitId){
    try{
        if([IO.FileInfo]::new($Path).Length -gt 1MB -or -not (Test-ConsolidationAcl $Path $false)){return $false}
        $r=ConvertFrom-Json -InputObject ([Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($Path))) -AsHashtable -DateKind String
        return ($r -is [Collections.IDictionary] -and $r['schemaVersion'] -eq 1 -and $r['runId'] -ceq $RunId -and $r['manifestSha256'] -ceq $ManifestSha256 -and $r['unitId'] -ceq $UnitId)
    }catch{return $false}
}
# Copies one approved file into staging: CreateNew, flush, hash check, then ACL.
# The source position is given relative to the source root so the opened handle can
# be checked against the root's physical path.
function Copy-ConsolidationFile([string]$SourceRoot,[string]$SourcePhysical,[string]$Relative,[string]$Destination,$Expected,[string]$Key){
    $Source=[IO.Path]::Combine($SourceRoot,$Relative)
    Write-ConsolidationTrace 'read' $Source;Write-ConsolidationTrace 'write' $Destination
    $in=[IO.FileStream]::new($Source,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    try{
        # The inventory saw no reparse point, but an ancestor can be swapped for a
        # junction afterwards, and a matching hash says nothing about which path the
        # bytes came through. The handle actually read from must resolve to the
        # source position itself.
        try{$final=[OneStarMaker.Workflow.StoreContextProbe]::FinalPath($in)}catch{throw 'reparse'}
        if(-not $final.TrimEnd('\').Equals([IO.Path]::Combine($SourcePhysical,$Relative),[StringComparison]::OrdinalIgnoreCase)){throw 'reparse'}
        $out=[IO.FileStream]::new($Destination,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try{
            $hash=[Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256);$buffer=[byte[]]::new(1MB);$total=[long]0
            try{
                while(($n=$in.Read($buffer,0,$buffer.Length)) -gt 0){
                    $out.Write($buffer,0,$n);$hash.AppendData($buffer,0,$n);$total+=$n
                    if($script:FaultHook){& $script:FaultHook 'file-write' $Key}
                }
                $out.Flush($true);$sha=[Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
            }finally{$hash.Dispose()}
        }finally{$out.Dispose()}
    }finally{$in.Dispose()}
    if($total -ne [long]$Expected.bytes -or $sha -cne $Expected.sha256){throw 'source-changed'}
    Set-ArtifactAcl $Destination $false
}
# Removes only this run's own unpublished staging for one item.
function Remove-ConsolidationStaging([string]$RunDirectory,[string]$Path){
    if(-not (Test-ArtifactWithin $Path ([IO.Path]::Combine($RunDirectory,'staging'))) -or -not [IO.Directory]::Exists($Path)){return}
    foreach($entry in [IO.Directory]::EnumerateFileSystemEntries($Path,'*',[IO.SearchOption]::AllDirectories)){if(Test-ConsolidationReparse $entry){throw 'store-path-unavailable'}}
    Write-ConsolidationTrace 'delete-staging' $Path
    [IO.Directory]::Delete($Path,$true)
}
function Publish-ConsolidationItem([string]$RunDirectory,[string]$SourceRoot,[string]$SourcePhysical,[string]$LogicalRoot,[string]$UnitId,[int]$Index,$Item){
    $staging=[IO.Path]::Combine($RunDirectory,'staging',$UnitId,[string]$Index)
    Remove-ConsolidationStaging $RunDirectory $staging
    Initialize-ConsolidationDirectory $LogicalRoot $staging
    $deployment=$Item.role -ceq 'deployment'
    $sourceDirectory=if($deployment){'Artifacts\deployments'}else{$Item.key}
    foreach($directory in @($Item.directories)){$path=[IO.Path]::Combine($staging,$directory);Write-ConsolidationTrace 'create-directory' $path;[IO.Directory]::CreateDirectory($path)|Out-Null;Set-ArtifactAcl $path $true}
    foreach($file in @($Item.files)){Copy-ConsolidationFile $SourceRoot $SourcePhysical ([IO.Path]::Combine($sourceDirectory,$file.path)) ([IO.Path]::Combine($staging,$file.path)) $file $Item.key}
    if($script:FaultHook){& $script:FaultHook 'staged' $Item.key}
    if($deployment){
        # Receipt before config: registry discovery enumerates *.config.json and
        # must never find a config whose receipt is not yet present.
        $target=[IO.Path]::Combine($LogicalRoot,'Artifacts','deployments');Initialize-ConsolidationDirectory $LogicalRoot $target
        foreach($file in @($Item.files|Sort-Object {$_.path.EndsWith('.config.json')})){
            $destination=[IO.Path]::Combine($target,$file.path)
            if([IO.File]::Exists($destination)){
                $existing=Read-ConsolidationFile $destination
                if($existing.sha256 -cne $file.sha256 -or -not (Test-ConsolidationAcl $destination $false)){throw 'concurrent-writer'};continue
            }
            if($script:FaultHook){& $script:FaultHook 'before-publish' $Item.key}
            Write-ConsolidationTrace 'move' $destination
            try{[IO.File]::Move([IO.Path]::Combine($staging,$file.path),$destination,$false)}catch{if([IO.File]::Exists($destination)){throw 'concurrent-writer'};throw}
            Assert-ArtifactAcl $destination $false
        }
        Remove-ConsolidationStaging $RunDirectory $staging
    }else{
        $destination=[IO.Path]::Combine($LogicalRoot,$Item.key)
        Initialize-ConsolidationDirectory $LogicalRoot ([IO.Path]::GetDirectoryName($destination))
        if($script:FaultHook){& $script:FaultHook 'before-publish' $Item.key}
        # The whole directory appears at once. A rename never replaces an existing
        # destination, so a writer that got there first stops this unit untouched.
        Write-ConsolidationTrace 'move' $destination
        try{[IO.Directory]::Move($staging,$destination)}catch{if([IO.Directory]::Exists($destination) -or [IO.File]::Exists($destination)){throw 'concurrent-writer'};throw}
        Assert-ArtifactAcl $destination $true
    }
    if($script:FaultHook){& $script:FaultHook 'published' $Item.key}
}
# consolidate: re-plans, re-checks the approved manifest, and publishes approved
# units in manifest order (deployment units first, then tasks; inside a task unit
# Workflow last). Held or changed units are reported, never forced: any of them
# makes the run 'partial'.
function Invoke-StoreConsolidation([string]$ManifestPath,[string]$ManifestSha256,[bool]$DryRun){
    try{
        Assert-ConsolidationContext
        if([string]::IsNullOrWhiteSpace($ManifestPath) -or -not [IO.Path]::IsPathFullyQualified($ManifestPath) -or $ManifestSha256 -cnotmatch '\A[0-9a-f]{64}\z' -or -not [IO.File]::Exists($ManifestPath) -or [IO.FileInfo]::new($ManifestPath).Length -gt 64MB){throw 'manifest-invalid'}
        $bytes=[IO.File]::ReadAllBytes($ManifestPath)
        if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant() -cne $ManifestSha256){throw 'manifest-hash-mismatch'}
        try{$approved=ConvertFrom-Json -InputObject ([Text.UTF8Encoding]::new($false,$true).GetString($bytes)) -AsHashtable -Depth 64 -DateKind String}catch{throw 'manifest-invalid'}
        $logical=Get-ConsolidationLogicalRoot
        if($approved -isnot [Collections.IDictionary] -or $approved['logicalRoot'] -cne $logical -or $approved['sourceRoot'] -isnot [string]){throw 'manifest-invalid'}
        Assert-ConsolidationRoots $approved.sourceRoot $logical
        try{$sourcePhysical=(Get-WindowsStorePathIdentity $approved.sourceRoot).physicalPath}catch{throw 'invalid-source-root'}
        $current=Get-ConsolidationCurrentPlan $approved.sourceRoot $logical
        try{$verdicts=@(Test-ConsolidationManifest $approved $current)}catch{throw 'manifest-invalid'}
        $runId=$ManifestSha256.Substring(0,32)
        if($DryRun){
            $units=@($verdicts|ForEach-Object {[ordered]@{unitId=$_.unitId;verdict=$_.verdict;result=$_.verdict}})
            return [ordered]@{status=$(if(@($units|Where-Object {$_.verdict -cnotin @('publish','complete')}).Count){'partial'}else{'passed'});reasonCode=$null;dryRun=$true;runId=$runId;units=$units}
        }
        $run=[IO.Path]::Combine($logical,'Artifacts','consolidation',$runId)
        if(-not [IO.Directory]::Exists($logical)){throw 'store-path-unavailable'}
        Initialize-ConsolidationDirectory $logical ([IO.Path]::Combine($run,'receipts'))
        if([IO.Path]::GetPathRoot($run) -cne [IO.Path]::GetPathRoot($logical)){throw 'store-path-unavailable'}
        # Exclusive for the run's lifetime; a second invocation stops instead of interleaving.
        $lockPath=[IO.Path]::Combine($run,'run.lock')
        try{$lock=[IO.FileStream]::new($lockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch [IO.IOException]{throw 'consolidation-busy'}
        try{
            # Everything below the run directory is this run's own. The rule is applied
            # on every execution, because an interruption between creating an entry and
            # setting its ACL would otherwise leave it inherited for good.
            foreach($d in @($run,[IO.Path]::Combine($run,'receipts'))){Set-ArtifactAcl $d $true}
            Set-ArtifactAcl $lockPath $false
            # Pending records left by an interrupted execution are this run's own staging.
            $records=[IO.Path]::Combine($run,'staging','records');Remove-ConsolidationStaging $run $records;Initialize-ConsolidationDirectory $logical $records
            Set-ArtifactAcl ([IO.Path]::Combine($run,'staging')) $true
            $copy=[IO.Path]::Combine($run,'manifest.json')
            if([IO.File]::Exists($copy)){if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($copy))).ToLowerInvariant() -cne $ManifestSha256){throw 'run-conflict'}}
            else{Write-ConsolidationRecord $run $copy $bytes 'manifest'}
            $journal=[IO.Path]::Combine($run,'journal.jsonl');if(-not [IO.File]::Exists($journal)){[IO.File]::WriteAllBytes($journal,[byte[]]@())};Set-ArtifactAcl $journal $false
            $script:Journal=$journal;Write-ConsolidationTrace 'run-start' $runId
            $fresh=@{};foreach($unit in $current.units){$fresh[$unit.unitId]=$unit}
            $done=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal);$results=[Collections.Generic.List[object]]::new()
            foreach($verdict in $verdicts){
                $result=$verdict.verdict;$unit=if($fresh.ContainsKey($verdict.unitId)){$fresh[$verdict.unitId]}else{$null}
                if($result -cin @('publish','complete')){
                    try{
                        if(@($unit.dependsOn|Where-Object {-not $done.Contains($_)}).Count){throw 'dependency-pending'}
                        if($result -ceq 'publish'){
                            for($i=0;$i -lt @($unit.items).Count;$i++){if($unit.items[$i].state -cin @('absent','partial')){Publish-ConsolidationItem $run $approved.sourceRoot $sourcePhysical $logical $unit.unitId $i $unit.items[$i]}}
                            $result='published'
                        }
                        if($script:FaultHook){& $script:FaultHook 'before-receipt' $unit.unitId}
                        $receipt=[IO.Path]::Combine($run,'receipts',$unit.unitId+'.json')
                        # A damaged receipt is reported and left as it is, never rewritten.
                        if([IO.File]::Exists($receipt)){if(-not (Test-ConsolidationReceipt $receipt $runId $ManifestSha256 $unit.unitId)){throw 'receipt-invalid'}}
                        else{Write-ConsolidationRecord $run $receipt ([Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -Depth 16 -Compress -InputObject ([ordered]@{schemaVersion=1;runId=$runId;manifestSha256=$ManifestSha256;unitId=$unit.unitId;kind=$unit.kind;items=@($unit.items|ForEach-Object {[ordered]@{key=$_.key;files=@($_.files).Count;publishedByThisRun=($_.state -cne 'identical')}});completedAt=(& $script:Clock).ToString('o')})))) $unit.unitId}
                        $null=$done.Add($unit.unitId)
                    }catch{$result=if($_.Exception.Message -cin @('dependency-pending','concurrent-writer','source-changed','reparse','receipt-invalid')){$_.Exception.Message}else{'publish-failed'}}
                }
                Write-ConsolidationTrace ('unit-'+$result) $verdict.unitId
                $results.Add([ordered]@{unitId=$verdict.unitId;verdict=$verdict.verdict;result=$result})
            }
        }finally{$script:Journal=$null;$lock.Dispose()}
        $open=@($results|Where-Object {$_.result -cnotin @('published','complete')}).Count
        return [ordered]@{status=$(if($open){'partial'}else{'passed'});reasonCode=$null;dryRun=$false;runId=$runId;units=@($results)}
    }catch{return ConvertTo-ConsolidationFailure $_}
}
Export-ModuleMember -Function Invoke-StoreConsolidationPlan,Invoke-StoreConsolidation
