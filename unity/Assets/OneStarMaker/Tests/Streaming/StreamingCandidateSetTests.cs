#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Runtime.Streaming;
using UnityEngine;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// 移行 M-1: 候補集合とチューニング値を寿命で割った 2 型の入力検証。
    /// 旧 <c>StreamingConfig</c> が 1 型で見ていた不変条件を、それぞれの持ち主へ振り分けている。
    /// </summary>
    [TestFixture]
    public class StreamingCandidateSetTests
    {
        private static Bounds UnitVolume(float x = 0f, float z = 0f)
            => new(new Vector3(x, 0f, z), new Vector3(10f, 10f, 10f));

        // ═══════════════════════════════════════════
        //  StreamingCandidate
        // ═══════════════════════════════════════════

        [Test]
        public void Candidate_EmptyVolume_Throws()
        {
            // 空の体積は「空間に属さない」の表明（§34 §5）。距離政策の候補にはできない。
            Assert.Throws<ArgumentException>(
                () => new StreamingCandidate("Cell_0_0", new Bounds(Vector3.zero, Vector3.zero)));
        }

        [Test]
        public void Candidate_EmptyIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => new StreamingCandidate(string.Empty, UnitVolume()));
        }

        // ═══════════════════════════════════════════
        //  StreamingCandidateSet
        // ═══════════════════════════════════════════

        [Test]
        public void CandidateSet_EmptyList_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new StreamingCandidateSet(Array.Empty<StreamingCandidate>()));
        }

        [Test]
        public void CandidateSet_DuplicateIdentity_Throws()
        {
            // 同じ体積を複数 identity が共有する使い方がある以上、重複は必ず設定ミスである（W-2 の芽）。
            var candidates = new List<StreamingCandidate>
            {
                new("Cell_0_0", UnitVolume()),
                new("Cell_0_0", UnitVolume(x: 100f)),
            };

            Assert.Throws<ArgumentException>(() => new StreamingCandidateSet(candidates));
        }

        [Test]
        public void CandidateSet_SameVolumeDifferentIdentity_IsAllowed()
        {
            // 同じ AABB を複数 identity が持ってよい（§34 §6。候補集合の差し替えの土台）。
            var volume = UnitVolume();
            var set = new StreamingCandidateSet(new List<StreamingCandidate>
            {
                new("Alpha_Cell_0_0", volume),
                new("Beta_Cell_0_0", volume),
            });

            Assert.AreEqual(2, set.Candidates.Count);
        }

        [Test]
        public void CandidateSet_DefensivelyCopiesInput()
        {
            var source = new List<StreamingCandidate> { new("Cell_0_0", UnitVolume()) };
            var set = new StreamingCandidateSet(source);

            source[0] = new StreamingCandidate("Cell_9_9", UnitVolume(x: 900f));

            Assert.AreEqual("Cell_0_0", set.Candidates[0].Identity,
                "構築後に呼び出し側のリストを書き換えても候補集合は変わらない");
        }

        // ═══════════════════════════════════════════
        //  StreamingPolicySettings
        // ═══════════════════════════════════════════

        [Test]
        public void PolicySettings_UnloadRadiusNotGreaterThanLoadRadius_Throws()
        {
            // ヒステリシス幅が 0 以下になるとロード直後にアンロードして振動する。
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new StreamingPolicySettings(loadRadius: 100f, unloadRadius: 100f, maxInFlight: 2));
        }

        [Test]
        public void PolicySettings_NonPositiveMaxInFlight_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new StreamingPolicySettings(loadRadius: 100f, unloadRadius: 200f, maxInFlight: 0));
        }
    }
}
