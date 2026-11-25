#nullable enable

using NUnit.Framework;
using OneStarMaker.Editor.ContentOverlay;
using SampleGame.DependOnAll.Editor.Build;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>起動の後の観測の 1 回分（<see cref="PlayerOverlayStartupObserver.Step"/>）の遷移の表。Unity も Player も使わない。</summary>
    public sealed class PlayerOverlayStartupObserverTests
    {
        private const string Overlay = "20261124T030000000Z-over";
        private static readonly string Registered = ContentOverlayPlayConfig.RegisteredLogMarker + "identity=" + Overlay + " baseRevision=base path=D:\\o";
        private static readonly string FirstScene = ContentOverlayPlayConfig.PlayerFirstSceneLogMarker + "scene=Title identity=base kind=playable stable=True";

        private static PlayerOverlayAttempt Pending(bool overlay = true) => new()
        {
            outcome = nameof(PlayerOverlayOutcome.Launched), startup = nameof(PlayerOverlayStartup.Pending),
            hasOverlay = overlay, overlayIdentity = overlay ? Overlay : "",
        };

        [Test]
        public void FirstSceneAfterTheRegistration_IsObserved()
        {
            var step = PlayerOverlayStartupObserver.Step(Pending(), "a\r\n" + Registered + "\r\n" + FirstScene + "\r\n", false, 5);
            Assert.That(step.Startup, Is.EqualTo(PlayerOverlayStartup.Observed));
            Assert.That(step.Changed, Is.True);
        }

        [Test]
        public void FirstSceneWithoutTheRegistration_FailsForAnOverlayLaunch_AndIsObservedWithout()
        {
            Assert.That(PlayerOverlayStartupObserver.Step(Pending(), FirstScene + "\n" + Registered, false, 5).Startup,
                Is.EqualTo(PlayerOverlayStartup.Failed), "登録の行は初回 Scene の行より前に要る");
            var other = Registered.Replace(Overlay, "another-overlay");
            Assert.That(PlayerOverlayStartupObserver.Step(Pending(), other + "\n" + FirstScene, false, 5).Startup,
                Is.EqualTo(PlayerOverlayStartup.Failed), "別の上書きの登録では足りない");
            Assert.That(PlayerOverlayStartupObserver.Step(Pending(overlay: false), FirstScene, false, 5).Startup,
                Is.EqualTo(PlayerOverlayStartup.Observed));
        }

        [Test]
        public void ExitBeforeTheFirstScene_Fails_WithTheFirstExceptionLine()
        {
            var log = "x\nContentDirectoryException: Content overlay digest does not match the overlay directory.\nInvalidOperationException: later\n";
            var step = PlayerOverlayStartupObserver.Step(Pending(), log, true, 3);
            Assert.That(step.Startup, Is.EqualTo(PlayerOverlayStartup.Failed));
            Assert.That(step.Reason, Is.EqualTo("ContentDirectoryException: Content overlay digest does not match the overlay directory."));
            Assert.That(PlayerOverlayStartupObserver.Step(Pending(), "", true, 3).Reason, Is.Not.Empty);
        }

        [Test]
        public void NoFirstSceneWithinTheLimit_StaysPending_ThenTimesOut()
        {
            var waiting = PlayerOverlayStartupObserver.Step(Pending(), Registered, false, PlayerOverlayStartupObserver.TimeoutSeconds);
            Assert.That(waiting.Startup, Is.EqualTo(PlayerOverlayStartup.Pending));
            Assert.That(waiting.Changed, Is.False);
            Assert.That(PlayerOverlayStartupObserver.Step(Pending(), Registered, false, PlayerOverlayStartupObserver.TimeoutSeconds + 1).Startup,
                Is.EqualTo(PlayerOverlayStartup.TimedOut));
        }

        [Test]
        public void ReReadWithoutTheProcess_DecidesFromTheLogAlone()
        {
            // domain reload・Editor の再起動の後、pid の process が無い時は log だけで決める。
            Assert.That(PlayerOverlayStartupObserver.Step(Pending(), Registered + "\n" + FirstScene, true, 999).Startup,
                Is.EqualTo(PlayerOverlayStartup.Observed));
            Assert.That(PlayerOverlayStartupObserver.Step(Pending(), Registered, true, 999).Startup, Is.EqualTo(PlayerOverlayStartup.Failed));
        }

        [Test]
        public void AFinishedRecord_IsNotRewritten()
        {
            var done = Pending();
            done.startup = nameof(PlayerOverlayStartup.Observed);
            var step = PlayerOverlayStartupObserver.Step(done, "", true, 999);
            Assert.That(step.Changed, Is.False);
            Assert.That(step.Startup, Is.EqualTo(PlayerOverlayStartup.Observed));
            var notStarted = new PlayerOverlayAttempt { outcome = nameof(PlayerOverlayOutcome.BaseNotInstalled) };
            Assert.That(PlayerOverlayStartupObserver.Step(notStarted, FirstScene, false, 1).Changed, Is.False);
        }
    }
}
