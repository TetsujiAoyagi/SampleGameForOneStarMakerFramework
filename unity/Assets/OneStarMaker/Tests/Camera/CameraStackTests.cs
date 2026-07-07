#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Runtime.CameraSystem;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Tests.CameraSystem
{
    /// <summary>
    /// CAM-03: レイヤー×スタックポリシー + ハンドルのレッドテスト。
    /// </summary>
    [TestFixture]
    public class CameraStackTests
    {
        private LogicalCamera _fallback = null!;
        private LogicalCamera _gameplayA = null!;
        private LogicalCamera _gameplayB = null!;
        private LogicalCamera _cutscene = null!;
        private CameraStack _stack = null!;

        [SetUp]
        public void SetUp()
        {
            _fallback = new LogicalCamera("fallback");
            _gameplayA = new LogicalCamera("gameplay-a");
            _gameplayB = new LogicalCamera("gameplay-b");
            _cutscene = new LogicalCamera("cutscene");
            _stack = new CameraStack(_fallback);
        }

        [Test]
        public void Push_EmptyStack_BecomesActive()
        {
            using var handle = _stack.Push(_gameplayA, CameraLayer.Gameplay, CameraBlendSpec.Cut);

            Assert.That(_stack.ActiveCamera, Is.SameAs(_gameplayA));
            Assert.That(_stack.IsUsingFallback, Is.False);
            Assert.That(handle.IsDisposed, Is.False);
        }

        [Test]
        public void Push_SameLayer_TopWins()
        {
            using (_stack.Push(_gameplayA, CameraLayer.Gameplay, CameraBlendSpec.Cut))
            using (_stack.Push(_gameplayB, CameraLayer.Gameplay, CameraBlendSpec.Cut))
            {
                Assert.That(_stack.ActiveCamera, Is.SameAs(_gameplayB));
            }
        }

        [Test]
        public void Push_HigherLayer_WinsOverLowerStackTop()
        {
            using (_stack.Push(_gameplayA, CameraLayer.Gameplay, CameraBlendSpec.Cut))
            using (_stack.Push(_gameplayB, CameraLayer.Gameplay, CameraBlendSpec.Cut))
            using (_stack.Push(_cutscene, CameraLayer.Cutscene, CameraBlendSpec.Cut))
            {
                Assert.That(_stack.ActiveCamera, Is.SameAs(_cutscene));
            }
        }

        [Test]
        public void Dispose_Top_RestoresPrevious()
        {
            var topHandle = _stack.Push(_gameplayA, CameraLayer.Gameplay, CameraBlendSpec.Cut);
            var winnerHandle = _stack.Push(_cutscene, CameraLayer.Cutscene, CameraBlendSpec.Cut);

            winnerHandle.Dispose();

            Assert.That(_stack.ActiveCamera, Is.SameAs(_gameplayA));
            topHandle.Dispose();
        }

        [Test]
        public void Dispose_NonTop_RemovesWithoutActiveChange()
        {
            var bottomHandle = _stack.Push(_gameplayA, CameraLayer.Gameplay, CameraBlendSpec.Cut);
            using (_stack.Push(_gameplayB, CameraLayer.Gameplay, CameraBlendSpec.Cut))
            {
                var changes = new List<ActiveCameraChangeInfo>();
                _stack.ActiveCameraChanged += changes.Add;

                bottomHandle.Dispose();

                Assert.That(_stack.ActiveCamera, Is.SameAs(_gameplayB));
                Assert.That(changes, Is.Empty);
            }
        }

        [Test]
        public void Dispose_Twice_IsIdempotent()
        {
            var handle = _stack.Push(_gameplayA, CameraLayer.Gameplay, CameraBlendSpec.Cut);

            Assert.DoesNotThrow(() =>
            {
                handle.Dispose();
                handle.Dispose();
            });

            Assert.That(handle.IsDisposed, Is.True);
        }

        [Test]
        public void AllStacksEmpty_FallbackCameraActive()
        {
            var handle = _stack.Push(_gameplayA, CameraLayer.Gameplay, CameraBlendSpec.Cut);

            handle.Dispose();

            Assert.That(_stack.ActiveCamera, Is.SameAs(_fallback));
            Assert.That(_stack.IsUsingFallback, Is.True);
        }

        [Test]
        public void Push_ActiveChange_ReportsBlendSpec()
        {
            var blend = new CameraBlendSpec
            {
                DurationSec = 1.25f,
                Easing = CameraBlendEasing.EaseInOut,
            };
            ActiveCameraChangeInfo? change = null;
            _stack.ActiveCameraChanged += info => change = info;

            using (_stack.Push(_gameplayA, CameraLayer.Gameplay, blend))
            {
                Assert.That(change, Is.Not.Null);
                Assert.That(change!.Value.BlendSpec.DurationSec, Is.EqualTo(1.25f).Within(1e-5f));
                Assert.That(change.Value.BlendSpec.Easing, Is.EqualTo(CameraBlendEasing.EaseInOut));
                Assert.That(change.Value.NewCamera, Is.SameAs(_gameplayA));
            }
        }

        [Test]
        public void Dispose_ActiveChange_UsesDepartingCameraBlendSpec()
        {
            var departingBlend = new CameraBlendSpec
            {
                DurationSec = 2.5f,
                Easing = CameraBlendEasing.EaseInOut,
            };
            ActiveCameraChangeInfo? change = null;
            _stack.ActiveCameraChanged += info => change = info;

            using (_stack.Push(_gameplayA, CameraLayer.Gameplay, CameraBlendSpec.Cut))
            {
                var departingHandle = _stack.Push(_cutscene, CameraLayer.Cutscene, departingBlend);
                change = null;

                departingHandle.Dispose();

                Assert.That(change, Is.Not.Null);
                Assert.That(change!.Value.BlendSpec.DurationSec, Is.EqualTo(2.5f).Within(1e-5f));
                Assert.That(change.Value.BlendSpec.Easing, Is.EqualTo(CameraBlendEasing.EaseInOut));
                Assert.That(change.Value.NewCamera, Is.SameAs(_gameplayA));
            }
        }
    }
}
