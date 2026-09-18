#nullable enable

using System.Threading;
using System;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.BuildContent;
using UnityEngine;

namespace OneStarMaker.Runtime.AssetManagement.Internal
{
    internal interface IContentDirectoryToken { }

    /// <summary>Addressables の文字列 port と分離した、revision 固定の Content Directory port。</summary>
    internal interface IContentDirectoryBackend
    {
        string BuildIdentity { get; }
        string Target { get; }
        string CachePrefix { get; }
        AssetKey GetObjectKey(string logicalKey, string representation);
        UniTask<IBackendAsset> LoadAssetAsync<T>(string logicalKey, string representation, CancellationToken ct)
            where T : UnityEngine.Object;
        UniTask<IBackendScene> LoadSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options, CancellationToken ct);
        UniTask<IBackendInstance> InstantiateAsync(string logicalKey, string representation, Transform? parent, bool worldSpace, CancellationToken ct);
        UniTask UnloadSceneAsync(IBackendScene scene);
        void ReleaseSceneAfterUnityShutdown(IBackendScene scene);
        void Release(IBackendAsset asset);
        void ConfigureCacheEviction(Action evictRevisionEntries);
        UniTask StopAndDrainAsync();
        UniTask CloseAsync();
        void BeginSynchronousShutdown();
    }
}
