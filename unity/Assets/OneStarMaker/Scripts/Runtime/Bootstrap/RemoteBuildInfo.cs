#nullable enable

namespace OneStarMaker.Runtime
{
    /// <summary>リモート build-info.json のデシリアライズ用。</summary>
    [System.Serializable]
    public sealed class RemoteBuildInfo
    {
        /// <summary>ビルド元 Git リビジョン。</summary>
        public string revision = string.Empty;

        /// <summary>ビルド日時(UTC ISO8601)。</summary>
        public string builtAtUtc = string.Empty;
    }
}
