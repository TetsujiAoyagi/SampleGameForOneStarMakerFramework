# Artifact Storage: store root 移設の実行と合流

`ARTIFACT_STORE_ROOT_RELOCATION.md`（以下「移設スライス」）が GO 候補 head で用意した consumer（新 root を読む resolver と 3 CLI）が動ける状態を、実ストアで一回だけ作る運用スライス。旧 root の所有 role を新 root へ移し、移行済みの印を発行し、LocalCache 側 copy を新 root へ合流し、清掃 job を新 root で再登録する。移設スライス §2 O1 の工程表の手順 2〜8 を担い、手順 9（移設スライスの merge）の前提を作る。`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §6 の「store root 移設の実行と合流」と、同 §6 の「分裂したストアの合流」のうち合流の実行を受け持つ。

> **有効な scope（A3 の O4 による）:** 実ストアの移設、印の発行、smoke、清掃 job の再登録。LocalCache 側 copy の合流は別スライス。上の段落と §1 の目的は A0 の入力として残している。

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `B`（Phase A revision 1 を 2026-10-19 に凍結。凍結版 = message に `A3 freeze` を含む commit。Phase B 未着手）
- branch: `claude/artifact-store-root-migration`（移設スライスの branch `claude/artifact-store-root-relocation` の `94f93930` から切った。`34cf6607..94f93930` の差分は `ARTIFACT_STORE_ROOT_RELOCATION.md` だけで、コードは移設スライスの GO 候補 head `34cf6607` と同一。PR base は `develop`）
- implementation base commit: `94f939303951c8ec0614303d66525bb9abcbacec`（コードは `34cf6607302d27f49bf9d3febaaa31d624f5cbed` と同一）
- implementation head commit: 未固定
- risk: `high`（実ストアの不可逆な移設、資格情報 directory の移動、旧 writer の静止と清掃 job の停止・再登録、owner 承認の manifest による合流、移設中は store を使う CLI が無い期間を作る）
- owner: OSM maintainers（A3 の凍結、手順 3 の静止と旧 task の削除、実ストア操作の一括承認（O7）、Phase D の判断は owner）
- created: 2026-10-19
- expires: 2026-12-19 または置換 revision
- harvest to: `tools/Artifacts/README.md`（移設の完了後の所在）、`tools/Workflow/README.md`・`tools/Harness/README.md`（必要な場合）、`docs/README.md` の program 表、`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4・§6
- Phase A snapshot の取得案内・状態: Git 上の本文で完結（A0 = commit `a016d02a`、A2 入力版 = commit `0dd3bd6e`、凍結版 = message に `A3 freeze` を含む commit。凍結版の内容 sha256 は commit の blob（`git rev-parse <commit>:docs/handoff/ARTIFACT_STORE_ROOT_MIGRATION.md`）で求められるので転記しない）
- Phase B result snapshot の取得案内・状態: 未到達
- evidence bundle の取得案内・状態: 未到達
- C' blind bundle の取得案内・状態: 未到達
- 引継ぎ票:
  - 次の Phase・役割: Phase B（実装）。
  - 次の作業: 下の「Phase B の着手手順」に従い、§3・§4 の module・入口・suite を実装し、B の出口まで行う。
  - 引き継ぐ commit SHA: この branch の先頭（message に `A3 freeze` を含む commit）。作業ツリーが dirty なら引き継がない。
  - 取得案内の所在: なし（Phase A は Git 上の本文だけ）。
  - 必要な権限: リポジトリの読み書き、pwsh 7。Unity は使わない。実ストア・LocalCache・資格情報・Task Scheduler・R2 に触れない。
  - 停止規則: 下の「Phase A へ差し戻す条件」に当たったら実装を止めて返す。
  - 引き渡さないもの: ローカル store・LocalCache の実 path、資格情報。

### Phase B の着手手順

1. **読むもの:** `AGENTS.md`、`.agents/skills/osm-workflow/SKILL.md`（「Phase B: 実装」）、本書の §2 の「A1:」「A3:」で始まる小節と §3〜§5（これが凍結した仕様。§1 と §2 の A0 の小節は入力の記録で、A1・A3 と食い違うところは A1・A3 を正とする）。コードは `tools/Workflow/WindowsStoreRoot.psm1`（resolver の関数・seam・public 型 `StoreRootNative`）、`tools/Workflow/WindowsStoreContext.psm1`、`tools/Workflow/tests/StoreRoot.Tests.ps1` と `StoreRootTestSupport.ps1`（temp base と module scope の seam の設定方法、`-Case` / `-ResultPath` の suite の形）。
2. **照合:** branch `claude/artifact-store-root-migration` の HEAD が引継ぎ票の commit で、作業ツリーが clean であること。食い違えば始めない。
3. **実装の順序:** (1) `tools/Workflow/StoreRootMigration.psm1`（§3・§4。入口の 3 分岐、前提の確認、role ごとの状態表、件数と合計 size、rename と tombstone、印の発行と resolver による確認、例外を固定 code に畳む）。(2) 引数を取らない入口 `tools/Workflow/StoreRootMigration.ps1`。(3) `tools/Workflow/tests/StoreRootMigration.Tests.ps1`（§5 の `ac-1`、`ac-1b`、`ac-2`〜`ac-6`、`ac-8`、`ac-9`）。新規 `.ps1` / `.psm1` の書き方は既存の `tools/Workflow` に合わせる（`#nullable enable` は Unity 側の `.cs` だけの規則）。`Task.Delay` / `Thread.Sleep` を使わない。
4. **してはいけないこと:** 入口 script を実ストアに対して実行しない（実行は Phase C の後の高コスト証拠）。consumer の既存 file（SHA A `34cf6607` の `tools/`）と README を変えない（I6）。`unity/` を変えない。
5. **B の出口:** `pwsh tools/Workflow/tests/StoreRootMigration.Tests.ps1 -Case '*' -ResultPath <temp path>` と `pwsh tools/Workflow/tests/StoreRoot.Tests.ps1 -Case '*' -ResultPath <temp path>` が executed 1 件以上・failed 0、`pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` が error 0、`git diff --name-status 94f93930 HEAD -- tools/ unity/` が §3 の 3 file だけ。§6 に実装・HANDOFF との差（B 適応）・未実行・implementation head を書き、§0 の status と引継ぎ票を次の担当（発見 C。動作確認は `対象なし`）へ更新し、1 回 commit する。push と PR はしない（O5）。
6. **Phase A へ差し戻す条件:** consumer の既存 file を変えないと成立しない（I6）。所有 role 配下の file の内容を開く必要が出た（I5）。module scope の seam では AC-2・AC-4・AC-5 の失敗注入ができない。§4 の状態表・入口の分岐・reasonCode を変える必要が出た。

### Phase A の着手手順（完了済み。記録として残す）

1. `AGENTS.md`、`.agents/skills/osm-workflow/SKILL.md`、同 `references/phases-and-handoff.md`・`references/handoff-template.md`・`references/architecture-gates.md`・`references/review-evidence.md`・`references/docs-policy.md`、`docs/README.md` を全文読む。次に本書を全文読む。
2. 移設スライスの `ARTIFACT_STORE_ROOT_RELOCATION.md` の §2（特に O1・O2 と「ここでは答えない問い」）、§4（「決定」D3〜D9、「移行済みの印」「resolver の契約」「経路表」「記録 path の分類表」「合流の manifest」）、§7・§8 の「後続スライスの入力」を読む。`tools/Artifacts/README.md` の「エージェント操作の事前承認」「実行するprocessの条件（store rootと移行済みの印）」「分裂したストアの合流」を読む。
3. branch `claude/artifact-store-root-migration` を checkout し、HEAD を引継ぎ票と照合する。
4. A1 初稿を書く。commit は A2 入力版（1 回）と A3 凍結版（1 回）だけにし、途中の改稿を積まない。新規 A1 なので進める最低条件を不変条件と成立条件に分ける。高コスト証拠の分類と判定 C の照合 script の方式も A1 で書く。
5. A2 独立レビューを取り、指摘を §5 に採用・不採用・保留で記録する。risk `high` なので、A0 だけから代替構成を出すレビューを 1 件検討し、C' 用に未関与の系列を予約する。
6. A3 で owner と凍結し、`pwsh tools/docs-audit.ps1` と `pwsh tools/contract-audit.ps1` を通す。PR の作成時期は §2 の未決事項 U9 の回答に従う。

## 1. 目的と対象外

- 目的: 移設スライスの consumer（GO 候補 head `34cf6607` の CLI。以下 (C)）が production の経路で動く状態（新 root に移行済みの印があり、旧 root に所有 role が無い）を、実ストアの記録を失わず、store を再分裂させずに作る。そのための移行プログラム（以下 (D)、producer）を Phase A〜C' で版を固定してから、O1 工程表の手順 3〜8 を一回実行する。あわせて、LocalCache 側 copy のうち合流できる単位を新 root へ合流し、Task Scheduler の Evidence 清掃 job を新 root の binding で再登録する。
- 対象外:
  - 移設スライスの consumer コード（resolver、3 CLI の gate、旧 root 別名の prefix 置換、合流 plan の `legacyRoots`）の変更。変更が要るなら移設スライスの GO 候補 head が変わるので、本スライスでは行わず owner へ返す。
  - 移行済みの印の形式（移設スライス O2）と位置の変更。
  - Codex 側残置（LocalCache 側 copy）の削除・期限・後始末。program §6「Codex側残置の後始末」の既存項目のまま。合流は copy を読むだけで消さない。
  - 合流で `hold` になる単位の解消方式（公開途中の不可視性を保つ方式）の設計。program §6「分裂したストアの合流」が「必要なら別の設計スライスに分ける」としたもの。
  - 旧 root の親 directory `OneStarMaker`、`RevisionLocks`（Unity Runtime の `RevisionProcessLease` が毎回作る）、所有 role 以外の directory（`H2a Review` 等の not-owned）の削除・移動。
  - 旧 root 別名（移設スライス S4）の廃止期限。program §6「分裂したストア」の入力のまま。
  - 資格情報の新規登録・置換・rotation・失効、別 account / bucket、公開配布、R2 の通常操作を超える操作。
  - `ArtifactsRoot`（`--artifacts-root` / `OSM_ARTIFACTS_ROOT`）。
  - Unity 側コード、Cloud・別 PC からの取得。
  - 移設スライスの手順 9（merge）と手順 10（移設スライスの Phase D）。それぞれ owner と移設スライスの Phase D 担当が行う。本スライスはその前提を作るところまで。
- 現況（A0 で固定した観測。日付は 2026-10-19、別記のものを除く）:
  - 移設スライスは GO 候補 head `34cf6607`（push しない軽量 tag `evidence/artifact-store-root-relocation/1`）で、判定 C が GO、C' 1 巡目が blocker なし。PR #139 は push 済み・未 merge で、本スライスの手順 2〜8 が済むまで merge しない（移設スライス §0）。
  - 実ストアでの (C) の振る舞い: 印なしの実状態で、非仮想化 pwsh と packaged app（Codex、full access）内の両方から `workflow-task.ps1 status` が `store-not-migrated`・exit 1。新 root は前後で不在、旧 root の tree 一覧（名前・属性・size・更新時刻）は 47,974 entry で前後一致（移設スライス §6 の動作確認、対象 head `34cf6607`）。
  - 本 session（Claude Code の PowerShell tool。非仮想化として扱う）の metadata 照会: 新 root `<UserProfile>\OneStarMaker` は不在。旧 root の `Artifacts` と `Workflow` は directory として存在し、`Harness` は `Directory.Exists` が false（`Directory.Exists` はアクセス拒否と不在を区別しないので、不在の証明ではない。2026-10-17 の `store plan` で Harness の 4 task が LocalCache 側にだけあったことと矛盾しない）。`UserProfile` と `LocalApplicationData` は同じ volume（drive root が一致）。`UserProfile` の祖先 3 段に reparse point は無い（移設スライス §6）。
  - 2026-10-17 の読取のみの `store plan`（1 回、`status: passed`）: 単位 20（`publish` 4・`skip` 0・`hold` 16）、`excluded` 90（`unreferenced-transfer` 77、`not-owned` 8、`out-of-scope-role` 2、`credentials` 1、`revision-locks` 1、`unrecognized` 1）。Harness の 4 task は `h2c-unity-gate-r2` が `publish`、`h1-transport-identity` と `h2c-unity-gate` が `hold`（`reference-missing`）、`artifact-evidence-lifecycle` が `hold`（`dependency-held`、`multi-workflow-unit`、`reference-localcache`、`reference-missing`、`reference-out-of-role`）。deployment 単位 1 件が `conflict` で保留され、Evidence を持つ task 単位の多くがそれへの `dependency-held`。`unrecognized` 1 件は `retirements-v1`（名前表に無い）。この manifest は `logicalRoot` が旧 root で `legacyRoots` を持たないので、(C) では `manifest-invalid` になり再 plan が要る（移設スライス §4「合流の manifest」）。`store consolidate` は dry-run も含めて未実行。
  - 旧 writer は 2 つある: (A) merge 前の develop の checkout の CLI（本 repo の develop checkout と、develop を checkout した他の worktree・session）、(B) 旧 runtime copy で動く Task Scheduler の Evidence 清掃 job。現在の (B) の登録状態は本 A0 では観測していない。
  - 移設スライスの Phase C / C' の Evidence（task `artifact-store-root-relocation`、`locality` = `local`）は (A) で旧 root の `Artifacts` に stage されている。移設スライスの Phase D はこれを移設後に (C) で読む（旧 root 別名の prefix 置換に依存）。
  - packaged app 内の process は、LocalCache 側に旧 root の `Artifacts` / `Workflow` / `Harness` の copy が残る間、(C) でも `legacy-store-present` で止まる（移設スライス I3・D2）。移設後も、残置の後始末（program §6）が済むまで packaged app を Phase 担当にできない。
  - 移設スライスの C / C' が後続の入力として挙げたもののうち、本スライスの実行に関わるもの: (1) 旧 root の導出は `LocalApplicationData` の既定位置で、AppData を redirect した host では redirect 先にだけある旧記録が prefix 置換に乗らない（本 host が redirect していないことは未観測）。(2) `StoreConsolidation.psm1` の reasonCode の allow-list に resolver の 3 code が無く、入口通過後に resolver が失敗すると `consolidation-failed` 等に畳まれる。(3) 印の読込みは 4 KiB の判定より前に file 全長を確保する（fail-closed）。

## 2. 意思決定と受け入れ境界

A0 では入力と未決事項を固定した。下の A0 の小節（入口条件〜本文へ転記した実装制約）は A1 で書き換えず、A1 初稿は「A1:」で始まる小節に書く。両者が食い違う箇所は A1 の小節に理由を書き、A3 で owner が決める。

### 入口条件（移設スライス §2 O1 の決定を正とする）

- 移設スライスの GO 候補 head（SHA A = `34cf6607`）で判定 C が GO、C' に blocker が無い。2026-10-19 に成立。
- 本スライスは移設スライスの merge の**前**に、GO 候補 head（SHA A）を前提に行う（O1 の順序 (γ)）。program §6 の「入口条件: 当該スライスのmerge」は O1 の決定より前の古い記述で、本 A0 と同じ commit で program §6 を O1 に合わせて直す。
- 手順 3〜8 の実ストア操作は、本スライスが Phase A〜C' を経て (D) の版（SHA B）を固定した後に限る（O1 手順 2）。

### O1 工程表（移設スライス §2 から転記。本スライスは手順 2〜8 を担う）

登場する actor と版は 4 つで、混同しない: (A) merge 前の develop の CLI（旧コード。非仮想化）、(B) 旧 runtime copy で動く清掃 job（旧コード。Task Scheduler）、(C) 移設スライスの GO 候補 head の CLI（新コード。consumer。印を書かない）、(D) 本スライスの移行プログラム（producer。印を書く唯一の actor。別 branch で Phase A〜C' を経る）。

| 手順 | 実行担当 | 使う版 | 入口の状態 | 失敗時の停止・再開 |
|---|---|---|---|---|
| 1. 判定 C・C' を完了し GO 候補 head（SHA A）を固定 | 移設スライスの Phase C / C' | 記録は (A) | 印なし・旧 root 稼働中 | 完了済み（2026-10-19） |
| 2. (D) を本スライスとして設計・実装・レビューし、版（SHA B）を固定 | 本スライスの Phase A〜C' | (D) は (C) の resolver を読取に使ってよいが、印の書込みと旧 root の move / copy は (D) が持つ | 同上 | (D) が未完なら移設スライスは merge せず待つ。(A) の運用を続ける |
| 3. 旧 writer の静止: (A) の新規実行を禁止し、(B) の job を無効化し、実行中 job の終了を確認 | owner（手順は (D) の runbook） | — | 同上 | 終了を確認できなければ移行に進まない |
| 4. 旧 root の最終 inventory（名前・属性・size・更新時刻）と差分保全 | (D) | SHA B | 同上 | inventory が取れなければ停止 |
| 5. 所有 role（`Artifacts`、`Workflow`、`Harness`）の移設（同一 volume の move、または複写 + ACL 再設定）と、ACL・内容・数量の照合 | (D)、承認は owner | SHA B | 移行中（印なし） | 照合不一致なら停止。旧 root を削除せず、印を書かない。(A) を再開しない（再開すると旧 root が再び writer になる）。回復の入口は (D) の runbook |
| 6. 旧 root の所有 role 不在の確認 | (D) | SHA B | 同上 | 残っていれば 5 へ戻る |
| 7. 印の原子的発行（一時名 → rename） | (D) | SHA B | 移行後 | 発行失敗なら印を残さず停止 |
| 8. 新 head の smoke test（下の smoke 項目。seam 無しの production resolver と 3 CLI） | (D) の動作確認担当、観測は非仮想化側 | (C) = SHA A | 移行後 | 不一致なら印を消さず、(A)・(B) を再開せず、owner に返す |
| 9. `develop` への merge（移設スライス） | owner | — | 移行後 | merge が通らない・延期なら (C) の worktree を唯一の CLI として運用を続け、(A) を再開しない。印の削除・旧 root の復元・自動 rollback はしない |
| 10. 移設スライスの Phase D | 移設スライスの Phase D 担当 | (C) または merge 後の develop | 移行後 | (C) の CLI で Phase D を書けなければ移設スライスの欠陥として owner に返す |

### 移設スライスから転記した入口条件と制約

- 印を書く前に、旧 root の所有 role が空であることを列挙で確認する（A2 R2）。
- 2026-10-17 の `store plan` の manifest は `manifest-invalid` になるので、(C) で再 plan する（A2 R1 F8）。
- 移行の実行後は旧コード（develop の checkout、旧 runtime copy の清掃 job）を動かさない。本スライス自身と移設スライスの Phase D の Workflow / Evidence の記録は GO 候補 head（SHA A）の CLI で行う（A2 R1 E1・E2）。
- (D) は移設スライスの resolver を読取に使ってよいが、印の書込みと旧 root の move / copy は (D) が持つ。`store consolidate` を旧 root の実体から新 root への移設に使わない（I3 により旧 root が残る間は resolver が止まるため。A3 R3 F2）。
- 旧 runtime copy の清掃 job は旧 root に書き続けるので、移行前に停止する（A2 R1 E2）。job の root 変更と再登録も本スライスが持つ。既存の binding manifest は各 role の `logicalPath` が旧 root なので (C) では `runtime-binding-invalid` になり、再 install が要る（移設スライス §1 A1 の観測）。
- 印の形式（O2）: 位置 `<root>\store-root.json`。JSON object で field は `schemaVersion`（整数 1）、`kind`（`osm-store-root`）、`ownerSid`（書いた Windows ユーザーの SID、現在ユーザーと一致）、`createdAt`（ISO 8601）の 4 つちょうど。UTF-8（BOM 無し）、4 KiB 以下。同じ directory の一時名に書いて flush し、rename で現す（部分状態を見せない）。
- 旧 root の残存判定は親 directory ではなく所有 role 3 つの directory の存在で行う。移行は親 directory `OneStarMaker` と `RevisionLocks` を消さない（Unity の実行で親は再び現れうる）。
- LocalCache 側 copy の合流先は新 root。manifest の承認は owner で、承認までは `consolidate` を dry-run も含めて実行しない（program §6）。
- H1 適用 task（`h2c-unity-gate` 等）の CURRENT・receipt が持つ path bytes と新 root の両立は、本スライスの合流後確認で扱う（移設スライス A2 R2 リスク 4）。
- packaged app 側は、LocalCache の残置がある間は `legacy-store-present` を期待し、成功とは記録しない（A3 R3 F5）。

### smoke test の項目（O1 手順 8。移設スライス §2「ここでは答えない問い」から転記）

seam を使わない production の `Get-OsmStoreRoot` と 3 CLI（`artifacts.ps1`、`workflow-task.ps1`、`harness.ps1`）を (C) = SHA A で使い、非仮想化側で次を確認する。

1. root と、その配下に開いた file handle の実体 path（`GetFinalPathNameByHandle`）が論理 path と一致する。
2. Evidence binding の再生成と検証が通る。
3. `workflow-task.ps1 status` で記録を読み戻せる。
4. `harness.ps1 current` で読める。
5. 旧 root に所有 role が無い。
6. 旧 runtime の job が停止状態である。
7. packaged app 側: LocalCache の残置がある間は `legacy-store-present` を期待する（成功とは記録しない）。

### このスライスが答える問い（A0 案。A1 で一つの問いに確定する）

(D) を SHA B に固定し、O1 手順 3〜8 を実ストアで一回実行したとき、旧 writer を静止したまま所有 role の記録を失わずに新 root へ移し、印を発行して、(C) の production smoke test が通る状態に到達できるか。あわせて、LocalCache 側 copy の合流できる単位を新 root へ合流し、清掃 job を新 root の binding で再登録できるか。

後半（合流と job 再登録）を同じ問いに束ねるか、独立して失敗しうる別の問いに分けるかは U1 で決める。

### 進める最低条件の候補（A0 案。A1 で不変条件と成立条件に分け、観測可能な語に直す）

- 起きてはいけないことの候補:
  - 旧 writer の静止（(A) の新規実行の禁止、(B) の無効化と実行中 job の終了）を確認できないまま、旧 root の所有 role を動かす。
  - 移設後の照合（名前・属性・size・更新時刻・数量・ACL）が一致しないまま、または旧 root の所有 role の不在を列挙で確認しないまま、印を書く。
  - 印を部分状態で見せる、または発行失敗後に印を残す。
  - 不変の記録（receipt・catalog・transfers の記録）の bytes を書き換える（旧 root 別名の prefix 置換の前提を壊す）。
  - 旧 root の親 directory、`RevisionLocks`、not-owned の directory を消す・動かす。LocalCache 側 copy を消す・書き換える。
  - 移設中・失敗時に (A) を再開する、または印の削除・旧 root の復元・自動 rollback をする。
  - owner の承認の無い manifest で `consolidate`（dry-run を含む）を実行する。`store consolidate` を旧 root から新 root への移設に使う。
  - 資格情報（DPAPI の暗号化 store）・鍵・header・署名 URL を引数・stdout・stderr・ログ・Git・テスト結果へ出す。資格情報 file の内容を読む・複写して置換する。
- 起きなければならないことの候補:
  - 手順 4 の inventory と手順 5 の照合結果が、所有 role の全 entry について一致する。
  - 印が O2 の形式で原子的に現れ、(C) の resolver が受け入れる。
  - smoke 項目 1〜6 が非仮想化側で全件成立し、7 は `legacy-store-present` として記録される。
  - 移設スライスの Phase C / C' の Evidence を (C) で取得できる（移設スライスの Phase D の前提）。
  - 合流（承認した manifest の `publish` 単位）が新 root に入り、(C) で読める。H1 適用 task の CURRENT について、合流した単位が (C) の `harness.ps1 current` で読める。
  - 清掃 job が新 root の binding で登録され、`schedule-status` で確認できる。

### ここでは答えない問いと所有する後続（A0 案）

- Codex 側残置（LocalCache 側 copy）の後始末と、packaged app を再び Phase 担当にする条件: program §6「Codex側残置の後始末」（入口に「残置の削除後に packaged app を Phase 担当にする」が加わっている。移設スライス §2）。
- `hold` 単位（`reference-missing`、`dependency-held`、`conflict` 等）の解消方式: program §6「分裂したストアの合流」（必要なら別の設計スライス）。
- 旧 root 別名の廃止期限と条件: program §6「分裂したストア」。
- 移設スライスの C / C' の後続入力のうち、本スライスの実行に関わらないもの（`Get-HarnessStoreRoot` の export、別名免除の書き方の不統一、CLI 経路の junction case 等）: 移設スライスの Phase D が harvest 先を決める。

### 未決事項（A1 で答える）

- **U1 問いの分割:** 合流（`store plan` の再実行、owner の manifest 承認、`consolidate`）と清掃 job の再登録を、移設（手順 3〜8）と同じ最低条件に束ねるか、独立した問いとして分けるか。合流は移設の完了（印あり・旧 root に所有 role なし）を前提にし、manifest 承認という別の owner 判断を待つ。
- **U2 move か複写 + ACL 再設定か:** 本 host では `UserProfile` と `LocalApplicationData` が同じ volume で、directory の move が候補になる。move では明示 ACE は保たれるが、継承 ACE は新しい親（`<UserProfile>\OneStarMaker`）から再計算されうるので、private ACL 規則（owner と継承無しの 3 主体 FullControl。`<root>\Artifacts\credentials` 等）が移設後も成立するかを照合の対象にする。新 root directory 自体を誰が作り、どの ACL にするか（`Initialize-CredentialRoot` は `<root>` に存在確認だけを適用し、作らない）。
- **U3 旧 writer の静止の確認方法（手順 3）:** develop を checkout した全 worktree・並行 session・Unity Editor を含めて (A) の新規実行を止める方法と、その確認。(B) の登録状態の観測（(A) の `schedule-status` で足りるか）、無効化、実行中 instance の終了確認。手順 3 以降、(C) は印が出るまで `store-not-migrated`、(A) は禁止なので、store を使う CLI が無い期間ができる。その期間の長さと、その間に Evidence・Workflow を記録できないことの扱い。
- **U4 移設中の記録の置き場:** 手順 3〜7 の inventory・照合・印発行の生記録は、store に stage できない期間に作られる。どこに一時保持し、手順 8 の後に (C) でどう Evidence として固定するか。本スライスの Phase A〜C' の記録は手順 3 より前なので (A) で記録でき、所有 role と一緒に新 root へ移る。
- **U5 高コスト証拠への分類:** 手順 3〜8 は不可逆な実ストア操作。O1 の「手順 2 で SHA B を固定してから 3〜8」を、`review-evidence.md` の「高コスト証拠と最終証拠ラウンド」（判定 C → 暫定 C' → 高コスト証拠の取得 → 最終証拠ラウンド）に当てはめるか。当てはめる場合の、実装から独立した期待値（inventory との一致、smoke 項目）と照合 script の方式。
- **U6 手順 5 の失敗時の回復:** 照合不一致・途中失敗の状態（一部の role だけが移った、印なし）からの回復手順を (D) の runbook にどこまで持たせるか。O1 は自動 rollback・旧 root の復元をしないとしている。
- **U7 合流と再登録の順序:** 印の発行後、smoke test、`store plan` の再実行、manifest 承認、`consolidate`、清掃 job の再 install の順序。`store plan` / `consolidate` は非仮想化 process を要求し、LocalCache 側の source を実 path で読取専用に開く（packaged app 内からは実行しない）。合流後に、移設スライスの C / C' 入力 (2) の reasonCode の畳み込みが運用上の障害になるか。
- **U8 旧 runtime copy の扱い:** 旧 root の `Artifacts\runtime\<head>\` は `Artifacts` と一緒に移る。旧 binding manifest は旧 root の `logicalPath` を持つ。再 install 後の旧 runtime copy の扱い（残す・清掃に任せる）。
- **U9 (D) の置き場所・寿命と PR:** (D) を `tools/` のどこに置くか（`tools/Workflow` の resolver を読取に使う）、一回の実行後もリポジトリに残すか。本 branch は移設スライスの未 merge の commit を含むので、PR を開く時期（#139 の merge 前か後か）と、本スライスの PR の merge 順。
- **U10 host 条件の確認:** 本 host で、process が見る `LocalApplicationData` が既定位置と一致する（AppData を redirect していない）こと。移設スライスの C / C' 入力 (1) が本 host で問題にならないことを、手順 4 の前に metadata で確かめるか。
- **U11 事前承認の範囲:** 実ストアの一回の移設と清掃 job の再登録は、`tools/Artifacts/README.md`「エージェント操作の事前承認」の通常操作に含まれない（一回の reset と同じく、A3 で対象・除外と開始条件を固定して実行する種類）。A3 で固定する対象・除外・開始条件と、owner の承認の取り方（手順 3・5、manifest）。

### 本文へ転記した実装制約

- 依存は Game → Framework の一方向。asmdef 参照を勝手に追加しない。本スライスは Unity 側を変えない見込み。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- path は `tools/path-budget.json` の予算に従う。
- Artifact Storage の通常操作は README の事前承認に従い、それ以外（本スライスの実ストア移設、資格情報の置換等）は A3 で固定した範囲と owner の承認でだけ行う。秘密を露出しない。利用中の保護と対象外の非削除を維持する。実行環境の承認・拒否を迂回しない。
- 移設スライスの consumer コード（`34cf6607`）、印の形式（O2）、資格情報の保存方式（DPAPI CurrentUser）、private ACL 規則、Known Folder から root を導く原則は変えない。
- PR の base は `develop`。

### A1: 進め方の前提（owner の決定、2026-10-19）

- 実ストアに今使っている記録は無く、壊れても作り直せる（O7 の理由）。そのため、本スライスの証拠・照合・手順は、移設が実際に終わったことと (C) が新 root で動くことを確かめる最小限にする。owner は「厳密にやる意味を感じない」とした。
- AGENTS.md の常時契約（秘密の非露出、`Task.Delay` / `Thread.Sleep` の禁止、path 予算、PR の base は `develop` 等）は緩めない。
- 用語: **旧 root** = `<LocalApplicationData の既定位置>\OneStarMaker`。**新 root** = `<UserProfile>\OneStarMaker`。**所有 role** = `Artifacts`、`Workflow`、`Harness`。**tombstone** = 移した role と同じ名前で旧 root に置く空の通常 file（O8）。

### A1: このスライスが答える問い（確定）

(D) を SHA B に固定し、O1 手順 3〜8 を実ストアで一回実行したとき、所有 role を新 root へ移し、印を発行し、(C) の smoke test（清掃 job の新 root での再登録を含む）が通る状態に到達できるか。LocalCache 側 copy の合流は対象外（O4）。

### A1: 進める最低条件

- 不変条件（起きてはいけないこと）:
  - **I1 移せないものを複写で代えない:** (D) は所有 role を同一 volume の directory rename（`MoveFileExW`、`MOVEFILE_COPY_ALLOWED` を付けない）だけで移す。別 volume・移動先の既存・rename 失敗では止まり、複写しない（資格情報の暗号化 store の内容を読まないため）。
  - **I2 role が残る間に印を書かない:** 印は、旧 root の所有 role 3 つがすべて tombstone（通常 file）になったことを確かめた後にだけ書く（当初から無い role にも tombstone を置く。§4 の状態表）。
  - **I3 印の部分状態を見せない:** 新 root 直下の一時名に `CreateNew` で書いて flush し、上書きしない rename で `store-root.json` に現す。失敗したら一時名を消して止まる。既存の `store-root.json` は上書きしない。
  - **I4 仮想化された process で動かさない:** (D) は `Get-OsmStoreContext` が `not-virtualized` のときだけ動く（packaged app 内で LocalCache 側 copy を移さないため。A2 R1 F1）。
  - **I5 秘密と path を出さない:** (D) の出力は固定 reasonCode と件数だけで、絶対 path・SID・資格情報の内容を含まない。(D) は内部の .NET 例外をすべて catch し、inner を付けずに固定 code だけを出す（rename の失敗 message には絶対 path が入るため。移設スライスの D8 と同じ）。(D) は所有 role 配下の file の内容を開かない。(C) の正常出力には絶対 path が出うるので、smoke の生出力は local の Evidence にだけ置き、実 path・SID・秘密を tracked 文書と PR 本文へ転記しない（A2 R3 P3）。
  - **I6 consumer を変えない:** SHA B の `tools/` は SHA A（`34cf6607`）に対して §3 の新規 file だけが差分。`unity/` の差分は空。
- 成立条件（起きなければならないこと）:
  - **S1 offline 検証:** §5 の suite が temp base で通り、(D) が書いた印を、同じ temp base を seam に設定した (C) の `Get-OsmStoreRoot` が受け入れる。
  - **S2 移設:** 実ストアで (D) が `passed` を返し、移した role について移す前後の file 数と合計 size が一致し（reparse point をたどらず、前後で同じ関数で数える）、旧 root の所有 role 3 つがすべて tombstone になる。当初から無かった role は出力で `initiallyAbsent` として区別し、移したとは数えない（A2 R3 P1-2）。
  - **S3 smoke:** O1 手順 8 の smoke 項目 1・2・3・5・6 が、元の条件で (C) の worktree から非仮想化 pwsh で成立する。項目 4 は O10 による空状態の CLI / resolver の確認で代え、`harness.ps1 current` による実際の記録の読み戻しは未検証のまま残す（A2 R5 P1。元の項目 4 と同等の成立とは扱わない）。項目 2 は、旧 task が登録済みならその削除 → (C) の `schedule install` → `schedule status` が `passed`。項目 3 は、手順 3 より前に (A) で start し `status` で読めることを確かめた task `artifact-store-root-migration` を、(C) の `workflow-task.ps1 status` が exit 0 で返す。項目 4 は、(C) の `harness.ps1 status` が exit 0 で `taskはありません` を出す（**読む記録が無いことの確認**であり、記録の読戻しではない。旧 root に Harness が無く、合流もしない（O4）ので、新 root に読み戻す Harness の記録がそもそも無い。`status` は resolver を通って新 root の tasks の一覧を読むので、Harness が新 root で動くことは示せる。O10、A2 R4 P1-B）。項目 3・4 とも「resolver の 3 code が出ない」だけでは成立としない。項目 6 は、旧 task が `Disabled` で `Running` でない、または手順 3 の時点から `not-installed`。項目 7（packaged app 側）は、owner が packaged app の session を割り当てたときだけ記録し、無ければ `未実施` でよい。tombstone と LocalCache 側の directory が同じ名前で重なる view の見え方は未観測なので、`legacy-store-present` 以外が出ても欠陥にせず、観測として記録する。
  - **S4 既存記録:** 移設スライスの判定 C bundle と C' bundle を (C) の `fetch` で取得できる（移設スライスの Phase D の前提）。取得案内の reference が失われていた場合は成功と読み替えず、この条件の扱いを owner に返す（A2 R3 P3）。
  - **S5 機械検査:** `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` が error 0。I6 の `git diff`。

### A1: 受け入れ条件

- **AC-1（I1・I2・S1、offline）:** temp base で 3 role を移し、tombstone 3 つと印ができ、seam を設定した `Get-OsmStoreRoot` が新 root を返す。
- **AC-1b（S2、当初から無い role）:** 旧 root・新 root とも `Harness` が無く、他の 2 role がある。期待: 2 role を移し、`Harness` は新 root に作らず tombstone だけを置き、出力が `Harness` を `initiallyAbsent` とし、印ができて resolver が通る。旧 root の `Harness` がアクセス拒否・reparse のときは止まる。
- **AC-2（I1、失敗経路）:** rename に `ERROR_NOT_SAME_DEVICE` / `ERROR_ACCESS_DENIED` を注入すると、その role を動かさず、複写もせず、印を書かずに失敗する。同じ command の再実行で、移し済みの role は飛ばして残りを移す。rename の後・tombstone の前に失敗を注入した場合も、再実行で tombstone を作ってから先へ進む（A2 R3 P2-1）。tombstone の `CreateNew` の失敗では印を書かずに止まる。
- **AC-3（I2、失敗経路）:** 旧 root と新 root の両方に同じ role の directory がある（tombstone を消して旧コードが作り直した状態を含む）。期待: `role-split` で止まり、印を書かない。
- **AC-4（I3、失敗経路）:** 一時名の書込み・rename に失敗を注入。期待: `store-root.json` も一時名も残らない。
- **AC-5（I4、失敗経路）:** `Get-OsmStoreContext` の seam が `virtualized` / `unknown`。期待: 何も作らず動かさずに失敗する。
- **AC-6（I5）:** AC-1〜AC-5 の stdout・stderr に temp base の絶対 path と現在ユーザーの SID が現れない。rename の seam に絶対 path 入りの .NET 例外を投げさせても、出力は固定 code だけ。
- **AC-8（再実行、終了境界）:** (1) 有効な印があり、seam を設定した `Get-OsmStoreRoot` が新 root を返す状態で再実行。合格例: 何も変えずに `passed`（`already-migrated`）。不合格例: 印を書き直す、または `failed`。(2) 印が壊れている（0 byte、field 不足、`ownerSid` 不一致）、または印があって旧 root に directory の role がある。合格例: `store-root-precondition` で失敗し、印を直さず上書きしない。不合格例: `passed`（A2 R4 P2-A）。
- **AC-9（tombstone の境界）:** 旧 root の所有 role 名に size 0 でない通常 file がある。期待: `role-unknown` で止まり、tombstone と扱わない（A2 R4 P2-B）。
- **AC-7（S2〜S4、実ストア）:** §5「実ストアでの実行」の生出力が各手順の期待どおり。

### A1: ここでは答えない問いと所有する後続

- **LocalCache 側 copy を合流させる価値があるか**（O4 で owner が決定。価値が無ければ合流せずに copy を削除して終える）: program §6 の新しい 1 項目（下の「O4 の決定と program §6 の改め方」）。価値があると判断した単位だけが、`store plan` の再実行、owner の manifest 承認、`consolidate`、合流後の H1 適用 task の CURRENT・receipt の path bytes と新 root の両立の確認、`StoreConsolidation.psm1` の reasonCode の畳み込みの扱いへ進む。入口条件は本スライスの GO。
- **新しい host（旧 root の無い PC）での印の発行:** (D) は本スライスの PR の merge より前に削除する（O6）ので、移設後は印を書く手段がリポジトリに無くなる。別 PC で (C) 以降の CLI を使うには印の発行手段が要る: program §6「取得経路と配布」の入力。
- **Harness CURRENT の読み戻しは未実証（O10）:** 移設スライスの Phase D（harvest で移設後の現況を書くときに、Harness の記録の読み戻しを新 root で確かめていないことを残す）と、LocalCache 側 copy の後続（Harness の 4 task の CURRENT を合流させる価値の判断の入力）へ引き継ぐ。consumer の修正や fixture の新設は求めない。
- 旧 root に残る tombstone 3 つと親 directory の後始末: 上の LocalCache 側 copy の項目（削除を行う場合に同じ承認で扱う）。
- A0 案の他の項目（`hold` 単位の解消方式、旧 root 別名の廃止期限、移設スライスの C / C' の後続入力のうち本スライスに関わらないもの）はそのまま。

### A1: 判定定義と停止規則

- GO: I1〜I6 に違反が無く、S1〜S5 が成立し、判定 C と最終 C' に blocker が無い。S3 の成立は、smoke 項目 1・2・3・5・6 は元の条件で、項目 4 は O10 による空状態の確認で判定し、Harness CURRENT の読み戻しは未検証と記録する（A2 R5 P1）。CONDITIONAL ACCEPT は定義しない。
- 実ストアの実行は不可逆だが、作り直せるので、NO-GO でも rollback はしない。owner に返し、必要なら作り直す。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。実ストアの実行で (D) が `failed` を返したら、その出力を記録して owner に返す。

### A1: 未決事項 U1〜U11 への回答（owner の決定を反映）

- **U1:** 清掃 job の再登録は同じ問いに含める（smoke 項目 2 そのもので、印の後の (C) でしか行えない）。合流は分離（O4、決定済み）。
- **U2:** move だけ（I1）。新 root directory は (D) が作り、ACL は UserProfile からの継承のまま変えない（移設スライスは新 root を共有 directory とし、専用の ACL 境界は `Artifacts` 以下）。`Artifacts` と `Workflow` は継承無しの明示 ACE なので rename で変わらない。`Harness` は旧 root の ACL を継承しており、継承済みの ACE が残るが、(C) は `Harness` に private ACL を要求しないので受け入れる（A2 R1 F7）。ACL の照合はしない。
- **U3:** owner が手順 3 を行う。(1) Unity Editor と、develop を checkout した worktree・並行 session の store を使う作業を止め、新規実行も継続中の実行も無いことを確かめる。(2) 旧清掃 job を `Get-ScheduledTask -TaskPath '\OneStarMaker\' -TaskName 'OSM-Evidence-Cleanup-<repositoryId 先頭 12>'`（`EvidenceSchedule.psm1` と同じ表記）で名前まで特定して照会し、結果を 3 つに分ける（A2 R4 P1-A）。(a) 登録済みで `Running`: `Disable-ScheduledTask` → `Stop-ScheduledTask` → `Running` でなくなるまで待って確かめる。(b) 登録済みで `Running` でない: `Disable-ScheduledTask` → `Disabled` で `Running` でないことを確かめる。(c) 厳密な not found（`ObjectNotFound` かつ `CmdletizationQuery_NotFound*`。`EvidenceSchedule.psm1` と同じ判定）: `not-installed` と記録して続ける（disable も unregister も要らない）。それ以外の照会失敗、またはどれかの操作の失敗では移設に進まない（`Disable-ScheduledTask` は実行中の instance を止めないため。A2 R3 P1-1）。(D) 自身は process や Task Scheduler を照会しない。旧 job の action は旧 root の `Artifacts\runtime\<head>\` を指すので、rename の後は起動しても script が無く失敗する。実行中の instance が中の file を開いていれば rename が失敗する（`rename-failed` → 再実行）。旧 writer が rename の後に動いても、tombstone があるので旧コードの `CreateDirectory` が失敗し、旧 root に role は作られない（O8）。store を使う CLI が無い期間は手順 3 から手順 8 まで。
- **U4:** 実行の記録は (D) と smoke の生出力のテキスト 1 つだけにし、手順 8 の後に (C) の `evidence stage` で task `artifact-store-root-migration` に固定する。run directory は作らない。
- **U5:** 手順 3〜8 の実ストアの実行を高コスト証拠とし、判定 C → 暫定 C' → 実行 → 最終証拠ラウンドで進める（O9）。期待値は §5 の各手順の期待。照合 script は使わず、判定 C と最終 C' が生出力を読んで判断する。
- **U6:** (D) は role ごとに旧 root と新 root の状態の組で動作を決め（§4 の状態表）、再実行で前へ進む。移す前の記録は持たない。両方に directory がある、reparse・アクセス拒否・error のどれかなら止まり、owner に返す。
- **U7:** (A) で `workflow-task.ps1 start -Task artifact-store-root-migration` を行い、同じ (A) の `status -Task artifact-store-root-migration` で読めることを確かめる（設定が無いと start は記録が成立していても配送待ちで exit 2 を返しうるので、終了コードだけで失敗と断定しない。A2 R4 P3。本スライスの Phase C の Evidence の stage より前。smoke 項目 3 の読戻しの対象にもなる）→ 手順 3 → (D) の実行（4〜7）→ 手順 8（smoke 項目 1・5・6 → 旧 task の削除 → 項目 2 → 項目 3・4 → S4 の `fetch` → 項目 7 → 記録の stage）→ 最終証拠ラウンド（判定 C・最終 C'）→ 以降は O5 の順序。
- **U8:** 旧 runtime copy は `Artifacts` と一緒に移り、そのまま残す。
- **U9:** (D) は `tools/Workflow/` に置き、本スライスの PR の merge より前に削除する（O6）。PR の時期と merge の順序は O5。
- **U10:** (D) は `Get-OsmStoreContext`（I4）に加え、process の `LocalApplicationData` が既定位置と一致することを確かめ、違えば止まる。
- **U11:** 実ストアの操作は owner の一括承認で、範囲の制限は設けない（O7）。資格情報の登録・置換と R2 の操作は本スライスに不要なので行わない。秘密は出さない（I5）。

### A3: owner の決定（O4〜O9。2026-10-19）

- **O4 合流の分離:** 合流は別スライスにし、本スライスの対象外とする。後続の問いは「LocalCache 側 copy を合流させる価値があるか」で、価値が無ければ合流せずに copy を削除して終える。owner の原文の要旨:「合流は別スライスで良い。合流させる価値があるのかは問いたい。無ければ合流というか削除で良い」。反映先は下の「O4 の決定と program §6 の改め方」。
- **O5 PR の時期と merge の順序（owner の補足で置き換え。2026-10-19）:** 次の順に進める。owner の原文の要旨:「今すぐでなくてよい。このタスクが Phase C / C' まで終わって PR を出し、PR レビューで問題ないとできたら、#139 の merge 後にその PR を merge する」。
  1. 本スライスの Phase C / C' を終える。O9 により、最終 C' は実ストアの実行（手順 3〜8）の後。
  2. 本スライスの PR を開く（base は `develop`）。この時点で #139 は未 merge なので、本 branch の `develop` との差分には #139 の commit が含まれる。そのことを PR 本文に書く（本スライスの差分は `94f93930..<head>`）。
  3. PR レビューで問題が無いことを確かめる。
  4. Phase D の後始末の commit を作る: (D) の module・script・suite の削除（O6）、harvest、本書の削除。実装の判定に使った head（SHA B）は変えず、削除だけの commit として扱う。SHA B は push しない軽量 tag `evidence/artifact-store-root-migration/<n>` で到達可能に保つ。tag は push しないので、別環境からは追跡できない。PR 本文に SHA B の値と、local の Evidence（task `artifact-store-root-migration`）にその実行記録があることを書き、PR とローカルの Evidence の参照関係を残す（A2 R4 P3）。
  5. 後始末の commit を載せた head で、PR レビューをもう 1 回確かめる（merge する head がレビューされていない状態を作らないため）。
  6. owner が #139 を merge する（O1 手順 9。移設の後なので O1 の順序は変わらない）。repo の慣行（#134〜#138）どおり merge commit で入れれば、本スライスの PR の差分は #139 の merge の後に自然に本スライスの分だけになり、rebase も cherry-pick も要らない。squash で入れた場合は、本スライス独自の commit だけを載せ直し（rebase または cherry-pick）、内容が SHA B と同じことを `git diff` で確かめて記録する。実ストアの移行はやり直さない（A2 R3 P2-2）。
  7. owner が本スライスの PR を merge する。
  理由: owner 以外に作業者がいないため、#139 の merge を待たずに PR を開いてよい。
- **O6 (D) の寿命:** 残さない。本スライスの PR の merge より前（O5 の 4）に削除する（新しい host での印の発行は後続の入力。上の「ここでは答えない問い」）。
- **O7 実ストア操作の範囲:** 無制限（owner の一括承認）。理由: 実ストアに今使っている記録が無く、作り直せるため。U11 の承認方式（Decision ID と inventory の sha256）は採らない。
- **O8 旧 writer の再出現:** 推奨の (a) を採る。各 role の rename の直後に、(D) が旧 root にその role と同じ名前の空の通常 file（tombstone）を `CreateNew` で置く。resolver は同名の通常 file を不在と扱い（移設スライス AC-I3-d）、旧コードの `CreateDirectory` は同名 file で失敗するので、旧 root に role が作り直されない。
- **O9 O1 手順 2 の読み:** 案どおり。「Phase A〜C' を経て SHA B を固定」を、暫定 C' に blocker が無い head を SHA B とし、最終 C' は実ストアの実行の後とする。実行後に (D) の修正が要った場合は、実行した版を記録に残したまま Phase A の新しい revision にし、実行済みの手順はやり直さない。

- **O10 smoke 項目 4 の縮退（owner が 2026-10-19 に承認）:** O1 から転記した smoke 項目 4「`harness.ps1 current` で読める」を、「(C) の `harness.ps1 status` が exit 0 で `taskはありません` を出す（読む記録が無いことの確認）」に、実行の前に置き換える。これは元の項目 4 と同等の検証ではなく、**明示的な縮退**である。`status` は resolver を通って新 root の tasks の一覧を読むが、Harness の永続記録（CURRENT）を読めることは示さない。Harness CURRENT の読み戻しは未検証のまま、移設スライスの Phase D と LocalCache 側 copy の後続へ引き継ぐ（「ここでは答えない問い」）。理由: `harness.ps1 init` は `ApprovedSpecifications.psm1` が承認した 4 task と snapshot 本文の sha256 の厳密一致を要求し、進行役 session が git 履歴の全 `.md` blob（1,498 個、LF と CRLF の両方）を 4 つの承認値と照合して一致 0 件だったので、承認済み snapshot を Git から得られない見込みが高い。旧 root に Harness が無く、合流もしない（O4）ので、新 root に読み戻す Harness の記録はもともと無い。consumer の承認の allow-list は広げない（I6）。owner の承認: owner が 2026-10-19 に、元の smoke 項目 4 からの明示的な縮退として承認した（原文の要旨:「両方 OK」）。元の条件が達成されたとは扱わない（A2 R5 P1）。

### A3: O4 の決定と program §6 の改め方（凍結の commit で program §6 と `docs/README.md` に反映した）

`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §6 の 3 項目を次のように直す。owner の決定は文書の再開条件より優先するので、既存の再開条件（RC1・RC2）の書き換えも同じ commit で行う。

1. **「store root 移設の実行と合流」→「store root 移設の実行」:** 対象から LocalCache 側 copy の合流を除く。本スライス（`ARTIFACT_STORE_ROOT_MIGRATION.md`）の所有は、実ストアの新 root への移設、印の発行、清掃 job の再登録、smoke まで。「下の『分裂したストアの合流』の manifest 承認を含む」と「判断: owner（manifest の承認を含む）」の manifest の部分を消す。
2. **「分裂したストアの合流」と「Codex側残置の後始末」を 1 項目にまとめる:** 新しい項目名は「LocalCache 側 copy の扱い（合流の価値の判断と削除）」。
   - 問い: LocalCache 側 copy（packaged app が旧 root の `Artifacts` / `Workflow` / `Harness` として書いた記録）を新 root へ合流させる価値があるか。価値が無ければ合流せずに copy を削除して終える。価値があると判断した単位だけを、`store plan` の再実行 → owner の manifest 承認 → `consolidate` → 合流後の確認（H1 適用 task の CURRENT・receipt の path bytes と新 root の両立を含む）へ進める。
   - 価値の判断の入力（既存の記述から移す）: 2026-10-17 の `store plan` の分類（単位 20、`publish` 4・`hold` 16、deployment 単位 1 件の `conflict`）。Harness の 4 task（`h2c-unity-gate`、`h2c-unity-gate-r2`、`h1-transport-identity`、`artifact-evidence-lifecycle`）の CURRENT が copy 側にだけあり、`docs/README.md` の注記と program §4 の A の注記が「非仮想化 process から読めない」としていること。`hold` の解消方式（公開途中の不可視性）は、合流の価値があると判断した単位に必要な場合だけ設計スライスに分ける。Harness CURRENT の新 root での読み戻しが未実証であること（本スライスの O10）。tombstone を置いた後の packaged app の仮想化された view で、実体側の通常 file と LocalCache 側の directory が同じ名前で重なったときの見え方（未観測。本スライスの smoke 項目 7 の観測を入力にする）。名前表の `retirements-v1`、仮想化以外の path 不一致の診断、`StoreConsolidation.psm1` の reasonCode の畳み込み（移設スライスの C / C' の入力）は、合流に進む場合だけの入力として残す。
   - 削除の扱い: copy の削除は不可逆なので、対象（package と copy 内の role）・除外・開始条件をそのスライスの A3 で固定し、owner の承認で 1 回行う（Artifacts README の「一回の旧開発環境reset」と同じ種類の操作で、通常操作の事前承認に含めない）。削除の後に packaged app を Phase 担当にできる（移設スライス I3 の `legacy-store-present` が解ける）ことを、移設スライスが加えた入口の記述から引き継ぐ。Storage の 30 日清掃が copy に届かないこと、定期清掃 job の binding が copy を見ないことの記述は、削除で不要になるなら消す。
   - 入口条件: 本スライスの GO（印あり・旧 root に所有 role なし・smoke 合格）。旧 RC1・RC2 の再開条件は置き換える（owner が 2026-10-19 に後続として扱うと決めたため）。判断: owner（価値の判断、manifest の承認、削除の承認）。
3. **「取得経路と配布」に入力を 1 つ足す:** 本スライスの移行プログラムは PR の merge より前に削除する（O6）ので、旧 root の無い新しい host で印を発行する手段が無くなる。別 PC で store を使う作業の入口に、印の発行手段を加える。
4. **連動する公開面の記述:** `docs/README.md` の program 表と注記（合流前の `store plan`）、program §4 の A・E の注記は、凍結では書き換えず、本スライスの Phase D の harvest で現況（移設後・copy は未処理）に合わせる。凍結の commit では、`docs/README.md` の「進行中の単一スライス」の本スライスの説明から「LocalCache 側 copy を新 root へ合流する」を除いた。`tools/Artifacts/README.md`「実行するprocessの条件」の末段にある項目名「store root 移設の実行と合流」は、I6（README を変えない）のため Phase D の harvest で直す。

§1 の目的・対象外（A0 の入力）は書き換えない。§1 の「LocalCache 側 copy の削除・期限・後始末は対象外」は、この決定と一致する（削除は上の後続が持つ）。

## 3. 責務マップ

| file | 責務 | 所有者・寿命 | 依存 | テスト境界 | 行数 |
|---|---|---|---|---|---|
| `tools/Workflow/StoreRootMigration.psm1`（新規。PR の merge 前に削除。O6） | 一回の移設の本体。入口で seam 無しの `Get-OsmStoreRoot` の結果を 3 通りに分ける（§4。A2 R4 P2-A）、前提の確認（I4、U10、同一 volume）、role ごとの状態の分類（§4）、件数と合計 size、rename と tombstone、旧 root の 3 role が tombstone であることの確認、印の発行、`Get-OsmStoreRoot` が新 root を返すことの確認。内部の例外は固定 code に畳む（I5）。 | 一回限り。状態を持たない。 | `WindowsStoreRoot.psm1`（関数と public 型 `StoreRootNative`。A2 R1 F8）、`WindowsStoreContext.psm1`、`MoveFileExW`。domain module と `StoreConsolidation*` を import しない。 | private seam（`$script:` 変数: base、旧 base、store context、rename・書込みの失敗注入）。tests は module を import し、既存 suite と同じく `& (Get-Module ...) { $script:... }` で設定する。CLI 引数・環境変数からは設定できない（移設スライスの I4 と同じ理由）。 | 約 150 |
| `tools/Workflow/StoreRootMigration.ps1`（新規。PR の merge 前に削除。O6） | 薄い入口。module を import して本体を呼び、`{"status":...,"reasonCode":...}` と件数を出す。引数は取らない。 | 同上。 | 上の module。 | tests は入口を呼ばない（seam を設定できないため）。入口は判定 C が読んで確かめる。 | 約 20 |
| `tools/Workflow/tests/StoreRootMigration.Tests.ps1`（新規。PR の merge 前に削除。O6） | AC-1〜AC-6、AC-1b、AC-8、AC-9。 | テスト。 | 上の module、resolver の seam。 | 本体。 | 約 220 |

一回限りで PR の merge 前に消すので（O6）、policy・I/O・CLI の分割（A1 初稿）はしない。README は変えない（消すものを公開面に書かない）。

## 4. 実装計画

- role ごとの状態の分類（旧 root・新 root の組。照会は `StoreRootNative.Query`。A2 R3 P1-2・P2-1）:

  | 旧 root | 新 root | 動作 |
  |---|---|---|
  | directory | 不在 | 件数と size を数えて rename → tombstone を `CreateNew` → 新 root 側を数えて一致を確かめる |
  | 不在 | 不在 | 当初から無い。新 root には作らず tombstone だけ置き、`initiallyAbsent` と出す |
  | 不在 | directory | rename の後・tombstone の前に止まった。tombstone を作ってから次へ |
  | size 0 の通常 file（tombstone） | directory | 移し済み。次へ |
  | size 0 の通常 file（tombstone） | 不在 | 当初から無かった role の再実行。次へ |
  | size 0 でない通常 file | — | `role-unknown` で止まる（tombstone と扱わない。A2 R4 P2-B） |
  | directory | directory | `role-split` で止まる |
  | どちらかが reparse・アクセス拒否・error、または新 root 側が通常 file | — | `role-unknown` で止まる |

  3 role がすべて tombstone になったら印を発行する。
  「旧 不在・新 directory」を rename の後・tombstone の前に止まった状態と読むのは、移設の履歴を永続の記録で証明しない、この host の作り直せるストアとしての割り切りである（A2 R4 P2-B）。
- 入口の分岐（A2 R4 P2-A）: seam 無しの `Get-OsmStoreRoot` が (a) 新 root を返したら、何も変えずに `passed`（`already-migrated`）。(b) `store-not-migrated` なら、上の状態表で移設へ進む。(c) それ以外（`legacy-store-present`、`store-root-unavailable`、その他）は `store-root-precondition` で止まり、既存の印を直さず上書きしない（AC-8）。
- reasonCode: `store-root-precondition`（入口の分岐の (c)）、`virtualized-store`、`store-context-unknown`、`legacy-redirected`、`different-volume`、`role-split`、`role-unknown`、`rename-failed`、`count-mismatch`、`tombstone-failed`、`marker-write-failed`、`marker-not-accepted`、`migration-failed`。
- 件数と合計 size: 各実行の中で、rename の前に旧 root 側、後に新 root 側を数えて出す。reparse point はたどらない（`EnumerationOptions` の `AttributesToSkip = ReparsePoint`、`RecurseSubdirectories`）。前後で同じ関数を使う。再実行で移し済みの role は後の値だけを出す（S2 は初回の出力で確かめる）。記録の file は作らない。
- 順序: script と suite → `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- Phase B から A へ差し戻す条件: consumer の既存 file を変えないと成立しない（I6）、所有 role 配下の file の内容を開く必要が出た（I5）。
- 対象外の維持: Phase B と判定 C は実ストアに触れない（tests は temp base）。

## 5. テストとレビュー計画

- 単体テスト: `tools/Workflow/tests/StoreRootMigration.Tests.ps1`（`ac-1`、`ac-1b`、`ac-2`〜`ac-6`、`ac-8`、`ac-9`。すべて判定必須の `tools/Workflow/tests/*.Tests.ps1` に含まれる）。`Task.Delay` / `Thread.Sleep` を使わない。
- 差し戻し中の起点 `-Filter`: 同 suite と `tools/Workflow/tests/StoreRoot.Tests.ps1`（各 `-Case '*' -ResultPath <path>`）。
- 判定必須テスト（SHA B）: `tools/Workflow/tests/*.Tests.ps1` の全 file（各 `-Case '*' -ResultPath <path>`、executed 1 件以上・failed 0）、両 audit の error 0、I6 の `git diff --name-status 34cf6607 <SHA B> -- tools/ unity/`。`tools/Artifacts/tests` と `tools/Harness/tests` は、consumer の file が変わらない（I6）ので判定必須にしない。
- 全 EditMode 回帰の適用除外: 適用除外。理由: `unity/` を変えない。代替証拠: I6 の `git diff`。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路: 下の「実ストアでの実行」だけ（高コスト証拠）。動作確認（Phase B の後）は `対象なし`（offline の suite で閉じる）。
- 未知の操作経路: なし（rename・tombstone・印は offline の suite で temp base に対して確かめる。smoke の経路は移設スライスで疎通済み）。
- 人間の判断が必要な条件: なし。owner の行為は手順 3 の静止と、手順 8 の旧 task の削除。
- 高コスト証拠（実ストアでの実行。暫定 C' に blocker が無い SHA B で 1 回）:
  0. 前提: (A) で `workflow-task.ps1 start -Task artifact-store-root-migration` を行い、(A) の `status` で読めることを確かめ済み（U7）。
  1. owner: 手順 3（U3。旧 job を名前で特定し、照会結果を 3 つに分けて処置する。`not-installed` も記録する。それ以外の照会失敗では進まない）。
  2. 実行担当（非仮想化 pwsh、SHA B の clean worktree）: `pwsh tools/Workflow/StoreRootMigration.ps1`。期待: `passed`、移した role の前後の件数と合計 size が一致、`initiallyAbsent` の role が出力に明示される。`failed` なら止めて owner に返す。
  3. 実行担当（(C) = `34cf6607` の clean worktree、`tools/Artifacts` の Release build あり）:
     - 項目 1・5: `Get-OsmStoreRoot` が seam 無しで返り、`store-root.json` の handle の実体 path が論理 path と一致する（真偽値だけを出す）。
     - 項目 6: 旧 task が `Disabled` で `Running` でない、または手順 1 の時点から `not-installed`。
     - 項目 2: 対象名で既存 task を照会し、登録されている場合だけ owner が旧 task を `Unregister-ScheduledTask` し、担当が `pwsh tools/artifacts.ps1 evidence schedule install --profile osm --config <v2 config>` → `schedule status` が `passed`（config の所在は owner に確かめる。同名の task が残っていると install は `scheduler-identity-mismatch` で止まるので、削除が先）。
     - 項目 3: `pwsh tools/workflow-task.ps1 status -Task artifact-store-root-migration` が exit 0 でその task を返す。
     - 項目 4: `pwsh tools/harness.ps1 status` が exit 0 で `taskはありません` を出す（S3。読む記録が無いことの確認）。
     - S4: 移設スライスの 2 bundle の `fetch` が `passed`。
     - 項目 7: S3 のとおり。
  4. 生出力を 1 つのテキストにし、(C) の `evidence stage --profile osm --config <v2 config> --selection <selection file>` で task `artifact-store-root-migration` に固定する（selection file と config の所在、S4 に使う取得案内は手順 3 より前に確かめておく。値は tracked 文書に書かない）。生出力は local の Evidence にだけ置き、実 path・SID・秘密を tracked 文書と PR に転記しない（I5）。
- 判定 C の照合 script の方式: なし（生出力が短いので、判定 C と最終 C' が読んで判断する）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、I6 の `git diff`。
- 機械検査・H1: 不採用（移設スライスの D6 と同じ理由）。Phase A〜暫定 C' の Workflow / Evidence は (A)（develop checkout の CLI）で記録し、実行の後は (C) で行う。
- A0/A1 主担当・モデル・ベンダー: A0 = owner と Phase A 補助 session。A1 = Phase A 主担当 session（Claude Opus 5.5、Anthropic）。
- A2 の観点（実施済み）: R1 アーキテクチャゲートと不変条件の反証（実行可能性を併せた）、R2 A0 だけから代替構成。
- A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版（2026-10-19）:
  - 入力版（R1）: A2 入力版。内容 sha256（commit の blob、LF）`232cda5c761ede6d48f97f66b0b0b8c449b4ed77b92645e4c7494013ce3ecbe2`。補助: commit `0dd3bd6e`。
  - 入力版（R2）: A0 固定版（commit `a016d02a` の `git archive` の展開。本書は §3〜§5 が空で、初稿を含まない）。
  - **R1**（上記の観点 R1 に R3 の実行可能性を併せた）: 主担当 session が起動した新規 subagent（Claude 系、Anthropic。A1 と同じ系列。独立性: 初稿を入力とし、R2 の指摘は見ていない）。読取専用、実ストアに触れない。指摘 F1〜F13。原文の要旨は主担当 session の共有ノート。
  - **R2**（初稿を見ず、A0 だけから代替構成）: codex CLI 0.159.2 の非対話実行（GPT 系 `gpt-6-luna`、OpenAI、reasoning effort は既定の low、read-only sandbox）。展開した A0 commit だけを読み、現行の作業 tree は渡していない。原文は主担当 session の共有ノート。
  - 採否（A3 の統合。主担当が決めた。owner の判断待ちの事項は O4〜O9 に残し、ここでは決めていない。O4 はその後 owner が決定した）:
    | 指摘 | 採否 | 反映先・理由 |
    |---|---|---|
    | R1 F1（仮想化判定が無く、packaged app 内の実行で LocalCache の copy を移しうる） | 採用 | P8（`Get-OsmStoreContext` = `not-virtualized`）、U10、AC-I1-a、§3 の依存。既存 module の import だけで consumer は変わらない。 |
    | R1 F2（承認 hash が resume と矛盾。hash の対象が未定義） | 採用 | I1 を「現況 inventory の正規形の hash」に。§4「正規形と承認 hash」。AC-I1-d。 |
    | R1 F3（`RevisionLocks` の出現・非所有の変化で rename 後に行き止まり） | 採用 | I2 (c) を最上位の名前・種別・FileId に絞り、`RevisionLocks` を entry ごと除外。`plan -Rebase`（分類が健全なときだけ、owner の再承認で続ける）。AC-I2-b。 |
    | R1 F4（rename 後の旧 writer の再出現を回復できない） | 一部採用・保留 | 状態表に `reappeared` を足し、用語の節の「(D) は作らない」を「検出して止まる」に直した。tombstone 案は旧 root への書込みを加える（I5・I6・U11 の改訂）ので O8 で owner の判断待ち。 |
    | R1 F5（昇格 process の command line が読めない、対話 host 内の実行が見えない） | 採用 | P5 を fail-closed に、`pwsh` の件数と owner の確認を P7 の要件に。U3、V-M1 に昇格 pwsh の観測。 |
    | R1 F6（P2 が印の発行後の resume と矛盾、古い一時名で行き止まり） | 採用 | P2 の許容集合、I3 の一時名の規則と同じ runId の残りの削除、AC-I3-c。 |
    | R1 F7（`Harness` は継承 ACL。U2(4) の記述が誤り） | 採用 | U2 を書き直し、inventory に `AreAccessRulesProtected` と旧 root の DACL。新 root の ACL は継承のまま（下の R2 U2 と併せて主担当が決めた）。 |
    | R1 F8（resolver の public 型を使える。複製は型名衝突。role 名の所在の数え方） | 採用 | §3 の I/O 行・M5 を public 型の呼出しに。ゲート照合 1・3・7、`owned-role-names-match-resolver` の test。 |
    | R1 F9（O1 手順 2 の読み替えが owner 判断の一覧に無い） | 採用（判断は保留） | U5 に明記し、O9 で owner の判断待ち。 |
    | R1 F10（Decision ID の流れの抜け） | 採用 | `execute` が宣言 ID と承認 ID を両方記録し別値を要求（`approval-reused`）、3 つ目の Decision は smoke のテキストへ。E8。 |
    | R1 F11（SID の置換が不十分、E7 が host 依存） | 採用 | I7（SDDL は owner と DACL、well-known 以外は salt 付き hash の token）、E7 の正規表現、`sid-tokenized`。 |
    | R1 F12（状態表の通常 file・reparse・denied、旧 root の無い host の P4、P6 の not-found、実行中 instance） | 採用 | 状態表の 3 行、M8、P6 の厳密判定と instance、U3。 |
    | R1 F13（AC-S4-r 項目 4 の期待、config の所在） | 採用 | AC-S4-r（期待値を Phase B が offline で観測して固定）、V-R2 項目 2 に所在の確認。 |
    | R2 高「問いが広すぎる」・U1・U7 | 一部採用 | 合流を分ける点は初稿と一致（O4）。清掃 job の再登録も分ける点は不採用: 再登録は smoke 項目 2（A0 で固定した smoke の一部）そのもので、印の後の (C) でしか行えず、移設の成否の観測に含まれる。 |
    | R2 高「不変条件に観測可能な合否が無い」 | 採用（初稿と一致） | A0 の候補への指摘。A1 で不変条件・成立条件と受け入れ条件に分けた。 |
    | R2 高 U3・U6（静止確認と部分移設の回復が未設計） | 採用（初稿と一致） | U3・状態表・resume。R2 の「owner が継続か手動復旧かを判断」は、曖昧な状態で止めて owner に返す本案と同じ。 |
    | R2 U2（新 root と各 role に ACL を明示設定する） | 不採用 | 移設スライスは新 root を共有 directory とし（`Assert-SharedParent` は存在と reparse だけ）、専用の ACL 境界を `Artifacts` 以下に置く。(D) が ACL を変えると I4 の SDDL 一致の観測とも食い違う。ACL の照合は採用（R1 F7）。 |
    | R2 U4（作業用 copy の削除は Evidence 保存の確認後） | 採用（初稿と一致） | run directory は消さず、後始末は Phase D。 |
    | R2 U5（期待値の事前固定、独立担当の照合、fixture の先行） | 採用（初稿と一致） | §5「高コスト証拠」「照合 script の方式」。 |
    | R2 U8（旧 runtime copy は保持） | 採用（初稿と一致） | U8。 |
    | R2 U9（一回限りで恒久 CLI として残さない、`tools/Artifacts` に置く） | 不採用（寿命は保留） | 置き場所は依存（resolver の OS 基盤層、domain に依存しない）から `tools/Workflow`。寿命は O6 で owner の判断待ち（新しい host の初期化に要るという本案の理由と、R2 の一回限りの案を併記）。 |
    | R2 U10（旧 root の redirect と reparse の確認） | 採用 | P3 に旧 root とその祖先の reparse を追加。 |
    | R2 U11（通常操作の承認範囲外として A3 で固定） | 採用（初稿と一致） | O7（owner の判断待ち）。 |
    | R2「入口条件と O1 手順 9 の衝突」 | 不採用（誤読） | A0 の入口条件（移設スライスの merge の前に手順 3〜8）と O1 手順 9（手順 8 の後に merge）は同じ順序を述べており、衝突しない。 |
    | R2「ACL の照合粒度が A0 で不明」 | 採用 | R1 F7 と同じ反映。 |
- O5〜O9 の決定による簡素化（2026-10-19。owner の「厳密にやる意味を感じない」と O7 の理由による）で、上の採用分のうち次の反映を本文から削った: R1 F2・F3（承認 hash の正規形と `plan -Rebase`。承認の hash 自体をやめた）、F5・F12 の P5・P6（process と Task Scheduler の照会。owner の手順 3 と tombstone で代える）、F6 の一時名の規則の細部、F7 の ACL の記録と照合、F10（Decision ID の流れ）、F11（SID の token 化。記録に SID を書かないので不要）、F13 の項目 4 の期待値の事前観測。A1 初稿の policy / I/O / CLI の 3 分割、FileId・SDDL の照合、run directory、照合 script と合成 fixture、動作確認 V-M1 も削った。残したもの: F1（仮想化判定、I4）、F4（tombstone、O8）、F8（resolver の public 型）、F9（O9）。
- **R3**（A2 の追加。2026-10-19）: owner が取得した外部レビュー（GPT 系の Web サービス、OpenAI。GPT 系の 2 件目）。入力: owner の O4〜O9 を反映した未 commit の A3 統合版の全文と、GitHub 上の #139 のコード（静的照合のみ）。原文は owner の手元の file。判定は「方針は支持。凍結の前に P1 の 3 件を閉じる」。
- **事前チェック**（2026-10-19）: Phase A の進行役 session が、Phase C / C' で指摘されそうな点を本文とコードで見直したもの（11 項目。R3 との重なりは下に併記）。
- R3 と事前チェックの採否（主担当が決めた）:
  | 指摘 | 採否 | 反映先・理由 |
  |---|---|---|
  | R3 P1-1・事前 3（disable は実行中の instance を止めない） | 採用（小さく） | U3 の (2)(3)、smoke 項目 6 に `Running` でないこと。(D) は照会しない（owner の手順の gate）。旧 job の action が旧 root を指す事実と、unregister が install より先の理由を併記。 |
  | R3 P1-2・事前 1（当初から無い `Harness` で `role-missing` か tombstone の欠け） | 採用 | §4 の状態表（旧・新の組）で置き換え、`role-missing` と「移す前の記録」を廃止。当初から無い role にも tombstone を置き `initiallyAbsent` と出す。I2、S2、AC-1b。 |
  | R3 P1-3・事前 2（smoke 3・4 が読戻しを証明しない） | 採用 | S3 と §5 の手順 3: 項目 3 は (A) で start 済みの task を exit 0 で返す、項目 4 は新 root で fixture の `init` → `current`。U7 に (A) の start を置いた（Phase C の Evidence の stage にも要る）。 |
  | R3 P2-1（rename の後・tombstone の前の resume） | 採用 | 状態表の「旧 不在・新 directory」行、AC-2。 |
  | R3 P2-2・事前 7（#139 の merge 方式と PR の差分） | 採用（簡単な形） | O5 の 6: repo の慣行どおり merge commit なら rebase も cherry-pick も要らない。squash の場合だけ載せ直して `git diff` で同一性を記録。SHA B は軽量 tag で保つ。 |
  | R3 P2-3・事前 11（A0 の scope 表示と A3 の食い違い） | 採用 | 冒頭に「有効な scope」の 1 行。 |
  | R3 P3（正常出力の絶対 path、`evidence stage` の引数、新しい host の制限、S4 の扱い） | 採用 | I5 に正常出力の扱い、§5 の手順 4 に `--selection` と事前の所在確認、S4 に reference 喪失時は owner に返す。新しい host の制限は既に後続の入力。 |
  | 事前 4（seam を子 process の script 変数で設定できない） | 採用 | §3 を module 本体 + 薄い入口に。tests は module scope で seam を設定（既存 suite の方式）。 |
  | 事前 5（.NET 例外の message に絶対 path） | 採用 | I5、AC-6。 |
  | 事前 6（件数で reparse をたどらない） | 採用 | §4 の件数と合計 size。 |
  | 事前 8（後始末の commit がレビューの後にあり、merge する head が未レビュー） | 採用 | O5 の 4・5: 後始末の commit を載せた head で PR レビューをもう 1 回確かめる。 |
  | 事前 9（tombstone と LocalCache の directory が重なる view の見え方は未観測） | 採用 | S3 の項目 7 は `legacy-store-present` 以外でも欠陥にしない。§2「O4 の決定と program §6 の改め方」の後続の入力に追加。 |
  | 事前 10（印の後の再実行が `store-root-precondition`） | 採用 | `already-migrated` で `passed`（§4、AC-8）。 |
- **R4**（A2 の追加。2026-10-19）: owner が取得した外部レビュー（GPT 系の Web サービス、OpenAI。GPT 系の 3 件目。R3 と同じ担当の再レビュー）。入力: R3 と事前チェックを反映した未 commit の統合版と、#139 のコード（静的照合のみ）。原文は owner の手元の file。判定は「R3 の P1 3 件は解消。凍結の前に新しい P1 2 件を閉じる」。
- R4 の採否（主担当が決めた）:
  | 指摘 | 採否 | 反映先・理由 |
  |---|---|---|
  | P1-A（旧 task が未登録の場合が未分類、TaskPath の表記） | 採用 | U3 の (a)〜(c)、S3 の項目 6 の合格条件、§5 の手順 1 と項目 2。表記は `'\OneStarMaker\'` と task 名まで。 |
  | P1-B（`harness.ps1 init` が承認済み snapshot の sha256 の厳密一致を要求する） | 採用（smoke の方法を変える） | 承認済み snapshot は Git から得られない見込み（上の O10 の理由）。smoke 項目 4 を `harness.ps1 status` の `taskはありません` に、実行の前に置き換えた（O10）。allow-list は広げない。 |
  | P2-A（印ありの再実行と入口の分岐の不一致） | 採用 | §4「入口の分岐」、§3、AC-8 に破損した印の case。 |
  | P2-B（tombstone を任意の通常 file と区別しない） | 採用 | 状態表で size 0 だけを tombstone とし、それ以外は `role-unknown`。「旧 不在・新 directory」の読みが割り切りであることを明記。AC-9。 |
  | P3（U3 の path の改行、start の exit 2、tag の追跡性） | 採用 | U3 の path と TaskPath の表記を直した（前回の置換で崩れていた）。U7・§5 の手順 0 に (A) の `status` での確認。O5 の 4 に PR と local の Evidence の参照関係。 |
- **R5**（A2 の追加。2026-10-19）: owner が取得した外部レビュー（GPT 系の Web サービス、OpenAI。GPT 系の 4 件目。R3・R4 と同じ担当の 3 回目）。入力: R4 を反映した未 commit の統合版と、#139 のコード（静的照合のみ）。判定は「A3 凍結に進める水準。小修正を凍結版に入れることを推奨」。owner のコメントの要旨:「だいたいいい」。
- R5 の採否（主担当が決めた）:
  | 指摘 | 採否 | 反映先・理由 |
  |---|---|---|
  | P1（O10 を元の smoke 項目 4 と同等の成立と書かない） | 採用 | S3 と判定定義を「1・2・3・5・6 は元の条件、4 は空状態の確認、`current` の読み戻しは未検証」に。O10 を縮退として owner の承認待ちの欄に。未実証を移設スライスの Phase D と LocalCache の後続へ。 |
  | P2（AC-9 が suite と単体テストの列挙に無い） | 採用 | §3 の suite の責務欄と §5 の単体テストに `ac-9`。判定必須の範囲に含まれることを明記。 |
  | P3（手順 4 の `harness.ps1 current` の注記が古い） | 採用 | 手順 4 と I5 の理由を「(C) の正常出力には絶対 path が出うる」一般の規則に。 |
- A3 統合担当・モデル・採否: Phase A 主担当 session（Claude Opus 5.5、Anthropic）。2026-10-19。owner の指示（主担当の判断で A3 まで進め、凍結はしない）により採否を主担当が決め、その後 owner が O4〜O9 を決め、R3・事前チェック・R4・R5 の採否も主担当が決めて本文へ反映した。**未凍結**。凍結（`A3 freeze` の commit）は owner の凍結指示の後に行う。統合版は未 commit の作業ツリーにある（A2 入力版 `0dd3bd6e` からの差分）。
- 独立性の強化条件を満たせない場合の理由: R1 は A1 と同じ系列（別 session・初稿のみ入力）。R2 は別ベンダーで初稿を見ていない。Phase A では Claude 系と GPT 系だけを使い、C' のためにそれ以外の系列を残した。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
