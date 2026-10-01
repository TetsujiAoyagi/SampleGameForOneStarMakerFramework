# SampleGame

横スクロール STG「NewGradious」の再設計プロジェクト。  
旧プロジェクト（NewGradious）の設計上の問題を洗い出し、汎用フレームワーク + ゲーム層のクリーンアーキテクチャで一から構築する。

---

## 技術スタック

| 項目 | バージョン / 選定 |
|---|---|
| Unity | **6.6 (6000.6.0f1)** |
| Render Pipeline | **URP 17.6.0** |
| Scripting Backend | **IL2CPP** (Android) |
| .NET | **.NET Standard 2.1** |
| DI | **手動 DI**（コンストラクタ注入 + Factory 配線。コンテナ不採用 — [03-di.md](Docs/Architecture/03-di.md)） |
| Async | **UniTask 2.5.10** |
| Reactive | **R3 1.3.0** + ObservableCollections |
| Tween | **LitMotion** |
| Logging | **ZLogger 2.5.10** + **Microsoft.Extensions.Logging**（`ILogger<T>` / `ILoggerFactory`） |
| Asset Management | **Content Directory（通常）** + **Addressables 2.11.2**（互換 backend / 残存 owner） |
| UI | **uGUI**（Phase 4 で UI Toolkit 段階移行予定） |
| Input | **Unity InputSystem** |
| NuGet | **NuGetForUnity** |

---

## プロジェクト構造

方針の正本（Assembly 依存 + Scene ごとのアセット同居）: [Docs/Architecture/27-folder-structure.md](Docs/Architecture/27-folder-structure.md)

```
Assets/
├── OneStarMaker/Scripts/        ← 汎用ゲームフレームワーク
│   ├── Foundation/              ← OneStarMaker.Foundation.asmdef (leaf)
│   │   ├── Config/              … AppConfig + 3 ConfigProvider
│   │   ├── Logging/             … AppLoggerFactory (ILoggerFactory), MessagePackZLoggerFormatter
│   │   ├── Telemetry/           … AppTelemetry, ITelemetrySink, JsonFileTelemetrySink
│   │   ├── DebugSocket/         … DebugStudio 連携プロトコル DTO (MessagePack)
│   │   └── UpdateSystem/        … UpdateCoordinator, UpdateLayer（正本: docs/updater/UPDATER_CURRENT_SPEC.md）
│   ├── Runtime/                 ← OneStarMaker.Runtime.asmdef (→ Foundation)
│   │   ├── Bootstrap/           … AbstractApplicationInitializer（3フェーズ起動基盤）
│   │   ├── SceneSystem/         … SceneDirector (partial×4), SceneBase, SceneLifecycleManager 他
│   │   ├── UISystem/            … UICommon (SiblingIndex 管理), UIView (6レイヤー)
│   │   ├── AssetManagement/     … IAssetManagement, AddressableBackend, AssetResidentCache
│   │   ├── AssetDescriptions/   … SceneAssetDescription, ScenePayload, LoadType
│   │   ├── DebugSocketServices/ … DebugSocketService（ヒエラルキー/インスペクタ/コマンド）
│   │   └── UpdateSystem/        … UpdateSystemHost, UpdaterDriver（ホスティング層）
│   ├── Debug/                   ← OneStarMaker.Debug.asmdef (→ Foundation + Runtime + TMP)
│   │   └── Profiler/            … DebugProfilerView, FrameTimeSampler, FrameTimeGraphRenderer
│   └── Editor/
│       ├── SceneGraph/          … OneStarMaker.Editor (→ Runtime)。Scene Graph Editor / SceneResource 生成
│       ├── Build/
│       │   ├── Selection/       … OneStarMaker.Build.Selection (Editor-only pure leaf)
│       │   ├── Materialization/ … OneStarMaker.Build.Materialization (Editor-only adapter)
│       │   └── その他           … OneStarMaker.Editor。Content build / Delivery / 旧 Addressables 案内
│       └── TestObservation/     … OneStarMaker.Editor.TestObservation（Unity Test Framework callback の観測）
├── OneStarMaker/Tests/          ← OneStarMaker.Tests / Tests.Editor（Game を参照しない）
│   ├── Scene, AssetManagement, UpdateSystem, Build のフレームワークテスト
│   └── Editor/TestObservation/  … OneStarMaker.Tests.TestObservation.Editor（観測器専用のテスト）
│
├── SampleGame/                  ← ゲーム固有実装
│   ├── DependOnAll/             … AppInitializer, GameSceneFactory, NullLoadingDisplay
│   ├── Common/                  … ゲーム共通の型・シーン定義
│   ├── OutGame/
│   │   └── Title/TitleScene.cs  … タイトル画面
│   ├── InGame/                  … 四季 World / Cell / 職種 companion / Player
│   └── Tests/                   … SampleGame.Tests / Tests.Editor（アプリの EditMode）
│
└── Docs/Architecture/           ← 設計ドキュメント群（現況・到着契約・移行記録。索引は ARCHITECTURE.md）
```

---

## Assembly 依存ルール

```
OneStarMaker.Foundation  (leaf — フレームワーク内依存なし)
       ▲
OneStarMaker.Runtime ──► Foundation + UniTask + Addressables + LitMotion + InputSystem + R3
       ▲
OneStarMaker.Debug ──► Foundation + Runtime + TMP

OneStarMaker.Build.Selection  (Editor-only pure leaf — assembly参照なし)
       ▲
       │
OneStarMaker.Build.Materialization ──► Selection + Runtime + Unity.Addressables (Editor-only)
       ▲
OneStarMaker.Tests.Editor ────┘ (Selection も直接参照。SampleGame は参照しない)

OneStarMaker.Editor.TestObservation ──► UnityEditor.TestRunner + UnityEngine.TestRunner
       ▲
OneStarMaker.Tests.TestObservation.Editor ──► 同じ TestRunner 2 assembly

SampleGame.Tests ──→ Common, InGame, OutGame, DependOnAll, Foundation, Runtime, OneStarMaker.Tests
SampleGame.Tests.Editor ──→ DependOnAll.Editor, OutGame, Foundation, Runtime, Editor, Build.*
SampleGame.DependOnAll ──→ Common, InGame, OutGame, Foundation, Runtime, Debug
SampleGame.InGame      ──→ Common, Foundation, Runtime
SampleGame.OutGame     ──→ Common, Foundation, Runtime
SampleGame.Common      ──→ Foundation, Runtime
```

- **依存は上→下の一方向のみ**
- Foundation はフレームワーク内最下層（Config / Logging / Telemetry / UpdateSystem コア）
- Debug は TMP 等の重い依存を隔離。Game 層からは DependOnAll のみ参照
- **DI コンテナは不採用（手動 DI 正式採用、2026-07-06 決定）**。依存配線は DependOnAll に集約
- Game 層のクラスはコンストラクタ注入で依存を受け取る
- TestObservation の 2 assembly は Editor 専用で、Framework Runtime / Game を参照しない。Unity Test Framework 1.8.0 以上で有効になり、テスト側は `UNITY_INCLUDE_TESTS` も必要。実行契約は [Harness README](../../tools/Harness/README.md#unity-内観測editmode) を参照

---

## 主要コンポーネント

### Config（設定管理）
3ソース（JSON ファイル / 環境変数 / コマンドライン引数）を優先順位付きでマージする `AppConfig`。  
Microsoft.Extensions.Configuration 互換のキー形式（`:` 区切り）。

### Logging（ログ）
`AppLoggerFactory` が `ILoggerFactory` を構成し、rolling file（JSON）と DebugSocket realtime stream を一本化する。
- Game 層は `ILogger<T>` と ZLogger 拡張（`ZLogInformation` 等）を直接参照する（独自 `IAppLogger<T>` ラッパーは不採用）
- `AppInitializer` → `GameSceneFactory` → `Scene` へ手動 DI。Game 層での `new AppLoggerFactory()` は禁止
- テスト時は `NullLoggerFactory.Instance` を注入

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

### Telemetry（テレメトリ）
OTel 互換の TraceId/SpanId を持つ軽量スパン計測。JSONL ファイル出力 + DebugSocket 経由で外部ツール DebugStudio（`tools/DebugStudio`）へストリーム。詳細は [12-telemetry.md](Docs/Architecture/12-telemetry.md)。

### AssetManagement（アセット管理）
通常経路は Content Directory。Addressables は `IAssetManagement` / `IAssetBackend` の互換口として残る。
LFU + 時間減衰の常駐キャッシュ `AssetResidentCache`（AssetType 別バジェット）を内蔵。詳細は [13-resource-system.md](Docs/Architecture/13-resource-system.md)。

### Content Directory / Delivery（Editor ツール）
Editor directory Play は Content Directory build 後の transport publish と DIST Delivery の verified install を使う。Editor Play 向け publish 入口は未整備。BS4 Player は Player build 内で publish する別経路。
旧 Hybrid / Remote / Variant Player overlay は案内のみ。詳細は [20-variant-checkout-workflow.md](Docs/Architecture/20-variant-checkout-workflow.md)。

### UpdateSystem（フレームスケジューラ）
MonoBehaviour.Update によらない更新基盤（Layer / Coordinator / Job System バックエンド）。正本仕様は `docs/updater/UPDATER_CURRENT_SPEC.md`（リポジトリルート）。

---

## 開発フェーズ

開発フェーズと未実装範囲は [ARCHITECTURE.md の開発フェーズ](ARCHITECTURE.md#12-開発フェーズ) を正とする。
四季 World / SceneStreaming、Content Directory / DIST Delivery、S-4c の最小 RenderEnvironment と代表 bake、FW / アプリのテストアセンブリ分離は実装済みである。これは旧 STG の全ゲーム実装や RenderWorld 全体の完成を意味しない。

---

## 実装規模（2026-07-06 時点）

以下は当時の測定記録であり、現在のファイル数・行数・実装範囲ではない。

**OneStarMaker Framework: 約 192 ファイル / 約 21,000 行**（テスト含む）

| Assembly | ファイル数 | 行数(概算) | 主な内容 |
|---|---|---|---|
| Foundation | 76 | 5,600 | Config, Logging, Telemetry, DebugSocket DTO, UpdateSystem コア |
| Runtime | 59 | 6,900 | SceneSystem, UISystem, AssetManagement, AssetDescriptions, Bootstrap, DebugSocketServices |
| Debug | 3 | 600 | Profiler オーバーレイ |
| Editor | 30 | 5,000 | Scene Graph Editor, Variant ビルド, AssetDescription 収集 |
| Tests | 24 | 3,200 | Scene, AssetManagement, UpdateSystem, Build |

**SampleGame: 4 ファイル / 約 120 行**（`AppInitializer`, `GameSceneFactory`, `NullLoadingDisplay`, `TitleScene`）

このほか外部ツール **DebugStudio**（`tools/DebugStudio`、.NET 8 WPF + CLI、独自テスト・CI あり）が付属する。

---

## 設計ドキュメント

メインドキュメント: [ARCHITECTURE.md](ARCHITECTURE.md)  
詳細セクションは [Docs/Architecture/](Docs/Architecture/) 配下に分割。

---

## セットアップ

1. Unity 6.6 (6000.6.0f1) で **`unity/`** フォルダを開く
2. NuGetForUnity が自動で NuGet パッケージを復元する
3. Editor directory Play には **Tools > OSM > Content** での Content Directory build に加え、transport publish と Delivery の verified install / **Use For Next Play** が必要。Editor Play 向け publish 入口は未整備なので、build メニューだけではこの手順は完了しない。BS4 Player は Player build 内で publish する。手順と現状の制約は [20-variant-checkout-workflow.md](Docs/Architecture/20-variant-checkout-workflow.md)
