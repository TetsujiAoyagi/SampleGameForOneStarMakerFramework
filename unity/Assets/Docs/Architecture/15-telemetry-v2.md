# 15. テレメトリ要件定義 v2 — ボトルネック検出・メモリ監視

> ステータス: **実装更新済み** (2026-04-29 時点で現行コードに同期)  
> 前提: [12-telemetry.md](12-telemetry.md) の基盤は custom lightweight span + ZLogger / DebugSocket transport へ更新済み。本ドキュメントはその上に乗る bottleneck / anomaly 設計を記録する。

---

## 目次

1. [目的の再定義](#1-目的の再定義)
2. [計測対象と優先順位](#2-計測対象と優先順位)
3. [閾値設計](#3-閾値設計)
4. [メモリスナップショット設計](#4-メモリスナップショット設計)
5. [GC スパイク検出設計](#5-gc-スパイク検出設計)
6. [UI 描画コスト計測設計](#6-ui-描画コスト計測設計)
7. [消費者と段階的デリバリー](#7-消費者と段階的デリバリー)
8. [出力先と表示方式](#8-出力先と表示方式)
9. [スコープ外（将来対応）](#9-スコープ外将来対応)
10. [トレードオフ記録](#10-トレードオフ記録)
11. [実装フェーズ](#11-実装フェーズ)

---

## 1. 目的の再定義

### Before（12-telemetry.md の目的）

> 「**何がどのくらいかかっているか**」を記録する。

### After（本ドキュメントの目的）

> 「**何が遅い / 何が異常か**」を自動検知し、開発者に即座に伝える。

既存のテレメトリ基盤（Activity Span + JSONL Sink）は「データの記録」に特化している。  
本拡張は、そのデータに**閾値判定・異常検知の層**を被せることで、  
「ログを開いて探す」から「**問題が勝手に名乗り出る**」への転換を実現する。

---

## 2. 計測対象と優先順位

| 優先度 | 領域 | 計測内容 | 検出方式 | Phase |
|:---:|:---|:---|:---|:---:|
| **P0** | シーン読み込み | 各フェーズ（PreLoad / Load / Init / ViewIn）の所要時間 | AppConfig 固定閾値超過 → `TelemetryTagType.Bottleneck` + alert | 1 |
| **P0** | App 起動 | 3 フェーズ（SubsystemReg / BeforeSceneLoad / AfterSceneLoad）の所要時間 | 同上 | 1 |
| **P1** | メモリ使用量 | シーン遷移前後のメモリスナップショット差分 | 増分が閾値超過 → Warning | 1 |
| **P2** | GC スパイク | 毎フレーム `GC.CollectionCount` の差分 | GC 発生フレームを検出 → Warning | 1 |
| **P3** | UI 描画コスト | Canvas Rebuild 回数 / Batch 数 | `ProfilerRecorder` で取得、閾値超過 → Warning | 1 |

---

## 3. 閾値設計

### 3.1 定義場所

既存の `AppConfig`（3 ソースマージ: JSON / 環境変数 / CLI）に統合する。

```
telemetry.thresholds.sceneLoadMs          : 500    # シーンロード全体の警告閾値 (ms)
telemetry.thresholds.scenePhaseMs         : 200    # 個別フェーズの警告閾値 (ms)
telemetry.thresholds.appStartupPhaseMs    : 1000   # 起動フェーズの警告閾値 (ms)
telemetry.thresholds.memoryDeltaMb        : 50     # シーン遷移時メモリ増分の警告閾値 (MB)
telemetry.thresholds.gcPerFrame           : 1      # フレーム中の GC 発生回数の警告閾値
telemetry.thresholds.canvasRebuildPerFrame : 5      # Canvas Rebuild 回数の警告閾値
telemetry.thresholds.batchCount           : 100    # Batch 数の警告閾値
```

### 3.2 超過時の動作

1. `TelemetryRecord` に `TelemetryTagType.Bottleneck` を付与する。
2. `TelemetryAlertStream` へ通知し、`DebugProfilerView` が警告行を表示する。
3. `JsonFileTelemetrySink` / DebugSocket transport の双方へ同じ telemetry record を流す。

### 3.3 設計根拠

- **固定閾値 + ログ警告**を選択した理由: 実装コストが低く、AppConfig で環境ごとに調整可能。
- 統計的異常検知（過去 N 回の平均からの逸脱）は Phase 2 以降の将来検討とする。

---

## 4. メモリスナップショット設計

### 4.1 計測タイミング

```
SceneDirector.SwitchScene()
├── [スナップショット取得: Before]
│     GC.GetTotalMemory(false)
│     Profiler.GetTotalAllocatedMemoryLong()
│     Profiler.GetTotalReservedMemoryLong()
│
├── ... (シーン遷移処理) ...
│
└── [スナップショット取得: After]
      GC.GetTotalMemory(false)
      Profiler.GetTotalAllocatedMemoryLong()
      Profiler.GetTotalReservedMemoryLong()
      → delta を計算、閾値超過で Warning
```

### 4.2 記録データ

| フィールド | 型 | 説明 |
|:---|:---|:---|
| `memory.before.managedHeapMb` | double | 遷移前の Managed Heap (MB) |
| `memory.after.managedHeapMb` | double | 遷移後の Managed Heap (MB) |
| `memory.delta.managedHeapMb` | double | 差分 (MB) |
| `memory.before.nativeAllocMb` | double | 遷移前の Native Alloc (MB) |
| `memory.after.nativeAllocMb` | double | 遷移後の Native Alloc (MB) |
| `memory.delta.nativeAllocMb` | double | 差分 (MB) |
| `memory.before.reservedMb` | double | 遷移前の Reserved (MB) |
| `memory.after.reservedMb` | double | 遷移後の Reserved (MB) |

これらは `TelemetryRecord.MetadataValue` に数値で保持し、必要な異常分類だけを `TelemetryTagType` へ載せる。

### 4.3 トレードオフ

- `GC.GetTotalMemory(true)` （強制 GC 付き）は使用**しない**。シーン遷移中のフレームスパイクを誘発するため。
- `Profiler.GetTotalAllocatedMemoryLong()` は Editor / Development Build でのみ正確。Release Build では 0 を返す可能性があるため、Release では Managed Heap のみを記録する。

---

## 5. GC スパイク検出設計

### 5.1 検出方式

毎フレーム `GC.CollectionCount(0)` の差分を取る。差分 > 0 なら GC が発生したフレーム。

```csharp
// DebugProfilerView.Update() 内
int currentGcCount = GC.CollectionCount(0);
int delta = currentGcCount - _lastGcCount;
if (delta > thresholdGcPerFrame)
{
    _logger.LogWarning(
        ZString.Format(
            "[Telemetry] GC spike: {0} collections in frame {1} (scene: {2})",
            delta, Time.frameCount, currentSceneName));
}
_lastGcCount = currentGcCount;
```

### 5.2 記録データ

| フィールド | 型 | 説明 |
|:---|:---|:---|
| `gc.gen0Count` | int | Gen 0 GC 発生回数 |
| `gc.frameNumber` | int | 発生フレーム番号 |
| `gc.sceneName` | string | UI warning 用の文言。telemetry 本体は zero-allocation 優先で scene id / numeric data を優先 |

### 5.3 パフォーマンスへの影響

- `GC.CollectionCount(0)` は int を返すだけの**ゼロアロケーション**呼び出し。毎フレームのコストは無視できる。
- 検出時のログ出力（Warning）にのみ ZString を使い、GC を発生させないよう注意する。

---

## 6. UI 描画コスト計測設計

### 6.1 計測方式

`Unity.Profiling.ProfilerRecorder` を使用して以下のカウンタを取得する。

| カウンタ名 | 意味 |
|:---|:---|
| `UI.Canvas.RebuildBatchedCount` | Canvas Rebuild が走った回数 |
| `UI.Canvas.BatchCount` | 描画バッチ数 |

```csharp
// Debug assembly 内に配置
var rebuildRecorder = ProfilerRecorder.StartNew(
    ProfilerCategory.Internal, "UI.Canvas.RebuildBatchedCount");
var batchRecorder = ProfilerRecorder.StartNew(
    ProfilerCategory.Internal, "UI.Canvas.BatchCount");
```

### 6.2 記録タイミング

- 既存の `DebugProfilerView` で 1 秒サマリとして集約する。
- 1 秒間のピーク値が閾値を超えた場合に Warning を出力する。

### 6.3 配置先

`OneStarMaker.Debug` assembly に配置する。  
理由: `ProfilerRecorder` は `Unity.Profiling` 名前空間であり、Debug assembly の責務（Profiler 統合）と一致する。Release Build では Debug assembly ごと除外される。

---

## 7. 消費者と段階的デリバリー

| Phase | 消費者 | 出力先 | スコープ |
|:---:|:---|:---|:---|
| **Phase 1** | 開発中の自分 | `DebugProfilerView` 警告行 + Unity local log + DebugStudio telemetry panel | **実装済み** |
| **Phase 2** | QA プレイ後分析 | `DebugStudio.App` export UI + `DebugStudio.Export` による NDJSON / Elastic Bulk export | **実装済み（telemetry / service status / log）** |
| **Phase 3** | リリース後ユーザー端末 | Elastic / Kibana ダッシュボード（Filebeat 連携 or bulk ingest） | Kibana saved objects の正本は `tools/DebugStudio/elastic/kibana/debugstudio-overview.ndjson`（埋め込みリソース経由で artifact 出力）。saved search パネル 2 枚の overview まで実装済み。Lens 等のパネル作り込みは後続スライス |

---

## 8. 出力先と表示方式

### Phase 1: DebugProfilerView への統合

既存の `DebugProfilerView`（FPS / CPU / GPU 表示）に以下の警告行を追加する。

```
[FPS] 60.0  [CPU] 8.2ms  [GPU] 6.1ms
[⚠ GC] 2 collections @ frame 1234 (InGame)       ← GC スパイク検出時のみ表示
[⚠ Memory] +62.3 MB after scene load (InGame)     ← メモリ閾値超過時のみ表示
[⚠ UI] 8 rebuilds, 142 batches                    ← UI 閾値超過時のみ表示
```

- 警告行は **閾値超過時のみ**表示し、正常時は非表示（ノイズ削減）。
- 警告は N 秒間表示した後にフェードアウトする。

### JSONL 出力

Unity 側は `JsonFileTelemetrySink` を経由して ZLogger entry を生成し、  
rolling file と DebugSocket realtime stream の両方へ流す。  
DebugStudio 側 export では `TagBits` と decoded `Tags` の両方を保持するため、後分析時に anomaly 抽出しやすい。

### Phase 3: Elastic / Kibana への投入手順

`DebugStudio.Export` は operator 向けに次の artifact をまとめて出力できる。

- index template
- ingest pipeline
- Filebeat sample config
- bulk ingest command
- Kibana saved objects
- one-shot ingest runner

推奨順は次の通り。

1. `ElasticArtifactBundleWriter` で artifact 一式を出す
2. `import-telemetry.ps1` で template / pipeline 登録 + telemetry / log bulk 投入を行う
3. `import-kibana.ps1` で saved objects を import する
4. 通し実行したい場合は `invoke-ingest.ps1` を使う

この段階では **one-shot ingest runner を主経路** とし、Filebeat 常設化は運用段階で追加する。
Kibana saved objects の正本はリポジトリ上の NDJSON ファイルであり、C# は埋め込みリソースを吐き出すだけである。パネルの集計内容（Lens 等）の作り込みは後続スライスで行う。

### 各 command の責務

| command | 役割 | 想定入力 |
|---|---|---|
| `import-telemetry.ps1` | telemetry / service-status / log の template / pipeline を登録し、その後 bulk NDJSON を投入する | Elastic URL, telemetry bulk file, log bulk file |
| `import-kibana.ps1` | Kibana saved objects bundle を import する | Kibana URL, saved objects file |
| `invoke-ingest.ps1` | `import-telemetry.ps1` → `import-kibana.ps1` の順で呼ぶ | Elastic URL, Kibana URL |

### operator 向け最小 runbook

1. DebugStudio.App で telemetry / log を export する
2. `ElasticArtifactBundleWriter` で artifact を出す
3. `commands\\import-telemetry.ps1` を実行する
4. `commands\\import-kibana.ps1` を実行する
5. Kibana で `DebugStudio Overview` を開く

### 運用上の注意

- bulk 投入前に template / pipeline を先に登録する
- telemetry と log は別 bulk file として扱う
- saved objects import は `overwrite=true` 前提なので、既存 object を上書きする
- Filebeat sample config は継続運用用であり、最初の疎通確認は bulk 経路を優先する

---

## 9. スコープ外（将来対応）

| # | 内容 | 対応トリガー |
|:---|:---|:---|
| S1 | 個別リソース（テクスチャ / Prefab）の読み込み時間計測 | ResourceSystem 実装時 |
| S2 | 入力遅延（ボタン押下 → 画面反映のレイテンシ） | InputManager 実装時 |
| S3 | オブジェクトプール枯渇検知（弾 / エフェクト） | InGame ループ実装時 |
| S4 | 統計的異常検知（過去 N 回の平均からの逸脱で自動閾値設定） | 十分なデータ蓄積後 |
| S5 | Addressables アセット単位の参照カウント / サイズ監視 | ResourceSystem 実装時 |

---

## 10. トレードオフ記録

| 選択 | メリット | デメリット | 判断理由 |
|:---|:---|:---|:---|
| 固定閾値（統計的検知ではなく） | 実装コスト低。AppConfig で即調整可能 | 環境差・コンテンツ差に弱い | 個人開発で十分なサンプル数が溜まるまで統計的検知は不要 |
| `GC.CollectionCount` 毎フレーム | 正確な発生フレーム特定 | 毎フレーム呼び出しのオーバーヘッド（ただし int 返却のみで無視できる） | STG では GC 発生フレームの特定が致命的に重要 |
| `GC.GetTotalMemory(false)` | フレームスパイクを誘発しない | 強制 GC 後の「真の使用量」が取れない | ゲーム中の安定性を優先 |
| `ProfilerRecorder` で UI 計測 | Unity ネイティブ精度。追加コールバック不要 | Development Build / Editor 限定。Release では取得不可 | Debug assembly に閉じるため問題なし |
| `DebugProfilerView` に統合（専用 View 新設ではなく） | 既存インフラ活用。画面占有を増やさない | 表示情報が過密になるリスク | 「閾値超過時のみ表示」で解決。正常時は既存表示のみ |

---

## 11. 実装フェーズ

### 依存グラフ

```
B1 (閾値コンフィグ定義)
├── B2 (シーンロード閾値チェック)
├── B3 (App 起動閾値チェック)
├── B4 (メモリスナップショット)
├── B5 (GC スパイク検出)
└── B6 (UI 描画コスト計測)
      └── B7 (DebugProfilerView 警告統合)
```

### 実装タスク一覧

| Task | 内容 | Assembly | 新規 / 変更 | 依存 |
|:---:|:---|:---|:---|:---|
| **B1** | `AppConfig` に閾値キーを追加、`TelemetryThresholds` 読み出しクラス新設 | Foundation | 新規 1 + 変更 1 | — |
| **B2** | `SceneLifecycleManager` / `SceneDirector` の既存 `FinishSpan` 後に閾値チェックを追加 | Runtime | 変更 2 | B1 |
| **B3** | `AbstractApplicationInitializer` の既存 `FinishSpan` 後に閾値チェックを追加 | Runtime | 変更 1 | B1 |
| **B4** | `SceneDirector.SwitchScene` 前後でメモリスナップショット取得・差分チェック | Runtime | 変更 2 | B1 |
| **B5** | `DebugProfilerView` に `GC.CollectionCount` 差分検出ロジック追加 | Debug | 変更 1 | B1 |
| **B6** | `DebugProfilerView` に `ProfilerRecorder` による UI カウンタ取得追加 | Debug | 変更 1 | B1 |
| **B7** | `DebugProfilerView` の描画部に警告行の表示ロジックを追加 | Debug | 変更 1 | B2-B6 |

### 変更ファイル見積もり

| Assembly | 新規 | 変更 | 合計 |
|:---|:---:|:---:|:---:|
| Foundation | 1 (`TelemetryThresholds.cs`) | 1 (`AppConfig` 連携) | 2 |
| Runtime | 0 | 5 (`SceneLifecycleManager`, `SceneDirector.Transitions`, `SceneDirector.Loading`, `SceneDirector.Unloading`, `AbstractApplicationInitializer`) | 5 |
| Debug | 0 | 1 (`DebugProfilerView`) | 1 |
| **合計** | **1** | **7** | **8** |
