# Artifact Storage Route Proof — Phase B result

このsnapshotは実装head `1356d6705cb8b7af120755bda2ac8c13d8170b34` の実装内容とPhase B検証を記録する。レビュー所見、Phase Cの結論、疑念候補を含めない。

- Phase A revision snapshot: `docs/handoff/ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION.md`
- Phase A snapshot SHA-256: `5DDB850ECC9F05DFFE666CAE752BF6C7AA4F8A5620883CA5203FB47D3AD0353F`（UTF-8 no BOM file bytes）
- implementation base: `93d2a1c436361ef6ee702096a61087cde55319b4`
- implementation head: `1356d6705cb8b7af120755bda2ac8c13d8170b34`
- generated at: `2026-09-27T22:45:57Z`
- 担当: Codex（実装）
- PowerShell: `7.6.5`
- .NET SDK: `10.0.401`

## 実装結果

- unsigned GETの拒否証拠を、`401 / unauthorized / Unauthorized` または `403 / forbidden / AccessDenied` の同一応答tupleに限定した。HTTP 400は引き続き`inconclusive`とする。
- body readが期限で中断されても、既に取得した期待長prefix hashを返し、EOFと全体hashを未確定のままにする。
- lock対象のoverwrite/delete成功応答だけでprovider failureへ分類しない。保持状態を裏付ける再GET証拠がない場合は`inconclusive`に留める。
- evidenceへ記録するbase/headを必須引数で固定し、親子process双方で40桁commit ID形式を検証する。実行時のHEADやmerge-baseを推測して記録しない。
- childの期限・pipe・例外経路で終了確認できない場合、回復DELETEとの競合を避け、cleanupを未確認のまま残す。
- offline suiteは実装と同じ12操作ループを通り、別pwsh processから返る単一JSON行をparseしてschema検証する。prefix露出、曖昧PUT後の回復期限、停止未確認時のcleanup抑止も確認する。
- 日本語コメントで、対応するstatus tuple、partial hash保持、固定revision、lock成功応答の扱い、子停止確認とcleanup順序の根拠を記録した。

## 検証結果

- `dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore`: 成功、警告0、エラー0。
- `pwsh -NoProfile -File tools/Artifacts/tests/R2RouteTransport.Tests.ps1`: 3 passed。S3 codeのchunk境界、上限時prefix、取消し時prefixを確認。
- `pwsh -NoProfile -File tools/Artifacts/tests/RouteProof.Tests.ps1`: 14 passed。12操作成功経路、実pwsh子process/JSON往復、status tuple、期限、cleanup確認を含む。
- `pwsh -NoProfile -File tools/Artifacts/tests/Credentials.Tests.ps1`: 21 passed、failed 0。所有Windowsユーザー権限で実行。
- `pwsh tools/contract-audit.ps1`: pass。
- `pwsh tools/docs-audit.ps1`: pass。
- `git diff --check 93d2a1c436361ef6ee702096a61087cde55319b4 1356d6705cb8b7af120755bda2ac8c13d8170b34`: 新しい実装・テスト差分に空白エラーなし。履歴に含まれる従前のB snapshot末尾空行についてwarning 1件。

## 未実行・適用除外

実R2通信、実credentialを用いたRoute proof、Cloudflare owner設定の前後記録、Unity/EditMode、Player/Build、Phase C/C'は未実行。Unity側ファイルを変更していないため全EditMode回帰はA3記載の理由により適用除外とし、PowerShell/.NET suiteと機械監査を代替証拠とする。offline子process試験は別pwshとのJSON/stdio境界を実際に通すが、R2 transport・CredentialStoreを使う実child操作は行わない。これらの結果はprovider能力、実R2の非公開性、Bucket Lockの実効性を証明しない。

## source / generated DLL SHA-256

- `tools/Artifacts/Probe/RouteProof.ps1`: `73FEEF62253C51B429A7188068FF75B8B49AE456C77DC177E35CE108738882EB`
- `tools/Artifacts/Probe/R2RouteTransport.cs`: `5B9A64318F16E5306EA84C676C1458899CE8F39E062D52AB0DB261934BD9D6A9`
- `tools/Artifacts/Probe/R2RouteTransport.psm1`: `F61006E34C55FF26D77EF29A5A494B277ECC41336D4D9560218E0F4DBD9F8F9B`
- `tools/Artifacts/tests/RouteProof.Tests.ps1`: `A3245026CDBCE3FA8715087BAD144ACA702208BB88419B9E79BD5610B3D4D21F`
- `tools/Artifacts/tests/R2RouteTransport.Tests.ps1`: `A76C27EAC0DB8701EB0DE8C3A680F27EE523253EC0EB1C1F3CDD6418D2900193`
- `tools/Artifacts/Probe/artifacts/route-transport/R2RouteTransport.dll`: `D4B4B21B19112103877D3DEBEF906AE82494090C5D53B0513F6BDAA93A85101F`（Phase C bundleに収録した実行DLL）
