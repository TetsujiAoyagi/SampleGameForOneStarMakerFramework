#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OneStarMaker.Editor.SceneGraph;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.DependOnAll.Editor.Streaming.Cells.Planning;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SampleGame.DependOnAll.Editor.WorldAuthoring
{
    /// <summary>値の照合と Editor 読者。検査は保存値を修復・再計算しない。</summary>
    internal static class SeasonWorldValidation
    {
        internal sealed class Observation
        {
            internal string Identity = string.Empty;
            internal string Parent = string.Empty;
            internal string ResourcePath = string.Empty;
            internal string ResourceGuid = string.Empty;
            internal LoadType LoadType;
            internal bool StreamByDistance;
            internal bool Generated = true;
            internal readonly List<(string variant, string path, string guid, bool addressable)> Payloads = new();
        }

        internal static List<string> Compare(SeasonWorldGenerationPlan plan, IReadOnlyList<Observation> actual)
        {
            var issues = new List<string>();
            var expected = plan.Entries.ToDictionary(e => e.Identity, StringComparer.Ordinal);
            if (actual.Count != 440) issues.Add("Resource count: expected 440, actual " + actual.Count);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var assetGuids = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var scenes = 0;
            foreach (var item in actual)
            {
                if (!identities.Add(item.Identity)) issues.Add("Duplicate identity: " + item.Identity);
                if (!expected.TryGetValue(item.Identity, out var entry)) { issues.Add("Unexpected identity: " + item.Identity); continue; }
                if (item.ResourceGuid.Length == 0 || !assetGuids.Add(item.ResourceGuid)) issues.Add("Missing/duplicate resource GUID: " + item.Identity);
                if (item.ResourcePath != entry.ResourcePath) issues.Add("Resource path: " + item.Identity);
                if (item.Parent != entry.Parent) issues.Add("Parent: " + item.Identity);
                if (item.LoadType != entry.LoadType || item.StreamByDistance != entry.IsCell) issues.Add("Load policy: " + item.Identity);
                if ((entry.IsCell || (!entry.IsLighting && entry.X >= 0)) && !item.Generated)
                    issues.Add("Not Generated: " + item.Identity);
                var expectedPayloads = new Dictionary<string, string>(StringComparer.Ordinal);
                if (entry.ScenePath.Length > 0) expectedPayloads.Add(string.Empty, entry.ScenePath);
                if (entry.WhiteboxPath.Length > 0) expectedPayloads.Add("Whitebox", entry.WhiteboxPath);
                if (item.Payloads.Count != expectedPayloads.Count) issues.Add("Payload count: " + item.Identity);
                var variants = new HashSet<string>(StringComparer.Ordinal);
                foreach (var payload in item.Payloads)
                {
                    scenes++;
                    if (!variants.Add(payload.variant) || !expectedPayloads.TryGetValue(payload.variant, out var path) || path != payload.path)
                        issues.Add("Payload variant/path: " + item.Identity);
                    if (payload.guid.Length == 0 || !assetGuids.Add(payload.guid)) issues.Add("Missing/duplicate payload GUID: " + item.Identity);
                    if (!paths.Add(payload.path)) issues.Add("Duplicate payload path: " + payload.path);
                    if (!payload.addressable) issues.Add("Missing Addressable: " + payload.path);
                }
            }
            foreach (var missing in expected.Keys.Except(identities)) issues.Add("Missing identity: " + missing);
            if (scenes != 652) issues.Add("Scene payload count: expected 652, actual " + scenes);
            return issues;
        }

        internal static bool Finite(Bounds b)
            => Finite(b.center.x) && Finite(b.center.y) && Finite(b.center.z)
                && Finite(b.size.x) && Finite(b.size.y) && Finite(b.size.z)
                && b.size.x > 0 && b.size.y > 0 && b.size.z > 0
                && (double)b.size.x * b.size.y * b.size.z > 1;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static bool Contains(Bounds outer, Bounds inner, float tolerance)
        {
            if (!Finite(outer) || !Finite(inner)) return false;
            for (var axis = 0; axis < 3; axis++)
                if (inner.min[axis] < outer.min[axis] - tolerance || inner.max[axis] > outer.max[axis] + tolerance) return false;
            return true;
        }

        internal static List<string> CompareVolume(int x, int y, Bounds full, Bounds whitebox, Bounds environment, Bounds saved)
        {
            var issues = new List<string>();
            if (!Finite(full) || !Finite(whitebox)) issues.Add("Cell own volume must be finite and >1 m3");
            var grid = new Bounds(new Vector3(x * 250 + 125, 48, y * 250 + 125), new Vector3(250, 96, 250));
            if (!Contains(grid, full, 5) || !Contains(grid, whitebox, 5)) issues.Add("Renderer exceeds grid by >5m");
            if (!SceneVolumeMath.IsEmpty(environment) && !Contains(grid, environment, 5)) issues.Add("Environment exceeds grid by >5m");
            if (!Contains(full, whitebox, 0.001f)) issues.Add("Whitebox exceeds Full own volume");
            var merged = SceneVolumeMath.Merge(full, new[] { (environment, false) });
            if (!Contains(merged, saved, 0.001f) || !Contains(saved, merged, 0.001f)) issues.Add("Saved volume differs from Full + Environment");
            return issues;
        }

        internal static bool SameSeasonSize(Bounds a, Bounds b)
            => Finite(a) && Finite(b) && Mathf.Abs(a.size.x - b.size.x) <= 2
                && Mathf.Abs(a.size.y - b.size.y) <= 2 && Mathf.Abs(a.size.z - b.size.z) <= 2;

        internal static List<string> Inspect(SeasonWorldGenerationPlan plan, SeasonWorldWipe.Manifest before)
        {
            var issues = new List<string>();
            var resources = SeasonWorldGenerationCommand.LoadAll<SceneResource>();
            var nodes = SeasonWorldGenerationCommand.LoadAll<SceneNodeData>();
            var graphs = SeasonWorldGenerationCommand.LoadAll<SceneGraphEdges>();
            var map = SeasonWorldGenerationCommand.Required<SceneResourceMap>(SeasonWorldGenerationPlan.MapPath);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("Addressables missing.");
            var observations = new List<Observation>();
            var ids = new HashSet<string>(plan.Entries.Select(e => e.Identity), StringComparer.Ordinal);
            foreach (var resource in resources)
            {
                var path = AssetDatabase.GetAssetPath(resource);
                if (!ids.Contains(resource.Identity) && !path.StartsWith(SeasonWorldGenerationPlan.Root + "/", StringComparison.Ordinal)) continue;
                var observation = new Observation
                {
                    Identity = resource.Identity, ResourcePath = path, ResourceGuid = AssetDatabase.AssetPathToGUID(path),
                    Parent = resource.Parent != null ? resource.Parent.Identity : string.Empty,
                    LoadType = resource.LoadType, StreamByDistance = resource.StreamByDistance,
                    Generated = CellAuthoringPolicy.Resolve(resource.Identity) == CellAuthoringPolicyKind.Generated,
                };
                foreach (var payload in resource.GetPayloads())
                {
                    if (payload == null || payload.Reference == null) { issues.Add("Null payload: " + resource.Identity); continue; }
                    var guid = payload.Reference.AssetGUID;
                    observation.Payloads.Add((payload.Variant, AssetDatabase.GUIDToAssetPath(guid), guid, settings.FindAssetEntry(guid) != null));
                }
                observations.Add(observation);
            }
            issues.AddRange(Compare(plan, observations));
            var expectedScenes = new HashSet<string>(plan.Entries.SelectMany(e => new[] { e.ScenePath, e.WhiteboxPath }).Where(p => p.Length > 0));
            var actualScenes = new HashSet<string>(AssetDatabase.FindAssets("t:SceneAsset", new[] { SeasonWorldGenerationPlan.Root }).Select(AssetDatabase.GUIDToAssetPath));
            foreach (var path in expectedScenes.Except(actualScenes).Concat(actualScenes.Except(expectedScenes))) issues.Add("Scene set difference: " + path);
            var serializedMap = new SerializedObject(map);
            if (serializedMap.FindProperty("_generateHash").stringValue != SceneResourceGenerator.ComputeCurrentHash(nodes, graphs)) issues.Add("Map hash mismatch");
            var mapResources = map.SceneResources.ToArray();
            if (mapResources.Any(r => r == null) || mapResources.Where(r => r != null).Select(r => r.Identity).Distinct().Count() != mapResources.Length)
                issues.Add("Map null/duplicate identity");
            if (!new HashSet<string>(resources.Select(r => r.Identity)).SetEquals(mapResources.Where(r => r != null).Select(r => r.Identity))) issues.Add("Map membership mismatch");
            foreach (var result in SceneGraphValidator.ValidateAll(nodes, graphs))
                if (result.Severity == SceneGraphValidator.Severity.Error) issues.Add(result.ToString());
            foreach (var resource in resources)
            {
                if (resource.Parent != null && resource.Parent.Children.Count(c => c == resource) != 1) issues.Add("Parent reverse link: " + resource.Identity);
                if (resource.Children.Any(c => c == null || c.Parent != resource)) issues.Add("Child reverse link: " + resource.Identity);
                var visited = new HashSet<SceneResource>();
                for (var parent = resource; parent != null; parent = parent.Parent)
                    if (!visited.Add(parent)) { issues.Add("Resource cycle: " + resource.Identity); break; }
            }
            foreach (var entry in plan.Entries)
            {
                var matching = nodes.Where(n => n.Identity == entry.Identity).ToArray();
                if (matching.Length != 1) { issues.Add("Node count: " + entry.Identity); continue; }
                var node = matching[0];
                if (AssetDatabase.GetAssetPath(node) != entry.NodePath || node.NodeLoadType != entry.LoadType) issues.Add("Node path/policy: " + entry.Identity);
                var edges = graphs.SelectMany(g => g.Edges).Where(e => e.Child == node).ToArray();
                if (edges.Length != 1 || edges[0].Parent == null || edges[0].Parent!.Identity != entry.Parent) issues.Add("Graph parent: " + entry.Identity);
                var resource = resources.FirstOrDefault(r => r.Identity == entry.Identity);
                if (resource == null) continue;
                if (node.Payloads.Any(p => p == null || p.Reference == null)) { issues.Add("Null node payload: " + entry.Identity); continue; }
                var nodePayloads = node.Payloads.Select(p => p.Variant + ":" + p.Reference.AssetGUID).OrderBy(p => p);
                var resourcePayloads = resource.GetPayloads().Where(p => p != null && p.Reference != null).Select(p => p.Variant + ":" + p.Reference.AssetGUID).OrderBy(p => p);
                if (!nodePayloads.SequenceEqual(resourcePayloads)) issues.Add("Graph payload projection: " + entry.Identity);
            }
            var removedIds = new HashSet<string>(before.Delete.Where(a => a.Identity.Length > 0).Select(a => a.Identity));
            foreach (var old in before.Delete)
            {
                var remaining = AssetDatabase.GUIDToAssetPath(old.Guid);
                // DeleteAsset can leave a Library GUID map; require a real file or Addressable entry.
                if ((remaining.Length > 0 && File.Exists(remaining)) || settings.FindAssetEntry(old.Guid) != null)
                    issues.Add("Old GUID remains: " + old.Path);
            }
            // Missing GUIDs can disappear from AssetDatabase.GetDependencies; inspect serialized references too.
            var removedGuids = new HashSet<string>(before.Delete.Select(a => a.Guid), StringComparer.Ordinal);
            foreach (var path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)
                && (p.EndsWith(".asset") || p.EndsWith(".unity") || p.EndsWith(".prefab"))))
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(path), @"guid: ([0-9a-f]{32})"))
                    if (removedGuids.Contains(match.Groups[1].Value)) issues.Add("Dangling old GUID in " + path + ": " + match.Groups[1].Value);
            if (resources.Any(r => removedIds.Contains(r.Identity)) || nodes.Any(n => removedIds.Contains(n.Identity))) issues.Add("Old identity remains");
            foreach (var kept in before.Keep.Concat(before.Update))
            {
                var path = kept.Path == SeasonWorldGenerationPlan.OldMaterialPath ? SeasonWorldGenerationPlan.MaterialPath : kept.Path;
                if (AssetDatabase.AssetPathToGUID(path) != kept.Guid) issues.Add("Preserved GUID changed: " + path);
            }
            foreach (var kept in before.Keep)
            {
                var path = kept.Path == SeasonWorldGenerationPlan.OldMaterialPath ? SeasonWorldGenerationPlan.MaterialPath : kept.Path;
                var dependencies = AssetDatabase.GetDependencies(path, false).Where(p => p != path).Select(AssetDatabase.AssetPathToGUID);
                if (!new HashSet<string>(kept.Dependencies).SetEquals(dependencies)) issues.Add("Preserved references changed: " + path);
            }
            var own = new Dictionary<string, Bounds>();
            foreach (var entry in plan.Entries.Where(e => e.ScenePath.Length > 0)) own[entry.Identity] = ReadVolume(entry.ScenePath, entry.IsLighting, issues);
            var sizes = new Dictionary<(int, int), Bounds>();
            foreach (var entry in plan.Entries.Where(e => e.IsCell))
            {
                var resource = resources.FirstOrDefault(r => r.Identity == entry.Identity);
                if (resource == null) continue;
                var full = own[entry.Identity];
                var env = plan.Entries.Single(e => e.Parent == entry.Identity);
                var whitebox = ReadVolume(entry.WhiteboxPath, false, issues);
                foreach (var issue in CompareVolume(entry.X, entry.Y, full, whitebox, own[env.Identity], resource.Volume)) issues.Add(entry.Identity + ": " + issue);
                if (sizes.TryGetValue((entry.X, entry.Y), out var prior) && !SameSeasonSize(prior, resource.Volume)) issues.Add("Season AABB size >2m: " + entry.Identity);
                sizes[(entry.X, entry.Y)] = resource.Volume;
            }
            return issues;
        }

        internal static bool SceneTextAssignsLightingSettings(string sceneText)
        {
            if (sceneText == null) throw new ArgumentNullException(nameof(sceneText));
            var match = Regex.Match(sceneText, @"(?m)^\s*m_LightingSettings:\s*\{([^}]*)\}");
            if (!match.Success) return false;
            var body = match.Groups[1].Value;
            if (Regex.IsMatch(body, @"\bguid:\s*[0-9a-fA-F]{32}\b")) return true;
            var fileId = Regex.Match(body, @"\bfileID:\s*(-?\d+)\b");
            return fileId.Success && fileId.Groups[1].Value != "0";
        }

        private static Bounds ReadVolume(string path, bool lighting, List<string> issues)
        {
            // Match SceneVolumeSceneReader: inspect the real scene additively and ignore empty bounds.
            var scene = SceneManager.GetSceneByPath(path);
            var openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var renderers = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).ToArray();
                if (lighting && (roots.Length != 1 || renderers.Length != 0
                    || roots.Any(r => r.GetComponentsInChildren<Component>(true).Any(c => c != null && !(c is Transform)))
                    || SceneTextAssignsLightingSettings(File.ReadAllText(path))))
                    issues.Add("Lighting is not empty scaffold: " + path);
                return SceneVolumeMath.TryUnion(renderers.Where(r => r != null)
                    .Select(r => r.bounds).Where(b => !SceneVolumeMath.IsEmpty(b)).ToArray(), out var volume)
                    ? volume : default;
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }
    }
}
