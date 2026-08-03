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
using OneStarMaker.Runtime.Config;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UpdateSystem;
using OneStarMaker.Runtime.UpdateSystem.Hosting;
using OneStarMaker.Runtime.UISystem;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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
        // ─── Fields ───

        private AppConfig? _config;
        private SceneDirector? _sceneDirector;
        private SceneResourceMap? _sceneResourceMap;
        private GameObject? _uiCommonObject;
        private GameObject? _eventSystemObject;
        private CancellationTokenSource? _cts;
        private AppLoggerFactory? _loggerFactory;
        private DebugSocketService? _debugSocketService;
        private UpdateSystemHost? _updateSystemHost;
        private bool _debugSocketTelemetrySinkRegistered;

        /// <summary>
        /// Addressables Load / Release の一元管理。
        /// BeforeSceneLoad で生成し、AfterSceneLoad 以降の全 Addressables 操作をここ経由にする。
        /// Application.quitting 時に ReleaseAppAll で App 常駐分を解放する。
        /// </summary>
        private IAssetManagement? _assetManagement;

        /// <summary>LoadUICommonAsync でロードした UICommon シーンのハンドル。</summary>
        private ISceneHandle? _uiSceneHandle;

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
        /// Framework 標準のロガーファクトリ。
        /// DebugSocketService が有効な場合は、その realtime stream もここへ配線される。
        /// </summary>
        protected AppLoggerFactory? LoggerFactory => _loggerFactory;

        /// <summary>Framework 標準の更新 coordinator。BeforeSceneLoad 完了後に有効。</summary>
        protected UpdateCoordinator? UpdateCoordinator => _updateSystemHost?.Coordinator;

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
                instance.ReleaseAll();
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
            try
            {
                instance.InitializeBeforeSceneLoad();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// AfterSceneLoad フェーズ。ロード済みシーンの登録と初回シーンのロードを行う。
        /// </summary>
        protected static void BootstrapAfterSceneLoad(AbstractApplicationInitializer instance)
        {
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

                // Updater は scene object の Awake register を受ける必要があるため、
                // BeforeSceneLoad の時点で install しておく。
                _updateSystemHost = CreateUpdateSystemHost();

                // テレメトリ閾値を Config から読み込み
                AppTelemetry.Thresholds = new TelemetryThresholds(_config);

                // DebugSocket は Config 解決後でないと起動条件を決められない。
                // ここでは service と logger factory を組み立てるだけに留め、
                // 実際の socket 待受開始は AfterSceneLoad 側へ寄せる。
                _debugSocketService = CreateDebugSocketService(_config);
                LogDebugSocketBootstrapStatus(_config);
                _loggerFactory = CreateLoggerFactory(_debugSocketService?.RealtimeStream);
                // ── テレメトリ Sink 登録 ──
                RegisterDefaultTelemetrySink();

                success = true;
            }
            finally
            {
                var memAfter = RuntimeTelemetryMetadataFactory.CaptureMemorySnapshot();
                var (metadata, payload) = RuntimeTelemetryMetadataFactory.CreateTimingMemoryTelemetry(
                    memBefore,
                    memAfter,
                    stage: "BeforeSceneLoad");

                AppTelemetry.FinishSpan(
                    span: span,
                    metadata: metadata,
                    isSuccess: success,
                    level: TelemetryLevel.Summary,
                    tags: null,
                    payload: payload);
            }
        }

        // ─── Lifecycle: AfterSceneLoad ───

        private async UniTaskVoid InitializeAfterSceneLoad()
        {
            var startupStage = "load-ui-common";
            var span = AppTelemetry.StartSpan(Foundation.Core.TelemetryStartType.AppStartup, null);
            var success = false;
            CancellationToken ct = default;
            // Contract v3: AfterSceneLoad 区間の memory delta + 最終 stage 名を payload に載せる。
            var memBefore = RuntimeTelemetryMetadataFactory.CaptureMemorySnapshot();

            try
            {
                startupStage = "load-remote-catalog";
                await TryLoadRemoteCatalogAsync();

                startupStage = "load-ui-common";
                Debug.Log("[AppInit] AfterSceneLoad: loading UICommon.");
                var uiCommon = await LoadUICommonAsync();

                startupStage = "load-scene-resource-map";
                Debug.Log("[AppInit] AfterSceneLoad: loading SceneResourceMap.");
                _sceneResourceMap = await LoadSceneResourceMapAsync();

                if (_assetManagement == null || _cts == null)
                {
                    Debug.LogError("[AppInit] BeforeSceneLoad が未完了のため AfterSceneLoad をスキップします。");
                    return;
                }

                ct = _cts.Token;

                // Framework 標準の長寿命サービスを先に起動する。
                // 派生クラスが OnServicesInitializing を override しても、
                // base 呼び出し忘れで DebugSocket が起動しない事故を避けるため。
                startupStage = "start-framework-services";
                await StartFrameworkServicesAsync(ct);

                // Phase 2 拡張ポイント: HostedService 等の追加初期化
                startupStage = "initialize-app-services";
                await OnServicesInitializing(ct);

                startupStage = "create-scene-factory";
                var sceneFactory = CreateSceneFactory();

                startupStage = "create-scene-director";
                _sceneDirector = new SceneDirector(
                    sceneFactory,
                    uiCommon,
                    _sceneResourceMap,
                    CreateLoadingDisplay(),
                    _assetManagement);
                _updateSystemHost?.BindSceneDirector(_sceneDirector);

                if (_sceneDirector == null)
                {
                    Debug.LogError("[AppInit] BeforeSceneLoad が未完了のため AfterSceneLoad をスキップします。");
                    return;
                }

                // Editor のプレイモードで開いていたシーンを SceneDirector に登録する。
                // PerformUnitySceneLoad が SceneManager.GetSceneByName でロード済みシーンを検出し、
                // 再ロードせず RootGameObjects を返すため二重ロードは発生しない。
                startupStage = "register-loaded-scenes";
                await RegisterAlreadyLoadedScenes(ct);

                success = true;
            }
            catch (OperationCanceledException)
            {
                // アプリ終了によるキャンセル — 正常
                success = true; // キャンセルは失敗ではない
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AppInit] AfterSceneLoad failed at stage '{startupStage}': {ex}");
                try
                {
                    // 派生側が OnServicesInitializing で常駐リソースを確保している場合、
                    // その後段（SceneDirector 構築・初回シーン追加）の失敗では通常の終了処理まで残り続ける。
                    // Framework 自身の ReleaseAll をここで呼ぶと診断用状態まで一律に失うため、
                    // 所有者である派生クラスへ限定的な回収機会を渡す。
                    OnAfterSceneLoadInitializationFailed(startupStage, ex);
                }
                catch (Exception cleanupException)
                {
                    // 元の起動失敗を隠さず、後始末の失敗は追加情報としてだけ記録する。
                    Debug.LogError(
                        $"[AppInit] Cleanup after failed AfterSceneLoad initialization also failed: {cleanupException}");
                }

                // SceneDirector や SceneFactory は、途中まで初期化したアプリ固有サービスを参照している可能性がある。
                // 失敗した bootstrap を継続して stale な依存を使わせないため、派生側の回収後に
                // Framework の Director / Updater / AssetManagement も同じ失敗境界で破棄する。
                ReleaseAll();
            }
            finally
            {
                var memAfter = RuntimeTelemetryMetadataFactory.CaptureMemorySnapshot();
                // 成功時は最終段階名ではなく区間名を載せる（失敗時だけ到達 stage で切り分ける）。
                var stageForPayload = success ? "AfterSceneLoad" : startupStage;
                var (metadata, payload) = RuntimeTelemetryMetadataFactory.CreateTimingMemoryTelemetry(
                    memBefore,
                    memAfter,
                    stage: stageForPayload);

                AppTelemetry.FinishSpan(
                    span: span,
                    metadata: metadata,
                    isSuccess: success,
                    level: TelemetryLevel.Summary,
                    tags: null,
                    payload: payload);
            }
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("[EventSystem]");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            UnityEngine.Object.DontDestroyOnLoad(go);
            _eventSystemObject = go;
        }

        private async UniTask<UICommon> LoadUICommonAsync()
        {
            UICommon? uiCommon = null;

            var activeScene = SceneManager.GetActiveScene();
            if(activeScene.name == "UICommon")
            {
                // 既に UICommon シーンがロードされている場合は、そこから UICommon を探す
                var tempGo = activeScene.GetRootGameObjects().FirstOrDefault(g => g.name == "UICommon");
                if (tempGo != null)
                {
                    uiCommon = tempGo.GetComponent<UICommon>();
                    if (uiCommon != null)
                    {
                        return uiCommon;
                    }
                }
            }

            var address = GetUICommonPrefabAddress();
            // UICommon は SceneDirector 管理外の App 常駐シーン。ReleaseAppAll で解放される
            // bool / int / SceneReleaseMode の並びを位置引数で固定しないとオーバーロードが曖昧になる
            var desc = new SceneAssetDescription();
            desc.AddPayload(string.Empty, new AssetReference(address));
            _uiSceneHandle = await _assetManagement!.LoadSceneAsync(
                "UICommon",
                desc,
                string.Empty,
                new SceneLoadOptions(LoadSceneMode.Additive, activateOnLoad: true, priority: 100));
            var go = _uiSceneHandle.GetRootGameObjects().FirstOrDefault();
            if (go == null)
            {
                throw new InvalidOperationException($"UICommon scene has no root object: {address}");
            }
            go.name = "[UICommon]";
            _uiCommonObject = go;

            uiCommon = go.GetComponent<UICommon>();
            if (uiCommon == null)
            {
                throw new InvalidOperationException(
                    $"UICommon component not found on prefab: {address}");
            }

            EnsureEventSystem();

            return uiCommon;
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
        /// リモート Addressables カタログを追加ロードする(開発ワークフロー専用のフォールバック)。
        /// URL の解決順序:
        ///   1. AppConfig のキー "assetCheckout:remoteCatalogUrl"(開発ビルド・実機で使用)
        ///   2. Editor のみ: RemoteCatalogRuntimeBridge 経由で現在選択中プロファイルの URL
        /// URL が空ならロードをスキップする(ローカル完結の開発者に一切影響を与えない)。
        /// ロード失敗(サーバ不達等)は警告ログのみで起動を継続する。
        /// サーバ不達で起動が長時間ブロックされるのを避けるためタイムアウトを設ける。
        /// </summary>
        private async UniTask TryLoadRemoteCatalogAsync()
        {
            var url = ResolveRemoteCatalogUrl();
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            try
            {
                Debug.Log($"[AppInit] Loading remote content catalog: {url}");
                // サーバ不達時に無限待機しないよう短いタイムアウトを設ける。
                var handle = UnityEngine.AddressableAssets.Addressables.LoadContentCatalogAsync(url);
                await handle.ToUniTask().Timeout(System.TimeSpan.FromSeconds(10));
                Debug.Log("[AppInit] Remote content catalog loaded. Missing local assets will resolve from remote bundles.");
                await WarnOnRevisionMismatchAsync(url);
            }
            catch (System.Exception ex)
            {
                // フォールバック不能でも起動は続行する。ローカルに揃っている開発者には無害。
                Debug.LogWarning($"[AppInit] Failed to load remote content catalog ('{url}'). Continuing with local assets only. Reason: {ex.Message}");
            }
        }

        /// <summary>
        /// リモートカタログ URL を解決する。Config 優先、次に Editor 注入。
        /// </summary>
        private string ResolveRemoteCatalogUrl()
        {
            var fromConfig = _config?.GetString("assetCheckout:remoteCatalogUrl", string.Empty) ?? string.Empty;
            if (!string.IsNullOrEmpty(fromConfig))
            {
                return fromConfig;
            }

#if UNITY_EDITOR
            var resolver = OneStarMaker.Runtime.AssetManagement.RemoteCatalogRuntimeBridge.EditorRemoteCatalogUrlResolver;
            if (resolver != null)
            {
                var url = resolver();
                if (!string.IsNullOrEmpty(url))
                {
                    return url!;
                }
            }
#endif

            return string.Empty;
        }

        /// <summary>
        /// リモートのビルド元リビジョンとローカル作業コピーのリビジョンを比較し、乖離を警告する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// catalog と同じディレクトリにある build-info.json を取得し、
        /// <c>revision</c> フィールドをローカルリビジョンと比較する。
        /// </para>
        /// <para>
        /// ベストエフォート。取得/解析失敗やローカルリビジョン不明時は静かにスキップし、
        /// 起動をブロックしない。
        /// </para>
        /// </remarks>
        /// <param name="catalogUrl">ロード済みリモートカタログの URL。</param>
        private async UniTask WarnOnRevisionMismatchAsync(string catalogUrl)
        {
            try
            {
                var buildInfoUrl = DeriveBuildInfoUrl(catalogUrl);
                if (string.IsNullOrEmpty(buildInfoUrl)) return;

                string json;
                using (var request = UnityWebRequest.Get(buildInfoUrl))
                {
                    await request.SendWebRequest().ToUniTask();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        // build-info.json が無い/取得失敗。比較はスキップ(警告のみ debug)。
                        Debug.Log($"[AppInit] build-info.json を取得できませんでした（比較スキップ）: {buildInfoUrl}");
                        return;
                    }
                    json = request.downloadHandler.text;
                }

                var info = JsonUtility.FromJson<RemoteBuildInfo>(json);
                var remoteRevision = info?.revision ?? string.Empty;
                var localRevision = ResolveLocalRevision();

                if (string.IsNullOrEmpty(remoteRevision) || string.IsNullOrEmpty(localRevision))
                {
                    // どちらか不明なら比較不能。
                    return;
                }

                if (!string.Equals(remoteRevision, localRevision, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning(
                        $"[AppInit] リビジョン乖離を検出しました。リモートのビルド済みアセットは古い/新しい可能性があります。\n" +
                        $"  local  = {localRevision}\n" +
                        $"  remote = {remoteRevision} (builtAtUtc={info?.builtAtUtc})\n" +
                        "  混在ロードで不具合が出る場合はリモートを再ビルドするか、ローカルをリモートのリビジョンへ合わせてください。");
                }
                else
                {
                    Debug.Log($"[AppInit] リビジョン一致: {localRevision}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AppInit] リビジョン比較に失敗しました（無視して続行）: {ex.Message}");
            }
        }

        /// <summary>catalog URL と同じディレクトリの build-info.json の URL を導出する。</summary>
        /// <param name="catalogUrl">リモートカタログの URL。</param>
        /// <returns>build-info.json の URL。導出不能時は空文字。</returns>
        private static string DeriveBuildInfoUrl(string catalogUrl)
        {
            if (string.IsNullOrEmpty(catalogUrl)) return string.Empty;
            var lastSlash = catalogUrl.LastIndexOf('/');
            if (lastSlash < 0) return string.Empty;
            return catalogUrl.Substring(0, lastSlash + 1) + "build-info.json";
        }

        /// <summary>
        /// ローカル作業コピーのリビジョンを解決する。
        /// </summary>
        /// <remarks>
        /// 解決順序:
        /// <list type="number">
        /// <item><description>AppConfig の <c>assetCheckout:localRevision</c>(ビルド時に焼き込み)</description></item>
        /// <item><description>Editor 注入 (<see cref="RemoteCatalogRuntimeBridge.EditorLocalRevisionResolver"/>)</description></item>
        /// </list>
        /// どちらも無ければ空文字を返す。
        /// </remarks>
        /// <returns>ローカル Git リビジョン。不明時は空文字。</returns>
        private string ResolveLocalRevision()
        {
            var fromConfig = _config?.GetString("assetCheckout:localRevision", string.Empty) ?? string.Empty;
            if (!string.IsNullOrEmpty(fromConfig)) return fromConfig;
#if UNITY_EDITOR
            var resolver = OneStarMaker.Runtime.AssetManagement.RemoteCatalogRuntimeBridge.EditorLocalRevisionResolver;
            if (resolver != null)
            {
                var rev = resolver();
                if (!string.IsNullOrEmpty(rev)) return rev!;
            }
#endif
            return string.Empty;
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

            var configPath = GetConfigFilePath();
            if (!string.IsNullOrEmpty(configPath))
            {
                providers.Add(new JsonFileConfigProvider(configPath, _assetManagement!));
            }

            var envPrefix = GetEnvironmentVariablePrefix();
            providers.Add(new EnvironmentVariableConfigProvider(envPrefix));

            providers.Add(new CommandLineConfigProvider());

            return new AppConfig(providers);
        }

        /// <summary>
        /// Framework が標準で持つサービス群を起動する。
        /// 今回は DebugSocketService だけだが、将来の長寿命サービスもここへ集約できる。
        /// </summary>
        private async UniTask StartFrameworkServicesAsync(CancellationToken ct)
        {
            if (_debugSocketService == null)
            {
                Debug.LogWarning("[AppInit] DebugSocket は無効です。DebugStudio 接続を受けるには debugSocket:enabled=true が必要です。");
                return;
            }

            Debug.Log(
                $"[AppInit] Starting DebugSocket transport. mode={_debugSocketService.Options.TransportMode}, endpoint={_debugSocketService.Options.EndpointDisplayName}, autoStart={_debugSocketService.Options.AutoStart}.");
            await _debugSocketService.StartAsync(ct);
            Debug.Log(
                $"[AppInit] DebugSocket transport start sequence finished. mode={_debugSocketService.Options.TransportMode}, isRunning={_debugSocketService.IsRunning}.");

            // Telemetry は AppTelemetry から流れてくるため、
            // socket service へ橋渡しする sink を一度だけ登録する。
            if (_debugSocketService.Options.SendTelemetry && !_debugSocketTelemetrySinkRegistered)
            {
                AppTelemetry.AddSink(new DebugSocketTelemetrySink(_debugSocketService));
                _debugSocketTelemetrySinkRegistered = true;
            }
        }

        private void OnApplicationQuitting()
        {
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
            Application.quitting -= OnApplicationQuitting;

            // まず framework service 側へ停止を通知する。
            _cts?.Cancel();

            _debugSocketService?.Dispose();
            _debugSocketService = null;
            _debugSocketTelemetrySinkRegistered = false;

            _updateSystemHost?.Dispose();
            _updateSystemHost = null;

            _loggerFactory?.Dispose();
            _loggerFactory = null;

            _cts?.Dispose();
            _cts = null;

            _config = null;

            // 論理台帳のみ。AssetManagement の Scene Unload は誘発しない。
            _sceneDirector?.Dispose();
            _sceneDirector = null;

            // Shutdown: Scene backend Unload なし・同期で全アセット解放
            // （UICommon / SceneResourceMap / Config / 各 Scene 所有分を含む）
            _assetManagement?.ReleaseAll();
            _assetManagement = null;

            _sceneResourceMap = null;

            if (_uiCommonObject != null)
            {
                UnityEngine.Object.Destroy(_uiCommonObject);
                _uiCommonObject = null;
            }

            _uiSceneHandle = null;

            if (_eventSystemObject != null)
            {
                UnityEngine.Object.Destroy(_eventSystemObject);
                _eventSystemObject = null;
            }

            // テレメトリをシャットダウン（Sink の Flush + Dispose）
            AppTelemetry.Shutdown();
        }

        // ─── Template methods ───

        /// <summary>SceneBase のファクトリを生成する。</summary>
        protected abstract ISceneFactory CreateSceneFactory();

        /// <summary>UICommon Prefab の Addressable アドレスを返す。</summary>
        protected abstract string GetUICommonPrefabAddress();

        /// <summary>SceneResourceMap の Addressable アドレスを返す。</summary>
        protected abstract string GetSceneResourceMapAddress();

        /// <summary>ローディング表示の実装を返す。</summary>
        protected abstract ILoadingDisplay CreateLoadingDisplay();

        /// <summary>
        /// 設定ファイルのパスを返す。空文字を返すとファイル読み込みをスキップする。
        /// デフォルトは StreamingAssets/app-config.json。
        /// </summary>
        protected virtual string GetConfigFilePath()
            => string.Empty;

        /// <summary>
        /// 環境変数のプレフィックスを返す（例: "ONESM_"）。
        /// プレフィックスに一致する環境変数のみが読み込まれる。
        /// 空文字の場合は全環境変数を対象とする。
        /// </summary>
        protected virtual string GetEnvironmentVariablePrefix() => "";

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
            if(_loggerFactory == null)
            {
                Debug.LogWarning("[AppInit] LoggerFactory が未構築のため、デフォルトのテレメトリ Sink を登録できません。");
                return;
            }
            AppTelemetry.AddSink(new JsonFileTelemetrySink(_loggerFactory));
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
