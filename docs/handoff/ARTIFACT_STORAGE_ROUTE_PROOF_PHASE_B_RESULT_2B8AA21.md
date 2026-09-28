# Artifact Storage Route Proof — Phase B result

このsnapshotは凍結Phase A revision `5DDB850ECC9F05DFFE666CAE752BF6C7AA4F8A5620883CA5203FB47D3AD0353F`を実装対象にしたimplementation head `2b8aa218c36f336562d31c5b45399000460a4c13`の最終Phase B記録である。Phase Cの所見・結論は含めない。

- Phase A snapshot: `artifacts/route-proof-phase-c-78a9d6c/blind-audit/snapshots/phase-a.md`（SHA-256は上記）
- implementation base: `93d2a1c436361ef6ee702096a61087cde55319b4`
- implementation head: `2b8aa218c36f336562d31c5b45399000460a4c13`
- generated at: `2026-09-28T03:07:42Z`
- 担当: Codex（GPT-6、variant未確認）
- PowerShell: `7.6.5`
- .NET SDK: `10.0.401`

## 実装結果

- child processを起動する直前に単調時計のtimestampを記録し、子へ非秘密の値として渡す。子はcredential profile読取後、transport callbackを呼ぶ直前に起動時点からの経過時間を引き、operationに残った期限だけをtransportへ設定する。
- 最大45秒の子予算を、transport操作、子の結果JSON返却、process treeとstdout/stderr pipeの終了確認に分けた。標準予算ではtransportに最大29秒、結果返却に1秒、停止確認に15秒を置き、親のprocess waitを30秒以内にする。run残時間が短い場合は全枠を短縮し、未実行/未確認をinconclusiveに保つ。
- unsigned request factoryとredirect無効handler factoryは本番通信とoffline検査で共有する。試験は実factoryを呼び、GET、固定bucket/key URI、空query、Authorizationなし、redirect無効を検査する。
- lock拒否の判定は403、`forbidden`、`ObjectLockedByBucketPolicy`の一致に加え、timeoutなし、EOF確認済み、上限未到達、redirectなしを要求する。
- credential callbackのtimeout/cancelで、PowerShell各stream、Host、Console、例外本文に含めたdummy sentinelが外部へ出ないことを確認する。実credentialは使用しない。
- 日本語コメントで、request境界、単調時計とdeadline配分、pipe完了条件、lock拒否の完了条件、秘密出力を遮断する理由を記録した。

## 検証結果

- `dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore`: 成功、警告0、エラー0。
- `pwsh -NoProfile -File tools/Artifacts/tests/R2RouteTransport.Tests.ps1`: 5 passed。
- `pwsh -NoProfile -File tools/Artifacts/tests/RouteProof.Tests.ps1`: 22 passed。production 12操作loop、別pwsh JSON境界、process停止と両pipe完了、親子で共有するoperation deadline、cross-process monotonic timestampを含む。
- `pwsh -NoProfile -File tools/Artifacts/tests/Credentials.Tests.ps1`: 22 passed、failed 0。所有WindowsユーザーのACL変更可能な権限で実行。
- `pwsh tools/contract-audit.ps1`: pass。
- `pwsh tools/docs-audit.ps1`: pass。
- `git diff --check BASE..HEAD -- tools/Artifacts`: pass。

## 未実行・適用除外

実R2通信、実credentialを用いたroute proof、Cloudflare owner設定の前後記録、Unity/EditMode、Player/Buildは未実行。endpointが未設定でownerのBucket Lock rule前後記録もないため、A3必須証拠に必要な実R2確認は未開始である。Unity側を変更していないため全EditMode回帰はPhase A3記載の理由により適用除外とし、PowerShell/.NET suiteと機械監査を代替証拠とする。これらの結果だけでは実R2の非公開性やprovider能力を証明しない。

## source / generated DLL SHA-256

- `tools/Artifacts/Probe/RouteProof.ps1`: `1414B0D932B9B68AF89746FB4D783E78D59F56589F31D4AE9A08576A55CD4503`
- `tools/Artifacts/Probe/R2RouteTransport.cs`: `05B356C6E5A9B6FA4BC3DA9891C1FCD638D75BFE458E89348FA4E501694D3948`
- `tools/Artifacts/Probe/R2RouteTransport.psm1`: `F61006E34C55FF26D77EF29A5A494B277ECC41336D4D9560218E0F4DBD9F8F9B`
- `tools/Artifacts/tests/RouteProof.Tests.ps1`: `F84D65C329D474BFFB2222FF093C7B77C1BA9F994967B846FC03715A1FC39158`
- `tools/Artifacts/tests/R2RouteTransport.Tests.ps1`: `B555D61175E53A5F44E17F6FF7263A404EECD7D38BB22B1D6A4C7F19E2FB0368`
- `tools/Artifacts/tests/Credentials.Tests.ps1`: `9E00307C95CA0D22276C7D96759730918C5420BE03288A9716E1A7492B3C1F27`
- `tools/Artifacts/Probe/artifacts/route-transport/R2RouteTransport.dll`: `EDCA3AEB40B89C1DEDD4D595CF179DE28B4A14B086300608F2EB19624D2B71C7`
