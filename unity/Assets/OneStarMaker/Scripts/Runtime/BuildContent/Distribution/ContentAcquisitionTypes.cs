#nullable enable

using System;
using System.Collections.Generic;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    /// <summary>bytes と別の経路で得た revision と期待 digest の組。pin がある取得は、取得元の head と申告を使わない。</summary>
    public sealed class ContentPin
    {
        public ContentPin(string revision, string manifestSha256)
        {
            Revision = ContentOriginQuery.RequireSegment(revision, nameof(revision));
            if (!ContentManifestValidation.Hash(manifestSha256))
                throw new ArgumentException("Pinned digest must be 64 lowercase hex characters.", nameof(manifestSha256));
            ManifestSha256 = manifestSha256;
        }

        public string Revision { get; }
        public string ManifestSha256 { get; }
    }

    /// <summary>host が渡す取得元の列の 1 件。列の順が当たる順（遅い取得元を最後に置ける）。</summary>
    public sealed class ContentAcquisitionOrigin
    {
        public ContentAcquisitionOrigin(IContentOrigin origin, ContentOriginTrust trust)
        {
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
            if (!Enum.IsDefined(typeof(ContentOriginTrust), trust)) throw new ArgumentOutOfRangeException(nameof(trust));
            Trust = trust;
        }

        public IContentOrigin Origin { get; }
        public ContentOriginTrust Trust { get; }
    }

    public sealed class ContentAcquisitionRequest
    {
        public ContentAcquisitionRequest(string contentSet, string target, string unityVersion, int rootSchemaVersion,
            long diskBudgetBytes, ContentPin? pin = null)
        {
            ContentSet = ContentOriginQuery.RequireSegment(contentSet, nameof(contentSet));
            Target = ContentOriginQuery.RequireSegment(target, nameof(target));
            UnityVersion = string.IsNullOrWhiteSpace(unityVersion) ? throw new ArgumentException("Unity version is required.", nameof(unityVersion)) : unityVersion;
            RootSchemaVersion = rootSchemaVersion;
            if (diskBudgetBytes <= 0) throw new ArgumentOutOfRangeException(nameof(diskBudgetBytes));
            DiskBudgetBytes = diskBudgetBytes;
            Pin = pin;
        }

        public string ContentSet { get; }
        public string Target { get; }
        public string UnityVersion { get; }
        public int RootSchemaVersion { get; }
        public long DiskBudgetBytes { get; }
        public ContentPin? Pin { get; }
    }

    /// <summary>取得の終わり方。取得の成否だけで、Play が使う revision は呼び手（host）が別に確かめる。</summary>
    public enum ContentAcquisitionState
    {
        /// <summary>取得元から取得・検証して、この呼び出しが install を公開した。</summary>
        Acquired,
        /// <summary>対象の組が既に到着点にあり、再検証が通った（bytes を得た取得元は空）。</summary>
        AlreadyInstalled,
        /// <summary>対象が決まらない、または全ての取得元が対象を持たない・届かない。</summary>
        NotFound,
        /// <summary>取得元が尽き、少なくとも 1 つの取得元の bytes が対象の digest と違った。</summary>
        IntegrityFailure,
        /// <summary>digest は対象と同じだが、manifest が壊れている・identity か target が違う・互換でない（どの取得元でも同じ bytes）。</summary>
        TargetUnusable,
        /// <summary>到着点に同じ revision が違う digest で入っている。上書きしない。</summary>
        ArrivalDigestConflict,
        /// <summary>到着点（cache）の側の失敗。取得元を替えても直らない。</summary>
        ArrivalFailure,
        /// <summary>呼び手の token の取消。取得元が自分で投げた取消はここに入らない。</summary>
        Canceled,
    }

    public enum ContentAcquisitionStage { Target = 1, Arrival = 2, Fetch = 3 }

    /// <summary>1 つの取得元に当たった 1 回の記録。</summary>
    public sealed class ContentAcquisitionAttempt
    {
        internal ContentAcquisitionAttempt(ContentAcquisitionStage stage, string originId, string outcome, string reason, bool readManifest)
        {
            Stage = stage;
            OriginId = originId;
            Outcome = outcome;
            Reason = reason;
            ReadManifest = readManifest;
        }

        public ContentAcquisitionStage Stage { get; }
        public string OriginId { get; }
        /// <summary>試行の結果の短い名前（<c>Target</c>、<c>NotFound</c>、<c>Unavailable</c>、<c>Unreachable</c>、<c>Integrity</c>、<c>Installed</c> など）。</summary>
        public string Outcome { get; }
        public string Reason { get; }
        /// <summary>この取得元から <c>transport.json</c> を読んだか（自分が install を公開したとは限らない）。</summary>
        public bool ReadManifest { get; }
    }

    public sealed class ContentAcquisitionResult
    {
        internal ContentAcquisitionResult(ContentAcquisitionState state, string reason, ContentInstallResult? install, string? revision,
            string? manifestSha256, string? targetOriginId, string? bytesOriginId, ContentDigestAuthority? authority,
            IReadOnlyList<ContentAcquisitionAttempt> attempts, IReadOnlyList<string> removedInstalls, IReadOnlyList<string> diagnostics)
        {
            State = state;
            Reason = reason;
            Install = install;
            Revision = revision;
            ManifestSha256 = manifestSha256;
            TargetOriginId = targetOriginId;
            BytesOriginId = bytesOriginId;
            DigestAuthority = authority;
            Attempts = attempts;
            RemovedInstalls = removedInstalls;
            Diagnostics = diagnostics;
        }

        public ContentAcquisitionState State { get; }
        public string Reason { get; }
        /// <summary><see cref="ContentAcquisitionState.Acquired"/> と <see cref="ContentAcquisitionState.AlreadyInstalled"/> の時の install（再利用の印を含む）。</summary>
        public ContentInstallResult? Install { get; }
        /// <summary>対象の組（決まった時だけ）。</summary>
        public string? Revision { get; }
        public string? ManifestSha256 { get; }
        /// <summary>対象を決めた取得元（pin の時は null）。</summary>
        public string? TargetOriginId { get; }
        /// <summary>bytes を得て、この呼び出しが install を公開した取得元（install 済みの時は null）。</summary>
        public string? BytesOriginId { get; }
        public ContentDigestAuthority? DigestAuthority { get; }
        public IReadOnlyList<ContentAcquisitionAttempt> Attempts { get; }
        /// <summary>
        /// 取得の前後で cache から消えた install（<c>&lt;contentSet&gt;/&lt;revision&gt;</c>）。installer の容量の確保が消しうる。
        /// cache の transaction の外で読んだ観測の差で、他の操作の削除も入りうる（原因の帰属ではない）。
        /// </summary>
        public IReadOnlyList<string> RemovedInstalls { get; }
        /// <summary>結果を変えない診断（片付けに失敗した staging の path、観測できなかった事実）。</summary>
        public IReadOnlyList<string> Diagnostics { get; }
    }
}
