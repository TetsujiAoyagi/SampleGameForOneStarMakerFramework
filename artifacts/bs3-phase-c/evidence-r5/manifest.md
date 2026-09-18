# BS3 Phase C/C' 固定証拠 revision 5

- generated: 2026-09-18 JST
- implementation base: `25a5024d310347ef9ce67128636a06e0e0fb5172`
- implementation head: `fec3d503645ebce4032a1fd9c54903fbf4744cdb`
- Phase A: `artifacts/bs3-phase-a/phase-a-r2.md`, SHA-256 `0DEA239D630F5F40487C53F5733377DBA4FB80D1CE86A85EA0D0C54303CD72D9`
- Phase B: `artifacts/bs3-phase-b/phase-b-r5.md`, SHA-256 `6B72DCEB351E7FEA174E9B4C618907F2D87076FABFABCF6448C2C08CD6C9356E`

## 固定差分と監査

- `full.diff`: SHA-256 `1127CADA75618E1E823272E7BC14BE2FAA77CB181C17152041DAE5E1C1C0FA63`
- `stat.txt`: SHA-256 `CCFD17C07F522CC43CAD47AE72EE4E7BB5BF365B90E596FEB6B18F57E8C432E9`
- `name-status.txt`: SHA-256 `BED3098349274F0D19880EC794D927F516F8DA6B9D978E7637477C735FC78988`
- `contract-audit.txt`: `pwsh -NoProfile -File tools/contract-audit.ps1`、exit 0、SHA-256 `13DF56B620F2D3A1419A55656C0C85EC90E543309555D7F5C1AFF74CDCF37250`
- `docs-audit.txt`: `pwsh -NoProfile -File tools/docs-audit.ps1`、exit 0、SHA-256 `625B8320E8A39A9DB47EC21CC4C9AD69F53745B1CBD642586B802F74E944309A`。HANDOFF warning 1 件。

## Unity 生結果

Unity 6000.6.0f1。Editor 終了後、承認済み sandbox 外で `pwsh -NoProfile -File tools/run-tests.ps1 -Filter <name>` を実行。`BuildContentDirectoryIntegrationTests` だけ `-WithGraphics` を追加した。全 filter で Unity と runner exit 0、XML total > 0、failed = 0。生 XML/log は `raw/`、個別 SHA-256 は `raw-checksums.txt`（SHA-256 `DB6B6BF2CEC22387ABA993EA387ECA027A5CC3AAD3192896875551359F4A92DD`）。

- ContentDirectory: 21/21、`results-ContentDirectory-20260918-210201.xml`
- BuildContentDirectoryIntegrationTests: 1/1、`results-BuildContentDirectoryIntegrationTests-20260918-210404.xml`
- ContentRevisionGate: 3/3、`results-ContentRevisionGate-20260918-210618.xml`
- SceneDirector: 63/63、`results-SceneDirector-20260918-210738.xml`
- AssetManagement: 39/39、`results-AssetManagement-20260918-210815.xml`
- SceneVariantForwardingTests: 6/6、`results-SceneVariantForwardingTests-20260918-210856.xml`

既定 headless の integration は同 head で XML/Unity log に 1/1 成功を記録した後の OS process 終了待ちが長期化した。標準ランナーの `-WithGraphics` では正常終了を得た。この違いは後続 Editor テスト環境の入力とし、成功 run の option を固定する。この束は Phase C/C' の所見を含まない。
