#nullable enable

using System.Linq;
using NUnit.Framework;
using OneStarMaker.Runtime.CameraSystem;
using OneStarMaker.Runtime.Streaming;
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
    /// CAM-09: CameraSystemSliceSetup の focus 供給元構成テスト。
    /// </summary>
    [TestFixture]
    public class CameraSystemSliceSetupTests
    {
        [Test]
        public void CameraSystemSliceSetup_CreateStreamingFocusSources_DefaultExcludesMainAndMinimap()
        {
            using var setup = CameraSystemSliceSetup.Create(new FakeCameraBackend());

            var sources = setup.CreateStreamingFocusSources();

            Assert.That(sources, Has.Count.EqualTo(3));
            Assert.That(sources.Select(s => s.View).ToArray(), Is.EqualTo(new[]
            {
                setup.SplitViewA,
                setup.SplitViewB,
                setup.MinimapView,
            }));
            Assert.That(sources.Take(2).All(s => s.IncludeInStreaming), Is.True);
            Assert.That(sources[2].IncludeInStreaming, Is.False);
            Assert.That(sources.Any(s => ReferenceEquals(s.View, setup.MainView)), Is.False);
        }

        [Test]
        public void CameraSystemSliceSetup_CreateStreamingFocusSources_WithIncludeMainView_IncludesMain()
        {
            using var setup = CameraSystemSliceSetup.Create(new FakeCameraBackend());

            var sources = setup.CreateStreamingFocusSources(includeMainView: true);

            Assert.That(sources, Has.Count.EqualTo(4));
            Assert.That(sources[0].View, Is.SameAs(setup.MainView));
            Assert.That(sources[0].IncludeInStreaming, Is.True);
            Assert.That(sources[1].View, Is.SameAs(setup.SplitViewA));
            Assert.That(sources[2].View, Is.SameAs(setup.SplitViewB));
            Assert.That(sources[3].View, Is.SameAs(setup.MinimapView));
            Assert.That(sources[3].IncludeInStreaming, Is.False);
        }
    }
}
