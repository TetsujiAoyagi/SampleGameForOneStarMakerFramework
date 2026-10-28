#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.Rendering.Environments;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.CameraSystem;
using OneStarMaker.Tests.SceneSystem.Helpers;
using SampleGame.Common.Rendering.Vfx;
using SampleGame.DependOnAll;
using SampleGame.InGame.Streaming;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.VFX;
using Object = UnityEngine.Object;
using RuntimeCameraSystem = OneStarMaker.Runtime.CameraSystem.Core.CameraSystem;

namespace SampleGame.Tests.Rendering.Vfx
{
    /// <summary>
    /// Season の Atmosphere VFX（Spring_AtmosphereVFX）と Cell VFX（Spring_VFX_4_2）を実 Scene として Play 中に載せ、
    /// 対応時の登録・有効化と粒子、止まった時間の領域でロードしたときに Resume まで始まらないこと、
    /// 非対応・未構成時に何も有効化されないこと、親の unload での回収を見る。
    /// 描画の陽性・陰性は <see cref="VfxSceneRenderPlayTests"/> が専用 Scene で測る。
    /// <para>
    /// Scene は test が持つ SceneDirector で載せる。アプリの SceneDirector で Season を載せると祖先の InGameSession の
    /// 距離ストリーミングが動き、読み込み半径の外の Cell (4,2) を次の Tick で外すので、Cell VFX の観測が競合する。
    /// test の director はアプリと同じ UICommon・AssetManagement・variant を使い、実 SceneResource の複製から
    /// Season_Spring を根にした Map を作る（同じ .unity を同じ経路で載せる。Lighting は RenderEnvironment の lease を
    /// 取るので入れない）。Scene class は実 GameSceneFactory が分類し、配置済み VFX をテストの VfxSystem に登録する
    /// （テストの VfxSystem はテストの director の Scene lifecycle に接続する）。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class VfxScenePlayTests : VfxPlayTestFixture
    {
        private const string SeasonsRoot = "Assets/SampleGame/InGame/InGameSession/Seasons/Spring";
        private const string SeasonResourcePath = SeasonsRoot + "/Season_Spring.asset";
        private const string AtmosphereResourcePath = SeasonsRoot + "/Spring_AtmosphereVFX/Spring_AtmosphereVFX.asset";
        private const string AtmosphereVfxPath = SeasonsRoot + "/Spring_AtmosphereVFX/Spring_AtmosphereVFX_Petals.vfx";
        private const string CellResourcePath = SeasonsRoot + "/Cells/Spring_Cell_4_2/Spring_Cell_4_2.asset";
        private const string CellVfxResourcePath = SeasonsRoot + "/Cells/Spring_Cell_4_2/Spring_VFX_4_2/Spring_VFX_4_2.asset";
        private const string CellVfxAssetPath = SeasonsRoot + "/Cells/Spring_Cell_4_2/Spring_VFX_4_2/Spring_VFX_4_2_GroundFog.vfx";

        private const string Season = "Season_Spring";
        private const string Atmosphere = "Spring_AtmosphereVFX";
        private const string Cell = "Spring_Cell_4_2";
        private const string CellVfx = "Spring_VFX_4_2";

        private const int SceneFrameLimit = 3000;

        // AC8 で止めておくフレーム数（AC6 と同じ）。
        private const int PausedFrames = 30;

        // ═══ AC4 (a)(b): 対応時に実 Scene の VisualEffect が全て有効になり、粒子が出る ═══

        [UnityTest]
        public IEnumerator Supported_SeasonAndCellVfxScenes_EnableAndEmit()
            => UniTask.ToCoroutine(async () =>
            {
                WriteGraphicsFacts();
                AssumeGraphicsSupported();
                UsePcQuality();
                var host = CreateHost(RealFacts);
                await WithTestScenesAsync(host, async scenes =>
                {
                    await scenes.Director.AddScene(Season, null, CancellationToken.None);
                    Assert.That(scenes.Director.IsSceneStable(Season), Is.True);
                    Assert.That(scenes.Director.IsSceneStable(Atmosphere), Is.True,
                        "Season の Stable までに NecessaryAlways の Atmosphere が Stable");
                    await AssertEnabledAndEmittingAsync(scenes.ScenePathOf(Atmosphere), AtmosphereVfxPath);

                    await scenes.Director.AddScene(Cell, null, CancellationToken.None);
                    Assert.That(scenes.Director.IsSceneStable(Cell), Is.True);
                    await scenes.Director.AddScene(CellVfx, null, CancellationToken.None);
                    Assert.That(scenes.Director.IsSceneStable(CellVfx), Is.True);
                    var cellEffects = await AssertEnabledAndEmittingAsync(scenes.ScenePathOf(CellVfx), CellVfxAssetPath);
                    await AssertRenderBoundsInsideCellAsync(cellEffects, scenes.VolumeOf(Cell));
                });
            });

        // ═══ S-4d4a AC8: 止まった時間の領域でロードした配置済み VFX は、Resume まで始まらない ═══

        [UnityTest]
        public IEnumerator Paused_PlacedVfxScenes_DoNotStartUntilResume()
            => UniTask.ToCoroutine(async () =>
            {
                WriteGraphicsFacts();
                AssumeGraphicsSupported();
                UsePcQuality();
                var host = CreateHost(RealFacts);
                var layer = GameplayLayer;
                layer.IsPaused = true;
                await WithTestScenesAsync(host, async scenes =>
                {
                    await scenes.Director.AddScene(Season, null, CancellationToken.None);
                    await scenes.Director.AddScene(Cell, null, CancellationToken.None);
                    await scenes.Director.AddScene(CellVfx, null, CancellationToken.None);
                    var effects = EffectsIn(scenes.ScenePathOf(Atmosphere)).Concat(EffectsIn(scenes.ScenePathOf(CellVfx))).ToArray();
                    Assert.That(effects, Is.Not.Empty, "前提: 配置済みの VisualEffect がある");

                    for (var frame = 0; frame < PausedFrames; frame++)
                    {
                        await UniTask.NextFrame();
                        Assert.That(effects.Sum(effect => effect.aliveParticleCount), Is.Zero,
                            $"止まっている間に粒子が出た（frame {frame}）");
                        Assert.That(effects.Count(effect => effect.enabled), Is.Zero,
                            $"止まっている間に開始した（frame {frame}）");
                    }

                    layer.IsPaused = false;
                    var frames = 0;
                    for (; frames < ParticleFrameLimit && !effects.All(effect => effect.aliveParticleCount > 0); frames++)
                    {
                        await UniTask.NextFrame();
                    }

                    TestContext.Out.WriteLine(
                        $"pausedFrames={PausedFrames} resumeToAliveFrames={frames} frameLimit={ParticleFrameLimit} " +
                        $"alive={string.Join(",", effects.Select(effect => effect.aliveParticleCount))}");
                    Assert.That(effects.All(effect => effect.enabled), Is.True, "Resume の後に開始しない VisualEffect がある");
                    Assert.That(effects.All(effect => effect.aliveParticleCount > 0), Is.True, "Resume の後に粒子が増えない");
                });
            });

        // ═══ AC5（S-4d4a AC9）: 非対応・未構成では、新しく載せた実 Scene の VisualEffect が 1 つも有効にならず、
        //     動的な起動も何もせず、ログも増えない ═══

        [UnityTest]
        public IEnumerator Unsupported_RealVfxScenes_EnableNothingAndLogNothing()
            => UniTask.ToCoroutine(async () =>
            {
                await AssertRealScenesStayDisabledAsync(CreateHost(UnsupportedFacts));
            });

        [UnityTest]
        public IEnumerator Unconfigured_RealVfxScenes_EnableNothingAndLogNothing()
            => UniTask.ToCoroutine(async () =>
            {
                await AssertRealScenesStayDisabledAsync(CreateHost((VfxGraphBackend?)null));
            });

        // ═══ AC6 (a)(b): 親の unload で VFX Scene の GameObject が残らない ═══

        [UnityTest]
        public IEnumerator ParentUnload_RemovesVfxSceneObjects()
            => UniTask.ToCoroutine(() => WithTestScenesAsync(CreateHost(RealFacts), async scenes =>
            {
                await scenes.Director.AddScene(Season, null, CancellationToken.None);
                await scenes.Director.AddScene(Cell, null, CancellationToken.None);
                await scenes.Director.AddScene(CellVfx, null, CancellationToken.None);
                var atmospherePath = scenes.ScenePathOf(Atmosphere);
                var cellVfxPath = scenes.ScenePathOf(CellVfx);
                Assert.That(CountObjectsIn(cellVfxPath), Is.GreaterThan(0), "前提: Cell VFX Scene の GameObject が載っている");
                Assert.That(CountObjectsIn(atmospherePath), Is.GreaterThan(0), "前提: Atmosphere Scene の GameObject が載っている");

                await scenes.Director.UnloadScene(Cell);
                var cellFrames = await WaitUntilGoneAsync(cellVfxPath);
                TestContext.Out.WriteLine(
                    $"cellUnloadFrames={cellFrames} frameLimit={SceneFrameLimit} cellVfxObjects={CountObjectsIn(cellVfxPath)} " +
                    $"atmosphereObjects={CountObjectsIn(atmospherePath)}");
                Assert.That(scenes.Director.IsSceneLoaded(CellVfx), Is.False);
                Assert.That(CountObjectsIn(cellVfxPath), Is.Zero, "Cell の unload の後に Cell VFX の GameObject が残る");
                Assert.That(CountObjectsIn(atmospherePath), Is.GreaterThan(0), "Cell の unload は Season の子に触れない");

                await scenes.Director.UnloadScene(Season);
                var seasonFrames = await WaitUntilGoneAsync(atmospherePath);
                TestContext.Out.WriteLine(
                    $"seasonUnloadFrames={seasonFrames} frameLimit={SceneFrameLimit} atmosphereObjects={CountObjectsIn(atmospherePath)}");
                Assert.That(scenes.Director.IsSceneLoaded(Atmosphere), Is.False);
                Assert.That(CountObjectsIn(atmospherePath), Is.Zero, "Season の unload の後に Atmosphere の GameObject が残る");
            }));

        // ─── 実 Scene の観測 ───

        private async UniTask<VisualEffect[]> AssertEnabledAndEmittingAsync(string scenePath, string expectedAssetPath)
        {
            var expected = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(expectedAssetPath);
            Assert.That(expected != null, Is.True, $"VFX asset が無い: {expectedAssetPath}");
            var effects = EffectsIn(scenePath);
            Assert.That(effects, Is.Not.Empty, $"Scene に VisualEffect が無い: {scenePath}");

            // 有効化は登録の後の最初の反映点（LateUpdate）で起きる。
            var enableFrames = 0;
            for (; enableFrames < SceneFrameLimit && !effects.All(effect => effect.enabled); enableFrames++)
            {
                await UniTask.NextFrame();
            }

            TestContext.Out.WriteLine($"scene={scenePath} enabledAfterFrames={enableFrames}");
            Assert.That(effects.All(effect => effect.enabled), Is.True, $"対応時に有効にならない VisualEffect がある: {scenePath}");
            Assert.That(effects.All(effect => effect.visualEffectAsset == expected), Is.True,
                $"Scene の VisualEffect が本番の .vfx を参照していない: {scenePath}");

            var frame = 0;
            for (; frame < ParticleFrameLimit && !effects.All(IsActive); frame++)
            {
                await UniTask.NextFrame();
            }

            TestContext.Out.WriteLine(
                $"scene={scenePath} effects={effects.Length} activeFrame={frame} frameLimit={ParticleFrameLimit} " +
                $"alive={string.Join(",", effects.Select(effect => effect.aliveParticleCount))}");
            Assert.That(effects.All(IsActive), Is.True, $"上限フレーム内に粒子が出ない: {scenePath}");
            return effects;

            static bool IsActive(VisualEffect effect) => effect.HasAnySystemAwake() || effect.aliveParticleCount > 0;
        }

        private async UniTask AssertRenderBoundsInsideCellAsync(VisualEffect[] effects, Bounds cellVolume)
        {
            var renderers = effects.Select(effect => effect.GetComponent<VFXRenderer>()).ToArray();
            Assert.That(renderers.All(renderer => renderer != null), Is.True, "VisualEffect に VFXRenderer が無い");
            var frame = 0;
            for (; frame < ParticleFrameLimit && renderers.Any(renderer => renderer.bounds.size == Vector3.zero); frame++)
            {
                await UniTask.NextFrame();
            }

            foreach (var renderer in renderers)
            {
                var bounds = renderer.bounds;
                TestContext.Out.WriteLine(
                    $"cellVfxBounds center={bounds.center:R} size={bounds.size:R} cellVolume center={cellVolume.center:R} " +
                    $"size={cellVolume.size:R} frame={frame}");
                // 寸法 0 は体積の収集から落ちるので包含は自明に成り立つ。空振りの合格にしない。
                Assert.That(bounds.size, Is.Not.EqualTo(Vector3.zero), "有効化後の VFXRenderer.bounds の寸法が 0");
                Assert.That(cellVolume.Contains(bounds.min) && cellVolume.Contains(bounds.max), Is.True,
                    "Cell VFX の描画 bounds が親 Cell の体積の外へ出る");
            }
        }

        private UniTask AssertRealScenesStayDisabledAsync(VfxRuntimeHost host) => WithTestScenesAsync(host, async scenes =>
        {
            var logCount = 0;
            void Count(string condition, string stackTrace, LogType type) => logCount++;
            Application.logMessageReceived += Count;
            try
            {
                await scenes.Director.AddScene(Season, null, CancellationToken.None);
                await scenes.Director.AddScene(Cell, null, CancellationToken.None);
                await scenes.Director.AddScene(CellVfx, null, CancellationToken.None);

                // 動的な起動も何もしない（例外を出さず、VisualEffect を作らない）。
                var before = CountVisualEffects();
                var spawned = host.System.Spawn(
                    new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity), OneStarMaker.Runtime.AssetManagement.AssetOwner.Scene(CellVfx));
                Assert.That(spawned.IsActive, Is.False);
                Assert.That(spawned.Play(), Is.False);
                Assert.That(CountVisualEffects(), Is.EqualTo(before));
                await UniTask.NextFrame();
                await UniTask.NextFrame();
            }
            finally
            {
                Application.logMessageReceived -= Count;
            }

            foreach (var identity in new[] { Atmosphere, CellVfx })
            {
                var effects = EffectsIn(scenes.ScenePathOf(identity));
                TestContext.Out.WriteLine(
                    $"scene={identity} effects={effects.Length} enabled={effects.Count(effect => effect.enabled)}");
                Assert.That(effects, Is.Not.Empty, $"前提: {identity} に VisualEffect がある");
                Assert.That(effects.Count(effect => effect.enabled), Is.Zero, $"{identity} で有効な VisualEffect がある");
            }

            TestContext.Out.WriteLine($"consoleLogsDuringLoad={logCount}");
            Assert.That(logCount, Is.Zero, "ロードの間に Unity console のログが増えた");
            LogAssert.NoUnexpectedReceived();
        });

        /// <summary>
        /// test の director が載せた Scene を、本体の成否にかかわらず本体の直後に外す。
        /// 後始末を派生の [UnityTearDown] に分けない: 基底の TearDown が ExitPlayMode（ドメインの再読込）をまたぐので、
        /// 派生にもう 1 つ TearDown を置いた形では、本体の観測が期待どおりでも TearDown が NullReferenceException で落ちた。
        /// </summary>
        private static async UniTask WithTestScenesAsync(VfxRuntimeHost host, Func<TestSceneDirector, UniTask> body)
        {
            var scenes = TestSceneDirector.Create(host);
            try
            {
                await body(scenes);
            }
            finally
            {
                await scenes.DisposeAsync();
            }
        }

        private static VisualEffect[] EffectsIn(string scenePath)
            => Object.FindObjectsByType<VisualEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(effect => string.Equals(effect.gameObject.scene.path, scenePath, StringComparison.Ordinal))
                .ToArray();

        private static int CountObjectsIn(string scenePath)
            => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(transform => string.Equals(transform.gameObject.scene.path, scenePath, StringComparison.Ordinal));

        private static async UniTask<int> WaitUntilGoneAsync(string scenePath)
        {
            var frame = 0;
            for (; frame < SceneFrameLimit && (SceneManager.GetSceneByPath(scenePath).isLoaded || CountObjectsIn(scenePath) > 0); frame++)
            {
                await UniTask.NextFrame();
            }

            return frame;
        }

        /// <summary>
        /// test が所有する SceneDirector と、実 SceneResource の複製で作った Map。
        /// 複製は Season_Spring を根にし（祖先の InGameSession を載せない）、子は VFX の検査に要るものだけにする。
        /// </summary>
        private sealed class TestSceneDirector
        {
            private static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly SceneResourceMap _map;
            private readonly List<SceneResource> _clones;
            private readonly Dictionary<string, SceneResource> _originals;

            private TestSceneDirector(
                SceneDirector director,
                SceneResourceMap map,
                List<SceneResource> clones,
                Dictionary<string, SceneResource> originals)
            {
                Director = director;
                _map = map;
                _clones = clones;
                _originals = originals;
            }

            internal SceneDirector Director { get; }

            /// <param name="host">配置済み VFX を登録するテストの VfxSystem。この director の Scene lifecycle に接続する。</param>
            internal static TestSceneDirector Create(VfxRuntimeHost host)
            {
                var originals = new Dictionary<string, SceneResource>(StringComparer.Ordinal)
                {
                    [Season] = LoadResource(SeasonResourcePath),
                    [Atmosphere] = LoadResource(AtmosphereResourcePath),
                    [Cell] = LoadResource(CellResourcePath),
                    [CellVfx] = LoadResource(CellVfxResourcePath),
                };
                var season = Clone(originals[Season], null);
                var atmosphere = Clone(originals[Atmosphere], season);
                var cell = Clone(originals[Cell], season);
                var cellVfx = Clone(originals[CellVfx], cell);
                var clones = new List<SceneResource> { season, atmosphere, cell, cellVfx };
                var map = SceneTestHelper.CreateSceneResourceMap(clones.ToArray());

                // アプリの起動が作った依存を同じ設定で使う（BuildContentDirectoryIntegrationTests と同じ、検証時だけの観測）。
                var initializer = Type.GetType("SampleGame.DependOnAll.AppInitializer, SampleGame.DependOnAll")
                    ?.GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
                Assert.That(initializer, Is.Not.Null, "実アプリの起動 instance が無い");
                var appDirector = typeof(AbstractApplicationInitializer).GetField("_sceneDirector", Instance)?.GetValue(initializer);
                Assert.That(appDirector, Is.Not.Null, "起動の失敗: アプリの SceneDirector が無い");
                object Field(string name) => typeof(SceneDirector).GetField(name, Instance)!.GetValue(appDirector)!;

                var cameraSystem = new RuntimeCameraSystem(new FakeCameraBackend());
                var factory = new GameSceneFactory(
                    NullLoggerFactory.Instance,
                    cameraSystem,
                    new NoopCameraBackgroundApplier(),
                    CellCompanionSet.Full,
                    new UnusedRenderEnvironment(),
                    host.System);
                var ctor = typeof(SceneDirector).GetConstructors(Instance).Single(c => c.GetParameters().Length == 7);
                var director = (SceneDirector)ctor.Invoke(new[]
                {
                    factory, Field("_uiCommon"), map, Field("_loadingDisplay"), Field("_assetManagement"),
                    Field("_sceneVariant"), Field("_useContentDirectory"),
                });

                // Scene 所有の登録を受理させるため、最初の Scene を載せる前に接続する（本番の initializer と同じ順序）。
                host.BindSceneLifecycle(director.OnSceneEvent, director);
                Assert.That(host.IsSceneLifecycleBound, Is.True, "前提: テストの VfxSystem が director に接続した");
                return new TestSceneDirector(director, map, clones, originals);
            }

            internal string ScenePathOf(string identity)
            {
                // payload の参照（AssetReference）はこの asmdef から読めない。制作契約で .unity は SceneResource と
                // 同じフォルダ・同じ名前に置かれる（VfxSceneStructureTests が payload の GUID まで照合する）。
                var resourcePath = AssetDatabase.GetAssetPath(_originals[identity]);
                var scenePath = System.IO.Path.ChangeExtension(resourcePath, ".unity").Replace('\\', '/');
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null, Is.True, $"前提: {identity} の .unity がある");
                return scenePath;
            }

            internal Bounds VolumeOf(string identity) => _originals[identity].Volume;

            internal async UniTask DisposeAsync()
            {
                try
                {
                    if (Director.IsSceneLoaded(Season))
                    {
                        await Director.UnloadScene(Season);
                    }
                }
                finally
                {
                    Director.Dispose();
                    Object.Destroy(_map);
                    foreach (var clone in _clones)
                    {
                        if (clone != null)
                        {
                            Object.Destroy(clone);
                        }
                    }
                }
            }

            private static SceneResource LoadResource(string path)
            {
                var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(path);
                Assert.That(resource != null, Is.True, $"実 SceneResource が無い: {path}");
                return resource!;
            }

            // 同じ payload・LoadType・体積・StreamByDistance を持つ複製を作り、親子だけを複製の中で張り直す。
            private static SceneResource Clone(SceneResource original, SceneResource? parent)
            {
                var clone = Object.Instantiate(original);
                clone.name = original.name;
                typeof(SceneResource).GetField("_children", Instance)!.SetValue(clone, new List<SceneResource>());
                typeof(SceneResource).GetField("_parent", Instance)!.SetValue(clone, null);
                if (parent != null)
                {
                    SceneTestHelper.AddChild(parent, clone);
                }

                return clone;
            }
        }

        private sealed class NoopCameraBackgroundApplier : ICameraBackgroundApplier
        {
            public void SetClearFlag(ICameraView view, ClearFlag clearFlag, Color color)
            {
            }
        }

        // Lighting を Map に入れないので、RenderEnvironment の lease は取られない。
        private sealed class UnusedRenderEnvironment : IRenderEnvironment
        {
            public bool HasActiveOwner => false;

            public RenderEnvironmentLease Acquire(object ownerKey)
                => throw new InvalidOperationException("VFX の Play テストは RenderEnvironment を使わない");
        }
    }
}
