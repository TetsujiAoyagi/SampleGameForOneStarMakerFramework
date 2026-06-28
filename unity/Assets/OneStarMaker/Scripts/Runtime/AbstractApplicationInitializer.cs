#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.Config;
using OneStarMaker.Foundation.Core;
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
    ///     protected override string GetFirstSceneIdentify()     =&gt; "Title";
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
                AppTelemetry.FinishSpan(
                    span: span,
                    metadata: default,
                    isSuccess: success,
                    level: TelemetryLevel.Summary,
                    tags: null);
            }
        }

        // ─── Lifecycle: AfterSceneLoad ───

        private async UniTaskVoid InitializeAfterSceneLoad()
        {
            var startupStage = "load-ui-common";
            var span = AppTelemetry.StartSpan(Foundation.Core.TelemetryStartType.AppStartup, null);
            var success = false;
            CancellationToken ct = default;

            try
            {
                Debug.Log("[AppInit] AfterSceneLoad: loading UICommon.");
                var uiCommon = await LoadUICommonAsync();

                startupStage = "load-scene-resource-map";
                Debug.Log("[AppInit] AfterSceneLoad: loading SceneResourceMap.");
                _sceneResourceMap = await LoadSceneResourceMapAsync();

                startupStage = "create-scene-factory";
                var sceneFactory = CreateSceneFactory();

                if (_assetManagement == null || _cts == null)
                {
                    Debug.LogError("[AppInit] BeforeSceneLoad が未完了のため AfterSceneLoad をスキップします。");
                    return;
                }

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

                ct = _cts.Token;

                // Framework 標準の長寿命サービスを先に起動する。
                // 派生クラスが OnServicesInitializing を override しても、
                // base 呼び出し忘れで DebugSocket が起動しない事故を避けるため。
                startupStage = "start-framework-services";
                await StartFrameworkServicesAsync(ct);

                // Phase 2 拡張ポイント: HostedService 等の追加初期化
                startupStage = "initialize-app-services";
                await OnServicesInitializing(ct);

                // Editor のプレイモードで開いていたシーンを SceneDirector に登録する。
                // PerformUnitySceneLoad が SceneManager.GetSceneByName でロード済みシーンを検出し、
                // 再ロードせず RootGameObjects を返すため二重ロードは発生しない。
                startupStage = "register-loaded-scenes";
                await RegisterAlreadyLoadedScenes(ct);

                // 初回シーンのロード（RegisterAlreadyLoadedScenes で登録済みなら冪等にスキップ）
                var firstScene = GetFirstSceneIdentify();
                if (!string.IsNullOrEmpty(firstScene))
                {
                    startupStage = "load-first-scene";
                    await _sceneDirector.AddScene(firstScene, null, ct);
                }

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
            }
            finally
            {
                AppTelemetry.FinishSpan(
                    span: span,
                    metadata: default,
                    isSuccess: success,
                    level: TelemetryLevel.Summary,
                    tags: null);
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

        private UniTask<SceneResourceMap> LoadSceneResourceMapAsync()
        {
            var address = GetSceneResourceMapAddress();
            // ScriptableObject はアプリ生存期間中ずっと必要。ReleaseAppAll まで保持する
            var handle = _assetManagement!.LoadAppAssetSync<SceneResourceMap>(AssetKey.FromAddress(address));
            var map = handle.Value
                ?? throw new InvalidOperationException($"SceneResourceMap not found: {address}");
            return UniTask.FromResult(map);
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

            _sceneDirector?.Dispose();
            _sceneDirector = null;

            // UICommon シーン / SceneResourceMap / Config 等の App 常駐ハンドルを一括 Release
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

        /// <summary>初回ロードするシーンの識別子を返す。</summary>
        protected abstract string GetFirstSceneIdentify();

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

    /// <summary>
    /// Runtime 層で扱う telemetry 用の軽量メモリ snapshot。
    /// 文字列や参照型を持たず、呼び出し元の hot path に余計なアロケーションを入れない。
    /// </summary>
    public readonly struct RuntimeTelemetryMemorySnapshot
    {
        public readonly long ManagedMem;
        public readonly long NativeMem;

        public RuntimeTelemetryMemorySnapshot(long managedMem, long nativeMem)
        {
            ManagedMem = managedMem;
            NativeMem = nativeMem;
        }
    }

    /// <summary>
    /// Runtime / Debug で使う telemetry metadata と tag 判定の共通 helper。
    ///
    /// <para>
    /// 本来は `Runtime\Telemetry\` へ独立配置したい責務だが、
    /// 現在は Unity 生成 csproj の取り込みを乱さずに進めるため既存ファイル内へ置いている。
    /// 重要なのは「責務を Runtime 側へ寄せること」であり、
    /// hot path のゼロアロ特性を崩さないことを優先する。
    /// </para>
    /// </summary>
    public static class RuntimeTelemetryMetadataFactory
    {
        public static RuntimeTelemetryMemorySnapshot CaptureMemorySnapshot()
        {
            return new RuntimeTelemetryMemorySnapshot(
                managedMem: GC.GetTotalMemory(false),
                nativeMem: UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());
        }

        public static Metadata CreateMemoryMetadata(in RuntimeTelemetryMemorySnapshot snapshot)
        {
            return new Metadata(
                managedMem: snapshot.ManagedMem,
                nativeMem: snapshot.NativeMem);
        }

        public static Metadata CreateProfilerMetadata(float cpuTime, float gpuTime)
        {
            return new Metadata(
                cpuTime: cpuTime,
                gpuTime: gpuTime,
                managedMem: UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(),
                nativeMem: UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());
        }

        public static TelemetryTagType? ClassifyFrameRate(float fps)
        {
            if (fps > 0f && fps < 30f)
            {
                return TelemetryTagType.FrameRateDrop;
            }

            return null;
        }

        public static TelemetryTagType? ClassifyMemoryDelta(
            in RuntimeTelemetryMemorySnapshot before,
            in RuntimeTelemetryMemorySnapshot after,
            TelemetryThresholds? thresholds)
        {
            if (thresholds == null)
            {
                return null;
            }

            var managedDeltaMb = GetManagedDeltaMb(before, after);
            var nativeDeltaMb = GetNativeDeltaMb(before, after);
            TelemetryTagType? tags = null;

            if (managedDeltaMb > thresholds.MemoryDeltaMb)
            {
                tags = TelemetryTagType.ManagedMemoryOver;
            }

            if (nativeDeltaMb > thresholds.MemoryDeltaMb)
            {
                tags = tags.HasValue
                    ? tags | TelemetryTagType.NativeMemoryOver
                    : TelemetryTagType.NativeMemoryOver;
            }

            if (tags.HasValue)
            {
                tags |= TelemetryTagType.Bottleneck;
            }

            return tags;
        }

        public static double GetManagedDeltaMb(
            in RuntimeTelemetryMemorySnapshot before,
            in RuntimeTelemetryMemorySnapshot after)
        {
            return (after.ManagedMem - before.ManagedMem) / (1024.0 * 1024.0);
        }

        public static double GetNativeDeltaMb(
            in RuntimeTelemetryMemorySnapshot before,
            in RuntimeTelemetryMemorySnapshot after)
        {
            return (after.NativeMem - before.NativeMem) / (1024.0 * 1024.0);
        }
    }
}
