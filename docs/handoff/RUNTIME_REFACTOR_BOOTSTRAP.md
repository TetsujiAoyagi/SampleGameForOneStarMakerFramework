# AbstractApplicationInitializer の責務分解（旧リモートカタログ・UICommon・常駐サービス）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `A`（A1 改訂版 = A2 入力版。A2 未実施、A3 未凍結。**実装キューではない**）
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
- Phase A snapshot の取得案内・状態: A2 入力版は、この A1 改訂を収録した commit（A2 入力版の commit。SHA は本書に書かず起動指示で渡す）の本書と、その content sha256 で固定する（文書だけで script・機械向け期待値を持たないので stage は不要）。commit hash と sha256 は A2 開始時に §5「A2 独立レビュー」へ記録する。A2 中の改訂版は Phase A snapshot で渡す。A3 凍結版は未生成
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: A2（独立レビュー）。
  - 次の作業: §5 の A2 計画に従い、アーキテクチャゲートと目的レビュー（高リスクなので A0 だけから代替構成を出すレビューも検討）を、A2 入力版を入力に互いの指摘を見せずに依頼し、結果を §5「A2 独立レビュー」に記録する。目的レビューには §1 の「目的」欄の原文だけを渡す（利用者の欄は無い）。その後に A3（owner と主担当で採否、U4 の順序を閉じる）。
  - 引き継ぐ commit: A2 入力版の commit（SHA は起動指示で渡す）。
  - 必要な権限: なし（文書のみ）。Unity Editor・テスト・資格情報は不要。
  - 停止規則: A3 で凍結するまで Phase B を始めない。同じ session で Phase B に入らない。A2 の指摘で `CONTENT_DIRECTORY_STARTUP` へ移した範囲（facade・Content Directory の判定表）を本書へ戻す必要が出たら、本書で裁かず owner に返す（D4 の変更になる）。
  - 引き渡さないもの: なし（Phase A 内）。

### PR 状態ブロック

```text
状態: Phase A の 作業中（A1 改訂版 = A2 入力版）
Phase A: revision 0、commit <A2 入力版の commit。起動指示で渡す>
implementation base / head: d4861882 / 未固定
次の担当: A2。次の作業: 独立レビュー（§5）
HANDOFF: docs/handoff/RUNTIME_REFACTOR_BOOTSTRAP.md
Evidence 取得案内: 未生成
未決事項: §2.5 の U4・U5（U1・U2 は CONTENT_DIRECTORY_STARTUP へ移し、U3 は D4 で決定）
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
  - **他 folder の internal 型への参照（Runtime 内で Bootstrap だけが持つ）:** BuildContent 8 型（`ContentDirectorySession`、`UnityContentDirectoryBackend`、`PlayerContentConfiguration`、`PlayerBakedContentConfiguration`、`ContentDeliveryFiles`、`ContentInstallReceipt`、`InstalledRevisionVerifier`、`VerifiedInstalledRevision`）、Config 1 型（`RequiredJsonFileConfigProvider`）、DebugSocketServices 1 型（`DebugSocketTelemetrySink`）、Rendering 1 型（`VfxRuntimeHost`）。加えて `AssetManagement` の internal メソッド 4 つ（`SealAddressables`（246 行目）、`InstallContentDirectory`（605）、`CloseContentDirectoryAsync`（421、463）、`CompleteContentDirectoryPlayStop`（981））を、`_assetManagement` を具象 `AssetManagement.AssetManagement` へ確かめる 5 箇所（240、419、456、603、977 行目）を経て呼ぶ。`AssetManagement` のコンストラクタは public（235 行目）。
  - **変更理由の混在。** 同じクラスに (a) 3 フェーズの順序と hook（起動契約）、(b) `AppConfig` から Content Directory 構成を解く判定（1079〜1151 行目、`ReadContentDirectoryConfiguration` + nested `ContentDirectoryConfiguration`）、directory と決まった直後の Addressables の封じ（240〜249 行目、BS5a）、session の登録・close と宣言の不一致警告（594〜636 行目、`InstallConfiguredContentDirectoryAsync` + `WarnOnDeclarationMismatch`、BS5b-2）、435〜480 行目付近の失敗時 cleanup、ReleaseAll の 2 分岐）、(c) 旧リモート Addressables catalog の追加ロードと revision 乖離警告（673〜822 行目、5 メソッド。`TryLoadRemoteCatalogAsync` に呼び出しは無く、`InitializeAfterSceneLoad` 内のコメント「旧リモート Addressables catalog 追加ロードは通常起動から外した」が残る）、(d) UICommon Scene / prefab の取得 3 経路と EventSystem の生成（514〜592 行目）、(e) logger / DebugSocket / UpdateSystemHost / VfxRuntimeHost の生成・開始・破棄順（886〜924 行目、941〜1009 行目）が入っている。
  - **隠れた結合。** `SampleGame/Tests/Bootstrap/InvalidCompanionConfigStartupTests.cs` は reflection で private フィールド `_assetManagement` を差し替える。`OneStarMaker/Tests/BuildContent/PlayerContentConfigurationTests.cs` は `ShouldNotifyPlayerContentFailure` と nested `PlayerContentStage` / `AppendFixtureStages` を派生 harness から使う。S-4d5a が足した SampleGame の Play テスト群は `ActiveVfxRuntimeHost` を読む。
  - **観測可能な契約。** AfterSceneLoad の stage 名は 16 個で、失敗時に `OnAfterSceneLoadInitializationFailed(stage, ex)` と `OnPlayerContentStartupFailedAsync(stage, …)` に渡り、BS4 Player の receipt に残る: `before-scene-load`、`register-content-directory`、`load-ui-common`、`wait-ui-common-ready`、`load-scene-resource-map`、`start-framework-services`、`initialize-app-services`、`create-scene-factory`、`resolve-scene-variant`、`create-scene-director`、`register-loaded-scenes`、`load-player-first-scene`、`run-player-content-smoke`、`unload-player-first-scene`、`close-player-content-directory`、`complete-player-content-smoke`。このほかに BS5b-2 が足した動的な stage `"load-editor-first-scene:" + firstScene`（Editor directory Play の初回 Scene。Player の receipt には現れない）が 1 つある。
  - **現在の順序（不変条件の基準）。** BeforeSceneLoad: CTS 生成 → `Application.quitting` 購読 → `AssetManagement` 生成 → `BuildConfig`（required Player なら `PlayerBakedContentConfiguration` もここで生成） → `ReadContentDirectoryConfiguration` → （directory なら）`SealAddressables` と封じたことの `Debug.Log` → `CreateUpdateSystemHost` → `AppTelemetry.Thresholds` → `CreateDebugSocketService` → `LogDebugSocketBootstrapStatus` → `CreateLoggerFactory` → `VfxRuntimeHost.Create` → `RegisterDefaultTelemetrySink`。AfterSceneLoad: （directory かつ非 required なら）session 登録 → UICommon 取得 → `WaitForPanelReadyAsync` → SceneResourceMap（Player / directory / Addressables の 3 経路） → `StartFrameworkServicesAsync` → `OnServicesInitializing` → `CreateSceneFactory` → `ResolveSceneVariant` → （directory かつ未登録なら）session 登録 → representation の上書き → `SceneDirector` 生成 → `BindSceneDirector` → `BindSceneLifecycle`（VFX） → `RegisterAlreadyLoadedScenes` → （firstScene があれば）required Player でないなら Editor の初回 Scene を `AddScene` して観測ログ 1 行、required Player なら Player content smoke の 6 段。session 登録の直後に宣言の不一致警告（`WarnOnDeclarationMismatch`）。ReleaseAll: `_cts.Cancel` → `DisposeVfx` → DebugSocket → UpdateSystemHost → LoggerFactory → CTS → `_bakedPlayerContent` / `_config` / `_contentDirectoryConfiguration` → `SceneDirector.Dispose` → `AssetManagement.ReleaseAll`（+ `CompleteContentDirectoryPlayStop` または `BeginSynchronousShutdown`） → map → UICommon GO → `_uiSceneHandle` → EventSystem GO → `AppTelemetry.Shutdown`。
  - 失敗経路: AfterSceneLoad の catch は、required Player で first scene がロード済みなら unload（Editor は ReleaseAll と Play の終了に任せる） → directory が登録済みなら close → `OnAfterSceneLoadInitializationFailed` → （required Player なら）`OnPlayerContentStartupFailedAsync` → `ReleaseAll` の順で、各段の例外は記録して元の例外を隠さない。`OperationCanceledException` は非 required のときだけ成功扱い。
  - 本書の範囲との対応: 上の (b) と、BuildContent 8 型・Config 1 型・`AssetManagement` の internal メソッド 4 つへの参照は `CONTENT_DIRECTORY_STARTUP` の範囲で、本スライスでは initializer に残る。本スライスが移すのは (c)〜(e) と DebugSocketServices 1 型への参照。Rendering 1 型（`VfxRuntimeHost`）への参照は `FrameworkServiceSet` へ移るが、Bootstrap folder の中に残る。
  - 設計文書: `04-app-startup.md` §4.2 のクラス構造図は現在のメソッド構成を前提にしている。§4.7 には「Editor 設定読取の未決」（`BuildConfig` が directory 判定より前に `JsonFileConfigProvider` で Addressables を読む）が記録されており、本スライスはこの未決を解決も正当化もしない。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: `AbstractApplicationInitializer` の起動・終了順序と派生クラス向け公開面を変えずに、旧リモートカタログ・UICommon 取得・Framework 常駐サービスを変更理由ごとのクラスへ分け、`InitializeAfterSceneLoad` を分割し、Bootstrap から DebugSocketServices の internal 型への参照を 0 にできるか。
- 判定定義: §2.1 の不変条件と成立条件を §2.2 の受け入れ条件で全て確認できれば GO。一つでも未達なら NO-GO。スパイクではないので CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし（A3 後に作る場合は本書と別の記録にする）

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I1 公開面不変:** §3.5 の一覧にある protected / internal メンバーの名前、signature、アクセス修飾子、nested の位置、`static` の有無を変えない。追加もしない。private フィールド `_assetManagement` の名前と型を変えない（SampleGame.Tests の reflection）。
- **I2 起動順序不変:** §1 の「現在の順序」にある各処理の相対順序を変えない。stage 名 16 個の文字列、動的 stage の接頭辞 `load-editor-first-scene:`、各処理に対応する stage の割当を変えない。
- **I3 終了順序不変:** `ReleaseAll` の解放順（§1）を変えない。複数回呼んでも安全であることを保つ。
- **I4 失敗経路不変:** 各失敗点で呼ばれる hook と、その前に行う cleanup の順序、例外の握り方（元の例外を隠さない、cleanup の例外は記録だけ）を変えない。`ContentDirectoryException` の `Code` とメッセージを変えない。`OperationCanceledException` の扱いを変えない。
- **I5 越境を広げない:** 変更後の `Runtime/Bootstrap/**` に DebugSocketServices の internal 型の識別子が現れない。BuildContent・Config の internal 型と `AssetManagement` の internal メソッド 4 つ（`SealAddressables`、`InstallContentDirectory`、`CloseContentDirectoryAsync`、`CompleteContentDirectoryPlayStop`）への参照は、`AbstractApplicationInitializer.cs` の外（新しい 3 クラス）へ出さない。`CONTENT_DIRECTORY_STARTUP` が 1 ファイルだけを見れば済むようにするため。Rendering の `VfxRuntimeHost` は対象外（§1）。
- **I6 境界不変:** `.asmdef` の変更 0 件。`unity/Assets/SampleGame/` の変更 0 件（Tests を含む）。新しい public 型は 0。新しい public メンバーは `DebugSocketService.CreateTelemetrySink` の 1 つだけ。
- **I7 Telemetry 不変:** AppStartup span は BeforeSceneLoad と AfterSceneLoad の 2 つのまま。payload の `stage`（成功時 `BeforeSceneLoad` / `AfterSceneLoad`、失敗時は到達 stage）を変えない。
- **I8 新しい静的状態を作らない:** `ActiveVfxRuntimeHost` 以外の static 可変状態を増やさない。service locator を作らない。

**成立条件（起きなければならないこと）**

- **S1 サイズ:** `AbstractApplicationInitializer.cs` が 1,050 行以下。`InitializeAfterSceneLoad` が 110 行以下。（A1 初稿の 900 行は S2 を含めた値。S2 の分、約 130 行を戻した。900 行以下は `CONTENT_DIRECTORY_STARTUP` の目標として同書へ移した。見積りは §3.3）
- **S2:** 欠番（Content Directory の facade。`CONTENT_DIRECTORY_STARTUP` へ移した）。
- **S3 旧リモートカタログの隔離:** `Runtime/Bootstrap/LegacyRemoteCatalogProbe.cs`（internal）に旧 5 メソッドが移り、initializer から消える。呼び出し経路の有無は現状のまま（増やさない・減らさない）。
- **S4 UICommon 取得の隔離:** `Runtime/Bootstrap/BootstrapUiCommonLoader.cs`（internal）が UICommon の 3 経路と EventSystem 生成、両 GameObject と scene handle の破棄を所有する。
- **S5 常駐サービス集合:** `Runtime/Bootstrap/FrameworkServiceSet.cs`（internal）が logger / DebugSocket / UpdateSystemHost / VfxRuntimeHost を保持し、`StartAsync` と `Dispose` の順序を所有する。DebugSocket の telemetry sink は `DebugSocketService.CreateTelemetrySink()`（新 public）で得る。
- **S6:** 欠番（facade の判定表の単体テスト。`CONTENT_DIRECTORY_STARTUP` へ移した）。
- **S7 回帰:** 全 EditMode 回帰と `pwsh tools/contract-audit.ps1` が合格する。

### 2.2 受け入れ条件

- **AC1（I1）** 次のコマンドの出力（空白正規化後）が base と head で一致する。

  ```bash
  grep -E "^\s*(protected|internal|public)\s" unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs | sed -E 's/^\s+//; s/\s+/ /g' | sort
  ```

  加えて `grep -n "_assetManagement" ` で private フィールド宣言が残る。合格例: 行の順序が変わる。不合格例: `protected virtual` が `protected internal virtual` になる、hook が別クラスへ移って initializer から消える。
- **AC2（I2）** 次の出力が base と同じ 16 個である。

  ```bash
  grep -rhoE '"(before-scene-load|[a-z]+(-[a-z]+)+)"' unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap | sort -u
  ```

  加えて `grep -rn '"load-editor-first-scene:"' unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap` が 1 行を返す（動的 stage。上の grep は `:` で終わる文字列を拾わない）。

  かつ `InvalidCompanionConfigStartupTests`（5 件。`create-scene-factory` で失敗する経路、`HasSceneDirector` / `HasAssetManagement` が false に戻ること）が変更なしで合格する。
- **AC3（I2, I3, I4）** B が §1 の「現在の順序」を、head の `InitializeBeforeSceneLoad` / `InitializeAfterSceneLoad` / `ReleaseAll` と、移動先の各メソッドを辿って 1 行ずつ対応付けた表を §6 に書く。C は表と実コードを照合する。対応が無い行、順序が入れ替わった行が 1 つでもあれば不合格。合格例: `StartFrameworkServicesAsync` の本文が `FrameworkServiceSet.StartAsync` に移り、呼び出し位置が `OnServicesInitializing` の直前のまま。不合格例: VFX の `BindSceneLifecycle` が `RegisterAlreadyLoadedScenes` の後になる。
- **AC4（I4）** 既存テスト `PlayerContentConfigurationTests`（9 件。initializer の nested 型と Player content smoke の段を派生 harness から使う）と `SampleGame.Tests.Editor.Build.BuildContentDirectoryIntegrationTests`（1 件。Editor 統合で起動経路を通す）が変更なしで合格する。
- **AC5（I5）** 次が 0 行を返す。

  ```bash
  grep -rn "DebugSocketTelemetrySink" unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap
  ```

  かつ、次が `AbstractApplicationInitializer.cs` の行だけを返す（件数は問わない。新しい 3 ファイルに 1 行でも出れば不合格）。

  ```bash
  grep -rln -E "ContentDirectorySession|UnityContentDirectoryBackend|PlayerContentConfiguration|PlayerBakedContentConfiguration|ContentDeliveryFiles|ContentInstallReceipt|InstalledRevisionVerifier|VerifiedInstalledRevision|RequiredJsonFileConfigProvider|SealAddressables|InstallContentDirectory|CloseContentDirectoryAsync|CompleteContentDirectoryPlayStop" unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap
  ```
- **AC6（I6）** `git diff --stat base..head` に `.asmdef` と `unity/Assets/SampleGame/` が無い。`public` を含む追加行が `DebugSocketService.cs` の `CreateTelemetrySink` 以外に無い（`git diff base..head | grep -E '^\+.*\bpublic\b'` で確認。新しい internal クラスの中の `public` メンバーは、型が internal なので公開面に出ない。B はその行を §6 に列挙し、C は各行の型が internal であることを確かめる）。
- **AC7（I7）** `AppTelemetry.StartSpan(... AppStartup ...)` の呼び出しが Bootstrap folder に 2 箇所、`CreateTimingMemoryTelemetry(... stage: ...)` の `stage` 値が `"BeforeSceneLoad"` と `success ? "AfterSceneLoad" : startupStage` のまま。
- **AC8（S1）** `wc -l` で `AbstractApplicationInitializer.cs` ≤ 1,050、`InitializeAfterSceneLoad` の開始行から対応する閉じ括弧まで ≤ 110 行。
- **AC9:** 欠番（facade の判定表。`CONTENT_DIRECTORY_STARTUP` へ移した）。
- **AC10:** 欠番（facade の install 失敗経路。`CONTENT_DIRECTORY_STARTUP` へ移した）。
- **AC11（S3）** `LegacyRemoteCatalogProbe.cs` に `TryLoadAsync` / URL 解決 / revision 比較 / `build-info.json` URL 導出 / ローカル revision 解決が移り、`AbstractApplicationInitializer.cs` に `Addressables.LoadContentCatalogAsync`、`UnityWebRequest`、`RemoteCatalogRuntimeBridge` の参照が無い。`DeriveBuildInfoUrl` 相当の純関数に 3 ケース（末尾 `/` あり、`/` なし、空）の単体テストを足す（`Tests/Bootstrap/`）。
- **AC12（S4）** `AbstractApplicationInitializer.cs` に `SceneManager.GetActiveScene`、`EventSystem`、`InputSystemUIInputModule`、`GetComponent<UICommon>` の参照が無く、`BootstrapUiCommonLoader` が required Player のときに Addressables fallback を拒否する現行の例外（メッセージ同一）を投げる。
- **AC13（S5）** `AbstractApplicationInitializer.ReleaseAll` の service 解放が `FrameworkServiceSet.Dispose` 1 呼び出しになり、その内部順序が VFX → DebugSocket → UpdateSystemHost → LoggerFactory である（AC3 の表で確認）。`_debugSocketTelemetrySinkRegistered` 相当の「一度だけ登録」が保たれる。
- **AC14（S7, I6）** `pwsh tools/run-tests.ps1`（空 filter、EditMode）合格、テスト件数が base + 新規（AC11 の 3 件）以上。`pwsh tools/contract-audit.ps1` 合格。

### 2.3 ここでは答えない問いと所有スライス

- Content Directory 起動を facade（`ContentDirectoryStartup`）に集め、Bootstrap から BuildContent の internal 型と `AssetManagement` の directory 用 internal メソッドへの参照を 0 にする: `CONTENT_DIRECTORY_STARTUP`（[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)。program A0 §6 D4 の (3)）
- 連携部分と OSM 固有部分を BuildSystem 本体から分ける asmdef の判断: asmdef スライス（未作成。program A0 §4.3 の X1・X4）
- Bootstrap を `OneStarMaker.Runtime.Bootstrap` assembly に切り、namespace を揃える: asmdef スライス（未作成）
- `VfxRuntimeHost` を public にするか、Bootstrap と Rendering の境界をどう引くか: asmdef スライスの A0。S-4d program の所有物なので本スライスは触らない
- `04-app-startup.md` §4.7「Editor 設定読取の未決」（`JsonFileConfigProvider` が Addressables を読む）: 本スライスは `BuildConfig` の provider 列を変えない。未決は未決のまま残す
- `UpdateBehaviourAdapter` や `InputManager` のように製品配線の無い入口を initializer から配線するか: asmdef Stage 3 / 各機能の所有スライス
- `AbstractApplicationInitializer` を 500 行以下にするために hook を interface へ括り出す等の公開面の再設計: 本スライスでは行わない。必要なら新しい Phase A

### 2.4 本文へ転記した実装制約（`AGENTS.md` の常時契約から）

- 依存は Game → Framework の一方向。asmdef 参照の追加は行わない。
- アセットは `IAssetManagement` 経由。`BootstrapUiCommonLoader` と map の取得は現行と同じ `IAssetManagement` の公開メソッドを使う。
- `SceneState` の 14 値を変えない。状態変更は `SceneLifecycleManager` が所有する（本スライスは読まない）。
- 公開 API のログ抽象は `ILogger<T>`。新しい public メンバー（`DebugSocketService.CreateTelemetrySink`）の signature に `ZLogger*` 型を出さない。現行 initializer は `Debug.Log*` を使っており、移動先でも同じ呼び出しを保つ（ログ文言を変えない）。
- Update は `UpdateSystemRuntime` に登録する。`FrameworkServiceSet` は `UpdateSystemHost` の生成・破棄を移すだけで、登録の経路を変えない。
- Editor コードを Runtime アセンブリに置かない。現行の `#if UNITY_EDITOR` 3 箇所（`ResolveRemoteCatalogUrl`、`ResolveLocalRevision`、`ResolveSceneVariant`）はそのまま移す。
- `record` を使わない。新規 `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object`（UICommon の GameObject、EventSystem の GameObject、`EventSystem.current`）は `== null` / `!= null` で判定する。現行コードもそうなっている。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- 参照 0 を削除理由にしない（旧リモートカタログ経路は隔離して残す）。
- Unity Editor でのコンパイル確認は Phase B が行ってよい。Unity テストの実行・判定は Phase C。`unity test` / `unity run` は使わず `pwsh tools/run-tests.ps1`。

### 2.5 未決事項（A2 で意見を求める）

- **U1・U2:** 欠番（facade の入力の渡し方と `RequiredJsonFileConfigProvider` の扱い。`CONTENT_DIRECTORY_STARTUP` へ移した）。
- **U3（決定済み）:** A1 初稿は「`FrameworkServiceSet` を本スライスに含めるか」と、S2 を独立スライスにする案を残していた。program A0 §6 D4（owner 判断、2026-11-07）で S2 を独立スライスにすることが決まり、本書は S3〜S5 を 1 スライスで扱う（§2.6）。`FrameworkServiceSet`（S5）は含める。含めないと initializer は S1 の 1,050 行を満たさない（§3.3）。
- **U4（事実で解消）:** S-4d5b は PR #157（2026-11-03）で `develop` に入った。変更は Architecture 文書への harvest と HANDOFF の削除だけで、`AbstractApplicationInitializer` を触っていない。S-4 program 自体も削除済みなので、本スライスと並行して initializer を触る S-4 のスライスは無い。
- **U5 private メソッドの名前。** `_assetManagement` 以外に reflection で読まれている private メンバーは見つかっていない。B は Phase B 開始時に `GetField(` / `GetMethod(` / `nameof(` の grep を SampleGame.Tests と OneStarMaker.Tests で再確認し、見つかれば名前を保つ。

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
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 3 フェーズの起動順序、失敗境界、派生クラス向け hook の template | 起動契約（順序・stage・hook）が変わるとき | App 寿命。下の 3 クラスを所有し `ReleaseAll` で破棄 | 下の 3 クラス、`SceneDirector` / `UICommon` / `IAssetManagement` / `AppConfig` / `AppTelemetry` の public API。Content Directory 起動のための BuildContent・Config の internal 型と `AssetManagement` の internal メソッド 4 つは base のまま残る（`CONTENT_DIRECTORY_STARTUP` が外す。I5） | §3.5 のまま | 1,303 → 約 1,000（非分割判断は §3.3） |
| `Runtime/Bootstrap/LegacyRemoteCatalogProbe.cs`（新規、internal sealed） | 旧リモート Addressables catalog の追加ロードと revision 乖離警告（案内のみの旧経路） | 旧経路の案内文・URL 規則が変わるとき、または退役するとき | 状態なし。`AppConfig` を受け取る | Addressables、`UnityWebRequest`、`RemoteCatalogRuntimeBridge`、`RemoteBuildInfo` | internal: `UniTask TryLoadAsync(AppConfig config)`、internal static `string DeriveBuildInfoUrl(string)` | 0 → 約 160 |
| `Runtime/Bootstrap/BootstrapUiCommonLoader.cs`（新規、internal sealed） | UICommon の取得 3 経路（ロード済み Scene / directory logical key / Addressables address）と EventSystem の生成、両 GameObject と scene handle の破棄 | UICommon の取得経路・EventSystem の構成が変わるとき | initializer が所有。GameObject は App 寿命 | `IAssetManagement`、`UICommon`、`SceneManager`、`EventSystem`、`InputSystemUIInputModule`、`SceneAssetDescription` | internal: `UniTask<UICommon> LoadAsync(IAssetManagement, BootstrapUiCommonSource source)`、`void Dispose()`。`BootstrapUiCommonSource` は internal readonly struct（mode と key / address） | 0 → 約 110 |
| `Runtime/Bootstrap/FrameworkServiceSet.cs`（新規、internal sealed） | logger / DebugSocket / UpdateSystemHost / VfxRuntimeHost の保持、`StartAsync`、telemetry sink の一度だけの登録、`Dispose` の順序 | 常駐サービスの集合と開始・停止順が変わるとき | initializer が所有。生成は initializer の hook（`Create*`）が行い、インスタンスを受け取る | `DebugSocketService`（public）、`UpdateSystemHost`、`AppLoggerFactory`、`VfxRuntimeHost`（internal、S-4d 所有）、`AppTelemetry` | internal: ctor(logger, debugSocket, updateHost, vfxHost)、`UniTask StartAsync(CancellationToken)`、`UpdateSystemHost? UpdateSystemHost`、`VfxRuntimeHost? Vfx`、`void Dispose()` | 0 → 約 120 |
| `Runtime/DebugSocketServices/DebugSocketService.cs` | （既存。追加のみ） | sink の生成を service の責務にする | — | — | public: `ITelemetrySink CreateTelemetrySink()`（`DebugSocketTelemetrySink` は internal のまま） | 391 → 約 400 |
| `Tests/Bootstrap/LegacyRemoteCatalogProbeTests.cs`（新規） | AC11 の 3 ケース | — | — | — | — | 0 → 約 40 |
| `unity/Assets/Docs/Architecture/04-app-startup.md` | harvest 先（Phase D）。§4.2 の図を新構成（initializer + 3 クラス）に、§4.3 の `ReleaseAll` 図に `FrameworkServiceSet.Dispose` / `BootstrapUiCommonLoader.Dispose` を反映 | — | — | — | — | +15 程度 |

### 3.2 配置と依存を選んだ理由

- **Content Directory 起動の置き場所と、assembly を分けたときの扱いは本書で決めない。** `CONTENT_DIRECTORY_STARTUP` の範囲（[同書](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md) §3.2。A0 §4.2 の改訂を当てた理由はそこにある）。A1 初稿のここにあった「ASSET_CYCLE §1 の owner 決定で、目標は 2 assembly」は、owner の上位目的（AssetManagement 単独・BuildSystem 単独・併用。[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)）に置き換わって無効になった。本書が守るのは、その越境を新しい 3 クラスへ広げないこと（I5）だけ。
- **旧リモートカタログは Bootstrap folder に残す。** Addressables 互換経路の案内であり、BuildContent（Content Directory）の責務ではない。退役するときに 1 ファイル消せる形にしておく。
- **UICommon 取得と常駐サービスは internal。** 派生クラスは hook 経由で値を渡すだけで、これらの型を直接使わない。公開面を増やさないため internal にする。
- **hook は initializer に残す。** 公開面を変えない（I1）。`FrameworkServiceSet` は hook が作ったインスタンスを受け取る構成にして、派生クラスが override する場所を変えない。
- **新しい interface や抽象は作らない。** 3 クラスは具象で、initializer が直接生成する。差し替え点は既存の hook のままにし、テストは URL 導出の純関数と既存の harness で足りる。

### 3.3 行数警報

- **500 行:** `AbstractApplicationInitializer.cs` は約 1,000 行で残る。見積り（base `d4861882` の行範囲から）: S3 で 673〜826 行目の約 150 行、S4 で 514〜592 行目とフィールド・`ReleaseAll` の破棄で約 85 行、S5 で 886〜924 行目と `DisposeVfx`・フィールド・`ReleaseAll` の破棄で約 55 行が減り、`InitializeAfterSceneLoad` の分割で約 10 行増える（差し引き約 280 行減で約 1,020 行）。S1 の 1,050 行以下に収まる見込みだが余裕は小さい。`CONTENT_DIRECTORY_STARTUP` が残りの Content Directory 分（約 130 行）を外して約 890 行になり、A1 初稿の見積り（約 860 行）とほぼ一致する。**非分割妥当**と判断する根拠: 本スライス後に残る内容は (a) XML doc と使用例 約 60 行、(b) protected 公開面の宣言と doc 約 250 行、(c) 3 フェーズの順序と失敗境界 約 350 行、(d) `BuildConfig` / `RegisterAlreadyLoadedScenes` / map 取得 約 100 行、(e) Content Directory 起動 約 150 行（`CONTENT_DIRECTORY_STARTUP` の範囲）。(b) は公開面なので動かせない（I1）。(c) は「起動順序の契約」という 1 つの変更理由。(d) は順序と同じ理由で変わる。(e) は別スライスが外す。これ以上分けるには hook を interface へ括り出す公開面の再設計が要り、本スライスの対象外。A2 はこの判断を審査する。
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
- private だが名前を保つもの: フィールド `_assetManagement`（SampleGame.Tests の reflection）

## 4. 実装計画

- 変更対象: §3.1 の表。
- 順序（各手順を 1 commit にし、手順ごとに Unity Editor でコンパイルを確認する）:
  1. `LegacyRemoteCatalogProbe` へ 5 メソッドを移す（本文は変えない）。AC11 のテスト。
  2. `BootstrapUiCommonLoader` へ `LoadUICommonAsync` / `EnsureEventSystem` と GameObject / handle の破棄を移す。
  3. `DebugSocketService.CreateTelemetrySink()` を足し、`FrameworkServiceSet` へ `StartFrameworkServicesAsync` / `DisposeVfx` / service の破棄順を移す。`ActiveVfxRuntimeHost` の設定・解除は initializer に残す。
  4. `InitializeAfterSceneLoad` を、Player content smoke の 6 段（`RunPlayerContentSmokeAsync`）と失敗時 cleanup（`CleanupAfterStartupFailureAsync`）の private メソッドに分け、本体を 110 行以下にする。stage 名の代入位置は変えない。両メソッドの中の Content Directory の close（`CloseContentDirectoryAsync`）と具象 `AssetManagement` への確認は、本文を変えずにそのまま移す（`CONTENT_DIRECTORY_STARTUP` が facade に置き換える）。
  5. AC1 / AC2 / AC5 / AC6 / AC7 / AC8 のコマンドを実行し、AC3 の対応表を §6 に書く。`pwsh tools/contract-audit.ps1`。
- Phase B から Phase A へ差し戻す条件: §3.5 の公開面を変えないと分けられない。順序を変えないとコンパイルできない、または例外の握り方を変えないと移せない箇所がある。§3.1 にない public メンバーが要る。`VfxRuntimeHost` の型・可視性を変える必要がある。Content Directory 起動のコード（`CONTENT_DIRECTORY_STARTUP` の範囲）を initializer の外へ出さないと S1 か AC8 を満たせない。SampleGame のテストを変えないと通らない。
- 対象外を維持する方法: Content Directory 起動のコード（`ReadContentDirectoryConfiguration`、nested `ContentDirectoryConfiguration`、`InstallConfiguredContentDirectoryAsync`、`WarnOnDeclarationMismatch`、`BuildConfig`、Addressables の封じ、`ReleaseAll` の directory 分岐）の本文を変えない（AC5）。`.asmdef` を開かない。`unity/Assets/SampleGame/` に触れない（AC6 の `git diff --stat`）。hook の宣言行を編集しない（AC1）。`04-app-startup.md` の変更は Phase D まで行わない。

## 5. テストとレビュー計画

- 単体テスト: `LegacyRemoteCatalogProbeTests`（3 件）。Unity なしで検証できる範囲は URL 導出。UICommon 取得と常駐サービスは既存の起動経路のテスト（AC2 / AC4、`GameSceneLoggingTests`、VFX の Play テスト群）と AC3 の対応表で確認し、新しい単体テストは作らない（実体が Unity の Scene・GameObject・transport で、移動だけなので、偽物を作るより既存の経路の方が強い）。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `OneStarMaker.Tests.BuildContent`、`OneStarMaker.Tests.Bootstrap`、`SampleGame.Tests.Bootstrap`、`SampleGame.Tests.Editor.Build`、`SampleGame.Tests.App`（`GameSceneLoggingTests` が起動経路を通る）。
- 判定必須テスト（GO 候補 head）: 全 EditMode 回帰（空 filter）。XML で新規 3 件と AC2 / AC4 の既存テストが収録されていること。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: 既存の `BuildContentDirectoryIntegrationTests`（Editor 統合、1 件、約 300 行）が Content Directory の build → install → 起動を通す。S-4d5a の Play テスト群（`SampleGame/Tests/Rendering/Vfx/*PlayTests`）が `ActiveVfxRuntimeHost` 経由で VFX 配線を確認する。どちらも全 EditMode 回帰に含まれる。Build は不要。
- 操作・実行時・目視条件の検証経路: **Editor の directory Play を 1 回**（AC3 の順序表の実機確認）。担当は動作確認の実行担当（AI）。環境: ローカル Unity Editor、Content Directory build 済みの install が Delivery に 1 つある状態（無ければ `20-variant-checkout-workflow.md` の手順で用意する。用意できなければ「未実施」と理由を書く）。操作: Title Scene から Play を開始し、Title が表示されたら停止する。観測: Console に `[AppInit] AfterSceneLoad: loading UICommon.` → `loading SceneResourceMap.` → `Starting DebugSocket transport` または `DebugSocket は無効です` の順で出ること、例外が無いこと、停止時に `Cannot find handle for scene` 等の例外が無いこと。保存する証拠: Console ログの text。合否は構造（ログ順序と例外 0）で判断し、画像は使わない。C / C' への受け渡しは evidence bundle。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: なし（directory Play は S-4 で反復済みの既知経路）。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし
- 高コスト証拠: なし
- 判定 C の照合 script の方式: なし（AC のコマンド、AC3 の対応表、XML のテスト名・件数で判定する）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC1 / AC2 / AC5 / AC6 / AC7 / AC8 のコマンド。
- A0/A1 主担当・モデル・ベンダー: Claude Code（Anthropic）。調査は本書 §1 の現況に転記済み。
- A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版: 下記。観点は (a) アーキテクチャゲート（3 クラスの責務と配置、`CreateTelemetrySink` 以外に公開面が要らないか、§3.3 の非分割判断と S1 の 1,050 行、§2.6 の分割の判断、I5 の「越境を新しいクラスへ広げない」が `CONTENT_DIRECTORY_STARTUP` の前提として足りるか）、(b) 不変条件の破り方（I2〜I4 を破る順序を 1 つ挙げられるか、AC3 の対応表方式で検出できるか、stage 名と receipt の互換）、(c) テスト境界（UICommon 取得と常駐サービスに新しい単体テストを作らない判断、SampleGame.Tests の reflection 以外の隠れた結合）。目的レビューは §1 の「目的」欄の原文だけを入力にする（利用者は `なし`）。高リスクなので、A0（program A0 と本書 §1 の現況）だけから代替構成を出すレビューを検討する。A2 は「計画対象は実装済みと仮定して」評価し、未実装を理由に差し戻さない。
- A3 統合担当・モデル・採否: 未定（owner + 主担当）。U3 は D4 で決定済み、U4 は事実で解消済み（§2.5）。A3 では A2 の指摘の採否と U5 の扱いを閉じる。
- C' 用に予約した担当・モデル・ベンダー: A2 で使わなかった系列。高リスクなので、Phase A / B / C に関与していないベンダーを優先する。
- 独立性の強化条件を満たせない場合の理由: 未定（A2 で記録する）。

### A2 独立レビュー

（A1 入力版の commit と content sha256、各レビューの観点・担当・モデル・ベンダー・結果をここに記録する）

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
