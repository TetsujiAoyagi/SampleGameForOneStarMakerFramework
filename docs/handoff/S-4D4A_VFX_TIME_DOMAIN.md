# S-4d4a VFX の時間の領域と配置済み VFX の登録

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `A`（revision 2 の A1 案。revision 1 は A3 凍結済みで、差分は §2.7）
- branch: `claude/s-4d4a-vfx-time-domain`
- implementation base commit: `a4e940b1`（`develop`）
- implementation head commit: 未固定
- risk: `high`（Framework の公開 API・所有者／寿命・Update の経路を変える）
- owner: 発注者 / S-4d4a 担当
- created: 2026-10-27
- expires: S-4d4a の Phase D 完了時
- harvest to: Architecture §24 §16（VFX 抽象・backend・配置済み VFX）、UpdateSystem の layer の一覧（`UpdateLayerIds` を記述している既存文書。Phase D で場所を確かめる）
- workflow task: `s-4d4-local-event`（`tools/workflow-task.ps1 start` 済み。分割前の id をそのまま使う）
- Phase A snapshot の取得案内・状態: 凍結前スパイクの原記録は `evidence stage` で固定済み（ローカル固定済み（未昇格）、task `s-4d4-local-event`）。取得案内は主担当の worktree の `TestResults/` にあり、次の担当への引渡しは未完了。凍結した HANDOFF 本体は A3 凍結 commit で固定する
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - **2026-10-27 更新:** Phase B は中断。§6.1 の疑いはスパイクで事実と確認され（§6.1.1）、A 再開に分類した。revision 2 の A1 案は §2.7（§6.1.1 の未決は主担当が R1〜R3 で決めた）。次は A2（差分だけの独立レビュー）→ A3（発注者の承認で凍結）→ Phase B の再開（中断 commit を起点に R1〜R3 を足す）。B のコードは本ファイルを含む中断 commit にあり（`git log -1 -- docs/handoff/S-4D4A_VFX_TIME_DOMAIN.md`）、revision 2 の凍結後に B を再開するときの起点にする。Editor 操作・コンパイル確認は未了（§6.5）。以下の項目は revision 1 の B 開始時のもの。
  - 次の Phase・役割: Phase B（実装担当）。新しい session で始める。§4.1 の順（時間制御 → 配置済み VFX）で、§3 の責務マップの範囲だけを実装する。
  - 凍結した Phase A: revision 1。凍結 commit は本ファイルを含む A3 凍結 commit（`git log -1 -- docs/handoff/S-4D4A_VFX_TIME_DOMAIN.md` で引く）。implementation base は `a4e940b1`。引継ぎの開始時に、凍結 commit と自分の worktree の HEAD を照合し、作業ツリーが dirty なら引き継がない。
  - 入力: 本 HANDOFF（凍結版）。凍結前スパイクの原記録は Phase A snapshot の欄のとおり（Phase B では読まなくてよい。§1.4 が要約）。
  - 必要な権限: Unity Editor の操作（`.vfx` の Update Mode の変更、d3 の Scene の marker 設定の確認）とコンパイル確認。Unity テストと Build は Phase C（H2 未適用のため Phase B では実行しない）。
  - 停止規則: §4.2 に当たったら実装を止めて Phase A へ返す。新しい事実は「新しい事実の分類」に従う。
  - 引き渡さないもの: A2・外部レビューの指摘の原文（採否は §5.3 にある）。

### 0.1 分割（発注者、2026-10-27）

S-4d4 を 2 スライスに分けた。順序は `d4a -> d4b`。d4b は d4a のマージ後に S-4 program §0 の手順で着手する。

- **d4a（本書）:** VFX の時間の領域（gameplay layer への追従）と、配置済み VFX の `VfxSystem` への登録。Framework の時間の契約と、d3 の配置済み VFX の変更。
- **d4b:** local Event。`Spring_Events_4_2`、trigger、Cell unload での回収。d4b へ送る事項は §10 にまとめる。

分割の理由: 変更する理由が 3 つ（Framework の時間の契約、配置済み VFX の登録方式、Events の制作契約）あり、1 PR では C' の監査範囲が広すぎる。外部レビューの「時間制御 → 配置済み VFX → Local Event の順に小さく進める」とも一致する。

---

## 1. 目的と対象外

- **目的:** 動的に起動した VFX と Scene に配置した VFX の全てが、属する時間の領域（gameplay layer）の Pause / Resume / 時間の倍率に同じ規則で追従するようにする。配置済み VFX も `VfxSystem` に登録し、能力判定・時間・回収を 1 つの経路にまとめる。
- **対象外:**
  - local Event（Events Scene、trigger、Cell unload での動的 VFX の回収）は d4b。
  - 独立 Scene の大型 Event、Planner 実証の全体、S-4 の harvest と削除（d5）。
  - ParticleSystem fallback（後続の独立スライス、担当未定）。
  - Player での VFX 描画、`CullNone` のコスト、本番 SceneGraph で Season と Cell を同時に載せる経路の VFX（S-9）。
  - 同時数の上限・優先度・間引き・pool / prewarm・DebugStudio での観測。
  - UpdateSystem の汎用イベント機構や新しい時間管理の仕組み、新しいキャンセル機構（d4 行と外部レビューで「作らない」と決定済み）。
  - gameplay のコード（Player の移動など）を gameplay layer へ移して、ゲームの一時停止を実際に作ること（所有スライス未定）。
  - 既存 `VfxSystem.Tick` 全体の確保の最適化（外部レビュー Medium 4）。
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
- 実 `SceneDirector`（`TestableSceneDirector`）での VFX テストは `VfxSystemSceneDirectorTests`（293 行、5 件: unload 前の回収、親 unload で子所有の回収、PreLoad 中の cancel、OnLoaded 失敗、Destroy の例外）が既にある。同じ identity の再ロード後に `Spawn` が再び受理されることは、合成 `Subject` では `VfxSystemOwnerTests.AfterRemoved_StillRejected_UntilPreLoading_AndOldHandleDoesNotRevive` が確認しているが、**実 `SceneDirector` の往復のテストは無い。**
- `VfxSystemOwnerTests.cs` は 828 行。

**UpdateSystem。**

- `UpdateLayer.IsPaused { get; set; }`、`TimeScale { get; private set; }` と `SetTimeScale(float)`。`TryCreateExecutionContext` は `IsPaused` なら false を返し、その layer の要素の Update / LateUpdate を呼ばない。そうでなければ `deltaTime * TimeScale` を渡す。**layer の状態変化の通知は無い。**
- `UpdateCoordinator` に layer を作らずに引く API は無い（`GetOrCreateLayer` だけ）。
- 現在 SampleGame に layer を pause / time scale する呼び出しは無い。
- `UpdateBehaviourAdapter` の既定 layer は文字列 `"Gameplay"`・order 0（定数化されていない）。`GetOrCreateLayer` は同じ id に別の order を渡すと例外。

**SampleGame の Scene と配線。**

- `Spring_Cell_4_2` の子に Environment / Lighting / VFX（d3）がある。**`Spring_Events_4_2` は未作成**（`.unity`・`SceneResource` とも無い）。World Workspace の Planner role の作成 transaction（`WorldCompanionCreationPlan`）で作れる。
- `GameSceneFactory`（`SampleGame/DependOnAll`）は、親が `StreamByDistance` の子を全て `CellCompanionScene`（何もしない no-UI scene）にする。`IRenderEnvironment` は ctor 注入で `SeasonLightingScene` へ渡している（Game の service を Scene class へ渡す既存の形）。
- `SceneBase.Initialize(GameObject[] roots)` が RootGameObjects を受け取り `OnInitialize` を呼ぶ。`OnLoadedImpl` 等の非同期 hook がある。`SceneBase` は SceneDirector が注入する `Assets`（`IAssetManagement`）と `LoadSceneScopedAssetAsync<T>(AssetKey)`（`AssetOwner.Scene(identity)` 所有）を既に持つ。
- `CellCompanionRoleClassifier` と `CellCompanionRole` は InGame の internal で、`SampleGame.DependOnAll` からは呼べない。
- VFX Graph は `VisualEffect.enabled` の切り替えと `Reinit` で初期イベントを再送する（d2 の実測。`VfxGraphBackend` の注記）。d3 の `.vfx` は初期イベント `OnPlay`、`m_UpdateMode: 0`。
- Player は `FlyController`（`[RequireComponent(typeof(Rigidbody))]`）。物理 trigger（`Collider.isTrigger` + `OnTriggerEnter`）で検出できる。
- Scene の unload の順序（`SceneDirector.Unloading`）: `PreUnloading`（Scene 所有の VFX を回収）→ Unity Scene unload（Scene 内の go の `OnDestroy` → `Bind` 所有の VFX を回収）→ `ReleaseScene(identity)`（`AssetOwner.Scene(identity)` の asset を解放）→ `Removed`。

**BuildSystem との接点（主目的の観点）。**

- Content Directory の production 候補は **Scene payload だけ**（`ScenePayloadMappingPolicy`）。Scene 以外の asset の候補を作る production 経路は無く、`LoadContentAssetAsync<T>(logicalKey, representation, owner)` が引ける非 Scene の entry は BS4 の test fixture（`Bs4FixtureComposer`、`bs4:fixture:prefab`）だけ。
- したがって、Events Scene が `IAssetManagement` で動的にロードする `VisualEffectAsset` は、**現状の Content Directory には入らない**。Scene から直接参照された asset なら Scene の依存として同じ build に入る。
- BS4 Spring Player は Title の smoke の後に `Application.Quit(0)` し、InGame の Spring に入らない（S-9 へ送り済み）。この worktree のルートは 97 文字で、Player build の path 予算（96 以下）を満たさない。

### 1.4 実測（凍結前スパイク、2026-10-27）

§5.1 のスパイクの観測。原記録は Artifact Storage に固定した（§0 の Phase A snapshot の欄）。条件: Unity 6000.6.0f1、Direct3D11、batchmode、graphics あり、カメラなし。使い捨てブランチ（base `a4e940b1`、スパイクのテストの commit `84d44922`、未 push）。`run-tests.ps1 -WithGraphics` で 6 件実行（記録用で合否は持たない）。設定値: `Time.maximumDeltaTime = 0.333`、`VFXManager.maxDeltaTime = 0.05`、`VFXManager.fixedTimeStep = 1/60`。fixture は Constant Rate 64/s・capacity 68・`OnPlay`。

**観測（事実）。**

- **S1 初期の有効化:** 無効で保存した `VisualEffect` を有効にすると、同じフレームの `PostLateUpdate.VFXUpdate` で spawner が Playing / Looping になり、reap しない fixture では同じフレームに粒子が出た。有効化の前に書いた `pause = true`・`playRate = 0.5` は有効化の後も保たれた。`pause = true` では spawner は Playing になったが simulation の時間は 0 のままで、`pause = false` のフレームから進んだ。
- **S2 反映の時点:** VFX の更新は PlayerLoop の `PostLateUpdate.VFXUpdate` の 1 つだけ。全 LateUpdate の後（VFXUpdate の前）に書いた `pause = true` も `playRate = 4` も、同じフレームの VFXUpdate に効いた（測ったのは PlayerLoop の `PreLateUpdate.ScriptRunBehaviourLateUpdate` の直後への差し込みで、MonoBehaviour の LateUpdate そのものではない）。
- **S3 進む時間:** Update Mode 0（Fixed、既存の fixture と d3 の `.vfx`）は `Time.timeScale` と `playRate` の両方に比例した。mode 2（Fixed + Ignore Time Scale）と mode 3（Delta + Ignore Time Scale）は `Time.timeScale` に依らず `playRate` にだけ比例した。mode 0・2 は 1/60 の刻みで進み（短いフレームは 0、長いフレームは 1/30）、mode 3 は各フレームの実時間に追従した。500 ms の負荷スパイクの次のフレームは 3 つとも `Time.deltaTime = 0.333`（頭打ち）、`unscaledDeltaTime = 0.517`（頭打ちなし）、VFX の 1 フレームの進み = 0.05（`VFXManager.maxDeltaTime` で頭打ち）。spawn 数 ÷ spawn rate は spawn system の `totalTime` と一致した。
- **S4 rate 0:** `playRate = 0` は受理され、ログは出ない。rate 0 のまま有効化すると spawner は Playing になるが時間は進まない。後から `playRate = 1` にすると、`Play` を再送しなくても同じフレームから spawn と simulation が始まった。再生中に 0 にすると止まり、1 に戻すと続きから進んだ。
- **S5 読み戻し:** reap しない system の `aliveParticleCount` は spawn と同じフレームで変わった。reap する system では約 60 フレームに 1 回しか変わらなかった（Stop の後、34 フレーム後に変化した例が 1 件）。
- **S6 再有効化:** `enabled` と `SetActive` のどちらの false → true でも、粒子が消え、時間が 1/60 から出直した。

**設計への反映（主担当の判断。観測とは分ける）。**

- D3 の Update Mode は **Delta Time + Ignore Time Scale（mode 3）** にする。`Time.timeScale` を VFX 内部で効かせず（mode 0 は二重に効く）、1/60 の刻みにもならない（mode 2 はフレームごとの一致が取れない）。
- `VfxGraphBackend` の換算: VFX の 1 フレームの進みは `min(unscaledDeltaTime, VFXManager.maxDeltaTime) × playRate`（S3 の観測。スパイクのフレームで `playRate` を掛けた値は未測定）なので、`playRate = 領域の時間 ÷ min(unscaledDeltaTime, VFXManager.maxDeltaTime)`（= rate × unscaled ÷ min(...)）とする。分母が 0・異常なら前回の `playRate` を保つ。スパイクのフレームで `playRate` が大きくなる（例: 0.333 ÷ 0.05 ≈ 6.7）。
- D4（開始の契約）は Unity の標準の挙動だけで成り立つ。S2 で LateUpdate の反映が同じフレームに効き、S4 で rate 0 からの再開に `Play` の再送が要らない。S1 から「pause したまま有効化しても進まない」ことも分かったが、D4 は保留の方式のまま維持する（§2.5 の採らなかった案）。
- I4（再有効化しない）の根拠を S6 で再確認した。
- AC6〜AC8 の観測値は reap しない fixture の `aliveParticleCount` か spawn system の `totalTime` を使う。reap する system の `aliveParticleCount` はフレーム単位の判定に使わない。

**未確認（初回確認は Phase C）。** カメラがある条件、スパイクのフレームで `playRate > 1` を掛けたときの進み（AC7 のスパイク列が確認地点）、MonoBehaviour の LateUpdate そのものからの書き込み（`Vfx` layer の反映点は `UpdaterDriver.LateUpdate` の中で、測った差し込み点より前。AC6 が確認地点）。不成立なら §4.2 の差し戻し条件に当たる。

---

## 2. 意思決定と受け入れ境界

- **このスライスが答える問い:** 動的に起動した VFX と Scene に配置した VFX の全てが、属する時間の領域（gameplay layer）の Pause / Resume / 時間の倍率に、負荷スパイクを含めて gameplay のコードと同じ時間で追従し、止まっている間は開始もしないか。配置済み VFX を `VfxSystem` に登録しても、Scene の寿命と非対応環境での停止が保たれるか。

### 2.1 時間の定義（本書の条件はこの定義で読む）

- **領域の時間（domain delta）:** あるフレームに、その領域の layer の要素が受け取る delta。gameplay の領域では `layer.IsPaused ? 0 : Time.deltaTime × layer.TimeScale`（`UpdaterDriver` が渡す `Time.deltaTime` は `Time.maximumDeltaTime` で頭打ちになる。その値をそのまま使う）。unscaled の領域では `min(Time.unscaledDeltaTime, Time.maximumDeltaTime)`（revision 2。VFX Graph は 1 フレームを上限より先へ進めないので、Unity の頭打ちと同じ上限で切る。§2.7）。
- **effect の時間:** その effect の simulation が実際に進んだ時間。
- **一致:** 任意の連続したフレーム列（`maximumDeltaTime` を超える負荷スパイクのフレームを含む）で、effect の時間の和が領域の時間の和と許容誤差内で等しい。
- **反映点:** `Vfx` layer の LateUpdate。`VfxSystem` はここで領域の状態を backend に書く。
- **開始:** 動的な VFX では `Play` による発射、配置済み VFX では最初の有効化（初期イベントで自動再生される）。

### 2.2 不変条件（起きてはいけないこと）

- **I1 止まった時間で進まない:** あるフレームの反映点で所属 layer が `IsPaused` だった場合、そのフレームの VFX の simulation は進まない。動的・配置済みの両方。layer が止まって要素が呼ばれないことが、反映を止めない。反映点より後に変えた値は次のフレームから効く（遅れは最大 1 フレーム）。
- **I2 止まった時間で開始しない:** 領域が止まっている間は、動的な VFX の `Play` も配置済み VFX の有効化も backend に届かない。粒子は 1 つも出ない。開始は、領域が止まっていない最初の反映点で、そのフレームの状態を書いた後に届く。
- **I3 待ち時間は effect 自身の時間:** 消え切り待ちの timeout は領域の時間で数える。止まっている間は進まない。意図して unscaled で数えるのは unscaled の領域だけ（§2.1 の unscaled の領域の時間で数える）。所有者の終端は時間と無関係に即回収する。
- **I4 配置済み VFX を壊さない:** 配置済み VFX の回収は無効化と登録解除だけで、Scene が所有する GameObject を破棄しない。回収後に `VfxSystem` が同じ component を再び有効化することは無い（回収は終端）。同じ component の二重登録は 2 件目を拒否する。
- **I5 非対応環境:** 非対応の `VfxSystem` では、配置済み VFX の登録と動的 VFX の起動のどちらでも、例外・backend 呼び出し（起動時の `QueryCapability` 1 回を除く）・追加の診断ログ（起動時の 1 件を除く）が無く、配置済みの `VisualEffect` は 1 つも有効にならない。
- **I6 常時契約:** `SceneState` 14 値不変、Game → Framework の一方向、`VisualEffect` 型を Framework の公開面にも SampleGame の InGame にも出さない（Common に閉じる）、asmdef 参照の追加 0 件、1 つの UpdateSystem の例外で他の Tick を止めない。

### 2.3 成立条件（起きなければならないこと）

- **E1 Pause → Resume（動的）:** gameplay layer を再生中に `IsPaused = true` にすると動的な VFX の再生が止まり、`false` に戻すと再開する（実 `VisualEffect`、PC）。
- **E2 Pause → Resume（配置済み）:** 同じ操作で `Spring_VFX_4_2` / `Spring_AtmosphereVFX` の配置済み VFX も止まり、再開する。
- **E3 時間の一致:** gameplay の領域に属する VFX の effect の時間が、§2.1 の意味で領域の時間と一致する（`Time.timeScale` と layer の `TimeScale` が 1 以外、かつ負荷スパイクのフレームを含む）。
- **E4 配置済み VFX の自動再生を保つ:** 領域が止まっていなければ、配置済み VFX は Scene のロード後に従来どおり自動で再生される（d3 の振る舞い）。止まっていれば Resume の反映点で始まる。

### 2.4 受け入れ条件（検証する条件を併記）

| # | 内容 | 条件 | 失敗経路 |
|---|---|---|---|
| AC1 | 偽 backend・偽 time source: 反映点ごとに、各 instance の (paused, rate) が handle の前回の値と違うときだけ `SetPlayback` を 1 回呼ぶ。反映点の間に Pause → Resume して値が戻った場合は 0 回で合格。所属 layer が Pause の間も反映点は呼ばれ、Resume を反映する | I1, E1 | ○（反映を所属 layer の Update に置いた実装は落ちる） |
| AC2 | 偽 backend: 止まった領域で `Play`（動的）と `Adopt`（配置済み）をすると、backend `Play` は 0 回のまま。Resume の最初の反映点で `SetPlayback` → `Play` の順に各 1 回。保留中に `Stop` したら backend `Play` 0 回のまま回収（動的は `Destroy`、配置済みは `Release`） | I2, E4 | ○ |
| AC3 | 偽 backend: Stopping 中の待ち時間は、反映点ごとに領域の時間（gameplay は `Time.deltaTime × layer.TimeScale`、Pause 中は 0、unscaled は `min(unscaledDeltaTime, maximumDeltaTime)`）だけ進む。`Time.deltaTime` を `maximumDeltaTime` で頭打ちにした値を与えた場合もその値で進む。Pause 中でも所有者の終端で即回収 | I3 | ○ |
| AC4 | 偽 backend: 配置済みの登録（所有者は配置した Scene の `Scene(id)`）は backend `Adopt` 1 回。所有 Scene の `PreUnloading` で backend `Release` 1 回・`Destroy` 0 回、その時点で go は破棄されていない。handle の `Stop` でも `Release` 1 回で、その後に `Play` が呼ばれない。同じ component の 2 回目の `Adopt` は backend を呼ばず何もしない handle | I4 | ○ |
| AC5 | 偽 time source: `unscaledDeltaTime` が 0・負・NaN・∞ のフレームは、rate だけ前回の値を保ち、例外を出さない。paused はその反映点の所属 layer の状態を使い、(paused, rate) の組が前回と違えば `SetPlayback` を 1 回呼ぶ。異常 delta のフレームに Pause / Resume が重なった場合は、rate が前回の値のまま・paused が新しい値で 1 回呼ばれる。状態の変化が無ければ 0 回 | E3, I1 | ○ |
| AC6 | 実 `VisualEffect`（`-WithGraphics`）: 一定の spawn rate で寿命の無い粒子を出す fixture で、再生中に Pause → N フレームの間 `aliveParticleCount` が増えない → Resume → 増える。動的と配置済みの**両方**が必須。fixture は reap しないもの（既存 `VfxImmortal` と同じ設定で Update Mode だけ D3 の規約）を使い、N = 30、除くフレームは 0（§1.4 の S5: reap しない system の `aliveParticleCount` は同じフレームで変わる） | E1, E2, I1 | |
| AC7 | 実 `VisualEffect`: gameplay layer に test 用の要素を 1 つ登録し、(`Time.timeScale`, layer `TimeScale`) = (0.5, 0.5)・(1, 2)・(2, 0.5) のそれぞれで、その要素が受け取った `DeltaTime` の和と、VFX の進んだ時間（spawn system の `totalTime` の増分。capacity に達する前なら spawn 数 ÷ spawn rate と一致する。§1.4 の S3）が許容誤差内で一致する。加えて、`maximumDeltaTime` を超える負荷スパイクのフレーム（test が重い処理で 1 フレームを引き延ばす。`Thread.Sleep` は使わず、計算で待つ）を含む列でも一致する。revision 2: 頭打ちにならないが `VFXManager.maxDeltaTime` の既定（0.05）を超えるフレーム（u ≈ 0.1 と 0.2）を含む列でも一致する。0 は使わない | E3 | ○（常に rate = 1 の実装、単純な積の実装、VFX 側の上限を揃えない実装のどれかで落ちる） |
| AC8 | 実 `VisualEffect`: 止まった領域で配置済み VFX の Scene をロード → Resume まで `aliveParticleCount` が 0 → Resume の後に増える | I2, E4 | ○ |
| AC9 | 非対応の `VfxSystem` で配置済み Scene のロードと動的 `Spawn` → 例外 0、起動時の `QueryCapability` 以外の backend 呼び出し 0、診断ログは起動時の 1 件だけ、有効な `VisualEffect` 0 | I5 | ○ |
| AC10 | authoring（EditMode）: `Seasons/` 配下の全ての配置済み `VisualEffect` が無効で保存され、同じ GameObject に marker（`PlacedVfxActivator`）を持ち、`visualEffectAsset` が null でない。全ての `.vfx` が §2.5 D3 の Update Mode の規約に従う | I4, I5, E3 | ○ |
| AC11 | `contract-audit` 通過、asmdef の diff 0、`SceneState` の diff 0、`SampleGame/InGame` に `UnityEngine.VFX` の using 0 | I6 | ○ |
| AC12 | 確保 0 の範囲は**新しく足す反映処理だけ**: 生存 instance があり値が変わらないフレームで、反映処理が確保 0。既存 `Tick` の `SnapshotAll` 等は対象外（外部レビュー Medium 4） | §16.5 の既存方針 | ○ |

- **ここでは答えない問いと所有する後続スライス:** local Event と Cell unload での動的 VFX の回収（S-4d4b、§10）。大型 Event と Planner 実証の全体（S-4d5）。ParticleSystem fallback（後続の独立スライス、担当未定）。Player での描画・Spring に入る Player 経路（S-9。BuildSystem の候補として §4.3 に記録）。Content Directory の非 Scene asset 候補の production 経路と、その実装・確認は BuildSystem の実装系へ移す（U1 の裁定。所有 HANDOFF は未作成）。gameplay のコードを gameplay layer へ移して一時停止を実際に作ること（所有スライス未定）。Prefab で動的に置いた配置済み VFX の登録（`Bind(go)` 所有の `Adopt`）。
- **判定定義:** GO = §2.2・§2.3 の全条件を AC で確認できた。NO-GO = いずれかの不変条件の違反、または成立条件の不成立。CONDITIONAL ACCEPT は使わない。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- **A3 後の例外承認:** なし
- **決定記録:** なし

### 2.5 設計

**原則（発注者、2026-10-27。U2・U3 の裁定）。** VFX の時間は「どの世界に属するか」で決め、作り方（動的に起動したか、Scene に配置したか）では分けない。

1. 全ての VFX は時間の領域を 1 つ持つ。既定は gameplay の世界の時間で、それ以外（UI 演出など unscaled）は明示して外れる。
2. 時間の領域ごとに時計は 1 つで、VFX への反映は `VfxSystem` の 1 か所で行う。gameplay のコードが見る時間と VFX が見る時間を一致させる（§2.1）。
3. effect の待ち時間は、その effect 自身の時間で数える。実時間での出口は timer ではなく所有者の終端が持つ。
4. `IsPaused`（状態）と `TimeScale == 0`（倍率）は区別する。

**原則（発注者、2026-10-27。外部レビュー 1 巡目への追加方針）。** Unity（と将来の他の backend）の標準的な初期状態・有効化の挙動をまず確認し、それに逆らう不自然な初期化の細工を避ける。望ましい状態遷移は Framework が担う。backend 間で揃えるのは観測できる Framework の契約で、Unity 固有の初期化手順の共通化ではない。

**D1. Game が `IVfxSystem` を受け取る経路。** `AbstractApplicationInitializer` に `protected IVfxSystem? Vfx`（`AssetManagement` と同じ形。`BeforeSceneLoad` の後は非 null と XML doc に書く。型名 `VfxSystem` と紛れないよう property 名は `Vfx`）を足す。`AppInitializer.CreateSceneFactory` が `GameSceneFactory` の ctor へ渡し、`GameSceneFactory` が `CellCompanionScene` と `SeasonAtmosphereVfxScene` の ctor へ渡す（`IRenderEnvironment` と同じ形。service locator にしない）。

**D2. 時間の領域 = gameplay layer。** Framework の `UpdateLayerIds` に `Gameplay = "Gameplay"` と `GameplayLayerOrder = 0` を足す。`UpdateBehaviourAdapter` の既定（文字列 `"Gameplay"`、order 0）と同じ値を定数にするだけで、既存の layer と同一になる（adapter はこの定数を使うよう直す）。順序は Input(-100) < Gameplay(0) < Camera(50) < Vfx(55) < Streaming(60)。

- **作り手:** `VfxRuntimeHost` の生成時に Framework が `GetOrCreateLayer(Gameplay, GameplayLayerOrder)` で作り、存在を保証する。
- 時間の領域の実体は `UpdateLayer` の `IsPaused` / `TimeScale`（2026-10-25 の発注者決定と両立）。
- `Vfx` layer は時間の領域にしない（反映を担うので pause / time scale しない。§16.5 に契約として書く）。所属先に `Vfx` を指定したら引数の誤り（`ArgumentException`）。

**D3. 宣言・時間・反映。**

- **宣言:** `VfxSpawnRequest` に `TimeLayerId`（既定 = `UpdateLayerIds.Gameplay`）と `WithUnscaledTime()` を足す。既存の ctor の引数は変えない。
- **Framework が backend に渡す値:** 反映点ごとに instance の `paused`（所属 layer の `IsPaused`）と `rate`（= その領域の時間 ÷ `Time.unscaledDeltaTime`。「このフレーム、effect が実時間に対して何倍で進むべきか」）。`unscaledDeltaTime` が 0・負・NaN・∞ なら rate だけ前回の値を保つ。rate の計算ができないことと pause の反映は分け、paused は常にその反映点の所属 layer の状態を使う（外部レビュー 2 巡目 指摘 1）。backend へは (paused, rate) の組が前回書いた組と違うときに書く。負荷スパイクのフレームでは `Time.deltaTime` が頭打ちになるので rate が下がり、gameplay と同じ量だけ進む（§2.1 の一致。外部レビュー High 1）。
- **backend の責任:** 渡された rate のとおりに effect を進める。backend 自身の delta の扱い（VFX Graph の delta の上限や更新方式）への補正は backend が持ち、Framework は Unity の事情を知らない。`VfxGraphBackend` の換算は `playRate = rate × unscaledDeltaTime ÷ min(unscaledDeltaTime, VFXManager.maxDeltaTime)`（§1.4）。revision 2: backend は VFX Graph の 1 フレームの上限を `Time.maximumDeltaTime` に揃える（§2.7）。揃えないと、上限と頭打ちの間のフレームで rate が変わらず換算が書き直されない（§6.1）。
- **`.vfx` の Update Mode の規約:** VFX Graph が `Time.timeScale` を内部で効かせると Framework の rate と二重になるので、全ての `.vfx` を **Delta Time + Ignore Time Scale** にする（§1.4 の S3 で決めた。Fixed は 1/60 の刻みでフレームごとに一致しない）。d3 の `.vfx` 2 件と test fixture の `.vfx` は Update Mode の設定だけを変える（Editor 操作）。
- **反映点:** `Vfx` layer の LateUpdate。Update 中のどの layer で pause / time scale を変えても、同じフレームの simulation の前に反映する（VFX Graph の simulation が LateUpdate より後に走ることは §5.1 の S2 で実測する）。
- **値の出所:** internal の `IVfxTimeSource`（`Time.deltaTime`・`Time.unscaledDeltaTime` と、layer id → (IsPaused, TimeScale) を返す）。host が `UnityEngine.Time` と `UpdateCoordinator` の新しい `TryGetLayer(string, out UpdateLayer)`（layer を作らない）で実装する。`VfxSystem` / binding は `UnityEngine.Time` を直接読まない。layer がまだ無い id は「止まっていない、倍率 1」。`Tick(float)` の引数は変えない。
- **timeout:** 各 instance の消え切り待ちは、反映点ごとに領域の時間だけ進める（I3）。出口は所有者の終端（Scene / Bind は unload・破棄、App / Manual は `VfxSystem.Dispose`）。**App / Manual 所有の消え切り待ちは、時間が止まり続けると `Dispose` まで残る**ことを契約に書く。d2 が unscaled を選んだ理由（`timeScale = 0` で出口が無くなる）は所有者の終端で置き換え、§16.5 の該当文と、それを前提にした既存テストを書き換える。
- **状態の置き場:** 前回書いた (paused, rate)、領域、開始の保留は `VfxHandle` が持つ。`VfxTimeLayerBinding`（internal、Implements/）は値を持たない計算（rate、差分、timeout の増分）だけを担う。

**D4. 開始の契約（外部レビュー High 2）。**

- Framework が保証すること: **領域が止まっている間は開始を backend に届けない。開始は、領域が止まっていない最初の反映点で、そのフレームの (paused, rate) を書いた後に届ける**（I2）。順序は常に「登録 → 状態の反映 → 開始」。
- 動的: `IVfxHandle.Play()` は受理して true を返す。止まっていれば handle に開始の保留を立て、Resume の反映点で backend `Play` を呼ぶ。保留中の `Stop` / `Dispose` は backend `Play` を呼ばずに回収する。
- 配置済み: `Adopt` は登録だけで開始しない。配置済み VFX は「自動で開始するもの」として、Framework が最初の止まっていない反映点で backend `Play` を呼ぶ。backend の `Play` は、adopted の id では最初の 1 回が有効化（`VisualEffect.enabled = true` で初期イベントが送られる。Unity の標準の挙動のまま）、2 回目以降は動的と同じ再トリガ。
- これにより、`initialEventName` の書き換え、`enabled` の切り替え、`Reinit`、「pause したまま有効化する」のような Unity の標準に逆らう手順を使わない。§5.1 の実測は、この契約が Unity の標準の挙動だけで成り立つこと（有効化 = 開始、pause / playRate の効く時点）を確かめるために行う。
- `TimeScale == 0`（または `Time.timeScale == 0`）は止まっていないので開始は届き、rate 0 で進まない（原則 4）。rate 0 で開始したときの初期イベントの扱いは §5.1 の S4 で実測する。

**D5. 配置済み VFX の登録（Adopt）。**

- `IVfxSystem.Adopt(UnityEngine.Object placed, AssetOwner owner, string timeLayerId = UpdateLayerIds.Gameplay)` → `IVfxHandle`（unscaled の明示は D3 と同じ意味。形は B で既存の命名に合わせてよい）。`placed` は配置済みの VFX の component（Framework は型を知らない）。
- **所有者は配置した Scene の `Scene(id)`。** `PreUnloading` で回収するので、回収の時点で `VisualEffect` はまだ破棄されていない。VFX ごとに notifier を足さずに済む。`OnInitialize` の時点で `ISceneQuery.IsSceneLoaded` は true なので受理される（`a4e940b1` で確認）。
- **backend の契約:** `IVfxBackend.Adopt(UnityEngine.Object placed)` → `VfxInstanceId`（登録だけ。有効化しない）、`IVfxBackend.Release(VfxInstanceId id)`（adopted 専用。無効化して登録を外すだけで GameObject を壊さない。id ごとに高々 1 回。破棄済みの対象にも安全に完了する）。`Destroy` の意味は変えない。どちらを呼ぶかは handle の種別で `VfxSystem` が決める。
- **再有効化しない:** VFX Graph は `enabled` の切り替えで初期イベントを再送する（d2 の実測。`VfxGraphBackend` の注記。§5.1 の S6 で再確認する）。adopted の回収は終端で、再び使うなら新しく `Adopt` する（I4）。
- **二重登録:** 同じ component の 2 回目の `Adopt` は拒否して何もしない handle を返す（Editor で既に開いている Scene を `PerformUnitySceneLoad` が再利用する経路で起きうる）。
- **非対応環境:** `VfxSystem` は `Adopt` で backend を呼ばず何もしない handle を返すので、有効化は起きない（I5）。`PlacedVfxActivator` が `VfxCapabilityState` を読む経路は無くなるので、**`VfxCapabilityState` は削除する**（読み手が置き換えで消えるため。参照 0 を理由にしない）。`VfxGraphBackend` 内の判定共有と、読んでいるテストを合わせて直す。
- **marker:** `PlacedVfxActivator` は名前を変えず、有効化をやめて marker にする。`Awake` は何もしない（XML doc に契約として書く）。公開面は `Component Effect`（同じ go の `VisualEffect` を `Component` として返す）と時間の宣言（gameplay / unscaled）。InGame は `VisualEffect` 型を使わず `Adopt(marker.Effect, ...)` を呼ぶ（I6）。`VisualEffect` は従来どおり無効で保存する。
- **誰が登録するか:** `CellCompanionScene` と `SeasonAtmosphereVfxScene` が `OnInitialize` で root から marker を集めて `Adopt` する。role は見ない。共通処理は InGame の internal の小さい class（`PlacedVfxAdopter`）に置く。root の走査は Scene のロード時に 1 回で、Environment Scene（216 件）でも marker が無ければ何もしない。
- **時点:** 開始は D4 のとおり最初の止まっていない反映点（`Initialize` の後の最初の LateUpdate）。従来の `Awake` での有効化より 1 フレーム程度遅れうることは許容する（演出品質の判断は求めない）。

**D6. d1 の C / C' からの入力の採否（d4a の分）。**

- `BindOwner_DestroyedWithoutNotification_ReleasedOnNextTick` の Tick 前 `Destroy`=0: 不採用。契約（§16.3）は「通知が届かない破棄は次の Tick」で、その確認として妥当。
- `VfxSystemOwnerTests.cs` の分割: 不採用。d4a はこのファイルに追加しない（時間は `VfxSystemTimeLayerTests.cs`、登録は `VfxSystemAdoptTests.cs`）。
- 再ロード後の再受理（実 `SceneDirector` の 1 往復）は d4b へ送る（§10）。

**採らなかった案。**

- **領域単位で backend に渡す（`SetTimeDomain`、配置済みは `VfxSystem` の外の registry）:** 配置済み VFX を `VfxSystem` の外に置くので、U2 の裁定と両立しない。
- **VFX Graph に `Time.timeScale` を効かせ、`playRate` は layer の倍率だけにする:** 合成が 2 か所に分かれる（原則 2）。
- **`playRate = Time.timeScale × layer.TimeScale`（A3 統合候補の 1 版）:** `Time.deltaTime` は `maximumDeltaTime` で頭打ちになり `unscaledDeltaTime` はならないので、負荷スパイクで gameplay とずれる（外部レビュー High 1）。領域の時間 ÷ unscaled の rate に置き換えた。
- **配置済み VFX を pause した状態で有効化する（保留をやめて即時に開始する）:** §1.4 の S1 で、Unity では pause したまま有効化しても simulation が進まないと分かった。保留の状態を handle から無くせるが、「止まっている間に開始しても進まない」ことが backend ごとの挙動に依存する。保留の方式なら、止まっている間は backend に開始が届かないことを Framework だけで保証でき、2 巡目の外部レビューも保留の方式を前提に「成立すれば現設計を維持」としたので、維持する。
- **layer の Update から `VisualEffect.Simulate` で進める:** 毎フレーム呼ぶ用途で使えるか・描画と性能への影響が未確認。§5.1 の結果で AC6・AC7 が成り立たない見込みになったときの代替として控えに残す。

### 2.6 未決事項

- **U1〜U4 は裁定済み（発注者、2026-10-27）。** U1: trigger の VFX asset を build に乗せる経路は BuildSystem の実装系へ（d4b の対象外にも引き継ぐ）。U2・U3: 配置済みも `VfxSystem` に登録し同じ規則、timeout は effect 自身の時間。U4: gameplay layer を時間の領域とし、既存の `UpdateBehaviourAdapter` の既定 layer を定数にする。
- **§5.1 の実測で決めたもの:** `.vfx` の Update Mode（Delta Time + Ignore Time Scale）、`VfxGraphBackend` の換算、AC6 の N = 30・除外 0（reap しない fixture）、反映点が simulation より前であること（S2）、D4 が Unity の標準の挙動だけで成り立つこと（S2・S4）。未決事項は無い。

### 2.7 revision 2 の差分（§6.1・§6.1.1 の A 再開。主担当の案）

**理由:** 「変わったときだけ書く」（AC1）と、毎フレームの u に依存する backend の換算が、`VFXManager.maxDeltaTime（0.05）< u ≤ Time.maximumDeltaTime（0.333）` のフレームで両立しない。スパイク（§6.1.1）で、そのフレームの VFX の進みが 0.05 に止まることを確認した。

**R1. VFX Graph の上限を Unity の頭打ちに揃える。** `VfxGraphBackend` は能力判定が対応のとき、`VFXManager.maxDeltaTime` に実行時の `Time.maximumDeltaTime` を写し、Dispose で写す前の値に戻す（写した backend だけが戻す）。
- 揃えると、gameplay の領域では u によらず `playRate = Time.timeScale × Layer の TimeScale` になる（§6.1.1 の T2・T3: VFX は `min(u, 上限) × playRate`、`Time.deltaTime` は `min(u, maximumDeltaTime) × timeScale`）。「変わったときだけ書く」のまま全フレームで一致し、D3 の換算の式と AC1 は変えない。
- 値は手で書かず実行時に写す（0.333 と 0.333333 のずれを §6.1.1 の T2 で観測した）。
- **project 設定（`VFXManager.asset`）は変えない。** 同じ値を Time の設定と別の asset に二重に持つと、片方だけ変わって再び食い違う。
- **追従しない契約:** game が実行中に `Time.maximumDeltaTime` を変えても、backend は写し直さない。§16 の契約と `IVfxBackend` の実装の注記に書く。SampleGame は実行中に変えていない。
- 採らなかった案: VFX 側の上限を大きくする（例: 1.0）。u ≤ 上限なら換算が要らなくなるが、u > 上限で同じ問題が戻り、unscaled の VFX の 1 回の進みも大きくなる。

**R2. unscaled の領域の時間を `min(u, Time.maximumDeltaTime)` にする（§2.1）。** R1 の後でも、VFX は 1 フレームを上限より先へ進めない。上限なしの u を unscaled の領域の時間にすると、rate は常に 1 で書き直されず、u > 上限のフレームで VFX が上限までしか進まない。そこで領域の時間の定義を、実際に進められる量に合わせる。
- Framework の変更: internal の `IVfxTimeSource` に `MaximumDeltaTime`（`Time.maximumDeltaTime`）を足し、`VfxTimeLayerBinding` が unscaled の領域の時間を `min(u, MaximumDeltaTime)` にする。rate はこれを u で割った値で、頭打ちのフレームでは 1 未満になって書かれる。消え切り待ちの timeout も同じ時間で数える（I3・AC3）。公開 API は変えない。
- 意味: unscaled の VFX も 1 フレームに `Time.maximumDeltaTime` より先へは進まない（gameplay と同じ上限）。実時間の長い停止をまたいで一気に進めることはしない。

**R3. AC7 に、頭打ちにならないが 0.05 を超えるフレーム（u ≈ 0.1、0.2）の列を足す。** 今回の欠陥の失敗経路そのもの。あわせて、unscaled の要求で u > `maximumDeltaTime` のフレームの進みが `maximumDeltaTime` になることを AC3（偽 backend）と AC7 の注記で確かめる。

**変えないもの:** 時間の宣言、反映点、開始の保留（D4）、Adopt と回収（D5）、AC1・AC2・AC4〜AC6・AC8〜AC12。B のコード（中断 commit）を起点に B を再開する。

**§4.2 への追加:** 能力判定の時点で `VFXManager.maxDeltaTime` に書けない、または書いた値が VFX の進みに効かない（AC7 の u ≈ 0.1・0.2 の列が一致しない）。

---

## 3. 責務マップ

| ファイル | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `OneStarMaker/Scripts/Runtime/UpdateSystem/Layers/UpdateLayerIds.cs` | `Gameplay` / `GameplayLayerOrder` | 時間の領域の定義 | 定数 | なし | 追加 2 | — | +8 |
| `OneStarMaker/Scripts/Runtime/UpdateSystem/Adapters/UpdateBehaviourAdapter.cs` | 既定 layer を定数で書く | 同じ事実を 1 か所に | — | `UpdateLayerIds` | 変更なし | 既存 | ±0 |
| `OneStarMaker/Scripts/Foundation/UpdateSystem/World/UpdateCoordinator.cs` | `TryGetLayer(string, out UpdateLayer)`（作らずに引く） | 副作用なしの参照 | — | — | 追加 1 | `UpdateCoordinatorTests` | +12 |
| `.../Rendering/Vfx/Abstractions/VfxSpawnRequest.cs` | 時間の領域の宣言（既定 gameplay、`WithUnscaledTime`） | 時間の宣言 | 値型 | `UpdateLayerIds` | `TimeLayerId`、`WithUnscaledTime` | 単体 | 55 → 80 |
| `.../Abstractions/IVfxSystem.cs` | `Adopt` | 配置済みの登録 | — | — | 追加 1 | — | 24 → 40 |
| `.../Abstractions/IVfxHandle.cs` | `Play` の XML doc に「止まった領域では開始を保留し true を返す」を追加 | 開始の契約 | — | — | doc のみ | — | 50 → 55 |
| `.../Abstractions/IVfxBackend.cs` | `SetPlayback(id, paused, rate)`、`Adopt`、`Release`、adopted の `Play` の意味 | 反映と登録 | — | — | 追加 3 | 偽 backend | 52 → 90 |
| `.../Implements/IVfxTimeSource.cs`（新規、internal） | `Time.deltaTime` / `unscaledDeltaTime` / `maximumDeltaTime`（revision 2）と layer の状態を返す口 | 時間の値の注入 | — | — | internal | — | 0 → 30 |
| `.../Implements/VfxTimeLayerBinding.cs`（新規、internal） | rate・差分・timeout の増分の計算。状態を持たない | UpdateSystem 側の仕様への追従 | — | `IVfxTimeSource` | なし | 偽 source で単体 | 0 → 110 |
| `.../Implements/VfxSystem.cs` | `Adopt` の受理規則（二重登録の拒否を含む）、反映点の呼び出し、開始の保留の解放、`Vfx` layer 指定の拒否、adopted の回収で `Release` | 受理規則 | App | — | `Adopt` | 既存 + 新規 | 512 → 580（500 超は既存由来。非分割理由: 増分は `Adopt` の受理規則と binding の呼び出しで、時間の計算は binding へ分けた。Phase C で構造劣化の由来を区別して記録する） |
| `.../Implements/VfxHandle.cs` | 領域、前回書いた値、adopted の種別、開始の保留 | — | handle 寿命 | — | internal | 単体 | 251 → 300 |
| `.../Implements/VfxRuntimeHost.cs` | Gameplay layer の作成、`IVfxTimeSource` の実装、LateUpdate の反映点の転送 | 配線 | App | `UpdateCoordinator`、`UnityEngine.Time` | internal | `VfxRuntimeHostTests` | 228 → 280 |
| `.../Implements/UnconfiguredVfxBackend.cs` | 追加 IF の空実装 | IF 追加 | — | — | — | — | 51 → 65 |
| `OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | `protected IVfxSystem? Vfx` | Game への経路 | App | — | 追加 1 | 既存 bootstrap テスト | 1241 → 1250（既存の大きさ。増分 1 property で非分割） |
| `OneStarMaker/Tests/Rendering/Vfx/FakeVfxBackend.cs` | `SetPlayback` / `Adopt` / `Release` と呼び出し順の記録 | IF 追加 | — | — | — | — | 192 → 250 |
| `OneStarMaker/Tests/Rendering/Vfx/VfxSystemTimeLayerTests.cs`・`VfxSystemAdoptTests.cs`（新規）、`VfxSystemTickAllocationTests.cs`（AC12）、`VfxRuntimeHostTests.cs`（Gameplay layer の作成・反映点）、`UpdateCoordinatorTests`（`TryGetLayer`）、timeout の unscaled を前提にした既存テスト | テスト | — | — | — | — | — | 新規 2 本 各 ≈250 |
| `SampleGame/Common/Scripts/Rendering/Vfx/VfxGraphBackend.cs` | `SetPlayback` → `pause` / `playRate`（§1.4 の換算）、`Adopt`（登録だけ）、adopted の `Play`（最初は有効化）、`Release`、revision 2: 対応時に `VFXManager.maxDeltaTime` を `Time.maximumDeltaTime` に揃え、Dispose で戻す | 実 backend | backend | `UnityEngine.VFX` | — | Play（`-WithGraphics`） | 342 → 420 |
| `SampleGame/Common/Scripts/Rendering/Vfx/PlacedVfxActivator.cs` | marker（`Effect`、時間の宣言）。`Awake` は何もしない | 登録方式の変更 | Scene 内の go | なし | 公開 property | authoring EditMode | 38 → 40 |
| `SampleGame/Common/Scripts/Rendering/Vfx/VfxCapabilityState.cs` | 削除 | 読み手が `VfxSystem` の判定へ置き換わる | — | — | 削除 | — | 50 → 0 |
| `SampleGame/InGame/InGameSession/World/CellCompanionScene.cs`・`SeasonAtmosphereVfxScene.cs` | marker を集めて `Adopt` | 登録 | Scene | `IVfxSystem` | ctor 引数 | 実 SceneDirector の Play テスト | 38 → 60 / 44 → 60 |
| `SampleGame/InGame/InGameSession/World/PlacedVfxAdopter.cs`（新規、internal） | root から marker を集めて `Adopt` する共通処理 | 登録 | — | `IVfxSystem` | internal | Play テスト | 0 → 50 |
| `SampleGame/DependOnAll/GameSceneFactory.cs`・`AppInitializer.cs` | `IVfxSystem` の注入 | 配線 | — | — | ctor 引数 | 既存 | +10 / +5 |
| `SampleGame/Tests/Rendering/Vfx/`（`PlacedVfxAuthoringTests`、`VfxScenePlayTests`、`VfxGraphBackendPlayTests`、`VfxPlayTestFixture` ほか `VfxCapabilityState` を読むもの・`Awake` の有効化を前提にするもの） | テストの追従と AC6〜AC10 | — | — | — | — | — | — |
| d3 の `.vfx` 2 件と test fixture の `.vfx` | Update Mode を D3 の規約に | 合成の 1 か所化 | — | — | — | AC10 | Editor 操作 |

asmdef 参照の追加は 0 件。公開 API の追加は `UpdateLayerIds.Gameplay` / `GameplayLayerOrder`、`UpdateCoordinator.TryGetLayer`、`VfxSpawnRequest.TimeLayerId` / `WithUnscaledTime`、`IVfxSystem.Adopt`、`IVfxBackend.SetPlayback` / `Adopt` / `Release`、`AbstractApplicationInitializer.Vfx`。S-4 program §10 と §6.3 を A3 の凍結 commit で更新する。

---

## 4. 実装計画

### 4.1 順序（外部レビューの指示: 時間制御 → 配置済み VFX の順に小さく進める）

1. **時間制御（Framework）:** `UpdateLayerIds.Gameplay`、adapter の定数化、`TryGetLayer`、`VfxSpawnRequest` の宣言、`IVfxTimeSource`、`VfxTimeLayerBinding`、`IVfxBackend.SetPlayback`、開始の保留、LateUpdate の反映点、timeout の時間基準、`protected Vfx`。偽 backend の単体テストと、§16.5 の timeout の記述に合わせた既存テストの書き換え。
2. **時間制御（backend）:** `VfxGraphBackend.SetPlayback`（§5.1 の換算）、`.vfx` の Update Mode（Editor 操作）。
3. **配置済み VFX:** `IVfxSystem.Adopt` / `IVfxBackend.Adopt` / `Release`、adopted の `Play`、marker への変更、`PlacedVfxAdopter`、`CellCompanionScene` / `SeasonAtmosphereVfxScene` / `GameSceneFactory` の配線、`VfxCapabilityState` の削除、authoring テストと d3 の Play テストの追従。
4. Play テスト（AC6〜AC8）と authoring テスト（AC10）。

### 4.2 Phase B から Phase A へ差し戻す条件

- §3 に無い公開 API・状態・依存・所有者・寿命が要る。
- asmdef 参照の追加が要る。
- Unity の標準の挙動だけでは D4 の開始の契約を満たせず、`initialEventName` の書き換え・`enabled` の切り替え・`Reinit` 等が要る。
- `VisualEffect.pause` / `playRate` / Update Mode が AC6・AC7 の振る舞いを示さず、反映の経路（D3）を変える必要がある。
- VFX Graph の simulation が `Vfx` layer の LateUpdate より前に走り、I1 の「同じフレームで反映」が成り立たない。
- 負荷スパイクのフレームで、§1.4 の換算の `playRate` が VFX の進みに効かず、AC7 のスパイク列が一致しない。
- revision 2: 能力判定の時点で `VFXManager.maxDeltaTime` に書けない、または書いた値が VFX の進みに効かない（AC7 の u ≈ 0.1・0.2 の列が一致しない）。
- `CellCompanionScene` に marker の収集を置くと、VFX 以外の companion（Environment / Lighting / Events）の振る舞いが変わる。

### 4.3 BuildSystem の既知の課題との関係（判断基準による記録）

- **content build:** d4a は新しい Scene や asset を足さない。変わるのは d3 の Scene の component と `.vfx` の設定だけで、`Build Spring Full / Whitebox` の Content Directory には従来どおり Scene payload として入る。Phase C で両方の build が成功し、d3 の 2 つの VFX Scene が入ることを確かめる（判定必須）。
- **Player build:** d4a では行わない。理由は (1) BS4 の Player は smoke の後に終了して Spring に入らないので、Player 上で時間の追従を観測する経路が無い（S-9 へ送り済みの課題）、(2) この worktree のルートが 97 文字で予算外。どちらも BuildSystem の作業候補として記録する（「Spring に入る Player の観測経路」と「Player build を行う checkout の置き場」）。

---

## 5. テストとレビュー計画

### 5.1 凍結前の実測スパイク（外部レビュー High 2 の依頼。実施済み）

推測と実測を分けるため、凍結の前に Unity 6000.6.0f1 / VFX Graph 17.6.0 の振る舞いを最小の PlayMode テストで測る。Unity テストの実行は Phase C の責任（AGENTS.md）なので、**別の実行担当 session** が行い、生の観測だけを返す。GO / NO-GO と設計の判断は書かない。

- **担当:** 実行担当 session（新規）。sandbox 外の実行の承認が要る。主担当（本 session）は観測の解釈と HANDOFF への記録を行う。
- **環境・初期状態:** 本ブランチの HEAD から作った使い捨てのブランチ（マージしない）。Unity Editor が閉じていること。
- **操作:** 使い捨てのブランチに、スパイク用の PlayMode テスト 1 本（`SampleGame/Tests/Rendering/Vfx/` に置く。既存の `VfxPlayTestFixture` と fixture の `.vfx` を使い、新しい fixture の `.vfx` は既存のものを複製して Update Mode だけを変える。Editor 操作で作る）を足し、`pwsh tools/run-tests.ps1 -WithGraphics -Filter <スパイクのテスト>` で実行する。`Thread.Sleep` / `Task.Delay` は使わない。
- **観測する問い:**
  - **S1:** 無効で保存した `VisualEffect` を有効にしたとき、初期イベントがどのフレームで処理され、粒子がいつ出るか。有効にする前に `pause = true` / `playRate` を書いた場合、その値は保たれ、粒子は出ないか（D4 はこれに依存しないが、標準の挙動として記録する）。
  - **S2:** LateUpdate で書いた `pause` / `playRate` が、同じフレームの simulation に効くか（反映点が simulation より前か）。
  - **S3:** Update Mode が (Fixed Delta Time + Ignore Time Scale) と (Delta Time + Ignore Time Scale) のそれぞれで、1 フレームに effect が進む時間と、`Time.unscaledDeltaTime`・`VFXManager` の delta の上限・負荷スパイクのフレームの関係。`playRate` をどう与えれば effect の時間が `Time.deltaTime × layer.TimeScale` に一致するか。
  - **S4:** `playRate = 0` の受理と、`playRate = 0` のまま開始したときの初期イベントと粒子。続けて、**`playRate = 0` で開始 → `playRate > 0` に変える → `Play` を再送せずに粒子の発生と simulation が始まるか**（外部レビュー 2 巡目 指摘 2）。成り立てば D4 を維持し、成り立たなければ A2 に戻って D4 を再検討する（先回りの回避策は入れない）。
  - **S5:** `aliveParticleCount` の読み戻しが何フレーム遅れるか（AC6 の除外フレーム数）。
  - **S6:** 有効化 → 無効化 → 再有効化で初期イベントが再送されるか（d2 の実測の再確認）。
- **保存する証拠:** 生ログ、結果 XML、テストの観測値の出力（フレームごとの値）、スパイク用テストの差分。対象の commit を記録する。証拠の保存・引渡しは [レビュー証拠](../../.agents/skills/osm-workflow/references/review-evidence.md#保存先とリモートへの配布) に従う（`evidence stage`）。
- **結果の扱い:** 主担当が §1.3 に「実測（commit、日付）」として記録し、推測（文書・コードの読み）と分ける。D3 の Update Mode と換算、AC6 の数値を確定する。D4 の契約が Unity の標準の挙動だけで成り立たないと分かったら、D4 を書き直して A2 をやり直す。
- **状態:** 実施済み（2026-10-27）。観測は §1.4。実行担当は独立した worktree の subagent（Anthropic、Claude 系。Remote Control 経由の会話から新しい session を起動できなかったため）。原記録は `evidence stage` で固定した（§0）。

### 5.2 テスト

- **単体テスト（EditMode、Unity なしの論理）:** `VfxSystemTimeLayerTests.cs`（新規。AC1〜AC3、AC5、`Vfx` layer 指定の拒否、未作成 layer の扱い、App / Manual 所有の消え切り待ちが止まった時間で残ること）、`VfxSystemAdoptTests.cs`（新規。AC2 の配置済み側、AC4、AC9 の登録側）、`UpdateCoordinatorTests` に `TryGetLayer`、`VfxSystemTickAllocationTests` に AC12、`VfxRuntimeHostTests` に Gameplay layer の作成と反映点の転送。
- **Play テスト（`-WithGraphics`）:** `VfxGraphBackendPlayTests` に AC6（動的）・AC7、`VfxScenePlayTests` に AC6（配置済み）・AC8・AC9（配置済み側）。
- **authoring（EditMode）:** AC10（`PlacedVfxAuthoringTests` の更新）。
- **差し戻し中の起点 `-Filter`:** `OneStarMaker.Tests.Rendering.Vfx`、`SampleGame.Tests.Rendering.Vfx`、`UpdateCoordinatorTests`。
- **判定必須テスト:** 全 EditMode 回帰（空 filter）、上記 Play テスト（`-WithGraphics`）、Content Directory の build 2 種（`Build Spring Full`・`Build Spring Whitebox`。d3 の 2 つの VFX Scene が入ること）。
- **全 EditMode 回帰の適用除外:** なし
- **操作・実行時・目視条件の検証経路:** E1〜E4 は Play テストで閉じる（構造・値の検査）。見た目の判断は求めない。AC6・AC8 は `aliveParticleCount`（寿命の無い粒子・一定の spawn rate の fixture）、AC7 は spawn 数 ÷ spawn rate と gameplay layer の要素の `DeltaTime` の和で判定する。実行担当は Phase C（判定 C の実行担当）。
- **未知の操作経路:** §5.1 の S1〜S6 は実測した（§1.4）。残る 3 点（カメラがある条件、スパイクのフレームでの `playRate > 1`、MonoBehaviour の LateUpdate からの書き込み）は「未確認。初回確認は Phase C」。理由: Unity テストが要る。担当: 発見 C の Unity 実行担当。確認地点: AC6・AC7 の最初の実行。不成立時: §4.2 の差し戻し条件として Phase A を新しい revision で再開し、D3 の換算か `Simulate` の控え案を比較する。
- **人間の判断が必要な条件:** なし
- **高コスト証拠:** なし（Player build を行わない）
- **判定 C の照合 script の方式:** なし
- **機械検査:** `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。

### 5.3 レビューの記録

- **A0/A1 主担当:** この session の主担当（Anthropic、Claude Opus 系）。
- **A2 入力版（分割前）:** commit `6a0216a9`（旧名 `docs/handoff/S-4D4_LOCAL_EVENT.md`）、内容 sha256 `57a16b15c59d2564f13e381ae15ac4123f48a2727084f287912c30212933dfd9`。3 件とも同じ入力版を使い、互いの指摘を見せていない。
- **A2 独立レビュー（分割前の入力版に対して）:**
  - (1) アーキテクチャゲート — 新規 subagent（Anthropic、Claude Sonnet 系）。指摘 12 件。
  - (2) 不変条件の反例探し・AC の曖昧さ — cursor-agent（xAI、Grok 系）。指摘 15 件（反例 1、曖昧 9、前提の食い違い 1、B での判断 4）。破れなかった不変条件: 旧 I1・I2・I4〜I8。
  - (3) A0 だけから代替構成 — 新規 subagent（Anthropic、Claude Fable 系）。計画案は読ませていない。
- **A2 の採否（主担当の案。d4a / d4b の行き先を併記）:**

| 指摘 | 内容 | 採否 | 理由・反映先 |
|---|---|---|---|
| 反例 F1（high） | 反映が次の Tick まで遅れ、Tick 間の Pause→Resume が消える | 採用 | 反映点を `Vfx` layer の LateUpdate にし、I1 を定義（d4a D3・AC1） |
| アーキ A1（high） | internal の role classifier を DependOnAll から呼べない | 採用 | Events 用の Scene class と factory の role 分岐をやめ、`CellCompanionScene` が集める（d4a D5、d4b §10） |
| アーキ A2（high） | adopted の `Destroy` に別の意味を持たせる | 採用 | `IVfxBackend.Release` を別メソッドに（d4a D5） |
| アーキ A3 | 配置済みの所有者の根拠 | 採用 | `Scene(id)`（d4a D5） |
| アーキ A4 | `Time.timeScale` を Framework が直接読むとテスト不能 | 採用 | `IVfxTimeSource` で注入（d4a D3） |
| アーキ A5・反例 F12 | Gameplay layer の作り手と order | 採用 | 既存の adapter 既定を定数化し、host が作る（d4a D2） |
| アーキ A6 | `VfxCapabilityState` の存続 | 採用 | 削除（d4a D5） |
| アーキ A7 | §3 の漏れ | 採用 | `FakeVfxBackend`、既存テスト、確保 0 を §3・§5 に（d4a） |
| アーキ A8 | 前回の値の二重保持 | 採用 | `VfxHandle` に 1 か所（d4a D3） |
| アーキ A9 | property 名 | 採用 | `Vfx`（d4a D1） |
| アーキ A10 | trigger の論理を純 C# へ | 採用 | `LocalVfxEventSession`（d4b §10） |
| アーキ A11 | InGame に `VisualEffect` 型を出さない | 採用 | marker が `Component` を返す（d4a D5・I6） |
| アーキ A12 | marker の `Awake` no-op | 採用 | d4a D5 |
| 曖昧 F2〜F10 | AC の読み方 | 採用 | d4a の AC1〜AC10 に読みを固定。trigger 側（旧 AC1・AC3・AC15）は d4b へ |
| 前提 F11 | 再ロードの合成テストは既にある | 採用 | d4b §10 |
| B 判断 F13 | 再有効化で初期イベントが再送される | 採用 | adopted の回収を終端にする（d4a I4・D5） |
| B 判断 F14 | unscaled と `.vfx` の Ignore Time Scale の食い違い | 採用 | 全 `.vfx` を Ignore Time Scale に（d4a D3） |
| B 判断 F15 | trigger の `.vfx` の Addressables entry | 採用 | d4b §10 |
| 代替: 領域単位の `SetTimeDomain` と外部 registry | | 不採用 | U2 の裁定と両立しない |
| 代替: Events 用の新 Scene class を作らない | | 採用 | d4b §10 |
| 代替: Ignore Time Scale と単一の適用点 | | 採用 | d4a D3（rate の定義は外部レビュー High 1 で改めた） |
| 代替: A3 前の小さい PlayMode 観測 | | 採用（実行は別の実行担当） | §5.1。外部レビュー High 2 と同じ依頼になった |

- **外部レビュー 1 巡目（発注者が依頼。A3 統合候補に対して）と採否:**

| 指摘 | 内容 | 採否 | 反映先 |
|---|---|---|---|
| High 1 | `Time.deltaTime` は `maximumDeltaTime` で頭打ち、unscaled は頭打ちにならないので、単純な積は負荷スパイクで乖離する | 採用 | §2.1 の時間の定義、D3 の rate（領域の時間 ÷ unscaled）と backend の換算、AC3・AC5・AC7。timeout は意図して unscaled ではない（I3） |
| High 2 | 配置済み VFX は有効化で自動再生し、AC6（Adopt 後・Play 前に状態を書く）と順序が合わない | 採用 | D4 の開始の契約（止まった領域では開始を保留、「登録 → 状態の反映 → 開始」）、I2・AC2・AC8、§5.1 の実測、Unity の標準に逆らう手順を使わないことを §4.2 の差し戻し条件に |
| Medium 3 | 積が 1 の組だけでは常に rate = 1 の誤実装を見逃す | 採用 | AC7 に (0.5, 0.5)・(1, 2) と負荷スパイクの列 |
| Medium 4 | 確保 0 の範囲 | 採用 | AC12 を反映処理だけに限定、既存 `Tick` の最適化は対象外（§1） |
| Medium 5 | asset ロード中の unload | 採用 | d4b §10（`LoadAssetAsync` が await の後に所有者の終端を確かめない事実も記録） |
| 維持する方針 | 統合・区別・`CellCompanionScene`・所有の区別・反映専用の Vfx layer・fallback と非 Scene asset は後続・AC15 を Player の証明と混同しない | 維持 | 全体 |
| 作業指示 | 時間制御 → 配置済み VFX → Local Event の順に小さく、独立レビューを維持 | 採用 | 分割（§0.1）、§4.1 |

- **外部レビュー 2 巡目（発注者が依頼。A3 統合版に対して）と採否:** 判定は「2 点を小さく補正して S1〜S6 の実測へ」。

| 指摘 | 内容 | 採否 | 反映先 |
|---|---|---|---|
| 指摘 1（High） | 異常 delta で `SetPlayback` を省くと、同じ反映点の Pause も届かず I1 と衝突する | 採用 | D3（rate の計算不能と pause の反映を分ける）、AC5（重なるケースを追加） |
| 指摘 2（Medium） | rate = 0 で開始した後、rate を正にしただけで再開するかは別の問い | 採用 | §5.1 S4 に追加。不成立なら A2 に戻る |
| 実測の優先 | S2・S3 が D3 の成立条件 | 維持 | §5.1 |
| 維持すべき判断 | 分割、`VfxSystem` での統合、Unity の標準の尊重、`Release`、対象外 | 維持 | 全体 |
- **A3 統合:** revision 1 は発注者の承認で凍結した（`8ba343af`）。
- **revision 2（2026-10-27）:** Phase B で見つけた D3 の換算の食い違いを、スパイク（§6.1.1）で確認して A 再開に分類した。差分は §2.7。A2 は差分の範囲（§2.1・D3・I3・AC3・AC7・§3 の 2 行・§4.2）だけを、revision 1 に関与していない独立レビューで見る。A3 は発注者の承認で凍結する。
- **主担当が自分で洗い直した、Phase C / C' で突かれそうな点（反映済み）:** 同じ配置済み VFX の二重 `Adopt`（I4・AC4）、止まった時間で App / Manual 所有の消え切り待ちが残ること（D3 の契約に明記）、216 件の Environment Scene の root 走査（D5 に根拠）、`aliveParticleCount` の読み戻しの遅れ（AC6・§5.1 S5）、d3 のテストの前提の変化（§3）、PR の大きさ（§0.1 の分割）。
- **C' 用に予約した担当:** OpenAI 系（Codex）。A2・A3 では使っていない。
- **独立性の強化条件:** A2 の 3 件のうち 2 件が Anthropic 系（モデルは別）。C' を A〜C に関与していないベンダーで行うために OpenAI 系を予約した。

## 6. Phase B 実装結果

**状態: 中断（Phase A revision 2 待ち）。** §4.1 の 1〜4 のコードは書いた。Editor 操作（`.vfx` の Update Mode）とコンパイル確認は未了。D3 の換算に食い違いの疑いを 1 件見つけ、新しい事実の分類に従ってスパイクで因果を確かめた（発注者了承 2026-10-27）。疑いは事実で、§6.1 の判断の基準により **A 再開**（§6.1 の結果）。

### 6.1 D3 の換算の疑い（スパイク。確認後に分類する）

- **疑い（論理の上の推論で、未観測）:** D3 は (paused, rate) を「前回と違うときだけ」backend へ書く（AC1）。backend の換算 `playRate = rate × u ÷ min(u, VFXManager.maxDeltaTime)`（u = unscaledDeltaTime）は毎フレームの u に依存する。§1.4 の S3 のモデル（VFX の 1 フレームの進み = `min(u, VFXManager.maxDeltaTime) × playRate`）が `0.05 < u ≤ 0.333` でも成り立つなら、そのフレームでは `Time.deltaTime` が頭打ちにならず rate が変わらない。すると `SetPlayback` は呼ばれず、playRate は前回の値（通常 1）のままになり、VFX は 0.05 秒、gameplay は u 秒進む。これは E3（任意の連続したフレーム列で一致）に反する。
- **未確認の前提:** §1.4 で測った負荷スパイクは u ≈ 0.517 の 1 点だけで、`0.05 < u ≤ 0.333` のフレームは測っていない。`Time.maximumDeltaTime` が `Time.timeScale` を掛ける前と後のどちらに効くかも測っていない。解決案（VFX 側の上限を `Time.maximumDeltaTime` に揃える、または十分大きくして換算を無くす）は、この 2 点が分かるまで比べられない。
- **担当:** 別の実行担当 session（§5.1 と同じ）。sandbox の外での実行の承認と、Unity のライセンスが有効な状態が要る（2026-10-27 時点では、この PC の Editor.log に `License revoked` が出ていた）。主担当は観測の解釈と分類を行い、実行担当は GO / NO-GO・設計の判断を書かない。
- **環境:** §1.4 のスパイクの使い捨てブランチ（`84d44922`、Update Mode 0 / 2 / 3 の fixture がある）から、新しい使い捨てブランチを作る（マージしない）。条件は §1.4 と同じで、`pwsh tools/run-tests.ps1 -WithGraphics -Filter <スパイクのテスト>` で実行する。待ちに `Thread.Sleep` / `Task.Delay` を使わず、フレームの引き延ばしは計算のループで行う。fixture は Update Mode 3（Delta Time + Ignore Time Scale）の、reap しないもの。
- **観測する問い（各条件 3 回、フレームごとの値を記録する）:**
  - **T1（欠陥の確認）:** `Time.timeScale = 1`・`playRate = 1`・`VFXManager.maxDeltaTime` は既定（0.05）。1 フレームを u ≈ 0.1、0.2、0.5 に引き延ばしたときの、spawn system の `totalTime` の増分、`Time.deltaTime`、`Time.unscaledDeltaTime`。
  - **T2（上限を上げたとき）:** 実行中に `VFXManager.maxDeltaTime` を 0.333 と 1.0 にし、T1 と同じフレーム列で同じ値を取る。u ≈ 0.5 のフレームでは `playRate = 2` も取る（§1.4 の未確認「スパイクのフレームで playRate > 1 を掛けたときの進み」）。値はテストの最後に元に戻し、終了後に `unity/ProjectSettings/` の git diff が空であることを記録する。
  - **T3（頭打ちと timeScale の順序）:** `Time.timeScale = 2` と `0.5` のそれぞれで、u ≈ 0.2 と 0.5 のフレームの `Time.deltaTime`（頭打ちが掛ける前か後か）。
- **保存する証拠:** 生ログ、結果 XML、フレームごとの値の出力、スパイクのテストの差分、対象の commit。`evidence stage` で固定する（§0 の取得案内の欄に状態を書く）。
- **判断の基準（観測の前に固定する）:**
  - T1 で u ≈ 0.1・0.2 のフレームの `totalTime` の増分が 0.05 ± 10% なら、疑いは事実。T2・T3 の結果で解決案を選ぶ。修正は §3 の外（project 設定か、backend が全体の設定を持つ寿命）になるので **A 再開**とし、revision 2 で D3 の換算、AC7（`0.05 < u ≤ 0.333` のフレーム列を足すか）、§3、§4.2 の差分だけを A2 で見直す。時間の宣言、反映点、開始の保留、Adopt は変えない。B のコード（中断 commit）を捨てず、再開の起点にする。
  - T1 で増分が u ± 10% なら、疑いは誤り。§6.1 を取り下げ、Phase B を revision 1 のまま続ける。
  - どちらにも当たらなければ、観測を記録して主担当が発注者に返す（追加の取得は 1 回まで）。
- **実装の現状:** `VfxGraphBackend.SetPlayback` は D3 の式のまま。

#### 6.1.1 スパイクの結果（2026-10-27）

- **実施:** 別の実行担当 session（Anthropic、Claude 系）。§1.4 のスパイクのブランチから切った使い捨てブランチ（テストの commit `e05ad48e`、未 push、マージしない）。Unity 6000.6.0f1、`-WithGraphics`、各条件 3 回、テストは Passed。実行後の `unity/ProjectSettings/` の git diff は空。原記録は `evidence stage` で固定した（ローカル固定済み（未昇格）。取得案内は実行担当の報告にあり、次の担当への引渡しは未完了）。
- **観測（事実。increment は spawn system の `totalTime` の 1 フレームの増分）:**
  - **T1**（`VFXManager.maxDeltaTime` 0.05、timeScale 1、playRate 1）: u ≈ 0.1・0.2 では `Time.deltaTime` = u、increment 0.050000。u ≈ 0.5 では `Time.deltaTime` 0.333333、increment 0.050000。3 回とも同じ。
  - **T2:** `maxDeltaTime` 0.333 では、u ≈ 0.1・0.2 の increment は u、u ≈ 0.5 で 0.333000（設定値）。1.0 では increment は u（u ≈ 0.5 で約 0.500）。playRate 2 ではどれも 2 倍（0.333 で 0.666000、1.0 で約 1.000）。
  - **T3**（`maxDeltaTime` 0.05）: increment はどの条件でも 0.050000。`Time.deltaTime` は timeScale 2 で u ≈ 0.2 → 約 0.400、u ≈ 0.5 → 0.666667。timeScale 0.5 で u ≈ 0.2 → 約 0.100、u ≈ 0.5 → 0.166667。
  - その他: T1 の 1 回目だけ、Play の直後に引き延ばしていない長いフレームが 1 つあった（u 0.83）。
- **解釈（主担当。観測とは分ける）:**
  - VFX の 1 フレームの進みは `min(u, VFXManager.maxDeltaTime) × playRate` で、playRate は頭打ちの後に掛かる（T2。§1.4 の未確認「スパイクのフレームで playRate > 1」も解消）。
  - `Time.deltaTime = min(u, Time.maximumDeltaTime) × Time.timeScale`。頭打ちは timeScale を掛ける**前**に効く（T3）。
  - T1 の u ≈ 0.1・0.2 の increment が 0.05（± 10% の内）なので、§6.1 の疑いは事実。
- **分類:** **A 再開**（§6.1 の判断の基準）。修正は VFX Graph の全体の設定（`VFXManager.maxDeltaTime`）に触れ、§3 の外になる。
- **revision 2 の起点（主担当の分析。未凍結）:**
  - **上限を揃える案:** `VFXManager.maxDeltaTime` を `Time.maximumDeltaTime` と同じ値にする。T2・T3 から、gameplay の領域では u によらず `playRate = Time.timeScale × Layer の TimeScale` になり、「変わったときだけ書く」（AC1）のままで全フレームで一致する。D3 の換算の式は変えなくてよい。0.333 と 0.333333 のような手書きのずれを避けるため、値は実行時の `Time.maximumDeltaTime` から写す。
  - **上限を大きくする案**（例: 1.0）: u ≤ 上限なら換算が不要になる（playRate = rate）。ただし u > 上限のフレームで同じ問題が戻り、unscaled の VFX の 1 回の進みが大きくなる。上限を揃える案より弱い。
  - **残る未決（§2.7 の R1〜R3 で主担当が決めた）:** (1) unscaled の領域。どちらの案でも rate は常に 1 で書き直されないので、u > 上限のフレームでは VFX が上限までしか進まない。unscaled の領域の時間を `min(u, 上限)` と定義し直すか（§2.1 の改訂）、別の扱いにするか。(2) 設定の置き場と寿命。project 設定（`VFXManager.asset`）を変えるか、backend が起動時に写すか。game が実行中に `Time.maximumDeltaTime` を変えたときに追従するか、しないことを契約にするか。(3) AC7 に `0.05 < u ≤ 0.333` のフレーム列を足すか（今回の欠陥は AC7 のスパイク列だけでは見逃しうる）。
  - **変えないもの:** 時間の宣言、反映点、開始の保留、Adopt と回収、テストの大半。B のコード（中断 commit）は revision 2 の凍結後に B を再開するときの起点にする。

### 6.2 実装したもの（§3 との対応）

- **Framework（時間）:** `UpdateLayerIds.Gameplay` / `GameplayLayerOrder`、adapter の定数化、`UpdateCoordinator.TryGetLayer`（`#nullable enable` を追加）、`VfxSpawnRequest.TimeLayerId` / `WithUnscaledTime`、internal `IVfxTimeSource`・`VfxTimeLayerBinding`（static、状態なし）、`IVfxBackend.SetPlayback`、`VfxHandle` の領域・前回値・開始の保留、`VfxSystem` の反映点、host の Gameplay Layer 作成・時間源・LateUpdate での反映、`AbstractApplicationInitializer.Vfx`。
- **Framework（配置済み）:** `IVfxSystem.Adopt`、`IVfxBackend.Adopt` / `Release`、adopted の回収は `Release`、二重登録の拒否（`VfxSystem` の集合）。
- **SampleGame:** `VfxGraphBackend`（`SetPlayback` の換算、`Adopt`、adopted の最初の `Play` は `enabled = true`、`Release` は無効化だけ）、`PlacedVfxActivator` を marker に（`Effect`、`UsesUnscaledTime`）、`VfxCapabilityState` 削除、`PlacedVfxAdopter`（internal static）、`CellCompanionScene` / `SeasonAtmosphereVfxScene` が `OnInitialize` で登録、`GameSceneFactory` / `AppInitializer` の注入。
- **テスト:** `VfxSystemTimeLayerTests`（AC1〜AC3・AC5・所属先の検証）、`VfxSystemAdoptTests`（AC2 配置済み・AC4・AC9 登録側）、`FakeVfxTimeSource`（新規の偽物）、`FakeVfxBackend` の追従、`VfxSystemTickAllocationTests` に AC12、`VfxRuntimeHostTests` の unscaled 前提のテストを LateUpdate・領域の時間の確認に書き換え、`UpdateCoordinatorTests` に `TryGetLayer`。SampleGame: `VfxGraphBackendPlayTests` に AC6（動的・配置済み）・AC7、`VfxScenePlayTests` に AC8・AC9（未構成を追加、動的 `Spawn` を追加）、`PlacedVfxAuthoringTests` に AC10 の Update Mode、`VfxSceneRenderPlayTests` / `VfxGraphCapabilityPolicyTests` / `GameSceneLoggingTests` / `CellSceneTests` の追従。`VfxCapabilityState` を検査していたテスト（writer の Dispose・未判定）は対象が消えたので削除した。

### 6.3 B 適応（凍結条件の意味を変えない実装上の判断）

- **`Tick(float)`:** 引数は変えず、反映点にした（host は `Vfx` Layer の LateUpdate から呼ぶ）。public ctor（時間源なし）では Tick の delta を全領域の時間とし、Layer は「止まっていない、倍率 1」とみなす。internal ctor が時間源を受け取る。
- **unscaled の表し方:** `TimeLayerId == null` を unscaled とした（D5 の「形は既存の命名に合わせてよい」）。`Adopt` の `timeLayerId` も同じ。
- **初期の再生状態:** backend の初期状態を (止まっていない, 1) と契約に書き、handle の「前回の値」の初期値にした。値が同じなら書かない。止まっているフレームの rate は前回の値を保つ。
- **host のテスト用の差し込み口:** `VfxRuntimeHost.FrameTimeForTest`（EditMode では `Time.deltaTime` を制御できないため）。
- **marker の時間の宣言:** `PlacedVfxActivator` に serialized の `_unscaledTime`（既定 false）。

### 6.4 規模（§3 の予想との差）

- `VfxSystem.cs` 512 → 678（予想 580）。`Adopt` の受理規則（無効 id・生存中 id・再入後の再判定）が `Spawn` と同じ形で並び、反映点と時間源なしの時間源が増えた。時間の計算は binding へ分けてある。
- `VfxHandle.cs` 251 → 389（予想 300）。開始の保留・再生状態の書き写し・adopted の回収の分岐と、その契約のコメント。
- Phase C で構造劣化の由来を区別して記録する。

### 6.5 未実行・未確認

- **Editor 操作:** `.vfx` 4 件（d3 の 2 件、fixture の `VfxImmortal` / `VfxShortLived`）の Update Mode を Delta Time + Ignore Time Scale にする操作は未実施（現在は全て 0）。AC10 の新しいテストはこの状態では落ちる。
- **コンパイル確認:** 未確認。Editor はこの worktree で起動したが、Library の初回の import の後、Editor.log に `License revoked: Your Unity Personal license has been revoked.` が出て Pipeline が 503 のまま応答しない。新規 `.cs` 5 件の `.meta` は初回の import で生成された（commit に含む）。
- **`pwsh tools/contract-audit.ps1`:** errors 0 / warnings 0（作業ツリー、2026-10-27）。
- **Unity テスト・Build:** 未実行（Phase C の責任）。
- **公開文書:** Architecture §24 の §16.5（unscaled の Tick）と `VfxCapabilityState` の記述は古くなった。Phase D の harvest で直す。
- **スライス外の差分（commit に含めない）:** この worktree で Editor を開くと、`MobileDependencyResolver/Editor` の `.pdb.meta` 5 件が削除される。原因は、`.gitignore` の `*.pdb` で vendor の `.pdb` 本体が checkout に無く、tracked の `.meta` だけが残って Unity が孤立した `.meta` として消すこと（元の checkout には `.pdb` がローカルにあるので起きない）。新しい worktree・clone では毎回起きる。d4a では毎回 restore して差分に入れない。根本の修正は d4a の範囲外で、別の作業として扱う。

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手

---

## 10. S-4d4b へ送る事項（d4b の Phase A の A0 入力。凍結済みの条件ではない）

d4b は d4a のマージ後に S-4 program §0 の手順で着手し、この節を program §6.3 の d4b 行（凍結の commit で書き写す）から自己完結の HANDOFF へ転記する。

- **答える問い:** Cell の Events Scene に置いた trigger から `IVfxSystem` 越しに VFX を起動し、Cell の unload（と所有 go の破棄）で確実に回収できるか。
- **前提（d4a で入るもの）:** `protected Vfx`、gameplay layer と時間の追従、開始の契約（止まった領域では開始を保留）、`CellCompanionScene` が `IVfxSystem` を受け取ること。
- **分割前の A1 で用意した条件（d4b の A1 の起点）:**
  - 不変条件: 回収漏れなし（Events の unload の後、trigger から起動した instance の `Destroy` が高々 1 回、`Removed` 時点で全て 1 回）、source より先に instance（`ReleaseScene(Events)` より前に `Destroy`）、Planner で VFX Scene に依存しない、非対応環境で trigger を発火しても何も起きない。
  - 成立条件: trigger で起動（準備前の進入はロード完了の時点で起動）、Cell unload で回収、go の `Object.Destroy` で `OnDestroy` の呼び出しの中で回収、同じ identity の再ロード後に再び受理（実 `TestableSceneDirector` の 1 往復。合成 `Subject` では `VfxSystemOwnerTests.AfterRemoved_StillRejected_UntilPreLoading_AndOldHandleDoesNotRevive` が確認済み）、Events Scene が `Build Spring Full` と `Build Spring Whitebox` の**両方**に Scene payload として入る（Player の動作の証明ではない）。
- **分割前の A3 統合候補で決めた設計:**
  - Events 用の新しい Scene class は作らない。`CellCompanionScene` が root から trigger（`LocalVfxEventTrigger`、MonoBehaviour）を集める。role 判定を `GameSceneFactory` に足さない（internal の `CellCompanionRoleClassifier` を DependOnAll から呼べないため）。
  - trigger 1 つ分の論理（asset の準備、進入中かどうか、handle を 1 つ持ち進入で起動・再入で前の handle を即 `Stop`・退出で `StopAllowingParticlesToFinish`、準備完了の時点で進入中なら起動）は純 C# の `LocalVfxEventSession` に置き、偽 `IVfxSystem` で単体テストする。Collider は最初から有効にして進入を記録する。
  - asset は `SceneBase.LoadSceneScopedAssetAsync`（既存。`AssetOwner.Scene(Events の identity)`）、VFX は `AssetOwner.Bind(trigger の go)`。unload の順序（PreUnloading → Unity Scene unload で `OnDestroy` → `ReleaseScene`）で「VFX の回収 → asset の解放」が保たれる。
  - trigger の `.vfx` の Addressables entry は Phase B の Editor 操作で Addressables の API から足す（YAML を直接編集しない）。World Workspace の transaction は Scene の entry だけを作る。
  - trigger の VFX は gameplay layer に属する（U4）。
- **外部レビュー Medium 5（必須検討）:** asset のロード中に Cell の unload が始まり、後からロードが完了した場合でも、終了した trigger が Spawn せず、source asset が残らないこと。`AssetManagement.LoadAssetAsync` は await の後に所有者の Scene が `ReleaseScene` 済みかを確かめずに登録する（`a4e940b1` の `AssetManagement.cs:52-74`）ので、キャンセルが届かないと残る。新しいキャンセル機構は作らず、既存の CancellationToken と所有者の終端で成り立つかをテストで確かめる。足りなければ、session が終端の後に完了したロードの handle をすぐ Release する。Framework 側の await の後の所有者確認の欠落は、所有スライス未定の後続の入力として記録する。
- **U1（裁定済み）:** trigger の VFX asset を Content Directory・Player build に乗せる経路は BuildSystem の実装系へ。d4b は Content Directory 上で trigger の VFX が起動することを条件にしない。
- **ここでは答えない問い:** 大型 Event（d5）。
