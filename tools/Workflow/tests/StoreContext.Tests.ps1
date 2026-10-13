param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# Store-context decision and the three interactive entries. Every OS operation of
# the probe is replaced through the module's private seam, except in the single
# native case, which writes only one DeleteOnClose file in the probe area.
$context=Import-Module (Join-Path $PSScriptRoot '../WindowsStoreContext.psm1') -Force -PassThru
$tools=[IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..','..'))
$local=[Environment]::GetFolderPath('LocalApplicationData')
$probeArea=[IO.Path]::Combine($local,'OneStarMaker.StoreProbe')
$storeRoot=[IO.Path]::Combine($local,'OneStarMaker')
$root=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-store-context-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root)|Out-Null
$cases=@('decision-not-virtualized','decision-virtualized','decision-unknown-open','decision-unknown-final-path','decision-unknown-close','decision-unknown-empty-final-path','probe-outside-store','native-probe-deletes-on-close','no-package-identity-input','cli-virtualized-all-entries','cli-unknown-failure-stages','cli-decision-before-store','import-has-no-store-side-effects')
$selected=@(if($Case -ceq '*'){$cases}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
$observed=[ordered]@{}
function Assert([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
function Test-Within([string]$Path,[string]$Parent){$p=[IO.Path]::GetFullPath($Path).TrimEnd('\');$r=[IO.Path]::GetFullPath($Parent).TrimEnd('\');$p.Equals($r,[StringComparison]::OrdinalIgnoreCase) -or $p.StartsWith($r+'\',[StringComparison]::OrdinalIgnoreCase)}
# A fake OS seam. $Final maps the opened logical path to the reported final path.
function New-FakeIo([string]$Fail='',[scriptblock]$Final={param($p)$p}){
    $log=[Collections.Generic.List[string]]::new()
    $io=@{
        Open={param($Path)$log.Add('open:'+$Path);if($Fail -ceq 'open'){throw 'fake-open'};[pscustomobject]@{Path=$Path}}.GetNewClosure()
        FinalPath={param($Handle)$log.Add('final-path');if($Fail -ceq 'final-path'){throw 'fake-final-path'};& $Final $Handle.Path}.GetNewClosure()
        Close={param($Handle)$log.Add('close');if($Fail -ceq 'close'){throw 'fake-close'}}.GetNewClosure()
    }
    return [pscustomobject]@{Io=$io;Log=$log}
}
function Invoke-Decision($Fake){& $context {param($v)$script:ProbeIo=$v} $Fake.Io;try{return Get-OsmStoreContext}finally{& $context {$script:ProbeIo=$null}}}
function Get-LocalCachePath([string]$Path){return [IO.Path]::Combine($local,'Packages','Fixture.Package_0000000000000','LocalCache','Local',$Path.Substring($local.Length+1))}
function Invoke-Child([string[]]$Arguments,[int]$Seconds=30){
    $info=[Diagnostics.ProcessStartInfo]::new([Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)
    $info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($argument in @('-NoProfile','-File')+$Arguments){$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info)
    try{
        $out=$process.StandardOutput.ReadToEndAsync();$err=$process.StandardError.ReadToEndAsync()
        Assert ($process.WaitForExit($Seconds*1000)) 'child deadline'
        Assert ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),3000)) 'child pipes'
        return [pscustomobject]@{Exit=$process.ExitCode;Out=$out.Result;Err=$err.Result}
    }finally{if(-not $process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
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
    Assert ($Run.Calls.Count -ge 2 -and $Run.Calls[0] -ceq 'import:WindowsStoreContext.psm1' -and $Run.Calls[1].StartsWith('probe:open:')) ($Run.Label+' did not decide first')
    foreach($open in @($Run.Calls|Where-Object {$_.StartsWith('probe:open:')})){$path=$open.Substring(11);Assert ((Test-Within $path $probeArea) -and -not (Test-Within $path $storeRoot)) ($Run.Label+' probe outside area')}
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
                'decision-not-virtualized'{$f=New-FakeIo;Assert ((Invoke-Decision $f) -ceq 'not-virtualized') 'not virtualized';Assert ($f.Log.Count -eq 3 -and $f.Log[1] -ceq 'final-path' -and $f.Log[2] -ceq 'close') 'seam order'}
                'decision-virtualized'{$f=New-FakeIo -Final {param($p)Get-LocalCachePath $p};Assert ((Invoke-Decision $f) -ceq 'virtualized') 'virtualized';Assert ($f.Log[-1] -ceq 'close') 'handle not closed'}
                'decision-unknown-open'{$f=New-FakeIo 'open';Assert ((Invoke-Decision $f) -ceq 'unknown') 'open failure';Assert ($f.Log.Count -eq 1) 'continued after open failure'}
                'decision-unknown-final-path'{$f=New-FakeIo 'final-path';Assert ((Invoke-Decision $f) -ceq 'unknown') 'final-path failure';Assert ($f.Log[-1] -ceq 'close') 'handle not closed after query failure'}
                # A close failure is undecidable even when the final path matched.
                'decision-unknown-close'{$f=New-FakeIo 'close';Assert ((Invoke-Decision $f) -ceq 'unknown') 'close failure'}
                'decision-unknown-empty-final-path'{$f=New-FakeIo -Final {param($p)''};Assert ((Invoke-Decision $f) -ceq 'unknown') 'empty final path'}
                'probe-outside-store'{
                    $first=New-FakeIo;$null=Invoke-Decision $first;$second=New-FakeIo;$null=Invoke-Decision $second
                    $a=$first.Log[0].Substring(5);$b=$second.Log[0].Substring(5)
                    Assert ((Test-Within $a $probeArea) -and -not (Test-Within $a $storeRoot)) 'probe inside store'
                    Assert ($a -cne $b -and [IO.Path]::GetFileName($a) -cmatch '\A[0-9a-f]{32}\.probe\z') 'probe name not unique'
                }
                'native-probe-deletes-on-close'{
                    # Wrap the native operations to learn the one path this call created.
                    $native=& $context {$script:NativeIo};$seen=[Collections.Generic.List[string]]::new()
                    $wrap=@{Open={param($p)$seen.Add($p);& $native.Open $p}.GetNewClosure();FinalPath=$native.FinalPath;Close=$native.Close}
                    & $context {param($v)$script:ProbeIo=$v} $wrap
                    try{$value=Get-OsmStoreContext}finally{& $context {$script:ProbeIo=$null}}
                    $observed.nativeDecision=$value
                    Assert ($value -ceq 'not-virtualized') ('this suite requires a non-virtualized host; got '+$value)
                    Assert ($seen.Count -eq 1 -and -not [IO.File]::Exists($seen[0])) 'probe file left behind'
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
                    foreach($mode in @('open-fails','final-path-fails','close-fails')){
                        Assert-StoppedBeforeStore (Invoke-Entry 'artifacts.ps1' $artifactEntries[0] $mode) 'store-context-unknown'
                        Assert-StoppedBeforeStore (Invoke-Entry 'workflow-task.ps1' $workflowEntries[1] $mode) 'store-context-unknown'
                        Assert-StoppedBeforeStore (Invoke-Entry 'harness.ps1' $harnessEntries[2] $mode) 'store-context-unknown'
                    }
                }
                'cli-decision-before-store'{
                    # Non-virtualized entries continue; the first store boundary follows the closed probe.
                    foreach($run in @((Invoke-Entry 'artifacts.ps1' $artifactEntries[0] 'not-virtualized'),(Invoke-Entry 'workflow-task.ps1' $workflowEntries[1] 'not-virtualized'),(Invoke-Entry 'harness.ps1' $harnessEntries[2] 'not-virtualized'))){
                        $close=[Array]::IndexOf([string[]]$run.Calls,'probe:close');$store=@(for($i=0;$i -lt $run.Calls.Count;$i++){if($run.Calls[$i].StartsWith('store:')){$i}})
                        Assert ($close -ge 0 -and $store.Count -ge 1 -and $store[0] -gt $close) ($run.Label+' order '+($run.Calls -join ','))
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
                        "& `$c {param(`$p)`$script:ProbeIo=@{Open={param(`$x)[IO.File]::AppendAllText(`$p,'probe'+[Environment]::NewLine);throw 'x'};FinalPath={throw 'x'};Close={}}} '"+$calls.Replace("'","''")+"'`n"
                    foreach($module in $modules){$body+="Import-Module '"+$module.Replace("'","''")+"'`n"}
                    $body+="'imported'"
                    [IO.File]::WriteAllText($script,$body,[Text.UTF8Encoding]::new($false))
                    $run=Invoke-Child @($script) 120
                    Assert ($run.Exit -eq 0 -and $run.Out.Contains('imported')) ('import failed '+$run.Err)
                    Assert (-not [IO.File]::Exists($calls)) 'import ran the probe'
                }
            }
            $executed.Add($case)
        }catch{$failed.Add($case+': '+$_.Exception.Message)}
        finally{& $context {$script:ProbeIo=$null}}
    }
    if($ResultPath){[IO.File]::WriteAllText($ResultPath,(ConvertTo-Json -Depth 8 -InputObject ([ordered]@{registered=$cases;selected=$selected;executed=@($executed);failed=@($failed);observed=$observed})),[Text.UTF8Encoding]::new($false))}
    foreach($name in $executed){"PASS $name"};foreach($name in $failed){"FAIL $name"}
    "Cases: $($selected.Count)/$($cases.Count); passed: $($executed.Count); failed: $($failed.Count)"
    if($failed.Count -or $executed.Count -ne $selected.Count){exit 1}
}finally{
    & $context {$script:ProbeIo=$null}
    if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)}
}
