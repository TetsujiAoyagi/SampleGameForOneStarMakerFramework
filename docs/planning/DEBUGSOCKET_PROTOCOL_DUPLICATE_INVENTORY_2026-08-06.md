# DebugSocket 二重定義差分棚卸し (PROTO-00b) — 2026-08-06

> 生成移行前の現行手書き DTO を突合した結果。wire 互換に影響する不一致はゼロ。

## 結論

| 区分 | 結果 |
|---|---|
| 共有 wire フィールド / Key / 型 / 既定値 | **一致** |
| 意図的差分 | message type 12/13（DS/CLI）、`LogEnvelopeV1.Kind`（DS `[IgnoreMember]`）、Unity `FromRecord`/`FromPayload` |
| wire 破壊的不一致 | **なし** |

## 意図的差分（許容）

| 差分 | 側 | 理由 |
|---|---|---|
| `DebugSocketMessageType` 12/13 (`ControlCommand*`) | DS only | CLI control plane。YAML surfaces で Unity 出力から除外する |
| `ControlCommandRequest/ResponseEnvelopeV1`, `ControlCommandRoundtripStatus` | DS only | 同上 |
| `[IgnoreMember] Kind` on `LogEnvelopeV1` | DS only | UI/schema 用ビュー。wire に乗らない |
| `FromRecord` / `FromPayload` | Unity only | 内部 Telemetry モデルへの mapper。partial 手書きへ移す |
| namespace 差（`Logging` vs `Protocol`） | 双方 | 現行互換のため維持。emitter の NS 上書きで吸収 |
| XML コメント文面 | 双方 | 無視 |

## 共有型（フィールド Key 一致）

`DebugSocketEnvelopeV1`, `LogEnvelopeV1`(Keys 0–18), `DebugTelemetryEnvelopeV1`(0–28),
`DebugTelemetryPayloadV1`(0–20), `DebugCommand*`, `DebugSocketServiceStatusEnvelopeV1`,
`CapabilityHandshake*`, `DebugStudioCapability`, `Hierarchy*`, `Inspector*` 一式。

## 片側のみ（生成外）

| 型 | 側 | 扱い |
|---|---|---|
| `DebugSocketOptions`, `UnitySessionCorrelationContext` | Unity | 生成外 |
| `DebugSocketProtocol` | 双方 | 初回生成対象外（手書き維持） |

## ドリフト検知手順（手動確認メモ）

1. DS 側で `LogEnvelopeV1` の `[Key(7)] Message` を一時的に `[Key(70)]` へずらす
2. `dotnet test` の PROTO-00 golden serialize が失敗することを確認
3. 変更を破棄する

（本ブランチでは Key をずらした確認後、直ちにリバート済み）
