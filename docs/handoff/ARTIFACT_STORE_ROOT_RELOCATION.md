# Artifact Storage: store root の LocalAppData 外への移設

Artifact Storage・Workflow・Harness が共有する store の実体を、packaged app の AppData 書込み仮想化の対象外へ移し、どの host・どの起動経路から使っても同じ場所に書く契約にするスライス。`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §6 の保留項目「Virtualized Host Route」を owner が 2026-10-18 に再開し、問いを「仮想化された場所の中で動かす」から「仮想化されない場所へ移す」に置き換えたもの。

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A0 固定済み。次は A1）
- branch: `claude/artifact-store-root-relocation-plan`（PR base は `develop`）
- implementation base commit: `abb997751035ab3a284916a97c32f887a8faec16`（`origin/develop`、PR #138 を含む）
- implementation head commit: 未固定
- risk: `high`（保存先の移動、資格情報 root の base 変更、fail-closed 契約の追加、一回の移行を後続に持つ）
- owner: OSM maintainers（A3 の凍結と Phase D の判断は owner）
- created: 2026-10-18
- expires: 2026-12-18 または置換 revision
- harvest to: `tools/Artifacts/README.md`（store の所在、実行する process の条件）、`tools/Workflow/README.md`、`tools/Harness/README.md`、`docs/README.md` の program 表、`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4・§6
- Phase A snapshot の取得案内・状態: 未生成
- Phase B result snapshot の取得案内・状態: 未生成（未到達）
- evidence bundle の取得案内・状態: 未生成（未到達）
- C' blind bundle の取得案内・状態: 未生成（未到達）
- 引継ぎ票:
  - 次の Phase・役割: Phase A 主担当（A1 初稿）。その後 A2 独立レビュー、A3 統合・凍結。
  - 次の作業: §1〜§2 に固定した A0 を入力に、§2 の最低条件（不変条件・成立条件）と受け入れ条件、§3 責務マップ、§4 実装計画、§5 テスト計画を A1 初稿として埋める。A2 は高リスクの規則に従い 2 件以上の独立レビューを取り、1 件はアーキテクチャゲート、1 件は初稿を見せず A0（§1〜§2）だけから代替構成を出す。A3 で owner と採否を分類して凍結し、PR を `develop` へ開いて状態ブロックを置く。Phase B は始めない。
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

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: store root を AppData 外の固定位置に一元化し、移行前の process も移行後の process も store を再分裂させずに fail-closed に振る舞う実装は、既存の保存契約・資格情報契約・offline suite を保ったまま成立するか。
- 進める最低条件（A1 で不変条件と成立条件に分けて書く。A0 からの必須入力は次のとおりで、A1 は観測可能な語に直して採用し、削る場合は理由を書く）:
  - 不変条件（候補）:
    - 旧 root（`%LOCALAPPDATA%\OneStarMaker`）の既存 entry を、このスライスのコードが変更・削除・移動しない。
    - 新 root に移行済みの印が無い間は、どの CLI も新 root に store を作らず、既存 file も開かず、固有の reasonCode（例 `store-not-migrated`）で拒否する。旧 root に store が残っている間も同じ。
    - 新 root の導出に環境変数の値を正として使わない。
    - 新 root が repo 配下、OneDrive 同期配下、reparse point 配下なら拒否する。
    - 秘密（鍵、暗号化 store、header、署名 URL）を引数・ログ・Git・出力へ出さない。
  - 成立条件（候補）:
    - tests 以外で LocalApplicationData から root を組んでいた 15 file が、1 つの resolver を経由する。
    - 非仮想化 process と、full access の Codex デスクトップ内 process の両方から、新 root の同じ論理 path が同じ実体に解決される。
    - 資格情報 root の ACL 検査（`CredentialPathAcl.psm1` の base）が新 root でも同じ規則で通る。
    - 既存 offline suite（`tools/Artifacts/tests`、`tools/Workflow/tests`、`tools/Harness/tests`）が新 root 前提で合格する。
- 受け入れ条件: A1 で、各不変条件に失敗経路を含む 1 件以上、各成立条件に検証例を書く。時点（移行前・移行中・移行後）と、旧 root と新 root の両方に store がある状態の扱いには合格例・不合格例を付ける。
- ここでは答えない問いと所有する後続スライス:
  - 実ストアの新 root への移行の実行（同一 volume の move で ACL を保つか、別 drive への複写で ACL を再設定するか、移行済みの印の書込み）: 後続スライス「store root 移設の実行と合流」（`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §6 に入口条件付きで置く。入口条件はこのスライスの merge）。
  - LocalCache 側 copy の合流先を新 root にすること、manifest 承認、`store consolidate` の実行: 同じ後続スライス。
  - Task Scheduler の Evidence 清掃 job の root 変更と再登録: 同じ後続スライス。
  - Codex 側残置（LocalCache 側 copy）の後始末: program §6 の既存項目のまま。
  - Codex を Phase 担当として運用する際の mode 固定（full access）と host 条件の文書化: このスライスの README 更新に含めるかを A1 で決め、含めないなら program §6 へ送る。
- 判定定義: GO は進める最低条件の全件成立。NO-GO はいずれかの不成立。CONDITIONAL ACCEPT は定義しない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約（owner 決定、2026-10-18）:
  - Codex デスクトップを Claude Code と並行する Phase 担当に加える。再開条件は、Cloud 取得経路の RC3 と同じ「担当として割り当てた」事実を採用する。
  - 方針は store root の移設であり、仮想化された場所の中で動かす経路は作らない。
  - Codex 独自のサンドボックスは使わない。実行主体は同一 Windows ユーザー。
  - 資格情報の保存方式・ACL 規則・Known Folder から導く原則は変えない。
  - AGENTS.md の常時契約（`#nullable enable` は Unity 側のみ、path 予算、Artifact Storage の事前承認の範囲、`Task.Delay` / `Thread.Sleep` の禁止）を守る。
- 未決事項（A1 で決める。A0 では決め打たない）:
  - 新 root の導出方法。候補 (a) `UserProfile` Known Folder 直下の `OneStarMaker`。候補 (b) 別 drive の固定 path。候補 (c) 固定位置の小さな設定 file から読む。(a) は probe 済み、(b) は未観測、(c) は設定 file 自体の所在と ACL が要る。
  - 仮想化判定（probe）の扱い。解決した root の親に対して同じ probe を行うか、root が AppData 外なら判定を不要にするか、判定を残して root 配下で行うか。
  - 移行済みの印の形式と、旧 root に store が残っている場合の検出方法。
  - 資格情報 root の base をどう差し替えるか（`Initialize-CredentialRoot` の allowed base）。
  - `StoreConsolidation.psm1` / `StoreConsolidationPlan.psm1` の root 前提（`%LOCALAPPDATA%\OneStarMaker`）を resolver に寄せる範囲。合流先が新 root になることを前提にするか。
  - H1（`external-current-v1`）の採否。Artifacts/Harness 作業は A3 で明示する。
  - 全 EditMode 回帰の適用除外。Unity 側コードを変えないなら理由と代替証拠（offline suite 全件）を書く。

## 3. 責務マップ

A1 で file ごとに責務、変更理由、所有者・寿命、依存、公開面、テスト境界、配置理由、現在行数と予想増分を書く。A0 の入力として、tests 以外で LocalApplicationData から root を直接組んでいる file を挙げる。

| file | 現在の root の組み方 |
|---|---|
| `tools/Artifacts/ArtifactPaths.psm1` | transfers と deployments の論理 root |
| `tools/Artifacts/BuildContract.psm1` | Build store の root |
| `tools/Artifacts/BuildStore.psm1` | Build store の root |
| `tools/Artifacts/ContentStore.psm1` | Content store の root |
| `tools/Artifacts/Credentials/CredentialPathAcl.psm1` | 資格情報 root と ACL 検査の base。Known Folder から導き、repo 配下・OneDrive 配下を拒否 |
| `tools/Artifacts/EvidenceContract.psm1` | deployments の論理 root |
| `tools/Artifacts/EvidencePaths.psm1` | evidence-v2 の論理 root |
| `tools/Artifacts/EvidenceReset.psm1` | reset の対象 root |
| `tools/Artifacts/EvidenceSchedule.psm1` | runtime と credentials の root |
| `tools/Artifacts/SeriesRetirementStore.psm1` | retirements の root |
| `tools/Artifacts/StoreConsolidation.psm1` | 合流先の実ストア root |
| `tools/Harness/RecordStore.psm1` | Harness の task directory（`-StoreRoot` で差し替え可） |
| `tools/harness.ps1` | 同上の CLI 側 |
| `tools/Workflow/TaskEventStore.psm1` | Workflow の root |
| `tools/Workflow/WindowsStoreContext.psm1` | 仮想化判定の probe 位置（test 用 root hook あり） |

各 module は test 用の root hook（`TestRoot`、`LocalRootForTest`、`CredentialRootHook` 等）を既に持つ。A1 では、新しい resolver をどの module に置き、既存 hook をどう残すかを配置理由付きで書く。

## 4. 実装計画

A1 で書く。変更対象、順序、Phase B から Phase A へ差し戻す条件、対象外を維持する方法を含める。

## 5. テストとレビュー計画

A1 で書く。A0 からの入力:

- 単体テスト: 既存 suite は `tools/Artifacts/tests`、`tools/Workflow/tests`（`StoreContext.Tests.ps1` は実 LocalApplicationData で probe を行う case を持つ）、`tools/Harness/tests`。新 root 前提への付け替えと、移行前ガード（旧 root 残存・新 root 未移行）の失敗経路の case を要する。
- 操作・実行時の検証経路（候補）: full access の Codex デスクトップ内 process から新 root への書込みが実体側へ届くことを、非仮想化 process から観測する。実行担当は Codex 側 session、観測担当は非仮想化側。合否は書かず生の観測を残す。
- 判定必須テスト、起点 filter、全 EditMode 回帰の適用除外、高コスト証拠、判定 C の照合 script の方式は A1 で決め A3 で凍結する。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0 主担当: owner と Phase A 補助 session（本 revision の A0 固定）。
- A1 主担当・A2 独立レビュー・A3 統合・C' 予約: A1 以降で記録する。

## 6. Phase B 実装結果

未到達。

## 7. Phase C

未到達。

## 8. Phase C'

未到達。

## 9. Phase D

未到達。
