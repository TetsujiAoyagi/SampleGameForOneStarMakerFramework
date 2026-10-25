#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Tests.Rendering.Vfx;
using SampleGame.Common.Rendering.Vfx;
using UnityEngine;
using UnityEngine.Rendering;

namespace SampleGame.Tests.Rendering.Vfx
{
    /// <summary>
    /// VFX Graph の対応可否の規則（Unity の状態を読まない）と、判定結果の単一の出所（<see cref="VfxCapabilityState"/>）。
    /// Mobile 実機の代わりに、platform の事実を差し替えた規則と実 backend の判定経路を確かめる（実機での実証ではない）。
    /// </summary>
    [TestFixture]
    public sealed class VfxGraphCapabilityPolicyTests
    {
        private readonly List<IDisposable> _disposables = new();

        [TearDown]
        public void TearDown()
        {
            // 後から作ったものから戻す（writer の Dispose だけが状態を戻すので、順序で残りが出ないように）。
            for (var i = _disposables.Count - 1; i >= 0; i--)
            {
                _disposables[i].Dispose();
            }

            _disposables.Clear();
        }

        private static VfxPlatformFacts Facts(
            GraphicsDeviceType device = GraphicsDeviceType.Direct3D11,
            bool compute = true,
            int ssbo = 8,
            ColorSpace colorSpace = ColorSpace.Linear)
            => new(compute, ssbo, colorSpace, device);

        // ═══════════════════════════════════════════
        //  規則（表駆動）
        // ═══════════════════════════════════════════

        [TestCase(GraphicsDeviceType.Direct3D11)]
        [TestCase(GraphicsDeviceType.Direct3D12)]
        [TestCase(GraphicsDeviceType.Vulkan)]
        [TestCase(GraphicsDeviceType.Metal)]
        [TestCase(GraphicsDeviceType.OpenGLCore)]
        public void Evaluate_SupportedDevice_AllRequirementsMet_IsSupported(GraphicsDeviceType device)
        {
            var supported = VfxGraphCapabilityPolicy.Evaluate(Facts(device), out var reason);

            Assert.That(supported, Is.True);
            Assert.That(reason, Is.Null);
        }

        private static IEnumerable<TestCaseData> UnsupportedCases()
        {
            yield return new TestCaseData(Facts(compute: false), "Compute shaders").SetName("Unsupported_NoComputeShaders");
            yield return new TestCaseData(Facts(ssbo: 0), "maxComputeBufferInputsVertex").SetName("Unsupported_NoVertexSsbo");
            yield return new TestCaseData(Facts(GraphicsDeviceType.OpenGLES3), "OpenGLES3").SetName("Unsupported_OpenGLES3");
            yield return new TestCaseData(Facts(GraphicsDeviceType.Null), "GraphicsDeviceType.Null").SetName("Unsupported_NoGraphicsDevice");
            yield return new TestCaseData(Facts(colorSpace: ColorSpace.Gamma), "Linear").SetName("Unsupported_GammaColorSpace");
        }

        [TestCaseSource(nameof(UnsupportedCases))]
        public void Evaluate_RequirementMissing_IsUnsupportedWithReason(VfxPlatformFacts facts, string reasonFragment)
        {
            var supported = VfxGraphCapabilityPolicy.Evaluate(facts, out var reason);

            Assert.That(supported, Is.False);
            Assert.That(reason, Does.Contain(reasonFragment));
        }

        // ═══════════════════════════════════════════
        //  判定結果の公開（実 backend + VfxSystem）
        // ═══════════════════════════════════════════

        private VfxGraphBackend Backend(Func<VfxPlatformFacts> readFacts)
        {
            var backend = new VfxGraphBackend(readFacts);
            _disposables.Add(backend);
            return backend;
        }

        private VfxSystem CreateSystem(VfxGraphBackend backend, FakeVfxLogger logger)
        {
            var system = new VfxSystem(backend, logger);
            _disposables.Add(system);
            return system;
        }

        [Test]
        public void VfxSystemCreation_ReadsFactsOnce_AndPublishesSupported()
        {
            var reads = 0;
            var backend = Backend(() =>
            {
                reads++;
                return Facts();
            });
            Assert.That(reads, Is.EqualTo(0), "backend の ctor は環境を読まない");

            CreateSystem(backend, new FakeVfxLogger());

            Assert.That(reads, Is.EqualTo(1));
            Assert.That(VfxCapabilityState.Current, Is.EqualTo(VfxCapability.Supported));
        }

        [Test]
        public void VfxSystemCreation_UnsupportedFacts_PublishesUnsupported()
        {
            var logger = new FakeVfxLogger();
            CreateSystem(Backend(() => Facts(GraphicsDeviceType.OpenGLES3)), logger);

            Assert.That(VfxCapabilityState.Current, Is.EqualTo(VfxCapability.Unsupported));
            Assert.That(logger.WarningCount, Is.EqualTo(1));
        }

        /// <summary>評価中の例外では、評価の前に書いた Unsupported が残り、VfxSystem の判定（非対応）と一致する。</summary>
        [Test]
        public void VfxSystemCreation_FactsReadThrows_UnsupportedRemains()
        {
            var logger = new FakeVfxLogger();
            var backend = Backend(() => throw new InvalidOperationException("facts unavailable"));

            var system = CreateSystem(backend, logger);

            Assert.That(VfxCapabilityState.Current, Is.EqualTo(VfxCapability.Unsupported));
            Assert.That(logger.ErrorCount, Is.EqualTo(1));
            Assert.That(system.LiveInstanceCount, Is.EqualTo(0));
        }

        [Test]
        public void BackendDispose_ResetsToUndetermined()
        {
            var backend = Backend(() => Facts());
            CreateSystem(backend, new FakeVfxLogger());
            Assert.That(VfxCapabilityState.Current, Is.EqualTo(VfxCapability.Supported));

            backend.Dispose();

            Assert.That(VfxCapabilityState.Current, Is.EqualTo(VfxCapability.Undetermined));
        }

        /// <summary>後から書いた backend が勝つ。先の backend の Dispose は値を戻さず、writer の Dispose だけが戻す。</summary>
        [Test]
        public void LaterWriterWins_EarlierBackendDisposeDoesNotReset()
        {
            var first = Backend(() => Facts());
            CreateSystem(first, new FakeVfxLogger());
            var second = Backend(() => Facts(GraphicsDeviceType.OpenGLES3));
            CreateSystem(second, new FakeVfxLogger());
            Assert.That(VfxCapabilityState.Current, Is.EqualTo(VfxCapability.Unsupported));

            first.Dispose();
            Assert.That(VfxCapabilityState.Current, Is.EqualTo(VfxCapability.Unsupported), "writer でない backend は戻さない");

            second.Dispose();
            Assert.That(VfxCapabilityState.Current, Is.EqualTo(VfxCapability.Undetermined));
        }

        [Test]
        public void UnsupportedBackend_IsNeverAskedToCreate()
        {
            var backend = Backend(() => Facts(GraphicsDeviceType.Null));
            var logger = new FakeVfxLogger();
            var system = CreateSystem(backend, logger);
            var source = new GameObject("NotAVfxAsset");
            try
            {
                var handle = system.Spawn(new VfxSpawnRequest(source, Vector3.zero, Quaternion.identity), OneStarMaker.Runtime.AssetManagement.AssetOwner.App);
                Assert.That(handle.IsActive, Is.False);
                Assert.That(backend.RootForTest == null, Is.True, "非対応では backend の root も作らない");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }
}
