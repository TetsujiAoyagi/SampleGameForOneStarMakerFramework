param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../ContentApplication.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactApplication.psm1')
Import-Module (Join-Path $PSScriptRoot '../ContentContract.psm1')
Import-Module (Join-Path $PSScriptRoot '../ContentStore.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot '../ArtifactPaths.psm1')
. (Join-Path $PSScriptRoot 'ContentTestSupport.ps1')
# Contentの値の規則（AC1〜AC5・AC16・AC3/AC18のallowlist）を、非秘密の合成manifestと
# 隔離した設定だけで検査する。通信・store・退避は使わない（それらはPublish/Fetch suite）。
$registered=@('identity-and-series','key-derivation','manifest-paths','manifest-reader','limits','reference-codec','config-values','intent-shape','transport-allowlist')
$selected=@(if($Case -ceq '*'){$registered}else{$Case.Split(',')})
$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
function Assert([bool]$Condition,[string]$Message){Assert-EvidenceTest $Condition $Message}
function Assert-Throws([scriptblock]$Action,[string]$Code,[string]$Label){
    $thrown=$null;try{& $Action}catch{$thrown=$_.Exception.Message}
    Assert ($thrown -ceq $Code) "$Label : expected '$Code', got '$thrown'"
}
$revision='20260924T202227427Z-0123456789abcdef0123456789abcdef'
$hash='a'*64
function New-ManifestBytes([object[]]$Files,[hashtable]$Override=@{}){
    $m=[ordered]@{version=1;contentSet='bs4-spring-full';revision=$revision;target='StandaloneWindows64-Player';files=@($Files)}
    foreach($k in @($Override.Keys)){if($null -eq $Override[$k]){$m.Remove($k)}else{$m[$k]=$Override[$k]}}
    return [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $m -Depth 8))
}
function Copy-Ordered($Source){$o=[ordered]@{};foreach($k in $Source.Keys){$o[$k]=$Source[$k]};return $o}
function New-File([string]$Path,$Size=10){[ordered]@{path=$Path;size=$Size;sha256=$hash}}
$config=@{Data=[pscustomobject]@{repositoryId=('a'*64);endpoint='https://x';bucket='osm-artifacts';prefix='development/content/v1/'};Hash=('1'*64);EvidenceConfigSha256=('2'*64)}
Import-ArtifactPackage
foreach($caseName in $selected){
    $fixture=$null
    try{
        Assert ($caseName -cin $registered) 'unknown-case'
        switch -CaseSensitive ($caseName){
            'identity-and-series'{
                Assert ((Get-ContentTargetToken 'StandaloneWindows64-Player') -ceq 'windows-x64-player') 'target table'
                foreach($t in @('Android','standalonewindows64-player','StandaloneWindows64-Player ','')){Assert-Throws {Get-ContentTargetToken $t} 'unsupported-series' "target $t"}
                Assert-Throws {Assert-ContentTargetToken 'linux'} 'unsupported-series' 'token table'
                foreach($ok in @('bs4-spring-full','spring-full','all-full','spring-whitebox','spring-full-whitebox','a','0x',('a'*64))){Assert-ContentToken $ok}
                foreach($bad in @('','A','Bs4','-a','a_b','a b','a.b',('a'*65),'日本','a/b','a-b ',"a`n")){Assert-Throws {Assert-ContentToken $bad} 'invalid-content-identity' "contentSet '$bad'"}
                Assert-ContentRevision $revision
                foreach($bad in @('','20260924T202227427Z-0123456789ABCDEF0123456789ABCDEF','20260924T202227427Z-0123456789abcdef0123456789abcde','20260924T20222742Z-0123456789abcdef0123456789abcdef',($revision+"`n"),('x'+$revision))){Assert-Throws {Assert-ContentRevision $bad} 'invalid-content-identity' "revision $([regex]::Escape($bad))"}
                Assert ((Get-ContentSeries ('a'*64) 'all-full' 'windows-x64-player') -ceq (('a'*64)+'/all-full/windows-x64-player')) 'series'
                $files=@(New-File 'a.cf')
                Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes $files @{target='Android'})} 'unsupported-series' 'manifest target'
                Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes $files @{contentSet='Bad Set'})} 'invalid-content-identity' 'manifest contentSet'
                Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes $files @{revision='nope'})} 'invalid-content-identity' 'manifest revision'
            }
            'key-derivation'{
                $prefix='development/content/v1/'+('a'*64)+'/bs4-spring-full/windows-x64-player/'+$revision+'/'
                Assert ((Get-ContentKey $config 'bs4-spring-full' 'windows-x64-player' $revision) -ceq ($prefix+'transport.json')) 'transport key'
                Assert ((Get-ContentKey $config 'bs4-spring-full' 'windows-x64-player' $revision 'abc.cf') -ceq ($prefix+'content/abc.cf')) 'content key'
                Assert ((Get-ContentKey $config 'bs4-spring-full' 'windows-x64-player' $revision 'sub/b.resS') -ceq ($prefix+'content/sub/b.resS')) 'nested key'
                $long=(('d'*79)+'/')*2+('e'*80)
                Assert ($long.Length -le 240 -and (Get-ContentKey $config 'bs4-spring-full' 'windows-x64-player' $revision $long).Length -le 512) 'key length bound'
                Assert-Throws {Get-ContentKey $config 'bs4-spring-full' 'windows-x64-player' $revision ('p'*241)} 'invalid-content-manifest' 'path over 240'
                Assert-Throws {Get-ContentKey $config 'Bad' 'windows-x64-player' $revision} 'invalid-content-identity' 'key contentSet'
                Assert-Throws {Get-ContentKey $config 'bs4-spring-full' 'android' $revision} 'unsupported-series' 'key target'
                Assert-Throws {Get-ContentKey $config 'bs4-spring-full' 'windows-x64-player' 'rev'} 'invalid-content-identity' 'key revision'
            }
            'manifest-paths'{
                $bad=@('CON','con.cf','aux','NUL.txt','COM1','lpt9.cf','a.','a ','a/b.','../x','a/../b','./a','a/./b','a//b','/a','a\b','a:b','x..y','..','.',' a','credentials','token','a/secret','.env','a/.git','id_rsa','k.pem','a/k.key','é.cf','a b.cf','a*.cf','a?.cf',('p'*241),'')
                foreach($p in $bad){Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes @(New-File $p))} 'invalid-content-manifest' "path '$p'"}
                foreach($p in @('0123456789abcdef0123456789abcdef.cf','f.resS','BuildManifestHash.txt','a/b/c.bin','A_b-c.1',('p'*240))){$m=ConvertFrom-ContentManifest (New-ManifestBytes @(New-File $p));Assert ($m.Files[0].path -ceq $p) "accepted $p"}
                Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes @((New-File 'A.cf'),(New-File 'a.cf')))} 'invalid-content-manifest' 'case-only duplicate'
                Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes @((New-File 'a.cf'),(New-File 'a.cf')))} 'invalid-content-manifest' 'exact duplicate'
                Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes @((New-File 'a'),(New-File 'a/b')))} 'invalid-content-manifest' 'file then directory'
                Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes @((New-File 'a/b'),(New-File 'a')))} 'invalid-content-manifest' 'directory then file'
                Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes @((New-File 'A'),(New-File 'a/b')))} 'invalid-content-manifest' 'case-folded collision'
                Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes @(New-File 'a.cf' 0))} 'unsupported-content' 'zero byte'
                foreach($size in @(-1,1.5,'10')){Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes @(New-File 'a.cf' $size))} 'invalid-content-manifest' "size $size"}
                Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes @([ordered]@{path='a.cf';size=1;sha256='ABC'}))} 'invalid-content-manifest' 'hash shape'
                Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes @([ordered]@{path='a.cf';size=1}))} 'invalid-content-manifest' 'missing hash'
            }
            'manifest-reader'{
                $files=@(New-File 'a.cf')
                # 1 MiB超でも4 MiB以下なら読める（既存の1 MiB readerは使わない）。実測のsourceFilesが大半を占める形を模す。
                $filler=@(1..9000|ForEach-Object{[ordered]@{path="Assets/Source/Generated/Scene$_.asset";sha256=$hash}})
                $big=New-ManifestBytes $files @{sourceFiles=$filler}
                Assert ($big.Length -gt 1MB -and $big.Length -lt 4MB) "fixture size $($big.Length)"
                $m=ConvertFrom-ContentManifest $big;Assert ($m.Files.Count -eq 1 -and $m.Sha256 -ceq (Get-Sha $big) -and $m.ManifestBytes -eq $big.Length) 'large manifest'
                $tmp=[IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-content-reader-'+[Guid]::NewGuid().ToString('N'));[IO.Directory]::CreateDirectory($tmp)|Out-Null
                try{
                    $p=[IO.Path]::Combine($tmp,'transport.json');[IO.File]::WriteAllBytes($p,$big);Assert ((Read-ContentManifestFile $p).Length -eq $big.Length) '4 MiB reader'
                    $s=[IO.FileStream]::new($p,[IO.FileMode]::Create);try{$s.SetLength(4MB+1)}finally{$s.Dispose()}
                    Assert-Throws {Read-ContentManifestFile $p} 'invalid-content-manifest' 'file over 4 MiB'
                    [IO.File]::WriteAllBytes($p,[byte[]]@());Assert-Throws {Read-ContentManifestFile $p} 'invalid-content-manifest' 'empty file'
                    Assert-Throws {Read-ContentManifestFile ([IO.Path]::Combine($tmp,'absent.json'))} 'invalid-source' 'missing file'
                    Assert-Throws {Read-ContentManifestFile 'relative.json'} 'invalid-source' 'relative path'
                }finally{[IO.Directory]::Delete($tmp,$true)}
                Assert-Throws {ConvertFrom-ContentManifest ([byte[]]::new(4MB+1))} 'invalid-content-manifest' 'bytes over 4 MiB'
                $u=[Text.Encoding]::UTF8
                $good='{"contentSet":"bs4-spring-full","revision":"'+$revision+'","target":"StandaloneWindows64-Player","files":[{"path":"a.cf","size":1,"sha256":"'+$hash+'"}]}'
                $null=ConvertFrom-ContentManifest $u.GetBytes($good)
                Assert-Throws {ConvertFrom-ContentManifest $u.GetBytes($good.Replace('"files"','"contentSet":"all-full","files"'))} 'invalid-content-manifest' 'duplicate top-level key'
                Assert-Throws {ConvertFrom-ContentManifest $u.GetBytes($good.Replace('"size":1','"size":1,"size":2'))} 'invalid-content-manifest' 'duplicate nested key'
                Assert-Throws {ConvertFrom-ContentManifest ([byte[]](0xFF,0xFE,0x7B,0x7D))} 'invalid-content-manifest' 'invalid UTF-8'
                foreach($bad in @('','[]','null','{}','{"contentSet":1}',$good.Replace('"files":[{"path":"a.cf","size":1,"sha256":"'+$hash+'"}]','"files":[]'),$good.Replace('"files":','"files":{},"x":'),$good.TrimEnd('}'))){Assert-Throws {ConvertFrom-ContentManifest $u.GetBytes($bad)} 'invalid-content-manifest' "shape $bad"}
                $extra=$good.Replace('"files"','"unityVersion":"6000.6.0f1","sourceFiles":[],"files"');$null=ConvertFrom-ContentManifest $u.GetBytes($extra)
            }
            'limits'{
                $many=@(1..1023|ForEach-Object{New-File ("f$_.cf")})
                $m=ConvertFrom-ContentManifest (New-ManifestBytes $many);Assert ($m.ObjectCount -eq 1024) 'object limit includes transport.json'
                Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes (@($many)+@(New-File 'f1024.cf')))} 'size-limit' '1025 objects'
                $null=ConvertFrom-ContentManifest (New-ManifestBytes @(New-File 'a.cf' 256MB))
                Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes @(New-File 'a.cf' (256MB+1)))} 'size-limit' 'single file over 256 MiB'
                $null=ConvertFrom-ContentManifest (New-ManifestBytes @((New-File 'a.cf' 256MB),(New-File 'b.cf' 256MB)))
                Assert-Throws {ConvertFrom-ContentManifest (New-ManifestBytes @((New-File 'a.cf' 256MB),(New-File 'b.cf' 256MB),(New-File 'c.cf' 1)))} 'size-limit' 'total over 512 MiB'
            }
            'reference-codec'{
                $record=[pscustomobject]@{repositoryId=('a'*64);contentSet='bs4-spring-full';target='windows-x64-player';revision=$revision;seriesId=(('a'*64)+'/bs4-spring-full/windows-x64-player');manifestSha256=$hash;objectCount=221;totalBytes=5716575}
                $ref=New-ContentReference $config $record;Assert ($ref.StartsWith('osm-content-v1:',[StringComparison]::Ordinal) -and $ref -cmatch '\Aosm-content-v1:[A-Za-z0-9_-]+\z') 'reference format'
                $v=Read-ContentReference $ref $config;Assert ($v.manifestSha256 -ceq $hash -and $v.objectCount -eq 221 -and $v.revision -ceq $revision) 'reference roundtrip'
                function ConvertTo-Ref($Object){'osm-content-v1:'+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $Object -Compress))).TrimEnd('=').Replace('+','-').Replace('/','_')}
                $base=[ordered]@{schemaVersion=[long]1;purpose='content';repositoryId=('a'*64);contentSet='bs4-spring-full';target='windows-x64-player';revision=$revision;seriesId=$record.seriesId;manifestSha256=$hash;objectCount=221;totalBytes=100}
                $null=Read-ContentReference (ConvertTo-Ref $base) $config
                $variants=@{purpose='build';repositoryId=('b'*64);contentSet='Bad';target='android';revision='x';seriesId='other';manifestSha256='abc';objectCount=1;extra=1}
                foreach($k in $variants.Keys){$o=(Copy-Ordered $base);$o[$k]=$variants[$k];Assert-Throws {Read-ContentReference (ConvertTo-Ref $o) $config} 'invalid-reference' "field $k"}
                foreach($n in @(0,1025,'221',2.5)){$o=(Copy-Ordered $base);$o.objectCount=$n;Assert-Throws {Read-ContentReference (ConvertTo-Ref $o) $config} 'invalid-reference' "objectCount $n"}
                $o=(Copy-Ordered $base);$o.Remove('totalBytes');Assert-Throws {Read-ContentReference (ConvertTo-Ref $o) $config} 'invalid-reference' 'missing field'
                $o=(Copy-Ordered $base);$o.totalBytes=513MB;Assert-Throws {Read-ContentReference (ConvertTo-Ref $o) $config} 'invalid-reference' 'totalBytes limit'
                foreach($bad in @('','osm-build-v1:abc','osm-content-v1:','osm-content-v1:***','osm-content-v1:'+$ref.Substring(15)+'=','osm-content-v1:'+$ref.Substring(15).Replace('-','+'),('osm-content-v1:'+('A'*5000)))){Assert-Throws {Read-ContentReference $bad $config} 'invalid-reference' "text $($bad.Length)"}
                $dup=(ConvertTo-Json -InputObject $base -Compress).Replace('"purpose":"content"','"purpose":"content","purpose":"content"')
                Assert-Throws {Read-ContentReference ('osm-content-v1:'+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($dup)).TrimEnd('=').Replace('+','-').Replace('/','_')) $config} 'invalid-reference' 'duplicate field'
                $other=@{Data=[pscustomobject]@{repositoryId=('b'*64);endpoint='x';bucket='osm-artifacts';prefix='p'};Hash=$hash;EvidenceConfigSha256=$hash}
                Assert-Throws {Read-ContentReference $ref $other} 'invalid-reference' 'other repository'
            }
            'config-values'{
                $fixture=New-ContentFixture
                $c=Read-ContentConfig $fixture.Config
                Assert ($c.Data.repositoryId -ceq ('a'*64) -and $c.Data.prefix -ceq 'development/content/v1/' -and $c.Data.bucket -ceq 'osm-artifacts' -and $c.Hash -cmatch '\A[0-9a-f]{64}\z' -and $c.EvidenceConfigSha256 -ceq $fixture.EvidenceHash) 'valid config'
                $base=[ordered]@{schemaVersion=1;purpose='content';profile='osm';evidenceConfigPath=$fixture.EvidencePath;evidenceConfigSha256=$fixture.EvidenceHash;repositoryId=('a'*64);prefix='development/content/v1/';policy='content-publish-v1'}
                $mutations=@{purpose='build';profile='other';prefix='development/builds/v1/';policy='build-publish-v1';schemaVersion=2;repositoryId=('b'*64);evidenceConfigSha256=('0'*64);evidenceConfigPath='relative.json'}
                foreach($k in $mutations.Keys){
                    $o=(Copy-Ordered $base);$o[$k]=$mutations[$k]
                    $path=[IO.Path]::Combine((New-ArtifactOperation),'content-config.json');Write-PrivateJson $path $o
                    Assert-Throws {Read-ContentConfig $path} 'invalid-content-config' "config $k"
                }
                foreach($shape in @('extra','missing')){
                    $o=(Copy-Ordered $base);if($shape -ceq 'extra'){$o.note='x'}else{$o.Remove('policy')}
                    $path=[IO.Path]::Combine((New-ArtifactOperation),'content-config.json');Write-PrivateJson $path $o
                    Assert-Throws {Read-ContentConfig $path} 'invalid-content-config' "config $shape"
                }
                Assert-Throws {Read-ContentConfig ([IO.Path]::Combine($fixture.Root,'content-config.json'))} 'invalid-content-config' 'not in a private operation'
                $buildShaped=(Copy-Ordered $base);$buildShaped.purpose='build';$buildShaped.prefix='development/builds/v1/';$buildShaped.policy='build-publish-v1'
                $path=[IO.Path]::Combine((New-ArtifactOperation),'build-config.json');Write-PrivateJson $path $buildShaped
                Assert-Throws {Read-ContentConfig $path} 'invalid-content-config' 'build-config is not reusable'
            }
            'intent-shape'{
                $files=@(1..40|ForEach-Object{New-File "$('{0:x32}' -f $_).cf" (100+$_)})
                $m=ConvertFrom-ContentManifest (New-ManifestBytes $files)
                $a=New-ContentIntent $config $m;$b=New-ContentIntent $config $m
                $ja=ConvertTo-Json -InputObject $a -Compress -Depth 24
                Assert ($ja -ceq (ConvertTo-Json -InputObject $b -Compress -Depth 24)) 'intent is deterministic'
                Assert (-not $ja.Contains(':\') -and -not $ja.Contains('operation') -and -not $ja.Contains('publishedAt') -and -not $ja.Contains('https://')) 'intent carries no path, operation, time or endpoint'
                Assert ($a.entries.Count -eq 40 -and $a.objectCount -eq 41 -and $a.manifestSha256 -ceq $m.Sha256 -and $a.configSha256 -ceq ('1'*64)) 'intent fields'
                $big=ConvertFrom-ContentManifest (New-ManifestBytes @(1..1023|ForEach-Object{New-File "$('{0:x32}' -f $_).resS" 1000}))
                Assert ([Text.Encoding]::UTF8.GetByteCount((ConvertTo-Json -InputObject (New-ContentIntent $config $big) -Compress -Depth 24)) -lt 1MB) 'full-size intent fits 1 MiB'
                $r=New-ContentReceipt $a ('3'*64) '2026-10-11T00:00:00.0000000+00:00'
                Assert (@($r.Keys).Count -eq 13 -and $r.intentSha256 -ceq ('3'*64) -and $r.manifestSha256 -ceq $m.Sha256 -and $r.objectCount -eq 41) 'receipt fields'
            }
            'transport-allowlist'{
                # 実通信を起こさないよう、許可regexをsourceから取り出して形だけを検査する（Invoke-R2ArtifactOperationは呼ばない）。
                $r2=[IO.File]::ReadAllText([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Transport/R2ArtifactTransport.psm1')));$child=[IO.File]::ReadAllText([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Transport/ArtifactReadback.ps1')))
                $patterns=@{
                    transport=[regex]::Match($r2,'\$Request\.Key -cnotmatch ''([^'']+)''').Groups[1].Value
                    readback=[regex]::Match($child,'\$data\.key -cnotmatch ''([^'']+)''').Groups[1].Value
                }
                $p='development/content/v1/'+('a'*64)+'/bs4-spring-full/windows-x64-player/'+$revision+'/'
                $accept=@(($p+'transport.json'),($p+'content/f55ff16f66f43360266b95db6f8fec01.cf'),($p+'content/BuildManifestHash.txt'),($p+'content/a/b/c.resS'),($p+'content/'+('x'*240)))
                $reject=@(($p+'content/../x'),($p+'content/a/../b'),($p+'content/./a'),($p+'content/..'),($p+'content/'),($p+'content'),($p+'content//a'),($p+'transport.json/'),($p+'other.json'),($p+'content/a b'),($p+'content/'+('x'*241)),($p+"content/a`n"),('development/content/v2/'+$p.Substring(23)),$p.Replace('bs4-spring-full','BS4'),$p.Replace($revision,'rev'),$p.Replace(('a'*64),('a'*63)),('development/content/v1/'+('a'*64)+'/bs4-spring-full/'+$revision+'/transport.json'))
                foreach($name in $patterns.Keys){
                    Assert ($patterns[$name].Contains('development/content/v1/')) "$name allowlist has the Content shape"
                    foreach($k in $accept){Assert ([regex]::IsMatch($k,$patterns[$name],[Text.RegularExpressions.RegexOptions]::CultureInvariant)) "$name accepts $k"}
                    foreach($k in $reject){Assert (-not [regex]::IsMatch($k,$patterns[$name],[Text.RegularExpressions.RegexOptions]::CultureInvariant)) "$name rejects $k"}
                    # 既存の許可形は変わらない。
                    foreach($k in @(('development/builds/v1/'+('a'*64)+'/samplegame/windows-x64-player/il2cpp-high/'+('b'*32)+'/bundle.zip'),('development/evidence/v2/'+('a'*64)+'/task/'+('b'*32)+'/bundle.zip'))){Assert ([regex]::IsMatch($k,$patterns[$name])) "$name keeps existing shape"}
                }
                # DELETEは入口で拒否する（このスライスはR2 objectを削除しない）。検証前に例外になるので通信は起きない。
                Assert ($r2.Contains("-and `$Request.Key -cmatch '\Adevelopment/content/v1/')")) 'delete rule present for the Content prefix'
                $request=@{Endpoint='https://'+('e'*32)+'.r2.cloudflarestorage.com';Operation='delete';Key=($p+'content/a.cf');SourcePath=$null;DestinationPath=$null;DeadlineMilliseconds=1000}
                $transportModule=@(Get-EvidenceTestModules R2ArtifactTransport)[0]
                Assert-Throws {& $transportModule {param($r)Invoke-R2ArtifactOperation 'id' 'secret' ('f'*32) $r} $request} 'Artifact transport unavailable.' 'content delete'
            }
        }
    }catch{$failed.Add($caseName+': '+$_.Exception.GetType().Name+': '+$_.Exception.Message+' (line '+$_.InvocationInfo.ScriptLineNumber+')')}
    finally{$executed.Add($caseName);Clear-ContentFixture $fixture}
}
if($selected.Count -eq 0 -or $selected.Count -ne $executed.Count){$failed.Add('selection')}
if($ResultPath){Write-EvidenceTestJson $ResultPath ([ordered]@{registered=$registered;selected=$selected;executed=@($executed);failed=@($failed);binaries=@(Get-EvidenceTestBinaries)})}
foreach($item in $failed){[Console]::Error.WriteLine($item)}
[Console]::WriteLine("ContentContract $($executed.Count), failed $($failed.Count)")
if($failed.Count){exit 1}
