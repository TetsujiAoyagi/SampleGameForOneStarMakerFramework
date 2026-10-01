#nullable enable
using System;
using NUnit.Framework;
using OneStarMaker.Editor.TestObservation;

namespace OneStarMaker.Tests.Editor.TestObservation
{
    public sealed class ObservationStateTests
    {
        private static TestLeaf Leaf(string id) => new TestLeaf { id = id, name = "Case", fullname = "Fixture.Case",
            uniqueName = "Assembly/Fixture/Case", assemblyName = "OneStarMaker.Tests.TestObservation.Editor", runState = "Runnable" };
        private static TestResultLeaf Result(string id, string value = "Passed") => new TestResultLeaf {
            id = id, name = "Case", fullname = "Fixture.Case", result = value, label = "" };
        private static void Capture(TestObservationState state, int domain, string utc, long ticks)
        {
            state.AddAssemblies(new[] { new AssemblyObservation { domainOrdinal = domain, observedAtUtc = utc,
                ticks = ticks, fullName = "OneStarMaker.Editor.TestObservation, Version=1.0.0.0",
                location = "C:/observer.dll", loadedModuleVersionId = "mvid", diskSha256 = new string('A', 64), status = "observed" } });
        }

        [Test]
        public void PassedLeaf_RequiresBothCallbacksAndSeal()
        {
            var state = TestObservationState.Create(Guid.NewGuid().ToString(), "C:/project", 123, 1000);
            state.Bootstrap("domain-a", "now", 10, 1000);
            state.RunStarted(new[] { Leaf("id-1") }, "now", 20);
            state.Event("started", "id-1", "", "", "now", 21);
            state.Event("finished", "id-1", "Passed", "", "now", 29);
            state.RunFinished(new[] { Result("id-1") }, "now", 30);
            Assert.That(state.status, Is.EqualTo("complete"));
            Assert.That(state.@sealed, Is.False);
            state.Seal();
            Assert.That(state.@sealed && state.sealSequence == state.sequence, Is.True);
        }

        [Test]
        public void MissingStarted_DoesNotBecomeComplete()
        {
            var state = TestObservationState.Create(Guid.NewGuid().ToString(), "C:/project", 123, 1000);
            state.Bootstrap("domain-a", "now", 10, 1000);
            state.RunStarted(new[] { Leaf("id-1") }, "now", 20);
            state.Event("finished", "id-1", "Passed", "", "now", 29);
            state.RunFinished(new[] { Result("id-1") }, "now", 30);
            state.Seal();
            Assert.That(state.status, Is.Not.EqualTo("complete"));
        }

        [Test]
        public void Reload_PreservesFirstSelection()
        {
            var state = TestObservationState.Create(Guid.NewGuid().ToString(), "C:/project", 123, 1000);
            state.Bootstrap("domain-a", "now", 10, 1000);
            state.RunStarted(new[] { Leaf("id-1") }, "now", 20);
            state.Event("started", "id-1", "", "", "now", 21);
            state.BeforeReload("now", 23);
            state.Bootstrap("domain-b", "now", 24, 1000);
            state.Event("finished", "id-1", "Passed", "", "now", 29);
            state.RunFinished(new[] { Result("id-1") }, "now", 30);
            Assert.That(state.domains.Count, Is.EqualTo(2));
            Assert.That(state.selected.Count, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateAttempt_IsInvalid()
        {
            var state = TestObservationState.Create(Guid.NewGuid().ToString(), "C:/project", 123, 1000);
            state.Bootstrap("domain-a", "now", 10, 1000);
            state.RunStarted(new[] { Leaf("id-1") }, "now", 20);
            state.Event("started", "id-1", "", "", "now", 21);
            state.Event("finished", "id-1", "Passed", "", "now", 22);
            state.Event("started", "id-1", "", "", "now", 23);
            state.Event("finished", "id-1", "Passed", "", "now", 24);
            state.RunFinished(new[] { Result("id-1") }, "now", 30);
            Assert.That(state.status, Is.EqualTo("invalid"));
        }

        [Test]
        public void CallbackAfterSeal_ChangesSequenceAndRejectsCandidate()
        {
            var state = TestObservationState.Create(Guid.NewGuid().ToString(), "C:/project", 123, 1000);
            state.Bootstrap("domain-a", "now", 10, 1000);
            state.RunStarted(new[] { Leaf("id-1") }, "now", 20);
            state.Event("started", "id-1", "", "", "now", 21);
            state.Event("finished", "id-1", "Passed", "", "now", 29);
            state.RunFinished(new[] { Result("id-1") }, "now", 30);
            state.Seal();
            var sealedSequence = state.sealSequence;
            state.Event("started", "id-1", "", "", "now", 31);
            Assert.That(state.status, Is.EqualTo("invalid"));
            Assert.That(state.sequence, Is.GreaterThan(sealedSequence));
        }

        [Test]
        public void ReloadAfterRunFinished_CannotSealComplete()
        {
            var state = TestObservationState.Create(Guid.NewGuid().ToString(), "C:/project", 123, 1000);
            state.Bootstrap("domain-a", "now", 10, 1000);
            state.RunStarted(new[] { Leaf("id-1") }, "now", 20);
            state.Event("started", "id-1", "", "", "now", 21);
            state.Event("finished", "id-1", "Passed", "", "now", 29);
            state.RunFinished(new[] { Result("id-1") }, "now", 30);
            Assert.That(state.status, Is.EqualTo("complete"));
            // 終端候補の後で domain が再開しても、初回選択と成功候補を流用させない。
            state.BeforeReload("now", 31);
            state.Bootstrap("domain-after-finish", "now", 32, 1000);
            state.Seal();
            Assert.That(state.status, Is.EqualTo("invalid"));
            Assert.That(state.failure.Exists(x => x.Contains("RunFinished 後")), Is.True);
        }

        [Test]
        public void Seal_RequiresRunStartAndRunFinishCapture()
        {
            foreach (var missing in new[] { "RunStarted", "RunFinished", "" })
            {
                var state = TestObservationState.Create(Guid.NewGuid().ToString(), "C:/project", 123, 1000);
                state.Bootstrap("domain-a", "now", 10, 1000);
                state.RunStarted(new[] { Leaf("id-1") }, "start", 20);
                if (missing != "RunStarted") Capture(state, 0, "start", 20);
                state.Event("started", "id-1", "", "", "now", 21);
                state.Event("finished", "id-1", "Passed", "", "now", 29);
                state.RunFinished(new[] { Result("id-1") }, "finish", 30);
                if (missing != "RunFinished") Capture(state, 0, "finish", 30);
                state.Seal();
                Assert.That(state.status == "complete", Is.EqualTo(missing == ""), missing);
            }
        }

        [Test]
        public void Seal_RequiresEveryInRunReloadResumeCapture()
        {
            foreach (var missingResume in new[] { false, true })
            {
                var state = TestObservationState.Create(Guid.NewGuid().ToString(), "C:/project", 123, 1000);
                state.Bootstrap("domain-a", "boot", 10, 1000);
                state.RunStarted(new[] { Leaf("id-1") }, "start", 20);
                Capture(state, 0, "start", 20);
                state.Event("started", "id-1", "", "", "now", 21);
                state.BeforeReload("before", 23);
                state.Bootstrap("domain-b", "resume", 24, 1000);
                if (!missingResume) Capture(state, 1, "resume", 24);
                state.Event("finished", "id-1", "Passed", "", "now", 29);
                state.RunFinished(new[] { Result("id-1") }, "finish", 30);
                Capture(state, 1, "finish", 30);
                state.Seal();
                Assert.That(state.status == "complete", Is.EqualTo(!missingResume), "reload resume capture");
            }
        }

        [Test]
        public void ExecutedFailedLeaf_WithCompleteCallbacks_IsCompleteObservation()
        {
            var state = TestObservationState.Create(Guid.NewGuid().ToString(), "C:/project", 123, 1000);
            state.Bootstrap("domain-a", "boot", 10, 1000);
            state.RunStarted(new[] { Leaf("id-1") }, "start", 20);
            Capture(state, 0, "start", 20);
            state.Event("started", "id-1", "", "", "event", 21);
            state.Event("finished", "id-1", "Failed", "", "event", 29);
            state.RunFinished(new[] { Result("id-1", "Failed") }, "finish", 30, "Failed");
            Capture(state, 0, "finish", 30);
            state.Seal();
            // test の Failed は XML に残す。callback が揃った観測自体は complete である。
            Assert.That(state.status, Is.EqualTo("complete"));
            Assert.That(state.failure, Is.Empty);
        }

        [Test]
        public void NonexecutedFailedLeaf_IsIncompleteButContradictionIsInvalid()
        {
            var state = TestObservationState.Create(Guid.NewGuid().ToString(), "C:/project", 123, 1000);
            state.Bootstrap("domain-a", "boot", 10, 1000);
            state.RunStarted(new[] { Leaf("id-1") }, "start", 20);
            Capture(state, 0, "start", 20);
            state.RunFinished(new[] { Result("id-1", "Failed") }, "finish", 30, "Failed");
            Capture(state, 0, "finish", 30);
            state.Seal();
            Assert.That(state.status, Is.EqualTo("incomplete"));
            Assert.That(state.failure.Exists(x => x.Contains("callback が欠測")), Is.True);

            var inconsistent = TestObservationState.Create(Guid.NewGuid().ToString(), "C:/project", 123, 1000);
            inconsistent.Bootstrap("domain-a", "boot", 10, 1000);
            inconsistent.RunStarted(new[] { Leaf("id-1") }, "start", 20);
            inconsistent.Event("started", "id-1", "", "", "event", 21);
            inconsistent.Event("finished", "id-1", "Failed", "", "event", 29);
            inconsistent.RunFinished(new[] { Result("id-1", "Failed") }, "finish", 30, "Passed");
            Assert.That(inconsistent.status, Is.EqualTo("invalid"));
        }

        [Test]
        public void UnknownOrUnrepresentedRootResult_IsRejectedByCompleteness()
        {
            foreach (var root in new[] { "Unexpected", "Failed" })
            {
                var state = TestObservationState.Create(Guid.NewGuid().ToString(), "C:/project", 123, 1000);
                state.Bootstrap("domain-a", "boot", 10, 1000);
                state.RunStarted(new[] { Leaf("id-1") }, "start", 20);
                state.Event("started", "id-1", "", "", "event", 21);
                state.Event("finished", "id-1", "Passed", "", "event", 29);
                state.RunFinished(new[] { Result("id-1") }, "finish", 30, root);
                Assert.That(state.status, Is.EqualTo(root == "Unexpected" ? "invalid" : "incomplete"), root);
            }
        }
    }
}
