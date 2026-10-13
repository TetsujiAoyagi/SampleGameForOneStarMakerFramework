# Artifact Storage: Windows AppData 仮想化下の対話 CLI と分裂したストア

## 0. メタデータと引継ぎ票

このtracked文書の全Phase欄には、各成果物の取得案内の引渡し先・担当と状態だけを書く。reference・期待packageSha256の実値、ローカルの実path、内部catalog/receiptの内容は転記しない。

- type: `slice`
- status: `A`（r3。A2 2件の指摘を反映した改稿。A3 未凍結）
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

**引継ぎ票 — 次は A3（owner の決定と凍結）。** r3 は A2 1件目（構造ゲート担当、§5.7）と2件目（A0 だけからの代替構成、§5.8）を反映した改稿で、採否は A3 で owner が確定する。A3 前の残作業の状況は次のとおり。
1. ~~§5.4 の r2 probe 領域の疎通~~ 2026-10-13 に両 context で確認済み。
2. ~~A2 の2件目~~ 2026-10-13 に実施済み（§5.8）。
3. A3 で owner が D1〜D6 と §5.7・§5.8 の採否を決め、凍結する。

A2 / A3 の担当も Artifact Storage の実操作（publish / fetch / 合流）と資格情報の操作は行わない。既存ストアの移動・削除・書換えは凍結前に行わない。

### PR 状態ブロック（PR 作成時の写し）

```text
状態: Phase A の作業中（r3、A3 未凍結）
Phase A: revision r3（A2 2件を反映、A3 未凍結）、commit 未記録
implementation base / head: 3f79639c12eac16da77d06608675564e906f9a49 / 未固定
次の担当: Phase A（A3 前）。次の作業: A3 の決定と凍結
HANDOFF: docs/handoff/ARTIFACT_STORE_VIRTUALIZATION.md
Evidence 取得案内: 未生成
未決事項: D1〜D6（§2.6）、§5.7・§5.8 の採否
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

## 2. 意思決定と受け入れ境界（r3。A2 2件の指摘を反映、A3 未凍結）

用語:

- **ストア:** `%LOCALAPPDATA%\OneStarMaker\` 配下（Codex 側では同じ論理 path が LocalCache へ写る）。
- **probe 領域:** 判定 module だけが所有する `%LOCALAPPDATA%\OneStarMaker.StoreProbe\`（仮称。ストアの外の兄弟）。ストアに含めない。
- **合流の単位:** task 単位（その task の Workflow task directory、Evidence task directory、catalog が参照する transfers の operation、Harness task directory）と deployment 単位（deployment receipt 一式）。

### 2.1 不変条件（起きてはいけないこと）

- **I1 — 分裂を広げない:** 仮想化または判定不能と判定された process で、対話 CLI（`tools/artifacts.ps1` の全 subcommand、`tools/workflow-task.ps1`、`tools/harness.ps1`）がストア配下に directory・file を作らず、既存 file を開いて変更しない。判定はストアへの最初のアクセス（Workflow guard、deployment receipt、資格情報の読取りを含む）より前に行い、判定の書込みは probe 領域だけに限る。各 module は読込み（import）時にストアの root 解決・directory 作成・ACL 設定を行わない（A2b-1）。
- **I2 — 理由を丸めない:** 仮想化と判定した結果は `status=failed`、`reasonCode=virtualized-store`、判定不能は `reasonCode=store-context-unknown` で返し、どちらも exit 1 とする。どちらも CLI の最終出力まで `unconfirmed` に変えない。実 path・SID・package 名・鍵を出さない。
- **I3 — 非仮想化と判定できた実行の挙動を変えない:** 判定が「非仮想化」の process（Claude デスクトップ、通常の端末、Task Scheduler の job）では、既存の全 subcommand の結果・reasonCode・exit・ACL / reparse / identity 検査が変わらない。package identity の有無で分岐しない。判定不能（probe の作成・照会の失敗）で既存の成功経路が `store-context-unknown` になるのは、本スライスが意図する唯一の挙動変更とする（A2-6）。
- **I4 — 合流は非破壊:** 合流は Codex 側の file・directory を変更・削除しない（読取り専用 handle だけを使う）。実ストアの既存 file を上書き・削除しない。単位の公開前に単位全体を照合し、既存 entry が一つでも bytes・ACL・owner の点で一致しなければ、その単位の file を一つも公開しない。
- **I5 — 記録を書き換えない:** 合流は catalog / receipt / intent / event / CURRENT の bytes を変えずに複写する。parse した値を再 serialize しない。path 文字列の付替え・再生成をしない。
- **I6 — 安全性を弱めない:** 合流で作る directory・file は、各 role の既存規則と同じ owner・継承無しの3主体 FullControl ACL を持つ（Workflow の `Set-WorkflowPrivateAcl` と Artifacts の `Set-ArtifactAcl` は同じ規則であることを A2 後にコードで確認した）。bytes 同一で skip する既存 entry も ACL・owner を検査する。読取り元・複写先の祖先に reparse があれば単位を止める。
- **I7 — 範囲外に触れない:** 資格情報領域、§1.4 の非所有 directory、`RevisionLocks`、合流対象外の role（`runtime`、`builds-v1`、`contents-v1`）の file を開かない（hash 計算も読取りに含める）。存在確認は名前の列挙までとし、列挙で除外を決めてから内容を開く。

### 2.2 成立条件（起きなければならないこと）

- **S1 — 検出:** Codex 内で実行した対話 CLI の全入口が `virtualized-store` で止まる。
- **S2 — 計画:** 非仮想化の process から、両側の対象 role を棚卸しし、合流候補・衝突・参照欠落・除外を分類した合流 manifest 候補を、ストアを変更せずに出せる（plan / dry-run）。
- **S3 — 合流:** owner が承認した manifest の単位だけを公開し、単位ごとの receipt を残す。途中で止まっても、再実行で上書き・削除をせずに完了へ収束する。衝突・参照欠落で保留した単位が残る場合は、run 全体を `partial`（exit 2）とし、全体完了を返さない。非衝突の単位はその間も公開してよい（A2b-3）。
- **S4 — 取得の回復:** 合流後、非仮想化の担当が DebugStudio property write の A3 r3 固定入力を、既存の取得案内の reference と独立した期待 hash で fetch し、必須本文を閲覧できる。catalog がそろったことだけでは合格にしない。S4 は S3 の全体完了とは別に判定する（保留単位が残っていても、対象単位が公開済みなら S4 は判定できる）。実ストアの鍵で認証に失敗した場合は、合流の構造検証の結果を保存したまま S4 を未成立とし、資格情報の複写・置換で直さない（A2b-6）。

### 2.3 受け入れ条件（各条件の検証例）

| # | 検証する条件 | 内容 | 種別 |
|---|---|---|---|
| AC1 | I1, I2, S1 | 仮想化を注入した offline test で、全入口が `virtualized-store` を返し、ストアの I/O seam の呼出しが0件、probe 領域の seam だけが呼ばれる。CLI の最終 JSON と exit を確認する。 | offline |
| AC2 | I1, I2（失敗経路） | probe の作成・final path 照会・後始末の失敗をそれぞれ注入すると `store-context-unknown` を返し、ストアの I/O seam が0件。 | offline |
| AC3 | I3 | 「identity ありで非仮想化」「identity 無しで非仮想化」を注入したとき、既存の全 Artifacts / Workflow / Harness suite の結果が変わらない。 | offline |
| AC4 | I1, S1 | Codex 内で fetch・publish・`workflow-task.ps1 status`・`harness.ps1 current` を1回ずつ実行し、全て `virtualized-store`。前後で Codex 側 `OneStarMaker` 配下の entry 一覧（名前・件数・最終更新。内容は開かない）が同一で、probe 領域に `*.probe` が残らない。 | 実環境（§5.3） |
| AC5 | I3 | Claude デスクトップ内で読取り系の subcommand が既存どおりの reasonCode を返す。 | 実環境 |
| AC6 | I4, S3 | 合成 fixture: task A（実ストアに無し）は公開、task B（全 entry が bytes・ACL 同一）は skip、task C（1 file だけ不一致）は衝突で、C の file は一つも公開されない。衝突の入力には、同名 Workflow task の event 列が分岐したもの（version は同じで内容が違う）と、Harness CURRENT が両側で違うものを含める。どちらも自動で勝者を選ばず保留し、event の連結・version の振直しをしない（A2b-4）。run は `partial`（exit 2）。Codex 側の対象 file の hash・時刻が不変。 | offline |
| AC7 | I5 | 公開後の各 file の SHA-256 が複写元と一致し、既存の `Read-EvidenceTask` / `Read-WorkflowTaskState` が実ストア上で読める。 | offline |
| AC8 | I6 | 公開先の ACL / owner が規則と一致する。bytes 同一だが ACL 不適合の既存 entry は skip せず単位を止める。複写元・複写先の祖先へ reparse を注入すると単位を止める。 | offline |
| AC9 | I7 | `credentials`、非所有 directory、`RevisionLocks`、対象外 role に置いた sentinel file が、plan・実行のどちらでも open されない（I/O seam の open 記録で確認）。 | offline |
| AC10 | I4, S3（失敗経路） | 障害を (a) file 書込みの途中、(b) staging の hash 確認後・公開前、(c) 単位内の公開の間（transfers 公開後・Workflow 公開前）、(d) Workflow 公開後・receipt 記録前、に注入する。各場合の再実行が、上書き・削除なしで完了へ収束する。消すのは合流 run 自身の staging だけ。 | offline |
| AC11 | S4 | 実ストアへの合流後、非仮想化の担当が既存の取得案内で fetch し、独立 hash 一致・必須本文の閲覧を確認する。 | 高コスト（§5.5） |
| AC12 | I4（並行） | 公開の途中で `Get-WorkflowTasks` を呼んでも、その単位は列挙されない。公開前に別の writer が同じ Workflow task directory を作った場合は `concurrent-writer` で止まり、既存 entry を上書き・削除しない。 | offline |
| AC13 | I5, S2 | 合流計画の検査（ファイル I/O なしのメモリ fixture）: catalog が参照する transfers の operation が単位に含まれず実ストアにも無ければ `reference-missing`。JSON の文字列値に LocalCache の path が含まれれば拒否。論理 path でも対象 role 外を指せば拒否。二つの task が同じ transfers の operation を参照する場合は、二つを一つの単位に結合する（A2b-4）。 | offline |
| AC14 | I1 | 三つの CLI が import する全 module を、仮想化を注入した状態で読み込むだけでは、ストアの I/O seam が0件（A2b-1）。 | offline |

時点の読み違いを防ぐ例（AC4）: **合格**は「Codex 内の実行の前後で Codex 側の entry 一覧が同一」。**不合格**は「`virtualized-store` を返したが、Workflow の task directory や `task.lock` が新しく作られている」。

時点の読み違いを防ぐ例（AC10 (c)）: **合格**は「再実行で、公開済みの transfers は bytes・ACL 一致で skip され、残りが公開されて receipt が残る」。**不合格**は「再実行が公開済みの transfers を衝突とみなして止まる」「消して作り直す」。

### 2.4 ここでは答えない問い

§1.4 と §1.5 のとおり。最低条件未達を後続へ書き移すことで現スライスの成功にしない。

### 2.5 判定定義と停止規則

- **GO:** I1〜I7 に違反が無く、S1〜S4 を満たす。
- **NO-GO:** いずれかの不変条件違反、または成立条件の未達。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクではないので CONDITIONAL ACCEPT は使わない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.6 A3 で owner が決める事項（r2 の推奨付き）

- **D1 — 仮想化された実行の扱い:** **推奨は fail-closed。** 仮想化は `virtualized-store`、判定不能は `store-context-unknown`。(a)「一貫した実体 root で動く」は、package の中から実ストアへ新規 file を書けないため、package 外の仲介か AppData 外への再配置が必要で、D3 の後続へ送る。
  - 判定方法: 書込み probe だけで判定する。package identity は使わない（U2: identity 無しの仮想化と、identity ありの非仮想化の両方を観測した）。
  - probe の境界（A2-1）: probe はストアの外の **probe 領域** だけに書く。呼出しごとに固有名の file を `CreateNew` と `FileOptions.DeleteOnClose` で開き、その handle の final path を論理 path と比べて、handle を閉じる。閉じれば OS が file を消すので、例外経路でも後始末を呼出し側に頼らない。他の呼出しが残した file は清掃しない（自分が作ったものだけを後始末する。A2b-2）。process の強制終了で残る file は、ストアの外の probe 領域に限られるので許容する。probe 領域の directory 自体は残してよい（ストアの外で、判定 module が所有する）。
  - 疎通の前提: probe 領域がストアの外でも、Codex 内で仮想化を検出できること（LocalAppData 全体が仮想化の対象であれば成り立つ）と、`DeleteOnClose` で LocalCache 側に file が残らないことを、A3 前に Codex 内で1回確認する（§5.4）。不成立なら probe 領域をストア内の予約名へ移す案に切り替え、I1 の境界をその予約名で明示し直す。
  - 採らない案: package identity で一律拒否（Claude を誤検出、Codex を見逃す）。identity 無しで probe を省く最適化（Codex を見逃す）。書込みせず既存 root の実体 path だけで判定（実ストアに root がある場合に見逃す）。
- **D2 — 既存の分裂の扱い:** **推奨は非破壊合流。** 非仮想化の process から、owner が承認した manifest の単位だけを、Codex 側から実ストアの同じ相対位置へ bytes 同一で公開する。参照だけ解決する案（Codex 側を第二の root にする）は二つの書き手を恒久化するので採らない。廃止案は DebugStudio の固定入力を失うので採らない。
  - **計画と実行を分ける（A2-5）:** `plan` が対象 role の棚卸しと全単位の参照検査を行い、manifest 候補（単位・相対 path・bytes・SHA-256・分類・除外）を出す。参照検査は role に依存しない規則で行う。JSON の文字列値のうち完全修飾 path であるものは、(i) LocalCache の path を含まない、(ii) 論理ストア root の下では、合流対象 role の中を指す、(iii) 指す先が同じ単位に含まれるか、実ストアに bytes・ACL 同一で既にある、を全て満たす必要がある。ストア外の path（worktree の source path 等）は参照扱いせず記録しない。owner が manifest を承認し、`execute` は承認した manifest の sha256 を必須にして、実行時にも同じ検査をやり直す。差があればその単位を `source-changed` / `target-changed` で止める。
  - **書込みの方式（A2-3）:** 合流 run は実ストアに専用 directory（`Artifacts\consolidation\<runId>\`、private ACL）を持ち、manifest の写し・journal・staging・単位 receipt をそこに置く。staging と公開先は同じ volume であることを確認する。各 file は Codex 側を読取り専用で開き、staging へ `CreateNew` → flush → SHA-256 照合 → ACL 設定の順で作る。公開は directory 単位の非上書き rename（公開先が存在すれば失敗する）で行い、最終 path へ直接書かない。staging は run の寿命で所有し、再実行で自分の未公開 staging だけを捨てて作り直す。
  - **公開順と並行 writer（A2-4）:** 単位内の公開順は deployment → transfers の operation → Evidence task directory → Harness task directory → **Workflow task directory を最後**。定期清掃 job と `evidence cleanup` は Workflow の task 一覧（`Get-WorkflowTasks`）を起点に処理対象を決めるので（`EvidenceCleanup.psm1` 205行、`EvidenceSchedule.psm1` 388行で確認）、Workflow を公開するまで単位は通常処理に見えない。対話 CLI に同じ taskId を明示して呼ぶと、Workflow guard が task directory を先に作りうる。その場合は Workflow 公開の非上書き rename が失敗し `concurrent-writer` で止まる。既存 entry は消さない。運用の前提として、合流 run の間は対象 taskId の CLI 操作をしない。合流 run 自体の排他は run directory の lock file（`FileShare.None`）で、二重起動を止める。
  - **単位の結合と保留（A2b-4）:** 単位は、利用に必要な依存がその中で閉じた集合とする。role をまたいで共有される参照があれば、それらの単位を一つに結合する。bytes が違う場合、また履歴・参照が両立しない場合は、勝者を自動で選ばずその単位を保留する。
  - **公開後の確認（A2b-5）:** 公開した単位ごとに、既存の読取り経路（`workflow-task.ps1 status`、`evidence inspect`、`harness.ps1 current`）を非仮想化 context で実行し、各 domain の validator を通ることを確かめる。合流 module は domain module を import しない（手順として実行する）。
  - 実行形は「一回の旧開発環境 reset」と同じく、plan → owner 承認 → dry-run → 実行 → 単位 receipt → 公開後の確認 → 再実行時の skip とする。
- **D3 — 仮想化された host の今後:** **推奨は、本スライスの GO 後は仮想化された host で Storage・Workflow・Harness の操作を行わず、非仮想化の担当（owner の端末、Claude 等）が実行担当を務める運用とし、host 側の経路は後続 `Virtualized Host Route` へ送ること。** 影響: Codex が B を担当するスライスでは、固定入力の fetch と B exit の publish を非仮想化の実行担当へ依頼する必要がある。
- **D4 — 合流対象の role:** 推奨は `workflow`、`evidence-v2`、`deployments`、`transfers`（対象 task の catalog が参照する operation だけ）、`harness`。r1 で条件付きとした `builds-v1` は本スライスから外し、名前の列挙だけ行う（Build は Evidence の受け渡しに不要で、Build store の receipt の参照規則を本スライスで検査しないため）。`runtime` は U4 の確認に回す。`credentials`、`RevisionLocks`、非所有 directory は対象外。
- **D5 — H1 / CURRENT 方式:** 推奨は本スライスを従来の tracked HANDOFF で進め、H1 へ移行しない（H1 の CURRENT 自体が分裂の当事者）。
- **D6 — Harness から判定 module への依存（r2 で追加）:** `tools/harness.ps1` は現在 Workflow を import しない。判定 module を Workflow の基盤層に置くと、Harness → Workflow 基盤の新しい依存 edge ができる。推奨は、この edge を基盤層（OS metadata と probe だけ、domain を持たない）に限って許可すること。代替は A2 2件目が出した `tools/WindowsStore/`（判定）と `tools/StoreMerge/`（合流）の新設案である。依存方向の違いはフォルダ境界の根拠になるが、既存の `WindowsStorePaths.psm1` が Workflow に残るため、OS 基盤層が二か所に分かれる。移動は本スライスの範囲外なので、推奨は Workflow 基盤層に置き、両 module をまとめて移す判断を後続へ送ること。

## 3. 責務マップ（r2）

| 対象 | 責務（一文） | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 配置理由 | 現在行 / 予想増分 |
|---|---|---|---|---|---|---|---|---|
| 新規 `tools/Workflow/WindowsStoreContext.psm1` | 現在の process のストアが書込み仮想化下にあるかを、probe 領域だけを使って判定する | OS の判定方法・probe 規則が変わったとき | probe の file と handle を呼出し単位で所有し、`DeleteOnClose` で閉じて消す。probe 領域の directory と、自分の命名規則に合う残骸の後始末も所有する。判定結果は保持しない | OS API（`GetFinalPathNameByHandleW`）と `WindowsStorePaths.psm1` だけ。Workflow / Artifacts / Harness の domain module を import しない | `Get-OsmStoreContext`（仮称）が `not-virtualized` / `virtualized` / `unknown` の3値を返す。private の注入 seam（OS I/O と path 比較を差替え。CLI / env から設定不可） | 注入 seam で3値と各失敗経路を offline 検査 | `WindowsStorePaths.psm1` と同じ OS 基盤層。三つの CLI が共有でき、Workflow core → Artifacts の依存を作らない。Harness からの edge は D6 | 0 / 約110 |
| `tools/artifacts.ps1` | 全 subcommand の入口で判定を呼び、`virtualized` / `unknown` なら対応する reasonCode を返して終える | 入口の追加・変更時 | 呼出し1回 | 上記 module | 既存 CLI と新 reasonCode 2つ | 既存 CLI fixture へ注入 | composition root。各 Application module に散らさず一箇所で止める | 154 / 約+20 |
| `tools/workflow-task.ps1` | 同上 | 同上 | 同上 | 同上 | 同上 | `WorkflowCliFixture.ps1` | 同上 | 66 / 約+12 |
| `tools/harness.ps1` | 同上 | 同上 | 同上 | 同上（D6） | 同上 | Harness tests | 同上 | 243 / 約+12 |
| 新規 `tools/Artifacts/StoreConsolidationPlan.psm1` | 棚卸し結果（メモリ上のモデル）から単位を作り、参照・衝突・除外を分類して manifest 候補と検査結果を返す | 単位の定義、参照検査の規則、対象 role が変わったとき | 状態を持たない純粋関数 | なし（ファイル・OS に触れない） | `New-ConsolidationPlan`、`Test-ConsolidationManifest`（仮称） | メモリ fixture だけで全分類を検査（AC13） | 規則と I/O を分けて、ストアに触れずに検査できるようにする（A2-5）。規則の変更理由は I/O の変更理由と独立 | 0 / 約200 |
| 新規 `tools/Artifacts/StoreConsolidation.psm1` | 対象 role の棚卸し、staging、単位の公開、journal と receipt、run の排他を行う | 公開の手順・障害回復・ACL の扱いが変わったとき | run directory（lock、manifest の写し、journal、staging、receipt）を run の寿命で所有する。receipt は不変記録として残す。Codex 側は読取り専用 handle だけ | `StoreConsolidationPlan.psm1`、`WindowsStoreContext.psm1`（自身も非仮想化でなければ実行しない）、`WindowsStorePaths.psm1`、`ArtifactAcl.psm1`。Evidence / Workflow の domain module は import しない（テストからは読取り確認に使う） | `artifacts.ps1 store plan --source-root ...`、`store consolidate --manifest ... --manifest-sha256 ... [--dry-run]`（仮称） | 合成 fixture の両 root、I/O seam（open 記録）、4点の障害注入（AC6〜AC10、AC12） | Artifacts 側の一回の管理操作として、`EvidenceReset.psm1` と同じ形に揃える（A2 の支持）。Workflow / Harness の directory も bytes 同一で扱うだけで、その domain の判断は持たない | 0 / 約300 |
| テスト（新規 suite 3本） | 判定と入口、計画、合流 | 上記と同じ | テスト専用 root | 既存 TestSupport | なし | offline のみ | 既存 suite の形に合わせる | 0 / 約550 |
| `tools/Artifacts/README.md`、`tools/Workflow/README.md`、`tools/Harness/README.md` | 実行条件、reasonCode、合流手順 | 公開面の現況 | 永続 | なし | 文書 | docs-audit | harvest 先 | 既存 / 約+40 |

行数警報: `StoreConsolidation.psm1` は 300 行前後の見込みで、500 行の警報には届かない。超える場合は「棚卸し」と「公開・回復」の分割を C で判断する。`EvidencePaths.psm1`、`ArtifactPaths.psm1`、`TaskEventStore.psm1`、`RecordStore.psm1`、`CredentialPathAcl.psm1` は変更しない（判定を入口へ置くため）。

## 4. 実装計画（A3 で凍結する）

- 順序: (1) 判定 module と注入 seam → (2) 三つの CLI 入口 → (3) offline test（AC1〜AC3）→ (4) 計画 module とメモリ fixture（AC13）→ (5) 合流 module の棚卸しと plan → (6) staging・公開・journal・receipt・run 排他 → (7) offline test（AC6〜AC10、AC12）→ (8) README。
- Phase B から Phase A へ差し戻す条件: 入口以外（各 Application module・resolver）に判定を入れないと I1 を満たせない場合。合流で bytes の書換えが必要と判明した場合。directory 単位の非上書き rename が使えず、公開順で単位を隠せない場合。新しい保存先・資格情報の扱いが必要になった場合。判定に package manifest の解析など新しい依存が必要になった場合。
- 対象外の維持: 上記の既存 resolver・ACL 規則を変えない。Codex 側への書込み API を持たない。

## 5. テストとレビュー計画

### 5.1 テスト

- 単体テスト: 判定（3値と各失敗経路、identity の有無で分岐しないこと）、入口の順序（判定がストア I/O より前）、import 時の副作用（AC14）、計画（AC13）、合流（AC6〜AC10、AC12）。時間待機を使わず、並行は既存 suite と同じ別 process の signal 同期。
- 差し戻し中の起点 filter: 新規 3 suite、`tools/Workflow/tests/TaskLifecycle.Tests.ps1`、`tools/Artifacts/tests/ArtifactEvidence.Tests.ps1`、`EvidenceCleanup.Tests.ps1`（`rg` を PATH へ通す）、Harness tests。C が根拠付きで変更してよい。
- 判定必須テスト: 3 project の Release build、Artifacts の全 suite、Workflow TaskLifecycle、Harness tests、新規 3 suite、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- 全 EditMode 回帰の適用除外: **適用除外**。理由: Unity source・依存・asmdef を変更しない PowerShell / .NET tooling のスライスである（先行 Artifacts スライスと同じ扱い）。代替証拠: 上記の offline 全 suite と contract-audit。
- 統合・Unity テスト: なし。

### 5.2 機械検査

`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、全 Artifacts PowerShell の parse。

### 5.3 操作・実行時条件の検証経路

| 条件 | 担当 | 環境・初期状態 | 操作 | 観測と合否 | 証拠と受渡し |
|---|---|---|---|---|---|
| AC4 | Codex デスクトップ内の AI 実行担当（C が手順を渡す） | GO 候補 head を checkout した worktree。Codex 側の entry 一覧を事前取得 | §2.3 AC4 の4コマンド | 全て `virtualized-store`、entry 一覧が前後で同一、probe 領域に残骸なし | コマンド出力 JSON と前後の一覧（名前・件数・最終更新。内容と実 path の詳細は含めない）を evidence bundle へ |
| AC5 | Claude デスクトップ内の AI 実行担当 | 同じ head | 読取り系の subcommand | 既存どおりの reasonCode | 同上 |
| AC11 | 非仮想化の実行担当 | 合流済みの実ストア | 既存の取得案内で fetch、必須本文の閲覧 | 独立 hash 一致・閲覧可 | 高コスト証拠（§5.5） |

### 5.4 未知の操作経路の疎通

- **確認済み（2026-10-12）:** D1 の書込み probe（r1 の置き場所は OneStarMaker 配下）。Codex 内（Codex が起動した PowerShell 7.6.5、承認済みの sandbox 外経路）では、実体 path が `Packages\<Codex package>\LocalCache\Local\OneStarMaker\…` で論理 path と不一致、identity は無し（15700）。Claude 内（Claude Code の PowerShell）では実体 path が論理 path と一致し、identity は Store 版 PowerShell のものだった。Claude 側は probe directory も削除し、OneStarMaker 直下の entry 数は3のまま変わらなかった。Codex 側は一時 file を削除したが、空の probe directory が1つ残った（直下の entry 数は7から8。既存 entry の属性・更新時刻は不変）。その削除は Codex 側の担当の判断に委ねる。どちらの観測も R2 の通信・認証は含まない。
- **一部確認（2026-10-13）:** r2 の probe 領域の Claude 側（下記 (b)(c) の Claude 内分）。Claude Code の PowerShell で `OneStarMaker.StoreProbe` に `CreateNew` + `DeleteOnClose` の file を作ると、final path は論理 path と一致し、handle を閉じた後に file は残らなかった（probe 領域の entry 0件）。疎通のために作った probe 領域の directory は削除した。
- **確認済み（2026-10-13）:** r2 の probe 領域の Codex 側。Codex 内（承認済みの sandbox 外経路、PowerShell 7.6.5）で `OneStarMaker.StoreProbe` に `CreateNew` + `DeleteOnClose` の file を1回作ると、final path は `Packages\<Codex package>\LocalCache\Local\OneStarMaker.StoreProbe\<固有 file>` で論理 path と不一致だった。handle を閉じた後、file は論理側・LocalCache 側とも残らなかった（明示的な削除はしていない）。疎通で作った probe 領域の directory は両側とも事前に無く、終了時に削除して両側とも残っていない。上の Claude 側の結果と合わせ、D1 の probe 領域（ストア外の兄弟）と `DeleteOnClose` の後始末は両 context で成立した。
- **確認済み（2026-10-12、owner のセッション許可あり）:** U3。両 context で `credentials status` を1回ずつ実行した（世代 ID と endpoint は転記しない）。
  - 実ストア（Claude 内）: exit 0、`rotation state: stable`、retired 世代なし、updated UTC 2026-10-10。
  - Codex 側（Codex 内）: exit 0、`rotation state: stable`、retired 世代なし、updated UTC 2026-10-05。
  - 二つは別々の record で、どちらも rotation 途中（`pending-revocation`）ではない。Codex 側の record の方が古い。`status` はローカルの記録だけを読むので、R2 で有効かは示さない。コードを読んだ範囲では、`status` は既存の資格情報 directory では検査だけを行い、何も作らない。
  - 合流への影響: 資格情報は合流対象外で（D4）、S4 の fetch は実ストアの鍵を使う。実ストアの鍵が R2 で有効かは、S4 の高コスト証拠を取る時点で初めて分かる。認証で失敗した場合も、鍵の再入力・削除で直さず owner へ返す（常時契約）。
  - 後続への入力: 同じ bucket に有効な token が二つ存在しうる（Codex 側の record が別の token を保持している可能性）。失効の要否は owner が管理画面で判断する。本スライスでは扱わない。
- **一部確認（2026-10-12）:** U5 の path 文字列。Codex 内で catalog 1件を読取り専用で開き、絶対 path 12個が全て論理 path だった（値は転記しない。サイズ・更新時刻は不変）。全単位の検査は D2 の `plan` が機械的に行う。

### 5.5 高コスト証拠

- **実ストアへの合流の実行と S4 の fetch:** 実ストアを変更するため高コスト証拠に分類する。手順: 判定 C → 暫定 C' → `store plan` → owner が manifest を承認 → dry-run → 実行 → 単位 receipt → 公開後の確認（D2） → 既存取得案内で fetch。
- 独立した期待値（A2-2 で範囲を限定）:
  - 公開先の各 file の SHA-256 が manifest と一致する。
  - Codex 側は、**manifest に載った file だけ**を実行前後で hash 照合し、一致する。
  - 除外領域（資格情報、非所有 directory、`RevisionLocks`、対象外 role）は内容を開かない。実行前後の名前・件数・最終更新の列挙が同一で、合流 run の journal の open 記録にそれらの path が無い。
  - 衝突・参照欠落の単位数が dry-run と一致し、保留が残れば run の結果が `partial` である。
  - 公開した全単位で、公開後の確認（D2）の3コマンドが既存どおり成功する。
  - fetch の packageSha256 が取得案内の独立 hash と一致する。
- 判定 C の照合 script の方式: **A3 凍結 script** を推奨する。判定 C の担当が非仮想化かつ資格情報へ届かない環境で script を書ける保証が無いため。合成 fixture と期待値は A3 前に用意する。

### 5.6 担当・モデル（実績）

- A0/A1 主担当: Claude Code（Claude Opus 5.5、Anthropic）。非仮想化の Claude デスクトップから、ディレクトリ名の列挙、コードの読取り、§5.4 の probe と `credentials status`（owner 許可あり）だけを行った。Artifact Storage の操作、ストアの変更は行っていない。
- §5.4 の Codex 内疎通: Codex（OpenAI）。疎通の観測だけで、A2 のレビューではない。
- A2 独立レビュー 1件目: Codex（OpenAI）、新しいセッション。観点はアーキテクチャゲート（責務マップ、I1〜I7 を破る順序、D1〜D5）。入力版は r1 の内容 sha256 `4461c4a9ec0dc0b5ff43d6f9dd4b271af2de1f49a4aa0f084714553552c7dd3e`（commit `08cb1d1`）。開始時に一致を確認済み。結果は §5.7。
- A2 独立レビュー 2件目: Codex（OpenAI）、新しいセッション。観点は A0 だけからの代替構成。入力は commit `b96c358` の §1 だけ（抽出した §1 の UTF-8 の sha256 `a92e42097aa833b90e70d5658a1741a1fba317fcefd24fee54ba7a6efc00cee9`、開始時に一致を確認済み）。§0・§2 以降・1件目の指摘は見ていない。結果は §5.8。同じベンダーの2件目なので、独立性は「別セッション・別入力」による。
- A3 統合: 未実施。
- C' 用に予約する担当: Anthropic・OpenAI 以外の系列（候補は cursor-agent の Grok 系）。A2 では使わない。

### 5.7 A2 findings と r2 での採否（A3 で owner が確定する）

| # | severity | 指摘 | 根拠の節 | r2 の扱い | 理由 |
|---|---|---|---|---|---|
| A2-1 | P1 | probe の書込みが I1 の「ストアへの最初のアクセスより前」と AC1 の「I/O seam 0件」に矛盾。削除失敗時に残骸が増える | I1、AC1、AC4、D1、§3 | 採用 | probe をストア外の probe 領域へ移して I1 の境界を明示。`DeleteOnClose` で例外経路の後始末を OS に任せ、強制終了の残骸は判定 module が自分の命名規則に合うものだけを消す。§3 の責務を「OS metadata だけ」から「probe の file・handle・後始末の所有」に修正。ストア外での検出は §5.4 で疎通を追加 |
| A2-2 | P1 | 高コスト証拠の「Codex 側の全 file の SHA-256」は I7 の除外領域を読ませる | §1.4、I7、§5.5 | 採用 | hash 照合を manifest の file に限定し、除外領域は名前の列挙と journal の open 記録で確認する形に変更。I7 に hash 計算も読取りであると明記 |
| A2-3 | P1 | file の途中で止まると、非上書き・rollback 禁止のため再実行で回復できない | I4、I5、S3、AC10、§3 | 採用 | run 所有の staging → flush → hash 照合 → directory 単位の非上書き rename で公開する方式を D2 に固定。AC10 に4つの障害注入点を追加 |
| A2-4 | P1 | 合流中の既存 writer（定期 job の sync 等）と role をまたぐ公開順の管理者が無い | I4、S3、S4、D2、D4、§3、§4 | 採用 | 公開順を Workflow 最後に固定し、job と cleanup が Workflow の task 一覧を起点にすることをコードで確認した。同じ taskId の対話 CLI との競合は `concurrent-writer` で止める。run の二重起動は lock で止める。管理者は合流 module（run directory の所有者）。AC12 を追加 |
| A2-5 | P2 | U5 の全件検査と manifest 作成の責務が抜けている | U5、I5、I7、D4、§3、§5.4 | 採用 | 計画（純粋関数）を `StoreConsolidationPlan.psm1` に分離し、role に依存しない参照検査の規則を D2 に書いた。AC13 を追加 |
| A2-6 | P2 | 判定不能時の fail-closed と、I3 の完全互換が両立しない | I3、AC2、AC3、D1 | 採用 | I3 を「非仮想化と判定できた実行」に限定し、判定不能を別 reasonCode `store-context-unknown` にした。それが意図した唯一の挙動変更であることを I3 に明記 |

A2 が示した I1〜I7 の反例（guard の後に判定、汎用 catch で `unconfirmed` 化、bytes 同一 skip で ACL を見落とす、列挙前に除外領域を hash 等）は、AC1・AC2・AC6・AC8・AC9 の検証内容に反映した。配置について A2 は、判定 module を Workflow 基盤層に置く案と、合流 module を Artifacts 側に置く案を支持した。Workflow の ACL 規則が `TaskEventStore` の内部関数である点は、Artifacts の ACL 規則と同一であることを確認したうえで `ArtifactAcl.psm1` を使う、とした（I6）。

### 5.8 A2 2件目（A0 だけからの代替構成）との突合と r3 での採否（A3 で owner が確定する）

代替構成の骨格（ストア外の書込み probe による3値判定、identity を使わない、非仮想化 context だけでの非破壊合流、計画と I/O の分離、Codex 側の不変）は r2 と一致した。差分は次のとおり。

| # | 代替構成の主張 | r3 の扱い | 理由 |
|---|---|---|---|
| A2b-1 | module の読込み時にもストアを初期化しない契約にする | 採用 | I1 に追記し、AC14 を追加。判定より前に import 時の副作用で書けば I1 が破れる |
| A2b-2 | probe は自分が作った対象だけを後始末し、既存の残置物を清掃しない | 採用 | `DeleteOnClose` で通常・例外経路は足り、他の呼出しの file を消す必要がない。r2 の「命名規則に合う残骸の清掃」を削除 |
| A2b-3 | 保留が残れば `partial` とし、全体完了を返さない。対象の取得回復と全記録の合流完了を分ける | 採用 | S3 と S4 を書き分けた |
| A2b-4 | 単位は依存が閉じた集合。共有参照は単位を結合。同名 Workflow task の分岐・Harness CURRENT の衝突は勝者を選ばず保留 | 採用 | D2 と AC6・AC13 に反映 |
| A2b-5 | 合流後、転送先の各 domain の validator を通ることを確認する | 採用（手順として） | 合流 module に domain module を import させず、既存 CLI の読取り経路を公開後に実行する手順を D2 と §5.5 に追加 |
| A2b-6 | 実ストアの認証失敗時は、構造検証の結果を保存したまま取得回復を未成立にする | 採用 | S4 に追記（U3 の記録と同じ方針） |
| A2b-7 | 通常 CLI と定期 job が共通で使う保守用の排他門（MaintenanceGate）を設け、合流中と中断中の通常アクセスを拒否する。古い process の停止を開始条件にする | 不採用（代替あり） | 定期 job は install 時の head の固定 runtime で動くため、新しい門は job の再 install なしには効かず、runtime の変更は本スライスの範囲を広げる。r2 の公開順（Workflow を最後に置く）は今の job のまま途中の単位を見せない（`Get-WorkflowTasks` 起点であることを確認済み）。同じ taskId を明示する対話 CLI は Workflow 未公開の間 `task-not-registered` で失敗し、guard が先に directory を作れば `concurrent-writer` で止まる（AC12）。残るリスクは「合流中に対象 taskId を操作しない」という運用前提で、A3 で owner が受け入れるか決める |
| A2b-8 | 配置を `tools/WindowsStore/` と `tools/StoreMerge/` に新設する | 保留（D6） | D6 に両案を並べた。推奨は OS 基盤層を二か所に分けない現案 |
| A2b-9 | 判定不能の reasonCode を `store-context-unconfirmed` とする | 不採用 | 意味は r2 の `store-context-unknown` と同じ。既存の `unconfirmed` と字面が近く、利用者が取り違えやすい |

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
