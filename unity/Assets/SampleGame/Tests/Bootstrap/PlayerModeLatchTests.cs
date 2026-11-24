#nullable enable

using NUnit.Framework;
using SampleGame.DependOnAll;

namespace OneStarMaker.Tests.Bootstrap
{
    /// <summary>
    /// SampleGame の Player の構成の latch（開始 Scene の名前を成果物の marker として読む）の表。Unity を使わない。
    /// </summary>
    public sealed class PlayerModeLatchTests
    {
        [TestCase("UICommon")]
        [TestCase("PlayablePlayerStart")]
        [TestCase("Title")]
        [TestCase("")]
        public void Editor_IsNeverThePlayerConfiguration(string activeSceneName)
        {
            Assert.That(AppInitializer.IsBs4PlayerMode(isEditor: true, activeSceneName), Is.False);
        }

        [TestCase("UICommon")]
        [TestCase("PlayablePlayerStart")]
        public void Player_StartScenesOfBothKinds_AreThePlayerConfiguration(string activeSceneName)
        {
            Assert.That(AppInitializer.IsBs4PlayerMode(isEditor: false, activeSceneName), Is.True);
        }

        [TestCase("uicommon")]
        [TestCase("PlayablePlayerStart ")]
        [TestCase("Title")]
        [TestCase("")]
        public void Player_OtherNames_AreNotThePlayerConfiguration(string activeSceneName)
        {
            // 名前は ordinal に一致する時だけ。大文字小文字の違いや空白を許すと、別の Scene から始まる exe が Player の構成になりうる。
            Assert.That(AppInitializer.IsBs4PlayerMode(isEditor: false, activeSceneName), Is.False);
        }

        [Test]
        public void PlayableStartSceneName_IsTheCommittedSceneName()
        {
            Assert.That(DirectoryBootstrapKeys.PlayableStartSceneName, Is.EqualTo("PlayablePlayerStart"));
        }
    }
}
