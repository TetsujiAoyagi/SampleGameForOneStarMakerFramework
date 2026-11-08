#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using OneStarMaker.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>
    /// 実験の Play を囲む Editor の状態。3 つを退避・変更・復元する。
    /// (1) Play の初期 Scene（U-b1）: `EditorSceneManager.playModeStartScene` を空の起点 Scene にし、
    ///     利用者が開いている元の Scene を Unity が Play の初期 Scene としてロードしないようにする。
    /// (2) project の Game の bootstrap: この project では Play に入ると Game の initializer が必ず起動し、
    ///     Editor Play は content mode の config が無いと fail-closed で止まる。実験はその bootstrap を使わない
    ///     （U-b2）ので、Process の環境変数で addressables mode を与えて静かに起動させ、その存在を record に残す。
    ///     Game の名前は書かず、Framework の型から発見する（`ObservationReloadTests` の前例に倣うが、その code は呼ばない）。
    /// (3) Play 中の Unity のログ: N1〜N5 の破れを test の失敗にしない（HANDOFF §5）ため、エラーのログは record に移す。
    /// 退避した値は SessionState に置き、Domain Reload の後でも戻せるようにする。
    /// </summary>
    internal static class PlayEnvironment
    {
        private const string StartSceneKey = "OsmSpikeCdr.PlayEnv.StartScene";
        private const string StartSceneSavedKey = "OsmSpikeCdr.PlayEnv.StartSceneSaved";
        private const string EnvKeyKey = "OsmSpikeCdr.PlayEnv.EnvKey";
        private const string EnvHadKey = "OsmSpikeCdr.PlayEnv.EnvHad";
        private const string EnvPreviousKey = "OsmSpikeCdr.PlayEnv.EnvPrevious";
        private static ExperimentRecord? s_logRecord;
        private static Cell? s_logCell;

        internal static void SetStartScene(string? scenePath)
        {
            if (!SessionState.GetBool(StartSceneSavedKey, false))
            {
                var current = EditorSceneManager.playModeStartScene;
                SessionState.SetString(StartSceneKey, current != null ? AssetDatabase.GetAssetPath(current) : string.Empty);
                SessionState.SetBool(StartSceneSavedKey, true);
            }
            EditorSceneManager.playModeStartScene = scenePath == null ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        }

        internal static string CurrentStartScene()
        {
            var current = EditorSceneManager.playModeStartScene;
            return current != null ? AssetDatabase.GetAssetPath(current) : string.Empty;
        }

        internal static bool RestoreStartScene()
        {
            if (!SessionState.GetBool(StartSceneSavedKey, false)) return true;
            var path = SessionState.GetString(StartSceneKey, string.Empty);
            EditorSceneManager.playModeStartScene = path.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            SessionState.EraseString(StartSceneKey);
            SessionState.EraseBool(StartSceneSavedKey);
            return CurrentStartScene() == path;
        }

        /// <summary>Game の bootstrap に addressables mode を与える。initializer が無ければ何もせず null を返す。</summary>
        internal static string? QuietGameBootstrap()
        {
            var prefix = FindGamePrefix();
            if (prefix == null) return null;
            var key = prefix + "CONTENT__RUNTIMEMODE";
            if (SessionState.GetString(EnvKeyKey, string.Empty).Length == 0)
            {
                var previous = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process);
                SessionState.SetString(EnvKeyKey, key);
                SessionState.SetBool(EnvHadKey, previous != null);
                SessionState.SetString(EnvPreviousKey, previous ?? string.Empty);
            }
            Environment.SetEnvironmentVariable(key, "addressables", EnvironmentVariableTarget.Process);
            return key;
        }

        internal static void RestoreGameBootstrap()
        {
            var key = SessionState.GetString(EnvKeyKey, string.Empty);
            if (key.Length == 0) return;
            var previous = SessionState.GetBool(EnvHadKey, false) ? SessionState.GetString(EnvPreviousKey, string.Empty) : null;
            Environment.SetEnvironmentVariable(key, previous, EnvironmentVariableTarget.Process);
            SessionState.EraseString(EnvKeyKey);
            SessionState.EraseBool(EnvHadKey);
            SessionState.EraseString(EnvPreviousKey);
        }

        /// <summary>Unity のエラー・例外・assert のログを record へ移す。Domain Reload で購読が外れるので、Play に入るたびに呼ぶ。</summary>
        internal static void CaptureLogs(ExperimentRecord record, Cell? cell)
        {
            s_logRecord = record;
            s_logCell = cell;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        internal static void StopCapturingLogs()
        {
            Application.logMessageReceived -= OnLog;
            s_logRecord = null;
            s_logCell = null;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log || type == LogType.Warning) return;
            var record = s_logRecord;
            if (record == null) return;
            try { record.Write(s_logCell, "unity.log", ("type", type.ToString()), ("message", condition), ("stack", stackTrace)); }
            catch (Exception) { /* log の転記の失敗で Unity のログ配信を止めない。 */ }
        }

        private static string? FindGamePrefix()
        {
            // Play で bootstrap を起動するのは [RuntimeInitializeOnLoadMethod] を自身で宣言する派生型だけ。
            var initializers = TypeCache.GetTypesDerivedFrom<AbstractApplicationInitializer>()
                .Where(type => !type.IsAbstract && type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Any(method => method.IsDefined(typeof(RuntimeInitializeOnLoadMethodAttribute), false)))
                .ToArray();
            if (initializers.Length != 1) return null;
            var method = initializers[0].GetMethod("GetEnvironmentVariablePrefix",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (method == null) return null;
            // constructor の副作用を避けるため、初期化しない instance で override を呼ぶ。
            return method.Invoke(FormatterServices.GetUninitializedObject(initializers[0]), null) as string;
        }
    }
}
