# BS3 Phase C evidence revision 11

- generated: 2026-09-19 02:31 JST
- implementation base: `25a5024d310347ef9ce67128636a06e0e0fb5172`
- implementation head: `f8fc698965ee3a2595754a02d66397edd67133af`
- Phase A snapshot: `artifacts/bs3-phase-a/phase-a-r3.md`, SHA-256 `70E6E7897366327C74F425A24D1FDD174F4735123F083EB961629693DF0B1E47`
- Phase B result: `artifacts/bs3-phase-b/phase-b-r11.md`, SHA-256 `CD1C12F719500AC9AE94BF2F3A02D9764A33FED230696A38F436EF85735120BE`

| file | SHA-256 |
| --- | --- |
| `full.diff` | `47D694E315AEFD41CFAAB7E6166255FA3CC8BED04E57DCCE56C3DD6B794383BF` |
| `stat.txt` | `66B5B0CBED7D01BEB42CB54A8E22FC94C2CD16D599B5A7C2B1F5CA2F66F63013` |
| `name-status.txt` | `5C55BECD02396FDB87DA59ACA1A5CB6191CD9D41545CC74C835DCA7AF3968181` |
| `contract-audit.txt` | `F28692FFAC3237C570317E73C1F4797DD65D0D4DE5DA0EEF336AF95EAEEE0B5D` |
| `docs-audit.txt` | `6CA0EED18631F118F0FA6C084E7BF56B1AA75E96F1A0ADED0B0EB7D660EAED26` |
| `raw-checksums.txt` | `6FC5536C3F69C5E83E68EACCC6690A0147392886FD22B9C1D3D9EA33618F7E98` |

`full.diff` は `git diff --binary <base> <head>`、`stat.txt` と `name-status.txt` は同じ両端の `git diff` で生成した。機械検査は `pwsh -NoProfile -File tools/contract-audit.ps1` と `pwsh -NoProfile -File tools/docs-audit.ps1` を実行し、両方 exit 0。文書監査は作業中 HANDOFF の見出しに関する警告 1 件を表示した。

Unity 6000.6.0f1 で `pwsh -NoProfile -File tools/run-tests.ps1 -Filter <name> -WithGraphics` を各フィルタへ実行した。八つの runner はすべて exit 0、XML は合計 137/137、failed 0、skipped 0。内訳は不正起動設定 2/2、Index 4/4、revision gate 4/4、session 18/18、SceneDirector 63/63、AssetManagement 39/39、SceneVariant forwarding 6/6、実 directory と旧/新 bootstrap の統合 1/1。生 XML/log を `raw/` に複写し、元の `TestResults/` は変更していない。
