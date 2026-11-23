#nullable enable

using OneStarMaker.Runtime;

namespace OneStarMaker.Editor.ContentOverlay
{
    /// <summary>
    /// Editor の Play で上書きを起動に渡す config の key と、起動が上書きつきの登録に成功した時の log の marker。
    /// 正本は Runtime の起動の設定（internal）で、ここは host の Editor の入口が文字列を複写せずに使うための公開し直しだけを持つ。
    /// </summary>
    /// <remarks>
    /// 渡し方: 検証済みの base の pair（<c>content:runtimeMode=directory</c>、<c>content:installedRevisionPath</c>、
    /// <c>content:manifestSha256</c> と、今ある directory の key）と一緒に、2 つの key をその Play の config に置く。
    /// 起動は base を検証し直してから base → 上書きの順に登録し、上書きの記録と base を照合する。片方だけ、addressables、
    /// 検証済みでない base、Player の構成のどれでも起動の失敗になり、base だけでは始まらない。
    /// </remarks>
    public static class ContentOverlayPlayConfig
    {
        /// <summary>上書きの directory の絶対 path。</summary>
        public const string OverlayPathKey = ContentOverlayStartup.OverlayPathKey;
        /// <summary>上書きの build の identity。</summary>
        public const string OverlayIdentityKey = ContentOverlayStartup.OverlayIdentityKey;
        /// <summary>上書きつきの登録に成功した 1 行の先頭。続く <c>identity=</c> の値が登録した上書きの identity。</summary>
        public const string RegisteredLogMarker = ContentOverlayStartup.RegisteredLogMarker;
    }
}
