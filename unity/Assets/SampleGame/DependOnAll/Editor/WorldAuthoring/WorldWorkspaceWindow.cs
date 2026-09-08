#nullable enable

using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.WorldAuthoring
{
    internal sealed class WorldWorkspaceWindow : EditorWindow
    {
        private const string MapPath = "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";
        private WorldSeason _season;
        private int _x;
        private int _y;
        private WorldWorkspaceRole _role;
        private WorldLevelPayload _payload;
        private string _message = string.Empty;

        [MenuItem("OneStarMaker/World Workspace/Open Window")]
        private static void ShowWindow()
        {
            var window = GetWindow<WorldWorkspaceWindow>();
            window.titleContent = new GUIContent("World Workspace");
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("World Workspace", EditorStyles.boldLabel);
            _season = (WorldSeason)EditorGUILayout.EnumPopup("Season", _season);
            _x = EditorGUILayout.IntSlider("X", _x, 0, 8);
            _y = EditorGUILayout.IntSlider("Y", _y, 0, 5);
            _role = (WorldWorkspaceRole)EditorGUILayout.EnumPopup("Role", _role);
            if (_role == WorldWorkspaceRole.Level)
                _payload = (WorldLevelPayload)EditorGUILayout.EnumPopup("Payload", _payload);

            var selection = new WorldWorkspaceSelection(_season, _x, _y, _role, _payload);
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(MapPath);
            if (map == null)
            {
                EditorGUILayout.HelpBox($"SceneResourceMap is missing: {MapPath}", MessageType.Error);
                return;
            }

            if (WorldCompanionRecoveryService.IsBlocked(out var blocked))
            {
                EditorGUILayout.HelpBox(blocked, MessageType.Error);
                return;
            }

            var resolution = WorldWorkspaceSceneOpener.Resolve(map, selection.BuildOpenPlan());
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Open plan", EditorStyles.boldLabel);
            var plan = selection.BuildOpenPlan();
            for (var i = 0; i < plan.Count; i++)
            {
                var item = plan[i];
                EditorGUILayout.LabelField(
                    $"{i + 1}. {item.Identity} [{(string.IsNullOrEmpty(item.Variant) ? "default" : item.Variant)}]"
                    + (item.Required ? string.Empty : " (optional)"));
            }

            if (resolution.MissingRequired.Count > 0)
                EditorGUILayout.HelpBox("Missing required:\n" + string.Join("\n", resolution.MissingRequired), MessageType.Error);
            if (resolution.MissingOptional.Count > 0)
                EditorGUILayout.HelpBox("Missing optional:\n" + string.Join("\n", resolution.MissingOptional.ConvertAll(item => item.Identity)), MessageType.Info);

            using (new EditorGUI.DisabledScope(!resolution.CanOpen))
            {
                if (GUILayout.Button("Open"))
                    WorldWorkspaceSceneOpener.Open(map, selection, out _message);
            }

            var canCreate = resolution.CanOpen
                && resolution.MissingOptional.Count == 1
                && resolution.MissingOptional[0].CanCreate;
            using (new EditorGUI.DisabledScope(!canCreate))
            {
                if (GUILayout.Button("Create + Open"))
                    WorldCompanionSceneCreator.CreateAndOpen(selection, out _message);
            }

            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, MessageType.None);
        }
    }
}
