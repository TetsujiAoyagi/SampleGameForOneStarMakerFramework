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
            // -----------------------------------------------------------------
            // 1. 前回 Play Mode ビルドが中断された場合、Library 配下の snapshot から復元する。
            //    共有 Addressables 設定が一時削除状態のまま残らないようにする。
            // -----------------------------------------------------------------
            AddressablesGroupSnapshot.RestorePending(builderInput.AddressableSettings);

            // -----------------------------------------------------------------
            // 2. 開発者ローカルで選択中の BuildVariantProfile を取得する。
            //    未選択時はフィルタせず標準 FastMode と同一挙動にフォールバックする。
            // -----------------------------------------------------------------
            var profile = DeveloperVariantSettings.instance.GetActiveProfile();
            if (profile == null)
            {
                Debug.Log("[VariantHybridPlayModeScript] No active profile. Falling back to standard Fast Mode.");
                return base.BuildDataImplementation<TResult>(builderInput);
            }

            var settings = builderInput.AddressableSettings;

            // -----------------------------------------------------------------
            // 3. snapshot を取得する。以降の一時削除は RecordRemoved で記録し、
            //    using 終了（Dispose）または次回 RestorePending で必ず元に戻す。
            // -----------------------------------------------------------------
            using var snapshot = AddressablesGroupSnapshot.Capture(settings);

            // -----------------------------------------------------------------
            // 4. AssetDescription ベースの whitelist を構築する。
            // -----------------------------------------------------------------
            var whitelist = VariantWhitelistBuilder.Build(profile);
            if (whitelist.HasErrors)
            {
                foreach (var error in whitelist.Errors)
                {
                    Debug.LogError($"[VariantHybridPlayModeScript] {error}");
                }

                var message = "[VariantHybridPlayModeScript] Play Mode build aborted due to whitelist validation errors.";
                Debug.LogError(message);
                return AddressableAssetBuildResult.CreateResult<TResult>(null, 0, message);
            }

            // -----------------------------------------------------------------
            // 5. AlwaysIncluded（Bootstrap 等の必須アセット）の閉包がローカルで完結しているか検証する。
            //    必須アセットの依存が欠けている Play は起動不能のため、ここで中断する。
            // -----------------------------------------------------------------
            var alwaysIncludedGuids = profile.AlwaysIncludedAssets
                .Where(r => r != null && !string.IsNullOrEmpty(r.AssetGUID))
                .Select(r => r.AssetGUID)
                .ToList();

            if (alwaysIncludedGuids.Count > 0)
            {
                var essentialClosure = AssetDependencyClosure.Compute(alwaysIncludedGuids);
                if (!essentialClosure.IsComplete)
                {
                    var sb = new StringBuilder(256);
                    sb.AppendLine(
                        "[VariantHybridPlayModeScript] Essential (AlwaysIncluded) assets are missing locally; cannot enter Play Mode.");
                    sb.AppendLine("Missing paths:");
                    foreach (var path in essentialClosure.MissingAssetPaths)
                    {
                        sb.AppendLine($"  - {path}");
                    }

                    sb.AppendLine("Checkout them or configure remote catalog.");
                    var detailMessage = sb.ToString();
                    Debug.LogError(detailMessage);

                    return AddressableAssetBuildResult.CreateResult<TResult>(
                        null,
                        0,
                        "[VariantHybridPlayModeScript] Essential (AlwaysIncluded) assets are missing locally; cannot enter Play Mode. Checkout them or configure remote catalog.");
                }
            }

            // -----------------------------------------------------------------
            // 6. ローカルカタログから除外する GUID 集合を決める。
            //
            //    (X) whitelist 対象外の ManagedGuids … VariantWhitelistBuilder.ExcludedGuids
            //    (Y) whitelist に含まれるが閉包がローカルで欠損している GUID
            //
            //    これらを Addressables 設定から一時削除すると、FastMode のローカルカタログ生成時に
            //    エントリが載らなくなる。Play 起動後、Runtime が追加ロードするリモートカタログ側に
            //    同じ address のエントリがあれば、そちらで解決される（ハイブリッド）。
            // -----------------------------------------------------------------
            var toExclude = new HashSet<string>(StringComparer.Ordinal);
            var exclusionReasons = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var guid in whitelist.ExcludedGuids)
            {
                toExclude.Add(guid);
                exclusionReasons[guid] = "whitelist除外（Variant 不一致などで Managed だが同梱対象外）";
            }

            foreach (var guid in whitelist.IncludedGuids)
            {
                var closure = AssetDependencyClosure.Compute(new[] { guid });
                if (closure.IsComplete)
                {
                    continue;
                }

                toExclude.Add(guid);
                var reasonSb = new StringBuilder(128);
                reasonSb.Append("閉包欠損（ローカルに依存アセットが不足）");
                if (closure.MissingAssetPaths.Count > 0)
                {
                    reasonSb.Append(": ");
                    reasonSb.Append(string.Join(", ", closure.MissingAssetPaths));
                }

                exclusionReasons[guid] = reasonSb.ToString();

                Debug.Log(
                    $"[VariantHybridPlayModeScript] Excluding included GUID {guid} ({ResolveAddress(settings, guid)}) " +
                    $"due to incomplete local closure. Missing: {string.Join(", ", closure.MissingAssetPaths)}");
            }

            // -----------------------------------------------------------------
            // 7. 除外対象を Addressables 設定から一時削除する。
            //    削除前に snapshot.RecordRemoved で address / group / labels を保存し、
            //    Dispose 時に元の状態へ復元できるようにする。
            // -----------------------------------------------------------------
            foreach (var guid in toExclude)
            {
                var entry = settings.FindAssetEntry(guid);
                if (entry == null)
                {
                    continue;
                }

                snapshot.RecordRemoved(entry);
                settings.RemoveAssetEntry(guid, postEvent: false);
            }

            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.BatchModification,
                null,
                postEvent: false,
                settingsModified: true);
            AssetDatabase.SaveAssets();

            // -----------------------------------------------------------------
            // 8. 診断ログ: 除外 GUID と理由、リモートフォールバックの有効性を出力する。
            // -----------------------------------------------------------------
            LogExclusionDiagnostics(settings, profile, toExclude, exclusionReasons);

            // -----------------------------------------------------------------
            // 9. 標準 FastMode ビルドへ委譲する（AssetDatabase 直読みの Play Mode カタログ生成）。
            // 10. return 時に using 終了 → snapshot.Dispose で Addressables 設定を復元する。
            // -----------------------------------------------------------------
            var result = base.BuildDataImplementation<TResult>(builderInput);
            return result;
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
