#nullable enable

using System.IO;
using Unity.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CD0Spike.Editor
{
    internal static class Cd0FixtureAuthoring
    {
        [MenuItem("OneStarMaker/CD0/Generate Fixture")]
        private static void GenerateFixture()
        {
            EnsureFolder(Cd0FixturePaths.FixtureRoot);
            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                CreatePayloadScene();
                CreateBootstrapScene();
                var probeAsset = CreateOrLoad<Cd0ProbeAsset>(Cd0FixturePaths.ProbeAsset);
                probeAsset.SetValue("probe-v1");
                EditorUtility.SetDirty(probeAsset);

                var sceneId = LoadableSceneIdEditorUtility.CreateLoadableSceneId(Cd0FixturePaths.PayloadScene);
                var objectId = LoadableObjectIdEditorUtility.CreateLoadableObjectId(probeAsset);
                var loadable = new Loadable<Cd0ProbeAsset>(objectId);
                var root = CreateOrLoad<Cd0Root>(Cd0FixturePaths.RootAsset);
                root.Initialize(sceneId, loadable);
                EditorUtility.SetDirty(root);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[CD0] Fixture generated at {Cd0FixturePaths.FixtureRoot}");
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }

        private static void CreatePayloadScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var markerObject = new GameObject("CD0 Payload Marker");
            var marker = markerObject.AddComponent<Cd0SceneMarker>();
            marker.SetValue("scene-v1");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, Cd0FixturePaths.PayloadScene);
        }

        private static void CreateBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var runnerObject = new GameObject("CD0 Probe Runner");
            var runner = runnerObject.AddComponent<Cd0ProbeRunner>();
            var serializedRunner = new SerializedObject(runner);
            serializedRunner.FindProperty("_contentDirectoryPath").stringValue = Cd0FixturePaths.ResolveProjectRelative(Cd0FixturePaths.DefaultContentOutput);
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, Cd0FixturePaths.BootstrapScene);
        }

        private static T CreateOrLoad<T>(string assetPath) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, assetPath);
            return created;
        }

        private static void EnsureFolder(string assetFolder)
        {
            var current = "Assets";
            foreach (var segment in assetFolder.Substring("Assets/".Length).Split('/'))
            {
                var next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }
    }
}
