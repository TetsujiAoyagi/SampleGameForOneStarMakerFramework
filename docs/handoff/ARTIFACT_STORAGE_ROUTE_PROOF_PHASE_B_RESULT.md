# Artifact Storage Route Proof — Phase B result

この文書は凍結A3 revision 2に対するPhase Bの実装結果であり、Phase C/C'の所見・判定を含めない。

- Phase A snapshot: `docs/handoff/ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION.md`、UTF-8/LF SHA-256 `91BEA838FCE7C3C2092E7CA1B9DB58D896C1737CE502513E723B248F5C573217`、凍結commit `2af742ffc4f9cd27573759774d0ffff27b4876a5`。
- implementation base: `93d2a1c436361ef6ee702096a61087cde55319b4`。今回の実装修正開始点: `da4e405a0c5019a2f0edd857e1a2c11b61432c34`。
- implementation head: `8c1793ec507205d5134da5a9fd22b6d12c33cb56`。作成UTC: `2026-09-28T15:51:57Z`。担当: Codex / GPT-6 Sol、Phase B。

## B1: 有限なオフライン照合

旧B result `ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_B_RESULT_DA4E405.md` のDLL SHA-256は `274F47FA6835371ADEB02DBC48286200B5931CA97011BFC3E2298BBB702CC1CD`。固定bundleの `files.sha256` とB開始時の現存DLLは `5842970B2515EED5E2E56A93870AEBDCEF5D103951E4818DCAAD2E7ABB7C23E2`。旧run `artifacts/route-proof-phase-c-da4e405/live-run/probe-output.jsonl` のSHA-256は `A807B04350035FE4613F032950766C9AED6040642E8EB5D335117391C78EFF9E`。旧build logは成功だが、記載DLL hashとbundle/実体の不一致により旧runがどのDLLをロードしたか確定できない。今回のbuildで旧runを遡及証明しない。

正規GETについて、(1) source・fixtureのmethod/同一key/segment escape済みURI、(2) Authorization/署名・対象変更queryの不在とredirect無効、(3) source→DLL load→child JSON→親判定の既存経路をオフラインで確認した。`CreateUnsignedRequest`はGETと`BuildObjectUri`を用い、`SendAsync`はそのrequestを送る。親は同じstepのkeyから期待URIを作り、観測したmethod/URI/auth/queryを照合する。既存transport回帰7件も通過した。正規条件への具体的違反は見つからず、400/InvalidArgumentの原因は未特定。GET経路の推測修正は行っていない。

## B2/B3: 実装

- `Read-LockRule`は厳密な10 fieldのBefore入力のみを受け取り、`AfterHash`混入・BeforeHash欠落/型不正・未知field・不正ruleを拒否する。出力の`lockRule.afterHash`は常にnull。正常な12操作ループでもnullを確認した。
- `tools/Artifacts/README.md`の入力説明と実行例を10 fieldに更新した。実際のAfter設定はPhase Cが独立した事後原記録で照合する。既存runの400/inconclusive・GOなしも現況へ反映した。
- `RouteProof.Tests.ps1`の一時fixtureは本番`RouteProof.ps1`をbyte-identicalにコピーしてSHA-256一致を確認する。相対import先をtest専用store/transport moduleに限定し、別`pwsh -File <copy> -Child`を通す。正常childはexit 0・stdout 1 JSON・stderr空・operation/Generation一致。不正profileはexit 3、期限切れはexit 4で、いずれもtransport未呼出し。全経路でdummy secretの出力なし。test childの待機・終了・pipe回収には有限の期限を設けた。

## B4: オフライン検証と未実行

- `RouteProof.Tests.ps1` の変更関連 `-Case` 限定回帰: 12件成功、0件失敗。`R2RouteTransport.Tests.ps1`: 7件成功、0件失敗。
- `dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore`: 成功、警告0・エラー0。最初のsandbox実行は`obj`書込み拒否で失敗し、承認済みのsandbox外経路で同じcommandを再実行して成功。target net8.0、既存AWSSDK.S3 3.7.501.14、.NET SDK 10.0.401。buildは通信成功の証拠ではない。
- `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、`git diff --check`: 成功。
- 新buildの `R2RouteTransport.dll` SHA-256: `67EB1207A7CF9FF8D333A6E302EF069865418B4BD78F47A7A373B80646DDF6E3`。関連source `R2RouteTransport.cs`: `F9089FA32439FAFDEBD74F225328E575907E37EA028BA6926CB3629E75B08C8D`、`RouteProof.ps1`: `0031E946D0B5BC53CF0ED2E7E3E7F1F9FEF05AA26275372136D7382F4201A821`、test `RouteProof.Tests.ps1`: `A7B41E3D3B768260F979B8DC180A01BC49B296E7D7D43A849AF3459D318E0EC4`、`README.md`: `D385A94885ED4019DB7FFD2BE5B57DEE200E3FED1FBDF72FE73A8A002678D619`。
- R2への通信、実profileやBucket Lock設定の変更、実After観測、Phase Cの全件判定回帰/固定evidence、Phase C'は未実行。Unity側に変更はなく、EditMode/PlayMode/buildも未実行。

Bの引渡し条件を満たしたため、400未解決のままPhase Bを終了する。sliceのGO、R2採用、Phase C/C'完了を意味しない。

## Phase A revision 3 に対する局所 B 適応

- Phase A snapshot: `docs/handoff/ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION_3.md`、UTF-8/LF SHA-256 `F34E67FA3747F84C6C11759B2CA46D67303ED01A0DDBC69AE5777C859FF78F57`、凍結 commit `5172d597cd2725f3cff4305d9bddce237071c685`。source-first 候補は `f256252f31bd90c308b63f97a301539ad7e1cfc8`。
- 実装 head: `31be639a968402e0bdf3f65fd9eda8d0d818dbf3`（2026-09-28T16:42:10Z）。`Test-UnsignedPrivacy` の限定400判定に `-not $Observation.TimedOut` を追加した。`400 / other / InvalidArgument`、`EofConfirmed=true`、異なる本文・prefix hash でも `TimedOut=true` なら非合格になる回帰を追加した。既存の status/class/Code、EOF、上限、redirect、hash の判定は維持した。
- `RouteProof.Tests.ps1` の関連 `-Case @('unsigned*','timeout*','production loop completes*')`: 6件成功、0件失敗。全 `RouteProof.Tests.ps1`: 26件成功、0件失敗。`pwsh tools/contract-audit.ps1` と `git diff --check`: 成功。
- 実装 head の SHA-256: `RouteProof.ps1` は `F70917424219EC6487199C6A5155B08729375959308902E4A63AC18653798CE4`、`RouteProof.Tests.ps1` は `5FFD39CBCF7F2123109D82FAE753E03B160650A6A2080911B6EE644430594B6B`。
- README、transport、credential store、設定は変更していない。R2 通信、実 profile / Bucket Lock 設定の変更、実 After 観測、DLL build、Phase C の判定必須検証と固定 evidence、Phase C'、Unity の Editor compile / EditMode / PlayMode / build は未実行。今回の変更は PowerShell 2ファイルだけで、R2 の実経路や slice GO を証明しない。

## Phase A revision 4 に対する局所 B 適応

- Phase A snapshot: `docs/handoff/ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION_4.md`、UTF-8/LF SHA-256 `C349E7281FD45673C08BFE702EEC003B634AC0C16DAA97EBAF8328083AAF34FA`、凍結commit `3022005b7275fa4ef557ee13811bcc56c7ece52f`。実装開始点 `31be639a968402e0bdf3f65fd9eda8d0d818dbf3`、開始時docs tip `3022005b7275fa4ef557ee13811bcc56c7ece52f`。
- 実装 head: `9c46b5837796de23465f6d9c059bb326c2f06858`（2026-09-28T17:22:14Z）。以降の本 B result 更新commitはdocs-onlyであり、実装headに含めない。
- revision 3 のlocked上書きで `409 / other / ObjectLockedByBucketPolicy` を観測したため、`Test-LockRejection`にこの正確な同一応答tupleを追加した。既存の `403 / forbidden / ObjectLockedByBucketPolicy` とtimeout・EOF・redirect・limit guardを維持し、汎用409やstatus/classの交差値は拒否する。production loopの同一Generation、保持内、陽性対照、最終認証GETの元byte数・hash照合は変更していない。
- `RouteProof.Tests.ps1`で403/409の許可tuple、不許可Code・交差class・timeout・EOF不明・limit・redirectを確認した。本番12操作loopとJSON境界を通し、locked上書きとDELETEの両方が409でも元hash GET後に完走し、最終GETがchanged hashならinconclusiveになることを確認した。`tools/Artifacts/README.md`にも限定tupleと最終GET条件、旧runの未到達範囲を反映した。
- 関連 `-Case @('lock*','production*','unsigned*','timeout*','transport timeout*','pipe budget*','stdout*','child stop*','ambiguous initial*','unconfirmed child*')`: 16件成功、0件失敗。`dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore`: 成功、警告0・エラー0。`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、`git diff --check`: 成功。
- 実装 head 時点のSHA-256: `RouteProof.ps1` `EEC708C0BDC14650CCF7E6C0F1FD6521AD1149E7C25B237C09255508533572E4`、`RouteProof.Tests.ps1` `B305F3E6BCE312B247E14D82142121C81DCEB3984030085D987778A149AC3849`、`tools/Artifacts/README.md` `401BA80AC5A5F45A6A40BE38A4A760FAD8E774DAD3C8FF1407EAD971AEFC4BDE`、build後の `R2RouteTransport.dll` `EA45A0314056F88CBCDEEEBD9304D49A0B96C2B20B7C2B287462E9C924ADA7F6`。SHAは作業ツリーのファイルbyteに対する値。
- R2RouteTransport.cs / SDK / CredentialStore / profile / ruleは変更していない。R2通信、実前後設定取得、Phase Cの全件回帰・live・GO判定、Phase C'、Unity Editor compile / EditMode / PlayMode / buildは未実行。今回のbuildとoffline回帰は実R2のLock実効性を証明しない。
