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
    /// 検証済みの installed revision から登録した session の 1 つの層（base か、重ねた上書き）を、要求時解決の供給元として答える。
    /// 候補の有無と実効の表現は session の層の解決から、identity と互換の事実は、base の層なら登録時の検証の値、
    /// 上書きの層なら上書きの root の記録から作る。候補が今読む directory の層の物かの照合（<see cref="IContentSourceBinding"/>）も答える。
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
    /// <para>
    /// 上書きを重ねた session では層ごとに 1 つ作り（id は別）、policy は上書き → base の順に並べる。各供給元は、層の解決（session と共有）が
    /// 自分の層を選んだ時だけ候補を返す。base の層の供給元は、上書きの層が選ばれた要求に NotFound を返す（Unity は同じ id の物を上書きの版で
    /// 出すので、base を指す候補を返すと record と base の pin が嘘になる）。
    /// </para>
    /// </remarks>
    internal sealed class RegisteredDirectoryProvider : IContentProvider, IContentSourceBinding
    {
        /// <summary>登録した directory（base の層）の供給元の id。</summary>
        internal const string DefaultId = "registered-content-directory";

        /// <summary>base に重ねた上書きの層の供給元の id。base の供給元と別の値にし、loader が照合の相手を取り違えないようにする。</summary>
        internal const string OverlayId = "registered-content-overlay";

        private readonly ContentDirectorySession _session;
        private readonly bool _overlay;
        private readonly IReadOnlyDictionary<string, string> _facts;

        /// <summary>base の層の供給元（1 directory の登録でも、上書きを重ねた登録でも同じ）。</summary>
        /// <exception cref="ArgumentException">session が検証済みの登録でない（呼び手は先に <see cref="ContentDirectorySession.VerifiedRevision"/> を見る）。</exception>
        internal RegisteredDirectoryProvider(ContentDirectorySession session, string id = DefaultId)
            : this(session, false, id)
        {
        }

        /// <summary>層を指定して作る。<paramref name="overlay"/> が true なら上書きの層の供給元（session が上書きを持たなければ拒む）。</summary>
        /// <exception cref="ArgumentException">session が検証済みの登録でない、または上書きの層を持たない session に上書きの層を指定した。</exception>
        internal RegisteredDirectoryProvider(ContentDirectorySession session, bool overlay, string id)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Provider id is required.", nameof(id));
            var verified = session.VerifiedRevision
                ?? throw new ArgumentException("Only a session registered from a verified installed revision can be a provider.", nameof(session));

            Id = id;
            _overlay = overlay;
            if (!overlay)
            {
                Identity = new ContentIdentity(verified.ContentSet, verified.Identity, verified.Target, verified.ManifestSha256);
                // 事実は検証で読んだ manifest の値から作る。基準と同じ定数から作ると互換の判定が常に通る。
                _facts = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ContentCompatibilityFactKeys.EngineVersion] = verified.UnityVersion,
                    [ContentCompatibilityFactKeys.RootSchemaVersion] = verified.RootSchemaVersion.ToString(CultureInfo.InvariantCulture),
                };
                return;
            }

            var layer = session.OverlayLayer
                ?? throw new ArgumentException("The session has no overlay layer.", nameof(session));
            // 登録は照合を通った上書きの層だけを持つので、ここで記録が無いことは無い（session が無ければ上で拒む）。
            var record = layer.Root!.OverlayBase!;
            // identity は上書き自身の値（contentSet は root の記録、revision は上書きの build identity、target は登録時の base の検証済みの target）。
            // digest は持たない（上書きは install も publish もされない）。上書きを pin で選ぶ要求は作れない（13 章が受け入れた制約）。
            Identity = new ContentIdentity(record.OverlayContentSet, layer.Identity, session.Target, string.Empty);
            // 事実は上書きの root から作る（基準の定数や base の検証の値から作らない）。base の 4 欄は記録の写しで、
            // 記録と実際に登録した base の一致は登録の口の照合が保証する（RecordedBaseMatch の保証の境界）。
            _facts = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ContentCompatibilityFactKeys.EngineVersion] = record.UnityVersion,
                [ContentCompatibilityFactKeys.RootSchemaVersion] = layer.Root!.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                [ContentCompatibilityFactKeys.OverlayBaseContentSet] = record.BaseContentSet,
                [ContentCompatibilityFactKeys.OverlayBaseRevision] = record.BaseRevision,
                [ContentCompatibilityFactKeys.OverlayBaseTarget] = record.BaseTarget,
                [ContentCompatibilityFactKeys.OverlayBaseManifestSha256] = record.BaseManifestSha256,
            };
        }

        public string Id { get; }

        public ContentSourceKind SourceKind => _overlay ? ContentSourceKind.ContentOverlay : ContentSourceKind.ContentDirectory;

        private ContentAcceptanceBasis Basis => _overlay ? ContentAcceptanceBasis.RecordedBaseMatch : ContentAcceptanceBasis.VerifiedDigest;

        /// <summary>
        /// この層の候補が持つ identity。base の層は登録の検証済みの値（contentSet・revision・target・manifest の SHA-256）、
        /// 上書きの層は上書き自身の contentSet・build identity・target と空の digest。
        /// </summary>
        internal ContentIdentity Identity { get; }

        public ValueTask<ContentAvailability> FindAsync(ContentRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();

            if (!_session.IsAccepting)
                return new ValueTask<ContentAvailability>(new ContentAvailability(
                    ContentAvailabilityStatus.Unavailable, reason: "The registered content directory is no longer accepting loads."));

            if (!TryMapKind(request.Kind, out var kind))
                return NotFound("No matching entry in the registered content directory.");
            var resolution = _session.ResolveEntry(request.LogicalKey, kind, request.Representation);
            var entry = resolution.Entry;
            if (entry == null)
                return NotFound("No matching entry in the registered content directory.");
            if (resolution.Layer!.IsOverlay != _overlay)
                return NotFound(_overlay
                    ? "The base layer holds the entry for this request; the overlay layer does not."
                    : resolution.BaseShadowedAtRequested
                        ? "The base entry is shadowed by an overlay entry with the same native id; the overlay layer was selected."
                        : "The overlay layer holds the entry for this request and was selected.");

            var candidate = new ContentCandidate(
                Id,
                SourceKind,
                request.LogicalKey,
                request.Kind,
                ContentEntryMatch.EffectiveRepresentation(entry.Representation),
                Identity,
                // 参照は opaque。install 先・上書きの path を記録へ出さない。
                null,
                Basis,
                // load の経路は base と同じ content 入口（loader はこの経路の候補だけを content 入口に渡す）。
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

            // 層まで比べる: 由来と受け入れ根拠がこの層の値で、identity がこの層の identity であること。
            // base の供給元の候補を上書きの層の照合に（逆も）渡すと偽になる。
            return _session.IsAccepting
                && string.Equals(candidate.ProviderId, Id, StringComparison.Ordinal)
                && candidate.SourceKind == SourceKind
                && candidate.AcceptanceBasis == Basis
                && candidate.LoadPath == ContentLoadPath.ContentDirectory
                && candidate.Identity.Equals(Identity);
        }

        private static ValueTask<ContentAvailability> NotFound(string reason)
            => new(new ContentAvailability(ContentAvailabilityStatus.NotFound, reason: reason));

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
