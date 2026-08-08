# Telemetry セッション属性の付与 + 死んだ mapping の除去 ハンドオフ (2026-08-08)

| | |
|---|---|
| スライス | P1（セッション属性）。~~P3（dead mapping 除去）~~ → **2026-08-08 に中止。§4.5 参照** |
| ブランチ | `feature/telemetry-session-attributes` |
| Phase | A 完了（**2026-08-08 に §4.5 で改訂**）/ B 未着手 |
| 後続 | P2（Frame sample にシーン名）は**このスライスに含めない**。別 HANDOFF |

> **⚠ 実装前に §4.5 と §4.6 を必ず読むこと。**
> §4.5 — Phase B の着手時に §1.4 の前提が事実と異なることが判明し、P3 を中止して P1-7 を追加した。
> §1.4 / §3.3 / §4 の P3-1 / §5.1 の T8 は §4.5 で上書きされている。
> §4.6 — `unityVersion` → **`engineVersion`** に改名した（P1-8）。
> **§2 / §4 の P1-2 / P1-3 / P1-6 / P3-1 に残っている `UnityVersion` / `unityVersion` の記述は §4.6 で上書きされている。**

このドキュメントは自己完結で書いてある。**他のドキュメントを読む必要はない。**
必要な既存コードの内容はすべて本文に転記してある。

---

## 0. 1分で把握

Unity のテレメトリを Elasticsearch に貯めているが、**どのビルド・どの端末で観測された値なのかが1件も記録されていない**。
そのため Kibana で「前のビルドより遅くなった」が言えない。実行をまたいだ比較ができないなら Elastic に貯める意味がない。

やること:

1. **P1** — handshake の Welcome に `buildVersion` / `platform` / `deviceModel` / `osVersion` / `engineVersion` を載せ、DebugStudio が telemetry の全ドキュメントへ付与する
2. ~~**P3** — index template にあるのに誰も書き込んでいない `event.*` / `service.*` / `trace.*` / `span.*` の mapping を削除する~~ → **中止（§4.5.2）。前提が事実と異なった**

やらないこと:

- log ストリーム（`debugstudio-log-*`）への属性付与 → §1.5
- Kibana ダッシュボードの作成 → 本スライス完了後に別途
- ECS 準拠 → §1.4

---

## 1. 確定方針（設計判断。実装側で変更しない）

### 1.1 属性は「毎ドキュメント送信」ではなく「handshake で1回送る」

セッション定数なので、テレメトリ1件ごとに wire に載せるのは帯域とホットパスの無駄。
**Unity 側の telemetry 生成経路には一切触らない。**

```
Unity: bootstrap で値を採取 → Welcome に載せて1回送信
DebugStudio: Welcome を受けて sessionId ごとに保持 → export/永続時に全 record へ付与
```

### 1.2 付与は sessionId をキーにした辞書引きにする（**最重要**）

「現在接続中のセッションの属性を使う」実装にしてはいけない。
既存コード `unity/Assets/OneStarMaker/Scripts/Foundation/DebugSocket/UnitySessionCorrelationContext.cs` の doc コメントが、この罠を名指しで警告している（原文ママ）:

> `SessionId` は DebugSocket handshake Welcome に載せる ID と必ず同一にする。
> export 時に DebugStudio 側で後付けすると、**再接続・遅延受信・過去ファイル export で別 session を誤付与し得る**ため、wire message 作成時点で producer が確定させる。

telemetry record は既に `SessionId` を持っている。**その値をキーに属性を引く。**
再接続で別ビルドの Unity が繋がっても、古い retained record には古い属性が付く。

### 1.3 未知の sessionId には何も付与しない

属性が引けなかったら **キー自体を出力しない**（`null` や `"unknown"` を入れない）。
既存の mapper は「無意味な 0 / -1 を出さない」方針で書かれており、それに揃える。
欠測は欠測のまま出したほうが Kibana 上で「データが無い」と分かる。

### 1.4 P3 は「埋める」ではなく「消す」【❌ 2026-08-08 撤回。§4.5 が正】

> **この節の前提は誤りだった。P3 はこのスライスから外す。以下は経緯として残す。**

`event.category` / `event.action` / `service.name` / `trace.id` / `span.id` / `span.parent.id` は
index template に mapping されているが、`TelemetryExportRecord` に対応するプロパティが無く、**誰も書き込んでいない**。
Kibana のフィールド一覧には出るのに常に空になり、ダッシュボードを組むとき確実に踏む。

分割軸は `name`（9種の処理名）と `tags`（8種の異常タグ）で足りているので、**削除する**。
ECS 準拠にするなら別スライスで正面から議論する。このスライスでは埋めない。

### 1.5 log ストリームは対象外

`debugstudio-log-*` にも同じ属性があると嬉しいが、
ログの index template / ingest pipeline / export record は telemetry と別系統で、
同時に触ると差分が倍になりレビュー不能になる。**telemetry のみ。**

---

## 2. 追加するフィールド（確定）

| NDJSON キー | 型 | Unity 側の値 | 例 |
|---|---|---|---|
| `buildVersion` | keyword | `Application.version` | `"1.4.2"` |
| `platform` | keyword | `Application.platform.ToString()` | `"WindowsPlayer"` |
| `deviceModel` | keyword | `SystemInfo.deviceModel` | `"Pixel 8"` |
| `osVersion` | keyword | `SystemInfo.operatingSystem` | `"Android OS 14 / API-34"` |
| ~~`unityVersion`~~ → **`engineVersion`** | keyword | `Application.unityVersion` | `"6000.5.0f1"` |

命名は既存の flat camelCase（`sessionId` / `producerSequence` / `elapsedMs`）に揃える。ネストしない。

> **§4.6（2026-08-08）で `unityVersion` → `engineVersion` に改名した。** DebugStudio は Unity に依存しないため。
> 契約（YAML field id 13 の name）ごと改名しており、**id は動かしていない**。

Welcome YAML の field id は既存の 0〜8 の続きで **9〜13** を使う。

---

## 3. 変更対象ファイル一覧（A-1: 規模見積もり）

「現在行数 → 予想行数 / 責務数」。**予想を超えそうになったら実装を止めて §6 に書くこと。**

### 3.1 契約（codegen 経由。手編集禁止）

| ファイル | 行数 | 責務 |
|---|---|---|
| `protocol/debugsocket/envelopes/capability_handshake_welcome_envelope_v1.yaml` | 17 → 22 / 1 | 契約定義 |
| `unity/.../Foundation/DebugSocket/CapabilityHandshakeWelcomeEnvelopeV1.cs` | 43 → 58 / 1 | **生成物** |
| `tools/DebugStudio/src/DebugStudio.Contracts/Protocol/CapabilityHandshakeWelcomeEnvelopeV1.cs` | 41 → 56 / 1 | **生成物** |

生成物 2 つは `tools/protocol-codegen/generate.ps1` の出力。**手で書き換えない。**

### 3.2 Unity 側

| ファイル | 行数 | 責務 |
|---|---|---|
| `unity/.../Foundation/DebugSocket/UnitySessionAttributes.cs` | **新規 0 → 80 / 1** | 起動時の環境値をメインスレッドで焼き込む |
| `unity/.../Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 814 → 815 / 変化なし | capture 呼び出し **1 行だけ** |
| `unity/.../Runtime/DebugSocketServices/DebugSocketService.Inbound.cs` | 114 → 122 / 変化なし | Welcome に 5 フィールド代入 |

### 3.3 DebugStudio 側

| ファイル | 行数 | 責務 |
|---|---|---|
| `src/DebugStudio.Export/Models/TelemetrySessionAttributes.cs` | **新規 0 → 45 / 1** | 属性の値オブジェクト |
| `src/DebugStudio.App/Core/Stores/TelemetrySessionAttributesStore.cs` | **新規 0 → 90 / 1** | sessionId → 属性の保持と引き当て |
| `src/DebugStudio.Export/Models/TelemetryExportRecord.cs` | 96 → 116 / 1 | プロパティ 5 追加 |
| `src/DebugStudio.App/Core/Services/TelemetryRecordExportMapper.cs` | 176 → 200 / 1 | 引数追加と代入 |
| `src/DebugStudio.App/Core/Services/SessionMessageRouter.cs` | 118 → 122 / 変化なし | Welcome を新 store にも流す |
| `src/DebugStudio.App/Core/Services/TelemetryPersistenceService.cs` | 48 → 58 / 変化なし | store 注入 |
| `src/DebugStudio.App/Core/Services/TelemetryExportService.cs` | 114 → 124 / 変化なし | store 注入 |
| `src/DebugStudio.App/Core/Services/ElasticTelemetryPushService.cs` | 203 → 213 / 変化なし | store 注入 |
| `src/DebugStudio.App/Core/Composition/AppCompositionRoot.cs` | 273 → 281 / 変化なし | 配線 |
| `src/DebugStudio.Export/Elastic/ElasticTelemetryIndexTemplateDefinition.cs` | 176 → ~~**160**~~ **181** / 1 | ~~P1 で +5、P3 で −21~~ → **P1 の +5 のみ（§4.5）** |
| `src/DebugStudio.Export/Elastic/ElasticBulkTelemetryNdjsonBuilder.cs` | 188 → 193 / 1 | **§4.5 で追加。** `CreatePayloadDictionary` に 5 キー |

**このスライスが 500 行 / 3 責務を超えさせるファイルは無い。**
新規・改修対象で最大は index template の 176 行で、P3 の削除により減る。

ただし `AbstractApplicationInitializer.cs` は **既に 814 行あり、着手前から 500 行を超えている**。
これは**このスライスが作った負債ではない**。
今回そこへ足すのは `UnitySessionAttributes.Capture();` の **1 行だけ**であり、
初期化ロジックをこのファイルへ追加で書き込まないこと。分割は別スライスの議題。

### 3.4 新責務の配置（A-3: これは設計判断としてこう決めた）

| 新責務 | 置き場 | なぜそこか |
|---|---|---|
| 環境値の採取 | **新規 `UnitySessionAttributes.cs`** | `UnitySessionCorrelationContext` に相乗りさせない。あれは「ID と採番」の責務であって「端末情報」ではない。混ぜると 95 行が 2 責務になる |
| 属性の保持 | **新規 `TelemetrySessionAttributesStore.cs`** | `CapabilityStateStore` に相乗りさせない。あれは「現在の negotiation 状態」の 1 スナップショットで、**過去 session を複数保持できない構造**。§1.2 の要件を満たせない |
| 属性の値オブジェクト | `DebugStudio.Export/Models/` | mapper は App にあるが record は Export にある。Export 側に置かないと Export → App の逆流参照になる |

---

## 4. 施工チケット

### P1-1 契約に 5 フィールドを足す

`protocol/debugsocket/envelopes/capability_handshake_welcome_envelope_v1.yaml` の現在の内容（全文）:

```yaml
name: CapabilityHandshakeWelcomeEnvelopeV1
kind: message
surfaces: [unity, debugstudio]
fields:
  - { id: 0, name: SchemaVersion, type: i32, default: 1 }
  - { id: 1, name: SessionId, type: string, default: "" }
  - { id: 2, name: ServerName, type: string, default: "" }
  - { id: 3, name: SelectedSchemaVersion, type: i32, default: 1 }
  - { id: 4, name: ServerCapabilities, type: DebugStudioCapability, default: None }
  - { id: 5, name: NegotiatedCapabilities, type: DebugStudioCapability, default: None }
  - { id: 6, name: SupportedMessageTypes, type: array<i32>, default: [] }
  - { id: 7, name: TimestampUnixTimeMilliseconds, type: i64 }
  - { id: 8, name: StatusMessage, type: string, default: "" }
encodings:
  messagepack:
    key_field: id
```

`fields` の末尾に追加する:

```yaml
  - { id: 9,  name: BuildVersion,  type: string, default: "" }
  - { id: 10, name: Platform,      type: string, default: "" }
  - { id: 11, name: DeviceModel,   type: string, default: "" }
  - { id: 12, name: OsVersion,     type: string, default: "" }
  - { id: 13, name: UnityVersion,  type: string, default: "" }
```

**既存 id を変更・並べ替えしない。** MessagePack の key は id なので、動かすと wire 互換が壊れる。

再生成:

```bash
pwsh tools/protocol-codegen/generate.ps1
```

検証:

```bash
pwsh tools/protocol-codegen/generate.ps1 --check
```

### P1-2 Unity: 環境値をメインスレッドで焼き込む

新規 `unity/Assets/OneStarMaker/Scripts/Foundation/DebugSocket/UnitySessionAttributes.cs`。

要件:

- `static` クラス。`Capture()` でメインスレッドから値を採取して保持する
- `BuildVersion` / `Platform` / `DeviceModel` / `OsVersion` / `UnityVersion` の 5 プロパティ（すべて `string`、未 capture 時は `string.Empty`）
- `Capture()` は複数回呼ばれても壊れないこと（domain reload で再入する）
- テスト用に `ResetForTests()` を `internal` で持たせる（既存 `UnitySessionCorrelationContext` に同名の慣習がある）
- `.meta` ファイルを忘れない

呼び出し場所は `unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` の
`BootstrapSubsystemRegistration`。現状（抜粋）:

```csharp
protected static void BootstrapSubsystemRegistration(AbstractApplicationInitializer instance)
{
    try
    {
        // domain reload / player restart の切替点で session / sequence を切り替え、
        // 旧 session の Log / Telemetry に新 ID を混ぜない。
        UnitySessionCorrelationContext.ResetForNewPlayerSession();
        instance.ReleaseAll();
    }
    catch (Exception ex)
    {
        Debug.LogException(ex);
    }
}
```

`ResetForNewPlayerSession()` の直後に `UnitySessionAttributes.Capture();` を足す。

> **なぜ bootstrap で焼き込むのか（罠）**
> `Application.version` / `SystemInfo.deviceModel` などは **Unity のメインスレッドからしか安全に読めない**。
> Welcome を組み立てる `DebugSocketService.Inbound.cs` はソケット受信経路で、メインスレッドである保証がない。
> 採取と使用を分離すること。

### P1-2b Welcome に代入する

`unity/Assets/OneStarMaker/Scripts/Runtime/DebugSocketServices/DebugSocketService.Inbound.cs` の該当箇所（現状）:

```csharp
return DebugSocketProtocol.SerializeMessage(
    DebugSocketMessageType.CapabilityWelcome,
    new CapabilityHandshakeWelcomeEnvelopeV1
    {
        SessionId = sessionId,
        ServerName = string.IsNullOrWhiteSpace(Application.productName) ? "Unity Player" : Application.productName,
        SelectedSchemaVersion = selectedSchemaVersion,
        ServerCapabilities = runtimeAvailableCapabilities,
        NegotiatedCapabilities = negotiatedCapabilities,
        SupportedMessageTypes = SupportedMessageTypes,
        TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        StatusMessage = statusMessage,
    });
```

5 フィールドを `UnitySessionAttributes` から代入する。

**あわせて `ServerName` の `Application.productName` 直読みも `UnitySessionAttributes` 経由に移す。**
同じ行の隣で同じスレッド制約を踏んでいる既存の危うさで、放置すると「新しい 5 つだけ安全」という中途半端な状態になる。
`ProductName` プロパティを `UnitySessionAttributes` に足し、フォールバック `"Unity Player"` はこの呼び出し側に残す（挙動を変えない）。

### P1-3 DebugStudio: 属性の値オブジェクト

新規 `tools/DebugStudio/src/DebugStudio.Export/Models/TelemetrySessionAttributes.cs`。

`sealed record` で `BuildVersion` / `Platform` / `DeviceModel` / `OsVersion` / `UnityVersion` を持つ。
空文字は「無し」として扱えるようにしておく（§1.3 でキーを出さない判断に使う）。

### P1-4 DebugStudio: sessionId → 属性の store

新規 `tools/DebugStudio/src/DebugStudio.App/Core/Stores/TelemetrySessionAttributesStore.cs`。

API:

| メソッド | 意味 |
|---|---|
| `void ApplyWelcome(CapabilityHandshakeWelcomeEnvelopeV1 welcome)` | `welcome.SessionId` をキーに属性を記録。SessionId が空なら何もしない |
| `TelemetrySessionAttributes? TryGet(string? sessionId)` | 未知・null・空なら `null` |

- **複数 session を同時に保持できること。**再接続で上書き消去してはいけない（§1.2）
- スレッド安全にすること。受信は socket スレッド、export は UI スレッドから来る
- 無制限に増えないよう上限を設ける（**上限 32 session、超えたら古いものから捨てる**）。DebugStudio は開発ツールなので厳密な LRU でなくてよいが、上限は必ず入れる

配線は `tools/DebugStudio/src/DebugStudio.App/Core/Services/SessionMessageRouter.cs` の既存箇所:

```csharp
public void RouteCapabilityWelcomeMessage(CapabilityHandshakeWelcomeEnvelopeV1 welcome)
{
    // capability welcome だけは store mutation が単なる蓄積ではなく、
    // negotiation 結果の正本更新になる。ここで state store を更新してから外へ通知する。
    _capabilityStateStore.ApplyWelcome(welcome);
    CapabilityWelcomeReceived?.Invoke(welcome);
}
```

ここに新 store への `ApplyWelcome` を足す。`CapabilityStateStore` は**変更しない**（§3.4）。

### P1-5 export record と mapper

`TelemetryExportRecord.cs` に 5 プロパティ（すべて `string?`）を追加。

`TelemetryRecordExportMapper.ToExportRecord` に第 2 引数
`TelemetrySessionAttributes? sessionAttributes = null` を足す。
既定値 `null` にすることで既存のテストがコンパイルできる。

属性が `null`、または個々の値が空文字なら **そのキーを出力しない**（`null` を代入する）。
既存の mapper は同じ思想で書かれている。該当コメント（原文ママ）:

```csharp
// TimingMemory / CameraCounters など payload が正のとき、無意味な flat 0/-1 を出さない。
```

呼び出し側は 3 箇所。すべて `store.TryGet(record.SessionId)` の結果を渡す:

| ファイル | 行 |
|---|---|
| `Core/Services/TelemetryPersistenceService.cs` | 37 |
| `Core/Services/TelemetryExportService.cs` | 61 |
| `Core/Services/ElasticTelemetryPushService.cs` | 147 |

3 つとも store をコンストラクタ注入する。配線は
`Core/Composition/AppCompositionRoot.cs`（`capabilityStateStore` を作っている 132 行目付近と、
`new TelemetryPersistenceService(messageRouter, writer)` の 230 行目付近）。

### P1-6 index template に 5 keyword を追加

`tools/DebugStudio/src/DebugStudio.Export/Elastic/ElasticTelemetryIndexTemplateDefinition.cs` の
`properties` 辞書に追加する:

```csharp
["buildVersion"] = new { type = "keyword" },
["platform"] = new { type = "keyword" },
["deviceModel"] = new { type = "keyword" },
["osVersion"] = new { type = "keyword" },
["unityVersion"] = new { type = "keyword" },
```

### P3-1 死んだ mapping を削除【❌ 2026-08-08 中止。実装しない。§4.5 が正】

同じファイルから以下を**削除**する（現状の該当ブロック）:

```csharp
["event"] = new
{
    properties = new
    {
        category = new { type = "keyword" },
        action = new { type = "keyword" },
    }
},
["trace"] = new
{
    properties = new
    {
        id = new { type = "keyword" },
    }
},
["span"] = new
{
    properties = new Dictionary<string, object?>
    {
        ["id"] = new { type = "keyword" },
        ["parent"] = new
        {
            properties = new
            {
                id = new { type = "keyword" },
            }
        }
    }
},
["service"] = new
{
    properties = new
    {
        name = new { type = "keyword" },
    }
},
```

**`traceId` / `spanId` / `parentSpanId`（flat / `long` 型）は残す。**
これらは mapper が実際に書き込んでいる。消すのはネストした ECS 風の `trace.id` / `span.id` の方だけ。

---

## 4.5 Phase B からの差し戻しと Phase A の再判断（2026-08-08）

Phase B（Grok 4.5）が着手前に §1.4 と §3 の前提誤りを 2 件報告し、実装を止めた。
Phase A（Claude Code）が実コードで裏取りし、**2 件とも事実**と確認した。人間の判断を経て以下に改訂する。

### 4.5.1 【追加チケット P1-7】Elastic `_bulk` 経路にも 5 キーを載せる

**誤り:** §3.3 の変更対象ファイル一覧に `ElasticBulkTelemetryNdjsonBuilder.cs` が入っていなかった。

`TelemetryExportRecord` にプロパティを足せば全経路に載る、というのが §3 の暗黙の前提だったが、
Elastic Push（`ElasticTelemetryPushService` → `BuildBulkPayload`）は record を直接 serialize しておらず、
`ElasticBulkTelemetryNdjsonBuilder.CreatePayloadDictionary` の**明示的なホワイトリスト Dictionary** を経由する。
ファイル export（`NdjsonTelemetryRecordSerializer`）だけが record をそのまま serialize する。

したがって **P1-5 だけでは §5.3 の curl 確認は 5 フィールドを返さない**。

**施工:** `tools/DebugStudio/src/DebugStudio.Export/Elastic/ElasticBulkTelemetryNdjsonBuilder.cs` の
`CreatePayloadDictionary` に、既存の `["producerSequence"]` の並びに合わせて 5 キーを追加する:

```csharp
["buildVersion"] = record.BuildVersion,
["platform"] = record.Platform,
["deviceModel"] = record.DeviceModel,
["osVersion"] = record.OsVersion,
["unityVersion"] = record.UnityVersion,
```

`SerializerOptions` は `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` なので、
`null` を入れればキーは出力されない。§1.3 の方針はこの経路でもそのまま成立する。**`""` を入れないこと**（空文字はキーが出てしまう）。

> **ファイル export と `_bulk` の byte 一致は既存テストで守られている**
> （`ElasticBulkTelemetryNdjsonBuilderTests.BuildBulkPayload_ファイルexportとbyte完全一致する`）。
> `ElasticBulkTelemetryExportWriter` も同じ builder を通るので、片方だけ直すことはできない構造になっている。

### 4.5.2 【中止】P3 はこのスライスから外す

**誤り:** §1.4 の「`event.*` / `service.*` / `trace.*` / `span.*` は誰も書き込んでいない」は事実ではない。

`ElasticBulkTelemetryNdjsonBuilder.CreatePayloadDictionary` が `ElasticTelemetryDocumentFactory.Create` 経由で
**全ドキュメントに毎回書き込んでいる**（`tools/DebugStudio/src/DebugStudio.Export/Elastic/ElasticTelemetryDocumentFactory.cs`）:

| nested フィールド | 実際に入る値 | 既存 flat フィールドとの関係 |
|---|---|---|
| `event.category` | `record.Stream` | `stream` と重複 |
| `event.action` | `record.Name` | `name` と重複 |
| `service.name` | `record.Source` | `source` と重複 |
| `trace.id` | `record.TraceId` の文字列化 | `traceId`（long）と重複 |
| `span.id` / `span.parent.id` | `record.SpanId` / `ParentSpanId` の文字列化 | `spanId` / `parentSpanId` と重複 |

つまり実態は「**常に空の死んだ mapping**」ではなく「**既存フィールドの型違い重複**」であり、§1.4 の削除根拠は成立しない。
mapping だけ消しても書き込みは残るため、dynamic mapping で `text` + `keyword` の multi-field として生え直し、
「Kibana のフィールド一覧から消す」という目的も達成しない。

**判断（人間）: P3 をこのスライスから外す。** 理由:

- 削除根拠だった前提が消えた以上、「消す/残す/ECS に寄せる」は改めて正面から決める議題になる
- 書き込み側まで消すと `ElasticTelemetryDocumentFactory` / `ElasticTelemetryDocument` の削除まで波及し、
  P1 とレビュー対象が混ざる（§1.5 で log ストリームを外したのと同じ理由）
- §1.4 自身が「ECS 準拠にするなら別スライスで正面から議論する」と書いている

**したがって `ElasticTelemetryIndexTemplateDefinition.cs` からは何も削除しない。P1-6 の +5 だけ行う。**
後続スライスの論点は「nested ECS 風フィールドと flat フィールドの重複をどちらに寄せるか」。

### 4.5.3 この差し戻しが Phase A に残す教訓（続きは §4.6）

「`TelemetryExportRecord` にプロパティを足せば全経路に載る」を確認せずに書いた。
**出力経路が 2 本（ファイル serialize / 明示 Dictionary）あることは、`grep TelemetryExportRecord` で 1 分で分かった。**
今後、export record にフィールドを足す HANDOFF では、**consumer を grep で列挙してからファイル一覧を書く**こと。

---

## 4.6 【追加チケット P1-8】`unityVersion` → `engineVersion` に改名する（2026-08-08 / 人間の判断）

### 4.6.1 なぜ

**DebugStudio は Unity に依存しない。** .NET 8 のツールで、Unity へのコード依存は 1 つも無い。
にもかかわらず、このスライスは DebugStudio 側に `UnityVersion` という producer 固有の語を新しく持ち込んでいた。

他の 4 フィールド（`buildVersion` / `platform` / `deviceModel` / `osVersion`）は既に producer 非依存の語なので、
**5 つのうち 1 つだけが Unity に寄っている**状態だった。

**この field はまだ 1 度も wire に流れていない**（このスライスが未マージ）ため、**契約ごと改名して互換コストはゼロ**。

### 4.6.2 施工

| 対象 | 変更 |
|---|---|
| `protocol/debugsocket/envelopes/capability_handshake_welcome_envelope_v1.yaml` | id 13 の name を `UnityVersion` → `EngineVersion`。**id 13 は動かさない** |
| 生成物 2 つ（Unity / DebugStudio の `CapabilityHandshakeWelcomeEnvelopeV1.cs`） | `generate.ps1` で再生成。手編集しない |
| `unity/.../DebugSocketService.Inbound.cs` | `EngineVersion = UnitySessionAttributes.UnityVersion` |
| `unity/.../UnitySessionAttributes.cs` | **変更しない。** Unity 側で `UnityVersion` と呼ぶのは正しい（実際に Unity だから） |
| `DebugStudio.Export/Models/TelemetrySessionAttributes.cs` | `UnityVersion` → `EngineVersion` |
| `DebugStudio.Export/Models/TelemetryExportRecord.cs` | 同上。**加えて新 5 プロパティの doc コメントから Unity の API 名を除去**（下記 4.6.3） |
| `DebugStudio.App/Core/Stores/TelemetrySessionAttributesStore.cs` | 同上 |
| `DebugStudio.App/Core/Services/TelemetryRecordExportMapper.cs` | 同上 |
| `DebugStudio.Export/Elastic/ElasticBulkTelemetryNdjsonBuilder.cs` | NDJSON キー `"unityVersion"` → `"engineVersion"` |
| `DebugStudio.Export/Elastic/ElasticTelemetryIndexTemplateDefinition.cs` | mapping キー同上 |
| テスト（T5 / T7 / T10 / T11 / store / wire 互換） | 新しい名前へ追随 |

**境界の考え方: 改名の境界は wire（契約）に置く。** Unity 側は「Unity のバージョン」を採取しているので `UnitySessionAttributes.UnityVersion` のままでよく、
それを Welcome へ載せる時点で producer 非依存の `EngineVersion` になる。

### 4.6.3 DebugStudio 側の doc コメントから Unity の API 名を除去する

`TelemetryExportRecord.cs` の新 5 プロパティの doc コメントが Unity の API 名を直書きしている（現状）:

```csharp
/// <summary>Application.version。未知 session では null（キー省略）。</summary>
/// <summary>Application.platform。未知 session では null（キー省略）。</summary>
/// <summary>SystemInfo.deviceModel。未知 session では null（キー省略）。</summary>
/// <summary>SystemInfo.operatingSystem。未知 session では null（キー省略）。</summary>
/// <summary>Application.unityVersion。未知 session では null（キー省略）。</summary>
```

**DebugStudio の model が Unity の API を知っている必要は無い。** 意味で書き直す
（例: 「producer のビルドバージョン」「producer の実行プラットフォーム」「端末モデル」「OS バージョン」「producer の engine/runtime バージョン」）。
「未知 session では null（キー省略）」の部分は情報量があるので残すこと。

### 4.6.4 スコープ外（既存負債）

`UnityFrameAtStart` / `UnityFrameAtEnd` / `UnityFrameAtEmit` / `unityFrame` は**このスライス以前から DebugStudio に居る**。
既に Elastic に投入済みのデータがあり、改名すると index 互換を壊す。**このスライスでは触らない。別スライスの議題。**

---

## 5. 受入条件

### 5.1 必ず書く単体テスト（A-4）

| # | 対象 | 検証内容 | 置き場 |
|---|---|---|---|
| T1 | `TelemetrySessionAttributesStore` | 未知 sessionId → `null` | `tests/DebugStudio.App.Tests/Stores/` |
| T2 | 同上 | **2 つの session を同時に保持し、それぞれ正しい属性が引ける**（§1.2 の核心） | 同上 |
| T3 | 同上 | 空 SessionId の Welcome を渡しても登録されない | 同上 |
| T4 | 同上 | 上限を超えたら件数が上限以下に保たれる | 同上 |
| T5 | `TelemetryRecordExportMapper` | 属性ありで 5 キーが NDJSON に出る | `tests/DebugStudio.App.Tests/` |
| T6 | 同上 | **属性 `null` のときキー自体が出ない**（`"buildVersion"` という文字列を含まないことを assert） | 同上 |
| T7 | `ElasticTelemetryIndexTemplateDefinition` | 5 keyword が含まれる | `tests/DebugStudio.Export.Tests/Elastic/` |
| ~~T8~~ | ~~同上~~ | ~~`"event"` / `"service"` の mapping が含まれない~~ **§4.5.2 で P3 中止のため削除。書かない** | — |
| T9 | `UnitySessionAttributes` | `Capture()` 前に読んでも例外にならず空文字が返る | `unity/Assets/OneStarMaker/Tests/Foundation/` |
| **T10** | `ElasticBulkTelemetryNdjsonBuilder` | **属性ありで 5 キーが `_bulk` NDJSON に出る**（§4.5.1。record に足しただけでは載らない経路の回帰止め） | `tests/DebugStudio.Export.Tests/Elastic/` |
| **T11** | 同上 | **属性 `null` のときキー自体が出ない**（T6 の `_bulk` 版） | 同上 |

> **T2 / T6 / T10 は必ず書くこと。** T2 は §1.2 の「別 session を誤付与しない」を守る唯一の機械的な保証で、
> T6 は §1.3 の「欠測を欠測のまま出す」が壊れたことを検出する唯一の手段。
> T10 は §4.5.1 の踏み外し（record に足しただけで Elastic に載ったつもりになる）を検出する唯一の手段。
> この 3 本が無いと、レビューで目視するしかなくなる。

テスト名は日本語で書く。既存の慣習（例: `SessionId_初期化後は固定されHandshakeと同一値になる`）に合わせる。

### 5.2 コマンド

```bash
pwsh tools/protocol-codegen/generate.ps1 --check
```

```bash
dotnet test tools/DebugStudio/DebugStudio.sln
```

```bash
pwsh tools/run-tests.ps1
```

Unity テストは **Unity Editor を閉じた状態で実行する**（プロジェクトロックで失敗する）。
exit 0 かつ 1 件以上実行され failed 0 であること。**テスト 0 件は失敗扱い**（コンパイルエラーが 0 件として現れる）。

### 5.3 実地確認（手動）

1. `cd tools/DebugStudio/elastic && docker compose up -d`
2. `dotnet run --project tools/DebugStudio/src/DebugStudio.ElasticArtifactGen`
3. `%LOCALAPPDATA%\DebugStudio\elastic-artifacts\commands\import-telemetry.ps1 -ElasticUrl http://localhost:9200`
4. DebugStudio を起動して Unity を接続し、しばらくプレイする
5. Telemetry パネルの **Elastic Preflight** → **Elastic Push**
6. 確認:

```bash
curl "http://localhost:9200/debugstudio-telemetry-*/_search?size=1&_source=buildVersion,platform,deviceModel,osVersion,engineVersion,sessionId"
```

5 フィールドが値付きで返ること。

> **index template の変更は既に存在する index には効かない。**
> 古い index が残っていると、追加した 5 keyword が mapping されないまま dynamic mapping で `text` として入る。
> 確認は新しい index（= 日付が変わった後、または `curl -X DELETE "http://localhost:9200/debugstudio-telemetry-*"` の後）で行う。
>
> **`event.*` が Kibana のフィールド一覧に出るのは §4.5.2 のとおり現状仕様。**このスライスでは消えない。

---

## 6. 共通の注意

**wire 互換**

- MessagePack の key は field id。**既存 id 0〜8 を動かさない**
- 新旧の組み合わせで落ちないこと。古い Unity ↔ 新 DebugStudio では 9〜13 が欠測 → 既定値 `""` → §1.3 によりキーが出ないだけ。**例外を投げてはいけない**
- PROTO-00 golden fixture（`protocol/debugsocket/fixtures/proto00/`）に Welcome は含まれていない（確認済み）。今回の変更では壊れないはずだが、`--check` で確認すること

**Unity 固有**

- `Application.*` / `SystemInfo.*` はメインスレッド専用。§P1-2 の焼き込みを飛ばさない
- 破棄済み `UnityEngine.Object` に対しては `?.` と `??` が Unity の `==` オーバーロードを迂回して短絡しない。
  **このスライスに `UnityEngine.Object` を扱う箇所は無いはずだが、新規 Unity コードでこの 2 演算子を使ったら §6 に報告すること**
- 新規 `.cs` には `.meta` を必ず付ける
- **Unity 側で `record` / `record struct` / `init` アクセサを使わない（2026-08-08 に実測で踏んだ）。**
  positional record は `init` を生成し、それには `System.Runtime.CompilerServices.IsExternalInit` が要るが Unity の参照アセンブリに存在せず、
  `error CS0518` でプロジェクト全体がコンパイル不能になる。`unity/Assets/OneStarMaker/Scripts` 配下に `record` 型宣言は 1 つも無い（既存慣習は plain struct / class）。
  **DebugStudio（.NET 8）では `record` を使ってよい。制約は Unity 側だけ。**

**規模**

- §3 の予想行数を超えそうになったら、**書き進める前に手を止めて §6 に書く**

---

## 7. Phase C からの差し戻し

<!-- Phase C レビュアが記入。実装者はここを消さない -->

### 巡目 1（Opus 4.8 / 静的レビュー・読み取り専用）

**差し戻しなし。** 詳細は §8。

### 巡目 2（Opus 5 / 最終チェック・実行検証あり）

#### R1【BLOCK】Unity がコンパイルできない — `UnitySessionAttributes.cs`

`pwsh tools/run-tests.ps1` を本体リポジトリで実行した実測:

```
Assets\OneStarMaker\Scripts\Foundation\DebugSocket\UnitySessionAttributes.cs(56,20): error CS0518:
Predefined type 'System.Runtime.CompilerServices.IsExternalInit' is not defined or imported
（57,20）（58,20）（59,20）（60,20）（61,20）も同じ。計 6 件
Unity 終了コード: 1 / 結果 XML なし / テスト 0 件実行
```

`private readonly record struct Snapshot(...)` の positional record が `init` アクセサを生成し、
それが要求する `System.Runtime.CompilerServices.IsExternalInit` が Unity の参照アセンブリに存在しない。

**なぜ巡目 1 で出なかったか（プロセスの穴）:**

| 担い手 | 検出できなかった理由 |
|---|---|
| Phase B（Grok 4.5） | worktree に `unity/Library/` が無く、Unity バッチモードを実行できない |
| Phase C 巡目 1（Opus 4.8） | `--plan` は read-only でシェルを拒否する。静的レビューではコンパイルは分からない |

**構造的な結論: Unity 側のコードは、Unity を実行できる場所で 1 回コンパイルするまで「実装完了」ではない。**
`--plan` レビューと worktree 実装の組み合わせは、この検証を誰も担当しない穴を作る。§6 に恒久ルールとして追記した。

**対処:** Phase B へ差し戻し。`record` を使わない実装へ置換（不変スナップショットの設計意図は維持）。

---

## 8. Phase C レビュー

<!-- Phase C レビュアが記入。git diff --stat から始め、機能レビューの前に構造レビューを行う -->

担い手: 巡目 1 = Opus 4.8（cursor-agent `--plan`、読み取り専用）/ 巡目 2 = Opus 5（Claude Code、実行検証あり）
実装: Grok 4.5（cursor-agent、git worktree 隔離）

### 8.1 構造レビュー（機能レビューより先に実施）

`git diff --stat`: 32 ファイル / +646 / −46。**50% 以上増えたファイルは無い。**

| ファイル | §3 の予想 | 実測 | 判定 |
|---|---|---|---|
| `UnitySessionAttributes.cs`（新規） | 80 行 | 84 行 | 予想内 |
| `TelemetrySessionAttributesStore.cs`（新規） | 90 行 | 74 行 | 予想内 |
| `TelemetrySessionAttributes.cs`（新規） | 45 行 | 14 行 | 予想内 |
| `ElasticTelemetryIndexTemplateDefinition.cs` | 181 行 | +5 のみ | 予想内 |
| `AbstractApplicationInitializer.cs` | +1 行 | +2 行（`using` 1 行含む） | 実質遵守 |

責務配置は §3.4 の設計判断どおり。`UnitySessionCorrelationContext` にも `CapabilityStateStore` にも相乗りしていない。
**新規ロジックのうち単体テストが書けないものは無い**（store は純粋、mapper は静的関数、Unity 側は `Capture()` の有無だけ）。

**このスライスが作った構造負債: なし。**

### 8.2 機能レビュー

#### 合格した項目（根拠つき）

| 項目 | 根拠 |
|---|---|
| **§1.2 の核心（誤付与しない）** | 3 経路とも `store.TryGet(envelope.SessionId)`。「現在接続中セッション」は使っていない |
| **§1.2 の前提が実際に成立する**（巡目 1 は未確認だった） | `DebugSocketClientSession.SessionId = UnitySessionCorrelationContext.SessionId`（`Transport/DebugSocketClientSession.cs:54`）で、この値が `CreateCapabilityWelcomeFrame(session.SessionId, …)`（`Protocol/DebugSocketInboundMessageRouter.cs:150`）に渡る。telemetry 側も `AppTelemetry.cs:305/389` で同じ `UnitySessionCorrelationContext.SessionId` を使う。**したがって Welcome の SessionId と telemetry record の SessionId は同一値になり、TryGet が実際に当たる** |
| **§1.3 欠測はキーごと省略** | mapper は `NullIfEmpty` で個別判定。file 経路は `WhenWritingNull`、`_bulk` 経路は `AddIfPresent` |
| **§4.5.1（P1-7）** | `CreatePayloadDictionary` に 5 キー。T10 / T11 が両方向を固定 |
| **§4.5.2（P3 中止）** | index template から削除ゼロ |
| **wire 互換（既存 id）** | YAML の 9〜13 は末尾追加、0〜8 不変。生成物 2 つとも `[Key(9..13)]` |
| **wire 互換（旧 Unity → 新 DebugStudio）** | 既存テスト `LogEnvelope_旧keyのみpayloadは相関fieldがdefaultになる` が示すとおり、欠測 key はプロパティ初期化子（`string.Empty`）のまま残る。加えて `ApplyWelcome` が `?? string.Empty` で受けているため **null 逆流も起きない**。§1.3 によりキーが出ないだけで例外は投げない |
| **Unity メインスレッド制約** | `Application.*` / `SystemInfo.*` の読み取りは `Capture()` 内のみ。ソケット受信経路（`DebugSocketService.Inbound.cs`）から `using UnityEngine;` が消えている |
| **偽 null チェック** | 新規 Unity コードに `UnityEngine.Object` を扱う箇所が無く、`?.` / `??` / `is null` / `ReferenceEquals` の該当なし |
| **`.meta`** | 2 ファイルとも既存と同じ最小書式。GUID は repo 内で一意（衝突 0 件を確認） |
| **`internal ResetForTests()` が Tests から見える** | `Scripts/Foundation/AssemblyInfo.cs:5` に `InternalsVisibleTo("OneStarMaker.Tests")` |
| **mapper の呼び出し漏れ** | `ToExportRecord(` の全 3 production 呼び出しが属性を渡している（grep 済み）。store は `AppCompositionRoot.cs:114` の 1 インスタンスを 4 経路で共有 |

#### R1【BLOCK → 修正済み】Unity コンパイル不能

§7 巡目 2 参照。`record struct` → plain `readonly struct` へ置換して解消。

#### R2【非ブロッキング → 修正済み】旧 Welcome payload に対する回帰テストが無い

§6 は「古い Unity ↔ 新 DebugStudio で例外を投げてはいけない」を要件に挙げているが、**それを固定するテストが 1 本も無い。**
同じ状況に対する前例テストが `tests/DebugStudio.Contracts.Tests/CorrelationProtocolRoundtripTests.cs:90`
（`LogEnvelope_旧keyのみpayloadは相関fieldがdefaultになる`）に既にあるので、Welcome 版を同じ形で書けば足りる。

上記のとおり現状の挙動は正しいので**ブロックしない**が、機械的な保証が無い状態で §6 の要件を「満たしている」と言っているのは
レビュアの目視に依存しており、CLAUDE.md の「テスト要求は構造の指示より強く効く」に反する。

**対処（Phase B 差し戻し、production コード非変更）:** テスト 2 本を追加。

- `CorrelationProtocolRoundtripTests.WelcomeEnvelope_旧keyのみpayloadはセッション属性fieldがdefaultになる`
  — `[Key(0)]`〜`[Key(8)]` だけの Legacy 型を serialize → 現行型へ deserialize。既存 8 フィールドの復元と、新 5 フィールドが `string.Empty` になることを固定
- `TelemetrySessionAttributesStoreTests.ApplyWelcome_旧Welcome相当の空属性でも例外にならず5値は空になる`

**新 5 フィールドの欠測時の既定値は実測で `string.Empty`**（プロパティ初期化子が残る。`null` にはならない）。
推測ではなく実行で確認済み。

### 8.2b 実行検証の実測（Phase C 巡目 2 が自分で実行した）

| コマンド | 結果 |
|---|---|
| `pwsh tools/protocol-codegen/generate.ps1 --check` | exit 0 |
| `dotnet test tools/DebugStudio/DebugStudio.sln` | **342 合格 / 失敗 0**（Export 63 / Server 10 / Contracts 35 / Cli 7 / App 227） |
| `pwsh tools/run-tests.ps1`（R1 修正**前**） | **exit 1 / テスト 0 件 / 結果 XML なし** — §7 の R1 |
| `pwsh tools/run-tests.ps1`（R1 修正**後**） | **exit 0 / 447 合格 / 失敗 0** |
| **P1-8（§4.6 改名）後に全て再実行** | `--check` exit 0（53 generated files match YAML）/ `dotnet test` **344 合格 0 失敗** / `run-tests.ps1` **exit 0 / 447 合格 0 失敗** |

P1-8 の改名漏れは grep で確認済み: `tools/DebugStudio` と `protocol` に `unityVersion` / `UnityVersion` のヒット **0 件**。
`unity/` 側の `UnitySessionAttributes.UnityVersion` は §4.6 の設計判断どおり意図的に残っている（wire 境界で `EngineVersion` に変換）。
field id 0〜13 はすべて不変（13 の name だけ変更）。

Unity は同日ベースライン（`TestResults/results-all-20260808-100948.xml`）が 446 件で、**+1 = T9**。
結果 XML に `OneStarMaker.Tests.Foundation.UnitySessionAttributesTests.Capture前に読んでも例外にならず空文字が返る` の passed を確認済み。既存テストの回帰はゼロ。

### 8.3 確認していないこと（重要）

| 項目 | 状態 |
|---|---|
| **§5.3 の実地確認（docker + Elastic + 実機 Unity 接続 + curl）** | **未実施。** Elastic を立てて実際に 5 フィールドが入ることは誰も確認していない。コード上の経路（mapper → record → `_bulk` Dictionary → index template）は追跡済みだが、**実際に Kibana で見えるところまでは未確認** |
| 実機（Android / iOS）での `SystemInfo.deviceModel` の値 | 未確認。Editor 実行しか通していない |
| 32 session 上限を実際に超えたときの挙動 | 単体テストのみ。実運用では未確認 |
| DebugStudio を server 側にする inversion 経路での handshake 方向 | **未確認。** `DebugStudioServerSessionTransport` も `CapabilityWelcomeReceived` を上げる構造なので同じ router を通るはずだが、その経路で Unity 側が Welcome を送るのかは追っていない |
| **telemetry と Welcome の到着順** | **§8 では見落としていた。C' 監査 A1 が検出し、Phase C が実コードで裏取りした。§9.2 参照** |

> **§8.2 の「合格」判定への訂正:** 上表最終行のとおり、rolling file 永続経路については
> 「全 record に属性が付く」とは言えない（§9.2）。**コードとテストは正しいが、設計文 §1.1 の記述が実態より強い。**

---

## 9. Phase C' 監査

<!-- 実装にも設計にも関与していないモデルが記入 -->

担い手: Grok 4.5（cursor-agent `--plan --trust`）。

> **プロセス上の逸脱（申し送り）:** CLAUDE.md は「C' 監査は実装にも設計にも関与していないモデル。実装が Grok なら監査は Grok 以外」と定めているが、
> 今回は人間の指示により**実装と同じ Grok 4.5** が監査を担当した。**自己採点である。**
> 実際に自分の実装に対する指摘（A1）を出してはいるが、この結果を「独立監査を通った」とは扱えない。
> 中立モデル（Composer 2.5 / GPT-5.6 Sol など）での再監査は未実施。

### 9.1 判定

§8 の事実主張に誤りは見つからなかった。ただし **§8 が見落としていた実害のある欠陥を 1 件検出（A1）。**

### 9.2 A1【非ブロッキング / 要申し送り】handshake 前 telemetry は rolling file に属性なしで焼き付く

**Phase C（Opus 5）が実コードで裏取りして事実と確認した。**

Unity 側の telemetry 送出は handshake 完了を待たない:

```
unity/.../Runtime/DebugSocketServices/DebugSocketService.cs:321
public void EnqueueTelemetry(in TelemetryRecord record)
{
    if (!Options.SendTelemetry) { return; }   // ← gate はこれだけ
    EnqueueOutgoingMessage(...);
}
```

`HasCompletedCapabilityHello` は inbound の DebugCommand / InspectorQuery しか塞いでいない
（`Protocol/DebugSocketInboundMessageRouter.cs:201` と `:244`）。
したがって **接続直後〜Hello 受信までの窓で telemetry フレームが先に飛ぶ。**

DebugStudio 側で影響が出るのは 3 経路のうち **rolling file 永続だけ**:

| 経路 | mapping のタイミング | 影響 |
|---|---|---|
| `TelemetryPersistenceService` | **受信時に 1 回だけ**（`OnTelemetryReceived` → `_writer.Enqueue`） | **Welcome より前に着いた record は属性なしで書かれ、後から Welcome が来ても遡って埋められない** |
| `TelemetryExportService` | export 実行時に retained snapshot を再 map | 影響なし（Welcome 到着後に押せば付く） |
| `ElasticTelemetryPushService` | push 実行時に再 map | 影響なし |

§1.1 は「DebugStudio: Welcome を受けて sessionId ごとに保持 → **export/永続時**に全 record へ付与」と書いているが、
**永続経路は「付与時に全 record を見直す」構造になっていない。**これは設計文と実装の乖離であり、このスライスが作ったものではなく
`TelemetryPersistenceService` の既存構造（受信時 1 回書き）に由来する。

**なぜ非ブロッキングか:** 窓は接続直後の数フレームで、開発ツールの rolling file の先頭数件が欠測するだけ。
§5.3 が検証する Elastic Push 経路は正しく付く。**ただし「Kibana で全件にビルド情報が付く」とは言えない**ことは申し送る。

**対処は別スライス。** 選択肢は (a) Unity 側で negotiation 完了まで telemetry を送らない、
(b) DebugStudio 側で Welcome 前の record を保留して Welcome 後に flush、(c) 仕様として受け入れて文書化。

### 9.3 A2【非ブロッキング】ServiceStatus record には属性が付かない

`TelemetryExportService.CreateServiceStatusRecord` は mapper を通らないため 5 フィールドが付かない。
同じ index（`debugstudio-telemetry-*` ではなく `debugstudio-service-status-*`）へ行くので実害は小さいが、
HANDOFF が明示的に対象外と書いていなかった。**仕様として対象外**でよい。

### 9.4 監査が §8 を妥当と認めた点

- §8.2 の「§1.2 の前提が実際に成立する」論証（`DebugSocketClientSession.SessionId` → Welcome / `AppTelemetry` → record の同一値）は正しい
- §8.3 の「確認していないこと」は正直で、§5.3 未実施を受入条件の穴として明示できている
- `AddIfPresent` が HANDOFF §4.5.1 の素朴案より正しいという §8 の評価は妥当
- eviction が FIFO（厳密な LRU でない）ことは HANDOFF が許容している範囲

### 9.5 監査からのプロセス指摘

巡目 1（Opus 4.8）は「差し戻しなし」と判定したが、巡目 2 で BLOCK が出た。
§6 の `record` 禁止ルールは巡目 2 で追記されたので巡目 1 は照合できなかったが、
**`unity/Assets/OneStarMaker/Scripts` 配下に `record` 型宣言が 1 つも無いことは巡目 1 でも grep できた。**
「既存コードベースに前例が無い構文が新規に入った」は、ルールが明文化されていなくても静的レビューで拾える。
