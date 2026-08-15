# DebugSocket Protocol: YAML description 対応 + ドキュメントコメント backfill ハンドオフ (2026-08-08)

> **次エージェント向け実装ハンドオフ。** 設計判断は確定済み。
> 本書が施工の正本。実装中に方針を変える場合は、先に本書を更新してからコードを触る。
>
> 前提スライス: `DEBUGSOCKET_PROTOCOL_YAML_CODEGEN_HANDOFF_2026-08-06.md`（PR #10）。**そちらがマージ済みであること。**

---

## 0. 1分で把握

| 項目 | 内容 |
|---|---|
| 問題 | PR #10 の wire DTO 生成移行で、**フィールド単位の `///` ドキュメントコメント 416 行が両側から消えた**（Unity 238 / DebugStudio 178）。YAML にも emitter にも移設先が無い |
| 決定 | YAML に `description` を導入し、emitter が `/// <summary>` を出す。旧コメントを YAML へ backfill して再生成する |
| 非目標 | 契約そのものの変更、新フィールド追加、`DebugSocketProtocol` の共通化、protobuf emitter、命名変更 |
| 絶対条件 | **生成 C# の API サーフェス（`[Key(n)]` / 型 / nullable / 既定値 / enum 値 / `[MessagePackObject]` / `[IgnoreMember]`）が 1 行も変わらないこと。** 変わるのはコメント行だけ |

なぜやるか: このリポジトリの主張は「境界・寿命・依存の契約を**実装とドキュメントの両方で**公開すること」（`docs/GOALS_AND_STRENGTHS.md`）。wire 契約からフィールドの「なぜ」が消えた状態は、その主張と正面から矛盾する。

---

## 1. 確定方針

### 1.1 description の置き場所

**YAML が正本。** C# 側の `///` は生成物であり、手で書かない。

```yaml
name: LogEnvelopeV1
kind: message
surfaces: [unity, debugstudio]
description: |
  sender と receiver の間で共有する realtime log contract v1。
  ZLogger の内部表現をそのまま wire format に流さず、
  受信アプリが安定して扱える小さな DTO に落としている。
fields:
  - id: 0
    name: SchemaVersion
    type: i32
    default: 1
    description: wire format のスキーマバージョン。receiver が sender の更新に追随できているかの判定基準。
```

対応する 3 箇所すべてに `description` を許可する:

| 位置 | 出力先 |
|---|---|
| ドキュメント直下（型単位） | 型の `/// <summary>` |
| `fields[]` の各要素 | プロパティの `/// <summary>` |
| enum の `members[]` の各要素 | enum メンバーの `/// <summary>` |

**`description` が無い場合は `///` を出力しない。** 空の `<summary></summary>` を出さないこと。

### 1.2 インライン記法との両立（重要）

現行 YAML の大半のフィールドはフロースタイル 1 行で書かれている。

```yaml
  - { id: 0, name: SchemaVersion, type: i32, default: 1 }
```

description を足すと 1 行に収まらなくなる。**フロー / ブロックのどちらでも読めるようにローダを直すのではなく、description を付けるフィールドだけブロックスタイルへ展開する。** YamlDotNet の `YamlMappingNode` はどちらも同じ構造で読めるため、ローダ側の変更は不要（`description` キーの読み取り追加のみ）。

### 1.3 旧コメントの取得元

`9c6463d`（PR #10 の分岐元 = 移行前の手書き DTO）。

```bash
git show 9c6463d:unity/Assets/OneStarMaker/Scripts/Foundation/Logging/LogEnvelopeV1.cs
git show 9c6463d:tools/DebugStudio/src/DebugStudio.Contracts/Protocol/LogEnvelopeV1.cs
```

**両側で文面が違う場合がある。** その場合は Unity 側を優先し、DS 側にしか無い情報（UI 都合の記述など）は本文に統合する。統合できない側固有の記述は YAML に入れず、手書き partial 側（`LogEnvelopeV1.Kind.cs` 等）に残す。

### 1.4 やらないこと

- コメントの「改善」。**移設であって推敲ではない。** 原文を保つ
- `<para>` などの XML タグの再構成。原文が持っていれば残す
- description の英訳。このリポジトリの既存コメントは日本語

---

## 2. 変更対象ファイル一覧（A-1: 規模見積もり）

| ファイル | 現在行数 → 予想行数 | 責務数 | 備考 |
|---|---|---|---|
| `protocol/debugsocket/SCHEMA.md` | 52 → 約 75 | 1 | `description` の仕様を追記 |
| `tools/protocol-codegen/Model/SchemaModels.cs` | 70 → 約 85 | 1 | `Description` プロパティを 3 箇所に追加 |
| `tools/protocol-codegen/Loading/YamlSchemaLoader.cs` | 313 → 約 345 | 1 | `description` の読み取り |
| `tools/protocol-codegen/Emitters/MessagePackCsharpEmitter.cs` | 369 → 約 420 | 1 → 2 | **下記 A-2 参照** |
| `protocol/debugsocket/envelopes/*.yaml`（19 ファイル） | 各 10〜41 → 各 +20〜80 | — | backfill 本体 |
| `protocol/debugsocket/enums.yaml` | 85 → 約 160 | — | enum メンバーの description |
| `protocol/debugsocket/messages.yaml` | 18 → 約 35 | — | message type の description |
| 生成 `.cs` 53 ファイル | — | — | 再生成のみ。**手で触らない** |

### A-2: 500 行 / 3 責務を超える見込みへの対処

`MessagePackCsharpEmitter.cs` は 369 → 420 行で 500 行は超えないが、**「型を出力する」責務に「XML doc コメントを整形する」責務が混ざる**。doc コメント整形（複数行 description の `/// ` 前置、XML エスケープ、空 description の抑止）は次の新規ファイルへ切り出すこと。

- **新規: `tools/protocol-codegen/Emitters/XmlDocWriter.cs`（約 60 行想定）**

これは**設計判断としてそう決めた**（A-3）。理由は A-4 に書くとおり、この整形ロジックが本スライスで唯一テストを書くべき箇所だから。emitter 本体に埋めるとテストが書けなくなる。

### A-3: 既存ファイルへの新責務割り当て

上記以外に既存ファイルへ新しい責務を足す箇所は無い。`YamlSchemaLoader` への `description` 読み取り追加は既存責務（YAML → モデル）の範囲内。

---

## 3. A-4: 単体テストの要求（必須）

**`XmlDocWriter` は `ProtocolSchema` にも `File` I/O にも依存しない純粋関数として書くこと。文字列 → 文字列。これができていないとテストが書けない。**

新規テストプロジェクト `tools/protocol-codegen/tests/ProtocolCodegen.Tests/`（xunit、`DebugStudio.sln` には**入れない**。`dotnet test` を個別に叩く）に、最低限これらを書く:

| # | 入力 | 期待 |
|---|---|---|
| T1 | `null` / 空文字 / 空白のみ | 出力ゼロ行（`///` を一切出さない） |
| T2 | 1 行の description | `/// <summary>` / `/// {text}` / `/// </summary>` の 3 行 |
| T3 | 複数行（`\n` 含む） | 各行に `/// ` が前置され、行末に余分な空白が無い |
| T4 | `<` `>` `&` を含む description | XML エスケープされる（`&lt;` 等）。**ただし原文が `<para>` 等の正当な XML タグを含む場合の扱いを本書で決めきれていないため、実装者は「エスケープする / しない」のどちらかを選び、選んだ理由をテスト名かコメントに残すこと** |
| T5 | 任意のインデント幅（Unity は braced NS で 8 スペース、DS は file-scoped で 4 スペース） | 指定インデントが全行に付く |

加えて **回帰の錨**として、既存の `--check` と PROTO-00 golden fixture を必ず通す（§5 参照）。

---

## 4. 実装順序

```text
1. SCHEMA.md に description 仕様を書く
2. SchemaModels に Description を足す
3. YamlSchemaLoader で読む
4. XmlDocWriter を新規で書く + T1〜T5 のテストを書く（ここでテストが緑になるまで先へ進まない）
5. MessagePackCsharpEmitter から XmlDocWriter を呼ぶ
6. YAML 1 ファイル（log_envelope_v1.yaml 推奨）だけ backfill して再生成し、
   生成 .cs の diff が「コメント行の追加のみ」であることを目視確認する  ← ここが一番の検問
7. 6 が確認できてから残り 20 ファイルを backfill
8. generate.sh で再生成 → --check → 両側テスト
```

**手順 6 を飛ばさないこと。** 20 ファイル backfill してから差分を見ると、コメント以外の混入に気づけない。

---

## 5. 受入条件

- [ ] `./tools/protocol-codegen/generate.sh --check` が緑
- [ ] `dotnet test tools/protocol-codegen/tests/ProtocolCodegen.Tests` が緑（T1〜T5）
- [ ] `dotnet test tools/DebugStudio/DebugStudio.sln -c Release` が Failed 0
- [ ] `pwsh tools/run-tests.ps1`（Unity Editor を閉じて実行）が 1 件以上実行され failed 0
- [ ] **`9c6463d` からの API サーフェス差分が PR #10 時点と同一であること。** 下記コマンドの出力が、コメント行以外で増えていないこと:

```bash
git diff <PR10マージ後のdevelop> HEAD -- 'unity/Assets/OneStarMaker/Scripts/**/*.cs' 'tools/DebugStudio/src/DebugStudio.Contracts/Protocol/*.cs' | grep -E "^[+-]" | grep -vE "^[+-]\s*///|^[+-]{3}"
```

  → **出力が空**であること。空でなければコメント以外を変えている

- [ ] 旧コメント 416 行のうち、意図的に落としたものがあれば理由を本書 §6 に列挙してある
- [ ] 生成 `.cs` を手編集していない（`--check` が緑ならこれは自動的に担保される）

---

## 6. Phase C からの差し戻し

（空。Phase C が記入する）

---

## 7. Phase C レビュー

（空。Phase C が記入する）

---

## 8. Phase C' 監査

（空。実装にも設計にも関与していないモデルが記入する）
