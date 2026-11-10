#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Editor.PlaySnapshot
{
    /// <summary>1 つの Scene の複製の記録。SessionState に JSON で置くので public field にする。</summary>
    [Serializable]
    internal sealed class SceneSnapshotEntry
    {
        public string identity = "";
        public string sourcePath = "";
        public string sourceGuid = "";
        public string copyPath = "";
        public string copyGuid = "";
        public bool wasDirty;
        /// <summary>複製の bytes の SHA-256（小文字の 16 進）。Dirty の Scene では in-memory の内容になる。</summary>
        public string sha256 = "";
        public bool isActive;
    }

    /// <summary>複製を作れない理由（新規未保存、同名 2 つ、書き込み先が既にある、書き出しの失敗）。利用者に出す文言を持つ。</summary>
    public sealed class PlaySnapshotException : Exception
    {
        public PlaySnapshotException(string message) : base(message) { }
        public PlaySnapshotException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// 開いている Scene を <c>SaveScene(saveAsCopy)</c> で置き場へ複製する。元の Scene の file・Dirty・hierarchy は変えない
    /// （<c>saveAsCopy</c> を付けない保存は元の Scene の path と Dirty を変える）。途中で失敗したら、それまでに書いた複製を消してから投げる。
    /// </summary>
    internal static class SceneSnapshotWriter
    {
        /// <summary>
        /// <paramref name="scenes"/>（loaded の Scene を開いていた順）を <paramref name="folderAssetPath"/> の下へ <c>&lt;Scene 名&gt;.unity</c> で複製する。
        /// <paramref name="reservedNames"/> は置き場の中で別の用途に使う Scene 名（空の開始 Scene 等）で、同名の Scene は複製しない。
        /// </summary>
        internal static IReadOnlyList<SceneSnapshotEntry> Write(IReadOnlyList<Scene> scenes, Scene activeScene, string folderAssetPath,
            IReadOnlyCollection<string> reservedNames)
        {
            if (scenes == null) throw new ArgumentNullException(nameof(scenes));
            if (string.IsNullOrEmpty(folderAssetPath)) throw new ArgumentException("Folder is required.", nameof(folderAssetPath));
            if (scenes.Count == 0) throw new PlaySnapshotException("No open scene to copy.");

            foreach (var scene in scenes)
            {
                if (!scene.IsValid() || !scene.isLoaded) throw new PlaySnapshotException("A scene to copy is not loaded.");
                if (scene.path.Length == 0)
                    throw new PlaySnapshotException("An open scene has never been saved. Save it before playing with open scenes.");
                if (reservedNames.Contains(scene.name, StringComparer.Ordinal))
                    throw new PlaySnapshotException("An open scene uses the reserved name '" + scene.name + "'.");
            }
            var duplicate = scenes.GroupBy(x => x.name, StringComparer.Ordinal).FirstOrDefault(x => x.Count() > 1);
            if (duplicate != null)
                throw new PlaySnapshotException("Two open scenes are named '" + duplicate.Key + "'. Close one of them.");

            var written = new List<SceneSnapshotEntry>();
            try
            {
                foreach (var scene in scenes)
                {
                    var copyPath = folderAssetPath + "/" + scene.name + ".unity";
                    if (File.Exists(copyPath) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(copyPath, AssetPathToGUIDOptions.OnlyExistingAssets)))
                        throw new PlaySnapshotException("The snapshot destination already exists: " + copyPath);

                    var entry = new SceneSnapshotEntry
                    {
                        identity = scene.name,
                        sourcePath = scene.path,
                        sourceGuid = AssetDatabase.AssetPathToGUID(scene.path),
                        copyPath = copyPath,
                        wasDirty = scene.isDirty,
                        isActive = scene == activeScene,
                    };
                    written.Add(entry);
                    if (!EditorSceneManager.SaveScene(scene, copyPath, saveAsCopy: true))
                        throw new PlaySnapshotException("Unity could not write the snapshot of scene '" + scene.name + "' to " + copyPath + ".");
                    entry.copyGuid = AssetDatabase.AssetPathToGUID(copyPath);
                    entry.sha256 = Sha256Of(copyPath);
                }
                return written;
            }
            catch (Exception ex)
            {
                foreach (var entry in written) DeleteCopy(entry.copyPath);
                if (ex is PlaySnapshotException) throw;
                throw new PlaySnapshotException("Writing the play snapshot failed: " + ex.Message, ex);
            }
        }

        internal static string Sha256Of(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
        }

        private static void DeleteCopy(string copyPath)
        {
            // AssetDatabase が知っている物は .meta ごと消す。import の前に失敗した物は file だけ残りうる。
            if (!AssetDatabase.DeleteAsset(copyPath))
            {
                if (File.Exists(copyPath)) File.Delete(copyPath);
                if (File.Exists(copyPath + ".meta")) File.Delete(copyPath + ".meta");
            }
        }
    }
}
