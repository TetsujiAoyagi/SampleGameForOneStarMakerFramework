# 5. シーン管理

> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る

---

## 5.1 設計方針

シーンは**親子階層のツリー構造**で定義され、`SceneDirector` が一元管理する。
SceneDirector は責務ごとに **partial class** で分割する。

ディスク上のフォルダも Scene 親子に揃え、必要なアセット（Scene / Texture / Mesh 等）はそのツリー配下に同居させる。配置ルールは [27-folder-structure.md](27-folder-structure.md)（軸 B）を参照。

```
SceneDirector.cs                    … フィールド, ctor, Dispose, ISceneQuery, テストアクセサ, ヘルパー
SceneDirector.Loading.cs            … AddScene, LoadSceneBase, LoadUnityScene, PerformUnitySceneLoad
SceneDirector.FailedLoadCleanup.cs  … 通常例外で失敗した Add の newlyCreated 回収
SceneDirector.Unloading.cs          … UnloadScene, RemoveScene, 3-Phase, CleanupCanceledScene, PerformUnitySceneUnload
SceneDirector.Transitions.cs        … SwitchScene, GoBack, ClearHistory, ExecuteTransitionPlan
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

※ Initializing → PreUnloading も有効（ViewIn 中の Unload / AddUIView 失敗後の RemoveScene）
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
            (SceneState.Initializing, SceneState.PreUnloading) => true,
            (SceneState.Stable, SceneState.PreUnloading) => true,
            (SceneState.PreUnloading, SceneState.PreUnloaded) => true,
            (SceneState.PreUnloaded, SceneState.Unloading) => true,
            (SceneState.Unloading, SceneState.Unloaded) => true,
            (SceneState.Unloaded, SceneState.AfterUnloading) => true,
            // キャンセルはロードフェーズからのみ
            (>= SceneState.PreLoading and <= SceneState.WaitLoadChildScene,
                SceneState.LoadCanceled) => true,
            (SceneState.LoadCanceled, SceneState.AfterUnloading) => true,
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
    ILoadingDisplay loadingDisplay,
    IAssetManagement assetManagement,
    string sceneVariant = "")
```

`sceneVariant` は起動時に一度解決した Scene payload Variant。null は `ArgumentNullException`、空文字は正当な Production 値。公開 setter/getter は持たない。`PerformUnitySceneLoad` は常にこの値を `IAssetManagement.LoadSceneAsync` へ渡す。`FindPayload` と AssetManagement の「要求 Variant が無ければ空 Variant」fallback は変えない。実行中の切替口は無い。解決手順は [§4.8](04-app-startup.md#48-起動時-scene-variant-と職種-companion-set)。

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

### 何を親に置くか — シーンツリーはスコープ木である

シーンツリーは**画面遷移の分類図ではなく、Ready 状態と寿命を共有するスコープの木**である。この区別が、親にサービス初期化を置いてよいかを決める。

そう扱える根拠は §5.5 の切り替え規則そのものにある。`SwitchScene` は兄弟だけを差し替え、**親はアンロードしない**。親を明示的に降ろしたときだけ子孫も再帰的に降りる（§5.8 の3フェーズアンロード）。つまり親は「毎回まとめて生死する単位」ではなく、**枝にいるあいだ居残る共有スコープ**として振る舞う。

親にサービス初期化を置く設計は正しい。ただし条件が一つある。

> 親が初期化してよいのは、**その親のスコープに入る全ての子が必要とするもの**だけ。

役割分担はこうなる。

| 層 | 担うもの |
|---|---|
| 手動 DI（[03-di.md](03-di.md)） | インスタンスの配線・所有関係 |
| 親 Scene | そのサービスをいつ Initialize / Load して Ready にするか |
| 子 Scene | 親の Ready を前提にしてよい |

この分担が成り立つので、分割初期化はシーンツリーだけで表現できる。**依存 DAG や `Requires` リストは導入しない。**

#### 判断基準

新しい初期化物が出たら「ツリーに載せるか」ではなく、こう問う。

> **この状態は、どの画面集合で必ず共有され、いつ無効化されるべきか？**

| 答え | 置き場所 |
|---|---|
| その親の全子孫で必要。親を出たら不要 | 親 Scene がロード責務を持つ |
| 特定の子孫だけで必要 | もっと下の子 / Bootstrap Scene |
| 画面木をまたぐ。アプリ / アカウント単位 | App サービス（Camera / Logger / AssetManagement / UICommon が該当）。必要時に遅延初期化 |
| 複数枝で共有するが寿命が一致しない | 無理にツリーに載せない |

#### 「親に置くと巻き込まれる」ときは木が実態と合っていない

```text
OutGame
├─ Title       … Auth 不要
└─ Home        … Auth 必要
```

これは「親に Auth を置くと Title が巻き込まれる」問題ではない。**OutGame を Auth のスコープにしたいのに、Auth 不要の Title を同じスコープに入れている**のが問題である。対処は3つのいずれか。

1. Auth が枝全体で必須なら、**木を実態に合わせて切る**（`AuthenticatedSession` 配下に Home / Shop / Battle を置く）
2. Auth が画面木をまたぐなら、ツリーから外して App サービスにする（構築と初期化は別。遅延初期化してよい）
3. 一部の子だけ違うなら、その差は子 / Bootstrap 側で吸収する

**捨てる前提:** 画面の見た目上の分類と、サービスの寿命スコープは必ず同じ木にできる。
**残す前提:** シーンツリーは Ready 状態を保証するスコープ木であり、親はスコープ全体の初期化責務を持てる。

## 5.7 LoadType

| LoadType | 動作 | 用途 |
|---|---|---|
| `NecessaryAlways` | 親ロード時に同期的（await）にロード | HUD 等、親と同時に必要なもの |
| `IncrementalAlways` | 親ロード時に非同期（Forget）でロード | バックグラウンドで先読みしたいもの |
| `OnDemand` | 明示的な `AddScene` 呼び出し時のみロード | ポーズメニュー、リザルト等 |

**`IncrementalAlways` の既知の不具合（未修正。修正スライスは未作成）:** 親の unload は、ロード中で取消し窓を過ぎた `IncrementalAlways` の子を保留の unload（`_pendingUnloads`）に積む。保留を消費するのは公開 `AddScene` の対象 Scene の経路だけで、子の `IncrementalLoadAsync` は消費しない。そのため子は Stable まで進んで残り、以後の `UnloadScene` も保留に入っているとして早期 return する。project の `SceneResource` に `IncrementalAlways` は 0 件（Season Atmosphere VFX はこれを避けて `NecessaryAlways` にした）。

LoadType は**親に対する引っ張り**専用である。距離で載せるかは LoadType の値を増やさず、現状は格子キーと `CellIdentity.Format` で決める（[§21](21-scene-streaming.md)）。到着は [§34 OnDemand の空間政策](34-ondemand-spatial-policy.md) が候補集合と体積で決める。Pause / Result のような明示 `AddScene` は、距離政策の候補に入っていない OnDemand である。

### AddScene の祖先ロード

深いシーンを `AddScene` すると、`CollectNecessaryScenes` で祖先チェーン（ルート→…→直接の親）を収集し、
**各祖先も `isLoadChildren: true` / `isLoadChildScene: true` でロードする。**
ターゲット自身と同じ LoadType 再帰規則が祖先にも適用される。

| LoadType | 祖先ロード中の挙動 |
|---|---|
| `NecessaryAlways` | 同期的（await）に PreLoad + Unity Scene ロード |
| `IncrementalAlways` | バックグラウンド（Forget）で PreLoad + Unity Scene ロード |
| `OnDemand` | ロードしない（明示的 `AddScene` のみ） |

例: `Session` 配下に `Always` [NecessaryAlways] と `Demand` / `OtherDemand` [OnDemand] があるとき、
`AddScene("Demand")` は `Session` と `Always` を Stable までロードするが、`OtherDemand` はロードしない。

例: `Root → Mid` [NecessaryAlways] `→ Leaf` [OnDemand] のとき、
`AddScene("Leaf")` は `Root` / `Mid` / `Leaf` の3つすべて Stable になる。

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
| 通常例外で Add 失敗 | `RecoverFailedLoadAsync` が辞書と pending を消す。pending 除去は `finally` |

通常例外の回収本体は `SceneDirector.FailedLoadCleanup.cs`。この Add が作った newlyCreated だけを対象にし、公開 API / SceneState 値 / SceneEventType は増やさない。Stable 個体は触らない。Initializing は 3-phase。Loading は `CleanupCanceledScene`。途中失敗した unload は `ContinuePreUnLoadFromCurrent` / `ContinueAfterUnLoadFromCurrent` で同じ状態へ再遷移しない。二次例外はログして元の Add 例外を隠さない。1個体の再開失敗で兄弟回収と pending 除去を止めない。

### 3フェーズアンロード

sibling 間参照を保証するため、子孫のアンロードを3フェーズに分離する:

```
Phase 1: 全子孫の ViewOut + PreUnload（全 Unity Scene がまだ残っている → sibling 参照可能）
Phase 2: 全子孫の Unity Scene アンロード（AssetManagement.UnloadSceneAsync / Addressables）
Phase 3: 全子孫の AfterUnload + ReleaseScene（所有アセットのみ）+ Dispose + 辞書除去
最後に self を同じ順序で処理
```

Phase 3 の `ReleaseScene` は所有アセット解放専用で、backend Scene Unload は行わない（Phase 2 済みが前提）。
未 Unload の Scene に対して `ReleaseScene` を呼ぶと契約違反として例外になる。

`SceneDirector.Dispose`（Play 終了の Initializer.ReleaseAll から呼ばれる）は論理台帳と SceneBase のみ破棄し、
`AssetManagement.ReleaseScene` / Addressables Unload は呼ばない。実リソースの teardown は直後の
`AssetManagement.ReleaseAll()`（Shutdown 契約: Scene Unload なし・同期解放）に一元化する。

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
        await RecoverFailedLoadAsync(new List<string> { childId });
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

## 5.13 Cell と職種 companion の分類

距離ストリーミングの Cell と、その職種 child（Environment / Lighting / VFX / Events）は、identity を座標 parse せず `SceneResource` の構造だけで分類する。Framework は職種名を知らない。

| 判定 | 規則 |
|---|---|
| Cell | `SceneResource.StreamByDistance == true`。`CellScene` は false を constructor で拒否する |
| Cell child | `Parent != null && Parent.StreamByDistance`。同じ lifecycle の `CellCompanionScene` |
| Season Atmosphere VFX | identity が `{Season}_AtmosphereVFX` で、親が `Season_{Season}` かつ親が `StreamByDistance` でない。no-UI の `SeasonAtmosphereVfxScene`（親が streaming の Cell なら constructor で拒否） |
| Season major Event | identity が `{Season}_Event_{slug}`（season は `_` を含まず slug は空でない）、親が `Season_{同じ season}` で `StreamByDistance` でない、`LoadType.OnDemand`。`SeasonMajorEventScene`。名前・親・`LoadType` の全てを要し、構造だけでは判定しない（`Spring_Lighting` を飲み込まない。`NecessaryAlways` の誤設定は分類されず、ロード時に factory の null として失敗が表に出る）。Atmosphere の判定の後、identity の完全一致の前に置く |
| その他 | identity の完全一致（Title / InGameSession 等） |

Unity オブジェクトの null 判定は `== null` / `!= null`。公開 bounds が必要な箇所は `SceneResource.Volume` を返し、格子定数から再計算しない。`DemoCellScene` は identity 由来の tint を持たない。見た目を焼く責務は後続の世界制作スライス。

`SessionCellCompanionLoadDriver` は resident Cell の `SceneResource.Children` だけを列挙する。Map 全走査、親名からの child 生成、第二の距離政策は持たない。parent が Stable になる前は child を Add しない。同じ child への同時 Add は最大一件。

Add 完了後は、fresh resident snapshot に parent が残っていること、parent が Stable であること、捕捉した `SceneBase` と `ISceneQuery.GetLoadedScene(parent)` が `ReferenceEquals` で同一であること、session が未キャンセルであることを再確認する。一つでも偽なら、その Add が載せた child を `LoadingDisplayType.None` で Unload する。通常の parent unload は既存の再帰 unload を正とし、driver は先回り remove を出さない。`SceneBase` は `UnityEngine.Object` ではないため、この `ReferenceEquals` は偽 null 禁止に抵触しない。

職種の選別は SampleGame の制作規約に閉じる。identity を `_` で分割し、末尾二要素の直前一要素だけを `Environment` / `Lighting` / `VFX` / `Events` と完全一致比較する。座標や新しい child identity は生成しない。未知 role は warning して飛ばす。同一 Cell の同一 role が複数ならその role を一件も Add しない。欠落 role は optional として正常。どの集合を載せるかは起動時の `world:cellCompanionSet`（[§4.8](04-app-startup.md#48-起動時-scene-variant-と職種-companion-set)）。

Events の child も Scene class は他の職種と同じ `CellCompanionScene` で、職種による分岐は持たない（role の分類器は SampleGame.InGame の internal で、composition から呼べない）。振る舞いは root の内容で決まる。`CellCompanionScene` は `OnInitialize` で root から配置済み VFX の marker と `LocalVfxEventTrigger` を集め、trigger があるときだけ binder を作る（`OnLoadedImpl` で trigger の asset を Scene 所有でロードし、`OnPreUnLoadedImpl` で全 trigger を終わらせる）。trigger も配置済み VFX も置かない companion では何もしない。Events Scene は進行制御（trigger 等）を持ち、Renderer を置かないので親 Cell の `_volume` を変えず、VFX Scene に依存しない（`Planner` でも Events だけで trigger が動く）。trigger から起動する VFX の寿命は [§24 §16.9](24-rendering-system.md#169-cell-events-の-trigger-から起動する-vfx)。

Season Atmosphere VFX は Cell の companion ではない。Season の `NecessaryAlways` の子なので、companion set に関係なく Season と一緒に載り（`Planner` でも載る）、Season の Stable はそのロードを待つ。`SessionSeasonController` は `NecessaryAlways` の子として観測個体に登録するので、Season の枝の交換は Atmosphere の終端も待つ。`StreamByDistance == false` なので距離の候補には入らない。

Season major Event（実例は `Spring_Event_MultiCellProof`）は Cell の companion でも Season の `NecessaryAlways` の子でもない。Season の `OnDemand` の子で、Cell bundle と独立に Add / Unload し、寿命は Season の枝が所有する。

- **所有者。** `SessionSeasonController` が枝ごとに internal の `MajorEventDriver`（純 C#）を作り、枝の終了と `StopWorldOperations` で停止する。1 つの枝に同時に生きる major Event は最大 1 件。driver の状態は Idle → Loading（受理）→ Live（Stable を観測）→ Ending（完了・枝の終了）→ Idle（終端の `Removed` / `CancelCleanedUp` を観測）。
- **受理の優先順位。** 上から順に見て最初に当たった段で答える。(1) driver の停止 → `Rejected(Stopped)`。(2) 枝と Season の生存: 枝の操作中 `BranchBusy`、Season の未ロード（unload の開始後を含む）`SeasonNotLoaded`、捕捉した個体からの差し替え `SeasonReplaced`、Stable でない `SeasonNotStable`。(3) driver の状態: Idle でない間、別 identity は `OtherEventActive`、同じ identity の Ending は `Ending`、Loading・Live は `AlreadyActive`。(4) 構造: active Season の直下でない・`OnDemand` でない・`StreamByDistance` は `NotSeasonChild` / `NotOnDemand` / `StreamByDistance`。段 2 を段 3 より先に見るのは、親が Event を生かせない状態で「動いている」と答えないため。拒否は Warning を 1 回出し、状態を遷移させない。Event の Add 中も Season は Stable のまま（SceneDirector は親の状態を遷移させない）なので、親が健全な間の段 3 の答えは変わらない。
- **発行。** 受理・Loading の記録・`AddScene` の開始を 1 つの同期区間で行う。controller の private な発行は、registry へ op を登録 → `AddScene` を開始（最初の await まで同期に進み、SceneDirector が個体を辞書に入れる）→ **await より前に**個体を捕捉して観測個体に登録し、完了の delegate を個体へ付ける → await。await の後に付けると `OnStabledImpl` の受付が delegate より先に始まる。捕捉できなかった場合も driver は元の Add の task の終わりを観測するまで Idle に戻らず、2 本目の Add を出さない（例外は Error 1 回）。driver が受ける delegate は「発行して捕捉した個体を返す」「unload する」「個体へ完了の delegate を付ける」の 3 つだけ。
- **枝の切替と停止。** `TearDownActiveBranchAsync` は driver を停止してから既存の順で Season を unload する。Event は発行時に観測個体に登録済みなので、切替は Event がどの位相にあってもその終端を待ってから次の Season を Add する。Session の unload（drain しない）では Framework の親子の unload が回収し、停止後の driver は完了の通知で unload を発行しない。
- **祖先の unload との関係。** Session・`InGame` など祖先の unload では、SceneDirector が子孫の収集と同じ同期区間で Season の `PhasePreUnload` に入り、`GetLoadedScene(Season)` が null になる。以後の要求は段 2 で拒否され、unload 中の親の下へ Add が出ない。これは現在の構成（Season は UIView を持たず `PreUnloading` へ同期に進む）に依存する観測で、major Event の契約ではない。要求は物理の callback や frame の処理から届き、`OnSceneEvent` の同期の購読者からは要求しない。
- **入口と開始。** 要求の入口は `InGameSession` の internal `RequestMajorEvent(string)` だけで、public interface も `IInGameSessionServices` も持たない。開始は Cell Events に置いた `MajorEventStartTrigger` で、`CellCompanionScene` が作る `MajorEventStartBinder` が Events → Cell → Season → `InGameSession` の固定の段数で親をたどって要求する（汎用の祖先歩行は無い）。Cell Events に置くので `Planner` の companion set でも動く。Scene を跨ぐ参照は event の identity 文字列だけ。
- **Event Scene 側。** `SeasonMajorEventScene` は完了の受付を `OnStabledImpl` から始め、その後の最初の進入（`MajorEventCompletionTrigger`）で delegate を 1 回呼ぶ。完了の通知は Loading・Live のどちらで届いても Ending にする。Event Scene から driver への逆向きの口は無い。VFX の寿命は [§24 §16.10](24-rendering-system.md#1610-season-直下の-major-event-の-vfx)。
- **起点の Cell からの独立。** 起点の Cell とその Events が unload されても Event は残る（親は Season）。
- **確かめていること。** `MajorEventDriverTests`（受理の優先順位の組み合わせ、`AlreadyActive`・終端の後の再発行、Loading 中の完了、捕捉なしの発行）、`SessionSeasonControllerTests`（Live・Add 中の各位相での枝の切替が Event の終端を待つ）、`GameSceneLoggingTests` の分類、実 Scene の `SeasonMajorEventPlayTests`（起動と Cell unload からの独立、完了と枝の切替での回収、非対応の `VfxSystem`、Event のロード中の Session unload、`Planner` での開始 trigger からの起動）。
