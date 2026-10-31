# Bs5aSelectionCheck

BuildSystem スライス BS5a の判定 C が使う offline の照合 script と、Phase A で凍結する期待値の置き場。期待値の意味と判定条件はそのスライスの HANDOFF が正で、この README は使い方だけを書く。Unity には依存しない。

## 入口

- `Check-Bs5aSelection.ps1 -Preflight <preflight.json ...> [-Mode Baseline|Judgment]`: selected の sorted stable key を期待値と比べる。全件 PASS で 0。
- `Save-Bs5aBaseline.ps1 -Preflight <preflight.json ...> -SourceCommit <sha>`: 期待値 (i) を `expected/<contentSet>.baseline.keys` と出所の `.baseline.json` に書く。
- `Check-Bs5aSelection.tests.ps1`: 合成 preflight による自己検査。

## 期待値の出所（混ぜない）

| file | 種類 | 出所 |
|---|---|---|
| `expected/<contentSet>.baseline.keys` / `.json` | (i) 実測 | 旧 policy の commit（`.json` の `sourceCommit`）の Editor で menu を実行した `preflight.json` |
| `expected/attached-assets.keys` / `.tsv` | (ii) 導出 | `ScenePayloadMappingPolicy.StableKey` の規則と各 asset の `.meta` の GUID から手で導いた |
| `expected/spring-planner-whitebox.keys` / `.logical.tsv` | (iii) 仕様 | 宣言の到達規則を `SceneResourceMap` に手で適用した logical key の一覧 |

実装の出力で (ii) (iii) を上書きしない。`bs4-spring-full` は `spring-full` の baseline に probe 候補（`bs4:fixture:prefab`、GUID は build ごとに変わる）1 件を加えたものと比べる。
