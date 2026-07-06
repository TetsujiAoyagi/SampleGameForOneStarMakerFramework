#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.Streaming;
using UnityEngine;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// T-06: WorldStreamingController ポリシー層のレッドテスト（FakeBackend 使用・同期的に決定的）。
    /// FakeBackend による純 C# テスト。Controller のポリシー（desired/retain・G-6 再照合・in-flight 上限）を検証する。
    /// </summary>
    [TestFixture]
    public class WorldStreamingControllerTests
    {
        // ═══════════════════════════════════════════
        //  テスト用ヘルパー
        // ═══════════════════════════════════════════

        private static StreamingConfig CreateConfig(
            int gridWidth = 5,
            int gridHeight = 5,
            float cellSize = 100f,
            float loadRadius = 150f,
            float unloadRadius = 250f,
            int maxInFlight = 4)
        {
            var grid = new CellGridConfig(Vector3.zero, cellSize, height: 10f);
            return new StreamingConfig(grid, gridWidth, gridHeight, loadRadius, unloadRadius, maxInFlight);
        }

        private static Vector3 CellCenter(int x, int y, in CellGridConfig grid)
        {
            return grid.Origin + new Vector3(
                (x + 0.5f) * grid.CellSize,
                0f,
                (y + 0.5f) * grid.CellSize);
        }

        private static float XzDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// 注視点と半径から desired set（グリッド範囲内・XZ 距離 <= radius）を計算する。
        /// Controller 実装の期待値算出に使用。
        /// </summary>
        private static HashSet<string> ComputeCellsWithinRadius(
            Vector3 focus,
            StreamingConfig config,
            float radius)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var grid = config.Grid;

            for (var x = 0; x < config.GridWidth; x++)
            {
                for (var y = 0; y < config.GridHeight; y++)
                {
                    var center = CellCenter(x, y, grid);
                    if (XzDistance(focus, center) <= radius)
                    {
                        result.Add(CellIdentity.Format(x, y));
                    }
                }
            }

            return result;
        }

        private static void MarkAllAddsAsLoaded(FakeStreamingBackend backend)
        {
            foreach (var call in backend.AddCalls)
            {
                backend.SetLoaded(call.CellId, loaded: true);
            }
        }

        // ═══════════════════════════════════════════
        //  desired set / ロード半径
        // ═══════════════════════════════════════════

        [Test]
        public void Tick_FocusInGrid_RequestsCellsWithinLoadRadius()
        {
            var config = CreateConfig(loadRadius: 120f, unloadRadius: 200f);
            var backend = new FakeStreamingBackend();
            var controller = new WorldStreamingController(config, backend);

            var focus = CellCenter(2, 2, config.Grid);
            controller.Tick(focus);

            var requested = backend.AddCalls.Select(c => c.CellId).ToHashSet(StringComparer.Ordinal);
            var expected = ComputeCellsWithinRadius(focus, config, config.LoadRadius);

            CollectionAssert.AreEquivalent(expected, requested);
        }

        // ═══════════════════════════════════════════
        //  アンロード半径
        // ═══════════════════════════════════════════

        [Test]
        public void Tick_CellBeyondUnloadRadius_IsRemoved()
        {
            var config = CreateConfig(loadRadius: 80f, unloadRadius: 120f, maxInFlight: 8);
            var backend = new FakeStreamingBackend();
            var controller = new WorldStreamingController(config, backend);

            var nearFocus = CellCenter(2, 2, config.Grid);
            controller.Tick(nearFocus);
            MarkAllAddsAsLoaded(backend);
            backend.ClearHistory();

            var farFocus = CellCenter(0, 0, config.Grid);
            controller.Tick(farFocus);

            var removed = backend.RemoveCalls.Select(c => c.CellId).ToList();
            Assert.That(removed, Does.Contain("Cell_2_2"), "アンロード半径外になったセルは RequestRemove される");
        }

        // ═══════════════════════════════════════════
        //  ヒステリシス（ロード半径外・アンロード半径内 → 何も発火しない）
        // ═══════════════════════════════════════════

        [Test]
        public void Tick_CellBetweenRadii_IsRetained()
        {
            var config = CreateConfig(loadRadius: 40f, unloadRadius: 240f);
            var backend = new FakeStreamingBackend();
            var controller = new WorldStreamingController(config, backend);

            var cell22Center = CellCenter(2, 2, config.Grid);
            controller.Tick(cell22Center);
            MarkAllAddsAsLoaded(backend);
            backend.ClearHistory();

            // focus (300,0,250): Cell_2_2 中心 (250,0,250) = 50m（40 超・240 内）、
            // Cell_3_2 中心 (350,0,250) = 50m（40 超 → desired 外）。何も発火しない。
            var focusBetween = cell22Center + new Vector3(50f, 0f, 0f);
            controller.Tick(focusBetween);

            Assert.IsEmpty(backend.CallHistory, "ヒステリシス帯では RequestAdd/Remove は発火しない");
        }

        // ═══════════════════════════════════════════
        //  差分発火（同一 focus で冗長呼び出しなし）
        // ═══════════════════════════════════════════

        [Test]
        public void Tick_SameFocusTwice_NoRedundantRequests()
        {
            var config = CreateConfig();
            var backend = new FakeStreamingBackend();
            var controller = new WorldStreamingController(config, backend);

            var focus = CellCenter(2, 2, config.Grid);
            controller.Tick(focus);
            MarkAllAddsAsLoaded(backend);

            var historyCountAfterFirst = backend.CallHistory.Count;
            controller.Tick(focus);

            Assert.AreEqual(historyCountAfterFirst, backend.CallHistory.Count,
                "同一 focus で 2 回 Tick してもバックエンド呼び出しは増えない");
        }

        // ═══════════════════════════════════════════
        //  priority / 発行順（focus に近い順）
        // ═══════════════════════════════════════════

        [Test]
        public void Tick_LoadRequests_OrderedByDistanceToFocus()
        {
            var config = CreateConfig(loadRadius: 180f, unloadRadius: 280f);
            var backend = new FakeStreamingBackend();
            var controller = new WorldStreamingController(config, backend);

            var focus = CellCenter(2, 2, config.Grid);
            controller.Tick(focus);

            var adds = backend.AddCalls.ToList();
            Assert.Greater(adds.Count, 1, "複数セルがロード対象になる設定であること");

            var grid = config.Grid;
            var distances = adds
                .Select(c =>
                {
                    Assert.IsTrue(CellIdentity.TryParse(c.CellId, out var coord));
                    return XzDistance(focus, CellCenter(coord.x, coord.y, grid));
                })
                .ToList();

            for (var i = 1; i < distances.Count; i++)
            {
                Assert.LessOrEqual(distances[i - 1], distances[i],
                    "RequestAdd の発行順は focus への距離昇順である");
            }

            CollectionAssert.AreEqual(
                Enumerable.Range(0, adds.Count).ToList(),
                adds.Select(c => c.Priority).ToList(),
                "priority は Tick 内の発行順序数（0 始まりの距離順ランク）");
        }

        // ═══════════════════════════════════════════
        //  in-flight 上限
        // ═══════════════════════════════════════════

        [Test]
        public void Tick_InFlightLimit_Respected()
        {
            var config = CreateConfig(loadRadius: 250f, unloadRadius: 350f, maxInFlight: 2);
            var backend = new FakeStreamingBackend { AutoCompleteRequestAdd = false };
            var controller = new WorldStreamingController(config, backend);

            var focus = CellCenter(2, 2, config.Grid);
            controller.Tick(focus);

            var firstTickAdds = backend.AddCalls.ToList();
            Assert.LessOrEqual(firstTickAdds.Count, 2, "maxInFlight=2 のとき未完了 RequestAdd は 2 件以下");
            Assert.AreEqual(2, firstTickAdds.Count, "desired が 2 件超のとき最初の Tick で 2 件発行される");

            var desired = ComputeCellsWithinRadius(focus, config, config.LoadRadius);
            Assert.Greater(desired.Count, 2, "テスト前提: desired が maxInFlight を超える");

            var pendingCell = firstTickAdds[0].CellId;
            backend.CompleteRequestAdd(pendingCell);
            backend.SetLoaded(pendingCell, loaded: true);

            backend.ClearHistory();
            controller.Tick(focus);

            Assert.AreEqual(1, backend.AddCalls.Count(),
                "1 件完了後の次 Tick でキューから 1 件追加発行される");
            Assert.IsFalse(backend.AddCalls.Any(c => c.CellId == pendingCell),
                "既にロード済みのセルは再発行しない");
        }

        // ═══════════════════════════════════════════
        //  キュー取り消し（desired から外れたセルは発行されない）
        // ═══════════════════════════════════════════

        [Test]
        public void Tick_QueuedCellLeavesDesired_IsNotIssued()
        {
            var config = CreateConfig(loadRadius: 250f, unloadRadius: 350f, maxInFlight: 1);
            var backend = new FakeStreamingBackend { AutoCompleteRequestAdd = false };
            var controller = new WorldStreamingController(config, backend);

            var centerFocus = CellCenter(2, 2, config.Grid);
            controller.Tick(centerFocus);

            var firstTickAdds = backend.AddCalls.ToList();
            var inFlightCell = firstTickAdds.First().CellId;
            Assert.IsTrue(backend.IsRequestAddPending(inFlightCell));

            const string queuedFarCell = "Cell_3_3";
            Assert.IsFalse(firstTickAdds.Any(c => c.CellId == queuedFarCell),
                "maxInFlight=1 では初回 Tick で最近傍セルのみ発行され、Cell_3_3 はキュー待ち");

            // 隅へ focus を移動: キュー待ちの遠方セル（Cell_3_3）は desired から外れる
            var centerDesired = ComputeCellsWithinRadius(centerFocus, config, config.LoadRadius);
            Assert.IsTrue(centerDesired.Contains(queuedFarCell), "テスト前提: 中心 focus では Cell_3_3 が desired 内");

            var cornerFocus = CellCenter(0, 0, config.Grid);
            var cornerDesired = ComputeCellsWithinRadius(cornerFocus, config, config.LoadRadius);
            Assert.IsFalse(cornerDesired.Contains(queuedFarCell), "Cell_3_3 は隅 focus では desired 外");

            backend.ClearHistory();
            controller.Tick(cornerFocus);

            Assert.IsFalse(backend.AddCalls.Any(c => c.CellId == queuedFarCell),
                "desired から外れたキュー待ちセルは発行されない");

            backend.CompleteRequestAdd(inFlightCell);
            backend.ClearHistory();
            controller.Tick(cornerFocus);

            Assert.IsFalse(backend.AddCalls.Any(c => c.CellId == queuedFarCell),
                "完了後も desired 外セルは発行されない");
        }

        // ═══════════════════════════════════════════
        //  G-6 再照合（RequestAdd 完了だが IsLoaded=false → 次 Tick で再発行）
        // ═══════════════════════════════════════════

        [Test]
        public void Tick_AddCompletedButNotLoaded_ReissuesNextTick()
        {
            var config = CreateConfig(loadRadius: 80f, unloadRadius: 160f, maxInFlight: 4);
            var backend = new FakeStreamingBackend();
            var controller = new WorldStreamingController(config, backend);

            var focus = CellCenter(2, 2, config.Grid);
            controller.Tick(focus);

            // G-6: RequestAdd は正常完了するが IsLoaded は false のまま（Stable 未到達）
            Assert.IsFalse(backend.IsLoaded("Cell_2_2"), "テスト前提: 完了後も未ロード");
            var firstAddCount = backend.AddCalls.Count(c => c.CellId == "Cell_2_2");
            Assert.Greater(firstAddCount, 0, "初回 Tick で Cell_2_2 への RequestAdd が発行される");

            backend.ClearHistory();
            controller.Tick(focus);

            Assert.Greater(backend.AddCalls.Count(c => c.CellId == "Cell_2_2"), 0,
                "IsLoaded=false のセルは次 Tick で RequestAdd が再発行される（G-6 再照合）");
        }

        // ═══════════════════════════════════════════
        //  in-flight 中の focus 移動 → desired へ収束
        // ═══════════════════════════════════════════

        [Test]
        public void Tick_FocusMovesDuringInFlight_ConvergesToDesired()
        {
            var config = CreateConfig(loadRadius: 120f, unloadRadius: 220f, maxInFlight: 2);
            var backend = new FakeStreamingBackend { AutoCompleteRequestAdd = false };
            var controller = new WorldStreamingController(config, backend);

            var focusA = CellCenter(1, 1, config.Grid);
            controller.Tick(focusA);
            var inFlightFromA = backend.AddCalls.Select(c => c.CellId).ToHashSet(StringComparer.Ordinal);

            var focusB = CellCenter(3, 3, config.Grid);
            var desiredB = ComputeCellsWithinRadius(focusB, config, config.LoadRadius);
            Assert.IsFalse(desiredB.SetEquals(inFlightFromA), "focus B の desired は focus A と異なる");

            backend.ClearHistory();
            controller.Tick(focusB);

            // focus B で desired 外になった in-flight セルは RequestRemove される
            foreach (var cellId in inFlightFromA)
            {
                if (!desiredB.Contains(cellId))
                {
                    Assert.That(backend.RemoveCalls.Select(c => c.CellId), Does.Contain(cellId),
                        $"desired 外になった {cellId} は RequestRemove される");
                }
            }

            var converged = false;
            for (var cycle = 0; cycle < 10; cycle++)
            {
                // 良性バックエンドをシミュレート: 保留中の Add を全て完了させ、ロード済みにする
                // （ループ突入前から保留中のものも含む。完了させないと in-flight が飽和したまま
                //   呼び出しゼロ = 偽の収束になってしまう）
                foreach (var cellId in backend.PendingAddCellIds.ToList())
                {
                    backend.CompleteRequestAdd(cellId);
                    backend.SetLoaded(cellId, loaded: true);
                }

                backend.ClearHistory();
                controller.Tick(focusB);

                if (backend.CallHistory.Count == 0)
                {
                    converged = true;
                    break;
                }
            }

            Assert.IsTrue(converged, "10 サイクル以内に Tick がバックエンド呼び出しゼロで収束する");

            for (var x = 0; x < config.GridWidth; x++)
            {
                for (var y = 0; y < config.GridHeight; y++)
                {
                    var cellId = CellIdentity.Format(x, y);
                    Assert.AreEqual(
                        desiredB.Contains(cellId),
                        backend.IsLoaded(cellId),
                        $"収束後のロード済み集合は desiredB と一致: {cellId}");
                }
            }

            backend.ClearHistory();
            controller.Tick(focusB);
            Assert.IsEmpty(backend.CallHistory, "収束後の Tick でも呼び出しゼロ");
        }

        // ═══════════════════════════════════════════
        //  Add/Remove 競合（Add 保留中に desired 外 → 復帰 → Remove 完了前の再 Add 抑止）
        // ═══════════════════════════════════════════

        [Test]
        public void Tick_InFlightAddLeavesDesiredThenReturns_NoDoubleRequestAdd()
        {
            // loadRadius 80 / cellSize 100: focus をセル中心に置くとそのセルのみ desired。
            // unloadRadius 120: 隅 focus では Cell_2_2 中心まで約 283m → retain 外。
            var config = CreateConfig(loadRadius: 80f, unloadRadius: 120f, maxInFlight: 4);
            var backend = new FakeStreamingBackend
            {
                AutoCompleteRequestAdd = false,
                AutoCompleteRequestRemove = false,
            };
            var controller = new WorldStreamingController(config, backend);

            var focusHome = CellCenter(2, 2, config.Grid);
            controller.Tick(focusHome);
            Assert.IsTrue(backend.IsRequestAddPending("Cell_2_2"), "テスト前提: Cell_2_2 の Add が保留中");

            // Add 保留のまま focus が離脱 → retain 外の in-flight セルへ RequestRemove が発行される
            var focusAway = CellCenter(0, 0, config.Grid);
            backend.ClearHistory();
            controller.Tick(focusAway);
            Assert.That(backend.RemoveCalls.Select(c => c.CellId), Does.Contain("Cell_2_2"),
                "Add 保留中でも retain 外になったセルは RequestRemove される");

            // Add / Remove とも未完了のまま focus が復帰 → 再 RequestAdd は抑止される
            // （FakeBackend は同一セルへの二重 RequestAdd で例外を投げるため、発行されれば失敗する）
            backend.ClearHistory();
            controller.Tick(focusHome);
            Assert.IsFalse(backend.AddCalls.Any(c => c.CellId == "Cell_2_2"),
                "Add/Remove が in-flight の間は同一セルへ再 RequestAdd しない");

            // 両方完了後の次 Tick で G-6 再照合により 1 回だけ再発行される
            backend.CompleteRequestAdd("Cell_2_2");
            backend.CompleteRequestRemove("Cell_2_2");
            Assert.IsFalse(backend.IsLoaded("Cell_2_2"), "Remove 完了後は未ロード状態");

            backend.ClearHistory();
            controller.Tick(focusHome);
            Assert.AreEqual(1, backend.AddCalls.Count(c => c.CellId == "Cell_2_2"),
                "Add/Remove 完了後の Tick で RequestAdd がちょうど 1 回再発行される（G-6）");
        }
    }
}
