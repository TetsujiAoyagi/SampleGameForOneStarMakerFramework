#nullable enable

using System.IO;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.InGame.Player;
using SampleGame.InGame.UI;
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
    /// PlayerScene / InGameUI の Unity シーン・payload・Addressables を一括整備する。
    /// ランタイムで GameObject を組み立てない前提のため、オーサリングは必ずこの経路（または同等の手動配置）で行う。
    /// </summary>
    public static class PlayerInGameSliceSceneCreator
    {
        private const string MenuPath = "OneStarMaker/Sample/Create Player + InGameUI Slice Scenes";

        private const string PlayerScenePath =
            "Assets/SampleGame/InGame/InGameSession/PlayerScene/PlayerScene.unity";
        private const string InGameUIScenePath =
            "Assets/SampleGame/InGame/InGameSession/InGameUI/InGameUI.unity";
        private const string InGameHudUxmlPath =
            "Assets/SampleGame/InGame/InGameSession/InGameUI/InGameHud.uxml";
        private const string PlayerSceneResourcePath =
            "Assets/OneStarMakerCommon/SceneMap/PlayerScene.asset";
        private const string InGameUIResourcePath =
            "Assets/OneStarMakerCommon/SceneMap/InGameUI.asset";

        [MenuItem(MenuPath)]
        public static void CreateScenes()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(InGameHudUxmlPath);
            if (uxml == null)
            {
                throw new FileNotFoundException($"UXML が見つかりません: {InGameHudUxmlPath}");
            }

            CreatePlayerScene();
            CreateInGameUIScene(uxml);

            RegisterAddressableScene(PlayerScenePath);
            RegisterAddressableScene(InGameUIScenePath);

            // SceneResource の payload GUID を更新（親子関係は既存マップを維持）。
            UpdateSceneResourcePayload(PlayerSceneResourcePath, "PlayerScene", PlayerScenePath, LoadType.NecessaryAlways);
            UpdateSceneResourcePayload(InGameUIResourcePath, "InGameUI", InGameUIScenePath, LoadType.NecessaryAlways);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[PlayerInGameSliceSceneCreator] 完了。\n" +
                "1. Play Mode で OutGame → InGame に入り、View_Main 追従と InGameUI HUD を確認。\n" +
                "2. シーンに Camera / AudioListener が無いことを Hierarchy で確認。\n" +
                "3. Scene Graph 運用時は Nodes 側 payload も同期して Generate。");
        }

        /// <summary>
        /// Flyer + LookAt + PlayerRigBindings のみを置く。Camera は絶対に置かない。
        /// </summary>
        private static void CreatePlayerScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("PlayerFlightRig");
            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 2.2f;
            capsule.radius = 0.6f;

            // 見た目用ボディ（シーン内 Primitive。ランタイム生成ではない）
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "FlyerBody";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            Object.DestroyImmediate(body.GetComponent<Collider>());

            // Follow は機体後方オフセット（三人称）。LookAt は前方（ピッチ用）。
            // 機体ルートを Follow にするとカメラがコライダー内部に落ちる。
            var follow = new GameObject("FollowTarget");
            follow.transform.SetParent(root.transform, false);
            follow.transform.localPosition = new Vector3(0f, 1.2f, -6f);

            var lookAt = new GameObject("LookAtTarget");
            lookAt.transform.SetParent(root.transform, false);
            lookAt.transform.localPosition = new Vector3(0f, 0f, 8f);

            var flyer = root.AddComponent<FlyController>();
            var bindings = root.AddComponent<PlayerRigBindings>();

            var so = new SerializedObject(bindings);
            so.FindProperty("_flyer").objectReferenceValue = flyer;
            so.FindProperty("_followTarget").objectReferenceValue = follow.transform;
            so.FindProperty("_lookAtTarget").objectReferenceValue = lookAt.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, PlayerScenePath);
            Debug.Log($"[PlayerInGameSliceSceneCreator] PlayerScene 保存: {PlayerScenePath}");
        }

        private static void CreateInGameUIScene(VisualTreeAsset uxml)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var viewObject = new GameObject("InGameHudView");
            var view = viewObject.AddComponent<InGameHudView>();

            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_visualTreeAsset").objectReferenceValue = uxml;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, InGameUIScenePath);
            Debug.Log($"[PlayerInGameSliceSceneCreator] InGameUI 保存: {InGameUIScenePath}");
        }

        private static void UpdateSceneResourcePayload(
            string assetPath,
            string identity,
            string sceneAssetPath,
            LoadType loadType)
        {
            var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(assetPath);
            if (resource == null)
            {
                Debug.LogWarning($"[PlayerInGameSliceSceneCreator] SceneResource 無し: {assetPath}");
                return;
            }

            var sceneGuid = AssetDatabase.AssetPathToGUID(sceneAssetPath);
            var so = new SerializedObject(resource);
            so.FindProperty("_identity").stringValue = identity;

            var descriptionProp = so.FindProperty("_sceneAssetDescription");
            var loadTypeProp = descriptionProp?.FindPropertyRelative("_loadType");
            if (loadTypeProp != null)
            {
                loadTypeProp.enumValueIndex = (int)loadType;
            }

            var payloadsProp = descriptionProp?.FindPropertyRelative("_payloads");
            if (payloadsProp != null)
            {
                payloadsProp.ClearArray();
                payloadsProp.InsertArrayElementAtIndex(0);
                var payloadElement = payloadsProp.GetArrayElementAtIndex(0);
                payloadElement.FindPropertyRelative("Variant").stringValue = string.Empty;
                var referenceProp = payloadElement.FindPropertyRelative("Reference");
                referenceProp.FindPropertyRelative("m_AssetGUID").stringValue = sceneGuid;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[PlayerInGameSliceSceneCreator] SceneResource payload 更新: {identity} -> {sceneGuid}");
        }

        private static void RegisterAddressableScene(string scenePath)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[PlayerInGameSliceSceneCreator] AddressableAssetSettings が見つかりません。");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(scenePath);
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = scenePath;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
            Debug.Log($"[PlayerInGameSliceSceneCreator] Addressables 登録: {scenePath}");
        }
    }
}
