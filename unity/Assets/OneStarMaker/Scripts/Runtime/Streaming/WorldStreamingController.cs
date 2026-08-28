#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;

namespace OneStarMaker.Runtime.Streaming
{
    /// <summary>
    /// セルストリーミングのポリシー層（21-scene-streaming.md §8 / D-3）。
    /// desired set 計算・差分発火・ヒステリシス・in-flight 上限・priority を担う純 C# クラス。
    /// UpdateSystem への接続は薄いアダプタに分離し、テストでは <see cref="Tick"/> を直接呼ぶ。
    /// </summary>
    public sealed class WorldStreamingController
    {
        private readonly HashSet<string> _inFlightAddCells = new(StringComparer.Ordinal);
        private readonly HashSet<string> _inFlightRemoveCells = new(StringComparer.Ordinal);
        // Controller は状態を持つ同期ポリシー層であり、再入・スレッドセーフはスコープ外。
        // hot path の Tick(Vector3) では、この 1 要素バッファを再利用して余分な GC を増やさない。
        private readonly Vector3[] _singleFocusBuffer = new Vector3[1];

        /// <summary>
        /// Controller を構築する。
        /// </summary>
        /// <param name="config">ポリシーパラメータ。</param>
        /// <param name="backend">ロード/アンロード要求先。</param>
        public WorldStreamingController(StreamingConfig config, ISceneStreamingBackend backend)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        /// <summary>ポリシーパラメータ。</summary>
        public StreamingConfig Config { get; }

        /// <summary>ロード/アンロード要求先。</summary>
        public ISceneStreamingBackend Backend { get; }

        /// <summary>
        /// 単一注視点を受け取り、desired/retain 集合の差分をバックエンドへ発火する。
        /// 毎 Tick <see cref="ISceneStreamingBackend.IsLoaded"/> で実状態と再照合し自己修復する（G-6）。
        /// </summary>
        /// <param name="focusPosition">注視点のワールド座標。</param>
        public void Tick(Vector3 focusPosition)
        {
            _singleFocusBuffer[0] = focusPosition;
            Tick(_singleFocusBuffer);
        }

        /// <summary>
        /// 複数注視点を受け取り、desired/retain 集合の差分をバックエンドへ発火する（CAM-08）。
        /// desired = 各 focus の loadRadius 内セルの和集合。
        /// retain = 各 focus の unloadRadius 内セルの和集合。
        /// priority はセル中心から最寄り focus への距離昇順。
        /// </summary>
        /// <param name="focusPositions">注視点のワールド座標列（1 件以上）。</param>
        public void Tick(IReadOnlyList<Vector3> focusPositions)
        {
            if (focusPositions is null)
            {
                throw new ArgumentNullException(nameof(focusPositions));
            }

            if (focusPositions.Count == 0)
            {
                throw new ArgumentException("注視点は 1 件以上必要です。", nameof(focusPositions));
            }

            var grid = Config.Grid;
            var desiredOrdered = new List<(string cellId, float distance)>();
            var retain = new HashSet<string>(StringComparer.Ordinal);
            var loaded = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < Config.Cells.Count; i++)
            {
                var cell = Config.Cells[i];
                var cellId = CellIdentity.Format(cell.x, cell.y);
                var center = GetCellCenter(cell.x, cell.y, grid);
                var nearestDistance = GetNearestFocusDistance(focusPositions, center);

                if (Backend.IsLoaded(cellId))
                {
                    loaded.Add(cellId);
                }

                if (nearestDistance <= Config.LoadRadius)
                {
                    desiredOrdered.Add((cellId, nearestDistance));
                }

                if (nearestDistance <= Config.UnloadRadius)
                {
                    retain.Add(cellId);
                }
            }

            desiredOrdered.Sort(CompareByDistance);

            IssueRemovesOutsideRetain(loaded, retain);
            IssueAddsForDesired(desiredOrdered, loaded);
        }

        private void IssueRemovesOutsideRetain(HashSet<string> loaded, HashSet<string> retain)
        {
            var toUnload = new HashSet<string>(StringComparer.Ordinal);

            foreach (var cellId in loaded)
            {
                if (!retain.Contains(cellId))
                {
                    toUnload.Add(cellId);
                }
            }

            foreach (var cellId in _inFlightAddCells)
            {
                if (!retain.Contains(cellId))
                {
                    toUnload.Add(cellId);
                }
            }

            foreach (var cellId in toUnload)
            {
                IssueRemove(cellId);
            }
        }

        private void IssueAddsForDesired(
            List<(string cellId, float distance)> desiredOrdered,
            HashSet<string> loaded)
        {
            var issuedThisTick = new HashSet<string>(StringComparer.Ordinal);
            var priority = 0;

            foreach (var (cellId, _) in desiredOrdered)
            {
                // 空き枠は発行ごとに再評価する。即時完了するバックエンドでは
                // 発行直後に in-flight が同期的に空くため、1 Tick で maxInFlight を
                // 超える件数を順次発行できる（未完了の同時数は常に maxInFlight 以下）。
                if (_inFlightAddCells.Count >= Config.MaxInFlight)
                {
                    break;
                }

                if (loaded.Contains(cellId))
                {
                    continue;
                }

                if (_inFlightAddCells.Contains(cellId))
                {
                    continue;
                }

                // Remove 保留中のセルへ再 Add すると Backend 側で Add/Remove が競合する。
                // Remove 完了後の次 Tick の再照合（G-6）に委ねる。
                if (_inFlightRemoveCells.Contains(cellId))
                {
                    continue;
                }

                if (issuedThisTick.Contains(cellId))
                {
                    continue;
                }

                IssueAdd(cellId, priority);
                issuedThisTick.Add(cellId);
                priority++;
            }
        }

        private void IssueAdd(string cellId, int priority)
        {
            _inFlightAddCells.Add(cellId);

            try
            {
                var task = Backend.RequestAdd(cellId, priority);
                ObserveAddCompletionAsync(cellId, task).Forget();
            }
            catch
            {
                _inFlightAddCells.Remove(cellId);
                throw;
            }
        }

        private void IssueRemove(string cellId)
        {
            if (_inFlightRemoveCells.Contains(cellId))
            {
                return;
            }

            // 保留中の Add はここでは解放しない。RequestAdd の完了までは
            // _inFlightAddCells に保持し続けることで、Add 未完了のセルが desired に
            // 戻った際の二重 RequestAdd を防ぐ（完了後は G-6 再照合が回収する）。
            _inFlightRemoveCells.Add(cellId);

            try
            {
                var task = Backend.RequestRemove(cellId);
                ObserveRemoveCompletionAsync(cellId, task).Forget();
            }
            catch
            {
                _inFlightRemoveCells.Remove(cellId);
                throw;
            }
        }

        /// <summary>
        /// RequestAdd の完了を観測して in-flight から除去する。
        /// UniTask は二重 await 不可のため、消費はこの 1 箇所の await のみ（施行表 §5）。
        /// 完了済みタスクなら await は同期的に継続し、同一 Tick 内で in-flight が空く。
        /// 例外・キャンセルもここで観測し、未観測例外を残さない。
        /// </summary>
        private async UniTaskVoid ObserveAddCompletionAsync(string cellId, UniTask task)
        {
            try
            {
                await task;
            }
            catch (Exception)
            {
                // 観測済み。失敗したセルは次 Tick の IsLoaded 再照合（G-6）で再発行される。
            }
            finally
            {
                _inFlightAddCells.Remove(cellId);
            }
        }

        /// <summary>RequestRemove の完了観測。<see cref="ObserveAddCompletionAsync"/> と同じ規約。</summary>
        private async UniTaskVoid ObserveRemoveCompletionAsync(string cellId, UniTask task)
        {
            try
            {
                await task;
            }
            catch (Exception)
            {
                // 観測済み。
            }
            finally
            {
                _inFlightRemoveCells.Remove(cellId);
            }
        }

        private static int CompareByDistance(
            (string cellId, float distance) left,
            (string cellId, float distance) right)
        {
            var distanceCompare = left.distance.CompareTo(right.distance);
            if (distanceCompare != 0)
            {
                return distanceCompare;
            }

            return StringComparer.Ordinal.Compare(left.cellId, right.cellId);
        }

        private static Vector3 GetCellCenter(int x, int y, in CellGridConfig grid)
        {
            return grid.Origin + new Vector3(
                (x + 0.5f) * grid.CellSize,
                0f,
                (y + 0.5f) * grid.CellSize);
        }

        private static float GetXzDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>複数 focus のうちセル中心に最も近い focus への XZ 距離。</summary>
        private static float GetNearestFocusDistance(IReadOnlyList<Vector3> focusPositions, Vector3 cellCenter)
        {
            var nearest = float.MaxValue;

            for (var i = 0; i < focusPositions.Count; i++)
            {
                var distance = GetXzDistance(focusPositions[i], cellCenter);
                if (distance < nearest)
                {
                    nearest = distance;
                }
            }

            return nearest;
        }
    }
}
