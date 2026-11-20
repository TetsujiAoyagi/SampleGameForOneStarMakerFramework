#nullable enable

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// Editor の Play の複製（snapshot）を要求時解決の供給元として答える。policy の先頭に置き、宣言にある identity の Scene の要求には
    /// 常に既ロードの経路（<see cref="ContentLoadPath.AlreadyLoadedScene"/>）の候補を返す。load は預け元を Scene の台帳に据えた AssetManagement の
    /// internal の口に任せる（<see cref="IContentSceneLoadBinding"/>）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 宣言にある identity には <see cref="ContentAvailabilityStatus.Unavailable"/> も <see cref="ContentAvailabilityStatus.NotFound"/> も返さない。
    /// どちらも resolver を次の供給元（directory）へ進ませ、利用者が開いて編集している Scene の代わりに build 済みの版が黙って動く。
    /// 宣言は 1 回の Play の間変わらないので、答えは要求の時点の状態に依らない。採れない事情（file が無い等）は load の時に失敗として出る。
    /// </para>
    /// <para>
    /// 候補の表現は要求の表現をそのまま申告し、表現の指定が無い要求には配線が渡した値（directory と同じ Full）を申告する（空の表現の候補は作れない）。
    /// 複製を採る理由は「利用者が開いている Scene であること」で、表現は検証しない。record の表現は要求の写しで、中身の表現の保証ではない。
    /// </para>
    /// <para>
    /// expected identity（pin）を載せた要求には、identity の無い <see cref="ContentAvailabilityStatus.Rejected"/> を返して探索を止める。
    /// 複製の identity は空なので pin と合わず、止めないと pin に合う directory の版が採られる。
    /// </para>
    /// <para>
    /// 宣言に無い identity と、Object の要求には NotFound を返す（Object の snapshot は扱わない）。
    /// </para>
    /// <para>
    /// 寿命は loader と同じ 1 回の Play。Scene も token も持たず、static の bridge を読まない（宣言は預け元の instance から読む）。
    /// Editor の build だけ。
    /// </para>
    /// </remarks>
    internal sealed class EditorSnapshotProvider : IContentProvider, IContentSceneLoadBinding
    {
        /// <summary>供給元の id。directory の供給元と別の値にする。</summary>
        internal const string DefaultId = "editor-play-snapshot";

        /// <summary>record の事実の key（ホスト固有の事実。互換の予約 key の接頭辞を使わない）。</summary>
        internal const string SourcePathFact = "osm.editorSnapshot.sourcePath";
        internal const string CopyPathFact = "osm.editorSnapshot.copyPath";
        internal const string WasDirtyFact = "osm.editorSnapshot.wasDirty";
        internal const string Sha256Fact = "osm.editorSnapshot.sha256";

        private readonly EditorSnapshotSceneSource _source;
        private readonly string _unspecifiedRepresentation;

        /// <param name="source">宣言と Unity の I/O を持つ預け元。配線は同じ instance を AssetManagement に据える。</param>
        /// <param name="unspecifiedRepresentation">表現の指定が無い要求に申告する表現（directory が代わりに返す Full）。</param>
        internal EditorSnapshotProvider(EditorSnapshotSceneSource source, string unspecifiedRepresentation, string id = DefaultId)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(unspecifiedRepresentation))
                throw new ArgumentException("The representation for unspecified requests is required.", nameof(unspecifiedRepresentation));
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Provider id is required.", nameof(id));
            _unspecifiedRepresentation = unspecifiedRepresentation;
            Id = id;
        }

        public string Id { get; }

        public ContentSourceKind SourceKind => ContentSourceKind.EditorSnapshot;

        /// <summary>宣言の件数（起動の log）。</summary>
        internal int DeclaredCount => _source.Declaration.Count;

        public ValueTask<ContentAvailability> FindAsync(ContentRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();

            if (request.Kind != ContentKind.Scene || !_source.TryGetDeclared(request.LogicalKey, out var declared))
                return new ValueTask<ContentAvailability>(new ContentAvailability(
                    ContentAvailabilityStatus.NotFound, reason: "The play snapshot does not declare this scene."));

            if (request.ExpectedIdentity.HasValue)
                return new ValueTask<ContentAvailability>(new ContentAvailability(
                    ContentAvailabilityStatus.Rejected,
                    reason: "This Play reads scene '" + declared.Identity + "' from the play snapshot; a request pinned to a content identity cannot be served."));

            var candidate = new ContentCandidate(
                Id,
                SourceKind,
                request.LogicalKey,
                request.Kind,
                request.Representation.Length != 0 ? request.Representation : _unspecifiedRepresentation,
                // 複製は検証した物ではないので identity を持たない。
                default,
                null,
                ContentAcceptanceBasis.OpenSceneSnapshot,
                ContentLoadPath.AlreadyLoadedScene,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [SourcePathFact] = declared.SourcePath,
                    [CopyPathFact] = declared.CopyPath,
                    [WasDirtyFact] = declared.WasDirty ? "true" : "false",
                    [Sha256Fact] = declared.Sha256,
                });
            return new ValueTask<ContentAvailability>(new ContentAvailability(ContentAvailabilityStatus.Available, candidate));
        }

        /// <inheritdoc />
        public bool Declares(string sceneIdentity) => _source.TryGetDeclared(sceneIdentity, out _);

        /// <inheritdoc />
        public bool IsLoadedBy(ContentCandidate candidate, IAssetManagement assetManagement)
        {
            if (candidate == null || assetManagement == null)
                return false;

            // 実物の AssetManagement で、この供給元の預け元を据えている物だけを信じる（wrapper や別実装の台帳は確かめられない）。
            if (!(assetManagement is AssetManagement.AssetManagement concrete) || !concrete.IsAdoptedSceneSource(_source))
                return false;

            return string.Equals(candidate.ProviderId, Id, StringComparison.Ordinal)
                && candidate.SourceKind == SourceKind
                && candidate.AcceptanceBasis == ContentAcceptanceBasis.OpenSceneSnapshot
                && candidate.LoadPath == ContentLoadPath.AlreadyLoadedScene
                && candidate.Kind == ContentKind.Scene
                && _source.TryGetDeclared(candidate.LogicalKey, out _);
        }

        /// <inheritdoc />
        public UniTask<ISceneHandle> LoadSceneAsync(ContentCandidate candidate, IAssetManagement assetManagement, SceneLoadOptions options, CancellationToken ct)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (!(assetManagement is AssetManagement.AssetManagement concrete) || !concrete.IsAdoptedSceneSource(_source))
                throw new InvalidOperationException("The asset manager does not hold this play snapshot's scene source.");
            return concrete.AdoptSceneAsync(candidate.LogicalKey, options, ct);
        }
    }
}
#endif
