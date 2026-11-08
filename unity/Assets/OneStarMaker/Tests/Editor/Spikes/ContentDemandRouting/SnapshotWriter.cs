#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>1 つの snapshot の結果。Domain Reload を越えて round の状態に持つ。</summary>
    [Serializable]
    internal sealed class SnapshotInfo
    {
        public string identity = "";
        public string originalPath = "";
        public string originalGuid = "";
        public string snapshotPath = "";
        public string snapshotGuid = "";
        public string sha256 = "";
        public string marker = "";
        public double elapsedMs;
        public bool refused;
        public string refusal = "";
    }

    /// <summary>
    /// Play 前に、開いている Scene の現在の内容（Dirty を含む）を一時 root へ別 asset として書き出す（HANDOFF §2 用語 snapshot）。
    /// 書き出しは `EditorSceneManager.SaveScene(scene, path, saveAsCopy: true)` だけで、元の Scene を保存しない（N1 の経路 (f)）。
    /// file 名は元の Scene と同じにする。Unity の Scene 名は file 名から決まり、論理 Scene identity を名前で引く
    /// 現行の経路（`GetSceneByName`）と fixture の directory の entry が同じ identity を指すため（N2 の経路 (a) を観測する）。
    /// 失敗した時は書き出した物を消してから投げる（AC9 (a)）。
    /// </summary>
    internal static class SnapshotWriter
    {
        /// <summary>AC9 (a) の注入点。"after-copy" / "after-marker" の段で true を返すと、その段で例外を投げる。</summary>
        internal static Func<string, bool>? FailAt;

        internal static SnapshotInfo Create(Scene original, string destinationFolder, string marker)
        {
            var info = new SnapshotInfo { identity = original.name, originalPath = original.path, marker = marker };
            // 拒否を許すのは新規未保存（path が空）だけ（AC2）。黙って元を使わず、拒否を結果に明示する。
            if (original.path.Length == 0)
            {
                info.refused = true;
                info.refusal = "untitled-scene: the scene has no asset path, so no logical identity or GUID can be recorded";
                return info;
            }
            info.originalGuid = AssetDatabase.AssetPathToGUID(original.path);
            var destination = destinationFolder + "/" + Path.GetFileName(original.path);
            if (File.Exists(TempRootLease.FullPath(destination)))
                throw new InvalidOperationException("Snapshot destination already exists: " + destination);
            var watch = Stopwatch.StartNew();
            try
            {
                if (!EditorSceneManager.SaveScene(original, destination, true))
                    throw new InvalidOperationException("SaveScene(saveAsCopy) returned false: " + original.path);
                Inject("after-copy");
                OriginMarker.RewriteInFile(TempRootLease.FullPath(destination), marker);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Inject("after-marker");
                info.snapshotPath = destination;
                info.snapshotGuid = AssetDatabase.AssetPathToGUID(destination);
                info.sha256 = SceneStateProbe.Sha256OfFile(TempRootLease.FullPath(destination));
                if (info.snapshotGuid.Length == 0) throw new InvalidOperationException("Snapshot was not imported: " + destination);
                info.elapsedMs = watch.Elapsed.TotalMilliseconds;
                return info;
            }
            catch
            {
                Delete(destination);
                throw;
            }
        }

        internal static bool Delete(string assetPath)
        {
            if (assetPath.Length == 0) return true;
            var full = TempRootLease.FullPath(assetPath);
            if (File.Exists(full) || AssetDatabase.AssetPathToGUID(assetPath).Length != 0) AssetDatabase.DeleteAsset(assetPath);
            if (File.Exists(full)) File.Delete(full);
            if (File.Exists(full + ".meta")) File.Delete(full + ".meta");
            return !File.Exists(full);
        }

        private static void Inject(string stage)
        {
            if (FailAt != null && FailAt(stage)) throw new InjectedFailureException("snapshot:" + stage);
        }
    }

    /// <summary>harness が注入点で投げる例外。本物の失敗と区別して record に残す。</summary>
    internal sealed class InjectedFailureException : Exception
    {
        internal InjectedFailureException(string stage) : base("Injected failure at " + stage) => Stage = stage;
        internal string Stage { get; }
    }
}
