# Runtime 内の依存循環 2 件の解消

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `A`（A1 初稿。A2 未実施、A3 未凍結。**実装キューではない**）
- branch: 計画は `claude/runtime-refactor-stage0-plan`（本書と [RUNTIME_REFACTOR_BOOTSTRAP.md](RUNTIME_REFACTOR_BOOTSTRAP.md) を同じ文書 PR で `develop` へ）。実装は Phase B 開始時に `develop` から専用ブランチを切る（名前は B 担当が `<prefix>/runtime-refactor-cycles` で決める）
- implementation base commit: `840b6609a443e7ed3252c1c83224d3d9c5c16b49`（`origin/develop`、PR #149 のマージ）。B 開始時に `develop` の head で再固定する
- implementation head commit: 未固定（Phase B で記録）
- risk: `normal`（public 型 2 つの namespace 変更と public メソッド 1 つの移動。実行時挙動の変更はない）
- owner: root（依頼者）。A2 / B / C / C' の担当は Phase ごとに選ぶ
- created: 2026-10-31
- expires: 2026-11-30 または置換 revision の成立時。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/27-folder-structure.md` §2.2（Runtime 内の folder 間でも循環を作らないこと、現行の向き 2 件）。新しい Architecture 文書は作らない
- 関連スライス: [RUNTIME_REFACTOR_BOOTSTRAP.md](RUNTIME_REFACTOR_BOOTSTRAP.md)（同時に計画した対のスライス。本書が先、Bootstrap が後。ファイルは重ならない）
- Phase A snapshot の取得案内・状態: A1 入力版は本文を収録した commit と本文の content sha256 で固定する（文書だけなので stage は不要）。値は A2 開始時に §5「A2 独立レビュー」へ記録する。A3 凍結版は未生成
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A2（独立レビュー 2 観点）。
  - 次の作業: §5 の観点で本書を独立にレビューし、指摘を §5「A2 独立レビュー」へ記録する。A3 で owner と主担当が採否を決め、凍結版の commit と content sha256 を記録する。
  - 引き継ぐ commit: 本書を収録した計画ブランチの commit（A2 開始時に §5 へ転記）。
  - 必要な権限: なし（文書のみ）。Unity Editor・テスト・資格情報は不要。
  - 停止規則: A3 の凍結は owner の承認を待つ。同じ session で Phase B に入らない。
  - 引き渡さないもの: なし（Phase A 内）。

### PR 状態ブロック

```text
状態: Phase A の 作業中（A1 初稿）
Phase A: revision 0、commit <計画 PR 作成時に記録>
implementation base / head: 840b6609 / 未固定
次の担当: Phase A2 独立レビュー。次の作業: §5 の 2 観点でレビューし結果を §5 へ記録
HANDOFF: docs/handoff/RUNTIME_REFACTOR_CYCLES.md
Evidence 取得案内: 未生成
未決事項: §2 の U1〜U3
```

## 1. 目的と対象外

- 目的: `OneStarMaker.Runtime` 内の folder 間依存にある循環 2 件（AssetManagement ⇄ BuildContent、CameraSystem ⇄ Streaming）を、型の意味・公開メンバー・実行時挙動を変えずに解消し、Runtime 内の folder 間依存が一方向であることを `grep` で機械確認できる状態にする。これは Runtime asmdef 分割（後続）の前提であり、Camera を独立 assembly にするときに Streaming への参照が無いこと、Assets を独立 assembly にするときに内部の向きが一方向であることを先に成立させる。
- 対象外（所有する後続）:
  - asmdef の追加・変更、`InternalsVisibleTo` の変更: 後続 `RUNTIME_ASMDEF_STAGE1`（未作成。Camera / UI / DebugSocket を切るスライス）
  - `AbstractApplicationInitializer` の分解と、Bootstrap が使う他 folder の internal 型の seam 化: [RUNTIME_REFACTOR_BOOTSTRAP.md](RUNTIME_REFACTOR_BOOTSTRAP.md)
  - BuildContent → AssetManagement の internal 5 型（`IBackendAsset` / `IBackendInstance` / `IBackendScene` / `IContentDirectoryBackend` / `IContentDirectoryToken`）の public 化。向きは正しいので触らない。AssetManagement と BuildContent を別 assembly にするかは asmdef Stage 2 の問い
  - `SceneDirector` の `Loading` / `Unloading` / `Transitions` にある telemetry 終了処理 3 重化の除去: SceneSystem を次に触るスライス、または asmdef Stage 2 の core 整理
  - `CameraSystemSliceSetup.Create` に製品コードの呼び出しが無い事実の扱い（配線するか、独立 assembly に置くか）: asmdef Stage 1 / 3 の A0 入力。本スライスは配線しない・削除しない
  - `Runtime/Bootstrap` と `Runtime/Telemetry` の namespace が folder と一致しない件: Bootstrap を独立 assembly にするスライス
- 現況（base `840b6609` の実測）:
  - Runtime は 17 folder、約 24,800 行、1 asmdef。folder 間の依存は慣習だけで、機械検査は無い。型名の出現で辺を引いた結果、循環は次の 2 件だけで、どちらも 2026-07 以降レビューと監査をすり抜けていた。
  - **循環 A。** `Runtime/AssetManagement/AssetManagement.ContentDirectory.cs` は `using OneStarMaker.Runtime.BuildContent;` を持ち、`ContentDirectoryException` と `ContentDirectoryFailureCode`（宣言は `Runtime/BuildContent/ContentDirectoryException.cs`、32 行、どちらも public）を 6 箇所で投げる。一方 BuildContent は AssetManagement の `AssetKey` / `AssetManagement` / `AssetType` / `SceneLoadOptions` と internal 5 型を使う。port `IContentDirectoryBackend` は `Runtime/AssetManagement/Internal/` にあり、adapter（`ContentDirectorySession`、`UnityContentDirectoryBackend`）は BuildContent にある。つまり BuildContent → AssetManagement が本来の向きで、例外型 2 つだけが逆向きの辺を作っている。
  - **循環 B。** `CameraFocusSource`（public readonly struct）は `Runtime/Streaming/CameraFocusProvider.cs` 69 行目で宣言され、`Runtime/CameraSystem/Hosting/CameraSystemSliceSetup.cs` 80 行目の `CreateStreamingFocusSources(bool includeMainView = false)` だけがそれを生成する。`CameraSystemSliceSetup.cs` の `using OneStarMaker.Runtime.Streaming;` はこのメソッドのためだけにある。Streaming は `CameraSystem` / `ICameraView` を使う（`CameraFocusProvider`、`CameraStreamingFocusAdapter`）。`CameraSystemSliceSetup.Create` と `CreateStreamingFocusSources` に製品コードの呼び出しは無く、テスト `Tests/Camera/CameraSystemSliceSetupTests.cs`（59 行、`[Test]` 2 件）だけが使う。
  - `ContentDirectoryException` / `ContentDirectoryFailureCode` を参照するファイルは 19（Runtime/AssetManagement 1、Runtime/Bootstrap 1、Runtime/BuildContent 12、OneStarMaker.Tests 5、SampleGame.Tests.Editor 1）。うち `using OneStarMaker.Runtime.AssetManagement;` を**持たない**ものは 10（§3）。
  - 設計文書での言及: `18-asset-description.md` 190 行目が `ContentDirectoryException` を名前だけで参照（namespace は書いていない）。`23-camera-system.md` は `CreateStreamingFocusSources` を参照しない。`docs/streaming/` にも参照は無い。
  - `SerializeReference` の実データは無く、`m_Script` は GUID 解決なので、`.cs` と `.meta` を一緒に移せば serialized 参照は壊れない。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: Runtime の folder 間依存から循環 2 件を、公開 API の意味と実行時挙動を変えずに取り除けるか。
- 判定定義: §2.1 の不変条件と成立条件を §2.2 の受け入れ条件で全て確認できれば GO。一つでも未達なら NO-GO。スパイクではないので CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし（A3 後に作る場合は本書と別の記録にする）

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I1 型の同一性:** `ContentDirectoryException` と `ContentDirectoryFailureCode` の型名、基底型（`InvalidOperationException`）、コンストラクタ引数、プロパティ、enum メンバーの名前と順序、XML doc を変えない。変わるのは namespace（`OneStarMaker.Runtime.BuildContent` → `OneStarMaker.Runtime.AssetManagement`）とファイルの置き場所だけ。
- **I2 Camera 側の公開面:** `CameraSystemSliceSetup` の public メンバーのうち `CreateStreamingFocusSources` 以外（`Create`、`MainView`、`SplitViewA`、`SplitViewB`、`MinimapView`、`PushCutscene`、`Dispose` 等）の signature を変えない。`CameraFocusSource` / `CameraFocusProvider` / `CameraStreamingFocusAdapter` の既存 public メンバーを変えない（追加だけ）。
- **I3 境界不変:** `.asmdef` の変更 0 件。`SampleGame/` の製品コード変更 0 件（`SampleGame/Tests/` の `using` 変更は可）。移動する `.cs` の `.meta` は一緒に移して GUID を保つ。
- **I4 挙動不変:** 生成される focus source の件数・順序・`IncludeInStreaming` の値（既定: SplitViewA(true), SplitViewB(true), MinimapView(false)。`includeMainView: true` のときは MainView(true) が先頭）を変えない。例外を投げる箇所・`Code`・メッセージを変えない。
- **I5 テストが減らない:** 移動する 2 テストは同じ assertion を保つ。全 EditMode のテスト件数が base 以上。

**成立条件（起きなければならないこと）**

- **S1 AssetManagement → BuildContent の辺 0:** `Runtime/AssetManagement/**/*.cs` に文字列 `OneStarMaker.Runtime.BuildContent` が無い。
- **S2 CameraSystem → Streaming の辺 0:** `Runtime/CameraSystem/**/*.cs` に文字列 `OneStarMaker.Runtime.Streaming` と `CameraFocusSource` が無い。
- **S3 新しい循環を作らない:** 変更後の `Runtime/BuildContent/**` が参照する Runtime の namespace は `AssetManagement` / `AssetDescriptions` だけ（既存どおり）、`Runtime/Streaming/**` は `CameraSystem` / `SceneSystem` だけ（既存どおり）。
- **S4 回帰:** 全 EditMode 回帰と `pwsh tools/contract-audit.ps1` が合格する。

### 2.2 受け入れ条件

- **AC1（S1, I1）** `Runtime/AssetManagement/Abstractions/ContentDirectoryException.cs` に 2 型が namespace `OneStarMaker.Runtime.AssetManagement` で存在し、`Runtime/BuildContent/ContentDirectoryException.cs` は無い。`git diff -M --stat base..head` が rename として検出する（類似度 90% 以上）。`git diff -M base..head -- <新 path>` の変更行が `namespace` 行だけである。不合格例: enum に値を足す、`sealed` を外す、コンストラクタの省略可能引数の順序を変える。
- **AC2（S1）** 次が 0 行を返す。

  ```bash
  grep -rn "OneStarMaker.Runtime.BuildContent" unity/Assets/OneStarMaker/Scripts/Runtime/AssetManagement
  ```

- **AC3（I1 の失敗経路）** `ContentDirectoryFailureCode.<値>` を assert する既存テスト（`Tests/BuildContent/ContentRevisionGateTests.cs`、`ContentDirectoryIndexTests.cs`、`PlayerContentConfigurationTests.cs`、`Distribution/ContentDeliveryFailureMatrixTests.cs`、`Tests/Editor/Build/ContentDirectorySessionTests.cs`、`SampleGame/Tests/Editor/Build/BuildContentDirectoryIntegrationTests.cs`）が、`using` 行の追加・削除以外の変更なしで合格する。`git diff base..head -- <各テスト>` に `using` 以外の変更行が無い。
- **AC4（S2, I2）** `CameraSystemSliceSetup.cs` から `CreateStreamingFocusSources` と `using OneStarMaker.Runtime.Streaming;` が消え、次が 0 行を返す。

  ```bash
  grep -rn -E "OneStarMaker\.Runtime\.Streaming|CameraFocusSource" unity/Assets/OneStarMaker/Scripts/Runtime/CameraSystem
  ```

  かつ、base と head で次の出力が `CreateStreamingFocusSources` の 1 行差だけである。

  ```bash
  grep -E "^\s*public" unity/Assets/OneStarMaker/Scripts/Runtime/CameraSystem/Hosting/CameraSystemSliceSetup.cs
  ```

- **AC5（I4, S2）** Streaming 側の新しい入口（§3、U1）に対し、既存 2 テストと同じ assertion（既定で 3 件: SplitViewA(true) → SplitViewB(true) → MinimapView(false)。`includeMainView: true` で 4 件、先頭が MainView(true)）が `Tests/Streaming/` で合格する。合格例: `CameraSystemSliceSetupTests` の 2 メソッドの本文が、呼び出し式の差し替えだけで新ファイルへ移る。不合格例: 順序の assertion を `CollectionAssert.AreEquivalent` に緩める。
- **AC6（I3）** `git diff --stat base..head` に `.asmdef` が無く、`unity/Assets/SampleGame/` の変更が `SampleGame/Tests/` 配下だけ。移動した `.cs` に対応する `.meta` も rename として検出される。
- **AC7（S3）** 次の 2 つの出力が base と同じ集合である（新しい namespace が増えていない）。

  ```bash
  grep -rhoE "using OneStarMaker\.Runtime\.[A-Za-z.]+;" unity/Assets/OneStarMaker/Scripts/Runtime/BuildContent | sort -u
  ```

  ```bash
  grep -rhoE "using OneStarMaker\.Runtime\.[A-Za-z.]+;" unity/Assets/OneStarMaker/Scripts/Runtime/Streaming | sort -u
  ```

  BuildContent 側は `AssetManagement` の using が増える（既に持つファイルが多いので集合としては同じ）。
- **AC8（S4, I5）** `pwsh tools/run-tests.ps1`（空 filter、EditMode）が合格し、XML のテスト件数が base 以上。`pwsh tools/contract-audit.ps1` 合格。

### 2.3 ここでは答えない問いと所有スライス

§1 の対象外のとおり。加えて:

- `IContentDirectoryBackend` を public にして AssetManagement と BuildContent を別 assembly に分けるか: asmdef Stage 2 の A0
- `CameraSystemSliceSetup` 自体を Camera に残すか Streaming 側の「代表構成」として扱うか: asmdef Stage 1 の A0（本スライスは置き場所を変えない）

### 2.4 本文へ転記した実装制約（`AGENTS.md` の常時契約から）

- 依存は Game → Framework の一方向。asmdef 参照の追加は設計判断なので行わない（本スライスは asmdef に触れない）。
- 新規または編集する Unity 側の `.cs` は先頭に `#nullable enable`（対象ファイルは全て既に持つ。外さない）。
- `record` を使わない。Unity の偽 null は `== null` / `!= null` で判定する（本スライスで新しい判定は書かない）。
- テストで `Task.Delay` / `Thread.Sleep` を使わない（移動するテストは同期）。
- 参照 0 を削除理由にしない（`CameraSystemSliceSetup.Create` と `CreateStreamingFocusSources` 相当の機能は残す）。
- `.meta` は `.cs` と一緒に `git mv` する。Unity Editor でのコンパイル確認は Phase B が行ってよい。Unity テストの実行・判定は Phase C。

### 2.5 未決事項（A2 で意見を求める）

- **U1 Streaming 側の新しい入口の置き場所。** A1 案は `CameraStreamingFocusAdapter` に `public static IReadOnlyList<CameraFocusSource> CreateSliceFocusSources(CameraSystemSliceSetup setup, bool includeMainView = false)` を足す（既存 adapter の責務「CameraSystem の View 群から focus を組む」と同じ変更理由）。代替は新 static class `CameraSliceFocusSources`。新 class はファイルが 1 つ増えるだけで責務の違いが無いので A1 は前者。
- **U2 例外型の配置。** A1 案は `Runtime/AssetManagement/Abstractions/`（`AssetKey` / `AssetOwner` / `SceneLoadOptions` 等の public 型と同じ場所）。port `IContentDirectoryBackend` は `Internal/` にあるが、例外は public なので `Abstractions/` に揃える。
- **U3 `CameraFocusSource` を Camera 側へ移す案を採らない判断。** 移すと Camera が「ストリーミングに含めるか」という Streaming の語彙（`IncludeInStreaming`）を持つことになる。A1 は生成メソッドを Streaming へ移す。

## 3. 責務マップ

新しい責務は増えない。変更は「置き場所」と「生成メソッドの所属」の 2 つ。

| ファイル | 責務（一文） | 変更内容 | 現在行数 | 予想増分 |
|---|---|---|---:|---:|
| `Runtime/BuildContent/ContentDirectoryException.cs` → `Runtime/AssetManagement/Abstractions/ContentDirectoryException.cs`（`.meta` も） | Content Directory の公開境界で返す失敗情報（例外と reason code） | `git mv`、`namespace` 行の変更 | 32 | 0 |
| `Runtime/AssetManagement/AssetManagement.ContentDirectory.cs` | 内蔵 Content Directory 経路（変更なし） | `using OneStarMaker.Runtime.BuildContent;` を削除 | 164 | −1 |
| `Runtime/BuildContent/ContentRevisionGate.cs`、`RevisionProcessLease.cs`、`PlayerContentConfiguration.cs`、`Distribution/ContentCacheStore.cs`、`Distribution/ContentInstaller.cs`、`Distribution/ContentSourceAdvisor.cs`、`Distribution/InstalledRevisionVerifier.cs` | （変更なし） | `using OneStarMaker.Runtime.AssetManagement;` を追加 | — | 各 +1 |
| `Tests/BuildContent/ContentDirectoryIndexTests.cs`、`ContentRevisionGateTests.cs`、`PlayerContentConfigurationTests.cs` | （変更なし） | 同上 | — | 各 +1 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` と、既に `using OneStarMaker.Runtime.AssetManagement;` を持つ残り 8 ファイル | （変更なし） | 変更不要。B は `using OneStarMaker.Runtime.BuildContent;` が他の型のために残ることを確認する | — | 0 |
| `Runtime/CameraSystem/Hosting/CameraSystemSliceSetup.cs` | 代表的な View 構成（メイン + 分割 2 面 + RT ミニマップ）の組み立てと所有 | `CreateStreamingFocusSources` と `using OneStarMaker.Runtime.Streaming;` を削除。summary の「SceneStreaming 向け focus 供給元の生成も担う」を削除 | 127 | 約 −25 |
| `Runtime/Streaming/CameraStreamingFocusAdapter.cs` | CameraFocusProvider と WorldStreamingController を束ねる adapter。**追加:** 代表構成から focus source 列を組む static 入口（U1） | static メソッド追加（本文は移動元と同じ。既定で MainView を除外、MinimapView は `IncludeInStreaming=false`） | 73 | 約 +25 |
| `Tests/Camera/CameraSystemSliceSetupTests.cs`（`.meta` も） | （削除） | 2 テストを下へ移し、ファイルを削除 | 59 | −59 |
| `Tests/Streaming/CameraStreamingFocusAdapterTests.cs` | 既存の adapter テスト。**追加:** 移動した 2 テスト（名前は `CreateSliceFocusSources_*` に変える） | 2 メソッド追加 | 既存 | 約 +40 |
| `unity/Assets/Docs/Architecture/27-folder-structure.md` | フォルダ構成戦略（harvest 先。Phase D） | §2.2 禁止の list に「Runtime 内の folder 間でも循環を作らない。現行の向きは BuildContent → AssetManagement、Streaming → CameraSystem」を 1 項目追加 | — | +2 |

配置と依存を選んだ理由:

- 例外型は port の側に置く。契約型（port、例外、reason code）を同じ側に置けば、adapter は契約だけに依存し、契約は adapter を知らない。
- focus source の生成は「どの View をストリーミングに含めるか」の判断なので Streaming の語彙に属する。Camera は View を提供するだけで、ストリーミングの包含可否を知らない。
- 新しいファイル・クラス・asmdef は作らない。行数警報（500 行 / 3 責務 / 50% 増）に該当するファイルは無い。

## 4. 実装計画

- 変更対象: §3 の表のとおり。
- 順序: (1) 例外型 2 つの `git mv`（`.cs` と `.meta`）と `namespace` 変更 → `using` の追加 7 + 3 と削除 1 → Unity Editor でコンパイル確認。(2) `CreateSliceFocusSources` を Streaming へ追加し、`CameraSystemSliceSetup` から削除 → テスト 2 件を移してファイルを削除 → コンパイル確認。(3) AC2 / AC4 / AC7 の `grep`。(4) `pwsh tools/contract-audit.ps1`。(5) 起点 filter のテストは Phase C（発見 C）が実行する。
- 各手順を 1 commit にする（rename の検出を保つため、(1) の `git mv` と `namespace` 変更は同じ commit に入れてよい）。判定 C の前に整理するかは [公開点と Git 履歴](../../.agents/skills/osm-workflow/SKILL.md) に従う。
- Phase B から Phase A へ差し戻す条件: 例外型 2 つ以外に移動が必要な型が見つかった（例: BuildContent の別の型を AssetManagement が使っている）。`CameraSystemSliceSetup` の private メンバーを public にしないと生成メソッドを移せない。`.meta` の rename を保てない。既存テストの assertion を変えないと通らない。
- 対象外を維持する方法: `.asmdef` を開かない。`SampleGame/` は `Tests/Editor/Build/BuildContentDirectoryIntegrationTests.cs` の `using` 確認だけ。AC6 の `git diff --stat` で機械確認する。

## 5. テストとレビュー計画

- 単体テスト: 新しい中核ロジックは無い。移動する 2 テスト（AC5）が focus source 生成の挙動を固定する。例外型は既存テスト（AC3）が `Code` 値で固定する。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `OneStarMaker.Tests.Streaming`、`OneStarMaker.Tests.CameraSystem`、`OneStarMaker.Tests.BuildContent`、`SampleGame.Tests.Editor.Build`。
- 判定必須テスト（GO 候補 head）: 全 EditMode 回帰（空 filter）。XML で `CreateSliceFocusSources_*` 2 件と AC3 のテストが収録されていること。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: 既存の EditMode だけ。PlayMode・Build は不要（実行時の分岐を変えない）。
- 操作・実行時・目視条件の検証経路: 対象なし（全条件が EditMode テストと `grep` / `git diff` で閉じる）。
- 未知の操作経路の疎通結果: なし
- 人間の判断が必要な条件: なし
- 高コスト証拠: なし
- 判定 C の照合 script の方式: なし（AC の `grep` / `git diff` コマンドと XML のテスト名・件数で判定する）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC2 / AC4 / AC6 / AC7 のコマンド。
- A0/A1 主担当・モデル・ベンダー: Claude Code（Anthropic）。調査は本書 §1 の現況に転記済み。
- A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版: 下記。観点は (a) アーキテクチャゲート（例外型の置き場所と向き、focus source 生成の所属、U1〜U3）、(b) 挙動同一性（AC の網羅、既存テストが変更なしで通るか、`.meta` と rename、SampleGame.Tests への影響）。A2 は「計画対象は実装済みと仮定して」評価し、未実装を理由に差し戻さない。
- A3 統合担当・モデル・採否: 未定（owner + 主担当）。
- C' 用に予約した担当・モデル・ベンダー: A2 で使わなかった系列。A2 を Anthropic 以外の 2 系列にできれば、C' は残る 1 系列。
- 独立性の強化条件を満たせない場合の理由: 未定（A2 で記録する）。

### A2 独立レビュー

（A1 入力版の commit と content sha256、各レビューの観点・担当・モデル・ベンダー・結果をここに記録する）

## 6. Phase B 実装結果

- 実装:
- HANDOFF との差:
- 未実行:
- implementation head commit:
- Phase B 担当・モデル・ベンダー:

### 動作確認

- 対象 head:
- 条件ごとの実行担当・環境・実行したコマンド:
- 生の観測と保存先・状態:
- 検証経路の期待と違う観測と、B への差し戻し:
- `未実施` の条件と理由・担当: 対象なし（§5 のとおり検証経路に該当条件が無い）

## 7. Phase C

- 種別:
- 使用した evidence bundle の取得案内・状態:
- 構造適合:
- 現在の問いを阻害する findings:
- 後続スライスへ移送する findings:
- 実行したテストコマンドと `-Filter`、対象を選んだ理由:
- テスト結果:
- 判定必須のうち未実行:
- 未確認事項:
- 担当・モデル:

## 8. Phase C'

- 担当方式:
- 種別と巡:
- 受け取った決定記録の版:
- 使用した blind audit bundle の取得案内・状態:
- 確認範囲・方法:
- 判定:
- 現在の問いを阻害する findings:
- 後続スライスへ移送する findings:
- 残存リスク:
- 監査できなかった範囲:
- 独立性:
- 発見 C / 判定 C 結論の事前閲覧・設計実装への関与:
- 担当・モデル:

## 9. Phase D

- C / C' の突合:
- マージ判断:
- 必要な Evidence の保存・取得・必須内容の閲覧確認:
- 取得案内の引渡し確認:
- harvest: `27-folder-structure.md` §2.2 に 1 項目（§3 の表の最終行）
- 削除確認:
- 通常の task 終了・後始末の状態と未完了事項:
