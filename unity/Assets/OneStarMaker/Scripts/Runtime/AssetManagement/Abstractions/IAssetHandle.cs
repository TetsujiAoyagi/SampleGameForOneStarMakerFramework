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

    /// <summary>
    /// 「この handle はどの owner 分の参照か」を Release へ伝える内部契約。
    /// これが無いと handle 経由の解放が owner を特定できず、
    /// Owners（誰が持っているか）と RefCount（何本持たれているか）が乖離する。
    /// 公開 API を増やさないため internal に置く。
    /// </summary>
    internal interface IOwnedAssetHandle
    {
        /// <summary>この handle を発行した Load 呼び出しの owner。</summary>
        AssetOwner Owner { get; }
    }
}
