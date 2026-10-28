# S-4d4b local Event

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A1 初稿。A2 入力版）
- branch: `claude/s-4d4b-local-event`
- implementation base commit: `9d0c8f2f`（`develop`、S-4d4a の PR #147 のマージ）
- implementation head commit: 未固定
- risk: `high`（所有者・寿命の実例、Addressables entry の追加、非同期ロードと unload の競合に触れる。Framework の公開 API は変えない見込み）
- owner: 発注者 / S-4d4b 担当
- created: 2026-10-28
- expires: S-4d4b の Phase D 完了時
- harvest to: Architecture §24 §16（§16.7 の S-4d4b 項目の解消と、Events Scene の trigger から起動する VFX の寿命の実例）、§05 §5.13（Cell の職種 companion のうち Events の振る舞い）
- workflow task: `s-4d4b-local-event`（未開始。最初に Evidence を固定する時点で `pwsh tools/workflow-task.ps1 start` する）
- Phase A snapshot の取得案内・状態: 未生成（凍結前スパイクは計画していない。凍結した HANDOFF 本体は A3 凍結 commit で固定する）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票: Phase A 作業中。A1 初稿を A2 入力版として commit し、A2 の独立レビューへ渡す。A3 の凍結は発注者の承認を待つ。Phase B へは A3 凍結後に新しい session で入る。

---

## 1. 目的と対象外

- **目的:** Cell `Spring (4,2)` の Events Scene `Spring_Events_4_2` に trigger を置き、trigger から VFX を `IVfxSystem` 越しに動的に起動し、Cell の unload と trigger の GameObject の破棄で確実に回収されることを、実 Scene で示す。d1 から送られてきた `Bind(go)` 所有の実例（破棄通知での回収、source asset と instance の前後関係、同じ identity の再ロード後の再受理）をここで閉じる。
- **対象外:**
  - 独立 Scene の大型 Event `Spring_Event_MultiCellProof`、Planner 実証の全体、S-4 の harvest と program の削除（S-4d5）。
  - trigger の VFX asset を Content Directory・Player build に乗せる経路（BuildSystem の実装系。2026-10-27 発注者決定、§2.6 U1）。
  - ParticleSystem fallback（後続の独立スライス、担当未定）。
  - Player での VFX 描画、Spring に入る Player の観測経路（S-9。BuildSystem の候補として §4.3 に記録）。
  - 同時数の上限・優先度・間引き・pool / prewarm・DebugStudio での観測（S-4 program §6.4 で入れないとしたもの）。
  - gameplay のコードを gameplay layer へ移してゲームの一時停止を作ること、Prefab で動的に置いた配置済み VFX の登録（d4a から送られた所有スライス未定の事項）。
  - Framework の変更。`AssetManagement.LoadAssetAsync` が await の後に所有者の Scene の終端を確かめない欠落の修正（§1.3）は、所有スライス未定の後続の入力として記録するだけにする。
- **現況:** §1.3（A0、`9d0c8f2f` で確認）。

### 1.1 Phase A の判断基準（発注者、2026-10-27。S-4 program §0 から転記）

A1 の各案と A3 の裁定の材料に明記する。

- **主目的は BuildSystem。** いまの主目的は、きちんとした BuildSystem を作ることで、VFX（S-4d）はそのつなぎ込みの題材である。各案で、スライスの成果物が content build・Player build の経路に乗るかと、Build 側の既知の課題（BS4 の Player は smoke の後に終了して InGame の Spring に入らない、transport staging の path が予算の外）を踏むかを書く。Build 側の課題は「やらない」で片付けず、BuildSystem の作業の候補として扱う。
- **残すコードは SampleGame に必要なものだけ。** 生成や authoring の道具を残すかは「SampleGame にそのコードが必要か（無いと SampleGame の検証が成り立たないか）」で決める。必要でなければ、一時スクリプトで作り commit の前に消す案を既定にする。「他の季節・Cell でも使える」「再利用できる」は残す理由にしない。
- **checkout のルート。** Player を build するスライスは、ルートが 96 文字以下の checkout で作業する（transport staging の path の制約）。この worktree のルートは 82 文字（末尾の区切りを含む）。d4b は Player を build しない（§4.3）。

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
└─ Season_Summer / Autumn / Winter         同型
```

- Cell 配下の全職種 Scene は `StreamByDistance = false`、`LoadType.OnDemand`。親 Cell が Stable になった後、SampleGame の companion loader が選択中の制作プロファイルに応じて明示 `AddScene` する。Unload は親 Cell の再帰破棄へ委ねる。

**職種ごとの所有物（program §2.2）。**

- **Cell Events**: trigger、spawn、encounter anchor、局所的な進行制御。
- **Cell VFX**: 霧、煙、流水など、その Cell が常駐中は継続する環境エフェクト。
- VFX Scene にゲーム進行 trigger を置かない。Events Scene が VFX の開始・停止を要求する。
- Scene を跨ぐ `GameObject` の直接参照は禁止し、event id、world position、service query、`IAssetManagement` で寿命を宣言してロードする prefab で接続する。

**Event Scene の粒度（program §2.3）。** 次のいずれにも該当しないイベントは `*_Events_{x}_{y}` に含める。複数 Cell に効果が及ぶ／起点 Cell の Unload 後も継続する必要がある／専用 Timeline・Cinemachine staging を独立ファイルとして所有したい／別担当が Cell bundle と独立して変更する規模。いずれかに該当するときだけ `Season_Event_{slug}`（d5）とする。小イベント 1 件ごとに Scene を増やさない。

**VFX の粒度と寿命（program §2.4）。**

- ヒット、破裂、短い演出など一時的な VFX は Scene にせず prefab / `VisualEffectAsset` にする。
- 一時 VFX は `IAssetManagement` でロードし、`AssetOwner.Scene(eventSceneIdentity)` または `AssetOwner.Bind(go)` で寿命を宣言する。

**物理フォルダ（program §2.5）。** `Seasons/Spring/Cells/Spring_Cell_4_2/Spring_Events_4_2/Spring_Events_4_2.unity`（repo 相対 path は `unity/Assets/SampleGame/InGame/InGameSession/` から始まる。最長の想定 `…/Spring_Events_4_2/Spring_Events_4_2_Burst.vfx.meta` で 130 文字前後、予算 150 以内）。

**Companion profile（program §3.2）。** AppConfig key `world:cellCompanionSet`。`Full`: Environment + Lighting + VFX + Events／`Planner`: Events のみ／`Lighting`: Environment + Lighting／`VFX`: Environment + Lighting + VFX。既定は `Full`、不明な値は設定エラー。`SessionCellCompanionLoadDriver` はロード済み Cell の `SceneResource.Children` を列挙し、親 identity から名前を組み立てない。role 判定は SampleGame の制作規約に閉じ、Framework は職種を知らない。role identity はロード対象選別だけに使い、座標・bounds・辞書 key の導出には使わない。

**World Workspace の作成（program §3.4）。** Planner: Whitebox Cell + optional Cell Events だけを開く。存在しない Lighting / VFX / Events を作る操作は、`.unity`、`SceneResource`、親子 link、Map、Addressables entry を同じ Editor transaction で作る。Scene / asset YAML は直接編集しない。

**VFX Graph（program §6.1）。** `com.unity.visualeffectgraph` `17.6.0`。PC Quality で描画を必須受入、Mobile は起動時に backend が対応可否を返し、非対応なら `VisualEffect` を有効化しない。診断ログは Framework が一度だけ出し、backend と Scene 側は出さない。

**実証 Scene（program §6.2、d4b の分）。** `Spring_Events_4_2` は Cell 内 trigger から local VFX を起動する。Planner Play は `content:representation=Whitebox` と `world:cellCompanionSet=Planner` を使う（Planner 実証の全体は d5）。

**§6.3 の d4b 行（A0 入力。凍結済みの条件ではない）。**

- 答える問い: Cell 内の trigger から VFX を起動し、Cell unload（と所有 go の破棄）で確実に回収できるか。
- 範囲: `Spring_Events_4_2`。trigger と、`AssetOwner.Bind(trigger の go)` で起動する VFX。asset は `SceneBase.LoadSceneScopedAssetAsync`（`AssetOwner.Scene(Events の identity)`）でロードし、unload の順序（PreUnloading → Unity Scene unload で `OnDestroy` → `ReleaseScene`）で「VFX の回収 → asset の解放」を保つ。Events 用の新しい Scene class を作らず、`CellCompanionScene` が root から trigger を集める（internal の `CellCompanionRoleClassifier` を DependOnAll から呼べないため、`GameSceneFactory` に role 分岐を足さない）。trigger 1 つ分の論理（準備前の進入はロード完了時に起動、再入で前の handle を即 `Stop`、退出で `StopAllowingParticlesToFinish`）は純 C# の session に置いて偽 `IVfxSystem` で単体テストする。trigger の `.vfx` の Addressables entry は Editor 操作で API から足す。Planner profile は Events だけを読むため、trigger は `Spring_VFX_4_2` に依存しない。trigger の VFX は gameplay layer に属する。
- d1 から: `Bind(go)` 用 notifier の `OnDestroy` が実 Scene unload と go の Destroy で回収を呼ぶことの Play Mode 確認、source asset のロード / `Spawn` / 所有者の終端 / source の Release の前後関係が見える実例、同じ identity の Scene の再ロード後の再受理を実 `TestableSceneDirector` で 1 往復（合成 `Subject` では `VfxSystemOwnerTests.AfterRemoved_StillRejected_UntilPreLoading_AndOldHandleDoesNotRevive` が確認済み）。
- 外部レビューから（必須検討）: asset のロード中に Cell unload が始まり後からロードが完了しても、終了した trigger が Spawn せず source が残らないこと。新しいキャンセル機構は作らず、既存の CancellationToken と所有者の終端で確かめ、足りなければ session が完了したロードの handle をすぐ Release する。Framework 側の欠落は所有スライス未定の後続の入力。Events Scene が `Build Spring Full` と `Build Spring Whitebox` の両方に Scene payload として入ること（Player の動作の証明ではない）。
- 対象外: 独立 Scene の大型 Event（d5）、Content Directory の非 Scene asset の production 経路（BuildSystem の実装系、所有 HANDOFF 未作成）。

**VFX 基盤の責務配置（program §6.4、2026-10-17 発注者決定。d4b に関わる分）。**

- 抽象は Framework、VFX Graph に依存する実装は SampleGame。
- Framework の責務: 寿命の宣言と回収（Framework が回収するのは再生中の instance で、asset の寿命は `IAssetManagement` に残す。所有者の unload は消え切り待ちの Stop を打ち切って回収する）、非対応環境での停止（何もしない handle と診断ログ 1 回）、型に依存しない handle。
- source asset の寿命: `Spawn` は呼び出し側がロード済みの `UnityEngine.Object` を受け取り、Framework は asset handle を保持・Release しない。呼び出し側は、その source から作った instance が回収されるまで source を有効に保つ。asset と VFX に同じ `AssetOwner` を使っても解放順は保証されない。
- 回収の保証範囲: Framework が保証するのは、所有者の終端の後に backend へ破棄を要求する時点と回数。
- d1 の寿命管理（`Bind(go)` を含む動的な起動）の対象は d4b / d5。

**公開 API と依存（program §10）。** Framework 公開面の追加は限定する。VFX の公開面は d1・d2・d4a で決めたもの（`IVfxSystem`（`Spawn` / `Adopt`）、`VfxSystem`、`IVfxHandle`、`VfxSpawnRequest`（`TimeLayerId` / `WithUnscaledTime`）、`IVfxBackend`、`VfxInstanceId`、`AbstractApplicationInitializer.CreateVfxBackend()` / `Vfx`、`UpdateLayerIds.Vfx` / `Gameplay` と order 定数、`UpdateCoordinator.TryGetLayer`）。Framework は season 名・Cell 座標文法・職種 role を知らない。Game → Framework の依存方向を維持する。VFX Graph 依存は SampleGame（`SampleGame.Common`）に閉じ、InGame に `VisualEffect` 型を出さない。`SceneState` の 14 値は変更しない。新しい LoadType も追加しない。

**検証方針（program §11 の S-4d のうち d4b）。** local Event は Cell unload で回収される。Mobile 非対応時に例外、未解放 handle、無限 retry がない。658 Scene を個別に目視しない。`unity test` / `unity run`、Cloud での Unity CLI、Scene / asset YAML 手編集は禁止。Phase B は Editor 操作・compile 確認まで、Unity テストと Build は Phase C。Phase C は Editor を適切に閉じ、標準の `pwsh tools/run-tests.ps1` と必要な Build を実行する。人間の Editor を強制終了しない。

**スライス境界（program §12）。** program の §6.3 / §6.4 に無い公開 API、依存、所有者、寿命は d4b の Phase A で決め、program の該当行も Phase A の PR で更新する。Phase B で決めない。Phase D は Phase A で選んだ既存の Architecture 文書へ harvest し、この HANDOFF を削除し、program の §6.3 d4b 行を完了に更新する（program 本体は残す）。

### 1.3 A0 の現況（`9d0c8f2f`）

**VFX 抽象（Architecture §24 §16 が正。d4a までで入ったもの）。**

- `IVfxSystem.Spawn(in VfxSpawnRequest, AssetOwner)` → `IVfxHandle`（`IsActive` / `Play` / `Stop` / `StopAllowingParticlesToFinish` / Set* / `Dispose`）。`VfxSpawnRequest(source, position, rotation)` は親なし（backend が遅延生成する `DontDestroyOnLoad` の root の下に作る）で、時間の領域の既定は gameplay layer。
- 所有者の終端: `Scene(id)` は `SceneDirector` の `StateChanged(PreUnloading | LoadCanceled)`、`Bind(go)` は internal の `VfxOwnerDestroyNotifier.OnDestroy`（同期）か、通知が届かない破棄なら go が `== null` になった後の最初の `Tick`。終端中・未ロードの Scene、破棄済みの go、`source == null` の `Spawn` は backend を呼ばずに何もしない handle を返す（ログなし）。
- 開始の契約（§16.8）: 領域が止まっている間の `Play` は保留。`Play()` の可否は呼び出し時点の領域の状態で決まり、反映点（`Vfx` layer の LateUpdate）より後に Resume した同じフレームの `Play` はそのフレームに開始する（d4a の DR-1 で I1 違反としないと裁定済み）。**いまの SampleGame に layer を pause / resume するコードは無い。**
- `CellCompanionScene`（51 行）は親が `StreamByDistance` の子（Environment / Lighting / VFX / Events の全て）の Scene class で、`OnInitialize` で `PlacedVfxAdopter.AdoptAll`（marker を集めて `Scene(id)` 所有で `Adopt`。VFX を置いていない Scene では何もしない）を呼ぶ。ctor で `IVfxSystem` と `ILoggerFactory` を受け取る。`GameSceneFactory` が構造（親が `StreamByDistance`）だけで分類し、role を見ない。
- `CellCompanionRoleClassifier` / `CellCompanionRole` は `SampleGame.InGame` の internal（`Spring_Events_4_2` → `Events` は `CellCompanionPolicyTests` で確認済み）。
- `VfxSystemSceneDirectorTests`（OneStarMaker.Tests、293 行、実 `TestableSceneDirector`）に、同じ identity の再ロード後の再受理のテストは無い。`VfxSystemOwnerTests.cs` は 828 行。
- `FakeVfxBackend`（OneStarMaker.Tests の internal）は SampleGame.Tests から使える（`OneStarMaker.Tests` の `InternalsVisibleTo("SampleGame.Tests")`、SampleGame.Tests の asmdef が `OneStarMaker.Tests` を参照）。SampleGame の VFX の Play テストは `VfxPlayTestFixture` と、test が持つ `SceneDirector`（実 `SceneResource` の複製で Season を根にした Map）で実 Scene を載せる方式（`VfxScenePlayTests`）。

**Scene の lifecycle と asset のロード（読み。テストでの確認は §2.3）。**

- `SceneBase.LoadSceneScopedAssetAsync<T>(AssetKey, ct)` は `Assets.LoadAssetAsync<T>(key, AssetOwner.Scene(identity), ct)`。既存の使用例は `SeasonScene.OnPreLoadedImpl`（`AssetKey.FromAddress(<asset path>)`）。
- `AssetManagement.LoadAssetAsync` は backend のロードを await した後、所有者の Scene の終端を確かめずに registry へ登録する（`AssetManagement.cs:52-74`）。所有者の Scene が先に `ReleaseScene` された後にロードが完了すると、その asset は誰にも解放されない。**この欠落は Framework 側に残る（d4b は直さない。後続の入力）。**
- `SceneDirector` の Add の流れ: PreLoad（キャンセル窓。`LoadCts` あり）→ ポイント・オブ・ノーリターンで `LoadCts = null` → `LoadUnityScene(..., CancellationToken.None, ...)`（Unity Scene のロード → 子 → `Initialize(roots)`（`OnInitialize`）→ `ExecuteLoaded(ct)` で `OnLoadedImpl`）→ Initializing → Stable → **保留 unload があればここで `RemoveScene`**。`OnLoadedImpl` に渡る ct は `CancellationToken.None`。
- `UnloadScene` は、ロード中の Scene に対して、キャンセル窓の中なら `LoadCts.Cancel()`、窓の外（`OnLoadedImpl` の実行中を含む）なら `_pendingUnloads` に入れて Stable 到達後に `RemoveScene` する。親 Cell の再帰 unload（`CollectLoadedDescendants`）も、ロード中の子は同じ規則（キャンセルか保留）に倒し、保留した子は子自身の Add の流れが Stable の後に `RemoveScene` する。
- したがって、**asset のロードを Events Scene の `OnLoadedImpl` の中で await すれば、その Scene の `ReleaseScene` はロードの完了より前に起きない**（読み）。キャンセル窓の中（`OnPreLoadedImpl`）でロードした場合は、キャンセル時に `CleanupCanceledScene` が `ReleaseScene` を呼ぶが、await 中のロードが後から完了すると上の欠落に当たる。
- 通常の unload の順序（`RemoveScene` の 3 フェーズ）: `PreUnloading`（Scene 所有の VFX を回収、`OnPreUnLoadedImpl`）→ Unity Scene unload（Scene 内の go の `OnDestroy` → `Bind` 所有の VFX を回収）→ `AfterUnloading`・`ReleaseScene(identity)`（`Scene(identity)` 所有の asset を解放）→ `Removed`。
- `IAssetDiagnostics.GetOwners(AssetKey)` で、ある asset を現在所有している owner の一覧を読める（未ロードなら空）。
- program §2.1 の注記: Framework の `IncrementalAlways` の子は、ロード中に親が unload されると保留 unload が消費されずに残る（別の Framework スライス、所有 HANDOFF 未作成）。Events は `OnDemand` で companion loader が明示 `AddScene` するので、この経路は通らない（読み）。S-4a の受入（親 Cell の Unload 中または Add 完了の競合時に子を残さない）は `SessionCellCompanionLoadDriverTests` が持つ。

**SampleGame の Scene と制作。**

- `Spring_Cell_4_2` の子に Environment / Lighting / VFX（d3）がある。**`Spring_Events_4_2` は未作成**（`.unity`・`SceneResource`・Addressables entry とも無い）。World Workspace の Planner role の作成 transaction（`WorldCompanionCreationPlan.Create`、role Planner → `Events`）で作れる。
- 体積（S-4d3 の N-9）: Cell の体積の再計算は `StreamByDistance == false` の子だけを親へ畳み、収集は `Renderer`（`includeInactive: true`）に限る。Renderer を置かない Events Scene は Cell の `_volume` を変えない見込み（読み。§2.3 の AC で確かめる）。
- Player は `FlyController`（`[RequireComponent(typeof(Rigidbody))]`、補間あり、回転固定）。物理 trigger（`Collider.isTrigger` + `OnTriggerEnter` / `OnTriggerExit`）で検出できる。physics の callback は FixedUpdate の段で、`Update` より前に走る。
- `PlacedVfxAuthoringTests` は `Seasons/` 配下の全ての `.vfx` が Delta Time + Ignore Time Scale（`m_UpdateMode: 3`）であることを確かめる。新しい `.vfx` もこの規約に従う。
- InGame は `VisualEffect` 型を使わない（d4a の I6、`SampleGame/InGame` に `UnityEngine.VFX` の using 0）。

**BuildSystem との接点（主目的の観点）。**

- Content Directory の production 候補は Scene payload だけ（`ScenePayloadMappingPolicy`）。Events Scene は World Workspace の transaction で Addressables entry と Map に入るので、`Build Spring Full / Whitebox` に Scene payload として入る見込み（d3 の VFX Scene と同じ経路）。
- Events Scene が `IAssetManagement` で動的にロードする `.vfx` は Scene の依存ではないので、Content Directory には入らない。Editor Play は Addressables の AssetDatabase 経路でロードする。
- BS4 Spring Player は Title の smoke の後に `Application.Quit(0)` し、InGame の Spring に入らない（S-9 へ送り済み）。

---

## 2. 意思決定と受け入れ境界（A1）

- **このスライスが答える問い:** Cell の Events Scene に置いた trigger から `IVfxSystem` 越しに VFX を起動し、Cell の unload と trigger の GameObject の破棄で、source asset の解放より前に、漏れなく 1 回ずつ回収できるか。

### 2.1 不変条件（起きてはいけないこと）

- **I1 回収漏れ・二重回収なし:** `Spring_Events_4_2` の unload（親 Cell の再帰 unload を含む）の後、その Scene の trigger から起動した全ての instance について、backend `Destroy` が instance ごとに高々 1 回で、Events の `Removed` の時点で全て 1 回呼ばれている。`Removed` の後に、その instance の `VisualEffect` の GameObject が残っていない。
- **I2 source より先に instance:** trigger の VFX の各 instance の backend `Destroy` が呼ばれた時点で、その source asset はまだ `Scene(Spring_Events_4_2)` に所有されている（`IAssetDiagnostics.GetOwners` に含まれる）。Events の `Removed` の時点で、その asset の owner は 0 件。
- **I3 終わった trigger は起動しない:** trigger が終わった後（Events Scene の `PreUnloading` 以降、または trigger の GameObject の破棄の後）、その trigger から `Spawn` が呼ばれない。asset のロードの完了が終わりより後に届いた場合も同じで、そのとき source asset は Events の `Removed` の時点で残っていない。
- **I4 Planner で VFX Scene に依存しない:** `Spring_VFX_4_2`（と Environment）をロードしない状態で、trigger の起動・回収が例外なく成り立つ。
- **I5 非対応環境:** 非対応の `VfxSystem` で trigger に進入しても、例外、`QueryCapability` 以外の backend 呼び出し、起動時の 1 件以外の診断ログが無い。Events の unload の後に source asset が残らない。
- **I6 他の companion を変えない:** trigger を持たない companion（Environment / Lighting / VFX）の振る舞い・ログ・ロード時間の経路を変えない（trigger が無ければ asset をロードしない）。Events Scene の追加で `Spring_Cell_4_2` の `_volume` が変わらない。
- **I7 常時契約:** `SceneState` 14 値不変、Game → Framework の一方向、Framework の公開 API の diff 0、asmdef 参照の追加 0 件、`SampleGame/InGame` に `UnityEngine.VFX` の using 0、Unity の偽 null を `== null` / `!= null` で判定する、テストで `Task.Delay` / `Thread.Sleep` を使わない。

### 2.2 成立条件（起きなければならないこと）

- **E1 trigger で起動:** Rigidbody を持つ物体が trigger に入ると、trigger の位置・回転で VFX instance が 1 つ作られ、`Play` が届く。所有者は `Bind(trigger の go)`、時間の領域は gameplay。
- **E2 準備前の進入:** asset のロードが終わる前に入っていた場合は、ロードの完了の時点で E1 と同じように起動する（入って出た後なら起動しない）。
- **E3 再入と退出:** 退出で、その trigger の handle に `StopAllowingParticlesToFinish`。退出の後の再入、または handle が残ったままの再入では、前の handle を即 `Stop` してから新しく起動する（trigger ごとに生存 instance は高々 1 つ）。
- **E4 Cell unload で回収:** 親 Cell `Spring_Cell_4_2` の unload で、E1 の instance が I1・I2 のとおり回収される。
- **E5 go の破棄で回収:** Play 中に trigger の go を `Object.Destroy` すると、その go に `Bind` した instance の `Destroy` が、そのフレームの破棄（`OnDestroy`）の中で呼ばれる（次のフレームの `Tick` を待たない）。
- **E6 再ロード後の再受理:** 同じ identity の Scene を unload → 再ロードした後、再び起動できる（Framework: 実 `TestableSceneDirector` の 1 往復、SampleGame: 実 Scene で Cell を載せ直して trigger から再び `Create`）。
- **E7 content build:** `Tools/OSM/Content/Build Spring Full` と `Build Spring Whitebox` の両方の Content Directory に、`Spring_Events_4_2` が Scene payload として入る（Player の動作の証明ではない）。

### 2.3 受け入れ条件（検証する条件を併記）

| # | 内容 | 条件 | 失敗経路 |
|---|---|---|---|
| AC1 | 単体（偽 `IVfxSystem`）: session が準備済みで進入 0→1 なら `Spawn` 1・`Play` 1。owner は `Bind(trigger)`、request は trigger の位置・回転、`TimeLayerId` は gameplay | E1 | |
| AC2 | 単体: 準備前に進入 → `Spawn` 0 → 準備完了で `Spawn` 1・`Play` 1。準備前に進入して退出 → 準備完了で `Spawn` 0 | E2 | ○ |
| AC3 | 単体: 退出（在室 1→0）で `StopAllowingParticlesToFinish` 1。再入で前の handle に `Stop` 1 → 新しい `Spawn` 1。同じ Rigidbody の 2 つ目の Collider の進入・退出で在室数が変わらない | E3 | ○ |
| AC4 | 単体: 終了（`End`）の後の進入、終了の後に届いた準備完了のどちらでも `Spawn` 0。準備の失敗（`Fail`）の後の進入で `Spawn` 0・例外 0 | I3 | ○ |
| AC5 | Framework（OneStarMaker.Tests、`TestableSceneDirector` と既存の偽 asset backend）: `OnLoadedImpl` で Scene 所有の asset のロードを待っている間に、その Scene の unload（直接、と親の再帰 unload の両方）を要求する → ロードを完了させる → `Removed` の時点で `GetOwners(key)` が 0 件、unload は Stable の後に 1 回だけ走る | I3（source が残らない）、§1.3 の読みの確認 | ○ |
| AC6 | Framework（`VfxSystemSceneDirectorTests` に 1 件）: `Scene(id)` 所有の `Spawn` → unload → 同じ identity を再ロード → `Spawn` が受理され `Create` 1 → unload で `Destroy` 1。古い handle は生き返らない | E6 | ○ |
| AC7 | Play（実 Scene、test の `SceneDirector`、偽 backend `FakeVfxBackend`）: Cell 4_2 と Events 4_2 だけを載せる（`Spring_VFX_4_2`・Environment は載せない）。kinematic `Rigidbody` + Collider の test 用 go を trigger の内へ動かし FixedUpdate を通す → `Create` 1・`Play` 1、位置が trigger と一致 | E1, I4 | |
| AC8 | Play（AC7 の続き）: Cell を unload。記録する列が「`GetOwners(key)` が `Scene(Events)` を含む状態での `Destroy`（instance ごとに 1）→ Events の `Removed`（このとき `GetOwners(key)` 0 件）」。source asset のロード / `Create` / `Destroy` / `Removed` の前後をテストのログに出す（d1 の「前後関係が見える実例」） | E4, I1, I2 | ○ |
| AC9 | Play（実 backend `VfxGraphBackend`、`-WithGraphics`）: AC7・AC8 と同じ操作で、起動中は trigger の位置に有効な `VisualEffect` が 1 つあり、Events の `Removed` の後に trigger の VFX の `VisualEffect` が 0 件（`DontDestroyOnLoad` の root を含む） | E1, I1 | ○ |
| AC10 | Play（偽 backend）: trigger の go を `Object.Destroy` したフレーム N に、backend `Destroy` が 1 回呼ばれ、記録したフレーム番号が N（`Tick` の回収なら N+1 になる）。その後の Cell unload で同じ instance の `Destroy` は増えない | E5, I1 | ○ |
| AC11 | Play（偽 backend）: Cell を unload → 同じ Map で Cell と Events を再び載せる → 進入で `Create` が再び 1（合計 2）、古い instance の `Destroy` は 1 のまま | E6 | |
| AC12 | Play: 非対応の `VfxSystem`（`UnsupportedFacts`）で AC7・AC8 の操作 → 例外 0、`QueryCapability` 以外の backend 呼び出し 0、診断ログは起動時の 1 件だけ、Events の `Removed` で `GetOwners(key)` 0 件 | I5 | ○ |
| AC13 | 単体または Play: trigger の asset のロードが失敗する（存在しない address）→ Events Scene のロードは成功し（§2.5 D4 の U2 案）、Error ログは trigger ごとに 1 件、進入で `Spawn` 0 | I3, U2 | ○ |
| AC14 | authoring（EditMode）: `Spring_Events_4_2` に `LocalVfxEventTrigger` が 1 つ以上あり、それぞれ同じ go に `isTrigger` の Collider を持ち、address が空でなく `Seasons/` 配下の `.vfx` の Addressables entry を指す。Events Scene に `VisualEffect`・`PlacedVfxActivator`・`Renderer` が無い。`.vfx` は Update Mode 3（既存の `PlacedVfxAuthoringTests` の規約） | §1.2 の所有物の契約、I6 | ○ |
| AC15 | authoring（EditMode）: `Spring_Cell_4_2` の `SceneResource` の `_volume` が base（`9d0c8f2f`）と同じ。`Spring_Events_4_2` の `SceneResource` は `StreamByDistance = false`・`LoadType.OnDemand`・親が `Spring_Cell_4_2` | I6、§1.2 | ○ |
| AC16 | 判定 C: `Build Spring Full` と `Build Spring Whitebox` が成功し、両方の Content Directory に `Spring_Events_4_2` が Scene payload として入る | E7 | |
| AC17 | 既存の回帰: `VfxScenePlayTests`（d3・d4a の配置済み VFX）と `SessionCellCompanionLoadDriverTests` が変わらず通る。`contract-audit` 通過、asmdef・`SceneState`・Framework の公開 API の diff 0、`SampleGame/InGame` に `UnityEngine.VFX` の using 0 | I6, I7 | ○ |

**読み方の固定（合格例・不合格例）。**

- AC3 の「在室」: 進入者は `attachedRigidbody` を持つ Collider で、Rigidbody ごとに数える。合格: 同じ Rigidbody の 2 つの Collider が順に入っても `Spawn` は 1。不合格: Collider ごとに数えて 2 回起動する。`attachedRigidbody` を持たない Collider（静的な地形など）は数えない。
- AC10 の「そのフレーム」: `Object.Destroy` は破棄をフレームの終わりまで遅らせる。合格: `Destroy` の記録が `Object.Destroy` を呼んだフレームの番号。不合格: 次のフレームの番号（`Tick` の偽 null 検査で回収した）。
- I3 の「終わり」: Events の `PreUnloading` で全 session を終わらせ、trigger の `OnDestroy` でもその session を終わらせる（早いほう）。合格: `PreUnloading` と Unity Scene unload の間に physics の進入が届いても `Spawn` 0。

- **ここでは答えない問いと所有する後続スライス:** 大型 Event と Planner 実証の全体（S-4d5）。trigger の VFX asset を Content Directory・Player build に乗せる経路と、Content Directory 上で trigger の VFX が起動すること（BuildSystem の実装系、所有 HANDOFF 未作成）。`AssetManagement.LoadAssetAsync` の await の後の所有者確認の欠落（Framework、所有スライス未定）。Player での描画と Spring に入る Player の観測経路（S-9）。退出の通知が来ない破棄・非 active 化（進入中の Rigidbody が trigger の中で消えた場合。在室が残り、Cell unload まで VFX が残る。所有スライス未定）。ParticleSystem fallback（後続の独立スライス）。
- **判定定義:** GO = §2.1・§2.2 の全条件を AC で確認できた。NO-GO = いずれかの不変条件の違反、または成立条件の不成立。CONDITIONAL ACCEPT は使わない（スパイクではない）。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- **A3 後の例外承認:** なし
- **決定記録:** なし

### 2.4 Architecture §16.7 の S-4d4b 項目の扱い

| 項目 | 扱い |
|---|---|
| notifier の `OnDestroy` が実 Scene unload と go の Destroy で回収を呼ぶことの Play Mode 確認 | 採用（AC8・AC10） |
| 同じ identity の再ロードを実 `SceneDirector` で 1 往復 | 採用（AC6・AC11） |
| d2 のテストが直接見ていない経路のうち、`Bind` 所有者の破棄から 1 フレーム以内の回収 | 採用（AC10 は同じフレームを求める） |
| 同、`Create` 直後に発射しないことの長い観測窓、host の Update 登録の失敗、backend の `Create` が GameObject の作成後に失敗した場合の後始末 | 不採用（d4b の問いの判定に要らない。§16.7 に残す） |
| graphics なしの既定の回帰で実 `VisualEffect` の Play テストが Inconclusive になる | 記録のみ。d4b の実 backend の AC は AC9 だけにし、残りの Play テストは偽 backend で graphics なしでも走る形にする |
| 反映点より後に Resume した同じフレームの `Play` | 条件を足さない。trigger の `Play` は physics の callback（FixedUpdate の段、`Update` より前）か、`OnLoadedImpl` の完了の続きで呼ばれ、いまの SampleGame に layer を pause / resume するコードは無い。契約は §16.8 と d4a の DR-1 のまま |
| d1 の C / C' から: `BindOwner_DestroyedWithoutNotification_ReleasedOnNextTick` の Tick 前 `Destroy`=0、`VfxSystemOwnerTests.cs` の分割 | 不採用（d4a の判断を維持。d4b はこのファイルに追加しない） |

### 2.5 A1 の設計

**D1. Events Scene の作成（Editor 操作）。** World Workspace の Planner role の作成 transaction で `Spring_Events_4_2`（`.unity`、`SceneResource`、親子 link、Map、Addressables entry）を作る。trigger の go と `.vfx` は Editor の API で置く。そのための一時スクリプトは commit の前に消す（判断基準「残すコードは SampleGame に必要なものだけ」）。Scene / asset YAML は直接編集しない。

- trigger は 1 つ。go に `BoxCollider`（`isTrigger`）と `LocalVfxEventTrigger` を置き、Renderer は置かない（I6）。位置は `Spring_Cell_4_2` の地表の上で、Player が飛行で通れる場所（B が Editor で決め、§6 に記録する）。
- `.vfx` は `Spring_Events_4_2/` に 1 つ（例: `Spring_Events_4_2_Burst.vfx`）。既存の `.vfx` の複製から作り、短い発射（Single Burst 等）と、Update Mode 3（Delta Time + Ignore Time Scale）にする。culling は既定のまま（動的な一時 VFX。カメラの無い Play テストでは粒子を観測しない）。
- `.vfx` の Addressables entry は Addressables の API で、Scene の entry と同じ group に足す。address は asset path（`SeasonScene` の既存の規約と同じ）。

**D2. trigger の component（`LocalVfxEventTrigger`、SampleGame.InGame、MonoBehaviour）。** authoring の面と physics の callback の受け口だけを持つ。

- serialized: `_vfxAddress`（string。`.vfx` の Addressables address）。`VisualEffectAsset` を直接参照しない（Scene の依存にすると §2.4 の「`IAssetManagement` でロードし寿命を宣言する」に反し、InGame に VFX の型も入る）。
- `OnTriggerEnter` / `OnTriggerExit` で `other.attachedRigidbody` を取り、null でなければ `EntityId`（`GetInstanceID` は obsolete-as-error なので使わない）で session に進入・退出を伝える。session が付く前の callback は、在室の集合として component 側で保持し、付いた時点で session へ渡す（Collider は最初から有効。Unity Scene のロード直後から進入を記録する）。
- `OnDestroy` で session を終わらせる（I3）。

**D3. trigger 1 つ分の論理（`LocalVfxEventSession`、SampleGame.InGame の internal、純 C#）。**

- 入力: `IVfxSystem`、所有者 `AssetOwner`（`Bind(trigger の go)`）、位置・回転。状態は `Preparing` → `Ready`（source を受け取る）/ `Failed` → `Ended` の一方向。在室は Rigidbody の id の集合。生存 handle は高々 1 つ。
- 進入（在室 0→1）: `Ready` なら、残っている handle に `Stop` → `Spawn(new VfxSpawnRequest(source, position, rotation), owner)` → `Play`。`Preparing` なら在室だけ記録する。
- 退出（在室 1→0）: handle があれば `StopAllowingParticlesToFinish`（handle は持ったまま。次の進入で `Stop` する）。
- `MarkReady(source)`: `Preparing` のときだけ `Ready` にし、在室が 1 以上なら進入と同じ起動をする。`Ended` / `Failed` なら何もしない。
- `Fail()`: `Preparing` のときだけ `Failed` にする。
- `End()`: `Ended` にして handle の参照を手放す（instance の回収は Framework の `Bind` の終端が持つので、session は `Stop` を呼ばない）。以後の進入・`MarkReady` は何もしない（I3）。
- source asset の handle は session が持たない（所有者は Events Scene の `Scene(id)`。§1.3 の読みのとおり、`OnLoadedImpl` でロードすれば `ReleaseScene` はロードの完了より前に起きない）。そのため「完了したロードの handle を session がすぐ Release する」補助は入れない（AC5 で前提を確かめる）。

**D4. `CellCompanionScene` が trigger を束ねる。** 新しい Scene class と factory の role 分岐は作らない（§1.2 の d4b 行。internal の role classifier を DependOnAll から呼べない）。

- `OnInitialize`: 既存の `PlacedVfxAdopter.AdoptAll` に加え、root から `LocalVfxEventTrigger`（`includeInactive: true`）を集め、各 trigger に session を作って付ける。共通処理は InGame の internal の小さい class（`LocalVfxEventBinder`）に置く（`PlacedVfxAdopter` と同じ形）。trigger が無ければ何もしない（I6。Environment 216 件は root の走査だけ）。
- `OnLoadedImpl`: 各 trigger について `LoadSceneScopedAssetAsync<UnityEngine.Object>(AssetKey.FromAddress(address))` を await し、`MarkReady(source)`。InGame は VFX の型を知らないので `UnityEngine.Object` で受け、型の検査は backend の `Create` に任せる（`VisualEffectAsset` でなければ何も作らない。§16.6）。
- **U2（案）: ロードの失敗は Events Scene のロード失敗にしない。** 例外（`OperationCanceledException` を除く）を捕まえて Error ログを trigger ごとに 1 件出し、`Fail()` で trigger を止め、Scene のロードは続ける。理由: trigger の VFX は表示の演出で、Events Scene の他の進行制御まで止めない。Content Directory を使う Player では、この `.vfx` が directory に入らないので（U1）、失敗を Scene のロード失敗にすると Events 全体が載らなくなる。失敗はログで見える。
- `OnPreUnLoadedImpl`: 全 session を `End()`（I3。`PreUnloading` と Unity Scene unload の間に届いた physics の進入で `Spawn` しない）。
- 寿命: asset は `Scene(Events の identity)`、VFX は `Bind(trigger の go)`。unload の順序（`PreUnloading` → Unity Scene unload で `OnDestroy` → `ReleaseScene`）で「VFX の回収 → asset の解放」が保たれる（I2）。
- **U3（案）: 進入者は `attachedRigidbody` を持つ Collider（Rigidbody ごとに数える）。** Player に限らない。いまの Cell に Player 以外の Rigidbody は無い。Player だけに絞るには Player を識別する規約（tag や marker）を新しく足す必要があり、d4b の問いの判定には要らない。

**D5. テストの経路。** 純 C# の session は偽 `IVfxSystem`（SampleGame.Tests に置く、記録するだけの小さい実装）で単体テストする。実 Scene の Play テストは `VfxScenePlayTests` と同じ test の `SceneDirector` で `Spring_Cell_4_2` を載せ、`Spring_Events_4_2` を `AddScene` する（Season を根にした複製の Map。companion loader と距離ストリーミングは動かさない）。lifecycle の AC は `FakeVfxBackend` で graphics なしでも走らせ、実 `VisualEffect` の有無だけを AC9（`-WithGraphics`）で見る。

**採らなかった案。**

- **Events 用の Scene class（`CellEventsScene`）と factory の role 分岐:** internal の role classifier を DependOnAll から呼べず、構造だけでは Events と他の companion を区別できない（分割前の A2 アーキ A1）。
- **trigger が `VisualEffectAsset` を直接参照する（Scene の依存にする）:** Content Directory と Player build に Scene の依存として乗るので、主目的（BuildSystem）の観点では経路が単純になる。一方、program §2.4 の「一時 VFX は `IAssetManagement` でロードし寿命を宣言する」に反し、InGame に VFX の型が入り、asset の寿命が Scene の寿命に暗黙に結び付く。U1 で非 Scene asset の経路を BuildSystem に移した裁定とも合わない。
- **初回の進入でロードする（遅延ロード）:** ロードと unload が競合し、§1.3 の Framework の欠落に当たる。`OnLoadedImpl` で先にロードすれば、SceneDirector の保留 unload の規則で競合が構造的に起きない。
- **`OnPreLoadedImpl`（キャンセル窓）でロードする:** Unity Scene のロード前なので trigger の address を読めない。キャンセル時に await 中のロードが後から完了すると欠落に当たる。
- **VFX の親を trigger の transform にする:** Unity の Scene unload が VFX の GameObject も一緒に壊すので、Framework の回収が漏れても I1 の「GameObject が残らない」が偶然成り立ってしまい、AC9 が回収漏れを見逃す。親なし（backend の `DontDestroyOnLoad` の root）なら回収漏れが残骸として見える。

### 2.6 未決事項

- **U1（裁定済み、発注者 2026-10-27）:** trigger の VFX asset を Content Directory・Player build に乗せる経路は BuildSystem の実装系へ。d4b は Content Directory 上で trigger の VFX が起動することを条件にしない。
- **U2（A3 で裁定）:** trigger の asset のロード失敗を Events Scene のロード失敗にしない案（D4）。
- **U3（A3 で裁定）:** 進入者を `attachedRigidbody` を持つ Collider とする案（D4）。

---

## 3. 責務マップ（A1）

| ファイル | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `SampleGame/InGame/InGameSession/World/LocalVfxEventSession.cs`（新規、internal） | trigger 1 つ分の状態（準備・在室・handle）と起動・停止の判断 | trigger の振る舞いの規則 | session は trigger の go の寿命。instance は `Bind(trigger)`、source は呼び出し側 | `IVfxSystem`、`VfxSpawnRequest`、`AssetOwner` | internal | 偽 `IVfxSystem` で単体（AC1〜AC4） | 0 → 130 |
| `SampleGame/InGame/InGameSession/World/LocalVfxEventTrigger.cs`（新規、MonoBehaviour） | authoring の面（address）、physics の callback から session への転送、付く前の在室の保持、`OnDestroy` で終了 | authoring と Unity の受け口 | Scene 内の go | session | public（MonoBehaviour）、serialized field 1 | authoring EditMode（AC14）、Play | 0 → 80 |
| `SampleGame/InGame/InGameSession/World/LocalVfxEventBinder.cs`（新規、internal static） | root から trigger を集め session を付ける、asset をロードして準備完了を伝える・失敗を記録する、全 session の終了 | Events の制作契約の lifecycle の部分 | — | `IVfxSystem`、`IAssetManagement` の Scene スコープのロード（delegate で受ける）、logger | internal | Play（AC7〜AC13） | 0 → 90 |
| `SampleGame/InGame/InGameSession/World/CellCompanionScene.cs` | `OnInitialize` / `OnLoadedImpl` / `OnPreUnLoadedImpl` から binder を呼ぶ | 呼び出しの追加 | Scene | 既存 | 変更なし | Play | 51 → 85 |
| `OneStarMaker/Tests/Rendering/Vfx/VfxSystemSceneDirectorTests.cs` | 再ロード後の再受理 1 件（AC6） | テスト | — | — | — | — | 293 → 340 |
| `OneStarMaker/Tests/Scene/`（既存の Scene のテストのうち適切なファイル、または新規 1 本） | `OnLoadedImpl` の Scene スコープのロード中の unload（AC5） | テスト | — | 既存の偽 asset backend | — | — | +120 |
| `SampleGame/Tests/World/LocalVfxEventSessionTests.cs`（新規）と偽 `IVfxSystem` | AC1〜AC4 | テスト | — | — | — | — | 0 → 250 |
| `SampleGame/Tests/Rendering/Vfx/LocalVfxEventPlayTests.cs`（新規） | AC7〜AC13 | テスト | — | `VfxPlayTestFixture`、`FakeVfxBackend` | — | — | 0 → 400 |
| `SampleGame/Tests/Rendering/Vfx/PlacedVfxAuthoringTests.cs` または新規の authoring テスト | AC14・AC15 | テスト | — | — | — | — | 283 → 350 |
| `Seasons/Spring/Cells/Spring_Cell_4_2/Spring_Events_4_2/`（新規 Scene、`SceneResource`、`.vfx`）、Map と Addressables の設定 | 実証 Scene | — | — | — | — | — | Editor 操作 |

- Framework のコードは変えない（テストだけを足す）。公開 API の追加 0、asmdef 参照の追加 0（`SampleGame.InGame` は `OneStarMaker.Runtime` を参照済み）。program §10 は変えない。
- 行数警報: `VfxSystemSceneDirectorTests` 以外の既存ファイルの増加は 50% 未満。`CellCompanionScene.cs` は 51 → 85（+67%）で警報に当たる。非分割の理由: 増えるのは lifecycle の hook から binder を呼ぶ数行で、判断は session と binder に分けた。`LocalVfxEventPlayTests.cs` は 400 行前後の見込みで、500 を超えたら AC の群（lifecycle / 非対応 / 失敗）で分ける。
- 責務の数: session（規則）、trigger（Unity の受け口）、binder（Scene の lifecycle との接続）の 3 つを別ファイルにする。変える理由が違う（trigger の振る舞いの仕様、authoring の形、Scene の lifecycle の仕様）。

---

## 4. 実装計画（A1）

### 4.1 順序

1. `LocalVfxEventSession` と単体テスト（AC1〜AC4）。
2. Framework のテスト 2 件（AC5・AC6）。AC5 が §1.3 の読みと違う結果になったら、直さずに止めて Phase A へ返す（§4.2）。
3. `LocalVfxEventTrigger`、`LocalVfxEventBinder`、`CellCompanionScene` の呼び出し。
4. Editor 操作: World Workspace の Planner role で `Spring_Events_4_2` を作る → trigger と `.vfx` を置く → `.vfx` の Addressables entry を足す。一時スクリプトは commit の前に消す。
5. Play テスト（AC7〜AC13）と authoring テスト（AC14・AC15）。コンパイル確認と `pwsh tools/contract-audit.ps1` まで（Unity テストと Build は Phase C）。

### 4.2 Phase B から Phase A へ差し戻す条件

- §3 に無い公開 API・状態・依存・所有者・寿命が要る。Framework のコードの変更が要る。
- asmdef 参照の追加が要る。
- AC5 で、`OnLoadedImpl` のロード中の unload がロードの完了より前に `ReleaseScene` を呼ぶ（§1.3 の読みが誤り）。その場合は session が完了したロードの handle を Release する案（§1.2 の外部レビューの代替）を A2 で比べる。
- World Workspace の transaction で Events Scene を作れない、または作った Scene が Cell の `_volume` を変える。
- `CellCompanionScene` に trigger の処理を置くと、trigger を持たない companion の振る舞いが変わる。

### 4.3 BuildSystem の既知の課題との関係（判断基準による記録）

- **content build:** Events Scene は World Workspace の transaction で Addressables entry と Map に入り、`Build Spring Full / Whitebox` の Content Directory に Scene payload として入る（AC16、判定必須）。trigger の `.vfx` は Content Directory に入らない（U1）。**BuildSystem の作業候補:** Content Directory の非 Scene asset の production 経路。d4b の trigger の `.vfx` が、Scene 以外の asset を production で引く最初の具体的な使い手になる（address は asset path、owner は `Scene(Events)`）。
- **Player build:** d4b では行わない。理由: BS4 の Player は smoke の後に終了して Spring に入らないので、Player 上で trigger を踏む観測経路が無い（S-9 へ送り済み）。この worktree のルートは 82 文字で path 予算には収まるが、観測経路が無いことが理由である。**BuildSystem の作業候補:** Spring に入る Player の観測経路（変わらず）。
- **Content Directory を使う実行での振る舞い（読み）:** `.vfx` が directory に無いので trigger の asset のロードは失敗し、U2 の案なら Error ログを出して trigger だけが止まる。Events Scene は載る。

---

## 5. テストとレビュー計画（A1）

- **単体テスト（EditMode、Unity の Scene なし）:** `LocalVfxEventSessionTests`（AC1〜AC4）。Framework の `VfxSystemSceneDirectorTests` に AC6、Scene のテストに AC5。
- **Play テスト:** `LocalVfxEventPlayTests`（AC7・AC8・AC10〜AC13 は `FakeVfxBackend` で graphics なしでも走る。AC9 は実 `VfxGraphBackend` で `-WithGraphics`）。進入は kinematic `Rigidbody` + Collider の test 用の go を `Rigidbody.MovePosition` で trigger の内へ動かし、FixedUpdate を通して `OnTriggerEnter` 経路で起こす（待ちはフレームの yield。`Task.Delay` / `Thread.Sleep` は使わない）。
- **authoring（EditMode）:** AC14・AC15。
- **差し戻し中の起点 `-Filter`:** `SampleGame.Tests.World.LocalVfxEventSessionTests`、`SampleGame.Tests.Rendering.Vfx`、`OneStarMaker.Tests.Rendering.Vfx.VfxSystemSceneDirectorTests`、AC5 のテスト。
- **判定必須テスト（GO 候補 head）:** 全 EditMode 回帰（空 filter）、上記 Play テスト（`-WithGraphics` で 1 プロセスにまとめる）、Content Directory の build 2 種（`Build Spring Full`・`Build Spring Whitebox`、AC16）。
- **全 EditMode 回帰の適用除外:** なし
- **操作・実行時・目視条件の検証経路:** E1〜E6 は Play テストで閉じる（構造・値の検査。backend の呼び出し記録、`IAssetDiagnostics.GetOwners`、`VisualEffect` の件数、フレーム番号）。見た目の判断は求めない。E7 は Content Directory の build の出力で確かめる（判定 C の実行担当）。動作確認の対象: なし（offline のテストで閉じない操作・目視の条件が無い）。
- **未知の操作経路:** kinematic Rigidbody と静的な trigger の Collider で、test が `MovePosition` で動かしたときに `OnTriggerEnter` / `OnTriggerExit` が届くこと（Unity の標準の挙動。未確認。初回確認は Phase C、理由: Unity テストが要る。担当: 発見 C の Unity 実行担当。確認地点: AC7 の最初の実行。不成立なら、test 用の go を非 kinematic にして位置を書く等の手段の変更は B 適応、trigger の検出方式を変える必要があれば Phase A へ返す）。
- **人間の判断が必要な条件:** なし
- **高コスト証拠:** なし（Player build を行わない）
- **判定 C の照合 script の方式:** なし
- **機械検査:** `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- **A0/A1 主担当:** この session の主担当（モデル・ベンダーは A3 で実績として記録する）。
- **A2 独立レビュー（予定）:** (1) アーキテクチャゲート（責務の分け方、所有者・寿命、`CellCompanionScene` に置くことの妥当性、テスト境界）、(2) 不変条件の反例探し（I1〜I3 を破る順序、特に unload とロードと physics の callback の競合）と AC の曖昧さ、(3) 高リスクのため A0（§1）だけから代替構成を出すレビュー。同じ入力版を使い、互いの指摘を見せない。C' 用に、A〜C に関与しない系列を 1 つ予約する。
- **A3 統合:** 未実施

## 6. Phase B 実装結果

未着手。

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
