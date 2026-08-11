# Kibana ダッシュボードの中身（K3）ハンドオフ (2026-08-11)

> 前スライス: [`KIBANA_DASHBOARD_FOUNDATION_HANDOFF_2026-08-08.md`](KIBANA_DASHBOARD_FOUNDATION_HANDOFF_2026-08-08.md)（器と安全網。マージ済み）
> 本スライス: **中身**（K3）。前スライスの §1.4 が「次スライス」と名指しした部分。

---

## 0. 1分で把握

現在 Kibana に import される `DebugStudio Overview` は、**saved search 2 枚（telemetry の生行 / warning 以上のログ行）を並べただけ**で、集計パネルが 1 つも無い。同じものは DebugStudio の LogViewer が Docker なしでライブに出すため、**ダッシュボードとしての価値は現状ほぼゼロ**である。これは事故ではなく、前スライスが「器と安全網だけ作る」と決めた結果（前 HANDOFF §1.4）。

本スライスで答えられるようにする問いは 2 つだけ:

| 問い | 見るもの |
|---|---|
| **Q1. 今の実行で何が重いか** | 1 run の中を時間軸で見る（fps / cpuMs / gpuMs / メモリの推移 ＋ そのとき動いていた span） |
| **Q2. 前の実行と比べて何が重くなったか** | run をまたいで代表値を並べる（AppStartup / SceneLoad / cpu p95 / fps p05 / メモリ） |

**この 2 問に答えないパネルは作らない。** Kibana が DebugStudio に構造的に勝てるのは次の 4 点だけで、そこに寄せないパネルは LogViewer の劣化コピーになるため:

1. **横断** — run / build / platform / device をまたいだ比較
2. **履歴** — 「前からこうだったのか、今回からか」
3. **分布** — 最後の 1 値ではなく p05 / p95
4. **結合** — `traceId` / `producerSequence` による telemetry ↔ log の突き合わせ

やること:

1. **K3-0（gate）** — セッション属性 5 フィールドが Elastic に**実在しない**（前スライス U-4、実測 1154 件中 0 件）。原因を特定して直す。**これが通るまでパネルを作らない**
2. **K3-1 / K3-2** — 検算ルール（V ルール）を拡張し、Lens パネルと query 内フィールド参照も機械的に赤にできるようにする
3. **K3-3** — 各パネルの集計クエリを ES|QL で確定させ、`tools/DebugStudio/elastic/queries/` に正本として置く
4. **K3-4** — Kibana UI でパネルを組み、`_export` した NDJSON で正本を差し替える
5. **K3-5** — README とドキュメントの更新

やらないこと（理由は §1）:

- **起動 stage 別 span の追加**（人間の判断。「起動はビルド後の exe だけ気にすればよく、Editor の各 stage はある程度飲み込むべき」）
- log ストリームへの `buildVersion` 付与（前スライスから継続してスコープ外。session 単位までで妥協する）
- 前スライス §8.6 の **S-1 / S-2 / S-3**（`Writers/` と `Elastic/` の配置整理）。別 PR
- ECS 準拠

---

## 0.5 この HANDOFF の設計そのものを批判的にレビューすること（**必須**）

**この HANDOFF は仕様であって、正しさの保証ではない。**

前スライスで、実在しないフィールド `log.level` を参照した saved search が **Phase C レビュー 3 巡と Phase C' 監査を通過した**（前 HANDOFF §8.4 / §8.5）。原因は「HANDOFF に書いてある文字列と実装が一致するか」だけを見ていたこと。**仕様そのものが間違っている場合、仕様との照合をいくら重ねても検出できない。**

したがって実装側に次を要求する。

| 粒度 | 何を疑うか |
|---|---|
| **方針**（§1） | 「比較軸は run」「クエリ先行」「Lens 手書き禁止」等の判断そのもの。もっと良い立て方があるなら書く |
| **パネル**（§2） | そのパネルが Q1 / Q2 のどちらにどう答えるか説明できるか。説明できないパネルは**作らずに指摘する** |
| **クエリ**（§4 K3-3） | 本文のクエリ草案は**未検証**。文法・集計の意味・欠測時の挙動を疑う |
| **ファイル配置**（§3） | 責務が増えたファイルの置き場。§3.5 の「設計判断としてこう決めた」に反対なら書く |
| **テスト**（§5.1） | そのテストが本当に赤にできるか。**赤を実際に見てから緑にすること** |

**反対意見は §6 に書いてから進むこと。** 黙って従うのも、黙って別のことをするのも失敗として扱う。指摘が仕様の誤りを突いていた場合、それは Phase A（この HANDOFF を書いた側）の失点であって実装側の失点ではない。

**特に、次のいずれかに気づいたら手を止めて §6 に書くこと:**

- パネルが参照するフィールドが Elastic に実在しない（K3-0 で潰したはずだが再発しうる）
- クエリが構文エラーで通らない、または通るが意味が違う
- 「run 比較」に必要なデータ（複数 run 分）がそもそも Elastic に入っていない

---

## 1. 確定方針（設計判断。実装側で勝手に変更しない。反対なら §6 へ）

### 1.1 比較軸は **run（`sessionId`）**。build は軸ではなくラベル

重くなる原因はビルドだけではない。同一ビルドでも Editor / 実機、キャッシュ有無、シーン内容、端末で変わる。**`buildVersion` を横軸にすると、build 以外の理由で重くなったケースが全部見えなくなる。**

したがって:

- **横軸は run**（`sessionId` を `MIN(@timestamp)` 順に並べた離散軸）
- `buildVersion` / `platform` / `deviceModel` / `osVersion` / `engineVersion` は**同じダッシュボードの「run メタ表」に横並びで出す**
- 読み方は「4 番目の run から重い → メタ表を見ると端末が変わっている」という**人間の当てはめ**

### 1.2 Editor の run と Player の run を混ぜない

人間の判断: **「起動はビルド後の exe だけを気にすればよく、Editor の各 stage はある程度飲み込むべき」。**

`platform` は `Application.platform.ToString()`（[`UnitySessionAttributes.cs:35`](../../unity/Assets/OneStarMaker/Scripts/Foundation/DebugSocket/UnitySessionAttributes.cs)）なので、値は `WindowsEditor` / `WindowsPlayer` 等になる。

- **AppStartup を見るパネルは `platform` で Player に絞れること**をダッシュボードの必須要件とする（フィルタ or コントロール）
- **起動の内訳を細かくするための Unity 側の観測点追加は行わない。** 現状 AppStartup は `BeforeSceneLoad` / `AfterSceneLoad` の 2 span で、成功時の `payload.stage` は区間名に潰れる（[`AbstractApplicationInitializer.cs:322`](../../unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs)）。**この 2 分割のままでよい**

### 1.3 パネルより先にクエリを確定する

Lens のパネル定義は Kibana のバージョンに依存する巨大な JSON だが、**そこに埋まっている「何をどう集計したいか」はバージョンに依存しない。**

前スライスの事故（実在しないフィールドを参照したパネルが全レビューを通過）は、**「パネルを作る」と「その数字が本当に取れる」が同じ作業に混ざっていた**ために起きた。分離する:

```
ES|QL クエリを書く → 実 Elastic に当てて通ることを確認 → queries/ に正本として commit
  → その正本を Kibana UI でパネルに起こす → _export → NDJSON 差し替え
```

**クエリが通らないうちにパネルを作らない。** クエリ正本があれば、Kibana のバージョンが上がってパネル JSON が壊れても、**何を作りたかったかは残る**。

### 1.4 Lens / ES|QL パネルの `state` JSON を手書きしない

前スライス §1.2 の踏襲。**Kibana UI で組んで `_export` したものだけを正本にする。** 手書きすると必ず壊れるうえ、壊れ方が「import は通るが描画されない」になって検出が遅れる。

これにより K3-4 は **Kibana UI を操作できる担い手**（人間、またはブラウザを操作できるエージェント）が必要になる。§4 の担い手欄を見ること。

### 1.5 データが実在しないフィールドをパネルに載せない（**K3-0 が gate**）

前スライス実測（§8.4 U-4）: telemetry index 1154 件のうち、`buildVersion` / `platform` / `deviceModel` / `osVersion` / `engineVersion` は **`_field_caps` にフィールドとして存在せず、`exists` クエリで 0 件**。原因は (a) Unity → DebugStudio の handshake で飛んでいない (b) export mapper が落としている (c) 前スライス実装前のデータしか入っていない、のいずれかで**未特定**。

§1.1 と §1.2 の両方がこの 5 フィールドに依存している。**K3-0 が終わるまで K3-3 以降に進まない。**

### 1.6 deprecated なフラット欄を参照しない（継続）

`cpuTime` / `gpuTime` / `managedMem` / `nativeMem` / `cameraTotalViewCount` / `cameraAdditionalViewCount` / `cameraBlendingViewCount` / `cameraMaxStackDepthTotal` の 8 語は **Telemetry Contract v3 で deprecated**。正本は `payload.*` 側。

現行の V6 がこれを saved search の `columns` / `sort` / query について検算している。**K3 で追加するパネルにも同じ禁止がかかる**（V7 以降で lens も見る。§4 K3-2）。

---

## 2. ダッシュボード仕様

**data view は既存の 2 本を使う**（`debugstudio-telemetry-dataview` = `debugstudio-telemetry-*`、`debugstudio-log-dataview` = `debugstudio-log-*`。いずれも `timeFieldName = @timestamp`）。新規追加しない。

**ダッシュボードは 2 枚にする。** 1 枚の NDJSON バンドルに両方を入れる（§3.5）。

### 2.1 D1「Run Timeline」— 今の run で何が重いか

**1 run を選んで時間軸（秒）で見る。** run の選択は `sessionId` のコントロールで行う。

| # | パネル | データ | 集計 |
|---|---|---|---|
| D1-1 | CPU / GPU 推移 | telemetry, `kind=sample AND name=ProfilerSummary` | `@timestamp` の date histogram（1s）× `payload.cpuMs` / `payload.gpuMs` の平均 |
| D1-2 | fps 推移 | 同上 | 同 histogram × `payload.fps` の**最小値**（平均ではない。落ち込みを見る） |
| D1-3 | メモリ推移 | 同上 | 同 histogram × `payload.managedBytes` / `payload.nativeBytes` の最大値 |
| D1-4 | 重い span | telemetry, `kind=span` | テーブル: `name` / `payload.targetIdentity` / `elapsedMs` / `payload.managedDeltaBytes`、`elapsedMs` 降順 |
| D1-5 | イベント発生点 | telemetry, `kind=event` | 同 histogram × `name`（`GcSpike` / `UiCost`）の件数 |
| D1-6 | 異常タグ内訳 | telemetry | `tags` の terms 集計（件数） |
| D1-7 | warning 以上のログ | log | **既存の saved search `debugstudio-log-warnings` をそのまま貼る** |

**読み方**（ダッシュボードの description に書くこと）: D1-1/D1-2 で落ちている区間を見つける → D1-4 でその時刻の span を名指し → D1-5 / D1-6 で GC か UI かを切る。

**注意 — D1-4 の span は入れ子である。** `SceneTransition` は `SceneLoad` / `SceneUnload` を内包する（`parentSpanId` で親子が付く）。**合計する棒グラフを作ってはいけない**（二重計上）。`name` 別に分けたテーブルにする。

### 2.2 D2「Run over Run」— 前と比べて何が重くなったか

**横軸は run。** run は `sessionId` を `MIN(@timestamp)` 順に並べる。

| # | パネル | 集計 | なぜこの統計量か |
|---|---|---|---|
| D2-1 | **run メタ表** | run ごとに 開始時刻 / `buildVersion` / `platform` / `deviceModel` / `engineVersion` / run 長（秒） | 「いつ何が変わったか」の当てどころ。§1.1 の要 |
| D2-2 | AppStartup | run × `payload.stage`（`BeforeSceneLoad` / `AfterSceneLoad`）別の `elapsedMs` 最大値 | run に 1 回ずつしか出ないので max = その run の値 |
| D2-3 | SceneLoad | run × `payload.targetIdentity` 別の `elapsedMs` の p50 | シーンごとに重さが違うので混ぜない。回数が少ないので p50 |
| D2-4 | CPU | run 別 `payload.cpuMs` の **p95** | 平均は「ほとんどの時間が暇」に引っ張られて鈍い |
| D2-5 | fps | run 別 `payload.fps` の **p05** | **平均 fps は無意味。**効くのは悪い方の裾 |
| D2-6 | メモリ | run 別 `payload.managedBytes` の最大値 | ピークが上がっていれば重くなっている |
| D2-7 | 異常発生率 | run 別 `GcSpike` / `UiCost` の件数を **run 長で割った率** | run 長が違うので絶対数は比較できない |

**D2-2 は `platform` で Player に絞れること**（§1.2）。他のパネルは絞らなくてよい。

**D2-7 の分母**: `ProfilerSummary` は 1 秒に 1 件出る（[`DebugProfilerView.cs:208`](../../unity/Assets/OneStarMaker/Scripts/Debug/Profiler/DebugProfilerView.cs) の `LogSummary`）。つまり **run 内の `ProfilerSummary` 件数 ≒ run の秒数**であり、これを分母に使える。`MAX(@timestamp) - MIN(@timestamp)` でもよい。**どちらを採るかは K3-3 で実測して決め、決めた理由を §6 に書くこと。**

### 2.3 percentile の取り方（**間違えやすい**）

開発者 1 人の数 run しか無いので、**run をまたいだ percentile は取らない。**

```
正: run 内で p95 を取る → その代表値を run 間で並べる
誤: 全 run を混ぜて p95 を取る
```

誤の方をやると、run が 1 本増えるたびに過去の値まで動く。

---

## 3. 変更対象ファイル一覧（A-1: 規模見積もり）

### 3.1 クエリ正本（新規ディレクトリ）

| ファイル | 現在 | 予想 | 責務数 |
|---|---|---|---|
| `tools/DebugStudio/elastic/queries/README.md` | — | 40 行 | 1（クエリ正本の使い方） |
| `tools/DebugStudio/elastic/queries/runs.esql` | — | 12 行 | 1 |
| `tools/DebugStudio/elastic/queries/app-startup-per-run.esql` | — | 12 行 | 1 |
| `tools/DebugStudio/elastic/queries/scene-load-per-run.esql` | — | 12 行 | 1 |
| `tools/DebugStudio/elastic/queries/frame-cost-per-run.esql` | — | 14 行 | 1 |
| `tools/DebugStudio/elastic/queries/event-rate-per-run.esql` | — | 14 行 | 1 |

### 3.2 正本 NDJSON

| ファイル | 現在 | 予想 | 注意 |
|---|---|---|---|
| `tools/DebugStudio/elastic/kibana/debugstudio-overview.ndjson` | 5 行 / 2514 byte | 15〜20 行 / 数十 KB | **1 行 = 1 saved object。行数ではなくオブジェクト数で数える。** eol=lf / BOM 無しを維持（`.gitattributes` で固定済み） |

### 3.3 DebugStudio.Export 側

| ファイル | 現在 | 予想 | 責務数 |
|---|---|---|---|
| `Elastic/Kibana/KibanaSavedObjectBundleValidator.cs` | 318 行 | **後述の分割前提で 380 行以内** | 検算のみ |
| `Elastic/Kibana/DeprecatedFieldCatalog.cs`（新規） | — | 40 行 | 1（deprecated 語の単一正本） |
| `Elastic/Kibana/Validation/DashboardPanelRules.cs`（新規・条件付き） | — | 120 行 | 1 |
| `Elastic/Kibana/Validation/SavedObjectFieldRules.cs`（新規・条件付き） | — | 120 行 | 1 |

**A-2: `KibanaSavedObjectBundleValidator.cs` は 318 行で、V7〜V11 を足すと 500 行を確実に超える。** 先に分割先を決めておく:

1. **必ずやる**: deprecated 8 語の `HashSet` と `Regex` の二重管理（前スライス外部レビュー指摘 3）を `DeprecatedFieldCatalog.cs` へ切り出し、**Regex は語リストから生成する**
2. **`Validate` 本体が 400 行を超えたら**、V ルールを `Elastic/Kibana/Validation/` へルール単位で分割する。`Validate` は各ルールを呼ぶだけにする
3. 分割しても **`KibanaSavedObjectBundleValidator.Validate` という公開入口は変えない**（テストと呼び出し側が壊れる）

### 3.4 テスト側

| ファイル | 現在 | 予想 |
|---|---|---|
| `tests/.../Elastic/Kibana/KibanaSavedObjectBundleValidatorTests.cs` | 171 行 | 300 行 |
| `tests/.../Elastic/Kibana/KibanaOverviewBundleTests.cs` | 118 行 | 180 行 |
| `tests/.../Elastic/Kibana/KibanaSavedObjectFieldMappingTests.cs` | 107 行 | 190 行 |

**`KibanaOverviewBundleTests` に IO を持ち込まないこと。** このクラスは「埋め込みリソースだけを見る」責務で、前スライスで一度 IO が混入して 206 行に膨らみ、マージ前に差し戻した経緯がある（前 HANDOFF §8.6 是正 2）。index template と突き合わせる検算は **`KibanaSavedObjectFieldMappingTests` 側**。

### 3.5 新責務の配置（**A-3: これは設計判断としてこう決めた**）

| 判断 | 内容 | 理由 |
|---|---|---|
| **P-1** | **クエリ正本は `tools/DebugStudio/elastic/queries/` に置く。C# に埋め込まない** | パネルの意図を Kibana のバージョンから独立させるのが目的（§1.3）。C# に入れると「artifact として出力するか」という別の判断が発生する。**本スライスでは artifact 出力しない。人間と実装者が curl で叩くためのファイル** |
| **P-2** | **ダッシュボードは 2 枚だが、NDJSON は 1 ファイルのまま** | ファイルを分けると csproj の `EmbeddedResource` / `LogicalName`、writer、import コマンド、テストの 4 箇所が連動する。data view も共有する。**1 バンドル = 1 import が今の設計** |
| **P-3** | **`ElasticKibanaSavedObjectsWriter`（52 行）は触らない** | 埋め込みリソースを書き出すだけの責務で、パネルが増えても仕事は変わらない |
| **P-4** | **deprecated 語の正本は `DeprecatedFieldCatalog` 1 箇所** | 現状 `HashSet` と `Regex` の二重管理で、片方だけ更新すると columns/sort と query で判定が食い違う |
| **P-5** | **`CollectMappedFieldPaths`（現在テストの private）は production に昇格させない**、テスト内で共有する | 消費者のいない public API を先に生やさない（`UNUSED_API_INVENTORY_2026-08-03.md` の方針）。lens 対応で必要になったら**テストの共有ヘルパ**として切り出す |

---

## 4. 施工チケット

**担い手欄の意味:**

- **headless** — 通常の実装エージェントで完結する（コード + テスト + curl）
- **要 Kibana UI** — ブラウザで Kibana を操作できる担い手が必要。headless の実装者はここを飛ばし、**成果物の NDJSON を受け取ってから続きをやる**

| # | 内容 | 担い手 | 依存 |
|---|---|---|---|
| K3-0 | セッション属性 5 フィールドの欠損を直す（**gate**） | headless | — |
| K3-1 | V ルール拡張（V7〜V10）+ deprecated 語の一元化 | headless | — |
| K3-2 | lens パネルの検算対応（V11） | headless | K3-1 |
| K3-3 | ES|QL クエリ正本の確定 | headless | K3-0 |
| K3-4 | Kibana UI でパネルを組み `_export` → 正本差し替え | **要 Kibana UI** | K3-2, K3-3 |
| K3-5 | README / ドキュメント更新 | headless | K3-4 |

### 環境（全チケット共通）

Elasticsearch / Kibana は **8.17.0**、`xpack.security.enabled=false`（[`docker-compose.yml`](../../tools/DebugStudio/elastic/docker-compose.yml)）。

```powershell
cd tools/DebugStudio/elastic
docker compose up -d
dotnet run --project tools/DebugStudio/src/DebugStudio.ElasticArtifactGen
& "$env:LOCALAPPDATA\DebugStudio\elastic-artifacts\commands\import-telemetry.ps1" -ElasticUrl http://localhost:9200
& "$env:LOCALAPPDATA\DebugStudio\elastic-artifacts\commands\import-kibana.ps1"   -KibanaUrl  http://localhost:5601
# → http://localhost:5601/app/dashboards#/view/debugstudio-overview-dashboard
```

> **`dotnet run` は必ず本ブランチの作業ツリーから実行すること。** 別ブランチから実行すると旧 artifact が `%LOCALAPPDATA%` に生成され、import しても意図した内容にならない。
>
> **artifact 生成 → `import-kibana.ps1` を実行しない限り、ダッシュボードは Kibana に存在しない。** 前スライスの実地確認では「そもそも一度も import されていなかった」ことが「Dashboard が見えない」の直接原因だった。

---

### K3-0 セッション属性 5 フィールドの欠損を直す（**gate**）

**これが終わるまで K3-3 / K3-4 に進まない。**

#### 事実（前スライス §8.4 U-4 の実測）

telemetry index 1154 件に対し、`buildVersion` / `platform` / `deviceModel` / `osVersion` / `engineVersion` は **`_field_caps` にフィールドとして存在せず、`exists` で 0 件**。`sessionId` は存在し値も入っている。

原因は次のいずれかで、**まだ特定されていない**:

- **(a)** Unity → DebugStudio の handshake（Welcome）で属性が飛んでいない
- **(b)** DebugStudio の export mapper / Elastic push が落としている
- **(c)** 単に前スライス実装前の古いデータしか入っていない

#### 手順

1. **まず (c) を切り分ける。** これが一番安い。Unity を 1 回起動して DebugStudio に接続し、telemetry を新規に流したうえで再度 `_field_caps` を見る

```bash
curl -s "http://localhost:9200/debugstudio-telemetry-*/_field_caps?fields=buildVersion,platform,deviceModel,osVersion,engineVersion"
curl -s "http://localhost:9200/debugstudio-telemetry-*/_search?size=1&sort=@timestamp:desc" | head -c 2000
```

2. (c) でなければ **(b)** を見る。Elastic push は record を直接 serialize せず `CreatePayloadDictionary` のホワイトリストを通る。5 キーが**そこに入っているか**を実コードで確認する（前スライス `TELEMETRY_SESSION_ATTRIBUTES_HANDOFF_2026-08-08.md` のコミットで追加済みのはずだが、**「追加した」と「実際に出ている」は別**）
3. それでも出なければ **(a)**。DebugStudio 側で受信した Welcome の内容をログに出して確認する

#### 実装時の注意

- **属性は `sessionId` をキーに引く。「現在接続中のセッション」を使ってはいけない。** 過去の record に現在のセッションの属性が付くと、run 比較が静かに嘘になる
- **属性が引けない / 空文字のときはキー自体を出さない。** 空文字で埋めると Kibana で `""` という値の run が生まれる
- **handshake より前に届いた telemetry には属性が付かない**（前スライスの既知の制約）。**これは直さなくてよい**が、K3-3 のクエリで「属性が null の run が混ざる」ことを前提にすること

#### 完了条件

`_field_caps` に 5 フィールドが現れ、直近 run の document に**値が入っている**こと。curl の出力を §6 か §7 に**貼る**。

> **「コードを直したから入るはず」で完了にしないこと。** 前スライスはそれで 1 スライス分遅れた。

---

### K3-1 V ルール拡張（V7〜V10）+ deprecated 語の一元化

現在の検算は V1〜V6（[`KibanaSavedObjectBundleValidator.cs`](../../tools/DebugStudio/src/DebugStudio.Export/Elastic/Kibana/KibanaSavedObjectBundleValidator.cs)）:

| ルール | 内容 |
|---|---|
| V1 | 全行が空でない `type` と `id` を持つ |
| V2 | `id` が重複しない |
| V3 | dashboard の `panelsJSON` が JSON 配列で、要素数 1 以上 |
| V4 | `panelsJSON` の `panelRefName` と `references` の `panel_*` が 1:1 |
| V5 | `references[].id` が bundle 内に実在する |
| V6 | search の `columns` / `sort` / query に deprecated 8 語が無い |

**追加するルール**（すべて前スライスの積み残し。出典を併記する）:

| ルール | 内容 | 出典 |
|---|---|---|
| **V7** | **各パネルは非空の `panelRefName` を持つ。** 現在の V4 は `panelRefName` を持たないパネルを見逃す（`panelsJSON` に要素があり `references` が空なら V3/V4 とも緑になる） | 外部レビュー指摘 1 |
| **V8** | **`references[].type` が参照先オブジェクトの `type` と一致する。** 現在の V5 は id の存在しか見ず、誤 type でも緑 | 外部レビュー指摘 2 |
| **V9** | **`type=search` は `attributes.kibanaSavedObjectMeta.searchSourceJSON` を文字列として持つ。** これが無いと V6 の query 走査が丸ごとスキップされ、**前スライス §0 が直した「器が無い」不具合が再発しても緑になる** | C' 監査 A1 / U-6 |
| **V10** | **`type=search` の `sort` は必須かつ配列**（欠如・文字列は不可）。V6 の sort 検査は `ValueKind==Array` のときだけ走るため、欠如／文字列に戻ると検査ごと消える | C' 監査 A1 / U-6。欠如も赤は §6.4 で確定 |

**deprecated 語の一元化**（外部レビュー指摘 3 / C' 監査 A3）:

- `DeprecatedFields`（`HashSet`）と `DeprecatedFieldInQuery`（`Regex`）が同じ 8 語を二重に持っている。**片方だけ更新すると columns/sort と query で判定が食い違う**
- `DeprecatedFieldCatalog` に語リストを 1 本化し、**Regex はそのリストから生成する**

#### 実装時の注意

- **V7 と V4 の関係を壊さないこと。** V7 は「`panelRefName` が存在すること」、V4 は「存在するものが `references` と 1:1 であること」。V7 を V4 の中に混ぜると、どちらで落ちたか分からなくなる
- **issue の `RuleId` は既存の書式（`"V1"` 等）に揃える。** メッセージは `行 N (id='X'): V7 — …` の形（既存に合わせる）
- **`Validate` は例外を投げず issue のリストを返す**（既存の契約）。新ルールも同じ
- **検算経路を純関数に保つこと。** `Elastic/Kibana/` のうち **parser / validator / catalog / model 系**（`KibanaSavedObjectBundleParser` / `KibanaSavedObjectBundleValidator` / `DeprecatedFieldCatalog` / `KibanaSavedObject` / `KibanaSavedObjectBundle` / `KibanaSavedObjectReference` / `KibanaSavedObjectValidationIssue`）には `System.IO` / `File.` / `GetManifestResourceStream` が 1 つも無い。**ここに IO を持ち込むと検算がテスト不能になる**

> **訂正（2026-08-11）:** 初版はここを「`Elastic/Kibana/` の 6 ファイルには IO が 1 つも無い（C' 監査が裏付け済み）」と書いていたが、**これは事実誤り**だった。同ディレクトリの `ElasticKibanaSavedObjectsWriter`（`GetManifestResourceStream` / `File.WriteAllTextAsync`）と `ElasticKibanaImportCommandWriter`（`File.WriteAllTextAsync`）は**設計どおり IO を持つ**。ファイル数も 6 ではなく現在 9。
>
> **この誤りは Phase C レビュー 1 巡目に「`Elastic/Kibana/` 配下は IO 無し」と*良い点*として追認され、C' 監査で初めて実測により覆った。** §0.5 が警告した「仕様との照合をいくら重ねても仕様の誤りは検出できない」が、この HANDOFF 自身で再発した実例である。**不変条件として意味があるのは「検算経路に IO を持ち込まない」であって、ディレクトリ全体ではない。**

---

### K3-2 lens パネルの検算対応（V11）

K3-4 で入る lens 型 saved object を検算できるようにする。**K3-4 より先にやること**（安全網が無い状態でパネルを入れない）。

| ルール | 内容 |
|---|---|
| **V11** | **lens が参照するフィールドが、対応する index template に mapping されている** |

現在 `KibanaSavedObjectFieldMappingTests` が saved search の `columns` について同じ検算をしている（前スライス §8.5 で追加。**これが無かったために実在しないフィールド `log.level` を参照した saved search が全レビューを通過した**）。**その lens 版**を作る。

#### 実装時の注意

- **lens の `attributes.state` の構造は Kibana のバージョン依存で、仕様として固定できない。** したがって V11 は「state を正確に構文解析する」のではなく、**`state` の JSON を再帰的に走査して `sourceField` / `field` といったキーの文字列値を集め、それが mapping に無ければ落とす**という緩い実装でよい。**緩いことを doc コメントに書くこと**
- 前スライスの申し送り: **既存の columns 検算は `searchSourceJSON` の query 内のフィールド参照を見ていない。** query だけを実在しないフィールドに書き換えても緑のまま通る。**V6 の query 走査と同じ仕組みで拡張できるので、ここで一緒に塞ぐこと**
- **`CollectMappedFieldPaths` は production に昇格させない**（§3.5 P-5）。テスト側の共有ヘルパにする
- **index template の mapping は入れ子**（`payload.cpuMs` は `payload` → `properties` → `cpuMs`）。既存の `CollectMappedFieldPaths` が再帰で扱っているので、それに合わせる

#### このチケットの空振りを防ぐ

**追加したルールが実際に赤を出すことを確認してから緑にすること。** 具体的には、正本の lens に**存在しないフィールド名を一時的に入れて赤になることを目で見る**。前スライスでも同じ手順で空振りでないことを確認している（`columns` を `logLevel` に戻して赤を確認）。

---

### K3-3 ES|QL クエリ正本の確定

**§2 の各パネルに対応するクエリを書き、実 Elastic に当てて通るまで直し、通った形を `tools/DebugStudio/elastic/queries/` に commit する。**

実行方法:

```bash
curl -s -X POST "http://localhost:9200/_query?format=txt" \
  -H 'Content-Type: application/json' \
  -d '{"query": "FROM debugstudio-telemetry-* | LIMIT 5"}'
```

#### 以下は**草案であり未検証**。通らなければ直すのが本チケットの仕事

**runs.esql**（D2-1 run メタ表）

```esql
FROM debugstudio-telemetry-*
| STATS started = MIN(@timestamp), ended = MAX(@timestamp), docs = COUNT(*)
    BY sessionId, buildVersion, platform, deviceModel, engineVersion
| EVAL runSeconds = DATE_DIFF("seconds", started, ended)
| SORT started DESC
| LIMIT 20
```

**app-startup-per-run.esql**（D2-2）

```esql
FROM debugstudio-telemetry-*
| WHERE kind == "span" AND name == "AppStartup" AND platform == "WindowsPlayer"
| STATS ms = MAX(elapsedMs), started = MIN(@timestamp) BY sessionId, payload.stage
| SORT started ASC
```

**scene-load-per-run.esql**（D2-3）

```esql
FROM debugstudio-telemetry-*
| WHERE kind == "span" AND name == "SceneLoad"
| STATS p50 = PERCENTILE(elapsedMs, 50), n = COUNT(*), started = MIN(@timestamp)
    BY sessionId, payload.targetIdentity
| SORT started ASC
```

**frame-cost-per-run.esql**（D2-4 / D2-5 / D2-6）

```esql
FROM debugstudio-telemetry-*
| WHERE kind == "sample" AND name == "ProfilerSummary"
| STATS cpuP95 = PERCENTILE(payload.cpuMs, 95),
        fpsP05 = PERCENTILE(payload.fps, 5),
        managedMax = MAX(payload.managedBytes),
        samples = COUNT(*),
        started = MIN(@timestamp)
    BY sessionId
| SORT started ASC
```

**event-rate-per-run.esql**（D2-7）

```esql
FROM debugstudio-telemetry-*
| STATS gc = COUNT(*) WHERE name == "GcSpike",
        ui = COUNT(*) WHERE name == "UiCost",
        samples = COUNT(*) WHERE name == "ProfilerSummary",
        started = MIN(@timestamp)
    BY sessionId
| EVAL gcPerMin = 60.0 * gc / samples, uiPerMin = 60.0 * ui / samples
| SORT started ASC
```

#### 実装時の注意（**踏みやすい順**）

- **`STATS ... COUNT(*) WHERE <cond>`（集計単位のフィルタ）が 8.17 で使えるか分からない。** 通らなければ `event-rate-per-run.esql` を **2 クエリに分けて、率の計算は読む側に委ねる**（D2-7 のパネルを「件数」と「run 長」の 2 列にする）。**その場合 §2.2 の D2-7 を変えることになるので §6 に書くこと**
- **`payload.stage` のようなドット付きフィールド名**が識別子として通らない場合は バッククォート（`` `payload.stage` ``）で囲む
- **`DATE_DIFF` の引数順**（単位, 開始, 終了）を実際の出力で確認する。逆にすると符号が反転する
- **`buildVersion` 等が null の run が混ざる**（K3-0 の既知の制約: handshake 前の record）。`BY` に含めると null グループができる。**それを消すか残すかを決めて、決めた理由をクエリのコメントに書く**
- **percentile を run をまたいで取らない**（§2.3）。`BY sessionId` を外さないこと
- 各 `.esql` の先頭に **`// 対応パネル: D2-3` のように、どのパネルの正本かをコメントで書くこと**

#### 完了条件

**5 本すべてが実 Elastic で結果を返し、返った結果を §6 か §7 に貼ること。** 「構文は合っているはず」で完了にしない。

---

### K3-4 Kibana UI でパネルを組み `_export` → 正本差し替え（**要 Kibana UI**）

#### 手順

1. K3-3 のクエリ正本を見ながら、Kibana UI で D1 / D2 のパネルを組む
2. `Stack Management → Saved Objects` で、**data view 2 本・saved search 2 本・dashboard 2 枚**を選んで `_export`（**関連オブジェクトを含める**）
3. 得た NDJSON で `tools/DebugStudio/elastic/kibana/debugstudio-overview.ndjson` を差し替える
4. `dotnet test tools/DebugStudio/DebugStudio.sln` を通す（V1〜V11 と正本テストが検算する）
5. artifact を再生成し、**import し直して実際に描画されることを目で見る**

#### 実装時の注意

- **`_export` した NDJSON の `id` を書き換えないこと。** `references` と対応が切れる。既存 id（`debugstudio-telemetry-dataview` / `debugstudio-log-dataview` / `debugstudio-telemetry-timeline` / `debugstudio-log-warnings` / `debugstudio-overview-dashboard`）は**そのまま維持**する。Kibana 上でも同じ id で作ること
- **`_export` は末尾に `{"exportedCount":N,"missingRefCount":0,...}` のサマリ行を付ける。これは saved object ではないので削除すること**（V1 が `type`/`id` 空で落とす）
- **改行コードは LF、BOM なし。** `.gitattributes` で `*.ndjson` は `eol=lf` 固定済みだが、`_export` の結果を Windows のエディタで開いて保存すると壊れる
- **既存の 2 枚の saved search パネルを消さないこと。** D1-7 は `debugstudio-log-warnings` をそのまま使う
- **1 行が数 KB〜数十 KB になる。`git diff` は読めない。** それでよい（§1.4 の前提）。レビューは検算テストと**実際の描画**で行う
- **前スライスの U-8**: log ingest pipeline の `rename`（`logLevel` → `log.level`）は**新規投入時にしか走らない**。既存の古い log document は flat な `logLevel` を持ったままなので、**D1-7 の log パネルは古いデータでは 0 件のまま**。これは不具合ではない。**新しいデータで確認すること**

#### 完了条件

**import 後の Kibana で D1 / D2 のパネルが実際に値を描画していることを確認し、何が見えたかを §7 に書く。** パネルが空だった場合は「空である」と書く（前スライスの `buildVersion` 列と同じ扱い）。

---

### K3-5 README / ドキュメント更新

- [`tools/DebugStudio/elastic/README.md`](../../tools/DebugStudio/elastic/README.md)（152 行）に **§ クエリ正本の使い方**を足す（`queries/` の位置づけ、curl の叩き方、パネルとの対応）
- README の import 手順に **「artifact 生成 → import しない限りダッシュボードは存在しない」** を明記する（前スライスで実際に踏んだ罠）
- **ダッシュボードの `description` に読み方を書く**（§2.1 の「D1-1/D1-2 で落ちている区間 → D1-4 で span を名指し → D1-5/D1-6 で切る」）。これは NDJSON 側なので K3-4 と同時になる

#### やらないこと

- `unity/Assets/Docs/Architecture/` 配下に**新しい `.md` を作らないこと**。Unity プロジェクト内に `.md` を足すと `.meta` が増える。本スライスに Unity 側ドキュメントの新規追加は不要

---

## 5. 受入条件

### 5.1 必ず書く単体テスト（**A-4**）

| # | テスト | 対象 | なぜ必要か |
|---|---|---|---|
| **T1** | `パネルにpanelRefNameが無いとV7で落ちる` | V7 | 現行 V3/V4 が見逃す穴（外部レビュー指摘 1） |
| **T2** | `referencesのtypeが実オブジェクトと違うとV8で落ちる` | V8 | 外部レビュー指摘 2 |
| **T3** | `searchにkibanaSavedObjectMetaが無いとV9で落ちる` | V9 | **前スライス §0 の不具合そのものの再発検知** |
| **T4** | `searchのsortが文字列だとV10で落ちる` | V10 | 同上 |
| **T5** | `deprecated8語すべてがcolumnsとqueryの双方で落ちる` | V6 + Catalog | 現行テストは実質 `cpuTime` 1 語しか赤にできない（C' 監査 A3）。**8 語 × 2 経路を `[InlineData]` で回す** |
| **T6** | `lensが参照するフィールドはindextemplateにmappingされている` | V11 | 前スライスの事故（実在しないフィールド）の lens 版 |
| **T7** | `searchSourceJSONのquery内フィールドもmapping検算の対象になる` | V11 拡張 | 前スライスの申し送り（query は未検査だった） |
| **T8** | `正本NDJSONはV1〜V11をすべて満たす` | 正本 | 既存テストの拡張 |
| **T9** | `正本のダッシュボードは2枚あり、各パネルがreferencesと1:1` | 正本 | パネル 0 枚事故の再発検知（既存の考え方の踏襲） |

**全テストについて、赤になることを一度目で見てから緑にすること。** 前スライスでは「空振りでないことを実測で確認した」記録が残っており、それが唯一この種の穴を塞いだ。

**テストが書けないロジックがあれば、それは配置が間違っている。** 検算は純関数で、IO を持たない（§3.5 / K3-1 の注意）。

### 5.2 コマンド

```bash
dotnet test tools/DebugStudio/DebugStudio.sln
```

**現在のベースラインは 369 passed / 0 failed。** 減っていたら報告すること。

```bash
pwsh tools/run-tests.ps1
```

**K3-0 で Unity 側を触った場合のみ必要。** 触っていなければ不要（本スライスは基本 DebugStudio 側で閉じる）。Unity Editor を閉じてから実行すること。**テスト 0 件は失敗扱い。**

### 5.3 実地確認（手動）— **これを飛ばして完了としない**

| # | 確認 | 合格条件 |
|---|---|---|
| **U-1** | K3-0 の 5 フィールド | `_field_caps` に現れ、直近 run に値が入っている |
| **U-2** | K3-3 のクエリ 5 本 | すべて実 Elastic で結果を返す |
| **U-3** | import | `success: true` / `errors` なし |
| **U-4** | D1 の描画 | 1 run を選んで、fps / cpu / メモリの推移と span テーブルが値付きで出る |
| **U-5** | D2 の描画 | **2 run 以上**が横軸に並び、run メタ表に build / platform / device が出る |
| **U-6** | Editor 除外 | D2-2 が `platform` で Player に絞れる |

> **U-5 には run が 2 本以上必要。** Unity を 2 回以上起動して telemetry を流すこと。**1 run しか無い状態で「D2 が動いた」と報告しない。**

---

## 6. Phase C からの差し戻し / 実装側からの設計への異議

### 6.1 K3-0 Phase B（2026-08-11）— セッション属性 5 フィールド

#### 原因判定: **(c)**（前スライス実装前の古いデータしか Elastic に入っていなかった）

**(a) / (b) は現行コード上は繋がっている。** 根拠:

| 経路 | 根拠（ファイル:行） |
|---|---|
| **(a) Unity → Welcome** | `UnitySessionAttributes.Capture()` が `AbstractApplicationInitializer.cs:126`。Welcome 組み立てで 5 フィールド代入が `DebugSocketService.Inbound.cs:60-64`。wire key 9〜13 は Unity / Contracts 双方の `CapabilityHandshakeWelcomeEnvelopeV1.cs` で一致 |
| **(a) SessionId 一致** | Welcome の `session.SessionId` は `DebugSocketClientSession.cs:54` で `UnitySessionCorrelationContext.SessionId`。telemetry も `AppTelemetry.cs:305/389` で同値 |
| **(b) store** | `SessionMessageRouter.cs:98-99` が Welcome を `TelemetrySessionAttributesStore.ApplyWelcome` へ流す。lookup は `sessionId` キー（`TelemetrySessionAttributesStore.cs:66-76`）。「現在接続中」依存なし |
| **(b) mapper / `_bulk`** | `TelemetryRecordExportMapper.cs:96-100` が空文字を null 化。`ElasticBulkTelemetryNdjsonBuilder.CreatePayloadDictionary`（`:134-138`）が `AddIfPresent` でキー省略。index template に 5 keyword（`ElasticTelemetryIndexTemplateDefinition.cs:152-156`） |
| **(b) 配線** | `AppCompositionRoot.cs:114-128,150-157,210` で export / push / persistence / router が**同一** `TelemetrySessionAttributesStore` を共有 |

**(c) の根拠（このマシンの LocalAppData）:**

- `%LOCALAPPDATA%\DebugStudio\telemetry\` の全 `*.ndjson`（最新は `debugstudio-telemetry_2026-08-08_001.ndjson`、mtime **18:35**）に `buildVersion` / `platform` / `deviceModel` / `osVersion` / `engineVersion` は **0 件**
- 前スライス PR #14（session attributes）の merge は **2026-08-08 23:33**（`b21b044`）。つまり U-4 が読んだ L0 永続は**実装マージ前に書かれたファイル**
- foundation U-4 の「最新 doc 2026-08-08 / 5 フィールドが `_field_caps` に無い」は、この古い L0 を Filebeat / import した結果と整合する

#### 何を直したか

**本番コードの欠陥は見つからなかったため、本番コードは変更していない。**  
Filebeat が読む L0 rolling 経路で「Welcome 後の同一 `sessionId` なら 5 キーが出る / Welcome 前は出ない」を固定する回帰テストを `TelemetryPersistenceServiceTests` に追加した（既存テストは router と persistence で store が別インスタンスだった）。

#### 確認していないこと

- **Docker / Elasticsearch / Kibana はこの環境で起動できない。** `_field_caps` / 直近 run の curl 実測は**未了**
- Unity をこのブランチで起動して DebugStudio に接続し、新規 telemetry を流す実機確認も**未了**
- したがって HANDOFF §4 K3-0 の完了条件（Elastic 上に 5 フィールドが実在）は**このジョブでは満たしていない**。「コード上は (a)/(b) が繋がり、(c) が U-4 欠損の説明になる」までが到達点

#### 設計への異議（§0.5）

1. **K3-0 完了条件が「Elastic 実測」一択だと、Docker 不可の Phase B 作業ツリーでは原理的に完了不能になる。** 「(a)/(b) をコード＋単体テストで潰す」と「(c) 切り分け＋ curl 実測」を完了の二段に分け、後者を Phase C / 人間担当と明記した方が差し戻し理由が嘘にならない
2. **手順の「まず (c)」は正しいが、切り分けの最安は LocalAppData の rolling NDJSON の mtime と `buildVersion` 有無を見ること。** U-4 時点でそれを書いていれば、(c) は curl 前に確定できた
3. §1.1〜1.5 / パネル方針への異議は K3-0 範囲では無し（属性依存の前提自体は妥当。実データが無いだけ）

### 6.2 K3-1 Phase B（2026-08-11）— V7〜V10 + DeprecatedFieldCatalog

#### 何を直したか

- `DeprecatedFieldCatalog.cs` を新規追加し、deprecated 8 語の `HashSet` / `Regex` 二重管理を廃止。`QueryPattern` は語リストから生成
- `KibanaSavedObjectBundleValidator` に V7〜V10 を追加（公開入口 `Validate` は維持。分割せず 317 行で 380 行制限内）
- 単体テスト T1〜T5 を `KibanaSavedObjectBundleValidatorTests` に追加

#### 赤を見た記録

| テスト | 壊し方 | 結果 |
|---|---|---|
| T1 V7 | 実装前にテストだけ追加 | `Assert.Contains(V7)` 失敗、issues=`[]`（V3/V4 緑の穴を実測） |
| T2 V8 | 同上 | `Assert.Contains(V8)` 失敗、issues=`[]`（誤 type でも V5 緑） |
| T3 V9 | 同上 | `Assert.Contains(V9)` 失敗、issues=`[]` |
| T4 V10 | 同上 | `Assert.Contains(V10)` 失敗、issues=`[]`（sort 文字列でも検査スキップ） |
| T5 Catalog | 実装後に Catalog から `gpuTime` を一時削除 | columns / query 双方の `gpuTime` InlineData が失敗（Collection: `[]`）。復元後は 8 語×2 経路すべて緑 |

実装後: validator テスト 28 合格。全解 `dotnet test tools/DebugStudio/DebugStudio.sln` → **391 passed / 0 failed**（ベースライン 369 + 本ジョブで +20、K3-0 分含む）。

#### 確認していないこと

- Kibana UI / Elastic への import 実測（本ジョブ範囲外）
- 正本 NDJSON を人手で壊して V7〜V10 が本番バンドル経路でも赤になることの再確認（単体フィクスチャで赤は確認済み）

#### 設計への異議（§0.5）

1. **§3.3 の「Validate 本体が 400 行を超えたら分割」と JOB の「V7〜V10 で 380 行超えない」は閾値が食い違う。** 今回は 317 行で両条件を満たしたので分割しなかったが、K3-2 の V11 で確実に超える。分割タイミングは「K3-1 完了時点で先に切る」方が K3-2 の diff が読みやすい（今回は JOB 指示どおり未分割）
2. **V10 を「sort プロパティ欠如」も赤にするか**は仕様が曖昧。本実装は「無いか配列でない」を赤にした（文字列に戻る穴と、sort 検査ごと消える穴の両方）。欠如を許容するならテストとルール文を明示して欲しい
3. §1 / パネル方針への異議は K3-1 範囲では無し

### 6.3 K3-2 Phase B（2026-08-11）— V11 lens mapping + query 内フィールド検算

#### 何を直したか

- `IndexTemplateFieldMappingHelper`（テスト共有）を新設。`CollectMappedFieldPaths` は production に昇格せず、ここに切り出し（§3.5 P-5）
- lens `attributes.state` を再帰走査して `sourceField` / `field` の文字列値を集め、index template mapping と突き合わせる（V11・緩い実装。限界はヘルパの doc コメントに明記）
- saved search の `searchSourceJSON` query 内の `field:` 参照も同じ mapping 検算の対象に拡張（前スライスの申し送り）
- 公開入口 `KibanaSavedObjectBundleValidator.Validate` は未変更。V11 は template 照合のため `KibanaSavedObjectFieldMappingTests` 側（columns 検算と同系統）

#### 赤を見た記録

| テスト | 壊し方 | 結果 |
|---|---|---|
| T6 V11 | 合成 lens fixture に `sourceField=payload.doesNotExist` | unmapped として検出（`payload.doesNotExist`）。正本は lens 0 個で緑 |
| T7 query | 正本 `debugstudio-log-warnings` の query だけ `log.level` → `payload.doesNotExist` に一時書き換え | `saved search 'debugstudio-log-warnings' の searchSourceJSON query に index template へ mapping されていないフィールドがある: payload.doesNotExist`。復元後緑 |
| （旧穴） | 同上の query 破壊のまま columns 検算だけ実行 | **緑のまま**（query 未検査だった穴を実測） |

実装後: `dotnet test tools/DebugStudio/DebugStudio.sln` → **393 passed / 0 failed**（K3-1 時点 391 + T6/T7 で +2）。

#### 確認していないこと

- 実 Kibana から `_export` した本物の lens `state` での false negative / false positive（正本に lens が未だ 0。K3-4 後に再確認が要る）
- kuery 以外（Lucene / filter DSL / ES|QL 埋め込み）のフィールド参照
- lens の deprecated 8 語禁止（§1.6 の「lens も見る」は mapping とは別。本ジョブ範囲外）

#### 設計への異議（§0.5）

1. **K3-1 異議 1 の「V11 で Validate が 400 行超」は当たらない。** V11 は index template 照合なので `Validate` に載せず FieldMappingTests 側。Validate 分割は K3-1 時点の行数懸念としては解消
2. **lens → data view → template の対応付けは未実装。** 呼び出し側が mapping 集合を渡す。正本に lens が入ったら references の index-pattern で振り分ける必要がある（今は telemetry mapping を仮に使う／lens 0 個）
3. §1 / パネル方針への異議は K3-2 範囲では無し

### 6.4 Phase C 差し戻し（R1 / R2）対応（2026-08-11）

#### R2 — lens → data view → mapping 振り分け

- `IndexTemplateFieldMappingHelper.TryResolveMappedFieldPaths` を追加。`references` の `type=index-pattern` の id で照合先を選ぶ
  - `debugstudio-telemetry-dataview` → telemetry index template mapping
  - `debugstudio-log-dataview` → log index template mapping
  - **どちらでもない / index-pattern 参照が無い / 複数 data view** → 赤（失敗理由をメッセージに出す）。黙って通すと K3-4 で log 側 lens が telemetry mapping と照合されて検算が嘘になるため
- lens / saved search（columns・query）の検算をいずれもこの振り分けに統一。saved search は以前も正本 id ごとに正しい mapping を渡していたが、**references 起点ではなかった**ので同時に直した
- 合成 fixture: **赤を先に見てから緑**
  1. log data view + `payload.cpuMs`（telemetry 専用）→ unmapped で赤
  2. log data view + `log.level`（log 専用）→ 緑
  3. 正本（lens 0 個）→ 緑のまま

#### R1 — V10 の sort 欠如

**判断: 欠如も赤（現行を確定）。**

理由: V6 の sort 走査は `ValueKind==Array` のときだけ走る。`sort` が無いと V6 sort 検査が丸ごとスキップされ、V9（searchSourceJSON 必須）と同型の「器が無いと下流が死ぬ」穴が残る。T4 の文言は「文字列だと落ちる」だけだが、欠如を許容するとその穴が仕様として残る。正本は常に配列で `sort` を持つ。メッセージを「無い」/「配列ではない」に分け、欠如用テストを追加した。

#### 赤を見た記録 / 確認していないこと

| ケース | 結果 |
|---|---|
| log DV + `payload.cpuMs` lens | 赤（`payload.cpuMs` unmapped） |
| log DV + `log.level` lens | 緑 |
| search に `sort` 無し | V10 赤（`sort が無い`） |

確認していないこと: 実 Kibana `_export` lens の reference 名のばらつき、未知 data view id を正本に足したときの運用フロー（現状は赤で止める）。

実装後: `dotnet test tools/DebugStudio/DebugStudio.sln` → **394 passed / 0 failed**（直前 393 + sort 欠如テスト 1）。

### 6.5 Phase C 差し戻し 2 巡目（R3 / R4）対応（2026-08-11）

#### R3 — Lens sentinel `___records___` の除外

- `IndexTemplateFieldMappingHelper.LensMappingExcludedSentinels` に **`___records___` のみ**を列挙
- 理由: Lens の count（Count of records）が使う擬似フィールド（Kibana `DOCUMENT_FIELD_NAME`）。index mapping に存在しないが正当な state。除外しないと K3-4 で正当な count metric が false-positive 赤になる
- 「mapping に無いものを全部見逃す」方向には緩めていない。列挙 sentinel 以外は従来どおり赤
- 合成 fixture: `___records___` → 収集されず緑 / `payload.doesNotExist` → 引き続き赤

#### R4 — `KueryFieldPattern` の doc と実装の不一致

**選択: 照合前にダブルクォート文字列を除去し、doc（「引用符内は見ない」）どおりにする。**

理由: doc が意図していた挙動の方が正しい。値内の `"a:b"` をフィールド名と誤収集すると V11 query 検算が嘘の赤を出しうる。Regex 側を複雑にするより、照合前に引用符区間を空白へ置換する方が意図が追いやすい。単一引用符・高度なエスケープは対象外（緩いパーサの限界として残す）。

#### 赤を見た記録 / 確認していないこと

| ケース | 結果 |
|---|---|
| log DV + `___records___` lens | 緑（sentinel 除外） |
| log DV + `payload.doesNotExist` lens | 赤（unmapped） |
| log DV + `payload.cpuMs` lens | 赤（従来どおり） |

確認していないこと: 実 Kibana `_export` の count metric 以外に同種 sentinel が増えていないか、単一引用符 kuery の誤収集。

実装後: `dotnet test tools/DebugStudio/DebugStudio.sln` → **394 passed / 0 failed**（件数は直前と同じ。既存 Fact 内に fixture を追加）。

### 6.6 K3-0 gate 実測 + K3-3 Phase B（2026-08-11、Claude Code / Opus 5）

Docker / Elasticsearch / Kibana が起動した環境で、前スライスが「確認していないこと」に
積み残していた実測をすべて行った。

#### K3-0 gate: **通過**

§7.4 が「未達成」と書いていた U-1 を閉じた。実測出力は §7.6 に貼る。
§6.1 の原因判定 **(c)（実装前の古いデータしか入っていなかった）は実データで裏付けられた** —
新しく流した 2 run は 5 フィールドすべてに値を持ち、それより前の run は全て null。

#### 設計への異議（§0.5）

| # | 内容 |
|---|---|
| **D-1** | **§2.2 D2-2 と付録 A.3 の「AppStartup は run に 2 回（BeforeSceneLoad / AfterSceneLoad の 2 span）」は実データと違う。** 実測では **run に 1 span だけ**で、`payload.stage` は `AfterSceneLoad` のみ。`app-startup-per-run.esql` は `BY ... payload.stage` を残してあるので、BeforeSceneLoad 側が出るようになれば自動的に 2 行へ分かれる。**HANDOFF 本文の訂正が要る** |
| **D-2** | **§4 K3-3 の app-startup 草案にある `platform == "WindowsPlayer"` をクエリ正本に焼き込まなかった。** §1.2 の要件は「platform で Player に**絞れること**」であって「常に Player だけを出すこと」ではない。焼き込むと Editor しか無いデータセットで 0 行になり、**パネルが壊れているのか Player run が無いのかを区別できなくなる**。platform を出力列として残し、絞り込みはダッシュボードのコントロールに寄せた |
| **D-3** | **§2.2 D2-7 の分母は「run 長」を採った**（§2.2 が K3-3 で決めろと指示していた選択）。理由は 2 つ。(1) `ProfilerSummary` が 1 件も出ていないので件数を分母にすると常にゼロ除算になる (2) 分母が「たまたま有効になっている sample ストリーム」に依存するのは脆く、run 長は telemetry が 1 件でもあれば必ず定義できる。件数分母の利点（Unity 一時停止区間を除ける）は、その区間では分子の event も出ないため実質相殺される |
| **D-4** | **§2.2 D2-7 に `bottleneck` 列を足した**（草案は `gc` / `ui` のみ）。`GcSpike` / `UiCost` が 0 件なので、草案のままではパネルが常に全ゼロになる。`tags` に `Bottleneck` が付いた record 数は **ProfilerSummary 非依存で実データが動く**ため、D2-7 が今日から意味を持つ唯一の列になる |
| **D-5** | **§2.3 の「run をまたいだ percentile を取らない」に加えて、`n`（サンプル数）を出すことを `scene-load-per-run.esql` の要件にした。** 実測では `payload.targetIdentity` ごとの n がほとんど **1**。n=1 の p50 はその 1 件そのものであって代表値ではなく、n を出さないと読み手が p50 を過信する |

#### 見つかった、パネルを作る前に潰すべき問題

| # | 内容 |
|---|---|
| **B-1（gate 級）** | **`ProfilerSummary` / `GcSpike` / `UiCost` が Elastic にも L0 にも 1 件も無い。** §2 の 14 パネル中 **8 枚**（D1-1 / D1-2 / D1-3 / D1-5 / D2-4 / D2-5 / D2-6 / D2-7）がこれに依存する。原因は export ではなく **Unity が一度も emit していない**こと。詳細は §7.7 |
| **B-2** | **index 間の mapping 衝突。** `debugstudio-telemetry-2026.08.08` だけが index template 適用前の動的 mapping を持ち、`kind` / `payload.stage` / `payload.targetIdentity` / `payload.shape` が `text`。**`kind` はクエリごと 400 で落ち、`payload.*` はエラー無しで全行 null になる。** 後者が §0.5 の言う「仕様との照合では検出できない」型 |
| **B-3** | **`tags == "Bottleneck"` は静かに間違える。** ES\|QL は multivalue フィールドへの比較を null にするため、`["Bottleneck","NativeMemoryOver"]` の record が数から丸ごと落ちる。実測で **17 件が 8 件になった**。エラーが出ないので結果を見ても気づけない。`MV_CONTAINS` は 8.17 に存在しない |
| **B-4** | **`sessionId` が null の record を `BY sessionId` に流すと偽の run ができる。** 実測で **runSeconds = 48227 秒（13 時間超）** の存在しない run が 1 行でき、他の run（数十秒）が縦軸で潰れた。**セッション属性が null なのとは別問題**で、そちらは run が実在するので落としてはいけない |

#### 何を作ったか

`tools/DebugStudio/elastic/queries/` に `.esql` 5 本 + `README.md`。
**5 本すべて実 Elasticsearch 8.17 に投げて通ることを確認した**（出力は §7.6）。
B-2 / B-3 / B-4 は罠として `README.md` の表と各クエリのコメントに転記した
（実装側は 23k 行のドキュメントを読まない前提なので、クエリの隣に置く）。

#### 確認していないこと

- **`frame-cost-per-run.esql` は 0 行しか返していない。** 構文と列解決が通ることは確認したが、**値が入った状態での挙動は未確認**（B-1 が解けるまで確認できない）
- `.esql` は検算テストの対象外。V6 の deprecated 8 語も V11 の mapping 照合もかからない

---


## 7. Phase C レビュー

### 7.0 体制と収束（2026-08-11）

| Phase | 担い手 | 結果 |
|---|---|---|
| B 実装 | cursor-agent `cursor-grok-4.5-high`（3 ジョブ + 差し戻し 2 ジョブ） | K3-0 / K3-1 / K3-2 |
| C レビュー | cursor-agent `claude-opus-4-8-thinking-high`（`--plan --trust`、3 巡） | 1 巡目 APPROVE + R1/R2 → 2 巡目 APPROVE + R3/R4 → **3 巡目 APPROVE / 指摘なしで収束** |
| C 最終チェック | Claude Code (Opus 5) | 本節 §7.1〜§7.4 |

**本スライスの実施範囲は K3-0 / K3-1 / K3-2 のみ。** K3-3 / K3-4 / K3-5 は未着手（理由は §7.4）。

### 7.1 構造レビュー

`git diff develop..HEAD --stat`（HANDOFF を除く）:

```
DeprecatedFieldCatalog.cs              |  36 ++（新規）
KibanaSavedObjectBundleValidator.cs    | 181 +++---（318 → 321 行）
TelemetryPersistenceServiceTests.cs    |  93 ++
IndexTemplateFieldMappingHelper.cs     | 306 ++（新規・テスト側）
KibanaSavedObjectBundleValidatorTests  | 113 ++（171 → 276 行）
KibanaSavedObjectFieldMappingTests     | 275 ++（107 → 329 行）
```

> **訂正（C' 監査 §8.1 の指摘による）:** 初版はテスト 2 本の現在行数を「263 行 / 239 行」と書いていたが、これは `git diff` の挿入行数を現在行数と取り違えたもので、**実測は 276 行 / 329 行**。`wc -l` で確認して上表を訂正した。「自分で数えた」と書いた箇所で数えていなかったのは本節の失点。**production 側の 321 行（380 行制限内）は実測値で、こちらは誤っていない。**

- **50% 以上増えたファイルは production に無い。** `KibanaSavedObjectBundleValidator.cs` は 318 → **321 行**で §3.3 の 380 行制限内。V6〜V10 の追加分は `DeprecatedFieldCatalog` 抽出による削減と相殺されている。§3.3 の「`Validate` が 400 行を超えたら分割」は発火せず、分割不要は妥当
- **増えたのはテスト側**（新規ヘルパ 306 行 + 既存テスト 2 本に +327 行 = 計 +633 行）。責務は「mapping パス収集」「data view 振り分け」「lens/kuery のフィールド抽出」の 3 つで、いずれも純関数として単体で呼べる形になっている。**テストが書けないロジックは無い**
- **§3.5 P-5 は守られている。** `CollectMappedFieldPaths` は `internal static` のテスト側ヘルパのままで production に昇格していない
- **V7 と V4 は別ルールとして分離**（`ValidateV3V4AndV7` 内で `RuleId` が分かれ、V7 は `continue` して V4 の 1:1 判定に流さない）。どちらで落ちたか区別できる
- **`DeprecatedFieldCatalog` の Regex は語リストから生成**（`string.Join("|", Fields.Select(Regex.Escape))`）。二重管理は解消。lookbehind `(?<![.\w])` により `payload.cameraTotalViewCount` は誤検出しない

### 7.2 実測した数字（**自分で実行した**）

```
dotnet test tools/DebugStudio/DebugStudio.sln
→ 失敗: 0、合格: 394
   (Contracts 37 / Export 107 / Server 10 / Cli 7 / App 233)
```

**ベースライン 369 → 394（+25）。減少なし。** §6 の自己申告 394 と一致する。

**Phase C レビュー（Opus 4.8）は 3 巡とも `dotnet test` を実行していない**（`--plan` がシェルを拒否するため）。3 巡分のレビューは全て「HANDOFF の申告値を前提とした」判定であり、**数字を実測したのはこの最終チェックが初めて**である。

### 7.3 最終チェックで新たに出た指摘

| # | 重大度 | 内容 |
|---|---|---|
| **F1** | **中（Phase A = HANDOFF の失点）** | **§4 K3-1 の「`Elastic/Kibana/` の 6 ファイルには `System.IO` / `File.` / `GetManifestResourceStream` が 1 つも無い（C' 監査が裏付け済み）」は事実誤り。** 実際には `ElasticKibanaSavedObjectsWriter.cs`（`GetManifestResourceStream` / `File.WriteAllTextAsync`）と `ElasticKibanaImportCommandWriter.cs`（`File.WriteAllTextAsync`）が IO を持つ。**守るべき不変条件は「検算経路（parser / validator / catalog / model）に IO を持ち込まない」**であり、それは今回も守られている。**問題は、Opus 4.8 が 1 巡目で「`Elastic/Kibana/` 配下は IO 無し」を*良い点*として追認したこと** — 仕様の文言をそのまま検証結果として書いた。§0.5 が警告した「仕様との照合をいくら重ねても仕様の誤りは検出できない」が、この HANDOFF 自身で再発した。**次スライスで §4 K3-1 の文言を訂正すること** |
| **F2** | 低〜中（本スライスが作った潜在穴） | `IndexTemplateFieldMappingHelper.StripDoubleQuotedSegments` は kuery のダブルクォート区間を空白に置換してから field を拾う。**KQL はフィールド名自体の引用（`"log.level": warn`）を許すため、引用符付きフィールド名は照合対象から静かに落ちる。** 現正本の kuery は `log.level: ("warning" or ...)` で値だけが引用されているため無害だが、**「検算が静かに消える」型の穴**で、本スライスが塞ごうとしたものと同型。doc の限界列挙にもこの項目が無い。修正案: 引用符区間を除去する前に `"field":` 形を先に拾う、または最低限 doc の限界に明記する |
| **F3** | ~~低~~ → **中**（C' 監査 §8.1 の指摘を受けて格上げ。安全網が*誤って塞ぐ*方向のリスクであり、K3-4 着手前に方針決定が要る） | `TryResolveMappedFieldPaths` は index-pattern 参照が **複数あると赤**にする。**K3-4 で annotation layer / reference line を持つ Lens は複数 data view を参照しうる**ため、正当なパネルが赤になる可能性がある。現在 lens 0 個なので無害。K3-4 着手時に実 `_export` で確認すること |
| **F4** | 低 | 既存テスト名 `正本NDJSONはV1からV6で指摘0件である` が**古い**。中身は `KibanaSavedObjectBundleValidator.Validate` を丸ごと呼ぶため、**実際には V1〜V10 を正本に対して強制している**（§5.1 T8 は実質達成）。名前だけ V6 で止まっており、読み手が「V7〜V10 は正本にかかっていない」と誤解する。改名するだけ |

**§5.1 の T9（正本のダッシュボードは 2 枚あり、各パネルが `references` と 1:1）は未実装。** 正本は現在 dashboard 1 枚 / lens 0 個 / 5 オブジェクトで、2 枚目は K3-4 の成果物であるため**本スライスでは原理的に書けない**。K3-4 と同時に入れること。

### 7.4 確認していないこと（**ここが本スライスの限界**）

- **Elastic / Kibana の実測は一切していない。** この環境では Docker が停止しており、`docker compose up -d` を回していない。したがって:
  - **U-1（`_field_caps` に 5 フィールドが現れる）は未達成。K3-0 は gate の完了条件を満たしていない**
  - U-2 / U-3 / U-4 / U-5 / U-6 も全て未実施
- **K3-0 の原因判定 (c) は状況証拠にとどまる。** 根拠は「このマシンの `%LOCALAPPDATA%\DebugStudio\telemetry\` の最新 rolling NDJSON（mtime 2026-08-08 18:35）に 5 キーが 0 件で、session attributes を入れた PR #14 の merge が同日 23:33」というもの。**Elastic 上で新規 run を流して 5 フィールドが出ることは確認していない。** 「コードは繋がっているから入るはず」で完了扱いにしていない点は §6.1 に明記されている
- **Unity 側のコードは 1 行も変更していない**（`git diff` に `unity/` 配下が 1 件も無い）。したがって `record` 型の混入や `?.` / `??` による偽 null チェックのリスクは本スライスでは発生していない。`pwsh tools/run-tests.ps1` も不要（§5.2 の条件どおり）
- **K3-3 / K3-4 / K3-5 は未着手。** K3-3 は実 Elastic での実行が完了条件、K3-4 は Kibana UI 操作が必要（§4 の担い手欄が「要 Kibana UI」）、K3-5 は K3-4 の成果物に依存する。**環境が無いまま「クエリは書いたが未検証」を commit すると、§1.3 が禁止した「クエリが通らないうちにパネルを作らない」の逆をやることになるため、着手しない判断をした**
- **正本 NDJSON は 1 byte も変更していない。** パネルは 1 枚も増えていない。**本スライスの成果は「中身を入れる前の安全網」までであり、§0 が掲げた Q1 / Q2 にはまだ 1 つも答えられない**

---

### 7.5 PR #16 レビュー（cursor[bot]）への対応（2026-08-11）

判定は **Approve / ブロッカーなし**。新規指摘 N1〜N3 のうち、**挙動を変えない小修正と、誤検知を消す 1 件をマージ前に取り込んだ。**

| ID | 対応 |
|---|---|
| **N1**（低〜中・**修正済み**） | **V6 の query 走査が引用符を剥がしていなかったため、`message: "cpuTime is high"` が誤検知で赤になっていた。** `DeprecatedFieldCatalog.TryFindInQuery` を追加し、照合前にダブルクォート区間を処理する。**直後が `:` の区間はフィールド名として中身を残し、それ以外は値として空白に落とす**ため、`"cpuTime": 10`（引用符付きフィールド名）は引き続き赤になる。テスト `queryの引用符内の値はV6に落ちず引用符付きフィールド名は落ちる` を追加し、**修正を外すと `Assert.DoesNotContain() Failure` で赤になることを実測した** |
| **F1**（中・**修正済み**） | §4 K3-1 の「`Elastic/Kibana/` の 6 ファイルには IO が 1 つも無い」を訂正した（同節の訂正ブロックを参照）。**§7 / §8 だけで訂正すると本文が残って次のレビュアーが再誤認する**という指摘に従い、本文側を直した |
| **F4**（低・**修正済み**） | テスト名を `正本NDJSONはV1からV10で指摘0件である` に改名し、「`Validate` を丸呼びするのでルールが増えれば自動的に正本へ強制される / V11 は index template を要するため別テスト」を doc コメントに書いた |
| **F2 / F3 / N2 / N3** | **次スライス送り。** F2（引用符付きフィールド名が mapping 検算から落ちる）は検算ヘルパ側の同型の穴、F3（複数 index-pattern で赤）は K3-4 の annotation layer 着手前に方針決定が要る、N2（`TryResolve` の失敗系にテストが無い）、N3（`searchSourceJSON.filter` 内フィールドが未走査） |

**`dotnet test` = 395 passed / 0 failed**（+1 = N1 のテスト）。

> **N1 は、私（Phase C 最終チェック）も Opus 4.8（3 巡）も見落としていた。** F2 として「引用符付きフィールド名が*落ちる*」方向は指摘できていたのに、**同じ引用符の扱いが V6 側では*逆方向の誤検知*を生んでいることに気づかなかった。** 非対称性を疑う視点が抜けていた。

---

### 7.6 K3-0 / K3-3 の実測出力（2026-08-11、Claude Code / Opus 5）

環境: Elasticsearch / Kibana 8.17.0（`docker compose`、`xpack.security.enabled=false`）。

#### U-1 — `_field_caps` に 5 フィールドが現れる: **合格**

```
$ curl -s "http://localhost:9200/debugstudio-telemetry-*/_field_caps?fields=buildVersion,platform,deviceModel,osVersion,engineVersion"

{"indices":["debugstudio-telemetry-2026.07.18","debugstudio-telemetry-2026.07.19",
"debugstudio-telemetry-2026.07.26","debugstudio-telemetry-2026.08.08","debugstudio-telemetry-2026.08.11"],
"fields":{
 "engineVersion":{"keyword":{"type":"keyword","metadata_field":false,"searchable":true,"aggregatable":true}},
 "buildVersion" :{"keyword":{"type":"keyword","metadata_field":false,"searchable":true,"aggregatable":true}},
 "osVersion"    :{"keyword":{"type":"keyword","metadata_field":false,"searchable":true,"aggregatable":true}},
 "deviceModel"  :{"keyword":{"type":"keyword","metadata_field":false,"searchable":true,"aggregatable":true}},
 "platform"     :{"keyword":{"type":"keyword","metadata_field":false,"searchable":true,"aggregatable":true}}}}
```

直近 run の document に**値が入っている**こと:

```
$ curl -s "http://localhost:9200/debugstudio-telemetry-*/_search?size=1&sort=@timestamp:desc"

"_index":"debugstudio-telemetry-2026.08.11",
"_source":{
  "platform"     :"WindowsEditor",
  "osVersion"    :"Windows 11  (10.0.26200)",
  "engineVersion":"6000.5.0f1",
  "buildVersion" :"0.1.0",
  "deviceModel"  :"FRONTIER (Inversenet Inc.)",
  "kind":"sample","name":"CameraSystemSnapshot",
  "sessionId":"3c943cbb2fbb4f25b1c9f69c7f06139a",
  "@timestamp":"2026-08-11T09:15:40.299Z"}
```

`exists` 件数は **220 件**で、これは 2026-08-11 の新規 2 run（137 + 83）と**完全に一致**する。
それより前の run は 5 フィールドすべてが null。

> **§6.1 の原因判定 (c) は実データで裏付けられた。** 状況証拠（LocalAppData の mtime と
> PR #14 の merge 時刻）だけでなく、Elastic 上で「実装後に流した run にだけ属性が付く」ことを確認した。
> **本番コードの変更は不要だった**という §6.1 の結論も維持される。
>
> なお §6.1 が「handshake より前に届いた telemetry には属性が付かない」を既知の制約として
> 挙げていたが、**この 2 run では該当 record が 0 件**だった（220 件が run の全 record と一致）。
> 制約が消えたわけではなく、この 2 run でたまたま踏まなかっただけとして扱う。

#### U-2 — クエリ 5 本が実 Elastic で結果を返す: **合格**

`runs.esql`（D2-1）— 20 行。新旧の run が属性の有無で並ぶ:

```
        started         |     docs      |           sessionId            | buildVersion | platform      | deviceModel              | runSeconds
2026-08-11T09:14:21.858Z|137            |3c943cbb2fbb4f25b1c9f69c7f06139a|0.1.0         |WindowsEditor  |FRONTIER (Inversenet Inc.)|78
2026-08-11T09:07:31.032Z| 83            |26dfe7fba8764df4b5587a01b52da073|0.1.0         |WindowsEditor  |FRONTIER (Inversenet Inc.)|58
2026-08-08T09:34:36.098Z| 41            |ace2b4c0a3024ad2b28f7d7f2cfe8614|null          |null           |null                      |26
2026-08-08T09:22:29.166Z| 55            |7452d637a4334142821e10fbe2ba1f93|null          |null           |null                      |32
（以下 2026-07-26 / 07-19 の 15 run、すべて属性 null）
```

`app-startup-per-run.esql`（D2-2）— 4 行:

```
      ms       |        started         |           sessionId            |   platform    | payload.stage
1851.6362      |2026-08-08T09:22:29.174Z|7452d637a4334142821e10fbe2ba1f93|null           |AfterSceneLoad
1258.8166      |2026-08-08T09:34:36.164Z|ace2b4c0a3024ad2b28f7d7f2cfe8614|null           |AfterSceneLoad
5544.8431      |2026-08-11T09:07:31.041Z|26dfe7fba8764df4b5587a01b52da073|WindowsEditor  |AfterSceneLoad
2055.6453      |2026-08-11T09:14:21.870Z|3c943cbb2fbb4f25b1c9f69c7f06139a|WindowsEditor  |AfterSceneLoad
```

**どの run も AppStartup は 1 span しか無く、stage は `AfterSceneLoad` のみ**（§6.6 D-1）。

> 上 2 行の `payload.stage` は、mapping 衝突があった時点では**エラー無しで null になっていた**。
> index を作り直したら値が出た。B-2 の「静かな壊れ方」の直接の証拠。

`scene-load-per-run.esql`（D2-3）— 42 行:

```
       p50        |       n       |           sessionId            |payload.targetIdentity
3269.2928         |1              |26dfe7fba8764df4b5587a01b52da073|Title
1666.0206         |1              |26dfe7fba8764df4b5587a01b52da073|InGameSession
1693.424          |1              |26dfe7fba8764df4b5587a01b52da073|HomeScene
 222.4247         |5              |3c943cbb2fbb4f25b1c9f69c7f06139a|Environment_1_0
 288.0494999999999|2              |3c943cbb2fbb4f25b1c9f69c7f06139a|Cell_2_3
（以下 37 行）
```

**n がほとんど 1**。§6.6 D-5 の根拠。

`frame-cost-per-run.esql`（D2-4/5/6）— **0 行**（列は解決する。構文エラーではない）:

```
    cpuP95     |    fpsP05     |  managedMax   |   nativeMax   |    samples    |    started    |   sessionId
---------------+---------------+---------------+---------------+---------------+---------------+---------------
（行なし）
```

`event-rate-per-run.esql`（D2-7）— 19 行:

```
      gc       |      ui       |  bottleneck   |           sessionId            |  runSeconds   | bottleneckPerMin
0              |0              |9              |3c943cbb2fbb4f25b1c9f69c7f06139a|78             |6.923076923076923
0              |0              |8              |26dfe7fba8764df4b5587a01b52da073|58             |8.275862068965518
0              |0              |5              |ace2b4c0a3024ad2b28f7d7f2cfe8614|26             |11.538461538461538
0              |0              |7              |7452d637a4334142821e10fbe2ba1f93|32             |13.125
（以下 15 run）
```

`gc` / `ui` は全 run で 0（§6.6 B-1）。`bottleneck` は実データが動く（§6.6 D-4）。

#### B-3 の実測（`tags == "Bottleneck"` が静かに間違える）

telemetry 全 709 件（`sessionId` 非 null）に対する 3 つの数え方:

```
| 数え方                                                    | 件数 |
| terms 集計（STATS n = COUNT(*) BY tags の "Bottleneck"）  |  57  |
| CONCAT("|",MV_CONCAT(tags,"|"),"|") LIKE "*|Bottleneck|*" |  57  |  ← 正しい
| tags == "Bottleneck"                                      |  30  |  ← 27 件落ちる
```

落ちた 27 件は `tags` が `["Bottleneck","NativeMemoryOver"]` の record
（terms 集計の `NativeMemoryOver` が 27 件で一致する）。
**エラーも警告も出ず件数だけが減る**ため、結果を読んでも気づけない。

#### B-2 の実測（index 間 mapping 衝突）

```
$ curl -s "http://localhost:9200/debugstudio-telemetry-*/_field_caps?fields=kind,payload.stage,payload.targetIdentity,payload.shape,payload.cameraTotalViewCount"

CONFLICT kind                        {'keyword': [...2026.08.11], 'text': [...2026.08.08]}
CONFLICT payload.shape               {'keyword': [...2026.08.11], 'text': [...2026.08.08]}
CONFLICT payload.stage               {'keyword': [...2026.08.11], 'text': [...2026.08.08]}
CONFLICT payload.targetIdentity      {'keyword': [...2026.08.11], 'text': [...2026.08.08]}
CONFLICT payload.cameraTotalViewCount{'long'   : [...2026.08.08], 'integer': [...2026.08.11]}
```

**壊れ方が 2 種類あり、片方は静かである:**

| フィールド | 症状 |
|---|---|
| `kind` | `verification_exception` / 400。**クエリごと落ちるので気づける** |
| `payload.stage` / `payload.targetIdentity` | **エラー無しで全行 null。** 気づけない |

原因は `debugstudio-telemetry-2026.08.08` **1 本だけ**が index template 適用前の
動的 mapping で作られていること。他の index は無害。

**対処: index を作り直した**（データは破棄可という判断を人間から得た）。
telemetry / log の index を全削除 → Filebeat の registry ボリュームを破棄 → 再作成し、
L0 の `.ndjson` を先頭から現行 template 準拠の index へ再投入した。
クエリ側に回避コード（`kind::keyword` のキャスト等）を入れない方針を採った。
**Kibana の data view でも conflict 型は Lens で集計不能になるため、K3-4 のためにも
データ側で直す必要がある。**

再投入後の実測:

```
$ curl -s ".../_field_caps?fields=kind,payload.stage,payload.targetIdentity,payload.shape,payload.cameraTotalViewCount,buildVersion,platform,deviceModel,osVersion,engineVersion"
ok  kind ['keyword']            ok  payload.stage ['keyword']
ok  payload.shape ['keyword']   ok  payload.targetIdentity ['keyword']
ok  payload.cameraTotalViewCount ['integer']
ok  buildVersion / platform / deviceModel / osVersion / engineVersion ['keyword']
--- conflicts: 0
```

**`.esql` 5 本すべてを、再投入後の `debugstudio-telemetry-*`（wildcard）に対して通し直した。**
上に貼った出力はすべて再投入後の値。

> **ES はワイルドカードでの index 削除を拒否する**（`action.destructive_requires_name` が既定で true）。
> `_cat/indices` で名前を取ってから明示的に列挙して DELETE する必要がある。
>
> **Windows PowerShell 5.1 では `&&` が使えず、`curl` は `Invoke-WebRequest` の別名**なので
> `-s -X DELETE` が通らない。手順を人に渡すときは `Invoke-RestMethod` で書くか pwsh 7 を指定すること。

#### B-5（作り直して初めて分かった）— 旧 index は二重投入されていた

再投入後の telemetry 件数は **770 件で、L0 の `.ndjson` の総行数と完全に一致**した。
作り直す前は **1516 件**あった。

| L0 ファイル | 行数 | 旧 index | 再投入後 |
|---|---|---|---|
| `2026-07-19_001` | 128 | 277 | 85 |
| `2026-07-26_001` + `2026-07-27_001` | 164 + 162 | 652 | 326 |
| `2026-08-08_001` | 96 | 192 | 96 |
| `2026-08-11_001` | 220 | 220 | 220 |

**最新の 2026-08-11 だけは重複していない**ため、§7.6 の K3-0 gate の根拠（220 件 = 137 + 83）は
影響を受けない。一方 `runs.esql` の `docs` 列と `event-rate-per-run.esql` の
`bottleneck` 列は旧データで**約 2 倍に膨らんでいた**ので、上の出力は再投入後の値に貼り替えてある。

> **これも「エラーを出さずに数字だけ嘘になる」型である。** 件数が 2 倍でもクエリは正常に返る。
> 気づけたのは「L0 の行数」という**外部の基準**と突き合わせたからで、
> Elastic の中だけを見ていても永久に分からない。
> **ダッシュボードの数字を信じる前に、L0 の行数と一致するかを一度は確認すること。**

---

### 7.7 新しい gate: `ProfilerSummary` が emit されていない（**次スライスへの申し送り**）

#### 事実（実測）

```
$ FROM debugstudio-telemetry-* | STATS n = COUNT(*) BY name, kind
CameraSystemSnapshot 1074 | SceneLoad 275 | SceneUnload 70 | SceneTransition 44
AppStartup 35 | CameraSwitch 18
→ ProfilerSummary / GcSpike / UiCost は 0 件
```

**L0 の rolling NDJSON にも 0 件**なので、export / Filebeat / Elastic のいずれの問題でもない。
Unity が一度も emit していない。

```
$ %LOCALAPPDATA%\DebugStudio\telemetry\debugstudio-telemetry_2026-08-11_001.ndjson （220 行）
('sample','CameraSystemSnapshot') 129 / ('span','SceneLoad') 57 / ('span','SceneUnload') 26
('span','SceneTransition') 4 / ('span','AppStartup') 2 / ('span','CameraSwitch') 2
```

#### 原因

発生源は `DebugProfilerView.Update()` → `LogSummary()`（1 秒ごと）だが、
**`DebugProfilerView` はプロジェクト全体で呼び出し側が 1 つも無い。**

```
$ grep -rn "DebugProfilerView" unity/Assets --include=*.cs
→ 定義（DebugProfilerView.cs）と AppTelemetry.cs のコメント以外に参照なし
$ grep -rln "DebugProfilerView" unity/Assets --include=*.prefab --include=*.unity --include=*.asset
→ 0 件（prefab / シーンにも置かれていない）
```

`UIView` なので Debug レイヤーに積まれない限り `Update()` が回らない。

#### なぜ本スライスで直さなかったか

`UIView` を画面に出す唯一の経路は `UICommon.AddUIView(ownerId, view, ct)` で、
**呼び出し側は `SceneDirector.Loading` ただ 1 つ、`ownerId` は必ずシーン識別子**
（`RemoveUIView(ownerId)` も同じキーで引く）。

つまり `DebugProfilerView` のようなアプリ常駐 View を載せるには
**「シーンが所有しない UIView の寿命を誰が持つか」という所有権の設計判断**が要る。
これは OSM の中核契約（寿命スコープ）に触るので、Kibana ダッシュボードのスライスで
黙って決めてよい話ではない。**別スライスとして切る。**

#### 影響範囲

| 状態 | パネル |
|---|---|
| **作れる**（実データあり） | D1-4 重い span / D1-6 異常タグ内訳 / D1-7 warning ログ / D2-1 run メタ表 / D2-2 AppStartup / D2-3 SceneLoad |
| **作れない**（ProfilerSummary 依存） | D1-1 CPU/GPU 推移 / D1-2 fps 推移 / D1-3 メモリ推移 / D1-5 イベント発生点 / D2-4 CPU p95 / D2-5 fps p05 / D2-6 メモリ最大 / D2-7 の `gc` `ui` 列 |

**`.esql` は 5 本すべて書いて構文検証した**（§1.3 の「何を作りたかったかは残す」）が、
**0 行のクエリからパネルは作らない。**

> **これは K3-0 と同型の gate である。** 「フィールドが Elastic に無い」を潰したら、
> 次は「そのフィールドを持つ record 自体が生成されていない」が出た。
> §1.5 の gate 条件は「フィールドが `_field_caps` に現れること」だったが、
> **それは「パネルに値が出ること」を保証しない。** 次スライスの gate 条件は
> 「**そのパネルが参照する record が、直近 run に 1 件以上ある**」にすべきである。

---

### 7.8 K3-4 の着手で判明した — **V4 / V7 は実 `_export` を受け付けられない**

#### やったこと

Kibana UI で `runs.esql`（D2-1）を **ES|QL パネル**として 1 枚組み、dashboard を保存して
`_export` し、**その NDJSON を正本に差し替えて検算テストを実行した**（赤を先に見る手順）。

#### 結果: **赤 3 件**

```
$ dotnet test ... --filter "FullyQualifiedName~Kibana"
失敗: 3、合格: 41

正本に検算指摘がある:
行 5 (id='debugstudio-overview-dashboard'): V7 — panelsJSON[2] に非空の panelRefName が無い。
行 5 (id='debugstudio-overview-dashboard'): V4 — panelsJSON の panelRefName 'panel_p1' に対応する references が無い。
行 5 (id='debugstudio-overview-dashboard'): V4 — panelsJSON の panelRefName 'panel_p2' に対応する references が無い。
```

**正本は元に戻して緑（44 合格）に復帰済み。** 正本 NDJSON は 1 byte も変更していない。

#### 原因は 2 つあり、どちらも「仕様が実物と違う」

| # | 内容 |
|---|---|
| **G-1** | **Kibana 8.17 の `_export` は panel の reference 名を `p1:panel_p1` の形で出す**（`<panelIndex>:` 接頭辞が付く）。V4 は `reference.Name.StartsWith("panel_")` で絞っているので**1 件も拾えず**、既存の saved search パネル 2 枚が両方とも「references が無い」で赤になる。**手書きの正本（`panel_p1`）では通り、実 `_export` では落ちる** |
| **G-2** | **ES\|QL パネルは by-value で、`panelRefName` を持たない。** 内容は dashboard の `panelsJSON[].embeddableConfig.attributes` に丸ごと埋まる。V7 は「全パネルが非空の `panelRefName` を持つ」なので必ず赤になる |

#### これは §0.5 が警告した型そのもの

**V4 / V7 は手書きのフィクスチャに対してだけ検証されていた。** §1.4 は
「Kibana UI で組んで `_export` したものだけを正本にする」と決めているのに、
**その `_export` を安全網が受け付けられない。** 仕様（V ルール）と仕様（§1.4）が矛盾しており、
K3-1 / K3-2 のレビュー 3 巡 + C' 監査 + PR レビューはいずれもこれを検出していない。
**実物の `_export` を一度も通していなかったため。**

#### by-value ES|QL パネルの実物（`_export` から抜粋）

```
type: lens | panelIndex: e0ce0605-… | panelRefName: None
  embeddableConfig.attributes.references: []          ← index-pattern 参照が無い
  embeddableConfig.attributes.state.query:
    {"esql": "FROM debugstudio-telemetry-* | WHERE sessionId IS NOT NULL | STATS ... | LIMIT 20"}
```

**F3（複数 index-pattern で赤）より深刻な形で当たった。** F3 は「複数あると赤」を心配していたが、
実際に出てくる ES|QL パネルは **index-pattern 参照が 1 つも無い**。
`TryResolveMappedFieldPaths` は参照が無い場合も赤にするので、こちらでも落ちる
（今回は `type=lens` の saved object が生成されないため V11 まで到達していないが、
by-reference で作れば必ず当たる）。

一方で **by-value ES|QL パネルには大きな利点がある**:

- **`git diff` が読める。** 埋まっているのは ES|QL のクエリ文字列そのもので、
  §1.4 が「読めないので手書き禁止」とした巨大な Lens state ではない
- **`queries/` の正本と 1 対 1 で対応する。** §1.3 が狙った「パネルの意図をバージョンから独立させる」が、
  パネル自体で達成される
- **クエリは実 Elastic で検証済み**（§7.6）

#### 決めてもらう必要があること（**K3-4 の続行はここで止めた**）

| 案 | 内容 | 代償 |
|---|---|---|
| **A（推奨）** | **by-value ES\|QL パネルで組み、V4 / V7 を実 `_export` に合わせて直す。** V4 は reference 名の `<panelIndex>:` 接頭辞を許容する。V7 は「`panelRefName` を持つ **か** `embeddableConfig.attributes` を持つ」＝「パネルの中身が解決できる」に緩める | DebugStudio 側の C# + テスト作業が発生する（K3-2 の安全網の修正）。ES\|QL パネルは V11 の mapping 検算の対象外になるため、**代わりに「`FROM` が既知の index パターンを指す」等の別ルールが要る** |
| **B** | **classic（data view ベース）の Lens を by-reference で組む。** V4 の接頭辞問題だけ直せば V7 / V11 はそのまま効く | Lens の drag-and-drop を UI 操作で組む必要があり、この環境では **Monaco / Lens への修飾キー入力が届かない**（下記）ため実行可能性が低い。`git diff` も読めなくなる |

> **この環境の UI 操作の制約（実測）:** Kibana の ES|QL エディタ（Monaco）に対し、
> `type` は届くが **`Ctrl+A` / `Backspace` / `Ctrl+Shift+Home` などのキー入力が一切届かない**。
> テキストの置換は `triple_click` で行選択してから `type` する方法でのみ成功した。
> Lens の drag-and-drop はこれより難度が高い。

**A を採る場合、V4 / V7 の修正は K3-4 の一部ではなく K3-2 の差し戻しとして扱うのが正しい**
（安全網は K3-2 の成果物であり、K3-4 はそれを使う側）。

---

## 8. Phase C' 監査

### 8.1 JOB C' 監査（2026-08-11）

**限界:** 本監査は Phase B 実装者（cursor-grok-4.5-high）による自己採点であり、CLAUDE.md が求める「実装・設計に関与していないモデル」の条件を満たさない。

#### §7 の主張 — 再現できたもの / できなかったもの

| §7 の主張 | 本監査の実測 |
|---|---|
| `dotnet test` 394 / 0 failed（Contracts 37 / Export 107 / Server 10 / Cli 7 / App 233） | **再現。** 失敗 0、合格 394。内訳一致 |
| `KibanaSavedObjectBundleValidator.cs` が 321 行 | **再現。** `wc -l` = 321 |
| `CollectMappedFieldPaths` が production に昇格していない | **再現。** 定義は `tests/.../IndexTemplateFieldMappingHelper.cs` のみ。`src/` に無し |
| V7 と V4 が別ルールとして分離 | **再現。** `ValidateV3V4AndV7` 内で V7 は `RuleId="V7"` + `continue`、V4 は別 `RuleId` |
| **F1**: Writer 2 本が IO を持ち、§4 K3-1「6 ファイルに IO 無し」は事実誤り | **再現（事実誤りは本当）。** `ElasticKibanaSavedObjectsWriter`（`GetManifestResourceStream` / `File.WriteAllTextAsync`）と `ElasticKibanaImportCommandWriter`（`File.WriteAllTextAsync`）が IO 保有。検算経路（parser / validator / catalog / model 系）には IO 無し。なお配下は現在 **9 ファイル**（「6」も既に陳腐） |
| **F2**: `"log.level": warn` が `StripDoubleQuotedSegments` で照合から落ちる | **再現。** reflection で `CollectKueryFieldReferences` 実行 → フィールド **0 件**。対して `log.level: warn` と `log.level: ("warning" or ...)` は `log.level` を拾う |
| **F4**: テスト名は V6 だが中身は V1〜V10 | **再現。** `正本NDJSONはV1からV6で指摘0件である` は `Validate` 丸呼び（V1〜V10）。V11 は別テスト |

**再現できなかった（§7 側の誤記）:**

- §7.1 の行数: FieldMappingTests を「107 → **239**」と書いたが実測は **329** 行。ValidatorTests を「171 → **263**」と書いたが実測は **276** 行。diff の増分行と現在行数を混同した可能性が高い。「自分で数えた」主張と食い違う

#### §7 が見落としていた問題

- **F3 の格付けが K3-4 向けに甘い。** `TryResolveMappedFieldPaths` は index-pattern 参照が複数だとオブジェクトごと赤。annotation layer / reference line を持つ本物の Lens `_export` では正当パネルが落ちうる。§7 は「低」だが、次スライスで安全網が**誤って塞ぐ**方向のリスクなので **中（K3-4 着手前に方針決定が必要）** が妥当。F1 / F2 / F4 の格付け自体は過大でも過小でもない（F2 は現正本では無害、同型の静かな穴である点の指摘は正しい）
- **それ以外の重大な見落としは無し。** §7.4「Elastic 未実測 / K3-3〜5 未着手 / 正本未変更」は正直（本環境も `docker info` 失敗）。「確認できたのに諦めた」箇所は行数誤記程度

#### 本監査が確認していないこと

- Elastic `_field_caps` / 実 run 投入、Kibana UI、本物の `_export` NDJSON に対する V11 の当たり具合
- Phase C（Opus 4.8）が本当に `dotnet test` 未実行だったか（メタ主張）
- Unity 配下・`pwsh tools/run-tests.ps1`

---

## 付録 A. 使えるフィールド一覧（自己完結のため転記）

**実装側は 23k 行のドキュメントを読まない前提。以下がすべて。**

### A.1 telemetry index（`debugstudio-telemetry-*`）

| フィールド | 型 | 内容 |
|---|---|---|
| `@timestamp` | date | 時刻（data view の time field） |
| `kind` | keyword | **`span` / `sample` / `event`。フィルタの主キー** |
| `name` | keyword | `AppStartup` / `SceneTransition` / `SceneLoad` / `SceneUnload` / `ProfilerSummary` / `GcSpike` / `UiCost` / `CameraSystemSnapshot` / `CameraSwitch` |
| `elapsedMs` | double | 所要時間。**`kind=span` のみ意味を持つ**（sample では出力されない） |
| `isSuccess` | boolean | span の成否 |
| `tags` | keyword[] | `Bottleneck` / `CpuTimeOver` / `GpuTimeOver` / `ManagedMemoryOver` / `NativeMemoryOver` / `FrameRateDrop` / `AllocSpike` / `InputLatency` / `NetworkIssue` / `FatalError` |
| `sessionId` | keyword | **Unity 起動単位。run の識別子** |
| `traceId` / `spanId` / `parentSpanId` | long | 相関。**span は入れ子になる** |
| `producerSequence` | long | log と telemetry を横断する順序 |
| `unityFrameAtStart` / `unityFrameAtEnd` | long | span の frame |
| `buildVersion` / `platform` / `deviceModel` / `osVersion` / `engineVersion` | keyword | **セッション属性。現在 Elastic に入っていない（K3-0）** |
| `payload.shape` | keyword | `TimingMemory` / `Frame` / `EventDetail` / `CameraCounters` |
| `payload.targetIdentity` | keyword | Scene 系 span の対象 |
| `payload.stage` | keyword | AppStartup の区間（`BeforeSceneLoad` / `AfterSceneLoad`。失敗時のみ到達段階名） |
| `payload.managedBeforeBytes` / `managedAfterBytes` / `managedDeltaBytes` | long | span 前後のマネージドメモリ |
| `payload.nativeBeforeBytes` / `nativeAfterBytes` / `nativeDeltaBytes` | long | 同ネイティブ |
| `payload.fps` / `payload.cpuMs` / `payload.gpuMs` | float | **`ProfilerSummary` sample。1 秒ごと** |
| `payload.gpuAvailable` | boolean | GPU 計測可否。**false のとき `gpuMs` は意味を持たない** |
| `payload.managedBytes` / `payload.nativeBytes` | long | 同 sample の絶対値 |
| `payload.gcGen0Delta` / `payload.unityFrame` | integer | `GcSpike` / `UiCost` event |
| `payload.cameraTotalViewCount` ほか | integer | `CameraSystemSnapshot` |

**deprecated（参照禁止・§1.6）**: `cpuTime` / `gpuTime` / `managedMem` / `nativeMem` / `cameraTotalViewCount` / `cameraAdditionalViewCount` / `cameraBlendingViewCount` / `cameraMaxStackDepthTotal`（**`payload.` 接頭辞の付いた同名は正本であり、禁止されていない**）

### A.2 log index（`debugstudio-log-*`）

| フィールド | 型 | 備考 |
|---|---|---|
| `@timestamp` | date | |
| `log.level` | keyword | **正本。**`logLevel`（flat）は ingest pipeline が `log.level` へ rename する |
| `log.logger` / `category` | keyword | |
| `message` / `exception` | text | |
| `sessionId` | keyword | |
| `producerSequence` / `unityFrameAtEmit` | long | telemetry との突き合わせ |
| `traceId` / `spanId` | long | 同上 |

**log に `buildVersion` は無い**（スコープ外）。

### A.3 telemetry の発生源（どこから出ているか）

| name | 発生源 | 頻度 |
|---|---|---|
| `AppStartup` | `AbstractApplicationInitializer`（BeforeSceneLoad / AfterSceneLoad の 2 span） | run に 2 回 |
| `SceneTransition` / `SceneLoad` / `SceneUnload` | `SceneDirector.Transitions/Loading/Unloading` | 遷移ごと |
| `ProfilerSummary` | `DebugProfilerView.LogSummary` | **1 秒に 1 回** |
| `GcSpike` / `UiCost` | `DebugProfilerView` | 閾値超過時 |
| `CameraSystemSnapshot` / `CameraSwitch` | `CameraSystemTelemetryEmitter` / `CameraView` | 周期 / 切替時 |
