# 28. Telemetry Contract v3（kind + payload）

> ステータス: **実装中（段階移行 案 A）**（2026-08-01）  
> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る  
> 計画: [TELEMETRY_CONTRACT_REDESIGN_PLAN_2026-07-27.md](../../../../docs/planning/TELEMETRY_CONTRACT_REDESIGN_PLAN_2026-07-27.md)  
> 関連: [12-telemetry.md](12-telemetry.md)、[15-telemetry-v2.md](15-telemetry-v2.md)

---

## 1. 一文

輸送路（Sink / DebugSocket / DebugStudio / Filebeat）は残し、フラット `Metadata` に全部を詰める契約を廃して、**kind 分離 + payload 契約**へ立て直す。

---

## 2. Kind

| kind | 意味 | elapsedMs |
|---|---|---|
| `span` | 開始〜終了（AppStartup / SceneLoad / …） | 必須 |
| `sample` | 周期スナップ（ProfilerSummary / CameraSystemSnapshot / …） | **export ではキー省略**（意味を持たない） |
| `event` | 発火（GcSpike / UiCost） | 任意 |

kind の増殖は禁止。増やすのは `TelemetryStartType`（name）と payload 形だけ。

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
3. 消費者は **payload / kind を正**とする
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

## 7. 更新履歴

| 日付 | 内容 |
|---|---|
| 2026-08-01 | 初版。計画合意（TC-00）に基づき正典化。§16 は Update 基盤が占有のため §28 として採番 |
