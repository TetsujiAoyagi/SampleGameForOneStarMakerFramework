#nullable enable

using System;

namespace OneStarMaker.Runtime.AssetDescriptions
{
    /// <summary>
    /// <see cref="AssetPayload"/> への後方互換 alias。
    /// シリアライズ互換は <see cref="AssetPayload.Reference"/> の FormerlySerializedAs が担うため、
    /// 新規コードからは本型を使わない。
    /// </summary>
    [Serializable]
    [Obsolete("Use AssetPayload instead.")]
    public class ScenePayload : AssetPayload
    {
    }
}
