#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Editor.Build.Content
{
    // 計画が手元の file を読む口。test は fake に替えて、手元の file と GUID の対応だけを変える（編集の一覧は渡さない）。
    // 読むだけで、利用者の source を書かない。
    internal interface IContentOverlayLocalSource
    {
        // GUID が手元で指す file の path（`Assets/...`）。解決しない時と、解決しても file が無い時は null。
        string? ResolveGuid(string guid);
        // file の SHA-256（小文字 16 進）。file が無ければ null。path は `Assets/...` の形。
        string? Sha256(string path);
    }

    internal sealed class UnityContentOverlayLocalSource : IContentOverlayLocalSource
    {
        private readonly string _projectRoot = Path.GetDirectoryName(Application.dataPath)!;

        public string? ResolveGuid(string guid)
        {
            // AssetDatabase は、file を Assets の外へ退かして Refresh した後も、消えた asset の GUID と path の対応を返し続ける
            // （6000.6.9f1 で観測）。file の有無で「手元で解決する」を決める。
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            path = path.Replace('\\', '/');
            return File.Exists(Path.Combine(_projectRoot, path)) ? path : null;
        }

        public string? Sha256(string path) => ContentSourceHashes.Sha256File(Path.Combine(_projectRoot, path));
    }

    // 閉包の file と .meta の SHA-256 をまとめて取る。publish の sourceFiles、build の前後の照合、計画、再利用の鍵が同じ規則を使う。
    internal static class ContentSourceHashes
    {
        internal const string MetaSuffix = ".meta";

        // path（`Assets/...`）ごとに file と .meta の SHA-256 を取る。無い file は null として残す（「無い」も記録する）。
        internal static SortedDictionary<string, string?> Capture(IEnumerable<string> paths, Func<string, string?> sha256)
        {
            var result = new SortedDictionary<string, string?>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                if (!result.ContainsKey(path)) result[path] = sha256(path);
                var meta = path + MetaSuffix;
                if (!result.ContainsKey(meta)) result[meta] = sha256(meta);
            }
            return result;
        }

        internal static SortedDictionary<string, string?> Capture(string projectRoot, IEnumerable<string> paths)
            => Capture(paths, x => Sha256File(Path.Combine(projectRoot, x)));

        // 同じ path の集合で、値（無い＝null を含む）が 1 つでも違う path を返す。
        internal static IReadOnlyList<string> Differences(IReadOnlyDictionary<string, string?> expected, IReadOnlyDictionary<string, string?> actual)
            => expected.Keys.Union(actual.Keys, StringComparer.Ordinal)
                .Where(x => !actual.TryGetValue(x, out var a) || !expected.TryGetValue(x, out var e) || !string.Equals(a, e, StringComparison.Ordinal))
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();

        internal static string? Sha256File(string fullPath)
        {
            if (!File.Exists(fullPath)) return null;
            try
            {
                using var stream = File.OpenRead(fullPath);
                using var hash = System.Security.Cryptography.SHA256.Create();
                return Hex(hash.ComputeHash(stream));
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }

        internal static string Sha256Text(string text)
        {
            using var hash = System.Security.Cryptography.SHA256.Create();
            return Hex(hash.ComputeHash(new System.Text.UTF8Encoding(false).GetBytes(text)));
        }

        private static string Hex(byte[] bytes) => string.Concat(bytes.Select(x => x.ToString("x2")));
    }
}
