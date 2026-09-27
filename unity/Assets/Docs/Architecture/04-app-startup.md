# 4. アプリケーション起動

> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る

---

## 4.1 設計方針

起動処理は Unity の `[RuntimeInitializeOnLoadMethod]` を使い、3 フェーズで実行する。

```
SubsystemRegistration  → 前回セッションのクリーンアップ（Domain Reload 無効対応）
BeforeSceneLoad        → 設定・サービス群の同期初期化（UI / Mapはまだロードしない）
AfterSceneLoad         → content登録・UI / Map・Factory / Director構築 → 既存Scene登録・初回Sceneロード
```

**設計意図:**
- Editor Playはロード済みSceneを登録する。通常directory Playには検証済みcontentの準備が必要で、任意の未取得Sceneが自動取得されるわけではない。
- 通常PlayerはBS4が生成する単一のUICommon bootstrap Sceneから開始し、固定configの論理`firstScene`をcontentからロードする。旧Build Settings Scene 0をそのままゲームの初回Sceneとする案内は使わない。
- サービスの Dispose は `Application.quitting` で保証する（SubsystemRegistration で二重保護）。

## 4.2 クラス構造

```text
AbstractApplicationInitializer (OneStarMaker.Runtime)
  ├── BootstrapSubsystemRegistration() → ReleaseAll（前回セッション解放）
  ├── BootstrapBeforeSceneLoad()
  │     └── AssetManagement / AppConfig / directory設定 / logger / UpdateSystem等の準備
  └── BootstrapAfterSceneLoad()
        ├── Editor directoryのみ: sessionを先に登録
        ├── UICommon / SceneResourceMapの取得
        │     ├── Editor directory: contentのbootstrap entry
        │     ├── BS4 Player: 生成済みUICommon / graph metadata
        │     └── 明示Addressables互換: 互換ロード
        ├── Framework・アプリサービス開始 → CreateSceneFactory()
        ├── 起動表現確定（BS4はここでdirectory登録）→ SceneDirector構築
        └── RegisterAlreadyLoadedScenes() / 必要な論理初回Sceneロード
```

実際の派生クラスは [SampleGameのAppInitializer](../../SampleGame/DependOnAll/AppInitializer.cs)、共通順序は [AbstractApplicationInitializer](../../OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs) を参照する。3つのRuntimeInitializeOnLoadMethodから対応するBootstrap関数を呼ぶ。Editorのdirectory logical key、互換Addressables key、BS4のrequired file config / graph metadataを同じアドレスとして扱わない。残存するEditor設定読取と旧Variant検証の不一致候補は§4.7 / §4.8に分けて示す。

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
  ├── AssetManagement.ReleaseAll()  … directory Scene は token だけ返す。Addressables Scene Unload も native UnloadSceneAsync も呼ばない
  ├── CompleteContentDirectoryPlayStop() … 受付停止、cache 退避、unregister、OS read lease 解放。UniTask.GetResult は使わない
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

完了済みの UniTask に対してだけ使う。未完了の UniTask で `GetResult` すると throw する（`Task.Result` のような完了待ちではない）。
Play 停止の directory drain は PlayerLoop 待ちを含むので、`GetResult` せず同期の `CompleteContentDirectoryPlayStop` で閉じる。

```csharp
// ✗ バグ: 待てていない（Awaiter を取得して捨てているだけ）
host.StartServicesAsync(token).GetAwaiter();

// ✓ 同期的に完了を待つ（完了済み、または完了まで PlayerLoop を必要としない場合）
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
// BeforeSceneLoad の失敗を次フェーズへ渡し、部分初期化を回収する
protected static void BootstrapBeforeSceneLoad(AbstractApplicationInitializer instance)
{
    instance._beforeSceneLoadSucceeded = false;
    try
    {
        instance.InitializeBeforeSceneLoad();
        instance._beforeSceneLoadSucceeded = true;
    }
    catch (Exception ex)
    {
        Debug.LogException(ex);
        try { instance.ReleaseAll(); }
        catch (Exception cleanupException) { Debug.LogException(cleanupException); }
    }
}

// BootstrapAfterSceneLoad は成功フラグが false なら非同期初期化を開始しない。
// 不正な明示設定を既定 Addressables 起動として扱わない。

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
Editorでは開いているSceneを初期登録の入力にする。通常directory Playでも、Content Directory / Deliveryの起動条件（§4.7）は先に満たす必要がある。

- `AddScene` は冪等（既に登録済みならスキップ）。
- `PerformUnitySceneLoad` が `SceneManager.GetSceneByName` でロード済みシーンを検出し、再ロードしない。
- `SceneResourceMap` に未登録のシーン（テストシーン等）はスキップしてログ出力する。
- BS4 Playerは生成済みbootstrap Sceneと、そこからロードする論理`firstScene`を区別する。`RegisterAlreadyLoadedScenes`だけでPlayerの初期contentロードが完了するわけではない。

## 4.7 設定の読み込み（AppConfig）

AppConfigはJSON・環境変数・コマンドラインをマージする。一般キーは後のソースが前のソースを上書きするが、**BS4 Playerのprotected keyは後述の固定値との不一致を拒否する**。JSONの取得方法もEditor / 互換とBS4 Playerで異なる。

```
優先順位（低 → 高）:
  1. JSON              … GetConfigFilePathのkey / BS4のrequired file config
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
Runtime.JsonFileConfigProvider    … 残存するAddressables TextAsset設定読取（整合性の未決は下記）
RequiredJsonFileConfigProvider    … BS4のbs4-runtime.jsonを必須読取・固定
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
    => "Assets/SampleGame/Config/app-config.json"; // 現行Editor側providerはAddressables keyを取る。下記の未決に注意
protected override string GetEnvironmentVariablePrefix() => "MYAPP_";
```

### 制限事項

- JSON パーサは標準 JSON のみ対応（コメント非対応）。

### Content Directory の起動時選択

`content:runtimeMode` 未指定の Editor Play は Delivery「Use For Next Play」の verified installed pair が無ければ BeforeSceneLoad で失敗する。pair があれば directory。明示 `addressables` だけが Addressables 互換口。未知 mode は失敗する。known-good の自動適用はしない。app-config の未指定を directory に書き換えない。
`directory` は Editor Play（verified pair または明示 directoryPath）と、専用 runtime config を組み込んだ directory Player の経路である。
未知 mode、必須値の欠損、不一致は起動失敗とし、Addressables へ暗黙 fallback しない。
BeforeSceneLoad で失敗した起動回は AfterSceneLoad を開始しない。

package-relative directory を使う従来経路は維持する。DIST が管理する install を使う場合は
`content:installedRevisionPath` と `content:manifestSha256` を pair で指定し、片方だけの指定は拒否する。
Player では required JSON から固定した `schemaVersion`、`runtimeMode`、`contentSet`、`buildIdentity`、
`target`、`representation`、`firstScene`、`relativeDirectory`、`probeToken` を起動時に保持し、
環境変数や command line との不一致を拒否する。この protected-key 契約は Player に限り、
Editor Play は明示された install result の identity と digest を使う。
installed override は receipt、受信した transport manifest bytes の digest、contentSet、revision、target、
全 content files を照合してから登録する。target は `StandaloneWindows64-Player` に固定する。

Editorのdirectory sessionはAfterSceneLoadでUICommon / SceneResourceMapより先に登録する。起動時に選んだ
representation は Scene lifecycle の間固定する。通常の Editor directory Play は UICommon Scene と
SceneResourceMap を Content Directory の bootstrap entry（SampleGame 固定 logical key）から読む。
UICommon / SceneResourceMapのAddressablesロードは明示`addressables`互換起動だけが使う。config読取には下記の実装整合の未決がある。
directory Player は生成した `UICommon` bootstrap Scene と graph metadata、
`StreamingAssets/bs4-runtime.json` を使い、対応する local content directory を登録して論理初回 Scene を
ロードする。この Player mode は生成 bootstrap Scene の runtime 名と Player 実行で latch し、環境変数や
command line だけでは有効にならない。

事前検証の結果は利用 lease ではない。directory session は登録 reservation を取得した後に
installed revision を再検証し、成功した場合だけ native 登録へ進む。登録、発行済み native operation、
root、token、drain、unregister は引き続き `ContentDirectorySession` が所有する。

directory Player の正常終了は、UICommon ready、directory 登録、初回 Scene stable、代表 Object の
load / instantiate / verify / destroy / release、初回 Scene unload、directory close の順序を完了してから
行う。完了段階は immutable snapshot と schema v2 receipt に記録する。fixture failure でも instance と
handle を finally で回収し、実際に完了した段階だけを記録する。

診断用 hold は代表 Prefab の検証後から destroy 前までに限り、有限 deadline と明示 signal で解除する。
失敗、取消、timeout の場合も instance と handle は既存の `finally` で回収する。

**Editor設定読取の未決:** 現行`BuildConfig()`はdirectory選択判定より前に`JsonFileConfigProvider`を使い、SampleGameの`GetConfigFilePath()`はAddressables keyを返す。したがって「通常Editor起動はconfigもAddressablesを読まない」とは現況を断定できない。この依存を許容した残存ownerか、通常経路からの切断漏れかは未決であり、文書更新で採用・正当化しない。BS4のrequired file configとfail-closed条件は変更しない。

## 4.8 起動時 Scene Variant と職種 companion set

Scene payload の Variant と Cell 職種のロード集合は、**起動時に一度だけ**決まる。Play Mode / Player の実行中に切り替えない。反映には再起動が必要。

### Scene Variant

**通常directory経路:** 最終的な表現は`content:representation`の`Full` / `Whitebox`を使う。Content buildで同梱し、Deliveryのverified installを選択して起動する。BS4 Playerではrequired configの固定表現と一致させる。active `BuildVariantProfile`を通常経路の選択手順にしない（[§18](18-asset-description.md) / [§20](20-variant-checkout-workflow.md)）。

**Addressables互換経路:** `ResolveSceneVariant()`でnon-nullの文字列を解決する。nullは拒否し、空文字`""`はProductionの正当なdefault payloadとする。以下の優先順位・profile規則はこの互換設定の説明である。

優先順位は既存 AppConfig と同じ（低 → 高）:

```
JSON `assets:sceneVariant`  <  環境変数  <  コマンドライン
```

- key 欠落時は空文字。trim や大小文字変換はしない。
- Player は AppConfig だけを読む。Editor resolver をコンパイルしない。
- Editor では active `BuildVariantProfile.SceneVariant` が config より優先する。`SceneVariantRuntimeBridge.EditorSceneVariantResolver` の戻り値 `null` は active profile なし（config へ戻る）、`""` は Production profile の明示選択。この二つを混ぜない。directory Play の representation は `content:representation` だけを使い、profile SceneVariant を暗黙入力しない。
- Player の通常経路は BS4 coordinator。旧 `VariantPlayerBuild` メニューは案内のみで `app-config.json` を書き換えない。
- 空でない `BuildVariantProfile.SceneVariant` は、同 profile の whitelist に ordinal 完全一致で含まれなければ settings / Editor startup を失敗させる。不整合を default へ隠さない。

`Production.asset` の Scene Variant は空、whitelist に Whitebox を含めない。`WorldWhitebox.asset` は Scene Variant `Whitebox`、whitelist は `""` と `"Whitebox"`。

**旧profile検証の未決:** 現行`InitializeAfterSceneLoad()`はdirectory表現を上書きする前に`ResolveSceneVariant()`を呼ぶ。Editor resolverは`DeveloperVariantSettings.GetActiveSceneVariant()`で旧profileを検証するため、不正な旧profileがdirectory起動へ影響する可能性がある。これは静的な不一致候補で、Unity再現・修正判断は未実施。有効な旧profileを通常起動の新しい必須条件にはしない。

### 職種 companion set

`AppInitializer.CreateSceneFactory` は `world:cellCompanionSet` を SceneDirector 構築前に一度 parse し、immutable な集合を `GameSceneFactory` → `InGameSession` → `SessionCellCompanionLoadDriver` へ constructor 注入する。static な可変状態は使わない。

| 値 | 含める職種 |
|---|---|
| `Full`（key 欠落時の既定） | Environment + Lighting + VFX + Events |
| `Planner` | Events |
| `Lighting` | Environment + Lighting |
| `VFX` | Environment + Lighting + VFX |

空・空白・大小文字違い・未知値は `FormatException` で起動失敗。暗黙に Full へ戻さない。invalid config のあと、Framework はロード済み UI / map / App owner asset を release する。
