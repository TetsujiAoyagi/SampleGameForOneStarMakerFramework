#nullable enable

using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace SampleGame.Tests.Rendering.Vfx
{
    /// <summary>
    /// 本番の Atmosphere VFX と Cell VFX と同じ .vfx だけを置いた専用 Scene（Fixtures/VfxSceneRender.unity）で、
    /// 対応の VfxSystem に登録したときに VFX の画素が出ること（陽性）と、非対応の VfxSystem に登録した新しい instance では
    /// 出ないこと（陰性）を測る。
    /// 実 Scene には地形等の他の Renderer が混ざり、画素の閾値が VFX の描画の証拠にならないので、描画だけをここで見る。
    /// 実 Scene のロード・有効化・寿命は <see cref="VfxScenePlayTests"/> が見る（ここの成功で代替しない）。
    /// </summary>
    [TestFixture]
    public sealed class VfxSceneRenderPlayTests : VfxPlayTestFixture
    {
        private const string RenderFixtureScenePath = FixtureFolder + "VfxSceneRender.unity";
        private const int RenderFrameLimit = 600;
        private const int WarmUpFrameLimit = 600;
        private const int RenderPixelThreshold = 4;

        // ═══ AC4 (c) と AC5 の描画: 専用 Scene で、対応時は画素が出て、非対応に切り替えた後の新しい instance では出ない ═══

        [UnityTest]
        public IEnumerator RenderFixture_SupportedDrawsVfx_UnsupportedNewInstanceDrawsNothing()
            => UniTask.ToCoroutine(async () =>
            {
                WriteGraphicsFacts();
                AssumeGraphicsSupported();
                UsePcQuality();
                var supportedHost = CreateHost(RealFacts);

                // 陽性: 同じ process の陽性対照。これが閾値に達しなければ、陰性の画素 0 は描画の証拠にならない。
                // 専用 Scene は SceneDirector を通さずに載せるので、本番の Scene class の代わりに marker を登録する。
                var supportedScene = await LoadRenderFixtureAsync();
                AssertTargetsSavedDisabled(supportedScene);
                var supportedHandles = AdoptMarkers(supportedHost.System, supportedScene.GetRootGameObjects());
                Assert.That(supportedHandles.All(handle => handle.IsActive), Is.True, "前提: 対応時は登録される");
                foreach (var target in new[] { "AtmosphereVfx", "CellVfx" })
                {
                    var lit = await MeasureTargetAsync(supportedScene, target);
                    TestContext.Out.WriteLine($"supported target={target} litPixels={lit} threshold={RenderPixelThreshold}");
                    Assert.That(lit, Is.GreaterThanOrEqualTo(RenderPixelThreshold), $"対応時に {target} が描画されない");
                }

                supportedHandles.ForEach(handle => handle.Stop());
                await SceneManager.UnloadSceneAsync(supportedScene);

                // 陰性: 非対応の VfxSystem に登録した、新しく載せた instance。
                var unsupportedHost = CreateHost(UnsupportedFacts);
                var unsupportedScene = await LoadRenderFixtureAsync();
                AssertTargetsSavedDisabled(unsupportedScene);
                var unsupportedHandles = AdoptMarkers(unsupportedHost.System, unsupportedScene.GetRootGameObjects());
                Assert.That(unsupportedHandles.Any(handle => handle.IsActive), Is.False, "前提: 非対応では登録されない");
                foreach (var target in new[] { "AtmosphereVfx", "CellVfx" })
                {
                    var lit = await MeasureTargetAsync(unsupportedScene, target);
                    TestContext.Out.WriteLine($"unsupported target={target} litPixels={lit} threshold={RenderPixelThreshold}");
                    Assert.That(lit, Is.LessThan(RenderPixelThreshold), $"非対応時に {target} が描画された");
                    Assert.That(TargetEffect(unsupportedScene, target).enabled, Is.False, $"非対応時に {target} の VisualEffect が有効になった");
                }

                await SceneManager.UnloadSceneAsync(unsupportedScene);
                LogAssert.NoUnexpectedReceived();
            });

        // ─── 専用 Scene の描画 ───

        /// <summary>
        /// 保存時の状態の前提: 対象は非 active、VisualEffect は無効。登録（Adopt）より前に確かめる。対応の VfxSystem では
        /// 登録の後の最初の止まっていない反映点で backend の Play（= 有効化）が届くので、計測の時点では既に有効でありうる。
        /// </summary>
        private static void AssertTargetsSavedDisabled(Scene scene)
        {
            foreach (var target in new[] { "AtmosphereVfx", "CellVfx" })
            {
                var effect = TargetEffect(scene, target);
                Assert.That(effect.gameObject.activeSelf, Is.False, $"前提: {target} は非 active で保存されている");
                Assert.That(effect.enabled, Is.False, $"前提: {target} の VisualEffect は無効で保存されている");
            }
        }

        private static VisualEffect TargetEffect(Scene scene, string targetName)
            => scene.GetRootGameObjects().Single(root => root.name == targetName).GetComponent<VisualEffect>();

        private static async UniTask<Scene> LoadRenderFixtureAsync()
        {
            var operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                RenderFixtureScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.That(operation, Is.Not.Null, $"描画用の fixture Scene を載せられない: {RenderFixtureScenePath}");
            await operation!;
            var scene = SceneManager.GetSceneByPath(RenderFixtureScenePath);
            Assert.That(scene.isLoaded, Is.True);
            return scene;
        }

        /// <summary>
        /// 対象の 1 個だけを active にして、専用カメラの明示の描画で背景以外の画素を数える（上限フレーム内の最大）。
        /// 計測の前に通常の描画ループで同じ .vfx を一度描かせる（d2 の VfxGraphRenderPlayTests と同じ理由）。
        /// </summary>
        private async UniTask<int> MeasureTargetAsync(Scene scene, string targetName)
        {
            var roots = scene.GetRootGameObjects();
            var target = roots.Single(root => root.name == targetName);
            var camera = roots.Single(root => root.name == "VfxRenderCamera").GetComponent<Camera>();
            var effect = target.GetComponent<VisualEffect>();
            Assert.That(target.activeSelf, Is.False, "前提: 対象は計測の前は非 active");

            await WarmUpRegularRenderLoopAsync(effect.visualEffectAsset, camera.transform);

            var rt = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
            Objects.Add(rt);
            var readback = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            Objects.Add(readback);
            camera.targetTexture = rt;
            var disabledOutside = DisableRenderersOutside(scene);
            target.SetActive(true);
            try
            {
                await UniTask.NextFrame();
                AssertOnlyTargetRendererEnabled(target);
                var maxLit = 0;
                var frame = 0;
                for (; frame < RenderFrameLimit; frame++)
                {
                    maxLit = Math.Max(maxLit, CountLitPixels(camera, rt, readback));
                    if (effect.enabled && maxLit >= RenderPixelThreshold)
                    {
                        break;
                    }

                    await UniTask.NextFrame();
                }

                TestContext.Out.WriteLine(
                    $"target={targetName} enabled={effect.enabled} maxLit={maxLit} frame={frame} frameLimit={RenderFrameLimit} " +
                    $"alive={effect.aliveParticleCount}");
                return maxLit;
            }
            finally
            {
                target.SetActive(false);
                camera.targetTexture = null;
                foreach (var renderer in disabledOutside)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                    }
                }
            }
        }

        /// <summary>
        /// 計測の間だけ、専用 Scene の外で有効な Renderer（アプリの起動が載せる UICommon の UIPanel 等）を無効にし、
        /// 無効にしたものを返す（呼び出し側が finally で有効に戻す）。専用 Scene の中は無効にしない:
        /// そこに対象以外の有効な Renderer があれば fixture の欠陥なので、続く前提条件の検査で失敗させる。
        /// </summary>
        private static Renderer[] DisableRenderersOutside(Scene fixtureScene)
        {
            var disabled = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(renderer => renderer.enabled && renderer.gameObject.scene != fixtureScene)
                .ToArray();
            foreach (var renderer in disabled)
            {
                renderer.enabled = false;
            }

            TestContext.Out.WriteLine(
                $"disabledOutsideFixture={disabled.Length} " +
                $"[{string.Join(",", disabled.Select(renderer => renderer.gameObject.scene.path + ":" + renderer.name))}]");
            return disabled;
        }

        // 判定時に描画へ入りうる Renderer は対象の VFXRenderer だけ（非対応では 1 つも無い）。
        // 他の Renderer が有効なら、画素の閾値は対象の描画の証拠にならないので失敗させる。
        private static void AssertOnlyTargetRendererEnabled(GameObject target)
        {
            var others = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(renderer => renderer.enabled && renderer.gameObject != target)
                .Select(renderer => renderer.gameObject.scene.path + ":" + renderer.name)
                .ToArray();
            TestContext.Out.WriteLine($"precondition otherEnabledRenderers={others.Length} [{string.Join(",", others)}]");
            Assert.That(others, Is.Empty, "前提: 判定時に有効な Renderer が対象の VFXRenderer だけ");
        }

        /// <summary>
        /// 通常の描画ループ（有効なカメラの自動描画）に同じ .vfx を一度描かせる。計測そのものではない。
        /// batch の Editor で一度も描かれていない VFX は明示の Render に入らない観察があるため（Architecture §24 §16.6）。
        /// 計測を汚さないよう、補助のカメラ・RT・instance は別に作り、計測の前に壊す。
        /// </summary>
        private async UniTask WarmUpRegularRenderLoopAsync(VisualEffectAsset asset, Transform pose)
        {
            var warmRt = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
            var warmReadback = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var cameraGo = new GameObject("VfxWarmUpCamera");
            var effectGo = new GameObject("VfxWarmUpEffect");
            try
            {
                cameraGo.transform.SetPositionAndRotation(pose.position, pose.rotation);
                var warmCamera = cameraGo.AddComponent<Camera>();
                warmCamera.clearFlags = CameraClearFlags.SolidColor;
                warmCamera.backgroundColor = Color.black;
                warmCamera.targetTexture = warmRt;
                var warmEffect = effectGo.AddComponent<VisualEffect>();
                warmEffect.visualEffectAsset = asset;

                var lit = 0;
                var frames = 0;
                for (; frames < WarmUpFrameLimit && lit < RenderPixelThreshold; frames++)
                {
                    await UniTask.NextFrame();
                    lit = ReadLitPixels(warmRt, warmReadback);
                }

                TestContext.Out.WriteLine($"warmUpLitPixels={lit} warmUpFrames={frames} warmUpFrameLimit={WarmUpFrameLimit}");
                Assert.That(lit, Is.GreaterThanOrEqualTo(RenderPixelThreshold),
                    "通常の描画ループ（有効なカメラ）でも上限フレーム内に VFX が描かれない");
                warmCamera.targetTexture = null;
            }
            finally
            {
                Object.Destroy(cameraGo);
                Object.Destroy(effectGo);
                Object.Destroy(warmRt);
                Object.Destroy(warmReadback);
            }

            // Destroy はフレーム末に終わる。計測の前に補助の Renderer が残らないようにする。
            await UniTask.NextFrame();
            await UniTask.NextFrame();
        }

        private static int CountLitPixels(Camera camera, RenderTexture rt, Texture2D readback)
        {
            camera.Render();
            return ReadLitPixels(rt, readback);
        }

        private static int ReadLitPixels(RenderTexture rt, Texture2D readback)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            try
            {
                readback.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                readback.Apply(false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            var lit = 0;
            foreach (var pixel in readback.GetPixels32())
            {
                if (pixel.r > 8 || pixel.g > 8 || pixel.b > 8)
                {
                    lit++;
                }
            }

            return lit;
        }
    }
}
