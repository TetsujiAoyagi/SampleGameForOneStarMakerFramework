# HANDOFF: Artifact Storage の完了宣言と分裂 store の合流 plan

## 0. メタデータ

- type: `slice`
- status: `C`（発見 C 待ち。A3 は owner が 2026-10-17 に凍結。Phase B と動作確認は 2026-10-17 に完了）
- branch: `claude/artifact-storage-closeout`（`origin/develop` の `6ed90d69` から作成済み。ローカルのみで未 push。本文書の A1 初稿が最初の commit `82b0edc5`）
- planning base commit: `6ed90d69`（`origin/develop`、PR #132 マージ後。ローカルの `develop` は遅れているので先に `git fetch origin develop`）
- implementation base commit: `855e3da3`（Phase B 開始時の branch の HEAD。凍結本文 `7b7f243d` と同じ受け入れ境界で、§0 の記録だけが違う）
- implementation head commit: `4a9a0840`（Phase B の出口の commit）
- risk: `low`（文書改訂と読取のみの `store plan`。本スライス固有の操作（`store plan` と分類の確認）は実ストア・Codex 側 copy・R2・資格情報へ書込み・削除をしない。レビュー Evidence の通常の保存・引渡しは `review-evidence.md` に従う別の境界で、I1 のとおり）
- owner: OSM maintainers
- created: 2026-10-17
- expires: 2026-11-17 または置換 revision
- harvest to: `tools/Artifacts/README.md`、`docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md`（完了宣言後は削除候補）、`docs/README.md`、`.agents/skills/osm-workflow/references/review-evidence.md`
- Phase A snapshot の取得案内・状態: 固定済み。凍結 commit `7b7f243d`、本文の content sha256 `7fc425a09d678f355e118f99fa0a95eed726c5793aa87e1dead6d23577576ad2`（凍結 commit 時点の本文書。文書だけなので stage は不要）。`git show 7b7f243d:docs/handoff/ARTIFACT_STORAGE_CLOSEOUT.md` で取得する
- Phase B result snapshot の取得案内・状態: 未生成（B の結果は §6 と implementation head の commit。manifest は git 管理外の手元層にあり、所在は引継ぎ票の取得案内 note）
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase と役割: 発見 C（Phase B と異なるモデルの新規 session）。Phase B と動作確認（AC1 の `store plan`）は 2026-10-17 に完了し、動作確認の期待との差は無かった（§6）。発見 C は GO 判定をせず、C' を起動しない。
  - 引き継ぐ commit: branch `claude/artifact-storage-closeout`（ローカルのみ、未 push）の HEAD。implementation head は `4a9a0840`（B の出口の commit）で、その後の commit は §0 の記録だけを変えている。開始時に `git diff --stat 4a9a0840 HEAD` が本 HANDOFF だけであることと、`git status --short` で tracked file に差分が無いことを確かめ、食い違えば引き継がずに止める。untracked の `docs/handoff/ARTIFACT_STORE_VIRTUALIZATION.md` は本スライス外の文書で、触らない。
  - 次の作業: 固定した差分 `855e3da3..4a9a0840` を、§2 の I1〜I4・S1〜S7 と AC1〜AC8、§3 の責務マップに照らしてレビューする。AC4 は旧 README（`git show 855e3da3:tools/Artifacts/README.md`）と新 README の節の本文・anchor・reasonCode・S3 の行数と bytes を、AC7 は `git diff --name-only 855e3da3..4a9a0840` を、AC8 は §2 の grep と I2 の語の検査を機械で照合する。S1 は §6 の要約を manifest と照合する（下の取得案内）。
  - manifest の取得案内: この checkout の git 管理外の手元層（`.gitignore` で除外された作業 directory）にある `ARTIFACT_STORAGE_CLOSEOUT.store-plan-note.md` に、manifest と stdout の絶対 path を書いた。同じ PC で読取専用で開く。path の実値と manifest の中身を tracked 文書・commit・PR・Evidence・C' の bundle へ写さない（I1・I2）。
  - 必要な権限: 読取だけ。資格情報・network・Unity は不要。`store plan` を再実行しない（AC1 は 1 回）。
  - 停止規則: §2 の停止規則と blocker の基準（`phases-and-handoff.md`）。`store consolidate` は dry-run も含めて実行しない。
  - 引き渡さないもの: Codex 側 copy の削除・移動、R2 の設定変更、保留 list の機能の実装、Unity の変更、manifest の path の実値。発見 C の所見は C' に渡さない。
  - 判断の根拠: 2026-10-17 の独立調査レポート（owner の手元層にあり git 管理外。必要な事実は §1、再開条件は §2 に転記済み）。owner は同日に「Artifact Storage 開発はここで完了」と決めた。
  - 発見 C から C' までを 1 つの session がまとめて進める場合（オーケストレーター向け）:
    1. 着手前に `.agents/skills/osm-workflow/SKILL.md`、`references/phases-and-handoff.md`、`references/review-evidence.md` を全文読む。進め方の正本はこれらで、ここは本スライス固有の注意だけを書く。
    2. 順序: 発見 C → 差し戻しがあれば B（上限 4 巡）→ 未公開の試行錯誤 commit の整理 → GO 候補 head の固定 → 判定 C → C'（差し戻しは別に上限 3 巡）。上限で収束しなければ、残る指摘と違反根拠を付けて人間に返す。
    3. 各 Phase は新規 session で行う。オーケストレーター自身は所見を作らず、各担当の固定入力と出口を管理する。担当のモデル: 発見 C・判定 C は Phase B と異なるモデル、C' は B・C の両方と異なるモデルで blind bundle だけを読む（満たせなければ C' は未実施として人間へ返す）。使った担当とモデルは §7・§8 に記録する。
    4. 盲検の保護: 発見 C・判定 C の所見は C' 完了まで HANDOFF に commit しない（implementation head の下や base..head に入ると C' の完全 diff に所見が混ざる）。§7・§8 の記録は C' 完了後にまとめて commit する。C' の入力は §5「C' の入力」だけで、生 manifest・C の所見・PR は入れない。§6 の要約と manifest の一致は C' が監査できない範囲として §8 に記録する。
    5. 本スライスのテストは両 audit と AC4・AC7・AC8 の機械照合だけで、Unity・テスト suite は実行しない。判定 C は GO 候補 head で両 audit を再実行し、照合は git と grep で行う。S1 は判定 C が同じ PC で manifest を読取専用で開いて §6 と照合する。`store plan` は再実行せず、`store consolidate` は dry-run も含めて実行しない。
    6. Evidence の保存・引渡しは `review-evidence.md` に従う（`tools/Artifacts/README.md`「エージェント操作の事前承認」の範囲で再確認不要）。Artifacts の CLI は非仮想化の pwsh（PowerShell tool）から、config・selection を絶対 path で渡して使う。Bash では LocalApplicationData が仮想化されるので使わない。新しい worktree では先に 3 project の Release build を用意する。保護名（`Credentials.json` 等）の file は stage できないので改名する。判定 evidence を取った head は push しない軽量 tag `evidence/<taskId>/<n>` で残す。
    7. 外部 CLI（`cursor-agent`）を使う担当は Grok 系モデルだけを明示して使い、試行錯誤で繰り返し呼ばない。
    8. オーケストレーターがしないこと: push、PR 作成、マージ、Phase D、`workflow-task.ps1 end`、owner の判断の代行。C' 完了後に §0 の引継ぎ票を Phase D（owner）向けに更新して止まる。

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
  - 非仮想化 process で `pwsh tools/harness.ps1 current -Task artifact-evidence-lifecycle` は「CURRENT が見つかりません」で失敗する。README（冒頭と「Evidenceの責務とoffline検証」）・`docs/README.md`・program §0 はこのコマンドを「固定仕様・原観測の入口」と呼んでいる。`tools/Harness/README.md` も、同じく Codex 側にしか無い `h1-transport-identity`・`artifact-evidence-lifecycle`・`h2c-unity-gate-r2` の CURRENT を入口として案内している。README「分裂したストアの合流」の「運用の前提」と `docs/README.md` の H1 の説明にある `harness.ps1 current` は、特定 task を指さない一般の手順である。
  - `store plan` / `store consolidate` は PR #126 で実装・検証済み（`StoreConsolidationPlan` 24 case、`StoreConsolidation` suite）だが、実データへの plan は未実施。plan の `status: passed` は manifest を書けたことを示すだけで、単位が公開できることや CURRENT が読めることを示さない。単位の状態は `publish` / `skip` / `hold`。
  - `origin/develop` で 2026-10-10 以降の commit は `unity/Assets` 1 件に対し `tools/Artifacts` 37 件。Artifacts の非テストコードは約 10,900 行、README は 466 行・約 103 KB（1 行が 1 段落なので行数は規模の指標にならない）。
  - 他の文書から README の 9 つの見出しへ anchor link がある: `AGENTS.md` ほかが `#エージェント操作の事前承認`、`tools/Workflow/README.md` と `tools/Harness/README.md` が `#実行するprocessの条件appdata仮想化`、`review-evidence.md` が `#レビュー用の変更集合changespatch`、program が Build / Content 系の 6 見出し。`docs-audit.ps1` は anchor を検査しない。
  - Unity 側は Artifact Storage を参照しない。Storage で取得したものを Unity 側で使う経路は 2 つあり、どちらも 1 ページに書いた手順が無い。(a) Editor Play: `content fetch` の `payload/` を、Unity Editor の Delivery window（`Tools/OSM/Content/Delivery`）で Local/LAN directory に指定し、manifest digest・contentSet・revision・cache root・disk budget を入力して Prepare → **Use For Next Play**（次回以降の Editor Play に適用。`20-variant-checkout-workflow.md` §3、`04-app-startup.md`）。(b) Player: `build fetch` で取得した BS4 Player を起動する。Player が install 済みの Content を使う時は `content:installedRevisionPath` と `content:manifestSha256` の pair（installed override）を渡す（`04-app-startup.md`）。Use For Next Play は Player には効かない。
  - `content fetch` は新規 private operation の `payload/`（`transport.json` と `content/`）に取得する。この operation の期限清掃は README「開発Content 1件の保存と取得」で後続の作業とされ、Content の件数清掃もこの残置を読まず消さない（`ContentCleanup.psm1` のコメント）。Evidence の 7 日清掃（604800 秒）は Evidence catalog の staging の規則で、ここには及ばない。
  - program は §0 で「programは進行中」「後続B〜DのA3は別途」、§5 で「A〜Dのみを実装キューとする」と書いている。§4 B の 1 行にローカル作業 directory の実値がある。
  - Phase D で `schedule status` を確認する手順が無く、Store 版 pwsh の更新で日次 job が止まったことを人が気づく仕組みが無い（PR #129 で実測）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 利用できる入口と成立しない入口を読者が取り違えない形で、Artifact Storage の完了の境界を公開文書に書けるか。分裂 store の各単位は、読取のみの `store plan` でどう分類されるか。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - I1: `store plan` と分類の確認は、実ストア・Codex 側 copy・R2 のデータと metadata を変更・削除しない。manifest は両ストアの外の新規の絶対 path に書く。生 manifest を `evidence stage` / `publish` に入れない。レビュー Evidence の通常の保存・引渡し（`review-evidence.md`）は本条件の対象外の別の境界で、本スライスもそれに従う（免除しない）。Evidence に入れてよいのは §6 の要約、文書差分、audit 出力、AC4 の比較結果だけ。
    - I2: 本スライスで新たに公開するもの（改訂後の tracked 文書、commit diff の追加行、PR 本文、C / C' の bundle、§6）に、ローカル path の実値・Windows のローカル user / profile 名・SID・package 名・reference・packageSha256・manifest sha256 の実値を書かない。manifest の `sourceRoot` / `logicalRoot` は絶対 path なので写さない。program §4 B にある既存の実値は、program を改訂する時に除く。既存行を削除した diff の削除行は公開済みの履歴に残るので検査の対象外とし、その値を要約・コメントへ再び写さない。GitHub の owner 名や PR の URL は対象外。秘密が含まれていたと分かった場合は、文書の削除とは別に owner が安全上の扱いを決める。
    - I3: README の契約を失わない。(1) 既存の節（冒頭の導入 2 段落を除く）は本文を変えずに移す。節の本文は、その見出しの次の行から次の見出し（階層を問わない）の前までとする。変えてよいのは見出しの階層と節の順序だけ（下位節が全て移って空になり、参照元の anchor も無い見出しは消してよい）。例外は S7 の注記を既存の文の後に追加することだけで、既存の文の削除・言い換えは許さない。追加した注記は §6 に場所を列挙する。新しく足す見出しは既存の見出しと同じ名前にしない（GitHub は同名の見出しに `-1` を付けるので、既存の anchor がずれる）。(2) 冒頭 2 段落から削った文は §6 に全て列挙し、各文が「経緯（PR 参照へ置換）」か「新 README の別の箇所に同じ内容がある（その箇所を示す）」のどちらかである。(3) 他の文書からの既存 anchor link が全て新 README で解決する。`#エージェント操作の事前承認` を含む既存の見出し名は変えない。
    - I4: 保留 list の項目を次の仕事と読める書き方にしない。各項目は下記の再開条件のどれかと、再開を判断する者（owner）を持つ。
  - 再開条件（調査レポート §7.2・§7.5 から転記。owner が 2026-10-17 に採用）:
    - RC1: エンジン側スライス（S-5 / S-8 / S-9、InGame 縦糸）が Storage の不足で実際に止まった。
    - RC2: 同じ障害が実運用で 2 回以上起き、既存の reasonCode と README の手順で回復できなかった。
    - RC3: Cloud / 別 PC の読み手が実際に稼働を始めた（HANDOFF に担当として割り当てられた）。
    - 再開は、どれかを A0 に観測事実（日付・task・reasonCode）付きで書けた時に限る。仮定上の障害では再開しない。
    - 安全上の例外: 秘密の露出、保護対象の誤削除など既存の安全契約に違反したことが確認された場合は、RC1〜RC3 を待たず初回で該当操作の利用を止め、owner が評価する。これは機能開発の再開条件ではなく、保留 list の項目にもしない。
  - 成立条件（起きなければならないこと）:
    - S1: `store plan` を 1 回実行し、§6 に 3 層で記録する。(1) exit code・`status`・`reasonCode`。(2) manifest の単位数（`publish` / `skip` / `hold`）と `excluded` 数、Harness 4 task の単位 ID・状態・理由 code（単位に入らなければ `excluded` の理由 code）。(3) 結論: 合流できる単位、保留のまま設計判断が要る単位、plan 自体の失敗。理由 code は manifest のまま転記する。既知の例は、保留が `conflict`、`acl-mismatch`、`reparse`、`source-unreadable`、`reference-missing`、`reference-localcache`、`reference-out-of-role`、`source-unchecked`、`source-invalid`、`multi-workflow-unit`、`workflow-already-visible`、`dependency-held`、除外が `unrecognized`、`unreferenced-transfer`、`credentials`、`out-of-scope-role`、`revision-locks`、`not-owned`、`consolidation-run`（網羅ではない）。`passed` を「CURRENT が読める」「合流できる」と記録しない。
    - S2: program の status を「完了（保留 list 付き）」にし、§0 の status・§4 B〜E・§5 の残件の文を同じ意味に揃える（完了宣言と実装キューを同じ文書に併存させない）。§4 B〜E の未着手項目を再開条件付きの保留 list へ移す。`docs/README.md` の program 表も同じ状態にする。program から削ってよいのは PR・Git 履歴に残る経緯だけで、§2 の保存契約と §5 の責務・残す安全性・採用・不採用の判断は残す。
    - S3: README を「日常操作」「管理操作」「契約と known limits」「経緯」の 4 節に再編する（節の割当は §4-2 の表）。冒頭の導入の直後に日常操作節を置き、その最初の下位見出しより前は 7 コマンド（`evidence stage` / `evidence fetch` / `build collect` / `content collect` / `build cleanup` / `content cleanup` / `evidence schedule status`）の表と短い注記だけにする。冒頭の導入とこの表の部分を合わせて 30 行以内・8 KB 以内にする（owner が 2026-10-17 に採用。画面での見え方を保証する値ではない）。
    - S4: Storage で取得した Player と Content を別 PC へ手で運んで使う手順を README の日常操作節に 1 節置く。Editor Play の経路と Player の経路を分けて書く（AC5）。別 PC から直接 `content fetch` / `build fetch` できるとは書かない。
    - S5: Phase D の手順に `schedule status` の確認が 1 文入る（`review-evidence.md` の「Phase D の保存・引渡し確認」）。
    - S6: 機械で確かめられる条件が通る: `pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1`、anchor の解決、契約節の本文一致、変更 file 一覧、公開物の露出検査（AC4・AC7・AC8）。
    - S7: Codex 側にしか無い 4 task（`artifact-evidence-lifecycle`、`h1-transport-identity`、`h2c-unity-gate`、`h2c-unity-gate-r2`）の `harness.ps1 current -Task <id>` を固定記録の入口として案内する公開文書の全箇所（README・`docs/README.md`・program・`tools/Harness/README.md`）の隣に、合流前は実ストアに無く非仮想化 process から読めないことと、S1 の結論を書く。結論は単位の状態と理由 code で書き、理由が無い単位（`publish` / `skip`）は「理由なし・未合流」と書いて理由 code を作らない。`current -Task` の形でなく 4 task の CURRENT が読めると書く文も対象にする（`tools/Harness/README.md`「H2c Unity pilot」の、旧 task の CURRENT を保持し旧入口は読取のみとする文）。`store plan` が `passed` でない時は単位が無いので、単位の状態を作らず「実ストアに無く読めない。plan 未実施（<reasonCode>）」と書く（この場合は判定定義のとおり GO にしない）。特定 task を指さない一般の手順（「分裂したストアの合流」の「運用の前提」、`docs/README.md` の H1 の説明）には注記しない。
- 受け入れ条件:
  - AC1（S1, I1）: `store plan` を非仮想化 pwsh で 1 回実行する。`status` が `passed` 以外なら reasonCode を記録して止め、再実行・store の変更・`consolidate`（dry-run を含む）で結果を埋めない。不合格例: manifest を実ストアか Codex 側 copy の配下に書く。既存 file へ出力する。manifest を stage する。
  - AC2（S1, I2）: §6 の表は単位 ID・task ID・状態・理由 code・数だけを持ち、path と hash の実値を持たない。
  - AC3（S2, I4）: program の保留 list の各項目に RC1〜RC3 のどれかと判断者が付いている。program に「進行中」「実装キュー」の現在形の記述が残らない。合格例: 「Content 確定記録 — 再開: RC2（別 PC からの同時公開の衝突が実運用で 2 回以上、README の手順で回復できない）。判断: owner」。不合格例: 「後続で扱う」。
  - AC4（S3, I3）: 発見 C が次を照合する。(1) 旧 README の各節（冒頭 2 段落を除く）の本文と、新 README の同名の節の本文の差分が、§6 に列挙した S7 の注記の追加だけである。本文が空行だけで参照元の anchor も無い見出し（`保守とoffline検証`）の削除は、この照合の対象外とする。新しく足した見出しが既存の見出しと同名でない。(2) §6 の削除文一覧の各文の分類と、示した箇所に同じ内容があること。(3) 参照元（`git grep -o 'Artifacts/README.md#[^) ]*'` で抽出）の全 anchor が新 README の見出しから作る GitHub の slug と一致する。slug は見出し文字列そのものではなく、英字の小文字化・記号と全角括弧の除去・空白の `-` 化をした形で照合する（見本: 見出し `実行するprocessの条件（AppData仮想化）` → `#実行するprocessの条件appdata仮想化`）。(4) 旧 README の reasonCode 名（`` `[a-z]+(-[a-z0-9]+)+` `` の形）が全て新 README に残る。(5) S3 の行数・bytes。
  - AC5（S4）: 手順が次のとおり書かれている。共通: (1) 資格情報のある非仮想化 host で `content fetch`（Content）と、Player を使うなら `build fetch`（Player）を実行し、取得先を確認する。(2) 利用者が別 PC へ運ぶ（手作業）。Editor Play の経路: (3) 別 PC の Unity Editor（project を開いた状態）の Delivery window で Local/LAN directory に Content の `payload/` を指定し、manifest digest・contentSet・revision・cache root・disk budget を入力して Prepare する。(4) requested と installed の revision の一致を確認する。(5) Use For Next Play の後に Editor Play。Player の経路: install 済みの Content を Player に使わせる方法（`content:installedRevisionPath` と `content:manifestSha256` の pair）と Player の置き方・起動条件は、既存文書（README「開発Build 1件の保存と取得」「BS4 Player出力の退避と保存」、`04-app-startup.md`、`20-variant-checkout-workflow.md`）にある事実だけを書き、無ければ「未記載」と書く。Use For Next Play が Player に効くとは書かない。`content fetch` の operation は自動清掃されずに残ると書く（一括削除の手順は書かない）。2 台での実機往復は未検証と書く。不合格例: 「別 PC で `content fetch` する」「`payload/` は 7 日で清掃される」「Use For Next Play で Player が新しい Content を使う」「2 台で確認済み」。
  - AC6（S5）: `review-evidence.md` の Phase D 節に、非仮想化 pwsh で `evidence schedule status --profile osm --config <v2-config>` を確認する 1 文が入る。文は、仮想化 host では非仮想化の担当へ依頼すること、`passed` 以外（未登録、`scheduler-identity-mismatch` 等）をこの確認では直さず owner が README「自動清掃」の切替手順で処置することを含む。
  - AC7（S6）: 両 audit の出力を §6 に貼る。`git diff --name-only <implementation base>..<implementation head>` の一覧が §3 の変更対象 6 file に限られ、`unity/` と `tools/` 配下のコード・script・設定の変更が 0 である。
  - AC8（S7, I2）: `git grep -nE 'current -Task (artifact-evidence-lifecycle|h1-transport-identity|h2c-unity-gate|h2c-unity-gate-r2)'` で見つかる公開文書の全箇所（本 HANDOFF を除く）に未成立の記述が隣接している。`tools/Harness/README.md`「H2c Unity pilot」の旧入口の文（grep に当たらない）にも注記が隣接している。一般の手順の箇所に注記が付いていない。`tools/Harness/README.md` の差分は S7 の注記の追加だけで、他の文の削除・言い換えが無い。改訂後の公開文書の全文、commit diff の追加行、PR 本文を I2 の語（ローカル path の実値、Windows のローカル user / profile 名、SID、package 名、reference、packageSha256）で検査し、0 件である。program §4 B の既存の実値が除かれている。
- ここでは答えない問いと所有する後続:
  - `store consolidate` の実行と、保留単位の解消（owner が manifest を承認した後の別 task。I1 のため本スライスでは実行しない）。
  - Codex 側 copy の保持・削除・期限（owner の判断。program の保留 list に「Codex 側残置の後始末」として残す）。
  - R2 lifecycle / 条件付き書込み / Read only token の採用（保留 list。再開条件は RC3 等）。
  - `content fetch` の operation の期限清掃（保留 list。README の後続の作業の記述を保つ）。
- 判定定義: GO = S1〜S7 と I1〜I4 を満たす。全単位が保留でも、分類と理由 code を記録できれば GO を妨げない。GO は本スライスの条件を満たしたことだけを意味し、`consolidate` の完了・CURRENT の合流・Cloud 取得を含まない。NO-GO = `store plan` が `passed` にならず、原因が環境側で文書改訂だけでは問いに答えられない場合。この場合は §4-2 だけを進めて「plan 未実施」と明記し、GO にしない。
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
| `tools/Harness/README.md` | Harness の固定記録の入口 | S7 の注記だけを足す（4 task の CURRENT の入口の隣）。それ以外の文は変えない。README への anchor の参照元でもある | — | +3 行程度 |
| `AGENTS.md`、`tools/Workflow/README.md` | README への anchor の参照元 | 変更しない。AC4 (3) の照合対象 | — | 0 |

コードの変更は無い。README は既存節を縮めないので規模はほぼ変わらない。読みやすさは冒頭の日常操作の表と節の割当で得る（I3 を優先し、圧縮は冒頭 2 段落に限る）。

## 4. 実装計画

- 変更対象: §3 の変更対象 6 file（本 HANDOFF を含む）。
- 順序:
  1. **`store plan`（読取のみ）。** 非仮想化 pwsh（PowerShell tool か owner の端末）で `pwsh tools/artifacts.ps1 store plan --source-root <Codex 側 OneStarMaker の絶対 path> --output <手元層の新しい file の絶対 path>` を 1 回実行する。stdout の `status` が `passed` 以外なら reasonCode を §6 に記録して止める（§0 停止規則）。manifest を読み、S1 の 3 層で §6 にまとめる（path と hash の実値は書かない）。`consolidate` と dry-run は実行しない。
  2. **文書改訂。** program → README → docs/README → Harness README → review-evidence の順。README の節の割当は次の表のとおり（見出し名は変えず、階層だけを変えてよい）。

     | 現在の節 | 新しい節 | 扱い |
     |---|---|---|
     | 冒頭の導入 2 段落 | 導入／経緯 | 第 1 段落は短い導入に縮めてよい（完了の範囲と未成立の範囲を §1 目的 1 のとおり書く）。第 2 段落（スライス A の経緯）は経緯節へ移し PR 参照に置き換えてよい。CURRENT の入口の文には S7 の注記を付ける。削った文は §6 に列挙（I3 (2)） |
     | （新規）日常操作の表 | 日常操作 | 7 コマンドと、各コマンドの詳細節への link |
     | Evidence v2 の通常操作、ローカル固定（evidence stage）、R2への昇格（evidence promote）、レビュー用の変更集合（changes.patch） | 日常操作 | 本文不変で移動 |
     | 開発Build 1件の保存と取得、BS4 Player出力の退避と保存（build collect）、開発Content 1件の保存と取得、開発Buildの件数清掃、開発Contentの件数清掃 | 日常操作 | 本文不変で移動（現在は「保守とoffline検証」の下） |
     | （新規）別 PC で Player と Content を使う手順 | 日常操作 | AC5 |
     | 自動清掃、一回の旧開発環境reset、分裂したストアの合流、資格情報の操作、Synthetic Route proof、系列の退役 | 管理操作 | 本文不変で移動 |
     | エージェント操作の事前承認、実行するprocessの条件（AppData仮想化）、終了・再開・利用中と清掃、開発Buildの件数保持policy、Evidenceの責務とoffline検証 | 契約と known limits | 本文不変で移動。「Evidenceの責務とoffline検証」の CURRENT の入口の文の後に S7 の注記を足す（I3 (1) の例外。既存の文は消さない） |
     | 保守とoffline検証（見出しのみ） | — | 下位節が移った後の空の見出しは消してよい。参照元に anchor が無いことを AC4 (3) で確かめる |

  3. `pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1`、AC7 の変更 file 一覧、AC8 の grep。
- plan の結果の扱い: 全単位が保留でも、分類と理由 code を §6 に記録でき、S7 の注記を書ければ、本スライスの失敗とはしない（GO できる）。保留の解消に新しい設計（公開途中の不可視性を保つ方式等）が要る場合、その判断は合流の後続スライスへ送り、本スライスは文書改訂を続ける。
- Phase B から Phase A へ差し戻す条件: 本スライス自身の受け入れ条件を満たせない場合に限る。例: I3 の「本文不変で移動」を守ると S3 の行数・bytes を満たせない（条件の衝突）。plan の保留は差し戻しの理由にしない。
- 対象外を維持する方法: B の出口で `git diff --name-only <implementation base>..HEAD` を §6 に残す（AC7）。本スライス固有の操作では R2 へ何も送らない（network を使うコマンドを実行しない）。レビュー Evidence の保存・引渡しは `review-evidence.md` に従い、生 manifest を入れない（I1）。

## 5. テストとレビュー計画

- 単体テスト: なし（コード変更なし）。
- 差し戻し中の起点 `-Filter`: 対象なし。
- 判定必須テスト: `pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1`。加えて AC4・AC7・AC8 の機械照合（判定 C の担当が実行する）。
- 全 EditMode 回帰の適用除外: Unity を変更しないため適用外（program の文書改訂 PR #108 / #117 / #122 と同じ扱い）。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路:
  - AC1: 担当は Phase B。環境は非仮想化 pwsh。操作は §4-1 のコマンド 1 回。観測は exit code、stdout の `status` / `reasonCode` / `summary`、manifest の単位の状態と理由。合否は S1 のとおり。証拠は manifest（git 管理外の手元層）と §6 の要約。B は manifest の絶対 path を git 管理外の取得案内（手元層の note）に書き、引継ぎ票にはその note があることだけを書く（I2）。発見 C・判定 C は同じ PC で manifest を読取専用で開き、§6 の要約と照合する（別 worktree でも絶対 path で読める）。manifest は Evidence に入れない（I1）。C' は manifest を読まないので、§6 の要約と manifest の一致は C' の監査できない範囲として記録する。
  - AC4: 担当は発見 C（判定 C で再実行）。§4-2 の表に沿って旧 README（implementation base）と新 README（head）の節を対応させ、本文の差分・anchor・reasonCode を機械で照合する。
- 未知の操作経路の疎通結果: `store plan` を実データに対して実行した例は無い。初回確認は Phase B の §4-1 が兼ねる。不成立時は reasonCode を記録して停止（§0 停止規則）。
- 人間の判断が必要な条件: なし（A3 は凍結済み）。Codex 側 copy の扱いと、合流の承認（本スライスでは判断を求めるだけで実行しない）。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: なし（AC4・AC7・AC8 は git と grep の手順で足りる）。
- 機械検査: 両 audit、AC4・AC7・AC8。
- A0/A1 主担当: 本 HANDOFF の作成 session（調査レポートと同じ）。
- A2 独立レビュー: 3 件（§5 末尾）。1 件目は A1 初稿だけを読んだ。2 件目・3 件目は先行を読んだ再評価で、独立性制約あり。3 件目がアーキテクチャゲートを担当した。
- A3 統合担当: 人間（owner）。統合案の本文反映は A2 1 件目の session が行った（§5 末尾「A3 統合案」）。
- C' 用に予約した担当: AI なら Phase B・Phase C の両方と異なるモデル、新規 session、blind bundle（必須条件）。A2 で使ったベンダーであることは除外理由にしない。人間が担当する場合、発見 C・判定 C の結論を先に読んだか設計に関与していれば `独立性制約あり` と記録し、盲検の独立監査済みとは書かない。
- C' の入力: 対象 commit、A3 凍結本文、最終の文書差分、両 audit の出力、§6 の要約（path と hash を除く）、AC4 の比較結果。生 manifest と発見 C・判定 C の所見は入れない。
- 独立性の強化条件を満たせない場合の理由: 強化条件（Phase A〜C に関与していない系列）は高リスク変更の条件で、本スライス（risk `low`）には求めない。A2 で 3 ベンダーを使ったため、C' が A2 と同じベンダーになりうることを記録する。

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

### A3 統合と凍結（owner が 2026-10-17 に凍結）

- 担当: A2 1 件目の session。入力は A2 3 件と、owner が依頼した A3 前の差分レビュー（4 件目。git 管理外。「A2 の転記だけで本文の受け入れ境界が改訂されていない」と指摘し、下表の 10 点と X1 を求めた）。
- 本文の改訂: §1〜§5 を書き直した（A1 初稿は commit `82b0edc5`）。owner が下の「owner が決めること」5 点を採用し、6 件目の差分レビューの反映後の版（commit `7b7f243d`）を凍結した。
- owner が決めること（2026-10-17、owner が 5 点とも採用。下の 4・5 は凍結候補の差分レビューを受けて本文を確定形にした）:
  1. 再開条件 RC1〜RC3（§2）の採用。調査レポート（owner の手元層）§7.2・§7.5 から逐語に近い形で転記した。A2 は 3 件とも「条件を作らない」としたので、内容は変えていない。
  2. S3 の数値（導入と日常操作の表で 30 行以内・8 KB 以内）。
  3. README を縮める方針の変更: A1 初稿は README を約 100 行縮める案だったが、統合案は I3 を守るため既存節を本文不変で移すだけにし、圧縮は冒頭 2 段落に限った。規模はほぼ変わらず、読みやすさは冒頭の表と節の割当で得る。調査レポート T2 の「README の圧縮」はこの範囲でしか達成しない。
  4. 安全上の例外（§2 再開条件の末尾、下表 M4）。「案」を外した。
  5. 変更対象に `tools/Harness/README.md` を足すこと（下表 Q2。S7 の注記だけ）。S7 の成立に必要なので採否の選択ではなく採用として書いた。

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

- 不採用: なし。保留: なし。

#### 統合案への追加レビュー（5 件目）と再点検

- 入力: owner が依頼した A3 統合案のレビュー（5 件目。OpenAI の GPT 系。git 管理外）。判定は「B1・B2 を直し M1〜M3 の範囲を明確にすれば A3 凍結してよい。新しい調査・設計・実装スライスは不要」。owner が決めること 1〜3 は採用を推奨。
- あわせて、A2 1 件目の session が Phase C / C' で指摘されそうな矛盾を本文全体で再点検した（Q1〜Q7）。

| 指摘 | 採否 | 反映した節 | 確認方法 |
|---|---|---|---|
| B1 I3 (1)・AC4 (1) の本文一致と S7 の注記の衝突 | 採用 | §2 I3 (1)（S7 の注記の追加だけを例外にし、場所を §6 に列挙）、AC4 (1)、§4-2 表 | AC4 (1) |
| B2 I2 が diff の削除行にも実値 0 を求める。「user 名」が広すぎる | 採用 | §2 I2（追加行・改訂後の文書に限定、削除行は対象外で再転記しない、Windows のローカル user / profile 名に限定）、AC8 | AC8 |
| M1 AC8 の grep が一般の手順にも当たる。理由の無い単位の書き方 | 採用 | §1 現況、§2 S7（4 task に限定、「理由なし・未合流」）、AC8 の grep | AC8 |
| M2 §0 risk と §4 の「書込み 0」「R2 に送らない」が I1 より広い | 採用 | §0 risk、§4 対象外の維持 | — |
| M3 C' の独立性をリポジトリ規則（B・C と異なるモデル、新規 session、blind）どおりに | 採用 | §5 C' 用に予約した担当、強化条件 | — |
| M4 確認された安全契約違反は再開条件を待たない | 採用（owner、2026-10-17） | §2 再開条件の末尾 | — |
| Q1 AC5 が Editor Play と Player を混ぜている。Use For Next Play は次回以降の Editor Play に適用され、Player は `content:installedRevisionPath` と `content:manifestSha256` の pair で install 済み Content を使う（`20-variant-checkout-workflow.md` 冒頭・§3、`04-app-startup.md`）。Delivery window は Unity Editor の window | 採用 | §1 現況、§2 S4・AC5（経路を分け、不合格例を追加） | AC5 |
| Q2 `tools/Harness/README.md` も Codex 側にしか無い CURRENT（`h1-transport-identity`、`artifact-evidence-lifecycle`、`h2c-unity-gate-r2`）を入口として案内しているが、S7 の対象外だった | 採用（owner が決めること 5、2026-10-17） | §1 現況、§2 S7・AC7・AC8、§3、§4 | AC8 |
| Q3 「節の本文」の範囲が未定義（下位節を含むか） | 採用 | §2 I3 (1)（次の見出しの前までと定義） | AC4 (1) |
| Q4 新しい見出しが既存と同名だと GitHub の slug に `-1` が付き、既存の anchor がずれる | 採用 | §2 I3 (1)、AC4 (1) | AC4 (1) |
| Q5 program の −20 KB の圧縮に、残す内容の基準が無い（保存契約・不採用の判断を消しうる） | 採用 | §2 S2 | AC3 と C の差分確認 |
| Q6 C が別 worktree の新規 session だと手元層の manifest の場所が分からない。C' は manifest を読まないので S1 の要約を検証できない | 採用 | §5 検証経路 AC1（絶対 path は git 管理外の取得案内に置く。C' の監査できない範囲として記録）、§0 引き渡さないもの | — |
| Q7 §5 の C' の記述が「文書差分と §6 の要約だけ」と「C' の入力」の 2 か所で食い違う | 採用 | §5（C' 用に予約した担当から入力の記述を除き、C' の入力に一本化） | — |

#### 凍結候補の差分レビュー（6 件目）と owner の助言

- 入力: owner が依頼した凍結候補の差分レビュー（6 件目。xAI の Grok 4.7。git 管理外。先行を読んでおり独立していない）。判定は「4 文を直せば凍結してよい。新しい調査・実装スライスは不要」。owner が決める 5 点は、4 の「案」を外すこと、5 を採否でなく採用と書くことを条件に、本文のまま凍結できるとした。あわせて owner から GPT 系の助言（全単位が保留でも分類と理由を記録できれば失敗にしない。Phase A への差し戻しは本スライス自身の受け入れ条件を満たせない場合に限る）を受けた。

| 指摘 | 採否 | 反映した節 | 確認方法 |
|---|---|---|---|
| F1 AC4 (1) が空の見出し `保守とoffline検証` の削除を不合格にする | 採用 | AC4 (1) | AC4 (1) |
| F2 plan が `passed` でない時、S7 が書く単位の状態が無い | 採用 | §2 S7 | AC8 |
| F3 §4 の差し戻し文の主語が混ざっている | 採用（owner の助言と合わせて書き直し） | §4 plan の結果の扱い・差し戻す条件、§2 判定定義 | — |
| F4 AC4 (3) の slug の見本が無く、見出し文字列の完全一致と読み違える | 採用 | AC4 (3) | AC4 (3) |
| F5 Harness README の H2c 節の旧入口の文が `current -Task` の形でなく AC8 の grep に当たらない。Harness README の差分を注記に限る | 採用（旧 task の CURRENT を保持し旧入口は読取のみとする文を対象にした） | §2 S7、AC8 | AC8 |
| 助言 全単位保留は失敗にしない、差し戻しは自身の受け入れ条件の不成立に限る | 採用 | §2 判定定義、§4 | — |

## 6. Phase B 実装結果

- 担当: Phase B の新規 session（会話履歴なし、§0 の開始手順で開始）。implementation base `855e3da3`。
- 新しい事実の分類: なし（A 再開・スパイク・後続スライスに当たる事実は出なかった）。B 適応 1 件: docs-audit の検査2が「公開面（`tools/Artifacts/README.md`）が個別の HANDOFF に依存」を拒否したため、README 冒頭の保留 list への参照を program の path ではなく `docs/README.md` の作業台の表への link にした。

### S1: `store plan` の結果（AC1・AC2）

1. 実行結果: 2026-10-17、PowerShell tool（非仮想化 pwsh）で §4-1 のコマンドを 1 回。exit code `0`、`status: passed`、`reasonCode` なし。`--output` は git 管理外の手元層の新規 file、`--source-root` は Codex の package の `LocalCache\Local\OneStarMaker`。manifest と stdout の絶対 path は同じ手元層の取得案内 note にだけ置いた（I2）。`consolidate` と dry-run は実行していない。
2. 単位の集計: 単位 20（`publish` 4・`skip` 0・`hold` 16）。`excluded` 90（`unreferenced-transfer` 77、`not-owned` 8、`out-of-scope-role` 2、`credentials` 1、`revision-locks` 1、`unrecognized` 1）。

   | 単位 ID | 種別 | 状態 | 理由 code | 依存 |
   |---|---|---|---|---|
   | `43341dc7cf86b2d49633a1b4030bc2c9` | deployment | `hold` | `conflict` | — |
   | `3b9237f0c85358da125c6df29fa16289`（task `artifact-evidence-lifecycle`） | task | `hold` | `dependency-held`、`multi-workflow-unit`、`reference-localcache`、`reference-missing`、`reference-out-of-role` | `43341dc7…` |
   | `5e400c669d2532336cd6e7fd3067fb10`（task `h1-transport-identity`） | task | `hold` | `reference-missing` | — |
   | `bbe4eab0486f63dcc88185634be149a9`（task `h2c-unity-gate`） | task | `hold` | `reference-missing` | — |
   | `7cf9fb3454f8192b7a0806b203c4d28d`（task `h2c-unity-gate-r2`） | task | `publish` | なし | — |
   | `05cd0a989948e6a217df1f90b7f1375d`、`11077bd04dd56e79cf10e6962933b40c`、`4031ad57c1aa792fd5593de472fde455`、`7be8022b9abc84b491a324ae8d896ce2`、`8d74d8cd4160864e885847dd0b3b9451`、`b9c1920fc3fdaf2426f39dc46e11534e` | task | `hold` | `dependency-held`、`reference-localcache` | `43341dc7…` |
   | `079107c03e8dfe70af88c5d7ac4a4b1d`、`0e447f5f84884bed9a1733677abde1af`、`41b6094b42678a73be861ab9066438a2`、`513fb4aaf0e8f67b48b8b3f1877e065a`、`f62d5ffe64b8fce830406abc0ce12ef0` | task | `hold` | `dependency-held` | `43341dc7…` |
   | `68d4ae3009a01308df37b6d81e30d1a4` | task | `hold` | `conflict`、`dependency-held`、`reference-localcache`、`workflow-already-visible` | `43341dc7…` |
   | `a7c5231354da4ff582566dc45002cd84`、`ca81adc19c5660627f5544600fcf0f73`、`ee7ef561cb8c9cea7e6686a44fbe70e3` | task（Workflow のみ） | `publish` | なし | — |

   Harness 4 task はどれも単位に入り、`excluded` には無い。task ID と単位の対応は manifest の item の key（`harness` role と、`artifact-evidence-lifecycle` は `evidence`・`workflow` role も）で照合した。
3. 結論: 合流できる単位は `publish` の 4（Harness の `h2c-unity-gate-r2` と、Workflow だけの 3）。ただし `passed` は manifest を書けたことだけで、CURRENT が読めることや合流の完了を示さない。保留のまま設計判断が要る単位は `hold` の 16 で、うち 13 は deployment 単位（`conflict`）への `dependency-held` を持つ。Harness の 3 task（`artifact-evidence-lifecycle`、`h1-transport-identity`、`h2c-unity-gate`）は `reference-missing` を含む。plan 自体の失敗は無い。保留の解消と `consolidate` は program §6 の保留 list「分裂したストアの合流」へ送った（owner が manifest を承認するまで実行しない）。

### §4-2 文書改訂

- program（`docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md`）: §0 status を「完了（保留list付き）」へ。§4 を「完了したスライス」（A・B・C・E の完了記録と「決めたこと」を残す）にし、§4 の各「後続への入力」、旧 C の別 host / Cloud、旧 D、旧 E の後続を、新しい §6 保留 list（RC1〜RC3 と判断者 owner 付き、安全上の例外を含む）へ移した。§5 の「残件」と「文書改訂のPhase D」の文から実装キュー・進行中の現在形を除いた。§2・§3 と §5 の責務・残す安全性・採用・不採用は変えていない。§4 B のローカル作業 directory の実値を除いた。規模は 254 行・約 58 KB → 221 行・約 52 KB（§3 の見積り −20 KB に届かないのは、「決めたこと」と後続の入力の中身を保留 list に残したため）。
- README（`tools/Artifacts/README.md`）: §4-2 の表のとおり 4 節（日常操作・管理操作・契約と known limits・経緯）に再編。冒頭の導入と日常操作の表の部分は 22 行・3,108 bytes（S3 の 30 行・8 KB 以内）。新しい見出しは `日常操作`、`別PCで取得したPlayerとContentを使う手順`、`管理操作`、`契約と known limits`、`経緯` で、既存の見出しと同名でない。空の見出し `保守とoffline検証` は消した（参照元の anchor は無い）。
- `docs/README.md`: program 表の行を「完了（保留list付き）」にし、表の前の文を「program は次の4件。Artifact Storage は完了（保留list付き）で、他の3件は進行中。」にした。S7 の注記を 2 か所に足した。
- `tools/Harness/README.md`: S7 の注記 4 段落の追加だけ（削除行 0）。
- `review-evidence.md`「Phase D の保存・引渡し確認」: AC6 の 1 文を追加。

#### I3 (2): 冒頭 2 段落から削った文

第 1 段落の第 1 文（DPAPI・bucket）と第 3 文（Workflow の所有と 30 日のコピー清掃）は新しい導入にそのまま残した。第 2 段落は経緯節へ本文を変えずに移した（後ろに S7 の注記を追加）。削った文は第 1 段落の次の 6 文で、どれも「新 README の別の箇所に同じ内容がある」。

| 削った文 | 同じ内容がある箇所 |
|---|---|
| 通常Evidenceは明示した非秘密file集合をpublishし、別sessionで期待hashを照合してfetch・ログ/原画像を閲覧できます。 | 「Evidence v2 の通常操作」（publish・fetch の手順と期待 packageSha256 の照合）、導入の「完了の範囲」 |
| 既存Build 1件も明示した全file集合から保存・取得でき、BS4の配布形Player出力は `build collect` の1コマンドで退避・保存できます。 | 「開発Build 1件の保存と取得」「BS4 Player出力の退避と保存（build collect）」、日常操作の表 |
| R2上のBuildは `build cleanup`、Contentは `content cleanup` で系列ごとに件数清掃でき、使い終わった系列は `build retire`／`content retire` で退役させてR2から消し切れます。 | 「開発Buildの件数清掃」「開発Contentの件数清掃」「系列の退役」、日常操作の表 |
| 同一PCの担当間の固定入力は `evidence stage` でR2を使わずに渡せます（ローカル固定への link）。 | 「ローカル固定（evidence stage）」の冒頭、日常操作の表 |
| ローカル固定したartifactは `evidence promote` で同じreference・packageSha256のままR2へ昇格できます（R2への昇格への link）。 | 「R2への昇格（evidence promote）」、「ローカル固定」の「昇格」 |
| 別host/CloudがR2から取得する経路は後続スライスです。 | 導入の「完了に含めない」（別PC・CloudからのR2の直接取得）、「R2への昇格」の fetch の項（Cloud・別hostがR2から取得する経路は後続） |

#### S7 の注記の場所（I3 (1) の例外と AC8）

- `tools/Artifacts/README.md`: 「Evidenceの責務とoffline検証」の `current -Task artifact-evidence-lifecycle` を含む code block の直後（既存節で唯一の追加）。「経緯」の第 2 段落（旧冒頭）の直後。導入の「完了に含めない」は新規の文。
- `docs/README.md`: H2c の CURRENT 方式の文（`h2c-unity-gate` / `h2c-unity-gate-r2`）の直後の段落、program 表の行の中。
- program: §0 status の入口の文の直後、§1「現在の実装機能は…CURRENTの固定記録に残す」の直後、§4 A の「Git外CURRENTから取得する」の段落の直後。
- `tools/Harness/README.md`: `current -Task h1-transport-identity` の code block の直後、「Evidence lifecycle のtask限定gate」の入口の段落の直後、「H2c Unity pilot」の旧入口の文（旧taskのCURRENTを保持し旧入口は読取のみとする）を含む段落の直後、`current -Task h2c-unity-gate-r2` の段落の直後。
- 注記しなかった一般の手順: README「分裂したストアの合流」の「運用の前提」、`docs/README.md` の H1 の説明の文。

### §4-3 検査（B の自己確認。判定ではない）

- `pwsh tools/docs-audit.ps1`: `AUDIT_RESULT kind=docs files=111 errors=0 warnings=0 checks=1,2,3`（初回は上記の検査2で 1 件。直して 0 件）。
- `pwsh tools/contract-audit.ps1`: `AUDIT_RESULT kind=contract files=627 errors=0 warnings=0 checks=1,2,3,4,5,6,7,8 applicable8=False`、検査1 の差分 0 ファイル、「機械で判定できる契約に違反なし」。
- AC7: `git diff --name-only 855e3da3..<implementation head>` は `.agents/skills/osm-workflow/references/review-evidence.md`、`docs/README.md`、`docs/handoff/ARTIFACT_STORAGE_CLOSEOUT.md`、`docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md`、`tools/Artifacts/README.md`、`tools/Harness/README.md` の 6 file。`unity/` と `tools/` 配下のコード・script・設定の変更は 0。
- AC4 の自己照合（旧 README は `855e3da3`）: 既存節の本文の差分は「Evidenceの責務とoffline検証」への S7 注記の挿入だけ。消えた見出しは `保守とoffline検証`（本文は空行のみ）。参照元の anchor 15 件と README 内の anchor 20 件は全て新しい見出しの slug で解決。旧 README の reasonCode 形の語 92 種は全て新 README に残る。
- AC8 の自己確認: grep の当たる公開文書の全箇所に注記が隣接。追加行と改訂後の 5 文書にローカル path・Windows のローカル user / profile 名・SID・package 名・reference・packageSha256 の実値は 0 件。
- 未実行: Unity・テスト suite は対象外（§5）。判定 C の機械照合は C の担当が行う。

### 動作確認

- 対象 head: implementation head（§0）。条件: AC1 の `store plan`（§5 の検証経路で担当は Phase B）。
- 実行時の HEAD は implementation base `855e3da3`（§4-1 を文書改訂より先に行う順序のため）。base..head に `tools/` のコード・script の差分は無いので、実行したコードは head と同じ。
- 実行担当: Phase B の session（B の出口で実行してよい条件）。コマンドは §4-1 のとおり 1 回で、生の観測は上の S1 の 1（exit `0`、`status: passed`、`reasonCode` なし、`summary` units 20 / publish 4 / skip 0 / hold 16）。manifest は手元層にある。
- 検証経路の期待との差: なし。B の session が取った観測なので判定 C の判定入力にはしない（判定 C が同じ PC で manifest を読取専用で開いて照合する）。
- 状態: `なし`（未実施の条件は無い）。

## 7. Phase C

未実施

## 8. Phase C'

未実施

## 9. Phase D

未実施。harvest 先は §0。本 HANDOFF の削除時に、program 文書も完了宣言後の保留 list だけを README か docs/README へ移して削除してよいかを owner が決める。
