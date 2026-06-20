#nullable enable

using NUnit.Framework;
using OneStarMaker.Editor.Build;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    public sealed class AssetDescriptionCollectorTests
    {
        [Test]
        public void Collect_IncludesSceneResourceMapDescriptions()
        {
            var profile = ScriptableObject.CreateInstance<BuildVariantProfile>();
            var map = ScriptableObject.CreateInstance<SceneResourceMap>();
            var resource = ScriptableObject.CreateInstance<SceneResource>();
            resource.Identity = "TestScene";

            var description = new SceneAssetDescription();
            var payloadsField = typeof(SceneAssetDescription).GetField(
                "_payloads",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            payloadsField!.SetValue(description, new System.Collections.Generic.List<AssetPayload>
            {
                new AssetPayload
                {
                    Reference = new UnityEngine.AddressableAssets.AssetReference("cccccccccccccccccccccccccccccccc"),
                    Variant = string.Empty,
                },
            });

            var descField = typeof(SceneResource).GetField(
                "_sceneAssetDescription",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            descField!.SetValue(resource, description);

            var listField = typeof(SceneResourceMap).GetField(
                "_sceneResources",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            listField!.SetValue(map, new System.Collections.Generic.List<SceneResource> { resource });

            var mapField = typeof(BuildVariantProfile).GetField(
                "_sceneResourceMap",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mapField!.SetValue(profile, map);

            var collected = AssetDescriptionCollector.Collect(profile);
            Assert.That(collected.Count, Is.EqualTo(1));
            Assert.That(collected[0].SourceName, Does.Contain("SceneResourceMap:TestScene"));
        }
    }
}
