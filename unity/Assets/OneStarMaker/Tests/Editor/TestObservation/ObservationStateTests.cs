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
    }
}
