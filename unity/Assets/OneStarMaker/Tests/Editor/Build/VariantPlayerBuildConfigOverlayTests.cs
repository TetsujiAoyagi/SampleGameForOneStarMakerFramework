#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using OneStarMaker.Editor.Build;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    [TestFixture]
    public sealed class VariantPlayerBuildConfigOverlayTests
    {
        private readonly List<string> _tempDirectories = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var path in _tempDirectories)
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            _tempDirectories.Clear();
        }

        [Test]
        public void Upsert_ReplacesExistingEmptyVariantAndEscapesFirstScene()
        {
            var json = "{\n  \"assets:sceneVariant\": \"\"\n}";
            json = VariantPlayerBuild.UpsertTopLevelString(json, VariantPlayerBuild.SceneVariantConfigKey, "Whitebox");
            json = VariantPlayerBuild.UpsertTopLevelString(json, VariantPlayerBuild.FirstSceneIdentifyConfigKey, "A\\\"B");

            Assert.That(json, Does.Contain("\"assets:sceneVariant\": \"Whitebox\""));
            Assert.That(json, Does.Contain("A\\\\\\\"B"));
        }

        [Test]
        public void BuildWithOverlay_Success_EmbedsBothValuesAndRestoresExactBytes()
        {
            var original = new byte[] { 0xef, 0xbb, 0xbf }.Concat(Encoding.UTF8.GetBytes("{\r\n  \"assets:sceneVariant\" : \"\"\r\n}"));
            RunRestoreCase(original, throws: false);
        }

        [Test]
        public void BuildWithOverlay_Exception_RestoresExactBytes()
        {
            RunRestoreCase(Encoding.UTF8.GetBytes("{\n}\n"), throws: true);
        }

        private void RunRestoreCase(byte[] original, bool throws)
        {
            var directory = Path.Combine(Path.GetTempPath(), "OneStarMakerVariantOverlay", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            _tempDirectories.Add(directory);
            var path = Path.Combine(directory, "app-config.json");
            File.WriteAllBytes(path, original);
            var profile = CreateProfile();
            var backend = new InspectingBackend(path, throws);
            try
            {
                if (throws)
                    Assert.Throws<InvalidOperationException>(() => VariantPlayerBuild.BuildWithOverlay(profile, path, backend));
                else
                    VariantPlayerBuild.BuildWithOverlay(profile, path, backend);

                Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
                Assert.That(backend.ObservedJson, Does.Contain("\"assets:sceneVariant\" : \"Whitebox\""));
                Assert.That(backend.ObservedJson, Does.Contain("\"assetCheckout:firstSceneIdentify\" : \"Planner\""));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static BuildVariantProfile CreateProfile()
        {
            var profile = ScriptableObject.CreateInstance<BuildVariantProfile>();
            Set(profile, "_sceneVariant", "Whitebox");
            Set(profile, "_firstSceneIdentify", "Planner");
            Set(profile, "_variantWhitelist", new List<string> { string.Empty, "Whitebox" });
            return profile;
        }

        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

        private sealed class InspectingBackend : IVariantPlayerBuildBackend
        {
            private readonly string _path;
            private readonly bool _throws;
            internal InspectingBackend(string path, bool throws) { _path = path; _throws = throws; }
            internal string ObservedJson { get; private set; } = string.Empty;
            public void Build()
            {
                ObservedJson = File.ReadAllText(_path);
                if (_throws) throw new InvalidOperationException("injected");
            }
        }
    }

    internal static class ByteArrayTestExtensions
    {
        internal static byte[] Concat(this byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, result, 0, first.Length);
            Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
            return result;
        }
    }
}
