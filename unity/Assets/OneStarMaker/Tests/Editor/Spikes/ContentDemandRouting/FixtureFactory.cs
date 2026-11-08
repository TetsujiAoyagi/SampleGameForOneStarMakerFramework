#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>fixture の asset path。Domain Reload を越えて round の状態に持つ。</summary>
    [Serializable]
    internal sealed class FixturePaths
    {
        public string boot = "";
        public string entry = "";
        public string other = "";
        public string dup = "";
        public string prefab = "";
        public string material = "";
        public string buildNext = "";
        public string buildOther = "";
        public string buildDup = "";
    }

    /// <summary>build した 1 つの Content Directory。</summary>
    [Serializable]
    internal sealed class BuiltDirectory
    {
        public string identity = "";
        public string path = "";
        public string summary = "";
        public double elapsedMs;
        public List<string> logicalKeys = new();
    }

    /// <summary>
    /// Game のサンプルを使わない独立した fixture（HANDOFF §1 対象外、program §9）を一時 root に作り、
    /// その Scene から Content Directory を build する。
    /// Scene: `Boot`（Play の空の起点、U-b1）、`Entry`（snapshot の対象）、`Other`（未要求の Scene、AC5）、
    /// `Dup`（同名衝突、AC3）。build 用の写し `buildsrc/` の `Next`・`Other`・`Dup` は由来マーカーを build の値にしてある。
    /// 共有の prefab と material は prefab override（AC2）と参照先の変更（G22、AC16）に使う。
    /// HANDOFF §3 の ScriptableObject は material で代える（Editor asmdef の ScriptableObject 型は build に入らないため）。
    /// </summary>
    internal static class FixtureFactory
    {
        internal const string Representation = "Full";

        internal static FixturePaths Create(TempRootLease lease)
        {
            var folder = lease.EnsureAssetChild("fixture");
            var buildFolder = lease.EnsureAssetChild("buildsrc");
            var paths = new FixturePaths
            {
                boot = folder + "/Boot.unity", entry = folder + "/Entry.unity", other = folder + "/Other.unity",
                dup = folder + "/Dup.unity", prefab = folder + "/Shared.prefab", material = folder + "/Shared.mat",
                buildNext = buildFolder + "/Next.unity", buildOther = buildFolder + "/Other.unity", buildDup = buildFolder + "/Dup.unity",
            };
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No fixture shader is available.");
            AssetDatabase.CreateAsset(new Material(shader), paths.material);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                cube.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(paths.material);
                PrefabUtility.SaveAsPrefabAsset(cube, paths.prefab);
            }
            finally { UnityEngine.Object.DestroyImmediate(cube); }

            SaveScene(paths.boot, null, false);
            SaveScene(paths.entry, OriginMarker.Original, true, paths.prefab);
            SaveScene(paths.other, OriginMarker.Original, false);
            SaveScene(paths.dup, OriginMarker.Original, false);
            SaveScene(paths.buildNext, OriginMarker.FixtureBuild, false);
            SaveScene(paths.buildOther, OriginMarker.FixtureBuild, false);
            SaveScene(paths.buildDup, OriginMarker.FixtureBuild, false);
            EditorSceneManager.OpenScene(paths.boot, OpenSceneMode.Single);
            return paths;
        }

        /// <summary>
        /// 製品の `UnityContentDirectoryAdapter` で directory を build する。選択（BuildPlan）と materialization を通さず、
        /// adapter の入力（projection）を fixture から直接作る。adapter の build 設定（圧縮、type tree を含む `BuildContentOptions`）は
        /// 製品と同じ物を使い、Editor Play で読めるかを Phase C の実行で観測する（N3 の未確認の箇所）。
        /// active build target が StandaloneWindows64 Player でなければ adapter が投げる。呼び出し側は UNTESTED として残す。
        /// </summary>
        internal static BuiltDirectory BuildDirectory(string identity, string workspace, IReadOnlyList<(string LogicalKey, string Path)> scenes)
        {
            var roots = new List<ProjectedContent>();
            var result = new BuiltDirectory { identity = identity, path = Path.GetFullPath(workspace) };
            foreach (var (logicalKey, path) in scenes)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (guid.Length == 0) throw new InvalidOperationException("Build source scene is not imported: " + path);
                var candidate = new BuildContentCandidate(logicalKey + "/" + Representation, logicalKey, guid,
                    new BuildProvenance("cdr-spike", logicalKey));
                roots.Add(new ProjectedContent(candidate, path, Representation, Array.Empty<BuildDependencyEntry>()));
                result.logicalKeys.Add(logicalKey);
            }
            var projection = new BuildContentProjectionResult(roots, Array.Empty<BuildDependencyEntry>(), Array.Empty<BuildContentIssue>());
            Directory.CreateDirectory(result.path);
            var watch = Stopwatch.StartNew();
            var built = new UnityContentDirectoryAdapter().Build(projection, identity, result.path, null);
            result.elapsedMs = watch.Elapsed.TotalMilliseconds;
            result.summary = built.Summary;
            return result;
        }

        internal static void ValidateBuildTarget() => new UnityContentDirectoryAdapter().ValidateTarget();

        internal static string TargetName => BuildContentCoordinator.TargetName;

        private static void SaveScene(string path, string? marker, bool entry, string? prefabPath = null)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (marker != null)
            {
                OriginMarker.Create(marker);
                var payload = new GameObject("Payload");
                payload.transform.localPosition = new Vector3(1f, 2f, 3f);
                if (entry)
                {
                    new GameObject("Removable");
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath!);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    instance.name = "SharedInstance";
                }
            }
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new InvalidOperationException("Fixture scene could not be saved: " + path);
        }

        /// <summary>build 済みの Scene の中から、由来マーカーが付いた fixture の Scene を探す（Play 中の観測用）。</summary>
        internal static List<Scene> LoadedScenesNamed(string name)
        {
            var scenes = new List<Scene>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == name) scenes.Add(scene);
            }
            return scenes;
        }
    }
}
