#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// AC9: 合成 fixture directory（TestResults/bs3-fixture）に Scene 1 件（Whitebox）と付随 Object 1 件（Full）を実際に build し、
    /// source fixture を消してから、Whitebox で動く Scene から付随 asset が Full fallback で解決・ロード・解放できることを、
    /// 封じた Addressables backend の下で確かめる。同じ entry を 2 つの所有者で取得し、片方を解放してももう片方は有効。
    /// 宣言した名前で entry の無いものは EntryMissing（AC21 の実 directory 版）。
    /// </summary>
    public sealed class AttachedAssetDirectoryIntegrationTests
    {
        private const string FixtureParent = "Assets/OneStarMakerGenerated/BS5aTests";
        private const string SceneKey = "fixture-scene";
        private const string ChildKey = "fixture-child";
        private string _folder = "";
        private string _copy = "";

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            if (_folder.Length != 0) AssetDatabase.DeleteAsset(_folder);
            if (_copy.Length != 0 && Directory.Exists(_copy)) Directory.Delete(_copy, true);
            DeleteIfEmpty(FixtureParent);
            DeleteIfEmpty("Assets/OneStarMakerGenerated");
        }

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator AttachedObject_IsResolvedWithFullFallback_FromTheBuiltDirectoryOnly()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                Assert.Ignore("BS5a fixture requires StandaloneWindows64 Player target.");
            const string modeEnv = "SAMPLEGAME_CONTENT__RUNTIMEMODE";
            var previousMode = Environment.GetEnvironmentVariable(modeEnv, EnvironmentVariableTarget.Process);
            EnsureFolder(FixtureParent);
            _folder = FixtureParent + "/" + Guid.NewGuid().ToString("N");
            EnsureFolder(_folder);
            BuildContentResult result;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // Scene（Whitebox）と、その Scene の node が所有する付随 Object（Full の material）。
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("Fixture shader is unavailable.");
                var material = new Material(shader);
                var materialPath = _folder + "/Lit.mat";
                AssetDatabase.CreateAsset(material, materialPath);
                var scenePath = _folder + "/Scene.unity";
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                EditorSceneManager.SaveScene(scene, scenePath);
                UnityEngine.Object.DestroyImmediate(cube);
                AssetDatabase.SaveAssets();

                var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                var materialGuid = AssetDatabase.AssetPathToGUID(materialPath);
                var sceneCandidate = new BuildContentCandidate(SceneKey + "/Whitebox", SceneKey, sceneGuid, new BuildProvenance("fixture", SceneKey));
                var attachedCandidate = new BuildContentCandidate(SceneKey + ":Lit/Full", SceneKey + ":Lit", materialGuid,
                    new BuildProvenance("fixture", SceneKey + ":Lit"), SceneKey);
                var tags = new Dictionary<string, IReadOnlyList<BuildTag>>
                {
                    [sceneCandidate.StableKey] = new[] { new BuildTag("Representation", "Whitebox") },
                    [attachedCandidate.StableKey] = new[] { new BuildTag("Representation", "Full") },
                };
                BuildDependencySnapshot Closure(string path, string guid) => new(guid, path,
                    AssetDatabase.GetDependencies(path).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal))
                        .Select(p => new BuildDependencyEntry(AssetDatabase.AssetPathToGUID(p), p)));
                var snapshot = new BuildMaterializationSnapshot(new[] { sceneCandidate, attachedCandidate }, tags,
                    Array.Empty<BuildContentRequirement>(), new[] { Closure(scenePath, sceneGuid), Closure(materialPath, materialGuid) });
                var plan = new BuildPlan(new BuildRequest(new[] { new BuildTag("Representation", "Whitebox"), new BuildTag("Representation", "Full") }),
                    new[] { sceneCandidate, attachedCandidate }, Array.Empty<BuildContentExclusion>());
                var artifacts = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "bs3-fixture"));
                result = new BuildContentCoordinator().Build(new BuildContentRequest(plan, snapshot, "bs5a-attached", artifacts));
                Assert.That(result.IsSuccess, Is.True, result.Summary);
                _copy = Path.Combine(artifacts, "attached-copy-" + result.Identity);
                Copy(result.ContentPath!, _copy);
            }
            finally
            {
                if (previous.Any(x => x.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // 移設後の検証が source asset に依存しないよう、Play の前に fixture を消す。
                if (_folder.Length != 0) { AssetDatabase.DeleteAsset(_folder); _folder = ""; }
            }

            Environment.SetEnvironmentVariable(modeEnv, "addressables", EnvironmentVariableTarget.Process);
            try
            {
                yield return new EnterPlayMode();
                yield return UniTask.ToCoroutine(async () =>
                {
                    var session = ContentDirectorySession.Register(_copy, result.Identity, BuildContentCoordinator.TargetName);
                    // 本番と同じく封じた Addressables backend の上で読む。directory の外へ抜ければ InvalidOperationException になる。
                    var assets = new AssetManagement();
                    assets.InstallContentDirectory(session);
                    assets.SealAddressables();
                    IAssetHandle<Material>? ownerHandle = null;
                    IAssetHandle<Material>? childHandle = null;
                    try
                    {
                        var lit = new AttachedAssetDescription("Lit", new List<AssetPayload> { new(string.Empty, new AssetReference("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")) });
                        var ghost = new AttachedAssetDescription("Ghost", new List<AssetPayload> { new(string.Empty, new AssetReference("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")) });
                        var ownerLineage = new List<KeyValuePair<string, IReadOnlyList<AttachedAssetDescription>>>
                        {
                            new(SceneKey, new[] { lit, ghost }),
                        };
                        var childLineage = new List<KeyValuePair<string, IReadOnlyList<AttachedAssetDescription>>>
                        {
                            new(ChildKey, Array.Empty<AttachedAssetDescription>()),
                            new(SceneKey, new[] { lit, ghost }),
                        };
                        var owner = Scene(SceneKey, assets, ownerLineage);
                        var child = Scene(ChildKey, assets, childLineage);

                        // Whitebox で動く Scene から、Full しか無い付随 entry へ fallback して解決・ロードする。
                        ownerHandle = await owner.LoadAttached<Material>("Lit");
                        Assert.That(ownerHandle.Value, Is.Not.Null);
                        childHandle = await child.LoadAttached<Material>(SceneKey, "Lit");
                        Assert.That(childHandle.Value, Is.SameAs(ownerHandle.Value));

                        // 宣言はあるが entry の無い名前は EntryMissing（失敗した key と表現が入る）。
                        var missing = await Catch(() => owner.LoadAttached<Material>("Ghost"));
                        Assert.That(missing, Is.TypeOf<ContentDirectoryException>());
                        Assert.That(((ContentDirectoryException)missing!).Code, Is.EqualTo(ContentDirectoryFailureCode.EntryMissing));
                        Assert.That(((ContentDirectoryException)missing).LogicalKey, Is.EqualTo(SceneKey + ":Ghost"));
                        Assert.That(((ContentDirectoryException)missing).Representation, Is.EqualTo("Whitebox"));

                        // 片方の所有者を解放しても、もう片方は有効。IAssetHandle.IsValid は「参照先がまだ有効か」で
                        // 所有者ごとの有効性ではないので、解放は所有者の一覧で確かめる。
                        var litKey = session.GetObjectKey(SceneKey + ":Lit", "Whitebox");
                        Assert.That(assets.GetOwners(litKey), Is.EquivalentTo(new[] { AssetOwner.Scene(SceneKey), AssetOwner.Scene(ChildKey) }));
                        assets.ReleaseScene(SceneKey);
                        Assert.That(assets.GetOwners(litKey), Is.EqualTo(new[] { AssetOwner.Scene(ChildKey) }));
                        Assert.That(childHandle.IsValid, Is.True);
                        Assert.That(childHandle.Value, Is.Not.Null);
                        ownerHandle = null;
                    }
                    finally
                    {
                        if (childHandle != null) assets.ReleaseScene(ChildKey);
                        if (ownerHandle != null) assets.ReleaseScene(SceneKey);
                        await session.CloseAsync();
                    }
                });
                yield return new ExitPlayMode();
            }
            finally
            {
                Environment.SetEnvironmentVariable(modeEnv, previousMode, EnvironmentVariableTarget.Process);
            }
        }

        private static async UniTask<Exception?> Catch(Func<UniTask<IAssetHandle<Material>>> action)
        {
            try { await action(); return null; }
            catch (Exception exception) { return exception; }
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

        private static void Copy(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        }

        private static void EnsureFolder(string path)
        {
            var parent = "Assets";
            foreach (var segment in path.Substring("Assets/".Length).Split('/'))
            {
                var next = parent + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, segment);
                parent = next;
            }
        }

        private static void DeleteIfEmpty(string assetFolder)
        {
            if (!AssetDatabase.IsValidFolder(assetFolder)) return;
            var absolute = Path.Combine(Application.dataPath, assetFolder.Substring("Assets/".Length));
            if (Directory.GetFileSystemEntries(absolute).Length == 0) AssetDatabase.DeleteAsset(assetFolder);
        }
    }
}
