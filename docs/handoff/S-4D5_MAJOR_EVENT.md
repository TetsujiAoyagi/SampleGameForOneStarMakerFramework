# S-4d5a major Event

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `B` 完了、発見 C 1 巡目の差し戻し待ち（A3 凍結版は `3104ff5a`、2026-10-29。A2 入力版は `1fa1b472`。採否の要約は §5.1）
- branch: `claude/s-4d5-major-event`
- implementation base commit: `22ebf130`（`develop`、S-4d4b の PR #148 のマージ）
- implementation head commit: `1766af84`（Phase B の出口。GO 候補 head としては未固定）
- risk: `high`（Season の排他 controller に新しい所有物を足す。親の unload と子の Add の競合、Addressables entry の追加、`InGameSession` への internal method の追加に触れる（公開 interface は足さない）。Framework のコードと公開 API は変えない見込み）
- owner: 発注者 / S-4d5 担当
- created: 2026-10-29
- expires: S-4d5a の Phase D 完了時
- harvest to: Architecture §05 §5.13（Season 直下の major Event の分類と寿命）、§24 §16（`Scene(id)` 所有の動的 VFX の実例。§16.7 の更新）。S-4 program 全体の harvest と削除は d5b の範囲（§2.6 U1）
- workflow task: `s-4d5-major-event`（未開始。最初に Evidence を固定する時点で `pwsh tools/workflow-task.ps1 start` する）
- Phase A snapshot の取得案内・状態: 未生成（凍結した HANDOFF 本体は A3 凍結 commit で固定する）
- Phase B result snapshot の取得案内・状態: 未生成（B の結果は §6 と commit `1766af84` で引き渡す）
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票: 発見 C（1 巡目）完了。対象 head は implementation head `1766af84`（HEAD は記録のみの `0708e361`）。阻害する指摘 2 件（テストの観測の取り方 F1・F2）と、凍結条件の読みの確定が要る 1 件（F3、AC9 と AC3 の食い違い）。次は B（F1・F2 の観測の修正）と、F3 の人間の決定記録。製品コードの欠陥は見つからず、Play の根（§5 未知の操作経路 (1)）は成立した。詳細は §7。判定 C・C' は未着手。

---

## 1. 目的と対象外

- **目的:** Season 直下の独立 Scene `Spring_Event_MultiCellProof` を、Cell bundle から独立して Add / Unload し、完了または Season の unload で確実に回収されることを、実 Scene で示す。d1 から送られてきた `Scene(id)` 所有の動的 VFX の実例（所有 Scene の unload での回収、source asset との前後関係）をここで閉じる。
- **対象外:**
  - Content Directory の Whitebox を使う Editor Play での Planner 実証、S-4 program の harvest と削除（§2.6 U1 の分割案では S-4d5b）。
  - 非 Scene asset（`.vfx`）を Content Directory・Player build に乗せる経路、Editor Play 向けの publish 入口（BuildSystem の実装系。§4.3）。
  - S-5 の Tunnel 遷移、季節の切替の UI や gameplay。`RequestSeasonAsync` の本番の呼び出し元は増やさない。
  - 同時数の上限・優先度・間引き・pool / prewarm・DebugStudio での観測（S-4 program §6.4）。
  - §24 §16.7 の所有スライス未定の事項（進入者の追跡の厳密化、`LoadAssetAsync` の await 後の所有者確認の欠落、`IncrementalAlways` の保留 unload、companion 作成 transaction の untitled Scene）。
  - Framework の変更。
- **現況:** §1.3（A0、`22ebf130` で確認）。

### 1.1 Phase A の判断基準（発注者、2026-10-27。S-4 program §0 から転記）

A1 の各案と A3 の裁定の材料に明記する。

- **主目的は BuildSystem。** いまの主目的は、きちんとした BuildSystem を作ることで、VFX（S-4d）はそのつなぎ込みの題材である。各案で、スライスの成果物が content build・Player build の経路に乗るかと、Build 側の既知の課題（BS4 の Player は smoke の後に終了して InGame の Spring に入らない、transport staging の path が予算の外）を踏むかを書く。Build 側の課題は「やらない」で片付けず、BuildSystem の作業の候補として扱う。
- **残すコードは SampleGame に必要なものだけ。** 生成や authoring の道具を残すかは「SampleGame にそのコードが必要か（無いと SampleGame の検証が成り立たないか）」で決める。必要でなければ、一時スクリプトで作り commit の前に消す案を既定にする。「他の季節・Cell でも使える」「再利用できる」は残す理由にしない。
- **checkout のルート。** Player を build するスライスは、ルートが 96 文字以下の checkout で作業する。この worktree のルートは 29 文字（末尾の区切りを含む）。d5a は Player を build しない（§4.3）。

### 1.2 S-4 program から転記した制作契約

**SceneGraph（program §2.1）。**

```text
InGameSession
├─ Tunnel                                  S-5 / NecessaryAlways
├─ Season_Spring                           logical / OnDemand
│  ├─ Spring_Lighting                      NecessaryAlways
│  ├─ Spring_AtmosphereVFX                 NecessaryAlways / optional
│  ├─ Spring_Cell_{x}_{y}                  OnDemand / StreamByDistance
│  │  ├─ Spring_Environment_{x}_{y}        OnDemand / resident companion
│  │  ├─ Spring_Lighting_{x}_{y}           OnDemand / optional companion
│  │  ├─ Spring_VFX_{x}_{y}                OnDemand / optional companion
│  │  └─ Spring_Events_{x}_{y}             OnDemand / optional companion
│  └─ Spring_Event_{slug}                  OnDemand / major event
├─ Season_Summer                           同型
...
```

**職種ごとの所有物（§2.2 から抜粋）。** Cell Events は trigger、spawn、encounter anchor、局所的な進行制御。**Major Event は、複数 Cell に跨る、起点 Cell より長寿命、または Timeline / Cinemachine を独立管理するイベント。** VFX Scene にゲーム進行 trigger を置かない。Events Scene が VFX の開始・停止を要求する。Scene を跨ぐ `GameObject` の直接参照は禁止し、event id、world position、service query、`IAssetManagement` で寿命を宣言してロードする prefab で接続する。

**Event Scene の粒度（§2.3）。** 次のいずれにも該当しないイベントは `*_Events_{x}_{y}` に含める。複数 Cell に効果が及ぶ／起点 Cell の Unload 後も継続する必要がある／専用 Timeline / Cinemachine staging を独立ファイルとして所有したい／別担当が Cell bundle と独立して変更する規模。いずれかに該当するときだけ独立 Scene にする。program §2.3 は名前を `Season_Event_{slug}` と書き、§2.1・§2.5・§6.2 は `Spring_Event_{slug}` と書く。本スライスは `{Season}_Event_{slug}`（例 `Spring_Event_MultiCellProof`）と読み、§2.3 の表記を凍結 commit で直す（D1）。

**VFX の粒度と寿命（§2.4）。** 一時 VFX は `IAssetManagement` でロードし、`AssetOwner.Scene(eventSceneIdentity)` または `AssetOwner.Bind(go)` で寿命を宣言する。

**物理フォルダ（§2.5）。** `Seasons/Spring/Events/Spring_Event_MultiCellProof/Spring_Event_MultiCellProof.unity`。

**実証 Scene（§6.2）。** `Spring_Event_MultiCellProof` は Cell bundle とは独立して Add / Unload でき、Season Unload で必ず回収される。Planner Play は `content:representation=Whitebox` と `world:cellCompanionSet=Planner` を使い、Environment をロードしない状態の local / major event 両方を実証する。S-4 完了時の Scene 実体は 658 件。

**d5 の行（§6.3）。** 答える問い: Cell bundle から独立した Event Scene を Add / Unload し、完了または Season unload で回収できるか。Planner profile で Environment を読まずに Event が動くか。範囲: `Spring_Event_MultiCellProof`、その寿命の所有者（S-4b の Season 排他 controller との関係を含め d5 の Phase A で決める）、§6.2 の Planner 実証、本書の harvest と削除。Phase A で major Event と Planner 実証・harvest の見積もりを分け、大きければ追加分割を判断する。

**公開 API と依存（§10）。** Framework は season 名、Cell 座標文法、職種 role を知らない。Game → Framework の依存方向を維持する。`SceneState` の 14 値と `LoadType` は変更しない。VFX Graph 依存は SampleGame 側に閉じる。

**検証方針（§11 S-4d）。** local Event は Cell unload、major Event は完了または Season unload で回収される（d5）。Planner profile で Environment をロードせず Whitebox 上の Event が動く（d5）。`unity test` / `unity run`、Scene / asset YAML の手編集は禁止。Phase B は Editor 操作・compile 確認まで、Unity テストと Build は Phase C。

**VFX の契約（Architecture §24 §16 の要約）。** `Scene(id)` 所有の instance は `SceneDirector.OnSceneEvent` の `PreUnloading` / `LoadCanceled` で、Unity の Scene unload と asset の解放（`ReleaseScene`）より前に壊す。`Spawn` は呼び出し側がロード済みの source を受け取り、Framework は asset を保持しない。source は instance が回収されるまで有効に保つ（同じ `AssetOwner` でも解放順は保証されない。asset を VFX より長く生かす）。全 VFX は gameplay layer の時間に追従する（d4a）。

### 1.3 A0 の現況（`22ebf130`）

- **Season の排他 controller。** `SessionSeasonController`（internal、`SampleGame/InGame/InGameSession/Streaming/Runtime/Season/`、599 行）。所有者は `InGameSession`（`OnLoadedImpl` で生成、`OnPreUnLoadedImpl` で drain せず `Dispose`。残りは Framework の再帰 unload が回収）。枝の操作は `_branchBusy` で 1 件に直列化し、`TearDownActiveBranchAsync` は driver を破棄 → Season を unload → `_registry.WaitAllIncomplete` → `_tracker.WaitAllLive`（観測個体の終端）を待ってから次の Season を Add する。`NecessaryAlways` の子（`Spring_Lighting`・`Spring_AtmosphereVFX`）は Stable 後に観測個体として登録する。Add / Unload は registry が追跡する private の `IssuedSceneController` を通る（今は Ensure と companion driver だけが使う）。**Season の非 Cell の OnDemand 子を Add する口は無い。** `RequestSeasonAsync` の本番の呼び出し元は初期 Ensure だけ。
- **SceneDirector の性質。** `AddScene` は未ロードの祖先を `isLoadChildren` で載せる（Season が未ロードなら Event の Add が Season と Lighting・Atmosphere を controller を通さずに載せる）。親が既にロード済みなら、子の個体は `AddScene` の最初の await（`ExecutePreLoad`）より前に辞書へ入る（A2 (2) の読み）。親の unload は `CollectLoadedDescendants` の後順で子を PreUnload → Unity unload → AfterUnload の 3 段で回収し、ロード中の子の完了は待たない。ロード中の子の扱いは位相で分かれる: キャンセル窓（`PreLoading`、`LoadCts` が生きている区間）は `LoadCts.Cancel()` → `CleanupCanceledScene`（終端は `CancelCleanedUp`）。窓の外（`OnLoadedImpl` を含む `WaitLoadChildScene` まで）は `_pendingUnloads` に入り、公開 `AddScene` の対象の経路で Stable の後に消費される（終端は `Removed`）。`Initializing` は `IsActive` で、`UnloadScene` は cancel も保留もせずそのまま `RemoveScene` に入る。**キャンセル窓で cancel された `AddScene` は、呼び出し元の token がキャンセルされていなければ例外にせず戻る。** **親が unload 中に子を Add した場合**、子の待ちは子の identity だけを見て、`LoadSceneBase(parent)` は親が `None` 以外なら早期に戻るので、子孫の収集後に Add された子は取り残されうる（A2 (2) も同じ機構を確認）。
- **発行済み操作の追跡の順序。** controller の `IssuedSceneController.AddScene` は op を登録してから `await _inner.AddScene` し、**戻った後に** `TryCaptureAndBind` で個体を観測個体に登録する。`SceneTerminalTracker` は未登録の identity の終端を捨て、`GetLoadedScene` は unload 開始済み・`LoadCanceled` の個体を null にする。したがって、Add の await 中に個体が終端すると bind されず、成功側では `CompleteOp` もされないので、その op は `WaitAllIncomplete` を永久に塞ぐ（A2 (2)。既存の companion driver の Add にも同じ順序があるが、本スライスでは変えない。§2.4）。
- **分類。** `GameSceneFactory` は `StreamByDistance` → `DemoCellScene`、親が `StreamByDistance` → `CellCompanionScene`、`{Season}_AtmosphereVFX`（親 `Season_{Season}`）→ `SeasonAtmosphereVfxScene`、identity の完全一致、それ以外は null（`SceneDirector` が "SceneFactory returned null" で失敗）。`SeasonCandidateSelection` は `StreamByDistance` でない子を距離候補に入れない。**"Spring_Event_" / "MultiCellProof" を参照するコード・asset は無い。**
- **local Event（d4b）。** `CellCompanionScene`（81 行）が `OnInitialize` で `PlacedVfxAdopter` と `LocalVfxEventBinder` を作り、`OnLoadedImpl` で `.vfx` を `Scene(Events)` 所有でロード、`OnPreUnLoadedImpl` で `EndAll()`。trigger（`LocalVfxEventTrigger`）は出来事を Scene class の session へ流すだけ。
- **Scene class から session の service への経路。** `PlayerScene`・`InGameUI`・`ResultScene` は直親 1 段を `SceneQuery.GetLoadedScene(parent.Identity) as IInGameSessionServices` で解決し、それぞれ手書きしている（兄弟間のやり取りは親の `IInGameSessionServices` 経由のみ）。祖先を複数段たどる API は無い。Cell の companion（深さ 3）と Season の子（深さ 2）は `InGameSession` の子孫で、グラフとしては祖先をたどれば届く。`IInGameSessionServices` は Flight・Focus・Streaming の観測・WorldReady・停止・登録を既に持つ。
- **Planner。** `world:cellCompanionSet` は `AppInitializer` が読み、`GameSceneFactory` → `InGameSession` → `SessionCellCompanionLoadDriver` に渡る。Planner は Cell の Events だけを載せる。Season の子（Lighting・Atmosphere・Event）は companion set の対象外。`content:representation` は Content Directory のモードで読む。Editor Play は `ContentDeliveryPlayBridge` が verified install から環境変数を設定するが、companion set は設定せず、Editor Play 向けの publish 入口は未整備（program §3.1）。**Planner + Whitebox の全体の Play テストは無い。** `LocalVfxEventPlayTests` は test の `SceneDirector` と複製 Map（Season_Spring・Cell_4_2・Events_4_2）に `CellCompanionSet.Planner` を渡して載せる。
- **World Workspace。** 作成 transaction の kind は `CellCompanion=0`・`SeasonAtmosphere=1`（journal に int で永続化、追加のみ）。Season の Event の kind は無い。
- **Content build。** Season の子は親をたどって Season に属すると判定され、Full のみの payload は Whitebox build と共有される（Lighting・Atmosphere と同じ）。node・payload・Addressables の候補が揃わないと "Payload/candidate mismatch" で失敗する。`.vfx` は Scene の依存でないので入らない（§16.7）。
- **件数。** `Seasons/` 配下の `.unity` は 657。`VfxSceneStructureTests.SeasonSpring_ChildrenAreLightingAtmosphereAnd54Cells` が Season_Spring の子 56 件を確かめている（d5 で 57 に変わる）。

---

## 2. 意思決定と受け入れ境界（A3 凍結版）

- **このスライスが答える問い:** Season 直下の独立 Event Scene を、Season の排他 controller の管理下で Cell bundle と独立に Add し、完了または Season の unload で、その Scene と Scene 所有の動的 VFX を確実に回収できるか。

**Event の位相の語（全条件で共通）。** 個体の位相は SceneDirector の状態で、キャンセル窓（`PreLoading`）、窓の外のロード（`OnLoadedImpl` を含む `WaitLoadChildScene` まで）、`Initializing`、Stable、終了処理（unload の要求から終端まで）の 5 つ。終端は `Removed` または `CancelCleanedUp`。driver の状態は Idle → Loading（受理）→ Live（Stable を観測）→ Ending（完了・枝の終了）→ Idle（終端を観測）。

### 2.1 不変条件（起きてはいけないこと）

- **I1（親の外で生きない）:** major Event の Add は、active な Season が Stable で枝の操作中でない間にだけ発行される。受理の判定、状態の記録、`AddScene` の開始は 1 つの同期区間で行い、その間に await を挟まない。Season が未ロード・unload 中・枝の切替中・Session の停止後の要求は Add を発行しない（祖先の暗黙のロードも、unload 中の親への取り残しも起きない）。
  - **保証の範囲:** Season を unload する全経路。Season を直接の対象にする unload は controller の枝の終了だけで、driver の停止と `_branchBusy` が先に立つ。Session・`InGame` など祖先の unload では、SceneDirector が子孫の収集と同じ同期区間で Season の `PhasePreUnload` に入り、Season は UIView を持たないので `PreUnloading` へ同期に進む（`IsUnloadStarted` が真、`GetLoadedScene` が null）。よって収集の後に届く要求は Stable の判定で `Rejected` になる（A3 版レビュー R1）。
  - **この証明の位置づけ:** 「Season の Scene class は UIView を登録しない」は現コードの観測で、本スライスの設計契約にしない（Season の UI の有無を major Event の契約にしない）。保証は現在の構成と指定した呼び出し経路に依存する。AC9 の続きは後続のフレームでの受付の停止を確かめるだけで、同期区間の窓が無いことは §1.3 のコードの順序で扱う。崩れた場合の口は §2.4 の後続。要求は物理の callback や frame の処理から届き、`OnSceneEvent` の同期の購読者から要求しない（post-order の収集の途中で同期に呼ばれると、Season の `PreUnloading` より前になる）。
- **I2（重複しない）:** 1 つの Season 枝に同時に生きる major Event は最大 1 件。driver が Idle でない間、同じ identity の要求は `AlreadyActive`（Loading・Live）または `Rejected`（Ending）、別の identity の要求は `Rejected` で、どれも 2 つ目の Add を発行しない。同じフレームの複数の進入も含む。複数同時の辞書・pool・queue は作らない（A3 凍結前レビュー R3-1）。
- **I3（Season より長く残らない）:**
  - (a) 枝の切替は、Event がどの位相にあっても、Event の終端を観測してから次の Season を Add する。待ちが Event の op で永久に塞がれない。
  - (b) Session の unload（drain しない）では、Event は自分の終端まで進んで消え、その Scene 所有の VFX instance と source は残らない。ロード中だった Event は自分のロードの完了の後に終端する（Season の `Removed` より後になりうる）。unload の開始後の `Spawn` の回数は保証しない（A3 版レビュー R2。gameplay の要件ではなく、回収が問い）。
  - (c) Season の `Removed` の次のフレームに、Stable だった Event の個体も、その Scene 所有の VFX instance も残らない。
- **I4（回収の順序）:** Event の Scene 所有の VFX は、source asset の解放（`ReleaseScene`）より前に壊される。Event の `Removed` の次のフレームに、その Event の `VisualEffect` は 0 件。
- **I5（既存を変えない）:** Season の切替、距離 streaming、companion driver、local Event（d4b）、配置済み VFX（d4a）、`Spring_Lighting` などの既存の Season の子の分類の振る舞いと既存テストの結果を変えない（`VfxSceneStructureTests` の子の件数 56 → 57 の更新を除く）。Framework のコード・公開 API、`SceneState`、`LoadType` を変えない。

### 2.2 成立条件（起きなければならないこと）

- **E1（起動）:** active な Season が Stable の間の要求で、Event Scene が Add され Stable になり、Cell (4,2) と (5,2) の体積の中に 1 つずつ置いた anchor の位置で VFX が起動する。
- **E2（完了）:** Event Scene の完了の trigger で、controller が Event Scene を unload し、VFX が回収される。完了後の再要求で再び起動できる。
- **E3（Season の unload）:** Event の生存中の Season の unload で、Event Scene と VFX が回収される。
- **E4（Cell からの独立）:** Event の生存中に起点の Cell (4,2) とその Events を unload しても、Event Scene と VFX は残る。
- **E5（Planner の companion set）:** `CellCompanionSet.Planner` の構成で Environment を載せずに、Cell Events の開始 trigger から major Event が起動し、完了する。Content Directory の Whitebox を使う Planner Play は含まない（U1 で d5b）。
- **E6（authoring と content build）:** `Spring_Event_MultiCellProof` が Season_Spring の子（OnDemand、`StreamByDistance` false、payload は Full のみ）として存在し、`GameSceneFactory` が major Event の class に分類し、Addressables entry を持ち、`Build Spring Whitebox` の Content Directory に Scene payload として入る。Season_Spring の子は 57 件、`Seasons/` 配下の `.unity` は 658 件。

### 2.3 受け入れ条件（検証する条件を併記）

単体 = 偽 scene controller / query / terminal events の純 C# テスト。Play = test の `SceneDirector` と実 Scene の Play Mode テスト。EditMode = authoring の検査。**Play は 3 本（AC6、AC7・AC8、AC10）と、祖先の unload の 1 本（AC9）に限る。** 位相の網羅は単体（AC1〜AC5）で見て、Play の本数を増やさない。

| AC | 内容 | 条件 | 種別 |
|---|---|---|---|
| AC1 | active Season が Stable の間の要求 1 回で、Add が 1 回発行され、`Accepted` を返す。返る前に driver は Loading で、個体は観測個体に登録済み | E1・I1 | 単体 |
| AC2 | 受理（driver の static な純関数）: Season 未ロード、捕捉した Season の個体と現在の個体が別（`ReferenceEquals` が false）、Stable でない、枝の切替中、停止後、identity が active Season の直下でない・OnDemand でない・`StreamByDistance` である（別 Season の Event、Cell、Lighting 等）のそれぞれで `Rejected`（理由付き）。driver は Add を発行せず Warning を 1 回出す | I1 | 単体（失敗経路） |
| AC3 | Loading・Live の要求は `AlreadyActive`、Ending の要求は `Rejected`。どちらも Add 0。終端（`Removed` と `CancelCleanedUp` のどちらでも）を観測した後の要求は Add 1。同じ同期区間の 2 回の要求で Add 1。完了の通知が Add の await が戻る前（Loading）に届いても Ending になり、unload が 1 回発行される。偽 Event A が Loading・Live の間の別 identity の偽 Event B の要求は `Rejected`、Add 0。A の終端の後の B の要求は Add 1 | I2・E2 | 単体（失敗経路） |
| AC4 | Event が Live の間の枝の切替は、Event の終端を待ってから次の Season を Add する（偽 terminal events で Event の終端を遅らせると次の Add が保留される） | I3(a) | 単体 |
| AC5 | Event の Add の await 中に枝の切替を始める: (i) キャンセル窓で `CancelCleanedUp`、(ii) 窓の外（`OnLoadedImpl` の await 中）で保留の後の `Removed`、(iii) `Initializing`。いずれも次の Season の Add は Event の終端の後に発行され、registry の未完了 op は 0、Event の個体は 0。(iv) 発行の失敗: 偽 controller で await より前に個体を捕捉できない場合も、元の Add の task が終わる（完了・例外）までは driver が Idle に戻らず、その間の要求で 2 本目の Add を出さない。例外は driver が観測して Error を 1 回出し、未観測の例外を残さない | I3(a) | 単体（失敗経路） |
| AC6 | 実 Scene: 開始 → Stable → anchor 2 か所に VFX 2 件。Cell (4,2) と Events を unload し、Cell の `Removed` の次のフレームで Event は Stable、VFX 2 件が `IsActive` | E1・E4 | Play |
| AC7 | 実 Scene: 完了の trigger に kinematic Rigidbody を入れると Event が回収される。(a) 記録 backend: Event の `PreUnloading` → instance の `Destroy` 2 回 → `ReleaseScene`（`IAssetDiagnostics.GetOwners` から Event の所有者が消える）→ `Removed` の順。(b) 実 backend: 起動で Event が起動した `VisualEffect` 2 件、Event の `Removed` の次のフレームで**その 2 件**が 0 件、再要求で再び 2 件。数えるのは Event Scene の個体が起動したものだけで（起動の前後の差か、Event の所有で識別する）、`Spring_AtmosphereVFX` など既存の `VisualEffect` は数えない。(c) 非対応の `VfxSystem` でも開始・完了が例外なく進み、`VisualEffect` は有効にならない（旧 AC11 を統合）。計測のために製品コードや共通のテスト基盤を足さない（`LocalVfxEventPlayTests` と同じ分け方） | E2・I4・I5 | Play（AC8 と同じ test class） |
| AC8 | 実 Scene: Event が Stable の間に枝を終了して Season を unload すると、(a) 記録 backend: Event の `PreUnloading` → instance の `Destroy` 2 回 → `ReleaseScene` → Event の `Removed` → Season の `Removed` の順。(b) 実 backend: Season の `Removed` の次のフレームで Event の個体なし・Event が起動した `VisualEffect` 0 件（AC7 (b) と同じ数え方。Season 全体の終了後の総数と混同しない） | E3・I3(c)・I4 | Play（AC7 と同じ test class） |
| AC9 | 実 Scene で 1 回だけ: Event の窓の外のロード中に Session を unload する（本番の退出と同じく controller の待ちを通さない）。Event は保留の後に終端し、その `Removed` の次のフレームに Event の個体なし・Event が起動した `VisualEffect` 0 件（AC7 (b) と同じ数え方）・`GetOwners` に Event の所有者なし。`Spawn` の回数は判定しない。同じテストの続きで、Session の unload の開始の次のフレームに 1 回要求すると `Rejected`、Add 0。これは後続のフレームでの受付の停止を確かめるもので、子孫の収集の直後の同期区間に窓が無いことの直接の証明ではない（そちらは I1 のコードの順序の確認で扱う。A3 凍結前レビュー R3-5）。位相ごとの Play の行列にしない | I1・I3(b)・I4 | Play（失敗経路、1 本） |
| AC10 | 実 Scene: `CellCompanionSet.Planner` で Season・Cell (4,2) を載せ、Environment が 1 件も載っていない状態で、Events の開始 trigger から Event が起動し、完了の trigger で回収される。Events_4_2 は既存の companion driver が resident の Cell (4,2) から載せたもので、テストが直接 Add しない（誰が載せたかを記録する）。Whitebox の representation は含まない | E5 | Play |
| AC11 | 欠番（第 3 版で AC7 (c) に統合） | — | — |
| AC12 | authoring: `Spring_Event_MultiCellProof` の node・payload（Full のみ）・親子 link・Map・Addressables entry（Scene と同じ group、address は asset path）。`.vfx` は Scene と同じフォルダで、Update Mode は §24 §16.6 の規約（`PlacedVfxAuthoringTests` と同じ検査）。Event Scene に Renderer・`VisualEffect`・`PlacedVfxActivator` が無い。anchor は Cell (4,2) と (5,2) の `_volume` の中に 1 つずつ、完了の trigger は (5,2) の体積の中、開始の trigger は `Spring_Events_4_2` の (4,2) の体積の中にあり、どちらも `isTrigger` の Collider を持つ。開始の trigger の event identity は Season_Spring の子に実在し、`GameSceneFactory` が `SeasonMajorEventScene` に分類する。Season_Spring の子 57 件 | E6 | EditMode |
| AC13 | `GameSceneFactory` の分類: `Spring_Event_MultiCellProof` は `SeasonMajorEventScene`、`Spring_Lighting` は `SeasonLightingScene`、`Spring_AtmosphereVFX` は `SeasonAtmosphereVfxScene` のまま。名前の規則に合わない Season の子（例 `Spring_Foo`）、親が別 Season の `Summer_Event_X`、名前と親が合っても `LoadType` が `OnDemand` でない Event（誤った authoring）は major Event にしない | I5・E6 | EditMode |
| AC14 | Phase C で `Build Spring Whitebox` を 1 回実行し、成功し、Content Directory に `Spring_Event_MultiCellProof` の Scene payload が入る（Full の共有。`.vfx` は入らない） | E6 | Phase C の content build |
| AC15 | 既存の Season の切替・companion driver・local Event・配置済み VFX のテストが通る（最終の全 EditMode 回帰と、Streaming・Vfx の PlayMode） | I5 | 回帰 |

AC3 の読み方: 合格例: Stable で完了した直後の要求は `Rejected`、Event の `Removed` の後の要求は Add 1。Loading 中に `CancelCleanedUp` で終端した後の要求は Add 1。不合格例: 完了の直後の要求で Add が発行され、unload 中の個体と新しい個体が同時に生きる。

AC5 の読み方: 合格例: (i)〜(iii) のどれでも、偽 controller に記録された次の Season の Add が、Event の終端の通知より後。不合格例: Event の終端の前に次の Season を Add する、または Event の op が未完了のまま切替が終わらない。

AC6〜AC9 の「次のフレーム」は、対象の `Removed` の通知を受けたフレームの次の `yield return null` の後。

### 2.4 Architecture §16.7 の事項と既存の欠落の扱い

| 事項 | 扱い |
|---|---|
| `Scene(id)` 所有の動的 VFX の実 Scene での回収（d1 の寿命管理の対象として d5 に残っていた） | 本スライスで確かめる（AC7・AC8） |
| notifier 単独の `OnDestroy`・`Bind` の 1 フレーム回収 | 変更なし（major Event は `Bind` を使わない） |
| `LoadAssetAsync` の await 後の所有者確認の欠落 | 変更なし。major Event も `OnLoadedImpl` でロードして避ける（d4b と同じ） |
| `.vfx` の Content Directory・Player の経路 | 変更なし。BuildSystem の候補（§4.3） |
| 本番の SceneGraph での Season と Cell の同時ロード（S-9） | 変更なし |
| 既存の `IssuedSceneController.AddScene` が await の後に bind する順序（§1.3。companion driver の Add が枝の切替の待ちを塞ぎうる） | 本スライスは major Event の Add だけに発行時の bind を使い、既存の経路は変えない。companion driver の経路の確認と修正は後続の入力として §16.7 に送る（所有スライス未定） |
| Season が UIView を持つようになっても祖先の unload の開始を知る口、major Event の仕組みの Framework への移設と Season 以外の親への一般化 | 本スライスは作らない。必要になった時点の後続の入力として §16.7 へ送る |
| 親の unload の開始後に子を Add すると取り残される（Framework） | Framework は変えない。major Event は I1 の同期の受理と発行で、この窓に Add を出さない。Framework の修正は後続の入力として §16.7 に送る |

### 2.5 A3 の設計

- **D0（置き場所と粒度。第 3 版）:** 製品コードは d4b の session / trigger / binder / Scene class と同じ役割の 4 つに収める。driver と受理は Season の枝の所有物として `Streaming/Runtime/Season/` の internal に置き、Season と呼んでよい。アプリを前提にしない port・層・公開の要求 interface は作らない（発注者の指示「前提にしないと膨らむなら後続へ送る」に従い、前提にしない形は §2.4 の後続へ送る）。
- **D1（名前と配置）:** identity は `{Season}_Event_{slug}`、本スライスは `Spring_Event_MultiCellProof`。Season_Spring の子、`LoadType.OnDemand`、`StreamByDistance` false、payload は Full のみ（Whitebox build と共有）。フォルダは program §2.5 の `Seasons/Spring/Events/Spring_Event_MultiCellProof/`。`.vfx` は同じフォルダ。名前の規則は `SeasonCellNames` に `MajorEvent(season, slug)` と `TryParseMajorEvent` で足す（変更理由は同じ「命名規則」）。`GameSceneFactory` の分類は「名前が `{Season}_Event_{slug}`（season は `_` を含まない、slug は空でない）**かつ** 親が `Season_{同じ season}` で `StreamByDistance` でない **かつ** `LoadType.OnDemand`」とし（受理と同じ構造の条件。`NecessaryAlways` の誤設定は分類されず、ロード時に factory の null で失敗として表に出る。A3 版レビュー R3）、Atmosphere の分岐の後・identity の完全一致の前に置く。名前の条件を欠いた構造だけの判定にはしない（`Spring_Lighting` を飲み込む。AC13）。program §2.3 の `Season_Event_{slug}` を凍結 commit で直す。
- **D2（driver と発行）:** 新しい internal の `MajorEventDriver`（`Season/MajorEventDriver.cs`、純 C#）を枝ごとに作り（`CreateDrivers` で作る）、枝の終了で停止する。
  - **受理:** driver の中の純関数（static）で、入力は要求 identity、捕捉した active Season の個体と現在の個体、Stable か、枝の操作中か、停止後か、driver の状態。結果は `Accepted` / `AlreadyActive` / `Rejected(reason)`（同じファイルの internal enum）。理由の集合は AC2 で凍結し、実装中に増やすなら Phase A へ戻す。構造の判定（親・`LoadType`・`StreamByDistance`）は `SceneResource` で見て、名前の規則は使わない。
  - **発行:** 受理・状態の記録（Loading）・`AddScene` の開始を 1 つの同期区間で行う（I1・I2）。発行の手続きは controller の private method に置く: 既存の registry へ op を登録 → 内側の `AddScene` を開始（最初の await まで同期に進み、SceneDirector が個体を辞書に入れる）→ **await より前に** `TryCaptureAndBind` → await。driver は controller から生成時に渡される delegate（発行して捕捉した個体を返す、unload する）でこれを呼ぶ。interface や公開の decorator 型は作らない。major Event だけが使い、既存の `IssuedSceneController.AddScene` は変えない（§2.4）。await より前に個体を捕捉できない場合も「発行は無かった」とはしない: driver は Ending にとどまり、元の Add の task の終わり（完了・例外）を必ず観測してから Idle に戻す。例外は Error を 1 回出して閉じる（再試行も 2 本目の Add もしない。A3 凍結前レビュー R3-2）。factory が分類しない Season の子への要求（d5a は構造が正しい実証 Scene 1 件だけを前提にする）も、Add の失敗としてこの経路で閉じる。
  - **完了の観測:** driver は捕捉した個体に完了の delegate を渡す（`SeasonMajorEventScene` の internal method 1 つ。controller が生成時に「個体へ delegate を付ける」delegate を渡し、単体テストは偽物を渡す）。**付けるのは捕捉の時点（`AddScene` の最初の await より前）。** `OnStabledImpl` はその await の中で走るので、await の後に付けると受付が delegate より先に始まる。完了の通知は Loading・Live のどちらで届いても Ending にする（Add の await が戻る前に届く順序でも取りこぼさない）。Live への遷移は、Add の await が戻った時点で捕捉した個体と現在の個体が同じで Stable であることで判定する。通知で Ending にして unload を発行する（unload 中・保留の位相は SceneDirector が扱う）。Event Scene から session への逆向きの口は作らない。
  - **枝の終了と Session の停止:** controller の `StopWorldOperations()`（`Dispose` から呼ばれる）でも driver を停止し、以後の完了の通知で unload を発行しない（回収は Framework の親子の unload に任せる。A3 凍結前レビュー R3-6）。`TearDownActiveBranchAsync` は driver を停止（以後の要求は `Rejected`、完了の待ちを打ち切る）してから既存の順で Season を unload する。Event は発行時に観測個体に登録済みなので、`WaitAllIncomplete` と `WaitAllLive` が Event の終端まで待つ（AC4・AC5）。driver は Season を載せない。
  - **delegate の上限:** driver が受ける delegate は、発行して捕捉した個体を返すもの、unload するもの、捕捉時に完了の delegate を個体へ付けるもの、の 3 つで止める。4 つ目が要る、または interface が無いと単体テストが書けないなら Phase A へ戻す（§4.2）。
  - **controller の上限:** controller に足すのは driver の生成と停止、上の private な発行、要求の委譲だけ。受理の式と状態遷移を controller の本体へ書かない。599 行から 690 行を超える見込みになったら止める（発行の手続きが controller の外へ漏れ始めた兆候。interface を足して解決しない）。
- **D3（要求の入口）:** `InGameSession` に internal の `RequestMajorEvent(string eventIdentity)` を足し、controller の driver へ 1 行で委譲する（controller が無い・停止後は `Rejected`）。public の interface は作らず、`IInGameSessionServices` にも足さない（PlayerScene・InGameUI を巻き込まない）。同じアセンブリの開始 binder だけが呼ぶ。
- **D4（Event Scene の class）:** 新しい `SeasonMajorEventScene`（`World/`）。`OnInitialize` で root から `MajorEventVfxAnchor`（位置だけ）と `MajorEventCompletionTrigger`（進入の出来事だけ）を集める。`OnLoadedImpl` で `.vfx` を `LoadSceneScopedAssetAsync`（所有者 `Scene(Event)`）でロード（キャンセル窓の外。d4b と同じ）。`OnStabledImpl` で anchor ごとに同期に `Spawn`（所有者 `Scene(Event)`、親なし、時間は gameplay layer）して `Play` する。親の生存は確かめない（Season の unload 中に Stable へ達した Event が起動しても、保留の unload で `Scene(Event)` 所有の回収が壊す。A3 版レビュー R2）。回収は Framework の `Scene(id)` 所有の経路（`PreUnloading` で壊す）に任せ、Scene class で `Stop` を呼ばない。完了の受付は `OnStabledImpl` から始め、その後の最初の進入で driver から渡された delegate を 1 回呼ぶ（ロード中の進入は捨てる。Stable の時点で既に体積の中にいる物体は、出て入り直すまで完了にしない。物理の callback の中では呼ぶだけで、unload は driver が非同期に進める）。source のロード失敗は Event の失敗にせず Error を 1 回出し VFX を起動しない（d4b の U2 と同じ）。
- **D5（開始の trigger）:** 新しい `MajorEventStartTrigger`（MonoBehaviour、event の identity を文字列で持ち、進入の出来事を流すだけ）を `Spring_Events_4_2` に置く。収集と要求は `MajorEventStartBinder`（`LocalVfxEventBinder` と並ぶ）が持ち、`CellCompanionScene` は生成と呼び出しだけ。要求先は Events の親鎖を固定の段数（Events → Cell → Season → `InGameSession`）で `SceneQuery.GetLoadedScene` を使ってたどり、`InGameSession` として得る。任意の祖先を型まで歩く汎用の関数は作らず、既存の直親の解決（PlayerScene 等）も書き換えない。得られなければ Warning を 1 回出して要求しない。Scene を跨ぐ参照は event id だけ（§2.2）。Cell Events に置くので Planner の companion set で動く（E5）。
- **D6（authoring）:** Scene・SceneResource・親子 link・Map・Addressables entry・`.vfx`・anchor・trigger の配置は、一時の Editor スクリプトで Editor API を使って作り、commit の前に消す（判断基準の既定。§2.6 U2）。YAML は手編集しない。残る安全網は AC12・AC13 の EditMode 検査。
- **D7（進入者）:** 開始・完了の trigger の進入者は d4b と同じく `attachedRigidbody` を持つ Collider 全て。在室の追跡はせず、marker は状態を持たない。開始は進入ごとに要求を出し（重複は I2 の規則で吸収）、完了は Scene class が最初の 1 回だけ通知する。

### 2.6 未決事項

- **U1（分割）:** program §6.3 は「major Event と Planner 実証・harvest の見積もりを分け、大きければ追加分割」とする。見積もり: major Event（本 HANDOFF の範囲）は製品コード 430〜600 行・テスト 900〜1300 行・Scene 1 件（第 3 版。U4）。Content Directory の Whitebox を使う Editor Play の Planner 実証は、Editor Play 向けの publish 入口が未整備で、local Event・major Event の `.vfx` が Content Directory に入らない（ロードが失敗する）ので、BuildSystem の実装（非 Scene asset の production 経路と Editor Play の publish 入口）を先に要する。program 全体の harvest（Architecture §05 / §18 / §24 / §27 と Streaming 現状仕様）と削除も文書量が大きい。**推奨: d5 を d5a（本 HANDOFF、major Event と companion set の Planner）と d5b（Content Directory の Whitebox の Planner Play と S-4 の harvest・削除）に分け、d5b の前に BuildSystem の上の 2 件を所有する HANDOFF を切る。** 主目的が BuildSystem なので、d5b の実証を BuildSystem の成果の受け入れに使える。A2 (1)・(3) も同じ分割を支持した。
- **U2（authoring の道具）:** 推奨は一時スクリプト（D6）。代案は World Workspace の transaction に `SeasonEvent` kind を足す（journal の int、ownership proof、window、path 予算のテストを含め +150〜250 行）。SampleGame の検証に道具は不要なので既定どおり一時スクリプト。A2 の 3 件とも一時スクリプトを支持。
- **U3（開始の経路）:** 推奨は Cell Events の開始 trigger（D5）。代案はテストだけが driver へ要求する形（本番の開始経路が無くなり、E5 の「Events の trigger から」を満たせない）。
- **U4（d5a の追加分割。第 3 版）:** 第 3 版の見積もりは製品 430〜600 行（新規 4 種と配線）、テスト 900〜1300 行で、d4b（製品 約 430 行、テスト 約 1500 行、1 スライス）と同じ規模。分割せず 1 スライスで進める。ただし規模の不確かさは Play の根（§5 の未知の操作経路 (1)）に集中しているので、次のどれかが起きたら凍結せず、d5a を「driver と controller の発行、単体の AC1〜AC5、factory の AC13」（Unity の Scene を要しない）と「Event Scene・素材・Play・content build」（AC6〜AC12・AC14）に分ける: (1) の確認（初回は Phase C）で §5 の停止条件に当たる、Play の根の組み立てが d4b の `LocalVfxEventPlayTests.Scenes.cs`（362 行）の規模を大きく超える見込みになる、controller が 690 行を超える見込みになる。

---

## 3. 責務マップ（A3 凍結版）

| ファイル | 責務・変更理由 | 所有者・寿命 | 依存・公開面 | テスト境界 | 行数（現在 → 予想） |
|---|---|---|---|---|---|
| 新規 `Streaming/Runtime/Season/MajorEventDriver.cs` | 受理の純関数と結果の enum、状態（Idle / Loading / Live / Ending）、発行の呼び出し、完了の待ち、終端の観測 | controller の枝ごと。枝の終了で停止 | controller が渡す delegate、query、terminal events。internal | 偽の delegate / query / terminal events で AC1〜AC3 | 0 → 160〜220 |
| `SessionSeasonController.cs` | driver の生成と停止、private な発行（await より前の bind）、要求の委譲 | InGameSession | 既存 | `SessionSeasonControllerTests` に AC4・AC5 | 599 → 640〜680（690 を超える見込みで止める。D2） |
| `InGameSessionScene.cs` | internal の `RequestMajorEvent` で driver へ委譲 | InGameSession | 既存。public 面は変えない | — | 174 → 182〜188 |
| 新規 `World/SeasonMajorEventScene.cs` | Event Scene の lifecycle: source のロード、Stable で anchor の位置に `Scene(Event)` 所有の `Spawn`、Stable からの完了の受付と delegate の呼び出し | Event Scene | `IVfxSystem` | Play（AC6〜AC9） | 0 → 110〜150 |
| 新規 `World/MajorEventVfxAnchor.cs`・`MajorEventCompletionTrigger.cs`・`MajorEventStartTrigger.cs` | Scene に置く marker と trigger。位置か進入の出来事だけを流し、状態を持たない | 各 GameObject | MonoBehaviour（1 ファイル 1 class は Unity の制約） | EditMode（配置）、Play | 0 → 計 50〜90 |
| 新規 `World/MajorEventStartBinder.cs` | 開始 trigger の収集、固定段数の親鎖で `InGameSession` を得て要求 | Cell companion | `ISceneQuery` | Play（AC10） | 0 → 50〜80 |
| `World/CellCompanionScene.cs` | `MajorEventStartBinder` の生成と呼び出し | Cell companion | 既存 | Play | 81 → 88〜95 |
| `SeasonCellNames.cs`・`DependOnAll/GameSceneFactory.cs` | 命名の規則と分類 | — | 既存 | EditMode（AC13） | 44 → 60、130 → 152 |
| Scene・asset（`Spring_Event_MultiCellProof.unity`、SceneResource、`.vfx`、Season_Spring の子、Map、Addressables、`Spring_Events_4_2` の開始 trigger） | 実証の素材 | — | — | AC12・AC14 | — |
| テスト（新規 `MajorEventDriverTests`・`SeasonMajorEventPlayTests`・`MajorEventAuthoringTests`、`SessionSeasonControllerTests` の追加、`VfxSceneStructureTests` の 56 → 57） | — | — | — | — | 0 → 900〜1300 |

- **第 2 版から減らした型と、その条件の検証先:**
  - `MajorEventAdmission` → driver の static な受理。AC2・AC3 は `MajorEventDriverTests` で見る。
  - `IMajorEventIssuer` → controller の private な発行と、driver へ渡す delegate。AC1 は driver の単体で、await より前の bind（AC5）は実際の発行を通す `SessionSeasonControllerTests` で見る。
  - `IMajorEventCompletion` → Scene class の internal method 1 つと delegate。driver 側は AC3 の単体で偽の delegate、Scene 側は AC7 の Play で見る。
  - `MajorEventRequestResult`（別ファイル） → driver のファイルの internal enum。AC1〜AC3。
  - `ISeasonMajorEvents`（public） → `InGameSession` の internal method。AC10 の Play で見る。
  - 祖先の解決の汎用関数 → binder の固定段数の解決。AC10。
  - 旧 AC11 → AC7 (c)。
- **警報（`SessionSeasonController`）:** 既に 500 行を超える。足すのは D2 の 3 つだけで、受理と状態は driver へ置く（companion の `CellCompanionPolicy` / `SessionCellCompanionLoadDriver` と同じ分け方）。発行を controller の private に置くのは、op の登録・tracker・registry が controller の private な所有物だから。
- **非分割（`CellCompanionScene`）:** 開始 trigger の収集を `MajorEventStartBinder` に出すので、Scene class の責務は「配置済み VFX・local Event・major Event の開始を、それぞれの binder に Scene の lifecycle でつなぐ」1 つのまま。
- **配置:** driver は Season の枝の所有物なので `Streaming/Runtime/Season/`。namespace は既存の `SampleGame.InGame.Streaming`。Scene class・trigger・binder は既存の `World/`（d4b と同じ）。新しい asmdef・依存 edge は無い。

## 4. 実装計画（A3 凍結版）

### 4.1 順序

1. driver、controller の private な発行と配線、`InGameSession` の委譲、単体テスト（AC1〜AC5）。
2. Scene class・marker・trigger・binder、`SeasonCellNames`・`GameSceneFactory`（AC13）。
3. 一時スクリプトで Scene・asset を作り、`Spring_Events_4_2` に開始 trigger を置く。一時スクリプトを消す。
4. authoring・Play テスト（AC6〜AC12）。`VfxSceneStructureTests` の件数。
5. `pwsh tools/contract-audit.ps1`、Editor の compile 確認。

### 4.2 Phase B から Phase A へ差し戻す条件

- Framework のコード・公開 API、`SceneState`、`LoadType`、asmdef 参照の変更が要る。
- 親がロード済みのとき、SceneDirector が `AddScene` の最初の await より前に子の個体を辞書に入れない（D2 の発行時の bind が成り立たない）。
- driver を controller の枝の所有にできない（例: 枝の切替が Event の終端を待てない）。
- `Scene(id)` 所有の回収が `ReleaseScene` より後になる（I4 が Framework の契約の外で破れる）。
- controller の外に Season の直接の unload が要る、または `OnSceneEvent` の同期の購読者から要求する経路が要る（I1 の範囲が変わる）。
- 祖先の unload で Season の `PreUnloading` が子孫の収集と同じ同期区間に入らない（AC9 の続きの要求が `Rejected` にならない）。Season の Scene class へ UI の契約を足す、または Framework に unload の開始の通知を足して守ることはしない。
- 発行の失敗（await より前に個体を捕捉できない）から、再試行・2 本目の Add・新しい受理の理由が要る。
- marker・binder・親鎖の解決が、d4b の trigger 以上の状態を持ち始める。
- Play の根を成り立たせるために、Session・Ensure・streaming・companion のどれかの製品の挙動を変える（§5 の停止条件）。
- driver の delegate が 3 つで足りない（D2）、または発行の interface や public の要求 interface が無いと単体テストが書けない。driver が controller の private な registry に触りすぎている兆候なので、発行の手続きを controller の private に戻して解く。層を足して解かない。
- controller が 690 行を超える見込みになる（D2）。
- §5 の未知の操作経路 (1) の確認で選んだ構成が Play テストで成り立たず、本番に無い composition が要る。

### 4.3 BuildSystem の既知の課題との関係（判断基準による記録）

- Event Scene は Content Directory の Spring build に Scene payload として入る（AC14 で Phase C が 1 回 build する）。`.vfx` は入らないので、Content Directory の実行では Event のロードは成功し VFX のロードだけが失敗して Error を出す（d4b と同じ）。これを直す「非 Scene asset の production 経路」と「Editor Play 向けの publish 入口」は BuildSystem の実装の候補で、U1 の推奨では d5b の前提として所有 HANDOFF を切る。
- d5a は Player を build しない。BS4 の Player が Spring に入らない課題は S-9 / BuildSystem の候補のまま（本スライスでは踏まない）。

## 5. テストとレビュー計画（A3 凍結版）

- **単体テスト:** `MajorEventDriverTests`（AC1〜AC3。偽の delegate・query・terminal events）、`SessionSeasonControllerTests` の追加（AC4・AC5 の 3 位相）。既存の偽 controller / query / terminal events を使う。
- **差し戻し中の起点 `-Filter`:** `MajorEventDriverTests`、`SessionSeasonControllerTests`、`SeasonMajorEventPlayTests`、`MajorEventAuthoringTests`、`VfxSceneStructureTests`、`LocalVfxEventPlayTests`。
- **判定必須テスト:** 最終の全 EditMode 回帰（空 filter）、PlayMode の `SeasonMajorEventPlayTests`・`LocalVfxEventPlayTests`・`VfxScenePlayTests`・Streaming の PlayMode（`-WithGraphics` を併用。§16.7 の `-nographics` の Inconclusive）、`Build Spring Whitebox` の content build 1 回（AC14）。
- **全 EditMode 回帰の適用除外:** なし。
- **操作・実行時・目視条件の検証経路:** AC6〜AC10 は Play テスト、AC14 は Phase C の content build（`Tools/OSM/Content/Build Spring Whitebox`、生成された Content Directory の manifest に Event の Scene payload があることを見る）で閉じる。人間の目視は無い。
- **未知の操作経路:** (1) Play テストで開始 binder が `InGameSession` を解決し、実 controller の Add / Unload を通す構成。確かめるのは「複製 Map の根を実 `InGameSession` にして Season_Spring の枝だけを残せるか」だけ。`InGameSession.OnStabledImpl` の初期 Ensure が Spring `(0,4)` を固定で載せるのは既存の挙動で、テストがその Cell を Map に含めて既存の Ensure に載せるのは製品の変更ではない。そのうえで Cell (4,2)・(5,2) と Events_4_2 をどう載せるか（距離 streaming は Focus が null なので動かない読み。companion driver は resident の Cell の子だけを載せる）を、既存の挙動の範囲で決める。**計画した構成（コードの読みで決めた。d4b の `LocalVfxEventPlayTests.Scenes.cs` と同じ作り）:** 複製 Map の根を `InGameSession` にし、Season_Spring と その `NecessaryAlways` の子、Cell (0,4)・(4,2)・(5,2) とその Events、Event を含める。`GameSceneFactory` は `CellCompanionSet.Planner` で作る。Cell (4,2) と Events_4_2 は、テストが公開の `RegisterFlight` に偽の `IFlightReadModel` を (4,2) の位置で登録し、既存の距離 streaming と companion driver に載せさせる（Focus が null でなくなる。製品の変更は無い）。**状態: 未確認。初回確認は Phase C**（理由: 確認には Play Mode テストの実行が要り、Phase A・B では実行しない。担当: Phase C の実行担当。確認地点: Phase C の最初の発見の実行）。Phase B は compile と、Editor での Map の組み立ての範囲の確認までにする。**不成立のとき（A3 で合意）:** 下の停止条件に当たるなら製品を変えずに §2.6 U4 の分割（Scene を要しない側を先に完了する）へ進む。
  - **停止条件（次が要るなら製品側を都合よくせず、凍結しない。§2.6 U4 の分割を含めて Phase A で比べ直す）:** Ensure を飛ばすフラグ・テスト用の子 class・初期 Cell を引数で差し替える口。Focus が null で距離 streaming が動かないことへの streaming 側の特別扱い。companion driver が resident の子しか載せず Events_4_2 が載らないことへの、driver の新しい載せる規則。要求の入口を public にすること。
  - **代案:** 製品の API を足すのではなく、テストが driver を直接組む構成（driver は Season を知ってよい）。
  - (2) 完了・開始の trigger の進入を kinematic Rigidbody の `MovePosition` で起こす経路は d4b で成立済み。
- **人間の判断が必要な条件:** なし。
- **高コスト証拠:** なし。
- **判定 C の照合 script の方式:** なし（テスト XML の実行テスト名と件数、content build の outcome で判定）。
- **機械検査:** `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。

### 5.1 レビューの記録（凍結時に要約）

- **A0/A1 主担当:** この session の主担当（Anthropic 系）。
- **A2 入力版:** commit `1fa1b472`、内容 sha256 `92ecad46e1ca5063e5d7d3706e14d028e1bd1b7ff40868415fe2ab1570cf961d`。
- **A2 独立レビュー（2026-10-29、同じ入力版、互いの指摘は見せない）:** (1) アーキテクチャゲート（Anthropic 系、主担当とは別のモデル）、(2) 不変条件の反例探し（cursor-agent、xAI の Grok 系）、(3) A0 だけからの代替構成（Anthropic 系、主担当・(1) とは別のモデル）。
- **A3 の追加レビュー（発注者経由、4 回）:** A3 統合版（R1〜R4）、第 2 版のスコープレビュー、第 3 版の読み、第 3 版の反証レビュー（R3-1〜R3-6、OpenAI 系）。
- **発注者の決定（2026-10-29）:** U1〜U3 は推奨案で承認（d5a / d5b の分割、一時スクリプト、Cell Events の開始 trigger）。「根幹はアプリを前提にしない。前提にしないと膨らむなら後続へ送る」「膨らみそうなら分割する」。後者は U4 に記録し、規模が d4b と同等なので 1 スライスのまま進める。
- **採用した主な指摘（反映先）:**
  - 受理・状態の記録・Add の開始を 1 つの同期区間にする（Grok。I1・I2・D2）。
  - Add の await 中に Event が終端すると op が切替の待ちを永久に塞ぐ（Grok、コードで確認）→ major Event だけ await より前に bind（D2）。既存の companion の経路は変えず後続へ（§2.4）。
  - 分類は名前・親・`OnDemand`・非距離の全部（Grok、R3。D1・AC13）。
  - 位相の語の定義、AC5 の位相、AC14 の content build 1 回、フレームの指定（Grok）。
  - Season の unload 中の受理の窓（R1）→ 新しい仕組みは足さず、範囲と根拠をコードの観測として I1 に明記。Season の UI の有無を契約にしない（第 2 版レビュー）。
  - 「unload の開始後の `Spawn` 0 回」は保証しない（R2）。回収だけを問う（I3(b)・D4・AC9）。
  - AC7・AC8 を記録 backend と実 backend に分ける（R4）。実 backend は Event が起動した `VisualEffect` だけを数える（R3-4）。
  - 第 2 版の「アプリを前提にしない層」（port・別フォルダ・公開の要求 interface・汎用の祖先関数）は膨らみとして撤回し、d4b と同じ粒度に戻した（第 2 版レビュー。D0・§3）。完了の delegate を付ける時点、delegate は 3 つまで（第 3 版の読み。D2）。
  - 1 枝に major Event は最大 1 件（R3-1。I2・AC3）。発行の失敗でも元の Add の終わりを観測する（R3-2。D2・AC5 (iv)）。Events_4_2 は companion driver に載せさせ、テストが直接 Add しない（R3-3。AC10・§5）。AC9 は後続フレームの受付の停止の確認と位置づける（R3-5）。Session の停止でも driver を止める（R3-6。D2）。
  - 主担当の見直し: 完了の受付を Stable から始める（D4）、Loading 中の完了（AC3）、AC9 の要求のフレーム。
- **採らなかった主な代案:** `OnPreUnLoadedImpl` で全 instance を壊す（`Scene(id)` 所有の回収を確かめるのが問い）、Timeline・時間経過での完了、World Workspace の `SeasonEvent` kind、Framework への unload 開始の通知・major Event の汎用化（§2.4 の後続へ）、Play の位相別の網羅、テストのための Ensure・streaming・companion の変更。
- **経緯の原文:** レビューの原文は本文に転記しない（Phase A snapshot の対象）。
- **C' 用に予約した担当:** Phase A に関与した系列は Anthropic 系・xAI（Grok 系）・OpenAI 系（第 3 版の反証レビュー R3 は発注者が依頼した OpenAI 系。A2 の時点の OpenAI 系の予約はこれで使った）。C' は Phase A〜C に関与していない系列（例: Google 系）を Phase C' の開始時に利用可能なものから選ぶ。どれも使えない場合は `独立性制約あり` として理由と残存リスクを記録し、発注者が採否を判断する。

## 6. Phase B 実装結果

- 実装（`1766af84`、base `22ebf130` からの差分）:
  - 製品: `MajorEventDriver.cs`（新規 407 行。受理の純関数 `Admit`、結果と理由の enum、Idle / Loading / Live / Ending、Idle へ戻るのは Add の task の終わりと捕捉個体の終端の両方を観測した後）、`SessionSeasonController.cs`（599 → 675 行。driver の生成・停止、await より前に観測登録する private の `IssueMajorEventAdd`、`RequestMajorEvent` の委譲）、`InGameSessionScene.cs`（174 → 181 行、internal の `RequestMajorEvent`）、`SeasonMajorEventScene.cs`（新規 172 行）、`MajorEventVfxAnchor.cs`・`MajorEventCompletionTrigger.cs`・`MajorEventStartTrigger.cs`（新規 計 98 行）、`MajorEventStartBinder.cs`（新規 87 行）、`CellCompanionScene.cs`（81 → 84 行）、`SeasonCellNames.cs`（44 → 63 行、`MajorEvent` / `TryParseMajorEvent`）、`GameSceneFactory.cs`（130 → 148 行）。
  - Scene・asset: `Seasons/Spring/Events/Spring_Event_MultiCellProof/`（`.unity`・SceneResource・`_Burst.vfx`）、`SceneGraphData/Nodes/Seasons/Spring_Event_MultiCellProof.asset`、`Total.asset` の node と Edge、`Season_Spring.asset` の子、`SceneResourceMap.asset`（再生成）、Default Local Group の Scene と `.vfx` の entry（address は asset path）、`Spring_Events_4_2.unity` に開始の trigger。`Seasons/` 配下の `.unity` は 658 件、Season_Spring の子は 57 件。
  - テスト: `MajorEventDriverTests`（新規。AC1〜AC3 と停止・捕捉なしの発行）、`SessionSeasonControllerTests`（AC4、AC5 (i)〜(iv)）、`GameSceneFactoryTests`（`GameSceneLoggingTests.cs` に AC13）、`MajorEventAuthoringTests`（新規、AC12 の SceneResource・Addressables・Scene の中身・開始の trigger）、`VfxSceneStructureTests`（AC12 の node・Edge・Map・選択に Event を追加、子の件数 56 → 57）、`SeasonMajorEventPlayTests`（新規、AC6〜AC10。Play の根は `.Scenes.cs` の 334 行）。
- HANDOFF との差（いずれも B 適応。契約・責務配置・受け入れ条件は変えていない）:
  - **起動位置の marker が `.vfx` の address を持つ。** D4 は anchor を「位置だけ」とし、source の address の置き場所を決めていない。d4b の trigger（`VfxAddress`）と同じく authoring の値として anchor に置き、Scene class は異なる address ごとに 1 回ロードする。marker は判断も状態も持たない。
  - **AC12 を 2 つの test class に分けた。** `SampleGame.Tests` は `OneStarMaker.Editor` と Addressables を参照しない（asmdef 参照は足さない）ので、node・Edge・Map・payload の参照先・Full / Whitebox の選択は Editor asmdef の `VfxSceneStructureTests` の spec 表に Event を足して見る。これに伴い同 class の `SeasonSpring_ChildrenAreLightingAtmosphereAnd54Cells` を `...MajorEventAnd54Cells`、`SeasonSelection_SpringIncludesBothVfxScenes` を `...IncludesVfxAndMajorEventScenes` に改名した（判定 C の XML ではこの名前で探す）。AC13 は既存の `GameSceneFactoryTests` に足した（fake の重複を避けるため）。
  - **authoring の一時スクリプト。** 既存の World Workspace の作成 transaction を Season 直下の子の種類（Atmosphere）で実行し、node の LoadType だけを OnDemand に直して Map を再生成した。`.vfx` は `Spring_Events_4_2_Burst.vfx` の複製。スクリプトは commit の前に削除した。1 回目と 2 回目の実行は、Editor に未保存の untitled Scene があって transaction の Scene 作成と rollback が失敗した（作られたのは空フォルダと journal だけで、node・Edge・Map・Addressables は未変更を確認したうえで手で消した）。保存済み Scene を先に開く形に直して 3 回目で成功。
  - **Play は 6 本。** §2.3 の「3 本と AC9 の 1 本」に対し、AC7 の (a) 記録 backend・(b) 実 backend・(c) 非対応は host が別なので分け、同じ host の AC7 と AC8 は 1 本にまとめた（AC6、AC7 (a)+AC8 (a)、AC7 (b)+AC8 (b)、AC7 (c)、AC9、AC10）。位相ごとの Play は作っていない。
  - **Play の根。** 計画した構成のまま（複製 Map の根を実 `InGameSession`、factory は Planner、Focus は公開の `RegisterFlight` に偽の Flight）。Cell (5,2) は含めていない（起動位置と完了の trigger は Event Scene にあり、Cell (5,2) を載せる必要がない）。Lighting・Atmosphere も含めない（`RenderEnvironment` を要し、Event の VFX だけを数えるため）。AC10 の「Environment が載っていない」を意味のある観測にするため `Spring_Environment_4_2` を Map に含め、Planner で載らないことを見る。AC8 の枝の終了には `Season_Summer` と `Summer_Cell_0_4` を Map に含め、controller の `RequestSeasonAsync` を reflection で呼ぶ。
  - **行数。** 製品は約 870 行で見積もり（430〜600 行）を超えた。主に driver（見積もり 160〜220 → 407 行）の enum 3 種・契約のコメント・捕捉なしの発行の経路による。controller は 675 行で上限 690 の内、Play の根は d4b の 362 行以内。§2.6 U4 の分割の条件には当たらない。
- 未確認（発見 C の最初の実行で確かめる）:
  - §5 の未知の操作経路 (1)（Play の根が実 `InGameSession` の Ensure・距離 streaming・companion driver で成り立つか）。
  - AC9 は、Event の状態が `Loading`〜`WaitLoadChildScene` に入ったことを test のフレームの処理で見てから Session を unload する。Unity Scene のロードが 1 フレームで終わると Stable に先を越され、前提の Assert（「unload の時点で Event は Stable でない」）で落ちる。その場合は製品を変えず、観測の取り方だけを B で直す。
- 未実行: Unity テスト（全 EditMode・PlayMode）と `Build Spring Whitebox`（AC14）は Phase C。Editor でのコンパイルは確認済み（エラー 0、変更したファイルに警告なし）。`pwsh tools/contract-audit.ps1` は errors=0 warnings=0。
- implementation head commit: `1766af84`
- Phase B 担当・モデル・ベンダー: この session の担当（Anthropic 系）。

### 動作確認

- 対象 head: `1766af84`
- 条件ごとの実行担当・環境・実行したコマンド: なし（下記）
- 生の観測と保存先・状態: なし
- 検証経路の期待と違う観測と、B への差し戻し: なし
- `未実施` の条件と理由・担当: `対象なし`（§5 の検証経路は AC6〜AC10 が Play テスト、AC14 が content build で、どちらも動作確認の対象外。人間の目視の条件は無い）

## 7. Phase C

### 発見 C（1 巡目）

- 種別: 発見。対象: implementation head `1766af84`（base `22ebf130`）。evidence bundle は作らず commit 固定の diff を直接読んだ（取得案内: 未生成）。GO 判定はしない。
- 担当・モデル: Phase B と別の担当（Anthropic 系の別 session）。
- 機械検査: `pwsh tools/contract-audit.ps1` errors=0 warnings=0、`pwsh tools/docs-audit.ps1` errors=0 warnings=0。
- 構造適合: 責務マップと一致（driver は Season の枝の所有物、発行は controller の private、Scene class・trigger・binder は `World/`、新しい asmdef・公開面なし）。controller 675 行（上限 690 の内）。偽 null は `== null` 判定。Framework 無変更。製品コードの欠陥は見つからなかった。
- 実行したテスト（起点 filter の範囲。Unity は EditMode で起動）:
  - EditMode 78 件 全通過（test-run の total=78、failed=0。対象は `MajorEventDriverTests`・`SessionSeasonControllerTests`・`GameSceneFactoryTests`・`MajorEventAuthoringTests`・`VfxSceneStructureTests`）。
  - `SeasonMajorEventPlayTests`（`pwsh tools/run-tests.ps1 -WithGraphics -Filter SampleGame.Tests.Rendering.Vfx.SeasonMajorEventPlayTests`）6 件中 3 件通過（AC7 (b)+AC8 (b)、AC7 (c)、AC10）、3 件失敗（下の F1〜F3）。
- **§5 未知の操作経路 (1): 成立。** 複製 Map の根を実 `InGameSession`、Planner、偽 Flight の構成で、Ensure・距離 streaming・companion driver が動き、Events_4_2 が companion driver から載り、major Event が Add / Stable / 完了 / 枝の終了で回収された（AC10、AC7 (b)、AC8 (b) が通過）。停止条件に当たらず、§2.6 U4 の分割は不要。
- **実行環境の事実（判定 C の手順に影響）:** `SampleGame.Tests` の asmdef は `includePlatforms: Editor` のため、Play 系の `[UnityTest]` は `-Platform PlayMode` では 0 件になる（`-Platform PlayMode` は「成功したテストがありません」で failed）。Play 系は EditMode の `pwsh tools/run-tests.ps1 -WithGraphics -Filter ...` で実行する。§5 の判定必須テストの「PlayMode」はこの意味で読む。また filter は namespace 付きで書く（`OneStarMaker.Tests.Streaming.*`、`OneStarMaker.Tests.SampleGame.GameSceneFactoryTests`、`SampleGame.Tests.Rendering.Vfx.*`、`OneStarMaker.Tests.Editor.WorldAuthoring.VfxSceneStructureTests`）。

#### 指摘

| ID | bucket | 違反する条件 | 内容 | 修正先 |
|---|---|---|---|---|
| F1 | 現在の問いを阻害する欠陥（テストの観測） | AC6（検証が成り立たない。製品の違反ではない） | `Start_SpawnsAtBothAnchors_AndSurvivesCellUnload` の 93 行が失敗。待ちが `!IsSceneLoaded(Cell)` で、`IsSceneLoaded` は unload の開始で false になる。AC6 の定義は「Cell の `Removed` の次のフレーム」。Cell の unload の開始の次のフレームには子の Events がまだ unload 中で `IsSceneLoaded(Events)` が true になる（出力: `cellRemovedFrame=1344 eventsLoaded=True`）。Event の生存（94 行）はその前に確認していない。 | B 適応: Cell の `Removed` イベントを購読して待つ。製品は変えない |
| F2 | 現在の問いを阻害する欠陥（テストの観測） | AC7 (a)・AC8 (a)（同上） | `CompletionAndBranchSwitch_RecordingBackend_DestroyBothBeforeSourceRelease` が `AssertRecoveryOrder`（302 行）で失敗。タイムラインは `event Stable → destroy id=1 → destroy id=2 → event PreUnloading → ... → event Removed owners=0`。VfxSystem は `BindSceneLifecycle` で test の購読より先に `OnSceneEvent` を購読するので、PreUnloading の同じ dispatch の中の Destroy が test の `event PreUnloading` の記録より前に並ぶ。Destroy は source が所有されたまま（`ownedByEvent=True`）、`Removed` の時点の所有者は 0 で、I4 の順序そのものは満たしている。assert の「Destroy が PreUnloading の後」が購読順の人工物。 | B 適応: PreUnloading の記録と Destroy の順序を購読順に依存しない形にする（例: Destroy が Stable の後・`PreUnloaded` の前、かつ `ownedByEvent=True`、と読む）。製品は変えない |
| F3 | 凍結条件の読みの確定が要る（人間の決定記録） | AC9 と AC3 | `SessionUnloadDuringEventLoad_EventEndsAfterPending_AndRequestsAreRejected` の 228 行が失敗: Session の unload の開始の次のフレームの要求が `Rejected` ではなく `AlreadyActive`。Event は窓の外のロード中（driver は Loading）で、Session 自身の `OnPreUnLoadedImpl`（driver の `Stop`）は子の保留が終わるまで走らないため、driver はまだ停止していない。受理は同じ identity かつ Loading を AlreadyActive と定め（AC3）、Add は発行されていない（`EventPreLoadingCount` の assert 237 行は未到達）。I1 の「Add を発行しない」・AC9 の主旨（後続フレームの受付の停止）は満たすが、AC9 の文言「`Rejected`」とは合わない。AC3（Loading は AlreadyActive）と AC9 の文言が同じ状況で食い違う。 | 人間の決定記録で読みを確定する（案: AC9 の続きは「Add 0（`Accepted` でない）」とする。`Rejected` に統一するなら受理に「親の unload 開始」を足す設計変更で Phase A 再開）。確定後に B で test を直す |

- 後続スライスの入力: なし。
- 判定必須テストのうち未実行: 全 EditMode 回帰（空 filter）、`VfxScenePlayTests`・`LocalVfxEventPlayTests`・Streaming の Play 系（`-WithGraphics`）、`Build Spring Whitebox`（AC14）。差し戻しが決まったため判定用の検証一式は実行していない。
- 未確認事項: AC9 の Event のロード中の位相は、F3 の位置（228 行）まで前提 assert（`Event が Stable でない`）を通過した。つまり §6 の未確認（Unity Scene のロードが 1 フレームで終わり Stable に先を越される懸念）は、この 1 回の実行では起きなかった。以後の実行での再現性は未確認。
- 差し戻し: B へ（F1・F2）。F3 は人間の決定記録を先に取る。往復は 1 巡目として数える。

## 8. Phase C'

未着手

## 9. Phase D

未着手
