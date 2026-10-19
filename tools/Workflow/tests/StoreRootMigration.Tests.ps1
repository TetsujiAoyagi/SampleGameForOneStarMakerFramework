param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# The one-time store migration (StoreRootMigration.psm1) on temporary stand-ins
# for the UserProfile and LocalApplicationData Known Folders. Both the migration's
# seams and the resolver's seams point at the same temporary bases, so the
# resolver that accepts the marker is the production code with only its Known
# Folder values replaced. No case touches the real store, the real former root,
# the real Known Folders or Task Scheduler; the entry script is not called
# because its seams cannot be set.
. (Join-Path $PSScriptRoot 'StoreRootTestSupport.ps1')
# Imported before any fixture so that its nested resolver receives the seams too.
$migration=Import-Module ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../StoreRootMigration.psm1'))) -PassThru
$root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-store-migration-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root)|Out-Null
$cases=@('ac-1','ac-1b','ac-2','ac-3','ac-4','ac-5','ac-6','ac-8','ac-9')
$selected=@(if($Case -ceq '*'){$cases}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
$observed=[ordered]@{}
$roleNames=@('Artifacts','Workflow','Harness')
$reasonCodes=@('migrated','already-migrated','store-root-precondition','virtualized-store','store-context-unknown','legacy-redirected','different-volume','role-split','role-unknown','rename-failed','count-mismatch','tombstone-failed','marker-write-failed','marker-not-accepted','migration-failed')
function Assert([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
# A fixture without a marker: the former root holds the given roles with a few
# files each (one hidden, one nested), plus RevisionLocks and a not-owned name
# that the migration must leave alone. Both modules' seams point at it.
function New-MigrationFixture([string]$Work,[string[]]$Roles=$roleNames){
    $fx=New-StoreRootTestBase $Work -WithoutMarker
    foreach($name in @($Roles+@('RevisionLocks','H2a Review'))){
        $directory=[IO.Path]::Combine($fx.LegacyRoot,$name,'nested');[IO.Directory]::CreateDirectory($directory)|Out-Null
        [IO.File]::WriteAllText([IO.Path]::Combine($fx.LegacyRoot,$name,'a.txt'),$name)
        [IO.File]::WriteAllText([IO.Path]::Combine($directory,'b.bin'),'0123456789')
        $hidden=[IO.Path]::Combine($directory,'.hidden');[IO.File]::WriteAllText($hidden,'h');[IO.File]::SetAttributes($hidden,[IO.FileAttributes]::Hidden)
    }
    Set-MigrationSeams $fx
    return $fx
}
function Set-MigrationSeams($Fx,[hashtable]$Seams=@{}){
    & $migration {param($b,$l,$s)
        $script:BaseForTest=$b;$script:LegacyBaseForTest=$l;$script:ResolvedLocalForTest=$null;$script:StoreContextForTest='not-virtualized'
        $script:QueryForTest=$null;$script:RenameForTest=$null;$script:TombstoneForTest=$null;$script:MarkerIoForTest=$null
        foreach($key in $s.Keys){Set-Variable -Scope Script -Name $key -Value $s[$key]}
    } $Fx.Base $Fx.LegacyBase $Seams
}
function Clear-MigrationSeams { & $migration {$script:BaseForTest=$null;$script:LegacyBaseForTest=$null;$script:ResolvedLocalForTest=$null;$script:StoreContextForTest=$null;$script:QueryForTest=$null;$script:RenameForTest=$null;$script:TombstoneForTest=$null;$script:MarkerIoForTest=$null} }
# Runs the migration with every output stream captured, as the entry script
# would print it. Returns the result and the full text for the leak checks.
function Invoke-Migration {
    $output=@(Invoke-OsmStoreRootMigration 2>&1 3>&1 4>&1 5>&1 6>&1)
    $result=@($output|Where-Object {$_ -is [Collections.IDictionary]})
    Assert ($result.Count -eq 1 -and $output.Count -eq 1) ('unexpected output: '+($output|Out-String))
    $script:lastText=ConvertTo-Json -InputObject $result[0] -Depth 5 -Compress
    $script:texts.Add($script:lastText)
    return $result[0]
}
function Assert-Result($Result,[string]$Status,[string]$Code,[string]$Label){
    Assert ($Result.status -ceq $Status -and $Result.reasonCode -ceq $Code) ($Label+': expected '+$Status+'/'+$Code+', got '+$script:lastText)
}
function Get-Role($Result,[string]$Name){ return @($Result.roles|Where-Object {$_.name -ceq $Name})[0] }
function Get-MarkerPath($Fx){ return [IO.Path]::Combine($Fx.Root,'store-root.json') }
function Test-Tombstone([string]$Path){ return [IO.File]::Exists($Path) -and [IO.FileInfo]::new($Path).Length -eq 0 }
function Invoke-Resolver{try{return Get-OsmStoreRoot}catch{return $_.Exception.Message}}
# The store root holds nothing but the moved roles and the marker: no temporary
# marker name is left behind.
function Assert-NoPending($Fx){
    if(-not [IO.Directory]::Exists($Fx.Root)){return}
    $left=@(Get-ChildItem -LiteralPath $Fx.Root -Force -File|Where-Object Name -cne 'store-root.json')
    Assert ($left.Count -eq 0) ('left in store root: '+(@($left|ForEach-Object Name) -join ','))
}
function Assert-Migrated($Fx,[string]$Label){
    foreach($name in $roleNames){Assert (Test-Tombstone ([IO.Path]::Combine($Fx.LegacyRoot,$name))) ($Label+': no tombstone for '+$name)}
    Assert ([IO.File]::Exists((Get-MarkerPath $Fx))) ($Label+': no marker')
    Assert ((Invoke-Resolver) -ceq $Fx.Root) ($Label+': resolver did not accept the marker')
    Assert-NoPending $Fx
}
function Assert-NotMarked($Fx,[string]$Label){
    Assert (-not [IO.File]::Exists((Get-MarkerPath $Fx))) ($Label+': marker written')
    Assert-NoPending $Fx
}
# Snapshot of everything below both bases, for "nothing changed".
function Get-FixtureTree($Fx){ return (Get-StoreRootTestTree $Fx.Base)+"`n--`n"+(Get-StoreRootTestTree $Fx.LegacyBase) }
# Records each call, delegating to the native rename unless $Inject answers.
function New-RenameSeam($Log,[scriptblock]$Inject){
    return {param([string]$Source,[string]$Target)
        $Log.Add([IO.Path]::GetFileName($Source))
        $answer=& $Inject $Source $Target
        if($null -ne $answer){return $answer}
        [OneStarMaker.Workflow.StoreRootMigrationNative]::Move($Source,$Target)}.GetNewClosure()
}
$script:texts=[Collections.Generic.List[string]]::new();$script:lastText=''
try{
    foreach($case in $selected){
        $work=[IO.Path]::Combine($root,$case)
        try{
            Assert ($case -cin $cases) 'unknown case'
            switch($case){
                'ac-1'{
                    # Three roles move by rename, keep their counts, leave tombstones and
                    # a marker the resolver accepts; RevisionLocks and the not-owned name stay.
                    $fx=New-MigrationFixture $work
                    $others=@('RevisionLocks','H2a Review'|ForEach-Object {Get-StoreRootTestTree ([IO.Path]::Combine($fx.LegacyRoot,$_))})
                    $trees=@{};foreach($name in $roleNames){$trees[$name]=(Get-StoreRootTestTree ([IO.Path]::Combine($fx.LegacyRoot,$name))).Replace($fx.LegacyRoot,'<root>')}
                    $result=Invoke-Migration
                    Assert-Result $result 'passed' 'migrated' 'migration'
                    foreach($name in $roleNames){
                        $role=Get-Role $result $name
                        Assert ($role.action -ceq 'moved') ($name+' action '+$role.action)
                        Assert ($role.before.files -eq 3 -and $role.before.directories -eq 1 -and $role.before.bytes -eq ($name.Length+11)) ($name+' counted '+(ConvertTo-Json $role.before -Compress))
                        Assert ($role.after.files -eq $role.before.files -and $role.after.directories -eq $role.before.directories -and $role.after.bytes -eq $role.before.bytes) ($name+' counts differ')
                        # A rename keeps names, attributes, sizes and write times.
                        Assert ((Get-StoreRootTestTree ([IO.Path]::Combine($fx.Root,$name))).Replace($fx.Root,'<root>') -ceq $trees[$name]) ($name+' tree changed')
                    }
                    Assert-Migrated $fx 'ac-1'
                    Assert ((@('RevisionLocks','H2a Review'|ForEach-Object {Get-StoreRootTestTree ([IO.Path]::Combine($fx.LegacyRoot,$_))}) -join "`n") -ceq ($others -join "`n")) 'not-owned names changed'
                    $observed['ac-1']=$script:lastText
                }
                'ac-1b'{
                    # Harness exists on neither side: only its tombstone is placed and the
                    # result names it initiallyAbsent instead of counting it as moved.
                    $fx=New-MigrationFixture $work @('Artifacts','Workflow')
                    $result=Invoke-Migration
                    Assert-Result $result 'passed' 'migrated' 'migration'
                    $harness=Get-Role $result 'Harness'
                    Assert ($harness.action -ceq 'initiallyAbsent' -and $null -eq $harness.before -and $null -eq $harness.after) ('Harness '+$script:lastText)
                    foreach($name in @('Artifacts','Workflow')){Assert ((Get-Role $result $name).action -ceq 'moved') ($name+' not moved')}
                    Assert (([OneStarMaker.Workflow.StoreRootNative]::Query([IO.Path]::Combine($fx.Root,'Harness'))) -ceq 'absent') 'Harness created in the store root'
                    Assert-Migrated $fx 'ac-1b'
                    # A former Harness that is denied or a reparse point stops the run.
                    foreach($status in @('denied','reparse')){
                        $fx=New-MigrationFixture ([IO.Path]::Combine($work,$status)) @('Artifacts','Workflow')
                        $harnessPath=[IO.Path]::Combine($fx.LegacyRoot,'Harness')
                        $query={param($p)if([string]::Equals($p,$harnessPath,[StringComparison]::OrdinalIgnoreCase)){return $status};[OneStarMaker.Workflow.StoreRootNative]::Query($p)}.GetNewClosure()
                        Set-MigrationSeams $fx @{QueryForTest=$query}
                        $before=Get-FixtureTree $fx
                        Assert-Result (Invoke-Migration) 'failed' 'role-unknown' ('Harness '+$status)
                        Assert ((Get-FixtureTree $fx) -ceq $before) ('changed with Harness '+$status)
                        Assert-NotMarked $fx ('Harness '+$status)
                    }
                }
                'ac-2'{
                    foreach($variant in @(@{error=17;code='different-volume'},@{error=5;code='rename-failed'})){
                        $fx=New-MigrationFixture ([IO.Path]::Combine($work,[string]$variant.error))
                        $workflowTree=Get-StoreRootTestTree ([IO.Path]::Combine($fx.LegacyRoot,'Workflow'))
                        $log=[Collections.Generic.List[string]]::new();$errorCode=$variant.error
                        Set-MigrationSeams $fx @{RenameForTest=(New-RenameSeam $log {param($s,$t)if([IO.Path]::GetFileName($s) -ceq 'Workflow'){return $errorCode}}.GetNewClosure())}
                        $result=Invoke-Migration
                        Assert-Result $result 'failed' $variant.code ('error '+$variant.error)
                        # The failing role stays in place, nothing is copied, Harness is not reached.
                        Assert ((Get-StoreRootTestTree ([IO.Path]::Combine($fx.LegacyRoot,'Workflow'))) -ceq $workflowTree) 'Workflow changed'
                        Assert (-not [IO.Directory]::Exists([IO.Path]::Combine($fx.Root,'Workflow'))) 'Workflow copied'
                        Assert ([IO.Directory]::Exists([IO.Path]::Combine($fx.LegacyRoot,'Harness')) -and ($log -join ',') -ceq 'Artifacts,Workflow') ('renamed '+($log -join ','))
                        Assert-NotMarked $fx ('error '+$variant.error)
                        # The same command again skips the moved role and moves the rest.
                        $log.Clear();Set-MigrationSeams $fx @{RenameForTest=(New-RenameSeam $log {param($s,$t)$null})}
                        $result=Invoke-Migration
                        Assert-Result $result 'passed' 'migrated' ('rerun after '+$variant.error)
                        Assert ((Get-Role $result 'Artifacts').action -ceq 'alreadyMoved' -and ($log -join ',') -ceq 'Workflow,Harness') ('rerun renamed '+($log -join ','))
                        Assert-Migrated $fx ('rerun after '+$variant.error)
                    }
                    # Stopped after the rename and before the tombstone: the rerun places
                    # the tombstone first and goes on.
                    $fx=New-MigrationFixture ([IO.Path]::Combine($work,'after-rename'))
                    $log=[Collections.Generic.List[string]]::new()
                    Set-MigrationSeams $fx @{RenameForTest=(New-RenameSeam $log {param($s,$t)if([IO.Path]::GetFileName($s) -ceq 'Workflow'){[void][OneStarMaker.Workflow.StoreRootMigrationNative]::Move($s,$t);throw [IO.IOException]::new('after rename '+$t)}})}
                    Assert-Result (Invoke-Migration) 'failed' 'rename-failed' 'after rename'
                    Assert (-not [IO.File]::Exists([IO.Path]::Combine($fx.LegacyRoot,'Workflow')) -and [IO.Directory]::Exists([IO.Path]::Combine($fx.Root,'Workflow'))) 'state after rename'
                    Assert-NotMarked $fx 'after rename'
                    Set-MigrationSeams $fx
                    $result=Invoke-Migration
                    Assert-Result $result 'passed' 'migrated' 'rerun after rename'
                    $workflow=Get-Role $result 'Workflow'
                    Assert ($workflow.action -ceq 'moved' -and $null -eq $workflow.before -and $workflow.after.files -eq 3) ('Workflow '+$script:lastText)
                    Assert-Migrated $fx 'rerun after rename'
                    # A tombstone that cannot be created stops before the marker.
                    $fx=New-MigrationFixture ([IO.Path]::Combine($work,'tombstone'))
                    Set-MigrationSeams $fx @{TombstoneForTest={param($p)throw [UnauthorizedAccessException]::new('denied '+$p)}}
                    Assert-Result (Invoke-Migration) 'failed' 'tombstone-failed' 'tombstone'
                    Assert ([IO.Directory]::Exists([IO.Path]::Combine($fx.Root,'Artifacts')) -and [IO.Directory]::Exists([IO.Path]::Combine($fx.LegacyRoot,'Workflow'))) 'tombstone failure went on'
                    Assert-NotMarked $fx 'tombstone'
                }
                'ac-3'{
                    # The same role on both sides, including a tombstone replaced by a
                    # directory that old code created again after the move.
                    $fx=New-MigrationFixture $work
                    [IO.Directory]::CreateDirectory([IO.Path]::Combine($fx.Root,'Workflow'))|Out-Null
                    $before=Get-FixtureTree $fx
                    Assert-Result (Invoke-Migration) 'failed' 'role-split' 'split before the move'
                    Assert ((Get-FixtureTree $fx) -ceq $before) 'changed on split'
                    Assert-NotMarked $fx 'split before the move'
                    $fx=New-MigrationFixture ([IO.Path]::Combine($work,'recreated'))
                    Set-MigrationSeams $fx @{MarkerIoForTest=@{Write={param($p,$b)throw 'stop'};Rename={param($s,$t)0}}}
                    Assert-Result (Invoke-Migration) 'failed' 'marker-write-failed' 'moved without marker'
                    $tombstone=[IO.Path]::Combine($fx.LegacyRoot,'Artifacts');[IO.File]::Delete($tombstone);[IO.Directory]::CreateDirectory($tombstone)|Out-Null
                    Set-MigrationSeams $fx
                    $before=Get-FixtureTree $fx
                    Assert-Result (Invoke-Migration) 'failed' 'role-split' 'recreated by old code'
                    Assert ((Get-FixtureTree $fx) -ceq $before) 'changed on recreated split'
                    Assert-NotMarked $fx 'recreated by old code'
                }
                'ac-4'{
                    $fx=New-MigrationFixture $work
                    $native=& $migration {$script:NativeMarkerIo}
                    # The temporary name is written (partly), then the write fails.
                    Set-MigrationSeams $fx @{MarkerIoForTest=@{Write={param($p,$b)[IO.File]::WriteAllBytes($p,$b[0..3]);throw [IO.IOException]::new('write '+$p)};Rename=$native.Rename}}
                    Assert-Result (Invoke-Migration) 'failed' 'marker-write-failed' 'write failure'
                    Assert-NotMarked $fx 'write failure'
                    # The rename fails, by error code and by exception.
                    foreach($rename in @({param($s,$t)183},{param($s,$t)throw [IO.IOException]::new('rename '+$t)})){
                        Set-MigrationSeams $fx @{MarkerIoForTest=@{Write=$native.Write;Rename=$rename}}
                        Assert-Result (Invoke-Migration) 'failed' 'marker-write-failed' 'rename failure'
                        Assert-NotMarked $fx 'rename failure'
                    }
                    # Every role is a tombstone already, so the rerun only writes the marker.
                    Set-MigrationSeams $fx
                    $result=Invoke-Migration
                    Assert-Result $result 'passed' 'migrated' 'rerun'
                    Assert (@($result.roles|Where-Object {$_.action -cne 'alreadyMoved'}).Count -eq 0) ('rerun '+$script:lastText)
                    Assert-Migrated $fx 'rerun'
                }
                'ac-5'{
                    foreach($variant in @(@{seams=@{StoreContextForTest='virtualized'};code='virtualized-store'},@{seams=@{StoreContextForTest='unknown'};code='store-context-unknown'},@{seams=@{ResolvedLocalForTest='C:\Redirected\AppData\Local'};code='legacy-redirected'})){
                        $fx=New-MigrationFixture ([IO.Path]::Combine($work,$variant.code))
                        Set-MigrationSeams $fx $variant.seams
                        $before=Get-FixtureTree $fx
                        Assert-Result (Invoke-Migration) 'failed' $variant.code $variant.code
                        Assert ((Get-FixtureTree $fx) -ceq $before -and -not [IO.Directory]::Exists($fx.Root)) ('changed on '+$variant.code)
                    }
                }
                'ac-6'{
                    # Every result, on success and on each failure path, holds only fixed
                    # codes, role names and counts: no temp path, no SID, no user name,
                    # even when an injected .NET exception carries the absolute path.
                    $script:texts.Clear()
                    $sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
                    $fx=New-MigrationFixture ([IO.Path]::Combine($work,'success'));$null=Invoke-Migration;$null=Invoke-Migration
                    foreach($seams in @(
                        @{RenameForTest={param($s,$t)throw [IO.IOException]::new('cannot move '+$s+' to '+$t)}},
                        @{TombstoneForTest={param($p)throw [UnauthorizedAccessException]::new('denied '+$p)}},
                        @{QueryForTest={param($p)throw [IO.IOException]::new('query '+$p)}},
                        @{MarkerIoForTest=@{Write={param($p,$b)throw [IO.IOException]::new('write '+$p)};Rename={param($s,$t)0}}},
                        @{StoreContextForTest='virtualized'}
                    )){
                        $fx=New-MigrationFixture ([IO.Path]::Combine($work,[Guid]::NewGuid().ToString('N')))
                        Set-MigrationSeams $fx $seams
                        $result=Invoke-Migration
                        Assert ($result.status -ceq 'failed') ('passed with '+(@($seams.Keys) -join ','))
                    }
                    $fx=New-MigrationFixture ([IO.Path]::Combine($work,'split'));[IO.Directory]::CreateDirectory([IO.Path]::Combine($fx.Root,'Harness'))|Out-Null;$null=Invoke-Migration
                    $secrets=@($root,$work,[IO.Path]::GetTempPath().TrimEnd('\'),$sid,[Environment]::UserName)
                    foreach($text in $script:texts){
                        foreach($secret in $secrets){Assert (-not $text.Contains($secret,[StringComparison]::OrdinalIgnoreCase)) ('leaked into '+$text)}
                        $document=ConvertFrom-Json $text
                        Assert ([string]$document.reasonCode -cin $reasonCodes) ('not a fixed code: '+$text)
                        foreach($role in @($document.roles)){Assert ([string]$role.name -cin $roleNames) ('unexpected role name: '+$text)}
                    }
                    $observed['ac-6-results']=$script:texts.Count
                }
                'ac-8'{
                    # (1) A valid marker the resolver accepts: nothing changes.
                    $fx=New-MigrationFixture $work
                    Assert-Result (Invoke-Migration) 'passed' 'migrated' 'first run'
                    $before=Get-FixtureTree $fx;$bytes=[IO.File]::ReadAllBytes((Get-MarkerPath $fx))
                    $result=Invoke-Migration
                    Assert-Result $result 'passed' 'already-migrated' 'second run'
                    Assert ((Get-FixtureTree $fx) -ceq $before -and [Linq.Enumerable]::SequenceEqual([byte[]][IO.File]::ReadAllBytes((Get-MarkerPath $fx)),[byte[]]$bytes)) 'second run changed something'
                    # (2) A broken or foreign marker, with or without roles still in the
                    # former root, and a valid marker with a role still there: precondition.
                    $variants=[ordered]@{
                        'empty'=[byte[]]@()
                        'missing-field'=(New-StoreRootTestMarkerBytes @{createdAt=$null})
                        'foreign-owner'=(New-StoreRootTestMarkerBytes @{ownerSid='S-1-5-18'})
                        'legacy-role'=(New-StoreRootTestMarkerBytes)
                    }
                    foreach($name in $variants.Keys){
                        $fx=New-MigrationFixture ([IO.Path]::Combine($work,$name))
                        $null=New-StoreRootTestMarker $fx.Base $variants[$name]
                        $before=Get-FixtureTree $fx;$bytes=[IO.File]::ReadAllBytes((Get-MarkerPath $fx))
                        Assert-Result (Invoke-Migration) 'failed' 'store-root-precondition' $name
                        Assert ((Get-FixtureTree $fx) -ceq $before -and [Linq.Enumerable]::SequenceEqual([byte[]][IO.File]::ReadAllBytes((Get-MarkerPath $fx)),[byte[]]$bytes)) ($name+' changed')
                    }
                }
                'ac-9'{
                    # Only a 0-byte regular file is a tombstone; a regular file in the
                    # store root under a role name is not a role either.
                    $fx=New-MigrationFixture $work @('Workflow','Harness')
                    [IO.File]::WriteAllText([IO.Path]::Combine($fx.LegacyRoot,'Artifacts'),'x')
                    $before=Get-FixtureTree $fx
                    Assert-Result (Invoke-Migration) 'failed' 'role-unknown' 'non-empty file'
                    Assert ((Get-FixtureTree $fx) -ceq $before) 'changed on non-empty file'
                    Assert-NotMarked $fx 'non-empty file'
                    $fx=New-MigrationFixture ([IO.Path]::Combine($work,'store-file')) @('Workflow','Harness')
                    [IO.File]::WriteAllBytes([IO.Path]::Combine($fx.LegacyRoot,'Artifacts'),[byte[]]@())
                    [IO.Directory]::CreateDirectory($fx.Root)|Out-Null;[IO.File]::WriteAllText([IO.Path]::Combine($fx.Root,'Artifacts'),'')
                    $before=Get-FixtureTree $fx
                    Assert-Result (Invoke-Migration) 'failed' 'role-unknown' 'file in the store root'
                    Assert ((Get-FixtureTree $fx) -ceq $before) 'changed on file in the store root'
                    Assert (-not [IO.File]::Exists((Get-MarkerPath $fx))) 'marker written'
                }
            }
            $executed.Add($case)
        }catch{$failed.Add($case+': '+$_.Exception.Message+' '+$_.ScriptStackTrace.Split("`n")[0])}
        finally{Clear-MigrationSeams;Clear-StoreRootTestSeams}
    }
    if($ResultPath){[IO.File]::WriteAllText($ResultPath,(ConvertTo-Json -Depth 8 -InputObject ([ordered]@{registered=$cases;selected=$selected;executed=@($executed);failed=@($failed);observed=$observed})),[Text.UTF8Encoding]::new($false))}
    foreach($name in $executed){"PASS $name"};foreach($name in $failed){"FAIL $name"}
    "Cases: $($selected.Count)/$($cases.Count); passed: $($executed.Count); failed: $($failed.Count)"
    if($failed.Count -or $executed.Count -ne $selected.Count){exit 1}
}finally{
    Clear-MigrationSeams;Clear-StoreRootTestSeams
    if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)}
}
