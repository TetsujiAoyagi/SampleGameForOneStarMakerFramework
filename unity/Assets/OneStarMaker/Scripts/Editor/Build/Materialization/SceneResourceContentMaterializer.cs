#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OneStarMaker.Build.Selection;
using OneStarMaker.Runtime.SceneSystem;

namespace OneStarMaker.Editor.Build.Materialization
{
    public sealed class SceneResourceContentMaterializer
    {
        private readonly IAssetDatabaseGateway _gateway;
        public SceneResourceContentMaterializer() : this(new UnityAssetDatabaseGateway()) { }
        internal SceneResourceContentMaterializer(IAssetDatabaseGateway gateway) =>
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

        public BuildMaterializationResult Materialize(SceneResourceMap? map)
        {
            var issues = new List<BuildMaterializationIssue>();
            if (map == null)
            {
                issues.Add(new BuildMaterializationIssue(BuildMaterializationIssueCode.NullMap,
                    BuildMaterializationSubject.Map, "map"));
                return new BuildMaterializationResult(null, issues);
            }

            try { return MaterializeCore(map, issues); }
            catch (Exception exception)
            {
                issues.Add(new BuildMaterializationIssue(BuildMaterializationIssueCode.GatewayFailure,
                    BuildMaterializationSubject.Gateway, "AssetDatabase", detailKey: exception.GetType().FullName,
                    message: exception.Message));
                return new BuildMaterializationResult(null, issues);
            }
        }

        private BuildMaterializationResult MaterializeCore(SceneResourceMap map, ICollection<BuildMaterializationIssue> issues)
        {
            var candidates = new List<BuildContentCandidate>();
            var requirements = new List<BuildContentRequirement>();
            var dependencies = new List<BuildDependencySnapshot>();
            var tags = new Dictionary<string, IReadOnlyList<BuildTag>>(StringComparer.Ordinal);
            var logicalKeys = new HashSet<string>(StringComparer.Ordinal);
            var stableKeys = new HashSet<string>(StringComparer.Ordinal);
            var physicalKeys = new HashSet<string>(StringComparer.Ordinal);
            var closureBuilder = new AssetDependencySnapshotBuilder(_gateway);

            foreach (var resource in map.SceneResources.OrderBy(x => x == null ? string.Empty : x.Identity, StringComparer.Ordinal))
            {
                if (resource == null)
                {
                    Add(issues, BuildMaterializationIssueCode.NullResource, BuildMaterializationSubject.Resource, "null");
                    continue;
                }
                var logical = resource.Identity;
                if (!IsStable(logical))
                {
                    Add(issues, BuildMaterializationIssueCode.InvalidResourceIdentity, BuildMaterializationSubject.Resource, logical ?? string.Empty);
                    continue;
                }
                if (!logicalKeys.Add(logical))
                {
                    Add(issues, BuildMaterializationIssueCode.DuplicateLogicalKey, BuildMaterializationSubject.Resource, logical);
                    continue;
                }
                if (resource.SceneAssetDescription == null)
                {
                    Add(issues, BuildMaterializationIssueCode.MissingDescription, BuildMaterializationSubject.Resource, logical);
                    continue;
                }

                requirements.Add(new BuildContentRequirement(logical, BuildContentCardinality.ExactlyOne));
                foreach (var payload in resource.SceneAssetDescription.Payloads)
                {
                    if (payload == null)
                    {
                        Add(issues, BuildMaterializationIssueCode.NullPayload, BuildMaterializationSubject.Payload, logical);
                        continue;
                    }
                    var variant = payload.Variant;
                    if (variant == null || (variant.Length != 0 && !IsStable(variant)))
                    {
                        Add(issues, BuildMaterializationIssueCode.InvalidVariant, BuildMaterializationSubject.Payload, logical, detail: variant);
                        continue;
                    }
                    if (payload.Reference == null || string.IsNullOrEmpty(payload.Reference.AssetGUID))
                    {
                        Add(issues, BuildMaterializationIssueCode.MissingReference, BuildMaterializationSubject.Payload, logical);
                        continue;
                    }
                    var guid = AssetDependencySnapshotBuilder.NormalizeGuid(payload.Reference.AssetGUID);
                    if (guid == null)
                    {
                        Add(issues, BuildMaterializationIssueCode.InvalidRootGuid, BuildMaterializationSubject.Payload, logical,
                            rootGuid: payload.Reference.AssetGUID);
                        continue;
                    }
                    var closure = closureBuilder.Build(guid, logical, issues);
                    if (closure == null) continue;
                    var representation = ScenePayloadMappingPolicy.Representation(variant);
                    var candidate = ScenePayloadMappingPolicy.Candidate(logical, representation, guid, closure.RootPath);
                    if (!stableKeys.Add(candidate.StableKey))
                    {
                        Add(issues, BuildMaterializationIssueCode.DuplicateStableKey, BuildMaterializationSubject.Payload,
                            candidate.StableKey, guid);
                        continue;
                    }
                    if (!physicalKeys.Add(candidate.PhysicalKey))
                    {
                        Add(issues, BuildMaterializationIssueCode.PhysicalKeyCollision, BuildMaterializationSubject.Payload,
                            candidate.StableKey, guid);
                        continue;
                    }
                    candidates.Add(candidate);
                    dependencies.Add(closure);
                    tags.Add(candidate.StableKey, new[] { ScenePayloadMappingPolicy.Tag(representation) });
                }
            }

            var snapshot = issues.Count == 0
                ? new BuildMaterializationSnapshot(candidates, tags, requirements, dependencies)
                : null;
            return new BuildMaterializationResult(snapshot, issues);
        }

        private static bool IsStable(string? value) => !string.IsNullOrEmpty(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);
        private static void Add(ICollection<BuildMaterializationIssue> issues, BuildMaterializationIssueCode code,
            BuildMaterializationSubject subject, string subjectKey, string? rootGuid = null, string? path = null, string? detail = null) =>
            issues.Add(new BuildMaterializationIssue(code, subject, subjectKey, rootGuid, path, detail));
    }
}
