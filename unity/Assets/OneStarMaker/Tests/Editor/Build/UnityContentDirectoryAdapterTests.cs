#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// adapter が利用者の source（Scene の file と .meta）を書かず、開いている未保存の Scene と、読み取り専用の .meta・`userData` が空でない .meta、
    /// Unity の build の失敗・例外のどれでも build の前後で source が変わらず生成 folder が残らないこと
    /// （契約は 18 章 §4「Scene に焼き込む prefab と model の編集」）。Unity の Content Directory build は adapter の継ぎ目で差し替える
    /// （成功は workspace に manifest の pointer だけを書く）。実 build の確認は git に残した harness が担う
    /// （docs/unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md「6000.6.9f1 で印を外した製品の build 経路」の測り直し）。
    /// </summary>
    public sealed class UnityContentDirectoryAdapterTests
    {
        private const string Manifest = "BuildManifestHash.txt";
        private string _folder = "";
        private string _artifacts = "";
        private SceneSetup[] _setup = Array.Empty<SceneSetup>();
        private string _scene = "";
        private int _buildCalls;
        private string _metaBefore = "";
        private string _sceneHashBefore = "";
        private string _metaHashBefore = "";

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath)!;
        private static string Disk(string assetPath) => Path.Combine(ProjectRoot, assetPath);
        private static string Sha256(string path)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));
        }

        [SetUp]
        public void SetUp()
        {
            // adapter は固定の target でしか build しない（自動では切り替えない）。
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                Assert.Ignore("The adapter tests require the active StandaloneWindows64 Player target.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    Assert.Ignore("An open scene has unsaved changes; the test does not replace it.");
            _setup = EditorSceneManager.GetSceneManagerSetup();
            var id = Guid.NewGuid().ToString("N").Substring(0, 8);
            _folder = "Assets/OsmAdapterTest" + id;
            _artifacts = Path.GetFullPath(Path.Combine(ProjectRoot, "..", "TestResults", "bs3-fixture", "adapter-" + id));
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(_folder));
            // 閉包に prefab を持つ Scene。印のあった頃にこの Scene が build を止める条件の対象だった形をそのまま使う。
            var prefab = CreatePrefab("P1");
            _scene = CreateScene("S1", prefab);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _buildCalls = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (string.IsNullOrEmpty(_folder)) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (Directory.Exists(Disk(_folder)))
                foreach (var file in Directory.GetFiles(Disk(_folder), "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
            AssetDatabase.DeleteAsset(_folder);
            if (Directory.Exists(_artifacts)) Directory.Delete(_artifacts, true);
            if (_setup.Length != 0 && _setup.All(x => File.Exists(Disk(x.path))) && _setup.Any(x => x.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(_setup);
            _folder = "";
        }

        [Test]
        public void OpenUnsavedScene_WithReadOnlyMeta_BuildsWithoutTouchingSource()
        {
            var scene = EditorSceneManager.OpenScene(_scene, OpenSceneMode.Single);
            MakeUnsavedEdit(scene);
            File.SetAttributes(Disk(_scene + ".meta"), FileAttributes.ReadOnly);
            Remember();
            var result = Build(Succeed);
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            Assert.That(_buildCalls, Is.EqualTo(1));
            AssertSourceUnchanged();
            // 未保存の変更は Editor に残る（build は開いている Scene を保存も破棄もしない）。
            var after = SceneManager.GetSceneByPath(_scene);
            Assert.That(after.isLoaded, Is.True);
            Assert.That(after.isDirty, Is.True);
            Assert.That(after.GetRootGameObjects().Any(x => x.name == "UnsavedEdit"), Is.True);
            AssertNoGeneratedFolder();
        }

        [Test]
        public void NonEmptyUserData_DoesNotStopTheBuild()
        {
            var importer = AssetImporter.GetAtPath(_scene);
            importer.userData = "someone-else";
            importer.SaveAndReimport();
            Remember();
            Assert.That(_metaBefore, Does.Contain("someone-else"));
            var result = Build(Succeed);
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            Assert.That(_buildCalls, Is.EqualTo(1));
            AssertSourceUnchanged();
            Assert.That(AssetImporter.GetAtPath(_scene).userData, Is.EqualTo("someone-else"));
            AssertNoGeneratedFolder();
        }

        [Test]
        public void BuildCallFails_FailsAndLeavesNothing([Values(false, true)] bool throws)
        {
            Remember();
            var result = Build(p =>
            {
                _buildCalls++;
                if (throws) throw new InvalidOperationException("seam throws");
                return new UnityContentBuildResult(false, "seam failed");
            });
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Issues.Single().Code, Is.EqualTo(BuildContentIssueCode.BuildFailure));
            Assert.That(result.Issues.Single().Detail, Does.Contain(throws ? "seam throws" : "seam failed"));
            Assert.That(_buildCalls, Is.EqualTo(1));
            AssertSourceUnchanged();
            AssertNoGeneratedFolder();
        }

        private BuildContentResult Build(Func<BuildContentDirectoryParameters, UnityContentBuildResult> build)
        {
            var guid = AssetDatabase.AssetPathToGUID(_scene);
            var candidate = new BuildContentCandidate("S1", "S1", guid, new BuildProvenance("adapter-test", _scene));
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { candidate }, Array.Empty<BuildContentExclusion>());
            var snapshot = new BuildMaterializationSnapshot(new[] { candidate }, new Dictionary<string, IReadOnlyList<BuildTag>>(),
                Array.Empty<BuildContentRequirement>(), new[] { Closure(_scene, guid) });
            return new BuildContentCoordinator(new UnityContentDirectoryAdapter(build)).Build(
                new BuildContentRequest(plan, snapshot, "adapter", _artifacts, null, null, BuildContentMode.Full));
        }

        private static BuildDependencySnapshot Closure(string path, string rootGuid)
        {
            // 製品の snapshot と同じ閉包（AssetDependencySnapshotBuilder）。
            var issues = new List<BuildMaterializationIssue>();
            var real = new AssetDependencySnapshotBuilder(new UnityAssetDatabaseGateway()).Build(rootGuid, path, issues);
            Assert.That(real, Is.Not.Null, string.Join("; ", issues.Select(x => x.Code)));
            return real!;
        }

        private UnityContentBuildResult Succeed(BuildContentDirectoryParameters parameters)
        {
            _buildCalls++;
            File.WriteAllText(Path.Combine(parameters.outputPath, Manifest), "fake");
            return new UnityContentBuildResult(true, "fake");
        }

        private void Remember()
        {
            _metaBefore = File.ReadAllText(Disk(_scene + ".meta"));
            _metaHashBefore = Sha256(Disk(_scene + ".meta"));
            _sceneHashBefore = Sha256(Disk(_scene));
        }

        private void AssertSourceUnchanged()
        {
            Assert.That(Sha256(Disk(_scene)), Is.EqualTo(_sceneHashBefore), _scene);
            Assert.That(Sha256(Disk(_scene + ".meta")), Is.EqualTo(_metaHashBefore), _scene + ".meta");
            Assert.That(File.ReadAllText(Disk(_scene + ".meta")), Is.EqualTo(_metaBefore));
        }

        // 生成 folder と、それを入れる空の親 folder が build の後に残らない。
        private static void AssertNoGeneratedFolder()
            => Assert.That(Directory.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated")), Is.False);

        // 開いている Scene に未保存の変更を作る。batch の Editor では GameObject を足しただけでは Scene が dirty にならない
        // ので、変更を足した上で dirty の印を明示に付ける。対話の Editor でも同じ状態になる。
        private static void MakeUnsavedEdit(Scene scene)
        {
            var go = new GameObject("UnsavedEdit");
            SceneManager.MoveGameObjectToScene(go, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Assert.That(scene.isDirty, Is.True, "the test could not create unsaved changes in " + scene.path);
        }

        private string CreatePrefab(string name)
        {
            var path = _folder + "/" + name + ".prefab";
            var root = new GameObject(name);
            new GameObject("Child").transform.SetParent(root.transform, false);
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                Assert.That(saved, Is.True, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            return path;
        }

        private string CreateScene(string name, string prefab)
        {
            var path = _folder + "/" + name + ".unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
            Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True, path);
            return path;
        }
    }
}
