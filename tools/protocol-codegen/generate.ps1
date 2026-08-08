$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "../..")
dotnet run --project (Join-Path $Root "tools/protocol-codegen/ProtocolCodegen.csproj") -c Release -- `
  --input (Join-Path $Root "protocol/debugsocket") `
  --repo-root $Root `
  @args
