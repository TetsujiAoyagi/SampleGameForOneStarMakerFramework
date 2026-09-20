#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
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

        [Test]
        public async Task DirectoryMode_RoutesSelectedRepresentationWithoutAddressablesFallback()
        {
            var backend = new FakeAssetBackend();
            var assets = new Runtime.AssetManagement.AssetManagement(backend);
            var directory = new DirectoryScenePort();
            assets.InstallContentDirectory(directory);
            var resource = CreateResource("DirectoryScene", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
            var map = SceneTestHelper.CreateSceneResourceMap(resource);
            CreatedSOs.Add(map);
            var director = new ExposedDirector(Factory, UICommon, map, assets, "Whitebox", true);

            await director.Load(resource);

            Assert.That(directory.SceneIdentity, Is.EqualTo("DirectoryScene"));
            Assert.That(directory.Representation, Is.EqualTo("Whitebox"));
            Assert.That(backend.LoadSceneCallCount, Is.Zero);
            director.Dispose();
            await assets.CloseContentDirectoryAsync();
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
            internal ExposedDirector(ISceneFactory factory, Runtime.UISystem.UICommon ui, SceneResourceMap map, IAssetManagement assets, string variant, bool useContentDirectory = false)
                : base(factory, ui, map, new NoLoadingDisplay(), assets, variant, useContentDirectory) { }

            internal UniTask<(bool BackendSceneLoaded, GameObject[] RootObjects)> Load(SceneResource resource)
                => PerformUnitySceneLoad(resource.Identity, resource, 100);
        }

        private sealed class DirectoryScenePort : IContentDirectoryBackend
        {
            public string BuildIdentity => "build";
            public string Target => "StandaloneWindows64";
            public string CachePrefix => "directory:test:";
            public string? SceneIdentity { get; private set; }
            public string? Representation { get; private set; }
            public AssetKey GetObjectKey(string logicalKey, string representation) => throw new NotSupportedException();
            public UniTask<IBackendAsset> LoadAssetAsync<T>(string logicalKey, string representation, System.Threading.CancellationToken ct) where T : UnityEngine.Object => throw new NotSupportedException();
            public UniTask<IBackendScene> LoadSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options, System.Threading.CancellationToken ct)
            {
                SceneIdentity = sceneIdentity;
                Representation = representation;
                return UniTask.FromResult<IBackendScene>(new DirectoryScene());
            }
            public UniTask<IBackendInstance> InstantiateAsync(string logicalKey, string representation, Transform? parent, bool worldSpace, System.Threading.CancellationToken ct) => throw new NotSupportedException();
            public UniTask UnloadSceneAsync(IBackendScene scene) => UniTask.CompletedTask;
            public void ReleaseSceneAfterUnityShutdown(IBackendScene scene) { }
            public void ReleaseSceneTokenAfterPlayStop(IBackendScene scene) { }
            public void Release(IBackendAsset asset) { }
            public void ConfigureCacheEviction(Action evictRevisionEntries) { }
            public UniTask StopAndDrainAsync() => UniTask.CompletedTask;
            public void EnsureAccepting() { }
            public UniTask CloseAsync() => UniTask.CompletedTask;
            public void CompletePlayStop() { }
            public void BeginSynchronousShutdown() { }
        }

        private sealed class DirectoryScene : IBackendScene, IContentDirectoryToken
        {
            public bool IsLoaded => true;
            public string Name => "DirectoryScene";
            public GameObject[] GetRootGameObjects() => Array.Empty<GameObject>();
        }

        private sealed class NoLoadingDisplay : ILoadingDisplay
        {
            public UniTask Show(LoadingDisplayType type, System.Threading.CancellationToken ct) => UniTask.CompletedTask;
            public UniTask Hide(System.Threading.CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
