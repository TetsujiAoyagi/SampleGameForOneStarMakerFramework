# SceneStreaming T-02 引き継ぎ書 (2026-07-06)

> 前セッションからの引き継ぎ。次の作業は **T-02: SceneDirector の並行 AddScene ガード実装（H-1）+ キャンセル窓修正（H-4）**。
> 設計の正典は `unity/Assets/Docs/Architecture/21-scene-streaming.md`。本書は作業状態の復元用スナップショット。

---

## 1. 現在地（1分で把握）

- SceneStreaming の設計ドキュメント（21-scene-streaming.md）は完成済み。実装はまだ何もしていない
- TDD で進行中。**テストは書き終わっており、レッド7本が「次に直すもの」を正確に指している**
- プロダクションコードは未変更。変更したのはテストコードとドキュメントのみ

**テスト現況（EditMode / OneStarMaker.Tests.SceneSystem = 53本）:**

| 状態 | 本数 | 内容 |
|---|---|---|
| グリーン | 46 | ライフサイクル27 + 復活ベースライン17 + 並行順次系など |
| レッド | 5 | `SceneDirectorConcurrentAddSceneTests` — H-1（並行 AddScene 競合） |
| レッド | 2 | `SceneDirectorCancellationTests` の `Timeout(10000)` 付き2本 — H-4（キャンセル窓） |

このレッド7本をグリーンにするのが T-02 の受入条件。既存グリーン46本＋他アセンブリのテストに回帰を出さないこと。

## 2. テストの実行方法（実証済みコマンド）

Unity エディタを閉じた状態で:

```powershell
& "D:\UnityEditor\6000.5.0f1\Editor\Unity.exe" -batchmode `
  -projectPath "D:\repositories\unity\SampleGameForOneStarMakerFramework\unity" `
  -runTests -testPlatform EditMode `
  -testFilter "OneStarMaker.Tests.SceneSystem" `
  -testResults "D:\repositories\unity\SampleGameForOneStarMakerFramework\test-results-scenesystem.xml" `
  -logFile "D:\repositories\unity\SampleGameForOneStarMakerFramework\unity-test-run-scenesystem.log"
```

注意: Unity.exe は即座に制御を返すため、`Get-Process Unity | ForEach-Object { $_.WaitForExit() }` で完了を待ってから XML を読むこと。SceneSystem 全体で6分程度（H-4 の Timeout 2本が各10秒消費）。

## 3. H-1: 並行 AddScene の親共有競合（レッド5本の原因）

**現象**: 2つの `AddScene` が同じ親を共有すると、後発が親の `LoadUnityScene` に二重突入する。
`SceneLifecycleManager` の遷移検証が働き `InvalidOperationException: Invalid scene state transition: Loading → Loading`（または `PreLoading → Loading`）で即死する。サイレントな二重ロードより手前で、状態機械が大声で壊れるのが実測結果。

**競合経路は3つ**（詳細は `Tests/Scene/SceneDirectorConcurrentAddSceneTests.cs` のクラスコメント）:

- (a) 親が `Loading` 中（`PerformUnitySceneLoad` await 中）に後発が `LoadUnityScene(親)` へ突入
  — `SceneDirector.Loading.cs` の `LoadUnityScene` 冒頭スキップ条件が `IsActive`（=Stable）のみのため
- (b) 親が `PreLoading` 中の場合、`LoadSceneBase` の既存シーン分岐（`IsNone` のみ判定）が PreLoad 完了を**待たずに素通り**して (a) に合流
- (c) 同一 identity の並行 `AddScene`: 先頭ガードの「ロード中ならスキップ」が**即 return** するため、後発の awaiter が Stable 到達前に完了してしまう

**修正方針（21-scene-streaming.md §5 H-1）**: 識別子ごとの in-flight タスク共有。ロード進行中の identity への `AddScene` は新規ロードを開始せず、進行中の UniTask を await して合流する。確立すべき契約は **「AddScene の await 完了 = 対象シーンの Stable 到達（またはキャンセル）」**。ストリーミングの WorldStreamingController はこの契約を信頼する。

実装ヒント: `UniTask` を複数 awaiter で共有する場合は `.Preserve()` または `UniTaskCompletionSource` 経由にすること（UniTask は既定で二重 await 不可）。in-flight 辞書のクリーンアップは成功・キャンセル・例外の全経路で必要。

## 4. H-4: PreLoad 中のキャンセル窓が機能しない（レッド2本の原因）

**現象**: PreLoad 実行中に `UnloadScene` を呼ぶと、キャンセル窓内のはずなのに `LoadCts.Cancel()` ではなく `_pendingUnloads` 登録に落ちる。PreLoad がキャンセル待ちでブロックしている場合デッドロック（テストでは 180 秒ハング → `Timeout(10000)` で10秒レッド化済み）。

**根本原因**: `SceneDirector.Loading.cs` の `AddScene` 内で、`LoadCts = linkedCts` の代入が `LoadSceneBase`（= PreLoad 実行）の**完了後**にある。PreLoad 中は `ScenePair.LoadCts` が null のため、`UnloadScene` のキャンセル窓判定（`SceneDirector.Unloading.cs`）が保留アンロード側へ倒れる。

**修正方針（21-scene-streaming.md §5 H-4）**: `LoadCts` を SceneBase 生成時（PreLoad 開始前）に代入する。`LoadSceneBase` が `newlyCreatedScenes` に追加した直後が候補。PoNR での null クリアと、キャンセル/例外経路でのクリーンアップは現行の構造を維持すること。

**再現テスト**: `SceneDirectorCancellationTests.UnloadScene_DuringPreLoadWindow_CancelsViaLoadCts` / 同 `AddScene_CancelDuringPreLoad_NoThrowIfUnloadSceneCanceled`（どちらも既知ハングのコメント付き。修正後は `Timeout` 属性を外してよい）

## 5. 主要ファイル

| ファイル | 役割 |
|---|---|
| `unity/Assets/OneStarMaker/Scripts/Runtime/SceneSystem/SceneDirector.Loading.cs` | **修正対象**。AddScene / LoadSceneBase / LoadUnityScene |
| `unity/Assets/OneStarMaker/Scripts/Runtime/SceneSystem/SceneDirector.Unloading.cs` | キャンセル窓判定（UnloadScene 冒頭）。H-4 で挙動が変わる側 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/SceneSystem/SceneLifecycleManager.cs` | 状態遷移の検証器。**変更しない**（これが競合の検出器として機能している） |
| `unity/Assets/OneStarMaker/Tests/Scene/SceneDirectorConcurrentAddSceneTests.cs` | T-01 のテスト（レッド5本 + 健全性1本）。仕様書として読む |
| `unity/Assets/OneStarMaker/Tests/Scene/SceneDirectorCancellationTests.cs` | H-4 のレッド2本を含むキャンセル系 |
| `unity/Assets/OneStarMaker/Tests/Scene/TestDoubles/TestableSceneDirector.cs` | identity 別ロードゲート `SceneLoadGates` / 呼び出し回数 `UnitySceneLoadCallCounts` |

## 6. 制約・注意

- **SceneLifecycleManager の遷移規則を緩めて例外を消す解決は禁止**。二重遷移が起きない構造にするのが正解で、検証器は今回バグを検出した功労者
- 画面遷移（SwitchScene / GoBack / TransitionPlan）の挙動は変えない。H-1 修正は AddScene / UnloadScene の並行性のみが対象
- 修正が SceneDirector の広範な再設計に発展しそうなら停止して報告（21-scene-streaming.md §11 撤退ライン1に該当）
- このプロジェクトは手動 DI（VContainer 不使用、03-di.md）・Cysharp スタック（UniTask/R3/ZLogger/ZString）・ドキュメント正典は `unity/Assets/ARCHITECTURE.md` 起点
- 完了したら 21-scene-streaming.md の §5 / §10 に判定記録を追記する（T-01 の完了記録の形式に倣う）

## 7. T-02 の後（参考）

T-03: `AddScene` への priority / テレメトリレベル公開(H-2, H-3。受入条件=既存呼び出しの挙動不変) → T-04 以降はセル制作・Controller 実装へ。チケット全体は 21-scene-streaming.md §10。
