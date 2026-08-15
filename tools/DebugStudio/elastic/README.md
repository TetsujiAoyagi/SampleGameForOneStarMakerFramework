# DebugStudio Elastic L1 Verify / L2 Ship Runbook

ローカル Elastic / Kibana を起動し、DebugStudio の L0 永続化 NDJSON を Elastic へ届ける手順です。

| 経路 | 用途 | DebugStudio の関与 |
|---|---|---|
| L1 Verify | retained telemetry を WPF から明示 `_bulk` 投入 | preflight / push UI のみ |
| L2 Ship | L0 NDJSON を Filebeat が tail して継続配送 | artifact 生成のみ。**Filebeat は起動・監督しない** |

DebugStudio は API key を UI・ログ・設定ファイルへ保存しません。

## 前提

- Docker Desktop など compose 実行環境
- DebugStudio ビルド済み WPF アプリ
- L0 永続化先: `%LocalAppData%\DebugStudio\telemetry\` / `%LocalAppData%\DebugStudio\logs\`（flat rolling `*.ndjson`）

## 1. Elastic / Kibana / Filebeat (compose) を起動

```powershell
cd tools/DebugStudio/elastic
docker compose up -d
```

疎通確認:

```powershell
curl http://localhost:9200/
```

**ローカル compose は `xpack.security.enabled=false` のため API key 不要**です。

### endpoint の違い（重要）

| 実行場所 | Filebeat config | Elasticsearch endpoint |
|---|---|---|
| **host**（artifact 生成物） | `%LocalAppData%\DebugStudio\elastic-artifacts\filebeat\debugstudio-filebeat.yml` | `http://localhost:9200` |
| **compose 内** | `tools/DebugStudio/elastic/filebeat/filebeat.yml` | `http://elasticsearch:9200` |

host Filebeat と compose Filebeat で参照する config は別です。endpoint を取り違えると preflight は通っても ship が失敗します。

compose Filebeat は `%LOCALAPPDATA%\DebugStudio` を `/mnt/debugstudio-l0` へ **read-only** マウントし、registry は `filebeat-data` named volume に保持します。
両 config は filestream の NDJSON parser（`target: ""` / `overwrite_keys: true`）で各行を event root に復元するため、L0 の `@timestamp`、`stream` など既存 document field は `message` 文字列だけに閉じ込められず保持されます。入力 ID は event field にならないため、各 input が明示する `debugstudio.route` により `debugstudio-telemetry-YYYY.MM.dd` / `debugstudio-log-YYYY.MM.dd` へ配送します。`stream` は既存 ingest pipeline の意味を保つため routing には使用しません。

## 2. template / pipeline の bootstrap（L2 前提）

Filebeat 投入前に index template と ingest pipeline を登録します。L1 Verify の **Elastic Push** でも同じ bootstrap が走ります。

**artifact 経由（operator / CI）:**

```powershell
dotnet run --project tools/DebugStudio/src/DebugStudio.ElasticArtifactGen
cd $env:LOCALAPPDATA\DebugStudio\elastic-artifacts\commands
.\import-telemetry.ps1 -ElasticUrl http://localhost:9200
.\import-kibana.ps1 -KibanaUrl http://localhost:5601
```

`import-telemetry.ps1` は template / pipeline を PUT し、同梱 bulk NDJSON があれば `_bulk` も実行します。L2 継続 tail だけが目的なら template / pipeline PUT 部分が重要です。
`import-kibana.ps1` は saved objects（下記のダッシュボード 2 枚を含む）を Kibana へ import します。dashboard を見る前に必ず実行してください。

| ダッシュボード | id | 答える問い |
|---|---|---|
| **DebugStudio Run Timeline** | `debugstudio-overview-dashboard` | Q1: 今の実行で何が重いか。`run (sessionId)` コントロールで 1 run に絞って見る |
| **DebugStudio Run over Run** | `debugstudio-run-over-run-dashboard` | Q2: 前の実行と比べて何が重くなったか。run を横に並べる |

読み方は各ダッシュボードの description に書いてあります。

> **artifact を生成して `import-kibana.ps1` を実行しない限り、ダッシュボードは Kibana に存在しません。**
> 「Dashboard が見えない」の原因が「そもそも一度も import していなかった」だったことが実際にあります。
> `dotnet run` は**必ず見ているブランチの作業ツリーから**実行してください。別ブランチから実行すると
> 旧 artifact が `%LOCALAPPDATA%` に生成され、import しても意図した内容になりません。

> **template / pipeline の PUT は ingest より先に行う必要があります。**
> template 適用前に作られた index は動的 mapping になり、後から template を直しても**既存 index の
> mapping は変わりません**。そのまま `debugstudio-telemetry-*` を横断すると、`kind` のように
> クエリごと 400 で落ちるフィールドと、`payload.stage` のように**エラー無しで全行 null になる**
> フィールドが混在します。後者は結果を見ても気づけません。
> 衝突の確認と復旧は「トラブルシュート」を見てください。

**L1 WPF 経由:** Telemetry パネルの **Elastic Preflight** → **Elastic Push**（retained telemetry がある場合）。

## 3. L1 Verify（任意・疎通確認）

| 変数 | 既定 | 説明 |
|---|---|---|
| `DEBUGSTUDIO_ELASTIC_URL` | `http://localhost:9200` | loopback のみ |
| `DEBUGSTUDIO_KIBANA_URL` | `http://localhost:5601` | 成功時に UI が案内 |
| `DEBUGSTUDIO_ELASTIC_API_KEY` | 未設定 | **security 有効な管理 Elastic のみ**。Base64 済み値を `Authorization: ApiKey <value>` にそのまま使用 |

1. Unity セッションを接続し telemetry を発生させる
2. Telemetry パネルの **Elastic L1 Verify** で env 設定有無と retained preview を確認
3. **Elastic Preflight** → **Elastic Push**

失敗しても L0 NDJSON 永続化や受信処理は継続します。

## 4. L2 Ship — E2E 検証（Unity → DebugStudio → NDJSON → Filebeat → Kibana）

1. **L0 Capture:** DebugStudio を起動し Unity を接続してプレイする
2. **NDJSON 確認:** `%LocalAppData%\DebugStudio\telemetry\debugstudio-telemetry_*.ndjson` と `logs\debugstudio-logs_*.ndjson` が増える
3. **bootstrap:** 上記 §2 を完了する（未登録だと pipeline 参照で ingest が失敗する）
4. **Filebeat ship:**
   - **compose:** `docker compose up -d filebeat`（§1 済みなら再起動のみ）
   - **host:** artifact の `debugstudio-filebeat.yml` を Filebeat に渡して起動（DebugStudio は起動しない）
5. **Kibana 確認:** `http://localhost:5601` で Dashboard → `DebugStudio Run Timeline` / `DebugStudio Run over Run` を開き、あわせて Discover で `debugstudio-telemetry-*` / `debugstudio-log-*` に document が増えることを確認する

```powershell
# ingest 件数のざっくり確認（security 無効 compose）
curl "http://localhost:9200/debugstudio-telemetry-*/_count"
curl "http://localhost:9200/debugstudio-log-*/_count"
```

## 5. Filebeat health / ingest lag

| 確認 | コマンド / 観点 |
|---|---|
| compose プロセス | `docker compose ps filebeat` が `running` |
| Filebeat ログ | `docker compose logs -f filebeat` に publish error がない |
| registry 進行 | `filebeat-data` volume 内 registry が更新され続ける |
| ingest lag | L0 最新ファイルの末尾行数 vs Elasticsearch `_count` の差分。Filebeat 停止中は L0 のみ増え lag が開く |
| Elastic 側エラー | `GET _ingest/pipeline/debugstudio-telemetry/_simulate` や bulk item error（L1 と同様） |

**失敗分離:** Filebeat / Elastic 障害は L0 Capture を止めません。DebugStudio と Unity はそのまま動き、NDJSON は LocalAppData に蓄積されます。復旧後 Filebeat は checkpoint から tail を再開します。

## 6. 管理 Elastic 向け API key 注入（秘密値は書かない）

ローカル compose では不要です。本番 / ステージング等で security が有効な場合:

- **禁止:** DebugStudio UI・ログ・リポジトリ committed config への API key 平文保存
- **推奨:** Elastic Agent policy、Vault / K8s Secret、CI secret、Filebeat 起動時の環境変数展開など **運用側 inject**
- artifact / compose の YAML には `output.elasticsearch.api_key` の**注入手順のみ**日本語コメントで記載（値は含めない）

例（host 運用の概念。実際の secret 値は別管理）:

```yaml
output.elasticsearch:
  hosts: ["https://elastic.example.com:9200"]
  # api_key: ${DEBUGSTUDIO_ELASTIC_API_KEY}  # 秘密管理から注入
```

## 7. artifact config 生成（host Filebeat）

```powershell
dotnet run --project tools/DebugStudio/src/DebugStudio.ElasticArtifactGen
# 出力: %LocalAppData%\DebugStudio\elastic-artifacts\
# 第2引数 inputRoot で L0 ルートを上書き可能
```

既定 input root は `%LocalAppData%\DebugStudio` です。

## 8. 停止

```powershell
docker compose down
```

L0 NDJSON と Filebeat registry (`filebeat-data`) は volume / ホスト側に残ります。

## 9. ES|QL クエリ正本

ダッシュボードの各パネルが**何をどう集計しているか**の正本を
[`queries/`](queries/) に `.esql` として置いています。

Lens のパネル定義は Kibana のバージョンに依存する巨大な JSON で `git diff` では読めませんが、
そこに埋まっている「何を集計したいか」はバージョンに依存しません。分離しておくと、
Kibana が上がってパネル JSON が壊れても**何を作りたかったかは残ります**。

**順序は必ず「クエリを通す → パネルに起こす」です。** 逆をやると、実在しないフィールドを
参照したパネルがレビューを通過します（2026-08-08 のスライスで実際に起きました）。

```powershell
$body = @{ query = (Get-Content -Raw tools/DebugStudio/elastic/queries/runs.esql) } | ConvertTo-Json -Compress
Invoke-RestMethod -Method Post -Uri "http://localhost:9200/_query?format=txt" `
  -ContentType "application/json" -Body $body
```

ES|QL は `//` 行コメントと改行をそのまま受け付けるので、ファイルの中身を無加工で投げられます。

| ファイル | パネル |
|---|---|
| `heavy-spans.esql` | D1-4 重い span |
| `tag-breakdown.esql` | D1-6 異常タグ内訳 |
| `runs.esql` | D2-1 run メタ表 |
| `app-startup-per-run.esql` | D2-2 AppStartup |
| `scene-load-per-run.esql` | D2-3 SceneLoad |
| `event-rate-per-run.esql` | D2-7 異常発生率 |
| `frame-cost-per-run.esql` | **パネル未実装**（D2-4 CPU / D2-5 fps / D2-6 メモリ用。実データで 0 行） |

**この対応は `KibanaEsqlQuerySourceOfTruthTests` が両方向に強制します。** パネルを足して
`.esql` を足し忘れても、`.esql` を直してパネルに反映し忘れても赤くなります。

クエリを書くときに踏む罠（multivalue への `==` が静かに件数を減らす、等）は
[`queries/README.md`](queries/README.md) にまとめてあります。

### Lens の ES|QL パネルは既定で先頭 5 列しか表示しない

**クエリが 10 列返しても、パネルは 5 列だけ表示した状態で保存されます。** Kibana は
編集画面に「Displaying a limited portion of the available fields」という警告を出しますが、
**保存後のダッシュボードには何も出ません**。K3-4 ではこれを見落とし、run メタ表が
`platform` / `deviceModel` / `osVersion` / `engineVersion` / `runSeconds` を、
異常発生率パネルが `gcPerMin` / `uiPerMin` / `bottleneckPerMin` を落としたまま
「完成」として commit されていました（PR #17 レビューで発覚）。

**パネルを作ったら、クエリの列数と表示列数を必ず突き合わせること。** 足りない列は
Lens の Visualization configuration → Metrics → 「Add or drag-and-drop a field」で足します。

これは `KibanaEsqlPanelColumnCoverageTests` が機械的に強制するようになりました。
意図的に列を落とす場合は同テストの `IntentionallyHiddenColumns` に理由付きで宣言します
（**黙って消える**を**宣言して消す**に変えるのが目的）。

### パネルのタイトルは NDJSON 上で 2 箇所ある

| 場所 | 何か |
|---|---|
| `panelsJSON[].title` | **ダッシュボードに表示される名前。** ここだけが人間の付けた名前 |
| `panelsJSON[].embeddableConfig.attributes.title` | Lens が列名から自動生成した内部名（`Table started & ended & …`） |

**後者は手で直しません。** §1.4 が「`_export` したものだけを正本にする」と決めており、
これは Lens の state の一部です。by-value パネルの inline エディタにこの欄は出てこないので、
書き換えるなら NDJSON を手編集するしかなく、それは §1.4 違反になります。
**表示は前者が使われるので実害はありません**が、NDJSON を読むときは前者を見てください。

## トラブルシュート

| 症状 | 確認 |
|---|---|
| preflight 失敗 | Elastic 起動、`DEBUGSTUDIO_ELASTIC_URL` が loopback か |
| Filebeat が 0 件 | L0 に NDJSON があるか、マウント path `/mnt/debugstudio-l0` が空でないか |
| pipeline エラー | §2 bootstrap 済みか、`debugstudio-telemetry` / `debugstudio-log` pipeline が存在するか |
| log だけ 0 件 / Filebeat dropped | L1 Push は telemetry のみ bootstrap。log は `import-telemetry.ps1` か pipeline PUT が必要。既に drop 済みなら下記「registry を作り直す」 |
| bulk item 409 (L1) | create 再実行による重複 |
| host Filebeat が ES に届かない | config が `localhost:9200` か（compose 内 endpoint と混同していないか） |
| **件数が L0 より多い（二重投入）** | 下記「件数を検算する」 |
| **フィールドが全行 null / `Cannot use field [x] due to ambiguities`** | 下記「mapping 衝突」 |

### 件数を検算する（**ダッシュボードの数字を信じる前に一度やる**）

registry を作り直すと Filebeat は L0 を**先頭から読み直す**ため、既存 index を消さずに行うと
**同じ record が二重に入ります。エラーは出ず、件数だけが増えます。**
実際に telemetry が 770 件のところ 1516 件入っていたことがあります。

```powershell
# L0 の総行数（これが正解）
(Get-ChildItem "$env:LOCALAPPDATA\DebugStudio\telemetry\*.ndjson" | Get-Content | Measure-Object -Line).Lines
# Elasticsearch 側
curl "http://localhost:9200/debugstudio-telemetry-*/_count"
```

一致しなければ二重投入です。**Elastic の中だけを見ていても分かりません。**

### mapping 衝突

```powershell
curl "http://localhost:9200/debugstudio-telemetry-*/_field_caps?fields=kind,payload.stage,payload.targetIdentity"
```

1 つのフィールドに型が 2 つ以上出たら衝突です。壊れ方は 2 種類あり、
**`keyword` / `text` の衝突は集計でエラーになるものと、静かに null になるものがあります。**
Kibana の data view でも conflict 型になり、**Lens で集計できなくなります。**

### registry を作り直す（衝突・二重投入の復旧）

**index を消してから registry を消すこと。** 順番を逆にすると二重投入になります。

```powershell
# 1. index を明示列挙して削除（ES はワイルドカード削除を拒否する: action.destructive_requires_name）
$names = (Invoke-RestMethod -Uri "http://localhost:9200/_cat/indices/debugstudio-telemetry-*,debugstudio-log-*?h=index&format=json").index
Invoke-RestMethod -Method Delete -Uri ("http://localhost:9200/" + ($names -join ","))

# 2. registry を破棄して再作成（L0 を先頭から読み直す）
docker compose rm -f filebeat
docker volume rm elastic_filebeat-data
docker compose up -d filebeat
```

完了後に必ず「件数を検算する」と「mapping 衝突」の両方を確認してください。

> **Windows PowerShell 5.1 では `&&` が使えず、`curl` は `Invoke-WebRequest` の別名**です
> （`-s -X DELETE` が通りません）。上記は `Invoke-RestMethod` で書いてあります。
> `&&` で繋ぎたい場合は pwsh 7 を使ってください。

## L0 永続化の契約

Telemetry の自動永続は Log（`logs\`、先行して稼働）と**同じ運用契約に揃えてある**。

| 項目 | 決定 |
|---|---|
| 対象 | `Telemetry` フレームのみ（`ServiceStatus` は含めない） |
| 形式 | NDJSON、日次 + サイズ rolling |
| 出力先 | `%LocalAppData%\DebugStudio\telemetry\` |
| ファイル名 | `debugstudio-telemetry_yyyy-MM-dd_NNN.ndjson` |
| 保持 | **10 MB × 10 世代** |
| Elastic Bulk | 手動 Export 専用。自動配送はしない（配送は Filebeat の責務） |

### 運用ルール

| 領域 | ルール |
|---|---|
| 送信失敗 | **L0 Capture を止めない。** replay は Filebeat の checkpoint / retry に任せる |
| memory | NDJSON write は非同期。shipper が停滞しても DebugStudio の memory を増やさない |
| データ量 | 高頻度 telemetry の sampling / aggregation は producer か transform 層で明示する |
| PII / secrets | message / tag の allowlist と redaction 基準を本番導入前に定義する |
| retention | development / QA / production で index lifecycle を分ける |
| credentials | **Unity・`app-config`・リポジトリに Elastic key を置かない**（§6 の API key 注入を使う） |
