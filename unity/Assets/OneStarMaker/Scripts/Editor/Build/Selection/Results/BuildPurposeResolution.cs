#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OneStarMaker.Build.Selection
{
    /// <summary>
    /// 到達規則が導いた requirement 1 件。(logical key, 表現) ごとに厳密 1 件の候補を要求し、
    /// root からの到達経路（identity の列）を「なぜ入っているか」として preflight に残す。
    /// 付随 asset の requirement の経路は、所有 node までの経路。
    /// </summary>
    public sealed class BuildPurposeRequirement
    {
        private readonly ReadOnlyCollection<string> _path;

        public BuildPurposeRequirement(string logicalKey, string representation, IEnumerable<string> path)
        {
            if (!BuildRepresentationSelection.IsStable(logicalKey))
                throw new ArgumentException("Logical key must be non-empty and have no surrounding whitespace.", nameof(logicalKey));
            if (!BuildRepresentationSelection.IsStable(representation))
                throw new ArgumentException("Representation must be non-empty and have no surrounding whitespace.", nameof(representation));
            if (path == null) throw new ArgumentNullException(nameof(path));
            var snapshot = path.ToArray();
            if (snapshot.Length == 0 || snapshot.Any(step => !BuildRepresentationSelection.IsStable(step)))
                throw new ArgumentException("A requirement path must start at a root and contain stable identities.", nameof(path));
            LogicalKey = logicalKey;
            Representation = representation;
            _path = Array.AsReadOnly(snapshot);
        }

        public string LogicalKey { get; }
        public string Representation { get; }

        /// <summary>root から所有 node までの identity の列。先頭は宣言の root の 1 つ。</summary>
        public IReadOnlyList<string> Path => _path;

        internal BuildContentRequirement ToContentRequirement() =>
            new BuildContentRequirement(LogicalKey, BuildContentCardinality.ExactlyOne, Representation);
    }

    /// <summary>
    /// 宣言 1 つに対する到達と選択の結果。graph の検証や requirement の導出で止まった場合、
    /// <see cref="Selection"/> は plan を持たず issue だけを持ち、<see cref="Requirements"/> は空になる。
    /// </summary>
    public sealed class BuildPurposeResolution
    {
        private readonly ReadOnlyCollection<BuildPurposeRequirement> _requirements;

        internal BuildPurposeResolution(BuildPlanResult selection, IEnumerable<BuildPurposeRequirement> requirements, BuildRequest request)
        {
            Selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _requirements = Array.AsReadOnly(requirements
                .OrderBy(item => item.LogicalKey, StringComparer.Ordinal)
                .ThenBy(item => item.Representation, StringComparer.Ordinal)
                .ToArray());
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public BuildPlanResult Selection { get; }

        /// <summary>logical key、次に表現の ordinal 順。</summary>
        public IReadOnlyList<BuildPurposeRequirement> Requirements => _requirements;

        /// <summary>selector に渡した request（宣言の filter と表現の優先順の和）。</summary>
        public BuildRequest Request { get; }

        public bool IsSuccess => Selection.IsSuccess;
    }
}
