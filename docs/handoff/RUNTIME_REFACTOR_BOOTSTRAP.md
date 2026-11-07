# AbstractApplicationInitializer の責務分解（旧リモートカタログ・UICommon・常駐サービス）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `A`（**A3 凍結**。2026-11-07 に owner が凍結を指示した。Phase B は本書を含む文書 PR の `develop` へのマージ後に始める）
- **A1 改訂の記録（2026-11-07）:** program の A0 [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §4.2 の BOOTSTRAP 向けの表を当てた。§6 D4（owner 判断）に従い、S2（Content Directory 起動の facade）と、それだけに属する AC9 / AC10・U1 / U2・責務マップの行・実装手順 1 を独立スライス `CONTENT_DIRECTORY_STARTUP` へ移し、素材は A0 §4.2 の §3.2 / §2.1 S2 / §2.3 の行を当てた形で [RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md) に置いた（正本は同書だけ）。本書は S3〜S5 と、それに伴う `InitializeAfterSceneLoad` の分割だけを扱う。番号は A0 や他文書からの参照を保つため振り直さず、移した S2・S6・AC9・AC10・U1・U2 は欠番にした。分割の判断は §2.6
- branch: A1 初稿は計画ブランチ `claude/runtime-refactor-stage0-plan`（PR #161 で `develop` へマージ済み）。A1 改訂は `claude/runtime-refactor-bootstrap-a1-d230c4`。実装は Phase B 開始時に `develop` から専用ブランチを切る（名前は B 担当が `<prefix>/runtime-refactor-bootstrap` で決める）
- implementation base commit: `d4861882`（A1 初稿は `840b6609`（PR #149 のマージ）で測り、2026-11-07 に再固定した。間に initializer を変えたのは BS5a（#150）と BS5b-2（#156）で、§1 と §3.5 は `d4861882` の実測に取り直してある）。A1 改訂の時点（2026-11-07）の `develop` の head `7c9b364d` は、`unity/Assets/OneStarMaker/` と `unity/Assets/SampleGame/` で `d4861882` との差が 0 なので、取り直していない。B 開始時に `develop` の head で再固定する。順序: 本スライスは A0 §6 D4 の列（ASSET_CYCLE → `BUILDSYSTEM_STANDALONE_ENTRY` → `CONTENT_DIRECTORY_STARTUP` → asmdef）の外にあり、先行条件を持たない（ASSET_CYCLE は Bootstrap folder を触らず namespace も変えない）。ただし `CONTENT_DIRECTORY_STARTUP` と同じ `AbstractApplicationInitializer.cs` を触るので、両者の Phase B を同時に走らせない。後に B へ入る側が base を取り直す
- implementation head commit: 未固定（Phase B で記録）
- risk: `high`（アプリ起動・終了の順序、失敗経路、Player の receipt に現れる stage 名、派生クラスの公開面に触れる。実行時挙動は変えないが、変えていないことの証明が主な作業になる）
- owner: root（依頼者）。A2 / B / C / C' の担当は Phase ごとに選ぶ
- created: 2026-10-31
- expires: 2026-11-30 または置換 revision の成立時。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/04-app-startup.md` §4.2（クラス構造）と §4.3（解放の保証）。§4.7「Content Directory の起動時選択」の契約文は変えない（実装の置き場所だけが変わる）。新しい Architecture 文書は作らない
- 関連スライス: [RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)（本書から切り出した S2。順序は上の implementation base の行）。[RUNTIME_REFACTOR_ASSET_CYCLE.md](RUNTIME_REFACTOR_ASSET_CYCLE.md) は先行条件ではない（namespace を据え置くので、本書が前提にする `using` の変化は無い）。S-4 の d5a は `CreateVfxBackend` hook と `VfxRuntimeHost` 配線を足した。残っていた d5b は PR #157 で完了し、initializer を触らなかった（§2.5 U4）
- Phase A snapshot の取得案内・状態: A2 入力版は commit `7b2cf205` の本書（§5「A2 独立レビュー」に content sha256）。A3 凍結版は、この票を含む commit の本書（SHA は本書に書かず起動指示で渡す）。本スライスは文書だけで、凍結する script・機械向け期待値を持たないので、Artifact Storage の snapshot は作らない（凍結の正は commit）。A3 途中版を外部レビューへ渡した経路は owner の手元で、取得案内は無い
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase B（実装）。新しい session で始める。
  - 次の作業: 本書を含む文書 PR が `develop` へマージされた後、`develop` の head で implementation base を再固定し（§1 と §3.5 の行番号・公開面を取り直す。差があれば §1 を直してから進める）、専用ブランチを切って §4 の順序で実装する。最初に AC15 の harness が `OneStarMaker.Tests` の型だけで組めるかを確かめる。§6 に結果（AC3 の 4 つの表、AC6 の分類、Content Directory の状態に触れるメソッドの一覧）を書き、B の出口で commit する。
  - 引き継ぐ commit: この票を含む commit（A3 凍結版。SHA は起動指示で渡す）。Phase B の実装の起点は、そのマージ後の `develop`。
  - 必要な権限: ローカルの Unity Editor の起動とコンパイル確認（`osm-unity-editor` Skill に従う）。Unity テストの実行・判定は Phase C。資格情報は不要。
  - 停止規則: §4 の「Phase B から Phase A へ差し戻す条件」に当たったら止めて返す。`CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。`CONTENT_DIRECTORY_STARTUP` へ移した範囲（facade・Content Directory の判定表）を本書へ戻す必要が出たら、本書で裁かず owner に返す（D4 の変更になる）。判定 C の実行や C' の起動を担わない。
  - 引き渡さないもの: A2 レビュー担当の生の回答（主担当の作業領域にだけあり、採否は §5 の表が正）。

### PR 状態ブロック

```text
状態: Phase A の 完了（A3 凍結）
Phase A: revision 0、A2 入力版 commit 7b2cf205、A3 凍結版 commit <起動指示で渡す>
implementation base / head: d4861882（B 開始時に develop の head で再固定） / 未固定
引き継ぐ commit: <A3 凍結版の commit。起動指示で渡す>
次の担当: Phase B。次の作業: 文書 PR のマージ後、§4 の順序で実装
HANDOFF: docs/handoff/RUNTIME_REFACTOR_BOOTSTRAP.md
Evidence 取得案内: 未生成
未決事項: なし（U1・U2 は CONTENT_DIRECTORY_STARTUP へ移し、U3 は D4 で決定、U4 は事実で解消、U5 は A3 で閉じた）
```

## 1. 目的と対象外

- 目的: `Runtime/Bootstrap/AbstractApplicationInitializer.cs`（1,303 行、1 クラス）から、起動契約（3 フェーズの順序、失敗境界、派生クラス向け hook）と変更理由の異なる 3 つの責務（旧リモートカタログの案内経路、UICommon の取得、Framework 常駐サービスの集合）をそれぞれのクラスへ移し、最長メソッド `InitializeAfterSceneLoad` を順序が読める長さに分ける。移した結果として、Bootstrap folder から DebugSocketServices の internal 型への参照を 0 にする。Content Directory の起動は initializer に残し、独立スライス `CONTENT_DIRECTORY_STARTUP` が facade へ移す。
- 目的の改訂（A1 改訂、2026-11-07）:
  - 元の文言: 「`Runtime/Bootstrap/AbstractApplicationInitializer.cs`（1,303 行、1 クラス）から、3 フェーズの起動順序と派生クラス向け hook の template だけを残し、変更理由の異なる 4 つの責務（Content Directory の起動、旧リモートカタログの案内経路、UICommon の取得、Framework 常駐サービスの集合）をそれぞれのクラスへ移す。移した結果として、Bootstrap folder から BuildContent / Config / DebugSocketServices の internal 型への参照を 0 にし、Content Directory の起動は BuildContent 側の public な 1 型だけを通る形にする。これは Bootstrap を独立 assembly にする後続スライスの前提である。」
  - 変更理由: program A0 §6 D4 で、Content Directory 起動（旧 S2）は AssetManagement / BuildSystem の単独利用 program の列（`BUILDSYSTEM_STANDALONE_ENTRY` の後）に入り、独立スライスになった。残る 3 責務は単独利用の成立に関係しないので、その列の外で進める。Config の internal（`RequiredJsonFileConfigProvider`）は `BuildConfig` に残り、facade と一緒に扱うかを `CONTENT_DIRECTORY_STARTUP` の U2 で決めるので、本書の目的から外した。「Bootstrap を独立 assembly にする前提」は、BuildContent の越境を `CONTENT_DIRECTORY_STARTUP` が片付けるまで成り立たないので、本書の目的には書かない。
  - 承認: owner の D4 判断（2026-11-07、program A0 §6）。利用者の入力は増やしていない（本スライスに利用者の操作は無い）。
- 利用者: なし（Framework 内部の構造変更。派生クラスの作者から見た公開面は変えない）。
- 対象外（所有する後続）:
  - Content Directory 起動（構成の解決、Addressables の封じ、session の登録・install・close、Play 停止時の完了、`BuildConfig` 内の `PlayerBakedContentConfiguration` 生成）と、BuildContent の internal 型・`AssetManagement` の directory 用 internal メソッド 4 つへの参照の解消: [RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)。本スライスはこれらのコードの意味を変えず、initializer の外へ出さない（AC5）。`InitializeAfterSceneLoad` の分割で private メソッドへ移るものは、同じファイルの中に留まる
  - protected / internal の公開面（hook、accessor、nested 型、static 入口）の追加・削除・signature 変更: 本スライスでは一切行わない。必要なら Phase A を再開する
  - 起動順序・終了順序・失敗経路・stage 名の変更。性能改善。旧リモートカタログ経路の削除（通常起動から外れているが、`20-variant-checkout-workflow.md` が案内として残している）
  - namespace `OneStarMaker.Runtime` → `OneStarMaker.Runtime.Bootstrap` への移動（`AppInitializer.cs` の `using` が変わるので SampleGame の変更になる）: Bootstrap を独立 assembly にするスライス
  - `Runtime/Config/RequiredJsonFileConfigProvider`（internal）の public 化や移動、`BuildConfig` の変更: 触らない。facade へ移すかは `CONTENT_DIRECTORY_STARTUP` の U2、assembly の扱いは asmdef スライスで決める
  - `Runtime/Rendering/Vfx/Implements/VfxRuntimeHost`（internal）と `ActiveVfxRuntimeHost`: S-4d program の所有。本スライスは呼び出し位置を `FrameworkServiceSet` の中へ動かすだけで、型・可視性・生成と破棄の順序を変えない。Bootstrap → Rendering の internal 参照 1 件は残り、asmdef Stage 2 の入力として記録する
  - `UseRequiredPlayerFileConfiguration`（BS4 Player）の protected-key 契約、fail-closed の条件、receipt の schema: 変えない
  - SampleGame の製品コード（`AppInitializer.cs` を含む）: 変更 0
  - `AbstractApplicationInitializer` の XML doc 冒頭の使用例と `04-app-startup.md` §4.5 の実装ルール: 変えない
- 現況（base `d4861882` の実測）:
  - 1,303 行、19 フィールド、3 つの protected static 入口（`BootstrapSubsystemRegistration` / `BootstrapBeforeSceneLoad` / `BootstrapAfterSceneLoad`）、6 つの protected accessor、23 の protected virtual / abstract hook、protected nested 型 2 つ（`PlayerContentStage`、`PlayerContentFixtureResult`）、protected static 1 つ（`AppendFixtureStages`）、internal static 4 つ（`ShouldNotifyPlayerContentFailure`、`RunsPlayerContentSmoke`、`ReadEditorFirstScene`、`ActiveVfxRuntimeHost`。中 2 つは BS5b-2 で追加）。一覧は §3.5。
  - 最長メソッド `InitializeAfterSceneLoad` は 219 行（294〜512 行目）。Framework 全体で最長。
  - **他 folder の internal 型への参照（Runtime 内で Bootstrap だけが持つ）:** BuildContent 9 型（`ContentDirectorySession`、`UnityContentDirectoryBackend`、`PlayerContentConfiguration`、`PlayerBakedContentConfiguration`、`ContentDeliveryFiles`、`ContentInstallReceipt`、`InstalledRevisionVerifier`、`VerifiedInstalledRevision`、`ContentDeclarationMismatch`（630・635 行目。A1 初稿の 8 型から漏れていたのを A2 で訂正））、Config 1 型（`RequiredJsonFileConfigProvider`）、DebugSocketServices 1 型（`DebugSocketTelemetrySink`）、Rendering 1 型（`VfxRuntimeHost`）。加えて `AssetManagement` の internal メソッド 4 つ（`SealAddressables`（246 行目）、`InstallContentDirectory`（605）、`CloseContentDirectoryAsync`（421、463）、`CompleteContentDirectoryPlayStop`（981））を、`_assetManagement` を具象 `AssetManagement.AssetManagement` へ確かめる 5 箇所（240、419、456、603、977 行目）を経て呼ぶ。`AssetManagement` のコンストラクタは public（235 行目）。
  - **変更理由の混在。** 同じクラスに (a) 3 フェーズの順序と hook（起動契約）、(b) `AppConfig` から Content Directory 構成を解く判定（1079〜1151 行目、`ReadContentDirectoryConfiguration` + nested `ContentDirectoryConfiguration`）、directory と決まった直後の Addressables の封じ（240〜249 行目、BS5a）、session の登録・close と宣言の不一致警告（594〜636 行目、`InstallConfiguredContentDirectoryAsync` + `WarnOnDeclarationMismatch`、BS5b-2）、435〜480 行目付近の失敗時 cleanup、ReleaseAll の 2 分岐）、(c) 旧リモート Addressables catalog の追加ロードと revision 乖離警告（673〜822 行目、5 メソッド。`TryLoadRemoteCatalogAsync` に呼び出しは無く、`InitializeAfterSceneLoad` 内のコメント「旧リモート Addressables catalog 追加ロードは通常起動から外した」が残る）、(d) UICommon Scene / prefab の取得 3 経路と EventSystem の生成（514〜592 行目）、(e) logger / DebugSocket / UpdateSystemHost / VfxRuntimeHost の生成・開始・破棄順（886〜924 行目、941〜1009 行目）が入っている。
  - **隠れた結合。** `SampleGame/Tests/Bootstrap/InvalidCompanionConfigStartupTests.cs` は reflection で private フィールド `_assetManagement` を差し替える（149 行目）。`SampleGame/Tests/Editor/Build/BuildContentDirectoryIntegrationTests.cs` は reflection で `_contentDirectorySession` と `_sceneDirector` を読む（449・451 行目）。`_sceneDirector` は VFX の Play テスト群（`VfxScenePlayTests`、`LocalVfxEventPlayTests.Scenes`、`SeasonMajorEventPlayTests.Scenes`）も読む（A1 初稿は `_assetManagement` だけとしていたのを A2 で訂正）。`OneStarMaker/Tests/Editor/TestObservation/ObservationReloadTests.cs` は `GetMethod("GetEnvironmentVariablePrefix")` で protected hook を読む（I1 で守られる）。`OneStarMaker/Tests/BuildContent/PlayerContentConfigurationTests.cs` は `ShouldNotifyPlayerContentFailure` と nested `PlayerContentStage` / `AppendFixtureStages` を派生 harness から使う。S-4d5a が足した SampleGame の Play テスト群は `ActiveVfxRuntimeHost` を読む。
  - **観測可能な契約。** AfterSceneLoad の stage 名は 16 個で、失敗時に `OnAfterSceneLoadInitializationFailed(stage, ex)` と `OnPlayerContentStartupFailedAsync(stage, …)` に渡り、BS4 Player の receipt に残る: `before-scene-load`、`register-content-directory`、`load-ui-common`、`wait-ui-common-ready`、`load-scene-resource-map`、`start-framework-services`、`initialize-app-services`、`create-scene-factory`、`resolve-scene-variant`、`create-scene-director`、`register-loaded-scenes`、`load-player-first-scene`、`run-player-content-smoke`、`unload-player-first-scene`、`close-player-content-directory`、`complete-player-content-smoke`。このほかに BS5b-2 が足した動的な stage `"load-editor-first-scene:" + firstScene`（Editor directory Play の初回 Scene。Player の receipt には現れない）が 1 つある。
  - **BeforeSceneLoad の逐次代入。** 常駐サービスは 1 つ作るたびにフィールドへ代入される（253・261・263・267 行目）。そのため (1) 後ろの hook（`CreateDebugSocketService`、`CreateLoggerFactory`、`CreateVfxBackend`、`RegisterDefaultTelemetrySink`）は前で作ったものを protected accessor（`UpdateCoordinator`、`LoggerFactory`、`Vfx`）で読める。(2) base の `RegisterDefaultTelemetrySink`（1275〜1282 行目）は `_loggerFactory` が null なら Warning を出して JSON sink を登録しない。(3) 途中の hook が投げると、`BootstrapBeforeSceneLoad` の catch の `ReleaseAll` が作り済みの UpdateSystemHost（ctor で PlayerLoop に install 済み）と DebugSocket を破棄する。
  - **AfterSceneLoad の早期 return と冒頭の検査。** 337 行目（`_assetManagement` か `_cts` が null）と 384 行目（`_sceneDirector` が null）は catch を通らずに return し、finally が失敗の telemetry（その時点の stage）を出す。`ReleaseAll` は呼ばない。冒頭の `_cts == null` は例外を投げて catch に入る（306〜307 行目）。
  - **stage の代入点。** `InitializeAfterSceneLoad` の `startupStage` への代入は 18 箇所（初期値 296 行目を含む。313、317、321、326、343、347、350、353、360、368、390、400、408、411、415、418、424 行目）。`load-ui-common` は初期値と 317 行目の 2 箇所、`register-content-directory` は非 required の先行登録（313）と required ではない後段の登録（360）の 2 箇所。`before-scene-load` は AfterSceneLoad の代入ではなく、`BootstrapAfterSceneLoad` が BeforeSceneLoad の失敗を Player へ通知するときの stage（201 行目）。AC2 の literal 16 個は、この 1 個と AfterSceneLoad の 15 個（動的な接頭辞を除く）。
  - **UICommon の所有（526〜592 行目）。** 3 経路で所有が違う。(1) active Scene が `UICommon` で root に UICommon がある経路は **借用**: そのまま返し、`_uiCommonObject` にも `_uiSceneHandle` にも入れず、`EnsureEventSystem` も呼ばない。(2) directory、(3) Addressables の経路は、scene handle を `_uiSceneHandle` に、root を `_uiCommonObject` に入れ（component の検査より前）、最後に `EnsureEventSystem` を呼ぶ。EventSystem は `EventSystem.current` が無く Play 中のときだけ作り、そのときだけ `_eventSystemObject` に入れる。`ReleaseAll` は `AssetManagement.ReleaseAll` の後で `_uiCommonObject` を `Destroy` し、`_uiSceneHandle` は参照を null にするだけ（`ISceneHandle` は `IDisposable` ではなく、scene の unload は AM の ReleaseAll に任せる）。
  - **`UseRequiredPlayerFileConfiguration` の評価時点。** protected virtual の getter で、UICommon の取得では active Scene の検査の後に初めて読まれる（545 行目）。
  - **現在の順序（不変条件の基準）。** BeforeSceneLoad: CTS 生成 → `Application.quitting` 購読 → `AssetManagement` 生成 → `BuildConfig`（required Player なら `PlayerBakedContentConfiguration` もここで生成） → `ReadContentDirectoryConfiguration` → （directory なら）`SealAddressables` と封じたことの `Debug.Log` → `CreateUpdateSystemHost` → `AppTelemetry.Thresholds` → `CreateDebugSocketService` → `LogDebugSocketBootstrapStatus` → `CreateLoggerFactory` → `VfxRuntimeHost.Create` → `ActiveVfxRuntimeHost` の設定 → `RegisterDefaultTelemetrySink`。AfterSceneLoad: （directory かつ非 required なら）session 登録 → UICommon 取得 → `WaitForPanelReadyAsync` → SceneResourceMap（Player / directory / Addressables の 3 経路） → `StartFrameworkServicesAsync` → `OnServicesInitializing` → `CreateSceneFactory` → `ResolveSceneVariant` → （directory かつ未登録なら）session 登録 → representation の上書き → `SceneDirector` 生成 → `BindSceneDirector` → `BindSceneLifecycle`（VFX） → `RegisterAlreadyLoadedScenes` → （firstScene があれば）required Player でないなら Editor の初回 Scene を `AddScene` して観測ログ 1 行、required Player なら Player content smoke の 6 段。session 登録の直後に宣言の不一致警告（`WarnOnDeclarationMismatch`）。ReleaseAll: `_cts.Cancel` → `DisposeVfx` → DebugSocket → UpdateSystemHost → LoggerFactory → CTS → `_bakedPlayerContent` / `_config` / `_contentDirectoryConfiguration` → `SceneDirector.Dispose` → `AssetManagement.ReleaseAll`（+ `CompleteContentDirectoryPlayStop` または `BeginSynchronousShutdown`） → map → UICommon GO → `_uiSceneHandle` → EventSystem GO → `AppTelemetry.Shutdown`。
  - 失敗経路: AfterSceneLoad の catch は、required Player で first scene がロード済みなら unload（Editor は ReleaseAll と Play の終了に任せる） → directory が登録済みなら close → `OnAfterSceneLoadInitializationFailed` → （required Player なら）`OnPlayerContentStartupFailedAsync` → `ReleaseAll` の順で、各段の例外は記録して元の例外を隠さない。`OperationCanceledException` は非 required のときだけ成功扱い。
  - 本書の範囲との対応: 上の (b) と、BuildContent 9 型・Config 1 型・`AssetManagement` の internal メソッド 4 つへの参照は `CONTENT_DIRECTORY_STARTUP` の範囲で、本スライスでは initializer に残る。本スライスが移すのは (c)〜(e) と DebugSocketServices 1 型への参照。Rendering 1 型（`VfxRuntimeHost`）への参照は `FrameworkServiceSet` へ移るが、Bootstrap folder の中に残る。
  - 設計文書: `04-app-startup.md` §4.2 のクラス構造図は現在のメソッド構成を前提にしている。§4.7 には「Editor 設定読取の未決」（`BuildConfig` が directory 判定より前に `JsonFileConfigProvider` で Addressables を読む）が記録されており、本スライスはこの未決を解決も正当化もしない。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: `AbstractApplicationInitializer` の起動・終了順序と派生クラス向け公開面を変えずに、旧リモートカタログ・UICommon 取得・Framework 常駐サービスを変更理由ごとのクラスへ分け、`InitializeAfterSceneLoad` を分割し、Bootstrap から DebugSocketServices の internal 型への参照を 0 にできるか。
- 判定定義: §2.1 の不変条件と成立条件を §2.2 の受け入れ条件で全て確認できれば GO。一つでも未達なら NO-GO。スパイクではないので CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし（A3 後に作る場合は本書と別の記録にする）

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I1 公開面不変:** §3.5 の一覧にある protected / internal メンバーの名前、signature、アクセス修飾子、nested の位置、`static` の有無を変えない。追加もしない。private フィールド `_assetManagement`、`_contentDirectorySession`、`_sceneDirector` の名前と型を変えない（SampleGame.Tests の reflection。§1）。
- **I2 起動順序不変:** §1 の「現在の順序」にある各処理の相対順序を変えない。stage 名 16 個の文字列、動的 stage の接頭辞 `load-editor-first-scene:`、各処理に対応する stage の割当を変えない。
- **I3 終了順序不変:** `ReleaseAll` の解放順（§1）を変えない。複数回呼んでも安全であることを保つ。`ActiveVfxRuntimeHost` は、自分の host と同じときだけ（`ReferenceEquals`）、host の Dispose より前に解除する。破棄の途中で例外が出たときの扱い（現行は try/catch が無く、例外は `ReleaseAll` の外へ出る）も変えず、移動先で try/catch を足さない。UICommon の借用経路の GameObject・EventSystem は破棄せず、scene handle の unload も足さない（§1「UICommon の所有」）。
- **I4 失敗経路不変:** 各失敗点で呼ばれる hook と、その前に行う cleanup の順序、例外の握り方（元の例外を隠さない、cleanup の例外は記録だけ）を変えない。`ContentDirectoryException` の `Code` とメッセージを変えない。`OperationCanceledException` の扱いを変えない。BeforeSceneLoad の途中失敗では、それまでに作った常駐サービスが全て `ReleaseAll` で破棄される。AfterSceneLoad の失敗時に hook・receipt・telemetry へ渡す stage は、失敗した処理に割り当てられた stage のまま（§1 の 16 個の割当）。337・384 行目の早期 return（catch を通らず、失敗 telemetry だけを出す）を残す。
- **I4a hook から見える状態不変:** BeforeSceneLoad で各 `Create*` の戻り値は、次の hook を呼ぶより前に protected accessor から見える（§1「BeforeSceneLoad の逐次代入」）。UICommon の取得で読む virtual（`UseRequiredPlayerFileConfiguration`、`GetDirectoryBootstrapSceneLogicalKey`、`GetUICommonPrefabAddress`）は現行と同じ時点・同じ分岐でだけ評価される。`UseRequiredPlayerFileConfiguration` は active Scene の検査の後、2 つの hook はロード済み UICommon の経路と required Player の拒否の経路では呼ばれない。getter が純粋であることには頼らない（派生クラスの実装を本スライスは知らない）。
- **I5 越境を広げない:** 変更後の `Runtime/Bootstrap/**` に DebugSocketServices の internal 型の識別子が現れない。BuildContent・Config の internal 型と `AssetManagement` の internal メソッド 4 つ（`SealAddressables`、`InstallContentDirectory`、`CloseContentDirectoryAsync`、`CompleteContentDirectoryPlayStop`）への参照は、`AbstractApplicationInitializer.cs` の外（新しい 3 クラス）へ出さない。`CONTENT_DIRECTORY_STARTUP` が 1 ファイルだけを見れば済むようにするため。Rendering の `VfxRuntimeHost` は対象外（§1）。
- **I6 境界不変:** `.asmdef` の変更 0 件。`unity/Assets/SampleGame/` の変更 0 件（Tests を含む）。新しい public 型・public メンバーは 0（`DebugSocketService.CreateTelemetrySink` は internal。A2 入力版の public を A3 で改めた）。
- **I7 Telemetry 不変:** AppStartup span は BeforeSceneLoad と AfterSceneLoad の 2 つのまま。payload の `stage`（成功時 `BeforeSceneLoad` / `AfterSceneLoad`、失敗時は到達 stage）を変えない。
- **I8 新しい静的状態を作らない:** `ActiveVfxRuntimeHost` 以外の static 可変状態を増やさない。service locator を作らない。

**成立条件（起きなければならないこと）**

- **S1 サイズ:** `AbstractApplicationInitializer.cs` が 1,080 行以下。`InitializeAfterSceneLoad` が 130 行以下。（A1 初稿の 900 / 110 行は、S2 を含め、`InitializeAfterSceneLoad` の抽出を 2 つと見た値。S2 の分を戻し、A2 の再見積り（抽出 2 つでは約 149 行残る）に合わせて抽出を 4 つにし、stage の保持者の分を足した。900 行以下は `CONTENT_DIRECTORY_STARTUP` の目標として同書へ移した。見積りは §3.3）
- **S2:** 欠番（Content Directory の facade。`CONTENT_DIRECTORY_STARTUP` へ移した）。
- **S3 旧リモートカタログの隔離:** `Runtime/Bootstrap/LegacyRemoteCatalogProbe.cs`（internal）に旧 5 メソッドが移り、initializer から消える。呼び出し経路の有無は現状のまま（増やさない・減らさない）。
- **S4 UICommon 取得の隔離:** `Runtime/Bootstrap/BootstrapUiCommonLoader.cs`（internal）が UICommon の 3 経路と EventSystem 生成を持ち、所有する経路（directory / Addressables）の UICommon の GameObject と、自分が作った EventSystem の GameObject の破棄を所有する。scene handle は参照を持つだけで、`Dispose` で参照を null にする。scene の解放は AssetManagement の `ReleaseAll` が担い、loader は unload しない。借用経路では何も所有しない（§1「UICommon の所有」）。
- **S5 常駐サービス集合:** `Runtime/Bootstrap/FrameworkServiceSet.cs`（internal）が logger / DebugSocket / UpdateSystemHost / VfxRuntimeHost を保持し、`StartAsync` と `Dispose` の順序を所有する。BeforeSceneLoad の最初の `Create*` より前に空で作られ、各サービスを現行の代入位置で 1 つずつ受け取る（I4a）。DebugSocket の telemetry sink は `DebugSocketService.CreateTelemetrySink()`（新 internal）で得る。
- **S6:** 欠番（facade の判定表の単体テスト。`CONTENT_DIRECTORY_STARTUP` へ移した）。
- **S7 回帰:** 全 EditMode 回帰と `pwsh tools/contract-audit.ps1` が合格する。

### 2.2 受け入れ条件

- **AC1（I1）** 次の 3 つで判定する。
  1. 次のコマンドの出力が base と head で一致する。修飾子で始まる宣言を、`{`・`;`・`=>` が現れるまで次の行と連結してから比べるので、複数行にまたがる宣言（例: `OnPlayerContentStartupFailedAsync`、1229 行目〜）の引数まで含まれる。式形式メンバーの `=>` 以降と、行末の `{`、1 行で書いた本体は落とす（accessor の本体は S5 で `FrameworkServiceSet` 経由に変わるため）。`{ get; private set; }` のような accessor の可視性は残る。空白・括弧の内側・カンマの前後は正規化するので、宣言の改行位置を変えただけでは差にならない。base で 51 行。

     ```bash
     awk '/^[ \t]*(protected|internal|public)[ \t]/{d=$0; while (d !~ /[{;]|=>/ && (getline l)>0) d=d" "l; print d}' unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs | sed -E 's/[ \t]*=>.*$//; s/\)[ \t]*\{.*$/)/; s/[ \t]*\{[ \t]*$//; s/;[ \t]*$//; s/^[ \t]+//; s/[ \t]+/ /g; s/\( /(/g; s/ \)/)/g; s/ ?, ?/, /g' | sort
     ```

  2. `PlayerContentStage` の値の並びが base と head で一致する（修飾子を持たない enum の値は 1 のコマンドに出ないため）。base は `UiCommonReady ContentDirectoryRegistered FirstSceneStable FixtureLoaded FixtureInstantiated FixtureBehaviorVerified FixtureDestroyed FixtureHandleReleased FirstSceneUnloaded ContentDirectoryClosed` の 10 値。

     ```bash
     awk '/protected enum PlayerContentStage/{p=1;next} p&&/}/{exit} p' unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs | grep -oE '^[ \t]*[A-Z][A-Za-z]*' | tr -d ' \t' | tr '\n' ' '
     ```

  3. `grep -nE "private [^;]*\b(_assetManagement|_contentDirectorySession|_sceneDirector)\b" unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` が base と同じ 3 つのフィールド宣言（型も同じ）を返す。

  1・2 は機械の補助で、テキストの照合なので取りこぼしがありうる（例: 属性の追加、`#if` の中の宣言）。判定 C は機械の結果に加えて、§3.5 の一覧の各メンバーについて base と head の宣言を本文で 1 つずつ見比べ、複数行の宣言の引数・既定値・属性・`static` の有無まで同じであることを §7 に記録する（必須）。合格例: 行の順序が変わる、accessor の本体が `_services?.LoggerFactory` になる。不合格例: `protected virtual` が `protected internal virtual` になる、hook が別クラスへ移って initializer から消える、複数行の宣言の引数の型が変わる、initializer と `FrameworkServiceSet` の両方に同じサービスのフィールドが残る（AC13 で確認）。
- **AC2（I2）** 次の出力が base と同じ 16 個である。

  ```bash
  grep -rhoE '"(before-scene-load|[a-z]+(-[a-z]+)+)"' unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap | sort -u
  ```

  加えて `grep -rn '"load-editor-first-scene:"' unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap` が 1 行を返す（動的 stage。上の grep は `:` で終わる文字列を拾わない）。

  かつ `InvalidCompanionConfigStartupTests`（4 件。A2 入力版の「5 件」は誤りで、`[Test]` は 4 つ。`create-scene-factory` で失敗する経路、`HasSceneDirector` / `HasAssetManagement` が false に戻ること）が変更なしで合格する。
- **AC3（I2, I3, I4, I4a）** B が head のコードを辿って次の 4 つの対応表を §6 に書く。C は表と実コードを照合する。対応が無い行、順序が入れ替わった行が 1 つでもあれば不合格。
  1. **順序:** §1 の「現在の順序」の各行 → head の呼び出し位置（`InitializeBeforeSceneLoad` / `InitializeAfterSceneLoad` / `ReleaseAll` と移動先のメソッド）。BeforeSceneLoad には、`FrameworkServiceSet` を空で作る位置（最初の `Create*` より前）と、各 `Create*` の戻り値を set へ渡す位置（次の hook 呼び出しより前）を行として含める。
  2. **stage:** §1「stage の代入点」の 18 箇所（初期値を含む）を分岐・代入箇所ごとに全数、直後に呼ぶ処理と、その代入を行うメソッドに対応付ける。同じ literal の 2 箇所（`load-ui-common`、`register-content-directory`）は別の行にする。`StartupProgress` の stage の初期値が `load-ui-common` であること。代入先は `StartupProgress`（§4 手順 4）で、catch・`CleanupAfterStartupFailureAsync`・finally の telemetry が同じインスタンスの現在値を読むこと。別の行として、`BootstrapAfterSceneLoad` の `before-scene-load` の通知が base と同じ位置・条件で残ること。
  3. **失敗経路:** AfterSceneLoad の catch の 6 行（LogError → Player 初回 Scene の unload → directory の close → `OnAfterSceneLoadInitializationFailed` → `OnPlayerContentStartupFailedAsync` → `ReleaseAll`）、OCE の filter、337・384 行目の早期 return 2 つ、BeforeSceneLoad の途中失敗で作り済みのサービスが破棄される経路、UICommon の取得途中の失敗で GameObject が破棄される経路（loader を await より前に field へ入れる）。
  4. **UICommon:** ロード済み Scene の検査（借用経路はここで return）→ `UseRequiredPlayerFileConfiguration` の評価と拒否 → directory か否かの分岐 → 該当する hook の評価とロード → handle と GameObject の記録 → component の検査 → `EnsureEventSystem` の行。加えて 3 経路ごとの所有の表（借用 / 所有）: GameObject を破棄するか、handle を持つか、EventSystem を作りうるか。`BootstrapUiCommonLoader.Dispose` の呼び出し位置が、base の `_uiCommonObject` の `Destroy` の位置（`AssetManagement.ReleaseAll` と map の解放の後）であること。

  合格例: `StartFrameworkServicesAsync` の本文が `FrameworkServiceSet.StartAsync` に移り、呼び出し位置が `OnServicesInitializing` の直前のまま。不合格例: VFX の `BindSceneLifecycle` が `RegisterAlreadyLoadedScenes` の後になる。`RunPlayerContentSmokeAsync` が stage を戻り値で返し、helper の中の失敗で catch が `load-player-first-scene` を見る。
- **AC4（I4）** 既存テスト `PlayerContentConfigurationTests`（XML の実行件数は base と同じ。`[TestCase]` を含め base で 13 件。A2 入力版の「9 件」は誤り。initializer の nested 型と static 判定を派生 harness から使う）と `SampleGame.Tests.Editor.Build.BuildContentDirectoryIntegrationTests`（1 件。Editor 統合で起動経路を通し、reflection で `_contentDirectorySession` と `_sceneDirector` を読む）が変更なしで、XML 上 **Passed** である（Skipped / Ignored は不合格。後者は StandaloneWindows64 以外で Ignore になるので、判定 C は StandaloneWindows64 の Editor で実行する）。
- **AC5（I5）** 次が 0 行を返す。

  ```bash
  grep -rn "DebugSocketTelemetrySink" unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap
  ```

  かつ、次が `AbstractApplicationInitializer.cs` の 1 ファイルだけを返す（件数は問わない。新しい 3 ファイルに 1 行でも出れば不合格。コメントに名前を書いただけでも不合格になるが、厳しい側なので許容する）。

  ```bash
  grep -rln -E "ContentDirectorySession|UnityContentDirectoryBackend|PlayerContentConfiguration|PlayerBakedContentConfiguration|ContentDeliveryFiles|ContentInstallReceipt|InstalledRevisionVerifier|VerifiedInstalledRevision|ContentDeclarationMismatch|ContentDirectoryConfiguration|RequiredJsonFileConfigProvider|SealAddressables|InstallContentDirectory|CloseContentDirectoryAsync|CompleteContentDirectoryPlayStop|AssetManagement\.AssetManagement\b|_contentDirectorySession|_bakedPlayerContent" unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap
  ```
- **AC6（I6）** 次の 3 つで判定する。
  1. `git diff --name-only base..head` に `\.asmdef$` と `^unity/Assets/SampleGame/` に当たる行が無い。
  2. 新しい public 型が 0: `git diff base..head -- 'unity/Assets/OneStarMaker/Scripts/*.cs' | grep -E '^\+[^+].*\bpublic\s+(sealed\s+|static\s+|abstract\s+|partial\s+|readonly\s+)*(class|struct|interface|enum|record)\b'` が 0 行。
  3. 新しい public メンバーが 0: `git diff base..head -- 'unity/Assets/OneStarMaker/Scripts/*.cs' | grep -E '^\+[^+].*\bpublic\b'` の各行を、B が §6 で「internal / private 型のメンバー」「コメント」「移動しただけの既存行」のどれかに分類する。C は各行がその分類に当たることを確かめ、どれにも当たらない行が 1 つでもあれば不合格。`CreateTelemetrySink` は `DebugSocketService.cs`（partial の本体）に `internal` で置く。対象を `Scripts/` に限るのは、NUnit のテスト fixture が public class であるため（テストは公開面ではない）。
- **AC7（I7）** `AppTelemetry.StartSpan(... AppStartup ...)` の呼び出しが Bootstrap folder に 2 箇所。span を閉じる処理を共通の private メソッドへ抽出した場合も、そのメソッドへ渡す stage の値が BeforeSceneLoad では `"BeforeSceneLoad"`、AfterSceneLoad では `success ? "AfterSceneLoad" : <StartupProgress の現在の stage>` で、`isSuccess`・`level`・`tags`・`payload` の渡し方が base と同じ。
- **AC8（S1）** `wc -l` で `AbstractApplicationInitializer.cs` ≤ 1,080、`InitializeAfterSceneLoad` の開始行から対応する閉じ括弧まで ≤ 130 行。
- **AC9:** 欠番（facade の判定表。`CONTENT_DIRECTORY_STARTUP` へ移した）。
- **AC10:** 欠番（facade の install 失敗経路。`CONTENT_DIRECTORY_STARTUP` へ移した）。
- **AC11（S3）** `LegacyRemoteCatalogProbe.cs`（internal static class。移動で変えてよいのは、`_config` の読み取りを引数 `AppConfig config` に置き換えることと `static` を付けることだけ）に `TryLoadAsync` / URL 解決 / revision 比較 / `build-info.json` URL 導出 / ローカル revision 解決が移り、`AbstractApplicationInitializer.cs` に `Addressables.LoadContentCatalogAsync`、`UnityWebRequest`、`RemoteCatalogRuntimeBridge`、`using UnityEngine.Networking` が無い。`DeriveBuildInfoUrl` 相当の純関数に 3 ケース（末尾 `/` あり、`/` なし、空）の単体テストを足す（`Tests/Bootstrap/`）。
- **AC12（S4, I4a）** `AbstractApplicationInitializer.cs` に `SceneManager.GetActiveScene`、`EventSystem`、`InputSystemUIInputModule`、`GetComponent<UICommon>` の参照が無い。`BootstrapUiCommonLoader` は、required Player のときに Addressables fallback を拒否する現行の例外と、directory の key が空のときの例外を、現行と同じメッセージ・同じ判定順で投げる。`UseRequiredPlayerFileConfiguration` は `Func<bool>`、hook（`GetDirectoryBootstrapSceneLogicalKey` / `GetUICommonPrefabAddress`）は `Func<string>` で loader に渡し、loader の中の現行と同じ時点・分岐でだけ評価する。directory か否か（`_contentDirectoryConfiguration != null`）は副作用の無い field の読み取りなので、呼び出し時に bool で渡してよい。借用経路では loader は何も所有しない（AC3 の表 4）。
- **AC13（S5, I3）** `AbstractApplicationInitializer.ReleaseAll` のサービス部が、`_cts.Cancel` → `ActiveVfxRuntimeHost` の条件付き解除（`ReferenceEquals` で自分の host のときだけ） → `FrameworkServiceSet.Dispose` → `_services = null` → `_cts.Dispose` の順である。`FrameworkServiceSet.Dispose` の内部順は VFX → DebugSocket → telemetry sink の登録 flag の初期化 → UpdateSystemHost → LoggerFactory で、未生成のものは飛ばし、2 回呼んでも安全。set は BeforeSceneLoad ごとに作り直し、使い回さない（sink の「一度だけ登録」は set のインスタンスが持つ）。`Dispose` の中に try/catch を足さない（I3）。initializer に logger / DebugSocket / UpdateSystemHost / VfxRuntimeHost のフィールドが残らない（AC3 の表 1 と `grep` で確認）。
- **AC14（S7, I6）** `pwsh tools/run-tests.ps1`（空 filter、EditMode）合格。テスト件数が base + 新規（AC11 の 3 件 + AC15 の 3 件 + AC16 の 2 件）以上。`pwsh tools/contract-audit.ps1` 合格。
- **AC15（I3, I4, I4a）** `Tests/Bootstrap/` に、`InvalidCompanionConfigStartupTests` の `TestInitializer` と同じ形の派生 harness（config file なし、環境変数で `content:runtimeMode=addressables`）を使う EditMode テストを 3 件足す。harness は `OneStarMaker.Tests` の中の型（`FakeAssetBackend` 等）だけで組み、SampleGame の型を使わない。組めない場合は Phase B を止めて Phase A へ戻す（テストの配置と観測点を決め直す。Phase C へ未確認のまま渡さない）。各テストは TearDown で `BootstrapSubsystemRegistration` を呼び、`ActiveVfxRuntimeHost`・`UpdateSystemRuntime`・`AppTelemetry` の static 状態を戻す。BeforeSceneLoad の失敗は `Debug.LogException` を出すので `LogAssert.Expect` で受ける。
  1. **サービスの途中まで:** `CreateVfxBackend` の override が、呼ばれた時点で `UpdateCoordinator` と `LoggerFactory` が非 null であることを記録してから例外を投げる。`BootstrapBeforeSceneLoad` の後に、記録が両方 true で、`UpdateCoordinator` と `LoggerFactory` が null、`UpdateSystemRuntime.Coordinator` が null（作り済みの host が破棄された）であることを確かめる。
  2. **サービスが揃った後:** `RegisterDefaultTelemetrySink` の override が、呼ばれた時点で `LoggerFactory` と `Vfx` が非 null であることを記録してから例外を投げる（base は呼ばない）。後に、記録が両方 true で、`Vfx`・`LoggerFactory`・`UpdateCoordinator`・`ActiveVfxRuntimeHost`・`UpdateSystemRuntime.Coordinator` が全て null であることを確かめる。
  3. **UICommon の component 欠落:** `FakeAssetBackend.SetSceneRoots` に UICommon component の無い root を置き（Addressables 経路）、`BootstrapAfterSceneLoad` が `load-ui-common` で失敗して `OnAfterSceneLoadInitializationFailed` に `load-ui-common` が渡り、例外メッセージが base と同じ（`UICommon component not found on prefab: <address>`）で、`ReleaseAll` が所有する root を破棄しようとする（EditMode の `Destroy may not be called from edit mode` を `LogAssert.Expect` で受ける。既存テストと同じ観測）ことを確かめる。借用経路（active Scene が `UICommon`）は EditMode で作れないので AC3 の表 4 で確認する。
- **AC16（I3）** `Tests/Bootstrap/FrameworkServiceSetTests.cs` に、Unity の transport・PlayerLoop を使わない 2 件を足す: 全て未設定のまま `Dispose` しても例外が出ない、`Dispose` を 2 回呼んでも例外が出ない。
- **AC17（§2.4 のログ文言）** 次の出力（`[AppInit]` を含む文字列リテラルの集合）が base と head で一致する。移動先のファイルを含めるため Bootstrap folder 全体を対象にする。

  ```bash
  grep -rhoE '"\[AppInit\][^"]*"|\$"\[AppInit\][^"]*"' unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap | sort
  ```

  複数行に分けた文字列は先頭の断片だけが比べられる。C は差が出た行について、文言が変わったのか断片の切れ目が変わっただけかを本文で確かめる。

### 2.3 ここでは答えない問いと所有スライス

- Content Directory 起動を facade（`ContentDirectoryStartup`）に集め、Bootstrap から BuildContent の internal 型と `AssetManagement` の directory 用 internal メソッドへの参照を 0 にする: `CONTENT_DIRECTORY_STARTUP`（[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)。program A0 §6 D4 の (3)）
- 連携部分と OSM 固有部分を BuildSystem 本体から分ける asmdef の判断: asmdef スライス（未作成。program A0 §4.3 の X1・X4）
- Bootstrap を `OneStarMaker.Runtime.Bootstrap` assembly に切り、namespace を揃える: asmdef スライス（未作成）
- `DebugSocketService.CreateTelemetrySink` を public にするか（Bootstrap と DebugSocketServices を別 assembly にしたときに要る）: asmdef スライス。本スライスは internal で置く
- `VfxRuntimeHost` を public にするか、Bootstrap と Rendering の境界をどう引くか: asmdef スライスの A0。S-4d program の所有物なので本スライスは触らない
- `04-app-startup.md` §4.7「Editor 設定読取の未決」（`JsonFileConfigProvider` が Addressables を読む）: 本スライスは `BuildConfig` の provider 列を変えない。未決は未決のまま残す
- `UpdateBehaviourAdapter` や `InputManager` のように製品配線の無い入口を initializer から配線するか: asmdef Stage 3 / 各機能の所有スライス
- `AbstractApplicationInitializer` を 500 行以下にするために hook を interface へ括り出す等の公開面の再設計: 本スライスでは行わない。必要なら新しい Phase A

### 2.4 本文へ転記した実装制約（`AGENTS.md` の常時契約から）

- 依存は Game → Framework の一方向。asmdef 参照の追加は行わない。
- アセットは `IAssetManagement` 経由。`BootstrapUiCommonLoader` と map の取得は現行と同じ `IAssetManagement` の公開メソッドを使う。
- `SceneState` の 14 値を変えない。状態変更は `SceneLifecycleManager` が所有する（本スライスは読まない）。
- 公開 API のログ抽象は `ILogger<T>`。新しい public 面は作らない（I6）。internal の `CreateTelemetrySink` も signature に `ZLogger*` 型を出さない。現行 initializer は `Debug.Log*` を使っており、移動先でも同じ呼び出しを保つ（ログ文言を変えない）。
- Update は `UpdateSystemRuntime` に登録する。`FrameworkServiceSet` は `UpdateSystemHost` の生成・破棄を移すだけで、登録の経路を変えない。
- Editor コードを Runtime アセンブリに置かない。現行の `#if UNITY_EDITOR` 3 箇所（`ResolveRemoteCatalogUrl`、`ResolveLocalRevision`、`ResolveSceneVariant`）はそのまま移す。
- `record` を使わない。新規 `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object`（UICommon の GameObject、EventSystem の GameObject、`EventSystem.current`）は `== null` / `!= null` で判定する。現行コードもそうなっている。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- 参照 0 を削除理由にしない（旧リモートカタログ経路は隔離して残す）。
- 移したメソッド名を `<c>` で参照しているコメント（`Editor/Build/Variants/LocalRevisionEditorInjector.cs:19`、`Runtime/AssetManagement/RemoteCatalogRuntimeBridge.cs:18`、`Runtime/Rendering/Vfx/Implements/VfxRuntimeHost.cs:17`）は、コメントだけを追随させてよい。コードは変えない。
- Unity Editor でのコンパイル確認は Phase B が行ってよい。Unity テストの実行・判定は Phase C。`unity test` / `unity run` は使わず `pwsh tools/run-tests.ps1`。

### 2.5 未決事項（A2 で意見を求める）

- **U1・U2:** 欠番（facade の入力の渡し方と `RequiredJsonFileConfigProvider` の扱い。`CONTENT_DIRECTORY_STARTUP` へ移した）。
- **U3（決定済み）:** A1 初稿は「`FrameworkServiceSet` を本スライスに含めるか」と、S2 を独立スライスにする案を残していた。program A0 §6 D4（owner 判断、2026-11-07）で S2 を独立スライスにすることが決まり、本書は S3〜S5 を 1 スライスで扱う（§2.6）。`FrameworkServiceSet`（S5）は含める。含めないと initializer は S1 の 1,080 行を満たさない（§3.3）。
- **U4（事実で解消）:** S-4d5b は PR #157（2026-11-03）で `develop` に入った。変更は Architecture 文書への harvest と HANDOFF の削除だけで、`AbstractApplicationInitializer` を触っていない。S-4 program 自体も削除済みなので、本スライスと並行して initializer を触る S-4 のスライスは無い。
- **U5 private メンバーの名前（A3 で閉じた）。** A2 で `_contentDirectorySession` と `_sceneDirector` の reflection が見つかり、I1 と §3.5 に加えた。B は Phase B 開始時に `GetField(` / `GetMethod(` / `GetProperty(` / `nameof(` を `unity/Assets/SampleGame/Tests`（`Editor` を含む）と `unity/Assets/OneStarMaker/Tests` で grep し直す（`GetField(` の引数は次の行に折り返すことがあるので `-A2` で見る）。base より増えていれば名前を保ち、§6 に記録する。

### 2.6 スライス分割の判断

- **S2 を切り出した（D4 の適用）。** S2 は AssetManagement / BuildSystem の単独利用 program の問い（越境を 1 箇所に集め、asmdef スライスが裁定できるようにする）に答えるもので、`BUILDSYSTEM_STANDALONE_ENTRY` の結論（`ContentDirectoryIndex` 等に public 入口ができるか、target の固定を外すか）で facade の越える internal と入力が変わる。S3〜S5 はその program に関係しない。変更理由も判定に使う証拠（S2 は Content Directory の判定表と install 失敗、S3〜S5 は initializer の順序の保存）も違うので、問いを分けた。素材は [RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md) に移した。
- **S3〜S5 はこれ以上分けない。** 3 つとも「initializer から変更理由の違う責務を出す」同じ問いの部分で、判定は同じ不変条件（I1〜I4 の公開面・順序・失敗経路の保存）と同じ証拠（AC3 の順序対応表、directory Play 1 回、全 EditMode 回帰）で行う。分けると、同じ順序対応表と動作確認を 3 回作ることになる。独立して失敗しうる未知も無い（S3 は呼び出しの無い経路の移動、S4・S5 は既知の経路の移動で、新しい実行時の経路を作らない）。A1 初稿の「B1 = S2 + S3、B2 = S4 + S5」は、S2 が抜けたので根拠（asmdef の前提を先に）を失った。
- **`InitializeAfterSceneLoad` の分割は本書に残す。** Player content smoke の 6 段と失敗時 cleanup は Content Directory の close を含むが、private メソッドへ移すだけで意味も呼び出し先も変えず、同じファイルに留まる（I5）。`CONTENT_DIRECTORY_STARTUP` は、その private メソッドの中の呼び出しを facade に置き換える。
- **受け入れ条件に別の問いの判定は残っていない。** AC4 の `BuildContentDirectoryIntegrationTests` は Content Directory の機能ではなく、initializer の起動経路が変わっていないことの回帰として使う。

## 3. 責務マップ

配置は全て既存 folder。新しい asmdef・folder・namespace は作らない。

### 3.1 ファイルと責務

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 許可する依存 | 公開面 | 現在行 → 予想 |
|---|---|---|---|---|---|---|
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 3 フェーズの起動順序、失敗境界、派生クラス向け hook の template | 起動契約（順序・stage・hook）が変わるとき | App 寿命。`BootstrapUiCommonLoader` と `FrameworkServiceSet` のインスタンスを所有し `ReleaseAll` で破棄（`LegacyRemoteCatalogProbe` は static で所有しない） | 下の 3 クラス、`SceneDirector` / `UICommon` / `IAssetManagement` / `AppConfig` / `AppTelemetry` の public API。Content Directory 起動のための BuildContent・Config の internal 型と `AssetManagement` の internal メソッド 4 つは base のまま残る（`CONTENT_DIRECTORY_STARTUP` が外す。I5） | §3.5 のまま | 1,303 → 約 1,000（非分割判断は §3.3） |
| `Runtime/Bootstrap/LegacyRemoteCatalogProbe.cs`（新規、internal static class） | 旧リモート Addressables catalog の追加ロードと revision 乖離警告（案内のみの旧経路） | 旧経路の案内文・URL 規則が変わるとき、または退役するとき | なし（状態を持たず、呼び出し経路も無い。退役時に 1 ファイル削除） | Addressables、`UnityWebRequest`、`RemoteCatalogRuntimeBridge`、`RemoteBuildInfo` | internal static: `UniTask TryLoadAsync(AppConfig config)`、`string DeriveBuildInfoUrl(string)` | 0 → 約 160 |
| `Runtime/Bootstrap/BootstrapUiCommonLoader.cs`（新規、internal sealed） | UICommon の取得（3 経路: ロード済み Scene の借用 / directory logical key / Addressables address）と、所有する経路での GameObject・EventSystem の GameObject・scene handle の参照の寿命 | UICommon の取得経路・EventSystem の構成が変わるとき | initializer が所有し、**`LoadAsync` を await する前に** field へ入れる。loader は handle と GameObject を、現行と同じ位置（component の検査より前）で自分の field に入れる。GameObject は App 寿命で、`Dispose` で破棄（2 回呼んでも安全）。借用経路では何も所有せず、`Dispose` は何もしない。scene handle は参照を持つだけで、`Dispose` で null にする（unload しない。scene の解放は AM の `ReleaseAll`）。`Dispose` は initializer の `ReleaseAll` の中で、base の `_uiCommonObject` の `Destroy` の位置（AM の `ReleaseAll` と map の解放の後）で呼ぶ | `IAssetManagement`、`UICommon`、`SceneManager`、`EventSystem`、`InputSystemUIInputModule`、`SceneAssetDescription` | internal: `UniTask<UICommon> LoadAsync(IAssetManagement, BootstrapUiCommonSource source)`、`void Dispose()`。`BootstrapUiCommonSource` は internal readonly struct で、directory か否かの bool、`UseRequiredPlayerFileConfiguration` を遅延評価する `Func<bool>`、hook を遅延評価する `Func<string>` 2 つを持つ（mode を先に確定しない。I4a） | 0 → 約 115 |
| `Runtime/Bootstrap/FrameworkServiceSet.cs`（新規、internal sealed） | logger / DebugSocket / UpdateSystemHost / VfxRuntimeHost の段階的な保持、`StartAsync`、telemetry sink の一度だけの登録、`Dispose` の順序 | 常駐サービスの集合と開始・停止順が変わるとき | initializer が所有。BeforeSceneLoad の最初の `Create*` より前に空で作って field に入れ、生成は initializer の hook（`Create*`）が行い、戻り値を現行の代入位置で 1 つずつ set に渡す（I4a）。BeforeSceneLoad ごとに作り直し、`ReleaseAll` で `Dispose` して null に戻す | `DebugSocketService`（public）、`UpdateSystemHost`、`AppLoggerFactory`、`VfxRuntimeHost`（internal、S-4d 所有）、`AppTelemetry` | internal: 引数なし ctor、getter / setter `AppLoggerFactory? LoggerFactory`・`DebugSocketService? DebugSocket`・`UpdateSystemHost? UpdateSystemHost`・`VfxRuntimeHost? Vfx`、`UniTask StartAsync(CancellationToken)`、`void Dispose()`（未設定は飛ばし、2 回呼んでも安全）。`ActiveVfxRuntimeHost` の設定・解除は initializer に残す（setter が private のため） | 0 → 約 130 |
| `Runtime/DebugSocketServices/DebugSocketService.cs` | （既存。追加のみ） | sink の生成を service の責務にする | — | — | internal: `ITelemetrySink CreateTelemetrySink()`（partial の本体ファイルに置く。`DebugSocketTelemetrySink` は internal のまま） | 391 → 約 400 |
| `Tests/Bootstrap/LegacyRemoteCatalogProbeTests.cs`（新規） | AC11 の 3 ケース | — | — | — | — | 0 → 約 40 |
| `Tests/Bootstrap/FrameworkServiceSetTests.cs`（新規） | AC16 の 2 件 | — | — | — | — | 0 → 約 30 |
| `Tests/Bootstrap/BeforeSceneLoadServiceStagingTests.cs`（新規） | AC15 の 3 件（派生 harness。`OneStarMaker.Tests` の型だけ） | — | — | — | — | 0 → 約 170 |
| `unity/Assets/Docs/Architecture/04-app-startup.md` | harvest 先（Phase D）。§4.2 の図を新構成（initializer + 3 クラス）に、§4.3 の `ReleaseAll` 図に `FrameworkServiceSet.Dispose` / `BootstrapUiCommonLoader.Dispose` を反映 | — | — | — | — | +15 程度 |

### 3.2 配置と依存を選んだ理由

- **Content Directory 起動の置き場所と、assembly を分けたときの扱いは本書で決めない。** `CONTENT_DIRECTORY_STARTUP` の範囲（[同書](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md) §3.2。A0 §4.2 の改訂を当てた理由はそこにある）。A1 初稿のここにあった「ASSET_CYCLE §1 の owner 決定で、目標は 2 assembly」は、owner の上位目的（AssetManagement 単独・BuildSystem 単独・併用。[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)）に置き換わって無効になった。本書が守るのは、その越境を新しい 3 クラスへ広げないこと（I5）だけ。
- **旧リモートカタログは Bootstrap folder に残す。** Addressables 互換経路の案内であり、BuildContent（Content Directory）の責務ではない。退役するときに 1 ファイル消せる形にしておく。
- **UICommon 取得と常駐サービスは internal。** 派生クラスは hook 経由で値を渡すだけで、これらの型を直接使わない。公開面を増やさないため internal にする。
- **hook は initializer に残す。** 公開面を変えない（I1）。`FrameworkServiceSet` は hook が作ったインスタンスを受け取る構成にして、派生クラスが override する場所を変えない。
- **新しい interface や抽象は作らない。** 3 クラスは具象で、initializer が直接生成する。差し替え点は既存の hook のままにし、テストは URL 導出の純関数と既存の harness で足りる。

### 3.3 行数警報

- **500 行:** `AbstractApplicationInitializer.cs` は約 1,040 行で残る。見積り（base `d4861882` の行範囲から）: S3 で 673〜826 行目の約 150 行、S4 で 514〜592 行目とフィールド・`ReleaseAll` の破棄で約 85 行、S5 で 886〜924 行目と `DisposeVfx`・フィールド・`ReleaseAll` の破棄で約 55 行が減る。`InitializeAfterSceneLoad` の 4 つの抽出（signature と波括弧で 1 つ約 5 行）と `StartupProgress`（約 10 行）で約 30 行増える。A2 の独立した再見積りは抽出込みで約 1,015〜1,035 行。S1 は 1,080 行以下とした。`InitializeAfterSceneLoad` は 219 行から、catch 本体（約 48 行減）、Player smoke（約 18 行減）、Editor 初回 Scene（約 10 行減）、finally の telemetry（約 16 行減）を抜いて約 125 行になる見込みで、S1 は 130 行以下とした。`CONTENT_DIRECTORY_STARTUP` が残りの Content Directory 分（約 130 行）を外して約 890 行になり、A1 初稿の見積り（約 860 行）とほぼ一致する。**非分割妥当**と判断する根拠: 本スライス後に残る内容は (a) XML doc と使用例 約 60 行、(b) protected 公開面の宣言と doc 約 250 行、(c) 3 フェーズの順序と失敗境界 約 350 行、(d) `BuildConfig` / `RegisterAlreadyLoadedScenes` / map 取得 約 100 行（map の取得は UICommon と同じく「directory なら logical key、それ以外は address」で呼び分けるが、所有物を持たず AM の App owner の handle だけなので、loader へ移さず順序の側に残す）、(e) Content Directory 起動 約 150 行（`CONTENT_DIRECTORY_STARTUP` の範囲）。(b) は公開面なので動かせない（I1）。(c) は「起動順序の契約」という 1 つの変更理由。(d) は順序と同じ理由で変わる。(e) は別スライスが外す。これ以上分けるには hook を interface へ括り出す公開面の再設計が要り、本スライスの対象外。A2 はこの判断を審査する。
- **3 責務:** 変更後の initializer は「順序 + hook + 失敗境界」と、`CONTENT_DIRECTORY_STARTUP` まで残る Content Directory 起動の 2 つ。後者は本スライスの対象外で、同書が外す。3 クラスは各 1 責務。
- **50% 増:** 既存ファイルで 50% 以上増えるものは無い。`DebugSocketService.cs` は +10 行。

### 3.4 harvest 先

`04-app-startup.md` §4.2 と §4.3（Phase D）。§4.7 の契約文は変えない。`20-variant-checkout-workflow.md` の旧リモート案内は、クラス名を書いていなければ変更不要（B が確認）。

### 3.5 公開面の一覧（I1 の基準。base `d4861882`）

変えない・増やさない・減らさないもの。

- protected static: `BootstrapSubsystemRegistration(AbstractApplicationInitializer)`、`BootstrapBeforeSceneLoad(AbstractApplicationInitializer)`、`BootstrapAfterSceneLoad(AbstractApplicationInitializer)`、`AppendFixtureStages(List<PlayerContentStage>, IReadOnlyList<PlayerContentStage>)`
- protected accessor: `Config`、`SceneDirector`、`AssetManagement`、`Vfx`、`LoggerFactory`、`UpdateCoordinator`
- protected abstract: `CreateSceneFactory()`、`GetUICommonPrefabAddress()`、`GetSceneResourceMapAddress()`、`CreateLoadingDisplay()`
- protected virtual: `CreateVfxBackend()`、`ResolveSceneVariant()`、`GetDirectoryBootstrapSceneLogicalKey()`、`GetDirectoryBootstrapMapLogicalKey()`、`GetConfigFilePath()`、`UseRequiredPlayerFileConfiguration`、`GetRequiredPlayerConfigurationPath()`、`LoadPlayerSceneResourceMap()`、`GetEnvironmentVariablePrefix()`、`OnServicesInitializing(CancellationToken)`、`OnAfterSceneLoadInitializationFailed(string, Exception)`、`OnPlayerContentReadyAsync(IAssetManagement, CancellationToken)`、`OnPlayerContentShutdownCompletedAsync(IReadOnlyList<PlayerContentStage>)`、`OnPlayerContentStartupFailedAsync(string, Exception, IReadOnlyList<PlayerContentStage>)`、`CreateLoggerFactory(Stream?)`、`CreateDebugSocketService(AppConfig)`、`CreateDebugCommandDispatcher()`、`CreateUpdateSystemHost()`、`RegisterDefaultTelemetrySink()`
- protected nested: `enum PlayerContentStage`（10 値、順序固定）、`sealed class PlayerContentFixtureResult`
- internal static: `ShouldNotifyPlayerContentFailure(bool)`、`RunsPlayerContentSmoke(bool)`、`ReadEditorFirstScene(AppConfig)`、`ActiveVfxRuntimeHost { get; private set; }`
- private だが名前を保つもの: フィールド `_assetManagement`（`InvalidCompanionConfigStartupTests` の reflection）、`_contentDirectorySession`（`BuildContentDirectoryIntegrationTests` の reflection）、`_sceneDirector`（同テストと VFX の Play テスト群の reflection）

## 4. 実装計画

- 変更対象: §3.1 の表。
- 順序（各手順を 1 commit にし、手順ごとに Unity Editor でコンパイルを確認する）:
  1. `LegacyRemoteCatalogProbe` へ 5 メソッドを移す（`_config` は引数にする。AC11）（本文は変えない）。AC11 のテスト。
  2. `BootstrapUiCommonLoader` へ `LoadUICommonAsync` / `EnsureEventSystem` と GameObject / handle の破棄を移す。
  3. `DebugSocketService.CreateTelemetrySink()`（internal）を足し、`FrameworkServiceSet` へ `StartFrameworkServicesAsync` / `DisposeVfx` の破棄部分 / service の破棄順を移す。set は BeforeSceneLoad の最初の `Create*` より前に空で作り、各 `Create*` の戻り値を現行の代入位置で set に渡す。protected accessor（`LoggerFactory`、`UpdateCoordinator`、`Vfx`）と base の `RegisterDefaultTelemetrySink` は set を読む。`ActiveVfxRuntimeHost` の設定・解除は initializer に残し、解除は `ReferenceEquals` の条件付きで set の `Dispose` より前に行う（AC13）。
  4. `InitializeAfterSceneLoad` を次の 4 つの private メソッドに分け、本体を 130 行以下にする。
     - `RunPlayerContentSmokeAsync`（Player content smoke の 6 段）と `LoadEditorFirstSceneAsync`（Editor directory Play の初回 Scene）。
     - `CleanupAfterStartupFailureAsync`（catch の中の Player 初回 Scene の unload、directory の close、2 つの hook の呼び出し。段ごとの try/catch は現行のまま持つ）。catch の `LogError` と `ReleaseAll` は本体の catch に残し、`ReleaseAll` は helper の後に呼ぶ。
     - `FinishStartupSpan`（span を閉じる処理。BeforeSceneLoad の finally と共通にする。AC7）。
     - stage と `playerContentStages` は private nested の `sealed class StartupProgress` に置き、`InitializeAfterSceneLoad` の冒頭で 1 回の起動ごとに作って各 helper に渡す（static にしない。I8）。stage の代入は helper の中でも `StartupProgress` に対して行い、戻り値で返さない（AC3 の表 2）。
     - `RunPlayerContentSmokeAsync` と `LoadEditorFirstSceneAsync` は try/catch を持たない。OCE の filter と `catch (Exception)` は本体に 1 つずつ残す。337・384 行目の早期 return は本体に残す。
     - helper はファイルの中で `InitializeAfterSceneLoad` の直後に、呼ばれる順に置く。
     - 両 helper の中の Content Directory の close（`CloseContentDirectoryAsync`）と具象 `AssetManagement` への確認は、本文を変えずにそのまま移す（`CONTENT_DIRECTORY_STARTUP` が facade に置き換える）。
  5. AC15 / AC16 のテストを書く（AC15 の harness は最初に組めるかを確かめる。組めなければ差し戻し）。
  6. AC1 / AC2 / AC5 / AC6 / AC7 / AC8 / AC17 のコマンドを実行し、AC3 の 4 つの表と AC6 の分類を §6 に書く。head で `grep -n "_contentDirectoryConfiguration\|_contentDirectorySession\|_bakedPlayerContent"` を実行し、Content Directory の状態に触れるメソッドの一覧を §6 に `CONTENT_DIRECTORY_STARTUP` への入力として記録する（受け入れ条件ではない）。`pwsh tools/contract-audit.ps1`。
- Phase B から Phase A へ差し戻す条件: §3.5 の公開面を変えないと分けられない。AC15 の harness が `OneStarMaker.Tests` の型だけで組めない。順序を変えないとコンパイルできない、または例外の握り方を変えないと移せない箇所がある。§3.1 にない public メンバーが要る。`VfxRuntimeHost` の型・可視性を変える必要がある。Content Directory 起動のコード（`CONTENT_DIRECTORY_STARTUP` の範囲）を initializer の外へ出さないと S1 か AC8 を満たせない。SampleGame のテストを変えないと通らない。
- 対象外を維持する方法: Content Directory 起動のコード（`ReadContentDirectoryConfiguration`、nested `ContentDirectoryConfiguration`、`InstallConfiguredContentDirectoryAsync`、`WarnOnDeclarationMismatch`、`BuildConfig`、Addressables の封じ、`ReleaseAll` の directory 分岐）の本文を変えない（AC5）。`.asmdef` を開かない。`unity/Assets/SampleGame/` に触れない（AC6 の `git diff --stat`）。hook の宣言行を編集しない（AC1）。`04-app-startup.md` の変更は Phase D まで行わない。

## 5. テストとレビュー計画

- 単体テスト: `LegacyRemoteCatalogProbeTests`（3 件）、`FrameworkServiceSetTests`（2 件）、`BeforeSceneLoadServiceStagingTests`（3 件、派生 harness）。Unity なしで検証できる範囲は URL 導出と set の空・二重の Dispose。成功経路の UICommon 取得と常駐サービスは既存の起動経路のテスト（AC2 / AC4、`GameSceneLoggingTests`、VFX の Play テスト群、`FlyControllerTeleportTests`）と AC3 の対応表で確認する。BeforeSceneLoad の途中失敗、サービスが揃った後の失敗、UICommon の component 欠落は既存テストが通らないので AC15 で足す（A2 と外部レビューの指摘）。UICommon の借用経路は EditMode で作れないので AC3 の表 4 で確認する。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `OneStarMaker.Tests.BuildContent`、`OneStarMaker.Tests.Bootstrap`、`SampleGame.Tests.Bootstrap`、`SampleGame.Tests.Editor.Build`、`OneStarMaker.Tests.SampleGame`（`GameSceneLoggingTests`。A2 入力版の `SampleGame.Tests.App` は存在しない namespace だった）、`SampleGame.Tests.Rendering`（VFX の Play テスト群が `ActiveVfxRuntimeHost` と `_sceneDirector` を読む）、`SampleGame.Tests.Streaming`（`FlyControllerTeleportTests` が実際の `AppInitializer` を起動する）。
- 判定必須テスト（GO 候補 head）: 全 EditMode 回帰（空 filter、StandaloneWindows64 の Editor）。XML で、新規 8 件、AC2 / AC4 の既存テスト、VFX の Play テスト群、`FlyControllerTeleportTests` が収録され、Passed であること（Skipped / Ignored は不合格）。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: 既存の `BuildContentDirectoryIntegrationTests`（Editor 統合、1 件、約 300 行）が Content Directory の build → install → 起動を通す。S-4d5a の Play テスト群（`SampleGame/Tests/Rendering/Vfx/*PlayTests`）が `ActiveVfxRuntimeHost` 経由で VFX 配線を確認する。どちらも全 EditMode 回帰に含まれる。Build は不要。
- 操作・実行時・目視条件の検証経路: **Editor の directory Play を 1 回**（AC3 の順序表の実機確認）。担当は動作確認の実行担当（AI）。環境: ローカル Unity Editor、Content Directory build 済みの install が Delivery に 1 つある状態（無ければ `20-variant-checkout-workflow.md` の手順で用意する。用意できなければ「未実施」と理由を書く）。操作: Title Scene から Play を開始し、Title が表示されたら停止する。観測: Console に `[AppInit] AfterSceneLoad: loading UICommon.` → `loading SceneResourceMap.` → `Starting DebugSocket transport` または `DebugSocket は無効です` の順で出ること、例外が無いこと、停止時に `Cannot find handle for scene` 等の例外が無いこと。保存する証拠: Console ログの text。合否は構造（ログ順序と例外 0）で判断し、画像は使わない。C / C' への受け渡しは evidence bundle。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: なし（directory Play は S-4 で反復済みの既知経路）。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし
- 高コスト証拠: なし
- 判定 C の照合 script の方式: なし（AC のコマンド、AC3 の対応表、XML のテスト名・件数で判定する）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC1 / AC2 / AC5 / AC6 / AC7 / AC8 / AC17 のコマンド。
- A0/A1 主担当・モデル・ベンダー: Claude Code（Anthropic）。調査は本書 §1 の現況に転記済み。
- A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版: 下記。観点は (a) アーキテクチャゲート（3 クラスの責務と配置、`CreateTelemetrySink` 以外に公開面が要らないか、§3.3 の非分割判断と S1 の 1,050 行、§2.6 の分割の判断、I5 の「越境を新しいクラスへ広げない」が `CONTENT_DIRECTORY_STARTUP` の前提として足りるか）、(b) 不変条件の破り方（I2〜I4 を破る順序を 1 つ挙げられるか、AC3 の対応表方式で検出できるか、stage 名と receipt の互換）、(c) テスト境界（UICommon 取得と常駐サービスに新しい単体テストを作らない判断、SampleGame.Tests の reflection 以外の隠れた結合）。目的レビューは §1 の「目的」欄の原文だけを入力にする（利用者は `なし`）。高リスクなので、A0（program A0 と本書 §1 の現況）だけから代替構成を出すレビューを検討する。A2 は「計画対象は実装済みと仮定して」評価し、未実装を理由に差し戻さない。
- A3 統合担当・採否: owner + 主担当。採否は下の「A3 の採否」。owner が 2026-11-07 に凍結を指示した。
- C' 用に予約した担当・モデル・ベンダー: A2 で使わなかった系列。高リスクなので、Phase A / B / C に関与していないベンダーを優先する。
- 独立性の強化条件を満たせない場合の理由: 未定（A2 で記録する）。

### A2 独立レビュー

- 入力版: commit `7b2cf205` の本書。content sha256 `172391acd5d1c3581c3fdc6b713123cbeb52b154a73a00c0cbe4bb8008170c15`。
- 実施: 2026-11-07。4 件を互いの指摘を見せずに並行して実施した。いずれも主担当と同じベンダーの別 session（独立性制約あり。owner が別ベンダーの外部レビューを別途行う）。
  - **R1 アーキテクチャゲート**（観点 (a)）: 入力版、`architecture-gates.md`、program A0、`CONTENT_DIRECTORY_STARTUP`、現コード。「計画対象は実装済みと仮定」を指示。判定: 条件付き支持（blocker 1 / should 7 / nit 3）。
  - **R2 不変条件の破り方とテスト境界**（観点 (b)(c)）: 入力版と現コード・テスト。同じ枠の指示。判定: 条件付き支持（blocker 3 / should 6 / nit 2）。
  - **R3 目的レビュー**: §1「目的」欄の原文だけ（利用者 `なし`）。入力 sha256 `2aae67b5787e95c2d35857fbd65fb59218051a0bcdb03bf121c9404567a49775`。保証すべきこと 12 件、作らないもの 10 件。
  - **R4 A0 だけからの代替構成**: 本書 §1 と program A0、現コードだけ（初稿は読まない）。推奨案は本書と同じ 3 クラス（`FrameworkServiceSet`、UICommon の loader（hook は `Func<string>`）、旧カタログの static class）+ `InitializeAfterSceneLoad` の同ファイル内分割（stage は private nested の保持者）+ internal 側の `CreateTelemetrySink`。initializer 約 1,040 行。代替案（partial class のみ、stage の pipeline）は不採用を推奨。
- R1・R2 の生の回答は git に入れていない（主担当の作業領域にだけある）。採否は下の表が正。

### A3 の採否

| 指摘 | 内容 | 採否 | 反映先・理由 |
|---|---|---|---|
| R1 F-01 / R2 F-02（blocker） | `FrameworkServiceSet` を 4 インスタンスの ctor で作ると、BeforeSceneLoad 途中失敗でサービスが漏れ、hook から accessor が null に見え、base の telemetry sink が消える | 採用 | 段階的に満たす保持者に変更（§3.1、S5、§4 手順 3）。I4 / I4a、AC3 表 1・3、AC15 |
| R2 F-01（blocker） | AC1 が accessor の本体まで比べるので、正しい実装で必ず不一致 | 採用 | AC1 の sed で `=>` 以降を落とす |
| R2 F-03（blocker）/ R1 F-02 後半 | async helper へ stage を渡す方式が未定。戻り値で返すと receipt の stage が黙ってずれる | 採用 | `StartupProgress`（§4 手順 4）、AC3 表 2 |
| R1 F-02（should） | 抽出 2 つでは `InitializeAfterSceneLoad` が約 149 行残り 110 行に届かない | 採用（数値と手順の両方） | 抽出を 4 つ（Editor 初回 Scene、span の終了を追加）にし、上限を 130 行、ファイルを 1,080 行に（S1、AC8、§3.3）。R3 の「極小ラッパーを乱立させない」とは、追加の 2 つが「Player smoke と対になる分岐」と「2 フェーズ共通の処理」なので両立すると判断 |
| R1 F-03 | `CreateTelemetrySink` を public にする根拠が目的の改訂で消えた | 採用 | internal に（I6、§3.1、§2.3 に asmdef スライスへの問いとして残す）。R3 の保証 12 とも一致 |
| R1 F-04 / R2 F-04 | UICommon の source を先に組み立てると hook の呼び出し有無と検証順が変わる。loader を await 後に field へ入れると失敗時に GameObject が残る | 採用 | mode + `Func<string>`、await 前に field へ（§3.1、I4a、AC3 表 3・4、AC12） |
| R1 F-06 / R2 F-05 | `ActiveVfxRuntimeHost` の解除と set の Dispose の順、`_cts`・sink flag・`_services = null` の位置が未定 | 採用 | AC13 を書き直し、I3 に条件付き解除を明記 |
| R1 F-07 / R2 F-09 | `ContentDeclarationMismatch` が BuildContent の internal 型の一覧と AC5 から漏れている | 採用 | §1 を 9 型に、AC5 の正規表現を拡張（`ContentDirectoryConfiguration`、`AssetManagement.AssetManagement`、Content Directory 用 field も追加）。`CONTENT_DIRECTORY_STARTUP` の一覧も同じ訂正 |
| R1 F-08 / R2 F-06 | reflection の隠れた結合が `_assetManagement` 以外にもある（`_contentDirectorySession`、`_sceneDirector`）。`CONTENT_DIRECTORY_STARTUP` の field 統合が AC4 のテストを壊す | 採用 | §1、I1、§3.5、U5、AC1。`CONTENT_DIRECTORY_STARTUP` §2 に申し送り。触れる箇所の目録は §4 手順 6 で B の記録にする（受け入れ条件にはしない。結果に依存する文書を合否に使わないため） |
| R2 F-07 | AC6 が機械判定できない（HANDOFF 自身が diff に入る、`--stat` の省略、文言の矛盾、partial） | 採用 | AC6 を `--name-only`、`*.cs` に限った grep、public 型 0 の grep、行の分類の 3 段に |
| R2 F-08 / R1 F-11 | 新しい単体テストを作らない判断は失敗経路で空白。統合テストは StandaloneWindows64 以外で Ignore | 一部採用 | AC15（BeforeSceneLoad 途中失敗と telemetry sink の時点）、AC16（set の空・二重 Dispose）、AC4 に Passed を明記、判定必須に VFX の Play テスト群と `FlyControllerTeleportTests`、起点 filter に Rendering / Streaming。UICommon の component 欠落のテストは A2 では不採用にしたが、外部レビュー S2 で採用に改めた（AC15-3） |
| R2 F-10 | helper の try/catch と OCE の filter の位置 | 採用（修正して） | §4 手順 4。`CleanupAfterStartupFailureAsync` は現行の段ごとの try/catch を持つので「helper は try/catch を持たない」は smoke と Editor 初回 Scene の helper に限定 |
| R2 F-11 | 移したメソッド名を参照するコメントが Bootstrap の外に 3 箇所 | 採用 | §2.4 にコメントだけの追随を許可 |
| R1 F-09 | map の取得を initializer に残す理由が無い | 採用 | §3.3 (d)、loader の責務を寿命の側から書き直し |
| R1 F-10 / R4 | `LegacyRemoteCatalogProbe` は static class でよい | 採用 | §3.1、AC11 |
| R4 | 早期 return 2 つ（337・384 行目）が catch を通らずに失敗 telemetry を出す経路を残す | 採用 | §1、I4、AC3 表 3、§4 手順 4 |
| R4 | `[AppInit]` ログと stage literal の集合を前後で静的に照合する | 採用 | AC17（stage は AC2 が既に照合） |
| R4 | hook の呼び出し列を記録するテスト、hook ごとの fault injection で stage 名を確かめるテスト | 不採用 | AC3 の表 1・2 と AC15 で、壊れやすい箇所（逐次代入・stage の受け渡し・揃った後の失敗）は覆える。hook ごとの fault injection は hook 23 個分の harness になり、移動だけのスライスに対して重い。外部レビューも「全 hook への fault injection は不要」とした |
| R3（保証 4・5） | 起動契約が 1 か所で読める、分割後のメソッドが実行順に並ぶ | 採用 | §4 手順 4（helper を `InitializeAfterSceneLoad` の直後に呼ばれる順で置く） |
| R3（保証 8 / 作らないもの 5） | internal を public 化して参照 0 にしない | 採用 | R1 F-03 と同じ |
| R3（その他） | 振る舞い不変、新しい hook・interface・pipeline・facade を作らない、旧経路を消さない | 既に満たしている | I1〜I8、§2.3、§3.2 |

**外部レビュー（2026-11-07、owner が依頼。入力は A3 途中版、`develop` `7c9b364d` のコード。判定「方向性は支持。凍結前に Blocker 3 件と Should 3 件の処理を推奨」）**

| 指摘 | 内容 | 採否 | 反映先・理由 |
|---|---|---|---|
| B1 | UICommon の借用と所有を区別し、scene handle の解放権限を明記する | 採用 | §1「UICommon の所有」、I3、§3.1、AC3 表 4、AC12。コードで確認（530〜543 行目の借用経路は field に入れない） |
| B2 | stage の代入は 17 ではなく 18 箇所 | 採用 | §1「stage の代入点」、AC3 表 2。コードで確認 |
| B3 | AC15 の「未確認。初回確認は Phase C」が判定必須と矛盾 | 採用 | AC15 から免責を削り、組めなければ Phase A へ差し戻す（§4）。`FakeAssetBackend` が `OneStarMaker.Tests` にあることを確認した |
| S1 | `UseRequiredPlayerFileConfiguration` の評価時点も守る | 採用（遅延評価） | I4a、§3.1、AC12。getter の純粋さに頼る案は採らない（派生クラスの実装を知らないので owner の契約追加が要り、本スライスの範囲を超える） |
| S2 | UICommon の component 欠落は既存パターンで安くテストできる | 採用 | AC15-3。A2 で不採用にした判断（偽の Scene が要る）は誤りだった |
| S3 | サービスが揃った後の失敗を 1 件 fault injection する | 採用（AC15-2 に統合） | AC15-2 を「記録してから投げる」に変え、全 accessor・`ActiveVfxRuntimeHost`・`UpdateSystemRuntime.Coordinator` の null を確かめる。fake の VFX backend の Dispose 回数は数えない（`VfxRuntimeHost.Create` は null backend で未構成の backend を使うので、`ActiveVfxRuntimeHost` と accessor の null で破棄を観測できる） |

**外部レビュー 2 回目（2026-11-07、owner から受領）**

| 指摘 | 内容 | 採否 | 反映先・理由 |
|---|---|---|---|
| E2-1（重要） | AC1 の grep は複数行の宣言の引数を拾えない（`OnPlayerContentStartupFailedAsync`） | 採用 | AC1 を、宣言を連結してから比べるコマンドに変え（base で確認: 複数行の宣言が引数ごと 1 行になる）、判定 C に §3.5 の各メンバーの本文での見比べを必須にした。主担当の追加: 同じ理由で `PlayerContentStage` の値（修飾子を持たない）も AC1 に出ていなかったので、値の並びの比較を足した |
| E2-2 | S4 は scene handle の「破棄を所有」と書き、§3.1（参照を null にするだけ、解放は AM）と矛盾 | 採用 | S4 を §3.1 に合わせた |
| E2-3 | §1「本書の範囲との対応」が BuildContent 8 型のまま | 採用 | 9 型に訂正 |

**主担当の自己点検（Phase C / C' で突かれそうな点。2026-11-07）**

| 点 | 内容 | 反映先 |
|---|---|---|
| P1 | AC2 の「5 件」は実数 4 件、AC4 の「9 件」は `[TestCase]` を含め 13 件 | AC2、AC4（AC4 は「base と同じ件数」にした） |
| P2 | 起点 filter の `SampleGame.Tests.App` は存在しない namespace。`GameSceneLoggingTests` は `OneStarMaker.Tests.SampleGame` | §5 |
| P3 | AC6 の grep が `OneStarMaker/` 全体だと、新しいテスト fixture（public class）で必ず該当する | AC6 を `Scripts/` に限定 |
| P4 | `LegacyRemoteCatalogProbe` を static にすると `_config` を引数にする必要があり、「本文は変えない」と衝突する | AC11、§4 手順 1 に許す変更を明記 |
| P5 | 破棄の途中の例外の扱い（現行は try/catch 無し）を移動で「改善」すると挙動が変わる。逆に C' が「例外で後続が破棄されない」と指摘しうる | I3、AC13 に「try/catch を足さない」を明記（現行の挙動の保存が本スライスの問い） |
| P6 | AC15 のテストは PlayerLoop と `AppTelemetry` の static 状態を触るので、他のテストへの漏れを C' が突く | AC15 に TearDown での `BootstrapSubsystemRegistration` と `LogAssert` を明記 |
| P7 | §1 の順序に `ActiveVfxRuntimeHost` の設定と冒頭の `_cts` 検査が無く、AC3 の表 1 で照合されない | §1 |
| P8 | §3.1 の initializer の所有欄が static の probe を「所有」と書いていた | §3.1 |

初稿に無かった保証で R3 が挙げたもの（振る舞い・失敗境界・hook の不変、寿命の不変）は、R1・R2 の指摘を当てた I3 / I4 / I4a で本文に入った。初稿が作る要らないものは、R3 の観点では public の `CreateTelemetrySink` だけで、internal に改めた。目的欄の文言は変えていないので、目的レビューのやり直しは要らない。

## 6. Phase B 実装結果

- 実装:
- HANDOFF との差:
- AC3 の順序対応表（§1「現在の順序」の各行 → head の呼び出し位置）:
- 未実行:
- implementation head commit:
- Phase B 担当・モデル・ベンダー:

### 動作確認

- 対象 head:
- 条件ごとの実行担当・環境・実行したコマンド:
- 生の観測（Console ログの順序、例外の有無）と保存先・状態:
- 検証経路の期待と違う観測と、B への差し戻し:
- `未実施` の条件と理由・担当:

## 7. Phase C

- 種別:
- 使用した evidence bundle の取得案内・状態:
- 構造適合（§3.1 との照合、§3.3 の行数警報の判定: 分割済み / 非分割妥当 / Phase A 再開）:
- 現在の問いを阻害する findings:
- 後続スライスへ移送する findings:
- 実行したテストコマンドと `-Filter`、対象を選んだ理由:
- テスト結果:
- 判定必須のうち未実行:
- 未確認事項:
- 担当・モデル:

## 8. Phase C'

- 担当方式:
- 種別と巡:
- 受け取った決定記録の版:
- 使用した blind audit bundle の取得案内・状態:
- 確認範囲・方法:
- 判定:
- 現在の問いを阻害する findings:
- 後続スライスへ移送する findings:
- 残存リスク:
- 監査できなかった範囲:
- 独立性:
- 発見 C / 判定 C 結論の事前閲覧・設計実装への関与:
- 担当・モデル:

## 9. Phase D

- C / C' の突合:
- マージ判断:
- 必要な Evidence の保存・取得・必須内容の閲覧確認:
- 取得案内の引渡し確認:
- harvest: `04-app-startup.md` §4.2 / §4.3
- 削除確認:
- 通常の task 終了・後始末の状態と未完了事項:
