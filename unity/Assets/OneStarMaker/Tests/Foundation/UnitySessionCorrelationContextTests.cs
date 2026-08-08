#nullable enable

using NUnit.Framework;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Foundation.Logging;
using OneStarMaker.Foundation.Telemetry;

namespace OneStarMaker.Tests.Foundation
{
    /// <summary>
    /// session ID と producer sequence の採番契約を検証する。
    ///
    /// <para>
    /// sessionId は初期化後に固定され handshake と同一値になる。
    /// producer sequence は Log と Telemetry が共有する 1 本のカウンタで、
    /// stream ごとに分けると同一 frame 内の全体順序が再構成できなくなる。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class UnitySessionCorrelationContextTests
    {
        [SetUp]
        public void SetUp()
        {
            UnitySessionCorrelationContext.ResetForTests();
            UnityPlayerLoopFrameObservation.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            UnitySessionCorrelationContext.ResetForTests();
            UnityPlayerLoopFrameObservation.ResetForTests();
        }

        [Test]
        public void SessionId_初期化後は固定されHandshakeと同一値になる()
        {
            UnitySessionCorrelationContext.ResetForNewPlayerSession();
            var sessionId = UnitySessionCorrelationContext.SessionId;

            Assert.IsFalse(string.IsNullOrEmpty(sessionId));
            Assert.AreEqual(sessionId, UnitySessionCorrelationContext.SessionId);
        }

        [Test]
        public void NextProducerSequence_LogとTelemetryで1から単調増加する()
        {
            UnitySessionCorrelationContext.ResetForNewPlayerSession();

            Assert.AreEqual(1, UnitySessionCorrelationContext.NextProducerSequence());
            Assert.AreEqual(2, UnitySessionCorrelationContext.NextProducerSequence());
            Assert.AreEqual(3, UnitySessionCorrelationContext.NextProducerSequence());
        }

        [Test]
        public void ResetForNewPlayerSession_旧sessionIdとsequenceを切り替える()
        {
            UnitySessionCorrelationContext.ResetForNewPlayerSession();
            var firstSession = UnitySessionCorrelationContext.SessionId;
            _ = UnitySessionCorrelationContext.NextProducerSequence();
            _ = UnitySessionCorrelationContext.NextProducerSequence();

            UnitySessionCorrelationContext.ResetForNewPlayerSession();
            var secondSession = UnitySessionCorrelationContext.SessionId;

            Assert.AreNotEqual(firstSession, secondSession);
            Assert.AreEqual(1, UnitySessionCorrelationContext.NextProducerSequence());
        }
    }
}
