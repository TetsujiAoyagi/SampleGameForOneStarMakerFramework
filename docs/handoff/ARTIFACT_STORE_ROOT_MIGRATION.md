# Artifact Storage: store root 移設の実行と合流

`ARTIFACT_STORE_ROOT_RELOCATION.md`（以下「移設スライス」）が GO 候補 head で用意した consumer（新 root を読む resolver と 3 CLI）が動ける状態を、実ストアで一回だけ作る運用スライス。旧 root の所有 role を新 root へ移し、移行済みの印を発行し、LocalCache 側 copy を新 root へ合流し、清掃 job を新 root で再登録する。移設スライス §2 O1 の工程表の手順 2〜8 を担い、手順 9（移設スライスの merge）の前提を作る。`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §6 の「store root 移設の実行と合流」と、同 §6 の「分裂したストアの合流」のうち合流の実行を受け持つ。

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A1 初稿 = A2 入力版。A2 未着手）
- branch: `claude/artifact-store-root-migration`（移設スライスの branch `claude/artifact-store-root-relocation` の `94f93930` から切った。`34cf6607..94f93930` の差分は `ARTIFACT_STORE_ROOT_RELOCATION.md` だけで、コードは移設スライスの GO 候補 head `34cf6607` と同一。PR base は `develop`）
- implementation base commit: `94f939303951c8ec0614303d66525bb9abcbacec`（コードは `34cf6607302d27f49bf9d3febaaa31d624f5cbed` と同一）
- implementation head commit: 未固定
- risk: `high`（実ストアの不可逆な移設、資格情報 directory の移動、旧 writer の静止と清掃 job の停止・再登録、owner 承認の manifest による合流、移設中は store を使う CLI が無い期間を作る）
- owner: OSM maintainers（A3 の凍結、手順 3・5 の承認、合流 manifest の承認、Phase D の判断は owner）
- created: 2026-10-19
- expires: 2026-12-19 または置換 revision
- harvest to: `tools/Artifacts/README.md`（移設の完了後の所在と、合流の結果）、`tools/Workflow/README.md`・`tools/Harness/README.md`（必要な場合）、`docs/README.md` の program 表、`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4・§6
- Phase A snapshot の取得案内・状態: Git 上の本文で完結（A0 = commit `a016d02a`（message に `A0`）。A2 入力版 = message に `A1` と `A2 input` を含む commit で、その commit SHA と内容 sha256 は A2 の担当が §5 に記録する）
- Phase B result snapshot の取得案内・状態: 未到達
- evidence bundle の取得案内・状態: 未到達
- C' blind bundle の取得案内・状態: 未到達
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A2（独立レビュー）。進行は Phase A 主担当。
  - 次の作業: 下の「Phase A の着手手順」5 に従い、A2 入力版（この branch の先頭 commit）の commit SHA と内容 sha256 を §5 に記録し、§5「A2 の観点」の各レビューを同じ入力版で独立に依頼する。互いの指摘を見せない。A0 だけから代替構成を出すレビューには A0 の commit `a016d02a` の本書を渡し、初稿を渡さない。C' 用に Phase A で使わない系列を残す。
  - 引き継ぐ commit SHA: この branch の先頭（A2 入力版の commit）。作業ツリーが dirty なら引き継がない。
  - 取得案内の所在: なし（Phase A は Git 上の本文だけ）。
  - 必要な権限: リポジトリの読み、pwsh 7。A2 で実ストア・LocalCache・資格情報・Task Scheduler・R2 を変更しない。
  - 停止規則: A2 の指摘が移設スライスの consumer コード（`34cf6607`）、印の形式（O2）、O1 工程表の変更を要する場合は、採否を決めずに owner へ返す。A2 の結果の記録は commit せず作業ツリーに置き、A3 凍結版で 1 回だけ commit する。
  - 引き渡さないもの: ローカル store・LocalCache の実 path、資格情報。A2 の各レビューに他のレビューの指摘。

### Phase A の着手手順

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

### A1: 用語と時点

- **旧 root** = `<LocalApplicationData の既定位置>\OneStarMaker`（移設スライスの `Get-OsmLegacyStoreRoot`）。**新 root** = `<UserProfile>\OneStarMaker`。**所有 role** = `Artifacts`、`Workflow`、`Harness` の 3 directory。**非所有 entry** = 旧 root 配下のうち所有 role の配下でない entry（直下の `RevisionLocks`、`H2a Review` 等の not-owned directory とその配下）。
- **run directory** = (D) が 1 回の移設ごとに作る記録の置き場 `<UserProfile>\OneStarMaker.Migration.<runId>`（runId は 32 桁 hex。新 root・旧 root のどちらの配下でもない。U4）。
- 時点: **移行前** = 印なし・新 root なし・旧 root に所有 role あり。**移行中** = 印なし・新 root あり（所有 role の一部または全部を移した）。**移行後** = 印あり・旧 root に所有 role なし。移設スライスの「移行中」（印あり・旧 root に所有 role あり）は本スライスの (D) が作らない状態で、作った場合は I2 の違反。
- **承認 inventory** = 手順 4 で (D) が書き、その sha256 を owner が承認した inventory file。**移設後 inventory** = 手順 5 の後に (D) が取り直したもの。

### A1: このスライスが答える問い（確定案）

(D) を SHA B に固定し、O1 手順 3〜8 を実ストアで一回実行したとき、旧 writer を静止したまま、所有 role の全 entry を bytes と file identity を保って新 root へ移し、印を発行し、(C) の production smoke test（清掃 job の新 root での再登録を含む）が通る状態に到達できるか。

A0 案の後半のうち、清掃 job の再登録は smoke 項目 2（Evidence binding の再生成と検証）そのものなので同じ問いに含める。LocalCache 側 copy の合流は別の問いに分け、後続へ送る（U1。A3 で owner が決める O4）。

### A1: 進める最低条件

- 不変条件（起きてはいけないこと）:
  - **I1 静止の確認前に動かさない:** (D) の `execute` は、同じ呼出しの中で取り直した preflight の観測 P1〜P7（§4「preflight」）がすべて期待どおりで、かつ取り直した旧 root の inventory の sha256 が承認 inventory の sha256 と一致したときだけ、最初の directory を作る・動かす。どれか 1 つでも外れれば、旧 root・新 root のどちらにも entry を作らず動かさずに失敗する（run directory への記録だけを行う）。resume（§4「状態の分類と resume」）も同じ検査を毎回通る。
  - **I2 照合前に印を書かない:** 印は、同じ `execute` の中で次の 3 つがすべて成立した後にだけ書く。(a) 承認 inventory と移設後 inventory が所有 role の全 entry で一致する（§4「inventory と照合」）。(b) 旧 root の所有 role 3 つの不在を、移設スライスの D9 と同じ意味（zero-access の open で `ERROR_FILE_NOT_FOUND` / `ERROR_PATH_NOT_FOUND`、または同名の通常 file）で証明できる。(c) 旧 root の非所有 entry が、`RevisionLocks` の配下を除いて承認 inventory と一致する。
  - **I3 印の部分状態と失敗後の残存を作らない:** 印の bytes は新 root 直下の一時名に `CreateNew` で書いて flush してから、上書きしない rename で `store-root.json` に現す。書込み・flush・rename のどれかが失敗したら、自分が作った一時名の file だけを消し、`store-root.json` が無い状態で失敗する。`store-root.json` が既にあれば上書きせず失敗する。
  - **I4 記録の bytes と identity を変えない:** (D) は所有 role 配下の file を内容の読み書きのために開かず、複写しない。移設は同一 volume の directory rename（`MoveFileExW`、`MOVEFILE_COPY_ALLOWED` と `MOVEFILE_REPLACE_EXISTING` を付けない）だけで行う。別 volume・既存の移動先・rename 失敗では複写に切り替えず止まる。観測: 承認 inventory と移設後 inventory で、所有 role の各 entry の FileId・size・更新時刻・属性・SDDL が一致する（FileId が同じなら同じ実体であり、複写ではない）。
  - **I5 対象外に触れない:** (D) は旧 root の親 directory、`RevisionLocks`、非所有 entry を作らず、動かさず、消さず、内容を開かない。`<LocalApplicationData>\Packages`（LocalCache 側 copy）を開かず列挙もしない。`Artifacts\credentials` 配下の file は metadata（属性・size・時刻・FileId・SDDL）だけを読み、内容を開かない。Task Scheduler・R2・資格情報 store を変更しない（Task Scheduler は読取の照会だけ）。
  - **I6 逆向きの操作をしない:** (D) は、印の削除、新 root から旧 root への move、旧 root の所有 role の作成、Task Scheduler の task の有効化・登録・削除を行う経路を持たない。失敗時は止まって記録を残すだけで、自動 rollback をしない。本書の手順も (A) の再開と (B) の再有効化を指示しない。
  - **I7 秘密と path を出さない:** (D) の stdout・stderr・run directory の記録は、root を token（`<legacy>`、`<new>`、`<run>`）で表した相対 path、固定 reasonCode、件数、hash、真偽値だけを持つ。絶対 path、SID（SDDL 中の現在ユーザーの SID は token `<current-user>` に置き換える）、他 process の command line、資格情報の内容・鍵・header・署名 URL を含めない。内部の .NET 例外は固定 code に畳む（移設スライスの D8 と同じ）。
  - **I8 合流と移設を混ぜない:** このスライスは `store plan` と `store consolidate`（dry-run を含む）を実行しない。`store consolidate` を旧 root から新 root への移設に使わない。
  - **I9 consumer を変えない:** SHA B の `tools/` は、SHA A（`34cf6607`）に対して、§3 の新規 file と README の追記だけが差分である。`unity/` の差分は空。
- 成立条件（起きなければならないこと）:
  - **S1 (D) の offline 検証:** 新規 suite（§5）が temp base で、成功、resume、各 preflight の失敗、照合不一致、印の発行失敗、旧 root の無い host の初期化を通し、(D) が書いた印を、同じ temp base を seam に設定した (C) の `Get-OsmStoreRoot` が受け入れて root を返す。
  - **S2 実ストアの移設照合:** 実ストアの run の記録で、承認 inventory と移設後 inventory が所有 role の全 entry について一致し（相対名・種別・属性・size・更新時刻・FileId・SDDL）、旧 root の所有 role 3 つが不在で、非所有 entry が `RevisionLocks` 配下を除いて一致する。
  - **S3 印の発行:** 新 root 直下に `store-root.json` が O2 の形式（4 field ちょうど、`schemaVersion` 1、`kind` `osm-store-root`、`ownerSid` が現在ユーザー、`createdAt` ISO 8601、UTF-8 BOM 無し、4 KiB 以下）であり、新 root 直下に (D) の一時名の file が無く、seam 無しの `Get-OsmStoreRoot` が新 root を返す。
  - **S4 smoke:** O1 手順 8 の smoke 項目 1〜6 が、(C) の worktree から非仮想化 pwsh で成立し、項目 7 が packaged app 側で `legacy-store-present` として記録される。項目 2 は清掃 job の再登録（owner が旧 task を削除し、(C) の `schedule install` の後に `schedule status` が `passed`）で行う。
  - **S5 既存記録の読戻し:** 移設スライスの Phase C / C' の Evidence（2 bundle）を (C) の `fetch` で取得でき、全 file の hash が取得案内と一致する。本スライスの task `artifact-store-root-migration` を (C) の `workflow-task.ps1 status` で読める。
  - **S6 機械検査:** `tools/Artifacts/tests`、`tools/Workflow/tests`、`tools/Harness/tests` の全 suite が各 1 件以上実行・failed 0 で合格し、`pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` が error 0。I9 の `git diff`。

### A1: 受け入れ条件

検証する条件を見出しに併記する。AC-*-o は offline（S1 の suite）、AC-*-r は実ストアの run の記録（高コスト証拠。§5）で確かめる。

- **AC-I1-a（I1、失敗経路、offline）:** P1〜P7 を 1 つずつ外した 7 case（P1: seam で `Get-OsmStoreRoot` が `store-root-unavailable`、P2: 新 root に未知の file、P3: redirect の seam、P4: 別 volume の seam、P5: process 照会の seam が一致 1 件、P6: task が `Ready`、P7: 宣言なし）。期待: `execute` が固定 code で失敗し、旧 root と新 root の親の tree 一覧が前後で一致（run directory の追加だけを許す）。
- **AC-I1-b（I1、失敗経路、offline）:** plan の後に旧 root の `Workflow` 配下へ file を 1 つ足し、承認 inventory の sha256 で `execute`。期待: `inventory-changed` で失敗し、何も作らず動かさない。差分（相対名と変化した field 名）が run directory の `execute-*.json` に残る。
- **AC-I1-c（I1、合格例・不合格例、時点）:** owner の承認より後に P6 が崩れた（task が再び有効）。合格例: `execute` の preflight で止まる。不合格例: plan 時点の preflight を使って移す。
- **AC-I2-a（I2、失敗経路、offline）:** 照合の seam で移設後 inventory の 1 entry の size を変える／旧 root に `Harness` directory を残す（rename の seam で 1 role だけ失敗させる）／非所有 entry を 1 つ消す。期待: いずれも `store-root.json` が作られず、`execute` が `compare-mismatch` / `legacy-role-present` / `nonowned-changed` で失敗する（または partial。下の AC-I6-a）。
- **AC-I2-b（I2、合格例、名前の境界）:** 旧 root に `Workflow` という名の通常 file と `H2a Review` directory と `RevisionLocks` が残る。期待: 所有 role は不在と判定され印が書かれる。`RevisionLocks` 配下の変化（execute 中に file を足す seam）は不一致にしない。
- **AC-I2-c（I2、失敗経路、不在を証明できない）:** 旧 root の所有 role 名に対する照会で、アクセス拒否・reparse・未知の Win32 error を seam で注入。期待: 印を書かず `legacy-query-failed` で失敗。
- **AC-I3-a（I3、失敗経路、offline）:** 一時名の書込み・flush・rename のそれぞれで失敗を注入。期待: 新 root 直下に `store-root.json` も一時名の file も無い。
- **AC-I3-b（I3、合格例・不合格例、終了境界）:** `store-root.json` が既にある新 root（resume で印の発行後に止まった場合を含む）。合格例: 上書きせず、既存の印が O2 の形式で現在ユーザーのものなら照合だけ行って `passed`（`already-issued`）、そうでなければ `marker-conflict` で失敗。不合格例: 書き直す、または消す。
- **AC-I4-a（I4、offline）:** temp base で移設した後、所有 role の全 entry の FileId・size・更新時刻・属性・SDDL が前後で一致する。複写の seam（rename の代わりに copy した fixture）を照合に通すと FileId の不一致で `compare-mismatch` になる。
- **AC-I4-b（I4、失敗経路）:** rename に `ERROR_NOT_SAME_DEVICE` / `ERROR_ALREADY_EXISTS` / `ERROR_ACCESS_DENIED`（所有 role 配下の file を別 handle で開いたまま）を返させる。期待: 複写に切り替えず、その role を動かさずに失敗し、他 role の状態は記録どおり（partial）。
- **AC-I4-r（I4、実ストア）:** 実 run の承認 inventory と移設後 inventory の所有 role の全 entry で FileId が一致する（判定 C の照合 script が確かめる）。
- **AC-I5-a（I5、offline）:** fixture の旧 root に `RevisionLocks`、`H2a Review`、`Artifacts\credentials\<dummy>`、LocalCache の代役 `<legacy の親>\Packages\P\LocalCache\Local\OneStarMaker\Workflow` を置く。期待: 非所有 entry と LocalCache の代役の tree 一覧が前後で一致し、I/O の記録 seam に、`credentials` 配下の file への read / write access の open と `Packages` 配下の列挙が無い。
- **AC-I6-a（I6、失敗経路、partial）:** 3 role のうち 2 つを移した後に 3 つ目の rename が失敗。期待: 旧 root に戻す move が起きず、印が無く、出力は `partial`（exit 2）。同じ `execute` を再実行すると、移した 2 role は `already-moved` として承認 inventory と照合され、3 つ目を移して完了する。
- **AC-I6-b（I6、機械検査）:** (D) の source に `Enable-ScheduledTask`、`Register-ScheduledTask`、`Unregister-ScheduledTask`、印の file の削除、新 root から旧 root への move の呼出しが無い（判定 C の grep。意味判定は C が行う）。
- **AC-I7-a（I7、offline）:** 各失敗経路の stdout・stderr と run directory の全 file に、temp base の絶対 path、現在ユーザーの SID、seam で注入した例外の message、fixture の process command line の文字列が現れない。
- **AC-I8-a（I8、機械検査・実ストア）:** (D) の source が `StoreConsolidation*.psm1` を import せず、実 run の手順（§5 の高コスト証拠）に `store plan` / `consolidate` の呼出しが無い。
- **AC-I9-a（I9、機械検査）:** `git diff --name-status 34cf6607 <SHA B> -- tools/` が §3 の新規 file と README 2 件だけ。`git diff --stat 34cf6607 <SHA B> -- unity/` が空。
- **AC-S1-a（S1）:** offline の成功経路で、(D) が書いた印を、同じ temp base を `BaseForTest` / `LegacyBaseForTest` に設定した `Get-OsmStoreRoot` が受け入れて新 root を返す。旧 root が無い fixture（新しい host）では、`plan` が全 role `absent` を記録し、`execute` が新 root と印だけを作る。
- **AC-S2-r（S2）:** 実 run の `inventory-before.json`（承認 inventory）と `inventory-after.json` を判定 C の照合 script に通して `match`。
- **AC-S3-r（S3）:** 実 run の `marker.json`（印の field 名・size・BOM 有無・sha256、`ownerSid` が現在ユーザーと一致したかの真偽値）と、smoke 項目 1 の出力。
- **AC-S4-r（S4）:** §5 の検証経路 V-R2 の各項目の生出力が、各項目の期待と一致する。項目 4 の合格例: 実ストアに Harness の記録があれば選んだ task の `current` が表示される／無ければ未登録の task の Harness 側の既存の失敗が返る。不合格例: `store-not-migrated`・`legacy-store-present`・`store-root-unavailable` のどれかが出る。
- **AC-S5-r（S5）:** 移設スライスの判定 C bundle と C' bundle の `fetch` が `passed`、全 file の hash が送信元と一致。`workflow-task.ps1 status -Task artifact-store-root-migration` が状態を返す（resolver の 3 code でない）。
- **AC-S6-a（S6）:** §5「判定必須テスト」の全 suite の JSON で executed 1 件以上・failed 0、両 audit の error 0。

### A1: ここでは答えない問いと所有する後続

A0 案の 4 項目はそのまま。加えて次の 2 つを送る。

- LocalCache 側 copy の新 root への合流（`store plan` の再実行、owner の manifest 承認、`consolidate`、合流後の H1 適用 task の CURRENT・receipt の path bytes と新 root の両立の確認、`StoreConsolidation.psm1` の reasonCode の畳み込みが運用上の障害になるか）: program §6「分裂したストアの合流」。入口条件に「本スライスの GO（印あり・旧 root に所有 role なし・smoke 合格）」を加える（U1。A3 の O4 で owner が決め、凍結時に program §6 を直す）。
- (D) を新しい host（旧 root の無い PC）の初期化に使う運用の文書化と、別 PC での実行: program §6「取得経路と配布」の入力。本スライスは旧 root の無い場合の経路を offline で検証するだけで、実行しない。

### A1: 判定定義と停止規則

- GO: 最終証拠ラウンド（§5「高コスト証拠」）で、I1〜I9 のどれにも違反が無く、S1〜S6 がすべて成立し、判定 C と最終 C' の両方に blocker が無い。NO-GO: いずれかの違反・不成立。CONDITIONAL ACCEPT は定義しない。
- 高コスト証拠（手順 3〜8）は不可逆で、取得後の NO-GO でも自動 rollback・印の削除・(A) の再開をしない（O1）。NO-GO なら owner に返し、回復は O1 手順 5 の「回復の入口」（§4「状態の分類と resume」）か、owner が決める新しい revision で行う。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。手順 3 以降の失敗は、§4 の停止点で止めて owner へ返し、同じ session で原因調査や再実行をしない（resume は owner の指示で行う）。

### A1: 未決事項 U1〜U11 への回答

- **U1:** 清掃 job の再登録は同じ問いに含める（smoke 項目 2 が binding の再生成と検証を要求し、それは `schedule install` である。印の発行後でないと (C) の install は `store-not-migrated` で止まるので、手順 8 の中でしか行えない）。合流は分ける。理由: (1) owner の manifest 承認という別の判断を待ち、承認の時期が移設と独立に決まる。(2) 2026-10-17 の plan で 16 単位が `hold` で、`hold` の解消方式は対象外（§1）なので、合流の成立条件を観測可能に書くと「`publish` 単位の合流」だけになり、移設の GO（O1 手順 9 の前提）と結び付ける理由が無い。(3) 合流は移設スライスの merge（手順 9）の前提ではない。A0 の §1 目的と program §6 の記述（合流の実行を本スライスに寄せる）と食い違うので、A3 の O4 で owner が決める。owner が束ねると決めた場合は、合流を 2 つ目の成立条件群ではなく別 revision の問いとして書き直す。
- **U2:** move だけを使う。理由: (1) 本 host は同じ volume（§1 の観測）。(2) 複写 + ACL 再設定は、`Artifacts\credentials` の暗号化 store の内容を読んで複写することになり、A0 の不変条件候補「資格情報 file の内容を読む・複写して置換する」に当たる。(3) rename は directory 単位で原子的で、FileId が保たれ、bytes を書き換えない（I4 の観測が強くなる）。(4) 所有 role の directory は private ACL（明示 ACE・継承無し。`Set-OwnerAcl` が作った）なので、同一 volume の rename で SD がそのまま残り、新しい親の継承 ACE の再計算を受けない。移設後の照合に SDDL の一致を入れて確かめる（I4）。新 root directory `<UserProfile>\OneStarMaker` は (D) が作り、ACL は UserProfile からの継承のまま変えない（移設スライスの `Assert-SharedParent` は存在と reparse だけを見る）。別 volume の host では P4 で止まり、複写の経路は作らない（必要になれば新しい revision）。
- **U3:** 手順 3 は owner が行い、(D) は検査だけを行う。owner: Unity Editor をすべて閉じ、develop を checkout した worktree の session（Claude Code・Codex 等）に store を使う CLI の新規実行をさせないことを確認し、`Disable-ScheduledTask` で `\OneStarMaker\` 配下の清掃 job を無効にし、静止の宣言を Decision ID（Workflow README の書式）で出す。(D) の preflight（P5・P6）が同じ Windows ユーザーの process と Task Scheduler を読取で照会する。directory rename は配下の file を開いている process があると失敗するので（AC-I4-b）、照会の後に始まった writer も move の時点では遮られる。move の後に (A) が動いた場合は旧 root に所有 role が再び現れ、(C) の resolver が `legacy-store-present` で止まる（fail-closed。smoke 項目 5 で検出）。store を使う CLI が無い期間は手順 3 から手順 8 の項目 3 まで（目安 1 時間以内）で、その間の Workflow / Evidence の記録は run directory に置き、手順 8 の後に (C) で stage する（U4）。期間中に通常の Phase 作業を行わないことを owner が手順 3 の宣言に含める。
- **U4:** run directory（用語の節）に、preflight・inventory・execute の journal・照合結果・印の記録を JSON で置く。新 root にも旧 root にも置かないのは、移行中に resolver の名前表と所有 role の照合へ混ざらないため。手順 8 の後に (C) の `evidence stage` で task `artifact-store-root-migration` に固定し、取得案内を §0 の引渡し先に残す。run directory 自体は (D) も本書の手順も消さない（後始末は Phase D で owner が決める）。本スライスの Phase A〜C' の記録は手順 3 より前に (A) で行い、所有 role と一緒に新 root へ移る（S5 の `status` で読み戻す）。
- **U5:** 当てはめる。手順 3〜8 の実ストアの実行を高コスト証拠に分類し、判定 C → 暫定 C' → 高コスト証拠の取得 → 最終証拠ラウンドで進める（§5「高コスト証拠」）。O1 手順 2 の「SHA B の固定」は暫定 C' に blocker が無い head の固定に当たる。期待値は (D) の実装から独立に §5 に書き、照合 script は判定 C の offline 実行とする。
- **U6:** (D) の `execute` は冪等で、run directory の承認 inventory を正にして状態を毎回観測から分類する（§4「状態の分類と resume」）。回復は「同じ `execute` の再実行」だけで、前に進む向きにしか動かない。分類できない状態（両方にある、どちらにも無い等）では止まり、owner に返す。runbook はその状態の観測の出し方までを持ち、owner の判断を代行しない。
- **U7:** 順序は 手順 3 → 4（`plan`）→ owner の承認 → 5〜7（`execute`）→ 8（smoke 項目 1・5・6 → owner が旧 task を削除 → 項目 2 → 項目 3・4 → S5 の `fetch` → 項目 7 → run 記録の stage）→ 手順 9（移設スライスの merge、owner）。合流は後続（U1）。reasonCode の畳み込みは合流の後続の入力として送る。
- **U8:** 旧 runtime copy `Artifacts\runtime\<旧 head>\` は `Artifacts` と一緒に移り、そのまま残す（Artifacts README「自動清掃」の切替手順と同じく削除しない）。旧 binding manifest は旧 root の `logicalPath` を持つが、旧 task を削除した後は読まれない。新 runtime は (C) の install が `<new>\Artifacts\runtime\<34cf6607…>\` に作る。
- **U9:** (D) は `tools/Workflow/` の OS 基盤層に置き（§3）、実行後もリポジトリに残す。印を書く唯一の producer で、旧 root の無い新しい host では (D) を通さないと (C) の CLI が `store-not-migrated` のまま使えないため（A3 の O6）。PR は最終証拠ラウンドの GO の後、移設スライスの PR #139 の merge の後に作る。本 branch は #139 の commit を含むので、#139 を merge commit で入れれば本 branch の PR の差分は本スライスの commit だけになり、head を変えずに済む。squash で入れる場合は本 branch を develop へ合わせる必要があり、判定後の head 変更になる（A3 の O5）。
- **U10:** 確かめる。P3 として、process の `GetFolderPath('LocalApplicationData')` が Known Folder の既定位置（移設スライスの resolver と同じ `KF_FLAG_DEFAULT_PATH`）と一致することを `plan` と `execute` の preflight で照会する（metadata だけ）。一致しなければ止まる（redirect 先の旧記録の扱いは対象外）。
- **U11:** 次を A3 で固定する（A3 の O7）。対象: 旧 root（既定位置）の所有 role 3 directory の新 root への rename、新 root directory と run directory の作成、印の発行、owner による清掃 job の無効化と旧 task の削除、(C) の `schedule install` 1 回、(C) の通常操作（`status`、`current`、`fetch`、`evidence stage`）。除外: 旧 root の親・`RevisionLocks`・非所有 entry、LocalCache 側 copy、資格情報の内容と登録、R2 の通常操作を超える操作、`store plan` / `consolidate`、Unity 側。開始条件: 暫定 C' に blocker が無い SHA B が固定され、owner が手順 3 の宣言（Decision ID）を出し、`plan` の出力した inventory の sha256 を owner が承認（Decision ID）した後。承認は会話で owner 本人が行い、Decision ID と承認した sha256 を run directory の execute 記録に残す。

### A1: A3 で owner に決めてもらう事項

- **O4 合流の分離:** U1 の案（合流を program §6「分裂したストアの合流」へ送り、入口条件に本スライスの GO を加える）を採るか。採る場合は凍結と同じ commit で program §6 の 2 項目の記述を直す。
- **O5 PR の時期と #139 の merge 方式:** U9 の案（本スライスの PR は最終 GO と #139 の merge の後。#139 は merge commit で入れる）を採るか。
- **O6 (D) の寿命:** 実行後もリポジトリに残し、新しい host の初期化の入口とするか（README に書く）。残さない場合は Phase D で削除する。
- **O7 実ストア操作の承認範囲:** U11 の対象・除外・開始条件と、承認を Decision ID と inventory の sha256 で与える方式。

## 3. 責務マップ

責務は独立して変わる理由で数える。行数は見込み（新規 file のみ。既存 file のコードは変えない。I9）。

| file | 責務 | 変更理由 | 所有者・寿命 | 依存（入力 → 出力） | 公開面 | テスト境界 | 配置理由 | 行数 |
|---|---|---|---|---|---|---|---|---|
| `tools/Workflow/StoreRootMigration.psm1`（新規） | 移設の判断（policy）。preflight の観測の評価、role ごとの状態の分類、inventory の正規化と照合、印の bytes の生成、run の記録の schema と承認 hash の計算。I/O をしない。 | 「何が揃えば移してよいか」「何をもって一致とするか」「印の内容」の規則。 | 状態を持たない。process 寿命。 | 観測値（hashtable / 配列）→ 判定結果・差分・bytes。I/O module も domain module も import しない。 | `Test-OsmMigrationPreflight`、`Get-OsmMigrationRoleState`、`Compare-OsmMigrationInventory`、`New-OsmStoreRootMarkerBytes`、`Get-OsmMigrationInventoryHash`。 | in-memory の観測値だけで全規則を検証。 | 規則と I/O を分け、規則を temp directory なしで検証する（アーキテクチャゲート 2・5）。 | 約 220 |
| `tools/Workflow/StoreRootMigrationIo.psm1`（新規） | 移設の OS I/O。tree の metadata 列挙（属性・size・更新時刻・FileId・SDDL。内容を開かない）、zero-access の存在照会（移設スライス D9 と同じ意味）、同一 volume の directory rename、新 root と run directory の作成、印の一時名書込み・flush・非上書き rename、process と Task Scheduler の読取照会、`LocalApplicationData` の既定位置の照会。 | Windows API の使い方。 | 状態を持たない。handle は呼出しの中で閉じる。 | Win32（`CreateFileW`、`GetFileInformationByHandleEx`、`MoveFileExW`、`SHGetKnownFolderPath`）、CIM（`Win32_Process`）、`Get-ScheduledTask`、`WindowsStorePaths.psm1`（実体 path）→ 観測値。domain module を import しない。 | 上記の各関数。private seam: `$script:IoForTest`（rename・書込み・照会の失敗注入と I/O の記録）。CLI・環境変数から設定できない。 | temp base（同じ volume）で native 実行。seam で失敗注入。 | resolver と同じ OS 基盤層。resolver の native helper は export されておらず、consumer を変えない（I9）ため照会を複製する。複製した照会は §5 の case で resolver と同じ 6 分類を返すことを確かめる。 | 約 280 |
| `tools/store-root.ps1`（新規） | (D) の CLI。`plan`（preflight + inventory を run directory に書き、承認 hash を出す）、`execute`（再 preflight、再 inventory の照合、新 root の作成、rename、移設後の照合、不在の証明、印の発行、seam 無しの resolver での確認）。出力は `{"status":...,"reasonCode":...}` と件数・hash。 | 手順の順序（orchestration）。 | process 寿命。run directory の所有者は run。 | 上の 2 module と `WindowsStoreRoot.psm1`（`Get-OsmStoreRoot` を P1 と発行後の確認に、`Get-OsmLegacyStoreRoot` を旧 root の導出に使う。読取だけ）→ stdout JSON、exit code、run directory。3 CLI と store module を import しない。 | `plan -OwnerQuiescence <Decision ID>`、`execute -Run <runId> -InventorySha256 <trusted64> -Approval <Decision ID>`。 | 子 process で temp base の CLI case（tests 用の seam は fixture script から設定）。 | 3 CLI と並ぶ管理用の入口。3 CLI の入口は印が無いと止まるので、既存 CLI の分岐にはできない（consumer を変えない）。 | 約 120 |
| `tools/Workflow/tests/StoreRootMigration.Tests.ps1`（新規） | policy の全規則（preflight 7 項目、状態分類表、照合の field ごとの不一致、`RevisionLocks` の除外、承認 hash、印の bytes）。 | policy の契約。 | テスト。 | policy module。 | なし。 | 本体。 | 同じ domain。 | 約 260 |
| `tools/Workflow/tests/StoreRootMigrationIo.Tests.ps1`（新規） | I/O と CLI の全経路（成功、resume、partial、各失敗注入、旧 root の無い host、(C) の resolver による印の受入れ、I5・I7 の観測）。 | I/O と orchestration の契約。 | テスト。 | CLI（子 process）、I/O module、resolver の seam。 | なし。 | 本体。 | policy の suite と分けるのは、temp directory と子 process を要し実行時間と前提が違うため。 | 約 380 |
| `tools/Workflow/tests/StoreRootMigrationHostFixture.ps1`（新規） | 検証経路 V-M1 の fixture。`<UserProfile>` 直下の一時 base に旧 root・新 root の代役と private ACL の role directory を作り、seam を設定して `plan` → `execute` を実行し、生の記録を出す。 | 実 volume 上での rename の振る舞い（FileId・更新時刻・SDDL の保持）を実ストアの前に観測するため。 | テスト。 | CLI、I/O module の seam。 | なし（tests 配下）。 | 本体。 | 移設スライスの `StoreRootHostFixture.ps1` と同じ扱い。 | 約 90 |
| `tools/Workflow/tests/fixtures/store-root-migration-*/`（新規） | 判定 C の照合 script を原記録の前に通す合成 fixture（§5）。 | 期待値の凍結。 | テスト。 | なし。 | なし。 | 照合 script の入力。 | 同上。 | JSON 5 組 |
| `tools/Artifacts/README.md`、`tools/Workflow/README.md`（変更） | 「実行するprocessの条件」の末段（「後続の作業が所有」）を、(D) の入口・手順・失敗 code・run directory・新しい host の初期化に書き換える。 | 公開面は今この瞬間に真であること。 | — | — | — | docs-audit。 | — | 文のみ |

行数警報: 500 行を超える file は無い。`StoreRootMigrationIo.Tests.ps1` は約 380 行。policy・I/O・orchestration を 3 file に分けたのは、変更理由（規則 / Windows API / 手順の順序）とテスト方法（in-memory / temp directory / 子 process）が違うため。

アーキテクチャゲートの照合（A1 自己確認）:

1. 独立して変わる判断の同居: 規則（policy）・API（I/O）・順序（CLI）を分けた。所有 role の最上位名 3 つは、resolver、`StoreConsolidation.psm1`、`EvidenceSchedule.psm1` に加えて policy module が 4 箇所目として持つ。consumer を変えない（I9）ので共通化しない。名前が増える変更は 4 file に波及する（下の 7）。
2. Policy と I/O: policy は観測値だけを受ける。I/O は判定をしない。
3. 依存方向: (D)（CLI → policy・I/O）→ resolver（`WindowsStoreRoot.psm1`）・`WindowsStorePaths.psm1`。resolver・store module・3 CLI は (D) を import しない。(D) は domain module と `StoreConsolidation*` を import しない（I8）。
4. 所有者・寿命: 印の producer は (D) だけ。run directory は run が所有し、(D) は作るが消さない。新 root directory は (D) が作り、以後は store が所有する。
5. 単体テスト: policy は in-memory、I/O は temp base で Unity なしに検証できる。
6. 公開面: CLI 1 つ（2 action）と reasonCode（§4）。module の関数は tests と CLI だけが使う。
7. 将来の変更: 印の形式の変更は resolver と policy の 2 file（O2 の変更は対象外）、所有 role の追加は 4 file、rename の方式は I/O 1 file。

## 4. 実装計画

### 決定（根拠付き）

- **M1 move だけ:** U2 のとおり。`MoveFileExW(src, dst, 0)`。.NET の `Directory.Move` は使わない（flag を明示できず、失敗の分類が粗い）。
- **M2 2 段の CLI と承認 hash:** `plan` が承認 inventory を書いて sha256 を出し、owner がその値を承認し、`execute` が同じ値を `-InventorySha256` で受ける。Artifacts の `--manifest-sha256` と同じ型の承認で、承認後の変化を `execute` の再 inventory との比較で検出する（I1、AC-I1-b）。
- **M3 preflight を execute でも取り直す:** 承認と実行の間に状態が変わりうるので、plan の観測を使い回さない（AC-I1-c）。
- **M4 照合は rename の前後の inventory で行う:** 所有 role 配下の全 entry について、相対名・種別・属性・size・更新時刻（UTC、100 ns）・FileId（`FILE_ID_INFO` の 128 bit）・SDDL（owner と DACL、継承 flag を含む。現在ユーザーの SID を token に置換）。role directory 自身の更新時刻も比べる（同一 volume の rename で変わらない想定。V-M1 で実 volume の振る舞いを観測し、変わる場合は Phase B から A へ返す。§4「差し戻す条件」）。内容の hash は取らない（I4。内容を開かないため。FileId と size・時刻で同一実体を示す）。
- **M5 不在の証明は D9 と同じ意味:** zero-access `CreateFileW`（`OPEN_EXISTING`、`FILE_FLAG_BACKUP_SEMANTICS`、`FILE_FLAG_OPEN_REPARSE_POINT`）。不在・通常 file = 不在、directory = 残存、reparse・アクセス拒否・その他 = 証明できない。
- **M6 run directory:** `CreateNew` 相当（既存なら失敗）で作り、ACL は継承のまま。記録は `CreateNew` → flush → rename で書く。`execute` の再実行は新しい `execute-<n>.json` を足し、前の記録を書き換えない。
- **M7 記録の path:** 絶対 path を記録しない。`<legacy>\Artifacts\…` のような token 付き相対 path だけを持つ（I7）。
- **M8 新しい host:** 旧 root（既定位置）が無い、または所有 role が全部 `absent` のときも同じ手順で、新 root を作って印を書く。P5〜P7 は同じく要求する（新しい host でも旧コードの writer がありうるため）。

### preflight（P1〜P7。`plan` と `execute` の両方で毎回取る）

| 項目 | 観測 | 期待 | 外れたときの reasonCode |
|---|---|---|---|
| P1 | seam 無しの `Get-OsmStoreRoot` の結果 | `store-not-migrated` で失敗（静的拒否・祖先 reparse を通過した証拠）。resume で印が既にある場合は AC-I3-b | `store-root-precondition` |
| P2 | 新 root の存在と直下の名前 | 不在。resume では直下が所有 role と (D) の一時名だけ | `new-root-unexpected` |
| P3 | `GetFolderPath('LocalApplicationData')` と既定位置 | 一致 | `legacy-redirected` |
| P4 | 旧 root と UserProfile の volume（実体 path の drive root） | 一致 | `different-volume` |
| P5 | 同一ユーザーの process のうち command line に `artifacts.ps1`・`workflow-task.ps1`・`harness.ps1`・`EvidenceCleanupJob.ps1` を含むもの（自分の process tree を除く）と `Unity.exe` の件数 | 0（command line 自体は記録しない） | `writer-running` |
| P6 | `\OneStarMaker\` 配下の task の名前・`State` | すべて `Disabled`（`Running` が無い） | `scheduler-active` |
| P7 | `-OwnerQuiescence` の Decision ID | Workflow README の Decision 書式に合う | `quiescence-missing` |

### 状態の分類と resume（role ごと。承認 inventory を正にする）

| 承認 inventory での旧 root | 現在の旧 root | 現在の新 root | 分類 | 動作 |
|---|---|---|---|---|
| directory | directory | 不在 | `pending` | 再 inventory の一致を確かめて rename |
| directory | 不在 | directory | `already-moved` | rename せず、移設後の照合に進む |
| directory | directory | directory | `split` | 止まる（`role-split`）。owner へ |
| directory | 不在 | 不在 | `lost` | 止まる（`role-missing`）。owner へ |
| 不在 | 不在 | 不在 | `absent` | 何もしない |
| 不在 | 旧 root と新 root のどちらかに directory | （同左） | `unexpected` | 止まる（`role-unexpected`） |
| 照会できない | — | — | `unknown` | 止まる（`legacy-query-failed`） |

全 role が `already-moved` または `absent` になったら、移設後の照合（I2 の (a)〜(c)）→ 印の発行 → seam 無しの `Get-OsmStoreRoot` が新 root を返すことの確認の順に進む。出力: `passed`（exit 0）、`partial`（一部の role だけ移した。exit 2。同じ `execute` の再実行で前へ進む）、`failed`（exit 1。固定 reasonCode）。

reasonCode（固定集合）: 上の 2 表の code、`invalid-run`、`inventory-changed`、`inventory-hash-mismatch`、`rename-failed`、`compare-mismatch`、`legacy-role-present`、`nonowned-changed`、`marker-conflict`、`marker-write-failed`、`marker-not-accepted`（発行後に resolver が新 root を返さない。印は消さない）、`migration-failed`（それ以外）。

### inventory と照合

- `inventory-before.json`（`plan` が書く）: `schemaVersion` 1、`runId`、`roles`（3 role の存在分類）、`entries`（旧 root 配下の全 entry。各 `root`（`legacy`）、`path`（`<legacy>\` からの相対）、`kind`、`attributes`、`size`、`lastWriteUtc`、`fileId`、`sddl`）。非所有 entry も同じ形で持つ（I2 (c)）。`RevisionLocks` 配下は名前と種別だけを記録し、照合から除く。
- `inventory-after.json`（`execute` が書く）: 同じ形で、所有 role は `root` = `new`、非所有 entry は `legacy`。
- 照合: 所有 role の entry は `path` を role からの相対にそろえ、全 field の完全一致。集合の過不足も不一致。非所有 entry は `RevisionLocks` 配下を除いて完全一致。結果は `compare.json`（`result` = `match` / `mismatch`、`differences` = `{path, field}` の配列）。

### 変更対象と順序

1. policy module と `StoreRootMigration.Tests.ps1`（preflight・状態分類・照合・印の bytes・承認 hash）。
2. I/O module（列挙、照会、rename、印の書込み、process / task の照会）と I/O の単体 case。
3. CLI `tools/store-root.ps1` と `StoreRootMigrationIo.Tests.ps1` の CLI case（AC-I1〜I7、AC-S1-a）。
4. `StoreRootMigrationHostFixture.ps1` と `tests/fixtures/store-root-migration-*/`（§5 の 5 組）。
5. README 2 件。
6. `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、§5 の起点 filter の suite を B の出口で通す。

### Phase B から Phase A へ差し戻す条件

- consumer（SHA A の `tools/` の既存 file）を変えないと (D) が成立しない（I9）。
- 同一 volume の directory rename で、FileId・更新時刻・SDDL のどれかが保たれないことが分かった（M4 の前提崩れ。照合 field の選び直しは設計判断）。
- 列挙・照会のために所有 role 配下の file の内容を開く必要が出た（I4・I5）。
- process・Task Scheduler の照会が昇格なしでは成立しない（U3 の静止確認の方式の崩れ）。
- 印の形式（O2）を変える必要が出た。

### 対象外を維持する方法

- Phase B・動作確認・判定 C は実ストア・LocalCache・資格情報・Task Scheduler の登録・R2 に触れない。B のテストは temp base、V-M1 は `<UserProfile>` 直下の一時 base で行い、観測担当が終了時に削除する。実 Task Scheduler と実 process の照会（読取）は V-M1 で 1 回行ってよい。
- (D) は `store plan` / `consolidate` を呼ばず、`StoreConsolidation*` を import しない（I8）。
- consumer の既存 file を編集しない（I9。判定 C の `git diff`）。

## 5. テストとレビュー計画

- 単体テスト:
  - `StoreRootMigration.Tests.ps1`（policy、in-memory）: `preflight-p1`〜`preflight-p7`（各期待と外れ）、`role-state-<分類>`（状態分類表の 7 行）、`compare-<field>`（7 field と集合の過不足）、`compare-revisionlocks-excluded`、`inventory-hash`、`marker-bytes`（O2 の 4 field・BOM 無し・4 KiB 以下、余分な field が無い）。
  - `StoreRootMigrationIo.Tests.ps1`（temp base、子 process の CLI）: `ac-i1-a-p1`〜`ac-i1-a-p7`、`ac-i1-b`、`ac-i1-c`、`ac-i2-a-size`・`ac-i2-a-role`・`ac-i2-a-nonowned`、`ac-i2-b`、`ac-i2-c-1`〜`ac-i2-c-3`、`ac-i3-a-write`・`ac-i3-a-flush`・`ac-i3-a-rename`、`ac-i3-b-own`・`ac-i3-b-conflict`、`ac-i4-a`、`ac-i4-a-copy`、`ac-i4-b-1`〜`ac-i4-b-3`、`ac-i5-a`、`ac-i6-a`、`ac-i7-a`、`ac-s1-a`、`ac-s1-a-fresh-host`、`query-matches-resolver-1`〜`-6`（複製した照会が移設スライスの AC-I3-f と同じ 6 分類を返す）。
  - すべて `Task.Delay` / `Thread.Sleep` を使わない。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `tools/Workflow/tests/StoreRootMigration.Tests.ps1`、`tools/Workflow/tests/StoreRootMigrationIo.Tests.ps1`、`tools/Workflow/tests/StoreRoot.Tests.ps1`（各 `-Case '*' -ResultPath <path>`）。
- 判定必須テスト（GO 候補 head = SHA B）: `tools/Artifacts/tests/*.Tests.ps1`、`tools/Workflow/tests/*.Tests.ps1`、`tools/Harness/tests/*.Tests.ps1` の全 file を各 `-Case '*' -ResultPath <path>` で実行し、executed 1 件以上・failed 0。`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1` が error 0。AC-I6-b・AC-I8-a の grep、AC-I9-a の `git diff`。V-M1 の生観測。実行環境の前提: 判定用 worktree に `tools/Artifacts` の Release build があり、`rg` が PATH にある（移設スライスと同じ既知の前提）。
- 全 EditMode 回帰の適用除外: 適用除外。理由: 変更は `tools/` の PowerShell と README に限り、`unity/` を変えない。代替証拠: 上記の offline suite 全件、両 audit、`git diff --stat 34cf6607 <SHA B> -- unity/` が空。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路:
  - **V-M1（動作確認と判定 C。実 volume での (D) の通し実行）:** 対象: SHA B の候補 head。担当: 動作確認の実行担当（非仮想化 pwsh）。初期状態: `<UserProfile>` 直下に担当が `OneStarMaker.MigrationCheck.<32 桁 hex>` を作る。操作: `pwsh tools/Workflow/tests/StoreRootMigrationHostFixture.ps1 -Base <その一時 base>`（fixture が代役の旧 root に private ACL の `Artifacts`・`Workflow`・`Harness`、`Artifacts\credentials\dummy.bin`（内容は乱数）、`RevisionLocks`、`H2a Review` を作り、seam で base を差し替えて `plan` → `execute` を実行する。P5・P6 は実 process と実 Task Scheduler の照会を行い、その結果は記録するが合否に使わず、seam の値で通す）。観測: CLI の stdout と run directory の全 JSON、fixture が出す「role directory 自身の更新時刻の前後」「FileId の前後」「SDDL の前後」。期待: `passed`、`compare.json` が `match`、印を同じ base の resolver が受け入れる。P5・P6 の実照会が昇格なしで値を返す（返らなければ差し戻す条件）。後始末: 担当が一時 base を削除する。保存証拠: 生出力と run directory の複製を 1 つの時刻付きテキストと JSON 群にし、C / C' の bundle に含める。
  - **V-R1・V-R2（高コスト証拠。下記）。**
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: A1 では未確認。初回確認は V-M1（動作確認）で、確認するのは (1) 同一 volume の directory rename で role directory 自身と配下の更新時刻・FileId・SDDL が保たれるか、(2) 同一ユーザーの `Win32_Process` の command line と `\OneStarMaker\` の task の `State` を昇格なしで読めるか。不成立時は §4「差し戻す条件」で Phase A へ返す。packaged app 側の smoke 項目 7 は移設スライスの V1 で疎通済み（同じ経路）。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: 観察の判断は無い。owner の行為は 3 つ（手順 3 の静止と宣言、手順 5 の承認、手順 8 の旧 task の削除）で、それぞれ Decision ID を run directory の記録に残す。owner の宣言は観察記録ではなく承認として扱う（P5・P6 の機械照会と併せて使う）。
- 高コスト証拠（不可逆な実ストアの操作。取得は暫定 C' に blocker が無い SHA B で 1 回）:
  - **V-R1（手順 3〜7）:** 担当: owner（手順 3・5 の承認、手順 3 の無効化）と、判定 C 用の実行担当 session（(D) の実行。非仮想化 pwsh、SHA B の clean worktree）。手順: (0) 手順 3 より前に (A) で `workflow-task.ps1 start -Task artifact-store-root-migration` が済んでいることを `status` で確かめる。(1) owner が U3 の静止を行い、`Get-ScheduledTask -TaskPath '\OneStarMaker\'` で `Disabled` を確かめて Decision ID を出す。(2) `pwsh tools/store-root.ps1 plan -OwnerQuiescence <ID>`。stdout の `runId`・`inventorySha256`・role ごとの entry 数を owner に示す。(3) owner が承認の Decision ID を出す。(4) `pwsh tools/store-root.ps1 execute -Run <runId> -InventorySha256 <承認した値> -Approval <ID>`。`partial` なら同じ command を owner の指示で 1 回再実行する。`failed` なら止めて owner へ返す。保存証拠: run directory の全 file と各 command の生出力。
  - **V-R2（手順 8 の smoke と S5）:** 担当: 動作確認の実行担当（非仮想化 pwsh、(C) = `34cf6607` の clean worktree で、`tools/Artifacts` の Release build があるもの）、項目 2 の旧 task の削除は owner、項目 7 は owner が割り当てた packaged app の session（full access、同じ worktree の版）。手順と期待:
    1. 項目 1・5: (C) の `tools/Workflow/WindowsStoreRoot.psm1` と `WindowsStorePaths.psm1` を import した pwsh で `Get-OsmStoreRoot` を seam 無しで呼び、返り値が `GetFolderPath('UserProfile')` + `\OneStarMaker` と一致し、`store-root.json` を `FileShare.Read` で開いた handle の実体 path が論理 path と一致することを真偽値で出す（path は出さない）。期待: どちらも true（返り値が得られたことが、旧 root の所有 role 不在の resolver による確認でもある）。
    2. 項目 6: `Get-ScheduledTask -TaskPath '\OneStarMaker\'` の名前と `State`。期待: 旧 task が `Disabled`。
    3. 項目 2: owner が Artifacts README「自動清掃」の切替手順どおり旧 task を `Unregister-ScheduledTask` し、担当が (C) の worktree で `pwsh tools/artifacts.ps1 evidence schedule install --profile osm --config <新 root の v2 config>` → `... schedule status ...`。期待: install が成功し、status が `passed`。
    4. 項目 3: `pwsh tools/workflow-task.ps1 status -Task artifact-store-root-migration`。期待: task の状態を返す（resolver の 3 code でない）。
    5. 項目 4: `pwsh tools/harness.ps1 current -Task <id>`（承認 inventory に `Harness` の task があればその id、無ければ未登録の id）。期待: AC-S4-r の合格例。
    6. S5: 移設スライスの判定 C bundle と C' bundle の取得案内で `pwsh tools/artifacts.ps1 fetch --profile osm --config <v2 config> --reference <案内の値> --sha256 <案内の値>`。期待: 両方 `passed`、hash 一致。
    7. 項目 7: packaged app 側で `pwsh tools/workflow-task.ps1 status -Task artifact-store-root-migration`。期待: `legacy-store-present`・exit 1（LocalCache の残置がある間。成功とは記録しない）。
    8. run directory と smoke の生出力を (C) の `evidence stage` で task `artifact-store-root-migration` に固定する。
    保存証拠: 各項目の生出力を 1 つの時刻付きテキストにし（packaged app 側の出力と、その session の `git rev-parse HEAD` を担当が受け取って貼る）、run directory と一緒に stage する。
  - 実装から独立した期待値（判定 C の照合 script が原記録で確かめる）: E1 承認 inventory の sha256 = `plan` の出力 = `execute` の `-InventorySha256` = owner の承認の値。E2 所有 role の entry 集合と全 field が承認 inventory と移設後 inventory で一致し、FileId が全件一致。E3 移設後の旧 root の所有 role 3 つが不在、非所有 entry が `RevisionLocks` 配下を除いて一致。E4 `marker.json` が O2 の形式で、一時名の file が新 root 直下に無い。E5 preflight の P1〜P7 が `execute` の記録で全件期待どおり。E6 V-R2 の各項目が上記の期待どおり。E7 run directory の全 file と stdout に token 以外の絶対 path と `S-1-5-21-` で始まる SID が無い。
- 判定 C の照合 script の方式: 判定 C の offline 実行。判定 C の担当が、run directory と smoke の生出力の複製に対して E1〜E7 を確かめる script を自分の環境で書いて実行する（資格情報・通信・実ストアに触れない）。原記録の前に、Phase B が `tools/Workflow/tests/fixtures/store-root-migration-*/` に置く合成 fixture 5 組（架空の値。実 host の path・SID を含まない）に通す。期待値（A3 で凍結）:
  - `match`: 全 E が成立。期待 `match`。
  - `size-mismatch`: 移設後 inventory の 1 file の `size` だけが違う。期待 `mismatch`、差異 `size`。
  - `copied`: 移設後 inventory の 1 file の `fileId` だけが違う（複写された）。期待 `mismatch`、差異 `fileId`。
  - `role-remaining`: 移設後の旧 root に `Harness` が `directory` として残る。期待 `mismatch`、差異 `legacyRoles.Harness`。
  - `marker-extra-field`: `marker.json` の field に 5 つ目がある。期待 `mismatch`、差異 `marker.fields`。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC-I6-b・AC-I8-a の grep、AC-I9-a の `git diff`。
- 機械検査・H1: H1（`external-current-v1`）は不採用。理由: 移設スライスの D6 と同じく、この branch の `harness.ps1` は移行前に `store-not-migrated` で止まる。Phase A〜C'（暫定）の Workflow / Evidence は (A)（develop checkout の CLI、非仮想化 pwsh）で記録し、手順 8 の後の記録は (C) で行う。task id は `artifact-store-root-migration`。
- A2 の観点（担当は A2 の開始時に選ぶ。互いの指摘を見せない）:
  - R1 アーキテクチャゲート（§3 の 3 分割、照会の複製、所有 role 名の 4 箇所目、run directory の所有）と、各不変条件を破る順序の構成（§3・§4 の変更は実装済みと仮定し、契約内の操作順序で破れるかを問う。現コードは consumer の「変えない」確認にだけ使う）。
  - R2 A0 だけから代替構成（入力は A0 の commit `a016d02a` の本書。初稿を見せない）。
  - R3（任意）実行可能性: V-M1・V-R1・V-R2 の手順を、その担当が会話なしで実行できるか。owner の 3 つの行為と Decision ID の流れ。
  - C' のために、Phase A で使わない系列を 1 つ残す。
- A0/A1 主担当・モデル・ベンダー: A0 = owner と Phase A 補助 session。A1 = Phase A 主担当 session（Claude Opus 5.5、Anthropic）。
- A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版: A2 の担当が記録する（入力版 = message に `A1` と `A2 input` を含む commit。内容 sha256 を正とし commit hash を補助とする）。
- A3 統合担当・モデル・採否: 未到達。
- 独立性の強化条件を満たせない場合の理由: 未到達。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
