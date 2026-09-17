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
    // Unity Editor API に依存する唯一の BS2b build adapter。
    // 一時的な BuildContentRoot asset を作って Content Directory に含め、build 後は生成元だけを消す。
    internal sealed class UnityContentDirectoryAdapter : IContentDirectoryAdapter
    {
        // この subtree は adapter が所有する一時 asset。既存の Game/Framework asset は消さない。
        private const string GeneratedRoot = "Assets/OneStarMakerGenerated/BS2b";
        private const string Marker = "bs2b-owner.marker";

        public void ValidateTarget()
        {
            // BS2b は固定 target の証拠に限定する。自動切替は Library の状態や後続 build を変えるため行わない。
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                throw new InvalidOperationException("BS2b requires active StandaloneWindows64 Player target.");
        }

        public ContentDirectoryBuild Build(BuildContentProjectionResult projection, string identity, string workspace)
        {
            ValidateTarget();
            // 前回の異常終了で残った所有物だけを掃除する。marker のない folder は触れない。
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
                    // materialization 後に root の GUID/path が変わっていれば、別 asset を build しない。
                    // 依存閉包全体は再探索せず、選択 root の同一性だけを Editor 上で最終確認する。
                    if (AssetDatabase.AssetPathToGUID(item.Path) != item.Candidate.PhysicalKey)
                        throw new InvalidOperationException("Root GUID/path changed after materialization: " + item.Path);
                    if (item.Path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    {
                        // Scene は Unity の loadable scene ID を保存し、path/GUID 自体を Runtime の load ID にしない。
                        var sceneId = LoadableSceneIdEditorUtility.CreateLoadableSceneId(item.Path);
                        entries.Add(new BuildContentEntry(item.Candidate.LogicalKey, item.Candidate.StableKey,
                            item.Representation, sceneId));
                    }
                    else
                    {
                        // Object ID は main asset の local file ID を含む Unity reference から作る。
                        // 汎用サブアセット locator と型検証は BS3 の責務。
                        var asset = AssetDatabase.LoadMainAssetAtPath(item.Path);
                        if (asset == null) throw new InvalidOperationException("Root asset missing: " + item.Path);
                        var objectId = LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset);
                        entries.Add(new BuildContentEntry(item.Candidate.LogicalKey, item.Candidate.StableKey,
                            item.Representation, new Loadable<UnityEngine.Object>(objectId)));
                    }
                }
                var root = ScriptableObject.CreateInstance<BuildContentRoot>();
                // directory に同梱する単一 root。build identity と entry を同じ serialized asset に閉じ込める。
                // BS3 は登録後にこの root を一件だけ取得し、source を再走査せず対応を復元する。
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
                    // rootAssetPaths は生成 root のみ。そこから参照される Scene/Object を Unity が収集する。
                    // workspace は target/contentSet 固定なので、同じ場所への再 build が可能。
                    outputPath = workspace, rootAssetPaths = new[] { rootPath }, name = identity,
                    compression = BuildCompression.Uncompressed, options = BuildContentOptions.None,
                    extraScriptingDefines = Array.Empty<string>()
                };
                var report = BuildPipeline.BuildContentDirectory(parameters);
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Unity Content BuildReport failed: " + report?.summary.result);
                var manifest = Path.Combine(workspace, "BuildManifestHash.txt");
                // Unity の内部 file 名や manifest 内容は OSM の公開 protocol として解釈しない。
                // pointer と metadata directory の存在だけを検証し、directory 一式を coordinator に渡す。
                if (!File.Exists(manifest)) throw new InvalidOperationException("Unity manifest pointer missing.");
                // Unity の previousBuildReportDirectories には content 出力 folder を渡せる。
                // summary.outputPath はその folder を指す保証がないため、検証済み workspace を記録する。
                var metadata = Path.GetFullPath(workspace);
                if (!Directory.Exists(metadata))
                    throw new InvalidOperationException("Unity content output directory missing: " + metadata);
                return new ContentDirectoryBuild(
                    report.summary.result + "|" + report.summary.platform + "|" + report.summary.totalSize,
                    manifest, metadata);
            }
            finally
            {
                // 成否にかかわらず、この実行で生成した asset と空の親 folder だけを削除する。
                // published directory は Assets 外にあり、ここでは触れない。
                if (File.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated", "BS2b", identity, Marker)))
                    AssetDatabase.DeleteAsset(markerPath);
                AssetDatabase.DeleteAsset(folder);
                DeleteIfEmpty(GeneratedRoot);
                DeleteIfEmpty("Assets/OneStarMakerGenerated");
            }
        }

        private static void DeleteIfEmpty(string assetFolder)
        {
            if (!AssetDatabase.IsValidFolder(assetFolder)) return;
            var absolute = Path.Combine(Application.dataPath, assetFolder.Substring("Assets/".Length));
            if (Directory.GetFileSystemEntries(absolute).Length == 0) AssetDatabase.DeleteAsset(assetFolder);
        }

        private static void CleanupOrphans()
        {
            // ownership marker と folder 名が一致する孤児だけを対象にする。
            // 別機能や人が置いた生成物を一括削除しないための制限。
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
            // AssetDatabase を通して .meta を生成し、Unity が認識する一時 root にする。
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
