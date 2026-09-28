# Artifact Storage Route Proof — 進行 HANDOFF

## 0. 現在の入力

- type: slice / status: revision 4 A3 凍結済み。revision 3 の B head 31be639 に対する判定 C は locked 上書きの409で inconclusive、§11に保存。revision 4 の局所 B / 新 C / C' は未実施。GOなし。
- branch: codex/artifact-storage-route-proof / risk: high
- owner: repository owner / created: 2026-09-29 JST / expires: Phase D または次の A revision
- harvest to: tools/Artifacts/README.md、BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md の現況
- implementation base: 93d2a1c436361ef6ee702096a61087cde55319b4
- 既存 implementation head / 新 B の開始点: da4e405a0c5019a2f0edd857e1a2c11b61432c34
- A 開始時 docs tip: dfbed2fdd48027ccbdcaecbbd8124da30019a362
- 最新の実装 head / revision 4 B開始点: 31be639a968402e0bdf3f65fd9eda8d0d818dbf3。旧 B head: 8c1793ec507205d5134da5a9fd22b6d12c33cb56。B result: [ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_B_RESULT.md](ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_B_RESULT.md)（局所適応を追記済み）。revision 4 の新判定 evidence / C' bundle: 未生成。docs-only commitはimplementation headにしない。
- 現行 Phase A 規範本文: [ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION_4.md](ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION_4.md)。旧凍結規範: [revision 3](ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION_3.md)、[revision 2](ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION.md)。本台帳は議論・errata・進行を持ち、C' に渡さない。
- A3 snapshot 生成 UTC: 2026-09-28T15:26:42Z。
- A3 snapshot SHA-256（UTF-8 / LF）: 91BEA838FCE7C3C2092E7CA1B9DB58D896C1737CE502513E723B248F5C573217。CRLF checkout では LF に正規化して照合する。Git blob: 16a78f2f46e902db0cab7f6a4a288f085faceeeb。
- 固定 commit: 本文書と snapshot を収録する docs-freeze-artifact-route-proof-a3-r2。自己参照する commit SHA は本文へ埋め込まず、引継ぎプロンプトに確定 SHA を示す。凍結 snapshot の内容は以後変更しない。
- レビュー統合済み候補: 2026-09-28T15:22:57Z、規範本文 SHA-256 662A26AED6B6010D7C9C115F21492A9134A82BE745CE427086DA0C598F03E380。これは承認前候補の識別子で、A3凍結記録ではない。
- revision 3 A3 snapshot 生成 UTC: 2026-09-28T16:34:42Z。SHA-256（UTF-8 / LF）: F34E67FA3747F84C6C11759B2CA46D67303ED01A0DDBC69AE5777C859FF78F57。CRLF checkout では LF に正規化して照合する。固定 commit は本台帳・snapshot・docs/README.md を収録する docs-freeze-artifact-route-proof-a3-r3。確定 SHA は引継ぎ時に指定し、snapshot 自体へ自己 hash を埋め込まない。
- revision 4 A3 snapshot 生成 UTC: 2026-09-28T17:05:32Z。SHA-256（UTF-8 / LF）: C349E7281FD45673C08BFE702EEC003B634AC0C16DAA97EBAF8328083AAF34FA。CRLF checkout では LF に正規化して照合する。固定 commit は本台帳・snapshot・docs/README.md を収録する docs-freeze-artifact-route-proof-a3-r4。確定 SHA は引継ぎ時に指定し、snapshot 自体へ自己 hash を埋め込まない。

本文書と現行規範本文を入力とする。新たな commit 別 RESULT / FINAL / RERUN は増やさない。旧固定 snapshot・結果は過去の対象版の証拠として保存する。§1〜§9 は revision 2 の履歴、§10〜§11 は revision 3、revision 4 の採否・現在の入力は §12 に記録する。

## 1. A0 — 固定した現況と問い

計画開始 docs tip は origin より 20 commit 先行し、対象は source/test 10 件、docs 10 件。既存 probe を全廃する根拠はない。R2 能力未証明と probe の実装完了を分ける。

旧 A3 は artifacts/route-proof-phase-c-2b8aa21/blind-audit/snapshots/phase-a.md、SHA-256 5DDB850ECC9F05DFFE666CAE752BF6C7AA4F8A5620883CA5203FB47D3AD0353F。旧 tracked Phase A 文書も計画開始 docs tip から復元できる。固定原本は変更しない。

旧 §6 は「旧A3への判定適合修復と必要な回帰試験をPhase Bに含める」「現行許可集合のまま400が続けば inconclusive で終了条件未達とする」「代替endpoint/request形を試すための実R2探索は後続へ送る」と定めた。400 非合格は正規条件違反の修正禁止ではない。現行 GET は静的には account endpoint の /osm-artifacts/<escaped-key>、GET、無署名、redirect 無効で、400 の実装原因は未確定。

既存実 run の一次記録:

- artifacts/route-proof-phase-c-da4e405/live-run/probe-output.jsonl / SHA-256 A807B04350035FE4613F032950766C9AED6040642E8EB5D335117391C78EFF9E。
- run-id 6b372c357cef44379298471d514e8e75、2026-09-28 14:19:07〜14:19:12 UTC。unlocked PUT 200、認証 GET 200/57 byte/hash 一致、unsigned GET 400/InvalidArgument/EOF、回復 DELETE 204、NoSuchKey 404。最終 inconclusive、lock 未到達。
- artifacts/route-proof-phase-c-da4e405/live-run/before-state.json / SHA-256 31622BAAF93994F64D0EBF672E929E4E34A5D7AEE7E1E659FB715404E8897C72、取得 UTC 2026-09-28T14:17:10.8062901Z。Age 86400 秒 / probe/locked/ の rule 1 件は既設。これは実効性・実 After 観測の証拠ではない。再設定依頼をしない。

da4e405 固定 bundle の発見 C に旧凍結条件への implementation blocker 指摘はなかった。GO / 判定 C / C' ではない。C' 未実施。追加 R2 通信は禁止されたまま。

### 証拠の errata と改訂動機（blind 入力外）

ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_B_RESULT_DA4E405.md の DLL hash は 274F47FA6835371ADEB02DBC48286200B5931CA97011BFC3E2298BBB702CC1CD、固定 bundle と現在の DLL 実体は 5842970B2515EED5E2E56A93870AEBDCEF5D103951E4818DCAAD2E7ABB7C23E2。source hash と bundle 内 23 entry の整合だけでは B 記載/build/live の対応を確定できない。原因未特定。元 B result/bundle/log を書換えず、旧 run を新 head の合格証拠へ転用しない。

起動前に同じ BeforeHash / AfterHash を入力しても未来の設定確認にならない。規範本文では Before 入力と実 After 原記録の責任を分ける。元 run の cleanup 成功を否定する変更ではない。

12 件の B/C 結果文書と古い main metadata に代えて、本台帳を現行入口へ整理する。旧固定物は D まで保持。artifacts は untracked のまま stage/commit 禁止。既存 tracked artifacts 191 件と履歴を削除・書換えしない。

問い・最低条件・責務・回帰・C 再開条件・停止は規範本文 §1〜§6。B は証拠対応の整理、Before/After 分離、実 child 入口回帰、立証した正規条件違反の局所修正まで。400 解消を B 完了条件にせず、slice GO 条件は緩めない。

## 2. A2 / A3 採否

- A0/A1 主担当: この計画セッションの Codex。実装担当にはしない。
- A0 のみの代替案: GPT-6 Sol、新規 subagent /root/a0_alternative。A1 非提示、read-only。狭い修復を凍結し同一 R2 再実行を B 完了条件にしない案を受領。
- A2 同一入力版: A1 v1。規範本文 SHA-256 A3D6ED0CD2870403B323837D31312C936DE9A4B1BC499AE2F82B76DAF3AD4AAB、A0 台帳 SHA-256 ABD73B2970F96279F6DA8485D1850329D1232CB1762C57B35948B1D6A5BD62AD。両担当が一致確認、相互所見を非提示。台帳 §0/§1 と規範本文を同じ入力にした。
- architecture reviewer: GPT-6 Astra / OpenAI、別 subagent /root/a2_architecture。責務・寿命・規模・テスト境界をレビュー。旧凍結本文の比較時に旧 A2/A3 履歴が検索出力に含まれたが、今回の他 A2 所見は非提示。
- failure / 実装可能性 reviewer: GPT-5.6 Sol / OpenAI、別 subagent /root/a2_feasibility。正常 child 入口、開始条件、失敗時の分類、有限停止をレビュー。
- 主担当採否: 下記を統合済み。両担当から修正文言について自身の指摘の閉鎖確認を取得。新たな網羅レビューは反復していない。
- 人間の A3 統合承認: 取得済み。2026-09-29 JST、owner の「あいよ、A3凍結しよ。あとSOLに渡すプロンプトを教えて」により、以下の採否・Bの範囲・停止境界を承認。
- C' 強化独立性: A/B/C 未関与系列または人間を開始時に確保。A2 に全候補系列を使い切らない。

採否（全て人間の最終統合承認済み）:

1. A0 代替案: 採用。同じ R2 request 再実行や400解消をB完了条件にせず、狭い修復を引き渡す。ただし原 slice の GO 最低条件をオフライン成功へ置き換えない。
2. Architecture / P2 / semantic / unique: 採用。AfterHash 入力変更時に README の例が壊れるため、B2 と責務マップへ当該説明・実行例の同時更新を追加。担当の閉鎖確認済み。
3. Architecture / P2 / semantic / unique: 採用。全 repo diff は C が非盲検で保管し、C' へは source/test/config/利用手順の完全実装 diff と列挙した clean 文書だけを渡す。旧所見の差分混入を防ぐ。担当の閉鎖確認済み。
4. Failure / high / semantic / unique: 採用。private の観測条件を Public Development URL 無効・Custom Domains 空と明記し、前後記録へ含める。前不足は environment-blocked、後変化・不明は inconclusive、いずれも GO 不可。担当の閉鎖確認済み。
5. Architecture / 明確化 / unique: 採用。旧条件の 429/TooManyRequests の組と1秒以上の間隔を省略せず本文へ転記。retry や能力否定条件を増やす変更ではない。

不採用: なし。保留は400の原因・実能力と将来live許可で、Bが埋める設計欄ではない。A2の残存指摘はなし。A/B/C/C' の成果判定は別々に記録する。

承認された統合判断: 規範本文の B1〜B4 を SOL の有限な実装範囲とし、400 未解決でもその引渡し条件を満たせば B 完了、slice GO / C' / R2通信許可にはしない境界で A3 凍結する。規範本文・本台帳・docs/README.md の3文書だけを docs commit で固定する。現在のセッションでは B を実装せず、別セッションの SOL へ渡す。

## 3. SOL 用開始プロンプト

**A3 統合承認済み。** §0 の snapshot hash と凍結 commit を確認し、別セッションの SOL が B を開始する。

~~~text
repository: D:\repositories\unity\SampleGameForOneStarMakerFramework
branch: codex/artifact-storage-route-proof
担当: Phase B。実装担当は SOL。

AGENTS.md、.agents/skills/osm-workflow/SKILL.md と必要参照、
docs/handoff/ARTIFACT_STORAGE_ROUTE_PROOF.md の §0 / §1、
docs/handoff/ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION.md を読む。
台帳の A3 承認と snapshot SHA-256 を確認。不一致・未承認なら実装しない。
規範本文を正とし、旧チャットや旧 C finding を追加の設計指示にしない。

slice base: 93d2a1c436361ef6ee702096a61087cde55319b4
修正開始点: da4e405a0c5019a2f0edd857e1a2c11b61432c34
docs-only commit と implementation head を分ける。

規範本文 §4 の B1→B4 を行う。正規条件への具体的違反だけ直し、
原因不明400を推測修正しない。Before/After分離と実child入口の
オフライン回帰を完成させ、限定確認後に B result を一つ残す。
400未解決でも規範のB引渡し条件を満たしたらそこで終了する。

R2通信、実profile再登録、設定変更、C/GO/C'開始は禁止。
実payload、代替endpoint、header探索、汎用CLI/Helper追加は範囲外。
artifactsはadd/commitしない。add -A、履歴書換えをしない。
commit時は変更したsource/test/docsだけ明示列挙する。
最終報告は変更、検証と未実行、実装head、B result、
400原因を立証できたか、C開始に残る条件を簡潔に示す。
~~~

## 6. Phase B

担当: Codex / GPT-6 Sol。revision 2 のB1〜B4を実施し、implementation headを 8c1793ec507205d5134da5a9fd22b6d12c33cb56 に固定した。変更・理由・限定オフライン検証・未実行・source/DLL hashは§0の汎用B resultに記録。旧runのDLL対応は未確定で、正規GET条件への具体的違反は見つからず、400/InvalidArgumentの原因は未特定。実R2通信は行っていない。Bの引渡し条件を満たして終了し、slice GOやPhase C/C'の判定には読み替えない。

## 7. Phase C

担当: Codex / GPT-6 Astra。B（GPT-6 Sol）と異なるモデルの新規セッションで発見レビューと検証を実施。固定 base は 93d2a1c436361ef6ee702096a61087cde55319b4、implementation head は 8c1793ec507205d5134da5a9fd22b6d12c33cb56。A3 の UTF-8/LF hash を照合し、全 repository diff / stat / name-status と改訂差分を保存した。凍結済み責務配置・契約・失敗経路への具体的な実装 blocker は未発見。547行の orchestration は既存の単一操作ループ・期限・清掃責務に留まり、transport と秘密の寿命は別の既存境界に維持されている。

オフライン検証は Release/net8.0 build（警告0・エラー0）、Credentials 22件、RouteProof `-Case '*'` 25件、transport 7件、contract-audit、docs-audit、完全実装 diff の `--check` が成功。Credentials は初回 sandbox の ACL 制約による17件失敗を保存し、承認済み sandbox 外経路で全22件成功。Unity は A3 の適用除外どおり未実行。全 repository diff の検査では旧 `ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_B_REVISION_RESULT_RERUN.md:44` の末尾空行警告のみを記録し、旧証拠を変更していない。

owner のオンライン許可と今回提出された Cloudflare 設定・token scope の原画像2件を受領した。原画像を無加工で owner/SYSTEM のみの ACL を持つ `live-run` へ保存し、hash、受領・記録 UTC、観察者、取得方法を事前原記録へ記載した。画像には r2.dev 無効、Custom Domains なし、multipart abort 7日のみ、`probe/locked/` の Age 86400秒 rule 1件、対象 bucket の Item Read/Write のみを確認。既存profile、清掃予定と厳密な10 field入力も固定した。撮影 UTC は画像に埋め込まれておらず、受領 UTC と混同していない。

2026-09-28T16:05:16.3540125Z〜16:05:24.3843680Z に sandbox 外で許可された1 runのみを実行した。run-id は `b01e9958a72e4e109250e89a4127d749`。unlocked PUT 200 → 別processの認証GET 200（57 byte・SHA-256一致）→ unsigned GET 400/InvalidArgument（113 byte、EOF確認、全体/prefix hashはobjectと異なる）で停止。回復DELETE 204 → GET 404/NoSuchKeyで削除を確認した。exit 4 / inconclusive。locked key は作成しておらず、今回の残存objectはない。追加のR2通信・profile/rule変更・同一試行の反復は行っていない。

source・DLL・依存hashは実行直前/直後に照合し一致、間にbuildや実行物copyはない。今回の DLL SHA-256 は `E16BDA342C3E3254CA468EC9BF3DF24189D25B1E80D1D20C522C7454E1F2D7FC`。旧runの不明なDLL対応は遡及証明しない。今回の Before 原記録 SHA-256 は `72C1E074424A642C64F5A398DF0DD0D2DA35F3273A6DEB3D02756600A9A34C68`、生JSONLは `E5C88DC406F4CC7CF224DC6DD1B8C46179F43E592D9B07B53134FD98D63AA5A3`。

証拠: `artifacts/route-proof-phase-c-8c1793e/`。最終 manifest `final-live-bundle.sha256` の SHA-256 は `F796DF5C4CA9741EABDA5F2B083F411F353A112955DAA091986EBE012B944D0A`。完全差分、snapshot、source/実行物hash、各生ログ・exit、原画像、Before、10 field入力、開始/終了UTC、実行前後Git状態を含む。ローカル証拠は本スライスの監査・Phase Dまで保持し、同じpathで取得してmanifestを照合する。artifactsはstage/commitしない。

400はA3 §2.2/§5どおり inconclusive であり、非公開性成功・provider能力否定・コード欠陥確定へ読み替えない。locked経路未到達、独立した実Afterは未観測で、設定不変も主張しない。GO候補に必要な証拠が揃わないためC'は開始せず、GO / slice完了とはしていない。原因不明400だけを理由にBを再開せず、この1回で停止した。

## 8. Phase C'

未実施

## 9. Phase D

C/C' 突合・マージ・harvest 未実施。本改訂の作成を slice 完了としない。

## 10. Phase A revision 3 — source-first 400 受け入れ

### A0 / A1

owner は、400 の原因を修正できなかった後に source を先に修正した事実を明示し、A をその事実に合わせて新 revision として A3 凍結し、400 を受け入れるよう指示した。追加のLockスクリーンショット提出は求めず、既設設定を維持したオンライン検証も許可した。主担当はこの指示を、同一の正規 unsigned GET が存在 object の内容を返さなかった実観測を限定合格に加える判断として固定した。「400を認証拒否の原因と確定」「旧 run を新 head の合格証拠とする」「A3だけでGO」とはしない。

A0 の現況: 旧凍結 revision 2 の規範本文・SHAは§0のとおり不変。旧 B head 8c1793e の C live は認証PUT/GET、unsigned 400/InvalidArgument/EOF、回復清掃まで観測し、lockには未到達。旧 C の実測と原画像は `artifacts/route-proof-phase-c-8c1793e/` に保持。source-first commit f256252 は `RouteProof.ps1`、`RouteProof.Tests.ps1`、`tools/Artifacts/README.md` だけを変更し、400/other/InvalidArgument を guard 付きで受け入れる。新 head の判定 C / C' と Bucket Lock 実効性は未実施。今回の問いと最低条件・対象外・停止規則は新規範本文§1〜§6。400原因の解明は最低条件ではない。

A1 は [revision 3 規範本文](ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION_3.md) に自己完結して記載。source-first 候補の既知の `TimedOut=true / EofConfirmed=true` 誤合格余地を固定し、発見 C から必須の局所 B 適応と回帰に戻す。公開設定・全有効Lock rule・lifecycle・writer権限の前後観測経路は、主担当が保存済みCloudflare profileの管理画面で read-only 疎通確認し、非秘密記録 `artifacts/route-proof-phase-a-r3/settings-path-check.txt` を保存した。これは新 run の前後証拠ではない。新 run では C が同じ経路で時刻付き生テキストを取得し、設定を変更しない。

### A2 独立レビューと採否

- A0のみの代替構成: GPT-6 Luna / OpenAI、subagent `/root/a0_alternative`。限定非露出と認証原因の主張を分離し、旧8cのrunをf256のGOへ転用しない提案を採用。
- A2 同一入力: revision 3 A1候補（UTF-8/LF SHA-256 `007923B153B18305157554F8553133C5EB91F6F2C1CB360574C75C7FB39BA088`）、本台帳の当時の§0/§1、AGENTS.md、workflow Skillと参照。レビュー相互の所見は非提示。architecture担当 GPT-6 Astra / OpenAI `/root/a2_arch_r3`、failure/evidence担当 GPT-5.6 Sol / OpenAI `/root/a2_evidence_r3`。architecture担当の初回読取には台帳§2以降の旧A2/旧C履歴が混入したが、新r3の他A2所見は参照していない。
- 両レビューの設定取得経路 blocker: 採用。既提出のowner画像を将来の事後証拠にせず、主担当が in-app browser の Cloudflare bucket Settings と writer token 詳細を実際に閲覧した。規範本文§2.3に C の操作、観測項目、UTC・URL・DOM保存、認証喪失時の実行前停止を追記。両担当が自身の所見の閉鎖を確認。
- failure/evidence担当の timeout guard blocker: 採用。f256252 の `Test-UnsignedPrivacy` に `TimedOut` 明示拒否がない具体的違反を規範本文§4へ固定し、§5に矛盾観測の非合格回帰を必須化。これは予定された局所 B 適応であり、400の合格集合を広げる追加設計ではない。担当が自身の所見の閉鎖を確認した。
- その他の構造・旧run非転用・C'独立性・400の限定意味・停止規則は妥当と評価。不採用はなし。保留は400の因果と新headでのlock実効性で、GO判定へ偽装しない。

### A3 凍結境界

ownerの「Aをそのように直してA3凍結して、400受け入れます」を統合承認とし、A2 blocker の閉鎖後 2026-09-28T16:34:42Z に規範本文を A3 凍結した。snapshot SHA-256 と固定 commit は§0に記録する。凍結後も旧 revision 2 の snapshot は一切編集しない。A3完了はGOではなく、既知 timeout の局所修正、同じ新実装headでの限定・全回帰、12操作 live、Bucket Lockと前後設定の実観測、判定 C / 独立 C' がGOに残る。

## 11. Revision 3 の判定 C — locked 上書きの 409

担当は Codex / GPT-6 Astra、新規セッション。B（GPT-6 Sol）とのモデル相違を満たす。固定 base は `93d2a1c436361ef6ee702096a61087cde55319b4`、head は `31be639a968402e0bdf3f65fd9eda8d0d818dbf3`、実行時 docs tip は `34135d80a0f49def79b91db0797609cbc30bb49b`。A3 freeze commit `5172d597cd2725f3cff4305d9bddce237071c685` と §0 の revision 3 snapshot hash が一致した。B snapshot hash は `D60CCF4A1342DE0E5AF9D44E29CE193B11EDEB0DFA441A5C76F7D42916A0A6D2`。docs-only tip を実装 head にしていない。

発見 C では全 repository diff / stat / name-status と source-first 差分を固定し、責務配置と失敗経路を先に照合した。RouteProof の単一 orchestration、transport の request・有限読み取り、CredentialStore の秘密寿命は分離され、Unity 側変更はない。400 の status/class/Code tuple、timeout、EOF、redirect、本文上限、全体・prefix hash、実 child 入口、Before/After 分離を確認した。凍結済み条件への具体的な実装 blocker は未発見。構造分割や新 API の追加は求めていない。

判定用 offline 検証は Release/net8.0 build（警告0・エラー0）、Credentials 22件、RouteProof `-Case '*'` 26件、transport 7件、contract-audit、docs-audit が成功。コマンド・exit・case一覧・生ログは manifest に収録。初回 sandbox の Credentials は ACL 拒否、RouteProof は child 起動 timeout で失敗し、生ログを保持した。同じ試験を承認済み sandbox 外経路で実行して上記全件成功。Unity EditMode/PlayMode/build は A3 §5 の適用除外どおり未実行。

新しい Before/After は C 自身が保存済み Cloudflare profile の read-only DOM から取得した。Settings は開始前 `2026-09-28T16:48:29.164Z`、終了後 `2026-09-28T16:54:20.555Z`。writer は開始前 `16:51:20.165Z`、終了後 `16:54:09.919Z`。r2.dev 無効、Custom Domains なし、全有効 lock rule は `probe_locked / probe/locked/ / Age 86400秒` の1件、lifecycle は7日後の multipart abort のみ、writer は対象 bucket の Item Write/Read のみで前後同一。画面URL・UTC・生DOM・観察者・清掃予定を保存し、before <= run start < run end <= after を照合した。設定・token の変更や owner への画像再提出依頼は行っていない。

許可された1 runを `2026-09-28T16:53:16.6807729Z`〜`16:53:32.0530764Z`、15.371266秒で実行。run-id は `62bd4f1ab6b24a9d93026108ee5dae5c`、新 key は2個。unlocked PUT/認証GETの57 byte・hash一致、unsigned `400 / other / InvalidArgument`（113 byte、EOF、全体と期待長prefixのhash不一致）、上書き/変更hash GET、DELETE 204/NoSuchKey 404を通過した。限定400を認証拒否原因の証明にはしていない。同じ Generation の locked PUT/認証GETも成功した。

次の locked 上書きは **`409 / other / ObjectLockedByBucketPolicy`**、EOF true、limitReached false。A3 §1(3) と `Test-LockRejection` は `403 / forbidden / ObjectLockedByBucketPolicy` を要求するため、probe は exit 4 / inconclusive で停止した。locked DELETE 拒否・最後の原hash GETは未到達。409を403へ書き換えず、再run・追加request・設定変更は行わない。これは凍結済み status 許可集合と実観測の不一致であり、現時点でコード欠陥や provider 能力否定を確定しない。許可集合を変更するなら A3 §6 に従い新 revision の A 判断が必要。C' は開始しない。

unlocked は NoSuchKey まで確認済み。残存予定 key は `probe/locked/62bd4f1ab6b24a9d93026108ee5dae5c/object.txt`、保持実効性は unconfirmed。既設1日 rule を維持し、保守的な清掃可能時刻を run終了+86400秒の `2026-09-29T16:53:32.0530764Z`（JST 2026-09-30 01:53:32）と記録した。所有者の後続作業でその時刻以後に DELETE/NoSuchKey を確認する予定であり、清掃完了・自動清掃予約済みとはしていない。

fresh pwsh の loaded assembly location/hash と実行前後 DLL・依存hashが一致。DLL SHA-256 は `ED3A6DE29DC34D3F88BEC55A8CAFDDBE2AC7051AB50F32F93F731633A0AE9909`。実行間に build/copy はない。source は build前と実行前後の `git diff --exit-code <head> -- tools/Artifacts .gitignore` が成功して同じ固定headと照合された。ただし収録用scriptのcase-insensitive globにより、前後manifestのFiles配列からtools/Artifacts sourceが抜けた。原manifestを変更せず、事後の `source-correspondence.json` に12ファイルのSHA-256・作業tree Git blob・固定head blob一致を補記した。事前source SHA一覧を取得済みとは主張しない。

証拠id/path は `artifacts/route-proof-phase-c-31be639/`、manifest生成UTC `2026-09-28T16:55:38.8649494Z`。全差分・snapshot・build/load・全試験ログ・設定前後DOM・入力・live JSONL・実行物4ファイル・対応manifestを収録。生JSONL SHA-256 は `A2B8F962EA57B388AC88520CB87003D665D8369574554F0CDF3CD69C244E0FC5`。`files.sha256` 自体のSHA-256は `74CE1413AB3DA337814082333B8978F037B9180CEFDC4EB21D38E89ABFEC7A18`。同じローカルpathから取得し各行をSHA-256照合する。監査とPhase D終了まで保持し、artifactsはstage/commitしない。C' blind bundleは未生成、転送検証は未実施。結論は **inconclusive / GOなし**。

## 12. Phase A revision 4 — Lock 409 の限定受け入れ

### A0 / A1

§11の同じhead・同じrunでは、限定400を通過しunlockedの全操作、locked PUT/認証GETまで成功したが、locked上書きで `409 / other / ObjectLockedByBucketPolicy` を観測した。Cloudflareの[公式エラー表](https://developers.cloudflare.com/r2/api/error-codes/)は同Codeを403と説明する。A3 revision 3の403条件を変更せずに409を合格とすることはできず、旧Cはinconclusiveのまま保持する。現スライスの問いは変えず、今答えていない locked DELETE 拒否・最終原hash GETを新headと新runで確かめる。

A1 は [revision 4 規範本文](ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION_4.md) に自己完結して記載。409/other/ObjectLockedByBucketPolicy を403/forbidden/同Codeに加えるが、両拒否操作の同一応答tuple、陽性対照、同じwriter Generation、保持内、最後の認証GETで原byte/hash、前後設定一致をすべて必須にする。statusやCode単独、旧partial run、probe exit 0だけではGOにしない。既存 profile・rule・SDK・transport・秘密の寿命・実payloadは変えない。新runの前後観測はCが確認済みの管理画面経路で取得し、ownerへ画像を再要求しない。

### A2 独立レビューと採否

- A0のみの代替案: GPT-6 Luna / OpenAI、subagent `/root/a0_lock409_alt`。A1非提示。旧runの409をGOにせずAを改訂し、正確なLock拒否tupleと未到達のDELETE/最終GETを要求する案を採用。旧keyで不足分だけ確認する案は不採用。旧runはsource SHA一覧の事前収録が不完全であり、判定C/C'に必要な同一最終headの12操作・完全な前後実行物対応を新runで揃える方を選んだ。
- A2 同一入力版: revision 4 A1初稿（UTF-8/LF SHA-256 `5EE0424B7D1A01C605653D2E0E5A576EB3664E3C99DEA163A1253B68313BE3D8`）、本台帳§0/§11、AGENTS.mdとworkflow参照。architecture担当 GPT-6 Astra / OpenAI `/root/a2_arch_r4`、failure/evidence担当 GPT-5.6 Sol / OpenAI `/root/a2_failure_r4`。相互の所見は非提示。
- architecture: blockerなし。409の判定policyを既存 `Test-LockRejection` に置き、transport / store / UI証拠責務を増やさない配置、限られたPS/.NET回帰、時刻付き前後観測を妥当と評価。任意明確化「今回の409対応ではtransportを変更せず、別の正規request違反が示されたときだけ§3を再照合」は採用し規範本文§4へ反映。
- failure/evidence: 同一入力版で409をstatus単独にせず、上書きとDELETEの双方、最終原hash GET、陽性対照、新head・新run、C/C'を要求する設計にblockerなし。レビュー中に主担当が任意明確化を先に一時反映し初稿hashがずれたため、元の初稿へ戻して指定hashを再照合し、担当が同一入力版として閉鎖確認した。その後に任意明確化を統合した。内容上の不採用・保留指摘はない。

### A3 凍結境界

ownerの「GOになるまでやって」「これ以上私に何かを求めない」「全部承認なしですすめて」を、既設設定と元の問いを維持したこの限定改訂・新1 runへの統合承認として扱う。2026-09-28T17:05:32Z に規範本文をA3凍結した。snapshot SHA-256と固定commitは§0に記録する。これはGO/C'合格を意味しない。新Bは409 paired tupleだけを局所実装し、別セッションのCは新headの全判定と1 run、C'は必須証拠が揃った場合だけ独立監査する。旧snapshot/旧bundleは変更せず、artifactsはstage/commitしない。
