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

        /// <summary>指定シーン本体をアンロードする。所有アセットの解放は ReleaseScene が担当する。</summary>
        UniTask UnloadSceneAsync(string sceneIdentity, CancellationToken ct = default);

        /// <summary>Prefab を生成し、生成 GameObject の破棄時にインスタンス寿命を解放する。</summary>
        UniTask<GameObject> InstantiateAsync(
            AssetKey key,
            Transform? parent = null,
            bool worldSpace = false,
            CancellationToken ct = default);

        /// <summary>Manual owner で取得したハンドルを明示解放する。</summary>
        void Release(IAssetHandle handle);

        /// <summary>指定シーン所有のアセットとシーンハンドルを解放する。</summary>
        void ReleaseScene(string sceneIdentity);

        /// <summary>アプリ終了時に全ロード済みリソースを解放する。</summary>
        void ReleaseAll();
    }
}
