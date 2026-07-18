#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Editor.SceneGraph;
using OneStarMaker.Runtime.AssetDescriptions;
using SampleGame.OutGame;
using SampleGame.OutGame.Background;
using SampleGame.OutGame.Title;
using OneStarMaker.Runtime.UISystem;
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
    /// OutGame 共有背景と Title の最小縦スライスを Unity アセットへ配線する。
    /// SceneResource は Scene Graph Generate が正本であるため、このツールは変更しない。
    /// </summary>
    public static class OutGameBackgroundSliceCreator
    {
        private const string MenuPath = "OneStarMaker/Sample/Create OutGame Background Slice";
        private const string OutGameScenePath = "Assets/SampleGame/OutGame/OutGameScene.unity";
        private const string TitleScenePath = "Assets/SampleGame/OutGame/Title/Title.unity";
        private const string BackgroundUxmlPath = "Assets/SampleGame/OutGame/Background/OutGameBackground.uxml";
        private const string TitleUxmlPath = "Assets/SampleGame/OutGame/Title/Title.uxml";
        private const string DefaultTexturePath = "Assets/SampleGame/OutGame/Background/DefaultOutGameBackground.asset";
        private const string DefaultDefinitionPath = "Assets/SampleGame/OutGame/Background/DefaultOutGameBackgroundDefinition.asset";
        private const string OutGameNodePath = "Assets/SceneGraphData/Nodes/OutGame.asset";
        private const string TitleNodePath = "Assets/SceneGraphData/Nodes/Title.asset";
        private const string TotalGraphPath = "Assets/SceneGraphData/Graphs/Total.asset";

        [MenuItem(MenuPath)]
        public static void CreateOrUpdate()
        {
            var definition = GetOrCreateDefaultDefinition();

            ConfigureOutGameScene();
            ConfigureTitleScene(definition);
            RegisterAddressableScene(OutGameScenePath);
            UpdateSceneGraph();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[OutGameBackgroundSliceCreator] OutGame 背景と Title のシーン配線が完了しました。\n" +
                "OutGame の Scene Graph Payload と生成済み SceneResourceMap も更新しました。");
        }

        private static OutGameBackgroundDefinition GetOrCreateDefaultDefinition()
        {
            var definition = AssetDatabase.LoadAssetAtPath<OutGameBackgroundDefinition>(DefaultDefinitionPath);
            if (definition != null)
            {
                return definition;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultTexturePath);
            if (texture == null)
            {
                texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "DefaultOutGameBackground",
                };
                texture.SetPixel(0, 0, new Color(0.26f, 0.36f, 0.50f, 1f));
                texture.Apply();
                AssetDatabase.CreateAsset(texture, DefaultTexturePath);
            }

            definition = ScriptableObject.CreateInstance<OutGameBackgroundDefinition>();
            var serializedDefinition = new SerializedObject(definition);
            serializedDefinition.FindProperty("_texture").objectReferenceValue = texture;
            serializedDefinition.FindProperty("_tint").colorValue = Color.white;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, DefaultDefinitionPath);
            return definition;
        }

        private static void ConfigureOutGameScene()
        {
            var scene = EditorSceneManager.OpenScene(OutGameScenePath, OpenSceneMode.Single);
            var backgroundUxml = LoadRequiredAsset<VisualTreeAsset>(BackgroundUxmlPath);
            var view = Object.FindAnyObjectByType<OutGameBackgroundView>();
            if (view == null)
            {
                var backgroundObject = GameObject.Find("Background") ?? new GameObject("Background");
                view = backgroundObject.AddComponent<OutGameBackgroundView>();
            }

            AssignVisualTreeAsset(view, backgroundUxml);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureTitleScene(OutGameBackgroundDefinition definition)
        {
            var scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            var titleUxml = LoadRequiredAsset<VisualTreeAsset>(TitleUxmlPath);
            var view = Object.FindAnyObjectByType<TitleView>();
            if (view == null)
            {
                view = new GameObject("TitleView").AddComponent<TitleView>();
            }

            AssignVisualTreeAsset(view, titleUxml);
            view.AssignBackgroundDefinitionForEditor(definition);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AssignVisualTreeAsset(UIToolkitView view, VisualTreeAsset visualTreeAsset)
        {
            view.AssignVisualTreeAssetForEditor(visualTreeAsset);
            EditorUtility.SetDirty(view);
        }

        private static void RegisterAddressableScene(string scenePath)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidDataException("AddressableAssetSettings が見つかりません。");
            }

            var guid = AssetDatabase.AssetPathToGUID(scenePath);
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = scenePath;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }

        private static void UpdateSceneGraph()
        {
            var outGameNode = LoadRequiredAsset<SceneNodeData>(OutGameNodePath);
            var titleNode = LoadRequiredAsset<SceneNodeData>(TitleNodePath);
            var totalGraph = LoadRequiredAsset<SceneGraphEdges>(TotalGraphPath);

            var sceneGuid = AssetDatabase.AssetPathToGUID(OutGameScenePath);
            outGameNode.Payloads.Clear();
            outGameNode.Payloads.Add(new AssetPayload(string.Empty, new AssetReference(sceneGuid)));
            EditorUtility.SetDirty(outGameNode);

            // 失われたアセットへの null 参照は Generate を止めるため、Graph の中間データで除去する。
            totalGraph.GraphNodes.RemoveAll(node => node == null);
            totalGraph.AddNode(outGameNode);
            totalGraph.AddNode(titleNode);

            if (!totalGraph.Edges.Any(edge => edge.Parent == outGameNode && edge.Child == titleNode))
            {
                totalGraph.RemoveEdgeByChild(titleNode);
                totalGraph.AddEdge(outGameNode, titleNode);
            }

            EditorUtility.SetDirty(totalGraph);

            var nodes = AssetDatabase.FindAssets("t:SceneNodeData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SceneNodeData>)
                .Where(node => node != null)
                .ToList();
            var graphs = AssetDatabase.FindAssets("t:SceneGraphEdges")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SceneGraphEdges>)
                .Where(graph => graph != null)
                .ToList();

            if (!SceneResourceGenerator.Generate(nodes, graphs))
            {
                throw new InvalidDataException("Scene Graph Generate が失敗しました。Console の検証エラーを確認してください。");
            }
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
    }
}
