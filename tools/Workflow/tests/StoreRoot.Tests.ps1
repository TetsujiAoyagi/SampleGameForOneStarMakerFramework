param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# The store-root resolver: every failure path and the success path, on temporary
# stand-ins for the UserProfile and LocalApplicationData Known Folders. No case
# reads or writes the real store, the real former root or the real Known
# Folders; the one child process that observes the real Known Folder values
# replaces the marker open through the seam and never reaches the former root.
. (Join-Path $PSScriptRoot 'StoreRootTestSupport.ps1')
$module=Import-StoreRootTestModule
$tools=[IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..','..'))
$repo=[IO.Path]::GetFullPath([IO.Path]::Combine($tools,'..'))
$root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-store-root-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root)|Out-Null
$cases=@('ac-i1-e','ac-i2-a','ac-i2-b','ac-i2-c','ac-i3-b','ac-i3-c','ac-i3-d','ac-i3-f-1','ac-i3-f-2','ac-i3-f-3','ac-i3-f-4','ac-i3-f-5','ac-i3-f-6','ac-i4-a','ac-i5-a','ac-i5-b','ac-i5-c','ac-i6-a')
$selected=@(if($Case -ceq '*'){$cases}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
$observed=[ordered]@{}
$codes=@('store-not-migrated','legacy-store-present','store-root-unavailable')
function Assert([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
# Runs the resolver and returns the root, or the thrown message.
function Invoke-Resolver{try{return Get-OsmStoreRoot}catch{return $_.Exception.Message}}
function Assert-Code([string]$Actual,[string]$Expected,[string]$Label){Assert ($Actual -ceq $Expected) ($Label+': expected '+$Expected+', got '+$Actual)}
# Every message a failure may carry is one of the three codes and nothing more:
# no base path, no package name, no SID, no path element other than OneStarMaker.
function Assert-FixedMessage([string]$Message,[string]$Label,[string[]]$Secrets){
    Assert ($Message -cin $codes) ($Label+': message is not a fixed code: '+$Message)
    foreach($secret in $Secrets){if($secret){Assert (-not $Message.Contains($secret)) ($Label+': message leaked '+$secret)}}
}
function New-Junction([string]$Path,[string]$Target){
    try{New-Item -ItemType Junction -Path $Path -Target $Target -ErrorAction Stop|Out-Null;return $true}catch{return $false}
}
function Remove-Junction([string]$Path){if([IO.Directory]::Exists($Path)){[IO.Directory]::Delete($Path,$false)}}
# Imports the domain modules whose root getters must all stop at the gate.
function Get-DomainGetters{
    $modules=@{}
    foreach($pair in @(@('TaskEventStore','Workflow/TaskEventStore.psm1'),@('RecordStore','Harness/RecordStore.psm1'),@('ArtifactPaths','Artifacts/ArtifactPaths.psm1'),@('EvidencePaths','Artifacts/EvidencePaths.psm1'),@('CredentialPathAcl','Artifacts/Credentials/CredentialPathAcl.psm1'),@('BuildStore','Artifacts/BuildStore.psm1'),@('ContentStore','Artifacts/ContentStore.psm1'),@('StoreConsolidation','Artifacts/StoreConsolidation.psm1'),@('EvidenceSchedule','Artifacts/EvidenceSchedule.psm1'))){
        $modules[$pair[0]]=Import-Module ([IO.Path]::Combine($tools,$pair[1])) -PassThru
    }
    $r=$repo
    return [ordered]@{
        'Get-ArtifactRoot'={& $modules.ArtifactPaths {Get-ArtifactRoot}}.GetNewClosure()
        'Get-EvidenceRoot'={& $modules.EvidencePaths {Get-EvidenceRoot}}.GetNewClosure()
        'Get-WorkflowRoot'={& $modules.TaskEventStore {Get-WorkflowRoot}}.GetNewClosure()
        'Get-TaskDirectory'={& $modules.RecordStore {param($p)Get-TaskDirectory $p 'store-root-fixture' ''} $r}.GetNewClosure()
        'Get-CredentialRoot'={& $modules.CredentialPathAcl {Get-CredentialRoot ''}}.GetNewClosure()
        'Get-BuildRoot'={& $modules.BuildStore {Get-BuildRoot}}.GetNewClosure()
        'Get-ContentRoot'={& $modules.ContentStore {Get-ContentRoot}}.GetNewClosure()
        'Get-ConsolidationLogicalRoot'={& $modules.StoreConsolidation {Get-ConsolidationLogicalRoot}}.GetNewClosure()
        'Get-EvidenceBindingRoots'={& $modules.EvidenceSchedule {Get-EvidenceBindingRoots}}.GetNewClosure()
    }
}
function Invoke-Getter([scriptblock]$Getter){try{$null=& $Getter;return 'returned'}catch{return $_.Exception.Message}}
try{
    foreach($case in $selected){
        $work=[IO.Path]::Combine($root,$case)
        try{
            Assert ($case -cin $cases) 'unknown case'
            switch($case){
                'ac-i1-e'{
                    # A junction standing in for the former root's Harness, the store root's
                    # Harness and the repository is rejected by its physical path; so is a
                    # missing tail below the junction. The targets stay untouched.
                    $fx=New-StoreRootTestBase $work
                    foreach($directory in @([IO.Path]::Combine($fx.LegacyRoot,'Harness'),[IO.Path]::Combine($fx.Root,'Harness'))){[IO.Directory]::CreateDirectory($directory)|Out-Null}
                    $before=@((Get-StoreRootTestTree $fx.LegacyRoot),(Get-StoreRootTestTree $fx.Root))
                    $alias=[IO.Path]::Combine($work,'alias');$junctions=0
                    foreach($target in @([IO.Path]::Combine($fx.LegacyRoot,'Harness'),[IO.Path]::Combine($fx.Root,'Harness'),[IO.Path]::Combine($repo,'tools'))){
                        Remove-Junction $alias
                        if(-not (New-Junction $alias $target)){continue}
                        $junctions++
                        try{
                            foreach($path in @($alias,[IO.Path]::Combine($alias,'sub','missing'))){
                                $message=try{Assert-OsmExplicitStoreRoot $path;'accepted'}catch{$_.Exception.Message}
                                Assert-Code $message 'store-root-unavailable' ('junction '+$target+' via '+$path)
                            }
                        }finally{Remove-Junction $alias}
                    }
                    $observed['ac-i1-e']=if($junctions -eq 3){'junctions'}else{'skipped: junction unavailable ('+$junctions+'/3)'}
                    # The injected variant: the alias is an ordinary directory whose physical
                    # path, as reported by the identity seam, lies under each boundary.
                    $plain=[IO.Path]::Combine($work,'plain');[IO.Directory]::CreateDirectory($plain)|Out-Null
                    $paths=@($module.NestedModules|Where-Object Name -ceq 'WindowsStorePaths')[0]
                    foreach($target in @([IO.Path]::Combine($fx.LegacyRoot,'Harness'),[IO.Path]::Combine($fx.Root,'Harness'),[IO.Path]::Combine($repo,'tools'))){
                        & $paths {param($t)$script:IdentityHook={param($p)[pscustomobject]@{physicalPath=$t;volumeSerial='00000000';fileId='0'*32;isDirectory=$true}}.GetNewClosure()} $target
                        try{
                            $message=try{Assert-OsmExplicitStoreRoot ([IO.Path]::Combine($plain,'store'));'accepted'}catch{$_.Exception.Message}
                            Assert-Code $message 'store-root-unavailable' ('identity seam '+$target)
                        }finally{& $paths {$script:IdentityHook=$null}}
                    }
                    $message=try{Assert-OsmExplicitStoreRoot ([IO.Path]::Combine($plain,'store'));'accepted'}catch{$_.Exception.Message}
                    Assert-Code $message 'accepted' 'temp explicit root'
                    Assert ((Get-StoreRootTestTree $fx.LegacyRoot) -ceq $before[0] -and (Get-StoreRootTestTree $fx.Root) -ceq $before[1]) 'targets changed'
                }
                'ac-i2-a'{
                    # Root directory present, marker absent: the resolver and every domain
                    # root getter stop with store-not-migrated; only the marker is opened.
                    $fx=New-StoreRootTestBase $work -WithoutMarker;[IO.Directory]::CreateDirectory($fx.Root)|Out-Null
                    $log=[Collections.Generic.List[string]]::new();$queries=[Collections.Generic.List[string]]::new()
                    Set-StoreRootTestSeams $fx.Base $fx.LegacyBase (New-StoreRootTestMarkerIo $log) (New-StoreRootTestLegacyQuery $queries)
                    $before=Get-StoreRootTestTree $fx.Base
                    Assert-Code (Invoke-Resolver) 'store-not-migrated' 'resolver'
                    $getters=Get-DomainGetters
                    foreach($name in $getters.Keys){Assert-Code (Invoke-Getter $getters[$name]) 'store-not-migrated' $name}
                    Assert ((Get-StoreRootTestTree $fx.Base) -ceq $before) 'new root changed'
                    $opened=@($log|Where-Object {$_.StartsWith('open:')}|ForEach-Object {$_.Substring(5)}|Sort-Object -Unique)
                    Assert ($opened.Count -eq 1 -and $opened[0] -ceq [IO.Path]::Combine($fx.Root,'store-root.json')) ('opened '+($opened -join ','))
                    Assert ($queries.Count -eq 0) 'former root queried before the marker'
                    $observed['ac-i2-a-getters']=@($getters.Keys).Count
                }
                'ac-i2-b'{
                    $fx=New-StoreRootTestBase $work -WithoutMarker
                    $before=Get-StoreRootTestTree $fx.Base
                    Assert-Code (Invoke-Resolver) 'store-not-migrated' 'resolver'
                    Assert (-not [IO.Directory]::Exists($fx.Root) -and (Get-StoreRootTestTree $fx.Base) -ceq $before) 'root created'
                }
                'ac-i2-c'{
                    # A marker still being written (0 bytes) is unavailable, not migrated.
                    $fx=New-StoreRootTestBase $work -WithoutMarker;$null=New-StoreRootTestMarker $fx.Base ([byte[]]@())
                    $before=Get-StoreRootTestTree $fx.Base
                    Assert-Code (Invoke-Resolver) 'store-root-unavailable' 'empty marker'
                    Assert ((Get-StoreRootTestTree $fx.Base) -ceq $before) 'new root changed'
                }
                'ac-i3-b'{
                    # Marker present, no owned role under the former root (a not-owned
                    # directory may remain): the root is returned and nothing is opened there.
                    $fx=New-StoreRootTestBase $work;[IO.Directory]::CreateDirectory([IO.Path]::Combine($fx.LegacyRoot,'H2a Review'))|Out-Null
                    [IO.File]::WriteAllText([IO.Path]::Combine($fx.LegacyRoot,'H2a Review','notes.md'),'x')
                    $queries=[Collections.Generic.List[string]]::new();Set-StoreRootTestSeams $fx.Base $fx.LegacyBase $null (New-StoreRootTestLegacyQuery $queries)
                    $before=@((Get-StoreRootTestTree $fx.LegacyBase),(Get-StoreRootTestTree $fx.Base))
                    Assert-Code (Invoke-Resolver) $fx.Root 'resolver'
                    Assert ((Get-StoreRootTestTree $fx.LegacyBase) -ceq $before[0] -and (Get-StoreRootTestTree $fx.Base) -ceq $before[1]) 'trees changed'
                    Assert ($queries.Count -eq 3 -and @($queries|Where-Object {$_ -like 'query:*\OneStarMaker\Artifacts=absent' -or $_ -like 'query:*\OneStarMaker\Workflow=absent' -or $_ -like 'query:*\OneStarMaker\Harness=absent'}).Count -eq 3) ('queries '+($queries -join ','))
                }
                'ac-i3-c'{
                    # Marker absent and the former root alive: the marker decides first.
                    $fx=New-StoreRootTestBase $work -WithoutMarker;[IO.Directory]::CreateDirectory([IO.Path]::Combine($fx.LegacyRoot,'Workflow'))|Out-Null
                    $queries=[Collections.Generic.List[string]]::new();Set-StoreRootTestSeams $fx.Base $fx.LegacyBase $null (New-StoreRootTestLegacyQuery $queries)
                    Assert-Code (Invoke-Resolver) 'store-not-migrated' 'precedence'
                    Assert ($queries.Count -eq 0) 'former root queried before the marker'
                }
                'ac-i3-d'{
                    $fx=New-StoreRootTestBase $work
                    [IO.Directory]::CreateDirectory([IO.Path]::Combine($fx.LegacyRoot,'H2a Review'))|Out-Null
                    [IO.File]::WriteAllText([IO.Path]::Combine($fx.LegacyRoot,'Workflow'),'not a directory')
                    $before=Get-StoreRootTestTree $fx.LegacyBase
                    Assert-Code (Invoke-Resolver) $fx.Root 'file named like a role'
                    Assert ((Get-StoreRootTestTree $fx.LegacyBase) -ceq $before) 'former root changed'
                }
                {$_ -clike 'ac-i3-f-*'}{
                    $fx=New-StoreRootTestBase $work
                    $queries=[Collections.Generic.List[string]]::new();$inject=@{}
                    $rolePath=[IO.Path]::Combine($fx.LegacyRoot,'Workflow');$expected=$fx.Root
                    switch($case){
                        'ac-i3-f-1'{[IO.Directory]::CreateDirectory($rolePath)|Out-Null;[IO.File]::WriteAllText([IO.Path]::Combine($rolePath,'state.json'),'{}');$expected='legacy-store-present'}
                        'ac-i3-f-2'{}
                        'ac-i3-f-3'{[IO.Directory]::CreateDirectory($fx.LegacyRoot)|Out-Null;[IO.File]::WriteAllText($rolePath,'x')}
                        'ac-i3-f-4'{
                            $target=[IO.Path]::Combine($work,'elsewhere');[IO.Directory]::CreateDirectory($target)|Out-Null;[IO.Directory]::CreateDirectory($fx.LegacyRoot)|Out-Null
                            if(New-Junction $rolePath $target){$observed['ac-i3-f-4']='junction'}else{$inject[$rolePath]='reparse';$observed['ac-i3-f-4']='skipped: junction unavailable, status injected'}
                            $expected='store-root-unavailable'
                        }
                        'ac-i3-f-5'{$inject[$rolePath]='denied';$expected='store-root-unavailable'}
                        'ac-i3-f-6'{$inject[$rolePath]='error';$expected='store-root-unavailable'}
                    }
                    Set-StoreRootTestSeams $fx.Base $fx.LegacyBase $null (New-StoreRootTestLegacyQuery $queries $inject)
                    $before=Get-StoreRootTestTree $fx.LegacyBase
                    try{
                        Assert-Code (Invoke-Resolver) $expected $case
                        Assert ((Get-StoreRootTestTree $fx.LegacyBase) -ceq $before) 'former root changed'
                    }finally{if($case -ceq 'ac-i3-f-4'){Remove-Junction $rolePath}}
                    Assert (@($queries|Where-Object {-not $_.StartsWith('query:'+$fx.LegacyRoot+'\')}).Count -eq 0) ('query outside the former root '+($queries -join ','))
                }
                'ac-i4-a'{
                    # A child whose USERPROFILE / LOCALAPPDATA / HOME point elsewhere still
                    # derives both bases from the Known Folders. The marker open is replaced so
                    # the real UserProfile is only named, never read, and the former root is
                    # never reached.
                    $child=[IO.Path]::Combine($work,'env-child.ps1');[IO.Directory]::CreateDirectory($work)|Out-Null
                    [IO.File]::WriteAllText($child,@'
param([string]$Module)
Set-StrictMode -Version Latest;$ErrorActionPreference='Stop'
$m=Import-Module $Module -PassThru
$opened=[Collections.Generic.List[string]]::new()
& $m {param($l)$script:MarkerIoForTest=@{Open={param($p)$l.Add($p);throw [IO.FileNotFoundException]::new('fixture')}.GetNewClosure();FinalPath={};Read={};Close={}}} $opened
$message=try{Get-OsmStoreRoot}catch{$_.Exception.Message}
ConvertTo-Json -Compress -InputObject ([ordered]@{message=$message;marker=$(if($opened.Count){$opened[0]}else{''});legacy=(Get-OsmLegacyStoreRoot);envUser=$env:USERPROFILE;envLocal=$env:LOCALAPPDATA})
'@,[Text.UTF8Encoding]::new($false))
                    $fake=[IO.Path]::Combine($work,'fake-profile');[IO.Directory]::CreateDirectory([IO.Path]::Combine($fake,'AppData','Local'))|Out-Null
                    $info=[Diagnostics.ProcessStartInfo]::new([Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)
                    $info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
                    $info.Environment['USERPROFILE']=$fake;$info.Environment['LOCALAPPDATA']=[IO.Path]::Combine($fake,'AppData','Local');$info.Environment['HOME']=$fake
                    foreach($argument in @('-NoProfile','-File',$child,'-Module',$module.Path)){$info.ArgumentList.Add($argument)}
                    $process=[Diagnostics.Process]::Start($info)
                    try{
                        $out=$process.StandardOutput.ReadToEndAsync();$err=$process.StandardError.ReadToEndAsync()
                        Assert ($process.WaitForExit(30000)) 'child deadline';Assert ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),3000)) 'child pipes'
                        Assert ($process.ExitCode -eq 0) ('child failed '+$err.Result)
                        $result=@($out.Result -split "`r?`n"|Where-Object {$_.StartsWith('{')})[-1]|ConvertFrom-Json
                    }finally{if(-not $process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
                    $userProfile=[Environment]::GetFolderPath('UserProfile');$local=[Environment]::GetFolderPath('LocalApplicationData')
                    Assert ($result.envUser -ceq $fake) 'child environment not replaced'
                    Assert ($result.message -ceq 'store-not-migrated') ('child resolver '+$result.message)
                    Assert ($result.marker -ceq [IO.Path]::Combine($userProfile,'OneStarMaker','store-root.json')) ('marker base followed the environment: '+$result.marker)
                    Assert ($result.legacy -ceq [IO.Path]::Combine($local,'OneStarMaker')) ('legacy base followed the environment: '+$result.legacy)
                    Assert (-not $result.marker.StartsWith($fake,[StringComparison]::OrdinalIgnoreCase) -and -not $result.legacy.StartsWith($fake,[StringComparison]::OrdinalIgnoreCase)) 'derived from the environment'
                }
                'ac-i5-a'{
                    $fx=New-StoreRootTestBase $work
                    $log=[Collections.Generic.List[string]]::new()
                    foreach($base in @([IO.Path]::Combine($repo,'tools'),$repo,'D:\OneDrive\OSM-Artifacts','D:\OneDrive\OSM-Artifacts\sub','\\server\share')){
                        $log.Clear();Set-StoreRootTestSeams $base $fx.LegacyBase (New-StoreRootTestMarkerIo $log)
                        Assert-Code (Invoke-Resolver) 'store-root-unavailable' ('base '+$base)
                        Assert ($log.Count -eq 0) ('static rejection opened the marker for '+$base)
                    }
                    # An ancestor that is a junction, even when the marker is readable through it.
                    $target=[IO.Path]::Combine($work,'real-profile');[IO.Directory]::CreateDirectory($target)|Out-Null;$null=New-StoreRootTestMarker $target
                    $link=[IO.Path]::Combine($work,'linked-profile')
                    if(New-Junction $link $target){
                        try{Set-StoreRootTestSeams $link $fx.LegacyBase;Assert-Code (Invoke-Resolver) 'store-root-unavailable' 'ancestor junction'}finally{Remove-Junction $link}
                        $observed['ac-i5-a']='junction'
                    }else{$observed['ac-i5-a']='skipped: junction unavailable'}
                    Set-StoreRootTestSeams $fx.Base $fx.LegacyBase;Assert-Code (Invoke-Resolver) $fx.Root 'valid base after rejections'
                }
                'ac-i5-b'{
                    $fx=New-StoreRootTestBase $work -WithoutMarker
                    $variants=[ordered]@{
                        'foreign-sid'=(New-StoreRootTestMarkerBytes @{ownerSid='S-1-5-18'})
                        'schema-2'=(New-StoreRootTestMarkerBytes @{schemaVersion=2})
                        'other-kind'=(New-StoreRootTestMarkerBytes @{kind='other'})
                        'not-json'=([Text.UTF8Encoding]::new($false).GetBytes('kind=osm-store-root'))
                        'too-large'=([Text.UTF8Encoding]::new($false).GetBytes('{"schemaVersion":1,"kind":"osm-store-root","ownerSid":"'+[Security.Principal.WindowsIdentity]::GetCurrent().User.Value+'","createdAt":"2026-10-19T00:00:00Z","pad":"'+('x'*4100)+'"}'))
                        'missing-field'=(New-StoreRootTestMarkerBytes @{createdAt=$null})
                        'extra-field'=(New-StoreRootTestMarkerBytes @{note='x'})
                        'bom'=([byte[]]@(0xEF,0xBB,0xBF)+(New-StoreRootTestMarkerBytes))
                        'array'=([Text.UTF8Encoding]::new($false).GetBytes('[1]'))
                    }
                    foreach($name in $variants.Keys){
                        $null=New-StoreRootTestMarker $fx.Base ([byte[]]$variants[$name])
                        $before=Get-StoreRootTestTree $fx.Base
                        Assert-Code (Invoke-Resolver) 'store-root-unavailable' ('marker '+$name)
                        Assert ((Get-StoreRootTestTree $fx.Base) -ceq $before) ('marker '+$name+' changed the root')
                    }
                    $null=New-StoreRootTestMarker $fx.Base;Assert-Code (Invoke-Resolver) $fx.Root 'valid marker after variants'
                }
                'ac-i5-c'{
                    $fx=New-StoreRootTestBase $work
                    $log=[Collections.Generic.List[string]]::new();$elsewhere=[IO.Path]::Combine($work,'elsewhere','OneStarMaker','store-root.json')
                    Set-StoreRootTestSeams $fx.Base $fx.LegacyBase (New-StoreRootTestMarkerIo $log -Final {param($h)$elsewhere}.GetNewClosure())
                    Assert-Code (Invoke-Resolver) 'store-root-unavailable' 'marker resolving elsewhere'
                    Assert ($log -ccontains 'close') 'handle not closed'
                }
                'ac-i6-a'{
                    $fx=New-StoreRootTestBase $work -WithoutMarker
                    $secrets=@($fx.Base,$fx.LegacyBase,$work,[Security.Principal.WindowsIdentity]::GetCurrent().User.Value,[Environment]::UserName,'Fixture.Package')
                    # Marker absent, then every malformed marker, then a former-root residue.
                    Assert-FixedMessage (Invoke-Resolver) 'absent' $secrets
                    $null=New-StoreRootTestMarker $fx.Base ([byte[]]@());Assert-FixedMessage (Invoke-Resolver) 'empty' $secrets
                    $null=New-StoreRootTestMarker $fx.Base (New-StoreRootTestMarkerBytes @{ownerSid='S-1-5-18'});Assert-FixedMessage (Invoke-Resolver) 'foreign' $secrets
                    $null=New-StoreRootTestMarker $fx.Base;[IO.Directory]::CreateDirectory([IO.Path]::Combine($fx.LegacyRoot,'Artifacts'))|Out-Null
                    $message=Invoke-Resolver;Assert-Code $message 'legacy-store-present' 'residue';Assert-FixedMessage $message 'residue' $secrets
                    [IO.Directory]::Delete([IO.Path]::Combine($fx.LegacyRoot,'Artifacts'))
                    # .NET exceptions carrying the full path are translated to the fixed code.
                    $log=[Collections.Generic.List[string]]::new()
                    foreach($exception in @([IO.IOException]::new('open failed: '+[IO.Path]::Combine($fx.Root,'store-root.json')),[UnauthorizedAccessException]::new('denied: '+$fx.Root),[IO.FileNotFoundException]::new('missing: '+$fx.Root))){
                        $thrown=$exception
                        Set-StoreRootTestSeams $fx.Base $fx.LegacyBase (New-StoreRootTestMarkerIo $log -Open {param($p)throw $thrown}.GetNewClosure())
                        $message=Invoke-Resolver
                        Assert-Code $message $(if($exception -is [IO.FileNotFoundException]){'store-not-migrated'}else{'store-root-unavailable'}) ('injected '+$exception.GetType().Name)
                        Assert-FixedMessage $message ('injected '+$exception.GetType().Name) $secrets
                    }
                    Set-StoreRootTestSeams $fx.Base $fx.LegacyBase (New-StoreRootTestMarkerIo $log -Final {param($h)throw [IO.IOException]::new('final: '+$fx.Root)}.GetNewClosure())
                    Assert-FixedMessage (Invoke-Resolver) 'final path exception' $secrets
                    Set-StoreRootTestSeams $fx.Base $fx.LegacyBase $null {param($p)throw [UnauthorizedAccessException]::new('legacy: '+$p)}
                    $message=Invoke-Resolver;Assert-Code $message 'store-root-unavailable' 'legacy query exception';Assert-FixedMessage $message 'legacy query exception' $secrets
                    # The explicit root check carries no path either.
                    $message=try{Assert-OsmExplicitStoreRoot ([IO.Path]::Combine($fx.LegacyRoot,'Harness'));'accepted'}catch{$_.Exception.Message}
                    Assert-FixedMessage $message 'explicit root' $secrets
                }
            }
            $executed.Add($case)
        }catch{$failed.Add($case+': '+$_.Exception.Message+' '+$_.ScriptStackTrace.Split("`n")[0])}
        finally{Clear-StoreRootTestSeams}
    }
    if($ResultPath){[IO.File]::WriteAllText($ResultPath,(ConvertTo-Json -Depth 8 -InputObject ([ordered]@{registered=$cases;selected=$selected;executed=@($executed);failed=@($failed);observed=$observed})),[Text.UTF8Encoding]::new($false))}
    foreach($name in $executed){"PASS $name"};foreach($name in $failed){"FAIL $name"}
    "Cases: $($selected.Count)/$($cases.Count); passed: $($executed.Count); failed: $($failed.Count)"
    if($failed.Count -or $executed.Count -ne $selected.Count){exit 1}
}finally{
    Clear-StoreRootTestSeams
    if([IO.Directory]::Exists($root)){
        foreach($junction in @(Get-ChildItem -LiteralPath $root -Recurse -Force -Directory|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint})){[IO.Directory]::Delete($junction.FullName,$false)}
        [IO.Directory]::Delete($root,$true)
    }
}
