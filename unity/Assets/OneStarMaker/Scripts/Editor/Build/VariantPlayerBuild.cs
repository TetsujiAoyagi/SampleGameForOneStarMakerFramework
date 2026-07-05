#nullable enable

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// アクティブな <see cref="BuildVariantProfile"/> の構成でプレイヤービルドを行う Editor メニュー。
    /// </summary>
    /// <remarks>
    /// Build Settings の Scene 0 (SampleScene) は差し替えず、
    /// 共有の app-config.json に論理初回シーン識別子を一時書き込みしてからビルドし、
    /// 完了後 (成否問わず) 必ず元内容へ復元する。
    /// Addressables の同梱内容は <see cref="VariantFilteringBuildScript"/> で別途ビルドすること。
    /// </remarks>
    public static class VariantPlayerBuild
    {
        /// <summary>論理初回シーン差し替え用の app-config.json パス (Assets 相対)。</summary>
        private const string AppConfigAssetPath = "Assets/SampleGame/Config/app-config.json";

        /// <summary>app-config.json 内の論理初回シーン識別子キー。</summary>
        private const string FirstSceneIdentifyConfigKey = "assetCheckout:firstSceneIdentify";

        /// <summary>プレイヤービルド出力先ディレクトリ (プロジェクトルート相対)。</summary>
        private const string OutputDirectory = "Builds/ActiveVariant";

        /// <summary>Windows 向け実行ファイル名。</summary>
        private const string WindowsExecutableName = "SampleGame.exe";

        /// <summary>Windows 以外向け実行ファイル名 (拡張子なし)。</summary>
        private const string GenericExecutableName = "SampleGame";

        /// <summary>Build Settings に登録するダミー起動シーン (Scene 0)。</summary>
        private const string BootstrapScenePath = "Assets/Scenes/SampleScene.unity";

        /// <summary>
        /// アクティブ Variant プロファイルの論理初回シーン設定を反映してプレイヤービルドを実行する。
        /// </summary>
        [MenuItem("OneStarMaker/Build/Build Player (Active Variant)")]
        public static void BuildActiveVariant()
        {
            var profile = DeveloperVariantSettings.instance.GetActiveProfile();
            if (profile == null)
            {
                Debug.LogError(
                    "[VariantPlayerBuild] Project Settings > OneStarMaker > Variant でプロファイルを選択してください。");
                return;
            }

            var configFullPath = GetProjectRelativeFullPath(AppConfigAssetPath);
            string originalJson;
            try
            {
                originalJson = File.ReadAllText(configFullPath);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[VariantPlayerBuild] app-config.json の読み込みに失敗したためビルドを中止します: {ex.Message}");
                return;
            }

            if (!string.IsNullOrEmpty(profile.FirstSceneIdentify))
            {
                try
                {
                    var modifiedJson = InsertFirstSceneIdentify(originalJson, profile.FirstSceneIdentify);
                    File.WriteAllText(configFullPath, modifiedJson);
                    AssetDatabase.ImportAsset(AppConfigAssetPath);
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"[VariantPlayerBuild] app-config.json への firstSceneIdentify 書き込みに失敗したためビルドを中止します: {ex.Message}");
                    RestoreAppConfig(configFullPath, originalJson);
                    return;
                }
            }

            var firstSceneLabel = string.IsNullOrEmpty(profile.FirstSceneIdentify)
                ? "Title(既定)"
                : profile.FirstSceneIdentify;
            Debug.Log(
                $"[VariantPlayerBuild] 初回シーン: {firstSceneLabel}。" +
                " Addressables は別途 VariantFilteringBuildScript でビルドすること。");

            try
            {
                ExecutePlayerBuild();
            }
            finally
            {
                RestoreAppConfig(configFullPath, originalJson);
            }
        }

        /// <summary>
        /// app-config.json の JSON テキストに論理初回シーン識別子キーを挿入する。
        /// </summary>
        /// <param name="originalJson">元の app-config.json 全文。</param>
        /// <param name="sceneIdentify">挿入するシーン識別子。</param>
        /// <returns>キーを追加/更新した JSON 文字列。</returns>
        /// <exception cref="ArgumentException">JSON 形式が不正な場合。</exception>
        private static string InsertFirstSceneIdentify(string originalJson, string sceneIdentify)
        {
            if (string.IsNullOrEmpty(originalJson))
            {
                throw new ArgumentException("app-config.json が空です。", nameof(originalJson));
            }

            if (originalJson.Contains($"\"{FirstSceneIdentifyConfigKey}\"", StringComparison.Ordinal))
            {
                Debug.Log(
                    $"[VariantPlayerBuild] app-config.json に {FirstSceneIdentifyConfigKey} が既に存在するため、そのまま使用します。");
                return originalJson;
            }

            var openBraceIndex = originalJson.IndexOf('{');
            if (openBraceIndex < 0)
            {
                throw new ArgumentException("app-config.json にオブジェクト開始 '{' が見つかりません。", nameof(originalJson));
            }

            var closeBraceIndex = originalJson.LastIndexOf('}');
            if (closeBraceIndex < 0 || closeBraceIndex <= openBraceIndex)
            {
                throw new ArgumentException("app-config.json にオブジェクト終了 '}' が見つかりません。", nameof(originalJson));
            }

            var innerContent = originalJson.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);
            var isEmptyObject = string.IsNullOrWhiteSpace(innerContent);

            var escapedIdentify = EscapeJsonString(sceneIdentify);
            var entry = isEmptyObject
                ? $"\n  \"{FirstSceneIdentifyConfigKey}\": \"{escapedIdentify}\"\n"
                : $",\n  \"{FirstSceneIdentifyConfigKey}\": \"{escapedIdentify}\"\n";

            return originalJson.Substring(0, closeBraceIndex) + entry + originalJson.Substring(closeBraceIndex);
        }

        /// <summary>
        /// プレイヤービルドを実行し、結果をログ出力する。
        /// </summary>
        private static void ExecutePlayerBuild()
        {
            var outputDir = Path.Combine(Path.GetDirectoryName(Application.dataPath)!, OutputDirectory);
            Directory.CreateDirectory(outputDir);

            var target = EditorUserBuildSettings.activeBuildTarget;
            var exeName = IsWindowsBuildTarget(target) ? WindowsExecutableName : GenericExecutableName;
            var locationPathName = Path.Combine(outputDir, exeName);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { BootstrapScenePath },
                locationPathName = locationPathName,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log(
                    $"[VariantPlayerBuild] プレイヤービルド成功: {locationPathName} " +
                    $"(出力サイズ: {report.summary.totalSize} bytes)");
            }
            else
            {
                Debug.LogError(
                    $"[VariantPlayerBuild] プレイヤービルド失敗: {report.summary.result} " +
                    $"(エラー数: {report.summary.totalErrors})");
            }
        }

        /// <summary>
        /// app-config.json を元の内容へ復元し、AssetDatabase へ反映する。
        /// </summary>
        /// <param name="configFullPath">app-config.json のフルパス。</param>
        /// <param name="originalJson">退避しておいた元内容。</param>
        private static void RestoreAppConfig(string configFullPath, string originalJson)
        {
            try
            {
                File.WriteAllText(configFullPath, originalJson);
                AssetDatabase.ImportAsset(AppConfigAssetPath);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[VariantPlayerBuild] app-config.json の復元に失敗しました。手動で内容を確認してください: {ex.Message}");
            }
        }

        /// <summary>
        /// Assets 相対パスをプロジェクトルート基準のフルパスへ変換する。
        /// </summary>
        /// <param name="assetPath">Assets 配下の相対パス。</param>
        /// <returns>フルパス。</returns>
        private static string GetProjectRelativeFullPath(string assetPath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath)!;
            return Path.Combine(projectRoot, assetPath);
        }

        /// <summary>
        /// JSON 文字列リテラル用に特殊文字をエスケープする。
        /// </summary>
        /// <param name="value">エスケープ対象文字列。</param>
        /// <returns>エスケープ済み文字列。</returns>
        private static string EscapeJsonString(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// 指定 BuildTarget が Windows スタンドアロン向けかどうかを返す。
        /// </summary>
        /// <param name="target">判定対象。</param>
        /// <returns>Windows 向けの場合 true。</returns>
        private static bool IsWindowsBuildTarget(BuildTarget target)
        {
            return target is BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64;
        }
    }
}
