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
            Debug.LogWarning(
                "[VariantHybridPlayModeRegistrar] Hybrid Play Mode registration is retired. " +
                "Use Tools/OSM/Content Delivery Use For Next Play. Addressables DataBuilders are not mutated.");
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
