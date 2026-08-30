#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Editor.Streaming;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Cells
{
    /// <summary>既存 Cell 1 件分の状態（AssetDatabase に触れない純データ）。</summary>
    public readonly struct CellExistingState
    {
        public CellExistingState(
            string identity,
            bool hasCellAuthoredRoot,
            bool hasEnvironmentScene,
            bool hasEnvironmentAuthoredRoot)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            HasCellAuthoredRoot = hasCellAuthoredRoot;
            HasEnvironmentScene = hasEnvironmentScene;
            HasEnvironmentAuthoredRoot = hasEnvironmentAuthoredRoot;
        }

        public string Identity { get; }
        public bool HasCellAuthoredRoot { get; }
        /// <summary>Environment resource の存在記録。Populate / Skip の直接入力にはしない。</summary>
        public bool HasEnvironmentScene { get; }
        public bool HasEnvironmentAuthoredRoot { get; }
    }

    /// <summary>Populate（書き込み可） / Skip（触らない）。</summary>
    public enum CellPopulationAction
    {
        Populate = 0,
        Skip = 1,
    }

    /// <summary>生成対象 1 件分の Populate / Skip 計画。</summary>
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

    /// <summary>target 集合外にある既存 Cell identity の削除計画。</summary>
    public sealed class CellDeletionEntry
    {
        public CellDeletionEntry(string identity)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        }

        public string Identity { get; }
    }

    /// <summary>既存状態 + policy から Populate / Skip / 削除可否を決める純関数の出力。</summary>
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

        public IReadOnlyList<CellPopulationEntry> PopulationEntries { get; }
        public IReadOnlyList<CellDeletionEntry> DeletionEntries { get; }

        public bool ShouldPopulateEnvironment(string identity)
        {
            for (var i = 0; i < PopulationEntries.Count; i++)
            {
                var entry = PopulationEntries[i];
                if (string.Equals(entry.Identity, identity, StringComparison.Ordinal))
                {
                    return entry.EnvironmentAction == CellPopulationAction.Populate;
                }
            }

            return false;
        }

        public bool IsDeletable(string identity)
        {
            for (var i = 0; i < DeletionEntries.Count; i++)
            {
                if (string.Equals(DeletionEntries[i].Identity, identity, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static CellPopulationPlan Compute(
            IReadOnlyList<WorldCellGenerationTarget> targets,
            IReadOnlyList<CellExistingState> existingStates)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (existingStates == null)
            {
                throw new ArgumentNullException(nameof(existingStates));
            }

            WorldCellGenerationTarget.Validate(targets);

            var targetIdentities = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < targets.Count; i++)
            {
                targetIdentities.Add(targets[i].Identity);
            }

            var existingByIdentity = new Dictionary<string, CellExistingState>(StringComparer.Ordinal);
            for (var i = 0; i < existingStates.Count; i++)
            {
                var state = existingStates[i];
                if (!existingByIdentity.TryAdd(state.Identity, state))
                {
                    throw new ArgumentException($"既存 identity が重複しています: {state.Identity}", nameof(existingStates));
                }
            }

            var populationEntries = new List<CellPopulationEntry>(targets.Count);
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var hasCellAuthoredRoot = false;
                var hasEnvironmentAuthoredRoot = false;
                if (existingByIdentity.TryGetValue(target.Identity, out var existing))
                {
                    hasCellAuthoredRoot = existing.HasCellAuthoredRoot;
                    hasEnvironmentAuthoredRoot = existing.HasEnvironmentAuthoredRoot;
                }

                ResolveActions(
                    target.Identity,
                    hasCellAuthoredRoot,
                    hasEnvironmentAuthoredRoot,
                    out var cellAction,
                    out var environmentAction);
                populationEntries.Add(new CellPopulationEntry(
                    target.Identity,
                    target.Coordinate,
                    cellAction,
                    environmentAction));
            }

            var deletionEntries = new List<CellDeletionEntry>();
            for (var i = 0; i < existingStates.Count; i++)
            {
                var identity = existingStates[i].Identity;
                if (targetIdentities.Contains(identity)
                    || CellAuthoringPolicy.Resolve(identity) == CellAuthoringPolicyKind.HandAuthored)
                {
                    continue;
                }

                deletionEntries.Add(new CellDeletionEntry(identity));
            }

            return new CellPopulationPlan(populationEntries, deletionEntries);
        }

        private static void ResolveActions(
            string identity,
            bool hasCellAuthoredRoot,
            bool hasEnvironmentAuthoredRoot,
            out CellPopulationAction cellAction,
            out CellPopulationAction environmentAction)
        {
            if (CellAuthoringPolicy.Resolve(identity) == CellAuthoringPolicyKind.Generated)
            {
                cellAction = CellPopulationAction.Populate;
                environmentAction = CellPopulationAction.Populate;
                return;
            }

            cellAction = hasCellAuthoredRoot
                ? CellPopulationAction.Skip
                : CellPopulationAction.Populate;
            environmentAction = hasEnvironmentAuthoredRoot
                ? CellPopulationAction.Skip
                : CellPopulationAction.Populate;
        }
    }
}
