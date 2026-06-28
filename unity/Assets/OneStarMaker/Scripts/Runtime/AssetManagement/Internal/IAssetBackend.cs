#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace OneStarMaker.Runtime.AssetManagement.Internal
{
    /// <summary>
    /// Addressables 型を公開しないロード backend。
    /// </summary>
    internal interface IAssetBackend
    {
        /// <summary>address 文字列でアセットを非同期ロードする。</summary>
        UniTask<IBackendAsset> LoadAssetAsync<T>(string address, CancellationToken ct) where T : UnityEngine.Object;

        /// <summary>address 文字列でアセットを同期ロードする。</summary>
        IBackendAsset LoadAssetSync<T>(string address) where T : UnityEngine.Object;

        /// <summary>address 文字列でシーンを非同期ロードする。</summary>
        UniTask<IBackendScene> LoadSceneAsync(string address, SceneLoadOptions options, CancellationToken ct);

        /// <summary>ロード済みシーンをアンロードする。</summary>
        UniTask UnloadSceneAsync(IBackendScene scene, CancellationToken ct);

        /// <summary>address 文字列で Prefab を生成する。</summary>
        UniTask<IBackendInstance> InstantiateAsync(string address, Transform? parent, bool worldSpace, CancellationToken ct);

        /// <summary>ロード済みアセットまたはインスタンスを解放する。</summary>
        void Release(IBackendAsset asset);
    }

    /// <summary>backend 内部のアセット表現。</summary>
    internal interface IBackendAsset
    {
        UnityEngine.Object? Asset { get; }
        bool IsValid { get; }
    }

    /// <summary>backend 内部のシーン表現。</summary>
    internal interface IBackendScene
    {
        bool IsLoaded { get; }
        string Name { get; }
        GameObject[] GetRootGameObjects();
    }

    /// <summary>backend 内部の生成インスタンス表現。</summary>
    internal interface IBackendInstance
    {
        GameObject? Instance { get; }
    }
}
