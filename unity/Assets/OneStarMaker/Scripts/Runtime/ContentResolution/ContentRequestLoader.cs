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
    /// ロード元の同一性は未検証である。AssetManagement は登録済みの directory の identity を公開しないので、
    /// ここで読んだ directory が採用した候補と同じものかを確かめられない。記録の SourceBindingVerified は常に false で、
    /// この接続と検証は provider route・install・Unity load をつなぐ後続の統合作業が所有する。
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

            var (candidate, record) = await ResolveForLoadAsync(request, AssetOwner.Scene(request.LogicalKey).ToString(), ct);
            var handle = await _assetManagement.LoadContentSceneAsync(request.LogicalKey, candidate.Representation, options, ct);
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

            var (candidate, record) = await ResolveForLoadAsync(request, owner.ToString(), ct);
            var handle = await _assetManagement.LoadContentAssetAsync<T>(request.LogicalKey, candidate.Representation, owner, ct);
            return new AssetLoadOutcome<T>(handle, record);
        }

        private async UniTask<(ContentCandidate Candidate, ContentLoadRecord Record)> ResolveForLoadAsync(
            ContentRequest request,
            string ownerLabel,
            CancellationToken ct)
        {
            var result = await _resolver.ResolveAsync(request, _policy, ct);
            if (!result.IsResolved)
                throw new ContentResolutionException(result, ContentLoadRejection.None);

            var candidate = result.Candidate!;
            // 由来ではなく経路で弾く。snapshot 由来でも一時 directory 経由の候補は読めるが、
            // 既ロード Scene や Addressables の候補を directory から読むと記録の由来が虚偽になる。
            if (candidate.LoadPath != ContentLoadPath.ContentDirectory)
                throw new ContentResolutionException(result, ContentLoadRejection.LoadPathUnsupported);

            // 記録はロードの前に作る。ロード後に作って失敗すると、取得した handle を呼び出し側へ渡せず解放できなくなる。
            var record = new ContentLoadRecord(request, candidate, result.Probes, ownerLabel, ContentTraceability.Traceable, sourceBindingVerified: false);
            return (candidate, record);
        }
    }
}
