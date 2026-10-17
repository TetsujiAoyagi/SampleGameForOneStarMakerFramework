# S-4d1 Framework の VFX 抽象

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `C`（発見 C 完了、blocker なし。判定 C は実施したが**未完了**: 全 EditMode 回帰で本スライス差分外の 41 件が failed。人間の判断待ち、§7）
- branch: `claude/s-4d1-vfx-abstraction`
- implementation base commit: `ba8245f4`（`develop`、PR #134 のマージ）
- implementation head commit: `4289fbf0`（Phase B の出口。判定 C の GO 候補 head として固定し、ローカルの軽量 tag `evidence/s-4d1-vfx-abstraction/1` を付けた。push しない）
- risk: `high`（Framework の公開 API、所有者・寿命、Scene lifecycle 通知への新しい購読者）
- owner: 発注者 / S-4d1 担当
- created: 2026-10-17
- expires: S-4d1 の Phase D 完了時
- harvest to: Architecture §24（Rendering）へ「VFX 抽象」の節を追加する。§05（Scene）は変更しない（§3.4）
- workflow task: `s-4d1-vfx-abstraction`（2026-10-17 に `workflow-task.ps1 start` 済み。Phase B で再度 start しない）
- Phase A snapshot の取得案内・状態: 確認済み。A2 入力版は commit `01f48d3c`（content sha256 は §5「A2 独立レビュー」）。**A3 凍結版は commit `1462b14c`、本書の content sha256 `2078f87a5b253c88ff5f5cbf2eda422777810c69dabe1306ebca2875dcda6fe4`**（`git show 1462b14c:docs/handoff/S-4D1_VFX_ABSTRACTION.md` の LF 内容の sha256。この値は凍結版を指し、これを書き足した後の本書は指さない）。凍結 commit は program（`S-4_FULL_SPEC_WORLD_AUTHORING.md` の §6.3・§6.4・§10）の更新も含む。凍結 commit の後の記録用 commit はこの §0 だけを変え、凍結条件を変えない。文書だけなので stage は不要
- evidence bundle の取得案内・状態: ローカル固定済み（未昇格）。判定 C の bundle 1 件（commit 固定 base `ba8245f4` / head `4289fbf0`、19 file。発見 C・判定 C の所見と DR-1 を含まない）を task `s-4d1-vfx-abstraction` に `evidence stage` で固定し、送信側で fetch と manifest の全 hash 一致を確認した。取得案内は主 checkout の git 管理外 `TestResults/s-4d1-vfx-stage/retrieval-guide.md`（本 worktree の同 path に写し）。発見 C の起点 filter の原 source は `TestResults/` に残したまま（判定 C の bundle には判定 C 自身の実行を収録）。
- C' blind bundle の取得案内・状態: **未生成（判定 C 未完了のため C' を開始しない。§7）。** 判定 C の bundle は所見を含まないので、人間が判定 C を完了と裁定した時点で同じ bundle を C' 入力に転用でき、その案内は同 directory の `cprime-retrieval-guide.md` に用意してある（C' 担当へはまだ渡していない）。
- Phase B result snapshot の取得案内・状態: ローカル固定済み（未昇格）。判定 evidence bundle に `phase-b-result.md`（B 出口の記録 commit `19dd5255` 版、所見なし）として収録。
- 引継ぎ票（判定 C 未完了 → 人間の判断へ）:
  - 前の Phase の結果: 判定 C を head `4289fbf0` で実施（§7「判定 C」）。起点 filter 82/82 passed、機械検査・AC14 合格。全 EditMode 回帰は 1,232 件中 failed 41 で、全件が本スライスの差分外（`SampleGame/DependOnAll/Editor/WorldAuthoring` の WorldCompanion 系 39、shader compile OOM 1、TestObservation の domain reload 1）。単独 filter でも WorldCompanion 系 39 件と domain reload 1 件は同じ例外で再現し、OOM の 1 件は単独では passed。判定必須の「failed 0」を満たさないので判定 C は未完了、GO 判定・C' の起動はしていない。
  - 次の役割・作業: 人間（発注者）の判断。選択肢は (a) 失敗 41 件を本スライスの差分外・環境起因と裁定し、判定 C を完了として記録する（判定必須の読み方を変えないなら決定記録 DR-2 に、全 EditMode 回帰の合否の扱いを変えるなら Phase A の新 revision に）。その場合は §0 の C' blind bundle を「ローカル固定済み」に更新し、`cprime-retrieval-guide.md` を C' 担当へ渡す。(b) 失敗の原因を別 task（S-4a/S-4c の Editor 側、または実行環境）で解消してから、同じ head `4289fbf0` で全 EditMode 回帰だけをやり直す（head が変わらなければ bundle の他の証拠は有効。[判定後の head 変更] の規則に当たらない）。
  - 判定 C 担当からの観察（判断材料、所見ではない）: 失敗 39 件の共通一次原因は `WorldCompanionOwnershipProof.HashFile` が scene の `.meta` を開く時点の `DirectoryNotFoundException` で、同じ log にその scene と folder の import 記録がある。S-4c の判定記録（commit `d550832b`、2026-09-23）に同 fixture の一時的な Failed と再実行での 908/908 の履歴があり、当時の project path は主 checkout。今回は `.claude/worktrees/` 配下の worktree で決定的に再現した（path 依存かは未確認）。
  - 停止規則: osm-workflow の Phase C。判定 C は判定必須の規則を自分で緩めない。
  - 必要な権限: 同上。(b) の再実行は sandbox 外の Unity。
  - 引き渡さないもの: 発見 C・判定 C の所見（§7）と DR-1 は C' に渡さない。判定 C で起動した Unity 3 process は全て終了（うち 2 回は既知の native 終了 stall と同症状で runner が step.json を書かず、PID の確認後に終了処理。§7）。
- （旧）引継ぎ票（発見 C → 判定 C。記録として残す）:
  - 前の Phase の結果: 発見 C 完了（§7）。blocker 0・major 0。§6 の B 適応 3 点は受理（AC4 (c) は §2.3 / I5 を正とする読みを支持。判定 C の前に人間の決定記録で読み方を確定する）。起点 filter の Unity テスト 82 件 passed、AC6 (b) の終端通知は `LoadCanceled` と観測。
  - 次の Phase・役割: 判定 C（GO 候補 head `4289fbf0` の判定必須テストの実行と判定）。差し戻しは発生していないので履歴の整理は不要。C' の起動は判定 C 完了後。
  - 開始の手順:
    1. `AGENTS.md` と `.agents/skills/osm-workflow/SKILL.md`、その「Phase A、C、C'…では全文読む」とする references を読む。
    2. `git worktree list` で branch `claude/s-4d1-vfx-abstraction` の既存 worktree を探し、そこで作業する。implementation head `4289fbf0` が HEAD の祖先で、その後の commit は本書だけを変えた記録用 commit であること（`git diff 4289fbf0 HEAD --name-only` が本書だけ）、作業ツリーが clean であること（Editor の import が `unity/Assets/MobileDependencyResolver/` の `.pdb.meta` 削除を残すことがあり、本スライスの変更ではないので `git checkout` で戻してよい）を確かめる。食い違えば始めずに止めて報告する。
    3. AC4 (c) の読み方の決定記録（§7 の B 適応 3 点目）が人間から出ているかを確認する。出ていなければ判定の前に求める。
    4. 判定必須テスト（§5）: 起点 filter の全件と、最終の全 EditMode 回帰（空 filter）を `pwsh tools/run-tests.ps1` で実行する。XML の実行テスト名と件数で、3 ファイルのテスト（発見 C では 82 件）が収録されていることを確認する。`pwsh tools/contract-audit.ps1` と AC14 の `git diff --name-status` も判定 bundle に入れる。
    5. osm-workflow の「レビュー証拠」に従い、判定 evidence bundle を `evidence stage` で固定し、head に軽量 tag `evidence/s-4d1-vfx-abstraction/1` を付け、取得案内の所在を本書に書く。C' 用 blind bundle は発見 C・判定 C の所見を含めずに同じ commit から作る。
  - 停止規則: osm-workflow の Phase C（blocker の基準、差し戻し上限）に従う。
  - 必要な権限: Unity テストは sandbox 外の承認済み経路（osm-workflow の Phase C）。`evidence stage` は Artifacts README の事前承認の範囲。
  - 引き渡さないもの: 発見 C の所見（§7）は C' に渡さない。発見 C で起動した Unity は終了済み（正常終了のログを確認）。

## 1. 目的と対象外

- 目的: VFX の寿命、非対応環境での停止、例外の隔離を、VFX 実装の型（VFX Graph の `VisualEffect` 等）に依存しない Framework の契約として実装し、偽 backend で単体テストする。
- 対象外（所有スライス）:
  - `com.unity.visualeffectgraph` の導入、version 固定、SampleGame の実 backend、起動時の実 capability 判定、Scene に配置済みの `VisualEffect` を止める仕組み、確認用 asset、App composition への組み込み（`VfxSystem` の生成と Dispose 順、`BindSceneLifecycle`、`Tick` の `UpdateSystemRuntime` 登録）: **d2**
  - `Spring_AtmosphereVFX` / `Spring_VFX_4_2` の VFX Scene と Cell 体積: **d3**
  - `Spring_Events_4_2` の trigger からの起動、Cell unload での回収、notifier の `OnDestroy` の実発火と asset 解放との前後関係（Play Mode）: **d4**
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

**d1 行（program §6.3）。** 答える問いは本書 §2。中核ロジックは偽 backend で単体テストし、動作数は偽 backend が記録する呼び出し回数で確かめる（公開 API にしない）。Phase A の必須検討は (a) Stop / Dispose / 所有者の unload / 消え切り待ちの遅延完了が競合したときの手順、(b) per-frame 更新の要否、(c) harvest 先の既存 Architecture 文書。(a) は §2.2、(b) は §3.2、(c) は §0 と §3.4。

**公開 API と依存（program §10）。** Framework 公開面の追加は「VFX の抽象（§6.4 の handle、寿命の宣言、非対応時の停止、backend の差し込み口）」に限る。Framework は season 名、Cell 座標文法、職種 role を知らない。VFX Graph 依存は SampleGame 側に閉じ、OneStarMaker Runtime の公開 API へ `VisualEffect` 型を出さない。`SceneState` の14値は変更しない。新しい LoadType も追加しない。asmdef 参照の追加は d2 で決める。

**検証方針（program §11、d1 の該当分）。** Mobile 非対応時に例外、未解放 handle、無限 retry がない（d1 は偽 backend）。Scene / asset YAML の手編集、`unity test` / `unity run` は禁止。

**スライス境界（program §12）。** 本書にない公開 API、依存、状態、所有者が必要になったら Phase B で決めず Phase A へ戻す。d1 の Phase D は、現況になった契約を Phase A で選んだ既存の Architecture 文書へ harvest し（新しい文書を起こす前に既存への統合を確認する）、本 HANDOFF を削除し、program の §6.3 d1 行を完了に更新する。

### 1.2 既存の通知・解放経路の調査（A0）

発注者経由の外部助言「新しい汎用ライフサイクル管理機構を作る前に、既存の通知・解放経路で成立するかを確認する。`AssetOwner.Scene` は主として `IAssetManagement` が管理するアセットの寿命を表すもので、VFX 側が Scene unload や `Bind(go)` の失効をどう認識するかは別途検討が要る。`SceneBase` の unload フックをまず調べる」を受けて、次を確認した。A2-3（初稿を読まない代替構成）も同じ結論に達した。

| 経路 | 現在の働き | VFX 回収への流用 |
|---|---|---|
| `AssetOwner`（`AssetManagement/Abstractions/AssetOwner.cs`） | 寿命スコープを表す値だけ。`Kind` / `Id` / `BoundObject` / `GameObjectId` は `internal`。自身は通知を持たない。`default(AssetOwner)` は `Kind=App`・`Id=null` で、`GetHashCode` は `Id` が null だと例外を投げる | **使う。** 所有者の宣言型として公開 API の引数にする。VFX 抽象を `OneStarMaker.Runtime` asmdef に置き、`internal` の分類を読む（AssetManagement への依存 edge として §3.1 に書く）。`default` は拒否する |
| `AssetOwner.Scene(id)` の解放 | 通知ではなく、`SceneDirector.DisposeUnloadedScene`（Phase 3）と `CleanupCanceledScene` が `IAssetManagement.ReleaseScene(id)` を明示的に呼ぶ | **使わない。** 呼ばれるのは Unity Scene 本体の Unload（Phase 2）の後で、VFX の instance は既に破棄済みになりうる。`SceneDirector` から VFX へ依存を足すことにもなる |
| `AssetOwner.Bind(go)` の解放 | `internal sealed AssetReleaseOnDestroy`（MonoBehaviour）を go に付け、`OnDestroy` で具象 `AssetManagement.NotifyGameObjectDestroyed(ulong)` を呼ぶ。batchmode / EditMode では `OnDestroy` 経由の通知が発火しない（`Tests/AssetManagement/AssetManagementTests.cs:236` の記録） | **同じ型を真似て別に作る。** 既存 component は具象 `AssetManagement` 専用。汎用の多重通知へ広げると AssetManagement の変更理由が増えるので、VFX 専用の小さな notifier を置く（§3.1） |
| `SceneBase` の `OnPreUnLoadedImpl` / `OnAfterUnLoadedImpl` | サブクラスごとの protected virtual フック。S-4c の `SeasonLightingScene` は `OnPreUnLoadedImpl` で RenderEnvironment lease を解放する | **主経路にしない。** 各 Game Scene が回収を書く責任を負い、program §6.4「所有者が unload されたら Framework が回収する」「Game 側に分岐を書かせない」に反する。書き忘れが漏れになる。Game がフック内で handle を明示 Stop すること自体は許す |
| `SceneDirector.OnSceneEvent`（公開 `Observable<SceneEvent>`） | 全状態遷移で `StateChanged`、Stable で `Added`、辞書除去後に `Removed`、cancel 回収後に `CancelCleanedUp`、`SceneDirector.Dispose` で `OnCompleted`。Framework の `UpdateSystemHost.BindSceneDirector` と SampleGame の `SceneDirectorTerminalEvents` が既に購読している。R3 の `Observer.OnNext` は購読者の例外を捕捉して既定ハンドラへ回すので、購読者の例外で SceneDirector が止まることは無いが、ログにも出ない | **主経路にする（§3.2）。** 新しい公開 event 型も状態値も不要。購読側で例外を捕捉し、R3 の捕捉に依存しない |
| `UpdateSystemRuntime` | `RegisterElement` で毎フレーム tick。Coordinator はシステム単位で例外を隔離する | **d1 では登録しない。** 毎フレームの処理が要るのは消え切り待ちの完了判定と破棄済み `Bind` 所有者の補助検出だけで、`SoundPlayer.Tick` と同じく `Tick` を公開して登録は composition（d2）に任せる（§3.2） |

`SceneDirector` の終端の網羅（調査結果。A2-2 R1・R6 で補正）:

- 通常 Unload: Phase 1 で `StateChanged(PreUnloading)` → `await RemoveUIView` → `ExecutePreUnLoad`（ここで lifecycle が PreUnloading に遷移）→ `StateChanged(PreUnloaded)`、Phase 2 で `Unloading` / Unity Scene unload / `Unloaded`、Phase 3 で `ExecuteAfterUnLoad` → `ReleaseScene` → `Removed`。子孫は葉が先で同じ順序（全子孫の Phase 1 が先に揃う）。`PreUnloading` 通知の時点では lifecycle はまだ遷移しておらず、`ISceneQuery.IsSceneLoaded` は true のまま。
- Load キャンセル: キャンセル窓は PreLoad 中だけ（PoNR で `LoadCts = null`、`SceneDirector.Loading.cs:203-211`）。窓内の Unload / ct cancel は `CleanupCanceledScene` に入り、`StateChanged(LoadCanceled)` → Unity Scene unload → `ReleaseScene` → `CancelCleanedUp`。`PreUnloading` は出ない。PoNR 後のロード中の Unload は `_pendingUnloads` に積まれ、Stable 後に通常 Unload になる。
- Add 失敗の回収（`RecoverFailedLoadAsync`）: active へ到達した個体は通常 Unload（`PreUnloading` から）、まだロード中の個体は `CleanupCanceledScene`（`LoadCanceled` から）に入る。途中状態からの再開（`ResumeUnloadFromCurrentState`）は、先に失敗した RemoveScene が `PreUnloading` を出した後にしか来ない。
- `OnLoadedImpl` は PoNR 後の `ExecuteLoaded`（`Loading.cs:563`）で呼ばれ、そのときの状態は WaitLoadChildScene（直後に Initializing へ遷移）。`OnPreLoadedImpl` は PreLoading 中で、`IsSceneLoaded` は true。
- 同じ identity の再ロード: `AddSceneCore` は既存個体の `IsUnloadStarted || IsLoadCanceled` の間待つので、新しい `StateChanged(PreLoading)` は必ず前の個体の `Removed` / `CancelCleanedUp` の後。
- App 終了: `SceneDirector.Dispose` は Scene を Dispose して Subject を Dispose する（購読者には `OnCompleted`）。個別の `Removed` は出ず、Unity Scene の unload も起きない。

### 1.3 前例

- S-4c の `RenderEnvironment`: App 寿命の単一 owner を lease と generation token で調停し、stale lease の操作が新しい owner を壊さない。policy は純 C#、Unity I/O は `IRenderEnvironmentSink` に分け、偽 sink で単体テストする。配置は `Scripts/Runtime/Rendering/Environments/{Abstractions,Implements}`。
- `SoundSystem`: `ISoundBackend` は差し替え口で、時間は呼び出し側の `Tick` で進む。`UpdateSystemRuntime` への登録とフレーム順序は所有者（composition）が守る。
- `UpdateSystemHost.BindSceneDirector(SceneDirector)`: Framework の App 常駐物が SceneDirector 生成後に scene event を購読する前例。二回目の呼び出しは前の購読を Dispose して差し替える。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: VFX の寿命、非対応環境での停止、例外の隔離を、VFX 実装の型に依存せず、既存の Scene lifecycle 通知と `AssetOwner` だけを使う Framework の契約として保証できるか。
- 判定定義: 進める最低条件（§2.1）を全て満たせば GO。一つでも未達なら NO-GO。スパイクではないので CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: DR-1（下記）。Phase A snapshot（A3 凍結版）には追記しない。
  - **DR-1（版 1、2026-10-17、適用時点: 判定 C の開始前。発注者が主担当とのチャットで裁定し、記録文面を承認）:** AC4 (c) の読み方。Tick による解放の最中に再入した `Spawn` について、生きている Scene への `Spawn` は受理される（§2.3 / I5 を正とし、AC4 の「再入した `Spawn` の `Create`=0」は破棄済みの `Bind` 所有者にだけ適用する）。B の実装と発見 C の判断はこの読み方。理由: `Tick` は §2.3 の所有者の終端ではなく、I5 の拒否理由の網羅列挙に「Tick 中」が無い。Tick 中に Scene の `Spawn` を拒否するには §3.1 にない状態（Tick 中の印）が要るので、それを望むなら Phase A の新 revision になる。この裁定は AC4 (a)(b) の「`Create`=0」の意味と合否を変えず、I1〜I7・S1〜S6 の合否も変えない。発見 C の指摘に対する裁定なので C' の blind bundle には渡さない（review-evidence の「C' 用決定記録」）。

### 2.1 進める最低条件

保証の範囲: d1 が保証するのは、Framework が backend に何をいつ何回要求するか（呼び出し回数・順序・状態）である。backend 自身がその要求どおりに native 資源を解放したか、実 VFX Graph の粒子判定が正しいかは保証の対象外で、backend 契約（§3.1 の `IVfxBackend`）と d2〜d4 の検証が持つ。d1 の偽 backend テストの合格を、実 backend の挙動の証明と扱わない。

**不変条件（起きてはいけないこと）**

- **I1 所有者より長く回収要求を遅らせない:** 所有者が終端に入ったら（§2.3）、その所有者で作った instance に対する `Destroy` 要求は、§2.3 の表の「回収の時点」までに1回出ている。消え切り待ちの途中でも同じ。終端の後に、その所有者の instance が Created / Playing / Stopping のまま索引に残らない。
- **I2 二重破棄・破棄後操作がない:** 1つの handle（`Create` が返した1つの instance）に対する `Destroy` 要求は高々1回。`Destroy` を要求した後、その handle に由来する backend 呼び出しは0回（backend が破棄済みの id を後で別の instance に使い回すのは契約上許され、その新しい instance への呼び出しは違反ではない）。handle の後続操作、遅れて届いた消え切り完了、二回目の所有者終端、再入した通知はどれも backend を呼ばない。`Destroy` 要求が例外を投げても再試行しない。
- **I3 backend の例外が漏れない・他を止めない:** backend の例外は、それを起こした instance だけを回収して終わる（§2.2）。他の instance の Tick・回収・パラメータ操作は続く。scene event の購読処理、`Bind` 破棄通知、`Tick`、`Dispose`、`BindSceneLifecycle`、handle と `Spawn` の全メソッドから、backend 由来の例外を呼び出し元へ投げない。scene event 処理では R3 の捕捉に依存せず、購読処理の中で捕捉する。対象は backend 由来の例外で、`ILogger` 自身の例外と、呼び出し側の引数誤り（ctor の `backend` / `logger` が null、`BindSceneLifecycle` の引数が null は `ArgumentNullException`、ctor の timeout の値域外は `ArgumentOutOfRangeException`）は対象外。
- **I4 非対応時は何もしない:** backend が非対応を返した、または判定が例外を投げた場合、判定呼び出しは `VfxSystem` の生涯で1回、それ以外の backend 呼び出しは0回、診断ログは1回、例外0件。`Spawn` は何もしない handle を返す。
- **I5 不正・終端済みの所有者と入力に作らない:** 次のどれかに当たる `Spawn` は backend の `Create` を呼ばず、何もしない handle を返し、索引に何も登録しない。`default(AssetOwner)`（`Id == null`）、終端中または未ロードの `Scene(id)`、Scene lifecycle が未接続または `OnCompleted` 後の `Scene(id)`、破棄済み（偽 null を含む）の `Bind` の go、`source == null`（偽 null を含む）、`HasParent=true` で `Parent == null`（破棄済み）、`Dispose` 後の `VfxSystem`。`Bind` の所有者で `BoundObject` が null のもの（internal の `AssetOwner.FromGameObjectId` で作られたもの）も破棄済みと同じく拒否する。
- **I6 待ちが有限:** 消え切り待ちの Stop は、backend が粒子なしを報告する、Tick 時間で timeout に達する、所有者が終端に入る、`Dispose` される、のいずれかで必ず `Destroy` 要求に至る。同じ instance への再試行ループを持たない。timeout は ctor で有限の正の値だけを受け付ける（AC3）。前の2つは `Tick` が呼ばれることが前提で、`Tick` の登録は d2 の必須受け入れ条件とする。後の2つは `Tick` に依存しない。
- **I7 境界を変えない:** Framework 公開面に VFX Graph の型が無い。asmdef 参照の追加0件。`SceneState` / `SceneEventType` / `LoadType` / `AssetOwner` / `SceneDirector` / `SceneBase` / `AssetManagement` の公開面と実装を変えない。d1 のコードは `UpdateSystemRuntime` に登録しない。SampleGame のファイルを変更しない。`VfxSystem` は asset handle を保持・Release しない。

**成立条件（起きなければならないこと）**

- **S1 即時 Stop:** `Spawn` → `Play` → `Stop` で backend の `Create` / `Play` / `Destroy` が各1回呼ばれ、handle は非アクティブになる。
- **S2 消え切り待ち Stop:** Playing の handle への `StopAllowingParticlesToFinish` は backend の `StopEmitting` を1回呼び、Tick で backend が粒子なしを返した時点で `Destroy` を1回呼ぶ。
- **S3 Scene 所有者の回収:** 実 `SceneDirector`（既存の `TestableSceneDirector`）で、所有者 Scene の Unity Scene unload（`PerformUnitySceneUnload`）より前に instance が `Destroy` される。通常 Unload では `StateChanged(PreUnloading)` の時点、Load キャンセルでは `StateChanged(LoadCanceled)` の時点。
- **S4 Bind 所有者の回収:** 破棄通知の内部入口が呼ばれた時点で同期に `Destroy` される。通知が来なかった破棄済み go（偽 null）は、その後の最初の `Tick` で `Destroy` される。notifier の `OnDestroy` が実際に内部入口を呼ぶことは d1 では示さない（EditMode で発火しないため。d4 で確認する）。
- **S5 名前付きパラメータ:** float / int / bool / Vector3 / Vector4 の値が名前と一緒に backend へそのまま渡り、backend の成否が呼び出し元へ返る。
- **S6 全回収:** `VfxSystem.Dispose` で全所有者の instance が `Destroy` され、scene event の購読が解除され、以後の `Spawn` は I5 に従う。

### 2.2 状態機械と競合の手順（program §6.3 の必須検討 (a)）

handle の状態は `Created`（生成済み・未再生）→ `Playing` → `Stopping`（消え切り待ち）→ `Released` の一方向で、`Released` は終端（吸収状態）。

**回収の規則（本文。全経路に適用する）:** `Released` への遷移は1か所（handle の `Release`）だけを通り、次の順で行う。(1) 既に `Released` なら何もしない。(2) 状態を `Released` に書く。(3) 索引から外す（回収 callback）。(4) backend `Destroy` を1回呼び、例外は捕捉して記録する。(2)(3) を backend 呼び出しより先に行うので、`Destroy` が同期で破棄通知や `Spawn` を再入させても二回目の `Destroy` は起きない。複数 handle をまとめて回収する処理（scene event、`Bind` 破棄通知、`Tick`、`Dispose`、`BindSceneLifecycle` の差し替え、`OnCompleted`）は、索引の snapshot を列挙する。

**backend 呼び出しの後の状態の書き戻し（本文。全操作に適用する）:** backend の `Play` / `StopEmitting` / `HasAliveParticles` / Set* の呼び出し中に、同じ handle への `Stop()` / `Dispose()` や所有者の終端が同期で再入しうる。backend 呼び出しの後で状態を書く操作は、戻った時点の状態が呼び出し前と同じときだけ新しい状態を書く。違っていれば（再入で Stopping や Released に進んでいれば）何も書き戻さず、それ以上 backend を呼ばず、`Play` と Set* は `false` を返す。状態は一方向にしか進まないので、再入の後に前の状態へ戻すことはない。`Released` は Destroy の前処理だけでなく、この再入の後でも吸収状態である。`StopAllowingParticlesToFinish()` は `backend.StopEmitting` を呼ぶ前に Stopping を書く（呼び出し中に同じ handle の `StopAllowingParticlesToFinish()` が再入しても、`StopEmitting` は1回だけ）。例: `Play()` の `backend.Play` が `true` を返しても、その間に `Released` になっていれば Playing に戻さない。Playing 中の再 `Play()` の `backend.Play` の中で `StopAllowingParticlesToFinish()` が再入して Stopping になったら、外側の `Play()` は Playing を書かず `false` を返す。`StopEmitting` の呼び出し中に `Dispose()` が再入して Released になったら、Stopping には戻らない（Stopping は呼び出し前に書いてあり、Released が上書きしている）。`IsActive` は Released 以外で true。

**`Dispose` の手順:** (1) 既に Dispose 済みなら何もしない。(2) Dispose 済みの印を先に書く（以後の `Spawn` は I5 で拒否し、破棄通知・`Tick`・scene event・`BindSceneLifecycle` は backend を呼ばない）。(3) scene event の購読を Dispose する。(4) 全 handle の snapshot を回収する。

**`Spawn` の手順:** (1) I5 の受理判定。(2) backend `Create`。例外なら Error ログ、`Destroy` 要求0回、何もしない handle。無効 id（0）が返ったら Error ログ、`Destroy` 要求0回、何もしない handle。既に索引にある（生存中の）id が返ったら backend 契約違反として Error ログ、`Destroy` 要求0回（生存中の別 instance を壊さないため）、何もしない handle。(3) `Create` の後で I5 の受理判定をやり直す（`Create` 中の再入で所有者が終端に入った、または `Dispose` された場合に備える）。不受理なら受け取った id に `Destroy` を1回要求し、何もしない handle。(4) 索引への登録と、`Bind` なら notifier の設置。ここで例外が出たら索引から外して `Destroy` を1回要求し、何もしない handle。(5) handle を返す（状態は Created）。

| 現在 | 操作・事象 | 結果 |
|---|---|---|
| Created / Playing | `Play()` | backend `Play`。`true` なら Playing（Playing 中は再トリガ）で `true` を返す。`false` なら状態を変えず `false` を返す |
| Stopping | `Play()` | 何もしない（`false`）。Stopping は Released にしか進まない |
| Created / Playing | `Stop()` | `Release` |
| Playing | `StopAllowingParticlesToFinish()` | Stopping を書いてから backend `StopEmitting`。待ち時間を 0 から数える |
| Created | `StopAllowingParticlesToFinish()` | 粒子が無いので `Release` |
| Stopping | `Stop()` / `Dispose()` | 待ちを打ち切って `Release` |
| Stopping | `StopAllowingParticlesToFinish()` | 何もしない（待ちを延長しない） |
| Created / Playing / Stopping | `Dispose()` | `Release`（`Stop()` と同じ） |
| Created / Playing / Stopping | 所有者の終端 | 待ちを打ち切って `Release` |
| Created / Playing / Stopping | Set* | 名前が null・空文字なら backend を呼ばず `false`（`Release` しない）。それ以外は backend に名前と値を渡し、戻り値をそのまま返す |
| Stopping | Tick | 下の Tick の手順 |
| 任意（Released 以外） | backend 例外 | 例外を投げた handle の最初の失敗だけ Error ログ → `Release`（そこでの `Destroy` 例外は Error ログに追記せず捕捉だけする） |
| Released | 全ての操作・事象 | 何もしない。`Play` と Set* は `false`、`IsActive` は `false` |

**Tick の手順（`Tick(float deltaSeconds)`）:** (1) `deltaSeconds` が負・NaN・∞なら 0 として扱う。(2) Stopping の各 handle（snapshot）について、待ち時間に加算し、`待ち時間 >= timeout` なら粒子判定を呼ばずに `Release`。そうでなければ backend `HasAliveParticles` を呼び、`false` なら `Release`。`Tick(0)` も粒子判定は行う。(3) `Bind` 所有者（snapshot）について、go が `== null` なら、その所有者の全 handle を `Release`。

合格例: Stopping 中に所有者 Scene の `PreUnloading` が来て `Destroy` 1回、その後の Tick で粒子判定は呼ばれない。timeout=1.0 で `Tick(0.5)`（粒子判定1回、`Destroy`=0）→ `Tick(0.5)`（粒子判定0回、`Destroy`=1）。不合格例: `PreUnloading` で `Destroy` した後、`Removed` の掃除で同じ id を再び `Destroy` する。`Tick(0.5)` を2回で `Destroy`=0（`>` を使った）。偽 backend の `Destroy` の中から破棄通知を再入させて列挙が例外になる。`backend.Play` の中で `Stop()` が再入した後、外側の `Play()` が Playing を書いて `IsActive=true` に戻る。

### 2.3 所有者の終端の定義

| 所有者 | 終端に入る事象（回収の時点） | 終端から外れる事象 |
|---|---|---|
| `Scene(id)` | `StateChanged` で `State` が `PreUnloading` または `LoadCanceled` を受けた時点。保険として `Removed` / `CancelCleanedUp` でも同じ回収を行う（冪等）。判定は `Type` と `State` の組で行い、`State` だけで判定しない（`Removed` は `State=AfterUnloading`、`CancelCleanedUp` は `State=LoadCanceled` を載せる） | 同じ id の `StateChanged(PreLoading)`（同じ identity の再ロード）。`Removed` / `CancelCleanedUp` では外さない |
| すべての `Scene(id)` | `OnCompleted`（SceneDirector の Dispose）。完了の印を handle の回収より先に立て、以後は `BindSceneLifecycle` し直すまで `Scene(id)` の `Spawn` を拒否する。`BindSceneLifecycle` の二回目の呼び出し（差し替え）でも、前の購読を Dispose し、既存の全 `Scene(id)` 所有 handle を回収し、終端中集合を空にする。差し替えの間（前の購読の Dispose から新しい購読の開始まで）は `Scene(id)` の `Spawn` を拒否する（回収中の再入に備える） | `BindSceneLifecycle` |
| `Bind(go)` | 破棄通知の内部入口が呼ばれた時点（同期）。通知が来なかった場合は、go が `== null` になった後の最初の `Tick` | なし（go ごとに一度） |
| `App` | `VfxSystem.Dispose` | なし |
| `Manual` | `VfxSystem.Dispose`（呼び出し側が handle を `Dispose` するのが通常） | なし |

終端の処理は、(1) 終端中集合へ id を登録する、(2) その所有者の handle を回収する、の順で行う。`PreUnloading` 通知の時点では `ISceneQuery.IsSceneLoaded(id)` がまだ true なので、回収中の `Destroy` から再入した `Spawn` を拒否できるのは (1) の集合だけである。

`Spawn` 時の `Scene(id)` 受理: Scene lifecycle 接続済み（かつ `OnCompleted` 前）、`ISceneQuery.IsSceneLoaded(id)` が true、終端中集合に id が無い、の全て。PreLoading 中の自 Scene 所有（`OnPreLoadedImpl` 内の `Spawn`）と、WaitLoadChildScene 中の自 Scene 所有（`OnLoadedImpl` 内の `Spawn`）は受理する。

**source asset の契約（呼び出し側の事前条件）:** `Spawn` に渡す `source` は、`IAssetManagement` でロードした asset を呼び出し側がそのまま渡すもので、`VfxSystem` は asset handle を保持・Release しない。呼び出し側は、その source から作った instance が Released になるまで source を有効に保つ。asset の所有者と VFX の所有者を同じ `AssetOwner` にしても、解放順は保証されない（例: 同じ `Bind(go)` では `AssetReleaseOnDestroy` と VFX の notifier の `OnDestroy` の順序が決まっていない）。安全な組み合わせは、asset の所有者が VFX の所有者より長く生きるもの（例: Events Scene の `Scene(id)` で asset をロードし、その Scene の中の go に `Bind` した VFX を起動する）。backend の `Destroy` は source が失効していても安全に完了しなければならない（backend 契約。d2 で確認）。

### 2.4 受け入れ条件

各条件の検証テストは偽 backend の呼び出し回数・順序で判定する（公開 API にしない）。ログは偽の `ILogger<VfxSystem>` で数える。

- **AC1（S1, I2）** `Spawn` → `Play` → `Stop` → `Stop` → `Dispose`: `Create`=1, `Play`=1, `Destroy`=1。2回目以降は backend 呼び出し0。あわせて §2.2 の表の Created・Playing の行を固定する: Created への `StopAllowingParticlesToFinish` は `StopEmitting`=0 で即 `Destroy`=1。backend `Play` が `false` なら状態は Created のまま（続く `StopAllowingParticlesToFinish` は `StopEmitting`=0 で `Destroy`=1）。Playing 中の再 `Play` は backend `Play` を再び呼ぶ（`Play`=2）。
- **AC2（S2, I6）** `Play` → `StopAllowingParticlesToFinish` → Tick（粒子あり）→ Tick（粒子なし）: `StopEmitting`=1、`Destroy`=1（2回目の Tick で）。以後の Tick で粒子判定0回。
- **AC3（I6）** `VfxSystem` の ctor の `gracefulStopTimeoutSeconds` は `0 < 値` かつ有限の値だけを受け付け、0・負・NaN・±∞は `ArgumentOutOfRangeException` で拒否する（`float.MaxValue` 等の大きな有限値は受理）。この ctor の引数検査の例外は I3（backend 由来の例外）の対象外。§2.2 の Tick の手順どおり。timeout=1.0 で `Tick(0.5)` ×2 の合格例・不合格例（§2.2）。負・NaN・∞の delta は待ち時間を進めない（不合格例: NaN で即 timeout、負で待ち時間が減る）。`Tick(0)` で粒子判定1回。
- **AC4（I1, I2）** §2.2 の各競合行を1テストずつ（Stopping 中の `Stop` / `Dispose` / 所有者終端 / 二重終端（`PreUnloading` → `Removed`））: 全て `Destroy`=1。再入は3本を別テストにする: 偽 backend の `Destroy` の中から (a) scene event 回収中、(b) `Dispose` 中、(c) `Tick` 中に、別の `Bind` 所有者の破棄通知と、同じ Scene 所有の `Spawn` を再入させる。列挙例外0、各 id の `Destroy`=1、再入した `Spawn` の `Create`=0。backend 呼び出し中の再入を2本足す: (d) 偽 backend の `Play` の中から同じ handle の `Stop()`、(e) `StopEmitting` の中から同じ handle の `Dispose()`。外側の呼び出しが戻った後も `IsActive=false`、`Destroy`=1、その後の backend 呼び出し0、例外0（R2-01）。(f) Playing の handle に再 `Play()` → 偽 backend の `Play` の中から同じ handle の `StopAllowingParticlesToFinish()` → 外側の `Play()` が戻る: 外側の `Play()` は `false`、状態は Stopping のまま（`IsActive` は Stopping でも true なので、`IsActive` だけで判定しない。続く `Play()` の backend `Play` 呼び出し0と、`StopEmitting`=1 で判定する）、その後の `Tick`（粒子なし）で `Destroy`=1（A3 最終候補レビュー F1）。
- **AC5（S3, I1）** `TestableSceneDirector` で所有者 Scene を Add → Spawn/Play → Unload。`UnitySceneUnloadAction` で、その Scene の `PerformUnitySceneUnload` が呼ばれた時点の偽 backend の `Destroy` 回数を捕捉する。合格: 捕捉値=1、最終 `Destroy`=1。不合格: 捕捉値=0 で最終 `Destroy`=1（`Removed` の保険で回収した）。子 Scene 所有の instance は、親 Unload の最初の `PerformUnitySceneUnload` の時点で回収済み。ここでの子は Initializing / Stable の子孫に限る（`CollectLoadedDescendants` は active な子孫だけを 3-phase に入れる。PoNR 後にまだロード中の子は `_pendingUnloads` 経由で後から Unload され、そのとき自分の終端通知で回収される。親の終端をその子へ伝播させる実装はしない）。`TestableSceneDirector` 自体は変更しない。
- **AC6（S3, I1, I5）** 到達可能な2経路:
  - (a) `OnPreLoadedImpl` 中に `Spawn(Scene(self))`（受理される）→ ct cancel（既存 `SceneDirectorCancellationTests` と同じ型）→ `StateChanged(LoadCanceled)` の時点で `Destroy`=1、その Scene の `PerformUnitySceneUnload` の時点で `Destroy`=1、`CancelCleanedUp` の後も `Destroy`=1 のまま。
  - (b) `OnLoadedImpl` 中に `Spawn(Scene(self))`（受理される）→ 同じフックが非キャンセルの例外を投げる → `RecoverFailedLoadAsync` の回収。その経路が実際に出す終端通知（`PreUnloading` か `LoadCanceled` のどちらか。テストで観測した方を記録する）の時点で `Destroy`=1、`PerformUnitySceneUnload` の時点で `Destroy`=1。架空のイベント列を期待値に固定しない。
- **AC7（I5）**
  - `default(AssetOwner)`: `Create`=0、例外0、索引の件数0。
  - 終端中（`PreUnloading` 後 `Removed` 前）の `Scene(id)`: 偽 `ISceneQuery` は `IsSceneLoaded(id)=true` を返し続ける。それでも `Create`=0。不合格例: query を false にしないと通らない実装。
  - `PreUnloading` の回収中に偽 backend の `Destroy` から再入した `Spawn(Scene(id))`: `Create`=0。
  - `Removed` 後の同じ id: まだ拒否（`Create`=0）。同じ id の `PreLoading` の後は受理（`Create`=1）。終端前の古い handle は復活しない（`IsActive=false`）。
  - 破棄済み go: go が生きている間に `AssetOwner.Bind(go)` を作り、`DestroyImmediate(go)` の後でその owner を `Spawn` に渡す（破棄済みの go では `AssetOwner.Bind` 自体が `ArgumentNullException` を投げるので、それは `Spawn` の検査ではない）。
  - 未ロード id、lifecycle 未接続、`OnCompleted` 後、`source` が null / 偽 null、`HasParent=true` で破棄済みの `Parent`、`Dispose` 後: いずれも `Create`=0 で何もしない handle（`IsActive=false`、全操作で例外0）。
- **AC8（S4, I1）** `Bind(go)`: 内部の破棄通知入口を呼んだ時点で `Destroy`=1（Tick 不要）。通知せず `DestroyImmediate(go)` した場合は、Tick 前は `Destroy`=0 でも仕様内で、次の Tick の後に `Destroy`=1。同じ go を2つの `VfxSystem` が所有した場合、どちらの破棄通知入口にも届く（notifier が system ごとに付く。§3.1）。
- **AC9（I2, I3）** 経路ごとの `Destroy` 回数を固定する:
  - `Create` が例外 / 無効 id / 生存中の id を返す: 呼び出し元への例外0、`Destroy`=0、Error ログ1件、何もしない handle。他の生存中の instance は操作できる。
  - `Create` の後の登録・notifier 設置が例外、または `Create` の中から所有者を終端させる / `Dispose` する: 受け取った id の `Destroy`=1、索引の件数0、何もしない handle。
  - `Play` / `StopEmitting` / `HasAliveParticles` / Set* が例外: 呼び出し元への例外0、その handle は `Destroy` 試行1回で Released、Error ログはその handle について1件、同じ Tick の他 handle の処理は継続。
  - `Destroy` が例外: `Destroy` 試行1回（再試行なし）、handle は Released、Error ログはその handle について1件（先に別の例外で記録済みなら追加しない）、後続の handle の回収は継続。backend 内の資源が残らないことは、この偽 backend テストでは判定しない。
- **AC10（I1, I3）** `TestableSceneDirector` で Scene S が h1・h2 を所有し、h1 の `Destroy` が例外: `UnitySceneUnloadAction` の時点で h1・h2 とも `Destroy` 試行=1、Error ログ1件、Unload は `Removed` まで完走。不合格例: h2 の `Destroy` が `Removed` の後に記録される。R3 が購読者の例外を捕捉することを合格理由にしない。
- **AC11（I4）** 非対応の偽 backend: 判定1回、それ以外0回、Warning ログ1回、`Spawn` を複数回呼んでも同じ。判定が例外: Error ログ1回、非対応と同じ扱い。
- **AC12（S5）** 各型の Set* が名前・値をそのまま渡し、backend の `false` をそのまま返す（例外とは扱わない）。Released の handle では backend を呼ばず `false`。名前が null・空文字なら backend 呼び出し0で `false`、handle は Released にならない（呼び出し側の入力誤りで instance を失わない）。
- **AC13（S6, I1）** App / Manual / Scene / Bind 所有の instance がある状態で `Dispose`: 全て `Destroy`=1。購読の解除は直接観測する: 購読の開始と、購読 disposable の Dispose を数えるテスト用の `Observable<SceneEvent>`（テストのファイルに置く R3 の `Observable<T>` の派生。公開 API は増やさない）を渡し、初回の `BindSceneLifecycle` で購読1・解除0、差し替えで旧の解除1・新の購読1、`Dispose` で新の解除1、二回目の `Dispose` で解除の回数が変わらない。あわせて、`Dispose` の後に同じ Observable からイベントを送っても backend 呼び出し0、例外0（後続イベントが無害なことだけを購読解除の証明にしない）。`Dispose` 後の `Tick` / `BindSceneLifecycle` / scene event / `Spawn` は backend を呼ばない。`OnCompleted` で Scene 所有が全回収され、以後の `Scene(id)` の `Spawn` は `Create`=0。`BindSceneLifecycle` の差し替え: 旧 Subject に送ったイベントは影響しない、既存の Scene 所有 handle は差し替え時に `Destroy`=1、新しい Subject のイベントは1回だけ処理される（購読と解除の回数は上のテスト用 Observable で数える）。
- **AC14（I7）** `pwsh tools/contract-audit.ps1` 合格。差分の範囲は比較する commit を分けて確かめる。(a) A3 凍結 commit → implementation head の `git diff --name-status` が `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/Vfx/`・`unity/Assets/OneStarMaker/Tests/Rendering/Vfx/`（配下の `.meta` を含む）、新しいフォルダ自身の `.meta`（`unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/Vfx.meta`、`unity/Assets/OneStarMaker/Tests/Rendering/Vfx.meta`）、本 HANDOFF に閉じる。(b) implementation base `ba8245f4` → implementation head では、それに加えて A3 凍結 commit で変えた `docs/handoff/S-4_FULL_SPEC_WORLD_AUTHORING.md` だけを許す（A2 入力版 commit の本 HANDOFF の追加を含む）。Phase D の harvest（Architecture §24）は implementation head より後の段階で、この検査の対象外。asmdef ファイルの変更0件。公開型のシグネチャに `UnityEngine.VFX` 名前空間の型が無い。

### 2.5 ここでは答えない問いと所有スライス

- **d2**（program §6.3 の d2 行に追記する必須受け入れ条件）:
  - `VfxSystem.Tick` を `UpdateSystemRuntime` に登録する。Layer とフレーム順序、scaled / unscaled delta の選択。
  - `VfxSystem` の生成場所（AbstractApplicationInitializer か SampleGame の AppInitializer か）、`BindSceneLifecycle` の呼び出し、`VfxSystem.Dispose` と `SceneDirector.Dispose` / `AssetManagement.ReleaseAll` の順序。backend 未構成（VFX を使わないゲーム）を Unsupported と区別すること。
  - capability 判定の結果の単一の出所: `VfxSystem` が判定を1回だけ呼び、backend は自分の判定結果を Scene 配置済み VFX の停止に共有する（再判定しない）。Unsupported で診断ログが重複しない。
  - 実 backend の契約の確認: `Create` が id を返す前に失敗したときの後始末、`Destroy` の冪等性と破棄済み GameObject・失効した source への耐性、`HasAliveParticles` の実値、同時に生存する instance に同じ id を返さない。
  - 消え切り待ち timeout の既定値（d1 は 10 秒）の製品妥当性。
  - `BindSceneLifecycle` を SceneDirector の生成直後、最初の Scene のロード前に呼ぶ配線順（R2-04）。
  - `Destroy` 要求と native 実体の破棄完了は別であること（Unity の `Object.Destroy` はフレームの終わりまで破棄を遅らせる）。`Destroy` から戻った後に native 側が source を参照し続けても安全か、安全に切断できるかを実 backend で確かめる。`Destroy` の呼び出しが成功しただけで native 資源の解放を実証したと扱わない（R2-05）。
  - `VfxSpawnRequest.Parent` が null のとき、instance をどの Unity Scene に置くか（R2-06）。
- **d4**: notifier の `OnDestroy` が実 Scene unload・go の Destroy で内部入口を呼ぶことの Play Mode 確認。source のロード / `Spawn` / 所有者の終端 / source の Release の前後関係が見える実例。
- 同時数の上限、優先度、間引き、pool / prewarm、DebugStudio 観測: 必要になったスライスの Phase A。

### 2.6 本文へ転記した実装制約（`AGENTS.md` の常時契約から）

- 依存は Game → Framework の一方向。asmdef 参照の追加は設計判断なので行わない。
- アセットは `IAssetManagement` 経由。d1 は asset をロードしない（`Spawn` は呼び出し側がロード済みの `UnityEngine.Object` を受け取る。§2.3 の source 契約）。
- `SceneState` の14値を変えない。状態変更は `SceneLifecycleManager` が所有する（d1 は読むだけ）。
- 公開 API のログ抽象は `ILogger<T>`。`ZLogger*` 型を公開面に出さない。
- 1つの `UpdateSystem` の例外で他のシステムの `Tick` を止めない（d1 は登録しないが、`Tick` 自体が backend 例外を投げない I3 で整合する）。
- Editor コードを Runtime アセンブリに置かない。
- `record` を使わない。新規・編集する `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object`（`BoundObject`、`parent`、`source`）は `== null` / `!= null` で判定し、`?.` / `??` / `is null` / `ReferenceEquals` を使わない。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。時間は `Tick(delta)` で注入する。
- 参照 0 を削除理由にしない。

### 2.7 未決事項

なし（A2 で挙げた U1〜U3 は §5「A3 統合」で裁定した）。

## 3. 責務マップ

全て新規ファイル。既存ファイルの変更は無い（harvest の Architecture §24 は Phase D）。

### 3.1 ファイルと責務

配置: `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/Vfx/{Abstractions,Implements,Components}/`、namespace `OneStarMaker.Runtime.Rendering.Vfx`、asmdef `OneStarMaker.Runtime`（既存）。

依存 edge: `VfxOwnerIndex` と `VfxSystem` は `AssetOwner` の internal メンバー（`Kind` / `Id` / `GameObjectId` / `BoundObject`）を読む。これは AssetManagement への依存で、`AssetOwnerKind` に値が増えたら VFX の受理規則も見直す。Kind の switch は既知の4値を列挙し、それ以外は拒否（I5 と同じ扱い）にする（黙って App 扱いにしない）。

公開型は6つ。各型を名指しする利用者:

| 型 | 利用者と public にする理由 |
|---|---|
| `IVfxSystem` | d4 / d5 の Game Scene が `Spawn` だけを見る窓口。Game 側のテストで差し替える |
| `VfxSystem` | d2 の composition が生成・`BindSceneLifecycle`・`Tick` 登録・`Dispose` する |
| `IVfxHandle` | Game Scene が保持して操作する |
| `VfxSpawnRequest` | Game Scene が `Spawn` に渡す |
| `IVfxBackend` | d2 の SampleGame backend が実装する |
| `VfxInstanceId` | `IVfxBackend` のシグネチャに現れる（`SoundVoiceId` と同じ前例） |

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 依存 | 公開面 | 予想行数 |
|---|---|---|---|---|---|---|
| `Abstractions/IVfxSystem.cs` | Game が VFX を起動する窓口 | 起動 API の形 | — | `AssetOwner`、`VfxSpawnRequest` | public interface。`IVfxHandle Spawn(in VfxSpawnRequest request, AssetOwner owner)` | 35 |
| `Abstractions/IVfxHandle.cs` | 1 instance への型に依存しない操作 | handle 操作の形 | 呼び出し側が保持、寿命は §2.3 | — | public interface（`IDisposable`）。`Dispose` は `Stop` と同じ即時の回収で、消え切り待ちの途中でも待ちを打ち切る（`using` で囲むと消え切り待ちが打ち切られる）ことを XML doc に書く。`bool IsActive`、`bool Play()`、`void Stop()`、`void StopAllowingParticlesToFinish()`、`bool SetFloat/SetInt/SetBool/SetVector3/SetVector4(string name, …)` | 55 |
| `Abstractions/VfxSpawnRequest.cs` | 起動要求の値 | 起動時の入力項目 | 値 | UnityEngine | public readonly struct。`UnityEngine.Object Source`、`bool HasParent` と `Transform? Parent`。親なしの ctor は `HasParent=false`（どの Unity Scene に置くかは backend の責務（d2））、親ありの ctor は `HasParent=true`。`HasParent=true` で `Parent == null`（破棄済みの偽 null を含む）なら破棄済みとして拒否する。偽 null と「未指定」を `ReferenceEquals` で区別しないための形。`default(VfxSpawnRequest)` は `Source` が null なので I5 で拒否される、`Vector3 Position` / `Quaternion Rotation`（world 座標）。source の有効期間の契約（§2.3）を XML doc に書く | 50 |
| `Abstractions/IVfxBackend.cs` | VFX 実装（d2 の SampleGame）への差し込み口 | backend 契約 | backend は `VfxSystem` が借用（所有・Dispose しない） | `VfxInstanceId` | public interface。`bool QueryCapability(out string reason)`、`VfxInstanceId Create(in VfxSpawnRequest)`、`bool Play(VfxInstanceId)`、`void StopEmitting(VfxInstanceId)`、`bool HasAliveParticles(VfxInstanceId)`、`bool SetFloat/SetInt/SetBool/SetVector3/SetVector4(VfxInstanceId, string, …)`、`void Destroy(VfxInstanceId)`。`QueryCapability` の `reason` は `out string?`。契約（XML doc）: main thread のみ。`Create` は instance を作るだけで発射しない（発射は `Play` だけ。Created から即 `Release` する規則の前提）。`Create` が id を返す前に失敗したら backend が後始末する。同時に生存する instance に同じ id を返さない。`Destroy` は破棄済み GameObject・失効した source に対しても安全で、失敗した場合の回復は backend の責任。backend はログを出さない | 90 |
| `Abstractions/VfxInstanceId.cs` | backend が返す instance の識別値 | 識別子の表現 | 値 | — | public readonly struct（0 は無効） | 45 |
| `Implements/VfxSystem.cs` | scene event・破棄通知・Tick を受けて規則を適用する orchestration | 受理規則・終端イベントの解釈・ログ方針 | App 寿命（composition が生成・Dispose）。handle と `VfxOwnerIndex` を所有 | `IVfxBackend`、`ILogger<VfxSystem>`、`Observable<SceneEvent>`、`ISceneQuery`、`VfxOwnerIndex`、`VfxHandle` | public sealed class（`IVfxSystem`, `IDisposable`）。ctor(`IVfxBackend backend`, `ILogger<VfxSystem> logger`, `float gracefulStopTimeoutSeconds = 10f`)。timeout は有限の正の値以外を `ArgumentOutOfRangeException` で拒否する。全メソッドは Unity の main thread からだけ呼ぶ（UniTask の継続を含む。スレッド安全にはしない）。Scene identity の比較（`SceneEvent.SceneIdentify` と `AssetOwner` の Id）は `StringComparison.Ordinal`。ctor で `QueryCapability` を1回呼ぶ。`BindSceneLifecycle(Observable<SceneEvent>, ISceneQuery)`（二段階にする理由: SceneDirector は App 常駐サービスより後に生成される。`UpdateSystemHost.BindSceneDirector` と同じ。使用の前提: SceneDirector の生成直後、最初の Scene をロードする前に呼ぶ。途中で接続すると、接続前に出た `PreUnloading` を知らないまま `IsSceneLoaded` が true の Scene を受理しうる。途中接続の状態同期は約束しない）、`Tick(float deltaSeconds)`、`Dispose()`。内部入口 `NotifyOwnerGameObjectDestroyed(ulong)` | 300 |
| `Implements/VfxHandle.cs` | §2.2 の状態機械と、backend 呼び出しの例外境界 | 状態遷移規則 | `VfxSystem` が生成し、Released まで索引が保持 | `IVfxBackend`、`VfxInstanceId`。ctor で回収 callback（`Action<VfxHandle>`、索引からの除去）と失敗記録 callback（`Action<VfxHandle, Exception>`、ログ）を受け取る。`VfxSystem` の型を参照しない | internal sealed class（`IVfxHandle`）。`Release()`、`AdvanceStopping(float delta, float timeout)` | 190 |
| `Implements/NullVfxHandle.cs` | 何もしない handle | — | 共有 singleton | — | internal sealed class | 35 |
| `Implements/VfxOwnerIndex.cs` | 所有者 → handle の索引と、Scene 所有者の終端状態（終端中集合、lifecycle 接続・完了の有無） | 索引と終端状態の表現 | `VfxSystem` が所有 | `AssetOwner`（internal） | internal sealed class。「終端に入れる・外す」「所有者の handle の snapshot」「全件の snapshot」「Bind 所有者の snapshot」。SceneEvent を知らない（イベントの意味は `VfxSystem` が決め、Index は適用だけ行う） | 140 |
| `Components/VfxOwnerDestroyNotifier.cs` | `Bind(go)` の go 破棄を1つの `VfxSystem` へ知らせる | Unity の破棄通知 | go に付く（go と同寿命） | `Action<ulong>`（`VfxSystem` の型を参照しない） | internal sealed MonoBehaviour。`Initialize(Action<ulong> onDestroyed, ulong gameObjectId, object systemKey)`。id は `Initialize` 時に保持し、`OnDestroy` 内で `gameObject` を参照しない。system ごとに1つ付ける（同じ go に既に同じ `systemKey` の notifier があれば付けない。別の system なら別に付ける）。`VfxSystem.Dispose` の後に届いた通知は何もしない。go が残る間は callback が `VfxSystem` を参照し続ける（既知。Phase C の観察項目） | 35 |

テスト（`unity/Assets/OneStarMaker/Tests/Rendering/Vfx/`、asmdef `OneStarMaker.Tests` 既存。InternalsVisibleTo は既存の `AssemblyInfo.cs`）:

| ファイル | 対象 | 予想行数 |
|---|---|---|
| `FakeVfxBackend.cs` | 呼び出し回数・順序・例外注入・粒子あり/なし・返す id・`Destroy` 内の再入 callback を記録・制御する偽 backend | 200 |
| `FakeVfxLogger.cs` | レベル別の件数を数える偽 `ILogger<VfxSystem>` | 50 |
| `CountingSceneEventObservable.cs` | 購読の開始と購読 disposable の Dispose を数え、イベントを流せるテスト用 `Observable<SceneEvent>`（AC13） | 50 |
| `VfxHandleStateTests.cs` | `VfxHandle` 単体（`VfxSystem` なし）。AC1〜AC3（ctor の timeout の値域は `VfxSystemOwnerTests`）、AC4 の競合行と (d)(e)、AC9 の handle 側、AC12 | 360 |
| `VfxSystemOwnerTests.cs` | `Subject<SceneEvent>` と偽 `ISceneQuery` で駆動。AC3 の ctor の timeout の値域、AC4 の再入 (a)(b)(c)、AC7、AC8、AC9 の Spawn 側、AC11、AC13 | 420 |
| `VfxSystemSceneDirectorTests.cs` | 既存 `TestableSceneDirector` / `SceneDirectorTestBase` を使う統合。AC5、AC6、AC10 | 220 |

### 3.2 配置と依存を選んだ理由

- **Scene 所有者は `SceneDirector.OnSceneEvent` を購読する。** 既存の公開通知で、Framework 内の前例（`UpdateSystemHost`）がある。`Removed` を待たず `PreUnloading` / `LoadCanceled` で回収するのは、Phase 2 の Unity Scene unload より前、かつ Phase 3 の `ReleaseScene`（asset 解放）より前に instance を壊すためで、backend が破棄済みの GameObject を扱う場面を減らす（`Removed` / `CancelCleanedUp` は冪等な保険）。`SceneBase` のフックは Game 側の書き忘れに依存するので主経路にしない（§1.2）。
- **`SceneDirector` 具象ではなく `Observable<SceneEvent>` と `ISceneQuery` を受け取る。** 単体テストを `Subject<SceneEvent>` で駆動でき、`SceneDirector` から VFX への依存を作らない。
- **`Bind(go)` は VFX 専用 notifier と Tick の保険の二経路。** notifier は通知が届けば同期に回収でき、Tick の偽 null 検査は一度も active にならなかった go（`OnDestroy` が呼ばれない。既存 AssetManagement と同じ制約）を拾う。既存 `AssetReleaseOnDestroy` は具象 `AssetManagement` 専用で、多重化すると AssetManagement の変更理由が増える。
- **毎フレームの処理（消え切り待ちの完了・timeout、偽 null 検査）は公開 `Tick` に閉じ、d1 は `UpdateSystemRuntime` に登録しない。** `SoundPlayer.Tick` の前例と同じで、登録とフレーム順序は composition（d2）が持つ。所有者の終端は通知で処理し、Tick に依存しない。エフェクト単位の例外隔離は `VfxHandle` の backend 呼び出し境界で行い、Coordinator のシステム単位の隔離に加わる d1 の境界とする。
- **`Rendering/Vfx` に置く。** `Rendering/Environments`（S-4c）と同じく描画系の App 常駐サービスで、`Abstractions` / `Implements` の分け方も揃える。新しい asmdef は作らない（依存が `OneStarMaker.Runtime` 内で閉じ、`AssetOwner` の internal 分類を読む必要がある）。
- **`VfxHandle`・`VfxOwnerIndex`・`VfxSystem` を分ける。** 状態遷移規則（§2.2）、索引と終端状態の表現、イベントの解釈と受理規則（§2.3）は独立に変わる。`VfxHandle` は callback だけを受け取るので、`VfxSystem` なしで状態機械をテストできる。
- **handle は class。** pool / 再利用が無い段階では generation slot は過剰で、偽 backend の呼び出し回数での検証が単純になる（A2-3 X1 を不採用。§5）。
- **ログは `ILogger<VfxSystem>` を ctor で受け取る。** 公開 API のログ抽象の常時契約に合わせる。backend と Scene 側はログを出さない（program §6.1）。ログの回数: Unsupported の Warning は `VfxSystem` の生涯で1回、判定例外の Error は生涯で1回、handle の最初の backend 失敗の Error は handle ごとに1回、`Spawn` の `Create` 失敗の Error は失敗ごとに1回。I5 の拒否はログを出さない。

### 3.3 行数警報

- 500行: 該当なし（最大は `VfxSystem.cs` 300行、テストの `VfxSystemOwnerTests.cs` 420行）。
- 3責務: 該当なし（§3.1 の各行が一つの変更理由）。
- 50%以上の増加: 既存ファイルの変更が無いので該当なし。

### 3.4 harvest 先

Architecture §24（`24-rendering-system.md`、現在 366 行）に「VFX 抽象」の節を追加する（§9 RenderEnvironment の後）。内容は §2.2 の状態機械と回収の規則、§2.3 の所有者の終端と source 契約、§3.2 の配置理由、backend 契約、d1 で答えない問いの所有先。§05（Scene）には新しい契約を足さない（`OnSceneEvent` の意味は変えない）。新しい Architecture 文書は作らない。

## 4. 実装計画

- 変更対象: §3.1 の新規ファイルと各 `.meta`（Unity Editor が生成する）。
- 順序: (1) Abstractions の型 → (2) `FakeVfxBackend` / `FakeVfxLogger` と `VfxHandle` + `VfxHandleStateTests` → (3) `VfxOwnerIndex` / `VfxSystem` / `NullVfxHandle` + `VfxSystemOwnerTests` → (4) notifier → (5) `VfxSystemSceneDirectorTests` → (6) Editor でコンパイル確認、`pwsh tools/contract-audit.ps1`。
- Phase B から Phase A へ差し戻す条件: §3.1 にない公開型・公開メンバー、asmdef 参照、`SceneDirector` / `SceneBase` / `AssetManagement` / `AssetOwner` / `TestableSceneDirector` の変更、SampleGame の変更、`UpdateSystemRuntime` への登録が必要になった場合。AC5 / AC6 / AC10 の順序を `UnitySceneUnloadAction` で観測できない場合。AC6 (b) の経路が `PreUnloading` / `LoadCanceled` のどちらも出さないと分かった場合。
- 対象外を維持する方法: `com.unity.visualeffectgraph` を manifest に足さない。SampleGame の asmdef・コードに触れない。AC14 の機械確認。

## 5. テストとレビュー計画

- 単体テスト: §3.1 のテスト 5 ファイル。Unity なしで検証できる範囲は `VfxHandle` の状態機械と `VfxSystem` の受理・回収規則（`Subject<SceneEvent>`・偽 `ISceneQuery`・偽 backend・偽 logger）。`Bind(go)` と偽 null は EditMode の GameObject と `DestroyImmediate` を使う。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Rendering.Vfx`（C が根拠付きで変更可）。
- 判定必須テスト: 起点 filter の全件（XML でテスト 3 ファイルのテストが収録されていること）と、最終の全 EditMode 回帰（空 filter）。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: `VfxSystemSceneDirectorTests`（EditMode、`TestableSceneDirector`）。PlayMode・Build は不要。
- 操作・実行時・目視条件の検証経路: 対象なし（全条件が EditMode テストと機械検査で閉じる。notifier の実発火は d4 へ送った）。
- 未知の操作経路: なし
- 人間の判断が必要な条件: なし
- 高コスト証拠: なし
- 判定 C の照合 script: なし（XML のテスト名・件数と contract-audit の出力で判定する）
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC14 の `git diff --stat` / `--name-status`。
- A0/A1 主担当: Claude Code（Anthropic）
- A2 独立レビュー: 下記
- A3 統合担当: 人間（発注者）と主担当。採否案と本文改訂は主担当（Claude Code、Anthropic）が作り、発注者が 2026-10-17 に凍結を承認した。
- C' 用に予約した担当: 最低条件（Phase B・Phase C の両方と異なるモデル、新しい session、blind audit bundle）を満たす担当を、Phase B・C のモデルが決まった時点で選ぶ。第一候補は xAI 系列（`cursor-agent` の Grok）で、Phase A では凍結前の事前レビュー1件（A3 改訂版への xAI レビュー）にしか関与していない。
- 独立性の強化条件を満たせない場合の理由: 強化条件（Phase A・B・C に関与していない系列）は満たせない。利用できる3系列が全て Phase A に関与した（Anthropic は A0〜A3、OpenAI は A0 の助言と A2-4・再レビュー、xAI は凍結前の事前レビュー）。C' は `独立性制約あり` として、関与の最も少ない xAI 系列を推奨する。残存リスクは、xAI が A3 改訂版の本文と採否表を読んだことによるアンカリングである。

### A2 独立レビュー

- 入力版: commit `01f48d3c`（branch の最初の commit）、`docs/handoff/S-4D1_VFX_ABSTRACTION.md` の content sha256 `11c1ede6700d5809a2009a4dc96f81c4c9c3b7e96e8442aaee4c870c185a99cf`（`git show 01f48d3c:<path>` の LF 内容）。
- 3 件を同じ入力版で並行して実施した。互いの指摘は見せていない。全て主担当の session が起動した別 context の subagent（Anthropic の別モデル）で、ファイルは変更していない。
  - A2-1 アーキテクチャゲート（architecture-gates.md の Phase A 7 項目と §1.2 の事実確認）: Claude Sonnet 系列。
  - A2-2 不変条件の反証・競合・失敗経路（I1〜I7 を SceneDirector の全終端経路に当てる）: Claude Fable 系列。
  - A2-3 初稿を読まず A0（program §2.1〜§2.4・§6.1・§6.3・§6.4・§10〜§12 と既存コード、発注者経由の外部助言）だけから代替構成を出す: Claude Opus 系列。
- 独立性制約あり: A2-1〜A2-3 は主担当と同じベンダー（Anthropic）。その後、発注者が OpenAI 系列の追加レビュー（A2-4）を依頼した。xAI 系列は C' 用に残した（§5 の C' 予約）。

#### A2-1 アーキテクチャゲート（blocker 0、major 4、minor 4）

| ID | 重要度 | 内容（要約） |
|---|---|---|
| G1 | major | `VfxSystem` に受理規則・終端検知3系統・Tick/Dispose が同居する。`VfxOwnerIndex` は「規則を持たない」とあるのに終端中集合（PreLoading で外す遷移規則）を持ち、規則が2か所に分かれる。Scene 所有者の終端追跡を1つの internal 純 C# 型に集めるか、Index を「索引＋終端状態」の所有者と書き直す |
| G2 | major | `VfxHandle` → `VfxSystem` の型参照で循環し、状態機械のテストが `VfxSystem` なしで書けない。handle には backend・id・timeout・回収 callback・ログ sink だけを ctor で渡す |
| G3 | major | 公開型 8 個は多い。`VfxBackendCapability` と `VfxSystemOptions`（項目1つ）は引数化できる可能性。`IVfxSystem` と `VfxSystem` の両方を public にする理由が無い（`SoundPlayer` は sealed 具象のみ）。型ごとに「Game / SampleGame が名指しする理由」を書き、書けないものは internal へ |
| G4 | major | `AssetOwner` の internal 分類を読むことは AssetManagement への暗黙の依存 edge なので §3.1 に明記する。`default(AssetOwner)` は `Kind=App`・`Id=null` になり黙って App 所有で受理される。I5 に含める。Kind の switch に default 分岐を置かない |
| G5 | minor | §1.2 の事実は一致（`SceneDirector.Unloading.cs:234/294/389/401`、`SceneDirector.cs:97`、`UpdateSystemHost.cs:73`）。注意: `PreUnloading` 通知の時点では `IsSceneLoaded` が true のままなので、終端中集合が必須である理由を §2.3 に書く。Add 失敗の回収で `Removed` だけになる経路では回収が Unity Scene unload の後になり、S3 の時点はその経路に当てはまらない。U3 の制約（他 Scene に出した `Scene(id)` 所有 VFX が graceful にできない）を明記 |
| G6 | minor | `BindSceneLifecycle` の二段階初期化の理由と、二重呼び出しの扱い（前の購読を Dispose して差し替える `UpdateSystemHost` 型）を決める |
| G7 | minor | Tick が呼ばれない構成では I6 が成立しない。I6 の保証範囲を限定するか、Dispose・所有者終端で必ず終わることを残す。「`Tick` の `UpdateSystemRuntime` 登録」を program §6.3 の d2 行に書く |
| G8 | minor | notifier → 具象 `VfxSystem` の参照は `Action<ulong>` 程度の窓口に絞れる。同じ go に notifier を重複して付けない（`AssetManagement.cs:353` の `GetComponent` 先行確認と同じ） |

構造上破れやすい順序: (I2) Stopping 中に `PreUnloading` の回収 → その `Destroy` が誘発した Bind 破棄通知が列挙中に再入 → `Removed` の保険が同じ handle を再び `Destroy`。状態の書き込みと索引からの除去が `Release` 1か所に閉じていないと破れる。(I5) 終端中集合への登録を回収より後に書くと、回収中のコールバックからの Spawn が受理される。

#### A2-2 不変条件の反証（blocker 0、major 4、minor 6）

| ID | 重要度 | 内容（要約） |
|---|---|---|
| R1 | major | **AC6 は現コードでは到達不能。** キャンセル窓は PreLoad 中だけで、PoNR で `LoadCts = null`（`SceneDirector.Loading.cs:203-211`）。`OnLoadedImpl` は PoNR 後の `ExecuteLoaded`（`:563`）で、そのときの状態は Initializing ではなく WaitLoadChildScene（`:566` で Initializing へ遷移）。PoNR 後の Unload は `_pendingUnloads` 経由で Stable 後の PreUnloading 経路になり、`LoadCanceled` は出ない。AC6 を (a) `OnPreLoadedImpl` 中の Spawn → キャンセル → `LoadCanceled` で `Destroy`=1、(b) `OnLoadedImpl` 中の Spawn → 同フックの非キャンセル例外 → `RecoverFailedLoadAsync` → `LoadCanceled` で `Destroy`=1、の2本に書き直す。§2.3 の「Initializing」を訂正し、PreLoad 中の Spawn を受理するかを決める（主担当が `Loading.cs` の該当行を確認した） |
| R2 | major | I3 / AC10: R3 1.3.1 の `Observer.OnNext` は購読者の例外を捕捉して既定ハンドラへ回すので、例外を外へ投げても SceneDirector の Unload は完走する。AC10 は「Unload 完了」しか見ないので I3 違反を検出できない。破る順序: S 所有の h1・h2 → PreUnloading の処理で h1 の `Destroy` 例外が外へ漏れる → h2 が `Removed` まで残る。AC10 に「2つ目の `Destroy` が `PerformUnitySceneUnload` より前、Error ログ1件」を足す。R3 が握ることに依存しない |
| R3 | major | 再入は Tick 以外でも起きる（EditMode の `DestroyImmediate` は同期）。PreUnloading の列挙中 `Destroy` → 子の notifier の `OnDestroy` → 索引変更 → 列挙例外。「列挙は snapshot、`Release` は Released の書き込みと索引除去を backend 呼び出しより先に行う」を §2.2 の規則本文に昇格し、AC4 に scene event 中・Dispose 中の再入を足す |
| R4 | major | リポジトリ自身が「batchmode/EditMode では `OnDestroy` 経由の破棄通知が発火しない」と記録している（`Tests/AssetManagement/AssetManagementTests.cs:236`）。d1 で示せるのは内部入口と Tick の偽 null 検査だけで、S4 を限定し notifier の実機確認を d4 の AC として名指しで送る。U2 を「Tick 登録は I1（Bind の保険）と I6（App/Manual の待ち）の成立前提」として d2 の必須 AC にする。notifier の id は `Initialize` で保持する。複数の `VfxSystem` が同じ go を所有すると `GetComponent==null` 型では2つ目が通知を受けない |
| R5 | minor | `PreUnloading` 通知は `TransitionTo` と `await RemoveUIView` より前（`Unloading.cs:234-241`）。AC7 の合格例を「偽 `ISceneQuery` は PreUnloading 後も true のまま、それでも `Create`=0」に固定する。`Removed` の `State` は AfterUnloading、`CancelCleanedUp` は LoadCanceled なので、`State` だけで判定しない |
| R6 | minor | 終端の網羅は確認済み。同じ identity の再ロードの `PreLoading` は必ず終端通知の後（`Loading.cs:141-146`）。`Subject.Dispose()` は `OnCompleted` を出す。Unload が途中で失敗して PreUnloading のまま残った個体は終端中集合に残り続けるが、既存の AddScene の永久待ちと整合（記録のみ） |
| R7 | minor | `TestableSceneDirector` に順序の記録は無い。`UnitySceneUnloadAction` で `PerformUnitySceneUnload` 時点の偽 backend `Destroy` 回数を捕捉する合格例・不合格例を AC5 に書く（テスト用型の変更は不要） |
| R8 | minor | timeout の比較（`>=` / `>`）、同じ Tick で粒子なしと timeout が同時に成立したときの順序、`Tick(0)`、有限の大きな値を固定する。timeout 到達時は粒子判定を呼ばずに Release する案 |
| R9 | minor | backend `Play` が失敗したときの状態（Created のままか Playing か）と、その後の `StopAllowingParticlesToFinish` の扱いを決める |
| R10 | minor | AC13 の「購読解除」は backend の回数では観測できない。`HasObservers` か、Dispose 後の `PreLoading` + Spawn → `Create`=0 で代替する。`BindSceneLifecycle` の二重呼び出しは前の購読を Dispose する |

#### A2-3 A0 だけからの代替構成（初稿を読んでいない）

既存経路の調査結論は §1.2 と一致した（SceneBase フック・`ReleaseScene`・`AssetReleaseOnDestroy` は Framework の保証に使えず、`OnSceneEvent` が主経路。新しい汎用ライフサイクル機構は不要）。初稿と異なる点:

| ID | 内容 | 初稿との差 |
|---|---|---|
| X1 | handle を `readonly struct`（system 参照・slot・generation）にし、`default` を何もしない handle にする。generation 不一致は no-op（RenderEnvironment lease と同じ型）。class / `IDisposable` は `using` で消え切り待ちを意図せず打ち切りやすく、毎回割り当てが起きる | 初稿は class の `IVfxHandle : IDisposable` |
| X2 | backend は instance object（`IVfxBackendInstance`: Play / StopEmitting / `IsDrained` / Destroy / Set*）を返す | 初稿は `VfxInstanceId` を backend に渡す形 |
| X3 | `IUpdateElement` の薄い adapter（`VfxSystemUpdateElement`）を d1 に置き、`OnElementLateUpdate` で Tick する。試験は `Tick` 側。StopAndDrain を呼んだそのフレームは粒子判定しない。時間は scaled `DeltaTime` | 初稿は d1 で登録せず d2 に送る（U2） |
| X4 | Bootstrap に `protected virtual IVfxBackend? CreateVfxBackend() => null` を置き、SceneDirector 生成直後に作り、ReleaseAll で SceneDirector.Dispose より前に Dispose する。null は「未構成」で、Unsupported と分けてログを出さない（VFX を使わないゲームで毎回診断ログが出るのを防ぐ） | 初稿は composition を d2 に送る |
| X5 | `Bind(go)` は notifier を作らず、Spawn 時の偽 null 拒否と毎 Tick の偽 null 検査だけで検知する。`spawn.parent` 未指定時の既定 parent を go にして、instance の破棄を go の破棄に揃える | 初稿は notifier ＋ Tick の保険 |
| X6 | 最大リスク: `Bind(go)` の asset は `AssetReleaseOnDestroy` で即時に解放されるが、VFX の検知は次フレームなので、instance が go の子でなければ最大1フレーム解放済み asset を参照する。別 parent を指定した Bind を禁止するか、共通の破棄通知（AssetManagement の変更）を検討するかを A3 で決める | 初稿に記述なし |
| X7 | 回収点は Unity Scene unload の前（Phase 1 の入口）。`ReleaseScene`（Phase 3）による asset 解放より先に instance が消える。`OnCompleted` で system を終端状態にし、以後の Spawn は何もしない | 初稿と一致 |
| X8 | harvest は §24 に節を足し、§05 に「OnSceneEvent 購読者の回収時点（PreUnloading の入口）」を1行足す | 初稿は §05 を変えない |
| X9 | A3 で決める点: 消え切り待ち上限の既定値、Manual / App 所有を d1 で許すか、終端済み所有者への Spawn で warning を出すか | — |

不変条件案は初稿と同じ範囲で、追加は「VfxSystem が asset handle を Release しない」（初稿 §2.6 の制約に相当し、不変条件としては未記載）。

#### A2-4 発注者が依頼した追加レビュー（OpenAI 系列、独立性制約あり）

- 担当: 発注者が OpenAI 系列のモデル（GPT）に依頼し、2026-10-17 にレビュー文書を主担当へ渡した。原文は発注者の手元層にあり、tracked にしない。
- 入力: 発注者が渡した A1 ＋ A2-1〜A2-3 の記録版と、`develop` `ba8245f4` のコード・program。A2-1〜A2-3 の指摘を読んだうえでのレビューなので、A2-1〜A2-3 から独立していない。入力版の content sha256 は照合していない（A2-1〜A2-3 の記録を追記した未 commit の版。commit は残していない）。
- 判定: 「A3 凍結は現状 NO-GO、指摘反映後に GO 候補」。基本方針（Framework は VFX Graph 非依存、`OnSceneEvent` を利用、新しい汎用ライフサイクル機構なし、d1〜d5 の分割）は維持。
- 新規の指摘（A2-1〜A2-3 に無いもの。主担当が本文・コードで事実を確認した印を付ける）:

| ID | 重要度 | 内容（要約） |
|---|---|---|
| F-01 | A3 blocker | AC9 は backend の `Create` 例外でも「`Destroy` 試行1回」を求めるが、例外では id が返らないので破棄対象が無い（主担当確認: §2.4 AC9 の列挙に `Create` が入っている）。`Create` 成功前の例外は backend が後始末し Framework の `Destroy`=0、id を受け取った後の失敗は `Destroy` 試行1回、無効 id（0）の返却の意味、`Create` 中の再入（owner 終端・Dispose）後の再検証を分けて固定する |
| F-02 | A3 blocker | backend の `Destroy` 自身が失敗すると、二重 `Destroy` 禁止で再試行しないため backend に native instance が残りうる。I1「backend に残らない」は無条件には保証できない。I1 を「Framework が回収を要求し終える」保証と、backend の破棄失敗時の隔離に分け、破棄失敗後の回復責任を backend 契約に書く |
| F-03 | A3 blocker | source asset と instance の寿命の依存契約が無い。`Bind(go)` で読んだ source を同じ `Bind(go)` の VFX に渡すと、`AssetReleaseOnDestroy` と VFX の notifier の `OnDestroy` の順序は保証されない。U1 は `UnityEngine.Object source` を採用し、「source は利用する instance が Released になるまで呼び出し側が有効に保つ」事前条件を公開契約に書く。同じ owner なら安全、という推論を禁止する。実 backend での確認は d2〜d4 |
| F-05 | A3 blocker | `default(AssetOwner)` は `Id=null` なので、辞書キーにすると `AssetOwner.GetHashCode` の `StringComparer.Ordinal.GetHashCode(Id)` が例外を投げる（主担当確認: `AssetOwner.cs` の `GetHashCode`）。索引に入れる前に拒否する。G4 / G5 / R5 と合わせ、終端集合への登録を backend 呼び出しより先に行う |
| F-11 | major | `BindSceneLifecycle` の再接続時に、既存の Scene 所有 handle を維持するか一括回収するか。安全側は回収。旧 Subject のイベントが影響しないことを試験する |
| F-12 | major（d2 接続） | capability 判定1回と、Scene 配置済み VFX の停止（d2）を両立させるため、判定結果の単一の出所（system か backend か）を A3 で決め、配置済み VFX の停止は d2 の AC に送る。「backend 未構成」と Unsupported を混同しない（X4 と同旨） |
| F-13 | major | `source == null`（偽 null を含む）は `Create` を呼ばず何もしない handle（推奨）。破棄済み parent の扱い。backend は同時に生存する別 instance に同じ id を返さない。位置・回転の座標系（world / local）を `VfxSpawnRequest` に書く |
| F-14 | minor | ログ回数（Unsupported の Warning 1回、判定例外の Error 1回、instance の最初の失敗の Error 1回、`Destroy` の追加失敗の報告方針）を一意にする。logger 自身の例外まで no-throw にするか、I3 を backend 例外の隔離に絞るか |

- A2-1〜A2-3 の ID に対する推奨（レビュー §3）: G1 / G2 / G4〜G8・R1〜R5・R7〜R10・X6・X7・U3 は採用、G3 は一部採用（削減ありきにせず利用者を列記）、R6 は記録、X1 は不採用（pool / reuse が無い段階では generation slot は過剰で、class handle を第一候補）、X2 は保留または不採用、X3 / X4 は d2 へ送る、X5 は不採用（notifier を残し Tick は保険）、X8 は §24 のみ、X9 は timeout の既定値を d2 / d4 で再評価し App / Manual は d1 で許す、U1 は `UnityEngine.Object`（F-03 の契約付き）、U2 は d2 へ送る判断を支持。
- 重複する指摘: F-04 は R4 / X5 / X6、F-06 は R1、F-07 は R2 / R3、F-08 は G2、F-09 は G1、F-10 は R8 / R9、F-15 は R7 / R4 と同旨。
- d2〜d5 への引継ぎ案（レビュー §6）: d2 に `Create` 途中失敗の後始末と `Destroy` の冪等性、source の保持者と解放順、capability 結果の共有、Tick の登録とフレーム順序、`VfxSystem.Dispose` と `SceneDirector.Dispose` / `ReleaseAll` の順序、実値での検証。d4 に `OnDestroy` の実発火と asset 解放との前後関係。


### A3 統合（2026-10-17 に発注者が凍結を承認）

- 担当: 主担当（Claude Code）が A2-1〜A2-4 を突き合わせて採否案と本文の改訂を作った。発注者は 2026-10-17 に改訂の作業を了承した。発注者は同日、採否と本文を承認して凍結した（再レビュー・xAI レビュー・最終レビュー・自己点検の反映後の版）。
- 改訂の主題: 責務を増やさず、保証の境界を精密にする（A2-4 の総評と同じ）。構成（`VfxSystem` + `VfxHandle` + `VfxOwnerIndex` + notifier、class handle、`OnSceneEvent` の購読、`Tick` は d2 が登録）は A1 を維持した。

| 元 ID | 裁定 | 理由と本文の反映先 |
|---|---|---|
| G1 / F-09 | 採用 | `VfxOwnerIndex` を「索引と Scene 所有者の終端状態」の所有者と書き直し、SceneEvent の解釈は `VfxSystem` に置いた。新しい型は足さない（§3.1、§3.2） |
| G2 / F-08 | 採用 | `VfxHandle` は回収 callback と失敗記録 callback を ctor で受け取り、`VfxSystem` を参照しない。`VfxHandleStateTests` は `VfxSystem` なしで書く（§3.1） |
| G3 | 一部採用 | `VfxBackendCapability` を `bool QueryCapability(out string reason)` に、`VfxSystemOptions` を ctor 引数に置き換え、公開型を8から6に減らした。残る6型は利用者を表で示した。`IVfxSystem` と `VfxSystem` の併存は、Game が `Spawn` だけを見る窓口と composition が使う具象の分離として残す（§3.1） |
| G4 / F-05 | 採用 | `default(AssetOwner)` を索引に入れる前に拒否（`GetHashCode` の例外も避ける）。internal 分類への依存 edge と Kind の switch の扱いを明記（§2.1 I5、§3.1、AC7） |
| G5 | 採用 | `PreUnloading` 通知の時点で `IsSceneLoaded` が true のままであることと、終端中集合を先に更新する理由を §2.3 に書いた。Add 失敗の回収経路は AC6 (b) で観測する |
| G6 / R10 / F-11 | 採用 | `BindSceneLifecycle` の差し替えは前の購読を Dispose し、既存の Scene 所有 handle を回収する（安全側）。二段階にする理由を §3.1 に書いた（§2.3、AC13） |
| G7 / U2 | 採用 | I6 を「Tick に依存する終わり方」と「依存しない終わり方」に分け、`Tick` の登録を d2 の必須受け入れ条件に送った（§2.1 I6、§2.5） |
| G8 / F-04 | 採用 | notifier は `Action<ulong>` を受け取り、system ごとに1つ付ける。同じ go を2つの system が所有しても通知が欠けない（§3.1、AC8） |
| R1 / F-06 | 採用（必須） | AC6 を到達可能な2経路に書き直し、§1.2 と §2.3 の状態名を訂正した。PreLoading 中・WaitLoadChildScene 中の自 Scene 所有の `Spawn` は受理する |
| R2 / F-07 | 採用（必須） | AC10 に「h1 の `Destroy` 例外でも h2 を Unity Scene unload より前に回収、Error ログ1件」を足した。R3 の捕捉に依存しないことを I3 に書いた |
| R3 / F-07 | 採用（必須） | 回収の規則（Released の書き込み → 索引除去 → `Destroy`、snapshot 列挙）を §2.2 の本文に昇格し、AC4 に再入3本を足した |
| R4 / F-04 / F-15 | 採用（必須） | S4 と AC8 を「内部入口で同期に回収」「通知なしは次の Tick」に限定し、notifier の実発火を d4 に送った（§2.1 S4、§2.5） |
| R5 | 採用 | AC7 を「偽 `ISceneQuery` は true のまま、それでも `Create`=0」に固定した。`Type` と `State` の組で判定することを §2.3 に書いた |
| R6 | 記録 | 終端の網羅の確認結果を §1.2 に反映した。Unload が途中で失敗して PreUnloading のまま残った個体は、既存の AddScene の永久待ちと同じく終端中集合に残る（d1 では変えない） |
| R7 / F-15 | 採用 | AC5 / AC6 / AC10 の順序を `UnitySceneUnloadAction` で捕捉する。`TestableSceneDirector` は変更しない |
| R8 / F-10 | 採用 | timeout は `待ち時間 >= timeout`、到達した Tick は粒子判定を呼ばない、負・NaN・∞は 0、`Tick(0)` も粒子判定する（§2.2、AC3） |
| R9 / F-10 | 採用 | backend `Play` は `bool` を返し、`false` なら状態を変えない。例外とは区別する。`StopEmitting` と `HasAliveParticles` の例外は handle の失敗として回収する（§2.2、§3.1） |
| F-01 | 採用（必須） | `Spawn` の手順を §2.2 に書き、`Create` の例外・無効 id・生存中 id は `Destroy` 要求0回、`Create` の後の失敗と再入は受け取った id に `Destroy` 1回とした（AC9） |
| F-02 | 採用（必須） | I1 を「回収要求を出す時点」の保証に改め、backend 自身の破棄失敗の回復は backend 契約に置いた。d1 の偽 backend テストで native 資源の残存を判定しないことを §2.1 冒頭と AC9 に書いた |
| F-03 / X6 / U1 | 採用（必須） | `Spawn` は `UnityEngine.Object source` を受け取る（U1 は A1 案）。呼び出し側が instance の Released まで source を有効に保つ事前条件と、「同じ owner なら安全」という推論の禁止、安全な組み合わせの例を §2.3 に書いた。実 backend での確認は d2 / d4 |
| F-12 / X4 | d2 へ送る | capability 結果の単一の出所（`VfxSystem` が1回呼び、backend が自分の結果を配置済み VFX に共有）と、backend 未構成を Unsupported と区別することを d2 の必須受け入れ条件にした（§2.5） |
| F-13 | 採用 | `source` の null / 偽 null と破棄済み `parent` は拒否、`parent` が null は親を指定しない意味（配置先の Unity Scene は d2 の backend が決める。R2-06 で訂正）、位置・回転は world 座標、backend は生存中の instance に同じ id を返さない（§2.1 I5、§3.1、AC7・AC9） |
| F-14 | 採用 | I3 を backend 由来の例外に絞り、ログの回数を §3.2 に固定した |
| X1 | 不採用 | pool / 再利用が無い段階では generation slot は過剰。class handle を維持する（A2-4 も同じ推奨） |
| X2 | 不採用 | backend を instance object 方式にしても回収の規則は変わらず、偽 backend での回数の数え方が複雑になる。id 方式で `Create` 失敗の扱いを F-01 で固定した |
| X3 | d2 へ送る | `IUpdateElement` の adapter と実際の登録は composition の責務（§2.5） |
| X5 | 不採用 | notifier を残し、Tick は保険にする。通知が届く場合の同期回収を失わないため |
| X7 | 採用 | `PreUnloading` / `LoadCanceled` を主な入口、`OnCompleted` を最終回収とする（A1 と同じ） |
| X8 | 一部採用 | harvest は §24 だけ。`OnSceneEvent` の意味を変えないので §05 には足さない |
| X9 | 裁定 | timeout の既定値は 10 秒（d2 / d4 で再評価）。App / Manual 所有は d1 で許し、`VfxSystem.Dispose` を終端にする。終端済み所有者への `Spawn` は警告を出さない（拒否の理由は handle の `IsActive=false` で分かる） |
| U3 | 採用 | 回収点は `PreUnloading`。その Scene 自身の `OnPreUnLoadedImpl` から、その Scene 所有の VFX の消え切り待ちを始めることはできない（program §6.4 の「所有者の unload は消え切り待ちを打ち切る」と整合） |

#### A3 改訂版への再レビュー（A2-4 と同じ担当、2026-10-17）

- 担当・入力: 発注者が OpenAI 系列のモデル（GPT）に、A3 改訂版（未 commit）・A1＋A2 版・A2-4 の原文を渡して依頼した。原文は発注者の手元層。判定は「方向性は妥当。R2-01〜R2-03 を反映し、差分を確認したうえで承認候補」。A2-4 の F-01〜F-07・G1〜G3・X1・U2 は解消と評価された。
- 主担当の裁定（凍結の承認と一緒に人間が確認する）:

| ID | 内容（要約） | 裁定と反映先 |
|---|---|---|
| R2-01 | backend の `Play` / `StopEmitting` を呼んでいる最中に同じ handle の `Stop()` / `Dispose()` が再入すると、外側が Playing / Stopping を書き戻して Released から復活する（I2 違反） | 採用（必須）。「backend 呼び出しの後の状態の書き戻し」と `Dispose` の手順を §2.2 の本文に足し、AC4 に (d)(e) を足した |
| R2-02 | ctor の timeout が NaN / ±∞ だと `待ち時間 >= timeout` が成立せず、I6 が破れる | 採用（必須）。有限の正の値以外を ctor で `ArgumentOutOfRangeException` にし、I3 の対象外と明記した（§2.1 I6、§3.1、AC3） |
| R2-03 | AC14 は差分が Vfx と本 HANDOFF に閉じることを求めるが、A3 凍結 commit は program も変えるので、base からの比較では不合格になる | 採用（必須）。比較を (a) 凍結 commit → head と (b) base → head に分け、(b) では program だけを追加で許した（AC14） |
| R2-04 | 初回の `BindSceneLifecycle` が `PreUnloading` の後になると、終端を知らないまま受理しうる | 採用。SceneDirector の生成直後・最初のロード前に呼ぶ使用の前提を §3.1 に書き、配線順を d2 の必須事項に送った。差し替えの間は `Scene(id)` の `Spawn` を拒否する（§2.3） |
| R2-05 | `Destroy` 要求と Unity の native 実体の破棄完了は別（`Object.Destroy` はフレームの終わりまで遅れる） | d2 へ送る。d1 の保証範囲（要求の時点と回数）は変えない（§2.5） |
| R2-06 | `Parent == null` の「Scene root」は、backend が所有者の Scene を知らないので一意に決まらない | 採用。「親を指定しない。配置先は backend の責務」に訂正し、d2 に送った（§3.1、§2.5） |
| R2-07 | go が残る間、notifier の callback が `VfxSystem` を参照し続ける | 記録。`Dispose` 後の通知は何もしないことを §3.1 に書き、参照の残存は既知として Phase C の観察項目にした。汎用の購読機構は作らない |

#### A3 改訂版への xAI レビュー（凍結前の事前レビュー、2026-10-17）

- 担当・入力: 発注者が xAI 系列のモデル（Grok）に、A3 改訂版（未 commit、再レビューの反映後）の全文を渡して依頼した。採否表を含むので盲検ではない。原文は発注者の手元層。コードは `ba8245f4` で照合された。
- 判定: 「凍結しない（NO-GO）。構成は維持し、B1〜B3 を本文と受け入れ条件に反映してから承認する」。§1.2・§2.3 の主張（`PreUnloading` 時点の `IsSceneLoaded`、`LoadCanceled` の順序、AC6 の到達性、再ロードの `PreLoading`、`Removed` / `CancelCleanedUp` の `State`、`default(AssetOwner)` の例外、AC5 の観測、asmdef 不要、notifier の d4 送り、harvest 先）はコードと一致と評価された。
- このレビューは C' ではない。xAI を C' に使う場合も `独立性制約あり` になる（§5 の C' 予約）。

| ID | 内容（要約） | 裁定と反映先 |
|---|---|---|
| B1 | S2 は `StopAllowingParticlesToFinish` が必ず `StopEmitting` を呼ぶと書くが、状態表は Created なら即 `Release` する。AC1 / AC2 は成功した `Play` の後しか見ない | 採用（必須）。S2 を Playing に限り、Created の即 `Release`、`Play` が `false` のときの状態、Playing 中の再 `Play` を AC1 に足した |
| B2 | 破棄済み `parent` の拒否と、`parent == null` を「親なし」とする扱いは、偽 null では区別できない（`ReferenceEquals` は常時契約で禁止） | 採用（必須）。`VfxSpawnRequest` に `HasParent` を足し、親なしの ctor と親ありの ctor を分けた（§3.1、I5、AC7） |
| B3 | Set* の名前が null・空文字だと、backend の `ArgumentNullException` で instance ごと回収されうる | 採用（必須）。backend を呼ばず `false`、`Release` しない（§2.2、AC12） |
| W1 | `Create` が発射しないことを backend 契約に書く | 採用（§3.1 の `IVfxBackend`） |
| W2 | `IVfxHandle.Dispose` は消え切り待ちを打ち切る。`using` で待ちが打ち切られることを公開面に書く | 採用（§3.1 の `IVfxHandle`） |
| W3 | AC5 の「子」は active な子孫に限る。PoNR 後にまだロード中の子は親の 3-phase に入らない | 採用（AC5）。親の終端を子へ伝播させる実装はしない |
| W4 | AC7 の破棄済み go は、生存中に作った `AssetOwner` を `DestroyImmediate` の後で渡す形にする。`OnCompleted` の完了印は回収より先 | 採用（AC7、§2.3） |

#### A3 最終候補への OpenAI レビュー（2026-10-17）

- 担当・入力: A2-4・再レビューと同じ担当（OpenAI 系列）。xAI レビューと自己点検の反映後の未 commit 版を、発注者が渡した。原文は発注者の手元層。
- 判定: 「設計方針は承認可能。F1 / F2 を最小反映した版を A3 凍結 GO とする」。アーキテクチャと公開 API・スライス境界は GO。R2-01〜R2-07 は解消と評価された。

| ID | 内容（要約） | 裁定と反映先 |
|---|---|---|
| F1 | 「戻った時点で Released なら書き戻さない」だけでは、Playing 中の再 `Play` の `backend.Play` 中に `StopAllowingParticlesToFinish` が再入して Stopping になった後、外側が Playing を書いて一方向の遷移に反する | 採用（必須）。書き戻しを「呼び出し前と同じ状態のときだけ書く」に改め、`StopAllowingParticlesToFinish` は `StopEmitting` の前に Stopping を書く（§2.2）。AC4 (f) を足し、`IsActive` だけで判定しないと書いた |
| F2 | AC13 の「Dispose 後のイベントで backend 呼び出し0」は、購読を解除し忘れても `Dispose` 済みの早期 return で合格してしまう | 採用（必須）。購読と解除を数えるテスト用 Observable で、初回・差し替え・`Dispose`・二回目の `Dispose` の回数を直接観測する（AC13、§3.1 のテストの表） |
| F3 | 本書に「本書自身を含む凍結 commit と content sha256」を書くと循環する | 採用。凍結 commit の後の記録用 commit で書き、書いた値は直前の凍結版を指すと定義した（§0）。機能仕様は変えない |

#### 主担当の自己点検（Phase C / C' で指摘されそうな点、2026-10-17）

発注者の依頼で、主担当が凍結前の本文を判定 C・C' の目で読み直した。

| ID | 内容 | 反映 |
|---|---|---|
| S-01 | AC14 (a) の許容範囲が `Rendering/Vfx/` 配下だけで、新しいフォルダ自身の `.meta`（`Rendering/Vfx.meta`）が外にあるため、判定 C で不合格になる | AC14 に2つのフォルダ `.meta` を足した |
| S-02 | I2 の「その id への backend 呼び出し0」は、backend が破棄済みの id を別の instance に使い回すと正しい動作が違反に読める | I2 を handle（1つの instance）単位に書き直した |
| S-03 | I3 の「全メソッドから例外を投げない」と、ctor・`BindSceneLifecycle` の引数検査の例外の関係が未定義 | 引数誤りの例外（`ArgumentNullException` / `ArgumentOutOfRangeException`）を I3 の対象外と明記した |
| S-04 | スレッドの前提が `IVfxBackend` にしか無い | `VfxSystem` も main thread 限定（スレッド安全にしない）と書いた |
| S-05 | Scene identity の比較方法が未指定 | `StringComparison.Ordinal` と書いた |
| S-06 | AC13 の「購読解除」を `Subject.HasObservers` で確かめる案（A2-2 R10）は、リポジトリに使用例が無く、Phase B でその API の有無を確かめる手間が残る | Dispose 後に同じ Subject へ送ったイベントで backend 呼び出し0、という振る舞いの確認に変えた |
| S-07 | R2-01 の再入テスト (d)(e) と ctor の timeout の値域が、どのテストファイルに入るか書かれていない | §3.1 のテストの表に割り当てた |
| S-08 | `default(VfxSpawnRequest)` の扱い | `Source` が null なので I5 で拒否されると明記した |

残る確認（Phase B で観測する）: `OneStarMaker.Runtime` から `ILogger<T>`（Microsoft.Extensions.Logging.Abstractions）が参照できるのは、既存の `UpdateBehaviourAdapter` がそれを使ってコンパイルできていることを根拠にしている。asmdef の変更が必要になったら §4 の差し戻し条件で Phase A に返す。

- program の更新（凍結と同じ commit）: `S-4_FULL_SPEC_WORLD_AUTHORING.md` の §6.3 d1 行（型名・配置と、d1 で答える範囲）、d2 行・d4 行（§2.5 の送り事項）、§6.4（source 契約と回収点）、§10（公開型6つ）。
- 残る反証可能性（凍結後に B / C で見る点）: AC6 (b) の終端通知が実際にどちらになるか（観測して記録する）。偽 backend の再入 callback で、実 Unity の `DestroyImmediate` の同期再入を十分に再現できるか（d2 / d4 で実 backend を見る）。

## 6. Phase B 実装結果

- implementation head: §0 の欄を正とする（この節を書いた commit と同じ）。
- 変更: §3.1 の新規ファイル（Runtime 10・テスト 6）と各 `.meta`、新しいフォルダの `.meta`、本書。既存ファイル・asmdef・SampleGame・`TestableSceneDirector` の変更なし。`com.unity.visualeffectgraph` の追加なし。
- 行数（予想との差）: `VfxSystem.cs` 435（予想 300）、`VfxHandle.cs` 251（予想 190）、`VfxOwnerIndex.cs` 115、他の Runtime は 24〜55。増分の大半は XML doc と理由のコメントで、責務は §3.1 の一行のまま。500 行の警報には当たらない。テストは `VfxSystemOwnerTests.cs` が最大。

### HANDOFF との差（B 適応の記録）

- **internal のテスト用差し込み口を1つ足した。** `VfxSystem.RegistrationFaultForTest`（internal、`Action<VfxHandle>?`）。AC9 の「`Create` の後の登録・notifier 設置が例外」を、実 Unity の API（`AddComponent` は通常例外を投げない）で再現する手段が無いため。Spawn の登録段階の try の中で呼ぶだけで、公開面・状態・所有者・寿命は増えない。あわせて internal の `VfxSystem.LiveInstanceCount`（索引の件数。AC7・AC9・AC13 の「索引の件数0」の観測用）と `VfxOwnerDestroyNotifier.SystemKey`（同じ system の notifier を二重に付けない判定）、`VfxHandle.HandleState` / `State` / `Id` / `Owner`（internal）を置いた。§4 の差し戻し条件（§3.1 にない**公開**型・公開メンバー）には当たらないと判断した。
- **delta の正規化（§2.2 Tick 手順 (1)）は `VfxHandle.AdvanceStopping` で行う。** `VfxSystem.Tick` は値をそのまま渡す。`VfxHandleStateTests` が handle 単体で AC3 の delta を確かめるため。外から見た挙動は同じ。
- **AC4 (c) の再入 Spawn の扱い。** AC4 は (a)(b)(c) を「同じ Scene 所有の Spawn を再入させ、`Create`=0」と一括で書くが、`Tick` は所有者の終端ではなく、§2.3 の受理規則では生きている Scene への Spawn は受理される（Tick 中に拒否する規則は本文に無い）。§2.3 を正とし、(c) のテスト（`Reentry_DuringTickRelease_IsSafe`）は「破棄済み `Bind` 所有者への再入 Spawn は `Create`=0」「生きている Scene への再入 Spawn は受理され、進行中の Tick の snapshot に巻き込まれず Created のまま」「列挙例外0、各 id の `Destroy`=1」を判定する。(a)(b) は AC4 の文言どおり `Create`=0。発見 C で AC4 の文言と §2.3 のどちらを正とするかの確認を求める。

### 観測と未実行

- Editor でのコンパイル: 2026-10-17、この worktree の project を Unity 6000.6.0f1 で起動（Library が無い状態からの初回 import）。`Tundra build success`、`error CS` 0 件、`OneStarMaker.Runtime.dll` / `OneStarMaker.Tests.dll` に新しい型が入ったことを確認。`.meta` は Editor が生成した（21 件。ファイル 16・フォルダ 5）。import の副作用で `unity/Assets/MobileDependencyResolver/` の `.pdb.meta` 5 件が削除されたが、本スライスの対象外なので git で戻した。
- `ILogger<T>` の参照（§5 の残る確認）: `VfxSystem` は `Microsoft.Extensions.Logging` を asmdef の変更なしで参照してコンパイルできた。
- AC6 (b) の終端通知（`PreUnloading` / `LoadCanceled` のどちらか）: テストは観測した方を `TestContext.Out` に出す。Phase B ではテストを実行しないので**未観測**。Phase C の結果 XML の出力から記録する。
- `pwsh tools/contract-audit.ps1`: errors=0 warnings=0（検査1 の差分 16 ファイル）。
- 未実行: Unity テスト（`pwsh tools/run-tests.ps1`。Phase C）、Build、動作確認（§5 で `対象なし`）。

## 7. Phase C

### 発見 C（2026-10-17）

- 種別: 発見（GO 判定・判定 C・C' の起動はしていない）
- 使用した evidence の取得案内・状態: 未保存。commit 固定（base `ba8245f4`・head `4289fbf0`、`git diff --name-status` / `--stat` と `pwsh tools/contract-audit.ps1`・`pwsh tools/docs-audit.ps1` の結果）でレビューした。stage snapshot は使っていない。下記テストの生ログ・XML・step.json は `tools/run-tests.ps1` の出力（リポジトリ直下 `TestResults/` の GUID directory `cc372836-b9fa-4b2b-b92b-64dba56c22be`、git 管理外）に原 source として残っている。`evidence stage` は未実施で、判定 C の bundle 生成時に行う。
- 構造適合:
  - §3.1 の責務マップと一致。Runtime の新規ファイル 10（`Abstractions` 5・`Implements` 4・`Components` 1）、テスト 6、各 `.meta`、フォルダ `.meta` 5。公開型は §3.1 の 6 つだけ（`IVfxSystem` / `VfxSystem` / `IVfxHandle` / `VfxSpawnRequest` / `IVfxBackend` / `VfxInstanceId`）。公開シグネチャに `UnityEngine.VFX` 名前空間の型なし（grep 0 件）。asmdef 変更 0。既存ファイル・SampleGame・`TestableSceneDirector` の変更なし。`UpdateSystemRuntime` への登録なし。
  - AC14 (a): `git diff --name-status 1462b14c 4289fbf0` は `Rendering/Vfx/`・`Tests/Rendering/Vfx/`（`.meta` 含む）・2 つのフォルダ `.meta`・本書に閉じる。(b): base からの差分はそれに加えて `S-4_FULL_SPEC_WORLD_AUTHORING.md` だけ。`contract-audit.ps1` は errors=0 warnings=0。`docs-audit.ps1` は errors=0 warnings=0。
  - 依存: `VfxSystem` / `VfxOwnerIndex` が `AssetOwner` の internal 分類を読む（§3.1 の依存 edge どおり）。`AssetOwnerKind` の switch は 4 値を列挙し default は拒否。`VfxHandle` は `VfxSystem` を参照せず callback だけを受ける（G2）。notifier は `Action<ulong>` を受ける（G8）。
  - §2.2 の回収の規則（Released を書く → 索引除去 → `Destroy`、snapshot 列挙）は `VfxHandle.Release` の 1 か所に閉じ、複数回収は全て `VfxOwnerIndex` の snapshot を列挙している。backend 呼び出し後の書き戻しは「呼び出し前と同じ状態のときだけ」になっている（`Play` / `SetParameter` / `AdvanceStopping`）。`StopAllowingParticlesToFinish` は `StopEmitting` の前に Stopping を書く（F1）。`Spawn` の手順 (1)〜(5) と `Dispose` の手順は §2.2 どおり。終端中集合への登録は回収より先（§2.3）。
  - Unity の偽 null: `request.Source` / `request.Parent` / `owner.BoundObject` / `go` は `== null` / `!= null` で判定。`ReferenceEquals` は `VfxHandle` と `SystemKey`（`VfxSystem`、非 `UnityEngine.Object`）にだけ使われている。`#nullable enable` 全件、`record` なし、テストに `Task.Delay` / `Thread.Sleep` なし。
  - 行数: `VfxSystem.cs` 435（予想 300、+45%）、`VfxHandle.cs` 251（予想 190）。500 行未満で、増分は XML doc と理由コメント。§3.1 の変更理由は 1 行のまま。分割は求めない。
- 現在の問いを阻害する findings: **なし**（blocker 0、major 0）。I1〜I7・S1〜S6 と AC1〜AC14 の各項目を実装とテストに対応付け、対応する偽 backend の呼び出し回数・順序の判定がテストにあることを確認した。
- 後続スライスへ移送する findings・記録（いずれも凍結済み条件・常時契約への違反根拠なし）:
  - C-01（記録、d2）: `VfxSystem.Tick` は全体を 1 つの try/catch で囲む。backend の例外は `VfxHandle` で捕捉されるので I3 は成立するが、backend 以外の例外（callback や logger 由来）が 1 つの handle で起きると、同じ Tick の残りの handle の処理が飛ぶ。`UpdateSystemRuntime` 登録時（d2）に Coordinator の隔離と合わせて観察する。
  - C-02（記録、d2）: Unsupported の `VfxSystem` も `BindSceneLifecycle` で scene event を購読し、終端中集合を維持する。backend 呼び出しは 0 で I4 は成立する。d2 の「backend 未構成と Unsupported の区別」の入力。
  - C-03（記録、同時数・pool のスライス）: `EnsureDestroyNotifier` は `Bind` 所有の `Spawn` ごとに `GetComponents<VfxOwnerDestroyNotifier>()` の配列を割り当てる。
  - C-04（記録、R2-07 既知）: 登録段階で失敗した `Spawn` や Released 後も notifier は go に残り、callback が `VfxSystem` を参照し続ける。`Dispose` 後の通知は無視される。
  - C-05（テストの頑健性、d4 の入力）: `BindOwner_DestroyedWithoutNotification_ReleasedOnNextTick` は Tick 前の `Destroy`=0 を厳密に判定するが、AC8 は「0 でも仕様内」（1 でも違反ではない）。EditMode の `DestroyImmediate` で `OnDestroy` が発火する環境では、仕様内の挙動でテストが落ちる。現環境では通っている。
  - C-06（命名）: `Tick_CompletesGracefulStop_AndTimesOut` は timeout 経路だけを通る（粒子ありのまま）。粒子なしで `Destroy` する経路は `Reentry_DuringTickRelease_IsSafe` と `VfxHandleStateTests` が持つ。
- §6「HANDOFF との差」の 3 点への判断:
  1. internal のテスト用差し込み口（`RegistrationFaultForTest`、`LiveInstanceCount`、`SystemKey`、`VfxHandle` の internal 観測用メンバー）: **B 適応として受理。** §4 の差し戻し条件は公開型・公開メンバーで、これらは internal。契約・所有者・寿命・公開面を変えない。`RegistrationFaultForTest` は Runtime に残るテスト専用の seam なので、d2 以降の composition から使わないこと（harvest 時に注記する）。
  2. delta の正規化を `VfxHandle.AdvanceStopping` で行う: **B 適応として受理。** §2.2 Tick 手順 (1) の意味（負・NaN・∞を 0）は変わらず、外から見た挙動は同じ。AC3 の delta のテストは handle 単体で固定されている。
  3. AC4 (c) の再入 `Spawn`: **§2.3 / I5 を正とする B の読みを支持する。** `Tick` は所有者の終端ではなく、I5 の拒否理由は網羅列挙で「Tick 中」を含まない。(c) で生きている Scene の `Spawn` を拒否するには §3.1 にない状態（Tick 中の印）が要る。B のテストは、(c) の「`Create`=0」を破棄済み `Bind` 所有者への再入 `Spawn` で、生きている Scene への再入 `Spawn` は受理されて進行中の列挙に巻き込まれないことを判定している。AC4 の文言と I5 の矛盾は「受け入れ条件の文言の隙間」であり blocker にしない。**判定 C の前に人間が決定記録で読み方を確定することを求める**（「(c) の `Create`=0 は終端済み所有者への再入に適用し、生きている Scene の受理は I5 に従う」）。Tick 中に Scene の `Spawn` を拒否する規則を望むなら、それは新しい状態の追加なので Phase A の新 revision になる。
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Rendering.Vfx`（sandbox 外、Editor が閉じていることを lockfile と process で確認してから起動）。§5 の起点 filter そのまま。Unity テストが一度も実行されていないため、凍結済み失敗経路（AC4 の再入、AC9・AC10 の例外、AC6 の到達性）と AC6 (b) の終端通知の観測に必要。
- テスト結果: XML `results.xml` に 82 件、passed 82 / failed 0 / skipped 0、exit 0、所要 約 215 秒。3 ファイルのテストが全て収録されている（`VfxHandleStateTests` 32、`VfxSystemOwnerTests` 45、`VfxSystemSceneDirectorTests` 5。TestCase 展開を含む）。Unity は正常終了（log 末尾に leak check と Package Manager shutdown、stall なし）。**AC6 (b) の観測: `OnLoaded` の非キャンセル例外からの回収は `StateChanged(LoadCanceled)` を出した**（`TestContext.Out` の `OnLoaded failure terminal event: LoadCanceled`）。これは `RecoverFailedLoadAsync` が WaitLoadChildScene の個体を `stillLoading` に分類して `CleanupCanceledScene` に入れる経路（`SceneDirector.FailedLoadCleanup.cs` / `SceneLifecycleManager.IsInLoadingPhase`）と一致する。Editor の import が `unity/Assets/MobileDependencyResolver/` の `.pdb.meta` 5 件を削除したので `git checkout` で戻した（§0 の記載どおり、本スライスの変更ではない）。
- 判定必須のうち未実行: 最終の全 EditMode 回帰（空 filter）。判定 C で実行する。
- 重い検証を発見段階で限定実行した場合の理由と範囲: 上記の起点 filter 1 回だけ。PlayMode・Build・全回帰は実行していない。
- 未確認事項: notifier の `OnDestroy` の実発火（d4）。実 backend での `Destroy` の冪等性・失効 source への耐性（d2）。
- 担当・モデル: 判断担当と実行担当は同一（Claude Fable 系列、Anthropic。Phase B とは異なるモデル、新しい session）。

### 判定 C（2026-10-17）

- 種別: 判定（GO 候補 head `4289fbf0`。**判定 C は未完了**。理由は下記「判定必須のうち不合格」）
- 使用した evidence の取得案内・状態: ローカル固定済み（未昇格）。commit 固定（base `ba8245f4` / head `4289fbf0`）の bundle 1 件を task `s-4d1-vfx-abstraction` に `evidence stage` で固定し、送信側で fetch と manifest の全 hash 一致を確認した。取得案内は主 checkout の git 管理外 `TestResults/s-4d1-vfx-stage/retrieval-guide.md`（本 worktree の同 path に写し）。bundle は発見 C・判定 C の所見と DR-1 を含まない（C' 入力に転用できる形）。head には軽量 tag `evidence/s-4d1-vfx-abstraction/1` を付けた（push しない）。
- 開始時の照合: branch `claude/s-4d1-vfx-abstraction` の既存 worktree。HEAD `7ca9305a` は `4289fbf0` の子孫で、`git diff 4289fbf0 HEAD --name-only` は本書だけ。作業ツリー clean。Unity process 0・lockfile 不在を確認してから起動。DR-1（§2）は判定の前に人間から出ている。
- 構造適合・機械検査（head で再確認）: `pwsh tools/contract-audit.ps1` errors=0 warnings=0（検査1 の差分 16 ファイル）。`pwsh tools/docs-audit.ps1` errors=0 warnings=0。AC14 (a) `git diff --name-status 1462b14c 4289fbf0`: `Rendering/Vfx/`・`Tests/Rendering/Vfx/`（`.meta` 含む）・2 つのフォルダ `.meta`・本書に閉じる。(b) base からの差分はそれに加えて `S-4_FULL_SPEC_WORLD_AUTHORING.md` だけ。asmdef 差分 0、`manifest.json` 差分 0、Runtime の Vfx 配下に `UnityEngine.VFX` 参照 0、公開型は §3.1 の 6 つ、`UpdateSystemRuntime` への登録 0（コメントでの言及 1 件のみ）、`#nullable enable` 欠落 0。
- 実行したテストコマンドと対象: いずれも sandbox 外の pwsh から `pwsh tools/run-tests.ps1`（EditMode、`-nographics`、Unity 6000.6.0f1）。
  1. `-Filter OneStarMaker.Tests.Rendering.Vfx`: XML 82 件、passed 82 / failed 0 / skipped 0、exit 0、所要 約 96 秒。3 ファイルのテストを収録（`VfxHandleStateTests` 32、`VfxSystemOwnerTests` 45、`VfxSystemSceneDirectorTests` 5）。AC6 (b) の終端通知は `LoadCanceled`（`OnLoadedFailure_DestroysAtObservedTerminalEvent` の出力。発見 C と同じ）。Unity は正常終了（log 末尾に leak check、stall なし）。
  2. 空 filter（全 EditMode 回帰）: XML 1,232 件、**passed 1,191 / failed 41** / skipped 0、Unity の終了コード 2（`One or more tests failed`）、テスト所要 594 秒。assembly 別: `OneStarMaker.Tests` 770/770、`OneStarMaker.Tests.Editor` 153/153、`SampleGame.Tests` 131/131、`Addressables DocExample` 1/1、`OneStarMaker.Tests.TestObservation.Editor` 39/40、`SampleGame.Tests.Editor` 97/137。Vfx の 82 件は全件収録・全件 passed。
- 判定必須のうち不合格（41 件、全て本スライスの差分外）:
  - `SampleGame.Tests.Editor` の `OneStarMaker.Tests.Editor.WorldAuthoring.WorldCompanionCreationTransactionTests` 13 件・`WorldCompanionRecoveryTests` 26 件: 共通の一次原因は `WorldCompanionOwnershipProof.HashFile` が `__WorldCompanionTests_<id>/.../<id>_Events/<id>_Events.unity.meta` を `File.OpenRead` した時点の `DirectoryNotFoundException`（同じ log で、その scene と folder の import 自体は記録されている）。S-4a/S-4c の Editor 側（`SampleGame/DependOnAll/Editor/WorldAuthoring/`）で、本スライスは触れていない。S-4c の判定記録（commit `d550832b`、2026-09-23）でも `WorldCompanionRecoveryTests` が一度 Failed になり、再実行で 908/908 になった履歴がある。
  - `OneStarMaker.Tests.Editor.Build.BuildContentDirectoryIntegrationTests.ScenePrefabTextureAndTwoRepresentationsBuildAndMoveAsOneDirectory` 1 件: `Universal Render Pipeline/Lit` の shader compile が `out of memory during compilation`（環境の資源）。
  - `OneStarMaker.Tests.Editor.TestObservation.ObservationReloadTests.RealDomainReloadRoundTrip` 1 件: `ContentDirectoryException: content:runtimeMode is required unless a verified installed revision pair is provided` の未期待ログ。
  - 判定 C の扱い: 判定必須は「空 filter の全 EditMode 回帰で failed 0」（§5、osm-workflow Phase C「exit 0 は 1 件以上実行かつ failed 0」）。失敗はいずれも本スライスの凍結条件・常時契約への違反根拠を持たない（差分外・環境起因の疑い）が、判定 C はこの規則を自分で緩めないので **未完了** とし、人間の判断を求める。判定 C の担当が行った限定診断は下記。
  - 限定診断（1 回）: 失敗 4 fixture を `-Filter 'OneStarMaker.Tests.Editor.WorldAuthoring|OneStarMaker.Tests.Editor.Build.BuildContentDirectoryIntegrationTests|OneStarMaker.Tests.Editor.TestObservation.ObservationReloadTests'` で単独実行（XML 75 件、passed 35 / failed 40）。WorldCompanion 系 39 件と `RealDomainReloadRoundTrip` は同じ例外で再現、`BuildContentDirectoryIntegrationTests` の 1 件は passed（全回帰での失敗は shader compile の OOM）。全体成功への読み替えには使わない。
- Unity の終了: 起点 filter の run は正常終了。全回帰と限定診断の 2 run は、log が `Test run completed … Exiting with code 2` と MemoryLeaks 行で終わった後に Unity PID が残り（`tools/Harness/README.md` の既知 native 終了 stall と同症状。限定調査は同症状の確認 1 回のみ）、runner pwsh が `step.json` を書かずに終了していた。今回起動した PID だけを対象に終了処理を行い（その時点で `Stop-Process` は「見つかりません」= 既に exited で handle 保持の entry だけが残存）、判定は完成した results.xml と log 末尾に基づく。テストの副作用（`AddressableAssetSettings.asset` の `m_currentHash`、`.pdb.meta` 5 件）は `git checkout` で戻した。作業ツリーは本書以外 clean。
- 現在の問いを阻害する findings（本スライスの凍結条件・常時契約への違反）: なし。I1〜I7・S1〜S6・AC1〜AC14 に対応する Vfx の 82 件は起点 filter・全回帰の両方で全件 passed、AC14・I7 の機械確認は上記のとおり合格。
- 後続・人間へ返す事項: 全 EditMode 回帰の failed 41 件の扱い（§0 の引継ぎ票）。発見 C の C-01〜C-06 はそのまま後続入力。
- 判定: **判定 C 未完了**（判定必須の全 EditMode 回帰が failed 0 でない。本スライスに帰属する不合格は 0）。GO / NO-GO は記録しない。C' は起動しない。
- 担当・モデル: 判断担当と実行担当は同一（Claude Fable 系列、Anthropic。Phase B・発見 C とは別の新しい session。発見 C と同系列）。

## 8. Phase C'

未着手

## 9. Phase D

未着手
