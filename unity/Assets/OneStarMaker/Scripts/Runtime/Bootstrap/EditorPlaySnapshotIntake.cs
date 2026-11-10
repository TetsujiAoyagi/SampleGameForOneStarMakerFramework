#nullable enable

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace OneStarMaker.Runtime
{
    /// <summary>取り込みの照合が見る loaded の Scene の 1 件（Unity の Scene の値を写したもの）。</summary>
    internal readonly struct EditorPlaySnapshotLoadedScene
    {
        internal EditorPlaySnapshotLoadedScene(string name, string path)
        {
            Name = name ?? string.Empty;
            Path = path ?? string.Empty;
        }

        internal string Name { get; }
        internal string Path { get; }
    }

    /// <summary>
    /// 宣言と、取り込みの段が load を終えた時点の loaded の Scene の照合。状態を持たず、Unity の Scene を使わない。
    /// <para>
    /// 照合が合わない Play は起動の失敗にする。合わないまま進むと、後の登録や初回 Scene の load が同じ名前を Content Directory から読み、
    /// 編集中の Scene の代わりに build 済みの Scene が黙って動く。
    /// </para>
    /// </summary>
    internal static class EditorPlaySnapshotIntake
    {
        /// <summary>宣言のとおりに 1 Scene 1 個ずつ複製が loaded なら null、そうでなければ理由。</summary>
        internal static string? Verify(IReadOnlyList<EditorPlaySnapshotScene> declaration, IReadOnlyList<EditorPlaySnapshotLoadedScene> loaded)
        {
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));
            if (loaded == null) throw new ArgumentNullException(nameof(loaded));
            if (declaration.Count == 0) return "The play snapshot declaration is empty.";

            var duplicateIdentity = declaration.GroupBy(x => x.Identity, StringComparer.Ordinal).FirstOrDefault(x => x.Count() > 1);
            if (duplicateIdentity != null)
                return "The play snapshot declares scene '" + duplicateIdentity.Key + "' more than once.";
            if (declaration.Count(x => x.IsActive) > 1)
                return "The play snapshot declares more than one active scene.";

            foreach (var scene in declaration)
            {
                var sameName = loaded.Where(x => string.Equals(x.Name, scene.Identity, StringComparison.Ordinal)).ToArray();
                var copies = sameName.Count(x => string.Equals(x.Path, scene.CopyPath, StringComparison.Ordinal));
                if (copies == 0)
                    return "The play snapshot of scene '" + scene.Identity + "' is not loaded from " + scene.CopyPath + ".";
                if (copies > 1)
                    return "The play snapshot of scene '" + scene.Identity + "' is loaded more than once.";
                var other = sameName.FirstOrDefault(x => !string.Equals(x.Path, scene.CopyPath, StringComparison.Ordinal));
                if (sameName.Length != 1)
                    return "Another scene named '" + scene.Identity + "' is already loaded from " +
                           (other.Path.Length == 0 ? "(no path)" : other.Path) + "; the snapshot would run twice.";
            }
            return null;
        }

        /// <summary>
        /// 取り込みが途中で失敗した時に unload する Scene。path が宣言の複製と一致する loaded の Scene だけで、
        /// 空の開始 Scene、Content Directory から来た Scene（UI 等）、宣言に無い Scene は含めない。
        /// </summary>
        internal static IReadOnlyList<EditorPlaySnapshotLoadedScene> SelectUnloadTargets(
            IReadOnlyList<EditorPlaySnapshotScene> declaration, IReadOnlyList<EditorPlaySnapshotLoadedScene> loaded)
        {
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));
            if (loaded == null) throw new ArgumentNullException(nameof(loaded));
            var copyPaths = new HashSet<string>(declaration.Select(x => x.CopyPath), StringComparer.Ordinal);
            return loaded.Where(x => x.Path.Length != 0 && copyPaths.Contains(x.Path)).ToArray();
        }

        /// <summary>取り込みの成功の log。観測 UI はこの接頭辞と <c>scene=</c> で Scene 名を拾う。</summary>
        internal const string LogPrefix = "[AppInit] Editor play snapshot intake: ";

        internal static string FormatLoadedLog(EditorPlaySnapshotScene scene) =>
            LogPrefix + "scene=" + scene.Identity + " copy=" + scene.CopyPath + " active=" + (scene.IsActive ? "true" : "false");
    }
}
#endif
