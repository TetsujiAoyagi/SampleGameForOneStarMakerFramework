# HLOD resident pilot — 条件付き・未承認の候補

## 0. メタデータと着手条件

- type: `slice`（将来 Gate 2 の設計種。現時点の実装スライスは未承認）
- status: **UNAPPROVED。実装 A3 は未凍結、B / C / C' / 実装 D は未実施。**
- program: [HLOD_PROGRAM.md](HLOD_PROGRAM.md) の Gate 2。Gate 0 の問題観測と Gate 1 の最小代替比較で必要性が残った場合だけ、新しい A1 / A2 / 人間 A3 を行う。
- branch / PR: 計画文書 PR #91、base `develop`。将来の実装 branch / base / head は未選定・未生成。
- risk: 将来の表示 ownership / frame boundary の実装は `high`。今回の3文書修正は文書のみ。
- owner: 統合担当 root（candidate の維持・証拠保持）、発注者（将来の A3 / D 判断）。将来 B / C / C' 担当は未選定。
- created: 2026-10-04
- expires / harvest 期限: **2026-11-05 または候補を置き換える revision**。root が program と同時に見直す。
- harvest to: 現在の計画状態は `docs/README.md`。将来、実証された表示契約のみ `unity/Assets/Docs/Architecture/24-rendering-system.md` へ反映する。文書 PR の D では candidate を保持し、解決・置換時に削除する。
- 実装 Phase A snapshot / B result / evidence bundle / blind bundle: 未生成。過去の A1 / A2 入力は §6 の candidate 履歴であり、新しい実装 A3 ではない。
- H1 / H2 / `external-current-v1`: 未適用。将来の適用判断を先取りしない。

**本書の具体値、型・ファイル案、試験経路、条件 M1〜M5 はすべて未承認の候補である。** 文書修正の承認や fixture の成功から、pilot の実装、非同期ロード、detail 解放、hierarchy、baker の開始を自動的に導かない。Gate 0 / 1 で program が閉じれば本 candidate も終了する。

## 1. 候補が答える問いと境界

全アセット常駐の小 fixture で、2 detail 群と両方を覆う1手製 proxy を、物理・gameplay の寿命を変えず、hysteresis 付きの排他的表示として切り替えられるか。これは Gate 1 で残った問題が複数群の coverage 関係を必要とする場合の**制御実証候補**であり、遠景を作る既定解ではない。

入力として保持する責務境界:

- Scene parent は寿命・依存関係であり HLOD tree ではない。Content Directory の同一 Scene identity に対する別 representation 同時ロード / deferred activation の拒否を変えない。
- Cell の Renderer / Collider は同居する。`CellScene` は bounds の運搬に留め、距離 policy を追加しない。companion の役割とロード追随を変更しない。
- `RenderEnvironment` の最小 lease / sink は実装済み。RenderWorld / BRG / HLOD は未実装であり、候補実証の前提にしない。
- UpdateSystemRuntime の既存順序は `ActivatePendingRegistrations → RunUpdate → RunLateUpdate → ApplyMainThreadChanges → ApplyStructuralChanges`。候補は既存登録 / apply を使う案で、独自 PlayerLoop を足す承認ではない。
- 依存は Game → Framework。`SampleGame.InGame` の既存 Runtime / Foundation 参照と `SampleGame.Tests` の InGame 参照・既存 IVT の範囲を使う案。asmdef edge の追加は新 A3 で判断する。

候補の対象外: 非同期 asset load / release、Scene / Variant / SceneState の変更、production Cell / SceneMap / AppInitializer / GameSceneFactory、companion 全体の移行、cross-fade、自動簡略化、階層、baker、RenderWorld / BRG、動的 / skinned / transparent renderer、複数 camera、製品品質・性能採用。後続の各問いは program の独立 Gate を所有する。

## 2. 最低条件の候補（新 A3 で選定・凍結する）

- **M1: 明示対応関係。** 2 detail group の ID / MeshRenderer membership / world bounds、proxy の coverage IDs / membership / bounds、閾値を宣言する。null、空集合、重複 Renderer、detail / proxy 跨ぎ、coverage の不足・余分、無効 bounds / 閾値を表示変更前に拒否する。数値包含と、proxy が代表形であるという画像上の対応を区別する。
- **M2: coverage 全体の切替え。** 有効 fixture の初期は detail のみ。main-camera rendering boundary ごとに「全 detail on / 全 proxy off」または逆を成立させ、dead band は直前の表示を保持する。境界往復・帯域内の揺れ・遠近直接移動で観測する。具体的な frame / Camera 契約は §5 を新 A3 で確認してから凍結する。
- **M3: 描画と寿命の独立。** 書込みは登録 MeshRenderer.enabled のみ。GameObject.SetActive、Collider.enabled、gameplay component の停止、Scene load / unload、asset API を使わない。同居 Collider の enabled / raycast と gameplay sentinel の継続を state で検証する。proxy は visual-only、配下 Collider は0件（primitive の自動 Collider も除去・検査）。
- **M4: 取得・返却と競合拒否。** 既存表示 owner や LODGroup / Animator 等の競合 writer、無効 manifest、Updater 未初期化なら無変更で登録拒否し、部分取得を rollback する。取得した owner だけが対象 enabled を書く。登録取得時に authored enabled 状態を対象ごとに保存し、disable / destroy で**生存対象の元の値を正確に復元**して ownership / Updater 登録を解放する。共通の初期値を再設定して復元の代用にしない。部分 cleanup は他 owner の項目を消さず、最後の owner 終了時には表も空とする。全表 clear / domain reload で漏れを隠さない。二重 cleanup、登録失敗後 cleanup、再登録、遅延 callback の no-op を検証する。
- **M5: 同一 head の証拠。** 新 A3 が定める最終全 EditMode、実 frame state、main-camera render 後の実画像、機械検査を固定 base / head に対応付ける。state の合格は画像の代用ではなく、画像は全 frame 状態・lifetime / unload の代用ではない。

将来これらを凍結した場合の判定案は、最低条件が揃えば限定制御実証の GO、不足・違反なら NO-GO / 未判定。CONDITIONAL ACCEPT は候補に設けない。fixture の GO は本番採用や製品 look / performance / memory 改善を示さない。次の機能にはその機能の観測した必要性と新 A1 / A2 / A3 を求める。候補段階の条件を、現在の文書 PR の Unity 実行ゲートにしない。

## 3. 手製 manifest と表示契約の設計種

外部向け汎用 asset schema を作らず、fixture component の serialized fields に宣言する案。以下は**未承認の制作例**であり、既存 Cell データでも実装済み fixture でもない。

- detail A: ID `detail-a`、`DetailA/Base` / `DetailA/Marker`。bounds center `(-5,2,0)`、size `(10,4,8)`。
- detail B: ID `detail-b`、`DetailB/Base` / `DetailB/Marker`。bounds center `(5,2,0)`、size `(10,4,8)`。
- proxy: `Proxy/Mesh` 1件、coverage IDs は厳密に `{detail-a, detail-b}`。bounds center `(0,2,0)`、size `(20,4,8)`、Collider 0件。
- 距離例: camera world position から集約 AABB への最短距離、near `20` / far `30`。finite かつ `0 <= near < far`。
- authored 表示例: detail true / proxy false、GameObject は active、pilot component は camera 注入前 disabled。static opaque geometry、shadow casting Off、表示の競合 writer なし。
- owner / Camera 案: `ResidentHlodPilot` component が実 AppInitializer の `View_Main` Camera を登録前に直接受け取る。fixture に Camera / AudioListener / host を追加せず、毎 frame の Camera.main 探索をしない。具体的借用 owner と経路は後続 A3 で確認する。

bounds 候補述語は、宣言 bounds と Renderer.bounds の center / size / 算出 min / max が全成分 finite、size 各軸が0以上かつ少なくとも1軸が正。NaN / Infinity、負サイズ、全軸ゼロを拒否し、平面・線状の非空 AABB を許す。包含は各軸 `outer.min <= inner.min && inner.max <= outer.max`（境界一致を許容）、epsilon / clamp / 絶対値補正なし。detail bounds は Renderer.bounds を、coverage bounds は両 detail bounds を含む。既存共通 validator の保証という主張ではない。

手置き2群と1 mesh の代表形を使い、実行中の fixture root の transform は固定する案。階層探索による全 Renderer / Cell の自動収集、汎用 writer arbitration を作らない。競合を拒否できる対象・方法と authoring 検査を新 A3 で決め、未知の writer も調停できると主張しない。

登録 / 終了・切替えの候補:

1. 有効登録まで authored 表示を保持。全入力検証→ownership 一括取得→Updater 登録。失敗は部分取得を戻し、表示を変えない。
2. Detail から距離 `>= far` で Proxy、Proxy から `<= near` で Detail、帯域内は保持。登録時 camera 不在は拒否、運用中の不在は直前有効表示を保持する案。
3. `OnElementLateUpdate` は apply 要求、Unity I/O と pure policy は `ApplyMainThread` に置く案。camera pose が確定した後、main-camera culling / render 前の1回の同期呼出しで集合を変更する。await / yield / render を挟まない。setter 個別瞬間を atomic と称さない。
4. 終了を先に確定して古い callback を no-op にし、Unregister、authored enabled 復元、取得項目解放を行う。破棄済み Unity object は `== null` で除外。個別 Renderer の途中破棄の汎用復旧は候補外。

表示 ownership は登録 scope の借用で、AssetOwner.Bind や asset lifetime と同じものではない。将来資産を取得する問いは `IAssetManagement` と明示 `AssetOwner` で別に設計する。

## 4. 責務配置の候補と停止境界

具体的ファイル・公開面を採用するには新 A3 が必要。候補 Runtime は Game 側 `SampleGame.InGame.InGameSession/World/Hlod` に置き、Framework owner / API をここでは新設しない。

- pure selection policy（`ResidentHlodPolicy.cs` 案、50〜90行）: 値と前状態から表現を決める internal 型。Unity object / Updater / asset API に非依存、state は owner が持つ。
- Game-owned display I/O（`ResidentHlodDisplay.cs` 案、160〜230行）: 明示 MeshRenderer の ownership 取得・衝突拒否・反映・復元。display instance は登録 scope 所有。private static の Renderer→display 表は managed domain と同寿命、項目は取得から rollback / cleanup まで（activation 待ちを含む）。生存 owner の取得状態を全表 reset で消さない。実 Renderer の EditMode 試験を候補にする。
- owner / orchestration（`ResidentHlodPilot.cs` 案、150〜220行）: manifest / camera を policy と display へ結び、Updater 参加・終了を調停。MonoBehaviour と同寿命、asset 所有なし。必要最小限の public authoring 型と内部試験口を検討する。
- policy / display / frame tests と隔離 fixture: 既存 Editor-only `SampleGame.Tests` と IVT の範囲を使う案。frame test は app host / MainView / Camera を借り、fixture / modifier handle / 観測購読 / capture buffer / 一時設定の復元のみを所有する。production SceneMap / Build Settings を変更しない。

現在の実装行数は全候補0。policy / display / orchestration は変更理由・依存・テスト方法が異なるため分ける案。表示の取得・反映・返却は同じ契約に閉じる。500行、独立3責務、追加 asmdef edge、新しい状態 / owner / lifetime / public API、既存 Scene / bootstrap 改変が必要なら新しい設計判断として Phase A に返す。行数合わせの Helper / Manager を足さない。

Unity C# の `#nullable enable`、record 禁止、偽 null、公開 `ILogger<T>`、Runtime / Editor 隔離、SceneState 14値と SceneLifecycleManager 所有、UpdateSystemRuntime の順序・例外分離を守る。テストで Task.Delay / Thread.Sleep を使わない。候補の authoring も到達可能な Editor 経由とし、Scene YAML 直書きで代替しない。

## 5. 将来の検証案と未確認の経路

ローカルでは必要な Unity Editor 操作が可能。ただし**この candidate の app / Camera / render capture / 受渡しは未確認**であり、接続や workstation の存在だけを実行成功としない。後続 A1 / A2 で小さな feasibility 確認を行い、新 A3 は Unity 6000.6.0f1 の executor / 担当、初期状態、capture API / readback 境界、最初の確認地点、証拠受渡し、不成立時の分類を選定・凍結する。人間の手動移動・撮影は暗黙の fallback ではない。

保持する試験の設計種:

- pure policy: near / far 境界、dead band、直接移動・反復、非有限 / 負値 / 逆順閾値。
- display / owner: 2→1、membership / bounds / coverage の無変更拒否、競合 writer / owner、Updater 不在、activation 待ちの終了、二重 cleanup / 再登録、authored enabled の対象別復元、他 owner 生存中の部分 cleanup と最後の表空、Collider raycast / sentinel、proxy Collider 不在。
- frame state: authored 初期→near→far→dead band→near を通常 Updater で進め、main-camera rendering boundary の排他集合を記録。終了・再入場の ownership / callback 残存を検査。
- 実画像: 同じ camera の render 完了後の near / proxy / 復帰・切替前後を保存。pose / frame / 解像度を固定・記録し、選択表現が描かれ明白な全消失・重複がないかを C / C' が原画像で評価できる形にする。setter state から合成した画像を描画証拠にしない。

過去 r2 の host 借用案を設計入力として保持する。EnterPlayMode では実 AppInitializer が host / CameraSystem を作るため、test が別 UpdateSystemHost を作らない。read-only reflection で実 bootstrap の SceneDirector / Updater / Camera を観測し、Coordinator の一致を確認する案。private field 書換え・Install / Bind / Activate 手動駆動・fake host 置換を行わない。

CameraSystemHost.Root の `View_Main` を一件に特定し、test 所有 ICameraPoseModifier handle を MainView.AddModifier で借用する案。通常の Camera Layer の pose 確定後、HLOD apply 前後から main-camera render まで camera が動かないことを観測する。fixture に host / Camera / AudioListener を追加しない。capture の Camera.Render / ReadPixels 等の具体的 supported route は未選定・未疎通で、新 RenderFeature / pipeline 改変 / runtime capture service の承認ではない。

途中失敗も finally / UnityTearDown で pilot 終了・authored 状態復元、frame 購読 / modifier handle 解放、camera targetTexture 等の復元後に test buffer 回収を行う案。借用 host / View / Camera を Dispose / Destroy / Uninstall せず、ExitPlayMode の app 終了に任せる。Editor Scene setup / playModeStartScene と一時 process 環境設定の元値を保存・復元し、User / Machine 設定を変えない。bootstrap 入力・Content Directory 起動の失敗は基盤不成立として扱い、その場で別 host を実装しない。

将来の差し戻し起点案は `pwsh tools/run-tests.ps1 -Filter SampleGame.Tests.Rendering.ResidentHlod`、実描画を含むときは `-WithGraphics`。判定案は GO 候補 head の `pwsh tools/run-tests.ps1 -WithGraphics`（空 filter、最終全 EditMode、同条件の frame tests を集約）。XML の実名・件数・失敗・skip を確認し、0件 / 必須 Ignore を成功にしない。Editor-only asmdef の EnterPlayMode / ExitPlayMode 案であり、PlayMode platform の採用ではない。将来 pilot に全 EditMode 免除はない。担当と実行許可は新 A3 で確定し、Unity テスト / build は標準 runner と Phase C の責任分担を守る。

raw log / XML と画像・frame 記録は同 run に対応させ、base / head / 時刻 / hash / 保持期限付きで C と blind C' が同じコピーを取得・展開・閲覧できる経路を決める。画像を無条件に product branch へコミットしない。state、画像、asset lifetime 証拠の意味を混同せず、全常駐 fixture から memory 削減を主張しない。製品の美観・性能予算は別 Phase A が所有する。

## 6. 過去の候補レビューと現在の承認境界

過去 A1 r1〜r3 は pilot の設計候補を検討した履歴である。r1 / r2 A2 ではモデル指定 `gpt-6-astra` が architecture、別 session の `gpt-6-sol` が受入境界・失敗経路を確認した。指定モデルであり、実行側 ID は未検証。いずれも Unity 実行なし。

r1 の test 所有 host は実 AppInitializer と競合するため、r2 で app host / Camera 借用と通常 activation・test handle のみ cleanup へ変更する案を採った。proxy Collider 不在も追加した。r3 は [ownership 表の保持者・項目寿命](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/91#discussion_r4176050040) と [bounds 述語](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/91#discussion_r4176050042) を明確化し、全表 reset で cleanup 不良を隠す案を採らなかった。これらは有用な設計入力であり、実装・疎通成功・人間 A3 の証拠ではない。

今回 PR #91 の文書修正は、Gate 0 / Gate 1 を先行させ、pilot を不要と判断できる終了と条件付き Gate 2 に揃える A3 だけを凍結した。旧候補の exact owner / Camera / capture / asset lifetime を承認していない。Gate 2 の必要性が生じた時点で新しい A1 / A2 / A3 と固定 evidence を用意し、担当・モデルと独立性を実績として記録する。

## 7. Phase C（将来 pilot）

未実施

## 8. Phase C'（将来 pilot）

未実施

## 9. Phase D と candidate の寿命

将来 pilot の実装 D は未実施。今回の文書 D では program / candidate を進行中の作業台として保持する。解決・不要判断・新 revision による置換時だけ、真である現況・実証済み契約を harvest して本書を削除する。
