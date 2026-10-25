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
    /// </summary>
    [TestFixture]
    public sealed class VfxGraphRenderPlayTests : VfxPlayTestFixture
    {
        private const int RenderFrameLimit = 600;
        private const int RenderPixelThreshold = 4;

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

            var backend = new VfxGraphBackend();
            var host = CreateHost(backend);
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

        private static int CountLitPixels(Camera camera, RenderTexture rt, Texture2D readback)
        {
            camera.Render();
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
