param([Parameter(Mandatory=$true)][string]$Entry,[Parameter(Mandatory=$true)][string]$CallsPath,[Parameter(Mandatory=$true)][string]$Base,[Parameter(Mandatory=$true)][string]$LegacyBase,[Parameter(Mandatory=$true)][string]$EntryArgumentsBase64)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# Fresh process for one public entry. The real store-root resolver (and the path
# identity module it uses) is loaded, with its Known Folder seams pointed at the
# temporary UserProfile and LocalApplicationData stand-ins the test prepared, and
# its marker and former-root operations recorded. Every other module import, and
# git, is the store I/O seam: the entry reaches store code only through them, so
# any record with the 'store:' prefix means the entry went past the gate.
function Record([string]$Name){[IO.File]::AppendAllText($CallsPath,$Name+"`n")}
. (Join-Path $PSScriptRoot 'StoreRootTestSupport.ps1')
$resolver=Import-StoreRootTestModule
$native=& $resolver {$script:NativeMarkerIo}
$io=@{
    Open={param([string]$Path)Record ('marker:open:'+$Path);& $native.Open $Path}.GetNewClosure()
    FinalPath={param($Handle)Record 'marker:final-path';& $native.FinalPath $Handle}.GetNewClosure()
    Read={param($Handle)Record 'marker:read';& $native.Read $Handle}.GetNewClosure()
    Close={param($Handle)Record 'marker:close';& $native.Close $Handle}.GetNewClosure()
}
$query={param([string]$Path)$status=[string][OneStarMaker.Workflow.StoreRootNative]::Query($Path);Record ('legacy:query:'+$Path+'='+$status);$status}
Set-StoreRootTestSeams $Base $LegacyBase $io $query
function Import-Module([string]$Name){
    if([IO.Path]::GetFileName($Name) -ceq 'WindowsStoreRoot.psm1'){Record 'import:WindowsStoreRoot.psm1';return}
    Record ('store:import:'+[IO.Path]::GetFileName($Name));throw 'store-boundary-reached'
}
function git {Record 'store:git';throw 'store-boundary-reached'}
# Arguments arrive as base64 JSON so that entry flags such as -Task never bind to this fixture.
$EntryArguments=@(ConvertFrom-Json -InputObject ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($EntryArgumentsBase64))))
# PowerShell parameter names (-Task) stay bare so they bind; every other token,
# including the artifacts '--flag' grammar, is a quoted literal value.
$line=". '"+$Entry.Replace("'","''")+"'"
foreach($token in $EntryArguments){$line+=' '+$(if($token -cmatch '\A-[A-Za-z]+\z'){$token}else{"'"+$token.Replace("'","''")+"'"})}
. ([scriptblock]::Create($line))
exit $LASTEXITCODE
