#nullable enable

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Editor.Build;
using OneStarMaker.Runtime.AssetManagement;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    [TestFixture]
    public sealed class BuildVariantProfileSceneVariantTests
    {
        private string _originalGuid = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _originalGuid = DeveloperVariantSettings.instance.ActiveProfileGuid;
        }

        [TearDown]
        public void TearDown()
        {
            DeveloperVariantSettings.instance.SetActiveProfileGuid(_originalGuid);
            SceneVariantRuntimeBridge.EditorSceneVariantResolver = null;
        }

        [Test]
        public void Production_UsesDefaultVariantAndExcludesWhitebox()
        {
            var profile = Load("Assets/OneStarMaker/Editor/BuildProfiles/Production.asset");
            Assert.That(profile.SceneVariant, Is.Empty);
            Assert.That(profile.VariantWhitelist, Does.Not.Contain("Whitebox"));
        }

        [Test]
        public void WorldWhitebox_UsesDefaultAndWhiteboxPayloads()
        {
            var profile = Load("Assets/OneStarMaker/Editor/BuildProfiles/WorldWhitebox.asset");
            Assert.That(profile.SceneVariant, Is.EqualTo("Whitebox"));
            Assert.That(profile.VariantWhitelist, Is.EqualTo(new[] { string.Empty, "Whitebox" }));
        }

        [Test]
        public void Validator_RejectsNonEmptyVariantOutsideWhitelist()
        {
            var profile = ScriptableObject.CreateInstance<BuildVariantProfile>();
            SetField(profile, "_sceneVariant", "Whitebox");
            SetField(profile, "_variantWhitelist", new List<string> { string.Empty });
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => profile.ThrowIfSceneVariantInvalid());
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Injector_DistinguishesNoProfileAndExplicitEmptyProfile()
        {
            DeveloperVariantSettings.instance.SetActiveProfileGuid(string.Empty);
            SceneVariantEditorInjector.Install();
            Assert.That(SceneVariantRuntimeBridge.EditorSceneVariantResolver!(), Is.Null);

            var path = "Assets/OneStarMaker/Editor/BuildProfiles/Production.asset";
            DeveloperVariantSettings.instance.SetActiveProfileGuid(AssetDatabase.AssetPathToGUID(path));
            Assert.That(SceneVariantRuntimeBridge.EditorSceneVariantResolver!(), Is.EqualTo(string.Empty));
        }

        private static BuildVariantProfile Load(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildVariantProfile>(path);
            Assert.That(profile, Is.Not.Null, path);
            return profile!;
        }

        private static void SetField(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);
    }
}
