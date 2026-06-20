#nullable enable

using System.Collections.Generic;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// SceneResourceMap 内の SceneAssetDescription を列挙する。
    /// SceneAssetDescription は SceneResource に埋め込まれるため AssetDatabase 検索では見つからない。
    /// </summary>
    public sealed class SceneResourceMapSource : IAssetDescriptionSource
    {
        /// <inheritdoc />
        public IEnumerable<CollectedAssetDescription> Collect(BuildVariantProfile profile)
        {
            var map = ResolveSceneResourceMap(profile);
            if (map == null)
            {
                yield break;
            }

            foreach (var resource in map.SceneResources)
            {
                if (resource == null)
                {
                    continue;
                }

                var description = resource.SceneAssetDescription;
                if (description == null)
                {
                    continue;
                }

                yield return new CollectedAssetDescription(
                    description,
                    isOptional: false,
                    sourceName: $"SceneResourceMap:{resource.Identity}");
            }
        }

        /// <summary>Profile に設定された SceneResourceMap を返す。</summary>
        internal static SceneResourceMap? ResolveSceneResourceMap(BuildVariantProfile profile)
            => profile.SceneResourceMap;
    }
}
