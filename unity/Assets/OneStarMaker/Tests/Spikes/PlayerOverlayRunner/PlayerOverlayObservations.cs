#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OneStarMaker.Tests.Spikes.PlayerOverlayRunner
{
    /// <summary>
    /// 生の値の読み取り（HANDOFF §3）。runtime の API だけで書き、期待値を持たず、判定をしない（版・正しい class・壊れたの判定は
    /// Editor 側の判定の写像が record から決める）。S2-1 の round の本体から、読み取りの部分を写した。
    /// Unity の偽 null を見落とさないよう、UnityEngine.Object は == null で判定する（AGENTS.md）。
    /// probe の型は参照せず、Component と完全名の文字列だけで扱う（P4 の答えを変えない。HANDOFF §4 の禁止）。
    /// </summary>
    internal static class PlayerOverlayObservations
    {
        public const string Missing = "Missing", Null = "null";

        public static string Id(Object? o) => o == null ? Null : EntityId.ToULong(o.GetEntityId()).ToString();

        /// <summary>texture の全 pixel を RGBA の順に並べた bytes の sha256。読めなければ "unreadable: 例外の型"。</summary>
        public static string Pixels(Texture2D? tex)
        {
            if (tex == null) return Null;
            try
            {
                var pixels = tex.GetPixels32();
                var bytes = new byte[pixels.Length * 4];
                for (var i = 0; i < pixels.Length; i++)
                {
                    bytes[i * 4] = pixels[i].r; bytes[i * 4 + 1] = pixels[i].g; bytes[i * 4 + 2] = pixels[i].b; bytes[i * 4 + 3] = pixels[i].a;
                }
                using var sha = SHA256.Create();
                return string.Concat(sha.ComputeHash(bytes).Select(x => x.ToString("x2")));
            }
            catch (Exception ex) { return "unreadable: " + ex.GetType().Name; }
        }

        /// <summary>
        /// GetComponents&lt;Component&gt;() の並びのまま。解決できない script の component（== null）は "Missing"、それ以外は完全名で、
        /// MonoBehaviour だけ tab の後に JsonUtility.ToJson の値を付ける（HANDOFF §2 の (f) と「正しい class」）。
        /// </summary>
        public static string[] Components(GameObject? go) =>
            go == null ? Array.Empty<string>() : go.GetComponents<Component>().Select(c => c == null
                ? Missing
                : (c.GetType().FullName ?? "") + (c is MonoBehaviour ? "\t" + JsonUtility.ToJson(c) : "")).ToArray();

        /// <summary>名前と型の種類（Texture2D・Material・Shader・GameObject）で列挙した物の EntityId（HANDOFF §2 の「実体の数」の照合）。</summary>
        public static string[] Enumerate(string kind, string name)
        {
            IEnumerable<Object> all = kind switch
            {
                "Shader" => Resources.FindObjectsOfTypeAll<Shader>(),
                "GameObject" => Resources.FindObjectsOfTypeAll<GameObject>(),
                "Material" => Resources.FindObjectsOfTypeAll<Material>(),
                _ => Resources.FindObjectsOfTypeAll<Texture2D>(),
            };
            return all.Where(x => x != null && x.name == name).Select(Id).ToArray();
        }

        /// <summary>名前ごとの、全ての Object の数（P0 の (2)、round の開始の時の fixture の名前の物）。</summary>
        public static string[] CountByName(IEnumerable<string> names)
        {
            var all = Resources.FindObjectsOfTypeAll<Object>().Where(x => x != null).GroupBy(x => x.name).ToDictionary(x => x.Key, x => x.Count());
            return names.Select(n => n + "=" + (all.TryGetValue(n, out var c) ? c : 0)).ToArray();
        }

        public static string[] LoadedScenes() =>
            Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(x => x.isLoaded).Select(x => x.name).ToArray();

        // 参考（P0 の成否には使わない）: Scene に属する root の GameObject（DontDestroyOnLoad の物を含む）の名前。
        public static string[] RootObjects() =>
            Resources.FindObjectsOfTypeAll<GameObject>().Where(x => x != null && x.transform.parent == null && x.scene.IsValid())
                .Select(x => x.scene.name + "/" + x.name).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        public static GameObject? Root(Scene scene, Func<string, bool> name) =>
            scene.IsValid() && scene.isLoaded ? scene.GetRootGameObjects().FirstOrDefault(x => x != null && name(x.name)) : null;

        public static string[] RootNames(Scene scene) => scene.IsValid() && scene.isLoaded ? scene.GetRootGameObjects().Where(x => x != null).Select(x => x.name).ToArray() : Array.Empty<string>();

        /// <summary>
        /// 参照をたどって、物ごとに EntityId を集める。slot の定義（fixture の事実の file の名前）で、Renderer の i 番目の material を
        /// その slot の物に、その mainTexture と shader を対応する物に帰属させる。参照が == null なら "null" を数える。
        /// </summary>
        public static void Reach(GameObject? go, IReadOnlyDictionary<string, string[]> slots, IReadOnlyDictionary<string, string> textureOf, string shaderItem, IDictionary<string, List<string>> reached)
        {
            if (go == null || !slots.TryGetValue(go.name, out var slotItems)) return;
            void Add(string item, Object? o) { if (reached.TryGetValue(item, out var list)) list.Add(Id(o)); }
            var renderer = go.GetComponent<Renderer>();
            var materials = renderer == null ? Array.Empty<Material>() : renderer.sharedMaterials;
            for (var i = 0; i < slotItems.Length; i++)
            {
                var m = i < materials.Length ? materials[i] : null;
                Add(slotItems[i], m);
                if (textureOf.TryGetValue(slotItems[i], out var texItem)) Add(texItem, m == null ? null : m.mainTexture);
                Add(shaderItem, m == null ? null : m.shader);
            }
        }

        /// <summary>
        /// 保持している GameObject の生の状態（HANDOFF §2 の「壊れた」の材料）。material は "material|mainTexture|shader" の EntityId、
        /// texture は "EntityId|pixel の sha256"。比べるのは判定の写像で、ここは読むだけ。
        /// </summary>
        public static (string objectId, bool renderer, string[] materials, string[] textures, string[] components, string name) Held(GameObject? go)
        {
            if (go == null) return (Null, false, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), "");
            var renderer = go.GetComponent<Renderer>();
            var materials = renderer == null ? Array.Empty<Material>() : renderer.sharedMaterials;
            var m = materials.Select(x => x == null ? Null + "|" + Null + "|" + Null : Id(x) + "|" + Id(x.mainTexture) + "|" + Id(x.shader)).ToArray();
            var t = materials.Select(x => x == null ? null : x.mainTexture as Texture2D).Select(x => Id(x) + "|" + Pixels(x)).ToArray();
            return (Id(go), renderer != null, m, t, Components(go), go.name);
        }

        public static string[] HeldTexture(Texture2D? tex) => new[] { Id(tex) + "|" + Pixels(tex) };
    }
}
