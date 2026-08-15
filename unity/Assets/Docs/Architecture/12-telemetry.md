# 12. テレメトリ設計

> ステータス: **実装更新済み** (2026-04-29 時点で現行コードに同期)  
> Contract v3（kind + payload）の正本: [28-telemetry-contract-v3.md](28-telemetry-contract-v3.md)。旧フラット Metadata は段階移行のため deprecated 併記。

---

## 目次

1. [目的](#1-目的)
2. [決定事項サマリ](#2-決定事項サマリ)
3. [現状ギャップ分析](#3-現状ギャップ分析)
4. [Telemetry 基盤 (Foundation)](#4-telemetry-基盤-foundation)
5. [Scene テレメトリ (Runtime)](#5-scene-テレメトリ-runtime)
6. [App 起動テレメトリ](#6-app-起動テレメトリ)
7. [Profiler 統合 (Debug)](#7-profiler-統合-debug)
8. [DEBUG / Release 切替](#8-debug--release-切替)
9. [施行 (T1-T8)](#9-施行-t1-t8)
10. [ZString ホットパス最適化](#10-zstring-ホットパス最適化)
11. [変更ファイル一覧](#11-変更ファイル一覧)
12. [実装時の注意点](#12-実装時の注意点)
13. [将来拡張](#13-将来拡張)

---

## 1. 目的

Framework に統一的なテレメトリ基盤を導入し、以下を実現する:

- **Scene 遷移の各フェーズ所要時間**を自動計測（どこで時間がかかっているかの特定）
- **App 起動の 3 フェーズ所要時間**を自動計測
- **構造化テレメトリイベント**を JSON ファイルに記録（将来 Elastic に送信）
- **TraceId / SpanId** によるイベント間の因果関係紐付け（OpenTelemetry 互換）
- **Profiler データ**（FPS/GPU）を同一テレメトリストリームに統合
- **ユーザー操作コンテキスト**（どのボタン押下が遷移を起こしたか）の紐付け

---

## 2. 決定事項サマリ

| 項目 | 決定 |
|---|---|
| Elastic 連携 | Unity 側は local rolling file + DebugSocket realtime stream、DebugStudio 側は `DebugStudio.App` と `DebugStudio.Export` に責務分離した上で NDJSON / Elastic Bulk export、template / pipeline、Kibana saved objects artifact まで実装済み |
| Elastic 投入導線 | `DebugStudio.Export` が artifact bundle / bulk ingest command / Kibana import command / one-shot ingest runner を生成する |
| Trace モデル | `AppTelemetry` 独自の lightweight span (`TelemetrySpan`) ベース |
| 計測粒度 | ライフサイクルフェーズ単位（PreLoad / Load / Init / ViewIn 等） |
| 記録頻度 | `TelemetryLevel.Verbose/Summary/Off` で制御。Profiler summary / anomaly も同一 stream に統合 |
| テレメトリ配置 | Foundation に span/sink、Runtime/Debug に metadata factory と producer を配置 |
| 実装スコープ | Scene 遷移 + App 起動 + Profiler + DebugSocket / DebugStudio export |

---

## 2.1 Elastic 投入フロー

`DebugStudio.Export` 側では、観測済みデータを Elastic / Kibana へ持っていく operator 導線を次の 2 段で扱う。

1. **DebugStudio.App**
   - retain 済み telemetry / service status / log を
   - NDJSON または Elastic Bulk NDJSON として export する
2. **DebugStudio.Export**
   - template / pipeline / saved objects / command 群を生成する
   - operator は生成された PowerShell を使って Elastic / Kibana へ投入する

この分離により、**WPF の export UI** と **Elastic 運用 artifact** を分離しつつ、
Unity runtime へ Elastic 固有責務を持ち込まない。

### 生成される主な artifact

| 種別 | 既定パス | 役割 |
|---|---|---|
| telemetry index template | `templates\\debugstudio-telemetry.index-template.json` | telemetry 用 mapping |
| service-status index template | `templates\\debugstudio-service-status.index-template.json` | service status 用 mapping |
| log index template | `templates\\debugstudio-log.index-template.json` | log 用 mapping |
| telemetry ingest pipeline | `pipelines\\debugstudio-telemetry.ingest-pipeline.json` | telemetry 正規化 |
| log ingest pipeline | `pipelines\\debugstudio-log.ingest-pipeline.json` | log 正規化 |
| Filebeat sample config | `filebeat\\debugstudio-filebeat.yml` | 継続取り込み用 sample |
| bulk ingest command | `commands\\import-telemetry.ps1` | template / pipeline 登録 + telemetry / log bulk 投入 |
| Kibana import command | `commands\\import-kibana.ps1` | saved objects import |
| one-shot runner | `commands\\invoke-ingest.ps1` | bulk ingest -> Kibana import の順で実行 |
| Kibana saved objects | `kibana\\debugstudio-overview.ndjson` | data view / saved search / dashboard |

### 推奨実行順

1. `ElasticArtifactBundleWriter` で artifact 一式を出力する
2. DebugStudio.App 側で telemetry / log の bulk NDJSON を export する
3. `import-telemetry.ps1` を実行する
4. `import-kibana.ps1` を実行する
5. 通し運用では `invoke-ingest.ps1` を使う

### いまの主経路

- **推奨:** one-shot ingest runner
- **補助線:** Filebeat sample config

現段階では、まず **bulk で確実に入ること** を主経路にし、
継続取り込みの Filebeat 常設化は後段で運用判断する。

---

## 3. 現状ギャップ分析

### 3.1 今あるもの

| 機能 | 状態 | テレメトリ観点 |
|---|---|---|
| `ILogger<T>` + ZLogger | ✅ 稼働 | rolling file と realtime stream を AppLoggerFactory で構成 |
| `DebugProfilerView` | ✅ 稼働 | 画面表示 + `ILogger<T>` + `AppTelemetry.WriteRecord(...)` + alert stream |
| `SceneEvent` Observable | ✅ 稼働 | StateChanged/Added/Removed を発火。タイミング情報なし |
| `SceneLifecycleManager` | ✅ 稼働 | 14 状態の遷移バリデーション。滞在時間計測なし |

### 3.2 ギャップ

| # | 領域 | ギャップ | 配置先 |
|---|---|---|---|
| G1 | DebugStudio export | telemetry 以外の normalized export はまだ薄い | DebugStudio |
| G2 | sender 実装 | realtime stream は formatter 主導で、processor hook への置き換えは未着手 | Foundation |
| G3 | receiver app | companion app の schema/version handling は文書化と最小実装が残る | External / DebugStudio |
| G4 | Runtime module layout | `Runtime\\Telemetry\\` への物理分割は Unity project 再生成待ち | Runtime |

---

## 4. Telemetry 基盤 (Foundation)

### 4.1 AppTelemetry (static class)

zero-allocation を優先した lightweight trace/span façade。全層からアクセス可能。

```csharp
namespace OneStarMaker.Foundation.Telemetry
{
    public static class AppTelemetry
    {
        public static TelemetryLevel Level { get; set; } = TelemetryLevel.Verbose;
        public static TelemetryThresholds? Thresholds { get; set; }
        public static TelemetryAlertStream AlertStream { get; }

        public static TelemetrySpan? StartSpan(TelemetryStartType name, TelemetryTagType? tags);
        public static TelemetrySpan? StartChildSpan(TelemetryStartType name, TelemetryTagType? tags, in TelemetrySpan parent);
        public static double FinishSpan(in TelemetrySpan? span, in Metadata metadata, bool isSuccess = true, TelemetryLevel level = TelemetryLevel.Verbose, TelemetryTagType? tags = null);
        public static void WriteRecord(in TelemetryRecord record);
    }
}
```

### 4.2 TelemetryLevel

```csharp
public enum TelemetryLevel
{
    /// <summary>全フェーズをリアルタイム出力。</summary>
    Verbose = 0,
    /// <summary>遷移完了時にサマリのみ。</summary>
    Summary = 1,
    /// <summary>テレメトリ無効。</summary>
    Off = 2,
}
```

### 4.3 ITelemetrySink

```csharp
public interface ITelemetrySink
{
    void Write(TelemetryRecord record);
}
```

`TelemetryRecord` は immutable struct。`TelemetryStartType` を「操作種別」、`TelemetryTagType` を「異常/補助分類」に分離し、数値 detail は `Metadata` に寄せる。

### 4.4 ZLogger 経由の telemetry transport

`JsonFileTelemetrySink` は名前に反して「JSONL 直書き」ではなく、telemetry record を固定キーの structured ZLogger entry として流す。

- rolling file 側は JSON formatter（telemetry 行もここに残る）
- realtime stream 側は `MessagePackZLoggerFormatter` が通常ログのみ framed message 化
- telemetry EventId の entry は formatter が意図的に捨て、DebugStudio への telemetry は `DebugSocketTelemetrySink` 専用経路のみ

この構成により、Unity 側 producer は `TelemetryRecord` だけを意識し、transport 事情は logging infrastructure に閉じ込める。

---

## 5. Scene テレメトリ (Runtime)

### 5.1 SceneEvent 拡張 (T3)

```csharp
public readonly struct SceneEvent
{
    public SceneEventType Type { get; }
    public string SceneIdentify { get; }
    public SceneState State { get; }
    public long ElapsedMs { get; }         // ★ 追加: フェーズ所要時間
    public long TimestampUtcTicks { get; } // ★ 追加: イベント発生時刻
    ...
}
```

### 5.2 SceneLifecycleManager フェーズ計測 (T3)

`TransitionTo()` 内で `Stopwatch` を使い、フェーズの開始/終了時に経過時間を記録。
各フェーズ完了時に子 Activity を Stop する。

```
SwitchScene (親 Activity)                          ← T4
├── AddScene (子 Activity)                         ← T4
│   ├── PreLoad   ──→ SceneEvent(ElapsedMs=12)    ← T3
│   ├── Load      ──→ SceneEvent(ElapsedMs=85)    ← T3
│   ├── WaitChild ──→ SceneEvent(ElapsedMs=200)   ← T3
│   ├── Init      ──→ SceneEvent(ElapsedMs=5)     ← T3
│   └── ViewIn    ──→ SceneEvent(ElapsedMs=30)    ← T3
└── UnloadScene (子 Activity)                      ← T4
    ├── PreUnload ──→ SceneEvent(ElapsedMs=15)
    ├── Unload    ──→ SceneEvent(ElapsedMs=45)
    └── AfterUnload ──→ SceneEvent(ElapsedMs=3)
```

### 5.3 遷移全体 Span (T4)

`SwitchSceneCore` / `AddScene` の先頭で親 Activity を開始。
Tags にシーン ID・遷移元・遷移先を付与。

### 5.4 操作コンテキスト (T8)

`SwitchScene` / `AddScene` にオプショナルな `IReadOnlyDictionary<string, string>? tags` パラメータを追加。
Activity.Tags に伝搬し、テレメトリレコードに記録される。

---

## 6. App 起動テレメトリ

### T5: AbstractApplicationInitializer

3 フェーズの前後に Activity Start/Stop を埋め込み:

```
App Startup (親 Activity)
├── SubsystemRegistration  ──→ 12ms
├── BeforeSceneLoad        ──→ 45ms
└── AfterSceneLoad         ──→ 230ms
```

---

## 7. Profiler 統合 (Debug)

### T7: DebugProfilerView → telemetry / alert stream

- 1 秒サマリを `AppTelemetry.WriteRecord(...)` で出力
- GC spike / UI cost anomaly も `TelemetryStartType` + `TelemetryTagType` で同一 stream に統合
- 警告表示は `TelemetryAlertStream` を購読して受ける
- `ProfilerRecorder` の寿命管理は `ProfilerUiCostCollector` に分離済み

---

## 8. DEBUG / Release 切替

| 条件 | フェーズ別リアルタイム | 遷移完了サマリ | Profiler サマリ |
|---|---|---|---|
| DEBUG + Verbose | ✅ 出力 | ✅ 出力 | ✅ 出力 |
| DEBUG + Summary | ❌ | ✅ 出力 | ✅ 出力 |
| Release + Summary | ❌ (コード除去) | ✅ 出力 | ❌ (Debug assembly 除外) |
| Release + Off | ❌ | ❌ | ❌ |

- フェーズ別リアルタイム出力は `[Conditional("DEBUG")]` でゼロコスト除去。
- Summary は `AppConfig` の `Telemetry:Level` で動的制御。

---

## 9. 施行 (T1-T8)

### 依存グラフ

```
T1 (Foundation基盤)
├── T2 (ZLogger enrichment)
├── T3 (Scene フェーズ計測) ─── T4 (遷移全体Span) ─── T8 (操作タグ)
├── T5 (App 起動計測)
└── T6 (テレメトリSink) ─── T7 (Profiler統合)
```

### 実装順

| Phase | 内容 | Assembly | 新規/変更 |
|---|---|---|---|
| T1 | `AppTelemetry`, `ITelemetrySink`, `TelemetryLevel`, `TelemetryRecord` | Foundation | 完了 |
| T2 | `AppLoggerFactory` / realtime stream / MessagePack formatter | Foundation | 完了 |
| T3 | Runtime telemetry metadata factory / memory snapshot helper | Runtime | 完了 |
| T4 | `SwitchScene` / `AddScene` / `UnloadScene` / startup の span 再配線 | Runtime | 完了 |
| T5 | threshold / bottleneck / alert stream | Foundation + Debug | 完了 |
| T6 | `JsonFileTelemetrySink` / DebugSocket telemetry envelope | Foundation | 完了 |
| T7 | `DebugProfilerView` summary + GC spike + UI cost + collector 分離 | Debug | 完了 |
| T8 | DebugStudio tag decode / export / telemetry UI | DebugStudio | 完了 |

---

## 10. ZString ホットパス最適化

GC Alloc を最小化するため、ホットパス（毎フレーム・毎遷移で呼ばれる箇所）に ZString を導入した。

### 方針: ホットパス限定

| 箇所 | ZString 化 | 理由 |
|---|---|---|
| `DebugProfilerView.Update()` テキスト更新 | ✅ `ZString.Format` | 毎フレーム実行。最大効果 |
| `DebugProfilerView.LogSummary()` ログ構築 | ✅ `ZString.Format` | 毎秒実行 |
| `JsonFileTelemetrySink.FormatJson()` | ✅ `ZString.CreateStringBuilder()` | 毎遷移で StringBuilder 割り当て回避 |
| `TelemetryRecord.ToString()` | ✅ `ZString.Format` | デバッグ・ログから頻繁に呼ばれる |
| `SceneEvent.ToString()` | ✅ `ZString.Format` | Observable 購読者から毎遷移で呼ばれる |
| Editor コード (SceneGraph 等) | ❌ そのまま | GC 非感受。可読性優先 |
| 例外メッセージ / 起動時1回 | ❌ そのまま | 最適化不要 |

### ZString パッケージ構成

| パッケージ | 用途 |
|---|---|
| `ZString.2.6.0` (NuGet DLL) | `ZString.Format`, `ZString.Concat`, `Utf16ValueStringBuilder` |
| `ZStringFormatExtension.0.0.6` (NuGet DLL) | `IPEndPoint` 等の `TryFormat` 拡張。TMP 拡張は含まない |

> **注意:** ZString の TMP 拡張 (`SetTextFormat`) は UPM パッケージ版にのみ含まれる。
> NuGet DLL 版では使用不可のため、`ZString.Format` + `text =` を使用する。

### `Utf16ValueStringBuilder` と `ref` の注意

`ZString.CreateStringBuilder()` が返す `Utf16ValueStringBuilder` は struct。
`using var sb = ...` で宣言すると C# の仕様で readonly 扱いになり、`ref` で渡せない。
ヘルパーメソッドに `ref` 渡しが必要な場合は `try/finally` で手動 Dispose する:

```csharp
var sb = ZString.CreateStringBuilder();
try
{
    AppendString(ref sb, "key", "value"); // ref 渡し可能
    return sb.ToString();
}
finally
{
    sb.Dispose();
}
```

---

## 10.5 realtime transport と DebugStudio 連携

### 概要

- Unity 側は `AppLoggerFactory` の realtime stream に通常ログのみを流す。
- realtime stream は `MessagePackZLoggerFormatter` により、`DebugSocketProtocol` の framed Log message に変換される。
- telemetry EventId の ZLogger entry は formatter が意図的に捨て、DebugStudio ログパネルへの二重送信を防ぐ。
- telemetry は `DebugSocketTelemetrySink` → `DebugSocketService.EnqueueTelemetry` の専用経路のみで DebugStudio へ届く。
- ローカル rolling file（JSON formatter）は telemetry 行を従来どおり保持する。
- `DebugSocketService` は realtime log frame を WebSocket session に enqueue する。
- DebugStudio 側は message type ごとに envelope を解釈し、UI 表示と NDJSON export に流す。

### クライアント構成

1. **Producer（ログ）**
   - 通常の `ILogger<T>` 呼び出し → realtime stream
2. **Producer（telemetry）**
   - `DebugSocketTelemetrySink` → `DebugSocketService.EnqueueTelemetry`
   - `JsonFileTelemetrySink` → rolling file のみ（realtime stream には載せない）
3. **Formatter**
   - `MessagePackZLoggerFormatter` が通常 `LogInfo` を `DebugSocketEnvelopeV1` + `LogEnvelopeV1` に変換
   - telemetry EventId の entry は 0 バイト出力（フレーム未生成）
4. **Transport**
   - `DebugSocketRealtimeStream` → `DebugSocketService.EnqueueRealtimeLogFrame(...)`
   - telemetry 専用 → `DebugSocketService.EnqueueTelemetry(...)`
5. **Receiver**
   - DebugStudio が `DebugSocketMessageType` ごとに decode し、store / export / UI に反映

### 期待効果

- Unity 側 producer は transport 実装を直接持たずに済む。
- telemetry と通常 log の経路が分離され、二重送信が起きない。
- ローカル解析用 rolling file には telemetry が残り、DebugStudio 側は専用 sink 経由で正本を受け取る。

### Telemetry / Log frame 相関（L2 前提）

Unity producer が wire 作成時に付与する additive field。DebugStudio export 時の後付けは行わない。

| field | 対象 | 意味 |
|---|---|---|
| `sessionId` | Log / Telemetry | Unity 起動単位 ID。handshake Welcome と同一 |
| `producerSequence` | Log / Telemetry | session 内で Log / Telemetry が共有する単調増加順序 |
| `unityFrameAtStart` / `unityFrameAtEnd` | Telemetry span | span 開始・終了 frame。非 main thread は null |
| `unityFrameAtEmit` | Log | formatter が envelope を組み立てた時点の frame |
| `traceId` / `spanId` | Log（optional） | active span 内のみ。span 外は null |

Kibana / NDJSON 突合例:

```text
sessionId:"<id>" AND unityFrameAtEmit:100
sessionId:"<id>" AND producerSequence:[1 TO 10]
sessionId:"<id>" AND traceId:9001
```

multi-frame span は `unityFrameAtStart < unityFrameAtEnd`。worker log は `unityFrameAtEmit` が null のため `threadId` + `timestamp` + optional `traceId` で判別する。

---

## 11. 変更ファイル一覧

### Foundation (新規 5 + 変更 2)

| ファイル | 種別 | 内容 |
|---|---|---|
| `Foundation/Telemetry/AppTelemetry.cs` | 新規 | lightweight span、alert stream、Sink 管理 |
| `Foundation/Telemetry/TelemetryRecord.cs` | 新規 | immutable struct + `Metadata` |
| `Foundation/Telemetry/TelemetryLevel.cs` | 新規 | Verbose/Summary/Off enum |
| `Foundation/Telemetry/ITelemetrySink.cs` | 新規 | 出力先インターフェース |
| `Foundation/Telemetry/JsonFileTelemetrySink.cs` | 新規 | telemetry を structured ZLogger entry として流す Sink |
| `Foundation/Logging/AppLoggerFactory.cs` | 変更 | rolling file + realtime stream + MessagePack formatter |
| `Foundation/OneStarMaker.Foundation.asmdef` | 変更 | DiagnosticSource, System.Text.Json, ZString DLL 参照追加 |

### Runtime (変更 6)

| ファイル | 種別 | 内容 |
|---|---|---|
| `Runtime/SceneSystem/SceneLifecycleManager.cs` | 変更 | Stopwatch フェーズ計測 |
| `Runtime/SceneSystem/SceneEvent.cs` | 変更 | ElapsedMs / TimestampUtcTicks 追加 + ZString 化 |
| `Runtime/SceneSystem/SceneDirector.Transitions.cs` | 変更 | SwitchScene span + telemetryTags |
| `Runtime/SceneSystem/SceneDirector.Loading.cs` | 変更 | AddScene span + telemetryTags |
| `Runtime/SceneSystem/SceneDirector.Unloading.cs` | 変更 | UnloadScene span + telemetryTags |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 変更 | 起動フェーズ span + Sink 登録 + runtime telemetry helper |
| `Runtime/OneStarMaker.Runtime.asmdef` | 変更 | DiagnosticSource, ZString DLL 参照追加 |

### Debug (変更 2)

| ファイル | 種別 | 内容 |
|---|---|---|
| `Debug/Profiler/DebugProfilerView.cs` | 変更 | Profiler summary / GC spike / UI cost / alert 表示 |
| `Debug/OneStarMaker.Debug.asmdef` | 変更 | ZString, ZStringFormatExtension DLL 参照追加 |

---

## 12. 実装時の注意点

### asmdef の precompiledReferences

現在の telemetry 実装は `Activity` 依存を外し、Foundation/Runtime/Debug の独自型で閉じている。
そのため telemetry 自体のために `System.Diagnostics.DiagnosticSource.dll` を必須にはしない。

| Assembly | 必要な precompiledReferences (テレメトリ関連) |
|---|---|
| Foundation | `ZString.dll`, MessagePack 関連 DLL, logging 関連 DLL |
| Runtime | `ZString.dll` |
| Debug | `ZString.dll`, `ZStringFormatExtension.dll` |

### ZLogger AdditionalFormatter の in パラメータ

`AdditionalFormatter` デリゲートの第2引数 `LogInfo` は `in` 修飾子が必要:

```csharp
formatter.AdditionalFormatter = (Utf8JsonWriter writer, in LogInfo info) => { ... };
```

型推論に頼ると `in` が省略されコンパイルエラーになるため、明示的型宣言が必須。

---

## 13. 将来拡張

| # | 内容 | トリガー |
|---|---|---|
| F1 | `ElasticTelemetrySink` — HTTP 直送 | Elastic 環境構築時 |
| F2 | Filebeat 設定自動生成 | 運用基盤決定時 |
| F3 | Addressable 個別アセットの子 Span | 大規模アセット管理着手時 |
| F4 | Input イベントとの Correlation | Input 基盤実装時 |
| F5 | OTel SDK 移行 | IL2CPP 互換性確認後 |
| F6 | Kibana ダッシュボード | Elastic 連携後 |
