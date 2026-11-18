#nullable enable

using System;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.BuildContent.Distribution;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 登録した session（1 つの directory、または base に上書きを 1 つ重ねた層の列）と AssetManagement から、
    /// 要求時解決の供給元・互換の基準・policy・loader を組み立てる。
    /// 起動の配線と test が同じ組み立てを使う（起動側に組み立ての判断を置かない）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 結果は 3 通りで、それ以外の分岐を持たない。
    /// (1) 検証済みの登録: 組み立てて返す。(2) 検証済みでない登録（<see cref="ContentDirectorySession.VerifiedRevision"/> が null）:
    /// 「作らない」と理由を返す。呼び手は今までどおり直接の経路で動く。(3) 検証済みの登録で組み立てが失敗した:
    /// 例外を投げる。呼び手は握らない（黙って直接の経路で動くと、実在の供給元が使われないまま Play が成功してしまう）。
    /// </para>
    /// <para>
    /// 互換の基準: contentSet と target は登録の検証済みの値、engine と root schema は Distribution の定数（この runtime が受け付ける値）。
    /// revision は固定しない。directory が 1 つの間は固定しても結果は変わらず、要求をまたぐ固定は後続（要求の時点の install）が決める。
    /// 候補の事実は manifest の値から作るので（<see cref="RegisteredDirectoryProvider"/>）、基準と事実は別の出所になる。
    /// </para>
    /// <para>
    /// 上書きを重ねた session では供給元を 2 つ（上書き → base の順、id は別）作る。<see cref="Provider"/> は今までどおり base の層の供給元で、
    /// 上書きの層の供給元は <see cref="OverlayProvider"/> にある（起動の log と既存の利用者が読む <see cref="Provider"/> の意味を変えない）。
    /// 互換の基準は base の検証済みの値のまま（上書きの候補は記録した base の値を基準と比べる）。
    /// </para>
    /// <para>
    /// 状態を持たず、session の token を取らない。route を捨てても close の順序は変わらない。
    /// </para>
    /// </remarks>
    internal sealed class RegisteredDirectoryRoute
    {
        private RegisteredDirectoryRoute(
            RegisteredDirectoryProvider provider,
            RegisteredDirectoryProvider? overlayProvider,
            ContentCompatibilityEnvelope envelope,
            ResolutionPolicy policy,
            ContentRequestLoader loader)
        {
            Provider = provider;
            OverlayProvider = overlayProvider;
            Envelope = envelope;
            Policy = policy;
            Loader = loader;
        }

        /// <summary>base の層（1 directory の登録ではその directory）の供給元。</summary>
        internal RegisteredDirectoryProvider Provider { get; }

        /// <summary>上書きの層の供給元。上書きを重ねない登録では null。</summary>
        internal RegisteredDirectoryProvider? OverlayProvider { get; }

        internal ContentCompatibilityEnvelope Envelope { get; }

        internal ResolutionPolicy Policy { get; }

        internal ContentRequestLoader Loader { get; }

        /// <summary>
        /// この directory が要求の表現の無い entry に代えて返す表現（索引の Full）。呼び手が要求に fallback を明示する時に使う。
        /// directory が fallback する先はこれだけなので、要求が別の表現を fallback に書いても、その表現の候補は出ない。
        /// </summary>
        internal string FallbackRepresentation => ContentEntryMatch.Full;

        /// <summary>
        /// route を組み立てる。検証済みでない登録には false と理由を返す（例外にしない）。
        /// </summary>
        /// <param name="session">登録した session。</param>
        /// <param name="assetManagement">その session を今登録している実物の AssetManagement。</param>
        /// <exception cref="ArgumentNullException">引数が null。</exception>
        /// <exception cref="InvalidOperationException">
        /// 検証済みの session を、その session を登録していない AssetManagement と組んだ、または session が既に受付を止めた。
        /// どちらも配線の誤りで、組み立てても照合が常に偽になる。
        /// </exception>
        internal static bool TryCreate(
            ContentDirectorySession session,
            AssetManagement.AssetManagement assetManagement,
            out RegisteredDirectoryRoute? route,
            out string notCreatedReason)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (assetManagement == null) throw new ArgumentNullException(nameof(assetManagement));

            var verified = session.VerifiedRevision;
            if (verified == null)
            {
                route = null;
                notCreatedReason = "the content directory was not registered from a verified installed revision";
                return false;
            }

            if (!assetManagement.IsInstalledContentDirectory(session))
                throw new InvalidOperationException("The content directory session is not the one installed in this AssetManagement.");
            if (!session.IsAccepting)
                throw new InvalidOperationException("The content directory session is no longer accepting loads.");

            var provider = new RegisteredDirectoryProvider(session);
            var overlayProvider = session.OverlayLayer == null
                ? null
                : new RegisteredDirectoryProvider(session, true, RegisteredDirectoryProvider.OverlayId);
            var envelope = new ContentCompatibilityEnvelope(
                verified.ContentSet,
                verified.Target,
                ContentManifestValidation.SupportedUnityVersion,
                ContentManifestValidation.SupportedRootSchemaVersion,
                revision: null);
            // 上書き → base の順。上書きの層が選ばれた要求は base の供給元が NotFound を返すので、順は採用の結果を変えず、probe の経緯を決める。
            var providers = overlayProvider == null
                ? new IContentProvider[] { provider }
                : new IContentProvider[] { overlayProvider, provider };
            var policy = new ResolutionPolicy(providers, new ContentCompatibilityRule(envelope));
            route = new RegisteredDirectoryRoute(provider, overlayProvider, envelope, policy, new ContentRequestLoader(assetManagement, policy));
            notCreatedReason = string.Empty;
            return true;
        }
    }
}
