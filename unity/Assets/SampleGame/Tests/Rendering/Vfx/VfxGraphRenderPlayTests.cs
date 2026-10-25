#nullable enable

using System;
using System.Collections;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using SampleGame.Common.Rendering.Vfx;
using UnityEngine;
using UnityEngine.TestTools;

namespace SampleGame.Tests.Rendering.Vfx
{
    /// <summary>
    /// PC Quality・graphics あり（-WithGraphics）で、Framework の VfxSystem と実 backend を通して起動した最小 VFX が
    /// カメラの描画結果に背景以外の画素として現れることを見る。batch の Editor ではカメラが自動で描画されないので、
    /// RenderTexture を target にしたカメラを明示的に Render し、同期の ReadPixels で読み戻す。
    /// 計測の前に、通常の描画ループに一度 VFX を描かせる（<see cref="WarmUpRegularRenderLoop"/> の理由を参照）。
    /// </summary>
    [TestFixture]
    public sealed class VfxGraphRenderPlayTests : VfxPlayTestFixture
    {
        private const int RenderFrameLimit = 600;
        private const int RenderPixelThreshold = 4;
        private const int WarmUpFrameLimit = 600;

        [UnityTest]
        public IEnumerator Render_SpawnedVfxAppearsInCameraOutput()
        {
            // 計測値: 経路不成立と実行環境の不備を区別するために、最初に出す。
            TestContext.Out.WriteLine(
                $"graphicsDeviceType={SystemInfo.graphicsDeviceType} supportsComputeShaders={SystemInfo.supportsComputeShaders} " +
                $"maxComputeBufferInputsVertex={SystemInfo.maxComputeBufferInputsVertex} colorSpace={QualitySettings.activeColorSpace}");
            AssumeGraphicsSupported();

            var pc = Array.IndexOf(QualitySettings.names, "PC");
            Assert.That(pc, Is.GreaterThanOrEqualTo(0), "Quality に PC が無い");
            PreviousQualityLevel = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(pc, true);

            var backend = new VfxGraphBackend();
            var host = CreateHost(backend);
            yield return WarmUpRegularRenderLoop(backend, host);

            var rt = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
            Objects.Add(rt);
            var cameraGo = NewObject("VfxRenderCamera");
            cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 0f, -4f), Quaternion.identity);
            var camera = cameraGo.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.targetTexture = rt;
            var readback = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            Objects.Add(readback);

            var handle = host.System.Spawn(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity), AssetOwner.App);
            Assert.That(handle.IsActive, Is.True);
            var effect = backend.FindForTest(IdOf(handle))!;

            yield return null;
            var beforePlay = CountLitPixels(camera, rt, readback);
            TestContext.Out.WriteLine($"litPixelsBeforePlay={beforePlay} threshold={RenderPixelThreshold}");
            Assert.That(beforePlay, Is.LessThan(RenderPixelThreshold), "Play の前は描画されない");

            handle.Play();
            var lit = 0;
            var reachedFrame = -1;
            // 描画されない場合に「発射していない」と「発射しているが描画経路に入らない」を分けるための計測。
            // aliveParticleCount は GPU からの読み戻しで遅れるので、最初に 0 を超えたフレームと最大値を残す。
            var maxAlive = 0;
            var firstAliveFrame = -1;
            for (var frame = 0; frame < RenderFrameLimit; frame++)
            {
                yield return null;
                lit = CountLitPixels(camera, rt, readback);
                var alive = (int)effect.aliveParticleCount;
                if (alive > 0 && firstAliveFrame < 0)
                {
                    firstAliveFrame = frame;
                }

                maxAlive = Math.Max(maxAlive, alive);
                if (lit >= RenderPixelThreshold)
                {
                    reachedFrame = frame;
                    break;
                }
            }

            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            var pipelineName = pipeline != null ? pipeline.GetType().Name : "builtin";
            TestContext.Out.WriteLine(
                $"litPixels={lit} reachedFrame={reachedFrame} frameLimit={RenderFrameLimit} " +
                $"maxAliveParticles={maxAlive} firstAliveFrame={firstAliveFrame} " +
                $"hasAnySystemAwake={effect.HasAnySystemAwake()} culled={effect.culled} enabled={effect.enabled} " +
                $"activeInHierarchy={effect.gameObject.activeInHierarchy} renderPipeline={pipelineName}");
            Assert.That(lit, Is.GreaterThanOrEqualTo(RenderPixelThreshold),
                maxAlive > 0
                    ? "上限フレーム内に背景以外の画素が閾値に達しない（粒子は生きている: 描画経路の側）"
                    : "上限フレーム内に背景以外の画素が閾値に達しない（粒子が 0 のまま: 発射・シミュレーションの側）");
        }

        /// <summary>
        /// 計測の前に、通常の描画ループ（有効なカメラの自動描画）に VFX を 1 度描かせる。計測そのものではない。
        /// <para>
        /// 理由: 起動したばかりの batch の Editor では、通常の描画ループが一度も VFX Graph の粒子を描いていないと、
        /// 明示の <c>camera.Render()</c>（と SRP の render request）に粒子が入らない（粒子は生きていて culled でもない）。
        /// 一度描かれると、同じ Editor の process の間は（Play を出入りしても）明示の描画に入る。
        /// Unity 6000.6 / VFX Graph 17.6 / URP 17.6 の batch の Editor で実測した（内部の原因は未確定）。
        /// テスト runner は新しい process でこのテストを走らせるので、ここで条件を揃える。
        /// </para>
        /// <para>
        /// 計測（AC5 の手順）を汚さないため、補助のカメラと VFX は計測用とは別の RT・別の instance にし、
        /// 計測を始める前に両方を壊す（計測の RT に書くのは明示の Render だけ）。補助のカメラにも上限フレーム内で
        /// 描かれなければ、通常の描画ループでも VFX が描かれないので、その時点で失敗させる。
        /// </para>
        /// </summary>
        private IEnumerator WarmUpRegularRenderLoop(VfxGraphBackend backend, VfxRuntimeHost host)
        {
            var warmRt = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
            Objects.Add(warmRt);
            var warmReadback = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            Objects.Add(warmReadback);
            var warmGo = NewObject("VfxWarmUpCamera");
            warmGo.transform.SetPositionAndRotation(new Vector3(0f, 0f, -4f), Quaternion.identity);
            var warmCamera = warmGo.AddComponent<Camera>();
            warmCamera.clearFlags = CameraClearFlags.SolidColor;
            warmCamera.backgroundColor = Color.black;
            warmCamera.targetTexture = warmRt;
            var warmHandle = host.System.Spawn(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity), AssetOwner.App);
            Assert.That(warmHandle.IsActive, Is.True);
            var warmId = IdOf(warmHandle);
            warmHandle.Play();

            var warmLit = 0;
            var warmFrames = 0;
            for (; warmFrames < WarmUpFrameLimit && warmLit < RenderPixelThreshold; warmFrames++)
            {
                yield return null;
                // 自動描画の結果をそのまま読む（ここでは Render しない）。
                warmLit = ReadLitPixels(warmRt, warmReadback);
            }

            TestContext.Out.WriteLine($"warmUpLitPixels={warmLit} warmUpFrames={warmFrames} warmUpFrameLimit={WarmUpFrameLimit}");
            Assert.That(warmLit, Is.GreaterThanOrEqualTo(RenderPixelThreshold),
                "通常の描画ループ（有効なカメラ）でも上限フレーム内に VFX が描かれない");

            warmHandle.Stop();
            warmCamera.enabled = false;
            warmCamera.targetTexture = null;
            UnityEngine.Object.Destroy(warmGo);
            UnityEngine.Object.Destroy(warmRt);
            // Stop の後の Tick で instance が壊れ、Destroy はフレーム末に終わる。
            yield return null;
            yield return null;
            // 計測値: 計測の間に残る有効なカメラ（app が作るものを含む）と VisualEffect の数。
            TestContext.Out.WriteLine(
                $"afterWarmUp enabledCameras={Camera.allCamerasCount} visualEffects={CountVisualEffects()}");
            Assert.That(warmGo == null && backend.FindForTest(warmId) == null, Is.True,
                "前提: 計測の前に補助のカメラと VFX が残っていない");
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
