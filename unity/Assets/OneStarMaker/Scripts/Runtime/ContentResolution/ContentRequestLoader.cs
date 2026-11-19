#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// resolver の決定を既存の <see cref="IAssetManagement"/> の content 入口へつなぐ。
    /// 所有・解放・同時ロードの調停は AssetManagement の既存契約をそのまま使い、新しい解放 API を足さない。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IAssetManagement"/> に member を足さない理由: 由来（どの directory から読んだか）を handle に持たせると
    /// AssetManagement の公開契約を変えることになり、この層を使わない単独利用を変えてしまう。また、Runtime の新しい
    /// API は directory の基数や Editor snapshot の機構が決まるまで凍結しない。そのため、探索の層は AssetManagement の
    /// 上の任意の層として置き、AssetManagement からは参照されない。
    /// </para>
    /// <para>
    /// 記録が実際に読んだ directory を指すことは、load の前に確かめる。AssetManagement は登録中の directory の identity を
    /// 公開しないので、採用した候補を返した供給元に照合（<see cref="IContentSourceBinding"/>）を尋ね、真の時だけ content 入口を呼ぶ。
    /// 照合を答えない供給元の候補と、照合が偽の候補は読まずに拒む。確かめずに読む option は持たない（読んだ物と記録が
    /// 食い違っても気づけなくなるため）。したがって成功した記録の SourceBindingVerified は常に true になる。
    /// </para>
    /// <para>
    /// 照合と content 入口の呼び出しの間に <c>await</c> を置かない。content 入口は最初の <c>await</c> より前にその時点の directory を
    /// 掴み、以後は掴んだ directory だけを使う。照合の後に <c>await</c> を挟むと、その間の directory の入れ替えで、照合した物と
    /// 違う directory から読みうる。照合は各 load method の中で、解決の <c>await</c> が終わった直後に同期で行う
    /// （<c>await</c> される別の async method の中に置くと、その method の戻りが <c>await</c> の境界になる）。
    /// </para>
    /// <para>
    /// この型は AssetManagement の public だけを使う。同じ assembly にある内部の型（directory の登録や台帳）を呼んで
    /// 既存の封鎖や所有の調停を迂回しない。
    /// </para>
    /// </remarks>
    public sealed class ContentRequestLoader
    {
        private readonly IAssetManagement _assetManagement;
        private readonly ResolutionPolicy _policy;
        private readonly ContentResolver _resolver = new ContentResolver();

        public ContentRequestLoader(IAssetManagement assetManagement, ResolutionPolicy policy)
        {
            _assetManagement = assetManagement ?? throw new ArgumentNullException(nameof(assetManagement));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        /// <summary>
        /// Scene を要求する。所有は既存の content Scene 入口と同じく scene 台帳（identity = logical key）で、
        /// 解放は <see cref="IAssetManagement.UnloadSceneAsync"/> → <see cref="IAssetManagement.ReleaseScene"/>。
        /// <paramref name="options"/> はそのまま渡す。activation を保留する要求を AssetManagement が拒む例外は包まない。
        /// </summary>
        /// <exception cref="ArgumentException">要求の種別が Scene でない（入口の取り違えは解決の失敗ではない）。</exception>
        /// <exception cref="ContentResolutionException">解決できなかった、または採用した候補をロード前に拒んだ。</exception>
        public async UniTask<SceneLoadOutcome> LoadSceneAsync(ContentRequest request, SceneLoadOptions options = default, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Kind != ContentKind.Scene)
                throw new ArgumentException("LoadSceneAsync accepts only Scene requests.", nameof(request));

            var result = await ResolveAsync(request, ct);
#if UNITY_EDITOR
            if (result.Candidate!.LoadPath == ContentLoadPath.AlreadyLoadedScene)
                return await LoadAlreadyLoadedSceneAsync(request, result, options, ct);
#endif
            RequireDirectoryPath(result);
            // ここから content 入口の呼び出しまで await を置かない（照合した directory から読むため）。
            var record = BindBeforeLoad(request, result, AssetOwner.Scene(request.LogicalKey).ToString());
            var handle = await _assetManagement.LoadContentSceneAsync(request.LogicalKey, record.Candidate.Representation, options, ct);
            return new SceneLoadOutcome(handle, record);
        }

        /// <summary>
        /// Object を要求し、<paramref name="owner"/> の寿命に紐付ける。解放は owner の既存の契約に従う。
        /// </summary>
        /// <exception cref="ArgumentException">要求の種別が Object でない。</exception>
        /// <exception cref="ContentResolutionException">解決できなかった、または採用した候補をロード前に拒んだ。</exception>
        public async UniTask<AssetLoadOutcome<T>> LoadAssetAsync<T>(ContentRequest request, AssetOwner owner, CancellationToken ct = default)
            where T : UnityEngine.Object
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Kind != ContentKind.Object)
                throw new ArgumentException("LoadAssetAsync accepts only Object requests.", nameof(request));

            var result = await ResolveAsync(request, ct);
            RequireDirectoryPath(result);
            // ここから content 入口の呼び出しまで await を置かない（照合した directory から読むため）。
            var record = BindBeforeLoad(request, result, owner.ToString());
            var handle = await _assetManagement.LoadContentAssetAsync<T>(request.LogicalKey, record.Candidate.Representation, owner, ct);
            return new AssetLoadOutcome<T>(handle, record);
        }

        /// <summary>解決する。経路の確認と照合はここに置かない（呼び手が await の後に同期で行う）。</summary>
        private async UniTask<ContentResolutionResult> ResolveAsync(ContentRequest request, CancellationToken ct)
        {
            var result = await _resolver.ResolveAsync(request, _policy, ct);
            if (!result.IsResolved)
                throw new ContentResolutionException(result, ContentLoadRejection.None);
            return result;
        }

        /// <summary>
        /// 採用した候補が content 入口で読む経路かを確かめる。同期で終わる。由来ではなく経路で弾く。snapshot 由来でも一時 directory 経由の候補は読めるが、
        /// 既ロード Scene や Addressables の候補を directory から読むと記録の由来が虚偽になる。
        /// </summary>
        private static void RequireDirectoryPath(ContentResolutionResult result)
        {
            if (result.Candidate!.LoadPath != ContentLoadPath.ContentDirectory)
                throw new ContentResolutionException(result, ContentLoadRejection.LoadPathUnsupported);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 既ロードの経路の候補を、候補を返した供給元の内部の口（<see cref="IContentSceneLoadBinding"/>）で読む。
        /// 口を持たない供給元の候補は今までどおり <see cref="ContentLoadRejection.LoadPathUnsupported"/>、供給元が「自分の候補でない、
        /// またはこの AssetManagement に預け元を据えていない」と答えれば <see cref="ContentLoadRejection.SourceBindingMismatch"/> で、どちらも読まない。
        /// </summary>
        /// <remarks>
        /// 供給元の load の失敗（複製が採れない、照合が合わない）は包まずに通す。content 入口の load の失敗と同じ扱いで、例外の文が
        /// Scene の identity と理由を言う（<see cref="ContentResolutionException"/> の文は固定で、それを言えない）。directory の content 入口で読み直さない。
        /// 記録は load の前に作り、load が照合を通って成功した時だけ返す（返した record の SourceBindingVerified は常に true）。
        /// </remarks>
        private async UniTask<SceneLoadOutcome> LoadAlreadyLoadedSceneAsync(
            ContentRequest request, ContentResolutionResult result, SceneLoadOptions options, CancellationToken ct)
        {
            var candidate = result.Candidate!;
            if (!(FindProvider(candidate.ProviderId) is IContentSceneLoadBinding binding))
                throw new ContentResolutionException(result, ContentLoadRejection.LoadPathUnsupported);
            if (!binding.IsLoadedBy(candidate, _assetManagement))
                throw new ContentResolutionException(result, ContentLoadRejection.SourceBindingMismatch);

            var record = new ContentLoadRecord(request, candidate, result.Probes, AssetOwner.Scene(request.LogicalKey).ToString(),
                ContentTraceability.Traceable, sourceBindingVerified: true);
            var handle = await binding.LoadSceneAsync(candidate, _assetManagement, options, ct);
            return new SceneLoadOutcome(handle, record);
        }

        /// <summary>
        /// policy に、<paramref name="sceneIdentity"/> を宣言する既ロードの口の供給元があるか。SceneDirector は真の identity に限り、
        /// load 済みの同名の Scene を名前で再利用する前にこの loader で読む（宣言に無い identity の順は変えない）。
        /// </summary>
        internal bool DeclaresAlreadyLoadedScene(string sceneIdentity)
        {
            if (string.IsNullOrEmpty(sceneIdentity))
                return false;
            foreach (var provider in _policy.Providers)
            {
                if (provider is IContentSceneLoadBinding binding && binding.Declares(sceneIdentity))
                    return true;
            }

            return false;
        }
#endif

        /// <summary>
        /// 採用した候補の供給元に、候補が今の AssetManagement が読む directory の物かを尋ね、真なら記録を作る。同期で終わる。
        /// </summary>
        private ContentLoadRecord BindBeforeLoad(ContentRequest request, ContentResolutionResult result, string ownerLabel)
        {
            var candidate = result.Candidate!;
            if (!(FindProvider(candidate.ProviderId) is IContentSourceBinding binding))
                throw new ContentResolutionException(result, ContentLoadRejection.SourceBindingUnavailable);

            if (!binding.IsLoadedBy(candidate, _assetManagement))
                throw new ContentResolutionException(result, ContentLoadRejection.SourceBindingMismatch);

            // 記録はロードの前に作る。ロード後に作って失敗すると、取得した handle を呼び出し側へ渡せず解放できなくなる。
            return new ContentLoadRecord(request, candidate, result.Probes, ownerLabel, ContentTraceability.Traceable, sourceBindingVerified: true);
        }

        /// <summary>
        /// 候補を返した供給元を policy から引く。resolver は候補の provider id と probe した供給元の id の一致を確かめてから採るので、
        /// id で引けば候補を返した供給元に当たる（policy の id は ordinal で一意）。
        /// </summary>
        private IContentProvider? FindProvider(string providerId)
        {
            foreach (var provider in _policy.Providers)
            {
                if (string.Equals(provider.Id, providerId, StringComparison.Ordinal))
                    return provider;
            }

            return null;
        }
    }
}
