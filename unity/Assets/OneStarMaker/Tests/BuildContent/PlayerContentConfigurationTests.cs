#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Foundation.Config;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.Config;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;

namespace OneStarMaker.Tests.BuildContent
{
    public sealed class PlayerContentConfigurationTests
    {
        [Test]
        public void Read_ValidRelativeDirectory_ResolvesUnderInstallRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "osm-bs4-config", Guid.NewGuid().ToString("N"));
            var value = PlayerContentConfiguration.Read(Config("content", "StandaloneWindows64-Player"), root, "StandaloneWindows64-Player");
            Assert.That(value.Path, Is.EqualTo(Path.GetFullPath(Path.Combine(root, "content"))));
        }

        [TestCase("../outside", "StandaloneWindows64-Player")]
        [TestCase("content", "WrongTarget")]
        public void Read_InvalidPathOrTarget_IsRejected(string relative, string configuredTarget)
        {
            var root = Path.Combine(Path.GetTempPath(), "osm-bs4-config", Guid.NewGuid().ToString("N"));
            Assert.Throws<ContentDirectoryException>(() =>
                PlayerContentConfiguration.Read(Config(relative, configuredTarget), root, "StandaloneWindows64-Player"));
        }

        [Test]
        public void Read_MissingSchema_IsRejected()
        {
            var config = new AppConfig(new IConfigProvider[] { new DictionaryProvider(new Dictionary<string, string>()) });
            Assert.Throws<ContentDirectoryException>(() =>
                PlayerContentConfiguration.Read(config, Path.GetTempPath(), "StandaloneWindows64-Player"));
        }

        [Test]
        public void Resolve_DoesNotSupplyDeclaredRuntimeDefaults()
        {
            // Player の baked config に無い key は、directory の宣言から補わない（宣言は Runtime が Warning に使うだけ）。
            // 解決結果は content の欄だけを持ち、world:* の値はどこにも現れない。
            // install root は区切り文字で終わらない path にする（Path.GetTempPath() の末尾の区切りでは包含判定が成り立たない）。
            var root = Path.Combine(Path.GetTempPath(), "osm-bs5a-config", Guid.NewGuid().ToString("N"));
            var baked = new PlayerBakedContentConfiguration(Config("content", "StandaloneWindows64-Player"));
            var merged = Config("content", "StandaloneWindows64-Player");
            Assert.That(merged.ContainsKey("world:cellCompanionSet"), Is.False);
            var resolved = PlayerContentConfiguration.Resolve(baked, merged, root, "StandaloneWindows64-Player");
            Assert.That(resolved.Path, Is.EqualTo(Path.GetFullPath(Path.Combine(root, "content"))));
            Assert.That(resolved.Representation, Is.EqualTo("Full"));
            Assert.That(merged.ContainsKey("world:cellCompanionSet"), Is.False, "Resolve が config に runtime 既定値を書き足した");

            // 宣言は Resolve の入力にならない: 引数に宣言の型が無いので、baked config に無い key を宣言から補う経路が無い。
            var parameterTypes = typeof(PlayerContentConfiguration).GetMethod(nameof(PlayerContentConfiguration.Resolve),
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
                .GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            Assert.That(parameterTypes, Is.EqualTo(new[] { typeof(PlayerBakedContentConfiguration), typeof(AppConfig), typeof(string), typeof(string) }));
            var members = typeof(PlayerContentConfiguration).GetProperties(
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Assert.That(members, Is.EqualTo(new[] { "FirstScene", "Identity", "Path", "Representation", "VerifiedRevision" }));
        }

        [Test]
        public void Resolve_WrongContentSetOverride_IsRejectedBeforeInstalledIo()
        {
            var baked = new PlayerBakedContentConfiguration(Config("content", "StandaloneWindows64-Player", "baked-set"));
            var merged = Config("content", "StandaloneWindows64-Player", "other-set");
            var error = Assert.Throws<ContentDirectoryException>(() =>
                PlayerContentConfiguration.Resolve(baked, merged, Path.GetTempPath(), "StandaloneWindows64-Player"));
            Assert.That(error!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidConfiguration));
        }

        [Test]
        public void RequiredJson_MissingOrInvalid_IsRejected()
        {
            var root = Path.Combine(Path.GetTempPath(), "osm-bs4-required", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var missing = new RequiredJsonFileConfigProvider(Path.Combine(root, "missing.json"));
                Assert.Throws<FileNotFoundException>(() => missing.Load(new Dictionary<string, string>()));
                var invalidPath = Path.Combine(root, "invalid.json");
                File.WriteAllText(invalidPath, "{");
                Assert.Throws<FormatException>(() => new RequiredJsonFileConfigProvider(invalidPath).Load(new Dictionary<string, string>()));
            }
            finally { Directory.Delete(root, true); }
        }

        [TestCase(true, ExpectedResult = true)]
        [TestCase(false, ExpectedResult = false)]
        public bool FailureNotification_IsLimitedToRequiredPlayerMode(bool required) =>
            AbstractApplicationInitializer.ShouldNotifyPlayerContentFailure(required);

        // 初回 Scene の後の行き先: Editor は種類を見ずに Editor、Player は baked の種類で smoke か playable。
        // internal の enum は public の test の引数・戻り値にできないので、種類は bool、行き先は名前で表す。
        [TestCase(false, false, ExpectedResult = "Editor")]
        [TestCase(false, true, ExpectedResult = "Editor")]
        [TestCase(true, false, ExpectedResult = "Smoke")]
        [TestCase(true, true, ExpectedResult = "Playable")]
        public string RouteFirstScene_SelectsByConfigurationAndKind(bool isPlayer, bool playable) =>
            AbstractApplicationInitializer.RouteFirstScene(isPlayer, playable ? PlayerContentKind.Playable : PlayerContentKind.Smoke).ToString();

        // 失敗時の初回 Scene の unload は Player の構成だけ（種類によらない）。Editor は ReleaseAll と Play の終了に任せる。
        [TestCase(true, ExpectedResult = true)]
        [TestCase(false, ExpectedResult = false)]
        public bool FirstSceneUnloadOnFailure_IsLimitedToRequiredPlayerMode(bool isPlayer) =>
            AbstractApplicationInitializer.UnloadsFirstSceneOnStartupFailure(isPlayer);

        [TestCase("smoke", ExpectedResult = "Smoke")]
        [TestCase("playable", ExpectedResult = "Playable")]
        public string PlayerKind_ValidRawValue_IsRead(string raw) =>
            PlayerBakedContentConfiguration.ParsePlayerKind(raw).ToString();

        [TestCase("")]
        [TestCase("Playable")]
        [TestCase("SMOKE")]
        [TestCase("x")]
        public void PlayerKind_EmptyOrInvalidRawValue_IsRejected(string raw)
        {
            var error = Assert.Throws<ContentDirectoryException>(() => PlayerBakedContentConfiguration.ParsePlayerKind(raw));
            Assert.That(error!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidConfiguration));
        }

        [Test]
        public void BakedConfiguration_MissingPlayerKind_IsRejected()
        {
            var error = Assert.Throws<ContentDirectoryException>(() =>
                new PlayerBakedContentConfiguration(Config("content", "StandaloneWindows64-Player", playerKind: null)));
            Assert.That(error!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidConfiguration));
        }

        [TestCase("smoke", true)]
        [TestCase("playable", false)]
        public void BakedConfiguration_KeepsRawAndReadPlayerKind(string raw, bool isSmoke)
        {
            var baked = new PlayerBakedContentConfiguration(Config("content", "StandaloneWindows64-Player", playerKind: raw));
            Assert.That(baked.PlayerKindRaw, Is.EqualTo(raw));
            Assert.That(baked.PlayerKind, Is.EqualTo(isSmoke ? PlayerContentKind.Smoke : PlayerContentKind.Playable));
        }

        // protected の比較は baked の生の値と重ねた後の値の ordinal 比較（大小違いの override も拒む）。
        [TestCase("smoke", null, true)]
        [TestCase("playable", null, true)]
        [TestCase("smoke", "playable", false)]
        [TestCase("playable", "smoke", false)]
        [TestCase("playable", "Playable", false)]
        public void Resolve_PlayerKindOverride_IsComparedWithTheRawBakedValue(string baked, string? overridden, bool accepted)
        {
            var root = Path.Combine(Path.GetTempPath(), "osm-s41-config", Guid.NewGuid().ToString("N"));
            var bakedConfig = new PlayerBakedContentConfiguration(Config("content", "StandaloneWindows64-Player", playerKind: baked));
            var merged = Overlay(baked,
                overridden == null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["content:playerKind"] = overridden });
            if (accepted)
            {
                Assert.DoesNotThrow(() => PlayerContentConfiguration.Resolve(bakedConfig, merged, root, "StandaloneWindows64-Player"));
                return;
            }
            var error = Assert.Throws<ContentDirectoryException>(() =>
                PlayerContentConfiguration.Resolve(bakedConfig, merged, root, "StandaloneWindows64-Player"));
            Assert.That(error!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidConfiguration));
            Assert.That(error.Message, Is.EqualTo("Protected Player content configuration was overridden."));
        }

        // 今の 9 つの protected key の override の拒みは、種類の key を足しても変わらない。
        [TestCase("content:schemaVersion", "2")]
        [TestCase("content:runtimeMode", "addressables")]
        [TestCase("content:buildIdentity", "other")]
        [TestCase("content:contentSet", "other-set")]
        [TestCase("content:target", "other-target")]
        [TestCase("content:representation", "Whitebox")]
        [TestCase("content:firstScene", "Home")]
        [TestCase("content:relativeDirectory", "other")]
        [TestCase("content:probeToken", "other-token")]
        public void Resolve_ExistingProtectedKeyOverride_IsStillRejected(string key, string value)
        {
            var bakedConfig = new PlayerBakedContentConfiguration(Config("content", "StandaloneWindows64-Player"));
            var merged = Overlay("smoke", new Dictionary<string, string> { [key] = value });
            var error = Assert.Throws<ContentDirectoryException>(() =>
                PlayerContentConfiguration.Resolve(bakedConfig, merged, Path.GetTempPath(), "StandaloneWindows64-Player"));
            Assert.That(error!.Message, Is.EqualTo("Protected Player content configuration was overridden."));
        }

        // 失敗の通知の latch は生の値が ordinal に playable の時だけ Playable（欠け・大小違い・smoke は動作確認の扱い）。
        [TestCase("playable", ExpectedResult = true)]
        [TestCase(null, ExpectedResult = false)]
        [TestCase("", ExpectedResult = false)]
        [TestCase("smoke", ExpectedResult = false)]
        [TestCase("Playable", ExpectedResult = false)]
        public bool Latch_IsPlayableOnlyForTheExactRawValue(string? raw) =>
            PlayerBakedContentConfiguration.LatchPlayerKind(raw) == PlayerContentKind.Playable;

        [Test]
        public async Task StartupFailureHooks_DerivedCleanupThrows_StillNotifiesOnceAndLogsBoth()
        {
            var order = new List<string>();
            var logged = new List<string>();
            await AbstractApplicationInitializer.RunStartupFailureHooksAsync(
                () => { order.Add("cleanup"); throw new InvalidOperationException("cleanup failed"); },
                () => { order.Add("notify"); throw new InvalidOperationException("notify failed"); },
                (hook, exception) => logged.Add(hook + ":" + exception.Message));
            Assert.That(order, Is.EqualTo(new[] { "cleanup", "notify" }));
            Assert.That(logged, Is.EqualTo(new[] { "derived cleanup:cleanup failed", "player content failure notification:notify failed" }));
        }

        [Test]
        public async Task StartupFailureHooks_NoFailure_CallsBothInOrderWithoutLogging()
        {
            var order = new List<string>();
            var logged = new List<string>();
            await AbstractApplicationInitializer.RunStartupFailureHooksAsync(
                () => order.Add("cleanup"),
                () => { order.Add("notify"); return UniTask.CompletedTask; },
                (hook, exception) => logged.Add(hook));
            Assert.That(order, Is.EqualTo(new[] { "cleanup", "notify" }));
            Assert.That(logged, Is.Empty);
        }

        [Test]
        public async Task StartupFailureHooks_NullNotification_OnlyRunsCleanup()
        {
            var order = new List<string>();
            var logged = new List<string>();
            await AbstractApplicationInitializer.RunStartupFailureHooksAsync(
                () => { order.Add("cleanup"); throw new InvalidOperationException("cleanup failed"); },
                null,
                (hook, exception) => logged.Add(hook));
            Assert.That(order, Is.EqualTo(new[] { "cleanup" }));
            Assert.That(logged, Is.EqualTo(new[] { "derived cleanup" }));
        }

        [Test]
        public void EditorFirstScene_IsReadFromConfig_AndEmptyWhenAbsent()
        {
            Assert.That(AbstractApplicationInitializer.ReadEditorFirstScene(Config("content", "target")), Is.EqualTo("Title"));
            var absent = new AppConfig(new IConfigProvider[]
            {
                new DictionaryProvider(new Dictionary<string, string> { ["content:runtimeMode"] = "directory" }),
            });
            Assert.That(AbstractApplicationInitializer.ReadEditorFirstScene(absent), Is.Empty);
            var other = new AppConfig(new IConfigProvider[]
            {
                new DictionaryProvider(new Dictionary<string, string> { ["content:firstScene"] = " OutGameScene " }),
            });
            Assert.That(AbstractApplicationInitializer.ReadEditorFirstScene(other), Is.EqualTo("OutGameScene"));
        }

        [Test]
        public void FixtureStages_OrderedSubsequence_AcceptsCleanupAfterFailure()
        {
            Assert.DoesNotThrow(() => StageHarness.Validate(0, 3, 4));
            Assert.Throws<InvalidOperationException>(() => StageHarness.Validate(1, 0));
            Assert.Throws<InvalidOperationException>(() => StageHarness.Validate(0, 0));
            Assert.Throws<InvalidOperationException>(() => StageHarness.Validate(0, 99));
        }

        private static AppConfig Config(string relative, string target, string contentSet = "set", string? playerKind = "smoke") =>
            new(new IConfigProvider[] { new DictionaryProvider(Values(relative, target, contentSet, playerKind)) });

        private static Dictionary<string, string> Values(string relative, string target, string contentSet, string? playerKind)
        {
            var values = new Dictionary<string, string>
            {
                ["content:schemaVersion"] = "1", ["content:relativeDirectory"] = relative,
                ["content:buildIdentity"] = "identity", ["content:target"] = target,
                ["content:runtimeMode"] = "directory", ["content:contentSet"] = contentSet,
                ["content:representation"] = "Full", ["content:firstScene"] = "Title",
            };
            if (playerKind != null) values["content:playerKind"] = playerKind;
            return values;
        }

        // baked の上に環境変数・command line の値を重ねた形（後の provider が勝つ）。
        private static AppConfig Overlay(string? playerKind, IReadOnlyDictionary<string, string> overrides) =>
            new(new IConfigProvider[]
            {
                new DictionaryProvider(Values("content", "StandaloneWindows64-Player", "set", playerKind)),
                new DictionaryProvider(overrides),
            });

        private sealed class DictionaryProvider : IConfigProvider
        {
            private readonly IReadOnlyDictionary<string, string> _values;
            internal DictionaryProvider(IReadOnlyDictionary<string, string> values) => _values = values;
            public void Load(Dictionary<string, string> store)
            { foreach (var value in _values) store[value.Key] = value.Value; }
        }

        private sealed class StageHarness : AbstractApplicationInitializer
        {
            internal static void Validate(params int[] values)
            {
                var stages = new List<PlayerContentStage>();
                foreach (var value in values) stages.Add(value switch
                {
                    0 => PlayerContentStage.FixtureLoaded,
                    1 => PlayerContentStage.FixtureInstantiated,
                    2 => PlayerContentStage.FixtureBehaviorVerified,
                    3 => PlayerContentStage.FixtureDestroyed,
                    4 => PlayerContentStage.FixtureHandleReleased,
                    _ => PlayerContentStage.UiCommonReady,
                });
                AppendFixtureStages(new List<PlayerContentStage>(), stages);
            }

            protected override ISceneFactory CreateSceneFactory() => null!;
            protected override string GetUICommonPrefabAddress() => string.Empty;
            protected override string GetSceneResourceMapAddress() => string.Empty;
            protected override ILoadingDisplay CreateLoadingDisplay() => null!;
        }
    }
}
