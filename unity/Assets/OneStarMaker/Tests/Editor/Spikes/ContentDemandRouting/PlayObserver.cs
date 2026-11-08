#nullable enable

using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>
    /// Play 中の Unity の Scene の観測。`SceneManager` 上の Scene を名前・path・handle・由来マーカーで列挙し、
    /// load / unload の通知を record に残す。unload の通知と `RoutingBackend` の unload の行の組から、
    /// 判定担当が物理 Scene の unload 責任（所有の (b)）を読む。
    /// </summary>
    internal static class PlayObserver
    {
        private static ExperimentRecord? s_record;
        private static Cell? s_cell;

        internal static List<object?> LoadedScenes()
        {
            var scenes = new List<object?>();
            for (var i = 0; i < SceneManager.sceneCount; i++) scenes.Add(Describe(SceneManager.GetSceneAt(i)));
            return scenes;
        }

        internal static Dictionary<string, object?> Describe(Scene scene)
            => new()
            {
                ["name"] = scene.name, ["path"] = scene.path, ["handle"] = scene.handle, ["loaded"] = scene.isLoaded,
                ["markers"] = OriginMarker.ReadAll(scene),
            };

        /// <summary>同名の Scene を全て数え、それぞれのマーカーを返す（N2、AC3）。</summary>
        internal static Dictionary<string, object?> SameName(string name)
        {
            var matches = new List<object?>();
            var loaded = 0;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name != name) continue;
                matches.Add(Describe(scene));
                if (scene.isLoaded) loaded++;
            }
            var byName = SceneManager.GetSceneByName(name);
            return new Dictionary<string, object?>
            {
                ["name"] = name, ["loadedCount"] = loaded, ["scenes"] = matches,
                // 現行の名前の再利用経路（`PerformUnitySceneLoad` の `GetSceneByName`）が返す実体（AC4）。
                ["getSceneByName"] = byName.IsValid() ? Describe(byName) : null,
            };
        }

        internal static void Hook(ExperimentRecord record, Cell cell)
        {
            s_record = record;
            s_cell = cell;
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneUnloaded -= OnUnloaded;
            SceneManager.sceneLoaded += OnLoaded;
            SceneManager.sceneUnloaded += OnUnloaded;
        }

        internal static void Unhook()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneUnloaded -= OnUnloaded;
            s_record = null;
            s_cell = null;
        }

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
            => s_record?.Write(s_cell, "unity.sceneLoaded", ("scene", Describe(scene)), ("mode", mode.ToString()));

        private static void OnUnloaded(Scene scene)
            => s_record?.Write(s_cell, "unity.sceneUnloaded", ("name", scene.name), ("path", scene.path), ("handle", scene.handle));
    }
}
