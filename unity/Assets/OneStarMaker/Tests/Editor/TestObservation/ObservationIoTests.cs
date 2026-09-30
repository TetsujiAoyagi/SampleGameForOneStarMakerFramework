#nullable enable
using System;
using System.IO;
using NUnit.Framework;
using OneStarMaker.Editor.TestObservation;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.TestObservation
{
    public sealed class ObservationIoTests
    {
        [Test]
        public void ProgressRoundTrip_PreservesLongTicksAndIdentity()
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "observation.json");
                var writer = new TestObservationWriter(path);
                var id = Path.GetFileName(directory);
                var state = TestObservationState.Create(id, "C:/project", 123, 10000000);
                state.Bootstrap("domain-a", "now", 9007199254740993L, 10000000);
                writer.Progress(state);
                var recovered = writer.Read(id, "C:/project", 123, 10000000);
                Assert.That(recovered, Is.Not.Null);
                Assert.That(recovered!.domains[0].bootstrapTicks, Is.EqualTo(9007199254740993L));
                Assert.Throws<IOException>(() => writer.Read(id, "C:/project", 124, 10000000));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void Terminal_IsCreateNewAndRequiresSeal()
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(directory);
            try
            {
                var writer = new TestObservationWriter(Path.Combine(directory, "observation.json"));
                var state = TestObservationState.Create(Guid.NewGuid().ToString(), "C:/project", 123, 1000);
                Assert.Throws<IOException>(() => writer.Terminal(state));
                state.Seal();
                writer.Terminal(state);
                Assert.Throws<IOException>(() => writer.Terminal(state));
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
