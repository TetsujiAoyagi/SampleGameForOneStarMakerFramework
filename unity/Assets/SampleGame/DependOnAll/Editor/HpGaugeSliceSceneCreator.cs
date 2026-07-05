#nullable enable

using System.IO;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.OutGame.ConfirmDialog;
using SampleGame.OutGame.HpGauge;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SampleGame.DependOnAll.Editor
{
    /// <summary>
    /// HP ゲージ Vertical Slice 用のシーン・SceneResource・Addressables 登録を自動化する。
    /// </summary>
    public static class HpGaugeSliceSceneCreator
    {
        private const string MenuPath = "OneStarMaker/Sample/Create HpGauge Slice Scenes";

        private const string HpGaugeScenePath = "Assets/SampleGame/OutGame/HpGauge/HpGauge.unity";
        private const string ConfirmDialogScenePath = "Assets/SampleGame/OutGame/ConfirmDialog/ConfirmDialog.unity";
        private const string HpGaugeUxmlPath = "Assets/SampleGame/OutGame/HpGauge/HpGauge.uxml";
        private const string ConfirmDialogUxmlPath = "Assets/SampleGame/OutGame/ConfirmDialog/ConfirmDialog.uxml";
        private const string SceneMapFolder = "Assets/OneStarMakerCommon/SceneMap";
        private const string SceneResourceMapPath = "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";
        private const string HpGaugeResourcePath = "Assets/OneStarMakerCommon/SceneMap/HpGauge.asset";
        private const string ConfirmDialogResourcePath = "Assets/OneStarMakerCommon/SceneMap/ConfirmDialog.asset";

        [MenuItem(MenuPath)]
        public static void CreateHpGaugeSliceScenes()
        {
            EnsureDirectory("Assets/SampleGame/OutGame/HpGauge");
            EnsureDirectory("Assets/SampleGame/OutGame/ConfirmDialog");
            EnsureDirectory(SceneMapFolder);

            var hpGaugeUxml = LoadRequiredAsset<VisualTreeAsset>(HpGaugeUxmlPath);
            var confirmDialogUxml = LoadRequiredAsset<VisualTreeAsset>(ConfirmDialogUxmlPath);

            CreateViewScene(HpGaugeScenePath, "HpGaugeView", hpGaugeUxml, typeof(HpGaugeView));
            CreateViewScene(ConfirmDialogScenePath, "ConfirmDialogView", confirmDialogUxml, typeof(ConfirmDialogView));

            RegisterAddressableScene(HpGaugeScenePath);
            RegisterAddressableScene(ConfirmDialogScenePath);

            var hpGaugeResource = CreateOrUpdateSceneResource(
                HpGaugeResourcePath,
                "HpGauge",
                HpGaugeScenePath,
                parent: null);
            var confirmDialogResource = CreateOrUpdateSceneResource(
                ConfirmDialogResourcePath,
                "ConfirmDialog",
                ConfirmDialogScenePath,
                parent: hpGaugeResource);

            SetParentChildRelation(hpGaugeResource, confirmDialogResource);
            RegisterSceneResourceMap(hpGaugeResource, confirmDialogResource);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LogManualSteps();
            Debug.Log("[HpGaugeSliceSceneCreator] HpGauge / ConfirmDialog スライスシーンの生成が完了しました。");
        }

        private static void CreateViewScene(
            string scenePath,
            string gameObjectName,
            VisualTreeAsset visualTreeAsset,
            System.Type viewType)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var viewObject = new GameObject(gameObjectName);
            var view = viewObject.AddComponent(viewType);

            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_visualTreeAsset").objectReferenceValue = visualTreeAsset;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[HpGaugeSliceSceneCreator] シーンを保存しました: {scenePath}");
        }

        private static SceneResource CreateOrUpdateSceneResource(
            string assetPath,
            string identity,
            string sceneAssetPath,
            SceneResource? parent)
        {
            var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(assetPath);
            if (resource == null)
            {
                resource = ScriptableObject.CreateInstance<SceneResource>();
                AssetDatabase.CreateAsset(resource, assetPath);
            }

            var sceneGuid = AssetDatabase.AssetPathToGUID(sceneAssetPath);
            var sceneReference = new AssetReference(sceneGuid);

            var serializedResource = new SerializedObject(resource);
            serializedResource.FindProperty("_identity").stringValue = identity;

            var descriptionProp = serializedResource.FindProperty("_sceneAssetDescription");
            if (descriptionProp != null)
            {
                var loadTypeProp = descriptionProp.FindPropertyRelative("_loadType");
                if (loadTypeProp != null)
                {
                    loadTypeProp.enumValueIndex = (int)LoadType.OnDemand;
                }

                var payloadsProp = descriptionProp.FindPropertyRelative("_payloads");
                if (payloadsProp != null)
                {
                    payloadsProp.ClearArray();
                    payloadsProp.InsertArrayElementAtIndex(0);
                    var payloadElement = payloadsProp.GetArrayElementAtIndex(0);
                    payloadElement.FindPropertyRelative("Variant").stringValue = string.Empty;
                    var referenceProp = payloadElement.FindPropertyRelative("Reference");
                    referenceProp.FindPropertyRelative("m_AssetGUID").stringValue = sceneGuid;
                }
            }

            serializedResource.FindProperty("_parent").objectReferenceValue = parent;
            serializedResource.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[HpGaugeSliceSceneCreator] SceneResource を更新しました: {assetPath} (Identity={identity})");
            return resource;
        }

        private static void SetParentChildRelation(SceneResource parent, SceneResource child)
        {
            var parentSo = new SerializedObject(parent);
            var childrenProp = parentSo.FindProperty("_children");
            childrenProp.ClearArray();

            var childIndex = -1;
            for (var i = 0; i < childrenProp.arraySize; i++)
            {
                if (childrenProp.GetArrayElementAtIndex(i).objectReferenceValue == child)
                {
                    childIndex = i;
                    break;
                }
            }

            if (childIndex < 0)
            {
                childrenProp.InsertArrayElementAtIndex(childrenProp.arraySize);
                childIndex = childrenProp.arraySize - 1;
            }

            childrenProp.GetArrayElementAtIndex(childIndex).objectReferenceValue = child;
            parentSo.ApplyModifiedPropertiesWithoutUndo();

            var childSo = new SerializedObject(child);
            childSo.FindProperty("_parent").objectReferenceValue = parent;
            childSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterSceneResourceMap(SceneResource hpGaugeResource, SceneResource confirmDialogResource)
        {
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(SceneResourceMapPath);
            if (map == null)
            {
                Debug.LogWarning(
                    $"[HpGaugeSliceSceneCreator] SceneResourceMap が見つかりません: {SceneResourceMapPath}。" +
                    "手動で Scene Graph Generate を実行してください。");
                return;
            }

            var mapSo = new SerializedObject(map);
            var listProp = mapSo.FindProperty("_sceneResources");
            UpsertSceneResource(listProp, hpGaugeResource);
            UpsertSceneResource(listProp, confirmDialogResource);
            mapSo.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[HpGaugeSliceSceneCreator] SceneResourceMap に HpGauge / ConfirmDialog を登録しました。");
        }

        private static void UpsertSceneResource(SerializedProperty listProp, SceneResource resource)
        {
            for (var i = 0; i < listProp.arraySize; i++)
            {
                if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == resource)
                {
                    return;
                }
            }

            listProp.InsertArrayElementAtIndex(listProp.arraySize);
            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = resource;
        }

        private static void RegisterAddressableScene(string scenePath)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning(
                    "[HpGaugeSliceSceneCreator] AddressableAssetSettings が見つかりません。" +
                    "Addressables グループへの手動登録が必要です。");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(scenePath);
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = scenePath;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);

            Debug.Log($"[HpGaugeSliceSceneCreator] Addressables に登録しました: {scenePath}");
        }

        private static T LoadRequiredAsset<T>(string assetPath) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                throw new FileNotFoundException($"必須アセットが見つかりません: {assetPath}");
            }

            return asset;
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
                var folderName = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
                {
                    AssetDatabase.CreateFolder(parent, folderName);
                }
            }
        }

        private static void LogManualSteps()
        {
            Debug.Log(
                "[HpGaugeSliceSceneCreator] === 残る手作業 / 確認手順 ===\n" +
                "1. Unity メニュー OneStarMaker > Scene Graph Editor を開き、HpGauge / ConfirmDialog ノードと親子 Edge を追加する（Scene Graph 運用時）。\n" +
                "2. Scene Graph Generate を実行し、SceneResourceMap の GenerateHash 整合性を取る（本スクリプト単体登録後は Generate 忘れ警告が出る場合あり）。\n" +
                "3. app-config.json の assetCheckout:firstSceneIdentify を \"HpGauge\" に設定するか、Title から HpGauge へ遷移する UI を追加する。\n" +
                "4. Play Mode で Damage 連打 → HP 表示の FromCurrent 追従、Flash/Shake 残留なしを確認する。\n" +
                "5. Open Dialog → 背面入力ブロック、Opening 途中 Close（UnloadScene）で Rewind 逆再生を確認する。\n" +
                "6. Addressables Build（必要な場合）を実行し、実機/CI ビルドでシーンロードを確認する。");
        }
    }
}
