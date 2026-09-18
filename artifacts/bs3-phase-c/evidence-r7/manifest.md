# BS3 Phase C/C' 固定証拠 revision 7

- generated: 2026-09-19 JST
- implementation base: `25a5024d310347ef9ce67128636a06e0e0fb5172`
- implementation head: `7997dc4`（コード最終変更 `e722d72`、後続 commit は今回作成した旧 revision 証拠の整理と Phase B snapshot）
- Phase A: `artifacts/bs3-phase-a/phase-a-r2.md`, SHA-256 `0DEA239D630F5F40487C53F5733377DBA4FB80D1CE86A85EA0D0C54303CD72D9`
- Phase B: `artifacts/bs3-phase-b/phase-b-r7.md`, SHA-256 `072B6A33BDDDE0B51F8A75B4DD30053285BE87DB0551E2265484F9E463432D45`

## 固定差分と監査

- `full.diff`: SHA-256 `D62F33E3CE2B3361C5032561F9EFDD16C473ECD9567654D4853B752F72382E91`
- `stat.txt`: SHA-256 `82944C5683B763E356EEC8B8B4C6493E7E440C1E62A56E657816162EE423C44F`
- `name-status.txt`: SHA-256 `9732DBF01D01924ACB47AAC32D51A7CA7F577091B50AA118B167306243DE9028`
- `contract-audit.txt`: `pwsh -NoProfile -File tools/contract-audit.ps1`、exit 0、SHA-256 `0D77CF8F4A1EEB7C95757B5FFF0145F229466509C37F80133072BC99683CB424`
- `docs-audit.txt`: `pwsh -NoProfile -File tools/docs-audit.ps1`、exit 0、SHA-256 `81E5AF20D94BC1A380D5F174CD320C8AD3512491F937B276BEC59A3990B25B4A`。HANDOFF warning 1 件。

## Unity 生結果

Unity 6000.6.0f1、Editor を閉じて承認済み sandbox 外で `pwsh -NoProfile -File tools/run-tests.ps1 -Filter <name> -WithGraphics` を実行。下記の通常 filter は runner exit 0、XML total > 0、failed = 0。生 XML/log は `raw/`、個別 SHA-256 は `raw-checksums.txt`（SHA-256 `2033AB06A93F09543392714924AB494CAB1983464C1050BBF9AFC5E483859DDA`）。

- ContentDirectory: 23/23、`results-ContentDirectory-20260918-225149.xml`
- ContentRevisionGate: 3/3、`results-ContentRevisionGate-20260918-225113.xml`
- SceneDirector: 63/63、`results-SceneDirector-20260918-224833.xml`
- AssetManagement: 39/39、`results-AssetManagement-20260918-225001.xml`
- SceneVariantForwardingTests: 6/6、`results-SceneVariantForwardingTests-20260918-225037.xml`
- BuildContentDirectoryIntegrationTests: XML 1/1 成功、`results-BuildContentDirectoryIntegrationTests-20260918-225853.xml`。Unity log は正常な shutdown 末尾まで達したが OS process が残り、runner は終了しなかったため手動中断した。別の同一 head run `20260918-225319` と `20260919-003843` も XML 1/1 成功後に終了待ちで停止した。`20260919-004410` は XML 作成前に中断した。integration の runner exit 0 はこの head で未確認。前 head `c218ecc` では同 filter の runner exit 0 を一度観測しているが、この束の合格証拠とはしない。

今回作成した旧 revision の複製証拠は最終 tree から外した。旧 review の経緯は commit history に残る。この束は Phase C/C' の所見を含まない。
