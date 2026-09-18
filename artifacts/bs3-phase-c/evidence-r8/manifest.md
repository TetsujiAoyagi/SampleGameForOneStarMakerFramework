# BS3 Phase C/C' 固定証拠 revision 8

- generated: 2026-09-19 JST
- implementation base: `25a5024d310347ef9ce67128636a06e0e0fb5172`
- implementation head: `48b93f8`（最終コード変更 `4cdc0a4`。後続は旧複製証拠整理と HANDOFF の監査前更新）
- Phase A: `artifacts/bs3-phase-a/phase-a-r3.md`, SHA-256 `70E6E7897366327C74F425A24D1FDD174F4735123F083EB961629693DF0B1E47`
- Phase B: `artifacts/bs3-phase-b/phase-b-r8.md`, SHA-256 `076EFEC853B582C33933384B17BF7E9EDEE3D2D06A25A7A4A0DF2A548C719FCE`

## 固定差分と監査

- `full.diff`: SHA-256 `1150F1510DE8A5D4D684B666425AFD3A45A2785C6CE3E448998B1846D6B79BE7`
- `stat.txt`: SHA-256 `F2E8CD19B02D2DA3FBD31643C10A694E388DDC82793561A1C305068C9A0D8643`
- `name-status.txt`: SHA-256 `34EF745D74AD770E43D944E0711A43D253FD1E7FE44EE3A75E1A7CB569E31866`
- `contract-audit.txt`: `pwsh -NoProfile -File tools/contract-audit.ps1`、exit 0、SHA-256 `0D77CF8F4A1EEB7C95757B5FFF0145F229466509C37F80133072BC99683CB424`
- `docs-audit.txt`: `pwsh -NoProfile -File tools/docs-audit.ps1`、exit 0、SHA-256 `077F48E3BB36A0E985543B58321E41855CF394859A6103847C8CA2DBC2F8D362`。HANDOFF harvest warning 1 件。

## Unity 生結果

Unity 6000.6.0f1。Editor を閉じて承認済み sandbox 外で `pwsh -NoProfile -File tools/run-tests.ps1 -Filter <name> -WithGraphics` を実行。以下の各 filter は Unity/runner exit 0、XML total > 0、failed 0。生 XML/log は `raw/`、個別 SHA-256 は `raw-checksums.txt`（SHA-256 `FF258CE07FD255C2CD3E066341A359B137F1D401477B9A5C1A6B3401A9B5F8DC`）。

- ContentDirectorySessionTests: 18/18、`results-ContentDirectorySessionTests-20260919-012007.xml`
- ContentDirectoryIndexTests: 4/4、`results-ContentDirectoryIndexTests-20260919-012048.xml`
- ContentRevisionGate: 4/4、`results-ContentRevisionGate-20260919-005808.xml`
- BuildContentDirectoryIntegrationTests: 1/1、`results-BuildContentDirectoryIntegrationTests-20260919-010629.xml`。実 build、移設、source fixture 削除後の型付きロード・解放に加え、実アプリの既定 Addressables Play と環境変数で選んだ directory Play の登録・Director 生成を観測した。
- SceneDirector: 63/63、`results-SceneDirector-20260919-011416.xml`
- AssetManagement: 39/39、`results-AssetManagement-20260919-011500.xml`
- SceneVariantForwardingTests: 6/6、`results-SceneVariantForwardingTests-20260919-011539.xml`

合計 135/135。広い `ContentDirectory` filter は同じ session/index/integration を一度に拾って XML 23/23 成功したが Unity OS process が残り runner は中断した。上記三つの独立 filter では全 23 件それぞれ exit 0 を得た。広い filter の停止原因は別途調査可能なテスト環境の残存課題として扱い、合格を偽装しない。

旧 revision の複製証拠は最終 tree から外し、commit history に経緯を残した。この束は Phase C/C' の所見を含まない。
