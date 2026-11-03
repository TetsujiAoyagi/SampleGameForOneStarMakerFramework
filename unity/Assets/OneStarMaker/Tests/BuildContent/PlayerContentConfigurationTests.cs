#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // Editor directory Play は初回 Scene をロードしたまま続け、Player の smoke と失敗時の unload は Player だけが行う。
        [TestCase(true, ExpectedResult = true)]
        [TestCase(false, ExpectedResult = false)]
        public bool PlayerContentSmoke_IsLimitedToRequiredPlayerMode(bool required) =>
            AbstractApplicationInitializer.RunsPlayerContentSmoke(required);

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

        private static AppConfig Config(string relative, string target, string contentSet = "set") => new(new IConfigProvider[]
        {
            new DictionaryProvider(new Dictionary<string, string>
            {
                ["content:schemaVersion"] = "1", ["content:relativeDirectory"] = relative,
                ["content:buildIdentity"] = "identity", ["content:target"] = target,
                ["content:runtimeMode"] = "directory", ["content:contentSet"] = contentSet,
                ["content:representation"] = "Full", ["content:firstScene"] = "Title",
            }),
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
