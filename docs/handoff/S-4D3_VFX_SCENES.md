# S-4d3 VFX Scene 2件

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `C`（発見 C 第2巡で F4 解消・阻害する指摘 0。履歴整理の後、GO 候補 head `87daa97c` で判定 C 待ち。§7）。経緯: 差し戻し 2 巡目（2/4）で F4 を修正。発見 C 第1巡で F1〜F3 は解消、新しい阻害する指摘 1 件（F4）。 発見 C 第0巡で阻害する指摘 3 件 → 差し戻し 1 巡目で修正。 Phase B 完了（2026-10-26。A3 凍結は同日。U1 は (b)、U2〜U6 はレビュー側の推奨で発注者が裁定し、凍結を承認した）
- branch: `claude/s-4d3-vfx-scenes`
- implementation base commit: `6667f88f`（`develop`、PR #143（S-4d2）のマージ）
- risk: `high`（Scene・SceneResource・Addressables の生成、Scene の寿命と Season の子の LoadType、Player build への収録）
- owner: 発注者 / S-4d3 担当
- created: 2026-10-25
- expires: S-4d3 の Phase D 完了時
- harvest to: Architecture §24 §16（§16.6 配置済み VFX の実 Scene での確認、§16.7 の d3 行の削除）、§05 §5.13（Season 直下の Atmosphere VFX の分類と LoadType）、§21 または STREAMING_CURRENT_SPEC（VFX Scene と Cell 体積の規約）。新しい文書は起こさない
- workflow task: `s-4d3-vfx-scenes`（Phase B の着手時に start 済み）
- Phase A snapshot の取得案内・状態: A2 入力版は未 commit の版（content sha256 は §5「A2 独立レビュー」）。A3 凍結版はブランチ `claude/s-4d3-vfx-scenes` のローカル commit（本書・program の改訂を含む。SHA は commit の後に引継ぎ票で渡す）
- Phase B result snapshot の取得案内・状態: ブランチ `claude/s-4d3-vfx-scenes` のローカル commit（Phase B 完了 commit。SHA は commit の後に引継ぎ票で渡す）。未 push
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票（発見 C 第2巡 → 判定 C）:
  - 前の Phase の結果: 発見 C 第2巡が完了（implementation head `adf23ebd`、§7「発見 C 第2巡」）。起点 filter の実行は 170 件中 passed 170・failed 0。F4 は解消（DebugSocket の接続失敗の warning 0 件）。阻害する指摘 0、後続へ移送する指摘 0。
  - 次の Phase・役割: **判定 C（別 session）**。GO 候補 head `87daa97c`（下記の履歴整理の後の implementation head）で §5 の判定必須テスト（`pwsh tools/run-tests.ps1 -WithGraphics`、空 filter の全 EditMode 回帰）、機械検査、AC7（Player、高コスト証拠。§5 の手順と最終証拠ラウンドの順）を行う。発見 C の所見は C' へ渡さない。
  - 巡のカウント: B と発見 C の間で 2/4（上限 4）。
  - 引き継ぐ commit: branch `claude/s-4d3-vfx-scenes` の先頭（この HANDOFF だけを変える記録用 commit）。implementation head は `87daa97c`、implementation base は `6667f88f`。
  - 判定 C の前の履歴整理（2026-10-27、発注者の指示）: A3 凍結 `a7ff035a` の後の未公開の 8 commit（Phase B・差し戻し 2 巡・発見 C 3 巡の記録）を、実装 1 commit `87daa97c`（親 `a7ff035a`）と、この HANDOFF の記録 1 commit にまとめた。`87daa97c` の tree は HANDOFF 以外が発見 C 第2巡の対象 `adf23ebd` と完全に一致する（`git diff 87daa97c adf23ebd` が HANDOFF だけ）ので、第2巡の結果はそのまま `87daa97c` に当てはまる。`87daa97c` の HANDOFF は Phase B 完了時（§6 まで）の版。§6・§7 に出てくる整理前の SHA（`0bb0c46d`・`f264cd6e`・`adf23ebd` 等）は各巡の記録として残し、ローカルの退避 branch `s-4d3-pre-squash-backup` から到達できる（判定 evidence を取った head ではないので tag にはしない。Phase D の後始末で消す）。未 push（同じ PC のローカル SHA で照合する）。作業ツリーは、別作業の未追跡 `docs/handoff/DEBUGSTUDIO_S2_CUT_PLAN.md` を除き clean。Editor は終了済み。
- 引継ぎ票（以前: Phase B 差し戻し 2 巡目 → 発見 C 第2巡。経緯として残す）:
  - 前の Phase の結果: Phase B 差し戻し 2 巡目が完了（§7「Phase B 差し戻し 2 巡目」）。F4 はテストの起動条件の B 適応で直した（`VfxPlayTestFixture` が Play の間だけ DebugSocket を無効にする。Framework の変更なし）。Phase A 再開に当たる事実は無い。Editor のコンパイル確認は errors 0、`contract-audit` errors 0、`docs-audit` errors 0。Unity テストは未実行（発見 C の責任）。
  - 次の Phase・役割: **発見 C 第2巡（別 session）**。修正 head に対して §7 第0巡と同じコマンド（`-WithGraphics`）を DebugStudio を起動していない環境で実行し、F4 の解消（failed 0、描画テストの本体が DebugSocket の warning を拾わない）と回帰を確かめる。全回帰・Player は判定 C まで回さない。
  - 巡のカウント: B と発見 C の間で 2/4（上限 4）。
  - 引き継ぐ commit: branch `claude/s-4d3-vfx-scenes` の先頭（この HANDOFF とテスト 1 ファイルを変える差し戻し 2 巡目の commit。implementation head はこの commit）。implementation base は `6667f88f`。未 push（同じ PC のローカル SHA で照合する）。作業ツリーは、別作業の未追跡 `docs/handoff/DEBUGSTUDIO_S2_CUT_PLAN.md` を除き clean。Editor は終了済み。
- 引継ぎ票（以前: 発見 C 第1巡 → Phase B 差し戻し 2 巡目。経緯として残す）:
  - 前の Phase の結果: 発見 C 第1巡が完了（implementation head `f264cd6e`、§7「発見 C 第1巡」）。起点 filter の実行は 170 件中 passed 169・failed 1。F1〜F3 は解消（実 Scene の 4 件は全て passed、AC4 (b) の bounds 包含も観測）。残る 1 件は新しい阻害する指摘 F4（DebugSocket の接続失敗の warning が長い描画テストの本体に混ざり `LogAssert.NoUnexpectedReceived` で落ちる。B 適応）。
  - 次の Phase・役割: **Phase B（差し戻し 2 巡目 = 2/4、別 session）**。B の入力はこの HANDOFF（§7 F4）だけ。直す対象は F4。B 適応で足り、Phase A の再開に当たる事実が出たら止めて返す。Unity テストの限定確認は次の発見 C 側の session が行う（B は Editor のコンパイル確認まで）。
  - 巡のカウント: B と発見 C の間で 2/4（上限 4）。
  - 引き継ぐ commit: branch `claude/s-4d3-vfx-scenes` の先頭（この HANDOFF だけを変える発見 C 第1巡の記録用 commit）。implementation head は `f264cd6e`、implementation base は `6667f88f`。未 push（同じ PC のローカル SHA で照合する）。作業ツリーは、別作業の未追跡 `docs/handoff/DEBUGSTUDIO_S2_CUT_PLAN.md` を除き clean。Editor は終了済み。
- 引継ぎ票（以前: Phase B 差し戻し 1 巡目 → 発見 C 第1巡。経緯として残す）:
  - 前の Phase の結果: Phase B 差し戻し 1 巡目が完了（§7「Phase B 差し戻し 1 巡目」）。F1 は `.vfx` の culling の authoring（B 適応）と特定して直し、F2・F3 はテストの B 適応で直した。Phase A 再開に当たる事実は無い。Editor のコンパイル確認は errors 0、`contract-audit` errors 0、`docs-audit` errors 0。Unity テストは未実行（発見 C の責任）。
  - 次の Phase・役割: **発見 C 第1巡（別 session）**。修正 head に対して §7 第0巡の末尾のコマンド（起点 filter + 名前空間 2 つ、`-WithGraphics`）を実行し、F1〜F3 の解消と回帰を確かめる。全回帰・Player は判定 C まで回さない。
  - 巡のカウント: B と発見 C の間で 1/4（上限 4）。
  - 引き継ぐ commit: branch `claude/s-4d3-vfx-scenes` の先頭（この HANDOFF だけを変える記録用 commit）。implementation head は `f264cd6e`（親は第0巡の記録 commit `09a203ab`）、implementation base は `6667f88f`。未 push（同じ PC のローカル SHA で照合する）。作業ツリーは、別作業の未追跡 `docs/handoff/DEBUGSTUDIO_S2_CUT_PLAN.md` を除き clean。Editor は終了済み。
- 引継ぎ票（以前: 発見 C 第0巡 → Phase B 差し戻し 1 巡目。経緯として残す）:
  - 前の Phase の結果: 発見 C 第0巡が完了（base `6667f88f` → implementation head `0bb0c46d`、§7）。起点 filter の実行は 170 件中 failed 5。5 件とも本スライスの Play 中テストで、阻害する指摘 3 件（§7 の F1〜F3）。
  - 次の Phase・役割: **Phase B（差し戻し 1 巡目 = 1/4、別 session）**。B の入力はこの HANDOFF（§7）だけ。直す対象は §7 の F1〜F3。B 適応で足りるものは修正し、Phase A の再開に当たる事実（§4 の差し戻し条件、AC・公開面の変更）が出たら止めて返す。F1 はスパイクの範囲と終了条件が §7 にある。Unity テストの限定確認は発見 C 側の session が行う（B は Editor のコンパイル確認まで）。
  - 巡のカウント: B と発見 C の間で 1/4（上限 4）。
  - 引き継ぐ commit: branch `claude/s-4d3-vfx-scenes` の先頭（発見 C 第0巡の記録用 commit。implementation head `0bb0c46d` の後で、HANDOFF だけを変える）。implementation base は `6667f88f`。未 push（同じ PC のローカル SHA で照合する）。作業ツリーは、別作業の未追跡 `docs/handoff/DEBUGSTUDIO_S2_CUT_PLAN.md` を除き clean にする。
  - （以下は発見 C 第0巡の前の記録。経緯として残す）Phase B 完了（§6）。Unity テスト・Build は未実行だった。動作確認は対象なし。Phase B 完了 commit `0bb0c46d`（その子の HANDOFF だけの commit は引継ぎの記録で、実装差分を変えない）、Phase A の凍結版は `a7ff035a`。Unity は起動後にライセンス確認のダイアログで止まることがあり、その場合は computer use で閉じてよい（発注者の許可、2026-10-26）。閉じられなければ進行役に伝える。

## 1. 目的と対象外（A0）

- 目的: Spring の Season Atmosphere VFX（`Spring_AtmosphereVFX`）と Cell VFX（`Spring_VFX_4_2`）を、program §2 の制作契約どおり別 Scene として作り、PC で表示でき、非対応環境では有効化されず、Scene の寿命で回収され、Cell の距離ストリーミング体積を変えないことを実 Scene で確かめる。d2 が送った配置済み VFX の authoring 検査と、Player（BS4 経路）での描画・build 収録もここで答える。
- 対象外:
  - Event からの VFX 起動、`Spring_Events_4_2`、動的 VFX の `Spawn`（d4）。
  - VFX の時間の所属（layer の `IsPaused` / `TimeScale` への追従）。d3 で配置した VFX に後から変更が要るかは d4 が判断し、d3 の受け入れ条件を増やさない（program §6.3 d3 行）。
  - `Spring_Event_MultiCellProof`、Planner 実証、Season 排他 controller と Season 子の関係（d5）。
  - Summer / Autumn / Winter の Atmosphere VFX と、他の Cell の VFX（必要な季節・Cell だけ作る。S-4 では Spring の 2 件）。
  - Mobile 実機での判定と描画（S-4 では行わない。d2 §2.7 U2 の発注者決定）。
  - VFX Graph 非対応環境向けの ParticleSystem fallback（代替表示）。d3 では実装せず、要否・契約・実装は後続の独立スライス（担当・時期未定）で決める（§2 決定記録 D2）。恒久的な不採用ではない。d3 の `PlacedVfxActivator` や SceneGraph を将来の fallback に向けて先回りで一般化しない。
  - VFX の見た目の作り込み。d3 の VFX は「Cell 常駐の環境エフェクト」「季節全域の常時エフェクト」の代表として成立する最小のもの。
  - 同時数の上限、優先度、距離での間引き、pool、DebugStudio での観測（program §6.4）。

### 1.1 program から転記した制作契約

（`docs/handoff/S-4_FULL_SPEC_WORLD_AUTHORING.md` の §2.1〜§2.5・§3.2〜§3.4・§6.1〜§6.4・§10〜§12 から d3 に必要なものを転記した。）

- **SceneGraph（§2.1）:** `Season_Spring` は Scene payload を持たない論理ノード。子は `Spring_Lighting`（`NecessaryAlways`）、`Spring_AtmosphereVFX`（`IncrementalAlways` / optional）、`Spring_Cell_{x}_{y}`（`OnDemand` / `StreamByDistance`）。Cell の子の職種 Scene（Environment / Lighting / VFX / Events）は `StreamByDistance = false`・`LoadType.OnDemand` で、親 Cell が Stable になった後に SampleGame の companion loader が選択中の profile に応じて明示 `AddScene` し、Unload は親 Cell の再帰破棄に委ねる。
- **職種の所有物（§2.2）:** Cell VFX は霧・煙・流水など、その Cell の常駐中ずっと続く環境エフェクト。Season Atmosphere VFX は季節全域の常時エフェクトで、必要な季節だけ作る。VFX Scene にゲーム進行の trigger を置かない。Scene を跨ぐ `GameObject` の直接参照は禁止。
- **VFX の粒度（§2.4）:** Cell 常駐中ずっと必要な複数エフェクトは 1 Cell 1 VFX Scene に束ねる。季節全域は Season Atmosphere VFX。一時的な VFX は Scene にしない。
- **物理フォルダ（§2.5）:** 論理 SceneGraph と物理フォルダの親子を合わせる。Cell VFX は `Seasons/Spring/Cells/Spring_Cell_4_2/Spring_VFX_4_2/Spring_VFX_4_2.unity`。Season 直下は `Seasons/Spring/Spring_Lighting/Spring_Lighting.unity` と同じ形。
- **Companion profile（§3.2）:** `world:cellCompanionSet` の `Full`（Environment + Lighting + VFX + Events）、`Planner`（Events のみ）、`Lighting`（Environment + Lighting）、`VFX`（Environment + Lighting + VFX）。role の判定は SampleGame の制作規約に閉じ、Framework は職種を知らない。
- **World Workspace（§3.4）:** VFX は Cell + Environment + Season Lighting + optional Cell VFX を開く。存在しない Lighting / VFX / Events を作る操作は `.unity`・`SceneResource`・親子 link・Map・Addressables entry を同じ Editor transaction で作る。Scene / asset の YAML を直接編集しない。
- **Content（§3.1）:** Lighting / Events 等の Full のみの補助 Scene は Whitebox build にも含める。欠損を source や Addressables で埋めない。
- **VFX Graph（§6.1）:** PC Quality で描画を必須受入。Mobile は起動時の判定で非対応なら `VisualEffect` を有効化しない。診断ログは Framework が 1 回だけ出し、backend と Scene 側は出さない。（A3 r2 で program を改訂: S-4d3 では Mobile 向け ParticleSystem fallback を実装せず、非対応環境では代替描画も行わない。fallback の要否・契約・実装は後続の独立スライスで決める（担当・時期未定）。この延期は d3 の GO 条件を妨げない。改訂前の「作らない」は恒久方針ではなかった。）
- **§6.3 d3 行:** 答える問いは「Season / Cell の VFX を §2 の制作契約どおり別 Scene として配置し、PC で表示でき、非対応環境では止まるか」。範囲は `Spring_AtmosphereVFX`（Season）と `Spring_VFX_4_2`（Cell）、d2 の停止の仕組みの実 Scene での確認。**必須検討:** Cell の体積（N-9）。体積の再計算は `StreamByDistance == false` の子だけを親へ畳み、収集は `Renderer`（`includeInactive: true`）に限る。`Spring_VFX_4_2` を保存したあと Cell の `_volume` が変わるかを確かめ、変わったときだけ除外規約を決める。`Spring_AtmosphereVFX` の畳み先は Season ノードで、Cell の AABB には入らない。
- **§6.4:** Scene に置く VFX の寿命は Scene の寿命で回収されるので、d1 の寿命管理（`VfxSystem`）の対象ではない。非対応時の停止は配置済み VFX にも適用し、配置済みのものを止める処理は SampleGame の backend 側（d2 の `PlacedVfxActivator`）が持つ。Scene 側は起動時の判定結果を見るだけで、追加の診断ログを出さない。
- **§10:** Framework は season 名・Cell 座標文法・職種 role を知らない。VFX Graph の依存は SampleGame に閉じる。`SceneState` の 14 値と `LoadType` を変えない。Framework の公開面の追加は program §10 の列挙に限る（d3 で増やす予定は無い）。
- **§11 S-4d（d3 の行）:** PC で Season / Cell VFX を表示できる。非対応時、`Spring_AtmosphereVFX` / `Spring_VFX_4_2` をロードしても例外・有効な `VisualEffect`・追加の診断ログが無い。VFX Scene の追加で Cell の距離ストリーミング体積が意図せず変わらない。`unity test` / `unity run`、Cloud での Unity CLI、Scene / asset の YAML 手編集は禁止。旧大量生成器を復活させない。
- **§12:** program に無い公開 API・依存・所有者・寿命は d3 の Phase A で決め、program の該当行も Phase A の PR で更新する。

### 1.2 d2 から送られた事項（Architecture §24 §16.6・§16.7、d2 HANDOFF §2.5）

1. 配置済み VFX の authoring 検査（`VisualEffect` を有効のまま保存した、`PlacedVfxActivator` が無い）と、実 Scene の Load / Unload での停止の確認。
2. Player（Content Directory 経路）での描画と、VFX の shader の build への収録。`ProjectSettings/VFXManager.asset` の shader 参照が Player で欠ける場合を判定に含めるか。
3. Cell の体積（N-9）: `VisualEffect` と一緒に付く `VFXRenderer` は `Renderer` の派生なので、`includeInactive: true` の収集が VFX の bounds を Cell の AABB に入れうる。
4. 配置済み VFX は `VisualEffect` だけを無効で保存し、同じ GameObject の `VFXRenderer` は有効のまま。非対応環境で描画されないことを実 Scene で確かめる。
5. batch の Editor で明示の描画だけで検査するテストは、計測の前に別のカメラと instance で一度描かせる必要がある（§16.6）。GUI の Editor で、複数の instance のうち Game view のカメラから見えるものだけが明示の描画に映った観察もある（同じ現象の一部かは未確認）。

### 1.3 現況（A0 の調査、base `6667f88f`）

- **Season の子:** `Season_Spring.asset`（`Seasons/Spring/`）の子は `Spring_Lighting`（`NecessaryAlways`、`StreamByDistance = 0`、`_children: []`）と 54 Cell。Atmosphere VFX は無い。
- **Scene class の生成:** `GameSceneFactory.CreateSceneClass`（99 行）は `StreamByDistance` → `DemoCellScene`、親が `StreamByDistance` → `CellCompanionScene`、それ以外は identity の switch（`Season_*` → `SeasonScene`、`*_Lighting` → `SeasonLightingScene` 等）で、どれにも当たらなければ null を返す。`SceneDirector` は null を `InvalidOperationException`（"SceneFactory returned null"）にする。**したがって `Spring_AtmosphereVFX` は factory に分類を足さないとロードできない。** `Spring_VFX_4_2` は親が Cell なので既存の `CellCompanionScene`（38 行、no-UI、ログ 1 行）になる。
- **LoadType の意味（Framework）:** `NecessaryAlways` は親のロード時に await、`IncrementalAlways` は親のロード時に Forget でバックグラウンドロード、`OnDemand` は明示 `AddScene` のみ。
- **`IncrementalAlways` の保留 unload（A2-2 が発見、主担当がコードで確認）:** 親の unload が、ロード中で取消し窓を過ぎた `IncrementalAlways` の子を `_pendingUnloads` に積む（`SceneDirector.Unloading.cs:326-335`）。保留の消費は公開 `AddScene` の対象 Scene の経路（`SceneDirector.Loading.cs:246`）にしか無く、子の `IncrementalLoadAsync`（`:593-612`）は消費しない。したがって子は Stable まで進んで残り、以後の `UnloadScene` も `_pendingUnloads.Contains` で早期 return する（`Unloading.cs:43`）。project の `SceneResource` に `IncrementalAlways`（`_loadType: 2`）は 0 件で、この不具合は潜在のまま。d3 で Atmosphere を `IncrementalAlways` にすると最初の利用者になる（§2.7 U6）。
- **Season controller:** `SessionSeasonController`（599 行）は Season の Stable 後に `NecessaryAlways` の子だけを観測個体として登録する（`RegisterNecessaryAlwaysChildren`）。枝の交換（`TearDownActiveBranchAsync`）は観測個体の終端を待つ（`SceneTerminalTracker.WaitAllLive`）。`IncrementalAlways` の子は観測されない。距離候補は `SeasonCandidateSelection.FromSeasonChildren` が Season 直下の `StreamByDistance` の子だけから作るので、`StreamByDistance = false` の Atmosphere は候補に入らない。
- **Companion loader:** `CellCompanionRoleClassifier` は identity の末尾から 3 番目の token（`VFX` 等）で role を決め、`CellCompanionPolicy` が profile と role から選ぶ（`Full` / `VFX` は VFX を読む。`Planner` / `Lighting` は読まない）。`Spring_VFX_4_2` は既存の規則で分類できる。
- **SceneGraph の source（A3 改訂レビュー R1 で追記）:** SceneGraph の Edge は `Assets/SceneGraphData/Graphs/Total.asset`（`SceneGraphEdges`、graph はこの 1 件）が持つ。生成済み `SceneResourceMap.GenerateHash` は `SceneResourceGenerator.ComputeCurrentHash(nodes, graphs)`（node の Identity / LoadType / Payloads と Edge 構造のダイジェスト）と一致する。`WorldCompanionCreationTransaction` は作成前（`:144-146`）と作成後（`:259-261`、`graph.ContainsNode` / `GetParent` を含む）にこの一致を検査する。
- **作成の道具:** World Workspace の `WorldCompanionCreationPlan` / `WorldCompanionCreationTransaction`（93 / 390 行、recovery journal 付き）は Lighting / VFX / Events の **Cell の子だけ**を作る。親が `StreamByDistance` の Cell でなければ拒否し、生成後の `LoadType == OnDemand`・`StreamByDistance == false`・体積 0 を検査し、Addressables の Default Group に entry を作る。Season 直下の子を作る道具は無い（S-4b の大量生成器は撤去済み）。
- **体積:** `SceneVolumeRecalculator`（Framework Editor、295 行）は Scene の保存フックで、保存した Scene とその祖先の `_volume` を再計算する。自分の体積は `SceneVolumeSceneReader.ComputeOwnVolume` が Scene の全 root の `GetComponentsInChildren<Renderer>(includeInactive: true)` のワールド AABB を合併して求め、`StreamByDistance == false` の子の体積を親へ畳む。Renderer の種類による除外は無い。
- **d2 の部品（`SampleGame/Common/Scripts/Rendering/Vfx/`）:** `VfxCapabilityState`（`Undetermined` / `Supported` / `Unsupported`、writer 付き）、`PlacedVfxActivator`（Awake で `Supported` のときだけ同じ GameObject の `VisualEffect` を有効化。ログを出さない）。テストの fixture は `SampleGame/Tests/Rendering/Vfx/Fixtures/`（`PlacedVfx.prefab`、`VfxImmortal.vfx`、`VfxShortLived.vfx`）と `VfxPlayTestFixture`（190 行、Play 中のテストの起動と後始末、描画の事前 warm-up）。
- **代表 Cell のテスト:** `CellLightingSceneTests`（EditMode、Scene を開いて Light の構成を数える）が S-4c の前例。
- **Player:** `Tools/OSM/Content/Build BS4 Spring Player`（`SampleGamePlayerBuildCoordinator`）が Content Directory と Windows x64 Player を作る。Player は固定 config の `firstScene` から InGame の Spring を始め、スポーンは `(0,4)`。起動直後に Season_Spring の子（Lighting と、d3 後は Atmosphere）が載るが、`(4,2)` は距離の外でロードされない。既存の Player 内の観測は BS4 の content probe（`PlayerContentSmoke`）だけで、VFX の描画を観測する仕組みは無い。

## 2. 意思決定と受け入れ境界（A1、A3 統合案）

- このスライスが答える問い: `Spring_AtmosphereVFX` と `Spring_VFX_4_2` を制作契約どおりの別 Scene として作ったとき、PC の Play で表示され、非対応環境では 1 つも有効化されず、Scene の寿命（Cell unload・Season unload）で回収され、Cell の距離ストリーミング体積を変えず、Player で Atmosphere が描画されるか。
- 判定定義: 進める最低条件（§2.1）を全て満たせば GO。一つでも未達なら NO-GO。スパイクではないので CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録（凍結前の発注者決定）:
  - **D1 描画の画素検査は専用テスト Scene で行う（2026-10-26）:** 実 Scene には地形等の他の Renderer が混じり、非黒画素の閾値は VFX の描画証拠にならない。AC4 (c)・AC5 の画素検査は、本番 2 Scene と同一の `.vfx` asset を参照する専用テスト Scene で陽性・陰性を同条件で取る。実 Scene のロード・有効化・寿命の検査（AC4 (a)(b)(d)・AC5 の有効化 0・AC6）と Player の実描画（AC7）は維持し、専用 Scene の描画成功で代替しない。
  - **D2 ParticleSystem fallback は後続へ（2026-10-26）:** d3 では実装しない。非対応・`Undetermined` 時は d2 の契約どおり `VisualEffect` を有効化せず、代替表示が無いことは d3 の失敗ではない。要否・契約・実装は後続の独立スライス（担当・時期未定。d4・d5・S-9 に決め打ちしない）。program の「作らない」を「今回作らない」に改訂する（§1.1）。d2 の「非対応では有効化しない」契約は変えない。
  - **D3 U1〜U6 の裁定と凍結の承認（2026-10-26）:** U1 は **(b)**（作成 transaction を Season の子へ広げ、World Workspace から Atmosphere を作る。主担当・レビュー側の推奨 (a) と異なる発注者の裁定）。U2 現行案（Atmosphere は profile に関係なく載る）、U3 現行案 + R5、U4 (a)（実 Player と画像）、U5 現行案、U6 (b)（`NecessaryAlways`）。U1 (b) に伴う設計は §2.3「Season の子の作成」、AC10、§3.1・§3.2、§4 に入れた。

### 2.1 進める最低条件

保証の範囲: d3 が保証するのは、(a) 2 つの VFX Scene が制作契約どおりの構造で存在し、content の選択に入ること、(b) Editor の Play（graphics あり）で実 Scene として載り、表示・非対応時の停止・Scene の寿命での回収が成り立つこと、(c) Cell の体積が変わらないこと、(d) Player build に入り、Player で Atmosphere が描画されること。描画の陽性・陰性は、本番と同一の `.vfx` を使う専用テスト Scene で保証する（D1）。Event からの起動、時間の所属、Season 交換（S-5）の演出、非対応環境での代替表示（ParticleSystem fallback、D2）は保証しない。

**不変条件（起きてはいけないこと）**

- **I1 非対応で有効化しない:** `VfxCapabilityState` が `Unsupported` / `Undetermined` のとき、2 つの VFX Scene をロードしても、その Scene に属する（`go.scene` が一致する）`VisualEffect` で `enabled == true` のものが 0、例外 0、ロードの前後で Unity console に増えるログ 0。同じ `.vfx` を置いた専用テスト Scene を非対応の状態で新しくロードしたとき、カメラの描画結果に VFX の画素が出ない（`VFXRenderer` が有効のままでも描画されない。D1）。
- **I2 体積を変えない:** 2 つの VFX Scene の作成・保存・体積の再計算の前後で、全 Cell（`StreamByDistance == true` の 216 件）の `_volume` が一致する（完全一致）。`StreamByDistance` は 0 件変わらない。変わってよい `_volume` は、新しい 2 件と、`Spring_AtmosphereVFX` の祖先の鎖（`Season_Spring` とその上位）だけ。
- **I3 authoring の契約を破らない:** VFX を含む Scene（§2.6 の対象集合）の全ての `VisualEffect`（非 active の GameObject のものを含む）は、無効で保存され、同じ GameObject に `PlacedVfxActivator` を持ち、`visualEffectAsset` が null でない。VFX Scene に `Collider` と、ゲーム進行・カメラ追従の script が無い。Cell VFX の描画 bounds（各 System の Bounds Setting Mode は Manual、空間は Local。§2.3）は親 Cell の `_volume` の内側にある。違反を 1 件でも含めると検査が失敗する。
- **I4 寿命:** Cell `Spring_Cell_4_2` の unload の後に `Spring_VFX_4_2` に属する GameObject が残らない。`Season_Spring` の unload の後に `Spring_AtmosphereVFX` に属する GameObject が残らない。`Season_Spring` が Stable のとき `Spring_AtmosphereVFX` も Stable で、`SessionSeasonController` の観測個体に入る（U6 の推奨案）。
- **I5 境界を変えない:** Framework（`unity/Assets/OneStarMaker/`）の差分が 0。`SceneState` / `LoadType` / `SceneResource` / `SceneDirector` を変えない。asmdef の差分が 0。既存の 652 Scene・Cell Lighting 2 件・他の `SceneResource` の payload・親子・LoadType・`StreamByDistance` が変わらない（`Season_Spring` と `Spring_Cell_4_2` の `_children` への各 1 件の追加と、I2 が許す体積の変化を除く）。

**成立条件（起きなければならないこと）**

- **S1 構造:** `Spring_AtmosphereVFX`（親 `Season_Spring`、`NecessaryAlways`、`StreamByDistance = false`、payload は既定 Variant の 1 件）と `Spring_VFX_4_2`（親 `Spring_Cell_4_2`、`OnDemand`、`StreamByDistance = false`、payload は既定 Variant の 1 件）の `.unity`・`SceneResource`・SceneGraph node・`Total.asset` の親子 Edge・`SceneResourceMap` の entry・Addressables entry がそれぞれ 1 件ずつ §2.5 の path と規則で存在し、Map の `GenerateHash` が source から計算したハッシュと一致する。本番の Scene 実体は 652 + 2（Cell Lighting）+ 2 = 656 件（テスト fixture の Scene は別枠）。
- **S2 content の選択:** `SeasonSceneSelectionPolicy` が Spring の `Full` と `Whitebox` の両方で 2 つの VFX Scene を選び、`Payload/candidate mismatch` を出さない。
- **S3 PC で表示:** graphics あり（`-WithGraphics`）・PC Quality の Editor Play で、各 VFX Scene をロードすると、`Supported` のときその Scene の `VisualEffect` が全て有効になり、粒子が出る。2 つの `.vfx` を置いた専用テスト Scene で、`Supported` のとき対象の VFX だけを描画したカメラの明示の描画結果に背景以外の画素が閾値以上現れる（D1）。
- **S4 ロード経路:** `Season_Spring` のロードで `Spring_AtmosphereVFX` が載り、Season の Stable までに Stable になる。`Spring_Cell_4_2` が Stable になった後、companion profile `Full` と `VFX` では `Spring_VFX_4_2` が選ばれ、`Planner` と `Lighting` では選ばれない。
- **S5 Player:** BS4 Spring Player の build が成功し、Content Directory に 2 つの VFX Scene が入り、Player の起動後に Atmosphere が描画される。

### 2.2 受け入れ条件

検証する条件を括弧で併記する。テスト名は Phase B が付ける仮名。

- **AC1（S1・S2・I5）** `VfxSceneStructureTests`（`SampleGame.Tests.Editor`、EditMode、Scene を開かず asset を読む）: S1 の各項目。**SceneGraph source と生成済み Map の整合（R1）:** `Spring_AtmosphereVFX` と `Spring_VFX_4_2` の `SceneNodeData` がそれぞれ 1 件存在し、`Total.asset` の `SceneGraphEdges` が両 node を含み、`GetParent` がそれぞれ `Season_Spring`・`Spring_Cell_4_2` を返す。全 `SceneNodeData` と全 `SceneGraphEdges` から `SceneResourceGenerator.ComputeCurrentHash` を計算し、`SceneResourceMap.GenerateHash` と一致する（`WorldCompanionCreationTransaction` の作成後検査と同じ照合。生成器・SceneGraph Editor の改修と transaction の新設はしない）。Addressables entry は `Default Local Group`、address は scene path（既存の Cell companion と同じ規則。`WorldCompanionCreationTransaction.cs:152,240`）。SceneGraph node は `Assets/SceneGraphData/Nodes/Seasons/Spring_AtmosphereVFX.asset` と `Assets/SceneGraphData/Nodes/Cells/Spring_VFX_4_2.asset`。`Season_Spring` の `_children` は Lighting・Atmosphere・54 Cell。S2 は実 node から `SeasonSceneSelectionPolicy.SelectDetailed` を Spring の `Full` / `Whitebox` で呼び、選択に 2 つの Scene が入ることを確かめる（既存の build テストと同じ node・候補の作り方。Phase B が `SampleGame.Tests.Editor` の既存参照で足りることを確かめ、足りなければ Phase A に戻す）。
- **AC2（I2・I3）** 手順: (1) `Recalculate All` → 全 `SceneResource` の `_volume` と `StreamByDistance` を書き出す（A。HEAD の保存値との差は別に §6 に記録する）→ (2) 2 つの Scene を作り、VFX を置いて保存 → (3) `Recalculate All` → 同じものを書き出す（B）→ (4) A と B の差を §6 に記録する。書き出しは Editor 操作の一時スクリプト（commit しない）。**合格基準は A と B の比較だけ**で、差は I2 が許すものだけ（R4）。HEAD の保存値と A の差は既存のドリフトとして §6 に別に記録し、d3 の VFX による差と混同しない。合格例: Cell 216 件の `_volume` が A と B で 1 件も変わらない。不合格例: `Spring_Cell_4_2` の extent が VFX の bounds の分だけ広がる。判定 C は `VfxSceneStructureTests` の中で、`Spring_Cell_4_2` の `_volume` が A の値（Phase B が §6 に記録し、テストに定数で持つ）と一致することを確かめる。
  - **Cell VFX の描画 bounds の検査（R5・U3）:** `Spring_VFX_4_2` の `.vfx` の各 System は Bounds Setting Mode を Manual、空間を Local で作る（§2.3）。Local の Manual bounds は `VisualEffect` の Transform で world に写されるので、world での描画範囲は有効化後の `VFXRenderer.bounds` で観測する。(1) AC2 の手順 (2) の後に、保存した各 VFX Scene の `VFXRenderer` の有無と、Edit Mode の `renderer.bounds`（`VisualEffect` 無効のまま）を §6 に記録する。寸法が 0 なら、その事実を記録する（体積の収集から落ちるので A==B は成り立つが、bounds の規約は検査していない）。(2) 規約の検査は AC4 (b) の実 Scene で行う: `Supported` で `Spring_VFX_4_2` の `VisualEffect` が有効になった後、その Scene の全 `VFXRenderer.bounds` が寸法 0 でなく（空振りの防止）、`Spring_Cell_4_2` の `_volume`（world AABB）に含まれる。汎用の `.vfx` 静的解析器は作らない。
  - **Atmosphere:** 畳み先は `Season_Spring` で、I2 のとおり祖先の鎖の `_volume` の変化だけを許す。描画 bounds の Cell への包含は検査しない。
- **AC3（I3）** `PlacedVfxAuthoringTests`（`SampleGame.Tests`、EditMode）: 対象集合（§2.6。必須 2 Scene と依存で拾った Scene の和集合）の Scene を順に開き、`GetComponentsInChildren<VisualEffect>(includeInactive: true)` の全てについて I3 を確かめる。**必須 2 Scene（R3）:** `Spring_AtmosphereVFX.unity` と `Spring_VFX_4_2.unity` が存在しなければ失敗。それぞれに `VisualEffect` が 1 件以上あり、全てが `visualEffectAsset != null`、無効で保存、同じ GameObject に `PlacedVfxActivator` を持つ（`.vfx` の参照が外れて依存の走査から落ちた Scene も、必須側で失敗する）。**失敗経路:** 一時 Scene に「有効のまま保存した `VisualEffect`」「activator の無い `VisualEffect`」「非 active の GameObject に有効のまま置いた `VisualEffect`」を置き、検査関数がそれぞれ違反を返すことを別ケースで確かめる（検査関数は Scene を引数に取る走査にし、一時 Scene で呼べるようにする）。走査対象が空でないことだけでは合格にしない（必須 2 Scene の契約を上のとおり個別に検査する）。描画テスト用の fixture Scene は本番 authoring の対象集合に含めない。
- **AC4（S3・S4）** `VfxScenePlayTests`（`SampleGame.Tests`、`[UnityTest]`、`EnterPlayMode`、`-WithGraphics`。d2 の `VfxPlayTestFixture` の起動手順を使う）。観測語: Stable は `ISceneQuery.IsSceneStable`、「その Scene に属する」は `go.scene` の一致。`Supported` で (a) `Season_Spring` を `AddScene` して await → その時点で `Spring_AtmosphereVFX` が Stable、その Scene の `VisualEffect` が全て `enabled == true`、上限フレーム数内に `HasAnySystemAwake() || aliveParticleCount > 0`、その Scene の `VisualEffect.visualEffectAsset` が §2.5 の `.vfx` と同一 asset、(b) `Spring_Cell_4_2` を `AddScene` し、Stable の後に `Spring_VFX_4_2` を `AddScene`（companion driver の選択は (d) で見る）して同じ観測と、AC2 の描画 bounds の検査、(c) **専用テスト Scene での描画（D1・R2）:** `SampleGame/Tests/Rendering/Vfx/Fixtures/` の描画専用 `.unity`（§2.5）を `EditorSceneManager.LoadSceneAsyncInPlayMode`（Additive）で新しくロードする。Scene の中身は専用の Camera（黒背景、RT）と、本番 2 Scene と**同一の `.vfx` asset** を参照する `VisualEffect`（無効で保存 + `PlacedVfxActivator`）を持つ GameObject 2 個だけ（テスト専用の似た `.vfx` は禁止）。2 個は非 active で保存し、判定ごとに対象の 1 個だけを active にする。前提条件として、判定時にロード済みの全 Scene で有効な `Renderer` が対象の `VFXRenderer` だけであることを確かめる（成り立たなければ失敗）。§16.6 の warm-up（`VfxPlayTestFixture` の知見を再利用）の後に `camera.Render()` + `ReadPixels`、背景以外の画素が閾値以上（閾値と上限フレーム数は Phase B が fixture から決めて §6 に記録）。汎用の Layer 管理や描画テストの枠組みは作らない、(d) companion profile ごとの選択: 実 `SessionCellCompanionLoadDriver` のテスト（既存の `SessionCellCompanionLoadDriverTests` の形）に `Spring_VFX_4_2` の実 `SceneResource` を入れ、`Full` / `VFX` で選ばれ `Planner` / `Lighting` で選ばれない。
- **AC5（I1）** 同じ fixture で、Unsupported を返す backend を注入して `VfxCapabilityState` を `Unsupported` にした後に、**新しく** 2 つの Scene をロードする（AC4 で有効化された instance が同じ Play に残っていれば、それは数えない。activator は有効なものを無効化しない）: その Scene に属する `VisualEffect` の `enabled == true` が 0、`LogAssert.NoUnexpectedReceived`、Unity console のログの件数のロード前後の差 0（`ILogger` は file / stream に出て console に出ないので、`SeasonAtmosphereVfxScene` の Information 1 行は数えない）。続けて、Unsupported に切り替えた後に**新しく**ロードした専用テスト Scene の instance で、AC4 (c) と同じ対象の切替・前提条件・warm-up・計測を行い、画素が閾値未満。同じ process の AC4 (c) が閾値以上を示していることを陽性対照とする（先に有効化された VFX を停止させる仕様は要求しない）。`Undetermined` は writer の test backend を Dispose して作る（`VfxCapabilityState.ResetIfWriter`）。**不合格例:** warm-up をせずに画素 0 を合格にする。AC4 が失敗・Inconclusive のまま AC5 だけを合格にする。
- **AC6（I4）** 同じ fixture: (a) `Spring_Cell_4_2` を `UnloadScene` → 上限フレーム数内に `Spring_VFX_4_2` が unload され、`Spring_VFX_4_2` に属する GameObject が 0、(b) `Season_Spring` を unload → `Spring_AtmosphereVFX` に属する GameObject が 0、(c) 実 `SessionSeasonController` の既存テストの形で、Season の Stable の後に `Spring_AtmosphereVFX` が観測個体に登録されていることを確かめる（`RegisterNecessaryAlwaysChildren` は `NecessaryAlways` の子を全て登録するので、コードの変更は無い）。
- **AC7（S5）** 高コスト証拠（§5）。専用テスト Scene の描画成功を、実 Player の描画の代替にしない（D1）。
- **AC8（I5）** `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` が合格。base `6667f88f` → implementation head の `git diff --name-status` が §4 の変更対象に閉じ、`unity/Assets/OneStarMaker/` と `*.asmdef` の差分が 0。`.unity` の件数は **`Seasons/` 配下の本番 Scene が base から +2**、テスト fixture（`SampleGame/Tests/Rendering/Vfx/Fixtures/`）が +1 で、別枠に数える（リポジトリ全体では +3）。fixture の Scene は SceneGraph・`SceneResourceMap`・Addressables に入っていない。

| 証明したいこと | 検証する場所 |
|---|---|
| 2 つの Scene と `.vfx` の参照・graph・content の選択 | AC1・AC3、実 Scene の asset |
| 実 Scene のロード・`VisualEffect` の有効化・粒子の活動 | AC4 (a)(b)、実 Scene |
| VFX 固有の描画の陽性・陰性 | AC4 (c)・AC5、専用テスト Scene |
| 非対応時に実 Scene の `VisualEffect` が有効化されない | AC5、実 Scene |
| Cell / Season の unload と GameObject の消滅 | AC6、実 Scene |
| Player の実描画 | AC7、BS4 Spring Player |
- **AC9（回帰）** 判定必須テスト（§5）が failed 0。`SampleGame.Tests.Rendering.Vfx`、`OneStarMaker.Tests.Rendering.Vfx`、`SampleGame.Tests.Editor.WorldAuthoring`（既存の Cell companion の作成・回復・path 予算のテストを含む）が全件 passed。
- **AC10（S1、U1 (b)）** `WorldCompanionCreationTransactionTests` / `WorldCompanionRecoveryTests` / `WorldWorkspaceSelectionTests` / `WorldCompanionPathBudgetTests`（`SampleGame.Tests.Editor`、EditMode、既存の一時 fixture の形）に次を足す。(a) Season Atmosphere の plan で transaction が成功し、node の LoadType が `NecessaryAlways`、`StreamByDistance == false`、体積 0、親 Edge が Season、Map のハッシュが source と一致、Addressables entry が `Default Local Group` で address が scene path。(b) Season Atmosphere の plan で親が streaming Cell のとき、Cell companion の plan で親が Season のとき、それぞれ Preflight が拒否する。(c) Season Atmosphere の作成の途中に既存の fault injector で失敗させ、rollback で生成物が全て消え、source のハッシュが開始時と一致する（1 ケース）。(d) 作成の種類の項目が無い journal を Cell companion として回復する（互換）。(e) 4 季節の Season Atmosphere の plan の path が `tools/path-budget.json` の予算に収まる。(f) `GameSceneLoggingTests` で `Spring_AtmosphereVFX` と、Scene の無い季節の identity（例 `Summer_AtmosphereVFX`、親 `Season_Summer`）が `SeasonAtmosphereVfxScene` に分類される。既存の Cell companion のテストは変更せずに通る。

### 2.3 状態と順序

- **Atmosphere の LoadType（U6 の推奨案）:** `NecessaryAlways`。program §2.1 の `IncrementalAlways` を変える。理由: §1.3 の保留 unload の不具合で、ロード中の Season の unload が Atmosphere を残す（I4 違反）。`NecessaryAlways` なら Season の Stable が Atmosphere のロードを待ち、`SessionSeasonController.RegisterNecessaryAlwaysChildren` が観測個体に登録するので、枝の交換は Atmosphere の終端を待つ（Lighting と同じ扱い。SampleGame・Framework のコードの変更は無い）。代償は Season の Stable が Atmosphere のロードの分だけ遅れること（Season の切替は S-5 の Tunnel の中で行う想定）。Framework の不具合の修正は d3 では行わず、別のスライスへ送る（§2.4）。
- **Atmosphere と profile:** Atmosphere は Cell の companion ではないので、companion profile に関係なく Season と一緒に載る（`Planner` でも載る）。§2.7 U2。
- **Atmosphere の配置:** 固定の world 配置で、カメラを追従する script を持たない（Scene 跨ぎの参照の禁止と I3）。スポーン `(0,4)` の Cell の上空を含む範囲に置き、Player の初期カメラから見える位置にする（AC7 の観測点）。VFX asset の各 System の bounds は Manual・Local 空間（Cell VFX も同じ。AC2 の描画 bounds の検査は有効化後の `VFXRenderer.bounds` で行う）。季節全域を覆う作り込みは対象外で、代表として spawn 周辺の範囲に限る（U5）。正確な位置・範囲は Phase B が決めて §6 に記録する。
- **配置済み VFX の有効化:** 2 つの Scene の `VisualEffect` は無効で保存し、`PlacedVfxActivator` が Awake で `VfxCapabilityState` を読む。判定は `BeforeSceneLoad` の `VfxSystem` 生成で済んでいるので、どの Scene の Awake でも結果は公開済み（Architecture §24 §16.5）。有効化で初期イベントが送られ、自動で再生される。Scene 側は何もログに出さない。`Unsupported` / `Undetermined` では有効化せず、代替表示も出さない（D2。代替表示が無いことは d3 の失敗ではない）。
- **Scene class:** `Spring_AtmosphereVFX` は新しい `SeasonAtmosphereVfxScene`（no-UI。`CellCompanionScene` と同じ形で、親が `StreamByDistance` でないことを要求）。`GameSceneFactory` は、作成の道具の命名規則と同じ「identity が `{Season}_AtmosphereVFX` で、親が `Season_{Season}`」を `SeasonAtmosphereVfxScene` に分類する（U1 (b) で他の季節の Atmosphere も道具から作れるので、作れた Scene がロード時に "SceneFactory returned null" で落ちないようにする）。season 名の文法は SampleGame の制作規約に閉じる。
- **Season の子の作成（U1 (b)）:** 既存の `WorldCompanionCreationPlan` / `WorldCompanionCreationTransaction` を、Cell の子に加えて **Season 直下の Atmosphere VFX** を作れるように広げる。recovery journal・ownership proof・rollback の仕組みは再利用し、別の transaction は作らない。
  - plan に作成の種類（Cell companion / Season Atmosphere）を持たせる。Season Atmosphere の plan は `WorldCompanionCreationPlan` の新しい生成関数が `WorldSeason` から作り、identity `{Season}_AtmosphereVFX`、親 `Season_{Season}`、folder `Seasons/{Season}/{identity}/`、node `Assets/SceneGraphData/Nodes/Seasons/{identity}.asset`、`LoadType.NecessaryAlways`。
  - Preflight の親の検査は種類で分ける: Cell companion は従来どおり親が `StreamByDistance` の Cell。Season Atmosphere は親の `SceneResource` が `StreamByDistance == false` で、親 node が Scene payload を持たない（論理ノード）。どちらでも、既存の重複・hash・graph・Addressables の検査はそのまま通す。
  - node の作成と作成後の検査（`VerifyCommitted`）の LoadType は plan の値を使う（Cell companion は `OnDemand`、Season Atmosphere は `NecessaryAlways`）。`StreamByDistance == false`・体積 0（空の Scene を作るので両方で成り立つ）・親 link・Map のハッシュ・Addressables の検査は共通。
  - recovery journal に作成の種類を 1 項目足す。既定値は Cell companion とし、項目の無い既存の journal は従来どおり Cell companion として回復する（`LoadType.OnDemand` が enum の 0 であることに頼らず、種類の enum の 0 を Cell companion にする）。
  - World Workspace の窓に、選択中の season の「Atmosphere VFX を作成して開く」操作を 1 つ足す（`WorldCompanionSceneCreator` 経由。既に存在すれば作らずに開くか、存在を伝える）。program §3.4 の Role 別の open plan（VFX = Cell + Environment + Season Lighting + optional Cell VFX）は変えない。
  - 作成した Scene は空。VFX の配置・`PlacedVfxActivator`・保存は Phase B が Editor で行う（Cell VFX と同じ）。

### 2.4 ここでは答えない問いと所有スライス

- **Framework の `IncrementalAlways` の子の保留 unload が消費されない不具合（§1.3）:** 別の Framework 修正スライス（d3 と独立。所有 HANDOFF は未作成）。d3 は `IncrementalAlways` を使わないので依存しない。修正後に Atmosphere を `IncrementalAlways` へ戻すかはその時点の判断。
- Season 交換と Season 排他 controller の関係（Atmosphere は U6 で観測個体になる）: **S-4d5**（program §6.3 d5 行）。
- Planner profile で Atmosphere を読まない選択肢: **S-4d5**（Planner 実証の Phase A。d3 は §2.7 U2 の現状を記録する）。
- VFX の時間の所属、d3 の配置済み VFX への後からの変更: **S-4d4**。
- Event から VFX を起動する経路、動的 VFX: **S-4d4**。
- `(4,2)` 付近での Player の Cell VFX の描画（スポーン `(0,4)` から距離の外）: **S-9**。d3 の Player の証拠は Atmosphere の描画と、Cell VFX の content への収録まで。
- 季節全域の bounds が大きい VFX の culling と同時数: **S-9**。
- VFX Graph 非対応環境での ParticleSystem fallback（要否、対応 platform、表現の対応関係、選択の場所、Scene の寿命との整合、非対応時のテスト）: **後続の独立スライス（担当・時期未定）**。d4・d5・S-9 には割り振らない。後続の計画は今は作らない（D2）。
- Summer / Autumn / Winter の Atmosphere と他 Cell の VFX: 制作の判断（必要な季節・Cell だけ作る）。S-4 の範囲外。
- Atmosphere を World Workspace の Role 別 open plan に入れること（VFX role で Atmosphere も開く等）: 後続の authoring 改善（所有スライス未定）。d3 は作成と単独で開く操作まで（U1 (b)）。
- Lighting / Events 等、Atmosphere 以外の Season 直下の子を道具で作ること: 同上。
- authoring 検査を保存フックにすること: 同上。

### 2.5 path と構造（決定）

| Scene | `.unity` と `SceneResource` | SceneGraph node | 親 | LoadType |
|---|---|---|---|---|
| `Spring_AtmosphereVFX` | `Assets/SampleGame/InGame/InGameSession/Seasons/Spring/Spring_AtmosphereVFX/Spring_AtmosphereVFX.{unity,asset}` | `Assets/SceneGraphData/Nodes/Seasons/Spring_AtmosphereVFX.asset` | `Season_Spring` | `NecessaryAlways` |
| `Spring_VFX_4_2` | `.../Seasons/Spring/Cells/Spring_Cell_4_2/Spring_VFX_4_2/Spring_VFX_4_2.{unity,asset}` | `Assets/SceneGraphData/Nodes/Cells/Spring_VFX_4_2.asset` | `Spring_Cell_4_2` | `OnDemand` |

SceneGraph の Edge は `Assets/SceneGraphData/Graphs/Total.asset` に `Season_Spring → Spring_AtmosphereVFX`、`Spring_Cell_4_2 → Spring_VFX_4_2` の 2 件を足す。Addressables は `Default Local Group`、address は scene path。VFX の asset（`.vfx`）は各 Scene のフォルダに置く（Scene と一緒に移動・削除される制作単位にするため）。

描画テスト用の fixture Scene（D1）は `Assets/SampleGame/Tests/Rendering/Vfx/Fixtures/VfxSceneRender.unity`（仮名。Phase B が path 予算の中で決める）。本番の SceneGraph・`SceneResourceMap`・Addressables・Build Settings に入れず、`SceneResource` を作らない。中の `VisualEffect` は上の 2 つの `.vfx` を直接参照する。path の長さは `tools/path-budget.json` に従う（`WorldCompanionPathBudgetTests` が Cell の子の path を検査している）。

### 2.6 本文へ転記した実装制約（`AGENTS.md` の常時契約から）

- 依存は Game → Framework の一方向。asmdef 参照を追加しない（必要になったら Phase A に戻す）。
- アセットは `IAssetManagement` 経由（Scene のロードは `SceneDirector` 経由）。テストは `AssetDatabase` で読んでよい（Editor-only の test assembly）。
- `SceneState` の 14 値を変えない。状態変更は `SceneLifecycleManager` が所有する。
- 公開 API のログ抽象は `ILogger<T>`。`SeasonAtmosphereVfxScene` のログは `CellCompanionScene` と同じ Information 1 行（生成時）に限る。VFX 側（activator・backend）はログを出さない。
- Editor コードを Runtime アセンブリに置かない。Scene・`.vfx`・`SceneResource` の作成は Editor 操作で行い、YAML を手で編集しない。
- `record` を使わない。新規・編集する `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。Play 中の待ちはフレームで上限を持つ。
- path は `tools/path-budget.json` の予算に従う。
- ローカルの Unity Editor は必要なら起動・操作してよい。Phase B は対象を限定した Editor 操作とコンパイル確認まで。Unity テストと Build の実行・判定は Phase C。`unity test` / `unity run` は使わない。
- 参照 0 を削除理由にしない。
- **authoring 検査の対象集合（AC3、R3）:** 次の和集合。(1) 必須 2 Scene（`Spring_AtmosphereVFX.unity`・`Spring_VFX_4_2.unity`。欠損は失敗）。(2) `Assets/SampleGame/InGame/InGameSession/Seasons/` 配下の `.unity` のうち、`AssetDatabase.GetDependencies`（再帰）に `.vfx` を含むもの（VFX を置いた職種外の Scene を拾う。全 Scene を無条件に開かずに済む）。(1) があるので、`.vfx` の参照が外れた必須 Scene が対象から脱落しない。テスト fixture は含めない。d3 の時点では 2 件。

### 2.7 未決事項（A3 で発注者が裁定済み。結果は §2 決定記録 D3）

裁定: U1 = (b)、U2 = 現行案、U3 = 現行案 + R5、U4 = (a)、U5 = 現行案、U6 = (b)。以下は裁定前の選択肢と理由の記録。

- **U6 Atmosphere の LoadType（A2-2 の blocker への対応）:** 案 (a) Framework の保留 unload の修正スライスを先に行い、d3 は program どおり `IncrementalAlways`。案 (b) d3 は `NecessaryAlways`（Framework 差分 0。program §2.1 の行を d3 の Phase A の PR で更新する）。案 (c) `IncrementalAlways` のまま、ロード中の Season の unload の確認を d5 へ送る（既知の残留を抱えたまま GO にする）。**推奨 (b):** I4 を Framework に触れずに満たせ、Season controller の観測（枝の交換で終端を待つ）にも自動で入る。(c) は既知の不具合を受け入れ条件から外すことになる。Framework の修正は別スライスで行う（§2.4）。
- **U1 `Spring_AtmosphereVFX` の作り方:** 既存の作成 transaction は Cell の子専用。案 (a) Phase B が一度だけ使う Editor の一時スクリプトで作り、commit 前に削除する（構造は AC1 が恒久的に検査する。S-4b の生成器と同じ扱い）。案 (b) `WorldCompanionCreationPlan` / `Transaction` を Season の子に広げ、World Workspace から作れるようにする（A2-3 の推奨。recovery journal を再利用でき、他の季節にも使える）。**推奨 (a):** Atmosphere は「必要な季節だけ」で、S-4 では 1 件。(b) は 390 行の transaction の不変条件（親が Cell、`OnDemand`、体積 0）を変える変更で、d3 の問いに答えない。AC1 が entry の規則（group・address・node）を検査するので、一時スクリプトの誤りは build より前に見つかる。
- **U2 Atmosphere と companion profile:** Atmosphere は Season の子なので `Planner` / `Lighting` でも載る。**推奨:** d3 は受け入れ、事実を記録する。Planner で読まない選択肢は d5 へ送る（§2.4）。
- **U3 Cell の体積（N-9）の規約:** **推奨:** authoring の規約として「Cell VFX の描画 bounds（各 System は Manual・Local）は親 Cell の体積の内側に収める」を凍結し、AC2 の手順（記録）と AC4 (b)（有効化後の `VFXRenderer.bounds` の包含と寸法 0 でないこと）で検査する（R5）。Phase B が AC2 で Cell の体積の差を観測したら、まず VFX asset の bounds を Cell の内側に直す（authoring の修正で B 適応）。それでも体積が変わる、または規約自体が成り立たない（霧が Cell の外へ広がる表現が必要等）なら Phase A に戻す。Framework の体積収集に VFX の型の除外を入れる案は採らない（Framework が VFX Graph を知ることになり、program §10 に反する）。
- **U4 Player の証拠の範囲:** 案 (a) 高コスト証拠として、BS4 Spring Player を 1 回 build し、(i) build の成功と Content Directory に 2 つの VFX Scene が入ることの機械照合、(ii) Player を起動し、Spring の Stable の後に AI がデスクトップのスクリーンショットを撮り、人間が画像で Atmosphere の描画を判断する（画像を evidence に入れる）。案 (b) `PlayerContentSmoke` に VFX の probe（有効な `VisualEffect` の件数と粒子の有無）を足して (ii) を機械化する（A2-3 の推奨）。案 (c) Player は (i) だけにし、描画は S-9 へ送る。**推奨 (a):** d2 の発注者決定（U1）は Player の描画を d3 で確かめるとしたので (c) は決定の変更になる。(b) は Player の起動経路に検証の処理を足し、BS4 の probe の契約と「Scene 側は追加のログを出さない」に触れる。(a) は人間の判断を 1 枚の画像に限定でき、`Player.log` の Error 0 も機械で見られる。Atmosphere が Player で見えない場合は NO-GO。
- **U5 VFX の表現:** Cell VFX は地表の霧（Cell の内側の範囲）、Atmosphere は浮遊する花びら／塵を、スポーン `(0,4)` の Cell の上空を含む固定の範囲に置く。**推奨:** この 2 つ。見た目の作り込みは対象外で、粒子が出て画素と画像で判定できれば足りる。

## 3. 責務マップ（A1）

### 3.1 ファイルと責務

| ファイル | 責務 | 変更理由 | 所有者・寿命 | 依存 | テスト境界 | 行数 |
|---|---|---|---|---|---|---|
| `SampleGame/InGame/InGameSession/World/SeasonAtmosphereVfxScene.cs`（新規） | Season 直下の Atmosphere VFX Scene の no-UI lifecycle | Season の子の Scene class の規則 | `SceneDirector` が Scene の寿命で所有 | `OneStarMaker.Runtime.SceneSystem`、`ILogger` | `GameSceneLoggingTests` に分類の 1 ケース | 約 40 |
| `SampleGame/DependOnAll/GameSceneFactory.cs` | identity → Scene class | `{Season}_AtmosphereVFX`（親 `Season_{Season}`）の分類を足す | — | 既存 | 同上 | 99 → 約 105 |
| `SampleGame/DependOnAll/Editor/WorldAuthoring/WorldCompanionCreationPlan.cs` | 作成の path・identity・親・種類・LoadType の計画 | Season Atmosphere の生成関数と種類・LoadType を足す（U1 (b)） | Editor 操作の 1 回分 | 既存 | AC10 (a)(b)(e) | 93 → 約 140 |
| `.../WorldAuthoring/WorldCompanionCreationTransaction.cs` | 作成の transaction・検査・rollback・回復 | Preflight の親の検査を種類で分け、LoadType を plan から取る | journal が回復まで所有 | 既存 | AC10 (a)〜(d) | 390 → 約 420 |
| `.../WorldAuthoring/WorldCompanionRecoveryJournal.cs` | 回復 journal の永続化 | 作成の種類の項目（既定は Cell companion） | 既存 | 既存 | AC10 (d) | 208 → 約 215 |
| `.../WorldAuthoring/WorldCompanionSceneCreator.cs`、`WorldWorkspaceWindow.cs` | 窓からの作成と開く操作 | Season の「Atmosphere VFX を作成して開く」 | — | 既存 | 窓は手動（Phase B が Spring で 1 回使い、それが `Spring_AtmosphereVFX` の作成になる） | 64 → 約 95、87 → 約 100 |
| `Seasons/Spring/Spring_AtmosphereVFX/`（`.unity`・`.asset`・`.vfx`） | 季節全域の VFX の Scene と payload | 新規 | Scene の寿命（Season の子、`NecessaryAlways`） | `PlacedVfxActivator` | AC1〜AC7 | — |
| `Seasons/Spring/Cells/Spring_Cell_4_2/Spring_VFX_4_2/`（`.unity`・`.asset`・`.vfx`） | Cell 常駐の VFX の Scene と payload | 新規 | Scene の寿命（Cell の子、`OnDemand`） | 同上 | AC1〜AC6 | — |
| `Season_Spring.asset`、`Spring_Cell_4_2.asset`、SceneGraph node、`Assets/SceneGraphData/Graphs/Total.asset`、`SceneResourceMap.asset`、Addressables の group | 親子 link・Edge・Map（`GenerateHash` を含む）・entry | 2 件の追加 | — | — | AC1 | 追加のみ |
| `SampleGame/Tests/Rendering/Vfx/Fixtures/VfxSceneRender.unity`（新規、仮名） | 描画の陽性・陰性だけを測る専用 Scene（Camera と、本番の `.vfx` を参照する `VisualEffect` 2 個） | D1 | テストが Play 中にロードし、テストの後始末で破棄 | 本番の 2 つの `.vfx`、`PlacedVfxActivator` | AC4 (c)・AC5 | — |
| `SampleGame/Tests/Editor/WorldAuthoring/VfxSceneStructureTests.cs`（新規、`SampleGame.Tests.Editor`） | AC1・AC2 の構造・content 選択・体積の検査 | — | — | 既存参照（`OneStarMaker.Editor`・`Unity.Addressables`・`SampleGame.DependOnAll.Editor`） | EditMode | 約 180 |
| `SampleGame/Tests/Rendering/Vfx/PlacedVfxAuthoringTests.cs`（新規、`SampleGame.Tests`） | AC3 の authoring 検査（Scene を引数に取る走査と、対象集合の列挙） | — | — | `SampleGame.Common`（`PlacedVfxActivator`） | EditMode | 約 150 |
| `SampleGame/Tests/Rendering/Vfx/VfxScenePlayTests.cs`（新規、`SampleGame.Tests`） | AC4〜AC6 の Play 中の確認（実 Scene と専用テスト Scene） | — | — | `VfxPlayTestFixture`（d2） | `-WithGraphics` の Play | 約 350 |

- テストの asmdef の分け方（A2-1 #1〜#3）: AC1・AC2 は SceneGraph node（`OneStarMaker.Editor` の `SceneNodeData`）、Addressables の Editor API、`SeasonSceneSelectionPolicy`（`SampleGame.DependOnAll.Editor`）を読むので、それらを参照済みの `SampleGame.Tests.Editor` に置く（Addressables の Editor 型は既存の `WorldCompanionCreationTransactionTests` と同じ reflection の形）。AC3〜AC6 は `PlacedVfxActivator`（`SampleGame.Common`）と d2 の fixture を使うので `SampleGame.Tests` に置く（`SampleGame.Tests.Editor` は `SampleGame.Common` を参照しない）。この分け方で asmdef 参照の追加は 0。
- 新しい公開 API: なし（`SeasonAtmosphereVfxScene` は SampleGame 内の public sealed class で、既存の `CellCompanionScene` と同じ公開度）。Framework の変更: なし。

### 3.2 配置と依存を選んだ理由

- `SeasonAtmosphereVfxScene` を `CellCompanionScene` と分けるのは、`CellCompanionScene` が「親が streaming Cell」を ctor で要求する（分類の誤りを早く落とす）からで、条件を緩めるとその検査を失う。
- Atmosphere の分類は SampleGame の factory に置く（Season の子の役割は SampleGame の制作規約。Framework は知らない）。構造だけの分類（「親が Season で `StreamByDistance` でない」）は Lighting と区別できないので、identity の命名を併せて見る。A1 では `"Spring_AtmosphereVFX"` の 1 行を足す案だったが、U1 (b) で作成の道具が全季節の Atmosphere を作れるようになったため、道具の命名規則（`{Season}_AtmosphereVFX`、親 `Season_{Season}`）と同じ規則で分類する。道具が作れる Scene と factory が分類できる Scene を一致させるため。
- Season の子の作成を既存の transaction の拡張にするのは、recovery journal・ownership proof・rollback と作成後の検査（親 link・Map のハッシュ・Addressables）を二重に持たないため。別の transaction を作ると、Unity の途中終了からの回復の経路が 2 つになる。拡張は「親の種類と LoadType」の 2 点に限り、Cell companion の不変条件（親が streaming Cell、`OnDemand`）は種類が Cell companion のとき従来どおり検査する。

### 3.3 harvest 先

Architecture §24 §16.6 に「実 Scene での確認（Atmosphere / Cell VFX、非対応時に描画されない）」と authoring 検査の場所を 1〜2 行、§16.7 の d3 行を削除。§05 §5.13 に Season 直下の Atmosphere VFX（`NecessaryAlways`、profile に関係なく載る、Scene class）を追記。Cell の体積と VFX の bounds の規約（U3）は Cell の体積を説明している既存の節（§21 または STREAMING_CURRENT_SPEC、Phase D が既存の記述の場所に合わせる）に 1 行。program §2.1（Atmosphere の LoadType）と §6.3 の d3 行を更新（§2.1 は Phase A の PR で、d3 行の完了は Phase D で）。program の ParticleSystem fallback の行（§1 確定事項・§6.1）は A3 で改訂済み（D2）。後続スライスの所有が決まるまで program に「後続スライス（担当未定）」として残す。

### 3.4 行数警報

- 該当なし。新規ファイルはいずれも 500 行未満の見込み。`VfxScenePlayTests` が 500 を超える見込みになったら、表示（AC4・AC5）と寿命（AC6）で分ける。

## 4. 実装計画

- 変更対象: §3.1 の全ファイル（新規フォルダの `.meta` を含む）、Addressables の group の asset、`Assets/SceneGraphData/Graphs/Total.asset`、`SceneResourceMap.asset`、`Season_Spring.asset`・`Spring_Cell_4_2.asset` と体積の再計算で変わる `SceneResource`（AC2 で列挙したものだけ）、描画テスト用の fixture Scene（`SampleGame/Tests/Rendering/Vfx/Fixtures/`）、program 文書（§2.1・§6.3 d3 行。fallback の行は A3 で改訂済み）。
- 順序: (1) AC2 の書き出し A → (2) `Spring_VFX_4_2` を World Workspace の作成機能で作り、Editor で VFX を置く（`VisualEffect` 無効 + `PlacedVfxActivator`、各 System の bounds は Manual・Local で Cell の内側）→ (3) 作成 transaction の Season の子への拡張と窓の操作（U1 (b)）、AC10 のテスト → (4) 窓の操作で `Spring_AtmosphereVFX` を作り、Editor で VFX を置く → (4') 保存と `Recalculate All`、AC2 の書き出し B と差の確認（U3）→ (5) 描画テスト用の fixture Scene を作る（本番の 2 つの `.vfx` を参照）→ (6) `SeasonAtmosphereVfxScene` と factory → (7) テスト → (8) contract-audit / docs-audit、Editor のコンパイル確認。
- Phase B から Phase A へ差し戻す条件: asmdef 参照の追加が要る。Framework の変更が要る。U1 (b) の拡張で、既存の Cell companion のテストの期待値を変える必要が出る、または recovery journal の回復の意味（rollback の順序・barrier）を変える必要が出る。U3 の authoring の修正で Cell の体積が戻らない。`PlacedVfxActivator` / `VfxCapabilityState` の変更が要る。`SessionSeasonController` の変更が要る。
- 対象外を維持する方法: Events Scene を作らない。`VfxSystem` / `IVfxSystem` を使わない。Framework の保留 unload を直さない（§2.4）。ParticleSystem への切替の component・配線・抽象 API・共通 fallback manager を足さない（D2）。作成 transaction の拡張は Season Atmosphere に限り、Season 直下の任意の子を作る汎用化はしない（U1 (b)）。汎用の描画テスト枠組み・Layer 管理・bounds 解析器、恒久的な Player probe を作らない。製品コードに描画の probe を足さず、PC Scene の camera を検証用に変えない。

## 5. テストとレビュー計画

- 単体テスト: `GameSceneLoggingTests` に Atmosphere の分類（AC10 (f)）。AC3 の検査関数の失敗経路。AC10 の作成 transaction の拡張。
- 差し戻し中の起点 `-Filter`: `SampleGame.Tests.Rendering.Vfx;SampleGame.Tests.Editor.WorldAuthoring;SampleGame.Tests.App;SampleGame.Tests.Streaming`（Play 中のテストを含むので `-WithGraphics`。C が根拠付きで変更可）。
- 判定必須テスト（GO 候補 head）: `pwsh tools/run-tests.ps1 -WithGraphics`（空 filter の全 EditMode 回帰）。XML で §3.1 の新しいテストの収録、failed 0、本スライスのテストの Inconclusive・Ignored 0。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: `VfxScenePlayTests`（d2 の前例と同じく Editor-only asmdef の EditMode テストで `EnterPlayMode`）。
- 操作・実行時・目視条件の検証経路:
  - **AC4〜AC6:** 担当は Phase C（Unity テスト）。環境はこの PC、`pwsh tools/run-tests.ps1 -WithGraphics -Filter SampleGame.Tests.Rendering.Vfx`。観測は XML とテストが出力する計測値（graphics device、画素数、閾値、到達フレーム数、`VisualEffect` の件数、専用テスト Scene の前提条件（有効な `Renderer` が対象だけ）、Cell VFX の `VFXRenderer.bounds`）。
  - **AC2:** 担当は Phase B（作成の前後の書き出し）と判定 C（テスト）。
  - **AC7（Player、U4 案 (a)）:** 担当は判定 C の実行担当（最終証拠ラウンド）。(1) 対象 head の worktree で Editor を開き `Tools/OSM/Content/Build BS4 Spring Player` を 1 回実行する。(2) build の出力と Content Directory の一覧に `Spring_AtmosphereVFX` と `Spring_VFX_4_2` があることを機械照合する。(3) Player を起動し（`fullscreenMode: 1` なのでデスクトップの撮影に写る）、`Player.log` で Spring `(0,4)` の Stable を確認した後に AI がデスクトップのスクリーンショットを 1 枚撮り、Player を正常終了する。(4) 画像・`Player.log`・対象 head・時刻を evidence に入れる。(5) 人間が画像で Atmosphere の描画を判断し、判断を記録する（観察記録の受理。C' が画像を再評価できるよう画像を bundle に入れる）。
- 未知の操作経路の疎通結果: **未確認。初回確認は Phase C**（AC4〜AC6 の実 Scene のロード、AC7 の Player）。理由: Unity テストと Build は Phase C の責任。担当: 発見 C の起点 filter の最初の実行（AC4〜AC6）、判定 C の実行担当（AC7）。不成立時の対応: AC4 (c) の描画が不成立なら d2 §2.7 U3 と同じ順（別の明示的な描画経路 → 人間の画像判断 → NO-GO）。AC7 の Player が起動しない・Spring に達しない場合は基盤不備か実装欠陥かを切り分けて記録し、人間に返す。
- 人間の判断が必要な条件: AC7 (5) の Player の画像。観察記録の受理に加え、画像を evidence に入れて C' が再評価できるようにする。
- 高コスト証拠: AC7（BS4 Spring Player の build と起動）。独立した期待値: build 成功、Content Directory に 2 つの VFX Scene、`Player.log` に例外・Error が 0、画像に Atmosphere の粒子が写っている（人間の判断）。手順は判定 C → 暫定 C' → AC7 の取得 → 最終証拠ラウンド（review-evidence の「高コスト証拠と最終証拠ラウンド」）。
- 判定 C の照合 script: なし（XML のテスト名・件数、build の出力、`Player.log`、contract-audit・docs-audit で判定する）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC8 の `git diff --name-status`、`.unity` の件数。
- A0/A1 主担当: Claude Code（Anthropic）
- A2 独立レビュー: 下記
- A3 統合担当: 主担当が採否案と本文改訂を作り、発注者が U1〜U6 を裁定して凍結を承認した（D3）。
- C' 用に予約した担当: xAI 系列（`cursor-agent` の Grok）を Phase A で使わずに残した。
- 独立性の強化条件を満たせない場合の理由: 未定（Phase B・C のモデルが決まった時点で記録する）。

### A2 独立レビュー

- 入力版: 未 commit の版。`docs/handoff/S-4D3_VFX_SCENES.md` の content sha256 `3019e0f2a46a9f3d381783fe491f046b4c929c5cd3c5f5573a38a6e8f2476464`。3 件とも sha256 の一致を確認した。
- 3 件を同じ入力版で並行して実施した。互いの指摘は見せていない。全て主担当の session が起動した別 context の subagent で、ファイルは変更していない。
  - A2-1 アーキテクチャゲート（architecture-gates.md の Phase A 7 項目と A0 の事実照合）: Claude Sonnet 系列。blocker 0、major 2、minor 5。
  - A2-2 不変条件の反証・失敗経路・VFX Graph の事実確認: Claude Fable 系列。blocker 1、major 4、minor 6。
  - A2-3 初稿（§2 以降）を読まず A0 だけから代替構成を出す: Claude Opus 系列。
- 独立性制約あり: A2-1〜A2-3 は主担当と同じベンダー（Anthropic）。xAI 系列は C' 用に残した。
- A0 の事実記述（§1.3）は A2-1 が行番号で照合し、誤りはテストの置き場所の結論（§3.1）だけだった。A2-2 の blocker の根拠（保留 unload）は主担当がコードで確かめ、§1.3 に追記した。

### A3 統合（主担当の採否案。発注者が承認済み、D3）

| 指摘 | 採否 | 反映先 |
|---|---|---|
| A2-2 #1 `IncrementalAlways` の子の保留 unload が消費されず、ロード中の Season の unload で Atmosphere が残る（blocker） | 採用。主担当がコードで確認。U6 として裁定を求め、推奨は `NecessaryAlways` | §1.3、I4、S1、§2.3、§2.4、U6、AC6 |
| A2-2 #2 AC6 (c) の窓は競争条件で Inconclusive 0 と矛盾 | 採用（U6 (b) で (c) の窓自体が無くなる）。(c) を観測個体の登録の確認に置き換えた | AC6 |
| A2-1 #1・#2・#3 テストの置き場所の結論が asmdef 上成り立たず、表と本文が食い違う | 採用。AC1・AC2 は `SampleGame.Tests.Editor`、AC3〜AC6 は `SampleGame.Tests` | §3.1 |
| A2-1 #4・#5 Addressables の group・address と node の置き場所を具体化 | 採用 | §2.5、AC1 |
| A2-1 #6 体積の変化は Atmosphere の祖先の鎖全体に及ぶ | 採用 | I2、AC2 |
| A2-1 #7・A2-2 #8 `VisualEffect` 無効時の `VFXRenderer` の bounds が未確認。寸法 0 なら AC2 が規約を検査しない | 採用。`VFXRenderer` の有無と bounds を記録し、VFX asset の Manual bounds が Cell の体積に含まれることを直接検査する | I3、AC2、U3 |
| A2-2 #3 `Recalculate All` のドリフトが基準値に混入 | 採用。作成前にも `Recalculate All` を行い、HEAD との差は別に記録 | AC2 |
| A2-2 #4 Atmosphere の配置と bounds が未決で、観測点が定義できない | 採用。固定配置・追従 script なし・スポーン上空を含む範囲・Manual bounds | §2.3、U5、I3 |
| A2-2 #5 AC1 が content の選択規則を見ていない | 採用。`SeasonSceneSelectionPolicy` を実 node で Full / Whitebox について呼ぶ | S2、AC1 |
| A2-2 #6 AC5 の読み違い（同じ Play に残る instance、ログの sink、`Undetermined` の作り方） | 採用 | AC5 |
| A2-2 #7 AC3 の走査は `includeInactive: true` | 採用（失敗経路にも 1 ケース）。Scene 跨ぎ参照の検査は Unity が保存時に拒否するので外した | I3、AC3 |
| A2-2 #9 陽性対照のカメラを bounds の内側に置く | 採用 | AC4 (c)、AC5 |
| A2-2 #10 観測語（Stable、Scene に属する）の固定 | 採用 | AC4 |
| A2-2 Player の事実（`VFXManager.asset` の shader は Project Settings として Player に入る、asset 固有の shader は `.vfx` の sub-asset として Scene bundle に入る、`fullscreenMode: 1`） | 確認（AC7 の手順に反映） | §5 |
| A2-3 U1 は transaction を Season の子へ一般化すべき | 主担当は不採用を推奨したが、発注者が (b) を裁定（D3）。拡張は Season Atmosphere に限る | U1、§2.3、AC10、§3.1、§3.2、§4 |
| A2-3 authoring 検査の対象を `.vfx` への依存で選ぶ | 採用（名前の規約より漏れが少なく、全 Scene を開かずに済む） | §2.6 |
| A2-3 Player の描画を `PlayerContentSmoke` の probe で機械化 | 不採用（U4 (b) として併記） | U4 |
| A2-3 factory は identity の明示（Lighting と同じ形） | 確認（初稿と同じ） | §3.2 |
| A2-3 Planner でも Atmosphere が載ること、Season 交換との関係、bounds の大きい VFX の culling は後続へ | 採用（d5・S-9 へ） | §2.4 |
| A2-3 `IncrementalAlways` のロード中の例外が見えなくなる | U6 (b) で該当しなくなる | — |

### A3 改訂レビュー（2026-10-26、発注者経由の改訂指示）

判定は条件付き GO（構成は妥当、全面再設計は不要）。指示に含まれた発注者決定 2 件は §2 の決定記録 D1・D2。レビュー側の U1〜U6 の推奨（U1 (a)、U2 現行、U3 現行 + R5、U4 (a)、U5 現行、U6 (b)）は主担当の推奨と一致した。その後の発注者の裁定は U1 だけ (b)、他は推奨どおり（D3）。レビューが「作らない」に挙げた World Workspace transaction の Season 一般化は、U1 (b) の裁定で Season Atmosphere に限って範囲に入った。

| 指摘 | 採否 | 反映先 |
|---|---|---|
| R1 SceneGraph source（node・`Total.asset` の Edge）と生成済み Map のハッシュの整合を AC1 に追加。`Total.asset` を変更対象に明記 | 採用。主担当が `ComputeCurrentHash` / `GenerateHash` と transaction の照合（`:144-146`、`:259-261`）をコードで確認 | §1.3、S1、AC1、§2.5、§3.1、§4 |
| R2 AC4 (c)・AC5 の画素検査を本番と同一の `.vfx` を使う専用テスト Scene へ移す（発注者決定 D1） | 採用 | D1、I1、S3、AC4 (c)、AC5、AC7、AC8、§2.5、§3.1、§4、§5 |
| R3 AC3 の対象集合に必須 2 Scene を和集合で含め、`.vfx` の参照外れで脱落させない | 採用 | AC3、§2.6 |
| R4 AC2 の合格基準を A==B に一本化し、テストの期待値も A から取る | 採用 | AC2 |
| R5 Cell VFX の Manual bounds の検査方法を具体化 | 採用。各 System を Manual・Local で作り、world の描画範囲は有効化後の `VFXRenderer.bounds` で観測（AC4 (b)、寸法 0 を不合格）。EditMode の寸法 0 は記録だけ | I3、AC2、AC4 (b)、§2.3、U3 |
| fallback の後続化と program の改訂（発注者決定 D2） | 採用。program §1 確定事項・§6.1 を改訂 | D2、§1、§1.1、§2.1、§2.3、§2.4、§3.3、§4 |

## 6. Phase B 実装結果

- 対象 head: Phase B 完了 commit（凍結版 `a7ff035a` の子。SHA は引継ぎ票で渡す）。workflow task は `s-4d3-vfx-scenes` を start 済み（version 1）。
- 実行していないもの: Unity テスト（`run-tests.ps1`）、Addressables / Player の Build、AC7。Phase C の責任（§5）。Editor のコンパイル確認は行い、全 asmdef が errors 0 で通った（新しいテスト型 4 件と追加ケースの読み込みも確認）。`contract-audit` errors 0、`docs-audit` errors 0（警告 1 件は §7/§8 の「未着手」を埋まりと数える既存の判定）。
- 動作確認: 対象なし（§5 の検証経路の条件は Unity テスト（AC4〜AC6）、Phase B 自身の書き出し（AC2）、高コスト証拠（AC7）で、動作確認の対象外）。窓の「Create + Open Spring_AtmosphereVFX」ボタンは GUI では押しておらず、ボタンが呼ぶ `WorldCompanionSceneCreator.CreateOrOpenSeasonAtmosphere(Spring)` を Editor で直接呼んで作成した（§3.1 の「Phase B が Spring で 1 回使う」はこの呼び出し）。`Spring_VFX_4_2` も同様に `CreateAndOpen`（Spring, 4, 2, VFX）で作った。

### 実装

- `SeasonAtmosphereVfxScene`（no-UI、親が streaming でないことを ctor で要求、生成時の Information 1 行のみ）と、`GameSceneFactory` の分類（identity `{Season}_AtmosphereVFX` かつ親 `Season_{Season}` かつ親が streaming でない）。
- 作成 transaction の拡張（U1 (b)）: `WorldCompanionCreationKind`（`CellCompanion = 0`、`SeasonAtmosphere = 1`）を plan と journal（`creationKind`、未知の値は corrupt）に持たせ、LoadType は `WorldCompanionCreationPlan.LoadTypeFor(kind)` から引く。Preflight の親の検査を種類で分けた（Season Atmosphere は親が streaming でなく、親 node の payload が 0）。窓に「Create + Open / Open {Season}_AtmosphereVFX」を 1 つ足した（既にあれば開くだけ）。
- テスト: `VfxSceneStructureTests`（AC1・AC2 の定数）、`PlacedVfxAuthoringTests`（AC3、検査関数は Scene を引数に取り、失敗経路 4 件と適合 1 件）、`VfxScenePlayTests`（AC4 (a)(b)・AC5 の実 Scene・AC6 (a)(b)）、`VfxSceneRenderPlayTests`（AC4 (c)・AC5 の描画）、`SessionCellCompanionLoadDriverTests` に AC4 (d)、`SessionSeasonControllerTests` に AC6 (c)、AC10 (a)〜(f) を既存 4 ファイルと `GameSceneLoggingTests`（クラス名は `GameSceneFactoryTests`）へ追加。既存のテストの本体と期待値は変えていない（共有の `WorldCompanionTestScope` に種類の引数（既定は Cell companion）を足し、`AssertCommitted` に node / resource の LoadType の一致を足した）。

### B 適応（Phase A は再開しない）

1. **`WorldCompanionOwnershipProof.cs` の変更（§3.1 に無いファイル）:** 回復時の所有の証明が node / resource の LoadType を `OnDemand` 固定で照合しており、Season Atmosphere の rollback（AC10 (c)）が「Node ownership mismatch」で止まる。§2.3「node の作成と作成後の検査の LoadType は plan の値を使う」の範囲として、journal の種類から LoadType を引くようにした。resource は生成器が node から写すまで既定値（`OnDemand`）なので、既定値と計画の値の両方を受ける（Cell companion では同じ値）。rollback の順序・barrier は変えていない。AC10 (c) は生成の前（`AddressableCreated`）と後（`Verified`）の 2 点で失敗させる。
2. **`VfxScenePlayTests` の分割（§3.4 の行数警報）:** 1 ファイルで 610 行になったので、描画の専用 Scene の部分を `VfxSceneRenderPlayTests.cs`（222 行）へ分け、残りは 391 行。§3.4 の例示（表示 / 寿命）と違い、bulk を持つ描画の補助で切った（寿命だけを外すと 500 行を下回らない）。共有する起動条件 3 つ（能力の公開・PC Quality・graphics の計測値）は d2 の `VfxPlayTestFixture` に protected で移した。
3. **AC4〜AC6 の Scene は test が持つ SceneDirector で載せる:** アプリの SceneDirector で `Season_Spring` を `AddScene` すると祖先の InGameSession が載って距離ストリーミングが動き、`WorldStreamingController.IssueRemovesOutsideRetain` が読み込み半径の外の `Spring_Cell_4_2` を次の Tick で外す（テストが載せた Cell も対象）ため、AC4 (b)・AC6 (a) が競合する。test の director はアプリの起動が作った UICommon・LoadingDisplay・AssetManagement・variant・Content Directory の設定を reflection で読み（`BuildContentDirectoryIntegrationTests` と同じ観測）、実 `SceneResource` 4 件（Season_Spring、Atmosphere、Cell 4_2、VFX 4_2）の複製で Season_Spring を根にした Map を作る（同じ `.unity`・payload・LoadType・体積）。Scene class は実 `GameSceneFactory` が分類する。`Spring_Lighting` は RenderEnvironment の lease を取るので複製に入れない。観測語（`IsSceneStable`、`go.scene` の一致）と期待値は §2.2 のまま。Phase C で前提が崩れた場合（UICommon の取得等）は基盤不備として記録する。
4. **`SampleGame.Tests` は Addressables を参照しないので、Play テストの Scene の path は payload の GUID ではなく `SceneResource` と同じフォルダ・同じ名前の `.unity` から引く。** payload の GUID との一致は `VfxSceneStructureTests`（`SampleGame.Tests.Editor`）が照合する。asmdef は足していない。

### AC2: 体積の書き出しと比較

- 書き出しは Editor の一時スクリプト（commit しない）。全 `SceneResource` の identity・path・`StreamByDistance`・LoadType・`_volume`（center / size を round-trip の書式）・親。
- HEAD の保存値: 453 件（`StreamByDistance == true` 216 件）。手順 (1) の `Recalculate All` は「更新 0 件」で、A は HEAD の保存値と完全一致（既存のドリフト 0）。
- 手順 (3) の `Recalculate All` も「更新 0 件」。B は 455 件。**A と B の差は新しい 2 行（`Spring_AtmosphereVFX`、`Spring_VFX_4_2`。どちらも `_volume` は 0、`StreamByDistance == false`）だけで、既存 453 件（Cell 216 件を含む）の `_volume`・`StreamByDistance`・LoadType・親は完全一致。** `Season_Spring` の `_volume` も 0 のまま（I2 が許す祖先の鎖の変化は起きていない）。
- 判定 C 用の定数: `Spring_Cell_4_2` の `_volume` は center (1125, 24.5, 625)、size (245, 49, 245)（world AABB は x 1002.5〜1247.5、y 0〜49、z 502.5〜747.5）。`VfxSceneStructureTests.ExpectedCellVolume` に持つ。
- 描画 bounds（R5 の (1)）: 保存直後の Edit Mode で、両 Scene の `VFXRenderer` は存在し、`enabled == false`、`renderer.bounds` の寸法は 0（center は各 GameObject の位置）。寸法 0 なので体積の収集から落ち、A == B はこの事実でも成り立つ（bounds の規約はここでは検査していない。AC4 (b) が有効化後に検査する）。d2 HANDOFF §2.5 の「`VFXRenderer` は有効のまま」と違い、Edit Mode では `VisualEffect` の無効に合わせて `VFXRenderer.enabled` も false と読めた（Play 中の値は未観測）。

### 作成物の中身

- `.vfx` は VFX Graph の Simple Loop template から作り（Editor の `CreateTemplateAsset`）、各 System（Initialize / Update / Output の 1 系統）の Bounds Setting Mode を Manual、空間を Local にした（Gravity の Force だけは template どおり World 空間）。bounds は template の center (0, 1, 0)・size (3.5, 4, 3.5)、padding (0.5, 0.5, 0.5)。import 時に compile され、shader 1・compute shader 3・material 1 の sub-asset を持つ（VFX Graph 窓の compile 経路は窓で開いていない asset では使えなかったので使っていない）。
  - `Spring_VFX_4_2/Spring_VFX_4_2_GroundFog.vfx`（地表の霧の代表）
  - `Spring_AtmosphereVFX/Spring_AtmosphereVFX_Petals.vfx`（花びら／塵の代表）
- 配置（各 Scene の root に GameObject 1 個、`VisualEffect` 無効 + `PlacedVfxActivator`、他の component なし）:
  - `GroundFog`: 位置 (1125, 12, 625)、scale (20, 8, 20)。Local の bounds（padding 込み）の world の範囲は概算 x 1080〜1170、y 2〜38、z 580〜670 で、Cell の体積の内側。
  - `SpringPetals`: 位置 (125, 0, 1135)、scale 12。world の範囲は概算 x 98〜152、y -15〜39、z 1108〜1162。スポーン (125, 28, 1125) とその上空・前方（Player は +z を向いて出る）を含む（U5、AC7 の観測点）。
- 描画用 fixture `SampleGame/Tests/Rendering/Vfx/Fixtures/VfxSceneRender.unity`: 無効の Camera `VfxRenderCamera`（位置 (0, 1, -5)、黒背景、FOV 60。RT はテストが付ける）、非 active の `AtmosphereVfx` / `CellVfx`（原点、scale 1、本番の上の 2 つの `.vfx` を直接参照、`VisualEffect` 無効 + activator）。SceneResource・SceneGraph・Map・Addressables・Build Settings に入れていない。
- 計測の閾値と上限（AC4 (c)・AC5、d2 の fixture と同じ値）: 背景以外の画素（いずれかの channel > 8）を 128×128 の RT で数え、閾値 4。描画の上限 600 フレーム、warm-up の上限 600 フレーム、粒子の出現の上限 6000 フレーム（`ParticleFrameLimit`）、unload 後の消滅の上限 3000 フレーム。
- 構造（S1）: `Season_Spring._children` は Lighting・Atmosphere・54 Cell の 56 件、`Spring_Cell_4_2._children` に VFX が 1 件増えた。`Total.asset` に node 2・Edge 2。Addressables は `Default Local Group` に 2 entry（address は scene path）。`SceneResourceMap` は 2 件の追加と `_generateHash` の更新（`b802a1ddfb7f6e94` → `d0b4f213a2108c4c`）に加え、生成器の出力順で既存の 2 件（GUID `c84b0242…`・`4b037e04…`）の並びが入れ替わった（内容は不変）。

### 新しい事実（後続スライスの入力）

- **無題の Scene を開いた Editor から作成すると、作成も rollback も失敗する（既存の transaction の挙動）:** 起動直後の Editor（無題・未保存の Scene）で `CreateAndOpen` を呼ぶと、`NewScene(Additive)` が「untitled scene unsaved」で失敗し、rollback の `RestoreSceneManagerSetup`（元の構成が空）も「No loaded scene found」で失敗して journal が残った。回復は、保存済みの `Assets/Scenes/SampleScene.unity` を開き、journal の元の構成をそれに合わせてから公式の `RecoverPending` を実行した（生成物・フォルダは消え、source のハッシュは開始時の `b802a1ddfb7f6e94` に一致）。その後 SampleScene を開いた状態で作り直した。d3 の拡張では変えていない経路（Cell companion と共通）で、d3 の凍結条件の違反ではない。所有は World Workspace の authoring 改善（§2.4、所有スライス未定）。
- MobileDependencyResolver の `.pdb.meta` 5 件は、この worktree に `.pdb` 本体が無いため Editor の import が削除した。範囲外なので commit 前に戻した（Editor を開くたびに再発しうる）。

### 引継ぎ（発見 C へ）

- 起点 filter（§5）: `SampleGame.Tests.Rendering.Vfx;SampleGame.Tests.Editor.WorldAuthoring;SampleGame.Tests.App;SampleGame.Tests.Streaming`、`-WithGraphics`。新しいテストの名前空間は `SampleGame.Tests.Rendering.Vfx`（Play / authoring）と `OneStarMaker.Tests.Editor.WorldAuthoring`（構造・AC10。既存の WorldAuthoring テストと同じ名前空間）、`OneStarMaker.Tests.SampleGame`（factory）、`SampleGame.Tests.Streaming`。起点 filter の `SampleGame.Tests.Editor.WorldAuthoring` は既存テストの名前空間と一致しない可能性があるので、Phase C が XML で収録を確かめる。
- 初回疎通（§5）が未確認のもの: AC4〜AC6 の実 Scene のロード（上の B 適応 3 の test director を含む）、AC4 (c) の専用 Scene の描画、AC7。

## 7. Phase C

### 発見 C 第0巡（Phase B の差し戻し前）

- 種別・対象: 発見 C 第0巡（GO 判定ではない）。implementation base `6667f88f` → head `0bb0c46d`。作業ツリーは HANDOFF だけが異なる子 commit `02fd0263` で、未追跡の `DEBUGSTUDIO_S2_CUT_PLAN.md` 以外は clean だった。
- 担当・モデル: Claude Code（Anthropic、Claude Sonnet 系列）。C' 用の xAI 系列は未使用。
- 構造適合: 差分は §4 の変更対象に閉じる。`unity/Assets/OneStarMaker/` と `*.asmdef` の差分は 0（`git diff --name-only`）。`contract-audit` errors 0 / warnings 0、`docs-audit` errors 0（警告 1 件は §7/§8 の「未着手」を埋まりと数える既存の判定）。`GameSceneFactory` の分類順（Cell companion → Atmosphere）、`SeasonAtmosphereVfxScene` の ctor 検査、Preflight の種類別の親検査、journal の `creationKind`（未知の値は corrupt）は §2.3・§3.1 のとおり。
- 実行したコマンド: `pwsh tools/run-tests.ps1 -WithGraphics -Filter "SampleGame.Tests.Rendering.Vfx;SampleGame.Tests.Editor.WorldAuthoring;SampleGame.Tests.App;SampleGame.Tests.Streaming;OneStarMaker.Tests.Editor.WorldAuthoring;OneStarMaker.Tests.SampleGame"`（§5 の起点 filter に、§6 引継ぎが注意した実際の名前空間 2 つを足した。受け入れ条件の追加ではない）。環境: Direct3D11、`supportsComputeShaders=True`、`maxComputeBufferInputsVertex=128`、Linear。
- 結果: 170 件中 passed 165・failed 5・Inconclusive 0・Skipped 0。通ったもの: `VfxSceneStructureTests`（AC1・AC2）、`PlacedVfxAuthoringTests`（AC3）、AC10 の追加ケース、`GameSceneFactoryTests`（分類）、`SessionCellCompanionLoadDriverTests`（AC4 (d)）、`SessionSeasonControllerTests`（AC6 (c)）、既存の WorldAuthoring / Streaming / App のテスト。結果 XML・ログは git 管理外の `TestResults/` 配下（`evidence stage` はしていない。ローカル path は転記しない）。
- native 終了 stall: Unity の終了コードが 2 だった。AGENTS.md の既知 native 終了 stall として扱い、調査はしていない。追加の調査はしない。
- 作業ツリーの扱い: Editor が `unity/Assets/MobileDependencyResolver/` の `.pdb.meta` 5 件を削除したので `git checkout` で戻した（本スライスと無関係。§6 と同じ）。

**阻害する指摘（凍結済み条件への違反）**

| ID | 違反する条件 | 一次証拠 | 新しい事実の分類 |
|---|---|---|---|
| F1 | AC4 (a)(b)・S3（Supported の実 Scene で粒子が出る） | `VfxScenePlayTests.Supported_SeasonAndCellVfxScenes_EnableAndEmit`: Atmosphere の `VisualEffect` は有効になったが、`ParticleFrameLimit`（6000）内に `HasAnySystemAwake() \|\| aliveParticleCount > 0` が成り立たず `:173` で失敗。Cell VFX と bounds の検査（AC4 (b)・AC2 の R5）には到達していない | **スパイク**（原因が未確認。下記） |
| F2 | AC5・AC6 (a)(b)（I1・I4 の判定が成立しない） | `VfxScenePlayTests` の `Unsupported_…`・`Undetermined_…`・`ParentUnload_…` の 3 件: テスト本体の観測は出力済みで期待どおり（`enabled=0`、`consoleLogsDuringLoad=0`、unload 後の GameObject 0）だが、3 件とも TearDown で `NullReferenceException`（`EditModePcHelper.SetEnumeratorPC`）になり failed | **B 適応**（テストの後始末の実装。凍結条件の意味は変えない） |
| F3 | AC4 (c)・AC5（D1 の前提条件「判定時にロード済みの全 Scene で有効な `Renderer` が対象の `VFXRenderer` だけ」） | `VfxSceneRenderPlayTests.RenderFixture_…`: 前提条件で `otherEnabledRenderers` が `Assets/OneStarMaker/Scenes/UISystem/UIScene.unity:UIPanel` を 1 件含み失敗。計測（画素）には到達していない | **B 適応**（下記） |

**各指摘の詳細と B への指示**

- **F1（スパイク）**
  - 事実: 有効化は成立（`enabled` の assert は通過）。粒子の活動だけが出ない。`.vfx` は Simple Loop template から作り、各 System を Manual・Local にしただけ（§6）で、Editor で再生した観測は無い。
  - 未確認: (1) Edit Mode の Play・Game view が描画しない状況で `HasAnySystemAwake()` / `aliveParticleCount` が進まないだけか（d2 の `VfxPlayTestFixture` の前例はこの観測で粒子を確認している）。(2) 実 Scene の `VisualEffect` の位置・scale・Manual bounds が原因で、粒子が culling されるか。(3) template の spawn が Loop・Rate で始まっていないか（initial event、`PlacedVfxActivator` による有効化のタイミングで event が送られているか）。
  - 確認する範囲: B は、F3 を先に直して専用 Scene で同じ `.vfx` が画素を出すかを見る（Phase C の限定実行の対象。B は Editor のコンパイル確認まで）ことを前提に、(a) `.vfx` を Editor で開いて spawn の設定を確認する、(b) 粒子の観測を d2 の前例と同じ方法に揃える、の 2 点までを行う。
  - 終了条件: 原因が (i) 観測方法（テストの B 適応）、(ii) `.vfx` の authoring（`.vfx` の修正は B 適応。Manual bounds・配置の修正を含む）のどちらかに特定できれば B 適応として直す。(iii) AC の前提（例: Edit Mode の Play で実 Scene の粒子を観測できない）が成り立たないと分かった場合は、観測した事実を書いて Phase A 再開として人間に返す。**1 回の診断で特定できなければ止めて返す**（診断の反復はしない）。
- **F2（B 適応）**
  - 事実: この fixture だけが、基底の `VfxPlayTestFixture.ExitPlayModeAndRestore`（`ExitPlayMode`、ドメイン再読込を伴う）に加えて、派生に `[UnityTearDown] UnloadTestScenes` を持つ。TearDown の NRE は `VfxScenePlayTests` の 4 件の失敗のうち 3 件に出ている。`VfxSceneRenderPlayTests` は派生の TearDown を持たず、この NRE は出ていない。
  - 仮説（未確認）: `ExitPlayMode` をまたぐと 2 つ目の TearDown が再開できない。基底に後始末の仮想フックを足す（TearDown を 1 つにまとめる）か、test の director の後始末を各テストの末尾（`finally`）へ移す。
  - 直す際の注意: AC6 の観測語（unload 後に `go.scene` の一致する GameObject が 0）と上限フレーム数は変えない。unload のフレーム数が 0 だったのは `UnloadScene` の await が完了まで待つためで不自然ではないが、`CountObjectsIn == 0` の assert が実際に走っていることを直した後のログで確かめる。
- **F3（B 適応。条件の意味は変えない）**
  - 判断: 凍結条件は「判定時にロード済みの全 Scene で有効な `Renderer` が対象の `VFXRenderer` だけ」で、文言はそのまま満たせる。アプリ起動で載る UICommon の `UIPanel` を、テストが計測の間だけ無効にし、後で元に戻せば満たせる（前提条件の assert は変えず、assert の前に他の Renderer を無効にする）。条件の意味は変わらないので Phase A 再開にしない。
  - 直す際の注意: 無効にした Renderer の一覧（Scene・名前）をログに出し、`finally` で元の `enabled` に戻す。無効にできない Renderer（対象の VFX 以外が計測に入る別の経路）が残る場合は、事実を書いて止める（その場合は条件の意味に関わるので Phase A 再開の判断を人間に返す）。

- 後続スライスへ移送する指摘: なし。
- 未確認(第0巡の範囲外): AC7（Player）、全 EditMode 回帰、`Planner` / `Lighting` profile の実機確認は判定 C まで未実施。F1 が解消するまで AC4 (b) の Cell VFX の bounds 包含（R5）は未観測。
- 次の発見 C は、B の修正 head に対して §5 の起点 filter（上のコマンド）を `-WithGraphics` で実行し、F1〜F3 の解消と他のテストの回帰を確認する（全回帰・Player は判定 C まで回さない）。

### Phase B 差し戻し 1 巡目（1/4）

- 対象: base `09a203ab`（第0巡の記録 commit）→ implementation head `f264cd6e`。変更は 4 ファイル（2 つの `.vfx` の各 1 行、`VfxScenePlayTests.cs`、`VfxSceneRenderPlayTests.cs`）。§4 の変更対象の中で、Framework・asmdef・本番 Scene・`SceneResource`・Addressables の差分は無い。
- 実行したもの: Editor のコンパイル確認（errors 0。新しいメンバーが読み込まれたことを reflection で確認）、`contract-audit` errors 0 / warnings 0、`docs-audit` errors 0（警告 1 件は既存の §7/§8 の判定）。実行していないもの: Unity テスト（`run-tests.ps1`）。発見 C 第1巡で確かめる。Editor が `MobileDependencyResolver` の `.pdb.meta` 5 件を消したので戻した（§6 と同じ）。
- **F1（スパイク → B 適応 (ii) `.vfx` の authoring と特定）:**
  - 診断（1 回）: (a) Editor で 2 つの `.vfx` の設定を読み、d2 の fixture（`VfxImmortal.vfx`、`VfxShortLived.vfx`）と比べた。spawn・initial event（`OnPlay`）・system の構成は同じで、違いは culling・bounds の mode と空間・`reapParticles` だけだった。culling は d2 の fixture が `CullNone`（`m_CullingFlags: 0`、常に simulate）、新しい 2 つは template の既定 `CullDefault`（`3`、見えているときだけ bounds の計算と simulate）。(b) d2 の前例の観測（カメラ無しで `aliveParticleCount` を待つ）は `CullNone` の asset が前提だった。AC4 (a)(b) の実 Scene には VFX を見るカメラが無く（Atmosphere は (125, 0, 1135)）、`CullDefault` では system が起きない。§7 第0巡の「有効化は成り立つが粒子だけが出ない」と合う。
  - 修正: 2 つの `.vfx` の culling を `CullNone` にした（Editor の `VisualEffectResource.cullingFlags`。YAML は手で編集していない。他の設定は変えていない）。Cell の常駐中・季節中ずっと続く環境エフェクトなので、カメラが向いていない間も simulate を続け、カメラが向いたときに粒子が空から出直さない、という authoring の判断。観測の方法は d2 の前例のまま（テストは変えていない）。画面外でも simulate するコストは、program の同時数・culling の範囲（S-9、§2.4）の入力にする。
  - 未確認: これは静的な診断で、粒子が出ることは発見 C 第1巡の実行で確かめる。これで出なければ、§7 F1 の終了条件どおり、観測した事実を書いて Phase A 再開として人間に返す（B で再診断しない）。
- **F2（B 適応）:** `VfxScenePlayTests` の派生の `[UnityTearDown]` を消し、test の director の後始末を各テストの本体の直後（`WithTestScenesAsync` の `finally`）に移した。TearDown は基底の `ExitPlayModeAndRestore` の 1 つだけになる。観測語・期待値・上限フレーム数は変えていない。AC6 のログに unload 後の GameObject の件数（`cellVfxObjects`・`atmosphereObjects`）を足したので、`CountObjectsIn == 0` の assert が走ったことを発見 C がログで確かめられる。
- **F3（B 適応）:** `VfxSceneRenderPlayTests` は、計測の間だけ専用 Scene の外の有効な `Renderer`（UICommon の `UIPanel` 等）を無効にし（`disabledOutsideFixture=` で Scene・名前をログに出す）、`finally` で元に戻す。前提条件の assert（有効な `Renderer` が対象だけ）は変えていない。専用 Scene の中の Renderer は無効にしないので、fixture の欠陥は従来どおり前提条件で失敗する。無効にしても他の Renderer が残れば同じ assert で失敗し、§7 F3 の「止めて返す」に当たる。

### 発見 C 第1巡（差し戻し 1 巡目の修正の確認）

- 種別・対象: 発見 C 第1巡（GO 判定ではない）。implementation base `6667f88f` → head `f264cd6e`（差し戻し 1 巡目の差分は `09a203ab..f264cd6e` の 4 ファイル）。作業ツリーは HANDOFF だけが異なる子 commit `4d687048` で、未追跡の `DEBUGSTUDIO_S2_CUT_PLAN.md` 以外は clean だった。
- 担当・モデル: Claude Code（Anthropic、Claude Opus 系列）。C' 用の xAI 系列は未使用。
- 構造適合: 差し戻し 1 巡目の差分は §7 の記録どおり（2 つの `.vfx` の `m_CullingFlags` 3 → 0 の各 1 行、テスト 2 ファイル）。`unity/Assets/OneStarMaker/` と `*.asmdef` の差分は base から 0。`contract-audit` errors 0 / warnings 0。F2 の修正（`WithTestScenesAsync` の `finally` で後始末）と F3 の修正（専用 Scene の外の Renderer を計測の間だけ無効にし `finally` で戻す、`!= null` で偽 null を考慮）は前提条件の assert・観測語・上限フレーム数を変えていない。
- 実行したコマンド: §7 第0巡と同じ（`pwsh tools/run-tests.ps1 -WithGraphics -Filter "SampleGame.Tests.Rendering.Vfx;SampleGame.Tests.Editor.WorldAuthoring;SampleGame.Tests.App;SampleGame.Tests.Streaming;OneStarMaker.Tests.Editor.WorldAuthoring;OneStarMaker.Tests.SampleGame"`）。環境: Direct3D11、`supportsComputeShaders=True`、`maxComputeBufferInputsVertex=128`、Linear。DebugStudio のサーバーは起動していない（第0巡と同じ）。
- 結果: 170 件中 passed 169・failed 1・Inconclusive 0・Skipped 0。`SampleGame.Tests.Rendering.Vfx` の収録は 44 件。結果 XML・ログは git 管理外の `TestResults/` 配下（`evidence stage` はしていない）。
- **F1 解消:** `Supported_SeasonAndCellVfxScenes_EnableAndEmit` が passed。Atmosphere・Cell VFX とも `effects=1`、`activeFrame=2`（上限 6000）で `HasAnySystemAwake() || aliveParticleCount > 0` が成立（その時点の `alive=0`。AC4 (a) の条件は OR なので合格）。AC4 (b) の bounds 包含（R5）を初めて観測: `cellVfxBounds center=(1125, 20, 625) size=(70, 32, 70)`（寸法 0 でない）が `cellVolume center=(1125, 24.5, 625) size=(245, 49, 245)` に含まれる。
- **F2 解消:** `Unsupported_…`・`Undetermined_…`・`ParentUnload_…` が passed（TearDown の NRE なし）。AC5 の実 Scene は両 capability で `enabled=0`、`consoleLogsDuringLoad=0`。AC6 のログ `cellUnloadFrames=0 cellVfxObjects=0 atmosphereObjects=1`、`seasonUnloadFrames=0 atmosphereObjects=0` で、`CountObjectsIn == 0` の assert が走ったことを確認した。
- **F3 解消:** `VfxSceneRenderPlayTests` の 4 回の計測すべてで `disabledOutsideFixture=1 [Assets/OneStarMaker/Scenes/UISystem/UIScene.unity:UIPanel]`、`precondition otherEnabledRenderers=0`。陽性は Atmosphere・Cell とも `litPixels=4`（閾値 4、`alive=1`）、陰性は両方 `litPixels=0`（`frame=600`）。画素の判定はすべて期待どおりで、テストは下の F4 だけで落ちた。
- native 終了 stall: Unity の終了コードが 2。既知 native 終了 stall として扱い、調査はしていない。
- 作業ツリーの扱い: Editor が `MobileDependencyResolver` の `.pdb.meta` 5 件を削除したので `git checkout` で戻した（§6 と同じ）。

**阻害する指摘（凍結済み条件への違反）**

| ID | 違反する条件 | 一次証拠 | 新しい事実の分類 |
|---|---|---|---|
| F4 | AC4 (c)・AC5（`LogAssert.NoUnexpectedReceived`。判定 C の failed 0） | `VfxSceneRenderPlayTests.RenderFixture_SupportedDrawsVfx_UnsupportedNewInstanceDrawsNothing`: 本体末尾の `LogAssert.NoUnexpectedReceived()` が `[Warning] [DebugSocket] Failed to connect to DebugStudio server. endpoint=ws://127.0.0.1:5011/debugsocket/ …` で失敗。warning は陰性側の計測中（Atmosphere の 600 フレームの間）に出ている | **B 適応**（テストの起動条件。凍結条件の意味は変えない） |

- **F4 の詳細と B への指示**
  - 事実: アプリの起動（`AppInit`）は DebugSocket を `mode=Connect`・`autoStart=True` で始め、DebugStudio のサーバーが無いと `DebugSocketTransportHost.ConnectLoopAsync`（`:176`）が接続失敗の warning を出す（以後 2 秒ごとに再接続）。warning は Play の開始から数秒後に非同期で届き、unity.log では Play session ごとに 1 回出ている。`VfxPlayTestFixture` の起動待ち（`IsSceneLifecycleBound`）のコメントは「接続が見えた時点で起動のログは出終わっている」と仮定しているが、接続失敗の warning はこの仮定の外にある。本体が約 3 秒の他の Play テストは warning の前に終わって通り、本体が約 6.7 秒の描画テストだけが拾った。第0巡では描画テストが前提条件で先に落ちていたので表に出なかった。DebugStudio の有無とタイミングに依存するので、他の Play テスト（d2 の `VfxGraphBackendPlayTests`、AC5 の `consoleLogsDuringLoad`）も同じ原因で揺れうる。
  - 直す範囲: テスト側（`VfxPlayTestFixture` を含む）で、Play テストの本体に DebugSocket の接続のログが混ざらないようにする。方法は B が選ぶ（例: テストの Play では DebugSocket を開始しない構成にする、等）。AC5 の `LogAssert.NoUnexpectedReceived` と console ログ件数 0 の条件は弱めない（`LogAssert.ignoreFailingMessages` や、このテストだけ assert を外す・無条件の `LogAssert.Expect` で吸う形は条件の意味を変えるので不可）。Framework（`unity/Assets/OneStarMaker/`）の変更が必要になる場合は止めて返す。
  - 確認の観点（次の発見 C）: 同じ起点 filter で failed 0。DebugStudio を起動していない環境で、描画テストの本体が warning を拾わないこと。

- 後続スライスへ移送する指摘: なし。
- 未確認(第1巡の範囲外): AC7（Player）、全 EditMode 回帰、`Planner` / `Lighting` profile の実機確認は判定 C まで未実施。
- 次の発見 C は、B の修正 head に対して同じコマンドを `-WithGraphics` で実行し、F4 の解消と回帰を確認する（全回帰・Player は判定 C まで回さない）。

### Phase B 差し戻し 2 巡目（2/4）

- 対象: base `be1b9ad9`（第1巡の記録 commit）→ implementation head（この巡の commit。SHA は §0 の引継ぎ票の照合で使う）。実装の変更は `VfxPlayTestFixture.cs` の 1 ファイル。Framework・asmdef・本番 Scene・`SceneResource`・Addressables・`app-config.json` の差分は無い。
- 実行したもの: Editor のコンパイル確認（`scriptCompilationFailed=False`、新しい定数が読み込まれたことを reflection で確認）、`contract-audit` errors 0 / warnings 0、`docs-audit` errors 0。実行していないもの: Unity テスト（`run-tests.ps1`）。発見 C 第2巡で確かめる。Editor が `MobileDependencyResolver` の `.pdb.meta` 5 件を消したので戻した（§6 と同じ）。自分で起動した Editor は、コンパイル確認の後に `EditorApplication.Exit(0)` を呼んでも 7 分以上 process が残ったので、既知の native 終了 stall として調査せず、その PID だけを強制終了し、残った `unity/Temp/UnityLockfile` を消した（他の Unity process は無い）。
- **F4（B 適応。テストの起動条件）:** `VfxPlayTestFixture` の SetUp が、Play に入る前に環境変数 `SAMPLEGAME_DEBUGSOCKET__ENABLED=false` を設定し、TearDown の `finally` で元の値に戻す（`SAMPLEGAME_CONTENT__RUNTIMEMODE` と同じ扱い）。`AppConfig` は環境変数を config file より優先するので、この Play では `CreateDebugSocketService` が service を作らず、接続ループ（`ConnectLoopAsync`）とその warning が発生しない。無効のときに出る起動の warning 2 件（BeforeSceneLoad の `[AppInit] DebugSocket disabled.` と AfterSceneLoad の `[AppInit] DebugSocket は無効です。`）は、どちらも Scene lifecycle への接続（`BindSceneLifecycle`）より前に同期で出るので、SetUp の起動待ちの中で出終わり、本体には入らない。
  - 条件の扱い: `LogAssert.NoUnexpectedReceived`・AC5 の console ログ件数 0 は変えていない（`ignoreFailingMessages`・`LogAssert.Expect` は使っていない）。VFX の Play テストは DebugSocket を使わないので、観測の対象は変わらない。fixture を共有する d2 の `VfxGraphBackendPlayTests`・`VfxGraphRenderPlayTests` も同じ条件で起動するようになり、§7 F4 が指摘した DebugStudio の有無とタイミングへの依存がまとめて無くなる。
  - 未確認: 静的な確認だけ。描画テストの本体が warning を拾わないこと・failed 0 は発見 C 第2巡の実行で確かめる。

### 発見 C 第2巡（差し戻し 2 巡目の修正の確認）

- 種別・対象: 発見 C 第2巡（GO 判定ではない）。implementation base `6667f88f` → head `adf23ebd`（差し戻し 2 巡目の差分は `be1b9ad9..adf23ebd` の HANDOFF とテスト 1 ファイル）。作業ツリーは未追跡の `DEBUGSTUDIO_S2_CUT_PLAN.md` 以外は clean だった。
- 担当・モデル: Claude Code（Anthropic、Claude Opus 系列）。C' 用の xAI 系列は未使用。
- 構造適合: 差分は §7「Phase B 差し戻し 2 巡目」の記録どおり（`VfxPlayTestFixture.cs` だけ。SetUp で `SAMPLEGAME_DEBUGSOCKET__ENABLED=false` を設定し、TearDown の `finally` で元に戻す。`SAMPLEGAME_` は `AppInitializer.GetEnvironmentVariablePrefix` と一致）。`LogAssert` の assert・観測語・上限フレーム数は変えていない（`ignoreFailingMessages`・`LogAssert.Expect` の追加なし）。`unity/Assets/OneStarMaker/` と `*.asmdef` の差分は base から 0。`contract-audit` errors 0 / warnings 0。
- 実行したコマンド: §7 第0巡と同じ（`pwsh tools/run-tests.ps1 -WithGraphics -Filter "SampleGame.Tests.Rendering.Vfx;SampleGame.Tests.Editor.WorldAuthoring;SampleGame.Tests.App;SampleGame.Tests.Streaming;OneStarMaker.Tests.Editor.WorldAuthoring;OneStarMaker.Tests.SampleGame"`）。環境: Direct3D11、`supportsComputeShaders=True`、`maxComputeBufferInputsVertex=128`、Linear。DebugStudio は起動していない（process 無し）。
- 結果: 170 件中 passed 170・failed 0・Inconclusive 0・Skipped 0。`SampleGame.Tests.Rendering.Vfx` の収録は 44 件。Unity の終了コードは 0（今回は native 終了 stall なし）。結果 XML・ログは git 管理外の `TestResults/` 配下（`evidence stage` はしていない）。
- **F4 解消:** `RenderFixture_SupportedDrawsVfx_UnsupportedNewInstanceDrawsNothing` が passed。unity.log に `Failed to connect to DebugStudio server` は 0 件（第1巡は Play session ごとに 1 件）。計測は第1巡と同じ: 4 回とも `disabledOutsideFixture=1 [Assets/OneStarMaker/Scenes/UISystem/UIScene.unity:UIPanel]`、陽性 `litPixels=4`（閾値 4）、陰性 `litPixels=0`。
- 回帰: 第1巡で通ったものは全て passed。AC4 (b) `cellVfxBounds center=(1125, 20, 625) size=(70, 32, 70)` ⊂ `cellVolume center=(1125, 24.5, 625) size=(245, 49, 245)`、AC5 `consoleLogsDuringLoad=0`（2 件）、AC6 `cellUnloadFrames=0 cellVfxObjects=0 atmosphereObjects=1`・`seasonUnloadFrames=0 atmosphereObjects=0`。d2 の `VfxGraphBackendPlayTests`・`VfxGraphRenderPlayTests` も passed。
- 作業ツリーの扱い: Editor が `MobileDependencyResolver` の `.pdb.meta` 5 件を削除したので `git checkout` で戻した（§6 と同じ）。
- 阻害する指摘: なし。後続スライスへ移送する指摘: なし。
- 未確認（第2巡の範囲外）: 判定必須テスト（空 filter の全 EditMode 回帰）、AC7（Player）、`Planner` / `Lighting` profile の実機確認。判定 C で行う。
- 次: 未解決の blocker が無いので、`adf23ebd` を GO 候補 head として判定 C へ進む。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
