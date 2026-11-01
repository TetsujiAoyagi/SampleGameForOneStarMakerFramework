#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace OneStarMaker.Runtime.AssetManagement.Internal
{
    /// <summary>
    /// 取得系を拒否し、解放と unload だけを内側へ通す <see cref="IAssetBackend"/> の decorator。
    /// Content Directory mode の起動は、mode を決めるために config を Addressables 経由で読まなければならないので
    /// backend 無しでは作れない。代わりに config を読んだ直後に <see cref="AssetManagement.SealAddressables"/> が
    /// backend をこれに差し替え、以後は入口の数え方によらず backend 呼び出しの全てが失敗する。
    /// 封じる前に取得した handle と Scene は内側の backend で解放・unload できる。
    /// </summary>
    internal sealed class SealedAssetBackend : IAssetBackend
    {
        private readonly IAssetBackend _inner;

        public SealedAssetBackend(IAssetBackend inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>封じる前の backend。解放と unload はここへ届く。</summary>
        internal IAssetBackend Inner => _inner;

        public UniTask<IBackendAsset> LoadAssetAsync<T>(string address, CancellationToken ct) where T : UnityEngine.Object
            => throw Sealed(address);

        public IBackendAsset LoadAssetSync<T>(string address) where T : UnityEngine.Object
            => throw Sealed(address);

        public UniTask<IBackendScene> LoadSceneAsync(string address, SceneLoadOptions options, CancellationToken ct)
            => throw Sealed(address);

        public UniTask<IBackendInstance> InstantiateAsync(string address, Transform? parent, bool worldSpace, CancellationToken ct)
            => throw Sealed(address);

        public UniTask UnloadSceneAsync(IBackendScene scene, CancellationToken ct) => _inner.UnloadSceneAsync(scene, ct);

        public void Release(IBackendAsset asset) => _inner.Release(asset);

        private static InvalidOperationException Sealed(string address) => new(
            "Addressables backend is sealed: the Content Directory mode does not load from Addressables after the configuration was read. address=" + address);
    }
}
