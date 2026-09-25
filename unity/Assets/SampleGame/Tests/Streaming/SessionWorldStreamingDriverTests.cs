#nullable enable

using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OneStarMaker.Runtime.Streaming;
using SampleGame.InGame.Streaming;
using UnityEngine;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// S-4b: CurrentCellIdentity は Catalog.Format ではなく active 候補の XZ 体積。
    /// 複数なら identity の ordinal 最小。
    /// </summary>
    [TestFixture]
    public sealed class SessionWorldStreamingDriverTests
    {
        [Test]
        public void CurrentCellIdentity_PicksOrdinalMinAmongOverlappingVolumes()
        {
            var volume = new Bounds(new Vector3(10f, 0f, 10f), new Vector3(20f, 10f, 20f));
            var candidates = new StreamingCandidateSet(new[]
            {
                new StreamingCandidate("Spring_Cell_1_0", volume),
                new StreamingCandidate("Spring_Cell_0_0", volume),
            });
            var backend = new FakeStreamingBackend();
            using var driver = new SessionWorldStreamingDriver(
                backend,
                candidates,
                () => new Vector3(10f, 5f, 10f),
                NullLogger.Instance);

            Assert.That(driver.CurrentCellIdentity, Is.EqualTo("Spring_Cell_0_0"));
        }

        [Test]
        public void CurrentCellIdentity_OutsideVolume_IsNull()
        {
            var volume = new Bounds(Vector3.zero, Vector3.one);
            var candidates = new StreamingCandidateSet(new[]
            {
                new StreamingCandidate("Spring_Cell_0_0", volume),
            });
            var backend = new FakeStreamingBackend();
            using var driver = new SessionWorldStreamingDriver(
                backend,
                candidates,
                () => new Vector3(50f, 0f, 50f),
                NullLogger.Instance);

            Assert.That(driver.CurrentCellIdentity, Is.Null);
        }
    }
}
