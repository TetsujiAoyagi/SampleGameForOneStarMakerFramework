# BS3 Phase B 実装結果 revision 9

- frozen Phase A: `artifacts/bs3-phase-a/phase-a-r3.md`
- implementation base: `25a5024d310347ef9ce67128636a06e0e0fb5172`
- implementation head: `7c34c1d`
- 実装担当: 主担当 GPT-6 Astra

Runtime directory の登録、manifest 索引、型付き Object / Prefab / Scene ロード、所有者と cache の解放、同一 process の revision 削除排他、明示 close と再試行を実装した。既定の Addressables 経路は維持し、明示設定した directory mode は既存のアプリ起動順序から選択する。

build root の target は BS2b の `StandaloneWindows64-Player` と一致させた。登録と削除 gate と session は canonical absolute path を共有し、Windows の末尾 separator と大小文字 alias を同一 path として扱う。統合テストは実 directory のロードに加え、既定 Addressables と directory mode の実アプリ Editor Play 起動を確認する。

manifest の旧 entry は representation が未記録でも Full として扱う。この nullable 入力を `EffectiveRepresentation(string?)` の型契約に明示した。コード本体の索引化規則は変えていない。

Unity バッチテスト、契約監査、文書監査の結果は Phase C の固定証拠に記録する。対象外の別 process 同時利用保護と配布時の物理削除は後続 slice の責務である。
