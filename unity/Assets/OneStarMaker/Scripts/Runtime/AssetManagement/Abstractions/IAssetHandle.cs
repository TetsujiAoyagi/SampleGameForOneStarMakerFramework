#nullable enable

using UnityEngine;

namespace OneStarMaker.Runtime.AssetManagement
{
    /// <summary>
    /// AssetManagement が返すアセットハンドル。
    /// </summary>
    public interface IAssetHandle
    {
        /// <summary>AssetRegistry 上の正規化キー。</summary>
        string Key { get; }

        /// <summary>参照先がまだ有効か。</summary>
        bool IsValid { get; }
    }

    /// <summary>
    /// 型付きアセットハンドル。
    /// </summary>
    public interface IAssetHandle<out T> : IAssetHandle where T : Object
    {
        /// <summary>ロード済み Unity オブジェクト。解放済みまたは型不一致なら null。</summary>
        T? Value { get; }
    }
}
