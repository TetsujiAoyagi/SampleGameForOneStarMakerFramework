# HLOD resident pilot — Phase A1 初稿

## 0. メタデータ

- type: `slice`
- status: **A / A1 r2。A2 architecture 再確認・受け入れ境界レビュー済み、指摘反映。A3 未凍結。Phase B を開始する指示ではない。**
- branch: `codex/hlod-resident-pilot`（PR base: `develop`）
- implementation base commit: `2c29c99806788406551affba6cc795e67e614748`
- implementation head commit: 未生成（現在は文書のみ）
- risk: `high`（小規模だが表示所有者・フレーム境界を新設するため）
- owner: 主担当エージェント（A1 / 調整）、発注者（A3 / D）。B / C / C' 担当は未選定
- created: 2026-10-04
- expires: 2026-11-04 または方針変更時に見直す
- harvest to: `unity/Assets/Docs/Architecture/24-rendering-system.md`。限定された実証結果のみ反映し、D で本 HANDOFF を削除
- program: [HLOD_PROGRAM.md](HLOD_PROGRAM.md) の段1
- Phase A snapshot path / id・生成時刻・hash: r1 A2 入力は文書 commit `4c8e1e2c` の直前固定版（pilot SHA256 `1de19a246bce671a601e0f99bfff676aa259b27ede2fa60090f8bedae1c3ad9b`、2026-10-04T03:45:13Z）。r2 A2 入力: `hlod-a1-r2`、pilot SHA256 `ca3e81162618c1ec17d7a86c61997ce9ef8b2c14d77b0ab80716080837870258`、program SHA256 `d88352ea41bb5e10a797a70ec3b35ecd2565107717b1d57206663a253f7cd722`、2026-10-04T03:51:58Z。本版は下記A2採否を反映。A3 snapshot は未生成
- Phase B result snapshot / evidence bundle / C' blind bundle（各 path / id・生成時刻・hash）: 未生成
- H1 / H2 / `external-current-v1`: 未適用。従来の tracked HANDOFF を使う

## 1. 目的・A0・対象外

**問い:** 全アセット常駐の小さな fixture で、2 detail 群とその両方を覆う1手製 proxy を、物理・gameplay の寿命を変えず、hysteresis 付きの排他的な表示として切り替えられるか。

現況と設計入力:

1. Scene parent は寿命依存であり HLOD tree ではない。Content Directory は同一 Scene identity の異なる representation の同時ロードと deferred activation を拒否する。
2. Cell の Renderer と Collider は同居する。`CellScene` 自体は bounds の運搬のみで、距離 policy を追加してはならない。companion の役割追加・ロード追随も今回は変更しない。
3. `RenderEnvironment` の最小実装だけが存在し、RenderWorld / BRG は未実装。これらを待たず、SampleGame の限定 fixture で ordinary MeshRenderer の小さな表示所有口を検証する。
4. フレーム順序は `ActivatePendingRegistrations → RunUpdate → RunLateUpdate → ApplyMainThreadChanges → ApplyStructuralChanges`。既存 `UpdateSystemRuntime.RegisterElement / RequestElementApply / UnregisterElement` と `IMainThreadApplyElement` を使う。
5. `SampleGame.InGame` は Runtime / Foundation を参照済み、`SampleGame.Tests` は InGame を参照済み。InGame の `InternalsVisibleTo("SampleGame.Tests")` がある。asmdef 追加・依存変更は不要な構成にする。

対象外: 非同期ロード、asset 解放、Scene / Content Directory / Variant の追加契約、全 companion の再配置、SceneState 変更、RenderWorld、BRG、custom Mesh LOD、cross-fade、baker / proxy 自動生成、階層化、動的 / skinned / transparent renderer、複数 camera、製品の視覚品質・性能採用判定。本番の四季 Cell / SceneMap / AppInitializer / GameSceneFactory は変更しない。

## 2. 意思決定と受け入れ境界（A3 候補）

### 進める最低条件と観測可能な詳細

- **M1: 手製対応関係が明示される。** 2 detail group の ID / MeshRenderer membership / world bounds、proxy の coverage ID 集合 / membership / bounds が宣言される。null、空集合、同一 Renderer の重複・detail/proxy 跨ぎ、coverage の欠落・余分、無効 bounds / 閾値を登録前に拒否する。bounds の包含は数値検査、proxy が対象を代表するという意味上の対応は manifest と代表画像で確認する。
- **M2: 有効な fixture の表示が排他的に切り替わる。** 初期は両 detail 群のみ。距離で Detail / Proxy を選び、dead band は直前の表示を維持する。各 main-camera rendering boundary で「全 detail on / 全 proxy off」またはその逆だけになる。閾値往復、帯域内の揺れ、遠近への直接移動を検証する。操作・画像の検証経路は §5 の未決事項が解けるまで成立済みとしない。
- **M3: 表示以外の寿命を動かさない。** 変更は登録対象の `MeshRenderer.enabled` のみ。`GameObject.SetActive`、Collider.enabled、Scene load/unload、asset API、gameplay component の enable/disable を行わない。同居 Collider の enabled / raycast と gameplay sentinel の継続を前後で確認する。proxy は visual-only とし、proxy 配下に Collider を持たせない。primitive 由来の自動付与 Collider も authoring 時に除去し、その不在を fixture 検査で確認する。
- **M4: 所有と終了が閉じる。** 無効 manifest / 既存 owner との Renderer 衝突 / Updater 未初期化なら、表示の変更なしに登録拒否し、途中取得を戻す。有効登録だけが enabled の変更権を持つ。disable / destroy は Updater 登録と表示 ownership を解放し、生存 Renderer は authored 初期表示に戻す。二重 cleanup、登録失敗後 cleanup、再登録で漏れ・旧 callback の適用を残さない。
- **M5: 対象版の検証が読める。** 最終の全 EditMode 回帰、関連する実フレーム検証・代表画像、機械検査を同じ GO 候補 head の証拠として固定する。数値/構造の合格を画像の代用にせず、画像を全フレーム検査の代用にしない。

判定: M1〜M5 が揃い、現在の問いに致命的な反証がなければ **GO**。不足または違反があれば **NO-GO / 未判定**。CONDITIONAL ACCEPT は定義しない。GO は段2の設計へ進める証拠であり、本番 HLOD 完成・性能向上・メモリ削減を意味しない。条件を満たしたら終了し、将来の問いを追加して引き延ばさない。

後続への移送先: 非同期 proxy は program 段2、visual/gameplay 所有分離は段3a、detail load/release は段3b、階層は段4、baker は段5。複数 camera・影/反射/GI・実コンテンツ品質・性能予算は program の採用判断時に別途 Phase A を切る。これらは本 pilot の blocker にしない。

A3 後の例外承認: なし（A3 自体が未実施）。凍結後の blocker は M1〜M5 または常時契約への違反根拠があるものに限る。新しい受け入れ条件は人間の明示承認と revision が必要。

### 手製 manifest の初期仕様

外部用の汎用 asset schema は作らず、fixture component の serialized fields に宣言する。下表は専用 fixture の**制作仕様**であり、既存 Cell の実データではない。B で Editor により参照を設定し、対象 `.unity` と `.meta` を保存する。

| 宣言 | 値 / membership |
|---|---|
| owner / camera | `ResidentHlodPilot` の1 component / 実 AppInitializer が所有する `View_Main` の Camera を §5 の手順で借用して登録前に直接注入。fixture に Camera / AudioListener / host を追加しない。`Camera.main` の毎フレーム探索はしない |
| detail A | ID `detail-a`、Renderer `DetailA/Base` と `DetailA/Marker`。world bounds center `(-5,2,0)`, size `(10,4,8)` |
| detail B | ID `detail-b`、Renderer `DetailB/Base` と `DetailB/Marker`。world bounds center `(5,2,0)`, size `(10,4,8)` |
| proxy | Renderer `Proxy/Mesh` 1件。coverage IDs は厳密に `{detail-a, detail-b}`。集約 world bounds center `(0,2,0)`, size `(20,4,8)`。proxy 配下の Collider は0件 |
| 距離 | camera world position から集約 AABB への最短距離。near `20`、far `30`（Unity units）。finite かつ `0 <= near < far` |
| 初期表示 | 全 detail enabled=true、proxy enabled=false。全対象 GameObject は active、pilot component は初期 disabled（camera 注入後に enable）。shadow casting は Off、opaque material の静的 geometry。LODGroup / Animator 等の表示競合なし |

detail は小さな2組の手置き primitive、proxy は両組の代表形を表す手置きの1 mesh。自動合成・簡略化はしない。上記 bounds は対応 Renderer.bounds を保守的に含むことを検証し、coverage bounds は両 detail bounds を含む。geometry は固定し、fixture root の移動・回転・スケール変更は実行中に行わない。

manifest 外の Renderer、child 階層全体、全 Cell を自動収集しない。登録 owner が存在する間、他コンポーネントは対象 enabled を書き換えない。この制約は fixture authoring とテストで確認する。未知の第三者 writer を調停する汎用 arbitration は作らない。

### 切替え・登録・解放の契約

- 初期 Detail から、距離 `>= far` で Proxy。Proxy から距離 `<= near` で Detail。帯域内と条件未到達では保持。camera 不在時は直前の有効表示を保持し、登録時 camera 不在は拒否する。
- fixture は最初の描画より前から authored 初期表示を持つ。登録待ちを理由に両方 off / on にせず、有効登録まで enabled を変更しない。無効 manifest は入力表示をそのまま残すだけで、既に壊れた authoring の見た目まで保証しない。
- owner component が全入力を検証し、Renderer ownership を一括取得してから Updater へ登録する。失敗時は取得済み ownership を解放し表示を触らない。ownership は同一 Renderer を二つの pilot owner が借りることを拒否する小さな内部管理に限定する。
- `OnElementLateUpdate` は apply 要求だけを出す。camera / Renderer への Unity I/O と純 policy の呼出しは `ApplyMainThread` で行う。SampleGame の既存 Gameplay layer を利用し、独自 PlayerLoop、MonoBehaviour.Update、SRP callback 内の切替えは追加しない。
- **pre-render swap の意味:** `ApplyMainThreadChanges` 内の1回の同期呼出しで、camera位置を読み、全対象の生存・owned を検査し、旧表現を off / 新表現を on にする。間に await / yield / render を挟まない。CPU 上の各 setter の瞬間を atomic と称さず、次の main-camera culling/render 開始までに集合全体が確定することを証明する。
- この fixture は apply 後から描画まで camera を移動しない。実アプリの既存 Camera Layer による Brain / Modifier / Snapshot 確定後に apply を行う。Cinemachine backend の改変と複数 camera への一般化は対象外。実 framerender boundary の監視は §5 のテストだけが行い、production の SRP 購読は増やさない。
- disable / destroy では先に owner を終了扱いにし、遅延 callback は no-op にする。その後 Unregister と表示復元・ownership 解放を行う。二重 cleanup は no-op。破棄済み Unity object は `== null` で確認しスキップする。fixture の途中で個別 Renderer を破棄する運用と、その一般復旧機構は対象外。

### 本文へ転記した常時制約

Game → Framework の一方向、既存 asmdef 参照を維持する。Editor code は Runtime assembly に置かない。`SceneState` 14値・順序・所有者は変更しない。新規/編集 Unity `.cs` は `#nullable enable`、`record` 禁止。Unity object の偽 null を避け、`?.` / `??` / `is null` / `ReferenceEquals` で生存判定しない。公開ログ抽象は `ILogger<T>`。テストで `Task.Delay` / `Thread.Sleep` を使わずフレーム/シグナルで進める。アセット取得が必要になれば `IAssetManagement` 経由だが、本スライスでは取得自体を増やさない。

## 3. 責務マップと規模

全て**予定**。現在行数は新規のため0。新設 Runtime namespace は `SampleGame.InGame.World.Hlod`。`World/Hlod` は表示 coverage という独立ドメインを置く境界で、汎用 Helpers / Managers を増やす場所ではない。

| 予定ファイル（`unity/Assets/` から） | 一つの責務 / 変更理由 | 所有・依存・公開面・テスト境界 | 現在 → 予定行数 |
|---|---|---|---|
| `SampleGame/InGame/InGameSession/World/Hlod/ResidentHlodPolicy.cs` | 距離と前状態から表現を選ぶ policy。閾値の意味が変わるときだけ変更 | 値のみ、UnityEngine / Updater / 資産 API に非依存の internal 型。状態は owner が持ち、policy は決定的。Unity object なしの単体テスト | 0 → 50–90 |
| `SampleGame/InGame/InGameSession/World/Hlod/ResidentHlodDisplay.cs` | 明示 Renderer 集合の表示 ownership と一括反映を担う Unity I/O | owner の Bind(go) 相当の寿命。内部の ownership 表は active owner の参照のみで cleanup 時に消す。取得時の検証・衝突拒否・enabled 反映・復元を同じ契約に閉じる。internal、Unity MeshRenderer のみ入力。asset/Scene API なし。EditMode で実 Renderer を使用 | 0 → 160–230 |
| `SampleGame/InGame/InGameSession/World/Hlod/ResidentHlodPilot.cs` | serialized manifest と camera を表示/policyへ結び、Updater 参加・終了を調停する orchestration | MonoBehaviour の public authoring 型、操作 API は必要最小限・内部テスト口。component と同寿命で asset 所有は取得しない。既存 Runtime/ Foundation API だけに依存。`OnEnable/OnDisable/OnDestroy` と `IUpdateElement/IMainThreadApplyElement`、純 policy と display へ委譲 | 0 → 150–220 |
| `SampleGame/Tests/Rendering/ResidentHlodPolicyTests.cs` | 距離選択の境界・往復・不正値 | `SampleGame.Tests`、既存 IVT。pure values で検証 | 0 → 80–130 |
| `SampleGame/Tests/Rendering/ResidentHlodDisplayTests.cs` | manifest/ownership/復元/Collider 不変の検証 | `SampleGame.Tests`（Editor-only）。一時 GameObject を所有し finally で cleanup | 0 → 180–280 |
| `SampleGame/Tests/Rendering/ResidentHlodFrameTests.cs` | 実 Updater / main-camera frame の境界観測と代表画像保存 | 既存 Editor-only `SampleGame.Tests` で EnterPlayMode/ExitPlayMode 往復。実 AppInitializer の既存 host / View_Main を借用。test が所有するのは fixture、camera modifier handle、観測購読、capture buffer と一時設定の復元だけ。frame event は観測のみ。bootstrap 読取・scope cleanup・限定 capture 支援は同じ試験の寿命に閉じる | 0 → 260–380 |
| `SampleGame/Tests/Fixtures/Hlod/ResidentHlodPilot.unity` と必要な `.meta` / opaque Material | 上記の手製 manifest と見える代表形 | テスト所有の隔離 Scene。Camera / AudioListener / UpdaterHost は置かず、pilot component は初期 disabled。既存 content / SceneMap / Build Settings は変更しない。B の許可済み Editor 操作で作成 | 新規、小 fixture のみ |

既存 implementation の変更予定は0行。既存 `SampleGame.Tests` は Editor-only で Unity Test Framework を使用しており、新 Runtime 参照は不要。もし EnterPlayMode / 画像 API に新しい asmdef edge が必要なら B で追加せず A へ返す。

新規ファイルは増加率判定ではすべて50%以上だが、小さく見せるための分割はしない。policy / orchestration / Unity I/O は依存とテスト方法が異なるため分ける。display の所有登録・反映・解放は一つの表示契約の取得/使用/返却で、別 registry framework に広げない。500行または独立した3責務になる見込みが出たら A に戻し、行数合わせの委譲を作らない。

## 4. 実装順序と停止条件

1. A2 で同一 A1 snapshot を独立にレビューし、少なくとも1件が配置・責務・依存・owner/寿命・テスト可能性を見る。A0 だけからの代替案レビューを検討し、C' 用の未関与担当を残す。
2. 検証経路と画像の受け入れ方を §5 に実名/環境付きで追記し、人間 A3 で採否と範囲を凍結する。未確定のまま B に進めない。
3. B は policy → display → pilot owner → tests を実装。許可された Editor で小 fixture の authoring・compile 確認を行う。cloud に Editor がなければ Unity CLI / YAML 直書きで代替しない。
4. B は `pwsh tools/contract-audit.ps1` を実行し、Unity テストは未実行として C に渡す（H2 未適用）。C は固定 implementation head から構造・契約の発見レビューを先行し、最終候補で必須検証をまとめる。
5. C' は同じ base/head の blind bundle を使う。D は C/C' を突き合わせ、実装済みの限定契約を harvest して本書を削除する。

停止して A へ返す条件: 新しい状態/owner/寿命/public API/asmdef edge が必要、既存 Scene/companion/Bootstrap の変更が必要、Renderer.enabled だけでは M1〜M4 を満たせない、policy が Unity I/O と分離不能、撮影/実行経路が凍結条件を満たせない。原因未確認は対象を絞って観測し、B 適応で直せる不具合と設計変更を区別する。新しい benchmark や production migration は後続へ送る。

## 5. テスト・検証経路・A2/A3

### 起点テストと判定必須

- pure policy: near/far のちょうど境界、帯域内維持、遠近への直接移動、反復、NaN/Infinity/負値・逆順閾値。
- display/owner: 正しい2→1、重複・null・bounds/coverage 不正の無変更拒否、owner衝突、Updater不在、未active登録からの終了、二重 cleanup、disable→enable、同居 Collider の raycast、proxy配下Collider不在、gameplay sentinel。
- 実フレーム: authored 初期表示→near→far→dead band→near を実 Updater で進め、main-camera render boundary 毎に排他集合を検査。再生終了・再入場でも owner/登録が残らないことを確認する。
- 差し戻し中の起点: `pwsh tools/run-tests.ps1 -Filter SampleGame.Tests.Rendering.ResidentHlod`。画像/実フレーム対象を含むときは `-WithGraphics`。C は違反根拠に応じ filter を変更できる。
- 判定必須: GO 候補 head で `pwsh tools/run-tests.ps1 -WithGraphics`（空 filter、最終全 EditMode 回帰）。同一条件で上記 frame tests も集約する。XMLのテスト名・件数・失敗・skipを確認し、0件や必須fixtureの Ignore を成功にしない。全 EditMode 除外: **なし**。
- `-Platform PlayMode` は採用しない。既存 test asmdefs は Editor-only で、Play 往復は EditMode 内 `EnterPlayMode/ExitPlayMode` の実績を利用する。`-ObserveUnity` は assembly/実行の観測であり画像証拠ではない。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、`git diff --check`。

### 実アプリ host を借用する試験の初期化・終了（r2）

r1 の「test が host を所有する」は撤回する。EnterPlayMode では `AppInitializer` の RuntimeInitialize が実 host / CameraSystem を生成するため、test で `new UpdateSystemHost()` を呼ぶと Runtime singleton を置換し、SceneDirector 未bindの新hostでは通常登録がactiveにならない。既存 bootstrap を変更・抑止・private method呼出しせず、以下の**実アプリ借用経路**に限定する。

1. Editor-only の `ResidentHlodFrameTests` は非並列で実行する。Scene setup / playModeStartScene と、変更する process 環境変数 `SAMPLEGAME_CONTENT__RUNTIMEMODE`、`SAMPLEGAME_DEBUGSOCKET__ENABLED`、`SAMPLEGAME_TELEMETRY__PROFILER__ENABLED` の元値を保存する。作業中の Scene に未保存変更があれば勝手に破棄しない。専用 fixture Scene を起点にし、process限定でそれぞれ `addressables` / `false` / `false` として EnterPlayMode に入る。User/Machine 環境変数、config asset、Addressables 設定、既存 SceneMap は変更しない。これは bootstrap 用の既存互換経路であり、HLOD が新規 asset load を所有する意味ではない。
2. 実 `AppInitializer.Sub/Before/After` に起動を任せる。既存 `BuildContentDirectoryIntegrationTests.GetBootstrapState` と同じ read-only reflection 方針で `AppInitializer.s_instance`、基底 `_sceneDirector` / `_updateSystemHost`、派生 `_cameraSystem` / `_cameraSystemHost` を読み、非null・生存と `UpdateSystemRuntime.Coordinator` がその app host の Coordinator と同一であることを確認する。private field の書換え、`Install/BindSceneDirector/ActivatePendingRegistrations` の test からの呼出しはしない。
3. `SceneDirector` の生成は bootstrap が hostへbindする同期区間にある。fixture は SceneResourceMap 未登録なので `RegisterAlreadyLoadedScenes` によりスキップされ、HLOD用論理Sceneを作らない。通常の登録を受理する安定状態を待つ。起動失敗・所定の試験timeoutは基盤不成立として記録し、例外ログを無視してfixtureを進めたり代替hostを作ったりしない。待機はフレーム/条件で行う。
4. 読み取った `CameraSystemHost.Root` 直下の `View_Main` を1件に特定し、その実 Camera を借りる。app の `ICameraSystem.MainView.AddModifier` に test専用 `ICameraPoseModifier` を1件追加し、戻り値 handle をtestが所有する。modifierは固定 position / rotation / lens のみを返し、Cameraのtransformを別Updateから直接書かない。既存 `CameraSystemUpdateElement.OnElementLateUpdate` が Brain→Modifier→Snapshot を確定し、`CameraView.Tick` が実Cameraへ反映する。これによりHLOD applyより前にposeを確定できる。新Camera、論理Camera、追加View、AudioListenerは作らない。
5. authored初期表示を確認してから、disabledのpilotへ借用Cameraを内部設定口で渡してenableする。pilotが既存 `UpdateSystemRuntime.RegisterElement` を呼び、既存driverが次の安定フレームでactivationを行うことを `OnElementStart` と最初のapply到達で確認する。testは Coordinator のRun/Applyを手動駆動しない。登録待ちから main-camera boundary を記録し、初回far要求でも最初の成功applyまではDetailを保つ。pose sequenceはmodifierの入力だけを次フレーム前に変更し、同じapp Coordinator / Camera参照が保たれたことも観測する。
6. `UnityTearDown` / finally は途中失敗も対象に、pilotをdisableしてno-op化・Unregister・表示復元し、通常driverのstructural phaseが通った後に登録/ownership残存と再callbackがないことを確認する。testのframe購読とmodifier handleをDisposeする。captureで一時変更したcamera targetTexture等は元値へ戻してからtest所有bufferをRelease/Destroyし、fixture所有objectだけを回収する。借用host / MainView / CameraはDispose・Destroy・Uninstallしない。borrow中に既存hostが失われた場合は失敗として記録し、再生成しない。
7. ExitPlayMode で実アプリ自身の終了処理にhost等の解放を任せ、Editor Scene setup / playModeStartScene / process環境変数を必ず復元する。再入場は再び実bootstrapを通し、旧fixtureの所有権やcallbackが残らないことを確認する。テストが既存AppのRelease系private methodを呼ぶ経路は設けない。

この経路はソース上で成立する既存入口を選んだ設計であり、今回のexecutorで疎通済みではない。必要なAddressables互換bootstrap入力がrunnerで解決しない場合も、HLOD制御欠陥とは区別して§5末尾の基盤不成立手順へ戻す。bootstrapの新実装・fake置換をその場で足さない。

### 操作・画像の経路（現在は未成立）

| 条件 | 担当・初期状態・操作案 | 合否と保存する証拠 |
|---|---|---|
| M1 / M4 | C担当（未選定）が承認済み Unity 6000.6.0f1 executor で test fixture を開き登録/終了試験 | XML + raw log + manifest dump。全取得の拒否時無変更・cleanup を確認 |
| M2 / M3 | C担当が同じ head、URP、借用 View_Main とtest所有modifierで、実アプリ Updater の各 phase を通し、近遠/帯域内位置へ動かす。frame event で境界を観測 | frame番号・camera pose・選択表現・Renderer集合、Collider raycast / sentinel の結果。Unity state assertion が制御契約の主証拠 |
| M2 の表示確認 | 同じ担当が test 内の最小 capture 支援で near / proxy / 復帰と切替前後の代表画像を保存。解像度・camera pose・frame番号を固定して記録 | 実際に選択表現が描かれること、明白な全消失・重複がないことを画像で確認。C/C' がコピー先で原画像を開けることを確認。意匠やpopの製品採用評価はしない |

画像取得支援は新規であり、既存 `Camera.Render` / ReadPixels / screenshot の成立実績はない。§3の frame test にのみ含める限定実装として、URP の当該 camera の描画完了後に実描画 target を読み保存する案。新 RenderFeature / pipeline 改変 / runtime capture service は作らない。API の詳細は既存 URP の supported route を確認し、必要なら A2/A3 で範囲を修正する。setter 状態から画像を合成して実描画証拠にしてはならない。

**2026-10-04 の経路確認:** この cloud executor に Unity / 対象 Editor は存在しない。利用可能な既存 CI は `.github/workflows/debugstudio.yml` の .NET 検証のみで、Unity install/license、EditMode、Play 往復、capture を実行しない。ユーザー computer は接続されていても task authorization がなく、saved coding environment も現在ない。接続だけで実行可能と見なさない。

未確認。初回の Unity test / frame / capture 経路確認は **Phase C 担当**へ渡す案であり、A3 では (a) 使用許可済み executor または発注者指定の既存 runner、(b) 担当、(c) fixture の最初の実フレーム試験時を確認地点とすること、(d) 不成立なら実装欠陥と基盤不備を区別し、権限内の最小基盤整備か問いの分離へ戻すこと、を明示合意する必要がある。現時点ではこの案も未承認。人間の手動移動/撮影を暗黙のfallbackにしない。

保存は `tools/run-tests.ps1` が返す当該 run の TestResults 配下に raw log / XML、画像・frame記録を隣接させる案。固定 base/head、生成時刻、hash、保持期限を evidence 台帳へ記録し、C / C' の両方が同じコピーを取得・展開・閲覧できる経路を A3 で決める。生画像を無条件に製品 branchへコミットしない。転送経路未確認は証拠受渡し成功としない。

人間に美観の合否を依頼する条件: **本 pilot には設けない。** 見えることと排他性の画像確認は C/C' の担当が行う案。画像の独立再評価ができる証拠形式の受け入れ自体は、人間 A3 で合意する。製品の見た目・performance の基準をこの小 fixture のGOへ追加しない。

### レビュー・採否欄

- A0/A1 主担当・モデル・ベンダー: 主担当エージェント / 実行モデル識別子は未取得（推測で記入しない） / OpenAI
- A2 独立レビュー: モデル指定 `gpt-6-astra` / OpenAI の担当が r1 architecture レビューと同じ指摘の r2 再確認を実施。別セッション・モデル指定 `gpt-6-sol` / OpenAI の担当が同じ r2 固定入力を独立に受け入れ境界・失敗経路観点で確認。記載したモデル名は起動時の指定で、実行側のモデルIDは未検証。いずれも読取のみ、Unity実行なし
- A2 finding と採用/不採用/保留理由: **採用（M2/M4/M5の検証経路）**。r1のtest所有hostは実AppInitializerのglobal host / CameraSystemと競合し、SceneDirector未bindでactivationも成立しない。r2では§5の実アプリhost/Camera借用、通常registration/activation、test所有handleのみcleanupへ変更。runtimeの3責務分離・依存・表示ownerの境界は維持。r2のarchitecture再確認で当該指摘は設計/ソース上解消。受け入れ境界レビューでは追加blockerなし、M3を明確にするproxy配下Collider禁止・不在検査の提案を採用した。Unity実行/capture/evidence経路は未成立のまま。これらは主担当の採用案であり人間A3承認ではない
- A3 統合担当・人間の採否: 未実施。既存の着手依頼を、本書で初めて提示した条件の承認へ読み替えない
- C' 予約担当・モデル・ベンダー: 未選定。B/Cと異なるモデル・新規session・blind bundleを守り、可能ならA未関与の系列を残す
- 独立性の強化条件を満たせない場合: 実際に判明した段階で理由・残存リスクを記録し、人間判断を得る

## 6. Phase B 実装結果

未着手。実装、HANDOFFとの差、未実行事項、implementation head、担当・モデルは実施時に記録する。A1 文書作成は実装完了ではない。

## 7. Phase C

未実施

## 8. Phase C'

未実施

## 9. Phase D

未実施。C/C'突合、マージ判断、実証済み契約のharvest、本HANDOFF削除を別々に確認する。
