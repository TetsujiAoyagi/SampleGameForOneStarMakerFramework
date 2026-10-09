param([string]$ResultPath='',[string]$Case='*',[switch]$MissingE1SourceProbe)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
. (Join-Path $PSScriptRoot 'EvidenceReaderFixture.ps1')
$registered=@('v2-roundtrip','reader-hash','handoff-identity-substitution','unsupported-schema','selection-path','one-put-lost-response','readback-failure','receipt-atomic-failure','secret-exception-output','fresh-child-fetch','legacy-cli-network-zero','put-reconcile-no-reput','put-recovery-child-protected')
$selected=if($Case -ceq '*'){$registered}else{@($Case.Split(','))};$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
foreach($caseName in $selected){
    $executed.Add($caseName)
    try{
        Assert-EvidenceTest ($caseName -cin $registered) 'unknown-case';$env=New-EvidenceTestEnvironment;$fixture=New-EvidenceReaderFixture $env.Root;$selectionPath=[IO.Path]::Combine($env.Root,'selection.json');Write-EvidenceTestJson $selectionPath $fixture.Selection
        switch($caseName){
            'unsupported-schema'{$old=$fixture.Selection;$old.schemaVersion=1;Write-EvidenceTestJson $selectionPath $old;$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'failed' -and $env.Counts.Count -eq 0) 'legacy network';break}
            'selection-path'{$fixture.Selection.files=@('../outside');Write-EvidenceTestJson $selectionPath $fixture.Selection;$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'failed' -and $env.Counts.Count -eq 0) 'path network';break}
            'one-put-lost-response'{$inner=$env.Transport;$hook={param($g,$req)$r=& $inner $g $req;if($req.Operation -ceq 'put'){throw 'response-lost'};$r}.GetNewClosure();Set-EvidenceTestValue ArtifactApplication TransportHook $hook;$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'failed' -and @($env.Counts.Values|Where-Object {$_ -gt 1}).Count -eq 0 -and $env.Objects.Count -eq 1) 'one-put';break}
            'readback-failure'{Set-EvidenceTestValue ArtifactApplication ReadbackHook {throw 'reader-failed'};$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'failed' -and $null -eq $p.reference) 'unverified success';break}
            'receipt-atomic-failure'{Set-EvidenceTestValue EvidencePaths WriteHook {param($stage,$path)if($stage -ceq 'before-rename' -and $path -like '*receipts*'){throw 'crash'}};$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'failed' -and $null -eq $p.reference) 'receipt success';break}
            'secret-exception-output'{Set-EvidenceTestValue ArtifactApplication TransportHook {throw 'DUMMY_SECRET'};$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ((ConvertTo-Json $p -Depth 10) -cnotmatch 'DUMMY_SECRET') 'secret output';break}
            'legacy-cli-network-zero'{
                $info=[Diagnostics.ProcessStartInfo]::new('pwsh');$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
                foreach($arg in @('-NoProfile','-File',(Join-Path $PSScriptRoot '../../artifacts.ps1'),'evidence','commit','--candidate','dummy','--candidate-sha256',('a'*64),'--settings-after','dummy','--settings-after-sha256',('a'*64))){$info.ArgumentList.Add($arg)}
                $proc=[Diagnostics.Process]::new();$proc.StartInfo=$info;try{$null=$proc.Start();$out=$proc.StandardOutput.ReadToEndAsync();$err=$proc.StandardError.ReadToEndAsync();Assert-EvidenceTest ($proc.WaitForExit(10000)) 'legacy bounded';Assert-EvidenceTest ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),3000)) 'legacy pipes';Assert-EvidenceTest ($proc.ExitCode -eq 1 -and $out.Result -match 'unsupported-command' -and $env.Counts.Count -eq 0) 'legacy accepted'}finally{$proc.Dispose()};break
            }
            'put-recovery-child-protected'{
                $inner=$env.Transport;$hook={param($g,$req)$r=& $inner $g $req;if($req.Operation -ceq 'put'){throw 'response-lost'};$r}.GetNewClosure();Set-EvidenceTestValue ArtifactApplication TransportHook $hook;$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath
                $readback={param($op,$c,$gen,$key,$bytes,$hash)Write-EvidenceTestJson ([IO.Path]::Combine($op,'readback-process.json')) ([pscustomobject]@{process=Get-EvidenceProcessIdentity;exited=$false;pipesClosed=$false});throw 'unconfirmed-child'}.GetNewClosure();Set-EvidenceTestValue ArtifactApplication ReadbackHook $readback
                $g=Enter-WorkflowTaskGuard ('a'*64) fixture
                try{
                    $rejected=$false;try{$null=Assert-EvidenceTransitionSafe $env.Config fixture $g}catch{$rejected=$true};Assert-EvidenceTest $rejected 'unknown recovery child accepted'
                    $task=Read-EvidenceTask $env.Config fixture $g;$artifact=$task.artifacts[0];Assert-EvidenceTest ($null -ne $artifact.operation -and [IO.File]::Exists($artifact.operation.childMarker)) 'recovery child ownership missing'
                    $stage=@($task.staging|Where-Object path -CEQ $artifact.operation.path);Assert-EvidenceTest ($stage.Count -eq 1 -and -not $stage[0].adopted) 'recovery stage missing'
                    $stopped=[pscustomobject]@{pid=2147483647;startedAt='2000-01-01T00:00:00.0000000Z'};$artifact.operation.process=$stopped;$stage[0].operation.process=$stopped;$null=Write-EvidenceTask $env.Config fixture $task $task.generation $g;$recoveryPath=$stage[0].path
                }finally{$g.Dispose()}
                $before=($env.Counts.Values|Measure-Object -Sum).Sum;$later=[DateTimeOffset]::UtcNow.AddDays(8);Set-EvidenceTestValue EvidenceCleanup Clock {$later}.GetNewClosure();$clean=Invoke-EvidenceCleanup $env.ConfigPath
                Assert-EvidenceTest ($clean.status -ceq 'pending' -and $env.Objects.Count -eq 1 -and [IO.Directory]::Exists($recoveryPath) -and ($env.Counts.Values|Measure-Object -Sum).Sum -eq $before) 'unknown recovery child released or retried'
                break
            }
            'put-reconcile-no-reput'{
                $inner=$env.Transport;$hook={param($g,$req)$r=& $inner $g $req;if($req.Operation -ceq 'put'){throw 'response-lost'};$r}.GetNewClosure();Set-EvidenceTestValue ArtifactApplication TransportHook $hook;$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath
                $g=Enter-WorkflowTaskGuard ('a'*64) fixture;try{$null=Assert-EvidenceTransitionSafe $env.Config fixture $g;$task=Read-EvidenceTask $env.Config fixture $g;Assert-EvidenceTest ($task.artifacts[0].state -ceq 'ready' -and @($env.Counts.GetEnumerator()|Where-Object {$_.Key -like 'put *' -and $_.Value -gt 1}).Count -eq 0) 'recovery reput';foreach($owned in $task.artifacts[0].ownedCopies){Assert-EvidenceTest (@($task.staging|Where-Object {$owned.path.StartsWith($_.path+[IO.Path]::DirectorySeparatorChar,[StringComparison]::Ordinal)}).Count -gt 0) 'put recovery ownership missing'}}finally{$g.Dispose()};break
            }
            default{
                $p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'passed') ('publish failed '+$p.reasonCode)
                if($caseName -ceq 'fresh-child-fetch'){
                    $ref=Read-EvidenceReference $p.reference $env.Config;[IO.File]::WriteAllBytes([IO.Path]::Combine($env.Root,'remote.zip'),$env.Objects[$ref.key])
                    $info=[Diagnostics.ProcessStartInfo]::new('pwsh');$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
                    foreach($arg in @('-NoProfile','-File',(Join-Path $PSScriptRoot 'EvidenceTestChild.ps1'),'-Root',$env.Root,'-ConfigPath',$env.ConfigPath,'-Reference',$p.reference,'-Sha256',$p.packageSha256)){$info.ArgumentList.Add($arg)}
                    $proc=[Diagnostics.Process]::new();$proc.StartInfo=$info;try{$null=$proc.Start();$out=$proc.StandardOutput.ReadToEndAsync();$err=$proc.StandardError.ReadToEndAsync();Assert-EvidenceTest ($proc.WaitForExit(15000)) 'child bounded';Assert-EvidenceTest ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),3000)) 'child pipes';Assert-EvidenceTest ($proc.ExitCode -eq 0 -and $out.Result -match '"status":"passed"') ('child fetch '+$err.Result+$out.Result)}finally{$proc.Dispose()};break
                }
                $hash=if($caseName -ceq 'reader-hash'){'0'*64}else{$p.packageSha256};$reference=$p.reference
                if($caseName -ceq 'handoff-identity-substitution'){$ref=Read-EvidenceReference $reference $env.Config;$ref.head='9'*40;$reference='osm-evidence-v2:'+ [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json $ref -Compress))).TrimEnd('=').Replace('+','-').Replace('/','_')}
                $f=Invoke-EvidenceFetch $env.ConfigPath $reference $hash
                if($caseName -ceq 'v2-roundtrip'){Assert-EvidenceTest ($f.status -ceq 'passed') ('fetch failed '+$f.reasonCode);foreach($name in $fixture.Files){Assert-EvidenceTest ((Get-FileHash -LiteralPath ([IO.Path]::Combine($f.outputPath,$name))).Hash -ceq (Get-FileHash -LiteralPath ([IO.Path]::Combine($fixture.Source,$name))).Hash) 'inner hash'}}else{Assert-EvidenceTest ($f.status -ceq 'failed' -and $null -eq $f.outputPath) 'reader accepted substitution'}
            }
        }
    }catch{$failed.Add($caseName);[Console]::Error.WriteLine($caseName+': '+$_.Exception.Message)}
    finally{Set-EvidenceTestValue EvidencePaths WriteHook $null}
}
Complete-EvidenceTestSuite $ResultPath $registered $selected $executed $failed
[Console]::WriteLine("ArtifactEvidence $($executed.Count)/$($registered.Count), failed $($failed.Count)")
