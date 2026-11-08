#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// provider が返す候補。provider は候補を申告するだけで、採るかどうかは resolver と規則が決める。
    /// 由来（<see cref="SourceKind"/>）とロード経路（<see cref="LoadPath"/>）は別の欄として持つ。
    /// </summary>
    public sealed class ContentCandidate
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyFacts =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

        /// <param name="providerId">候補を返した provider の id。空は拒む。resolver は probe した provider の id と ordinal で照合する。</param>
        /// <param name="sourceKind">由来。<see cref="ContentSourceKind.None"/> は拒む（由来の無い候補を作らせない）。</param>
        /// <param name="representation">
        /// 候補の表現。空は拒む。空の表現で content 入口を呼ぶと既存 index の fallback が実際の表現を変え、
        /// 台帳には空が記録されてしまうため。
        /// </param>
        /// <param name="manifestReference">opaque な参照。path として信用しない。</param>
        /// <param name="loadPath">ロード経路。<see cref="ContentLoadPath.None"/> は拒む。</param>
        /// <param name="facts">provider 固有の事実の小さな辞書。複製して保持するので、渡した辞書を後から変えても影響しない。</param>
        public ContentCandidate(
            string providerId,
            ContentSourceKind sourceKind,
            string logicalKey,
            ContentKind kind,
            string representation,
            ContentIdentity identity,
            string? manifestReference,
            ContentAcceptanceBasis acceptanceBasis,
            ContentLoadPath loadPath,
            IReadOnlyDictionary<string, string>? facts = null)
        {
            if (string.IsNullOrWhiteSpace(providerId))
            {
                throw new ArgumentException("Provider id is required.", nameof(providerId));
            }

            if (sourceKind == ContentSourceKind.None || !Enum.IsDefined(typeof(ContentSourceKind), sourceKind))
            {
                throw new ArgumentException("Source kind is required.", nameof(sourceKind));
            }

            if (representation == null)
            {
                throw new ArgumentNullException(nameof(representation));
            }

            if (representation.Length == 0)
            {
                throw new ArgumentException("Candidate representation must not be empty.", nameof(representation));
            }

            if (loadPath == ContentLoadPath.None || !Enum.IsDefined(typeof(ContentLoadPath), loadPath))
            {
                throw new ArgumentException("Load path is required.", nameof(loadPath));
            }

            ProviderId = providerId;
            SourceKind = sourceKind;
            LogicalKey = logicalKey ?? throw new ArgumentNullException(nameof(logicalKey));
            Kind = kind;
            Representation = representation;
            Identity = identity;
            ManifestReference = manifestReference ?? string.Empty;
            AcceptanceBasis = acceptanceBasis;
            LoadPath = loadPath;
            Facts = CopyFacts(facts);
        }

        public string ProviderId { get; }

        public ContentSourceKind SourceKind { get; }

        public string LogicalKey { get; }

        public ContentKind Kind { get; }

        public string Representation { get; }

        public ContentIdentity Identity { get; }

        public string ManifestReference { get; }

        public ContentAcceptanceBasis AcceptanceBasis { get; }

        public ContentLoadPath LoadPath { get; }

        public IReadOnlyDictionary<string, string> Facts { get; }

        private static IReadOnlyDictionary<string, string> CopyFacts(IReadOnlyDictionary<string, string>? facts)
        {
            if (facts == null || facts.Count == 0)
            {
                return EmptyFacts;
            }

            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in facts)
            {
                if (pair.Key == null)
                {
                    throw new ArgumentException("Fact keys must not be null.", nameof(facts));
                }

                copy[pair.Key] = pair.Value ?? string.Empty;
            }

            return new ReadOnlyDictionary<string, string>(copy);
        }
    }
}
