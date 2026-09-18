# BS3 Phase C/C' 固定証拠 revision 6

- generated: 2026-09-18 JST
- implementation base: `25a5024d310347ef9ce67128636a06e0e0fb5172`
- implementation head: `c218ecc6d686030a286afc5d559089006d9b50a9`
- Phase A: `artifacts/bs3-phase-a/phase-a-r2.md`, SHA-256 `0DEA239D630F5F40487C53F5733377DBA4FB80D1CE86A85EA0D0C54303CD72D9`
- Phase B: `artifacts/bs3-phase-b/phase-b-r6.md`, SHA-256 `5F7A68FF89639D2B397658BEC3B6047045B23569E81A9D9EC6999400766183B7`

## 固定差分と監査

- `full.diff`: SHA-256 `5851F060E4795EE9115C5F8FB7C71A93900BBE6EDC01CCC68CE0892C39C7E251`
- `stat.txt`: SHA-256 `9F8B11F62F422D6C5E81BE93463A0D9FF36E6126897B7E5A76469542475060A5`
- `name-status.txt`: SHA-256 `79EA3B9D8512C36E7340E2E835F2097EA68560103D638026ABCD9474B66653CF`
- `contract-audit.txt`: `pwsh -NoProfile -File tools/contract-audit.ps1`、exit 0、SHA-256 `0D77CF8F4A1EEB7C95757B5FFF0145F229466509C37F80133072BC99683CB424`
- `docs-audit.txt`: `pwsh -NoProfile -File tools/docs-audit.ps1`、exit 0、SHA-256 `6F026F4AE611E2D246CFD9FC11A555E9FF0DF2E4C4487EF4CBE110DA1944917D`。HANDOFF warning 1 件。

## Unity 生結果

Unity 6000.6.0f1。Editor 終了後、承認済み sandbox 外で `pwsh -NoProfile -File tools/run-tests.ps1 -Filter <name> -WithGraphics` を実行。全 filter で Unity と runner exit 0、XML total > 0、failed = 0。生 XML/log は `raw/`、個別 SHA-256 は `raw-checksums.txt`（SHA-256 `6C6C2EE327B9C90CEB4194D84963D2E0CBFCD33ACA7DF3E481F7AD591B778300`）。

- ContentDirectory: 22/22、`results-ContentDirectory-20260918-212231.xml`
- BuildContentDirectoryIntegrationTests: 1/1、`results-BuildContentDirectoryIntegrationTests-20260918-221047.xml`
- ContentRevisionGate: 3/3、`results-ContentRevisionGate-20260918-212727.xml`
- SceneDirector: 63/63、`results-SceneDirector-20260918-212459.xml`
- AssetManagement: 39/39、`results-AssetManagement-20260918-212610.xml`
- SceneVariantForwardingTests: 6/6、`results-SceneVariantForwardingTests-20260918-212648.xml`

既定 headless では統合テストの Unity XML 1/1 完了後に OS process 終了待ちが長期化した。`-WithGraphics` の最終 run は正常終了した。この違いは後続の Editor テスト環境へ渡す。この束は Phase C/C' の所見を含まない。
