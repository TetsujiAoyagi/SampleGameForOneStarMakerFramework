#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OneStarMaker.Runtime;
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
    /// テストが自分の backend を作って確かめる（後から能力判定を書いた backend が <see cref="VfxCapabilityState"/> の writer になる）。
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
                // 後に作ったものから戻す（host → backend の順。writer の backend の Dispose で状態が未判定に戻る）。
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

        private protected VfxRuntimeHost CreateHost(VfxGraphBackend backend, float timeoutSeconds = 10f)
        {
            var coordinator = UpdateSystemRuntime.Coordinator
                              ?? throw new InvalidOperationException("UpdateSystem の coordinator が無い");
            return Track(VfxRuntimeHost.Create(backend, NullLoggerFactory.Instance, coordinator, timeoutSeconds));
        }

        private protected static VfxInstanceId IdOf(IVfxHandle handle) => ((VfxHandle)handle).Id;

        // ─── 起動の条件（d3 の VFX Scene のテストが共有）───

        protected void PublishCapability(Func<VfxPlatformFacts> facts, VfxCapability expected)
        {
            // test の backend を writer にする。TearDown の Dispose で未判定に戻る。
            var backend = Track(new VfxGraphBackend(facts));
            backend.QueryCapability(out _);
            Assert.That(VfxCapabilityState.Current, Is.EqualTo(expected), "前提: 能力判定の状態");
        }

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
