# Artifact Storage Route Proof — Phase B result

このsnapshotはPhase A revision `5DDB850ECC9F05DFFE666CAE752BF6C7AA4F8A5620883CA5203FB47D3AD0353F`を実装対象にしたimplementation head `27cc63b1143775815944f45ea8b3a4508f8f44af`の最終Phase B記録である。Phase Cの所見・結論は含めない。

- Phase A snapshot: `artifacts/route-proof-phase-c-78a9d6c/blind-audit/snapshots/phase-a.md`（SHA-256は上記）
- implementation base: `93d2a1c436361ef6ee702096a61087cde55319b4`
- implementation head: `27cc63b1143775815944f45ea8b3a4508f8f44af`
- generated at: `2026-09-27T23:57:29Z`
- 担当: Codex（GPT-6、variant未確認）
- PowerShell: `7.6.5`
- .NET SDK: `10.0.401`

## 実装結果

- credential store、通信transport、route手順の責務をPhase Aの計画範囲に保った。署名無しHTTP requestのmethod・URI・認証要素・redirect方針はtransportが所有し、pass/fail判定はRouteProofが所有する。
- unsigned request factoryとredirect無効handler factoryを本番通信とoffline試験で共有した。試験は実factoryを呼び、GET、固定bucket/key URI、空query、Authorizationなし、redirect無効を検査する。
- 子processの通信待ちと停止確認は同じ最大45秒予算を共有する。通信待ちに最大30秒を使い、残予算でprocess treeの終了とstdout/stderrのpipe完了を確認する。期限後の固定追加待機は行わず、確認できない場合は回復DELETEを抑止する。
- lock拒否の判定は403、`forbidden`、`ObjectLockedByBucketPolicy`の一致に加え、timeoutなし、EOF確認済み、上限未到達、redirectなしを要求する。
- credential callbackのtimeout/cancelで、PowerShell各stream、Host、Console、例外本文に含めたdummy sentinelが外部へ出ないことを追加確認する。実credentialは使用しない。
- 日本語コメントで、request共有理由、終了確認の時間境界、lock拒否の完了条件、secret寿命と出力遮断の検証理由を記録した。

## 検証結果

- `dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore`: 成功、警告0、エラー0。
- `pwsh -NoProfile -File tools/Artifacts/tests/R2RouteTransport.Tests.ps1`: 5 passed。
- `pwsh -NoProfile -File tools/Artifacts/tests/RouteProof.Tests.ps1`: 20 passed。production 12操作loop、別pwsh JSON境界、有限な子process停止確認、pipe予算共有、lock応答のEOF/timeout/redirect分類を含む。
- `pwsh -NoProfile -File tools/Artifacts/tests/Credentials.Tests.ps1`: 22 passed、failed 0。所有WindowsユーザーのACL変更可能な権限で実行。
- `pwsh tools/contract-audit.ps1`: pass。
- `pwsh tools/docs-audit.ps1`: pass。
- `git diff --check BASE..HEAD -- tools/Artifacts`: pass。全差分のcheckでは旧handoff `ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_B_REVISION_RESULT_RERUN.md:44`に既存の末尾空行が報告された。

## 未実行・適用除外

実R2通信、実credentialを用いたroute proof、Cloudflare owner設定の前後記録、Unity/EditMode、Player/Buildは未実行。endpointが未設定でownerのBucket Lock rule前後記録もないため、A3必須証拠に必要な実R2確認は未開始である。Unity側を変更していないため全EditMode回帰はPhase A3記載の理由により適用除外とし、PowerShell/.NET suiteと機械監査を代替証拠とする。これらの結果だけでは実R2の非公開性やprovider能力を証明しない。

## source / generated DLL SHA-256

- `tools/Artifacts/Probe/RouteProof.ps1`: `04564F9C434A08FF8D1E8470EAE7BFDBC5DA5042E7F649D1D9EB11FFCF4D8ACE`
- `tools/Artifacts/Probe/R2RouteTransport.cs`: `05B356C6E5A9B6FA4BC3DA9891C1FCD638D75BFE458E89348FA4E501694D3948`
- `tools/Artifacts/Probe/R2RouteTransport.psm1`: `F61006E34C55FF26D77EF29A5A494B277ECC41336D4D9560218E0F4DBD9F8F9B`
- `tools/Artifacts/tests/RouteProof.Tests.ps1`: `1569170C4C1374F93685C5C0D476AF5FAAB7BF672407DB21EBFC924AEDEFD1AE`
- `tools/Artifacts/tests/R2RouteTransport.Tests.ps1`: `B555D61175E53A5F44E17F6FF7263A404EECD7D38BB22B1D6A4C7F19E2FB0368`
- `tools/Artifacts/tests/Credentials.Tests.ps1`: `9E00307C95CA0D22276C7D96759730918C5420BE03288A9716E1A7492B3C1F27`
- `tools/Artifacts/Probe/artifacts/route-transport/R2RouteTransport.dll`: `1CC94DC94DDE98330577F0CEA0EB82768B2084D5304DF98878C173D976395627`
