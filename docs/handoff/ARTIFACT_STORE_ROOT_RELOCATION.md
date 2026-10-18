# Artifact Storage: store root の LocalAppData 外への移設

Artifact Storage・Workflow・Harness が共有する store の実体を、packaged app の AppData 書込み仮想化の対象外へ移し、どの host・どの起動経路から使っても同じ場所に書く契約にするスライス。`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §6 の保留項目「Virtualized Host Route」を owner が 2026-10-18 に再開し、問いを「仮想化された場所の中で動かす」から「仮想化されない場所へ移す」に置き換えたもの。

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A0 固定済み。A1 初稿を A2 入力版として固定。次は A2 独立レビュー）
- branch: `claude/artifact-store-root-relocation-plan`（PR base は `develop`）
- implementation base commit: `abb997751035ab3a284916a97c32f887a8faec16`（`origin/develop`、PR #138 を含む）
- implementation head commit: 未固定
- risk: `high`（保存先の移動、資格情報 root の base 変更、fail-closed 契約の追加、一回の移行を後続に持つ）
- owner: OSM maintainers（A3 の凍結と Phase D の判断は owner）
- created: 2026-10-18
- expires: 2026-12-18 または置換 revision
- harvest to: `tools/Artifacts/README.md`（store の所在、実行する process の条件、合流の対象 root）、`tools/Workflow/README.md`、`tools/Harness/README.md`、`docs/README.md` の program 表と H1 の所在、`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4・§6
- Phase A snapshot の取得案内・状態: 未生成（Phase A は Git 上の本文だけで完結する。A2 入力版と A3 凍結版の commit SHA と内容 sha256 を §5 に記録する）
- Phase B result snapshot の取得案内・状態: 未生成（未到達）
- evidence bundle の取得案内・状態: 未生成（未到達）
- C' blind bundle の取得案内・状態: 未生成（未到達）
- 引継ぎ票:
  - 次の Phase・役割: Phase A。A2 独立レビュー（2 件以上。1 件はアーキテクチャゲート、1 件は初稿を見ずに A0（§1〜§2 の A0 部分）だけから代替構成を出す）。その後 A3 統合・凍結。
  - 次の作業: §5「A2 独立レビュー」に記録した入力版（内容 sha256 を正、commit SHA を補助）を各レビュー担当に渡し、互いの指摘を見せずに観点別の findings を取る。各不変条件 I1〜I6 を破る順序を 1 つ挙げられるかを確かめる。指摘を §5 に採用・不採用・保留で記録し、A3 で owner と凍結する。§2「A3 で owner が決める事項」は A3 の入力。Phase B は始めない。
  - 引き継ぐ commit SHA: この branch の先頭 commit。開始時に `git log -1` の SHA と worktree の HEAD を照合し、作業ツリーが dirty なら引き継がない。
  - 取得案内の所在: なし（Phase A は Git 上の本文だけで完結する）。
  - 必要な権限: リポジトリの読み書きだけ。Phase A の間は実ストア・LocalCache 側の記録・資格情報・R2 に触らない。Unity は不要。
  - 停止規則: A0 の owner 決定（§2「本文へ転記した実装制約」）と衝突する設計が必要になったら、A1 を続けずに owner へ返す。A3 凍結後に Phase B を始めない。
  - 引き渡さないもの: ローカル store・LocalCache の実 path、資格情報、Codex 側 copy の内容。

### 着手手順

別 session が短い prompt から始められるように、Phase A 主担当の最初の手順をここに固定する。

1. `.agents/skills/osm-workflow/SKILL.md`、同 `references/phases-and-handoff.md`、`references/handoff-template.md`、`references/architecture-gates.md`、`references/docs-policy.md`、`docs/README.md` を全文読む。
2. `claude/artifact-store-root-relocation-plan` を checkout し、HEAD を引継ぎ票と照合する。
3. A1 初稿を書く。commit は A2 入力版（1 回）と A3 凍結版（1 回）だけにし、途中の改稿を積まない。A2 入力版は内容の sha256 を §5 に記録する。
4. A2 独立レビューを取り、指摘を §5 に採用・不採用・保留で記録する。
5. A3 で owner と凍結し、`pwsh tools/docs-audit.ps1` と `pwsh tools/contract-audit.ps1` を通してから PR を `develop` へ開く。PR 本文先頭に状態ブロックを置く。

### PR 状態ブロック

PR を作る前は §0 を唯一の正本とする。PR 作成時に次を本文先頭へ置く。

```text
状態: Phase A の <作業中 / 完了>
Phase A: revision <番号>、commit <SHA>
implementation base / head: abb99775 / 未固定
次の担当: <Phase と役割>。次の作業: <作業>
HANDOFF: docs/handoff/ARTIFACT_STORE_ROOT_RELOCATION.md
Evidence 取得案内: 未生成
未決事項: <項目 または なし>
```

## 1. 目的と対象外

- 目的: store root を AppData 書込み仮想化の対象外の固定位置に一元化し、非仮想化 process と packaged app 内の process の両方から同じ論理 path が同じ実体に解決されるようにする。移行前に新旧どちらの process が走っても store を再分裂させない fail-closed の振る舞いを契約に含める。
- 対象外:
  - 仮想化された場所（`%LOCALAPPDATA%` 配下）で Storage を動かし続ける経路。保留項目の旧い問いであり、このスライスで答えない。
  - 資格情報の保存方式（DPAPI）、private ACL 規則（owner と継承無しの 3 主体 FullControl）、環境変数を root の正として信用しない原則、OneDrive 同期配下・repo 配下の拒否。これらは変えずに引き継ぐ。
  - Codex のサンドボックス（restricted token / CodexSandbox ユーザー）への対応。owner は Codex を full access で動かすと決めた。
  - 別 PC・Cloud からの取得、Virtualized host 以外の保留項目。
  - `ArtifactsRoot`（`--artifacts-root` / `OSM_ARTIFACTS_ROOT`）。これは退避した Player・Content の置き場で store とは別物。触らない。
  - 実ストアの移行の実行、LocalCache 側 copy の合流（`store consolidate`）の実行、Task Scheduler の清掃 job の再登録、Codex 側残置の後始末。後続スライス（§2「ここでは答えない問い」）が所有する。
- 現況（A0 で固定した観測。日付はすべて 2026-10-18、別記のものを除く）:
  - store の実体は `%LOCALAPPDATA%\OneStarMaker` 配下（`Artifacts`、`Workflow`、`Harness`）に固定されている。tests 以外で `[Environment]::GetFolderPath('LocalApplicationData')` を直接呼んで root を組む file は 15 件ある（§3）。
  - `tools/artifacts.ps1`、`tools/workflow-task.ps1`、`tools/harness.ps1` は、引数解釈より前に `tools/Workflow/WindowsStoreContext.psm1` で呼出しごとの一意名 directory による probe を行い、仮想化されていれば `virtualized-store`、判定不能なら `store-context-unknown` で拒否する（PR #126）。
  - Codex デスクトップは Microsoft Store 配布の MSIX packaged app（package family `OpenAI.Codex_2p2nqsd0c76g0`、SignatureKind Store、`C:\Program Files\WindowsApps` 配下）。2026-10-17 以前に Codex 内の process が書いた Workflow / Harness の記録は package の LocalCache 側にだけあり、実ストアから読めない。読取のみの `store plan` は 2026-10-17 に 1 回行い、Harness の 4 task は `publish` 1・`hold` 3。`store consolidate` は未実行で、owner の manifest 承認待ち。
  - probe 1 回目（Codex デスクトップ、既定のサンドボックス有効）: Codex 内から `%USERPROFILE%\OneStarMaker.Probe\probe.txt` を作ろうとしてアクセス拒否、exit 1。実体側にも LocalCache 側にも file は作られなかった。サンドボックス有効時は workspace 外へ書けない。
  - probe 2 回目（Codex デスクトップ、full access: `approval_policy = "never"`、`sandbox_mode = "danger-full-access"`）: 同じ file の作成に成功。通常の非仮想化 pwsh から `Test-Path` が True、`%LOCALAPPDATA%\Packages\OpenAI.Codex_2p2nqsd0c76g0\LocalCache` 配下に同名 file は無し。`%USERPROFILE%` 直下は Codex デスクトップの書込み仮想化の対象外。
  - 未観測: full access の Codex から `%LOCALAPPDATA%` 配下へ書いたときの振り分け（既存の LocalCache 側記録から仮想化されると推定するが、full access での再観測はしていない）。別 drive 直下（例 `D:\`）の振り分け。package の初回状態（LocalCache が無い状態）の挙動。
  - Codex のサンドボックスは MSIX の仮想化とは別の層で、restricted token と専用ユーザーで動く。full access にすると両方の層のうちサンドボックスだけが外れる。
- A1 で追加した観測（コード読取、2026-10-18。設計の入力であり、A0 の観測を変えない）:
  - 不変の記録（Evidence の receipt・catalog、transfers の operation 記録）は、保存先の完全修飾 path を bytes のまま持つ。`EvidencePaths.psm1` の `Resolve-EvidenceStoredPath` と `ArtifactPaths.psm1` の `Resolve-ArtifactStoredPath` は、記録の path が「現在の実 root」または「論理 root（LocalAppData から組んだ path）」のどちらかの配下にあるときだけ、I/O の宛先を実 root に付け替える。移行後にこれらの記録を読むには、旧 root（`%LOCALAPPDATA%\OneStarMaker`）の論理 path を「読取専用の別名」として受け入れる必要がある。記録の bytes は書き換えない。
  - 清掃 job の binding manifest（`EvidenceSchedule.psm1` の `Assert-EvidenceStorageBinding`）は、各 role の `logicalPath` が LocalAppData から組んだ path と一致することを要求する。root を移すと既存の manifest は `runtime-binding-invalid` になり、job の再 install が要る（後続スライスが所有する「清掃 job の再登録」と一致する）。
  - `StoreConsolidationPlan.psm1` の参照検査（`Get-ConsolidationItemReferences`）は、記録中の完全修飾 path のうち論理 root 配下のものだけを参照として検査する。合流先を新 root にすると、LocalCache 側 copy の記録が持つ旧 root 配下の参照は「store 外の path」として検査から外れ、`reference-missing` の保護が効かなくなる。参照の判定にも旧 root の別名が要る。
  - `BuildContract.psm1` の `Assert-BuildSourceRoot` は、Build の取り込み元が store の所有領域（`Artifacts`、`Workflow`）やその親でないことを、論理 path と実体 path の両方で拒否する。移行前後のどちらの root も拒否境界に含める必要がある。
  - `StoreConsolidation.psm1` の `Assert-ConsolidationContext` は、合流の process が LocalAppData 非仮想化であることを要求する（source が LocalCache 側 path でも、実 path で読取専用に開く）。この要求は root を移しても変えない。
  - 各 module の test 用 root hook（`$script:TestRoot`、`LocalRootForTest`、`CredentialRootHook`、`BindingRootsHook`、`OwnedRootsForTest`、`DeploymentRoot`、Harness の `-StoreRoot`）は、production の root 導出を丸ごと置き換える形で既存 suite に使われている（Artifacts の tests 21 file、Workflow の tests 3 file。Harness は `-StoreRoot` 引数）。hook を残せば既存 suite の fixture は変わらない。
  - 新 root の候補 `<UserProfile>\OneStarMaker` は、旧 root より path が 13 文字短い。`tools/path-budget.json` の予算は repo 相対 path と checkout root の長さに関するもので、store の所在には関わらない。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: store root を AppData 外の固定位置に一元化し、移行前の process も移行後の process も store を再分裂させずに fail-closed に振る舞う実装は、既存の保存契約・資格情報契約・offline suite を保ったまま成立するか。
- 進める最低条件（A1 で不変条件と成立条件に分けた。A0 の候補はすべて観測可能な語に直して採用し、削ったものは無い。I2 は A0 の第 2 候補を「印」と「旧 root 残存」の 2 条件に分け、I6 を追加した）:
  - 不変条件（起きてはいけないこと）:
    - **I1 旧 root 不変:** このスライスのコードは、旧 root `<LocalApplicationData>\OneStarMaker` 配下のどの entry も作らず、変更せず、削除せず、移動しない。観測: 旧 root 配下の名前・属性・file の size と更新時刻の一覧が、どの CLI 呼出し・module 呼出しの前後でも一致する（`tools/Workflow/tests/StoreContext.Tests.ps1` の `Get-Tree` 方式）。例外は probe の一意名 directory（`<LocalApplicationData>` 直下、旧 root の外）だけ。
    - **I2 未移行での拒否:** 新 root `<UserProfile>\OneStarMaker` に移行済みの印（§4「移行済みの印」）が無い間、どの CLI（`artifacts.ps1`、`workflow-task.ps1`、`harness.ps1`）も、どの store module の root 解決も、新 root 配下に directory・file を作らず、印の file 以外の既存 file を開かず、reasonCode `store-not-migrated` で失敗する（CLI は `{"status":"failed","reasonCode":"store-not-migrated"}`、exit 1）。
    - **I3 旧 root 残存での拒否:** 移行済みの印があっても、LocalAppData が仮想化されていない process から見て旧 root に所有 role の directory（`Artifacts`、`Workflow`、`Harness` のいずれか）が残っている間、3 つの CLI は新 root に書かず、既存 file を開かず、reasonCode `legacy-store-present` で失敗する。LocalAppData の probe が `unknown` なら `store-context-unknown` で失敗する。
    - **I4 root 導出に環境変数を使わない:** 新 root・旧 root の導出は Known Folder（`UserProfile`、`LocalApplicationData`）からだけ行う。観測: `USERPROFILE`・`LOCALAPPDATA`・`HOME` を別の path に置き換えた子 process で導出結果が変わらない。CLI 引数にも root を受けない。
    - **I5 不適切な root の拒否:** 新 root が repo 配下、`D:\OneDrive\OSM-Artifacts` 配下、UNC、または root までのどの祖先が reparse point でも、印の有無にかかわらず `store-root-unavailable` で失敗する。印の file 自体が reparse point、所有者 SID 不一致、schema 不一致、または handle の実体 path が論理 path と違う場合も同じ。
    - **I6 秘密の非露出:** 鍵、暗号化 store、header、署名 URL、実 path の root 部分を、CLI の引数・stdout・stderr・ログ・Git・テスト結果へ出さない。reasonCode の出力は固定 code だけで、path・package 名・SID を含まない。
  - 成立条件（起きなければならないこと）:
    - **S1 単一 resolver:** tests 以外で LocalApplicationData から root を組んでいた 15 file（§3）が、production 経路では `tools/Workflow/WindowsStoreRoot.psm1` の `Get-OsmStoreRoot`（新 root）と `Get-OsmLegacyStoreRoot`（旧 root の別名。読取専用の照合にだけ使い、開かない）を経由し、`LocalApplicationData` から `OneStarMaker` を組む箇所が tests と `WindowsStoreContext.psm1`（probe の位置）を除いて 0 件になる。
    - **S2 同一実体への解決:** 移行済みの印がある新 root に対し、非仮想化 process と、full access の packaged app 内 process（Codex デスクトップ）の両方で、`Get-OsmStoreRoot` が返す path と、その配下に開いた file handle の実体 path（`GetFinalPathNameByHandle`）が、論理 path と一致し、互いに一致する。
    - **S3 資格情報 root の規則維持:** `CredentialPathAcl.psm1` の `Initialize-CredentialRoot` の allowed base が新 root になり、3 段（`<root>`、`<root>\Artifacts`、`<root>\Artifacts\credentials`）の初期化・ACL 検査（owner と継承無しの 3 主体 FullControl）・reparse 拒否・repo/OneDrive 拒否が、旧 root と同じ規則で通る。DPAPI の scope（CurrentUser）は変えない。
    - **S4 旧記録の読取:** 移行前に書かれた不変の記録が持つ旧 root 配下の完全修飾 path を、`Resolve-EvidenceStoredPath`・`Resolve-ArtifactStoredPath`・`StoreConsolidationPlan` の参照検査が新 root の同じ相対位置へ付け替えて扱う。記録の bytes は変わらない。
    - **S5 offline suite の合格:** `tools/Artifacts/tests`、`tools/Workflow/tests`、`tools/Harness/tests` の全 suite が新 root 前提で、各 suite 1 件以上実行・failed 0 で合格し、`pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` が error 0 で通る。
- 受け入れ条件（各条件の検証例。不変条件には失敗経路を含む。時点は「移行前」= 印なし・旧 root に store あり、「移行中」= 印あり・旧 root に所有 role あり、「移行後」= 印あり・旧 root に所有 role なし）:
  - **AC-I1-a（失敗経路、移行前）:** 印なし・旧 root に `Workflow` あり。3 CLI をそれぞれ 1 回呼ぶ。期待: 旧 root の tree 一覧が前後で一致。exit 1、`store-not-migrated`。
  - **AC-I1-b（失敗経路、移行中）:** 印あり・旧 root に `Harness` だけあり。非仮想化。3 CLI を呼ぶ。期待: 旧 root の tree 一覧が前後で一致、新 root 配下は印の file 以外に entry が増えない。exit 1、`legacy-store-present`。
  - **AC-I1-c（成功経路、移行後）:** 印あり・旧 root に所有 role なし（`H2a Review` 等の not-owned directory は残っていてよい）。`workflow-task.ps1 status` を呼ぶ。期待: 旧 root の tree 一覧が前後で一致（not-owned directory は開かれない）、新 root 配下に task directory が作られない（status は guard だけで状態を作らない）。
  - **AC-I2-a（失敗経路）:** 新 root directory が存在し印の file が無い。module 直接呼出し（`Get-ArtifactRoot`、`Get-EvidenceRoot`、`Get-WorkflowRoot`、`Get-TaskDirectory`、`Get-CredentialRoot`、`Get-BuildRoot`、`Get-ContentRoot`、`Get-ConsolidationLogicalRoot`、`Get-EvidenceBindingRoots`）。期待: すべて `store-not-migrated` で throw し、新 root 配下の tree 一覧が前後で一致。
  - **AC-I2-b（失敗経路）:** 新 root directory 自体が無い。期待: `store-not-migrated`、新 root が作られない（`<UserProfile>` 直下の一覧が前後で一致）。
  - **AC-I2-c（合格例・不合格例、終了境界）:** 印の file が存在するが 0 byte（書込み途中）。合格例: `store-root-unavailable` で失敗し何も作らない。不合格例: 印ありと扱って root を返す。
  - **AC-I3-a（失敗経路）:** 印あり・旧 root に `Artifacts` あり・probe `not-virtualized`。期待: 3 CLI が `legacy-store-present`、新 root 配下に entry が増えない。
  - **AC-I3-b（合格例、仮想化 process）:** 印あり・旧 root に `Workflow` あり・probe `virtualized`（fake IO seam）。期待: 3 CLI が旧 root 残存を理由に止まらず、新 root で処理に進む（LocalCache 側の残置は PR #126 の旧コードが書けない死んだ copy であり、分裂の writer にならない。§4「決定 D2」）。
  - **AC-I3-c（失敗経路）:** 印あり・probe `unknown`（fake IO seam で directory 作成失敗）。期待: `store-context-unknown`、新 root に書かない。
  - **AC-I3-d（合格例・不合格例、優先関係）:** 印なし・旧 root に `Workflow` あり・probe `virtualized`。合格例: `store-not-migrated`（印の検査が先）。不合格例: `legacy-store-present` や処理の続行。
  - **AC-I3-e（合格例、名前の境界）:** 旧 root に `Workflow` という名の file（directory でない）と `H2a Review` directory だけがある。期待: 所有 role の directory 無しとして扱い、`legacy-store-present` にならない。
  - **AC-I4-a（失敗経路）:** `USERPROFILE` と `LOCALAPPDATA` を temp 配下に差し替えた子 pwsh で `Get-OsmStoreRoot`・`Get-OsmLegacyStoreRoot` の導出 base を観測。期待: Known Folder の値のまま（test hook 無しの導出結果が環境変数に追従しない）。
  - **AC-I5-a（失敗経路）:** test hook で base を repo 配下、`D:\OneDrive\OSM-Artifacts` 配下、UNC（`\\server\share`）、祖先に junction を含む path にする。期待: 印の有無にかかわらず `store-root-unavailable`。
  - **AC-I5-b（失敗経路）:** 印の `ownerSid` が別 SID、`schemaVersion` が 2、`kind` が別文字列、JSON でない、4 KiB 超。期待: `store-root-unavailable`。
  - **AC-I5-c（失敗経路）:** 印の file の handle の実体 path が論理 path と違う（`IdentityHook` / probe seam で差し替え）。期待: `store-root-unavailable`。
  - **AC-I6-a（失敗経路）:** AC-I2〜I5 の各失敗出力（stdout・stderr・例外 message）に、base path・package 名・SID・`OneStarMaker` 以外の path 要素が含まれない。
  - **AC-S1-a:** `grep` 相当の機械検査: `tools/**/*.ps1`、`tools/**/*.psm1` のうち tests 配下と `WindowsStoreContext.psm1` を除いて、`LocalApplicationData` と `'OneStarMaker'` を同一行で組む箇所が 0 件。判定 C が `git grep` で確認する。
  - **AC-S1-b:** 15 file の各 root 関数を、module の `TestRoot` 等を未設定のまま、resolver の `BaseForTest` / `LegacyBaseForTest` hook だけを設定して呼ぶ。期待: 返る path が hook した base 配下の `OneStarMaker\<role>` になる（`tools/Artifacts/tests/StoreRootWiring.Tests.ps1`、`Harness.Tests.ps1`、`TaskLifecycle.Tests.ps1` に 1 case ずつ）。
  - **AC-S2-a（操作・実行時。§5 検証経路 V2）:** `<UserProfile>` 直下に観測担当が作った一時 base と fixture の印を使い、Codex（full access）内と非仮想化 pwsh の両方で fixture script を実行。期待: 両者の `Get-OsmStoreRoot` の出力と、書いた file の handle の実体 path が、論理 path と一致し互いに一致する。Codex 側が書いた file が非仮想化側の一覧に現れ、`<LocalApplicationData>\Packages\*\LocalCache` 配下に同名が無い。
  - **AC-S2-b（操作・実行時。§5 検証経路 V1）:** 移行前の実状態（印なし）で、Codex（full access）内と非仮想化 pwsh の両方から `workflow-task.ps1 status` を呼ぶ。期待: 両方 `store-not-migrated`・exit 1、`<UserProfile>\OneStarMaker` が前後で存在しない。
  - **AC-S3-a:** `Credentials.Tests.ps1` の既存 case（初期化、ACL 不一致拒否、reparse 拒否、repo/OneDrive 拒否、WriteLedger の境界）が、allowed base を resolver の base に差し替えた状態で全件合格。追加 case: `Initialize-CredentialRoot` が `<root>` の 1 段目に `Assert-SharedParent`（存在確認のみ、ACL 変更なし）を適用し、`<root>` が無ければ作らず `Credential operation unavailable.` で失敗（印があっても新 root を作らない）。
  - **AC-S4-a:** 新 root を base hook、旧 root を legacy hook にし、旧 root 配下の完全修飾 path を持つ receipt から `Resolve-EvidenceStoredPath` を呼ぶ。期待: 新 root の同じ相対位置へ解決し、旧 root 側は `Directory.Exists` も `File.Exists` も呼ばれない（`OpenHook` / `WriteHook` 相当の観測）。`Resolve-ArtifactStoredPath` も同じ。
  - **AC-S4-b:** `StoreConsolidationPlan.Tests.ps1` の in-memory fixture で、記録の参照が旧 root 配下を指す item。期待: 新 root の相対 key に写像されて参照検査され、参照先が無ければ `reference-missing`。LocalCache 配下の参照は従来どおり `reference-localcache`。
  - **AC-S4-c（合格例・不合格例）:** 参照が旧 root でも新 root でもない path（worktree の source）。合格例: 参照として扱わず記録しない（従来どおり）。不合格例: 旧 root の別名処理の副作用で `reference-missing` になる。
  - **AC-S5-a:** §5「判定必須テスト」の全 suite を `-ResultPath` 付きで実行し、各 JSON の executed が 1 件以上・failed が 0 件。
- ここでは答えない問いと所有する後続スライス:
  - 実ストアの新 root への移行の実行（同一 volume の move で ACL を保つか、別 drive への複写で ACL を再設定するか、移行済みの印の書込み）: 後続スライス「store root 移設の実行と合流」（`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §6 に入口条件付きで置く。入口条件はこのスライスの merge）。印の書込み関数はこのスライスに含めない（§4「決定 D4」）。
  - LocalCache 側 copy の合流先を新 root にすること、manifest 承認、`store consolidate` の実行: 同じ後続スライス。
  - Task Scheduler の Evidence 清掃 job の root 変更と再登録: 同じ後続スライス。
  - Codex 側残置（LocalCache 側 copy）の後始末: program §6 の既存項目のまま。
  - このスライスの merge から後続スライスの移行実行までの間、develop の CLI が `store-not-migrated` で全操作を拒否する運用上の空白の扱い（§2「A3 で owner が決める事項」の O1）: owner の判断。採られた選択肢によっては後続スライスの入口条件に「merge 直後に実行」が加わる。
  - `store plan` / `consolidate` の名前表に `retirements-v1` と印の file 名を登録すること: program §6「分裂したストアの合流」の既存入力（挙動は安全側の `unrecognized`）。このスライスでは印の file 名だけを `TopLevel` の表へ `marker` として登録し、開かない（§4）。
  - Codex を Phase 担当として運用する際の mode 固定（full access）と host 条件の文書化: このスライスの README 更新に含める（§4「決定 D7」。製品名に依存しない語で書く）。
- 判定定義: GO は進める最低条件（I1〜I6、S1〜S5）の全件成立。NO-GO はいずれかの不成立。CONDITIONAL ACCEPT は定義しない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約（owner 決定、2026-10-18）:
  - Codex デスクトップを Claude Code と並行する Phase 担当に加える。再開条件は、Cloud 取得経路の RC3 と同じ「担当として割り当てた」事実を採用する。
  - 方針は store root の移設であり、仮想化された場所の中で動かす経路は作らない。
  - Codex 独自のサンドボックスは使わない。実行主体は同一 Windows ユーザー。
  - 資格情報の保存方式・ACL 規則・Known Folder から導く原則は変えない。
  - AGENTS.md の常時契約（`#nullable enable` は Unity 側のみ、path 予算、Artifact Storage の事前承認の範囲、`Task.Delay` / `Thread.Sleep` の禁止）を守る。
- A1 で決めた事項（A0 の未決事項への回答。根拠は §4「決定」）:
  - 新 root の導出: 候補 (a) `UserProfile` Known Folder 直下の `OneStarMaker`。(b) 別 drive は未観測で volume の存在に依存し、(c) 設定 file は所在と ACL の問いを増やす。
  - 仮想化判定（probe）: LocalAppData 直下の probe を残し、結果の意味を「3 CLI の拒否」から「旧 root 残存検査を行うかの選択」に変える。新 root には書込み probe を置かず、印の file handle の実体 path 照合で読取の仮想化・reparse・UNC を検出する。
  - 移行済みの印: 新 root 直下の固定名 JSON file。旧 root 残存の検出は所有 role directory の存在（非仮想化 process のみ）。
  - 資格情報 root の base: resolver が返す新 root を allowed base にし、3 段の規則は不変。
  - `StoreConsolidation` / `StoreConsolidationPlan`: 合流先は新 root。旧 root は参照検査の別名として plan に渡す。
  - H1（`external-current-v1`）: 不採用。理由は §5「機械検査・H1」。
  - 全 EditMode 回帰: 適用除外。理由と代替証拠は §5。
- A3 で owner が決める事項:
  - **O1 merge 後の運用空白:** A1 の設計（I2）では、このスライスの merge から後続スライスの移行実行までの間、develop の 3 CLI はすべて `store-not-migrated` で失敗し、Evidence の stage / fetch・Workflow の start / end・Harness の current が使えない。候補: (α) そのまま受け入れ、後続スライスを merge 直後に実行する（A1 の採用案。fail-closed が単純で、root が 2 つ生きる状態を作らない）。(β) 印が無い間は旧 root で従来どおり動く「移行前 mode」を持ち、印が現れた後だけ旧 root を拒否する（空白は無くなるが、新コードが旧 root に書く経路が増え、I2 の「新 root に作らない・開かない」は保てるが「拒否する」ではなくなる。README に mode の見分け方が要る）。A1 は (α) を採用して書いた。(β) を選ぶ場合は Phase A revision 2 として I2 と §3・§4・§5 を改稿する。
  - **O2 印の file 名と内容:** §4「移行済みの印」の `store-root.json` と 4 field（`schemaVersion`、`kind`、`ownerSid`、`createdAt`）。後続スライスがこの形式で書く。

## 3. 責務マップ

責務は独立して変わる理由で数える。行数は `wc -l`（2026-10-18、base `abb99775`）。増分は見込み。

### 新規

| file | 責務 | 変更理由 | 所有者・寿命 | 依存（入力 → 出力） | 公開面 | テスト境界 | 配置理由 | 行数 |
|---|---|---|---|---|---|---|---|---|
| `tools/Workflow/WindowsStoreRoot.psm1`（新規） | store root の所在を決め、移行済みの印を検査し、旧 root の別名を返す OS 基盤層の resolver。directory も file も作らず、ACL を変えない。 | 「store がどこにあるか」「移行済みか」の判断。role の内訳や ACL 規則は持たない。 | 状態を持たない。process 寿命。 | `[Environment]::GetFolderPath`、`WindowsStorePaths.psm1`（実体 path 照合）、`WindowsStoreContext.psm1`（entry 用の probe）→ 文字列の root、または固定 reasonCode の throw。domain module（Artifacts / Workflow の TaskEventStore / Harness）を import しない。 | `Get-OsmStoreRoot`、`Get-OsmLegacyStoreRoot`、`Assert-OsmStoreEntry`。private seam: `$script:BaseForTest`、`$script:LegacyBaseForTest`、`$script:MarkerIoForTest`（印の open / 実体 path 照合の差し替え）。CLI・環境変数からは設定できない。 | temp directory を base にした native 実行と、seam による失敗注入。Unity 不要。 | `WindowsStorePaths` / `WindowsStoreContext` と同じ OS 基盤層。Artifacts も Harness も既にこの層を import しており、依存方向（domain → OS 基盤）を保てる。probe と別 file にするのは変更理由が違うため（probe: 「この process の AppData 書込みは仮想化されているか」、resolver: 「store はどこで、移行済みか」）。 | 新規 約 120 行 |
| `tools/Workflow/tests/StoreRoot.Tests.ps1`（新規） | resolver の全失敗経路と成功経路、I1〜I6 の module 側の検証。 | resolver の契約。 | テスト。 | resolver、temp directory。 | なし。 | 本体。 | resolver と同じ domain。 | 新規 約 300 行 |
| `tools/Workflow/tests/StoreRootHostFixture.ps1`（新規） | 検証経路 V2 の host 側 fixture。`-Base` の一時 base で resolver を通して file を 1 つ書き、論理 path と実体 path を JSON で出す。 | 操作・実行時の検証を機械的に再現するため。 | テスト。 | resolver（`BaseForTest` seam）。 | なし（tests 配下、CLI ではない）。 | 本体。 | V2 の手順を担当が同じ script で実行できるようにする。 | 新規 約 60 行 |
| `tools/Artifacts/tests/StoreRootWiring.Tests.ps1`（新規） | Artifacts 側 11 module の root 関数が resolver を経由することの配線検査（AC-S1-b）。 | S1 の検証。 | テスト。 | 各 module、resolver の seam。 | なし。 | 本体。 | 検査対象が Artifacts に集中するため。Workflow / Harness 側は既存 suite へ 1 case ずつ追加。 | 新規 約 120 行 |

### 変更（既存 15 file と周辺）

| file | 現在の root の組み方 | 変更後 | 変更理由・配置理由 | 現在行数 → 増分 |
|---|---|---|---|---|
| `tools/Artifacts/ArtifactPaths.psm1` | transfers の論理 root と shared 親（`<LocalAppData>\OneStarMaker`）を 3 箇所で組む | `Get-OsmStoreRoot` + `Artifacts\transfers`。`Resolve-ArtifactStoredPath` の論理 prefix に `Get-OsmLegacyStoreRoot` + `Artifacts\transfers` を読取専用の別名として追加。`New-ArtifactOperation` の shared 親は `<root>`（存在を要求、作らない）。 | S1、S4。責務（operation directory の作成と所有権）は不変。 | 118 → +10 |
| `tools/Artifacts/BuildContract.psm1` | `Assert-BuildSourceRoot` の所有領域境界 | 境界に新 root と旧 root の両方の `Artifacts` / `Workflow` を含める。 | 取り込み元が store のどちらの root でも拒否。 | 201 → +4 |
| `tools/Artifacts/BuildStore.psm1` | `builds-v1` の root | `Get-OsmStoreRoot` + `Artifacts\builds-v1`。shared 親の存在要求は不変。 | S1。 | 310 → +2 |
| `tools/Artifacts/ContentStore.psm1` | `contents-v1` の root | 同上。 | S1。 | 115 → +2 |
| `tools/Artifacts/Credentials/CredentialPathAcl.psm1` | `Get-CredentialRoot` と `Initialize-CredentialRoot` の base を LocalAppData から組む | base を `Get-OsmStoreRoot` の親（`<UserProfile>`）にし、allowed 3 段は `<root>`、`<root>\Artifacts`、`<root>\Artifacts\credentials`。`<root>` の段は `Assert-SharedParent`（存在確認のみ）。test 用の `TestBase` 経路は不変。 | S3。ACL 規則・DPAPI・WriteLedger の境界は変えない。 | 166 → +6 |
| `tools/Artifacts/EvidenceContract.psm1` | deployments の論理 root | `Get-OsmStoreRoot` + `Artifacts\deployments`。 | S1。 | 89 → +2 |
| `tools/Artifacts/EvidencePaths.psm1` | evidence-v2 の論理 root を 2 箇所で組む | `Get-OsmStoreRoot` + `Artifacts\evidence-v2`。`Resolve-EvidenceStoredPath` の論理 prefix に旧 root の別名を追加。 | S1、S4。 | 100 → +8 |
| `tools/Artifacts/EvidenceReset.psm1` | reset の対象 root | `Get-OsmStoreRoot` + `Artifacts`。 | S1。reset は後続スライスの運用で使うが、root の所在だけ変える。 | 235 → +2 |
| `tools/Artifacts/EvidenceSchedule.psm1` | runtime root、binding roots、credential root | 3 箇所を resolver 経由にする。`Assert-EvidenceStorageBinding` の suffix 検査（`\OneStarMaker\Workflow` 等）は不変。 | S1。既存 manifest の `logicalPath` 不一致は後続の再 install が解く。 | 422 → +6 |
| `tools/Artifacts/SeriesRetirementStore.psm1` | `retirements-v1` の root | `Get-OsmStoreRoot` + `Artifacts\retirements-v1`。 | S1。 | 105 → +2 |
| `tools/Artifacts/StoreConsolidation.psm1` | 合流先の実ストア root | `Get-ConsolidationLogicalRoot` を resolver 経由にし、`Get-ConsolidationCurrentPlan` へ旧 root を別名として渡す。`TopLevel` 表に印の file 名を `marker`（開かない）として登録。`Assert-ConsolidationContext`（非仮想化要求）は不変。 | S1、S4。 | 397 → +8 |
| `tools/Artifacts/StoreConsolidationPlan.psm1` | `Get-ConsolidationItemReferences` が `$LogicalRoot` 1 つで参照を判定 | `[string[]]$LogicalRoots` を受け、どれかの配下なら同じ相対 key に写像。pure のまま。 | S4。 | 248 → +6 |
| `tools/Harness/RecordStore.psm1` | `Get-TaskDirectory` の既定 StoreRoot | 既定を `Get-OsmStoreRoot` + `Harness`。`-StoreRoot` の差し替えは不変。`../Workflow/WindowsStoreRoot.psm1` を import（Harness → Workflow OS 基盤層。既に `harness.ps1` が同層を import している）。 | S1。 | 397 → +4 |
| `tools/harness.ps1` | `status` の一覧 root と入口の probe | 入口を `Assert-OsmStoreEntry` に置き換え、`status` の root を `Get-TaskDirectory` と同じ導出にする。 | S1、I2、I3。 | 247 → +2 |
| `tools/Workflow/TaskEventStore.psm1` | Workflow の root | `Get-OsmStoreRoot` + `Workflow`。 | S1。 | 241 → +2 |
| `tools/Workflow/WindowsStoreContext.psm1` | probe の位置（LocalAppData 直下） | コードは不変。冒頭コメントを「この結果は旧 root 残存検査を行うかの選択に使う」に改める。 | 意味の変更を文書化。 | 107 → +4（コメント） |
| `tools/artifacts.ps1`、`tools/workflow-task.ps1` | 入口の probe と `virtualized-store` / `store-context-unknown` の出力 | 入口を `Assert-OsmStoreEntry` に置き換える。失敗出力の形は不変で、reasonCode に `store-not-migrated`、`legacy-store-present`、`store-root-unavailable`、`store-context-unknown` が入る。`virtualized-store` は 3 CLI の入口からは出なくなる（`store plan` / `consolidate` の内部 code としては残る）。 | I2、I3。 | 195 / 76 → +2 / +2 |
| `tools/Artifacts/EvidenceCleanupJob.ps1` | root を組まない（binding manifest から読む） | 変更なし。binding の logicalPath 検査が resolver 経由になる（`EvidenceSchedule.psm1`）。 | — | 0 |
| `tools/Workflow/tests/StoreContext.Tests.ps1` | CLI 3 入口が `virtualized` で拒否することを検査 | CLI case を新しい意味に改める: `virtualized` + 印あり → 続行、印なし → `store-not-migrated`、`unknown` → `store-context-unknown`。判定が store module import より前に行われること、import に副作用が無いことの case は不変。 | I2、I3。 | 395 → +40 |
| 既存 suite（`Credentials.Tests.ps1`、`ArtifactEvidence.Tests.ps1`、`ArtifactTransfer.Tests.ps1`、`StoreConsolidation.Tests.ps1`、`StoreConsolidationPlan.Tests.ps1`、`EvidenceSchedule.Tests.ps1`、`BuildCollect.Tests.ps1`、`TaskLifecycle.Tests.ps1`、`Harness.Tests.ps1`） | module の `TestRoot` 等で root を差し替える | hook は残す。AC-S3-a、AC-S4-a〜c、AC-S1-b、BuildContract の両 root 拒否の case を追加。 | S1、S3、S4。 | 各 +10〜+40 |
| `tools/Artifacts/README.md`、`tools/Workflow/README.md`、`tools/Harness/README.md`、`docs/README.md`（H1 の所在の 1 文） | store の所在を LocalApplicationData と書く | 所在を `UserProfile` Known Folder 直下の `OneStarMaker` に改め、「実行する process の条件」を §4「決定 D2・D7」の意味に書き換え、reasonCode 表を更新し、合流の対象 root と旧 root の別名を書く。 | 公開面は今この瞬間に真であること。 | 文のみ |

行数警報: 500 行・3 責務・50% 増に当たる file は無い（最大は `EvidenceSchedule.psm1` 422 行 +6）。新規 resolver は「所在の決定」「印の検査」「旧 root 残存検査」の 3 つを持つが、いずれも「store の所在が正しく移行済みか」という 1 つの変更理由に属し、同じ依存・寿命・テスト方法を持つので非分割とする。probe と分けるのは前掲のとおり。

アーキテクチャゲートの照合（A1 自己確認）:

1. 独立して変わる判断の同居: resolver に role 名（`Artifacts\evidence-v2` 等）を持ち込まない。role の内訳は各 domain module のまま。
2. Policy と I/O: 印の検査は I/O（file open、実体 path 照合）を含むが、seam で差し替え可能にし、判定の規則（schema、SID、prefix 一致）は seam の外に置く。
3. 依存方向: domain（Artifacts、Workflow の TaskEventStore、Harness）→ Workflow OS 基盤層（`WindowsStore*`）。逆向きの import を作らない。resolver は domain を知らない。
4. 所有者・寿命: resolver は状態を持たない。印の所有者は後続スライス（書く側）で、このスライスは読むだけ。
5. 単体テスト: resolver は temp base と seam で Unity なしに全経路を検証できる。
6. 公開面: 関数 3 つ、reasonCode 3 つ（`store-not-migrated`、`legacy-store-present`、`store-root-unavailable`）。既存の `store-context-unknown` を再利用。
7. 将来の変更: root の所在だけを変えるなら resolver 1 file、role の追加なら domain module 1 file、probe の方式なら `WindowsStoreContext` 1 file。

## 4. 実装計画

### 決定（根拠付き）

- **D1 新 root は `<UserProfile Known Folder>\OneStarMaker`。** (a) は probe 2 回目で実体側へ書かれることを観測済み。`%USERPROFILE%` は MSIX の書込み仮想化（AppData 配下と HKCU）の対象外で、Codex の既定サンドボックスを外せば書ける。(b) 別 drive は未観測で、drive の有無が host ごとに違う。(c) 設定 file は「設定 file はどこに置き、誰が守るか」を新たに問う。ACL の既定は `%LOCALAPPDATA%` と同じく profile 継承で、private 境界は従来どおり `<root>\Artifacts\*`、`<root>\Workflow`、`<root>\Harness` の下に置く。
- **D2 probe は残し、意味を変える。** probe の目的は「この process が AppData へ書くと別実体へ行くか」の判定で、方式（一意名 directory）は PR #126 で検証済み。root が AppData 外へ出た後も、旧 root 残存（I3）を正しく観測できるのは非仮想化 process だけなので、probe の結果で残存検査の要否を選ぶ。`virtualized` の process は旧 root の LocalCache 側 copy を見るが、それは PR #126 以降の旧コードが書けない死んだ copy で、分裂の writer にならない（旧コードは `virtualized-store` で止まる）。`unknown` は従来どおり fail-closed。3 CLI は `virtualized` 自体では止まらなくなる（これが本スライスの目的）。`store plan` / `consolidate` は非仮想化要求を保つ（合流の I/O 意味を検証した前提を変えない）。新 root に書込み probe を置かないのは、追加の I/O と「store 内の probe directory」を名前表に増やすことに見合う未検出の脅威が無く、読取側の仮想化・reparse・UNC は印の file handle の実体 path 照合で検出できるため。S2 の検証経路で書込みの実体を実機観測する。
- **D3 検査の順序は 印 → probe → 旧 root 残存。** 移行前の通常状態（印なし・旧 root あり）で `store-not-migrated` を返し、`legacy-store-present` を「移行後に旧 root が再出現した」異常の専用 code にするため（AC-I3-d）。
- **D4 印の書込み関数はこのスライスに含めない。** I2 は「印が無い間は新 root に作らない」を要求し、印を書く関数は新 root を作る唯一の経路になる。書く側（移行の実行、move か copy かの選択、ACL 再設定）は後続スライスが所有し、このスライスは形式と読取・検査だけを定める。tests は fixture で印を書く。
- **D5 旧 root は「別名」として残す。** `Get-OsmLegacyStoreRoot` は path 文字列の導出だけで I/O をしない。用途は (1) 不変の記録の path bytes を新 root へ付け替える（S4）、(2) Build 取り込み元の拒否境界、(3) 3 CLI 入口の残存検査（存在確認のみ）。旧 root を開く・作る・消す経路をこのスライスに作らない（I1）。
- **D6 H1（`external-current-v1`）は不採用。** このスライスの branch の `harness.ps1` は移行前に `store-not-migrated` で止まるため、自分の固定記録を自分の store に書けない。Evidence の stage / fetch と Workflow の start / end は、合流前の実ストアを所有する **develop 版 CLI**（非仮想化 process、`D:\...\SampleGameForOneStarMakerFramework` の develop checkout）で行う。tracked HANDOFF（本書）を続ける。
- **D7 packaged app 内の実行条件を README に書く。** 製品名に依存しない語で、「packaged app 内の process は、その app 独自のサンドボックスを無効にし、同一 Windows ユーザーで実行する。サンドボックスが有効なら probe または印の読取が失敗し `store-context-unknown` / `store-root-unavailable` で止まる」と書く。Codex 固有の設定値（`approval_policy`、`sandbox_mode`）は README に書かず、各自の git 管理外の運用メモに置く。

### 移行済みの印

- 位置: `<root>\store-root.json`（`<root>` = `<UserProfile>\OneStarMaker`）。
- 内容: JSON object、field は次の 4 つだけ（過不足は `store-root-unavailable`）。`schemaVersion`: 整数 1。`kind`: 文字列 `osm-store-root`。`ownerSid`: 書いた Windows ユーザーの SID 文字列（現在ユーザーと一致を要求）。`createdAt`: ISO 8601 文字列。
- 読取: `FileShare.Read` で開き、4 KiB 以下、UTF-8（BOM 無し）。handle の実体 path（`GetFinalPathNameByHandle`）が論理 path と一致しなければ `store-root-unavailable`。reparse point（file 自体と祖先）は拒否。存在しなければ `store-not-migrated`。
- 書く側: 後続スライス「store root 移設の実行と合流」。移行（move または copy + ACL 再設定）の最後に書く。このスライスは書かない（D4）。
- 合流の名前表: `StoreConsolidation.psm1` の `TopLevel` に `store-root.json` を `marker` として登録し、列挙はするが開かず、manifest の `excluded` に理由 `marker` で載せる。

### resolver の契約（`tools/Workflow/WindowsStoreRoot.psm1`）

- `Get-OsmStoreRoot`: `[Environment]::GetFolderPath('UserProfile')`（seam `BaseForTest`）→ `<base>\OneStarMaker` → 静的拒否（完全修飾でない、`\\` 始まり、repo 配下、`D:\OneDrive\OSM-Artifacts` 配下）→ 祖先と root の reparse 拒否 → 印の検査 → root 文字列を返す。失敗は throw（message は固定 reasonCode）。directory・file を作らず、ACL を変えない。呼出しごとに検査する（cache しない。`AppDomain` の binding と同じく process 内の差し替えを許さない）。
- `Get-OsmLegacyStoreRoot`: `[Environment]::GetFolderPath('LocalApplicationData')`（seam `LegacyBaseForTest`）→ `<base>\OneStarMaker`。I/O 無し。
- `Assert-OsmStoreEntry`: 3 CLI の入口専用。順に (1) `Get-OsmStoreRoot`（印）、(2) `Get-OsmStoreContext`（probe。`unknown` → `store-context-unknown`）、(3) `not-virtualized` のときだけ旧 root の `Artifacts` / `Workflow` / `Harness` の directory 存在確認（`[IO.Directory]::Exists` のみ。開かない）→ あれば `legacy-store-present`。例外は呼出し側で `{"status":"failed","reasonCode":<code>}`・exit 1 にする（既存の出力形）。store module の import より前に呼ぶ（既存と同じ位置）。
- 出力・例外に path・SID・package 名を含めない（I6）。

### 変更対象と順序

1. resolver と `StoreRoot.Tests.ps1`（AC-I2、I4、I5、I6、I3-b〜e の module 側）。
2. 3 CLI の入口を `Assert-OsmStoreEntry` に置き換え、`StoreContext.Tests.ps1` の CLI case を改める（AC-I1-a〜c、I3-a〜d）。
3. Artifacts 11 module・Harness・Workflow の root を resolver 経由にし（S1）、`StoreRootWiring.Tests.ps1` と既存 suite への追加 case（AC-S1-b、S3-a、S4-a〜c、BuildContract の両 root 拒否）。
4. `StoreConsolidationPlan` の複数論理 root と名前表の `marker`（AC-S4-b、c）。
5. `StoreRootHostFixture.ps1`（V2 用）。
6. README 4 件と `docs/README.md` の 1 文（D7 を含む）。
7. `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、§5 の起点 filter の suite を B の出口で通す。

### Phase B から Phase A へ差し戻す条件

- 印の形式・位置、reasonCode の集合、検査順序（D3）を変える必要が出た。
- resolver が domain module を import しないと成立しない、または domain module 間に新しい import が要る。
- 旧 root を開く・作る・消す経路が必要になった（I1 に抵触）。
- `Resolve-*StoredPath` の別名処理で、記録の bytes を書き換えないと読めない記録が見つかった（S4 の前提崩れ）。
- 既存 suite の fixture（`TestRoot` 等の hook）を外さないと新 root 前提にできない。
- Unity 側コードの変更が必要になった（§5 の適用除外の前提崩れ）。

### 対象外を維持する方法

- 実ストア・LocalCache 側・資格情報・R2 に Phase B で触らない。B のテストは temp base だけを使う。V1 は読取（存在確認）と失敗出力の観測だけで、V2 は `<UserProfile>` 直下の一時 base で行い、観測担当が終了時に削除する。
- 印を書く関数・移行 script・job 再登録・`consolidate` の実行は書かない。
- `ArtifactsRoot` 系（`--artifacts-root` / `OSM_ARTIFACTS_ROOT`）に触れない。
- Codex 固有の設定値を tracked 文書へ書かない。

## 5. テストとレビュー計画

- 単体テスト:
  - 新規 `tools/Workflow/tests/StoreRoot.Tests.ps1`: AC-I2-a〜c、AC-I3-b〜e（module 側）、AC-I4-a、AC-I5-a〜c、AC-I6-a。各 case は temp base を使い、失敗経路では base 配下の tree 一覧が前後で一致することを確認する。
  - 改修 `tools/Workflow/tests/StoreContext.Tests.ps1`: CLI 3 入口の case を D2 の意味に改め、AC-I1-a〜c、AC-I3-a〜d を CLI 境界で確認。判定が store module import より前であること、import の副作用が無いことの既存 case は維持。
  - 新規 `tools/Artifacts/tests/StoreRootWiring.Tests.ps1` と `Harness.Tests.ps1`・`TaskLifecycle.Tests.ps1` への追加: AC-S1-b。
  - 既存 suite への追加: `Credentials.Tests.ps1`（AC-S3-a）、`ArtifactEvidence.Tests.ps1`・`ArtifactTransfer.Tests.ps1`（AC-S4-a）、`StoreConsolidationPlan.Tests.ps1`・`StoreConsolidation.Tests.ps1`（AC-S4-b、c、名前表の `marker`）、`BuildCollect.Tests.ps1`（両 root の取り込み元拒否）。
  - すべて `Task.Delay` / `Thread.Sleep` を使わない。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `tools/Workflow/tests/StoreRoot.Tests.ps1`、`tools/Workflow/tests/StoreContext.Tests.ps1`、`tools/Artifacts/tests/StoreRootWiring.Tests.ps1`、`tools/Artifacts/tests/Credentials.Tests.ps1`、`tools/Artifacts/tests/ArtifactEvidence.Tests.ps1`、`tools/Artifacts/tests/StoreConsolidationPlan.Tests.ps1`、`tools/Workflow/tests/TaskLifecycle.Tests.ps1`、`tools/Harness/tests/Harness.Tests.ps1`（各 `-Case '*' -ResultPath <path>`）。
- 判定必須テスト（GO 候補 head）: `tools/Artifacts/tests/*.Tests.ps1`（全 file）、`tools/Workflow/tests/*.Tests.ps1`（全 file）、`tools/Harness/tests/*.Tests.ps1`（全 file）を各 `-Case '*' -ResultPath <path>` で実行し、各 JSON の executed 1 件以上・failed 0 件。`pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` が error 0。AC-S1-a の `git grep`。V1・V2 の生観測（下記）。`git diff --stat abb99775..<head> -- unity/` が空であること。
- 全 EditMode 回帰の適用除外: 適用除外。理由: 変更は `tools/` の PowerShell と README に限り、Unity 側（`unity/`）のコード・asmdef・Scene・Prefab・Addressables を変えない。代替証拠: 上記の offline suite 全件、`contract-audit`、`docs-audit`、`git diff --stat -- unity/` が空。
- 統合・Unity テスト: なし（Unity を使わない）。
- 操作・実行時・目視条件の検証経路:
  - **V1（AC-S2-b、I2 の実機）:** 対象: GO 候補 head。環境・初期状態: 実状態（印なし。`<UserProfile>\OneStarMaker` が存在しない）。担当: 非仮想化側 = 判定 C の実行担当（通常の pwsh）、packaged app 側 = owner が割り当てた Codex デスクトップの session（full access、同一 Windows ユーザー、作業 worktree は GO 候補 head）。操作: 両側で `pwsh tools/workflow-task.ps1 status -Task store-root-v1-check` を 1 回ずつ。観測: stdout の JSON と exit code。非仮想化側が前後に `[IO.Directory]::Exists([IO.Path]::Combine([Environment]::GetFolderPath('UserProfile'),'OneStarMaker'))` と、旧 root 直下の名前一覧（名前と属性のみ。内容は読まない）を記録。期待（判定 C が照合）: 両側 `store-not-migrated`・exit 1、新 root が前後とも存在しない、旧 root の名前一覧が前後で一致。保存証拠: 両側の生出力と前後の一覧を 1 つの時刻付きテキストにし、C / C' の bundle に含める。packaged app 側の出力は非仮想化側が受け取って同じ file に貼る。
  - **V2（AC-S2-a、S2 の実機）:** 対象: GO 候補 head。環境・初期状態: 非仮想化側が `<UserProfile>` 直下に `OneStarMaker.HostCheck.<32 桁 hex>` を作り、その中に `OneStarMaker\store-root.json`（fixture の印。`ownerSid` は現在ユーザー）を置く。担当: V1 と同じ。操作: packaged app 側で `pwsh tools/Workflow/tests/StoreRootHostFixture.ps1 -Base <その一時 base> -Action write`（resolver の seam で base を差し替え、`<root>\hostcheck\<guid>.txt` を 1 つ書き、`Get-OsmStoreRoot` の返り値・書いた file の handle 実体 path・`Get-OsmStoreContext` の生結果を JSON で出す）。非仮想化側で `-Action read`（同じ root の一覧と各 file の handle 実体 path を JSON で出す）。観測: 両 JSON。非仮想化側が `<LocalApplicationData>\Packages\*\LocalCache` 配下に同名 file が無いことを `Test-Path` で記録。期待: 両側の root が一致、file の実体 path が論理 path と一致、packaged app 側が書いた file が非仮想化側の一覧にある、LocalCache 側に無い。packaged app 側の `Get-OsmStoreContext` の生結果（`virtualized` / `not-virtualized` / `unknown`）は合否に使わず記録する（A0 の未観測項目の初回観測）。後始末: 非仮想化側が一時 base を削除する。保存証拠: V1 と同じ形式。
  - V1・V2 は不可逆でも高コストでもないため高コスト証拠に分類しない。packaged app 側の担当を用意できなければ `未実施` とし、判定 C を完了と記録しない。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: packaged app（Codex デスクトップ、full access）内で pwsh を起動して repo の script を実行し file を書く経路は、A0 の probe 2 回目で疎通済み（file 作成と実体側への到達）。未確認なのは full access での LocalAppData 側 probe の振り分けで、V2 が生結果として記録する（合否に使わない）。追加の検証支援は `StoreRootHostFixture.ps1`（§3）。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし（V1・V2 は構造・値の検査で閉じる）。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: 判定 C の offline 実行。判定 C の担当が V1・V2 の生 JSON の複製に対し、root の一致・実体 path の一致・一覧の包含を照合する小さな script を自分の環境で書いて実行する。資格情報・通信・実ストアへのアクセスは不要。原記録へ適用する前に、A3 で凍結する合成 fixture（一致する組と、実体 path が LocalCache 配下に逸れた不一致の組の 2 件）で一度通す。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC-S1-a の `git grep -n "LocalApplicationData" -- 'tools/**/*.ps1' 'tools/**/*.psm1'`（tests と `WindowsStoreContext.psm1` 以外に `OneStarMaker` と同一行のものが無いこと）。
- 機械検査・H1: H1（`external-current-v1`）は不採用（D6）。理由: この branch の `harness.ps1` は移行前に `store-not-migrated` で止まり、固定記録を書けない。Evidence の stage / fetch と `workflow-task.ps1` の start / end は、develop checkout の CLI を非仮想化 process で使う（この branch の CLI は使わない）。task id は `artifact-store-root-relocation`。
- A0/A1 主担当・モデル・ベンダー: A0 = owner と Phase A 補助 session（本 revision の A0 固定）。A1 = Phase A 主担当 session（Claude Code、Anthropic、Claude Fable 5.1）。
- A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版（内容 sha256 を正、commit hash を補助）:
  - 入力版: A2 入力版の内容 sha256 と commit SHA は A2 開始時にここへ記録する（記録前は「未記録」）。
  - R1（アーキテクチャゲート + 各不変条件を破る順序の構成）: 未実施。
  - R2（初稿を見ず、A0 だけから代替構成）: 未実施。
- A3 統合担当・モデル・採否: 未実施。
- C' 用に予約した担当・モデル・ベンダー: A2 で使わない系列を 1 つ残す。実績は C' の欄に記録する。
- 独立性の強化条件を満たせない場合の理由: 未記録。

## 6. Phase B 実装結果

未到達。

## 7. Phase C

未到達。

## 8. Phase C'

未到達。

## 9. Phase D

未到達。
