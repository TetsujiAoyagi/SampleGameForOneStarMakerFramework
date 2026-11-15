#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// Scene に焼き込む prefab の版の印を、製品の coordinator と adapter で書いて戻す経路（HANDOFF CONTENT_PREFAB_STAMP の
    /// AC5 (a)〜(g)、AC7 (a)〜(e)、AC8'）。一時 folder の prefab と Scene を使い、Unity の Content Directory build は
    /// adapter の継ぎ目で差し替える（成功は workspace に manifest の pointer だけを書く）。記録の置き場はテストごとの一時 folder。
    /// </summary>
    public sealed class ScenePrefabStampEditorTests
    {
        private const string Manifest = "BuildManifestHash.txt";
        private string _folder = "";
        private string _records = "";
        private string _artifacts = "";
        private SceneSetup[] _setup = Array.Empty<SceneSetup>();
        private string _s1 = "", _s2 = "", _plain = "";
        private int _buildCalls;
        private string[] _observed = Array.Empty<string>();
        private readonly Dictionary<string, string> _metaDuringBuild = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _importerDuringBuild = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _metaBefore = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> _metaTimeBefore = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _sceneBefore = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> _sceneTimeBefore = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath)!;
        private static string Disk(string assetPath) => Path.Combine(ProjectRoot, assetPath);

        [SetUp]
        public void SetUp()
        {
            // adapter は固定の target でしか build しない（自動では切り替えない）。
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                Assert.Ignore("The scene prefab stamp tests require the active StandaloneWindows64 Player target.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    Assert.Ignore("An open scene has unsaved changes; the test does not replace it.");
            _setup = EditorSceneManager.GetSceneManagerSetup();
            var id = Guid.NewGuid().ToString("N").Substring(0, 8);
            _folder = "Assets/OsmStampTest" + id;
            _records = Path.Combine(ProjectRoot, "Temp", "osm-stamp-test-" + id);
            _artifacts = Path.GetFullPath(Path.Combine(ProjectRoot, "..", "TestResults", "bs3-fixture", "stamp-" + id));
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(_folder));
            var p1 = CreatePrefab("P1");
            var p2 = CreatePrefab("P2");
            _s1 = CreateScene("S1", p1);
            _s2 = CreateScene("S2", p2);
            _plain = CreateScene("SPlain", null);
            // 改行が CRLF の .meta を 1 つ持たせ、印の書き込みと戻しが改行の形を変えないことを見る。
            var crlf = File.ReadAllText(Disk(_s2 + ".meta")).Replace("\r\n", "\n").Replace("\n", "\r\n");
            File.WriteAllText(Disk(_s2 + ".meta"), crlf);
            AssetDatabase.ImportAsset(_s2, ImportAssetOptions.ForceUpdate);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _buildCalls = 0;
            _metaDuringBuild.Clear();
            _importerDuringBuild.Clear();
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
            if (Directory.Exists(_records)) Directory.Delete(_records, true);
            if (Directory.Exists(_artifacts)) Directory.Delete(_artifacts, true);
            if (_setup.Length != 0 && _setup.All(x => File.Exists(Disk(x.path))) && _setup.Any(x => x.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(_setup);
            _folder = "";
        }

        // ───── AC5: build の後に source が戻る ─────

        [Test]
        public void Success_WritesStampDuringBuild_AndRestoresAfter()
        {
            Remember(_s1, _s2, _plain);
            var result = Build(new[] { _s1, _s2, _plain });
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            Assert.That(_buildCalls, Is.EqualTo(1));
            // build の間は、閉包に prefab を持つ Scene だけが印を持ち、製品の Refresh がそれを import している。
            Assert.That(State(_metaDuringBuild[_s1]), Is.EqualTo(UserDataState.OwnStamp));
            Assert.That(State(_metaDuringBuild[_s2]), Is.EqualTo(UserDataState.OwnStamp));
            Assert.That(State(_metaDuringBuild[_plain]), Is.EqualTo(UserDataState.Empty));
            Assert.That(_importerDuringBuild[_s1], Does.StartWith(ScenePrefabStampRules.Prefix));
            Assert.That(_metaDuringBuild[_s2], Does.Contain("\r\n"));
            AssertRestored(_s1, _s2, _plain);
            AssertUntouched(_plain);
            Assert.That(Directory.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated")), Is.False);
            Assert.That(Directory.Exists(_records) ? Directory.GetFiles(_records) : Array.Empty<string>(), Is.Empty);
        }

        [Test]
        public void BuildCallFails_Restores([Values(false, true)] bool throws)
        {
            Remember(_s1, _s2);
            var result = Build(new[] { _s1, _s2 }, p =>
            {
                Observe();
                if (throws) throw new InvalidOperationException("seam throws");
                return new UnityContentBuildResult(false, "seam failed");
            });
            AssertFailure(result, throws ? "seam throws" : "seam failed");
            Assert.That(State(_metaDuringBuild[_s1]), Is.EqualTo(UserDataState.OwnStamp));
            AssertRestored(_s1, _s2);
        }

        [Test]
        public void StopsBeforeBuildCall_AfterStampWritten_Restores()
        {
            Remember(_s1, _s2);
            // root の GUID の照合（印を書いた後、build の呼び出しの前）で止める。
            var wrongGuid = new string('f', 32);
            var result = Build(new[] { _s1, _s2 }, overrideGuid: (_s1, wrongGuid));
            AssertFailure(result, "Root GUID/path changed");
            Assert.That(_buildCalls, Is.Zero);
            AssertRestored(_s1, _s2);
        }

        [Test]
        public void LeftoverWithRecord_NextBuildRestores([Values(true, false)] bool stampedAgain)
        {
            Remember(_s1, _s2);
            LeaveStamp(_s1);
            Assert.That(State(File.ReadAllText(Disk(_s1 + ".meta"))), Is.EqualTo(UserDataState.OwnStamp));
            var result = Build(stampedAgain ? new[] { _s1, _s2 } : new[] { _s2 });
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            if (stampedAgain) Assert.That(State(_metaDuringBuild[_s1]), Is.EqualTo(UserDataState.OwnStamp));
            AssertRestored(_s1, _s2);
            Assert.That(Directory.GetFiles(_records), Is.Empty);
        }

        [Test]
        public void LeftoverEditedOnOtherLines_RestoresOnlyTheStampLine_WithWarning()
        {
            Remember(_s1);
            LeaveStamp(_s1);
            var stamped = File.ReadAllText(Disk(_s1 + ".meta"));
            File.WriteAllText(Disk(_s1 + ".meta"), stamped.Replace("assetBundleName: ", "assetBundleName: stamptest"));
            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape(_s1) + @"\.meta"));
            var result = Build(new[] { _s2 });
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            Assert.That(File.ReadAllText(Disk(_s1 + ".meta")),
                Is.EqualTo(_metaBefore[_s1].Replace("assetBundleName: ", "assetBundleName: stamptest")));
        }

        [Test]
        public void SecondMetaLocked_FirstRestored_BuildFails([Values(FileShare.None, FileShare.Read)] FileShare share)
        {
            // None は書く前の検査の読み取りで止まり、Read は 1 つ目を書いた後の 2 つ目の書き込みで止まる。
            Remember(_s1, _s2);
            BuildContentResult result;
            using (new FileStream(Disk(_s2 + ".meta"), FileMode.Open, FileAccess.Read, share))
                result = Build(new[] { _s1, _s2 });
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Issues.Single().Code, Is.EqualTo(BuildContentIssueCode.BuildFailure));
            Assert.That(_buildCalls, Is.Zero);
            AssertRestored(_s1, _s2);
            Assert.That(Directory.Exists(_records) ? Directory.GetFiles(_records) : Array.Empty<string>(), Is.Empty);
        }

        [Test]
        public void StampWithoutRecord_IsRestoredLineOnly_WithWarning()
        {
            Remember(_s1);
            // Library を消した後を模す: 記録が無く、userData が印の形を持つ。
            var meta = File.ReadAllText(Disk(_s1 + ".meta"));
            File.WriteAllText(Disk(_s1 + ".meta"), ScenePrefabStampRules.WriteStamp(meta, new string('a', 64)));
            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape(_s1) + @"\.meta"));
            var result = Build(new[] { _s1 });
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            AssertRestored(_s1);
        }

        [Test]
        public void LeftoverOfMovedScene_IsRestoredAtTheNewPath()
        {
            Remember(_s1);
            LeaveStamp(_s1);
            AssetDatabase.CreateFolder(_folder, "Moved");
            var moved = _folder + "/Moved/S1m.unity";
            Assert.That(AssetDatabase.MoveAsset(_s1, moved), Is.Empty);
            var result = Build(new[] { moved });
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            Assert.That(_buildCalls, Is.EqualTo(1));
            // 移動後の Scene も今回の印の対象なので、build の間は移動後の .meta が今回の印を持つ。
            Assert.That(State(_metaDuringBuild[moved]), Is.EqualTo(UserDataState.OwnStamp));
            Assert.That(File.ReadAllText(Disk(moved + ".meta")), Is.EqualTo(_metaBefore[_s1]));
            Assert.That(AssetImporter.GetAtPath(moved).userData, Is.Empty);
        }

        // ───── AC7: 未保存の編集・読み取り専用・空でない userData で、書く前に止まる ─────

        [Test]
        public void UnsavedOpenScene_Stops([Values(true, false)] bool second)
        {
            Remember(_s1, _s2);
            var scene = EditorSceneManager.OpenScene(second ? _s2 : _s1, OpenSceneMode.Single);
            MakeUnsavedEdit(scene);
            var result = Build(new[] { _s1, _s2 });
            AssertFailure(result, second ? _s2 : _s1);
            Assert.That(_buildCalls, Is.Zero);
            AssertUntouched(_s1, _s2);
            Assert.That(scene.isDirty, Is.True);
            Assert.That(scene.GetRootGameObjects().Any(x => x.name == "UnsavedEdit"), Is.True);
        }

        [Test]
        public void UnsavedSceneThatIsNotActive_Stops()
        {
            Remember(_s1, _s2);
            EditorSceneManager.OpenScene(_plain, OpenSceneMode.Single);
            var scene = EditorSceneManager.OpenScene(_s2, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(_plain));
            MakeUnsavedEdit(scene);
            var result = Build(new[] { _s1, _s2 });
            AssertFailure(result, _s2);
            AssertUntouched(_s1, _s2);
            Assert.That(scene.isDirty, Is.True);
        }

        [Test]
        public void ReadOnlyMeta_Stops()
        {
            Remember(_s1, _s2);
            File.SetAttributes(Disk(_s2 + ".meta"), File.GetAttributes(Disk(_s2 + ".meta")) | FileAttributes.ReadOnly);
            var result = Build(new[] { _s1, _s2 });
            AssertFailure(result, _s2);
            Assert.That(_buildCalls, Is.Zero);
            AssertUntouched(_s1, _s2);
        }

        [TestCase("  userData: foreign")]
        [TestCase("  userData: osm-prefab-stamp-zzz")]
        [TestCase("  userData:  ")]
        [TestCase("  userData:\t")]
        public void UserDataNotEmpty_Stops(string line)
        {
            var meta = File.ReadAllText(Disk(_s2 + ".meta"));
            var current = ScenePrefabStampRules.FindUserData(meta);
            File.WriteAllText(Disk(_s2 + ".meta"),
                meta.Substring(0, current.ValueStart - "userData:".Length - 2) + line + meta.Substring(current.ValueStart + current.ValueLength));
            Assert.That(State(File.ReadAllText(Disk(_s2 + ".meta"))), Is.EqualTo(UserDataState.Other));
            Remember(_s1, _s2);
            var result = Build(new[] { _s1, _s2 });
            AssertFailure(result, _s2);
            Assert.That(_buildCalls, Is.Zero);
            AssertUntouched(_s1, _s2);
        }

        [Test]
        public void UnsavedSceneWithLeftover_StopsWithoutRestoring([Values(true, false)] bool stampedAgain)
        {
            Remember(_s2);
            LeaveStamp(_s1);
            Remember(_s1);
            var scene = EditorSceneManager.OpenScene(_s1, OpenSceneMode.Single);
            MakeUnsavedEdit(scene);
            var result = Build(stampedAgain ? new[] { _s1, _s2 } : new[] { _s2 });
            AssertFailure(result, _s1);
            Assert.That(_buildCalls, Is.Zero);
            // 残った印も戻さず（.meta は印のまま）、どの .meta も書かない。
            AssertUntouched(_s1, _s2);
            Assert.That(State(File.ReadAllText(Disk(_s1 + ".meta"))), Is.EqualTo(UserDataState.OwnStamp));
            Assert.That(scene.isDirty, Is.True);
            Assert.That(scene.GetRootGameObjects().Any(x => x.name == "UnsavedEdit"), Is.True);
        }

        // ───── AC8'（AC8 の代替。Phase B の Editor の確認で AC8 が不成立だったので採った。HANDOFF §6） ─────
        // 開いている Scene の .meta を書くと、変更が無くても Editor が「modified externally」のダイアログを出すため、
        // 印・戻しの対象の Scene が開いていれば、変更の有無にかかわらず書く前に止まる。

        [Test]
        public void OpenSceneWithoutChanges_Stops([Values(true, false)] bool additive)
        {
            Remember(_s1, _s2);
            if (additive) EditorSceneManager.OpenScene(_plain, OpenSceneMode.Single);
            var scene = EditorSceneManager.OpenScene(_s1, additive ? OpenSceneMode.Additive : OpenSceneMode.Single);
            Assert.That(scene.isDirty, Is.False);
            var result = Build(new[] { _s1, _s2 });
            AssertFailure(result, _s1);
            Assert.That(_buildCalls, Is.Zero);
            AssertUntouched(_s1, _s2);
            var after = SceneManager.GetSceneByPath(_s1);
            Assert.That(after.isLoaded, Is.True);
            Assert.That(after.isDirty, Is.False);
        }

        [Test]
        public void OpenLeftoverSceneWithoutChanges_StopsWithoutRestoring()
        {
            Remember(_s2);
            LeaveStamp(_s1);
            Remember(_s1);
            var scene = EditorSceneManager.OpenScene(_s1, OpenSceneMode.Single);
            Assert.That(scene.isDirty, Is.False);
            var result = Build(new[] { _s2 });
            AssertFailure(result, _s1);
            Assert.That(_buildCalls, Is.Zero);
            AssertUntouched(_s1, _s2);
            Assert.That(State(File.ReadAllText(Disk(_s1 + ".meta"))), Is.EqualTo(UserDataState.OwnStamp));
        }

        [Test]
        public void OpenSceneThatNeedsNoStamp_DoesNotStop()
        {
            // 印を書かず戻しの対象でもない Scene は、開いていても未保存でも止めない（file に触れない）。
            Remember(_s1, _plain);
            var scene = EditorSceneManager.OpenScene(_plain, OpenSceneMode.Single);
            MakeUnsavedEdit(scene);
            var result = Build(new[] { _s1, _plain });
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            AssertRestored(_s1);
            AssertUntouched(_plain);
            Assert.That(scene.isDirty, Is.True);
        }

        // ───── 部品 ─────

        private BuildContentResult Build(IReadOnlyList<string> scenes,
            Func<BuildContentDirectoryParameters, UnityContentBuildResult>? build = null, (string path, string guid)? overrideGuid = null)
        {
            _observed = scenes.Concat(new[] { _s1, _s2, _plain }).Distinct(StringComparer.Ordinal).ToArray();
            var candidates = scenes.Select(path =>
            {
                var guid = overrideGuid != null && overrideGuid.Value.path == path ? overrideGuid.Value.guid : AssetDatabase.AssetPathToGUID(path);
                return new BuildContentCandidate(Path.GetFileNameWithoutExtension(path), Path.GetFileNameWithoutExtension(path), guid,
                    new BuildProvenance("stamp-test", path));
            }).ToArray();
            var closures = scenes.Select((path, i) => Closure(path, candidates[i].PhysicalKey)).ToArray();
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), candidates, Array.Empty<BuildContentExclusion>());
            var snapshot = new BuildMaterializationSnapshot(candidates, new Dictionary<string, IReadOnlyList<BuildTag>>(),
                Array.Empty<BuildContentRequirement>(), closures);
            var adapter = new UnityContentDirectoryAdapter(build ?? Succeed, _records);
            return new BuildContentCoordinator(adapter).Build(
                new BuildContentRequest(plan, snapshot, "stamp", _artifacts, null, null, BuildContentMode.Full));
        }

        private static BuildDependencySnapshot Closure(string path, string rootGuid)
        {
            // 製品の snapshot と同じ閉包（AssetDependencySnapshotBuilder）。GUID を差し替える時は root の項目だけを替える。
            var issues = new List<BuildMaterializationIssue>();
            var real = new AssetDependencySnapshotBuilder(new UnityAssetDatabaseGateway())
                .Build(AssetDatabase.AssetPathToGUID(path), path, issues);
            Assert.That(real, Is.Not.Null, string.Join("; ", issues.Select(x => x.Code)));
            return new BuildDependencySnapshot(rootGuid, path,
                real!.Entries.Select(x => x.Path == path ? new BuildDependencyEntry(rootGuid, path) : x));
        }

        private UnityContentBuildResult Succeed(BuildContentDirectoryParameters parameters)
        {
            Observe();
            File.WriteAllText(Path.Combine(parameters.outputPath, Manifest), "fake");
            return new UnityContentBuildResult(true, "fake");
        }

        // build の呼び出しの時点の .meta と、Unity が import した userData。
        // 今回の root と fixture の Scene のうち、今ある物だけを見る（移動した Scene の移動前の path は file が無い）。
        private void Observe()
        {
            _buildCalls++;
            foreach (var path in _observed)
            {
                if (!File.Exists(Disk(path + ".meta"))) continue;
                _metaDuringBuild[path] = File.ReadAllText(Disk(path + ".meta"));
                _importerDuringBuild[path] = AssetImporter.GetAtPath(path).userData;
            }
        }

        // 異常終了を模す: 印を書いた後に戻しの処理（Dispose）を呼ばない。記録はテストの記録の置き場に残る。
        private void LeaveStamp(string scene)
        {
            var guid = AssetDatabase.AssetPathToGUID(scene);
            var candidate = new BuildContentCandidate("leftover", "leftover", guid, new BuildProvenance("stamp-test", scene));
            var projection = BuildContentProjection.Create(
                new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { candidate }, Array.Empty<BuildContentExclusion>()),
                new BuildMaterializationSnapshot(new[] { candidate }, new Dictionary<string, IReadOnlyList<BuildTag>>(),
                    Array.Empty<BuildContentRequirement>(), new[] { Closure(scene, guid) }));
            Assert.That(projection.IsValid, Is.True);
            var stamp = new ScenePrefabStamp(null, _records);
            stamp.Prepare(projection.Roots);
            stamp.Apply();
        }

        private void Remember(params string[] scenes)
        {
            foreach (var scene in scenes)
            {
                _metaBefore[scene] = File.ReadAllText(Disk(scene + ".meta"));
                _metaTimeBefore[scene] = File.GetLastWriteTimeUtc(Disk(scene + ".meta"));
                _sceneBefore[scene] = ScenePrefabStampRules.Sha256Hex(File.ReadAllBytes(Disk(scene)));
                _sceneTimeBefore[scene] = File.GetLastWriteTimeUtc(Disk(scene));
            }
        }

        // .meta が記録したバイトに戻り、Unity が import した userData も空に戻り、Scene の file 本体は一度も書かれていない。
        private void AssertRestored(params string[] scenes)
        {
            AssertFiles(scenes);
            foreach (var scene in scenes)
                Assert.That(AssetImporter.GetAtPath(scene).userData, Is.Empty, scene + " userData");
        }

        // .meta は bytes も更新時刻も変わらない（書かれていない）。
        private void AssertUntouched(params string[] scenes)
        {
            AssertFiles(scenes);
            foreach (var scene in scenes)
                Assert.That(File.GetLastWriteTimeUtc(Disk(scene + ".meta")), Is.EqualTo(_metaTimeBefore[scene]), scene + ".meta time");
        }

        private void AssertFiles(string[] scenes)
        {
            foreach (var scene in scenes)
            {
                Assert.That(File.ReadAllText(Disk(scene + ".meta")), Is.EqualTo(_metaBefore[scene]), scene + ".meta");
                Assert.That(ScenePrefabStampRules.Sha256Hex(File.ReadAllBytes(Disk(scene))), Is.EqualTo(_sceneBefore[scene]), scene);
                Assert.That(File.GetLastWriteTimeUtc(Disk(scene)), Is.EqualTo(_sceneTimeBefore[scene]), scene + " time");
            }
        }

        private void AssertFailure(BuildContentResult result, string detail)
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Issues.Single().Code, Is.EqualTo(BuildContentIssueCode.BuildFailure));
            Assert.That(result.Issues.Single().Detail, Does.Contain(detail));
        }

        private static UserDataState State(string meta) => ScenePrefabStampRules.FindUserData(meta).State;

        // 開いている Scene に未保存の変更を作る。batch の Editor では GameObject を足しただけでは Scene が dirty にならない
        // （判定 C 1 回目の F2）ので、変更を足した上で dirty の印を明示に付ける。対話の Editor でも同じ状態になる。
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

        private string CreateScene(string name, string? prefab)
        {
            var path = _folder + "/" + name + ".unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (prefab != null) PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
            else new GameObject("Plain");
            Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True, path);
            return path;
        }
    }
}
