#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.SceneSystem;
using Unity.Loading;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// directory mode の付随 asset の読み口を、偽の native directory と封じた Addressables backend で確かめる。
    /// AC21: 付随 Object entry を欠いた root では EntryMissing で失敗し、Addressables backend は呼ばれない（呼び出し回数 0）。
    /// AC9 の解決の規則（Whitebox の Scene から Full の entry へ fallback、2 所有者の片方の解放）も同じ構成で見る。
    /// 実 Content Directory を build する AC9 本体は SampleGame 側の統合テストが持つ。
    /// </summary>
    public sealed class AttachedAssetDirectoryTests
    {
        private const string Owner = "fixture-scene";
        private const string Child = "fixture-child";
        private const string ScenePath = "Assets/OneStarMaker/Scenes/UISystem/UIScene.unity";

        [Test]
        public void MissingAttachedEntry_FailsWithEntryMissing_WithoutTouchingAddressables()
        {
            var path = TempDirectory();
            var sceneId = LoadableSceneIdEditorUtility.CreateLoadableSceneId(ScenePath);
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            // Scene の entry だけ。付随 asset `fixture-scene:Lit` の Object entry を欠いた root。
            root.Initialize("build", "StandaloneWindows64-Player", new[] { new BuildContentEntry(Owner, "scene", "Whitebox", sceneId) });
            var native = new FakeNativeDirectory(root);
            var addressables = new CountingAddressablesBackend();
            var assets = new Runtime.AssetManagement.AssetManagement(addressables);
            ContentDirectorySession? session = null;
            try
            {
                session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64-Player", native);
                assets.InstallContentDirectory(session);
                assets.SealAddressables();
                var scene = Scene(Owner, assets, Lineage((Owner, "Lit")));

                var failure = Assert.ThrowsAsync<ContentDirectoryException>(async () => await scene.LoadAttached<Material>("Lit"));
                Assert.That(failure!.Code, Is.EqualTo(ContentDirectoryFailureCode.EntryMissing));
                Assert.That(failure.LogicalKey, Is.EqualTo("fixture-scene:Lit"));
                Assert.That(failure.Representation, Is.EqualTo("Whitebox"));
                Assert.That(addressables.LoadAssetCallCount, Is.Zero, "Addressables backend が呼ばれた");
                Assert.That(native.ObjectLoads, Is.Zero, "native は entry 解決の後にしか呼ばれない");

                session.CloseAsync().GetAwaiter().GetResult();
            }
            finally
            {
                Unregister(assets, session);
                UnityEngine.Object.DestroyImmediate(root);
                Directory.Delete(path);
            }
        }

        [Test]
        public void WhiteboxScene_ResolvesFullAttachedEntry_AndTwoOwnersReleaseIndependently()
        {
            var path = TempDirectory();
            var asset = AssetDatabase.LoadMainAssetAtPath("Assets/TutorialInfo/Icons/URP.png");
            Assert.That(asset, Is.Not.Null);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId), Is.True);
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize("build", "StandaloneWindows64-Player", new[]
            {
                new BuildContentEntry(Owner + ":Lit", "lit", "Full",
                    new Loadable<UnityEngine.Object>(LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset)), guid, localId),
            });
            var texture = new Texture2D(1, 1);
            var native = new FakeNativeDirectory(root) { ObjectResult = new FakeAsset(texture) };
            var addressables = new CountingAddressablesBackend();
            var assets = new Runtime.AssetManagement.AssetManagement(addressables);
            ContentDirectorySession? session = null;
            try
            {
                session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64-Player", native);
                assets.InstallContentDirectory(session);
                assets.SealAddressables();
                var owner = Scene(Owner, assets, Lineage((Owner, "Lit")));
                var child = Scene(Child, assets, Lineage((Child, null), (Owner, "Lit")));

                // Whitebox で登録した Scene から、Full しか無い付随 entry へ fallback して解決する。
                var ownerHandle = owner.LoadAttached<Texture2D>("Lit").GetAwaiter().GetResult();
                var childHandle = child.LoadAttached<Texture2D>(Owner, "Lit").GetAwaiter().GetResult();
                Assert.That(ownerHandle.Value, Is.SameAs(texture));
                Assert.That(childHandle.Value, Is.SameAs(texture));
                Assert.That(native.ObjectLoads, Is.EqualTo(1), "同じ entry は 1 回だけ native でロードし、所有者ごとに参照を数える");
                var litKey = session.GetObjectKey(Owner + ":Lit", "Whitebox");
                Assert.That(assets.GetOwners(litKey), Is.EquivalentTo(new[] { AssetOwner.Scene(Owner), AssetOwner.Scene(Child) }));

                // 片方の所有者を解放しても、もう片方は有効。
                assets.ReleaseScene(Owner);
                // IAssetHandle.IsValid は「参照先がまだ有効か」で、所有者ごとの有効性ではない。共有 entry は他の所有者が
                // 持つ限り生きているので、解放した側の handle も IsValid のまま。解放は所有者の一覧で確かめる。
                Assert.That(assets.GetOwners(litKey), Is.EqualTo(new[] { AssetOwner.Scene(Child) }));
                Assert.That(childHandle.IsValid, Is.True);
                Assert.That(childHandle.Value, Is.SameAs(texture));
                Assert.That(native.ReleaseCount, Is.Zero);
                assets.ReleaseScene(Child);
                Assert.That(native.ReleaseCount, Is.EqualTo(1));
                Assert.That(addressables.LoadAssetCallCount, Is.Zero);

                session.CloseAsync().GetAwaiter().GetResult();
            }
            finally
            {
                Unregister(assets, session);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(root);
                Directory.Delete(path);
            }
        }

        // 失敗した assert の後も directory の登録を残さない。登録は process に 1 つだけで、残すと後続の
        // directory テストが RevisionBusy /「already registered」で連鎖失敗する。正常経路で close 済みなら no-op。
        private static void Unregister(Runtime.AssetManagement.AssetManagement assets, ContentDirectorySession? session)
        {
            if (session == null) return;
            assets.ReleaseAll();
            session.CompletePlayStop();
        }

        private static string TempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-bs5a-attached-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static IReadOnlyList<KeyValuePair<string, IReadOnlyList<AttachedAssetDescription>>> Lineage(params (string Identity, string? Attached)[] nodes)
        {
            var lineage = new List<KeyValuePair<string, IReadOnlyList<AttachedAssetDescription>>>();
            foreach (var (identity, attached) in nodes)
            {
                IReadOnlyList<AttachedAssetDescription> declarations = attached == null
                    ? Array.Empty<AttachedAssetDescription>()
                    : new[] { new AttachedAssetDescription(attached, new List<AssetPayload> { new(string.Empty, new AssetReference("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")) }) };
                lineage.Add(new KeyValuePair<string, IReadOnlyList<AttachedAssetDescription>>(identity, declarations));
            }
            return lineage;
        }

        private static AttachedScene Scene(string identity, IAssetManagement assets,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<AttachedAssetDescription>>> lineage)
        {
            var resource = ScriptableObject.CreateInstance<SceneResource>();
            resource.Identity = identity;
            var scene = new AttachedScene(resource);
            scene.BindAssets(assets, new SceneContentBinding(true, "Whitebox", lineage));
            return scene;
        }

        private sealed class AttachedScene : SceneBase
        {
            public AttachedScene(SceneResource resource) : base(resource, new NullSceneQuery(), null!) { }
            public UniTask<IAssetHandle<T>> LoadAttached<T>(string name) where T : UnityEngine.Object => LoadAttachedAssetAsync<T>(name, CancellationToken.None);
            public UniTask<IAssetHandle<T>> LoadAttached<T>(string owner, string name) where T : UnityEngine.Object => LoadAttachedAssetAsync<T>(owner, name, CancellationToken.None);
        }

        private sealed class NullSceneQuery : ISceneQuery
        {
            public SceneBase? GetLoadedScene(string identity) => null;
            public bool IsSceneLoaded(string identity) => false;
            public bool IsSceneStable(string identity) => false;
        }

        private sealed class FakeNativeDirectory : IContentNativeDirectory
        {
            private readonly BuildContentRoot _root;
            internal FakeNativeDirectory(BuildContentRoot root) => _root = root;
            internal int ObjectLoads;
            internal int ReleaseCount;
            internal IBackendAsset? ObjectResult;
            public bool Register(string path) => true;
            public BuildContentRoot[] GetRoots() => new[] { _root };
            public void Unregister() { }
            public UniTask<IBackendScene> LoadSceneAsync(BuildContentEntry entry, SceneLoadOptions options) => throw new NotSupportedException();
            public UniTask UnloadSceneAsync(IBackendScene scene) => UniTask.CompletedTask;
            public UniTask<IBackendAsset> LoadObjectAsync<T>(BuildContentEntry entry) where T : UnityEngine.Object
            {
                ObjectLoads++;
                return ObjectResult != null ? UniTask.FromResult(ObjectResult) : throw new NotSupportedException();
            }
            public UniTask<IBackendInstance> InstantiateAsync(BuildContentEntry entry, Transform? parent, bool worldSpace) => throw new NotSupportedException();
            public void Release(IBackendAsset asset) => ReleaseCount++;
        }

        private sealed class FakeAsset : IBackendAsset, IContentDirectoryToken
        {
            internal FakeAsset(Texture2D asset) => Asset = asset;
            public UnityEngine.Object? Asset { get; }
            public bool IsValid => Asset != null;
        }

        // Addressables の代わり。呼ばれた回数だけを数える（封じた後は 0 のまま）。
        private sealed class CountingAddressablesBackend : IAssetBackend
        {
            internal int LoadAssetCallCount;
            public UniTask<IBackendAsset> LoadAssetAsync<T>(string address, CancellationToken ct) where T : UnityEngine.Object
            { LoadAssetCallCount++; throw new NotSupportedException(); }
            public IBackendAsset LoadAssetSync<T>(string address) where T : UnityEngine.Object
            { LoadAssetCallCount++; throw new NotSupportedException(); }
            public UniTask<IBackendScene> LoadSceneAsync(string address, SceneLoadOptions options, CancellationToken ct) => throw new NotSupportedException();
            public UniTask UnloadSceneAsync(IBackendScene scene, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask<IBackendInstance> InstantiateAsync(string address, Transform? parent, bool worldSpace, CancellationToken ct) => throw new NotSupportedException();
            public void Release(IBackendAsset asset) { }
        }
    }
}
