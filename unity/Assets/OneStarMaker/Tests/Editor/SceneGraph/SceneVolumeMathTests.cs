#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Editor.SceneGraph;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.SceneGraph
{
    /// <summary>
    /// 移行 M-1: シーン体積の合併規則（34-ondemand-spatial-policy.md §5 / §6）。
    /// 純関数だけを検証する。シーンの開閉と SerializedProperty 書き込みは対象外。
    /// </summary>
    [TestFixture]
    public sealed class SceneVolumeMathTests
    {
        private static Bounds Box(float centerX, float centerZ, float size)
            => new(new Vector3(centerX, 0f, centerZ), new Vector3(size, size, size));

        private static readonly Bounds Empty = new(new Vector3(999f, 999f, 999f), Vector3.zero);

        // ═══════════════════════════════════════════
        //  IsEmpty / TryUnion
        // ═══════════════════════════════════════════

        [Test]
        public void IsEmpty_ZeroSize_IsTrueRegardlessOfCenter()
        {
            // 「空間に属さない」の表明は寸法で決まる。中心がどこにあっても関係ない。
            Assert.IsTrue(SceneVolumeMath.IsEmpty(Empty));
            Assert.IsFalse(SceneVolumeMath.IsEmpty(Box(0f, 0f, 1f)));
        }

        [Test]
        public void TryUnion_EmptyList_ReturnsFalse()
        {
            Assert.IsFalse(SceneVolumeMath.TryUnion(new List<Bounds>(), out _));
        }

        [Test]
        public void TryUnion_AllPartsEmpty_ReturnsFalse()
        {
            Assert.IsFalse(SceneVolumeMath.TryUnion(new List<Bounds> { Empty, Empty }, out _));
        }

        [Test]
        public void TryUnion_EmptyPart_DoesNotStretchResultTowardIt()
        {
            // 空の体積を Encapsulate すると、その中心まで箱が伸びて中心がずれる。
            var parts = new List<Bounds> { Box(100f, 100f, 10f), Empty };

            Assert.IsTrue(SceneVolumeMath.TryUnion(parts, out var union));
            Assert.AreEqual(new Vector3(100f, 0f, 100f), union.center);
            Assert.AreEqual(new Vector3(10f, 10f, 10f), union.size);
        }

        [Test]
        public void TryUnion_TwoBoxes_EncapsulatesBoth()
        {
            // (0,0) の 10 角箱 と (100,0) の 10 角箱 → x は -5..105、中心 50
            var parts = new List<Bounds> { Box(0f, 0f, 10f), Box(100f, 0f, 10f) };

            Assert.IsTrue(SceneVolumeMath.TryUnion(parts, out var union));
            Assert.AreEqual(50f, union.center.x, 0.001f);
            Assert.AreEqual(110f, union.size.x, 0.001f);
        }

        // ═══════════════════════════════════════════
        //  Merge（§34 §6: 候補でない子だけ畳む）
        // ═══════════════════════════════════════════

        [Test]
        public void Merge_NonCandidateChild_IsFoldedIntoParent()
        {
            // 現行の南辺セル: Ground が Environment 子側にあり、親を畳まないと中心がずれる。
            var own = Box(0f, 0f, 10f);
            var children = new List<(Bounds volume, bool streamByDistance)>
            {
                (Box(100f, 0f, 10f), false),
            };

            var merged = SceneVolumeMath.Merge(own, children);

            Assert.AreEqual(50f, merged.center.x, 0.001f, "候補でない子は親の体積へ畳む");
        }

        [Test]
        public void Merge_CandidateChild_IsNotFolded()
        {
            // 候補である子を畳むと、親の中心が兄弟候補に引きずられる。
            var own = Box(0f, 0f, 10f);
            var children = new List<(Bounds volume, bool streamByDistance)>
            {
                (Box(100f, 0f, 10f), true),
            };

            var merged = SceneVolumeMath.Merge(own, children);

            Assert.AreEqual(own.center, merged.center);
            Assert.AreEqual(own.size, merged.size);
        }

        [Test]
        public void Merge_EmptyOwnWithNonCandidateChild_TakesChildVolume()
        {
            // 自分のシーンに何も無くても、畳んだ子の体積で空間に属する。
            var children = new List<(Bounds volume, bool streamByDistance)>
            {
                (Box(100f, 0f, 10f), false),
            };

            var merged = SceneVolumeMath.Merge(Empty, children);

            Assert.IsFalse(SceneVolumeMath.IsEmpty(merged));
            Assert.AreEqual(new Vector3(100f, 0f, 0f), merged.center);
        }

        [Test]
        public void Merge_EmptyOwnWithOnlyCandidateChildren_StaysEmpty()
        {
            // World のような容れ物は空のまま。子（候補）を吸って空間に属してはいけない。
            var children = new List<(Bounds volume, bool streamByDistance)>
            {
                (Box(100f, 0f, 10f), true),
                (Box(300f, 0f, 10f), true),
            };

            var merged = SceneVolumeMath.Merge(Empty, children);

            Assert.IsTrue(SceneVolumeMath.IsEmpty(merged));
        }

        [Test]
        public void Merge_NoChildren_ReturnsOwn()
        {
            var own = Box(42f, 7f, 10f);

            var merged = SceneVolumeMath.Merge(own, null);

            Assert.AreEqual(own.center, merged.center);
            Assert.AreEqual(own.size, merged.size);
        }
    }
}
