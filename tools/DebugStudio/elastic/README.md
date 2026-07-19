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
```

`import-telemetry.ps1` は template / pipeline を PUT し、同梱 bulk NDJSON があれば `_bulk` も実行します。L2 継続 tail だけが目的なら template / pipeline PUT 部分が重要です。

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
5. **Kibana 確認:** `http://localhost:5601` で `debugstudio-telemetry-*` / `debugstudio-log-*` に document が増える

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

## トラブルシュート

| 症状 | 確認 |
|---|---|
| preflight 失敗 | Elastic 起動、`DEBUGSTUDIO_ELASTIC_URL` が loopback か |
| Filebeat が 0 件 | L0 に NDJSON があるか、マウント path `/mnt/debugstudio-l0` が空でないか |
| pipeline エラー | §2 bootstrap 済みか、`debugstudio-telemetry` / `debugstudio-log` pipeline が存在するか |
| log だけ 0 件 / Filebeat dropped | L1 Push は telemetry のみ bootstrap。log は `import-telemetry.ps1` か pipeline PUT が必要。既に drop 済みなら `docker compose rm -f -s filebeat` → `docker volume rm elastic_filebeat-data` → `docker compose up -d filebeat` で再読込（telemetry 重複あり） |
| bulk item 409 (L1) | create 再実行による重複 |
| host Filebeat が ES に届かない | config が `localhost:9200` か（compose 内 endpoint と混同していないか） |

## 関連

- 計画: [`docs/planning/DEBUGSTUDIO_TELEMETRY_PERSISTENCE_AND_ELASTIC_DELIVERY_PLAN_2026-07-19.md`](../../../docs/planning/DEBUGSTUDIO_TELEMETRY_PERSISTENCE_AND_ELASTIC_DELIVERY_PLAN_2026-07-19.md)
