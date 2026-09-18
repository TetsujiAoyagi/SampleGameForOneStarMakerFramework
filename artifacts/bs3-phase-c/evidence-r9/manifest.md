# BS3 Phase C evidence revision 9

- generated: 2026-09-19 01:58 JST
- implementation base: `25a5024d310347ef9ce67128636a06e0e0fb5172`
- implementation head: `7c34c1d9edc136403c1b08d6b5e983613e1cb607`
- Phase A snapshot: `artifacts/bs3-phase-a/phase-a-r3.md`, SHA-256 `70E6E7897366327C74F425A24D1FDD174F4735123F083EB961629693DF0B1E47`
- Phase B result: `artifacts/bs3-phase-b/phase-b-r9.md`, SHA-256 `898304FD9FB014F10C17E05FA2D7BE26D4231CB93E4470D2F06765BB306E63C0`

| file | SHA-256 |
| --- | --- |
| `full.diff` | `6608053333D96B7E2E8C4E8AA7293A84FF7409D47E9F9880A490976648F4C90C` |
| `stat.txt` | `F2E8CD19B02D2DA3FBD31643C10A694E388DDC82793561A1C305068C9A0D8643` |
| `name-status.txt` | `34EF745D74AD770E43D944E0711A43D253FD1E7FE44EE3A75E1A7CB569E31866` |
| `contract-audit.txt` | `0D77CF8F4A1EEB7C95757B5FFF0145F229466509C37F80133072BC99683CB424` |
| `docs-audit.txt` | `077F48E3BB36A0E985543B58321E41855CF394859A6103847C8CA2DBC2F8D362` |
| `raw-checksums.txt` | `0D89998C9E299A2387F7823F3D6056755ECFCCA86DB066ECC1567120C4943B8A` |

`full.diff` は `git diff --binary <base> <head>`、`stat.txt` と `name-status.txt` は同じ両端の `git diff` で生成した。機械検査は `pwsh -NoProfile -File tools/contract-audit.ps1` と `pwsh -NoProfile -File tools/docs-audit.ps1` を実行し、どちらも exit 0。後者は HANDOFF の Phase C/C' 見出しに関する警告 1 件を表示した。

Unity 6000.6.0f1 の `pwsh -NoProfile -File tools/run-tests.ps1 -Filter <name> -WithGraphics` による EditMode raw XML と log は `raw/` に複写し、元の `TestResults/` は変更していない。Index 4/4、revision gate 4/4、session 18/18、SceneDirector 63/63、AssetManagement 39/39、SceneVariant forwarding 6/6 は各 runner が exit 0。

実 directory と旧/新アプリ起動を含む統合テストは 3 回実行した。`014007` は XML 1/1 成功、`014617` は 180 秒の NUnit timeout で XML 0/1、`015308` は XML 1/1 成功。3 回とも Unity の終了ログと XML が出た後に process/runner が戻らず、runner の exit 0 は得られなかった。待機を中断したため shell exit は 1。この境界を隠さず、XML/log の機能判定と runner 判定を分けてレビューする。Unity 外で `TestResults/` に保存済みの以前の実装 revision 8 統合結果は runner exit 0 だったが、今回の固定 head の成功証拠には数えない。
