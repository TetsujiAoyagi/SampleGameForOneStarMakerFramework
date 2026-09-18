# BS3 Phase B 実装結果 revision 8

- frozen Phase A: `artifacts/bs3-phase-a/phase-a-r3.md`
- implementation base: `25a5024d310347ef9ce67128636a06e0e0fb5172`
- code head: `4cdc0a4`
- 実装担当: 主担当 GPT-6 Astra

revision 7 の C は実アプリの Editor Play 選択が未検証と判定し、C' は物理的に同じ path の末尾 separator alias が revision gate で一致しないことを指摘した。実アプリ起動を統合テストへ加えると、BS2b build root target の `StandaloneWindows64-Player` と Runtime の期待値 `StandaloneWindows64` が異なり、登録失敗を実証した。Phase A revision 3 で期待値を実 build に揃える判断を凍結してから実装した。

Runtime は起動設定、登録、削除 gate、session/cache が同じ canonical absolute path を使う。Windows では path の末尾 separator と大小文字 alias を畳む。initializer の target 定数は既存 build root の Player subtarget 表現に合わせ、診断 field まで統一した。統合テストは移設済み実 directory の型付きロードに加え、既定 Addressables Play の Director と session 不在、および環境変数で明示した directory Play の session/Director と delete lease 拒否を確認する。

実 directory 統合 1/1 と revision gate 4/4 は Unity 6000.6.0f1 の規定 runner で exit 0。残りの回帰、契約監査、文書監査は Phase C の固定証拠束に記録する。旧 head `7997dc4` の C/C' NO-GO 判定はこの版へ流用しない。
