param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# Every Artifacts module that owns a root derives it from the store-root
# resolver: with the modules' own offline roots left unset and only the
# resolver's Known Folder seams pointed at a temporary migrated store root, each
# root function answers below <base>\OneStarMaker. The second half checks that
# the same functions stop at the gate while an owned role is still under the
# former root. Nothing here touches the real store or the real Known Folders.
. (Join-Path $PSScriptRoot '../../Workflow/tests/StoreRootTestSupport.ps1')
$tools=[IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..','..'))
$root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-store-root-wiring-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root)|Out-Null
$moduleNames=@('ArtifactPaths','BuildContract','BuildStore','ContentStore','CredentialPathAcl','EvidenceContract','EvidencePaths','EvidenceReset','EvidenceSchedule','SeriesRetirementStore','StoreConsolidation')
$cases=@($moduleNames|ForEach-Object {'ac-s1-b-'+$_.ToLowerInvariant()})+@('ac-i3-e-modules')
$selected=@(if($Case -ceq '*'){$cases}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
function Assert([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
$modules=@{}
# The Build source boundary uses the packaging policy type, which the artifact application loads on demand.
$application=Import-Module ([IO.Path]::Combine($tools,'Artifacts/ArtifactApplication.psm1')) -PassThru
& $application {Import-ArtifactPackage}
foreach($pair in @(@('ArtifactPaths','Artifacts/ArtifactPaths.psm1'),@('BuildContract','Artifacts/BuildContract.psm1'),@('BuildStore','Artifacts/BuildStore.psm1'),@('ContentStore','Artifacts/ContentStore.psm1'),@('CredentialPathAcl','Artifacts/Credentials/CredentialPathAcl.psm1'),@('EvidenceContract','Artifacts/EvidenceContract.psm1'),@('EvidencePaths','Artifacts/EvidencePaths.psm1'),@('EvidenceReset','Artifacts/EvidenceReset.psm1'),@('EvidenceSchedule','Artifacts/EvidenceSchedule.psm1'),@('SeriesRetirementStore','Artifacts/SeriesRetirementStore.psm1'),@('StoreConsolidation','Artifacts/StoreConsolidation.psm1'),@('TaskEventStore','Workflow/TaskEventStore.psm1'))){
    $modules[$pair[0]]=Import-Module ([IO.Path]::Combine($tools,$pair[1])) -PassThru
}
# Each module's root answers, as a script block over the module's own scope so
# that unexported getters are reachable too. $fx is the fixture of the case.
function Get-Answers($Fx){
    return [ordered]@{
        ArtifactPaths=@{Call={& $modules.ArtifactPaths {Get-ArtifactRoot}};Expected=[IO.Path]::Combine($Fx.Root,'Artifacts','transfers')}
        BuildStore=@{Call={& $modules.BuildStore {Get-BuildRoot}};Expected=[IO.Path]::Combine($Fx.Root,'Artifacts','builds-v1')}
        ContentStore=@{Call={& $modules.ContentStore {Get-ContentRoot}};Expected=[IO.Path]::Combine($Fx.Root,'Artifacts','contents-v1')}
        CredentialPathAcl=@{Call={& $modules.CredentialPathAcl {Get-CredentialRoot ''}};Expected=[IO.Path]::Combine($Fx.Root,'Artifacts','credentials')}
        EvidenceContract=@{Call={& $modules.EvidenceContract {Get-EvidenceDeploymentRoot}};Expected=[IO.Path]::Combine($Fx.Root,'Artifacts','deployments')}
        EvidencePaths=@{Call={& $modules.EvidencePaths {Get-EvidenceRoot}};Expected=[IO.Path]::Combine($Fx.Root,'Artifacts','evidence-v2')}
        EvidenceReset=@{Call={& $modules.EvidenceReset {Get-EvidenceResetRoot}};Expected=[IO.Path]::Combine($Fx.Root,'Artifacts')}
        EvidenceSchedule=@{Call={& $modules.EvidenceSchedule {Get-EvidenceRuntimeRoot}};Expected=[IO.Path]::Combine($Fx.Root,'Artifacts','runtime')}
        SeriesRetirementStore=@{Call={& $modules.SeriesRetirementStore {Get-SeriesRetirementRoot}};Expected=[IO.Path]::Combine($Fx.Root,'Artifacts','retirements-v1')}
        StoreConsolidation=@{Call={& $modules.StoreConsolidation {Get-ConsolidationLogicalRoot}};Expected=$Fx.Root}
        TaskEventStore=@{Call={& $modules.TaskEventStore {Get-WorkflowRoot}};Expected=[IO.Path]::Combine($Fx.Root,'Workflow')}
    }
}
function Invoke-Answer([scriptblock]$Call){try{return [string](& $Call)}catch{return 'error:'+$_.Exception.Message}}
try{
    foreach($case in $selected){
        $work=[IO.Path]::Combine($root,$case)
        try{
            Assert ($case -cin $cases) 'unknown case'
            $fx=New-StoreRootTestBase $work
            $answers=Get-Answers $fx
            if($case -ceq 'ac-i3-e-modules'){
                # Marker present, Workflow still under the former root: every getter stops
                # with legacy-store-present, the new root keeps only its marker, and the
                # only file opened on the new root side is the marker.
                [IO.Directory]::CreateDirectory([IO.Path]::Combine($fx.LegacyRoot,'Workflow'))|Out-Null
                $log=[Collections.Generic.List[string]]::new();Set-StoreRootTestSeams $fx.Base $fx.LegacyBase (New-StoreRootTestMarkerIo $log)
                $before=@((Get-StoreRootTestTree $fx.Base),(Get-StoreRootTestTree $fx.LegacyBase))
                foreach($name in $answers.Keys){
                    $actual=Invoke-Answer $answers[$name].Call
                    Assert ($actual -ceq 'error:legacy-store-present') ($name+': '+$actual)
                }
                # The binding roots of the scheduled job and the Build source boundary stop too.
                $actual=Invoke-Answer {& $modules.EvidenceSchedule {Get-EvidenceBindingRoots}};Assert ($actual -ceq 'error:legacy-store-present') ('Get-EvidenceBindingRoots: '+$actual)
                $actual=Invoke-Answer {& $modules.BuildContract {param($p)Assert-BuildSourceRoot $p} $work};Assert ($actual -ceq 'error:legacy-store-present') ('Assert-BuildSourceRoot: '+$actual)
                Assert ((Get-StoreRootTestTree $fx.Base) -ceq $before[0] -and (Get-StoreRootTestTree $fx.LegacyBase) -ceq $before[1]) 'a root changed'
                $opened=@($log|Where-Object {$_.StartsWith('open:')}|ForEach-Object {$_.Substring(5)}|Sort-Object -Unique)
                Assert ($opened.Count -eq 1 -and $opened[0] -ceq [IO.Path]::Combine($fx.Root,'store-root.json')) ('opened '+($opened -join ','))
            }elseif($case -ceq 'ac-s1-b-buildcontract'){
                # The Build source boundary covers both roots' Artifacts and Workflow, and their parents.
                foreach($bad in @([IO.Path]::Combine($fx.Root,'Artifacts','x'),[IO.Path]::Combine($fx.Root,'Workflow'),[IO.Path]::Combine($fx.LegacyRoot,'Artifacts','x'),[IO.Path]::Combine($fx.LegacyRoot,'Workflow','y'),$fx.Root,$fx.LegacyRoot)){
                    $actual=try{& $modules.BuildContract {param($p)Assert-BuildSourceRoot $p} $bad;'accepted'}catch{'error:'+$_.Exception.Message}
                    Assert ($actual -ceq 'error:invalid-source') ('accepted '+$bad+': '+$actual)
                }
                $ok=[IO.Path]::Combine($work,'player');[IO.Directory]::CreateDirectory($ok)|Out-Null
                $actual=try{& $modules.BuildContract {param($p)Assert-BuildSourceRoot $p} $ok;'accepted'}catch{'error:'+$_.Exception.Message}
                Assert ($actual -ceq 'accepted') ('rejected a plain source '+$actual)
            }else{
                $name=@($answers.Keys|Where-Object {('ac-s1-b-'+$_.ToLowerInvariant()) -ceq $case})[0]
                $actual=Invoke-Answer $answers[$name].Call
                Assert ($actual -ceq $answers[$name].Expected) ($name+': expected '+$answers[$name].Expected+', got '+$actual)
                if($name -ceq 'EvidenceSchedule'){
                    $roots=& $modules.EvidenceSchedule {Get-EvidenceBindingRoots}
                    Assert ($roots.workflow -ceq [IO.Path]::Combine($fx.Root,'Workflow') -and $roots.evidence -ceq [IO.Path]::Combine($fx.Root,'Artifacts','evidence-v2') -and $roots.deployments -ceq [IO.Path]::Combine($fx.Root,'Artifacts','deployments') -and $roots.transfers -ceq [IO.Path]::Combine($fx.Root,'Artifacts','transfers') -and $roots.runtime -ceq [IO.Path]::Combine($fx.Root,'Artifacts','runtime')) 'binding roots are not below the store root'
                }
                Assert ((Get-StoreRootTestTree $fx.Base) -ceq (Get-StoreRootTestTree $fx.Base)) 'unstable tree'
            }
            $executed.Add($case)
        }catch{$failed.Add($case+': '+$_.Exception.Message+' '+$_.ScriptStackTrace.Split("`n")[0])}
        finally{Clear-StoreRootTestSeams}
    }
    if($ResultPath){[IO.File]::WriteAllText($ResultPath,(ConvertTo-Json -Depth 8 -InputObject ([ordered]@{registered=$cases;selected=$selected;executed=@($executed);failed=@($failed)})),[Text.UTF8Encoding]::new($false))}
    foreach($name in $executed){"PASS $name"};foreach($name in $failed){"FAIL $name"}
    "Cases: $($selected.Count)/$($cases.Count); passed: $($executed.Count); failed: $($failed.Count)"
    if($failed.Count -or $executed.Count -ne $selected.Count){exit 1}
}finally{
    Clear-StoreRootTestSeams
    if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)}
}
