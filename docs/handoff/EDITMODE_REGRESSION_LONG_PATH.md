# 全 EditMode 回帰の 41 件の失敗と Windows の path 予算

## 0. メタデータ

- type: slice
- status: A（A1。方針は発注者と合意済み。A2 入力版の固定前）
- branch: `claude/longpath-editmode-regression`
- implementation base commit: `ba8245f4`（origin/develop、PR #134 のマージ）
- implementation head commit: 未実装
- risk: normal（公開 API、asmdef、所有者、寿命は変えない）
- owner: 発注者（計画の統合）
- created: 2026-10-18
- expires: 2026-11-17
- harvest to: `AGENTS.md`（path 予算の常時契約。数値は書かず、正本の file を指す）、`tools/Harness/README.md`（run-tests の事前検査）
- Phase A snapshot の取得案内・状態: 未生成
- Phase B result / evidence bundle / C' blind bundle: 未生成
- 引継ぎ票: 次は A2。入力版はこの文書の commit で固定する。

## 1. 目的と対象外

- 目的: 全 EditMode 回帰（`pwsh tools/run-tests.ps1` の空 filter）を、主 checkout と深い worktree の両方で failed 0 にする。Windows の MAX_PATH による失敗を、環境に依存しない規則（path 予算）で防ぎ、予算を超えた checkout では原因の分かる形で即座に止める。
- 対象外:
  - 特定の場所への junction や配置の指定など、ローカル環境に依存する回避策。発注者の方針により不採用（§4）。
  - OS のレジストリ（LongPathsEnabled）などシステムの設定の変更。Unity 本体と package の改変。
  - long path に対応した IO の helper の実装。いまのコードには、長さを抑えられない経路が無い（§1 の現況）。必要になったときの扱いは、規則として §4 に書くだけにする。
  - PlayMode と Player の回帰。Unity native 終了時の stall の再調査。
- 現況（2026-10-18 に確認）:
  - OS の `LongPathsEnabled` は既に 1。git の `core.longpaths` は true。このどちらも原因ではない。
  - Unity native は 260 文字を超える path を扱える。主 checkout の `unity/Library` の最長 file は、絶対 path で約 265 文字。
  - Unity 同梱の Mono（Editor の C# と同じ BCL）を単体で動かした spike の結果:
    - `File.Exists` / `Directory.*` / `File.Delete` は 260 文字を超えても動く。
    - `File.OpenRead` / `ReadAllText` / `WriteAllText` / `Copy` / `Move` / `FileInfo.Length` は失敗する。
    - `\\?\` 接頭辞は Mono が `C:\?\...` に書き換えるので使えない。
    - P/Invoke の `CreateFileW("\\?\" + path)` で得たハンドルを `FileStream(SafeFileHandle)` に包めば、326 文字でも読み書きできる。§4 の規則 3 の根拠として記録する。
  - repo 相対 path の最長（tracked）:
    - NuGet の展開物（`unity/Assets/Packages/`、vendor）は 178 文字。
    - それ以外は 138 文字（`unity/Assets/SampleGame/InGame/InGameSession/Seasons/Winter/Cells/Winter_Cell_8_5/Winter_Environment_8_5/Winter_Environment_8_5.unity.meta`）。
  - 生成器（`WorldCompanionCreationPlan.Create`）の path は `{Season}_{Role}_{X}_{Y}` の命名で長さが決まる。本番の最長は、上の 138 文字と同じ形になる。
  - checkout のルートの長さ（末尾の区切りを含む絶対 path）:
    - 主 checkout: 57 文字。
    - Claude の worktree: 75 + 名前 + 1 文字（この worktree は 98 文字）。
    - Codex の worktree: 67 + 名前の文字数。

### 失敗 41 件の一次原因

S-4d1 の判定 C の XML（`.claude/worktrees/s-4d1-vfx-abstraction/TestResults`）で確認した。その worktree は調査中に削除されたので、生の証拠は残っていない。

| 群 | 件数 | 一次原因 |
| --- | --- | --- |
| `WorldCompanionCreationTransactionTests` / `WorldCompanionRecoveryTests` | 39 | テストの fixture の path が repo 相対で 164 文字ある（`Assets/__WorldCompanionTests_<12>/World/Seasons/Spring/Cells/S4aTest<12>_Cell/S4aTest<12>_Events/S4aTest<12>_Events.unity.meta`）。ルートの 97 文字と合わせると絶対 path で 261 文字になり、`WorldCompanionOwnershipProof.HashFile` の `File.OpenRead` が Mono で失敗する。fault injection を期待するテストも、この例外で先に落ちる。本番の命名より 26 文字長い。 |
| `ObservationReloadTests.RealDomainReloadRoundTrip` | 1 | Framework のテストが Play に入ると、SampleGame の `AppInitializer.Before` が走る。a77cbaf4（2026-09-20）から、Editor Play は `content:runtimeMode` も verified pair も無いと fail-closed になる。このテストは 1c176bf4（2026-10-01）で、それより後に追加された。ほかの Play テストは `SAMPLEGAME_CONTENT__RUNTIMEMODE=addressables` を自分で設定するが、このテストは設定しない。path とは関係が無い。 |
| `BuildContentDirectoryIntegrationTests.ScenePrefabTextureAndTwoRepresentationsBuildAndMoveAsOneDirectory` | 1 | URP/Lit のシェーダコンパイラが `out of memory during compilation` を出した（物理メモリ 16 GB）。同じ worktree の別の全件 run（1237 件）では出ておらず、単独の実行では passed。環境（メモリ）の可能性が高いが、未確定。 |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 環境に依存しない path 予算で、予算内の任意の checkout で全 EditMode 回帰が通り、予算外の checkout では原因の分かる形で止まる状態を作れるか。
- 進める最低条件:
  - 不変条件:
    - I1: 予算内の checkout では、テストの合否がルートの長さで変わらない。
    - I2: 予算を超えた checkout で、run-tests が Unity を起動しない（path 長による謎の失敗を出さない）。
    - I3: fail-closed の契約（Editor Play の `content:runtimeMode` 必須、`WorldCompanionOwnershipProof` の所有の証明）を、テストを通すために緩めない。
    - I4: OS / レジストリ / Unity のインストール / ユーザーの環境変数を変えない。
    - I5: 予算の数値の正本は 1 箇所。ほかの場所は正本を読むか参照するだけにする。
  - 成立条件:
    - S1: 深い worktree（このもの、ルート 98 文字）と主 checkout（57 文字）の両方で、全 EditMode 回帰の XML が failed 0、total が 1 以上。
    - S2: contract-audit が、予算を超える repo 相対 path（tracked と未追跡の非 ignore）を検出する。
    - S3: run-tests が、ルートの長さ + R が 259 を超えるとき、超過の文字数を示して Unity の起動前に失敗する。
    - S4: テストの fixture と生成器の最長の path が R 以内であることを、Editor のテストが確認する。
- 受け入れ条件:
  - A1（I1, S1）: 2 つの checkout の全件 XML で、`WorldCompanion*` の 39 件と `ObservationReloadTests` が Passed。
  - A2（I2, S3, 失敗経路）: run-tests の offline テストで、ルートを長くした合成の入力が Unity の起動前に失敗し、process の起動の呼び出しが 0 回になる。予算ちょうどは通り、1 文字超えると失敗する（境界の合格例・不合格例）。
  - A3（S2, 失敗経路）: contract-audit の offline テストで、R + 1 文字の合成 path を検出する。vendor の allowlist（`unity/Assets/Packages/`）の中は検出しない。
  - A4（S4）: `WorldCompanionCreationPlan` について、本番の最長の組（最も長い季節名、役割名、座標の桁数）と `CreateForTests` の fixture の path が、R 以内であることをテストで確認する。
  - A5（I3）: `ObservationReloadTests` が content mode を明示して Passed になる。`AbstractApplicationInitializer` の fail-closed 分岐と `InvalidCompanionConfigStartupTests` は差分に入らない。不合格の例: `LogAssert.ignoreFailingMessages` で例外ログを握りつぶす。
  - A6（I4）: 差分と実行手順に、レジストリ、`setx`、Unity のインストール先への書き込みが無い。
  - A7（I5）: R の数値は `tools/path-budget.json` だけにある。contract-audit、run-tests、Editor のテストはその file を読む。`AGENTS.md` と README には数値を書かず、file を指す。
- ここでは答えない問い:
  - runtime の IO（Editor Play と Player で動くもの）の path 予算 → 後続スライス（仮称 `RUNTIME_PATH_BUDGET`）。Player の配置の path はルートが利用者の環境で決まるので、別の問いになる。
  - シェーダコンパイルの OOM の恒久策 → S1 の 2 回の全件 run で再現しなければ、後続の観察項目にする。
  - 長さを抑えられない path が将来できたときの helper の実装 → その経路を作るスライスが所有する（§4 の規則 3）。
- 判定定義: S1〜S4 が揃い、I1〜I5 に反証が無ければ GO。
- 停止規則: 標準どおり。OOM が判定 C の全件 run で再現した場合は、1 回だけ再実行する。それでも再現したら、理由を付けて発注者へ返す。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約: `#nullable enable`、`record` の禁止、`Task.Delay` / `Thread.Sleep` の禁止、asmdef の参照の追加は設計判断として停止する。
- 未決事項: Q2（§4）

## 3. 責務マップ

| file | 責務と変更理由 | 増分の見込み |
| --- | --- | --- |
| `tools/path-budget.json`（新規） | R（repo 相対 path の上限）と vendor の allowlist の正本。理由は JSON の `description` に書く。 | 新規 10 行前後 |
| `tools/contract-audit.ps1` | 検査 9（path 予算）: tracked と未追跡の非 ignore の path から、予算を超えるものを列挙して失敗する。既存の `$vendorPrefixes` とは目的が違う（vendor の中も、NuGet 以外は予算の対象にする）ので、allowlist は `path-budget.json` に置く。 | +30〜40 行 |
| `tools/contract-audit.tests.ps1` | 検査 9 の合成 fixture（R + 1 は検出し、allowlist の中と R ちょうどは通す） | +30 行 |
| `tools/run-tests.ps1` | Unity の起動前の事前検査: ルートの長さ（末尾の区切りを含む）+ R ≤ 259。満たさなければ、超過の文字数と正本の file を示して失敗し、step.json に `path-budget` の失敗を記録する。 | +20〜30 行 |
| `tools/Harness/tests/UnityTestRunner.Tests.ps1` | 事前検査の境界（ちょうどは通る、1 超えは起動 0 回で失敗） | +30 行 |
| `unity/Assets/SampleGame/Tests/Editor/WorldAuthoring/WorldCompanionCreationTransactionTests.cs` | fixture の path を R 以内に縮める（root の名前と、`S4aTest{token}` の 3 回の繰り返しを短くする。token の一意性は保つ） | ±10 行 |
| 同じフォルダの新規テスト（または既存の plan のテスト） | A4: 本番の最長と fixture の path ≤ R。R は `path-budget.json` から読む。 | 新規 40〜60 行 |
| `unity/Assets/OneStarMaker/Tests/Editor/TestObservation/ObservationReloadTests.cs` | Q2 の裁定に従う | ±10 行 |
| `AGENTS.md` | 常時契約に 1 行: checkout のルートと repo 相対 path は `tools/path-budget.json` の予算に従う。置き場所は指定しない。 | +1 行 |
| `tools/Harness/README.md` | run-tests の事前検査の説明 | 文書 |

### R の値（A2 で確認）

- 案: R = 150（repo 相対、`unity/` を含む）。ルートの上限は 259 − 150 = 109 文字になる。
- 余裕: 本番の最長 138 文字に対して 12 文字の余裕。Claude の worktree なら名前は 33 文字まで、Codex なら 42 文字まで入る。主 checkout は 52 文字の余裕。
- 259 の根拠: MAX_PATH の 260 は終端の NUL を含む。

## 4. 実装計画と規則

### path 予算の 3 つの規則（発注者と合意済み）

1. **repo 相対 path は R 以内。** contract-audit で検査する。例外は vendor の allowlist（NuGet の展開物）だけ。これは Unity native が import し、自前のコードは開かない。
2. **checkout のルートは 259 − R 以内。** 作り方の規定であって、置き場所は指定しない。run-tests が起動前に検査し、超えたら理由付きで止める。
3. **長さを R に収められない経路だけ、long path に対応した IO を使う。** いまのコードにはその経路が無いので、このスライスでは helper を作らない。将来、利用者が命名する asset のように長さが決まらない path を自前のコードで開くときは、そのスライスが helper（spike で確認した `CreateFileW` + `FileStream(SafeFileHandle)`）を導入する。

### 不採用の案

- 短い junction から Unity を起動する案: 特定の場所が要り、対象が増えるたびに維持が要る。ローカル環境に依存するので不採用。
- Editor の全 IO を helper に寄せ、直接の `File.*` を audit で禁止する案: 約 37 file の変更と恒常的な運用の負担がかかるわりに、package のコードには効かない。今回の失敗は規則 1 で防げるので不採用。

### Q2: `ObservationReloadTests`（推奨: (a)）

- (a) テストが Play の前に content mode を `addressables` にし、終了時に元へ戻す。ただし Framework のテストが `SAMPLEGAME_` の名前を直接知ると、依存の方向（Game → Framework）に反する。そこで Framework 側の汎用の仕組み（`AbstractApplicationInitializer` の環境変数の prefix を公開面から得る方法、または `CommandLineConfigProvider`）で設定できるかを B で確認する。できなければ (b) にする。
- (b) テストを `SampleGame.Tests.Editor` へ移し、ほかの Play テストと同じ方式で設定する。asmdef の参照（`OneStarMaker.Editor.TestObservation`）の追加が要る場合は、Phase B を止めて A に戻す。

### 順序

1. `path-budget.json` → contract-audit の検査 9 と offline テスト → run-tests の事前検査と offline テスト
2. fixture の短縮 → A4 のテスト → Q2
3. `AGENTS.md` と README

Phase B から A へ戻す条件: asmdef の参照の追加が必要になった場合。fixture を短くしても `WorldCompanion*` が path 以外の理由で失敗した場合（スパイクとして原因を確認してから分類する）。R = 150 に収まらない tracked の path が vendor 以外で見つかった場合。

## 5. テストとレビュー計画

- 差し戻し中の起点 `-Filter`: `WorldCompanion;ObservationReloadTests`（XML で 40 件以上の収録を確認する）。加えて、offline の `tools/contract-audit.tests.ps1` と `tools/Harness/tests/UnityTestRunner.Tests.ps1`。
- 判定必須テスト: 全 EditMode 回帰（空 filter）を、(1) この worktree（ルート 98 文字）と (2) 主 checkout の 2 回。どちらも sandbox の外で、ほかの Unity が動いていない状態で実行する。主 checkout の Editor と lock の状態は、実行の直前に確認する。offline の 2 つの suite も判定必須に含める。
- 全 EditMode 回帰の適用除外: なし
- 操作・実行時・目視の条件の検証経路: 対象なし（すべてテストで閉じる）
- 人間の判断が必要な条件: なし
- 高コスト証拠: なし
- 判定 C の照合 script: なし（XML の件数と failed 0、offline suite の exit で判定する）
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`
- A2 独立レビュー: 1 件以上。アーキテクチャゲート（予算の正本の所有者、contract-audit と run-tests の責務の分け方、Framework のテストと Game の依存の方向）を含める。
