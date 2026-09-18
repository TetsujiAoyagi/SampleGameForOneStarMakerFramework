# BS3 Phase B 実装結果 revision 3

- frozen Phase A: `artifacts/bs3-phase-a/phase-a-r2.md`
- implementation base: `25a5024d310347ef9ce67128636a06e0e0fb5172`
- implementation head: `c88c95c637dd373f2bcd85fb617421e8f702fb69`
- 初版と revision 2 の結果: `artifacts/bs3-phase-b/phase-b.md`、`artifacts/bs3-phase-b/phase-b-r2.md`
- 実装担当: GPT-5.6 Terra と主担当 GPT-6 Astra

revision 2 の C/C' で見つかった寿命契約違反を修正した。取消後の native cleanup が失敗した資源は session に保持し、明示 close で再試行する。失敗が続く間は unregister と delete lease の解放をしない。呼出側が受け取らなかった instance は session が GameObject を破棄して prefab token を返す。通常の instance は引き続き caller/Scene が所有する。登録失敗後の native rollback は session が保持し、process gate は予約の状態だけを管理する。AssetManagement の既存 registry/cache hit も受付停止を確認し、Scene identity が異なる表現を再利用しない。Prefab の Instantiate 例外では発行済みの source asset token を解放する。

Phase C の Unity テストと機械検査は revision 3 evidence bundle に固定する。旧 head `de160e1` の NO-GO レビュー結果は revision 3 の判定へ流用しない。
