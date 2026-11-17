#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 1 要求の実績: 要求、採用した候補（由来）、probe の実績、所有の表記、到達度。
    /// </summary>
    /// <remarks>
    /// この記録は選択結果の記録で、型そのものは実ロード元の証拠を持たない。採用した候補と実際に読む directory の同一性は、
    /// 記録を作る側が確かめ、確かめたかどうかを <see cref="SourceBindingVerified"/> に書く。
    /// 現在の loader は、候補を返した供給元が「候補は今読む directory の物」と答えた時だけ読み、その時だけ記録を返すので、
    /// loader が成功として返す記録の <see cref="SourceBindingVerified"/> は常に true になる。欄を残すのは、後続の経路が
    /// 確かめられない記録を作りうるためである。
    /// 記録が言うのは「その handle の中身はこの directory の物」で、新しく読んだか既に載っていた物を再利用したかは言わない。
    /// <see cref="OwnerLabel"/> が表すのは AssetManagement の台帳上の owner だけで、Scene の lifecycle や
    /// 物理 Scene の unload 責任は SceneSystem 側の記録が担う。
    /// 由来の無い成功を作らせないため、候補の SourceKind・ProviderId・AcceptanceBasis のどれかが欠けていれば作れない。
    /// </remarks>
    public sealed class ContentLoadRecord
    {
        /// <param name="ownerLabel">要求時に渡した owner の表記（観測値ではない）。</param>
        public ContentLoadRecord(
            ContentRequest request,
            ContentCandidate candidate,
            IEnumerable<ProviderProbe> probes,
            string ownerLabel,
            ContentTraceability traceability,
            bool sourceBindingVerified)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (candidate.SourceKind == ContentSourceKind.None)
            {
                throw new ArgumentException("A record needs the candidate's source kind.", nameof(candidate));
            }

            if (string.IsNullOrWhiteSpace(candidate.ProviderId))
            {
                throw new ArgumentException("A record needs the candidate's provider id.", nameof(candidate));
            }

            if (candidate.AcceptanceBasis == ContentAcceptanceBasis.None)
            {
                throw new ArgumentException("A record needs the candidate's acceptance basis.", nameof(candidate));
            }

            if (probes == null)
            {
                throw new ArgumentNullException(nameof(probes));
            }

            if (string.IsNullOrWhiteSpace(ownerLabel))
            {
                throw new ArgumentException("Owner label is required.", nameof(ownerLabel));
            }

            if (!Enum.IsDefined(typeof(ContentTraceability), traceability))
            {
                throw new ArgumentException("Unknown traceability.", nameof(traceability));
            }

            var copy = new List<ProviderProbe>();
            foreach (var probe in probes)
            {
                copy.Add(probe ?? throw new ArgumentException("Probes must not contain null.", nameof(probes)));
            }

            Request = request;
            Candidate = candidate;
            Probes = new ReadOnlyCollection<ProviderProbe>(copy);
            OwnerLabel = ownerLabel;
            Traceability = traceability;
            SourceBindingVerified = sourceBindingVerified;
        }

        public ContentRequest Request { get; }

        /// <summary>採用した候補。由来（SourceKind・ProviderId）と受け入れ根拠はここから読む。</summary>
        public ContentCandidate Candidate { get; }

        public IReadOnlyList<ProviderProbe> Probes { get; }

        public string OwnerLabel { get; }

        public ContentTraceability Traceability { get; }

        /// <summary>
        /// 採用した候補と実際に読んだ directory の同一性を、読む前に確かめたか。現在の loader が成功として返す記録は常に true
        /// （確かめられない候補は読まずに拒むため）。
        /// </summary>
        public bool SourceBindingVerified { get; }
    }
}
