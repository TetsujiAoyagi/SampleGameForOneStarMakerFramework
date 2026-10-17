# HANDOFF: Artifact Storage の完了宣言と分裂 store の合流 plan

## 0. メタデータ

- type: `slice`
- status: `A`（A2 レビュー 3 件と A3 前の差分レビューを受けた A3 統合案を本文に反映済み。owner の A3 承認待ちで、Phase B は未開始）
- branch: `claude/artifact-storage-closeout`（`origin/develop` の `6ed90d69` から作成済み。ローカルのみで未 push。本文書の A1 初稿が最初の commit `82b0edc5`）
- planning base commit: `6ed90d69`（`origin/develop`、PR #132 マージ後。ローカルの `develop` は遅れているので先に `git fetch origin develop`）
- implementation base commit: 未固定（Phase B 開始時に A3 凍結 commit を書く）
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
  - 次の Phase と役割: Phase A の A3（owner が §5「A3 統合案」の「owner が決めること」3 点を確定し、本文を凍結する）→ Phase B。
  - 引き継ぐ commit: branch `claude/artifact-storage-closeout`（ローカルのみ）。A1 初稿は `82b0edc5`、A3 統合案はこの branch の最新 commit。A3 凍結 commit の SHA を Phase A snapshot に書き、implementation base は Phase B 開始時に書く。
  - 次の作業（Phase B、凍結後）: §4 の順序どおり。先に §4-1 の `store plan`（読取のみ）を 1 回行い、その結果を §6 に記録してから §4-2 の文書改訂に進む。
  - 必要な権限: 非仮想化の pwsh（owner の端末か、LocalApplicationData が仮想化されない Agent 環境）。資格情報・network・Unity は不要。`store plan` は実ストアの外に manifest を書くだけで、Codex 側 copy を読取専用で開く。
  - 停止規則: `store plan` の `status` が `passed` 以外（`virtualized-store` / `store-context-unknown` / `invalid-source-root` / `invalid-output` / `store-path-unavailable` / `consolidation-failed` 等）なら reasonCode を記録して止め、再実行で埋めず、資格情報や store を触って直さない。`store consolidate` は owner が manifest を承認するまで実行しない（dry-run も同じ）。
  - 引き渡さないもの: Codex 側 copy の削除・移動、R2 の設定変更、保留 list に入れた機能の実装、Unity の変更。生 manifest は Evidence・公開物に入れない（I1・I2）。
  - 判断の根拠: 2026-10-17 の独立調査レポート（owner の手元層にあり git 管理外。clone には無いので、必要な事実は §1、再開条件は §2 に転記済み）。owner は同日に「Artifact Storage 開発はここで完了」と決めた。

## 1. 目的と対象外

- 目的:
  1. Artifact Storage の完了の境界を公開文書に書く。完了と宣言するのは、同一 Windows ユーザー・非仮想化 process で検証済みの基本操作と運用契約（下記の完成条件 6 件）だけとする。Harness CURRENT の実ストアへの合流、`store consolidate`、仮想化 host からの操作、別 PC / Cloud からの直接 R2 取得、保留 list の機能は完了に含めず、文書でも未成立と読めるようにする。
  2. program §4 B〜E に残る後続を、再開条件付きの保留 list に変える。
  3. 分裂 store の各単位が読取のみの `store plan` でどう分類されるか（公開対象／保留と理由／除外）を 1 回の実行で確定し、owner の合流判断の入力にする。合流は実行しない。
  4. README に、日常操作を先に判断できる入口を作る。契約の本文と既存の見出しは失わない。
- 対象外: `tools/` 配下のコード・script・設定の変更（`tools/Artifacts/README.md` など §3 に挙げた文書だけを変更する）、`unity/` の変更、`store consolidate`（dry-run を含む）、R2 の bucket 設定（lifecycle / lock / token）、保留 list の機能の実装、`content fetch` の期限清掃の実装、Codex 側 copy の削除・移動、Cloud・別 host の取得経路、2 台の PC での実機往復の検証。
- 現況（2026-10-17 の観測。調査レポート §3 / §7 / §9 からの転記と、A2 の照合）:
  - 完成条件 6 件（Evidence の stage → 別 session fetch、Player / Content の保存と全 file hash 取得、30 日清掃と N=10 件数清掃、intent → DELETE → GET 404 の削除確定、秘密非露出、offline suite の合格）は PR #109 / #115 / #118 / #119 / #123 / #128 / #131 と、調査時の手元実行（6 suite 103 case、failed 0）で満たしている。
  - 実ストア `LocalApplicationData\OneStarMaker\` には `Artifacts`、`RevisionLocks`、`Workflow` があり、**`Harness` directory が無い**。
  - Codex の packaged app の `LocalCache\Local\OneStarMaker\Harness\<repoId>\tasks\` に `artifact-evidence-lifecycle`、`h1-transport-identity`、`h2c-unity-gate`、`h2c-unity-gate-r2` がある（directory 名のみ確認。内容は未読）。同じ場所に `Artifacts`、`Workflow`、`WorktreeRecords` もある。
  - 非仮想化 process で `pwsh tools/harness.ps1 current -Task artifact-evidence-lifecycle` は「CURRENT が見つかりません」で失敗する。README（冒頭と「Evidenceの責務とoffline検証」）・`docs/README.md`・program §0 はこのコマンドを「固定仕様・原観測の入口」と呼んでいる。
  - `store plan` / `store consolidate` は PR #126 で実装・検証済み（`StoreConsolidationPlan` 24 case、`StoreConsolidation` suite）だが、実データへの plan は未実施。plan の `status: passed` は manifest を書けたことを示すだけで、単位が公開できることや CURRENT が読めることを示さない。単位の状態は `publish` / `skip` / `hold`。
  - `origin/develop` で 2026-10-10 以降の commit は `unity/Assets` 1 件に対し `tools/Artifacts` 37 件。Artifacts の非テストコードは約 10,900 行、README は 466 行・約 103 KB（1 行が 1 段落なので行数は規模の指標にならない）。
  - 他の文書から README の 9 つの見出しへ anchor link がある: `AGENTS.md` ほかが `#エージェント操作の事前承認`、`tools/Workflow/README.md` と `tools/Harness/README.md` が `#実行するprocessの条件appdata仮想化`、`review-evidence.md` が `#レビュー用の変更集合changespatch`、program が Build / Content 系の 6 見出し。`docs-audit.ps1` は anchor を検査しない。
  - Unity 側は Artifact Storage を参照しない。資格情報のある host で `build fetch` / `content fetch` → 利用者が運ぶ → Delivery window（`Tools/OSM/Content/Delivery`）の Local/LAN directory → manifest digest・contentSet・revision・cache root・disk budget を入力して Prepare → Use For Next Play、の往復を 1 ページに書いた手順が無い（Delivery 側の手順は `20-variant-checkout-workflow.md` §3）。
  - `content fetch` は新規 private operation の `payload/`（`transport.json` と `content/`）に取得する。この operation の期限清掃は README「開発Content 1件の保存と取得」で後続の作業とされ、Content の件数清掃もこの残置を読まず消さない（`ContentCleanup.psm1` のコメント）。Evidence の 7 日清掃（604800 秒）は Evidence catalog の staging の規則で、ここには及ばない。
  - program は §0 で「programは進行中」「後続B〜DのA3は別途」、§5 で「A〜Dのみを実装キューとする」と書いている。§4 B の 1 行にローカル作業 directory の実値がある。
  - Phase D で `schedule status` を確認する手順が無く、Store 版 pwsh の更新で日次 job が止まったことを人が気づく仕組みが無い（PR #129 で実測）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 利用できる入口と成立しない入口を読者が取り違えない形で、Artifact Storage の完了の境界を公開文書に書けるか。分裂 store の各単位は、読取のみの `store plan` でどう分類されるか。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - I1: `store plan` と分類の確認は、実ストア・Codex 側 copy・R2 のデータと metadata を変更・削除しない。manifest は両ストアの外の新規の絶対 path に書く。生 manifest を `evidence stage` / `publish` に入れない。レビュー Evidence の通常の保存・引渡し（`review-evidence.md`）は本条件の対象外の別の境界で、本スライスもそれに従う（免除しない）。Evidence に入れてよいのは §6 の要約、文書差分、audit 出力、AC4 の比較結果だけ。
    - I2: 公開物（tracked 文書、commit diff、PR 本文、C / C' の bundle、§6）に、ローカル path の実値・user 名・SID・package 名・reference・packageSha256・manifest sha256 の実値を書かない。manifest の `sourceRoot` / `logicalRoot` は絶対 path なので写さない。program §4 B にある既存の実値も、program を改訂する時に除く。
    - I3: README の契約を失わない。(1) 既存の節（冒頭の導入 2 段落を除く）は本文を変えずに移す。変えてよいのは見出しの階層と節の順序だけ（下位節が全て移って空になり、参照元の anchor も無い見出しは消してよい）。(2) 冒頭 2 段落から削った文は §6 に全て列挙し、各文が「経緯（PR 参照へ置換）」か「新 README の別の箇所に同じ内容がある（その箇所を示す）」のどちらかである。(3) 他の文書からの既存 anchor link が全て新 README で解決する。`#エージェント操作の事前承認` を含む既存の見出し名は変えない。
    - I4: 保留 list の項目を次の仕事と読める書き方にしない。各項目は下記の再開条件のどれかと、再開を判断する者（owner）を持つ。
  - 再開条件（調査レポート §7.2・§7.5 から転記。A3 で owner が採用を確認する）:
    - RC1: エンジン側スライス（S-5 / S-8 / S-9、InGame 縦糸）が Storage の不足で実際に止まった。
    - RC2: 同じ障害が実運用で 2 回以上起き、既存の reasonCode と README の手順で回復できなかった。
    - RC3: Cloud / 別 PC の読み手が実際に稼働を始めた（HANDOFF に担当として割り当てられた）。
    - 再開は、どれかを A0 に観測事実（日付・task・reasonCode）付きで書けた時に限る。仮定上の障害では再開しない。
  - 成立条件（起きなければならないこと）:
    - S1: `store plan` を 1 回実行し、§6 に 3 層で記録する。(1) exit code・`status`・`reasonCode`。(2) manifest の単位数（`publish` / `skip` / `hold`）と `excluded` 数、Harness 4 task の単位 ID・状態・理由 code（単位に入らなければ `excluded` の理由 code）。(3) 結論: 合流できる単位、保留のまま設計判断が要る単位、plan 自体の失敗。理由 code は manifest のまま転記する。既知の例は、保留が `conflict`、`acl-mismatch`、`reparse`、`source-unreadable`、`reference-missing`、`reference-localcache`、`reference-out-of-role`、`source-unchecked`、`source-invalid`、`multi-workflow-unit`、`workflow-already-visible`、`dependency-held`、除外が `unrecognized`、`unreferenced-transfer`、`credentials`、`out-of-scope-role`、`revision-locks`、`not-owned`、`consolidation-run`（網羅ではない）。`passed` を「CURRENT が読める」「合流できる」と記録しない。
    - S2: program の status を「完了（保留 list 付き）」にし、§0 の status・§4 B〜E・§5 の残件の文を同じ意味に揃える（完了宣言と実装キューを同じ文書に併存させない）。§4 B〜E の未着手項目を再開条件付きの保留 list へ移す。`docs/README.md` の program 表も同じ状態にする。
    - S3: README を「日常操作」「管理操作」「契約と known limits」「経緯」の 4 節に再編する（節の割当は §4-2 の表）。冒頭の導入の直後に日常操作節を置き、その最初の下位見出しより前は 7 コマンド（`evidence stage` / `evidence fetch` / `build collect` / `content collect` / `build cleanup` / `content cleanup` / `evidence schedule status`）の表と短い注記だけにする。冒頭の導入とこの表の部分を合わせて 30 行以内・8 KB 以内にする（値は A3 で owner が確定）。
    - S4: 手で運ぶ往復の手順を README の日常操作節に 1 節置く（AC5 の順序と記載事項）。別 PC から直接 `content fetch` できるとは書かない。
    - S5: Phase D の手順に `schedule status` の確認が 1 文入る（`review-evidence.md` の「Phase D の保存・引渡し確認」）。
    - S6: 機械で確かめられる条件が通る: `pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1`、anchor の解決、契約節の本文一致、変更 file 一覧、公開物の露出検査（AC4・AC7・AC8）。
    - S7: `harness.ps1 current -Task ...` を入口として案内する公開文書の全箇所（README・`docs/README.md`・program）の隣に、合流前は実ストアに無く非仮想化 process から読めないことと、S1 の結論（理由 code）を書く。
- 受け入れ条件:
  - AC1（S1, I1）: `store plan` を非仮想化 pwsh で 1 回実行する。`status` が `passed` 以外なら reasonCode を記録して止め、再実行・store の変更・`consolidate`（dry-run を含む）で結果を埋めない。不合格例: manifest を実ストアか Codex 側 copy の配下に書く。既存 file へ出力する。manifest を stage する。
  - AC2（S1, I2）: §6 の表は単位 ID・task ID・状態・理由 code・数だけを持ち、path と hash の実値を持たない。
  - AC3（S2, I4）: program の保留 list の各項目に RC1〜RC3 のどれかと判断者が付いている。program に「進行中」「実装キュー」の現在形の記述が残らない。合格例: 「Content 確定記録 — 再開: RC2（別 PC からの同時公開の衝突が実運用で 2 回以上、README の手順で回復できない）。判断: owner」。不合格例: 「後続で扱う」。
  - AC4（S3, I3）: 発見 C が次を照合する。(1) 旧 README の各節（冒頭 2 段落を除く）の本文と、新 README の同名の節の本文の差分が空。(2) §6 の削除文一覧の各文の分類と、示した箇所に同じ内容があること。(3) 参照元（`git grep -o 'Artifacts/README.md#[^) ]*'` で抽出）の全 anchor が新 README の見出しから作る GitHub の slug と一致する。(4) 旧 README の reasonCode 名（`` `[a-z]+(-[a-z0-9]+)+` `` の形）が全て新 README に残る。(5) S3 の行数・bytes。
  - AC5（S4）: 手順が次の順で書かれている。(1) 資格情報のある非仮想化 host で `build fetch`（Player）と `content fetch`（Content）を実行し、`payload/` の場所を確認する。(2) 利用者が別 PC へ運ぶ（手作業）。(3) Delivery window で Local/LAN directory に Content の `payload/` を指定し、manifest digest・contentSet・revision・cache root・disk budget を入力して Prepare する。(4) requested と installed の revision の一致を確認する。(5) Use For Next Play。Player の置き方・起動条件は既存文書（README「開発Build 1件の保存と取得」、`20-variant-checkout-workflow.md`）にある事実だけを書き、無ければ「未記載」と書く。`content fetch` の operation は自動清掃されずに残ると書く（一括削除の手順は書かない）。2 台での実機往復は未検証と書く。不合格例: 「別 PC で `content fetch` する」「`payload/` は 7 日で清掃される」「2 台で確認済み」。
  - AC6（S5）: `review-evidence.md` の Phase D 節に、非仮想化 pwsh で `evidence schedule status --profile osm --config <v2-config>` を確認する 1 文が入る。文は、仮想化 host では非仮想化の担当へ依頼すること、`passed` 以外（未登録、`scheduler-identity-mismatch` 等）をこの確認では直さず owner が README「自動清掃」の切替手順で処置することを含む。
  - AC7（S6）: 両 audit の出力を §6 に貼る。`git diff --name-only <implementation base>..<implementation head>` の一覧が §3 の変更対象 5 file に限られ、`unity/` と `tools/` 配下のコード・script・設定の変更が 0 である。
  - AC8（S7, I2）: `git grep -n 'harness.ps1 current'` の README・`docs/README.md`・program の全箇所に未成立の記述が隣接している。変更した公開物（差分の全文と PR 本文）を I2 の語（ローカル path の実値、user 名、SID、package 名、reference、packageSha256）で検査し、0 件である。program §4 B の既存の実値が除かれている。
- ここでは答えない問いと所有する後続:
  - `store consolidate` の実行と、保留単位の解消（owner が manifest を承認した後の別 task。I1 のため本スライスでは実行しない）。
  - Codex 側 copy の保持・削除・期限（owner の判断。program の保留 list に「Codex 側残置の後始末」として残す）。
  - R2 lifecycle / 条件付き書込み / Read only token の採用（保留 list。再開条件は RC3 等）。
  - `content fetch` の operation の期限清掃（保留 list。README の後続の作業の記述を保つ）。
- 判定定義: GO = S1〜S7 と I1〜I4 を満たす。GO は本スライスの条件を満たしたことだけを意味し、`consolidate` の完了・CURRENT の合流・Cloud 取得を含まない。NO-GO = `store plan` が `passed` にならず、原因が環境側で文書改訂だけでは問いに答えられない場合。この場合は §4-2 だけを進めて「plan 未実施」と明記し、GO にしない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約: `store plan` は `tools/Artifacts/README.md`「分裂したストアの合流」のとおり、`credentials`、`builds-v1`、`contents-v1`、`runtime`、`RevisionLocks` を開かず `excluded` に載せる。`--source-root` は絶対 path・UNC でない・実在・両ストアが互いを含まない・reparse でないこと、`--output` は絶対 path・未存在・親 directory が既存・両ストアの外であることが条件（違えば `invalid-source-root` / `invalid-output`）。Claude の Bash は LocalApplicationData が仮想化されるので、`store plan` は PowerShell tool か人間の端末で実行する。
- 未決事項: Codex 側 copy の最終的な扱い（owner）。Cloud・別 PC の読み手を置く予定の有無（owner。あれば RC3 に当たり、R2 Object Read only token の手順を保留 list の先頭に置く）。

## 3. 責務マップ

| ファイル | 責務 | 変更理由 | 現在規模 | 予想増分 |
|---|---|---|---:|---:|
| `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` | program の状態と保留 list | 完了宣言、§0・§5 の整合、§4 B〜E を保留 list へ、再開条件の追記、§4 B のローカル path の実値の削除 | 254 行・約 58 KB | −20 KB 程度（経緯を PR 参照に置換） |
| `tools/Artifacts/README.md` | 日常操作・管理操作・契約・known limits | 4 節へ再編（既存節は本文不変で移動）、日常操作の表と往復手順の追加、CURRENT 入口の注記 | 466 行・約 103 KB | +4 KB 程度（表・往復手順・注記）。既存節は縮めない |
| `docs/README.md` | program 索引 | program 表の行を「完了（保留 list 付き）」へ、CURRENT 入口の注記 | — | ±5 行 |
| `.agents/skills/osm-workflow/references/review-evidence.md` | Phase D 手順 | `schedule status` の確認を 1 文追加 | — | +2 行 |
| `docs/handoff/ARTIFACT_STORAGE_CLOSEOUT.md` | 本 HANDOFF | Phase ごとに追記、Phase D で harvest して削除 | — | — |
| 手元層の `store-plan-<日時>.json`（git 管理外。path は tracked 文書に書かない） | `store plan` の manifest | AC1 の生成物。Git・Evidence に入れない。同じ checkout の C が §6 の要約と照合するために読む | — | 新規 |
| `AGENTS.md`、`tools/Workflow/README.md`、`tools/Harness/README.md` | README への anchor の参照元 | 変更しない。AC4 (3) の照合対象 | — | 0 |

コードの変更は無い。README は既存節を縮めないので規模はほぼ変わらない。読みやすさは冒頭の日常操作の表と節の割当で得る（I3 を優先し、圧縮は冒頭 2 段落に限る）。

## 4. 実装計画

- 変更対象: §3 の変更対象 5 file（本 HANDOFF を含む）。
- 順序:
  1. **`store plan`（読取のみ）。** 非仮想化 pwsh（PowerShell tool か owner の端末）で `pwsh tools/artifacts.ps1 store plan --source-root <Codex 側 OneStarMaker の絶対 path> --output <手元層の新しい file の絶対 path>` を 1 回実行する。stdout の `status` が `passed` 以外なら reasonCode を §6 に記録して止める（§0 停止規則）。manifest を読み、S1 の 3 層で §6 にまとめる（path と hash の実値は書かない）。`consolidate` と dry-run は実行しない。
  2. **文書改訂。** program → README → docs/README → review-evidence の順。README の節の割当は次の表のとおり（見出し名は変えず、階層だけを変えてよい）。

     | 現在の節 | 新しい節 | 扱い |
     |---|---|---|
     | 冒頭の導入 2 段落 | 導入／経緯 | 第 1 段落は短い導入に縮めてよい。第 2 段落（スライス A の経緯）は経緯節へ移し PR 参照に置き換えてよい。CURRENT の入口の文には S7 の注記を付ける。削った文は §6 に列挙（I3 (2)） |
     | （新規）日常操作の表 | 日常操作 | 7 コマンドと、各コマンドの詳細節への link |
     | Evidence v2 の通常操作、ローカル固定（evidence stage）、R2への昇格（evidence promote）、レビュー用の変更集合（changes.patch） | 日常操作 | 本文不変で移動 |
     | 開発Build 1件の保存と取得、BS4 Player出力の退避と保存（build collect）、開発Content 1件の保存と取得、開発Buildの件数清掃、開発Contentの件数清掃 | 日常操作 | 本文不変で移動（現在は「保守とoffline検証」の下） |
     | （新規）別 PC で Player と Content を使う往復 | 日常操作 | AC5 |
     | 自動清掃、一回の旧開発環境reset、分裂したストアの合流、資格情報の操作、Synthetic Route proof、系列の退役 | 管理操作 | 本文不変で移動 |
     | エージェント操作の事前承認、実行するprocessの条件（AppData仮想化）、終了・再開・利用中と清掃、開発Buildの件数保持policy、Evidenceの責務とoffline検証 | 契約と known limits | 本文不変で移動。「Evidenceの責務とoffline検証」の CURRENT の入口の文には S7 の注記を足す（本文の文は消さない） |
     | 保守とoffline検証（見出しのみ） | — | 下位節が移った後の空の見出しは消してよい。参照元に anchor が無いことを AC4 (3) で確かめる |

  3. `pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1`、AC7 の変更 file 一覧、AC8 の grep。
- Phase B から Phase A へ差し戻す条件: `store plan` の結果が「全単位保留」で、保留理由の解消に Storage の設計判断（公開途中の不可視性を保つ方式等）が要る場合。この時は plan の結果だけを §6 に残し、合流は別スライスにする（文書改訂は続けてよい）。I3 の「本文不変で移動」を守ると S3 の行数・bytes を満たせない場合も、条件の衝突として A に返す。
- 対象外を維持する方法: B の出口で `git diff --name-only <implementation base>..HEAD` を §6 に残す（AC7）。R2 へは何も送らない（network を使うコマンドを実行しない）。

## 5. テストとレビュー計画

- 単体テスト: なし（コード変更なし）。
- 差し戻し中の起点 `-Filter`: 対象なし。
- 判定必須テスト: `pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1`。加えて AC4・AC7・AC8 の機械照合（判定 C の担当が実行する）。
- 全 EditMode 回帰の適用除外: Unity を変更しないため適用外（program の文書改訂 PR #108 / #117 / #122 と同じ扱い）。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路:
  - AC1: 担当は Phase B。環境は非仮想化 pwsh。操作は §4-1 のコマンド 1 回。観測は exit code、stdout の `status` / `reasonCode` / `summary`、manifest の単位の状態と理由。合否は S1 のとおり。証拠は manifest（git 管理外の手元層、同じ checkout）と §6 の要約。発見 C・判定 C は同じ PC・同じ checkout で manifest を読取専用で開き、§6 の要約と照合する。manifest は Evidence に入れない（I1）。
  - AC4: 担当は発見 C（判定 C で再実行）。§4-2 の表に沿って旧 README（implementation base）と新 README（head）の節を対応させ、本文の差分・anchor・reasonCode を機械で照合する。
- 未知の操作経路の疎通結果: `store plan` を実データに対して実行した例は無い。初回確認は Phase B の §4-1 が兼ねる。不成立時は reasonCode を記録して停止（§0 停止規則）。
- 人間の判断が必要な条件: 再開条件 RC1〜RC3 と S3 の数値の採用（A3）。Codex 側 copy の扱いと、合流の承認（本スライスでは判断を求めるだけで実行しない）。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: なし（AC4・AC7・AC8 は git と grep の手順で足りる）。
- 機械検査: 両 audit、AC4・AC7・AC8。
- A0/A1 主担当: 本 HANDOFF の作成 session（調査レポートと同じ）。
- A2 独立レビュー: 3 件（§5 末尾）。1 件目は A1 初稿だけを読んだ。2 件目・3 件目は先行を読んだ再評価で、独立性制約あり。3 件目がアーキテクチャゲートを担当した。
- A3 統合担当: 人間（owner）。統合案の本文反映は A2 1 件目の session が行った（§5 末尾「A3 統合案」）。
- C' 用に予約した担当: A2 で使っていないベンダー、または人間。文書差分と §6 の要約だけを読む。
- C' の入力: 対象 commit、A3 凍結本文、最終の文書差分、両 audit の出力、§6 の要約（path と hash を除く）、AC4 の比較結果。生 manifest と発見 C・判定 C の所見は入れない。
- 独立性の強化条件を満たせない場合の理由: A2 で 3 ベンダーを使ったので、C' に未使用のベンダーを用意できなければ人間が担当するか、`独立性制約あり` と記録して owner が採否を判断する。

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

### A2 レビュー結果（2 件目、独立性制約あり）

- 担当: owner が別サービスで依頼したレビュー（OpenAI の GPT 系。モデル名は owner 提供の file 名による）。GitHub の `develop`（`6ed90d69`）と本文書を読んだ。**1 件目（R1〜R10）を読んだ上での再評価なので、1 件目からは独立していない。** ローカルの実ストア・Codex 側 copy・manifest は見ていない。
- 原本: owner の手元（git 管理外）。必要な内容は以下に要約した。
- 総評: closeout の判断と scope の小ささには賛成。A3 前に要修正。「完成」と宣言できる範囲（同一 Windows ユーザー・非仮想化 process の基本操作と運用契約）と、宣言できない範囲（CURRENT の合流、`consolidate`、Codex 側 copy、別 PC / Cloud の直接 R2 取得、保留機能）を分ける。§2 の問いを「利用できる入口と成立しない入口を読者が取り違えない形で完了の境界を書けるか」に変えることを A3 の最重要判断とする。
- R1〜R10 の再評価: 全件採用。R1・R3・R4・R7・R8 は A3 前に必須、R2・R5・R6・R9・R10 は同じ改訂で取り込む。

| # | 指摘 | 確認（2 件目の記録担当による照合） |
|---|---|---|
| B1 | 完了宣言と、成立しない CURRENT 入口の不整合（R7 と同旨）。README・`docs/README.md`・program の該当箇所の全てで、入口の隣に「実ストアに未移行で参照できない」と書く。`plan passed` と `CURRENT available` は別の値として記録する。 | R7 と一致。 |
| B2 | I1（実ストアへの書込み 0）と、§5 AC1 の「必要なら manifest を `evidence stage` で渡す」が衝突する。stage は実ストアに state・catalog・artifact を書く。推奨は生 manifest を Evidence にせず、C / C' には path を除いた要約・Git 差分・audit 出力だけを渡す。 | 妥当。stage の書込み先は実ストアの `Artifacts` 配下（README「ローカル固定」）。1 件目は I2 側だけを見て、この衝突を見落とした。 |
| B3 | S4「別 PC で Player と Content を再現」は AC5 で示せない。`content fetch` は既存資格情報のある同一ユーザーの host が前提で、別 PC からの直接 R2 取得は program §4 C の未完了事項。S4 を「対応済みの host で取得した Player と `payload/` を利用者が別 PC へ手で運び、Delivery の Local/LAN directory に渡す手順」にし、手作業の工程と未実装の工程を分けて書く。文書を書いたことと 2 台で動かしたことを区別する。 | 妥当。§1 の対象外「Cloud・別 host の取得経路」と S4 の語が合っていない。 |
| B4 | AC5 の「`payload/` は 7 日清掃の対象」は README の known limit と逆。 | **README に根拠あり**: 「開発Content 1件の保存と取得」の後続の作業の列挙に「公開・取得で残るprivate operationの期限清掃」がある。R8 を「根拠なし」から「README と矛盾」に強める。 |
| B5 | 再開条件 3 件が非公開のレポートにしかない（R1 と同旨）。owner が採用する 3 条件を本文に転記し、保留項目と結び付け、回数・観測先・判断者を書く。原本を読めないレビュー担当は条件を作らない。 | R1 と一致。 |
| N1 | plan の `status: passed` は manifest を作れたことであって、Harness 4 task を公開できることではない。§6 を (1) plan の実行結果、(2) 単位の集計（publish / skip / hold / excluded）と 4 task の単位 ID・状態・理由、(3) 運用上の結論（合流できる・保留して設計判断が要る・plan 自体の失敗）に分ける。hold を自動で解消しない。 | 妥当。単位の状態が `publish` / `skip` / `hold` であることは実装と一致。 |
| N2 | 停止は `status != passed` の全件。再実行で結果を埋めず、環境の事実を記録して返す（R9 と同旨）。 | R9 と一致。 |
| N3 | 文書ごとの残し方。program を削除するなら、PR link だけを運用仕様の唯一の置き場にしない。`review-evidence.md` の `schedule status` は既存の確認への追加で、未登録・仮想化・identity 不一致を自動で直さない。 | 妥当。 |
| N4 | §0 の branch 欄（「次の担当が切る」）と引継ぎ票（「作成済み」）が食い違う。GitHub に branch は無く、ローカルだけ。 | 妥当。branch はローカルにのみある（未 push）。branch 欄をこの記録と同じ commit で直した。 |
| N5 | C' の入力が「文書と manifest 要約だけ」では曖昧。対象 commit、A3 凍結本文、最終の文書差分、audit 出力、単位分類の匿名化要約、契約・anchor 比較の結果を揃え、生 manifest と C の所見は入れない。 | 妥当。 |
| N6 | I2 の検査は PR 本文だけでなく、B / C のログ・表・commit diff の全文に及ぶ。program に具体的なローカル作業 directory の記述があり I2 と矛盾しないか A3 で確かめる。 | 妥当。当初「program に実値は無い」と照合したが誤り。3 件目の指摘のとおり、program §4 B の 1 行にローカル作業 directory の実値がある（照合の検索 pattern が漏らした）。 |

- 推奨の受け入れ境界（A3 の改訂入力）: I1 を「`store plan` と確認操作は両ストアと R2 のデータ・metadata を変更・削除しない。manifest は両ストアの外の新規 path。Evidence の保存・引渡しは別の境界として明示し、生 manifest を扱わない」に、S6 を「両 audit に加えて anchor の解決、契約節の原文比較、`git diff` の変更範囲、公開物の露出検査」に広げる。GO は本スライスが条件を満たしたことだけを意味し、`consolidate` の完了・CURRENT の合流・Cloud 対応を含まない。plan が失敗した時に文書だけを改訂しても GO にしない（§2 判定定義の NO-GO 時の扱いと同じ）。

### A2 レビュー結果（3 件目、独立性制約あり）

- 担当: owner が別サービスで依頼したレビュー（xAI の Grok 4.7。レビュー本文の自己申告）。入力は 1 件目・2 件目を含む本文書の全文で、`develop` 上の実装と文書に照合している。**先行 2 件を読んでいるので独立していない。** アーキテクチャゲートを担当した（新しいクラス・依存・所有者・寿命・公開 API は無く、対象は文書の責務）。
- 原本: owner の手元（git 管理外）。要約は以下。
- 総評: この版は A3 で凍結しない。完了宣言の受け入れ条件が、実ストアの入口・README の契約・`content fetch` の残置と合っていない。スライスは完了宣言・実データの plan・README 再編・別 PC 手順・Phase D の 1 文を一つの問いに束ねているので、A3 で見出しの対応表と、plan の結果が文書の合否を止める境界を凍結する。README を分割せず節で再編する判断は妥当。

| # | 指摘 | 確認（記録担当による照合） |
|---|---|---|
| G1 | 再開条件 3 件が本文に無い（R1・B5 と同旨）。 | 一致。 |
| G2 | 完了後も入口が CURRENT のまま残せる。plan の `passed` は CURRENT の存在ではない（R7・B1 と同旨）。 | 一致。 |
| G3 | S2 の対象が program の status と §4 B〜E だけで、§0（「進行中、後続 B〜D の A3 は別途」）と §5（「残件は A〜D を実装キュー」）が残ると、完了宣言と実装キューが同じ文書に併存する。 | **新規。妥当**。program §0 の status と §5 の残件の文を確認した。 |
| G4 | AC5 の 7 日清掃は実装と逆（R8・B4 と同旨）。`content fetch` は新規 private operation の `payload/` に書き、Content 清掃はその残置を読まず消さない。 | 一致。`ContentCleanup.psm1` に「content fetchの残置と同じ扱い」で清掃しない旨のコメントがある。 |
| G5 | S4 は AC5 の 4 手順では示せない。Delivery は Local/LAN directory のほかに manifest digest・contentSet・revision・cache root・disk budget を要し、Player の置き方も AC5 に無い（B3 に加えて入力項目を具体化）。 | 妥当。`20-variant-checkout-workflow.md` §3 の手順 3 がこれらの入力を要求している。 |
| G6 | §5 の「必要なら manifest を `evidence stage` で渡す」が I1 を破る（B2 と同旨）。 | 一致。 |
| G7 | README 再編で anchor が切れる。`AGENTS.md` が指す `#エージェント操作の事前承認` の見出し名は残す（R3 と同旨）。 | 一致。 |
| G8 | AC4 の grep では code を含まない契約文の削除を検出できない（R4 と同旨）。 | 一致。 |

- 同じ改訂で取り込むもの（先行と重ならない点だけ）:
  - `excluded` の理由 code にも `unrecognized`、`unreferenced-transfer`、`credentials`、`out-of-scope-role`、`revision-locks`、`not-owned`、`consolidation-run` がある。Harness 4 task が単位に入らなければ、その `excluded` の理由を §6 に書く。
  - manifest の `sourceRoot` と `logicalRoot` は絶対 path なので、§6 にも PR にも写さない。
  - program §4 B の 1 行にあるローカル作業 directory の実値を、program を改訂するときに公開本文から除く（I2）。
  - その他（理由 code の転記、日常操作節を行数で決める、管理操作の置き場、`status != passed` の全件停止、Phase D の 1 文の内容、§6 の 3 区分、C' の入力、GO の意味）は 1 件目・2 件目と同旨。
- 不変条件を破る順序: I1 は plan では破れず、AC1 の文言どおり `evidence stage` する場合と手順外の `consolidate` で破れる。I2 は `sourceRoot` の転記と、program の既存の実値の残置で破れる。I3・I4 は 1 件目と同じ。

### A3 統合案（owner の承認待ち）

- 担当: A2 1 件目の session。入力は A2 3 件と、owner が依頼した A3 前の差分レビュー（4 件目。git 管理外。「A2 の転記だけで本文の受け入れ境界が改訂されていない」と指摘し、下表の 10 点と X1 を求めた）。
- 本文の改訂: §1〜§5 を書き直した（A1 初稿は commit `82b0edc5`）。**A3 は未完了。** owner が下の「owner が決めること」を確定し、本文を凍結するまで Phase B を始めない。
- owner が決めること:
  1. 再開条件 RC1〜RC3（§2）の採用。調査レポート（owner の手元層）§7.2・§7.5 から逐語に近い形で転記した。A2 は 3 件とも「条件を作らない」としたので、内容は変えていない。
  2. S3 の数値（導入と日常操作の表で 30 行以内・8 KB 以内）。
  3. README を縮める方針の変更: A1 初稿は README を約 100 行縮める案だったが、統合案は I3 を守るため既存節を本文不変で移すだけにし、圧縮は冒頭 2 段落に限った。規模はほぼ変わらず、読みやすさは冒頭の表と節の割当で得る。調査レポート T2 の「README の圧縮」はこの範囲でしか達成しない。

| 指摘 | 採否 | 反映した節 | 確認方法 |
|---|---|---|---|
| R1 / B5 / G1 再開条件が本文に無い | 採用 | §2 RC1〜RC3、AC3 | owner の採用確認（A3）。AC3 |
| R2 / N1 理由 code の列挙と plan 結果の 3 層 | 採用 | §1 現況、§2 S1、AC2 | §6 の表の形を C が照合 |
| R3 / G7 anchor 切れ | 採用 | §1 現況、§2 I3 (3)、§3、§4-2 表、AC4 (3) | AC4 (3) |
| R4 / G8 code を含まない契約文の削除 | 採用 | §2 I3 (1)(2)、§4-2 表、AC4 (1)(2) | AC4 (1)(2) |
| R5 「1 画面」が観測できない | 採用 | §2 S3、§3 の規模を bytes に | AC4 (5) |
| R6 管理操作の置き場 | 採用 | §2 S3（4 節）、§4-2 表 | AC4 (1) の節の対応 |
| R7 / B1 / G2 成立しない入口 | 採用 | §1 目的 1、§2 問い・S7、AC8 | AC8 |
| R8 / B4 / G4 7 日清掃の誤り | 採用 | §1 現況・対象外、AC5 の不合格例、所有する後続 | AC5 |
| R9 / N2 停止条件 | 採用 | §2 AC1、本文へ転記した実装制約、§4-1 | AC1 |
| R10 Phase D の 1 文 | 採用 | §2 AC6 | AC6 |
| B2 / G6 I1 と `evidence stage` の衝突 | 採用 | §2 I1、§5 検証経路 AC1（manifest は手元層、Evidence に入れない） | AC1 の不合格例 |
| B3 / G5 手で運ぶ往復と直接取得の区別、Delivery の入力 | 採用 | §1 現況・対象外、§2 S4・AC5 | AC5 |
| G3 program §0・§5 との併存 | 採用 | §1 現況、§2 S2、AC3 | AC3 |
| N3 文書ごとの残し方 | 採用 | §2 S2・AC6、§4-2 | AC3・AC6 |
| N4 branch 欄の食い違い | 採用（A2 2 件目の記録時に修正済み） | §0 | — |
| N5 / G 追加 C' の入力 | 採用 | §5 C' の入力 | C' の bundle |
| N6 / G 追加 公開物の露出、program の実 path | 採用 | §2 I2、AC8 | AC8 |
| G 追加 `excluded` の理由 code、`sourceRoot` / `logicalRoot` | 採用 | §2 S1・I2 | AC2 |
| G 総評 plan の結果が文書の合否を止める境界 | 採用 | §2 判定定義、§4 差し戻し条件 | — |
| X1（4 件目）`tools/` 差分 0 と README 変更の矛盾 | 採用 | §1 対象外、§4 対象外の維持、AC7 | AC7（`git diff --name-only`） |
| GO の意味（2 件目・3 件目・4 件目） | 採用 | §2 判定定義 | — |

- 不採用: なし。保留: なし（owner が決める 3 点を除く）。

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
