#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent.Distribution;

namespace OneStarMaker.Tests.BuildContent.Distribution
{
    /// <summary>止まる規則の表（AC2）。installer の失敗は 12 個の code と未知の例外を、証拠と読み直しの全ての組で表の行に 1 つずつ割り当てる。</summary>
    public sealed class ContentAcquisitionRulesTests
    {
        private static IEnumerable<TestCaseData> InstallFailureCases()
        {
            var codes = Enum.GetValues(typeof(ContentDeliveryFailureCode)).Cast<ContentDeliveryFailureCode?>().Append(null).ToArray();
            Assert.That(codes, Has.Length.EqualTo(13));
            foreach (var code in codes)
            foreach (var source in new[] { false, true })
            foreach (var delivered in new[] { false, true })
            foreach (var present in new[] { false, true })
                yield return new TestCaseData(code, source, delivered, present)
                    .SetName("InstallFailure(" + (code?.ToString() ?? "UnknownException") + ",i=" + source + ",ii=" + delivered + ",present=" + present + ")");
        }

        [TestCaseSource(nameof(InstallFailureCases))]
        public void InstallFailure_IsDecidedByEvidenceAndTheReRead_NotByTheCode(ContentDeliveryFailureCode? code, bool source, bool delivered, bool present)
        {
            var decision = ContentAcquisitionRules.ForInstallFailure(code, source, delivered, present);
            if (present)
                Assert.That(decision.Action, Is.EqualTo(ContentAcquisitionAction.ReapplyArrival), "到着点に現れた対象は code に依らず到着点の側");
            else if (delivered)
                Assert.That((decision.Action, decision.Kind), Is.EqualTo((ContentAcquisitionAction.NextOrigin, ContentOriginFailureKind.Integrity)));
            else if (source)
                Assert.That((decision.Action, decision.Kind), Is.EqualTo((ContentAcquisitionAction.NextOrigin, ContentOriginFailureKind.Unreachable)));
            else
                Assert.That((decision.Action, decision.State), Is.EqualTo((ContentAcquisitionAction.End, ContentAcquisitionState.ArrivalFailure)),
                    "証拠の無い失敗は取得元を替えても直らない");
        }

        // 規則の型は internal なので、test の引数は名前で受ける（public の test method に internal の型を出さない）。
        [TestCase("Absent", "Proceed", null)]
        [TestCase("SameDigestVerified", "End", "AlreadyInstalled")]
        [TestCase("DifferentDigest", "End", "ArrivalDigestConflict")]
        [TestCase("CorruptOrUnverified", "End", "ArrivalFailure")]
        [TestCase("CacheUnreadable", "End", "ArrivalFailure")]
        public void Arrival(string observation, string action, string? state)
        {
            var decision = ContentAcquisitionRules.ForArrival(Parse<ContentArrivalObservation>(observation));
            Assert.That(decision.Action.ToString(), Is.EqualTo(action));
            if (state != null) Assert.That(decision.State.ToString(), Is.EqualTo(state));
        }

        [TestCase("Matched", "Proceed", null, "None")]
        [TestCase("Unreachable", "NextOrigin", null, "Unreachable")]
        [TestCase("DigestMismatch", "NextOrigin", null, "Integrity")]
        [TestCase("TooLarge", "NextOrigin", null, "Integrity")]
        [TestCase("ManifestInvalid", "End", "TargetUnusable", "None")]
        [TestCase("IdentityMismatch", "End", "TargetUnusable", "None")]
        [TestCase("TargetMismatch", "End", "TargetUnusable", "None")]
        [TestCase("Incompatible", "End", "TargetUnusable", "None")]
        [TestCase("OverBudget", "End", "ArrivalFailure", "None")]
        public void PreRead(string outcome, string action, string? state, string kind)
        {
            var decision = ContentAcquisitionRules.ForPreRead(Parse<ContentPreReadOutcome>(outcome));
            Assert.That(decision.Action.ToString() + "/" + decision.Kind, Is.EqualTo(action + "/" + kind));
            if (state != null) Assert.That(decision.State.ToString(), Is.EqualTo(state));
        }

        [Test]
        public void EveryArrivalAndPreReadValueIsInTheTable()
        {
            Assert.That(Enum.GetNames(typeof(ContentArrivalObservation)), Has.Length.EqualTo(5));
            Assert.That(Enum.GetNames(typeof(ContentPreReadOutcome)), Has.Length.EqualTo(9));
        }

        private static T Parse<T>(string name) where T : struct => (T)Enum.Parse(typeof(T), name);

        [Test]
        public void Query_OnlyFoundProceeds()
        {
            foreach (ContentQueryOutcome outcome in Enum.GetValues(typeof(ContentQueryOutcome)))
                Assert.That(ContentAcquisitionRules.ForQuery(outcome).Action,
                    Is.EqualTo(outcome == ContentQueryOutcome.Found ? ContentAcquisitionAction.Proceed : ContentAcquisitionAction.NextOrigin), outcome.ToString());
        }

        [Test]
        public void SuccessAndExhaustion()
        {
            Assert.That(ContentAcquisitionRules.ForInstallSuccess(false).State, Is.EqualTo(ContentAcquisitionState.Acquired));
            Assert.That(ContentAcquisitionRules.ForInstallSuccess(true).State, Is.EqualTo(ContentAcquisitionState.AlreadyInstalled));
            Assert.That(ContentAcquisitionRules.ForExhausted(true).State, Is.EqualTo(ContentAcquisitionState.IntegrityFailure));
            Assert.That(ContentAcquisitionRules.ForExhausted(false).State, Is.EqualTo(ContentAcquisitionState.NotFound));
        }
    }
}
