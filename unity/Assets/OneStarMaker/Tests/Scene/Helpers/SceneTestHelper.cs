#nullable enable

using System.Collections.Generic;
using System.Reflection;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;

namespace OneStarMaker.Tests.SceneSystem.Helpers
{
    /// <summary>
    /// テスト用: ScriptableObject のプライベートフィールドをリフレクションで設定するヘルパー。
    /// </summary>
    internal static class SceneTestHelper
    {
        private static readonly BindingFlags NonPublicInstance =
            BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>
        /// SceneResource を生成する。
        /// </summary>
        public static SceneResource CreateSceneResource(
            string identity,
            LoadType loadType = LoadType.OnDemand,
            SceneResource? parent = null)
        {
            var resource = ScriptableObject.CreateInstance<SceneResource>();
            resource.Identity = identity; // internal set

            if (parent != null)
            {
                resource.Parent = parent; // internal set
            }

            if (loadType != LoadType.OnDemand)
            {
                // SceneAssetDescription を生成して _loadType を設定
                var desc = new SceneAssetDescription();
                var loadTypeField = typeof(SceneAssetDescription)
                    .GetField("_loadType", NonPublicInstance);
                loadTypeField!.SetValue(desc, loadType);

                var descField = typeof(SceneResource)
                    .GetField("_sceneAssetDescription", NonPublicInstance);
                descField!.SetValue(resource, desc);
            }

            return resource;
        }

        /// <summary>
        /// 親 SceneResource に子を追加する（_children リストにリフレクション挿入）。
        /// </summary>
        public static void AddChild(SceneResource parent, SceneResource child)
        {
            child.Parent = parent;

            var childrenField = typeof(SceneResource)
                .GetField("_children", NonPublicInstance);
            var children = (List<SceneResource>)childrenField!.GetValue(parent);
            children.Add(child);
        }

        /// <summary>
        /// SceneResourceMap を生成し、指定した SceneResource を登録する。
        /// </summary>
        public static SceneResourceMap CreateSceneResourceMap(params SceneResource[] resources)
        {
            var map = ScriptableObject.CreateInstance<SceneResourceMap>();

            var dict = new Dictionary<string, SceneResource>(resources.Length);
            foreach (var r in resources)
            {
                dict[r.Identity] = r;
            }

            var dictField = typeof(SceneResourceMap)
                .GetField("_dictionary", NonPublicInstance);
            dictField!.SetValue(map, dict);

            return map;
        }
    }
}
