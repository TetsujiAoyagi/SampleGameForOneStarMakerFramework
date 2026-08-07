# DebugSocket YAML schema (PROTO-01)

Transport-neutral semantic contract. MessagePack Key binding uses stable `fields[].id`.

## File layout

- `meta.yaml` — emitter targets / default namespaces
- `messages.yaml` — `DebugSocketMessageType` values with surfaces
- `enums.yaml` — shared enums/flags
- `envelopes/*.yaml` — one message type per file
- `surfaces/*.yaml` — optional type allow-lists (informational; each type also declares `surfaces`)

## Type system

| YAML | C# |
|---|---|
| `u8` | `byte` |
| `i32` | `int` |
| `i64` | `long` |
| `f32` | `float` |
| `f64` | `double` |
| `bool` | `bool` |
| `string` | `string` |
| `bytes` | `byte[]` |
| `array<T>` | `T[]` |
| message name | nested DTO type |
| `optional: true` | `T?` |

## Defaults

- omit → CLR default (0/false/null)
- `""` → `string.Empty`
- `[]` → `Array.Empty<T>()`
- number / bool / string literals
- enum member name or `|` expression for flags

## Surfaces

- `unity` — Unity Foundation output
- `debugstudio` — DebugStudio.Contracts output (includes CLI)

Members/types without the target surface are omitted from that emitter output.
If a member omits `surfaces`, it inherits the parent type's surfaces.
Explicit `surfaces: []` means **exclude from all targets** (not "all targets").
Codegen also fails if Unity outputs contain `ControlCommand*` or message type 12/13.

## Encodings

```yaml
encodings:
  messagepack:
    key_field: id   # fields[].id → [Key(n)]
```

Do not put UnityEngine / ViewModel types in YAML.
