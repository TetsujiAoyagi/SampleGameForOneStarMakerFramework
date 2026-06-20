#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.AssetDescriptions;
using UnityEngine;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// Variant ホワイトリスト適用結果。
    /// Addressables グループ同期と Build Report の入力になる。
    /// </summary>
    public sealed class VariantWhitelistBuildResult
    {
        /// <summary>Addressables catalog に残す GUID 集合。</summary>
        public HashSet<string> IncludedGuids { get; } = new(StringComparer.Ordinal);

        /// <summary>AssetDescription 管理下で catalog から一時除外する GUID 集合。</summary>
        public HashSet<string> ExcludedGuids { get; } = new(StringComparer.Ordinal);

        /// <summary>Collector が列挙した Payload / AlwaysIncluded 由来の GUID 全体。</summary>
        public HashSet<string> ManagedGuids { get; } = new(StringComparer.Ordinal);

        /// <summary>必須 Description が 0 件同梱など、ビルド中断すべきエラー。</summary>
        public List<string> Errors { get; } = new();

        /// <summary>空 GUID など、ビルド継続可能な警告。</summary>
        public List<string> Warnings { get; } = new();

        /// <summary>同梱された Payload の識別子（SourceName:Variant:GUID）。</summary>
        public List<string> IncludedVariantLabels { get; } = new();

        /// <summary>whitelist 不一致で除外された Payload の識別子。</summary>
        public List<string> ExcludedVariantLabels { get; } = new();

        public bool HasErrors => Errors.Count > 0;
    }

    /// <summary>
    /// BuildVariantProfile の Variant ホワイトリストを GUID 集合へ変換する。
    /// ソース .asset 上の Payload は変更せず、Addressables ビルド対象だけを決める。
    /// </summary>
    public static class VariantWhitelistBuilder
    {
        /// <summary>
        /// Profile と Collector 結果から whitelist / excluded 集合を構築する。
        /// </summary>
        /// <param name="profile">Variant ホワイトリストと走査対象の設定。</param>
        /// <param name="additionalSources">テストや拡張用の追加走査元。null 可。</param>
        public static VariantWhitelistBuildResult Build(
            BuildVariantProfile profile,
            IEnumerable<IAssetDescriptionSource>? additionalSources = null)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var result = new VariantWhitelistBuildResult();
            var whitelist = ResolveVariantWhitelist(profile.VariantWhitelist);
            var descriptions = AssetDescriptionCollector.Collect(profile, additionalSources);

            foreach (var item in descriptions)
            {
                var includedCount = 0;

                foreach (var payload in item.Provider.Payloads)
                {
                    if (payload?.Reference == null ||
                        string.IsNullOrEmpty(payload.Reference.AssetGUID))
                    {
                        result.Warnings.Add(
                            $"{item.SourceName}: empty AssetReference (Variant='{payload?.Variant ?? string.Empty}').");
                        continue;
                    }

                    var guid = payload.Reference.AssetGUID;
                    result.ManagedGuids.Add(guid);

                    // Variant 名は完全一致のみ。Framework は名前の意味を解釈しない。
                    if (whitelist.Contains(payload.Variant))
                    {
                        result.IncludedGuids.Add(guid);
                        result.IncludedVariantLabels.Add(
                            $"{item.SourceName}:{payload.Variant}:{guid}");
                        includedCount++;
                    }
                    else
                    {
                        result.ExcludedVariantLabels.Add(
                            $"{item.SourceName}:{payload.Variant}:{guid}");
                    }
                }

                // 必須 Description は whitelist 適用後も最低 1 Payload が残る必要がある。
                if (!item.IsOptional && includedCount == 0)
                {
                    result.Errors.Add(
                        $"{item.SourceName}: no payload matched Variant whitelist.");
                }
            }

            IncludeAlwaysIncludedAssets(profile, result);

            // Excluded = Managed から Included を引いた差分。
            result.ExcludedGuids.Clear();
            foreach (var managedGuid in result.ManagedGuids)
            {
                if (!result.IncludedGuids.Contains(managedGuid))
                {
                    result.ExcludedGuids.Add(managedGuid);
                }
            }

            return result;
        }

        /// <summary>
        /// Profile に設定された Variant ホワイトリストを HashSet へ正規化する。
        /// 空リストの場合はデフォルト Variant（空文字）のみを許可する。
        /// </summary>
        public static HashSet<string> ResolveVariantWhitelist(IReadOnlyList<string> configuredWhitelist)
        {
            if (configuredWhitelist == null || configuredWhitelist.Count == 0)
            {
                return new HashSet<string>(StringComparer.Ordinal) { string.Empty };
            }

            return new HashSet<string>(configuredWhitelist, StringComparer.Ordinal);
        }

        /// <summary>
        /// SceneResourceMap 等の Variant 判定対象外アセットを無条件で Included に加える。
        /// Bootstrap 資産（SceneResourceMap, UIScene, app-config 等）向け。
        /// </summary>
        private static void IncludeAlwaysIncludedAssets(
            BuildVariantProfile profile,
            VariantWhitelistBuildResult result)
        {
            var index = 0;
            foreach (var reference in profile.AlwaysIncludedAssets)
            {
                IncludeDirectReference(
                    reference?.AssetGUID,
                    $"BuildVariantProfile:AlwaysIncludedAssets:{index}",
                    result);
                index++;
            }
        }

        /// <summary>AssetReference 直指定の GUID を Included / Managed に追加する。</summary>
        private static void IncludeDirectReference(
            string? guid,
            string sourceName,
            VariantWhitelistBuildResult result)
        {
            if (string.IsNullOrEmpty(guid))
            {
                result.Warnings.Add($"{sourceName}: AssetReference is empty.");
                return;
            }

            var resolvedGuid = guid!;
            result.ManagedGuids.Add(resolvedGuid);
            result.IncludedGuids.Add(resolvedGuid);
            result.IncludedVariantLabels.Add($"{sourceName}:<direct>:{resolvedGuid}");
        }
    }
}
