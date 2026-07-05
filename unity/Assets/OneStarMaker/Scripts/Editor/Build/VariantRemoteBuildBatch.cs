#nullable enable

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// リモート配信 Addressables ビルドを Unity バッチモードから実行するエントリポイント。
    /// </summary>
    /// <remarks>
    /// <para>
    /// CI / リモート PC 上の PowerShell スクリプト (<c>tools/rebuild-remote.ps1</c>) から
    /// <c>-executeMethod OneStarMaker.Editor.Build.VariantRemoteBuildBatch.Build</c> で呼び出す。
    /// </para>
    /// <para>
    /// 処理内容:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>
    /// コマンドライン引数 <c>-variantProfile</c> で指定された
    /// <see cref="BuildVariantProfile"/> を読み込む（未指定時は RemoteFull）。
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Addressables Settings の Active Player Data Builder を
    /// <see cref="VariantFilteringBuildScript"/> に切り替え、
    /// その <c>_activeProfile</c> フィールドにプロファイルを設定する。
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="AddressableAssetSettings.BuildPlayerContent"/> を実行し、
    /// Remote Catalog とバンドルを <c>ServerData/[BuildTarget]/</c> へ出力する。
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// 成功時は終了コード 0、失敗時は 1 を <see cref="EditorApplication.Exit"/> で返す。
    /// バッチ自動化では終了コードの返却が必須のため、すべての分岐で Exit を呼ぶ。
    /// </para>
    /// </remarks>
    internal static class VariantRemoteBuildBatch
    {
        /// <summary>ログ出力時の共通プレフィックス。</summary>
        private const string LogPrefix = "[VariantRemoteBuildBatch]";

        /// <summary>
        /// <c>-variantProfile</c> 未指定時に使用する既定 BuildVariantProfile のアセットパス。
        /// </summary>
        private const string DefaultProfileAssetPath =
            "Assets/OneStarMaker/Editor/BuildProfiles/RemoteFull.asset";

        /// <summary>
        /// リモート配信 Addressables ビルドを実行する。
        /// </summary>
        /// <remarks>
        /// Unity バッチモード起動例:
        /// <code>
        /// Unity.exe -batchmode -quit -projectPath &lt;path&gt;
        ///   -executeMethod OneStarMaker.Editor.Build.VariantRemoteBuildBatch.Build
        ///   -variantProfile Assets/OneStarMaker/Editor/BuildProfiles/RemoteFull.asset
        ///   -logFile -
        /// </code>
        /// </remarks>
        public static void Build()
        {
            try
            {
                var profilePath = ResolveVariantProfilePath();
                Debug.Log($"{LogPrefix} BuildVariantProfile path: {profilePath}");

                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings == null)
                {
                    Debug.LogError(
                        $"{LogPrefix} AddressableAssetSettings が見つかりません。" +
                        "Addressables グループを作成してから再度実行してください。");
                    EditorApplication.Exit(1);
                    return;
                }

                var profile = AssetDatabase.LoadAssetAtPath<BuildVariantProfile>(profilePath);
                if (profile == null)
                {
                    Debug.LogError(
                        $"{LogPrefix} BuildVariantProfile を読み込めません: {profilePath}");
                    EditorApplication.Exit(1);
                    return;
                }

                var builder = settings.DataBuilders
                    .OfType<VariantFilteringBuildScript>()
                    .FirstOrDefault();
                if (builder == null)
                {
                    Debug.LogError(
                        $"{LogPrefix} DataBuilders 内に VariantFilteringBuildScript が見つかりません。" +
                        "Addressables Settings に Variant Filtering Build Script を登録してください。");
                    EditorApplication.Exit(1);
                    return;
                }

                var serializedBuilder = new SerializedObject(builder);
                var activeProfileProperty = serializedBuilder.FindProperty("_activeProfile");
                if (activeProfileProperty == null)
                {
                    Debug.LogError(
                        $"{LogPrefix} VariantFilteringBuildScript の _activeProfile プロパティが見つかりません。");
                    EditorApplication.Exit(1);
                    return;
                }

                activeProfileProperty.objectReferenceValue = profile;
                serializedBuilder.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(builder);

                var builderIndex = settings.DataBuilders.IndexOf(builder);
                if (builderIndex < 0)
                {
                    Debug.LogError(
                        $"{LogPrefix} VariantFilteringBuildScript の DataBuilders 内 index を取得できません。");
                    EditorApplication.Exit(1);
                    return;
                }

                settings.ActivePlayerDataBuilderIndex = builderIndex;
                AssetDatabase.SaveAssets();

                Debug.Log(
                    $"{LogPrefix} Active Player Data Builder を VariantFilteringBuildScript (index={builderIndex}) に設定しました。" +
                    $" Profile='{profile.name}'");

                AddressableAssetSettings.BuildPlayerContent(
                    out UnityEditor.AddressableAssets.Build.AddressablesPlayerBuildResult result);

                if (!string.IsNullOrEmpty(result.Error))
                {
                    Debug.LogError($"{LogPrefix} BuildPlayerContent failed: {result.Error}");
                    EditorApplication.Exit(1);
                    return;
                }

                Debug.Log(
                    $"{LogPrefix} Build succeeded. Duration={result.Duration:F2}s, " +
                    $"OutputPath={result.OutputPath}, LocationCount={result.LocationCount}");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Unhandled exception: {ex}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// コマンドライン引数から <c>-variantProfile</c> の値を取得する。
        /// </summary>
        /// <returns>BuildVariantProfile のアセットパス。</returns>
        private static string ResolveVariantProfilePath()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-variantProfile", StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return DefaultProfileAssetPath;
        }
    }
}
