#nullable enable

using System;
using System.IO;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.BuildContent;

namespace OneStarMaker.Editor.ContentOverlay
{
    /// <summary>
    /// 上書きを起動に渡す config の key、上書きの directory の digest の算法、起動が出す log の marker。
    /// 正本は Runtime の起動の設定（internal）で、ここは host の Editor の入口が文字列と算法を複写せずに使うための公開し直しだけを持つ。
    /// </summary>
    /// <remarks>
    /// 渡し方（Editor の Play）: 検証済みの base の pair（<c>content:runtimeMode=directory</c>、<c>content:installedRevisionPath</c>、
    /// <c>content:manifestSha256</c> と、今ある directory の key）と一緒に、path と identity の 2 つの key をその Play の config に置く。
    /// digest の key は任意で、置けば起動が登録の直前に取り直して比べる。
    /// 渡し方（遊べる Player）: installed の pair（Player と同じ build の revision）と一緒に、path・identity・digest の 3 つの key を
    /// 置く（Player では digest が必須）。
    /// 起動は base を検証し直してから base → 上書きの順に登録し、上書きの記録と base を照合する。key の欠け、addressables、
    /// 検証済みでない base、動作確認の Player、digest の不一致のどれでも起動の失敗になり、base だけでは始まらない。
    /// </remarks>
    public static class ContentOverlayPlayConfig
    {
        /// <summary>上書きの directory の絶対 path。</summary>
        public const string OverlayPathKey = ContentOverlayStartup.OverlayPathKey;
        /// <summary>上書きの build の identity。</summary>
        public const string OverlayIdentityKey = ContentOverlayStartup.OverlayIdentityKey;
        /// <summary>上書きの directory の digest（<see cref="ComputeOverlayDigest"/> の値）。</summary>
        public const string OverlayDigestKey = ContentOverlayStartup.OverlayDigestKey;
        /// <summary>上書きつきの登録に成功した 1 行の先頭。続く <c>identity=</c> の値が登録した上書きの identity。</summary>
        public const string RegisteredLogMarker = ContentOverlayStartup.RegisteredLogMarker;
        /// <summary>遊べる Player が初回 Scene を load し終えた 1 行の先頭（Player の log）。</summary>
        public const string PlayerFirstSceneLogMarker = ContentOverlayStartup.PlayerFirstSceneLogMarker;

        /// <summary>
        /// 上書きの content directory の digest。Player が登録の直前に取り直す値と同じ規則で、directory の中の全 file の
        /// 相対 path と SHA-256 から作る（空の folder は数えない。reparse point があれば例外）。
        /// </summary>
        /// <param name="overlayDirectory">上書きの content directory の絶対 path。</param>
        public static string ComputeOverlayDigest(string overlayDirectory)
        {
            if (string.IsNullOrWhiteSpace(overlayDirectory) || !Path.IsPathRooted(overlayDirectory))
                throw new ArgumentException("The overlay directory must be an absolute path.", nameof(overlayDirectory));
            return ContentOverlayDigest.Compute(Path.GetFullPath(overlayDirectory));
        }
    }
}
