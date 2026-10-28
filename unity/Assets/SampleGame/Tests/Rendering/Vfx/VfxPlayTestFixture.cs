#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OneStarMaker.Foundation.UpdateSystem.Layers;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.UpdateSystem.Api;
using SampleGame.Common.Rendering.Vfx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace SampleGame.Tests.Rendering.Vfx
{
    /// <summary>
    /// 実 backend（VFX Graph）を Play 中に動かすテストの共通の起動と後始末。本物の AppInitializer が起動した状態で、
    /// テストが自分の backend と Framework の host（VfxSystem）を作って確かめる。配置済み VFX もテストの VfxSystem に
    /// 登録して有効にする（marker は自分では有効化しない）。
    /// <para>
    /// 時間: テストが変えた Gameplay Layer の pause・倍率と <c>Time.timeScale</c> は TearDown で元に戻す
    /// （アプリの Gameplay の要素も同じ Layer にいるため）。
    /// </para>
    /// <para>
    /// 起動の前提は既存の Play 中のテストと同じ: 現在の Scene 構成を保存して空の Scene を開き、content の runtime mode を
    /// addressables にしてから Play に入り、TearDown で戻す。起動の失敗は VFX の不具合と区別できる文言で失敗させる。
    /// graphics device が必要なケースは、実際の platform の事実が対応のときだけ実行する（-nographics では Inconclusive）。
    /// graphics の無い device で VisualEffect を有効化しないため。待ちは全てフレーム数の上限付きで、時間待ちは使わない。
    /// </para>
    /// </summary>
    public abstract class VfxPlayTestFixture
    {
        protected const string FixtureFolder = "Assets/SampleGame/Tests/Rendering/Vfx/Fixtures/";
        protected const string ShortLivedPath = FixtureFolder + "VfxShortLived.vfx";
        protected const string ImmortalPath = FixtureFolder + "VfxImmortal.vfx";
        protected const string PlacedPrefabPath = FixtureFolder + "PlacedVfx.prefab";
        private const string ContentModeKey = "SAMPLEGAME_CONTENT__RUNTIMEMODE";
        private const string DebugSocketEnabledKey = "SAMPLEGAME_DEBUGSOCKET__ENABLED";

        // 粒子の出現・消滅を待つフレーム数の上限。batch の Editor はフレームが短いので、寿命 1 秒前後の粒子
        // （fixture は VFX Graph の Simple Loop テンプレートの 0.8〜1.2 秒）が消え切るまでに数千フレームかかりうる。
        // 上限内に起きなければ不合格にする（固定フレーム後の状態だけは見ない）。
        protected const int ParticleFrameLimit = 6000;
        protected const int StartupFrameLimit = 3000;

        private readonly List<IDisposable> _disposables = new();
        private SceneSetup[]? _sceneSetup;
        private string? _previousContentMode;
        private bool _contentModeChanged;
        private string? _previousDebugSocketEnabled;
        private bool _debugSocketEnabledChanged;
        private bool _gameplayChanged;
        private bool _previousGameplayPaused;
        private float _previousGameplayScale = 1f;
        private float _previousTimeScale = 1f;

        /// <summary>TearDown で破棄する Object（GameObject・RenderTexture 等）。</summary>
        protected List<Object> Objects { get; } = new();

        /// <summary>0 以上なら TearDown でこの Quality に戻す。</summary>
        protected int PreviousQualityLevel { get; set; } = -1;

        protected VisualEffectAsset ShortLived { get; private set; } = null!;

        protected VisualEffectAsset Immortal { get; private set; } = null!;

        [UnitySetUp]
        public IEnumerator EnterPlayModeWithApp()
        {
            _sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _previousContentMode = Environment.GetEnvironmentVariable(ContentModeKey, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(ContentModeKey, "addressables", EnvironmentVariableTarget.Process);
            _contentModeChanged = true;

            // DebugSocket はこの Play では起動しない。app-config.json は connect・autoStart で始め、DebugStudio の
            // サーバーが無いと接続失敗の warning を Play の開始から数秒後に非同期で出し、以後も再接続のたびに出しうる。
            // その warning は起動の完了（下の Scene lifecycle への接続）とは無関係に届くので、本体の長いテストの
            // LogAssert.NoUnexpectedReceived に DebugStudio の有無とタイミング次第で混ざる。VFX のテストは DebugSocket を
            // 使わないので、assert を弱めず、ログの発生源を止める。無効のときの起動の warning は接続の前に同期で出る。
            _previousDebugSocketEnabled = Environment.GetEnvironmentVariable(DebugSocketEnabledKey, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(DebugSocketEnabledKey, "false", EnvironmentVariableTarget.Process);
            _debugSocketEnabledChanged = true;
            yield return new EnterPlayMode();

            // 起動の失敗を VFX の不具合と取り違えないよう、先に起動を確かめる。
            Assert.That(UpdateSystemRuntime.Coordinator, Is.Not.Null,
                "起動の失敗: BeforeSceneLoad が完了していない（UpdateSystem が install されていない）");
            var frameworkHost = AbstractApplicationInitializer.ActiveVfxRuntimeHost;
            Assert.That(frameworkHost, Is.Not.Null,
                "起動の失敗、または Framework が VFX の composition を作っていない");

            // AfterSceneLoad（非同期）の完了を待ってからテスト本体へ入る。本体の LogAssert.NoUnexpectedReceived は
            // 本体の間に届いた全てのログを数えるので、起動のログが本体に混ざると落ちる。
            // 完了の目印は Scene lifecycle への接続: DebugSocket の起動判断とそのログは接続より前で、接続の後の
            // ロード済み Scene の登録は、空の Scene と UICommon（どちらも SceneResourceMap に無い）を同期でスキップする
            // だけなので、接続が見えた時点で起動のログは出終わっている。
            for (var frame = 0; frame < StartupFrameLimit && !frameworkHost!.IsSceneLifecycleBound; frame++)
            {
                yield return null;
            }

            Assert.That(frameworkHost!.IsSceneLifecycleBound, Is.True,
                "起動の失敗（AfterSceneLoad が SceneDirector を作っていない）、または Scene lifecycle への接続の失敗");

            ShortLived = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(ShortLivedPath);
            Immortal = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(ImmortalPath);
            Assert.That(ShortLived != null && Immortal != null, Is.True, "fixture の .vfx が無い");
        }

        [UnityTearDown]
        public IEnumerator ExitPlayModeAndRestore()
        {
            try
            {
                RestoreTime();

                // 後に作ったものから戻す（host の Dispose が自分の backend も Dispose する）。
                for (var i = _disposables.Count - 1; i >= 0; i--)
                {
                    _disposables[i].Dispose();
                }

                _disposables.Clear();
                foreach (var obj in Objects)
                {
                    if (obj != null)
                    {
                        Object.Destroy(obj);
                    }
                }

                Objects.Clear();
                if (PreviousQualityLevel >= 0)
                {
                    QualitySettings.SetQualityLevel(PreviousQualityLevel, true);
                    PreviousQualityLevel = -1;
                }

                if (EditorApplication.isPlaying)
                {
                    yield return null;
                    yield return new ExitPlayMode();
                }

                if (_sceneSetup != null)
                {
                    if (_sceneSetup.Any(scene => scene.isLoaded && scene.isActive))
                        EditorSceneManager.RestoreSceneManagerSetup(_sceneSetup);
                    else
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    _sceneSetup = null;
                }
            }
            finally
            {
                if (_contentModeChanged)
                {
                    Environment.SetEnvironmentVariable(ContentModeKey, _previousContentMode, EnvironmentVariableTarget.Process);
                    _contentModeChanged = false;
                }

                if (_debugSocketEnabledChanged)
                {
                    Environment.SetEnvironmentVariable(DebugSocketEnabledKey, _previousDebugSocketEnabled, EnvironmentVariableTarget.Process);
                    _debugSocketEnabledChanged = false;
                }
            }
        }

        // ─── helpers ───

        protected static VfxPlatformFacts RealFacts() => new(
            SystemInfo.supportsComputeShaders,
            SystemInfo.maxComputeBufferInputsVertex,
            QualitySettings.activeColorSpace,
            SystemInfo.graphicsDeviceType);

        protected static VfxPlatformFacts UnsupportedFacts() => new(true, 8, ColorSpace.Linear, UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3);

        protected static void AssumeGraphicsSupported()
        {
            var supported = VfxGraphCapabilityPolicy.Evaluate(RealFacts(), out var reason);
            Assume.That(supported, Is.True, "この環境では VFX Graph を有効化しない: " + reason);
        }

        protected static int CountVisualEffects()
            => Object.FindObjectsByType<VisualEffect>(FindObjectsInactive.Include).Length;

        protected T Track<T>(T disposable) where T : IDisposable
        {
            _disposables.Add(disposable);
            return disposable;
        }

        protected GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            Objects.Add(go);
            return go;
        }

        /// <param name="backend">null なら未構成（Framework の UnconfiguredVfxBackend）。</param>
        private protected VfxRuntimeHost CreateHost(VfxGraphBackend? backend, float timeoutSeconds = 10f)
        {
            var coordinator = UpdateSystemRuntime.Coordinator
                              ?? throw new InvalidOperationException("UpdateSystem の coordinator が無い");
            return Track(VfxRuntimeHost.Create(backend, NullLoggerFactory.Instance, coordinator, timeoutSeconds));
        }

        /// <summary>指定の platform の事実で判定する backend と、それを使う host（テストの VfxSystem）を作る。</summary>
        private protected VfxRuntimeHost CreateHost(Func<VfxPlatformFacts> facts)
            => CreateHost(new VfxGraphBackend(facts));

        private protected static VfxInstanceId IdOf(IVfxHandle handle) => ((VfxHandle)handle).Id;

        /// <summary>
        /// Scene の root から marker を集め、テストの VfxSystem に登録する（本番では Scene class が行う）。
        /// 所有者は App にして、テストが handle で回収する。
        /// </summary>
        private protected static List<IVfxHandle> AdoptMarkers(IVfxSystem vfx, IEnumerable<GameObject> roots)
        {
            var handles = new List<IVfxHandle>();
            foreach (var root in roots)
            {
                foreach (var marker in root.GetComponentsInChildren<PlacedVfxActivator>(includeInactive: true))
                {
                    handles.Add(vfx.Adopt(marker.Effect!, AssetOwner.App, marker.UsesUnscaledTime ? null : UpdateLayerIds.Gameplay));
                }
            }

            return handles;
        }

        // ─── 時間（TearDown で戻す）───

        /// <summary>アプリの coordinator の Gameplay Layer（Framework の host が作っている）。</summary>
        protected UpdateLayer GameplayLayer
        {
            get
            {
                var coordinator = UpdateSystemRuntime.Coordinator
                                  ?? throw new InvalidOperationException("UpdateSystem の coordinator が無い");
                Assert.That(coordinator.TryGetLayer(UpdateLayerIds.Gameplay, out var layer), Is.True, "前提: Gameplay Layer がある");
                RememberTime();
                return layer!;
            }
        }

        protected void SetTimeScale(float timeScale)
        {
            RememberTime();
            Time.timeScale = timeScale;
        }

        private void RememberTime()
        {
            if (_gameplayChanged)
            {
                return;
            }

            var coordinator = UpdateSystemRuntime.Coordinator;
            if (coordinator != null && coordinator.TryGetLayer(UpdateLayerIds.Gameplay, out var layer))
            {
                _previousGameplayPaused = layer.IsPaused;
                _previousGameplayScale = layer.TimeScale;
            }

            _previousTimeScale = Time.timeScale;
            _gameplayChanged = true;
        }

        private void RestoreTime()
        {
            if (!_gameplayChanged)
            {
                return;
            }

            _gameplayChanged = false;
            Time.timeScale = _previousTimeScale;
            var coordinator = UpdateSystemRuntime.Coordinator;
            if (coordinator != null && coordinator.TryGetLayer(UpdateLayerIds.Gameplay, out var layer))
            {
                layer.IsPaused = _previousGameplayPaused;
                layer.SetTimeScale(_previousGameplayScale);
            }
        }

        private static readonly Stopwatch SpinClock = Stopwatch.StartNew();
        // ループの結果の置き場（読まれないが、捨てるとループごと最適化で消されうる）。
        private static double SpinSink { get; set; }

        /// <summary>
        /// 数値計算のループで main thread を指定秒だけ占有し、そのフレームを引き延ばす（負荷スパイクの再現）。
        /// <c>Thread.Sleep</c> は使わない（テストの待ちの約束）。
        /// </summary>
        protected static void SpinMainThread(double seconds)
        {
            var end = SpinClock.Elapsed.TotalSeconds + seconds;
            var sink = 0.0;
            while (SpinClock.Elapsed.TotalSeconds < end)
            {
                for (var i = 0; i < 256; i++)
                {
                    sink += Math.Sqrt(i + sink);
                }
            }

            SpinSink = sink;
        }

        // ─── 起動の条件（d3 の VFX Scene のテストが共有）───

        protected void UsePcQuality()
        {
            var pc = Array.IndexOf(QualitySettings.names, "PC");
            Assert.That(pc, Is.GreaterThanOrEqualTo(0), "Quality に PC が無い");
            PreviousQualityLevel = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(pc, true);
        }

        protected static void WriteGraphicsFacts()
            => TestContext.Out.WriteLine(
                $"graphicsDeviceType={SystemInfo.graphicsDeviceType} supportsComputeShaders={SystemInfo.supportsComputeShaders} " +
                $"maxComputeBufferInputsVertex={SystemInfo.maxComputeBufferInputsVertex} colorSpace={QualitySettings.activeColorSpace}");
    }
}
