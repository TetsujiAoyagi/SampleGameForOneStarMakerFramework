param([Parameter(Mandatory=$true)][string]$Entry,[Parameter(Mandatory=$true)][string]$CallsPath,[Parameter(Mandatory=$true)][ValidateSet('not-virtualized','virtualized','directory-fails','open-fails','final-path-fails','close-fails')][string]$Mode,[Parameter(Mandatory=$true)][string]$EntryArgumentsBase64)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# Fresh process for one public entry. Only the real store-context module is loaded,
# with its private OS seam replaced. Every other module import, and git, is the
# store I/O seam: the entry reaches store code only through them, so any record
# with the 'store:' prefix means the entry went past the decision.
function Record([string]$Name){[IO.File]::AppendAllText($CallsPath,$Name+"`n")}
$context=Import-Module ([IO.Path]::Combine([IO.Path]::GetDirectoryName($PSScriptRoot),'WindowsStoreContext.psm1')) -PassThru
$io=@{
    CreateDirectory={param($Path)Record ('probe:directory:'+$Path);if($Mode -ceq 'directory-fails'){throw 'fixture-directory'}}
    Open={param($Path)Record ('probe:open:'+$Path);if($Mode -ceq 'open-fails'){throw 'fixture-open'};[pscustomobject]@{Path=$Path}}
    FinalPath={param($Handle)Record 'probe:final-path';if($Mode -ceq 'final-path-fails'){throw 'fixture-final-path'}
        # The virtualized answer is the observed Codex shape: a package LocalCache copy.
        if($Mode -ceq 'virtualized'){$local=[Environment]::GetFolderPath('LocalApplicationData');return [IO.Path]::Combine($local,'Packages','Fixture.Package_0000000000000','LocalCache','Local',$Handle.Path.Substring($local.Length+1))};$Handle.Path}
    Close={param($Handle)Record 'probe:close';if($Mode -ceq 'close-fails'){throw 'fixture-close'}}
    RemoveDirectory={param($Path)Record ('probe:remove:'+$Path)}
}
& $context {param($v)$script:ProbeIo=$v} $io
function Import-Module([string]$Name){
    if([IO.Path]::GetFileName($Name) -ceq 'WindowsStoreContext.psm1'){Record 'import:WindowsStoreContext.psm1';return}
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
