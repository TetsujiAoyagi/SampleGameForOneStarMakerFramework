#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OneStarMaker.Editor.SceneGraph;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.InGame.World;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace SampleGame.DependOnAll.Editor.WorldAuthoring
{
    /// <summary>S-4b 一回生成の I/O 順序。P1 では呼び出さない。P2 前提を確認した Editor からのみ呼ぶ。</summary>
    public static class SeasonWorldGenerationCommand
    {
        private static string EvidenceRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/s-4b-p2"));

        [MenuItem("OneStarMaker/Sample/S-4b/Dry Run")]
        public static void DryRun()
        {
            var plan = new SeasonWorldGenerationPlan();
            var manifest = SeasonWorldWipe.Capture();
            Directory.CreateDirectory(EvidenceRoot);
            File.WriteAllText(Path.Combine(EvidenceRoot, "dry-run.txt"), manifest.ToText()
                + "\nCREATE\n" + string.Join("\n", plan.NewAssetPaths)
                + "\nMOVE (P2): " + SeasonWorldGenerationPlan.OldMaterialPath + " -> " + SeasonWorldGenerationPlan.MaterialPath);
            Debug.Log($"[S-4b] Dry-run only: delete={manifest.Delete.Count}, resources=440, scenes=652. {EvidenceRoot}");
        }

        /// <summary>Generate は再実行しない。P0 manifest と実ファイルで検査だけやり直す。</summary>
        public static void Revalidate()
        {
            var plan = new SeasonWorldGenerationPlan();
            var before = SeasonWorldWipe.Manifest.FromText(File.ReadAllText(Path.Combine(EvidenceRoot, "p0-manifest.txt")));
            var issues = SeasonWorldValidation.Inspect(plan, before);
            File.WriteAllLines(Path.Combine(EvidenceRoot, "validation.txt"), issues.Count == 0
                ? new[] { "No issues detected by implemented checks." }
                : issues);
            if (issues.Count > 0) throw new InvalidOperationException(string.Join("\n", issues));
        }

        // Deliberately no automatic initialization or menu invocation of Generate.
        public static void Generate(string expectedProjectPath)
        {
            var project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!string.Equals(project.TrimEnd('\\', '/'), Path.GetFullPath(expectedProjectPath).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Wrong Editor project: " + project);
            var plan = new SeasonWorldGenerationPlan();
            Preflight(plan);
            var manifest = SeasonWorldWipe.Capture();
            Directory.CreateDirectory(EvidenceRoot);
            var journal = Path.Combine(EvidenceRoot, "generation.log");
            if (File.Exists(journal)) throw new InvalidOperationException("Prior generation evidence exists. Inspect/recover P1 before another run.");
            File.WriteAllText(Path.Combine(EvidenceRoot, "p0-manifest.txt"), manifest.ToText()
                + "\nCREATE\n" + string.Join("\n", plan.NewAssetPaths));
            var timer = Stopwatch.StartNew();
            var phase = "start";
            var sceneCount = 0;
            void Log(string message)
            {
                var line = $"{DateTime.UtcNow:O}\t{timer.Elapsed.TotalSeconds:F3}s\t{phase}\t{sceneCount}/652\t{message}";
                File.AppendAllText(journal, line + "\n"); Debug.Log("[S-4b] " + line);
            }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var suspended = SceneVolumeRecalculator.SaveHookSuspended;
            SceneVolumeRecalculator.SaveHookSuspended = true;
            try
            {
                Log("begin resources=440 scenes=652 project=" + project);
                // All open scenes are saved and clean by Preflight. Release old scene instances before deleting assets.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                phase = "wipe"; Log("begin"); SeasonWorldWipe.Execute(manifest);
                phase = "material";
                Folder(Path.GetDirectoryName(SeasonWorldGenerationPlan.MaterialPath)!.Replace('\\', '/'));
                var error = AssetDatabase.MoveAsset(SeasonWorldGenerationPlan.OldMaterialPath, SeasonWorldGenerationPlan.MaterialPath);
                if (error.Length != 0) throw new IOException(error);
                var material = Required<Material>(SeasonWorldGenerationPlan.MaterialPath);
                var graph = Required<SceneGraphEdges>(SeasonWorldGenerationPlan.TotalPath);
                var session = Required<SceneNodeData>(SeasonWorldGenerationPlan.SessionNodePath);
                var nodes = new Dictionary<string, SceneNodeData>(StringComparer.Ordinal) { ["InGameSession"] = session };
                phase = "scenes/nodes";
                foreach (var entry in plan.Entries)
                {
                    Log(entry.Identity);
                    if (entry.ScenePath.Length > 0) { CreateScene(entry, false, material); sceneCount++; }
                    if (entry.WhiteboxPath.Length > 0) { CreateScene(entry, true, material); sceneCount++; }
                    var node = ScriptableObject.CreateInstance<SceneNodeData>();
                    node.Identity = entry.Identity; node.NodeLoadType = entry.LoadType;
                    if (entry.ScenePath.Length > 0) node.Payloads.Add(new AssetPayload(string.Empty, new AssetReference(Guid(entry.ScenePath))));
                    if (entry.WhiteboxPath.Length > 0) node.Payloads.Add(new AssetPayload("Whitebox", new AssetReference(Guid(entry.WhiteboxPath))));
                    Folder(Path.GetDirectoryName(entry.NodePath)!.Replace('\\', '/'));
                    AssetDatabase.CreateAsset(node, entry.NodePath);
                    nodes.Add(entry.Identity, node);
                    graph.AddNode(node); graph.AddNode(nodes[entry.Parent]); graph.AddEdge(nodes[entry.Parent], node);
                }
                EditorUtility.SetDirty(graph);
                AssetDatabase.SaveAssets();
                phase = "graph projection"; Log("begin");
                var allNodes = LoadAll<SceneNodeData>();
                var graphs = LoadAll<SceneGraphEdges>();
                if (!SceneResourceGenerator.Generate(allNodes, graphs)) throw new InvalidOperationException("Graph projection failed.");
                foreach (var entry in plan.Entries)
                {
                    var source = "Assets/OneStarMakerCommon/SceneMap/" + entry.Identity + ".asset";
                    Folder(Path.GetDirectoryName(entry.ResourcePath)!.Replace('\\', '/'));
                    error = AssetDatabase.MoveAsset(source, entry.ResourcePath);
                    if (error.Length != 0) throw new IOException(error);
                    var resource = Required<SceneResource>(entry.ResourcePath);
                    var so = new SerializedObject(resource);
                    so.FindProperty("_streamByDistance").boolValue = entry.IsCell;
                    so.FindProperty("_sceneAssetDescription").FindPropertyRelative("SceneIdentity").stringValue = entry.Identity;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Register(entry.ScenePath); Register(entry.WhiteboxPath);
                }
                Register(SeasonWorldGenerationPlan.MaterialPath);
                AssetDatabase.SaveAssets();
                phase = "initial volume bake"; Log("begin");
                SceneVolumeRecalculator.RecalculateAll();
                phase = "validation"; Log("begin (no repair)");
                var issues = SeasonWorldValidation.Inspect(plan, manifest);
                File.WriteAllLines(Path.Combine(EvidenceRoot, "validation.txt"), issues.Count == 0 ? new[] { "No issues detected by implemented checks." } : issues);
                if (issues.Count > 0) throw new InvalidOperationException(string.Join("\n", issues));
                phase = "complete"; Log("resources=440 scenes=652; runtime wiring and human validation pending");
                // Removed scenes cannot be restored; restore only surviving saved scenes.
                var surviving = setup.Where(s => File.Exists(s.path)).ToArray();
                if (surviving.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(surviving);
            }
            catch (Exception ex)
            {
                Log("FAILED; do not rerun; recover complete P1 asset/meta/graph/map/addressables snapshot. " + ex);
                throw;
            }
            finally { SceneVolumeRecalculator.SaveHookSuspended = suspended; }
        }

        private static void Preflight(SeasonWorldGenerationPlan plan)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Editor is not idle.");
            if (WorldCompanionRecoveryJournal.Exists) throw new InvalidOperationException("Resolve Workspace journal first.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty || string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Resolve dirty/untitled scenes first.");
            }
            foreach (var path in plan.NewAssetPaths.Concat(new[] { SeasonWorldGenerationPlan.MaterialPath }))
                if (File.Exists(path) || AssetDatabase.AssetPathToGUID(path).Length > 0)
                    throw new InvalidOperationException("Generation target already exists: " + path);
            var identities = new HashSet<string>(plan.Entries.Select(e => e.Identity), StringComparer.Ordinal);
            if (LoadAll<SceneNodeData>().Any(n => identities.Contains(n.Identity))
                || LoadAll<SceneResource>().Any(r => identities.Contains(r.Identity)))
                throw new InvalidOperationException("Season identity already exists elsewhere.");
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || settings.DefaultGroup == null) throw new InvalidOperationException("Addressables/default group missing.");
            Required<SceneGraphEdges>(SeasonWorldGenerationPlan.TotalPath);
            Required<SceneGraphLayout>(SeasonWorldGenerationPlan.LayoutPath);
            Required<SceneResourceMap>(SeasonWorldGenerationPlan.MapPath);
            Required<SceneNodeData>(SeasonWorldGenerationPlan.SessionNodePath);
            Required<SceneResource>(SeasonWorldGenerationPlan.SessionResourcePath);
            var material = Required<Material>(SeasonWorldGenerationPlan.OldMaterialPath);
            // Inspect the Material object, not URP's Editor metadata subasset (HANDOFF §7.18.4).
            var serializedMaterial = new SerializedObject(material);
            var property = serializedMaterial.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                var asset = property.objectReferenceValue;
                if (asset == null) continue;
                var dependency = AssetDatabase.GetAssetPath(asset);
                if (!(asset is Texture) && !(asset is Shader))
                    throw new InvalidOperationException("Shared Material dependency is not Texture/Shader: " + dependency);
                if (SeasonWorldWipe.IsAllowed(dependency)) throw new InvalidOperationException("Shared dependency would be wiped: " + dependency);
            }
        }

        private static void CreateScene(SeasonWorldGenerationPlan.Entry entry, bool whitebox, Material material)
        {
            var path = whitebox ? entry.WhiteboxPath : entry.ScenePath;
            Folder(Path.GetDirectoryName(path)!.Replace('\\', '/'));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Lightmapping.lightingSettings = null;
            var root = new GameObject(entry.IsCell ? DemoCellScene.AuthoredRootName : entry.IsLighting ? "SeasonLightingRoot" : "EnvironmentRoot");
            SceneManager.MoveGameObjectToScene(root, scene);
            foreach (var shape in SeasonWorldGenerationPlan.Shapes(entry, whitebox))
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = shape.Name; go.transform.SetParent(root.transform, false);
                go.transform.position = shape.Center; go.transform.localScale = shape.Size;
                go.transform.rotation = Quaternion.Euler(0, shape.Yaw, 0);
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                if (!shape.Collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            // HueOffset is a value contract for later Lit runtime wiring. MPBs are not serialized into scenes.
            if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Scene save failed: " + path);
        }

        private static void Register(string path)
        {
            if (path.Length == 0) return;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || settings.DefaultGroup == null) throw new InvalidOperationException("Addressables missing.");
            var entry = settings.CreateOrMoveEntry(Guid(path), settings.DefaultGroup);
            entry.address = path;
            EditorUtility.SetDirty(settings);
        }

        private static string Guid(string path)
        {
            var guid = AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets);
            return guid.Length > 0 ? guid : throw new IOException("Missing GUID: " + path);
        }

        internal static List<T> LoadAll<T>() where T : UnityEngine.Object
            => AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath)
                .Select(Required<T>).ToList();

        internal static T Required<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new FileNotFoundException(path);
            return asset;
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            Folder(parent);
            if (AssetDatabase.CreateFolder(parent, Path.GetFileName(path)).Length == 0) throw new IOException("Cannot create folder: " + path);
        }
    }
}
