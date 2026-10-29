# S-4d5 major Event

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A2 入力版。未凍結）
- branch: `claude/s-4d5-major-event`
- implementation base commit: `22ebf130`（`develop`、S-4d4b の PR #148 のマージ）
- implementation head commit: 未固定
- risk: `high`（Season の排他 controller に新しい所有物を足す。親の unload と子の Add の競合、Addressables entry の追加、SampleGame 内の公開 interface の追加に触れる。Framework のコードと公開 API は変えない見込み）
- owner: 発注者 / S-4d5 担当
- created: 2026-10-29
- expires: S-4d5 の Phase D 完了時
- harvest to: Architecture §05 §5.13（Season 直下の major Event の分類と寿命）、§24 §16（`Scene(id)` 所有の動的 VFX の実例。§16.7 の更新）。S-4 program 全体の harvest と削除は §2.6 U1 の分割案では d5b の範囲
- workflow task: `s-4d5-major-event`（未開始。最初に Evidence を固定する時点で `pwsh tools/workflow-task.ps1 start` する）
- Phase A snapshot の取得案内・状態: 未生成（凍結した HANDOFF 本体は A3 凍結 commit で固定する）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票: Phase A 作業中。本版は A2 の入力版。A2 の独立レビューを統合した A3 版を作り、発注者が凍結を指示するまで Phase B に入らない。

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
- **SceneDirector の性質。** `AddScene` は未ロードの祖先を `isLoadChildren` で載せる（Season が未ロードなら Event の Add が Season と Lighting・Atmosphere を controller を通さずに載せる）。親の unload は `CollectLoadedDescendants` の後順で子を PreUnload → Unity unload → AfterUnload の 3 段で回収する。ロード中の子はキャンセル窓の内なら cancel、外なら `_pendingUnloads` に入り、公開 `AddScene` の対象の経路で Stable の後に消費される。**親が unload 中に子を Add した場合**、子の待ちは子の identity だけを見て、`LoadSceneBase(parent)` は親が `None` 以外なら早期に戻るので、子孫の収集後に Add された子は取り残されうる（未テストの読み）。
- **分類。** `GameSceneFactory` は `StreamByDistance` → `DemoCellScene`、親が `StreamByDistance` → `CellCompanionScene`、`{Season}_AtmosphereVFX`（親 `Season_{Season}`）→ `SeasonAtmosphereVfxScene`、identity の完全一致、それ以外は null（`SceneDirector` が "SceneFactory returned null" で失敗）。`SeasonCandidateSelection` は `StreamByDistance` でない子を距離候補に入れない。**"Spring_Event_" / "MultiCellProof" を参照するコード・asset は無い。**
- **local Event（d4b）。** `CellCompanionScene`（81 行）が `OnInitialize` で `PlacedVfxAdopter` と `LocalVfxEventBinder` を作り、`OnLoadedImpl` で `.vfx` を `Scene(Events)` 所有でロード、`OnPreUnLoadedImpl` で `EndAll()`。trigger（`LocalVfxEventTrigger`）は出来事を Scene class の session へ流すだけ。
- **Scene class から session の service への経路。** `PlayerScene`・`InGameUI` は親を `SceneQuery.GetLoadedScene(parent.Identity) as IInGameSessionServices` で解決する（兄弟間のやり取りは親の `IInGameSessionServices` 経由のみ）。Cell の companion と Season の子は `InGameSession` の子孫で、祖先をたどれば同じ口に届く。
- **Planner。** `world:cellCompanionSet` は `AppInitializer` が読み、`GameSceneFactory` → `InGameSession` → `SessionCellCompanionLoadDriver` に渡る。Planner は Cell の Events だけを載せる。Season の子（Lighting・Atmosphere・Event）は companion set の対象外。`content:representation` は Content Directory のモードで読む。Editor Play は `ContentDeliveryPlayBridge` が verified install から環境変数を設定するが、companion set は設定せず、Editor Play 向けの publish 入口は未整備（program §3.1）。**Planner + Whitebox の全体の Play テストは無い。** `LocalVfxEventPlayTests` は test の `SceneDirector` と複製 Map（Season_Spring・Cell_4_2・Events_4_2）に `CellCompanionSet.Planner` を渡して載せる。
- **World Workspace。** 作成 transaction の kind は `CellCompanion=0`・`SeasonAtmosphere=1`（journal に int で永続化、追加のみ）。Season の Event の kind は無い。
- **Content build。** Season の子は親をたどって Season に属すると判定され、Full のみの payload は Whitebox build と共有される（Lighting・Atmosphere と同じ）。node・payload・Addressables の候補が揃わないと "Payload/candidate mismatch" で失敗する。`.vfx` は Scene の依存でないので入らない（§16.7）。
- **件数。** `Seasons/` 配下の `.unity` は 657。`VfxSceneStructureTests.SeasonSpring_ChildrenAreLightingAtmosphereAnd54Cells` が Season_Spring の子 56 件を確かめている（d5 で 57 に変わる）。

---

## 2. 意思決定と受け入れ境界（A1）

- **このスライスが答える問い:** Season 直下の独立 Event Scene を、Season の排他 controller の管理下で Cell bundle と独立に Add し、完了または Season の unload で、その Scene と Scene 所有の動的 VFX を確実に回収できるか。

### 2.1 不変条件（起きてはいけないこと）

- **I1（親の外で生きない）:** major Event の Scene は、active な Season が Stable で枝の操作中でない間にだけ Add される。Season が未ロード・unload 中・枝の切替中・Session の停止後の要求は Add を発行しない（祖先の暗黙のロードも、unload 中の親への取り残しも起きない）。
- **I2（重複しない）:** 同じ Event identity の生存個体は高々 1 つ。生存中・ロード中・終了処理中の再要求は 2 つ目の Add を発行しない。
- **I3（Season より長く残らない）:** Season の unload（枝の切替・Session の unload）の後に、Event の Scene 個体も、その Scene 所有の VFX instance も残らない。枝の切替は、Event の終端（`Removed`）を待ってから次の Season を Add する。
- **I4（回収の順序）:** Event の Scene 所有の VFX は、source asset の解放（`ReleaseScene`）より前に壊される。`Removed` の次のフレームに、その Event の `VisualEffect` は 0 件。
- **I5（既存を変えない）:** Season の切替、距離 streaming、companion driver、local Event（d4b）、配置済み VFX（d4a）の振る舞いと既存テストの結果を変えない。Framework のコード・公開 API、`SceneState`、`LoadType` を変えない。

### 2.2 成立条件（起きなければならないこと）

- **E1（起動）:** active な Season が Stable の間の要求で、Event Scene が Add され Stable になり、複数の Cell（4,2）と（5,2）の範囲に置いた anchor の位置で VFX が起動する。
- **E2（完了）:** Event Scene の完了の trigger で、controller が Event Scene を unload し、VFX が回収される。完了後の再要求で再び起動できる。
- **E3（Season の unload）:** Event の生存中の Season の unload で、Event Scene と VFX が回収される。
- **E4（Cell からの独立）:** Event の生存中に起点の Cell（4,2）とその Events を unload しても、Event Scene と VFX は残る。
- **E5（Planner の companion set）:** `CellCompanionSet.Planner` の構成で Environment を載せずに、Cell Events の開始 trigger から major Event が起動し、完了する。Content Directory の Whitebox を使う全体の Planner Play は U1 で d5b へ分ける案。
- **E6（authoring）:** `Spring_Event_MultiCellProof` が Season_Spring の子（OnDemand、`StreamByDistance` false、payload は Full のみ）として存在し、`GameSceneFactory` が major Event の class に分類し、Addressables entry と Content Directory の Spring build の Scene payload に入る。Season_Spring の子は 57 件、`Seasons/` 配下の `.unity` は 658 件。

### 2.3 受け入れ条件（検証する条件を併記）

単体 = 偽 scene controller / query / terminal events の純 C# テスト。Play = test の `SceneDirector` と実 Scene の Play Mode テスト。EditMode = authoring の検査。

| AC | 内容 | 条件 | 種別 |
|---|---|---|---|
| AC1 | active Season が Stable の間の要求 1 回で、Add が 1 回発行され、受理の結果を返す | E1 | 単体 |
| AC2 | Season 未ロード、枝の切替中（`_branchBusy`）、`StopWorldOperations` 後、Event が active Season の子でない（別 Season の Event、Cell、Lighting 等）の要求は、Add を発行せず拒否の結果を返し、Warning を 1 回出す | I1 | 単体（失敗経路） |
| AC3 | 生存中・ロード中・終了処理中の再要求は Add を発行しない（ロード中・生存中は既存として、終了処理中は拒否として返す）。`Removed` の後の再要求は新しい Add を発行する | I2・E2 | 単体（失敗経路） |
| AC4 | 枝の切替（`RequestSeasonAsync` で別 Season）は、Event の生存中なら Event の `Removed` を待ってから次の Season を Add する（偽 terminal events で Event の終端を遅らせると次の Add が保留される） | I3 | 単体 |
| AC5 | Event のロード中（`OnLoadedImpl` の await 中）に枝の切替を始めても、Event の終端の後に次の Season が Add され、Event の個体は残らない | I3 | 単体（失敗経路） |
| AC6 | 実 Scene: 開始 → Stable → anchor 2 か所に VFX 2 件。Cell (4,2) と Events を unload しても Event と VFX 2 件は残る | E1・E4 | Play |
| AC7 | 実 Scene: 完了の trigger に kinematic Rigidbody を入れると、`PreUnloading` → VFX の `Destroy` 2 回 → `ReleaseScene`（`IAssetDiagnostics.GetOwners` から Event の所有者が消える）→ `Removed` の順。`Removed` の次のフレームで Event の `VisualEffect` 0 件。再要求で再び 2 件 | E2・I4 | Play（実 backend） |
| AC8 | 実 Scene: Event の生存中に Season を unload すると、Event の `PreUnloading` → VFX の `Destroy` 2 回 → `ReleaseScene` → `Removed`、Season の `Removed` の後に Event の個体・VFX が 0 | E3・I3・I4 | Play |
| AC9 | 実 Scene: `CellCompanionSet.Planner` で Season・Cell (4,2) を載せ、Environment が 1 件も載っていない状態で、Events の開始 trigger から Event が起動し、完了の trigger で回収される | E5 | Play |
| AC10 | 非対応の `VfxSystem` でも開始・完了・Season の unload が例外なく進み、`VisualEffect` は有効にならない | I5・E2 | Play |
| AC11 | `Spring_Event_MultiCellProof` の node・payload（Full のみ）・親子 link・Map・Addressables entry（Scene と同じ group、address は asset path）、`.vfx` の Update Mode、Scene に Renderer・有効な `VisualEffect`・`PlacedVfxActivator` が無いこと、開始・完了の trigger の配置。Season_Spring の子 57 件。`GameSceneFactory` の分類 | E6 | EditMode |
| AC12 | Content Directory の `Build Spring Whitebox` の選択結果に `Spring_Event_MultiCellProof` の Scene payload が入る（Full を共有） | E6 | EditMode（選択 policy の単体）または Phase C の build |
| AC13 | 既存の Season の切替・companion driver・local Event・配置済み VFX のテストが変わらず通る（最終の全 EditMode 回帰と、Streaming・Vfx の PlayMode） | I5 | 回帰 |

AC3 の読み方: 「終了処理中」は完了の要求を受けてから `Removed` まで。合格例: 完了の直後の再要求は拒否、`Removed` の次の要求は Add 1 回。不合格例: 完了の直後の再要求で Add が発行され、unload 中の個体と新しい個体が同時に生きる。

AC6 の読み方: 「残る」は Cell の `Removed` の次のフレームで Event が Stable かつ VFX 2 件が `IsActive`。

### 2.4 Architecture §16.7 の事項の扱い

| 事項 | 扱い |
|---|---|
| `Scene(id)` 所有の動的 VFX の実 Scene での回収（d1 の寿命管理の対象として d5 に残っていた） | 本スライスで確かめる（AC7・AC8） |
| notifier 単独の `OnDestroy`・`Bind` の 1 フレーム回収 | 変更なし（major Event は `Bind` を使わない） |
| `LoadAssetAsync` の await 後の所有者確認の欠落 | 変更なし。major Event も `OnLoadedImpl` でロードして避ける（d4b と同じ） |
| `.vfx` の Content Directory・Player の経路 | 変更なし。BuildSystem の候補（§4.3） |
| 本番の SceneGraph での Season と Cell の同時ロード（S-9） | 変更なし |

### 2.5 A1 の設計

- **D1（名前と配置）:** identity は `{Season}_Event_{slug}`、本スライスは `Spring_Event_MultiCellProof`。Season_Spring の子、`LoadType.OnDemand`、`StreamByDistance` false、payload は Full のみ（Whitebox build と共有）。フォルダは program §2.5 の `Seasons/Spring/Events/Spring_Event_MultiCellProof/`。`.vfx` は同じフォルダ。命名の規則は `SeasonCellNames` に足し（`MajorEvent(season, slug)` と判定）、`GameSceneFactory` は Atmosphere と同じく規則で分類する（親が `Season_{Season}` で `StreamByDistance` でない）。program §2.3 の `Season_Event_{slug}` を凍結 commit で直す。
- **D2（寿命の所有者）:** `SessionSeasonController` の枝が所有する。新しい internal の `SessionMajorEventDriver` を枝ごとに作り（companion driver と同じく `CreateDrivers` で作り、`TearDownActiveBranchAsync` と `Dispose` で破棄）、controller の `IssuedSceneController` を通して Add / Unload する。Add した個体は既存の `BindCapturedInstance` で観測個体として登録するので、枝の切替の `_tracker.WaitAllLive` が Event の終端も待つ（AC4）。受理の判定（I1・I2）は driver が持つ: active Season が Stable、`_branchBusy` でない、停止していない、identity が active Season の直下で OnDemand かつ `StreamByDistance` でない、生存個体・ロード中・終了処理中でない。driver は Season を載せない（祖先の暗黙のロードを起こさない）。
- **D3（要求の口）:** `IInGameSessionServices` に `ISeasonMajorEvents MajorEvents { get; }` を足す。`ISeasonMajorEvents` は `MajorEventRequestResult Request(string eventIdentity)` と `void Complete(string eventIdentity)`（同期に受理を返し、Add / Unload は内部で進める。失敗は Warning ログ）。`InGameSession` は controller へ委譲する（controller が無い・停止後は拒否）。SampleGame.InGame 内の公開 interface の追加で、Framework の公開面は変えない。
- **D4（Event Scene の class）:** 新しい `SeasonMajorEventScene`（`SampleGame/InGame/InGameSession/World/`）。`OnInitialize` で root から `MajorEventVfxAnchor`（位置だけを持つ marker）と `MajorEventCompletionTrigger` を集める。`OnLoadedImpl` で `.vfx` を `LoadSceneScopedAssetAsync`（所有者 `Scene(Event)`）でロード（d4b と同じくキャンセル窓の外）。`OnStabledImpl` で anchor ごとに `Spawn`（所有者 `Scene(Event)`、親なし、時間は gameplay layer）して `Play`。回収は Framework の `Scene(id)` 所有の経路（`PreUnloading` で壊す）に任せ、Scene class で `Stop` を呼ばない。完了の trigger の進入で、祖先の `IInGameSessionServices` を解決して `MajorEvents.Complete(自分の identity)` を 1 回呼ぶ。source のロード失敗は Event の失敗にせず Error を 1 回出し VFX を起動しない（d4b の U2 と同じ）。
- **D5（開始の trigger）:** 新しい `MajorEventStartTrigger`（MonoBehaviour、event の identity を文字列で持つ）を `Spring_Events_4_2` に置く。`CellCompanionScene` が `OnInitialize` で集め、進入で祖先の `IInGameSessionServices.MajorEvents.Request(identity)` を呼ぶ。Scene を跨ぐ参照は event id だけ（§2.2）。Cell Events に置くので Planner の companion set で動く（E5）。
- **D6（authoring）:** Scene・SceneResource・親子 link・Map・Addressables entry・`.vfx`・trigger の配置は、一時の Editor スクリプトで Editor API を使って作り、commit の前に消す（判断基準の既定。World Workspace の transaction に Season Event の kind を足さない。§2.6 U2）。YAML は手編集しない。
- **D7（進入者）:** 開始・完了の trigger の進入者は d4b と同じく `attachedRigidbody` を持つ Collider 全て。在室の追跡はせず、進入ごとに要求を出す（重複は I2 の規則で吸収）。

### 2.6 未決事項

- **U1（分割）:** program §6.3 は「major Event と Planner 実証・harvest の見積もりを分け、大きければ追加分割」とする。見積もり: major Event（本 HANDOFF の範囲）は製品コード 400〜600 行・テスト 600〜900 行・Scene 1 件。Content Directory の Whitebox を使う Editor Play の Planner 実証は、Editor Play 向けの publish 入口が未整備で、local Event・major Event の `.vfx` が Content Directory に入らない（ロードが失敗する）ので、BuildSystem の実装（非 Scene asset の production 経路と Editor Play の publish 入口）を先に要する。program 全体の harvest（Architecture §05 / §18 / §24 / §27 と Streaming 現状仕様）と削除も文書量が大きい。**推奨: d5 を d5a（本 HANDOFF、major Event と companion set の Planner）と d5b（Content Directory の Whitebox の Planner Play と S-4 の harvest・削除）に分け、d5b の前に BuildSystem の上の 2 件を所有する HANDOFF を切る。** 主目的が BuildSystem なので、d5b の実証を BuildSystem の成果の受け入れに使える。
- **U2（authoring の道具）:** 推奨は一時スクリプト（D6）。代案は World Workspace の transaction に `SeasonEvent` kind を足す（journal の int、ownership proof、window、path 予算のテストを含め +150〜250 行）。SampleGame の検証に道具は不要なので既定どおり一時スクリプト。
- **U3（開始の経路）:** 推奨は Cell Events の開始 trigger（D5）。代案はテストだけが `ISeasonMajorEvents.Request` を呼ぶ形（本番の開始経路が無くなり、E5 の「Events の trigger から」を満たせない）。

---

## 3. 責務マップ（A1）

| ファイル | 責務・変更理由 | 所有者・寿命 | 依存・公開面 | テスト境界 | 行数（現在 → 予想） |
|---|---|---|---|---|---|
| 新規 `Streaming/Runtime/Season/SessionMajorEventDriver.cs` | major Event の受理の判定と Add / Unload の発行・個体の状態（ロード中・生存・終了処理中） | controller の枝ごと。枝の破棄で破棄 | `ISceneController`（controller の issued）、`ISceneQuery`、捕捉・登録の callback。internal | 偽 controller / query / terminal events で AC1〜AC5 | 0 → 150〜220 |
| `SessionSeasonController.cs` | driver の生成・破棄、受理の前提（Stable・busy・停止）の受け渡し、`ISeasonMajorEvents` の委譲 | InGameSession | 既存 | `SessionSeasonControllerTests` に AC4・AC5 | 599 → 640〜670 |
| `IInGameSessionServices.cs`・新規 `ISeasonMajorEvents.cs`・`MajorEventRequestResult` | Scene class から Event を要求する口 | — | SampleGame.InGame の public | — | +10、0 → 40 |
| `InGameSessionScene.cs` | `MajorEvents` を controller へ委譲 | InGameSession | 既存 | — | 174 → 190〜200 |
| 新規 `World/SeasonMajorEventScene.cs` | Event Scene の lifecycle: source のロード、anchor の位置で `Scene(Event)` 所有の `Spawn`、完了の通知 | Event Scene | `IVfxSystem`、`ISceneQuery`（祖先の解決） | Play（AC6〜AC10）。準備の論理は小さいので単体は置かない | 0 → 120〜180 |
| 新規 `World/MajorEventVfxAnchor.cs`・`MajorEventCompletionTrigger.cs`・`MajorEventStartTrigger.cs` | Scene に置く marker と trigger。出来事を Scene class へ流すだけ | 各 GameObject | MonoBehaviour | EditMode（配置）、Play | 0 → 各 30〜60 |
| `World/CellCompanionScene.cs` | 開始 trigger の収集と要求 | Cell companion | 既存 + `IInGameSessionServices` | Play（AC9） | 81 → 100〜115 |
| `SeasonCellNames.cs`・`DependOnAll/GameSceneFactory.cs` | 命名の規則と分類 | — | 既存 | EditMode（AC11） | 44 → 60、130 → 150 |
| Scene・asset（`Spring_Event_MultiCellProof.unity`、SceneResource、`.vfx`、Season_Spring の子、Map、Addressables、`Spring_Events_4_2` の開始 trigger） | 実証の素材 | — | — | AC11・AC12 | — |
| テスト（新規 `SessionMajorEventDriverTests`、`SeasonMajorEventPlayTests`、`MajorEventAuthoringTests`、`VfxSceneStructureTests` の 56 → 57） | — | — | — | — | 0 → 800〜1100 |

- **警報:** `SessionSeasonController` は既に 500 行を超える。追加は driver の生成・破棄と委譲だけにし、受理の判定と個体の状態は新しい driver へ置く（変更理由・テスト境界が異なる。companion driver と同じ分け方）。
- **配置:** driver は Season の枝の所有物なので `Streaming/Runtime/Season/` に置く（距離 streaming の語彙ではないが、枝の寿命と同じ）。Scene class と trigger は既存の `World/`（d4b と同じ）。新しい asmdef・依存 edge は無い。

## 4. 実装計画（A1）

### 4.1 順序

1. driver と controller の配線、`ISeasonMajorEvents`、単体テスト（AC1〜AC5）。
2. Scene class・marker・trigger、`SeasonCellNames`・`GameSceneFactory`。
3. 一時スクリプトで Scene・asset を作り、`Spring_Events_4_2` に開始 trigger を置く。一時スクリプトを消す。
4. authoring・Play テスト（AC6〜AC12）。`VfxSceneStructureTests` の件数。
5. `pwsh tools/contract-audit.ps1`、Editor の compile 確認。

### 4.2 Phase B から Phase A へ差し戻す条件

- Framework のコード・公開 API、`SceneState`、`LoadType`、asmdef 参照の変更が要る。
- driver を controller の枝の所有にできない（例: 枝の切替が Event の終端を待てない）。
- `Scene(id)` 所有の回収が `ReleaseScene` より後になる（I4 が Framework の契約の外で破れる）。
- test の `SceneDirector` で `IInGameSessionServices` の祖先を解決できず、Play テストの構成に本番にない composition が要る（§5 の未知の操作経路）。

### 4.3 BuildSystem の既知の課題との関係（判断基準による記録）

- Event Scene は Content Directory の Spring build に Scene payload として入る（AC12）。`.vfx` は入らないので、Content Directory の実行では Event のロードは成功し VFX のロードだけが失敗して Error を出す（d4b と同じ）。これを直す「非 Scene asset の production 経路」と「Editor Play 向けの publish 入口」は BuildSystem の実装の候補で、U1 の推奨では d5b の前提として所有 HANDOFF を切る。
- d5a は Player を build しない。BS4 の Player が Spring に入らない課題は S-9 / BuildSystem の候補のまま（本スライスでは踏まない）。

## 5. テストとレビュー計画（A1）

- **単体テスト:** `SessionMajorEventDriverTests`（AC1〜AC3）、`SessionSeasonControllerTests` の追加（AC4・AC5）。既存の偽 controller / query / terminal events を使う。
- **差し戻し中の起点 `-Filter`:** `SessionMajorEventDriverTests`、`SessionSeasonControllerTests`、`SeasonMajorEventPlayTests`、`MajorEventAuthoringTests`、`VfxSceneStructureTests`、`LocalVfxEventPlayTests`。
- **判定必須テスト:** 最終の全 EditMode 回帰（空 filter）、PlayMode の `SeasonMajorEventPlayTests`・`LocalVfxEventPlayTests`・`VfxScenePlayTests`・Streaming の PlayMode（`-WithGraphics` を併用。§16.7 の `-nographics` の Inconclusive）。
- **全 EditMode 回帰の適用除外:** なし。
- **操作・実行時・目視条件の検証経路:** 全て Play テストで閉じる（AC6〜AC10）。人間の目視は無い。
- **未知の操作経路:** (1) test の `SceneDirector` の Map に `InGameSession` を含めずに、Scene class が `IInGameSessionServices` を解決する方法（`LocalVfxEventPlayTests` は Season を根にした複製 Map で、祖先に `InGameSession` が無い）。候補は、複製 Map の根を `InGameSession` にして Season 以外の子を外す、または test の Map だけに `IInGameSessionServices` を実装する test 用の根の Scene class を factory に足す。凍結の前に、どちらが本番に無い composition を要さずに成り立つかを Editor で小さく確かめる。(2) 完了の trigger の進入を kinematic Rigidbody の `MovePosition` で起こす経路は d4b で成立済み。
- **人間の判断が必要な条件:** なし。
- **高コスト証拠:** なし。
- **判定 C の照合 script の方式:** なし（テスト XML の実行テスト名と件数で判定）。
- **機械検査:** `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- **A0/A1 主担当:** この session の主担当（Anthropic 系）。
- **A2 独立レビュー:** 未実施。
- **C' 用に予約した担当:** OpenAI 系（Phase A では使わない）。

## 6. Phase B 実装結果

未着手。

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
