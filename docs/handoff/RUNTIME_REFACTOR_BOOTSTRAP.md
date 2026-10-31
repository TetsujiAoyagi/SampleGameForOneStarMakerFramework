# AbstractApplicationInitializer の責務分解と internal 越境の seam 化

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `A`（A1 初稿。A2 未実施、A3 未凍結。**実装キューではない**）
- branch: 計画は `claude/runtime-refactor-stage0-plan`（本書と [RUNTIME_REFACTOR_CYCLES.md](RUNTIME_REFACTOR_CYCLES.md) を同じ文書 PR で `develop` へ）。実装は Phase B 開始時に `develop` から専用ブランチを切る（名前は B 担当が `<prefix>/runtime-refactor-bootstrap` で決める）
- implementation base commit: `840b6609a443e7ed3252c1c83224d3d9c5c16b49`（`origin/develop`、PR #149 のマージ）。B 開始時に `develop` の head で再固定する。**[RUNTIME_REFACTOR_CYCLES.md](RUNTIME_REFACTOR_CYCLES.md) のマージ後に始める**（同じファイルは触らないが、例外型の namespace が変わった後の `using` を前提にする）
- implementation head commit: 未固定（Phase B で記録）
- risk: `high`（アプリ起動・終了の順序、失敗経路、Player の receipt に現れる stage 名、派生クラスの公開面に触れる。実行時挙動は変えないが、変えていないことの証明が主な作業になる）
- owner: root（依頼者）。A2 / B / C / C' の担当は Phase ごとに選ぶ
- created: 2026-10-31
- expires: 2026-11-30 または置換 revision の成立時。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/04-app-startup.md` §4.2（クラス構造）と §4.3（解放の保証）。§4.7「Content Directory の起動時選択」の契約文は変えない（実装の置き場所だけが変わる）。新しい Architecture 文書は作らない
- 関連スライス: [RUNTIME_REFACTOR_CYCLES.md](RUNTIME_REFACTOR_CYCLES.md)（先行）。`S-4_FULL_SPEC_WORLD_AUTHORING.md` の残スライス **d5b** は `AbstractApplicationInitializer` を触る可能性がある（d5a は `CreateVfxBackend` hook と `VfxRuntimeHost` 配線を足した）。**本スライスの Phase B と d5b の Phase B を同時に走らせない。** 順序は owner が決める（§2.5 U4）
- Phase A snapshot の取得案内・状態: A1 入力版は本文を収録した commit と本文の content sha256 で固定する（文書だけなので stage は不要）。値は A2 開始時に §5「A2 独立レビュー」へ記録する。A3 凍結版は未生成
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A2（独立レビュー 3 観点）。
  - 次の作業: §5 の観点で本書を独立にレビューし、指摘を §5「A2 独立レビュー」へ記録する。A3 で owner と主担当が採否を決め、§2.5 の未決事項を閉じ、凍結版の commit と content sha256 を記録する。
  - 引き継ぐ commit: 本書を収録した計画ブランチの commit（A2 開始時に §5 へ転記）。
  - 必要な権限: なし（文書のみ）。Unity Editor・テスト・資格情報は不要。
  - 停止規則: A3 の凍結は owner の承認を待つ。同じ session で Phase B に入らない。
  - 引き渡さないもの: なし（Phase A 内）。

### PR 状態ブロック

```text
状態: Phase A の 作業中（A1 初稿）
Phase A: revision 0、commit <計画 PR 作成時に記録>
implementation base / head: 840b6609 / 未固定
次の担当: Phase A2 独立レビュー。次の作業: §5 の 3 観点でレビューし結果を §5 へ記録
HANDOFF: docs/handoff/RUNTIME_REFACTOR_BOOTSTRAP.md
Evidence 取得案内: 未生成
未決事項: §2.5 の U1〜U5
```

## 1. 目的と対象外

- 目的: `Runtime/Bootstrap/AbstractApplicationInitializer.cs`（1,248 行、1 クラス）から、3 フェーズの起動順序と派生クラス向け hook の template だけを残し、変更理由の異なる 4 つの責務（Content Directory の起動、旧リモートカタログの案内経路、UICommon の取得、Framework 常駐サービスの集合）をそれぞれのクラスへ移す。移した結果として、Bootstrap folder から BuildContent / Config / DebugSocketServices の internal 型への参照を 0 にし、Content Directory の起動は BuildContent 側の public な 1 型だけを通る形にする。これは Bootstrap を独立 assembly にする後続スライスの前提である。
- 対象外（所有する後続）:
  - protected / internal の公開面（hook、accessor、nested 型、static 入口）の追加・削除・signature 変更: 本スライスでは一切行わない。必要なら Phase A を再開する
  - 起動順序・終了順序・失敗経路・stage 名の変更。性能改善。旧リモートカタログ経路の削除（通常起動から外れているが、`20-variant-checkout-workflow.md` が案内として残している）
  - namespace `OneStarMaker.Runtime` → `OneStarMaker.Runtime.Bootstrap` への移動（`AppInitializer.cs` の `using` が変わるので SampleGame の変更になる）: Bootstrap を独立 assembly にするスライス
  - `Runtime/Config/RequiredJsonFileConfigProvider`（internal）の public 化や移動: Config は Bootstrap と同じ assembly に残す見込みなので触らない。asmdef Stage 2 の A0 で決める
  - `Runtime/Rendering/Vfx/Implements/VfxRuntimeHost`（internal）と `ActiveVfxRuntimeHost`: S-4d program の所有。本スライスは呼び出し位置を `FrameworkServiceSet` の中へ動かすだけで、型・可視性・生成と破棄の順序を変えない。Bootstrap → Rendering の internal 参照 1 件は残り、asmdef Stage 2 の入力として記録する
  - `UseRequiredPlayerFileConfiguration`（BS4 Player）の protected-key 契約、fail-closed の条件、receipt の schema: 変えない
  - SampleGame の製品コード（`AppInitializer.cs` を含む）: 変更 0
  - `AbstractApplicationInitializer` の XML doc 冒頭の使用例と `04-app-startup.md` §4.5 の実装ルール: 変えない
- 現況（base `840b6609` の実測）:
  - 1,248 行、19 フィールド、3 つの protected static 入口（`BootstrapSubsystemRegistration` / `BootstrapBeforeSceneLoad` / `BootstrapAfterSceneLoad`）、6 つの protected accessor、23 の protected virtual / abstract hook、protected nested 型 2 つ（`PlayerContentStage`、`PlayerContentFixtureResult`）、protected static 1 つ（`AppendFixtureStages`）、internal static 2 つ（`ShouldNotifyPlayerContentFailure`、`ActiveVfxRuntimeHost`）。一覧は §3.5。
  - 最長メソッド `InitializeAfterSceneLoad` は 203 行（284〜486 行目）。Framework 全体で最長。
  - **他 folder の internal 型への参照（Runtime 内で Bootstrap だけが持つ）:** BuildContent 8 型（`ContentDirectorySession`、`UnityContentDirectoryBackend`、`PlayerContentConfiguration`、`PlayerBakedContentConfiguration`、`ContentDeliveryFiles`、`ContentInstallReceipt`、`InstalledRevisionVerifier`、`VerifiedInstalledRevision`）、Config 1 型（`RequiredJsonFileConfigProvider`）、DebugSocketServices 1 型（`DebugSocketTelemetrySink`）、Rendering 1 型（`VfxRuntimeHost`）。加えて `AssetManagement` の internal メソッド 3 つ（`InstallContentDirectory`、`CloseContentDirectoryAsync`、`CompleteContentDirectoryPlayStop`）を `_assetManagement is AssetManagement.AssetManagement` の型確認を経て呼ぶ。
  - **変更理由の混在。** 同じクラスに (a) 3 フェーズの順序と hook（起動契約）、(b) `AppConfig` から Content Directory 構成を解く判定（1034〜1107 行目、`ReadContentDirectoryConfiguration` + nested `ContentDirectoryConfiguration`）と session の登録・close（568〜591 行目、366〜420 行目の失敗時 cleanup、ReleaseAll の 2 分岐）、(c) 旧リモート Addressables catalog の追加ロードと revision 乖離警告（628〜781 行目、5 メソッド。`TryLoadRemoteCatalogAsync` に呼び出しは無く、`InitializeAfterSceneLoad` 内のコメント「旧リモート Addressables catalog 追加ロードは通常起動から外した」が残る）、(d) UICommon Scene / prefab の取得 3 経路と EventSystem の生成（488〜566 行目）、(e) logger / DebugSocket / UpdateSystemHost / VfxRuntimeHost の生成・開始・破棄順（841〜879 行目、896〜966 行目）が入っている。
  - **隠れた結合。** `SampleGame/Tests/Bootstrap/InvalidCompanionConfigStartupTests.cs` は reflection で private フィールド `_assetManagement` を差し替える。`OneStarMaker/Tests/BuildContent/PlayerContentConfigurationTests.cs` は `ShouldNotifyPlayerContentFailure` と nested `PlayerContentStage` / `AppendFixtureStages` を派生 harness から使う。S-4d5a が足した SampleGame の Play テスト群は `ActiveVfxRuntimeHost` を読む。
  - **観測可能な契約。** AfterSceneLoad の stage 名は 16 個で、失敗時に `OnAfterSceneLoadInitializationFailed(stage, ex)` と `OnPlayerContentStartupFailedAsync(stage, …)` に渡り、BS4 Player の receipt に残る: `before-scene-load`、`register-content-directory`、`load-ui-common`、`wait-ui-common-ready`、`load-scene-resource-map`、`start-framework-services`、`initialize-app-services`、`create-scene-factory`、`resolve-scene-variant`、`create-scene-director`、`register-loaded-scenes`、`load-player-first-scene`、`run-player-content-smoke`、`unload-player-first-scene`、`close-player-content-directory`、`complete-player-content-smoke`。
  - **現在の順序（不変条件の基準）。** BeforeSceneLoad: CTS 生成 → `Application.quitting` 購読 → `AssetManagement` 生成 → `BuildConfig`（required Player なら `PlayerBakedContentConfiguration` もここで生成） → `ReadContentDirectoryConfiguration` → `CreateUpdateSystemHost` → `AppTelemetry.Thresholds` → `CreateDebugSocketService` → `LogDebugSocketBootstrapStatus` → `CreateLoggerFactory` → `VfxRuntimeHost.Create` → `RegisterDefaultTelemetrySink`。AfterSceneLoad: （directory かつ非 required なら）session 登録 → UICommon 取得 → `WaitForPanelReadyAsync` → SceneResourceMap（Player / directory / Addressables の 3 経路） → `StartFrameworkServicesAsync` → `OnServicesInitializing` → `CreateSceneFactory` → `ResolveSceneVariant` → （directory かつ未登録なら）session 登録 → representation の上書き → `SceneDirector` 生成 → `BindSceneDirector` → `BindSceneLifecycle`（VFX） → `RegisterAlreadyLoadedScenes` → （firstScene があれば）Player content smoke の 6 段。ReleaseAll: `_cts.Cancel` → `DisposeVfx` → DebugSocket → UpdateSystemHost → LoggerFactory → CTS → `_bakedPlayerContent` / `_config` / `_contentDirectoryConfiguration` → `SceneDirector.Dispose` → `AssetManagement.ReleaseAll`（+ `CompleteContentDirectoryPlayStop` または `BeginSynchronousShutdown`） → map → UICommon GO → `_uiSceneHandle` → EventSystem GO → `AppTelemetry.Shutdown`。
  - 失敗経路: AfterSceneLoad の catch は、first scene がロード済みなら unload → directory が登録済みなら close → `OnAfterSceneLoadInitializationFailed` → （required Player なら）`OnPlayerContentStartupFailedAsync` → `ReleaseAll` の順で、各段の例外は記録して元の例外を隠さない。`OperationCanceledException` は非 required のときだけ成功扱い。
  - 設計文書: `04-app-startup.md` §4.2 のクラス構造図は現在のメソッド構成を前提にしている。§4.7 には「Editor 設定読取の未決」（`BuildConfig` が directory 判定より前に `JsonFileConfigProvider` で Addressables を読む）が記録されており、本スライスはこの未決を解決も正当化もしない。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: `AbstractApplicationInitializer` の起動・終了順序と派生クラス向け公開面を変えずに、Content Directory 起動・旧リモートカタログ・UICommon 取得・Framework 常駐サービスを変更理由ごとのクラスへ分け、Bootstrap から BuildContent / Config / DebugSocketServices の internal 型への参照を 0 にできるか。
- 判定定義: §2.1 の不変条件と成立条件を §2.2 の受け入れ条件で全て確認できれば GO。一つでも未達なら NO-GO。スパイクではないので CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし（A3 後に作る場合は本書と別の記録にする）

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I1 公開面不変:** §3.5 の一覧にある protected / internal メンバーの名前、signature、アクセス修飾子、nested の位置、`static` の有無を変えない。追加もしない。private フィールド `_assetManagement` の名前と型を変えない（SampleGame.Tests の reflection）。
- **I2 起動順序不変:** §1 の「現在の順序」にある各処理の相対順序を変えない。stage 名 16 個の文字列と、各処理に対応する stage の割当を変えない。
- **I3 終了順序不変:** `ReleaseAll` の解放順（§1）を変えない。複数回呼んでも安全であることを保つ。
- **I4 失敗経路不変:** 各失敗点で呼ばれる hook と、その前に行う cleanup の順序、例外の握り方（元の例外を隠さない、cleanup の例外は記録だけ）を変えない。`ContentDirectoryException` の `Code` とメッセージを変えない。`OperationCanceledException` の扱いを変えない。
- **I5 internal 越境 0:** 変更後の `Runtime/Bootstrap/**` に BuildContent / Config / DebugSocketServices の internal 型の識別子が現れない（Rendering の `VfxRuntimeHost` だけは対象外として残す。§1）。`AssetManagement` の internal メソッド 3 つを Bootstrap から直接呼ばない（`ContentDirectoryStartup` 経由にする）。
- **I6 境界不変:** `.asmdef` の変更 0 件。`unity/Assets/SampleGame/` の変更 0 件（Tests を含む）。新しい public 型は §3 に列挙した 1 つ（`ContentDirectoryStartup`）と public メソッド 1 つ（`DebugSocketService.CreateTelemetrySink`）だけ。
- **I7 Telemetry 不変:** AppStartup span は BeforeSceneLoad と AfterSceneLoad の 2 つのまま。payload の `stage`（成功時 `BeforeSceneLoad` / `AfterSceneLoad`、失敗時は到達 stage）を変えない。
- **I8 新しい静的状態を作らない:** `ActiveVfxRuntimeHost` 以外の static 可変状態を増やさない。service locator を作らない。

**成立条件（起きなければならないこと）**

- **S1 サイズ:** `AbstractApplicationInitializer.cs` が 900 行以下。`InitializeAfterSceneLoad` が 110 行以下。
- **S2 Content Directory の facade:** `Runtime/BuildContent/ContentDirectoryStartup.cs`（public sealed class）が存在し、構成の解決（旧 `ReadContentDirectoryConfiguration` + `PlayerBakedContentConfiguration` 生成）、session の登録と install（旧 `InstallConfiguredContentDirectoryAsync`）、close、Play 停止時の完了（`CompleteContentDirectoryPlayStop` / `BeginSynchronousShutdown`）を所有する。Bootstrap は この型の public メンバーだけを呼ぶ。
- **S3 旧リモートカタログの隔離:** `Runtime/Bootstrap/LegacyRemoteCatalogProbe.cs`（internal）に旧 5 メソッドが移り、initializer から消える。呼び出し経路の有無は現状のまま（増やさない・減らさない）。
- **S4 UICommon 取得の隔離:** `Runtime/Bootstrap/BootstrapUiCommonLoader.cs`（internal）が UICommon の 3 経路と EventSystem 生成、両 GameObject と scene handle の破棄を所有する。
- **S5 常駐サービス集合:** `Runtime/Bootstrap/FrameworkServiceSet.cs`（internal）が logger / DebugSocket / UpdateSystemHost / VfxRuntimeHost を保持し、`StartAsync` と `Dispose` の順序を所有する。DebugSocket の telemetry sink は `DebugSocketService.CreateTelemetrySink()`（新 public）で得る。
- **S6 判定表の単体テスト:** `ContentDirectoryStartup.Resolve` の判定表（§2.2 AC9）が Unity Editor Play に依存しない EditMode テストで検証される。
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

  かつ `InvalidCompanionConfigStartupTests`（5 件。`create-scene-factory` で失敗する経路、`HasSceneDirector` / `HasAssetManagement` が false に戻ること）が変更なしで合格する。
- **AC3（I2, I3, I4）** B が §1 の「現在の順序」を、head の `InitializeBeforeSceneLoad` / `InitializeAfterSceneLoad` / `ReleaseAll` と、移動先の各メソッドを辿って 1 行ずつ対応付けた表を §6 に書く。C は表と実コードを照合する。対応が無い行、順序が入れ替わった行が 1 つでもあれば不合格。合格例: `StartFrameworkServicesAsync` の本文が `FrameworkServiceSet.StartAsync` に移り、呼び出し位置が `OnServicesInitializing` の直前のまま。不合格例: VFX の `BindSceneLifecycle` が `RegisterAlreadyLoadedScenes` の後になる。
- **AC4（I4）** 既存テスト `PlayerContentConfigurationTests`（9 件）、`SampleGame.Tests.Editor.Build.BuildContentDirectoryIntegrationTests`（1 件。Editor 統合）、`Tests/Editor/Build/ContentDirectorySessionTests` が変更なしで合格する。
- **AC5（I5）** 次が 0 行を返す。

  ```bash
  grep -rn -E "ContentDirectorySession|UnityContentDirectoryBackend|PlayerContentConfiguration|PlayerBakedContentConfiguration|ContentDeliveryFiles|ContentInstallReceipt|InstalledRevisionVerifier|VerifiedInstalledRevision|DebugSocketTelemetrySink|RequiredJsonFileConfigProvider|InstallContentDirectory|CloseContentDirectoryAsync|CompleteContentDirectoryPlayStop" unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap
  ```

  `RequiredJsonFileConfigProvider` は `BuildConfig` が required Player の構成を読むために使っている。U2 の判断で `ContentDirectoryStartup` 側（required 構成の読取を facade が担う）か、Config を Bootstrap と同じ assembly に残す前提で例外とするかを A3 で決める。A1 案は前者（§3）。
- **AC6（I6）** `git diff --stat base..head` に `.asmdef` と `unity/Assets/SampleGame/` が無い。`public` を含む追加行が `ContentDirectoryStartup.cs` と `DebugSocketService.cs` の `CreateTelemetrySink` 以外に無い（`git diff base..head | grep -E '^\+.*\bpublic\b'` で確認）。
- **AC7（I7）** `AppTelemetry.StartSpan(... AppStartup ...)` の呼び出しが Bootstrap folder に 2 箇所、`CreateTimingMemoryTelemetry(... stage: ...)` の `stage` 値が `"BeforeSceneLoad"` と `success ? "AfterSceneLoad" : startupStage` のまま。
- **AC8（S1）** `wc -l` で `AbstractApplicationInitializer.cs` ≤ 900、`InitializeAfterSceneLoad` の開始行から対応する閉じ括弧まで ≤ 110 行。
- **AC9（S2, S6）** `Tests/BuildContent/ContentDirectoryStartupTests.cs` が次の判定表を 1 行 1 テストで固定する。各テストは `AppConfig` を辞書 provider で組み立て、Unity の `Application` に依存する値（Editor Play か、install root）は引数で渡す。
  | 入力 | 期待 |
  |---|---|
  | `content:runtimeMode` 未指定、installed pair なし | `ContentDirectoryException(InvalidConfiguration)`、メッセージは現行のまま |
  | `addressables` | `null`（Addressables 互換起動） |
  | `directory`、Editor Play でない、required Player でない | `InvalidConfiguration`（Editor Play 限定） |
  | `installedRevisionPath` と `manifestSha256` の片方だけ | `InvalidConfiguration` |
  | 未知の mode | `InvalidConfiguration` |
  | required Player、`runtimeMode` が baked と不一致 | `InvalidConfiguration`（protected key） |
  | required Player、pair の片方だけ | `InvalidConfiguration` |
  | `directory`、Editor Play、`directoryPath` / `buildIdentity` / `representation` のいずれかが空 | `InvalidConfiguration` |
  | `directory`、Editor Play、正規化できない path | `InvalidConfiguration`（`ContentRevisionGate.TryNormalize` 失敗） |
  | `directory`、Editor Play、存在しない directory | `InvalidConfiguration` |
  | `directory`、Editor Play、存在する temp directory | 非 null、`Path` が正規化済み、`FirstScene` が空、`IsInstalled` が false |
  installed pair の verify 成功経路と Player の `PlayerContentConfiguration.Resolve` 経路は既存の `PlayerContentConfigurationTests` と `BuildContentDirectoryIntegrationTests` が覆う（新規テストでは重複させない）。
- **AC10（S2 の失敗経路, I4）** `ContentDirectoryStartup.InstallAsync` は、install が失敗したとき登録済み session を close してから元の例外を再送出する（現行 `InstallConfiguredContentDirectoryAsync` の catch と同じ）。EditMode テストで、既に directory を install 済みの `AssetManagement` に対して 2 回目の `InstallAsync` を呼ぶと `InvalidOperationException` が返り、session は close 済み（`ContentDirectorySession` の observable な状態、または偽 `IContentNativeDirectory` の呼び出し記録）であることを確認する。既存 `ContentDirectorySessionTests` の fixture（temp directory の index / manifest）を再利用する。再利用できない場合は「未確認。初回確認は Phase C」とし、B が理由を §6 に書く。
- **AC11（S3）** `LegacyRemoteCatalogProbe.cs` に `TryLoadAsync` / URL 解決 / revision 比較 / `build-info.json` URL 導出 / ローカル revision 解決が移り、`AbstractApplicationInitializer.cs` に `Addressables.LoadContentCatalogAsync`、`UnityWebRequest`、`RemoteCatalogRuntimeBridge` の参照が無い。`DeriveBuildInfoUrl` 相当の純関数に 3 ケース（末尾 `/` あり、`/` なし、空）の単体テストを足す（`Tests/Bootstrap/`）。
- **AC12（S4）** `AbstractApplicationInitializer.cs` に `SceneManager.GetActiveScene`、`EventSystem`、`InputSystemUIInputModule`、`GetComponent<UICommon>` の参照が無く、`BootstrapUiCommonLoader` が required Player のときに Addressables fallback を拒否する現行の例外（メッセージ同一）を投げる。
- **AC13（S5）** `AbstractApplicationInitializer.ReleaseAll` の service 解放が `FrameworkServiceSet.Dispose` 1 呼び出しになり、その内部順序が VFX → DebugSocket → UpdateSystemHost → LoggerFactory である（AC3 の表で確認）。`_debugSocketTelemetrySinkRegistered` 相当の「一度だけ登録」が保たれる。
- **AC14（S7, I6）** `pwsh tools/run-tests.ps1`（空 filter、EditMode）合格、テスト件数が base + 新規（AC9 の 11 件 + AC10 の 1 件 + AC11 の 3 件）以上。`pwsh tools/contract-audit.ps1` 合格。

### 2.3 ここでは答えない問いと所有スライス

- Bootstrap を `OneStarMaker.Runtime.Bootstrap` assembly に切り、namespace を揃える: asmdef Stage 2 のスライス（未作成）
- `VfxRuntimeHost` を public にするか、Bootstrap と Rendering の境界をどう引くか: asmdef Stage 2 の A0。S-4d program の所有物なので本スライスは触らない
- `04-app-startup.md` §4.7「Editor 設定読取の未決」（`JsonFileConfigProvider` が Addressables を読む）: 本スライスは `BuildConfig` の provider 列を変えない。未決は未決のまま残す
- `UpdateBehaviourAdapter` や `InputManager` のように製品配線の無い入口を initializer から配線するか: asmdef Stage 3 / 各機能の所有スライス
- `AbstractApplicationInitializer` を 500 行以下にするために hook を interface へ括り出す等の公開面の再設計: 本スライスでは行わない。必要なら新しい Phase A

### 2.4 本文へ転記した実装制約（`AGENTS.md` の常時契約から）

- 依存は Game → Framework の一方向。asmdef 参照の追加は行わない。
- アセットは `IAssetManagement` 経由。`BootstrapUiCommonLoader` と map の取得は現行と同じ `IAssetManagement` の公開メソッドを使う。
- `SceneState` の 14 値を変えない。状態変更は `SceneLifecycleManager` が所有する（本スライスは読まない）。
- 公開 API のログ抽象は `ILogger<T>`。新しい public 型（`ContentDirectoryStartup`）の public メンバーに `ZLogger*` 型を出さない。現行 initializer は `Debug.Log*` を使っており、移動先でも同じ呼び出しを保つ（ログ文言を変えない）。
- Update は `UpdateSystemRuntime` に登録する。`FrameworkServiceSet` は `UpdateSystemHost` の生成・破棄を移すだけで、登録の経路を変えない。
- Editor コードを Runtime アセンブリに置かない。現行の `#if UNITY_EDITOR` 3 箇所（`ResolveRemoteCatalogUrl`、`ResolveLocalRevision`、`ResolveSceneVariant`）はそのまま移す。
- `record` を使わない。新規 `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object`（UICommon の GameObject、EventSystem の GameObject、`EventSystem.current`）は `== null` / `!= null` で判定する。現行コードもそうなっている。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- 参照 0 を削除理由にしない（旧リモートカタログ経路は隔離して残す）。
- Unity Editor でのコンパイル確認は Phase B が行ってよい。Unity テストの実行・判定は Phase C。`unity test` / `unity run` は使わず `pwsh tools/run-tests.ps1`。

### 2.5 未決事項（A2 で意見を求める）

- **U1 `ContentDirectoryStartup` の入力の渡し方。** A1 案は `Resolve(AppConfig config, in ContentDirectoryStartupContext context)` で、`context` は `Target`（`"StandaloneWindows64-Player"`、現行 const）、`IsEditorPlayMode`（`Application.isEditor && Application.isPlaying` を呼び出し側で評価）、`UseRequiredPlayerFileConfiguration`、`RequiredPlayerConfig`（required のとき `BuildConfig` が読んだ captured 値の `AppConfig`、それ以外は null）、`InstallRoot`（`Path.GetDirectoryName(Application.dataPath)`）を持つ public readonly struct。Unity に依存する値を全て引数にするのは AC9 のテストのため。代替は `Resolve` が内部で `Application` を読む（テストが Editor Play に依存して AC9 が成立しない）。
- **U2 `RequiredJsonFileConfigProvider` の扱い。** A1 案は `BuildConfig` に残す（Config は Bootstrap と同じ assembly に残す見込みなので越境ではない）。AC5 の grep からは除外する。代替は required 構成の読取まで `ContentDirectoryStartup` に移す（facade が Config の責務を持つので A1 は採らない）。
- **U3 `FrameworkServiceSet` を本スライスに含めるか。** 含めない場合、initializer は約 950 行で S1 を満たさない。含める場合、VFX の生成・破棄（S-4d5a が決めた順序）を set の中へ移す。A1 は含める。A3 で「B1 = S2 + S3（asmdef の前提）、B2 = S4 + S5（サイズ）」に分ける選択肢も残す。分ける場合は本書を 2 つの HANDOFF に分け、B1 を先に凍結する。
- **U4 S-4d5b との順序。** d5b が `AbstractApplicationInitializer` を触るなら d5b を先に終える。触らないなら本スライスを先に進めてよい。owner が d5b の A0 を見て決める。
- **U5 private メソッドの名前。** `_assetManagement` 以外に reflection で読まれている private メンバーは見つかっていない。B は Phase B 開始時に `GetField(` / `GetMethod(` / `nameof(` の grep を SampleGame.Tests と OneStarMaker.Tests で再確認し、見つかれば名前を保つ。

## 3. 責務マップ

配置は全て既存 folder。新しい asmdef・folder・namespace は作らない。

### 3.1 ファイルと責務

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 許可する依存 | 公開面 | 現在行 → 予想 |
|---|---|---|---|---|---|---|
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 3 フェーズの起動順序、失敗境界、派生クラス向け hook の template | 起動契約（順序・stage・hook）が変わるとき | App 寿命。下の 4 クラスを所有し `ReleaseAll` で破棄 | 下の 4 クラス、`SceneDirector` / `UICommon` / `IAssetManagement` / `AppConfig` / `AppTelemetry` の public API | §3.5 のまま | 1,248 → 約 820（非分割判断は §3.3） |
| `Runtime/BuildContent/ContentDirectoryStartup.cs`（新規、public sealed） | `AppConfig` から Content Directory の起動構成を解き、session の登録・install・close・Play 停止時の完了を所有する | Content Directory の起動規則（mode、pair、protected key、Editor Play 限定、verify）が変わるとき | initializer が BeforeSceneLoad で `Resolve` し、ReleaseAll で `CompletePlayStop` / `BeginSynchronousShutdown`。session の寿命は従来どおり | 同 folder の internal 型全部、`AssetManagement` の internal メソッド 3 つ、`ContentDirectoryException`（CYCLES 後は `OneStarMaker.Runtime.AssetManagement`） | public: `static ContentDirectoryStartup? Resolve(AppConfig, in ContentDirectoryStartupContext)`、`string Path` / `BuildIdentity` / `Representation` / `FirstScene`、`bool IsInstalled`、`UniTask InstallAsync(AssetManagement)`、`UniTask CloseAsync(AssetManagement)`、`void CompletePlayStop(AssetManagement)`、`void BeginSynchronousShutdown()`。internal: `VerifiedInstalledRevision? VerifiedRevision`。`ContentDirectoryStartupContext` は public readonly struct（U1） | 0 → 約 230 |
| `Runtime/Bootstrap/LegacyRemoteCatalogProbe.cs`（新規、internal sealed） | 旧リモート Addressables catalog の追加ロードと revision 乖離警告（案内のみの旧経路） | 旧経路の案内文・URL 規則が変わるとき、または退役するとき | 状態なし。`AppConfig` を受け取る | Addressables、`UnityWebRequest`、`RemoteCatalogRuntimeBridge`、`RemoteBuildInfo` | internal: `UniTask TryLoadAsync(AppConfig config)`、internal static `string DeriveBuildInfoUrl(string)` | 0 → 約 160 |
| `Runtime/Bootstrap/BootstrapUiCommonLoader.cs`（新規、internal sealed） | UICommon の取得 3 経路（ロード済み Scene / directory logical key / Addressables address）と EventSystem の生成、両 GameObject と scene handle の破棄 | UICommon の取得経路・EventSystem の構成が変わるとき | initializer が所有。GameObject は App 寿命 | `IAssetManagement`、`UICommon`、`SceneManager`、`EventSystem`、`InputSystemUIInputModule`、`SceneAssetDescription` | internal: `UniTask<UICommon> LoadAsync(IAssetManagement, BootstrapUiCommonSource source)`、`void Dispose()`。`BootstrapUiCommonSource` は internal readonly struct（mode と key / address） | 0 → 約 110 |
| `Runtime/Bootstrap/FrameworkServiceSet.cs`（新規、internal sealed） | logger / DebugSocket / UpdateSystemHost / VfxRuntimeHost の保持、`StartAsync`、telemetry sink の一度だけの登録、`Dispose` の順序 | 常駐サービスの集合と開始・停止順が変わるとき | initializer が所有。生成は initializer の hook（`Create*`）が行い、インスタンスを受け取る | `DebugSocketService`（public）、`UpdateSystemHost`、`AppLoggerFactory`、`VfxRuntimeHost`（internal、S-4d 所有）、`AppTelemetry` | internal: ctor(logger, debugSocket, updateHost, vfxHost)、`UniTask StartAsync(CancellationToken)`、`UpdateSystemHost? UpdateSystemHost`、`VfxRuntimeHost? Vfx`、`void Dispose()` | 0 → 約 120 |
| `Runtime/DebugSocketServices/DebugSocketService.cs` | （既存。追加のみ） | sink の生成を service の責務にする | — | — | public: `ITelemetrySink CreateTelemetrySink()`（`DebugSocketTelemetrySink` は internal のまま） | 391 → 約 400 |
| `Tests/BuildContent/ContentDirectoryStartupTests.cs`（新規） | AC9 の判定表と AC10 | — | — | — | — | 0 → 約 220 |
| `Tests/Bootstrap/LegacyRemoteCatalogProbeTests.cs`（新規） | AC11 の 3 ケース | — | — | — | — | 0 → 約 40 |
| `unity/Assets/Docs/Architecture/04-app-startup.md` | harvest 先（Phase D）。§4.2 の図を新構成（initializer + 4 クラス）に、§4.3 の `ReleaseAll` 図に `FrameworkServiceSet.Dispose` / `ContentDirectoryStartup.CompletePlayStop` を反映 | — | — | — | — | +15 程度 |

### 3.2 配置と依存を選んだ理由

- **`ContentDirectoryStartup` を BuildContent に置く。** 使う型が全て BuildContent の internal で、Content Directory の起動規則は BuildContent の変更主体（Delivery / verify / protected key）と一緒に変わる。Bootstrap に置くと internal 越境が残る。public にするのは facade の 1 型だけで、`ContentDirectorySession` 等は internal のまま。
- **`AssetManagement` の internal メソッドも facade が呼ぶ。** `InstallContentDirectory` / `CloseContentDirectoryAsync` / `CompleteContentDirectoryPlayStop` は directory 専用の入口で、BuildContent と AssetManagement は asmdef Stage 2 でも同じ assembly（`Assets`）に置く見込み。Bootstrap がこれを直接呼ぶ現状は、`_assetManagement is AssetManagement.AssetManagement` の型確認を 4 箇所に散らしている。facade に寄せれば型確認は 1 箇所になる。
- **旧リモートカタログは Bootstrap folder に残す。** Addressables 互換経路の案内であり、BuildContent（Content Directory）の責務ではない。退役するときに 1 ファイル消せる形にしておく。
- **UICommon 取得と常駐サービスは internal。** 派生クラスは hook 経由で値を渡すだけで、これらの型を直接使わない。公開面を増やさないため internal にする。
- **hook は initializer に残す。** 公開面を変えない（I1）。`FrameworkServiceSet` は hook が作ったインスタンスを受け取る構成にして、派生クラスが override する場所を変えない。
- **新しい interface や抽象は作らない。** 4 クラスは具象で、initializer が直接生成する。差し替え点は既存の hook のままにし、テストは判定表（純関数）と既存の harness で足りる。

### 3.3 行数警報

- **500 行:** `AbstractApplicationInitializer.cs` は約 820 行で残る。**非分割妥当**と判断する根拠: 残る内容は (a) XML doc と使用例 約 60 行、(b) protected 公開面の宣言と doc 約 250 行、(c) 3 フェーズの順序と失敗境界 約 350 行、(d) `BuildConfig` / `RegisterAlreadyLoadedScenes` / map 取得 約 100 行。(b) は公開面なので動かせない（I1）。(c) は「起動順序の契約」という 1 つの変更理由。(d) は順序と同じ理由で変わる。これ以上分けるには hook を interface へ括り出す公開面の再設計が要り、本スライスの対象外。A2 はこの判断を審査する。
- **3 責務:** 変更後の initializer は「順序 + hook + 失敗境界」で 1 責務。4 クラスは各 1 責務。
- **50% 増:** 既存ファイルで 50% 以上増えるものは無い。`DebugSocketService.cs` は +10 行。

### 3.4 harvest 先

`04-app-startup.md` §4.2 と §4.3（Phase D）。§4.7 の契約文は変えない。`20-variant-checkout-workflow.md` の旧リモート案内は、クラス名を書いていなければ変更不要（B が確認）。

### 3.5 公開面の一覧（I1 の基準。base `840b6609`）

変えない・増やさない・減らさないもの。

- protected static: `BootstrapSubsystemRegistration(AbstractApplicationInitializer)`、`BootstrapBeforeSceneLoad(AbstractApplicationInitializer)`、`BootstrapAfterSceneLoad(AbstractApplicationInitializer)`、`AppendFixtureStages(List<PlayerContentStage>, IReadOnlyList<PlayerContentStage>)`
- protected accessor: `Config`、`SceneDirector`、`AssetManagement`、`Vfx`、`LoggerFactory`、`UpdateCoordinator`
- protected abstract: `CreateSceneFactory()`、`GetUICommonPrefabAddress()`、`GetSceneResourceMapAddress()`、`CreateLoadingDisplay()`
- protected virtual: `CreateVfxBackend()`、`ResolveSceneVariant()`、`GetDirectoryBootstrapSceneLogicalKey()`、`GetDirectoryBootstrapMapLogicalKey()`、`GetConfigFilePath()`、`UseRequiredPlayerFileConfiguration`、`GetRequiredPlayerConfigurationPath()`、`LoadPlayerSceneResourceMap()`、`GetEnvironmentVariablePrefix()`、`OnServicesInitializing(CancellationToken)`、`OnAfterSceneLoadInitializationFailed(string, Exception)`、`OnPlayerContentReadyAsync(IAssetManagement, CancellationToken)`、`OnPlayerContentShutdownCompletedAsync(IReadOnlyList<PlayerContentStage>)`、`OnPlayerContentStartupFailedAsync(string, Exception, IReadOnlyList<PlayerContentStage>)`、`CreateLoggerFactory(Stream?)`、`CreateDebugSocketService(AppConfig)`、`CreateDebugCommandDispatcher()`、`CreateUpdateSystemHost()`、`RegisterDefaultTelemetrySink()`
- protected nested: `enum PlayerContentStage`（10 値、順序固定）、`sealed class PlayerContentFixtureResult`
- internal static: `ShouldNotifyPlayerContentFailure(bool)`、`ActiveVfxRuntimeHost { get; private set; }`
- private だが名前を保つもの: フィールド `_assetManagement`（SampleGame.Tests の reflection）

## 4. 実装計画

- 変更対象: §3.1 の表。
- 順序（各手順を 1 commit にし、手順ごとに Unity Editor でコンパイルを確認する）:
  1. `ContentDirectoryStartup` と `ContentDirectoryStartupContext` を BuildContent に新設し、`ContentDirectoryStartupTests`（AC9 / AC10）を先に書く。initializer の `ReadContentDirectoryConfiguration` / `ContentDirectoryConfiguration` / `InstallConfiguredContentDirectoryAsync` / 失敗時 close / `ReleaseAll` の 2 分岐を facade 呼び出しに置き換える。フィールド 3 つ（`_contentDirectoryConfiguration`、`_contentDirectorySession`、`_bakedPlayerContent`）を `_contentDirectory`（`ContentDirectoryStartup?`）1 つにする。
  2. `LegacyRemoteCatalogProbe` へ 5 メソッドを移す（本文は変えない）。AC11 のテスト。
  3. `BootstrapUiCommonLoader` へ `LoadUICommonAsync` / `EnsureEventSystem` と GameObject / handle の破棄を移す。
  4. `DebugSocketService.CreateTelemetrySink()` を足し、`FrameworkServiceSet` へ `StartFrameworkServicesAsync` / `DisposeVfx` / service の破棄順を移す。`ActiveVfxRuntimeHost` の設定・解除は initializer に残す。
  5. `InitializeAfterSceneLoad` を、Player content smoke の 6 段（`RunPlayerContentSmokeAsync`）と失敗時 cleanup（`CleanupAfterStartupFailureAsync`）の private メソッドに分け、本体を 110 行以下にする。stage 名の代入位置は変えない。
  6. AC1 / AC2 / AC5 / AC6 / AC7 / AC8 のコマンドを実行し、AC3 の対応表を §6 に書く。`pwsh tools/contract-audit.ps1`。
- Phase B から Phase A へ差し戻す条件: §3.5 の公開面を変えないと分けられない。順序を変えないとコンパイルできない、または例外の握り方を変えないと移せない箇所がある。`ContentDirectoryStartup` に §3.1 にない public メンバーが要る。`VfxRuntimeHost` の型・可視性を変える必要がある。AC9 の判定表に Unity の `Application` を読まないと再現できない行がある。SampleGame のテストを変えないと通らない。
- 対象外を維持する方法: `.asmdef` を開かない。`unity/Assets/SampleGame/` に触れない（AC6 の `git diff --stat`）。hook の宣言行を編集しない（AC1）。`04-app-startup.md` の変更は Phase D まで行わない。

## 5. テストとレビュー計画

- 単体テスト: `ContentDirectoryStartupTests`（判定表 11 件 + install 失敗 1 件）、`LegacyRemoteCatalogProbeTests`（3 件）。Unity なしで検証できる範囲は判定表と URL 導出。session の登録・close は temp directory と既存 fixture を使う EditMode。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `OneStarMaker.Tests.BuildContent`、`OneStarMaker.Tests.Bootstrap`、`SampleGame.Tests.Bootstrap`、`SampleGame.Tests.Editor.Build`、`SampleGame.Tests.App`（`GameSceneLoggingTests` が起動経路を通る）。
- 判定必須テスト（GO 候補 head）: 全 EditMode 回帰（空 filter）。XML で新規 15 件と AC2 / AC4 の既存テストが収録されていること。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: 既存の `BuildContentDirectoryIntegrationTests`（Editor 統合、1 件、約 300 行）が Content Directory の build → install → 起動を通す。S-4d5a の Play テスト群（`SampleGame/Tests/Rendering/Vfx/*PlayTests`）が `ActiveVfxRuntimeHost` 経由で VFX 配線を確認する。どちらも全 EditMode 回帰に含まれる。Build は不要。
- 操作・実行時・目視条件の検証経路: **Editor の directory Play を 1 回**（AC3 の順序表の実機確認）。担当は動作確認の実行担当（AI）。環境: ローカル Unity Editor、Content Directory build 済みの install が Delivery に 1 つある状態（無ければ `20-variant-checkout-workflow.md` の手順で用意する。用意できなければ「未実施」と理由を書く）。操作: Title Scene から Play を開始し、Title が表示されたら停止する。観測: Console に `[AppInit] AfterSceneLoad: loading UICommon.` → `loading SceneResourceMap.` → `Starting DebugSocket transport` または `DebugSocket は無効です` の順で出ること、例外が無いこと、停止時に `Cannot find handle for scene` 等の例外が無いこと。保存する証拠: Console ログの text。合否は構造（ログ順序と例外 0）で判断し、画像は使わない。C / C' への受け渡しは evidence bundle。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: なし（directory Play は S-4 で反復済みの既知経路）。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし
- 高コスト証拠: なし
- 判定 C の照合 script の方式: なし（AC のコマンド、AC3 の対応表、XML のテスト名・件数で判定する）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC1 / AC2 / AC5 / AC6 / AC7 / AC8 のコマンド。
- A0/A1 主担当・モデル・ベンダー: Claude Code（Anthropic）。調査は本書 §1 の現況に転記済み。
- A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版: 下記。観点は (a) アーキテクチャゲート（4 クラスの責務と配置、`ContentDirectoryStartup` の public 面が最小か、§3.3 の非分割判断、U1〜U3）、(b) 不変条件の破り方（I2〜I4 を破る順序を 1 つ挙げられるか、AC3 の対応表方式で検出できるか、stage 名と receipt の互換）、(c) テスト境界（AC9 の判定表が現行コードの全分岐を覆うか、AC10 の fixture 再利用の実行可能性、SampleGame.Tests の reflection 以外の隠れた結合）。A2 は「計画対象は実装済みと仮定して」評価し、未実装を理由に差し戻さない。
- A3 統合担当・モデル・採否: 未定（owner + 主担当）。U3 の分割判断と U4 の順序を A3 で閉じる。
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
