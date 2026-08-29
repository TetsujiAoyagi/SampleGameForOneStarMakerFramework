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
    /// `.unity` の中身から <see cref="SceneResource"/> のワールド体積を焼く
    /// （34-ondemand-spatial-policy.md §5）。
    ///
    /// <para>
    /// <b>書くのは体積だけ。</b> <c>_streamByDistance</c> には触らない。
    /// 「距離政策の候補か」は幾何から導出できる事実ではなく<b>決定</b>であり（§34 §5）、
    /// それを知っているのは作業単位を焼く生成器（<c>WorldCellGenerator</c> / Environment 側）である。
    /// ここで「体積が空でなければ候補」と導出すると、Renderer を持つだけの
    /// Player や UI のシーンまで候補になる（実測で誤爆した）。
    /// </para>
    ///
    /// <para>
    /// 合併では**候補でない子だけ**を親へ畳む（§34 §6）。畳むかどうかの判断材料として
    /// フラグを<b>読む</b>のはここの仕事である。書かないだけである。
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
        /// 全 <see cref="SceneResource"/> の体積を再計算する。
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
                var own = new Dictionary<ulong, Bounds>(resources.Count);
                for (var i = 0; i < resources.Count; i++)
                {
                    own[IdOf(resources[i])] =
                        SceneVolumeSceneReader.ComputeOwnVolume(resources[i], liveScene: null);
                }

                var final = new Dictionary<ulong, Bounds>(resources.Count);

                for (var i = 0; i < resources.Count; i++)
                {
                    if (resources[i].Parent == null)
                    {
                        Resolve(resources[i], own, final, depth: 0);
                    }
                }

                var changed = 0;
                for (var i = 0; i < resources.Count; i++)
                {
                    var resource = resources[i];
                    var id = IdOf(resource);
                    // 親リンクが壊れていてルートから到達できなかったものも取りこぼさない。
                    if (!final.ContainsKey(id))
                    {
                        Resolve(resource, own, final, depth: 0);
                    }

                    if (Write(resource, final[id]))
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
                    var isSaved = IdOf(chain[i]) == IdOf(saved);
                    own[i] = SceneVolumeSceneReader.ComputeOwnVolume(
                        chain[i], isSaved ? savedScene : (Scene?)null);
                }

                // 合併は下から。鎖の外の子は保存済みの値をそのまま使う。
                var childVolume = default(Bounds);
                var childIsCandidate = false;
                var changed = false;

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
                            hasChildInChain && IdOf(child) == IdOf(chain[i + 1])
                                ? (childVolume, childIsCandidate)
                                : (child.Volume, child.StreamByDistance));
                    }

                    childVolume = SceneVolumeMath.Merge(own[i], children);
                    childIsCandidate = chain[i].StreamByDistance;
                    changed |= Write(chain[i], childVolume);
                }

                // 体積が動いていない保存（マテリアルだけ、空のシーン等）で
                // プロジェクトを dirty にしない。RecalculateAll と同じ条件にそろえる。
                if (changed)
                {
                    AssetDatabase.SaveAssets();
                }
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

        /// <summary>子から順に体積を確定させる。候補フラグは読むだけで書き換えない。</summary>
        private static void Resolve(
            SceneResource resource,
            IReadOnlyDictionary<ulong, Bounds> own,
            Dictionary<ulong, Bounds> final,
            int depth)
        {
            var id = IdOf(resource);
            if (depth >= MaxDepth || final.ContainsKey(id))
            {
                return;
            }

            var ownVolume = own.TryGetValue(id, out var stored)
                ? stored
                : SceneVolumeSceneReader.ComputeOwnVolume(resource, liveScene: null);

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

                Resolve(child, own, final, depth + 1);

                var childId = IdOf(child);
                // MaxDepth 打ち切りで子が未確定のまま戻ることがある。メニュー実行を落とさない。
                if (final.TryGetValue(childId, out var childVolume))
                {
                    children.Add((childVolume, child.StreamByDistance));
                }
            }

            final[id] = SceneVolumeMath.Merge(ownVolume, children);
        }

        /// <summary>
        /// 辞書キー用の安定 ID。`GetInstanceID` は Unity 6.5 で obsolete（CS0619 = エラー）なので使わない。
        /// </summary>
        private static ulong IdOf(SceneResource resource) => EntityId.ToULong(resource.GetEntityId());

        /// <summary>
        /// SerializedProperty 経由で体積だけを書き込む。値が変わったら true。
        /// <c>_streamByDistance</c> は生成器が持つ決定なので、ここでは触らない。
        /// </summary>
        private static bool Write(SceneResource resource, Bounds volume)
        {
            var so = new SerializedObject(resource);
            so.FindProperty("_volume").boundsValue = volume;

            if (!so.ApplyModifiedPropertiesWithoutUndo())
            {
                return false;
            }

            EditorUtility.SetDirty(resource);
            return true;
        }
    }
}
