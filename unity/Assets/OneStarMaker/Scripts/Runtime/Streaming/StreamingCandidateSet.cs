#nullable enable

using System;
using System.Collections.Generic;

namespace OneStarMaker.Runtime.Streaming
{
    /// <summary>
    /// 距離政策の候補集合（34-ondemand-spatial-policy.md §6）。
    ///
    /// <para>
    /// <b>寿命が短いほう。</b> 候補が変わったら丸ごと作り直す型であり、
    /// 半径のようなチューニング値（<see cref="StreamingPolicySettings"/>）とは同居させない。
    /// 同居させると、候補集合だけ差し替えたいときに半径まで道連れで作り直すことになる。
    /// </para>
    ///
    /// <para>
    /// 矩形も格子も、候補が何を意味するかも知らない。列挙は呼び出し側の責務である。
    /// 同じ体積を複数 identity が持ってよい（§34 §6）。逆に identity の重複は
    /// 必ず設定ミスなので構築時に弾く。
    /// </para>
    /// </summary>
    public sealed class StreamingCandidateSet
    {
        /// <param name="candidates">候補列（1 件以上・identity 重複なし）。</param>
        public StreamingCandidateSet(IReadOnlyList<StreamingCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (candidates.Count == 0)
            {
                throw new ArgumentException("候補集合は 1 件以上である必要があります。", nameof(candidates));
            }

            // 呼び出し側の配列を後から書き換えられないように防御的コピーを取る。
            var copy = new StreamingCandidate[candidates.Count];
            var seen = new HashSet<string>(candidates.Count, StringComparer.Ordinal);

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (string.IsNullOrEmpty(candidate.Identity))
                {
                    throw new ArgumentException(
                        $"候補 [{i}] が既定値です。StreamingCandidate はコンストラクタ経由で作ってください。",
                        nameof(candidates));
                }

                if (!seen.Add(candidate.Identity))
                {
                    throw new ArgumentException(
                        $"候補 identity が重複しています: '{candidate.Identity}'。",
                        nameof(candidates));
                }

                copy[i] = candidate;
            }

            Candidates = copy;
        }

        /// <summary>
        /// 候補列。走査順は与えられた順であり、政策の結果には影響しない
        /// （desired は距離昇順に並べ直され、同距離は identity の序数順で決まる）。
        /// </summary>
        public IReadOnlyList<StreamingCandidate> Candidates { get; }
    }
}
