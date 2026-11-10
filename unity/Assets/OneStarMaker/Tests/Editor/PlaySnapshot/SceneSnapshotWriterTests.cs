#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using OneStarMaker.Editor.PlaySnapshot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Tests.Editor.PlaySnapshot
{
    /// <summary>
    /// 開いている Scene の複製の書き出し。実際の Scene と AssetDatabase を使い、元の Scene の file・Dirty が変わらないことと、
    /// 複製が in-memory の内容を持つことを見る。test 用の Scene と書き出し先は一時 folder に作り、TearDown で消す。
    /// </summary>
    public sealed class SceneSnapshotWriterTests
    {
        private const string Root = "Assets/OsmPlaySnapshotWriterTests";
        private string _sourceFolder = "";
        private string _copyFolder = "";
        private SceneSetup[] _savedSetup = Array.Empty<SceneSetup>();

        [SetUp]
        public void SetUp()
        {
            _savedSetup = EditorSceneManager.GetSceneManagerSetup();
            var id = Guid.NewGuid().ToString("N").Substring(0, 8);
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "OsmPlaySnapshotWriterTests");
            _sourceFolder = Root + "/Source" + id;
            _copyFolder = Root + "/Copy" + id;
            AssetDatabase.CreateFolder(Root, "Source" + id);
            AssetDatabase.CreateFolder(Root, "Copy" + id);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Root);
            if (_savedSetup.Length != 0)
            {
                try { EditorSceneManager.RestoreSceneManagerSetup(_savedSetup); }
                catch (ArgumentException) { /* 元の Scene が無い setup（未保存の新規 Scene だけ）は戻せない */ }
            }
        }

        private Scene CreateSavedScene(string name, string objectName)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject(objectName);
            var path = _sourceFolder + "/" + name + ".unity";
            Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
            return SceneManager.GetSceneByPath(path);
        }

        [Test]
        public void DirtyScene_CopyHasInMemoryContent_AndSourceIsUnchanged()
        {
            var scene = CreateSavedScene("Title", "Saved");
            var sourceSha = SceneSnapshotWriter.Sha256Of(scene.path);
            new GameObject("UnsavedEdit");
            EditorSceneManager.MarkSceneDirty(scene);

            var entries = SceneSnapshotWriter.Write(new[] { scene }, scene, _copyFolder, new[] { "OsmPlaySnapshotStart" });

            Assert.That(entries, Has.Count.EqualTo(1));
            var entry = entries[0];
            Assert.That(entry.identity, Is.EqualTo("Title"));
            Assert.That(entry.copyPath, Is.EqualTo(_copyFolder + "/Title.unity"));
            Assert.That(entry.wasDirty, Is.True);
            Assert.That(entry.isActive, Is.True);
            Assert.That(entry.copyGuid, Is.Not.Empty.And.Not.EqualTo(entry.sourceGuid));
            Assert.That(entry.sha256, Is.EqualTo(SceneSnapshotWriter.Sha256Of(entry.copyPath)));
            Assert.That(File.ReadAllText(entry.copyPath), Does.Contain("UnsavedEdit"), "複製は in-memory の内容を持つ");
            Assert.That(SceneSnapshotWriter.Sha256Of(scene.path), Is.EqualTo(sourceSha), "元の file の bytes は変わらない");
            Assert.That(scene.isDirty, Is.True, "元の Scene の Dirty は変わらない");
            Assert.That(scene.path, Is.EqualTo(_sourceFolder + "/Title.unity"), "元の Scene の path は変わらない");
        }

        [Test]
        public void UnsavedNewScene_IsRejected_AndWritesNothing()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.Throws<PlaySnapshotException>(() => SceneSnapshotWriter.Write(new[] { scene }, scene, _copyFolder, Array.Empty<string>()));
            Assert.That(Directory.GetFiles(_copyFolder), Is.Empty);
        }

        [Test]
        public void TwoScenesWithTheSameName_AreRejected()
        {
            var first = CreateSavedScene("Same", "A");
            var otherFolder = Root + "/" + Path.GetFileName(_sourceFolder) + "b";
            AssetDatabase.CreateFolder(Root, Path.GetFileName(otherFolder));
            var second = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Assert.That(EditorSceneManager.SaveScene(second, otherFolder + "/Same.unity"), Is.True);
            first = SceneManager.GetSceneByPath(_sourceFolder + "/Same.unity");

            var exception = Assert.Throws<PlaySnapshotException>(() =>
                SceneSnapshotWriter.Write(new[] { first, second }, first, _copyFolder, Array.Empty<string>()));
            Assert.That(exception!.Message, Does.Contain("Same"));
            Assert.That(Directory.GetFiles(_copyFolder), Is.Empty);
        }

        [Test]
        public void ExistingDestination_IsRejected()
        {
            var scene = CreateSavedScene("Title", "Saved");
            File.WriteAllText(_copyFolder + "/Title.unity", "already here");
            Assert.Throws<PlaySnapshotException>(() => SceneSnapshotWriter.Write(new[] { scene }, scene, _copyFolder, Array.Empty<string>()));
            Assert.That(File.ReadAllText(_copyFolder + "/Title.unity"), Is.EqualTo("already here"), "既にある物は上書きも削除もしない");
        }

        [Test]
        public void FailureMidway_DeletesWhatWasWritten()
        {
            var first = CreateSavedScene("First", "A");
            var second = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Assert.That(EditorSceneManager.SaveScene(second, _sourceFolder + "/Second.unity"), Is.True);
            first = SceneManager.GetSceneByPath(_sourceFolder + "/First.unity");
            // 2 つ目の書き込み先を先に塞ぐ。1 つ目は書けて、2 つ目で失敗する。
            File.WriteAllText(_copyFolder + "/Second.unity", "blocker");

            Assert.Throws<PlaySnapshotException>(() =>
                SceneSnapshotWriter.Write(new[] { first, second }, first, _copyFolder, Array.Empty<string>()));
            Assert.That(File.Exists(_copyFolder + "/First.unity"), Is.False, "途中の失敗では書いた複製を消す");
            Assert.That(File.Exists(_copyFolder + "/First.unity.meta"), Is.False);
        }

        [Test]
        public void ReservedName_IsRejected()
        {
            var scene = CreateSavedScene("OsmPlaySnapshotStart", "A");
            Assert.Throws<PlaySnapshotException>(() =>
                SceneSnapshotWriter.Write(new[] { scene }, scene, _copyFolder, new[] { "OsmPlaySnapshotStart" }));
        }
    }
}
