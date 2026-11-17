#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.BuildContent;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 検証済みの installed revision から登録した 1 つの Content Directory を、要求時解決の供給元として答える。
    /// 候補の有無と実効の表現は directory の索引から、identity と互換の事実は登録時の検証の値から作る。
    /// 候補が今読む directory の物かの照合（<see cref="IContentSourceBinding"/>）も答える。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 連携部分の型である。BuildContent の session・索引と、AssetManagement の internal の問い合わせを使う。
    /// AssetManagement と BuildSystem 本体はこの型を知らない（参照の向きは一方向）。
    /// </para>
    /// <para>
    /// 寿命は session と同じ。session の token を取らず、native の load もしないので、供給元が生きていても close は完了する。
    /// close・Play 停止の後は <see cref="ContentAvailabilityStatus.Unavailable"/> を返し、照合は偽になる。
    /// </para>
    /// <para>
    /// 表現は索引が返した entry の実効の表現をそのまま申告し、採るかどうかをここで判定しない（判定は resolver だけに置く）。
    /// 索引は要求した表現の entry が無い時に Full の entry を返すので、要求と違う表現の候補を返すことがある。
    /// 要求が Full を fallback に明示していなければ、resolver がその候補を採らない。
    /// </para>
    /// </remarks>
    internal sealed class RegisteredDirectoryProvider : IContentProvider, IContentSourceBinding
    {
        /// <summary>登録した directory の供給元の id。directory が 1 つの間は固定の値で足りる。</summary>
        internal const string DefaultId = "registered-content-directory";

        private readonly ContentDirectorySession _session;
        private readonly IReadOnlyDictionary<string, string> _facts;

        /// <exception cref="ArgumentException">session が検証済みの登録でない（呼び手は先に <see cref="ContentDirectorySession.VerifiedRevision"/> を見る）。</exception>
        internal RegisteredDirectoryProvider(ContentDirectorySession session, string id = DefaultId)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Provider id is required.", nameof(id));
            var verified = session.VerifiedRevision
                ?? throw new ArgumentException("Only a session registered from a verified installed revision can be a provider.", nameof(session));

            Id = id;
            Identity = new ContentIdentity(verified.ContentSet, verified.Identity, verified.Target, verified.ManifestSha256);
            // 事実は検証で読んだ manifest の値から作る。基準と同じ定数から作ると互換の判定が常に通る。
            _facts = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ContentCompatibilityFactKeys.EngineVersion] = verified.UnityVersion,
                [ContentCompatibilityFactKeys.RootSchemaVersion] = verified.RootSchemaVersion.ToString(CultureInfo.InvariantCulture),
            };
        }

        public string Id { get; }

        public ContentSourceKind SourceKind => ContentSourceKind.ContentDirectory;

        /// <summary>登録の検証済みの identity（contentSet・revision・target・manifest の SHA-256）。候補は全てこの値を持つ。</summary>
        internal ContentIdentity Identity { get; }

        public ValueTask<ContentAvailability> FindAsync(ContentRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();

            if (!_session.IsAccepting)
                return new ValueTask<ContentAvailability>(new ContentAvailability(
                    ContentAvailabilityStatus.Unavailable, reason: "The registered content directory is no longer accepting loads."));

            if (!TryMapKind(request.Kind, out var kind)
                || !_session.TryFindEntry(request.LogicalKey, kind, request.Representation, out var entry)
                || entry == null)
                return new ValueTask<ContentAvailability>(new ContentAvailability(
                    ContentAvailabilityStatus.NotFound, reason: "No matching entry in the registered content directory."));

            var candidate = new ContentCandidate(
                Id,
                ContentSourceKind.ContentDirectory,
                request.LogicalKey,
                request.Kind,
                ContentEntryMatch.EffectiveRepresentation(entry.Representation),
                Identity,
                // 参照は opaque。install 先の path を記録へ出さない。
                null,
                ContentAcceptanceBasis.VerifiedDigest,
                ContentLoadPath.ContentDirectory,
                _facts);
            return new ValueTask<ContentAvailability>(new ContentAvailability(ContentAvailabilityStatus.Available, candidate));
        }

        /// <inheritdoc />
        public bool IsLoadedBy(ContentCandidate candidate, IAssetManagement assetManagement)
        {
            if (candidate == null || assetManagement == null)
                return false;

            // 実物の AssetManagement だけを信じる。wrapper や別実装が今どの directory を読むかは、ここから確かめられない。
            if (!(assetManagement is AssetManagement.AssetManagement concrete) || !concrete.IsInstalledContentDirectory(_session))
                return false;

            return _session.IsAccepting
                && string.Equals(candidate.ProviderId, Id, StringComparison.Ordinal)
                && candidate.SourceKind == ContentSourceKind.ContentDirectory
                && candidate.LoadPath == ContentLoadPath.ContentDirectory
                && candidate.Identity.Equals(Identity);
        }

        private static bool TryMapKind(ContentKind kind, out BuildContentKind mapped)
        {
            switch (kind)
            {
                case ContentKind.Scene:
                    mapped = BuildContentKind.Scene;
                    return true;
                case ContentKind.Object:
                    mapped = BuildContentKind.Object;
                    return true;
                default:
                    mapped = default;
                    return false;
            }
        }
    }
}
