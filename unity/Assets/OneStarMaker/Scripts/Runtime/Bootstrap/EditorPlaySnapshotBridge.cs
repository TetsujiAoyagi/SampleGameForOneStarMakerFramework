#nullable enable

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneStarMaker.Runtime
{
    /// <summary>
    /// Editor の Play 入口が Play の前に固定した Scene の複製（snapshot）の 1 件。起動の取り込みの段が読む形だけを持つ。
    /// Scene 名（<see cref="Identity"/>）は複製の file 名と同じで、Unity の Scene の name として見える。
    /// </summary>
    internal sealed class EditorPlaySnapshotScene
    {
        internal EditorPlaySnapshotScene(string identity, string copyPath, bool isActive)
        {
            if (string.IsNullOrEmpty(identity)) throw new ArgumentException("Identity is required.", nameof(identity));
            if (string.IsNullOrEmpty(copyPath)) throw new ArgumentException("Copy path is required.", nameof(copyPath));
            Identity = identity;
            CopyPath = copyPath;
            IsActive = isActive;
        }

        internal string Identity { get; }
        /// <summary><c>Assets/</c> から始まる複製の asset path。</summary>
        internal string CopyPath { get; }
        /// <summary>Play の前に active だった Scene の複製なら真（宣言の中で 1 つだけ）。</summary>
        internal bool IsActive { get; }
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
