#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// シーン体積（34-ondemand-spatial-policy.md §5）の合併規則。**純関数のみ。**
    ///
    /// <para>
    /// Unity を開かないと動かない I/O は <see cref="SceneVolumeRecalculator"/> にあり、
    /// テストが触るのはこちらだけである。
    /// </para>
    /// </summary>
    public static class SceneVolumeMath
    {
        /// <summary>
        /// 「空間に属さない」体積か（§34 §5）。原点の点ではなく、寸法ゼロを表明とみなす。
        /// </summary>
        public static bool IsEmpty(in Bounds volume) => volume.size == Vector3.zero;

        /// <summary>
        /// 体積列を 1 つの AABB へ合併する。
        /// </summary>
        /// <param name="parts">合併する体積列。空の体積は寄与しない。</param>
        /// <param name="result">合併結果。false のときは既定値。</param>
        /// <returns>1 件以上が寄与したら true。</returns>
        public static bool TryUnion(IReadOnlyList<Bounds>? parts, out Bounds result)
        {
            result = default;
            if (parts == null)
            {
                return false;
            }

            var found = false;
            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                // 空の体積は「そこに何も無い」であって「原点に点がある」ではない。
                // Encapsulate すると合併結果が原点まで引き伸ばされるため寄与させない。
                if (IsEmpty(part))
                {
                    continue;
                }

                if (!found)
                {
                    result = part;
                    found = true;
                    continue;
                }

                result.Encapsulate(part);
            }

            return found;
        }

        /// <summary>
        /// 自分の体積へ、**距離政策の候補でない子**の体積だけを畳み込む（§34 §6）。
        /// </summary>
        /// <remarks>
        /// 候補である子を畳むと、親の中心が兄弟候補に引きずられる。
        /// 職種分割の子（現状の Environment）は候補ではないので、空間的には同じ作業単位として畳む。
        /// </remarks>
        /// <param name="own">自分のシーンだけから求めた体積。空でもよい。</param>
        /// <param name="children">子の（体積, 距離政策の候補か）。</param>
        /// <returns>合併後の体積。寄与が 1 つも無ければ空。</returns>
        public static Bounds Merge(
            Bounds own,
            IReadOnlyList<(Bounds volume, bool streamByDistance)>? children)
        {
            var parts = new List<Bounds> { own };

            if (children != null)
            {
                for (var i = 0; i < children.Count; i++)
                {
                    var (volume, streamByDistance) = children[i];
                    if (streamByDistance)
                    {
                        continue;
                    }

                    parts.Add(volume);
                }
            }

            return TryUnion(parts, out var merged) ? merged : default;
        }
    }
}
