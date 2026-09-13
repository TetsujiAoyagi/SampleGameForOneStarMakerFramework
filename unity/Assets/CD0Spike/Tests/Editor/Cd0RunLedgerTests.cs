#nullable enable

using System;
using NUnit.Framework;

namespace CD0Spike.Tests
{
    public sealed class Cd0RunLedgerTests
    {
        [Test]
        public void CompletedSuccess_IsAcceptedUntilAbandoned()
        {
            var ledger = new Cd0RunLedger(1, Cd0OperationKind.Asset);

            ledger.MarkIssued(1);

            Assert.That(ledger.MarkCompleted(1, succeeded: true), Is.True);
            ledger.MarkCleanupDone(1);
            Assert.That(ledger.CanStartRetry, Is.True);
        }

        [Test]
        public void AbandonedOperation_DrainsBeforeRetryWithoutAcceptingResult()
        {
            var ledger = new Cd0RunLedger(4, Cd0OperationKind.Scene);

            ledger.MarkIssued(4);
            ledger.Abandon(4);

            Assert.That(ledger.MarkCompleted(4, succeeded: true), Is.False);
            Assert.That(ledger.CanStartRetry, Is.False);
            ledger.MarkCleanupDone(4);
            Assert.That(ledger.CanStartRetry, Is.True);
        }

        [Test]
        public void StaleGeneration_CannotMutateCurrentRun()
        {
            var ledger = new Cd0RunLedger(7, Cd0OperationKind.Asset);

            Assert.Throws<InvalidOperationException>(() => ledger.MarkIssued(6));
            Assert.That(ledger.Issued, Is.False);
        }

        [Test]
        public void CleanupBeforeTerminalEvent_IsRejected()
        {
            var ledger = new Cd0RunLedger(1, Cd0OperationKind.Scene);
            ledger.MarkIssued(1);

            Assert.Throws<InvalidOperationException>(() => ledger.MarkCleanupDone(1));
        }

        [Test]
        public void DuplicateCleanup_IsRejected()
        {
            var ledger = new Cd0RunLedger(1, Cd0OperationKind.Asset);
            ledger.MarkIssued(1);
            ledger.MarkCompleted(1, succeeded: false);
            ledger.MarkCleanupDone(1);

            Assert.Throws<InvalidOperationException>(() => ledger.MarkCleanupDone(1));
        }

        [Test]
        public void CleanupFailure_BlocksRetryAndCannotBecomeSuccess()
        {
            var ledger = new Cd0RunLedger(2, Cd0OperationKind.Scene);
            ledger.MarkIssued(2);
            ledger.MarkCompleted(2, succeeded: false);

            ledger.MarkCleanupFailed(2);

            Assert.That(ledger.CleanupFailed, Is.True);
            Assert.That(ledger.CanStartRetry, Is.False);
            Assert.Throws<InvalidOperationException>(() => ledger.MarkCleanupDone(2));
        }
    }
}
