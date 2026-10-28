# S-4d4b local Event

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `C'`（C' 1 巡目完了、阻害する欠陥なし。次は Phase D（人間の GO 判定と、判定 C・C' の突き合わせ）。Phase A は A3 凍結版 revision 1 `c68b6d39`、A2 入力版は `04d833b0`、採否は §5.1）
- branch: `claude/s-4d4b-local-event`
- implementation base commit: `9d0c8f2f`（`develop`、S-4d4a の PR #147 のマージ）
- implementation head commit: `4cb3b8d3`（B 差し戻し 1 巡目の修正 commit。前の実装 commit は `eba9ddaf`。判定 C・C' の GO 候補として固定。ローカル tag `evidence/s-4d4b-local-event/1`、push していない）
- risk: `high`（所有者・寿命の実例、Addressables entry の追加、非同期ロードと unload の競合に触れる。Framework の公開 API は変えない見込み）
- owner: 発注者 / S-4d4b 担当
- created: 2026-10-28
- expires: S-4d4b の Phase D 完了時
- harvest to: Architecture §24 §16（§16.7 の S-4d4b 項目の解消と、Events Scene の trigger から起動する VFX の寿命の実例）、§05 §5.13（Cell の職種 companion のうち Events の振る舞い）
- workflow task: `s-4d4b-local-event`（未開始。最初に Evidence を固定する時点で `pwsh tools/workflow-task.ps1 start` する）
- Phase A snapshot の取得案内・状態: 未生成（凍結前スパイクは計画していない。凍結した HANDOFF 本体は A3 凍結 commit で固定する）
- Phase B result snapshot の取得案内・状態: 未生成（引渡しはローカルの commit SHA で行う。evidence の固定は発見 C が `workflow-task.ps1 start` の後に行う）
- evidence bundle の取得案内・状態: ローカル固定済み（未昇格）。判定 C 用 bundle（head `4cb3b8d3`）。取得案内は進行役の共有メモにある。作成 session と、別 session（進行役）の両方で fetch と内容の閲覧を確認済み
- C' blind bundle の取得案内・状態: ローカル固定済み（未昇格）。所見を除いた C' 1 巡目の入力（head `4cb3b8d3`）。取得案内は進行役の共有メモにあり、判定 C の取得案内とは別に置いた。進行役の session で fetch と閲覧を確認し、その取得した写しを C' に渡した。C' の所見（Phase D 用）も別の bundle としてローカル固定済み（未昇格）で、取得案内は同じメモにある
- 引継ぎ票: C' 1 巡目完了（2026-10-29、対象 implementation head `4cb3b8d3`、阻害する欠陥なし。§8）。判定 C も完了（§7。全 EditMode の Unity 終了 stall は人間が既知リスクとして受容）。次は Phase D: 人間が判定 C（§7）と C'（§8）を突き合わせて GO を判断し、PR 作成（base `develop`）・harvest・HANDOFF 削除へ進む。ブランチは push していない。Evidence はすべてローカル固定済み（未昇格）

---

## 1. 目的と対象外

- **目的:** Cell `Spring (4,2)` の Events Scene `Spring_Events_4_2` に trigger を置き、trigger から VFX を `IVfxSystem` 越しに動的に起動し、Cell の unload と trigger の GameObject の破棄で確実に回収されることを、実 Scene で示す。d1 から引き継いだ動的 VFX の寿命の実証のうち、trigger の `End()`（Scene の `OnPreUnLoadedImpl` か trigger の `OnDestroy`）による回収、source asset と instance の前後関係、同じ identity の再ロード後の再受理をここで確かめる。`VfxOwnerDestroyNotifier` 単独の `OnDestroy` 経路の Play 確認は §16.7 に残す（§2.4。§1.2 の d1 からの転記は当初の A0 入力で、現行の条件は §2）。
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

- **I1 回収漏れ・二重回収なし:** `Spring_Events_4_2` の unload（親 Cell の再帰 unload を含む）の後、その Scene の trigger から起動した全ての instance について、backend `Destroy` が instance ごとに高々 1 回で、Events の `Removed` の時点で全て 1 回呼ばれている（AC8）。`Removed` の**次のフレーム**に、その instance の `VisualEffect` の GameObject が残っていない（AC9。`Object.Destroy` の native の破棄はフレームの終わりまで遅れる）。
- **I2 source より先に instance:** trigger の VFX の各 instance の backend `Destroy` が呼ばれた時点で、その source asset はまだ `Scene(Spring_Events_4_2)` に所有されている（`IAssetDiagnostics.GetOwners` に含まれる）。Events の `Removed` の時点で、その asset の owner は 0 件。
- **I3 終わった trigger は起動しない:** trigger が終わった後（Events Scene の `OnPreUnLoadedImpl` で全 session を `End()` した後、または trigger の GameObject の `OnDestroy` の後）、その trigger から `Spawn` が呼ばれない。asset のロードの完了が終わりより後に届いた場合も同じで、そのとき source asset は Events の `Removed` の時点で残っていない。`PreUnloading` の通知から `OnPreUnLoadedImpl` までの間に届いた進入で `Spawn` されても、その instance は `End()` の `Stop` で回収されるので I1・I2 は保たれる（A2 アーキ F3）。
- **I4 Planner で VFX Scene に依存しない:** `Spring_VFX_4_2`（と Environment）をロードしない状態で、trigger の起動・回収が例外なく成り立つ。
- **I5 非対応環境:** 非対応の `VfxSystem` で trigger に進入しても、例外、`QueryCapability` 以外の backend 呼び出し、起動時の 1 件以外の診断ログが無い。Events の unload の後に source asset が残らない。
- **I6 他の companion を変えない:** trigger を持たない companion（Environment / Lighting / VFX）の振る舞い・ログ・ロード時間の経路を変えない（trigger が無ければ asset をロードしない）。Events Scene の追加で `Spring_Cell_4_2` の `_volume` が変わらない。
- **I7 常時契約:** `SceneState` 14 値不変、Game → Framework の一方向、Framework の公開 API の diff 0、asmdef 参照の追加 0 件、`SampleGame/InGame` に `UnityEngine.VFX` の using 0、Unity の偽 null を `== null` / `!= null` で判定する、テストで `Task.Delay` / `Thread.Sleep` を使わない、新規・編集する `.cs` の先頭に `#nullable enable`、`record` を使わない。

### 2.2 成立条件（起きなければならないこと）

- **E1 trigger で起動:** Rigidbody を持つ物体が trigger に入ると、trigger の位置・回転で VFX instance が 1 つ作られ、`Play` が届く。所有者は `Bind(trigger の go)`、時間の領域は gameplay。
- **E2 準備前の進入:** asset のロードが終わる前に入っていた場合は、ロードの完了の時点で E1 と同じように起動する（入って出た後なら起動しない）。
- **E3 再入と退出:** 退出で、その trigger の handle に `StopAllowingParticlesToFinish`。退出の後の再入、または handle が残ったままの再入では、前の handle を即 `Stop` してから新しく起動する（trigger ごとに生存 instance は高々 1 つ）。
- **E4 Cell unload で回収:** 親 Cell `Spring_Cell_4_2` の unload で、E1 の instance が I1・I2 のとおり回収される。
- **E5 go の破棄で回収:** Play 中に trigger の go を `Object.Destroy` すると、その go に `Bind` した instance の backend `Destroy` が、go を破棄したフレームの中で要求される（次のフレームの `Tick` を待たない。経路は問わない。AC10）。
- **E6 再ロード後の再受理:** 同じ identity の Scene を unload → 再ロードした後、再び起動できる（Framework: 実 `TestableSceneDirector` の 1 往復、SampleGame: 実 Scene で Cell を載せ直して trigger から再び `Create`）。
- **E7 content build:** `Tools/OSM/Content/Build Spring Full` と `Build Spring Whitebox` の両方の Content Directory に、`Spring_Events_4_2` が Scene payload として入る（Player の動作の証明ではない）。

### 2.3 受け入れ条件（検証する条件を併記）

| # | 内容 | 条件 | 失敗経路 |
|---|---|---|---|
| AC1 | 単体（偽 `IVfxSystem`）: session が準備済みで進入 0→1 なら `Spawn` 1・`Play` 1。owner は `Bind(trigger)`、request は trigger の位置・回転、`TimeLayerId` は gameplay | E1 | |
| AC2 | 単体: 準備前に進入 → `Spawn` 0 → 準備完了で `Spawn` 1・`Play` 1。準備前に進入して退出 → 準備完了で `Spawn` 0 | E2 | ○ |
| AC3 | 単体: 退出（在室 1→0）で `StopAllowingParticlesToFinish` 1。再入で、持っている handle が `IsActive` なら `Stop` 1 → 新しい `Spawn` 1、`IsActive` でなければ（消え切って回収済み）`Stop` 0 → `Spawn` 1。どちらも新しい `Spawn` の前に古い handle が `IsActive` でない。同じ Rigidbody の id の重複した進入で `Spawn` は増えず、在室に無い id の退出で `StopAllowingParticlesToFinish` 0・例外 0 | E3 | ○ |
| AC4 | 単体: `End` で、持っている handle が `IsActive` なら `Stop` 1。`End` の後の進入、`End` の後に届いた準備完了のどちらでも `Spawn` 0。準備の失敗の後の進入で `Spawn` 0・例外 0。`End` を 2 回呼んでも `Stop` は 1 | I1, I3 | ○ |
| AC5 | Framework（OneStarMaker.Tests、`TestableSceneDirector` と、ロードの完了を test が決められる偽 asset backend。既存の `FakeAssetBackend` は `LoadAssetAsync` が即時に完了するので、保留できる test double をテスト側に足す）: `OnLoadedImpl` で Scene 所有の asset のロードを待っている間に、その Scene の unload（直接、と親の再帰 unload の両方）を要求する → ロードを完了させる → `Removed` の時点で `GetOwners(key)` が 0 件、unload は Stable の後に 1 回だけ走る | I3（source が残らない）、§1.3 の読みの確認 | ○ |
| AC6 | Framework（`VfxSystemSceneDirectorTests` に 1 件）: `Scene(id)` 所有の `Spawn` → unload → 同じ identity を再ロード → `Spawn` が受理され `Create` 1 → unload で `Destroy` 1。古い handle は生き返らない | E6 | ○ |
| AC7 | Play（実 Scene、test の `SceneDirector`、偽 backend `FakeVfxBackend`）: Cell 4_2 と Events 4_2 だけを載せる（`Spring_VFX_4_2`・Environment は載せない）。kinematic `Rigidbody` + Collider の test 用 go を trigger の内へ動かし FixedUpdate を通す → `Create` 1・`Play` 1、位置が trigger と一致 | E1, I4 | |
| AC8 | Play（AC7 の続き）: Cell を unload。記録する列が「Events の `PreUnloading` → `GetOwners(key)` が `Scene(Events)` を含む状態での `Destroy`（instance ごとに 1。Events の Unity Scene unload より前）→ Events の `Removed`（このとき `GetOwners(key)` 0 件、`Destroy` は増えていない）」。source asset のロード / `Create` / `Destroy` / `Removed` の前後をテストのログに出す（d1 の「前後関係が見える実例」）。回収が `OnDestroy` の通知や `Tick` に頼らないこと（session の `End()` の `Stop`）を、記録した `Destroy` が Unity Scene unload より前であることで確かめる。「Unity Scene unload より前」の観測: `Destroy` の記録時に trigger の go が `!= null` で、Events の Unity Scene の `isLoaded` が true | E4, I1, I2 | ○ |
| AC9 | Play（実 backend `VfxGraphBackend`、`-WithGraphics`）: AC7・AC8 と同じ操作で、起動中は trigger の位置に有効な `VisualEffect` が 1 つあり、Events の `Removed` の**次のフレーム**に trigger の VFX の `VisualEffect` が 0 件（Cell と Events だけを載せ、どちらも `VisualEffect` を持たないので、数えるのは読み込まれている全 `VisualEffect`。`DontDestroyOnLoad` の root を含む。`Object.Destroy` の native の破棄はフレームの終わりまで遅れるので、`Removed` と同じフレームでは数えない。§16.6） | E1, I1 | ○ |
| AC10 | Play（偽 backend）: trigger の go を `Object.Destroy` したフレーム N に、backend `Destroy` が 1 回呼ばれ（経路は判定しない。同じ go の `LocalVfxEventTrigger` と `VfxOwnerDestroyNotifier` の `OnDestroy` の順を Unity は定めないので、どちらが先でも合格。notifier の経路の証拠にしない）、記録したフレーム番号が N（`Tick` の回収なら N+1 になる）。その後の Cell unload で同じ instance の `Destroy` は増えない | E5, I1 | ○ |
| AC11 | Play（偽 backend）: Cell を unload → 同じ Map で Cell と Events を再び載せる → 進入で `Create` が再び 1（合計 2）、古い instance の `Destroy` は 1 のまま | E6 | |
| AC12 | Play: 非対応の `VfxSystem`（`UnsupportedFacts`）で AC7・AC8 の操作 → 例外 0、`QueryCapability` 以外の backend 呼び出し 0、診断ログは起動時の 1 件だけ、Events の `Removed` で `GetOwners(key)` 0 件 | I5 | ○ |
| AC13 | 単体（偽 `IVfxSystem`・偽の loader・記録用 logger）: session の準備（`PrepareAsync(loader)`）で loader が例外を投げる → `PrepareAsync` は例外を外へ出さずに完了し（`OperationCanceledException` だけは外へ出す）、Error ログは 1 件、以後の進入で `Spawn` 0。loader が `null` を返した場合も同じ（§2.5 D3・D4 の U2 案） | I3, U2 | ○ |
| AC14 | authoring（EditMode）: `Spring_Events_4_2` に `LocalVfxEventTrigger` が 1 つ以上あり、それぞれ同じ go に `isTrigger` の Collider を持ち、address が空でなく `Seasons/` 配下の `.vfx` の Addressables entry を指す。Events Scene に `VisualEffect`・`PlacedVfxActivator`・`Renderer` が無い。`.vfx` は Update Mode 3（既存の `PlacedVfxAuthoringTests` の規約） | §1.2 の所有物の契約、I6 | ○ |
| AC15 | authoring（EditMode）: `Spring_Cell_4_2` の `SceneResource` の `_volume` が base（`9d0c8f2f`）と同じ。`Spring_Events_4_2` の `SceneResource` は `StreamByDistance = false`・`LoadType.OnDemand`・親が `Spring_Cell_4_2`。`Spring_Cell_4_2` の `Children` に `Spring_Events_4_2` がちょうど 1 回あり、`CellCompanionRoleClassifier` がその identity を Events に分類する（`Full`・`Planner` のロード対象に入る。A3 GPT 補足 A） | I6、§1.2 | ○ |
| AC16 | 判定 C: `Build Spring Full` と `Build Spring Whitebox` が成功し、両方の Content Directory に `Spring_Events_4_2` が Scene payload として入る | E7 | |
| AC17 | 既存の回帰: `VfxScenePlayTests`（d3・d4a の配置済み VFX）と `SessionCellCompanionLoadDriverTests` が変わらず通る。`contract-audit` 通過、asmdef・`SceneState`・Framework の公開 API の diff 0、`SampleGame/InGame` に `UnityEngine.VFX` の using 0 | I6, I7 | ○ |

**読み方の固定（合格例・不合格例）。**

- AC3 の「在室」: 進入者は `attachedRigidbody` を持つ Collider で、Rigidbody ごとに数える。合格: 同じ Rigidbody の id の進入が 2 回届いても `Spawn` は 1。不合格: 2 回起動する。`attachedRigidbody` を持たない Collider（静的な地形など）は数えない。**前提: 進入者の Rigidbody は trigger に重なる Collider を 1 つだけ持つ**（いまの Player は `CapsuleCollider` 1 つ）。1 つの Rigidbody の複数 Collider の入退出（片方の退出で在室が消える）は d4b で保証しない（A3 GPT 指摘 1。下の「答えない問い」）。
- AC10 の「そのフレーム」: `Object.Destroy` は破棄をフレームの終わりまで遅らせる。合格: `Destroy` の記録が `Object.Destroy` を呼んだフレームの番号。不合格: 次のフレームの番号（`Tick` の偽 null 検査で回収した）。
- I3 の「終わり」: Events の `OnPreUnLoadedImpl` で全 session を `End()` し、trigger の `OnDestroy` でもその session を `End()` する（早いほう）。合格: `OnPreUnLoadedImpl` と Unity Scene unload の間に physics の進入が届いても `Spawn` 0。合格: `PreUnloading` の通知と `OnPreUnLoadedImpl` の間の進入で `Spawn` 1 → `End()` で `Stop` 1 → `Removed` までに `Destroy` 1。不合格: `End()` の後の進入で `Spawn` が呼ばれる。
- AC3 の「再入」: 単体テストでは退出と再入を同じ呼び出し列で続け、偽 handle の `IsActive` を test が決める（消え切りの回収は偽 handle の `IsActive = false` で表す。`Tick` は無い）。
- I1 の回収経路: Cell の unload では session の `End()` の `Stop` が回収する（`OnPreUnLoadedImpl` の時点。Unity Scene unload・`ReleaseScene` より前）。`Bind` の破棄通知（`OnDestroy`）と `Tick` の偽 null 検査は、trigger の go を個別に破棄した場合（E5）と、`End()` が呼ばれなかった場合の保険で、I1・I2 の根拠にしない。理由: `VfxOwnerDestroyNotifier` の契約では、一度も active にならなかった go 等で `OnDestroy` が呼ばれず、そのときの回収は `ReleaseScene` より後の `Tick` になる（A2 Grok F1）。

- **ここでは答えない問いと所有する後続スライス:** 大型 Event と Planner 実証の全体（S-4d5）。trigger の VFX asset を Content Directory・Player build に乗せる経路と、Content Directory 上で trigger の VFX が起動すること（BuildSystem の実装系、所有 HANDOFF 未作成）。`AssetManagement.LoadAssetAsync` の await の後の所有者確認の欠落（Framework、所有スライス未定）。Player での描画と Spring に入る Player の観測経路（S-9）。退出の通知が来ない破棄・非 active 化（進入中の Rigidbody が trigger の中で消えた場合。在室が残り、Cell unload まで VFX が残る）と、1 つの Rigidbody に Collider が複数ある進入者の在室（Collider ごとの入退出を Rigidbody へ集約する表現。どちらも進入者の追跡の厳密化で、所有スライス未定）。ParticleSystem fallback（後続の独立スライス）。
- **判定定義:** GO = §2.1・§2.2 の全条件を AC で確認できた。NO-GO = いずれかの不変条件の違反、または成立条件の不成立。CONDITIONAL ACCEPT は使わない（スパイクではない）。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- **A3 後の例外承認:** なし
- **決定記録:** なし

### 2.4 Architecture §16.7 の S-4d4b 項目の扱い

| 項目 | 扱い |
|---|---|
| notifier の `OnDestroy` が実 Scene unload と go の Destroy で回収を呼ぶことの Play Mode 確認 | 不採用（§16.7 に未確認として残す）。実 Scene の unload では `OnPreUnLoadedImpl` の `End()` の `Stop` が先に回収するので notifier の経路を通らない。trigger の go を個別に `Object.Destroy` した場合は、同じ go の trigger と notifier の `OnDestroy` の順を Unity が定めないので、AC10 は同じフレームの回収だけを確かめ、notifier 単独の経路の証明にはしない（A2 Grok F1 による設計の変更、A3 GPT 指摘 2・再レビュー P3）。d4b の問いの判定に notifier 単独の Play テストは要らない |
| 同じ identity の再ロードを実 `SceneDirector` で 1 往復 | 採用（AC6・AC11） |
| d2 のテストが直接見ていない経路のうち、`Bind` 所有者の破棄から 1 フレーム以内の回収 | 不採用（§16.7 に残す。上と同じ理由で、AC10 の同じフレームの回収は `Bind` の仕組みではなく trigger の `End()` による） |
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
- `OnTriggerEnter` / `OnTriggerExit` で `other.attachedRigidbody` を取り、null でなければその Rigidbody の id（`EntityId`。`GetInstanceID` は obsolete-as-error なので使わない）で session に進入・退出を伝える。判断はしない。
- session が付く前の callback は、進入・退出の**出来事の列**として順に溜め、付いた時点でその順に session へ流す（在室の集合は session だけが持つ。A2 アーキ F4）。Collider は最初から有効で、Unity Scene のロード直後から進入を記録する。
- `OnDestroy` で session を `End()` する（I3）。

**D3. trigger 1 つ分の論理（`LocalVfxEventSession`、SampleGame.InGame の internal、純 C#）。**

- 入力: `IVfxSystem`、所有者 `AssetOwner`（`Bind(trigger の go)`）、位置・回転。状態は `Preparing` → `Ready`（source を受け取る）/ `Failed` → `Ended` の一方向。在室は Rigidbody の id の集合。生存 handle は高々 1 つ。
- 進入（在室 0→1）: `Ready` なら、残っている handle に `Stop` → `Spawn(new VfxSpawnRequest(source, position, rotation), owner)` → `Play`。`Preparing` なら在室だけ記録する。
- 退出（在室 1→0）: handle があれば `StopAllowingParticlesToFinish`（handle は持ったまま。次の進入で `Stop` する）。
- 再入で前の handle を `Stop` するのは、その handle が `IsActive` のときだけ（消え切って回収済みなら参照を捨てるだけ）。
- `PrepareAsync(loader)`（A2 アーキ F2）: `loader` は source をロードする delegate（`Func<UniTask<UnityEngine.Object?>>`。binder が `LoadSceneScopedAssetAsync` を包んで渡す。返り値は `IAssetHandle<UnityEngine.Object>` の `.Value`）。`Preparing` のときだけ実行し、成功（非 null）なら `Ready` にして在室が 1 以上なら進入と同じ起動をする。例外（`OperationCanceledException` を除く）か null なら `Failed` にして Error ログを 1 件出し、例外を外へ出さない（U2）。完了の時点で `Ended` なら何もしない。ログは ctor で受け取る `ILogger` に出す。
- `End()`: `Ended` にし、持っている handle が `IsActive` なら `Stop` してから参照を手放す（A2 Grok F1。Cell の unload で、Unity Scene unload と `ReleaseScene` より前に instance を回収する。`Bind` の破棄通知に頼らない）。2 回目以降は何もしない。以後の進入・準備完了は何もしない（I3）。
- source asset の handle は session が持たない（所有者は Events Scene の `Scene(id)`。§1.3 の読みのとおり、`OnLoadedImpl` でロードすれば `ReleaseScene` はロードの完了より前に起きない）。そのため「完了したロードの handle を session がすぐ Release する」補助と、ロード完了の直後に Scene がまだロードされているかを確かめ直す補助は入れない（AC5 で前提を確かめる。前提が崩れたら §4.2 で Phase A へ返す）。

**D4. `CellCompanionScene` が trigger を束ねる。** 新しい Scene class と factory の role 分岐は作らない（§1.2 の d4b 行。internal の role classifier を DependOnAll から呼べない）。

- **binder は instance で、`CellCompanionScene` が `LocalVfxEventBinder?` を 1 つ持つ**（A2 アーキ F1。`SceneBase` は `OnPreUnLoadedImpl` の直前に `RootObjects` を空にするので、root から集め直せない）。session の一覧の寿命は Scene。trigger が無ければ binder を作らない（I6。Environment 216 件は root の走査だけ）。
- `OnInitialize`: 既存の `PlacedVfxAdopter.AdoptAll` に加え、root から `LocalVfxEventTrigger`（`includeInactive: true`）を集め、1 つ以上あれば binder を作り、各 trigger に session を作って付ける。
- `OnLoadedImpl`: binder が各 session の `PrepareAsync` を、`LoadSceneScopedAssetAsync<UnityEngine.Object>(AssetKey.FromAddress(address))` を包んだ loader で順に await する。loader は返った `IAssetHandle<UnityEngine.Object>` の `.Value` を返し、handle 自体は `VfxSpawnRequest` に渡さず個別に `Release` もしない（解放は `ReleaseScene`）。`.Value` が null なら U2・AC13 の失敗。InGame は VFX の型を知らないので `UnityEngine.Object` で受け、型の検査は backend の `Create` に任せる（`VisualEffectAsset` でなければ何も作らない。§16.6）。
- **U2（案）: ロードの失敗は Events Scene のロード失敗にしない。** session の `PrepareAsync` が失敗を捕まえて Error ログを trigger ごとに 1 件出し、その trigger だけを止める。Scene のロードは続ける。理由: trigger の VFX は表示の演出で、Events Scene の他の進行制御まで止めない。Content Directory を使う Player では、この `.vfx` が directory に入らないので（U1）、失敗を Scene のロード失敗にすると Events 全体が載らなくなる。失敗はログで見える。
- `OnPreUnLoadedImpl`: binder が全 session を `End()`（I1・I3。生存 instance を `Stop` で回収し、以後の進入で `Spawn` しない）。
- 寿命: asset は `Scene(Events の identity)`、VFX は `Bind(trigger の go)`。Cell の unload では `OnPreUnLoadedImpl` の `End()` が回収し、その後に Unity Scene unload、`ReleaseScene` が続くので「VFX の回収 → asset の解放」が保たれる（I2）。`Bind` の所有は trigger の go の個別の破棄（E5）と、`End()` が呼ばれなかった場合の保険。
- **U3 の比較:** A0 だけからの代替構成は、進入者を Player の Rigidbody に絞る案だった。識別の規約が要るので、主担当の案（Rigidbody を持つ Collider 全て）を維持して A3 の裁定に出す。
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
- **U2（裁定済み、発注者 2026-10-28）:** trigger の asset のロード失敗を Events Scene のロード失敗にしない（D4 の案どおり）。
- **U3（裁定済み、発注者 2026-10-28）:** 進入者を `attachedRigidbody` を持つ Collider とする（D4 の案どおり。Player に絞る案は採らない）。

---

## 3. 責務マップ（A1）

| ファイル | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `SampleGame/InGame/InGameSession/World/LocalVfxEventSession.cs`（新規、internal） | trigger 1 つ分の状態（準備・在室・handle）、起動・停止の判断、準備（loader の実行と失敗の記録）、終了時の `Stop` | trigger の振る舞いの規則 | session は binder（= Scene）が保持し、trigger の go の `OnDestroy` か Scene の `OnPreUnLoadedImpl` で終わる。instance は `Bind(trigger)`、source は `Scene(Events)` | `IVfxSystem`、`VfxSpawnRequest`、`AssetOwner`、`ILogger`、UniTask | internal | 偽 `IVfxSystem`・偽の loader・記録用 logger で単体（AC1〜AC4、AC13） | 0 → 160 |
| `SampleGame/InGame/InGameSession/World/LocalVfxEventTrigger.cs`（新規、MonoBehaviour） | authoring の面（address）、physics の callback から session への転送、付く前の出来事の列の保持、`OnDestroy` で `End()` | authoring と Unity の受け口 | Scene 内の go | session | public（MonoBehaviour）、serialized field 1 | authoring EditMode（AC14）、Play | 0 → 80 |
| `SampleGame/InGame/InGameSession/World/LocalVfxEventBinder.cs`（新規、internal、instance） | root から trigger を集め session を作って付ける、loader を作って各 session の準備を await する、全 session の `End()` | Events Scene の lifecycle との接続 | `CellCompanionScene` が保持（Scene の寿命）。session の一覧を持つ | `IVfxSystem`、Scene スコープのロード（delegate で受ける）、`ILoggerFactory` | internal | Play（AC7〜AC12） | 0 → 80 |
| `SampleGame/InGame/InGameSession/World/CellCompanionScene.cs` | `LocalVfxEventBinder?` のフィールド 1 つと、`OnInitialize` / `OnLoadedImpl` / `OnPreUnLoadedImpl` から binder を呼ぶ | 呼び出しの追加 | Scene | 既存 | 変更なし | Play | 51 → 85 |
| `OneStarMaker/Tests/Rendering/Vfx/VfxSystemSceneDirectorTests.cs` | 再ロード後の再受理 1 件（AC6） | テスト | — | — | — | — | 293 → 340 |
| `OneStarMaker/Tests/Scene/`（既存の Scene のテストのうち適切なファイル、または新規 1 本） | `OnLoadedImpl` の Scene スコープのロード中の unload（AC5） | テスト | — | ロードを保留できる偽 asset backend（新規の test double。既存の `FakeAssetBackend` は即時完了） | — | — | +150 |
| `SampleGame/Tests/World/LocalVfxEventSessionTests.cs`（新規）と偽 `IVfxSystem` | AC1〜AC4、AC13 | テスト | — | — | — | — | 0 → 250 |
| `SampleGame/Tests/Rendering/Vfx/LocalVfxEventPlayTests.cs`（新規） | AC7〜AC12 | テスト | — | `VfxPlayTestFixture`、`FakeVfxBackend` | — | — | 0 → 400 |
| `SampleGame/Tests/Rendering/Vfx/PlacedVfxAuthoringTests.cs` または新規の authoring テスト | AC14・AC15 | テスト | — | — | — | — | 283 → 350 |
| `Seasons/Spring/Cells/Spring_Cell_4_2/Spring_Events_4_2/`（新規 Scene、`SceneResource`、`.vfx`）、Map と Addressables の設定 | 実証 Scene | — | — | — | — | — | Editor 操作 |

- Framework のコードは変えない（テストだけを足す）。公開 API の追加 0、asmdef 参照の追加 0（`SampleGame.InGame` は `OneStarMaker.Runtime` を参照済み）。program §10 は変えない。
- 行数警報: `VfxSystemSceneDirectorTests` 以外の既存ファイルの増加は 50% 未満。`CellCompanionScene.cs` は 51 → 85（+67%）で警報に当たる。非分割の理由: 増えるのは binder のフィールド 1 つと、lifecycle の hook 3 つから binder を呼ぶ数行で（`PlacedVfxAdopter` の呼び出しと同じ形）、判断は session と binder に分けた。`LocalVfxEventPlayTests.cs` は 400 行前後の見込みで、500 を超えたら AC の群（lifecycle / 非対応 / 失敗）で分ける。
- 責務の数: session（規則）、trigger（Unity の受け口）、binder（Scene の lifecycle との接続）の 3 つを別ファイルにする。変える理由が違う（trigger の振る舞いの仕様、authoring の形、Scene の lifecycle の仕様）。

---

## 4. 実装計画（A1）

### 4.1 順序

1. `LocalVfxEventSession` と単体テスト（AC1〜AC4、AC13）。
2. Framework のテスト 2 件（AC5・AC6）。AC5 が §1.3 の読みと違う結果になったら、直さずに止めて Phase A へ返す（§4.2）。
3. `LocalVfxEventTrigger`、`LocalVfxEventBinder`、`CellCompanionScene` の呼び出し。
4. Editor 操作: World Workspace の Planner role で `Spring_Events_4_2` を作る → trigger と `.vfx` を置く → `.vfx` の Addressables entry を足す。一時スクリプトは commit の前に消す。
5. Play テスト（AC7〜AC12）と authoring テスト（AC14・AC15）。コンパイル確認と `pwsh tools/contract-audit.ps1` まで（Unity テストと Build は Phase C）。

### 4.2 Phase B から Phase A へ差し戻す条件

- §3 に無い公開 API・状態・依存・所有者・寿命が要る。Framework のコードの変更が要る。
- asmdef 参照の追加が要る。
- AC5 で、`OnLoadedImpl` のロード中の unload がロードの完了より前に `ReleaseScene` を呼ぶ（§1.3 の読みが誤り）。その場合は session が完了したロードの handle を Release する案（§1.2 の外部レビューの代替）を A2 で比べる。
- World Workspace の transaction で Events Scene を作れない、または作った Scene が Cell の `_volume` を変える。
- `CellCompanionScene` に trigger の処理を置くと、trigger を持たない companion の振る舞いが変わる。
- AC10 で、batchmode の Play 中に trigger の go を `Object.Destroy` しても `LocalVfxEventTrigger.OnDestroy` が同じフレームに届かない（§5 の未知の操作経路（2））。

### 4.3 BuildSystem の既知の課題との関係（判断基準による記録）

- **content build:** Events Scene は World Workspace の transaction で Addressables entry と Map に入り、`Build Spring Full / Whitebox` の Content Directory に Scene payload として入る（AC16、判定必須）。trigger の `.vfx` は Content Directory に入らない（U1）。**BuildSystem の作業候補:** Content Directory の非 Scene asset の production 経路。d4b の trigger の `.vfx` が、Scene 以外の asset を production で引く最初の具体的な使い手になる（address は asset path、owner は `Scene(Events)`）。
- **Player build:** d4b では行わない。理由: BS4 の Player は smoke の後に終了して Spring に入らないので、Player 上で trigger を踏む観測経路が無い（S-9 へ送り済み）。この worktree のルートは 82 文字で path 予算には収まるが、観測経路が無いことが理由である。**BuildSystem の作業候補:** Spring に入る Player の観測経路（変わらず）。
- **Content Directory を使う実行での振る舞い（読み）:** `.vfx` が directory に無いので trigger の asset のロードは失敗し、U2 の案なら Error ログを出して trigger だけが止まる。Events Scene は載る。

---

## 5. テストとレビュー計画（A1）

- **単体テスト（EditMode、Unity の Scene なし）:** `LocalVfxEventSessionTests`（AC1〜AC4、AC13）。Framework の `VfxSystemSceneDirectorTests` に AC6、Scene のテストに AC5。
- **Play テスト:** `LocalVfxEventPlayTests`（AC7・AC8・AC10〜AC12 は `FakeVfxBackend` で graphics なしでも走る。AC9 は実 `VfxGraphBackend` で `-WithGraphics`）。進入は kinematic `Rigidbody` + Collider の test 用の go を `Rigidbody.MovePosition` で trigger の内へ動かし、FixedUpdate を通して `OnTriggerEnter` 経路で起こす（待ちはフレームの yield。`Task.Delay` / `Thread.Sleep` は使わない）。
- **authoring（EditMode）:** AC14・AC15。
- **差し戻し中の起点 `-Filter`:** `SampleGame.Tests.World.LocalVfxEventSessionTests`、`SampleGame.Tests.Rendering.Vfx`、`OneStarMaker.Tests.Rendering.Vfx.VfxSystemSceneDirectorTests`、AC5 のテスト。
- **判定必須テスト（GO 候補 head）:** 全 EditMode 回帰（空 filter）、上記 Play テスト（`-WithGraphics` で 1 プロセスにまとめる）、Content Directory の build 2 種（`Build Spring Full`・`Build Spring Whitebox`、AC16）。
- **全 EditMode 回帰の適用除外:** なし
- **操作・実行時・目視条件の検証経路:** E1〜E6 は Play テストで閉じる（構造・値の検査。backend の呼び出し記録、`IAssetDiagnostics.GetOwners`、`VisualEffect` の件数、フレーム番号）。見た目の判断は求めない。E7 は Content Directory の build の出力で確かめる（判定 C の実行担当）。動作確認の対象: なし（offline のテストで閉じない操作・目視の条件が無い）。
- **未知の操作経路:** kinematic Rigidbody と静的な trigger の Collider で、test が `MovePosition` で動かしたときに `OnTriggerEnter` / `OnTriggerExit` が届くこと（Unity の標準の挙動。未確認。初回確認は Phase C、理由: Unity テストが要る。担当: 発見 C の Unity 実行担当。確認地点: AC7 の最初の実行。不成立なら、test 用の go を非 kinematic にして位置を書く等の手段の変更は B 適応、trigger の検出方式を変える必要があれば Phase A へ返す）。
- **未知の操作経路（2）:** Play テスト（`run-tests.ps1` は batchmode で起動する）で、active な trigger の go を `Object.Destroy` したとき `LocalVfxEventTrigger.OnDestroy` が同じフレームに届くこと（AC10 の回収はこの経路。notifier の経路は d4b で確かめない。§2.4）。notifier の XML doc は「一度も active にならなかった go や batchmode / EditMode では `OnDestroy` が呼ばれない」と書くが、Play 中の active な go の batchmode での MonoBehaviour の `OnDestroy` の振る舞いは未確認。未確認。初回確認は Phase C（理由: Unity テストが要る。担当: 発見 C の Unity 実行担当。確認地点: AC10 の最初の実行）。不成立なら、AC10（E5 の「同じフレーム」）を「次の `Tick`」に変える必要があり、成立条件の意味が変わるので Phase A を新しい revision で再開する。I1・I2 は E5 の経路に頼らないので影響しない。
- **人間の判断が必要な条件:** なし
- **高コスト証拠:** なし（Player build を行わない）
- **判定 C の照合 script の方式:** なし
- **機械検査:** `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- **A0/A1 主担当:** この session の主担当（モデル・ベンダーは A3 で実績として記録する）。
### 5.1 レビューの記録

- **A0/A1 主担当:** この session の主担当（Anthropic、Claude Opus 系）。
- **A2 入力版:** commit `04d833b0`、内容 sha256 `bee4616a83e655d94b7907e6d8e237f52c3748576f9843723d739fd7ab092ccf`。3 件とも同じ入力版を使い、互いの指摘を見せていない。
- **A2 独立レビュー（2026-10-28）:**
  - (1) アーキテクチャゲート — 新規 subagent（Anthropic、Claude Sonnet 系）。指摘 5 件（medium 2、low 3）。結論は「条件付き可、大きな設計変更は不要」。
  - (2) 不変条件の反例探し・AC の曖昧さ — cursor-agent（xAI、Grok 系）。指摘 4 件（high 1、medium 3）。破れなかった列: `OnLoadedImpl` の await 中の直接 unload と親の再帰 unload（§1.3 の読みは現コードで成り立つ）、`PreUnloading` の通知から `End` まで（Events は UI が無く `RemoveUIView` が即 return する）、notifier と次の `Tick` の両方の回収（`Destroy` は増えない）。
  - (3) A0 だけから代替構成 — 新規 subagent（Anthropic、Claude Fable 系）。§0〜§1 だけを渡し、設計案は読ませていない。構成は主担当の案とほぼ一致（Framework を変えない、`CellCompanionScene` に収集とロード、純 C# の session、asset は `OnLoadedImpl` で `Scene(Events)`、VFX は `Bind(go)`、親なし）。違いは下表。
- **A2 の採否（発注者が 2026-10-28 に主担当の案どおり承認。「範囲を膨らませない」方針）:**

| 指摘 | 内容 | 採否 | 理由・反映先 |
|---|---|---|---|
| Grok F1（high） | `End()` が handle を手放すだけだと、Cell の unload の回収が `Bind` の破棄通知に頼る。通知が届かない場合は `ReleaseScene` の後の `Tick` で回収され、I1・I2 が破れる | 採用 | `End()` で `IsActive` な handle を `Stop`（D3）。I1 の回収経路の読み（§2.3）、AC4・AC8 |
| Grok F2 | `Removed` と同じフレームでは `VisualEffect` の native の破棄が終わっていない | 採用 | AC9 を「`Removed` の次のフレームで 0 件」に |
| Grok F3 | AC3 の再入で、間に回収が入ると `Stop` の回数が変わる | 採用 | AC3 を `IsActive` で場合分けし、読み方を固定 |
| Grok F4 | AC13 が「単体または Play」で合否が分かれる | 採用 | AC13 を単体に固定（アーキ F2 と合わせて準備の論理を session へ） |
| アーキ F1 | static の binder では `OnPreUnLoadedImpl` で session に届かない（`RootObjects` は直前に空になる） | 採用 | binder を instance にし、`CellCompanionScene` が保持（D4、§3） |
| アーキ F2 | ロード・失敗・ログの判断が Play でしか確かめられない | 採用 | `PrepareAsync(loader)` を session に置き単体テスト（D3、AC13） |
| アーキ F3 | `PreUnloading` の通知と `End` の間に進入が届きうる | 採用 | I3 の文言と読み方（`End` で回収されるので I1・I2 は保たれる） |
| アーキ F4 | 在室の集合が trigger と session の 2 か所に分かれる | 採用 | trigger は出来事の列を溜めて流すだけ（D2） |
| アーキ F5 | `CellCompanionScene` の非分割理由の記述 | 採用 | §3 |
| 代替: `OnPreUnLoadedImpl` で全 handle を Dispose | | 採用（Grok F1 と同じ） | D3 の `End()` |
| 代替: ロード完了の直後に `IsSceneLoaded` を確かめ直し、false なら handle を即 Dispose する保険 | | 不採用 | §1.3 の読みでは起きない経路で、A2 (2) も現コードで成り立つと確かめた。通らない分岐を足さず、AC5 で前提を確かめ、崩れたら Phase A へ返す（§4.2） |
| 代替: 進入者を Player の Rigidbody に絞る | | 不採用（U3） | 識別の規約を新しく足す必要があり、範囲を膨らませない |

- **主担当が自分で洗い直した点（反映済み）:** `End()` の `Stop` を足したことで、d1 の「notifier の `OnDestroy` が実 Scene の unload で回収を呼ぶ」確認は通らない経路になる（§2.4 の表に記録）。go の個別の破棄の経路（AC10）は残すが、batchmode の Play での `OnDestroy` の届き方は未確認なので、未知の操作経路（2）と §4.2 に足した。A3 レビューで、AC10 も notifier の証拠にならないと指摘され、§2.4 の該当 2 行を不採用（§16.7 に残す）に直した。
- **A3 統合版のレビュー（2026-10-28）:** 統合版 `329d309c` を発注者が外部の GPT（OpenAI 系）に渡した静的照合レビュー。Unity の実行は無し。総合は「骨格は妥当、凍結前に P1 2 件・P2 2 件」。発注者の方針: Collider 群の厳密化で範囲が膨らむなら後続へ回してよい。
- **A3 レビューの採否:**

| 指摘 | 内容 | 採否 | 理由・反映先 |
|---|---|---|---|
| GPT 1（P1） | Rigidbody の id の集合では、同じ Rigidbody の Collider A・B が入って A だけ出ると在室が消え、B が中にいるのに止まる。AC3 の「2 つ目の Collider で在室数が変わらない」と食い違う | 制限として記録（後続へ） | AC3 の 2 つ目の Collider の宣言を削り、重複進入・在室に無い退出の扱いに直した。読み方に「進入者の Rigidbody は Collider 1 つ」の前提を明記（いまの Player は `CapsuleCollider` 1 つ）。Collider ごとの集約は「退出が来ない破棄」と同じ進入者の追跡の厳密化として「答えない問い」へ |
| GPT 2（P1） | AC10 は `LocalVfxEventTrigger.OnDestroy` → `End` → `Stop` だけで通り、notifier の `OnDestroy` の証拠にならない | 採用（未確認へ戻す） | AC10 に経路を明記。§2.4 の notifier の Play 確認と `Bind` 所有者の 1 フレーム回収を不採用（§16.7 に残す）に。未知の操作経路（2）・§4.2 を trigger の `OnDestroy` に直した。notifier 単独の Play テストは足さない |
| GPT 3（P2） | `LoadSceneScopedAssetAsync` は `IAssetHandle<T>` を返す | 採用 | D3・D4 に `.Value` の取り出し、handle を渡さない・個別 Release しない、null は U2 の失敗、を明記 |
| GPT 4（P2） | I1 の「`Removed` の後に残らない」が AC9 の「次のフレーム」と揃っていない | 採用 | I1 を `Removed` までの `Destroy` 1 回（AC8）と次のフレームの 0 件（AC9）の 2 段に |
| GPT 補足 A | 新しい Events が `Full`・`Planner` の対象に列挙される証拠 | 採用（小） | AC15 に Cell の `Children` への 1 回の登録と classifier の Events 分類を追加。driver の end-to-end は足さない |
| GPT 補足 B | BuildSystem の境界の維持 | 変更なし | §4.3 のまま |

- **A3 統合:** 統合版。U2・U3・A2 の採否は発注者が承認した（2026-10-28）。A3 レビューの採否も発注者が主担当の案どおり承認した（2026-10-28）。
- **凍結前の主担当の見直し（Phase C・C' で指摘されそうな点、反映済み）:**
  - AC10 が「経路は trigger の `OnDestroy` → `End`」と断定していたが、同じ go の notifier と trigger の `OnDestroy` の順は Unity が定めない。経路を判定から外し、フレームと回数だけで判定する形にした。
  - AC8 の「Unity Scene unload より前」に観測の方法が無かった。trigger の go の生存と Events の Unity Scene の `isLoaded` で観測する形にした。
  - AC9 の「trigger の VFX の `VisualEffect`」の見分け方が無かった。Cell と Events は `VisualEffect` を持たない（Cell 4_2 の `.unity` を確認）ので、全件を数える形にした。
  - AC5 と §3 が「既存の偽 asset backend」で保留中のロードを作れる前提だったが、`FakeAssetBackend.LoadAssetAsync` は即時完了する。保留できる test double を足すことを明記した（Framework のコードは変えない）。
  - I7 に常時契約の `#nullable enable` と `record` 禁止を足した。
  - 確かめて変更不要だった点: base `9d0c8f2f` の後に `origin/develop` は進んでいない（2026-10-28）。Content Directory の candidate は `SceneResource` の payload から作るので、`.vfx` の Addressables entry が candidate になって build が止まることは無い。Whitebox の build にも Full だけの補助 Scene（Events）が共通として入る（`SeasonSceneSelectionPolicy`）。
- **A3 再レビュー（2026-10-28）:** `89a6242a` を同じ GPT（OpenAI 系）が静的に再レビュー。結論は「凍結 GO を推奨。前回の P1・P2 は解消、新しいブロッカーなし」。P3 の 2 件（§1 の目的の「破棄通知での回収をここで閉じる」と §2.4 の `OnDestroy` の順の断定が現行の裁定と食い違う）と、E5 の文言を AC10 のフレームだけの観測に揃える提案を採用し、凍結 commit で直した。
- **A3 凍結:** 2026-10-28、発注者の指示で revision 1 として凍結。program の §6.3 d4b 行を更新した。
- **C' 用に予約した担当:** OpenAI 系（Codex）で予約していたが、A3 の統合版レビューに OpenAI 系（GPT）が関与したため、「A〜C に関与していないベンダー」の条件を満たさない。C' の担当ベンダーは Phase C' の開始までに発注者が決める（2026-10-28、発注者「後で決める」）。 2026-10-29、発注者が C' の担当の選定を Phase C〜C' の進行役に委任した（「A〜C に関与していないベンダー」の条件を満たす担当を進行役が選び、選んだ担当と理由を §8 に記録する）。
- **独立性の強化条件:** A2 の 3 件のうち 2 件が Anthropic 系（モデルは別）、1 件が xAI。A3 の統合版レビューは OpenAI 系。C' の担当は上記のとおり未定。

## 6. Phase B 実装結果

- 実装（§4.1 の順）:
  - `LocalVfxEventSession`（D3）と単体テスト `SampleGame/Tests/World/LocalVfxEventSessionTests.cs`（AC1〜AC4・AC13）。偽 `IVfxSystem` は同じフォルダの `RecordingVfxSystem.cs`（handle 操作の順序を記録し、`IsActive` を test が決める）。
  - Framework のテスト: AC5 は新規 `OneStarMaker/Tests/Scene/SceneDirectorSceneScopedAssetLoadTests.cs`（直接の unload と親の再帰 unload の 2 件。ロードを保留できる backend はファイル内の nested class）。AC6 は `VfxSystemSceneDirectorTests.ReloadSameIdentity_AcceptsNewSpawn_AndOldHandleStaysCollected`。Framework のコードは変えていない。
  - `LocalVfxEventTrigger`（D2）、`LocalVfxEventBinder`（D4。`CellCompanionScene` が instance を 1 つ持つ）、`CellCompanionScene` の `OnInitialize` / `OnLoadedImpl` / `OnPreUnLoadedImpl`（51 → 81 行）。
  - Editor 操作（D1。Pipeline の `eval` だけを使い、一時スクリプトは repo に置いていない）:
    - World Workspace の Planner role の作成 transaction（`WorldCompanionSceneCreator.CreateAndOpen`、Spring (4,2)）で `Spring_Events_4_2` の `.unity`・`SceneResource`・SceneGraph node・`Total.asset` の Edge・Map・Addressables entry（Default Local Group、address は scene path）を作った。`Spring_Cell_4_2` の `_volume` は変わっていない。
    - trigger: `LocalVfxTrigger_Burst`、位置 (1100, 4, 625)、回転 identity、`BoxCollider`（`isTrigger`、10×8×10）と `LocalVfxEventTrigger`。Cell の中心 (1125, *, 625) には高さ 49 の `Witness` の柱があるので、Cell の地表（y≈0）の上で柱を外した位置にした。Renderer は置いていない。
    - `.vfx`: `Spring_Events_4_2/Spring_Events_4_2_Burst.vfx`。`Spring_VFX_4_2_GroundFog.vfx` を `AssetDatabase.CopyAsset` で複製し、VFX Graph の Editor API で spawner の `VFXSpawnerConstantRate`（Rate 64）を `VFXSpawnerBurst`（Count 48、Single）に置き換えた（spawner の loop は Infinite のままなので、1 回だけ発射する）。Update Mode は 3 のまま。Addressables entry を Events Scene と同じ group に API で足した（address は asset path）。
  - Play テスト `SampleGame/Tests/Rendering/Vfx/LocalVfxEventPlayTests.cs`（AC7・AC8 は 1 件にまとめた、AC9〜AC12）と `LocalVfxEventPlayTests.Scenes.cs`（test の SceneDirector、記録する backend、ログの計数）。authoring テスト `SampleGame/Tests/Rendering/Vfx/LocalVfxEventAuthoringTests.cs`（AC14・AC15）。
- HANDOFF との差（いずれも B 適応。凍結条件の意味は変えていない）:
  - `.vfx` の「短い発射」は Single Burst（Count 48）で実現した（D1 の「Single Burst 等」の範囲）。
  - AC5 のために、テスト double `TestSceneBase` に Scene 所有のロードを lifecycle の action から呼ぶ `LoadSceneScopedAssetForTest` を足した（§3 の「保留できる test double」の一部。Framework のコードではない）。
  - Play テストは 1 ファイルで 609 行になったので、§3 の 500 行の規則で 2 ファイルに分けた。分け方は AC の群ではなく、テスト本体（290 行）と Scene の載せ方・観測の補助（360 行、`partial`）にした。AC ごとの群に分けると補助の重複か共有の基底が要るため。
  - authoring テスト（AC14・AC15）は既存の `PlacedVfxAuthoringTests` に足さず新規ファイルにした（Addressables の entry の照合は `VfxSceneStructureTests` と同じ reflection）。
  - AC8 は「`Destroy` の時点で trigger の go が `!= null`・Events の Unity Scene が `isLoaded`」に加え、Events の lifecycle の通知と `Destroy` の時系列（`PreUnloading` → `Destroy` → `Removed`）をテストのログに出して順序も確かめる。AC12 の診断ログは Warning 以上を数える（非対応の診断は `VfxSystem` の `LogWarning`）。
- B の作業中に見つけた事象（後続の入力。d4b の判定には影響しない）:
  - **World Workspace の作成 transaction が、未保存の untitled Scene だけが開いている Editor で preflight を通り、失敗後の rollback も元の Scene 構成（空）を復元できずに journal が残った。** Editor を起動した直後（Scene 未選択）に `CreateAndOpen` を呼ぶと「Cannot create a new scene additively with an untitled scene unsaved」で失敗し、rollback が「One active scene is required」で失敗した。作られていたのは空の companion フォルダだけ（journal の scene・resource・node・Addressables の GUID は全て空）だったので、そのフォルダを `AssetDatabase.DeleteAsset` で消し、この worktree の `Library/` の journal を消してから、Cell の Scene を開いた状態で作り直した。修正は World Workspace の作成 transaction の preflight（untitled の未保存 Scene を拒否する）か rollback（空の構成の復元）で、所有スライス未定。
  - Editor でこの project を開くと `Assets/MobileDependencyResolver/` の `.pdb.meta` 5 件が消える（対応する `.pdb` が無い）。commit 前に戻した。d4b の変更ではない。
- 確認したこと: Editor のコンパイル（エラー 0。新しく書いたファイルに警告 0）。`pwsh tools/contract-audit.ps1`（errors 0、warnings 0）。
- 未実行: Unity テストの全て（AC1〜AC15・AC17。Phase C）、Content Directory の build（AC16。判定 C）。§1.3 の読み（AC5）と §5 の未知の操作経路（1）（2）は、Phase C の最初の実行で初めて確かめられる。
- implementation head commit: `eba9ddaf`
- Phase B 担当: この session の主担当（Anthropic 系）

### 差し戻し 1 巡目（2026-10-29）

- 入力: §7 の発見 C 1 巡目の F1〜F3。いずれもテストの修正で、製品コード・凍結条件・Framework は変えていない。
- 修正（`3300d497..4cb3b8d3`、テスト 3 ファイル）:
  - F1: `LocalVfxEventSessionTests.LoaderCanceled_PropagatesCancellation_AndLogsNothing` を `Assert.CatchAsync<OperationCanceledException>` にした（派生の `TaskCanceledException` を許す）。
  - F2: `LocalVfxEventAuthoringTests` の AC14 の Update Mode を `PlacedVfxAuthoringTests.InspectUpdateMode`（行頭一致・ちょうど 1 件・値 3）が null を返すことで確かめる。
  - F3: `LocalVfxEventPlayTests.Unsupported_EnterAndUnload_NoBackendCallsNoExtraLogs_AndNoSourceLeft` に陽性対照を足した。進入の後、Unload の前に trigger の session を reflection で読み、在室数 1 と、起動で受け取った handle が `NullVfxHandle.Instance`（非対応の `VfxSystem.Spawn` が返す no-op）であることを要求する。在室だけでなく起動（`Spawn` の呼び出し）まで届いたことを示す。製品コードに観測用の面は足していない。
- 確認したこと: Editor のコンパイル（`scriptCompilationFailed` = false、変更した 3 ファイルに警告・エラーなし。確認後に Editor を正常終了した）。`pwsh tools/contract-audit.ps1`（errors 0、warnings 0）。Editor の起動で `.pdb.meta` 5 件が消える事象が再発し、`git checkout` で戻した（commit に含めない）。
- 未実行: Unity テストの全て（F1〜F3 の修正の確認を含む。発見 C 2 巡目）。
- Phase B 担当: この session の主担当（Anthropic 系）

### 動作確認

- 対象なし（§5「動作確認の対象: なし」。E1〜E7 は Play テストと build の出力で閉じる）。

## 7. Phase C

### 発見 C 1 巡目（2026-10-29）

- **種別:** 発見 C（GO 判定ではない）。対象 implementation head `eba9ddaf`、base `9d0c8f2f`。workflow task `s-4d4b-local-event` は開始済み（Evidence の stage は未実施）。
- **実行（`pwsh tools/run-tests.ps1`、Editor は閉じた状態、限定 filter）:**
  1. EditMode、filter `SampleGame.Tests.World.LocalVfxEventSessionTests;OneStarMaker.Tests.Rendering.Vfx.VfxSystemSceneDirectorTests;OneStarMaker.Tests.Scene.SceneDirectorSceneScopedAssetLoadTests;SampleGame.Tests.Rendering.Vfx.LocalVfxEventAuthoringTests`。26 件中 25 件 pass・1 件 fail。収録は session 18・`VfxSystemSceneDirectorTests` 6・authoring 2。AC5 の class は namespace が `OneStarMaker.Tests.SceneSystem` で、この filter の `OneStarMaker.Tests.Scene.` では一致せず 0 件だった（filter の誤り。次の実行で解消）。
  2. `-WithGraphics`、filter `OneStarMaker.Tests.SceneSystem.SceneDirectorSceneScopedAssetLoadTests;SampleGame.Tests.Rendering.Vfx.LocalVfxEventPlayTests`。7 件全て pass（AC5 の 2 件、AC7・8 / AC9 / AC10 / AC11 / AC12 の Play 5 件。AC9 は実 backend で Inconclusive でなく pass）。
- **未知の操作経路の確認結果:** §1.3 の読み（`OnLoadedImpl` 中の Scene 所有のロードは unload が Stable まで保留され、source が残らない）は AC5 の 2 件で成立。§5 の（1）kinematic Rigidbody の `MovePosition` で `OnTriggerEnter` が届く（AC7 pass）、（2）batchmode の Play で active な trigger の go の `Object.Destroy` が同じフレームに `OnDestroy` に届く（AC10 pass）はともに成立。Phase A の再開理由は無い。
- **構造適合:** §3 の責務マップどおり（session / trigger / binder / `CellCompanionScene` の 3 hook）。Framework のコード・公開 API・asmdef・`SceneState` の diff 0。`record` なし、新規 `.cs` に `#nullable enable`。静的な読みで、製品コード（session・trigger・binder・`CellCompanionScene`）に凍結条件・常時契約への違反は見つからなかった。テストコード本体は AC の条件との照合まで精読した（下の F1〜F3 以外は、AC の観測が条件どおり）。
- **阻害する指摘（いずれも B 適応。テストの修正で足り、Phase A の再開は不要）:**

| # | 内容 | 違反する凍結条件 | 修正案 |
|---|---|---|---|
| F1 | `LocalVfxEventSessionTests.LoaderCanceled_PropagatesCancellation_AndLogsNothing`（:295）が失敗。`Assert.ThrowsAsync<OperationCanceledException>` は型が完全一致を要求するが、`UniTask.FromCanceled` は派生の `TaskCanceledException` を投げる。製品コードは派生を再 throw していて正しい | AC13（`OperationCanceledException` だけは外へ出す）の判定が実行で不成立 | `Assert.CatchAsync<OperationCanceledException>` にする |
| F2 | `LocalVfxEventAuthoringTests` の AC14 が `.vfx` の Update Mode を `Does.Contain("m_UpdateMode: 3")` で確かめる。条件が引く既存規約（`PlacedVfxAuthoringTests.InspectUpdateMode`: 行頭一致、ちょうど 1 件、値 3）より弱く、`m_UpdateMode: 30` や別値との併存を通す | AC14（「`.vfx` は Update Mode 3（既存の `PlacedVfxAuthoringTests` の規約）」）を実際より弱く判定 | `PlacedVfxAuthoringTests.InspectUpdateMode`（同じ asmdef の internal）を呼んで null を要求する |
| F3 | `Unsupported_EnterAndUnload_NoBackendCallsNoExtraLogs_AndNoSourceLeft`（AC12）は「`QueryCapability` 以外の backend 呼び出し 0」を見るが、進入が session まで届いたことの陽性対照が無い。進入が届かなくても通る（空振りで pass できる） | AC12（「AC7・AC8 の操作」で非対応の経路を通すこと）を実際より弱く判定 | 進入が届いたことを観測する（例: 進入の後に trigger の Collider と probe の重なりを `Physics` で確認する、または session の在室数を reflection で読む）。非対応でも `Spawn` 自体は呼ばれ no-op handle が返るので、`VfxSystem` 側の観測が取れるならそれを使ってよい |

- **後続スライスへ移送する入力:** なし（§6 に記録済みの World Workspace transaction の preflight / rollback と `.pdb.meta` の消失は、引き続き d4b の判定に影響しない後続の入力）。
- **未実行（GO 候補 head の判定 C で行う）:** 全 EditMode 回帰（空 filter）、AC16（`Build Spring Full` / `Build Spring Whitebox`）、AC17 の既存回帰（`VfxScenePlayTests`、`SessionCellCompanionLoadDriverTests`）、`contract-audit` の再実行（B の記録のみ）、Evidence の stage と C 用 bundle。F1〜F3 の修正後の再実行は、起点 filter（§5）に AC5 の class（`OneStarMaker.Tests.SceneSystem.SceneDirectorSceneScopedAssetLoadTests`）を加えたものを使う。
- 実行の副作用: Unity が `Assets/MobileDependencyResolver/` の `.pdb.meta` 5 件を消す事象が再発したので、`git checkout` で戻した（commit には含めない。§6）。
- 発見 C 担当: Anthropic 系（Claude Sonnet 系）。

### 発見 C 2 巡目（2026-10-29）

- **種別:** 発見 C（GO 判定ではない）。対象 implementation head `4cb3b8d3`（前巡 `eba9ddaf` からの修正差分 `eba9ddaf..4cb3b8d3`、テスト 3 ファイルのみ）。
- **F1〜F3 の解消:**
  - F1: `Assert.CatchAsync<OperationCanceledException>` に変更。解消（実行で pass）。
  - F2: `PlacedVfxAuthoringTests.InspectUpdateMode(...)` が null であることを要求する形に変更。AC14 の規約（行頭一致、ちょうど 1 件、値 3）と同じ判定になった。解消。
  - F3: 非対応のテストに、進入後の session の在室数 1 と、保持する handle が `NullVfxHandle.Instance`（非対応の `Spawn` が返す no-op）であることの陽性対照を追加。進入が届かなければ失敗する。session の内部は reflection で読む（製品コードにテストのための面を足さない、とコメントに理由あり）。解消。
- **実行（`pwsh tools/run-tests.ps1 -WithGraphics`、Editor は閉じた状態、1 プロセス）:** filter `SampleGame.Tests.World.LocalVfxEventSessionTests;SampleGame.Tests.Rendering.Vfx.LocalVfxEventAuthoringTests;SampleGame.Tests.Rendering.Vfx.PlacedVfxAuthoringTests;SampleGame.Tests.Rendering.Vfx.LocalVfxEventPlayTests;OneStarMaker.Tests.Rendering.Vfx.VfxSystemSceneDirectorTests;OneStarMaker.Tests.SceneSystem.SceneDirectorSceneScopedAssetLoadTests`。46 件中 46 件 pass、exit 0（session 18、`PlacedVfxAuthoringTests` 13、`VfxSystemSceneDirectorTests` 6、Play 5、AC5 の 2、authoring 2）。Inconclusive・Skipped 0。
- **構造適合:** 修正差分はテストのみで、製品コード・Framework・asmdef の diff は 0。前巡の構造適合の結論は変わらない。
- **阻害する指摘:** なし。新たな違反根拠も見つからなかった。
- **後続スライスへ移送する入力:** なし（§6 の既存記録のみ）。
- **未実行（判定 C で行う）:** 全 EditMode 回帰（空 filter）、AC16、AC17 の既存回帰（`VfxScenePlayTests`、`SessionCellCompanionLoadDriverTests`）、`contract-audit`、Evidence の stage と C 用 bundle。
- 実行の副作用: `.pdb.meta` 5 件の消失が再発したので `git checkout` で戻した（commit には含めない）。
- 発見 C 担当: Anthropic 系（Claude Sonnet 系）。

### 判定 C（2026-10-29、GO 候補 implementation head `4cb3b8d3`。未完了）

- **種別:** 判定 C の途中記録。判定は出していない（完了とも記録しない）。C' は起動していない。
- **実行（`pwsh tools/run-tests.ps1`、空 filter、全 EditMode、-nographics、Editor は閉じた状態）:**
  1. 1 回目: XML は完成（1410 件中 passed 1387・failed 0・skipped 0・inconclusive 23）したが、Unity が終了せず CPU 時間が増え続け、runner を停止した。`step.json` なし。強制停止を伴うため成功証拠にしない。
  2. Harness README「Unity終了時の既知native stall」の停止規則に従い、同じ head で 1 回だけ再取得: XML は完成（同じ 1410 件・passed 1387・failed 0・skipped 0・inconclusive 23）したが、XML の完成後も Unity プロセスが 10 分以上終了しなかった。同じ症状。既知リスクとして raw のまま記録し、追加の調査・再取得はしない。
  - 再取得の `step.json`（後から生成）: status `failed`、exitCode 1、理由は「Inconclusive のテストがあります」と「Unity process が正常終了しませんでした: 1」。
  - inconclusive 23 件は graphics が要る Play テスト（`VfxGraphBackendPlayTests` 18、`VfxGraphRenderPlayTests` 1、`VfxScenePlayTests` 2、`VfxSceneRenderPlayTests` 1、`LocalVfxEventPlayTests` 1）。`-WithGraphics` 側で実行する。
- **`contract-audit`:** errors 0、warnings 0。
- **PC 再起動後の追加実行（同じ head `4cb3b8d3`）:**
  - `-WithGraphics`、filter は `SampleGame.Tests.Rendering.Vfx`・`OneStarMaker.Tests.Rendering.Vfx`・`SampleGame.Tests.World.LocalVfxEventSessionTests`・`OneStarMaker.Tests.SceneSystem.SceneDirectorSceneScopedAssetLoadTests` ほか。232 件中 232 件 passed、failed 0、inconclusive 0、skipped 0、exit 0、Unity は正常終了。収録（pass 件数）: `LocalVfxEventPlayTests` 5（AC7〜AC12。AC9 は実 backend で pass）、`LocalVfxEventSessionTests` 18、`VfxSystemSceneDirectorTests` 6（AC6 を含む）、AC5 の 2、`VfxScenePlayTests` 5、`VfxGraphBackendPlayTests` 20。
  - `OneStarMaker.Tests.Streaming.SessionCellCompanionLoadDriverTests`（AC17、EditMode）: 15 件 passed、exit 0。
  - AC16: `Build Spring Full` と `Build Spring Whitebox`（`SampleGameContentBuild.BuildSpringFull` / `BuildSpringWhitebox` を `-batchmode -executeMethod` で実行）。どちらも exit 0、`outcome.json` の summary は `Succeeded`。両方の Content Directory の catalog に `Spring_Events_4_2.unity` の Scene payload がある。
- **Evidence:** C 用 bundle（実行 log・XML・`step.json`、AC16 の log・`outcome.json`・catalog、`4cb3b8d3` までの diff、`contract-audit` の出力）を `evidence stage` した。状態: ローカル固定済み（未昇格）。引渡し先は同一 PC の C' 担当（進行役が選ぶ）。取得案内は進行役の共有メモ（side-session-notes）にある。同じ session で `fetch` が成功し、bundle の内容を読めることは確認した。別 session での取得確認は未実施。
- **判定 C の状態:** 判定必須のうち、全 EditMode 回帰だけが Unity の終了 stall で `step.json` が `failed` のまま（XML は 2 回とも 1387 passed・failed 0。inconclusive 23 は graphics 必須の Play テストで、上の `-WithGraphics` 実行で pass）。**この stall は、人間の決定（2026-10-29、「受容して完了」）により既知リスクとして受容した。** テスト失敗・欠測への受容ではなく、raw はそのまま保持する（強制停止を伴う run を成功証拠とはしない）。これにより判定必須が揃い、**判定 C 完了: GO 候補 implementation head `4cb3b8d3` に阻害する指摘なし。** 次は C'（未関与の担当を進行役が選ぶ。C 用 bundle は上記、blind 入力は所見から分離して生成する）。
- 判定 C 担当: Anthropic 系（Claude Sonnet 系）。

## 8. Phase C'

### C' 1 巡目（2026-10-29、最終 C'。高コスト証拠の分類なし）

- **担当:** cursor-agent（xAI、Grok 系）。新規セッションで、出力ファイル 1 つだけを書く読み取り専用の指示で動かした。実行後、ワークスペースに他の変更が無いことを確かめた。
- **担当の選定と理由（§5.1 の委任による）:** 最低条件は、B（Anthropic、Claude Opus 系）とも C（Anthropic、Claude Sonnet 系）とも異なるモデルであること。A〜C に関与していないベンダーは、この PC で使える範囲に無い（Anthropic は A2・B・C、xAI は A2、OpenAI は A3 に関与。OpenAI 系は利用上限で使えないと、発注者が 2026-10-29 に連絡）。そこで、関与が A2 の 1 件（入力版の不変条件レビュー）だけの xAI を選んだ。**独立性制約あり。** 強化条件を満たさず、A2 で同じ系統が設計の不変条件を見ているので、実装監査で見落としが残る余地がある。
- **入力:** C' blind bundle（§0 の取得案内）と、implementation head の `git archive`（この HANDOFF を除く）。bundle の中身は次のとおり。
  - 凍結した Phase A（`c68b6d39` の版）
  - 所見を含まない Phase B の実装結果（`a93fe946` の §6 に、監査対象 head を示す 1 行を足したもの）
  - HANDOFF を除く base..head の完全 diff
  - 判定必須の生証拠（全 EditMode の raw 2 回、`-WithGraphics` の実行、companion driver の実行、AC16 の log・outcome・catalog、`contract-audit`）
  - 発見 C・判定 C の所見と、判定 C に対する人間の裁定は含めていない。C' 用決定記録: なし（1 巡目）。
- **結果:** 阻害する欠陥なし。後続スライスへ移送する新しい指摘もなし。I1〜I7 を破る順序は作れず、AC1〜AC17 はどれも、条件を観測するテストまたは build の生証拠で通っていると監査した。
- **監査できなかった範囲・残存リスク（C' の記録）:**
  - `docs-audit` の出力は bundle に無い。
  - 証拠ログに git hash は無く、head との対応は manifest の宣言による。
  - 全 EditMode の 2 回目は、XML の完成から process の終了まで約 5 時間かかった。終了コードの直接の原因は分からない。
  - `-WithGraphics` の filter のうち `SampleGame.Tests.World.SessionCellCompanionLoadDriverTests` は、実体の namespace と一致しない。同じ 15 件は、空 filter と専用の実行で pass している。
  - 実 Scene の `Spawn` に渡る owner が `Bind(trigger)` であることは、Play テストでは断言していない。根拠は binder の接続と AC1 の単体テスト。
  - 退出の直後に Cell を unload する Play テストは無い。ただし `Stopping` 中も `End` が `Stop` するので、I1・I2 は破れない。
  - 凍結時に後続へ送った範囲は対象外（複数 Collider、退出通知の無い破棄、`LoadAssetAsync` の所有者確認、notifier 単独の Play、Content Directory での `.vfx`）。
- **所見の原記録:** C' の出力（findings）と実行の生出力は、Phase D 用の別 bundle としてローカル固定済み（§0）。判定 C との突き合わせは Phase D で行う。

## 9. Phase D

未着手。
