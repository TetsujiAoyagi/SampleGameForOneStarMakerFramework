#nullable enable

using NUnit.Framework;
using OneStarMaker.Editor.ContentOverlay;
using OneStarMaker.Runtime;

namespace OneStarMaker.Tests.Editor.ContentOverlay
{
    /// <summary>
    /// 起動の config から上書きの登録を決める関数（受け入れ条件 AC2 の (a)〜(f)・(j)）と、key と marker の公開し直し（(i)）。
    /// Unity を使わない。起動の呼び分け（(g)）と検査の位置（(h)）は code の読みで確かめる。
    /// </summary>
    public sealed class ContentOverlayStartupTests
    {
        private const string Path = @"D:\artifacts\bs2b\content\overlay-1";
        private const string Identity = "overlay-1";

        private static ContentOverlayStartupDecision Decide(bool hasPath = true, string path = Path, bool hasIdentity = true, string identity = Identity,
            bool editorPlay = true, bool player = false, bool modeSpecified = true, string mode = "directory", bool pair = true)
            => ContentOverlayStartup.Decide(hasPath, path, hasIdentity, identity, editorPlay, player, modeSpecified, mode, pair);

        [Test]
        public void NoOverlayKeys_IsNone_ForEverySetting()
        {
            Assert.That(Decide(hasPath: false, path: "", hasIdentity: false, identity: "").Kind, Is.EqualTo(ContentOverlayStartupKind.None));
            // 上書きの key が無い時は、mode・pair・Player の構成に依らず今までの登録の呼び方のまま（判断を増やさない）。
            Assert.That(Decide(hasPath: false, path: "", hasIdentity: false, identity: "", mode: "addressables").Kind, Is.EqualTo(ContentOverlayStartupKind.None));
            Assert.That(Decide(hasPath: false, path: "", hasIdentity: false, identity: "", player: true, editorPlay: false).Kind,
                Is.EqualTo(ContentOverlayStartupKind.None));
            Assert.That(Decide(hasPath: false, path: "", hasIdentity: false, identity: "", pair: false).Kind, Is.EqualTo(ContentOverlayStartupKind.None));
        }

        [Test]
        public void BothKeys_EditorPlay_DirectoryMode_VerifiedBase_IsOverlay()
        {
            var decision = Decide();
            Assert.That(decision.Kind, Is.EqualTo(ContentOverlayStartupKind.Overlay));
            Assert.That(decision.Path, Is.EqualTo(Path));
            Assert.That(decision.Identity, Is.EqualTo(Identity));
            // mode が無く pair がある（今の起動が暗黙の directory にする設定）でも重ねる。
            Assert.That(Decide(modeSpecified: false, mode: "").Kind, Is.EqualTo(ContentOverlayStartupKind.Overlay));
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
            Assert.That(decision.Reason, Is.Not.Empty);
        }

        [Test]
        public void UnverifiedBase_IsRejected()
            => Assert.That(Decide(pair: false).Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected), "directoryPath の明示の base には重ねない");

        [Test]
        public void AddressablesMode_IsRejected()
            => Assert.That(Decide(mode: "addressables").Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));

        [Test]
        public void UnknownMode_IsRejected()
            => Assert.That(Decide(mode: "other").Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));

        [Test]
        public void PlayerConfigurationOrNotEditorPlay_IsRejected()
        {
            Assert.That(Decide(player: true).Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));
            Assert.That(Decide(editorPlay: false).Kind, Is.EqualTo(ContentOverlayStartupKind.Rejected));
        }

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
            Assert.That(ContentOverlayPlayConfig.RegisteredLogMarker, Is.EqualTo(ContentOverlayStartup.RegisteredLogMarker));
            // content: の名前空間の外（Player の protected config の照合に入らない）。
            Assert.That(ContentOverlayPlayConfig.OverlayPathKey, Does.StartWith("editor:"));
            Assert.That(ContentOverlayPlayConfig.OverlayIdentityKey, Does.StartWith("editor:"));
        }
    }
}
