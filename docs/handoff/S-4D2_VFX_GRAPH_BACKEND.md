# S-4d2 VFX Graph 導入と SampleGame backend

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `D`（Phase D 完了。§9）
- branch: `claude/s-4d2-vfx-graph-backend`
- implementation base commit: `2734a6ea`（`develop`、PR #142 のマージ）
- implementation head commit: `628b0b53`（Phase B 差し戻し 3 巡目の出口。**GO 候補 head として固定**、ローカル軽量 tag `evidence/s-4d2-vfx-graph-backend/1`（push しない）。前の head は `2a82ace2`、その前は `2055069a`・`77a3a0b3`）
- risk: `high`（パッケージ導入、Framework の公開 API と起動・終了順序、所有者・寿命、Scene lifecycle への配線）
- owner: 発注者 / S-4d2 担当
- created: 2026-10-21
- expires: S-4d2 の Phase D 完了時
- harvest to: Architecture §24 §16（VFX 抽象）へ composition・Tick 登録・backend 契約の確認結果を追記する。§04（起動）は起動・終了順序の 1 行だけを追記する（§3.4）
- workflow task: `s-4d2-vfx-graph-backend`（2026-10-21 に `workflow-task.ps1 start` 済み。Phase B で再度 start しない）
- Phase A snapshot の取得案内・状態: A2 入力版は commit `87a9690a`（content sha256 は §5「A2 独立レビュー」）。**A3 凍結版は commit `17144576`、本書の content sha256 `856d910bff68e8bc3a3e8c4f851ca5eceddf8abee36a57730d7c57b1de057690`**（`git show 17144576:docs/handoff/S-4D2_VFX_GRAPH_BACKEND.md` の LF 内容の sha256。この値は凍結版を指し、これを書き足した後の本書は指さない）。凍結 commit は program（`S-4_FULL_SPEC_WORLD_AUTHORING.md` の §6.1・§6.3 d2〜d4 行・§10）の更新も含む。凍結 commit の後の記録用 commit はこの §0 だけを変え、凍結条件を変えない。文書だけなので stage は不要
- Phase B result snapshot の取得案内・状態: 未生成（Phase B の結果は commit `628b0b53`（差し戻し 3 巡目）と §6 で引き渡す。`evidence stage` はしていない）
- evidence bundle の取得案内・状態: ローカル固定済み（未昇格、`locality=local`）。head `628b0b53` / base `2734a6ea`、判定 C（2026-10-25）が `evidence stage` し、別 process の `fetch` で期待 hash 照合・12 file の閲覧を確認済み。引渡し先は進行役（取得案内は共有ノート `judgment-C-evidence.md`）。担当は判定 C の session
- C' blind bundle の取得案内・状態: ローカル固定済み（未昇格、`locality=local`）。同じ head・base。HANDOFF と発見 C・判定 C の所見を除いた diff・凍結 A3・生 XML/log/audit を収録（10 file）。別 process の `fetch` で hash 照合・閲覧を確認済み。C' 1 巡目で進行役が改めて `fetch` し、hash 照合が通った（§8）
- 引継ぎ票（C' → Phase D）: C' 1 巡目（最終）は blocker 0（§8）。判定 C も阻害する指摘 0（§7）。GO 候補 head は `628b0b53`（tag `evidence/s-4d2-vfx-graph-backend/1`）、base `2734a6ea`。次は Phase D（人間）: 判定 C（§7）と C'（§8）の突き合わせ（C' の後続入力を含む）、Evidence の保存・引渡しの確認（evidence bundle・C' blind bundle はローカル固定済み（未昇格）。取得案内は進行役の共有ノート）、push・PR 作成（base `develop`）の判断、harvest（§0 の harvest to）と HANDOFF の削除、`workflow-task.ps1 end`。push していない。作業ツリーは未追跡の `docs/handoff/DEBUGSTUDIO_S2_CUT_PLAN.md` を除いて clean。
- 引継ぎ票（AC14 裁定 → C'、経緯として残す）: 人間が AC14 の差を許容した（§7 決定記録 D2）。次は C' 1 巡目（新しい session、B・C と異なるモデル。予約は `cursor-agent` の Grok）。入力は C' blind bundle の取得案内だけで、判定 C・発見 C の所見と D1・D2 は渡さない。上限 3 巡。push・PR は人間が決める。
- 引継ぎ票（判定 C → 人間の AC14 裁定と C'、経緯として残す）: 判定 C は head `628b0b53` で完了（§7）。次は (1) 人間が AC14 の差（`-nographics` で Inconclusive 13、全て本スライスの Play 中のテスト）を裁定し決定記録に書く、(2) 新しい session が C' blind bundle（取得案内は上の欄）から監査する（上限 3 巡。C' に判定 C・発見 C の所見を渡さない）。push・PR は人間が決める。
- 引継ぎ票（GO 候補 head の固定 → 判定 C、経緯として残す）:
  - 前の Phase の結果: 発見 C は第3巡で阻害する指摘 0（§7）。履歴は整理しない（発注者の決定。§7「決定記録」D1）。GO 候補 implementation head は `628b0b53`、ローカル軽量 tag `evidence/s-4d2-vfx-graph-backend/1`。base は `2734a6ea`。
  - 次の Phase・役割: **判定 C（別 session）**。GO 候補 head `628b0b53` で、§5 の判定必須テスト（`pwsh tools/run-tests.ps1 -WithGraphics`、空 filter）と、AC14 のための既定（`-nographics`、空 filter）の全回帰を実行し、AC13 の機械検査（`git diff --name-status 2734a6ea 628b0b53`）、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1` を行う。XML で §3.1 のテスト 4 ファイルの収録、failed 0、本スライスのテストの Inconclusive・Ignored 0（AC5・AC12 の `Supported` が passed）を確かめ、既定との差（failed・Inconclusive・Ignored の集合）を AC14 として記録する。差の扱いは人間の裁定（決定記録）に返す。evidence bundle と C' blind audit bundle は review-evidence に従い同じ commit から作り、§0 の取得案内欄の状態を更新する。判定 C は C' を起動しない。
  - 引き継ぐ commit: branch の先頭（この §0・§7 を書いた記録用 commit。`628b0b53` との差は HANDOFF だけ）。push していない。作業ツリーは未追跡の `docs/handoff/DEBUGSTUDIO_S2_CUT_PLAN.md` を除いて clean。
  - 運用の注意: 発見 C 第0巡の stall した Unity（PID 27716）は残置。実行の前に、この project の Unity process を確かめる。
  - 必要な権限・停止規則・出口・モデル: 下の旧の記載（発見 C 第0巡の前の記録）と同じ。判定 C の担当は Phase B と異なるモデル。
- 引継ぎ票（発見 C 第3巡 → 進行役、経緯として残す）: 発見 C 第3巡は head `628b0b53` で起点 filter 132 件中 passed 132・failed 0。阻害する指摘 0（§7）。次は未公開の試行錯誤 commit の整理 → GO 候補 head の固定とローカル軽量 tag → 判定 C（下の旧記載の順序 (2)〜）。AC5 の補助の描画は U3 の (1) の範囲と判断した（§7）。巡は 3/4 で確定。Unity は正常終了（stall なし。PID 27716 は第0巡の残置のまま）。
- 引継ぎ票（Phase B 差し戻し 3 巡目 → 発見 C 第3巡、経緯として残す）:
  - 前の Phase の結果: Phase B 差し戻し 3 巡目が完了（implementation head `628b0b53`、前の head `2a82ace2` からの差は `VfxGraphRenderPlayTests.cs` の 1 ファイルだけ。§6「差し戻し 3 巡目」）。F1 の食い違いの原因を batch の Editor で切り分けた: 起動したばかりの Editor の process では、通常の描画ループ（有効なカメラ）が一度も VFX を描いていないと、明示の `camera.Render()` と SRP の render request（`StandardRequest`）のどちらにも粒子が入らない。一度描かれると同じ process の間は入る。第2巡の B の成功は、同じ process で先に有効なカメラの実験をしていたため（第2巡の「旧順序の混在で映らない」という判断は誤りの可能性が高い）。AC5 のテストは、計測の前に補助のカメラ（別の RT）に別の instance を描かせ、両方を壊してから凍結済みの手順で計測する。閾値・上限・計測の経路は変えていない。これを §2.7 U3 の (1) の適用（Editor で有効性を確かめた明示の描画経路）として扱った。Phase A の再開に当たるかの判断は下の「発見 C への依頼」。Unity テストは未実行。batch の Editor でコンパイルエラー 0（本スライスのファイルの警告 0）。contract-audit・docs-audit は errors 0・warnings 0。
  - 次の Phase・役割: **発見 C 第3巡（別 session）**。head `628b0b53` に対して §5 の起点 filter `OneStarMaker.Tests.Rendering.Vfx;SampleGame.Tests.Rendering.Vfx` を `-WithGraphics` で実行し、F1 の解消と他のテストの回帰を確かめる（全回帰は判定 C まで回さない）。`Render_SpawnedVfxAppearsInCameraOutput` の出力に `warmUpLitPixels`・`warmUpFrames`・`afterWarmUp enabledCameras=… visualEffects=…` が増えている。
  - 発見 C への依頼: 補助の描画（warm-up）を足したことが、AC5 の (3)「自動描画に頼らない」の凍結した手順の変更（Phase A の再開）に当たるか、U3 の (1) の範囲（検証手段の変更で、合格条件は緩めていない）かを判断する。B は後者と判断した。計測の RT に書くのは明示の Render だけで、計測の前に補助のカメラと instance は壊れている。前者と判断するなら人間へ返す。
  - 運用の注意: 発見 C 第0巡の stall した Unity（PID 27716）は残置（Phase B は終了させていない）。Phase B が起動した batch の Editor（6 回）は全て正常終了させた。`Temp/UnityLockfile` は無い。
  - 巡のカウント: B と発見 C の間で 3/4（上限 4）。次の発見 C で阻害する指摘が残れば、最後の 1 巡になる。
  - 引き継ぐ commit: branch `claude/s-4d2-vfx-graph-backend` の先頭（この §0・§6 を書いた記録用 commit。implementation head `628b0b53` の 1 つ後で、HANDOFF だけを変える）。push していない。作業ツリーは未追跡の `docs/handoff/DEBUGSTUDIO_S2_CUT_PLAN.md`（本スライスと無関係）を除いて clean。
  - 必要な権限・停止規則・出口・モデル: 下の旧の記載（発見 C 第0巡の前の記録）と同じ。
- 引継ぎ票（発見 C 第2巡 → Phase B 差し戻し 3 巡目、経緯として残す）: 発見 C 第2巡の結果は §7。head `2a82ace2` で起点 filter 132 件中 failed 1（F1 のみ残る: 粒子は生存・非 culled だが `camera.Render()`+RT の litPixels=0。F2 は解消）。B の第2巡の実測と食い違う。巡は 3/4。引き継ぐ commit は branch の先頭（この記録 commit）。Unity PID 27716（既知 stall）は残置。以下は前の引継ぎ票として残す。
- 引継ぎ票（Phase B 差し戻し 2 巡目 → 発見 C 第2巡）:
  - 前の Phase の結果: Phase B 差し戻し 2 巡目が完了（implementation head `2a82ace2`、前の head `2055069a` からの差は SampleGame の 3 ファイルだけ。§6「差し戻し 2 巡目」）。F2 は原因を Editor の実測で確かめて修正（`initialEventName` を `visualEffectAsset` より先に書く）。F1 は F2 と同じ原因と判断した: batch の Editor で、旧順序の instance は粒子が生きていても `camera.Render()` + RT に映らず、新順序では映った。AC5 の経路は不成立ではないので、§2.7 U3 の (1) には進めていない（AC5 の手順・閾値・上限は変えていない）。Phase A の再開に当たる事実は無い。Unity テストは未実行。Unity のコンパイル（batch の Editor）はエラー 0、本スライスのファイルの警告 0。contract-audit・docs-audit は errors 0・warnings 0。
  - 次の Phase・役割: **発見 C 第2巡（別 session）**。head `2a82ace2` に対して §5 の起点 filter `OneStarMaker.Tests.Rendering.Vfx;SampleGame.Tests.Rendering.Vfx` を `-WithGraphics` で実行し、F1・F2 の解消と他のテストの回帰を確かめる（全回帰は判定 C まで回さない）。F1 が残れば、§6「差し戻し 2 巡目」の実測と食い違う理由（batch の条件の差）を出力から切り分けて B へ戻す。
  - 運用の注意: 発見 C 第0巡の stall した Unity（PID 27716、`-runTests` でこの project）はまだ残っている。Phase B は終了させていない。Phase B が起動した Editor（GUI 1 回、batch 1 回）は正常終了させた。`Temp/UnityLockfile` は無い。
  - 巡のカウント: B と発見 C の間で 2/4（上限 4）。
  - 引き継ぐ commit: branch `claude/s-4d2-vfx-graph-backend` の先頭（この §0・§6 を書いた記録用 commit。implementation head `2a82ace2` の 1 つ後で、HANDOFF だけを変える）。push していない。作業ツリーは未追跡の `docs/handoff/DEBUGSTUDIO_S2_CUT_PLAN.md`（本スライスと無関係）を除いて clean。
  - 必要な権限・停止規則・出口・モデル: 下の旧の記載（発見 C 第0巡の前の記録）と同じ。
- 引継ぎ票（発見 C 第1巡 → Phase B 差し戻し 2 巡目、経緯として残す）: 発見 C 第1巡の結果は §7。head `2055069a` で起点 filter 132 件中 failed 2（F2: `initialEventName=''` でも Create 後 frame 3 で粒子が出る / F1: 粒子は生存・非 culled だが `camera.Render()`+RT に出ない = AC5 の経路不成立、U3 の (1) へ）。F3・F4 は解消。巡は 2/4。引き継ぐ commit は branch の先頭（この記録 commit）。以下は前の引継ぎ票として残す。
- 引継ぎ票（Phase B 差し戻し 1 巡目 → 発見 C 第1巡）:
  - 前の Phase の結果: Phase B 差し戻し 1 巡目が完了（implementation head `2055069a`、前の head `77a3a0b3` からの差は SampleGame の 4 ファイルだけ。§6「差し戻し 1 巡目」）。F2・F3・F4 は修正、F1 は原因を断定できないので、切り分けの計測をテストへ足した（F2 の修正後の再測定で通るか、発射側か描画経路側かを XML の出力で分けられる）。Phase A の再開に当たる事実は無い。Unity テストは未実行。Editor のコンパイル確認は未実施で、代わりに Unity 生成の csproj を `dotnet build` した（エラー 0・警告 0）。contract-audit・docs-audit は errors 0・warnings 0。
  - 次の Phase・役割: **発見 C 第1巡（別 session）**。head `2055069a` に対して §5 の起点 filter `OneStarMaker.Tests.Rendering.Vfx;SampleGame.Tests.Rendering.Vfx` を `-WithGraphics` で実行し、F1〜F4 の解消と他のテストの回帰を確かめる（全回帰は判定 C まで回さない）。F1 が残れば、`Render_SpawnedVfxAppearsInCameraOutput` の出力（`maxAliveParticles` 等）で発射側か描画経路側かを決め、描画経路側（AC5 の定義の「経路が不成立」）なら §2.7 U3 の (1) へ進めるため B へ戻す。前の head `77a3a0b3` からの実装の変更は修正 1 commit（`2055069a`）だけで、他は HANDOFF の記録用 commit。
  - 運用の注意: 発見 C 第0巡の Unity（既知の native 終了 stall）が、この worktree の project を `-runTests` で開いたまま残っている（2026-10-25 の Phase B 時点。`Temp/UnityLockfile` は無い）。Phase B は終了させていない。次の実行の前に、Unity の process がこの project に残っていないかを確かめる。
  - 巡のカウント: B と発見 C の間で 1/4（上限 4）。
  - 引き継ぐ commit: branch `claude/s-4d2-vfx-graph-backend` の先頭（この §0・§6 を書いた記録用 commit。implementation head `2055069a` の 1 つ後で、HANDOFF だけを変える）。push していない。作業ツリーは未追跡の `docs/handoff/DEBUGSTUDIO_S2_CUT_PLAN.md`（本スライスと無関係）を除いて clean。
  - 必要な権限・停止規則・出口・モデル: 下の旧の記載（発見 C 第0巡の前の記録）と同じ。
  - （以下は発見 C 第0巡 → Phase B 差し戻し 1 巡目の引継ぎ票。経緯として残す）
  - 前の Phase の結果（旧）: 発見 C 第0巡が完了（対象 base `2734a6ea` → head `77a3a0b3`、§7）。先行の全回帰（`-WithGraphics`、空 filter）は 1283 件中 failed 4。4 件とも本スライスの Play 中テストで、阻害する指摘 4 件（§7 の F1〜F4）。
  - 次の Phase・役割（旧）: **Phase B（差し戻し 1 巡目 = 1/4、別 session）**。B の入力はこの HANDOFF（§7）だけ。直す対象は §7 の F1〜F4。B 適応で足りるものは修正し、Phase A の再開に当たる事実（§4 の差し戻し条件、AC・公開面の変更）が出たら止めて返す。B は判定 C・C' を起動しない。
  - 確認（旧）: §5 の起点 filter `OneStarMaker.Tests.Rendering.Vfx;SampleGame.Tests.Rendering.Vfx` を `-WithGraphics` で実行（Phase C の責任の Unity テストを、差し戻し中の限定確認として発見 C の規則で行う。担当は B ではなく発見 C 側の session。B はコンパイル確認と contract-audit・docs-audit まで）。F1 は §7 のとおり、AC5 が U3 に当たるかを切り分けるまで U3 の順へ進まない。
  - （以下は発見 C 第0巡の前の記録。経緯として残す）
  - 前の Phase の結果（旧）: Phase B 完了（implementation head `77a3a0b3`、§6）。動作確認は AC1 のコンパイル確認 1 件で、`未実施` なし（§6「動作確認」）。Unity テストは未実行だった。
  - 次の作業（順序、旧）: (1) 発見 C: base `2734a6ea` → head `77a3a0b3` の固定した差分の構造・契約・凍結済み失敗経路を見る。§5「発見 C での先行の全回帰」の `pwsh tools/run-tests.ps1 -WithGraphics`（空 filter）を最初の巡で 1 回だけ回し、同じ head の既定（`-nographics`）の全回帰との差を人間へ返す（判定の証拠にしない）。差し戻しは B と往復する（上限 4 巡。B は Phase B と同じ規則で別 session）。(2) 阻害する指摘が無くなったら、未公開の試行錯誤 commit を整理して GO 候補 head を固定し、ローカル軽量 tag `evidence/s-4d2-vfx-graph-backend/<n>` を付ける。(3) 判定 C: GO 候補 head で §5 の判定必須テスト（`pwsh tools/run-tests.ps1 -WithGraphics`、空 filter）と AC13 の機械検査を行い、AC5・AC12 の `Supported` のケースが Inconclusive でなく passed であること、AC14 の既定との差を記録する。AC5 が §2.7 U3 の不成立なら U3 の順に切り替える。(4) C': 判定 C の証拠が揃った head で、blind audit bundle から新しい session で監査する（上限 3 巡）。(5) 全て終わったら §7・§8 と、この引継ぎ票を Phase D（人間）向けに書き換えて止まる。
  - モデル: Phase C は Phase B（Anthropic、Claude Opus 系列）と異なるモデル。C' は B・C の両方と異なるモデルで、Phase A・B で使っていない xAI 系列（`cursor-agent` の Grok）を予約してある。使った担当・モデルは §7・§8 に実績として書く。
  - 引き継ぐ commit: branch `claude/s-4d2-vfx-graph-backend` の先頭（発見 C 第0巡の記録用 commit。implementation head `77a3a0b3` の 3 つ後で、HANDOFF だけを変える）。implementation base は `2734a6ea`。push していない（同じ PC のローカル SHA で照合する）。開始前からある未追跡の `docs/handoff/DEBUGSTUDIO_S2_CUT_PLAN.md` は本スライスと無関係なので、作業ツリーの照合ではこの 1 件を除いて clean。
  - 必要な権限: Unity のバッチテストは Editor を閉じてから、最初の実行から sandbox 外の承認済み経路で `pwsh tools/run-tests.ps1` を起動する（osm-workflow の Phase C）。Phase B の Editor の Console に Unity のライセンスの通知（§6）が出ていたので、起動ログのライセンス接続を最初に確かめ、進まなければ osm-workflow の手順で止める（license の返却・再発行を回避策にしない）。証拠の保存と引渡しは Artifacts README の事前承認と review-evidence に従う。
  - 停止規則: §4「Phase B から Phase A へ差し戻す条件」と osm-workflow の blocker の基準・巡の上限。上限で収束しない、AC14 の差の扱い（決定記録）、U3 の (2) の画像判断は人間に返す。C' に発見 C・判定 C の結論・指摘を渡さない（C' は PR と可変な HANDOFF 全文を読まない）。
  - 出口: push・PR 作成は Phase D の前に人間が決める（進行役はしない）。

## 1. 目的と対象外（A0）

- 目的: 現行 project（Unity 6000.6.0f1、URP `17.6.0`）に VFX Graph を入れ、d1 の抽象（`IVfxSystem` / `IVfxBackend`）越しに PC で 1 件描画し、非対応環境では VFX を有効化せずに安全に止める。d1 が後続へ送った composition（生成場所・Tick 登録・Scene lifecycle への配線・終了順序）を決めて実装する。
- 対象外:
  - 実証 Scene（`Spring_AtmosphereVFX` / `Spring_VFX_4_2` は d3、`Spring_Events_4_2` は d4、`Spring_Event_MultiCellProof` と Planner 実証は d5）。
  - Mobile 用 ParticleSystem fallback（program の製品判断で作らない）。
  - 同時数の上限、優先度、カメラ距離での間引き、pool / prewarm、DebugStudio での観測（program §6.4 で最初は入れない）。
  - Game が `IVfxSystem` を受け取る経路（Game の最初の利用者は d4。§2.5）。
  - prefab を source にした `Spawn`（§2.5）。
- 現況: 下記 §1.1〜§1.4。

### 1.1 program から転記した制作契約

（`docs/handoff/S-4_FULL_SPEC_WORLD_AUTHORING.md` §2.1〜§2.4・§6.1・§6.2・§6.3 d2 行・§6.4・§10・§11・§12 から、d2 に必要なものを転記した。）

- **SceneGraph と寿命（§2.1）:** `Season_*` は Scene payload を持たない論理ノード。Cell 配下の職種 Scene（Environment / Lighting / VFX / Events）は `StreamByDistance = false`、`LoadType.OnDemand` で、親 Cell が Stable になった後に SampleGame の companion loader が明示 `AddScene` し、Unload は親 Cell の再帰破棄へ委ねる。`Spring_AtmosphereVFX` は Season ノードの子（`IncrementalAlways` / optional）。
- **職種の所有物（§2.2）:** Cell VFX は Cell 常駐中ずっと続く環境エフェクト、Season Atmosphere VFX は季節全域の常時エフェクト。VFX Scene にゲーム進行 trigger を置かず、Events Scene が VFX の開始・停止を要求する。Scene を跨ぐ `GameObject` の直接参照は禁止し、event id、world position、service query、`IAssetManagement` で寿命を宣言してロードする prefab で接続する。
- **VFX の粒度と寿命（§2.4）:** Cell 常駐中ずっと必要な複数エフェクトは 1 Cell 1 VFX Scene に束ねる。一時的な VFX は Scene にせず prefab / `VisualEffectAsset` にし、`IAssetManagement` でロードして `AssetOwner.Scene(eventSceneIdentity)` または `AssetOwner.Bind(go)` で寿命を宣言する。
- **VFX Graph（§6.1）:** `com.unity.visualeffectgraph` を導入する。採用 version は d2 の Phase A で互換性を確認して固定する（本書 §1.4 で `17.6.0` に決めた。旧指定 `17.5.0` は現行 URP `17.6.0` と一致しないので使わない）。必要な Runtime assembly reference だけを SampleGame 側 asmdef へ追加する。PC Quality で描画を必須受入とする。Mobile では起動時に backend が capability を判定して対応可否だけを返し、非対応なら `VisualEffect` を有効化しない。診断ログは Framework が一度だけ出し、backend と Scene 側は出さない。Mobile 用 ParticleSystem fallback は作らない。PC 必須・Mobile 安全停止を正式な製品判断とする。
- **§6.3 d2 行:** 答える問いは「現行 project（URP `17.6.0`）に VFX Graph を入れ、d1 の抽象越しに PC で描画し、非対応環境で安全に止められるか」。範囲は §6.1 の version 互換確認と固定、SampleGame 側 asmdef への Runtime assembly 参照（実名は Editor の import 後に確定）、起動時の capability 判定（§6.4 の backend 側）、backend 実装、Scene に配置済みの `VisualEffect` を止める仕組み（Component が有効化される前に適用できるかを検証する）、確認用の最小 asset。対象外は実証 Scene（d3〜d5）。d1 から送られた必須事項と、d1 の C / C' から送られた入力（採否は d2 の Phase A で決める）は §2.4 で一つずつ裁定する。
- **§6.4 責務配置:** 抽象は Framework、VFX Graph に依存する実装は SampleGame に置く。Framework の責務は (1) 寿命の宣言と回収、(2) 非対応環境での停止（backend が起動時に 1 回対応可否を返し、Framework は非対応なら何もしない handle を返して診断ログを 1 回だけ出す。Game 側に分岐を書かせない）、(3) 型に依存しない handle。Scene に置く VFX（d3）の寿命は Scene の寿命で回収されるので、d1 の寿命管理の対象は動的に起動する VFX（d4 / d5）である。非対応時の停止は両方に適用する。何もしない handle は Scene に配置済みの `VisualEffect` を止めないので、配置済みのものを止める処理は SampleGame の backend が持つ（d2 で入れ、d3 で実 Scene を確認）。Scene 側は起動時の判定結果を見るだけで、追加の診断ログを出さない。
- **§10 公開 API と依存:** Framework は season 名、Cell 座標文法、職種 role を知らない。VFX Graph 依存は SampleGame 側に閉じ、`OneStarMaker.Runtime` の公開 API へ `VisualEffect` 型を出さない。`SceneState` の 14 値は変更しない。新しい `LoadType` も追加しない。asmdef 参照追加は d2 の HANDOFF で `Unity.VisualEffectGraph.Runtime` の実 assembly 名を確認してから固定する。
- **§11 S-4d の検証:** PC で Season / Cell VFX を表示できる（d2 で 1 件）。Mobile 非対応時に例外、未解放 handle、無限 retry がない（d2 は実 backend）。`unity test` / `unity run`、Cloud での Unity CLI、Scene / asset YAML の手編集は禁止。旧大量生成器を復活させない。
- **§12 境界:** program に無い公開 API、依存、所有者、寿命は d2 の Phase A で決め、program の該当行も Phase A の PR で更新する。Phase B で決めない。

### 1.2 d1 の現況（Architecture §24 §16 と実装）

- 配置: `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/Vfx/`（`Abstractions/`・`Implements/`・`Components/`）、namespace `OneStarMaker.Runtime.Rendering.Vfx`、asmdef `OneStarMaker.Runtime`。公開型は `IVfxSystem`、`VfxSystem`、`IVfxHandle`、`VfxSpawnRequest`、`IVfxBackend`、`VfxInstanceId` の 6 つ。
- `VfxSystem`（471 行）: ctor `(IVfxBackend, ILogger<VfxSystem>, float gracefulStopTimeoutSeconds = 10f)` の中で `QueryCapability` を 1 回だけ呼ぶ。false か例外なら以後 backend を 0 回呼び、`LogWarning`（例外なら `LogError`）を 1 回出す。`BindSceneLifecycle(Observable<SceneEvent>, ISceneQuery)` は SceneDirector の生成直後・最初の Scene のロード前に呼ぶ約束（途中接続の状態同期は約束しない）。`Tick(float)` は消え切り待ちの timeout と破棄済み `Bind` go の検出だけを行い、所有者の終端は Tick に依存しない。`Dispose` は印 → 購読解除 → 全 instance 回収。
- 現在の `Tick` は instance 0 でも `_index.SnapshotAll()` と `_index.SnapshotBindOwners()` で毎回 `List` を 2 つ確保する。
- `BindSceneLifecycle` は `IsSceneLifecycleOpen = true` を `Subscribe` より前に立てる。`Subscribe` が例外を投げると lifecycle が開いたまま購読なしになり、Scene 所有の `Spawn` を受理し続ける（d1 の C / C' の入力）。
- `IVfxBackend` の契約（XML doc が正）: main thread だけ、`QueryCapability` は生涯 1 回、`Create` は作るだけで発射は `Play` だけ、`Create` が id を返す前に失敗したら backend が後始末、同時に生存する instance に同じ id を返さない、`Destroy` は id ごとに高々 1 回で破棄済み GameObject・失効した source に対しても安全に完了、backend はログを出さない。
- d1 は `UpdateSystemRuntime` に登録しておらず、どこからも `VfxSystem` を生成していない（参照は Framework 内とテストだけ）。

### 1.3 起動・終了と更新の現況（A0 の調査）

- **起動:** `AbstractApplicationInitializer`（1196 行、`unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/`）。`BeforeSceneLoad` の `InitializeBeforeSceneLoad` が AssetManagement → Config → `UpdateSystemHost` → DebugSocket → `_loggerFactory` の順に作る。`AfterSceneLoad`（非同期）で `CreateSceneFactory` → Scene Variant 解決 → Content Directory 登録 → `new SceneDirector(...)` → `_updateSystemHost.BindSceneDirector(...)` → `RegisterAlreadyLoadedScenes`（Editor の Play で開いていた Scene を登録）→ 最初の Scene の `AddScene`。SampleGame（`SampleGame/DependOnAll/AppInitializer.cs`、422 行）は `CreateSceneFactory` などの override と、`BeforeSceneLoad` の後に App 常駐の CameraSystem / RenderEnvironment / Profiler を自前で作る。SceneDirector の生成直後に Game が割り込む hook は無い。
- **Editor の Play で開いていた Scene:** Unity は `BeforeSceneLoad` の後、`AfterSceneLoad` の前にその Scene の Awake / OnEnable を行う。`SceneDirector` はまだ無いが、`BeforeSceneLoad` で作ったものはこの時点で使える。
- **終了:** `ReleaseAll`（`SubsystemRegistration` と `Application.quitting` の両方から呼ぶ、冪等）は、CTS cancel → DebugSocket → `_updateSystemHost.Dispose` → `_loggerFactory.Dispose` → … → `_sceneDirector.Dispose`（論理台帳と SceneBase のみ。AM の Unload は呼ばない）→ `AssetManagement.ReleaseAll` の順。SampleGame の CameraSystem 等は別の `Application.quitting` handler と `SubsystemRegistration` で解放している（Framework の handler の後に登録される）。
- **更新:** `UpdateSystemRuntime`（静的入口）/ `UpdateCoordinator.RegisterElement(layerId, IUpdateElement, layerOrder)`。layer 名と順序は `UpdateLayerIds`（`Input` -100、`Camera` 50、`Streaming` 60）。CameraSystem は `BeforeSceneLoad` 直後に Camera layer へ登録し、`ActivatePendingRegistrations()` で即時 active にしている（Scene 安定待ちの gate を経由しない）。`IUpdateElement.OnElementUpdate(in UpdateFrameContext)` は `DeltaTime` と `UnscaledDeltaTime` を持つ。

### 1.4 VFX Graph package の現況（A0 の調査）

- Unity 6000.6.0f1 のインストールに同梱の core package は `com.unity.visualeffectgraph` **17.6.0**（依存 `com.unity.shadergraph` 17.6.0・`com.unity.render-pipelines.core` 17.6.0）。現行 manifest の `com.unity.render-pipelines.universal` 17.6.0 と同じ版で、依存の版も一致する。VFX Graph は core package で、Editor の版ごとに同梱版が決まる（package の `System-Requirements.md`）。
- Runtime の asmdef は `Unity.VisualEffectGraph.Runtime`（同梱 package の `Unity.VisualEffectGraph.Runtime.asmdef` で確認）。`VisualEffect` / `VisualEffectAsset` は engine module `UnityEngine.VFXModule` にあり、asmdef 参照なしで使える（engine 参照は既定で入る）。
- 同梱文書による実行要件: compute shader 対応（`SystemInfo.supportsComputeShaders`）、SSBO 対応（`SystemInfo.maxComputeBufferInputsVertex > 0`）、OpenGL ES 非対応、URP では gamma color space 非対応。URP では preview 扱いの platform がある。
- project の設定: color space は Linear（`m_ActiveColorSpace: 1`）。Android の graphics API は Vulkan と OpenGLES3 の手動指定。Quality は `Mobile` と `PC` の 2 つで、現在は `PC`。`ProjectSettings/VFXManager.asset` は shader 参照が空の初期状態（package の import で Editor が埋める）。
- テスト: `tools/run-tests.ps1` は既定で `-nographics`、`-WithGraphics` で外せる。PlayMode platform の採用例は無く、Editor-only asmdef の EditMode テストで `EnterPlayMode` / `ExitPlayMode` を使う前例がある（`SampleGame.Tests.Editor` の UI テスト等）。`SampleGame.Tests`（Editor-only）は `SampleGame.Common` を参照する。`SampleGame.Tests.Editor` は参照しない。

## 2. 意思決定と受け入れ境界（A1）

- このスライスが答える問い: VFX Graph 17.6.0 を現行 project に入れ、d1 の抽象の実 backend と Framework の composition を通して、PC では 1 件描画でき、非対応環境では `VisualEffect` を 1 つも有効化せずに止まるか。
- 判定定義: 進める最低条件（§2.1）を全て満たせば GO。一つでも未達なら NO-GO。スパイクではないので CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.1 進める最低条件

保証の範囲: d2 が保証するのは、(a) package と backend が現行 project で動くこと、(b) Framework の composition が d1 の約束（配線順・Tick・終了順序）を満たすこと、(c) 非対応の判定結果が 1 つの出所から動的 VFX と配置済み VFX の両方に効くこと。実 Scene への配置（d3）、Event からの起動（d4）、Player での描画は保証しない（§2.5）。Mobile 実機での判定は行わず、判定の規則と、platform の事実を差し替えた実 backend の非対応経路をテストする（§2.7 U2。実機の実証とは記録しない）。

**不変条件（起きてはいけないこと）**

- **I1 非対応で有効化しない:** 判定が非対応（または判定不能）のとき、backend の `Create` は 0 回、`PlacedVfxActivator` を付けた配置済み `VisualEffect` の `enabled` は false のまま、backend と `PlacedVfxActivator` のログは 0 件、例外 0 件。診断ログは `VfxSystem` の 1 件だけ。
- **I2 判定の出所は 1 つ:** platform の事実の評価は backend の `QueryCapability` の中だけで行い、Framework の composition が `VfxSystem` を 1 つ作るたびに 1 回だけ起きる。`PlacedVfxActivator` は backend が公開した結果を読むだけで、自分で評価しない。結果が未公開（`Undetermined`）なら非対応と同じに扱う。`VfxCapabilityState` は書いた backend（writer）を持ち、別の backend の `Dispose` は値を戻さない。backend の生成・判定・host の生成のどこで失敗しても、`Supported` が残らない（失敗した backend は Dispose され、自分が writer なら `Undetermined` に戻す）。
- **I3 backend 契約を破らない:** 実 backend で、(a) `Create` 直後は発射していない（`Play` までは粒子 0）、(b) `Create` が失敗したら GameObject を残さない、(c) 同じ backend の生涯で id を再利用しない、(d) `Destroy` は未知の id・破棄済み GameObject・破棄済み source に対して例外を投げずに完了し、2 回目は何もしない、(e) `Destroy` から戻った時点で、GameObject が存在するなら `VisualEffect` は無効化され source（`visualEffectAsset`）を参照していない（native の破棄完了はフレーム末だが、その間に source を参照しない。Unity が先に壊した GameObject には書かない）、(f) backend はログを出さない、(g) backend の ctor は環境に依存する例外を投げない（環境の判定は `QueryCapability` だけが行う）。
- **I4 終了順序:** `ReleaseAll` で、`VfxSystem.Dispose` は Tick 要素の無効化と登録解除（生きている coordinator に対して）の後、`_updateSystemHost.Dispose`・`_loggerFactory.Dispose`・`_sceneDirector.Dispose`・`AssetManagement.ReleaseAll` より前に完了する。backend の `Dispose` は `VfxSystem.Dispose` の後。`ReleaseAll` を 2 回呼んでも二重に Dispose しない。backend の Dispose の後に backend の他のメソッドは呼ばれない。
- **I5 境界を変えない:** Framework（`OneStarMaker.*`）に VFX Graph の型と asmdef 参照が無い。SampleGame の asmdef 参照の追加は、Editor のコンパイルが要求した場合の `SampleGame.Common` → `Unity.VisualEffectGraph.Runtime` 1 件だけ（§2.6）。`SceneState` / `SceneEventType` / `LoadType` / `AssetOwner` / `SceneDirector` / `SceneBase` / `AssetManagement` の公開面を変えない。Framework の公開面の追加は §3.2 の 2 件だけ。
- **I6 Tick の負担と有限性:** `Tick` は毎フレーム unscaled delta で呼ばれる（`Time.timeScale = 0` でも消え切り待ちが有限で終わる）。instance 0 の `Tick` は managed heap を確保しない。`Tick` の中で 1 つの handle の処理が（backend 以外の原因でも）例外を投げても、同じ `Tick` の残りの handle は処理される。`BindSceneLifecycle` の `Subscribe` が例外を投げたら、Scene 所有の `Spawn` を拒否する状態に戻す。

**成立条件（起きなければならないこと）**

- **S1 package:** manifest に `com.unity.visualeffectgraph` `17.6.0` が入り、Editor の import とコンパイルがエラー 0 で終わる。
- **S2 PC で描画:** PC Quality・graphics あり（`-WithGraphics`）の Play で、Framework の `VfxSystem` と実 backend を通して `Spawn` → `Play` した最小 VFX が、カメラの描画結果に背景以外の画素として現れる。
- **S3 停止と回収:** 実 backend で、即時 `Stop` はその GameObject をフレーム末までに破棄し、`StopAllowingParticlesToFinish` は粒子が消えた時点か timeout で破棄する。所有者 `Bind(go)` の go を Destroy すると instance も破棄される。
- **S4 未構成:** `CreateVfxBackend` が null を返す project では、`VfxSystem` は未構成を示す理由で診断ログを 1 回出し、`Spawn` は何もしない handle を返す（Game は分岐しない）。非対応（判定が false）とはログの理由文で区別できる。
- **S5 配置済み VFX:** 無効のまま保存した `VisualEffect` と `PlacedVfxActivator` を持つ prefab を Play 中に生成すると、対応環境では `enabled` が true になり、非対応・未判定では false のまま。
- **S6 composition:** `BeforeSceneLoad` で `VfxSystem` が生成されて Tick 要素が active になり、`AfterSceneLoad` で `new SceneDirector` の直後・`RegisterAlreadyLoadedScenes` と最初の `AddScene` より前に `BindSceneLifecycle` が呼ばれる。
- **S7 配置先:** `HasParent = true` の `Spawn` は親の子に、`HasParent = false` の `Spawn` は backend が持つ `DontDestroyOnLoad` の root の子に GameObject を作る。

### 2.2 受け入れ条件

検証する条件を括弧で併記する。テスト名は Phase B が付ける仮名。

- **AC1（S1・I5）** `unity/Packages/manifest.json` に `"com.unity.visualeffectgraph": "17.6.0"` が 1 行入り、`packages-lock.json` の差分はこの package と依存の解決に閉じる。Editor の import 後のコンパイルでエラー 0（Phase B が Editor の Console で確認し、件数を §6 に記録）。
- **AC2（I2・I1）** `VfxGraphCapabilityPolicy.Evaluate(VfxPlatformFacts)` の表駆動テスト（EditMode、Unity の SystemInfo を読まない）。規則は「compute 対応、`maxComputeBufferInputsVertex > 0`、Linear color space、graphics device が拒否リスト（`Null`・`OpenGLES3`）に無い」の全てで対応とする（拒否リスト方式。platform は見ない）。false と理由: compute 非対応、`maxComputeBufferInputsVertex == 0`、`OpenGLES3`、`Null`（graphics なし）、Gamma。true: `Direct3D11` / `Direct3D12` / `Vulkan` / `Metal` / `OpenGLCore` の各行（他の条件は全て満たす）。
- **AC3（I2）** 実 backend に固定の facts を注入したテスト: `VfxSystem` の生成で facts の読み取りは 1 回、結果は `VfxCapabilityState` に 1 回公開される。評価の前に `Unsupported` を書き、評価が成功したときだけ結果で上書きする（評価中の例外では `Unsupported` が残り、`VfxSystem` の判定と一致する）。backend の `Dispose` で `Undetermined` に戻る。別の backend（後から公開した writer）がいるとき、先の backend の `Dispose` では戻らない。合格例: `VfxSystem` を作る → `Current == Supported`。不合格例: `PlacedVfxActivator` が facts を自分で読む。
- **AC4（I1）** facts を非対応に固定した実 backend の `VfxSystem`（偽 logger を ctor から数える）: ctor の Warning 1 件。続く `Spawn` 10 回は全て何もしない handle で、ログの増分 0、例外 0、`VisualEffect` の件数（`FindObjectsByType<VisualEffect>` の生成前後の差）0。同じ状態で `PlacedVfxActivator` 付き prefab を生成 → `VisualEffect.enabled == false`、ログ 0 件。`Undetermined` のまま生成しても false。不合格例: Warning を `Spawn` の間だけ数えて 0 件で合格にする。
- **AC5（S2）** `[UnityTest]` + `EnterPlayMode`、`-WithGraphics`。手順を固定する: (1) 最初に `SystemInfo.graphicsDeviceType` と `supportsComputeShaders` を計測値として出力する、(2) Quality をテストの中で `PC` に切り替え、TearDown で戻す、(3) 黒背景のカメラ（`targetTexture` = RT、自動描画に頼らない）の前方に最小 VFX を `Spawn` → `Play`、(4) 上限フレーム数の loop で `yield return null` し、毎回 `camera.Render()` → 同期の `ReadPixels` で読み戻し、背景と異なる画素が閾値以上になったら合格（上限内に達しなければ不合格）、(5) `Create` 直後・`Play` 前の同じ計測は閾値未満（I3 (a)）。閾値と上限フレーム数は fixture から Phase B が決めて §6 に記録する。(1) が graphics あり・compute 対応で、(4) が上限まで閾値未満のときだけ、§2.7 U3 でいう「描画判定の経路が不成立」とする。graphics なし・compute 非対応なら、経路ではなく実行環境の不備として扱い、環境を直して再実行する。
- **AC6（I3）** 実 backend の契約テスト（Play 中）: (a) `Create` の後、上限フレーム数の間 `HasAliveParticles == false`、`Play` の後、上限フレーム数の loop 内に true になる、(b) source に `VisualEffectAsset` でない Object を渡すと `VfxInstanceId.Invalid` が返り `VisualEffect` の件数が増えない（この証拠は GameObject を作る前の拒否に限る。GameObject を作った後に失敗する経路の後始末（catch で作りかけの GameObject を破棄して例外を外へ出す）は発見 C のコードレビューで確かめ、そのための fault injection の仕組みは作らない）、(c) 1000 回の `Create` / `Destroy` で id の重複 0、(d) 未知の id・Destroy 済みの id・外から `Object.Destroy` 済みの GameObject・source を破棄した後の `Destroy` が例外なし、(e) GameObject が存在する状態での `Destroy` から戻った直後に `VisualEffect.enabled == false` かつ `visualEffectAsset == null`、(f) テスト中の Unity ログに backend 由来の行が 0（`LogAssert.NoUnexpectedReceived` と backend のコードにログ呼び出しが無いこと）、(g) `Set*`: fixture の Exposed Property（float / int / bool / Vector3 / Vector4 を各 1 件）へ `SetFloat` / `SetInt` / `SetBool` / `SetVector3` / `SetVector4` で書くと true が返り、`VisualEffect.GetFloat` 等で読み戻した値が一致する。未定義の名前と、型の合わない名前は false を返し例外なし（`Has*` で確かめてから書く）。外から GameObject を `Object.Destroy` 済みの instance への `Set*` は false を返し例外なし（`IVfxBackend` の「false は書けなかった」と `VfxHandle` が backend の戻り値をそのまま返す既存の規約に合わせる。新しい parameter API は足さない）。
- **AC7（S3）** 実 backend と、テストが作る `VfxRuntimeHost`（Play 中の生きた coordinator に登録した Tick 要素から Tick される）: 即時 `Stop` の次フレームで GameObject が `== null`。`StopAllowingParticlesToFinish` の後、`HasAliveParticles` が少なくとも 1 回 true を返したことを確かめ、上限フレーム数の loop 内に破棄される。粒子が消えない fixture では、短い timeout（ctor に渡す）で破棄され、`StopAllowingParticlesToFinish` から破棄までの経過実時間が timeout 以上（最初の Tick の大きな delta で即 timeout する偽合格を除く）。`Bind(go)` の go を `Object.Destroy` すると次フレームまでに instance が破棄される。不合格例: 固定 N フレーム後に handle が非アクティブであることだけを見る。
- **AC8（S7）** 親ありの `Spawn` の GameObject は親の子、親なしは backend の root（`DontDestroyOnLoad` の Scene にある）の子。どちらも最終の world position / rotation が要求と一致する（許容誤差内）。少なくとも (a) 親なしで原点以外の位置・単位でない回転、(b) 原点以外へ移動し回転した親の下、の 2 ケースで確かめる（local と world の取り違えを検出するため）。backend の `Dispose` で root と残った instance が破棄される。
- **AC9（S4）** `VfxRuntimeHost` の単体テストで backend に null を渡す: `VfxSystem` の診断ログは 1 件で、理由文に `UnconfiguredVfxBackend` の固定文言を含む。非対応の facts での理由文とは異なる。
- **AC10（S6・I4・I2）** `VfxRuntimeHost` の単体テスト（EditMode、偽 backend・実 `UpdateCoordinator`・`Subject<SceneEvent>`・偽 `ISceneQuery`）: (a) 生成で Tick 要素が登録・active になり、`OnElementUpdate` が `UnscaledDeltaTime` を `Tick` へ渡す（`DeltaTime = 0`・`UnscaledDeltaTime > 0` の context で timeout が進む）、(b) `Dispose` の順序が「要素の無効化 → 登録解除 → `VfxSystem.Dispose` → backend の `Dispose`」で、2 回目の `Dispose` は何もしない、(c) 生成の途中の失敗で例外が呼び出し元へ届き、backend の `Dispose` が 1 回呼ばれ、`VfxSystem` を作った後の失敗ではその `Dispose` も済んでいる（所有権は `Create` に入った時点で host に移る）。失敗は既存の差し替え点だけで起こす: (c1) `CreateLogger` が例外を投げる偽 `ILoggerFactory`（`VfxSystem` の生成前の失敗）、(c2) 実 `UpdateCoordinator` に layer `Vfx` を別の order で先に作っておく（`GetOrCreateLayer` の order 衝突で `RegisterElement` が `InvalidOperationException` を投げる。`VfxSystem` の生成後の失敗）。`RegisterElement` が false を返す経路の後始末のコードは残すが、テストのために coordinator を virtual 化・interface 化しない（到達はコードレビューで確かめる）。実在の差し替え点で足りないと分かったら Phase A に戻す。(d) `BindSceneLifecycle` に `Subscribe` が例外を投げる observable を渡すと、host は Error を 1 回ログに出して例外を外へ出さない。Play 中のテスト（AC7 と同じ fixture）で、Framework の host が存在し、Tick 要素が active で、Scene lifecycle に接続済みであることを internal の accessor で確かめる。`AbstractApplicationInitializer` の配線の位置は diff で確認する（`BindSceneLifecycle` の呼び出しが `new SceneDirector` と `BindSceneDirector` の直後で `RegisterAlreadyLoadedScenes` より前、`ReleaseAll` の `DisposeVfx` が `_cts.Cancel` の直後で DebugSocket・`_updateSystemHost.Dispose` より前）。
- **AC11（I6）** `VfxSystem` のテスト: (a) instance 0 で warm-up の `Tick` 1 回の後、`Tick` 100 回の `GC.GetAllocatedBytesForCurrentThread()` の増分が 0（実装は `Tick` の冒頭で索引の件数 0 なら早期 return するだけにする。instance がある場合の snapshot の確保は変えない。再利用 `List` は snapshot の独立性と再入の問題を持ち込むので、測定の根拠ができるまで入れない）、(b) Stopping の handle がある場合の既存テスト（d1）は全て通る、(c) 2 つの Stopping の handle のうち 1 つ目の処理が backend 以外の原因で例外を投げても（偽 backend の `HasAliveParticles` で再入して索引を壊す等、Phase B が構成する）、2 つ目は同じ `Tick` で処理される、(d) `Subscribe` が例外を投げる observable で `BindSceneLifecycle` → 例外は呼び出し元へ届き、その後の `Spawn(Scene(id))` の `Create` は 0 回。
- **AC12（S5・I1）** prefab（無効の `VisualEffect` + `PlacedVfxActivator`、Editor 操作で保存）を Play 中に生成: `Unsupported` / `Undetermined` で `enabled == false`。`Supported` で `enabled == true`。`Supported` を注入するケースは、実 facts の判定も `Supported` のとき（`-WithGraphics`）だけ実行する（`Assume` で Inconclusive。`-nographics` の起点 filter で graphics の無い device に VFX を有効化しない）。判定 C では、このケースが Inconclusive でなく passed であることを XML で確かめる。`PlacedVfxActivator` は `VisualEffect` を有効から無効にする処理を持たない（有効化前に効く仕組みであることの構造上の確認）。
- **AC13（I5）** `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` が合格。base `2734a6ea` → implementation head の `git diff --name-status` が §4 の変更対象（と配下の `.meta`、本 HANDOFF、program 文書）に閉じる。Framework の `.cs` に `UnityEngine.VFX` の using / 型が無い。asmdef の差分は §2.6 の 1 件以内。
- **AC14（回帰）** 判定必須テスト（§5）が failed 0。d1 の `OneStarMaker.Tests.Rendering.Vfx` が全件 passed。全 EditMode 回帰を `-WithGraphics` で走らせるのは本スライスが初めてなので、既定（`-nographics`）の全回帰との差（failed・Inconclusive・Ignored の集合）を判定 C が記録する。差がこのスライスの差分と無関係かどうかの扱いは人間の裁定（決定記録）にする。

### 2.3 状態と順序の手順

- **能力判定の状態（`VfxCapabilityState`、SampleGame の static）:** 値（`Undetermined` / `Supported` / `Unsupported`）と、最後に書いた backend（writer）を持つ。backend の `QueryCapability` だけが書き（評価の前に `Unsupported`、評価に成功したら結果。書くたびに writer を自分にする）、backend の `Dispose` は自分が writer のときだけ `Undetermined` に戻す。後から書いた backend が勝つので、Play 中のテストが自分の backend を作るとテストの値になり、テストの backend の Dispose で `Undetermined` に戻る（本番の backend は以後 writer ではない。テストの Play を出れば本番も Dispose される）。Domain Reload 無効の Play 再開では、`SubsystemRegistration` の `ReleaseAll` が host を Dispose し、host の生成が途中で失敗した場合も host が backend を Dispose する（AC10 (c)）ので、前の session の `Supported` は残らない。
- **起動:** `InitializeBeforeSceneLoad` の `_loggerFactory` 生成の後で `VfxRuntimeHost.Create(CreateVfxBackend(), loggerFactory, coordinator)`。backend の所有権は `Create` に入った時点で host に移り、`Create` が失敗したら host が作りかけの `VfxSystem` と backend を Dispose してから例外を外へ出す。`VfxSystem` の ctor が `QueryCapability` を呼ぶので、Editor の Play で開いていた Scene の Awake（`BeforeSceneLoad` の後）でも結果は公開済み。`CreateVfxBackend` と `Create` の例外は、他の `Create*` hook と同じく `BeforeSceneLoad` の失敗にする（VFX を黙って無効にしない）。backend の ctor は環境に依存する例外を投げない（I3 (g)）ので、Mobile の非対応で起動は失敗しない。
- **Scene lifecycle:** `AfterSceneLoad` の `new SceneDirector(...)` と `_updateSystemHost.BindSceneDirector(...)` の直後に `_vfxRuntimeHost.BindSceneLifecycle(_sceneDirector.OnSceneEvent, _sceneDirector)`。host は `SceneDirector` の具象型を受け取らない（単体テストのため）。`BindSceneLifecycle` が例外を投げたら（I6）、host は Error を 1 回ログに出し、起動は続ける（Scene 所有の `Spawn` だけが拒否される）。`BeforeSceneLoad` から接続までの間（`AfterSceneLoad` の await の間）は、Scene 所有の `Spawn` は d1 の約束どおり拒否される。
- **更新:** Tick 要素は新しい layer `UpdateLayerIds.Vfx`（order 55。Camera 50 の後、Streaming 60 の前）に登録し、CameraSystem と同じく `ActivatePendingRegistrations()` で即時 active にする。Scene 安定待ちの gate を経由すると、gate が開かない経路（Player の BS4 smoke 等）で消え切り待ちが終わらなくなるため。`Tick` には `UnscaledDeltaTime` を渡す（VFX の simulation は scaled time なので、`timeScale = 0` では粒子が消えず、unscaled の timeout だけが出口になる）。順序の根拠: Camera の後なので、そのフレームの camera 位置が確定した後に回収判定が走る。Streaming の Scene unload より前に走るが、Scene 所有の回収は scene event で同期に起きるので順序に依存しない。
- **終了:** `ReleaseAll` の `_cts?.Cancel()` の直後、DebugSocket と `_updateSystemHost.Dispose()` より前に `DisposeVfx()`（host の `Dispose`: 要素を no-op にする → 生きている coordinator から登録解除 → `VfxSystem.Dispose` → backend が `IDisposable` なら `Dispose`）。logger と SceneDirector と AM より前なので、回収時のログが出せ、source asset は instance の破棄要求より後に解放される（Architecture §24 §16.4 の source 契約）。`SceneDirector.Dispose` が scene event の Subject を閉じる前に購読を解除しているので、logger の Dispose 後に `OnCompleted` の処理が走らない。Play Mode 終了で Unity が先に Scene を解体した場合も、I3 (d) により backend の `Destroy` は安全に完了する。

### 2.4 d1 から送られた事項と backend の決定

**必須事項（program §6.3 d2 行）**

| 事項 | 裁定 |
|---|---|
| `VfxSystem.Tick` の `UpdateSystemRuntime` 登録とフレーム順序 | §2.3「更新」。layer `Vfx`（order 55）、即時 active、unscaled delta |
| `VfxSystem` の生成場所・`BindSceneLifecycle`・`Dispose` と `SceneDirector.Dispose` / `AssetManagement.ReleaseAll` の順序 | Framework の `AbstractApplicationInitializer` が internal の `VfxRuntimeHost` を通して所有する（§3.2 の理由）。順序は §2.3 |
| backend 未構成と Unsupported の区別 | Framework が internal の `UnconfiguredVfxBackend`（`QueryCapability` が false と固定の理由文）を使う。ログは `VfxSystem` の既存の Warning 1 件で、理由文で区別する（S4・AC9）。Game には常に非 null の `VfxSystem` がある |
| capability 判定結果の単一の出所 | backend の `QueryCapability` が `VfxCapabilityState` へ公開し、`PlacedVfxActivator` はそれを読む（I2・§2.3） |
| `IVfxBackend` 契約の実 backend での確認 | AC6（`Create` 失敗の後始末、`Destroy` の冪等性と破棄済み GameObject・失効した source への耐性、id を再利用しない） |
| 消え切り待ち timeout の既定値（d1 は 10 秒） | 10 秒のまま。設定 key は作らない（必要になった Event スライスの Phase A で決める） |
| `BindSceneLifecycle` を SceneDirector の生成直後・最初の Scene のロード前に呼ぶ配線順 | §2.3「Scene lifecycle」・AC10 |
| `Destroy` 要求と native 実体の破棄完了の差 | backend の `Destroy` は id を対応表から先に外し、GameObject が存在するなら `VisualEffect.enabled = false`・`visualEffectAsset = null`・GameObject の非 active 化を同期に行ってから `Object.Destroy` する（`DestroyImmediate` は `Bind` 所有者の破棄中に失敗しうるので使わない。I3 (e)・AC6 (e)） |
| `VfxSpawnRequest.Parent` が null のときの配置先 | backend が遅延生成する `DontDestroyOnLoad` の root（S7・AC8）。active Scene に置くと、所有者と無関係な Scene の unload で Unity が先に壊すため |

**backend の実装の決定（A2 で固定）**

- **`Create`（発射しない）:** source が `VisualEffectAsset` でなければ何も作らず `Invalid`。非 active の GameObject に `AddComponent<VisualEffect>` → `visualEffectAsset` を設定 → `initialEventName = string.Empty`（有効化時の初期イベントを送らない）→ 親の下（親なしは root の下）へ移す → `request.Position` / `request.Rotation` を **world** の位置・回転として設定する（親の移動・回転に関係なく、最終の world transform が要求と一致する。d1 の `VfxSpawnRequest` の契約）→ active にする。途中の例外では作りかけの GameObject を破棄してから例外を外へ出す。`VisualEffect` は有効化のたび（と `Reinit` で）初期イベント（既定 `OnPlay`）を送るので、backend は `Reinit` と `enabled` の切り替えを呼ばない。
- **`Play`:** `VisualEffect.Play()`（全 Spawn system へ `OnPlay`）。**`StopEmitting`:** `VisualEffect.Stop()`。fixture は `OnPlay` / `OnStop` を Spawn に配線した既定の構成にする。
- **`HasAliveParticles`:** `HasAnySystemAwake() || aliveParticleCount > 0`。`aliveParticleCount` は GPU からの非同期の読み戻しで遅れうる（package の `ComponentAPI.md`）ので単独では使わない。誤判定は回収を遅らせる側に倒し、timeout が上限を保証する。
- **id:** backend の生涯で 1 から単調増加する `ulong`。再利用しない。
- **fixture:** `.vfx` 2 件は Culling Flags を常に更新（`CullNone`）にする。カメラに映らない間 simulation が止まり、粒子が消えないまま timeout でしか回収されない状態を避けるため。
- **配置済み VFX:** `PlacedVfxActivator` が `enabled = true` にした時点で初期イベントが送られ、配置済み VFX は自動で再生される（Cell 常駐の VFX に望ましい挙動。§3.3）。

**d1 の C / C' からの入力**

| 入力 | 採否 | 理由 |
|---|---|---|
| instance 0 でも `Tick` が毎フレーム `List` を 2 つ確保する | 採用（I6・AC11） | d2 で毎フレーム登録するので、常時の GC 負担になる |
| 拒否（`default(AssetOwner)`・終端中 Scene・破棄済み go・null source）の診断 | 不採用 | 拒否は Game が分岐しないための正常系で、毎フレームの `Spawn` でログが溢れる。最初の Game 利用者である d4 の Phase A で必要なら再検討する |
| `Tick` の全体 try/catch で backend 以外の例外が同じ Tick の残りを飛ばす | 採用（I6・AC11 (c)）。A2-3 の指摘で初稿の不採用を改めた | program §6.3 d1 行は「`UpdateSystemRuntime` に登録する場合、エフェクト単位の例外隔離はシステム単位の隔離に加えた d1 の境界」とした。d2 で登録するので、handle ごとの try/catch にする |
| Unsupported の `VfxSystem` も scene event を購読する | 不採用 | 購読の処理は backend を呼ばず、負担は Scene 遷移ごとの数回の分岐だけ |
| `BindSceneLifecycle` の `Subscribe` の例外で lifecycle が開いたまま | 採用（I6・AC11 (d)） | d2 で実配線するので、失敗時に Scene 所有を受理し続ける状態を残さない。`VfxSystem` は lifecycle を閉じて例外を外へ出し、host が捕捉してログに出す |
| `BindSceneLifecycle` の `ReleaseAll` が try の外で logger の例外が漏れる | 不採用 | `ILogger` 自身の例外は d1 の I3 で対象外とした範囲。lifecycle は回収の前に閉じているので fail-closed のまま。host が例外を捕捉する |

### 2.5 ここでは答えない問いと所有スライス

- 配置済み VFX の authoring 検査（`VisualEffect` を有効のまま保存した、`PlacedVfxActivator` が無い、の Editor 検査）と、実 Scene の Load / Unload での停止の確認: **S-4d3**。
- Player（Content Directory 経路）での描画と、VFX shader の build への収録: **S-4d3**（実 Scene の asset が build に入る最初のスライス）。
- Cell の距離ストリーミング体積への影響（N-9）: **S-4d3**。A2-3 の指摘: `VisualEffect` と一緒に付く `VFXRenderer` は `Renderer` の派生なので、`includeInactive: true` の `Renderer` 収集が VFX の bounds を Cell の AABB に入れうる。d3 の Phase A で確かめる。
- `VFXManager.asset` の shader 参照が Player で欠ける場合を判定に含めるか: **S-4d3**（Editor では package の import が埋める）。
- Game が `IVfxSystem` を受け取る経路（`GameSceneFactory` への注入等）、prefab を source にした `Spawn`、拒否の診断の要否: **S-4d4**。
- notifier の `OnDestroy` の実 Scene unload での発火、同じ identity の Scene の再ロード: **S-4d4**（d1 からの送り）。
- VFX の時間の所属: **S-4d4**（2026-10-25 発注者決定）。instance が所属する `UpdateLayer` の `IsPaused` / `TimeScale` に再生を追従させる（例: gameplay の layer と一緒に止める）。方向は「VFX は VfxSystem にまとめたまま、instance に時計（所属 layer）を持たせる」で、instance を VfxSystem の外に出さない（所有者の終端での回収・非対応での停止・例外の隔離を一か所に保つため）。d4 の Phase A で決めること: `Spawn` での layer の宣言のしかた（`VfxSpawnRequest` / `IVfxBackend` への追加。どちらも追加だけ）、backend への反映（VFX Graph の `pause` / `playRate`、値が変わったときだけ）、止まっている間の消え切り待ちの timeout の数え方（d2 は unscaled なので、止めた layer の VFX が止まっている間に打ち切られる）、配置済み VFX（`PlacedVfxActivator`）の所属の指定。2026-10-25 の program の追加レビューで、次も d4 の必須検討に足した（詳細は program §6.3 d4 行が正）: layer が `IsPaused` の間はその layer の要素が呼ばれないので、止まっている間も停止・再開を反映できる経路を選ぶこと、`Time.timeScale`・VFX Graph の `Ignore Time Scale`・`playRate`・layer の `TimeScale` の合成規則、`TimeScale == 0` と `IsPaused` の扱い、d3 で配置した VFX への後からの変更の要否（d4 が判断し、d3 の範囲は増やさない）。d2 はこれを入れず、d2 の Tick・timeout・backend の契約は変えない。
- Mobile 実機での判定と描画: S-4 では行わない（§2.7 U2 で発注者が確認する）。

### 2.6 本文へ転記した実装制約（`AGENTS.md` の常時契約から）

- 依存は Game → Framework の一方向。Framework に VFX Graph の型と asmdef 参照を入れない。asmdef 参照の追加は設計判断なので、本書が許すのは「Editor のコンパイルが要求した場合の `SampleGame.Common` → `Unity.VisualEffectGraph.Runtime` 1 件」だけ。それ以外が要るなら Phase A に戻す。backend が使う `VisualEffect` / `VisualEffectAsset` は engine module にあるので、追加は 0 件の見込み。
- アセットは `IAssetManagement` 経由。d2 の backend は asset をロードしない（source は呼び出し側がロードした Object）。テストの最小 asset は Editor の `AssetDatabase` から読む（Editor-only の test assembly）。
- `SceneState` の 14 値を変えない。状態変更は `SceneLifecycleManager` が所有する（d2 は購読するだけ）。
- 公開 API のログ抽象は `ILogger<T>`。`ZLogger*` 型を公開面に出さない。backend と `PlacedVfxActivator` はログを出さない。
- Update は `UpdateSystemRuntime`（の coordinator）に登録する。1 つの `UpdateSystem` の例外で他のシステムの `Tick` を止めない（`VfxSystem.Tick` は自分で例外を捕捉する）。
- Editor コードを Runtime アセンブリに置かない。prefab・`.vfx` の作成は Editor 操作で行い、Scene / asset の YAML を手で編集しない。
- `record` を使わない。新規・編集する `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object`（`VisualEffect`、GameObject、source、親、root）は `== null` / `!= null` で判定する。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。Play 中の待ちはフレーム（`yield return null`）、消え切り待ちの timeout は ctor の短い値とフレーム数で進める。
- path は `tools/path-budget.json` の予算に従う。
- ローカルの Unity Editor は必要なら起動・操作してよい。Phase B は対象を限定した Editor 操作（package の import、`.vfx` と prefab の作成）とコンパイル確認まで。Unity テストの実行・判定は Phase C。`unity test` / `unity run` は使わない。
- 参照 0 を削除理由にしない。

### 2.7 未決事項の裁定（A3。2026-10-25 に発注者が承認）

発注者が依頼した追加レビュー（§5「A3 前の追加レビュー」）の判断案を採り、2026-10-25 に発注者が主担当とのチャットで U1〜U4 をこの裁定どおり承認した（原文「そろそろ凍結しましょう」。その前の版で U1〜U4 の裁定案を承認対象として提示済み）。

- **U1 Player での描画 → d3 へ送る（承認）:** d2 は graphics ありの Editor の Play で実描画を判定し、Player build と VFX shader の build 統合は d3 が確かめる。program §11 の「PC で表示できる（d2 で 1 件）」は Editor の Play での 1 件と読む。二つの保証を混同しない。
- **U2 Mobile 実機 → S-4 では行わない（d2 の検証範囲として承認）:** d2 の証拠は「判定の規則（AC2）と、facts を差し替えた実 backend の非対応経路（AC4）」であり、Mobile 実機で例外が出ないことの実証ではない。実機で実施済みとは記録しない。規則は platform を見ないので、compute と SSBO に対応した Android の Vulkan 端末は `Supported` になるが、それは全 Android Vulkan 実機での動作保証ではない。
- **U3 描画判定の経路が成立しない場合 → S2 を弱めない（承認）:** 描画の判定（S2）を d3 へ先送りする案は採らない（d2 の中心の問いに答えないまま GO になるため。Player の描画を d3 へ送る U1 とは別の話）。AC5 の `camera.Render()` + RT + `ReadPixels` が不成立（AC5 の定義）なら、次の順に切り替える。(1) Unity 6000.6 / URP 17.6 で使える別の明示的なカメラ描画経路（例: SRP の render request API）を、Editor で有効性を確かめてから検証手段として使う（製品の実装要件は増やさない）。(2) それも不成立なら、Editor を通常起動して AI が Game view を撮影し、人間が画像で描画を判断する（対象 head・場面・観察者・結果を記録し、画像を evidence に入れる）。(3) どちらでも確かめられなければ NO-GO。判定手段の変更を合格条件の緩和に使わない。
- **U4 配置済み VFX の authoring 負担 → 受け入れる（承認）:** 有効化前に安全に止めることを優先する。Edit Mode の Scene view でプレビューされない負担の実害と、authoring の検査は d3 で扱う（代替案と不採用の理由は §3.3）。

## 3. 責務マップ（A1）

### 3.1 ファイルと責務

行数は現在 → 予想。

**Framework（`unity/Assets/OneStarMaker/Scripts/Runtime/`）**

| ファイル | 責務 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 行数 |
|---|---|---|---|---|---|---|
| `Rendering/Vfx/Implements/VfxRuntimeHost.cs`（新規、internal sealed、`IUpdateElement`） | `VfxSystem` 一式の生成・Tick 要素の登録と転送・Scene lifecycle への接続・Dispose 順序。orchestration だけで判定ロジックを持たない。`Create` に入った時点で backend を所有し、生成の途中で失敗したら自分で Dispose する。登録先の coordinator を自分で保持し、Dispose では要素を no-op にしてから登録解除する。接続は `BindSceneLifecycle(Observable<SceneEvent>, ISceneQuery)`（`SceneDirector` の具象型を受け取らない） | App（`AbstractApplicationInitializer` が所有。`BeforeSceneLoad` 〜 `ReleaseAll`） | `VfxSystem`、`IVfxBackend`、`ILoggerFactory`、`UpdateCoordinator`、`Observable<SceneEvent>`、`ISceneQuery` | internal（Play 中のテスト用の internal accessor を initializer に 1 件） | EditMode 単体（偽 backend、実 `UpdateCoordinator`、`Subject<SceneEvent>`） | 0 → 150 |
| `Rendering/Vfx/Implements/UnconfiguredVfxBackend.cs`（新規、internal sealed） | 未構成を示す backend。`QueryCapability` が false と固定の理由文を返し、他のメソッドは呼ばれない前提で何もしない | `VfxRuntimeHost` が生成（App） | なし | internal（理由文は internal const でテストから参照） | EditMode 単体 | 0 → 45 |
| `Rendering/Vfx/Implements/VfxSystem.cs` | 既存。`Tick` の確保回避と handle ごとの例外隔離、`Subscribe` 失敗時の lifecycle を閉じる処理（I6）を足す | 既存（App） | 既存 | 変えない | 既存テスト + AC11 | 471 → 510 |
| `Rendering/Vfx/Implements/VfxOwnerIndex.cs` | 既存。`Tick` の早期 return に使う件数を足す（snapshot の確保は変えない） | 既存 | 既存 | internal | 既存テスト + AC11 | 115 → 120 |
| `Bootstrap/AbstractApplicationInitializer.cs` | 既存。`protected virtual IVfxBackend? CreateVfxBackend()` の hook と、host の生成・接続・Dispose の 3 か所の呼び出し、Play 中のテスト用の internal accessor | 既存 | + `VfxRuntimeHost` | + protected virtual 1 件 | diff 確認と Play 中の存在確認（AC10）。既存の bootstrap テストの回帰 | 1196 → 1225 |
| `UpdateSystem/Layers/UpdateLayerIds.cs` | 既存。`Vfx` と `VfxLayerOrder = 55` を足す | 定数 | なし | + public const 2 件 | AC10 | 37 → 45 |

**SampleGame（`unity/Assets/SampleGame/Common/Scripts/Rendering/Vfx/`、namespace `SampleGame.Common.Rendering.Vfx`、asmdef `SampleGame.Common`）**

| ファイル | 責務 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 行数 |
|---|---|---|---|---|---|---|
| `VfxGraphCapabilityPolicy.cs`（新規。`VfxPlatformFacts` struct を同居） | platform の事実から対応可否と理由を決める純粋な規則 | static、状態なし | なし（`GraphicsDeviceType` / `ColorSpace` の enum だけ） | public（SampleGame 内） | EditMode 単体、表駆動（AC2） | 0 → 80 |
| `VfxCapabilityState.cs`（新規、static） | 判定結果の単一の出所。backend が書き、`PlacedVfxActivator` が読む。値と writer（最後に書いた backend）を持つ | Domain 内 static。writer の backend の Dispose で `Undetermined` に戻る | なし | public `Current`、internal の書き込み（writer 付き） | AC3 | 0 → 60 |
| `unity/Assets/SampleGame/Common/AssemblyInfo.cs`（新規） | `InternalsVisibleTo("SampleGame.Tests")`。`VfxCapabilityState` の書き込みと backend の facts 注入をテストから使うため（asmdef の変更ではない） | — | — | — | — | 0 → 5 |
| `VfxGraphBackend.cs`（新規、`IVfxBackend`・`IDisposable`） | VFX Graph の `VisualEffect` を作る・再生する・止める・壊す（§2.4 の手順）。facts の読み取りは internal ctor で注入（既定は `SystemInfo` / `QualitySettings`）。ctor は環境に依存する例外を投げない | `AbstractApplicationInitializer`（Framework）が `CreateVfxBackend` で受け取り、host が Dispose する（App） | `UnityEngine.VFX`（engine module）、`VfxGraphCapabilityPolicy`、`VfxCapabilityState` | public（SampleGame 内） | Play 中の契約テスト（AC5〜AC8） | 0 → 230 |
| `PlacedVfxActivator.cs`（新規、MonoBehaviour） | 無効のまま保存された配置済み `VisualEffect` を、`Supported` のときだけ Awake で有効にする | 置かれた Scene / prefab の寿命 | `VfxCapabilityState` | public MonoBehaviour | AC12 | 0 → 45 |
| `unity/Assets/SampleGame/DependOnAll/AppInitializer.cs` | 既存。`CreateVfxBackend` の override 1 件 | 既存 | + `SampleGame.Common.Rendering.Vfx` | 変えない | 回帰 | 422 → 428 |

**テスト**

| ファイル | 内容 | 行数 |
|---|---|---|
| `unity/Assets/OneStarMaker/Tests/Rendering/Vfx/VfxRuntimeHostTests.cs`（新規） | AC9・AC10 | 0 → 220 |
| `unity/Assets/OneStarMaker/Tests/Rendering/Vfx/VfxSystemTickAllocationTests.cs`（新規） | AC11（確保 0 と `Subscribe` 失敗） | 0 → 110 |
| `unity/Assets/SampleGame/Tests/Rendering/Vfx/VfxGraphCapabilityPolicyTests.cs`（新規） | AC2・AC3 | 0 → 120 |
| `unity/Assets/SampleGame/Tests/Rendering/Vfx/VfxGraphBackendPlayTests.cs`（新規、`EnterPlayMode`） | AC4〜AC8・AC12 | 0 → 420 |
| `unity/Assets/SampleGame/Tests/Rendering/Vfx/Fixtures/`（新規、Editor 操作で作成） | 最小 `.vfx`（短い寿命の粒子を出し続ける 1 system）と、粒子が消えない `.vfx`（timeout 用）。どちらも Culling Flags は常に更新（`CullNone`）で、`OnPlay` / `OnStop` を Spawn に配線した既定の構成。`PlacedVfxActivator` 付き prefab（`VisualEffect` は無効のまま保存） | asset |

**package と設定:** `unity/Packages/manifest.json`、`unity/Packages/packages-lock.json`、package の import で Editor が書く `unity/ProjectSettings/VFXManager.asset`（と import が書く ProjectSettings。§4 の許容）。

### 3.2 配置と依存を選んだ理由

- **composition を Framework に置く。** `BindSceneLifecycle` は `new SceneDirector` の直後・最初の Scene のロード前に呼ぶ必要があり、Dispose は logger・SceneDirector・AM より前に置く必要がある。どちらも `AbstractApplicationInitializer` の private な処理の間で、Game の hook からは割り込めない（SampleGame の CameraSystem 等は別の `Application.quitting` handler で、Framework の `ReleaseAll` との前後は登録順に依存する）。Framework は既に SceneDirector・AM・UpdateSystemHost の寿命と順序を所有しているので、同じ場所に置くと順序が 1 か所で読める。Game の差し込みは `CreateVfxBackend` 1 件に絞る。代替（SampleGame が `VfxSystem` を所有し、Framework に「SceneDirector 生成直後」と「解放の途中」の hook を 2 つ足す）は、公開面が増え、順序の約束が二つのクラスに分かれるので採らない。Tick の登録だけなら Game 側でも CameraSystem の前例どおりできるので、Framework が持つ理由は `BindSceneLifecycle` の配線と Dispose の順序の 2 点である。CameraSystem は `CinemachineCameraBackend` が Framework 内にあっても Game が所有するので非対称になるが、Camera は SceneDirector と AM の順序に依存しない。この理由は harvest で §24 §16 に残す。
- **`VfxRuntimeHost` を分ける。** 1196 行の initializer に判定や順序の詳細を入れず、呼び出し 3 か所だけにする。host は orchestration だけなので、偽 backend と実 `UpdateCoordinator` で単体テストできる。
- **Framework の公開面の追加は 2 件:** `AbstractApplicationInitializer.CreateVfxBackend()`（protected virtual、既定 null）と `UpdateLayerIds.Vfx` / `VfxLayerOrder`（定数）。layer を `UpdateLayerIds` に置くのは、Input・Camera・Streaming との順序を 1 つの一覧で読めるようにするため（同 class は共有する順序の契約）。Game が `IVfxSystem` を受け取る経路は d4 で決める（d2 に Game の利用者がいない）。`CreateVfxBackend` が null の派生 initializer（VFX を使わない project やテスト用）でも起動のたびに Warning が 1 件出る。Phase B は着手時に、起動時の Warning を数える既存のテスト・smoke が無いことを確かめ、§6 に記録する（あれば Phase A に戻す）。
- **backend 一式を `SampleGame.Common` に置く。** `PlacedVfxActivator` は d3 の InGame の Scene に、backend は DependOnAll の composition に、それぞれ使われる。両方が参照する `SampleGame.Common` に置けば asmdef の追加が要らない。`DependOnAll` は全体配線の場所で、Scene に置く MonoBehaviour を持たせない。
- **判定の規則を純粋な static に分ける。** `SystemInfo` を読む I/O と規則を分け、規則を表駆動で単体テストする（Mobile 実機の代わりの証拠になる。U2）。
- **`VfxCapabilityState` を static にする。** `PlacedVfxActivator` は Scene の deserialize で生成され、composition から注入できない。`BeforeSceneLoad` で結果が公開済みになることを §2.3 で保証し、`Undetermined` は非対応として fail-closed にする。Play 中のテストでは本番とテストの 2 つの backend が同居するので、writer を持たせて「後から書いた backend が勝ち、writer の Dispose だけが戻す」にする（A2-1・A2-2 の指摘）。
- **`.vfx` fixture はテストフォルダに置く。** Editor-only の test assembly から `AssetDatabase` で読むので、build に入らない。d3 は自分の Scene 用の asset を作る。

### 3.3 配置済み VFX を止める仕組み（program §6.3 の「有効化前に適用できるか」）

- **採用:** `VisualEffect` を無効（`enabled = false`）のまま保存し、同じ GameObject の `PlacedVfxActivator` が Awake で `Supported` のときだけ有効にする。無効のまま deserialize されるので、非対応環境で一度も有効にならないことが構造で決まる（AC12。`enabled` は serialize された値が復元されるだけで、有効化の処理は走らないことを A2-2 が確認した）。有効にした時点で初期イベント（既定 `OnPlay`）が送られ、配置済み VFX は自動で再生される。
- **不採用:** 有効のまま保存し、`[DefaultExecutionOrder]` の早い script が Awake で無効にする。native の `VisualEffect` の有効化は script の Awake の順序と独立に起きうるので、有効化前に止められることを保証できない（一度有効になれば GPU 資源の確保や非対応 API の呼び出しが先に起きうる）。
- **不採用:** `SceneManager.sceneLoaded` で backend が Scene を走査して止める。Awake / OnEnable の後に届くので有効化前ではない。
- 負担: Edit Mode の Scene view でプレビューされない（U4）。authoring の検査は d3。

### 3.4 harvest 先

Architecture §24 §16 に「composition（`VfxRuntimeHost`、起動・終了順序、Tick の layer）」と「SampleGame の backend（判定の規則、配置済み VFX の停止、`Destroy` の同期処理）」を追記し、§16.5 の d2 行を消す。§04 の起動・終了順序の記述に VFX の 1 行を足す。新しい文書は起こさない。

### 3.5 行数警報

- `AbstractApplicationInitializer.cs`（1196 行、既に 500 超）: 非分割。d2 は呼び出し 3 か所と hook 1 件（+24 行、2%）で、責務は host へ分けた。既存の大きさは本スライス由来ではない。
- `VfxSystem.cs`（471 → 約 510、500 超）: 非分割。追加は既存の Tick と `BindSceneLifecycle` の中の修正で、d1 で 3 つに分けた責務（状態遷移・索引・受理規則）の境界を変えない。
- `VfxGraphBackendPlayTests.cs`（約 420）: 非分割。全て同じ Play 中の fixture（カメラ・RT・backend）を共有する。500 を超える見込みになったら、描画（AC5）と契約（AC6〜AC8・AC12）で分ける。
- 3 責務・50% 増加の警報: 該当なし（`VfxOwnerIndex.cs` は +5% の見込み）。

## 4. 実装計画

- 変更対象: §3.1 の全ファイル（新規フォルダの `.meta` を含む）、`unity/Packages/manifest.json`、`unity/Packages/packages-lock.json`、package の import で Editor が書く `unity/ProjectSettings/` のファイル。ProjectSettings で `VFXManager.asset` 以外が変わったら、その内容を §6 に記録する（import 由来なら B 適応。手で編集しない）。
- 順序: (1) Editor で package を import し、コンパイルを確認する → (2) Framework の修正（`VfxSystem` / `VfxOwnerIndex`）とテスト → (3) `VfxRuntimeHost` / `UnconfiguredVfxBackend` / `UpdateLayerIds` / initializer の配線とテスト → (4) SampleGame の規則・状態・backend・activator → (5) Editor で fixture（`.vfx` 2 件、prefab 1 件）を作る → (6) Play 中のテスト → (7) contract-audit / docs-audit、Editor のコンパイル確認。
- Phase B から Phase A へ差し戻す条件: §2.6 以外の asmdef 参照が要る。Framework の公開面に §3.2 以外の追加が要る。`VisualEffect` を無効のまま保存しても deserialize 時に有効化される（§3.3 の前提が崩れる）。`VfxSystem` の受理・回収規則（d1 の契約）の変更が要る。
- 対象外を維持する方法: 実証 Scene を作らない。`GameSceneFactory` を変えない。`IVfxSystem` の公開経路を足さない。

## 5. テストとレビュー計画

- 単体テスト: `VfxGraphCapabilityPolicyTests`（純粋）、`VfxRuntimeHostTests`・`VfxSystemTickAllocationTests`（偽 backend、Unity なし）。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Rendering.Vfx;SampleGame.Tests.Rendering.Vfx`（Play 中のテストを含むときは `-WithGraphics`。`-nographics` では AC12 の `Supported` のケースと AC5 は Inconclusive になる。C が根拠付きで変更可）。
- 判定必須テスト（GO 候補 head）: `pwsh tools/run-tests.ps1 -WithGraphics`（空 filter の全 EditMode 回帰。Play 中のテストを同じプロセスに集約する）。XML で §3.1 のテスト 4 ファイルのテストが収録され、failed 0、かつ本スライスのテストの Inconclusive・Ignored が 0 であること。既定（`-nographics`）との差は AC14 のとおり記録する。
- 全 EditMode 回帰の適用除外: なし
- 発見 C での先行の全回帰: 全 EditMode 回帰を `-WithGraphics` で回すのは本スライスが初めてなので、発見 C の最初の巡で `pwsh tools/run-tests.ps1 -WithGraphics`（空 filter）を 1 回だけ先に回し、同じ head の既定（`-nographics`）の全回帰との差（failed・Inconclusive・Ignored の集合）を洗い出す。目的は、graphics ありの条件でだけ出る本スライスと無関係な失敗を判定 C より前に見つけ、扱い（決定記録の要否）を早めに人間へ返すこと。この先行の実行は判定の証拠にしない（判定 C は GO 候補 head で判定必須テストを改めて実行する）。差が出ても、本スライスの差分と関係のない失敗の修正は本スライスで行わない。
- 統合・Unity テスト: `VfxGraphBackendPlayTests`（Editor-only asmdef の EditMode テストで `EnterPlayMode` / `ExitPlayMode`。PlayMode platform は採用しない）。`EnterPlayMode` で本物の `AppInitializer` が起動するので、既存の前例（`SampleGame/Tests/Editor/OutGame/ConfirmDialogAccessibilityTests.cs` の `View_AssignsAccessibilityTextAndClearsItOnDestroy`）と同じ起動前提で Play に入る: 現在の Scene 構成を保存して空の Scene を開き、環境変数 `SAMPLEGAME_CONTENT__RUNTIMEMODE` を `addressables` にしてから `EnterPlayMode`、TearDown で環境変数と Scene 構成を戻す。起動の失敗を VFX の不具合と取り違えないよう、AC10 の Play 中の確認（Framework の host の存在）の前に起動が成功したことを確かめ、失敗していれば「起動の失敗」と分かる文言でテストを失敗させる。
- 操作・実行時・目視条件の検証経路:
  - **AC5（PC で描画）:** 担当は Phase C（Unity テスト）。環境はこの PC、Unity 6000.6.0f1、Quality `PC`、`pwsh tools/run-tests.ps1 -WithGraphics -Filter SampleGame.Tests.Rendering.Vfx`。手順は AC5 の (1)〜(5)（`camera.Render()` を明示し、同期の `ReadPixels`。batch の Editor には Game view が無く、カメラは自動で描画されないため）。観測は RT の画素数（構造の検査）。証拠は結果 XML とテストが出力する計測値（graphics device・compute 対応・画素数・閾値・到達フレーム数）を evidence bundle に収録する。補助として Phase C は RT を PNG に保存し bundle に入れる（人間の判断には使わない）。
  - **AC1 のコンパイル確認:** 担当は Phase B（Editor の Console）。動作確認で Phase B の head を Editor で開き直し、Console のエラー 0 を観測する。
  - 他の条件はテストで閉じる。
- 未知の操作経路の疎通結果: **未確認。初回確認は Phase C**（AC5）。理由: Unity テストの実行は Phase C の責任で、Phase A では実行しない。担当: 判定 C の実行担当。確認地点: 発見 C の起点 filter の最初の実行。不成立時の対応: §2.7 U3 の (1) → (2) → (3) の順。
- 人間の判断が必要な条件: 通常はなし。§2.7 U3 の (2) に進んだ場合だけ、描画の判断を人間が画像で行う（画像の独立再評価ができるよう、撮影画像を evidence に入れる）。
- 高コスト証拠: なし
- 判定 C の照合 script: なし（XML のテスト名・件数と contract-audit・docs-audit の出力で判定する）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC13 の `git diff --name-status`。
- A0/A1 主担当: Claude Code（Anthropic）
- A2 独立レビュー: 下記
- A3 統合担当: 人間（発注者）と主担当。採否案と本文改訂は主担当（Claude Code、Anthropic）が作り、発注者が 2026-10-25 に凍結を承認した。
- C' 用に予約した担当: xAI 系列（`cursor-agent` の Grok）を Phase A で使わずに残した。
- 独立性の強化条件を満たせない場合の理由: 未定（Phase B・C のモデルが決まった時点で記録する）。

### A2 独立レビュー

- 入力版: commit `87a9690a`（branch の最初の commit）、`docs/handoff/S-4D2_VFX_GRAPH_BACKEND.md` の content sha256 `b89a1b5c4ef6a1a415a12b8119e4ea34cfd27d094863754b4f9b4d7e48b8d79d`（`git show 87a9690a:<path>` の LF 内容）。A2-2 は sha256 の一致を確認した。
- 3 件を同じ入力版で並行して実施した。互いの指摘は見せていない。全て主担当の session が起動した別 context の subagent（Anthropic の別モデル）で、ファイルは変更していない。
  - A2-1 アーキテクチャゲート（architecture-gates.md の Phase A 7 項目と A0 の事実確認）: Claude Sonnet 系列。blocker 0、major 3、minor 6。
  - A2-2 不変条件の反証・失敗経路・VFX Graph の事実確認（同梱 package の文書とコード）: Claude Fable 系列。blocker 0、major 6、minor 8。
  - A2-3 初稿（§2 以降）を読まず A0 だけから代替構成を出す: Claude Opus 系列。
- 独立性制約あり: A2-1〜A2-3 は主担当と同じベンダー（Anthropic）。xAI 系列は C' 用に残した。
- A0 の事実記述（§1.2〜§1.4）に誤りの指摘は無かった（A2-1 が行番号で照合、A2-2 が import の生成物と `VisualEffect` の engine module を確認）。

### A3 統合（主担当の採否。2026-10-25 に発注者が凍結を承認）

| 指摘 | 採否 | 反映先 |
|---|---|---|
| A2-1 #1・A2-2 #1 Play 中のテストで本番とテストの 2 つの backend が `VfxCapabilityState` に書き、テストの Dispose が本番の状態を戻す | 採用。writer を持たせ、後から書いた backend が勝ち、writer の Dispose だけが戻す | I2、AC3、§2.3、§3.1、§3.2 |
| A2-1 #2 `SampleGame.Common` に `InternalsVisibleTo` が無く、テストが internal の書き込みを使えない | 採用。`SampleGame/Common/AssemblyInfo.cs` を足す（asmdef は変えない） | §3.1 |
| A2-1 #3・A2-2 #3 host の生成の途中で失敗すると backend が Dispose されず `Supported` と root が残る | 採用。所有権は `Create` に入った時点で host に移り、失敗時は host が Dispose してから例外を外へ出す | I2、AC10 (c)、§2.3、§3.1 |
| A2-1 #4 `CreateVfxBackend` の例外で起動全体が失敗する | 採用（制約の追加）。backend の ctor は環境に依存する例外を投げない。判定は `QueryCapability` だけ | I3 (g)、§2.3、§3.1 |
| A2-1 #5 未構成でも毎起動 Warning | 一部採用。未構成の扱い（Warning 1 件）は変えず、既存のテスト・smoke への影響を Phase B の着手時に確かめる | §3.2 |
| A2-3 未構成なら `VfxSystem` を作らずログも出さない | 不採用 | Game が常に非 null の `IVfxSystem` を受け取れるようにし（Game に分岐を書かせない program §6.4）、未構成を診断で見えるようにするため |
| A2-1 #6 Framework 所有の理由の補強（Tick だけなら Game でも可、Camera との非対称） | 採用 | §3.2 |
| A2-1 #7 host の接続の引数が `SceneDirector` だと単体テストできない | 採用。`BindSceneLifecycle(Observable<SceneEvent>, ISceneQuery)` | §2.3、§3.1 |
| A2-1 #8 initializer の配線が diff の確認だけ | 一部採用。Play 中に Framework の host の存在・Tick 要素の active・lifecycle の接続を internal accessor で確かめる。呼び出しの位置は diff の確認のまま（位置そのものを実行時に観測する手段が無い） | AC10、§3.1 |
| A2-1 #9 `UpdateLayerIds.Vfx` を public にする根拠 | 採用（根拠を書いた） | §3.2 |
| A2-1 補足 SceneDirector の生成前の await の間は Scene 所有の `Spawn` が拒否される | 採用（1 行） | §2.3 |
| A2-2 #2 `-nographics` で `Supported` を注入すると graphics の無い device で VFX を有効化する | 採用。そのケースは実 facts も `Supported` のときだけ実行し、判定 C では Inconclusive 0 を求める | AC12、§5 |
| A2-2 #4 active な GameObject に `AddComponent<VisualEffect>` すると初期イベントで発射する | 採用。`Create` の手順を固定し、`Reinit` と `enabled` の切り替えを backend で使わない | §2.4、§3.3 |
| A2-2 #5 粒子数の API の遅れと culling | 採用。`HasAnySystemAwake() || aliveParticleCount > 0`、fixture は `CullNone`、上限フレーム数の loop | §2.4、AC6、AC7、§3.1 |
| A2-2 #6 batch の Editor でカメラが自動で描画されない | 採用。`camera.Render()`・同期 `ReadPixels`・Quality の切り替えと復元・device 情報の出力、U3 の不成立の定義 | AC5、§5 |
| A2-2 #7 規則の allowlist / denylist と Metal・OpenGLCore、platform を見ないこと | 採用。拒否リスト方式、AC2 に行を追加、U2 に Android Vulkan を明記 | AC2、§2.7 |
| A2-2 #8 無効のまま保存した `VisualEffect` は有効化されない | 確認（変更なし。§3.3 に根拠を足した） | §3.3 |
| A2-2 #9 import が Assets 配下に何も生成しない | 確認（変更なし） | — |
| A2-2 #10 `_updateSystemHost.Dispose` の後の登録解除は死んだ coordinator への要求になる | 採用。A2-3 の終了順序の案と合わせ、`DisposeVfx` を `_cts.Cancel` の直後（DebugSocket・`_updateSystemHost.Dispose` の前）へ移し、要素を no-op にしてから登録解除する | I4、AC10、§2.3 |
| A2-2 #11〜#13 AC4・AC7・AC11 の読み違い（数え始め、偽の timeout 合格、warm-up） | 採用。合格例・不合格例を足した | AC4、AC7、AC11 |
| A2-2 #14 `-WithGraphics` の全回帰は初めて | 採用。既定との差を判定 C が記録し、差の扱いは人間の裁定にする | AC14、§5 |
| A2-3 d1 の入力「`Tick` の全体 try/catch」は採用すべき（program §6.3 d1 行のエフェクト単位の隔離） | 採用。初稿の不採用を改めた | I6、AC11 (c)、§2.4 |
| A2-3 d1 の入力「`BindSceneLifecycle` の `ReleaseAll` を try の中へ」 | 不採用 | lifecycle は回収の前に閉じるので fail-closed のまま。host が例外を捕捉する |
| A2-3 `Subscribe` の失敗を `VfxSystem` の中で捕捉して外へ出さない | 不採用（同じ結果を host 側で得る） | `VfxSystem` は composition の誤りを例外で知らせる d1 の方針を保ち、host が捕捉してログに出す |
| A2-3 `protected IVfxSystem? Vfx` を d2 で足す | 不採用 | d2 に Game の利用者がいない。d4 の Phase A で決める（§2.5） |
| A2-3 Tick の layer を Streaming の後（order 70）、scaled delta | 不採用 | 順序は §2.3 の根拠で 55。delta は `timeScale = 0` でも有限で終わるよう unscaled |
| A2-3 判定の前に `Unsupported` を書き、成功したときだけ結果を書く | 採用 | AC3、§2.3 |
| A2-3 `VFXRenderer` が N-9 の体積収集に入りうる | d3 へ送る | §2.5 |

### A3 前の追加レビュー（発注者が依頼、2026-10-24）

- 担当・入力: 発注者が別のレビュー担当（モデル名の記録なし）に、未 commit の A3 統合案と `develop` の関連コード・asmdef・`AGENTS.md` を渡して依頼した。A2-1〜A2-3 の採否表を読んだうえでのレビューなので、A2 から独立していない。原文は発注者の手元層。判定は「修正後 GO 推奨。全面再設計・再スライス不要」。
- 採否:

| 指摘 | 採否 | 反映先 |
|---|---|---|
| R1 `VfxSpawnRequest` の world 座標の契約が `Create` と AC8 に無い | 採用。`Create` で world の位置・回転を設定し、AC8 に親なしと移動・回転した親の 2 ケースを足した。d1 の公開 API は変えない | §2.4、AC8 |
| R2 `IVfxBackend.Set*` の実 backend の検証が無い | 採用。fixture に Exposed Property 5 型を各 1 件、読み戻し・未定義の名前・破棄済み instance を AC6 (g) に足した。新しい parameter API は足さない | AC6、§3.1 |
| R3 AC10 (c) の「`RegisterElement` が false / 例外の coordinator」は `RegisterElement` が virtual でないので作れない | 採用。既存の差し替え点（例外を投げる偽 `ILoggerFactory`、実 coordinator の layer order 衝突）で起こす。coordinator の virtual 化・interface 化はしない | AC10 (c) |
| R4 U3 (b)「描画判定を d3 へ」は S2 と GO の定義に矛盾する | 採用。(b) を取り下げ、別の明示的な描画経路 → 人間の画像判定 → NO-GO の順にした | §2.7 U3、AC5、§5 |
| R5 instance 0 の確保 0 は早期 return だけで足りる | 採用。再利用 `List` を取り下げた | AC11 (a)、§3.1、§3.5 |
| R6 AC6 (b) は GameObject を作る前の拒否しか証明しない | 採用。証拠の範囲を明記し、作った後の失敗の後始末はコードレビューで確かめる | AC6 (b) |
| U1〜U4 の判断案 | §2.7 の裁定として採り、発注者が承認した | §2.7 |

### program の追加レビュー（発注者が依頼、2026-10-25）

- 担当・入力: 発注者が別のレビュー担当（モデル名の記録なし）に、未 commit の program 文書（VFX の時間の所属を d4 へ送った版）と `develop` の UpdateSystem を渡して依頼した。原文は発注者の手元層。判定は「方針は支持。d4 の必須検討の明文化と局所的な記述修正で承認可能。再スライス不要。d2 の範囲は維持」。
- 採否: 全て採用し、program に反映した。(1) layer が止まっている間も VFX の停止・再開を反映できる経路、(2) `Time.timeScale`・`Ignore Time Scale`・`playRate`・layer の `TimeScale` の合成規則、(3) `TimeScale == 0` と `IsPaused` の扱いと timeout の時間基準、を d4 の必須検討として §6.3 d4 行に足した（本書 §2.5 にも要約）。d3 で配置した VFX への後からの変更は d4 が判断すると §6.3 d3 行に書いた（d3 の受け入れ条件は増やさない）。§6.1 の採用 version の記述を、d2 で `17.6.0` に固定した事実に揃えた。d2 の本文（範囲・受け入れ条件）は変えていない。

## 6. Phase B 実装結果

- 実装（§4 の順序どおり）:
  - **package:** `manifest.json` に `"com.unity.visualeffectgraph": "17.6.0"` を 1 行追加。Editor の import で `packages-lock.json` に同 package（`source: builtin`、依存 `com.unity.shadergraph` / `com.unity.render-pipelines.core` 17.6.0）の 1 項目だけが増え、`ProjectSettings/VFXManager.asset` の shader・resource 参照が埋まった。ProjectSettings の他のファイルは変わっていない。asmdef の変更は 0 件（`SampleGame.Common` は engine module の `UnityEngine.VFX` だけで compile できたので、§2.6 の 1 件も不要だった）。
  - **Framework:** `VfxSystem.Tick` は索引の件数 0 で早期 return し、handle・所有者ごとに try/catch する（logger 自身の例外も Tick の外へ出さない）。`BindSceneLifecycle` は `Subscribe` の例外で lifecycle を閉じてから例外を外へ出す。`VfxRuntimeHost`（internal、`IUpdateElement`）と `UnconfiguredVfxBackend`（internal、理由文は internal const）を追加。`UpdateLayerIds.Vfx` / `VfxLayerOrder = 55`。`AbstractApplicationInitializer` は `protected virtual IVfxBackend? CreateVfxBackend()`（既定 null）と、生成（`_loggerFactory` の直後）・接続（`BindSceneDirector` の直後、`RegisterAlreadyLoadedScenes` の前）・`DisposeVfx`（`ReleaseAll` の `_cts?.Cancel()` の直後、DebugSocket・`_updateSystemHost.Dispose` の前）の 3 か所、Play 中のテスト用の internal static accessor `ActiveVfxRuntimeHost` を足した。
  - **SampleGame（`SampleGame.Common.Rendering.Vfx`）:** `VfxGraphCapabilityPolicy`（`VfxPlatformFacts` 同居）、`VfxCapabilityState`（`VfxCapability` enum 同居、writer 付き）、`VfxGraphBackend`（§2.4 の手順。facts の読み取りは internal ctor で注入）、`PlacedVfxActivator`、`SampleGame/Common/AssemblyInfo.cs`（`InternalsVisibleTo("SampleGame.Tests")`）。`AppInitializer` は `CreateVfxBackend` の override 1 件。
  - **fixture（Editor 操作で作成、`unity/Assets/SampleGame/Tests/Rendering/Vfx/Fixtures/`）:** `VfxShortLived.vfx` と `VfxImmortal.vfx` は、VFX Graph の作成メニューと同じ経路（`VisualEffectAssetEditorUtility.CreateTemplateAsset`）で同梱の Simple Loop テンプレート（spawn 64/秒、寿命 0.8〜1.2 秒、capacity 68、`OnPlay` / `OnStop` は Spawn の既定の入力）から作り、VFX Graph の Editor API（internal なので reflection）で、両方の Culling Flags を `CullNone`、`VfxShortLived` に未接続の Exposed Property 5 件（`FloatProp` / `IntProp` / `BoolProp` / `Vector3Prop` / `Vector4Prop`）、`VfxImmortal` の Update に `reapParticles = false` を設定して `WriteAssetWithSubAssets` で保存した。保存後に `GetExposedProperties` で 5 件と型、`m_CullingFlags = 0` を確認した。`PlacedVfx.prefab` は `VisualEffect`（`VfxShortLived`、`enabled = false`）と `PlacedVfxActivator` の GameObject を `PrefabUtility.SaveAsPrefabAsset` で保存した（保存後に `enabled == false` を読み戻した）。YAML は手で編集していない。
  - **テスト（4 ファイルの予定を 6 ファイルにした。下記「HANDOFF との差」）:** `OneStarMaker.Tests.Rendering.Vfx` の `VfxRuntimeHostTests`（AC9・AC10 (a)〜(d)、9 件）・`VfxSystemTickAllocationTests`（AC11 (a)・(c)・(d)、4 件）、`SampleGame.Tests.Rendering.Vfx` の `VfxGraphCapabilityPolicyTests`（AC2・AC3、表駆動を含め 16 ケース）・`VfxGraphBackendPlayTests`（AC4・AC6〜AC8・AC10 の Play 中の確認・AC12）・`VfxGraphRenderPlayTests`（AC5）と、Play 入りの共通基底 `VfxPlayTestFixture`。
- AC5 の数値（fixture から決めた値）: 閾値は背景（黒）と異なる画素 4 個（RT 128×128、カメラは原点の 4 m 手前、画素の判定は RGB のどれかが 8 超）、上限は 600 フレーム。AC6・AC7 の粒子の出現・消滅の上限は 6000 フレーム（batch の Editor はフレームが短く、寿命 1 秒前後の粒子が消え切るまでに数千フレームかかりうるため）、timeout の fixture は ctor の timeout 0.5 秒で、経過実時間 ≥ 0.495 秒を求める。起動待ち（Scene lifecycle への接続）の上限は 3000 フレーム。
- HANDOFF との差:
  - `VfxOwnerIndex.cs` は変更しなかった。早期 return に使う件数（`internal int Count`）が既にあった。
  - Play 中のテストを 1 ファイル（約 420 行の見込み）から、共通基底 `VfxPlayTestFixture.cs`（176 行）・契約 `VfxGraphBackendPlayTests.cs`（396 行）・描画 `VfxGraphRenderPlayTests.cs`（105 行）の 3 ファイルにした。1 ファイルでは 625 行になり、§3.5 の「500 を超える見込みになったら描画（AC5）と契約で分ける」に当たったため。テストの集合と起点 filter（`SampleGame.Tests.Rendering.Vfx`）は変わらない。
  - 行数の予想との差: `VfxRuntimeHost.cs` 150 → 228、`VfxGraphBackend.cs` 230 → 315（寿命・所有・失敗境界の理由のコメントと、テスト用の internal accessor `RootForTest` / `FindForTest`）、`AbstractApplicationInitializer.cs` +29 → +45（同、コメント）。公開面は増えていない（accessor は全て internal）。
  - AC10 (c2)（`VfxSystem` を作った後の失敗）で「`VfxSystem` の `Dispose` も済んでいる」ことは、失敗の時点で instance が無いので backend の呼び出しとして観測できない。テストは「能力判定が 1 回呼ばれた（`VfxSystem` を作った）」「backend の `Dispose` が 1 回」「backend は能力判定以外に呼ばれていない」を確かめ、`VfxSystem.Dispose` の呼び出しは `VfxRuntimeHost.Create` の catch → `Dispose` のコードで確かめる（発見 C のコードレビューの対象）。
  - 新しい事実の分類: A 再開に当たるものは無い。上記はいずれも B 適応（凍結済みの契約・責務配置・公開面を変えない）。
- 着手時の確認（§3.2）: 起動時の Warning を数える既存のテスト・smoke は無かった。`unity/Assets` の C# と `tools/` の script を `LogType.Warning` / Warning の件数で検索し、`LogAssert.Expect(LogType.Warning, …)` は退役メニューのテストの固定文言だけ、`Application.logMessageReceived` を使う `StreamingIntegrationTests` は Error / Exception だけを数えていた。Phase A へは戻さない。
- Editor のコンパイル確認（AC1、Phase B の観測）: Unity 6000.6.0f1 でこの worktree の project を開き、package の import と最後のコンパイルの後、`recompile_status` は `failed: false`・compile エラー 0 件。Console の Error は compile 以外の 4 件だった: Unity のライセンスの通知 1 件（"License revoked: Your Unity Personal license has been revoked."）と、Editor の import・domain reload 中に送った Pipeline の eval が main thread の timeout になった記録 3 件。前者は Phase C のバッチテストの起動（ライセンス接続）に影響しうるので、判定 C の実行担当は起動ログのライセンス接続を最初に確かめる（返却・再発行を回避策にしない、は osm-workflow のとおり）。
- 作業ツリーの扱い: Editor が import の際に `unity/Assets/MobileDependencyResolver/` の `.pdb.meta` 5 件を削除した（対応する `.pdb` は `.gitignore` 済みで worktree に無い）。本スライスと無関係なので commit に含めず、`git restore` で戻した。開始前からあった未追跡の `docs/handoff/DEBUGSTUDIO_S2_CUT_PLAN.md` は本スライスと無関係で、触れていない。
- 機械検査: `pwsh tools/contract-audit.ps1`（errors 0・warnings 0、検査 9 の path 予算を含む）、`pwsh tools/docs-audit.ps1`（errors 0・warnings 0）。
- 未実行: Unity テストは全て未実行（Phase B では実行しない。§5 の起点 filter・判定必須テストは Phase C）。AC5 の描画経路の疎通も未確認（§5 のとおり初回確認は Phase C）。
- implementation head commit: §0 の引継ぎ票に記録する（この §6 を含む commit）。
- Phase B 担当・モデル・ベンダー: Claude Code（Anthropic、Claude Opus 系列）。C' 用に残した xAI 系列は Phase B でも使っていない。

### 動作確認

- 対象 head: `77a3a0b3`（branch の作業ツリーは記録用 commit `7f710dc5`。差は HANDOFF §0 だけ）
- 条件ごとの実行担当・環境・実行したコマンド: AC1 のコンパイル確認。実行担当は発注者（人間、Phase B の session ではない）。この PC のローカル Unity Editor 6000.6.0f1 で project を開き直した。
- 生の観測（出力、事前・事後の観測。合否は書かない）と保存先・状態: 発注者の報告（2026-10-25、主担当とのチャット）は「コンパイルエラーなし」。画像・ログの保存は無い。compile 以外の Console の Error の有無は報告に含まれていない。
- 検証経路の期待と違う観測と、B への差し戻し: なし
- `未実施` の条件と理由・担当（未実施が無ければ `なし`、検証経路に該当条件が無いスライスだけ `対象なし`）: なし

### 差し戻し 1 巡目（発見 C 第0巡の F1〜F4）

- 対象: 前の head `77a3a0b3` → implementation head `2055069a`。変更は `VfxGraphBackend.cs`・`VfxPlayTestFixture.cs`・`VfxGraphBackendPlayTests.cs`・`VfxGraphRenderPlayTests.cs` の 4 ファイルだけ（Framework・asmdef・公開面・fixture asset の変更 0）。
- **F2（AC6 (a)・I3 (a)）→ 修正（B 適応）:** 原因は `HasAliveParticles` の `HasAnySystemAwake()` と判断した。VFX Graph の system は有効化の直後から粒子が無くても awake を返すので、`Create` 直後に「粒子あり」になる。backend は instance ごとに Play を送ったかを持ち、Play の前は `aliveParticleCount > 0` だけで答える（Play の後は従来どおり awake か粒子数。読み戻しの遅れで早く回収しないため）。初期イベントの抑止（`initialEventName = string.Empty`）が効かずに本当に発射していれば、`aliveParticleCount` が 120 フレームの間に 0 を超えて同じテストが落ちるので、テストの意味は弱めていない。`initialEventName` の抑止が効いているかは engine の実装を確かめられなかったので、テストが Create 直後の `initialEventName` と `HasAnySystemAwake()` を計測値として出力する（発見 C の XML で確かめる）。もう一つの仮説（`initialEventNameOverriden`）は未確認のまま。
- **F3（AC7）→ テスト側で修正（B 適応）:** handle 側（Stop の後の最初の Tick を数えない）は採らなかった。消え切り待ちは「`Tick(deltaSeconds)` が待ち時間を進める」という d1 の規則（Architecture §24 §16、d1 の `VfxHandleStateTests` は最初の `AdvanceStopping` の delta を数える前提）で、変えると d1 の契約の変更（§4 の差し戻し条件）になる。Tick に渡る unscaled delta は直前のフレームの開始時刻からの差なので、Stop の後の最初の Tick には呼び出しより前の時間が入り、呼び出しの瞬間の実時間で測ると最大 1 フレーム分短く見える。テストは経過をフレームの時計（`Time.unscaledTimeAsDouble`）で測るように変えた。これは Tick が足した delta の合計と一致する。最初の Tick で即 timeout する実装は経過 1 フレーム分しかないので、AC7 の「偽合格を除く」は保たれる。許容（`timeout * 0.99`）は変えていない。
- **F4（AC7。起動のログの混入）→ 修正（B 適応）:** 原因は確かめた。Test Framework は `[UnitySetUp]` と本体に別の log scope を作り、本体の `LogAssert.NoUnexpectedReceived` は本体の間に届いた全種類のログを数える。SetUp は `AfterSceneLoad`（非同期）の完了を待たずに本体へ入っていた。共通の SetUp が Framework の host の Scene lifecycle への接続（`IsSceneLifecycleBound`）を上限 `StartupFrameLimit` で待つようにした。DebugSocket の開始とそのログは接続より前にあり、接続の後のロード済み Scene の登録は、空の Scene と UICommon（どちらも `SceneResourceMap` に無い）を同期でスキップしてログを出すだけなので、接続が見えた時点で起動のログは出終わっている。`Startup_…` のテストは待ちを SetUp に移したので `[Test]` にした（確かめる内容は同じ）。
- **F1（AC5・S2）→ 計測を足した（原因は未断定）:** F2 と同じ fixture・backend だが、F2 は「Play の前に粒子あり」と誤る問題で、Play の後に発射しない理由にはならない。描画経路の不成立か、実際に発射・シミュレーションされていないかは、Unity テストを実行しない Phase B では分けられない。`Render_SpawnedVfxAppearsInCameraOutput` が Play 後の各フレームの `aliveParticleCount` の最大値と最初に 0 を超えたフレーム、終了時の `HasAnySystemAwake`・`culled`・`enabled`・`activeInHierarchy`・render pipeline の型を出力し、失敗の文言を「粒子は生きている: 描画経路の側」と「粒子が 0 のまま: 発射・シミュレーションの側」に分けた。閾値・上限フレーム数・合否の条件は変えていない。描画経路の側と分かれば §2.7 U3 の (1) の候補（SRP の render request API 等）の有効性の確認を次の B で行う。
- 新しい事実の分類: A 再開に当たるものは無い（凍結済みの契約・責務配置・公開面・AC を変えていない）。
- コンパイル確認: Editor は開いていない。発見 C 第0巡の Unity（既知の native 終了 stall、この project を `-runTests` で開いたまま残っている）を Phase B は終了させず、Editor を同じ project で重ねて起動しなかった。代わりに Unity が生成した `unity/SampleGame.Tests.csproj`（`SampleGame.Common` を含む）を `dotnet build -p:LangVersion=10.0` でビルドし、エラー 0・警告 0（LangVersion の指定は、生成された csproj の既定 9.0 では本スライス外の既存ファイルが CS8773 で通らないため）。Unity のコンパイラでの確認は、発見 C 第1巡のテスト実行で兼ねる。
- 機械検査: `pwsh tools/contract-audit.ps1`（errors 0・warnings 0）、`pwsh tools/docs-audit.ps1`（errors 0・warnings 0）。
- 行数: `VfxGraphBackend.cs` 315 → 340、`VfxGraphBackendPlayTests.cs` 396 → 399、`VfxGraphRenderPlayTests.cs` 106 → 127、`VfxPlayTestFixture.cs` 176 → 190。
- 未実行: Unity テストは全て未実行（§0 の確認は発見 C 側）。
- 担当・モデル: Claude Code（Anthropic、Claude Opus 系列）。C' 用の xAI 系列は使っていない。

### 差し戻し 2 巡目（発見 C 第1巡の F1・F2）

- 対象: 前の head `2055069a` → implementation head `2a82ace2`。変更は `VfxGraphBackend.cs`・`VfxGraphBackendPlayTests.cs`・`VfxPlayTestFixture.cs` の 3 ファイルだけ（Framework・asmdef・公開面・fixture asset・AC5 のテストの変更 0）。
- 切り分けの方法: Phase B が Editor を起動し、Play 中に eval で `VfxShortLived` の instance とカメラを作って計測した（Unity テストではない。テストのファイルは使っていない）。GUI の Editor（6 回）と、batch の Editor（`-batchmode`、graphics あり、Direct3D11、Quality `PC`、3 回）。生の出力は scratch（git 管理外）で、evidence には入れていない。
- **F2（AC6 (a)・I3 (a)）→ 修正（B 適応）:** 原因は書く順序だった。`visualEffectAsset` を設定した後に `initialEventName = string.Empty` を書くと、getter は空を返すのに、有効化で asset 既定の `OnPlay` が送られて Play 前に発射する（既定のまま・存在しないイベント名を書いた場合と同じ数の粒子）。`initialEventName` を先に書いてから `visualEffectAsset` を設定すると、Play までの 120〜150 フレームで粒子 0・`HasAnySystemAwake == false`、Play の後に粒子が出た（GUI・batch の両方）。比較した他の案: 有効化の直後に `Stop()` を送る案も Play 前の粒子 0 だったが、§2.4 の手段（初期イベントを送らせない）をそのまま保てる順序の修正を採った。§2.4 の「`visualEffectAsset` を設定 → `initialEventName = string.Empty`」の順序だけを入れ替え、`enabled` の切り替えと `Reinit` を使わないこと・公開面・I3 の契約は変えていないので B 適応とする。テストの計測値の注釈は「読み戻しが空でも抑止の証拠にならない」に直した（合否の条件は変えていない）。
- **F1（AC5・S2）→ F2 と同じ原因と判断（AC5 の経路は不成立ではない）:** batch の Editor で、`DontDestroyOnLoad` の root の下の 1 件の instance・黒背景・無効のカメラ・ARGB32 の RT（テストと同じ条件）で、旧順序は Play 後 200 フレームで粒子 42 件でも `camera.Render()` の背景以外の画素が 0、新順序は 20 フレームで 4 画素・200 フレームで 28 画素だった。旧順序の instance が混ざると、同じ asset の新順序の instance も映らなかった（4 件同時の計測）。発見 C 第1巡の出力（粒子 62・非 culled・画素 0）と旧順序の計測は一致する。新順序の 1 件では、`camera.Render()`・`RenderPipeline.SubmitRenderRequest`（`StandardRequest` と URP の `SingleCameraRequest`）・有効のカメラの自動描画・`VFXUpdate` の直後の `camera.Render()` が同じ画素数を返し、同じ RT 経路で opaque の cube も描けた。§2.7 U3 の (1) へは進めず、AC5 の手順・閾値・上限フレーム数は変えていない。
- 後続スライスへの入力（現スライスの条件ではない）: GUI の Editor では、同じ asset の instance が複数あると、明示の `camera.Render()` に映るのは画面を描くカメラ（Game view の camera）から見える instance だけだった（instancing の batch 単位の可視判定と推定。未確定）。d2 のテストは 1 件なので影響しない。d3（実 Scene に複数配置）で RT 経路の検査を作る場合の注意として送る。
- 新しい事実の分類: A 再開に当たるものは無い（上記の順序の入れ替えは B 適応）。
- コンパイル確認: batch の Editor で修正後の script を compile し、エラー 0。本スライスのファイルの警告 0（`VfxPlayTestFixture` の obsolete な `FindObjectsSortMode` の overload の CS0618 を、同じ件数を返す overload に替えた。残る警告は本スライス外の既存の CS0108 だけ）。起動した Editor（GUI・batch）は Phase B が正常終了させた。Editor が `.pdb.meta` 5 件を削除したので `git restore` で戻した。
- 機械検査: `pwsh tools/contract-audit.ps1`（errors 0・warnings 0）、`pwsh tools/docs-audit.ps1`（errors 0・warnings 0）。
- 行数: `VfxGraphBackend.cs` 340 → 342、`VfxGraphBackendPlayTests.cs` 399 → 401、`VfxPlayTestFixture.cs` 190 → 190。
- 未実行: Unity テストは全て未実行（§0 の確認は発見 C 側）。
- 担当・モデル: Claude Code（Anthropic、Claude Opus 系列）。C' 用の xAI 系列は使っていない。

### 差し戻し 3 巡目（発見 C 第2巡の F1）

- 対象: 前の head `2a82ace2` → implementation head `628b0b53`。変更は `VfxGraphRenderPlayTests.cs` の 1 ファイルだけ（Framework・backend・asmdef・公開面・fixture asset の変更 0）。
- 切り分けの方法: Phase B が batch の Editor（`-batchmode`、graphics あり、Direct3D11、Quality `PC`）を起動し、Play 中に eval で instance とカメラを作って計測した（Unity テストではない）。各 Play で、`initialEventName` を先に書く新しい順序の instance 1 件を `DontDestroyOnLoad` の root の下に置き、無効のカメラ（原点の 4 m 手前、黒背景、ARGB32 の 128×128 RT）を `camera.Render()` して同期の `ReadPixels` で数えた（テストと同じ条件、300 フレーム）。Editor の process は 6 回起動し、同じ process で Play を続けて出入りした計測を含む。生の出力は scratch（git 管理外）で、evidence には入れていない。
- **観測（F1 の食い違いの原因）:**
  - 起動したばかりの process では、明示の `camera.Render()` の背景以外の画素は 300 フレームで 0（粒子は最大 45〜55 件）。前の Play の内容（配置済み prefab の有効化、source の複製と破棄、1000 回の生成と破棄、既定のイベントでの再生）に関係なく 0 だった。shader のコンパイル中ではない（`ShaderUtil.anythingCompiling == false`、batch では非同期コンパイルは無効）。
  - 第2巡の B の実験の script をそのまま新しい process で流しても 0 だった。第2巡で 4〜28 画素が出たのは、同じ process で先に有効なカメラを使う実験をしていたためと判断した。第2巡の「旧順序の instance が混ざると新順序も映らない」という判断も、この process の状態で説明できるので根拠が弱い（F2 の修正自体は Create 前の粒子 0 で確かめてあり、影響しない）。
  - SRP の render request（`RenderPipeline.SubmitRenderRequest` の `StandardRequest`）も、起動したばかりの process では 0 だった。§2.7 U3 の (1) の例の経路だけでは解決しない。
  - 有効なカメラが VFX を視野に入れて通常の描画ループで描くと（自分の RT へ）、同じ Play の無効のカメラの明示の `camera.Render()` に粒子が入り（最初のフレームから、最大 900 画素前後）、その後の Play でも、有効なカメラが無いまま明示の描画だけで閾値に達した（第 28〜52 フレーム、最大 450〜560 画素）。有効なカメラが VFX を視野に入れていない場合（x = 100 m）や、Play の前の 1 フレームだけ描いた場合は効かなかった。
  - 補助の描画を先に済ませてから壊し、新しい instance と無効のカメラで計測する手順（テストに入れた手順）を、新しい process の最初の Play で確かめた: 補助のカメラは 2 フレームで閾値に達し、壊した後は有効なカメラ 0・VisualEffect 0。計測の Play 前は 0 画素、Play 後は第 5 フレームで閾値（最大 91 画素）。
  - 内部の原因（VFX Graph / URP のどの初期化か）は確かめていない。
- **F1（AC5・S2）→ 計測の前に補助の描画を足した（§2.7 U3 の (1) の適用として扱う）:** `Render_SpawnedVfxAppearsInCameraOutput` は、Quality の切り替えの後、計測の前に `WarmUpRegularRenderLoop` を行う: 有効な補助のカメラ（計測とは別の RT）の前に、同じ host から別の instance を Spawn → Play し、補助の RT（自動描画の結果を読むだけ）が閾値に達するまで上限 600 フレームで待つ。達しなければ「通常の描画ループでも描かれない」として失敗する。達したら instance を Stop、補助のカメラと RT を壊し、2 フレーム後に両方が残っていないことを確かめてから、AC5 の (3)〜(5) の手順を変えずに行う。計測値として `warmUpLitPixels`・`warmUpFrames`・計測の前の有効なカメラと VisualEffect の数を出力する。閾値 4 画素・上限 600 フレーム・計測の経路（計測の RT に書くのは明示の `camera.Render()` だけ、同期の `ReadPixels`）・失敗の文言は変えていない。
- 分類の判断: B は、AC5 の合格条件を変えない検証手段の変更なので、§2.7 U3 の (1)（「別の明示的なカメラ描画経路を Editor で有効性を確かめてから検証手段として使う」）の範囲で、B 適応と判断した。ただし AC5 の (3) の「自動描画に頼らない」を、計測の前の補助の描画にまで広く読めば凍結した手順の変更になるので、発見 C の判断を求める（§0 の引継ぎ票）。製品の実装要件は増えていない（backend・Framework は不変）。
- 後続スライスへの入力（現スライスの条件ではない）: (a) batch の Editor で VFX を明示の描画だけで検査するテスト（d3 の RT 経路の検査等）は、同じ補助の描画が要る。(b) 第2巡の後続入力（GUI の Editor で、複数 instance のうち Game view のカメラから見えるものだけが明示の描画に映った）も、同じ現象の一部の可能性がある（未確認）。
- 新しい事実の分類: 上記の現象は Unity の batch の Editor の性質で、製品の契約・責務配置・公開面は変えない。
- コンパイル確認: batch の Editor で修正後の script を compile し、エラー 0。本スライスのファイルの警告 0（残る警告は本スライス外の既存の CS0108 だけ）。起動した batch の Editor（6 回）は全て正常終了させた。Editor が `.pdb.meta` 5 件を削除したので `git restore` で戻した。
- 機械検査: `pwsh tools/contract-audit.ps1`（errors 0・warnings 0）、`pwsh tools/docs-audit.ps1`（errors 0・warnings 0）。
- 行数: `VfxGraphRenderPlayTests.cs` 127 → 196。
- 未実行: Unity テストは全て未実行（§0 の確認は発見 C 側）。
- 担当・モデル: Claude Code（Anthropic、Claude Opus 系列）。C' 用の xAI 系列は使っていない。

## 7. Phase C

### 発見 C 第0巡（Phase B の差し戻し前）

- 種別・対象: 発見 C 第0巡（GO 判定ではない）。implementation base `2734a6ea` → head `77a3a0b3`。作業ツリーは記録用 commit `106dafcd`（差は HANDOFF だけ）で、未追跡の `DEBUGSTUDIO_S2_CUT_PLAN.md` 以外は clean だった。
- 担当・モデル: Claude Code（Anthropic、Claude Sonnet 系列。Phase B の Opus 系列と異なる）。C' 用の xAI 系列は未使用。
- 構造適合: `VfxRuntimeHost`・`UnconfiguredVfxBackend`・`VfxGraphBackend`・`VfxCapabilityState`・`PlacedVfxActivator`・`VfxGraphCapabilityPolicy` の責務、`VfxSystem` の Tick・`BindSceneLifecycle` の変更、initializer の配線 3 か所（生成は `_loggerFactory` の直後、接続は `BindSceneDirector` の直後で `RegisterAlreadyLoadedScenes` の前、`DisposeVfx` は `_cts?.Cancel()` の直後で DebugSocket・`_updateSystemHost.Dispose` より前）、公開面（§3.2 の 2 件のみ）、asmdef 差分 0 を §3 の責務マップと照合し、構造上の指摘は無かった。編集した Unity `.cs` の `#nullable enable`、`record`、`Task.Delay` / `Thread.Sleep` は問題なし。
- 実行したコマンドと結果: `pwsh tools/run-tests.ps1 -WithGraphics`（空 filter、最初の巡の 1 回だけ。判定の証拠にしない）。環境: Direct3D11、`supportsComputeShaders=True`、`maxComputeBufferInputsVertex=128`、Linear。結果は 1283 件中 passed 1279・failed 4・Inconclusive 0・Skipped 0。失敗 4 件は全て本スライスのテストで、`SampleGame.Tests.Rendering.Vfx` の 132 件中 128 件が passed。本スライス外の失敗は 0 件。テスト開始前に Unity のライセンス接続は成立していた。
- 未確認: (1) 同じ head の既定（`-nographics`）の全回帰は未実行なので、AC14 の「既定との差」は未取得（判定 C で記録する）。(2) AC5 が U3 に当たるかの切り分けは未了（F1）。(3) F2・F3 の原因は仮説のまま。
- native 終了 stall: 結果 XML の完成後も Unity が終了しなかった（`taskkill` は対象無しと返した）。AGENTS.md の既知 native 終了 stall として扱い、調査は打ち切った。追加の調査はしない。
- 生ログ・XML: git 管理外の `TestResults/` 配下にある（`evidence stage` はしていない。ローカル path はここに転記しない）。
- 作業ツリーの扱い: Editor が `unity/Assets/MobileDependencyResolver/` の `.pdb.meta` 5 件を再び削除したので `git restore` で戻した（本スライスと無関係。§6 と同じ）。

**阻害する指摘（凍結済み AC への違反）**

| ID | 違反する条件 | 一次証拠（結果 XML） | 状態 |
|---|---|---|---|
| F1 | AC5（S2） | `VfxGraphRenderPlayTests.Render_SpawnedVfxAppearsInCameraOutput`: `litPixelsBeforePlay=0`、Play 後は `litPixels=0 reachedFrame=-1 frameLimit=600`、閾値 4。graphics あり・compute 対応なので、AC5 の定義上は「描画判定の経路が不成立」に当たりうる。ただし F2 と同じ fixture・backend なので、実際に発射・描画されていない可能性も排除できていない | 切り分け未了。U3 の (1) へ進む前に、粒子が実際に発射しているか（`HasAliveParticles`・`aliveParticleCount` を同じ場面で出力）と、カメラ・RT 経路（URP のカメラに `camera.Render()` で VFX が入るか）を分けて確かめる |
| F2 | AC6 (a)・I3 (a)（`Create` は発射しない） | `VfxGraphBackendPlayTests.Create_DoesNotEmitUntilPlay`: `Create` 直後の frame 0 で `HasAliveParticles(id)` が true（テストの 135 行） | 事実。原因は仮説: `initialEventName = string.Empty` が既定の初期イベント（`OnPlay`）を抑止していない（`initialEventNameOverriden` の扱いの確認が要る）、または `HasAnySystemAwake()` が Play 前でも true を返す |
| F3 | AC7（S3） | `VfxGraphBackendPlayTests.StopAllowingParticlesToFinish_ImmortalParticles_DestroyedByTimeout`: 経過実時間 0.446 秒 < 期待 0.495 秒（ctor の timeout 0.5 秒の 0.99 倍）。テストの 334 行 | 事実。原因は仮説: `VfxHandle.AdvanceStopping` が Stop を呼んだフレームの delta 全体を加算するので、timeout が最大 1 フレーム分（この環境では約 0.05 秒）早く切れる。B 適応で直す先（handle の数え始め、またはテストの許容）は B が選ぶ。AC7 の「最初の Tick の大きな delta で即 timeout する偽合格を除く」の意図を満たすこと |
| F4 | AC7（S3）。テストの合否が起動の非同期ログに依存 | `VfxGraphBackendPlayTests.BindOwnerDestroyed_InstanceDestroyedByNextFrame`: `Unhandled log message: '[Log] [AppInit] Starting DebugSocket transport. mode=Connect, …'` | 事実（失敗の原因）。fixture の `EnterPlayModeWithApp` が `AfterSceneLoad` の完了（起動の非同期処理の終了）を待たずにテスト本体へ入るため、起動の Log が本体の最中に届く。原因（待ちが無いこと）は B が確かめる |

- 後続スライスへ移送する指摘: なし。
- B への指示: F1〜F4 を直す。F1 は F2 の修正後に再測定して切り分ける（F2 の修正で F1 が通る可能性がある）。切り分けた結果、AC5 の経路が不成立と分かった場合は §2.7 U3 の (1) の候補と有効性の確認までを B が調べ、検証手段の変更は HANDOFF に書いて発見 C へ返す（合格条件の緩和にしない）。凍結済みの契約・責務配置・公開面を変える必要が出たら止めて返す。
- 次の発見 C は、B の修正 head に対して §5 の起点 filter を `-WithGraphics` で実行し、F1〜F4 の解消と、他のテストの回帰を確認する（全回帰は判定 C まで回さない）。

### 発見 C 第1巡（Phase B 差し戻し 1 巡目の後）

- 種別・対象: 発見 C 第1巡（GO 判定ではない）。implementation head `2055069a`、作業ツリーは記録用 commit `83ce3bde`（差は HANDOFF だけ）。担当・モデル: Claude Code（Anthropic、Claude Sonnet 系列）。
- 実行: `pwsh tools/run-tests.ps1 -WithGraphics -Filter "OneStarMaker.Tests.Rendering.Vfx;SampleGame.Tests.Rendering.Vfx"`（1 回）。Direct3D11、compute 対応、Linear。結果は 132 件中 passed 130・failed 2・Inconclusive 0・Skipped 0。Unity は終了コード 2（失敗ありの通常終了）で終了し、stall は出ていない。生ログ・XML は git 管理外の `TestResults/` 配下（stage していない）。Editor が `.pdb.meta` 5 件を再度削除したので `git restore` 済み。全回帰は回していない。
- 解消: **F3**（AC7 の timeout）と **F4**（起動ログの混入）は passed。d1 の `OneStarMaker.Tests.Rendering.Vfx` は全件 passed。他のテストの回帰なし。
- **残る阻害する指摘**

| ID | 条件 | 一次証拠（XML） | 状態 |
|---|---|---|---|
| F2 | AC6 (a)・I3 (a) | `Create_DoesNotEmitUntilPlay`: 出力は `initialEventName='' hasAnySystemAwakeAtCreate=False`。それでも frame 3 で `aliveParticleCount=3` となり失敗（Play 前に発射）。前回の `HasAnySystemAwake` 仮説は部分的で、実際に Play 前に粒子が出ている | `initialEventName = string.Empty` が初期イベント（既定 `OnPlay`）の抑止になっていない可能性が高い（空文字は未設定として既定に戻る等）。B が engine/package のコードと実測で確かめ、`Create` で有効化する前に初期イベントを送らせない別の方法（例: 存在しないイベント名、または enabled=false のまま構成して Play で有効化する等。ただし §2.4 の「`enabled` 切り替えを使わない」と衝突しうるので衝突するなら Phase A へ返す）を選ぶ |
| F1 | AC5（S2） | `Render_SpawnedVfxAppearsInCameraOutput`: `graphicsDeviceType=Direct3D11 supportsComputeShaders=True`、`litPixelsBeforePlay=0`、`litPixels=0 reachedFrame=-1 frameLimit=600 maxAliveParticles=62 firstAliveFrame=2 hasAnySystemAwake=True culled=False enabled=True activeInHierarchy=True renderPipeline=UniversalRenderPipelineAsset` | 粒子は生きて simulation されている（`maxAliveParticles=62`、culled=false）のに、`camera.Render()` + RT + `ReadPixels` に現れない。AC5 の定義上「描画判定の経路が不成立」。§2.7 U3 の (1)（別の明示的な描画経路。SRP の render request API 等）の有効性確認へ進むため B へ戻す。出力の見落としを避けるため、候補は `RenderPipeline.SubmitRenderRequest` と、camera の `targetTexture` の format・VFX の bounds / カメラ位置・向き・near/far の確認（fixture の粒子がカメラの前方 4 m に収まるか）も含めて切り分ける |

- 後続スライスへ移送する指摘: なし。
- 次: Phase B 差し戻し 2 巡目（巡は 2/4）。F2 と F1 を直す。F1 は U3 の (1) の有効性確認までを B が行い、(2) の人間の画像判断が要る場合は止めて返す。次の発見 C は同じ起点 filter を `-WithGraphics` で再実行する。

### 発見 C 第2巡（Phase B 差し戻し 2 巡目の後）

- 種別・対象: 発見 C 第2巡（GO 判定ではない）。implementation head `2a82ace2`、作業ツリーは記録用 commit `04b0878b`（差は HANDOFF だけ）。担当・モデル: Claude Code（Anthropic、Claude Sonnet 系列）。
- 実行: `pwsh tools/run-tests.ps1 -WithGraphics -Filter "OneStarMaker.Tests.Rendering.Vfx;SampleGame.Tests.Rendering.Vfx"`（1 回）。Direct3D11、compute 対応、Linear。結果は 132 件中 passed 131・failed 1・Inconclusive 0・Skipped 0。Unity は終了コード 2（失敗ありの通常終了）で終了した。生ログ・XML は git 管理外の `TestResults/` 配下（stage していない）。Editor が `.pdb.meta` 5 件を再度削除したので `git restore` 済み。全回帰は回していない。
- 解消: **F2**（`Create_DoesNotEmitUntilPlay`）は passed。d1 の `OneStarMaker.Tests.Rendering.Vfx` は全件 passed。他のテストの回帰なし。
- **残る阻害する指摘**

| ID | 条件 | 一次証拠（XML） | 状態 |
|---|---|---|---|
| F1 | AC5（S2） | `Render_SpawnedVfxAppearsInCameraOutput`: `litPixelsBeforePlay=0`、`litPixels=0 reachedFrame=-1 frameLimit=600 maxAliveParticles=67 firstAliveFrame=2 hasAnySystemAwake=True culled=False enabled=True activeInHierarchy=True renderPipeline=UniversalRenderPipelineAsset` | 未解消。F2 の順序修正後も、粒子は生存・非 culled なのに `camera.Render()` + RT に 600 フレームで 1 画素も出ない。B の第2巡の実測（batch の Editor で新順序は 20 フレームで 4 画素）とテスト runner の `-runTests` 実行で食い違う。差の候補（未切り分け）: テスト fixture のカメラ・RT・粒子位置の構成と B の実測用の構成の差、`-runTests` の Play mode と通常の batch Editor の差、同 asset の複数 instance のうち Game view のカメラから見えるものだけが映った（B の後続入力）という挙動 |

- 後続スライスへ移送する指摘: なし。
- 次: Phase B 差し戻し 3 巡目（巡は 3/4）。B は、F1 を再現する条件（`-runTests` のテスト内）で、B の実測との差を切り分ける（fixture の構成・instance 数・カメラの対象を比較）。直らなければ §2.7 U3 の (1) の有効性確認へ進む。(2) の人間の画像判断が要るなら止めて返す。

### 発見 C 第3巡（Phase B 差し戻し 3 巡目の後）

- 種別・対象: 発見 C 第3巡（GO 判定ではない）。implementation head `628b0b53`、作業ツリーは記録用 commit `2754f3a9`（差は HANDOFF だけ）。担当・モデル: Claude Code（Anthropic、Claude Sonnet 系列）。
- 実行: `pwsh tools/run-tests.ps1 -WithGraphics -Filter "OneStarMaker.Tests.Rendering.Vfx;SampleGame.Tests.Rendering.Vfx"`（1 回、sandbox 外）。結果は 132 件中 passed 132・failed 0・Inconclusive 0・Skipped 0。終了コード 0。生ログ・XML は git 管理外の `TestResults/` 配下（stage していない）。Editor が `.pdb.meta` 5 件を削除したので `git restore` 済み。全回帰は回していない。
- 解消: **F1**（AC5）は passed。出力: `warmUpLitPixels=4 warmUpFrames=1`、`afterWarmUp enabledCameras=1 visualEffects=0`、`litPixelsBeforePlay=0`、`litPixels=4 reachedFrame=1 maxAliveParticles=0 hasAnySystemAwake=True culled=False`。他のテストの回帰なし。
- 観察（阻害ではない）: 計測は閾値 4 ちょうどで合格し、`aliveParticleCount` は 0 のまま（GPU 読み戻しの遅れ）。画素は Play 前 0・Play 後 4。閾値・上限は凍結どおり。余裕が小さいので、判定 C で落ちたら再現性を先に疑う。
- **判断（依頼への回答）:** 補助の描画（warm-up）は AC5 の (3)「自動描画に頼らない」の凍結手順の変更（Phase A の再開）には当たらず、§2.7 U3 の (1) の範囲（合格条件を緩めない検証手段の変更）と判断する。根拠: 計測の RT に書くのは明示の `camera.Render()` だけで、計測の前に補助のカメラ・instance が壊れていることを assert する。閾値・上限・失敗文言・backend は不変。補助の描画自体も閾値に達しなければ失敗する。ただし「通常の描画ループに先に描かせる」ことは batch の Editor の実測に基づく環境条件で、内部原因は未確定。d3 の RT 経路検査にも同じ前提が要る（§6 の後続入力 (a)）。人間へ返す blocker ではない。
- **阻害する指摘:** なし。後続スライスへ移送する指摘: なし（d3 への入力は §6 に既出）。
- 次: 未公開の試行錯誤 commit の整理 → GO 候補 implementation head の固定 → 判定 C。

### 判定 C（GO 候補 head `628b0b53`）

- 種別・対象: 判定 C。implementation base `2734a6ea` → head `628b0b53`（tag `evidence/s-4d2-vfx-graph-backend/1`）。作業ツリーは記録用 commit `af58632b`（差は HANDOFF だけ）。担当・モデル: Claude Code（Anthropic、Claude Sonnet 系列）。C' は起動していない。
- 実行: `pwsh tools/run-tests.ps1 -WithGraphics`（空 filter、Direct3D11・compute 対応・Linear）。**1283 件中 passed 1283・failed 0・Inconclusive 0・Skipped 0**、終了コード 0。XML に `VfxGraphCapabilityPolicyTests` 16、`VfxGraphBackendPlayTests` 15、`VfxGraphRenderPlayTests` 1、`VfxRuntimeHostTests` 9、`VfxSystemTickAllocationTests` 4 を収録。d1 の `OneStarMaker.Tests.Rendering.Vfx` は全件 passed。AC5 `Render_SpawnedVfxAppearsInCameraOutput` は passed（`warmUpLitPixels=12`、`litPixelsBeforePlay=0`、`litPixels=8 reachedFrame=0`、閾値 4）。AC12 の `Supported_PlacedVfxIsEnabled` は passed。
- AC13: `pwsh tools/contract-audit.ps1` errors 0・warnings 0、`pwsh tools/docs-audit.ps1` errors 0・warnings 0。`git diff --name-status 2734a6ea 628b0b53` は §4 の変更対象・配下の `.meta`・本 HANDOFF・program 文書に閉じる（`.asmdef` の差分 0 件、`unity/Assets/OneStarMaker` に `UnityEngine.VFX` / `VisualEffect` の参照 0）。
- **AC14（既定との差）:** 同じ head の `pwsh tools/run-tests.ps1`（`-nographics`、空 filter）は 1283 件中 passed 1270・failed 0・**Inconclusive 13**・Skipped 0（終了コード 1）。Inconclusive の 13 件は全て本スライスの Play 中のテスト（`VfxGraphBackendPlayTests` 12、`VfxGraphRenderPlayTests` 1）。failed の集合は両条件とも空で、本スライス外の差は 0 件。`-nographics` で Inconclusive になるのは §5 の想定どおり。**差の扱いは人間の裁定（決定記録）へ返す。**
- 備考: Editor が `.pdb.meta` 5 件を削除したので `git restore` 済み。Unity は正常終了（stall なし）。PC 再起動で中断した先の全回帰は証拠にしていない。生ログ・XML は git 管理外の `TestResults/`。
- 判定 C の結論: 判定必須テストと機械検査は条件を満たし、阻害する指摘はない。GO は C' と人間の AC14 裁定が揃うまで確定しない。

### 決定記録

- **D1（版 1、2026-10-25 適用、発注者）:** (a) 未公開の試行錯誤 commit は整理しない。実装の commit は Phase B と差し戻し 3 巡の各 1 つで、§6・§7 がその SHA を指すため。GO 候補 head は `628b0b53`（tag `evidence/s-4d2-vfx-graph-backend/1`）。(b) §5「発見 C での先行の全回帰」の既定（`-nographics`）の全回帰は発見 C で回さず、差は判定 C の AC14 で記録する。発見 C 第0巡の `-WithGraphics` の全回帰でスライス外の失敗が 0 件だったため。どの条件の意味・合否も変えない。
- **D2（版 1、2026-10-25 適用、発注者）:** AC14 の既定（`-nographics`）との差（head `628b0b53` で Inconclusive 13、failed は両条件とも 0）を許容する。13 件は全て本スライスの Play 中のテストで `-WithGraphics` では passed、本スライス外の差は 0 件のため。AC14 の合否・意味は変えない。

## 8. Phase C'

### C' 1 巡目（最終。高コスト証拠なし）

- 種別・対象: C' 1 巡目（高コスト証拠を分類していないので最終）。base `2734a6ea` → GO 候補 head `628b0b53`（tag `evidence/s-4d2-vfx-graph-backend/1`）。
- 入力: C' blind bundle（§0 の取得案内欄。状態はローカル固定済み（未昇格））を進行役が別 process で `fetch` し、期待 packageSha256 の照合が通った。中身は凍結 A3 版の HANDOFF、HANDOFF を除く差分（`full.diff`・`name-status`・`stat`）、`-WithGraphics` と既定の全回帰の生の XML・log、contract-audit・docs-audit の出力。差分と同梱のソースに発見 C・判定 C の所見や差し戻しへの言及が無いことを検索で確かめた。監査担当の作業場所には、この bundle と head `628b0b53` のソースの写し（`AGENTS.md`、`unity/Assets` の OneStarMaker・SampleGame・Docs/Architecture、`unity/Packages/manifest.json`。HANDOFF は含めない）と、観点を 2 つ（凍結条件・常時契約への違反と構造、証拠の十分さと未検証の失敗経路）に絞った中立の依頼文だけを置いた。可変 HANDOFF・PR・決定記録 D1・D2 は渡していない。
- 担当・モデル: `cursor-agent`（`-p --trust`、編集を禁止。ファイルの編集・作成の呼び出しは 0 件）の xAI Grok 4.7 High（`grok-4.7-high`）。1 回だけ実行し、約 13 分。
- 独立性: B は Anthropic Claude Opus 系列、C（発見・判定）は Anthropic Claude Sonnet 系列、C' は xAI 系列。C' は B・C と異なるモデルで、新しい session と blind bundle を使ったので最低条件を満たす。xAI 系列は Phase A・B・C で使っていないので、強化条件も満たす。
- 結論（C' の出力の要約）: **blocker 0 件。** このスライスの問い（§1・§2.1）への致命的な反証は無い。配線（生成・接続・破棄の位置、layer `Vfx` / order 55）は凍結どおりと確認。
- 後続スライスの入力（C' が blocker にしなかったもの。Phase D で判定 C と突き合わせる）:
  - AC14: 既定で Inconclusive 13 件。凍結が予告したのは AC5 と AC12 の `Supported` の 2 件で、契約テスト 11 件も graphics なしでは実行されない。
  - AC5: 計測の前に自動描画の warm-up がある。合否は明示の `camera.Render()` と `ReadPixels`。合格フレームの `maxAliveParticles=0`。補助の PNG は監査入力に無い。
  - AC6 (a): 非発射の確認は 120 フレームで、発射待ちの上限 6000 より短い。
  - AC7: Bind の破棄のテストは `yield return null` を 2 回待つ（凍結文言は「次フレームまで」）。
  - `HasAliveParticles` は Play 前だけ `aliveParticleCount` を使う（§2.4 の式は常に `HasAnySystemAwake()` を含む）。
  - §3.1 の名前一覧に無いテストファイル 2 つ（§3.5 の分割の許可の範囲）。
  - コードレビューに残した経路（`RegisterElement` が false のときの throw と Dispose、`Create` が GameObject の作成後に失敗したとき）はテストで実行されていない。
  - 配置 prefab の `VisualEffect` は無効、同じ物体の `VFXRenderer` は有効（I1 が止めるのは `VisualEffect.enabled`）。
- 監査できなかった範囲・残存リスク: HANDOFF §6 の記録（コンパイルの観測など）は入力外。Console のエラー 0 の一次記録は bundle に無い。`.vfx` と prefab が Editor 操作で作られたかは区別できない。Player の描画と Mobile 実機は U1・U2 でスライスの外。テストは再実行していない。画素の中身を画像で見ていない。
- 生の出力: 進行役の一時作業場所（git 管理外、stage していない）。上の要約が記録の正。

## 9. Phase D

- 判断: 発注者が 2026-10-25 に Phase D 完了とマージを指示した（PR #143）。判定 C（§7）と C'（§8）はどちらも阻害する指摘 0。PR の独立コードレビュー（GPT-6、cursor）も阻害する指摘 0。C' と判定 C の後続入力は Architecture §24 §16.7 へ送った。
- Evidence の保存・引渡し: 判定 C の evidence bundle、C' blind bundle、C' 1 巡目の出力（Phase D で進行役が stage）の 3 つはローカル固定済み（未昇格、`locality=local`）で、どれも別 process の `fetch` で hash 照合・閲覧を確認した。取得案内は進行役の共有ノートにある。R2 への昇格はしていない（同一 PC の読み手だけのため）。
- 日次清掃 job: `evidence schedule status` は `scheduler-identity-mismatch`（`passed` ではない）。この確認では直さず、owner が Artifacts README「自動清掃」の切替手順で処置する（未完了事項）。
- harvest: Architecture §24 §16 に §16.5（composition と更新）・§16.6（SampleGame の VFX Graph backend）を追加し、§16.4 の保証の範囲を更新、§16.7 の送りから d2 を消して d3・d4 の入力を足した。§04 の Shutdown 契約に `DisposeVfx` の 1 行。program と `docs/README.md` の完了表記を S-4d2 まで進めた。
- HANDOFF: この記録の次の commit で削除する。task は マージ後に `workflow-task.ps1 end -Reason completed` で終える。
- 担当: 進行役（Claude Code、Anthropic Claude Opus 系列）。
