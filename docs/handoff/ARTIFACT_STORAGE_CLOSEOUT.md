# HANDOFF: Artifact Storage の完了宣言と分裂 store の合流 plan

## 0. メタデータ

- type: `slice`
- status: `A`（A1 初稿。A2 独立レビュー 1 件と人間の A3 を経て Phase B へ）
- branch: `claude/artifact-storage-closeout`（次の担当が `origin/develop` から切る。本文書はその最初の commit に含める）
- planning base commit: `6ed90d69`（`origin/develop`、PR #132 マージ後。ローカルの `develop` は遅れているので先に `git fetch origin develop`）
- implementation base commit: 未固定（Phase B 開始時に branch の最初の commit を書く）
- implementation head commit: 未固定
- risk: `low`（文書改訂と読取のみの `store plan`。実ストア・R2・資格情報への書込みと削除は 0 件）
- owner: OSM maintainers
- created: 2026-10-17
- expires: 2026-11-17 または置換 revision
- harvest to: `tools/Artifacts/README.md`、`docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md`（完了宣言後は削除候補）、`docs/README.md`、`.agents/skills/osm-workflow/references/review-evidence.md`
- Phase A snapshot の取得案内・状態: 未生成（A3 凍結時に commit SHA で固定する。文書だけなので stage は不要）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase と役割: Phase A の A2（独立レビュー 1 件、観点は §5）→ 人間の A3 → Phase B。
  - 引き継ぐ commit: `origin/develop` の `6ed90d69`。本文書を branch の最初の commit に入れ、その SHA を implementation base に書く。
  - 次の作業: §4 の順序どおり。先に §4-1 の `store plan`（読取のみ）を 1 回行い、その結果を見てから §4-2 の文書改訂に進む。
  - 必要な権限: 非仮想化の pwsh（owner の端末か、LocalApplicationData が仮想化されない Agent 環境）。資格情報・network・Unity は不要。`store plan` は実ストアの外に manifest を書くだけで、Codex 側 copy を読取専用で開く。
  - 停止規則: `store plan` が失敗（`virtualized-store` / `store-context-unknown` / `invalid-source-root`）したら原因を記録して止め、資格情報や store を触って直さない。`store consolidate` は owner が manifest を承認するまで実行しない（dry-run も同じ）。
  - 引き渡さないもの: Codex 側 copy の削除・移動、R2 の設定変更、保留 list に入れた機能の実装、Unity の変更。
  - 判断の根拠: 2026-10-17 の独立調査レポート（owner の手元層にあり git 管理外。clone には無いので、必要な事実は §1 に転記済み）。owner は同日に「Artifact Storage 開発はここで完了」と決めた。

## 1. 目的と対象外

- 目的:
  1. Artifact Storage を「完成」と宣言し、program §4 B〜E に残る後続を**再開条件付きの保留 list** に変える。
  2. Harness の Git 外固定記録（CURRENT）が実ストアに無い状態を、読取のみの `store plan` で確定し、合流の可否と保留理由を owner に返す。
  3. README を人間が 1 画面で日常操作を判断できる形に分ける。
- 対象外: `tools/` と `unity/` のコード変更、`store consolidate` の実行、R2 の bucket 設定（lifecycle / lock / token）、保留 list の機能の実装、Codex 側 copy の削除、Cloud・別 host の取得経路。
- 現況（2026-10-17 の観測。レポート §3 / §7 / §9 から転記）:
  - 完成条件 6 件（Evidence の stage → 別 session fetch、Player / Content の保存と全 file hash 取得、30 日清掃と N=10 件数清掃、intent → DELETE → GET 404 の削除確定、秘密非露出、offline suite の合格）は PR #109 / #115 / #118 / #119 / #123 / #128 / #131 と、調査時の手元実行（6 suite 103 case、failed 0）で満たしている。
  - 実ストア `LocalApplicationData\OneStarMaker\` には `Artifacts`、`RevisionLocks`、`Workflow` があり、**`Harness` directory が無い**。
  - Codex の packaged app の `LocalCache\Local\OneStarMaker\Harness\<repoId>\tasks\` に `artifact-evidence-lifecycle`、`h1-transport-identity`、`h2c-unity-gate`、`h2c-unity-gate-r2` がある（directory 名のみ確認。内容は未読）。同じ場所に `Artifacts`、`Workflow`、`WorktreeRecords` もある。
  - 非仮想化 process で `pwsh tools/harness.ps1 current -Task artifact-evidence-lifecycle` は「CURRENT が見つかりません」で失敗する。README・docs/README・program はこのコマンドを「固定仕様・原観測の入口」と呼んでいる。
  - `store plan` / `store consolidate` は PR #126 で実装・検証済み（`StoreConsolidationPlan` 24 case、`StoreConsolidation` suite）だが、実データへの plan は未実施。
  - `origin/develop` で 2026-10-10 以降の commit は `unity/Assets` 1 件に対し `tools/Artifacts` 37 件。Artifacts の非テストコードは約 10,900 行、README は約 103 KB。
  - Unity 側は Artifact Storage を参照しない。BS4 → `build collect` / `content collect` → R2 → `content fetch` の `payload/` → Delivery window「Local/LAN directory」→ Prepare → Use For Next Play、で往復が成立しているが、この往復を 1 ページに書いた手順が無い。
  - Phase D で `schedule status` を確認する手順が無く、Store 版 pwsh の更新で日次 job が止まったことを人が気づく仕組みが無い（PR #129 で実測）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: Artifact Storage を完了と宣言しても、公開文書が指す入口と日常操作が実ストアで成立し、後続を再開する条件が文書で読めるか。Harness の固定記録は実ストアへ合流できるか。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - I1: 実ストア（`LocalApplicationData\OneStarMaker\`）と R2 に対する書込み・削除が 0 件である。`store plan` の manifest は実ストアの外（scratchpad 等）に置く。
    - I2: 公開文書・PR 本文・manifest の要約に、ローカル path の実値・SID・package 名・reference・packageSha256 の実値を書かない。
    - I3: 既存の安全契約（fail-closed、利用中保護、保護名規則、事前承認の範囲）を README から削らない。縮めるのは説明の重複と経緯であって契約ではない。
    - I4: 保留 list に入れた機能を「次にやる仕事」と読める書き方にしない。各項目に再開条件を付ける。
  - 成立条件（起きなければならないこと）:
    - S1: `store plan` の manifest が 1 つ生成され、Harness 4 task を含む各単位の公開対象／保留と理由（`reference-missing`、`multi-workflow-unit`、`workflow-already-visible`、ACL、reparse、`source-unchecked`、`source-invalid`）、`excluded` が表で HANDOFF §6 に要約されている。
    - S2: program 文書の status が「完了（保留 list 付き）」になり、§4 B〜E の未着手項目が再開条件付き保留 list に移っている。
    - S3: README が「日常操作」「契約と known limits」「経緯（PR 参照）」の 3 節に分かれ、冒頭 1 画面に日常操作（`evidence stage` / `fetch` / `build collect` / `content collect` / `build cleanup` / `content cleanup` / `schedule status`）が収まる。
    - S4: 別 PC で Player と Content を再現する手順（`content fetch` の `payload/` を Delivery の Local/LAN directory に渡す往復）が README に 1 節ある。
    - S5: Phase D の手順に `schedule status` の確認が 1 行入る（`review-evidence.md` の「Phase D の保存・引渡し確認」）。
    - S6: `pwsh tools/docs-audit.ps1` と `pwsh tools/contract-audit.ps1` が通る。
- 受け入れ条件:
  - AC1（S1, I1）: `store plan` を非仮想化 pwsh で 1 回実行し、exit code と `status`、単位数、保留数を記録する。失敗なら reasonCode を記録して止める。不合格例: manifest を実ストア配下に書く。consolidate を実行する。
  - AC2（S1, I2）: §6 の表は単位 ID と task ID、理由 code だけを持ち、path を持たない。
  - AC3（S2, I4）: program の保留 list の各項目に「再開条件」（レポート §7.2 の 3 条件のどれか）が付いている。合格例: 「Content 確定記録 — 再開条件: 別 PC からの同時公開が実運用で 2 回以上衝突した時」。不合格例: 「後続で扱う」。
  - AC4（S3, I3）: README の差分で、`known limits`、事前承認の範囲、各コマンドの拒否 code の説明が消えていない（grep で旧節の code 名が新 README に残ることを確認）。
  - AC5（S4）: 手順が `content fetch` の出力先、Delivery window の入力欄、Prepare、Use For Next Play の順で書かれ、`payload/` が 7 日清掃の対象であることを注記している。
  - AC6（S5）: `review-evidence.md` の Phase D 節に `schedule status` の確認が 1 文で入り、`scheduler-identity-mismatch` の時の切替手順（README「自動清掃」）を参照している。
  - AC7（S6）: 両 audit の出力を §6 に貼る。
- ここでは答えない問いと所有する後続:
  - `store consolidate` の実行と、保留単位の解消（owner が manifest を承認した後の別 task。I1 のため本スライスでは実行しない）。
  - Codex 側 copy の保持・削除・期限（owner の判断。program の保留 list に「Codex 側残置の後始末」として残す）。
  - R2 lifecycle / 条件付き書込み / Read only token の採用（保留 list。再開条件は program に書く）。
- 判定定義: GO = S1〜S6 と I1〜I4 を満たす。NO-GO = `store plan` が実行できず原因が環境側で、文書改訂だけでは問いに答えられない場合。この場合は §4-2 だけを進めて「plan 未実施」と明記して GO にしない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約: `store plan` は `tools/Artifacts/README.md`「分裂したストアの合流」のとおり、`credentials`、`builds-v1`、`contents-v1`、`runtime`、`RevisionLocks` を開かず `excluded` に載せる。Claude の Bash は LocalApplicationData が仮想化されるので、`store plan` は PowerShell tool か人間の端末で実行する。
- 未決事項: Codex 側 copy の最終的な扱い（owner）。Cloud・別 PC の読み手を置く予定の有無（owner。あれば R2 Object Read only token の手順を保留 list の先頭に置く）。

## 3. 責務マップ

| ファイル | 責務 | 変更理由 | 現在行数 | 予想増分 |
|---|---|---|---:|---:|
| `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` | program の状態と保留 list | 完了宣言、§4 B〜E を保留 list へ、§7 の完成・停止・再開条件を追記 | 254 | −120 程度（経緯を PR 参照に置換） |
| `tools/Artifacts/README.md` | 日常操作・契約・known limits | 3 節へ再編、往復手順 1 節追加。契約は削らない | 467 | −100 程度（重複説明の削除）、+30（往復手順） |
| `docs/README.md` | program 索引 | program 表の行を「完了（保留 list 付き）」へ | — | ±5 |
| `.agents/skills/osm-workflow/references/review-evidence.md` | Phase D 手順 | `schedule status` の確認を 1 文追加 | — | +2 |
| `<scratchpad>/store-plan.json`（Git 外） | `store plan` の manifest | AC1 の生成物。Git に入れない | — | 新規 |
| `docs/handoff/ARTIFACT_STORAGE_CLOSEOUT.md` | 本 HANDOFF | Phase ごとに追記、Phase D で harvest して削除 | — | — |

コードの変更は無い。500 行警報の対象は README（既に 467 行、縮める方向）だけで、分割は節構成で行う。

## 4. 実装計画

- 変更対象: §3 の 5 ファイル。
- 順序:
  1. **`store plan`（読取のみ）。** 非仮想化 pwsh で `pwsh tools/artifacts.ps1 store plan --source-root <Codex 側 OneStarMaker の絶対 path> --output <scratchpad>/store-plan.json` を 1 回実行する。manifest を読み、単位ごとの対象／保留／理由／`excluded` を §6 の表にまとめる（path は書かない）。Harness 4 task がどの単位に入り、保留ならその原因を特定する。`consolidate` と dry-run は実行しない。
  2. **文書改訂。** program → README → docs/README → review-evidence の順。README の再編は節の移動と重複削除に限り、契約の文を書き換えない（I3）。往復手順（S4）は `20-variant-checkout-workflow.md` §3 と README「開発 Content 1 件の保存と取得」を参照して書く。
  3. `pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1`。
- Phase B から Phase A へ差し戻す条件: `store plan` の結果が「全単位保留」で、保留理由の解消に Storage の設計判断（公開途中の不可視性を保つ方式等）が要る場合。この時は plan の結果だけを §6 に残し、合流は別スライスにする。
- 対象外を維持する方法: `tools/` と `unity/` の差分が 0 であることを B の出口で `git diff --stat` に残す。R2 へは何も送らない（network を使うコマンドを実行しない）。

## 5. テストとレビュー計画

- 単体テスト: なし（コード変更なし）。
- 差し戻し中の起点 `-Filter`: 対象なし。
- 判定必須テスト: `pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1`。
- 全 EditMode 回帰の適用除外: Unity を変更しないため適用外（program の文書改訂 PR #108 / #117 / #122 と同じ扱い）。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路:
  - AC1: 担当は Phase B。環境は非仮想化 pwsh。操作は §4-1 のコマンド 1 回。観測は exit code、stdout の `status` / `reasonCode`、manifest の単位数・保留数。合否は S1 のとおり。証拠は manifest（Git 外、scratchpad）と §6 の要約表。C / C' には要約表と、必要なら manifest を `evidence stage` で渡す（manifest に path が含まれるため公開文書へは転記しない）。
  - AC4: 担当は発見 C。旧 README の reasonCode 名（`` `[a-z]+(-[a-z0-9]+)+` `` の形）を抽出し、新 README に全て残ることを grep で確認する。
- 未知の操作経路の疎通結果: `store plan` を実データに対して実行した例は無い。初回確認は Phase B の §4-1 が兼ねる。不成立時は reasonCode を記録して停止（§0 停止規則）。
- 人間の判断が必要な条件: Codex 側 copy の扱いと、合流の承認（本スライスでは判断を求めるだけで実行しない）。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: なし。
- 機械検査: 両 audit。`git diff --stat` で `tools/` `unity/` の差分 0。
- A0/A1 主担当: 本 HANDOFF の作成 session（調査レポートと同じ）。
- A2 独立レビュー: 1 件。観点は (a) I3 の維持（README から契約が消えないか）、(b) 保留 list の再開条件が観測可能な語か、(c) `store plan` の手順が README の「分裂したストアの合流」と矛盾しないか。入力版は本文書の content sha256。
- A3 統合担当: 人間（owner）。
- C' 用に予約した担当: A2 と別ベンダーの 1 件。文書と manifest 要約だけを読む。
- 独立性の強化条件を満たせない場合の理由: 文書改訂のため、A2 1 件と C' 1 件で足りる。

## 6. Phase B 実装結果

未実施

### 動作確認

未実施

## 7. Phase C

未実施

## 8. Phase C'

未実施

## 9. Phase D

未実施。harvest 先は §0。本 HANDOFF の削除時に、program 文書も完了宣言後の保留 list だけを README か docs/README へ移して削除してよいかを owner が決める。
