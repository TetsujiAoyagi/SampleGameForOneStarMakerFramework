# Artifact Storage Route Proof — Phase B result

この文書は凍結A3 revision 2に対するPhase Bの実装結果であり、Phase C/C'の所見・判定を含めない。

- Phase A snapshot: `docs/handoff/ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION.md`、UTF-8/LF SHA-256 `91BEA838FCE7C3C2092E7CA1B9DB58D896C1737CE502513E723B248F5C573217`、凍結commit `2af742ffc4f9cd27573759774d0ffff27b4876a5`。
- implementation base: `93d2a1c436361ef6ee702096a61087cde55319b4`。今回の実装修正開始点: `da4e405a0c5019a2f0edd857e1a2c11b61432c34`。
- implementation head: `bd7b7e5ea53073e8af962beb9a15223a8890875c`。作成UTC: `2026-09-28T15:45:19Z`。担当: Codex / GPT-6 Sol、Phase B。

## B1: 有限なオフライン照合

旧B result `ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_B_RESULT_DA4E405.md` のDLL SHA-256は `274F47FA6835371ADEB02DBC48286200B5931CA97011BFC3E2298BBB702CC1CD`。固定bundleの `files.sha256` とB開始時の現存DLLは `5842970B2515EED5E2E56A93870AEBDCEF5D103951E4818DCAAD2E7ABB7C23E2`。旧run `artifacts/route-proof-phase-c-da4e405/live-run/probe-output.jsonl` のSHA-256は `A807B04350035FE4613F032950766C9AED6040642E8EB5D335117391C78EFF9E`。旧build logは成功だが、記載DLL hashとbundle/実体の不一致により旧runがどのDLLをロードしたか確定できない。今回のbuildで旧runを遡及証明しない。

正規GETについて、(1) source・fixtureのmethod/同一key/segment escape済みURI、(2) Authorization/署名・対象変更queryの不在とredirect無効、(3) source→DLL load→child JSON→親判定の既存経路をオフラインで確認した。`CreateUnsignedRequest`はGETと`BuildObjectUri`を用い、`SendAsync`はそのrequestを送る。親は同じstepのkeyから期待URIを作り、観測したmethod/URI/auth/queryを照合する。既存transport回帰7件も通過した。正規条件への具体的違反は見つからず、400/InvalidArgumentの原因は未特定。GET経路の推測修正は行っていない。

## B2/B3: 実装

- `Read-LockRule`は厳密な10 fieldのBefore入力のみを受け取り、`AfterHash`混入・BeforeHash欠落/型不正・未知field・不正ruleを拒否する。出力の`lockRule.afterHash`は常にnull。正常な12操作ループでもnullを確認した。
- `tools/Artifacts/README.md`の入力説明と実行例を10 fieldに更新した。実際のAfter設定はPhase Cが独立した事後原記録で照合する。
- `RouteProof.Tests.ps1`の一時fixtureは本番`RouteProof.ps1`をbyte-identicalにコピーしてSHA-256一致を確認する。相対import先をtest専用store/transport moduleに限定し、別`pwsh -File <copy> -Child`を通す。正常childはexit 0・stdout 1 JSON・stderr空・operation/Generation一致。不正profileはexit 3、期限切れはexit 4で、いずれもtransport未呼出し。全経路でdummy secretの出力なし。test childの待機・終了・pipe回収には有限の期限を設けた。

## B4: オフライン検証と未実行

- `RouteProof.Tests.ps1` の変更関連 `-Case` 限定回帰: 12件成功、0件失敗。`R2RouteTransport.Tests.ps1`: 7件成功、0件失敗。
- `dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore`: 成功、警告0・エラー0。最初のsandbox実行は`obj`書込み拒否で失敗し、承認済みのsandbox外経路で同じcommandを再実行して成功。target net8.0、既存AWSSDK.S3 3.7.501.14、.NET SDK 10.0.401。buildは通信成功の証拠ではない。
- `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、`git diff --check`: 成功。
- 新buildの `R2RouteTransport.dll` SHA-256: `67EB1207A7CF9FF8D333A6E302EF069865418B4BD78F47A7A373B80646DDF6E3`。関連source `R2RouteTransport.cs`: `F9089FA32439FAFDEBD74F225328E575907E37EA028BA6926CB3629E75B08C8D`、`RouteProof.ps1`: `0031E946D0B5BC53CF0ED2E7E3E7F1F9FEF05AA26275372136D7382F4201A821`、test `RouteProof.Tests.ps1`: `A7B41E3D3B768260F979B8DC180A01BC49B296E7D7D43A849AF3459D318E0EC4`。
- R2への通信、実profileやBucket Lock設定の変更、実After観測、Phase Cの全件判定回帰/固定evidence、Phase C'は未実行。Unity側に変更はなく、EditMode/PlayMode/buildも未実行。

Bの引渡し条件を満たしたため、400未解決のままPhase Bを終了する。sliceのGO、R2採用、Phase C/C'完了を意味しない。
