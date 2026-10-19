# Artifact Storage: LocalCache 側 copy の扱い（合流の価値の判断と削除）

packaged app（Codex）が旧 root の `Artifacts` / `Workflow` / `Harness` として書き、書込み仮想化で LocalCache 側に入った記録（以下 **copy**）を、新 root へ合流させる価値があるかを判断し、価値が無い部分は合流せずに削除して終えるスライス。`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md`（以下 program）§6「LocalCache側copyの扱い（合流の価値の判断と削除）」を受け持つ。前提は program §4 F（store root の移設と実ストアの移行。PR #139・#140、2026-10-19 に完了）。

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A0 完了。A1 初稿は未着手）
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
  - 次の Phase・役割: Phase A の A1（初稿）。
  - 次の作業: 下の「Phase A の着手手順」の 1〜4。§2 の A0 の小節は書き換えず、A1 初稿は「A1:」で始まる小節に書く。
  - 引き継ぐ commit SHA: この文書を追加した commit（branch の先頭）。作業ツリーが dirty なら引き継がない。
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

## 3. 責務マップ

A1 で書く。

## 4. 実装計画

A1 で書く。

## 5. テストとレビュー計画

A1 で書く。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
