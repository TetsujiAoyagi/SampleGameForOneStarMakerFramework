# Artifact Storage: store root 移設の実行と合流

`ARTIFACT_STORE_ROOT_RELOCATION.md`（以下「移設スライス」）が GO 候補 head で用意した consumer（新 root を読む resolver と 3 CLI）が動ける状態を、実ストアで一回だけ作る運用スライス。旧 root の所有 role を新 root へ移し、移行済みの印を発行し、LocalCache 側 copy を新 root へ合流し、清掃 job を新 root で再登録する。移設スライス §2 O1 の工程表の手順 2〜8 を担い、手順 9（移設スライスの merge）の前提を作る。`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §6 の「store root 移設の実行と合流」と、同 §6 の「分裂したストアの合流」のうち合流の実行を受け持つ。

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A0 固定。A1 未着手）
- branch: `claude/artifact-store-root-migration`（移設スライスの branch `claude/artifact-store-root-relocation` の `94f93930` から切った。`34cf6607..94f93930` の差分は `ARTIFACT_STORE_ROOT_RELOCATION.md` だけで、コードは移設スライスの GO 候補 head `34cf6607` と同一。PR base は `develop`）
- implementation base commit: `94f939303951c8ec0614303d66525bb9abcbacec`（コードは `34cf6607302d27f49bf9d3febaaa31d624f5cbed` と同一）
- implementation head commit: 未固定
- risk: `high`（実ストアの不可逆な移設、資格情報 directory の移動、旧 writer の静止と清掃 job の停止・再登録、owner 承認の manifest による合流、移設中は store を使う CLI が無い期間を作る）
- owner: OSM maintainers（A3 の凍結、手順 3・5 の承認、合流 manifest の承認、Phase D の判断は owner）
- created: 2026-10-19
- expires: 2026-12-19 または置換 revision
- harvest to: `tools/Artifacts/README.md`（移設の完了後の所在と、合流の結果）、`tools/Workflow/README.md`・`tools/Harness/README.md`（必要な場合）、`docs/README.md` の program 表、`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4・§6
- Phase A snapshot の取得案内・状態: Git 上の本文で完結（A0 = この branch の先頭 commit で、commit message に `A0` を含む。A2 入力版は §5 に commit SHA と内容 sha256 を記録する）
- Phase B result snapshot の取得案内・状態: 未到達
- evidence bundle の取得案内・状態: 未到達
- C' blind bundle の取得案内・状態: 未到達
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A1（主担当）。
  - 次の作業: 下の「Phase A の着手手順」に従い、§1・§2 の A0 を入力に A1 初稿を §2〜§5 へ書く。
  - 引き継ぐ commit SHA: この branch の先頭（A0 の commit）。作業ツリーが dirty なら引き継がない。
  - 取得案内の所在: なし（A0 は Git 上の本文だけ）。
  - 必要な権限: リポジトリの読み、pwsh 7。A1 で実ストア・LocalCache・資格情報・Task Scheduler・R2 を変更しない。A1 の設計に観測が要る場合は metadata の読取だけにし、ローカル path を本書に書かない。
  - 停止規則: A0 の入力（§2 の O1 工程表と転記した入口条件）と衝突する事実や、移設スライスの consumer コード（`34cf6607`）を変えないと成立しない設計が出たら、A1 を止めて owner へ返す。
  - 引き渡さないもの: ローカル store・LocalCache の実 path、資格情報。

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

A0 では入力と未決事項を固定する。進める最低条件・受け入れ条件・判定定義は A1 で書く。

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
