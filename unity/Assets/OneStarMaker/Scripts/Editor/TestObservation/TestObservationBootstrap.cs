#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OneStarMaker.Editor.TestObservation
{
    [InitializeOnLoad]
    internal static class TestObservationBootstrap
    {
        private static TestObservationState? state;
        private static TestObservationWriter? writer;
        private static TestObservationCallbacks? callbacks;
        private static string filter = "";

        static TestObservationBootstrap()
        {
            try { Initialize(); }
            catch (Exception ex)
            {
                Debug.LogError("OSM_OBSERVATION_ERROR bootstrap: " + ex);
                // progress が壊れて復帰できなくても、独立の設定記録が読めれば人間の Editor 設定だけは戻す。
                try { EmergencyRestore(); }
                catch (Exception restore) { Debug.LogError("OSM_OBSERVATION_ERROR emergency restore: " + restore); }
            }
        }

        internal static bool IsActive => state != null && writer != null;

        private static void Initialize()
        {
            var args = Environment.GetCommandLineArgs();
            // 手動 Test Runner と非観測 invocation には callback も永続ファイルも作らない。
            if (!Application.isBatchMode || !Has(args, "-runTests") ||
                Get(args, "-testPlatform") != "EditMode" ||
                !Guid.TryParse(Get(args, "-osmTestInvocation"), out var invocation)) return;
            var terminal = Get(args, "-osmTestObservation");
            var project = Get(args, "-projectPath");
            if (terminal == "" || project == "" || !Path.IsPathFullyQualified(terminal))
            { Debug.LogError("OSM_OBSERVATION_ERROR 観測引数が不完全です"); return; }
            filter = Get(args, "-testFilter");
            var fullProject = Path.GetFullPath(project);
            writer = new TestObservationWriter(terminal);
            var id = invocation.ToString();
            var pid = Process.GetCurrentProcess().Id;
            state = writer.Read(id, fullProject, pid, Stopwatch.Frequency) ??
                TestObservationState.Create(id, fullProject, pid, Stopwatch.Frequency);
            state.Bootstrap(Guid.NewGuid().ToString(), DateTime.UtcNow.ToString("o"), Stopwatch.GetTimestamp(), Stopwatch.Frequency);
            Persist();
            if (state.runStarted) { CaptureAssemblies(); Persist(); }
            callbacks = new TestObservationCallbacks(state, Persist, filter);
            TestRunnerApi.RegisterTestCallback(callbacks);
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            EditorApplication.quitting += Quit;
        }

        internal static void CaptureAssemblies()
        {
            if (state == null) return;
            var names = state.selected.Select(x => x.assemblyName).Where(x => x != "");
            state.AddAssemblies(LoadedAssemblyObservation.Capture(state.domains.Count - 1,
                DateTime.UtcNow.ToString("o"), Stopwatch.GetTimestamp(), names));
        }

        internal static void Fail(string message)
        {
            if (state == null) return;
            state.Fail(message);
            Persist();
        }

        private static void BeforeReload()
        {
            if (state == null) return;
            state.BeforeReload(DateTime.UtcNow.ToString("o"), Stopwatch.GetTimestamp());
            Persist();
            if (callbacks != null) TestRunnerApi.UnregisterTestCallback(callbacks);
            callbacks = null;
        }

        private static void Quit()
        {
            if (state == null || writer == null) return;
            try
            {
                RestoreReloadSettings();
                state.Seal();
                writer.Progress(state);
                writer.Terminal(state);
            }
            catch (Exception ex)
            {
                state.Fail("seal/write failure: " + ex.Message);
                try { writer.Progress(state); } catch { /* log veto が最後の拒否経路 */ }
                Debug.LogError("OSM_OBSERVATION_ERROR seal: " + ex);
            }
        }

        private static void Persist()
        {
            if (state == null || writer == null) return;
            try { writer.Progress(state); }
            catch (Exception ex)
            {
                // UTF が callback 例外を吸収しても sticky state とログの両方で runner に拒否を伝える。
                state.Fail("progress write failure: " + ex.Message);
                Debug.LogError("OSM_OBSERVATION_ERROR progress: " + ex);
            }
        }

        internal static void PrepareReloadSettings()
        {
            if (state == null || writer == null) return;
            try
            {
                if (File.Exists(writer.RecoveryPath)) throw new IOException("reload settings record が既にあります");
                var record = new ReloadSettingsRecord { invocationId = state.invocationId,
                    projectPath = state.projectPath, enterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled,
                    enterPlayModeOptions = (int)EditorSettings.enterPlayModeOptions };
                TestObservationWriter.WriteNew(writer.RecoveryPath, System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(record)));
                EditorSettings.enterPlayModeOptionsEnabled = false;
            }
            catch (Exception ex) { Fail("reload settings 保存失敗: " + ex.Message); Debug.LogError("OSM_OBSERVATION_ERROR reload settings: " + ex); throw; }
        }

        internal static void RestoreReloadSettings()
        {
            if (state == null || writer == null || !File.Exists(writer.RecoveryPath)) return;
            try
            {
                var record = JsonUtility.FromJson<ReloadSettingsRecord>(File.ReadAllText(writer.RecoveryPath));
                if (record == null || record.invocationId != state.invocationId || record.projectPath != state.projectPath)
                    throw new IOException("reload settings record の identity が一致しません");
                if (record.restored) return;
                EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)record.enterPlayModeOptions;
                EditorSettings.enterPlayModeOptionsEnabled = record.enterPlayModeOptionsEnabled;
                record.restored = true;
                TestObservationWriter.AtomicWrite(writer.RecoveryPath, System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(record)));
            }
            catch (Exception ex) { Fail("reload settings 復元失敗: " + ex.Message); Debug.LogError("OSM_OBSERVATION_ERROR reload settings: " + ex); throw; }
        }

        private static void EmergencyRestore()
        {
            var args = Environment.GetCommandLineArgs();
            if (!Guid.TryParse(Get(args, "-osmTestInvocation"), out var id)) return;
            var path = Get(args, "-osmTestObservation");
            var project = Get(args, "-projectPath");
            if (path == "" || project == "" || !Path.IsPathFullyQualified(path)) return;
            var recordPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? "", "reload-settings.json");
            if (!File.Exists(recordPath)) return;
            var record = JsonUtility.FromJson<ReloadSettingsRecord>(File.ReadAllText(recordPath));
            if (record == null || record.invocationId != id.ToString() || record.projectPath != Path.GetFullPath(project))
                throw new IOException("private recovery record の identity が一致しません");
            if (record.restored) return;
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)record.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = record.enterPlayModeOptionsEnabled;
            record.restored = true;
            TestObservationWriter.AtomicWrite(recordPath, System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(record)));
        }

        private static bool Has(string[] args, string name) => args.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
        private static string Get(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return "";
        }

        [Serializable] private sealed class ReloadSettingsRecord
        {
            public string invocationId = "", projectPath = "";
            public bool enterPlayModeOptionsEnabled, restored;
            public int enterPlayModeOptions;
        }
    }
}
