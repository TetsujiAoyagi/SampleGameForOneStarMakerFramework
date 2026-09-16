#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Runtime.BuildContent;
using Unity.Loading;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OneStarMaker.Editor.Build.Content
{
    internal sealed class UnityContentDirectoryAdapter : IContentDirectoryAdapter
    {
        private const string GeneratedRoot = "Assets/OneStarMakerGenerated/BS2b";
        private const string Marker = "bs2b-owner.marker";

        public void ValidateTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                throw new InvalidOperationException("BS2b requires active StandaloneWindows64 Player target.");
        }

        public ContentDirectoryBuild Build(BuildContentProjectionResult projection, string identity, string workspace)
        {
            ValidateTarget();
            CleanupOrphans();
            var folder = GeneratedRoot + "/" + identity;
            EnsureFolder(folder);
            var markerPath = folder + "/" + Marker;
            File.WriteAllText(Path.Combine(Application.dataPath, "OneStarMakerGenerated", "BS2b", identity, Marker), identity);
            AssetDatabase.Refresh();
            try
            {
                var entries = new List<BuildContentEntry>();
                foreach (var item in projection.Roots)
                {
                    if (AssetDatabase.AssetPathToGUID(item.Path) != item.Candidate.PhysicalKey)
                        throw new InvalidOperationException("Root GUID/path changed after materialization: " + item.Path);
                    if (item.Path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    {
                        var sceneId = LoadableSceneIdEditorUtility.CreateLoadableSceneId(item.Path);
                        entries.Add(new BuildContentEntry(item.Candidate.LogicalKey, item.Candidate.StableKey,
                            item.Representation, sceneId));
                    }
                    else
                    {
                        var asset = AssetDatabase.LoadMainAssetAtPath(item.Path);
                        if (asset == null) throw new InvalidOperationException("Root asset missing: " + item.Path);
                        var objectId = LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset);
                        entries.Add(new BuildContentEntry(item.Candidate.LogicalKey, item.Candidate.StableKey,
                            item.Representation, new Loadable<UnityEngine.Object>(objectId)));
                    }
                }
                var root = ScriptableObject.CreateInstance<BuildContentRoot>();
                root.Initialize(identity, BuildContentCoordinator.TargetName, entries);
                var rootPath = folder + "/BuildContentRoot.asset";
                AssetDatabase.CreateAsset(root, rootPath);
                EditorUtility.SetDirty(root);
                AssetDatabase.SaveAssetIfDirty(root);
                AssetDatabase.ImportAsset(rootPath, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<BuildContentRoot>(rootPath);
                if (loaded == null || loaded.BuildIdentity != identity || loaded.Entries.Count != entries.Count)
                    throw new InvalidOperationException("Generated root did not retain its serialized identities.");
                var parameters = new BuildContentDirectoryParameters {
                    outputPath = workspace, rootAssetPaths = new[] { rootPath }, name = identity,
                    compression = BuildCompression.Uncompressed, options = BuildContentOptions.None,
                    extraScriptingDefines = Array.Empty<string>()
                };
                var report = BuildPipeline.BuildContentDirectory(parameters);
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Unity Content BuildReport failed: " + report?.summary.result);
                var manifest = Path.Combine(workspace, "BuildManifestHash.txt");
                if (!File.Exists(manifest)) throw new InvalidOperationException("Unity manifest pointer missing.");
                // Unity owns the report/metadata layout. Keep its location, not its internal file list.
                var metadata = Path.GetFullPath(report.summary.outputPath);
                if (!Directory.Exists(metadata))
                    throw new InvalidOperationException("Unity report output directory missing: " + metadata);
                return new ContentDirectoryBuild(
                    report.summary.result + "|" + report.summary.platform + "|" + report.summary.totalSize,
                    manifest, metadata);
            }
            finally
            {
                if (File.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated", "BS2b", identity, Marker)))
                    AssetDatabase.DeleteAsset(markerPath);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static void CleanupOrphans()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedRoot)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:DefaultAsset", new[] { GeneratedRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path)?.Replace('\\', '/') != GeneratedRoot ||
                    !AssetDatabase.IsValidFolder(path)) continue;
                var marker = path + "/" + Marker;
                var diskPath = Path.Combine(Application.dataPath, "OneStarMakerGenerated", "BS2b",
                    Path.GetFileName(path), Marker);
                if (File.Exists(diskPath) && File.ReadAllText(diskPath) == Path.GetFileName(path))
                    AssetDatabase.DeleteAsset(path);
            }
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
    }
}
