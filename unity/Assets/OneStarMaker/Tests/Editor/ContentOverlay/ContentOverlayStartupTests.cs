#nullable enable

using NUnit.Framework;
using OneStarMaker.Editor.ContentOverlay;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.BuildContent;

namespace OneStarMaker.Tests.Editor.ContentOverlay
{
    /// <summary>
    /// 起動の config から上書きの登録を決める関数の表（Player の種類 × pair × mode × 3 key の有無）と、key と marker の公開し直し。
    /// Unity を使わない。起動の呼び分けと照合の位置は code の読みと Player の判定で確かめる。
    /// </summary>
    public sealed class ContentOverlayStartupTests
    {
        private const string Path = @"D:\artifacts\bs2b\content\overlay-1";
        private const string Identity = "overlay-1";
        private const string Digest = "0123456789abcdef";
        private const string EditorPlayOnly = "Content overlay is only available in Editor Play Mode.";
        private const string NeedsInstalled = "Content overlay can only be layered on a verified installed revision";
        private const string NeedsBoth = "Content overlay needs both";
        private const string NeedsDigest = "Content overlay on a Player needs editor:contentOverlayDigest.";

        private static ContentOverlayStartupDecision Decide(bool hasPath = true, string path = Path, bool hasIdentity = true, string identity = Identity,
            bool hasDigest = false, string digest = "", bool editorPlay = true, bool player = false, PlayerContentKind kind = PlayerContentKind.Smoke,
            bool modeSpecified = true, string mode = "directory", bool pair = true)
            => ContentOverlayStartup.Decide(hasPath, path, hasIdentity, identity, hasDigest, digest, editorPlay, player, kind, modeSpecified, mode, pair);

        /// <summary>遊べる Player の起動（Editor の Play ではない）。</summary>
        private static ContentOverlayStartupDecision Playable(bool hasPath = true, string path = Path, bool hasIdentity = true, string identity = Identity,
            bool hasDigest = true, string digest = Digest, bool pair = true, string mode = "directory")
            => Decide(hasPath, path, hasIdentity, identity, hasDigest, digest, editorPlay: false, player: true, kind: PlayerContentKind.Playable,
                mode: mode, pair: pair);

        [Test]
        public void NoOverlayKeys_IsNone_ForEverySetting()
        {
            Assert.That(Decide(hasPath: false, path: "", hasIdentity: false, identity: "").Kind, Is.EqualTo(ContentOverlayStartupKind.None));
            // 上書きの key が無い時は、mode・pair・Player の構成に依らず今までの登録の呼び方のまま（判断を増やさない）。
            Assert.That(Decide(hasPath: false, path: "", hasIdentity: false, identity: "", mode: "addressables").Kind, Is.EqualTo(ContentOverlayStartupKind.None));
            Assert.That(Decide(hasPath: false, path: "", hasIdentity: false, identity: "", player: true, editorPlay: false).Kind,
                Is.EqualTo(ContentOverlayStartupKind.None));
            Assert.That(Decide(hasPath: false, path: "", hasIdentity: false, identity: "", pair: false).Kind, Is.EqualTo(ContentOverlayStartupKind.None));
            // 遊べる Player の既定の起動（pair も key も無い）も None（既定の起動を変えない）。
            Assert.That(Playable(hasPath: false, path: "", hasIdentity: false, identity: "", hasDigest: false, digest: "", pair: false).Kind,
                Is.EqualTo(ContentOverlayStartupKind.None));
        }

        [Test]
        public void BothKeys_EditorPlay_DirectoryMode_VerifiedBase_IsOverlay()
        {
            var decision = Decide();
            Assert.That(decision.Kind, Is.EqualTo(ContentOverlayStartupKind.Overlay));
            Assert.That(decision.Path, Is.EqualTo(Path));
            Assert.That(decision.Identity, Is.EqualTo(Identity));
            Assert.That(decision.Digest, Is.Null, "Editor の Play は digest の key が無ければ照合しない");
            // mode が無く pair がある（今の起動が暗黙の directory にする設定）でも重ねる。
            Assert.That(Decide(modeSpecified: false, mode: "").Kind, Is.EqualTo(ContentOverlayStartupKind.Overlay));
        }

        [Test]
        public void EditorPlay_WithDigestKey_CarriesTheValueToTheCheck()
        {
            Assert.That(Decide(hasDigest: true, digest: Digest).Digest, Is.EqualTo(Digest));
            // key があれば空の値でも照合に回す（無視しない）。
            Assert.That(Decide(hasDigest: true, digest: "").Digest, Is.EqualTo(string.Empty));
        }

        [TestCase(true, "", true, Identity)]
        [TestCase(true, Path, true, "")]
        [TestCase(true, "  ", true, Identity)]
        [TestCase(true, Path, false, "")]
        [TestCase(false, "", true, Identity)]
        public void OneKeyOrAnEmptyValue_IsRejected(bool hasPath, string path, bool hasIdentity, string identity)
        {
            var decision = Decide(hasPath: hasPath, path: path, hasIdentity: hasIdentity, identity: identity);
            Assert.That(decision.Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));
            Assert.That(decision.Reason, Does.StartWith(NeedsBoth));
            var player = Playable(hasPath: hasPath, path: path, hasIdentity: hasIdentity, identity: identity);
            Assert.That(player.Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));
            Assert.That(player.Reason, Does.StartWith(NeedsBoth));
        }

        [Test]
        public void DigestOnly_IsRejected_OnBothThePlayerAndTheEditor()
        {
            // digest だけの起動を None にすると、key を残したまま base だけで始まる。
            var player = Playable(hasPath: false, path: "", hasIdentity: false, identity: "");
            Assert.That(player.Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));
            Assert.That(player.Reason, Does.StartWith(NeedsBoth));
            var editor = Decide(hasPath: false, path: "", hasIdentity: false, identity: "", hasDigest: true, digest: Digest);
            Assert.That(editor.Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));
            Assert.That(editor.Reason, Does.StartWith(NeedsBoth));
        }

        [Test]
        public void PlayablePlayer_WithPairAndThreeKeys_IsOverlay()
        {
            var decision = Playable();
            Assert.That(decision.Kind, Is.EqualTo(ContentOverlayStartupKind.Overlay));
            Assert.That(decision.Path, Is.EqualTo(Path));
            Assert.That(decision.Identity, Is.EqualTo(Identity));
            Assert.That(decision.Digest, Is.EqualTo(Digest));
        }

        [TestCase(false, "")]
        [TestCase(true, "")]
        [TestCase(true, "   ")]
        public void PlayablePlayer_WithoutADigestValue_IsRejected(bool hasDigest, string digest)
        {
            var decision = Playable(hasDigest: hasDigest, digest: digest);
            Assert.That(decision.Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));
            Assert.That(decision.Reason, Is.EqualTo(NeedsDigest));
        }

        [Test]
        public void PlayablePlayer_WithoutPair_IsRejectedWithTheInstalledReason()
        {
            var decision = Playable(pair: false);
            Assert.That(decision.Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));
            Assert.That(decision.Reason, Does.StartWith(NeedsInstalled));
            // digest が無くても pair の理由が先（同梱の directory に重ねられない、が利用者に要る理由）。
            Assert.That(Playable(pair: false, hasDigest: false, digest: "").Reason, Does.StartWith(NeedsInstalled));
        }

        [Test]
        public void PlayablePlayer_AddressablesOrUnknownMode_IsRejected()
        {
            Assert.That(Playable(mode: "addressables").Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));
            Assert.That(Playable(mode: "other").Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));
        }

        [Test]
        public void SmokePlayer_IsRejectedWithTheEditorPlayReason_EvenWithPairAndDigest()
        {
            var decision = Decide(hasDigest: true, digest: Digest, editorPlay: false, player: true, kind: PlayerContentKind.Smoke);
            Assert.That(decision.Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));
            Assert.That(decision.Reason, Is.EqualTo(EditorPlayOnly));
            Assert.That(Decide(editorPlay: false, player: true, kind: PlayerContentKind.Smoke, pair: false).Reason, Is.EqualTo(EditorPlayOnly));
        }

        [Test]
        public void NotEditorPlayAndNotPlayer_IsRejectedWithTheEditorPlayReason()
        {
            // Player の構成でない起動は baked の種類を見ない（playable を渡しても重ねない）。
            Assert.That(Decide(editorPlay: false).Reason, Is.EqualTo(EditorPlayOnly));
            Assert.That(Decide(editorPlay: false, kind: PlayerContentKind.Playable, hasDigest: true, digest: Digest).Reason, Is.EqualTo(EditorPlayOnly));
        }

        [Test]
        public void UnverifiedBase_IsRejected()
            => Assert.That(Decide(pair: false).Reason, Does.StartWith(NeedsInstalled), "directoryPath の明示の base には重ねない");

        [Test]
        public void AddressablesMode_IsRejected()
            => Assert.That(Decide(mode: "addressables").Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));

        [Test]
        public void UnknownMode_IsRejected()
            => Assert.That(Decide(mode: "other").Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));

        [Test]
        public void NoModeAndNoPair_WithOnlyOverlayKeys_IsRejected()
            => Assert.That(Decide(modeSpecified: false, mode: "", pair: false).Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));

        [Test]
        public void RegisteredLine_StartsWithTheMarker_AndPutsThePathLast()
        {
            var line = ContentOverlayStartup.FormatRegistered(Identity, "rev-1", @"D:\a b\c");
            Assert.That(line, Does.StartWith(ContentOverlayStartup.RegisteredLogMarker));
            Assert.That(line, Does.Contain("identity=" + Identity + " "));
            Assert.That(line, Does.EndWith(" path=" + @"D:\a b\c"));
        }

        [Test]
        public void EditorConstants_RepublishTheRuntimeValues()
        {
            Assert.That(ContentOverlayPlayConfig.OverlayPathKey, Is.EqualTo(ContentOverlayStartup.OverlayPathKey));
            Assert.That(ContentOverlayPlayConfig.OverlayIdentityKey, Is.EqualTo(ContentOverlayStartup.OverlayIdentityKey));
            Assert.That(ContentOverlayPlayConfig.OverlayDigestKey, Is.EqualTo(ContentOverlayStartup.OverlayDigestKey));
            Assert.That(ContentOverlayPlayConfig.RegisteredLogMarker, Is.EqualTo(ContentOverlayStartup.RegisteredLogMarker));
            Assert.That(ContentOverlayPlayConfig.PlayerFirstSceneLogMarker, Is.EqualTo("[AppInit] Player content first scene: "));
            Assert.That(ContentOverlayStartup.DigestMismatchReason, Is.EqualTo("Content overlay digest does not match the overlay directory."));
            // content: の名前空間の外（Player の protected config の照合に入らない）。
            Assert.That(ContentOverlayPlayConfig.OverlayPathKey, Does.StartWith("editor:"));
            Assert.That(ContentOverlayPlayConfig.OverlayIdentityKey, Does.StartWith("editor:"));
            Assert.That(ContentOverlayPlayConfig.OverlayDigestKey, Does.StartWith("editor:"));
        }
    }
}
