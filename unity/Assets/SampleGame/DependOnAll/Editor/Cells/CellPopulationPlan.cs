#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Cells
{
    /// <summary>
    /// グリッド定義の値（WorldGridDefinition 資産そのものではなく純データ）。
    /// 矩形集合を展開したセル座標を持つ。
    /// </summary>
    public readonly struct CellGridSpec
    {
        public CellGridSpec(IReadOnlyList<Vector2Int> cells, Vector3 origin, float cellSize)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (cells.Count == 0)
            {
                throw new ArgumentException("セル座標集合は 1 件以上である必要があります。", nameof(cells));
            }

            var copy = new Vector2Int[cells.Count];
            var membership = new HashSet<Vector2Int>();
            for (var i = 0; i < cells.Count; i++)
            {
                copy[i] = cells[i];
                membership.Add(cells[i]);
            }

            Cells = copy;
            Origin = origin;
            CellSize = cellSize;
            _membership = membership;
        }

        public IReadOnlyList<Vector2Int> Cells { get; }

        public Vector3 Origin { get; }

        public float CellSize { get; }

        private readonly HashSet<Vector2Int> _membership;

        public bool Contains(Vector2Int coordinate) => _membership.Contains(coordinate);
    }

    /// <summary>
    /// 既存 Cell 1 件分の状態（AssetDatabase に触れない純データ）。
    /// </summary>
    public readonly struct CellExistingState
    {
        public CellExistingState(
            string identity,
            Vector2Int coordinate,
            bool hasCellAuthoredRoot,
            bool hasEnvironmentScene,
            bool hasEnvironmentAuthoredRoot)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Coordinate = coordinate;
            HasCellAuthoredRoot = hasCellAuthoredRoot;
            HasEnvironmentScene = hasEnvironmentScene;
            HasEnvironmentAuthoredRoot = hasEnvironmentAuthoredRoot;
        }

        /// <summary>Cell identity（例: <c>Cell_0_0</c>）。</summary>
        public string Identity { get; }

        /// <summary>グリッド座標。</summary>
        public Vector2Int Coordinate { get; }

        /// <summary>Cell シーンに AuthoredRoot があるか。</summary>
        public bool HasCellAuthoredRoot { get; }

        /// <summary>
        /// Environment の <c>.unity</c> が存在するか。
        /// Populate / Skip 判定には使わない。<c>.unity</c> の存在は
        /// <c>EnsureEnvironmentSceneFile</c> が扱う呼び出し側の関心事であり、
        /// 判定は <see cref="HasEnvironmentAuthoredRoot"/> の有無だけで行う
        /// （空の <c>.unity</c> が残る半端状態から自己回復させるため）。
        /// </summary>
        public bool HasEnvironmentScene { get; }

        /// <summary>Environment シーンに AuthoredRoot があるか。</summary>
        public bool HasEnvironmentAuthoredRoot { get; }
    }

    /// <summary>Populate（書き込み可） / Skip（触らない）。</summary>
    public enum CellPopulationAction
    {
        Populate = 0,
        Skip = 1,
    }

    /// <summary>
    /// グリッド内 Cell 1 件分の Populate / Skip 計画。
    /// </summary>
    public sealed class CellPopulationEntry
    {
        public CellPopulationEntry(
            string identity,
            Vector2Int coordinate,
            CellPopulationAction cellAction,
            CellPopulationAction environmentAction)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Coordinate = coordinate;
            CellAction = cellAction;
            EnvironmentAction = environmentAction;
        }

        public string Identity { get; }
        public Vector2Int Coordinate { get; }
        public CellPopulationAction CellAction { get; }
        public CellPopulationAction EnvironmentAction { get; }
    }

    /// <summary>
    /// 範囲外 Cell フォルダの削除計画 1 件。
    /// </summary>
    public sealed class CellDeletionEntry
    {
        public CellDeletionEntry(string identity, Vector2Int coordinate)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Coordinate = coordinate;
        }

        public string Identity { get; }
        public Vector2Int Coordinate { get; }
    }

    /// <summary>
    /// 既存状態 + policy から Populate / Skip / 削除可否を決める純関数の出力。
    /// AssetDatabase / EditorSceneManager には依存しない。
    /// </summary>
    public sealed class CellPopulationPlan
    {
        public CellPopulationPlan(
            IReadOnlyList<CellPopulationEntry> populationEntries,
            IReadOnlyList<CellDeletionEntry> deletionEntries)
        {
            PopulationEntries = populationEntries
                ?? throw new ArgumentNullException(nameof(populationEntries));
            DeletionEntries = deletionEntries
                ?? throw new ArgumentNullException(nameof(deletionEntries));
        }

        /// <summary>グリッド内 Cell の Populate / Skip 計画。</summary>
        public IReadOnlyList<CellPopulationEntry> PopulationEntries { get; }

        /// <summary>
        /// 範囲外かつ削除してよい Cell フォルダの計画。
        /// HandAuthored は範囲外でもここに現れない。
        /// </summary>
        public IReadOnlyList<CellDeletionEntry> DeletionEntries { get; }

        /// <summary>
        /// Environment を Populate してよいか。
        /// 計画にエントリが無い座標（グリッド範囲外）は false。
        /// </summary>
        public bool ShouldPopulateEnvironment(Vector2Int coordinate)
        {
            for (var i = 0; i < PopulationEntries.Count; i++)
            {
                var entry = PopulationEntries[i];
                if (entry.Coordinate != coordinate)
                {
                    continue;
                }

                return entry.EnvironmentAction == CellPopulationAction.Populate;
            }

            return false;
        }

        /// <summary>
        /// 範囲外 Cell フォルダを削除してよいか。
        /// <see cref="DeletionEntries"/> に含まれる座標のみ true（HandAuthored は範囲外でも false）。
        /// </summary>
        public bool IsDeletable(Vector2Int coordinate)
        {
            for (var i = 0; i < DeletionEntries.Count; i++)
            {
                if (DeletionEntries[i].Coordinate == coordinate)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// グリッド定義の値・既存状態・policy から計画を返す。
        /// policy は <see cref="CellAuthoringPolicy.Resolve(UnityEngine.Vector2Int)"/> で解決する。
        /// </summary>
        public static CellPopulationPlan Compute(
            CellGridSpec grid,
            IReadOnlyList<CellExistingState> existingStates)
        {
            _ = existingStates ?? throw new ArgumentNullException(nameof(existingStates));

            var existingByCoordinate = new Dictionary<Vector2Int, CellExistingState>(existingStates.Count);
            for (var i = 0; i < existingStates.Count; i++)
            {
                var state = existingStates[i];
                existingByCoordinate[state.Coordinate] = state;
            }

            var populationEntries = new List<CellPopulationEntry>(grid.Cells.Count);
            for (var i = 0; i < grid.Cells.Count; i++)
            {
                var coordinate = grid.Cells[i];
                var hasCellAuthoredRoot = false;
                var hasEnvironmentAuthoredRoot = false;
                if (existingByCoordinate.TryGetValue(coordinate, out var existing))
                {
                    hasCellAuthoredRoot = existing.HasCellAuthoredRoot;
                    hasEnvironmentAuthoredRoot = existing.HasEnvironmentAuthoredRoot;
                }

                ResolveActions(
                    coordinate,
                    hasCellAuthoredRoot,
                    hasEnvironmentAuthoredRoot,
                    out var cellAction,
                    out var environmentAction);

                populationEntries.Add(new CellPopulationEntry(
                    identity: CellIdentity.Format(coordinate.x, coordinate.y),
                    coordinate: coordinate,
                    cellAction: cellAction,
                    environmentAction: environmentAction));
            }

            var deletionEntries = new List<CellDeletionEntry>();
            for (var i = 0; i < existingStates.Count; i++)
            {
                var state = existingStates[i];
                var c = state.Coordinate;
                if (grid.Contains(c))
                {
                    continue;
                }

                // HandAuthored は範囲外でも削除しない。負座標では Format 不可のため Identity を流用する。
                if (CellAuthoringPolicy.Resolve(c) == CellAuthoringPolicyKind.HandAuthored)
                {
                    continue;
                }

                deletionEntries.Add(new CellDeletionEntry(state.Identity, c));
            }

            return new CellPopulationPlan(populationEntries, deletionEntries);
        }

        private static void ResolveActions(
            Vector2Int coordinate,
            bool hasCellAuthoredRoot,
            bool hasEnvironmentAuthoredRoot,
            out CellPopulationAction cellAction,
            out CellPopulationAction environmentAction)
        {
            var policy = CellAuthoringPolicy.Resolve(coordinate);
            if (policy == CellAuthoringPolicyKind.Generated)
            {
                cellAction = CellPopulationAction.Populate;
                environmentAction = CellPopulationAction.Populate;
                return;
            }

            // HandAuthored: Cell / Environment は独立判定（AuthoredRoot の有無のみ）
            cellAction = hasCellAuthoredRoot
                ? CellPopulationAction.Skip
                : CellPopulationAction.Populate;
            environmentAction = hasEnvironmentAuthoredRoot
                ? CellPopulationAction.Skip
                : CellPopulationAction.Populate;
        }
    }
}
