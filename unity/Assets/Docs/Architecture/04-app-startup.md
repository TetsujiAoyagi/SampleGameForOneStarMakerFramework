# 4. アプリケーション起動

> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る

---

## 4.1 設計方針

起動処理は Unity の `[RuntimeInitializeOnLoadMethod]` を使い、3 フェーズで実行する。

```
SubsystemRegistration  → 前回セッションのクリーンアップ（Domain Reload 無効対応）
BeforeSceneLoad        → サービス群の同期初期化（Addressable WaitForCompletion）
AfterSceneLoad         → ロード済みシーンの登録 + 初回シーンのロード
```

**設計意図:**
- Bootstrap シーンを置かず、Build Settings の Scene 0 を初回シーンとする。
- Editor でどのシーンから Play しても動作する（Play-from-any-scene）。
- サービスの Dispose は `Application.quitting` で保証する（SubsystemRegistration で二重保護）。

## 4.2 クラス構造

```
AbstractApplicationInitializer (OneStarMaker.Runtime)
  ├── BootstrapSubsystemRegistration()  … ReleaseAll（前回セッション解放）
  ├── BootstrapBeforeSceneLoad()        … 同期初期化
  │     ├── BuildConfig()               … 3ソース → AppConfig 生成
  │     ├── EnsureEventSystem()         … InputSystemUIInputModule
  │     ├── LoadUICommon()              … Addressable → WaitForCompletion → Instantiate
  │     ├── LoadSceneResourceMap()      … Addressable → WaitForCompletion
  │     ├── CreateSceneFactory()        … abstract（Game 層で実装）
  │     └── new SceneDirector(...)
  ├── BootstrapAfterSceneLoad()         … 非同期初期化
  │     ├── OnServicesInitializing()    … virtual（Phase 2: HostedService 登録）
  │     └── RegisterAlreadyLoadedScenes()  … Editor Play 済みシーンの登録
  │
  ├── GetUICommonPrefabAddress()        … abstract
  ├── GetSceneResourceMapAddress()      … abstract
  ├── CreateLoadingDisplay()            … abstract
  ├── GetConfigFilePath()               … virtual（デフォルト: StreamingAssets/app-config.json）
  └── GetEnvironmentVariablePrefix()    … virtual（デフォルト: ""）

ApplicationInitializer (Game.DependOnAll)
  └── 上記の abstract を実装
```

派生クラスの実装パターン:

```csharp
sealed class AppInitializer : AbstractApplicationInitializer
{
    static readonly AppInitializer s_instance = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Sub()    => BootstrapSubsystemRegistration(s_instance);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Before() => BootstrapBeforeSceneLoad(s_instance);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void After()  => BootstrapAfterSceneLoad(s_instance);

    protected override ISceneFactory CreateSceneFactory() => new MySceneFactory(Config!);
    protected override string GetUICommonPrefabAddress()  => "Assets/Prefabs/UICommon.prefab";
    protected override string GetSceneResourceMapAddress() => "Assets/SceneMap/Map.asset";
    protected override ILoadingDisplay CreateLoadingDisplay() => new MyLoadingDisplay();
    protected override string GetConfigFilePath()         => Path.Combine(Application.streamingAssetsPath, "app-config.json");
    protected override string GetEnvironmentVariablePrefix() => "ONESM_";
}
```

## 4.3 リソース解放の保証

| タイミング | 呼び出し元 | 目的 |
|---|---|---|
| `Application.quitting` | イベント | Play 終了時の正常クリーンアップ（Shutdown） |
| `SubsystemRegistration` | Unity | Domain Reload 無効時の前回セッション解放（二重保護） |

`ReleaseAll()` は複数回呼び出しても安全。null チェック + null 代入パターン。

**Shutdown 契約（通常の `UnloadScene` 3フェーズとは別）:**

Play Mode 終了時は Unity が先に Scene を解体する。そのあとで `Addressables.UnloadSceneAsync` を呼ぶと
`Cannot find handle for scene` になるため、teardown では Scene backend Unload を行わない。

```
Initializer.ReleaseAll()
  ├── cancel CTS / stop services
  ├── SceneDirector.Dispose()       … 論理 Scene 台帳と SceneBase のみ破棄（AM Unload は呼ばない）
  ├── AssetManagement.ReleaseAll()  … 未 Unload Scene を台帳上 MarkUnloaded + 全アセットを同期解放
  │                                   （Addressables Scene Unload は呼ばない）
  ├── UICommon 等の残存 GO 破棄
  └── AppTelemetry.Shutdown()
```

ゲーム中の正式アンロードは引き続き `SceneDirector.UnloadScene` → Phase 2 `UnloadSceneAsync` → Phase 3 `ReleaseScene` が担う。

## 4.4 サービス注入パターン（手動 DI）

`ISceneFactory` を通じた手動 DI でサービスを注入する。

```csharp
// Game 層の SceneFactory
class GameSceneFactory : ISceneFactory
{
    private readonly SoundService _soundService;

    public GameSceneFactory(SoundService soundService)
    {
        _soundService = soundService;
    }

    public SceneBase? CreateSceneClass(SceneResource sr, ISceneQuery sceneQuery) => sr.Identity switch
    {
        "Title" => new TitleScene(sr, sceneQuery, _soundService),
        "InGame" => new InGameScene(sr, sceneQuery, _soundService),
        _ => null,
    };
}
```

> **決定 (2026-07-06):** 当初予定していた VContainer への移行は取り止め、手動 DI を正式採用（[03-di.md](03-di.md) 参照）。Factory の配線が破綻し始めた場合に再評価する。

## 4.5 実装ルール

### コンストラクタは軽量にする

```csharp
// ✗ やってはいけない
public AbstractApplicationInitializer(string loadingSceneId)
{
    var task = SomeAsyncMethod();
    task.Wait();                  // メインスレッドの同期ブロック
    CreateGameObject();           // Unity API をコンストラクタで呼ぶ
}

// ✓ こうする
public AbstractApplicationInitializer(string loadingSceneId)
{
    this.loadingSceneId = loadingSceneId;  // 値の保持のみ
}
```

**理由:**
- static フィールドの初期化経由でコンストラクタが呼ばれた場合、Unity API の呼び出しタイミングが保証されない。
- コンストラクタでの例外は呼び出し元で捕捉しづらい。

### async が不要なメソッドを async にしない

```csharp
// ✗ やってはいけない
public static async Task<Config> CreateSettings()
{
    var settings = new Config();
    settings.Port = 8080;               // 同期処理
    await Task.Delay(19);               // 意味のない遅延
    settings.IP = ReadEnvVariable();    // 同期処理
    return settings;
}

// ✓ こうする
public static Config CreateSettings()
{
    var settings = new Config { Port = 8080 };
    settings.IP = ReadEnvVariable();
    return settings;
}
```

### UniTask の同期待ちが必要な場合は `.GetAwaiter().GetResult()` を使う

```csharp
// ✗ バグ: 待てていない（Awaiter を取得して捨てているだけ）
host.StartServicesAsync(token).GetAwaiter();

// ✓ 同期的に完了を待つ
host.StartServicesAsync(token).GetAwaiter().GetResult();

// ✓ 完了を待たない場合は明示的に Forget
host.StartServicesAsync(token).Forget();
```

### CancellationTokenSource は適切に管理する

```csharp
// SubsystemRegistration で前回の CTS を解放してから新しいものを作る
private void ReleaseAll()
{
    _cts?.Cancel();
    _cts?.Dispose();
    _cts = null;
}
```

### 起動シーケンスにはエラーハンドリングを入れる

```csharp
// BeforeSceneLoad / AfterSceneLoad の Bootstrap メソッドは try-catch で囲む
protected static void BootstrapBeforeSceneLoad(AbstractApplicationInitializer instance)
{
    try
    {
        instance.InitializeBeforeSceneLoad();
    }
    catch (Exception ex)
    {
        Debug.LogException(ex);
    }
}

// AfterSceneLoad の async 本体も catch で例外を捕捉
private async UniTaskVoid InitializeAfterSceneLoad()
{
    try { /* ... */ }
    catch (OperationCanceledException) { /* アプリ終了 — 正常 */ }
    catch (Exception ex)
    {
        Debug.LogError($"[AppInit] AfterSceneLoad failed: {ex}");
    }
}
```

## 4.6 Play-from-any-scene（Editor 対応）

`AfterSceneLoad` で `SceneManager.sceneCount` を走査し、既にロード済みのシーンを `SceneDirector.AddScene` で登録する。
**Build Settings の Scene 0 を別途 `AddScene` する処理は持たない。** Editor で開いたシーンがそのまま初回シーンになる。

- `AddScene` は冪等（既に登録済みならスキップ）。
- `PerformUnitySceneLoad` が `SceneManager.GetSceneByName` でロード済みシーンを検出し、再ロードしない。
- `SceneResourceMap` に未登録のシーン（テストシーン等）はスキップしてログ出力する。
- ビルド時は Build Settings の Scene 0 が唯一の初回 Unity シーン。`RegisterAlreadyLoadedScenes` は Scene 0 を登録するだけで二重ロードは発生しない。

## 4.7 設定の読み込み（AppConfig）

アプリケーション設定は3つのソースからレイヤード方式でマージする。後のソースが前のソースを上書きする。

```
優先順位（低 → 高）:
  1. JSON ファイル     … StreamingAssets/app-config.json
  2. 環境変数          … プレフィックス付き（例: ONESM_SERVER__PORT=8080）
  3. コマンドライン引数 … --Server.Port=8080
```

### クラス構成

```
AppConfig                         … 統合クラス。型安全アクセス（GetString/GetInt/GetBool/GetFloat/GetSection）
IConfigProvider                   … プロバイダインターフェース
JsonConfigFlattener               … JSON 文字列を ":" 区切りへ展開する純粋ロジック
├── EnvironmentVariableConfigProvider … "__" → ":" 変換、プレフィックスフィルタ
└── CommandLineConfigProvider     … --Key=Value / --Key Value / --Flag 形式
Runtime.JsonFileConfigProvider    … Addressables 経由で JSON TextAsset を取得し、フラットキー化
```

### キー形式

| ソース | 表記例 | 内部キー |
|---|---|---|
| JSON | `{ "Server": { "Port": 8080 } }` | `Server:Port` |
| 環境変数 | `ONESM_SERVER__PORT=8080` | `Server:Port` |
| コマンドライン | `--Server.Port=8080` | `Server:Port` |

- 内部区切り文字は `:` （Microsoft.Extensions.Configuration 互換）。
- API 呼び出し時は `.` でも `:` でもどちらでもよい（内部で正規化）。
- キーは大文字小文字を区別しない。

### 使い方

```csharp
// AbstractApplicationInitializer 内で自動構築される。
// 派生クラスからは Config プロパティで参照可能。
protected override ISceneFactory CreateSceneFactory()
{
    var host = Config!.GetString("Server:Host", "localhost");
    var port = Config!.GetInt("Server:Port", 8080);
    return new MySceneFactory(host, port);
}

// カスタマイズ
protected override string GetConfigFilePath()
    => Path.Combine(Application.streamingAssetsPath, "my-config.json");
protected override string GetEnvironmentVariablePrefix() => "MYAPP_";
```

### 制限事項

- JSON パーサは標準 JSON のみ対応（コメント非対応）。
