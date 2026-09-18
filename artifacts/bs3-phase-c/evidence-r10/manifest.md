# BS3 Phase C evidence revision 10

- generated: 2026-09-19 02:14 JST
- implementation base: `25a5024d310347ef9ce67128636a06e0e0fb5172`
- implementation head: `2842dc44dea6930de4814826ef10489d45117196`
- Phase A snapshot: `artifacts/bs3-phase-a/phase-a-r3.md`, SHA-256 `70E6E7897366327C74F425A24D1FDD174F4735123F083EB961629693DF0B1E47`
- Phase B result: `artifacts/bs3-phase-b/phase-b-r10.md`, SHA-256 `44EBC3B2A295B975231980690185D38FD831DF889E79A97FB6153C1C373DDA3E`

| file | SHA-256 |
| --- | --- |
| `full.diff` | `6920AAC31B30BD563535534D46C045301EFD22E7C6319DFF5209E919DE7704D8` |
| `stat.txt` | `F0B5031322257EC4ED1AA60C1D8909EC95164D81D86069E7DD4A125C1C3F776A` |
| `name-status.txt` | `7DFEDD17E4C850616F93E3D0CDA591415A5EA5DE3A604BE9948A4A786D01DFB9` |
| `contract-audit.txt` | `0D77CF8F4A1EEB7C95757B5FFF0145F229466509C37F80133072BC99683CB424` |
| `docs-audit.txt` | `B2CEF11B25D298561F794C1F8BFCE467F8B6751D3B4950E1393639049F87D173` |
| `raw-checksums.txt` | `B4D7C9EE86A3FB58282E08AF5AF9852DD46E851BBCE8136C270960231D912255` |

`full.diff` は `git diff --binary <base> <head>`、`stat.txt` と `name-status.txt` は同じ両端の `git diff` で生成した。機械検査は `pwsh -NoProfile -File tools/contract-audit.ps1` と `pwsh -NoProfile -File tools/docs-audit.ps1` を実行し、両方 exit 0。文書監査は作業中 HANDOFF の見出しに関する警告 1 件を表示した。

Unity 6000.6.0f1 で `pwsh -NoProfile -File tools/run-tests.ps1 -Filter <name> -WithGraphics` を各フィルタへ実行した。七つの runner はすべて exit 0、XML は合計 135/135、failed 0、skipped 0。内訳は Index 4/4、revision gate 4/4、session 18/18、SceneDirector 63/63、AssetManagement 39/39、SceneVariant forwarding 6/6、実 directory と旧/新 bootstrap の統合 1/1。生 XML/log を `raw/` に複写し、元の `TestResults/` は変更していない。
