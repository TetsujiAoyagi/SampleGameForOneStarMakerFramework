#nullable enable

using System.Collections.Generic;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.InGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SampleGame.DependOnAll.Editor
{
    /// <summary>
    /// 受入条件 A-3（生成器が手編集を消さないこと）を機械判定するための使い捨てプローブ。
    /// <para>
    /// 人が Unity Editor で GameObject を足す作業を batchmode で代行する。
    /// <c>AuthoredRoot</c> の <b>子</b>として置くので、生成器が
    /// <c>AuthoredRoot</c> ごと作り直す旧挙動なら必ず消える。
    /// </para>
    /// <para>
    /// このクラスはスキャフォールドであり、S-2（季節化）完了後の削除候補である。
    /// 構造化の投資をしないと決めた（HANDOFF §2.1）。
    /// </para>
    /// </summary>
    public static class HandEditProbe
    {
        private const string CellsRootFolder = "Assets/SampleGame/InGame/InGameSession/World/Cells";
        private const string ProbePrefix = "__HandEditProbe_";

        /// <summary>手編集を模したプローブを置く対象（HandAuthored 指定と同じ南辺 4 枚）。</summary>
        private static readonly Vector2Int[] TargetCells =
        {
            new(0, 0),
            new(1, 0),
            new(2, 0),
            new(3, 0),
        };

        /// <summary>
        /// 南辺 4 枚の Cell / Environment の <c>AuthoredRoot</c> 配下にプローブを 1 つずつ足して保存する。
        /// <c>-executeMethod SampleGame.DependOnAll.Editor.HandEditProbe.StampHandEdits</c>
        /// </summary>
        public static void StampHandEdits()
        {
            RunBatch(stamp: true);
        }

        /// <summary>
        /// 置いたプローブ 8 個がすべて生存しているか検査する。1 つでも欠けたら exit 1。
        /// <c>-executeMethod SampleGame.DependOnAll.Editor.HandEditProbe.VerifyHandEdits</c>
        /// </summary>
        public static void VerifyHandEdits()
        {
            RunBatch(stamp: false);
        }

        private static void RunBatch(bool stamp)
        {
            var missing = new List<string>();
            try
            {
                for (var i = 0; i < TargetCells.Length; i++)
                {
                    var coordinate = TargetCells[i];
                    var cellId = CellIdentity.Format(coordinate.x, coordinate.y);
                    var envId = EnvironmentIdentity.Format(coordinate.x, coordinate.y);
                    var folder = $"{CellsRootFolder}/{cellId}";

                    Process(
                        $"{folder}/{cellId}.unity",
                        DemoCellScene.AuthoredRootName,
                        ProbePrefix + cellId,
                        stamp,
                        missing);
                    Process(
                        $"{folder}/{envId}.unity",
                        EnvironmentScene.AuthoredRootName,
                        ProbePrefix + envId,
                        stamp,
                        missing);
                }

                if (stamp)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Debug.Log($"[HandEditProbe] Stamped {TargetCells.Length * 2} probes.");
                    EditorApplication.Exit(0);
                    return;
                }

                if (missing.Count > 0)
                {
                    Debug.LogError(
                        $"[HandEditProbe] FAILED: {missing.Count} 個のプローブが失われた。\n"
                        + string.Join("\n", missing));
                    EditorApplication.Exit(1);
                    return;
                }

                Debug.Log($"[HandEditProbe] OK: {TargetCells.Length * 2} 個のプローブがすべて生存している。");
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HandEditProbe] FAILED: {ex}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// 1 シーン分の stamp / verify。<c>stamp</c> が false のときは読むだけで保存しない。
        /// </summary>
        private static void Process(
            string scenePath,
            string authoredRootName,
            string probeName,
            bool stamp,
            List<string> missing)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                missing.Add($"{scenePath}: シーンが存在しない");
                return;
            }

            // batchmode では untitled 未保存シーンと Additive が衝突するため Single で開く。
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject? authoredRoot = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root != null && root.name == authoredRootName)
                {
                    authoredRoot = root;
                    break;
                }
            }

            if (authoredRoot == null)
            {
                missing.Add($"{scenePath}: {authoredRootName} が無い");
                return;
            }

            var probe = authoredRoot.transform.Find(probeName);
            if (!stamp)
            {
                if (probe == null)
                {
                    missing.Add($"{scenePath}: {authoredRootName}/{probeName} が消えている");
                }

                return;
            }

            if (probe != null)
            {
                // 既に置いてある（2 回目の stamp）。重複させない。
                return;
            }

            var probeGo = new GameObject(probeName);
            SceneManager.MoveGameObjectToScene(probeGo, scene);
            probeGo.transform.SetParent(authoredRoot.transform, false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
