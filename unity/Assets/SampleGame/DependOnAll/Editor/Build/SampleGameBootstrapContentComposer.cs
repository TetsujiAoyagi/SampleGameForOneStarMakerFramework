#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Materialization;
using SampleGame.DependOnAll;

namespace SampleGame.DependOnAll.Editor.Build
{
    internal sealed class SampleGameBootstrapComposition
    {
        internal SampleGameBootstrapComposition(BuildPlan plan, BuildMaterializationSnapshot snapshot)
        { Plan = plan; Snapshot = snapshot; }
        internal BuildPlan Plan { get; }
        internal BuildMaterializationSnapshot Snapshot { get; }
    }

    /// <summary>
    /// 季節選択の後に、Editor directory Play 用の UICommon Scene と SceneResourceMap を
    /// 同じ plan/snapshot へ合成する。季節 policy は知らない。
    /// </summary>
    internal static class SampleGameBootstrapContentComposer
    {
        internal const string UiCommonLogicalKey = DirectoryBootstrapKeys.UiCommonScene;
        internal const string SceneResourceMapLogicalKey = DirectoryBootstrapKeys.SceneResourceMap;
        internal const string UiCommonPath = "Assets/OneStarMaker/Scenes/UISystem/UIScene.unity";
        internal const string SceneResourceMapPath = "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";
        internal const string Representation = DirectoryBootstrapKeys.Representation;

        internal static SampleGameBootstrapComposition Compose(
            BuildPlan plan,
            BuildMaterializationSnapshot snapshot,
            string uiGuid,
            BuildDependencySnapshot uiClosure,
            string mapGuid,
            BuildDependencySnapshot mapClosure)
        {
            if (plan == null || snapshot == null)
                throw new ArgumentNullException(plan == null ? nameof(plan) : nameof(snapshot));
            if (uiClosure == null || mapClosure == null)
                throw new ArgumentNullException(uiClosure == null ? nameof(uiClosure) : nameof(mapClosure));
            Require(uiGuid, nameof(uiGuid));
            Require(mapGuid, nameof(mapGuid));
            if (!string.Equals(uiClosure.RootGuid, uiGuid, StringComparison.Ordinal)
                || !string.Equals(mapClosure.RootGuid, mapGuid, StringComparison.Ordinal))
                throw new InvalidOperationException("Bootstrap closure root does not match the candidate physical key.");

            var ui = Candidate(UiCommonLogicalKey, uiGuid);
            var map = Candidate(SceneResourceMapLogicalKey, mapGuid);
            RejectCollision(snapshot, ui);
            RejectCollision(snapshot, map);

            var tags = new Dictionary<string, IReadOnlyList<BuildTag>>(StringComparer.Ordinal);
            foreach (var existing in snapshot.Candidates)
            {
                var values = snapshot.TagProviders.SelectMany(x => x.GetTags(existing)).ToArray();
                if (values.GroupBy(x => x.Dimension, StringComparer.Ordinal)
                    .Any(x => x.Select(y => y.Value).Distinct(StringComparer.Ordinal).Count() > 1))
                    throw new InvalidOperationException("Production snapshot contains conflicting tags.");
                tags.Add(existing.StableKey, values);
            }
            tags.Add(ui.StableKey, new[] { new BuildTag("Representation", Representation) });
            tags.Add(map.StableKey, new[] { new BuildTag("Representation", Representation) });

            var composedSnapshot = new BuildMaterializationSnapshot(
                snapshot.Candidates.Concat(new[] { ui, map }),
                tags,
                snapshot.Requirements.Concat(new[]
                {
                    new BuildContentRequirement(UiCommonLogicalKey, BuildContentCardinality.ExactlyOne),
                    new BuildContentRequirement(SceneResourceMapLogicalKey, BuildContentCardinality.ExactlyOne),
                }),
                snapshot.Dependencies.Concat(new[] { uiClosure, mapClosure }));
            var composedPlan = new BuildPlan(
                plan.Request,
                plan.SelectedContent.Concat(new[] { ui, map }),
                plan.ExcludedContent);
            return new SampleGameBootstrapComposition(composedPlan, composedSnapshot);
        }

        private static BuildContentCandidate Candidate(string logicalKey, string physicalKey) =>
            new(logicalKey + "/" + Representation, logicalKey, physicalKey,
                new BuildProvenance("samplegame-bootstrap", logicalKey));

        private static void RejectCollision(BuildMaterializationSnapshot snapshot, BuildContentCandidate candidate)
        {
            if (snapshot.Candidates.Any(x => x.StableKey == candidate.StableKey
                || x.LogicalKey == candidate.LogicalKey
                || x.PhysicalKey == candidate.PhysicalKey))
                throw new InvalidOperationException("Bootstrap content collides with a production candidate.");
            if (snapshot.Requirements.Any(x => x.LogicalKey == candidate.LogicalKey)
                || snapshot.Dependencies.Any(x => x.RootGuid == candidate.PhysicalKey))
                throw new InvalidOperationException("Bootstrap content collides with production metadata.");
        }

        private static void Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
                throw new ArgumentException("Value must be stable.", name);
        }
    }
}
