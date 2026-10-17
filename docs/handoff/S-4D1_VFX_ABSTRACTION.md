# S-4d1 Framework の VFX 抽象

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `A`（A2 実施中。A3 未凍結）
- branch: `claude/s-4d1-vfx-abstraction`
- implementation base commit: `ba8245f4`（`develop`、PR #134 のマージ）
- implementation head commit: 未固定（Phase B で記録）
- risk: `high`（Framework の公開 API、所有者・寿命、Scene lifecycle 通知への新しい購読者）
- owner: 発注者 / S-4d1 担当
- created: 2026-10-17
- expires: S-4d1 の Phase D 完了時
- harvest to: Architecture §24（Rendering）へ「VFX 抽象」の節を追加する。§05（Scene）は変更しない（§3 の「harvest 先」参照）
- workflow task: `s-4d1-vfx-abstraction`（2026-10-17 に `workflow-task.ps1 start` 済み）
- Phase A snapshot の取得案内・状態: A1 入力版は commit と本文の content sha256 で固定する（文書だけなので stage は不要）。値は §5「A2 独立レビュー」に記録する。A3 凍結版は未生成
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A3（人間と主担当による統合と凍結）。
  - 次の作業: §5 の A2 指摘を採用・不採用・保留に分類し、本文を改訂して凍結版の commit と content sha256 を記録する。凍結と同じ PR で `S-4_FULL_SPEC_WORLD_AUTHORING.md` の §6.3 d1 行・§6.4・§10 を、凍結した型名・配置・寿命に合わせて更新する（program §0 末尾の規則）。
  - 引き継ぐ commit: §5「A2 独立レビュー」に記録した A1 入力版 commit。
  - 必要な権限: なし（文書のみ）。Unity Editor・テスト・資格情報は不要。
  - 停止規則: A3 の凍結は人間の承認を待つ。同じ session で Phase B に入らない。
  - 引き渡さないもの: なし（Phase A 内）。

## 1. 目的と対象外

- 目的: VFX の寿命、非対応環境での停止、例外の隔離を、VFX 実装の型（VFX Graph の `VisualEffect` 等）に依存しない Framework の契約として実装し、偽 backend で単体テストする。
- 対象外（所有スライス）:
  - `com.unity.visualeffectgraph` の導入、version 固定、SampleGame の実 backend、起動時の実 capability 判定、Scene に配置済みの `VisualEffect` を止める仕組み、確認用 asset、App composition への組み込み（`VfxSystem` の生成、SceneDirector への接続、`Tick` の `UpdateSystemRuntime` 登録）: **d2**
  - `Spring_AtmosphereVFX` / `Spring_VFX_4_2` の VFX Scene と Cell 体積: **d3**
  - `Spring_Events_4_2` の trigger からの起動と Cell unload での回収（実 Scene の Play Mode 確認）: **d4**
  - `Spring_Event_MultiCellProof` と Planner 実証、S-4 の harvest: **d5**
  - 同時数の上限、優先度、カメラ距離での間引き、pool / prewarm、DebugStudio での観測: 必要になったスライスの Phase A で判断する（program §6.4）
  - Mobile 用 ParticleSystem fallback: 作らない（program §6.1 の製品判断）
- 現況（A0。2026-10-17 の調査）: §1.1〜§1.3。

### 1.1 program から転記した制作契約

**SceneGraph（program §2.1）。** `InGameSession` の下に論理ノード `Season_Spring`（payload なし、OnDemand）があり、その子が `Spring_Lighting`（NecessaryAlways）、`Spring_AtmosphereVFX`（IncrementalAlways / optional）、`Spring_Cell_{x}_{y}`（OnDemand / StreamByDistance）、`Spring_Event_{slug}`（OnDemand / major event）。Cell の子は `Spring_Environment_{x}_{y}`、`Spring_Lighting_{x}_{y}`、`Spring_VFX_{x}_{y}`、`Spring_Events_{x}_{y}` で、全て `StreamByDistance = false`、`LoadType.OnDemand`。親 Cell が Stable になった後、SampleGame の companion loader が明示 `AddScene` し、Unload は親 Cell の再帰破棄に委ねる。

**所有物（program §2.2）。** Cell VFX は Cell 常駐中に継続する環境エフェクト、Season Atmosphere VFX は季節全域の常時エフェクト、Cell Events は trigger・spawn・局所的な進行制御。VFX Scene にゲーム進行 trigger を置かず、Events Scene が VFX の開始・停止を要求する。Scene を跨ぐ `GameObject` の直接参照は禁止し、event id、world position、service query、`IAssetManagement` で寿命を宣言してロードする prefab で接続する。

**Event の粒度（program §2.3）。** 複数 Cell への効果、起点 Cell の Unload 後の継続、独立した Timeline / Cinemachine staging、別担当の独立変更のいずれかに当たるものだけ `Season_Event_{slug}` にし、他は `*_Events_{x}_{y}` に含める。

**VFX の粒度と寿命（program §2.4）。**

- Cell 常駐中ずっと必要な複数エフェクトは、1 Cell 1 VFX Scene に束ねる。
- 季節全域のエフェクトは Season Atmosphere VFX に置く。
- ヒット、破裂、短い演出など一時的な VFX は Scene にせず prefab / `VisualEffectAsset` にする。
- 一時 VFX は `IAssetManagement` でロードし、`AssetOwner.Scene(eventSceneIdentity)` または `AssetOwner.Bind(go)` で寿命を宣言する。

**Companion profile（program §3.2）。** `world:cellCompanionSet` は `Full`（Environment + Lighting + VFX + Events）、`Planner`（Events のみ）、`Lighting`、`VFX`。Planner は Events だけを読むので、Event からの VFX 起動は `Spring_VFX_{x}_{y}` のロードに依存できない（d4 の条件。d1 では、VFX 抽象が VFX Scene の存在を前提にしないことだけを守る）。

**VFX Graph の製品判断（program §6.1）。** PC Quality で描画必須。Mobile では起動時に backend が capability を判定して対応可否だけを返し、非対応なら `VisualEffect` を有効化しない。診断ログは Framework が一度だけ出し、backend と Scene 側は出さない。ParticleSystem fallback は作らない。

**責務配置（program §6.4、2026-10-17 発注者決定）。**

- 抽象は Framework、VFX Graph に依存する実装は SampleGame に置く。Framework に VFX Graph 依存を持たせない。
- Framework の責務は3つ。
  - 寿命の宣言と回収: 起動時に `AssetOwner` のスコープを指定させ、所有者が unload されたら Framework が回収する。回収するのは再生中のインスタンスで、asset の寿命は `IAssetManagement` に残す。所有者の unload は消え切り待ちの Stop を打ち切って回収する。
  - 非対応環境での停止: backend が起動時に1回、対応可否を返す。Framework は非対応なら何もしない handle を返し、診断ログを1回だけ出す。Game 側に分岐を書かせない。
  - 型に依存しない handle: Play、即時 Stop、粒子が消え切るのを待つ Stop、名前付きパラメータ。`VisualEffect` 型を公開面に出さない。
- Scene に置く VFX（d3）の寿命は Scene の寿命で回収されるため、d1 の寿命管理の対象は動的に起動する VFX（d4 / d5）である。非対応時の停止は両方に適用する。何もしない handle は Scene に配置済みの `VisualEffect` を止めないので、配置済みのものを止める処理は SampleGame の backend が持つ（d2）。

**d1 行（program §6.3）。** 答える問いは本書 §2。中核ロジックは偽 backend で単体テストし、動作数は偽 backend が記録する呼び出し回数で確かめる（公開 API にしない）。Phase A の必須検討は (a) Stop / Dispose / 所有者の unload / 消え切り待ちの遅延完了が競合したときの手順、(b) per-frame 更新の要否（既存の lifecycle 通知で足りれば `UpdateSystemRuntime` に登録しない。登録する場合、エフェクト単位の例外隔離は `UpdateSystemRuntime` のシステム単位の隔離に加えた d1 の境界とする）、(c) harvest 先の既存 Architecture 文書。(a) は §2.2、(b) は §3.2、(c) は §0 と §3.4。

**公開 API と依存（program §10）。** Framework 公開面の追加は「VFX の抽象（§6.4 の handle、寿命の宣言、非対応時の停止、backend の差し込み口）」に限る。Framework は season 名、Cell 座標文法、職種 role を知らない。VFX Graph 依存は SampleGame 側に閉じ、OneStarMaker Runtime の公開 API へ `VisualEffect` 型を出さない。`SceneState` の14値は変更しない。新しい LoadType も追加しない。asmdef 参照の追加は d2 で決める。

**検証方針（program §11、d1 の該当分）。** Mobile 非対応時に例外、未解放 handle、無限 retry がない（d1 は偽 backend）。Scene / asset YAML の手編集、`unity test` / `unity run` は禁止。

**スライス境界（program §12）。** 本書にない公開 API、依存、状態、所有者が必要になったら Phase B で決めず Phase A へ戻す。d1 の Phase D は、現況になった契約を Phase A で選んだ既存の Architecture 文書へ harvest し（新しい文書を起こす前に既存への統合を確認する）、本 HANDOFF を削除し、program の §6.3 d1 行を完了に更新する。

### 1.2 既存の通知・解放経路の調査（A0）

発注者経由の外部助言「新しい汎用ライフサイクル管理機構を作る前に、既存の通知・解放経路で成立するかを確認する。`AssetOwner.Scene` は主として `IAssetManagement` が管理するアセットの寿命を表すもので、VFX 側が Scene unload や `Bind(go)` の失効をどう認識するかは別途検討が要る。`SceneBase` の unload フックをまず調べる」を受けて、次を確認した。

| 経路 | 現在の働き | VFX 回収への流用 |
|---|---|---|
| `AssetOwner`（`AssetManagement/Abstractions/AssetOwner.cs`） | 寿命スコープを表す値だけ。`Kind` / `Id` / `BoundObject` / `GameObjectId` は `internal`。自身は通知を持たない | **使う。** 所有者の宣言型として公開 API の引数にする。VFX 抽象を `OneStarMaker.Runtime` asmdef に置けば `internal` の分類を読める |
| `AssetOwner.Scene(id)` の解放 | 通知ではなく、`SceneDirector.DisposeUnloadedScene`（Phase 3）と `CleanupCanceledScene` が `IAssetManagement.ReleaseScene(id)` を明示的に呼ぶ | **使わない。** 呼ばれるのは Unity Scene 本体の Unload（Phase 2）の後で、VFX の instance は既に破棄済みになりうる。`SceneDirector` から VFX へ依存を足すことにもなる |
| `AssetOwner.Bind(go)` の解放 | `internal sealed AssetReleaseOnDestroy`（MonoBehaviour）を go に付け、`OnDestroy` で具象 `AssetManagement.NotifyGameObjectDestroyed(ulong)` を呼ぶ | **同じ型を真似て別に作る。** 既存 component は具象 `AssetManagement` 専用。汎用の多重通知へ広げると AssetManagement の変更理由が増えるので、VFX 専用の小さな notifier を置く（§3.1） |
| `SceneBase` の `OnPreUnLoadedImpl` / `OnAfterUnLoadedImpl` | サブクラスごとの protected virtual フック。S-4c の `SeasonLightingScene` は `OnPreUnLoadedImpl` で RenderEnvironment lease を解放する | **使わない（主経路にしない）。** 各 Game Scene が回収を書く責任を負い、program §6.4「所有者が unload されたら Framework が回収する」「Game 側に分岐を書かせない」に反する。書き忘れが漏れになる。Game がフック内で handle を明示 Stop すること自体は許す |
| `SceneDirector.OnSceneEvent`（公開 `Observable<SceneEvent>`） | 全状態遷移で `StateChanged`、Stable で `Added`、辞書除去後に `Removed`、cancel 回収後に `CancelCleanedUp`、`SceneDirector.Dispose` で `OnCompleted`。Framework の `UpdateSystemHost.BindSceneDirector` と SampleGame の `SceneDirectorTerminalEvents` が既に購読している | **主経路にする（§3.2）。** 新しい公開 event 型も状態値も不要 |
| `UpdateSystemRuntime` | `RegisterElement` で毎フレーム tick。Coordinator はシステム単位で例外を隔離する | **d1 では登録しない。** 毎フレームの処理が要るのは消え切り待ちの完了判定と破棄済み `Bind` 所有者の補助検出だけで、`SoundPlayer.Tick` と同じく `Tick` を公開して登録は composition（d2）に任せる（§3.2） |

`SceneDirector` の終端の網羅（調査結果）:

- 通常 Unload: Phase 1 で `StateChanged(PreUnloading)` → `ExecutePreUnLoad` → `StateChanged(PreUnloaded)`、Phase 2 で `Unloading` / `Unloaded`、Phase 3 で `ExecuteAfterUnLoad` → `ReleaseScene` → `Removed`。子孫は葉が先で同じ順序（全子孫の Phase 1 が先に揃う）。
- Load キャンセル: `TransitionSceneState(LoadCanceled)` が `StateChanged(LoadCanceled)` を出し、その後に Unity Scene unload、`ReleaseScene`、`CancelCleanedUp`。`PreUnloading` は出ない。
- Add 失敗の回収（`RecoverFailedLoadAsync`）: 通常 Unload か、途中状態からの再開で `DisposeUnloadedScene` に到達し `Removed` を出す。
- App 終了: `SceneDirector.Dispose` は Scene を Dispose して Subject を Dispose する（購読者には `OnCompleted`）。個別の `Removed` は出ない。

### 1.3 前例

- S-4c の `RenderEnvironment`: App 寿命の単一 owner を lease と generation token で調停し、stale lease の操作が新しい owner を壊さない。policy は純 C#、Unity I/O は `IRenderEnvironmentSink` に分け、偽 sink で単体テストする。配置は `Scripts/Runtime/Rendering/Environments/{Abstractions,Implements}`。
- `SoundSystem`: `ISoundBackend` は差し替え口で、時間は呼び出し側の `Tick` で進む。`UpdateSystemRuntime` への登録とフレーム順序は所有者（composition）が守る。
- `UpdateSystemHost.BindSceneDirector(SceneDirector)`: Framework の App 常駐物が SceneDirector 生成後に scene event を購読する前例。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: VFX の寿命、非対応環境での停止、例外の隔離を、VFX 実装の型に依存せず、既存の Scene lifecycle 通知と `AssetOwner` だけを使う Framework の契約として保証できるか。
- 判定定義: 進める最低条件（§2.1）を全て満たせば GO。一つでも未達なら NO-GO。スパイクではないので CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし（A3 後に作る場合は本書と別の記録にする）

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I1 所有者より長く生きない:** 所有者が終端に入った後（`Scene(id)` は §2.3 の終端イベント、`Bind(go)` は go の破棄、`App` / `Manual` は `VfxSystem.Dispose`）、その所有者で作った instance が backend に残らない。消え切り待ちの途中でも同じ。
- **I2 二重破棄・破棄後操作がない:** 1つの backend instance に対する `Destroy` は高々1回。`Destroy` 後にその instance への backend 呼び出しは0回。handle の後続操作、遅れて届いた消え切り完了、二回目の所有者終端はどれも backend を呼ばない。
- **I3 例外が漏れない・他を止めない:** backend の例外は、それを起こした instance だけを回収（`Destroy` を1回試み、その例外も握る）して終わる。他の instance の Tick・回収・パラメータ操作は続く。`OnSceneEvent` の購読処理、`Bind` 破棄通知、`Tick`、handle と `Spawn` の全メソッドから呼び出し元へ例外を投げない（`SceneDirector` の unload を壊さない）。
- **I4 非対応時は何もしない:** backend が非対応を返した、または判定が例外を投げた場合、判定呼び出しは生涯1回、それ以外の backend 呼び出しは0回、診断ログは1回、例外0件。`Spawn` は何もしない handle を返す。
- **I5 終端済みの所有者に作らない:** 終端に入った（または未ロードの）`Scene(id)`、破棄済みの go、Scene lifecycle 未接続の `Scene(id)`、`Dispose` 後の `VfxSystem` への `Spawn` は backend の `Create` を呼ばず、何もしない handle を返す。
- **I6 待ちが有限:** 消え切り待ちの Stop は、backend が粒子なしを報告する、Tick 時間で timeout に達する、所有者が終端に入る、`Dispose` される、のいずれかで必ず `Destroy` に至る。同じ instance への再試行ループを持たない。
- **I7 境界を変えない:** Framework 公開面に VFX Graph の型が無い。asmdef 参照の追加0件。`SceneState` / `SceneEventType` / `LoadType` / `AssetOwner` の公開面を変えない。d1 のコードは `UpdateSystemRuntime` に登録しない。SampleGame のファイルを変更しない。

**成立条件（起きなければならないこと）**

- **S1 即時 Stop:** `Spawn` → `Play` → `Stop` で backend の `Create` / `Play` / `Destroy` が各1回呼ばれ、handle は非アクティブになる。
- **S2 消え切り待ち Stop:** `StopAllowingParticlesToFinish` は backend の `StopEmitting` を1回呼び、Tick で backend が粒子なしを返した時点で `Destroy` を1回呼ぶ。
- **S3 Scene 所有者の回収:** 実 `SceneDirector`（既存の `TestableSceneDirector`）で所有者 Scene を Unload すると、`StateChanged(PreUnloading)` の時点で（Phase 2 の Unity Scene unload より前に）instance が `Destroy` される。Load キャンセルでは `StateChanged(LoadCanceled)` の時点で `Destroy` される。
- **S4 Bind 所有者の回収:** 破棄通知（notifier の `OnDestroy` から呼ばれる内部入口）で `Destroy` される。通知が来なかった破棄済み go（偽 null）も次の `Tick` で `Destroy` される。
- **S5 名前付きパラメータ:** float / int / bool / Vector3 / Vector4 の値が名前と一緒に backend へそのまま渡り、backend の成否が呼び出し元へ返る。
- **S6 全回収:** `VfxSystem.Dispose` で全所有者の instance が `Destroy` され、以後の `Spawn` は I5 に従う。

### 2.2 競合の手順（program §6.3 の必須検討 (a)）

handle の状態は `Created`（生成済み・未再生）→ `Playing` → `Stopping`（消え切り待ち）→ `Released` の一方向で、`Released` は終端（吸収状態）。`Released` への遷移は必ず1か所（`Release(reason)`）を通り、そこで backend `Destroy` を1回だけ呼んで状態を先に `Released` へ書く（再入しても二回目は何もしない）。

| 現在 | 操作・事象 | 結果 |
|---|---|---|
| Created / Playing | `Play()` | backend `Play`（Playing 中は再トリガ）。Created → Playing |
| Stopping | `Play()` | 何もしない（`false`）。Stopping は Released にしか進まない |
| Created / Playing | `Stop()` | `Release`（即時 `Destroy`） |
| Playing | `StopAllowingParticlesToFinish()` | backend `StopEmitting` → Stopping。待ち開始時刻を記録 |
| Created | `StopAllowingParticlesToFinish()` | 粒子が無いので即 `Release` |
| Stopping | `Stop()` / `Dispose()` | 待ちを打ち切って `Release` |
| Stopping | `StopAllowingParticlesToFinish()` | 何もしない（待ちを延長しない） |
| Created / Playing / Stopping | `Dispose()` | `Release`（`Stop()` と同じ） |
| Created / Playing / Stopping | 所有者の終端 | 待ちを打ち切って `Release` |
| Stopping | Tick で粒子なし | `Release` |
| Stopping | Tick で timeout 到達 | `Release` |
| 任意 | backend 例外 | ログ（instance ごとに1回）→ `Release`（`Destroy` の例外も握る） |
| Released | 全ての操作・事象 | 何もしない。Set* は `false`、`IsActive` は `false` |

合格例: Stopping 中に所有者 Scene の `PreUnloading` が来て `Destroy` 1回、その後の Tick で粒子なし判定は呼ばれない。不合格例: `PreUnloading` で `Destroy` した後、`Removed` の掃除で同じ instance を再び `Destroy` する。不合格例: Tick の列挙中に、`Destroy` が引き起こした `Bind` 破棄通知で同じ集合を変更して列挙が例外になる（列挙は snapshot に対して行う）。

### 2.3 所有者の終端の定義

| 所有者 | 終端に入る事象 | 終端から外れる事象 |
|---|---|---|
| `Scene(id)` | `StateChanged` で `State` が `PreUnloading` または `LoadCanceled`。保険として `Removed` / `CancelCleanedUp` でも同じ回収を行う（冪等）。`OnCompleted`（SceneDirector の Dispose）で全 Scene 所有者 | 同じ id の `StateChanged(PreLoading)`（同じ identity の再ロード） |
| `Bind(go)` | VFX 用 notifier の `OnDestroy`。保険として `Tick` 時に `go == null`（偽 null を `==` で判定） | なし（go ごとに一度） |
| `App` | `VfxSystem.Dispose` | なし |
| `Manual` | `VfxSystem.Dispose`（呼び出し側が handle を `Dispose` するのが通常） | なし |

`Spawn` 時の `Scene(id)` 受理: Scene lifecycle 接続済み、かつ `ISceneQuery.IsSceneLoaded(id)` が true、かつ終端中の集合に id が無いこと。`OnLoadedImpl` 中（Initializing）の自 Scene 所有は受理する。

### 2.4 受け入れ条件

各条件の検証テストは偽 backend の呼び出し回数で判定する（公開 API にしない）。

- **AC1（S1, I2）** `Spawn` → `Play` → `Stop` → `Stop` → `Dispose`: `Create`=1, `Play`=1, `Destroy`=1。2回目以降は backend 呼び出し0。
- **AC2（S2, I6）** `Play` → `StopAllowingParticlesToFinish` → Tick（粒子あり）→ Tick（粒子なし）: `StopEmitting`=1、`Destroy`=1（2回目の Tick で）。以後の Tick で粒子判定0回。
- **AC3（I6）** 粒子ありのまま Tick を timeout 超過まで進める: timeout を超えた最初の Tick で `Destroy`=1。負・NaN・∞の delta は時間を進めない（不合格例: NaN で即 timeout、負で永遠に待つ）。
- **AC4（I1, I2, S3）** §2.2 の各競合行を1テストずつ（Stopping 中の `Stop` / `Dispose` / 所有者終端 / 二重終端（`PreUnloading` → `Removed`））: 全て `Destroy`=1。
- **AC5（S3, I1）** `TestableSceneDirector` で所有者 Scene を Add → Spawn/Play → Unload: `Destroy` は `PerformUnitySceneUnload` より前に記録される（偽 backend と TestableSceneDirector の呼び出し記録の順序で判定）。子 Scene 所有の instance は親 Unload で回収される。
- **AC6（S3, I1）** Load キャンセル（TestableSceneDirector のゲートで Stable 前に Unload）: `OnLoadedImpl` 中に Spawn した instance が `LoadCanceled` で `Destroy`=1。
- **AC7（I5）** 終端中（`PreUnloading` 後 `Removed` 前）の `Scene(id)`、未ロード id、lifecycle 未接続、破棄済み go、`Dispose` 後: いずれも `Create`=0 で何もしない handle（`IsActive=false`、全操作で例外0）。`Removed` → `PreLoading` 後の同じ id は再び受理される。
- **AC8（S4, I1）** `Bind(go)`: 内部の破棄通知入口で `Destroy`=1。通知せず `DestroyImmediate(go)` した場合は次の Tick で `Destroy`=1。
- **AC9（I3）** backend の `Create` / `Play` / `StopEmitting` / 粒子判定 / Set* / `Destroy` がそれぞれ例外を投げる場合: 呼び出し元への例外0、例外を投げた instance は `Destroy` 試行1回で Released、同じ Tick 内の他 instance の処理は継続、ログは instance ごとに1回。
- **AC10（I3）** scene event 処理中の backend 例外: `TestableSceneDirector` の Unload が完了し（`Removed` 到達）、例外は購読側で握られる。
- **AC11（I4）** 非対応の偽 backend: 判定1回、それ以外0回、Warning ログ1回、`Spawn` を複数回呼んでも同じ。判定が例外: Error ログ1回、非対応と同じ扱い。
- **AC12（S5）** 各型の Set* が名前・値をそのまま渡し、backend の `false` をそのまま返す。Released の handle では backend を呼ばず `false`。
- **AC13（S6, I1）** App / Manual / Scene / Bind 所有の instance がある状態で `Dispose`: 全て `Destroy`=1。scene event の購読も解除される。`OnCompleted` で Scene 所有が全回収される。
- **AC14（I7）** `pwsh tools/contract-audit.ps1` 合格。差分が `unity/Assets/OneStarMaker/` と本 HANDOFF・harvest 先文書に閉じ、asmdef ファイルの変更0件（`git diff --stat` で確認）。公開型のシグネチャに `UnityEngine.VFX` 名前空間の型が無い。

### 2.5 ここでは答えない問いと所有スライス

- 実 VFX Graph backend の粒子判定（`aliveParticleCount` 等）の妥当性、PC 描画、Mobile の実判定: **d2**
- `VfxSystem` の生成と `BindSceneLifecycle` / `Tick` 登録の場所（AbstractApplicationInitializer か SampleGame の AppInitializer か）とフレーム順序: **d2**
- 非対応判定の結果を Scene 側（配置済み VFX）へ見せる口が Framework に要るか: **d2**（d1 は公開しない）
- notifier の `OnDestroy` が実 Scene unload で呼ばれることの Play Mode 確認: **d4**
- 消え切り待ち timeout の既定値の製品妥当性: **d2 / d4**（d1 は設定可能な値として持つ）
- 同時数の上限、優先度、間引き、pool / prewarm、DebugStudio 観測: 必要になったスライスの Phase A

### 2.6 本文へ転記した実装制約（`AGENTS.md` の常時契約から）

- 依存は Game → Framework の一方向。asmdef 参照の追加は設計判断なので行わない。
- アセットは `IAssetManagement` 経由。d1 は asset をロードしない（`Spawn` は呼び出し側がロード済みの `UnityEngine.Object` を受け取る）。
- `SceneState` の14値を変えない。状態変更は `SceneLifecycleManager` が所有する（d1 は読むだけ）。
- 公開 API のログ抽象は `ILogger<T>`。`ZLogger*` 型を公開面に出さない。
- 1つの `UpdateSystem` の例外で他のシステムの `Tick` を止めない（d1 は登録しないが、`Tick` 自体が例外を投げない I3 で整合する）。
- Editor コードを Runtime アセンブリに置かない。
- `record` を使わない。新規・編集する `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object`（`BoundObject`、`Transform parent`、`source`）は `== null` / `!= null` で判定し、`?.` / `??` / `is null` / `ReferenceEquals` を使わない。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。時間は `Tick(delta)` で注入する。
- 参照 0 を削除理由にしない。

### 2.7 未決事項（A2 で意見を求める）

- U1: `Spawn` の引数を `UnityEngine.Object source` にするか、`IAssetHandle<T>` を受け取って handle の失効も見るか。A1 は前者（asset 寿命は `IAssetManagement` に残す program §6.4 に合わせ、VFX 側は asset の失効を見ない）。
- U2: `Tick` を d1 で `UpdateSystemRuntime` に登録しない判断（`SoundPlayer` 型）。登録しない場合、composition が登録を忘れると `App` / `Manual` 所有の消え切り待ちが `Dispose` まで終わらない。A1 はこれを d2 の受け入れ条件に送る。
- U3: `Scene(id)` の回収点を `PreUnloading`（Phase 1 開始時、Unity Scene がまだある）にする判断。その Scene 自身の `OnPreUnLoadedImpl` で消え切り待ちの演出を始めることはできなくなる（program §6.4 の「所有者の unload は消え切り待ちを打ち切る」と整合）。

## 3. 責務マップ

全て新規ファイル。既存ファイルの変更は無い（harvest の Architecture §24 は Phase D）。

### 3.1 ファイルと責務

配置: `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/Vfx/{Abstractions,Implements,Components}/`、namespace `OneStarMaker.Runtime.Rendering.Vfx`、asmdef `OneStarMaker.Runtime`（既存）。

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 依存 | 公開面 | 予想行数 |
|---|---|---|---|---|---|---|
| `Abstractions/IVfxSystem.cs` | Game が VFX を起動する窓口 | 起動 API の形 | — | `AssetOwner`、`VfxSpawnRequest` | public interface。`IVfxHandle Spawn(in VfxSpawnRequest request, AssetOwner owner)` | 30 |
| `Abstractions/IVfxHandle.cs` | 1 instance への型に依存しない操作 | handle 操作の形 | 呼び出し側が保持、寿命は §2.3 | — | public interface（`IDisposable`）。`bool IsActive`、`bool Play()`、`void Stop()`、`void StopAllowingParticlesToFinish()`、`bool SetFloat/SetInt/SetBool/SetVector3/SetVector4(string name, …)` | 50 |
| `Abstractions/VfxSpawnRequest.cs` | 起動要求の値（source、parent、位置、回転） | 起動時の入力項目 | 値 | UnityEngine | public readonly struct | 45 |
| `Abstractions/IVfxBackend.cs` | VFX 実装（d2 の SampleGame）への差し込み口 | backend 契約 | backend は `VfxSystem` が借用（所有・Dispose しない） | `VfxInstanceId`、`VfxBackendCapability` | public interface。`QueryCapability()`、`Create`、`Play`、`StopEmitting`、`HasAliveParticles`、`Set*`、`Destroy`。契約: main thread、`Destroy` は破棄済み GameObject を許容する | 70 |
| `Abstractions/VfxInstanceId.cs` | backend が返す instance の識別値 | 識別子の表現 | 値 | — | public readonly struct（0 は無効） | 40 |
| `Abstractions/VfxBackendCapability.cs` | 対応可否と診断理由 | 判定結果の項目 | 値 | — | public readonly struct（`IsSupported`、`Reason`） | 30 |
| `Implements/VfxSystem.cs` | 所有者の終端を受けて instance を回収し、非対応時に何もしない handle を返す orchestration | 寿命・受理規則・ログ方針 | App 寿命（composition が生成・Dispose）。handle 集合と所有者索引を所有 | `IVfxBackend`、`ILogger<VfxSystem>`、`Observable<SceneEvent>`、`ISceneQuery` | public sealed class。ctor(`IVfxBackend`, `ILogger<VfxSystem>`, `VfxSystemOptions`)、`BindSceneLifecycle(Observable<SceneEvent>, ISceneQuery)`、`Tick(float deltaSeconds)`、`Dispose()`。内部入口 `NotifyOwnerGameObjectDestroyed(ulong)` | 280 |
| `Implements/VfxSystemOptions.cs` | 消え切り待ち timeout 等の設定値 | 設定項目 | 値 | — | public sealed class（既定 timeout 10 秒） | 25 |
| `Implements/VfxHandle.cs` | §2.2 の状態機械と backend 呼び出しの例外境界 | 状態遷移規則 | `VfxSystem` が生成、Released まで索引が保持 | `IVfxBackend`、`VfxSystem`（回収通知） | internal sealed class（`IVfxHandle`） | 170 |
| `Implements/NullVfxHandle.cs` | 何もしない handle | — | 共有 singleton | — | internal sealed class | 35 |
| `Implements/VfxOwnerIndex.cs` | 所有者 → handle の索引と、終端中 Scene id の集合 | 索引の表現 | `VfxSystem` が所有 | `AssetOwner`（internal の Kind/Id/GameObjectId） | internal sealed class | 110 |
| `Components/VfxOwnerDestroyNotifier.cs` | `Bind(go)` の go 破棄を `VfxSystem` へ知らせる | Unity の破棄通知 | go に付く（go と同寿命） | `VfxSystem` | internal sealed MonoBehaviour | 25 |

テスト（`unity/Assets/OneStarMaker/Tests/Rendering/Vfx/`、asmdef `OneStarMaker.Tests` 既存）:

| ファイル | 対象 | 予想行数 |
|---|---|---|
| `FakeVfxBackend.cs` | 呼び出し回数・順序・例外注入・粒子あり/なしを記録・制御する偽 backend | 160 |
| `VfxHandleStateTests.cs` | AC1〜AC4、AC9、AC12 | 330 |
| `VfxSystemOwnerTests.cs` | AC7、AC8、AC11、AC13（`Subject<SceneEvent>` と偽 `ISceneQuery` で駆動） | 300 |
| `VfxSystemSceneDirectorTests.cs` | AC5、AC6、AC10（既存 `TestableSceneDirector` / `SceneDirectorTestBase` を使う統合） | 180 |

### 3.2 配置と依存を選んだ理由

- **Scene 所有者は `SceneDirector.OnSceneEvent` を購読する。** 既存の公開通知で、Framework 内の前例（`UpdateSystemHost`）がある。`Removed` を待たず `PreUnloading` / `LoadCanceled` で回収するのは、Phase 2 の Unity Scene unload より前に instance を壊すためで、backend が破棄済みの GameObject を扱う場面を減らす（`Removed` / `CancelCleanedUp` は冪等な保険）。`SceneBase` のフックは Game 側の書き忘れに依存するので主経路にしない（§1.2）。
- **`SceneDirector` 具象ではなく `Observable<SceneEvent>` と `ISceneQuery` を受け取る。** 単体テストを `Subject<SceneEvent>` で駆動でき、`SceneDirector` から VFX への依存を作らない。
- **`Bind(go)` は VFX 専用 notifier。** 既存 `AssetReleaseOnDestroy` は具象 `AssetManagement` 専用で、多重化すると AssetManagement の変更理由が増える。`OnDestroy` は一度も active にならなかった go では呼ばれない（Unity の仕様。既存 AssetManagement と同じ制約）ので、`Tick` の偽 null 検査を保険にする。
- **毎フレームの処理（消え切り待ちの完了・timeout、偽 null 検査）は公開 `Tick` に閉じ、d1 は `UpdateSystemRuntime` に登録しない。** `SoundPlayer.Tick` の前例と同じで、登録とフレーム順序は composition（d2）が持つ。所有者の終端は通知で処理し、Tick に依存しない。エフェクト単位の例外隔離は `VfxHandle` の backend 呼び出し境界で行い、Coordinator のシステム単位の隔離に加わる d1 の境界とする。
- **`Rendering/Vfx` に置く。** `Rendering/Environments`（S-4c）と同じく描画系の App 常駐サービスで、`Abstractions` / `Implements` の分け方も揃える。新しい asmdef は作らない（依存が `OneStarMaker.Runtime` 内で閉じ、`AssetOwner` の internal 分類を読む必要がある）。
- **`VfxHandle` と `VfxSystem` を分ける。** 状態遷移規則（§2.2）と、所有者の受理・終端の規則（§2.3）は独立に変わる。`VfxOwnerIndex` は索引の表現だけで、規則を持たない。
- **ログは `ILogger<VfxSystem>` を ctor で受け取る。** 公開 API のログ抽象の常時契約に合わせる。backend と Scene 側はログを出さない（program §6.1）。

### 3.3 行数警報

- 500行: 該当なし（最大は `VfxSystem.cs` 280行、テストの `VfxHandleStateTests.cs` 330行）。
- 3責務: 該当なし（§3.1 の各行が一つの変更理由）。
- 50%以上の増加: 既存ファイルの変更が無いので該当なし。

### 3.4 harvest 先

Architecture §24（`24-rendering-system.md`、現在 366 行）に「VFX 抽象」の節を追加する（§9 RenderEnvironment の後）。内容は §2.2 の状態機械、§2.3 の所有者の終端、§3.2 の配置理由、backend 契約、d1 で答えない問いの所有先。§05（Scene）には新しい契約を足さない（`OnSceneEvent` の意味は変えない）。新しい Architecture 文書は作らない。

## 4. 実装計画

- 変更対象: §3.1 の新規ファイルと各 `.meta`（Unity Editor が生成する）。
- 順序: (1) Abstractions の型 → (2) `FakeVfxBackend` と `VfxHandle` + `VfxHandleStateTests` → (3) `VfxOwnerIndex` / `VfxSystem` / `NullVfxHandle` + `VfxSystemOwnerTests` → (4) notifier → (5) `VfxSystemSceneDirectorTests` → (6) Editor でコンパイル確認、`pwsh tools/contract-audit.ps1`。
- Phase B から Phase A へ差し戻す条件: §3.1 にない公開型・公開メンバー、asmdef 参照、`SceneDirector` / `SceneBase` / `AssetManagement` / `AssetOwner` の変更、SampleGame の変更、`UpdateSystemRuntime` への登録が必要になった場合。`TestableSceneDirector` で AC5 / AC6 の順序を観測できない場合（テスト配置の見直しが要る）。
- 対象外を維持する方法: `com.unity.visualeffectgraph` を manifest に足さない。SampleGame の asmdef・コードに触れない。AC14 の機械確認。

## 5. テストとレビュー計画

- 単体テスト: §3.1 のテスト 4 ファイル。Unity なしで検証できる範囲は `VfxHandle` の状態機械と `VfxSystem` の受理・回収規則（`Subject<SceneEvent>`・偽 `ISceneQuery`・偽 backend）。`Bind(go)` と偽 null は EditMode の GameObject を使う。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Rendering.Vfx`（C が根拠付きで変更可）。
- 判定必須テスト: 起点 filter の全件（XML で 4 ファイルのテストが収録されていること）と、最終の全 EditMode 回帰（空 filter）。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: `VfxSystemSceneDirectorTests`（EditMode、`TestableSceneDirector`）。PlayMode・Build は不要。
- 操作・実行時・目視条件の検証経路: 対象なし（全条件が EditMode テストと機械検査で閉じる）。
- 未知の操作経路: なし
- 人間の判断が必要な条件: なし
- 高コスト証拠: なし
- 判定 C の照合 script: なし（XML のテスト名・件数と contract-audit の出力で判定する）
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC14 の `git diff --stat`。
- A0/A1 主担当: Claude Code（Anthropic）
- A2 独立レビュー: 下記
- A3 統合担当: 未定（人間 + 主担当）
- C' 用に予約した担当: A2 で使わなかったベンダー（OpenAI / xAI の系列）。発注者が A0 段階で OpenAI 系列のモデルに助言を得ているため、C' は xAI 系列（`cursor-agent` の Grok）を第一候補とする。
- 独立性の強化条件を満たせない場合の理由: 未定（A2 で同一ベンダーの別モデルを使った場合は下記に記録する）

### A2 独立レビュー

（A1 入力版の commit と content sha256、各レビューの観点・担当・モデル・ベンダー・結果をここに記録する）

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
