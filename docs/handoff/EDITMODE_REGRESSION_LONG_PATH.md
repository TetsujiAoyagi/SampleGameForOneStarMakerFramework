# 全 EditMode 回帰の 41 件の失敗と Windows の path 予算

## 0. メタデータ

- type: slice
- status: B 完了（2026-10-18。A3 revision 1 と決定記録 DR-1 に基づく）。次は発見 C
- branch: `claude/longpath-editmode-regression`
- implementation base commit: `620aff59`（origin/develop、PR #135 のマージ。2026-10-18 に `ba8245f4` から再固定した。§1 の関係する file には、その間の変更が無い）
- implementation head commit: `b54266ea`（`b54266ea5e935ef7691719cb375c528ae591fa07`）
- risk: normal（公開 API、所有者、寿命は変えない。asmdef の参照の追加は、A3 で承認した 1 本だけ。§4 の Q2）
- owner: 発注者（計画の統合）
- created: 2026-10-18
- expires: 2026-11-17
- harvest to: `AGENTS.md`（path 予算の常時契約。数値は書かず、正本の file を指す）、`tools/Harness/README.md`（run-tests の起動前検査）
- Phase A snapshot の取得案内・状態: 未保存。A3 の凍結 commit（この文書を変えた最後の commit）の本文で固定した。`evidence stage` への保存は、判定 C の bundle を作るときに行う。
- Phase B result: この HANDOFF の「Phase B 実装結果」の節（tracked）。evidence bundle / C' blind bundle: 未生成
- 引継ぎ票:
  - 次の担当: 発見 C（動作確認は対象なし。§5）。
  - 引き継ぐ commit: implementation head `b54266ea` の上に、この HANDOFF の B 記録だけを足した commit（`git log -1 -- docs/handoff/EDITMODE_REGRESSION_LONG_PATH.md` で照合する）。レビュー対象の差分は `620aff59..b54266ea`。作業ツリーは clean であること。
  - 必要な権限: 発見 C は差分の読み取りと offline の suite。差し戻し中の Unity テストは §5 の起点 filter で、sandbox の外の承認済み経路から実行する。
  - 停止規則: §2 の停止規則と、§4 の「Phase B から A へ戻す条件」。
  - 引き渡さないもの: なし（発見 C の所見はまだ無い）。B の判断の詳細は「Phase B 実装結果」と「決定記録」の節にある。

## 1. 目的と対象外

- 目的:
  - 全 EditMode 回帰の 41 件のうち、path に起因する 39 件と、テストの前提が足りない 1 件を直す。残る OOM の 1 件は、受け入れ手順の中で扱う。
  - 同じ種類の path の超過を、環境に依存しない予算で事前に検出する最小限の契約を作る。
- 対象外:
  - 特定の場所への junction や配置の指定など、ローカル環境に依存する回避策（§4 の不採用の案）。
  - OS のレジストリ、ユーザー / マシンの環境変数、Unity のインストールと package の変更。
  - long path に対応した IO の helper（Win32 `CreateFileW`）の実装。`File.*` の全面禁止。Editor の IO の一括置換。
  - PlayMode / Player / runtime の IO の path 予算。
  - OOM の恒久策。既存テストが環境を分離していない問題の修正（後続の `TEST_ENV_ISOLATION`）。
  - 「どんなに長い path でも動く」ことの保証。このスライスは「予算内なら動き、予算外なら起動前に止まる」だけを保証する。
- 現況（2026-10-18 に確認）:
  - OS の `LongPathsEnabled` は既に 1。git の `core.longpaths` は true。
  - Unity native は 260 文字を超える path を扱える。主 checkout の `unity/Library` の最長 file は、絶対 path で約 265 文字。
  - Unity 同梱の Mono（Editor の C# と同じ BCL）を単体で動かした spike の結果:
    - `File.Exists` / `Directory.*` / `File.Delete` は 260 文字を超えても動く。
    - `File.OpenRead` / `ReadAllText` / `WriteAllText` / `Copy` / `Move` / `FileInfo.Length` は失敗する。
    - `\\?\` 接頭辞は Mono が `C:\?\...` に書き換えるので使えない。
    - P/Invoke の `CreateFileW` + `FileStream(SafeFileHandle)` なら 326 文字でも動く（§4 の規則 3 の根拠）。
  - repo 相対 path の最長（tracked、`620aff59`）:
    - NuGet の展開物（`unity/Assets/Packages/`）は 178 文字。
    - それ以外は 138 文字（`.../Seasons/Winter/Cells/Winter_Cell_8_5/Winter_Environment_8_5/Winter_Environment_8_5.unity.meta`）。
  - 生成器 `WorldCompanionCreationPlan.Create`:
    - 入力は `WorldWorkspaceSelection`（季節 4 種、X = 0〜8、Y = 0〜5 をコンストラクタが検証する）。
    - 役割は Lighting / VFX / Events の 3 種。
    - path は scene、`.asset` の SceneResource、node、map と、それぞれの `.meta`。
    - 回復 journal は `Library/OneStarMaker/WorldWorkspace/pending-companion-create.json` と、その一時 file。
  - checkout のルートの長さ（末尾の区切りを含む絶対 path）:
    - 主 checkout: 57 文字。
    - Claude の worktree: 76 + 名前の文字数（この worktree は 98 文字）。
    - Codex の worktree: 67 + 名前の文字数。
  - `run-tests.ps1` の出力（`New-UnityTestOutput`）は `<OutputRoot>/<GUID>/` の下に置く。既定の OutputRoot は `<repo>/TestResults`。
    - Unity native が書く: `results.xml`、`unity.log`。
    - Unity 内の Mono（観測の writer）が書く: `observation*.json` と、その一時 file `<path>.<32 桁>.tmp`。
    - pwsh 7 が書く: `step.json`（.NET の long path に対応している）。
    - `Get-UnityObservationArgumentPath` は、一時 file が 260 文字以上になると、観測の引数だけを `\\?\` 形式にする。spike によれば、この形式は Mono の `File.*` で `C:\?\` に書き換わる可能性がある（未検証。§6 の C-3 で後続へ送る）。

### 失敗 41 件の一次原因

S-4d1 の判定 C の XML（`.claude/worktrees/s-4d1-vfx-abstraction/TestResults`）で確認した。その worktree は調査中に削除されたので、生の証拠は残っていない。原因の再現は、判定 C で base の commit に対して取り直す（§5）。

| 群 | 件数 | 一次原因 |
| --- | --- | --- |
| `WorldCompanionCreationTransactionTests` / `WorldCompanionRecoveryTests` | 39 | テストの fixture の path が、repo 相対で 164 文字ある。ルートの 97 文字と合わせると 261 文字になり、`WorldCompanionOwnershipProof.HashFile` の `File.OpenRead` が Mono で失敗する。本番の命名より 26 文字長い。 |
| `ObservationReloadTests.RealDomainReloadRoundTrip` | 1 | Framework のテストが Play に入ると、SampleGame の `AppInitializer.Before` が走る。Editor Play は、`content:runtimeMode` も verified pair も無いと fail-closed になる（a77cbaf4、2026-09-20）。このテストは、その後（1c176bf4、2026-10-01）に追加され、content mode を設定しない。path とは関係が無い。 |
| `BuildContentDirectoryIntegrationTests.ScenePrefabTextureAndTwoRepresentationsBuildAndMoveAsOneDirectory` | 1 | URP/Lit のシェーダコンパイルで `out of memory`（物理メモリ 16 GB）。別の全件 run と単独の実行では出ていない。環境の可能性が高いが、未確定。 |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 環境に依存しない path 予算と起動前検査で、2 つの基準 checkout の全 EditMode 回帰を failed 0 にし、予算外の実行を Unity の起動前に理由付きで止められるか。
- 用語:
  - **長さ**: .NET の `string.Length`（UTF-16 の code unit の数）。区切りは、repo 相対では `/`、絶対では OS の区切り。
  - **ルートの長さ**: `[IO.Path]::GetFullPath(<repo root>)` から末尾の区切りを除いた長さ + 1。
  - **上限**: 絶対 path は 259 文字以内（MAX_PATH の 260 は終端の NUL を含むため）。repo 相対 path は R 以内。
  - **管理対象**: (1) git の tracked と、未追跡で ignore されていない file、(2) 対象生成器（`WorldCompanionCreationPlan` と回復 journal）が作る path、(3) run-tests が Unity に書かせる出力の path。`Library/` など ignore 対象の生成物、vendor の allowlist、任意の動的な IO、Unity native の内部のアクセスは管理対象に入らない。
- 進める最低条件:
  - 不変条件:
    - I1: 予算に適合した tree（起動の時点で管理対象の (1) が R 以内）では、管理対象の IO の成否がルートの長さで変わらない。
    - I2: run-tests は、単独で呼ばれた場合も、起動の時点の予算違反（予算の正本の欠落や不正、ルートの超過、管理対象 (1) の超過、出力 path の超過）で Unity を起動しない。
    - I3: fail-closed の契約（Editor Play の `content:runtimeMode` 必須、`WorldCompanionOwnershipProof` の所有の証明、予算の正本の検証）を緩めない。
    - I4: このスライスの差分と実行手順は、OS / レジストリ / Unity のインストール / User・Machine スコープの環境変数を変えない。既存テストの副作用は I4 の対象外とし、§5 で区別して記録する。
    - I5: R と allowlist の正本は `tools/path-budget.json` の 1 箇所だけ。
    - I6: Framework（`OneStarMaker.*`）のコードとテストに、SampleGame 固有の名前（`SAMPLEGAME_` など）を書かない。`ObservationReloadTests.RealDomainReloadRoundTrip` の完全修飾名を変えない。
  - 成立条件:
    - S1: 深い worktree（ルート 98 文字）と短いルートの checkout（Q4）で、同じ implementation commit・clean な tree の全 EditMode 回帰の XML が、failed 0、total 1 以上、対象の 40 件が Passed。
    - S2: contract-audit の検査 9 が、管理対象 (1) の R の超過を検出する。
    - S3: run-tests の起動前検査が、I2 の各違反で、Unity を起動せずに `path-budget` の失敗の step を残す。
    - S4: Editor のテストが、対象生成器の全出力 path の最大値が R 以内であることを確認する。
    - S5: base の commit の深い worktree では、起点 filter の対象 39 件が path の例外で失敗する（因果の再現。元の生の証拠が失われたことの代替）。
- 受け入れ条件:
  - A1（S1, I1）: 2 つの XML で、対象の 40 件（`WorldCompanionCreationTransactionTests` 13 件、`WorldCompanionRecoveryTests` 26 件、`ObservationReloadTests.RealDomainReloadRoundTrip`）を完全修飾名で照合し、すべて Passed。2 つの run の leaf の集合と件数が一致する。`failed=0` だけで、対象が未収録や skip のまま成功とは認めない。
  - A2（S3, I2, 失敗経路）: run-tests の offline テストで、process の起動の seam の呼び出しが 0 回になることを確かめる。ケースは次のとおり。
    - ルート + R = 259 は通る。260 は失敗する。
    - ルートは適合し、R + 1 文字の未追跡 file がある tree は失敗する。
    - ルートは適合し、`-OutputRoot` だけが深く、出力 path が 260 文字になる場合は失敗する。259 は通る。`-ObserveUnity` では一時 file の長さで判定する。
    - 予算の正本の欠落・不正値は失敗する。
    - 空白を含む正常な出力先は、従来どおり成功する。
  - A3（S2, I5, 失敗経路）: contract-audit の offline テスト（一時 fixture にも `tools/path-budget.json` を置く）で次を確かめる。
    - R ちょうどは通る。R + 1 は検出する。
    - allowlist の中は検出しない。
    - 次の JSON は fail-closed になる（検査を飛ばして成功にしない）: 欠落、構文の不正、R が整数でない・1 未満・258 を超える、過剰な allowlist の prefix（`unity/` や `unity/Assets/` だけ、`..` を含む、末尾が `/` でない、`unity/Assets/` で始まらない）。
  - A4（S4）: `WorldWorkspaceSelection` のコンストラクタが受理する全ての組（範囲は、X・Y を 0〜99 で試して受理されたものから導出する。範囲の定数を複製しない）について、`WorldCompanionCreationPlan.Create` が返す scene / resource / node / map の path を集める。fixture については `CreateForTests` が実際に返す path を集める。回復 journal の path も含める。それぞれの `.meta` と `unity/` を加えた repo 相対の最大値が、R 以内であることを確認する。fixture の名前を定数で複製しない。保証するのは、列挙した対象生成器だけとする。
  - A5（I3, I6）: Q2 の裁定どおりに `ObservationReloadTests` が単独と全件の両方で Passed になる。完全修飾名は変わらない。content mode は Process スコープだけに設定し、テストの終了時に元の値（未設定を含む）へ戻す。`AbstractApplicationInitializer` の fail-closed の分岐と `InvalidCompanionConfigStartupTests` は差分に入らない。EditorSettings の復旧の仕組みを、content mode の復旧の証明に使わない。不合格の例: `LogAssert.ignoreFailingMessages` を使う、Framework のテストに `SAMPLEGAME_` を書く、Framework の本番 API を増やす。
  - A6（I4）: 差分と §5 の手順に、レジストリ、`setx`、`EnvironmentVariableTarget.User` / `Machine` への書き込み、Unity のインストール先への書き込みが無い。
  - A7（I5）: R と allowlist の数値・prefix は `tools/path-budget.json` だけにある。contract-audit、run-tests、Editor のテストはそこから読む。`AGENTS.md`、README、この HANDOFF の規則の節は、file を指すだけにする（§3 の R の値の案は A3 の裁定の記録として扱う）。
- ここでは答えない問い:
  - runtime / Player の IO の path 予算 → `RUNTIME_PATH_BUDGET`
  - 既存テストの User スコープの環境変数の書き換え → `TEST_ENV_ISOLATION`
  - 観測の引数の `\\?\` の分岐が Mono で働くか → `OBSERVATION_LONG_PATH_BRANCH`
  - シェーダコンパイルの OOM の恒久策 → 再現した場合だけ別の課題にする
  - 長さを抑えられない path を将来作る場合の helper → その経路を作るスライス
- 判定定義: S1〜S5 が揃い、I1〜I6 に反証が無ければ GO。
- 停止規則: 標準どおり。OOM が判定 C の全件 run で再現したら、同じ条件で 1 回だけ再実行する。それでも再現したら停止し、OOM を別の課題として発注者へ返す。
- A3 後の例外承認: なし
- 決定記録: DR-1（Q2-1 の派生型の数え方。版 1、2026-10-18 に Phase B の中で適用。末尾の「決定記録」の節）
- 本文へ転記した実装制約:
  - `#nullable enable`
  - `record` の禁止
  - `Task.Delay` / `Thread.Sleep` の禁止
  - asmdef の参照の追加は、A3 で承認した 1 本（`OneStarMaker.Tests.TestObservation.Editor` → `OneStarMaker.Runtime`）以外は停止する。IVT の追加も停止の対象。
  - 偽 null の判定は `== null`
- 未決事項: なし（Q2 と Q4 は 2026-10-18 に発注者が裁定した）

## 3. 責務マップ

### 今回必須

| file | 責務と変更理由 | 依存・テスト境界 | 増分の見込み |
| --- | --- | --- | --- |
| `tools/path-budget.json`（新規） | R、vendor の allowlist、`schemaVersion` の正本 | 読むのは下の module だけ | 10 行前後 |
| `tools/Harness/PathBudget.psm1`（新規） | 予算の読み込みと検証（fail-closed）、管理対象 (1) の列挙（git の tracked + 未追跡の非 ignore）と超過の判定、ルートの判定、絶対 path の判定。状態を持たない関数だけを置く | 入力は `-Root` と path の列。git と file system 以外には依存しない。offline でテストできる | 100〜140 行 |
| `tools/contract-audit.ps1` | 検査 9: module で予算を読み、管理対象 (1) の違反を列挙する。ほかの検査とは独立して実行する | module を import する | +20〜30 行 |
| `tools/contract-audit.tests.ps1` | `New-AuditFixture` が `tools/path-budget.json` を置く。検査 9 の正常・境界・JSON の不正・過剰な allowlist | 一時 Git fixture | +60〜80 行 |
| `tools/run-tests.ps1` | 起動前検査: 出力の作成 → 予算の読み込み → ルート → 管理対象 (1) → 出力 path（XML、log、step、`-ObserveUnity` では観測の file とその一時 file）。違反は `path-budget` の failed step にして、Unity を起動しない | module を import する。process の起動は既存の seam | +30〜40 行 |
| `tools/Harness/tests/UnityTestRunner.Tests.ps1` | A2 の各ケース（起動の seam の呼び出しが 0 回） | 既存の offline の形 | +60〜90 行 |
| `unity/Assets/SampleGame/Tests/Editor/WorldAuthoring/WorldCompanionCreationTransactionTests.cs` | fixture の path を短くする。root の名前と `S4aTest{token}` の繰り返しを縮め、token の一意性は保つ | — | ±10 行 |
| `unity/Assets/SampleGame/Tests/Editor/WorldAuthoring/WorldCompanionPathBudgetTests.cs`（新規） | A4。`tools/path-budget.json` の R を読む（Editor の外の file の読み取りだけ。asmdef は変えない） | `SampleGame.Tests.Editor` の既存の参照で足りる（`WorldCompanionCreationPlan` は internal で、IVT は既存） | 60〜90 行 |
| `ObservationReloadTests.cs` と関係する asmdef | Q2 の裁定に従う | Q2 を参照 | ±20 行 |
| `AGENTS.md` | 常時契約に 1 行: path は `tools/path-budget.json` の予算に従う。置き場所は指定しない | — | +1 行 |
| `tools/Harness/README.md` | 起動前検査の保証範囲と、保証しないもの | — | 文書 |

合計は約 450〜600 行で、半分以上を offline テストが占める見込み。新しい module を分ける理由: contract-audit（静的、全体）と run-tests（起動前、単独）の 2 つの呼び出し元が同じ判定を使うので、判定を 1 箇所に置いて二重の実装を避ける（I5、§6 の W-1）。

### 後続（このスライスでは実装しない）

`RUNTIME_PATH_BUDGET`、`TEST_ENV_ISOLATION`、`OBSERVATION_LONG_PATH_BRANCH`、OOM（再現した場合）。

### R の値（A3 で裁定。正本は `tools/path-budget.json` に置き、この節は裁定の記録として残す）

- 採用: R = 150（repo 相対、`unity/` を含む）。ルートの上限は 259 − 150 = 109 文字になる。
- 余裕:
  - 本番の最長 138 文字に対して 12 文字の余裕がある。
  - 名前の上限: Claude の worktree は 33 文字まで、Codex の worktree は 42 文字まで。主 checkout は 52 文字の余裕がある。
  - 既定の出力の一時 file は、ルート + 111 文字以内（`TestResults/` + GUID + `observation-progress.json` + 37）。ルートが 109 文字なら 220 文字で、上限に収まる。

## 4. 実装計画と規則

### path 予算の 3 つの規則（発注者と合意済み）

1. **管理対象 (1) の repo 相対 path は R 以内。** contract-audit の検査 9 と run-tests の起動前検査が、同じ module で判定する。例外は vendor の allowlist（NuGet の展開物）だけ。これは Unity native が import し、自前のコードは開かない。
2. **checkout のルートは 259 − R 以内。** 置き場所は指定しない。run-tests が起動前に検査する。
3. **長さを R に収められない経路だけ、long path に対応した IO を使う。** いまのコードにはその経路が無いので、このスライスでは作らない。

### run-tests が単独で保証すること / しないこと

- 保証する: 起動の時点の tree で、予算の正本が正しいこと、ルートが上限以内であること、管理対象 (1) が R 以内であること、Unity が書く出力の path が 259 文字以内であること。違反があれば Unity を起動しない。
- 保証しない:
  - 起動した後の tree の変更。
  - ignore 対象の生成物（`Library/` など）。
  - テストが動的に作る path（対象生成器は A4 で別に保証する）。
  - Unity native の内部のアクセス。
- contract-audit は、同じ判定を push 前・判定 C 前の静的な検査として実行する。どちらか一方が欠けても、もう一方の保証は弱まらない。

### Q2: `ObservationReloadTests`（A3 の裁定: Q2-1 を採用。2026-10-18、発注者）

調査の結果（静的。2026-10-18）、既存の契約だけでは (a) が成り立たない。

- テストの asmdef（`OneStarMaker.Tests.TestObservation.Editor`）が参照するのは `OneStarMaker.Editor.TestObservation` だけで、`OneStarMaker.Runtime` を参照しない。
- config の注入口は 2 つしかない。Game が決める環境変数の prefix（`AbstractApplicationInitializer.GetEnvironmentVariablePrefix` は protected）と、プロセスの argv（`CommandLineConfigProvider`）。argv はテスト中に変えられない。
- `TestObservationBootstrap` と `run-tests.ps1:222` は、このテストの完全修飾名を定数で持つ。`TestObservationBootstrap` は internal で、IVT は `OneStarMaker.Tests.TestObservation.Editor` だけに与えている。

選択肢:

- **Q2-1（採用）: Framework のテストのまま、Game が宣言した prefix を Framework の型から発見する。**
  - テストの asmdef に、`OneStarMaker.Runtime` への参照を 1 本足す（Framework → Framework。方向の違反は無い）。
  - テストは `AbstractApplicationInitializer` の派生型を `TypeCache` で探す。
    - 派生型が 0 個なら、bootstrap が動かないので何も設定しない。
    - 1 個なら、その型の protected の `GetEnvironmentVariablePrefix` を reflection で読み、`<prefix>CONTENT__RUNTIMEMODE=addressables` を Process スコープに設定して、終了時に戻す。`__` → `:` は Framework の `EnvironmentVariableConfigProvider` の規則。
    - 2 個以上なら、テストを失敗させる。
  - 利点: fixture が Framework に残り、完全修飾名も IVT も変わらない。`SAMPLEGAME_` を書かない。本番 API を増やさない。
  - 欠点: テストが protected の実装に reflection で依存する。Process の環境変数は Domain Reload を越えて残るので、復元は `finally` と `TearDown` の両方で行う。復元できなかった場合も、値は Unity のプロセスの終了で消える（Process スコープ）。
- **Q2-2（不採用）: テストを `SampleGame.Tests.Editor` へ移し、namespace と class 名は保つ。**
  - 完全修飾名は保たれる。
  - `SampleGame.Tests.Editor` から `OneStarMaker.Editor.TestObservation` への参照の追加と、Framework の `AssemblyInfo` への `InternalsVisibleTo("SampleGame.Tests.Editor")` の追加が要る。Runtime の AssemblyInfo には同じ前例がある。
  - 欠点: Framework の観測の契約（`RecoveryExpected` の固定名）が、Game の中にある fixture に依存する。Framework だけを使う利用者には fixture が無くなる。namespace と配置の所有者も食い違う。
- **不採用 Q2-3: Framework に、テスト用の config の注入口を足す。** 1 テストのための本番 API の追加になる。
- **不採用 Q2-4: runner が argv に `-content:runtimeMode=addressables` を付ける。** 全件の run で、未設定時の fail-closed を確かめる `InvalidCompanionConfigStartupTests` の前提を壊す。テストからは argv を消せない。

Phase B が Q2-1 の成立を確かめられなければ（派生型の発見、reflection、Play の移行時に値が有効になること）、Q2-2 や API の追加へ自動的に切り替えず、A に戻す。Play の移行時に値が有効になることの最終的な確認は、Phase C の起点 filter で行う。

### 順序

1. `path-budget.json` と `PathBudget.psm1` → contract-audit の検査 9 とテスト → run-tests の起動前検査とテスト
2. fixture の短縮 → A4 のテスト → Q2
3. `AGENTS.md` と README

Phase B から A へ戻す条件:

- A3 で承認した以外の asmdef の参照や IVT が必要になった場合。
- Q2 の方式が成り立たなかった場合。
- fixture を短くしても `WorldCompanion*` が path 以外の理由で失敗した場合（スパイクとして原因を確認する）。
- vendor 以外で R に収まらない tracked の path が見つかった場合。

## 5. テストとレビュー計画

- 差し戻し中の起点 `-Filter`: `WorldCompanion;ObservationReloadTests`（XML で対象の 40 件の収録を確認する）。offline の `tools/contract-audit.tests.ps1` と `tools/Harness/tests/UnityTestRunner.Tests.ps1`。
- 判定 C の手順（GO 候補 head、同じ commit、clean な tree）:
  1. 事前確認:
     - Unity が動いていないこと、project の lock、Editor の状態。
     - User と Machine のスコープに `SAMPLEGAME_*` が無いこと。結果を記録する。既存テストの副作用（`BuildContentDirectoryIntegrationTests` が User スコープを一時的に消して戻す）は、このスライスの手順と区別して記録する。
     - 2 つの checkout の HEAD と `git status` が同じであること。
  2. `pwsh tools/contract-audit.ps1`（検査 9 を含む）と offline の 2 つの suite。
  3. S5: 深い worktree で base の commit を checkout し、起点 filter を 1 回実行する。対象の 39 件が path の例外で失敗した XML を残す。
  4. 全 EditMode 回帰（空 filter）を、深い worktree と Q4 の checkout で 1 回ずつ実行する。どちらも sandbox の外で、ほかの Unity が動いていない状態で行う。
  5. A1 の照合: 対象の 40 件の完全修飾名と、2 つの run の leaf の集合・件数。
  6. OOM が出たら、§2 の停止規則に従う。
- **Q4: 短いルートで判定 C を実行する方法（A3 の裁定: 主 checkout を一時的に切り替える。2026-10-18、発注者）**:
  - 判定 C の直前に、主 checkout を GO 候補の commit へ detached で切り替える。全件 run の後に、元の branch（確認時点では develop）へ戻す。
  - 切り替えの前に、主 checkout の branch、HEAD、`git status`、Unity Editor と lock の状態を記録する。tracked の変更があれば切り替えずに停止し、発注者へ返す。未追跡の file（ほかの session のもの）には触れない。
  - 未追跡の file は管理対象 (1) に入るので、R を超えるものがあれば起動前検査で止まる。その場合は、その file を動かさずに停止し、発注者へ返す（§6 の C-5）。
  - 切り替えの前に、実行の時点で発注者に一言確認する。この裁定は方式の承認であり、その日の主 checkout の状態を確かめないまま操作してよいという意味ではない。
  - 不採用: ルートが短い別の worktree をその時だけ作る案。「ここに作れ」に近くなる。
- 全 EditMode 回帰の適用除外: なし
- 操作・実行時・目視の条件の検証経路: 対象なし（すべてテストで閉じる）
- 人間の判断が必要な条件: Q4 の主 checkout の切り替えを、実行の時点で一言確認することだけ
- 高コスト証拠: なし
- 判定 C の照合 script: なし。手順 5 の照合は、判定 C の担当が XML から機械的に行い、照合の方法を結果に記録する。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`
- A0/A1 主担当: Claude Code（Anthropic）
- A2 の入力版: 内容の sha256 は `76b401703ecb590696f94b399fb7d7fa80383d74db6fb5da5477c7bf3605efd3`。commit は `ca8f98b4`（base の再固定に伴い、同じ内容の `eb3f3d38` に置き換えた）。
- A2 独立レビュー:
  - R1: アーキテクチャゲート。Codex CLI 0.159.2（OpenAI、model 表示 gpt-6-astra）。read-only の sandbox で 1 回実行した（2026-10-18）。所見は 4 件。
  - R2: 計画の全体のレビュー。WebGPT（OpenAI）。発注者が依頼した。入力は A2 入力版の固定前の本文で、R1 の所見を知ったうえでのレビューなので、R1 とは独立していない。所見は 6 件と、判定 C の 5 点。
- A3 統合担当: 主担当と発注者。2026-10-18 に凍結した。採否は §6 のとおり確定。Q2 は Q2-1、Q4 は主 checkout の一時的な切り替え、R は 150 を採用した。
- C' 用に予約した担当: 未割当（A3 で予約しなかった）。C' の開始時に、B / C と異なるモデル系列から選ぶ。R1 と R2 はどちらも OpenAI 系なので、独立性のために、Phase A に関与していない系列か人間を優先する。

## 6. A2 所見の採否（A3 で確定。2026-10-18）

所見の根拠の箇所は、主担当がコードで実在を確認した。

| # | 所見 | 分類 | 採否 | 反映先 |
| --- | --- | --- | --- | --- |
| R1-1 | 全件回帰の既存テストが User スコープの環境変数を書き換えるので、I4 と矛盾する | blocker | 採用。I4 の範囲を、このスライスの差分と手順に限る。事前確認で User と Machine に `SAMPLEGAME_*` が無いことを記録する。副作用の修正は `TEST_ENV_ISOLATION` へ送る | I4、§5 の手順 1 |
| R1-2 / R2-1 | contract-audit と run-tests の保証がつながっていない | should / P1 | 採用（R2 の推奨 1）。判定を `PathBudget.psm1` に共通化し、run-tests が単独で、起動の時点の管理対象 (1) を検査する。保証する範囲としない範囲を §4 に書く | I1、I2、S3、A2、§3、§4 |
| R2-2 | 出力 path が予算の対象外 | P1 | 採用。出力の作成後・起動前に、XML / log / step と、`-ObserveUnity` の観測の file・一時 file を 259 文字以内で検査する。step は pwsh が書くので、理由付きの失敗 step を残せる | I2、A2、§3 |
| R1-3 / R2-4 | A4 は、実際に生成される全 path の最大値で判定する | should / P2 | 採用。受理される組を列挙し、`.meta` と `unity/` を含め、journal も加える。定数を複製しない。保証は対象生成器に限る | S4、A4 |
| R1-4 / R2-3 | Q2 の方式を B で確かめるとしたままにしない。(b) は完全修飾名に依存する契約を壊す | should / P1 | 採用。静的な調査で、既存の契約だけでは (a) が成り立たないことを確定した。Q2-1 / Q2-2 を発注者の裁定に回す。完全修飾名の維持と `SAMPLEGAME_` の禁止を I6 にする | I6、A5、§4 の Q2 |
| R2-5 | `path-budget.json` の所在、破損時の動作、offline fixture | P2 | 採用。`-Root` の `tools/path-budget.json` を読む。一時 fixture にも置く。欠落・不正・過剰な allowlist は fail-closed にする。長さの数え方を用語として定義する | §2 の用語、A3 |
| R2-6 | I1 の保証範囲が強すぎる | P2 | 採用。管理対象を定義し、I1 を「予算に適合した tree の管理対象の IO」に限る。全件 failed 0 は S1 の実測で別に受け入れる | §2 の用語、I1、S1 |
| R2-C1 | 2 つの run を同じ commit・tree で比べ、leaf の集合を照合する | P2 | 採用 | S1、A1、§5 |
| R2-C2 | 対象の 40 件を XML で明示的に Passed と確かめる | P2 | 採用 | A1 |
| R2-C3 | 全件の前に、出力 path と起動前検査を確かめる | P2 | 採用（起動前検査は run-tests の中で毎回走る）。lock の検査は従来どおり | §5 の手順 1・2 |
| R2-C4 | OOM は 1 回だけ再試行し、恒久策は混ぜない | P2 | 採用（現計画どおり） | 停止規則 |
| R2-C5 | R1-1 を忘れない | P2 | 採用（R1-1 と同じ） | §5 の手順 1 |
| R2-base | base を最新の develop に再固定する | — | 採用。`620aff59` に再固定した。関係する file に変更は無かった | §0 |

### 主担当による C / C' の観点の自己点検（追加の所見）

| # | 内容 | 扱い |
| --- | --- | --- |
| C-1 | 元の失敗の生の証拠が、worktree の削除で失われた。C' から「原因が path であることの一次証拠が無い」と突かれうる | S5 を追加する（base で起点 filter を再現する） |
| C-2 | 判定 C を主 checkout で実行するには、利用者の作業の場の branch を切り替える必要がある | Q4 として発注者の裁定に回す |
| C-3 | 既存の `Get-UnityObservationArgumentPath` の `\\?\` の分岐は、Mono で働かない可能性がある。起動前検査が一時 file を 259 文字以内に制限すれば、予算に適合した run ではこの分岐に到達しない | 既存のコードは変えない。分岐の実効性は `OBSERVATION_LONG_PATH_BRANCH` へ送る |
| C-4 | allowlist を広げれば検査を骨抜きにできる | A3 の過剰な allowlist の拒否で防ぐ |
| C-5 | 未追跡の file（ほかの session のものを含む）も管理対象 (1) に入るので、run-tests が失敗しうる | 意図した挙動とする。失敗のメッセージに、超過した path と長さを出す |
| C-6 | R2 は R1 の所見を知ったうえでのレビューで、独立していない | §5 に記録した。C' の担当の系列の選び方に反映する |

## Phase B 実装結果

この HANDOFF の §6 は A2 の採否に使っているので、テンプレートの §6 の内容をこの節に置く。§7 と §8 は Phase C と C' のために空けておく。

- 実装（base `620aff59` からの差分。§3 の責務マップどおり）:
  - `tools/path-budget.json`: R = 150、allowlist は `unity/Assets/Packages/` だけ。
  - `tools/Harness/PathBudget.psm1`: 予算の読み込みと検証、ルートの判定、管理対象 (1) の列挙（`git ls-files --cached --others --exclude-standard -z` を UTF-8 で読む）、超過の判定、起動前検査の組み立て。ルートが git の最上位でない場合と、git が失敗した場合も違反にする。
  - `tools/contract-audit.ps1`: 検査 9。`AUDIT_RESULT` は `checks=...,9` になる。Harness の解析（`checks=([0-9,]+)`）はそのまま通る。
  - `tools/run-tests.ps1`: 出力の作成の後、Unity の起動前に検査する。違反は `name=path-budget`、`kind=preflight` の failed step と marker にする。
  - fixture の短縮: root を `Assets/__WCT_{token}`、identity を `T{token}_Cell` / `T{token}_Events` にした（token は 12 桁のまま）。最長の repo 相対 path は 164 文字から 127 文字になった。path の組み立ては `WorldCompanionTestScope.CreateFixturePlan` に置き、A4 のテストも同じものを使う。
  - `WorldCompanionPathBudgetTests`（A4）: 座標は 0〜99 の格子を 1 組で探り、受理された座標を全ての季節・役割・payload と掛け合わせて、constructor と `Create` にもう一度判定させる（全組で格子を探ると、拒否の例外が約 40 万回になるため）。journal は `JournalPath` と、`Save` が並べる `.tmp` / `.bak` を含める。
  - Q2: `OneStarMaker.Tests.TestObservation.Editor` の asmdef に `OneStarMaker.Runtime` を 1 本足した。DR-1 の基準で initializer を探し、prefix は初期化しない instance で読む。`<prefix>CONTENT__RUNTIMEMODE=addressables` を Process スコープに設定し、元の値は `SessionState` に置いて、`finally` と `TearDown` の両方で戻す。完全修飾名、`AbstractApplicationInitializer`、`InvalidCompanionConfigStartupTests` は変えていない。
  - `AGENTS.md` に常時契約を 1 行、`tools/Harness/README.md` に「起動前の path 予算検査」を足した。
- HANDOFF との差:
  - B 適応: `tools/Harness/tests/UnityObservation.Tests.ps1` の runner の fixture を git の repo にして、予算の file を置いた（責務マップに無い file）。起動前検査で既存の runner のテストが失敗しないようにするためで、検査の内容は変えていない。
  - 新しい事実: `AbstractApplicationInitializer` の派生型は 3 つある（`AppInitializer`、`PlayerContentConfigurationTests.StageHarness`、`InvalidCompanionConfigStartupTests.TestInitializer`）。凍結どおりの数え方では Q2-1 が成立しないので停止し、DR-1 の裁定を受けた。
- B の出口での確認:
  - offline: `tools/contract-audit.tests.ps1` 29 PASS、`tools/Harness/tests/UnityTestRunner.Tests.ps1` 31 PASS（path 予算の 6 件を含む）、`UnityObservation.Tests.ps1` 16 PASS、`UnityGate.Tests.ps1` 21 PASS、`Harness.Tests.ps1` 35 PASS。
  - `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1`: errors 0。
  - Editor のコンパイル: この worktree（`Library/` が無い状態）で、`Unity.exe -batchmode -nographics -quit -projectPath unity` を sandbox の外で 1 回実行した（テストは無し）。exit 0、`error CS` は 0 件。`OneStarMaker.Tests.TestObservation.Editor.dll` と `SampleGame.Tests.Editor.dll` が生成された。
  - 初回 import の副作用: Unity が `unity/Assets/MobileDependencyResolver/Editor/**/*.pdb.meta` の 5 file を消し、`unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` の `m_RuntimeSettings` を書き換えた。このスライスとは関係が無いので、`git checkout --` で元に戻した。判定 C で新しい checkout を初めて開くときも、同じ変更が出うる。
- 未実行: Unity のテスト（Phase C の責任）。A1、A4、A5 と、S1、S5 は判定 C で確かめる。
- implementation head commit: §0 の値。
- Phase B 担当・モデル・ベンダー: Claude Code（Anthropic、Claude Opus 5.5）。

### 動作確認

- 対象なし（§5 のとおり、すべてテストで閉じる）。

## 決定記録

### DR-1: Q2-1 で数える派生型（版 1、2026-10-18、Phase B の中で Q2 の実装の前に適用）

- 裁定者: 発注者（「決定記録で承認」）。Phase A は再開しない。§4 の本文は書き換えない。
- 内容: 数える対象は、`AbstractApplicationInitializer` の派生型のうち、`[RuntimeInitializeOnLoadMethod]` を持つ static method を自身で宣言する型（DeclaredOnly）に限る。0 個・1 個・2 個以上の扱いは凍結どおり。
- 理由: Play で bootstrap を実際に起動するのはその型だけ。テストの中の private な派生型（`StageHarness`、`TestInitializer`）は起動しない。A5 と I6 の意味と合否は変わらない。
- 残るリスク: 将来の Game が `[RuntimeInitializeOnLoadMethod]` を initializer とは別の class に置くと 0 個になり、テストは何も設定しない。その場合は Game の fail-closed のログでテストが失敗するので、黙って通ることはない。
