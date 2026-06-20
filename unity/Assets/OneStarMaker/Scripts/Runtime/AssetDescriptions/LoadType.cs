#nullable enable

namespace OneStarMaker.Runtime.AssetDescriptions
{
    /// <summary>
    /// シーンのロードタイミングを定義する。
    /// </summary>
    public enum LoadType
    {
        /// <summary>明示的な AddScene 呼び出し時のみロードする。</summary>
        OnDemand,

        /// <summary>親シーンロード時に同期的（await）にロードする。</summary>
        NecessaryAlways,

        /// <summary>親シーンロード時に非同期（Forget）でバックグラウンドロードする。</summary>
        IncrementalAlways,
    }
}
