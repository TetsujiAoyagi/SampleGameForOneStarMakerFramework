# Artifact Storage: Windows AppData 仮想化下の対話 CLI と分裂したストア

## 0. メタデータと引継ぎ票

このtracked文書の全Phase欄には、各成果物の取得案内の引渡し先・担当と状態だけを書く。reference・期待packageSha256の実値、ローカルの実path、内部catalog/receiptの内容は転記しない。

- type: `slice`
- status: `A`（A0 / A1 初稿。A2 未実施、A3 未凍結）
- branch: `claude/artifacts-virtualized-store-a01`（`origin/develop` 起点）
- implementation base commit: `3f79639c12eac16da77d06608675564e906f9a49`（A3 で再確認する）
- implementation head commit: 未固定（Phase B 未着手）
- risk: `high`（永続ストアの所有境界、fail-closed 契約の追加、既存記録の移送、資格情報領域に隣接）
- owner: リポジトリ owner（依頼者）
- created: 2026-10-12
- expires: 2026-11-12 または置換 revision の成立時。期限で自動採用・実装しない。
- harvest to: `tools/Artifacts/README.md`（対話 CLI の実行条件と reasonCode、合流の結果）、`tools/Workflow/README.md` と `tools/Harness/README.md`（同じ実行条件）、`docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4（後続への入力）
- task: 未開始。`workflow-task.ps1 start` は A3 の担当が実行環境（§1.2 の分裂）を確認してから行う。仮想化された host から start しない。
- Phase A snapshot の取得案内・状態: 未生成（A2 入力版の確定時に生成。保存は §1.2 の分裂の影響を受けない非仮想化の担当が行う）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成
- PR: 未作成。作る場合も base は `develop`。

**引継ぎ票 — 次は Phase A2（独立レビュー）。** 入力版は本ファイルの A2 送付時点の内容 sha256 を正とし、commit hash を補助とする。A2 は §2.6 の決定候補 D1〜D5 と §5 の未知経路を重点に、互いの指摘を見せずに行う。少なくとも1件は [アーキテクチャゲート](../../.agents/skills/osm-workflow/references/architecture-gates.md) を担当し、高リスクのため A0（§1）だけから代替構成を出す担当を1件検討する。A2 / A3 の担当も Artifact Storage の実操作（publish / fetch / 資格情報 / 合流）は行わない。§5.4 の疎通のうち D1 の probe と U5 の1件は 2026-10-12 に確認済み。U3 も 2026-10-12 に owner の許可を得て両 context で確認した（§5.4）。既存ストアの移動・削除・書換えはどの Phase でも本 HANDOFF の凍結前に行わない。

### PR 状態ブロック（PR 作成時の写し）

```text
状態: Phase A の作業中（A1 初稿）
Phase A: revision r1（A2 未実施）、commit 未記録
implementation base / head: 3f79639c12eac16da77d06608675564e906f9a49 / 未固定
次の担当: Phase A2。次の作業: 独立レビュー（構造ゲート1件以上、A0 だけからの代替構成1件を検討）
HANDOFF: docs/handoff/ARTIFACT_STORE_VIRTUALIZATION.md
Evidence 取得案内: 未生成
未決事項: D1〜D5（§2.6）
```

## 1. A0 — 目的、現況、対象外

### 1.1 このスライスが答える問い

**AppData の書込み仮想化が有効な packaged process から対話 CLI を実行したとき、ストアを黙って分裂させず理由付きで拒否し、すでに分裂した記録を非破壊に一つの実体ストアへ合流させて、Codex が publish した Evidence を非仮想化の担当が取得できる状態に戻せるか。**

背景: 通常構成では B と C が別の host（別モデル・別アプリ）で動く。一方の host が仮想化されていると、publish した側と fetch する側が別のストアを見るため、Evidence の受け渡しが止まる。2026-10-12 時点で DebugStudio property write スライス（branch `codex/debugstudio-property-write-a01`）は B 開始時の固定入力 fetch で止まっている。

### 1.2 現況（2026-10-12 の確認結果）

**依頼者が確認した事実**

- Codex デスクトップ（package `OpenAI.Codex_2p2nqsd0c76g0`）は AppData の書込み仮想化が有効である。Codex 内の process が作った OSM の記録は `%LOCALAPPDATA%\Packages\OpenAI.Codex_2p2nqsd0c76g0\LocalCache\Local\OneStarMaker\` 配下（以下「Codex 側」）にあり、本来の `%LOCALAPPDATA%\OneStarMaker\`（以下「実ストア」）とは別の集合である。
- Claude デスクトップも packaged（`Claude_pzs8sxrjxfjjc`）だが、その LocalCache に OneStarMaker は無く、実ストアを直接使えている。**packaged であることは仮想化の十分条件ではない。**
- Codex から `pwsh tools/artifacts.ps1 fetch ...` を実行すると、`tools/Artifacts/EvidencePaths.psm1` の `Resolve-EvidenceStoredPath`（53行目: 実体 path が組み立てた target と異なる、または root 外）で内部例外 `invalid-evidence-path` になり、利用者には `status=failed / reasonCode=unconfirmed` だけが返る。呼出元は `EvidenceStateStore.psm1` の `Read-EvidenceTask`（60行目、ready/deleted/delete-pending の receipt 照合）と `EvidenceApplication.psm1` の `Invoke-EvidenceFetch`。
- 実ストアの側から fetch しても、task catalog が実ストアに無いため失敗する見込み（`Invoke-EvidenceFetch` は `Sync-EvidenceTaskGuarded` で local catalog と Workflow の task state を必須とし、catalog に artifact が無ければ `catalog-missing`）。
- 仮想化への対応は定期清掃 job の StorageBinding（AppDomain data `OneStarMaker.Evidence.StorageBinding.v1`、`tools/Artifacts/README.md`「自動清掃」節）にしか無く、対話 CLI の経路には無い。

**A0 担当が読取りだけで追加確認した事実**（ディレクトリ名の列挙のみ。ファイル内容、資格情報領域の中身は読んでいない）

- 分裂は Evidence だけではない。Codex 側には `Workflow`、`Harness`、`RevisionLocks`、`Artifacts\{builds-v1, credentials, deployments, evidence-v2, runtime, transfers}` と、tools が所有しない作業 directory（`H2a Review`、`H2b Review`、`WorktreeRecords`、`Artifacts\{evidence-handoffs, evidence-ledgers, evidence-retention, resets, review-evidence}`）がある。`contents-v1` は実ストアにだけある。
- Evidence task 名の集合は互いに素である。Codex 側 17件（`debugstudio-property-write`、`debugstudio-player-ui-route`、`artifact-evidence-lifecycle`、`artifact-build-publish-one`、`artifact-build-retention-policy`、`evidence-c-*` 9件、`integration-pr94/95/97-*`）。実ストア 8件（`series-retirement`、`content-retention-cleanup`、`build-retention-cleanup` 等）。
- Workflow task 名は **`integration-pr94-completion-20261010` が両側に存在する**（同名 task の event 列が分岐している可能性がある）。Codex 側には test fixture 由来と見られる repositoryId `aaaa…`（64字）の task 3件もある。
- Codex 側に `Artifacts\credentials` directory がある。Codex 内で資格情報の登録・置換・rotation が行われたかは未確認（§1.6 U3）。
- Harness（H1 CURRENT）は Codex 側にだけ task directory がある。`tools/Artifacts/README.md` が入口として示す `pwsh tools/harness.ps1 current -Task artifact-evidence-lifecycle` は、非仮想化の担当からは記録を見つけられない見込み。
- 実ストアの `builds-v1` は 47 entry、Codex 側は 2 entry。`deployments` は実ストア 3 / Codex 側 2 entry（同じ deploymentId の receipt が両側にあるかは未確認）。

**コードから読める機構**（A1 の前提。仮想化 OS 側の挙動は §1.6 U1 で一次確認する）

- 各ストアの root は `[Environment]::GetFolderPath('LocalApplicationData')` から論理 path を組み立てる。Evidence・deployments・transfers・runtime・Workflow は、論理 directory が存在すれば `Get-WindowsStorePathIdentity`（`GetFinalPathNameByHandleW`）で実体 path に置き換える。Harness（`tools/Harness/RecordStore.psm1`、`tools/harness.ps1`）、Build store、Content store、資格情報（`CredentialPathAcl.psm1`）は論理 path をそのまま使う。
- 実ストアの root directory が既に存在する場合、Codex 内でも root の実体 path は実ストアの path になる。その配下に Codex 内で新しく作った task directory と receipt は Codex 側に置かれるため、`Resolve-EvidenceStoredPath` は「root 外の実体」として拒否する。仮想化の有無を判定する専用の検査は無く、`invalid-evidence-path` は理由を保たずに `unconfirmed` へ丸められる。
- Workflow の `Enter-WorkflowTaskGuard` は guard 取得時に task directory を作る。Evidence の publish / fetch / inspect / use / release はいずれも guard を取るので、**読取り目的の fetch でも Codex 側に directory が増えうる**。
- 定期清掃 job は `New-EvidenceStorageBinding` で五つの専用領域（workflow / evidence / deployments / transfers / runtime）の論理 path と実体 path・volume / file identity を install 時に固定する。job の process は OS の Task Scheduler が起動するので package の外で動く。

### 1.3 次へ進める最低条件と制約（要約）

仮想化された実行が分裂を広げないこと、理由が `virtualized-store` として見えること、非仮想化の実行の挙動が変わらないこと、既存の分裂を非破壊に合流して Codex が publish した固定入力を非仮想化の担当が取得できること。詳細は §2。

常時契約から本文へ転記する制約:

- Artifact Storage の通常操作は `tools/Artifacts/README.md`「エージェント操作の事前承認」に従う。資格情報の新規登録・置換・rotation・失効/削除は承認範囲外。鍵・暗号化 store・header・署名 URL を会話・引数・環境変数・ログ・Git・同期先へ出さない。認証失敗を鍵の再入力・削除で修復しない。実行環境の承認・拒否を迂回しない。
- 既存 receipt / intent / catalog の bytes を一括で書き換えない（README「自動清掃」節の方針）。
- 文書の方針変更だけで清掃・移送・リセットを開始しない。
- 資格情報の暗号化ファイルを checkout・同期 folder・他 PC へ複製しない。本スライスは資格情報領域を移送対象にしない。
- `record` は Unity 側の契約で本スライスは PowerShell / .NET 8 のみ。Unity source・依存は変更しない。
- 例外本文・SDK 応答本文・実 path・SID・package の full path を結果へ出さない。非秘密の reasonCode と件数だけを返す。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。

### 1.4 対象外

- 仮想化された host から Storage を**使えるようにする**こと（out-of-package 実行の仲介、またはストアを AppData の外へ移す再配置）。§2.6 D3 で後続スライスへ送る案を推奨する。
- Codex 側の記録の削除・清掃。合流後も Codex 側は変更しない。
- 資格情報領域（両側）の読取り・複写・削除・比較。必要な確認は owner 許可付きの `credentials status`（非秘密の世代・状態だけ）に限る。
- tools が所有しない directory（`H2a Review`、`H2b Review`、`WorktreeRecords`、`Artifacts\{evidence-handoffs, evidence-ledgers, evidence-retention, resets, review-evidence}`）。所有者は owner / Codex の手作業で、本スライスは列挙だけ行い移送しない。
- DebugStudio（WPF）の LocalApplicationData 利用（ログ・telemetry・layout）。packaged ではなく本件の再現経路が無い。
- 別 host / Cloud への配布（program §4 C）。
- Unity テスト・Build。

### 1.5 後続へ送る問い（所有スライス名は A3 で owner が確定する）

- **Artifact Storage / Virtualized Host Route**: 仮想化された host（Codex）が Evidence を publish / fetch するための経路。候補は (i) package 外で動く固定 runtime への仲介、(ii) 全ストアを AppData の外の Known Folder へ再配置。どちらも新しい保存先または新しい実行経路で、資格情報の保存先・ACL・DPAPI の前提を変えうる。
- **Artifact Storage / Codex 側残置の後始末**: 合流済み記録の Codex 側コピーの扱い（保持・削除・期限）。Storage の 30 日清掃は Codex 側に届かない。
- **定期清掃 job の binding の再確認**: install した context と、Codex 側だけにある記録の清掃が行われていないこと（§1.6 U4）。
- Harness の CURRENT の合流方式（§2.6 D4 で本スライスから外した場合）。
- `invalid-evidence-path` 等、仮想化以外の path 不一致の診断を `unconfirmed` から分けるか（本スライスは仮想化の検出だけを扱う）。

### 1.6 未決事項・未確認

- **U1（OS 挙動）**: 仮想化下で、(a) 実ストアに既に存在する directory の配下へ新規 file を作る、(b) 既存 file を `File.Move(..., overwrite)` で置き換える、(c) 既存 file を開いて書く、のそれぞれがどちらへ書かれるか。Microsoft の公開説明では「新規の file / folder は package 専用の場所へ、既存 file の変更は元の場所で許可」とされるが、本リポジトリの atomic rename（新規 pending → rename）での結果は未確認。A1 は「仮想化下では OSM ストアへの書込みは一切行わない」前提で設計し、この確認に依存しない。
- **U2（検出方法）**: 2026-10-12 の疎通（§5.4）で、package identity と仮想化の有無が相関しないことを確認した。Codex 内の pwsh は identity が無い（`GetCurrentPackageFamilyName` が 15700 = `APPMODEL_ERROR_NO_PACKAGE`）のに仮想化されていた。Claude 内の pwsh は Store 版 PowerShell 自身の identity（`Microsoft.PowerShell_8wekyb3d8bbwe`）を持つが、仮想化されていなかった。identity は判定に使えない。残る論点は probe の置き場所と後始末（§2.6 D1）。
- **U3（資格情報の分岐）**: Codex 側の `credentials` に、実ストアと異なる世代の鍵が登録されていないか。実ストアの鍵が旧世代・失効済みなら、合流後の fetch は認証で失敗する。確認は owner 許可付きの `credentials status` を両 context で行う（中身は読まない）。2026-10-12 に owner の許可を得て両 context で確認した（§5.4）。実ストアと Codex 側は**別々の資格情報 record** で、どちらも rotation 途中ではなく、実ストアの方が新しい。どちらの token が現在 R2 で有効かは未確認。
- **U4（定期清掃 job）**: 現在の job がどちらの context から install されたか、binding の実体 path がどちらを指すか。Codex 側だけの task の 30 日清掃・配送が止まっている可能性。
- **U5（記録内の path 文字列）**: Codex 側の catalog / receipt が保持する絶対 path 文字列が、実ストアの論理 path（`%LOCALAPPDATA%\OneStarMaker\...`）を指しているか。§5.4 の疎通で、Codex 側の catalog 1件にある絶対 path 12個（receipt 1、putIntent 1、ownedCopies 9、staging 1）が全て論理 path で、LocalCache の文字列は0個だった。この1件は想定を支持するが、全件ではない。合流 manifest の作成時に全単位を機械検査する。指していれば、同じ相対位置へ bytes 同一で複写するだけで記録が自己整合する（書換え不要）。
- **U6（同名衝突）**: `integration-pr94-completion-20261010` の Workflow 記録が両側で同一か。`deployments` の receipt の重複・不一致。
- **U7（所有者不明の領域）**: `RevisionLocks` は本リポジトリの tools に生成箇所が無い。所有者を確認するまで移送しない。

## 2. 意思決定と受け入れ境界（A1 初稿）

### 2.1 不変条件（起きてはいけないこと）

- **I1 — 分裂を広げない:** 仮想化と判定された process で、対話 CLI（`tools/artifacts.ps1` の全 subcommand、`tools/workflow-task.ps1`、`tools/harness.ps1`）が OSM のストア配下に directory・file を作らず、既存 file を変更しない。判定はストアへの最初のアクセス（Workflow guard、deployment receipt の読取り、資格情報の読取りを含む）より前に行う。
- **I2 — 理由を丸めない:** I1 で止めた結果は `status=failed`、`reasonCode=virtualized-store` で返す。`unconfirmed` に丸めない。実 path・SID・package full name・鍵を出さない。
- **I3 — 非仮想化の挙動を変えない:** 非仮想化の process（Claude デスクトップ、通常の端末、Task Scheduler の job）では、既存の全 subcommand の結果・reasonCode・exit・ACL / reparse / identity 検査が変わらない。packaged で非仮想化の process を仮想化と判定しない。
- **I4 — 合流は非破壊:** 合流は Codex 側の file・directory を変更・削除しない。実ストアの既存 file を上書きしない。同じ相対 path に既存 entry があり内容が bytes 同一でなければ、その単位（task / deployment）を移送せず衝突として止める。
- **I5 — 記録を書き換えない:** 合流は catalog / receipt / intent / event の bytes を変えずに複写する。path 文字列の付替え・再生成を行わない。U5 が否定された場合は合流を実行せず Phase A へ戻す。
- **I6 — 安全性を弱めない:** 合流で作る directory・file は、各 role の既存規則と同じ owner・継承無し ACL を持ち、reparse 祖先を拒否する。読取り元（Codex 側）にも reparse・所有者の検査を行い、不一致なら止める。
- **I7 — 範囲外に触れない:** 資格情報領域、§1.4 の非所有 directory、`RevisionLocks`、合流対象に選ばれていない role を読まない（列挙での存在確認を除く）・複写しない・変更しない。

### 2.2 成立条件（起きなければならないこと）

- **S1 — 検出:** Codex 内で実行した publish / fetch / inspect / use / release / cleanup / schedule / build / content / credentials、`workflow-task.ps1 start/status/end/resume`、`harness.ps1 current` 等が `virtualized-store` で止まる。
- **S2 — 棚卸し:** 非仮想化の process から、両側の各 role の entry を非秘密で列挙し、合流候補・衝突・除外を分類した結果を出せる（dry-run）。
- **S3 — 合流:** 凍結した合流 manifest に載った単位だけを実ストアへ複写し、単位ごとの receipt（複写した相対 path・bytes・SHA-256、衝突・skip の理由）を残す。再実行は完了済み単位を skip し、未完了だけを再照合する。
- **S4 — 取得の回復:** 合流後、非仮想化の担当が DebugStudio property write の A3 r3 固定入力を、既存の取得案内の reference と独立した期待 hash で fetch し、必須本文を閲覧できる。

### 2.3 受け入れ条件（各条件の検証例）

| # | 検証する条件 | 内容 | 種別 |
|---|---|---|---|
| AC1 | I1, I2, S1 | 仮想化を注入した offline test で、全入口が store root・Workflow guard・deployment・credentials を開く前に `virtualized-store` を返す。呼ばれた I/O seam が0件。 | offline |
| AC2 | I1（失敗経路） | 判定 seam が例外・不明を返したとき、仮想化とみなして止める（fail-closed）。非仮想化の既定へ落ちない。 | offline |
| AC3 | I3 | 「identity ありで非仮想化」「identity 無しで非仮想化」を注入したとき（package identity で分岐しないことも含む）、既存の全 Artifacts / Workflow / Harness suite の結果が変わらない。 | offline |
| AC4 | I1, S1 | Codex 内で fetch・publish・`workflow-task.ps1 status`・`harness.ps1 current` を1回ずつ実行し、全て `virtualized-store`。前後で Codex 側 OneStarMaker 配下の entry 一覧（名前・件数・最終更新）が変わらない。 | 実環境（§5.3） |
| AC5 | I3 | Claude デスクトップ内で同じ読取り系 subcommand が既存どおりの reasonCode を返す（誤検出しない）。 | 実環境 |
| AC6 | I4, I5, S3 | 合成 fixture: Codex 側に task A（実ストアに無し）、task B（同名・bytes 同一）、task C（同名・内容不一致）を置き、A は複写・B は skip・C は衝突で止まる。Codex 側の全 file の hash・時刻が不変。 | offline |
| AC7 | I5 | 複写後の catalog / receipt の SHA-256 が複写元と一致し、既存の `Read-EvidenceTask` が実ストア上で receipt を解決できる。 | offline |
| AC8 | I6 | 複写先の ACL / owner が role 規則と一致。複写元・複写先の祖先に reparse を注入すると、その単位を止める。 | offline |
| AC9 | I7 | `credentials`、非所有 directory、`RevisionLocks` に置いた sentinel file が、dry-run・実行のどちらでも open されない（I/O seam の記録で確認）。 | offline |
| AC10 | S3（失敗経路） | 単位の途中で複写が失敗したとき、部分複写を完了扱いにせず、receipt に未完了を残し、再実行で残りだけを照合する。実ストアの既存 file を消して戻さない（rollback DELETE をしない）。 | offline |
| AC11 | S4 | 実ストアへの合流後、非仮想化の担当が既存の取得案内で fetch し、独立 hash 一致・必須本文の閲覧を確認する。 | 高コスト（§5.5） |

時点の読み違いを防ぐ例（AC4）: **合格**は「Codex 内の実行の前後で Codex 側の entry 一覧が同一」。**不合格**は「`virtualized-store` を返したが、Workflow の task directory や `task.lock` が新しく作られている」。

### 2.4 ここでは答えない問い

§1.4 と §1.5 のとおり。最低条件未達を後続へ書き移すことで現スライスの成功にしない。

### 2.5 判定定義と停止規則

- **GO:** I1〜I7 に違反が無く、S1〜S4 を満たす。
- **NO-GO:** いずれかの不変条件違反、または成立条件の未達。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクではないので CONDITIONAL ACCEPT は使わない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.6 A3 で owner が決める事項（A1 の推奨付き）

- **D1 — 仮想化された実行の扱い（依頼の問い (a)/(b)）:** **推奨は (b) fail-closed（`virtualized-store`）。** (a)「一貫した実体 root で動く」は、package の中からは実ストアへ新規 file を書けない（U1 の公開説明どおりなら、どの path 文字列を使っても新規 file は package 専用の場所へ行く）ため、package 外の仲介か AppData 外への再配置が必要になる。どちらも新しい実行経路・保存先で、資格情報の前提に及ぶので D3 の後続へ送る。
  - 検出方法の候補（U2）:
    - (1) 書込み probe だけで判定する。OneStarMaker 配下の専用 probe directory に `CreateNew` で一時 file を作り、その実体 path（`GetFinalPathNameByHandleW`、`\?\` を除去）が論理 path と一致するかを見る。一致しなければ仮想化とする。package identity は判定に使わない（U2: identity が無い仮想化と、identity があって非仮想化の両方を観測した）。probe は file と directory の両方を削除し、ストア本体には触れない。
    - (2) package identity があれば一律に拒否する案。Claude の pwsh を誤検出し（I3 違反）、Codex の pwsh を見逃す（I1 違反）ので採らない。identity が無いときに probe を省く最適化も、Codex を見逃すので採らない。
    - (3) 書込みをせず、既存の role root の実体 path だけで判定。実ストアに root が存在する場合に仮想化を見逃すため、I1 を満たせない。
    - A1 は (1) を推奨する。§5.4 の疎通では、Codex 内で一時 file は削除できたが、probe directory を削除しなかったため LocalCache 側に空 directory が1つ残った。実装では directory も削除し、仮想化下で削除後に何も残らないことを AC4 の前後比較で確認する。probe を置く場所（OneStarMaker 直下か、ストア外の兄弟か）は A3 で決める。probe が作成・削除に失敗したら判定不能とし、AC2 のとおり止める。
- **D2 — 既存の分裂の扱い:** **推奨は「非破壊合流」。** 非仮想化の process から、凍結 manifest に載った単位（task / deployment）だけを Codex 側から実ストアの同じ相対位置へ bytes 同一で複写する。参照だけ解決する案（実ストアの読取りで Codex 側を第二の root として見る）は、package 固有の path を恒久的な root にし、二つの書き手を固定するので採らない。廃止案は DebugStudio の固定入力を失うので採らない。README の「既存 receipt を一括で書き換えない」とは、bytes を変えない複写であれば整合する（U5 が前提）。
  - 実行形は既存の「一回の旧開発環境 reset」と同じく、A3（または C）で exact な単位一覧・hash・除外を固定した manifest、dry-run、単位ごとの receipt、再実行時の skip とする。
- **D3 — 仮想化された host の今後:** **推奨は、本スライスの GO 後は仮想化された host で Storage・Workflow・Harness の操作を行わず、非仮想化の担当（owner の端末、Claude 等）が実行担当を務める運用とし、host 側の経路は後続 `Virtualized Host Route` へ送ること。** 影響: Codex が B を担当するスライスでは、固定入力の fetch と B exit の publish を非仮想化の実行担当へ依頼する必要がある。owner がこの運用を受け入れない場合は、本スライスの問いを広げず、後続の優先度を上げる。
- **D4 — 合流対象の role:** 推奨は `workflow`、`evidence-v2`、`deployments`、`transfers`（catalog の staging / ownedCopies が transfers の operation を参照するため）、`harness`（README の入口 `harness.ps1 current -Task artifact-evidence-lifecycle` を回復するため）。`builds-v1`（Codex 側 2件）は棚卸しには含め、合流は衝突が無い場合に限る。`runtime` は定期清掃 job の固定 runtime で、合流せず U4 の確認に回す。`credentials`、`RevisionLocks`、非所有 directory は対象外。
- **D5 — H1 / CURRENT 方式:** Artifacts の先行スライスは H1（`external-current-v1`）を使ったが、その CURRENT 自体が分裂の当事者である。推奨は本スライスを従来の tracked HANDOFF で進め、H1 へ移行しない。

## 3. 責務マップ（A1 初稿。配置は A2 の構造ゲートで確認する）

| 対象 | 責務（一文） | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 配置理由 | 現在行 / 予想増分 |
|---|---|---|---|---|---|---|---|---|
| 新規 `tools/Workflow/WindowsPackageContext.psm1` | 現在の process が AppData の書込み仮想化下にあるかを判定する | OS の判定方法が変わったとき | 状態を持たない。呼出しごとに判定 | `WindowsStorePaths.psm1` と同じく OS metadata のみ。Workflow / Artifacts / Harness の domain を import しない | `Test-OsmStoreVirtualized`（仮称）と private の注入 seam（CLI / env からは設定不可） | 注入 seam で packaged / 非 packaged / 仮想化 / 判定不能を offline 検査 | `WindowsStorePaths.psm1` と同じ「filesystem metadata だけ」の層。三つの CLI が共有でき、Workflow core が Artifacts を import しない方向を保つ | 0 / 約80 |
| `tools/artifacts.ps1` | 全 subcommand の入口で判定を呼び、仮想化なら `virtualized-store` を返す | 入口の追加・変更時 | 呼出し1回 | 上記 module | 既存 CLI + 新 reasonCode | 既存 CLI fixture へ注入 | composition root。各 Application module に散らさず一箇所で止める | 154 / 約+15 |
| `tools/workflow-task.ps1` | 同上 | 同上 | 同上 | 同上 | 同上 | `WorkflowCliFixture.ps1` | 同上 | 66 / 約+10 |
| `tools/harness.ps1` | 同上 | 同上 | 同上 | 同上 | 同上 | Harness tests | 同上 | 243 / 約+10 |
| 新規 合流 module（配置は A2 で決める。候補 `tools/Artifacts/StoreConsolidation.psm1`） | 凍結 manifest の単位を Codex 側から実ストアへ非破壊に複写し、単位 receipt を残す | 合流の規則・対象 role が変わったとき | 一回の操作。receipt は実ストアの専用 directory に不変記録として残す | `WindowsStorePaths.psm1`、`ArtifactAcl.psm1`、Workflow の ACL 規則。Evidence / Workflow の domain 判定は import しない（bytes 同一の複写だけ） | `artifacts.ps1 store consolidate --manifest ... --manifest-sha256 ... [--dry-run]`（仮称） | 合成 fixture の両 root、I/O seam、障害注入 | Workflow と Harness の role も扱うため、Artifacts 配下に置くと所有境界が曖昧になる。`EvidenceReset.psm1` と同じ一回操作の形に揃える案と、`tools/Workflow` 側へ置く案を A2 で比較する | 0 / 約250 |
| テスト（新規 suite 2本） | 判定と入口、合流 | 上記と同じ | テスト専用 root | 既存 TestSupport | なし | offline のみ | 既存 suite の形に合わせる | 0 / 約400 |
| `tools/Artifacts/README.md` ほか README 2件 | 実行条件と reasonCode、合流手順 | 公開面の現況 | 永続 | なし | 文書 | docs-audit | harvest 先 | 既存 / 約+30 |

行数警報: 合流 module が 250 行を超える見込みは低い。超える場合は「manifest 検査」と「複写の実行」を分ける根拠（変更理由と障害注入の境界）があるかを C で確認する。`EvidencePaths.psm1` 等の既存 resolver は変更しない（判定を入口へ置くため）。

## 4. 実装計画（A3 で凍結する）

- 順序: (1) 判定 module と注入 seam → (2) 三つの CLI 入口 → (3) offline test（AC1〜AC3）→ (4) 合流 module の dry-run（棚卸し）→ (5) 合流の実行と receipt → (6) offline test（AC6〜AC10）→ (7) README。
- Phase B から Phase A へ差し戻す条件: 入口以外（各 Application module・resolver）に判定を入れないと I1 を満たせない場合。合流で bytes の書換えが必要と判明した場合（U5 の否定）。新しい保存先・資格情報の扱いが必要になった場合。判定に package manifest の解析など新しい依存が必要になった場合。
- 対象外の維持: `EvidencePaths.psm1`、`ArtifactPaths.psm1`、`TaskEventStore.psm1`、`RecordStore.psm1`、`CredentialPathAcl.psm1` の root 解決・ACL 規則を変えない。Codex 側への書込み API を持たない（合流 module は Codex 側を読取り専用 handle で開く）。

## 5. テストとレビュー計画

### 5.1 テスト

- 単体テスト: 判定（packaged × 仮想化 × 判定不能の組合せ）、入口の順序（判定が全 store I/O より前）、合流（AC6〜AC10）。時間待機を使わず、並行は既存 suite と同じ別 process の signal 同期。
- 差し戻し中の起点 filter: 新規 2 suite、`tools/Workflow/tests/TaskLifecycle.Tests.ps1`、`tools/Artifacts/tests/ArtifactEvidence.Tests.ps1`、`EvidenceCleanup.Tests.ps1`（`rg` を PATH へ通す）、Harness tests。C が根拠付きで変更してよい。
- 判定必須テスト: 3 project の Release build、Artifacts の全 suite、Workflow TaskLifecycle、Harness tests、新規 2 suite、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- 全 EditMode 回帰の適用除外: **適用除外**。理由: Unity source・依存・asmdef を変更しない PowerShell / .NET tooling のスライスである（先行 Artifacts スライスと同じ扱い）。代替証拠: 上記の offline 全 suite と contract-audit。
- 統合・Unity テスト: なし。

### 5.2 機械検査

`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、全 Artifacts PowerShell の parse。

### 5.3 操作・実行時条件の検証経路

| 条件 | 担当 | 環境・初期状態 | 操作 | 観測と合否 | 証拠と受渡し |
|---|---|---|---|---|---|
| AC4 | Codex デスクトップ内の AI 実行担当（C が手順を渡す） | GO 候補 head を checkout した worktree。Codex 側の entry 一覧を事前取得 | §2.3 AC4 の4コマンド | 全て `virtualized-store`、entry 一覧が前後で同一 | コマンド出力 JSON と前後の一覧（名前・件数・最終更新。内容と実 path の詳細は含めない）を evidence bundle へ |
| AC5 | Claude デスクトップ内の AI 実行担当 | 同じ head | 読取り系の subcommand | 既存どおりの reasonCode | 同上 |
| AC11 | 非仮想化の実行担当 | 合流済みの実ストア | 既存の取得案内で fetch、必須本文の閲覧 | 独立 hash 一致・閲覧可 | 高コスト証拠（§5.5） |

### 5.4 未知の操作経路の疎通（A3 前に確認する）

- **確認済み（2026-10-12）:** D1 (1) の probe。Codex 内（Codex が起動した PowerShell 7.6.5、承認済みの sandbox 外経路）では、実体 path が `Packages\<Codex package>\LocalCache\Local\OneStarMaker\…` で論理 path と不一致、identity は無し（15700）。Claude 内（Claude Code の PowerShell）では実体 path が論理 path と一致し、identity は Store 版 PowerShell のものだった。Claude 側は probe directory も削除し、OneStarMaker 直下の entry 数は3のまま変わらなかった。Codex 側は一時 file を削除したが、空の probe directory が1つ残った（直下の entry 数は7から8。既存 entry の属性・更新時刻は不変）。その削除は Codex 側の担当の判断に委ねる。どちらの観測も R2 の通信・認証は含まない。
- **確認済み（2026-10-12、owner のセッション許可あり）:** U3。両 context で `credentials status` を1回ずつ実行した（世代 ID と endpoint は転記しない）。
  - 実ストア（Claude 内）: exit 0、`rotation state: stable`、retired 世代なし、updated UTC 2026-10-10。
  - Codex 側（Codex 内）: exit 0、`rotation state: stable`、retired 世代なし、updated UTC 2026-10-05。
  - 二つは別々の record で、どちらも rotation 途中（`pending-revocation`）ではない。Codex 側の record の方が古い。`status` はローカルの記録だけを読むので、R2 で有効かは示さない。コードを読んだ範囲では、`status` は既存の資格情報 directory では検査だけを行い、何も作らない。
  - 合流への影響: 資格情報は合流対象外で（D4）、S4 の fetch は実ストアの鍵を使う。実ストアの鍵が R2 で有効かは、S4 の高コスト証拠を取る時点で初めて分かる。認証で失敗した場合も、鍵の再入力・削除で直さず owner へ返す（常時契約）。
  - 後続への入力: 同じ bucket に有効な token が二つ存在しうる（Codex 側の record が別の token を保持している可能性）。失効の要否は owner が管理画面で判断する。本スライスでは扱わない。
- **一部確認（2026-10-12）:** U5 の path 文字列。Codex 内で catalog 1件を読取り専用で開き、絶対 path 12個が全て論理 path だった（値は転記しない。サイズ・更新時刻は不変）。全単位の検査は合流 manifest の作成時に行う。

### 5.5 高コスト証拠

- **実ストアへの合流の実行と S4 の fetch:** 実ストアを変更する（不可逆ではないが、既存記録と混ざる）ため高コスト証拠に分類する。手順: 判定 C → 暫定 C' → owner が合流 manifest（exact な単位・相対 path・SHA-256・除外）を承認 → dry-run → 実行 → 単位 receipt → 既存取得案内で fetch。独立した期待値: 単位ごとの複写先 SHA-256 が manifest と一致、Codex 側の全 file の SHA-256 が実行前と一致、fetch の packageSha256 が取得案内の独立 hash と一致、衝突単位の数が dry-run と一致。
- 判定 C の照合 script の方式: **A3 凍結 script** を推奨する。判定 C の担当が非仮想化かつ資格情報へ届かない環境で script を書ける保証が無いため。合成 fixture と期待値は A1 改稿で用意する。

### 5.6 担当・モデル（実績）

- A0/A1 主担当: Claude Code（Claude Opus 5.5、Anthropic）。非仮想化の Claude デスクトップから、ディレクトリ名の列挙とコードの読取りだけを行った。資格情報・Artifact Storage の操作、ストアの変更は行っていない（§5.4 の自分の probe は作成・削除済み）。
- §5.4 の Codex 内疎通: Codex（OpenAI）。疎通の観測だけで、A2 のレビューではない。A2 で Codex を使う場合は、C' 用に Anthropic・OpenAI 以外の系列を予約する。
- A2 独立レビュー: 未実施。
- A3 統合: 未実施。
- C' 用に予約する担当: A2 で使わないモデル系列・ベンダーを1つ残す（A2 の担当決定時に記録する）。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
