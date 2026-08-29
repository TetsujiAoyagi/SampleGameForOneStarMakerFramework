#nullable enable

using System;
using UnityEngine;

namespace OneStarMaker.Runtime.Streaming
{
    /// <summary>
    /// 距離政策の候補 1 件（34-ondemand-spatial-policy.md §4 / §5）。
    ///
    /// <para>
    /// identity は**不透明なキー**である。政策層はこれを組み立てず、パースせず、
    /// そのまま <see cref="ISceneStreamingBackend"/> へ渡す。
    /// 距離計算に使うのは <see cref="Volume"/> の中心だけであり、
    /// 体積から identity を復元する経路は作らない（座標が第二の主キーとして復活するため）。
    /// </para>
    ///
    /// <para>
    /// 「距離政策の候補かどうか」のフラグはここに持たない。候補列に「候補でない」を
    /// 混ぜるのは矛盾であり、フラグは §34 §5 のとおりシーンのデータ側
    /// （<c>SceneResource.StreamByDistance</c>）にある。
    /// </para>
    /// </summary>
    public readonly struct StreamingCandidate
    {
        /// <param name="identity">シーンの一意識別子。空文字不可。</param>
        /// <param name="volume">ワールド AABB。空（size == zero）は「空間に属さない」の表明なので候補にできない。</param>
        public StreamingCandidate(string identity, Bounds volume)
        {
            if (string.IsNullOrEmpty(identity))
            {
                throw new ArgumentException("候補の identity は空にできません。", nameof(identity));
            }

            if (volume.size == Vector3.zero)
            {
                throw new ArgumentException(
                    $"候補 '{identity}' の体積が空です。空の体積は「空間に属さない」の表明であり、距離政策の候補にできません。",
                    nameof(volume));
            }

            Identity = identity;
            Volume = volume;
        }

        /// <summary>シーンの一意識別子（不透明）。</summary>
        public string Identity { get; }

        /// <summary>ワールド AABB。距離は中心の XZ を使う（§34 §5。表面距離は採らない）。</summary>
        public Bounds Volume { get; }
    }
}
