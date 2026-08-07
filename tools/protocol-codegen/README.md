# protocol-codegen

`protocol/debugsocket` YAML → MessagePack 用 C# DTO を生成する小さな CLI（.NET 8）。

```bash
# 生成（成果物はリポジトリに commit する）
./tools/protocol-codegen/generate.sh

# CI / ローカル検証: 生成結果が commit 済み内容と一致するか
./tools/protocol-codegen/generate.sh --check
```

Windows: `tools/protocol-codegen/generate.ps1`

## 注意

- Unity EditMode テストは既存パイプラインに乗る範囲で担保する。最低限 CI では `--check` + DebugStudio Contracts tests を必須とする。
- PROTO-00 golden fixture（`protocol/debugsocket/fixtures/proto00/`）でバイト互換を両側から検証する。
- protobuf / gRPC emitter は未実装（将来口のみ）。
