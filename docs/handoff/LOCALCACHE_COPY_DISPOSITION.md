# Artifact Storage: LocalCache 側 copy の扱い（合流の価値の判断と削除）

packaged app（Codex）が旧 root の `Artifacts` / `Workflow` / `Harness` として書き、書込み仮想化で LocalCache 側に入った記録（以下 **copy**）を、新 root へ合流させる価値があるかを判断し、価値が無い部分は合流せずに削除して終えるスライス。`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md`（以下 program）§6「LocalCache側copyの扱い（合流の価値の判断と削除）」を受け持つ。前提は program §4 F（store root の移設と実ストアの移行。PR #139・#140、2026-10-19 に完了）。

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A1 初稿。A2 は未着手。owner の決定 O1〜O8 は A3）
- branch: `claude/localcache-copy-disposition`（`develop` = `cbcdb22c` から切った。PR base は `develop`）
- implementation base commit: `cbcdb22cb7e47671ebd01927d1ac92fa7b9425c8`（PR #140 の merge 後の `develop`）
- implementation head commit: 未到達
- risk: `high`（copy の削除は不可逆。合流に進む場合は owner 承認の manifest による実ストアへの書込み。copy は資格情報 directory の複写を含む）
- owner: OSM maintainers（価値の判断、manifest の承認、削除の承認、A3 の凍結、Phase D の判断）
- created: 2026-10-19
- expires: 2026-12-19 または置換 revision
- harvest to: `tools/Artifacts/README.md`（「実行するprocessの条件（store rootと移行済みの印）」の末段、「分裂したストアの合流」）、`tools/Harness/README.md`（H1・H2c の固定記録の入口と「注記（合流前）」）、`docs/README.md`（program 表と「注記（合流前）」）、program §4 A・E・F の注記と §6 の本項目
- Phase A snapshot の取得案内・状態: Git 上の本文で完結（A0 = この文書を追加した commit。A2 入力版・凍結版は未到達）
- Phase B result snapshot の取得案内・状態: 未到達
- evidence bundle の取得案内・状態: 未到達
- C' blind bundle の取得案内・状態: 未到達
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A2（独立レビュー）。
  - 次の作業: A2 入力版を commit し、§5 の A2 の 3 件を、同じ入力版・互いの指摘を見せずに依頼する。結果を §5 に採用・不採用・保留で記録し、A3 で owner が O1〜O8 を決める。
  - 引き継ぐ commit SHA: A2 入力版の commit（未作成。A1 初稿は作業ツリーにある）。作業ツリーが dirty なら引き継がない。
  - 必要な権限: なし（A1 は文書だけ）。A1 で copy の追加観測が要る場合は、読取りだけを非仮想化の pwsh で行い、file の内容を開かない（§2「本文へ転記した実装制約」）。
  - 停止規則: A1 では copy・実ストア・旧 root・R2 を変更しない。`store plan` も実行しない（出力 manifest と probe directory を作るため。実行の要否は A1 が決め、A3 で固定する）。
  - 引き渡さないもの: copy・旧 root・新 root の実 path、package 名を含む path、資格情報。

### Phase A の着手手順

1. `AGENTS.md`、`.agents/skills/osm-workflow/SKILL.md`、同 `references/phases-and-handoff.md`・`references/handoff-template.md`・`references/architecture-gates.md`・`references/review-evidence.md`・`references/docs-policy.md`、`docs/README.md` を全文読む。次に本書を全文読む。
2. program の §2「開発Evidence」、§4 の A・E・F、§6 の本項目と「Cloud・別PCからの取得」「Contentの容量上限と孤児回収」を読む。`tools/Artifacts/README.md` の「エージェント操作の事前承認」「一回の旧開発環境reset」「分裂したストアの合流」「実行するprocessの条件（store rootと移行済みの印）」、`tools/Harness/README.md` の H1・H2c の節を読む。
3. branch `claude/localcache-copy-disposition` の HEAD を引継ぎ票と照合する。
4. A1 初稿を書く。commit は A2 入力版（1 回）と A3 凍結版（1 回）だけにし、途中の改稿を積まない。新規 A1 なので進める最低条件を不変条件と成立条件に分ける。高コスト証拠の分類と判定 C の照合 script の方式も A1 で書く。
5. A2 独立レビューを取り、指摘を §5 に採用・不採用・保留で記録する。risk `high` なので、A0 だけから代替構成を出すレビューを 1 件検討し、C' 用に未関与の系列を予約する。
6. A3 で owner と凍結し、`pwsh tools/docs-audit.ps1` と `pwsh tools/contract-audit.ps1` を通す。

## 1. 目的と対象外

- 目的: copy について、新 root へ合流させる価値の有無を owner が判断できる材料を揃え、その判断に従って処理を終える。価値が無い部分は合流せずに、対象・除外・開始条件を固定した一回の削除で消す。価値があると判断した単位だけを `store plan` の再実行 → owner の manifest 承認 → `consolidate` → 合流後の確認へ進める。終わった状態で、packaged app 内の process が copy のために `legacy-store-present` で止まらず、Storage の清掃が届かない記録が copy に残らない。
- 対象外:
  - 新 root・旧 root の resolver、3 CLI の gate、移行済みの印の形式と位置、旧 root 別名の prefix 置換（program §4 F の consumer）。変更が要るなら本スライスでは行わず owner へ返す。
  - 旧 root 別名の廃止期限（program §6「分裂したストア」の既存の入力のまま）。
  - Cloud・別 PC からの取得、旧 root の無い新しい host で印を発行する手段（program §6「Cloud・別PCからの取得」）。
  - 資格情報の新規登録・置換・rotation・失効、別 account / bucket、公開配布。copy 内の資格情報 directory の内容を読む・複写する・実ストアの鍵の置換に使うこと。
  - 合流に進む場合の、`hold` 単位の解消方式（公開途中の不可視性を保つ方式）の設計。必要になったら別の設計スライスに分ける（program §6）。
  - packaged app 自身（Codex）の設定・サンドボックス・package の管理。copy 以外の LocalCache の内容。
  - Unity 側コード、`ArtifactsRoot`、R2 の通常操作を超える bucket 操作。
- 現況（A0 で固定した観測。日付は 2026-10-19、別記のものを除く）:
  - **実ストア:** 移設済み。新 root（UserProfile 直下の `OneStarMaker`）に移行済みの印があり、旧 root（LocalApplicationData 直下の `OneStarMaker`）の所有 role 3 つは tombstone（size 0 の通常 file）。旧 root には ほかに `RevisionLocks`（directory）だけがある。本 session（Claude Code の PowerShell tool。非仮想化として扱う）の列挙で、旧 root の `Artifacts` / `Workflow` / `Harness` は `dir=False`、`RevisionLocks` は `dir=True`。
  - **copy の所在:** 本 session から `LocalApplicationData\Packages\*\LocalCache\Local\OneStarMaker` を列挙すると、copy があるのは Codex の package 1 つだけ（Claude の package には無い）。最上位の entry と、reparse point をたどらない再帰列挙の file 数・合計 size は次のとおり（内容は開いていない。更新時刻は各 directory 自身の LastWriteTime）:

    | entry | 種別 | file 数 | 合計 size | 更新時刻 |
    |---|---|---|---|---|
    | `Artifacts` | 所有 role | 4,999 | 約 1.15 GB | 2026-10-10 |
    | `Workflow` | 所有 role | 37 | 約 29 KB | 2026-10-08 |
    | `Harness` | 所有 role | 2,385 | 約 199 MB | 2026-09-29 |
    | `WorktreeRecords` | 非所有 | 5,220 | 約 213 MB | 2026-10-10 |
    | `H2a Review` / `H2b Review` | 非所有 | 223 / 825 | 約 51 MB / 約 98 MB | 2026-10-01 |
    | `RevisionLocks` | 非所有（Unity Runtime） | 1,346 | 0 | 2026-09-20 |
    | `.virtualization-probe-20261012-<hex>` | 非所有（過去のスパイク） | 0 | 0 | 2026-10-12 |

  - **copy の `Artifacts` の下:** `builds-v1`、`credentials`、`deployments`、`evidence-handoffs`、`evidence-ledgers`、`evidence-retention`、`evidence-v2`、`resets`、`review-evidence`、`runtime`、`transfers`。`evidence-v2` には repositoryId 1 つの `tasks` がある（最終更新 2026-10-12）。
  - **copy の `Workflow` の下:** repositoryId が 2 つ。本 repo の側に task 17 件（`artifact-build-publish-one`、`artifact-build-retention-policy`、`artifact-evidence-lifecycle`、`debugstudio-player-ui-route`、`debugstudio-property-write`、`evidence-c-<16 hex>` 9 件、`integration-pr94-completion-20261010`、`integration-pr95-repair-20261010`、`integration-pr97-docs-20261010`。更新 2026-10-08〜10-12）。もう一方は 64 桁が全て `a` の repositoryId で、`guard-bound-identity-mismatch`、`guard-cross-process`、`guard-identity` の 3 件（2026-10-08。テストの fixture が実ストアの位置へ書いたものに見えるが、未確認）。
  - **copy の `Harness` の下:** task 4 件（`h1-transport-identity` 2026-09-30、`h2c-unity-gate` 2026-10-04、`h2c-unity-gate-r2` 2026-10-04、`artifact-evidence-lifecycle` 2026-10-10）。実ストアの Harness は空（PR #140 の smoke で `harness.ps1 status` が `taskはありません`）。
  - **2026-10-17 の読取のみの `store plan`（1 回）:** 単位 20（`publish` 4・`skip` 0・`hold` 16）、`excluded` 90（`unreferenced-transfer` 77、`not-owned` 8、`out-of-scope-role` 2、`credentials` 1、`revision-locks` 1、`unrecognized` 1）。Harness の 4 task は `h2c-unity-gate-r2` が `publish`、`h1-transport-identity` と `h2c-unity-gate` が `hold`（`reference-missing`）、`artifact-evidence-lifecycle` が `hold`（`dependency-held`、`multi-workflow-unit`、`reference-localcache`、`reference-missing`、`reference-out-of-role`）。deployment 単位 1 件が `conflict` で保留され、Evidence を持つ task 単位の多くがそれへの `dependency-held`。`unrecognized` 1 件は `retirements-v1`。この manifest は `logicalRoot` が旧 root で `legacyRoots` を持たないので、現在の CLI では `manifest-invalid` になり、合流に進むなら再 plan が要る（program §4 F の移設スライスの記録）。`store consolidate` は dry-run も含めて一度も実行していない。
  - **公開文書の依存:** `tools/Harness/README.md` は H1 の入口として `harness.ps1 current -Task h1-transport-identity`、H2c の固定記録の入口として `harness.ps1 current -Task h2c-unity-gate-r2` を挙げ（`AGENTS.md` は H2c の固定記録の入口を同 README に委ねている）、`tools/Artifacts/README.md` はスライス A の入口として `harness.ps1 current -Task artifact-evidence-lifecycle` を挙げる。4 件とも copy 側にだけあり、非仮想化 process からは読めないことを「注記（合流前、2026-10-17の `store plan`）」として `docs/README.md` に 1 か所、`tools/Harness/README.md` に 4 か所、`tools/Artifacts/README.md` に 2 か所、program に 3 か所書いている。どちらに決めてもこれらの記述は書き換えが要る。
  - **Harness CURRENT の新 root での読み戻しは未実証:** PR #140 の smoke 項目 4 は owner 承認の縮退（空状態の `status`）で、`harness.ps1 current` が実際の記録を新 root で読めることは示していない。承認済み snapshot を Git から得られない見込みが高い（PR #140 の進行役が git 履歴の全 `.md` blob を 4 つの承認値と照合して一致 0 件）。
  - **packaged app 側:** copy の `Artifacts` / `Workflow` / `Harness` が見える間は、Codex 内の process は `legacy-store-present` で止まる（README「実行するprocessの条件」）。tombstone（実体側の通常 file）と copy の directory が同じ名前で重なったときの仮想化された view の見え方は未観測（PR #140 の smoke 項目 7 は未実施）。
  - **清掃が届かない:** Storage の 30 日清掃と定期清掃 job は実ストア（新 root）だけを見る。copy の Evidence・Build・transfers の記録は、どの清掃からも届かない。
  - **保存方針（program §2「開発Evidence」）:** 恒久的な設計判断・制約・検証要約は公開文書へ残し、生ログ・画像・bundle・派生 copy・witness に長期監査保存を課さない。削除後の参照は期限切れと分かればよく、payload の復元は保証しない。

## 2. 意思決定と受け入れ境界

A0 では入力と未決事項を固定した。下の A0 の小節は A1 で書き換えず、A1 初稿は「A1:」で始まる小節に書く。両者が食い違う箇所は A1 の小節に理由を書き、A3 で owner が決める。

### 入口条件（program §6 の本項目を正とする）

- 実ストアの移行の完了（PR #140）。2026-10-19 に成立。
- 再開: owner が 2026-10-19 に本スライスの Phase A を A0 から始めることを指示した。program §6 の本項目は旧 RC1・RC2 の再開条件を owner 決定で置き換えており、入口条件は上の 1 つだけ。
- 判断は owner: 価値の判断、manifest の承認、削除の承認。

### このスライスが答える問い（A0 案。A1 で一つの問いに確定する）

copy の各部分（Harness の 4 task の CURRENT、Workflow の task、Evidence とそれが指す R2 object、Build の記録、非所有の directory、資格情報の複写）について、新 root へ合流させる価値の有無を owner が判断できる材料を揃え、その判断に従って価値の無い部分を対象固定の一回の削除で消したとき、実ストアの記録と R2 の object の追跡を壊さずに、packaged app 内の process が copy のために `legacy-store-present` で止まらない状態に到達できるか。価値があると判断した単位がある場合は、その合流をこのスライスで行うか、別の問いに分けるかを U1 で決める。

### 価値の判断の材料（A0 で集めた論点。結論は owner が A1〜A3 で出す）

- **V1 Harness の 4 task の CURRENT:** 公開文書が「固定記録の入口」として名指ししている。合流しなければ、その入口は永久に読めないので、公開文書の入口を PR の記録（PR #93、#109 等）へ書き換えることになる。合流するなら、2026-10-17 時点で `publish` だったのは `h2c-unity-gate-r2` だけで、残り 3 件は `reference-missing` 等の `hold`。新 root での `harness.ps1 current` の読み戻しも未実証。program §2 の保存方針は、検証要約が公開文書と PR に残っていれば原記録の長期保存を求めない。
- **V2 Workflow の task:** 本 repo の 17 件が終了済みか（`end` の記録があるか）、まだ使う task があるかは未確認。全て `a` の repositoryId の 3 件はテスト由来に見えるが未確認。Workflow の task を捨てると、その task の Evidence の終了イベント（30 日の起算）も失われる。
- **V3 Evidence と R2 の追跡:** copy の `evidence-v2` の receipt・catalog が R2 へ publish した object を追跡しているなら、copy を消すとその object を消す主体がいなくなる（どの清掃も receipt の無い object を消さない）。copy を消す前に、copy の記録が指す R2 object を列挙して扱いを決めるか、孤児として program §6「Contentの容量上限と孤児回収」の入力へ送るかを決める。copy の Evidence のうち `staged`（R2 に無いもの）は copy にしか実体が無い。
- **V4 Build の記録:** copy の `builds-v1` は `store plan` の対象外（名前を列挙するだけ）。Codex が publish した Build の receipt がここにだけあるなら、V3 と同じく R2 object の追跡が失われる。
- **V5 非所有の directory:** `WorktreeRecords`、`H2a Review`、`H2b Review`、`RevisionLocks`、`.virtualization-probe-*`（合計約 360 MB）。tools の所有ではないので、`legacy-store-present` の判定（所有 role の 3 directory）には関わらない。削除の対象に含めるか、copy の親 directory ごと消すかは、所有者の確認と合わせて決める。
- **V6 資格情報の複写:** copy の `Artifacts\credentials` は実ストアの資格情報 store の複写の可能性がある。合流の対象外（`store plan` の `credentials` 除外）。消すと秘密の複写が減る一方、中身を開いて確かめることはしない。
- **V7 tombstone:** program §6 の本項目は削除の対象に「旧rootに残るtombstone」を挙げるが、`tools/Artifacts/README.md` は「旧版のCLIが旧rootにroleを作り直すのを防ぐ」ため「消さないでください」としている。両者は食い違う。
- **V8 判断の費用:** 合流に進む単位が 1 つでもあれば、再 plan、manifest 承認、dry-run、実行、合流後の確認、`hold` の解消（場合により設計スライス）と、移設スライスの C / C' が残した入力（reasonCode の allow-list、redirect host、仮想化以外の path 不一致の診断）が要る。削除だけなら、対象の固定と一回の実行と観測で済む。

### 進める最低条件の候補（A0 案。A1 で不変条件と成立条件に分け、観測可能な語に直す）

- 起きてはいけないことの候補:
  - 実ストア（新 root）の記録、旧 root の tombstone と `RevisionLocks`、copy 以外の LocalCache・package の内容を、削除・書換えする（V7 の決定より前に tombstone を消すことを含む）。
  - 対象を固定する前、または owner の削除の承認より前に、copy を消す。対象の列挙と実際に消したものが一致しないのに完了と記録する。
  - Codex 内の process が copy を使っている最中に削除する（静止を確かめずに消す）。
  - copy の記録が追跡する R2 object を、扱いを決めないまま追跡不能にする（V3・V4 の決定より前に消す）。
  - owner の承認の無い manifest で `consolidate`（dry-run を含む）を実行する。
  - 資格情報の複写の中身を読む・複写する・実ストアの鍵の置換に使う。鍵・header・署名 URL・実 path・package 名・SID を tracked 文書・PR・ログ・stdout へ出す。
  - 公開文書が名指しする入口（V1）を、読めないまま「固定記録の入口」と書き続ける。
- 起きなければならないことの候補:
  - copy の各部分について、owner の判断（合流 / 削除 / 残す）と理由が記録される。
  - 削除と決めた部分が、固定した対象の列挙どおりに消え、消えたことを非仮想化 process の列挙で確かめる。
  - 合流と決めた単位があれば、新 root の CLI（`workflow-task.ps1 status`、`evidence inspect`、`harness.ps1 current`）で読める。
  - packaged app 内の process で、3 CLI の resolver が copy のために `legacy-store-present` を返さない（Codex を実行担当に割り当てられた場合。V7 の決定で tombstone を残すなら、tombstone が見える view での結果を観測として記録する）。
  - 公開文書の「注記（合流前）」と入口の記述が、決定後の現況に合う。

### ここでは答えない問いと所有する後続（A0 案）

- R2 上の孤児 object（V3・V4 で追跡を失うと決めたもの）の回収: program §6「Contentの容量上限と孤児回収」。
- 合流に進む単位の `hold` の解消方式: 必要になったら別の設計スライス（program §6 の本項目）。
- 旧 root の tombstone と親 directory の最終的な後始末、旧 root 別名の廃止: program §6「分裂したストア」の既存の入力（V7 の決定で本スライスが持つことにした場合を除く）。
- 移設スライスの C / C' が残した入力のうち、合流に進まない場合に関わらないもの（`StoreConsolidation.psm1` の reasonCode の allow-list、redirect host の旧記録、印の読込みの全長確保、junction の case、probe directory の残置）: program §4 F の「後続の入力」のまま。
- packaged app を再び Phase 担当にする運用（サンドボックスの設定、app 固有の設定値）: 各自の git 管理外の運用メモ。

### 未決事項（A1 で答える）

- **U1 問いの分割:** 価値の判断、削除、合流（価値がある単位がある場合）を一つの最低条件に束ねるか。合流は再 plan と manifest 承認という別の owner 判断を待ち、`hold` の解消が要れば設計スライスを要するので、削除と独立に失敗しうる。価値の判断を A1〜A3 の中で owner が下し、凍結する問いを「削除」か「合流 + 削除」のどちらかにする進め方も候補。
- **U2 価値の判断に追加で要る観測:** V2 の task の終了状態、V3・V4 の copy が追跡する R2 object の有無と件数、`staged` の有無、全て `a` の repositoryId の由来。どれも copy の JSON を開く読取りになる。読取りだけの観測を A1 で行ってよいか（copy の内容を開く範囲と、出力に実 path・hash を出さない方法）、`store plan` の再実行で足りるか（現在の CLI で再 plan すると `legacyRoots` 付きの manifest になる）。
- **U3 削除の対象の単位:** 所有 role 3 つだけか、copy の親 directory `OneStarMaker` ごとか（V5）。非所有の directory の所有者の確認方法。
- **U4 削除の実行担当と静止:** 非仮想化 process（本 repo の CLI を通さない一回の操作か、一回限りの script か）。Codex を止めて copy への新規書込みが無いことを、どう確かめるか。削除中に Codex が起動して copy を作り直した場合の扱い。
- **U5 削除の方法と途中失敗:** 再帰削除の途中失敗（使用中の file、ACL、長い path、reparse point）での停止と再実行。reparse point をたどらないこと。資格情報の複写を開かずに消すこと。消した対象の記録（件数・size・名前の一覧）の置き場。
- **U6 tombstone（V7）:** 残すか消すか。残すなら、packaged app の view で tombstone（通常 file）が見えるときに resolver が通るか（README 上は同名の通常 file を不在と扱う）。消すなら、旧版の CLI が旧 root に role を作り直すことをどう防ぐか。
- **U7 検証経路:** packaged app 内での resolver の結果は、Codex を実行担当に割り当てないと取れない。owner が Codex の session を割り当てるか、`未実施` で可とするか。AI が取得できない証拠を暗黙の人間作業として凍結しない。
- **U8 公開文書の書換えの範囲:** V1 で削除に決めた場合、公開文書の入口（`harness.ps1 current -Task …`）を何に置き換えるか（PR の記録、README の要約）。`AGENTS.md` は H2c の固定記録の入口を `tools/Harness/README.md` に委ねるので、`AGENTS.md` 自体の変更は要らない見込みだが確認する。Harness の `ApprovedSpecifications.psm1` の承認 entry（4 task）と、それを使うテスト・`LocalChecks.psm1` の分岐は、CURRENT を消しても残すか（参照 0 を削除理由にしない契約）。
- **U9 高コスト証拠への分類:** copy の削除（と合流に進む場合の `consolidate`）は不可逆な実操作。`review-evidence.md` の「高コスト証拠と最終証拠ラウンド」に当てはめるか、実装が無い（文書と一回の操作だけの）スライスとしてどう Phase B〜C' を組むか。実装から独立した期待値（固定した対象の列挙との一致）と照合 script の方式。
- **U10 事前承認の範囲:** copy の削除は `tools/Artifacts/README.md`「エージェント操作の事前承認」の通常操作に含まれない（program §6 の本項目。「一回の旧開発環境reset」と同じ種類）。A3 で固定する対象・除外・開始条件と、owner の承認の取り方。

### 本文へ転記した実装制約

- 依存は Game → Framework の一方向。asmdef 参照を勝手に追加しない。本スライスは Unity 側を変えない見込み。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- path は `tools/path-budget.json` の予算に従う。
- 参照 0 を削除理由にしない（Harness の承認 entry、Evidence の旧 directory 名等）。
- Artifact Storage の通常操作は README の事前承認に従い、それ以外（copy の削除、合流）は A3 で固定した範囲と owner の承認でだけ行う。秘密を露出しない。利用中の保護と対象外の非削除を維持する。実行環境の承認・拒否を迂回しない。
- LocalAppData の下の状態を作る・消す・確かめる操作は、Claude Code の Bash（Git Bash の coreutils）ではなく pwsh で行う（Bash の書込みは Claude の package の LocalCache へ仮想化され、Bash の列挙は実体の確認にならない）。`store plan` / `consolidate` は非仮想化 process を要求する。
- consumer（resolver、3 CLI の gate、印の形式、旧 root 別名の prefix 置換）、資格情報の保存方式（DPAPI CurrentUser）、private ACL 規則は変えない。
- 出力・tracked 文書・PR に実 path・package 名を含む path・SID・reference と packageSha256 の実値を書かない。
- PR の base は `develop`。

### A1: 用語と推奨の前提

- 用語: **copy** = packaged app（Codex）の package の `LocalCache\Local\OneStarMaker` directory とその中身全体。**copy root** = その directory。**最上位 entry** = copy root の直下の entry（A0 の表の 8 つ）。**新 root** = UserProfile 直下の `OneStarMaker`。**旧 root** = LocalApplicationData 直下の `OneStarMaker`（tombstone 3 つと `RevisionLocks`）。**(P)** = 本スライスで作る一回限りの program（§3）。**inventory** = (P) が copy を読取りだけで数えた記録。**承認 manifest** = owner が sha256 を指定して承認した inventory。
- A1 の推奨は「**合流しない。copy root を丸ごと、owner が承認した最上位 entry だけ削除する。R2 の object は消さず、copy が追跡していた key の一覧を残して後続へ送る**」。理由は O1〜O3 に書く。価値の判断は owner のもので、A3 で O1〜O8 として決める。推奨と違う決定になった場合の扱いも各項目に書く。

### A1: このスライスが答える問い（確定案）

owner が O1 で「合流しない」と決めた前提で、(P) を SHA B に固定し、copy の inventory → owner の承認 → 承認した最上位 entry の削除を一回行ったとき、承認外のもの（新 root、旧 root、copy の除外 entry、他の package）と R2 を変えずに、copy の所有 role 3 つの directory が LocalCache から無くなった状態（packaged app 内の resolver が copy を理由に `legacy-store-present` を返す条件が消えた状態）に到達できるか。

O1 で合流する単位を owner が選んだ場合は、この問いを使わない。合流は別スライス（「LocalCache側copyの合流」）に分け、本スライスはその完了まで削除を行わない（U1 の回答）。

### A1: 進める最低条件

- 不変条件（起きてはいけないこと）:
  - **I1 承認外を変えない:** (P) の実行の前後で、新 root（印の bytes、直下の entry の名前と種別）、旧 root（tombstone 3 つの size 0 の通常 file と `RevisionLocks` の存在）、copy root の兄弟（package の `LocalCache\Local` の他の entry）、承認 manifest で除外した最上位 entry の中身（名前・種別・size・更新時刻）が変わらない。
  - **I2 承認の前に消さない:** 承認 manifest の sha256 と一致する manifest を受け取らない限り、(P) は file を 1 つも消さない。inventory は読取りだけで、copy・新 root・旧 root に何も作らない（出力は copy の外の、明示した出力 path にだけ書く）。
  - **I3 変わった copy を消さない:** 初回の削除の前に、承認した最上位 entry の配下の実際の entry（相対 path、種別、size、更新時刻）が承認 manifest と 1 件でも違えば、何も消さずに `copy-changed` で止まる。再実行では、残っている entry が承認 manifest の部分集合で属性が一致する場合だけ続ける。reparse point を見つけたら、たどらず、消さずに `reparse-present` で止まる。
  - **I4 仮想化された process で動かない:** `Get-OsmStoreContext` が `not-virtualized` でなければ、inventory も削除も始めない（packaged app の view では copy と実体が重なるため）。
  - **I5 資格情報の複写と他の内容を開かない:** (P) が内容を開くのは、§4 の「R2 key を読む record」に列挙した JSON file だけ。copy の `Artifacts\credentials` の配下は名前・種別・size・更新時刻だけを見て、開かない。
  - **I6 秘密と実 path を出さない:** stdout は固定 code と件数だけ。inventory と結果の file は copy root からの相対 path だけを持ち、copy root 自体は正規化した path の sha256（`sourceRootSha256`）で表す。package 名、SID、endpoint、資格情報を書かない。
  - **I7 R2 と資格情報に触れない:** (P) は network・R2・資格情報 store を使わない。
- 成立条件（起きなければならないこと）:
  - **S1 inventory:** 実 copy に対して (P) の inventory が `passed` を返し、最上位 entry ごとの file 数・合計 size・reparse point の有無、R2 key の一覧（record の種類、key、bytes、bucket 名）と読めなかった record の件数、Workflow の task ごとの終了記録の有無を出す。
  - **S2 owner の承認:** owner が inventory の要約を見て、削除する最上位 entry と承認 manifest の sha256 を 1 回の指示で明示する（O7）。
  - **S3 削除:** 承認 manifest で (P) の削除が `passed` を返し、承認した最上位 entry が、(P) と独立した非仮想化 pwsh の列挙で存在しない。削除した file 数と合計 size が承認 manifest の値と一致する。承認した最上位 entry が全てなら copy root も無い。
  - **S4 承認外の不変の観測:** I1 の対象を、削除の直前と直後に (P) と独立した非仮想化 pwsh の列挙で記録し、一致する。
  - **S5 R2 key の一覧の固定:** S1 の R2 key の一覧を含む inventory を本スライスの task で `evidence stage` し、別 process の `fetch` で読める。program §6「Contentの容量上限と孤児回収」への入力（件数、合計 bytes、取得案内の所在。key の実値は tracked 文書に書かない）は Phase D の harvest で書く。

### A1: 受け入れ条件

offline（temp base に Packages の形を作った fixture。§5）:

- **AC-1（S1・I2・I5）:** fixture の copy で inventory が `passed`。最上位 entry ごとの file 数と size が fixture の期待値と一致し、Evidence と Build の receipt から key と bytes が出る。copy・新 root・旧 root の列挙が実行前後で一致する。`credentials` の配下の file に読取り拒否の ACL を付けても inventory は `passed`（開いていないことの確認）。
- **AC-2（I2、失敗経路）:** sha256 が一致しない manifest、改変した manifest、schema の違う manifest では、削除が `manifest-hash-mismatch` / `manifest-invalid` で止まり、fixture の file 数が変わらない。
- **AC-3（I3、失敗経路）:** inventory の後に、承認した entry の配下へ file を 1 つ足す / 1 つ消す / size を変える / 更新時刻を変える。どれも初回の削除が `copy-changed` で止まり、1 つも消さない。
- **AC-4（I3、失敗経路）:** 承認した entry の配下に junction を置く。inventory は `reparse-present` を出し、削除は止まり、junction の先の file が残る。
- **AC-5（I1）:** 最上位 entry のうち 1 つを除外した manifest で削除すると、除外した entry、copy root の兄弟、新 root、旧 root の tombstone が前後で一致する。
- **AC-6（S3、再実行）:** 削除の途中（N 件目）で失敗を注入すると `partial` で止まり、同じ manifest の再実行で `passed` に収束する。2 回目以降の再実行は消すものが無く `passed`（件数 0）。使用中の file（排他で開いた handle）の削除失敗は `in-use` で止まり、handle を閉じた後の再実行で収束する。
- **AC-7（I4）:** context の seam を `virtualized` / `unknown` にすると、inventory と削除が `virtualized-store` / `store-context-unknown` で止まり、何も読まず消さない。
- **AC-8（I6）:** 全 case の stdout・stderr・出力 file に、fixture の copy root の絶対 path、fixture の package 名、SID が現れない。
- **AC-9（S1、読めない record）:** 壊れた JSON の receipt、key の無い receipt は R2 key として推測せず、`unparsedRecords` の件数に入る。
- **AC-10（I7）:** (P) の module が R2 transport・資格情報の module を import しないことを静的に確かめる（script の検査）。

実操作（高コスト証拠。§5）:

- **AC-11（S1〜S5）:** 実 copy で §4 の手順 H1〜H7 を一回行い、期待値（§5「高コスト証拠」）と照合して一致する。

### A1: ここでは答えない問いと所有する後続

- copy の合流（O1 で合流する単位を選んだ場合）: 新しい slice「LocalCache側copyの合流」（program §6 の本項目から分ける）。本スライスはその完了まで削除しない。
- R2 上の孤児 object の回収（S5 で引き渡す key の一覧を含む）: program §6「Contentの容量上限と孤児回収」。
- 旧 root の tombstone と親 directory の後始末、旧 root 別名の廃止: program §6「分裂したストア」の既存の入力（O4 で tombstone を残すため）。
- packaged app 内の process での実際の結果（印の読取り、サンドボックスの条件を含む）: O5 で owner が Codex の session を割り当てた場合だけ本スライスで観測として記録する。割り当てない場合は、Codex を次に Phase 担当にするときの最初の作業で確かめる（各自の運用メモ）。
- 移設スライスの C / C' が残した入力（reasonCode の allow-list 等）: program §4 F の「後続の入力」のまま。

### A1: 判定定義と停止規則

- GO: I1〜I7 に違反が無く、S1〜S5 が成立する（S4 は AC-11 の期待値と一致）。
- NO-GO: 上記のどれかが不成立。削除の途中で止まった（`partial`）まま再実行で収束しない場合も NO-GO で、owner に返す。
- 停止規則: 上記の GO で終了する。最低条件が未達のまま終了しない。スパイクではないので CONDITIONAL ACCEPT は使わない。H1〜H7 で想定外の結果（`copy-changed`、`reparse-present`、`in-use` の継続、S4 の不一致）が出たら、原因の調査や再実行で埋めず、観測を残して owner に返す。

### A1: owner に決めてもらう事項（A3 で O1〜O8 として凍結する）

- **O1 価値の判断（推奨: 全て合流しない）:** 理由: (a) 開発用の記録で、program §2 の方針は原記録の長期保存を求めない。Harness の 4 task の検証要約は PR（H2c は PR #93、スライス A は PR #109）と公開文書に残っている。(b) 4 task のうち 2026-10-17 に `publish` だったのは `h2c-unity-gate-r2` だけで、残りは `hold`。新 root での `harness.ps1 current` の読み戻しも未実証で、合流は再 plan・承認・実行・確認の一連と、移設スライスが残した入力の解消を要する（V8）。(c) Workflow の task は Codex の作業の記録で、終わった作業は PR に残り、続ける作業（例 `debugstudio-property-write`）は新しい task で再開できる。合流しない場合の代償: 公開文書の「固定記録の入口」（V1）が読めなくなるので、Phase D の harvest で PR の記録へ書き換える（O6）。copy の Evidence の終了イベントと 30 日清掃も無くなる（R2 の object は O3）。合流する単位を選ぶ場合: 上の「ここでは答えない問い」のとおり別スライスに分ける。
- **O2 削除の範囲（推奨: copy root を丸ごと。最上位 entry ごとに承認）:** 所有 role 3 つだけでも `legacy-store-present` の条件は消えるが、非所有の `WorktreeRecords`・`H2a Review`・`H2b Review`・`RevisionLocks`・`.virtualization-probe-*` も、仮想化のために copy 側にだけできた directory で、実ストアに対になる記録は無く、tools は読まない（`store plan` は名前を列挙するだけ）。owner は inventory を見て、最上位 entry ごとに除外できる（I1・AC-5）。
- **O3 copy が追跡する R2 object（推奨: 消さない。key の一覧を残す）:** copy の receipt が追跡する R2 object は、copy を消すとどの清掃にも消されない。消すには copy の receipt を読んで DELETE する新しい経路が要り（`evidence reset` は既知の key に固定された実装で使えない）、本スライスの規模と risk を上げる。開発用の private bucket の容量は孤児回収の後続が扱う。key の一覧（S5）があれば後続で回収できる。owner が「消す」を選ぶ場合は、DELETE の経路を後続スライスで作るまで削除を待つか、一覧だけ残して先に削除するかを決める。
- **O4 tombstone（推奨: 残す）:** README の「消さないでください」を正とし、program §6 の本項目の「旧rootに残るtombstone」を削除の対象から外す。理由: tombstone は旧版の CLI が旧 root に role を作り直すのを防ぐもので、copy の削除と独立している。resolver は同名の通常 file を不在と扱うので、copy を消した後の packaged app の view でも `legacy-store-present` の条件にならない。Phase D で program §6 の記述を直す。
- **O5 packaged app 内の観測（推奨: 任意）:** owner が Codex の session を割り当てた場合だけ、削除の後に Codex 内で `pwsh tools/workflow-task.ps1 status` を 1 回実行し、reasonCode を観測として記録する（`legacy-store-present` が出たら欠陥ではなく観測として owner に返す）。GO の条件にしない。理由: copy が原因の条件（LocalCache の所有 role の directory）は S3 の非仮想化の列挙で判定でき、Codex の実行条件（サンドボックス等）は本スライスの問いの外。
- **O6 公開文書の書換え（推奨: Phase D の harvest で行う）:** 削除の後に、`docs/README.md`、`tools/Harness/README.md`、`tools/Artifacts/README.md`、program の「注記（合流前）」計 10 か所と、`harness.ps1 current -Task <4 task>` を「固定記録の入口」とする記述を、「copy は YYYY-MM-DD に合流せずに削除した。検証の要約は PR #… にある」に直す。`ApprovedSpecifications.psm1` の承認 entry とそれを使うテスト・分岐は残す（参照 0 を削除理由にしない契約。CURRENT が無いと `current` は既存どおり「CURRENTが見つかりません」で止まる）。H1 の手順例（`init` / `run` 等の例）は、別 task で始めるときの書き方として残すかを harvest で決める。
- **O7 承認の取り方（推奨: inventory の後に 1 回）:** copy の削除は事前承認の通常操作に含めない（U10）。owner は H3 の要約（最上位 entry ごとの件数・size、R2 key の件数と合計 bytes、読めない record の件数、終了記録の無い task の名前）を見て、「manifest sha256 = <64 桁>、削除する最上位 entry = <名前の列>」を 1 回の指示で出す。これを削除の承認とし、H4〜H7 の実行に追加の承認を求めない。指示の原文は Evidence に入れる。
- **O8 (P) の寿命（推奨: merge 前に削除）:** PR #140 の移行 program と同じく一回限りなので、Phase D で (P) と suite を削除し、merge する差分に残さない。経緯は PR と削除前の commit に残す。

### A1: 未決事項 U1〜U10 への回答

- **U1:** 価値の判断は A3 で owner が O1 として下す。凍結する問いは「削除」だけにし、合流を選んだ単位があれば別スライスに分けて、その完了まで本スライスの削除を待つ。
- **U2:** A1 では copy の内容を開かない。価値の判断に要る追加の観測（R2 key、終了記録、全て `a` の repositoryId の中身）は、(P) の inventory（S1）として SHA B の版で一回だけ取る。`store plan` は再実行しない（合流しないので manifest は不要）。
- **U3:** O2 のとおり、最上位 entry ごとに承認する。
- **U4:** 実行担当は非仮想化の pwsh（Claude Code の PowerShell tool で `pwsh -NoProfile -File`）。静止は owner が H1 で行う: Codex のアプリを終了し、Codex 内の作業 session が無いことを確かめる。(P) は process を照会しない。削除中に Codex が copy に書けば、I3 の照合で `copy-changed` か `partial` になる。
- **U5:** §4 の削除の順序と、AC-3・AC-4・AC-6 のとおり。長い path は `\\?\` 形式で扱う（`tools/path-budget.json` の予算は repo 相対 path のためで、copy の中の path には適用されない）。消した対象の記録は (P) の結果 file（相対 path・件数・size。copy の外の出力 path）で、本スライスの Evidence に入れる。
- **U6:** O4 のとおり残す。
- **U7:** O5 のとおり任意。AI が取得できない証拠を GO の条件にしない。
- **U8:** O6 のとおり。`AGENTS.md` は H2c の固定記録の入口を `tools/Harness/README.md` に委ねるだけなので変えない見込み。harvest で文言を確かめる。
- **U9:** H2〜H7 を高コスト証拠に分類する（§5）。inventory は読取りだけだが、実 copy に対する承認の入力なので削除と同じ版・同じラウンドで取る。
- **U10:** O7 のとおり。program §6 の本項目の「ownerの承認で1回行う（通常操作の事前承認に含めない）」をそのまま使う。

## 3. 責務マップ

A1 初稿。全て新規で、O8 の推奨では Phase D で削除する一回限りの file。既存の file は変えない。

| file | 責務（一文） | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 予想行数 |
|---|---|---|---|---|---|---|---|
| `tools/Workflow/LocalCacheCopyDisposal.psm1` | 1 つの copy root を検証し、読取りだけの inventory を作り、承認 manifest と一致する範囲だけを削除する | copy の処理の規則（何を開くか、何を消すか、いつ止まるか） | 本スライス。一回の実行まで（O8） | `WindowsStoreContext.psm1`（`Get-OsmStoreContext`）、`WindowsStoreRoot.psm1`（新 root・旧 root の導出を重なりの拒否にだけ使う）。R2 transport・資格情報の module は import しない（AC-10） | `Invoke-OsmLocalCacheCopyInventory`、`Invoke-OsmLocalCacheCopyDelete` の 2 関数。module scope の seam（LocalApplicationData の base、context、削除の失敗注入） | temp base の fixture で全経路を offline に検証（§5）。Unity 不要 | 約 280 |
| `tools/Workflow/LocalCacheCopyDisposal.ps1` | 引数を検証して 2 関数のどちらかを呼び、固定 code と件数だけを stdout に出す入口 | 入口の引数 | 同上 | 上の module だけ | `inventory -SourceRoot <p> -Output <p>`、`delete -SourceRoot <p> -Manifest <p> -ManifestSha256 <64hex> -Output <p>` | suite から子 process で呼ぶ | 約 30 |
| `tools/Workflow/tests/LocalCacheCopyDisposal.Tests.ps1` | AC-1〜AC-10 | 受け入れ条件 | 同上 | 上の 2 file、既存の test support の書き方（`-Case` / `-ResultPath`） | なし | — | 約 320 |

- 配置の理由: store root の判定（`WindowsStoreRoot.psm1`）と context の判定（`WindowsStoreContext.psm1`）の隣で、PR #140 の移行 program と同じ置き方。`tools/Artifacts` に置かないのは、copy が Artifacts・Workflow・Harness の 3 role と非所有の directory にまたがり、Artifacts の application 層（publish・清掃）と寿命も変更理由も違うため。
- 分割の判断: 行数警報は発火しない。inventory・manifest の検証・削除は同じ規則（相対 path の集合と属性の一致）を共有し、同じ寿命（一回の実行で削除）で、同じ suite で検証するので、1 module に置く（非分割）。
- 新しい公開 API・状態・依存 edge: 上の 2 関数だけ。永続する状態は持たない（出力 file は copy の外の明示した path に書き、Evidence として stage する）。

## 4. 実装計画

### (P) の振る舞い

- **copy root の検証（両 mode 共通）:** `-SourceRoot` は絶対 path で、`<LocalApplicationData の Known Folder>\Packages\<1 segment>\LocalCache\Local\OneStarMaker` の形に一致する（大文字小文字は区別しない）。copy root とその祖先（LocalApplicationData まで）に reparse point が無い。新 root・旧 root と重ならない。`Get-OsmStoreContext` が `not-virtualized`（I4）。満たさなければ `invalid-source-root` / `virtualized-store` / `store-context-unknown` で止まる。
- **inventory:** copy root の下を、reparse point をたどらずに列挙する（reparse point は entry として記録し、その下を列挙しない）。各 entry の相対 path、種別（file / directory / reparse）、size、更新時刻（UTC、100ns 単位）を記録する。最上位 entry ごとに file 数・合計 size・reparse の有無をまとめる。内容を開くのは次の「R2 key を読む record」と「Workflow の終了記録」だけ（I5）。
  - **R2 key を読む record:** `Artifacts\evidence-v2` と `Artifacts\builds-v1` の配下の `.json` で 1 MiB 以下のもの。top-level の object が文字列の `key` を持てば、record の種類（`evidence` / `build`）、`key`、`packageBytes`（無ければ `bytes`、どちらも無ければ null）、`bucket`（あれば）を一覧に入れる。同じ key は 1 件にまとめる。`key` が無い JSON は `nonKeyRecords`、JSON として読めないものは `unparsedRecords` に数える（AC-9）。`Artifacts` の他の directory（`review-evidence`、`evidence-ledgers` 等の旧形式、`credentials`、`runtime`、`transfers`）は開かない。
  - **Workflow の終了記録:** `Workflow\<repositoryId>\tasks\<taskId>\state.json`（1 MiB 以下）の top-level の `status` だけを読み、task ごとに `ended` / `active` / `unknown` を出す。
  - 出力: `-Output`（copy の外。既存なら上書きせず `invalid-output`）へ JSON（`schemaVersion` 1、`kind` `osm-localcache-copy-inventory`、`sourceRootSha256`、`createdAt`、`entries`、`topLevel`、`r2Keys`、`nonKeyRecords`、`unparsedRecords`、`workflowTasks`）を一時名で書いて rename する。stdout は `{"status":"passed","entries":N,"topLevel":M,"r2Keys":K}`。
- **承認 manifest:** inventory の JSON に、owner の指示に従って `approvedTopLevel`（削除する最上位 entry の名前の配列）を加えた file。field の追加はこの 1 つだけで、他は inventory のまま。sha256 は file の bytes の SHA-256 で、owner の指示（O7）に書かれた値を `-ManifestSha256` に渡す。
- **delete:** (1) copy root を検証する。(2) manifest の sha256・schema・`sourceRootSha256` を確かめる（AC-2）。(3) 承認した最上位 entry の配下を列挙し、manifest の entry と比べる。初回（承認した entry が manifest のとおり全て在る）は完全一致を、再実行（一部が無い）は「残りが manifest の部分集合で属性が一致」を要求する。reparse point があれば止まる（I3、AC-3・AC-4）。(4) 承認した最上位 entry ごとに、深い順に file を消してから directory を消す（読取り専用属性は外してから消す。`\\?\` 形式の path を使う）。除外した entry と copy root の外には触れない。(5) 承認した最上位 entry が全て無くなり、copy root が空なら copy root を消す。(6) 結果 file（相対 path ごとの `deleted` / `absent-before`、件数、合計 size）を `-Output` に書く。失敗は `partial`（exit 2）で、結果 file に途中までを書く。同じ manifest の再実行で収束する（AC-6）。
- **reasonCode:** `invalid-source-root`、`virtualized-store`、`store-context-unknown`、`invalid-output`、`manifest-hash-mismatch`、`manifest-invalid`、`copy-changed`、`reparse-present`、`in-use`、`partial`、`disposal-failed`（それ以外の例外の畳み込み）。stdout に例外の message・path を出さない（I6）。

### 順序

1. Phase B: §3 の 3 file を実装し、§5 の suite を通す。実 copy に対して (P) を実行しない。
2. Phase C（発見 C → 判定 C）→ 暫定 C'。判定 C の照合 script は判定 C の担当が書く（§5）。
3. 高コスト証拠の取得（下の H1〜H7。暫定 C' の阻害指摘が無くなった head = SHA B で一回）。
4. 最終証拠ラウンド（判定 C → 最終 C'）。
5. Phase D: Evidence の保存・引渡しの確認、O6 の公開文書の書換え（harvest）、program §6 の更新（本項目の完了、O4 の tombstone の記述、孤児回収の項目への S5 の入力）、O8 の削除、本書の削除。

### 高コスト証拠の手順（H1〜H7）

| 手順 | 担当 | 内容 | 止まる条件 |
|---|---|---|---|
| H1 静止 | owner | Codex のアプリを終了し、Codex 内の作業 session が無いことを確かめ、「Codex を終了した」と伝える | 確かめられなければ H2 へ進まない |
| H2 前の観測 | 高コスト証拠の実行担当（非仮想化 pwsh） | §5 の観測 script で I1 の対象を列挙し、`before-1.json` に書く | script が失敗すれば止まる |
| H3 inventory | 同上 | SHA B の (P) で `inventory`。要約（最上位 entry ごとの件数・size、R2 key の件数と合計 bytes、`nonKeyRecords`・`unparsedRecords` の件数、`active` / `unknown` の task 名）を owner へ渡す | `passed` 以外なら止めて owner に返す |
| H4 承認 | owner | O7 の形で manifest sha256 と削除する最上位 entry を指示する。実行担当は inventory に `approvedTopLevel` を加えた manifest を作り、sha256 が指示と一致することを確かめる | 不一致なら止まる |
| H5 直前の観測 | 実行担当 | 観測 script で `before-2.json` を書く。`before-1.json` と一致することを確かめる | 不一致なら止めて owner に返す（H2 から後に何かが変わった） |
| H6 削除 | 実行担当 | SHA B の (P) で `delete`（承認 manifest と sha256）。`partial` なら同じ引数で 1 回だけ再実行する | 再実行でも `passed` にならなければ止めて owner に返す |
| H7 後の観測と保存 | 実行担当 | 観測 script で `after.json` を書く。H2〜H7 の生ログ、inventory、承認 manifest、owner の指示の原文、結果 file、3 つの観測 file を本スライスの task で `evidence stage` する（通常操作）。R2 key の一覧は inventory に含まれる（S5） | stage の失敗は既存の回復手順に従う |

H1〜H7 の間は、store を使う CLI（3 CLI）を H7 の `evidence stage` 以外で使わない。

### Phase B から Phase A へ差し戻す条件

- §3 の 3 file 以外（既存の module、README）を変えないと成立しない。
- I5 の「開く record」の範囲を広げないと S1 の出力を作れない。
- module scope の seam では AC-6・AC-7 の失敗注入ができない。
- reasonCode・manifest の field・入口の引数を変える必要が出た。

### 対象外を維持する方法

- (P) は R2 transport・資格情報の module を import しない（AC-10）。consumer の module は import するだけで変えない。`git diff --name-status cbcdb22c HEAD -- tools/ unity/` が §3 の 3 file だけであることを B の出口と判定 C で確かめる。

## 5. テストとレビュー計画

- 単体テスト: `tools/Workflow/tests/LocalCacheCopyDisposal.Tests.ps1`（AC-1〜AC-10）。temp base に `<base>\LocalAppData\Packages\<fixture package>\LocalCache\Local\OneStarMaker` の形の copy、兄弟 entry、新 root（印つき）、旧 root（tombstone）を作る。LocalApplicationData の base と context は module scope の seam で差し替える。`Task.Delay` / `Thread.Sleep` を使わない（使用中の file は排他 handle を開いたまま実行し、閉じてから再実行する）。
- 差し戻し中の起点: `pwsh tools/Workflow/tests/LocalCacheCopyDisposal.Tests.ps1 -Case '*' -ResultPath <temp>`（C が根拠付きで変更可）。
- 判定必須テスト（GO 候補 head）: 上の suite 全件、既存の `pwsh tools/Workflow/tests/StoreRoot.Tests.ps1 -Case '*' -ResultPath <temp>` と `StoreContext.Tests.ps1`（import する module の回帰）、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。どれも executed 1 件以上・failed 0。
- 全 EditMode 回帰の適用除外: 適用しない。理由: Unity 側（`unity/`）を変えない。代替証拠: `git diff --name-status` が `tools/Workflow` の 3 file と本書・program だけであること。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路: H1〜H7（上表）。H1・H4 は owner、H2・H3・H5〜H7 は高コスト証拠の実行担当（非仮想化 pwsh。Claude Code の PowerShell tool で可）。観測は全て構造・値の検査で、画像は使わない。証拠は H7 の `evidence stage` で本スライスの task に固定し、取得案内で判定 C・C' に渡す。動作確認（Phase B の後）: 実 copy に触れる条件は全て高コスト証拠なので `対象なし`。
- 観測 script（A1 で書き、A3 で凍結する。(P) と独立）: 引数 `-CopyRoot <copy root>`、`-Exclude <除外する最上位 entry の名前の配列>`、`-Output <path>`。出力は JSON で、(a) 新 root: 印の SHA-256、直下の entry の名前と種別。(b) 旧 root: 直下の entry の名前・種別・size。(c) copy root の兄弟: `LocalCache\Local` の直下の entry の名前と種別（copy root 自身を除く）。(d) copy root: 存在するか、直下の entry の名前と種別。(e) 除外した最上位 entry: reparse point をたどらない再帰列挙の相対 path・種別・size・更新時刻。絶対 path は書かない。

  ```powershell
  param([Parameter(Mandatory)][string]$CopyRoot,[string[]]$Exclude=@(),[Parameter(Mandatory)][string]$Output)
  $ErrorActionPreference='Stop'
  function Get-Kind([IO.FileSystemInfo]$i){if($i.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)){'reparse'}elseif($i -is [IO.DirectoryInfo]){'directory'}else{'file'}}
  function Get-Top([string]$Path){if(-not [IO.Directory]::Exists($Path)){return $null};@([IO.DirectoryInfo]::new($Path).EnumerateFileSystemInfos()|Sort-Object Name|ForEach-Object{[ordered]@{name=$_.Name;kind=(Get-Kind $_);size=$(if($_ -is [IO.FileInfo]){$_.Length}else{$null})}})}
  function Get-Tree([string]$Root){$out=[Collections.Generic.List[object]]::new();$stack=[Collections.Generic.Stack[string]]::new();$stack.Push($Root)
    while($stack.Count){$d=$stack.Pop();foreach($i in [IO.DirectoryInfo]::new($d).EnumerateFileSystemInfos()){$k=Get-Kind $i;$out.Add([ordered]@{path=[IO.Path]::GetRelativePath($Root,$i.FullName);kind=$k;size=$(if($i -is [IO.FileInfo]){$i.Length}else{$null});mtime=$i.LastWriteTimeUtc.Ticks});if($k -eq 'directory'){$stack.Push($i.FullName)}}}
    @($out|Sort-Object {$_.path})}
  $userProfile=[Environment]::GetFolderPath('UserProfile');$local=[Environment]::GetFolderPath('LocalApplicationData')
  $newRoot=[IO.Path]::Combine($userProfile,'OneStarMaker');$oldRoot=[IO.Path]::Combine($local,'OneStarMaker');$parent=[IO.Path]::GetDirectoryName($CopyRoot)
  $marker=[IO.Path]::Combine($newRoot,'store-root.json')
  $result=[ordered]@{schemaVersion=1;createdAt=[DateTimeOffset]::UtcNow.ToString('o')
    newRoot=[ordered]@{markerSha256=$(if([IO.File]::Exists($marker)){(Get-FileHash -LiteralPath $marker -Algorithm SHA256).Hash.ToLowerInvariant()}else{$null});top=(Get-Top $newRoot)}
    oldRoot=(Get-Top $oldRoot)
    copySiblings=@((Get-Top $parent)|Where-Object{$_ -and $_.name -cne [IO.Path]::GetFileName($CopyRoot)}|ForEach-Object{[ordered]@{name=$_.name;kind=$_.kind}})
    copyRoot=[ordered]@{exists=[IO.Directory]::Exists($CopyRoot);top=@((Get-Top $CopyRoot)|Where-Object{$_}|ForEach-Object{[ordered]@{name=$_.name;kind=$_.kind}})}
    excluded=[ordered]@{}}
  foreach($n in $Exclude){$p=[IO.Path]::Combine($CopyRoot,$n);$result.excluded[$n]=$(if([IO.Directory]::Exists($p)){Get-Tree $p}elseif([IO.File]::Exists($p)){'file'}else{'absent'})}
  [IO.File]::WriteAllText($Output,($result|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
  ```

- 人間の判断が必要な条件: H1（Codex の静止）と H4（承認）。どちらも owner の明示の指示の原文を Evidence に入れて受理する（観察記録の受理。画像の独立再評価は要らない）。O5 の Codex 内の観測は任意で、行った場合は対象 head・日時・reasonCode を記録する。
- 未知の操作経路の疎通: 2026-10-19 に A1 の担当が、上の観測 script を実 copy に対して読取りだけで 1 回試した（非仮想化 pwsh、`-Exclude Workflow`）。exit 0、約 0.7 秒。新 root の直下は `Artifacts`・`Workflow`・印、旧 root の直下は tombstone 3 つと `RevisionLocks`、copy root の兄弟 2 件、copy root の直下 8 件、`Workflow` の再帰列挙 61 entry。出力に絶対 path と package 名は無かった。`evidence stage` は既存の通常操作。(P) 自体は未実装なので、その実 copy での動作は高コスト証拠の H3・H6 で初めて確かめる。
- 高コスト証拠: H2〜H7。実装から独立した期待値: (1) `after.json` の `copyRoot` は、承認した最上位 entry が全てなら `exists=false`、それ以外は `top` が除外した entry の名前と一致。(2) `before-1`・`before-2`・`after` の `newRoot`・`oldRoot`・`copySiblings` が 3 つとも一致。(3) `after.excluded` が `before-2.excluded` と一致。(4) (P) の結果 file の削除件数と合計 size が、承認 manifest の承認した最上位 entry の file 数・合計 size と一致。(5) `before-1` と `before-2` が全体で一致（`createdAt` を除く）。
- 判定 C の照合 script の方式: 判定 C の offline 実行。判定 C の担当が、H7 で stage した原記録の複製に対して上の期待値 (1)〜(5) を照合する script を書いて実行する。照合は file の比較だけで、資格情報・通信を使わない。原記録へ適用する前に、判定 C の担当が作る合成 fixture（一致する例と、(2) の旧 root の tombstone が消えた例、(4) の件数が 1 違う例）で一度通す。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC-10 の import 検査。
- A0/A1 主担当: Claude Code の session（進行役）。
- A2 独立レビュー: 未実施。risk `high` なので、(1) アーキテクチャゲート（配置、依存、寿命、テスト境界）、(2) 不変条件 I1〜I7 を破る順序の探索と H1〜H7 の手順の穴、(3) A0 だけから代替構成を出すレビュー、の 3 件を候補とする。入力版は A2 入力版の commit の blob の sha256。
- A3 統合: 未実施（owner と進行役。O1〜O8 の決定）。
- C' 用に予約する担当: A2 で使わなかった系列・ベンダーを 1 つ残す。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
