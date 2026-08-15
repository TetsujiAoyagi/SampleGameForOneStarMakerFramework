#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Cells
{
    /// <summary>
    /// グリッド定義の値（WorldGridDefinition 資産そのものではなく純データ）。
    /// </summary>
    public readonly struct CellGridSpec
    {
        public CellGridSpec(int gridWidth, int gridHeight, Vector3 origin, float cellSize)
        {
            GridWidth = gridWidth;
            GridHeight = gridHeight;
            Origin = origin;
            CellSize = cellSize;
        }

        public int GridWidth { get; }
        public int GridHeight { get; }
        public Vector3 Origin { get; }
        public float CellSize { get; }
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

        /// <summary>Environment の <c>.unity</c> が存在するか。</summary>
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
        /// グリッド定義の値・既存状態・policy から計画を返す。
        /// policy は <see cref="CellAuthoringPolicy.Resolve(UnityEngine.Vector2Int)"/> で解決する。
        /// </summary>
        public static CellPopulationPlan Compute(
            CellGridSpec grid,
            IReadOnlyList<CellExistingState> existingStates)
        {
            _ = grid;
            _ = existingStates;
            throw new NotImplementedException();
        }
    }
}
