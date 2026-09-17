#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.BuildContent;
using Unity.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Build
{
    public sealed class BuildContentDirectoryIntegrationTests
    {
        private const string FixtureParent = "Assets/OneStarMakerGenerated/BS2bTests";
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
        public IEnumerator ScenePrefabTextureAndTwoRepresentationsBuildAndMoveAsOneDirectory()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                Assert.Ignore("BS2b fixture requires StandaloneWindows64 Player target.");
            EnsureFolder(FixtureParent);
            _folder = FixtureParent + "/" + Guid.NewGuid().ToString("N");
            EnsureFolder(_folder);
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var texture = new Texture2D(2, 2);
                texture.SetPixels(new[] { Color.red, Color.red, Color.red, Color.red });
                texture.Apply();
                var texturePath = _folder + "/Texture.png";
                File.WriteAllBytes(Path.Combine(Application.dataPath, texturePath.Substring("Assets/".Length)), texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath);
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("Fixture shader is unavailable.");
                var mat = new Material(shader);
                mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                var matPath = _folder + "/Shared.mat";
                AssetDatabase.CreateAsset(mat, matPath);
                var paths = new[] { _folder + "/Scene.unity", _folder + "/High.prefab",
                    _folder + "/Low.prefab", texturePath, _folder + "/Excluded.prefab" };
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var sceneObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sceneObject.GetComponent<Renderer>().sharedMaterial = mat;
                EditorSceneManager.SaveScene(scene, paths[0]);
                UnityEngine.Object.DestroyImmediate(sceneObject);
                for (var i = 1; i <= 2; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.GetComponent<Renderer>().sharedMaterial = mat;
                    PrefabUtility.SaveAsPrefabAsset(go, paths[i]);
                    UnityEngine.Object.DestroyImmediate(go);
                }
                var excluded = new GameObject("Excluded");
                PrefabUtility.SaveAsPrefabAsset(excluded, paths[4]);
                UnityEngine.Object.DestroyImmediate(excluded);
                EditorSceneManager.SaveScene(scene, paths[0]);
                AssetDatabase.SaveAssets();

                var candidates = paths.Select((path, i) => new BuildContentCandidate("fixture-" + i,
                    i == 1 || i == 2 ? "shared-logical" : "logical-" + i,
                    AssetDatabase.AssetPathToGUID(path), new BuildProvenance("fixture", path))).ToArray();
                var tags = new Dictionary<string, IReadOnlyList<BuildTag>> {
                    [candidates[1].StableKey] = new[] { new BuildTag("Representation", "High") },
                    [candidates[2].StableKey] = new[] { new BuildTag("Representation", "Low") },
                    [candidates[4].StableKey] = new[] { new BuildTag("Representation", "Excluded") }
                };
                var closures = paths.Select((path, i) => new BuildDependencySnapshot(candidates[i].PhysicalKey, path,
                    AssetDatabase.GetDependencies(path).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal))
                        .Select(p => new BuildDependencyEntry(AssetDatabase.AssetPathToGUID(p), p)))).ToArray();
                var snapshot = new BuildMaterializationSnapshot(candidates, tags,
                    new[] { new BuildContentRequirement("shared-logical", BuildContentCardinality.OneOrMore) }, closures);
                var selection = new BuildTagSelector().Select(new BuildRequest(new[] {
                        new BuildTag("Representation", "High"), new BuildTag("Representation", "Low") }),
                    snapshot.Candidates, snapshot.TagProviders,
                    new BuildSelectionPolicy(new BuildTagSchema(new[] {
                        new KeyValuePair<string, IEnumerable<string>>("Representation", new[] { "High", "Low", "Excluded" })
                    }), snapshot.Requirements));
                Assert.That(selection.IsSuccess, Is.True);
                var artifacts = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "bs2b"));
                var request = new BuildContentRequest(selection.Plan!, snapshot, "fixture", artifacts);
                var result = new BuildContentCoordinator().Build(request);
                Assert.That(result.IsSuccess, Is.True, result.Summary);
                Assert.That(File.Exists(result.ManifestPointer), Is.True);
                Assert.That(Directory.Exists(result.MetadataPath), Is.True);
                Assert.That(File.ReadAllText(result.PreflightPath), Does.Contain(result.Identity));
                Assert.That(File.ReadAllText(result.OutcomePath), Does.Contain(result.Identity));
                _copy = Path.Combine(artifacts, "integration-copy-" + result.Identity);
                Copy(result.ContentPath!, _copy);
            }
            finally
            {
                if (previous.Any(x => x.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (_folder.Length != 0) { AssetDatabase.DeleteAsset(_folder); _folder = ""; }
            }
            yield return new EnterPlayMode();
            var handle = ContentLoadManager.RegisterContentDirectory(_copy);
            Assert.That(handle.IsValid, Is.True);
            try
            {
                var roots = ContentLoadManager.GetRootAssets<BuildContentRoot>(handle);
                Assert.That(roots.Length, Is.EqualTo(1));
                Assert.That(roots[0].BuildIdentity,
                    Is.EqualTo(Path.GetFileName(_copy).Substring("integration-copy-".Length)));
                Assert.That(roots[0].Entries.Count, Is.EqualTo(4));
                Assert.That(roots[0].Entries.Count(x => x.LogicalKey == "shared-logical"), Is.EqualTo(2));
                Assert.That(roots[0].Entries.Where(x => x.LogicalKey == "shared-logical")
                    .Select(x => x.Representation), Is.EquivalentTo(new[] { "High", "Low" }));
                Assert.That(roots[0].Entries.Single(x => x.Kind == BuildContentKind.Scene).SceneId, Is.Not.EqualTo(default(LoadableSceneId)));
                Assert.That(roots[0].Entries.Where(x => x.Kind == BuildContentKind.Object).All(x => x.Object != null), Is.True);
            }
            finally { ContentLoadManager.UnregisterContentDirectory(handle); }
            yield return new ExitPlayMode();
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
