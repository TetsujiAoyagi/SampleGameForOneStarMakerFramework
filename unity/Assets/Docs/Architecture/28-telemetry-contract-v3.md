# 28. Telemetry Contract v3（kind + payload）

> ステータス: **実装中（段階移行 案 A）**。旧フラット欄は `AppTelemetry.cs` で deprecated 併記中、削除は TC-09 待ち  
> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る  
> 関連: [12-telemetry.md](12-telemetry.md)、[15-telemetry-v2.md](15-telemetry-v2.md)

---

## 1. 一文

輸送路（Sink / DebugSocket / DebugStudio / Filebeat）は残し、フラット `Metadata` に全部を詰める契約を廃して、**kind 分離 + payload 契約**へ立て直す。

### 1.1 なぜ立て直したか（旧契約の壊れ方）

v2 までは **1 レコード = 1 フラット `Metadata`**。用途の違う数値を同じ欄に同居させ、未設定を `0` / `-1` で表現していた。結果、Elastic 上でこうなった。

| レコード例 | 実際に埋まるもの | Elastic 上の見え方 |
|---|---|---|
| `ProfilerSummary` / `GcSpike` / `UiCost` | cpu/gpu/mem（点イベント）。`elapsedMs` は意図的に 0 | 件数が多く、elapsed=0 が全体平均を支配する |
| `CameraSystemSnapshot` | カメラカウンタのみ。elapsed/cpu/mem は空 | 「情報がない」行 |
| `SceneLoad` / `Unload` / `Transition` | elapsed + finish 時メモリ絶対値。cpu/gpu は未配線で常に 0 | 「半分空っぽ」 |
| `AppStartup` | elapsed のみ。`metadata: default` で mem も 0 | 芯なのに空 |

**0 埋めは「値が 0」と「観測していない」を区別できなくする。** §3 が「未設定は省略。0 埋め禁止」を要求しているのはこの再発防止であって、様式の好みではない。

### 1.2 壊してはいけない芯

v3 で契約を変えても、以下は維持する。

| 残すもの | 理由 |
|---|---|
| Sink 例外非伝播 | 観測がゲームを人質に取らない |
| enum `TelemetryStartType` / `TelemetryTagType` | hot path で文字列を増殖させない |
| 軽量 span（自前実装） | OTel SDK 全面導入は非目標（§1.3） |
| DebugSocket → DebugStudio → L0 NDJSON → Filebeat | 既に動いている輸送路 |
| Bottleneck 自己申告（閾値 + tag + AlertStream） | v2 の目的そのもの |
| Release でもゲームを止めない | Profiler API 不可時は欠測を明示する |

### 1.3 非目標

| やらない | 理由 |
|---|---|
| OpenTelemetry .NET/Unity SDK の全面導入 | hot path・依存・ライセンス面のコストが見合わない。概念（TraceId 等）は借用済み |
| Unity 内に Elastic 固有型を持ち込む | DebugStudio.Export / Filebeat 側の責務 |
| 「全レコードが全フィールドを埋める」 | 観測種が違うのに揃えると再び 0 埋めに戻る |
| 統計的異常検知 | データ蓄積後（[15-telemetry-v2.md](15-telemetry-v2.md)） |
| HLOD / Proxy テレメトリ | Streaming の後続フェーズ |

---

## 2. Kind

| kind | 意味 | elapsedMs |
|---|---|---|
| `span` | 開始〜終了（AppStartup / SceneLoad / …） | 必須 |
| `sample` | 周期スナップ（ProfilerSummary / CameraSystemSnapshot / …） | **export ではキー省略**（意味を持たない） |
| `event` | 発火（GcSpike / UiCost） | 任意 |

kind の増殖は禁止。増やすのは `TelemetryStartType`（name）と payload 形だけ。

---

## 2.5 共通エンベロープ（全 kind）

輸送・相関に必要な最小集合。**用途固有の数値をここに増やさない**（増やす先は §3 の payload）。

| フィールド | 必須 | 説明 |
|---|---|---|
| `schemaVersion` | ✅ | v3 から明示（現行 envelope の 1 と区別） |
| `kind` | ✅ | `span` / `sample` / `event` |
| `name` | ✅ | `TelemetryStartType` 文字列（拡張時は enum 追加） |
| `traceId` / `spanId` / `parentSpanId` | span は ✅ | sample / event は生成 ID でよい |
| `startTimestampUtcTicks` / `endTimestampUtcTicks` | ✅ | sample / event は同一時刻可 |
| `elapsedMs` | span のみ ✅ | sample では**キー自体を出さない**（0 埋め禁止） |
| `isSuccess` | span 推奨 | sample は省略可 |
| `level` | ✅ | Verbose / Summary / Off |
| `tags` / `tagBits` | 任意 | Bottleneck 等 |
| `sessionId` / `producerSequence` | ✅ | 相関 |
| `unityFrameAtStart` / `unityFrameAtEnd` | 任意 | |
| `payload` | ✅ | kind × name ごとのオブジェクト（§3） |

---

## 3. Payload（未設定は省略。0 埋め禁止）

| Shape | 主な name | 内容 |
|---|---|---|
| `TimingMemory` | Scene* / AppStartup | targetIdentity / stage + memory before/after/delta（bytes） |
| `Frame` | ProfilerSummary | fps / cpuMs / gpuMs? / managedBytes / nativeBytes |
| `EventDetail` | GcSpike / UiCost | gcGen0Delta? / unityFrame（Gc 差分が無い UiCost は gc キー省略） |
| `CameraCounters` | CameraSystemSnapshot | total/additional/blending view 数 + max stack depth |

Scene span に cpu/gpu 欄を持たない。GPU 非対応時は `gpuMs` キーを出さない。  
MessagePack wire 上の `elapsedMs` は型都合で常に存在するが、**export / NDJSON では sample と瞬間 event(0) でキー省略**する。

`JsonFileTelemetrySink`（ZLogger 経路）はテンプレート制約により nested `payload` オブジェクトを出せないため、`kind` / `payloadShape` / `payload*` フラット property で代替する。消費者は `kind` + `payloadShape` で解釈すること。

---

## 4. 段階移行（案 A）

1. envelope に `kind` + `payload` を追加（SchemaVersion=3、MessagePack Key 27/28）
2. 旧フラット欄（CpuTime / ManagedMem 等）は deprecated 併記
3. 消費者は **payload / kind を正**とする。**旧フラット欄は fallback にのみ使う** — 併記中に旧欄を正本として読むと §1.1 の 0 埋め汚染がそのまま戻る。payload にキーが無いのは「欠測」であって「0」ではない
4. 旧欄削除は TC-09（併記期間終了時）

---

## 5. 受け入れ（5 問）

| # | 問い | フィルタ |
|---|---|---|
| Q-A | AppStartup は何 ms か | `kind=span`, `name=AppStartup` + `payload.stage` |
| Q-B | SceneLoad は何 ms か | `kind=span`, `name=SceneLoad` + memory delta |
| Q-C | フレーム負荷 | `kind=sample`, `name=ProfilerSummary`（elapsed 無し） |
| Q-D | Streaming 常駐 | `StreamingStats`（TC-08・未実装） |
| Q-E | Bottleneck | tags に Bottleneck |

---

## 6. 実装マップ

| 層 | 触点 |
|---|---|
| Foundation | `TelemetryKind`, `TelemetryPayload`, `TelemetryRecord`, `AppTelemetry.FinishSpan`, `DebugTelemetryEnvelopeV1` |
| Runtime | `RuntimeTelemetryMetadataFactory`, SceneDirector.*, `AbstractApplicationInitializer` |
| Debug | `DebugProfilerView` |
| DebugStudio | Contracts / ExportMapper / `TelemetryExportRecord` / Elastic index template |

---

## 7. 決定済みの論点

再燃しやすいので結論だけ残す。

| 論点 | 結論 |
|---|---|
| 移行戦略 | **案 A: 段階併記**（kind + payload を追加し、旧フラットは deprecated 併記） |
| payload を string JSON にすると alloc が増える | Unity 側は **struct / typed writer を維持**し、オブジェクト化は Serialize 境界だけで行う |
| kind を増やしたい | **禁止。** 増やしてよいのは `name`（`TelemetryStartType`）と payload 形のみ |
| Bottleneck 超過 span を event としても二重発行するか | **しない**（span に tag を付け、AlertStream は現状維持） |
| `StreamingStats` の emit 周期 | 変化時 + 上限 1 Hz |
| 単位 | **ワイヤは bytes（整数）。MB 換算は表示層のみ** |

---

## 8. 更新履歴

| 日付 | 内容 |
|---|---|
| 2026-08-01 | 初版。§16 は Update 基盤が占有していたため §28 として採番 |
| 2026-08-15 | 設計理由（§1.1〜§1.3）と決定済み論点（§7）を計画書から吸収し、本書を単独で読めるようにした |
