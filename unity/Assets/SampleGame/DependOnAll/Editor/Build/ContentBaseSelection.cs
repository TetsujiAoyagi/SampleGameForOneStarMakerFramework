#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>取得で選んだ base の 1 件。key は cache root・contentSet・target。</summary>
    [Serializable]
    internal sealed class ContentBaseSelectionEntry
    {
        public string cacheRoot = "";
        public string contentSet = "";
        public string target = "";
        public string revision = "";
        public string manifestSha256 = "";
        public string targetOrigin = "";
        public string bytesOrigin = "";
        public string digestAuthority = "";
        /// <summary>UTC の <c>O</c> 形式。receipt の <c>installedUtc</c> と UTC で比べる。</summary>
        public string selectedUtc = "";

        internal DateTime SelectedUtc =>
            DateTime.Parse(selectedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
    }

    /// <summary>記録の読みの結果。<see cref="Entry"/> が null なら記録なしで、<see cref="Problem"/> が空でなければ読めなかった理由。</summary>
    internal sealed class ContentBaseSelectionRead
    {
        internal ContentBaseSelectionRead(ContentBaseSelectionEntry? entry, string problem)
        {
            Entry = entry;
            Problem = problem;
        }

        internal ContentBaseSelectionEntry? Entry { get; }
        internal string Problem { get; }
    }

    /// <summary>
    /// 取得で選んだ base の記録（<c>UserSettings/OSMContentBaseSelection.json</c>。git 管理外で、Editor を閉じても残る）。
    /// 入口はこれを読み、記録の組と同じ install の時刻を「install の時刻と、選んだ時刻の新しい方」と読んで並べる。
    /// <para>
    /// 記録の file が壊れている・読めない時は例外を漏らさず「記録なし」と理由を返す（取得を使わない利用者の Play を壊さない）。
    /// entry は cache root ごとに足し、消す所有者を置かない（存在しない cache root の entry は読まれないだけ）。
    /// </para>
    /// </summary>
    internal static class ContentBaseSelection
    {
        internal const string DefaultPath = "UserSettings/OSMContentBaseSelection.json";

        [Serializable]
        private sealed class Document
        {
            public int version = 1;
            public ContentBaseSelectionEntry[] entries = Array.Empty<ContentBaseSelectionEntry>();
        }

        /// <summary>書き手と入口で同じ key の正規化: <c>GetFullPath</c> の後に末尾の区切りを除く。比べる時は大小文字を区別しない。</summary>
        internal static string NormalizeCacheRoot(string cacheRoot) =>
            Path.GetFullPath(cacheRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        internal static ContentBaseSelectionRead Read(string path, string cacheRoot, string contentSet, string target)
        {
            try
            {
                if (!File.Exists(path)) return new ContentBaseSelectionRead(null, string.Empty);
                var document = JsonUtility.FromJson<Document>(File.ReadAllText(path, new UTF8Encoding(false, true)));
                if (document == null || document.version != 1 || document.entries == null)
                    return new ContentBaseSelectionRead(null, "The base selection record " + path + " has an unsupported form.");
                var key = NormalizeCacheRoot(cacheRoot);
                var entry = document.entries.LastOrDefault(x => x != null && Matches(x, key, contentSet, target));
                if (entry == null) return new ContentBaseSelectionRead(null, string.Empty);
                _ = entry.SelectedUtc; // 時刻を読めない entry は使わない。
                return new ContentBaseSelectionRead(entry, string.Empty);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or FormatException or DecoderFallbackException)
            {
                return new ContentBaseSelectionRead(null, "The base selection record " + path + " cannot be read: " + ex.Message);
            }
        }

        /// <summary>同じ key の entry を置き換えて書く。失敗は例外で返す（呼び手は取得の結果を変えず Warning にする）。</summary>
        internal static void Write(string path, ContentBaseSelectionEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            var key = NormalizeCacheRoot(entry.cacheRoot);
            entry.cacheRoot = key;
            var entries = new List<ContentBaseSelectionEntry>();
            try
            {
                if (File.Exists(path))
                {
                    var existing = JsonUtility.FromJson<Document>(File.ReadAllText(path, new UTF8Encoding(false, true)));
                    if (existing?.entries != null) entries.AddRange(existing.entries.Where(x => x != null));
                }
            }
            catch (Exception ex) when (ex is ArgumentException or DecoderFallbackException)
            {
                // 壊れた記録は読めないので、書き直す（他の cache root の entry も読めないまま失われる）。
            }
            entries.RemoveAll(x => Matches(x, key, entry.contentSet, entry.target));
            entries.Add(entry);
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(new Document { entries = entries.ToArray() }, true), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static bool Matches(ContentBaseSelectionEntry entry, string normalizedCacheRoot, string contentSet, string target)
        {
            if (string.IsNullOrWhiteSpace(entry.cacheRoot)) return false;
            string root;
            try { root = NormalizeCacheRoot(entry.cacheRoot); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
            return string.Equals(root, normalizedCacheRoot, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(entry.contentSet, contentSet, StringComparison.Ordinal)
                   && string.Equals(entry.target, target, StringComparison.Ordinal);
        }
    }
}
