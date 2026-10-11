param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../ContentApplication.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactApplication.psm1')
Import-Module (Join-Path $PSScriptRoot '../ContentTransfer.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactPaths.psm1')
. (Join-Path $PSScriptRoot 'ContentTestSupport.ps1')
# Content公開（退避・照合経路・順序・再実行・排他・予算・確定点）を、注入したtransport/readback/時計と
# 隔離したstore・transfers・ArtifactsRootだけで検査する。実R2との往復は判定Cの操作証拠として扱う。
$registered=@('collect-roundtrip','source-and-root-reject','stage-integrity','object-path','revision-conflict-and-order','resume-after-failure','rerun-priorities','concurrent-stage','concurrent-publish','budget','store-faults','fresh-process-cli')
$selected=@(if($Case -ceq '*'){$registered}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
function Assert([bool]$Condition,[string]$Message){Assert-EvidenceTest $Condition $Message}
function New-Obs($Request,[int]$Status=200,[string]$Class='success',[string]$Code=$null){[pscustomobject]@{Operation=$Request.Operation;Generation=('f'*32);HttpStatus=$Status;StatusClass=$Class;S3Code=$Code;Bytes=$null;Sha256=$null;StatusObserved=$true;TimedOut=$false;Redirected=$false}}
function Get-PutKeys($Fx){@($Fx.Order|Where-Object{$_.StartsWith('put ',[StringComparison]::Ordinal)}|ForEach-Object{$_.Substring(4)})}
function Get-PutCount($Fx,[string]$ObjectKey){@(Get-PutKeys $Fx|Where-Object{$_ -ceq $ObjectKey}).Count}
function Get-RemoteCount($Fx){@([IO.Directory]::EnumerateFiles($Fx.Remote,'*.bin')).Count}
function Collect($Fx,$Src,[string]$Root=''){Invoke-ContentCollect $Fx.Config $Src.Dir $(if($Root){$Root}else{$Fx.ArtifactsRoot})}
function Assert-NoDelete($Fx){Assert ((Get-Count $Fx 'delete') -eq 0 -and @($Fx.Order|Where-Object{$_.StartsWith('delete ',[StringComparison]::Ordinal)}).Count -eq 0) 'DELETE called'}
$savedRoot=[Environment]::GetEnvironmentVariable('OSM_ARTIFACTS_ROOT')
foreach($caseName in $selected){
    $fixture=$null
    try{
        Assert ($caseName -cin $registered) 'unknown-case'
        [Environment]::SetEnvironmentVariable('OSM_ARTIFACTS_ROOT',$null)
        $fixture=New-ContentFixture;$fx=$fixture
        switch -CaseSensitive ($caseName){
            'collect-roundtrip'{
                $src=New-ContentSource $fx;$before=Get-TreeFingerprint $src.Dir;$n=$src.Names.Count+1
                $r=Collect $fx $src
                Assert ($r.status -ceq 'passed' -and $r.reasonCode -ceq 'complete' -and $r.stage -ceq 'created' -and $r.publish -ceq 'published' -and $r.reference -and $r.receiptPath) "collect failed: $($r.reasonCode)"
                Assert ($r.manifestSha256 -ceq $src.ManifestSha -and $r.reference -cne $r.manifestSha256 -and $r.contentSet -ceq 'bs4-spring-full' -and $r.target -ceq 'windows-x64-player' -and $r.revision -ceq $src.Revision) 'identity fields'
                Assert ($r.objects.total -eq $n -and $r.objects.sent -eq $n -and $r.objects.puts -eq $n -and $r.objects.gets -eq 2*$n -and $r.residue.remote -ceq 'verified') "object counts $($r.objects|ConvertTo-Json -Compress)"
                Assert ((Get-TreeFingerprint $src.Dir) -ceq $before) 'source changed'
                Assert ((Get-TreeFingerprint (Get-StageDir $fx)) -ceq $before) 'stage is not an exact copy of the Content output'
                $puts=@(Get-PutKeys $fx);$expectOrder=@($src.Names|ForEach-Object{Get-Key "content/$_"})+@(Get-Key 'transport.json')
                Assert (($puts -join "`n") -ceq ($expectOrder -join "`n")) 'PUT order: content/* in manifest order, transport.json last'
                Assert ($fx.Order[0] -ceq ('readback '+(Get-Key 'transport.json'))) 'first operation is the remote transport.json check'
                foreach($k in $puts){$i=$fx.Order.IndexOf('put '+$k);Assert ($fx.Order[$i+1] -ceq "readback $k") "no readback after PUT of $k";if(-not $k.EndsWith('/transport.json',[StringComparison]::Ordinal)){Assert ($fx.Order[$i-1] -ceq "readback $k") "no check before PUT of $k"}}
                $snapshot=[IO.Path]::Combine($r.residue.localPath,'snapshot')
                Assert (@($fx.Requests|Where-Object{$_.Operation -ceq 'put'}).Count -eq $n -and @($fx.Requests|Where-Object{$_.Operation -ceq 'put' -and -not $_.SourcePath.StartsWith($snapshot+'\',[StringComparison]::OrdinalIgnoreCase)}).Count -eq 0) 'PUT must send the private snapshot, never the ArtifactsRoot file'
                $dirs=@($fx.ReadDirs);Assert ($dirs.Count -eq 2*$n -and @($dirs|Select-Object -Unique).Count -eq $dirs.Count) 'one child directory per readback'
                foreach($d in $dirs){Assert ([IO.Path]::GetDirectoryName($d) -ceq $r.residue.localPath -and [IO.Path]::GetFileName($d) -cmatch '\A[0-9a-f]{32}\z' -and [IO.File]::Exists([IO.Path]::Combine($d,'operation.lock'))) 'readback directory shape';Assert-ArtifactOperationFile $d ([IO.Path]::Combine($d,'readback-x.json'))}
                $dir=Get-StoreDir $fx;$intent=[IO.File]::ReadAllText([IO.Path]::Combine($dir,'intent.json'));$receipt=[IO.File]::ReadAllText($r.receiptPath)
                foreach($text in @($intent,$receipt,(ConvertTo-Json -InputObject $r -Compress -Depth 8))){Assert (-not $text.Contains('r2.cloudflarestorage') -and -not $text.Contains('e'*32+'.')) 'endpoint exposed'}
                Assert (-not $intent.Contains($r.residue.localPath) -and -not $intent.Contains('publishedAt')) 'intent must stay deterministic'
                $rc=$receipt|ConvertFrom-Json -DateKind String;$declared=[long](@(Get-ChildItem -LiteralPath (Join-Path (Get-StageDir $fx) 'content') -File)|Measure-Object Length -Sum).Sum
                Assert ($rc.manifestSha256 -ceq $src.ManifestSha -and $rc.objectCount -eq $n -and $rc.totalBytes -eq $declared -and $rc.intentSha256 -ceq (Get-FileHash -LiteralPath ([IO.Path]::Combine($dir,'intent.json'))).Hash.ToLowerInvariant() -and [DateTimeOffset]::Parse($rc.publishedAt).Offset -eq [TimeSpan]::Zero) 'receipt fields'
                $i=Invoke-ContentInspect $fx.Config 'bs4-spring-full' 'windows-x64-player' $src.Revision
                Assert ($i.status -ceq 'passed' -and $i.reference -ceq $r.reference -and $i.manifestSha256 -ceq $src.ManifestSha) 'inspect'
                Assert ((Get-RemoteCount $fx) -eq $n) 'remote object count';Assert-NoDelete $fx
            }
            'source-and-root-reject'{
                $src=New-ContentSource $fx;$root=$fx.ArtifactsRoot
                $bak=[IO.Path]::Combine($fx.Root,'bak');Copy-Item -LiteralPath $src.Dir -Destination $bak -Recurse
                function Restore-Source{Remove-Item -LiteralPath $src.Dir -Recurse -Force;Copy-Item -LiteralPath $bak -Destination $src.Dir -Recurse}
                $first=[IO.Path]::Combine($src.Dir,'content',$src.Names[0])
                $mutations=[ordered]@{
                    'missing file'=@{code='content-mismatch';do={[IO.File]::Delete($first)}}
                    'extra file'=@{code='content-mismatch';do={[IO.File]::WriteAllText([IO.Path]::Combine($src.Dir,'content','extra.cf'),'extra')}}
                    'hash mismatch'=@{code='content-mismatch';do={$b=[IO.File]::ReadAllBytes($first);$b[0]=$b[0] -bxor 1;[IO.File]::WriteAllBytes($first,$b)}}
                    'size mismatch'=@{code='content-mismatch';do={[IO.File]::AppendAllText($first,'x')}}
                    'extra top-level file'=@{code='content-mismatch';do={[IO.File]::WriteAllText([IO.Path]::Combine($src.Dir,'stray.txt'),'x')}}
                    'reparse in content'=@{code='invalid-source';do={$out=[IO.Path]::Combine($fx.Root,'outside');[IO.Directory]::CreateDirectory($out)|Out-Null;$null=New-Item -ItemType Junction -Path ([IO.Path]::Combine($src.Dir,'content','link')) -Target $out}}
                }
                foreach($name in $mutations.Keys){Restore-Source;& $mutations[$name].do;Assert-Failed (Collect $fx $src) $mutations[$name].code;if($name -ceq 'reparse in content'){Remove-Item -LiteralPath ([IO.Path]::Combine($src.Dir,'content','link')) -Force}}
                Restore-Source
                $manifest=Get-Content -LiteralPath ([IO.Path]::Combine($src.Dir,'transport.json')) -Raw|ConvertFrom-Json -DateKind String
                $variants=[ordered]@{
                    'zero byte'=@{code='unsupported-content';files=@($manifest.files|ForEach-Object{[ordered]@{path=$_.path;size=0;sha256=$_.sha256}})}
                    'path escape'=@{code='invalid-content-manifest';files=@([ordered]@{path='../x.cf';size=1;sha256=('a'*64)})}
                    'reserved name'=@{code='invalid-content-manifest';files=@([ordered]@{path='CON.cf';size=1;sha256=('a'*64)})}
                }
                $n=0
                foreach($name in $variants.Keys){$n++;$rev="2026092$n"+'T202227427Z-'+("$n"*32);$s=New-ContentSource $fx $rev @{files=$variants[$name].files};Assert-Failed (Collect $fx $s) $variants[$name].code}
                $s=New-ContentSource $fx '20260925T202227427Z-11111111111111111111111111111111' @{revision='20260101T000000000Z-ffffffffffffffffffffffffffffffff'};Assert-Failed (Collect $fx $s) 'invalid-content-identity'
                $s=New-ContentSource $fx '20260926T202227427Z-22222222222222222222222222222222' @{target='Android'};Assert-Failed (Collect $fx $s) 'unsupported-series'
                Assert-Failed (Invoke-ContentCollect $fx.Config 'relative\source' $root) 'invalid-source'
                Assert-Failed (Invoke-ContentCollect $fx.Config ([IO.Path]::Combine($fx.Root,'absent')) $root) 'invalid-source'
                Assert-Failed (Invoke-ContentCollect ([IO.Path]::Combine($fx.Root,'absent.json')) $src.Dir $root) 'invalid-content-config'
                $ops=@([IO.Directory]::EnumerateDirectories($fx.Owned[0])).Count
                Assert-Failed (Invoke-ContentCollect $fx.Config $src.Dir '') 'artifacts-root-not-configured'
                $junction=[IO.Path]::Combine($fx.Root,'junction-root');$null=New-Item -ItemType Junction -Path $junction -Target ([IO.Path]::Combine($fx.Root,'outside'))
                try{foreach($bad in @('relative\root',[IO.Path]::Combine($fx.Root,'absent'),$junction,[IO.Path]::Combine($src.Dir,'content'),$fx.Owned[0],$fx.Owned[1],$fx.Root)){Assert-Failed (Collect $fx $src $bad) 'invalid-artifacts-root'}}finally{Remove-Item -LiteralPath $junction -Force}
                Assert (@([IO.Directory]::EnumerateDirectories($fx.Owned[0])).Count -eq $ops) 'rejected root created an operation'
                Assert (-not [IO.Directory]::Exists([IO.Path]::Combine($root,'content')) -and $fx.Order.Count -eq 0 -and -not [IO.Directory]::Exists($fx.Owned[1])) 'rejected input was staged, sent or recorded'
            }
            'stage-integrity'{
                $src=New-ContentSource $fx;$dest=Get-StageDir $fx;$parent=[IO.Path]::GetDirectoryName($dest);$before=Get-TreeFingerprint $src.Dir
                $foreign=[IO.Path]::Combine($parent,'.'+$src.Revision+'.'+('9'*32)+'.staging');[IO.Directory]::CreateDirectory($foreign)|Out-Null;[IO.File]::WriteAllText([IO.Path]::Combine($foreign,'sentinel.txt'),'other')
                $first=[IO.Path]::Combine($src.Dir,'content',$src.Names[0]);$saved=[IO.File]::ReadAllBytes($first)
                Set-EvidenceTestValue BuildStage StageHook ({param($phase,$staging)if($phase -ceq 'before-copy'){[IO.File]::WriteAllText($first,'swapped')}}.GetNewClosure())
                Assert-Failed (Collect $fx $src) 'stage-hash-mismatch';[IO.File]::WriteAllBytes($first,$saved)
                Set-EvidenceTestValue BuildStage StageHook {param($phase,$staging)if($phase -ceq 'before-verify'){[IO.File]::WriteAllText([IO.Path]::Combine($staging,'transport.json'),'tampered')}}
                Assert-Failed (Collect $fx $src) 'stage-hash-mismatch';Set-EvidenceTestValue BuildStage StageHook $null
                $left=@([IO.Directory]::EnumerateDirectories($parent)|ForEach-Object{[IO.Path]::GetFileName($_)})
                Assert ($left.Count -eq 1 -and $left[0] -ceq [IO.Path]::GetFileName($foreign) -and [IO.File]::Exists([IO.Path]::Combine($foreign,'sentinel.txt'))) 'own staging left or foreign staging touched'
                [IO.Directory]::CreateDirectory($dest)|Out-Null;[IO.File]::WriteAllText([IO.Path]::Combine($dest,'different.cf'),'different');$conflict=Get-TreeFingerprint $dest
                Assert-Failed (Collect $fx $src) 'stage-conflict'
                Assert ((Get-TreeFingerprint $dest) -ceq $conflict -and (Get-TreeFingerprint $src.Dir) -ceq $before -and $fx.Order.Count -eq 0) 'conflicting stage changed, source changed or something was sent'
            }
            'object-path'{
                $src=New-ContentSource $fx;$keys=@($src.Names|ForEach-Object{Get-Key "content/$_"});$n=$src.Names.Count+1
                # (a) 一致する既存objectは送らない。404のものだけPUTする。
                foreach($i in 0,2){Set-Remote $fx $keys[$i] ([IO.File]::ReadAllBytes([IO.Path]::Combine($src.Dir,'content',$src.Names[$i])))}
                $r=Collect $fx $src;Assert ($r.status -ceq 'passed' -and $r.objects.sent -eq $n-2 -and $r.objects.verified -eq 2 -and $r.objects.puts -eq $n-2) "matched objects must not be resent: $($r.objects|ConvertTo-Json -Compress)"
                Assert ((Get-PutCount $fx $keys[0]) -eq 0 -and (Get-PutCount $fx $keys[2]) -eq 0) 'matched object was sent'
                # (b)〜(g) 条件ごとに新しいrevisionで試す。
                $cases=[ordered]@{
                    'mismatch'=@{code='object-conflict';setup={param($fx,$k)Set-Remote $fx $k ([byte[]](1,2,3))};hook=$null}
                    'forbidden precheck'=@{code='object-unconfirmed';hook={param($fx,$k)$fx.OnReadback={param($key)if($key -ceq $k){[pscustomobject]@{operation='readback';generation=('f'*32);key=$key;bytes=$null;sha256=$null;childExit=1;httpStatus=403;statusClass='forbidden';s3Code='AccessDenied';statusObserved=$true;timedOut=$false;redirected=$false;childStatus='failed';observedAt=[DateTimeOffset]::UtcNow.ToString('o')}}}.GetNewClosure()}}
                    'readback exception'=@{code='object-unconfirmed';hook={param($fx,$k)$fx.OnReadback={param($key)if($key -ceq $k){throw 'Readback child unconfirmed. SECRET-detail'}}.GetNewClosure()}}
                    'PUT response lost'=@{code='put-unconfirmed';hook={param($fx,$k)$fx.OnTransport={param($req)if($req.Operation -ceq 'put' -and $req.Key -ceq $k){throw 'secret SECRET-token https://eeeeeeee.r2.cloudflarestorage.com'}}.GetNewClosure()}}
                    'snapshot changed before PUT'=@{code='snapshot-changed';hook={param($fx,$k)$fx.OnReadback={param($key)if($key -ceq $k){[IO.File]::WriteAllText([IO.Path]::Combine([IO.Path]::GetDirectoryName($fx.ReadDirs[$fx.ReadDirs.Count-1]),'snapshot','content',$k.Substring($k.LastIndexOf('/')+1)),'swapped')};$null}.GetNewClosure()}}
                    'PUT 500'=@{code='put-unconfirmed';hook={param($fx,$k)$fx.OnTransport={param($req)if($req.Operation -ceq 'put' -and $req.Key -ceq $k){New-Obs $req 500 'server-error'}}.GetNewClosure()}}
                    'readback after PUT differs'=@{code='readback-unconfirmed';hook={param($fx,$k)$fx.OnReadback={param($key)if($key -ceq $k -and (Get-PutCount $fx $k) -gt 0){Get-ReadResult ('f'*32) $key ([byte[]](9,9)) 1 ('0'*64)}}.GetNewClosure()}}
                }
                $idx=0
                foreach($name in $cases.Keys){
                    $idx++;$rev="2026091$idx"+'T202227427Z-'+("$idx"*32);$s=New-ContentSource $fx $rev @{} 6 "s$idx";Clear-Order $fx;$fx.OnReadback=$null;$fx.OnTransport=$null
                    $k=Get-Key "content/$($s.Names[2])" $rev;$later=Get-Key "content/$($s.Names[3])" $rev;$tj=Get-Key 'transport.json' $rev
                    if($cases[$name].Contains('setup')){& $cases[$name].setup $fx $k}
                    if($cases[$name].hook){& $cases[$name].hook $fx $k}
                    $res=Collect $fx $s;Assert-Failed $res $cases[$name].code
                    Assert ((Get-PutCount $fx $k) -le 1 -and (Get-PutCount $fx $later) -eq 0 -and (Get-PutCount $fx $tj) -eq 0) "$name : retried the key or went on after stopping"
                    Assert (-not [IO.File]::Exists([IO.Path]::Combine((Get-StoreDir $fx $rev),'receipt.json'))) "$name : receipt written"
                    $json=ConvertTo-Json -InputObject $res -Compress -Depth 8;Assert (-not $json.Contains('SECRET') -and -not $json.Contains('r2.cloudflarestorage')) "$name : exception text leaked"
                    if($name -ceq 'snapshot changed before PUT'){Assert ((Get-PutCount $fx $k) -eq 0 -and $null -eq (Get-Remote $fx $k)) 'sent a file that no longer matched the manifest'}
                    if($name -ceq 'mismatch'){Assert ((Get-Remote $fx $k).Length -eq 3 -and (Get-PutCount $fx $k) -eq 0) 'mismatching object was overwritten'}
                    if($name -in @('forbidden precheck','readback exception')){Assert ((Get-PutCount $fx $k) -eq 0 -and $null -eq (Get-Remote $fx $k)) "$name : sent an unconfirmed object"}
                    Assert-NoDelete $fx
                }
            }
            'revision-conflict-and-order'{
                $src=New-ContentSource $fx;$tj=Get-Key 'transport.json'
                Set-Remote $fx $tj ([Text.Encoding]::UTF8.GetBytes('{"another":"manifest"}'))
                $r=Collect $fx $src;Assert-Failed $r 'revision-conflict'
                Assert (@(Get-PutKeys $fx).Count -eq 0 -and (Get-RemoteCount $fx) -eq 1 -and -not [IO.File]::Exists([IO.Path]::Combine((Get-StoreDir $fx),'intent.json'))) 'revision-conflict must send nothing and fix no intent'
                Assert ((Get-Remote $fx $tj).Length -eq 22) 'remote transport.json was overwritten'
                # transport.jsonは、全content/*の照合が通った後にだけ置く。途中で止まれば置かない。
                $s2=New-ContentSource $fx '20260927T202227427Z-33333333333333333333333333333333' @{} 6 'z';Clear-Order $fx;$third=Get-Key "content/$($s2.Names[2])" $s2.Revision
                $fx.OnTransport={param($req)if($req.Operation -ceq 'put' -and $req.Key -ceq $third){throw 'lost'}}.GetNewClosure()
                Assert-Failed (Collect $fx $s2) 'put-unconfirmed'
                Assert ((Get-PutCount $fx (Get-Key 'transport.json' $s2.Revision)) -eq 0 -and $null -eq (Get-Remote $fx (Get-Key 'transport.json' $s2.Revision))) 'transport.json placed before every content object was verified'
            }
            'resume-after-failure'{
                $src=New-ContentSource $fx;$n=$src.Names.Count+1;$fourth=Get-Key "content/$($src.Names[3])"
                # 4件目は保存されたがPUTの応答だけ失われる。同じrunの中では再送しない。
                $fx.OnTransport={param($req)if($req.Operation -ceq 'put' -and $req.Key -ceq $fourth){[IO.File]::WriteAllBytes((Get-RemotePath $fx.Remote $req.Key),[IO.File]::ReadAllBytes($req.SourcePath));throw 'response lost'}}.GetNewClosure()
                $a=Collect $fx $src;Assert-Failed $a 'put-unconfirmed';Assert ((Get-PutCount $fx $fourth) -eq 1 -and @(Get-PutKeys $fx).Count -eq 4 -and $a.residue.remote -ceq 'may-have-been-sent') 'first run did not stop at the unknown PUT'
                $intentHash=(Get-FileHash -LiteralPath ([IO.Path]::Combine((Get-StoreDir $fx),'intent.json'))).Hash;$fx.OnTransport=$null
                $b=Collect $fx $src
                Assert ($b.status -ceq 'passed' -and $b.stage -ceq 'existing' -and $b.publish -ceq 'published') "resume failed: $($b.reasonCode)"
                Assert ((Get-PutCount $fx $fourth) -eq 1 -and @(Get-PutKeys $fx).Count -eq $n -and $b.objects.verified -eq 4 -and $b.objects.sent -eq $n-4) 'resume resent a verified object or skipped a missing one'
                $unique=@(Get-PutKeys $fx|Select-Object -Unique);Assert ($unique.Count -eq $n) 'an object was sent twice across runs'
                Assert ((Get-FileHash -LiteralPath ([IO.Path]::Combine((Get-StoreDir $fx),'intent.json'))).Hash -ceq $intentHash) 'intent changed by resume'
                Assert-NoDelete $fx
            }
            'rerun-priorities'{
                $src=New-ContentSource $fx;$root=$fx.ArtifactsRoot;$a=Collect $fx $src;Assert ($a.status -ceq 'passed') 'first publish'
                Clear-Order $fx
                $b=Collect $fx $src;Assert ($b.status -ceq 'passed' -and $b.publish -ceq 'existing' -and $b.reference -ceq $a.reference -and $b.manifestSha256 -ceq $a.manifestSha256 -and $fx.Order.Count -eq 0) 'rerun with receipt must return existing without any network'
                $root2=[IO.Path]::Combine($fx.Root,'artifacts-root-2');[IO.Directory]::CreateDirectory($root2)|Out-Null
                $c=Collect $fx $src $root2;Assert ($c.status -ceq 'passed' -and $c.publish -ceq 'existing' -and $c.stage -ceq 'created' -and $c.reference -ceq $a.reference -and $fx.Order.Count -eq 0) 'other ArtifactsRoot must give the same decision'
                # (1) 退避先に違う内容があればstage-conflictが最優先。
                $stageFile=[IO.Path]::Combine((Get-StageDir $fx),'content',$src.Names[0]);$saved=[IO.File]::ReadAllBytes($stageFile);[IO.File]::WriteAllText($stageFile,'tampered')
                Assert-Failed (Collect $fx $src) 'stage-conflict';[IO.File]::WriteAllBytes($stageFile,$saved);Assert ($fx.Order.Count -eq 0) 'stage-conflict touched the network'
                # (3) 同じrevisionで別内容はintent-conflict（receiptがあっても成功にしない）。
                $other=@{} + $fx;$other.Repo=[IO.Path]::Combine($fx.Root,'repo2');$diff=New-ContentSource $other $src.Revision @{} 6 'different'
                $root3=[IO.Path]::Combine($fx.Root,'artifacts-root-3');[IO.Directory]::CreateDirectory($root3)|Out-Null
                Assert-Failed (Collect $fx $diff $root3) 'intent-conflict';Assert ($fx.Order.Count -eq 0) 'intent-conflict touched the network'
                # intentだけが残る（receipt無し）状態でも、別内容は同じ判定。
                $rev2='20260928T202227427Z-44444444444444444444444444444444';$s2=New-ContentSource $fx $rev2 @{} 6 'q';$second=Get-Key "content/$($s2.Names[1])" $rev2
                $fx.OnTransport={param($req)if($req.Operation -ceq 'put' -and $req.Key -ceq $second){throw 'lost'}}.GetNewClosure();Assert-Failed (Collect $fx $s2) 'put-unconfirmed';$fx.OnTransport=$null;Clear-Order $fx
                $d2=New-ContentSource $other $rev2 @{} 6 'r';Assert-Failed (Collect $fx $d2 $root3) 'intent-conflict';Assert ($fx.Order.Count -eq 0) 'intent-conflict (no receipt) touched the network'
                # (4) intentは同じで遠隔のtransport.jsonだけ違えばrevision-conflict。
                Set-Remote $fx (Get-Key 'transport.json' $rev2) ([byte[]](1,2,3,4));Clear-Order $fx
                Assert-Failed (Collect $fx $s2) 'revision-conflict';Assert (@(Get-PutKeys $fx).Count -eq 0) 'revision-conflict sent something'
            }
            'concurrent-stage'{
                $src=New-ContentSource $fx;$n=$src.Names.Count+1;$names=@(1..2|ForEach-Object{'osm-content-'+[Guid]::NewGuid().ToString('N')});$goName='osm-content-'+[Guid]::NewGuid().ToString('N')
                $readies=@($names|ForEach-Object{[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,$_)});$go=[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,$goName)
                try{
                    $jobs=@($names|ForEach-Object{Invoke-Child (Get-ChildArgs $fx 'collect' @{Source=$src.Dir;ArtifactsRoot=$fx.ArtifactsRoot;StageReadyEvent=$_;StageGoEvent=$goName})})
                    foreach($e in $readies){Assert ($e.WaitOne(120000)) 'child did not reach the stage rename'}
                    $null=$go.Set();$out=@($jobs|ForEach-Object{Finish-Child $_})
                }finally{$go.Dispose();foreach($e in $readies){$e.Dispose()}}
                $vals=@($out|ForEach-Object{$_.Output.Trim()|ConvertFrom-Json -Depth 8 -DateKind String})
                Assert (@($vals|Where-Object{$_.status -ceq 'passed'}).Count -ge 1) "nobody published: $($out|ForEach-Object{$_.Output+$_.Error})"
                foreach($v in $vals){Assert ($v.status -ceq 'passed' -or $v.reasonCode -ceq 'publish-in-progress') "rename loser returned $($v.reasonCode)"}
                Assert (@([IO.Directory]::EnumerateFiles($fx.Remote,'put-attempt-*.txt')).Count -eq $n) 'objects were sent more than once'
                Assert (@([IO.Directory]::EnumerateDirectories([IO.Path]::GetDirectoryName((Get-StageDir $fx)))).Count -eq 1) 'loser staging left'
            }
            'concurrent-publish'{
                $src=New-ContentSource $fx;$readyName='osm-content-'+[Guid]::NewGuid().ToString('N');$goName='osm-content-'+[Guid]::NewGuid().ToString('N')
                $ready=[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,$readyName);$go=[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,$goName)
                try{
                    $job=Invoke-Child (Get-ChildArgs $fx 'collect' @{Source=$src.Dir;ArtifactsRoot=$fx.ArtifactsRoot;ReadyEvent=$readyName;GoEvent=$goName})
                    # 子が最初のPUTの直前（lock保持中）に達したことをsignalで確認してから、同じrevisionを公開する。
                    Assert ($ready.WaitOne(120000)) 'child did not reach the first PUT'
                    $lost=Collect $fx $src;Assert-Failed $lost 'publish-in-progress'
                    Assert ($fx.Order.Count -eq 0 -and $null -eq $lost.objects) 'the losing side touched the network'
                    $null=$go.Set();$done=Finish-Child $job
                }finally{$go.Dispose();$ready.Dispose()}
                Assert ($done.Exit -eq 0 -and ($done.Output.Trim()|ConvertFrom-Json).status -ceq 'passed') "winner failed: $($done.Output) $($done.Error)"
                Assert (@([IO.Directory]::EnumerateFiles($fx.Remote,'put-attempt-*.txt')).Count -eq $src.Names.Count+1) 'objects were sent more than once'
                $after=Collect $fx $src;Assert ($after.status -ceq 'passed' -and $after.publish -ceq 'existing' -and $after.reference -ceq (($done.Output.Trim()|ConvertFrom-Json).reference)) 'lock was not released or the loser found another result'
            }
            'budget'{
                $m=@(Get-EvidenceTestModules ContentTransfer)[0]
                Assert ((& $m {Get-ContentBudgetMs 'publish' 221}) -eq 2068000 -and (& $m {Get-ContentBudgetMs 'fetch' 221}) -eq 742000 -and (& $m {Get-ContentBudgetMs 'publish' 1}) -eq 308000 -and (& $m {Get-ContentBudgetMs 'publish' 881}) -eq 7348000) 'budget formula'
                $src=New-ContentSource $fx '20260924T202227427Z-0123456789abcdef0123456789abcdef' @{} 3;$n=$src.Names.Count+1;$limit=(300+8*$n)*1000
                # 時計を進めるのはPUTの観測だけ。予算超過後は次のobjectへ進まず、成功にしない。
                $fx.OnTransport={param($req)if($req.Operation -ceq 'put'){$fx.Now+=$limit/2+1000};$null}.GetNewClosure()
                $r=Collect $fx $src;Assert-Failed $r 'budget-exceeded'
                Assert (@(Get-PutKeys $fx).Count -eq 2 -and -not [IO.File]::Exists([IO.Path]::Combine((Get-StoreDir $fx),'receipt.json')) -and (Get-PutCount $fx (Get-Key 'transport.json')) -eq 0) 'run continued after the budget expired'
                foreach($owner in @(Get-EvidenceTestModules ArtifactApplication)){Assert ($null -eq (& $owner {$script:Budget})) 'budget was not restored'}
                # 実行中の操作のdeadlineも、残り予算を超えない。
                # (a) 全objectの転送後・receipt確定の直前に予算が尽きたら、receiptを作らず成功にしない。再実行は照合だけで完了する。
                $fx.OnTransport=$null;$fx.Now=0;$s3=New-ContentSource $fx '20260930T202227427Z-77777777777777777777777777777777' @{} 3;$lastKey=Get-Key 'transport.json' $s3.Revision;$n3=$s3.Names.Count+1;Clear-Order $fx
                $fx.OnReadback={param($key)if($key -ceq $lastKey -and (Get-PutCount $fx $lastKey) -gt 0){$fx.Now=$limit+1};$null}.GetNewClosure()
                $late=Collect $fx $s3;Assert-Failed $late 'budget-exceeded'
                Assert (-not [IO.File]::Exists([IO.Path]::Combine((Get-StoreDir $fx $s3.Revision),'receipt.json')) -and @(Get-PutKeys $fx).Count -eq $n3) "receipt=$([IO.File]::Exists([IO.Path]::Combine((Get-StoreDir $fx $s3.Revision),'receipt.json'))) puts=$(@(Get-PutKeys $fx).Count) n3=$n3"
                $fx.Now=0;$fx.OnReadback=$null;$fx.OnTransport=$null;$putsBefore=@(Get-PutKeys $fx).Count
                $again=Collect $fx $s3;Assert ($again.status -ceq 'passed' -and @(Get-PutKeys $fx).Count -eq $putsBefore -and $again.objects.verified -eq $n3) 'rerun must complete by verification without resending'
                $fx.Now=0;$fx.OnTransport=$null;$fx.OnReadback={param($key)if($fx.Requests.Count -eq 0 -and $fx.Now -eq 0){$fx.Now=$limit-100000};$null}.GetNewClosure()
                $s2=New-ContentSource $fx '20260929T202227427Z-55555555555555555555555555555555' @{} 3;Clear-Order $fx
                $ok=Collect $fx $s2;Assert ($ok.status -ceq 'passed') "budgeted run failed: $($ok.reasonCode)"
                $first=@($fx.Requests|Where-Object{$_.Operation -ceq 'put'})[0];Assert ($first.DeadlineMilliseconds -le 100000 -and $first.DeadlineMilliseconds -ge 1) "PUT deadline $($first.DeadlineMilliseconds) exceeds the remaining budget"
            }
            'store-faults'{
                $src=New-ContentSource $fx;$n=$src.Names.Count+1;$dir=Get-StoreDir $fx
                # 最初の送信前のintent書込みに失敗したら何も送らない。
                Set-EvidenceTestValue ContentStore FaultHook {param($phase,$value)if($phase -ceq 'before-intent'){throw 'boom'}}
                Assert-Failed (Collect $fx $src) $null;Assert (@(Get-PutKeys $fx).Count -eq 0 -and -not [IO.File]::Exists([IO.Path]::Combine($dir,'intent.json'))) 'sent before the intent was fixed'
                # receiptの確定前に落ちたら成功を返さない。再実行はPUT 0で完了する。
                Set-EvidenceTestValue ContentStore FaultHook {param($phase,$value)if($phase -ceq 'before-receipt'){throw 'boom'}}
                $a=Collect $fx $src;Assert-Failed $a $null;Assert ($a.objects.puts -eq $n -and -not [IO.File]::Exists([IO.Path]::Combine($dir,'receipt.json'))) 'receipt state'
                $i=Invoke-ContentInspect $fx.Config 'bs4-spring-full' 'windows-x64-player' $src.Revision;Assert ($i.status -ceq 'failed' -and $null -eq (Get-Member -InputObject $i -Name reference -MemberType NoteProperty)) 'inspect succeeded without a receipt'
                Set-EvidenceTestValue ContentStore FaultHook $null;$puts=@(Get-PutKeys $fx).Count
                $b=Collect $fx $src;Assert ($b.status -ceq 'passed' -and @(Get-PutKeys $fx).Count -eq $puts -and $b.objects.verified -eq $n -and $b.objects.sent -eq 0) 'rerun after the receipt failure resent objects'
                # receipt rename後の失敗はcommit-unconfirmed。正本は残り、inspectとrerunで同じ参照になる。
                $s2=New-ContentSource $fx '20260930T202227427Z-66666666666666666666666666666666' @{} 3 'w'
                Set-EvidenceTestValue ContentStore FaultHook {param($phase,$value)if($phase -ceq 'after-receipt'){throw 'after-rename-unconfirmed'}}
                $c=Collect $fx $s2;Assert-Failed $c 'commit-unconfirmed';Set-EvidenceTestValue ContentStore FaultHook $null
                $j=Invoke-ContentInspect $fx.Config 'bs4-spring-full' 'windows-x64-player' $s2.Revision;$again=Collect $fx $s2
                Assert ($j.status -ceq 'passed' -and $again.publish -ceq 'existing' -and $again.reference -ceq $j.reference) 'recovery after commit-unconfirmed'
                # 破損・欠落したreceiptは成功にしない。
                $receiptPath=[IO.Path]::Combine($dir,'receipt.json');$good=[IO.File]::ReadAllBytes($receiptPath)
                foreach($corrupt in @([byte[]](0x7B),[Text.Encoding]::UTF8.GetBytes(([Text.Encoding]::UTF8.GetString($good)).Replace($src.ManifestSha,('0'*64))),[Text.Encoding]::UTF8.GetBytes(([Text.Encoding]::UTF8.GetString($good)).Replace('"succeeded"','"failed"')))){
                    [IO.File]::WriteAllBytes($receiptPath,$corrupt);Set-ArtifactAcl $receiptPath $false
                    Assert ((Invoke-ContentInspect $fx.Config 'bs4-spring-full' 'windows-x64-player' $src.Revision).status -ceq 'failed') 'corrupt receipt accepted by inspect'
                    $bad=Collect $fx $src;Assert-Failed $bad $null;Assert ($bad.reasonCode -cin @('receipt-invalid','unconfirmed')) "corrupt receipt gave $($bad.reasonCode)"
                }
                [IO.File]::WriteAllBytes($receiptPath,$good);Set-ArtifactAcl $receiptPath $false;Assert ((Invoke-ContentInspect $fx.Config 'bs4-spring-full' 'windows-x64-player' $src.Revision).status -ceq 'passed') 'restored receipt'
                [IO.File]::Move([IO.Path]::Combine($dir,'intent.json'),[IO.Path]::Combine($dir,'intent.moved'))
                Assert ((Invoke-ContentInspect $fx.Config 'bs4-spring-full' 'windows-x64-player' $src.Revision).status -ceq 'failed') 'receipt without intent accepted'
            }
            'fresh-process-cli'{
                $src=New-ContentSource $fx;$n=$src.Names.Count+1
                $c=Finish-Child (Invoke-Child (Get-ChildArgs $fx 'collect' @{Source=$src.Dir;ArtifactsRoot=$fx.ArtifactsRoot}))
                Assert ($c.Exit -eq 0) "fresh collect failed: $($c.Output) $($c.Error)"
                $v=$c.Output.Trim()|ConvertFrom-Json -Depth 8 -DateKind String;Assert ($v.status -ceq 'passed' -and $v.reference -and $v.manifestSha256 -ceq $src.ManifestSha -and $v.stage -ceq 'created') 'fresh collect identity'
                $w=Finish-Child (Invoke-Child (Get-ChildArgs $fx 'collect' @{Source=$src.Dir;ArtifactsRoot=$fx.ArtifactsRoot})) | ForEach-Object{$_.Output.Trim()|ConvertFrom-Json -Depth 8 -DateKind String}
                Assert ($w.publish -ceq 'existing' -and $w.reference -ceq $v.reference -and @([IO.Directory]::EnumerateFiles($fx.Remote,'put-attempt-*.txt')).Count -eq $n) 'fresh rerun sent again'
                $i=Finish-Child (Invoke-Child (Get-ChildArgs $fx 'inspect' @{ContentSet='bs4-spring-full';Target='windows-x64-player';Revision=$src.Revision}))
                Assert ($i.Exit -eq 0 -and ($i.Output.Trim()|ConvertFrom-Json).reference -ceq $v.reference) 'fresh inspect'
                $none=Finish-Child (Invoke-Child (Get-ChildArgs $fx 'collect' @{Source=$src.Dir}))
                Assert ($none.Exit -eq 1 -and ($none.Output.Trim()|ConvertFrom-Json).reasonCode -ceq 'artifacts-root-not-configured') 'unconfigured fresh CLI'
                $cli=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts.ps1'));$cfg=$fx.Config;$d=$src.Dir
                $bad=@(
                    @('content','collect','--profile','osm','--config',$cfg),
                    @('content','collect','--profile','osm','--config',$cfg,'--source',$d,'--latest','yes'),
                    @('content','collect','--profile','osm','--source',$d,'--artifacts-root',$fx.ArtifactsRoot),
                    @('content','collect','--profile','osm','--config',$cfg,'--source',$d,'--source',$d),
                    @('content','collect','--profile','other','--config',$cfg,'--source',$d),
                    @('content','publish','--profile','osm','--config',$cfg,'--source',$d),
                    @('content','inspect','--profile','osm','--config',$cfg,'--content-set','a','--target','windows-x64-player'),
                    @('content','fetch','--profile','osm','--config',$cfg,'--reference','osm-content-v1:x'),
                    @('content','fetch','--profile','osm','--config',$cfg,'--reference','osm-content-v1:x','--sha256',('a'*64),'--extra','1'),
                    @('content','collect','--profile','osm','--config',$cfg,'--source')
                )
                foreach($args2 in $bad){$g=Finish-Child (Invoke-Child (@('-NoProfile','-File',$cli)+$args2));Assert ($g.Exit -eq 1 -and $g.Output.Contains('unsupported-command')) "content grammar accepted: $($args2 -join ' ')"}
            }
        }
    }catch{$failed.Add($caseName+': '+$_.Exception.GetType().Name+': '+$_.Exception.Message+' (line '+$_.InvocationInfo.ScriptLineNumber+')')}
    finally{$executed.Add($caseName);[Environment]::SetEnvironmentVariable('OSM_ARTIFACTS_ROOT',$savedRoot);Clear-ContentFixture $fixture}
}
if($selected.Count -eq 0 -or $selected.Count -ne $executed.Count){$failed.Add('selection')}
if($ResultPath){Write-EvidenceTestJson $ResultPath ([ordered]@{registered=$registered;selected=$selected;executed=@($executed);failed=@($failed);binaries=@(Get-EvidenceTestBinaries)})}
foreach($item in $failed){[Console]::Error.WriteLine($item)}
[Console]::WriteLine("ContentPublish $($executed.Count), failed $($failed.Count)")
if($failed.Count){exit 1}
