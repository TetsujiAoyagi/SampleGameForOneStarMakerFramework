param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# Store-context decision and the three interactive entries. Every OS operation of
# the probe is replaced through the module's private seam, except in the native
# cases. Those run on a temporary root standing in for LocalApplicationData, apart
# from native-probe-deletes-on-close, which creates and removes one uniquely named
# probe directory (with one DeleteOnClose file) under the real LocalApplicationData.
$context=Import-Module (Join-Path $PSScriptRoot '../WindowsStoreContext.psm1') -Force -PassThru
$tools=[IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..','..'))
$local=[Environment]::GetFolderPath('LocalApplicationData')
$fixedArea=[IO.Path]::Combine($local,'OneStarMaker.StoreProbe')
$storeRoot=[IO.Path]::Combine($local,'OneStarMaker')
# The only directory name a call may create: the prefix and a 32-digit hex GUID.
$probeName='\AOneStarMaker\.StoreProbe\.[0-9a-f]{32}\z'
$root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-store-context-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root)|Out-Null
$cases=@('decision-not-virtualized','decision-virtualized','decision-unknown-open','decision-unknown-final-path','decision-unknown-close','decision-unknown-empty-final-path','probe-outside-store','native-probe-deletes-on-close','no-package-identity-input','cli-virtualized-all-entries','cli-unknown-failure-stages','cli-decision-before-store','import-has-no-store-side-effects',
    'decision-fresh-directory-each-call','decision-ignores-leftover-areas','decision-directory-exists-is-unknown','decision-directory-create-failure-is-unknown','decision-directory-removal-failure-keeps-result','decision-concurrent-calls-do-not-remove-each-other','decision-probe-parents','native-create-existing-directory-fails','native-open-does-not-create-parent')
$selected=@(if($Case -ceq '*'){$cases}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
$observed=[ordered]@{}
function Assert([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
function Test-Within([string]$Path,[string]$Parent){$p=[IO.Path]::GetFullPath($Path).TrimEnd('\');$r=[IO.Path]::GetFullPath($Parent).TrimEnd('\');$p.Equals($r,[StringComparison]::OrdinalIgnoreCase) -or $p.StartsWith($r+'\',[StringComparison]::OrdinalIgnoreCase)}
function Test-SamePath([string]$A,[string]$B){return [string]::Equals($A.TrimEnd('\'),$B.TrimEnd('\'),[StringComparison]::OrdinalIgnoreCase)}
# A fake OS seam. $Final maps the opened logical path to the reported final path.
# $Fail names the one operation that throws.
function New-FakeIo([string]$Fail='',[scriptblock]$Final={param($p)$p}){
    $log=[Collections.Generic.List[string]]::new()
    $io=@{
        CreateDirectory={param($Path)$log.Add('directory:'+$Path);if($Fail -ceq 'directory'){throw 'fake-directory'}}.GetNewClosure()
        Open={param($Path)$log.Add('open:'+$Path);if($Fail -ceq 'open'){throw 'fake-open'};[pscustomobject]@{Path=$Path}}.GetNewClosure()
        FinalPath={param($Handle)$log.Add('final-path');if($Fail -ceq 'final-path'){throw 'fake-final-path'};& $Final $Handle.Path}.GetNewClosure()
        Close={param($Handle)$log.Add('close');if($Fail -ceq 'close'){throw 'fake-close'}}.GetNewClosure()
        RemoveDirectory={param($Path)$log.Add('remove:'+$Path);if($Fail -ceq 'remove'){throw 'fake-remove'}}.GetNewClosure()
    }
    return [pscustomobject]@{Io=$io;Log=$log}
}
# The real OS operations, logging every path they are given. $BeforeCreate runs
# just before the native directory creation (to stage a name collision).
function New-NativeTrace([scriptblock]$BeforeCreate={param($p)}){
    $native=& $context {$script:NativeIo};$log=[Collections.Generic.List[string]]::new()
    $io=@{
        CreateDirectory={param($Path)$log.Add('directory:'+$Path);& $BeforeCreate $Path;& $native.CreateDirectory $Path}.GetNewClosure()
        Open={param($Path)$log.Add('open:'+$Path);& $native.Open $Path}.GetNewClosure()
        FinalPath=$native.FinalPath;Close=$native.Close
        RemoveDirectory={param($Path)$log.Add('remove:'+$Path);& $native.RemoveDirectory $Path}.GetNewClosure()
    }
    return [pscustomobject]@{Io=$io;Log=$log}
}
function Invoke-Decision($Fake,[string]$LocalRoot=''){
    & $context {param($v,$r)$script:ProbeIo=$v;$script:LocalRootForTest=$(if($r){$r}else{$null})} $Fake.Io $LocalRoot
    try{return Get-OsmStoreContext}finally{& $context {$script:ProbeIo=$null;$script:LocalRootForTest=$null}}
}
function Get-Logged($Log,[string]$Prefix){return @($Log|Where-Object {$_.StartsWith($Prefix)}|ForEach-Object {$_.Substring($Prefix.Length)})}
# One call: exactly one new directory with the probe name directly under the root,
# the probe file directly inside it, and nothing else removed. Returns the directory.
function Assert-OwnDirectoryOnly($Log,[string]$LocalRoot=$local,[string]$Prefix=''){
    $directories=@(Get-Logged $Log ($Prefix+'directory:'))
    Assert ($directories.Count -eq 1) ('directories created: '+$directories.Count)
    $directory=$directories[0]
    Assert (Test-SamePath ([IO.Path]::GetDirectoryName($directory)) $LocalRoot) 'probe directory is not directly under LocalApplicationData'
    Assert ([IO.Path]::GetFileName($directory) -cmatch $probeName) ('probe directory name '+[IO.Path]::GetFileName($directory))
    foreach($open in (Get-Logged $Log ($Prefix+'open:'))){Assert (Test-SamePath ([IO.Path]::GetDirectoryName($open)) $directory) 'probe file outside the own directory'}
    foreach($removed in (Get-Logged $Log ($Prefix+'remove:'))){Assert (Test-SamePath $removed $directory) 'removed a directory the call did not create'}
    return $directory
}
# Names and attributes of every entry below a root, plus size and write time of
# files; equal before and after means nothing below it was created, removed or
# written. Directory write times are left out: NTFS may report a directory's
# time from before its latest child was added, which is not a change.
function Get-Tree([string]$Path){return (@(Get-ChildItem -LiteralPath $Path -Recurse -Force|Sort-Object FullName|ForEach-Object {$_.FullName+'|'+$_.Attributes+$(if($_ -is [IO.FileInfo]){'|'+$_.LastWriteTimeUtc.Ticks+'|'+$_.Length}else{''})}) -join "`n")}
# A temporary stand-in for LocalApplicationData, spelled as the OS reports it so
# that a non-virtualized probe's final path equals its logical path.
function New-NativeRoot([string]$Name){
    $path=[IO.Path]::Combine($root,$Name);[IO.Directory]::CreateDirectory($path)|Out-Null
    $stream=[IO.FileStream]::new([IO.Path]::Combine($path,'spelling'),[IO.FileMode]::CreateNew,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None,1,[IO.FileOptions]::DeleteOnClose)
    try{return [IO.Path]::GetDirectoryName([OneStarMaker.Workflow.StoreContextProbe]::FinalPath($stream))}finally{$stream.Dispose()}
}
function Get-LocalCachePath([string]$Path){return [IO.Path]::Combine($local,'Packages','Fixture.Package_0000000000000','LocalCache','Local',$Path.Substring($local.Length+1))}
function Start-Child([string[]]$Arguments){
    $info=[Diagnostics.ProcessStartInfo]::new([Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)
    $info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($argument in @('-NoProfile','-File')+$Arguments){$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info)
    return [pscustomobject]@{Process=$process;Out=$process.StandardOutput.ReadToEndAsync();Err=$process.StandardError.ReadToEndAsync()}
}
function Wait-Child($Child,[int]$Seconds=30){
    $process=$Child.Process
    try{
        Assert ($process.WaitForExit($Seconds*1000)) 'child deadline'
        Assert ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($Child.Out,$Child.Err),3000)) 'child pipes'
        return [pscustomobject]@{Exit=$process.ExitCode;Out=$Child.Out.Result;Err=$Child.Err.Result}
    }finally{if(-not $process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function Invoke-Child([string[]]$Arguments,[int]$Seconds=30){return Wait-Child (Start-Child $Arguments) $Seconds}
# Runs one public entry in a fresh process through StoreContextCliFixture.ps1.
function Invoke-Entry([string]$Entry,[string[]]$EntryArguments,[string]$Mode){
    $calls=[IO.Path]::Combine($root,[Guid]::NewGuid().ToString('N')+'.calls')
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject @($EntryArguments) -Compress)))
    $result=Invoke-Child @((Join-Path $PSScriptRoot 'StoreContextCliFixture.ps1'),'-Entry',([IO.Path]::Combine($tools,$Entry)),'-CallsPath',$calls,'-Mode',$Mode,'-EntryArgumentsBase64',$encoded)
    $lines=if([IO.File]::Exists($calls)){@([IO.File]::ReadAllLines($calls))}else{@()}
    $json=@($result.Out -split "`r?`n"|Where-Object {$_.StartsWith('{')})
    $last=if($json.Count){$json[-1]|ConvertFrom-Json}else{$null}
    return [pscustomobject]@{Exit=$result.Exit;Out=$result.Out;Err=$result.Err;Calls=$lines;Json=$last;Label=$Entry+' '+($EntryArguments -join ' ')+' ['+$Mode+']'}
}
function Assert-StoppedBeforeStore($Run,[string]$ReasonCode){
    Assert ($Run.Exit -eq 1) ($Run.Label+' exit '+$Run.Exit+' '+$Run.Out+$Run.Err)
    Assert ($null -ne $Run.Json -and $Run.Json.status -ceq 'failed' -and $Run.Json.reasonCode -ceq $ReasonCode) ($Run.Label+' output '+$Run.Out)
    Assert (@($Run.Calls|Where-Object {$_.StartsWith('store:')}).Count -eq 0) ($Run.Label+' reached store: '+($Run.Calls -join ','))
    Assert ($Run.Calls.Count -ge 2 -and $Run.Calls[0] -ceq 'import:WindowsStoreContext.psm1' -and $Run.Calls[1].StartsWith('probe:directory:')) ($Run.Label+' did not decide first')
    $directory=Assert-OwnDirectoryOnly $Run.Calls -Prefix 'probe:'
    Assert (-not (Test-Within $directory $storeRoot)) ($Run.Label+' probe inside the store')
    # Neither the real path, the package name nor the user name may surface.
    foreach($marker in @($local,'Fixture.Package',[Environment]::UserName)){Assert (-not $Run.Out.Contains($marker) -and -not $Run.Err.Contains($marker)) ($Run.Label+' leaked a path')}
}
$artifactEntries=@(
    @('fetch','--profile','osm','--config','c','--reference','r','--sha256','s'),
    @('evidence','publish','--profile','osm','--config','c','--selection','s'),
    @('evidence','inspect','--profile','osm','--config','c','--task','t'),
    @('evidence','use','--profile','osm','--config','c','--reference','r','--consumer','x','--until','u','--reason','y'),
    @('evidence','release','--profile','osm','--config','c','--reference','r','--protection','p'),
    @('cleanup','--profile','osm','--config','c'),
    @('schedule','install','--profile','osm','--config','c'),
    @('schedule','status','--profile','osm','--config','c'),
    @('reset','--profile','osm','--manifest','m','--manifest-sha256','h','--owner-verification','o','--owner-verification-sha256','h'),
    @('build','publish','--profile','osm','--config','c','--selection','s'),
    @('build','fetch','--profile','osm','--config','c','--reference','r','--sha256','s'),
    @('build','inspect','--profile','osm','--config','c','--build-id','b'),
    @('build','collect','--profile','osm','--config','c','--source','s'),
    @('build','cleanup','--profile','osm','--config','c','--project','p','--target','t','--configuration','x'),
    @('build','use','--profile','osm','--config','c','--build-id','b','--consumer','x','--until','u','--reason','y'),
    @('build','release','--profile','osm','--config','c','--build-id','b','--protection','p'),
    @('content','fetch','--profile','osm','--config','c','--reference','r','--sha256','s'),
    @('content','inspect','--profile','osm','--config','c','--content-set','s','--target','t','--revision','r'),
    @('content','collect','--profile','osm','--config','c','--source','s'),
    @('content','cleanup','--profile','osm','--config','c','--content-set','s','--target','t'),
    @('credentials','set','--profile','osm'),
    @('credentials','status','--profile','osm'),
    @('credentials','remove','--profile','osm'),
    @('credentials','rotate','--profile','osm','--config','c'),
    @('credentials','confirm-revocation','--profile','osm','--config','c','--generation','g','--evidence','e'),
    @('store','plan','--source-root','s','--output','o'),
    @('store','consolidate','--manifest','m','--manifest-sha256','h'),
    @('not-a-command')
)
$workflowEntries=@(@('start','-Task','fixture-task'),@('status','-Task','fixture-task'),@('end','-Task','fixture-task','-Reason','completed','-ExpectedVersion','1','-Decision','d'),@('resume','-Task','fixture-task','-ExpectedVersion','2','-Decision','d'))
$harnessEntries=@(@('init','-Task','fixture-task'),@('status'),@('current','-Task','fixture-task'),@('run','-Task','fixture-task'),@('adopt','-Task','fixture-task'),@('handoff','-Task','fixture-task'),@('assist','-Task','fixture-task'),@('reference','-Task','fixture-task'),@('close','-Task','fixture-task'),@('restore','-Task','fixture-task'))
try{
    foreach($case in $selected){
        try{
            Assert ($case -cin $cases) 'unknown case'
            switch($case){
                'decision-not-virtualized'{
                    $f=New-FakeIo;Assert ((Invoke-Decision $f) -ceq 'not-virtualized') 'not virtualized'
                    Assert ($f.Log.Count -eq 5 -and $f.Log[0].StartsWith('directory:') -and $f.Log[1].StartsWith('open:') -and $f.Log[2] -ceq 'final-path' -and $f.Log[3] -ceq 'close' -and $f.Log[4].StartsWith('remove:')) ('seam order '+($f.Log -join ','))
                    $null=Assert-OwnDirectoryOnly $f.Log
                }
                'decision-virtualized'{$f=New-FakeIo -Final {param($p)Get-LocalCachePath $p};Assert ((Invoke-Decision $f) -ceq 'virtualized') 'virtualized';Assert ($f.Log[-2] -ceq 'close' -and $f.Log[-1].StartsWith('remove:')) 'handle not closed before the directory removal'}
                'decision-unknown-open'{$f=New-FakeIo 'open';Assert ((Invoke-Decision $f) -ceq 'unknown') 'open failure';Assert ($f.Log.Count -eq 3 -and $f.Log[2].StartsWith('remove:')) ('continued after open failure '+($f.Log -join ','));$null=Assert-OwnDirectoryOnly $f.Log}
                'decision-unknown-final-path'{$f=New-FakeIo 'final-path';Assert ((Invoke-Decision $f) -ceq 'unknown') 'final-path failure';Assert ($f.Log[-2] -ceq 'close' -and $f.Log[-1].StartsWith('remove:')) 'handle not closed after query failure'}
                # A close failure is undecidable even when the final path matched.
                'decision-unknown-close'{$f=New-FakeIo 'close';Assert ((Invoke-Decision $f) -ceq 'unknown') 'close failure';$null=Assert-OwnDirectoryOnly $f.Log}
                'decision-unknown-empty-final-path'{$f=New-FakeIo -Final {param($p)''};Assert ((Invoke-Decision $f) -ceq 'unknown') 'empty final path'}
                'probe-outside-store'{
                    $first=New-FakeIo;$null=Invoke-Decision $first;$second=New-FakeIo;$null=Invoke-Decision $second
                    $a=@(Get-Logged $first.Log 'open:')[0];$b=@(Get-Logged $second.Log 'open:')[0]
                    Assert (-not (Test-Within $a $storeRoot) -and -not (Test-Within $a $fixedArea)) 'probe inside the store or the former fixed area'
                    Assert ($a -cne $b -and [IO.Path]::GetFileName($a) -cmatch '\A[0-9a-f]{32}\.probe\z') 'probe name not unique'
                }
                'native-probe-deletes-on-close'{
                    # The real OS operations under the real LocalApplicationData: one new
                    # directory, one DeleteOnClose file, and neither is left behind.
                    $fixedBefore=[IO.Directory]::Exists($fixedArea)
                    $trace=New-NativeTrace;$value=Invoke-Decision $trace
                    $observed.nativeDecision=$value
                    Assert ($value -ceq 'not-virtualized') ('this suite requires a non-virtualized host; got '+$value)
                    $directory=Assert-OwnDirectoryOnly $trace.Log
                    $opened=@(Get-Logged $trace.Log 'open:')
                    Assert ($opened.Count -eq 1 -and -not [IO.File]::Exists($opened[0])) 'probe file left behind'
                    Assert (@(Get-Logged $trace.Log 'remove:').Count -eq 1 -and -not [IO.Directory]::Exists($directory)) 'probe directory left behind'
                    Assert ([IO.Directory]::Exists($fixedArea) -eq $fixedBefore) 'the former fixed probe area changed'
                }
                'no-package-identity-input'{
                    # The decision has no identity input; record which kind of host ran the suite.
                    if(-not ('OneStarMaker.Tests.PackageIdentity' -as [type])){Add-Type -TypeDefinition 'using System;using System.Runtime.InteropServices;using System.Text;namespace OneStarMaker.Tests{public static class PackageIdentity{[DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern int GetCurrentPackageFamilyName(ref uint n,StringBuilder b);public static bool Present(){uint n=0;return GetCurrentPackageFamilyName(ref n,null)!=15700;}}}'}
                    $observed.hostHasPackageIdentity=[OneStarMaker.Tests.PackageIdentity]::Present()
                    $sources=@([IO.File]::ReadAllText((Join-Path $PSScriptRoot '../WindowsStoreContext.psm1')))
                    foreach($entry in @('artifacts.ps1','workflow-task.ps1','harness.ps1')){$sources+=[IO.File]::ReadAllText([IO.Path]::Combine($tools,$entry))}
                    foreach($text in $sources){foreach($marker in @('GetCurrentPackage','PackageFamilyName','PackageFullName','ApplicationModel','APPMODEL')){Assert (-not $text.Contains($marker)) ('identity input '+$marker)}}
                    Assert ((Invoke-Decision (New-FakeIo)) -ceq 'not-virtualized') 'decision changed'
                }
                'cli-virtualized-all-entries'{
                    $count=0
                    foreach($arguments in $artifactEntries){Assert-StoppedBeforeStore (Invoke-Entry 'artifacts.ps1' $arguments 'virtualized') 'virtualized-store';$count++}
                    foreach($arguments in $workflowEntries){Assert-StoppedBeforeStore (Invoke-Entry 'workflow-task.ps1' $arguments 'virtualized') 'virtualized-store';$count++}
                    foreach($arguments in $harnessEntries){Assert-StoppedBeforeStore (Invoke-Entry 'harness.ps1' $arguments 'virtualized') 'virtualized-store';$count++}
                    $observed.virtualizedEntries=$count
                }
                'cli-unknown-failure-stages'{
                    foreach($mode in @('directory-fails','open-fails','final-path-fails','close-fails')){
                        Assert-StoppedBeforeStore (Invoke-Entry 'artifacts.ps1' $artifactEntries[0] $mode) 'store-context-unknown'
                        Assert-StoppedBeforeStore (Invoke-Entry 'workflow-task.ps1' $workflowEntries[1] $mode) 'store-context-unknown'
                        Assert-StoppedBeforeStore (Invoke-Entry 'harness.ps1' $harnessEntries[2] $mode) 'store-context-unknown'
                    }
                }
                'cli-decision-before-store'{
                    # Non-virtualized entries continue; the first store boundary follows the closed probe and the removal of its directory.
                    foreach($run in @((Invoke-Entry 'artifacts.ps1' $artifactEntries[0] 'not-virtualized'),(Invoke-Entry 'workflow-task.ps1' $workflowEntries[1] 'not-virtualized'),(Invoke-Entry 'harness.ps1' $harnessEntries[2] 'not-virtualized'))){
                        $close=[Array]::IndexOf([string[]]$run.Calls,'probe:close');$store=@(for($i=0;$i -lt $run.Calls.Count;$i++){if($run.Calls[$i].StartsWith('store:')){$i}})
                        $removed=@(for($i=0;$i -lt $run.Calls.Count;$i++){if($run.Calls[$i].StartsWith('probe:remove:')){$i}})
                        Assert ($close -ge 0 -and $removed.Count -eq 1 -and $removed[0] -gt $close -and $store.Count -ge 1 -and $store[0] -gt $removed[0]) ($run.Label+' order '+($run.Calls -join ','))
                        Assert ($null -eq $run.Json -or ($run.Json.reasonCode -cne 'virtualized-store' -and $run.Json.reasonCode -cne 'store-context-unknown')) ($run.Label+' rejected')
                    }
                }
                'import-has-no-store-side-effects'{
                    # Static: collect every module the three entries can import, transitively,
                    # and allow only declarations at module top level.
                    $queue=[Collections.Generic.Queue[string]]::new();$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
                    foreach($entry in @('artifacts.ps1','workflow-task.ps1','harness.ps1')){$queue.Enqueue([IO.Path]::Combine($tools,$entry))}
                    $modules=[Collections.Generic.List[string]]::new()
                    while($queue.Count){
                        $file=$queue.Dequeue();if(-not $seen.Add($file)){continue}
                        $ast=[Management.Automation.Language.Parser]::ParseFile($file,[ref]$null,[ref]$null)
                        foreach($command in $ast.FindAll({param($n)$n -is [Management.Automation.Language.CommandAst] -and $n.GetCommandName() -ceq 'Import-Module'},$true)){
                            $text=$command.CommandElements[1].Extent.Text
                            if($text -cmatch "'([^']+\.psm1)'"){$queue.Enqueue([IO.Path]::GetFullPath([IO.Path]::Combine([IO.Path]::GetDirectoryName($file),$Matches[1])))}
                        }
                        if($file.EndsWith('.psm1')){
                            $modules.Add($file)
                            foreach($statement in $ast.EndBlock.Statements){
                                if($statement -is [Management.Automation.Language.FunctionDefinitionAst]){continue}
                                # Script blocks assigned here (hooks, clocks, native seams) do not run at import.
                                $eager={param($n)for($p=$n.Parent;$null -ne $p -and $p -ne $statement;$p=$p.Parent){if($p -is [Management.Automation.Language.ScriptBlockExpressionAst]){return $false}};return $true}.GetNewClosure()
                                foreach($command in @($statement.FindAll({param($n)$n -is [Management.Automation.Language.CommandAst]},$true)|Where-Object {& $eager $_})){Assert ($command.GetCommandName() -cin @('Set-StrictMode','Import-Module','Export-ModuleMember','Join-Path','Add-Type')) ([IO.Path]::GetFileName($file)+' top-level '+$command.GetCommandName())}
                                foreach($invoke in @($statement.FindAll({param($n)$n -is [Management.Automation.Language.InvokeMemberExpressionAst]},$true)|Where-Object {& $eager $_})){Assert ($invoke.Static -and $invoke.Expression.Extent.Text -cin @('[TimeSpan]')) ([IO.Path]::GetFileName($file)+' top-level call '+$invoke.Extent.Text)}
                            }
                        }
                    }
                    Assert ($modules.Count -ge 30) 'module closure unexpectedly small'
                    $observed.importedModules=$modules.Count
                    # Dynamic: import that closure in a fresh process with the decision seam
                    # reporting virtualization; importing alone must not even run the probe.
                    $script=[IO.Path]::Combine($root,'import-only.ps1');$calls=[IO.Path]::Combine($root,'import-only.calls')
                    $body="Set-StrictMode -Version Latest;`$ErrorActionPreference='Stop';`$WarningPreference='SilentlyContinue'`n"+
                        "`$c=Import-Module '"+(Join-Path $PSScriptRoot '../WindowsStoreContext.psm1').Replace("'","''")+"' -PassThru`n"+
                        "& `$c {param(`$p)`$script:ProbeIo=@{CreateDirectory={param(`$x)[IO.File]::AppendAllText(`$p,'probe'+[Environment]::NewLine);throw 'x'};Open={throw 'x'};FinalPath={throw 'x'};Close={};RemoveDirectory={throw 'x'}}} '"+$calls.Replace("'","''")+"'`n"
                    foreach($module in $modules){$body+="Import-Module '"+$module.Replace("'","''")+"'`n"}
                    $body+="'imported'"
                    [IO.File]::WriteAllText($script,$body,[Text.UTF8Encoding]::new($false))
                    $run=Invoke-Child @($script) 120
                    Assert ($run.Exit -eq 0 -and $run.Out.Contains('imported')) ('import failed '+$run.Err)
                    Assert (-not [IO.File]::Exists($calls)) 'import ran the probe'
                }
                'decision-fresh-directory-each-call'{
                    $first=New-FakeIo;$second=New-FakeIo
                    Assert ((Invoke-Decision $first) -ceq 'not-virtualized' -and (Invoke-Decision $second) -ceq 'not-virtualized') 'decision'
                    $a=Assert-OwnDirectoryOnly $first.Log;$b=Assert-OwnDirectoryOnly $second.Log
                    Assert (-not (Test-SamePath $a $b)) 'two calls shared a directory'
                    foreach($fake in @($first,$second)){Assert (@(Get-Logged $fake.Log 'remove:').Count -eq 1) 'own directory not removed'}
                }
                'decision-ignores-leftover-areas'{
                    # The r4 failure: inside a virtualized process a new file in a directory
                    # that exists only on the physical side lands there. Model that rule; a
                    # call that probed in any existing directory would read 'not-virtualized'.
                    $physical=@($fixedArea,[IO.Path]::Combine($local,'OneStarMaker.StoreProbe.'+[Guid]::NewGuid().ToString('N')),$storeRoot)
                    $fake=New-FakeIo -Final {param($p)if($physical -contains [IO.Path]::GetDirectoryName($p)){$p}else{Get-LocalCachePath $p}}
                    Assert ((Invoke-Decision $fake) -ceq 'virtualized') 'an existing directory decided the result'
                    $null=Assert-OwnDirectoryOnly $fake.Log
                    # Native operations on a stand-in root that already holds the former fixed
                    # area, leftovers of other calls (empty and not) and the store.
                    $base=New-NativeRoot 'leftovers'
                    $existing=@('OneStarMaker.StoreProbe',('OneStarMaker.StoreProbe.'+[Guid]::NewGuid().ToString('N')),('OneStarMaker.StoreProbe.'+[Guid]::NewGuid().ToString('N')),'OneStarMaker')|ForEach-Object {[IO.Path]::Combine($base,$_)}
                    foreach($directory in $existing){[IO.Directory]::CreateDirectory($directory)|Out-Null}
                    foreach($directory in @($existing[0],$existing[2],$existing[3])){[IO.File]::WriteAllText([IO.Path]::Combine($directory,'sentinel'),'x')}
                    $before=Get-Tree $base
                    foreach($round in 1,2){
                        $trace=New-NativeTrace
                        Assert ((Invoke-Decision $trace $base) -ceq 'not-virtualized') ('native decision round '+$round)
                        $own=Assert-OwnDirectoryOnly $trace.Log $base
                        foreach($line in $trace.Log){$path=$line.Substring($line.IndexOf(':')+1);foreach($directory in $existing){Assert (-not (Test-Within $path $directory)) ('touched an existing directory: '+$line)}}
                        Assert (-not [IO.Directory]::Exists($own)) 'own directory left behind'
                    }
                    Assert ((Get-Tree $base) -ceq $before) 'an existing directory was created, removed or written'
                }
                'decision-directory-exists-is-unknown'{
                    # Native collision: the name appears just before this call creates it.
                    $base=New-NativeRoot 'collision'
                    $trace=New-NativeTrace -BeforeCreate {param($p)[IO.Directory]::CreateDirectory($p)|Out-Null;[IO.File]::WriteAllText([IO.Path]::Combine($p,'sentinel'),'x')}
                    Assert ((Invoke-Decision $trace $base) -ceq 'unknown') 'an existing name was used for the probe'
                    Assert (@(Get-Logged $trace.Log 'open:').Count -eq 0) 'probe file created in a directory the call did not create'
                    Assert (@(Get-Logged $trace.Log 'remove:').Count -eq 0) 'removed a directory the call did not create'
                    $directory=@(Get-Logged $trace.Log 'directory:')[0]
                    Assert ([IO.Directory]::Exists($directory) -and @([IO.Directory]::GetFileSystemEntries($directory)).Count -eq 1) 'the existing directory changed'
                }
                'decision-directory-create-failure-is-unknown'{
                    $fake=New-FakeIo 'directory'
                    Assert ((Invoke-Decision $fake) -ceq 'unknown') 'directory failure'
                    Assert ($fake.Log.Count -eq 1 -and $fake.Log[0].StartsWith('directory:')) ('continued after directory failure '+($fake.Log -join ','))
                }
                'decision-directory-removal-failure-keeps-result'{
                    $plain=New-FakeIo 'remove';$redirected=New-FakeIo 'remove' {param($p)Get-LocalCachePath $p}
                    Assert ((Invoke-Decision $plain) -ceq 'not-virtualized') 'removal failure changed not-virtualized'
                    Assert ((Invoke-Decision $redirected) -ceq 'virtualized') 'removal failure changed virtualized'
                    foreach($fake in @($plain,$redirected)){$null=Assert-OwnDirectoryOnly $fake.Log;Assert (@(Get-Logged $fake.Log 'remove:').Count -eq 1) 'retried or removed another directory'}
                }
                'decision-concurrent-calls-do-not-remove-each-other'{
                    # Two processes on one stand-in root. The first holds its directory until
                    # the second has created one; the second continues only after the first
                    # has finished and removed its own.
                    $shared=New-NativeRoot 'concurrent'
                    $child=[IO.Path]::Combine($root,'concurrent-child.ps1')
                    [IO.File]::WriteAllText($child,@'
param([string]$ModulePath,[string]$LocalRoot,[string]$Prefix,[string]$Role)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$c=Import-Module $ModulePath -PassThru
$native=& $c {$script:NativeIo}
$createdFirst=[Threading.EventWaitHandle]::OpenExisting($Prefix+'-created-first')
$createdSecond=[Threading.EventWaitHandle]::OpenExisting($Prefix+'-created-second')
$doneFirst=[Threading.EventWaitHandle]::OpenExisting($Prefix+'-done-first')
$state=@{Directory='';BothPresent=$false;OwnAfterOther=$false}
$create={param($Path)
    & $native.CreateDirectory $Path;$state.Directory=$Path
    if($Role -ceq 'first'){
        [void]$createdFirst.Set()
        if(-not $createdSecond.WaitOne(30000)){throw 'barrier'}
        $state.BothPresent=@([IO.Directory]::GetDirectories($LocalRoot,'OneStarMaker.StoreProbe.*')).Count -eq 2
    }else{
        [void]$createdSecond.Set()
        if(-not $doneFirst.WaitOne(30000)){throw 'barrier'}
        $state.OwnAfterOther=[IO.Directory]::Exists($Path)
    }
}.GetNewClosure()
& $c {param($v,$r)$script:ProbeIo=$v;$script:LocalRootForTest=$r} @{CreateDirectory=$create;Open=$native.Open;FinalPath=$native.FinalPath;Close=$native.Close;RemoveDirectory=$native.RemoveDirectory} $LocalRoot
try{$decision=Get-OsmStoreContext}finally{if($Role -ceq 'first'){[void]$doneFirst.Set()}}
ConvertTo-Json -Compress -InputObject ([ordered]@{decision=$decision;directory=$state.Directory;bothPresent=$state.BothPresent;ownAfterOther=$state.OwnAfterOther})
'@,[Text.UTF8Encoding]::new($false))
                    $prefix='OneStarMaker.StoreContextTests.'+[Guid]::NewGuid().ToString('N')
                    $events=@(foreach($name in @('created-first','created-second','done-first')){[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,$prefix+'-'+$name)})
                    try{
                        $module=Join-Path $PSScriptRoot '../WindowsStoreContext.psm1'
                        $first=Start-Child @($child,'-ModulePath',$module,'-LocalRoot',$shared,'-Prefix',$prefix,'-Role','first')
                        $second=Start-Child @($child,'-ModulePath',$module,'-LocalRoot',$shared,'-Prefix',$prefix,'-Role','second')
                        try{$a=Wait-Child $first 60}finally{$b=Wait-Child $second 60}
                    }finally{foreach($handle in $events){$handle.Dispose()}}
                    Assert ($a.Exit -eq 0 -and $b.Exit -eq 0) ('child failed '+$a.Err+$b.Err)
                    $ra=@($a.Out -split "`r?`n"|Where-Object {$_.StartsWith('{')})[-1]|ConvertFrom-Json;$rb=@($b.Out -split "`r?`n"|Where-Object {$_.StartsWith('{')})[-1]|ConvertFrom-Json
                    Assert ($ra.decision -ceq 'not-virtualized' -and $rb.decision -ceq 'not-virtualized') ('decisions '+$ra.decision+' '+$rb.decision)
                    Assert (-not (Test-SamePath $ra.directory $rb.directory)) 'the two calls shared a directory'
                    Assert ($ra.bothPresent) 'the two calls did not overlap'
                    Assert ($rb.ownAfterOther) 'the first call removed the second call''s directory'
                    Assert (@([IO.Directory]::GetFileSystemEntries($shared)).Count -eq 0) 'probe directories left behind'
                }
                'decision-probe-parents'{
                    $fake=New-FakeIo;$null=Invoke-Decision $fake
                    $directory=@(Get-Logged $fake.Log 'directory:')[0];$probe=@(Get-Logged $fake.Log 'open:')[0]
                    Assert (Test-SamePath ([IO.Path]::GetDirectoryName($directory)) $local) 'probe directory parent is not LocalApplicationData'
                    Assert (Test-SamePath ([IO.Path]::GetDirectoryName($probe)) $directory) 'probe file parent is not the call''s own directory'
                    Assert ([IO.Path]::GetFileName($probe) -cmatch '\A[0-9a-f]{32}\.probe\z') 'probe file name'
                    # The stand-in root replaces only the parent of the probe directory.
                    $stand=New-FakeIo;$standRoot=[IO.Path]::Combine($root,'parents');$null=Invoke-Decision $stand $standRoot
                    $null=Assert-OwnDirectoryOnly $stand.Log $standRoot
                }
                'native-create-existing-directory-fails'{
                    $base=New-NativeRoot 'create';$create=& $context {$script:NativeIo.CreateDirectory}
                    $fresh=[IO.Path]::Combine($base,'fresh');& $create $fresh
                    Assert ([IO.Directory]::Exists($fresh)) 'directory not created'
                    [IO.File]::WriteAllText([IO.Path]::Combine($fresh,'sentinel'),'x');$file=[IO.Path]::Combine($base,'file');[IO.File]::WriteAllText($file,'x')
                    $before=Get-Tree $base
                    foreach($existing in @($fresh,$file)){$threw=$false;try{& $create $existing}catch{$threw=$true};Assert $threw ('created over an existing name '+[IO.Path]::GetFileName($existing))}
                    # It never creates a missing parent either.
                    $threw=$false;try{& $create ([IO.Path]::Combine($base,'missing','child'))}catch{$threw=$true}
                    Assert ($threw -and -not [IO.Directory]::Exists([IO.Path]::Combine($base,'missing'))) 'created a missing parent'
                    Assert ((Get-Tree $base) -ceq $before) 'existing entries changed'
                }
                'native-open-does-not-create-parent'{
                    $base=New-NativeRoot 'open';$open=& $context {$script:NativeIo.Open}
                    $parent=[IO.Path]::Combine($base,'gone')
                    $threw=$false;try{$handle=& $open ([IO.Path]::Combine($parent,[Guid]::NewGuid().ToString('N')+'.probe'));$handle.Dispose()}catch{$threw=$true}
                    Assert ($threw -and -not [IO.Directory]::Exists($parent)) 'the probe open re-created its parent'
                }
            }
            $executed.Add($case)
        }catch{$failed.Add($case+': '+$_.Exception.Message)}
        finally{& $context {$script:ProbeIo=$null;$script:LocalRootForTest=$null}}
    }
    if($ResultPath){[IO.File]::WriteAllText($ResultPath,(ConvertTo-Json -Depth 8 -InputObject ([ordered]@{registered=$cases;selected=$selected;executed=@($executed);failed=@($failed);observed=$observed})),[Text.UTF8Encoding]::new($false))}
    foreach($name in $executed){"PASS $name"};foreach($name in $failed){"FAIL $name"}
    "Cases: $($selected.Count)/$($cases.Count); passed: $($executed.Count); failed: $($failed.Count)"
    if($failed.Count -or $executed.Count -ne $selected.Count){exit 1}
}finally{
    & $context {$script:ProbeIo=$null;$script:LocalRootForTest=$null}
    if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)}
}
