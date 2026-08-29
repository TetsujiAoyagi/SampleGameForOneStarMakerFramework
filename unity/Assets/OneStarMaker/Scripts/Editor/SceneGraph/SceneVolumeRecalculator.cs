#nullable enable

using System.Collections.Generic;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// `.unity` の中身から <see cref="SceneResource"/> の体積と距離政策の候補フラグを焼く
    /// （34-ondemand-spatial-policy.md §5）。
    ///
    /// <para>
    /// <b>候補フラグの規則:</b> 体積が空でなく、かつ**候補である祖先を持たない**シーンが候補である。
    /// これは §34 §6 の「距離の単位は人が開く作業単位」を機械的に言い直したものである。
    /// 空間を占める最上位のノードが作業単位であり、その下の分割は空間ではなく職種の分割
    /// （現状の Environment）なので、親へ畳んで候補にしない。
    /// </para>
    ///
    /// <para>
    /// <b>名前文法を一切使わない。</b> 親子は <see cref="SceneResource.Parent"/> /
    /// <see cref="SceneResource.Children"/>、シーンの所在は payload の GUID で引く。
    /// </para>
    ///
    /// <para>
    /// 合併規則は <see cref="SceneVolumeMath"/>（純関数）、アセットと `.unity` の読み取りは
    /// <see cref="SceneVolumeSceneReader"/> にある。
    /// </para>
    /// </summary>
    public static class SceneVolumeRecalculator
    {
        /// <summary>全件再計算メニュー。体積が引けないときの案内文で参照される。</summary>
        public const string RecalculateAllMenuPath = "OneStarMaker/Scene Volume/Recalculate All";

        /// <summary>親子を辿るときの安全上限（データが循環していても止まるため）。</summary>
        private const int MaxDepth = 64;

        /// <summary>
        /// 保存フックを一時停止する。生成器のように多数のシーンを続けざまに保存する処理が、
        /// 1 保存ごとに祖先の再計算を走らせないようにするためのもの。
        /// </summary>
        public static bool SaveHookSuspended { get; set; }

        [MenuItem(RecalculateAllMenuPath)]
        private static void RecalculateAllFromMenu()
        {
            var changed = RecalculateAll();
            Debug.Log($"[SceneVolumeRecalculator] 体積を再計算しました（更新 {changed} 件）。");
        }

        /// <summary>
        /// 全 <see cref="SceneResource"/> の体積と候補フラグを再計算する。
        /// </summary>
        /// <returns>実際に値が変わった件数。</returns>
        public static int RecalculateAll()
        {
            // batchmode でダイアログを出すと固まる。対話セッションでだけ未保存の扱いを訊く。
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[SceneVolumeRecalculator] 未保存シーンの扱いが確定しなかったため中止しました。");
                return 0;
            }

            var resources = SceneVolumeSceneReader.LoadAll();
            if (resources.Count == 0)
            {
                return 0;
            }

            var previous = SaveHookSuspended;
            SaveHookSuspended = true;
            try
            {
                var own = new Dictionary<int, Bounds>(resources.Count);
                for (var i = 0; i < resources.Count; i++)
                {
                    own[resources[i].GetInstanceID()] =
                        SceneVolumeSceneReader.ComputeOwnVolume(resources[i], liveScene: null);
                }

                var final = new Dictionary<int, Bounds>(resources.Count);
                var candidate = new Dictionary<int, bool>(resources.Count);

                for (var i = 0; i < resources.Count; i++)
                {
                    if (resources[i].Parent == null)
                    {
                        Resolve(resources[i], ancestorIsCandidate: false, own, final, candidate, depth: 0);
                    }
                }

                var changed = 0;
                for (var i = 0; i < resources.Count; i++)
                {
                    var resource = resources[i];
                    var id = resource.GetInstanceID();
                    // 親リンクが壊れていてルートから到達できなかったものも取りこぼさない。
                    if (!final.ContainsKey(id))
                    {
                        Resolve(resource, ancestorIsCandidate: false, own, final, candidate, depth: 0);
                    }

                    if (Write(resource, final[id], candidate[id]))
                    {
                        changed++;
                    }
                }

                if (changed > 0)
                {
                    AssetDatabase.SaveAssets();
                }

                return changed;
            }
            finally
            {
                SaveHookSuspended = previous;
            }
        }

        /// <summary>
        /// 保存されたシーンとその祖先だけを再計算する（保存フック用）。
        /// 兄弟や他の枝は保存済みの値をそのまま使う。全体の正本は <see cref="RecalculateAll"/>。
        /// </summary>
        public static void RecalculateForSavedScene(Scene savedScene)
        {
            if (!savedScene.IsValid() || string.IsNullOrEmpty(savedScene.path))
            {
                return;
            }

            var saved = SceneVolumeSceneReader.FindByScenePath(savedScene.path);
            if (saved == null)
            {
                return;
            }

            var chain = BuildRootFirstChain(saved);

            var previous = SaveHookSuspended;
            SaveHookSuspended = true;
            try
            {
                var own = new Bounds[chain.Count];
                for (var i = 0; i < chain.Count; i++)
                {
                    var isSaved = chain[i].GetInstanceID() == saved.GetInstanceID();
                    own[i] = SceneVolumeSceneReader.ComputeOwnVolume(
                        chain[i], isSaved ? savedScene : (Scene?)null);
                }

                // 候補判定は上から。祖先が候補ならその下は候補にしない。
                var candidate = new bool[chain.Count];
                var ancestorIsCandidate = false;
                for (var i = 0; i < chain.Count; i++)
                {
                    candidate[i] = !SceneVolumeMath.IsEmpty(own[i]) && !ancestorIsCandidate;
                    ancestorIsCandidate |= candidate[i];
                }

                // 合併は下から。鎖の外の子は保存済みの値をそのまま使う。
                var childVolume = default(Bounds);
                var childIsCandidate = false;

                for (var i = chain.Count - 1; i >= 0; i--)
                {
                    var hasChildInChain = i + 1 < chain.Count;
                    var children = new List<(Bounds volume, bool streamByDistance)>();
                    var nodeChildren = chain[i].Children;

                    for (var c = 0; c < nodeChildren.Count; c++)
                    {
                        var child = nodeChildren[c];
                        if (child == null)
                        {
                            continue;
                        }

                        children.Add(
                            hasChildInChain && child.GetInstanceID() == chain[i + 1].GetInstanceID()
                                ? (childVolume, childIsCandidate)
                                : (child.Volume, child.StreamByDistance));
                    }

                    childVolume = SceneVolumeMath.Merge(own[i], children);
                    childIsCandidate = candidate[i];
                    Write(chain[i], childVolume, childIsCandidate);
                }

                AssetDatabase.SaveAssets();
            }
            finally
            {
                SaveHookSuspended = previous;
            }
        }

        /// <summary>ルート → … → 対象 の順に祖先の鎖を組む。</summary>
        private static List<SceneResource> BuildRootFirstChain(SceneResource leaf)
        {
            var chain = new List<SceneResource>();
            var cursor = leaf;

            for (var depth = 0; depth < MaxDepth; depth++)
            {
                chain.Add(cursor);
                var parent = cursor.Parent;
                // SceneResource は UnityEngine.Object。?. / ?? で素通しさせない。
                if (parent == null)
                {
                    break;
                }

                cursor = parent;
            }

            chain.Reverse();
            return chain;
        }

        /// <summary>候補判定（上から）と合併（下から）を 1 回の再帰で行う。</summary>
        private static void Resolve(
            SceneResource resource,
            bool ancestorIsCandidate,
            IReadOnlyDictionary<int, Bounds> own,
            Dictionary<int, Bounds> final,
            Dictionary<int, bool> candidate,
            int depth)
        {
            var id = resource.GetInstanceID();
            if (depth >= MaxDepth || final.ContainsKey(id))
            {
                return;
            }

            var ownVolume = own.TryGetValue(id, out var stored)
                ? stored
                : SceneVolumeSceneReader.ComputeOwnVolume(resource, liveScene: null);

            var isCandidate = !SceneVolumeMath.IsEmpty(ownVolume) && !ancestorIsCandidate;
            candidate[id] = isCandidate;
            // 先に自分を確定扱いにしておくと、親子リンクが循環していても無限再帰しない。
            final[id] = ownVolume;

            var children = new List<(Bounds volume, bool streamByDistance)>();
            var nodeChildren = resource.Children;

            for (var i = 0; i < nodeChildren.Count; i++)
            {
                var child = nodeChildren[i];
                if (child == null)
                {
                    continue;
                }

                Resolve(child, isCandidate || ancestorIsCandidate, own, final, candidate, depth + 1);

                var childId = child.GetInstanceID();
                // MaxDepth 打ち切りで子が未確定のまま戻ることがある。メニュー実行を落とさない。
                if (final.TryGetValue(childId, out var childVolume))
                {
                    children.Add((childVolume, candidate[childId]));
                }
            }

            final[id] = SceneVolumeMath.Merge(ownVolume, children);
        }

        /// <summary>SerializedProperty 経由で書き込む。値が変わったら true。</summary>
        private static bool Write(SceneResource resource, Bounds volume, bool streamByDistance)
        {
            var so = new SerializedObject(resource);
            so.FindProperty("_volume").boundsValue = volume;
            so.FindProperty("_streamByDistance").boolValue = streamByDistance;

            if (!so.ApplyModifiedPropertiesWithoutUndo())
            {
                return false;
            }

            EditorUtility.SetDirty(resource);
            return true;
        }
    }
}
