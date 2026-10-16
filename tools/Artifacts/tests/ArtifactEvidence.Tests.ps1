param([string]$ResultPath='',[string]$Case='*',[switch]$MissingE1SourceProbe)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
. (Join-Path $PSScriptRoot 'EvidenceReaderFixture.ps1')
$registered=@('v2-roundtrip','reader-hash','handoff-identity-substitution','unsupported-schema','selection-path','one-put-lost-response','readback-failure','receipt-atomic-failure','secret-exception-output','fresh-child-fetch','legacy-cli-network-zero','put-reconcile-no-reput','put-recovery-child-protected','config-invalid-path','config-invalid-path-other-commands','deployment-receipt-missing','selection-invalid-path','invalid-path-no-leak','cli-relative-selection')
# Path-diagnostic cases: a rejected input must stop before the Workflow guard (which
# creates the task directory), the Storage catalog and the transport.
function Assert-EvidenceNotStarted($Env,[string]$Label){
    Assert-EvidenceTest ($Env.Counts.Count -eq 0) ($Label+' network')
    Assert-EvidenceTest (-not [IO.Directory]::Exists([IO.Path]::Combine($Env.Root,'catalog'))) ($Label+' catalog')
    Assert-EvidenceTest (-not [IO.Directory]::Exists([IO.Path]::Combine($Env.Root,'workflow',('a'*64),'tasks','unguarded'))) ($Label+' guard')
}
function New-EvidenceBadInputs($Env,$Fixture){
    $bad=[IO.Path]::Combine($Env.Root,'bad-input-marker');[IO.Directory]::CreateDirectory($bad)|Out-Null
    $broken=[IO.Path]::Combine($bad,'broken.json');[IO.File]::WriteAllText($broken,'{"DUMMY_SECRET":',[Text.UTF8Encoding]::new($false))
    $config=Get-Content -LiteralPath $Env.ConfigPath -Raw|ConvertFrom-Json -AsHashtable
    $short=[ordered]@{};foreach($k in $config.Keys){if($k -cne 'policy'){$short[$k]=$config[$k]}};$shortPath=[IO.Path]::Combine($bad,'short.json');Write-EvidenceTestJson $shortPath $short
    $hex=[ordered]@{}+$config;$hex.deploymentId='zz';$hexPath=[IO.Path]::Combine($bad,'hex.json');Write-EvidenceTestJson $hexPath $hex
    $value=[ordered]@{}+$config;$value.profile='other';$valuePath=[IO.Path]::Combine($bad,'value.json');Write-EvidenceTestJson $valuePath $value
    $sel=[ordered]@{}+$Fixture.Selection;$sel.taskId='unguarded';$selPath=[IO.Path]::Combine($bad,'selection.json');Write-EvidenceTestJson $selPath $sel
    $selShort=[ordered]@{}+$sel;$selShort.Remove('base');$selShortPath=[IO.Path]::Combine($bad,'selection-short.json');Write-EvidenceTestJson $selShortPath $selShort
    return [pscustomobject]@{Relative='bad-input-marker\relative.json';Missing=[IO.Path]::Combine($bad,'missing.json');Broken=$broken;Short=$shortPath;Hex=$hexPath;Value=$valuePath;Selection=$selPath;SelectionShort=$selShortPath}
}
$registered+=@('stage-local-fetch-child','stage-local-fetch-reject','stage-unresolved-refused','stage-source-publish','stage-changes-patch','stage-catalog-read','remote-fetch-codes-unchanged')
function ConvertTo-EvidenceTestReference($Ref){return 'osm-evidence-v2:'+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json $Ref -Compress))).TrimEnd('=').Replace('+','-').Replace('/','_')}
function Invoke-EvidenceTestStage($Env,[string]$SelectionPath){$s=Invoke-EvidenceStage $Env.ConfigPath $SelectionPath;Assert-EvidenceTest ($s.status -ceq 'passed' -and $s.locality -ceq 'local' -and $s.reference.StartsWith('osm-evidence-v2:')) ('stage failed '+$s.reasonCode);return $s}
function Invoke-EvidenceTestGit([string]$Repo,[string[]]$GitArgs){$out=& git -C $Repo @GitArgs;if($LASTEXITCODE -ne 0){throw ('git failed: '+($GitArgs -join ' '))};return $out}
# Relative path -> SHA-256 of every file under a checkout, excluding git metadata.
function Get-EvidenceTestTreeHashes([string]$Dir){$h=@{};foreach($f in Get-ChildItem -LiteralPath $Dir -Recurse -File -Force){$rel=[IO.Path]::GetRelativePath($Dir,$f.FullName).Replace('\','/');if($rel -cmatch '\A\.git(/|\z)'){continue};$h[$rel]=(Get-FileHash -LiteralPath $f.FullName).Hash};return $h}
# Compares base, base+changes.patch and the recorded file set. Each file must be
# either recorded with the patched content, deleted by the patch, or unchanged.
function Get-EvidenceTestPatchProblems($Base,$Applied,$Recorded,[string[]]$Deleted){
    $problems=[Collections.Generic.List[string]]::new()
    foreach($k in $Recorded.Keys){if($Applied[$k] -cne $Recorded[$k]){$problems.Add('recorded-differs:'+$k)}}
    foreach($k in $Applied.Keys){if(-not $Recorded.ContainsKey($k) -and $Base[$k] -cne $Applied[$k]){$problems.Add('unrecorded-change:'+$k)}}
    foreach($k in $Deleted){if($Applied.ContainsKey($k) -or $Recorded.ContainsKey($k) -or -not $Base.ContainsKey($k)){$problems.Add('deletion:'+$k)}}
    foreach($k in $Base.Keys){if(-not $Applied.ContainsKey($k) -and $k -cnotin $Deleted){$problems.Add('unrecorded-deletion:'+$k)}}
    return ,$problems.ToArray()
}
$selected=if($Case -ceq '*'){$registered}else{@($Case.Split(','))};$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
foreach($caseName in $selected){
    $executed.Add($caseName)
    try{
        Assert-EvidenceTest ($caseName -cin $registered) 'unknown-case';$env=New-EvidenceTestEnvironment;$fixture=New-EvidenceReaderFixture $env.Root;$selectionPath=[IO.Path]::Combine($env.Root,'selection.json');Write-EvidenceTestJson $selectionPath $fixture.Selection
        switch($caseName){
            'unsupported-schema'{$old=$fixture.Selection;$old.schemaVersion=1;Write-EvidenceTestJson $selectionPath $old;$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'failed' -and $env.Counts.Count -eq 0) 'legacy network';break}
            'selection-path'{$fixture.Selection.files=@('../outside');Write-EvidenceTestJson $selectionPath $fixture.Selection;$p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'failed' -and $env.Counts.Count -eq 0) 'path network';break}
            'config-invalid-path'{
                $b=New-EvidenceBadInputs $env $fixture
                foreach($path in @($b.Relative,$b.Missing,$b.Broken,$b.Short,$b.Hex)){$p=Invoke-EvidencePublish $path $b.Selection;Assert-EvidenceTest ($p.status -ceq 'failed' -and $p.reasonCode -ceq 'invalid-evidence-config' -and $p.residue.remote -ceq 'pre-put') ('config code '+$p.reasonCode)}
                $p=Invoke-EvidencePublish $b.Value $b.Selection;Assert-EvidenceTest ($p.reasonCode -ceq 'unsupported-schema') ('value code '+$p.reasonCode)
                Assert-EvidenceNotStarted $env 'config';break
            }
            'config-invalid-path-other-commands'{
                $b=New-EvidenceBadInputs $env $fixture;$codes=@()
                foreach($path in @($b.Relative,$b.Broken)){
                    $codes+=(Invoke-EvidenceFetch $path 'osm-evidence-v2:x' ('0'*64)).reasonCode
                    $codes+=(Invoke-EvidenceInspect $path 'unguarded').reasonCode
                    $codes+=(Invoke-EvidenceUse $path 'osm-evidence-v2:x' 'reviewer' ([DateTimeOffset]::UtcNow.AddHours(1).ToString('o')) 'review').reasonCode
                    $codes+=(Invoke-EvidenceRelease $path 'osm-evidence-v2:x' ('a'*32)).reasonCode
                }
                Assert-EvidenceTest (@($codes|Where-Object {$_ -cne 'invalid-evidence-config'}).Count -eq 0) ('other codes '+($codes -join ','))
                Assert-EvidenceNotStarted $env 'other';break
            }
            'deployment-receipt-missing'{
                $b=New-EvidenceBadInputs $env $fixture;$receipt=[IO.Path]::Combine($env.Root,'deployments',('d'*32)+'.json');$saved=[IO.File]::ReadAllText($receipt)
                [IO.File]::Delete($receipt);$codes=@((Invoke-EvidencePublish $env.ConfigPath $b.Selection).reasonCode,(Invoke-EvidenceInspect $env.ConfigPath 'unguarded').reasonCode)
                $short=$saved|ConvertFrom-Json -AsHashtable;$short.Remove('ownerObservationId');Write-EvidenceTestJson $receipt $short;$codes+=(Invoke-EvidencePublish $env.ConfigPath $b.Selection).reasonCode
                Assert-EvidenceTest (@($codes|Where-Object {$_ -cne 'unknown-deployment'}).Count -eq 0) ('receipt codes '+($codes -join ','))
                Assert-EvidenceNotStarted $env 'receipt';break
            }
            'selection-invalid-path'{
                $b=New-EvidenceBadInputs $env $fixture
                foreach($path in @($b.Relative,$b.Missing,$b.Broken,$b.SelectionShort)){$p=Invoke-EvidencePublish $env.ConfigPath $path;Assert-EvidenceTest ($p.status -ceq 'failed' -and $p.reasonCode -ceq 'invalid-selection' -and $p.residue.remote -ceq 'pre-put') ('selection code '+$p.reasonCode)}
                Assert-EvidenceNotStarted $env 'selection';break
            }
            'invalid-path-no-leak'{
                $b=New-EvidenceBadInputs $env $fixture
                $out=@((Invoke-EvidencePublish $b.Relative $b.Selection),(Invoke-EvidencePublish $b.Broken $b.Selection),(Invoke-EvidencePublish $env.ConfigPath $b.Broken),(Invoke-EvidencePublish $env.ConfigPath $b.Relative),(Invoke-EvidenceInspect $b.Broken 'unguarded'))|ConvertTo-Json -Depth 10
                foreach($marker in @('bad-input-marker','DUMMY_SECRET',$env.Root)){Assert-EvidenceTest (-not $out.Contains($marker)) 'leaked input'};break
            }
            'cli-relative-selection'{
                $info=[Diagnostics.ProcessStartInfo]::new('pwsh');$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
                foreach($arg in @('-NoProfile','-File',(Join-Path $PSScriptRoot 'EvidenceTestChild.ps1'),'-Root',$env.Root,'-ConfigPath',$env.ConfigPath,'-Action','publish','-Selection','bad-input-marker\relative.json')){$info.ArgumentList.Add($arg)}
                $proc=[Diagnostics.Process]::new();$proc.StartInfo=$info;try{$null=$proc.Start();$out=$proc.StandardOutput.ReadToEndAsync();$err=$proc.StandardError.ReadToEndAsync();Assert-EvidenceTest ($proc.WaitForExit(15000)) 'cli bounded';Assert-EvidenceTest ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),3000)) 'cli pipes'
                    $json=@($out.Result -split "`r?`n"|Where-Object {$_.StartsWith('{')})[-1]|ConvertFrom-Json;Assert-EvidenceTest ($proc.ExitCode -eq 1 -and $json.reasonCode -ceq 'invalid-selection' -and $json.residue.remote -ceq 'pre-put' -and $err.Result.Length -eq 0 -and -not $out.Result.Contains('bad-input-marker')) ('cli '+$proc.ExitCode+' '+$out.Result+$err.Result)}finally{$proc.Dispose()}
                Assert-EvidenceNotStarted $env 'cli';break
            }
            'stage-local-fetch-child'{
                # S1: a separate process holding only the reference and hash reads every entry, with all remote paths fatal.
                $s=Invoke-EvidenceTestStage $env $selectionPath;Assert-EvidenceTest ((Get-EvidenceRemoteTouches $env) -eq 0) 'stage touched remote'
                $a=(Read-EvidenceTestTask $env).artifacts[0]
                Assert-EvidenceTest ($a.state -ceq 'staged' -and $a.origin -ceq 'stage' -and $null -eq $a.receipt -and $null -eq $a.putIntent -and $a.key -ceq (Read-EvidenceReference $s.reference $env.Config).key -and $a.packageSha256 -ceq $s.packageSha256) 'staged record'
                $info=[Diagnostics.ProcessStartInfo]::new('pwsh');$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
                foreach($arg in @('-NoProfile','-File',(Join-Path $PSScriptRoot 'EvidenceTestChild.ps1'),'-Root',$env.Root,'-ConfigPath',$env.ConfigPath,'-Reference',$s.reference,'-Sha256',$s.packageSha256,'-Action','fetch-offline')){$info.ArgumentList.Add($arg)}
                $proc=[Diagnostics.Process]::new();$proc.StartInfo=$info
                try{$null=$proc.Start();$out=$proc.StandardOutput.ReadToEndAsync();$err=$proc.StandardError.ReadToEndAsync();Assert-EvidenceTest ($proc.WaitForExit(30000)) 'child bounded';Assert-EvidenceTest ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out,$err),3000)) 'child pipes'
                    Assert-EvidenceTest ($proc.ExitCode -eq 0) ('offline child fetch '+$out.Result+$err.Result);$json=@($out.Result -split "`r?`n"|Where-Object {$_.StartsWith('{')})[-1]|ConvertFrom-Json}finally{$proc.Dispose()}
                foreach($name in $fixture.Files){Assert-EvidenceTest ((Get-FileHash -LiteralPath ([IO.Path]::Combine($json.outputPath,$name))).Hash -ceq (Get-FileHash -LiteralPath ([IO.Path]::Combine($fixture.Source,$name))).Hash) ('entry differs '+$name)}
                Assert-EvidenceTest ((Get-EvidenceRemoteTouches $env) -eq 0) 'parent remote touched';break
            }
            'stage-local-fetch-reject'{
                # I2: every rejection is final and never falls back to the remote store.
                $s=Invoke-EvidenceTestStage $env $selectionPath
                Edit-EvidenceTestTask $env {param($t)$t.artifacts[0].packageSha256='0'*64};$f=Invoke-EvidenceFetch $env.ConfigPath $s.reference $s.packageSha256
                Assert-EvidenceTest ($f.status -ceq 'failed' -and $f.reasonCode -ceq 'identity-mismatch' -and $null -eq $f.outputPath) ('hash rewrite '+$f.reasonCode)
                Edit-EvidenceTestTask $env {param($t)$t.artifacts[0].packageSha256=$s.packageSha256}
                $g=Enter-WorkflowTaskGuard ('a'*64) 'other';try{$null=Invoke-WorkflowTaskTransition ('a'*64) 'other' 'started' 0 'other-start' '' $g}finally{$g.Dispose()}
                $ref=Read-EvidenceReference $s.reference $env.Config;$ref.taskId='other';$ref.key=Get-EvidenceKey $env.Config 'other' $ref.artifactId
                $f=Invoke-EvidenceFetch $env.ConfigPath (ConvertTo-EvidenceTestReference $ref) $s.packageSha256;Assert-EvidenceTest ($f.status -ceq 'failed' -and $f.reasonCode -ceq 'catalog-missing') ('other task '+$f.reasonCode)
                [IO.File]::Delete((Get-EvidenceStagedZip (Read-EvidenceTestTask $env).artifacts[0]).path)
                $f=Invoke-EvidenceFetch $env.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($f.status -ceq 'failed' -and $f.reasonCode -ceq 'changed-copy' -and $null -eq $f.outputPath) ('zip missing '+$f.reasonCode)
                Assert-EvidenceTest ((Get-EvidenceRemoteTouches $env) -eq 0) 'rejection touched remote';break
            }
            'stage-unresolved-refused'{
                # I1: an unfinished put or delete in the task is refused, never resolved, by stage and local fetch.
                $s=Invoke-EvidenceTestStage $env $selectionPath
                $inner=$env.Transport;$hook={param($g,$req)$r=& $inner $g $req;if($req.Operation -ceq 'put'){throw 'response-lost'};$r}.GetNewClosure();Set-EvidenceTestValue ArtifactApplication TransportHook $hook
                $p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'failed' -and @((Read-EvidenceTestTask $env).artifacts|Where-Object state -CEQ 'put-pending').Count -eq 1) 'put-pending fixture'
                $before=Get-EvidenceRemoteTouches $env
                $x=Invoke-EvidenceStage $env.ConfigPath $selectionPath;Assert-EvidenceTest ($x.status -ceq 'failed' -and $x.reasonCode -ceq 'unresolved-put') ('stage put '+$x.reasonCode)
                $f=Invoke-EvidenceFetch $env.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($f.status -ceq 'failed' -and $f.reasonCode -ceq 'unresolved-put') ('fetch put '+$f.reasonCode)
                Assert-EvidenceTest ((Get-EvidenceRemoteTouches $env) -eq $before) 'put resolved over network'
                $env=New-EvidenceTestEnvironment;$fixture=New-EvidenceReaderFixture $env.Root;Write-EvidenceTestJson $selectionPath $fixture.Selection
                $p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'passed') 'ready fixture';$s=Invoke-EvidenceTestStage $env $selectionPath
                $readyId=(Read-EvidenceReference $p.reference $env.Config).artifactId
                Edit-EvidenceTestTask $env {param($t)$r=@($t.artifacts|Where-Object artifactId -CEQ $readyId)[0];$r.state='delete-pending';$r.deleteIntent=[pscustomobject]@{key=$r.key;generation=$t.generation;packageSha256=$r.packageSha256;process=[pscustomobject]@{pid=2147483647;startedAt='2000-01-01T00:00:00.0000000Z'};createdAt=[DateTimeOffset]::UtcNow.ToString('o')}}
                $before=Get-EvidenceRemoteTouches $env
                $x=Invoke-EvidenceStage $env.ConfigPath $selectionPath;Assert-EvidenceTest ($x.status -ceq 'failed' -and $x.reasonCode -ceq 'unresolved-delete') ('stage delete '+$x.reasonCode)
                $f=Invoke-EvidenceFetch $env.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($f.status -ceq 'failed' -and $f.reasonCode -ceq 'unresolved-delete') ('fetch delete '+$f.reasonCode)
                Assert-EvidenceTest ((Get-EvidenceRemoteTouches $env) -eq $before -and $env.Objects.Count -eq 1) 'delete resolved over network';break
            }
            'stage-source-publish'{
                # I5: publishing from a staged staging makes another artifact; the staged one is never sent.
                $s=Invoke-EvidenceTestStage $env $selectionPath;$t=Read-EvidenceTestTask $env;$a=$t.artifacts[0];$stagePath=@($t.staging|Where-Object artifactId -CEQ $a.artifactId)[0].path;$staged=ConvertTo-Json $a -Depth 24 -Compress
                $sourcePath=[IO.Path]::Combine($env.Root,'source-selection.json');Write-EvidenceTestJson $sourcePath ([ordered]@{schemaVersion=2;purpose='evidence';taskId='fixture';root=$stagePath;files=@($fixture.Files|ForEach-Object{'snapshot/'+$_});base=$fixture.Selection.base;head=$fixture.Selection.head})
                $p=Invoke-EvidencePublish $env.ConfigPath $sourcePath;Assert-EvidenceTest ($p.status -ceq 'passed') ('source publish '+$p.reasonCode)
                $t=Read-EvidenceTestTask $env;$other=@($t.artifacts|Where-Object artifactId -CNE $a.artifactId)
                Assert-EvidenceTest ($other.Count -eq 1 -and $other[0].state -ceq 'ready' -and (Get-EvidenceArtifactOrigin $other[0]) -ceq 'publish' -and $p.reference -cne $s.reference) 'separate artifact'
                Assert-EvidenceTest ((ConvertTo-Json @($t.artifacts|Where-Object artifactId -CEQ $a.artifactId)[0] -Depth 24 -Compress) -ceq $staged) 'staged artifact changed'
                Assert-EvidenceTest (-not $env.Counts.ContainsKey('put '+$a.key) -and @($env.Counts.Keys|Where-Object {$_ -like 'put *'}).Count -eq 1) 'staged key sent';break
            }
            'stage-changes-patch'{
                # S1/AC11: a working tree with added, modified, deleted, untracked and binary files.
                $repo=[IO.Path]::Combine($env.Root,'repo');[IO.Directory]::CreateDirectory($repo)|Out-Null
                $null=Invoke-EvidenceTestGit $repo @('init','-q');foreach($c in @(@('user.email','fixture@example.invalid'),@('user.name','fixture'),@('core.autocrlf','false'))){$null=Invoke-EvidenceTestGit $repo (@('config')+$c)}
                foreach($n in @('keep.txt','mod.txt','del.txt')){[IO.File]::WriteAllText([IO.Path]::Combine($repo,$n),"base $n`n")};[IO.File]::WriteAllBytes([IO.Path]::Combine($repo,'bin.dat'),[byte[]](0..255))
                $null=Invoke-EvidenceTestGit $repo @('add','-A');$null=Invoke-EvidenceTestGit $repo @('commit','-qm','base');$base=(Invoke-EvidenceTestGit $repo @('rev-parse','HEAD')).Trim()
                [IO.File]::WriteAllText([IO.Path]::Combine($repo,'mod.txt'),"changed`n");[IO.File]::WriteAllText([IO.Path]::Combine($repo,'add.txt'),"added`n");$null=Invoke-EvidenceTestGit $repo @('add','add.txt')
                [IO.File]::Delete([IO.Path]::Combine($repo,'del.txt'));[IO.Directory]::CreateDirectory([IO.Path]::Combine($repo,'new'))|Out-Null;[IO.File]::WriteAllText([IO.Path]::Combine($repo,'new','u.txt'),"untracked`n");[IO.File]::WriteAllBytes([IO.Path]::Combine($repo,'bin.dat'),[byte[]](255..0))
                $indexHash=(Get-FileHash -LiteralPath ([IO.Path]::Combine($repo,'.git','index'))).Hash
                # The README procedure: a temporary index, never the real one.
                $reviewInput=[IO.Path]::Combine($env.Root,'review-input');[IO.Directory]::CreateDirectory($reviewInput)|Out-Null;$patch=[IO.Path]::Combine($reviewInput,'changes.patch')
                $env:GIT_INDEX_FILE=[IO.Path]::Combine($env.Root,'review.index')
                try{$null=Invoke-EvidenceTestGit $repo @('read-tree',$base);$null=Invoke-EvidenceTestGit $repo @('add','-A');$null=Invoke-EvidenceTestGit $repo @('diff','--cached','--binary','--no-color','--no-renames',"--output=$patch",$base)
                    $changed=@(Invoke-EvidenceTestGit $repo @('diff','--cached','--name-only','--no-renames','--diff-filter=d',$base));$deleted=@(Invoke-EvidenceTestGit $repo @('diff','--cached','--name-only','--no-renames','--diff-filter=D',$base))}
                finally{Remove-Item Env:GIT_INDEX_FILE}
                Assert-EvidenceTest ((Get-FileHash -LiteralPath ([IO.Path]::Combine($repo,'.git','index'))).Hash -ceq $indexHash) 'real index touched'
                Assert-EvidenceTest ((($changed|Sort-Object) -join ',') -ceq 'add.txt,bin.dat,mod.txt,new/u.txt' -and ($deleted -join ',') -ceq 'del.txt') ('change set '+($changed -join ',')+' / '+($deleted -join ','))
                [IO.File]::WriteAllText([IO.Path]::Combine($reviewInput,'base.txt'),$base+"`n");foreach($n in $changed){$to=[IO.Path]::Combine($reviewInput,'files',$n);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to))|Out-Null;[IO.File]::Copy([IO.Path]::Combine($repo,$n),$to)}
                Write-EvidenceTestJson $selectionPath ([ordered]@{schemaVersion=2;purpose='evidence';taskId='fixture';root=$reviewInput;files=@(@('base.txt','changes.patch')+@($changed|ForEach-Object{'files/'+$_}));base=$base;head=$base})
                $s=Invoke-EvidenceTestStage $env $selectionPath;$f=Invoke-EvidenceFetch $env.ConfigPath $s.reference $s.packageSha256;Assert-EvidenceTest ($f.status -ceq 'passed') ('local fetch '+$f.reasonCode)
                $fetchedBase=([IO.File]::ReadAllText([IO.Path]::Combine($f.outputPath,'base.txt'))).Trim()
                $baseDir=[IO.Path]::Combine($env.Root,'check-base');$applied=[IO.Path]::Combine($env.Root,'check-applied')
                foreach($dir in @($baseDir,$applied)){& git clone -q -c core.autocrlf=false $repo $dir;if($LASTEXITCODE){throw 'clone'};$null=Invoke-EvidenceTestGit $dir @('checkout','-q',$fetchedBase)}
                $null=Invoke-EvidenceTestGit $applied @('apply','--binary',[IO.Path]::Combine($f.outputPath,'changes.patch'))
                $recorded=Get-EvidenceTestTreeHashes ([IO.Path]::Combine($f.outputPath,'files'));$baseHashes=Get-EvidenceTestTreeHashes $baseDir;$appliedHashes=Get-EvidenceTestTreeHashes $applied
                $problems=Get-EvidenceTestPatchProblems $baseHashes $appliedHashes $recorded $deleted;Assert-EvidenceTest ($problems.Count -eq 0) ('patch mismatch '+($problems -join ','))
                Assert-EvidenceTest ($baseHashes['keep.txt'] -ceq $appliedHashes['keep.txt'] -and -not $recorded.ContainsKey('keep.txt')) 'unchanged file recorded'
                # The comparison must notice a recorded set that forgot one changed file.
                $short=@{}+$recorded;$short.Remove('bin.dat');Assert-EvidenceTest ((Get-EvidenceTestPatchProblems $baseHashes $appliedHashes $short $deleted) -ccontains 'unrecorded-change:bin.dat') 'omission not detected';break
            }
            'remote-fetch-codes-unchanged'{
                # I4: a missing copy of a ready artifact still reports the remote fetch's old generic code.
                $p=Invoke-EvidencePublish $env.ConfigPath $selectionPath;Assert-EvidenceTest ($p.status -ceq 'passed') 'publish'
                [IO.File]::Delete((Read-EvidenceTestTask $env).artifacts[0].ownedCopies[0].path)
                $f=Invoke-EvidenceFetch $env.ConfigPath $p.reference $p.packageSha256;Assert-EvidenceTest ($f.status -ceq 'failed' -and $f.reasonCode -ceq 'unconfirmed') ('remote code '+$f.reasonCode);break
            }
            'stage-catalog-read'{
                # The catalog reader: optional origin, staged without receipt, and stage-origin deletion without receipt.
                $null=Invoke-EvidenceTestStage $env $selectionPath
                Edit-EvidenceTestTask $env {param($t)$t.artifacts[0].state='deleted'};Assert-EvidenceTest ((Read-EvidenceTestTask $env).artifacts[0].state -ceq 'deleted') 'stage deletion unread'
                $rejected=$null;Edit-EvidenceTestTask $env {param($t)$t.artifacts[0].PSObject.Properties.Remove('origin')};try{$null=Read-EvidenceTestTask $env}catch{$rejected=$_.Exception.Message};Assert-EvidenceTest ($rejected -ceq 'receipt-missing') ('origin-less deletion '+$rejected)
                $catalog=[IO.Path]::Combine((Get-EvidenceTaskDirectory $env.Config fixture),'catalog.json');$doc=ConvertFrom-Json ([IO.File]::ReadAllText($catalog)) -DateKind String;$doc.artifacts[0]|Add-Member -NotePropertyName origin -NotePropertyValue 'remote';$doc.artifacts[0].state='staged';Write-EvidenceTestJson $catalog $doc
                $rejected=$null;try{$null=Read-EvidenceTestTask $env}catch{$rejected=$_.Exception.Message};Assert-EvidenceTest ($rejected -ceq 'corrupt-state') ('unknown origin '+$rejected);break
            }
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
