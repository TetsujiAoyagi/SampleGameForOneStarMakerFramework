# Artifact Storage Route Proof — Phase B rerun result

このsnapshotは固定実装head `b25a0f997434be1b2c76de70d4c7ea08f006e86d` の実装内容と再実行結果を記録する。レビュー所見やPhase Cの判定は含めない。

- Phase A revision snapshot: `docs/handoff/ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION.md`
- Phase A snapshot SHA-256: `5DDB850ECC9F05DFFE666CAE752BF6C7AA4F8A5620883CA5203FB47D3AD0353F`（UTF-8 no BOM file bytes）
- implementation base: `93d2a1c`（A3凍結snapshotに記録されたbranch point）
- implementation head: `b25a0f997434be1b2c76de70d4c7ea08f006e86d`
- generated at: `2026-09-27T21:10:13Z`
- 担当: Codex（モデルvariant未確認）

## 実装結果

- unsigned requestは独立したHTTP GETで生成し、method・URI・Authorization・署名query・対象変更query・redirectを閉じた観測へ記録する。HTTP 400/`InvalidArgument`は許可集合へ加えず、常に`inconclusive`とする。
- bodyは全体を保存せず逐次hash化する。EOF時だけ全体hashを確定し、上限到達時も期待長prefix hashを保持する。S3 XMLの`Code`は長さを制限して安全な値だけを逐次抽出し、Messageや本文を保存しない。
- response observationのOperationが要求operationと一致しなければ無効として扱う。object keyの`.` / `..` path segmentも拒否する。
- 実行とoffline試験で共有するRouteProofの12操作ループを維持し、clock、child runner、transport operationを注入する。子process・両pipeの待機を同じ子予算内で計算し、期限を過ぎた固定待機を追加しない。
- 最初のunlocked PUTを起動した時点で回復対象へ登録する。通常DELETEは認証GETで`NoSuchKey`を確認するまで除去扱いにせず、失敗後は本処理と独立した合計30秒以内で回復DELETEと確認GETを行う。
- 一度のunsigned prefix一致は露出観測として残すが、再現条件が揃う前に`provider-capability-failure`へ昇格しない。
- 判断根拠、失敗境界、保持しない情報、回復条件を日本語コメントで説明した。

## 検証結果

- `dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore`: 成功、警告0、エラー0。
- `pwsh -NoProfile -File tools/Artifacts/tests/R2RouteTransport.Tests.ps1`: 2 passed。S3 code抽出のchunk境界と、byte上限時のprefix hashを確認。
- `pwsh -NoProfile -File tools/Artifacts/tests/RouteProof.Tests.ps1`: 12 passed。production loop、単発prefix露出、曖昧PUT後の独立cleanup期限、NoSuchKey確認を含む。
- `pwsh -NoProfile -File tools/Artifacts/tests/Credentials.Tests.ps1`: 21 passed、failed 0。
- `pwsh tools/contract-audit.ps1`: pass。
- `pwsh tools/docs-audit.ps1`: pass。
- `git diff --check 93d2a1c b25a0f9`: pass。

## 未実行・適用除外

実R2通信、実credential使用、Cloudflare owner設定の前後記録、Unity/EditMode、Player/Build、Phase C/C'は未実行。Unity側のファイルを変更していないため全EditMode回帰は適用除外とし、判定には.NET build、transport/loop/credential suites、contract/docs auditを使う。これはprovider能力、実R2の非公開性、Bucket Lockの有効性を証明しない。HTTP 400/`InvalidArgument`が観測された場合は凍結条件どおり`inconclusive`で止める。

## source / generated DLL SHA-256

- `tools/Artifacts/Probe/RouteProof.ps1`: `B016A83309B4835A1554113D355D252A6ED0451A5A2F0736EE86F5FCF4FFFC14`
- `tools/Artifacts/Probe/R2RouteTransport.cs`: `8E4A666B6BDE67345CF9EABF63F4A512761A89841233D27F2A44EF64E5732EC1`
- `tools/Artifacts/Probe/R2RouteTransport.psm1`: `9E2F6DC989F565935E6027FDACBA71C7A1CEDEBDDFC024B9798344E3318F744A`
- `tools/Artifacts/tests/RouteProof.Tests.ps1`: `AA19B61F1FD436998006AEF95D09720219EF2AEF618BF10790C97E8926524A0C`
- `tools/Artifacts/tests/R2RouteTransport.Tests.ps1`: `785789AFAA9ECD797A3F84C72F7149F0DD73CEBA31B11EBA0325C3766D89694C`
- `tools/Artifacts/Probe/artifacts/route-transport/R2RouteTransport.dll`: `CABB8CAB8FEC8C68798A5FFD78B3494C87A1611CB8944E7B32C0ACC8A535572F`

