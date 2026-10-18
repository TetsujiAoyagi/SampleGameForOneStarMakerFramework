#nullable enable

using System;
using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Vfx
{
    /// <summary>
    /// <c>AssetOwner.Bind(go)</c> の go の破棄を、1つの VfxSystem へ知らせる。
    /// <para>
    /// 既存の <c>AssetReleaseOnDestroy</c> は具象 AssetManagement 専用で、多重化すると AssetManagement の変更理由が
    /// 増えるため VFX 専用に分けた。VfxSystem ごとに1つ付く（同じ go を二つの system が所有したら二つ付く）。
    /// </para>
    /// <para>
    /// 一度も active にならなかった go や batchmode / EditMode では <c>OnDestroy</c> が呼ばれないので、
    /// VfxSystem は Tick の偽 null 検査でも回収する。この通知は届けば同期に回収できる経路である。
    /// go が残る間は callback が VfxSystem を参照し続ける（VfxSystem の Dispose 後の通知は VfxSystem 側で無視する）。
    /// </para>
    /// </summary>
    internal sealed class VfxOwnerDestroyNotifier : MonoBehaviour
    {
        private Action<ulong>? _onDestroyed;
        private ulong _gameObjectId;

        /// <summary>この notifier の通知先を識別する値。同じ system の notifier を二重に付けないために使う。</summary>
        internal object? SystemKey { get; private set; }

        /// <summary>
        /// id は呼び出し側が go の生存中に取ったものを保持する。<c>OnDestroy</c> の中では gameObject を参照しない。
        /// </summary>
        internal void Initialize(Action<ulong> onDestroyed, ulong gameObjectId, object systemKey)
        {
            _onDestroyed = onDestroyed;
            _gameObjectId = gameObjectId;
            SystemKey = systemKey;
        }

        private void OnDestroy()
        {
            var callback = _onDestroyed;
            _onDestroyed = null;
            callback?.Invoke(_gameObjectId);
        }
    }
}
