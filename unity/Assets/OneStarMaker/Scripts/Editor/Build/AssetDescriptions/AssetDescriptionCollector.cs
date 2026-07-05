#nullable enable

using System.Collections.Generic;
using System.Linq;
using OneStarMaker.Runtime.AssetDescriptions;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// 全 Source から AssetDescription を統合収集する。
    /// BuildVariantProfile と IAssetDescriptionSource の組み合わせで走査対象を拡張できる。
    /// </summary>
    public static class AssetDescriptionCollector
    {
        /// <summary>プロジェクト標準の走査元。現状は SceneResourceMap のみ。</summary>
        private static readonly IAssetDescriptionSource[] DefaultSources =
        {
            new SceneResourceMapSource(),
        };

        /// <summary>
        /// 全 Source から IAssetPayloadProvider を重複排除して収集する。
        /// </summary>
        public static IReadOnlyList<CollectedAssetDescription> Collect(
            BuildVariantProfile profile,
            IEnumerable<IAssetDescriptionSource>? additionalSources = null)
        {
            var results = new List<CollectedAssetDescription>();
            var seenProviders = new HashSet<IAssetPayloadProvider>();

            foreach (var source in EnumerateSources(additionalSources))
            {
                foreach (var item in source.Collect(profile))
                {
                    // 同一 Provider が複数 Source から返っても 1 件だけ採用する。
                    if (!seenProviders.Add(item.Provider))
                    {
                        continue;
                    }

                    results.Add(item);
                }
            }

            return results;
        }

        private static IEnumerable<IAssetDescriptionSource> EnumerateSources(
            IEnumerable<IAssetDescriptionSource>? additionalSources)
        {
            foreach (var source in DefaultSources)
            {
                yield return source;
            }

            if (additionalSources == null)
            {
                yield break;
            }

            foreach (var source in additionalSources)
            {
                yield return source;
            }
        }
    }
}
