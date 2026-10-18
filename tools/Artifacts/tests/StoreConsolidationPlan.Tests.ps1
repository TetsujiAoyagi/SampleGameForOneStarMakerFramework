param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# Consolidation rules over in-memory models only. No case touches the file system;
# the paths below are strings that never exist on disk.
Import-Module (Join-Path $PSScriptRoot '../StoreConsolidationPlan.psm1') -Force
# The store root, the former root (an alias records written before the move still
# name) and a package copy of the former root.
$logical='C:\Users\fixture\OneStarMaker'
$legacyRoot='C:\Users\fixture\AppData\Local\OneStarMaker'
$source='C:\Users\fixture\AppData\Local\Packages\Fixture.Package_0000000000000\LocalCache\Local\OneStarMaker'
$repo='a'*64;$dep='d'*32;$op='0'*32;$op2='1'*32
$cases=@('task-items-share-a-unit','reference-missing-operation','reference-present-in-target','reference-localcache-rejected','reference-out-of-role-rejected','outside-store-path-ignored','shared-operation-merges-units','target-states','workflow-divergence-held','deployment-dependency','unreferenced-operation-excluded','invalid-record-held','target-keys-cover-references','manifest-recheck','plan-module-is-pure','referenced-file-missing','unchecked-record-held','multi-workflow-unit-held','workflow-already-visible-held','all-identical-unit-not-held','only-new-workflow-not-held','single-workflow-unit-not-held','multi-workflow-with-other-reasons','target-only-reference-unreadable','ac-s4-b','ac-s4-c')
$selected=@(if($Case -ceq '*'){$cases}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
function Assert([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
# Unchecked: the reader could not examine a JSON-looking record (too large to load).
function File([string]$Path,[string]$Json=$null,[string]$Sha=('e'*64),[bool]$Unchecked=$false){[pscustomobject]@{path=$Path;bytes=[long]10;sha256=$Sha;json=$Json;unchecked=$Unchecked}}
function Item([string]$Role,[string]$Task='',$Files=@(),[string]$Id=''){
    $key=switch($Role){'workflow'{"Workflow\$repo\tasks\$Task"}'harness'{"Harness\$repo\tasks\$Task"}'evidence'{"Artifacts\evidence-v2\$dep\$repo\tasks\$Task"}'transfers'{"Artifacts\transfers\$Id"}'deployment'{"Artifacts\deployments\$dep"}}
    [pscustomobject]@{role=$Role;key=$key;repositoryId=$repo;taskId=$Task;deploymentId=$dep;files=@($Files);directories=@();reparse=$false}
}
function Catalog([string[]]$Paths){File 'catalog.json' (ConvertTo-Json -Compress -Depth 10 -InputObject ([ordered]@{schemaVersion=2;artifacts=@([ordered]@{ownedCopies=@($Paths|ForEach-Object {[ordered]@{path=$_}})})}))}
function Inventory($Items,$Excluded=@()){[pscustomobject]@{logicalRoot=$logical;legacyRoots=@($legacyRoot);sourceRoot=$source;items=@($Items);excluded=@($Excluded)}}
function Seen([bool]$Exists,$Files=@(),[bool]$Acl=$true){[pscustomobject]@{exists=$Exists;files=@($Files);directories=@();aclOk=$Acl;reparse=$false}}
# The real store has the deployment unless a case says otherwise.
function Target([hashtable]$Extra=@{}){$t=@{"Artifacts\deployments\$dep"=(Seen $true @((File "$dep.json"),(File "$dep.config.json")))};foreach($k in $Extra.Keys){$t[$k]=$Extra[$k]};$t}
function Unit($Plan,[string]$Key){,@($Plan.units|Where-Object {@($_.items|Where-Object key -CEQ $Key).Count})}
# Two tasks merged into one unit by a shared operation; the operation is carried.
$shared="$logical\Artifacts\transfers\$op\owned\a.zip"
function Merged{@((Item transfers '' @(File 'owned\a.zip') $op),(Item evidence alpha @(Catalog @($shared))),(Item evidence beta @(Catalog @($shared))),(Item workflow alpha @(File 'state.json' '{}')),(Item workflow beta @(File 'state.json' '{"b":1}')))}
# The real store holding the given items byte for byte with the private ACL.
function Present($Items,[string[]]$Keys){$t=@{};foreach($i in @($Items|Where-Object {$_.key -cin $Keys})){$t[$i.key]=Seen $true @($i.files)};$t}
function Locked([string]$Path){[pscustomobject]@{path=$Path;bytes=[long]-1;sha256='unreadable';json=$null;unchecked=$false}}
foreach($case in $selected){
    try{
        Assert ($case -cin $cases) 'unknown case'
        switch($case){
            'task-items-share-a-unit'{
                $plan=New-ConsolidationPlan (Inventory @((Item workflow alpha @(File 'state.json' '{}')),(Item harness alpha @(File 'CURRENT.json' '{}')),(Item evidence alpha @(Catalog @())))) (Target)
                Assert ($plan.units.Count -eq 1 -and $plan.units[0].kind -ceq 'task' -and $plan.units[0].status -ceq 'publish') 'one publishable unit'
                Assert ((($plan.units[0].items|ForEach-Object role) -join ',') -ceq 'evidence,harness,workflow') 'Workflow is not published last'
            }
            'reference-missing-operation'{
                $plan=New-ConsolidationPlan (Inventory @((Item evidence alpha @(Catalog @("$logical\Artifacts\transfers\$op\owned\a.zip"))))) (Target)
                Assert ($plan.units[0].status -ceq 'hold' -and $plan.units[0].reasons -ccontains 'reference-missing') 'missing operation accepted'
            }
            'reference-present-in-target'{
                $plan=New-ConsolidationPlan (Inventory @((Item evidence alpha @(Catalog @("$logical\Artifacts\transfers\$op\owned\a.zip"))))) (Target @{"Artifacts\transfers\$op"=(Seen $true @(File 'owned\a.zip'))})
                Assert ($plan.units[0].status -ceq 'publish') ('present operation rejected '+($plan.units[0].reasons -join ','))
                $plan=New-ConsolidationPlan (Inventory @((Item evidence alpha @(Catalog @("$logical\Artifacts\transfers\$op\owned\a.zip"))))) (Target @{"Artifacts\transfers\$op"=(Seen $true @(File 'owned\a.zip') $false)})
                Assert ($plan.units[0].reasons -ccontains 'reference-missing') 'present operation with a foreign ACL accepted'
            }
            'reference-localcache-rejected'{
                foreach($path in @("$source\Artifacts\transfers\$op\a.zip",'C:\Users\fixture\AppData\Local\Packages\Other.App_1\LocalCache\Local\x.json','\\?\C:\Users\fixture\AppData\Local\Packages\Other.App_1\LocalCache\Local\x.json')){
                    $plan=New-ConsolidationPlan (Inventory @((Item workflow alpha @(File 'state.json' (ConvertTo-Json -Compress -Depth 10 @{nested=@(@{p=$path})}))))) (Target)
                    Assert ($plan.units[0].status -ceq 'hold' -and $plan.units[0].reasons -ccontains 'reference-localcache') ('LocalCache path accepted '+$path)
                }
            }
            'reference-out-of-role-rejected'{
                foreach($path in @("$logical\Artifacts\credentials\osm\record.bin","$logical\RevisionLocks\x","$logical\Artifacts\builds-v1\b\receipt.json","$logical\Artifacts\runtime\r","$logical\WorktreeRecords\w","$logical\Workflow\..\Artifacts\credentials\x")){
                    $plan=New-ConsolidationPlan (Inventory @((Item evidence alpha @(Catalog @($path))))) (Target)
                    Assert ($plan.units[0].reasons -ccontains 'reference-out-of-role') ('out-of-role path accepted '+$path)
                }
            }
            'outside-store-path-ignored'{
                $plan=New-ConsolidationPlan (Inventory @((Item workflow alpha @(File 'state.json' (ConvertTo-Json -Compress @{root='D:\repositories\worktree\evidence';rel='relative\path'}))))) (Target)
                Assert ($plan.units[0].status -ceq 'publish') 'source path treated as a reference'
            }
            'shared-operation-merges-units'{
                $shared="$logical\Artifacts\transfers\$op\owned\a.zip"
                $plan=New-ConsolidationPlan (Inventory @((Item evidence alpha @(Catalog @($shared))),(Item evidence beta @(Catalog @($shared))),(Item transfers '' @(File 'owned\a.zip') $op),(Item workflow alpha @(File 'state.json' '{}')),(Item workflow beta @(File 'state.json' '{}')))) (Target)
                Assert ($plan.units.Count -eq 1 -and @($plan.units[0].items).Count -eq 5) 'shared operation did not merge the two tasks'
                Assert ($plan.units[0].items[0].role -ceq 'transfers' -and $plan.units[0].items[-1].role -ceq 'workflow') 'publication order'
            }
            'target-states'{
                $files=@(File 'state.json' '{}')
                $wf="Workflow\$repo\tasks\alpha"
                $skip=New-ConsolidationPlan (Inventory @((Item workflow alpha $files))) (Target @{$wf=(Seen $true $files)})
                Assert ($skip.units[0].status -ceq 'skip' -and $skip.summary.skip -eq 1) 'identical target not skipped'
                $acl=New-ConsolidationPlan (Inventory @((Item workflow alpha $files))) (Target @{$wf=(Seen $true $files $false)})
                Assert ($acl.units[0].status -ceq 'hold' -and $acl.units[0].reasons -ccontains 'acl-mismatch') 'identical bytes with a foreign ACL skipped'
                $other=New-ConsolidationPlan (Inventory @((Item workflow alpha $files))) (Target @{$wf=(Seen $true @(File 'state.json' '{}' ('f'*64)))})
                Assert ($other.units[0].status -ceq 'hold' -and $other.units[0].reasons -ccontains 'conflict') 'different target bytes not held'
                $extra=New-ConsolidationPlan (Inventory @((Item workflow alpha $files))) (Target @{$wf=(Seen $true @($files+(File 'task.lock')))})
                Assert ($extra.units[0].reasons -ccontains 'conflict') 'extra target file not held'
                $reparse=Item workflow alpha $files;$reparse.reparse=$true
                Assert ((New-ConsolidationPlan (Inventory @($reparse)) (Target)).units[0].reasons -ccontains 'reparse') 'source reparse accepted'
            }
            'workflow-divergence-held'{
                # Same task, same event count, different content: no winner, no splice.
                $wf="Workflow\$repo\tasks\integration";$mine=@(File 'state.json' '{"version":3}' ('1'*64));$theirs=@(File 'state.json' '{"version":3}' ('2'*64))
                $cur="Harness\$repo\tasks\integration";$h1=@(File 'CURRENT.json' '{}' ('3'*64));$h2=@(File 'CURRENT.json' '{}' ('4'*64))
                $plan=New-ConsolidationPlan (Inventory @((Item workflow integration $mine),(Item harness integration $h1),(Item workflow other @(File 'state.json' '{}')))) (Target @{$wf=(Seen $true $theirs);$cur=(Seen $true $h2)})
                $held=Unit $plan $wf
                Assert ($held.Count -eq 1 -and $held[0].status -ceq 'hold' -and @($held[0].items|Where-Object state -CEQ 'conflict').Count -eq 2) 'divergence not held'
                Assert ((Unit $plan "Workflow\$repo\tasks\other")[0].status -ceq 'publish') 'unrelated unit held'
                Assert ($plan.summary.hold -eq 1 -and $plan.summary.publish -eq 1) 'summary'
            }
            'deployment-dependency'{
                $receipt=Item deployment '' @((File "$dep.json" '{}'),(File "$dep.config.json" '{}'))
                $plan=New-ConsolidationPlan (Inventory @($receipt,(Item evidence alpha @(Catalog @())))) @{}
                $d=Unit $plan "Artifacts\deployments\$dep";$t=Unit $plan "Artifacts\evidence-v2\$dep\$repo\tasks\alpha"
                Assert ($plan.units[0].kind -ceq 'deployment' -and $d[0].status -ceq 'publish' -and $t[0].status -ceq 'publish' -and $t[0].dependsOn -ccontains $d[0].unitId) 'deployment not published first'
                $plan=New-ConsolidationPlan (Inventory @($receipt,(Item evidence alpha @(Catalog @())))) @{"Artifacts\deployments\$dep"=(Seen $true @((File "$dep.json" '{}' ('9'*64)),(File "$dep.config.json" '{}')))}
                Assert ((Unit $plan "Artifacts\evidence-v2\$dep\$repo\tasks\alpha")[0].reasons -ccontains 'dependency-held') 'task of a conflicting deployment not held'
                $plan=New-ConsolidationPlan (Inventory @((Item evidence alpha @(Catalog @())))) @{}
                Assert ($plan.units[0].reasons -ccontains 'reference-missing') 'missing deployment accepted'
            }
            'unreferenced-operation-excluded'{
                $plan=New-ConsolidationPlan (Inventory @((Item transfers '' @(File 'a.zip') $op2),(Item workflow alpha @(File 'state.json' '{}'))) @([pscustomobject]@{key='Artifacts\credentials';reason='credentials'})) (Target)
                Assert ($plan.units.Count -eq 1 -and @($plan.excluded|Where-Object {$_.key -ceq "Artifacts\transfers\$op2" -and $_.reason -ceq 'unreferenced-transfer'}).Count -eq 1 -and @($plan.excluded|Where-Object reason -CEQ 'credentials').Count -eq 1) 'excluded list'
            }
            'invalid-record-held'{
                $plan=New-ConsolidationPlan (Inventory @((Item workflow alpha @(File 'state.json' '{"broken":')))) (Target)
                Assert ($plan.units[0].reasons -ccontains 'source-invalid') 'unparsable record accepted'
            }
            'target-keys-cover-references'{
                $keys=Get-ConsolidationTargetKeys (Inventory @((Item evidence alpha @(Catalog @("$logical\Artifacts\transfers\$op\a.zip","$logical\Workflow\$repo\tasks\beta\state.json")))))
                foreach($k in @("Artifacts\evidence-v2\$dep\$repo\tasks\alpha","Artifacts\transfers\$op","Artifacts\deployments\$dep","Workflow\$repo\tasks\beta")){Assert ($keys -ccontains $k) ('missing target key '+$k)}
                Assert ((Get-ConsolidationItemKey ("ARTIFACTS\Transfers\"+$op.ToUpperInvariant().Replace('0','A')+'\x')).key -ceq ('Artifacts\transfers\'+('a'*32))) 'key not canonical'
            }
            'manifest-recheck'{
                $wf="Workflow\$repo\tasks\alpha";$ev="Artifacts\evidence-v2\$dep\$repo\tasks\alpha"
                $inventory=Inventory @((Item workflow alpha @(File 'state.json' '{}')),(Item evidence alpha @(Catalog @())))
                $approved=New-ConsolidationPlan $inventory (Target)
                Assert ((Test-ConsolidationManifest $approved (New-ConsolidationPlan $inventory (Target))).verdict -ceq 'publish') 'unchanged'
                # The evidence item was published by an interrupted earlier run: converge.
                $evFiles=@(Catalog @())
                $partial=New-ConsolidationPlan $inventory (Target @{$ev=(Seen $true $evFiles)})
                Assert ((Test-ConsolidationManifest $approved $partial).verdict -ceq 'publish') 'own partial publication rejected'
                $done=New-ConsolidationPlan $inventory (Target @{$ev=(Seen $true $evFiles);$wf=(Seen $true @(File 'state.json' '{}'))})
                Assert ((Test-ConsolidationManifest $approved $done).verdict -ceq 'complete') 'complete not recognized'
                $changed=New-ConsolidationPlan (Inventory @((Item workflow alpha @(File 'state.json' '{}' ('7'*64))),(Item evidence alpha @(Catalog @())))) (Target)
                Assert ((Test-ConsolidationManifest $approved $changed).verdict -ceq 'source-changed') 'source change missed'
                $foreign=New-ConsolidationPlan $inventory (Target @{$wf=(Seen $true @(File 'state.json' '{}' ('8'*64)))})
                Assert ((Test-ConsolidationManifest $approved $foreign).verdict -ceq 'target-changed') 'target change missed'
                $other=[ordered]@{}+$approved;$other.sourceRoot='C:\elsewhere'
                $threw=$false;try{$null=Test-ConsolidationManifest $other $foreign}catch{$threw=$true};Assert $threw 'foreign manifest accepted'
                $heldPlan=New-ConsolidationPlan (Inventory @((Item evidence alpha @(Catalog @("$logical\Artifacts\transfers\$op\a"))))) (Target)
                Assert ((Test-ConsolidationManifest $heldPlan $heldPlan).verdict -ceq 'hold') 'held unit executable'
            }
            'referenced-file-missing'{
                # Belonging to a unit is not enough: the referenced file itself must exist.
                $wfPath="$logical\Workflow\$repo\tasks\alpha"
                $plan=New-ConsolidationPlan (Inventory @((Item workflow alpha @(File 'state.json' (ConvertTo-Json -Compress @{next="$wfPath\missing.json"}))))) (Target)
                Assert ($plan.units[0].status -ceq 'hold' -and $plan.units[0].reasons -ccontains 'reference-missing') 'missing file in the carried item accepted'
                $plan=New-ConsolidationPlan (Inventory @((Item workflow alpha @(File 'state.json' (ConvertTo-Json -Compress @{self="$wfPath\STATE.json";root=$wfPath}))))) (Target)
                Assert ($plan.units[0].status -ceq 'publish') ('present file rejected '+($plan.units[0].reasons -join ','))
                # A carried operation without the referenced file.
                $plan=New-ConsolidationPlan (Inventory @((Item evidence alpha @(Catalog @("$logical\Artifacts\transfers\$op\owned\a.zip"))),(Item transfers '' @(File 'owned\b.zip') $op))) (Target)
                Assert ($plan.units[0].reasons -ccontains 'reference-missing') 'missing file in a carried operation accepted'
                # An operation directory that exists in the real store without the referenced file.
                $plan=New-ConsolidationPlan (Inventory @((Item evidence alpha @(Catalog @("$logical\Artifacts\transfers\$op\owned\a.zip"))))) (Target @{"Artifacts\transfers\$op"=(Seen $true @(File 'owned\b.zip'))})
                Assert ($plan.units[0].reasons -ccontains 'reference-missing') 'missing file in an existing operation accepted'
                # The deployment an Evidence task depends on needs both of its records.
                $plan=New-ConsolidationPlan (Inventory @((Item evidence alpha @(Catalog @())))) @{"Artifacts\deployments\$dep"=(Seen $true @(File "$dep.config.json"))}
                Assert ($plan.units[0].reasons -ccontains 'reference-missing') 'deployment without its receipt accepted'
            }
            'unchecked-record-held'{
                # A record whose strings were not examined is never published as checked.
                $plan=New-ConsolidationPlan (Inventory @((Item workflow alpha @(File 'state.json' $null ('e'*64) $true)))) (Target)
                Assert ($plan.units[0].status -ceq 'hold' -and $plan.units[0].reasons -ccontains 'source-unchecked') 'unchecked large record accepted'
                $plan=New-ConsolidationPlan (Inventory @((Item workflow alpha @(File 'CURRENT.previous' $null ('e'*64) $true)))) (Target)
                Assert ($plan.units[0].reasons -ccontains 'source-unchecked') 'unchecked JSON-looking record accepted'
                $plan=New-ConsolidationPlan (Inventory @((Item workflow alpha @(File 'state.json' $null)))) (Target)
                Assert ($plan.units[0].status -ceq 'hold' -and $plan.units[0].reasons -ccontains 'source-invalid') 'named JSON record that is not JSON accepted'
                $plan=New-ConsolidationPlan (Inventory @((Item workflow alpha @((File 'state.json' '{}'),(File 'task.lock' $null))))) (Target)
                Assert ($plan.units[0].status -ceq 'publish') 'non-JSON file held'
            }
            'multi-workflow-unit-held'{
                # Two Workflow directories appear one rename at a time: the unit is held whole.
                $plan=New-ConsolidationPlan (Inventory (Merged)) (Target)
                Assert ($plan.units.Count -eq 1 -and $plan.units[0].status -ceq 'hold' -and $plan.units[0].reasons -ccontains 'multi-workflow-unit') ('shared operation '+($plan.units[0].reasons -join ','))
                # A task-to-task reference merges the same way.
                $plan=New-ConsolidationPlan (Inventory @((Item workflow alpha @(File 'state.json' (ConvertTo-Json -Compress @{next="$logical\Workflow\$repo\tasks\beta\state.json"}))),(Item workflow beta @(File 'state.json' '{}')))) (Target)
                Assert ($plan.units.Count -eq 1 -and $plan.units[0].reasons -ccontains 'multi-workflow-unit') ('task reference '+($plan.units[0].reasons -join ','))
                Assert ($plan.summary.hold -eq 1 -and $plan.summary.publish -eq 0) 'summary'
            }
            'workflow-already-visible-held'{
                # The task is listed already, so every other item would appear mid-publication.
                $items=@((Item evidence alpha @(Catalog @())),(Item workflow alpha @(File 'state.json' '{}')))
                $plan=New-ConsolidationPlan (Inventory $items) (Target (Present $items @("Workflow\$repo\tasks\alpha")))
                Assert ($plan.units[0].status -ceq 'hold' -and $plan.units[0].reasons -ccontains 'workflow-already-visible') ('single task '+($plan.units[0].reasons -join ','))
                # One of two merged tasks already listed: same rule, and only one Workflow is new.
                $items=Merged
                $plan=New-ConsolidationPlan (Inventory $items) (Target (Present $items @("Workflow\$repo\tasks\alpha")))
                Assert ($plan.units[0].reasons -ccontains 'workflow-already-visible' -and $plan.units[0].reasons -cnotcontains 'multi-workflow-unit') ('merged '+($plan.units[0].reasons -join ','))
                # An existing Workflow that differs is a conflict and is listed too.
                $items=@((Item evidence alpha @(Catalog @())),(Item workflow alpha @(File 'state.json' '{}')))
                $plan=New-ConsolidationPlan (Inventory $items) (Target @{"Workflow\$repo\tasks\alpha"=(Seen $true @(File 'state.json' '{}' ('f'*64)))})
                Assert ($plan.units[0].reasons -ccontains 'workflow-already-visible' -and $plan.units[0].reasons -ccontains 'conflict') ('conflicting '+($plan.units[0].reasons -join ','))
            }
            'all-identical-unit-not-held'{
                $items=@((Item evidence alpha @(Catalog @())),(Item workflow alpha @(File 'state.json' '{}')))
                $plan=New-ConsolidationPlan (Inventory $items) (Target (Present $items @($items|ForEach-Object key)))
                Assert ($plan.units[0].status -ceq 'skip' -and @($plan.units[0].reasons).Count -eq 0) ('single task '+($plan.units[0].reasons -join ','))
                $items=Merged
                $plan=New-ConsolidationPlan (Inventory $items) (Target (Present $items @($items|ForEach-Object key)))
                Assert ($plan.units[0].status -ceq 'skip' -and @($plan.units[0].reasons).Count -eq 0) ('merged '+($plan.units[0].reasons -join ','))
            }
            'only-new-workflow-not-held'{
                # Only beta's Workflow is new: one rename makes beta appear, nothing is mid-way.
                $items=Merged
                $plan=New-ConsolidationPlan (Inventory $items) (Target (Present $items @($items|Where-Object key -CNE "Workflow\$repo\tasks\beta"|ForEach-Object key)))
                Assert ($plan.units[0].status -ceq 'publish' -and @($plan.units[0].reasons).Count -eq 0) ($plan.units[0].reasons -join ',')
            }
            'single-workflow-unit-not-held'{
                $plan=New-ConsolidationPlan (Inventory @((Item evidence alpha @(Catalog @())),(Item harness alpha @(File 'CURRENT.json' '{}')),(Item workflow alpha @(File 'state.json' '{}')))) (Target)
                Assert ($plan.units[0].status -ceq 'publish' -and @($plan.units[0].reasons).Count -eq 0) ('single task '+($plan.units[0].reasons -join ','))
                # Merged with a task that has no Workflow directory: still one new Workflow.
                $plan=New-ConsolidationPlan (Inventory @((Item transfers '' @(File 'owned\a.zip') $op),(Item evidence alpha @(Catalog @($shared))),(Item evidence beta @(Catalog @($shared))),(Item workflow alpha @(File 'state.json' '{}')))) (Target)
                Assert ($plan.units.Count -eq 1 -and $plan.units[0].status -ceq 'publish') ('merged '+($plan.units[0].reasons -join ','))
            }
            'multi-workflow-with-other-reasons'{
                # Reasons are independent: the new ones neither replace nor hide the others.
                $plan=New-ConsolidationPlan (Inventory @((Item evidence alpha @(Catalog @("$logical\Artifacts\transfers\$op2\owned\x.zip"))),(Item workflow alpha @(File 'state.json' (ConvertTo-Json -Compress @{next="$logical\Workflow\$repo\tasks\beta\state.json"}))),(Item workflow beta @(File 'state.json' '{"broken":')))) (Target)
                foreach($r in @('multi-workflow-unit','reference-missing','source-invalid')){Assert ($plan.units[0].reasons -ccontains $r) ('missing '+$r+' in '+($plan.units[0].reasons -join ','))}
                # A deployment unit carries no Workflow; its dependant's hold does not hold it.
                $receipt=Item deployment '' @((File "$dep.json" '{}'),(File "$dep.config.json" '{}'))
                $plan=New-ConsolidationPlan (Inventory (@($receipt)+@(Merged))) @{}
                $d=Unit $plan "Artifacts\deployments\$dep";$t=Unit $plan "Workflow\$repo\tasks\alpha"
                Assert ($d[0].status -ceq 'publish' -and $t[0].reasons -ccontains 'multi-workflow-unit' -and $t[0].reasons -cnotcontains 'dependency-held') ('deployment '+$d[0].status+' task '+($t[0].reasons -join ','))
            }
            'target-only-reference-unreadable'{
                # Present and private is not enough: the referenced bytes must have been read.
                $items=@((Item evidence alpha @(Catalog @("$logical\Artifacts\transfers\$op\owned\a.zip"))))
                $plan=New-ConsolidationPlan (Inventory $items) (Target @{"Artifacts\transfers\$op"=(Seen $true @(Locked 'owned\a.zip'))})
                Assert ($plan.units[0].status -ceq 'hold' -and $plan.units[0].reasons -ccontains 'reference-missing') 'unreadable referenced file accepted'
                # A reference to the directory needs every file below it readable.
                $plan=New-ConsolidationPlan (Inventory @((Item evidence alpha @(Catalog @("$logical\Artifacts\transfers\$op"))))) (Target @{"Artifacts\transfers\$op"=(Seen $true @((File 'owned\b.zip'),(Locked 'owned\a.zip')))})
                Assert ($plan.units[0].reasons -ccontains 'reference-missing') 'directory with an unreadable file accepted'
                $plan=New-ConsolidationPlan (Inventory @((Item evidence alpha @(Catalog @())))) @{"Artifacts\deployments\$dep"=(Seen $true @((File "$dep.json"),(Locked "$dep.config.json")))}
                Assert ($plan.units[0].reasons -ccontains 'reference-missing') 'unreadable deployment record accepted'
                # The readable control still passes, also beside an unreadable file it does not reference.
                $plan=New-ConsolidationPlan (Inventory $items) (Target @{"Artifacts\transfers\$op"=(Seen $true @((File 'owned\a.zip'),(Locked 'owned\other.zip')))})
                Assert ($plan.units[0].status -ceq 'publish') ('readable reference rejected '+($plan.units[0].reasons -join ','))
            }
            'ac-s4-b'{
                # A reference under the former root maps to the same relative key as one
                # under the store root: checked, and held when the operation is absent.
                $inventory=Inventory @((Item evidence alpha @(Catalog @("$legacyRoot\Artifacts\transfers\$op\owned\a.zip"))))
                $plan=New-ConsolidationPlan $inventory (Target)
                Assert ($plan.units[0].status -ceq 'hold' -and $plan.units[0].reasons -ccontains 'reference-missing') 'former-root reference not checked'
                Assert ((Get-ConsolidationTargetKeys $inventory) -ccontains "Artifacts\transfers\$op") 'former-root reference not a target key'
                $plan=New-ConsolidationPlan $inventory (Target @{"Artifacts\transfers\$op"=(Seen $true @(File 'owned\a.zip'))})
                Assert ($plan.units[0].status -ceq 'publish') ('mapped reference rejected '+($plan.units[0].reasons -join ','))
                Assert ((@($plan.legacyRoots) -join ',') -ceq $legacyRoot) 'legacyRoots not recorded in the plan'
                # LocalCache paths stay rejected, former root or not.
                $plan=New-ConsolidationPlan (Inventory @((Item workflow alpha @(File 'state.json' (ConvertTo-Json -Compress @{p="$source\Artifacts\transfers\$op\a.zip"}))))) (Target)
                Assert ($plan.units[0].reasons -ccontains 'reference-localcache') 'LocalCache path accepted'
            }
            'ac-s4-c'{
                # Paths under neither root are not references: worktree sources, a sibling
                # that only shares the prefix, and another directory beside the store root.
                foreach($path in @('D:\repositories\worktree\evidence\x.json',"C:\Users\fixture\AppData\Local\OneStarMaker2\Artifacts\transfers\$op\a.zip","C:\Users\fixture\OneStarMakerX\Artifacts\transfers\$op\a.zip")){
                    $plan=New-ConsolidationPlan (Inventory @((Item workflow alpha @(File 'state.json' (ConvertTo-Json -Compress @{p=$path}))))) (Target)
                    Assert ($plan.units[0].status -ceq 'publish' -and @($plan.units[0].reasons).Count -eq 0) ('outside path treated as a reference '+$path)
                }
                # The alias in force at execution must be the one the manifest was planned with.
                $inventory=Inventory @((Item workflow alpha @(File 'state.json' '{}')))
                $approved=New-ConsolidationPlan $inventory (Target);$approved.legacyRoots=@('C:\Users\other\AppData\Local\OneStarMaker')
                $threw=$false;try{$null=Test-ConsolidationManifest $approved (New-ConsolidationPlan $inventory (Target))}catch{$threw=$_.Exception.Message -ceq 'manifest-invalid'}
                Assert $threw 'manifest planned with another alias accepted'
                $approved=New-ConsolidationPlan $inventory (Target);$approved.Remove('legacyRoots')
                $threw=$false;try{$null=Test-ConsolidationManifest $approved (New-ConsolidationPlan $inventory (Target))}catch{$threw=$_.Exception.Message -ceq 'manifest-invalid'}
                Assert $threw 'manifest without an alias accepted'
            }
            'plan-module-is-pure'{
                $ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot '../StoreConsolidationPlan.psm1'),[ref]$null,[ref]$null)
                $types=@($ast.FindAll({param($n)$n -is [Management.Automation.Language.TypeExpressionAst] -or $n -is [Management.Automation.Language.TypeConstraintAst]},$true)|ForEach-Object {$_.TypeName.FullName})
                foreach($type in $types){Assert ($type -cin @('Text.Json.JsonDocument','Text.Json.JsonElement','Collections.Generic.Stack[Text.Json.JsonElement]','Collections.Generic.List[string]','Collections.Generic.List[object]','Collections.Generic.HashSet[string]','StringComparer','StringComparison','string','regex','Text.RegularExpressions.RegexOptions','Convert','Security.Cryptography.SHA256','Text.Encoding','long','pscustomobject','ordered')) ('impure type '+$type)}
                $commands=@($ast.FindAll({param($n)$n -is [Management.Automation.Language.CommandAst]},$true)|ForEach-Object GetCommandName)
                # A null name is an invocation of the module's own script blocks.
                foreach($name in @($commands|Where-Object {$null -ne $_})){Assert ($name -cin @('Set-StrictMode','Export-ModuleMember','Sort-Object','ForEach-Object','Where-Object','Find','Join','Test-ConsolidationWithin','Get-ConsolidationItemKey','Get-ConsolidationJsonStrings','Get-ConsolidationItemReferences','Test-ConsolidationItemHas','Test-ConsolidationItemReadable','Get-ConsolidationFileSignature','Get-ConsolidationItemState')) ('impure command '+$name)}
            }
        }
        $executed.Add($case)
    }catch{$failed.Add($case+': '+$_.Exception.Message)}
}
if($ResultPath){[IO.File]::WriteAllText($ResultPath,(ConvertTo-Json -Depth 8 -InputObject ([ordered]@{registered=$cases;selected=$selected;executed=@($executed);failed=@($failed)})),[Text.UTF8Encoding]::new($false))}
foreach($name in $executed){"PASS $name"};foreach($name in $failed){"FAIL $name"}
"Cases: $($selected.Count)/$($cases.Count); passed: $($executed.Count); failed: $($failed.Count)"
if($failed.Count -or $executed.Count -ne $selected.Count){exit 1}
