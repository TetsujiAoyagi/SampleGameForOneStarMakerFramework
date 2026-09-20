#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// Editor Play Mode 用の Addressables Fast Mode 拡張 DataBuilder。
    /// ローカルに閉包が完結しているアセットだけを AssetDatabase 直読みカタログへ載せ、
    /// whitelist 対象外や閉包欠損のアセットは一時的に Addressables 設定から除外する。
    /// 除外されたエントリはローカルカタログに含まれないため、起動時に追加ロードされる
    /// リモートカタログ側で解決される（ハイブリッド Play Mode）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Addressables 標準 FastMode は「全 Addressable エントリがディスク上にある前提」で
    /// AssetDatabase から読み込む。ローカルに無いアセットをリモートへ倒すには、
    /// ローカルカタログ生成前に除外対象エントリを Addressables 設定から一時的に取り除く必要がある。
    /// </para>
    /// <para>
    /// 一時変更は共有される Addressables 設定を汚さないよう
    /// <see cref="AddressablesGroupSnapshot"/> で記録し、ビルド完了後（using 終了時）に
    /// 必ず復元する。Editor クラッシュ時の復元用に RestorePending も冒頭で呼ぶ。
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "VariantHybridPlayModeScript.asset",
        menuName = "OneStarMaker/Addressables/Variant Hybrid Play Mode Script")]
    public sealed class VariantHybridPlayModeScript : BuildScriptFastMode
    {
        /// <inheritdoc />
        public override string Name => "Variant Hybrid Play Mode Script";

        /// <inheritdoc />
        protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput)
        {
            AddressablesGroupSnapshot.RestorePending(builderInput.AddressableSettings);
            Debug.LogWarning(
                "[VariantHybridPlayModeScript] Hybrid Play Mode group mutation is retired. " +
                "Use Tools/OSM/Content Delivery Use For Next Play. Fast Mode proceeds without whitelist filtering.");
            return base.BuildDataImplementation<TResult>(builderInput);
        }

        /// <summary>
        /// 除外集合とリモートフォールバック設定の診断ログを出力する。
        /// </summary>
        private static void LogExclusionDiagnostics(
            AddressableAssetSettings settings,
            BuildVariantProfile profile,
            HashSet<string> toExclude,
            Dictionary<string, string> exclusionReasons)
        {
            var sb = new StringBuilder(512);
            sb.AppendLine("[VariantHybridPlayModeScript] Play Mode exclusion report");
            sb.AppendLine($"Excluded entry count: {toExclude.Count}");

            if (toExclude.Count > 0)
            {
                sb.AppendLine("Excluded GUIDs:");
                foreach (var guid in toExclude.OrderBy(g => g, StringComparer.Ordinal))
                {
                    exclusionReasons.TryGetValue(guid, out var reason);
                    sb.AppendLine($"  - {guid} ({ResolveAddress(settings, guid)})");
                    sb.AppendLine($"    理由: {reason ?? "不明"}");
                }
            }
            else
            {
                sb.AppendLine("Excluded GUIDs: (none — 全エントリがローカルカタログ対象)");
            }

            var remoteCatalogEnabled = !string.IsNullOrEmpty(profile.RemoteCatalogUrl);
            sb.AppendLine(
                remoteCatalogEnabled
                    ? $"Remote catalog fallback: 有効 ({profile.RemoteCatalogUrl})"
                    : "Remote catalog fallback: 無効 (RemoteCatalogUrl が空)");

            if (toExclude.Count > 0 && !remoteCatalogEnabled)
            {
                sb.AppendLine(
                    "警告: RemoteCatalogUrl が未設定のままエントリを除外しています。" +
                    "これらのアセットは Play 時にロード失敗する可能性があります。" +
                    "BuildVariantProfile.RemoteCatalogUrl を設定するか、欠損アセットをチェックアウトしてください。");
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// GUID から Addressables address を解決する。未登録 GUID は AssetDatabase パスへフォールバックする。
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
