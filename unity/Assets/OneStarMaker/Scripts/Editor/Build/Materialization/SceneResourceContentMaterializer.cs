#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OneStarMaker.Build.Selection;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;

namespace OneStarMaker.Editor.Build.Materialization
{
    // SceneResourceMap を検証し、Selection に渡せる候補一式（Scene payload と付随 asset）と到達規則の graph へ変換する公開入口。
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

        // 1 node 分の graph 入力。全 node の検査が終わるまで BuildGraphNode は作らない（不正入力で部分的な graph を公開しない）。
        private sealed class NodeDraft
        {
            public NodeDraft(string identity, string? parent, IReadOnlyList<string> children, BuildGraphLoad load)
            { Identity = identity; Parent = parent; Children = children; Load = load; }
            public string Identity { get; }
            public string? Parent { get; }
            public IReadOnlyList<string> Children { get; }
            public BuildGraphLoad Load { get; }
            public List<string> SceneRepresentations { get; } = new();
            public List<BuildGraphAttachedAsset> AttachedAssets { get; } = new();
        }

        private BuildMaterializationResult MaterializeCore(SceneResourceMap map, ICollection<BuildMaterializationIssue> issues)
        {
            var candidates = new List<BuildContentCandidate>();
            var requirements = new List<BuildContentRequirement>();
            var dependencies = new List<BuildDependencySnapshot>();
            var tags = new Dictionary<string, IReadOnlyList<BuildTag>>(StringComparer.Ordinal);
            var stableKeys = new HashSet<string>(StringComparer.Ordinal);
            var physicalKeys = new HashSet<string>(StringComparer.Ordinal);
            var drafts = new List<NodeDraft>();
            var closureBuilder = new AssetDependencySnapshotBuilder(_gateway);

            // 入力順によらず重複を報告できるよう、走査前に有効な論理キーを集計する。
            var identities = new HashSet<string>(map.SceneResources.Where(x => x != null && IsStable(x.Identity))
                .Select(x => x.Identity), StringComparer.Ordinal);
            foreach (var duplicate in map.SceneResources.Where(x => x != null && IsStable(x.Identity))
                .GroupBy(x => x.Identity, StringComparer.Ordinal).Where(x => x.Count() > 1))
                Add(issues, BuildMaterializationIssueCode.DuplicateLogicalKey,
                    BuildMaterializationSubject.Resource, duplicate.Key);

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
                if (resource.SceneAssetDescription == null)
                {
                    Add(issues, BuildMaterializationIssueCode.MissingDescription, BuildMaterializationSubject.Resource, logical);
                    continue;
                }

                var draft = new NodeDraft(logical, ParentIdentity(resource, issues), ChildIdentities(resource, issues),
                    ScenePayloadMappingPolicy.Load(resource.LoadType));
                drafts.Add(draft);

                foreach (var payload in resource.SceneAssetDescription.Payloads)
                {
                    var representation = ValidatePayload(payload, logical, BuildMaterializationSubject.Payload, issues, out var guid);
                    if (representation == null || guid == null) continue;
                    if (!draft.SceneRepresentations.Contains(representation, StringComparer.Ordinal))
                        draft.SceneRepresentations.Add(representation);
                    var closure = closureBuilder.Build(guid, logical, issues);
                    if (closure == null) continue;
                    var candidate = ScenePayloadMappingPolicy.Candidate(logical, representation, guid, closure.RootPath);
                    Admit(candidate, closure, representation, candidates, dependencies, tags, stableKeys, physicalKeys, issues);
                }
                // Requirements は「宣言された payload 群」。payload の無い論理 node には作らない（到達と要求は resolver が宣言から決める）。
                if (draft.SceneRepresentations.Count != 0)
                    requirements.Add(new BuildContentRequirement(logical, BuildContentCardinality.ExactlyOne));

                // 付随 asset。所有 node は候補の metadata に置き、logical key は `<node>:<名前>`。
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var attached in resource.AttachedAssets)
                {
                    if (attached == null || !IsStable(attached.Name))
                    {
                        Add(issues, BuildMaterializationIssueCode.InvalidAttachedAssetName, BuildMaterializationSubject.AttachedAsset, logical, detail: attached?.Name);
                        continue;
                    }
                    var attachedKey = AttachedAssetDescription.LogicalKey(logical, attached.Name);
                    if (!names.Add(attached.Name))
                    {
                        Add(issues, BuildMaterializationIssueCode.DuplicateAttachedAssetName, BuildMaterializationSubject.AttachedAsset, attachedKey);
                        continue;
                    }
                    // 付随 asset の key が別の node の identity と同じだと、Runtime の読み口と directory の entry が二重になる。
                    if (identities.Contains(attachedKey))
                    {
                        Add(issues, BuildMaterializationIssueCode.AttachedAssetKeyCollision, BuildMaterializationSubject.AttachedAsset, attachedKey);
                        continue;
                    }
                    var representations = new List<string>();
                    foreach (var payload in attached.Payloads)
                    {
                        var representation = ValidatePayload(payload, attachedKey, BuildMaterializationSubject.AttachedAsset, issues, out var guid);
                        if (representation == null || guid == null) continue;
                        if (!representations.Contains(representation, StringComparer.Ordinal)) representations.Add(representation);
                        var closure = closureBuilder.Build(guid, attachedKey, issues);
                        if (closure == null) continue;
                        var candidate = ScenePayloadMappingPolicy.AttachedCandidate(logical, attached.Name, representation, guid, closure.RootPath);
                        Admit(candidate, closure, representation, candidates, dependencies, tags, stableKeys, physicalKeys, issues);
                    }
                    if (representations.Count == 0)
                    {
                        // 参照の無い宣言は「何も要らない」ではなく宣言の欠陥。payload の検査が issue を出していなければここで出す。
                        if (attached.Payloads.Count == 0)
                            Add(issues, BuildMaterializationIssueCode.MissingReference, BuildMaterializationSubject.AttachedAsset, attachedKey);
                        continue;
                    }
                    draft.AttachedAssets.Add(new BuildGraphAttachedAsset(attached.Name, representations));
                    requirements.Add(new BuildContentRequirement(attachedKey, BuildContentCardinality.ExactlyOne));
                }
            }

            // 別 root 間でも同一 GUID が異なるパスを指す場合は、成功 snapshot にしない。
            foreach (var group in dependencies.SelectMany(x => x.Entries)
                .GroupBy(x => x.Guid, StringComparer.Ordinal))
            {
                var paths = group.Select(x => x.Path).Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray();
                for (var i = 1; i < paths.Length; i++)
                    issues.Add(new BuildMaterializationIssue(
                        BuildMaterializationIssueCode.DependencyIdentityCollision,
                        BuildMaterializationSubject.Dependency, group.Key,
                        path: paths[0], detailKey: paths[i]));
            }

            var snapshot = issues.Count == 0
                ? new BuildMaterializationSnapshot(candidates, tags, requirements, dependencies,
                    drafts.Select(x => new BuildGraphNode(x.Identity, x.Parent, x.Children, x.Load, x.SceneRepresentations, x.AttachedAssets)))
                : null;
            return new BuildMaterializationResult(snapshot, issues);
        }

        private static string? ParentIdentity(SceneResource resource, ICollection<BuildMaterializationIssue> issues)
        {
            // SceneResource は UnityEngine.Object。破棄済みを ?. で素通しさせない。
            if (resource.Parent == null) return null;
            if (IsStable(resource.Parent.Identity)) return resource.Parent.Identity;
            Add(issues, BuildMaterializationIssueCode.InvalidGraphLink, BuildMaterializationSubject.Resource, resource.Identity, detail: "parent");
            return null;
        }

        private static IReadOnlyList<string> ChildIdentities(SceneResource resource, ICollection<BuildMaterializationIssue> issues)
        {
            var children = new List<string>();
            foreach (var child in resource.Children)
            {
                if (child == null || !IsStable(child.Identity))
                {
                    Add(issues, BuildMaterializationIssueCode.InvalidGraphLink, BuildMaterializationSubject.Resource, resource.Identity, detail: "child");
                    continue;
                }
                children.Add(child.Identity);
            }
            return children;
        }

        // payload の Variant と参照を検査し、表現名と正規化した GUID を返す。失敗は issue にして null を返す。
        private static string? ValidatePayload(AssetPayload? payload, string subjectKey, BuildMaterializationSubject subject,
            ICollection<BuildMaterializationIssue> issues, out string? guid)
        {
            guid = null;
            if (payload == null)
            {
                Add(issues, BuildMaterializationIssueCode.NullPayload, subject, subjectKey);
                return null;
            }
            var variant = payload.Variant;
            if (variant == null || (variant.Length != 0 && !IsStable(variant)))
            {
                Add(issues, BuildMaterializationIssueCode.InvalidVariant, subject, subjectKey, detail: variant);
                return null;
            }
            if (payload.Reference == null || string.IsNullOrEmpty(payload.Reference.AssetGUID))
            {
                Add(issues, BuildMaterializationIssueCode.MissingReference, subject, subjectKey);
                return null;
            }
            guid = AssetDependencySnapshotBuilder.NormalizeGuid(payload.Reference.AssetGUID);
            if (guid == null)
            {
                Add(issues, BuildMaterializationIssueCode.InvalidRootGuid, subject, subjectKey, rootGuid: payload.Reference.AssetGUID);
                return null;
            }
            return ScenePayloadMappingPolicy.Representation(variant);
        }

        private static void Admit(BuildContentCandidate candidate, BuildDependencySnapshot closure, string representation,
            ICollection<BuildContentCandidate> candidates, ICollection<BuildDependencySnapshot> dependencies,
            IDictionary<string, IReadOnlyList<BuildTag>> tags, ISet<string> stableKeys, ISet<string> physicalKeys,
            ICollection<BuildMaterializationIssue> issues)
        {
            if (!stableKeys.Add(candidate.StableKey))
            {
                Add(issues, BuildMaterializationIssueCode.DuplicateStableKey, BuildMaterializationSubject.Payload,
                    candidate.StableKey, candidate.PhysicalKey);
                return;
            }
            // 同じ physical asset を Scene payload と付随 asset、または 2 つの node が宣言するのは衝突。共有は祖先に 1 回だけ宣言する。
            if (!physicalKeys.Add(candidate.PhysicalKey))
            {
                Add(issues, BuildMaterializationIssueCode.PhysicalKeyCollision, BuildMaterializationSubject.Payload,
                    candidate.PhysicalKey, candidate.PhysicalKey);
                return;
            }
            candidates.Add(candidate);
            dependencies.Add(closure);
            tags.Add(candidate.StableKey, new[] { ScenePayloadMappingPolicy.Tag(representation) });
        }

        private static bool IsStable(string? value) => !string.IsNullOrEmpty(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);
        private static void Add(ICollection<BuildMaterializationIssue> issues, BuildMaterializationIssueCode code,
            BuildMaterializationSubject subject, string subjectKey, string? rootGuid = null, string? path = null, string? detail = null) =>
            issues.Add(new BuildMaterializationIssue(code, subject, subjectKey, rootGuid, path, detail));
    }
}
