#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.AssetManagement;
using OneStarMaker.Tests.SceneSystem.Helpers;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace OneStarMaker.Tests.SceneSystem
{
    [TestFixture]
    public sealed class SceneVariantForwardingTests : SceneDirectorTestBase
    {
        [Test]
        public void Constructor_NullVariant_Throws()
        {
            var resource = CreateResource("VariantNull", "a", "b");
            var map = SceneTestHelper.CreateSceneResourceMap(resource);
            CreatedSOs.Add(map);

            Assert.Throws<ArgumentNullException>(
                () => new ExposedDirector(Factory, UICommon, map, AssetManagement, null!));
        }

        [TestCase("", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        [TestCase("Whitebox", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
        [TestCase("Missing", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        public async Task Load_ForwardsExactVariantAndPreservesDefaultFallback(string variant, string expectedAddress)
        {
            var backend = new FakeAssetBackend();
            var assets = new Runtime.AssetManagement.AssetManagement(backend);
            var resource = CreateResource(
                "Variant_" + (string.IsNullOrEmpty(variant) ? "Default" : variant),
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
            var map = SceneTestHelper.CreateSceneResourceMap(resource);
            CreatedSOs.Add(map);
            var director = new ExposedDirector(Factory, UICommon, map, assets, variant);

            await director.Load(resource);

            Assert.That(backend.SceneLoadAddresses, Is.EqualTo(new[] { expectedAddress }));
            director.Dispose();
        }

        [Test]
        public async Task Load_LogicalNodeWithoutPayload_DoesNotCallBackend()
        {
            var backend = new FakeAssetBackend();
            var assets = new Runtime.AssetManagement.AssetManagement(backend);
            var resource = SceneTestHelper.CreateSceneResource("LogicalNode");
            var description = new SceneAssetDescription("LogicalNode", LoadType.OnDemand, new List<AssetPayload>());
            SetDescription(resource, description);
            CreatedSOs.Add(resource);
            var map = SceneTestHelper.CreateSceneResourceMap(resource);
            CreatedSOs.Add(map);
            var director = new ExposedDirector(Factory, UICommon, map, assets, "Whitebox");

            await director.Load(resource);

            Assert.That(backend.LoadSceneCallCount, Is.Zero);
            director.Dispose();
        }

        private SceneResource CreateResource(string identity, string defaultGuid, string whiteboxGuid)
        {
            var resource = SceneTestHelper.CreateSceneResource(identity);
            var payloads = new List<AssetPayload>
            {
                new(string.Empty, new AssetReference(defaultGuid)),
                new("Whitebox", new AssetReference(whiteboxGuid)),
            };
            SetDescription(resource, new SceneAssetDescription(identity, LoadType.OnDemand, payloads));
            CreatedSOs.Add(resource);
            return resource;
        }

        private static void SetDescription(SceneResource resource, SceneAssetDescription description)
        {
            var field = typeof(SceneResource).GetField("_sceneAssetDescription", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field!.SetValue(resource, description);
        }

        private sealed class ExposedDirector : SceneDirector
        {
            internal ExposedDirector(ISceneFactory factory, Runtime.UISystem.UICommon ui, SceneResourceMap map, IAssetManagement assets, string variant)
                : base(factory, ui, map, new NoLoadingDisplay(), assets, variant) { }

            internal UniTask<(bool AddressablesLoaded, GameObject[] RootObjects)> Load(SceneResource resource)
                => PerformUnitySceneLoad(resource.Identity, resource, 100);
        }

        private sealed class NoLoadingDisplay : ILoadingDisplay
        {
            public UniTask Show(LoadingDisplayType type, System.Threading.CancellationToken ct) => UniTask.CompletedTask;
            public UniTask Hide(System.Threading.CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
