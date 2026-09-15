#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace OneStarMaker.Build.Selection
{
    public sealed class BuildTagSelector
    {
        public BuildPlanResult Select(
            BuildRequest request,
            IEnumerable<BuildContentCandidate> candidates,
            IEnumerable<IBuildTagProvider> providers,
            BuildSelectionPolicy policy)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (providers == null) throw new ArgumentNullException(nameof(providers));
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            var issues = new List<BuildValidationIssue>();
            var candidateSnapshot = candidates.Select(candidate => candidate == null
                    ? throw new ArgumentException("Candidates cannot contain null.", nameof(candidates))
                    : candidate.Snapshot())
                .OrderBy(candidate => candidate.StableKey, StringComparer.Ordinal)
                .ToArray();
            var providerSnapshot = providers.Select(provider => provider
                    ?? throw new ArgumentException("Providers cannot contain null.", nameof(providers)))
                .OrderBy(provider => provider.StableProviderKey, StringComparer.Ordinal)
                .ToArray();

            ValidateProviderKeys(providerSnapshot);
            ValidateCandidateIdentity(candidateSnapshot, issues);
            ValidateRequirements(policy.Requirements, issues);
            var requested = ValidateRequest(request, policy.Schema, issues);
            var attributed = AttributeAndValidate(candidateSnapshot, providerSnapshot, policy.Schema, issues);

            if (issues.Any(issue => issue.Severity == BuildValidationSeverity.Error))
                return new BuildPlanResult(null, issues);

            var selected = new List<BuildContentCandidate>();
            var excluded = new List<BuildContentExclusion>();
            foreach (var candidate in candidateSnapshot)
            {
                var rejection = FindRejection(attributed[candidate], requested);
                if (rejection == null)
                    selected.Add(candidate);
                else
                    excluded.Add(new BuildContentExclusion(candidate, rejection.Item1, rejection.Item2.Dimension!, rejection.Item2.Value!));
            }

            ValidateCardinality(candidateSnapshot, selected, policy.Requirements, issues);
            var plan = new BuildPlan(request, selected, excluded);
            return new BuildPlanResult(plan, issues);
        }

        private static void ValidateProviderKeys(IReadOnlyList<IBuildTagProvider> providers)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var provider in providers)
            {
                var key = provider.StableProviderKey;
                if (string.IsNullOrEmpty(key) || !string.Equals(key, key.Trim(), StringComparison.Ordinal))
                    throw new ArgumentException("Provider keys must be non-empty and have no surrounding whitespace.", nameof(providers));
                if (!keys.Add(key))
                    throw new ArgumentException("Provider keys must be unique.", nameof(providers));
            }
        }

        private static void ValidateCandidateIdentity(
            IReadOnlyList<BuildContentCandidate> candidates,
            ICollection<BuildValidationIssue> issues)
        {
            foreach (var group in candidates.GroupBy(candidate => candidate.StableKey, StringComparer.Ordinal))
            {
                if (group.Count() > 1)
                    AddError(issues, BuildValidationCode.DuplicateCandidateKey, BuildValidationSubject.Candidate,
                        group.Key, null, null, null, "Stable candidate key is duplicated.");
            }

            foreach (var group in candidates.GroupBy(candidate => candidate.PhysicalKey, StringComparer.Ordinal))
            {
                if (group.Select(candidate => candidate.StableKey).Distinct(StringComparer.Ordinal).Count() > 1)
                    AddError(issues, BuildValidationCode.PhysicalKeyCollision, BuildValidationSubject.Candidate,
                        group.Key, null, null, null, "Physical key is shared by multiple candidates.");
            }
        }

        private static void ValidateRequirements(
            IReadOnlyList<BuildContentRequirement> requirements,
            ICollection<BuildValidationIssue> issues)
        {
            foreach (var group in requirements.GroupBy(requirement => requirement.LogicalKey, StringComparer.Ordinal))
            {
                if (group.Count() <= 1) continue;
                var code = group.Select(requirement => requirement.Cardinality).Distinct().Count() == 1
                    ? BuildValidationCode.DuplicateRequirement
                    : BuildValidationCode.ConflictingRequirement;
                AddError(issues, code, BuildValidationSubject.Requirement, group.Key, null, null, null,
                    "Logical requirement is declared more than once.");
            }
        }

        private static Dictionary<string, HashSet<string>> ValidateRequest(
            BuildRequest request,
            BuildTagSchema schema,
            ICollection<BuildValidationIssue> issues)
        {
            var requested = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            if (request.Selections.Count == 0)
            {
                AddError(issues, BuildValidationCode.InvalidRequestSelection, BuildValidationSubject.Request,
                    "request", null, null, null, "Request must contain at least one selection.");
                return requested;
            }

            foreach (var tag in request.Selections)
            {
                if (!IsStable(tag.Dimension) || !IsStable(tag.Value))
                {
                    AddError(issues, BuildValidationCode.InvalidRequestSelection, BuildValidationSubject.Request,
                        "request", tag.Dimension, tag.Value, null, "Request selection is null, empty, or has surrounding whitespace.");
                    continue;
                }
                if (!schema.ContainsDimension(tag.Dimension!))
                {
                    AddError(issues, BuildValidationCode.UnknownDimension, BuildValidationSubject.Request,
                        "request", tag.Dimension, tag.Value, null, "Request dimension is not in the schema.");
                    continue;
                }
                if (!schema.ContainsValue(tag.Dimension!, tag.Value!))
                {
                    AddError(issues, BuildValidationCode.UnknownValue, BuildValidationSubject.Request,
                        "request", tag.Dimension, tag.Value, null, "Request value is not in the schema.");
                    continue;
                }
                if (!requested.TryGetValue(tag.Dimension!, out var values))
                {
                    values = new HashSet<string>(StringComparer.Ordinal);
                    requested.Add(tag.Dimension!, values);
                }
                values.Add(tag.Value!);
            }
            return requested;
        }

        private static Dictionary<BuildContentCandidate, IReadOnlyList<BuildTag>> AttributeAndValidate(
            IReadOnlyList<BuildContentCandidate> candidates,
            IReadOnlyList<IBuildTagProvider> providers,
            BuildTagSchema schema,
            ICollection<BuildValidationIssue> issues)
        {
            var result = new Dictionary<BuildContentCandidate, IReadOnlyList<BuildTag>>();
            foreach (var candidate in candidates)
            {
                var all = new List<Tuple<BuildTag, string>>();
                foreach (var provider in providers)
                {
                    var returned = provider.GetTags(candidate);
                    if (returned == null) throw new InvalidOperationException("A tag provider returned null.");
                    foreach (var tag in returned)
                    {
                        if (tag == null) throw new InvalidOperationException("A tag provider returned a null tag.");
                        all.Add(Tuple.Create(new BuildTag(tag.Dimension, tag.Value), provider.StableProviderKey));
                    }
                }

                foreach (var item in all)
                {
                    var tag = item.Item1;
                    if (!IsStable(tag.Dimension))
                    {
                        AddError(issues, BuildValidationCode.UnknownDimension, BuildValidationSubject.Candidate,
                            candidate.StableKey, tag.Dimension, tag.Value, item.Item2, "Candidate dimension is null, empty, or has surrounding whitespace.");
                    }
                    else if (!schema.ContainsDimension(tag.Dimension!))
                    {
                        AddError(issues, BuildValidationCode.UnknownDimension, BuildValidationSubject.Candidate,
                            candidate.StableKey, tag.Dimension, tag.Value, item.Item2, "Candidate dimension is not in the schema.");
                    }
                    else if (!IsStable(tag.Value) || !schema.ContainsValue(tag.Dimension!, tag.Value!))
                    {
                        AddError(issues, BuildValidationCode.UnknownValue, BuildValidationSubject.Candidate,
                            candidate.StableKey, tag.Dimension, tag.Value, item.Item2, "Candidate value is invalid or not in the schema.");
                    }
                }

                foreach (var duplicate in all.GroupBy(item => new TagKey(item.Item1.Dimension, item.Item1.Value)))
                {
                    if (duplicate.Count() > 1)
                    {
                        AddWarning(issues, BuildValidationCode.DuplicateTag, BuildValidationSubject.Candidate,
                            candidate.StableKey, duplicate.Key.Dimension, duplicate.Key.Value,
                            duplicate.Select(item => item.Item2).OrderBy(key => key, StringComparer.Ordinal).First(),
                            "Candidate tag is duplicated and was deduplicated.");
                    }
                }

                foreach (var dimension in all.Where(item => IsStable(item.Item1.Dimension) && IsStable(item.Item1.Value))
                    .GroupBy(item => item.Item1.Dimension!, StringComparer.Ordinal))
                {
                    if (dimension.Select(item => item.Item1.Value).Distinct(StringComparer.Ordinal).Count() > 1)
                    {
                        AddError(issues, BuildValidationCode.ConflictingCandidateValues, BuildValidationSubject.Candidate,
                            candidate.StableKey, dimension.Key, null,
                            dimension.Select(item => item.Item2).OrderBy(key => key, StringComparer.Ordinal).First(),
                            "Candidate has different values for the same dimension.");
                    }
                }

                result[candidate] = all.Select(item => item.Item1)
                    .Where(tag => IsStable(tag.Dimension) && IsStable(tag.Value))
                    .Distinct()
                    .OrderBy(tag => tag.Dimension, StringComparer.Ordinal)
                    .ThenBy(tag => tag.Value, StringComparer.Ordinal)
                    .ToArray();
            }
            return result;
        }

        private static Tuple<BuildExclusionReasonCode, BuildTag>? FindRejection(
            IReadOnlyList<BuildTag> tags,
            IReadOnlyDictionary<string, HashSet<string>> requested)
        {
            foreach (var tag in tags)
            {
                if (!requested.TryGetValue(tag.Dimension!, out var values))
                    return Tuple.Create(BuildExclusionReasonCode.UnrequestedDimension, tag);
                if (!values.Contains(tag.Value!))
                    return Tuple.Create(BuildExclusionReasonCode.ValueNotSelected, tag);
            }
            return null;
        }

        private static void ValidateCardinality(
            IReadOnlyList<BuildContentCandidate> candidates,
            IReadOnlyList<BuildContentCandidate> selected,
            IReadOnlyList<BuildContentRequirement> requirements,
            ICollection<BuildValidationIssue> issues)
        {
            foreach (var requirement in requirements.OrderBy(item => item.LogicalKey, StringComparer.Ordinal))
            {
                var total = candidates.Count(candidate => string.Equals(candidate.LogicalKey, requirement.LogicalKey, StringComparison.Ordinal));
                if (total == 0)
                {
                    AddError(issues, BuildValidationCode.RequiredGroupMissing, BuildValidationSubject.Requirement,
                        requirement.LogicalKey, null, null, null, "Required logical group has no candidates.");
                    continue;
                }
                var count = selected.Count(candidate => string.Equals(candidate.LogicalKey, requirement.LogicalKey, StringComparison.Ordinal));
                var valid = requirement.Cardinality == BuildContentCardinality.OneOrMore ? count >= 1 : count == 1;
                if (!valid)
                    AddError(issues, BuildValidationCode.CardinalityViolation, BuildValidationSubject.Requirement,
                        requirement.LogicalKey, null, null, null, "Selected candidate count violates the requirement.");
            }
        }

        private static bool IsStable(string? value) =>
            !string.IsNullOrEmpty(value) && string.Equals(value, value.Trim(), StringComparison.Ordinal);

        private static void AddError(ICollection<BuildValidationIssue> issues, BuildValidationCode code,
            BuildValidationSubject subject, string subjectKey, string? dimension, string? value,
            string? providerKey, string message) =>
            issues.Add(new BuildValidationIssue(BuildValidationSeverity.Error, code, subject, subjectKey,
                dimension, value, providerKey, message));

        private static void AddWarning(ICollection<BuildValidationIssue> issues, BuildValidationCode code,
            BuildValidationSubject subject, string subjectKey, string? dimension, string? value,
            string? providerKey, string message) =>
            issues.Add(new BuildValidationIssue(BuildValidationSeverity.Warning, code, subject, subjectKey,
                dimension, value, providerKey, message));

        private sealed class TagKey : IEquatable<TagKey>
        {
            public TagKey(string? dimension, string? value) { Dimension = dimension; Value = value; }
            public string? Dimension { get; }
            public string? Value { get; }
            public bool Equals(TagKey? other) => other != null
                && string.Equals(Dimension, other.Dimension, StringComparison.Ordinal)
                && string.Equals(Value, other.Value, StringComparison.Ordinal);
            public override bool Equals(object? obj) => Equals(obj as TagKey);
            public override int GetHashCode() => new BuildTag(Dimension, Value).GetHashCode();
        }
    }
}
