# BS3 Phase B 実装結果

- 入力: `artifacts/bs3-phase-a/phase-a-r2.md`（A3 凍結版）
- branch: `codex/bs3-runtime-directory`
- implementation base: `25a5024d310347ef9ce67128636a06e0e0fb5172`
- 実装担当: GPT-5.6 Terra と主担当 GPT-6 Astra
- 実装対象: schema v2 locator/category、root index、Content Directory session/native port、revision/delete gate、AssetManagement owner/cache/Scene route、Editor Play 起動配線、SceneDirector の固定 representation

## 変更と境界

- root は v1/v2、expected identity/target、entry の種別と locator を登録時に検証する。Object は完全一致、Scene のみ Full fallback とした。
- `ContentDirectorySession` は register から unregister までの process 内予約を持つ。caller 取消後も native terminal を観測し、成功資源を解放してから close する。Unity I/O は `IContentNativeDirectory` を通し、fake で terminal と rollback を検証できる。
- `AssetManagement` の既存 Addressables API は維持し、directory 専用の三つの API を追加した。registry/cache の owner 台帳を再利用し、directory token の native 解放を session へ戻す。cache key に path、identity、target、entry、locator を入れる。
- Editor Play の明示 config だけ directory mode を選択する。SceneResource の graph/payload metadata と同期 bootstrap は既存経路を使い、Scene 本体だけ directory entry の logical identity からロードする。
- `ContentRevisionGate.TryAcquireDelete` は物理削除を行わず、同一 process の active session と delete lease の競合を拒否する。

## Phase B での確認

- Unity Editor 6000.6.0f1 の `recompile` / `recompile_status`: `completed`, `failed=false`, errors 0。
- `pwsh tools/contract-audit.ps1`: exit 0、機械契約違反なし。
- Unity バッチテストと Content Directory build は未実行。Phase C に渡す。
- Unity Editor はこの作業で起動した PID 7612 を通常終了し、対象外の SampleGame `.meta` 5件は復元した。

## Phase C の必須検証

- source fixture を削除して移設した実 directory から Scene、Prefab、Texture、二表現をロード・解放する。
- fake session の登録失敗 rollback、取消後 terminal、close/unregister、delete lease 競合を実行する。
- 全テストは規定の `pwsh tools/run-tests.ps1` に `-Filter` を使い、結果 XML の tests > 0 / failed = 0 を確認する。
