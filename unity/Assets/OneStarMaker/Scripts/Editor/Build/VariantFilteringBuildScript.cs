#nullable enable

using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// BuildVariantProfile の Variant ホワイトリストを適用してから Packed Build する IDataBuilder。
    /// Addressables Settings で本 DataBuilder を有効化するには
    /// m_ActivePlayerDataBuilderIndex を VariantFilteringBuildScript の index に変更する。
    ///
    /// ビルドフロー:
    /// 1. 前回中断分の snapshot を復元
    /// 2. AssetDescription から whitelist を構築
    /// 3. Addressables グループを一時同期
    /// 4. 標準 Packed Build を実行
    /// 5. using 終了時に snapshot で Editor 設定を復元
    /// </summary>
    [CreateAssetMenu(
        fileName = "VariantFilteringBuildScript.asset",
        menuName = "OneStarMaker/Addressables/Variant Filtering Build Script")]
    public sealed class VariantFilteringBuildScript : BuildScriptPackedMode
    {
        /// <summary>ビルド時に適用する Variant ホワイトリスト設定。</summary>
        [SerializeField]
        private BuildVariantProfile? _activeProfile;

        /// <inheritdoc />
        public override string Name => "Variant Filtering Build Script";

        /// <summary>アクティブ BuildVariantProfile。</summary>
        public BuildVariantProfile? ActiveProfile => _activeProfile;

        /// <inheritdoc />
        protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput)
        {
            // 前回ビルドがクラッシュ等で中断された場合、残存 snapshot を先に復元する。
            AddressablesGroupSnapshot.RestorePending(builderInput.AddressableSettings);

            if (_activeProfile == null)
            {
                var message = "[VariantFilteringBuildScript] Active BuildVariantProfile is not assigned.";
                Debug.LogError(message);
                return AddressableAssetBuildResult.CreateResult<TResult>(null, 0, message);
            }

            var timer = Stopwatch.StartNew();
            // using 終了時に Addressables グループを元に戻す。
            using var snapshot = AddressablesGroupSnapshot.Capture(builderInput.AddressableSettings);
            var whitelistResult = VariantWhitelistBuilder.Build(_activeProfile);

            // Apply 前でも AssetDatabase パスで GUID の対応関係は確認できる。
            LogBuildReport(builderInput.AddressableSettings, whitelistResult);

            if (whitelistResult.HasErrors)
            {
                var message = "[VariantFilteringBuildScript] Build aborted due to whitelist validation errors.";
                Debug.LogError(message);
                return AddressableAssetBuildResult.CreateResult<TResult>(null, 0, message);
            }

            AddressablesGroupSyncFilter.Apply(
                builderInput.AddressableSettings,
                _activeProfile,
                whitelistResult,
                snapshot);
            // 同期先グループ未検出など、Filter 側で Errors が追加される場合がある。
            if (whitelistResult.HasErrors)
            {
                LogBuildReport(builderInput.AddressableSettings, whitelistResult);
                var message = "[VariantFilteringBuildScript] Build aborted due to Addressables group sync errors.";
                Debug.LogError(message);
                return AddressableAssetBuildResult.CreateResult<TResult>(null, 0, message);
            }

            builderInput.AddressableSettings.SetDirty(
                AddressableAssetSettings.ModificationEvent.BatchModification,
                null,
                postEvent: false,
                settingsModified: true);
            // 一時変更をディスクへ書き出してから Packed Build に進む。
            AssetDatabase.SaveAssets();

            // BuildScriptPackedMode の標準ビルドへ委譲する。
            var result = base.BuildDataImplementation<TResult>(builderInput);
            if (result != null)
            {
                result.Duration = timer.Elapsed.TotalSeconds;
            }

            return result;
        }

        /// <summary>
        /// whitelist 構築結果を Console に出力する。
        /// 本番ビルドに含まれる/除外される GUID と Variant を確認する用途。
        /// </summary>
        private static void LogBuildReport(
            AddressableAssetSettings settings,
            VariantWhitelistBuildResult result)
        {
            var sb = new StringBuilder(512);
            sb.AppendLine("[VariantFilteringBuildScript] Build report");
            sb.AppendLine($"Included GUIDs ({result.IncludedGuids.Count}):");
            foreach (var guid in result.IncludedGuids)
            {
                sb.AppendLine($"  + {guid} ({ResolveAddress(settings, guid)})");
            }

            sb.AppendLine($"Excluded GUIDs ({result.ExcludedGuids.Count}):");
            foreach (var guid in result.ExcludedGuids)
            {
                sb.AppendLine($"  - {guid} ({ResolveAddress(settings, guid)})");
            }

            sb.AppendLine("Included variants:");
            foreach (var label in result.IncludedVariantLabels)
            {
                sb.AppendLine($"  + {label}");
            }

            sb.AppendLine("Excluded variants:");
            foreach (var label in result.ExcludedVariantLabels)
            {
                sb.AppendLine($"  - {label}");
            }

            foreach (var warning in result.Warnings)
            {
                sb.AppendLine($"Warning: {warning}");
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// GUID から Addressables address を解決する。
        /// 未登録 GUID は AssetDatabase のパスへフォールバックする。
        /// </summary>
        private static string ResolveAddress(AddressableAssetSettings settings, string guid)
        {
            var entry = settings.FindAssetEntry(guid);
            if (entry != null && !string.IsNullOrEmpty(entry.address))
            {
                return entry.address;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? "<unknown>" : path;
        }
    }
}
