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
| **V10** | **`type=search` の `sort` は配列である**（文字列は不可）。V6 の sort 検査は `ValueKind==Array` のときだけ走るため、文字列に戻ると検査ごと消える | C' 監査 A1 / U-6 |

**deprecated 語の一元化**（外部レビュー指摘 3 / C' 監査 A3）:

- `DeprecatedFields`（`HashSet`）と `DeprecatedFieldInQuery`（`Regex`）が同じ 8 語を二重に持っている。**片方だけ更新すると columns/sort と query で判定が食い違う**
- `DeprecatedFieldCatalog` に語リストを 1 本化し、**Regex はそのリストから生成する**

#### 実装時の注意

- **V7 と V4 の関係を壊さないこと。** V7 は「`panelRefName` が存在すること」、V4 は「存在するものが `references` と 1:1 であること」。V7 を V4 の中に混ぜると、どちらで落ちたか分からなくなる
- **issue の `RuleId` は既存の書式（`"V1"` 等）に揃える。** メッセージは `行 N (id='X'): V7 — …` の形（既存に合わせる）
- **`Validate` は例外を投げず issue のリストを返す**（既存の契約）。新ルールも同じ
- **純関数を維持すること。** `Elastic/Kibana/` の 6 ファイルには `System.IO` / `File.` / `GetManifestResourceStream` が**1 つも無い**（C' 監査が裏付け済み）。ここに IO を持ち込むと検算がテスト不能になる

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

---


## 7. Phase C レビュー

（Phase C が記入する。`git diff --stat` の構造レビュー → 機能レビュー → **確認していないこと**の順）

---

## 8. Phase C' 監査

（実装にも設計にも関与していないモデルが記入する）

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
