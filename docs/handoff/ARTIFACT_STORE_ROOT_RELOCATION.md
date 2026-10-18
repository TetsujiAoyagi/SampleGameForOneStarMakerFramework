# Artifact Storage: store root の LocalAppData 外への移設

Artifact Storage・Workflow・Harness が共有する store の実体を、packaged app の AppData 書込み仮想化の対象外へ移し、どの host・どの起動経路から使っても同じ場所に書く契約にするスライス。`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §6 の保留項目「Virtualized Host Route」を owner が 2026-10-18 に再開し、問いを「仮想化された場所の中で動かす」から「仮想化されない場所へ移す」に置き換えたもの。

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `動作確認`（Phase A revision 1 を 2026-10-19 に owner が凍結。Phase B は 2026-10-19 に完了し、B の出口 commit で終えた。動作確認は V1 非仮想化側だけ実施済み）
- branch: `claude/artifact-store-root-relocation`（PR base は `develop`。Phase A の作業 branch `claude/artifact-store-root-relocation-plan` は A0 の commit `eebcdda7` で止まっており、A1〜A3 の commit はこの branch にある）
- implementation base commit: `abb997751035ab3a284916a97c32f887a8faec16`（`origin/develop`、PR #138 を含む）
- implementation head commit: 未固定
- risk: `high`（保存先の移動、資格情報 root の base 変更、fail-closed 契約の追加、一回の移行を後続に持つ）
- owner: OSM maintainers（A3 の凍結と Phase D の判断は owner）
- created: 2026-10-18
- expires: 2026-12-18 または置換 revision
- harvest to: `tools/Artifacts/README.md`（store の所在、実行する process の条件、合流の対象 root）、`tools/Workflow/README.md`、`tools/Harness/README.md`、`docs/README.md` の program 表と H1 の所在、`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4・§6
- Phase A snapshot の取得案内・状態: Git 上の本文で完結（A2 入力版は §5 に commit SHA と内容 sha256。A3 凍結版 = この branch の先頭 commit で、commit message に `A3 freeze` を含む）
- Phase B result snapshot の取得案内・状態: Git 上の commit で完結（B の出口 commit = この branch の先頭 commit で、commit message に `Phase B exit` を含む）。`evidence stage` の snapshot は未生成（同一 PC の引継ぎで、commit SHA で足りるため）
- evidence bundle の取得案内・状態: 未生成（未到達）
- C' blind bundle の取得案内・状態: 未生成（未到達）
- 引継ぎ票:
  - 次の Phase・役割: 動作確認（Phase B の後）。
  - 次の作業: B の出口 commit を対象 head として、§5 の検証経路のうち未実施の V1 packaged app 側と V2 両側（非仮想化側と packaged app 側）を各 1 回、手順どおりに実行し、生の観測と期待との差の有無を §6「動作確認」に残す（GO / NO-GO と合否は書かない）。V1 の非仮想化側は B が 1 回実行済み（§6。判定の入力にしない）。packaged app 側は Codex 側 session が担当する。期待と違う観測があれば発見 C を待たずに B へ戻す（この往復は B と発見 C の差し戻しの巡に数える）。差が無ければ引継ぎ票を発見 C へ更新する。発見 C は動作確認の完了を待たずに開始してよい（進め方は下の「Phase C〜C' 進行役の着手手順」）。
  - 引き継ぐ commit SHA: この branch の先頭 commit（B の出口。commit message に `Phase B exit` を含む）。開始時に `git log -1` で確認し、作業ツリーが dirty なら引き継がない。
  - 取得案内の所在: なし（B の結果は Git 上の commit と §6 で完結する。B の suite の result JSON と V1 の生記録は B session の作業領域にあり、判定の入力にしない）。
  - 必要な権限: リポジトリの読み、pwsh 7、packaged app（Codex デスクトップ、full access）内での pwsh 実行。V2 は `<UserProfile>` 直下の一時 base を作り、観測後に削除する。実ストア・LocalCache の中身・資格情報・R2 は触らない。
  - 停止規則: 手順が §5 の記述どおりに実行できない、または実ストア・旧 root に書く必要が生じたら実行を止め、観測と理由を §6 に書いて B へ返す。
  - 引き渡さないもの: ローカル store・LocalCache の実 path、資格情報、Codex 側 copy の内容。

### Phase B の着手手順（完了済み。記録として残す）

別 session が短い prompt から始められるように、Phase B 担当の最初の手順をここに固定する。

1. `AGENTS.md`、`.agents/skills/osm-workflow/SKILL.md`、同 `references/phases-and-handoff.md`、`references/architecture-gates.md`、`references/docs-policy.md` を全文読む。次に本書を全文読む（§2〜§5 が凍結済みの契約と計画）。
2. branch `claude/artifact-store-root-relocation` を checkout し、`git log -1` の message に `A3 freeze` が含まれ、作業ツリーが clean であることを確認する。
3. §4「変更対象と順序」の 1〜7 を順に実装する。各 step で対応する AC の case（§5 のテスト ID）を同時に書き、`pwsh <suite> -Case '*' -ResultPath <path>` で通す。
4. 設計判断が必要になったら §4「差し戻す条件」と照合し、該当すれば止めて Phase A へ返す（HANDOFF §6 に理由を書く）。
5. 出口: `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` が error 0、起点 filter の suite が全件 executed ≥ 1・failed 0。§6 を書き、引継ぎ票を動作確認担当へ更新し、commit する（push・PR はしない）。

### Phase C〜C' 進行役の着手手順

進行役は発見 C・判定 C・C' の各担当 session を順に用意し、引継ぎ票と PR 状態ブロックの正本（PR を更新するまでは本書 §0）を保つ。自分ではレビュー・判定・監査をしない。各担当への依頼は「本書 §0 の引継ぎ票に従って <Phase・役割> を行う」の一文にし、足りないことは依頼文でなく本書へ先に書いて commit する。

1. `AGENTS.md`、`.agents/skills/osm-workflow/SKILL.md`、同 `references/phases-and-handoff.md`・`references/review-evidence.md`・`references/architecture-gates.md` を全文読む。次に本書 §0・§2〜§6 を読む。
2. 照合: branch `claude/artifact-store-root-relocation` の先頭が B の出口 commit（message に `Phase B exit`）で、作業ツリーが clean であること。食い違えば止める。
3. 動作確認（packaged app 側）: V1 の packaged app 側と V2 の両側には Codex デスクトップの session が要り、進行役は起動できない。owner に割当てを依頼し、その間に発見 C を進める。V1・V2 の生観測が判定 C の入力になるのは、GO 候補 head で、B 以外の実行担当が取ったものだけ（§6 の V1 非仮想化側は B の観測で入力にしない）。packaged app 側の出力は非仮想化側が受け取り、§5 の形式の時刻付きテキストに貼る（Codex 側から Evidence を直接渡す経路は使わない）。
4. 発見 C: 新規 session、Phase B と異なるモデル。入力は commit 固定の差分 `abb99775..<B の出口 commit>` と本書（同一 PC なので stage snapshot は要らない）。構造レビュー（§3 の責務マップとの照合）を機能レビューより先に行い、§5「Phase C / C' で指摘されうる点」を判断の拘束にしない。指摘は blocker（違反する凍結済み条件・常時契約と一次証拠）と後続スライスの入力に分け、§7 に書く。差し戻しは修正用の新規 B session へ渡し、B ↔ 発見 C は 4 巡まで（動作確認からの差し戻しも数える）。修正後の確認は §5 の起点 filter を使う。
5. GO 候補 head の固定: blocker が無くなったら、未公開の試行錯誤 commit を整理し（A3 凍結版 `b07cd685` 以前は整理しない）、head を固定して §0 の implementation head に書く。判定 evidence を取る head は push しない軽量 tag `evidence/artifact-store-root-relocation/<n>` で到達可能に保つ。
6. 判定 C: 実行担当の session（B 以外）が、GO 候補 head を detach した新しい worktree で §5「判定必須テスト」だけを実行する。実行前に `tools/Artifacts/README.md` の保守節にある 3 project の Release build を行い、`rg` を PATH に足す（どちらも無いと環境要因で落ちる。無ければ実行せず止める）。V1・V2 の非仮想化側もこの実行担当が行う。判断担当（Phase B と異なるモデル）が JSON の case 名・件数（§5 のテスト ID 一覧）を照合し、§5 の照合 script を合成 fixture 2 件に通してから V1・V2 の原記録へ適用する。V1・V2 が揃うまで判定 C を完了と記録しない。
7. Evidence: bundle の stage / fetch と `workflow-task.ps1` の start / end は、この branch の CLI では `store-not-migrated` で止まるため、develop checkout の CLI を非仮想化 process で使う（§4 D6。task id は `artifact-store-root-relocation`）。bundle の構成と保存先は `review-evidence.md` に従い、取得案内の状態を §0 に書く（reference・packageSha256 の実値と local path は書かない）。
8. C': 判定 C の証拠が揃った head でだけ、新規 session で開始する。モデルは Phase B・C と異なり、強化条件として Phase A・B・C に関与していない系列（§5 の予約どおり Claude 系・GPT 系以外）を使う。入力は blind audit bundle だけで、本書全文や発見 C・判定 C の結論を渡さない。結果と使用モデルは §8 に記録する。
9. 公開点: push と PR #139 本文の状態ブロック更新は、C' の結果を owner に報告して確認を得てから行う。実ストアの移行（印の書込み）は後続スライスの仕事で、この進行では行わない（§2 O1）。
10. 各 Phase の出口で §0 の status・引継ぎ票を次の担当へ更新して commit する。

### Phase A の着手手順（完了済み。記録として残す）

Phase A 主担当の最初の手順。

1. `.agents/skills/osm-workflow/SKILL.md`、同 `references/phases-and-handoff.md`、`references/handoff-template.md`、`references/architecture-gates.md`、`references/docs-policy.md`、`docs/README.md` を全文読む。
2. `claude/artifact-store-root-relocation-plan` を checkout し、HEAD を引継ぎ票と照合する。
3. A1 初稿を書く。commit は A2 入力版（1 回）と A3 凍結版（1 回）だけにし、途中の改稿を積まない。A2 入力版は内容の sha256 を §5 に記録する。
4. A2 独立レビューを取り、指摘を §5 に採用・不採用・保留で記録する。
5. A3 で owner と凍結し、`pwsh tools/docs-audit.ps1` と `pwsh tools/contract-audit.ps1` を通してから PR を `develop` へ開く。PR 本文先頭に状態ブロックを置く。

### PR 状態ブロック

PR #139 は存在するが、Phase B は push・PR 本文の更新を行わない（B の出口 commit は未 push）。PR の状態ブロックは A3 凍結時点のままであり、次の公開点（[公開点](../../.agents/skills/osm-workflow/SKILL.md#公開点とgit履歴)）で次の形へ更新する。それまでは §0 を唯一の正本とする。

```text
状態: 動作確認（Phase B 完了）
Phase A: revision 1、commit <A3 凍結 commit の SHA>
implementation base / head: abb99775 / 未固定（B の出口 commit は §0 の引継ぎ票）
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
  - `<LocalApplicationData>\OneStarMaker\RevisionLocks\v1`（Unity Runtime の `RevisionProcessLease` が同一 Windows ユーザーの process 間調停に使う lock file の置き場。`unity/Assets/OneStarMaker/Scripts/Runtime/BuildContent/RevisionProcessLease.cs`）。store の role ではなく、Unity 側コードを変えないこのスライスでは移設しない。合流の名前表でも `revision-locks` として開かない既存分類のまま。
- 現況（A0 で固定した観測。日付はすべて 2026-10-18、別記のものを除く）:
  - store の実体は `%LOCALAPPDATA%\OneStarMaker` 配下（`Artifacts`、`Workflow`、`Harness`）に固定されている。tests 以外で `[Environment]::GetFolderPath('LocalApplicationData')` を直接呼んで root を組む file は 15 件ある（§3）。
  - `tools/artifacts.ps1`、`tools/workflow-task.ps1`、`tools/harness.ps1` は、引数解釈より前に `tools/Workflow/WindowsStoreContext.psm1` で呼出しごとの一意名 directory による probe を行い、仮想化されていれば `virtualized-store`、判定不能なら `store-context-unknown` で拒否する（PR #126）。
  - Codex デスクトップは Microsoft Store 配布の MSIX packaged app（package family `OpenAI.Codex_2p2nqsd0c76g0`、SignatureKind Store、`C:\Program Files\WindowsApps` 配下）。2026-10-17 以前に Codex 内の process が書いた Workflow / Harness の記録は package の LocalCache 側にだけあり、実ストアから読めない。読取のみの `store plan` は 2026-10-17 に 1 回行い、Harness の 4 task は `publish` 1・`hold` 3。`store consolidate` は未実行で、owner の manifest 承認待ち。
  - probe 1 回目（Codex デスクトップ、既定のサンドボックス有効）: Codex 内から `%USERPROFILE%\OneStarMaker.Probe\probe.txt` を作ろうとしてアクセス拒否、exit 1。実体側にも LocalCache 側にも file は作られなかった。サンドボックス有効時は workspace 外へ書けない。
  - probe 2 回目（Codex デスクトップ、full access: `approval_policy = "never"`、`sandbox_mode = "danger-full-access"`）: 同じ file の作成に成功。通常の非仮想化 pwsh から `Test-Path` が True、`%LOCALAPPDATA%\Packages\OpenAI.Codex_2p2nqsd0c76g0\LocalCache` 配下に同名 file は無し。`%USERPROFILE%` 直下は Codex デスクトップの書込み仮想化の対象外。
  - 未観測: full access の Codex から `%LOCALAPPDATA%` 配下へ書いたときの振り分け（既存の LocalCache 側記録から仮想化されると推定するが、full access での再観測はしていない）。別 drive 直下（例 `D:\`）の振り分け。package の初回状態（LocalCache が無い状態）の挙動。
  - Codex のサンドボックスは MSIX の仮想化とは別の層で、restricted token と専用ユーザーで動く。full access にすると両方の層のうちサンドボックスだけが外れる。
- A1 で追加した観測（コード読取、2026-10-18。設計の入力であり、A0 の観測を変えない。A2 の指摘で補正した箇所は末尾に記す）:
  - 不変の記録（Evidence の receipt・catalog、transfers の operation 記録）は、保存先の完全修飾 path を bytes のまま持つ。`EvidencePaths.psm1` の `Resolve-EvidenceStoredPath` と `ArtifactPaths.psm1` の `Resolve-ArtifactStoredPath` は、記録の path が「現在の実 root」または「論理 root（LocalAppData から組んだ path）」のどちらかの配下にあるときだけ、I/O の宛先を実 root に付け替える。さらに、記録の path（alias）が解決先と違い **かつ alias が存在する** ときは alias を開いて volumeSerial / fileId を比較し、不一致なら拒否する（`EvidencePaths.psm1:58-61`、`ArtifactPaths.psm1:49`）。移行後にこれらの記録を読むには、旧 root（`%LOCALAPPDATA%\OneStarMaker`）の論理 path を「prefix 置換だけに使う読取専用の別名」として受け入れ、別名側を開かない契約が要る。記録の bytes は書き換えない。
  - 清掃 job の binding manifest（`EvidenceSchedule.psm1` の `Assert-EvidenceStorageBinding`）は、各 role の `logicalPath` が LocalAppData から組んだ path と一致することを要求する。root を移すと既存の manifest は `runtime-binding-invalid` になり、job の再 install が要る（後続スライスが所有する「清掃 job の再登録」と一致する）。job の runtime copy の必須 file 一覧（`EvidenceSchedule.psm1:165`、`EvidenceCleanupJob.ps1:17`）は固定で、新しい module を加えるなら両方に載せる。
  - `StoreConsolidationPlan.psm1` の参照検査（`Get-ConsolidationItemReferences`）は、記録中の完全修飾 path のうち論理 root 配下のものだけを参照として検査する。合流先を新 root にすると、LocalCache 側 copy の記録が持つ旧 root 配下の参照は「store 外の path」として検査から外れ、`reference-missing` の保護が効かなくなる。参照の判定にも旧 root の別名が要る。manifest は `logicalRoot` を 1 値で持ち、承認時と実行時に一致を要求する（`StoreConsolidationPlan.psm1:217,231`、`StoreConsolidation.psm1:339`）ので、2026-10-17 の manifest は root の移設後に `manifest-invalid` になる。
  - `BuildContract.psm1` の `Assert-BuildSourceRoot` は、Build の取り込み元が store の所有領域（`Artifacts`、`Workflow`）やその親でないことを、論理 path と実体 path の両方で拒否する。移行前後のどちらの root も拒否境界に含める必要がある。
  - `StoreConsolidation.psm1` の `Assert-ConsolidationContext` は、合流の process が LocalAppData 非仮想化であることを要求する（source が LocalCache 側 path でも、実 path で読取専用に開く）。この要求は root を移しても変えない。
  - 各 module の test 用 root hook（`$script:TestRoot`、`LocalRootForTest`、`CredentialRootHook`、`BindingRootsHook`、`OwnedRootsForTest`、`DeploymentRoot`、Harness の `-StoreRoot`）は、production の root 導出を丸ごと置き換える形で既存 suite に使われている（Artifacts の tests 21 file、Workflow の tests 3 file。Harness は `-StoreRoot` 引数）。hook を残せば既存 suite の fixture は変わらない。ただし `harness.ps1 -StoreRoot` だけは公開 CLI の引数であり、任意の root を指せる（A2 R1 F1）。
  - `tools/Workflow/tests/StoreContextCliFixture.ps1` は 3 CLI の入口 case で、`WindowsStoreContext.psm1` 以外の module import を `store-boundary-reached` で止める。入口の module 構成を変えると、この fixture の境界も合わせる必要がある（A2 R1 F8）。
  - `workflow-task.ps1 status` も `Enter-WorkflowTaskGuard` を通り、guard は Workflow root・repo・tasks・task の 4 段を作成する（`TaskEventStore.psm1:76-79`）。`status` は「何も作らない」操作ではない（A2 R1 F2）。
  - `harness.ps1` の catch は例外 message を stderr へそのまま出し（`harness.ps1:244-246`）、`current` は `recordPath=<root>\...` を stdout に出す。`workflow-task.ps1` と `artifacts.ps1` は固定 code の allow-list で伏せる（A2 R1 F4）。
  - Unity Runtime の `RevisionProcessLease.Acquire` は `<LocalApplicationData>\OneStarMaker\RevisionLocks\v1` を `Directory.CreateDirectory` で毎回作る（`RevisionProcessLease.cs:47-50`）。したがって旧 root の親 directory `OneStarMaker` は移行後も Unity の実行で再び現れうる。旧 root の残存判定（I3）は親の存在ではなく所有 role 3 つの directory の存在で行い、後続の移行は親 directory と `RevisionLocks` を消さない（A3 主担当の再点検、2026-10-19）。
  - §3 の 15 file のうち root を組むのは 14 file で、`WindowsStoreContext.psm1` は probe の位置（LocalAppData 直下）にだけ使う。`StoreConsolidationPlan.psm1` は comment にしか旧 root を持たない（pure）。
  - 新 root の候補 `<UserProfile>\OneStarMaker` は、旧 root より path が 13 文字短い。`tools/path-budget.json` の予算は repo 相対 path と checkout root の長さに関するもので、store の所在には関わらない。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: store root を AppData 外の固定位置に一元化し、移行前の process も移行後の process も store を再分裂させずに fail-closed に振る舞う実装は、既存の保存契約・資格情報契約・offline suite を保ったまま成立するか。
- 進める最低条件（A1 で不変条件と成立条件に分けた。A0 の候補はすべて観測可能な語に直して採用し、削ったものは無い。I2 は A0 の第 2 候補を「印」と「旧 root 残存」の 2 条件に分け、I6 を追加した。A2 の採用分で I1・I3・I4・I6・S1・S2・S4 の文面を補正した）:
  - 不変条件（起きてはいけないこと）:
    - **I1 旧 root 不変:** このスライスのコードは、旧 root `<LocalApplicationData>\OneStarMaker` 配下のどの entry も作らず、変更せず、削除せず、移動しない。観測: 旧 root 配下の名前・属性・file の size と更新時刻の一覧が、どの CLI 呼出し・module 呼出しの前後でも一致する（`tools/Workflow/tests/StoreContext.Tests.ps1` の `Get-Tree` 方式）。旧 root への I/O は、access を要求しない metadata の照会（存在・属性・実体 path。§4「旧 root の照会」）だけに限り、内容を読まない。`harness.ps1 -StoreRoot` は tests 専用の明示 root で、文字列の prefix だけでなく実体 path でも旧 root 配下・新 root 配下・repo 配下を指す値（junction 経由を含む）を `store-root-unavailable` で拒否する（AC-I1-d、AC-I1-e）。
    - **I2 未移行での拒否:** 新 root `<UserProfile>\OneStarMaker` に移行済みの印（§4「移行済みの印」）が無い間、どの CLI（`artifacts.ps1`、`workflow-task.ps1`、`harness.ps1`）も、どの store module の root 解決（直接呼出し、binding の有無を問わず。`AppDomain` に binding が設定されているだけでは gate を省略しない）も、新 root 配下に directory・file を作らず、印の file 以外の store の role data（`Workflow`、`Harness`、`Artifacts\*` の記録）の内容を読まず（祖先の reparse 検査等、access を要求しない metadata の照会は除く）、reasonCode `store-not-migrated` で失敗する（CLI は `{"status":"failed","reasonCode":"store-not-migrated"}`、exit 1）。例外は清掃 job の実行前の整合性検査だけで、`EvidenceCleanupJob.ps1` と `Initialize-EvidenceRuntimeContext` が runtime copy（`<root>\Artifacts\runtime\<head>\` の `runtime.json` と manifest に列挙された file）を hash 照合のために読む既存の読取専用の検査（hash-first）は gate より前に行ってよい。role data の読取・作成・書込みは gate の通過前に行わない。
    - **I3 旧 root 残存での拒否:** 移行済みの印があっても、旧 root に所有 role の directory（`Artifacts`、`Workflow`、`Harness` のいずれか）がこの process から見えている間、どの CLI も、どの store module の root 解決（直接呼出し、binding の有無を問わず）も、新 root に書かず、role data を開かず（I2 と同じ hash-first の例外だけを認める）、reasonCode `legacy-store-present` で失敗する。旧 root 側の照会がアクセス拒否・列挙失敗・reparse 等で不在を確認できないときは `store-root-unavailable` で失敗する（不在を証明できない状態を不在と扱わない）。process が LocalAppData の仮想化された view を見ていて、そこに LocalCache 側の copy が見える場合も同じ（fail-closed。見えている directory が実体か copy かを判別しない）。検査は `Get-OsmStoreRoot` の中にあり、CLI の入口だけに置かない（§4「経路表」）。
    - **I4 root 導出に環境変数を使わない:** 新 root・旧 root の導出は Known Folder（`UserProfile`、`LocalApplicationData`）からだけ行う。観測: `USERPROFILE`・`LOCALAPPDATA`・`HOME` を別の path に置き換えた子 process で導出結果が変わらない。production の root を CLI 引数で受けない。`harness.ps1 -StoreRoot` は tests 専用の明示 root であり、I1 の拒否規則（prefix と実体 path の両方）に従う。
    - **I5 不適切な root の拒否:** 新 root が repo 配下、`D:\OneDrive\OSM-Artifacts` 配下、UNC、または root までのどの祖先が reparse point でも、印の有無にかかわらず `store-root-unavailable` で失敗する。印の file 自体が reparse point、所有者 SID 不一致、schema 不一致、または handle の実体 path が論理 path と違う場合も同じ。
    - **I6 秘密の非露出:** 鍵、暗号化 store、header、署名 URL を、CLI の引数・stdout・stderr・ログ・Git・テスト結果へ出さない。加えて、このスライスが新設・変更する失敗経路（3 CLI の入口の失敗 JSON、`WindowsStoreRoot.psm1` が throw する例外の message、入口通過後に resolver が失敗したときの `harness.ps1` の stderr）は固定 reasonCode だけを含み、path・package 名・SID を含まない。resolver は内部の .NET I/O 例外をすべて catch し、inner を付けずに固定 code だけを throw する。既存の正常出力（`harness.ps1 current` の `recordPath=` 等）は対象外とし、変えない。
  - 成立条件（起きなければならないこと）:
    - **S1 単一 resolver:** tests 以外で LocalApplicationData から root を組んでいた 14 file（§3 の 15 file から probe 位置の `WindowsStoreContext.psm1` を除く）が、production 経路では `tools/Workflow/WindowsStoreRoot.psm1` の `Get-OsmStoreRoot`（新 root）と `Get-OsmLegacyStoreRoot`（旧 root の別名。prefix の照合にだけ使い、開かない）を経由し、`tools/**/*.ps1`・`tools/**/*.psm1` のうち tests 配下・`WindowsStoreContext.psm1`・`WindowsStoreRoot.psm1` を除いて `LocalApplicationData` の出現が 0 件になる。
    - **S2 同一実体への解決:** 移行済みの印がある root に対し、非仮想化 process と、full access の packaged app 内 process（Codex デスクトップ）の両方で、`[Environment]::GetFolderPath('UserProfile')` の生の値が互いに一致し、`Get-OsmStoreRoot` が返す path（V2 では `BaseForTest` を適用した値で、生の base に一時 directory を足した path。生の値とは等しくない）とその配下に開いた file handle の実体 path（`GetFinalPathNameByHandle`）が、論理 path と一致し、互いに一致する。このスライスの GO が判定するのは host 間の fixture の一致までで、実ストアの移行後の動作は後続スライスの判定（§2「ここでは答えない問い」の smoke test）。
    - **S3 資格情報 root の規則維持:** `CredentialPathAcl.psm1` の `Initialize-CredentialRoot` の allowed base が新 root になり、3 段（`<root>`、`<root>\Artifacts`、`<root>\Artifacts\credentials`）の初期化・ACL 検査（owner と継承無しの 3 主体 FullControl）・reparse 拒否・repo/OneDrive 拒否が、旧 root と同じ規則で通る。DPAPI の scope（CurrentUser）は変えない。
    - **S4 旧記録の読取:** 移行前に書かれた不変の記録が持つ旧 root 配下の完全修飾 path を、`Resolve-EvidenceStoredPath`・`Resolve-ArtifactStoredPath`（binding あり・なしの両経路）・`StoreConsolidationPlan` の参照検査が、prefix 置換だけで新 root の同じ相対位置へ付け替えて扱う。別名は存在確認と identity 照合の対象にせず、旧 root 側を開かない。受け入れる path の種類と動作は §4「記録 path の分類表」に従い、既存の正規化・拒否規則（`..`、境界 prefix の偽装、reparse、ACL、`reference-localcache`）は緩めない。記録の bytes は変わらない。
    - **S5 offline suite の合格:** `tools/Artifacts/tests`、`tools/Workflow/tests`、`tools/Harness/tests` の全 suite が新 root 前提で、各 suite 1 件以上実行・failed 0 で合格し、`pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` が error 0 で通る。
- 受け入れ条件（各条件の検証例。不変条件には失敗経路を含む。時点は「移行前」= 印なし・旧 root に store あり、「移行中」= 印あり・旧 root に所有 role あり、「移行後」= 印あり・旧 root に所有 role なし）:
  - **AC-I1-a（失敗経路、移行前）:** 印なし・旧 root に `Workflow` あり。3 CLI をそれぞれ 1 回呼ぶ。期待: 旧 root の tree 一覧が前後で一致。exit 1、`store-not-migrated`。
  - **AC-I1-b（失敗経路、移行中）:** 印あり・旧 root に `Harness` だけあり。3 CLI を呼ぶ。期待: 旧 root の tree 一覧が前後で一致、新 root 配下は印の file 以外に entry が増えない。exit 1、`legacy-store-present`。
  - **AC-I1-c（成功経路、移行後）:** 印あり・旧 root に所有 role なし（`H2a Review` 等の not-owned directory は残っていてよい）。`workflow-task.ps1 status -Task <id>` を呼ぶ。期待: 旧 root の tree 一覧が前後で一致（not-owned directory は開かれない）。新 root 側は guard が作る `Workflow\<repositoryId>\tasks\<id>` の 4 段と `task.lock` 以外に entry が増えない。
  - **AC-I1-d（失敗経路、`-StoreRoot`）:** 印あり・移行後。`harness.ps1 init ... -StoreRoot <旧 root>\Harness`、同 `-StoreRoot <新 root>\Harness`、同 `-StoreRoot <repo 配下>` を呼ぶ。期待: いずれも `store-root-unavailable`、exit 1、旧 root・新 root の tree 一覧が前後で一致。temp 配下の `-StoreRoot` は従来どおり通る（Harness.Tests.ps1 の既存 case）。
  - **AC-I1-e（失敗経路、`-StoreRoot` の junction）:** temp 配下に junction `<temp>\alias` を作り、target を順に 旧 root の `Harness`、新 root の `Harness`、repo 配下にして `-StoreRoot <temp>\alias` を渡す。期待: いずれも `store-root-unavailable`、旧 root・新 root・repo の tree 一覧が前後で一致。`-StoreRoot` の末尾要素が存在しない場合は、存在する最も近い祖先までを実体 path で検査する（祖先が junction なら拒否）。junction を作れない環境では case を `skipped` と記録し、`IdentityHook` による注入 case で代替する。
  - **AC-I2-a（失敗経路）:** 新 root directory が存在し印の file が無い。module 直接呼出し（`Get-ArtifactRoot`、`Get-EvidenceRoot`、`Get-WorkflowRoot`、`Get-TaskDirectory`、`Get-CredentialRoot`、`Get-BuildRoot`、`Get-ContentRoot`、`Get-ConsolidationLogicalRoot`、`Get-EvidenceBindingRoots`）。期待: すべて `store-not-migrated` で throw し、新 root 配下の tree 一覧が前後で一致。`MarkerIoForTest` の記録で、開いた file が印の path だけであること。
  - **AC-I2-b（失敗経路）:** 新 root directory 自体が無い。期待: `store-not-migrated`、新 root が作られない（`<UserProfile>` 直下の一覧が前後で一致）。
  - **AC-I2-c（合格例・不合格例、終了境界）:** 印の file が存在するが 0 byte（書込み途中）。合格例: `store-root-unavailable` で失敗し何も作らない。不合格例: 印ありと扱って root を返す。
  - **AC-I2-d（失敗経路、binding 経路）:** binding manifest（`BindingRootsHook` 経由の fixture）で `Assert-EvidenceStorageBinding` を呼び、印なし。期待: `store-not-migrated`（binding 検証が resolver を介して失敗する）。
  - **AC-I2-e（失敗経路、binding 済みの root getter の直接呼出し）:** 子 process（既存 suite の `AppDomain` を汚さない）で、妥当な binding を `AppDomain` に直接設定し（`Initialize-EvidenceRuntimeContext` を通さない）、印なしの状態で `Get-EvidenceRoot`、`Get-ArtifactRoot`、`Get-EvidenceDeploymentRoot`、`Get-EvidenceRuntimeRoot`、`Get-WorkflowRoot` を直接呼ぶ。期待: すべて `store-not-migrated` で throw し、binding の `physicalPath` を返さず、新 root の tree 一覧が前後で一致。
  - **AC-I3-a（失敗経路、CLI）:** 印あり・旧 root に `Artifacts` あり。期待: 3 CLI が `legacy-store-present`、新 root 配下に entry が増えない。
  - **AC-I3-e（失敗経路、CLI を通らない writer。単体）:** 印あり・旧 root に `Workflow` あり。(1) module 直接呼出し（AC-I2-a と同じ関数群）、(2) binding あり経路（`BindingRootsHook` の fixture で `Assert-EvidenceStorageBinding`）、(3) `Initialize-EvidenceRuntimeContext` を temp の runtime と binding manifest の fixture で直接呼ぶ単体 case（Task Scheduler への実登録はしない）。期待: すべて `legacy-store-present` で throw し、新 root の tree 一覧が前後で一致、`MarkerIoForTest` の記録で新 root 側の store role data に開いた file が無いこと（(3) では runtime copy の hash 照合の読取は許される。I2 の例外）。旧 root に所有 role が無い fixture では同じ呼出しが通常処理へ到達する。
  - **AC-I3-g（失敗経路、binding 済みの root getter の直接呼出し）:** AC-I2-e と同じ子 process の fixture で、印あり・旧 root に `Workflow` あり。期待: 5 つの getter がすべて `legacy-store-present`。印あり・旧 root に所有 role なしでは、binding の `physicalPath` の identity / ACL 検証（既存）へ進んで返す。
  - **AC-I3-h（失敗経路、実 entry script）:** `EvidenceSchedule.Tests.ps1` の既存 runtime fixture（temp に runtime copy と `runtime.json` を作る）を使い、印あり・旧 root に `Workflow` ありの状態で `pwsh tools/Artifacts/EvidenceCleanupJob.ps1 -Runtime <temp runtime> -ManifestSha256 <hash>` を子 process で起動する。期待: stdout は既存の外側の変換どおり `{"status":"failed","reasonCode":"scheduler-runtime-unavailable"}`・exit 1（CLI の reasonCode と同一であることは求めない）。runtime copy 内の file 以外に読まれた file が無く（fixture の role data directory を `FileShare.None` で握った別 handle、または tree 一覧で観測）、新 root の tree 一覧が前後で一致。この case が環境要因で書けない場合は `未検証` と記録し、観測地点（`Initialize-EvidenceRuntimeContext` の直後）と追加 fixture を B の実装結果に書く。
  - **AC-I3-f（旧 root の照会の 6 case）:** 旧 root の所有 role 名に対し、(1) directory が存在 → `legacy-store-present`、(2) 不在（親の `OneStarMaker` 自体の不在を含む）→ 続行、(3) 同名の通常 file → 続行（AC-I3-d）、(4) junction / reparse point → `store-root-unavailable`、(5) アクセス拒否（ACL で現在ユーザーの読取を拒否した fixture、または seam で `ERROR_ACCESS_DENIED` を注入）→ `store-root-unavailable`、(6) 照会 API の失敗（seam で未知の Win32 error を注入）→ `store-root-unavailable`。どの case でも旧 root の tree 一覧が前後で一致し、file の内容を開かない。
  - **AC-I3-b（合格例、続行の観測）:** 印あり・旧 root に所有 role なし。CLI fixture で 3 入口を呼ぶ。期待: fixture の記録に `store:import:` が現れる（入口の検査を通過して store module の import に達したことを「続行」と定義する）。
  - **AC-I3-c（合格例・不合格例、優先関係）:** 印なし・旧 root に `Workflow` あり。合格例: `store-not-migrated`（印の検査が先）。不合格例: `legacy-store-present`。
  - **AC-I3-d（合格例、名前の境界）:** 旧 root に `Workflow` という名の file（directory でない）と `H2a Review` directory だけがある。期待: 所有 role の directory 無しとして扱い、`legacy-store-present` にならない。
  - **AC-I4-a（失敗経路）:** `USERPROFILE` と `LOCALAPPDATA` を temp 配下に差し替えた子 pwsh で `Get-OsmStoreRoot`・`Get-OsmLegacyStoreRoot` の導出 base を観測。期待: Known Folder の値のまま（test hook 無しの導出結果が環境変数に追従しない）。
  - **AC-I5-a（失敗経路）:** test hook で base を repo 配下、`D:\OneDrive\OSM-Artifacts` 配下、UNC（`\\server\share`）、祖先に junction を含む path にする。期待: 印の有無にかかわらず `store-root-unavailable`。
  - **AC-I5-b（失敗経路）:** 印の `ownerSid` が別 SID、`schemaVersion` が 2、`kind` が別文字列、JSON でない、4 KiB 超。期待: `store-root-unavailable`。
  - **AC-I5-c（失敗経路）:** 印の file の handle の実体 path が論理 path と違う（`MarkerIoForTest` で差し替え）。期待: `store-root-unavailable`。
  - **AC-I6-a（失敗経路）:** AC-I2〜I5 の各失敗出力（3 CLI の stdout JSON、`harness.ps1` の stderr、module 直接呼出しの例外 message）に、base path・package 名・SID・`OneStarMaker` 以外の path 要素が含まれない。`MarkerIoForTest` で open に完全 path 入りの .NET 例外を投げさせ、resolver の例外 message が固定 code だけであること。
  - **AC-S1-a（機械検査）:** `git grep -n LocalApplicationData -- 'tools/**/*.ps1' 'tools/**/*.psm1'` の結果から `tools/*/tests/`・`tools/Workflow/WindowsStoreContext.psm1`・`tools/Workflow/WindowsStoreRoot.psm1` を除いた件数が 0。判定 C が実行する。
  - **AC-S1-b:** 14 file の各 root 関数を、module の `TestRoot` 等を未設定のまま、resolver の `BaseForTest` / `LegacyBaseForTest` hook だけを設定して呼ぶ。期待: 返る path が hook した base 配下の `OneStarMaker\<role>` になる（`tools/Artifacts/tests/StoreRootWiring.Tests.ps1`、`Harness.Tests.ps1`、`TaskLifecycle.Tests.ps1` に 1 case ずつ）。
  - **AC-S2-a（操作・実行時。§5 検証経路 V2）:** `<UserProfile>` 直下に観測担当が作った一時 base と fixture の印を使い、Codex（full access）内と非仮想化 pwsh の両方で fixture script を実行。期待: 両者の `GetFolderPath('UserProfile')` の生の値が互いに一致し、`Get-OsmStoreRoot` の出力（`BaseForTest` 適用後の path）と書いた file の handle の実体 path が、論理 path と一致し互いに一致する。生の値と `Get-OsmStoreRoot` の出力は等しくない（後者は一時 directory を含む）。Codex 側が書いた file が非仮想化側の一覧に現れ、`<LocalApplicationData>\Packages\*\LocalCache` 配下に同名が無い。
  - **AC-S2-b（操作・実行時。§5 検証経路 V1）:** 移行前の実状態（印なし）で、Codex（full access）内と非仮想化 pwsh の両方から `workflow-task.ps1 status` を呼ぶ。期待: 両方 `store-not-migrated`・exit 1、`<UserProfile>\OneStarMaker` が前後で存在しない、旧 root の tree 一覧が前後で一致。
  - **AC-S3-a:** `Credentials.Tests.ps1` の既存 case（初期化、ACL 不一致拒否、reparse 拒否、repo/OneDrive 拒否、WriteLedger の境界）が、allowed base を resolver の base に差し替えた状態で全件合格。追加 case: `Initialize-CredentialRoot` が `<root>` の 1 段目に `Assert-SharedParent`（存在確認のみ、ACL 変更なし）を適用し、`<root>` が無ければ作らず `Credential operation unavailable.` で失敗（印があっても新 root を作らない）。
  - **AC-S4-a:** 新 root を base hook、旧 root を legacy hook にし、旧 root 配下の完全修飾 path を持つ receipt から `Resolve-EvidenceStoredPath` を呼ぶ。binding なしと、binding あり（`BindingRootsHook` の fixture）の 2 case。期待: 新 root の同じ相対位置へ解決し、旧 root 側は `Directory.Exists` も `File.Exists` も `Get-WindowsStorePathIdentity` も呼ばれない（legacy hook の base に実 directory を置き、その tree と `IdentityHook` の記録で観測）。`Resolve-ArtifactStoredPath` も同じ。
  - **AC-S4-b:** `StoreConsolidationPlan.Tests.ps1` の in-memory fixture で、記録の参照が旧 root 配下を指す item。期待: 新 root の相対 key に写像されて参照検査され、参照先が無ければ `reference-missing`。LocalCache 配下の参照は従来どおり `reference-localcache`。
  - **AC-S4-c（合格例・不合格例）:** 参照が旧 root でも新 root でもない path（worktree の source）。合格例: 参照として扱わず記録しない（従来どおり）。不合格例: 旧 root の別名処理の副作用で `reference-missing` になる。
  - **AC-S4-d:** 承認 manifest の `legacyRoots` が現在の別名と違う。期待: `manifest-invalid`（§4「合流の manifest」）。
  - **AC-S4-e（分類表の組合せ）:** §4「記録 path の分類表」の各行について、binding あり・なしの両経路で、(1) 別名側に同名 file が残っている（それでも別名に触れない）、(2) 新 root 側の対象が不在（`evidence-path-missing` / `AllowMissing` の既存規則）、(3) 別の role を指す別名 path（拒否）、(4) prefix だけ似た sibling（`<LocalAppData>\OneStarMaker2\...`。別名として扱わない）、(5) LocalCache 配下の path、(6) `..` を含む path（既存の拒否）。期待: 表の動作どおり。拒否の reasonCode は API ごとの既存のもの: `Resolve-EvidenceStoredPath` は `invalid-evidence-path`、`Resolve-ArtifactStoredPath` は `Artifact path unavailable.`、`StoreConsolidationPlan` の参照検査は (5) を `reference-localcache`、(3)・(4) を非参照または `reference-out-of-role` の既存分類。共通なのは「旧 root の別名を新 root へ誤写像しない」ことだけで、reasonCode を統一しない。旧 root 側は tree 一覧と `IdentityHook` の記録で触れていないこと。
  - **AC-S5-a:** §5「判定必須テスト」の全 suite を `-ResultPath` 付きで実行し、各 JSON の executed が 1 件以上・failed が 0 件。
- ここでは答えない問いと所有する後続スライス:
  - 実ストアの新 root への移行の実行（同一 volume の move で ACL を保つか、別 drive への複写で ACL を再設定するか、移行済みの印の書込み）: 後続スライス「store root 移設の実行と合流」（`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §6 に入口条件付きで置く。入口条件は O1 の決定どおり、このスライスの GO 候補 head で merge の直前。移行の実行に使う CLI はその head に固定する）。印の書込み関数はこのスライスに含めない（§4「決定 D4」）。同スライスの入口条件に次を加える: 印を書く前に旧 root の所有 role が空であることを列挙で確認する（A2 R2）、2026-10-17 の `store plan` manifest は `manifest-invalid` になるので再 plan する（A2 R1 F8）、移行の実行後は旧コード（develop の checkout、旧 runtime copy の清掃 job）を動かさず、後続スライス自身とこのスライスの Phase D の Workflow / Evidence の記録は GO 候補 head の CLI で行う（A2 R1 E1・E2）、移行プログラム（producer）はこのスライスの resolver を読取に使ってよいが、印の書込みと旧 root の move / copy は producer が持ち、`store consolidate` を旧 root の実体から新 root への移設に使わない（I3 により旧 root が残る間は resolver が止まるため。A3 R3 F2）。移行後の smoke test（後続スライスの判定。seam を使わない production の `Get-OsmStoreRoot` と 3 CLI で、root と file handle の実体 path の一致、Evidence binding の再生成と検証、`workflow-task.ps1 status` の読戻し、`harness.ps1 current` の読取、旧 root の所有 role 不在、旧 runtime job の停止状態を確認する。packaged app 側は LocalCache の残置がある間は `legacy-store-present` を期待し、成功とは記録しない。A3 R3 F5）。
  - LocalCache 側 copy の合流先を新 root にすること、manifest 承認、`store consolidate` の実行: 同じ後続スライス。
  - Task Scheduler の Evidence 清掃 job の root 変更と再登録: 同じ後続スライス。旧 runtime copy の job は旧 root に書き続けるので、移行前に停止することも同スライスの手順に含める（A2 R1 E2）。
  - Codex 側残置（LocalCache 側 copy）の後始末: program §6 の既存項目のまま。I3 により、LocalCache 側の copy が見える packaged app 内 process は残置が消えるまで `legacy-store-present` で止まるので、「残置の削除後に packaged app を Phase 担当にする」を同項目の入口に加える（A2 R1 F3）。
  - 旧 root 別名（S4）の廃止期限と条件: program §6「分裂したストア」に入力として置く（A2 R2）。このスライスでは別名を期限なしで持つ。
  - H1 適用 task（`h2c-unity-gate` 等）の CURRENT・receipt が持つ path bytes と新 root の両立: 後続スライス「store root 移設の実行と合流」の合流後確認に送る（A2 R2 リスク 4）。このスライスは Harness の root 所在だけを変える。
  - Codex を Phase 担当として運用する際の mode 固定（full access）と host 条件の文書化: このスライスの README 更新に含める（§4「決定 D7」。製品名に依存しない語で書く）。
- 判定定義: GO は進める最低条件（I1〜I6、S1〜S5）の全件成立。NO-GO はいずれかの不成立。CONDITIONAL ACCEPT は定義しない。GO・C' の判定は実ストアの移行（後続スライス）の成功を含まず、先取りしない。移行の実行は GO 候補 head の確定後、owner の承認で後続スライスが行う（§2 O1 の工程表）。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約（owner 決定、2026-10-18）:
  - Codex デスクトップを Claude Code と並行する Phase 担当に加える。再開条件は、Cloud 取得経路の RC3 と同じ「担当として割り当てた」事実を採用する。
  - 方針は store root の移設であり、仮想化された場所の中で動かす経路は作らない。
  - Codex 独自のサンドボックスは使わない。実行主体は同一 Windows ユーザー。
  - 資格情報の保存方式・ACL 規則・Known Folder から導く原則は変えない。
  - AGENTS.md の常時契約（`#nullable enable` は Unity 側のみ、path 予算、Artifact Storage の事前承認の範囲、`Task.Delay` / `Thread.Sleep` の禁止）を守る。
- A1 で決めた事項（A0 の未決事項への回答。根拠は §4「決定」。A2 で補正したものは注記）:
  - 新 root の導出: 候補 (a) `UserProfile` Known Folder 直下の `OneStarMaker`。(b) 別 drive は未観測で volume の存在に依存し、(c) 設定 file は所在と ACL の問いを増やす。R2 も同じ結論。
  - 仮想化判定（probe）: 3 CLI の入口から外し、`store plan` / `consolidate` の非仮想化要求にだけ残す（A2 R1 F3 で補正。A1 初稿は「probe の結果で旧 root 残存検査の要否を選ぶ」だった）。新 root には書込み probe を置かず、印の file handle の実体 path 照合で読取の仮想化・reparse・UNC を検出し、書込みの実体は V2 で実機観測する。
  - 移行済みの印: 新 root 直下の固定名 JSON file。旧 root 残存の検出は所有 role directory の存在（process の見え方を問わない）。
  - 資格情報 root の base: resolver が返す新 root を allowed base にし、3 段の規則は不変。
  - `StoreConsolidation` / `StoreConsolidationPlan`: 合流先は新 root。旧 root は参照検査の別名として plan に渡し、manifest に `legacyRoots` として記録する。
  - H1（`external-current-v1`）: 不採用。理由は §5「機械検査・H1」。R2 も不採用。
  - 全 EditMode 回帰: 適用除外。理由と代替証拠は §5。R2 も妥当と判断。
- A3 で owner が決めた事項（2026-10-18。2026-10-19 に revision 1 として凍結）:
  - **O1 merge 後の運用空白:** owner の決定は (α) の設計のまま、順序を (γ) にする。帰結: I2 の文面は変えない。後続スライス「store root 移設の実行と合流」の入口条件は「このスライスの GO 候補 head で、merge の直前」。移行の実行後から merge までの間は、develop の checkout と旧 runtime copy の清掃 job を動かさない（動かすと旧 root を再作成し、新コードが `legacy-store-present` で止まる）。このスライスの Phase D（Evidence の保存・引渡し確認、task の終了）は移行後に GO 候補 head の CLI で行い、Phase C / C' までは D6 のとおり merge 前の develop CLI で記録する。工程・責務・停止規則は次の表で凍結し、不可逆な実行は後続スライスと owner 承認に残す（R3 F2）。
    - 登場する actor と版は 4 つで、混同しない: (A) merge 前の develop の CLI（旧コード。非仮想化）、(B) 旧 runtime copy で動く清掃 job（旧コード。Task Scheduler）、(C) このスライスの GO 候補 head の CLI（新コード。consumer。印を書かない）、(D) 後続スライスの移行プログラム（producer。印を書く唯一の actor。別 branch で A/B/C を経る）。
    | 手順 | 実行担当 | 使う版 | 入口の状態 | 失敗時の停止・再開 |
    |---|---|---|---|---|
    | 1. 判定 C・C' を完了し GO 候補 head（SHA A）を固定 | Phase C / C' | 記録は (A) | 印なし・旧 root 稼働中 | NO-GO なら通常の差し戻し。移行に進まない |
    | 2. (D) を後続スライスとして設計・実装・レビューし、版（SHA B）を固定 | 後続スライスの Phase A〜C' | (D) は (C) の resolver を読取に使ってよいが、印の書込みと旧 root の move / copy は (D) が持つ | 同上 | (D) が未完なら本スライスは merge せず待つ。(A) の運用を続ける |
    | 3. 旧 writer の静止: (A) の新規実行を禁止し、(B) の job を無効化し、実行中 job の終了を確認 | owner（手順は (D) の runbook） | — | 同上 | 終了を確認できなければ移行に進まない |
    | 4. 旧 root の最終 inventory（名前・属性・size・更新時刻）と差分保全 | (D) | SHA B | 同上 | inventory が取れなければ停止 |
    | 5. 所有 role（`Artifacts`、`Workflow`、`Harness`）の移設（同一 volume の move、または複写 + ACL 再設定）と、ACL・内容・数量の照合 | (D)、承認は owner | SHA B | 移行中（印なし） | 照合不一致なら停止。旧 root を削除せず、印を書かない。(A) を再開しない（再開すると旧 root が再び writer になる）。回復の入口は (D) の runbook |
    | 6. 旧 root の所有 role 不在の確認 | (D) | SHA B | 同上 | 残っていれば 5 へ戻る |
    | 7. 印の原子的発行（一時名 → rename） | (D) | SHA B | 移行後 | 発行失敗なら印を残さず停止 |
    | 8. 新 head の smoke test（§2「ここでは答えない問い」の smoke 項目。seam 無しの production resolver と 3 CLI） | (D) の動作確認担当、観測は非仮想化側 | (C) = SHA A | 移行後 | 不一致なら印を消さず、(A)・(B) を再開せず、owner に返す |
    | 9. `develop` への merge | owner | — | 移行後 | merge が通らない・延期なら (C) の worktree を唯一の CLI として運用を続け、(A) を再開しない。印の削除・旧 root の復元・自動 rollback はしない |
    | 10. 本スライスの Phase D（Evidence の保存・引渡し確認、task 終了、harvest、HANDOFF 削除） | Phase D 担当 | (C) または merge 後の develop | 移行後 | (C) の CLI で Phase D を書けなければ、それは本スライスの欠陥として記録し、owner に返す |
    - 検討した候補は次のとおり。本案（I2）では、新コードが develop に入ってから後続スライスが印を書くまでの間、develop の 3 CLI はすべて `store-not-migrated` で失敗し、Evidence の stage / fetch、Workflow の start / end、Harness の current、`credentials`、Build / Content の fetch、`schedule-status` が使えない。候補:
    - (α) そのまま受け入れ、後続スライスを merge 直後に実行する。fail-closed が単純で、新コードが旧 root に書く経路を持たない。ただし旧 head の checkout と旧 runtime copy の清掃 job は α/β に関係なく旧 root に書き続けるので、「root が 2 つ生きない」のは新コードの process についてだけ（A2 R1 E2）。後続スライス自身の start / Evidence stage を develop CLI で記録できないため、記録方法を後続の入口条件に書く（A2 R1 E1）。
    - (β) 印が無い間は旧 root で従来どおり動く「移行前 mode」を持ち、印が現れた後だけ旧 root を拒否する。空白は無くなるが、新コードが旧 root に書く経路が増え、I2 の「拒否する」が「新 root に作らない・開かない」に弱まる。移行前 mode では LocalAppData の probe を 3 CLI の入口に残し、`virtualized` を従来どおり拒否しないと LocalCache の分裂が再発する（A2 R1 E3）。README に mode の見分け方が要る。選ぶ場合は Phase A revision 2 として I2・I3・§3・§4・§5 を改稿する。
    - (γ) 後続スライスの移行手順を、このスライスの GO 候補 head（merge 前）で先に実行して印を書き、その後 develop へ merge する。空白は無く、(β) の経路も要らない。後続スライスの入口条件を「merge 直後」から「GO 候補 head で、merge 直前」に反転し、移行の実行に使う CLI の head を固定する（A2 R1 E5）。
    - A1・A2 の推奨は (α) の設計 + (γ) の順序で、owner がこれを採った。
  - **O2 印の形式:** owner は A3 案どおりとした。§4「移行済みの印」の `store-root.json` と 4 field（`schemaVersion`、`kind`、`ownerSid`、`createdAt`）。書く側は同じ directory の一時名に書いて flush し、rename で現す（部分状態を見せない。A2 R1 F9）。後続スライスがこの形式で書く。
  - **O3 保留 2 件の扱い（§5「A2 独立レビュー」の保留）:** owner は A3 案どおりとした。(1) 旧 root 別名の廃止期限は program §6 に入力として置くだけにし、このスライスでは期限を持たない。(2) I1 の観測は名前・属性・size・更新時刻（既存 `Get-Tree` 方式）で、hash は含めない。

## 3. 責務マップ

責務は独立して変わる理由で数える。行数は `wc -l`（2026-10-18、base `abb99775`）。増分は見込み。

### 新規

| file | 責務 | 変更理由 | 所有者・寿命 | 依存（入力 → 出力） | 公開面 | テスト境界 | 配置理由 | 行数 |
|---|---|---|---|---|---|---|---|---|
| `tools/Workflow/WindowsStoreRoot.psm1`（新規） | store root の所在を決め、移行済みの印と旧 root の所有 role の残存を検査し、旧 root の別名を返し、tests 専用の明示 root を検査する OS 基盤層の resolver。directory も file も作らず、ACL を変えない。 | 「store がどこにあるか」「移行済みか」「旧 root が残っていないか」の判断。role の内訳や ACL 規則は持たない。所有 role の最上位名 3 つ（`Artifacts`、`Workflow`、`Harness`）だけを残存検査のために持つ。 | 状態を持たない。process 寿命。 | `[Environment]::GetFolderPath`、`WindowsStorePaths.psm1`（実体 path 照合）、自身の native helper（旧 root の照会。zero-access の `CreateFileW` で存在・不在・error を区別）→ 文字列の root、または固定 reasonCode の throw。`WindowsStoreContext.psm1`（probe）と domain module（Artifacts / Workflow の TaskEventStore / Harness）を import しない。 | `Get-OsmStoreRoot`、`Get-OsmLegacyStoreRoot`、`Assert-OsmExplicitStoreRoot`。private seam: `$script:BaseForTest`、`$script:LegacyBaseForTest`、`$script:MarkerIoForTest`（印の open / 実体 path 照合の差し替えと記録）、`$script:LegacyQueryForTest`（旧 root の照会の差し替え。Win32 error の注入）。CLI・環境変数からは設定できない。 | temp directory を base にした native 実行と、seam による失敗注入。Unity 不要。 | `WindowsStorePaths` / `WindowsStoreContext` と同じ OS 基盤層。Artifacts も Harness も既にこの層を import しており、依存方向（domain → OS 基盤）を保てる。probe と別 file にするのは変更理由が違うため（probe: 「この process の AppData 書込みは仮想化されているか」、resolver: 「store はどこで、移行済みか」）。 | 新規 約 170 行 |
| `tools/Workflow/tests/StoreRoot.Tests.ps1`（新規） | resolver の全失敗経路と成功経路、I1〜I6 の module 側の検証、旧 root の照会の 6 case、明示 root の junction case。 | resolver の契約。 | テスト。 | resolver、temp directory、junction。 | なし。 | 本体。 | resolver と同じ domain。 | 新規 約 400 行 |
| `tools/Workflow/tests/StoreRootHostFixture.ps1`（新規） | 検証経路 V2 の host 側 fixture。`-Base` の一時 base で resolver を通して file を 1 つ書き、`GetFolderPath('UserProfile')` の生の値、論理 path、実体 path、`Get-OsmStoreContext` の生結果を JSON で出す。 | 操作・実行時の検証を機械的に再現するため。 | テスト。 | resolver（`BaseForTest` seam）、`WindowsStoreContext.psm1`（観測のみ）。 | なし（tests 配下、CLI ではない）。 | 本体。 | V2 の手順を担当が同じ script で実行できるようにする。 | 新規 約 70 行 |
| `tools/Artifacts/tests/StoreRootWiring.Tests.ps1`（新規） | Artifacts 側 11 module の root 関数が resolver を経由することの配線検査（AC-S1-b）。 | S1 の検証。 | テスト。 | 各 module、resolver の seam。 | なし。 | 本体。 | 検査対象が Artifacts に集中するため。Workflow / Harness 側は既存 suite へ 1 case ずつ追加。 | 新規 約 120 行 |

### 変更（既存 15 file と周辺）

| file | 現在の root の組み方 | 変更後 | 変更理由・配置理由 | 現在行数 → 増分 |
|---|---|---|---|---|
| `tools/Artifacts/ArtifactPaths.psm1` | transfers の論理 root と shared 親（`<LocalAppData>\OneStarMaker`）を 3 箇所で組む。`Get-ArtifactRoot` は binding があれば binding の `physicalPath` を返す | `Get-ArtifactRoot` は binding の有無にかかわらず最初に `Get-OsmStoreRoot` を呼び（gate）、その後に binding の identity / ACL 検証、または `Get-OsmStoreRoot` + `Artifacts\transfers`。`Resolve-ArtifactStoredPath` の論理 prefix に `Get-OsmLegacyStoreRoot` + `Artifacts\transfers` を prefix 置換専用の別名として追加し、別名に対する存在確認・identity 照合（現行 49 行目）は行わない。`New-ArtifactOperation` の shared 親は `<root>`（存在を要求、作らない）。 | S1、S4。責務（operation directory の作成と所有権）は不変。 | 118 → +10 |
| `tools/Artifacts/BuildContract.psm1` | `Assert-BuildSourceRoot` の所有領域境界 | 境界に新 root と旧 root の両方の `Artifacts` / `Workflow` を含める。 | 取り込み元が store のどちらの root でも拒否。 | 201 → +4 |
| `tools/Artifacts/BuildStore.psm1` | `builds-v1` の root | `Get-OsmStoreRoot` + `Artifacts\builds-v1`。shared 親の存在要求は不変。 | S1。 | 310 → +2 |
| `tools/Artifacts/ContentStore.psm1` | `contents-v1` の root | 同上。 | S1。 | 115 → +2 |
| `tools/Artifacts/Credentials/CredentialPathAcl.psm1` | `Get-CredentialRoot` と `Initialize-CredentialRoot` の base を LocalAppData から組む | base を `Get-OsmStoreRoot` の親（`<UserProfile>`）にし、allowed 3 段は `<root>`、`<root>\Artifacts`、`<root>\Artifacts\credentials`。`<root>` の段は `Assert-SharedParent`（存在確認のみ）。test 用の `TestBase` 経路は不変。 | S3。ACL 規則・DPAPI・WriteLedger の境界は変えない。 | 166 → +6 |
| `tools/Artifacts/EvidenceContract.psm1` | deployments の論理 root。`Get-EvidenceDeploymentRoot` は binding があれば binding の `physicalPath` を返す | binding の有無にかかわらず最初に `Get-OsmStoreRoot` を呼び（gate）、その後に binding の検証、または `Get-OsmStoreRoot` + `Artifacts\deployments`。 | S1、I2、I3。 | 89 → +3 |
| `tools/Artifacts/EvidencePaths.psm1` | evidence-v2 の論理 root を 2 箇所で組む。`Get-EvidenceRoot` は binding があれば binding の `physicalPath` を返す | `Get-EvidenceRoot` は binding の有無にかかわらず最初に `Get-OsmStoreRoot` を呼び（gate）、その後に binding の検証、または `Get-OsmStoreRoot` + `Artifacts\evidence-v2`。`Resolve-EvidenceStoredPath` の論理 prefix に旧 root の別名を追加（binding あり経路では binding の `logicalPath` に加えて別名を受ける）。別名は prefix 置換専用で、現行 58-61 行目の alias identity 照合の対象にしない。 | S1、S4。 | 100 → +10 |
| `tools/Artifacts/EvidenceReset.psm1` | reset の対象 root | `Get-OsmStoreRoot` + `Artifacts`。 | S1。reset は後続スライスの運用で使うが、root の所在だけ変える。 | 235 → +2 |
| `tools/Artifacts/EvidenceSchedule.psm1` | runtime root、binding roots、credential root、runtime copy の必須 file 一覧。`Get-EvidenceRuntimeRoot` は binding があれば binding の `physicalPath` を返す | 3 箇所を resolver 経由にし、`Get-EvidenceRuntimeRoot` は binding の有無にかかわらず最初に `Get-OsmStoreRoot` を呼ぶ（gate）。`Assert-EvidenceStorageBinding` の suffix 検査（`\OneStarMaker\Workflow` 等）は不変。必須 file 一覧（165 行目）に `tools/Workflow/WindowsStoreRoot.psm1` を加える。`Initialize-EvidenceRuntimeContext` の runtime copy の hash 照合（読取専用）は gate より前のまま（I2 の例外）。 | S1、I2、I3。既存 manifest の `logicalPath` 不一致は後続の再 install が解く。 | 422 → +10 |
| `tools/Artifacts/EvidenceCleanupJob.ps1` | root を組まない（binding manifest から読む）。必須 file 一覧（17 行目） | 必須 file 一覧に `tools/Workflow/WindowsStoreRoot.psm1` を加える。root の解決は `EvidenceSchedule.psm1` 経由のまま。 | job の runtime copy に resolver を含める。 | +1 |
| `tools/Artifacts/SeriesRetirementStore.psm1` | `retirements-v1` の root | `Get-OsmStoreRoot` + `Artifacts\retirements-v1`。 | S1。 | 105 → +2 |
| `tools/Artifacts/StoreConsolidation.psm1` | 合流先の実ストア root | `Get-ConsolidationLogicalRoot` を resolver 経由にし、inventory と manifest に `legacyRoots`（`Get-OsmLegacyStoreRoot` の 1 件）を持たせ、承認時と実行時に一致を要求する。`TopLevel` 表に印の file 名を `marker`（開かない）として登録。`Assert-ConsolidationContext`（非仮想化要求）は不変。 | S1、S4。 | 397 → +10 |
| `tools/Artifacts/StoreConsolidationPlan.psm1` | `Get-ConsolidationItemReferences` が `$LogicalRoot` 1 つで参照を判定 | `$Inventory.legacyRoots` も受け、新 root か別名のどれかの配下なら同じ相対 key に写像。`Test-ConsolidationManifest` が `legacyRoots` の一致も要求。pure のまま。 | S4。 | 248 → +8 |
| `tools/Harness/RecordStore.psm1` | `Get-TaskDirectory` の既定 StoreRoot | 既定を `Get-OsmStoreRoot` + `Harness`。`-StoreRoot` が指定されたときは `Assert-OsmExplicitStoreRoot`（prefix と実体 path・reparse の拒否）を通してから使う。`../Workflow/WindowsStoreRoot.psm1` を import（Harness → Workflow OS 基盤層。既に `harness.ps1` が同層を import している）。 | S1、I1、I4。 | 397 → +8 |
| `tools/harness.ps1` | `status` の一覧 root と入口の probe | 入口の probe を、`-StoreRoot` 指定時は `Assert-OsmExplicitStoreRoot`、未指定時は `Get-OsmStoreRoot`（印・残存検査）に置き換える。`status` の root を `Get-TaskDirectory` と同じ導出にする。catch の stderr 出力は変えない（resolver が固定 code しか throw しないことで I6 を満たす）。 | S1、I1、I2、I3。 | 247 → +4 |
| `tools/Workflow/TaskEventStore.psm1` | Workflow の root。`Get-WorkflowRoot` は binding があれば binding の `physicalPath` を返す | binding の有無にかかわらず最初に `Get-OsmStoreRoot` を呼び（gate）、その後に binding の検証、または `Get-OsmStoreRoot` + `Workflow`。 | S1、I2、I3。 | 241 → +3 |
| `tools/Workflow/WindowsStoreContext.psm1` | probe の位置（LocalAppData 直下）。3 CLI の入口と `StoreConsolidation` が使う | コードは不変。冒頭コメントを「`store plan` / `consolidate` の非仮想化要求にだけ使う。3 CLI の入口では使わない」に改める。 | 意味の変更を文書化。 | 107 → +4（コメント） |
| `tools/artifacts.ps1`、`tools/workflow-task.ps1` | 入口の probe と `virtualized-store` / `store-context-unknown` の出力 | 入口の probe を `Get-OsmStoreRoot` の呼出しに置き換える（store module の import より前。結果は捨て、例外だけを失敗 JSON にする）。失敗出力の形は不変で、reasonCode に `store-not-migrated`、`legacy-store-present`、`store-root-unavailable` が入る。`virtualized-store` と `store-context-unknown` は 3 CLI の入口からは出なくなる（`store plan` / `consolidate` の code としては残る）。 | I2、I3。 | 195 / 76 → +2 / +2 |
| `tools/Workflow/tests/StoreContextCliFixture.ps1` | 入口 case の fixture。`WindowsStoreContext.psm1` 以外の import を `store-boundary-reached` で止める | 境界を「`WindowsStoreRoot.psm1` と `WindowsStorePaths.psm1` は本物を読み、`MarkerIoForTest`・`LegacyQueryForTest`・`BaseForTest`・`LegacyBaseForTest` を fixture の値に差し替える。それ以外の import は従来どおり止める」に改める。`-Mode` を印・旧 root の状態（印なし / 印あり+旧 root あり / 印あり+旧 root なし / 印不正）に置き換える。 | I2、I3 の CLI 境界。 | 32 → +20 |
| `tools/Workflow/tests/StoreContext.Tests.ps1` | probe の単体 case と、CLI 3 入口が `virtualized` で拒否する case | probe の単体 case（`decision-*`、`native-*`）は不変（`StoreConsolidation` が使う）。CLI case を新しい意味に改める: AC-I1-a〜c、AC-I3-a〜c、`-StoreRoot` の AC-I1-d。判定が store module import より前に行われること、import に副作用が無いことの case は不変。 | I2、I3。 | 395 → +50 |
| 既存 suite（`Credentials.Tests.ps1`、`ArtifactEvidence.Tests.ps1`、`ArtifactTransfer.Tests.ps1`、`StoreConsolidation.Tests.ps1`、`StoreConsolidationPlan.Tests.ps1`、`EvidenceSchedule.Tests.ps1`、`BuildCollect.Tests.ps1`、`TaskLifecycle.Tests.ps1`、`Harness.Tests.ps1`） | module の `TestRoot` 等で root を差し替える。`EvidenceSchedule.Tests.ps1` の runtime fixture は runtime copy の file 一覧（43 行目）を持つ | hook は残す。AC-S3-a、AC-S4-a〜e、AC-S1-b、AC-I2-d〜e、AC-I3-e・g・h、BuildContract の両 root 拒否の case を追加。runtime fixture の file 一覧に `tools/Workflow/WindowsStoreRoot.psm1` を加える。binding を直接設定する case は子 process で行う。 | S1、S3、S4、I2、I3。 | 各 +10〜+60 |
| `tools/Artifacts/README.md`、`tools/Workflow/README.md`、`tools/Harness/README.md`、`docs/README.md`（H1 の所在の 1 文） | store の所在を LocalApplicationData と書く | 所在を `UserProfile` Known Folder 直下の `OneStarMaker` に改め、「実行する process の条件」を §4「決定 D2・D7」の意味に書き換え（probe は合流専用、入口は印と旧 root 残存）、reasonCode 表を更新し、合流の対象 root と旧 root の別名、`harness.ps1 -StoreRoot` が tests 専用であることを書く。 | 公開面は今この瞬間に真であること。 | 文のみ |

行数警報: 500 行・3 責務・50% 増に当たる file は無い（最大は `EvidenceSchedule.psm1` 422 行 +8。`StoreContextCliFixture.ps1` は 32 → 52 行で 50% を超えるが、fixture の seam 差し替えが増えただけで責務は 1 つのまま。非分割）。新規 resolver は「所在の決定」「印の検査」「旧 root 残存検査」の 3 つを持つが、いずれも「store の所在が正しく移行済みか」という 1 つの変更理由に属し、同じ依存・寿命・テスト方法を持つので非分割とする。probe と分けるのは前掲のとおり。

アーキテクチャゲートの照合（A1 自己確認。A2 R1 F7 で 1 と 7 を補正）:

1. 独立して変わる判断の同居: resolver は所有 role の最上位名 3 つ（`Artifacts`、`Workflow`、`Harness`）だけを残存検査のために持ち、内訳（`evidence-v2` 等）は持たない。同じ 3 名は `StoreConsolidation.psm1` の `TopLevel` 表と `EvidenceSchedule.psm1` の suffix 検査にもある。
2. Policy と I/O: 印の検査は I/O（file open、実体 path 照合）を含むが、seam で差し替え可能にし、判定の規則（schema、SID、prefix 一致）は seam の外に置く。
3. 依存方向: domain（Artifacts、Workflow の TaskEventStore、Harness）→ Workflow OS 基盤層（`WindowsStore*`）。逆向きの import を作らない。resolver は domain を知らない。
4. 所有者・寿命: resolver は状態を持たない。印の所有者は後続スライス（書く側）で、このスライスは読むだけ。
5. 単体テスト: resolver は temp base と seam で Unity なしに全経路を検証できる。
6. 公開面: 関数 3 つ（`Get-OsmStoreRoot`、`Get-OsmLegacyStoreRoot`、`Assert-OsmExplicitStoreRoot`）、reasonCode 3 つ（`store-not-migrated`、`legacy-store-present`、`store-root-unavailable`）。
7. 将来の変更: root の所在だけを変えるなら resolver 1 file、role の内訳の追加なら domain module 1 file、所有 role の最上位名の追加なら resolver・`StoreConsolidation`・`EvidenceSchedule` の 3 file、probe の方式なら `WindowsStoreContext` 1 file。

## 4. 実装計画

### 決定（根拠付き）

- **D1 新 root は `<UserProfile Known Folder>\OneStarMaker`。** (a) は probe 2 回目で実体側へ書かれることを観測済み。`%USERPROFILE%` は MSIX の書込み仮想化（AppData 配下と HKCU）の対象外で、Codex の既定サンドボックスを外せば書ける。(b) 別 drive は未観測で、drive の有無が host ごとに違う。(c) 設定 file は「設定 file はどこに置き、誰が守るか」を新たに問う。ACL の既定は `%LOCALAPPDATA%` と同じく profile 継承で、private 境界は従来どおり `<root>\Artifacts\*`、`<root>\Workflow`、`<root>\Harness` の下に置く。
- **D2 probe は 3 CLI の入口から外し、合流にだけ残す。** probe の目的は「この process が AppData へ書くと別実体へ行くか」の判定で、root が AppData の外へ出た後、新 root への書込みに対しては何も保証しない。A1 初稿は probe の結果で旧 root 残存検査の要否を選んでいたが、A2 R1 F3 のとおり、旧 root に生きた writer（旧 head の checkout、旧 runtime copy の清掃 job）が残る間に packaged app 内の新 CLI が残存検査を飛ばすと、旧 root と新 root に別の writer が並ぶ。残存検査は process の見え方を問わず無条件に行い、LocalCache 側の copy が見えて止まる場合は fail-closed な誤検出として受け入れ、残置の削除（program §6）で解く。`store plan` / `consolidate` は従来どおり非仮想化を要求する（合流の I/O 意味を検証した前提を変えない）。新 root に書込み probe を置かないのは、追加の I/O と「store 内の probe directory」を名前表に増やすことに見合う未検出の脅威が無く、読取側の仮想化・reparse・UNC は印の file handle の実体 path 照合で検出できるため。書込みの実体は V2 で実機観測する。
- **D3 検査の順序は `-StoreRoot` → 印 → 旧 root 残存で、印と残存の検査は `Get-OsmStoreRoot` の中にある。** `harness.ps1 -StoreRoot` が指定されたときは tests 専用の明示 root として `Assert-OsmExplicitStoreRoot` で検査し（prefix と実体 path・reparse の両方で旧 root 配下・新 root 配下・repo 配下を `store-root-unavailable` で拒否。A3 R3 F4）、印・残存検査は行わない。それ以外は `Get-OsmStoreRoot` が印を先に見てから旧 root の残存を照会する。残存検査を CLI の入口でなく resolver に置くのは、CLI を通らない writer（module の直接呼出し、binding 検証、清掃 job の開始）も同じ判定を通すため（A3 R3 F1）。binding 分岐を持つ 5 つの root getter（`Get-ArtifactRoot`、`Get-EvidenceRoot`、`Get-EvidenceDeploymentRoot`、`Get-EvidenceRuntimeRoot`、`Get-WorkflowRoot`）も、binding の有無にかかわらず最初に `Get-OsmStoreRoot` を呼ぶ。`AppDomain` に binding があることは `Assert-EvidenceStorageBinding` が成功したことの証明にならないため、binding だけで gate を省略しない（A3 R4 F1）。移行前の通常状態（印なし・旧 root あり）で `store-not-migrated` を返し、`legacy-store-present` を「移行後に旧 root が再出現した」異常の専用 code にするため（AC-I3-c）。
- **D4 印の書込み関数はこのスライスに含めない。** I2 は「印が無い間は新 root に作らない」を要求し、印を書く関数は新 root を作る唯一の経路になる。書く側（移行の実行、move か copy かの選択、ACL 再設定）は後続スライスが所有し、このスライスは形式と読取・検査だけを定める。tests は fixture で印を書く。
- **D5 旧 root は「別名」として残し、内容を読まない。** `Get-OsmLegacyStoreRoot` は path 文字列の導出だけで I/O をしない。用途は (1) 不変の記録の path bytes を新 root へ prefix 置換で付け替える（S4。別名側の存在確認・identity 照合はしない。binding あり・なしの両経路に適用。分類は下の「記録 path の分類表」）、(2) Build 取り込み元の拒否境界（既存どおり metadata の照会で実体 path も境界に含める）、(3) `Get-OsmStoreRoot` の残存検査（下の「旧 root の照会」。metadata のみ）、(4) 合流 plan の参照検査と manifest の `legacyRoots`。旧 root の file の内容を開く・作る・消す経路をこのスライスに作らない（I1）。
- **D9 旧 root の照会は不在を証明できたときだけ続行する。** `[IO.Directory]::Exists` はアクセス拒否・I/O 失敗でも false を返すので使わない。zero-access の `CreateFileW`（`OPEN_EXISTING`、`FILE_FLAG_BACKUP_SEMANTICS`、`FILE_FLAG_OPEN_REPARSE_POINT`）で所有 role の path を開き、`ERROR_FILE_NOT_FOUND` / `ERROR_PATH_NOT_FOUND` を不在、成功して directory 属性なら存在（`legacy-store-present`）、成功して通常 file なら不在（AC-I3-d）、reparse 属性・アクセス拒否・その他の error は `store-root-unavailable` とする（A3 R3 F3）。handle は即座に閉じ、内容を読まない。
- **D6 H1（`external-current-v1`）は不採用。** このスライスの branch の `harness.ps1` は移行前に `store-not-migrated` で止まるため、自分の固定記録を自分の store に書けない。Phase C / C' までの Evidence の stage / fetch と Workflow の start / end は、合流前の実ストアを所有する **develop 版 CLI**（非仮想化 process、`D:\...\SampleGameForOneStarMakerFramework` の develop checkout）で行う。O1 の決定（γ）により移行は GO 候補 head で merge の直前に実行され、その後は develop 版 CLI を使わず、Phase D の記録は GO 候補 head の CLI で行う。tracked HANDOFF（本書）を続ける。
- **D7 packaged app 内の実行条件を README に書く。** 製品名に依存しない語で、「packaged app 内の process は、その app 独自のサンドボックスを無効にし、同一 Windows ユーザーで実行する。サンドボックスが有効なら印の読取や store への書込みが失敗して止まる。LocalAppData 側に packaged app の copy（旧 root の `Artifacts` / `Workflow` / `Harness`）が残っている間は `legacy-store-present` で止まる。`<UserProfile>` とその祖先が reparse point の host（profile を別 drive へ移した構成）では `store-root-unavailable` になる」と書く。Codex 固有の設定値（`approval_policy`、`sandbox_mode`）は README に書かず、各自の git 管理外の運用メモに置く。
- **D8 resolver の例外は固定 code だけ。** `WindowsStoreRoot.psm1` は内部の .NET I/O 例外（印の open 失敗、`GetFinalPathNameByHandle` 失敗等）をすべて catch し、inner を付けずに固定 reasonCode だけを throw する。`harness.ps1` の catch は例外 message を stderr へそのまま出すので、この契約で I6 を満たす（A2 R1 F4）。

### 移行済みの印

- 位置: `<root>\store-root.json`（`<root>` = `<UserProfile>\OneStarMaker`）。
- 内容: JSON object、field は次の 4 つだけ（過不足は `store-root-unavailable`）。`schemaVersion`: 整数 1。`kind`: 文字列 `osm-store-root`。`ownerSid`: 書いた Windows ユーザーの SID 文字列（現在ユーザーと一致を要求）。`createdAt`: ISO 8601 文字列。
- 読取: `FileShare.Read` で開き、4 KiB 以下、UTF-8（BOM 無し）。handle の実体 path（`GetFinalPathNameByHandle`）が論理 path と一致しなければ `store-root-unavailable`。reparse point（file 自体と祖先）は拒否。存在しなければ `store-not-migrated`。0 byte や JSON でない内容は `store-root-unavailable`。
- 書く側: 後続スライス「store root 移設の実行と合流」。旧 root の所有 role が空であることを列挙で確認した後、移行（move または copy + ACL 再設定）の最後に、同じ directory の一時名に書いて flush し、rename で現す（部分状態を見せない）。このスライスは書かない（D4）。
- 合流の名前表: `StoreConsolidation.psm1` の `TopLevel` に `store-root.json` を `marker` として登録し、列挙はするが開かず、manifest の `excluded` に理由 `marker` で載せる。

### resolver の契約（`tools/Workflow/WindowsStoreRoot.psm1`）

- `Get-OsmStoreRoot`: `[Environment]::GetFolderPath('UserProfile')`（seam `BaseForTest`）→ `<base>\OneStarMaker` → 静的拒否（完全修飾でない、`\\` 始まり、repo 配下、`D:\OneDrive\OSM-Artifacts` 配下）→ 祖先と root の reparse 拒否 → 印の検査（`store-not-migrated` / `store-root-unavailable`）→ 旧 root の照会（D9。`legacy-store-present` / `store-root-unavailable`）→ root 文字列を返す。失敗は throw（message は固定 reasonCode。D8）。directory・file を作らず、ACL を変えない。呼出しごとに検査する（cache しない。`AppDomain` の binding と同じく process 内の差し替えを許さない。印の検査後に印が消える TOCTOU は呼出しごとの検査で縮めるだけで残り、受け入れる）。1 回の呼出しの I/O は印の open 1 回と旧 root の metadata 照会 3 回で、store module が 1 command 中に数百回呼んでも数百 ms 以内に収まる見込み。清掃 job も root getter を呼ぶたびに通る（binding 検証時だけの最適化はしない。必要になれば実測を根拠に B 適応ではなく A の revision で例外を定める）。`EvidenceCleanup.Tests.ps1` の deadline case で退行を見る。
- `Get-OsmLegacyStoreRoot`: `[Environment]::GetFolderPath('LocalApplicationData')`（seam `LegacyBaseForTest`）→ `<base>\OneStarMaker`。I/O 無し。
- `Assert-OsmExplicitStoreRoot <path>`: `harness.ps1 -StoreRoot` の値（tests 専用の明示 root）の検査。完全修飾でない・`\\` 始まり・prefix が旧 root 配下・新 root 配下・repo 配下なら `store-root-unavailable`。存在する最も近い祖先までを `WindowsStorePaths.psm1` と同じ方法で開き、祖先と root 自身の reparse を拒否し、実体 path についても同じ prefix 拒否を適用する（junction 経由の別名を遮断。A3 R3 F4）。印・残存検査は行わない。temp 配下の root は通る。
- 3 CLI の入口: `harness.ps1` は `-StoreRoot` があれば `Assert-OsmExplicitStoreRoot`、無ければ `Get-OsmStoreRoot` を、`artifacts.ps1` と `workflow-task.ps1` は `Get-OsmStoreRoot` を、store module の import より前に呼ぶ（既存の probe と同じ位置）。例外は `{"status":"failed","reasonCode":<code>}`・exit 1 にする（既存の出力形）。
- 出力・例外に path・SID・package 名を含めない（I6、D8）。

### 経路表（どの actor が、いつ root を読み・作り・書くか。A3 R3 F1）

| 経路 | 印の検査 | 旧 root 残存の検査 | 新 root への初回書込み | 備考 |
|---|---|---|---|---|
| 3 CLI（`artifacts.ps1`、`workflow-task.ps1`、`harness.ps1` の `-StoreRoot` なし） | 入口の `Get-OsmStoreRoot`、以後 store module の各 root 関数が呼ぶたび | 同じ呼出しの中 | 入口通過後、各 module の既存の作成処理（guard、operation directory 等） | 失敗は入口の JSON |
| store module の直接呼出し（tests、将来の script） | 各 root 関数が `Get-OsmStoreRoot` を呼ぶたび | 同じ呼出しの中 | 呼出し側の既存処理 | `TestRoot` 等の hook が設定されていれば resolver を通らない（tests 専用。production では未設定） |
| 清掃 job（`EvidenceCleanupJob.ps1` → runtime copy の `EvidenceSchedule.psm1`）。実際の順序: (1) `runtime.json` の hash 照合と manifest に列挙された runtime copy の file の hash 照合（読取専用。I2 の例外）→ (2) 検証済みの copy から module import → (3) `Initialize-EvidenceRuntimeContext`（runtime の identity / ACL、manifest の再検証、`Assert-EvidenceStorageBinding`）→ (4) role data の I/O | (3) の binding 検証（`Get-EvidenceBindingRoots` → `Get-OsmStoreRoot`）と、(4) 以降の各 root getter の呼出しごと | 同じ呼出しの中 | (3) の通過後、job の既存処理 | gate より前に読んでよい file は runtime copy 配下の `runtime.json` と manifest 列挙 file だけ（有限列挙）。role data（`Workflow`、`Harness`、`Artifacts\*` の記録）は gate の前に読まず書かない。失敗の外側の出力は既存の `scheduler-runtime-unavailable`。job は deadline 付きで短命で、途中で旧 root が再出現する窓は呼出しごとの検査で縮める（TOCTOU と同じ受容） |
| binding あり経路の `Get-EvidenceRoot` / `Get-ArtifactRoot` / `Get-EvidenceDeploymentRoot` / `Get-EvidenceRuntimeRoot` / `Get-WorkflowRoot`（job の中、または binding を設定した任意の process） | 呼出しごと（binding の `physicalPath` を返す前に `Get-OsmStoreRoot`） | 同じ呼出しの中 | 呼出し側の既存処理 | binding は `AppDomain` に固定され process 内で差し替え不可。binding の存在だけでは gate を省略しない（AC-I2-e、AC-I3-g） |
| `harness.ps1 -StoreRoot <path>`（tests 専用） | 行わない | 行わない | `Get-TaskDirectory` が明示 root 配下に作る | `Assert-OsmExplicitStoreRoot` が旧 root・新 root・repo（prefix と実体）を拒否 |
| `store plan` / `consolidate` | `Get-ConsolidationLogicalRoot` → `Get-OsmStoreRoot` | 同じ呼出しの中 | run directory の作成（既存） | 加えて `Assert-ConsolidationContext`（LocalAppData probe、非仮想化要求）。旧 root の実体を source にした合流は I3 で止まる（移行は後続の producer が行う） |
| 後続スライスの移行プログラム（producer） | 読取に使ってよい | 同上 | 印の書込みと move / copy は producer 自身（このスライスの外） | 旧 root が残る間は resolver が `legacy-store-present` を返すので、producer は resolver に依存せずに移設し、最後に印を書く |
| 旧コード（merge 前 develop の CLI、旧 runtime copy の job） | 無し | 無し | 旧 root に書く | 新コードから止められない。O1 の工程 3 で静止する |

### 記録 path の分類表（S4、D5。A3 R3 F6）

| 記録 path の種類 | 動作 |
|---|---|
| 新 root 配下の正規 path | 既存の identity / ACL の契約をそのまま適用する |
| 旧 root 配下の正規 absolute path（prefix 置換専用の別名） | 別名を open / Exists / identity 照合に通さず、新 root の同じ相対位置だけを既存の契約で確認する |
| binding あり経路での旧 root の別名 | 上と同じ。binding の logical / physical の健全性検査は別に維持する |
| LocalCache 配下の path（合流 plan） | 参照として publish しない。`reference-localcache` |
| repo、worktree、他 role、境界外の path | 別名の名目で新 root に取り込まない。従来どおり拒否、または非参照 |
| prefix だけ似た sibling（`<LocalAppData>\OneStarMaker2\...` 等） | 別名として扱わない（`\` 区切りの prefix 一致だけを別名とする） |
| `..`、境界 prefix の偽装、reparse、ACL 不正 | 既存の正規化・拒否規則を緩めない |

### 合流の manifest

- inventory と manifest に `legacyRoots`（文字列配列。このスライスでは `Get-OsmLegacyStoreRoot` の 1 件）を加える。`Get-ConsolidationItemReferences` は `logicalRoot` または `legacyRoots` のどれかの配下の完全修飾 path を参照とみなし、相対 key は配下にある root からの相対にする。`Test-ConsolidationManifest` と `Invoke-StoreConsolidation` の承認 manifest 検査は `logicalRoot` に加えて `legacyRoots` の一致を要求する（AC-S4-d）。2026-10-17 の manifest は root の移設後に `manifest-invalid` になるので、後続スライスが再 plan する。

### 変更対象と順序

1. resolver と `StoreRoot.Tests.ps1`（AC-I2、I3-b〜f、I4、I5、I6 の module 側、AC-I1-e）。
2. 3 CLI の入口を `Get-OsmStoreRoot` / `Assert-OsmExplicitStoreRoot` に置き換え、`StoreContextCliFixture.ps1` の境界と `StoreContext.Tests.ps1` の CLI case を改める（AC-I1-a〜d、AC-I3-a〜c）。
3. Artifacts 11 module・Harness・Workflow の root を resolver 経由にし（S1）、`StoreRootWiring.Tests.ps1` と既存 suite への追加 case（AC-S1-b、S3-a、S4-a、S4-e、I2-d、I3-e、BuildContract の両 root 拒否）。runtime copy の必須 file 一覧 2 箇所に resolver を加える。
4. `StoreConsolidationPlan` の複数 root と manifest の `legacyRoots`、名前表の `marker`（AC-S4-b〜e）。
5. `StoreRootHostFixture.ps1`（V2 用）。
6. README 4 件と `docs/README.md` の 1 文（D7 を含む）。
7. `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、§5 の起点 filter の suite を B の出口で通す。

### Phase B から Phase A へ差し戻す条件

- 印の形式・位置、reasonCode の集合、検査順序（D3）を変える必要が出た。
- resolver が domain module を import しないと成立しない、または domain module 間に新しい import が要る。
- 旧 root を開く・作る・消す経路が必要になった（I1 に抵触）。別名の prefix 置換だけでは読めない記録が見つかった（S4 の前提崩れ）。
- 既存 suite の fixture（`TestRoot` 等の hook）を外さないと新 root 前提にできない。
- `harness.ps1` の例外出力を allow-list 化しないと I6 を満たせない（D8 の前提崩れ）。
- Unity 側コードの変更が必要になった（§5 の適用除外の前提崩れ）。

### 対象外を維持する方法

- 実ストア・LocalCache 側・資格情報・R2 に Phase B で触らない。B のテストは temp base だけを使う。V1 は読取（存在確認・metadata）と失敗出力の観測だけで、V2 は `<UserProfile>` 直下の一時 base で行い、観測担当が終了時に削除する。
- 印を書く関数・移行 script・job 再登録・`consolidate` の実行は書かない。
- `ArtifactsRoot` 系（`--artifacts-root` / `OSM_ARTIFACTS_ROOT`）に触れない。
- Codex 固有の設定値を tracked 文書へ書かない。

## 5. テストとレビュー計画

- 単体テスト:
  - 新規 `tools/Workflow/tests/StoreRoot.Tests.ps1`: AC-I1-e、AC-I2-a〜c、AC-I3-b〜d と AC-I3-f（module 側）、AC-I4-a、AC-I5-a〜c、AC-I6-a。各 case は temp base を使い、失敗経路では base 配下の tree 一覧が前後で一致することを確認する。
  - 改修 `tools/Workflow/tests/StoreContextCliFixture.ps1` と `StoreContext.Tests.ps1`: CLI 3 入口の case を D2・D3 の意味に改め、AC-I1-a〜d、AC-I3-a〜c を CLI 境界で確認。probe の単体 case、判定が store module import より前であること、import の副作用が無いことの既存 case は維持。
  - 新規 `tools/Artifacts/tests/StoreRootWiring.Tests.ps1` と `Harness.Tests.ps1`・`TaskLifecycle.Tests.ps1` への追加: AC-S1-b。
  - 既存 suite への追加: `Credentials.Tests.ps1`（AC-S3-a）、`ArtifactEvidence.Tests.ps1`・`ArtifactTransfer.Tests.ps1`（AC-S4-a、AC-S4-e、binding あり・なし）、`EvidenceSchedule.Tests.ps1`（AC-I2-d、AC-I3-e の binding と job 開始境界）、`StoreConsolidationPlan.Tests.ps1`・`StoreConsolidation.Tests.ps1`（AC-S4-b〜e、名前表の `marker`）、`BuildCollect.Tests.ps1`（両 root の取り込み元拒否）、`Harness.Tests.ps1`（AC-I1-d、AC-I1-e の CLI 側）。
  - すべて `Task.Delay` / `Thread.Sleep` を使わない。
  - 追加・変更するテスト ID の一覧（判定 C が XML / JSON の case 名で収録を確認する。case 名は AC の ID を小文字 kebab にしたもの）: `StoreRoot.Tests.ps1` = `ac-i1-e`、`ac-i2-a`〜`ac-i2-c`、`ac-i3-b`〜`ac-i3-d`、`ac-i3-f-1`〜`ac-i3-f-6`、`ac-i4-a`、`ac-i5-a`〜`ac-i5-c`、`ac-i6-a`。`StoreContext.Tests.ps1`（CLI） = `ac-i1-a`〜`ac-i1-c`、`ac-i3-a`〜`ac-i3-c`、既存 `cli-decision-before-store`・`import-has-no-store-side-effects`（意味を改める）、既存の `decision-*`・`native-*`（不変）。`StoreRootWiring.Tests.ps1` = `ac-s1-b-<module 名>`（11 件）。`Harness.Tests.ps1` = `ac-s1-b-recordstore`、`ac-i1-d`、`ac-i1-e-cli`。`TaskLifecycle.Tests.ps1` = `ac-s1-b-taskeventstore`。`Credentials.Tests.ps1` = `ac-s3-a`。`ArtifactEvidence.Tests.ps1` / `ArtifactTransfer.Tests.ps1` = `ac-s4-a-binding`、`ac-s4-a-nobinding`、`ac-s4-e-1`〜`ac-s4-e-6`。`EvidenceSchedule.Tests.ps1` = `ac-i2-d`、`ac-i2-e`（子 process）、`ac-i3-e-binding`、`ac-i3-e-runtime-context`、`ac-i3-g`（子 process）、`ac-i3-h-job-entry`（子 process。書けなければ `未検証` と記録）。`ac-s4-e-5` の期待は API ごと（`ArtifactEvidence` = `invalid-evidence-path`、`ArtifactTransfer` = `Artifact path unavailable.`、`StoreConsolidationPlan` = `reference-localcache`）。`StoreConsolidationPlan.Tests.ps1` = `ac-s4-b`、`ac-s4-c`。`StoreConsolidation.Tests.ps1` = `ac-s4-d`、`marker-excluded`。`BuildCollect.Tests.ps1` = `source-root-both-roots-rejected`。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `tools/Workflow/tests/StoreRoot.Tests.ps1`、`tools/Workflow/tests/StoreContext.Tests.ps1`、`tools/Artifacts/tests/StoreRootWiring.Tests.ps1`、`tools/Artifacts/tests/Credentials.Tests.ps1`、`tools/Artifacts/tests/ArtifactEvidence.Tests.ps1`、`tools/Artifacts/tests/StoreConsolidationPlan.Tests.ps1`、`tools/Workflow/tests/TaskLifecycle.Tests.ps1`、`tools/Harness/tests/Harness.Tests.ps1`（各 `-Case '*' -ResultPath <path>`）。
- 判定必須テスト（GO 候補 head）: `tools/Artifacts/tests/*.Tests.ps1`（全 file）、`tools/Workflow/tests/*.Tests.ps1`（全 file）、`tools/Harness/tests/*.Tests.ps1`（全 file）を各 `-Case '*' -ResultPath <path>` で実行し、各 JSON の executed 1 件以上・failed 0 件。`pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` が error 0。AC-S1-a の `git grep`。V1・V2 の生観測（下記）。`git diff --stat abb99775..<head> -- unity/` が空であること。実行環境の前提: 判定用の worktree に `tools/Artifacts` の Release build（Packaging / Transport の DLL）があり、`rg` が PATH にあること（既知。無いと一部 suite が環境要因で失敗する）。
- 全 EditMode 回帰の適用除外: 適用除外。理由: 変更は `tools/` の PowerShell と README に限り、Unity 側（`unity/`）のコード・asmdef・Scene・Prefab・Addressables を変えない。代替証拠: 上記の offline suite 全件、`contract-audit`、`docs-audit`、`git diff --stat -- unity/` が空。
- 統合・Unity テスト: なし（Unity を使わない）。
- 操作・実行時・目視条件の検証経路:
  - **V1（AC-S2-b、I2 の実機）:** 対象: GO 候補 head。環境・初期状態: 実状態（印なし。`<UserProfile>\OneStarMaker` が存在しない）。担当: 非仮想化側 = 判定 C の実行担当（通常の pwsh）、packaged app 側 = owner が割り当てた Codex デスクトップの session（full access、同一 Windows ユーザー、作業 worktree は GO 候補 head）。前段: 非仮想化側が `[Environment]::GetFolderPath('UserProfile')` の値と、その祖先各段の reparse 属性を記録する（host 条件の記録。合否には使わない）。操作: 両側で `pwsh tools/workflow-task.ps1 status -Task store-root-v1-check` を 1 回ずつ。観測: stdout の JSON と exit code。非仮想化側が前後に `[IO.Directory]::Exists([IO.Path]::Combine([Environment]::GetFolderPath('UserProfile'),'OneStarMaker'))` と、旧 root 配下の tree 一覧（`StoreContext.Tests.ps1` の `Get-Tree` と同じ項目: 名前・属性・file の size と更新時刻。内容は読まない）を記録。期待（判定 C が照合）: 両側 `store-not-migrated`・exit 1、新 root が前後とも存在しない、旧 root の tree 一覧が前後で一致。保存証拠: 両側の生出力と前後の一覧を 1 つの時刻付きテキストにし、C / C' の bundle に含める。packaged app 側の出力は非仮想化側が受け取って同じ file に貼る。
  - **V2（AC-S2-a、S2 の実機）:** 対象: GO 候補 head。環境・初期状態: 非仮想化側が `<UserProfile>` 直下に `OneStarMaker.HostCheck.<32 桁 hex>` を作り、その中に `OneStarMaker\store-root.json`（fixture の印。`ownerSid` は現在ユーザー）を置く。担当: V1 と同じ。操作: packaged app 側で `pwsh tools/Workflow/tests/StoreRootHostFixture.ps1 -Base <その一時 base> -Action write`（resolver の seam で base を差し替え、`<root>\hostcheck\<guid>.txt` を 1 つ書き、`GetFolderPath('UserProfile')` の生の値・`Get-OsmStoreRoot` の返り値・書いた file の handle 実体 path・`Get-OsmStoreContext` の生結果を JSON で出す）。非仮想化側で `-Action read`（同じ root の一覧と各 file の handle 実体 path、`GetFolderPath('UserProfile')` の生の値を JSON で出す）。観測: 両 JSON。非仮想化側が `<LocalApplicationData>\Packages\*\LocalCache` 配下に同名 file が無いことを `Test-Path` で記録。期待: 両側の `GetFolderPath('UserProfile')` と root が一致、file の実体 path が論理 path と一致、packaged app 側が書いた file が非仮想化側の一覧にある、LocalCache 側に無い。packaged app 側の `Get-OsmStoreContext` の生結果（`virtualized` / `not-virtualized` / `unknown`）は合否に使わず記録する（A0 の未観測項目の初回観測）。後始末: 非仮想化側が一時 base を削除する。保存証拠: V1 と同じ形式。
  - V1・V2 は不可逆でも高コストでもないため高コスト証拠に分類しない。packaged app 側の担当を用意できなければ `未実施` とし、判定 C を完了と記録しない。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: packaged app（Codex デスクトップ、full access）内で pwsh を起動して repo の script を実行し file を書く経路は、A0 の probe 2 回目で疎通済み（file 作成と実体側への到達）。未確認なのは full access での LocalAppData 側 probe の振り分けで、V2 が生結果として記録する（合否に使わない）。追加の検証支援は `StoreRootHostFixture.ps1`（§3）。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし（V1・V2 は構造・値の検査で閉じる）。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: 判定 C の offline 実行。判定 C の担当が V1・V2 の生 JSON の複製に対し、`GetFolderPath('UserProfile')`・root・実体 path の一致と一覧の包含を照合する小さな script を自分の環境で書いて実行する。資格情報・通信・実ストアへのアクセスは不要。原記録へ適用する前に、次の合成 fixture 2 件で一度通す（A3 で凍結する期待値。値は実 host の path を含まない架空のもの）。
  - fixture 1（一致）: 両側の JSON で `userProfile` = `X:\Users\u`、`root` = `X:\Users\u\OneStarMaker.HostCheck.0123456789abcdef0123456789abcdef\OneStarMaker`、packaged 側の `written.logicalPath` = `<root>\hostcheck\a.txt`、`written.finalPath` = 同じ値、非仮想化側の `listing` に `hostcheck\a.txt` とその `finalPath` = 同じ値、`localCacheHit` = `false`。期待: 照合 script が `match` を返す。
  - fixture 2（不一致）: fixture 1 と同じで、packaged 側の `written.finalPath` だけを `X:\Users\u\AppData\Local\Packages\P_0\LocalCache\Local\OneStarMaker.HostCheck.0123456789abcdef0123456789abcdef\OneStarMaker\hostcheck\a.txt` にし、`localCacheHit` = `true`。期待: 照合 script が `mismatch` を返し、差異の field 名（`written.finalPath`、`localCacheHit`）を列挙する。
  - fixture の JSON は `tools/Workflow/tests/fixtures/storeroot-hostcheck-{match,mismatch}.json` として Phase B が `StoreRootHostFixture.ps1` の出力形と同じ schema で置き、判定 C は自分の script をまずこの 2 件に通してから原記録へ適用する。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC-S1-a の `git grep`。
- 機械検査・H1: H1（`external-current-v1`）は不採用（D6）。理由: この branch の `harness.ps1` は移行前に `store-not-migrated` で止まり、固定記録を書けない。Evidence の stage / fetch と `workflow-task.ps1` の start / end は、merge 前の develop checkout の CLI を非仮想化 process で使う（この branch の CLI は使わない）。task id は `artifact-store-root-relocation`。
- A0/A1 主担当・モデル・ベンダー: A0 = owner と Phase A 補助 session（本 revision の A0 固定）。A1 = Phase A 主担当 session（Claude Fable 5.1、Anthropic）。
- A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版（内容 sha256 を正、commit hash を補助）:
  - 入力版（R1）: A2 入力版。内容 sha256（git blob、LF）`f79db02808229cc0450dff7efb1393e1f908a20aa70e0b3a113e702fceb575ca`（checkout の CRLF 版は `e2a4ad160747546d104b78fcd6dcfdbe4f159b9e0429f2794d04b4b20a05a1d9`）。補助: commit `22e6b66315bcb5d8d086023cfa726e23133b2934`。
  - 入力版（R2）: A0 固定版（本書の commit `eebcdda71b182d0e96d2eed03ea4fab725841a43` 時点の全文。初稿を含まない）。内容 sha256（CRLF checkout）`acfaec553713d221650f0d491e51aaa5a9b404ba6d6af73ef79d1b27f20fbabc`。
  - **R1**（アーキテクチャゲート + 各不変条件を破る順序の構成 + 受け入れ条件の読み違え + 判定可能性 + O1 のトレードオフ）: 主担当 session が起動した新規 subagent（Claude Fable 5.1、Anthropic。A1 と同じ系列。独立性: 初稿を入力とし、R2 の指摘は見ていない）。2026-10-18。指摘 10 件と B〜E。原文は主担当 session の共有ノート。
  - **R2**（初稿を見ず、A0 だけから代替構成）: codex CLI 0.159.2 の非対話実行（GPT 系 `gpt-6-luna`、OpenAI、reasoning effort は既定の low、read-only sandbox、develop checkout `abb99775` を読取専用で参照。`docs/handoff/` は読んでいないことを log で確認）。2026-10-18。原文は主担当 session の共有ノート。
  - 採否（owner が 2026-10-18 に確認し、保留 2 件は O3 で決めた）:
    | 指摘 | 採否 | 反映先・理由 |
    |---|---|---|
    | R1 F1（`harness.ps1 -StoreRoot` で旧 root に書ける。I4 と矛盾） | 採用 | I1・I4・D3・`RecordStore.psm1`・`harness.ps1` 行、AC-I1-d。`-StoreRoot` は tests 専用の明示 root として残し、旧 root・新 root・repo 配下を拒否する。 |
    | R1 F2（`status` も guard が directory を作る） | 採用 | AC-I1-c の観測を修正。§1 の観測に追記。 |
    | R1 F3（`virtualized` で残存検査を飛ばすと旧 root の生きた writer と並ぶ） | 採用 | I3 を無条件に。D2 を書き換え、probe を 3 CLI の入口から外す。AC-I3-b/c を差し替え。packaged app の残置は program §6「Codex 側残置の後始末」の入口へ。 |
    | R1 F4（I6 が正常出力と矛盾。`harness.ps1` の stderr に path） | 採用 | I6 を失敗経路と resolver の例外に限定。D8 新設。AC-I6-a に stderr と .NET 例外の注入を追加。 |
    | R1 F5（別名の identity 照合が旧 root を開く。binding 経路に別名が無い） | 採用 | S4・D5・`EvidencePaths` / `ArtifactPaths` 行を「prefix 置換専用、開かない、binding あり・なし両経路」に。AC-S4-a に binding あり case。 |
    | R1 F6（AC-S1-a の同一行検査は見逃す） | 採用 | S1・AC-S1-a を「`LocalApplicationData` の出現 0 件（tests・probe・resolver を除く）」に。 |
    | R1 F7（自己確認 1 の記述が偽） | 採用 | ゲート照合 1・7 を補正。 |
    | R1 F8（fixture の欠落、14 file、必須 file 一覧、manifest の `logicalRoot`） | 採用 | §3 に `StoreContextCliFixture.ps1`・`EvidenceCleanupJob.ps1` 行を追加、S1 を 14 file に、`EvidenceSchedule` 行に必須一覧、§4「合流の manifest」、後続の入口に再 plan。 |
    | R1 F9（印の書込みの原子性） | 採用 | O2 と §4「移行済みの印」に一時名 + rename。 |
    | R1 F10（祖先 junction の host で恒久失敗） | 採用 | V1 の前段に reparse 属性の記録、D7 に host 条件。 |
    | R1 B（I2 の TOCTOU が残る） | 採用（受容として記録） | resolver の契約に明記。 |
    | R1 C（AC-I3-b の「続行」の観測、I2 の binding 経路の文言） | 採用 | AC-I3-b を `store:import:` の記録で定義。I2 に「直接、または binding 検証を介して」。 |
    | R1 D（V2 に `GetFolderPath('UserProfile')` の生の値、I2 の「開かず」の観測手段、V1 の記録項目、S5 の環境前提） | 採用 | S2・AC-S2-a・V2・AC-I2-a・V1・判定必須テストに反映。 |
    | R1 E（O1 のトレードオフ 1〜5、第 3 案 γ） | 採用 | O1 を α/β/γ と帰結で書き直し。後続の入口条件へ E1・E2 を送る。 |
    | R2 1・4・5・6・7（新 root (a)、資格情報 base、plan の pure 維持と通常 CLI に fallback を置かない、H1 不採用、EditMode 除外） | 採用（初稿と一致） | 変更なし。一致を記録。 |
    | R2 2（新 root の親直下に毎回 probe を置く） | 不採用 | `%USERPROFILE%` 直下は MSIX の書込み仮想化の対象外で、probe は追加の I/O と残骸を増やすだけで未検出の脅威に対応しない。読取側は印の実体 path 照合、書込み側は V2 の実機観測で確認する。V2 で不一致が出れば Phase A を再開する（D2）。 |
    | R2 3（印は versioned manifest。印を書く前に旧 root が空であることを移行側が検証。merge と移行の間は旧 CLI を止める運用） | 採用（一部は移送） | 印は `schemaVersion` を持つ（初稿どおり）。旧 root の空検証は後続スライスの入口条件へ。旧 CLI の停止は O1 と後続の手順へ。 |
    | R2 観測条件「両 root にデータがあるときはどちらも勝者にせず拒否」 | 採用（初稿と一致） | I3。 |
    | R2 観測条件「旧 root の bytes hash 比較」 | 保留 | I1 の観測は名前・属性・size・更新時刻（既存 `Get-Tree` 方式）。hash まで要るかは O3-(2) で owner が決める。 |
    | R2「互換 resolver（別名）に期限・削除条件を設ける」 | 保留（移送） | このスライスでは期限なし。program §6「分裂したストア」の入力として置く（O3-(1)）。 |
    | R2 リスク 1（旧バイナリが移行後に旧 root へ書く） | 採用 | O1・I3・後続の入口条件（job 停止）。 |
    | R2 リスク 2（旧絶対 path の参照切れ） | 採用（初稿と一致） | S4・D5。 |
    | R2 リスク 3（移行中の両 root の不一致） | 採用 | I3 と後続スライスの静止・照合。 |
    | R2 リスク 4（H1 の CURRENT・receipt の bytes/path） | 採用（移送） | 後続スライスの合流後確認へ。 |
    | R2 リスク 5（probe の過大な保証） | 採用（一致） | 本案は probe に新 root の保証を負わせない（D2）。 |
    | R2 責務配置「resolver を `WindowsStorePaths` 相当に置くか probe と統合」 | 不採用（配置は一致、統合はしない） | 変更理由が違う 2 つを 1 file にしない（§3）。同じ OS 基盤層に置く点は一致。 |
- A3 統合担当・モデル・採否: owner と Phase A 主担当 session（Claude Fable 5.1、Anthropic）。2026-10-18 に owner が O1〜O3 を決め、採否表と本文へ反映した。凍結前に owner が外部レビュー R3・R4 を取り、2026-10-19 に採用分を反映した。owner が 2026-10-19 に Phase A revision 1 として凍結を指示。凍結版 = この branch の先頭 commit（message に `A3 freeze`）。凍結版の内容 sha256 は commit の blob（`git rev-parse HEAD:docs/handoff/ARTIFACT_STORE_ROOT_RELOCATION.md`）で機械的に求められるので転記しない。
- A3 凍結前の外部レビュー R3（A2 とは別の finding として記録）:
  - 入力版: owner の O1〜O3 を反映した A3 統合版（未 commit の作業ツリー。A2 入力版 commit `22e6b663` からの差分を含む）。担当: owner が依頼した外部レビュー（GPT 系の Web サービス、OpenAI。初稿・A2 の原文は見ていない。公開 `develop` の `abb99775` のコードを照合）。2026-10-18。原文は主担当 session の共有ノート。判定は「A3 凍結は見送り推奨。方針変更は不要。F1〜F3 を契約・手順・テストへ反映」。
  - 採否:
    | 指摘 | 採否 | 反映先・理由 |
    |---|---|---|
    | R3 F1（旧 root 残存検査が 3 CLI の入口だけで、module 直接・binding・清掃 job が迂回する） | 採用 | I3 を module 水準に。残存検査を `Get-OsmStoreRoot` の中へ移し、`Assert-OsmStoreEntry` を廃止（D3）。§4「経路表」を追加。AC-I3-e（直接・binding・job 開始境界）。 |
    | R3 F2（cutover の順序と producer の head が実行可能な形で未定義） | 採用 | O1 に actor 4 者（A〜D）と工程表（10 手順、担当・版・入口状態・失敗時停止）を凍結。判定定義に「GO・C' は実移行の成功を先取りしない」。後続の入口に producer の境界と `store consolidate` を移設に使わないことを明記。 |
    | R3 F3（`Directory.Exists` の false を不在と扱う fail-open） | 採用 | I3 に「不在を証明できないときは `store-root-unavailable`」。D9（zero-access open で存在・不在・error を区別）。AC-I3-f の 6 case。 |
    | R3 F4（`-StoreRoot` の文字列検査だけでは junction 経由の旧 root を遮断できない） | 採用 | I1・I4・D3 に実体 path と reparse の検査。`Assert-OsmExplicitStoreRoot` の契約。AC-I1-e（junction 3 case、作れない環境は skipped + 注入 case）。 |
    | R3 F5（V2 は fixture の一致であり、production root の移行後動作を保証しない） | 採用 | S2 に「GO が判定するのは host 間の fixture の一致まで」。後続の入口に移行後の smoke test 項目（seam 無しの resolver と 3 CLI、binding 再生成、`status` / `current` の読戻し、旧 role 不在、job 停止、packaged app は残置がある間 `legacy-store-present` を期待）。 |
    | R3 F6（binding と prefix 置換専用の別名の規則を 1 つの表に固定） | 採用 | §4「記録 path の分類表」。S4 に参照。AC-S4-e の組合せ case。 |
    | R3 F7（名前と範囲の一致: I3 の見出し、AC-S2-a の生の値と適用後の値、AC-S1-a の補完、hash を要求しない） | 採用 | I3 の文面、S2・AC-S2-a の注記、経路表で AC-S1-a を補完。hash は O3 のとおり含めない。 |
    | R3「改稿時の指示」1〜5（Phase B・実移行を始めない、migration ツール・旧 root 削除をこのスライスに足さない、actor 4 者を混同しない、凍結指示なしで commit / push / PR に進まない、提出物 (a)〜(e)） | 採用 | 本文（a）、本表（b）、§4 経路表（c）、O1 工程表（d）、§5 テスト ID 一覧（e）。commit・push・PR は owner の凍結指示まで行わない。 |
- A3 凍結前の外部レビュー R4（R3 の第 2 巡。A2 とは別の finding として記録）:
  - 入力版: R3 反映後の A3 統合版（未 commit の作業ツリー、2026-10-19）。担当: owner が依頼した外部レビュー（GPT 系の Web サービス、OpenAI。R3 と同じ担当。公開 `develop` の `abb99775` のコードと R3 を照合）。2026-10-19。原文は主担当 session の共有ノート。判定は「R3 の指摘は概ね反映。方針維持。F1 を凍結前に解消すれば凍結を前向きに判断できる」。
  - 採否:
    | 指摘 | 採否 | 反映先・理由 |
    |---|---|---|
    | R4 F1（binding 済みの root getter が gate を迂回できる。binding の存在は検証成功の証明でない） | 採用（推奨案） | I2・I3 を「binding の有無を問わず」に。binding 分岐を持つ 5 getter（`Get-ArtifactRoot`、`Get-EvidenceRoot`、`Get-EvidenceDeploymentRoot`、`Get-EvidenceRuntimeRoot`、`Get-WorkflowRoot`）が最初に `Get-OsmStoreRoot` を呼ぶ（D3、§3 の各行、経路表）。清掃 job だけ一度にする最適化は採らない（性能見込みを改訂）。AC-I2-e、AC-I3-g（子 process で binding を直接設定）。 |
    | R4 F2（清掃 job の hash-first と I2/I3 の「読まない」を切り分ける） | 採用 | I2 に例外（runtime copy の `runtime.json` と manifest 列挙 file の読取専用の整合性検査）を有限列挙で明記し、role data の I/O は gate 前に禁止。経路表の job 行を実際の順序に。AC-I3-e (3) を単体 case と明記し、AC-I3-h（実 entry script、外側の `scheduler-runtime-unavailable` を尊重、書けなければ `未検証`）を追加。 |
    | R4 F3（AC-S4-e の期待 reasonCode を API ごとに分ける） | 採用 | AC-S4-e とテスト ID 一覧に 3 系統の期待値（`invalid-evidence-path`、`Artifact path unavailable.`、`reference-localcache`）。 |
    | R4「再依頼文」（F1 最優先、hash-first を壊さない、O1〜O3・producer/consumer・別名・ACL/DPAPI を維持、実装・実ストア・Phase B・commit・push・PR は指示まで行わない） | 採用 | 本文のとおり。commit は owner の凍結指示（2026-10-19）で行った。push・PR は未実施。 |
- A3 主担当の再点検（2026-10-19。R3 反映後の本文を通読し、コードと照合した結果。本文へ反映済み）:
  - `RevisionProcessLease.cs` が旧 root の親 `OneStarMaker` を作り直す（§1 対象外・観測に追記。I3 は親の存在を判定に使わない設計で整合）。
  - I2 の「既存 file を開かず」が祖先の reparse 検査（metadata）と矛盾して読めたので、「内容を読まず」に直した。
  - `Assert-OsmStoreEntry` の廃止後に残る参照が無いことを確認した（R3 の採否表の記述だけ）。
  - `StoreContextCliFixture.ps1` の seam に `LegacyQueryForTest` を加えた。
  - 清掃 job の開始境界の fixture は `EvidenceSchedule.Tests.ps1` の既存の `Initialize-EvidenceRuntimeContext` 用 runtime fixture（4 箇所）を再利用できる。
  - `.agents` 配下の文書に store の所在の記述は無い（`osm-unity-editor` の `%LOCALAPPDATA%\Unity\bin` は Unity CLI の所在で無関係）。README 更新の対象は §3 の 4 file と `docs/README.md` の 1 文で足りる。
- Phase C / C' で指摘されうる点と本書の答え（A3 主担当の予見。C / C' の判断を縛らない）:
  1. 「resolver を毎回呼ぶ性能」: 1 呼出し = 印 open 1 + metadata 照会 3。清掃 job も getter ごとに通る（R4 F1）。C が `EvidenceCleanup.Tests.ps1` の deadline case で退行を見る。退行しても gate の省略は契約変更なので B 適応にせず、実測を根拠に A の revision で例外を定める。
  2. 「`$PSScriptRoot` 基準の repo 判定が runtime copy では runtime の directory を指す」: 既存の `CredentialPathAcl.psm1` と同じ前提。新 root が runtime copy（`<root>\Artifacts\runtime\<head>`）の配下になることは無いので誤拒否しない。逆向き（runtime copy が root の配下）は拒否条件でない。
  3. 「印の TOCTOU」: 受容済み（resolver の契約）。blocker にはならない（凍結済み条件に違反しない）。
  4. 「`TestRoot` 等の hook が production の迂回路」: 既存の受容パターン（private seam、CLI・環境変数から設定不可）。経路表に明記。
  5. 「`-StoreRoot` を tests 専用と言いながら public 引数のまま」: I1・I4 で規則を固定し、README に tests 専用と書く。引数の削除は Harness suite の構造変更になるので対象外。
  6. 「旧 root の親 `OneStarMaker` が残る／再出現する」: Unity の `RevisionLocks` のため意図どおり。所有 role 3 つだけを見る。
  7. 「packaged app が LocalCache 残置で `legacy-store-present` になり続ける」: 意図どおり（I3、D2）。残置の削除は program §6。V2 は fixture の一致だけを判定し、移行後の smoke は後続。
  8. 「`virtualized-store` / `store-context-unknown` が 3 CLI から消えるのは契約の後退か」: 入口の probe は AppData 内の store を守るものだった。root が AppData 外へ出た後は守る対象が無く、合流の非仮想化要求に残す。README の表を更新する。
  9. 「`D:\OneDrive\OSM-Artifacts` の固定 path」: 既存契約の引継ぎ（§1 対象外）。
  10. 「H1 task の CURRENT / receipt の path bytes」: 後続スライスへ移送済み（§2）。このスライスは Harness の root 所在だけを変える。
  11. 「全 EditMode 回帰の適用除外」: `git diff --stat -- unity/` が空であることを判定必須に含めた。`RevisionProcessLease.cs` も変えない。
  12. 「判定 C の照合 script の合成 fixture が凍結されていない」: §5「判定 C の照合 script の方式」に 2 件（一致・不一致）の期待値と置き場所を書いた。
- C' 用に予約した担当・モデル・ベンダー: Phase A で使った系列（Claude 系、GPT 系）以外を 1 つ残す。実績は C' の欄に記録する。
- 独立性の強化条件を満たせない場合の理由: R1 は A1 と同じ系列（別 session・初稿のみ入力）。R2 は別ベンダーで初稿を見ていない。C' は未関与の系列を使える見込み。

## 6. Phase B 実装結果

Phase B 担当: 新規 session（Claude Fable 5.1、Anthropic）。2026-10-19。実装 base は `abb99775`、A3 凍結版 `b07cd685` から着手。判定 C・C'・push・PR 更新は行っていない。

### 実装した内容（§4「変更対象と順序」1〜7）

1. resolver `tools/Workflow/WindowsStoreRoot.psm1`（新規）: `Get-OsmStoreRoot`（静的拒否 → 祖先と root の reparse 拒否 → 印の検査 → 旧 root の照会 → root）、`Get-OsmLegacyStoreRoot`（文字列のみ）、`Assert-OsmExplicitStoreRoot`（prefix と、存在する最も近い祖先の実体 path の両方で旧 root・store root・repo 配下を拒否）。印は `FileShare.Read` で開き、実体 path 一致、4 KiB 以下、BOM 無し UTF-8、4 field ちょうど、`ownerSid` が現在ユーザーを要求。旧 root の照会は zero-access の `CreateFileW`（D9）で存在・不在・file・reparse・denied・error を区別。例外は固定 reasonCode だけを throw（D8）。seam は `BaseForTest`、`LegacyBaseForTest`、`MarkerIoForTest`、`LegacyQueryForTest`。`tools/Workflow/tests/StoreRoot.Tests.ps1`（新規、18 case）。
2. 3 CLI の入口を `Get-OsmStoreRoot`（`harness.ps1 -StoreRoot` 時は `Assert-OsmExplicitStoreRoot`）に置換。`StoreContextCliFixture.ps1` は本物の resolver と `WindowsStorePaths` を読み、seam を test の一時 base に向け、印・旧 root の操作を記録する。`StoreContext.Tests.ps1` に `ac-i1-a`〜`ac-i1-c`、`ac-i3-a`〜`ac-i3-c` を追加し、`cli-decision-before-store`・`import-has-no-store-side-effects` の意味を改めた（`decision-*`・`native-*` は不変）。
3. 14 file の root を resolver 経由に変更（S1。AC-S1-a の `git grep` は tests・probe・resolver を除いて 0 件）。binding 分岐を持つ 5 getter は binding の有無を問わず最初に `Get-OsmStoreRoot` を呼ぶ。`Get-EvidenceBindingRoots` は hook の有無を問わず gate を通す（AC-I2-d）。`Resolve-EvidenceStoredPath` / `Resolve-ArtifactStoredPath` に旧 root の別名（prefix 置換専用。別名側は開かず identity 照合もしない。binding あり・なし両経路）。`Assert-BuildSourceRoot` の境界に両 root の `Artifacts` / `Workflow`。`Initialize-CredentialRoot` の一段目（store root）は `Assert-SharedParent`（存在確認のみ、作らない）。runtime copy の必須 file 一覧 2 箇所に resolver を追加。`tools/Artifacts/tests/StoreRootWiring.Tests.ps1`（新規、`ac-s1-b-<module>` 11 件 + `ac-i3-e-modules`）、`Harness.Tests.ps1`（`ac-s1-b-recordstore`、`ac-i1-d`、`ac-i1-e-cli`）、`TaskLifecycle.Tests.ps1`（既存 case が TestRoot で通ることを維持。binding 直接設定の既存 2 case が gate を通る）、`Credentials.Tests.ps1`（`ac-s3-a`）、`ArtifactEvidence.Tests.ps1` / `ArtifactTransfer.Tests.ps1`（`ac-s4-a-binding`、`ac-s4-a-nobinding`、`ac-s4-e-1`〜`6`）、`EvidenceSchedule.Tests.ps1`（`ac-i2-d`、`ac-i2-e`、`ac-i3-e-binding`、`ac-i3-e-runtime-context`、`ac-i3-g`、`ac-i3-h-job-entry`）、`BuildCollect.Tests.ps1`（`source-root-both-roots-rejected`）。
4. `StoreConsolidation.psm1` / `StoreConsolidationPlan.psm1`: 合流先は `Get-OsmStoreRoot`。inventory・plan・manifest に `legacyRoots`（`Get-OsmLegacyStoreRoot` の 1 件）。`Get-ConsolidationItemReferences` は store root か別名のどれかの配下を同じ相対 key に写像。`Test-ConsolidationManifest` と `Invoke-StoreConsolidation` は `legacyRoots` の一致を要求（違えば、または旧 manifest のように無ければ `manifest-invalid`）。`TopLevel` に `store-root.json`=`marker`（列挙のみ、開かない、`excluded` に理由 `marker`）。`StoreConsolidationPlan.Tests.ps1`（`ac-s4-b`、`ac-s4-c`）、`StoreConsolidation.Tests.ps1`（`ac-s4-d`、`marker-excluded`）。plan module は pure のまま（`plan-module-is-pure` 合格）。
5. `tools/Workflow/tests/StoreRootHostFixture.ps1`（新規。`-Base <UserProfile>\OneStarMaker.HostCheck.<hex>` を要求し、`-Action write` / `read` の JSON を出す）。照合 script 用の合成 fixture `tools/Workflow/tests/fixtures/storeroot-hostcheck-{match,mismatch}.json`（§5 の期待値どおり。`packaged` = write 出力、`native` = read 出力の schema）。
6. README 4 件と `docs/README.md` の 1 文。`tools/Artifacts/README.md` の「実行するprocessの条件」を store root と印の判定に改め（D2・D7、reasonCode 表、別名、`-StoreRoot` が tests 専用、packaged app 内の実行条件を製品名に依存しない語で）、仮想化判定は `store plan` / `consolidate` 専用の小節へ。合流の節と資格情報・Build・runtime・退役記録の所在の文を改めた。anchor を参照する 3 箇所を更新。
7. B の出口の検査: `pwsh tools/contract-audit.ps1` errors 0（warnings 0）、`pwsh tools/docs-audit.ps1` errors 0（warning 1 = 本書の §7/§8 検出。A3 時点から同じ）、`git diff --stat abb99775 -- unity/` は空。suite の結果は下の表。

### HANDOFF との差（B 適応として記録。契約・責務配置・reasonCode・検査順序は変えていない）

- **旧 root の base の導出（I4 の観測に関わる事実）:** `[Environment]::GetFolderPath('LocalApplicationData')` は、registry の template `%USERPROFILE%\AppData\Local` を process の環境変数で展開するため、`USERPROFILE` を差し替えた子 process では結果が変わる（2026-10-19 に実測。`UserProfile` Known Folder は profile の登録 path から導かれ、環境変数に追従しない）。`Get-OsmLegacyStoreRoot` は `SHGetKnownFolderPath(FOLDERID_LocalAppData, KF_FLAG_DEFAULT_PATH | KF_FLAG_DONT_VERIFY)`（shell が profile の登録から導く既定位置。環境変数に追従しないことを同日に実測）から導く。旧 root の残存検査は、process が見ている `LocalApplicationData` が既定位置と違う場合（AppData を redirect した host）にはその場所の所有 role も併せて照会する（fail-closed の側に倒す。返す別名は 1 件のまま）。AC-I4-a は凍結どおり「子 process で導出結果が変わらない」で合格。残る入力（後続スライスへ）: Local AppData を registry で redirect した host では、別名が既定位置になるため、redirect 先に書かれた旧記録の prefix 置換は効かない。標準の host では既定位置と実際の値は一致する。
- **`Get-HarnessStoreRoot`（`RecordStore.psm1` の export 1 件）:** `harness.ps1 status` の一覧 root と `Get-TaskDirectory` の既定 root を同じ導出にするための共通関数。§3 の「既定を `Get-OsmStoreRoot` + `Harness`、`-StoreRoot` は `Assert-OsmExplicitStoreRoot` を通す」の実装詳細で、新しい契約ではない。
- **CLI fixture の引数:** `-Mode` ではなく `-Base` / `-LegacyBase` を受け、印・旧 root の状態は test 側が共有 helper で用意する（tree 一覧の前後比較が CLI 呼出しだけを挟むため）。
- **テスト専用の共有 helper（§3 に無い test file）:** `tools/Workflow/tests/StoreRootTestSupport.ps1`（temp 配下の Known Folder 代役、印の書込み、seam の設定、tree 一覧、記録用の seam）と `tools/Artifacts/tests/CliChild.ps1`（文法・出力衛生だけを見る case が `artifacts.ps1` を gate 通過済みの一時 store root で起動する wrapper）。production code ではない。
- **既存 suite の fixture:** `TestRoot` 等の hook は外していない。入口の gate と binding 経路の gate を通すため、`New-EvidenceTestEnvironment`（Evidence 系の Artifacts suite）、Build / Content の親 fixture（`New-CollectFixture`、`New-BuildFixture`、BuildCleanup の `New-CleanupFixture`、`ContentTestSupport.ps1` の `New-ContentFixture`。`SeriesRetirementStore` などは `TestRoot` を設定しない経路でも `Get-OsmStoreRoot` を呼ぶため、seam が無いと実 host で `store-not-migrated` になり `retirement-unknown` に畳まれる）、各 child fixture（`EvidenceTestChild`、`Build*Child`、`Content*Child`、`SeriesRetirementChild`、EvidenceSchedule / EvidenceCleanup / StoreConsolidation の子 script）、`WorkflowModuleFixture`、`TaskLifecycle.Tests` が temp 配下の印付き store root へ seam を向ける。`WorkflowCliFixture` の mock は `Get-OsmStoreContext` → `Get-OsmStoreRoot`。`StoreContext.Tests.ps1` の `cli-virtualized-all-entries`・`cli-unknown-failure-stages` は、probe が入口から外れたため削除し、`ac-i1-a`・`ac-i1-b`・`ac-i3-a`・`ac-i3-c` が全入口の拒否を引き継ぐ。`StoreConsolidationPlan.Tests.ps1` の in-memory root 文字列を新 root の形に改め、`legacyRoots` を inventory に加えた。
- **AC-I2-a / AC-I3-e(1) の module 直接呼出し:** `StoreRoot.Tests.ps1` の `ac-i2-a` が 9 関数（Artifacts / Harness / Workflow の module を test 依存として import）を呼ぶ。AC-I3-e(1) の module 側は `StoreRootWiring.Tests.ps1` の `ac-i3-e-modules`（`Get-EvidenceBindingRoots`・`Assert-BuildSourceRoot` も含む）。
- **AC-I3-h:** 実 entry script（runtime copy の `EvidenceCleanupJob.ps1`）を、runtime copy 側の resolver instance に seam を設定した wrapper から `&` で起動する（`pwsh -File` 直接起動では seam を注入できないため）。観測は seam の記録（印の open が印 1 件、旧 root の照会が `Workflow=directory`）と新 root・role data の tree 一覧の前後一致で、`FileShare.None` の別 handle は使っていない。期待どおり外側の `scheduler-runtime-unavailable`・exit 1。
- **AC-S4-e(1) と I3 の両立（受け入れ条件の文言の隙間。I3 を優先）:** AC-S4-e(1) は「別名側に同名 file が残っている」状態で別名が新 root へ解決されることを求めるが、binding あり経路は I3 の gate を通るため、旧 root に `Artifacts` が残っていれば `legacy-store-present` で止まる（意図どおり）。そこで `ac-s4-a-binding` と `ac-s4-e-*` の binding 側は、旧 root を空にしてから実行し、別名が prefix 置換だけで解決されること・旧 root を開かず identity も読まないことを確かめる。disk 上に別名 file が残る状態は binding なし経路（`TestRoot` で gate を迂回する既存 hook）だけで確かめる。ArtifactEvidence と ArtifactTransfer の両方で同じ形。同じ理由で、BuildCollect の `source-root-both-roots-rejected` は旧 root 側に directory を作らず、path 文字列だけで境界を検査する（Wiring の `ac-s1-b-buildcontract` と同じ形）。
- **文法だけを見る CLI case:** `artifacts.ps1` を直接 `pwsh -File` で起動していた既存 case（ArtifactEvidence の `promote-cli` の bare 形、`legacy-cli-network-zero`、ArtifactTransfer の `legacy-config-reference-commit-network-zero`、BuildPublish・Content.Publish の文法 case）は、入口の gate で先に止まるため `CliChild.ps1` 経由に変えた。期待する reasonCode（`unsupported-command`）は変えていない。
- **AC-S3-a の「root 不在」:** 印がありながら store root が無い状態は TOCTOU でしか生じないので、`MarkerIoForTest` でメモリ上の印を返して再現した。
- **`New-ArtifactOperation`:** 本番経路は store root の存在を要求して作らない。`TestRoot` 経路は既存 fixture のため従来どおり shared 親を作る。
- **`ac-i1-e` / `ac-i3-f-4` / `ac-i5-a` の junction:** この環境では junction を作れたので実体で検証した。作れない環境では `observed` に `skipped` を記録し、identity seam の注入 case（`ac-i1-e` は常に併走）で代替する。

### B の出口の suite 結果

2026-10-19、B の出口 commit と同じ作業ツリー（commit 直前）で、`tools/Artifacts/tests`・`tools/Workflow/tests`・`tools/Harness/tests` の `*.Tests.ps1` 全 30 file を各 `-Case '*' -ResultPath <path>` で実行した（§5 の起点 filter 8 suite を含む。`rg` を PATH に足した）。30 file すべて executed ≥ 1・failed 0、exit 0。各 JSON は B session の作業領域に保持し、判定の入力にしない。`pwsh tools/contract-audit.ps1` errors 0（warnings 0）、`pwsh tools/docs-audit.ps1` errors 0（warning 1 は本書の §7/§8 検出で A3 時点と同じ）、AC-S1-a の `git grep` は対象外を除いて 0 件、`git diff --stat abb99775 -- unity/` は空。

| suite | registered | executed | failed |
| --- | --- | --- | --- |
| `Workflow/StoreRoot` | 18 | 18 | 0 |
| `Workflow/StoreContext` | 26 | 26 | 0 |
| `Workflow/TaskLifecycle` | 23 | 23 | 0 |
| `Harness/Harness` | 38 | 38 | 0 |
| `Harness/UnityGate` | 21 | 21 | 0 |
| `Harness/UnityObservation` | 16 | 16 | 0 |
| `Harness/UnityTestRunner` | 31 | 31 | 0 |
| `Artifacts/StoreRootWiring` | 12 | 12 | 0 |
| `Artifacts/Credentials` | 23 | 23 | 0 |
| `Artifacts/ArtifactEvidence` | 53 | 53 | 0 |
| `Artifacts/ArtifactTransfer` | 28 | 28 | 0 |
| `Artifacts/ArtifactPackage` | 9 | 9 | 0 |
| `Artifacts/ArtifactRotation` | 11 | 11 | 0 |
| `Artifacts/StoreConsolidationPlan` | 26 | 26 | 0 |
| `Artifacts/StoreConsolidation` | 22 | 22 | 0 |
| `Artifacts/EvidenceSchedule` | 87 | 87 | 0 |
| `Artifacts/EvidenceCleanup` | 56 | 56 | 0 |
| `Artifacts/EvidenceReset` | 31 | 31 | 0 |
| `Artifacts/EvidenceRetention` | 21 | 21 | 0 |
| `Artifacts/R2RouteTransport` | 7 | 7 | 0 |
| `Artifacts/RouteProof` | 26 | 26 | 0 |
| `Artifacts/BuildCollect` | 10 | 10 | 0 |
| `Artifacts/BuildPublish` | 9 | 9 | 0 |
| `Artifacts/BuildRetention` | 9 | 9 | 0 |
| `Artifacts/BuildCleanup` | 23 | 23 | 0 |
| `Artifacts/Content.Contract` | 9 | 9 | 0 |
| `Artifacts/Content.Fetch` | 5 | 5 | 0 |
| `Artifacts/Content.Publish` | 12 | 12 | 0 |
| `Artifacts/ContentCleanup` | 23 | 23 | 0 |
| `Artifacts/SeriesRetirement` | 17 | 17 | 0 |

### 動作確認（B が実行してよい分）

- **V1 非仮想化側（B session、判定入力にしない）:** 実状態（印なし）で `pwsh tools/workflow-task.ps1 status -Task store-root-v1-check` を 1 回。生出力 `{"status":"failed","reasonCode":"store-not-migrated"}`、exit 1。`<UserProfile>\OneStarMaker` は前後とも存在しない。旧 root の tree 一覧（名前・属性・size・更新時刻）は前後で一致（47,974 entry、内容は読んでいない）。`UserProfile` の各祖先の reparse 属性はすべて false。期待との差: なし。時刻付きの生記録は B session の作業領域に保持（path は転記しない）。
- **V1 packaged app 側、V2 両側:** 未実施（packaged app 側の担当 session が要る）。`StoreRootHostFixture.ps1` を用意済み。

### 未実行・未確認

- 判定 C・C'、push、PR #139 の状態ブロック更新（B の責任外。指示により行わない）。
- Unity: 変更なし（`git diff --stat abb99775 -- unity/` が空）。Editor のコンパイル確認は対象外。
- 動作確認の packaged app 側（V1）と V2。

## 7. Phase C

未到達。

## 8. Phase C'

未到達。

## 9. Phase D

未到達。
