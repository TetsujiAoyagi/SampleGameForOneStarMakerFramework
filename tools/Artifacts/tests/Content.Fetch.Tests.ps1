param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../ContentApplication.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactApplication.psm1')
Import-Module (Join-Path $PSScriptRoot '../ContentContract.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactPaths.psm1')
. (Join-Path $PSScriptRoot 'ContentTestSupport.ps1')
# Content取得（期待hashの独立性・file単位の照合・集合の完全一致・新規private展開先）を検査する。
# 取得物は偽storeから落とすだけで、実行しない。実R2からの取得は判定Cの操作証拠として扱う。
$registered=@('fetch-roundtrip','fetch-fresh-process','expected-hash-and-reference','fetch-integrity','fetch-budget-and-errors')
$selected=@(if($Case -ceq '*'){$registered}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
function Assert([bool]$Condition,[string]$Message){Assert-EvidenceTest $Condition $Message}
function Publish-Source($Fx,$Src){$r=Invoke-ContentCollect $Fx.Config $Src.Dir $Fx.ArtifactsRoot;Assert ($r.status -ceq 'passed') "publish failed: $($r.reasonCode)";Clear-Order $Fx;return $r}
function Fetch($Fx,$Published,[string]$Sha=''){Invoke-ContentFetch $Fx.Config $Published.reference $(if($Sha){$Sha}else{$Published.manifestSha256})}
function Assert-FetchFailed($Result,[string]$Code){
    Assert ($Result.status -ceq 'failed' -and $null -eq $Result.outputPath -and $Result.reasonCode -ceq $Code) "expected $Code, got $($Result.reasonCode) (status $($Result.status))"
}
# 取得側が検査する経路を通すため、公開済みと同じkeyに任意のbytesを置き、対応するreferenceを作る。
function New-RawReference($Fx,[byte[]]$Manifest,[string]$Revision,[int]$ObjectCount=2,[long]$TotalBytes=10){
    $config=@{Data=[pscustomobject]@{repositoryId=('a'*64);endpoint='x';bucket='osm-artifacts';prefix='development/content/v1/'}}
    $record=[pscustomobject]@{repositoryId=('a'*64);contentSet='bs4-spring-full';target='windows-x64-player';revision=$Revision;seriesId=(('a'*64)+'/bs4-spring-full/windows-x64-player');manifestSha256=(Get-Sha $Manifest);objectCount=$ObjectCount;totalBytes=$TotalBytes}
    Set-Remote $Fx (Get-Key 'transport.json' $Revision) $Manifest
    return @{reference=(New-ContentReference $config $record);manifestSha256=(Get-Sha $Manifest)}
}
function New-ManifestJson([string]$Revision,$Files){[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject ([ordered]@{contentSet='bs4-spring-full';revision=$Revision;target='StandaloneWindows64-Player';files=@($Files)}) -Depth 6))}
foreach($caseName in $selected){
    $fixture=$null
    try{
        Assert ($caseName -cin $registered) 'unknown-case'
        $fixture=New-ContentFixture;$fx=$fixture
        switch -CaseSensitive ($caseName){
            'fetch-roundtrip'{
                $src=New-ContentSource $fx;$n=$src.Names.Count+1;$p=Publish-Source $fx $src
                $artifactsPrint=Get-TreeFingerprint $fx.ArtifactsRoot;$sourcePrint=Get-TreeFingerprint $src.Dir
                $f=Fetch $fx $p
                Assert ($f.status -ceq 'passed' -and $f.reasonCode -ceq 'complete' -and $f.manifestSha256 -ceq $src.ManifestSha -and $f.objects.total -eq $n) "fetch failed: $($f.reasonCode)"
                Assert ((Get-Count $fx 'get') -eq $n -and (Get-Count $fx 'put') -eq 0) 'GET/PUT count'
                Assert ([IO.Path]::GetFileName($f.outputPath) -ceq 'payload' -and (Test-ArtifactWithin $f.outputPath $fx.Owned[0]) -and [IO.Path]::GetDirectoryName($f.outputPath) -ceq $f.residue.localPath) 'output must be a new private operation'
                Assert ((Get-TreeFingerprint $f.outputPath) -ceq $sourcePrint) 'fetched tree differs from the published Content output'
                Assert ((Get-TreeFingerprint $fx.ArtifactsRoot) -ceq $artifactsPrint -and (Get-TreeFingerprint $src.Dir) -ceq $sourcePrint) 'fetch wrote to ArtifactsRoot or changed the source'
                Assert-ArtifactAcl $f.outputPath $true;Assert-ArtifactAcl ([IO.Path]::Combine($f.outputPath,'transport.json')) $false
                $again=Fetch $fx $p;Assert ($again.status -ceq 'passed' -and $again.outputPath -cne $f.outputPath -and (Get-TreeFingerprint $f.outputPath) -ceq $sourcePrint) 'each fetch needs a new private destination'
                Assert (-not (ConvertTo-Json -InputObject $f -Compress).Contains('r2.cloudflarestorage') -and (Get-Count $fx 'delete') -eq 0) 'endpoint exposed or DELETE called'
            }
            'fetch-fresh-process'{
                $src=New-ContentSource $fx;$n=$src.Names.Count+1;$p=Publish-Source $fx $src
                $c=Finish-Child (Invoke-Child (Get-ChildArgs $fx 'fetch' @{Reference=$p.reference;Sha256=$p.manifestSha256}))
                Assert ($c.Exit -eq 0) "fresh fetch failed: $($c.Output) $($c.Error)"
                $v=$c.Output.Trim()|ConvertFrom-Json -Depth 8 -DateKind String
                Assert ($v.status -ceq 'passed' -and (Get-TreeFingerprint $v.outputPath) -ceq (Get-TreeFingerprint $src.Dir) -and @([IO.Directory]::EnumerateFiles($fx.Remote,'get-attempt-*.txt')).Count -eq $n) 'fresh fetch result'
                Assert (@([IO.Directory]::EnumerateFiles($fx.Remote,'put-attempt-*.txt')).Count -eq 0) 'fetch sent something'
                foreach($old in [IO.Directory]::EnumerateFiles($fx.Remote,'get-attempt-*.txt')){[IO.File]::Delete($old)}
                $bad=Finish-Child (Invoke-Child (Get-ChildArgs $fx 'fetch' @{Reference=$p.reference;Sha256=('0'*64)}))
                Assert ($bad.Exit -eq 1 -and ($bad.Output.Trim()|ConvertFrom-Json).reasonCode -ceq 'manifest-mismatch' -and @([IO.Directory]::EnumerateFiles($fx.Remote,'get-attempt-*.txt')).Count -eq 0) 'independent hash mismatch must stop before any GET'
            }
            'expected-hash-and-reference'{
                $src=New-ContentSource $fx;$p=Publish-Source $fx $src
                # 期待hashはreferenceとは独立。referenceのhashで代用せず、違えば何も取らない。
                Assert-FetchFailed (Fetch $fx $p ('0'*64)) 'manifest-mismatch';Assert ((Get-Count $fx 'get') -eq 0) 'GET before comparing the independent hash'
                foreach($bad in @('','abc',('A'*64),($p.manifestSha256+'0'))){Assert-FetchFailed (Invoke-ContentFetch $fx.Config $p.reference $bad) 'invalid-reference'}
                foreach($ref in @('','osm-build-v1:abc','osm-content-v1:***',($p.reference+'x'),$p.reference.Substring(0,$p.reference.Length-4))){Assert-FetchFailed (Invoke-ContentFetch $fx.Config $ref $p.manifestSha256) 'invalid-reference'}
                $other=@{Data=[pscustomobject]@{repositoryId=('b'*64);endpoint='x';bucket='osm-artifacts';prefix='p'}}
                $foreign=New-ContentReference $other ([pscustomobject]@{repositoryId=('b'*64);contentSet='bs4-spring-full';target='windows-x64-player';revision=$src.Revision;seriesId=(('b'*64)+'/bs4-spring-full/windows-x64-player');manifestSha256=$p.manifestSha256;objectCount=8;totalBytes=10})
                Assert-FetchFailed (Invoke-ContentFetch $fx.Config $foreign $p.manifestSha256) 'invalid-reference'
                Assert ((Get-Count $fx 'get') -eq 0) 'rejected input reached the network'
                # 取得したtransport.jsonのhashが期待と違えば、ほかのfileを取らない。
                $tj=Get-Key 'transport.json';$good=Get-Remote $fx $tj;Set-Remote $fx $tj ([Text.Encoding]::UTF8.GetBytes('{"replaced":true}'))
                Assert-FetchFailed (Fetch $fx $p) 'manifest-mismatch';Assert ((Get-Count $fx 'get') -eq 1) 'content files were fetched after a manifest hash mismatch'
                Set-Remote $fx $tj $good;Clear-Order $fx
                # referenceと中身の食い違い（件数）と、別のcontentSetを指すreference。
                $liar=New-ContentReference @{Data=[pscustomobject]@{repositoryId=('a'*64)}} ([pscustomobject]@{repositoryId=('a'*64);contentSet='bs4-spring-full';target='windows-x64-player';revision=$src.Revision;seriesId=(('a'*64)+'/bs4-spring-full/windows-x64-player');manifestSha256=$p.manifestSha256;objectCount=($src.Names.Count+2);totalBytes=10})
                Assert-FetchFailed (Invoke-ContentFetch $fx.Config $liar $p.manifestSha256) 'manifest-mismatch';Assert ((Get-Count $fx 'get') -eq 1) 'content files were fetched after the identity mismatch'
                Clear-Order $fx;$wrongSet=New-ContentReference @{Data=[pscustomobject]@{repositoryId=('a'*64)}} ([pscustomobject]@{repositoryId=('a'*64);contentSet='spring-full';target='windows-x64-player';revision=$src.Revision;seriesId=(('a'*64)+'/spring-full/windows-x64-player');manifestSha256=$p.manifestSha256;objectCount=($src.Names.Count+1);totalBytes=10})
                Set-Remote $fx ('development/content/v1/'+('a'*64)+'/spring-full/windows-x64-player/'+$src.Revision+'/transport.json') $good
                Assert-FetchFailed (Invoke-ContentFetch $fx.Config $wrongSet $p.manifestSha256) 'manifest-mismatch'
            }
            'fetch-integrity'{
                $src=New-ContentSource $fx;$p=Publish-Source $fx $src;$keys=@($src.Names|ForEach-Object{Get-Key "content/$_"})
                $originals=@($keys|ForEach-Object{,(Get-Remote $fx $_)})
                $damage=[ordered]@{
                    'tampered same size'={param($k,$o)$b=[byte[]]$o.Clone();$b[0]=$b[0] -bxor 0xFF;Set-Remote $fx $k $b}
                    'truncated'={param($k,$o)Set-Remote $fx $k ([byte[]]$o[0..($o.Length-2)])}
                    'extended'={param($k,$o)Set-Remote $fx $k ([byte[]]($o+[byte[]](1)))}
                    'missing'={param($k,$o)[IO.File]::Delete((Get-RemotePath $fx.Remote $k))}
                }
                foreach($name in $damage.Keys){
                    Clear-Order $fx;& $damage[$name] $keys[2] $originals[2]
                    $r=Fetch $fx $p;Assert-FetchFailed $r 'download-unconfirmed'
                    Set-Remote $fx $keys[2] $originals[2]
                }
                Assert ((Fetch $fx $p).status -ceq 'passed') 'restored remote must fetch again'
                # 余剰: 取得中に展開先へ別のfileが現れたら、集合の不一致として拒否する。
                Clear-Order $fx;$last=$keys[$keys.Count-1]
                $fx.OnTransport={param($req)if($req.Operation -ceq 'get' -and $req.Key -ceq $last){[IO.File]::WriteAllText([IO.Path]::Combine([IO.Path]::GetDirectoryName($req.DestinationPath),'extra.cf'),'extra')};$null}.GetNewClosure()
                Assert-FetchFailed (Fetch $fx $p) 'content-mismatch';$fx.OnTransport=$null
                # 不正なmanifestは、期待hashが一致しても受け付けない（path規則・上限は取得時にも同じ）。
                $rev='20260801T000000000Z-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
                foreach($bad in @('../escape.cf','CON.cf','a b.cf','x..y')){
                    $raw=New-RawReference $fx (New-ManifestJson $rev @([ordered]@{path=$bad;size=1;sha256=('a'*64)})) $rev
                    Assert-FetchFailed (Fetch $fx $raw) 'invalid-content-manifest'
                }
                $raw=New-RawReference $fx (New-ManifestJson $rev @([ordered]@{path='zero.cf';size=0;sha256=('a'*64)})) $rev;Assert-FetchFailed (Fetch $fx $raw) 'unsupported-content'
                $raw=New-RawReference $fx ([byte[]]::new(4MB+1)) $rev;Assert-FetchFailed (Fetch $fx $raw) 'invalid-content-manifest'
                Assert ((Get-Count $fx 'get') -ge 1 -and (Get-Count $fx 'delete') -eq 0) 'call counts'
            }
            'fetch-budget-and-errors'{
                $src=New-ContentSource $fx '20260924T202227427Z-0123456789abcdef0123456789abcdef' @{} 3;$p=Publish-Source $fx $src;$n=$src.Names.Count+1;$limit=(300+2*$n)*1000
                $fx.OnTransport={param($req)if($req.Operation -ceq 'get'){$fx.Now+=$limit/2+1000};$null}.GetNewClosure()
                Assert-FetchFailed (Fetch $fx $p) 'budget-exceeded';Assert ((Get-Count $fx 'get') -eq 2) 'fetch continued after the budget expired'
                foreach($owner in @(Get-EvidenceTestModules ArtifactApplication)){Assert ($null -eq (& $owner {$script:Budget})) 'budget was not restored'}
                # (b) 全downloadの後に予算が尽きたら、passedを返さない。
                $fx.Now=0;Clear-Order $fx;$lastGet=Get-Key "content/$($src.Names[$src.Names.Count-1])"
                $fx.OnTransport={param($req)if($req.Operation -ceq 'get' -and $req.Key -ceq $lastGet){$fx.Now=$limit+1};$null}.GetNewClosure()
                Assert-FetchFailed (Fetch $fx $p) 'budget-exceeded';Assert ((Get-Count $fx 'get') -eq $n) 'not every object was downloaded before the clock moved'
                $fx.Now=0;Clear-Order $fx
                $fx.OnTransport={param($req)if($req.Operation -ceq 'get'){throw 'SECRET-token https://eeeeeeee.r2.cloudflarestorage.com'}}.GetNewClosure()
                $r=Fetch $fx $p;Assert-FetchFailed $r 'download-unconfirmed'
                $json=ConvertTo-Json -InputObject $r -Compress;Assert (-not $json.Contains('SECRET') -and -not $json.Contains('r2.cloudflarestorage')) 'exception text leaked'
                $fx.OnTransport={param($req)if($req.Operation -ceq 'get'){[pscustomobject]@{Operation='get';Generation=('f'*32);HttpStatus=403;StatusClass='forbidden';S3Code='AccessDenied';Bytes=$null;Sha256=$null;StatusObserved=$true;TimedOut=$false;Redirected=$false}}}.GetNewClosure()
                Assert-FetchFailed (Fetch $fx $p) 'download-unconfirmed'
                Assert-FetchFailed (Invoke-ContentFetch ([IO.Path]::Combine($fx.Root,'absent.json')) $p.reference $p.manifestSha256) 'invalid-content-config'
            }
        }
    }catch{$failed.Add($caseName+': '+$_.Exception.GetType().Name+': '+$_.Exception.Message+' (line '+$_.InvocationInfo.ScriptLineNumber+')')}
    finally{$executed.Add($caseName);Clear-ContentFixture $fixture}
}
if($selected.Count -eq 0 -or $selected.Count -ne $executed.Count){$failed.Add('selection')}
if($ResultPath){Write-EvidenceTestJson $ResultPath ([ordered]@{registered=$registered;selected=$selected;executed=@($executed);failed=@($failed);binaries=@(Get-EvidenceTestBinaries)})}
foreach($item in $failed){[Console]::Error.WriteLine($item)}
[Console]::WriteLine("ContentFetch $($executed.Count), failed $($failed.Count)")
if($failed.Count){exit 1}
