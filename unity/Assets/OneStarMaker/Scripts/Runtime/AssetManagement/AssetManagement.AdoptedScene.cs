#nullable enable

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.BuildContent;
using UnityEngine;

namespace OneStarMaker.Runtime.AssetManagement
{
    /// <summary>
    /// Scene の台帳の 3 つ目の種別: AssetManagement の外（預け元）で読んだ Scene を預かる。
    /// 預かった Scene は directory の Scene と同じ API（<see cref="UnloadSceneAsync"/> → <see cref="ReleaseScene"/>、<see cref="ReleaseAll"/>）で離す。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 預け元を据えると、その identity（予約）は directory の content 入口から読めなくなる（<see cref="LoadContentSceneAsync"/> が
    /// 予約の identity を、台帳に預かった Scene があるかどうかに関係なく拒む）。予約が無いと、預け元の Scene が load 済みなのに同じ identity の
    /// directory の版が 2 つ目として載り、その後の unload が他の所有者の Scene を離しうる。
    /// </para>
    /// <para>
    /// 預かる口は <see cref="LoadContentSceneAsync"/> と同じ gate（<see cref="_contentSceneLoadGate"/>）の中で台帳の検査・取得・登録を行う。
    /// 別の gate にすると、2 つの load が同時に台帳を見て、<c>AssetRegistry.AddScene</c> の上書きで片方が台帳から漏れる。
    /// </para>
    /// <para>
    /// 預かった Scene は directory の物ではない。<see cref="CloseContentDirectoryAsync"/> は token の Scene だけを unload し、預かった Scene は
    /// close の後も台帳に残る（離すのは <see cref="UnloadSceneAsync"/> か <see cref="ReleaseAll"/>）。預けるのは directory が受付中の時だけで、
    /// close の最中と後は預からない（Play の配線は directory の登録と同じ寿命で預け元を据える）。
    /// </para>
    /// <para>
    /// Editor の build だけ。連携部分（要求時解決の供給元）だけが呼ぶ internal の口で、<see cref="IAssetManagement"/> には出さない。
    /// </para>
    /// </remarks>
    public sealed partial class AssetManagement
    {
        private IAdoptedSceneSource? _adoptedSceneSource;
        private HashSet<string>? _reservedSceneIdentities;

        /// <summary>
        /// 預け元を据え、その identity を予約する。1 つの AssetManagement に 1 つだけ。<see cref="ReleaseAll"/> で外れる。
        /// </summary>
        /// <exception cref="InvalidOperationException">既に据えてある、または予約する identity の Scene が既に台帳にある。</exception>
        internal void InstallAdoptedSceneSource(IAdoptedSceneSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (_adoptedSceneSource != null)
                throw new InvalidOperationException("An adopted scene source is already installed.");

            var reserved = new HashSet<string>(source.ReservedIdentities, StringComparer.Ordinal);
            // 据える前に台帳にある同じ identity は、預け元の Scene ではない（directory か Addressables の Scene）。
            // 予約した identity の台帳の entry が預かった Scene だけになることを、後始末（SceneDirector の台帳経由の unload）が前提にする。
            foreach (var scene in _registry.GetScenes())
            {
                if (!scene.IsUnloaded && reserved.Contains(scene.Identity))
                    throw new InvalidOperationException(
                        "Cannot reserve scene '" + scene.Identity + "' for adoption: a scene with the same identity is already loaded.");
            }

            _adoptedSceneSource = source;
            _reservedSceneIdentities = reserved;
        }

        /// <summary><paramref name="source"/> が今据えてある預け元そのもの（参照の同一）か。供給元が自分の預け元を渡して尋ねる。</summary>
        internal bool IsAdoptedSceneSource(IAdoptedSceneSource source)
            => _adoptedSceneSource != null && ReferenceEquals(_adoptedSceneSource, source);

        /// <summary>
        /// 預け元から <paramref name="sceneIdentity"/> の Scene を取得して台帳に預かり、handle を返す。既に預かっていれば取得せずに同じ Scene を返す。
        /// </summary>
        /// <remarks>
        /// 取消は gate を待つ間と取得の後に見る。取消済みの token では預け元を呼ばない。取得の途中で取消された時は、取得の完了を待ち、
        /// 得た Scene を預け元で unload してから取消を返す（台帳に入らず、load 済みのまま残る Scene を作らない）。
        /// </remarks>
        /// <exception cref="ContentDirectoryException">
        /// directory が登録されていない・受付中でない、activation の保留、予約していない identity、または同じ identity の預け元以外の Scene が台帳にある。
        /// </exception>
        /// <exception cref="InvalidOperationException">預け元が据えられていない。</exception>
        internal async UniTask<ISceneHandle> AdoptSceneAsync(string sceneIdentity, SceneLoadOptions options = default, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sceneIdentity))
                throw new ArgumentException("Scene identity is required.", nameof(sceneIdentity));
            var directory = RequireContentDirectory();
            var source = _adoptedSceneSource
                ?? throw new InvalidOperationException("No adopted scene source is installed.");
            // 預け元の load は activation を保留しない（directory の Scene と同じく、完了まで所有する）。
            if (!options.ActivateOnLoad)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,
                    "Deferred scene activation is not supported by adopted scene loads.", directory.BuildIdentity, directory.Target, sceneIdentity, string.Empty);
            if (!_reservedSceneIdentities!.Contains(sceneIdentity))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.EntryAmbiguous,
                    "Scene identity is not reserved by the adopted scene source.", directory.BuildIdentity, directory.Target, sceneIdentity, string.Empty);

            await _contentSceneLoadGate.WaitAsync(ct);
            try
            {
                directory.EnsureAccepting();
                if (_registry.TryGetScene(sceneIdentity, out var existing) && !existing.IsUnloaded)
                {
                    // 同じ預け元から預かった Scene だけを返す。予約の後に directory や Addressables の Scene がこの identity で台帳に入る道は無いが、
                    // 入っていたら預け元の Scene と偽って返さない。
                    if (existing.Backend is AdoptedScene adopted && ReferenceEquals(adopted.Source, source))
                        return new SceneHandle(sceneIdentity, existing.Backend);
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.EntryAmbiguous,
                        "Scene identity is already loaded from another source.", directory.BuildIdentity, directory.Target, sceneIdentity, string.Empty);
                }

                ct.ThrowIfCancellationRequested();
                var scene = await source.AcquireAsync(sceneIdentity);
                if (ct.IsCancellationRequested)
                {
                    await source.UnloadAsync(scene);
                    ct.ThrowIfCancellationRequested();
                }

                var entry = new AdoptedScene(scene, source);
                _registry.AddScene(sceneIdentity, entry);
                return new SceneHandle(sceneIdentity, entry);
            }
            finally { _contentSceneLoadGate.Release(); }
        }

        /// <summary>
        /// 予約した identity を directory の content 入口から読ませない。<see cref="LoadContentSceneAsync"/> が gate の中、台帳の検査より前に呼ぶ
        /// （台帳に預かった Scene が無い時、つまり未要求の複製が load 済みのまま lifecycle の外に居る時も拒む）。
        /// </summary>
        private void RejectReservedContentScene(IContentDirectoryBackend directory, string sceneIdentity, string representation)
        {
            if (_reservedSceneIdentities != null && _reservedSceneIdentities.Contains(sceneIdentity))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.EntryAmbiguous,
                    "Scene identity is reserved for an adopted scene; it is not loaded from the content directory.",
                    directory.BuildIdentity, directory.Target, sceneIdentity, representation);
        }

        /// <summary>
        /// 預かった Scene を預け元の unload で離す。既に load 済みでない（Unity が先に解体した、無効になった）Scene は unload 済みとして扱う。
        /// 失敗の例外はそのまま出し、呼び手（<see cref="UnloadSceneAsync"/>）は台帳の印を倒さない。
        /// </summary>
        private static UniTask UnloadAdoptedSceneAsync(AdoptedScene scene)
            => scene.Inner.IsLoaded ? scene.Source.UnloadAsync(scene.Inner) : UniTask.CompletedTask;

        /// <summary>Play 停止・起動の失敗の後始末。預け元を外し、予約を解く。Scene の unload はしない（Unity が解体する）。</summary>
        private void DetachAdoptedSceneSource()
        {
            _adoptedSceneSource = null;
            _reservedSceneIdentities = null;
        }

        /// <summary>台帳の entry。預け元が返した Scene と、離す時に使う預け元を持つ。</summary>
        private sealed class AdoptedScene : IBackendScene
        {
            public AdoptedScene(IBackendScene inner, IAdoptedSceneSource source)
            {
                Inner = inner ?? throw new InvalidOperationException("The adopted scene source returned no scene.");
                Source = source;
            }

            public IBackendScene Inner { get; }

            public IAdoptedSceneSource Source { get; }

            public bool IsLoaded => Inner.IsLoaded;

            public string Name => Inner.Name;

            public GameObject[] GetRootGameObjects() => Inner.GetRootGameObjects();
        }
    }
}
#endif
