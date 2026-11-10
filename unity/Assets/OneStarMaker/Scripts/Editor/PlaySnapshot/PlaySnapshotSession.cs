#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Editor.PlaySnapshot
{
    /// <summary>宣言の 1 Scene（読み取り専用）。host の Editor の観測 UI と record が使う。</summary>
    public sealed class PlaySnapshotScene
    {
        internal PlaySnapshotScene(SceneSnapshotEntry entry)
        {
            Identity = entry.identity;
            SourcePath = entry.sourcePath;
            SourceGuid = entry.sourceGuid;
            CopyPath = entry.copyPath;
            CopyGuid = entry.copyGuid;
            WasDirty = entry.wasDirty;
            Sha256 = entry.sha256;
            IsActive = entry.isActive;
        }

        /// <summary>論理 identity（Scene 名）。</summary>
        public string Identity { get; }
        public string SourcePath { get; }
        public string SourceGuid { get; }
        public string CopyPath { get; }
        public string CopyGuid { get; }
        public bool WasDirty { get; }
        /// <summary>複製の bytes の SHA-256（小文字の 16 進）。</summary>
        public string Sha256 { get; }
        public bool IsActive { get; }
    }

    /// <summary>
    /// Play snapshot の 1 回の Play の準備と解除。Editor の側の状態（複製と空の開始 Scene の file、宣言、Runtime の bridge、
    /// <c>playModeStartScene</c>）を作る者と消す者は、どれもこの class だけにする。
    /// <para>
    /// 準備は「置き場の回収 → 開いている Scene の複製 → 空の開始 Scene → <c>playModeStartScene</c> → 宣言（SessionState と bridge）」の順。
    /// 開いている Scene は Play に入らず（Unity は開始 Scene だけを読み、停止時に元の Scene へ戻す）、起動の取り込みの段が複製を読む。
    /// </para>
    /// <para>
    /// 解除（<see cref="Release"/>）は何度呼んでも同じ結果になる。契機は EnteredEditMode、Play に至らなかった idle、準備の例外、
    /// domain reload の後の初期化（宣言が残っていて Play 中でも移行中でもない時）。Editor の異常終了の後は、次の Editor の初期化と
    /// 次の準備の最初の回収が、死んだ process の folder と、置き場の下を指したままの <c>playModeStartScene</c> を片付ける。
    /// </para>
    /// <para>
    /// 宣言は SessionState（Editor process の寿命）に置き、Domain Reload が有効な Play で static の bridge が消えても、初期化で置き直す。
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class PlaySnapshotSession
    {
        /// <summary>1 回の Play の config に真で置く key。起動の取り込みの段はこの key がある時だけ宣言を読み、無ければ起動の失敗にする。</summary>
        public const string ExpectedConfigKey = EditorPlaySnapshotBridge.ExpectedConfigKey;

        /// <summary>置き場に作る空の開始 Scene の名前。開いている Scene に同名があれば複製しない。</summary>
        public const string StartSceneName = "OsmPlaySnapshotStart";

        private const string SessionKey = "OSM.PlaySnapshot.Session.v1";
        private const string LogPrefix = "[PlaySnapshot] ";

        /// <summary>
        /// 宣言の identity と同じ名前の Scene が、複製以外の path で load された（起動の後に unload され、別の供給元から読み直された）。
        /// 引数は identity と load された Scene の path。Play の間だけ、load のたびに出す（回数の整理は受け手が行う）。
        /// </summary>
        public static event Action<string, string>? SourceChanged;

        [Serializable]
        private sealed class Document
        {
            public string folderAssetPath = "";
            public string startScenePath = "";
            public SceneSnapshotEntry[] scenes = Array.Empty<SceneSnapshotEntry>();
        }

        static PlaySnapshotSession()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnUpdate;
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (Load() is { } document && (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                // Domain Reload が有効な Play。BeforeSceneLoad より前にここが走るので、起動の取り込みの段が読む口を置き直す。
                InstallBridge(document);
            }
            else if (!Application.isBatchMode)
            {
                // 宣言が残っていれば前の Play の残り、無くても異常終了の残骸がありうる。AssetDatabase を触るので初期化の後に行う。
                EditorApplication.delayCall += () =>
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    if (Load() != null) Release();
                    else CollectLeftovers(logWhenNothing: false);
                };
            }
        }

        /// <summary>準備済みの session があれば真。</summary>
        public static bool IsPrepared => Load() != null;

        /// <summary>今の宣言（並び順は Play の前に開いていた順）。準備されていなければ空。</summary>
        public static IReadOnlyList<PlaySnapshotScene> Declaration =>
            Load()?.scenes.Select(x => new PlaySnapshotScene(x)).ToArray() ?? Array.Empty<PlaySnapshotScene>();

        /// <summary>
        /// 今の session が、渡した loaded で開いている Scene（名前と path）の一覧と一致するか。次が全て真の時だけ真:
        /// session が準備済み、<c>playModeStartScene</c> がその session の空の開始 Scene、名前と path の集合が宣言の元の Scene の集合と同じ。
        /// Play の入口が、自分で準備した再 Play だけを通すために問い合わせる。
        /// </summary>
        public static bool Matches(IReadOnlyList<(string Name, string Path)> openScenes)
        {
            if (openScenes == null) throw new ArgumentNullException(nameof(openScenes));
            var document = Load();
            if (document == null) return false;
            var start = EditorSceneManager.playModeStartScene;
            if (start == null || !string.Equals(AssetDatabase.GetAssetPath(start), document.startScenePath, StringComparison.Ordinal)) return false;
            var declared = new HashSet<(string, string)>(document.scenes.Select(x => (x.identity, x.sourcePath)));
            var open = new HashSet<(string, string)>(openScenes.Select(x => (x.Name, x.Path)));
            return declared.SetEquals(open);
        }

        /// <summary>
        /// 開いている loaded の Scene を全て複製し、空の開始 Scene を <c>playModeStartScene</c> にし、宣言を置く。失敗したら作った物を全て戻して
        /// <see cref="PlaySnapshotException"/> を投げる。Play 中・移行中には呼ばない。<c>playModeStartScene</c> が既に設定されていれば上書きせず拒否する。
        /// </summary>
        public static void Prepare()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A play snapshot cannot be prepared during Play.");
            if (Load() != null) Release();
            // 開始 Scene の設定は利用者の物かもしれない。戻す値を常に null にするため、非 null なら始めない。
            if (EditorSceneManager.playModeStartScene != null)
                throw new PlaySnapshotException("A Play Mode start scene is already set. Clear EditorSceneManager.playModeStartScene first.");

            // 準備の所要（機構の比較の材料）。回収を含め、宣言を置き終えるまでを測る。
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            CollectLeftovers(logWhenNothing: false);
            var folderName = SnapshotRoot.NewFolderName(System.Diagnostics.Process.GetCurrentProcess().Id,
                SnapshotRoot.CurrentProcessStartUtcTicks(), Guid.NewGuid().ToString("N").Substring(0, 8));
            var folderAssetPath = SnapshotRoot.AssetPath + "/" + folderName;
            var document = new Document { folderAssetPath = folderAssetPath, startScenePath = folderAssetPath + "/" + StartSceneName + ".unity" };
            try
            {
                // 宣言は最初に置く（folder の path だけ）。この後のどこで失敗しても、Release が folder を見つけて消せる。
                Save(document);
                if (!AssetDatabase.IsValidFolder(SnapshotRoot.AssetPath)) AssetDatabase.CreateFolder("Assets", Path.GetFileName(SnapshotRoot.AssetPath));
                AssetDatabase.CreateFolder(SnapshotRoot.AssetPath, folderName);

                var active = SceneManager.GetActiveScene();
                var scenes = new List<Scene>();
                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded) scenes.Add(scene);
                }
                document.scenes = SceneSnapshotWriter.Write(scenes, active, folderAssetPath, new[] { StartSceneName }).ToArray();
                Save(document);

                WriteEmptyStartScene(document.startScenePath, active);
                var startAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(document.startScenePath);
                if (startAsset == null) throw new PlaySnapshotException("The empty start scene was not imported: " + document.startScenePath);
                EditorSceneManager.playModeStartScene = startAsset;
                InstallBridge(document);
                Debug.Log(LogPrefix + "Prepared in " + stopwatch.ElapsedMilliseconds + " ms: " + Describe(document));
            }
            catch (Exception ex)
            {
                Release();
                if (ex is PlaySnapshotException) throw;
                throw new PlaySnapshotException("Preparing the play snapshot failed: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// bridge を外し、<c>playModeStartScene</c> が置き場の下を指していれば null に戻し、宣言と置き場の folder を消す。
        /// 何度呼んでも同じ結果になる。消せなかった file は次の回収に任せる（次の Play の宣言には使わない）。
        /// </summary>
        public static void Release()
        {
            EditorPlaySnapshotBridge.Clear();
            ResetStartSceneIfOurs();
            var document = Load();
            SessionState.EraseString(SessionKey);
            if (document != null && document.folderAssetPath.Length != 0) DeleteFolder(document.folderAssetPath);
            CollectLeftovers(logWhenNothing: false);
        }

        /// <summary>置き場の folder が残っていなければ真（解除の後に、複製の bytes を残していないことを観測 UI に出す）。</summary>
        public static bool HasNoCopiesOnDisk => !Directory.Exists(SnapshotRoot.AssetPath);

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode && Load() != null) Release();
        }

        private static void OnUpdate()
        {
            // 宣言が無い間は SessionState の文字列の長さだけを見る。ExitingPlayMode の間は isPlaying だけが真なので、2 値の両方が偽の時だけ解除する。
            if (SessionState.GetString(SessionKey, string.Empty).Length == 0) return;
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Release();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!EditorApplication.isPlaying) return;
            var document = Load();
            if (document == null) return;
            var declared = document.scenes.FirstOrDefault(x => string.Equals(x.identity, scene.name, StringComparison.Ordinal));
            if (declared == null || string.Equals(declared.copyPath, scene.path, StringComparison.Ordinal)) return;
            SourceChanged?.Invoke(declared.identity, scene.path);
        }

        private static void InstallBridge(Document document)
        {
            var declaration = document.scenes.Select(x => new EditorPlaySnapshotScene(x.identity, x.copyPath, x.isActive)).ToArray();
            // file の無い複製は Unity に渡さず null を返す。起動の失敗の最初の 1 行を「snapshot が読めない」にするため
            // （渡すと Unity 自身の path の Error が先に出る）。
            EditorPlaySnapshotBridge.Set(declaration,
                path => File.Exists(path) ? EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive)) : null);
        }

        /// <summary>
        /// 空の Scene を additive に作って保存し、閉じる。開いている Scene の並びと Dirty は変えず、active Scene は元へ戻す。
        /// file を手で書かないのは、Unity の Scene の形式を入口が持たないため。
        /// </summary>
        private static void WriteEmptyStartScene(string path, Scene previousActive)
        {
            var empty = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                if (!EditorSceneManager.SaveScene(empty, path))
                    throw new PlaySnapshotException("Unity could not write the empty start scene to " + path + ".");
            }
            finally
            {
                EditorSceneManager.CloseScene(empty, removeScene: true);
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            }
        }

        private static void ResetStartSceneIfOurs()
        {
            var start = EditorSceneManager.playModeStartScene;
            if (start == null) return;
            var path = AssetDatabase.GetAssetPath(start);
            if (path.StartsWith(SnapshotRoot.AssetPath + "/", StringComparison.Ordinal)) EditorSceneManager.playModeStartScene = null;
        }

        /// <summary>前の Play と異常終了の残骸の回収。準備の最初と解除の最後に行い、開始 Scene の設定の残りも戻す。</summary>
        private static void CollectLeftovers(bool logWhenNothing)
        {
            ResetStartSceneIfOurs();
            var active = Load()?.folderAssetPath;
            var result = SnapshotRoot.Collect(Path.GetFullPath(SnapshotRoot.AssetPath), SnapshotRoot.ProcessStartOf,
                System.Diagnostics.Process.GetCurrentProcess().Id, active == null ? null : Path.GetFileName(active));
            foreach (var path in result.Malformed)
                Debug.LogWarning(LogPrefix + "Left an unexpected entry in the snapshot folder untouched: " + path);
            foreach (var path in result.Remaining)
                Debug.LogWarning(LogPrefix + "Could not remove a leftover snapshot; it will be retried and is not used for Play: " + path);
            if (result.Collected.Count != 0)
            {
                Debug.Log(LogPrefix + "Collected leftover snapshots: " + string.Join(", ", result.Collected));
                RefreshIfIdle();
            }
            else if (logWhenNothing)
                Debug.Log(LogPrefix + "No leftover snapshots.");
        }

        private static void DeleteFolder(string folderAssetPath)
        {
            var full = Path.GetFullPath(folderAssetPath);
            try
            {
                if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
                if (File.Exists(full + ".meta")) File.Delete(full + ".meta");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning(LogPrefix + "Could not remove the snapshot folder; it will be retried: " + folderAssetPath + " (" + ex.Message + ")");
            }
            RefreshIfIdle();
        }

        private static void RefreshIfIdle()
        {
            // 消した file を AssetDatabase へ伝える。Play 中・移行中は Unity の Play の終了の後の refresh に任せる。
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) AssetDatabase.Refresh();
        }

        private static string Describe(Document document) =>
            string.Join(", ", document.scenes.Select(x => x.identity + (x.isActive ? "*" : "") + (x.wasDirty ? " (dirty)" : "") +
                                                          " sha256=" + (x.sha256.Length >= 12 ? x.sha256.Substring(0, 12) : x.sha256)));

        private static Document? Load()
        {
            var json = SessionState.GetString(SessionKey, string.Empty);
            return json.Length == 0 ? null : JsonUtility.FromJson<Document>(json);
        }

        private static void Save(Document document) => SessionState.SetString(SessionKey, JsonUtility.ToJson(document));
    }
}
