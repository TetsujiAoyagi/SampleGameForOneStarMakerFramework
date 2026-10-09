# Fixture uses the 13 non-secret source names introduced by PR #106, with deterministic
# dummy bytes. It invokes the ordinary selector and publish pipeline; no private E1 data.
function New-EvidenceReaderFixture([string]$Root){
    $source=[IO.Path]::Combine($Root,'source');[IO.Directory]::CreateDirectory($source)|Out-Null
    $names=@('final-offline/snapshots/A3.md','final-offline/snapshots/B_RESULT.md','final-offline/snapshots/implementation.diff','final-offline/snapshots/implementation.name-status','final-offline/snapshots/implementation.stat','final-offline/c-raw/ArtifactTransfer.stdout.log','final-offline/c-raw/ArtifactPackage.stdout.log','continuation/current-run/publish-operation/operation-observations.json','continuation/current-run/publish-operation/protection-receipt.json','continuation/raw/fetch-reader/verification.json','continuation/settings-provenance.json','continuation/observations/bucket-before.json','continuation/observations/bucket-after.json')
    foreach($name in $names){$path=[IO.Path]::Combine($source,$name);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))|Out-Null;[IO.File]::WriteAllText($path,"offline-reader-dummy: $name`n",[Text.UTF8Encoding]::new($false))}
    $selection=[ordered]@{schemaVersion=2;purpose='evidence';taskId='fixture';root=$source;files=$names;base='b'*40;head='c'*40}
    return [pscustomobject]@{Selection=$selection;Source=$source;Files=$names}
}
