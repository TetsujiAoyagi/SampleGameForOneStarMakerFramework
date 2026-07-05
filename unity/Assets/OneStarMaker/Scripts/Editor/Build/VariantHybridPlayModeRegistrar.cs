#nullable enable

using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// <see cref="VariantHybridPlayModeScript"/> を Addressables Settings の DataBuilders 一覧へ
    /// 登録する Editor メニューコマンド。
    /// </summary>
    /// <remarks>
    /// AddressableAssetSettings.asset の YAML を手編集せず、
    /// ScriptableObject アセット作成と <see cref="AddressableAssetSettings.AddDataBuilder"/> 呼び出しで
    /// Play Mode Script ドロップダウンに選択肢を追加する。
    /// </remarks>
    internal static class VariantHybridPlayModeRegistrar
    {
        private const string DataBuildersFolder = "Assets/AddressableAssetsData/DataBuilders";
        private const string BuilderAssetPath = DataBuildersFolder + "/VariantHybridPlayModeScript.asset";

        /// <summary>
        /// Variant Hybrid Play Mode Script を Addressables Settings に登録する。
        /// </summary>
        [MenuItem("OneStarMaker/Addressables/Register Hybrid Play Mode Script")]
        public static void Register()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError(
                    "[VariantHybridPlayModeRegistrar] AddressableAssetSettings が見つかりません。" +
                    "Addressables グループを作成してから再度実行してください。");
                return;
            }

            if (settings.DataBuilders.Any(builder => builder is VariantHybridPlayModeScript))
            {
                Debug.Log(
                    "[VariantHybridPlayModeRegistrar] VariantHybridPlayModeScript は既に DataBuilders に登録済みです。");
                return;
            }

            EnsureDataBuildersFolderExists();

            var builder = ScriptableObject.CreateInstance<VariantHybridPlayModeScript>();
            AssetDatabase.CreateAsset(builder, BuilderAssetPath);
            AssetDatabase.SaveAssets();

            if (!settings.AddDataBuilder(builder))
            {
                settings.DataBuilders.Add(builder);
                settings.SetDirty(
                    AddressableAssetSettings.ModificationEvent.BatchModification,
                    builder,
                    postEvent: true,
                    settingsModified: true);
            }

            Debug.Log(
                "[VariantHybridPlayModeRegistrar] Variant Hybrid Play Mode Script を登録しました。\n" +
                "Addressables Settings の Play Mode Script ドロップダウンから " +
                "'Variant Hybrid Play Mode Script' を選択してください。");
        }

        /// <summary>
        /// DataBuilders 保存先フォルダが無ければ AddressableAssetsData 配下に作成する。
        /// </summary>
        private static void EnsureDataBuildersFolderExists()
        {
            if (AssetDatabase.IsValidFolder(DataBuildersFolder))
            {
                return;
            }

            const string parentFolder = "Assets/AddressableAssetsData";
            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AddressableAssetsData");
            }

            AssetDatabase.CreateFolder(parentFolder, "DataBuilders");
        }
    }
}
