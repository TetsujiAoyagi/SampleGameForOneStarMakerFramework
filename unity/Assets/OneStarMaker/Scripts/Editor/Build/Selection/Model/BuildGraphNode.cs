#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OneStarMaker.Build.Selection
{
    /// <summary>
    /// 親のロードに伴う子の扱い。Runtime の LoadType を Selection から参照しないための写し。
    /// </summary>
    public enum BuildGraphLoad
    {
        /// <summary>明示の要求でだけロードする。到達は宣言の filter が決める。</summary>
        OnDemand,
        /// <summary>親と一緒に必ずロードする。親に到達すれば必ず到達する。</summary>
        Always,
        /// <summary>親の後に背景でロードする。到達の規則は Always と同じ。</summary>
        Incremental
    }

    /// <summary>
    /// node が所有し、その node か子孫の Scene が読む付随 asset の宣言（名前と宣言済み表現列）。
    /// 期待集合は候補から逆算せず、この宣言から作る。
    /// </summary>
    public sealed class BuildGraphAttachedAsset
    {
        private readonly ReadOnlyCollection<string> _representations;

        public BuildGraphAttachedAsset(string name, IEnumerable<string> representations)
        {
            if (!BuildRepresentationSelection.IsStable(name))
                throw new ArgumentException("Attached asset names must be non-empty and have no surrounding whitespace.", nameof(name));
            if (representations == null) throw new ArgumentNullException(nameof(representations));
            var snapshot = representations.ToArray();
            if (snapshot.Length == 0)
                throw new ArgumentException("An attached asset must declare at least one representation.", nameof(representations));
            if (snapshot.Any(value => !BuildRepresentationSelection.IsStable(value)))
                throw new ArgumentException("Representation names must be non-empty and have no surrounding whitespace.", nameof(representations));
            if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
                throw new ArgumentException("An attached asset must not declare the same representation twice.", nameof(representations));
            Name = name;
            _representations = Array.AsReadOnly(snapshot.OrderBy(value => value, StringComparer.Ordinal).ToArray());
        }

        public string Name { get; }

        /// <summary>宣言済みの表現。ordinal 順。</summary>
        public IReadOnlyList<string> Representations => _representations;
    }

    /// <summary>
    /// 到達規則の入力 1 node。SceneGraph に固有ではなく、identity・親子・ロード種別・宣言済みの表現だけを持つ。
    /// <see cref="SceneRepresentations"/> は node が Scene payload として宣言した表現（候補からは導かない）。
    /// 空なら payload の無い中間 node で、到達の経路になり付随 asset を持てるが、Scene の requirement は作らない。
    /// </summary>
    public sealed class BuildGraphNode
    {
        private readonly ReadOnlyCollection<string> _children;
        private readonly ReadOnlyCollection<string> _sceneRepresentations;
        private readonly ReadOnlyCollection<BuildGraphAttachedAsset> _attachedAssets;

        public BuildGraphNode(
            string identity,
            string? parent,
            IEnumerable<string> children,
            BuildGraphLoad load,
            IEnumerable<string> sceneRepresentations,
            IEnumerable<BuildGraphAttachedAsset>? attachedAssets = null)
        {
            if (!BuildRepresentationSelection.IsStable(identity))
                throw new ArgumentException("Node identity must be non-empty and have no surrounding whitespace.", nameof(identity));
            if (parent != null && !BuildRepresentationSelection.IsStable(parent))
                throw new ArgumentException("Parent identity must be null or stable.", nameof(parent));
            if (children == null) throw new ArgumentNullException(nameof(children));
            if (sceneRepresentations == null) throw new ArgumentNullException(nameof(sceneRepresentations));
            if (!Enum.IsDefined(typeof(BuildGraphLoad), load)) throw new ArgumentOutOfRangeException(nameof(load));
            // 子の列は入力順を保つ。到達の判定は順序に依存しないが、親子リンクの相互一致の検査で重複を数える。
            var childSnapshot = children.Select(child => BuildRepresentationSelection.IsStable(child)
                    ? child
                    : throw new ArgumentException("Child identities must be non-empty and have no surrounding whitespace.", nameof(children)))
                .ToArray();
            var representationSnapshot = sceneRepresentations.Select(value => BuildRepresentationSelection.IsStable(value)
                    ? value
                    : throw new ArgumentException("Representation names must be non-empty and have no surrounding whitespace.", nameof(sceneRepresentations)))
                .ToArray();
            if (representationSnapshot.Distinct(StringComparer.Ordinal).Count() != representationSnapshot.Length)
                throw new ArgumentException("A node must not declare the same scene representation twice.", nameof(sceneRepresentations));
            var attachedSnapshot = (attachedAssets ?? Array.Empty<BuildGraphAttachedAsset>())
                .Select(asset => asset ?? throw new ArgumentException("Attached assets cannot contain null.", nameof(attachedAssets)))
                .OrderBy(asset => asset.Name, StringComparer.Ordinal)
                .ToArray();
            if (attachedSnapshot.Select(asset => asset.Name).Distinct(StringComparer.Ordinal).Count() != attachedSnapshot.Length)
                throw new ArgumentException("Attached asset names must be unique within a node.", nameof(attachedAssets));

            Identity = identity;
            Parent = parent;
            _children = Array.AsReadOnly(childSnapshot);
            Load = load;
            _sceneRepresentations = Array.AsReadOnly(representationSnapshot.OrderBy(value => value, StringComparer.Ordinal).ToArray());
            _attachedAssets = Array.AsReadOnly(attachedSnapshot);
        }

        public string Identity { get; }
        public string? Parent { get; }
        public IReadOnlyList<string> Children => _children;
        public BuildGraphLoad Load { get; }

        /// <summary>Scene payload として宣言済みの表現。ordinal 順。空なら payload の無い中間 node。</summary>
        public IReadOnlyList<string> SceneRepresentations => _sceneRepresentations;

        /// <summary>この node が所有する付随 asset。名前の ordinal 順。</summary>
        public IReadOnlyList<BuildGraphAttachedAsset> AttachedAssets => _attachedAssets;
    }
}
