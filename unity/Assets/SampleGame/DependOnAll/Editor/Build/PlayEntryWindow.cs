#nullable enable

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>
    /// Play 入口の観測 UI。Toolbar の要素と、RunPurpose・経路の選択、policy / record、回復操作を持つ詳細窓。
    /// <para>
    /// Toolbar の要素は既定で見えるとは限らないので、詳細窓を Editor セッションごとに 1 回、focus を奪わずに開く。Game view の裏に隠れないよう
    /// utility window にする。拒否の時は前面に出すが、dialog にはしない（毎回の popup で Play を止めない）。
    /// </para>
    /// </summary>
    internal sealed class PlayEntryWindow : EditorWindow
    {
        private const string Title = "OSM Play Entry";
        private const string ToolbarPath = "OSM/Play Entry";
        private const string OpenedThisSessionKey = "OSM.PlayEntry.WindowOpened";
        private Vector2 _scroll;

        [MenuItem("Tools/OSM/Play Entry")]
        private static void OpenFromMenu() => Open(focus: true);

        /// <summary>Editor セッションの最初の Play で 1 回だけ、focus を奪わずに開く。</summary>
        internal static void OpenOncePerSession()
        {
            if (SessionState.GetBool(OpenedThisSessionKey, false)) return;
            SessionState.SetBool(OpenedThisSessionKey, true);
            Open(focus: false);
        }

        internal static void ShowBlocked()
        {
            SessionState.SetBool(OpenedThisSessionKey, true);
            Open(focus: true);
        }

        private static void Open(bool focus)
        {
            // 既に開いていれば GetWindow はその窓を返す。focus が偽なら前面に出さない。
            var window = GetWindow<PlayEntryWindow>(utility: true, Title, focus);
            window.minSize = new Vector2(360, 220);
            window.Repaint();
        }

        [MainToolbarElement(ToolbarPath, defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement CreateToolbarElement()
        {
            var record = PlayEntryRecord.Load()?.record;
            var state = record == null ? "" : " · " + record.state;
            return new MainToolbarButton(
                new MainToolbarContent(PlayEntrySettings.RunPurpose + " / " + PlayEntrySettings.Route + state, null, "OneStarMaker Play Entry"),
                () => Open(focus: true));
        }

        internal static void RefreshToolbar() => MainToolbar.Refresh(ToolbarPath);

        private void OnEnable() => PlayEntryRecord.Changed += Repaint;

        private void OnDisable() => PlayEntryRecord.Changed -= Repaint;

        private void OnGUI()
        {
            DrawSelection();
            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var document = PlayEntryRecord.Load();
            if (document == null) EditorGUILayout.HelpBox("No Play has been recorded in this Editor session yet.", MessageType.Info);
            else DrawDocument(document);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawSelection()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                var purposes = SampleGameBuildPurposes.All.Select(x => x.Name).ToArray();
                var current = Array.IndexOf(purposes, PlayEntrySettings.RunPurpose);
                var selected = EditorGUILayout.Popup("Run Purpose", current, purposes);
                if (selected != current)
                {
                    PlayEntrySettings.RunPurpose = purposes[selected];
                    RefreshToolbar();
                }

                var route = (PlayEntryRoute)EditorGUILayout.EnumPopup("Route", PlayEntrySettings.Route);
                if (route != PlayEntrySettings.Route)
                {
                    PlayEntrySettings.Route = route;
                    RefreshToolbar();
                }
            }
        }

        private static void DrawDocument(PlayEntryRecord.Document document)
        {
            var policy = document.policy;
            var record = document.record;

            if (record.state == nameof(PlayEntryState.Blocked)) EditorGUILayout.HelpBox(policy.reason, MessageType.Warning);
            foreach (var warning in policy.warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            DrawRecoveries(policy);

            EditorGUILayout.LabelField("Policy", EditorStyles.boldLabel);
            Row("Run Purpose", policy.runPurpose);
            Row("Route", policy.route);
            Row("Action", policy.action);
            Row("Rule", policy.rule);
            Row("Reason", policy.reason);
            Row("Written Keys", policy.placedKeys.Length == 0 ? "(none)" : string.Join("\n", policy.placedKeys));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Record", EditorStyles.boldLabel);
            Row("State", record.state);
            Row("Effective Mode", record.effectiveMode);
            Row("Revision", record.revision.Length == 0 ? "-" : record.revision);
            Row("Initializer", record.initializer);
            Row("Errors / Exceptions", record.errorCount + " / " + record.exceptionCount);
            if (record.firstFailure.Length != 0) Row("First Failure", record.firstFailure);
            Row("Directory Sealed", record.sealedLog.Length == 0 ? "not observed" : "observed");
            Row("Variant Profile", record.variantProfileActive ? "active" : "none");
            DrawSnapshot(document);
        }

        private static void DrawSnapshot(PlayEntryRecord.Document document)
        {
            if (document.policy.snapshot.Length == 0) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Play Snapshot", EditorStyles.boldLabel);
            Row("Declared", string.Join("\n", document.policy.snapshot.Select(x =>
                x.identity + (x.active ? " (active)" : "") + "\n  from " + x.sourcePath + (x.dirty ? " (dirty)" : "") + "\n  sha256 " + x.sha256)));
            Row("Intake", document.record.intake.Length == 0 ? "not observed" : string.Join(", ", document.record.intake));
            Row("Origins", document.record.origins.Length == 0 ? "-" : string.Join("\n",
                document.record.origins.Select(x => x.identity + ": " + x.origin + " (" + x.path + ")")));
            Row("Copies Removed", document.record.copiesRemoved.Length == 0 ? "pending (Play has not ended)" : document.record.copiesRemoved);
            Row("Released By", PlayEntryRecord.ReleaseOwners);
        }

        private static void DrawRecoveries(PlayEntryRecord.Policy policy)
        {
            var recovery = PlayEntryRecord.ParseRecoveries(policy);
            if (recovery == PlayEntryRecovery.None) return;
            using (new EditorGUI.DisabledScope(PlayAsLauncher.CheckEditorState() != null))
            {
                if (recovery.HasFlag(PlayEntryRecovery.EmptyScene) && GUILayout.Button("Play in an empty scene"))
                {
                    // 空 Scene への置き換えと Play の開始で GUI の layout が壊れるので、この OnGUI の後に行う。
                    EditorApplication.delayCall += () => PlayEntryInterceptor.RecoverInEmptyScene();
                    GUIUtility.ExitGUI();
                }
                if (recovery.HasFlag(PlayEntryRecovery.VerifyPlayBuild) && GUILayout.Button("Build with Verify Play: " + policy.runPurpose))
                {
                    EditorApplication.delayCall += () => EditorApplication.ExecuteMenuItem(VerifyPlayLauncher.MenuRoot + policy.runPurpose);
                    GUIUtility.ExitGUI();
                }
                if (recovery.HasFlag(PlayEntryRecovery.SwitchToSource) && GUILayout.Button("Use the Source route"))
                {
                    PlayEntrySettings.Route = PlayEntryRoute.Source;
                    RefreshToolbar();
                }
                if (recovery.HasFlag(PlayEntryRecovery.SwitchToBuilt) && GUILayout.Button("Use the Built route"))
                {
                    PlayEntrySettings.Route = PlayEntryRoute.Built;
                    RefreshToolbar();
                }
            }
        }

        private static void Row(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                var style = EditorStyles.wordWrappedLabel;
                var height = style.CalcHeight(new GUIContent(value), Mathf.Max(120, EditorGUIUtility.currentViewWidth - EditorGUIUtility.labelWidth - 12));
                EditorGUILayout.SelectableLabel(value, style, GUILayout.Height(height));
            }
        }
    }
}
