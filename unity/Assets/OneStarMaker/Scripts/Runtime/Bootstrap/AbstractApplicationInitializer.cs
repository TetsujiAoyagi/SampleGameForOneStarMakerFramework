#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.Config;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Foundation.UpdateSystem.World;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Foundation.Logging;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime.DebugSocketServices;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.Config;
using OneStarMaker.Runtime.ContentResolution;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UpdateSystem;
using OneStarMaker.Runtime.UpdateSystem.Hosting;
using OneStarMaker.Runtime.UISystem;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime
{
    /// <summary>
    /// アプリケーション起動時の初期化を担う抽象クラス。
    /// 3 フェーズで初期化を実行する:
    ///   SubsystemRegistration → 前回セッションのクリーンアップ
    ///   BeforeSceneLoad       → サービス群の同期初期化
    ///   AfterSceneLoad        → ロード済みシーンの登録 + 初回シーンのロード
    ///
    /// <para>派生クラスの実装パターン:</para>
    /// <code>
    /// sealed class AppInitializer : AbstractApplicationInitializer
    /// {
    ///     static readonly AppInitializer s_instance = new();
    ///
    ///     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    ///     static void Sub()    =&gt; BootstrapSubsystemRegistration(s_instance);
    ///
    ///     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    ///     static void Before() =&gt; BootstrapBeforeSceneLoad(s_instance);
    ///
    ///     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    ///     static void After()  =&gt; BootstrapAfterSceneLoad(s_instance);
    ///
    ///     protected override ISceneFactory CreateSceneFactory() =&gt; new MySceneFactory(Config!);
    ///     protected override string GetUICommonPrefabAddress()  =&gt; "Assets/Prefabs/UICommon.prefab";
    ///     protected override string GetSceneResourceMapAddress() =&gt; "Assets/SceneMap/Map.asset";
    ///     protected override ILoadingDisplay CreateLoadingDisplay() =&gt; new MyLoadingDisplay();
    ///     protected override string GetConfigFilePath()         =&gt; Path.Combine(Application.streamingAssetsPath, "app-config.json");
    ///     protected override string GetEnvironmentVariablePrefix() =&gt; "ONESM_";
    /// }
    /// </code>
    /// </summary>
    public abstract class AbstractApplicationInitializer
    {
        // build root は Unity の BuildTarget と Player subtarget を一つの文字列で保存する。
        // Editor 側の Content Directory 出力名を変更する場合は、この照合値も同時に更新する。
        private const string ContentDirectoryTarget = "StandaloneWindows64-Player";

        // ─── Fields ───

        private AppConfig? _config;
        private SceneDirector? _sceneDirector;
        private SceneResourceMap? _sceneResourceMap;
        private CancellationTokenSource? _cts;

        /// <summary>
        /// Framework の常駐サービス（logger / DebugSocket / UpdateSystemHost / VFX）。BeforeSceneLoad の最初の Create* より前に
        /// 空で作り、hook の戻り値を 1 つずつ入れる。ReleaseAll で Dispose して null に戻す。
        /// </summary>
        private FrameworkServiceSet? _services;

        /// <summary>
        /// Addressables Load / Release の一元管理。
        /// BeforeSceneLoad で生成し、AfterSceneLoad 以降の全 Addressables 操作をここ経由にする。
        /// Application.quitting 時に ReleaseAppAll で App 常駐分を解放する。
        /// </summary>
        private IAssetManagement? _assetManagement;
        private ContentDirectoryConfiguration? _contentDirectoryConfiguration;
        private ContentDirectorySession? _contentDirectorySession;

        /// <summary>
        /// 要求時解決の loader。Editor の directory の Play で、登録が検証済みの時だけ起動が作る（Player と検証済みでない登録では null）。
        /// SceneDirector の Scene の読み込みと、host の code の直接の要求が共有する。
        /// </summary>
        private ContentRequestLoader? _contentLoader;
#if UNITY_EDITOR
        /// <summary>
        /// loader の policy の先頭に置いた Editor の Play の複製（snapshot）の供給元。snapshot を宣言した Play で、loader を配線した時だけ作る。
        /// 起動の計画は、これが無い snapshot の Play を起動の失敗にする。
        /// </summary>
        private EditorSnapshotProvider? _snapshotProvider;
#endif
        private PlayerBakedContentConfiguration? _bakedPlayerContent;

        /// <summary>
        /// 失敗の通知が読む Player の種類。<see cref="BuildConfig"/> が baked の生の値から、baked の検証より前に決める。
        /// <see cref="BootstrapBeforeSceneLoad"/> の冒頭で Smoke に戻し、<see cref="ReleaseAll"/> では消さない:
        /// 失敗の通知は ReleaseAll の後（BeforeSceneLoad の失敗）にも呼ばれ、その時 baked は既に消えているため。
        /// 起動の分岐はこの latch ではなく検証済みの baked を読む。
        /// </summary>
        private PlayerContentKind _latchedPlayerKind = PlayerContentKind.Smoke;
        private Exception? _beforeSceneLoadFailure;
        private bool _beforeSceneLoadSucceeded;

        /// <summary>
        /// UICommon の取得と、取得で得た GameObject（UICommon と、loader が作った入力用の GameObject）の寿命。AfterSceneLoad で取得を await する前に入れ、
        /// 取得の途中で失敗しても ReleaseAll が同じ位置で破棄できるようにする。
        /// </summary>
        private BootstrapUiCommonLoader? _uiCommonLoader;

        // ─── Protected accessors ───

        /// <summary>アプリケーション設定。BeforeSceneLoad 完了後に有効。</summary>
        protected AppConfig? Config => _config;

        /// <summary>現在の SceneDirector。BeforeSceneLoad 完了後に有効。</summary>
        protected SceneDirector? SceneDirector => _sceneDirector;

        /// <summary>
        /// Addressables Load / Release 管理。
        /// UICommon / SceneResourceMap のロードや SceneDirector への注入に使用する。
        /// </summary>
        protected IAssetManagement? AssetManagement => _assetManagement;

        /// <summary>
        /// 登録した Content Directory を供給元にする要求時解決の loader。SceneDirector を作る前に決まり、
        /// Editor の directory の Play で登録が検証済みの時だけ非 null。Player、Addressables の Play、検証済みでない登録では null。
        /// 返る記録は、実際に読んだ directory の identity と表現を指す。解放は <see cref="AssetManagement"/> の既存の API で行う。
        /// </summary>
        protected ContentRequestLoader? ContentLoader => _contentLoader;

        /// <summary>
        /// Game が VFX を起動・登録する窓口。BeforeSceneLoad 完了後は非 null で、VFX backend を構成していない・非対応の
        /// 環境でも何もしない handle を返す（Game に対応可否の分岐を書かせない）。
        /// Scene class へは factory の ctor 注入で渡す（service locator にしない）。
        /// </summary>
        protected IVfxSystem? Vfx => _services?.Vfx?.System;

        /// <summary>
        /// Framework 標準のロガーファクトリ。
        /// DebugSocketService が有効な場合は、その realtime stream もここへ配線される。
        /// </summary>
        protected AppLoggerFactory? LoggerFactory => _services?.LoggerFactory;

        /// <summary>Framework 標準の更新 coordinator。BeforeSceneLoad 完了後に有効。</summary>
        protected UpdateCoordinator? UpdateCoordinator => _services?.UpdateSystemHost?.Coordinator;

        /// <summary>
        /// テスト用: 起動中の initializer が所有する VFX の composition。Play 中のテストが、生成・Tick 要素の登録・
        /// Scene lifecycle への接続を確かめるために読む。Dispose で null に戻る。
        /// </summary>
        internal static VfxRuntimeHost? ActiveVfxRuntimeHost { get; private set; }

        // ─── Static entry points ───

        /// <summary>
        /// SubsystemRegistration フェーズ。前回セッションの残存状態をクリーンアップする。
        /// Enter Play Mode Settings で Domain Reload が無効の場合に必要。
        /// </summary>
        protected static void BootstrapSubsystemRegistration(AbstractApplicationInitializer instance)
        {
            try
            {
                // domain reload / player restart の切替点で session / sequence を切り替え、
                // 旧 session の Log / Telemetry に新 ID を混ぜない。
                UnitySessionCorrelationContext.ResetForNewPlayerSession();
                // Application / SystemInfo はメインスレッド専用。Welcome 組み立て前に焼き込む。
                UnitySessionAttributes.Capture();
                instance.ReleaseAll();
                instance._beforeSceneLoadFailure = null;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// BeforeSceneLoad フェーズ。サービス群を同期的に初期化する。
        /// </summary>
        protected static void BootstrapBeforeSceneLoad(AbstractApplicationInitializer instance)
        {
            instance._beforeSceneLoadSucceeded = false;
            instance._beforeSceneLoadFailure = null;
            instance._latchedPlayerKind = PlayerContentKind.Smoke;
            try
            {
                instance.InitializeBeforeSceneLoad();
                instance._beforeSceneLoadSucceeded = true;
            }
            catch (Exception ex)
            {
                instance._beforeSceneLoadFailure = ex;
                Debug.LogException(ex);
                // 起動設定の検証失敗後に残った AssetManagement/CTS を根拠として
                // AfterSceneLoad が既定 Addressables へ進むと、明示 directory 指定を黙って無視する。
                // 元の失敗を保持したまま部分初期化だけを回収し、次フェーズを閉じる。
                try { instance.ReleaseAll(); }
                catch (Exception cleanupException) { Debug.LogException(cleanupException); }
            }
        }

        /// <summary>
        /// AfterSceneLoad フェーズ。ロード済みシーンの登録と初回シーンのロードを行う。
        /// </summary>
        protected static void BootstrapAfterSceneLoad(AbstractApplicationInitializer instance)
        {
            if (!instance._beforeSceneLoadSucceeded)
            {
                Debug.LogError("[AppInit] BeforeSceneLoad が失敗したため AfterSceneLoad をスキップします。");
                if (instance._beforeSceneLoadFailure != null)
                {
                    // Player smoke は config 欠損も process failure として観測する必要がある。
                    // BeforeSceneLoad は同期 callback なので、AfterSceneLoad で非同期の失敗通知を起動する。
                    // この経路の派生 hook は receipt と exit code を同期的に確定して返す契約。
                    // fire-and-forget にすると config 欠損時の証拠が process 終了に負ける。
                    if (ShouldNotifyPlayerContentFailure(instance.UseRequiredPlayerFileConfiguration))
                        instance.OnPlayerContentStartupFailedAsync(
                            "before-scene-load",
                            instance._beforeSceneLoadFailure,
                            Array.Empty<PlayerContentStage>()).GetAwaiter().GetResult();
                    instance._beforeSceneLoadFailure = null;
                }
                return;
            }
            try
            {
                instance.InitializeAfterSceneLoad().Forget();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        // ─── Lifecycle: BeforeSceneLoad ───

        private void InitializeBeforeSceneLoad()
        {
            var span = AppTelemetry.StartSpan(Foundation.Core.TelemetryStartType.AppStartup, null);
            var success = false;
            // Contract v3: AppStartup でも memory before/after/delta を載せる（metadata: default 廃止）。
            var memBefore = RuntimeTelemetryMetadataFactory.CaptureMemorySnapshot();

            try
            {
                _cts = new CancellationTokenSource();

                // Application.quitting で確実にリソースを解放する
                Application.quitting += OnApplicationQuitting;

                // Config の JSON 読み込みも Addressables 経由のため、AssetManagement を先に生成する
                _assetManagement = new AssetManagement.AssetManagement();

                // Config を構築（ConfigFile → 環境変数 → コマンドライン引数 の優先順）
                _config = BuildConfig();
                _contentDirectoryConfiguration = ReadContentDirectoryConfiguration(_config);
                if (_contentDirectoryConfiguration != null && _assetManagement is AssetManagement.AssetManagement sealable)
                {
                    // directory と決まった直後に Addressables の取得経路を封じる。config の読み込みだけが封じる前の
                    // Addressables 利用で、以後の bootstrap（UICommon・SceneResourceMap）は directory の読み口を使う。
                    // Player の required JSON の経路は Addressables を使わないので、封じても失うものは無い。
                    // この 1 行は検証 Play の起動ログで「封じた」ことを確かめる目印でもある。
                    sealable.SealAddressables();
                    Debug.Log("[AppInit] Content directory mode: Addressables backend sealed after reading the configuration. " +
                              "Only Content Directory loads are accepted from here on.");
                }

                // 常駐サービスの入れ物を最初の Create* より前に作る。以後の hook は作り済みのサービスを accessor で読め、
                // 途中の hook が投げても作り済みのものは ReleaseAll で破棄される。
                var services = new FrameworkServiceSet();
                _services = services;

                // Updater は scene object の Awake register を受ける必要があるため、
                // BeforeSceneLoad の時点で install しておく。
                services.UpdateSystemHost = CreateUpdateSystemHost();
                KeepServicesAttached(services);

                // テレメトリ閾値を Config から読み込み
                AppTelemetry.Thresholds = new TelemetryThresholds(_config);

                // DebugSocket は Config 解決後でないと起動条件を決められない。
                // ここでは service と logger factory を組み立てるだけに留め、
                // 実際の socket 待受開始は AfterSceneLoad 側へ寄せる。
                services.DebugSocket = CreateDebugSocketService(_config);
                KeepServicesAttached(services);
                LogDebugSocketBootstrapStatus(_config);
                services.LoggerFactory = CreateLoggerFactory(services.DebugSocket?.RealtimeStream);
                KeepServicesAttached(services);
                // VfxSystem の ctor が能力判定を 1 回だけ行う。BeforeSceneLoad の後に Awake する Scene
                // （Editor の Play で開いていた Scene）からも判定結果を読めるよう、ここで作る。
                // 失敗は他の Create* hook と同じく BeforeSceneLoad の失敗にする（VFX を黙って無効にしない）。
                services.Vfx = VfxRuntimeHost.Create(CreateVfxBackend(), services.LoggerFactory, services.UpdateSystemHost!.Coordinator);
                KeepServicesAttached(services);
                ActiveVfxRuntimeHost = services.Vfx;
                // ── テレメトリ Sink 登録 ──
                RegisterDefaultTelemetrySink();

                success = true;
            }
            finally
            {
                FinishStartupSpan(span, memBefore, success, "BeforeSceneLoad");
            }
        }

        /// <summary>
        /// hook の中で ReleaseAll が走ると（派生クラスが <see cref="BootstrapSubsystemRegistration"/> を呼ぶ等）、set は破棄されて
        /// <c>_services</c> から外れるが、hook はその後も戻り値を返し、BeforeSceneLoad は続く。分割前は戻り値を initializer の field へ
        /// 直接代入していたので、その戻り値は次の hook の accessor から見え、後の ReleaseAll が破棄した。同じにするため、外れていたら
        /// 同じ set を field に戻す（破棄済みの set のメンバーは null なので、分割前に field が null に戻っていたのと同じ状態になる）。
        /// <c>_services</c> を別の set に替えるのは InitializeBeforeSceneLoad の冒頭だけなので、ここで見る値は null か同じ set に限る。
        /// </summary>
        private void KeepServicesAttached(FrameworkServiceSet services)
        {
            _services ??= services;
        }

        // ─── Lifecycle: AfterSceneLoad ───

        private async UniTaskVoid InitializeAfterSceneLoad()
        {
            var progress = new StartupProgress();
            var span = AppTelemetry.StartSpan(Foundation.Core.TelemetryStartType.AppStartup, null);
            var success = false;
            CancellationToken ct = default;
            // Contract v3: AfterSceneLoad 区間の memory delta + 最終 stage 名を payload に載せる。
            var memBefore = RuntimeTelemetryMetadataFactory.CaptureMemorySnapshot();
            try
            {
                if (_cts == null)
                    throw new InvalidOperationException("BeforeSceneLoad lifetime token is unavailable.");
                ct = _cts.Token;
                // 旧リモート Addressables catalog 追加ロードは通常起動から外した（LegacyRemoteCatalogProbe に隔離）。

                if (_contentDirectoryConfiguration != null && !UseRequiredPlayerFileConfiguration)
                {
                    progress.Stage = "register-content-directory";
                    await InstallConfiguredContentDirectoryAsync();
                }

                progress.Stage = "load-ui-common";
                Debug.Log("[AppInit] AfterSceneLoad: loading UICommon.");
                _uiCommonLoader = new BootstrapUiCommonLoader();
                var uiCommon = await _uiCommonLoader.LoadAsync(_assetManagement!, new BootstrapUiCommonSource(
                    _contentDirectoryConfiguration != null,
                    () => UseRequiredPlayerFileConfiguration,
                    GetDirectoryBootstrapSceneLogicalKey,
                    GetUICommonPrefabAddress));

                progress.Stage = "wait-ui-common-ready";
                await uiCommon.WaitForPanelReadyAsync(UseRequiredPlayerFileConfiguration, ct);
                if (UseRequiredPlayerFileConfiguration)
                    progress.PlayerContentStages.Add(PlayerContentStage.UiCommonReady);

                progress.Stage = "load-scene-resource-map";
                Debug.Log("[AppInit] AfterSceneLoad: loading SceneResourceMap.");
                _sceneResourceMap = UseRequiredPlayerFileConfiguration
                    ? LoadPlayerSceneResourceMap()
                    : _contentDirectoryConfiguration != null
                        ? await LoadDirectorySceneResourceMapAsync()
                        : await LoadSceneResourceMapAsync();

                if (_assetManagement == null || _cts == null)
                {
                    Debug.LogError("[AppInit] BeforeSceneLoad が未完了のため AfterSceneLoad をスキップします。");
                    return;
                }

                // Framework 標準の長寿命サービスを先に起動する。
                // 派生クラスが OnServicesInitializing を override しても、
                // base 呼び出し忘れで DebugSocket が起動しない事故を避けるため。_services は上の _cts と一緒に null になる。
                progress.Stage = "start-framework-services";
                await _services!.StartAsync(ct);

                // Phase 2 拡張ポイント: HostedService 等の追加初期化
                progress.Stage = "initialize-app-services";
                await OnServicesInitializing(ct);

                progress.Stage = "create-scene-factory";
                var sceneFactory = CreateSceneFactory();

                progress.Stage = "resolve-scene-variant";
                var sceneVariant = ResolveSceneVariant()
                    ?? throw new InvalidOperationException("ResolveSceneVariant returned null.");

                var useContentDirectory = _contentDirectoryConfiguration != null;
                if (_contentDirectoryConfiguration != null && _contentDirectorySession == null)
                {
                    progress.Stage = "register-content-directory";
                    await InstallConfiguredContentDirectoryAsync();
                }
                if (_contentDirectoryConfiguration != null)
                    sceneVariant = _contentDirectoryConfiguration.Representation;
                if (UseRequiredPlayerFileConfiguration && _contentDirectorySession != null)
                    progress.PlayerContentStages.Add(PlayerContentStage.ContentDirectoryRegistered);

                // Player の構成では配線せず、今の起動経路のままにする。
                var contentFallbackRepresentation = string.Empty;
                if (_contentDirectorySession != null && !UseRequiredPlayerFileConfiguration)
                {
                    progress.Stage = "wire-content-resolution";
                    contentFallbackRepresentation = WireContentResolution(_contentDirectorySession);
                }

                progress.Stage = "create-scene-director";
                _sceneDirector = new SceneDirector(
                    sceneFactory,
                    uiCommon,
                    _sceneResourceMap,
                    CreateLoadingDisplay(),
                    _assetManagement,
                    sceneVariant,
                    useContentDirectory,
                    _contentLoader,
                    contentFallbackRepresentation);
                _services?.UpdateSystemHost?.BindSceneDirector(_sceneDirector);
                // 最初の Scene の登録・ロードより前に接続する（VfxSystem は途中接続の状態同期を約束しない）。
                _services?.Vfx?.BindSceneLifecycle(_sceneDirector.OnSceneEvent, _sceneDirector);

                if (_sceneDirector == null)
                {
                    Debug.LogError("[AppInit] BeforeSceneLoad が未完了のため AfterSceneLoad をスキップします。");
                    return;
                }

#if UNITY_EDITOR
                // Editor の Play 入口が snapshot を宣言した Play の計画。取り込み（複製の load）より前に決め、成り立たなければ複製を読まずに起動の失敗にする。
                progress.Stage = "plan-editor-snapshots";
                var snapshotPlan = EditorPlaySnapshotIntake.PlanStartup(
                    _config != null && _config.GetBool(EditorPlaySnapshotBridge.ExpectedConfigKey),
                    _contentDirectoryConfiguration != null && !UseRequiredPlayerFileConfiguration,
                    _contentLoader != null && _snapshotProvider != null,
                    EditorPlaySnapshotBridge.Declaration,
                    name => _sceneResourceMap!.GetSceneResource(name) != null);
                if (snapshotPlan.Failure != null)
                    throw new InvalidOperationException(snapshotPlan.Failure);

                if (snapshotPlan.RequestsSnapshotScenes)
                {
                    // 宣言の複製を全て物理的に load して照合する。封じは IAssetManagement の Addressables の取得経路だけが対象で、
                    // この段は IAssetManagement を通らず、Scene の台帳にも触らない（lifecycle に入れるのは次の段の明示の要求だけ）。
                    progress.Stage = "intake-editor-snapshots";
                    await IntakeEditorSnapshotsAsync(ct);

                    // load 済みの Scene の走査はしない。物理的に load されていることを lifecycle への要求にしないため、
                    // 入口が「起動時に要求する」と印した Scene だけを要求する。印の無い複製は load 済みのまま lifecycle の外に居る。
                    progress.Stage = "request-snapshot-scenes";
                    await RequestSnapshotScenesAsync(snapshotPlan.Requested, ct);
                }
                else
                {
#endif
                    // Editor のプレイモードで開いていたシーンを SceneDirector に登録する。
                    // PerformUnitySceneLoad が SceneManager.GetSceneByName でロード済みシーンを検出し、
                    // 再ロードせず RootGameObjects を返すため二重ロードは発生しない。
                    progress.Stage = "register-loaded-scenes";
                    await RegisterAlreadyLoadedScenes(ct);
#if UNITY_EDITOR
                }
#endif

                if (_contentDirectoryConfiguration != null && _contentDirectoryConfiguration.FirstScene.Length != 0)
                {
                    // 種類は検証済みの baked から読む（重ねた後の値は Resolve が baked と同じことを確かめ済み）。
                    switch (RouteFirstScene(UseRequiredPlayerFileConfiguration, ReadBakedPlayerKind()))
                    {
                        case PlayerFirstSceneRoute.Editor:
                            await LoadEditorFirstSceneAsync(progress, ct);
                            break;
                        case PlayerFirstSceneRoute.Smoke:
                            await RunPlayerContentSmokeAsync(progress, ct);
                            break;
                        case PlayerFirstSceneRoute.Playable:
                            await LoadPlayerFirstSceneAsync(progress, ct);
                            break;
                        default:
                            throw new InvalidOperationException("Unknown first scene route.");
                    }
                }

                success = true;
            }
            catch (OperationCanceledException) when (!UseRequiredPlayerFileConfiguration)
            {
                // アプリ終了によるキャンセル — 正常
                success = true; // キャンセルは失敗ではない
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AppInit] AfterSceneLoad failed at stage '{progress.Stage}': {ex}");
                await CleanupAfterStartupFailureAsync(progress, ex);
                // SceneDirector や SceneFactory は、途中まで初期化したアプリ固有サービスを参照している可能性がある。
                // 失敗した bootstrap を継続して stale な依存を使わせないため、派生側の回収後に
                // Framework の Director / Updater / AssetManagement も同じ失敗境界で破棄する。
                ReleaseAll();
            }
            finally
            {
                // 成功時は最終段階名ではなく区間名を載せる（失敗時だけ到達 stage で切り分ける）。
                FinishStartupSpan(span, memBefore, success, success ? "AfterSceneLoad" : progress.Stage);
            }
        }

        /// <summary>
        /// Editor directory Play の初回 Scene（App が config content:firstScene で渡す）。ロードしたまま Play を続け、
        /// Player の smoke（検証・unload・close・receipt）は行わない。ロードの失敗は起動の失敗で、別の Scene へ逃がさない。
        /// 例外は握らず <see cref="InitializeAfterSceneLoad"/> の catch へ出す。
        /// </summary>
        private async UniTask LoadEditorFirstSceneAsync(StartupProgress progress, CancellationToken ct)
        {
            var firstScene = _contentDirectoryConfiguration!.FirstScene;
            // 失敗のログ（AfterSceneLoad failed at stage ...）に Scene 名を残す。
            progress.Stage = "load-editor-first-scene:" + firstScene;
            await _sceneDirector!.AddScene(firstScene, null, ct);
            // 検証 Play の観測点。Unity の Scene のロードではなく SceneDirector の Stable を出す。
            Debug.Log($"[AppInit] Editor content directory first scene: scene={firstScene} " +
                      $"identity={_contentDirectoryConfiguration!.BuildIdentity} stable={_sceneDirector!.IsSceneStable(firstScene)}");
        }

        /// <summary>
        /// 遊べる Player の初回 Scene。load して Stable を 1 行の log に出し、load したまま起動を終える。
        /// 動作確認の尾（fixture・unload・close・receipt・Quit）は走らせず、成功の hook も無い（遊べる Player は成功を receipt に書かない）。
        /// load の失敗は起動の失敗で、例外は握らず <see cref="InitializeAfterSceneLoad"/> の catch へ出す。
        /// </summary>
        private async UniTask LoadPlayerFirstSceneAsync(StartupProgress progress, CancellationToken ct)
        {
            var firstScene = _contentDirectoryConfiguration!.FirstScene;
            progress.Stage = "load-player-first-scene:" + firstScene;
            await _sceneDirector!.AddScene(firstScene, null, ct);
            // 起動の到達を外から読む印（Player の log）。文言は判定の照合が探すので変えない。
            Debug.Log($"[AppInit] Player content first scene: scene={firstScene} " +
                      $"identity={_contentDirectoryConfiguration!.BuildIdentity} kind=playable stable={_sceneDirector!.IsSceneStable(firstScene)}");
        }

        /// <summary>Player の構成なら検証済みの baked の種類。Editor は baked が無く、分岐は種類を見ない。</summary>
        private PlayerContentKind ReadBakedPlayerKind()
        {
            if (!UseRequiredPlayerFileConfiguration) return PlayerContentKind.Smoke;
            var baked = _bakedPlayerContent ?? throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,
                "Baked Player content configuration is unavailable.");
            return baked.PlayerKind;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor の Play snapshot の取り込み。宣言の Scene を並び順に 1 つずつ additive で load して完了を待ち、全部を照合し、
        /// active Scene を Play の前と同じにする。宣言が無い・load できない・照合が合わないのはどれも起動の失敗で、別の供給元へ逃がさない。
        /// <para>
        /// 失敗した時は、この段が load した複製を、例外を既存の catch へ出す前に全て unload する。この時点の複製は SceneDirector に未登録で、
        /// 起動の失敗の後始末（<see cref="CleanupAfterStartupFailureAsync"/>、<see cref="ReleaseAll"/>）は Unity の Scene を unload しないので、
        /// 段のほかに回収する者がいない。load は 1 つずつ行うので、失敗の時点で進行中の load は無い。Play の終了による cancel では
        /// unload しない（Unity が片付ける）。unload の失敗は Error の log にし、元の例外を隠さない。
        /// </para>
        /// </summary>
        private static async UniTask IntakeEditorSnapshotsAsync(CancellationToken ct)
        {
            var declaration = EditorPlaySnapshotBridge.Declaration;
            var loadScene = EditorPlaySnapshotBridge.LoadScene;
            if (declaration == null || loadScene == null)
                throw new InvalidOperationException(
                    "This Play declared Editor scene snapshots, but the snapshot declaration is not available (it may have been lost in a domain reload).");
            try
            {
                foreach (var scene in declaration)
                {
                    var operation = loadScene(scene.CopyPath)
                        ?? throw new InvalidOperationException("The play snapshot of scene '" + scene.Identity + "' could not be loaded: " + scene.CopyPath);
                    await operation.ToUniTask(cancellationToken: ct);
                    var loadedScene = SceneManager.GetSceneByPath(scene.CopyPath);
                    if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                        throw new InvalidOperationException("The play snapshot of scene '" + scene.Identity + "' did not load: " + scene.CopyPath);
                }

                var failure = EditorPlaySnapshotIntake.Verify(declaration, CaptureLoadedScenes());
                if (failure != null) throw new InvalidOperationException(failure);

                foreach (var scene in declaration)
                {
                    if (scene.IsActive) SceneManager.SetActiveScene(SceneManager.GetSceneByPath(scene.CopyPath));
                    Debug.Log(EditorPlaySnapshotIntake.FormatIntakeLog(scene));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                await UnloadEditorSnapshotCopiesAsync(declaration);
                throw;
            }
        }

        /// <summary>
        /// 起動時の要求: 入口が印した複製だけを、宣言の並び順に SceneDirector へ要求する（要求ごとに 1 行 log）。
        /// <para>
        /// 失敗したら、load 済みの複製を取り込みの失敗と同じ規則で全て unload してから例外を出す。起動の失敗の後始末は Unity の Scene を
        /// unload しないので、段のほかに回収する者がいない。Play の終了による cancel では unload しない（Unity が片付ける）。
        /// </para>
        /// </summary>
        private async UniTask RequestSnapshotScenesAsync(IReadOnlyList<EditorPlaySnapshotScene> requested, CancellationToken ct)
        {
            try
            {
                foreach (var scene in requested)
                {
                    await _sceneDirector!.AddScene(scene.Identity, null, ct);
                    Debug.Log(EditorPlaySnapshotIntake.FormatRequestLog(scene.Identity, _sceneDirector!.IsSceneStable(scene.Identity)));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                var declaration = EditorPlaySnapshotBridge.Declaration;
                if (declaration != null) await UnloadEditorSnapshotCopiesAsync(declaration);
                throw;
            }
        }

        /// <summary>
        /// load 済みの宣言の複製を全て unload する（起動の段の失敗の後始末）。unload の失敗は Error の log にし、元の例外を隠さない。
        /// </summary>
        private static async UniTask UnloadEditorSnapshotCopiesAsync(IReadOnlyList<EditorPlaySnapshotScene> declaration)
        {
            foreach (var target in EditorPlaySnapshotIntake.SelectUnloadTargets(declaration, CaptureLoadedScenes()))
            {
                try
                {
                    var unload = SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(target.Path));
                    if (unload == null)
                        Debug.LogError("[AppInit] Editor play snapshot cleanup could not unload " + target.Path + ".");
                    else
                        await unload.ToUniTask();
                }
                catch (Exception unloadException)
                {
                    Debug.LogError("[AppInit] Editor play snapshot cleanup also failed for " + target.Path + ": " + unloadException);
                }
            }
        }

        private static IReadOnlyList<EditorPlaySnapshotLoadedScene> CaptureLoadedScenes()
        {
            var loaded = new List<EditorPlaySnapshotLoadedScene>(SceneManager.sceneCount);
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) loaded.Add(new EditorPlaySnapshotLoadedScene(scene.name, scene.path));
            }
            return loaded;
        }

#endif
        /// <summary>
        /// Player content smoke の 6 段（初回 Scene のロード → fixture → unload → directory の close → 完了通知）。
        /// 例外は握らず <see cref="InitializeAfterSceneLoad"/> の catch へ出す。stage は段に入るたびに progress へ書く。
        /// </summary>
        private async UniTask RunPlayerContentSmokeAsync(StartupProgress progress, CancellationToken ct)
        {
            progress.Stage = "load-player-first-scene";
            await _sceneDirector!.AddScene(_contentDirectoryConfiguration!.FirstScene, null, ct);
            progress.PlayerContentStages.Add(PlayerContentStage.FirstSceneStable);
            progress.Stage = "run-player-content-smoke";
            var fixtureResult = await OnPlayerContentReadyAsync(_assetManagement!, ct);
            AppendFixtureStages(progress.PlayerContentStages, fixtureResult.CompletedStages);
            if (fixtureResult.Failure != null) throw fixtureResult.Failure;
            progress.Stage = "unload-player-first-scene";
            await _sceneDirector!.UnloadScene(_contentDirectoryConfiguration!.FirstScene);
            progress.PlayerContentStages.Add(PlayerContentStage.FirstSceneUnloaded);
            progress.Stage = "close-player-content-directory";
            if (_assetManagement is not AssetManagement.AssetManagement playerAssets)
                throw new InvalidOperationException("Player content requires the framework asset manager.");
            await playerAssets.CloseContentDirectoryAsync();
            _contentDirectorySession = null;
            progress.PlayerContentStages.Add(PlayerContentStage.ContentDirectoryClosed);
            progress.Stage = "complete-player-content-smoke";
            await OnPlayerContentShutdownCompletedAsync(progress.PlayerContentStages.ToArray());
        }

        /// <summary>
        /// AfterSceneLoad の失敗時、ReleaseAll より前に行う後始末（Player 初回 Scene の unload → directory の close → 派生クラスへの通知）。
        /// 各段の失敗は記録するだけで、元の起動例外を隠さない。ReleaseAll は呼び出し元の catch がこの後に呼ぶ。
        /// </summary>
        private async UniTask CleanupAfterStartupFailureAsync(StartupProgress progress, Exception ex)
        {
            // 初回 Scene の unload は Player の失敗経路だけ（動作確認・遊べるの両方。receipt・Quit の前に token を返す）。
            // Editor は ReleaseAll と Play の終了に任せる。
            if (UnloadsFirstSceneOnStartupFailure(UseRequiredPlayerFileConfiguration)
                && _sceneDirector != null && _contentDirectoryConfiguration != null
                && _contentDirectoryConfiguration.FirstScene.Length != 0
                && _sceneDirector.IsSceneLoaded(_contentDirectoryConfiguration.FirstScene))
            {
                try
                {
                    // failure receipt より先に正式 unload を完了し、directory tokenを返す。
                    await _sceneDirector.UnloadScene(_contentDirectoryConfiguration.FirstScene);
                    progress.PlayerContentStages.Add(PlayerContentStage.FirstSceneUnloaded);
                }
                catch (Exception unloadException)
                {
                    Debug.LogError($"[AppInit] Player first scene cleanup also failed: {unloadException}");
                }
            }
            if (_assetManagement is AssetManagement.AssetManagement directoryAssetManagement
                && _contentDirectorySession != null)
            {
                try
                {
                    // 起動途中の directory は initializer が所有する。失敗時も root/native token を残さず、
                    // close 側の ResourcesInUse 判定や cleanup 失敗は元の起動例外を隠さず記録する。
                    await directoryAssetManagement.CloseContentDirectoryAsync();
                    _contentDirectorySession = null;
                    progress.PlayerContentStages.Add(PlayerContentStage.ContentDirectoryClosed);
                }
                catch (Exception closeException)
                {
                    Debug.LogError($"[AppInit] Content directory cleanup after startup failure also failed: {closeException}");
                }
            }
            // 派生側が OnServicesInitializing で常駐リソースを確保している場合、
            // その後段（SceneDirector 構築・初回シーン追加）の失敗では通常の終了処理まで残り続ける。
            // Framework 自身の ReleaseAll をここで呼ぶと診断用状態まで一律に失うため、
            // 所有者である派生クラスへ限定的な回収機会を渡す。その後に Player の失敗を通知する。
            var stage = progress.Stage;
            await RunStartupFailureHooksAsync(
                () => OnAfterSceneLoadInitializationFailed(stage, ex),
                ShouldNotifyPlayerContentFailure(UseRequiredPlayerFileConfiguration)
                    ? () => OnPlayerContentStartupFailedAsync(stage, ex, progress.PlayerContentStages.ToArray())
                    : null,
                // 元の起動失敗を隠さず、後始末の失敗は追加情報としてだけ記録する。
                (hook, hookException) => Debug.LogError(
                    $"[AppInit] Cleanup after failed AfterSceneLoad initialization also failed ({hook}): {hookException}"));
        }

        /// <summary>
        /// 起動の失敗の後の派生への通知の順と失敗境界。派生の後始末 → Player の失敗の通知の順に、<b>別々の</b> try で呼ぶ。
        /// 派生の後始末が投げても失敗の通知（派生はその中で exit code を確定して Quit する）を必ず呼ぶ。
        /// 1 つの try にすると、後始末の例外で Quit に届かず、起動に失敗した Player が動き続ける。
        /// 失敗の通知が null（Editor）なら呼ばない。どちらの例外も log に出すだけで、呼び出し元へ投げない。
        /// </summary>
        internal static async UniTask RunStartupFailureHooksAsync(
            Action derivedCleanup, Func<UniTask>? notifyPlayerFailure, Action<string, Exception> logFailure)
        {
            try
            {
                derivedCleanup();
            }
            catch (Exception cleanupException)
            {
                logFailure("derived cleanup", cleanupException);
            }
            if (notifyPlayerFailure == null) return;
            try
            {
                await notifyPlayerFailure();
            }
            catch (Exception notifyException)
            {
                logFailure("player content failure notification", notifyException);
            }
        }

        /// <summary>
        /// AppStartup span を閉じる（BeforeSceneLoad と AfterSceneLoad で共通）。payload の stage は呼び出し側が決める。
        /// </summary>
        private static void FinishStartupSpan(
            TelemetrySpan? span, RuntimeTelemetryMemorySnapshot memBefore, bool success, string stage)
        {
            var memAfter = RuntimeTelemetryMetadataFactory.CaptureMemorySnapshot();
            var (metadata, payload) = RuntimeTelemetryMetadataFactory.CreateTimingMemoryTelemetry(
                memBefore,
                memAfter,
                stage: stage);

            AppTelemetry.FinishSpan(
                span: span,
                metadata: metadata,
                isSuccess: success,
                level: TelemetryLevel.Summary,
                tags: null,
                payload: payload);
        }

        /// <summary>
        /// 1 回の AfterSceneLoad の進み具合。stage は失敗時に hook・receipt・telemetry へ渡す名前で、初期値は load-ui-common。
        /// 起動ごとに作り、static にしない。helper にも同じインスタンスを渡し、stage は helper の中でもここへ書く。
        /// helper の中の失敗でも catch・後始末・finally の telemetry が失敗した処理の stage を読むため
        /// （戻り値で返すと、helper の途中で投げたときに呼び出し前の古い stage が残る）。
        /// </summary>
        private sealed class StartupProgress
        {
            internal string Stage { get; set; } = "load-ui-common";
            internal List<PlayerContentStage> PlayerContentStages { get; } = new();
        }

        private async UniTask InstallConfiguredContentDirectoryAsync()
        {
            var configuration = _contentDirectoryConfiguration
                ?? throw new InvalidOperationException("Content directory configuration is unavailable.");
            // 上書きの決定がある時だけ 2 層で登録する。その例外は握らず起動の失敗にし、base だけの登録へやり直さない。
            var overlay = configuration.Overlay;
            if (overlay != null && configuration.VerifiedRevision == null)
                throw new InvalidOperationException("A content overlay requires a verified base revision.");
            var session = overlay != null
                ? ContentDirectorySession.RegisterVerifiedWithOverlay(configuration.VerifiedRevision!, overlay.Path, overlay.Identity)
                : configuration.VerifiedRevision != null
                ? ContentDirectorySession.RegisterVerified(configuration.VerifiedRevision, new UnityContentDirectoryBackend())
                : ContentDirectorySession.Register(configuration.Path, configuration.BuildIdentity, ContentDirectoryTarget);
            if (overlay != null)
                Debug.Log(ContentOverlayStartup.FormatRegistered(overlay.Identity, configuration.VerifiedRevision!.Identity, overlay.Path));
            try
            {
                if (_assetManagement is not AssetManagement.AssetManagement assetManagement)
                    throw new InvalidOperationException("The configured asset manager cannot install a content directory session.");
                assetManagement.InstallContentDirectory(session);
            }
            catch
            {
                // field に保存する前の失敗でも登録済み directory を閉じる。
                // cleanup が失敗した場合も元の起動失敗を診断できるよう両方を記録する。
                try { await session.CloseAsync(); }
                catch (Exception cleanupException)
                { Debug.LogError($"[AppInit] Content directory cleanup after install failure failed: {cleanupException}"); }
                throw;
            }
            _contentDirectorySession = session;
            WarnOnDeclarationMismatch(session.Declaration, configuration.Representation);
        }

        /// <summary>
        /// 登録した directory を要求時解決の供給元にする loader を作り、<see cref="_contentLoader"/> に入れて 1 行出す。
        /// 組み立ての判断は route が持ち、ここは呼び出しと log だけを置く。戻り値は SceneDirector が要求に明示する fallback の表現
        /// （配線しない時は空）。検証済みでない登録は配線せず、理由を 1 行出して今までどおり直接の経路で動く。
        /// 検証済みの登録で route が投げた例外は握らない（起動の失敗にする。黙って直接の経路で Play を続けない）。
        /// </summary>
        private string WireContentResolution(ContentDirectorySession session)
        {
            if (_assetManagement is not AssetManagement.AssetManagement assetManagement)
                throw new InvalidOperationException("The configured asset manager cannot serve content resolution.");
#if UNITY_EDITOR
            var snapshot = CreateEditorSnapshotProvider();
            RegisteredDirectoryRoute? route;
            string notCreatedReason;
            var created = snapshot == null
                ? RegisteredDirectoryRoute.TryCreate(session, assetManagement, out route, out notCreatedReason)
                : RegisteredDirectoryRoute.TryCreate(session, assetManagement, snapshot.Value.Provider, out route, out notCreatedReason);
            if (!created)
#else
            if (!RegisteredDirectoryRoute.TryCreate(session, assetManagement, out var route, out var notCreatedReason))
#endif
            {
                Debug.Log($"[AppInit] Content resolution: wired=false reason={notCreatedReason}");
                return string.Empty;
            }

            _contentLoader = route!.Loader;
            var identity = route.Provider.Identity;
            Debug.Log($"[AppInit] Content resolution: wired=true provider={route.Provider.Id} contentSet={identity.ContentSet} " +
                      $"revision={identity.Revision} target={identity.Target} manifestSha256={identity.Digest}");
#if UNITY_EDITOR
            if (snapshot != null)
            {
                // 預け元を据えて宣言の identity を予約する（以後、その identity は directory の content 入口から読めない）。
                // 予約の時点で同じ identity の Scene が台帳にあれば例外で、起動の失敗にする。
                assetManagement.InstallAdoptedSceneSource(snapshot.Value.Source);
                _snapshotProvider = snapshot.Value.Provider;
                Debug.Log($"[AppInit] Content resolution: snapshot provider={snapshot.Value.Provider.Id} " +
                          $"scenes={snapshot.Value.Provider.DeclaredCount} requested={snapshot.Value.RequestedCount}");
            }
#endif
            return route.FallbackRepresentation;
        }

#if UNITY_EDITOR
        /// <summary>
        /// この Play が snapshot を宣言していれば（config の key が真で、bridge に宣言がある）、預け元と供給元を同じ宣言の instance から作る。
        /// 宣言していなければ null。key があって宣言が無い Play は、ここでは作らず、起動の計画が起動の失敗にする。
        /// </summary>
        private (EditorSnapshotSceneSource Source, EditorSnapshotProvider Provider, int RequestedCount)? CreateEditorSnapshotProvider()
        {
            if (_config == null || !_config.GetBool(EditorPlaySnapshotBridge.ExpectedConfigKey))
                return null;
            var declaration = EditorPlaySnapshotBridge.Declaration;
            var loadScene = EditorPlaySnapshotBridge.LoadScene;
            if (declaration == null || loadScene == null)
                return null;

            var source = new EditorSnapshotSceneSource(EditorPlaySnapshotIntake.ToDeclaredScenes(declaration), new UnityEditorSnapshotSceneIo(loadScene));
            // 表現の指定が無い要求に申告する表現は、directory が代わりに返す Full と同じ値にする。
            var provider = new EditorSnapshotProvider(source, ContentEntryMatch.Full);
            return (source, provider, declaration.Count(x => x.RequestAtStartup));
        }
#endif

        /// <summary>
        /// root の宣言（runtime 既定値と表現の主値）と起動設定・実効表現を比べ、違いを Warning 1 行で出す。
        /// Framework は key を解釈しない。宣言の無い root は何も出さない。目的違いの directory を Play したことを
        /// Scene の欠落ではなく起動時の 1 行で分かるようにする。
        /// </summary>
        private void WarnOnDeclarationMismatch(BuildContentDeclaration? declaration, string effectiveRepresentation)
        {
            var config = _config;
            if (declaration == null || config == null)
                return;
            var differences = ContentDeclarationMismatch.Find(declaration,
                key => config.ContainsKey(key) ? config.GetString(key) : null, effectiveRepresentation);
            if (differences.Count == 0)
                return;
            Debug.LogWarning($"[AppInit] Content directory declaration '{declaration.Name}' differs from the startup configuration: " +
                             ContentDeclarationMismatch.Format(differences));
        }

        private async UniTask<SceneResourceMap> LoadDirectorySceneResourceMapAsync()
        {
            var logicalKey = GetDirectoryBootstrapMapLogicalKey();
            if (string.IsNullOrEmpty(logicalKey))
                throw new InvalidOperationException("Directory Editor Play requires a bootstrap SceneResourceMap logical key.");
            var handle = await _assetManagement!.LoadContentAssetAsync<SceneResourceMap>(
                logicalKey,
                "Full",
                AssetOwner.App);
            var value = handle.Value;
            if (value == null)
                throw new InvalidOperationException($"SceneResourceMap not found: {logicalKey}");
            return value;
        }

        private async UniTask<SceneResourceMap> LoadSceneResourceMapAsync()
        {
            var address = GetSceneResourceMapAddress();
            // ScriptableObject は App スコープで保持。ReleaseAll まで解放しない
            var handle = await _assetManagement!.LoadAssetAsync<SceneResourceMap>(
                AssetKey.FromAddress(address),
                AssetOwner.App);
            return handle.Value
                ?? throw new InvalidOperationException($"SceneResourceMap not found: {address}");
        }

        /// <summary>
        /// Editor でプレイモードに入った際、既にロード済みのシーンを SceneDirector に登録する。
        /// SceneResourceMap に未登録のシーン（テストシーン等）はスキップする。
        /// </summary>
        private async UniTask RegisterAlreadyLoadedScenes(CancellationToken ct)
        {
            var sceneCount = SceneManager.sceneCount;
            for (var i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                // SceneResourceMap に未登録のシーンはスキップ
                if (_sceneResourceMap!.GetSceneResource(scene.name) == null)
                {
                    Debug.Log($"[AppInit] SceneResourceMap に未登録のシーンをスキップ: {scene.name}");
                    continue;
                }

                // AddScene は冪等。既にロード済みならスキップされる。
                await _sceneDirector!.AddScene(scene.name, null, ct);
            }
        }

        private AppConfig BuildConfig()
        {
            var providers = new List<IConfigProvider>(3);

            if (UseRequiredPlayerFileConfiguration)
            {
                var captured = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                new RequiredJsonFileConfigProvider(GetRequiredPlayerConfigurationPath()).Load(captured);
                var required = new AppConfig(new IConfigProvider[] { new CapturedConfigProvider(captured) });
                // latch は検証より前に生の値で決める。検証（必須・値の範囲）が投げても、失敗の通知は種類に合う形で出せる。
                _latchedPlayerKind = PlayerBakedContentConfiguration.LatchPlayerKind(
                    required.GetString(PlayerBakedContentConfiguration.PlayerKindKey, string.Empty));
                _bakedPlayerContent = new PlayerBakedContentConfiguration(required);
                providers.Add(new CapturedConfigProvider(captured));
            }
            else
            {
                var configPath = GetConfigFilePath();
                if (!string.IsNullOrEmpty(configPath))
                    providers.Add(new JsonFileConfigProvider(configPath, _assetManagement!));
            }

            var envPrefix = GetEnvironmentVariablePrefix();
            providers.Add(new EnvironmentVariableConfigProvider(envPrefix));

            providers.Add(new CommandLineConfigProvider());

            return new AppConfig(providers);
        }

        private sealed class CapturedConfigProvider : IConfigProvider
        {
            private readonly IReadOnlyDictionary<string,string> _values;
            internal CapturedConfigProvider(IReadOnlyDictionary<string,string> values)=>_values=values;
            public void Load(Dictionary<string,string> store){foreach(var value in _values)store[value.Key]=value.Value;}
        }

        private void OnApplicationQuitting()
        {
            // quit の始まりの境。Player の log をこの行で「閉じる前」と「閉じた後」に分けて読む（Editor の Play の停止でも出る）。
            Debug.Log("[AppInit] Application quitting.");
            ReleaseAll();
        }

        /// <summary>
        /// 全リソースを解放する。SubsystemRegistration と Application.quitting の双方から呼ばれる。
        /// 複数回呼び出しても安全。
        ///
        /// Shutdown 契約（通常の UnloadScene 3フェーズとは別）:
        /// 1. SceneDirector.Dispose … 論理 Scene 台帳と SceneBase のみ破棄（AM Unload は呼ばない）
        /// 2. AssetManagement.ReleaseAll … Addressables Scene Unload なしで台帳 MarkUnloaded + 全アセット同期解放
        /// Play Mode 終了では Unity が先に Scene を解体するため、ここで Addressables Unload すると
        /// 「Cannot find handle for scene」になる。正式 Unload はゲーム中の UnloadScene 経路だけが担う。
        /// </summary>
        private void ReleaseAll()
        {
            _beforeSceneLoadSucceeded = false;
            Application.quitting -= OnApplicationQuitting;

            // まず framework service 側へ停止を通知する。
            _cts?.Cancel();

            // VFX は logger・UpdateSystemHost・SceneDirector・AssetManagement より前に回収する。
            // 回収時の診断ログを出せ、生きている coordinator から登録解除でき、
            // source asset は instance の破棄要求より後に解放される。
            // テスト用の static は、自分の host を指しているときだけ、host の Dispose より前に外す。
            var services = _services;
            if (services?.Vfx != null && ReferenceEquals(ActiveVfxRuntimeHost, services.Vfx))
            {
                ActiveVfxRuntimeHost = null;
            }

            // VFX → DebugSocket → UpdateSystemHost → LoggerFactory の順（順序の理由は FrameworkServiceSet.Dispose）。
            services?.Dispose();
            _services = null;

            _cts?.Dispose();
            _cts = null;
            _bakedPlayerContent = null;

            _config = null;
            _contentDirectoryConfiguration = null;

            // 論理台帳のみ。AssetManagement の Scene Unload は誘発しない。
            _sceneDirector?.Dispose();
            _sceneDirector = null;

            // Shutdown: 旧 Addressables Scene は再 unload せず、同期で全 owner 資産を解放。
            // directory の完全 drain は AM が同じスタックで終える。UniTask.GetResult は使わない。
            if (_assetManagement is AssetManagement.AssetManagement directoryAssets)
            {
                directoryAssets.ReleaseAll();
                if (_contentDirectorySession != null)
                    directoryAssets.CompleteContentDirectoryPlayStop();
            }
            else
            {
                _assetManagement?.ReleaseAll();
                _contentDirectorySession?.BeginSynchronousShutdown();
            }
            _assetManagement = null;
            _contentDirectorySession = null;
            _contentLoader = null;
#if UNITY_EDITOR
            _snapshotProvider = null;
#endif

            _sceneResourceMap = null;

            // UICommon の GameObject → scene handle の参照 → loader が作った入力用の GameObject の順に手放す（loader の Dispose の中の順）。
            // AssetManagement.ReleaseAll の後に置く。scene の unload は AM が担い、loader は unload しない。
            _uiCommonLoader?.Dispose();
            _uiCommonLoader = null;

            // テレメトリをシャットダウン（Sink の Flush + Dispose）
            AppTelemetry.Shutdown();
        }

        // ─── Template methods ───

        /// <summary>SceneBase のファクトリを生成する。</summary>
        protected abstract ISceneFactory CreateSceneFactory();

        /// <summary>
        /// VFX の実装（backend）を作る。BeforeSceneLoad で 1 回呼ばれ、所有権は Framework に移る
        /// （<see cref="System.IDisposable"/> なら ReleaseAll で Dispose する）。
        /// null なら未構成として扱い、VFX 要求は全て何もしない handle になり、診断ログが 1 回出る。
        /// 環境に依存する例外を ctor で投げない（環境の判定は <see cref="IVfxBackend.QueryCapability"/> だけが行う）。
        /// </summary>
        protected virtual IVfxBackend? CreateVfxBackend() => null;

        /// <summary>
        /// 起動中に一度だけ Scene Variant を解決する。Editor では active profile が設定値より優先する。
        /// </summary>
        protected virtual string ResolveSceneVariant()
        {
            var configuredValue = _config?.GetString("assets:sceneVariant", string.Empty) ?? string.Empty;
#if UNITY_EDITOR
            return SceneVariantResolver.Resolve(
                configuredValue,
                SceneVariantRuntimeBridge.EditorSceneVariantResolver,
                useEditorResolver: true);
#else
            return SceneVariantResolver.Resolve(configuredValue, null, useEditorResolver: false);
#endif
        }

        /// <summary>UICommon Prefab の Addressable アドレスを返す。</summary>
        protected abstract string GetUICommonPrefabAddress();

        /// <summary>SceneResourceMap の Addressable アドレスを返す。</summary>
        protected abstract string GetSceneResourceMapAddress();

        /// <summary>Editor directory Play が Content Directory から読む UICommon Scene の logical key。</summary>
        protected virtual string GetDirectoryBootstrapSceneLogicalKey() => string.Empty;

        /// <summary>Editor directory Play が Content Directory から読む SceneResourceMap の logical key。</summary>
        protected virtual string GetDirectoryBootstrapMapLogicalKey() => string.Empty;

        /// <summary>ローディング表示の実装を返す。</summary>
        protected abstract ILoadingDisplay CreateLoadingDisplay();

        /// <summary>
        /// 設定ファイルのパスを返す。空文字を返すとファイル読み込みをスキップする。
        /// デフォルトは StreamingAssets/app-config.json。
        /// </summary>
        protected virtual string GetConfigFilePath()
            => string.Empty;

        /// <summary>Player の構成（動作確認・遊べるの両方）だけが Addressables 前に必須 plain JSON を読む。</summary>
        protected virtual bool UseRequiredPlayerFileConfiguration => false;

        protected virtual string GetRequiredPlayerConfigurationPath()
            => throw new InvalidOperationException("A Player configuration path is required.");

        /// <summary>Player package に複写した graph metadata を返す。source/AssetDatabase は使わない。</summary>
        protected virtual SceneResourceMap LoadPlayerSceneResourceMap()
            => throw new InvalidOperationException("A Player scene resource map is required.");

        /// <summary>
        /// 環境変数のプレフィックスを返す（例: "ONESM_"）。
        /// プレフィックスに一致する環境変数のみが読み込まれる。
        /// 空文字の場合は全環境変数を対象とする。
        /// </summary>
        protected virtual string GetEnvironmentVariablePrefix() => "";

        private ContentDirectoryConfiguration? ReadContentDirectoryConfiguration(AppConfig config)
        {
            var specified = config.ContainsKey("content:runtimeMode");
            var mode = specified ? config.GetString("content:runtimeMode") : string.Empty;
            var installedRoot=config.GetString("content:installedRevisionPath",string.Empty);
            var installedDigest=config.GetString("content:manifestSha256",string.Empty);
            // 上書きの key は、addressables と決めて config を返さない分岐と Player の分岐より前に見る（key を残したまま base だけ・
            // addressables で始めない）。重ねられる設定は検証済みの base の分岐にだけ渡る。
            var overlay = ContentOverlayStartup.Decide(
                config.ContainsKey(ContentOverlayStartup.OverlayPathKey), config.GetString(ContentOverlayStartup.OverlayPathKey, string.Empty),
                config.ContainsKey(ContentOverlayStartup.OverlayIdentityKey), config.GetString(ContentOverlayStartup.OverlayIdentityKey, string.Empty),
                Application.isEditor && Application.isPlaying, UseRequiredPlayerFileConfiguration, specified, mode,
                installedRoot.Length > 0 && installedDigest.Length > 0);
            if (overlay.Kind == ContentOverlayStartupKind.Rejected)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration, overlay.Reason);
            if (UseRequiredPlayerFileConfiguration)
            {
                var baked=_bakedPlayerContent ?? throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,"Baked Player content configuration is unavailable.");
                // protected 値は mode 分岐より先に照合する。addressables override で検査を迂回させない。
                if(!string.Equals(mode,baked.RuntimeMode,StringComparison.Ordinal))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,"Protected Player runtime mode was overridden.",baked.BuildIdentity,ContentDirectoryTarget);
                var hasRoot=installedRoot.Length>0;
                var hasDigest=installedDigest.Length>0;
                if(hasRoot!=hasDigest)throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,"Installed revision path and manifest digest must be provided together.",baked.BuildIdentity,ContentDirectoryTarget);
            }
            if (string.Equals(mode, "addressables", StringComparison.Ordinal)) return null;
            if ((installedRoot.Length==0)!=(installedDigest.Length==0))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,"Installed revision path and manifest digest must be provided together.");
            if (!specified)
            {
                // 未指定は Delivery の verified pair があるときだけ directory。known-good は見ない。
                if (installedRoot.Length==0)
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,
                        "content:runtimeMode is required unless a verified installed revision pair is provided.");
                mode = "directory";
            }
            if (!string.Equals(mode, "directory", StringComparison.Ordinal))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration, "content:runtimeMode must be addressables or directory.");
            if (!Application.isEditor || !Application.isPlaying)
            {
                if (!UseRequiredPlayerFileConfiguration)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration, "Content Directory runtime mode is only available in Editor Play Mode.");
                var baked=_bakedPlayerContent ?? throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,"Baked Player content configuration is unavailable.");
                var player = PlayerContentConfiguration.Resolve(baked,config, Path.GetDirectoryName(Application.dataPath)!, ContentDirectoryTarget);
                if (!Directory.Exists(player.Path))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration, "Configured Player content directory does not exist.", player.Identity, ContentDirectoryTarget);
                return new ContentDirectoryConfiguration(player.Path, player.Identity, player.Representation, player.FirstScene,player.VerifiedRevision);
            }
            var path = config.GetString("content:directoryPath", string.Empty);
            var identity = config.GetString("content:buildIdentity", string.Empty);
            var representation = config.GetString("content:representation", string.Empty);
            // Editor directory Play の初回 Scene。Framework は名前を App の config から受けるだけで、目的や値の意味を知らない。
            var firstScene = ReadEditorFirstScene(config);
            if ((string.IsNullOrWhiteSpace(path)&&installedRoot.Length==0) || string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(representation))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration, "Directory mode requires absolute directoryPath, buildIdentity, and representation.");
            // 起動、登録、削除 gate が同じ canonical path を使う。文字列 alias が
            // 別 revision と見なされると、使用中 directory の削除許可が誤る。
            if(installedRoot.Length>0)
            {
                var set=OneStarMaker.Runtime.BuildContent.Distribution.ContentDeliveryFiles.ReadJson<OneStarMaker.Runtime.BuildContent.Distribution.ContentInstallReceipt>(Path.Combine(installedRoot,"receipt.json")).contentSet;
                var verified=OneStarMaker.Runtime.BuildContent.Distribution.InstalledRevisionVerifier.Verify(installedRoot,installedDigest,set,identity,ContentDirectoryTarget);
                return new ContentDirectoryConfiguration(verified.ContentPath,identity,representation,firstScene,verified,
                    overlay.Kind == ContentOverlayStartupKind.Overlay ? overlay : null);
            }
            if (!ContentRevisionGate.TryNormalize(identity, ContentDirectoryTarget, path, out var fullPath))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,
                    "Content directory path is invalid.", identity, ContentDirectoryTarget,
                    representation: representation);
            if (!Directory.Exists(fullPath))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration, "Configured content directory does not exist.", identity, ContentDirectoryTarget, representation: representation);
            return new ContentDirectoryConfiguration(fullPath, identity, representation, firstScene,null);
        }

        private sealed class ContentDirectoryConfiguration
        {
            internal ContentDirectoryConfiguration(string path, string buildIdentity, string representation, string firstScene,OneStarMaker.Runtime.BuildContent.Distribution.VerifiedInstalledRevision? verifiedRevision,
                ContentOverlayStartupDecision? overlay = null)
            { Path = path; BuildIdentity = buildIdentity; Representation = representation; FirstScene = firstScene;VerifiedRevision=verifiedRevision; Overlay = overlay; }
            internal string Path { get; }
            internal string BuildIdentity { get; }
            internal string Representation { get; }
            internal string FirstScene { get; }
            internal OneStarMaker.Runtime.BuildContent.Distribution.VerifiedInstalledRevision? VerifiedRevision { get; }
            /// <summary>重ねる上書き（<see cref="ContentOverlayStartupKind.Overlay"/> の決定）。無ければ null。検証済みの base の分岐だけが持つ。</summary>
            internal ContentOverlayStartupDecision? Overlay { get; }
        }

        /// <summary>
        /// 追加サービスの初期化。Phase 2 以降で HostedService 登録や DI コンテナ構築を行う。
        /// </summary>
        protected virtual UniTask OnServicesInitializing(CancellationToken ct)
            => UniTask.CompletedTask;

        /// <summary>
        /// AfterSceneLoad 初期化の途中失敗を派生クラスへ通知する。
        /// OnServicesInitializing で確保したアプリ固有の常駐リソースだけを、所有者がここで回収する。
        /// Framework 共通リソースの破棄や再試行方針はこのフックでは決めない。
        /// </summary>
        protected virtual void OnAfterSceneLoadInitializationFailed(string stage, Exception exception)
        {
        }

        protected enum PlayerContentStage
        {
            UiCommonReady,
            ContentDirectoryRegistered,
            FirstSceneStable,
            FixtureLoaded,
            FixtureInstantiated,
            FixtureBehaviorVerified,
            FixtureDestroyed,
            FixtureHandleReleased,
            FirstSceneUnloaded,
            ContentDirectoryClosed,
        }

        protected sealed class PlayerContentFixtureResult
        {
            public PlayerContentFixtureResult(IReadOnlyList<PlayerContentStage> completedStages, Exception? failure)
            {
                CompletedStages = completedStages?.ToArray() ?? throw new ArgumentNullException(nameof(completedStages));
                Failure = failure;
            }

            public IReadOnlyList<PlayerContentStage> CompletedStages { get; }
            public Exception? Failure { get; }
        }

        protected static void AppendFixtureStages(List<PlayerContentStage> destination, IReadOnlyList<PlayerContentStage> stages)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            var allowed = new[] { PlayerContentStage.FixtureLoaded, PlayerContentStage.FixtureInstantiated,
                PlayerContentStage.FixtureBehaviorVerified, PlayerContentStage.FixtureDestroyed,
                PlayerContentStage.FixtureHandleReleased };
            var previous = -1;
            foreach (var stage in stages)
            {
                var index = Array.IndexOf(allowed, stage);
                if (index <= previous)
                    throw new InvalidOperationException("Fixture completion stages must be a unique ordered subsequence.");
                destination.Add(stage);
                previous = index;
            }
        }

        internal static bool ShouldNotifyPlayerContentFailure(bool useRequiredPlayerFileConfiguration) =>
            useRequiredPlayerFileConfiguration;

        /// <summary>初回 Scene の後の行き先。Editor は load したまま Play を続け、Player は baked の種類で動作確認の尾か、load したまま起動を終えるかを選ぶ。</summary>
        internal enum PlayerFirstSceneRoute { Editor, Smoke, Playable }

        /// <summary>
        /// 初回 Scene の後の分岐。Player の構成でなければ種類を見ずに Editor。Player では Playable の時だけ遊べる経路で、他は動作確認の尾。
        /// 2 段の bool 判定にすると、Player の Playable が Editor の経路に落ちうるので 3 値で返す。
        /// </summary>
        internal static PlayerFirstSceneRoute RouteFirstScene(bool isPlayerConfiguration, PlayerContentKind kind) =>
            !isPlayerConfiguration ? PlayerFirstSceneRoute.Editor
            : kind == PlayerContentKind.Playable ? PlayerFirstSceneRoute.Playable
            : PlayerFirstSceneRoute.Smoke;

        /// <summary>失敗時の初回 Scene の unload は Player の構成だけ（種類によらない）。Editor は ReleaseAll と Play の終了に任せる。</summary>
        internal static bool UnloadsFirstSceneOnStartupFailure(bool isPlayerConfiguration) => isPlayerConfiguration;

        /// <summary>
        /// 派生に見せる Player の種類の latch。失敗の通知（ReleaseAll の後にも呼ばれる）で、遊べる Player は receipt を書かないために読む。
        /// baked が読めなかった・種類が欠け・不正な値の時は偽（動作確認の扱い）。
        /// </summary>
        protected bool IsPlayablePlayerKind => _latchedPlayerKind == PlayerContentKind.Playable;

        /// <summary>Editor directory Play の初回 Scene。config に無ければ空で、従来どおり初回 content Scene をロードしない。</summary>
        internal static string ReadEditorFirstScene(AppConfig config) =>
            config.GetString("content:firstScene", string.Empty).Trim();

        protected virtual UniTask<PlayerContentFixtureResult> OnPlayerContentReadyAsync(IAssetManagement assetManagement, CancellationToken ct)
            => UniTask.FromResult(new PlayerContentFixtureResult(Array.Empty<PlayerContentStage>(), null));

        protected virtual UniTask OnPlayerContentShutdownCompletedAsync(IReadOnlyList<PlayerContentStage> completedStages)
            => UniTask.CompletedTask;

        protected virtual UniTask OnPlayerContentStartupFailedAsync(
            string stage,
            Exception exception,
            IReadOnlyList<PlayerContentStage> completedStages)
            => UniTask.CompletedTask;

        /// <summary>
        /// Framework 標準の logger factory を作る。
        /// 派生クラスが rolling size や realtime format を変えたい場合はここを差し替える。
        /// </summary>
        protected virtual AppLoggerFactory CreateLoggerFactory(Stream? realtimeStream)
            => new(realtimeStream);

        /// <summary>
        /// DebugSocketService を構築する。
        /// enabled=false のときは null を返し、不要な service を作らない。
        /// </summary>
        protected virtual DebugSocketService? CreateDebugSocketService(AppConfig config)
        {
            var options = DebugSocketOptions.FromConfig(config);
            if (!options.Enabled)
            {
                return null;
            }

            return new DebugSocketService(options, CreateDebugCommandDispatcher());
        }

        /// <summary>
        /// DebugSocket で受けたコマンドをアプリ側へ渡す dispatcher。
        /// 実コマンドを使うアプリは override して allowlist と実処理を定義する。
        /// </summary>
        protected virtual IDebugCommandDispatcher CreateDebugCommandDispatcher()
            => NullDebugCommandDispatcher.Instance;

        /// <summary>
        /// Framework 標準の update system host を構築する。
        /// 派生クラスが custom driver や profiler 計測を差し込みたい場合はここを override する。
        /// </summary>
        protected virtual UpdateSystemHost CreateUpdateSystemHost()
            => new();

        /// <summary>
        /// デフォルトのテレメトリ Sink を登録する。
        /// 派生クラスでオーバーライドして Elastic Sink 等を追加できる。
        /// </summary>
        protected virtual void RegisterDefaultTelemetrySink()
        {
            var loggerFactory = _services?.LoggerFactory;
            if(loggerFactory == null)
            {
                Debug.LogWarning("[AppInit] LoggerFactory が未構築のため、デフォルトのテレメトリ Sink を登録できません。");
                return;
            }
            AppTelemetry.AddSink(new JsonFileTelemetrySink(loggerFactory));
        }

        private static void LogDebugSocketBootstrapStatus(AppConfig config)
        {
            var options = DebugSocketOptions.FromConfig(config);
            if (!options.Enabled)
            {
                Debug.LogWarning("[AppInit] DebugSocket disabled. DebugStudio から接続するには debugSocket:enabled=true を設定してください。");
                return;
            }

            Debug.Log(
                $"[AppInit] DebugSocket configured. mode={options.TransportMode}, endpoint={options.EndpointDisplayName}, autoStart={options.AutoStart}, sendLogs={options.SendLogs}, sendTelemetry={options.SendTelemetry}.");

            if (!options.AutoStart)
            {
                Debug.LogWarning("[AppInit] DebugSocket autoStart is false. Transport は起動時に自動開始しません。");
            }
        }
    }
}
