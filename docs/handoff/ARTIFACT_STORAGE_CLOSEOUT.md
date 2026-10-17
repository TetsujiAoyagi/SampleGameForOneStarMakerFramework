# HANDOFF: Artifact Storage の完了宣言と分裂 store の合流 plan

## 0. メタデータ

- type: `slice`
- status: `A`（A1 初稿、A2 独立レビュー 1 件済み。人間の A3 を経て Phase B へ）
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
  - 次の Phase と役割: Phase A の A3（主担当と owner が §5「A2 独立レビュー結果」の R1〜R10 を採用・不採用・保留に分類して理由を残し、本文を凍結する）→ Phase B。
  - 引き継ぐ commit: branch `claude/artifact-storage-closeout`（`origin/develop` の `6ed90d69` から作成済み）。A1 初稿は最初の commit `82b0edc5`、A2 結果はその次の commit。implementation base は A3 凍結 commit を Phase B 開始時に書く。
  - 次の作業（A3）: R1 は owner が再開条件 3 件を §2 に転記しないと Phase B が AC3 を満たせない。R3・R4・R7・R8 は条件の追加・変更なので、採用するなら §2 / §3 / §4 を改訂してから凍結する。凍結後の Phase B は §4 の順序どおり（先に §4-1 の `store plan` を 1 回）。
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

### A2 独立レビュー結果（1 件目）

- 担当: 新規 session（A1 の会話・調査レポートを読んでいない）。モデル Claude Opus 5.5（Anthropic）。A1 主担当と同じベンダーの可能性があり、独立性は「session 分離のみ」。C' は §5 の予約どおり別ベンダーにする。
- 入力版: content sha256 `ab517b8dac275157605109c3a350033eab12dce873f8709b18118e00814ccb28`（commit `82b0edc5`、branch の最初の commit）。
- 観点: §5 の (a) I3 の維持、(b) 再開条件の観測可能性、(c) `store plan` 手順と README「分裂したストアの合流」・実装との整合。加えて不変条件を破る順序の探索。
- 読んだもの: 本文書、`tools/Artifacts/README.md`、program 文書 §0・§4、`StoreConsolidation.psm1` / `StoreConsolidationPlan.psm1` の plan 経路、README の anchor を参照する文書。

| # | 観点 | 指摘 | 根拠 | 提案 |
|---|---|---|---|---|
| R1 | (b) | AC3 の再開条件が「レポート §7.2 の 3 条件」を参照しているが、レポートは git 管理外で、3 条件は §1 に転記されていない。§0 は「必要な事実は §1 に転記済み」としており、Phase B は AC3 を満たせない。 | §0 判断の根拠、§2 AC3 | 3 条件を §2 に観測可能な語で転記する（A3 で owner が確定）。 |
| R2 | (c) | S1 / AC2 の保留理由の列挙が実装と合わない。実装の保留理由は `conflict`、`acl-mismatch`、`reparse`、`source-unreadable`、`reference-missing`、`reference-localcache`、`reference-out-of-role`、`source-unchecked`、`source-invalid`、`multi-workflow-unit`、`workflow-already-visible`、`dependency-held`。S1 は `conflict`（分岐した Workflow 履歴等、最も起きやすい）、`source-unreadable`、`reference-localcache`、`reference-out-of-role`、`dependency-held` を欠き、ACL・reparse を code でなく語で書いている。 | `StoreConsolidationPlan.psm1` の理由付与箇所 | §6 の表は manifest の理由 code をそのまま転記すると定め、S1 の列挙は例示にする。 |
| R3 | (a) | README の見出しを 3 節へ再編すると、他文書の anchor link が切れる。`AGENTS.md`（常時契約の「事前承認」link）、`review-evidence.md`、`tools/Workflow/README.md`、`tools/Harness/README.md`、program 文書が `tools/Artifacts/README.md#...` を参照している。`docs-audit.ps1` は anchor を検査しないので S6 では検出されない。§3 の責務マップにもこれらの参照元が無い。 | grep `Artifacts/README.md#`、`tools/*audit*` に anchor 検査なし | 不変条件に「既存の anchor 参照が全て解決する」を足し、AC で参照元の anchor を列挙して照合する。`AGENTS.md` の link は見出し名を保つ側で守る（常時契約の文書を書き換えない）。 |
| R4 | (a) | AC4 は reasonCode 名の grep だけで I3 を確かめるため、code を含まない契約文（事前承認の範囲、退役を通常操作に含めない旨、仮想化 host で Storage を操作しない旨、fail-closed の文）が消えても合格する。 | §2 AC4、README「エージェント操作の事前承認」「実行するprocessの条件」 | A3 で README の節を「契約（本文を変えずに移動）」と「圧縮してよい（経緯・重複説明）」に分類して凍結し、契約節は移動前後の本文の一致で確かめる。 |
| R5 | (a) | S3 の「冒頭 1 画面に収まる」は観測できない。README は 1 行が 1 段落（466 行・約 103 KB）なので、§3 の行数見積り（−100 行）も規模の指標にならない。 | `wc` | 「最初の `##` 節が 7 コマンドの表とその直後の短い注記だけで、N 行以内」など数えられる形にし、規模は bytes で見積もる。 |
| R6 | (a) | 3 節構成に、日常操作でも契約でもない管理操作（一回の reset、`store plan` / `consolidate`、資格情報、退役、Route proof）の置き場が無い。 | README の見出し一覧 | 「管理操作」節を足すか、どの節に入れるかを A3 で決める。 |
| R7 | — | 目的の問い「公開文書が指す入口が実ストアで成立するか」に対し、成立条件に「成立しない入口を文書がそう書く」が無い。plan の結果が保留でも、README 冒頭・`docs/README.md`・program は `harness.ps1 current -Task artifact-evidence-lifecycle` を入口と呼んだまま完了宣言できる。 | §2 問い、§1 現況 4 点目 | 成立条件に「合流前は CURRENT が実ストアに無いことと、その原因（plan の理由 code）を入口の記述の隣に書く」を足す。 |
| R8 | — | AC5 が「`payload/` は 7 日清掃の対象」と注記させるが、README の 7 日清掃は Evidence catalog の staging（`EvidenceRetentionPolicy`）の規則で、`content fetch` の operation が清掃される根拠は README にも実装の検索でも見つからなかった。誤りの事実を README に書かせうる。 | README「終了・再開・利用中と清掃」、`604800` の使用箇所 | 「Phase B が実装で清掃の有無を確かめ、確かめた事実（清掃されないなら手で消す前提）を書く」に変える。 |
| R9 | (c) | §0 の停止規則が挙げる失敗 code は 3 つだが、plan は `invalid-output`、`store-path-unavailable`、`consolidation-failed` でも失敗する。`--output` は絶対 path・未存在・親 directory 既存・両ストアの外が条件。 | `Invoke-StoreConsolidationPlan` | 停止規則を「`status` が `passed` 以外なら全て」にする。scratchpad の新しい file 名を使う旨を §4-1 に書く。 |
| R10 | — | AC6 の Phase D の 1 文は非仮想化 pwsh と `--config <v2-config>` を要する。Phase D の担当が仮想化 host の場合の扱い（非仮想化の担当へ依頼する）が無い。 | README「実行するprocessの条件」 | 1 文に「仮想化 host では非仮想化の担当へ依頼する」を含める。 |

- 不変条件を破る順序の探索:
  - I1: plan の経路は両ストアへ書かず、manifest は両ストアの外の新規 file に限られる（実装で確認）。virtualization probe は `%LOCALAPPDATA%` 直下のストア外 directory を作って消すだけ。破る順序は見つからなかった。§4-1 の手順外で `consolidate` を打つことだけが破る経路で、停止規則が防いでいる。
  - I2: manifest は path を含むので、§6 へ manifest の一部を貼ると破れる。AC2 で防いでいる。plan の stdout の `manifestSha256` は path でないが、C / C' へ渡す時は取得案内側に置くのがよい。
  - I3: R3・R4 の順序（見出し変更で AGENTS.md の link が切れる／code を含まない契約文の削除）で破れる。
  - I4: AC3 が R1 のとおり実行できないので、Phase B が条件を自作すると「後続で扱う」相当の弱い語になりうる。
- 問題なし: §4-1 のコマンド形は README・実装と一致する。`excluded` の扱い（`credentials` 等を開かない）も実装と一致する。`tools/` `unity/` 差分 0 の確認は機械で足りる。
- blocker 候補（A3 で採否を決める）: R1、R3、R7、R8。他は条件の明確化。

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
