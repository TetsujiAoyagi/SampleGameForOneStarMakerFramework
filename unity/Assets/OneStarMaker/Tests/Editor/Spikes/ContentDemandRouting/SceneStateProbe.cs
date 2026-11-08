#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>元の Scene 1 つの N1 の観測項目（HANDOFF §2 N1）。JsonUtility で Domain Reload を越えて持ち運ぶ。</summary>
    [Serializable]
    internal sealed class SceneState
    {
        public string key = "";
        public string path = "";
        public string guid = "";
        public bool fileExists;
        public string sha256 = "";
        public long mtimeUtcTicks;
        public bool isLoaded;
        public bool isDirty;
        public int rootCount;
        // Hierarchy の GameObject 名・数と Inspector の値の固定集合。1 要素が 1 GameObject。
        public List<string> hierarchy = new();
    }

    /// <summary>Editor の Scene setup（開いている Scene の集合と順序、active Scene）。</summary>
    [Serializable]
    internal sealed class EditorSetupState
    {
        public List<string> scenes = new();
        public string activeScene = "";
        public List<SceneState> sceneStates = new();
    }

    /// <summary>
    /// N1 の観測と比較。比較は差のあった項目名を返すだけで、cell の値を決めない。
    /// 未保存の新規 Scene（path が空）は setup の中の順番で識別する。
    /// </summary>
    internal static class SceneStateProbe
    {
        internal static EditorSetupState CaptureSetup()
        {
            var setup = new EditorSetupState();
            var active = SceneManager.GetActiveScene();
            setup.activeScene = KeyOf(active, -1);
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                var key = KeyOf(scene, i);
                setup.scenes.Add(key + "|loaded=" + scene.isLoaded);
                setup.sceneStates.Add(Capture(scene, key));
            }
            return setup;
        }

        /// <summary>開いていない Scene の file 側の項目だけを採る（参照先 prefab など）。</summary>
        internal static SceneState CaptureFile(string assetPath)
        {
            var state = new SceneState { key = assetPath, path = assetPath, guid = AssetDatabase.AssetPathToGUID(assetPath) };
            FillFile(state);
            return state;
        }

        internal static List<string> Compare(EditorSetupState before, EditorSetupState after)
        {
            var differences = new List<string>();
            if (before.activeScene != after.activeScene) differences.Add("setup.activeScene");
            if (!SameSequence(before.scenes, after.scenes)) differences.Add("setup.scenes");
            foreach (var expected in before.sceneStates)
            {
                var actual = after.sceneStates.Find(x => x.key == expected.key);
                if (actual == null) { differences.Add(expected.key + ".missing"); continue; }
                differences.AddRange(Compare(expected, actual));
            }
            return differences;
        }

        internal static List<string> Compare(SceneState before, SceneState after)
        {
            var differences = new List<string>();
            void Check(bool same, string field) { if (!same) differences.Add(before.key + "." + field); }
            Check(before.path == after.path, "path");
            Check(before.guid == after.guid, "guid");
            Check(before.fileExists == after.fileExists, "fileExists");
            Check(before.sha256 == after.sha256, "sha256");
            Check(before.mtimeUtcTicks == after.mtimeUtcTicks, "mtime");
            Check(before.isLoaded == after.isLoaded, "isLoaded");
            Check(before.isDirty == after.isDirty, "isDirty");
            Check(before.rootCount == after.rootCount, "rootCount");
            Check(SameSequence(before.hierarchy, after.hierarchy), "hierarchy");
            return differences;
        }

        internal static string Sha256OfFile(string fullPath)
        {
            using var stream = File.OpenRead(fullPath);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        internal static string KeyOf(Scene scene, int index)
            => scene.path.Length != 0 ? scene.path : "untitled#" + index.ToString(CultureInfo.InvariantCulture);

        private static SceneState Capture(Scene scene, string key)
        {
            var state = new SceneState
            {
                key = key, path = scene.path, isLoaded = scene.isLoaded, isDirty = scene.isDirty,
                guid = scene.path.Length != 0 ? AssetDatabase.AssetPathToGUID(scene.path) : string.Empty,
            };
            FillFile(state);
            if (!scene.isLoaded) return state;
            var roots = scene.GetRootGameObjects();
            state.rootCount = roots.Length;
            foreach (var root in roots) Walk(root.transform, root.name, state.hierarchy);
            return state;
        }

        private static void FillFile(SceneState state)
        {
            if (state.path.Length == 0) return;
            var full = TempRootLease.FullPath(state.path);
            state.fileExists = File.Exists(full);
            if (!state.fileExists) return;
            state.sha256 = Sha256OfFile(full);
            state.mtimeUtcTicks = File.GetLastWriteTimeUtc(full).Ticks;
        }

        private static void Walk(Transform transform, string path, List<string> output)
        {
            var go = transform.gameObject;
            var components = go.GetComponents<Component>();
            var names = new List<string>(components.Length);
            foreach (var component in components) names.Add(component == null ? "<missing>" : component.GetType().Name);
            var prefab = PrefabUtility.IsPartOfPrefabInstance(go)
                ? "prefab(overrides=" + PrefabUtility.GetPropertyModifications(go)?.Length.ToString(CultureInfo.InvariantCulture) + ")"
                : "-";
            output.Add(string.Join("|", path, go.activeSelf ? "on" : "off", Vec(transform.localPosition), Vec(transform.localScale),
                prefab, string.Join(",", names)));
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                Walk(child, path + "/" + child.name, output);
            }
        }

        private static string Vec(Vector3 value)
            => value.x.ToString("F4", CultureInfo.InvariantCulture) + "," + value.y.ToString("F4", CultureInfo.InvariantCulture) + ","
               + value.z.ToString("F4", CultureInfo.InvariantCulture);

        private static bool SameSequence(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++) if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
            return true;
        }

        internal static Scene FindOpen(string path)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.path == path) return scene;
            }
            return default;
        }
    }
}
