#nullable enable

using UnityEngine;

namespace OneStarMaker.Runtime.AssetManagement
{
    /// <summary>
    /// AssetManagement が返すシーンハンドル。
    /// </summary>
    public interface ISceneHandle
    {
        /// <summary>Framework 上のシーン識別子。</summary>
        string Identity { get; }

        /// <summary>シーンがロード済みか。</summary>
        bool IsLoaded { get; }

        /// <summary>Unity シーン名。</summary>
        string Name { get; }

        /// <summary>シーン直下の GameObject を取得する。</summary>
        GameObject[] GetRootGameObjects();
    }
}
