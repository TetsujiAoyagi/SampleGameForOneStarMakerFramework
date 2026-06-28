#nullable enable

using UnityEngine;

namespace OneStarMaker.Runtime.AssetManagement.Components
{
    /// <summary>
    /// GameObject 破棄時に AssetManagement へ owner 解放を通知する。
    /// </summary>
    internal sealed class AssetReleaseOnDestroy : MonoBehaviour
    {
        private AssetManagement? _owner;

        internal void Initialize(AssetManagement owner) => _owner = owner;

        private void OnDestroy()
        {
            _owner?.NotifyGameObjectDestroyed(EntityId.ToULong(gameObject.GetEntityId()));
        }
    }
}
