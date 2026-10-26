# S-4d4 local Event

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `A`（A2 入力版）
- branch: `claude/s-4d4-local-event`
- implementation base commit: `a4e940b1`（`develop`）
- implementation head commit: 未固定
- risk: `high`（公開 API・所有者／寿命・Update 経路・Content Directory の経路に触れる）
- owner: 発注者 / S-4d4 担当
- created: 2026-10-27
- expires: S-4d4 の Phase D 完了時
- harvest to: Architecture §24 §16（VFX 抽象・backend）、§05 §5.13（Cell と職種 companion）、Content Directory の非 Scene asset は BuildSystem 側へ移したので d4 からは harvest しない
- workflow task: `s-4d4-local-event`（`tools/workflow-task.ps1 start` 済み）
- Phase A snapshot の取得案内・状態: 未生成
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票: Phase A 作業中。U1〜U4 は裁定済み。A2 入力版を commit し、A2 の独立レビューを行う。Phase B へは A3 凍結後に新しい session で入る。

---

## 1. 目的と対象外

- **目的:** Cell `Spring (4,2)` の Events Scene `Spring_Events_4_2` に trigger を置き、trigger から VFX を `IVfxSystem` 越しに動的に起動し、Cell の unload で確実に回収されることを実 Scene で示す。あわせて、d2 で発注者が決めた「VFX の時間の所属」（instance が所属する `UpdateLayer` の `IsPaused` / `TimeScale` に再生を追従させる）を入れる。
- **対象外:**
  - 独立 Scene の大型 Event `Spring_Event_MultiCellProof`、Planner 実証の全体、S-4 の harvest と削除（d5）。
  - ParticleSystem fallback（後続の独立スライス、担当未定）。
  - Player での VFX 描画、`CullNone` のコスト、本番 SceneGraph で Season と Cell を同時に載せる経路の VFX（S-9）。
  - 同時数の上限・優先度・間引き・pool / prewarm・DebugStudio での観測（§6.4 で入れないとしたもの）。
  - UpdateSystem の汎用イベント機構や新しい時間管理の仕組み（d4 行で「このためだけに先に作らない」と決定済み）。
- **現況（A0、`a4e940b1` で確認）:** §1.3 にまとめる。

### 1.1 Phase A の判断基準（発注者、2026-10-27。S-4 program §0 から転記）

A1 の各案と A3 の裁定の材料に明記する。

- **主目的は BuildSystem。** いまの主目的はきちんとした BuildSystem を作ることで、VFX（S-4d）はそのつなぎ込みの題材である。各案で、スライスの成果物が content build・Player build の経路に乗るかと、Build 側の既知の課題（BS4 の Player は smoke の後に終了して InGame の Spring に入らない、transport staging の path が予算の外）を踏むかを書く。Build 側の課題は「やらない」で片付けず、BuildSystem の作業の候補として扱う。
- **残すコードは SampleGame に必要なものだけ。** 生成や authoring の道具を残すかは「SampleGame にそのコードが必要か（無いと SampleGame の検証が成り立たないか）」で決める。必要でなければ、一時スクリプトで作り commit の前に消す案を既定にする。「他の季節・Cell でも使える」「再利用できる」は残す理由にしない。
- **checkout のルート。** Player を build するスライスは、ルートが 96 文字以下の checkout で作業する。**この worktree のルートは 97 文字**なので、Player build はここでは行えない（§4.3）。

### 1.2 S-4 program から転記した制作契約

**SceneGraph（§2.1）。**

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
└─ Season_Summer / Autumn / Winter         同型
```

- Cell 配下の全職種 Scene は `StreamByDistance = false`、`LoadType.OnDemand`。親 Cell が Stable になった後、SampleGame の companion loader が選択中の制作プロファイルに応じて明示 `AddScene` する。Unload は親 Cell の再帰破棄へ委ねる。

**職種ごとの所有物（§2.2）。**

- **Cell Events**: trigger、spawn、encounter anchor、局所的な進行制御。
- **Cell VFX**: 霧、煙、流水など、その Cell が常駐中は継続する環境エフェクト。
- VFX Scene にゲーム進行 trigger を置かない。Events Scene が VFX の開始・停止を要求する。
- Scene を跨ぐ `GameObject` の直接参照は禁止し、event id、world position、service query、`IAssetManagement` で寿命を宣言してロードする prefab で接続する。

**Event Scene の粒度（§2.3）。** 次のいずれにも該当しないイベントは `*_Events_{x}_{y}` に含める。複数 Cell に効果が及ぶ／起点 Cell の Unload 後も継続する／専用 Timeline・Cinemachine staging を独立ファイルとして所有したい／別担当が Cell bundle と独立して変更する規模。いずれかに該当するときだけ `Season_Event_{slug}`（d5）とする。

**VFX の粒度と寿命（§2.4）。**

- Cell 常駐中ずっと必要な複数エフェクトは、1 Cell 1 VFX Scene に束ねる（d3 済み）。
- ヒット、破裂、短い演出など一時的な VFX は Scene にせず prefab / `VisualEffectAsset` にする。
- 一時 VFX は `IAssetManagement` でロードし、`AssetOwner.Scene(eventSceneIdentity)` または `AssetOwner.Bind(go)` で寿命を宣言する。

**物理フォルダ（§2.5）。** `Seasons/Spring/Cells/Spring_Cell_4_2/Spring_Events_4_2/Spring_Events_4_2.unity`。

**Companion profile（§3.2）。** AppConfig key `world:cellCompanionSet`。`Full`: Environment + Lighting + VFX + Events／`Planner`: Events のみ／`Lighting`: Environment + Lighting／`VFX`: Environment + Lighting + VFX。既定は `Full`、不明な値は設定エラー。`SessionCellCompanionLoadDriver` はロード済み Cell の `SceneResource.Children` を列挙し、親 identity から名前を組み立てない。role 判定は SampleGame の制作規約に閉じ、Framework は職種を知らない。role identity はロード対象選別だけに使い、座標・bounds・辞書 key の導出には使わない。

**World Workspace の作成（§3.4）。** 存在しない Lighting / VFX / Events を作る操作は、`.unity`、`SceneResource`、親子 link、Map、Addressables entry を同じ Editor transaction で作る。Scene / asset YAML は直接編集しない。Planner role の作成は Events（`WorldCompanionCreationPlan`）。

**VFX Graph（§6.1）。** `com.unity.visualeffectgraph` `17.6.0`（Unity 6000.6.0f1 同梱、URP `17.6.0`）。PC Quality で描画を必須受入、Mobile は起動時に backend が対応可否を返し非対応なら `VisualEffect` を有効化しない。診断ログは Framework が一度だけ出す。

**実証 Scene（§6.2、d4 の分）。** `Spring_Events_4_2` は Cell 内 trigger から local VFX を起動する。Planner Play は `content:representation=Whitebox` と `world:cellCompanionSet=Planner` を使う（Planner 実証の全体は d5）。

**§6.3 の d4 行（A0 入力。凍結済みの条件ではない）。**

- 答える問い: Cell 内の trigger から VFX を起動し、Cell unload で確実に回収できるか。
- 範囲: `Spring_Events_4_2`。trigger と、d1 の handle を `AssetOwner.Scene`（`Spring_Events_4_2` の identity）または `AssetOwner.Bind(go)` 所有で起動する経路。Events Scene は再帰破棄で親 Cell と一緒に Unload される。Planner profile は Events だけを読むため、trigger は `Spring_VFX_4_2` のロードに依存しない。
- d1 から: `Bind(go)` 用 notifier の `OnDestroy` が実 Scene unload と go の Destroy で回収を呼ぶことの Play Mode 確認。source asset のロード / `Spawn` / 所有者の終端 / source の Release の前後関係が見える実例。
- d1 の C / C' から（採否は d4 の Phase A）: 同じ identity の Scene の再ロード後に `Spawn` が再び受理されることの実 `TestableSceneDirector` での1往復の確認、`BindOwner_DestroyedWithoutNotification_ReleasedOnNextTick` が AC8 より厳しく Tick 前の `Destroy`=0 を判定していること、`VfxSystemOwnerTests.cs`（828 行）の分割。
- d2 の Phase A から（2026-10-25 発注者決定、必須検討）: VFX の時間の所属。instance が所属する `UpdateLayer` の `IsPaused` / `TimeScale` に再生を追従させる。VfxSystem にまとめたまま instance に所属 layer を持たせ、instance を VfxSystem の外に出さない。`Spawn` での宣言（`VfxSpawnRequest` / `IVfxBackend` への追加）、backend への反映（`pause` / `playRate`）、止まっている間の消え切り待ちの timeout の数え方、配置済み VFX の所属を決める。
- 2026-10-25 追加レビュー（必須検討）: (1) `UpdateLayer` は `IsPaused` の間その layer の要素を呼ばない（`TryCreateExecutionContext` が false）ので、止まっている間も pause の停止・再開を反映できる経路を選ぶ。`TimeScale` の変更が再生中の instance に反映される条件も固定する。(2) `Time.timeScale`、VFX Graph の `Ignore Time Scale`、`VisualEffect.playRate`、layer の `TimeScale` の合成規則（二重に効く・無視されることが無いかを小さい fixture で観測。`Ignore Time Scale` の規約を fixture と配置済み VFX で揃えるか）。(3) `TimeScale == 0` と `IsPaused` を同一視するか、消え切り待ちの timeout の時間基準。(4) 動的 VFX と配置済み VFX が同じ規則に従うか。d3 の配置済み VFX に変更が要るか。確認は「再生中に Pause → 停止 → Resume → 再開」の最小の実動作でよい。
- 対象外: 独立 Scene の大型 Event（d5）。

**VFX 基盤の責務配置（§6.4、2026-10-17 発注者決定）。**

- 抽象は Framework、VFX Graph に依存する実装は SampleGame。
- Framework の責務: (a) 寿命の宣言と回収（`AssetOwner` のスコープ。Framework が回収するのは再生中の instance で、asset の寿命は `IAssetManagement` に残す。所有者の unload は消え切り待ちの Stop を打ち切って回収する）、(b) 非対応環境での停止（何もしない handle と診断ログ 1 回）、(c) 型に依存しない handle（Play、即時 Stop、消え切りを待つ Stop、名前付きパラメータ。`VisualEffect` 型を公開面に出さない）。
- source asset の寿命: `Spawn` は呼び出し側がロード済みの `UnityEngine.Object` を受け取り、Framework は asset handle を保持・Release しない。呼び出し側は、その source から作った instance が回収されるまで source を有効に保つ。asset と VFX に同じ `AssetOwner` を使っても解放順は保証されない。
- 回収の保証範囲: Framework が保証するのは、所有者の終端の後に backend へ破棄を要求する時点と回数。backend 自身の破棄失敗からの回復は backend の契約。
- d1 の寿命管理の対象は動的に起動する VFX（d4 / d5）。

**公開 API と依存（§10）。** Framework 公開面の追加は限定する。VFX の公開型は `IVfxSystem`、`VfxSystem`、`IVfxHandle`、`VfxSpawnRequest`、`IVfxBackend`、`VfxInstanceId`（namespace `OneStarMaker.Runtime.Rendering.Vfx`）、d2 で `AbstractApplicationInitializer.CreateVfxBackend()` と `UpdateLayerIds.Vfx` / `VfxLayerOrder`。**Game が `IVfxSystem` を受け取る経路は d4 で決める。** Framework は season 名・Cell 座標文法・職種 role を知らない。VFX Graph 依存は SampleGame に閉じる。`SceneState` の14値は変更しない。新しい LoadType も追加しない。

**検証方針（§11 S-4d のうち d4）。** local Event は Cell unload で回収される。Mobile 非対応時に例外、未解放 handle、無限 retry がない。658 Scene を個別に目視しない。`unity test` / `unity run`、Cloud での Unity CLI、Scene / asset YAML 手編集は禁止。Phase B は Editor 操作・compile 確認まで、Unity テストと Build は Phase C。

**スライス境界（§12）。** 本書（S-4 program）の §6.3 / §6.4 に無い公開 API、依存、所有者、寿命は d4 の Phase A で決め、program の該当行も Phase A の PR で更新する。Phase B で決めない。Phase D は Phase A で選んだ既存の Architecture 文書へ harvest し、この HANDOFF を削除し、program の §6.3 d4 行を完了に更新する。

### 1.3 A0 の現況（`a4e940b1`）

**VFX 抽象（Architecture §24 §16 が正）。**

- `VfxSystem`（`OneStarMaker/Scripts/Runtime/Rendering/Vfx/Implements/VfxSystem.cs`、512 行）。`Spawn(in VfxSpawnRequest, AssetOwner)`、`Tick(float)`、`BindSceneLifecycle(Observable<SceneEvent>, ISceneQuery)`、`Dispose`。handle の状態は `Created → Playing → Stopping → Released`、回収は `Release` の 1 か所。`Scene(id)` 所有は `SceneDirector` の `StateChanged(PreUnloading | LoadCanceled)` で Unity Scene unload より前に回収、`Bind(go)` は `VfxOwnerDestroyNotifier.OnDestroy`（同期）か、通知が届かない破棄なら go が `== null` になった後の最初の `Tick`。
- `VfxRuntimeHost`（internal、228 行）が `AbstractApplicationInitializer` の中で `VfxSystem` を所有する。`BeforeSceneLoad` で生成、Tick は layer `UpdateLayerIds.Vfx`（order 55）に即時 active で登録し、**unscaled delta** を渡す（`Time.timeScale = 0` でも消え切り待ちの timeout が出口になるため）。
- **Game は `IVfxSystem` を受け取れない。** `AbstractApplicationInitializer` は `_vfxRuntimeHost` を private に持ち、Game 向けの accessor は無い（`IAssetManagement` は `protected IAssetManagement? AssetManagement` で公開されている）。
- `VfxSpawnRequest` は `Source`・`Position`・`Rotation`・`HasParent`・`Parent` だけで、時間の所属を持たない。`IVfxBackend` は `QueryCapability / Create / Play / StopEmitting / HasAliveParticles / Set* / Destroy`。
- 実 `SceneDirector`（`TestableSceneDirector`）での VFX テストは `VfxSystemSceneDirectorTests`（293 行、5 件: unload 前の回収、親 unload で子所有の回収、PreLoad 中の cancel、OnLoaded 失敗、Destroy の例外）が既にある。**同じ identity の再ロード後に `Spawn` が再び受理されることのテストは無い。**
- `VfxSystemOwnerTests.cs` は 828 行。

**UpdateSystem。**

- `UpdateLayer.IsPaused { get; set; }`、`TimeScale { get; private set; }` と `SetTimeScale(float)`。`TryCreateExecutionContext` は `IsPaused` なら false を返し、その layer の要素の Update / LateUpdate を呼ばない。そうでなければ `deltaTime * TimeScale` を渡す。**layer の状態変化の通知は無い。**
- `UpdateCoordinator` に layer を作らずに引く API は無い（`GetOrCreateLayer` だけ）。
- 現在 SampleGame に layer を pause / time scale する呼び出しは無い。

**SampleGame の Scene と配線。**

- `Spring_Cell_4_2` の子に Environment / Lighting / VFX（d3）がある。**`Spring_Events_4_2` は未作成**（`.unity`・`SceneResource` とも無い）。World Workspace の Planner role の作成 transaction（`WorldCompanionCreationPlan`）で作れる。
- `GameSceneFactory`（`SampleGame/DependOnAll`）は、親が `StreamByDistance` の子を全て `CellCompanionScene`（何もしない no-UI scene）にする。`IRenderEnvironment` は ctor 注入で `SeasonLightingScene` へ渡している（Game の service を Scene class へ渡す既存の形）。
- `SceneBase.Initialize(GameObject[] roots)` が RootGameObjects を受け取り `OnInitialize` を呼ぶ。`OnLoadedImpl` 等の非同期 hook がある。
- Player は `FlyController`（`[RequireComponent(typeof(Rigidbody))]`）。物理 trigger（`Collider.isTrigger` + `OnTriggerEnter`）で検出できる。
- Scene の unload の順序（`SceneDirector.Unloading`）: `PreUnloading`（Scene 所有の VFX を回収）→ Unity Scene unload（Scene 内の go の `OnDestroy` → `Bind` 所有の VFX を回収）→ `ReleaseScene(identity)`（`AssetOwner.Scene(identity)` の asset を解放）→ `Removed`。

**BuildSystem との接点（主目的の観点）。**

- Content Directory の production 候補は **Scene payload だけ**（`ScenePayloadMappingPolicy`）。Scene 以外の asset の候補を作る production 経路は無く、`LoadContentAssetAsync<T>(logicalKey, representation, owner)` が引ける非 Scene の entry は BS4 の test fixture（`Bs4FixtureComposer`、`bs4:fixture:prefab`）だけ。
- したがって、Events Scene が `IAssetManagement` で動的にロードする `VisualEffectAsset` は、**現状の Content Directory には入らない**。Scene から直接参照された asset なら Scene の依存として同じ build に入る。
- BS4 Spring Player は Title の smoke の後に `Application.Quit(0)` し、InGame の Spring に入らない（S-9 へ送り済み）。この worktree のルートは 97 文字で、Player build の path 予算（96 以下）を満たさない。

---

## 2. 意思決定と受け入れ境界（A1）

- **このスライスが答える問い:** Cell の Events Scene に置いた trigger から `IVfxSystem` 越しに VFX を起動し、Cell の unload（と所有 go の破棄）で確実に回収できるか。あわせて、動的・配置済みの全ての VFX が、属する時間の領域（gameplay layer）の Pause / Resume / TimeScale に同じ規則で追従するか。

### 2.1 不変条件（起きてはいけないこと）

- **I1 回収漏れなし:** `Spring_Events_4_2` の unload（親 Cell の再帰破棄を含む）の後、その Scene の trigger から起動した instance の backend `Destroy` が instance ごとに高々 1 回で、`Removed` の時点で全て 1 回。
- **I2 source より先に instance:** trigger の VFX の source asset が解放（`ReleaseScene(Spring_Events_4_2)`）されるより前に、その source から作った全 instance の `Destroy` が呼ばれている。
- **I3 止まった時間で進まない:** 所属する時間の領域が止まっている（所属 layer が `IsPaused`）間、その領域に属する VFX の再生が進まない。**動的に起動したものと配置済みのものの両方**に適用する。layer が止まって要素が呼ばれないことが、反映を止めない。
- **I4 待ち時間は effect 自身の時間:** 消え切り待ちの timeout は、その effect が実際に進んだ時間（所属領域の時間）で数える。止まっている間は進まない。所有者の終端は時間と無関係に即回収する。
- **I5 配置済み VFX を壊さない:** `VfxSystem` に登録した配置済み VFX の回収は、登録を外して止めるだけで、Scene が所有する GameObject を破棄しない（GameObject の寿命は Scene のまま）。
- **I6 Planner で VFX Scene に依存しない:** `world:cellCompanionSet=Planner`（Events のみ）で、trigger の起動が `Spring_VFX_4_2` のロードを要さず、例外を出さない。
- **I7 非対応環境:** 非対応の `VfxSystem` では、trigger の発火・配置済み VFX の登録のどちらでも、例外・backend 呼び出し・追加の診断ログが無く、配置済みの `VisualEffect` は 1 つも有効にならない（d3 の不変条件を保つ）。
- **I8 常時契約:** `SceneState` 14 値不変、Game → Framework の一方向、`VisualEffect` 型を Framework の公開面に出さない、asmdef 参照の追加 0 件、1 つの UpdateSystem の例外で他の Tick を止めない。

### 2.2 成立条件（起きなければならないこと）

- **E1 trigger で起動:** Player（`Rigidbody`）が `Spring_Events_4_2` の trigger に入ると、trigger の位置に VFX instance が 1 つ作られ再生される（PC、Editor Play）。
- **E2 Cell unload で回収:** 親 Cell `Spring_Cell_4_2` の unload で、E1 の instance が I1・I2 の順序で回収される。
- **E3 go の Destroy で回収:** trigger の go を Play 中に `Destroy` すると、その go に `Bind` した instance が `OnDestroy` の時点で回収される（Tick を待たない）。
- **E4 再ロード後の再受理:** 同じ identity の Events Scene を unload → 再ロードした後、trigger から再び起動できる（実 `SceneDirector` の 1 往復）。
- **E5 Pause → Resume（動的）:** gameplay layer を再生中に `IsPaused = true` にすると trigger の VFX の再生が止まり、`false` に戻すと再開する（実 `VisualEffect`、PC）。
- **E6 Pause → Resume（配置済み）:** 同じ操作で `Spring_VFX_4_2` / `Spring_AtmosphereVFX` の配置済み VFX も止まり、再開する。
- **E7 TimeScale の反映:** gameplay layer の `SetTimeScale` が、再生中の VFX（動的・配置済み）の再生速度に次の `VfxSystem.Tick` で反映される。
- **E8 Events Scene が content build に乗る:** `Tools/OSM/Content/Build Spring Full`（と Whitebox）の Content Directory に `Spring_Events_4_2` が Scene payload として入る。trigger の VFX asset を build に乗せることは d4 では扱わない（U1 の裁定、§2.5）。

### 2.3 受け入れ条件（A1。検証する条件を併記）

| # | 内容 | 条件 | 失敗経路 |
|---|---|---|---|
| AC1 | 実 SceneDirector で Cell 4_2 + Events 4_2 を載せ、trigger を発火 → backend `Create` 1・`Play` 1 | E1 | |
| AC2 | AC1 の後に Cell を unload。記録した順序が `Destroy(instance)` → `ReleaseScene(Spring_Events_4_2)`、`Removed` 時点で `Destroy` 回数 = 1 | E2, I1, I2 | ○ |
| AC3 | trigger の go を Destroy → 同じフレーム内に `Destroy` 1（Tick を挟まない） | E3 | |
| AC4 | Events を unload → 同じ identity を再ロード → trigger 発火で `Create` が再び受理される | E4 | |
| AC5 | 偽 backend・偽 layer source: 所属 layer の Pause / Resume / SetTimeScale が、次の Tick で `SetPlayback` を変化時だけ 1 回呼ぶ。layer が Pause の間も `Vfx` layer の Tick は呼ばれ、Resume を反映する | E5, E7, I3 | ○（反映を所属 layer の Update に置いた実装はこのテストで落ちる） |
| AC6 | 偽 backend: `Create` 直後・`Play` 前に現在の領域の状態を 1 回書く（止まった領域で起動した instance は止まった状態で始まる） | I3 | ○ |
| AC7 | 偽 backend: Stopping 中に所属 layer を Pause すると timeout の待ち時間が進まない。Resume で倍率に応じて進む。Pause 中でも所有者の終端で即 `Destroy` | I4 | ○ |
| AC8 | 偽 backend: 配置済みの登録（`Adopt`）は backend `Adopt` 1 回、所有 go の破棄で `Destroy` 1 回。`Destroy` の後も go は生きている（偽 backend が「破棄しない」種別として記録） | I5 | ○ |
| AC9 | 実 `VisualEffect`（`-WithGraphics`）: 再生中に Pause → 一定フレーム後も粒子の時間が進まない → Resume → 進む。動的・配置済みの両方 | E5, E6, I3 | |
| AC10 | 実 `VisualEffect`: `Time.timeScale`・layer `TimeScale` がともに 1 以外のとき、VFX の進む時間が gameplay layer の要素が受け取る scaled delta の和と一致する（二重・無視が無い） | E7、必須検討 (2) | ○ |
| AC11 | Planner profile（Events のみ）で Cell 4_2 を載せ trigger 発火 → 例外なし、`Spring_VFX_4_2` 未ロードのまま `Create` 1 | I6 | ○ |
| AC12 | 非対応の `VfxSystem` で AC1 と配置済み Scene のロード → 例外 0、backend 呼び出し 0、診断ログは起動時の 1 件だけ、有効な `VisualEffect` 0 | I7 | ○ |
| AC13 | `PlacedVfxAuthoringTests` を新しい登録方式に合わせて更新し、全ての配置済み `VisualEffect` が無効で保存され、登録の marker を持つことを確認 | I5, I7 | ○ |
| AC14 | EditMode の authoring 検査: `Spring_Events_4_2` に trigger が 1 つ以上あり、`VisualEffect` を置かず、Collider が trigger、他 Scene の go を参照しない | §2.2 契約 | ○ |
| AC15 | Content Directory の build 結果に `Spring_Events_4_2` が Scene payload として入る | E8 | |
| AC16 | `contract-audit` 通過、asmdef の diff 0、`SceneState` の diff 0 | I8 | ○ |

- **ここでは答えない問いと所有する後続スライス:** 大型 Event と Planner 実証の全体（S-4d5）。ParticleSystem fallback（後続の独立スライス、担当未定）。Player での描画・Spring に入る Player 経路（S-9。BuildSystem の候補として §4.3 に記録）。Content Directory の非 Scene asset 候補の production 経路と、その実装・確認（trigger の VFX asset を content build・Player build に乗せることを含む）は BuildSystem の実装系へ移す（U1 の裁定。所有 HANDOFF は未作成）。gameplay のコード（Player の移動など）を gameplay layer へ移して、ゲームの一時停止を実際に作ること（所有スライス未定。d4 は時間の領域の契約と VFX 側の追従だけを作る）。
- **判定定義:** GO = §2.1・§2.2 の全条件を AC で確認できた。NO-GO = いずれかの不変条件の違反、または成立条件の不成立。CONDITIONAL ACCEPT は使わない（スパイクではない）。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- **A3 後の例外承認:** なし
- **決定記録:** なし

### 2.4 A1 の設計

**原則（発注者、2026-10-27。U2・U3 の裁定）。** VFX の時間は「どの世界に属するか」で決め、作り方（動的に起動したか、Scene に配置したか）では分けない。

1. 全ての VFX は時間の領域を 1 つ持つ。既定は gameplay の世界の時間で、それ以外（UI 演出など unscaled）は明示して外れる。
2. 時間の領域ごとに時計は 1 つで、VFX への反映は `VfxSystem` の 1 か所で行う。`Time.timeScale`・layer の `TimeScale`・`playRate` の合成は 1 か所で定義し、gameplay のコードが見る時間と VFX が見る時間を一致させる。
3. effect の待ち時間（消え切り待ちの timeout）は、その effect 自身の時間で数える。実時間での出口は timer ではなく所有者の終端が持つ。
4. `IsPaused`（状態）と `TimeScale == 0`（倍率）は区別する。

**D1. Game が `IVfxSystem` を受け取る経路。** `AbstractApplicationInitializer` に `protected IVfxSystem? VfxSystem`（`AssetManagement` と同じ形。`BeforeSceneLoad` の後は非 null）を足す。`AppInitializer.CreateSceneFactory` が `GameSceneFactory` の ctor へ渡し、`GameSceneFactory` が Scene class へ ctor で渡す（`IRenderEnvironment` と同じ形。service locator にしない）。

**D2. 時間の領域 = gameplay layer。** Framework の `UpdateLayerIds` に `Gameplay`（と order 定数）を足す。「ゲームの世界の時間」という一般の概念で、季節・職種など SampleGame の語は入れない。

- 時間の領域の実体は `UpdateLayer` の `IsPaused` / `TimeScale` とする（2026-10-25 の発注者決定「instance が所属する `UpdateLayer` に追従」と両立させる）。gameplay のコードが同じ layer で動けば、コードと VFX が同じ時間で止まり・進む。
- `Vfx` layer は時間の領域にしない（VFX の反映そのものを担うので止めない。§16.5 に契約として書く）。所属先に `Vfx` を指定したら引数の誤り（`ArgumentException`）。
- order は A2 で決める（既存: Input -100、Camera 50、Vfx 55、Streaming 60）。

**D3. 宣言と反映。**

- **宣言:** `VfxSpawnRequest` に `TimeLayerId`（既定 = `UpdateLayerIds.Gameplay`）と、明示して外れる `WithUnscaledTime()` を足す。既存の ctor の引数は変えず、既定値が gameplay になる。
- **反映の経路:** 所属 layer の Update には置かない。`Vfx` layer の `VfxSystem.Tick` が、生存中の全 instance（動的・配置済み）について所属 layer の `IsPaused` / `TimeScale` を読み、前回 backend へ書いた値と違うときだけ `IVfxBackend.SetPlayback(id, bool paused, float playRate)` を 1 回呼ぶ。`Create` / `Adopt` の直後にも 1 回書く。layer の値を読む口は internal の `IVfxTimeLayerSource`（host が `UpdateCoordinator` の新しい `TryGetLayer(string, out UpdateLayer)` で実装。layer を作らない）。layer がまだ無い id は「止まっていない、倍率 1」。
- **合成規則:** layer の要素が受け取る scaled delta は `Time.deltaTime`（`Time.timeScale` 込み）× layer `TimeScale`。VFX は「`Time.timeScale` に従う（`Ignore Time Scale` を使わない）× `playRate` = layer `TimeScale`」で同じ積にする。unscaled の VFX は `Ignore Time Scale` を使う側の規約になるので、unscaled の指定と `.vfx` の設定が食い違う場合の扱いは A2 で決める。d3 の `.vfx` 4 件は `m_UpdateMode: 0`（読みでは固定 delta・`Ignore Time Scale` なし。未確認）で、固定 delta の更新と `pause` / `playRate` の合成は Phase C の最初の確認に入れる（AC10）。
- **timeout:** 各 instance の消え切り待ちは、その instance の領域の時間（gameplay なら layer の scaled delta 相当 = unscaled × `Time.timeScale` × (paused ? 0 : layer TimeScale)、unscaled なら unscaled delta）で進める。`Time.timeScale = 0` や layer の Pause の間は進まない。出口は所有者の終端（Scene / Bind は unload・破棄、App / Manual は `VfxSystem.Dispose`）。d2 が unscaled を選んだ理由（`timeScale = 0` で出口が無くなる）は、所有者の終端を出口にすることで置き換える。§16.5 の該当文を書き換える。
- **配置**: 反映と timeout の時間基準は、`VfxSystem`（512 行）とは変わる理由が違う（UpdateSystem 側の仕様に追従して変わる）ので、internal の `VfxTimeLayerBinding`（Implements/）に分ける。

**D4. 配置済み VFX の登録（Adopt）。**

- `IVfxSystem.Adopt(UnityEngine.Object placed, AssetOwner owner, string? timeLayerId = Gameplay)` → `IVfxHandle`。`placed` は配置済みの VFX の component（Framework は型を知らない。`Source` と同じ `UnityEngine.Object`）。owner は配置した go の `Bind(go)`。
- `IVfxBackend.Adopt(UnityEngine.Object placed)` → `VfxInstanceId`。backend が有効化（`VisualEffect.enabled = true`、初期イベントで自動再生）を行い、以後は動的な instance と同じ id で `SetPlayback` を受ける。`Destroy(id)` は adopted な id では「無効にして登録を外すだけ」で GameObject を壊さない（I5）。種別は backend が id ごとに覚える。
- handle の状態は `Playing` で始まる。`Stop` / `Dispose` は即時の回収（= 無効化と登録解除）。`StopAllowingParticlesToFinish` は動的と同じ。
- **非対応環境:** `VfxSystem` は `Adopt` で backend を呼ばず何もしない handle を返すので、有効化は起きない（I7）。これで配置済み VFX の有効化の判断も `VfxSystem` の能力判定 1 つに集まり、`PlacedVfxActivator` が `VfxCapabilityState` を直接読む経路は無くなる。`VfxCapabilityState` を残すか（backend 内部の判定結果の共有だけになる）は A2 で決める。
- **誰が登録するか:** 配置済み VFX を持つ Scene の class が、`OnInitialize` で root から marker（`PlacedVfxActivator` を marker と所属 layer の設定に変える。名前の変更は A2 で決める）を集めて `Adopt` する。Cell VFX は `CellCompanionScene`（role が VFX のものに `IVfxSystem` を渡す）、Season Atmosphere は `SeasonAtmosphereVfxScene`。marker の `Awake` は何もしない。`VisualEffect` は従来どおり無効で保存する。
- **時点:** d2 は「Component が有効化される前に止める」を求めた。無効で保存して Scene class が後から有効にする方式は、それを満たしたまま有効化の時点を `Initialize`（Unity Scene のロード直後）へ移す。`Awake` より遅れるので、Scene のロードから初回描画までに 1 フレーム以上の差が出うる。許容するかは A2 で確認する。

**D5. Events Scene と trigger。**

- SampleGame に `CellEventsScene : SceneBase` を足す。`GameSceneFactory` は親が `StreamByDistance` の子のうち role が Events のもの（既存の `CellCompanionRoleClassifier` の規則）を `CellEventsScene` にする。
- `CellEventsScene` は `OnInitialize` で root から `LocalVfxEventTrigger` を集め、`OnLoadedImpl` で trigger の VFX asset を `IAssetManagement.LoadAssetAsync` で `AssetOwner.Scene(自分の identity)` 所有でロードしてから trigger を有効にする。trigger が発火したら `IVfxSystem.Spawn(request, AssetOwner.Bind(trigger の go))`（時間の領域は既定の gameplay）→ `Play`。
- 所有者: asset は `Scene(Events の identity)`、VFX は `Bind(trigger の go)`。§1.3 の unload 順序（PreUnloading → Unity Scene unload で `OnDestroy` → `ReleaseScene`）により「VFX の回収 → asset の解放」が構造的に保たれる。
- trigger 退出: `StopAllowingParticlesToFinish`。再入: 前の handle が残っていれば即 `Stop` してから新しく起動する（同時 1 instance / trigger）。
- **U4（裁定）:** trigger の VFX は gameplay layer に属する（既定値のまま）。

**D6. d1 の C / C' からの入力の採否案。**

- 再ロード後の再受理: 採用（AC4、`VfxSystemSceneDirectorTests` に 1 件足す）。
- `BindOwner_DestroyedWithoutNotification_ReleasedOnNextTick` の Tick 前 `Destroy`=0: 不採用案。契約（§16.3）は「通知が届かない破棄は次の Tick」で、Tick 前に回収しないことはその契約の確認として妥当。
- `VfxSystemOwnerTests.cs` の分割: 不採用案。d4 はこのファイルに追加しない（時間は `VfxSystemTimeLayerTests.cs`、登録は `VfxSystemAdoptTests.cs`）。

**採らなかった案（A2 で比較してよい）。** layer の Update から `VisualEffect.Simulate` で VFX を進める方式は、時間の出所が layer の delta 1 つになり合成の問題が消える。一方、毎フレーム呼ぶ用途で使えるか・描画や性能への影響が未確認なので、AC10 が成り立たなかったときの代替として控えに残す。

### 2.5 未決事項

**U1. trigger の VFX asset を build にどう乗せるか — 裁定済み（発注者、2026-10-27）。** d4 の計画から外す。Content Directory に Scene 以外の asset の production 候補経路を入れることと、その実装・確認（trigger の VFX asset が content build・Player build に乗ること）は BuildSystem の実装系の所属にする。d4 は §2.4 の契約どおり `IAssetManagement` で asset をロードし（Editor Play と test の経路）、Content Directory 上で trigger の VFX が起動することは d4 の条件にしない。背景: 現状の Content Directory は Scene payload しか候補にしない（§1.3）。

**U2・U3 — 裁定済み（発注者、2026-10-27）。** 配置済み VFX も `VfxSystem` に登録して同じ規則に乗せる。timeout は effect 自身の時間で数える（§2.4 の原則と D3・D4）。初稿の「配置済みは所属なし・d3 を変えない」「所属があるときだけ timeout を止める」は、作り方で時間の規則を分ける部分最適として取り下げた。

**U4 — 裁定済み（発注者、2026-10-27）。** gameplay layer を新設し、trigger の VFX と配置済みの環境 VFX はそこに属する（D2・D5）。

A2 で決める残り: gameplay layer の order、unscaled 指定と `.vfx` の `Ignore Time Scale` の食い違いの扱い、`VfxCapabilityState` の存続、marker の名前、有効化の時点が `Awake` から `Initialize` へ遅れることの許容。

---

## 3. 責務マップ（A1）

| ファイル | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `OneStarMaker/Scripts/Runtime/UpdateSystem/Layers/UpdateLayerIds.cs` | `Gameplay` と order 定数 | 時間の領域の定義 | 定数 | なし | 追加 2 | — | +6 |
| `OneStarMaker/Scripts/Foundation/UpdateSystem/World/UpdateCoordinator.cs` | `TryGetLayer(string, out UpdateLayer)`（作らずに引く） | 副作用なしの参照 | — | — | 追加 1 | `UpdateCoordinatorTests` | +12 |
| `.../Rendering/Vfx/Abstractions/VfxSpawnRequest.cs` | 時間の領域の宣言（既定 gameplay、`WithUnscaledTime`） | 時間の宣言 | 値型 | `UpdateLayerIds` | `TimeLayerId`、`WithUnscaledTime` | 単体 | 55 → 80 |
| `.../Abstractions/IVfxSystem.cs` | `Adopt` | 配置済みの登録 | — | — | 追加 1 | — | 24 → 35 |
| `.../Abstractions/IVfxBackend.cs` | `SetPlayback`、`Adopt`、adopted の `Destroy` の意味 | 反映と登録 | — | — | 追加 2 | 偽 backend | 52 → 75 |
| `.../Implements/VfxTimeLayerBinding.cs`（新規、internal） | instance ごとの領域の値の読取りと差分反映、timeout の時間基準 | UpdateSystem 側の仕様への追従 | `VfxSystem` 寿命 | `IVfxTimeLayerSource`（internal） | なし | 偽 source・偽 backend で単体 | 0 → 130 |
| `.../Implements/VfxSystem.cs` | `Adopt` の受理規則、binding の呼び出し、`Vfx` layer 指定の拒否 | 受理規則 | App | — | `Adopt` | 既存 + 新規 | 512 → 560（500 超は既存。非分割理由: 増分は `Adopt` の受理規則（既存の `Spawn` と同じ判断）と binding の呼び出しで、時間の規則は binding へ分けた。A2 で再確認） |
| `.../Implements/VfxHandle.cs` | 所属・最後に書いた値・adopted の種別 | — | handle 寿命 | — | internal | 単体 | 251 → 285 |
| `.../Implements/VfxRuntimeHost.cs` | `IVfxTimeLayerSource` を coordinator から作る、Tick に unscaled と scaled の両方を渡す | 配線 | App | `UpdateCoordinator` | internal | `VfxRuntimeHostTests` | 228 → 255 |
| `.../Implements/UnconfiguredVfxBackend.cs` | 追加 IF の空実装 | IF 追加 | — | — | — | — | 51 → 60 |
| `OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | `protected IVfxSystem? VfxSystem` | Game への経路 | App | — | 追加 1 | 既存 bootstrap テスト | 1241 → 1250（既存の大きさ。増分 1 property で非分割） |
| `SampleGame/Common/Scripts/Rendering/Vfx/VfxGraphBackend.cs` | `SetPlayback` → `pause` / `playRate`、`Adopt`（有効化）、adopted の `Destroy` = 無効化と登録解除 | 実 backend | backend | `UnityEngine.VFX` | — | Play（`-WithGraphics`） | 342 → 400 |
| `SampleGame/Common/Scripts/Rendering/Vfx/PlacedVfxActivator.cs` | 有効化をやめ、配置済み VFX の marker と所属 layer の設定にする | 登録方式の変更 | Scene 内の go | なし | serialized field | authoring EditMode | 38 → 30 |
| `SampleGame/Common/Scripts/Rendering/Vfx/VfxCapabilityState.cs` | 存続または縮小（A2） | 判定の出所の一本化 | — | — | — | — | 50 → ? |
| `SampleGame/InGame/InGameSession/World/CellCompanionScene.cs`・`SeasonAtmosphereVfxScene.cs` | 配置済み VFX の marker を集めて `Adopt` | 登録 | Scene | `IVfxSystem` | — | 実 SceneDirector の Play テスト | 38 → 70 / 44 → 75 |
| `SampleGame/InGame/InGameSession/World/CellEventsScene.cs`（新規） | Events Scene の lifecycle。trigger の収集、asset のロード（Scene 所有）、trigger → Spawn | Events の制作契約 | Scene(Events identity) | `IVfxSystem`、`IAssetManagement`、logger | なし | 実 SceneDirector の Play テスト | 0 → 150 |
| `SampleGame/InGame/InGameSession/World/LocalVfxEventTrigger.cs`（新規、MonoBehaviour） | trigger の Collider、asset の key、発火の通知 | authoring の面 | Scene 内の go | なし（通知は callback） | serialized field | authoring EditMode | 0 → 80 |
| `SampleGame/DependOnAll/GameSceneFactory.cs`・`AppInitializer.cs` | Events role の分岐、`IVfxSystem` / `IAssetManagement` の注入 | 配線 | — | — | — | 既存 | +25 / +5 |
| `SampleGame/Tests/Rendering/Vfx/PlacedVfxAuthoringTests.cs` | 新しい登録方式の検査に更新 | 登録方式の変更 | — | — | — | — | 219 → 230 |
| `Seasons/Spring/Cells/Spring_Cell_4_2/Spring_Events_4_2/`（新規 Scene と trigger の VFX asset） | 実証 Scene | — | — | — | — | — | Editor transaction で作る |

asmdef 参照の追加は 0 件の見込み（`SampleGame.InGame` は `OneStarMaker.Runtime` を参照済み）。公開 API の追加は `UpdateLayerIds.Gameplay`（+ order）、`UpdateCoordinator.TryGetLayer`、`VfxSpawnRequest.TimeLayerId` / `WithUnscaledTime`、`IVfxSystem.Adopt`、`IVfxBackend.SetPlayback` / `Adopt`、`AbstractApplicationInitializer.VfxSystem`。S-4 program §10 の該当行を Phase A の PR で更新する。

---

## 4. 実装計画（A1）

### 4.1 順序

1. Framework: `UpdateLayerIds.Gameplay`、`TryGetLayer`、`VfxSpawnRequest` / `IVfxSystem` / `IVfxBackend` の追加、`VfxTimeLayerBinding`、`Adopt`、`VfxSystem` / host の配線、`protected VfxSystem`。偽 backend の単体テスト。
2. SampleGame backend: `VfxGraphBackend.SetPlayback` / `Adopt` / adopted の `Destroy`。
3. 配置済み VFX: marker への変更、`CellCompanionScene` / `SeasonAtmosphereVfxScene` の `Adopt`、`PlacedVfxAuthoringTests` の更新。d3 の Scene と `.vfx` は component の設定以外を変えない。
4. SampleGame Events: `LocalVfxEventTrigger`、`CellEventsScene`、`GameSceneFactory` の分岐。
5. Editor 操作: World Workspace の Planner role で `Spring_Events_4_2` を作る（transaction）。trigger を置き、VFX asset を同じフォルダに置く。Scene / asset YAML は直接編集しない。
6. Play テストと authoring テスト。

### 4.2 Phase B から Phase A へ差し戻す条件

- §3 に無い公開 API・状態・依存・所有者・寿命が要る。
- asmdef 参照の追加が要る。
- `VisualEffect.pause` / `playRate` が AC9・AC10 の振る舞いを示さず、反映の経路（D3）を変える必要がある。
- adopted の `VisualEffect` を無効化・再有効化したときに初期イベントの再送などで I5 の意味が変わる。

### 4.3 BuildSystem の既知の課題との関係（判断基準による記録）

- **content build:** Events Scene は World Workspace の transaction で Addressables entry と Map に入り、`Build Spring Full / Whitebox` の Content Directory に Scene payload として入る（d3 の VFX Scene と同じ経路）。trigger の VFX asset は Content Directory に入らない。これは BuildSystem の実装系へ移した（U1）。
- **Player build:** d4 では行わない。理由は (1) BS4 の Player は smoke の後に終了して Spring に入らないので、Player 上で trigger を踏む観測経路が無い（S-9 へ送り済みの課題を踏む）、(2) この worktree のルートが 97 文字で予算外。どちらも BuildSystem の作業候補として記録する（「Spring に入る Player の観測経路」と「Player build を行う checkout の置き場」）。

---

## 5. テストとレビュー計画（A1）

- **単体テスト（EditMode、Unity なしの論理）:** `VfxSystemTimeLayerTests.cs`（新規。AC5〜AC7、`Vfx` layer 指定の拒否、未作成 layer の扱い）、`VfxSystemAdoptTests.cs`（新規。AC8、AC12 の登録側）、`UpdateCoordinatorTests` に `TryGetLayer`、`VfxSystemSceneDirectorTests` に AC4。
- **Play テスト（`-WithGraphics`）:** `VfxGraphBackendPlayTests` に AC9（動的）・AC10、`VfxScenePlayTests` に AC9（配置済み）。新規 `CellEventsScenePlayTests`（AC1〜AC3、AC11）は d3 と同じ test 用 `SceneDirector`（実 `SceneResource` の複製の Map）で Cell 4_2 を根に載せる。trigger の発火は `Rigidbody` を持つ test 用の go を trigger 内へ移動させて物理で起こす（`OnTriggerEnter` 経路を通す）。
- **authoring（EditMode）:** AC13（`PlacedVfxAuthoringTests` の更新）、AC14。
- **差し戻し中の起点 `-Filter`:** `OneStarMaker.Tests.Rendering.Vfx`、`SampleGame.Tests.Rendering.Vfx`、`UpdateCoordinatorTests`、新規の Events テスト。
- **判定必須テスト:** 全 EditMode 回帰（空 filter）、上記 Play テスト（`-WithGraphics`）、`PlacedVfxAuthoringTests`（AC13）、Content Directory の build（AC15、Phase C）。
- **全 EditMode 回帰の適用除外:** なし
- **操作・実行時・目視条件の検証経路:** E1〜E7 は Play テストで閉じる（構造・値の検査）。見た目の判断は求めない。AC9・AC10 の「粒子の時間が進まない」は画素ではなく `VisualEffect` の観測値（粒子数・simulation の時間）で判定する。詳細な観測値の選定は A2 で実行可能性を確認する。
- **未知の操作経路:** `VisualEffect.pause` 中の粒子の時間と、`playRate` × `Time.timeScale` の観測方法。未確認。初回確認は Phase C（Unity テストが要るため）。不成立なら AC9・AC10 の観測値を変える Phase A revision とする。
- **人間の判断が必要な条件:** なし
- **高コスト証拠:** なし（Player build を行わない）
- **判定 C の照合 script の方式:** なし
- **機械検査:** `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- **A0/A1 主担当:** この session（モデル名は §5 の実績欄にだけ記録する）
- **A2 独立レビュー:** 未実施。予定: (1) アーキテクチャゲート（依存・所有者・寿命・公開面）、(2) 時間の所属の反映経路と不変条件の反例探し、(3) A0 だけから代替構成を出すレビュー（高リスクのため）。C' 用に 1 系列を予約する。
- **A3 統合:** 未実施

## 6. Phase B 実装結果

未着手。

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
