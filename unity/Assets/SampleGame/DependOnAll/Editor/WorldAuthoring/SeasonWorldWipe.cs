#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OneStarMaker.Editor.SceneGraph;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEditor.AddressableAssets;

namespace SampleGame.DependOnAll.Editor.WorldAuthoring
{
    /// <summary>旧資産の明示 allowlist と、その snapshot に限定した削除。</summary>
    internal static class SeasonWorldWipe
    {
        internal sealed class Asset
        {
            internal Asset(string path, string guid, string identity, IEnumerable<string> dependencies)
            { Path = path; Guid = guid; Identity = identity; Dependencies = dependencies.OrderBy(x => x, StringComparer.Ordinal).ToArray(); }
            internal string Path { get; }
            internal string Guid { get; }
            internal string Identity { get; }
            internal IReadOnlyList<string> Dependencies { get; }
        }

        internal sealed class Manifest
        {
            internal Manifest(IEnumerable<Asset> assets, IEnumerable<string> updates, IEnumerable<string>? references = null)
            {
                var all = assets.OrderBy(a => a.Path, StringComparer.Ordinal).ToArray();
                var updateSet = new HashSet<string>(updates, StringComparer.Ordinal);
                Delete = Array.AsReadOnly(all.Where(a => IsAllowed(a.Path)).ToArray());
                Update = Array.AsReadOnly(all.Where(a => !IsAllowed(a.Path) && updateSet.Contains(a.Path)).ToArray());
                Keep = Array.AsReadOnly(all.Where(a => !IsAllowed(a.Path) && !updateSet.Contains(a.Path)).ToArray());
                References = Array.AsReadOnly((references ?? Array.Empty<string>()).OrderBy(r => r, StringComparer.Ordinal).ToArray());
            }
            internal IReadOnlyList<Asset> Delete { get; }
            internal IReadOnlyList<Asset> Update { get; }
            internal IReadOnlyList<Asset> Keep { get; }
            internal IReadOnlyList<string> References { get; }
            internal string ToText()
            {
                var lines = new List<string> { "S-4b P0/P1 manifest (no mutation)", "path\tguid\tidentity\tdependencies" };
                foreach (var pair in new[] { ("DELETE", Delete), ("UPDATE", Update), ("KEEP", Keep) })
                    foreach (var asset in pair.Item2)
                        lines.Add(pair.Item1 + "\t" + asset.Path + "\t" + asset.Guid + "\t" + asset.Identity
                            + "\t" + string.Join(";", asset.Dependencies) + "\tmeta=" + asset.Path + ".meta");
                lines.AddRange(References);
                return string.Join("\n", lines);
            }
        }

        internal static bool IsAllowed(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Any(char.IsControl) || path.Contains("\\")
                || path.Split('/').Any(p => p == ".." || p == "." || p.Length == 0)) return false;
            if (path.EndsWith(".meta", StringComparison.Ordinal)) path = path.Substring(0, path.Length - 5);
            if (path == SeasonWorldGenerationPlan.OldRoot + "/World.unity"
                || path == SeasonWorldGenerationPlan.OldRoot + "/WorldGridDefinition.asset"
                || path == "Assets/OneStarMakerCommon/SceneMap/World.asset"
                || path == "Assets/SceneGraphData/Nodes/World.asset") return true;
            if (Regex.IsMatch(path, "^Assets/SceneGraphData/Nodes/Cells/(Cell|Environment)_[0-3]_[0-3]\\.asset$")) return true;
            return Regex.IsMatch(path, "^" + Regex.Escape(SeasonWorldGenerationPlan.OldRoot)
                + "/Cells/Cell_[0-3]_[0-3]/(?:[^/]+/)*[^/]+\\.(unity|asset)$");
        }

        internal static Manifest DryRun(IEnumerable<Asset> assets, IEnumerable<string> updates)
            => new(assets, updates);

        internal static Manifest Capture()
        {
            var paths = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)
                && !AssetDatabase.IsValidFolder(p)).ToArray();
            var updates = new HashSet<string>(StringComparer.Ordinal)
            { SeasonWorldGenerationPlan.MapPath, SeasonWorldGenerationPlan.TotalPath, SeasonWorldGenerationPlan.LayoutPath, SeasonWorldGenerationPlan.SessionResourcePath };
            var assets = new List<Asset>();
            var references = new List<string>();
            foreach (var path in paths)
            {
                var identity = string.Empty;
                if (path.EndsWith(".asset", StringComparison.Ordinal))
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(path);
                    if (obj is SceneResource resource)
                    {
                        identity = resource.Identity; updates.Add(path);
                        references.Add("RESOURCE\t" + identity + "\tparent=" + (resource.Parent != null ? resource.Parent.Identity : "")
                            + "\tchildren=" + string.Join(";", resource.Children.Select(c => c != null ? c.Identity : "<null>")));
                    }
                    if (obj is SceneNodeData node) identity = node.Identity;
                    if (obj is SceneGraphEdges graph)
                    {
                        updates.Add(path);
                        references.AddRange(graph.Edges.Select(e => "EDGE\t" + path + "\t"
                            + (e.Parent != null ? e.Parent.Identity : "<null>") + "\t" + (e.Child != null ? e.Child.Identity : "<null>")));
                    }
                    if (obj is SceneResourceMap map)
                    {
                        references.Add("MAP_HASH\t" + new SerializedObject(map).FindProperty("_generateHash").stringValue);
                        references.AddRange(map.SceneResources.Select(r => "MAP\t" + (r != null ? r.Identity : "<null>")));
                    }
                }
                if (path.StartsWith("Assets/AddressableAssetsData/", StringComparison.Ordinal)) updates.Add(path);
                assets.Add(new Asset(path, AssetDatabase.AssetPathToGUID(path), identity,
                    AssetDatabase.GetDependencies(path, false).Where(p => p != path).Select(AssetDatabase.AssetPathToGUID)));
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("Addressables settings missing.");
            foreach (var group in settings.groups)
                if (group != null)
                    references.AddRange(group.entries.Select(e => "ADDRESSABLE\t" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(group))
                        + "\t" + e.guid + "\t" + e.address));
            return new Manifest(assets, updates, references);
        }

        internal static void Execute(Manifest manifest)
        {
            // Validate the complete snapshot before the first mutation. Never recursively delete a directory.
            foreach (var asset in manifest.Delete)
                if (!IsAllowed(asset.Path) || AssetDatabase.IsValidFolder(asset.Path)
                    || AssetDatabase.AssetPathToGUID(asset.Path) != asset.Guid || asset.Guid.Length == 0)
                    throw new InvalidOperationException("Wipe snapshot changed: " + asset.Path);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("Addressables settings missing.");
            var removed = new HashSet<string>(manifest.Delete.Select(a => a.Guid), StringComparer.Ordinal);
            var layout = SeasonWorldGenerationCommand.Required<SceneGraphLayout>(SeasonWorldGenerationPlan.LayoutPath);
            layout.Positions.RemoveAll(p => p.Node != null
                && removed.Contains(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(p.Node))));
            EditorUtility.SetDirty(layout);
            foreach (var guid in AssetDatabase.FindAssets("t:SceneGraphEdges"))
            {
                var graph = AssetDatabase.LoadAssetAtPath<SceneGraphEdges>(AssetDatabase.GUIDToAssetPath(guid));
                if (graph == null) throw new InvalidOperationException("Graph disappeared.");
                foreach (var node in graph.GraphNodes.ToArray())
                    if (node != null && removed.Contains(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(node)))) graph.RemoveNode(node);
                graph.Edges.RemoveAll(e => (e.Parent != null && removed.Contains(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(e.Parent))))
                    || (e.Child != null && removed.Contains(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(e.Child)))));
                EditorUtility.SetDirty(graph);
            }
            foreach (var asset in manifest.Delete)
            {
                settings.RemoveAssetEntry(asset.Guid);
                if (!AssetDatabase.DeleteAsset(asset.Path)) throw new IOException("Wipe failed: " + asset.Path);
            }
            // Resource/Map references are projected from surviving graphs by Command, not silently pruned here.
        }
    }
}
