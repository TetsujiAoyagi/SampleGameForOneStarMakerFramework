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
        public void ProgressRoundTrip_DerivesRunPhaseWithoutUnfrozenJsonFlags()
        {
            var id = Guid.NewGuid().ToString();
            var directory = Path.Combine(Path.GetTempPath(), id);
            Directory.CreateDirectory(directory);
            try
            {
                var writer = new TestObservationWriter(Path.Combine(directory, "observation.json"));
                var state = TestObservationState.Create(id, "C:/project", 123, 1000);
                state.Bootstrap("domain-a", "boot", 10, 1000);
                state.RunStarted(new[] { new TestLeaf { id = "leaf", name = "Case", fullname = "Fixture.Case",
                    uniqueName = "Tests/Case", assemblyName = "OneStarMaker.Tests.TestObservation.Editor", runState = "Runnable" } }, "start", 20);
                writer.Progress(state);
                var recovered = writer.Read(id, "C:/project", 123, 1000)!;
                Assert.That(recovered.runStarted, Is.True);
                Assert.That(recovered.runFinished, Is.False);
                recovered.BeforeReload("before", 23);
                recovered.Bootstrap("domain-b", "resume", 24, 1000);
                recovered.Event("started", "leaf", "", "", "event", 25);
                recovered.Event("finished", "leaf", "Passed", "", "event", 26);
                recovered.RunFinished(new[] { new TestResultLeaf { id = "leaf", name = "Case", fullname = "Fixture.Case",
                    result = "Passed", label = "" } }, "finish", 30);
                writer.Progress(recovered);
                var finished = writer.Read(id, "C:/project", 123, 1000)!;
                Assert.That(finished.runStarted && finished.runFinished, Is.True);
                Assert.That(finished.domains.Count, Is.EqualTo(2));
                var json = File.ReadAllText(writer.ProgressPath);
                // 内部の寿命判定は復元できるが、凍結 v1 の top-level に bool を漏らさない。
                Assert.That(json, Does.Not.Contain("\"runStarted\":"));
                Assert.That(json, Does.Not.Contain("\"runFinished\":"));
                File.WriteAllText(writer.ProgressPath, json.Insert(json.IndexOf('{') + 1, "\n  \"runStarted\": true,"));
                Assert.Throws<IOException>(() => writer.Read(id, "C:/project", 123, 1000));
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

        [Test]
        public void RecoveryRecord_MissingAndWrongInvocationAreRejectedWhenExpected()
        {
            var id = Guid.NewGuid().ToString();
            var directory = Path.Combine(Path.GetTempPath(), id);
            Directory.CreateDirectory(directory);
            try
            {
                var writer = new TestObservationWriter(Path.Combine(directory, "observation.json"));
                Assert.That(writer.ReadRecovery(id, "C:/project", false), Is.Null);
                Assert.Throws<IOException>(() => writer.ReadRecovery(id, "C:/project", true));
                var record = new ReloadSettingsRecord { invocationId = id, projectPath = "C:/project",
                    enterPlayModeOptionsEnabled = true, enterPlayModeOptions = 3 };
                writer.CreateRecovery(record);
                Assert.Throws<IOException>(() => writer.ReadRecovery(Guid.NewGuid().ToString(), "C:/project", true));
                Assert.That(writer.ReadRecovery(id, "C:/project", true)!.restored, Is.False);
                Assert.Throws<IOException>(() => writer.ReadRestoredRecovery(id, "C:/project"));
                File.WriteAllText(writer.RecoveryPath, "{\"invocationId\":\"" + id + "\",\"projectPath\":\"C:/project\"}");
                Assert.Throws<IOException>(() => writer.ReadRecovery(id, "C:/project", true));
                File.WriteAllText(writer.RecoveryPath, "{\"invocationId\":\"" + id + "\",\"projectPath\":\"C:/project\",\"enterPlayModeOptionsEnabled\":\"true\",\"restored\":false,\"enterPlayModeOptions\":3}");
                Assert.Throws<IOException>(() => writer.ReadRecovery(id, "C:/project", true));
                record.restored = true;
                writer.SaveRecovery(record);
                Assert.That(writer.ReadRestoredRecovery(id, "C:/project").restored, Is.True);
                File.Delete(writer.RecoveryPath);
                Assert.Throws<IOException>(() => writer.ReadRecovery(id, "C:/project", true));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void RecoveryIsExpectedOnlyAfterSelectedFixtureStarts()
        {
            var state = TestObservationState.Create(Guid.NewGuid().ToString(), "C:/project", 123, 1000);
            state.Bootstrap("domain-a", "now", 10, 1000);
            state.RunStarted(new[] { new TestLeaf { id = "reload", name = "RealDomainReloadRoundTrip",
                fullname = "OneStarMaker.Tests.Editor.TestObservation.ObservationReloadTests.RealDomainReloadRoundTrip",
                uniqueName = "Tests/Reload", assemblyName = "OneStarMaker.Tests.TestObservation.Editor", runState = "Runnable" } }, "now", 20);
            Assert.That(TestObservationBootstrap.RecoveryExpected(state), Is.False);
            state.Event("started", "reload", "", "", "now", 21);
            Assert.That(TestObservationBootstrap.RecoveryExpected(state), Is.True);
        }
    }
}
