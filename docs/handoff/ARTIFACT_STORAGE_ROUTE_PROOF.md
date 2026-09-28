# Artifact Storage Route Proof — 進行 HANDOFF

## 0. 現在の入力

- type: slice / status: A3 凍結済み（revision 2）、Phase B 完了・Phase C 未開始
- branch: codex/artifact-storage-route-proof / risk: high
- owner: repository owner / created: 2026-09-29 JST / expires: Phase D または次の A revision
- harvest to: tools/Artifacts/README.md、BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md の現況
- implementation base: 93d2a1c436361ef6ee702096a61087cde55319b4
- 既存 implementation head / 新 B の開始点: da4e405a0c5019a2f0edd857e1a2c11b61432c34
- A 開始時 docs tip: dfbed2fdd48027ccbdcaecbbd8124da30019a362
- 新 implementation head: bd7b7e5ea53073e8af962beb9a15223a8890875c。B result: [ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_B_RESULT.md](ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_B_RESULT.md)、生成UTC 2026-09-28T15:45:19Z、SHA-256 C84F7DCBDB5EF3623FEB21DFD2C3BA372BCEA6C21E9417B6D16B8AF7D8700913。判定 evidence / C' bundle: 未生成。B resultを収録するdocs-only commitはimplementation headにしない。
- Phase A 規範本文: [ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION.md](ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_A_REVISION.md)。本台帳は議論・errata・進行を持ち、C' に渡さない。
- A3 snapshot 生成 UTC: 2026-09-28T15:26:42Z。
- A3 snapshot SHA-256（UTF-8 / LF）: 91BEA838FCE7C3C2092E7CA1B9DB58D896C1737CE502513E723B248F5C573217。CRLF checkout では LF に正規化して照合する。Git blob: 16a78f2f46e902db0cab7f6a4a288f085faceeeb。
- 固定 commit: 本文書と snapshot を収録する docs-freeze-artifact-route-proof-a3-r2。自己参照する commit SHA は本文へ埋め込まず、引継ぎプロンプトに確定 SHA を示す。凍結 snapshot の内容は以後変更しない。
- レビュー統合済み候補: 2026-09-28T15:22:57Z、規範本文 SHA-256 662A26AED6B6010D7C9C115F21492A9134A82BE745CE427086DA0C598F03E380。これは承認前候補の識別子で、A3凍結記録ではない。

本文書と規範本文を現行入力とする。新たな commit 別 RESULT / FINAL / RERUN は増やさない。旧固定 snapshot・結果は過去の対象版の証拠として保存する。

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

担当: Codex / GPT-6 Sol。revision 2 のB1〜B4を実施し、implementation headを bd7b7e5ea53073e8af962beb9a15223a8890875c に固定した。変更・理由・限定オフライン検証・未実行・source/DLL hashは§0の汎用B resultに記録。旧runのDLL対応は未確定で、正規GET条件への具体的違反は見つからず、400/InvalidArgumentの原因は未特定。実R2通信は行っていない。Bの引渡し条件を満たして終了し、slice GOやPhase C/C'の判定には読み替えない。

## 7. Phase C

未実施

## 8. Phase C'

未実施

## 9. Phase D

C/C' 突合・マージ・harvest 未実施。本改訂の作成を slice 完了としない。
