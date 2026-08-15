# アーキテクチャ設計書 v2

本ドキュメントは、SampleGame プロジェクトの設計方針・実装ガイドラインを定義する。  
前プロジェクト（NewGradious）のレビュー結果と設計議論を反映し、既知の問題を回避する設計を示す。

> **ファイル構成:** 各セクションの詳細は `Docs/Architecture/` 配下のサブドキュメントに分割されている。

---

## 目次

1. [全体構成](#1-全体構成)
2. [レイヤー構造と Assembly 依存ルール](#2-レイヤー構造と-assembly-依存ルール)
3. [DI・依存管理](Docs/Architecture/03-di.md)
4. [アプリケーション起動](Docs/Architecture/04-app-startup.md)
5. [シーン管理](Docs/Architecture/05-scene.md)
6. [UI 管理](Docs/Architecture/06-ui.md)
7. [サウンド / 入力 / HostedService](Docs/Architecture/07-09-services.md)
8. [コーディング規約・共通ルール](Docs/Architecture/10-coding-rules.md)
9. [Scene Graph Editor（Editor 拡張）](Docs/Architecture/11-scene-graph-editor.md)
10. [開発フェーズ](#12-開発フェーズ)
11. [ライブラリ選定](#13-ライブラリ選定)
12. [前プロジェクトからの教訓](#14-前プロジェクトからの教訓)

---

## 1. 全体構成

```
┌──────────────────────────────────────────────────────────────┐
│  Game (ゲーム固有の実装)                                       │
│  ├── DependOnAll  … 起動エントリーポイント・SceneFactory         │
│  │                  ※ 依存配線（コンポジションルート）を集約する層 │
│  ├── Common       … ゲーム共通サービス・シーン定義                │
│  ├── InGame       … インゲーム                                  │
│  └── OutGame      … アウトゲーム（タイトル等）                    │
├──────────────────────────────────────────────────────────────┤
│  OneStarMaker (汎用ゲームフレームワーク)                         │
│                                                              │
│  ┌─ Foundation (leaf) ─────────────────────────┐     │
│  │  Config  … AppConfig, IConfigProvider, 各プロバイダ  │     │
│  │  Logging … ILogger / ILoggerFactory,                 │     │
│  │              AppLoggerFactory                        │     │
│  │  Telemetry … AppTelemetry, ITelemetrySink,           │     │
│  │              JsonFileTelemetrySink, DebugSocket DTO  │     │
│  │  UpdateSystem … UpdateCoordinator, UpdateLayer       │     │
│  │              （正本: docs/updater/UPDATER_CURRENT_SPEC.md）│     │
│  └─────────────────────────────────────────────┘     │
│          ▲                                                    │
│  ┌─ Runtime ─┘──────────────────────────────────┐     │
│  │  SceneSystem … SceneDirector, SceneBase, ISceneQuery │     │
│  │  UISystem  … UICommon, UIView (6レイヤー + Blocker)  │     │
│  │  AssetManagement … IAssetManagement,                 │     │
│  │              AddressableBackend, AssetResidentCache  │     │
│  │  AssetDescriptions … SceneAssetDescription          │     │
│  │  Bootstrap … AbstractApplicationInitializer         │     │
│  │  DebugSocketServices … DebugSocketService           │     │
│  │  UpdateSystem (hosting) … UpdateSystemHost          │     │
│  └─────────────────────────────────────────────┘     │
│          ▲                                                    │
│  ┌─ Debug ──┴────────────────────────────────────┐     │
│  │  Profiler … DebugProfilerView, FrameTimeSampler    │     │
│  └─────────────────────────────────────────────┘     │
│                                                              │
│  Editor … SceneGraph Editor / Build (Variants,               │
│           AssetDescriptions, Addressables)                   │
└──────────────────────────────────────────────────────────────┘
```

### 設計原則

- **依存は上から下への一方向のみ。** Game → OneStarMaker は可。逆は不可。
- **OneStarMaker はゲーム固有の型を知らない。** インターフェースと抽象クラスで拡張ポイントを提供する。
- **Foundation はフレームワーク内で最下層（leaf）。** Config / Logging / Telemetry / UpdateSystem コア。全モジュールから参照可能。
- **Runtime は Foundation のみに依存。** SceneSystem, UISystem, AssetManagement, AssetDescriptions, Bootstrap。
- **Debug は Foundation + Runtime に依存。** TMP 等の重い依存を隔離。
- **DI コンテナは使わない（手動 DI 正式採用、2026-07-06 決定）。** 依存配線は DependOnAll に集約する。詳細は [03-di.md](Docs/Architecture/03-di.md)。
- **Game 層のクラスはコンストラクタ注入で依存を受け取る。** DI の Attribute (`[Inject]`) は使用禁止。

### 前プロジェクトからの変更点

| 項目 | 旧プロジェクト | 本プロジェクト |
|---|---|---|
| フレームワーク層 | OneStarMakerCommon + Framework.Core の2層が共存 | **OneStarMaker に統合・3 Assembly 分割** (Foundation / Runtime / Debug)。Framework.Core は廃止 |
| 理由 | Framework.Core は未完成の再設計案で実際には未使用 | 依存方向の明確化、ZLogger/TMP 等の重い依存を Debug に隔離 |

---

## 2. レイヤー構造と Assembly 依存ルール

フォルダ全体の戦略（Assembly 軸 + Scene ごとのアセット同居）は [27-folder-structure.md](Docs/Architecture/27-folder-structure.md) を参照。

```
OneStarMaker.Foundation  (leaf — フレームワーク内依存なし)
       ▲
       │
OneStarMaker.Runtime ──► Foundation + UniTask + Addressables + LitMotion + InputSystem + R3
       ▲
       │
OneStarMaker.Debug ──► Foundation + Runtime + TMP

DependOnAll ──→ Game.Common, Game.InGame, Game.OutGame, Foundation, Runtime, Debug
Game.InGame ──→ Game.Common, Foundation, Runtime
Game.OutGame ──→ Game.Common, Foundation, Runtime
Game.Common ──→ Foundation, Runtime
```

**禁止事項:**
- OneStarMaker から Game 層への参照
- 同階層の横断参照（例: InGame → OutGame）
- Assembly 循環依存
- DI コンテナ・リゾルバの導入（手動 DI 正式採用。[03-di.md](Docs/Architecture/03-di.md) の再評価条件を満たした場合のみ検討）

---

## 詳細セクション（サブドキュメント）

以下のセクションは個別ファイルに分割されている。

| # | セクション | ファイル |
|---|---|---|
| §3 | DI・依存管理 | [03-di.md](Docs/Architecture/03-di.md) |
| §4 | アプリケーション起動 | [04-app-startup.md](Docs/Architecture/04-app-startup.md) |
| §5 | シーン管理 | [05-scene.md](Docs/Architecture/05-scene.md) |
| §6 | UI 管理 | [06-ui.md](Docs/Architecture/06-ui.md) |
| §7-9 | サウンド / 入力 / HostedService | [07-09-services.md](Docs/Architecture/07-09-services.md) |
| §10 | コーディング規約・共通ルール | [10-coding-rules.md](Docs/Architecture/10-coding-rules.md) |
| §11 | Scene Graph Editor（Editor 拡張） | [11-scene-graph-editor.md](Docs/Architecture/11-scene-graph-editor.md) |
| §12 | テレメトリ設計 | [12-telemetry.md](Docs/Architecture/12-telemetry.md) |
| §13 | リソースシステム設計 | [13-resource-system.md](Docs/Architecture/13-resource-system.md) |
| §14 | アーキテクチャレビュー（ギャップ分析 & FW 比較、2026-03-07 断面） | [14-architecture-review.md](Docs/Architecture/14-architecture-review.md) |
| §15 | テレメトリ v2（ボトルネック検出・メモリ監視） | [15-telemetry-v2.md](Docs/Architecture/15-telemetry-v2.md) |
| §28 | テレメトリ Contract v3（kind + payload） | [28-telemetry-contract-v3.md](Docs/Architecture/28-telemetry-contract-v3.md) |
| §16 | Update 基盤設計（ドラフト。正本は `docs/updater/UPDATER_CURRENT_SPEC.md`） | [16-update-architecture.md](Docs/Architecture/16-update-architecture.md) |
| §17 | （欠番 — Variant BuildScript レビューは未保存のまま失われた） | — |
| §18 | AssetDescription — 目的・有用性・実装 | [18-asset-description.md](Docs/Architecture/18-asset-description.md) |
| §19 | AssetResidentCache（常駐キャッシュ）チケット仕様 | [19-asset-resident-cache-tickets.md](Docs/Architecture/19-asset-resident-cache-tickets.md) |
| §20 | Variant チェックアウトワークフロー | [20-variant-checkout-workflow.md](Docs/Architecture/20-variant-checkout-workflow.md) |
| §21 | SceneStreaming — セルストリーミング設計（ドラフト） | [21-scene-streaming.md](Docs/Architecture/21-scene-streaming.md) |
| §22 | （予約 — HLOD / Proxy ティア。21-scene-streaming.md §12 参照） | — |
| §23 | CameraSystem — カメラシステム設計（要件確定・実装前） | [23-camera-system.md](Docs/Architecture/23-camera-system.md) |
| §24 | RenderingSystem — レンダリングシステム構想（構想段階・骨子） | [24-rendering-system.md](Docs/Architecture/24-rendering-system.md) |
| §25 | DebugSocket サービス分割チケット | [25-debugsocket-service-refactoring-tickets.md](Docs/Architecture/25-debugsocket-service-refactoring-tickets.md) |
| §26 | UpdateSystem × Async — 時間権威 | [26-update-async-time-authority.md](Docs/Architecture/26-update-async-time-authority.md) |
| §27 | フォルダ構成戦略（Assembly × Scene 同居） | [27-folder-structure.md](Docs/Architecture/27-folder-structure.md) |
| §29 | UI エフェクト合成（非UIレンダラの描画順統合、検証待ち） | [29-ui-effect-compositing.md](Docs/Architecture/29-ui-effect-compositing.md) |
| — | Assembly 分割移行記録 | [migration-assembly-split.md](Docs/Architecture/migration-assembly-split.md) |

---

## 12. 開発フェーズ

| Phase | 内容 | 成果物 | 状態 |
|---|---|---|---|
| **Phase 1** | Framework 骨格 + Editor ツール | Assembly 定義、SceneState、SceneLifecycleManager、SceneDirector（テスト付）、SceneBase、UICommon（6レイヤー + Blocker）、UIView（Debug レイヤー含む）、AbstractApplicationInitializer、Config（AppConfig + 3 Provider）、SceneResource / SceneResourceMap / SceneAssetDescription / ScenePayload / LoadType / SceneContext / SceneEvent / SceneLoadProgress / SceneTransitionPlan、**Scene Graph Editor（§11）**、**AppLoggerFactory（ZLogger ベースの `ILoggerFactory`）**、**テレメトリ基盤（§12: AppTelemetry + lightweight span + JsonFileTelemetrySink + DebugSocket / MessagePack export + ZString 最適化）** | ✅ 完了 |
| **Phase 2** | Framework サービス | HostedService ラップ、SoundService 基盤、InputManager 基盤、操作キュー | 未着手 |
| **Phase 3** | Game 基盤 | DependOnAll 起動処理、SceneFactory、ApplicationService | 🔧 一部完了（AppInitializer, GameSceneFactory, NullLoadingDisplay, TitleScene 実装済） |
| **Phase 4** | Game 実装 + UI 移行 | Title → InGame 遷移、Player(MVVM + R3)、Grid 再構築、UI Toolkit 段階移行 | 未着手 |

> **Phase 1 完了後の追加実装（フェーズ表の枠外で進行したもの）:**
> テレメトリ v2 + DebugSocket / DebugStudio 連携（§12, §15）、AssetManagement + AssetResidentCache（§13, §19）、
> UpdateSystem（§16、正本 `docs/updater/UPDATER_CURRENT_SPEC.md`）、Variant ビルド / チェックアウトワークフロー（§18, §20）。

---

## 13. ライブラリ選定

| 領域 | 旧プロジェクト | **本プロジェクト** | 変更理由 |
|---|---|---|---|
| DI | Static Service Locator | **手動 DI（コンストラクタ注入 + Factory 配線）** | テスタビリティ・依存方向の強制。DI コンテナは不採用（2026-07-06 決定、[03-di.md](Docs/Architecture/03-di.md)） |
| リアクティブ | UniRx（コメントアウト状態） | **R3 + ObservableCollections** | UniRx → R3 が公式後継 |
| Tween | DOTween | **LitMotion** | Zero-Allocation、UniTask ネイティブ統合 |
| async | UniTask | **UniTask**（継続） | — |
| ログ | Debug.Log / DebugService | **ZLogger + `ILogger<T>` / `ILoggerFactory`** | 構造化ログと rolling file / realtime stream の分離 |
| 文字列構築 | string.Format / $"" | **ZString**（ホットパス限定） | GC Alloc ゼロ。毎フレーム・毎遷移パスで使用 |
| テレメトリ | なし | **`AppTelemetry` + lightweight Trace/Span + DebugSocket envelope** | Unity hot path の zero-allocation を優先しつつ、DebugStudio.App は観測と export UI、`DebugStudio.Export` は Elastic 向け export contract / writer を担当 |
| アセット | Addressables | **Addressables**（継続） | — |
| UI | uGUI | **uGUI**（継続、Phase 4 で UI Toolkit 段階移行） | まず動くもの優先 |
| 入力 | Unity InputSystem | **Unity InputSystem**（継続） | — |

**設計思想: Cysharp エコシステムへの統一。**  
UniTask, R3, LitMotion, ZLogger, ZString は全て Cysharp 互換。ライブラリ間の相互運用性と API 設計思想の一貫性を重視する。

---

## 14. 前プロジェクトからの教訓

| 問題 | 原因 | 本設計での対策 |
|---|---|---|
| コンストラクタで Unity API + async 同期ブロック | 設計ルール不在 | §4.5: コンストラクタ軽量化ルール |
| `.GetAwaiter()` で待てていない | 知識不足 | §10.2: async/await 規約 |
| SceneState の二重管理 | オーナー不明確 | §5.2: SceneLifecycleManager に集約 |
| `setSceneState` が internal で外部から呼べる | カプセル化不足 | §5.2: SceneLifecycleManager のみが変更可能 |
| finally 内で既に削除済みの要素にアクセス | catch/finally の使い分け不適切 | §5.8: catch でキャンセル処理 |
| キャンセル済みトークンでクリーンアップ実行 | ルール不在 | §5.8: CancellationToken.None |
| DOTween と Delay の不一致 | Tween の待ち方の知識不足 | §6.4: LitMotion を直接 await |
| Unload 時に ViewOut 未呼出 | 実装漏れ | §6.5: 明示的に ViewOut を呼ぶ |
| UICommon ↔ SceneBase の双方向依存 | 設計ルール不在 | §6.6: SceneDirector を仲介者にする |
| Forget した非同期のエラー消失 | ルール不在 | §5.8: エラーログを残す |
| CancellationTokenSource の Dispose 漏れ | ルール不在 | §4.3 + §10.4: ReleaseAll + Dispose パターン |
| Static Service Locator で NullReferenceException | 設計パターンの問題 | §4.4: ISceneFactory 経由の手動 DI（正式採用） |
| SceneDirector の Dispose 保証なし | ローカル変数で保持 | §4.3: Application.quitting + SubsystemRegistration 二重保護 |
| StandaloneInputModule（旧 Input Manager） | 更新漏れ | §4.2: InputSystemUIInputModule に変更 |
| private メソッドの camelCase/PascalCase 混在 | 規約不統一 | §10.1: PascalCase 統一 |
| Framework.Core と OneStarMakerCommon の2層共存 | 再設計が未完成のまま放置 | §1: OneStarMaker に統合、3 Assembly 分割 (Foundation / Runtime / Debug) |
| `WiatLoadChildScene` タイポ | コードレビュー不足 | §5.2: `WaitLoadChildScene` に修正 |
| `AfterUnloaded` 状態が未使用 | 不要な状態の残留 | §5.2: 削除（13状態 + LoadCanceled） |
| GridBuilder の面積計算バグ (`column * column`) | テスト不足 | Game 層は構造参考に新規実装。旧コード持ち込み禁止 |
| Player/Grid の UniRx コメントアウト放置 | 移行未完了 | R3 で新規実装 |
| 毎フレーム Debug.Log のスパム | ログ規約不在 | §10.7: ZLogger + `ILogger<T>`、hot path は ZString / struct telemetry を優先 |
