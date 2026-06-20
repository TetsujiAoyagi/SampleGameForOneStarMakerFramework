# SampleGame

横スクロール STG「NewGradious」の再設計プロジェクト。  
旧プロジェクト（NewGradious）の設計上の問題を洗い出し、汎用フレームワーク + ゲーム層のクリーンアーキテクチャで一から構築する。

---

## 技術スタック

| 項目 | バージョン / 選定 |
|---|---|
| Unity | **6.5 (6000.5.0f1)** |
| Render Pipeline | **URP 17.5.0** |
| Scripting Backend | **IL2CPP** (Android) |
| .NET | **.NET Standard 2.1** |
| DI | **VContainer 1.0.2** |
| Async | **UniTask 2.5.10** |
| Reactive | **R3 1.3.0** + ObservableCollections |
| Tween | **LitMotion** |
| Logging | **ZLogger 2.5.10** → `IAppLogger<T>` で隠蔽 |
| Asset Management | **Addressables 2.9.1** |
| UI | **uGUI**（Phase 4 で UI Toolkit 段階移行予定） |
| Input | **Unity InputSystem** |
| NuGet | **NuGetForUnity** |

---

## プロジェクト構造

```
Assets/
├── OneStarMaker/                ← 汎用ゲームフレームワーク (3 Assembly)
│   ├── Foundation/              ← OneStarMaker.Foundation.asmdef (leaf)
│   │   ├── Config/              … AppConfig + 3 ConfigProvider
│   │   └── Logging/             … IAppLogger<T>, AppLogger, AppLoggerFactory, NullAppLogger
│   ├── Runtime/                 ← OneStarMaker.Runtime.asmdef (→ Foundation)
│   │   ├── AbstractApplicationInitializer.cs  … 3フェーズ起動基盤
│   │   ├── Scene/               … SceneDirector (partial×4), SceneBase, SceneLifecycleManager 他
│   │   ├── UI/                  … UICommon (SiblingIndex 管理), UIView (6レイヤー)
│   │   └── AssetDescriptions/   … SceneAssetDescription, ScenePayload, LoadType
│   ├── Debug/                   ← OneStarMaker.Debug.asmdef (→ Foundation + Runtime + TMP)
│   │   └── Profiler/            … DebugProfilerView, FrameTimeSampler, FrameTimeGraphRenderer
│   ├── Editor/                  ← OneStarMaker.Editor.asmdef (→ Runtime)
│   │   └── SceneGraph/          … Scene Graph Editor (ノードベース可視化・SceneResource 生成)
│   └── Tests/                   ← OneStarMaker.Tests.asmdef (→ Foundation + Runtime)
│       └── Scene/               … SceneDirector テスト
│
├── SampleGame/                  ← ゲーム固有実装
│   ├── DependOnAll/             … AppInitializer, GameSceneFactory, NullLoadingDisplay
│   ├── Common/                  … ゲーム共通サービス・シーン定義（未実装）
│   ├── OutGame/
│   │   └── Scenes/TitleScene.cs … タイトル画面
│   └── InGame/                  … インゲーム（未実装）
│
└── Docs/Architecture/           ← 設計ドキュメント群
    ├── 03-di.md                 … DI・依存管理
    ├── 04-app-startup.md        … アプリケーション起動
    ├── 05-scene.md              … シーン管理
    ├── 06-ui.md                 … UI 管理（6レイヤー、SiblingIndex、Blocker）
    ├── 07-09-services.md        … サウンド / 入力 / HostedService
    ├── 10-coding-rules.md       … コーディング規約
    └── 11-scene-graph-editor.md … Scene Graph Editor 仕様
```

---

## Assembly 依存ルール

```
OneStarMaker.Foundation  (leaf — フレームワーク内依存なし)
       ▲
OneStarMaker.Runtime ──► Foundation + UniTask + Addressables + LitMotion + VContainer
       ▲
OneStarMaker.Debug ──► Foundation + Runtime + TMP

SampleGame.DependOnAll ──→ Common, InGame, OutGame, Foundation, Runtime, Debug, VContainer
SampleGame.InGame      ──→ Common, Foundation, Runtime
SampleGame.OutGame     ──→ Common, Foundation, Runtime
SampleGame.Common      ──→ Foundation, Runtime
```

- **依存は上→下の一方向のみ**
- Foundation はフレームワーク内最下層（Config + Logging）
- Debug は TMP 等の重い依存を隔離。Game 層からは DependOnAll のみ参照
- Game 層は VContainer を知らない（例外: DependOnAll のみ）
- Game 層のクラスはコンストラクタ注入で依存を受け取る

---

## 主要コンポーネント

### Config（設定管理）
3ソース（JSON ファイル / 環境変数 / コマンドライン引数）を優先順位付きでマージする `AppConfig`。  
Microsoft.Extensions.Configuration 互換のキー形式（`:` 区切り）。

### Logging（ログ）
`IAppLogger<T>` インターフェースで ZLogger を隠蔽。Game 層は ZLogger/MEL を直接参照しない。  
- `AppLoggerFactory` で生成（RollingFile JSON 出力 + Unity Console 転送）
- `Trace`/`Debug` レベルは `[Conditional]` で Release ビルド時にゼロコスト除去
- テスト時は `NullAppLogger<T>` を注入

### Scene（シーン管理）
`SceneDirector` が親子階層のシーンツリーを一元管理。  
- 14 状態のライフサイクル（`SceneLifecycleManager` が唯一のオーナー）
- `SwitchScene` / `GoBack` / `ClearHistory` による画面遷移
- `ISceneFactory` で Game 層がシーン生成を実装（DI の注入ポイント）
- Loading オーバーレイ対応

### UI（UI 管理）
単一 Canvas（`UICommon`、DontDestroyOnLoad）に全 UIView を集約。  
- **SiblingIndex が描画順の権威**（sortingOrder は使わない）
- 6レイヤー: `Background(0)` → `Normal(1)` → `Modal(2)` → `Dialog(3)` → `Loading(4)` → `Debug(5)`
- Modal〜Loading レイヤーは自動 Blocker 生成（Debug は除外）
- 同一レイヤー内は Stack 方式（後入れが前面）

### Scene Graph Editor（Editor ツール）
ノードベースの可視化エディタでシーンツリーを定義 → `SceneResource` / `SceneResourceMap` を自動生成。

---

## 開発フェーズ

| Phase | 内容 | 状態 |
|---|---|---|
| **Phase 1** | Framework 骨格（Config, Logging, Scene, UI, Editor） | ✅ 完了 |
| **Phase 2** | Framework サービス（HostedService, Sound, Input） | 未着手 |
| **Phase 3** | Game 基盤（起動処理, SceneFactory） | 🔧 一部完了 |
| **Phase 4** | Game 実装 + UI 移行 | 未着手 |

**Phase 3 実装済み:** `AppInitializer`, `GameSceneFactory`, `NullLoadingDisplay`, `TitleScene`

---

## 実装ファイル一覧（2026-03-07 時点）

**OneStarMaker Framework: 36 ファイル / 約 3,700 行** (3 Assembly)

| Assembly | カテゴリ | ファイル数 | 主要クラス |
|---|---|---|---|
| Foundation | Config | 5 | `AppConfig`, `IConfigProvider`, `JsonConfigFlattener`, `EnvironmentVariableConfigProvider`, `CommandLineConfigProvider` |
| Foundation | Logging | 4 | `IAppLogger<T>`, `AppLogger<T>`, `AppLoggerFactory`, `NullAppLogger<T>` |
| Runtime | Root | 2 | `AbstractApplicationInitializer`, `AssemblyInfo` |
| Runtime | Config | 1 | `JsonFileConfigProvider` |
| Runtime | Scene | 17 | `SceneDirector` (×4 partial), `SceneBase`, `SceneLifecycleManager`, `SceneState`, `SceneContext`, `SceneEvent`, `SceneLoadProgress`, `SceneTransitionPlan`, `SceneResourceMap`, `SceneResource`, `ISceneQuery`, `ISceneFactory`, `ILoadingDisplay`, `LoadingDisplayType` |
| Runtime | UI | 2 | `UICommon`, `UIView` |
| Runtime | AssetDescriptions | 3 | `SceneAssetDescription`, `ScenePayload`, `LoadType` |
| Debug | Profiler | 3 | `DebugProfilerView`, `FrameTimeSampler`, `FrameTimeGraphRenderer` |

**OneStarMaker.Editor: 10 ファイル**（Scene Graph Editor）

**SampleGame: 4 ファイル / 約 123 行**

| Assembly | ファイル | クラス |
|---|---|---|
| DependOnAll | 3 | `AppInitializer`, `GameSceneFactory`, `NullLoadingDisplay` |
| OutGame | 1 | `TitleScene` |

---

## 設計ドキュメント

メインドキュメント: [ARCHITECTURE.md](ARCHITECTURE.md)  
詳細セクションは [Docs/Architecture/](Docs/Architecture/) 配下に分割。

---

## セットアップ

1. Unity 6.5 (6000.5.0f1) で **`unity/`** フォルダを開く
2. NuGetForUnity が自動で NuGet パッケージを復元する
3. Addressable のビルドは未構成（Phase 3 で対応予定）
