#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetDescriptions;
using UnityEngine;

namespace OneStarMaker.Runtime.AssetManagement
{
    /// <summary>
    /// アセットとシーンのロード寿命をスコープ付きで一元管理する。
    /// </summary>
    public interface IAssetManagement
    {
        /// <summary>指定キーのアセットを非同期ロードし、owner の寿命に紐付ける。</summary>
        UniTask<IAssetHandle<T>> LoadAssetAsync<T>(AssetKey key, AssetOwner owner, CancellationToken ct = default)
            where T : Object;

        /// <summary>Bootstrap 用に App スコープで同期ロードする。</summary>
        IAssetHandle<T> LoadAppAssetSync<T>(AssetKey key) where T : Object;

        /// <summary>
        /// シーンを非同期ロードする。寿命キーとなる sceneIdentity は呼び出し側から明示的に渡す。
        /// （埋め込み SceneAssetDescription は SceneIdentity をシリアライズしないため desc には依存しない。）
        /// </summary>
        UniTask<ISceneHandle> LoadSceneAsync(
            string sceneIdentity,
            SceneAssetDescription desc,
            string variant = "",
            SceneLoadOptions options = default,
            CancellationToken ct = default);

        /// <summary>
        /// 指定シーン本体を backend（Addressables 等）経由でアンロードする。
        /// 通常 gameplay の SceneDirector 3フェーズ Phase 2 専用。
        /// 所有アセットの解放は続けて <see cref="ReleaseScene"/> が担当する。
        /// </summary>
        UniTask UnloadSceneAsync(string sceneIdentity, CancellationToken ct = default);

        /// <summary>Prefab を生成し、生成 GameObject の破棄時にインスタンス寿命を解放する。</summary>
        UniTask<GameObject> InstantiateAsync(
            AssetKey key,
            Transform? parent = null,
            bool worldSpace = false,
            CancellationToken ct = default);

        /// <summary>Manual owner で取得したハンドルを明示解放する。</summary>
        void Release(IAssetHandle handle);

        /// <summary>
        /// 指定シーン identity が所有するアセットだけを解放する（Phase 3 / 所有解放専用）。
        /// シーン本体の backend Unload は行わない。
        /// registry に「まだ Unload されていない Scene 本体」が残っている状態で呼ぶと
        /// <see cref="System.InvalidOperationException"/> を投げる
        /// （先に <see cref="UnloadSceneAsync"/> するか、teardown なら <see cref="ReleaseAll"/> を使うこと）。
        /// Scene 本体を載せずに <c>AssetOwner.Scene</c> だけで所有しているアセットは、従来どおり解放できる。
        /// </summary>
        void ReleaseScene(string sceneIdentity);

        /// <summary>
        /// プロセス／Play Mode 終了向けの同期 teardown。
        /// Unity 側が既に Scene を解体している前提のため、Addressables の Scene Unload は呼ばない。
        /// 未アンロード扱いの Scene を台帳上だけ MarkUnloaded し、全アセットを同期解放する。
        /// Application.quitting / SubsystemRegistration から Initializer.ReleaseAll 経由で呼ばれる。
        /// </summary>
        void ReleaseAll();
    }
}
