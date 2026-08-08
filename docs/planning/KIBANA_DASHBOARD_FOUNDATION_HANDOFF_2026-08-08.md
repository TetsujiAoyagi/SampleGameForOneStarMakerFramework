# Kibana saved objects の正本ファイル化 + 構造検算 ハンドオフ (2026-08-08)

| | |
|---|---|
| スライス | K1（正本ファイル化）/ K2（構造検算）/ K4（周辺の穴埋め） |
| ブランチ | `feature/kibana-dashboard-foundation`（`feature/telemetry-session-attributes` マージ後に切る） |
| Phase | A 完了 / B 未着手 |
| 後続 | **K3（ダッシュボード本体の作り込み）は別 HANDOFF。** §1.4 参照 |
| 前提 | `feature/telemetry-session-attributes`（`buildVersion` / `platform` / `deviceModel` / `osVersion` / `engineVersion` / `sessionId`）がマージ済みであること |

このドキュメントは自己完結で書いてある。**他のドキュメントを読む必要はない。**
必要な既存コードの内容はすべて本文に転記してある。

---

## 0. 1分で把握

Kibana に import するダッシュボード `DebugStudio Overview` は**パネルが 0 枚**で出力されている。

現在の唯一の正本は `tools/DebugStudio/src/DebugStudio.Export/Elastic/ElasticKibanaSavedObjectsWriter.cs`（118 行）で、C# の匿名型から saved object を組み立てている。そこにこう書いてある（原文ママ）:

```csharp
optionsJSON = "{\"useMargins\":true,\"syncColors\":false}",
panelsJSON = "[]",
```

`references` には `panel_0` / `panel_1` が宣言されているが、`panelsJSON` が空配列なので**参照先は宙に浮いていて何も描画されない**。

さらに saved search 2 本が saved object として不完全になっている。`references` の name は `kibanaSavedObjectMeta.searchSourceJSON.index` なのに、`attributes` 側に受け皿となる `kibanaSavedObjectMeta` が存在しない:

```csharp
attributes = new
{
    title,
    columns = Array.Empty<string>(),
    sort = "[[\"@timestamp\",\"desc\"]]",
},
references = new object[]
{
    new
    {
        id = dataViewId,
        name = "kibanaSavedObjectMeta.searchSourceJSON.index",
        type = "index-pattern",
    }
}
```

Kibana は import 時にこの `searchSourceJSON` の中の `indexRefName` へ参照を差し戻す。器が無ければ data view に結び付かない。加えて `sort` は配列であるべきところが文字列になっている。

そして**既存テストがこれを承認している**。`tools/DebugStudio/tests/DebugStudio.Export.Tests/Elastic/ElasticArtifactWriterTests.cs:245` の
`KibanaSavedObjectsはOverviewDashboardとSavedSearchを出力する` が見ているのは `type` / `title` / `references.Length >= 2` だけで、`panelsJSON` を一切見ていない。

やること:

1. **K1** — saved objects の正本を C# の文字列組み立てから**リポジトリに commit した NDJSON ファイル**へ移す。writer は埋め込みリソースを吐くだけにする
2. **K2** — NDJSON の構造を検算する純関数（parser / validator）を作り、**「パネル 0 枚」「宙に浮いた参照」を機械的に赤にする**
3. **K4** — 周辺の穴（`status` の mapping 欠落、README の導線切れ）を塞ぐ

やらないこと（§1.4 に理由）:

- **ダッシュボードのパネルを Lens で作り込むこと**（K3。次スライス）
- `debugstudio-service-status-*` の data view 追加
- log ストリームへの `buildVersion` 付与
- ECS 準拠

---

## 1. 確定方針（設計判断。実装側で変更しない）

### 1.1 saved objects の正本は「C# のコード」ではなく「commit した NDJSON ファイル」にする

**これが本スライスの中心。**

Kibana のダッシュボードは `attributes.panelsJSON` という**巨大な JSON 文字列**にパネル定義を丸ごと持つ。Lens パネルを 1 枚足すだけで数十行の入れ子 JSON が増える。これを C# の匿名型と `\"` エスケープで組み立てると:

- **レビューできない。** `git diff` に出るのはエスケープされた 1 行の文字列で、何が変わったか読めない
- **Kibana のバージョン更新で静かに壊れる。** 追随するとき差分の当てどころが無い
- **Kibana の UI で作ったものを取り込めない。** 実際にダッシュボードを組む作業は Kibana 上で行うのに、成果物を C# へ手写しすることになる

順序を逆にする:

```
Kibana UI で組む → Saved Objects の _export で NDJSON を得る
  → リポジトリへ commit（これが正本）
  → C# は埋め込みリソースとして吐き出すだけ
  → 構造の妥当性はテストが検算する（§1.3）
```

正本の置き場は **`tools/DebugStudio/elastic/kibana/debugstudio-overview.ndjson`**。
`tools/DebugStudio/elastic/` は既に `docker-compose.yml` / `filebeat/filebeat.yml` / `README.md` を持つ「Elastic 運用資材の置き場」であり、そこへ足すのが素直。

### 1.2 本スライスの seed は Lens を使わない。saved search パネル 2 枚で組む

**Phase B の実装者は Kibana を起動してダッシュボードを手で組める前提を置かない。**

Lens パネルの `attributes.state` は Kibana のバージョンに強く依存し、手書きすると必ず間違える。一方、**saved search をパネルとして貼る形式は小さく、手書きできる**。`panelsJSON` の 1 要素はこれだけで済む:

```json
{"type":"search","panelIndex":"p1","gridData":{"x":0,"y":0,"w":24,"h":15,"i":"p1"},"embeddableConfig":{},"panelRefName":"panel_p1"}
```

したがって本スライスの seed は:

- data view 2 本（telemetry / log）— 既存を修正して踏襲
- saved search 2 本 — **`kibanaSavedObjectMeta` を正しく持たせ、`columns` と query に実際の意味を入れる**
- dashboard 1 本 — **上記 2 本を panelsJSON に貼る**

これで「import すると 2 枚のパネルが実際に描画されるダッシュボード」が最小コストで手に入り、検算テスト（§1.3）が緑になる実体が揃う。Lens による集計パネルは K3（次スライス）で Kibana UI から `_export` して**このファイルを差し替える**。そのときの安全網が K2 の検算になる。

### 1.3 検算はテストで強制する。writer では検証しない

**A-4 の適用。** 「パネルが 0 枚のダッシュボードを出力していた」を二度と起こさないための唯一の機械的な保証は、**NDJSON を読んで構造を検算する純関数とそのテスト**である。

検証を **writer 実行時ではなくテストに置く**理由:

- 正本は commit 済みのファイル。壊れているなら **CI で止まるべき**であって、operator が artifact を生成する時点で例外を投げても遅い
- writer に検証を入れると、writer が「コピー」と「検証」の 2 責務になる

ただし writer は**埋め込みリソースが見つからないときは例外を投げる**。黙って空ファイルを吐いてはいけない（現状の失敗モードの再発）。

### 1.4 ダッシュボードの「中身」は本スライスに含めない

Kibana の Lens パネル（`buildVersion` 別の p95 バー、`payload.fps` の p05、GC スパイク発生率 …）を組むには、**実際に Elastic にデータが入った状態で Kibana UI を操作する**必要がある。これは:

- Docker + Elastic + Kibana + 実機 Unity 接続が要る
- 「何を見れば分かるか」を試行錯誤する作業であり、HANDOFF に手順として書き下せない

前スライス（`TELEMETRY_SESSION_ATTRIBUTES_HANDOFF_2026-08-08.md` §8.3）の申し送りに **「Elastic を立てて実際に 5 フィールドが入ることは誰も確認していない」** とある。**その確認が済んでいないうちにパネルの作り込みへ進んではいけない。**

本スライスは**器と安全網**だけを作る。中身は K3 で人間が Kibana 上で作り、`_export` した NDJSON でファイルを差し替える。

### 1.5 ダッシュボードから deprecated なフラット欄を参照しない

`unity/Assets/Docs/Architecture/28-telemetry-contract-v3.md` の §4（原文の要旨）:

> 1. envelope に `kind` + `payload` を追加（SchemaVersion=3）
> 2. **旧フラット欄（CpuTime / ManagedMem 等）は deprecated 併記**
> 3. 消費者は **payload / kind を正**とする
> 4. 旧欄削除は TC-09（併記期間終了時）

つまり telemetry ドキュメントには今、**同じ値が 2 箇所に載っている**:

| 正（v3） | deprecated（TC-09 で削除予定） |
|---|---|
| `payload.cpuMs` | `cpuTime` |
| `payload.gpuMs` | `gpuTime` |
| `payload.managedBytes` | `managedMem` |
| `payload.nativeBytes` | `nativeMem` |
| `payload.cameraTotalViewCount` | `cameraTotalViewCount`（トップレベル） |
| `payload.cameraAdditionalViewCount` | `cameraAdditionalViewCount`（同） |
| `payload.cameraBlendingViewCount` | `cameraBlendingViewCount`（同） |
| `payload.cameraMaxStackDepthTotal` | `cameraMaxStackDepthTotal`（同） |

**ダッシュボードが deprecated 側を参照すると、それが TC-09 で削除できない理由になる。**
saved search の `columns` / `sort` には v3 側（`kind` / `name` / `payload.*`）だけを書く。これを V6 で検算する（§4 K2-2）。

> **注意（実装者向け）:** `cameraViewId` と `cameraActiveCameraHash` には `payload` 側の対応が無い。**この 2 つは deprecated ではない。** 上の表の 8 つだけが対象。

---

## 2. 出力する saved objects（確定）

import 後に Kibana でこう見える、という確定仕様。

| # | type | id | title | 中身 |
|---|---|---|---|---|
| 1 | `index-pattern` | `debugstudio-telemetry-dataview` | `debugstudio-telemetry-*` | timeField=`@timestamp` |
| 2 | `index-pattern` | `debugstudio-log-dataview` | `debugstudio-log-*` | timeField=`@timestamp` |
| 3 | `search` | `debugstudio-telemetry-timeline` | `DebugStudio Telemetry Timeline` | columns に `kind` / `name` / `elapsedMs` / `buildVersion` / `sessionId`。query 空 |
| 4 | `search` | `debugstudio-log-warnings` | `DebugStudio Log Warnings` | **query に実際の絞り込みを入れる**（下記） |
| 5 | `dashboard` | `debugstudio-overview-dashboard` | `DebugStudio Overview` | **#3 と #4 をパネルとして貼る** |

`#4` の query（KQL）:

```
log.level: ("warning" or "error" or "critical")
```

`log.level` の実際の値は `tools/DebugStudio/src/DebugStudio.App/Core/Services/LogRecordExportMapper.cs:40` で決まっており、**すべて小文字**である（原文ママ）:

```csharp
LogLevel = log.Kind switch
{
    LogEntryKind.Trace => "trace",
    LogEntryKind.Debug => "debug",
    LogEntryKind.Information => "info",
    LogEntryKind.Warning => "warning",
    LogEntryKind.Error => "error",
    LogEntryKind.Critical => "critical",
    LogEntryKind.None => "none",
    _ => "unknown",
},
```

`"Warning"` と大文字で書くと 0 件になる。**この 3 語は小文字で書くこと。**

`debugstudio-service-status-*` の data view は **出さない**（設計判断）。理由: あれは DebugStudio 自身の稼働状態であってゲームの性能ではない。Overview に混ぜると「何を見るダッシュボードか」がぼやける。必要になった時点で別 data view / 別ダッシュボードにする。

---

## 3. 変更対象ファイル一覧（A-1: 規模見積もり）

「現在行数 → 予想行数 / 責務数」。**予想を超えそうになったら実装を止めて §7 に書くこと。**

### 3.1 正本ファイル（新規）

| ファイル | 行数 | 責務 |
|---|---|---|
| `tools/DebugStudio/elastic/kibana/debugstudio-overview.ndjson` | **新規 0 → 5 行 / 1** | saved objects の正本。1 行 1 オブジェクト。**行は長い（数百文字）が改行してはならない** |

### 3.2 DebugStudio.Export 側

| ファイル | 行数 | 責務 |
|---|---|---|
| `src/DebugStudio.Export/DebugStudio.Export.csproj` | 11 → 17 / 変化なし | `EmbeddedResource` 1 件追加 |
| `src/DebugStudio.Export/Elastic/ElasticKibanaSavedObjectsWriter.cs` | 118 → **55** / 1 | **組み立てをやめ、埋め込みリソースを書き出すだけにする** |
| `src/DebugStudio.Export/Elastic/Kibana/KibanaSavedObject.cs` | **新規 0 → 45 / 1** | NDJSON 1 行を表す値オブジェクト |
| `src/DebugStudio.Export/Elastic/Kibana/KibanaSavedObjectBundle.cs` | **新規 0 → 40 / 1** | 全行の集合 + id 引き |
| `src/DebugStudio.Export/Elastic/Kibana/KibanaSavedObjectBundleParser.cs` | **新規 0 → 75 / 1** | NDJSON 文字列 → bundle（**IO 無しの純関数**） |
| `src/DebugStudio.Export/Elastic/Kibana/KibanaSavedObjectBundleValidator.cs` | **新規 0 → 160 / 1** | V1〜V6 の検算（**IO 無しの純関数**） |
| `src/DebugStudio.Export/Elastic/Kibana/KibanaSavedObjectValidationIssue.cs` | **新規 0 → 30 / 1** | 指摘 1 件の値オブジェクト |
| `src/DebugStudio.Export/Elastic/ElasticTelemetryIndexTemplateDefinition.cs` | 181 → 182 / 1 | `status` に keyword mapping を 1 行追加（K4-1） |

### 3.3 テスト側

| ファイル | 行数 | 責務 |
|---|---|---|
| `tests/DebugStudio.Export.Tests/Elastic/Kibana/KibanaSavedObjectBundleParserTests.cs` | **新規 0 → 90 / 1** | parser の単体テスト |
| `tests/DebugStudio.Export.Tests/Elastic/Kibana/KibanaSavedObjectBundleValidatorTests.cs` | **新規 0 → 220 / 1** | validator の単体テスト（**本スライスの中心**） |
| `tests/DebugStudio.Export.Tests/Elastic/Kibana/KibanaOverviewBundleTests.cs` | **新規 0 → 115 / 1** | **実際に出力される正本が V1〜V6 を全て満たすこと**（T10〜T12b） |
| `tests/DebugStudio.Export.Tests/Elastic/ElasticArtifactWriterTests.cs` | 現在 → +25 / 変化なし | 既存テストの縮小と T13 / T15 / T16 の追加（下記） |

> `ElasticArtifactWriterTests.cs:245` の `KibanaSavedObjectsはOverviewDashboardとSavedSearchを出力する` は
> `lines[0]`〜`lines[4]` の**行順序に依存**している。正本ファイル化で行順序を維持すればそのまま通るが、
> **`panelsJSON` を検証していないという欠陥は残る**。このテストは `KibanaOverviewBundleTests` に役割を譲るので、
> 行順序依存の assert を削り、「5 行出る」「file が空でない」だけに縮める。**削除はしない**（writer の IO 経路の回帰止めとして残す）。

### 3.4 ドキュメント

| ファイル | 変更 |
|---|---|
| `tools/DebugStudio/elastic/README.md` | §2 に `import-kibana.ps1` の実行を追加（K4-2） |
| `unity/Assets/Docs/Architecture/15-telemetry-v2.md` | Phase 3 の行と §8 を現況に合わせる（K4-3） |

### 3.5 新責務の配置（A-3: これは設計判断としてこう決めた）

| 新責務 | 置き場 | なぜそこか |
|---|---|---|
| NDJSON の parse | **新規 `Elastic/Kibana/KibanaSavedObjectBundleParser.cs`** | `ElasticKibanaSavedObjectsWriter` に相乗りさせない。あれは「ファイルを書く」責務であって「読んで解釈する」責務ではない。混ぜると writer が IO とパースの 2 責務になり、**パースのテストにファイル IO が必要になる**（＝テストが書けなくなる） |
| 構造の検算 | **新規 `Elastic/Kibana/KibanaSavedObjectBundleValidator.cs`** | parser と分ける。parser は「読めるか」、validator は「意味が通るか」。混ぜると V1〜V6 を個別にテストできない |
| Kibana 関連の置き場 | **新規サブフォルダ `Elastic/Kibana/`** | `Elastic/` は既に 22 ファイルある。Kibana saved object は Elasticsearch の template / pipeline とは別の関心事なので、サブフォルダで分ける |

**本スライスが 500 行 / 3 責務を超えさせるファイルは無い。** 最大は validator の 160 行（1 責務）。

---

## 4. 施工チケット（施行表）

| # | チケット | 依存 | 規模 | 単体テスト |
|---|---|---|---|---|
| K1-1 | 正本 NDJSON を新規作成する | — | +5 行 | K2-3 の T7〜T12 で検証 |
| K1-2 | csproj に `EmbeddedResource` を足す | K1-1 | +6 行 | — |
| K1-3 | writer を「埋め込みリソースを書き出すだけ」に置換する | K1-2 | 118 → 55 行 | T13 |
| K2-1 | `KibanaSavedObject` / `Bundle` / `Parser` | — | 新規 160 行 | T1〜T3 |
| K2-2 | `KibanaSavedObjectBundleValidator`（V1〜V6） | K2-1 | 新規 190 行 | T4〜T6 |
| K2-3 | **正本が V1〜V6 を満たすことのテスト** | K1-1, K2-2 | 新規 115 行 | T10〜T12b |
| K2-4 | 既存 `ElasticArtifactWriterTests` の縮小 + **writer と正本を繋ぐテスト** | K1-3 | +25 行 | T13 / T15 / T16 |
| K4-1 | index template に `status` の keyword mapping | — | +1 行 | T14 |
| K4-2 | README §2 に `import-kibana.ps1` を追加 | — | +8 行 | — |
| K4-3 | `15-telemetry-v2.md` の Phase 3 記述を現況に合わせる | K1-3 | +5 行 | — |

**着手順**: K2-1 → K2-2 → K1-1 → K1-2 → K1-3 → K2-3 → K2-4 → K4-*。
**理由**: validator を先に書くと、K1-1 で NDJSON を書いたときに**その場で検算できる**。逆順にすると「たぶん正しい JSON」を先に commit してしまう。

---

### K1-1 正本 NDJSON を新規作成する

**ファイル:** `tools/DebugStudio/elastic/kibana/debugstudio-overview.ndjson`

**NDJSON は 1 行 1 オブジェクト。** 下記は読みやすさのため整形してあるが、**実ファイルでは各オブジェクトを 1 行に潰すこと**。順序も下記のとおり（data view → search → dashboard。参照先が先に来る）。

**1 行目 — telemetry data view**

```json
{"id":"debugstudio-telemetry-dataview","type":"index-pattern","attributes":{"title":"debugstudio-telemetry-*","timeFieldName":"@timestamp"},"references":[]}
```

**2 行目 — log data view**

```json
{"id":"debugstudio-log-dataview","type":"index-pattern","attributes":{"title":"debugstudio-log-*","timeFieldName":"@timestamp"},"references":[]}
```

**3 行目 — telemetry saved search**（整形表示。実ファイルは 1 行）

```json
{
  "id": "debugstudio-telemetry-timeline",
  "type": "search",
  "attributes": {
    "title": "DebugStudio Telemetry Timeline",
    "description": "span / sample / event の生ドキュメントを新しい順に見る。",
    "columns": ["kind", "name", "elapsedMs", "payload.stage", "buildVersion", "sessionId"],
    "sort": [["@timestamp", "desc"]],
    "kibanaSavedObjectMeta": {
      "searchSourceJSON": "{\"query\":{\"query\":\"\",\"language\":\"kuery\"},\"filter\":[],\"indexRefName\":\"kibanaSavedObjectMeta.searchSourceJSON.index\"}"
    }
  },
  "references": [
    { "id": "debugstudio-telemetry-dataview", "name": "kibanaSavedObjectMeta.searchSourceJSON.index", "type": "index-pattern" }
  ]
}
```

**4 行目 — log saved search**（整形表示。実ファイルは 1 行）

```json
{
  "id": "debugstudio-log-warnings",
  "type": "search",
  "attributes": {
    "title": "DebugStudio Log Warnings",
    "description": "warning 以上のログのみ。telemetry の異常と突き合わせる用。",
    "columns": ["log.level", "category", "message", "sessionId"],
    "sort": [["@timestamp", "desc"]],
    "kibanaSavedObjectMeta": {
      "searchSourceJSON": "{\"query\":{\"query\":\"log.level: (\\\"warning\\\" or \\\"error\\\" or \\\"critical\\\")\",\"language\":\"kuery\"},\"filter\":[],\"indexRefName\":\"kibanaSavedObjectMeta.searchSourceJSON.index\"}"
    }
  },
  "references": [
    { "id": "debugstudio-log-dataview", "name": "kibanaSavedObjectMeta.searchSourceJSON.index", "type": "index-pattern" }
  ]
}
```

> **エスケープ地獄に注意。** `searchSourceJSON` は「JSON 文字列の中に JSON が入っている」二重構造で、その中の KQL に `"` が含まれる。
> **手で数えて書かないこと。** 内側の JSON をまず単体で書き、`JsonSerializer.Serialize(innerJsonString)` に相当する変換を通して外側へ埋めるか、
> エディタの JSON 文字列エスケープ機能を使う。**書いたら必ず K2-3 のテストで parse を通して確認する**（T10 がこれを検出する）。

**5 行目 — dashboard**（整形表示。実ファイルは 1 行）

```json
{
  "id": "debugstudio-overview-dashboard",
  "type": "dashboard",
  "attributes": {
    "title": "DebugStudio Overview",
    "description": "Telemetry と log の突き合わせ用 overview。パネルの作り込みは後続スライス。",
    "optionsJSON": "{\"useMargins\":true,\"syncColors\":false,\"hidePanelTitles\":false}",
    "timeRestore": false,
    "kibanaSavedObjectMeta": {
      "searchSourceJSON": "{\"query\":{\"query\":\"\",\"language\":\"kuery\"},\"filter\":[]}"
    },
    "panelsJSON": "[{\"type\":\"search\",\"panelIndex\":\"p1\",\"gridData\":{\"x\":0,\"y\":0,\"w\":24,\"h\":15,\"i\":\"p1\"},\"embeddableConfig\":{},\"panelRefName\":\"panel_p1\"},{\"type\":\"search\",\"panelIndex\":\"p2\",\"gridData\":{\"x\":24,\"y\":0,\"w\":24,\"h\":15,\"i\":\"p2\"},\"embeddableConfig\":{},\"panelRefName\":\"panel_p2\"}]"
  },
  "references": [
    { "id": "debugstudio-telemetry-timeline", "name": "panel_p1", "type": "search" },
    { "id": "debugstudio-log-warnings", "name": "panel_p2", "type": "search" }
  ]
}
```

**確定事項:**

- `panelRefName` の値は `references[].name` と**完全一致**させる。Kibana の慣習は `panel_<panelIndex>`
- `gridData` の `i` は `panelIndex` と一致させる。グリッド幅は 48 なので `w:24` を横 2 枚
- `timeRestore: false` — 時間範囲はグローバルの time picker に従わせる。ビルド間比較では広い範囲を取りたいので、ダッシュボードに時間を焼き付けない
- **`migrationVersion` / `coreMigrationVersion` / `typeMigrationVersion` は付けない。** 誤った版番号を書くと Kibana の migration が誤適用される。現行 writer も付けていない。**付けない場合の Kibana 8.17 の挙動は §5.3 の実地確認で確かめること**
- **`_export` が末尾に付ける `{"exportedCount":N,...}` のサマリ行は入れない。** これは export の副産物であって saved object ではなく、V1（全行が `type` と `id` を持つ）に違反する

---

### K1-1b 追補（Phase B 直前に Phase A が追加）: `.gitattributes` に `*.ndjson` の eol を固定する

このリポジトリは **`core.autocrlf=true`** で、`.gitattributes` に `*.ndjson` の規則が**無い**（リポジトリ内に既存の `.ndjson` は 1 件も無く、前例も無い）。
このまま commit すると、**checkout 時に各行末が CRLF に変換される**。埋め込みリソースはディスク上のファイルをそのまま焼き込むので、
**行末に `\r` が付いた NDJSON が Kibana へ渡り、かつ OS によって成果物のバイト列が変わる**。

`.gitattributes` の末尾（`# ETC` 以降の適当な位置でよい）に 1 行足すこと:

```
*.ndjson                text eol=lf
```

**この 1 行を、NDJSON ファイルを作る前に足すこと。** 後から足しても既に checkout 済みのファイルは変換されない。

作成後に `git ls-files --eol tools/DebugStudio/elastic/kibana/debugstudio-overview.ndjson` で
`i/lf    w/lf` になっていることを確認する。`w/crlf` なら失敗している。

---

### K1-2 csproj に `EmbeddedResource` を足す

**ファイル:** `tools/DebugStudio/src/DebugStudio.Export/DebugStudio.Export.csproj`

現在の全文（原文ママ）:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

</Project>
```

`PropertyGroup` の後に追加する:

```xml
  <ItemGroup>
    <EmbeddedResource Include="..\..\elastic\kibana\debugstudio-overview.ndjson"
                      LogicalName="DebugStudio.Export.Elastic.Kibana.debugstudio-overview.ndjson" />
  </ItemGroup>
```

**`LogicalName` を明示すること。** 明示しないと、プロジェクト外の相対パスを含む Include のリソース名は MSBuild の規約で決まり、**リンク方法によって変わる**。名前を固定しないと `GetManifestResourceStream` が実行時に `null` を返す（＝ K1-3 の失敗モードそのもの）。

**なぜ埋め込みリソースか（設計判断）:** `DebugStudio.ElasticArtifactGen` は任意のディレクトリから `dotnet run` され、WPF アプリはビルド成果物として配布される。exe の隣に置いた素のファイルに依存すると、どちらの経路でも配置ミスで壊れる。アセンブリに焼き込めば経路によらず必ず存在する。

---

### K1-3 writer を「埋め込みリソースを書き出すだけ」に置換する

**ファイル:** `tools/DebugStudio/src/DebugStudio.Export/Elastic/ElasticKibanaSavedObjectsWriter.cs`（118 → 55 行）

**`CreateDataView` / `CreateSearch` / `CreateDashboard` の 3 メソッドと `SerializerOptions` を全て削除する。**

置換後の骨子:

```csharp
public sealed class ElasticKibanaSavedObjectsWriter
{
    internal const string ResourceName = "DebugStudio.Export.Elastic.Kibana.debugstudio-overview.ndjson";

    /// <summary>
    /// 正本 NDJSON をそのまま読み出す。テストからも同じ内容を検算できるよう public にする。
    /// </summary>
    public static string ReadSavedObjectsNdjson()
    {
        using var stream = typeof(ElasticKibanaSavedObjectsWriter).Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded Kibana saved objects resource '{ResourceName}' was not found.");

        using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return reader.ReadToEnd();
    }

    public async Task WriteAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        // 引数検証とディレクトリ作成は現行のまま維持する
        ...
        await File.WriteAllTextAsync(
            outputPath,
            ReadSavedObjectsNdjson(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken).ConfigureAwait(false);
    }
}
```

**必須:**

- **BOM を付けない。** リポジトリ内の NDJSON 経路は `NdjsonTelemetryRecordSerializer` が `UTF8Encoding(encoderShouldEmitUTF8Identifier: false)` を使っており、その理由がコメントに明記されている（原文ママ）:
  > NDJSON 出力用 encoding。BOM を先頭へ埋め込むと行指向 consumer が 1 行目を JSON として parse できなくなるため、BOM なし UTF-8 に固定する。
  Kibana の import も同じく 1 行目を parse する。**BOM が付くと 1 行目だけ壊れる。**
- **リソースが見つからないときは例外を投げる**（§1.3）。`?? throw` を省略して `null` を握り潰さない
- `ReadSavedObjectsNdjson()` を `public static` にする。**K2-3 のテストがファイル IO 抜きで正本を検算できるようにするため**（テスト可能な配置の強制。A-4）

---

### K2-1 `KibanaSavedObject` / `KibanaSavedObjectBundle` / `KibanaSavedObjectBundleParser`

**すべて `DebugStudio.Export/Elastic/Kibana/` へ置く。IO を一切含めない。**

`KibanaSavedObject`（45 行）— NDJSON 1 行の最小 shape:

| メンバー | 型 | 備考 |
|---|---|---|
| `Id` | `string` | 空なら V1 違反 |
| `Type` | `string` | 空なら V1 違反 |
| `Attributes` | `JsonElement` | 生のまま持つ。型付けしない |
| `References` | `IReadOnlyList<KibanaSavedObjectReference>` | `Id` / `Name` / `Type` |
| `LineNumber` | `int` | **1 始まり。** 指摘メッセージに行番号を出すため |

`KibanaSavedObjectBundle`（40 行）— `IReadOnlyList<KibanaSavedObject> Objects` と `TryGetById(string id)`。

`KibanaSavedObjectBundleParser`（75 行）— `static KibanaSavedObjectBundle Parse(string ndjson)`:

- 空行は読み飛ばす（末尾改行を許容）
- **行が JSON として parse できない場合は例外ではなく、`Id`/`Type` が空の `KibanaSavedObject` として返すか、専用の「parse 失敗」を表現する。** 例外を投げると V1 の「壊れた行があることを指摘として列挙する」ができなくなる。**どちらにするかは実装者が決めてよいが、決めた理由を §7 に 1 行書くこと**
- `.NET` の `record` を使ってよい（DebugStudio は net8.0。Unity 側の制約は関係ない）

---

### K2-2 `KibanaSavedObjectBundleValidator`（V1〜V6）

`static IReadOnlyList<KibanaSavedObjectValidationIssue> Validate(KibanaSavedObjectBundle bundle)`。
**例外を投げず、指摘のリストを返す。** 0 件なら妥当。

| ID | ルール | なぜ必要か |
|---|---|---|
| **V1** | 全行が空でない `type` と `id` を持つ | `_export` のサマリ行の混入と壊れた行を検出 |
| **V2** | `id` が bundle 内で重複しない | 後勝ちで静かに消えるのを防ぐ |
| **V3** | **`type=dashboard` の `attributes.panelsJSON` が JSON 配列として parse でき、要素数 >= 1** | **今回の不具合そのもの。この 1 本が本スライスの存在理由** |
| **V4** | `panelsJSON` の各要素の `panelRefName` と、`references` のうち名前が `panel_` で始まるものが **1:1 で対応する**（どちら向きの余りも指摘） | 現行の「`panel_0` が宙に浮く」を検出 |
| **V5** | すべての `references[].id` が bundle 内に存在する | missing reference のまま import して壊れるのを防ぐ |
| **V6** | `type=search` の `attributes.columns` / `attributes.sort` / **`searchSourceJSON` の query 文字列**に **§1.5 の deprecated 8 フィールドが含まれない** | TC-09 でフラット欄を消したときにダッシュボードが道連れにならない |

**V6 の対象リスト（この 8 つだけ。完全一致で判定する）:**

```
cpuTime, gpuTime, managedMem, nativeMem,
cameraTotalViewCount, cameraAdditionalViewCount,
cameraBlendingViewCount, cameraMaxStackDepthTotal
```

**V6 の判定方法（2 種類ある。混ぜないこと）:**

| 対象 | 判定 |
|---|---|
| `columns` の各要素 / `sort` の各フィールド名 | **文字列の完全一致** |
| `searchSourceJSON` の中の query 文字列 | 下記の正規表現 |

> **V6 を「部分文字列検索」で実装してはいけない。**
> `payload.cameraTotalViewCount` は `cameraTotalViewCount` を部分文字列として含むため、**正しい v3 側の参照を誤検出する**。
> `\b`（word boundary）を使うのも**誤り**。`.` は非単語文字なので `\bcameraTotalViewCount\b` は `payload.cameraTotalViewCount` の中にマッチしてしまう。
>
> query 文字列に対しては、**直前がドットでも単語文字でもないこと**を明示する:
>
> ```
> (?<![.\w])(cpuTime|gpuTime|managedMem|nativeMem|cameraTotalViewCount|cameraAdditionalViewCount|cameraBlendingViewCount|cameraMaxStackDepthTotal)(?![\w])
> ```
>
> これで `payload.cpuMs` も `payload.cameraTotalViewCount` も**マッチしない**ことを T6 で確認する。

> **V6 の限界（既知。次スライスへの申し送り）:** 本スライスのパネルは saved search なので、フィールドの露出面は `columns` / `sort` に限られる。
> K3 で Lens パネルが入ると、フィールド参照は Lens の `state` 内へ移り **V6 では捕まえられなくなる**。
> K3 では V6 を Lens 対応へ拡張する必要がある。**本スライスでは拡張しない**（対象の shape が確定していないものに対して scanner を書くのは無駄）。

`KibanaSavedObjectValidationIssue`（30 行）: `RuleId`（`"V3"` 等）/ `LineNumber` / `ObjectId` / `Message`。
**メッセージには必ず行番号と対象 id を含める。** 指摘を読んで NDJSON のどこを直せばいいか分からないと意味がない。

---

### K2-3 正本が V1〜V6 を満たすことのテスト

**`tests/DebugStudio.Export.Tests/Elastic/Kibana/KibanaOverviewBundleTests.cs`（新規 80 行）**

`ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson()` を直接呼び、`Parse` → `Validate` して**指摘 0 件**を assert する。
指摘があった場合は**全件をメッセージに含めて失敗させる**（1 件ずつ潰す往復を避ける）。

これが §1.3 の中心であり、**「パネル 0 枚」が二度と commit されないことの唯一の保証**である。

---

### K4-1 index template に `status` の keyword mapping

**ファイル:** `tools/DebugStudio/src/DebugStudio.Export/Elastic/ElasticTelemetryIndexTemplateDefinition.cs`

`ElasticBulkTelemetryNdjsonBuilder.CreatePayloadDictionary` は `status` を出している（原文ママ）:

```csharp
["status"] = record.Status,
```

一方 index template の `properties` に `status` が無い。結果、**dynamic mapping で `text` + `.keyword` サブフィールドになり、集計するには `status.keyword` と書く必要が出る**。他の keyword 欄と書き方が揃わない。

`["schemaVersion"]` の直後あたりに 1 行足す:

```csharp
["status"] = new { type = "keyword" },
```

> **既存 index には効かない。** index template の変更は**既に作られた index の mapping を変えない**。
> 確認は新しい index（日付が変わった後、または `curl -X DELETE "http://localhost:9200/debugstudio-telemetry-*"` の後）で行う。

---

### K4-2 README §2 に `import-kibana.ps1` を追加

**ファイル:** `tools/DebugStudio/elastic/README.md`

§2「template / pipeline の bootstrap（L2 前提）」の artifact 経由の手順が現在こうなっている（原文ママ）:

```powershell
dotnet run --project tools/DebugStudio/src/DebugStudio.ElasticArtifactGen
cd $env:LOCALAPPDATA\DebugStudio\elastic-artifacts\commands
.\import-telemetry.ps1 -ElasticUrl http://localhost:9200
```

**この手順どおりに進めた operator は saved objects を import しないので、ダッシュボードに到達できない。**
`import-kibana.ps1` の実行を追加し、§4 の「5. Kibana 確認」に `DebugStudio Overview` を開く旨を書く。

---

### K4-3 `15-telemetry-v2.md` の Phase 3 記述を現況に合わせる

**ファイル:** `unity/Assets/Docs/Architecture/15-telemetry-v2.md`

Phase 3 の行が現在こうなっている（原文ママ）:

> thin export foundation + Kibana saved objects artifact 実装済み、運用設定と dashboard 内容は今後拡張

「実装済み」がパネル 0 枚を指しているので誤読を招く。**正本がファイルになったこと**と、**パネルの作り込みは後続スライス**であることを書く。

---

## 5. 受入条件

### 5.1 必ず書く単体テスト（A-4）

| # | 対象 | 検証内容 | 置き場 |
|---|---|---|---|
| T1 | `KibanaSavedObjectBundleParser` | 2 行の NDJSON が 2 オブジェクトになる | `tests/.../Elastic/Kibana/` |
| T2 | 同上 | 末尾の空行を読み飛ばしてもオブジェクト数が増えない | 同上 |
| T3 | 同上 | `LineNumber` が 1 始まりで付く | 同上 |
| **T4** | `KibanaSavedObjectBundleValidator` | **`panelsJSON` が `"[]"` の dashboard で V3 が指摘される**（＝現行の不具合を再現し、検出できることの証明） | 同上 |
| **T5** | 同上 | **`references` に `panel_0` があるのに `panelsJSON` から参照されていないと V4 が指摘される** | 同上 |
| T6 | 同上 | `columns` に `cpuTime` を入れると V6 が指摘される。**`payload.cpuMs` でも `payload.cameraTotalViewCount` でも指摘されない**（query 文字列版の正規表現も同じケースで確認する） | 同上 |
| T7 | 同上 | 存在しない id を参照すると V5 が指摘される | 同上 |
| T8 | 同上 | `id` 重複で V2 が指摘される | 同上 |
| T9 | 同上 | `{"exportedCount":5}` のようなサマリ行があると V1 が指摘される | 同上 |
| **T10** | **正本 NDJSON** | **`ReadSavedObjectsNdjson()` の内容が V1〜V6 で指摘 0 件** | `KibanaOverviewBundleTests.cs` |
| T11 | 同上 | 5 オブジェクトが出力され、id が §2 の表と一致する。**さらに 2 本の search の `columns` が §4 K1-1 と完全一致する** | 同上 |
| **T12** | 同上 | dashboard の `panelsJSON` のパネル数が 2 で、**各パネルの `type` が `"search"`**、かつ `panelRefName` の参照先が `debugstudio-telemetry-timeline` / `debugstudio-log-warnings` である | 同上 |
| **T12b** | 同上 | **log search の `searchSourceJSON` を JSON として parse でき、`query.query` が `log.level: ("warning" or "error" or "critical")` と完全一致する** | 同上 |
| T13 | `ElasticKibanaSavedObjectsWriter` | 出力ファイルの先頭 3 バイトが BOM (`EF BB BF`) でない | `ElasticArtifactWriterTests.cs` |
| T14 | `ElasticTelemetryIndexTemplateDefinition` | `status` が `keyword` として含まれる | `tests/.../Elastic/` |
| **T15** | `ElasticKibanaSavedObjectsWriter` | **`WriteAsync` が書いたファイルの中身が `ReadSavedObjectsNdjson()` と完全一致する** | `ElasticArtifactWriterTests.cs` |
| **T16** | 同上 | **テスト自身が `Assembly.GetManifestResourceStream(ResourceName)` を開いて読んだ内容が、`ReadSavedObjectsNdjson()` の戻り値と完全一致する**（名前の存在確認だけでは不足。§6.5 G4 参照） | 同上 |

> **T4 / T5 / T10 / T12b / T15 は必ず書くこと。**
> T4 は**今まさに壊れている状態を再現し、検出できることを証明する唯一のテスト**。
> T5 は「参照だけあってパネルが無い」という、目視では絶対に見落とす形の破綻を捕まえる唯一の手段。
> T10 は T4/T5 の検算を**実際に配布される正本へ適用する**唯一の接続点で、これが無いと validator は誰も守らない飾りになる。
> **T12b は §4 K1-1 で警告したエスケープ地獄（JSON 文字列の中の JSON の中の KQL、その中の `"`）を検出する唯一の手段。**
> ここを間違えても import は成功し、パネルも描画され、**ただ 0 件になるだけ**なので、実地確認でも見落としうる。
> **T15 は「正本ファイルを直したのに writer が古い経路のままだった」を検出する唯一の手段。**
> T10〜T12 は `ReadSavedObjectsNdjson()` しか見ないので、`WriteAsync` が別の中身を書いていても全て緑になる。
>
> **T6 の後半（`payload.*` では指摘されない）を省略しないこと。** V6 を部分文字列検索や `\b` で実装した場合、
> このケースだけが誤りを検出する。

テスト名は日本語で書く。既存の慣習（例: `KibanaSavedObjectsはOverviewDashboardとSavedSearchを出力する`）に合わせる。

### 5.2 コマンド

```bash
dotnet test tools/DebugStudio/DebugStudio.sln
```

exit 0 かつ 1 件以上実行され failed 0 であること。**テスト 0 件は失敗扱い**（コンパイルエラーが 0 件として現れる）。

Unity 側のコードは**一切変更しない**ので `pwsh tools/run-tests.ps1` は不要。
ただし `unity/Assets/Docs/Architecture/15-telemetry-v2.md`（K4-3）を編集するため、**Unity の `.meta` が要るファイルを増やしていないことだけ確認する**（`.md` の追加はしない。既存ファイルの編集のみ）。

### 5.3 実地確認（手動）— **これを飛ばして完了としない**

**§1.4 のとおり、前スライスの「Elastic を立てて実際に確認していない」が未解消のまま積み上がっている。本スライスで解消する。**

```powershell
cd tools/DebugStudio/elastic
docker compose up -d
```

Elasticsearch / Kibana は **8.17.0**（`docker-compose.yml` で固定。`xpack.security.enabled=false` なので API key 不要）。

1. `dotnet run --project tools/DebugStudio/src/DebugStudio.ElasticArtifactGen`
2. `%LOCALAPPDATA%\DebugStudio\elastic-artifacts\commands\import-telemetry.ps1 -ElasticUrl http://localhost:9200`
3. **`%LOCALAPPDATA%\DebugStudio\elastic-artifacts\commands\import-kibana.ps1 -KibanaUrl http://localhost:5601`**
4. import のレスポンスを確認する:
   - `"success": true`
   - **`"successCount": 5`**
   - **`"errors"` が返っていないこと**（1 件でも返ったら §7 に全文を貼る）
5. DebugStudio を起動して Unity を接続し、しばらくプレイする
6. Telemetry パネルの **Elastic Preflight** → **Elastic Push**
7. `http://localhost:5601` で **Dashboard → `DebugStudio Overview` を開く**

**合格条件:**

| # | 確認 | 期待 |
|---|---|---|
| 1 | `DebugStudio Overview` を開く | **パネルが 2 枚描画される**（現状は 0 枚） |
| 2 | 左のパネル | telemetry のドキュメントが行として出る。`kind` / `name` / `buildVersion` の列が見える |
| 3 | 右のパネル | log が出る。**`log.level` が warning / error / critical のものだけ**であること |
| 4 | Discover → `debugstudio-telemetry-*` | `buildVersion` / `platform` / `deviceModel` / `osVersion` / `engineVersion` が**値付きで**入っている（前スライスの積み残しの解消） |
| 5 | 同上 | `status` が `keyword` として集計できる（`status.keyword` を要求されない）。**新しい index でのみ確認できる**（K4-1 の注記） |

`4` が満たせない場合、それは**本スライスの不具合ではなく前スライスの積み残し**なので、区別して §7 に書くこと。

```bash
curl "http://localhost:9200/debugstudio-telemetry-*/_search?size=1&_source=buildVersion,platform,deviceModel,osVersion,engineVersion,sessionId,status"
```

---

## 6. 共通の注意

**Kibana 8.17 の saved object schema（本スライス最大のリスク）**

- §4 K1-1 に書いた JSON shape は、**Kibana 8.17 のドキュメントと既存 writer の形から起こしたものであり、実際に import して通したものではない。**
  **§5.3 の手順 4 で `errors` が返ったら、そのレスポンス全文を §7 に貼ること。** 推測で JSON をいじって再試行する前に、まず現物を記録する
- import は `overwrite=true` で走る（`import-kibana.ps1` の現行実装）。**既存の同 id オブジェクトは上書きされる**
- **`panelsJSON` の `version` フィールドは書かない。** Kibana の版に紐づく値で、間違えると migration が誤作動する。省略すれば Kibana 側が補う
- saved object の id を変えない。`debugstudio-overview-dashboard` 等は既存 id を踏襲する。変えると古い import 済みオブジェクトが孤児になる

**NDJSON の取り扱い**

- **1 オブジェクト 1 行。改行で整形しない。** エディタの自動整形（Prettier 等）が `.ndjson` に効く設定になっていないか確認する
- **BOM なし UTF-8**（K1-3）
- 末尾に改行 1 個を付ける（現行 writer も付けている）

**構造**

- parser / validator に **IO を入れない**。`File.*` を 1 行でも書いたらテストが書けなくなる。ファイルを読むのは writer と test だけ
- §3 の予想行数を超えそうになったら、**書き進める前に手を止めて §7 に書く**

**このスライスに Unity のコードは含まれない**

- Unity 側は 1 行も変更しない。`?.` / `??` の偽 null チェック問題も、`record` 禁止も**本スライスには関係しない**
- DebugStudio は net8.0 なので **`record` を使ってよい**

**やってはいけない**

- ダッシュボードのパネルを Lens で作り込むこと（§1.4。K3 でやる）
- deprecated なフラット欄（§1.5 の 8 つ）を `columns` に入れること
- V6 を部分文字列検索で実装すること（§4 K2-2 の警告）
- `_export` のサマリ行を正本に含めること（§4 K1-1）

---

## 6.5 Phase A 外部レビュー（Grok 4.5 / 実装にも設計にも関与していないモデル）の反映

2026-08-08、Phase B へ出す前に本ドキュメントを `cursor-grok-4.5-high` に読ませ、論点 2 つに絞って批判させた。
**指摘 5 件のうち 4 件を反映済み**（下表）。以下は反映後の記録であり、実装側が再度対応する必要は無い。

| # | 指摘 | 判定 | 反映先 |
|---|---|---|---|
| G1 | T10〜T13 は `ReadSavedObjectsNdjson()` しか見ておらず、**`WriteAsync` が旧来の組み立てのままでも全緑になる** | **妥当。採用** | **T15 を追加**（§5.1）。必須テストに格上げ |
| G2 | V3/V4 はパネル数と参照の対応しか見ず、`panelsJSON[].type` が `search` かは未検証。by-value の Lens に差し替わっても緑 | **妥当。採用** | **T12 を拡張**（`type == "search"` と参照先 id を assert） |
| G3 | §2 で `columns` と log の KQL を確定させているのに、それを検証するテストが無い | **妥当。採用。これが最も重い** | **T11 を拡張し T12b を新設**。§4 K1-1 で「エスケープ地獄」と警告しておきながら検出手段が無かった |
| G4 | 「正本が commit 済みファイルで EmbeddedResource 経由」であることを見るテストが無い。C# 文字列定数に戻しても緑 | **趣旨は妥当。対処は変更。2 巡目で再指摘され、修正して閉じた** | 下記 |
| G5 | V6 は `columns`/`sort` しか見ず、`searchSourceJSON` の query 内の参照は破っても緑 | **妥当。採用** | **V6 を query 文字列へ拡張**（§4 K2-2）。あわせて `\b` が `payload.cameraTotalViewCount` に誤マッチする罠を明記し、正規表現を確定 |

**G4 の経緯（2 巡目で差し戻された。1 巡目の反映が不十分だった）**

1 巡目では T16 を「`GetManifestResourceNames()` に `ResourceName` が含まれる」とした。2 巡目でこれが**不十分**と指摘され、正しかった:

> csproj に `EmbeddedResource` を残したまま `ReadSavedObjectsNdjson` を C# 文字列定数に戻し、`WriteAsync` もそれを書けば、
> **T15・T16 とも緑のまま**「正本 = commit 済み NDJSON → 埋め込み経由」が成立しない。

リソースが**存在すること**と、実装が**それを読んでいること**は別。T16 を「**テスト自身がストリームを開いて読み、`ReadSavedObjectsNdjson()` の戻り値と一致すること**」へ引き上げて閉じた。
これで §1.1（正本はファイル、C# は吐くだけ）も同時に閉じる。**2 巡目はこの 1 件だけを返し、他は全て閉じたと判定した。**

**収束判定: 2 巡で終了。** 3 巡目は回さない — 残指摘は 1 件のみで、修正内容（比較対象をリソース名から中身へ変える）は指摘の中で確定しており、確認のためだけに外部モデルを再度回すのは `CLAUDE.md` のコスト規律に反する。

**Grok の「論点1（Kibana 8.17 の JSON shape）に欠陥は見当たらない」は、検証済みとして扱わない。**
このレビューは本ドキュメント 1 ファイルを読んだだけで、実際の Kibana に import しても、8.17 の saved object schema と照合してもいない。
**§6 冒頭のリスク記述と §5.3 手順 4 は、この外部レビューによって一切軽くなっていない。**
`search` 型の必須属性（`isTextBasedQuery` 等、版によって増えている）や `panelsJSON` の `version` 省略可否は、**実際に import して `errors` が返るかどうかでしか分からない。**

---

## 7. Phase C からの差し戻し

<!-- Phase B / C の往復で追記する。Phase A では空。 -->

---

## 8. Phase C レビュー

<!-- Phase C で追記する。Phase A では空。 -->

---

## 9. Phase C' 監査

<!-- Phase C' で追記する。Phase A では空。 -->
