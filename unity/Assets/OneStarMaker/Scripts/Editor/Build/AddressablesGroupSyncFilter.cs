#nullable enable

using UnityEditor.AddressableAssets.Settings;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// Variant whitelist に基づき Addressables グループを一時同期する。
    /// ソース .asset は変更せず、Addressables Settings 上の entry だけを増減する。
    /// </summary>
    internal static class AddressablesGroupSyncFilter
    {
        /// <summary>
        /// Included GUID を target group へ追加し、Excluded GUID の entry を一時削除する。
        /// 変更内容は snapshot に記録され、ビルド後に復元される。
        /// </summary>
        public static void Apply(
            AddressableAssetSettings settings,
            BuildVariantProfile profile,
            VariantWhitelistBuildResult whitelistResult,
            AddressablesGroupSnapshot snapshot)
        {
            var targetGroup = settings.FindGroup(profile.TargetAddressablesGroupName)
                ?? settings.DefaultGroup;
            if (targetGroup == null)
            {
                whitelistResult.Errors.Add(
                    $"Target Addressables group '{profile.TargetAddressablesGroupName}' was not found.");
                return;
            }

            foreach (var guid in whitelistResult.IncludedGuids)
            {
                // 既に Addressables 登録済みなら追加不要。
                if (settings.FindAssetEntry(guid) != null)
                {
                    continue;
                }

                settings.CreateOrMoveEntry(guid, targetGroup, readOnly: false, postEvent: false);
                snapshot.RecordAdded(guid);
            }

            foreach (var guid in whitelistResult.ExcludedGuids)
            {
                // Collector 管理外の entry（サードパーティ等）は触らない。
                if (!whitelistResult.ManagedGuids.Contains(guid))
                {
                    continue;
                }

                var entry = settings.FindAssetEntry(guid);
                if (entry == null)
                {
                    continue;
                }

                // 所属グループに関係なく一時削除する。復元は snapshot 任せ。
                snapshot.RecordRemoved(entry);
                settings.RemoveAssetEntry(guid, postEvent: false);
            }
        }
    }
}
