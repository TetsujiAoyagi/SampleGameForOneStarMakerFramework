#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
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

        [Test]
        public void FixtureStages_OrderedSubsequence_AcceptsCleanupAfterFailure()
        {
            Assert.DoesNotThrow(() => StageHarness.Validate(0, 3, 4));
            Assert.Throws<InvalidOperationException>(() => StageHarness.Validate(1, 0));
            Assert.Throws<InvalidOperationException>(() => StageHarness.Validate(0, 0));
            Assert.Throws<InvalidOperationException>(() => StageHarness.Validate(0, 99));
        }

        private static AppConfig Config(string relative, string target) => new(new IConfigProvider[]
        {
            new DictionaryProvider(new Dictionary<string, string>
            {
                ["content:schemaVersion"] = "1", ["content:relativeDirectory"] = relative,
                ["content:buildIdentity"] = "identity", ["content:target"] = target,
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
