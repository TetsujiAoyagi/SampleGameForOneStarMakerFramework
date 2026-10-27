#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Tests.Rendering.Vfx;
using SampleGame.Common.Rendering.Vfx;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

namespace SampleGame.Tests.Rendering.Vfx
{
    /// <summary>
    /// VFX Graph の対応可否の規則（Unity の状態を読まない）と、実 backend + VfxSystem で判定が 1 回だけ行われること。
    /// 配置済み VFX は判定結果を自分で読まず VfxSystem に登録されるので、判定の出所は VfxSystem の 1 か所だけになる。
    /// Mobile 実機の代わりに、platform の事実を差し替えた規則と実 backend の判定経路を確かめる（実機での実証ではない）。
    /// 対応と判定したときの副作用（VFX Graph の上限を Time.maximumDeltaTime に揃え、手放すと戻す）もここで見る。
    /// </summary>
    [TestFixture]
    public sealed class VfxGraphCapabilityPolicyTests
    {
        private readonly List<IDisposable> _disposables = new();
        private float _originalVfxMaxDeltaTime;

        [SetUp]
        public void SetUp()
        {
            _originalVfxMaxDeltaTime = VFXManager.maxDeltaTime;
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _disposables.Count - 1; i >= 0; i--)
            {
                _disposables[i].Dispose();
            }

            _disposables.Clear();

            // 失敗したテストが書き換えを残しても、後続のテストと project 設定に漏らさない。
            if (VFXManager.maxDeltaTime != _originalVfxMaxDeltaTime)
            {
                VFXManager.maxDeltaTime = _originalVfxMaxDeltaTime;
            }
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
        //  判定（実 backend + VfxSystem）
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
        public void VfxSystemCreation_ReadsFactsOnce_AndSupportedLogsNothing()
        {
            var reads = 0;
            var backend = Backend(() =>
            {
                reads++;
                return Facts();
            });
            Assert.That(reads, Is.EqualTo(0), "backend の ctor は環境を読まない");

            var logger = new FakeVfxLogger();
            CreateSystem(backend, logger);

            Assert.That(reads, Is.EqualTo(1));
            Assert.That(logger.TotalCount, Is.EqualTo(0));
        }

        [Test]
        public void VfxSystemCreation_UnsupportedFacts_LogsOneWarning()
        {
            var logger = new FakeVfxLogger();
            CreateSystem(Backend(() => Facts(GraphicsDeviceType.OpenGLES3)), logger);

            Assert.That(logger.WarningCount, Is.EqualTo(1));
            Assert.That(logger.TotalCount, Is.EqualTo(1));
        }

        /// <summary>評価中の例外は VfxSystem が非対応とみなし、Error を 1 件だけ残す。</summary>
        [Test]
        public void VfxSystemCreation_FactsReadThrows_TreatedAsUnsupported()
        {
            var logger = new FakeVfxLogger();
            var backend = Backend(() => throw new InvalidOperationException("facts unavailable"));

            var system = CreateSystem(backend, logger);

            Assert.That(logger.ErrorCount, Is.EqualTo(1));
            Assert.That(system.LiveInstanceCount, Is.EqualTo(0));
        }

        // ═══════════════════════════════════════════
        //  AC13 VFX Graph の上限の書き換えの所有と寿命
        // ═══════════════════════════════════════════

        /// <summary>
        /// 実 backend を 2 つ作り、順不同で Dispose する。生存中は VFX Graph の上限が Time.maximumDeltaTime に揃い、
        /// 全て手放した後は作る前の値に戻る。
        /// </summary>
        [TestCase(true, TestName = "AC13_TwoBackends_DisposedInCreationOrder_RestoresAfterLast")]
        [TestCase(false, TestName = "AC13_TwoBackends_DisposedInReverseOrder_RestoresAfterLast")]
        public void AC13_TwoBackends_AlignWhileAlive_AndRestoreAfterAllDisposed(bool disposeFirstCreatedFirst)
        {
            var original = VFXManager.maxDeltaTime;
            Assert.That(original, Is.Not.EqualTo(Time.maximumDeltaTime), "前提: 揃える前の値が Time.maximumDeltaTime と違う");
            Assert.That(VfxGraphMaxDeltaTimeAlignment.HoldersForTest, Is.Zero, "前提: 他の backend が持っていない");

            var first = Backend(() => Facts());
            var second = Backend(() => Facts());
            Assert.That(first.QueryCapability(out _), Is.True);
            Assert.That(VFXManager.maxDeltaTime, Is.EqualTo(Time.maximumDeltaTime), "対応と判定した時点で揃える");
            Assert.That(second.QueryCapability(out _), Is.True);
            Assert.That(first.QueryCapability(out _), Is.True);
            Assert.That(VfxGraphMaxDeltaTimeAlignment.HoldersForTest, Is.EqualTo(2), "同じ backend の 2 回目の判定では数えない");

            var (disposedFirst, disposedLast) = disposeFirstCreatedFirst ? (first, second) : (second, first);
            disposedFirst.Dispose();
            Assert.That(VFXManager.maxDeltaTime, Is.EqualTo(Time.maximumDeltaTime), "1 つ残っている間は揃えた値のまま");

            disposedLast.Dispose();
            Assert.That(VFXManager.maxDeltaTime, Is.EqualTo(original), "全て手放した後は作る前の値");
            Assert.That(VfxGraphMaxDeltaTimeAlignment.HoldersForTest, Is.Zero);

            disposedFirst.Dispose();
            Assert.That(VFXManager.maxDeltaTime, Is.EqualTo(original), "2 回目の Dispose は何もしない");
        }

        /// <summary>非対応の判定では VFX Graph の上限に書かない（非対応環境では VFX Graph の設定に触れない）。</summary>
        [Test]
        public void AC13_UnsupportedQuery_DoesNotWrite()
        {
            var original = VFXManager.maxDeltaTime;
            var backend = Backend(() => Facts(GraphicsDeviceType.OpenGLES3));

            Assert.That(backend.QueryCapability(out _), Is.False);
            Assert.That(VFXManager.maxDeltaTime, Is.EqualTo(original));
            Assert.That(VfxGraphMaxDeltaTimeAlignment.HoldersForTest, Is.Zero);

            backend.Dispose();
            Assert.That(VFXManager.maxDeltaTime, Is.EqualTo(original));
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
