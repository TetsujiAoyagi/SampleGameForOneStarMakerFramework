# Artifact Storage: Windows AppData 仮想化下の対話 CLI と分裂したストア

## 0. メタデータと引継ぎ票

このtracked文書の全Phase欄には、各成果物の取得案内の引渡し先・担当と状態だけを書く。reference・期待packageSha256の実値、ローカルの実path、内部catalog/receiptの内容は転記しない。

- type: `slice`
- status: `A`（2026-10-13、owner の裁定で Phase A を r4 として再開。r4 は A2・A3・統合差分の再レビューを経て凍結。次は B（r4 の追加分と前巡の戻し項目）。発見 C 再レビュー（差し戻し1巡目、先行）までの記録は §7 に残す。判定 C 未着手）
- branch: `claude/artifacts-virtualized-store-a01`（`origin/develop` 起点）
- implementation base commit: `3f79639c12eac16da77d06608675564e906f9a49`（A3 で `origin/develop` と一致を再確認済み）
- Phase A revision: **`r4`（2026-10-13 凍結）**。凍結 commit は `77364220ecdd5f7894750744cdaa6e80d1d1dd62`、凍結時の本ファイルの内容 sha256 は `44174f89c23c9d7224b749ff7891397acce239bc04b2afed65156fd4be1aa4e3`（`git show <commit>:<本ファイル>` の bytes。この記入だけの直後の commit は含めない）。§1〜§5 が B の唯一の仕様。r3 からの変更点は §5.10、A2・A3・再レビューは §5.11〜§5.13。
- 直前の凍結 revision: `r3`（凍結）。凍結 commit は `97145dc08a183e1f1893751f6a86925a3acda1da`（Phase B が `git log` で確認し、PR 状態ブロックへ転記した）。凍結時の本ファイルの内容 sha256 は `c7259ad70ce241ed323a2169c4f5f82a2be4598206bfd2e6b286a6fa8342197f`。
- implementation head commit: `808991618373b0bf56d2cb6b04faf40e050a1f1a`（差し戻し1巡目の修正後。§0 をこの状態にした commit で、直後のレビュー記録 commit は含めない）。初回の Phase B 完了時は `c55891a498dc3ca99312533d3c9a23126e72fd0c`。HANDOFF にレビュー結果だけを追記した commit は含めない。
- risk: `high`（永続ストアの所有境界、fail-closed 契約の追加、既存記録の移送、資格情報領域に隣接）
- owner: リポジトリ owner（依頼者）
- created: 2026-10-12
- expires: 2026-11-12 または置換 revision の成立時。期限で自動採用・実装しない。
- harvest to: `tools/Artifacts/README.md`（対話 CLI の実行条件と reasonCode、合流の結果）、`tools/Workflow/README.md` と `tools/Harness/README.md`（同じ実行条件）、`docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4（後続への入力。r4 で `Split Store Recovery` と `Virtualized Host Route` を所有スライスとして載せる小節を推奨。§1.5）
- task: `artifacts-virtualized-store`。2026-10-13 に Phase B の担当が非仮想化の host から `pwsh tools/workflow-task.ps1 start -Task artifacts-virtualized-store` で開始済み（version 1、active）。仮想化された host（Codex 等）から操作しない。
- Phase A snapshot の取得案内・状態: 未保存。owner の指示で、Phase A では Artifact Storage を操作していない。Phase B の固定入力は、凍結 commit の本ファイル（git で到達可能）とする。C / C' へ渡す時点で、非仮想化の担当が凍結 commit から所見を除いた snapshot を作り、通常どおり publish する。
- Phase B result snapshot / 判定 evidence bundle / C' blind bundle の取得案内・状態: 未生成。依頼により B は Artifact Storage を操作していない。判定 C / C' へ渡す時点で、非仮想化の担当が implementation head から生成して publish する（Phase A snapshot と同じ扱い）。
- PR: 未作成。作る場合も base は `develop`。

**引継ぎ票 — 次は Phase B（r4 凍結後の差し戻し2巡目）。** Phase A revision r4（2026-10-13 凍結、§5.12・§5.13）の §1〜§5 を唯一の仕様とする。implementation base / head は上記のまま。

- **B の作業:** (1) r4 の追加分（§4 の「r4 の追加分」: `multi-workflow-unit`・`workflow-already-visible` の保留、承認後の変化の扱い、保留単位の staging・receipt を作らないこと、検証例（§5.1）、README の合流節）。(2) 前巡の発見 C で B へ返した戻し項目（§7.7、参照先 file の読取り失敗を存在・ACL だけで成立としない修正。r4 と独立）。二つを一つの差し戻しラウンドにまとめてよい。
- **B の担当:** 新しい session。開始時にリモートの head、本 §0、凍結 commit と内容 sha256 を照合し、食い違えば引き継がない。§5.1 の起点 filter を実行して B の出口で引継ぎ票を更新し push する。
- **B の後:** 修正 head を固定して発見 C の再レビュー（B と異なるモデルの新規 session）。本スライスの高コスト証拠は なし（§5.5 案 B）なので、判定 C → C'（最終）で GO を判定する。

- **A2 の担当（完了、§5.11）:** B（Claude Opus 5.5 / Anthropic）とも r4 A1（同じ）とも異なるモデルの新しい session。C' 用に予約した系列（§5.6）は使わない。開始時に、リモートの head、本 §0、下記の入力 commit と内容 sha256 を照合し、食い違えば始めない。
- **A2 の入力:** r4 草案の commit と、その commit での本ファイルの内容 sha256（`git show <commit>:docs/handoff/ARTIFACT_STORE_VIRTUALIZATION.md` の bytes の SHA-256）。値は本 §0 の「A2 の入力（固定）」に記入する。読む範囲は §0 と §1〜§5（§5.10 の変更点一覧を含む）。§6・§7 は B と発見 C の記録で、A2 の観点の判断材料にしない。
- **A2 の観点:** (1) 裁定1・2の反映が正確で、裁定に不要な凍結条件の意味の変更が無いこと（§5.10 の表と r3 の本文 `97145dc` の差分で確かめる）。(2) 新しい保留規則（`multi-workflow-unit`、§2.6 D2）が I1〜I7・S1〜S3 を破る順序を作らないこと。特に、保留の判定と単位の最初の公開の前後、実行時の再棚卸しで Workflow が増える場合、deployment 単位の先行公開、他の保留理由との併存。(3) §5.5 の案 A / B と推奨の根拠。(4) 新しい状態・公開 API・依存を増やしていないこと。
- **A2 の後（完了）:** A3 で owner が採否と §5.5 の案、§1.5 の後続スライス名を決め（§5.12）、統合差分の再レビュー（§5.13）を経て r4 を凍結した。
- **実行環境:** B 以降は非仮想化の host（owner の端末、Claude デスクトップ等）。本スライスの入口は仮想化された host で `virtualized-store` を返す設計なので、仮想化された host では既存・新規 suite が成り立たない（AC4 の実環境確認だけは Codex 内で行う。§5.3）。
- **必要な権限（B 以降）:** sandbox 外での PowerShell 7 実行（LocalAppData の probe と隔離 fixture の ACL 設定を含む）。sandbox による probe 拒否の `store-context-unknown` は仮想化の証拠ではない。suite の前に3 project の Release DLL を既存 loader の出力先へ用意し、EvidenceCleanup の前に `rg` を PATH へ通す。Unity・資格情報・R2 通信は不要。
- **Evidence:** Phase A snapshot・Phase B result の保存・取得案内は未完了。前回および今回の発見 C の固定入力・生結果の原sourceは各専用 worktree の Git 管理外にあり、未保存・引渡し未完了。r4 A1 でも publish / fetch は実行していない。担当は非仮想化の保存・実行担当。判定 C / C' の bundle は未生成。この状態を保存・引渡し済みとは扱わない。
- **やらないこと（本スライス）:** 実ストアへの合流の実行・実データの `store plan`（§5.5 案 B で後続 `Split Store Recovery` へ送った）、Codex 側・実ストアの既存 file の変更・移動・削除、`credentials` の操作、判定 C・C' の起動（B の段階）。Artifact Storage の publish / fetch は非仮想化の保存担当が C / C' へ渡す時点で行う。
- **停止規則:** 阻害する指摘は凍結条件または常時契約への違反根拠を付けて B へ返す（`phases-and-handoff.md` の blocker の基準）。凍結条件の意味を変える判断が要れば Phase A の新しい revision にする。
- **引き渡さないもの:** C' に §5.7・§5.8・§5.11・§5.13 の所見、A2 / A3 の議論、§6 の「HANDOFF との差」以外の C の所見を渡さない。C' 用の担当は §5.6 で予約済み。

**A2 の入力（固定）:** r4 草案の commit は `2ba64e18e38480177300885ee4f4c6bdff234977`、その commit での本ファイルの内容 sha256 は `d3951934fd5c0872583948846f72b37e6f2f46fb5918fe0c43131183da52ff77`（`git show 2ba64e18e38480177300885ee4f4c6bdff234977:docs/handoff/ARTIFACT_STORE_VIRTUALIZATION.md` の bytes）。この記入だけの直後の commit は入力に含めない。

### PR 状態ブロック（PR 作成時の写し）

```text
状態: Phase A r4 凍結。Phase B（差し戻し2巡目）未着手。判定 C 未着手
Phase A: revision r4（凍結）、commit 77364220ecdd5f7894750744cdaa6e80d1d1dd62
implementation base / head: 3f79639c12eac16da77d06608675564e906f9a49 / 808991618373b0bf56d2cb6b04faf40e050a1f1a
次の担当: Phase B（r4 の追加分と前巡の戻し項目 1件）。次に発見 C 再レビュー → 判定 C → C'
HANDOFF: docs/handoff/ARTIFACT_STORE_VIRTUALIZATION.md
Evidence 取得案内: 未保存・引渡し未完了。保存は非仮想化の担当
未決事項: なし（保留した単位の扱い・取得の回復・実ストアへの合流は後続 Split Store Recovery）B への戻し項目 1件（§7.7、r4 と独立）
```

## 1. A0 — 目的、現況、対象外

### 1.1 このスライスが答える問い

**AppData の書込み仮想化が有効な packaged process から対話 CLI を実行したとき、ストアを黙って分裂させず理由付きで拒否し、すでに分裂した記録を、owner が承認した単位だけ非破壊に一つの実体ストアへ合流させる手段を、合流できない単位を理由付きで保留する形で用意できるか。**

r4 で問いを狭めた。合流後に Codex が publish した Evidence を非仮想化の担当が実際に取得できるか（旧 S4）は、実ストアの実データと資格情報の有効性に依存し、合流の手段の正しさとは別に判定できるので、後続 `Artifact Storage / Split Store Recovery`（§1.5）へ送る。

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

仮想化された実行が分裂を広げないこと、理由が `virtualized-store` として見えること、非仮想化の実行の挙動が変わらないこと、既存の分裂を非破壊に合流する計画と実行の手段があり、合流できない単位（複数の Workflow task を含む単位を含む）を理由付きで保留して全体完了を返さないこと。詳細は §2。合流後の取得の回復は本スライスの最低条件に含めない（§1.5）。

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
- 合流後の取得の回復（旧 S4 / AC11）と保留した単位の扱い。後続 `Artifact Storage / Split Store Recovery` へ送る（§1.5）。実ストアへの合流の実際の実行も、A3 で §5.5 の案 B を採ったので後続へ送る。
- Unity テスト・Build。

### 1.5 後続へ送る問い（`Split Store Recovery` は r4 の A3 で owner が確定。他は各所有スライスの着手時に確定する）

- **Artifact Storage / Split Store Recovery（r4 の A3 で新設を決定）**: 本スライスの合流の手段を実データに適用して、DebugStudio property write の A3 r3 固定入力の取得を回復する。受け取る問いは次の3つ。
  - **取得の回復（旧 S4 と旧 AC11 をそのまま移す）:** 合流後、非仮想化の担当が上記の固定入力を、既存の取得案内の reference と独立した期待 hash で fetch し、必須本文を閲覧できる。catalog がそろったことだけでは合格にしない。保留した単位が残っていても、対象の単位が公開済みなら判定できる。実ストアの鍵で認証に失敗した場合は、合流の構造検証の結果を保存したまま未成立とし、資格情報の複写・置換で直さない。
  - **実ストアへの合流の実行:** `store plan` → owner の manifest 承認 → dry-run → 実行 → 単位 receipt → 公開後の確認（D2）。A3 で §5.5 の案 B を採ったので、§5.5 の手順と独立した期待値（照合 script の方式を含む）をそのまま引き継ぐ。最初の手順（A0 の現況確認）として、読取りだけの `store plan`（実ストアも Codex 側も変更しない）を実データに1回実行し、その `reasons` で、対象の単位が ACL・JSON 検査・`multi-workflow-unit`・`workflow-already-visible` のどれで保留されるかを先に確定し、保留されるなら規則を変えるか（I6 や検査規則の意味の変更）をそのスライスの Phase A で決める。
  - **保留した単位の扱い:** `multi-workflow-unit` と `workflow-already-visible`（§2.6 D2）で保留した単位を、公開途中の不可視性を保ったまま合流する方法。r3 で検討した候補は、公開途中の単位を通常処理から隠す門、Workflow の task 一覧の列挙規則の変更、`tasks` directory ごとの一括配置、依存の順に置いて各 task を依存がそろってから見せる方式で、いずれも新しい gate・reader 契約、または不可視性の単位の読み替えを要する。対象の単位が保留された場合は、保留した単位の扱いを別の設計スライスに分けるか、回復を先に別の手段で行うかを、そのスライスの Phase A で決める（reader 契約・gate の設計変更は、一回限りの回復操作と変更理由・リスクが違うため）。
  - 所有スライス: **A3 で新しい `Artifact Storage / Split Store Recovery` を設けると決定した。** `Virtualized Host Route` は仮想化された host から Storage を使う新しい実行経路・保存先の設計で、資格情報の保存先・ACL・DPAPI の前提を変えうる。こちらは既存の実ストアだけで閉じる一回の回復操作で、変更理由・リスク・急ぎ方（DebugStudio の B が止まっている）が違う。代替は `Virtualized Host Route` に含める案で、スライスが一つ減るが、回復が host 経路の設計を待つことになる。
  - harvest 先: `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 に、本スライスの結果と並べて新しい小節（例: 「E. 仮想化 host と分裂したストア」）を設け、`Split Store Recovery` と `Virtualized Host Route` を所有スライスとして載せる。本スライスの Phase D で harvest する。
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

## 2. 意思決定と受け入れ境界（r4。r3 は 2026-10-13 に A3 で凍結済み。r3 からの変更点は §5.10）

用語:

- **ストア:** `%LOCALAPPDATA%\OneStarMaker\` 配下（Codex 側では同じ論理 path が LocalCache へ写る）。
- **probe 領域:** 判定 module だけが所有する `%LOCALAPPDATA%\OneStarMaker.StoreProbe\`（仮称。ストアの外の兄弟）。ストアに含めない。
- **合流の単位:** task 単位（その task の Workflow task directory、Evidence task directory、catalog が参照する transfers の operation、Harness task directory）と deployment 単位（deployment receipt 一式）。参照の共有で task 単位を結合した結果、一つの単位が含む Workflow task directory は2つ以上になりうる。
- **単位が新たに公開する Workflow task directory の数:** 単位に属する Workflow task directory のうち、実ストアで absent のもの、つまりその実行で新たに公開するものの数（Workflow task directory は directory 単位の非上書き rename で公開するので partial にはならない。item の partial は deployment の receipt と config の対にだけ生じる）。実ストアに bytes・ACL 同一で既にある Workflow はこの数に含めず、§2.6 D2 の `workflow-already-visible` の規則で扱う。

### 2.1 不変条件（起きてはいけないこと）

- **I1 — 分裂を広げない:** 仮想化または判定不能と判定された process で、対話 CLI（`tools/artifacts.ps1` の全 subcommand、`tools/workflow-task.ps1`、`tools/harness.ps1`）がストア配下に directory・file を作らず、既存 file を開いて変更しない。判定はストアへの最初のアクセス（Workflow guard、deployment receipt、資格情報の読取りを含む）より前に行い、判定の書込みは probe 領域だけに限る。各 module は読込み（import）時にストアの root 解決・directory 作成・ACL 設定を行わない（A2b-1）。
- **I2 — 理由を丸めない:** 仮想化と判定した結果は `status=failed`、`reasonCode=virtualized-store`、判定不能は `reasonCode=store-context-unknown` で返し、どちらも exit 1 とする。どちらも CLI の最終出力まで `unconfirmed` に変えない。実 path・SID・package 名・鍵を出さない。
- **I3 — 非仮想化と判定できた実行の挙動を変えない:** 判定が「非仮想化」の process（Claude デスクトップ、通常の端末、Task Scheduler の job）では、既存の全 subcommand の結果・reasonCode・exit・ACL / reparse / identity 検査が変わらない。package identity の有無で分岐しない。判定不能（probe の作成・照会の失敗）で既存の成功経路が `store-context-unknown` になるのは、本スライスが意図する唯一の挙動変更とする（A2-6）。
- **I4 — 合流は非破壊:** 合流は Codex 側の file・directory を変更・削除しない（読取り専用 handle だけを使う）。実ストアの既存 file を上書き・削除しない。単位の公開前に単位全体を照合し、既存 entry が一つでも bytes・ACL・owner の点で一致しなければ、その単位の file を一つも公開しない。新たに公開する Workflow task directory が2つ以上の単位（`multi-workflow-unit`）と、公開する item があるのに単位の Workflow が実ストアに既にある単位（`workflow-already-visible`）も、同じく file を一つも公開しない（公開途中の単位を Workflow の task 一覧に出さないため。§2.6 D2）。
- **I5 — 記録を書き換えない:** 合流は catalog / receipt / intent / event / CURRENT の bytes を変えずに複写する。parse した値を再 serialize しない。path 文字列の付替え・再生成をしない。
- **I6 — 安全性を弱めない:** 合流で作る directory・file は、各 role の既存規則と同じ owner・継承無しの3主体 FullControl ACL を持つ（Workflow の `Set-WorkflowPrivateAcl` と Artifacts の `Set-ArtifactAcl` は同じ規則であることを A2 後にコードで確認した）。bytes 同一で skip する既存 entry も ACL・owner を検査する。読取り元・複写先の祖先に reparse があれば単位を止める。
- **I7 — 範囲外に触れない:** 資格情報領域、§1.4 の非所有 directory、`RevisionLocks`、合流対象外の role（`runtime`、`builds-v1`、`contents-v1`）の file を開かない（hash 計算も読取りに含める）。存在確認は名前の列挙までとし、列挙で除外を決めてから内容を開く。

### 2.2 成立条件（起きなければならないこと）

- **S1 — 検出:** Codex 内で実行した対話 CLI の全入口が `virtualized-store` で止まる。
- **S2 — 計画:** 非仮想化の process から、両側の対象 role を棚卸しし、合流候補・衝突・参照欠落・複数 Workflow・既に見える Workflow・除外を分類した合流 manifest 候補を、ストアを変更せずに出せる（plan / dry-run）。該当する単位は、manifest 候補で `multi-workflow-unit` / `workflow-already-visible` の保留として示す。
- **S3 — 合流:** owner が承認した manifest の単位だけを公開し、単位ごとの receipt を残す。途中で止まっても、再実行で上書き・削除をせずに完了へ収束する。ここでの「完了」は、承認した単位のうち公開対象（承認時に保留でない単位）の全単位が published / complete であること。衝突・参照欠落・`multi-workflow-unit`・`workflow-already-visible` で保留した単位が残る場合は、run 全体を `partial`（exit 2）とし、全体完了を返さない。保留しない単位はその間も公開してよい（A2b-3）。
- **S4 — （r4 で後続 `Artifact Storage / Split Store Recovery` へ移した。§1.5）** 番号は欠番として残し、振り直さない。

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
| AC10 | I4, S3（失敗経路） | 障害を (a) file 書込みの途中、(b) staging の hash 確認後・公開前、(c) 単位内の公開の間（transfers 公開後・Workflow 公開前）、(d) Workflow 公開後・receipt 記録前、に注入する。各場合の再実行が、上書き・削除なしで完了へ収束する。消すのは合流 run 自身の staging だけ。失敗経路（r4）: (c) で止まった単位が、再実行の前に Codex 側の変化で構成が変わった場合（例: 新しい task が結合して Workflow が2つになる）、承認した単位は `source-changed` で止まり、公開済みの断片（transfers 等）は削除せず、Workflow が未公開なので task 一覧には現れないまま残る。これを正しい状態とする。 | offline |
| AC11 | — | （r4 で後続 `Artifact Storage / Split Store Recovery` へ移した。§1.5。番号は欠番） | — |
| AC12 | I4（並行）, S3 | 公開の途中で `Get-WorkflowTasks` を呼んでも、その単位は列挙されない（単位ごとの意味）。観測の基準: 公開対象の単位は、その単位の最後の Workflow の rename の前には列挙されず、rename の後に初めて列挙される（新たに公開する Workflow は1つなので、現れるのは rename の一回）。保留した単位は、実行の前・途中・後のどの時点でも、その実行によってその単位の task が新たに列挙されない。公開前に別の writer が同じ Workflow task directory を作った場合は `concurrent-writer` で止まり、既存 entry を上書き・削除しない。失敗経路: (i) 二つの task が参照の共有で一つの単位になり、新たに公開する Workflow が2つになる場合、実行はその単位の file を一つも公開せず `multi-workflow-unit` で保留する。(ii) 単位の Workflow が実ストアに bytes・ACL 同一で既にあり、他の item（Evidence・Harness 等）を公開する必要がある場合、その単位の file を一つも公開せず `workflow-already-visible` で保留する。(i)(ii) とも run は `partial`（exit 2）、実ストアの対象 path は不変、run directory にその単位の staging と単位 receipt が無く、同じ run の他の単位は公開される。全 item が identical の単位は保留せず従来どおり skip / complete とする。新たに公開する item が Workflow だけの単位（既にある Workflow と、absent の Workflow 1つ）は保留しない。(iii) 承認後の変化: 承認した manifest で公開対象だった単位の構成が実行時の再棚卸しで変われば（例: Workflow が1つから2つ）、その単位は `source-changed` で止まり、新しい構成の単位は未承認なので公開しない。承認時に保留だった単位は、理由の種類にかかわらず、実行時に保留理由が消えても公開せず、再 plan と再承認を要する。承認時に公開対象だった単位が実行時に保留の条件を満たせば（例: 承認後に別の writer が単位の Workflow を同一 bytes・ACL で置いた）`target-changed` で止まり、staging しない。 | offline |
| AC13 | I5, S2 | 合流計画の検査（ファイル I/O なしのメモリ fixture）: catalog が参照する transfers の operation が単位に含まれず実ストアにも無ければ `reference-missing`。JSON の文字列値に LocalCache の path が含まれれば拒否。論理 path でも対象 role 外を指せば拒否。二つの task が同じ transfers の operation を参照する場合は、二つを一つの単位に結合し（A2b-4）、新たに公開する Workflow が2つなら `multi-workflow-unit` で保留する。新たに公開する Workflow が1つ以下で、単位の Workflow が実ストアに無ければ保留しない。単位の Workflow が実ストアに既にあり（identical を含む）、Workflow 以外に公開する item があれば `workflow-already-visible` で保留する。全 item が identical の単位、Workflow 以外が全て identical で新たに公開する Workflow が1つだけの単位は保留しない。 | offline |
| AC14 | I1 | 三つの CLI が import する全 module を、仮想化を注入した状態で読み込むだけでは、ストアの I/O seam が0件（A2b-1）。 | offline |

時点の読み違いを防ぐ例（AC4）: **合格**は「Codex 内の実行の前後で Codex 側の entry 一覧が同一」。**不合格**は「`virtualized-store` を返したが、Workflow の task directory や `task.lock` が新しく作られている」。

時点の読み違いを防ぐ例（AC10 (c)）: **合格**は「再実行で、公開済みの transfers は bytes・ACL 一致で skip され、残りが公開されて receipt が残る」。**不合格**は「再実行が公開済みの transfers を衝突とみなして止まる」「消して作り直す」。

時点の読み違いを防ぐ例（AC12 の保留）: **合格**は「保留した単位の transfers・Evidence・Harness・Workflow のどれも実ストアに新たに現れず、run が `partial`」。**不合格**は「Workflow 以外（transfers・Evidence 等）を先に公開し、Workflow だけを保留する」「Workflow を1つだけ公開して残りを保留する」「実ストアに既にある Workflow の task に、Evidence・Harness だけを足す」「保留を `passed` の run に含める」「承認時に保留だった単位を、実行時に理由が消えたので公開する」。

### 2.4 ここでは答えない問い

§1.4 と §1.5 のとおり。最低条件未達を後続へ書き移すことで現スライスの成功にしない。

### 2.5 判定定義と停止規則

- **GO:** I1〜I7 に違反が無く、S1〜S3 を満たす。旧 S4（取得の回復）は r4 で後続へ移したので GO の条件に含めない。合流で保留した単位が残ること（`partial`）は、S3 の規則どおりなら NO-GO の理由にならない。
- **NO-GO:** いずれかの不変条件違反、または成立条件の未達。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクではないので CONDITIONAL ACCEPT は使わない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.6 決定事項（A3 で owner が推奨どおり採用）

- **D1 — 仮想化された実行の扱い:** **推奨は fail-closed。** 仮想化は `virtualized-store`、判定不能は `store-context-unknown`。(a)「一貫した実体 root で動く」は、package の中から実ストアへ新規 file を書けないため、package 外の仲介か AppData 外への再配置が必要で、D3 の後続へ送る。
  - 判定方法: 書込み probe だけで判定する。package identity は使わない（U2: identity 無しの仮想化と、identity ありの非仮想化の両方を観測した）。
  - probe の境界（A2-1）: probe はストアの外の **probe 領域** だけに書く。呼出しごとに固有名の file を `CreateNew` と `FileOptions.DeleteOnClose` で開き、その handle の final path を論理 path と比べて、handle を閉じる。閉じれば OS が file を消すので、例外経路でも後始末を呼出し側に頼らない。他の呼出しが残した file は清掃しない（自分が作ったものだけを後始末する。A2b-2）。process の強制終了で残る file は、ストアの外の probe 領域に限られるので許容する。probe 領域の directory 自体は残してよい（ストアの外で、判定 module が所有する）。
  - 疎通の前提: probe 領域がストアの外でも、Codex 内で仮想化を検出できること（LocalAppData 全体が仮想化の対象であれば成り立つ）と、`DeleteOnClose` で LocalCache 側に file が残らないことを、A3 前に Codex 内で1回確認する（§5.4）。不成立なら probe 領域をストア内の予約名へ移す案に切り替え、I1 の境界をその予約名で明示し直す。
  - 採らない案: package identity で一律拒否（Claude を誤検出、Codex を見逃す）。identity 無しで probe を省く最適化（Codex を見逃す）。書込みせず既存 root の実体 path だけで判定（実ストアに root がある場合に見逃す）。
- **D2 — 既存の分裂の扱い:** **推奨は非破壊合流。** 非仮想化の process から、owner が承認した manifest の単位だけを、Codex 側から実ストアの同じ相対位置へ bytes 同一で公開する。参照だけ解決する案（Codex 側を第二の root にする）は二つの書き手を恒久化するので採らない。廃止案は DebugStudio の固定入力を失うので採らない。
  - **計画と実行を分ける（A2-5）:** `plan` が対象 role の棚卸しと全単位の参照検査を行い、manifest 候補（単位・相対 path・bytes・SHA-256・分類・除外）を出す。参照検査は role に依存しない規則で行う。JSON の文字列値のうち完全修飾 path であるものは、(i) LocalCache の path を含まない、(ii) 論理ストア root の下では、合流対象 role の中を指す、(iii) 指す先が同じ単位に含まれるか、実ストアに bytes・ACL 同一で既にある、を全て満たす必要がある。ストア外の path（worktree の source path 等）は参照扱いせず記録しない。owner が manifest を承認し、`execute` は承認した manifest の sha256 を必須にして、実行時にも同じ検査をやり直す。差があればその単位を `source-changed` / `target-changed` で止める。
  - **書込みの方式（A2-3）:** 合流 run は実ストアに専用 directory（`Artifacts\consolidation\<runId>\`、private ACL）を持ち、manifest の写し・journal・staging・単位 receipt をそこに置く。staging と公開先は同じ volume であることを確認する。各 file は Codex 側を読取り専用で開き、staging へ `CreateNew` → flush → SHA-256 照合 → ACL 設定の順で作る。公開は directory 単位の非上書き rename（公開先が存在すれば失敗する）で行い、最終 path へ直接書かない。staging は run の寿命で所有し、再実行で自分の未公開 staging だけを捨てて作り直す。
  - **公開順と並行 writer（A2-4）:** 単位内の公開順は deployment → transfers の operation → Evidence task directory → Harness task directory → **Workflow task directory を最後**。定期清掃 job と `evidence cleanup` は Workflow の task 一覧（`Get-WorkflowTasks`）を起点に処理対象を決めるので（`EvidenceCleanup.psm1` 205行、`EvidenceSchedule.psm1` 388行で確認）、Workflow を公開するまで単位は通常処理に見えない。この保証は、新たに公開する Workflow が1つで、単位の Workflow が実ストアに無いことを前提とし、前提を満たさない単位は下記「公開途中に task 一覧へ見える単位の保留」のとおり公開しない（r4）。対話 CLI に同じ taskId を明示して呼ぶと、Workflow guard が task directory を先に作りうる。その場合は Workflow 公開の非上書き rename が失敗し `concurrent-writer` で止まる。既存 entry は消さない。運用の前提として、合流 run の間は対象 taskId の CLI 操作をしない。合流 run 自体の排他は run directory の lock file（`FileShare.None`）で、二重起動を止める。
  - **単位の結合と保留（A2b-4）:** 単位は、利用に必要な依存がその中で閉じた集合とする。role をまたいで共有される参照があれば、それらの単位を一つに結合する。bytes が違う場合、また履歴・参照が両立しない場合は、勝者を自動で選ばずその単位を保留する。
  - **公開途中に task 一覧へ見える単位の保留（r4）:** 単位を公開すると途中で task 一覧に見える構成を、次の二つの reasonCode で保留する。数え方は §2 の用語。(1) `multi-workflow-unit`: 新たに公開する Workflow が2つ以上。公開は directory 単位の非上書き rename で一つずつ行うので、最初の Workflow を置いてから最後を置くまで単位の一部が `Get-WorkflowTasks` に現れ、途中で止まればその状態が残る。(2) `workflow-already-visible`: 単位の Workflow が実ストアに既にあり（identical を含む）、Workflow 以外に公開する item（実ストアで absent / partial の transfers・Evidence・Harness 等）がある。新たに公開する Workflow だけが残る単位（例: 結合単位の task A の Workflow は identical、task B の Workflow だけが absent で他は全て identical）は保留しない。B の Workflow の rename 一回で task B が現れるだけで、公開途中の状態が無いため。その task は公開の前から一覧に見えているので、Workflow を最後に置いても他の item の公開途中が見える。「Workflow を最後に置く」公開順は、新たに公開する Workflow が1つで、単位の Workflow が実ストアに無い場合にだけ単位の不可視性（AC12）を保証する。全 item が identical の単位は公開しないので保留せず、従来どおり skip / complete とする。保留は既存の保留と同じ扱い（その単位の file を一つも公開しない、run を `partial` にする、依存する単位を `dependency-held` にする既存の伝播に従う）。保留した単位は staging せず、単位 receipt を作らず、journal には保留の trace だけを残す。判定は計画（純粋関数）で行い、`execute` の実行時の再検査（`Test-ConsolidationManifest` による承認 manifest との照合と再棚卸し）でも同じ規則を適用し、単位の最初の file を staging するより前に決める。承認した単位の構成（item 集合）が実行時に変われば `source-changed` で止め、新しい構成の単位は公開しない。承認時に保留だった単位は、保留の理由の種類（既存の理由と r4 の二つ）にかかわらず、実行時に理由が消えても公開しない（再 plan・再承認を要する。承認した manifest の「公開する単位」だけを公開する S3 の既存の規則と同じで、r3 からの意味の変更ではない）。承認時に公開対象だった単位が、実行時の再検査で保留の条件（r4 の二つを含む）を満たせば `target-changed` で止め、staging しない（既存の「実行時に保留理由が生じた単位は `target-changed`」と同じ）。例: 承認後に別の writer が単位の Workflow を同一 bytes・ACL で置き、他の item が absent のまま、という状態は自分の run では生じない（Workflow を最後に公開するため）ので、`target-changed` で止める。自分の中断の後に許容する target の遷移（absent → partial / identical）は、Workflow が identical になる場合、他の item も全て identical であるとき（(d) の中断後）に限る。deployment 単位は task 単位より先に独立して公開する既存の順のままで、task 単位の保留は deployment 単位の公開を止めない（他の理由で task 単位を保留した場合と同じ）。新しい状態・公開関数・引数・依存・gate は増やさない。reasonCode の値は2つ増え、manifest と結果に現れる（schemaVersion は変えない）。保留した単位の合流方法は後続 `Artifact Storage / Split Store Recovery` へ送る（§1.5）。
  - 保留の理由は互いに独立に判定し、一つの単位が複数の理由を持ってよい。`multi-workflow-unit` と `workflow-already-visible` の追加は、参照先の存在・読取り・ACL の検査（(iii)）を緩めず、置き換えない。
  - **公開後の確認（A2b-5）:** 公開した単位ごとに、既存の読取り経路（`workflow-task.ps1 status`、`evidence inspect`、`harness.ps1 current`）を非仮想化 context で実行し、各 domain の validator を通ることを確かめる。合流 module は domain module を import しない（手順として実行する）。
  - 実行形は「一回の旧開発環境 reset」と同じく、plan → owner 承認 → dry-run → 実行 → 単位 receipt → 公開後の確認 → 再実行時の skip とする。
- **D3 — 仮想化された host の今後:** **推奨は、本スライスの GO 後は仮想化された host で Storage・Workflow・Harness の操作を行わず、非仮想化の担当（owner の端末、Claude 等）が実行担当を務める運用とし、host 側の経路は後続 `Virtualized Host Route` へ送ること。** 影響: Codex が B を担当するスライスでは、固定入力の fetch と B exit の publish を非仮想化の実行担当へ依頼する必要がある。
- **D4 — 合流対象の role:** 推奨は `workflow`、`evidence-v2`、`deployments`、`transfers`（対象 task の catalog が参照する operation だけ）、`harness`。r1 で条件付きとした `builds-v1` は本スライスから外し、名前の列挙だけ行う（Build は Evidence の受け渡しに不要で、Build store の receipt の参照規則を本スライスで検査しないため）。`runtime` は U4 の確認に回す。`credentials`、`RevisionLocks`、非所有 directory は対象外。
- **D5 — H1 / CURRENT 方式:** 推奨は本スライスを従来の tracked HANDOFF で進め、H1 へ移行しない（H1 の CURRENT 自体が分裂の当事者）。
- **D6 — Harness から判定 module への依存（r2 で追加）:** `tools/harness.ps1` は現在 Workflow を import しない。判定 module を Workflow の基盤層に置くと、Harness → Workflow 基盤の新しい依存 edge ができる。推奨は、この edge を基盤層（OS metadata と probe だけ、domain を持たない）に限って許可すること。代替は A2 2件目が出した `tools/WindowsStore/`（判定）と `tools/StoreMerge/`（合流）の新設案である。依存方向の違いはフォルダ境界の根拠になるが、既存の `WindowsStorePaths.psm1` が Workflow に残るため、OS 基盤層が二か所に分かれる。移動は本スライスの範囲外なので、推奨は Workflow 基盤層に置き、両 module をまとめて移す判断を後続へ送ること。

## 3. 責務マップ（r4。r2 の表に r4 の保留規則を加えた。配置・依存は r2 から変えていない）

| 対象 | 責務（一文） | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 配置理由 | 現在行 / 予想増分 |
|---|---|---|---|---|---|---|---|---|
| 新規 `tools/Workflow/WindowsStoreContext.psm1` | 現在の process のストアが書込み仮想化下にあるかを、probe 領域だけを使って判定する | OS の判定方法・probe 規則が変わったとき | probe の file と handle を呼出し単位で所有し、`DeleteOnClose` で閉じて消す。probe 領域の directory と、自分の命名規則に合う残骸の後始末も所有する。判定結果は保持しない | OS API（`GetFinalPathNameByHandleW`）と `WindowsStorePaths.psm1` だけ。Workflow / Artifacts / Harness の domain module を import しない | `Get-OsmStoreContext`（仮称）が `not-virtualized` / `virtualized` / `unknown` の3値を返す。private の注入 seam（OS I/O と path 比較を差替え。CLI / env から設定不可） | 注入 seam で3値と各失敗経路を offline 検査 | `WindowsStorePaths.psm1` と同じ OS 基盤層。三つの CLI が共有でき、Workflow core → Artifacts の依存を作らない。Harness からの edge は D6 | 0 / 約110 |
| `tools/artifacts.ps1` | 全 subcommand の入口で判定を呼び、`virtualized` / `unknown` なら対応する reasonCode を返して終える | 入口の追加・変更時 | 呼出し1回 | 上記 module | 既存 CLI と新 reasonCode 2つ | 既存 CLI fixture へ注入 | composition root。各 Application module に散らさず一箇所で止める | 154 / 約+20 |
| `tools/workflow-task.ps1` | 同上 | 同上 | 同上 | 同上 | 同上 | `WorkflowCliFixture.ps1` | 同上 | 66 / 約+12 |
| `tools/harness.ps1` | 同上 | 同上 | 同上 | 同上（D6） | 同上 | Harness tests | 同上 | 243 / 約+12 |
| 新規 `tools/Artifacts/StoreConsolidationPlan.psm1` | 棚卸し結果（メモリ上のモデル）から単位を作り、参照・衝突・複数 Workflow・既に見える Workflow・除外を分類して manifest 候補と検査結果を返す | 単位の定義、参照検査の規則、保留の規則、対象 role が変わったとき | 状態を持たない純粋関数 | なし（ファイル・OS に触れない） | `New-ConsolidationPlan`、`Test-ConsolidationManifest`（仮称）。r4 の `multi-workflow-unit`・`workflow-already-visible` は既存の保留理由の集合に加わる reasonCode の値（manifest と結果に現れる公開面の値の追加）で、公開関数・引数・schemaVersion は変えない | メモリ fixture だけで全分類を検査（AC13。r4 の保留の有無の境界を含む） | 規則と I/O を分けて、ストアに触れずに検査できるようにする（A2-5）。規則の変更理由は I/O の変更理由と独立。新たに公開する Workflow の数と既にある Workflow は、単位の構成と target の観測（どちらもメモリ上のモデル）だけで決まるので、保留の判定もここに置く | 0 / 約200（r4 で約+10） |
| 新規 `tools/Artifacts/StoreConsolidation.psm1` | 対象 role の棚卸し、staging、単位の公開、journal と receipt、run の排他を行う | 公開の手順・障害回復・ACL の扱いが変わったとき | run directory（lock、manifest の写し、journal、staging、receipt）を run の寿命で所有する。receipt は不変記録として残す。Codex 側は読取り専用 handle だけ | `StoreConsolidationPlan.psm1`、`WindowsStoreContext.psm1`（自身も非仮想化でなければ実行しない）、`WindowsStorePaths.psm1`、`ArtifactAcl.psm1`。Evidence / Workflow の domain module は import しない（テストからは読取り確認に使う） | `artifacts.ps1 store plan --source-root ...`、`store consolidate --manifest ... --manifest-sha256 ... [--dry-run]`（仮称） | 合成 fixture の両 root、I/O seam（open 記録）、4点の障害注入（AC6〜AC10、AC12。r4 の複数 Workflow の単位が一つも公開されないこと、実行時の再棚卸しで増えた場合を含む） | Artifacts 側の一回の管理操作として、`EvidenceReset.psm1` と同じ形に揃える（A2 の支持）。Workflow / Harness の directory も bytes 同一で扱うだけで、その domain の判断は持たない。r4 の保留は計画の結果に従うだけで、この module に新しい判断を置かない | 0 / 約300（r4 の増分はテストだけの見込み） |
| テスト（新規 suite 3本） | 判定と入口、計画、合流 | 上記と同じ | テスト専用 root | 既存 TestSupport | なし | offline のみ | 既存 suite の形に合わせる | 0 / 約550 |
| `tools/Artifacts/README.md`、`tools/Workflow/README.md`、`tools/Harness/README.md` | 実行条件、reasonCode、合流手順。r4 で、公開途中の不可視性の前提（新たに公開する Workflow が1つで、単位の Workflow が実ストアに無い）と、`multi-workflow-unit`・`workflow-already-visible` の保留を、reason の一覧とともに Artifacts README の合流節に書く | 公開面の現況 | 永続 | なし | 文書 | docs-audit | harvest 先 | 既存 / 約+40 |

行数警報: `StoreConsolidation.psm1` は 300 行前後の見込みで、500 行の警報には届かない。超える場合は「棚卸し」と「公開・回復」の分割を C で判断する。`EvidencePaths.psm1`、`ArtifactPaths.psm1`、`TaskEventStore.psm1`、`RecordStore.psm1`、`CredentialPathAcl.psm1` は変更しない（判定を入口へ置くため）。

## 4. 実装計画（r4 で A3 の凍結対象）

- 順序: (1) 判定 module と注入 seam → (2) 三つの CLI 入口 → (3) offline test（AC1〜AC3）→ (4) 計画 module とメモリ fixture（AC13）→ (5) 合流 module の棚卸しと plan → (6) staging・公開・journal・receipt・run 排他 → (7) offline test（AC6〜AC10、AC12）→ (8) README。
- r4 の追加分（実装済みの head に対する差分として行う）: (4) に `multi-workflow-unit`・`workflow-already-visible` の保留とメモリ fixture、(7) に保留した単位が一つも公開されず staging・receipt も作られないこと、承認後の構成変化の offline test、(8) に README の公開途中の不可視性の限定と保留の説明。
- Phase B から Phase A へ差し戻す条件: 入口以外（各 Application module・resolver）に判定を入れないと I1 を満たせない場合。合流で bytes の書換えが必要と判明した場合。directory 単位の非上書き rename が使えず、公開順で単位を隠せない場合。新しい保存先・資格情報の扱いが必要になった場合。判定に package manifest の解析など新しい依存が必要になった場合。
- 対象外の維持: 上記の既存 resolver・ACL 規則を変えない。Codex 側への書込み API を持たない。

## 5. テストとレビュー計画

### 5.1 テスト

- 単体テスト: 判定（3値と各失敗経路、identity の有無で分岐しないこと）、入口の順序（判定がストア I/O より前）、import 時の副作用（AC14）、計画（AC13）、合流（AC6〜AC10、AC12）。時間待機を使わず、並行は既存 suite と同じ別 process の signal 同期。
- r4 の保留規則の検証例（case 名は仮称。既存 suite に足す）:
  - 計画 suite（メモリ fixture）: `multi-workflow-unit-held`（同じ operation を参照する二つの task が一つの単位になり `multi-workflow-unit`）、`workflow-already-visible-held`（単位の Workflow が実ストアに bytes・ACL 同一で既にあり、他に公開する item があれば `workflow-already-visible`。一方の Workflow だけが既にある結合単位も同じ）、`all-identical-unit-not-held`（全 item が identical の単位は保留せず skip / complete）、`only-new-workflow-not-held`（結合単位の一方の Workflow が identical、他方の Workflow だけが absent で他は全て identical なら保留しない）、`single-workflow-unit-not-held`（新たに公開する Workflow が1つで実ストアに無い単位、Workflow を持たない task との結合は保留しない）、`multi-workflow-with-other-reasons`（`reference-missing` 等と併存し、他の理由を消さない）。
  - 合流 suite（隔離 fixture）: `multi-workflow-unit-not-published`・`workflow-already-visible-not-published`（その単位の transfers・Evidence・Harness・Workflow のどれも実ストアに新たに現れない、run directory にその単位の staging・receipt が無い、run は `partial`・exit 2、実行の前後と途中の `Get-WorkflowTasks` に、その実行によってその単位の task が新たに現れない、同じ run の他の単位は公開される、Codex 側の hash・時刻は不変）、`unit-changed-after-approval`（承認時 Workflow 1つの単位が実行時に2つになれば `source-changed` で一つも公開しない。承認時に保留だった単位は、実行時に理由が消えても公開しない。承認後に別の writer が Workflow を同一 bytes で置いた単位は `target-changed` で staging しない）、`interrupted-then-changed`（AC10 (c) で止まった後に構成が変わった単位は `source-changed`、公開済みの断片は残り、task は一覧に現れない）。既存の `hidden-until-workflow` は Workflow 1つの単位の検証として残す。
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
| AC11 | （r4 で後続 `Artifact Storage / Split Store Recovery` へ移した。§1.5） | — | — | — | — |

### 5.4 未知の操作経路の疎通

- **確認済み（2026-10-12）:** D1 の書込み probe（r1 の置き場所は OneStarMaker 配下）。Codex 内（Codex が起動した PowerShell 7.6.5、承認済みの sandbox 外経路）では、実体 path が `Packages\<Codex package>\LocalCache\Local\OneStarMaker\…` で論理 path と不一致、identity は無し（15700）。Claude 内（Claude Code の PowerShell）では実体 path が論理 path と一致し、identity は Store 版 PowerShell のものだった。Claude 側は probe directory も削除し、OneStarMaker 直下の entry 数は3のまま変わらなかった。Codex 側は一時 file を削除したが、空の probe directory が1つ残った（直下の entry 数は7から8。既存 entry の属性・更新時刻は不変）。その削除は Codex 側の担当の判断に委ねる。どちらの観測も R2 の通信・認証は含まない。
- **一部確認（2026-10-13）:** r2 の probe 領域の Claude 側（下記 (b)(c) の Claude 内分）。Claude Code の PowerShell で `OneStarMaker.StoreProbe` に `CreateNew` + `DeleteOnClose` の file を作ると、final path は論理 path と一致し、handle を閉じた後に file は残らなかった（probe 領域の entry 0件）。疎通のために作った probe 領域の directory は削除した。
- **確認済み（2026-10-13）:** r2 の probe 領域の Codex 側。Codex 内（承認済みの sandbox 外経路、PowerShell 7.6.5）で `OneStarMaker.StoreProbe` に `CreateNew` + `DeleteOnClose` の file を1回作ると、final path は `Packages\<Codex package>\LocalCache\Local\OneStarMaker.StoreProbe\<固有 file>` で論理 path と不一致だった。handle を閉じた後、file は論理側・LocalCache 側とも残らなかった（明示的な削除はしていない）。疎通で作った probe 領域の directory は両側とも事前に無く、終了時に削除して両側とも残っていない。上の Claude 側の結果と合わせ、D1 の probe 領域（ストア外の兄弟）と `DeleteOnClose` の後始末は両 context で成立した。
- **確認済み（2026-10-12、owner のセッション許可あり）:** U3。両 context で `credentials status` を1回ずつ実行した（世代 ID と endpoint は転記しない）。
  - 実ストア（Claude 内）: exit 0、`rotation state: stable`、retired 世代なし、updated UTC 2026-10-10。
  - Codex 側（Codex 内）: exit 0、`rotation state: stable`、retired 世代なし、updated UTC 2026-10-05。
  - 二つは別々の record で、どちらも rotation 途中（`pending-revocation`）ではない。Codex 側の record の方が古い。`status` はローカルの記録だけを読むので、R2 で有効かは示さない。コードを読んだ範囲では、`status` は既存の資格情報 directory では検査だけを行い、何も作らない。
  - 合流への影響: 資格情報は合流対象外で（D4）、旧 S4 の fetch は実ストアの鍵を使う。実ストアの鍵が R2 で有効かは、取得の回復を確かめる時点で初めて分かる（r4 で後続 `Artifact Storage / Split Store Recovery` へ移した。§1.5）。認証で失敗した場合も、鍵の再入力・削除で直さず owner へ返す（常時契約）。
  - 後続への入力: 同じ bucket に有効な token が二つ存在しうる（Codex 側の record が別の token を保持している可能性）。失効の要否は owner が管理画面で判断する。本スライスでは扱わない。
- **一部確認（2026-10-12）:** U5 の path 文字列。Codex 内で catalog 1件を読取り専用で開き、絶対 path 12個が全て論理 path だった（値は転記しない。サイズ・更新時刻は不変）。全単位の検査は D2 の `plan` が機械的に行う。

### 5.5 高コスト証拠

r3 は「実ストアへの合流の実行と S4 の fetch」を一つの高コスト証拠にしていた。r4 で S4 の fetch を後続へ移し、残る「実ストアへの合流の実行」の置き場は **A3 で案 B（後続へ送る）に決定した。本スライスの高コスト証拠は なし** で、判定証拠が揃った head の C' を最終 C' とする。下記の手順・期待値と選択肢は、後続へ引き継ぐ内容と判断の記録として残す。

**合流の実行の手順と独立した期待値**（どちらの案でもこの内容で凍結・移送する。A2-2 で範囲を限定）:

- 手順: `store plan` → owner が manifest を承認 → dry-run → 実行 → 単位 receipt → 公開後の確認（D2）。実ストアを変更するので高コスト証拠に分類する。
- 期待値:
  - 公開先の各 file の SHA-256 が manifest と一致する。
  - Codex 側は、**manifest に載った file だけ**を実行前後で hash 照合し、一致する。
  - 除外領域（資格情報、非所有 directory、`RevisionLocks`、対象外 role）は内容を開かない。実行前後の名前・件数・最終更新の列挙が同一で、合流 run の journal の open 記録にそれらの path が無い。
  - 衝突・参照欠落・`multi-workflow-unit`・`workflow-already-visible` の単位数が dry-run と一致し、保留が残れば run の結果が `partial` である。それらの保留単位の file が実ストアに新たに一つも無い。
  - 公開した全単位で、公開後の確認（D2）の3コマンドが既存どおり成功する。

**選択肢:**

| 案 | 内容 | 判定の順序 | 利点 | 欠点 |
|---|---|---|---|---|
| A | 合流の実行を本スライスの高コスト証拠として残す（fetch は含めない） | 判定 C → 暫定 C' → 合流の実行 → 最終証拠ラウンド（判定 C と最終 C'） | 実データが S2 の分類規則・S3 の公開手順を通ることを本スライスで見られる。後続は fetch から始められる | 実ストアを変更する不可逆な操作が GO の前提になり、本スライスの GO が実データの状態（保留の多さ、ACL）と owner の承認の時期に依存する。暫定 C' と最終 C' の2回が要る |
| B | 合流の実行も後続 `Artifact Storage / Split Store Recovery` へ送る。本スライスの高コスト証拠は **なし** | 判定 C → C'（判定証拠が揃った head の C' を最終 C' とする） | 本スライスの GO が、凍結した offline 検証と実環境の AC4・AC5 だけで決まる。実ストアの変更を GO の後、取得の回復と同じ判断の中で一度だけ行える | 実データで初めて見つかる分類の問題（ACL、JSON 検査、複数 Workflow の該当）は後続で扱う |

**推奨は案 B（A3 で採用）。** 根拠:

- S1〜S3 の証明に実データの合流は要らない。S1 は AC1（全入口の offline 検査）と AC4（Codex 内の実環境確認。実ストアを変更しない）で、S2 は AC9・AC13 の合成 fixture で、S3 は AC6〜AC10・AC12 の合成 fixture で判定する。中断からの収束、衝突・保留時の非公開、Codex 側の不変、複数 Workflow の単位の非公開は、障害注入と境界条件の組み立てが要るので、実データの一回の実行では観測できず、合成 fixture の方が強い証拠になる。
- 実データの一回の実行が加えるのは「実際の記録が保留されずに通るか」で、これは取得の回復（後続へ移した旧 S4）が成り立つかの問いに属する。実データで保留が出たときに規則を変えるかの判断（I6 や検査規則の意味の変更）も、取得の回復と同じスライスで決める方が判断が一か所に閉じる。
- 実ストアの変更は不可逆で、owner の manifest 承認を要する。GO の前提にすると、本スライスの判定が実データの状態と承認の時期に縛られる。

**判定 C の照合 script の方式:** **なし**（案 B を採ったため。照合する高コスト証拠の原記録が本スライスに無い）。r3 の「A3 凍結 script」の方針（判定 C の担当が非仮想化かつ資格情報へ届かない環境で script を書ける保証が無いため、A1 で書き A3 で凍結する）は、合流の実行と一緒に後続へ移す。

### 5.6 担当・モデル（実績）

- A0/A1 主担当: Claude Code（Claude Opus 5.5、Anthropic）。非仮想化の Claude デスクトップから、ディレクトリ名の列挙、コードの読取り、§5.4 の probe と `credentials status`（owner 許可あり）だけを行った。Artifact Storage の操作、ストアの変更は行っていない。
- §5.4 の Codex 内疎通: Codex（OpenAI）。疎通の観測だけで、A2 のレビューではない。
- A2 独立レビュー 1件目: Codex（OpenAI）、新しいセッション。観点はアーキテクチャゲート（責務マップ、I1〜I7 を破る順序、D1〜D5）。入力版は r1 の内容 sha256 `4461c4a9ec0dc0b5ff43d6f9dd4b271af2de1f49a4aa0f084714553552c7dd3e`（commit `08cb1d1`）。開始時に一致を確認済み。結果は §5.7。
- A2 独立レビュー 2件目: Codex（OpenAI）、新しいセッション。観点は A0 だけからの代替構成。入力は commit `b96c358` の §1 だけ（抽出した §1 の UTF-8 の sha256 `a92e42097aa833b90e70d5658a1741a1fba317fcefd24fee54ba7a6efc00cee9`、開始時に一致を確認済み）。§0・§2 以降・1件目の指摘は見ていない。結果は §5.8。同じベンダーの2件目なので、独立性は「別セッション・別入力」による。
- A3 統合: Claude Code（Claude Opus 5.5、Anthropic）が統合案を作り、owner が 2026-10-13 に推奨どおり採用して凍結した（§5.9）。
- C' 用に予約する担当: Anthropic・OpenAI 以外の系列（候補は cursor-agent の Grok 系）。A2 では使わない。
- r4 の A1 主担当: Claude Code（Claude Opus 5.5、Anthropic）、新しいセッション。HANDOFF とコードの読取りだけを行い、ストア・Artifact Storage・資格情報・R2 には触れていない。r4 の A2 と統合差分の再レビューの実績は §5.11 と下記。A3 の統合は主担当（同上）が行い、owner が決定した（§5.12）。

### 5.7 A2 1件目の findings と採否（A3 で確定）

| # | severity | 指摘 | 根拠の節 | r2 の扱い | 理由 |
|---|---|---|---|---|---|
| A2-1 | P1 | probe の書込みが I1 の「ストアへの最初のアクセスより前」と AC1 の「I/O seam 0件」に矛盾。削除失敗時に残骸が増える | I1、AC1、AC4、D1、§3 | 採用 | probe をストア外の probe 領域へ移して I1 の境界を明示。`DeleteOnClose` で例外経路の後始末を OS に任せ、強制終了の残骸は判定 module が自分の命名規則に合うものだけを消す。§3 の責務を「OS metadata だけ」から「probe の file・handle・後始末の所有」に修正。ストア外での検出は §5.4 で疎通を追加 |
| A2-2 | P1 | 高コスト証拠の「Codex 側の全 file の SHA-256」は I7 の除外領域を読ませる | §1.4、I7、§5.5 | 採用 | hash 照合を manifest の file に限定し、除外領域は名前の列挙と journal の open 記録で確認する形に変更。I7 に hash 計算も読取りであると明記 |
| A2-3 | P1 | file の途中で止まると、非上書き・rollback 禁止のため再実行で回復できない | I4、I5、S3、AC10、§3 | 採用 | run 所有の staging → flush → hash 照合 → directory 単位の非上書き rename で公開する方式を D2 に固定。AC10 に4つの障害注入点を追加 |
| A2-4 | P1 | 合流中の既存 writer（定期 job の sync 等）と role をまたぐ公開順の管理者が無い | I4、S3、S4、D2、D4、§3、§4 | 採用 | 公開順を Workflow 最後に固定し、job と cleanup が Workflow の task 一覧を起点にすることをコードで確認した。同じ taskId の対話 CLI との競合は `concurrent-writer` で止める。run の二重起動は lock で止める。管理者は合流 module（run directory の所有者）。AC12 を追加 |
| A2-5 | P2 | U5 の全件検査と manifest 作成の責務が抜けている | U5、I5、I7、D4、§3、§5.4 | 採用 | 計画（純粋関数）を `StoreConsolidationPlan.psm1` に分離し、role に依存しない参照検査の規則を D2 に書いた。AC13 を追加 |
| A2-6 | P2 | 判定不能時の fail-closed と、I3 の完全互換が両立しない | I3、AC2、AC3、D1 | 採用 | I3 を「非仮想化と判定できた実行」に限定し、判定不能を別 reasonCode `store-context-unknown` にした。それが意図した唯一の挙動変更であることを I3 に明記 |

A2 が示した I1〜I7 の反例（guard の後に判定、汎用 catch で `unconfirmed` 化、bytes 同一 skip で ACL を見落とす、列挙前に除外領域を hash 等）は、AC1・AC2・AC6・AC8・AC9 の検証内容に反映した。配置について A2 は、判定 module を Workflow 基盤層に置く案と、合流 module を Artifacts 側に置く案を支持した。Workflow の ACL 規則が `TaskEventStore` の内部関数である点は、Artifacts の ACL 規則と同一であることを確認したうえで `ArtifactAcl.psm1` を使う、とした（I6）。

### 5.8 A2 2件目（A0 だけからの代替構成）との突合と採否（A3 で確定）

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

### 5.9 A3 の決定（2026-10-13、owner）

owner は主担当の統合案（D1〜D6、運用前提、§5.7・§5.8 の採否）を推奨どおり採用し、r3 を凍結した。

| 項目 | 決定 |
|---|---|
| D1 | 仮想化は `virtualized-store`、判定不能は `store-context-unknown` で fail-closed（exit 1）。判定はストア外の probe 領域だけを使う書込み probe で行い、package identity は使わない |
| D2 | owner が承認した manifest の単位だけを、非仮想化 context から実ストアへ bytes 同一で非破壊に合流する。計画（純粋関数）と I/O を分け、run 所有の staging から directory 単位の非上書き rename で公開し、Workflow を最後に公開する。衝突・分岐は勝者を選ばず保留し、残れば `partial` |
| D3 | GO 後は仮想化された host で Storage・Workflow・Harness を操作せず、非仮想化の担当が実行担当を務める。host 側の経路は後続 `Virtualized Host Route` |
| D4 | 合流対象は `workflow`、`evidence-v2`、`deployments`、対象 task が参照する `transfers`、`harness`。`builds-v1`・`runtime`・`credentials`・`RevisionLocks`・非所有 directory は対象外 |
| D5 | 従来の tracked HANDOFF で進め、H1 へ移行しない |
| D6 | 判定 module は `tools/Workflow/` の OS 基盤層に置き、Harness → その基盤層の依存 edge を許可する。`tools/WindowsStore/`・`tools/StoreMerge/` の新設は採らず、OS 基盤層の移動は後続へ送る |
| 運用前提 | 合流 run の間、対象 taskId を対話 CLI で操作しない。違反時は `concurrent-writer` で止まることを AC12 で保証する（A2b-7 の代替） |
| §5.7 | A2-1〜A2-6 を全て採用 |
| §5.8 | A2b-1〜A2b-6 を採用、A2b-7 は不採用（運用前提で代替）、A2b-8 は D6 のとおり不採用、A2b-9 は不採用 |

A3 後の例外承認・決定記録はなし。凍結条件を変える判断は Phase A の新しい revision として行う。

### 5.10 r4 の入力と r3 からの変更点（上の表は A1 草案の時点。A3 の統合で加えた変更は下の表で、上の表と重なる行は下の表が優先する）

**入力（owner の裁定、2026-10-13）:**

1. 複数の Workflow task を含む合流単位は公開せず、理由付きで保留する。AC12 の「公開途中のその単位は列挙されない」は単位ごとの意味のまま保つ。保留した単位は run を `partial` にする（S3 の既存規則）。保留した単位の扱いは後続へ送る。
2. 合流後の fetch による取得の回復（S4）と AC11 を本スライスから後続スライスへ移す。本スライスの GO 条件は I1〜I7 と S1〜S3 にする。

変更は上の裁定に必要な範囲に限り、それ以外の凍結条件（I1〜I7 の他の文、S1、AC1〜AC10・AC13 の既存の検証内容、AC14、D1・D3〜D6、§3 の配置・依存、§4 の差し戻し条件、§5.1 の起点 filter と判定必須テスト）の意味を変えない。

| 節 | 旧（r3） | 新（r4） | 理由 |
|---|---|---|---|
| §1.1 問い | 合流させて、Codex が publish した Evidence を非仮想化の担当が取得できる状態に戻せるか | 承認した単位だけ非破壊に合流させる手段を、合流できない単位を理由付きで保留する形で用意できるか | 取得の回復は実データと資格情報の有効性に依存し、合流の手段の正しさとは別に判定できる（裁定2） |
| §1.3 最低条件 | 合流して固定入力を取得できること | 計画と実行の手段があり、合流できない単位（複数 Workflow を含む）を保留して全体完了を返さないこと | 裁定1・2 |
| §1.4 対象外 | — | 取得の回復と保留した単位の扱いを追加。合流の実行の置き場は §5.5 | 裁定1・2 |
| §1.5 後続 | `Virtualized Host Route` 等 | `Split Store Recovery` の新設を推奨し、旧 S4・旧 AC11・合流の実行（案 B の場合）・保留した単位の扱いを受ける。program §4 の harvest 先を明示 | 移送先の受け皿。host 経路の設計とは変更理由・リスク・急ぎ方が違う |
| §2 用語 | 合流の単位 | 結合で Workflow が2つ以上になりうること、Workflow の数え方（skip するものも数える）を追加 | 公開の可否を実ストアの状態に依存させないため |
| §2.1 I4 | 不一致の単位の file を一つも公開しない | Workflow を2つ以上含む単位も一つも公開しない、を追加 | 公開順（Workflow 最後）が不可視性を保証するのは Workflow 1つの単位だけ（裁定1） |
| §2.2 S2 | 候補・衝突・参照欠落・除外 | 複数 Workflow の分類と `multi-workflow-unit` の表示を追加 | 裁定1 |
| §2.2 S3 | 衝突・参照欠落の保留で `partial` | 保留理由に `multi-workflow-unit` を追加 | 裁定1（`partial` は既存規則のまま） |
| §2.2 S4 | 取得の回復 | 欠番。後続へ移した | 裁定2 |
| §2.3 AC11 | S4 の fetch（高コスト） | 欠番。後続へ移した | 裁定2 |
| §2.3 AC12 | 公開途中に列挙されない、`concurrent-writer` | 単位ごとの意味を明記し、複数 Workflow の単位の非公開と実行時の再棚卸しで増えた場合の失敗経路を追加。S3 を検証対象に追加。合格例・不合格例を追加 | 裁定1。保留が「一部だけ公開」に読み違えられないようにする |
| §2.3 AC13 | 共有 operation の二つの task を結合 | 結合した単位を `multi-workflow-unit` で保留。保留しない境界（Workflow 1つ）と、実ストアに同一 entry がある場合も保留することを追加 | 裁定1 |
| §2.5 GO | I1〜I7、S1〜S4 | I1〜I7、S1〜S3。保留が残る `partial` は S3 どおりなら NO-GO の理由にならない | 裁定2 |
| §2.6 D2 | 公開順で不可視、結合と保留 | 公開順の保証の前提（Workflow 1つ）、`multi-workflow-unit` の保留規則（判定の位置、実行時の再検査、既存の伝播、deployment 単位との関係）、保留理由の独立性を追加 | 裁定1。新しい状態・公開 API・依存・gate を増やさない |
| §3 | 計画・合流・README の行 | 計画に保留の判定、合流は計画の結果に従うだけ、README に不可視性の限定と保留の説明 | 裁定1の責務の置き場 |
| §4 | 順序 (1)〜(8) | r4 の追加分を (4)(7)(8) に足す | 裁定1 |
| §5.1 | 単体テスト | 保留規則の検証例（計画4件、合流2件）を追加 | 裁定1 |
| §5.3 AC11 行 | 非仮想化の担当の fetch | 後続へ移した | 裁定2 |
| §5.4 U3 | S4 の fetch | 旧 S4、後続へ移したことを注記 | 裁定2（観測記録は変えない） |
| §5.5 | 合流の実行と S4 の fetch を高コスト証拠、A3 凍結 script | fetch を外した合流の実行の手順・期待値（`multi-workflow-unit` を追加）と、置き場の案 A / B。推奨は案 B（高コスト証拠なし、照合 script なし、どちらも後続へ） | 裁定2。S1〜S3 の証明は合成 fixture で足り、実データの実行が加えるのは取得の回復の成否 |
| §0・§5.6 | r3 の引継ぎ | r4 草案の状態、A2 の入力と担当 | 手続き |

**A3 の統合で加えた変更（§5.11 の採用分。A1 草案 `2ba64e1` からの差）:**

| 節 | A1 草案 | 統合版 | 理由 |
|---|---|---|---|
| §2 用語 | 単位が含む Workflow の数（skip するものも数える） | 単位が新たに公開する Workflow の数（absent / partial）。既にある Workflow は `workflow-already-visible` で扱う | 完了済みの単位を恒久的な保留にしない。既にある Workflow の不可視性は別の規則で守る |
| §2.1 I4 | 複数 Workflow の単位は一つも公開しない | 加えて、公開する item があるのに単位の Workflow が実ストアに既にある単位も一つも公開しない | Workflow を最後に置く公開順は、Workflow が実ストアに無い場合にだけ不可視性を保証する（裁定1の「AC12 は単位ごとの意味のまま」を保つ） |
| §2.2 S2・S3 | `multi-workflow-unit` | `workflow-already-visible` を追加。S3 の「完了」を「承認時に保留でない全単位が published / complete」と定義 | 保留がある run の収束の意味を定める |
| §2.3 AC10 | 4点の障害注入 | (c) で止まった後に構成が変わった単位は `source-changed` で止まり、断片は不可視のまま残る、を合格の状態として追加 | 収束の読み違いを防ぐ |
| §2.3 AC12 | 複数 Workflow の非公開、`source-changed` または `multi-workflow-unit` | 観測の基準時点（実行で新たに列挙されない）、既にある Workflow の場合、staging・receipt が無いこと、全 identical は保留しない、承認後の変化の両方向（構成が変われば `source-changed`、承認時の保留は理由が消えても公開しない）。不合格例を追加 | 二択の検証を一意にし、保留の副産物を定める |
| §2.3 AC13 | identical の Workflow も数えて保留 | 新たに公開する数で `multi-workflow-unit`、既にある Workflow と公開する item の併存で `workflow-already-visible`、全 identical は保留しない | 用語の変更に合わせる |
| §2.6 D2 | 複数の Workflow task を含む単位の保留 | 「公開途中に task 一覧へ見える単位の保留」に改め、二つの reasonCode、staging・receipt・journal の扱い、承認後の変化、reasonCode の値の追加（schemaVersion は不変）を明記 | 同上 |
| §1.5 | 所有スライスの案、合流の実行は案 B の場合 | `Split Store Recovery` の新設を決定。最初の手順を読取りだけの実データ `store plan` とする。対象の単位が保留されたときの分け方を足す | A3 の決定（§5.11 補足1・2） |
| §3・§4・§5.1 | `multi-workflow-unit` | 二つの reasonCode、公開面の値の追加の明記、検証例の更新 | 同上 |
| §5.5 | 案 A / B と推奨 | 案 B に決定。本スライスの高コスト証拠と照合 script は なし | A3 の決定 |
| §2 用語、§2.3 AC12・AC13、§2.6 D2、§5.1 | 統合版 `d51edae` | 再レビュー（§5.13）の反映: Workflow の partial は生じないことの明記、AC12 の観測基準を公開対象と保留で分ける、`workflow-already-visible` の「公開する item」を Workflow 以外と定め境界の例を追加、承認時に公開対象だった単位が実行時に保留条件を満たせば `target-changed`、承認時の保留は理由の種類にかかわらず公開しない（既存規則と同じ）、検証例 `only-new-workflow-not-held` の追加、見出し・重複文・誤字の整理 | 仕様を一意にし、B の解釈の分岐を防ぐ。新しい状態・API・依存・reasonCode は増えない |

本 r4 は、凍結した参照先の検査（D2 (iii)）を変えない。参照先の存在・読取り・ACL を確かめる既存の検査と `multi-workflow-unit` は独立した保留理由で、一方が他方を置き換えない。

### 5.11 r4 の A2 独立レビューと採否（2026-10-13 に A3 で owner が採否案どおり確定）

- **担当・入力:** Claude Code のサブエージェント（Claude Sonnet 5.5、Anthropic）、新しい文脈。B・r4 A1（Claude Opus 5.5）と異なるモデルだが**同じベンダー**で、独立性は「別モデル・別文脈・読取り専用」による（独立性制約あり）。Fable 5.1 は利用枠の都合で起動できなかった。C' 用の予約系列は使っていない。入力版は commit `2ba64e18e38480177300885ee4f4c6bdff234977`、内容 sha256 `d3951934fd5c0872583948846f72b37e6f2f46fb5918fe0c43131183da52ff77`（開始時に一致を確認）。§6・§7 は判断材料にしていない。編集・テスト・ストア操作なし。
- **観点1（裁定の反映）:** 指摘なし。§5.10 の表は r3 からの差分を漏れなく記述している。
- **観点4（アーキテクチャゲート）、deployment 単位の先行公開、他の保留理由との併存:** 指摘なし。
- **§5.5:** 案 B の推奨に同意。

| # | severity | 指摘 | 該当節 | 採否案 | 理由・修正案 |
|---|---|---|---|---|---|
| R4A2-1 | P1 | 単位の唯一の Workflow が実ストアに bytes・ACL 同一で既にある場合、実行は identical の item を skip し、他の item（Evidence・Harness 等）だけを公開する。公開の間、その task は `Get-WorkflowTasks` に列挙され、途中で止まればその状態が残る。「Workflow を最後に置く」は単位の Workflow が実ストアに無い場合にだけ不可視性を保証する（r3 から継承。A1 が実装 `StoreConsolidation.psm1:378` で確認） | I4、AC12、D2 | **採用（修正案 (a)）** | 裁定1の「AC12 は単位ごとの意味のまま保つ」に従い、保証を限定する (b) は採らない。単位に公開する item があり、かつ単位の Workflow が実ストアに既にある（identical を含む）場合は、その単位を新しい reasonCode `workflow-already-visible` で保留する。全 item が identical の単位は従来どおり skip / complete |
| R4A2-2 | P2 | 「skip する Workflow も数える」は、完了済みの単位に新しい task が結合しただけで恒久的な `hold` にする。AC12 の「どちらの task も現れず」は、片方が既に実ストアにある例と矛盾する | §2 用語、AC12、AC13、§5.1 | **採用** | R4A2-1 (a) と合わせて数え方を「実行で新たに公開する Workflow（absent / partial）」に改める。既にある Workflow は R4A2-1 の規則で扱うので不可視性は保たれる。AC12 の観測を「実行の前後と途中で、その単位の task が新たに列挙されない」に書き直す |
| R4A2-3 | P2 | AC12 の「`source-changed` または `multi-workflow-unit`」は二択で検証が定まらない。実装では unitId が item 集合の hash なので、Workflow が 1→2 になれば承認済み単位は `source-changed` に一意に決まる。逆方向（承認時 hold、実行時 1 に減る）の記述が無い | AC12、§5.1 | **採用** | 1→2 は `source-changed`（新しい構成の単位は未承認なので公開しない）。承認時に hold の単位は、実行時に保留理由が消えても公開せず、再 plan・再承認を要する。両方向を AC12 と §5.1 の例に入れる |
| R4A2-4 | P2 | 保留の判定と staging・journal・receipt の前後関係が曖昧 | D2 | **採用** | 保留した単位は staging せず、単位 receipt を作らず、journal には保留の trace だけを残す、と明記する。AC12 に「保留単位の staging・receipt が run directory に無い」を足す |
| R4A2-5 | P2 | S3 の「完了へ収束」が保留込みで未定義。公開途中で止まった単位が、再実行の前に Codex 側の変化で複数 Workflow になった場合の扱いも例が無い | S3、AC10 | **採用** | 「完了」は「承認した単位のうち公開対象の全単位が published / complete」と一文で定義する。上の場合は単位が `source-changed` で止まり、公開済みの断片は削除せず（I4）Workflow 未公開のため不可視のまま、が正しいことを AC10 の合格例に1行足す |
| R4A2-6 | P3 | `multi-workflow-unit` は manifest JSON の reason 値の追加で承認者に見える。「公開 API を増やさない」の範囲を明記すべき | §3、README 行 | **採用** | 「reasonCode の値の追加は含む（公開関数・引数・schemaVersion は変えない）」と明記し、README の合流節に reason の一覧として書く |
| 補足1 | P3 | 案 B では、DebugStudio の固定入力を含む単位が保留されても本スライスは GO になり、発見が遅れる。読取りだけの `store plan` を非ゲートの参考として GO 前に1回見る運用を A3 が選べる | §5.5 | **不採用（本スライスには入れない。後続へ）** | owner が主担当の推奨どおり決定。実データの plan は `Split Store Recovery` の最初の手順（A0 の現況確認）に置く。判定を実データに依存させない案 B の趣旨を保つため |
| 補足2 | P3 | `Split Store Recovery` の中の「保留した単位の扱い」は reader 契約・gate の設計変更を要し、急ぎの回復と変更理由が異なる | §1.5 | **採用** | §1.5 に1行足す: 対象の単位が保留された場合は、保留した単位の扱いを別の設計スライスに分けるか、回復を先に別の手段で行うかを、そのスライスの Phase A で決める |

**A3 への注記:** R4A2-1〜5 を採用すると、§2.1 I4、§2.2 S3、§2.3 AC10・AC12・AC13、§2.6 D2、§3、§5.1 の本文と §5.10 の表が変わる。新しい保留理由 `workflow-already-visible` は fail-closed の範囲を広げるので、裁定1と同じく owner の承認が要る。新しい状態・公開関数・依存・gate は増えない。統合後の本文を r4 として凍結するか、統合版を A2 に再度かけるかは owner が決める（主担当の推奨は、R4A2-1 が P1 なので統合版の差分だけを短く再レビューすること）。

### 5.12 r4 の A3 の決定（2026-10-13、owner）

owner は次を主担当の推奨どおり決定した。

| 項目 | 決定 |
|---|---|
| §5.11 | R4A2-1〜6 と補足2を採用（R4A2-1 は修正案 (a)、新しい reasonCode `workflow-already-visible`。fail-closed の範囲の拡大を承認）。補足1は本スライスに入れず、`Split Store Recovery` の最初の手順にする |
| §5.5 | 案 B。実ストアへの合流の実行は後続へ送り、本スライスの高コスト証拠と照合 script は なし |
| §1.5 | 後続 `Artifact Storage / Split Store Recovery` を新設する |
| 凍結の手順 | 凍結の前に、統合差分（A1 草案 `2ba64e1` → 統合版）を B・A1 と異なるモデルで短く再レビューする。阻害する指摘が無ければ r4 を凍結する |
| 凍結 | 再レビュー（§5.13）は P1 なし。P2・P3 を本文へ反映して r4 を凍結した（凍結 commit と内容 sha256 は §0） |

A3 後の例外承認・決定記録はなし。凍結条件を変える判断は Phase A の新しい revision として行う。

### 5.13 統合差分の再レビュー（2026-10-13）

- **担当・入力:** Claude Code のサブエージェント（Claude Sonnet 5.5、Anthropic）、新しい文脈、読み取り専用。B・A1 と異なるモデル、同じベンダー（独立性制約あり）。入力は統合版 commit `d51edae6a260c4d1eeaf89f6589d2e388b579077`（内容 sha256 `2b2fab42c37b732637b5bf9f79ad4e81e08bd679ceb5b2fad6448b32945ecd23`、開始時に一致を確認）と `2ba64e1` との差分。
- **結果:** 統合の正確さは指摘なし。自分の中断から `multi-workflow-unit` / `workflow-already-visible` が生じる順序は見つからなかった。凍結を阻害する P1 なし。

| # | severity | 指摘 | 扱い |
|---|---|---|---|
| R4RR-1 | P2 | AC12 の観測基準「どの時点でも新たに列挙されない」が、正常に公開した単位（Workflow の rename 後に列挙される）と矛盾する | 採用。公開対象と保留した単位で基準を分けた |
| R4RR-2 | P2 | 承認時に公開対象だった単位が、承認後の外部 writer で `workflow-already-visible` の条件になった場合の結果が未定義 | 採用。`target-changed` で止め staging しない（既存の再検査の規則と同じ）と D2・AC12 に明記 |
| R4RR-3 | P2 | 「他に公開する item」に新たに公開する Workflow が含まれるかが曖昧 | 採用。Workflow 以外の item と定め、Workflow だけが残る単位は保留しない境界の例と検証例を追加 |
| R4RR-4 | P2 | 用語の「absent または partial」で、Workflow が partial になりうると読める | 採用。Workflow は directory 単位の rename で partial にならず、partial は deployment の対だけと明記 |
| R4RR-5 | P2 | 「承認時の保留は理由が消えても公開しない」の適用範囲が不明 | 採用。理由の種類にかかわらず適用し、既存の規則（承認時 hold の単位は実行時も hold。`Test-ConsolidationManifest`）と同じで r3 からの意味の変更ではないと明記 |
| R4RR-6 | P3 | D2「公開順と並行 writer」の前提の重複文 | 採用。一文に統合 |
| R4RR-7 | P3 | 見出し・注記の取り残し、誤字、決定済みの案 A の注記 | 採用。整理した |

## 6. Phase B 実装結果

- 実施日: 2026-10-13。開始時に、リモート head `97145dc08a183e1f1893751f6a86925a3acda1da` と本ファイルの内容 sha256 `c7259ad70ce241ed323a2169c4f5f82a2be4598206bfd2e6b286a6fa8342197f` の一致を確認した。§2.6 D1 と同じ方法（`OneStarMaker.StoreProbe` に `CreateNew` + `DeleteOnClose` の file を1回作り final path を論理 path と比較）で、この session が非仮想化であることを確認した（一致、残骸なし、事前に無かった probe 領域の directory は削除）。その後 `pwsh tools/workflow-task.ps1 start -Task artifacts-virtualized-store` で task を開始した（version 1）。
- B では Artifact Storage の publish / fetch、資格情報、R2 への通信、実ストアと Codex 側の読取り・変更をしていない。テストは全て temp 配下の隔離 store で行い、実 LocalApplicationData に書いたのは判定 suite の native probe（probe 領域の DeleteOnClose file）だけである。

### 実装

| 対象 | 行数 | 内容 |
|---|---|---|
| 新規 `tools/Workflow/WindowsStoreContext.psm1` | 77 | `Get-OsmStoreContext` が `not-virtualized` / `virtualized` / `unknown` を返す。probe 領域に固有名の `.probe` を `CreateNew` + `DeleteOnClose` で開き、`GetFinalPathNameByHandleW` の結果を論理 path と比べて閉じる。作成・照会・close のどれが失敗しても `unknown`。private seam `$script:ProbeIo` は Open / FinalPath / Close の三つを一緒に差し替える。import 時は Add-Type だけで I/O をしない |
| `tools/artifacts.ps1` | 154 → 175 | 引数解釈より前に判定し、`virtualized-store` / `store-context-unknown` を `status=failed`・exit 1 で返す（import 失敗も `unknown` 扱い）。`store plan` / `store consolidate` の grammar を追加（plan 0、consolidate は passed 0・partial 2・それ以外 1） |
| `tools/workflow-task.ps1` | 66 → 70 | store module の import より前に同じ判定 |
| `tools/harness.ps1` | 243 → 247 | 同上。判定 module だけを import し、Workflow の domain は読まない（D6） |
| 新規 `tools/Artifacts/StoreConsolidationPlan.psm1` | 196 | 純粋関数。item key の正規化、JSON 文字列の参照検査（LocalCache・role 外・欠落）、task 単位と operation の union、deployment 単位への依存、target との比較（absent / identical / partial / conflict / acl-mismatch / reparse / source-unreadable）、保留の伝播、approved manifest の再照合（`Test-ConsolidationManifest`） |
| 新規 `tools/Artifacts/StoreConsolidation.psm1` | 351 | 両側の棚卸し（除外領域は名前の列挙だけ）、manifest 候補の出力、承認 manifest の sha256 照合、run directory（lock・manifest の写し・journal・staging・単位 receipt）、staging への CreateNew → flush → SHA-256 照合 → ACL、非上書き rename による公開、Workflow 最後、`concurrent-writer` 検出、再実行の収束 |
| 新規 suite 3本 | 205 + 30 / 148 / 321 | `tools/Workflow/tests/StoreContext.Tests.ps1`（+ 子 process fixture `StoreContextCliFixture.ps1`）13 case、`tools/Artifacts/tests/StoreConsolidationPlan.Tests.ps1` 15 case、`tools/Artifacts/tests/StoreConsolidation.Tests.ps1` 12 case |
| 既存テストの追従 | 小 | `WorkflowCliFixture.ps1` が新しい import を許可し、`Get-OsmStoreContext` を記録付きで差し替える。`TaskLifecycle.Tests.ps1` の import 件数の期待を 5 → 6 にし、判定が最初の境界であることを確認する |
| README 3件 | 約 +40 | Artifacts（実行条件・reasonCode、合流手順、責務と offline 検証）、Workflow・Harness（実行条件と参照） |

受け入れ条件と検証の対応（offline 分）:

| AC | 検証 |
|---|---|
| AC1 | `cli-virtualized-all-entries`: 三つの CLI の全入口 42 通り（artifacts 28、workflow-task 4、harness 10）を新しい process で実行し、最終 JSON・exit 1、store 境界（判定 module 以外の import と git）の呼出し0件、probe が probe 領域だけを開くこと、出力に実 path・package 名・user 名が無いことを確認 |
| AC2 | `cli-unknown-failure-stages`（3 CLI × 作成・照会・close の失敗）と `decision-unknown-*` 4 case |
| AC3 | `cli-decision-before-store`（非仮想化なら判定の後に store 境界へ進む）、`no-package-identity-input`、既存 suite の結果不変（下記） |
| AC6 | `publish-skip-conflict`（A 公開、B skip、C は分岐した Workflow 履歴と違う Harness CURRENT で保留、run は partial、C の target 不変、source 不変） |
| AC7 | `publish-skip-conflict` の全 file の hash 一致、`domain-readers-after-publish`（実 module で publish した記録を仮想化側へ移して合流し、`Read-WorkflowTaskState`・`Read-EvidenceTask`・`Get-WorkflowTasks` が実ストア側で読める） |
| AC8 | `acl-rule-and-reparse`（公開先と作った directory の ACL、継承 ACL の同一 entry は `acl-mismatch`、source 内の junction と target 祖先の junction は `reparse`、junction 先へ書かない） |
| AC9 | `excluded-never-opened`（credentials・builds-v1・contents-v1・runtime・evidence-ledgers・review-evidence・RevisionLocks・H2a Review・WorktreeRecords の sentinel を両側に置き、plan・実行の I/O 記録にそれらの配下が無い） |
| AC10 | `fault-file-write`・`fault-staged`・`fault-between-items`・`fault-before-receipt`（再実行で passed、各公開先の move は全体で1回、run の staging だけを消す、receipt が残る、source 不変） |
| AC12 | `hidden-until-workflow`（公開途中の `Get-WorkflowTasks` に task が出ない）、`concurrent-writer`（先に作られた Workflow task directory を変えずに `concurrent-writer`） |
| AC13 | 計画 suite 15 case（reference-missing、LocalCache・role 外の拒否、共有 operation の結合、分岐の保留等） |
| AC14 | `import-has-no-store-side-effects`（三つの CLI から推移的に import される 51 module の top-level を AST で検査し、宣言以外の実行文が無いこと。加えて判定が仮想化を返す状態で全 module を新しい process に import し、probe すら走らないこと） |

### 実行したテスト（B の確認。判定 C の代わりではない）

`rg` は cursor 同梱のものを PATH に通した。全て exit 0。

| suite | 結果 |
|---|---|
| StoreContext.Tests（新規） | 13/13 |
| StoreConsolidationPlan.Tests（新規） | 15/15 |
| StoreConsolidation.Tests（新規） | 12/12 |
| Workflow TaskLifecycle.Tests | 23/23 |
| ArtifactEvidence.Tests | 19/19 |
| EvidenceCleanup.Tests | 35、failed 0 |
| Harness.Tests / UnityGate / UnityObservation / UnityTestRunner | 35 / 21 / 16 / 25、failed 0 |
| 残りの Artifacts suite | ArtifactPackage 9、ArtifactRotation 11、ArtifactTransfer 20、BuildCleanup 23、BuildCollect 9、BuildPublish 9、BuildRetention 9、Content.Contract 9、Content.Fetch 5、Content.Publish 12、ContentCleanup 23、Credentials 22、EvidenceReset 31、EvidenceRetention 14、EvidenceSchedule 78、R2RouteTransport 7、RouteProof 26、全て failed 0（上の4本と合わせ Artifacts の全 21 suite） |

機械検査: `pwsh tools/contract-audit.ps1`（errors 0、warnings 0、検査8 not-applicable）、`pwsh tools/docs-audit.ps1`（errors 0、warnings 0）、tools 配下の全 PowerShell 106 file の parse（errors 0）。3 project の Release build は suite 実行のために worktree 内で行った（成果物は git 管理外）。

### HANDOFF との差（B 適応と読み方。設計判断の追加ではないと判断したもの）

1. **`store plan` に `--output` を追加した。** §3 の grammar は仮称で、plan はストアを変更しないため、manifest 候補を両ストアの外の新しい file に CreateNew で書き、標準出力には sha256 と件数だけを出す。
2. **判定は artifacts.ps1 の引数解釈より前。** 仮想化された process では未知の command も `virtualized-store` になる。
3. **AC1・AC2 の「ストアの I/O seam」を、判定 module 以外の module import と git の呼出しとして観測した。** ストアの I/O は全て判定の後に import される module の中にあり、CLI 自身は判定の前にストアへ触れないため。
4. **AC3 の identity の注入。** 判定は identity を入力に持たない（D1）ので注入する対象が無い。代わりに、判定 module と三つの入口に package identity API の参照が無いことを静的に確認し、実行した host の identity の有無を suite の結果 JSON に記録した。B の host は「identity あり（Store 版 PowerShell）で非仮想化」で、既存 suite の結果は変わらなかった。「identity 無しで非仮想化」の host は B では観測していない。
5. **AC14 は静的検査を保証の本体にした。** .NET の file API を実行時に横取りする仕組みは無いため、module の top-level に宣言（Set-StrictMode、Import-Module、Export-ModuleMember、Join-Path、Add-Type、定数・script block の代入）以外が無いことを AST で検査し、import だけの動的確認を加えた。
6. **deployment は独立した単位にした。** §2 の用語にある二種類の単位のとおり、deployment 単位（receipt と config の対）を task 単位より先に公開し、Evidence を含む task 単位はそれに依存する。依存先が保留なら task 単位も `dependency-held` で保留する。対の公開は receipt → config の順（registry は `*.config.json` を列挙するため）で、同一 bytes の片方だけがある状態は自分の中断として `partial` と扱い、再実行で残りを公開する。
7. **transfers は記録が参照した operation だけを開く。** 参照されない operation は名前だけを `unreferenced-transfer` として除外に載せる（D4）。operation 内の記録が別の operation を参照する場合に備え、参照が増えなくなるまで繰り返す。
8. **参照検査の対象 file。** 名前が `.json` か、内容が `{` / `[` で始まる file を JSON として検査する（Harness の `CURRENT.previous`、Workflow の `.tmp` 残骸を含む）。名前が `.json` の file は JSON として読めなければ（内容が JSON らしくない場合を含む）`source-invalid` で保留する。読み込みの上限（16MB）を超える JSON らしい file は検査せずに公開せず `source-unchecked` で保留する（差し戻し1巡目で変更）。
9. **読めない source file（writer が開いたまま等）は `source-unreadable` で保留する。** 承認できる hash が無いため。
10. **run の識別と再開。** runId は承認 manifest の sha256 の先頭 32 文字で、同じ manifest の再実行は同じ run directory を再開する。run directory にある manifest の写しが違えば `run-conflict`。dry-run は run directory も lock も作らない。一つの単位の失敗は run を止めず、単位ごとの結果に残す。manifest の写しと単位 receipt は run の staging に書いてから非上書き rename で置き、既存 receipt はこの run のこの単位の記録として読めた場合だけ完了とみなす（差し戻し1巡目で変更）。
11. **判定の比較は論理 path の文字列との比較のまま（D1 どおり）。** LocalApplicationData の path 自体に別名・junction がある host では、非仮想化でも `virtualized-store` になりうる（fail-closed 側）。B の host では一致した。

### C・owner へ渡す事実（新しい事実の分類: 未確認のため B では判断しない）

- **Harness と deployment receipt には、コード上の明示 ACL 規則が無い。** `RecordStore.psm1` は CURRENT・run を継承 ACL のまま書き、deployment receipt と config は tools の中に作成箇所が無く、読取り（`Read-EvidenceConfig`）も ACL を検査しない。I6・AC8・D2 (iii) を文字どおり実装したので、実ストアに継承 ACL のまま同一 bytes の Harness entry や deployment receipt があると `acl-mismatch` で保留し、Evidence task が参照する deployment receipt が実ストアにだけあって継承 ACL なら、その task 単位は `reference-missing` になる。後者が実データで起きると S4 の対象（DebugStudio property write の task）が保留になる。B は実ストアの ACL を読んでいないので、起きるかは未確認。判定 C の前後に非仮想化の担当が `store plan`（両ストアとも変更しない）を実行すれば、manifest の `reasons` で分かる。起きた場合、規則を変えるかは凍結条件（I6）の意味を変える判断なので Phase A の新しい revision になる。
- **transfers の operation にある Evidence の payload も参照検査の対象になる。** 差し戻し1巡目で、名前が `.json` の file は JSON として読めなければ保留するようにした。operation の中の payload の写し（Evidence の固定入力の各 entry）も同じ規則を受けるので、`.json` 名で JSON でない entry を含む Evidence は、その task 単位が `source-invalid` で保留になる。DebugStudio property write の固定入力に該当する entry があるかは未確認（B は実データを読んでいない）。上の ACL の件と同じく、非仮想化の担当の `store plan` の `reasons` で分かる。

### 差し戻し1巡目の修正（2026-10-13）

- **開始確認:** 新しい専用 worktree で、リモート head `2894dbf`、本 §0 の base / head / 次の担当、§1〜§5 と凍結 commit `97145dc` の同一性を照合した。§2.6 D1 と同じ方法（`Get-OsmStoreContext`）で、この session が `not-virtualized` であることを1回確認した（probe 領域に file の残骸なし）。仕様は r3 の §1〜§5 だけとした。
- **参照先の成立（D2 (iii)、AC13）:** 参照を item の単位ではなく item 内の path まで保持し、指す file・directory そのものが、単位で運ぶ item の中か実ストアの観測の中にあることを確かめる。同じ item の中を指す参照も検査する。Evidence task は deployment の receipt と config の両方を要する。欠ければ `reference-missing` で保留する。単位への所属と参照先の成立を別々に判定する。
- **検査できない記録（S2、D2 (i)〜(iii)）:** reader は 16MB を超える file を hash だけにし、名前が `.json` か先頭が JSON らしければ `unchecked` の印を付ける。plan は `unchecked` を `source-unchecked`、名前が `.json` で JSON として読めない file を `source-invalid` で保留する。16MB は読み込みの上限のままで、検査を省いて公開する条件ではなくなった。
- **run 記録の中断安全（S3、AC10）:** manifest の写しと単位 receipt を、run の `staging\records` に CreateNew → 書込み → flush → ACL の順で作り、非上書き rename で最終名に置く。実行の開始時に、前の実行が残した自分の `staging\records` だけを捨てる。既存 receipt は、この run（runId・manifest sha256）のこの unitId の記録として読め、ACL が規則どおりの場合だけ完了とみなし、そうでなければ書き換えずに単位を `receipt-invalid` で止める。run 配下の directory・lock・journal の ACL は実行ごとに付け直す（作成と ACL 設定の間で止まっても継承 ACL のまま残らない）。
- **読取り境界の reparse（I6、AC8）:** 複写で開いた source の handle の final path（判定 module の既存 type の `GetFinalPathNameByHandleW`）を、source root の実体 path（`WindowsStorePaths.psm1` の既存 identity）と相対位置から組み立てた path と比べ、違えば単位を `reparse` で止める。棚卸しの後に祖先が junction へ差し替わっても、hash の一致は読んだ経路の安全を示さないため。
- **構造:** 新しい公開関数・状態・CLI 引数は無い。`StoreConsolidation.psm1` が `WindowsStorePaths.psm1` を import した（§3 で許可済みの依存）。manifest の保留理由に `source-unchecked`、単位の結果に `reparse`・`receipt-invalid` が加わった（README の合流節に記載）。行数は `StoreConsolidation.psm1` 351 → 397、`StoreConsolidationPlan.psm1` 196 → 224。500 行の警報に届かず、棚卸し・公開・run 記録が同じ所有者・寿命・依存を共有するので非分割の判断は変えない。
- **テスト（修正前に失敗を確認してから修正）:** 計画 suite に `referenced-file-missing`（同じ item の欠落 file、運ぶ operation に無い file、実ストアの operation に無い file、receipt の無い deployment）と `unchecked-record-held`、合流 suite に `unexamined-json-held`（17MB の JSON と `.json` 名の平文）、`fault-run-records`（manifest の写しと receipt の書込み途中に障害を注入し、再実行で `passed` へ収束。壊れた receipt は完了とみなさず書き換えない）、`source-reparse-after-inventory`（複写が source を開く直前に task directory を同一 bytes の外部 directory への junction へ差し替える）を足した。修正前は5件とも失敗した。既存 case の追従: 計画 suite の target fixture に参照先 file と deployment の対を持たせた（files が空の target を参照先ありとして扱っていたため）。合流 suite の `domain-readers-after-publish` は、共有 fixture の payload が `.json` 名の平文なので、この suite の中だけ JSON 本文に置き換えた。

### 差し戻し1巡目の確認結果

Release DLL を既存 loader の出力先（`Packaging/artifacts/package`、`Transport/artifacts/transport`、`Probe/artifacts/probe`）に `dotnet build -c Release -o` で用意し（3件とも errors 0）、cursor 同梱の `rg` の directory を PATH へ通してから、§5.1 の起点 filter を `-Case '*'` で実行した。全て exit 0、failed 0。

| suite | 結果 |
|---|---|
| StoreContext.Tests | 13/13 |
| StoreConsolidationPlan.Tests | 17/17 |
| StoreConsolidation.Tests | 15/15 |
| Workflow TaskLifecycle.Tests | 23/23 |
| ArtifactEvidence.Tests | 19/19 |
| EvidenceCleanup.Tests | 35、failed 0 |
| Harness.Tests / UnityGate / UnityObservation / UnityTestRunner | 35 / 21 / 16 / 25、failed 0 |

機械検査: `pwsh tools/contract-audit.ps1`（違反なし、exit 0）、`pwsh tools/docs-audit.ps1`（errors 0、warnings 0）、tools 配下の全 PowerShell 106 file の parse（errors 0）、`git diff --check`（通過）。起点 filter 以外の Artifacts suite と判定必須テストの判定実行は Phase C。

### Phase A の判断が必要な事実（B の分類: A 再開）

- **事実:** 共有 operation や task 間の参照で単位を結合すると、一つの単位に複数の Workflow task directory が入る。公開は directory 単位の非上書き rename で、directory は一つずつしか現れない。`Get-WorkflowTasks` は `Workflow\<repo>\tasks` の直下の directory をすべて列挙するので、最初の Workflow を置いてから最後を置くまで、その単位の一部が task 一覧に現れる。途中で止まれば、その状態が残る。Workflow が1つの単位では起きない。
- **凍結範囲で検討して採れなかった案:** (a) 公開途中の単位を通常処理から隠す門（§5.8 A2b-7 で不採用）。(b) `Get-WorkflowTasks` の列挙規則の変更（§3 で `TaskEventStore.psm1` は変更しない）。(c) `tasks` directory ごと一度に置く（実ストアに `tasks` が既にあれば使えない）。(d) 複数の Workflow を含む単位を保留する（S3 の保留理由と fail-closed の範囲を増やし、その単位は合流できなくなる。S4 の対象が該当すれば S4 も成立しない）。(e) 依存の順に置き、各 task は自分の依存がそろってから現れるようにする（AC12 を単位ごとではなく task ごとに読み替える）。(a)(b) は計画外の gate・reader 契約、(d) は fail-closed 契約、(e) は AC12 の意味の変更にあたるので、B では選ばない。
- **現状:** 実装は変えていない（単位の中では Workflow を最後に、一つずつ置く）。README の「公開途中の単位は…見えません」は Workflow が1つの単位にだけ成り立つ。r4 の決定に合わせて直す。
- **その後:** r4（§2.6 D2「公開途中に task 一覧へ見える単位の保留」）で決定され、差し戻し2巡目で実装した（下記）。

### 差し戻し2巡目の修正（2026-10-13、r4 の追加分と §7.7 の戻し項目）

- **開始確認:** 新しい専用 worktree（detached。旧 worktree が branch を checkout したままのため）で、リモート head `4e036a1`、§0 の base / head、r4 の凍結 commit `7736422` とその内容 sha256、§1〜§5 と凍結 commit の同一性を照合し、全て一致した。§2.6 D1 と同じ方法（`Get-OsmStoreContext`、sandbox 外の PowerShell 7.6.6）で、この session が `not-virtualized` であることを1回確認した。仕様は r4 の §1〜§5 だけとした。
- **保留の規則（§2.6 D2 r4、AC12・AC13）:** `New-ConsolidationPlan` が task 単位ごとに、単位の Workflow のうち実ストアで absent のものを数え、2つ以上なら `multi-workflow-unit`、実ストアに既にある Workflow があり、Workflow 以外に absent / partial の item があれば `workflow-already-visible` を、既存の理由と独立に加える。全 item が identical の単位、新たに公開するのが Workflow 1つだけの単位は保留しない。deployment 単位は Workflow を持たないので対象外で、従来どおり先に公開する。
- **実行時の再検査:** 新しい関数・引数は足していない。`Test-ConsolidationManifest` の既存の規則で、承認時に保留だった単位は `hold` のまま（理由が消えても公開しない）、承認時に公開対象だった単位が実行時の計画で保留の条件を満たせば `target-changed`、item 集合が変われば unitId が変わるので `source-changed` になる。実行側（`StoreConsolidation.psm1`）は verdict が `publish` / `complete` 以外の単位を staging せず、単位 receipt を作らず、journal に `unit-<verdict>` の trace だけを書く既存の分岐のままで、変更していない。
- **参照先の読取り（§7.7 の戻し項目、S2・D2 (iii) の既存検査内の B 適応）:** 実ストアにだけある参照先は、存在・ACL・reparse に加えて、観測した file の hash が取れていること（読めたこと）を要する。参照が directory（または item 自体）を指す場合は、その下の全 file が読めることを要する。満たさなければ既存の `reference-missing` で保留する。正常に読める参照先は従来どおり成立する。
- **README:** `tools/Artifacts/README.md` の合流節で、公開途中の不可視性を「新たに公開する Workflow が1つで、単位の Workflow が実ストアにまだ無い」場合に限定し、`multi-workflow-unit`・`workflow-already-visible` の保留、保留単位の扱い（staging・receipt を作らない、journal の記録だけ）、承認後の変化の扱い、参照先の読取りの要件を書いた。
- **テスト（修正前に失敗を確認してから修正）:** 計画 suite に §5.1 の6 case と `target-only-reference-unreadable`、合流 suite に §5.1 の4 case と `target-only-reference-unreadable`（実ストアの参照先 file を `FileShare.None` で開いた状態の plan・実行と、閉じた後の対照）を足した。修正前は、計画 suite の `multi-workflow-unit-held`・`workflow-already-visible-held`・`multi-workflow-with-other-reasons`・`target-only-reference-unreadable` と、合流 suite の `multi-workflow-unit-not-published`・`workflow-already-visible-not-published`・`unit-changed-after-approval`・`target-only-reference-unreadable` が失敗した。保留しない側の対照（`all-identical-unit-not-held`・`only-new-workflow-not-held`・`single-workflow-unit-not-held`）と `interrupted-then-changed` は修正前から通った。後者は、構成が変われば unitId が変わる既存の性質で成り立っていたもので、回帰防止として残す。既存の `hidden-until-workflow` は Workflow 1つの単位の検証として変えていない。
- **構造:** 新しい状態・公開関数・引数・依存・gate は無い。計画 module に private な判定関数 `Test-ConsolidationItemReadable` を1つ足した（純粋関数。純粋性検査の許可リストに追加）。reasonCode の値は `multi-workflow-unit`・`workflow-already-visible` の2つが manifest の保留理由に加わった（schemaVersion は不変）。行数は `StoreConsolidationPlan.psm1` 224 → 248、`StoreConsolidation.psm1` は 397 のまま。合流 suite は 388 → 518 で 500 行の警報を超えたが、追加 case は既存の隔離 store・fixture・helper を共有する同じ合流 I/O の検証で、変更理由・依存・寿命が同じなので分割しない。

### 差し戻し2巡目の HANDOFF との差（B 適応と読み方）

1. **「新たに公開する」「既にある」は実ストアの観測（exists）で数えた。** source 側の状態（`source-unreadable` 等）に左右されないようにし、Workflow 以外の「公開する item」は観測で absent か、状態が `partial` のものとした。実ストアの Workflow が bytes の違う既存 entry（`conflict`）の場合も「既にある」に含め、`conflict` と `workflow-already-visible` が併存する（§2.6 D2「理由は互いに独立に判定」、§2「identical を含む」の読み）。
2. **directory を指す参照の読取り要件。** §7.7 は file の読取り失敗を対象にしている。D2 (iii) の「bytes・ACL 同一で既にある」は directory では配下の bytes を指すと読み、配下の全 file が読めることを要件にした。新しい reasonCode は作らず、(iii) を満たさない既存の `reference-missing` を使う。
3. **合流 suite の fixture。** 実ストアに既にある Workflow を置く case は、`Get-WorkflowTasks` が `tasks` directory の private ACL を検査するので、`Workflow` 配下の tree に private ACL を付けた（実ストアの通常の状態と同じ）。suite の各 case の後始末で `TaskEventStore` の test root も戻すようにした。

### 差し戻し2巡目の確認結果

3 project の Release DLL を既存 loader の出力先（`Packaging/artifacts/package`、`Transport/artifacts/transport`、`Probe/artifacts/probe`）に `dotnet build -c Release -o` で用意し（3件とも exit 0）、cursor 同梱の `rg` の directory を PATH の先頭へ通してから、§5.1 の起点 filter を `-Case '*'` で実行した。全て sandbox 外の PowerShell 7、隔離 temp store・fake transport・fake process。全て exit 0、failed 0。

| suite | 結果 |
|---|---|
| StoreContext.Tests | 13/13（native probe 1件を含む。probe 領域だけに書く） |
| StoreConsolidationPlan.Tests | 24/24 |
| StoreConsolidation.Tests | 20/20 |
| Workflow TaskLifecycle.Tests | 23/23 |
| ArtifactEvidence.Tests | 19/19 |
| EvidenceCleanup.Tests | 35、failed 0 |
| Harness.Tests / UnityGate / UnityObservation / UnityTestRunner | 35 / 21 / 16 / 25、failed 0 |

機械検査: `pwsh tools/contract-audit.ps1`（違反なし、exit 0）、`pwsh tools/docs-audit.ps1`（errors 0、warnings 0）、tools 配下の tracked PowerShell 106 file の parse（errors 0）、`git diff --check`（通過）。実ストア・Codex 側への CLI 実行、実データの `store plan` / `consolidate`、Artifact Storage の publish / fetch、資格情報の操作、R2 への通信は行っていない。

### 未実行

- AC4・AC5（実環境。Phase C の実行担当）、実ストアと Codex 側への `store plan`（B の範囲外）。AC11 / S4 と実ストアへの合流の実行は r4 で後続 `Artifact Storage / Split Store Recovery` へ移した（§1.5、§5.5 案 B）。
- 起点 filter 以外の Artifacts suite（判定必須）は差し戻し2巡目では実行していない（判定 C）。
- 判定必須テストの判定実行（Phase C）。Unity テスト・Build は本スライスに適用しない（§5.1）。Editor のコンパイル確認は Unity source を変えていないので対象外。
- Phase B result snapshot・evidence bundle の生成と publish（依頼により B は Artifact Storage を操作していない。§0 参照）。

- implementation head commit: 初回は c55891a498dc3ca99312533d3c9a23126e72fd0c。差し戻し1巡目の修正後は §0 を正とする。
- Phase B 担当・モデル・ベンダー: Claude Code（Claude Opus 5.5、Anthropic）。非仮想化の Claude デスクトップから実行した。差し戻し1巡目・2巡目も同じ担当・モデルの新しい session。

## 7. Phase C

### 7.1 種別・固定入力・実行環境

- **種別:** 発見 C（初回）。2026-10-13。GO 判定・判定 C の一括検証・C' の起動は行っていない。阻害する指摘 **5件**。
- **担当・モデル:** Codex（GPT-6、OpenAI）、新規 session。B の Claude Opus 5.5 / Anthropic と異なるモデル。追加エージェントは使用していない。
- **開始照合:** detached HEAD とリモート `claude/artifacts-virtualized-store-a01` はともに `1be335c07437df150ba13ddc054ecd48794ccf47`。sandbox の通信制約で最初の `git ls-remote` は失敗し、通常の sandbox 外経路で一致を確認した。PR は未作成。§0 の base / head と指定値が一致し、凍結 commit `97145dc` の §1〜§5 が現在の文書と同一であることも確認した。§6 は B の実績として扱い、仕様の代替にしていない。
- **対象:** `3f79639c12eac16da77d06608675564e906f9a49..c55891a498dc3ca99312533d3c9a23126e72fd0c` の完全 diff（binary / full-index）、stat、name-status を固定した。16 file、1742 insertions、5 deletions。実装 file は変更していない。
- **D1 の確認:** sandbox 外で `pwsh -NoProfile -Command "Import-Module ./tools/Workflow/WindowsStoreContext.psm1 -Force; Get-OsmStoreContext"` を1回実行し `not-virtualized`。CreateNew / DeleteOnClose / final path 比較の実装を使用した。今回の実行経路は非仮想化であり、Codex というアプリ名から仮想化を推定していない。probe directory の削除・既存 file の清掃はしていない。suite の native probe case はこの確認を繰り返さないため選択から除いた。
- **証拠の状態:** 固定 diff、機械検査、生結果 JSON、限定再現 script と出力、生成時刻・内容 hash を持つ manifest を原sourceとして保持。Artifact Storage の保存・取得案内は未保存、別 session への取得確認・引渡しは未完了。今回の依頼で publish / fetch が禁止されているため実行していない。非仮想化の保存・実行担当へ引き継ぐ。判定 bundle / blind bundle は未生成。原sourceの保持を配布完了としていない。

### 7.2 構造適合（機能レビューより先に実施）

- **判定基盤:** `WindowsStoreContext.psm1`（77行）は OS API、probe の呼出し単位の file / handle と private seam を所有する。結果を cache せず、domain の import は無い。§3 の古い残骸清掃の記述は r3 の D1 / A2b-2 により除外され、実装も他呼出しの file を削除しない。既存 `WindowsStorePaths` への import を使わず native API を同 module に持つ差は、許可された OS 基盤責務内の詳細である。
- **入口:** artifacts / workflow-task / harness の判定は domain import・guard・資格情報アクセスより前。Harness → Workflow は D6 が許可した基盤 edge だけ。既存 resolver・domain store・ACL 規則は変更していない。引数 binding は PowerShell の既存機構で、対応 subcommand の実行境界は入口に集約されている。
- **Policy:** `StoreConsolidationPlan.psm1`（196行）は item key、JSON 参照、unit の結合、target 分類、保留伝播、承認 manifest の照合を純粋関数に置く。公開関数4つのうち `Get-ConsolidationItemKey` / `Get-ConsolidationTargetKeys` は棚卸し側へモデルを渡す module 間境界で、CLI / env から seam を設定する面は無い。メモリ fixture だけで検査できる。
- **I/O・寿命:** `StoreConsolidation.psm1`（351行）は棚卸し、staging、公開、lock、journal、receipt を run の寿命で所有し、source を read-only handle で開く。ArtifactAcl / Plan / WindowsStoreContext に依存し、Workflow・Evidence・Harness の domain module を import しない。既存 reader の使用はテストと公開後の手順に限定される。OS 判定変更は基盤、参照規則変更は Plan、障害回復変更は I/O に閉じる配置である。
- **規模判断:** 新規 test 本体は 205 / 148 / 321行（入口 fixture 30行）。既存入口の増加は50%未満。新規 I/O は棚卸し・公開・run 永続化の3活動を持つ警報対象だが、単一の合流手順・所有者・寿命・依存・隔離 store テストを共有し、Policy は分離済みなので現時点では **非分割妥当**。500行未満という理由だけで判断していない。計画外のフォルダ・asmdef・Unity source・Game → Framework の edge は無い。
- **結論:** 責務配置・依存・所有者・寿命・単体テスト境界は概ね §3 に適合。以下の5件は、このスライスの新規アルゴリズム／失敗経路の欠陥で、既存由来の構造劣化として先送りできない。C-04 の修正が計画外の状態・gate・reader 契約を必要とするなら、その時点で A を再開する。

### 7.3 現在の問いを阻害する指摘

各件は semantic / unique、発見 C・上記担当による指摘。B への修正要求として採用し、修正 commit は未作成。C-01〜03・05 は B 適応（凍結した責務・契約内の不具合修正）。C-04 はまず B に現責務内の成立案を求め、計画外の状態・依存・公開 API または fail-closed 契約変更が必要なら A 再開へ分類する。C が新しい gate を設計・許可した意味ではない。

1. **C-01 / P1 — 参照 file の欠落を検出できない。** 違反根拠は S2、D2 の参照規則 (iii) と依存が閉じた単位、AC13。一次証拠は `StoreConsolidationPlan.psm1:73–75,151–155`。完全修飾 path を item key へ縮約し、同じ item への参照は捨て、別 item も unit への所属または target の exists / ACL だけで許可する。具体的順序: source の `Workflow/<repo>/tasks/alpha/state.json` が同 task の存在しない `missing.json` を指す → plan → unit は `publish`、reasons 空、参照 file は不存在。隔離再現 `missing-file-in-carried-item` で確認した。target に operation directory だけがある場合も、その中の実際の参照 file・bytes を検査しない。既存計画 suite の `reference-present-in-target` は files 空の target observation を成功例にしており、この抜けを固定している。単位への所属と参照先 file の成立を分けて検査する修正・欠落ケースが必要。

2. **C-02 / P1 — JSON の参照検査を黙って省ける。** 違反根拠は S2、D2 (i)〜(iii)、AC13 の LocalCache 拒否。一次証拠は `StoreConsolidation.psm1:53–60` と `StoreConsolidationPlan.psm1:61–64`。具体的順序: 16MB を超える有効な `state.json` に LocalCache の絶対 path を含める → reader は hash だけを計算し json=null → Plan は検査せず `publish`。隔離再現 `large-json-localcache` で reasons 空を確認した。小さい named `.json` の本文が `not-json` の場合も json=null のまま `publish`（`named-json-not-json-looking`）。B 差分8の「名前が .json の parse 失敗は保留」という説明とも一致しない。上限そのものを新仕様にせず、検査できない記録を成功扱いしない修正が必要。

3. **C-03 / P1 — run 記録の書込み中断が回復不能または偽の完了になる。** 違反根拠は S3、D2 の run 所有・再実行での収束、AC10 が検証する失敗経路。一次証拠は `StoreConsolidation.psm1:220–227,321–324,338–340`。manifest の写しと receipt は最終名へ直接 CreateNew / write / flush し、前者は既存 hash 不一致を恒久的 `run-conflict`、後者は存在だけで完了扱いにする。順序A: manifest.json 作成後、write / flush 中に停止して `{` が残る → 同じ承認 manifest を再実行 → 初回も再試行も `failed/run-conflict`（`interrupted-run-manifest`）。順序B: unit の file 公開後、receipt write 中に停止して `{` が残る → 再実行 → receipt は `{` のまま `passed/complete`（`interrupted-unit-receipt`）。実 process の強制終了はしておらず、各書込み窓で到達する中断状態を隔離 fixture に作って確認した。既存4点の fault は record write の途中を通らない。run 自身の durable 記録も中断安全にし、破損 receipt を存在だけで成功扱いしない修正が必要。

4. **C-04 / P1 — 結合単位の Workflow が途中で通常処理に見える。** 違反根拠は D2 の依存閉包・公開不可視性、AC12 の「公開途中のその単位は列挙されない」。一次証拠は `StoreConsolidationPlan.psm1:123–137`、`StoreConsolidation.psm1:334`、`TaskEventStore.psm1:231–239`。単位結合は行うが複数 Workflow directory は一つずつ rename する。具体的順序: alpha が beta の state.json を参照して二つの task が1 unit になる → alpha の Workflow を公開 → beta の公開前に `Get-WorkflowTasks` → alpha が列挙される → その場で中断すると run は partial、beta は不存在。隔離再現 `merged-unit-visible-before-complete` の unitCount=1、observed=[alpha]、betaPublished=false で確認した。同じ問題は AC13 の共有 operation による複数 task 結合にも適用される。既存 `hidden-until-workflow` は Workflow が1つの unit だけを検査している。凍結した不可視性を満たす修正案が必要で、AC12 の意味を「個々の task が使えればよい」に変えるなら A の新 revision が必要。

5. **C-05 / P1 — 棚卸し後の source reparse を通って公開できる。** 違反根拠は I6、AC8（読取り元の祖先に reparse があれば unit を止める）。一次証拠は `StoreConsolidation.psm1:175–184,230–252,261–263`。root / tree の検査は棚卸し時だけで、copy の read-only FileStream open では source の祖先・実体を再検査しない。具体的順序: plan と実行時の再棚卸しは通常 directory → copy の read trace の直後・open の直前に別 writer を模した fixture が task directory を退避して、同一 bytes の state.json を持つ隔離 outside directory への junction にする → copy はその祖先を通る → hash は一致し `passed/published`。`source-reparse-after-inventory` で injected=1、sourceIsReparse=true、published=true を確認した。注入による変更は隔離 fixture の actor が行い、合流自身や実 Codex source が変更したものではない。hash 一致は ancestry の安全性を証明しないため、実際の読取り境界で止める修正が必要。

### 7.4 凍結条件・B 適応11件の照合

- **I1 / I2 / S1:** guard・資格情報・domain import の前で判定し、42入口と作成／照会／close の失敗注入で JSON・exit・reasonCode の保持を確認。51 module の import 検査にも違反は見つからなかった。実 Codex 仮想化経路の AC4 は未実行。
- **I3 / AC3:** 今回も package identity あり・非仮想化。コードは identity に分岐せず、起点回帰は通過。identity 無しで非仮想化の host と既存全 suite の二条件照合は未確認で、今回の部分回帰を AC3 全体の成功にしていない。
- **I4 / I5 / S2:** 非上書き rename、unit の conflict / ACL 保留、source の read-only handle、元 bytes の hash 照合は通常経路で確認。参照閉包と JSON 検査は C-01 / C-02 が阻害する。parse→serialize による元記録の改変は見つからなかった。
- **I6:** 通常の private ACL、同一 bytes の ACL mismatch、事前にある reparse の保留は通過。読取り境界の順序には C-05 が残る。
- **I7:** 除外を名前列挙から先に決め、unreferenced operation は開かず、sentinel の open が無いことを確認。範囲外 role の読取り API の追加は無い。
- **S3 / AC6〜10 / AC12:** 通常の衝突・skip・4点の fault・単一 task の並行 writer は通過したが、C-03 / C-04 が収束と不可視性を阻害する。AC7 の既存 Workflow / Evidence reader case は DLL 準備後に通過。Harness validator と実 deployment の成立は今回のテストから一般化しない。
- **AC4 / AC5 / AC11 / S4:** 実環境確認・高コスト合流・fetch は未実行。S4 の未確認を現段階の新たな実装 blocker に数えず、判定 C / 高コスト証拠の担当へ残す。AC14 は起点で確認済み。

§6「HANDOFF との差」の判断（番号は B の11件に対応）:

- **1、2:** B 適応として妥当。仮称 grammar の `--output` は両 store の外の CreateNew file、入口で引数解釈より前に止める変更は I1 の境界を強める。
- **3、5:** B 適応として妥当。import / git 観測を入口の I/O 到達境界とし、top-level の静的検査と動的 import を組み合わせる。変更 module と推移的 import を照合し、seam 件数だけを一般的な filesystem 横取りの証明にはしていない。
- **4:** identity に入力面が無いので別の fake API を作らない適応は妥当。ただし「identity 無しで非仮想化」の観測と AC3 の全 suite 結果は未確認のまま引き継ぎ、凍結条件の免除として採用しない。
- **6:** deployment を独立 unit とし、receipt→config の順・依存保留・同一 subset の再開を扱うのは D2 と用語の範囲内。file 単位の対の公開は registry の発見順に合わせた B 詳細で、元 receipt の bytes を変える判断ではない。C-03 の metadata 書込み中断問題は別に修正する。
- **7:** D4 の参照された transfers だけを開き、参照が増えなくなるまで繰り返す適応は妥当。参照先 file の成立には C-01 の修正が必要。
- **8:** CURRENT.previous / .tmp の JSON を検査対象へ含める適応は妥当だが、named .json と大きい JSON の検査省略は凍結条件内の不具合（C-02）。この挙動を B 適応として容認しない。
- **9:** hash を得られない source を `source-unreadable` で保留するのは妥当な B 適応。
- **10:** runId、同一 manifest の再開、dry-run の無書込み、単位ごとの失敗継続は D2 / S3 の範囲内。ただし直接 record write で中断状態が残る点は C-03 の不具合で、収束保証の例外として承認しない。
- **11:** 論理 path の文字列比較は凍結 D1 どおり。LocalApplicationData 自体の alias / junction による誤拒否は新しい host 条件の不確実性で、現実装の条件違反とせず後続へ送る。

### 7.5 後続への入力・新しい事実の分類

- **Harness / deployment receipt の ACL 規則:** B が指摘した生成側の継承 ACL と I6 の厳密な照合は、既存コードから確認できる。I6 に従って `acl-mismatch` / `reference-missing` とすること自体は B 適応であり、規則を緩める理由にしていない。DebugStudio property write の対象が実際に保留されるかは **スパイク（S4 の実データ成立性未確認）**。確認範囲はその対象 unit の manifest reasons、終了点は ACL による保留の有無の確定。今回の依頼は実データの plan も禁止するため未実行で、凍結 §5.5 の計画段階の非仮想化実行担当・owner へ渡す。規則または I6 / S4 の意味を変える必要が確定したら **A 再開**。この未確認事実を6件目の blocker にしていない。
- **OS 基盤の配置整理、host alias による誤拒否、非仮想化担当への継続依頼経路:** **後続スライス** `Artifact Storage / Virtualized Host Route`。今回の凍結条件を満たすための新設計として追加しない。
- **Codex コピーの残置、job binding、資格情報の有効性:** 既存の §1.5 / U3 / U4 の所有先へ残す。実ストア／Codex 側の内容・ACL・資格情報を読んで一般化していない。
- 証拠の未保存・引渡し未完了、AC3 の未観測 host、AC4 / AC5、A3 凍結照合 script の未用意は、**現スライスの未確認・引継ぎ事項**。後続へ移送して判定の必須条件を免除する扱いではない。照合 script は本 session では作成していない。

### 7.6 実行した確認・filter・結果と未実行

全 suite は PowerShell 7 の sandbox 外経路、隔離 temp store / fake transport で実行した。実ストアへの CLI、実データ plan / consolidate、Artifact Storage の publish / fetch、資格情報操作、R2 通信は行っていない。

- **ビルド準備:** Packaging / Transport / Probe の3 project を `dotnet build <project> -c Release` で用意した後、既存 loader に合わせ `--no-restore -o` を各既存 `artifacts/package` / `artifacts/transport` / `artifacts/probe` に指定。全て exit 0、errors / warnings 0。判定 C の全 suite 実行を兼ねたものではない。
- **StoreContext:** `pwsh -NoProfile -File tools/Workflow/tests/StoreContext.Tests.ps1 -Case decision-not-virtualized,decision-virtualized,decision-unknown-open,decision-unknown-final-path,decision-unknown-close,decision-unknown-empty-final-path,probe-outside-store,no-package-identity-input,cli-virtualized-all-entries,cli-unknown-failure-stages,cli-decision-before-store,import-has-no-store-side-effects -ResultPath <原source>`。12 selected / executed、failed 0、exit 0。native probe は開始時の1回確認との重複を避けて除外。identity あり、virtualizedEntries=42、importedModules=51。
- **計画:** `pwsh -NoProfile -File tools/Artifacts/tests/StoreConsolidationPlan.Tests.ps1 -Case '*' -ResultPath <原source>`。15/15、failed 0、exit 0。
- **合流:** 同形の `tools/Artifacts/tests/StoreConsolidation.Tests.ps1 -Case '*'` は11 pass / 1 fail。`domain-readers-after-publish` が fixture publish unconfirmed で失敗。初回は既存 loader の出力先に DLL が無かったためで、準備修正後 `-Case domain-readers-after-publish` が1/1、failed 0、exit 0。残る11 case を無目的に再実行していない。
- **Workflow:** 同形の `tools/Workflow/tests/TaskLifecycle.Tests.ps1 -Case '*'`、23/23、failed 0、exit 0。
- **Evidence:** 同形の `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1 -Case '*'`、19 executed、9 failed（初回の DLL 準備不足）。規定出力先を用意後、`-Case v2-roundtrip,reader-hash,handoff-identity-substitution,one-put-lost-response,fresh-child-fetch,put-reconcile-no-reput,put-recovery-child-protected,selection-invalid-path,cli-relative-selection` が9/9、failed 0、exit 0。初回成功の10 case と合わせ起点19 caseを確認した。
- **Cleanup:** 同形の `tools/Artifacts/tests/EvidenceCleanup.Tests.ps1 -Case '*'`、初回35 failed（同じ準備不足）、再実行35 executed、failed 0、exit 0。各実行前に cursor 同梱 `rg.exe` の directory を PATH に追加した。初回の失敗 JSON も保持し、成功に書き換えていない。
- **Harness:** 同形の `tools/Harness/tests/{Harness,UnityGate,UnityObservation,UnityTestRunner}.Tests.ps1 -Case '*'`。35 / 21 / 16 / 25 executed、全て failed 0、各 exit 0。Unity 自体を起動するテストではなく fake process の offline suite。
- **限定反例:** C-01〜05 の条件を確認する7 scenario（参照欠落、named JSON、省略された大きい JSON、manifest 中断、receipt 中断、結合単位の途中可視化、棚卸し後 reparse）を隔離 fixture で実行し、上記の一次出力を保持。既存 test file・実装 code は修正していない。最初の worktree 内 fixture は ACL 設定が拒否されたため結果に使わず、既存 suite と同じ temp 領域で成立を確認した。これを仮想化として分類していない。
- **機械検査:** `pwsh tools/contract-audit.ps1` は exit 0、errors 0、warnings 0、検査8 not-applicable。レビュー記録更新後の `pwsh tools/docs-audit.ps1` は exit 0、errors 0、warnings 0。全 tools PowerShell の AST parse は exit 0、errors 0。生結果は原sourceへ保持。`git diff --check` も通過した。
- **未実行:** 判定 C の必須全 Artifacts suite、identity 無しの非仮想化 host の AC3、実環境 AC4 / AC5、高コスト AC11 / S4、実データの ACL 分類・plan、判定 C の照合 script、証拠 publish / fetch・別 session 閲覧確認。Unity テスト・Build は §5.1 の適用除外。差し戻しが決まったため判定 C・C' を起動しない。

**出口:** 実装 head は据え置き、レビュー記録だけの commit を既存 branch へ非 force push して B に返す。修正 head の確認後に発見 C を再度行う。

### 7.7 発見 C 再レビュー（差し戻し1巡目、先行）

- **種別・担当:** 発見 C（再レビュー、2026-10-13）。owner が §0 引継ぎ票の後者（本 head の先行レビュー）を依頼した。Codex（GPT-6、OpenAI）、新規 session。B の Claude Opus 5.5 / Anthropic と異なるモデル。追加エージェントは使用していない。GO 判定、判定 C の判定必須テスト一括実行、C' の起動、C-04 の設計判断は行っていない。
- **開始照合・仕様:** detached HEAD とリモート branch はともに `91b97a06f202657cb1f5ebb876922f3dc4f72a2f`。sandbox 内の最初の `git ls-remote` は通信制約で失敗し、sandbox 外で照合した。PR は未作成、§0 の PR 状態ブロック・base / head は指定値と一致。`91b97a0` はレビュー記録で、implementation head は `808991618373b0bf56d2cb6b04faf40e050a1f1a`。凍結 commit `97145dc` の §1〜§5 と開始時の本文が同一であることを照合した。§6 は B の実績、§7.1〜7.6 は前巡の所見として読んだ。
- **固定対象:** `c55891a498dc3ca99312533d3c9a23126e72fd0c..808991618373b0bf56d2cb6b04faf40e050a1f1a` の修正 diff（6 file、344 insertions、64 deletions）と、`3f79639c12eac16da77d06608675564e906f9a49..808991618373b0bf56d2cb6b04faf40e050a1f1a` の完全 diff（16 file、2022 insertions、5 deletions）を binary / full-index、stat、name-status で固定した。実装 file は変更していない。
- **実行環境:** sandbox 外で `pwsh -NoProfile -Command "Import-Module ./tools/Workflow/WindowsStoreContext.psm1 -Force; Get-OsmStoreContext"` を1回実行し `not-virtualized`。D1 の CreateNew / DeleteOnClose / final path 比較による結果で、アプリ名から推定していない。今回も suite の native probe case は重複を避けて除外した。probe directory の清掃・既存 file の削除はしていない。
- **Evidence の取得案内・状態:** 固定 diff、r3 §1〜§5、B result、生ログ・結果 JSON、限定再現 script / 出力、内容 hash と生成時刻の manifest を原sourceとして保持。依頼で publish / fetch が禁止されているため **未保存・引渡し未完了**。非仮想化の保存・実行担当へ引き継ぐ。判定 evidence / blind bundle は未生成。原source保持を保存・配布完了としていない。

**構造適合（機能照合より先に実施）**

- Plan（224行）は item 内の参照 path と `unchecked` をメモリモデルで検査する純粋関数のまま。公開4関数・依存・状態の所有者は変わらない。I/O（397行）は参照情報の採取、run 所有の記録 staging、receipt 検証、source handle の実体照合を担当する。追加 import の `WindowsStorePaths.psm1` は §3 で許可済みの OS 基盤で、Workflow / Evidence / Harness の domain import は増えていない。
- 判定 module（77行）・三つの入口の配置と順序、既存 resolver / domain / ACL 規則は初回から変わらない。Game / Framework、Runtime / Editor、asmdef、Unity source の変更は無い。規則変更は Plan、公開・回復変更は I/O、OS 判定は基盤へ閉じ、private seam と隔離 store で狙った単位を Unity なしに検査できる。
- I/O の棚卸し・公開・run 永続化は3活動の構造警報を引き続き持つが、同じ合流操作・run 所有者・寿命・依存・隔離テストを共有し、独立して変わる参照 policy は分離済みなので **非分割妥当**。500行未満だけを理由にしていない。今回は計画外の責務配置を要する新規構造 blocker は見つからなかった。

**前巡指摘の再照合**

各件は前巡と同じ semantic finding の追跡で、修正 commit は `8089916`。解消は §7.3 の具体的順序についての確認であり、関連条件全体の最終合格ではない。

- **C-01 / 解消:** 同じ item の `missing.json` 参照は捨てられず `hold/reference-missing` になった。独立の隔離再現 `C-01-missing-file-in-carried-item` と計画 suite `referenced-file-missing` で確認。運ぶ operation 内の欠落 file、target operation directory だけある状態、deployment receipt の欠落も保留する。一次コードは `StoreConsolidationPlan.psm1:59–99,169–184`。target-only 参照先の読取り失敗は、旧「欠落 file」順序とは別の C-06 として下記に記録する。
- **C-02 / 解消:** 17MB の有効 JSON に LocalCache path を含む順序は `source-unchecked`、`.json` 名の `not-json` は `source-invalid` で保留する。`unchecked-record-held` / `unexamined-json-held` が通過し、無関係な正常 unit は公開候補のまま。一次コードは `StoreConsolidation.psm1:48–70`、Plan `:62–75`。JSON 検査を省いたまま成功とする旧経路は無くなった。
- **C-03 / 解消:** manifest / receipt 書込みの途中に停止を注入しても、最終名には不完全 record が出ず run の `staging/records` にだけ残り、同じ manifest の再実行で `passed` に収束する。`fault-run-records` が manifest 中断 → receipt 中断 → 再実行成功を検証。最終 receipt を別途 `{` にした反例は `partial/receipt-invalid` となり、bytes は書き換えない。一次コードは I/O `:232–256,355–368,382–386`。旧実装が作った壊れた最終 manifest の救済を保証したという意味ではない。
- **C-04 / 未解決・owner 判断待ち:** 実装の公開順・reader は変更されていない。隔離再現 `C-04-merged-unit-visible-before-complete` は task 間参照で unitCount=1、最初の Workflow 公開後に observed=[alpha]、betaPublished=false、run=partial。対照の既存 `hidden-until-workflow` は Workflow が1つで初めは未公開の unit を検査し通過した。§6 の停止と事実の説明は正確で、採れなかった案を B が勝手に選んでいない。README `:127` の「公開途中の単位は…見えません」は現文面で Workflow 1つという限定を明記しておらず、§6 が述べるとおりその限定でだけ成立する説明である。修正文言や AC12 の読み方を本レビューで決めず、同じ C-04 の未解決事実として owner / r4 に残す。
- **C-05 / 解消:** 実行時棚卸し後・copy の open 直前に task directory を同一 bytes の outside directory への junction に替える隔離再現 `source-reparse-after-inventory` が通過。注入1回、unit は `partial/reparse`、Workflow は公開されず、outside bytes は不変。trace の read / write は連続して open の前にあるため、前巡の read trace 直後の順序と同じ読取り窓で確認した。一次コードは I/O `:257–281,341,297`。hash 一致に依存せず、開いた handle の final path を source root の実体と相対位置に照合する。

**新たな現在の問いを阻害する指摘 — 1件**

- **C-06 / P1 / semantic / unique / 修正要求として採用（未修正） — 読めない target-only 参照先を成立と扱う。** 違反根拠は **S2、D2 (iii) の実ストアの bytes・ACL による参照成立検査**（AC13 の参照検査を構成する失敗経路）。分類は **B 適応**。参照先の安全な成立を既存の Plan / I/O の責務内で検査する修正であり、新しい reader 契約・公開 API・所有者を要求しない。
  - **具体的順序:** source の Workflow alpha の `state.json` が、source には無い target の `Artifacts/transfers/<operation>/owned/a.zip` を指す → target operation/file の private ACL は正常 → 別 writer がその file を `FileShare.None` の read/write handle で開く → plan → 同じ状態で consolidate。正常に読める対照 plan は publish で、排他 open 後も hold にならない。
  - **一次証拠:** I/O `:52–72` は read 失敗を bytes=-1 / sha256=`unreadable` として返し、`:156–182` の target observation は path の存在と ACL 成功を保持する。Plan `:94–98,181–184` は参照先 path の存在・ACL / reparse だけを検査し、その hash が取得できたかを検査しない。隔離再現 `target-only-reference-unreadable` の出力は targetExists=true、targetAclOk=true、targetFileBytes=-1、targetFileSha=unreadable、planStatus=publish、reasons=[]、runStatus=passed、workflowPublished=true。実行時の再棚卸しも同じ失敗を受理する。実データや実 writer を使った結果ではない。
  - **修正確認に必要な範囲:** target-only 参照先の read 失敗で unit を保留し、全体完了にしないこと。正常な target-only 参照先を許可する対照も維持する。既存 entry を上書き・削除したり、読めない bytes を推定したりしない。旧 C-01 の欠落検出が直ったことを、この新しい失敗経路の合格へ一般化しない。

**凍結条件と新しい事実の分類**

- **I1 / I2 / AC1・2・14:** 判定は domain import / guard / 資格情報境界より前。42入口、probe の3失敗段階、51 module の静的 / 動的 import 検査は通過。I3 / AC3 は identity あり・非仮想化で起点回帰を確認した範囲のみ。identity 無しの host と既存全 suite を免除していない。
- **I4 / I5 / I6 / I7:** 非上書き公開、単位の衝突・ACL 不一致、source の hash / 時刻不変、元 bytes の複写、4点 fault、対象外 sentinel 不読取り、公開前 writer、事前 reparse は起点で通過。修正でこれらを破る新しい順序は今回の確認では見つからなかった。I6 の読取り境界は C-05、S2 は C-01・02 と新規 C-06、S3 / AC10 は C-03、AC12 は未解決 C-04 と対応する。S1 / AC4、AC5、S4 / AC11 は実環境・高コストの未確認を維持する。狭い回帰から I1〜I7 / S1〜S3 / AC1〜14 の全体合格を宣言しない。
- **§6 差分8:** `source-unchecked` / `source-invalid` は **B 適応**。S2 / D2 の検査を実施できない対象を保留し、検査省略を成功扱いしない不具合修正。16MB を受け入れ条件の新上限にしたり、保留を全体完了にしたりしていない。
- **§6 差分10:** run の staging record・非上書き rename・receipt identity / ACL 検証・run 所有 entry の ACL 回復は **B 適応**。S3 / AC10 の中断安全を、凍結した run 所有者・寿命・配置の中で回復する。receipt の破損を黙って完了とする例外は設けていない。
- **新しい transfers payload の source-invalid:** operation 内の payload も読取り・検査するコードと、共有 reader fixture が `.json` 名の平文を持ち合流 suite 内だけで有効 JSON に置き換えられた事実（test `:120–124`）は確認した。凍結 D2 の role 非依存検査に沿う実装詳細としては **B 適応**。DebugStudio property write の実対象にも該当 entry があり S4 が止まるかは **スパイク（S4 の成立性未確認）**。確認条件はその対象 unit の reasons、範囲は §5.5 の非仮想化担当による計画段階、終了点は `source-invalid` / `source-unchecked` による保留の有無の確定。今回の実データ plan 禁止により未実行。S4 を成立させるため検査対象や fail-closed / I5 の意味を変える必要が確定すれば **A 再開**。実対象を確認せず新規 blocker に数えず、後続へ移送して S4 を免除する扱いにもしていない。
- **後続への入力:** OS 基盤配置、host alias、非仮想化担当への経路は §7.5 / `Artifact Storage / Virtualized Host Route` のまま。残置、job binding、資格情報の有効性は §1.5 / U3・U4 の所有先に残す。今回の新規後続実装要求は無い。README の限定表現は C-04 の owner 判断後に合わせる入力であり、独立した新規 blocker に重複計上していない。

**実行したテスト・filter・結果**

全て sandbox 外の PowerShell 7 経路。隔離 temp store / fake transport / fake process の offline test であり、実ストアの plan / consolidate、Storage publish / fetch、資格情報操作、R2 通信は行っていない。

- **準備:** `dotnet build tools/Artifacts/{Packaging/ArtifactPackaging,Transport/R2ArtifactTransport,Probe/R2RouteTransport}.csproj -c Release -o tools/Artifacts/{Packaging/artifacts/package,Transport/artifacts/transport,Probe/artifacts/probe}` を対応する3組で実行。全て exit 0、warnings / errors 0。各 suite の process では cursor 同梱 `rg.exe` の directory を PATH に先頭追加（Cleanup の実行前に適用済み）。
- **コマンド形:** `pwsh -NoProfile -File <suite> -Case <filter> -ResultPath <原source>`。各 suite の stdout / stderr と結果 JSON を保持し、selected / executed の名前集合と非空・failed 0を照合。全10 suite 合計218 executed、全て exit 0。
- **StoreContext:** `tools/Workflow/tests/StoreContext.Tests.ps1`。filter は `decision-not-virtualized,decision-virtualized,decision-unknown-open,decision-unknown-final-path,decision-unknown-close,decision-unknown-empty-final-path,probe-outside-store,no-package-identity-input,cli-virtualized-all-entries,cli-unknown-failure-stages,cli-decision-before-store,import-has-no-store-side-effects`。12 selected / executed、failed 0。native probe 除外は開始時の1回確認と重複しないため。hostHasPackageIdentity=true、virtualizedEntries=42、importedModules=51。
- **他の起点 suite（filter は全て `*`）:** `tools/Artifacts/tests/StoreConsolidationPlan.Tests.ps1` 17/17、`StoreConsolidation.Tests.ps1` 15/15、`tools/Workflow/tests/TaskLifecycle.Tests.ps1` 23/23、`tools/Artifacts/tests/ArtifactEvidence.Tests.ps1` 19/19、`EvidenceCleanup.Tests.ps1` 35/35、`tools/Harness/tests/Harness.Tests.ps1` 35/35、`UnityGate.Tests.ps1` 21/21、`UnityObservation.Tests.ps1` 16/16、`UnityTestRunner.Tests.ps1` 25/25。全て failed 0。DLL 準備不足による再実行は無い。
- **限定再現:** 上記の C-01 欠落、C-06 読めない target-only 参照先（正常対照あり）、C-04 結合単位の途中可視化を、suite の隔離 store setup だけ再利用した Git 外 script で確認した（exit 0）。C-02・03・05 の旧順序は新 head の上記 named case で確認した。実 process の強制終了を使わず、record-write fault が書込み中断状態を作る検査である。
- **機械検査:** `pwsh tools/contract-audit.ps1` は exit 0、errors / warnings 0、検査8 not-applicable。全 tools PowerShell 106 file の AST parse は errors 0。レビュー記録更新後の `pwsh tools/docs-audit.ps1` は exit 0、errors / warnings 0。`git diff --check` も通過した。
- **未実行・未確認:** 判定 C の必須全 Artifacts suite、identity 無しの非仮想化 host（AC3）、実環境 AC4 / AC5、実データの ACL / payload 分類・plan、高コスト AC11 / S4、判定 C の照合 script、Evidence の保存・取得・別 session 閲覧確認。Unity test / Build は凍結 §5.1 の適用除外。判定 C と C' は未着手。

**出口:** C-01・02・03・05 の前巡順序は解消、新規 blocker 1件（C-06）。C-04 は未解決のまま次の担当を owner（r4 の要否判断）とし、C-06 を B への戻し項目として併記する。implementation head は据え置き、レビュー記録だけを commit し、既存 branch へ非 force push する。

## 8. Phase C'

未着手

## 9. Phase D

未着手
