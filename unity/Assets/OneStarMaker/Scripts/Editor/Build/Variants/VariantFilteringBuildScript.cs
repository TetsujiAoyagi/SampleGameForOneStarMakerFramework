#nullable enable

using System.IO;
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
            AddressablesGroupSnapshot.RestorePending(builderInput.AddressableSettings);
            Debug.LogWarning(
                "[VariantFilteringBuildScript] Variant whitelist Addressables build is retired. " +
                "Use Tools/OSM/Content build, Delivery, and BS4 Player. Packed Mode proceeds without group mutation.");
            return base.BuildDataImplementation<TResult>(builderInput);
        }

        /// <summary>
        /// リモート配信ビルドの成果物ディレクトリへ build-info.json を出力する。
        /// </summary>
        /// <remarks>
        /// ベストエフォート。git 取得やファイル書き込みに失敗してもビルド自体は失敗させない。
        /// 起動時の <c>WarnOnRevisionMismatchAsync</c> が本ファイルの <c>revision</c> を参照する。
        /// </remarks>
        private static void TryWriteBuildInfo()
        {
            try
            {
                // Addressables の [BuildTarget] トークンは標準プラットフォームでは
                // activeBuildTarget.ToString()（例: StandaloneWindows64）と一致する。
                // PlatformMappingService は Addressables のバージョンで名前空間が変わるため、
                // 依存を避けて BuildTarget 名から出力先サブフォルダを解決する。
                var platformSubFolder = EditorUserBuildSettings.activeBuildTarget.ToString();
                var dir = Path.Combine("ServerData", platformSubFolder);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var revision = TryGetGitHeadRevision();
                var builtAtUtc = System.DateTime.UtcNow.ToString("o");
                var json = $"{{\"revision\":\"{revision}\",\"builtAtUtc\":\"{builtAtUtc}\"}}";
                var outputPath = Path.Combine(dir, "build-info.json");
                File.WriteAllText(outputPath, json);
                Debug.Log($"[VariantFilteringBuildScript] Wrote build-info.json: {outputPath} (revision={revision})");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[VariantFilteringBuildScript] build-info.json の出力に失敗しました（ビルドは続行）: {ex.Message}");
            }
        }

        /// <summary>
        /// プロジェクトルートで <c>git rev-parse HEAD</c> を実行し、HEAD リビジョンを取得する。
        /// </summary>
        /// <returns>Git リビジョン。取得失敗時は空文字。</returns>
        private static string TryGetGitHeadRevision()
        {
            try
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                if (string.IsNullOrEmpty(projectRoot))
                {
                    return string.Empty;
                }

                using var process = new System.Diagnostics.Process();
                process.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse HEAD",
                    WorkingDirectory = projectRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                process.Start();
                if (!process.WaitForExit(3000))
                {
                    try { process.Kill(); } catch { /* best-effort */ }
                    return string.Empty;
                }

                if (process.ExitCode != 0)
                {
                    return string.Empty;
                }

                return process.StandardOutput.ReadToEnd().Trim();
            }
            catch (System.Exception)
            {
                return string.Empty;
            }
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
