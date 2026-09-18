# BS3 Phase B 実装結果 revision 4

- frozen Phase A: `artifacts/bs3-phase-a/phase-a-r2.md`
- implementation base: `25a5024d310347ef9ce67128636a06e0e0fb5172`
- implementation head: `236f4c33da7a61e8d796375a51bdde11c4037704`
- 初版と旧 revision: `artifacts/bs3-phase-b/phase-b.md`、`phase-b-r2.md`、`phase-b-r3.md`
- 実装担当: GPT-5.6 Terra と主担当 GPT-6 Astra

revision 3 の C/C' 所見に対し、まだ loaded の directory Scene を同期終了で即解放せず、非同期 unload の terminal まで登録を保持するようにした。通常の owner 解放や resident cache eviction で native release が失敗した場合は session が token を再試行台帳へ移し、close retry で回収する。logical key と representation の索引は各成分を符号化して衝突を防ぐ。既存の公開 `SceneDirector` constructor 署名を復元し、directory mode は同一 Runtime assembly の internal constructor で起動時に固定する。欠損 directory/root、特殊文字キー、解放失敗、同期終了の Scene terminal の fake test を追加した。

Phase C の Unity テストと監査は revision 4 evidence bundle に固定する。旧 head `c88c95c` の NO-GO はこの版の判定に流用しない。
