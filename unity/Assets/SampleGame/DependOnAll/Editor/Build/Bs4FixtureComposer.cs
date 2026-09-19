#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Materialization;

namespace SampleGame.DependOnAll.Editor.Build
{
    internal sealed class Bs4FixtureComposition
    {
        internal Bs4FixtureComposition(BuildPlan plan, BuildMaterializationSnapshot snapshot)
        { Plan = plan; Snapshot = snapshot; }
        internal BuildPlan Plan { get; }
        internal BuildMaterializationSnapshot Snapshot { get; }
    }

    /// <summary>BS4 probe 一件を immutable な production plan/snapshot へ正規合成する唯一の friend 利用箇所。</summary>
    internal static class Bs4FixtureComposer
    {
        internal const string LogicalKey = "bs4:fixture:prefab";

        internal static Bs4FixtureComposition Compose(BuildPlan plan, BuildMaterializationSnapshot snapshot,
            string prefabGuid, string prefabPath, string representation, string token)
        {
            if (plan == null || snapshot == null) throw new ArgumentNullException(plan == null ? nameof(plan) : nameof(snapshot));
            Require(prefabGuid, nameof(prefabGuid)); Require(prefabPath, nameof(prefabPath)); Require(representation, nameof(representation)); Require(token, nameof(token));
            var candidate = new BuildContentCandidate(LogicalKey + "/" + representation, LogicalKey, prefabGuid,
                new BuildProvenance("bs4-fixture", token));
            if (snapshot.Candidates.Any(x => x.StableKey == candidate.StableKey || x.LogicalKey == LogicalKey || x.PhysicalKey == prefabGuid))
                throw new InvalidOperationException("BS4 fixture collides with a production candidate.");
            if (snapshot.Requirements.Any(x => x.LogicalKey == LogicalKey) || snapshot.Dependencies.Any(x => x.RootGuid == prefabGuid))
                throw new InvalidOperationException("BS4 fixture collides with production metadata.");

            var tags = new Dictionary<string, IReadOnlyList<BuildTag>>(StringComparer.Ordinal);
            foreach (var existing in snapshot.Candidates)
            {
                var values = snapshot.TagProviders.SelectMany(x => x.GetTags(existing)).ToArray();
                if (values.GroupBy(x => x.Dimension, StringComparer.Ordinal).Any(x => x.Select(y => y.Value).Distinct(StringComparer.Ordinal).Count() > 1))
                    throw new InvalidOperationException("Production snapshot contains conflicting tags.");
                tags.Add(existing.StableKey, values);
            }
            tags.Add(candidate.StableKey, new[] { new BuildTag("Representation", representation) });
            var dependency = new BuildDependencySnapshot(prefabGuid, prefabPath,
                new[] { new BuildDependencyEntry(prefabGuid, prefabPath) });
            var composedSnapshot = new BuildMaterializationSnapshot(snapshot.Candidates.Concat(new[] { candidate }), tags,
                snapshot.Requirements.Concat(new[] { new BuildContentRequirement(LogicalKey, BuildContentCardinality.ExactlyOne) }),
                snapshot.Dependencies.Concat(new[] { dependency }));
            var composedPlan = new BuildPlan(plan.Request, plan.SelectedContent.Concat(new[] { candidate }), plan.ExcludedContent);
            return new Bs4FixtureComposition(composedPlan, composedSnapshot);
        }

        private static void Require(string value, string name)
        { if (string.IsNullOrWhiteSpace(value) || value != value.Trim()) throw new ArgumentException("Value must be stable.", name); }
    }
}
