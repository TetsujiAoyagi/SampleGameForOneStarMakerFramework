#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
dotnet run --project "$ROOT/tools/protocol-codegen/ProtocolCodegen.csproj" -c Release -- \
  --input "$ROOT/protocol/debugsocket" \
  --repo-root "$ROOT" \
  "$@"
