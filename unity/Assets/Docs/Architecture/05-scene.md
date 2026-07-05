# 5. シーン管理

> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る

---

## 5.1 設計方針

シーンは**親子階層のツリー構造**で定義され、`SceneDirector` が一元管理する。
SceneDirector は責務ごとに **partial class** で分割する。

```
SceneDirector.cs             … フィールド, ctor, Dispose, ISceneQuery, テストアクセサ, ヘルパー
SceneDirector.Loading.cs     … AddScene, LoadSceneBase, LoadUnityScene, PerformUnitySceneLoad
SceneDirector.Unloading.cs   … UnloadScene, RemoveScene, 3-Phase, CleanupCanceledScene, PerformUnitySceneUnload
SceneDirector.Transitions.cs … SwitchScene, GoBack, ClearHistory, ExecuteTransitionPlan
```

フォルダ構成は `Scripts/Runtime/SceneSystem/` フラットのまま維持する。
ファイル数が 25 を超えた時点（Phase 2 の操作キュー・SceneValidator 追加時を想定）で
`Director/`, `Lifecycle/`, `Resource/` 等へのサブフォルダ分割を再検討する。

```
Main (ルート、コンテナ)
  ├── OutGame (コンテナ)
  │     └── Title (画面)
  ├── InGame (コンテナ)
  │     ├── InGameMain       [NecessaryAlways]
  │     ├── Pause            [OnDemand]
  │     └── Result           [OnDemand]
  └── Loading (画面)
```

## 5.2 SceneState（ライフサイクル）

```
None → PreLoading → PreLoaded → Loading → Loaded → WaitLoadChildScene
  → Initializing → Stable → PreUnloading → PreUnloaded
  → Unloading → Unloaded → AfterUnloading

※ LoadCanceled は PreLoading〜WaitLoadChildScene からのみ遷移可能
※ LoadCanceled → AfterUnloading（キャンセル後クリーンアップ）も有効
```

13状態 + LoadCanceled の合計14値。

```csharp
public enum SceneState
{
    None,                // SceneBase インスタンス作成直後
    PreLoading,          // OnPreLoaded 実行中
    PreLoaded,           // OnPreLoaded 完了。Unity Scene ロード前の事前準備済み
    Loading,             // OnLoaded 実行中（Addressable アセットのロード等）
    Loaded,              // OnLoaded 完了
    WaitLoadChildScene,  // 子シーンのロード待ち
    Initializing,        // UIView の ViewIn 実行中
    LoadCanceled,        // ロードがキャンセルされた
    Stable,              // 安定状態。ユーザー操作を受け付けられる唯一の状態
    PreUnloading,        // OnPreUnLoad 実行中（ViewOut + リソース解放準備）
    PreUnloaded,         // OnPreUnLoad 完了
    Unloading,           // Unity Scene アンロード中
    Unloaded,            // Unity Scene アンロード完了
    AfterUnloading,      // OnAfterUnLoad 実行中（最終クリーンアップ）
}
```

### 各状態が守るもの

| ガード条件 | 使用する状態範囲 | 目的 |
|---|---|---|
| 既にロード済み/ロード中ならスキップ | `>= Loading && < Unloading` | AddScene の二重呼び出し防止 |
| アンロード進行中なら完了を待つ | `>= AfterUnloading` | Unload 完了を待ってから再 Add |
| ロード中は Unload 禁止 | `>= Loading && <= WaitLoadChildScene` | ロード途中の不整合防止 |
| 既に Unload 開始済みならスキップ | `>= PreUnloaded` | removeScene の二重呼び出し防止 |
| PreLoad 未実行かの判定 | `== None` | 初回 PreLoad の判定 |

### 状態遷移のオーナーシップ

**SceneState の変更は SceneLifecycleManager のみが行う。SceneDirector や SceneBase が直接書き換えてはならない。**

```csharp
internal class SceneLifecycleManager
{
    private SceneState _state = SceneState.None;
    public SceneState State => _state;

    // ヘルパープロパティ: 範囲比較を隠蔽する
    public bool IsInLoadingPhase         // PreLoading 〜 WaitLoadChildScene
        => _state is >= SceneState.PreLoading and <= SceneState.WaitLoadChildScene;
    public bool IsActive                 // Initializing or Stable
        => _state is SceneState.Initializing or SceneState.Stable;
    public bool IsUnloadStarted          // PreUnloading 以降（LoadCanceled は除外）
        => _state >= SceneState.PreUnloading && _state != SceneState.LoadCanceled;
    public bool IsLoadedOrActive         // Loading 〜 Stable（LoadCanceled は除外）
        => _state is >= SceneState.Loading and <= SceneState.Stable
           && _state != SceneState.LoadCanceled;
    public bool IsNone                   // PreLoad 未実行
        => _state == SceneState.None;
    public bool IsLoadCanceled           // ロードがキャンセルされた
        => _state == SceneState.LoadCanceled;
    public bool IsInAfterUnloading       // AfterUnloading 以降
        => _state >= SceneState.AfterUnloading;

    public void TransitionTo(SceneState newState)
    {
        if (!IsValidTransition(_state, newState))
            throw new InvalidOperationException(
                $"Invalid scene state transition: {_state} → {newState}");

        _state = newState;
    }

    private static bool IsValidTransition(SceneState from, SceneState to)
    {
        return (from, to) switch
        {
            (SceneState.None, SceneState.PreLoading) => true,
            (SceneState.PreLoading, SceneState.PreLoaded) => true,
            (SceneState.PreLoaded, SceneState.Loading) => true,
            (SceneState.Loading, SceneState.Loaded) => true,
            (SceneState.Loaded, SceneState.WaitLoadChildScene) => true,
            (SceneState.WaitLoadChildScene, SceneState.Initializing) => true,
            (SceneState.Initializing, SceneState.Stable) => true,
            (SceneState.Stable, SceneState.PreUnloading) => true,
            (SceneState.PreUnloading, SceneState.PreUnloaded) => true,
            (SceneState.PreUnloaded, SceneState.Unloading) => true,
            (SceneState.Unloading, SceneState.Unloaded) => true,
            (SceneState.Unloaded, SceneState.AfterUnloading) => true,
            // キャンセルはロードフェーズからのみ
            (>= SceneState.PreLoading and <= SceneState.WaitLoadChildScene,
                SceneState.LoadCanceled) => true,
            _ => false,
        };
    }
}
```

## 5.3 SceneBase

```
1シーン = 1 SceneBase サブクラス
1シーン = 0 or 1 UIView（明示的なルール。複数 UI が必要なら子シーンに分ける）
```

SceneBase のライフサイクルフック:

| メソッド | 状態遷移 | CancellationToken | 用途 |
|---|---|---|---|
| `OnPreLoadedImpl(ct)` | PreLoading → PreLoaded | **あり** | 事前リソースの準備 |
| `OnLoadedImpl(ct)` | (Loaded 内) | **あり** | Addressable アセットのロード等 |
| `OnInitialize()` | (Initializing 内) | なし | MonoBehaviour の参照取得、Initialize メソッド呼び出し |
| `OnPreUnLoadedImpl()` | PreUnloading → PreUnloaded | **なし（非キャンセル）** | リソース解放の準備 |
| `OnAfterUnLoadedImpl()` | Unloaded → AfterUnloading | **なし（非キャンセル）** | 最終クリーンアップ |

アンロードは一度開始したら必ず完了する。途中キャンセルは許可しない。

### 禁止事項: ライフサイクルフック内から SceneDirector を直接呼ばない

```csharp
// ✗ やってはいけない: Unload 中に AddScene を呼ぶ → 再入問題
protected override async UniTask OnPreUnLoadedImpl()
{
    await sceneDirector.AddScene("Loading", null, CancellationToken.None);
}

// ✓ こうする: 遷移プランを宣言的に返す
public override SceneTransitionPlan? CreateTransitionPlan()
{
    return new SceneTransitionPlan
    {
        LoadingDisplay = LoadingDisplayType.BlackScreen,
        NextSceneId = "InGame",
    };
}
```

SceneDirector が Stable 到達後に `CreateTransitionPlan()` を確認し、非 null なら `ExecuteTransitionPlan` で安全な順序で実行する。

## 5.4 ILoadingDisplay（ローディング表示）

ローディング表示の実体は `ILoadingDisplay` インターフェースで抽象化し、Game 層で実装する。
Canvas オーバーレイでも専用シーンでも、実装は自由。

```csharp
public interface ILoadingDisplay
{
    UniTask Show(LoadingDisplayType displayType, CancellationToken ct);
    UniTask Hide(CancellationToken ct);
}

public enum LoadingDisplayType
{
    None,           // 何もしない。サイレントロード
    BlackScreen,    // 黒画面オーバーレイ。フェードイン → ロード → フェードアウト
    Indicator,      // 右下アイコン表示。ノンブロッキング
}
```

### SceneDirector のコンストラクタ

```csharp
public SceneDirector(
    ISceneFactory sceneFactory,
    UICommon uiCommon,
    SceneResourceMap sceneResourceMap,
    ILoadingDisplay loadingDisplay)    // ← string loadingSceneIdentify を置き換え
```

## 5.5 SwitchScene・GoBack（兄弟切り替えと履歴戻り）

同じ親の下で兄弟シーンを切り替える最頻出操作。ローディング表示の裏でアンロードとロードを行い、ちらつきを防ぐ。

```csharp
public async UniTask SwitchScene(
    string? fromSceneIdentify,          // null なら Unload なし
    string toSceneIdentify,
    CancellationToken ct,
    SceneContext? context = null,
    LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen)
```

実行順序:

```
1. _loadingDisplay.Show(loadingDisplay, ct)   ← キャンセル可能（フェードイン中のみ）
   ★ ポイント・オブ・ノーリターン ★
2. UnloadScene(fromSceneIdentify)             ← 非キャンセル（メモリ解放）
3. AddScene(toSceneIdentify, None)            ← 非キャンセル（新シーンロード）
4. _loadingDisplay.Hide(None)                 ← 非キャンセル
```

> **Unload → Add の順序は意図的。**
> 旧シーンのメモリを先に解放してから新シーンをロードし、
> 両シーンが同時に存在するメモリピークを回避する。
> ct が効くのは Show のフェードイン中のみ。Show 完了後は PoNR。
> 以降は `CancellationToken.None` で実行し、画面消失を原理的に排除する。

### 例: OutGame 内の画面切り替え

```csharp
// Equipment から Shop へ切り替え。親 (OutGameData) は共有され話替え不要。
await sceneDirector.SwitchScene(
    "Equipment", "Shop", ct,
    loadingDisplay: LoadingDisplayType.BlackScreen);
```

### SceneTransitionPlan からの宣言的切り替え

```csharp
// SceneBase 内で宣言。SceneDirector が Stable 後に自動実行する。
public override SceneTransitionPlan? CreateTransitionPlan() => new()
{
    LoadingDisplay = LoadingDisplayType.BlackScreen,
    NextSceneId = "InGame",
    Context = new SceneContext().Set(new InGameArgs(stageId)),
};
```

### 遷移履歴と GoBack

SwitchScene は実行毎に履歴スタックに `(from, to)` を記録する（PoNR 通過後に Push）。
GoBack はスタックから Pop し、逆方向の SwitchScene を実行する。

```csharp
public async UniTask GoBack(
    CancellationToken ct,
    SceneContext? context = null,
    LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen)
```

```
SwitchScene("Title", "InGame", ct)   → 履歴: [(Title, InGame)]
SwitchScene("InGame", "Result", ct)  → 履歴: [(Title, InGame), (InGame, Result)]
GoBack(ct)                           → Result→InGame に切替、履歴: [(Title, InGame)]
GoBack(ct)                           → InGame→Title に切替、履歴: []
```

- `CanGoBack` プロパティで UI から戻るボタンの表示制御が可能
- `ClearHistory()` でタイトル復帰時などに履歴をリセット
- GoBack のキャンセルポリシーは SwitchScene と同一（Show のみ ct 有効、キャンセル時は履歴そのまま）
- ExecuteTransitionPlan 経由の SwitchScene も履歴に記録される

## 5.6 ISceneQuery（Scene DI の注入経路）

シーンツリーは「シーン版 DI コンテナ」として機能する。親シーンがサービス（データ、リソース）を提供し、子シーンはツリーの正規パスを辿って取得する。

```
ISceneQuery（読み取り専用）
  ↑ implements
SceneDirector ─── 操作 API（AddScene / UnloadScene）は ISceneQuery に公開しない
  │
  └─ CreateSceneClass(sceneResource, this)
       ↓
     ISceneFactory → new XxxScene(sr, sceneQuery, ...)
       ↓
     SceneBase.SceneQuery（protected）
```

### 依存方向

| 方向 | 関係 | 目的 |
|---|---|---|
| SceneDirector → SceneBase | 所有（ライフサイクル管理） | ロード・アンロード |
| SceneBase → ISceneQuery | 読み取り専用参照 | 親・兄弟の SceneBase 取得 |
| SceneDirector implements ISceneQuery | 実装 | `_currentScenes` Dictionary を委譲 |

SceneBase は SceneDirector を **知らない**。ISceneQuery 経由では `GetLoadedScene` / `IsSceneLoaded` のみ可能。遷移 API には触れないため、再入問題や設計規律の崩壊を防ぐ。

### 子シーンが親のサービスを取得する例

```csharp
// OutGameData シーン（コンテナ）: MasterData を公開する
public class OutGameDataScene : SceneBase
{
    public MasterDataRepository MasterData { get; private set; } = null!;

    public OutGameDataScene(SceneResource sr, ISceneQuery sceneQuery)
        : base(sr, sceneQuery) { }

    protected override async UniTask OnLoadedImpl(CancellationToken ct)
    {
        MasterData = await MasterDataRepository.LoadAsync(ct);
    }
}

// Equipment シーン（子）: 親のサービスを SceneQuery 経由で取得する
public class EquipmentScene : SceneBase
{
    public EquipmentScene(SceneResource sr, ISceneQuery sceneQuery)
        : base(sr, sceneQuery) { }

    protected override UniTask OnLoadedImpl(CancellationToken ct)
    {
        // ツリーの正規パスを辿って親のサービスを取得
        var parentId = SceneResource.Parent!.Identity;
        var dataScene = (OutGameDataScene)SceneQuery.GetLoadedScene(parentId)!;
        var masterData = dataScene.MasterData;
        // masterData を使って UI を初期化...
        return UniTask.CompletedTask;
    }
}
```

### 設計規律

| ルール | 理由 |
|---|---|
| 子は親（または祖先）のみ参照する | ツリーの依存方向を守る。子→親は常に安全（親は先に Stable に到達する） |
| 兄弟の直接参照は避ける | 兄弟のロード順は保証されない。必要なら共通の親にサービスを持たせる |
| ISceneQuery から AddScene / UnloadScene は呼べない | 読み取り専用。遷移は SceneTransitionPlan で宣言的に行う |

## 5.7 LoadType

| LoadType | 動作 | 用途 |
|---|---|---|
| `NecessaryAlways` | 親ロード時に同期的（await）にロード | HUD 等、親と同時に必要なもの |
| `Incremental` | 親ロード時に非同期（Forget）でロード | バックグラウンドで先読みしたいもの |
| `OnDemand` | 明示的な `AddScene` 呼び出し時のみロード | ポーズメニュー、リザルト等 |

## 5.8 キャンセル処理

キャンセルは要件として対応する。AddScene のみキャンセル可能、アンロードは常に非キャンセル。

### キャンセル窓とポイント・オブ・ノーリターン（PoNR）

AddScene の処理を2つのフェーズに分離し、キャンセル可能な窓を設ける。

```
┌── キャンセル窓（PreLoad フェーズ）──┐┌── PoNR 通過後（非キャンセル）──────┐
│ SceneBase.ExecutePreLoad(ct)       ││ Addressable Scene ロード            │
│ 親シーンの再帰 PreLoad             ││ SceneBase.ExecuteLoaded             │
│ children の PreLoad                ││ UIView.ViewIn                       │
│ ← 外部 ct / LoadCts でキャンセル可 ││ → CancellationToken.None で実行     │
└────────────────────────────────────┘└────────────────────────────────────┘
```

実装上のポイント:

```csharp
// 外部 ct とリンクした内部 CTS を作る
var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

// キャンセル窓: PreLoad フェーズ（ScenePair.LoadCts に登録）
await LoadSceneBase(sceneIdentify, linkedCts.Token, ...);
pair.LoadCts = linkedCts;  // UnloadScene からの外部キャンセルを受付可能

// ★ PoNR 通過: LoadCts をクリア → 以降は外部からキャンセル不可
pair.LoadCts = null;
linkedCts.Dispose();

// 以降は CancellationToken.None で Unity Scene ロード
await LoadUnityScene(sceneIdentify, ..., CancellationToken.None, ...);
```

### UnloadScene とロード中シーンの連携

UnloadScene が呼ばれた時、対象シーンの状態によって処理が分岐する:

| シーン状態 | UnloadScene の動作 |
|---|---|
| Stable / Initializing | 通常アンロード（RemoveScene） |
| ロード中 + キャンセル窓内 | `LoadCts.Cancel()` → AddScene の catch がクリーンアップ |
| ロード中 + PoNR 通過後 | ペンディング登録 → AddScene が Stable 到達後に自動 RemoveScene |
| アンロード開始済み / ペンディング済み | スキップ |

### 3フェーズアンロード

sibling 間参照を保証するため、子孫のアンロードを3フェーズに分離する:

```
Phase 1: 全子孫の ViewOut + PreUnload（全 Unity Scene がまだ残っている → sibling 参照可能）
Phase 2: 全子孫の Unity Scene アンロード
Phase 3: 全子孫の AfterUnload + Dispose + 辞書除去
最後に self を同じ順序で処理
```

アンロードは完全非キャンセル。`CancellationToken` を引数に取らない。

### ルール

- `catch` で捕まえる。`finally` でキャンセルチェックしない（正常完了時にもガードが必要になり複雑化する）。
- クリーンアップ処理には `CancellationToken.None` を使う。キャンセル済みトークンを渡すと後始末自体が中断される。
- `CleanupCanceledScene`（キャンセル時）と `RemoveScene`（通常アンロード）は分離する。通常アンロードの状態ガードがキャンセルクリーンアップと矛盾するため。
- `CleanupCanceledScene` は `LoadCanceled → AfterUnloading` 遷移を経由し、`OnAfterUnLoadedImpl()` を呼ぶ。**PreLoad で確保したリソースは AfterUnload（または Dispose）で解放すること。**

### Forget() した非同期処理のエラーを握りつぶさない

```csharp
// ✗ やってはいけない: エラーがサイレントに消える
LoadUnityScene(childId, childScene, ct).Forget();

// ✓ こうする: UniTaskVoid メソッドで try-catch ラップ
private async UniTaskVoid IncrementalLoadAsync(string childId, SceneBase child, ...)
{
    try
    {
        await LoadUnityScene(childId, child, ct, isLoadChildScene);
    }
    catch (OperationCanceledException) { /* 親のキャンセルに連動 */ }
    catch (Exception ex)
    {
        Debug.LogError($"[SceneDirector] Incremental load failed: {childId}: {ex}");
    }
}
```

## 5.9 テスタビリティ

SceneDirector の Unity Scene I/O (Addressables / SceneManager) を `protected virtual` にし、テスト時にオーバーライドする。

```csharp
// SceneDirector 内:
protected virtual async UniTask<(AsyncOperationHandle<SceneInstance>? Handle, GameObject[] RootObjects)>
    PerformUnitySceneLoad(string sceneIdentify, SceneResource sceneResource) { ... }

protected virtual async UniTask PerformUnitySceneUnload(
    string sceneIdentify, AsyncOperationHandle<SceneInstance>? handle) { ... }

// テストダブル:
class TestableSceneDirector : SceneDirector
{
    protected override UniTask<(...) > PerformUnitySceneLoad(...) => /* fake */;
    protected override UniTask PerformUnitySceneUnload(...) => UniTask.CompletedTask;
}

// ローディング表示のテストダブル:
class FakeLoadingDisplay : ILoadingDisplay
{
    public int ShowCallCount { get; private set; }
    public int HideCallCount { get; private set; }
    public LoadingDisplayType? LastDisplayType { get; private set; }
    public UniTask Show(LoadingDisplayType displayType, CancellationToken ct) { ... }
    public UniTask Hide(CancellationToken ct) { ... }
}
```

内部テストアクセサ (`internal` + `InternalsVisibleTo("OneStarMaker.Tests")`):
- `ContainsScene(string)` / `GetSceneState(string)` / `HasPendingUnload(string)`

## 5.10 並行アクセスの安全性

Phase 1 では遷移プランによる直列化のみ。Phase 2 で操作キューの導入を検討する。

```
呼び出し側 → SceneDirector.RequestAddScene()    → OperationQueue → 1件ずつ実行
             SceneDirector.RequestUnloadScene() → OperationQueue → 1件ずつ実行
```

ただし、遷移プラン（§5.3）との組み合わせが前提。
キュー内から AddScene を呼ぶとデッドロックするため、Unload → Add の連鎖は遷移プランとして SceneDirector が一括実行する。

## 5.11 Shared Context（シーン間データ受け渡し）

AddScene の呼び出し時に、型付き DTO をコンテキストとして渡す。Android の `Intent.putExtra()` や ASP.NET の `TempData` に相当する。
ScenePayload（アセット定義用）とは責務を分離し、専用の `SceneContext` クラスで実現する。

### API

```csharp
// 遷移元: 型付きデータをセットして AddScene
public async UniTask AddScene(
    string sceneIdentify,
    Func<UniTask>? afterOnLoadedTask,
    CancellationToken ct,
    SceneContext? context = null,           // ← 新規パラメータ
    IProgress<SceneLoadProgress>? progress = null);

// 使用例:
var ctx = new SceneContext();
ctx.Set(new InGameArgs(StageId: 3, Difficulty: Difficulty.Hard));
await sceneDirector.AddScene("InGame", null, ct, context: ctx);

// 遷移先: OnPreLoadedImpl 以降で取得
protected override UniTask OnPreLoadedImpl(CancellationToken ct)
{
    var args = Context?.Consume<InGameArgs>();  // 1回取ったら削除（TempData 方式）
    _stageId = args?.StageId ?? 1;
    return UniTask.CompletedTask;
}
```

### SceneContext API

| メソッド | 動作 |
|---|---|
| `Set<T>(T value)` | 型をキーにしてデータを格納。同型は上書き |
| `Get<T>()` | 参照型データを取得。未登録なら null |
| `GetValueType<T>()` | 値型データを取得。未登録なら null |
| `Has<T>()` | 指定型が存在するか |
| `Consume<T>()` | 取得してバッグから削除（TempData 方式） |

**ルール:**
- コンテキストは `AddScene` のターゲットシーンのみにセットされる。親シーン・子シーンには渡らない。
- 親や子が Context に依存するコードを書いてはならない（構造的に強制不可のため規約で担保）。
- `Consume<T>()` を推奨。取得漏れさせない。`Get<T>()` は何度も参照する用途向け。
- DTO は immutable な record または class で定義する。

## 5.12 SceneDirector Observable（イベント監視）

R3 の `Subject<SceneEvent>` で SceneDirector の状態変化を外部から観測可能にする。

```csharp
// SceneDirector が公開する Observable
public Observable<SceneEvent> OnSceneEvent => _sceneEventSubject;

// SceneEvent は readonly struct（GC 負荷最小化）
public readonly struct SceneEvent
{
    public SceneEventType Type { get; }        // Added / Removed / CancelCleanedUp
    public string SceneIdentify { get; }
    public SceneState State { get; }
}

public enum SceneEventType
{
    StateChanged,       // 状態遷移
    Added,              // Stable 到達
    Removed,            // アンロード完了
    CancelCleanedUp,    // キャンセルクリーンアップ完了
}
```

### 発火ポイント

| タイミング | SceneEventType | 用途 |
|---|---|---|
| `LoadUnityScene` 完了時 | `Added` | 新シーンが Stable に到達 |
| `PhaseAfterUnloadAndDispose` 完了時 | `Removed` | シーンが辞書から除去 |
| `CleanupCanceledScene` 完了時 | `CancelCleanedUp` | キャンセル後処理完了 |

### 使用例

```csharp
// デバッグ UI
sceneDirector.OnSceneEvent
    .Where(e => e.Type == SceneEventType.Added)
    .Subscribe(e => debugLabel.text = $"Loaded: {e.SceneIdentify}");

// Analytics
sceneDirector.OnSceneEvent
    .Where(e => e.Type is SceneEventType.Added or SceneEventType.Removed)
    .Subscribe(e => Analytics.TrackSceneTransition(e.SceneIdentify, e.Type.ToString()));
```

**ルール:**
- `OnSceneEvent` は観測専用。サブスクライバが SceneDirector の動作に影響を与えてはならない。
- Subject は SceneDirector の Dispose 時に自動的に Dispose される。
- Phase 2 以降でシーン間通信が必要になった場合は R3 `EventChannel<T>` パターンを検討する（MessagePipe はそれでも不足な場合）。
