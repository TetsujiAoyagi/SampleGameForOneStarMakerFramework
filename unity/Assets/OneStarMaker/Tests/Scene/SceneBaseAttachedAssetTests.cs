#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.AssetManagement;
using OneStarMaker.Tests.SceneSystem.Helpers;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace OneStarMaker.Tests.SceneSystem
{
    /// <summary>
    /// <c>SceneBase.LoadAttachedAssetAsync</c> の呼び出しの形（AC10）と、祖先が宣言した共有 asset を複数の Scene が
    /// それぞれ自分の所有で読む形（AC17）。偽の IAssetManagement で呼び出しを記録し、所有者の共有の解放だけは
    /// 偽 backend 入りの実 AssetManagement で見る。
    /// </summary>
    [TestFixture]
    public sealed class SceneBaseAttachedAssetTests
    {
        private const string LitGuid = "c03517b35199eb046852798ce73fa2d7";
        private const string BurstGuid = "447192e2365044e4caafad332d204daf";

        private readonly List<ScriptableObject> _created = new();
        private SceneResource _session = null!;
        private SceneResource _season = null!;
        private SceneResource _cell = null!;
        private SceneResource _events = null!;

        [SetUp]
        public void SetUp()
        {
            // InGameSession(Lit) ─ Season_Spring ─ Spring_Cell_4_2 ─ Spring_Events_4_2(Burst)
            _session = Resource("InGameSession");
            _season = Resource("Season_Spring", _session);
            _cell = Resource("Spring_Cell_4_2", _season);
            _events = Resource("Spring_Events_4_2", _cell);
            Attach(_session, "Lit", LitGuid);
            Attach(_events, "Burst", BurstGuid);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _created) if (so != null) Object.DestroyImmediate(so);
            _created.Clear();
        }

        [Test]
        public void DirectoryMode_LoadsOwnAttachedAsset_ByLogicalKeyVariantAndOwnScope()
        {
            var assets = new RecordingAssetManagement();
            var scene = Bind(_events, assets, useContentDirectory: true, variant: "Whitebox");

            scene.LoadAttached<Object>("Burst").GetAwaiter().GetResult();

            var call = assets.ContentCalls.Single();
            Assert.That(call.LogicalKey, Is.EqualTo("Spring_Events_4_2:Burst"));
            Assert.That(call.Representation, Is.EqualTo("Whitebox"));
            Assert.That(call.Owner, Is.EqualTo(AssetOwner.Scene("Spring_Events_4_2")));
            Assert.That(assets.AddressCalls, Is.Empty);
        }

        [Test]
        public void AddressablesMode_LoadsOwnAttachedAsset_ByDescriptionKeyAndOwnScope()
        {
            var assets = new RecordingAssetManagement();
            var scene = Bind(_events, assets, useContentDirectory: false, variant: "Whitebox");

            scene.LoadAttached<Object>("Burst").GetAwaiter().GetResult();

            var call = assets.AddressCalls.Single();
            var expected = AssetKey.FromDescription(_events.FindAttachedAsset("Burst")!, "Whitebox");
            Assert.That(call.Key, Is.EqualTo(expected));
            Assert.That(call.Owner, Is.EqualTo(AssetOwner.Scene("Spring_Events_4_2")));
            Assert.That(assets.ContentCalls, Is.Empty);
        }

        [Test]
        public void AncestorOwner_IsResolvedFromLineage_AndOwnedByTheReadingScene()
        {
            var assets = new RecordingAssetManagement();
            var season = Bind(_season, assets, useContentDirectory: true, variant: "Full");

            season.LoadAttached<Material>("InGameSession", "Lit").GetAwaiter().GetResult();

            var call = assets.ContentCalls.Single();
            Assert.That(call.LogicalKey, Is.EqualTo("InGameSession:Lit"));
            Assert.That(call.Owner, Is.EqualTo(AssetOwner.Scene("Season_Spring")), "所有者は宣言した node ではなく読む Scene");
        }

        [Test]
        public void NonAncestorOwner_UndeclaredName_AndBlankArguments_AreArgumentExceptions()
        {
            var assets = new RecordingAssetManagement();
            var season = Bind(_season, assets, useContentDirectory: true, variant: "Full");

            // 子孫（Spring_Events_4_2）は祖先ではない。
            Assert.Throws<ArgumentException>(() => season.LoadAttached<Object>("Spring_Events_4_2", "Burst"));
            // 自分にも祖先にも無い名前。
            Assert.Throws<ArgumentException>(() => season.LoadAttached<Object>("Lit"));
            Assert.Throws<ArgumentException>(() => season.LoadAttached<Object>("InGameSession", "Missing"));
            Assert.Throws<ArgumentException>(() => season.LoadAttached<Object>("", "Lit"));
            Assert.Throws<ArgumentException>(() => season.LoadAttached<Object>("InGameSession", " "));
            Assert.That(assets.ContentCalls, Is.Empty);
            Assert.That(assets.AddressCalls, Is.Empty);
        }

        [Test]
        public void LegacyBindAssets_UsesAddressablesWithoutVariant()
        {
            var assets = new RecordingAssetManagement();
            var scene = new AttachedScene(_events);
            scene.BindAssets(assets);

            scene.LoadAttached<Object>("Burst").GetAwaiter().GetResult();

            Assert.That(assets.AddressCalls.Single().Key, Is.EqualTo(AssetKey.FromDescription(_events.FindAttachedAsset("Burst")!, string.Empty)));
        }

        // ═══ AC17: 4 つの Season が同じ Lit を各 Scene(Season) 所有で読む ═══

        [Test]
        public void FourSeasons_ShareOneKeyWithFourOwners_InBothModes()
        {
            var seasons = new[] { "Season_Spring", "Season_Summer", "Season_Autumn", "Season_Winter" }
                .Select(name => name == "Season_Spring" ? _season : Resource(name, _session)).ToArray();

            var directory = new RecordingAssetManagement();
            foreach (var season in seasons)
                Bind(season, directory, useContentDirectory: true, variant: "Full").LoadAttached<Material>("InGameSession", "Lit").GetAwaiter().GetResult();
            Assert.That(directory.ContentCalls.Select(call => call.LogicalKey).Distinct(), Is.EqualTo(new[] { "InGameSession:Lit" }));
            Assert.That(directory.ContentCalls.Select(call => call.Owner), Is.EquivalentTo(seasons.Select(season => AssetOwner.Scene(season.Identity))));

            var addressables = new RecordingAssetManagement();
            foreach (var season in seasons)
                Bind(season, addressables, useContentDirectory: false, variant: "Full").LoadAttached<Material>("InGameSession", "Lit").GetAwaiter().GetResult();
            Assert.That(addressables.AddressCalls.Select(call => call.Key.Canonical).Distinct().Count(), Is.EqualTo(1));
            Assert.That(addressables.AddressCalls.Select(call => call.Owner), Is.EquivalentTo(seasons.Select(season => AssetOwner.Scene(season.Identity))));
        }

        [Test]
        public void SharedAsset_ReleasingOneSeason_KeepsTheOtherHandleValid()
        {
            var backend = new FakeAssetBackend();
            var assets = new Runtime.AssetManagement.AssetManagement(backend);
            var summer = Resource("Season_Summer", _session);
            var springHandle = Bind(_season, assets, useContentDirectory: false, variant: "Full").LoadAttached<Object>("InGameSession", "Lit").GetAwaiter().GetResult();
            var summerHandle = Bind(summer, assets, useContentDirectory: false, variant: "Full").LoadAttached<Object>("InGameSession", "Lit").GetAwaiter().GetResult();

            Assert.That(backend.LoadAssetCallCount, Is.EqualTo(1), "同じ canonical key は backend で共有される");
            assets.ReleaseScene("Season_Spring");
            Assert.That(springHandle.IsValid, Is.False);
            Assert.That(summerHandle.IsValid, Is.True);
            Assert.That(backend.ReleaseCallCount, Is.Zero);
            assets.ReleaseScene("Season_Summer");
            Assert.That(backend.ReleaseCallCount, Is.EqualTo(1));
        }

        private AttachedScene Bind(SceneResource resource, IAssetManagement assets, bool useContentDirectory, string variant)
        {
            var scene = new AttachedScene(resource);
            scene.BindAssets(assets, SceneContentBinding.FromResource(resource, useContentDirectory, variant));
            return scene;
        }

        private SceneResource Resource(string identity, SceneResource? parent = null)
        {
            var resource = SceneTestHelper.CreateSceneResource(identity);
            _created.Add(resource);
            if (parent != null) SceneTestHelper.AddChild(parent, resource);
            return resource;
        }

        private static void Attach(SceneResource resource, string name, string guid)
        {
            var list = (List<AttachedAssetDescription>)typeof(SceneResource)
                .GetField("_attachedAssets", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(resource);
            list.Add(new AttachedAssetDescription(name, new List<AssetPayload> { new(string.Empty, new AssetReference(guid)) }));
        }

        private sealed class AttachedScene : SceneBase
        {
            public AttachedScene(SceneResource resource) : base(resource, new NullSceneQuery(), null!) { }
            public UniTask<IAssetHandle<T>> LoadAttached<T>(string name) where T : Object => LoadAttachedAssetAsync<T>(name, CancellationToken.None);
            public UniTask<IAssetHandle<T>> LoadAttached<T>(string owner, string name) where T : Object => LoadAttachedAssetAsync<T>(owner, name, CancellationToken.None);
        }

        private sealed class NullSceneQuery : ISceneQuery
        {
            public SceneBase? GetLoadedScene(string identity) => null;
            public bool IsSceneLoaded(string identity) => false;
            public bool IsSceneStable(string identity) => false;
        }

        private sealed class RecordingAssetManagement : IAssetManagement
        {
            internal readonly List<(AssetKey Key, AssetOwner Owner)> AddressCalls = new();
            internal readonly List<(string LogicalKey, string Representation, AssetOwner Owner)> ContentCalls = new();

            public UniTask<IAssetHandle<T>> LoadAssetAsync<T>(AssetKey key, AssetOwner owner, CancellationToken ct = default) where T : Object
            { AddressCalls.Add((key, owner)); return UniTask.FromResult<IAssetHandle<T>>(new Handle<T>(key.Canonical)); }
            public UniTask<IAssetHandle<T>> LoadContentAssetAsync<T>(string logicalKey, string representation, AssetOwner owner, CancellationToken ct = default) where T : Object
            { ContentCalls.Add((logicalKey, representation, owner)); return UniTask.FromResult<IAssetHandle<T>>(new Handle<T>(logicalKey)); }
            public UniTask<ISceneHandle> LoadContentSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options = default, CancellationToken ct = default) => throw new NotSupportedException();
            public UniTask<GameObject> InstantiateContentAsync(string logicalKey, string representation, Transform? parent = null, bool worldSpace = false, CancellationToken ct = default) => throw new NotSupportedException();
            public IAssetHandle<T> LoadAppAssetSync<T>(AssetKey key) where T : Object => throw new NotSupportedException();
            public UniTask<ISceneHandle> LoadSceneAsync(string sceneIdentity, SceneAssetDescription desc, string variant = "", SceneLoadOptions options = default, CancellationToken ct = default) => throw new NotSupportedException();
            public UniTask UnloadSceneAsync(string sceneIdentity, CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask<GameObject> InstantiateAsync(AssetKey key, Transform? parent = null, bool worldSpace = false, CancellationToken ct = default) => throw new NotSupportedException();
            public void Release(IAssetHandle handle) { }
            public void ReleaseScene(string sceneIdentity) { }
            public void ReleaseAll() { }

            private sealed class Handle<T> : IAssetHandle<T> where T : Object
            {
                public Handle(string key) => Key = key;
                public string Key { get; }
                public bool IsValid => true;
                public T? Value => null;
            }
        }
    }
}
