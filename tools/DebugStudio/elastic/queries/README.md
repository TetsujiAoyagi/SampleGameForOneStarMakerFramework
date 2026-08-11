# ES|QL クエリ正本

`tools/DebugStudio/elastic/kibana/debugstudio-overview.ndjson` のダッシュボードパネルが
**何をどう集計しているか**の正本。

## なぜ別ファイルなのか

Lens パネルの定義は Kibana のバージョンに依存する巨大な JSON で、`git diff` では読めない。
一方そこに埋まっている「何を集計したいか」はバージョンに依存しない。分離しておくと:

- Kibana が上がってパネル JSON が壊れても、**何を作りたかったかは残る**
- パネルを作る前に「その数字が本当に取れるか」を実 Elastic で確認できる
- `curl` で叩けるので、Kibana を開かずに数字を確認できる

順序は必ず **クエリを通す → パネルに起こす**。逆をやると、実在しないフィールドを参照した
パネルがレビューを通過する（2026-08-08 スライスで実際に起きた）。

## 叩き方

```bash
curl -s -X POST "http://localhost:9200/_query?format=txt" \
  -H 'Content-Type: application/json' \
  --data-binary @<(python -c 'import json,sys;print(json.dumps({"query":open(sys.argv[1]).read()}))' runs.esql)
```

PowerShell（Windows でこちらが確実）:

```powershell
$body = @{ query = (Get-Content -Raw runs.esql) } | ConvertTo-Json -Compress
Invoke-RestMethod -Method Post -Uri "http://localhost:9200/_query?format=txt" `
  -ContentType "application/json" -Body $body
```

ES|QL は `//` 行コメントと改行をそのまま受け付けるので、**ファイルの中身を無加工で投げてよい**。

## ファイルとパネルの対応

| ファイル | パネル | 実データで動くか |
|---|---|---|
| `runs.esql` | D2-1 run メタ表 | ○ |
| `app-startup-per-run.esql` | D2-2 AppStartup | ○ |
| `scene-load-per-run.esql` | D2-3 SceneLoad | ○ |
| `frame-cost-per-run.esql` | D2-4 CPU / D2-5 fps / D2-6 メモリ | **× 0 行**（下記） |
| `event-rate-per-run.esql` | D2-7 異常発生率 | △ `bottleneck` 列のみ |

### `ProfilerSummary` が出ていない

`frame-cost-per-run.esql` が 0 行を返し、`event-rate-per-run.esql` の `gc` / `ui` が常に 0 なのは、
**`ProfilerSummary` / `GcSpike` / `UiCost` が Unity から 1 件も emit されていない**ため。
構文エラーではない。

発生源の `DebugProfilerView` は `UIView` だが、プロジェクト全体で呼び出し側が 1 つも無く、
Debug レイヤーに積まれることが無い。詳細と対応方針は
[`docs/planning/KIBANA_DASHBOARD_CONTENT_HANDOFF_2026-08-11.md`](../../../../docs/planning/KIBANA_DASHBOARD_CONTENT_HANDOFF_2026-08-11.md) §7 を見ること。

## 書くときに踏む罠（実測で踏んだもの）

| 罠 | 症状 | 対処 |
|---|---|---|
| **multivalue への `==`** | `tags == "Bottleneck"` が**エラーを出さずに件数だけ減らす**（実測 17 件 → 8 件）。ES\|QL は multivalue フィールドへの比較を null にするため、`["Bottleneck","NativeMemoryOver"]` の record が丸ごと落ちる | `CONCAT("|", MV_CONCAT(tags, "|"), "|") LIKE "*|Bottleneck|*"`。`MV_CONTAINS` は 8.17 に存在しない |
| **index 間の mapping 衝突** | 同じフィールドが index ごとに別の型だと、`kind` のように**クエリごと 400 で落ちる**ものと、`payload.stage` のように**エラー無しで全行 null になる**ものがある。後者は結果を見ても気づけない | index template を適用してから ingest する。衝突した index は作り直す。`_field_caps` で型が 1 つだけか確認する |
| **`sessionId` が null の record** | run ではないのに 1 グループにまとまり、実測で 48227 秒（13 時間超）の存在しない run が 1 行できて、他の run が縦軸で潰れる | `WHERE sessionId IS NOT NULL`。**セッション属性が null なのとは別物**で、そちらは落とさない（run は実在するので） |
| **run をまたいだ percentile** | run が 1 本増えるたびに過去の値まで動く | `BY sessionId` を外さない。run 内で代表値を取ってから run 間で並べる |
| **`DATE_DIFF` の引数順** | 逆にすると符号が反転する | `(単位, 開始, 終了)`。実測で正の秒数が返ることを確認済み |

## 参照禁止フィールド

`cpuTime` / `gpuTime` / `managedMem` / `nativeMem` / `cameraTotalViewCount` /
`cameraAdditionalViewCount` / `cameraBlendingViewCount` / `cameraMaxStackDepthTotal` の 8 語は
Telemetry Contract v3 で deprecated。正本は `payload.*` 側（`payload.cpuMs` 等）。

saved object 側はこの 8 語を `KibanaSavedObjectBundleValidator` の V6 が機械的に赤にするが、
**`.esql` ファイルは検算対象外**なので、ここは人間が守ること。
