# Artifact Storage Route Proof — Phase B result

このsnapshotは実装head `78a9d6c1b3d27eda8b85ebf60edffdf75d0d2928` の実装内容とPhase B検証を記録する。レビュー所見、Phase Cの結論、疑念候補を含めない。

- Phase A revision snapshot: `docs/handoff/ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION.md`
- Phase A snapshot SHA-256: `5DDB850ECC9F05DFFE666CAE752BF6C7AA4F8A5620883CA5203FB47D3AD0353F`（UTF-8 no BOM file bytes）
- implementation base: `93d2a1c436361ef6ee702096a61087cde55319b4`
- implementation head: `78a9d6c1b3d27eda8b85ebf60edffdf75d0d2928`
- generated at: `2026-09-27T23:13:34Z`
- 担当: Codex（GPT-6、variant未確認）
- PowerShell: `7.6.5`
- .NET SDK: `10.0.401`

## 実装結果

- unsigned GETの拒否判定は`401 / unauthorized / Unauthorized`または`403 / forbidden / AccessDenied`の対応するtupleだけを許可する。交差した応答は拒否根拠にしない。
- response bodyがtimeoutまたはEOF前のI/O切断で中断されても、期待長N byteを既に受信していればprefix hashを保持する。EOF未確認の全体hashは作らない。
- child timeout後はprocess treeのkillを要求し、root終了だけでなくstdout/stderr両pipeのEOFまで確認できた場合だけ停止確認済みとする。pipe待機は同じ45秒の残予算を共有し、stdout待機後にstderr用の残りを再計算する。
- locked keyのPUT応答だけで`retained-by-lock`を出さず、全操作後のauthenticated GET確認までは中間cleanupを`unconfirmed`とする。
- childはrun markerと一致する固定PUT fixtureだけを許し、byte長・SHA-256・run-idを検証する。payloadのbase64入力長も1024 byte上限相当に制限し、PUT以外のpayloadを拒否する。
- 入力revisionが不正な場合は未検証文字列をresultへ返さず、40桁hexのcommit IDか空値だけを記録する。
- 日本語コメントで、tuple照合、部分hash保持、shared pipe budget、停止確認、lock保持確定境界、child fixtureとrevision値域の根拠を説明した。

## 検証結果

- `dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore`: 成功、警告0、エラー0。
- `pwsh -NoProfile -File tools/Artifacts/tests/R2RouteTransport.Tests.ps1`: 4 passed。S3 code chunk、byte上限、timeout、I/O切断のprefix保持を確認。
- `pwsh -NoProfile -File tools/Artifacts/tests/RouteProof.Tests.ps1`: 20 passed。12操作loop、別pwsh JSON/stdio往復、shared pipe予算、停止判定、child payload、revision出力、cleanup分類を含む。
- `pwsh -NoProfile -File tools/Artifacts/tests/Credentials.Tests.ps1`: 21 passed、failed 0。所有Windowsユーザー権限で実行。
- `pwsh tools/contract-audit.ps1`: pass。
- `pwsh tools/docs-audit.ps1`: pass。
- `git diff --check 93d2a1c436361ef6ee702096a61087cde55319b4 78a9d6c1b3d27eda8b85ebf60edffdf75d0d2928`: 新しい実装差分に空白エラーなし。docs auditは履歴に含まれる従前Phase B snapshotの末尾空行warningを報告。

## 未実行・適用除外

実R2通信、実credentialを用いたRoute proof、Cloudflare owner設定の前後記録、Unity/EditMode、Player/Build、Phase C/C'は未実行。Unity側を変更していないため全EditMode回帰はA3記載の理由により適用除外とし、PowerShell/.NET suiteと機械監査を代替証拠とする。offlineのchild process試験は別pwshとのJSON/stdioを通すが、R2 transportとCredentialStoreを使う実child操作は行わない。これらの結果だけではprovider能力、実R2の非公開性、Bucket Lockの実効性を証明しない。

## source / generated DLL SHA-256

- `tools/Artifacts/Probe/RouteProof.ps1`: `06651DAA02CD50F92C64845B12FFECE9AD32D3C38220C605B37A73F5E0D61E56`
- `tools/Artifacts/Probe/R2RouteTransport.cs`: `52319E724798CA6C00D7A229723F29E0CFD8CBB71115EB7BD6FBCB62B0F76772`
- `tools/Artifacts/Probe/R2RouteTransport.psm1`: `F61006E34C55FF26D77EF29A5A494B277ECC41336D4D9560218E0F4DBD9F8F9B`
- `tools/Artifacts/tests/RouteProof.Tests.ps1`: `EFDF989D13F9B17294381D72A7BFE5CC2E47D90947B69F7D7713F9F4B0F31233`
- `tools/Artifacts/tests/R2RouteTransport.Tests.ps1`: `FCE2AA4F3900AB27AA794665DB1D9A2BD86ABA47D75FA4BDD1EA504CF7F75910`
- `tools/Artifacts/Probe/artifacts/route-transport/R2RouteTransport.dll`: `6673391EA06B1A00E760598F510DE29F86678C324F1A95AB4469443AFDCB67A6`
