# Artifact Storage Route Proof — Phase B result

この文書はPhase Bの実装結果だけを固定するsnapshotであり、Phase C/C'の所見・判定・疑念候補を含めない。

- Phase A snapshot: `328843ea521550af9da203315c2704d3d51cc8d0:docs/handoff/ARTIFACT_STORAGE_ROUTE_PROOF.md`
- implementation base: `93d2a1c`
- implementation head: `e122a79`
- generated at: `2026-09-27T12:31:45.9479822Z`
- 担当: Codex / GPT-5（このsession。モデルvariantの追加割当は記録しない）

## 実装

- `tools/Artifacts/Credentials/CredentialStore.psm1` に、profile `osm` のDPAPI recordを一操作中だけcallbackへ渡す限定export `Invoke-CredentialTransport` を追加した。callbackの全PowerShell streamとConsole出力を回収し、SDK response・例外本文・資格情報・byte配列を拒否する固定の非秘密戻り値検査を行う。
- `tools/Artifacts/Probe/RouteProof.ps1` に、endpoint/key/hashの全体一致検査、12操作の1操作1子process、30秒操作・45秒子process・5分run期限、非秘密JSON Lines、unlocked陽性対照、unsigned privacy判定、lock拒否判定、世代一致、限定再試行、cleanup分類を実装した。
- `tools/Artifacts/Probe/R2RouteTransport.psm1` と `R2RouteTransport.csproj` / `.cs` に、固定AWSSDK.S3のauthenticated PUT/GET/DELETEと署名無しHTTP GETを追加した。本文は保存せず、EOF時のhash、期待長prefix hash、byte数、status/S3 code、timeout/redirectだけを返す。
- `tools/Artifacts/tests/RouteProof.Tests.ps1` にdummy/fakeのoffline試験を追加し、`Credentials.Tests.ps1` にcallbackの正常、PowerShell stream/Host/Console/例外、秘密戻り値拒否を追加した。
- `tools/Artifacts/README.md` にPhase Bの入口、ownerの非秘密lock rule記録形式、実R2未実行、残存lock objectの扱いを追記した。

## Phase Bで実行した検証

- `dotnet restore tools/Artifacts/Probe/R2RouteTransport.csproj`: exit 0
- `dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore`: exit 0、warning 0、error 0
- 別processの `pwsh -NoProfile` から `Assembly.LoadFrom` して `OneStarMaker.Artifacts.Probe.RouteTransport` を取得: 成功
- `pwsh -NoProfile -File tools/Artifacts/tests/Credentials.Tests.ps1`: 21 cases passed、failed 0
- `pwsh -NoProfile -File tools/Artifacts/tests/RouteProof.Tests.ps1`: 8 cases passed、failed 0
- `pwsh -NoProfile -File tools/contract-audit.ps1`: exit 0
- `pwsh -NoProfile -File tools/docs-audit.ps1`: exit 0
- `git diff --cached --check`: エラーなし

## Phase Bで未実行

- 実R2へのPUT/認証GET/署名無しGET/上書き/DELETE、Bucket Lock ruleのowner設定確認、provider capabilityの合否判定
- 実鍵の再入力・`credentials set`・`--replace`、Cloudflare管理権限の取得
- Unity EditMode回帰、PlayMode、Addressables/Content Directory、Player Build
- Phase Cの固定evidence bundle、Phase C'のblind audit bundle、実Evidence/Buildの転送

実鍵、endpoint以外のaccount情報、Authorization、HTTP本文/header、SDK例外本文、秘密の引数/env/Git保存はPhase Bで扱っていない。実R2の合否やR2採用をこのsnapshotから宣言しない。
