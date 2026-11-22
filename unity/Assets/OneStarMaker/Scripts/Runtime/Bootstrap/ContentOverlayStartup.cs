#nullable enable

using System;

namespace OneStarMaker.Runtime
{
    /// <summary>起動の config から決めた上書きの扱い。</summary>
    internal enum ContentOverlayStartupKind
    {
        /// <summary>上書きの key がどちらも無い。登録の呼び方は今までと同じ。</summary>
        None,
        /// <summary>検証済みの base に上書きを 1 つ重ねて登録する。</summary>
        Overlay,
        /// <summary>上書きの key があるのに重ねられない設定。起動の失敗にする（key を無視して base だけで始めない）。</summary>
        Rejected,
    }

    /// <summary><see cref="ContentOverlayStartup.Decide"/> の結果。値だけを持つ。</summary>
    internal sealed class ContentOverlayStartupDecision
    {
        internal static readonly ContentOverlayStartupDecision None = new(ContentOverlayStartupKind.None, string.Empty, string.Empty, string.Empty);

        private ContentOverlayStartupDecision(ContentOverlayStartupKind kind, string path, string identity, string reason)
        { Kind = kind; Path = path; Identity = identity; Reason = reason; }

        internal static ContentOverlayStartupDecision Overlay(string path, string identity) => new(ContentOverlayStartupKind.Overlay, path, identity, string.Empty);
        internal static ContentOverlayStartupDecision Rejected(string reason) => new(ContentOverlayStartupKind.Rejected, string.Empty, string.Empty, reason);

        internal ContentOverlayStartupKind Kind { get; }
        /// <summary>上書きの directory の path（<see cref="ContentOverlayStartupKind.Overlay"/> の時だけ）。</summary>
        internal string Path { get; }
        /// <summary>上書きの build の identity（同上）。</summary>
        internal string Identity { get; }
        /// <summary>起動を失敗にする理由（<see cref="ContentOverlayStartupKind.Rejected"/> の時だけ）。</summary>
        internal string Reason { get; }
    }

    /// <summary>
    /// 起動の config に置かれた上書きの key 2 つから、上書きを重ねて登録するかを決める。状態を持たず、Unity を呼ばない。
    /// <para>
    /// 上書きは Editor の Play で、検証済みの base（installed の pair）にだけ重ねる（登録契約。base 1 つと上書き 0 か 1 つ）。
    /// key が 1 つでもあって重ねられない設定は、どれも起動の失敗にする。key を無視して base だけで始めると、利用者は編集が反映された
    /// Play だと思ったまま base の版を見る。key は <c>content:</c> の名前空間の外に置き、Player の protected config の照合には入れない
    /// （Player の構成では key があること自体を失敗にする）。
    /// </para>
    /// <para>
    /// key の名前と登録の log の marker は、ここが正本である。Framework の Editor が public の定数で公開し直し、host の入口と別の host は
    /// その定数だけを使う（文字列を複写しない）。
    /// </para>
    /// </summary>
    internal static class ContentOverlayStartup
    {
        internal const string OverlayPathKey = "editor:contentOverlayPath";
        internal const string OverlayIdentityKey = "editor:contentOverlayIdentity";
        /// <summary>上書きつきの登録に成功した時の 1 行の先頭。続けて <c>identity=</c>・<c>baseRevision=</c>・<c>path=</c>（path は最後）を出す。</summary>
        internal const string RegisteredLogMarker = "[AppInit] Content overlay registered: ";

        /// <param name="hasPathKey">config に <see cref="OverlayPathKey"/> がある（空文字でも「ある」）。</param>
        /// <param name="path">その値。</param>
        /// <param name="hasIdentityKey">config に <see cref="OverlayIdentityKey"/> がある。</param>
        /// <param name="identity">その値。</param>
        /// <param name="isEditorPlay">Editor の Play Mode の起動か。</param>
        /// <param name="isPlayerConfiguration">Player の required file の構成か。</param>
        /// <param name="modeSpecified">config に <c>content:runtimeMode</c> がある。</param>
        /// <param name="mode">その値（無ければ空）。</param>
        /// <param name="hasInstalledPair">installed の pair（revision の path と manifest の SHA-256）が両方ある。Editor ではこれが検証済みの base。</param>
        internal static ContentOverlayStartupDecision Decide(bool hasPathKey, string path, bool hasIdentityKey, string identity,
            bool isEditorPlay, bool isPlayerConfiguration, bool modeSpecified, string mode, bool hasInstalledPair)
        {
            if (!hasPathKey && !hasIdentityKey) return ContentOverlayStartupDecision.None;
            if (!hasPathKey || !hasIdentityKey || string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(identity))
                return ContentOverlayStartupDecision.Rejected(
                    "Content overlay needs both " + OverlayPathKey + " and " + OverlayIdentityKey + " with non-empty values.");
            if (isPlayerConfiguration || !isEditorPlay)
                return ContentOverlayStartupDecision.Rejected("Content overlay is only available in Editor Play Mode.");
            if (modeSpecified && string.Equals(mode, "addressables", StringComparison.Ordinal))
                return ContentOverlayStartupDecision.Rejected("Content overlay cannot be used with content:runtimeMode=addressables.");
            if (modeSpecified && !string.Equals(mode, "directory", StringComparison.Ordinal))
                return ContentOverlayStartupDecision.Rejected("Content overlay needs content:runtimeMode=directory.");
            // mode が無く pair も無い設定は、今の起動も失敗にする。key があるので、ここでも理由を上書きの側で示す。
            if (!hasInstalledPair)
                return ContentOverlayStartupDecision.Rejected(
                    "Content overlay can only be layered on a verified installed revision (content:installedRevisionPath and content:manifestSha256).");
            return ContentOverlayStartupDecision.Overlay(path, identity);
        }

        /// <summary>上書きつきの登録に成功した時の 1 行。record はこの行の identity を入口の計画と突き合わせる。</summary>
        internal static string FormatRegistered(string identity, string baseRevision, string path) =>
            RegisteredLogMarker + "identity=" + identity + " baseRevision=" + baseRevision + " path=" + path;
    }
}
