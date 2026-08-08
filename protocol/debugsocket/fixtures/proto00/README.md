# PROTO-00 golden fixtures

Captured from the **pre-codegen** hand-written MessagePack DTOs on 2026-08-06
using `MessagePackSerializerOptions.Standard`.

These bytes are the regression anchor for YAML → generated C# migration.
Do not regenerate casually after Key/default changes; update only with an
intentional wire-contract change and both-side test updates.

| File | Content |
|---|---|
| `log_envelope_v1.hex` | `LogEnvelopeV1` payload |
| `debug_telemetry_payload_v1.hex` | `DebugTelemetryPayloadV1` (TimingMemory) |
| `debug_telemetry_envelope_v1.hex` | `DebugTelemetryEnvelopeV1` including nested payload |
| `framed_log_envelope_v1.hex` | `DebugSocketProtocol.SerializeMessage(Log, …)` |
| `framed_telemetry_envelope_v1.hex` | `DebugSocketProtocol.SerializeMessage(Telemetry, …)` |

CLI control message types 12/13 are intentionally excluded.
