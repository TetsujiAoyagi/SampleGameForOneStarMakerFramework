# BS3 Phase B 実装結果 revision 7

- frozen Phase A: `artifacts/bs3-phase-a/phase-a-r2.md`
- implementation base: `25a5024d310347ef9ce67128636a06e0e0fb5172`
- implementation head: `e722d72`
- 実装担当: 主担当 GPT-6 Astra

revision 6 の C/C' 所見に対し、Scene の通常 unload・明示 close・同期終了が重なる場合に同じ native unload terminal を待つよう、session token 内で進行中 unload を共有した。成功 terminal で一回だけ token を返し、失敗時は登録と token を保持して次の retry を許す。Unity の unload API が操作を返さず Scene が残る場合は `OperationFailed` とし、成功と偽らない。起動設定、直接登録、削除 gate に与える不正 path は `InvalidConfiguration` に揃えた。fake native の競合・不正 path テストを追加した。

Phase B の contract audit は exit 0。Unity compile と絞り込み EditMode テストは Phase C で実行し、結果は revision 7 evidence bundle に固定する。旧 head `c218ecc` の C/C' 判定はこの版へ流用しない。
