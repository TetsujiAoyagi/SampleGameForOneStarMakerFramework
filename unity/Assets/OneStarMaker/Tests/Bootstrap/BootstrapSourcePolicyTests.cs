#nullable enable

using System;
using NUnit.Framework;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.BuildContent;

namespace OneStarMaker.Tests.Bootstrap
{
    /// <summary>
    /// 起動の構成から登録の位置・UICommon の規則・map の読み口を選ぶ表と、UICommon の読む先の表。Unity を使わない。
    /// </summary>
    public sealed class BootstrapSourcePolicyTests
    {
        // NUnit の test method は public なので、internal の enum は引数に取れない。名前で受けて読み直す。
        private static T Parse<T>(string name) where T : struct, Enum => (T)Enum.Parse(typeof(T), name);

        // ─── Plan ───

        [TestCase("Smoke")]
        [TestCase("Playable")]
        public void Plan_EditorWithDirectory_RegistersFirstAndReadsContent_RegardlessOfKind(string kindName)
        {
            var plan = BootstrapSourcePolicy.Plan(isPlayerConfiguration: false, Parse<PlayerContentKind>(kindName), useContentDirectory: true);
            Assert.That(plan.RegisterContentFirst, Is.True);
            Assert.That(plan.UiCommonRule, Is.EqualTo(BootstrapUiCommonRule.BorrowOrLoad));
            Assert.That(plan.MapSource, Is.EqualTo(BootstrapMapSource.Content));
        }

        [TestCase("Smoke")]
        [TestCase("Playable")]
        public void Plan_EditorWithoutDirectory_DoesNotRegisterAndReadsAddressables_RegardlessOfKind(string kindName)
        {
            var plan = BootstrapSourcePolicy.Plan(isPlayerConfiguration: false, Parse<PlayerContentKind>(kindName), useContentDirectory: false);
            Assert.That(plan.RegisterContentFirst, Is.False);
            Assert.That(plan.UiCommonRule, Is.EqualTo(BootstrapUiCommonRule.BorrowOrLoad));
            Assert.That(plan.MapSource, Is.EqualTo(BootstrapMapSource.Addressables));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Plan_SmokePlayer_KeepsTheCopiedOrder(bool useContentDirectory)
        {
            var plan = BootstrapSourcePolicy.Plan(isPlayerConfiguration: true, PlayerContentKind.Smoke, useContentDirectory);
            Assert.That(plan.RegisterContentFirst, Is.False);
            Assert.That(plan.UiCommonRule, Is.EqualTo(BootstrapUiCommonRule.BorrowOnly));
            Assert.That(plan.MapSource, Is.EqualTo(BootstrapMapSource.PlayerResources));
        }

        [Test]
        public void Plan_PlayablePlayerWithDirectory_UsesTheContentFirstOrder()
        {
            var plan = BootstrapSourcePolicy.Plan(isPlayerConfiguration: true, PlayerContentKind.Playable, useContentDirectory: true);
            Assert.That(plan.RegisterContentFirst, Is.True);
            Assert.That(plan.UiCommonRule, Is.EqualTo(BootstrapUiCommonRule.ContentOnly));
            Assert.That(plan.MapSource, Is.EqualTo(BootstrapMapSource.Content));
        }

        [Test]
        public void Plan_PlayablePlayerWithoutDirectory_Fails()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                BootstrapSourcePolicy.Plan(isPlayerConfiguration: true, PlayerContentKind.Playable, useContentDirectory: false));
            Assert.That(ex!.Message, Is.EqualTo("A playable Player requires a content directory."));
        }

        // ─── DecideUiCommon（8 通り） ───

        [TestCase("BorrowOrLoad", true, true, "ActiveScene")]
        [TestCase("BorrowOrLoad", false, true, "Content")]
        [TestCase("BorrowOrLoad", false, false, "Addressables")]
        [TestCase("BorrowOnly", true, true, "ActiveScene")]
        [TestCase("ContentOnly", false, true, "Content")]
        public void DecideUiCommon_Loads(string ruleName, bool activeSceneHasUiCommon, bool useContentDirectory, string expectedName)
        {
            var decision = BootstrapSourcePolicy.DecideUiCommon(Parse<BootstrapUiCommonRule>(ruleName), activeSceneHasUiCommon, useContentDirectory);
            Assert.That(decision.Failure, Is.Null);
            Assert.That(decision.Origin, Is.EqualTo(Parse<BootstrapUiCommonOrigin>(expectedName)));
        }

        [TestCase("BorrowOnly", false, true, "The Player bootstrap scene must contain UICommon; Addressables fallback is forbidden.")]
        [TestCase("ContentOnly", true, true, "A playable Player must not start from a scene that contains UICommon.")]
        [TestCase("ContentOnly", false, false, "A playable Player requires a content directory.")]
        public void DecideUiCommon_Fails(string ruleName, bool activeSceneHasUiCommon, bool useContentDirectory, string expectedMessage)
        {
            var decision = BootstrapSourcePolicy.DecideUiCommon(Parse<BootstrapUiCommonRule>(ruleName), activeSceneHasUiCommon, useContentDirectory);
            Assert.That(decision.Origin, Is.EqualTo(BootstrapUiCommonOrigin.None));
            Assert.That(decision.Failure, Is.EqualTo(expectedMessage));
        }

        [Test]
        public void BorrowOnly_IgnoresDirectory_WhenUiCommonIsMissing()
        {
            // 動作確認の Player は directory があっても content から UICommon を読まない（写しの順の契約）。
            Assert.That(BootstrapSourcePolicy.DecideUiCommon(BootstrapUiCommonRule.BorrowOnly, false, false).Failure,
                Is.EqualTo(BootstrapSourcePolicy.BorrowOnlyMissingMessage));
            Assert.That(BootstrapSourcePolicy.DecideUiCommon(BootstrapUiCommonRule.BorrowOnly, true, false).Origin,
                Is.EqualTo(BootstrapUiCommonOrigin.ActiveScene));
        }

        [Test]
        public void LogValues_AreStable()
        {
            Assert.That(BootstrapSourcePolicy.FormatOrigin(BootstrapUiCommonOrigin.ActiveScene), Is.EqualTo("active-scene"));
            Assert.That(BootstrapSourcePolicy.FormatOrigin(BootstrapUiCommonOrigin.Content), Is.EqualTo("content"));
            Assert.That(BootstrapSourcePolicy.FormatOrigin(BootstrapUiCommonOrigin.Addressables), Is.EqualTo("addressables"));
            Assert.That(BootstrapSourcePolicy.FormatMapSource(BootstrapMapSource.PlayerResources), Is.EqualTo("player-resources"));
            Assert.That(BootstrapSourcePolicy.FormatMapSource(BootstrapMapSource.Content), Is.EqualTo("content"));
            Assert.That(BootstrapSourcePolicy.FormatMapSource(BootstrapMapSource.Addressables), Is.EqualTo("addressables"));
        }
    }
}
