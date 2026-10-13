Set-StrictMode -Version Latest

# Rules for consolidating a split store. Pure functions over an in-memory model:
# no file, OS or clock access, so every classification is testable from memory.
# The I/O side (StoreConsolidation.psm1) inventories both stores into this model
# and executes only what a manifest produced here, and approved by the owner, says.
#
# Item keys are paths relative to the store root (%LOCALAPPDATA%\OneStarMaker).
# A task unit holds one task's Workflow, Evidence and Harness directories plus the
# transfers operations its records reference. A deployment unit holds one
# deployment receipt pair. Units sharing a referenced item are merged, because
# neither half is usable alone; nothing ever picks a winner between two copies.

# Publication order inside a unit. Workflow is last: cleanup and the scheduled job
# discover tasks from the Workflow task list, so a unit stays invisible to them
# until everything it depends on is in place.
$script:RoleOrder=@{deployment=0;transfers=1;evidence=2;harness=3;workflow=4}
$script:TaskPattern='[a-z0-9][a-z0-9-]{0,63}'
$script:ItemPatterns=@(
    @{role='workflow';pattern="\AWorkflow\\([0-9a-f]{64})\\tasks\\($script:TaskPattern)(?:\\|\z)";key={param($m)'Workflow\'+$m.Groups[1].Value.ToLowerInvariant()+'\tasks\'+$m.Groups[2].Value.ToLowerInvariant()}}
    @{role='harness';pattern="\AHarness\\([0-9a-f]{64})\\tasks\\($script:TaskPattern)(?:\\|\z)";key={param($m)'Harness\'+$m.Groups[1].Value.ToLowerInvariant()+'\tasks\'+$m.Groups[2].Value.ToLowerInvariant()}}
    @{role='evidence';pattern="\AArtifacts\\evidence-v2\\([0-9a-f]{32})\\([0-9a-f]{64})\\tasks\\($script:TaskPattern)(?:\\|\z)";key={param($m)'Artifacts\evidence-v2\'+$m.Groups[1].Value.ToLowerInvariant()+'\'+$m.Groups[2].Value.ToLowerInvariant()+'\tasks\'+$m.Groups[3].Value.ToLowerInvariant()}}
    @{role='transfers';pattern='\AArtifacts\\transfers\\([0-9a-f]{32})(?:\\|\z)';key={param($m)'Artifacts\transfers\'+$m.Groups[1].Value.ToLowerInvariant()}}
    @{role='deployment';pattern='\AArtifacts\\deployments\\([0-9a-f]{32})(?:\.config)?\.json\z';key={param($m)'Artifacts\deployments\'+$m.Groups[1].Value.ToLowerInvariant()}}
)
$script:RolePrefix='\A(?:Workflow|Harness|Artifacts\\(?:evidence-v2|transfers|deployments))(?:\\|\z)'

function Test-ConsolidationWithin([string]$Path,[string]$Root){
    $p=$Path.TrimEnd('\');$r=$Root.TrimEnd('\')
    return $p.Equals($r,[StringComparison]::OrdinalIgnoreCase) -or $p.StartsWith($r+'\',[StringComparison]::OrdinalIgnoreCase)
}
# Maps a store-relative path to its item. Returns $null for a path inside a target
# role that names no item (a role directory itself), and 'out-of-role' otherwise.
function Get-ConsolidationItemKey([string]$Relative){
    foreach($spec in $script:ItemPatterns){
        $m=[regex]::Match($Relative,$spec.pattern,[Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if($m.Success){return [pscustomobject]@{role=$spec.role;key=(& $spec.key $m)}}
    }
    if($Relative -imatch $script:RolePrefix){return $null}
    return 'out-of-role'
}
function Get-ConsolidationJsonStrings([string]$Json){
    $document=[Text.Json.JsonDocument]::Parse($Json)
    try{
        $stack=[Collections.Generic.Stack[Text.Json.JsonElement]]::new();$stack.Push($document.RootElement)
        while($stack.Count){
            $element=$stack.Pop()
            switch($element.ValueKind){
                'Object'{foreach($p in $element.EnumerateObject()){$stack.Push($p.Value)}}
                'Array'{foreach($v in $element.EnumerateArray()){$stack.Push($v)}}
                'String'{$element.GetString()}
            }
        }
    }finally{$document.Dispose()}
}
# Applies the role-independent reference rule to every string value of every JSON
# record in the item. Paths outside the logical store (worktree sources and the
# like) are not references and are not recorded.
function Get-ConsolidationItemReferences($Item,[string]$LogicalRoot,[string]$SourceRoot){
    $references=[Collections.Generic.List[string]]::new();$violations=[Collections.Generic.List[string]]::new()
    foreach($file in $Item.files){
        if($null -eq $file.json){continue}
        # Any JSON-looking file is checked; only a file named as a JSON record must parse.
        try{$strings=@(Get-ConsolidationJsonStrings $file.json)}catch{if($file.path.EndsWith('.json',[StringComparison]::OrdinalIgnoreCase)){$violations.Add('source-invalid')};continue}
        foreach($value in $strings){
            if($value -cnotmatch '\A(?:\\\\|[A-Za-z]:[\\/])'){continue}
            $path=$value.Replace('/','\');if($path.StartsWith('\\?\')){$path=$path.Substring(4)}
            # A LocalCache path would bind the record to the virtualized copy.
            if((Test-ConsolidationWithin $path $SourceRoot) -or $path -imatch '\\Packages\\[^\\]+\\LocalCache(?:\\|\z)'){$violations.Add('reference-localcache');continue}
            if(-not (Test-ConsolidationWithin $path $LogicalRoot)){continue}
            $relative=$path.Substring($LogicalRoot.TrimEnd('\').Length).TrimStart('\')
            if($relative -match '(?:\A|\\)\.\.?(?:\\|\z)'){$violations.Add('reference-out-of-role');continue}
            $target=Get-ConsolidationItemKey $relative
            if($target -ceq 'out-of-role'){$violations.Add('reference-out-of-role');continue}
            if($null -ne $target -and $target.key -cne $Item.key){$references.Add($target.key)}
        }
    }
    return [pscustomobject]@{references=@($references|Sort-Object -Unique);violations=@($violations|Sort-Object -Unique)}
}
# Keys the I/O side must observe in the real store before planning.
function Get-ConsolidationTargetKeys($Inventory){
    $keys=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($item in $Inventory.items){
        $null=$keys.Add($item.key)
        foreach($key in (Get-ConsolidationItemReferences $item $Inventory.logicalRoot $Inventory.sourceRoot).references){$null=$keys.Add($key)}
        if($item.role -ceq 'evidence'){$null=$keys.Add('Artifacts\deployments\'+$item.deploymentId)}
    }
    return @($keys|Sort-Object)
}
function Get-ConsolidationFileSignature($Files){return (@($Files|ForEach-Object {$_.path+'|'+$_.bytes+'|'+$_.sha256}|Sort-Object) -join "`n")}
# Compares one source item with its observation in the real store.
function Get-ConsolidationItemState($Item,$Observed){
    if($Item.reparse){return 'reparse'}
    # A source file that could not be read (held open by its writer) has no hash to approve.
    if(@($Item.files|Where-Object {$_.sha256 -cnotmatch '\A[0-9a-f]{64}\z'}).Count){return 'source-unreadable'}
    # A reparse point on the way to an absent destination would redirect the publication.
    if($null -ne $Observed -and $Observed.reparse){return 'reparse'}
    if($null -eq $Observed -or -not $Observed.exists){return 'absent'}
    # A deployment receipt pair is published file by file. An identical subset is
    # this consolidation's own interrupted publication, not a conflict.
    if($Item.role -ceq 'deployment' -and $Observed.aclOk -and @($Observed.files).Count -lt @($Item.files).Count){
        $source=@{};foreach($f in $Item.files){$source[$f.path]=$f.path+'|'+$f.bytes+'|'+$f.sha256}
        if(@($Observed.files|Where-Object {-not $source.ContainsKey($_.path) -or $source[$_.path] -cne ($_.path+'|'+$_.bytes+'|'+$_.sha256)}).Count -eq 0){return 'partial'}
    }
    if((Get-ConsolidationFileSignature $Item.files) -cne (Get-ConsolidationFileSignature $Observed.files) -or ((@($Item.directories)|Sort-Object) -join '|') -cne ((@($Observed.directories)|Sort-Object) -join '|')){return 'conflict'}
    # Identical bytes are skipped only when the existing entry also obeys the ACL rule.
    if(-not $Observed.aclOk){return 'acl-mismatch'}
    return 'identical'
}
function New-ConsolidationPlan($Inventory,$Target){
    $items=@{};foreach($item in $Inventory.items){$items[$item.key]=$item}
    $parent=@{}
    function Find([string]$k){while($parent[$k] -cne $k){$k=$parent[$k]};return $k}
    function Join([string]$a,[string]$b){$x=Find $a;$y=Find $b;if($x -cne $y){if([string]::CompareOrdinal($x,$y) -lt 0){$parent[$y]=$x}else{$parent[$x]=$y}}}
    # Task items of the same repository and task share a group; deployments stand alone.
    $groupOf=@{}
    foreach($item in $Inventory.items){
        $group=switch($item.role){'deployment'{'deployment:'+$item.key}'transfers'{'transfers:'+$item.key}default{'task:'+$item.repositoryId+'\'+$item.taskId}}
        $groupOf[$item.key]=$group;if(-not $parent.ContainsKey($group)){$parent[$group]=$group}
    }
    $refs=@{};$holds=@{}
    foreach($item in $Inventory.items){
        $r=Get-ConsolidationItemReferences $item $Inventory.logicalRoot $Inventory.sourceRoot;$refs[$item.key]=$r
        foreach($k in $r.references){if($items.ContainsKey($k) -and $items[$k].role -cne 'deployment'){Join $groupOf[$item.key] $groupOf[$k]}}
    }
    $members=@{}
    foreach($item in $Inventory.items){
        $root=Find $groupOf[$item.key];if(-not $members.ContainsKey($root)){$members[$root]=[Collections.Generic.List[object]]::new()};$members[$root].Add($item)
    }
    # Only operations some record references are carried; a lone operation is listed as excluded.
    foreach($root in @($members.Keys)){if(@($members[$root]|Where-Object role -CNE 'transfers').Count -eq 0){$members.Remove($root)}}
    $unitOf=@{};$units=[Collections.Generic.List[object]]::new()
    foreach($root in @($members.Keys|Sort-Object)){
        $list=@($members[$root]|Sort-Object @{e={$script:RoleOrder[$_.role]}},@{e={$_.key}})
        $digest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes((($list|ForEach-Object key) -join "`n")))).ToLowerInvariant()
        $unit=[ordered]@{unitId=$digest.Substring(0,32);kind=$(if($list[0].role -ceq 'deployment'){'deployment'}else{'task'});status=$null;reasons=@();dependsOn=@();items=@()}
        foreach($item in $list){
            $unitOf[$item.key]=$unit.unitId
            $unit.items+=[ordered]@{role=$item.role;key=$item.key;state=(Get-ConsolidationItemState $item $Target[$item.key]);files=@($item.files|Sort-Object path|ForEach-Object {[ordered]@{path=$_.path;bytes=[long]$_.bytes;sha256=$_.sha256}});directories=@(@($item.directories)|Sort-Object)}
        }
        $units.Add($unit)
    }
    $byId=@{};foreach($unit in $units){$byId[$unit.unitId]=$unit}
    foreach($unit in $units){
        $reasons=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal);$depends=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach($entry in $unit.items){
            if($entry.state -cin @('conflict','acl-mismatch','reparse','source-unreadable')){$null=$reasons.Add($entry.state)}
            $needed=@($refs[$entry.key].references)
            foreach($v in $refs[$entry.key].violations){$null=$reasons.Add($v)}
            if($entry.role -ceq 'evidence'){$needed+='Artifacts\deployments\'+$items[$entry.key].deploymentId}
            foreach($k in $needed){
                if($unitOf.ContainsKey($k)){if($unitOf[$k] -cne $unit.unitId){$null=$depends.Add($unitOf[$k])};continue}
                # Not carried by any unit: it must already exist intact in the real store.
                $seen=$Target[$k]
                if($null -eq $seen -or -not $seen.exists -or $seen.reparse -or -not $seen.aclOk){$null=$reasons.Add('reference-missing')}
            }
        }
        $unit.reasons=@($reasons|Sort-Object);$unit.dependsOn=@($depends|Sort-Object)
    }
    # A unit whose dependency is held cannot be used either, so it is held too.
    do{
        $changed=$false
        foreach($unit in $units){if($unit.reasons -cnotcontains 'dependency-held' -and @($unit.dependsOn|Where-Object {@($byId[$_].reasons).Count}).Count){$unit.reasons=@(@($unit.reasons)+'dependency-held'|Sort-Object);$changed=$true}}
    }while($changed)
    foreach($unit in $units){$unit.status=if(@($unit.reasons).Count){'hold'}elseif(@($unit.items|Where-Object state -CNE 'identical').Count){'publish'}else{'skip'}}
    $ordered=@($units|Sort-Object @{e={if($_.kind -ceq 'deployment'){0}else{1}}},@{e={$_.unitId}})
    $unreferenced=@($Inventory.items|Where-Object {$_.role -ceq 'transfers' -and -not $unitOf.ContainsKey($_.key)}|ForEach-Object {[ordered]@{key=$_.key;reason='unreferenced-transfer'}})
    return [ordered]@{
        schemaVersion=1;purpose='store-consolidation-v1';sourceRoot=$Inventory.sourceRoot;logicalRoot=$Inventory.logicalRoot
        units=$ordered;excluded=@(@($Inventory.excluded)+$unreferenced)
        summary=[ordered]@{units=$ordered.Count;publish=@($ordered|Where-Object status -CEQ 'publish').Count;skip=@($ordered|Where-Object status -CEQ 'skip').Count;hold=@($ordered|Where-Object status -CEQ 'hold').Count}
    }
}
# Re-checks an approved manifest against a plan computed at execution time.
# Returns one verdict per approved unit: 'publish', 'complete' (already identical),
# 'hold' (held when approved), 'source-changed' or 'target-changed'. An item that
# went from absent to partial or identical is this run's own earlier publication.
function Test-ConsolidationManifest($Approved,$Current){
    if($Approved.schemaVersion -ne 1 -or $Approved.purpose -cne 'store-consolidation-v1' -or $Approved.sourceRoot -cne $Current.sourceRoot -or $Approved.logicalRoot -cne $Current.logicalRoot){throw 'manifest-invalid'}
    $now=@{};foreach($unit in $Current.units){$now[$unit.unitId]=$unit}
    foreach($unit in $Approved.units){
        $verdict=if($unit.status -cne 'publish' -and $unit.status -cne 'skip'){'hold'}
        elseif(-not $now.ContainsKey($unit.unitId)){'source-changed'}
        else{
            $fresh=$now[$unit.unitId];$result=$null
            for($i=0;$i -lt @($unit.items).Count;$i++){
                $a=$unit.items[$i];$b=$fresh.items[$i]
                if($a.key -cne $b.key -or (Get-ConsolidationFileSignature $a.files) -cne (Get-ConsolidationFileSignature $b.files) -or ((@($a.directories)) -join '|') -cne ((@($b.directories)) -join '|')){$result='source-changed';break}
                if($a.state -cne $b.state -and -not ($a.state -cin @('absent','partial') -and $b.state -cin @('partial','identical'))){$result='target-changed'}
            }
            if($result){$result}elseif(@($fresh.reasons).Count){'target-changed'}elseif($fresh.status -ceq 'skip'){'complete'}else{'publish'}
        }
        [pscustomobject]@{unitId=$unit.unitId;verdict=$verdict}
    }
}
Export-ModuleMember -Function Get-ConsolidationItemKey,Get-ConsolidationTargetKeys,New-ConsolidationPlan,Test-ConsolidationManifest
