#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// 体積の再計算がアセットと `.unity` から読む部分（34-ondemand-spatial-policy.md §5）。
    /// 走査規則そのものは <see cref="SceneVolumeRecalculator"/>、合併規則は <see cref="SceneVolumeMath"/>。
    ///
    /// <para><b>名前文法を一切使わない。</b> シーンの所在は payload の GUID で引く。</para>
    /// </summary>
    internal static class SceneVolumeSceneReader
    {
        /// <summary>プロジェクト内の全 <see cref="SceneResource"/> を読み込む。</summary>
        internal static List<SceneResource> LoadAll()
        {
            var guids = AssetDatabase.FindAssets("t:SceneResource");
            var resources = new List<SceneResource>(guids.Length);

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(path);
                // SceneResource は UnityEngine.Object。?. / ?? で素通しさせない。
                if (resource == null)
                {
                    continue;
                }

                resources.Add(resource);
            }

            return resources;
        }

        /// <summary>`.unity` のアセットパスから、それを指す <see cref="SceneResource"/> を探す。</summary>
        internal static SceneResource? FindByScenePath(string scenePath)
        {
            var resources = LoadAll();
            for (var i = 0; i < resources.Count; i++)
            {
                if (TryGetScenePath(resources[i], out var path)
                    && string.Equals(path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    return resources[i];
                }
            }

            return null;
        }

        /// <summary>
        /// そのシーン**だけ**が占める体積を求める。子シーンは含まない。
        /// </summary>
        /// <param name="resource">対象。</param>
        /// <param name="liveScene">既に開いているシーン（保存フック経由）。null なら必要に応じて開閉する。</param>
        internal static Bounds ComputeOwnVolume(SceneResource resource, Scene? liveScene)
        {
            if (liveScene.HasValue && liveScene.Value.IsValid() && liveScene.Value.isLoaded)
            {
                return CollectVolume(liveScene.Value);
            }

            if (!TryGetScenePath(resource, out var scenePath))
            {
                return default;
            }

            var already = SceneManager.GetSceneByPath(scenePath);
            if (already.IsValid() && already.isLoaded)
            {
                // 人が開いているシーンを閉じない。
                return CollectVolume(already);
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                return CollectVolume(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        /// <summary>シーン内の全 Renderer のワールド AABB を合併する。</summary>
        private static Bounds CollectVolume(Scene scene)
        {
            var parts = new List<Bounds>();
            var roots = scene.GetRootGameObjects();

            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                {
                    continue;
                }

                var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
                for (var r = 0; r < renderers.Length; r++)
                {
                    var renderer = renderers[r];
                    if (renderer == null)
                    {
                        continue;
                    }

                    var bounds = renderer.bounds;
                    // 寸法ゼロは未初期化か空メッシュ。合併に混ぜると結果が原点まで伸びる。
                    if (SceneVolumeMath.IsEmpty(bounds))
                    {
                        continue;
                    }

                    parts.Add(bounds);
                }
            }

            return SceneVolumeMath.TryUnion(parts, out var volume) ? volume : default;
        }

        /// <summary>payload の GUID から `.unity` のアセットパスを引く。既定 Variant を優先する。</summary>
        private static bool TryGetScenePath(SceneResource resource, out string scenePath)
        {
            var payloads = resource.GetPayloads();
            var fallback = string.Empty;

            for (var i = 0; i < payloads.Count; i++)
            {
                var payload = payloads[i];
                // AssetPayload / AssetReference は UnityEngine.Object ではないので通常の null 判定。
                if (payload == null || payload.Reference == null)
                {
                    continue;
                }

                var guid = payload.Reference.AssetGUID;
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)
                    || !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(payload.Variant))
                {
                    scenePath = path;
                    return true;
                }

                if (string.IsNullOrEmpty(fallback))
                {
                    fallback = path;
                }
            }

            scenePath = fallback;
            return !string.IsNullOrEmpty(fallback);
        }
    }
}
