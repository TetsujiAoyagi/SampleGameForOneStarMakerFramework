# S-4d2 VFX Graph 導入と SampleGame backend

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `A`（A1 初稿。A2 の入力版）
- branch: `claude/s-4d2-vfx-graph-backend`
- implementation base commit: `2734a6ea`（`develop`、PR #142 のマージ）
- implementation head commit: 未固定
- risk: `high`（パッケージ導入、Framework の公開 API と起動・終了順序、所有者・寿命、Scene lifecycle への配線）
- owner: 発注者 / S-4d2 担当
- created: 2026-10-21
- expires: S-4d2 の Phase D 完了時
- harvest to: Architecture §24 §16（VFX 抽象）へ composition・Tick 登録・backend 契約の確認結果を追記する。§04（起動）は起動・終了順序の 1 行だけを追記する（§3.4）
- workflow task: `s-4d2-vfx-graph-backend`（2026-10-21 に `workflow-task.ps1 start` 済み。Phase B で再度 start しない）
- Phase A snapshot の取得案内・状態: 未生成（A2 入力版の commit 後に §5 へ記録する）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票: Phase A の途中（A2 独立レビュー待ち）。A3 の凍結は人間の承認を待つ。同じ session で Phase B に入らない。

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
- **VFX Graph（§6.1）:** `com.unity.visualeffectgraph` を導入する。採用 version は d2 の Phase A で互換性を確認して固定する（旧指定 `17.5.0` は現行 URP `17.6.0` と一致しないので使わない）。必要な Runtime assembly reference だけを SampleGame 側 asmdef へ追加する。PC Quality で描画を必須受入とする。Mobile では起動時に backend が capability を判定して対応可否だけを返し、非対応なら `VisualEffect` を有効化しない。診断ログは Framework が一度だけ出し、backend と Scene 側は出さない。Mobile 用 ParticleSystem fallback は作らない。PC 必須・Mobile 安全停止を正式な製品判断とする。
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

保証の範囲: d2 が保証するのは、(a) package と backend が現行 project で動くこと、(b) Framework の composition が d1 の約束（配線順・Tick・終了順序）を満たすこと、(c) 非対応の判定結果が 1 つの出所から動的 VFX と配置済み VFX の両方に効くこと。実 Scene への配置（d3）、Event からの起動（d4）、Player での描画は保証しない（§2.5）。Mobile 実機での判定は、判定に使う platform の事実を差し替えたテストで代える（§2.7 U2）。

**不変条件（起きてはいけないこと）**

- **I1 非対応で有効化しない:** 判定が非対応（または判定不能）のとき、backend の `Create` は 0 回、`PlacedVfxActivator` を付けた配置済み `VisualEffect` の `enabled` は false のまま、backend と `PlacedVfxActivator` のログは 0 件、例外 0 件。診断ログは `VfxSystem` の 1 件だけ。
- **I2 判定の出所は 1 つ:** platform の事実の評価は backend の `QueryCapability` の中だけで行い、Framework の composition が `VfxSystem` を 1 つ作るたびに 1 回だけ起きる。`PlacedVfxActivator` は backend が公開した結果を読むだけで、自分で評価しない。結果が未公開（`Undetermined`）なら非対応と同じに扱う。
- **I3 backend 契約を破らない:** 実 backend で、(a) `Create` 直後は発射していない（`Play` までは粒子 0）、(b) `Create` が失敗したら GameObject を残さない、(c) 同じ backend の生涯で id を再利用しない、(d) `Destroy` は未知の id・破棄済み GameObject・破棄済み source に対して例外を投げずに完了し、2 回目は何もしない、(e) `Destroy` から戻った時点で `VisualEffect` は無効化され source（`visualEffectAsset`）を参照していない（native の破棄完了はフレーム末だが、その間に source を参照しない）、(f) backend はログを出さない。
- **I4 終了順序:** `ReleaseAll` で、`VfxSystem.Dispose` は Tick 要素の登録解除の後、`_loggerFactory.Dispose`・`_sceneDirector.Dispose`・`AssetManagement.ReleaseAll` より前に完了する。backend の `Dispose` は `VfxSystem.Dispose` の後。`ReleaseAll` を 2 回呼んでも二重に Dispose しない。backend の Dispose の後に backend の他のメソッドは呼ばれない。
- **I5 境界を変えない:** Framework（`OneStarMaker.*`）に VFX Graph の型と asmdef 参照が無い。SampleGame の asmdef 参照の追加は、Editor のコンパイルが要求した場合の `SampleGame.Common` → `Unity.VisualEffectGraph.Runtime` 1 件だけ（§2.6）。`SceneState` / `SceneEventType` / `LoadType` / `AssetOwner` / `SceneDirector` / `SceneBase` / `AssetManagement` の公開面を変えない。Framework の公開面の追加は §3.2 の 2 件だけ。
- **I6 Tick の負担と有限性:** `Tick` は毎フレーム unscaled delta で呼ばれる（`Time.timeScale = 0` でも消え切り待ちが有限で終わる）。instance 0 の `Tick` は managed heap を確保しない。`BindSceneLifecycle` の `Subscribe` が例外を投げたら、Scene 所有の `Spawn` を拒否する状態に戻す。

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
- **AC2（I2・I1）** `VfxGraphCapabilityPolicy.Evaluate(VfxPlatformFacts)` の表駆動テスト（EditMode、Unity の SystemInfo を読まない）: compute 非対応、`maxComputeBufferInputsVertex == 0`、`OpenGLES3`、`Null`（graphics なし）、Gamma color space のそれぞれで false と理由、全て満たす PC 相当（`Direct3D11` / `Direct3D12` / `Vulkan`、Linear）で true。
- **AC3（I2）** 実 backend に固定の facts を注入したテスト: `VfxSystem` の生成で facts の読み取りは 1 回、結果は `VfxCapabilityState` に 1 回公開される。backend の `Dispose` で `Undetermined` に戻る。合格例: `VfxSystem` を作る → `Current == Supported`。不合格例: `PlacedVfxActivator` が facts を自分で読む。
- **AC4（I1）** facts を非対応に固定した実 backend の `VfxSystem` で `Spawn` を 10 回: 全て何もしない handle、GameObject の生成 0、例外 0、ログは `VfxSystem` の Warning 1 件（偽 logger で数える）。同じ状態で `PlacedVfxActivator` 付き prefab を生成 → `VisualEffect.enabled == false`、ログ 0 件。`Undetermined` のまま生成しても false。
- **AC5（S2）** `[UnityTest]` + `EnterPlayMode`、`-WithGraphics`、Quality を `PC` に固定: 黒背景のカメラを `RenderTexture` へ描画し、その前方に最小 VFX を `Spawn` → `Play`、所定のフレーム数（frame で待つ。`Task.Delay` を使わない）後に読み戻した画素のうち背景と異なる画素が閾値以上。`Play` 前（`Create` 直後）の同じ計測は閾値未満（I3 (a)）。閾値は fixture の大きさから Phase B が決めて §6 に記録する。
- **AC6（I3）** 実 backend の契約テスト（Play 中）: (a) `Create` 直後の数フレームは粒子 0、`Play` 後に粒子が出る、(b) source に `VisualEffectAsset` でない Object を渡すと `VfxInstanceId.Invalid` が返り GameObject が増えない、(c) 1000 回の `Create` / `Destroy` で id の重複 0、(d) 未知の id・Destroy 済みの id・外から `Object.Destroy` 済みの GameObject・source を破棄した後の `Destroy` が例外なし、(e) `Destroy` から戻った直後に `VisualEffect.enabled == false` かつ `visualEffectAsset == null`、(f) テスト中の Unity ログに backend 由来の行が 0。
- **AC7（S3）** 実 backend + Framework の `VfxSystem`（Play 中、Tick は Framework の Tick 要素から）: 即時 `Stop` の次フレームで GameObject が `== null`。`StopAllowingParticlesToFinish` の後、粒子が消えた時点（fixture の寿命から決めるフレーム数の範囲内）で破棄される。粒子が消えない fixture では timeout（テストでは短い timeout を ctor に渡す）で破棄される。`Bind(go)` の go を `Object.Destroy` すると次フレームまでに instance が破棄される。
- **AC8（S7）** 親ありの `Spawn` の GameObject は親の子、親なしは backend の root（`DontDestroyOnLoad` の Scene にある）の子。backend の `Dispose` で root と残った instance が破棄される。
- **AC9（S4）** `CreateVfxBackend` が null のとき（テスト用の initializer 部品または `VfxRuntimeHost` の単体テスト）、`VfxSystem` の診断ログは 1 件で、理由文に未構成を示す固定文言を含む。非対応の facts での理由文とは異なる。
- **AC10（S6・I4）** `VfxRuntimeHost` の単体テスト（EditMode、偽 backend・実 `UpdateCoordinator`）: 生成で Tick 要素が登録・active になり、`OnElementUpdate` が `UnscaledDeltaTime` を `Tick` へ渡す（`timeScale = 0` 相当の context でも timeout が進む）。`Dispose` の順序が「要素の登録解除 → `VfxSystem.Dispose` → backend の `Dispose`」で、2 回目の `Dispose` は何もしない。`AbstractApplicationInitializer` の配線は diff で確認する（`BindSceneDirector` の呼び出しが `new SceneDirector` の直後で `RegisterAlreadyLoadedScenes` より前、`ReleaseAll` の `DisposeVfx` が `_updateSystemHost.Dispose` の後で `_loggerFactory.Dispose` より前）。
- **AC11（I6）** `VfxSystem` のテスト: instance 0 で warm-up 後の `Tick` 100 回の `GC.GetAllocatedBytesForCurrentThread()` の増分が 0。Stopping の handle がある場合の既存テスト（d1）は全て通る。`Subscribe` が例外を投げる observable で `BindSceneLifecycle` → 例外は呼び出し元へ届き、その後の `Spawn(Scene(id))` の `Create` は 0 回。
- **AC12（S5・I1）** prefab（無効の `VisualEffect` + `PlacedVfxActivator`、Editor 操作で保存）を Play 中に生成: `Supported` で `enabled == true`、`Unsupported` / `Undetermined` で false。`PlacedVfxActivator` は `VisualEffect` を有効から無効にする処理を持たない（有効化前に効く仕組みであることの構造上の確認）。
- **AC13（I5）** `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` が合格。base `2734a6ea` → implementation head の `git diff --name-status` が §4 の変更対象（と配下の `.meta`、本 HANDOFF、program 文書）に閉じる。Framework の `.cs` に `UnityEngine.VFX` の using / 型が無い。asmdef の差分は §2.6 の 1 件以内。
- **AC14（回帰）** 判定必須テスト（§5）が failed 0。d1 の `OneStarMaker.Tests.Rendering.Vfx` が全件 passed。

### 2.3 状態と順序の手順

- **能力判定の状態（`VfxCapabilityState`、SampleGame の static）:** `Undetermined` → `Supported` / `Unsupported` の 1 回だけの遷移。backend の `QueryCapability` だけが書き、backend の `Dispose` だけが `Undetermined` に戻す。Domain Reload 無効の Play 再開では、Framework の `SubsystemRegistration` の `ReleaseAll` が backend を Dispose するので、前の session の結果は残らない。
- **起動:** `InitializeBeforeSceneLoad` の `_loggerFactory` 生成の後で `VfxRuntimeHost.Create(CreateVfxBackend(), loggerFactory, updateSystemHost)`。`VfxSystem` の ctor が `QueryCapability` を呼ぶので、Editor の Play で開いていた Scene の Awake（`BeforeSceneLoad` の後）でも結果は公開済み。`CreateVfxBackend` が例外を投げたら他の `Create*` hook と同じく `BeforeSceneLoad` の失敗にする（VFX を黙って無効にしない）。
- **Scene lifecycle:** `AfterSceneLoad` の `new SceneDirector(...)` と `_updateSystemHost.BindSceneDirector(...)` の直後に `_vfxRuntimeHost.BindSceneDirector(_sceneDirector)`。`BindSceneLifecycle` が例外を投げたら（I6）、host は Error を 1 回ログに出し、起動は続ける（Scene 所有の `Spawn` だけが拒否される）。
- **更新:** Tick 要素は新しい layer `UpdateLayerIds.Vfx`（order 55。Camera 50 の後、Streaming 60 の前）に登録し、CameraSystem と同じく `ActivatePendingRegistrations()` で即時 active にする。Scene 安定待ちの gate を経由すると、gate が開かない経路（Player の BS4 smoke 等）で消え切り待ちが終わらなくなるため。順序の根拠: Camera の後なので、そのフレームの camera 位置が確定した後に回収判定が走る。Streaming の Scene unload より前に走るが、Scene 所有の回収は scene event で同期に起きるので順序に依存しない。
- **終了:** `ReleaseAll` の `_updateSystemHost.Dispose()` の直後に `DisposeVfx()`（host の `Dispose`: 要素の登録解除 → `VfxSystem.Dispose` → backend が `IDisposable` なら `Dispose`）。logger と SceneDirector と AM より前なので、回収時のログが出せ、source asset は instance の破棄要求より後に解放される（Architecture §24 §16.4 の source 契約）。Play Mode 終了で Unity が先に Scene を解体した場合も、I3 (d) により backend の `Destroy` は安全に完了する。

### 2.4 d1 から送られた事項の裁定

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
| `Destroy` 要求と native 実体の破棄完了の差 | backend の `Destroy` は `VisualEffect.enabled = false`・`visualEffectAsset = null`・GameObject の非 active 化を同期に行ってから `Object.Destroy` する（I3 (e)・AC6 (e)） |
| `VfxSpawnRequest.Parent` が null のときの配置先 | backend が遅延生成する `DontDestroyOnLoad` の root（S7・AC8）。active Scene に置くと、所有者と無関係な Scene の unload で Unity が先に壊すため |

**d1 の C / C' からの入力**

| 入力 | 採否 | 理由 |
|---|---|---|
| instance 0 でも `Tick` が毎フレーム `List` を 2 つ確保する | 採用（I6・AC11） | d2 で毎フレーム登録するので、常時の GC 負担になる |
| 拒否（`default(AssetOwner)`・終端中 Scene・破棄済み go・null source）の診断 | 不採用 | 拒否は Game が分岐しないための正常系で、毎フレームの `Spawn` でログが溢れる。最初の Game 利用者である d4 の Phase A で必要なら再検討する |
| `Tick` の全体 try/catch で backend 以外の例外が同じ Tick の残りを飛ばす | 不採用 | backend 例外は handle ごとに隔離済み。残りは Framework 内の不具合で、隔離より修正の対象。次の Tick で残りは処理される |
| Unsupported の `VfxSystem` も scene event を購読する | 不採用 | 購読の処理は backend を呼ばず、負担は Scene 遷移ごとの数回の分岐だけ |
| `BindSceneLifecycle` の `Subscribe` の例外で lifecycle が開いたまま | 採用（I6・AC11） | d2 で実配線するので、失敗時に Scene 所有を受理し続ける状態を残さない |
| `BindSceneLifecycle` の `ReleaseAll` が try の外で logger の例外が漏れる | 不採用 | `ILogger` 自身の例外は d1 の I3 で対象外とした範囲。host 側（§2.3）が `BindSceneLifecycle` の例外を捕捉する |

### 2.5 ここでは答えない問いと所有スライス

- 配置済み VFX の authoring 検査（`VisualEffect` を有効のまま保存した、`PlacedVfxActivator` が無い、の Editor 検査）と、実 Scene の Load / Unload での停止の確認: **S-4d3**。
- Player（Content Directory 経路）での描画と、VFX shader の build への収録: **S-4d3**（実 Scene の asset が build に入る最初のスライス）。
- Cell の距離ストリーミング体積への影響（N-9）: **S-4d3**。
- Game が `IVfxSystem` を受け取る経路（`GameSceneFactory` への注入等）、prefab を source にした `Spawn`、拒否の診断の要否: **S-4d4**。
- notifier の `OnDestroy` の実 Scene unload での発火、同じ identity の Scene の再ロード: **S-4d4**（d1 からの送り）。
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

### 2.7 未決事項（A3 で発注者が決める）

- **U1 Player での描画:** d2 は Editor の Play（`-WithGraphics`）で描画を判定し、Player build は d3 へ送る（§2.5）。program §11 の「PC で表示できる（d2 で 1 件）」をこの読み方で満たすとしてよいか。
- **U2 Mobile 実機:** 非対応の判定は facts を差し替えた実 backend のテスト（AC4）で代え、実機の確認は S-4 で行わない。program §6.1 の「Mobile 安全停止」をこの範囲で満たすとしてよいか。
- **U3 描画判定の経路が成立しない場合:** AC5 の `RenderTexture` 読み戻しは Phase C が初めて実行する（未確認。§5）。batch の graphics ありで VFX が RT に描かれない等で成立しない場合、(a) Editor を通常起動し AI が Game view を撮影、人間が画像で描画を判断する、(b) 問いを分け、描画の判定を d3 へ送る、のどちらにするか。
- **U4 配置済み VFX の authoring 負担:** `VisualEffect` を無効のまま保存する規約では、Edit Mode の Scene view に配置済み VFX がプレビューされない（確認は Play か、一時的に有効にして保存しない操作）。この負担を受け入れるか（代替案と不採用の理由は §3.3）。

## 3. 責務マップ（A1）

### 3.1 ファイルと責務

行数は現在 → 予想。

**Framework（`unity/Assets/OneStarMaker/Scripts/Runtime/`）**

| ファイル | 責務 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 行数 |
|---|---|---|---|---|---|---|
| `Rendering/Vfx/Implements/VfxRuntimeHost.cs`（新規、internal sealed、`IUpdateElement`） | `VfxSystem` 一式の生成・Tick 要素の登録と転送・Scene lifecycle への接続・Dispose 順序。orchestration だけで判定ロジックを持たない | App（`AbstractApplicationInitializer` が所有。`BeforeSceneLoad` 〜 `ReleaseAll`） | `VfxSystem`、`IVfxBackend`、`ILoggerFactory`、`UpdateCoordinator`、`SceneDirector`（`Observable<SceneEvent>` と `ISceneQuery` として） | internal | EditMode 単体（偽 backend、実 `UpdateCoordinator`、`Subject<SceneEvent>`） | 0 → 130 |
| `Rendering/Vfx/Implements/UnconfiguredVfxBackend.cs`（新規、internal sealed） | 未構成を示す backend。`QueryCapability` が false と固定の理由文を返し、他のメソッドは呼ばれない前提で何もしない | `VfxRuntimeHost` が生成（App） | なし | internal（理由文は internal const でテストから参照） | EditMode 単体 | 0 → 45 |
| `Rendering/Vfx/Implements/VfxSystem.cs` | 既存。`Tick` の確保回避（I6）と `Subscribe` 失敗時の lifecycle を閉じる処理（I6）を足す | 既存（App） | 既存 | 変えない | 既存テスト + AC11 | 471 → 500 |
| `Rendering/Vfx/Implements/VfxOwnerIndex.cs` | 既存。Tick 用の再利用 snapshot（件数 0 の早期 return と、Tick 専用の再利用 `List`）を足す | 既存 | 既存 | internal | 既存テスト + AC11 | 115 → 145 |
| `Bootstrap/AbstractApplicationInitializer.cs` | 既存。`protected virtual IVfxBackend? CreateVfxBackend()` の hook と、host の生成・接続・Dispose の 3 か所の呼び出し | 既存 | + `VfxRuntimeHost` | + protected virtual 1 件 | diff 確認（AC10）。既存の bootstrap テストの回帰 | 1196 → 1220 |
| `UpdateSystem/Layers/UpdateLayerIds.cs` | 既存。`Vfx` と `VfxLayerOrder = 55` を足す | 定数 | なし | + public const 2 件 | AC10 | 37 → 45 |

**SampleGame（`unity/Assets/SampleGame/Common/Scripts/Rendering/Vfx/`、namespace `SampleGame.Common.Rendering.Vfx`、asmdef `SampleGame.Common`）**

| ファイル | 責務 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 行数 |
|---|---|---|---|---|---|---|
| `VfxGraphCapabilityPolicy.cs`（新規。`VfxPlatformFacts` struct を同居） | platform の事実から対応可否と理由を決める純粋な規則 | static、状態なし | なし（`GraphicsDeviceType` / `ColorSpace` の enum だけ） | public（SampleGame 内） | EditMode 単体、表駆動（AC2） | 0 → 80 |
| `VfxCapabilityState.cs`（新規、static） | 判定結果の単一の出所。backend が書き、`PlacedVfxActivator` が読む | Domain 内 static。backend の Dispose で `Undetermined` に戻る | なし | public `Current`、internal の書き込み | AC3 | 0 → 50 |
| `VfxGraphBackend.cs`（新規、`IVfxBackend`・`IDisposable`） | VFX Graph の `VisualEffect` を作る・再生する・止める・壊す。facts の読み取りは注入（既定は `SystemInfo` / `QualitySettings`） | `AbstractApplicationInitializer`（Framework）が `CreateVfxBackend` で受け取り、host が Dispose する（App） | `UnityEngine.VFX`（engine module）、`VfxGraphCapabilityPolicy`、`VfxCapabilityState` | public（SampleGame 内） | Play 中の契約テスト（AC5〜AC8） | 0 → 230 |
| `PlacedVfxActivator.cs`（新規、MonoBehaviour） | 無効のまま保存された配置済み `VisualEffect` を、`Supported` のときだけ Awake で有効にする | 置かれた Scene / prefab の寿命 | `VfxCapabilityState` | public MonoBehaviour | AC12 | 0 → 45 |
| `unity/Assets/SampleGame/DependOnAll/AppInitializer.cs` | 既存。`CreateVfxBackend` の override 1 件 | 既存 | + `SampleGame.Common.Rendering.Vfx` | 変えない | 回帰 | 422 → 428 |

**テスト**

| ファイル | 内容 | 行数 |
|---|---|---|
| `unity/Assets/OneStarMaker/Tests/Rendering/Vfx/VfxRuntimeHostTests.cs`（新規） | AC9・AC10 | 0 → 220 |
| `unity/Assets/OneStarMaker/Tests/Rendering/Vfx/VfxSystemTickAllocationTests.cs`（新規） | AC11（確保 0 と `Subscribe` 失敗） | 0 → 110 |
| `unity/Assets/SampleGame/Tests/Rendering/Vfx/VfxGraphCapabilityPolicyTests.cs`（新規） | AC2・AC3 | 0 → 120 |
| `unity/Assets/SampleGame/Tests/Rendering/Vfx/VfxGraphBackendPlayTests.cs`（新規、`EnterPlayMode`） | AC4〜AC8・AC12 | 0 → 420 |
| `unity/Assets/SampleGame/Tests/Rendering/Vfx/Fixtures/`（新規、Editor 操作で作成） | 最小 `.vfx`（短い寿命の粒子を出し続ける 1 system）と、粒子が消えない `.vfx`（timeout 用）、`PlacedVfxActivator` 付き prefab | asset |

**package と設定:** `unity/Packages/manifest.json`、`unity/Packages/packages-lock.json`、package の import で Editor が書く `unity/ProjectSettings/VFXManager.asset`（と import が書く ProjectSettings。§4 の許容）。

### 3.2 配置と依存を選んだ理由

- **composition を Framework に置く。** `BindSceneLifecycle` は `new SceneDirector` の直後・最初の Scene のロード前に呼ぶ必要があり、Dispose は logger・SceneDirector・AM より前に置く必要がある。どちらも `AbstractApplicationInitializer` の private な処理の間で、Game の hook からは割り込めない（SampleGame の CameraSystem 等は別の `Application.quitting` handler で、Framework の `ReleaseAll` との前後は登録順に依存する）。Framework は既に SceneDirector・AM・UpdateSystemHost の寿命と順序を所有しているので、同じ場所に置くと順序が 1 か所で読める。Game の差し込みは `CreateVfxBackend` 1 件に絞る。代替（SampleGame が `VfxSystem` を所有し、Framework に「SceneDirector 生成直後」と「解放の途中」の hook を 2 つ足す）は、公開面が増え、順序の約束が二つのクラスに分かれるので採らない。
- **`VfxRuntimeHost` を分ける。** 1196 行の initializer に判定や順序の詳細を入れず、呼び出し 3 か所だけにする。host は orchestration だけなので、偽 backend と実 `UpdateCoordinator` で単体テストできる。
- **Framework の公開面の追加は 2 件:** `AbstractApplicationInitializer.CreateVfxBackend()`（protected virtual、既定 null）と `UpdateLayerIds.Vfx` / `VfxLayerOrder`（定数）。Game が `IVfxSystem` を受け取る経路は d4 で決める（d2 に Game の利用者がいない）。
- **backend 一式を `SampleGame.Common` に置く。** `PlacedVfxActivator` は d3 の InGame の Scene に、backend は DependOnAll の composition に、それぞれ使われる。両方が参照する `SampleGame.Common` に置けば asmdef の追加が要らない。`DependOnAll` は全体配線の場所で、Scene に置く MonoBehaviour を持たせない。
- **判定の規則を純粋な static に分ける。** `SystemInfo` を読む I/O と規則を分け、規則を表駆動で単体テストする（Mobile 実機の代わりの証拠になる。U2）。
- **`VfxCapabilityState` を static にする。** `PlacedVfxActivator` は Scene の deserialize で生成され、composition から注入できない。`BeforeSceneLoad` で結果が公開済みになることを §2.3 で保証し、`Undetermined` は非対応として fail-closed にする。
- **`.vfx` fixture はテストフォルダに置く。** Editor-only の test assembly から `AssetDatabase` で読むので、build に入らない。d3 は自分の Scene 用の asset を作る。

### 3.3 配置済み VFX を止める仕組み（program §6.3 の「有効化前に適用できるか」）

- **採用:** `VisualEffect` を無効（`enabled = false`）のまま保存し、同じ GameObject の `PlacedVfxActivator` が Awake で `Supported` のときだけ有効にする。無効のまま deserialize されるので、非対応環境で一度も有効にならないことが構造で決まる（AC12）。
- **不採用:** 有効のまま保存し、`[DefaultExecutionOrder]` の早い script が Awake で無効にする。native の `VisualEffect` の有効化は script の Awake の順序と独立に起きうるので、有効化前に止められることを保証できない（一度有効になれば GPU 資源の確保や非対応 API の呼び出しが先に起きうる）。
- **不採用:** `SceneManager.sceneLoaded` で backend が Scene を走査して止める。Awake / OnEnable の後に届くので有効化前ではない。
- 負担: Edit Mode の Scene view でプレビューされない（U4）。authoring の検査は d3。

### 3.4 harvest 先

Architecture §24 §16 に「composition（`VfxRuntimeHost`、起動・終了順序、Tick の layer）」と「SampleGame の backend（判定の規則、配置済み VFX の停止、`Destroy` の同期処理）」を追記し、§16.5 の d2 行を消す。§04 の起動・終了順序の記述に VFX の 1 行を足す。新しい文書は起こさない。

### 3.5 行数警報

- `AbstractApplicationInitializer.cs`（1196 行、既に 500 超）: 非分割。d2 は呼び出し 3 か所と hook 1 件（+24 行、2%）で、責務は host へ分けた。既存の大きさは本スライス由来ではない。
- `VfxSystem.cs`（471 → 約 500）: 非分割。追加は既存の Tick と `BindSceneLifecycle` の中の修正で、d1 で 3 つに分けた責務（状態遷移・索引・受理規則）の境界を変えない。
- `VfxGraphBackendPlayTests.cs`（約 420）: 非分割。全て同じ Play 中の fixture（カメラ・RT・backend）を共有する。500 を超える見込みになったら、描画（AC5）と契約（AC6〜AC8・AC12）で分ける。
- 3 責務・50% 増加の警報: 該当なし（`VfxOwnerIndex.cs` は +26% の見込み）。

## 4. 実装計画

- 変更対象: §3.1 の全ファイル（新規フォルダの `.meta` を含む）、`unity/Packages/manifest.json`、`unity/Packages/packages-lock.json`、package の import で Editor が書く `unity/ProjectSettings/` のファイル。ProjectSettings で `VFXManager.asset` 以外が変わったら、その内容を §6 に記録する（import 由来なら B 適応。手で編集しない）。
- 順序: (1) Editor で package を import し、コンパイルを確認する → (2) Framework の修正（`VfxSystem` / `VfxOwnerIndex`）とテスト → (3) `VfxRuntimeHost` / `UnconfiguredVfxBackend` / `UpdateLayerIds` / initializer の配線とテスト → (4) SampleGame の規則・状態・backend・activator → (5) Editor で fixture（`.vfx` 2 件、prefab 1 件）を作る → (6) Play 中のテスト → (7) contract-audit / docs-audit、Editor のコンパイル確認。
- Phase B から Phase A へ差し戻す条件: §2.6 以外の asmdef 参照が要る。Framework の公開面に §3.2 以外の追加が要る。`VisualEffect` を無効のまま保存しても deserialize 時に有効化される（§3.3 の前提が崩れる）。`VfxSystem` の受理・回収規則（d1 の契約）の変更が要る。
- 対象外を維持する方法: 実証 Scene を作らない。`GameSceneFactory` を変えない。`IVfxSystem` の公開経路を足さない。

## 5. テストとレビュー計画

- 単体テスト: `VfxGraphCapabilityPolicyTests`（純粋）、`VfxRuntimeHostTests`・`VfxSystemTickAllocationTests`（偽 backend、Unity なし）。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Rendering.Vfx;SampleGame.Tests.Rendering.Vfx`（Play 中のテストを含むときは `-WithGraphics`。C が根拠付きで変更可）。
- 判定必須テスト（GO 候補 head）: `pwsh tools/run-tests.ps1 -WithGraphics`（空 filter の全 EditMode 回帰。Play 中のテストを同じプロセスに集約する）。XML で §3.1 のテスト 4 ファイルのテストが収録され、failed 0 であること。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: `VfxGraphBackendPlayTests`（Editor-only asmdef の EditMode テストで `EnterPlayMode` / `ExitPlayMode`。PlayMode platform は採用しない）。
- 操作・実行時・目視条件の検証経路:
  - **AC5（PC で描画）:** 担当は Phase C（Unity テスト）。環境はこの PC、Unity 6000.6.0f1、Quality `PC`、`pwsh tools/run-tests.ps1 -WithGraphics -Filter SampleGame.Tests.Rendering.Vfx`。観測は RT の画素数（構造の検査）。証拠は結果 XML とテストが出力する計測値（画素数・閾値）を evidence bundle に収録する。補助として Phase C は RT を PNG に保存し bundle に入れる（人間の判断には使わない）。
  - **AC1 のコンパイル確認:** 担当は Phase B（Editor の Console）。動作確認で Phase B の head を Editor で開き直し、Console のエラー 0 を観測する。
  - 他の条件はテストで閉じる。
- 未知の操作経路の疎通結果: **未確認。初回確認は Phase C**（AC5）。理由: Unity テストの実行は Phase C の責任で、Phase A では実行しない。担当: 判定 C の実行担当。確認地点: 発見 C の起点 filter の最初の実行。不成立時の対応: U3 の A3 裁定に従う。
- 人間の判断が必要な条件: なし（U3 で (a) を選んだ場合だけ、描画の判断を人間が画像で行う）。
- 高コスト証拠: なし
- 判定 C の照合 script: なし（XML のテスト名・件数と contract-audit・docs-audit の出力で判定する）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC13 の `git diff --name-status`。
- A0/A1 主担当: Claude Code（Anthropic）
- A2 独立レビュー: 未実施（A2 入力版の commit 後に記録する）
- A3 統合担当: 人間（発注者）と主担当
- C' 用に予約した担当: xAI 系列（`cursor-agent` の Grok）を Phase A で使わずに残す。
- 独立性の強化条件を満たせない場合の理由: 未定（Phase B・C のモデルが決まった時点で記録する）。

## 6. Phase B 実装結果

未着手。

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
