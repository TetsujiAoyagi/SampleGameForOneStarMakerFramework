# DebugStudio L1 Elastic Verify Runbook

ローカル Elastic / Kibana を起動し、DebugStudio WPF から retained telemetry を `_bulk` 投入して疎通確認する手順です。L2 Filebeat 方針は変更しません。

## 前提

- Docker Desktop など compose 実行環境（本 runbook では compose 起動のみ記載。実行は任意）
- DebugStudio ビルド済み WPF アプリ

## 1. Elastic / Kibana を起動

```powershell
cd tools/DebugStudio/elastic
docker compose up -d
```

疎通確認:

```powershell
curl http://localhost:9200/
```

security 無効構成を L1 の主経路とします。

## 2. 環境変数（任意）

| 変数 | 既定 | 説明 |
|---|---|---|
| `DEBUGSTUDIO_ELASTIC_URL` | `http://localhost:9200` | loopback のみ |
| `DEBUGSTUDIO_KIBANA_URL` | `http://localhost:5601` | 成功時に UI が案内 |
| `DEBUGSTUDIO_ELASTIC_API_KEY` | 未設定 | security 有効時のみ。Base64 済み値を `Authorization: ApiKey <value>` にそのまま使用 |

例:

```powershell
$env:DEBUGSTUDIO_ELASTIC_URL = "http://localhost:9200"
$env:DEBUGSTUDIO_KIBANA_URL = "http://localhost:5601"
```

秘密値は DebugStudio UI・ログ・設定ファイルへ保存しません。

## 3. DebugStudio で L1 Verify

1. Unity セッションを接続し telemetry を発生させる
2. Telemetry パネルの **Elastic L1 Verify** で env 設定有無と retained preview（最大 256 件の current-session 近似）を確認
3. **Elastic Preflight** で `GET /`
4. **Elastic Push** で index template / ingest pipeline bootstrap と `_bulk` 投入

失敗しても L0 NDJSON 永続化や受信処理は継続します。

## 4. Kibana で確認

- ブラウザで `http://localhost:5601`
- `debugstudio-telemetry-*` index に document が増えていることを確認

## 5. 停止

```powershell
docker compose down
```

## トラブルシュート

| 症状 | 確認 |
|---|---|
| preflight 失敗 | Elastic が起動しているか、`DEBUGSTUDIO_ELASTIC_URL` が loopback か |
| bulk item 409 | create 再実行による重複。受理不明 timeout 後の retry でも同様 |
| 0 件で push 不可 | retained telemetry が空。ServiceStatus のみでは push 対象外 |

## 関連

- 計画: [`docs/planning/DEBUGSTUDIO_TELEMETRY_PERSISTENCE_AND_ELASTIC_DELIVERY_PLAN_2026-07-19.md`](../../../docs/planning/DEBUGSTUDIO_TELEMETRY_PERSISTENCE_AND_ELASTIC_DELIVERY_PLAN_2026-07-19.md)
- L2: 既存 Filebeat artifact / operator scripts（変更なし）
