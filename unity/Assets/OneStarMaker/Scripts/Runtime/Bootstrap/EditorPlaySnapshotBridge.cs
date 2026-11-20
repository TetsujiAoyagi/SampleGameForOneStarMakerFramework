#nullable enable

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneStarMaker.Runtime
{
    /// <summary>
    /// Editor の Play 入口が Play の前に固定した Scene の複製（snapshot）の 1 件。起動の取り込みの段と、要求時解決の供給元の配線が読む形だけを持つ。
    /// Scene 名（<see cref="Identity"/>）は複製の file 名と同じで、Unity の Scene の name として見える。
    /// </summary>
    internal sealed class EditorPlaySnapshotScene
    {
        /// <summary>
        /// 照合だけを見る test 用の短い形。起動時に要求しない（<see cref="RequestAtStartup"/> は偽）、record の事実は空。
        /// Editor の session は下の形で全ての欄を渡す。
        /// </summary>
        internal EditorPlaySnapshotScene(string identity, string copyPath, bool isActive)
            : this(identity, copyPath, isActive, requestAtStartup: false, sourcePath: string.Empty, wasDirty: false, sha256: string.Empty)
        {
        }

        internal EditorPlaySnapshotScene(string identity, string copyPath, bool isActive, bool requestAtStartup, string sourcePath, bool wasDirty, string sha256)
        {
            if (string.IsNullOrEmpty(identity)) throw new ArgumentException("Identity is required.", nameof(identity));
            if (string.IsNullOrEmpty(copyPath)) throw new ArgumentException("Copy path is required.", nameof(copyPath));
            Identity = identity;
            CopyPath = copyPath;
            IsActive = isActive;
            RequestAtStartup = requestAtStartup;
            SourcePath = sourcePath ?? string.Empty;
            WasDirty = wasDirty;
            Sha256 = sha256 ?? string.Empty;
        }

        internal string Identity { get; }
        /// <summary><c>Assets/</c> から始まる複製の asset path。</summary>
        internal string CopyPath { get; }
        /// <summary>Play の前に active だった Scene の複製なら真（宣言の中で 1 つだけ）。</summary>
        internal bool IsActive { get; }

        /// <summary>
        /// 起動時に OSM の lifecycle へ要求する Scene なら真。決めるのは host の Play の入口の規則で、Framework は既定を持たない
        /// （偽の複製は物理的に load されるが、host の code が後から要求するまで lifecycle に入らない）。
        /// </summary>
        internal bool RequestAtStartup { get; }

        /// <summary>複製の元の Scene の path（record の事実）。</summary>
        internal string SourcePath { get; }

        /// <summary>複製を作った時に元の Scene が未保存の変更を持っていたか（record の事実）。</summary>
        internal bool WasDirty { get; }

        /// <summary>複製の bytes の SHA-256（record の事実。Play の間は再検証しない）。</summary>
        internal string Sha256 { get; }
    }

    /// <summary>
    /// Editor（<c>OneStarMaker.Editor</c> の Play snapshot の session）が、1 回の Play の宣言と Scene の load の手段を置く口。
    /// Runtime は Editor の型を参照しないので、load は delegate で受ける。
    /// <para>
    /// 寿命は 1 回の Play。置くのも外すのも Editor の session だけで、Domain Reload で static が消えた時は session が
    /// <c>[InitializeOnLoad]</c> で置き直す。置き直しに失敗した Play を「snapshot 無し」と取り違えないよう、入口は 1 回の Play の config に
    /// <see cref="ExpectedConfigKey"/> を真で置き、取り込みの段はその key がある時だけこの口を読む（key が無い Play では、残った値があっても読まない）。
    /// </para>
    /// <para>
    /// Player の build には入らない（file 全体が <c>UNITY_EDITOR</c>）。<c>content:</c> の名前空間の key ではないので、Player の protected config の照合にも入らない。
    /// </para>
    /// </summary>
    internal static class EditorPlaySnapshotBridge
    {
        /// <summary>この Play が snapshot を宣言したことを示す config key（真偽だけ。複製の path は運ばない）。</summary>
        internal const string ExpectedConfigKey = "editor:playSnapshotExpected";

        /// <summary>宣言（並び順は Play の前に開いていた順）。置かれていなければ null。</summary>
        internal static IReadOnlyList<EditorPlaySnapshotScene>? Declaration { get; private set; }

        /// <summary>複製の asset path を additive で load する。Unity が受け付けなかった時は null を返してよい（起動の失敗として扱う）。</summary>
        internal static Func<string, AsyncOperation?>? LoadScene { get; private set; }

        internal static void Set(IReadOnlyList<EditorPlaySnapshotScene> declaration, Func<string, AsyncOperation?> loadScene)
        {
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));
            if (declaration.Count == 0) throw new ArgumentException("A snapshot declaration needs at least one scene.", nameof(declaration));
            Declaration = declaration;
            LoadScene = loadScene ?? throw new ArgumentNullException(nameof(loadScene));
        }

        internal static void Clear()
        {
            Declaration = null;
            LoadScene = null;
        }
    }
}
#endif
