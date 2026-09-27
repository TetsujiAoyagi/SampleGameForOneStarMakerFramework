# Artifact Storage Route Proof — Phase B revision result

このsnapshotはPhase Bの実装結果だけを記録し、Phase C/C'の所見・判定を含めない。

- Phase A snapshot: `docs/handoff/ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION.md`
- Phase A snapshot SHA-256: `5DDB850ECC9F05DFFE666CAE752BF6C7AA4F8A5620883CA5203FB47D3AD0353F` (UTF-8 no BOM file bytes)
- implementation base: `ab79fbf4371306ccd68bc0e64f8af016217598df`
- implementation head: commit後にHANDOFFへ記録
- generated at: 2026-09-27T14:37:53Z
- 担当: Codex / GPT-6（model variant未確認）

## 実装

- 400/`InvalidArgument`を旧許可集合へ加えず、常に`inconclusive`のまま扱う。
- unsigned transportの閉じた観測にmethod、URI、Authorization/署名query/対象query有無を追加。SDK認証GETと分けたGET生成を記録し、redirectは無追跡。
- 本文をbyte配列へ蓄積せず逐次hash化。8193 byte到達時も期待長prefix hashを保持し、EOF未確認の全体hashはnull。
- RunIdを各childへ渡し、childでkeyとの一致を検証。
- stdout/stderrは子processの稼働中に非同期drainし、終了待ちとpipe回収を期限管理する。
- RouteProofの12操作loopを単一化し、同じloopをclock/child runner/transport境界の注入でoffline試験できるようにした。cleanupとterminal分類もproduction loop内で確認する。
- 契約判断の理由を日本語コメントで追加し、READMEを実装へ合わせた。

## 検証

- `pwsh -NoProfile -File tools/Artifacts/tests/RouteProof.Tests.ps1`: 9 passed, failed 0（同一12操作loop・cleanup含む）
- `dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore`: 0 warning, 0 error
- `pwsh tools/contract-audit.ps1`: pass
- `pwsh tools/docs-audit.ps1`: pass
- `git diff --check`: pass
- `Credentials.Tests.ps1`: 21中5 pass / 16 failed。失敗はsandboxがfixture directoryへのWindows ACL owner設定を拒否（`SetAccessControl` unauthorized）。所有ユーザー権限での再検証が必要。ACL/DPAPIの検証条件を弱める回避はしない。

## 未実行

実R2、実鍵、Cloudflare owner設定、Unity/EditMode、Player/Build、Phase C/C'。HTTP 400が引き続き観測された場合は凍結した案Aに従いGOせず`inconclusive`。本snapshotはprovider能力や非公開性を証明しない。

## 対象source SHA-256

- `tools/Artifacts/Probe/R2RouteTransport.cs` SHA-256 `151204A7C92A9A38B2133BB2990D3E1B4484CDB2DF52D75820D5B75DB13AEA98`
- `tools/Artifacts/Probe/R2RouteTransport.psm1` SHA-256 `04F8EBA313F10BAF7943A2DF960512AF0DC09806A75AADC835F3F2DD4B226405`
- `tools/Artifacts/Probe/RouteProof.ps1` SHA-256 `249BFC08A22CFDA351E85679BF940E2C70ACDF020B35D7E2F665B2BFE3D42342`
- `tools/Artifacts/tests/RouteProof.Tests.ps1` SHA-256 `3CE9BB785ADBA032676E6F4A381D30C8E837949501DD7205E805D3B588225A9C`
