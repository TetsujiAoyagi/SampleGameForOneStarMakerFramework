#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Text;
using OneStarMaker.Runtime.BuildContent.Distribution;

namespace OneStarMaker.Runtime.BuildContent
{
    /// <summary>
    /// 上書きの content directory の中身全体から 1 つの digest を作る（算法の正本）。状態を持たず、Unity を呼ばない。
    /// <para>
    /// 上書きは publish されず file の一覧と hash を持つ manifest が無いので、build の後に directory が書き換わったことを
    /// 入口（build の時に記録し、起動の直前に取り直す）と Player（登録の直前に取り直す）が同じ規則で見分けるために使う。
    /// 規則を変えると、既に記録した digest と Player が取り直す digest が食い違うので、入口と Runtime の両方が必ずこの関数を呼ぶ。
    /// </para>
    /// <para>
    /// 規則: directory の直下から再帰した全 file を、相対 path（<c>/</c> 区切り、大小文字はそのまま）の ordinal 順に並べ、
    /// 各 file の <c>&lt;相対 path&gt;\n&lt;SHA-256 の小文字の 16 進&gt;\n</c> を UTF-8 で連ねた bytes の SHA-256（小文字の 16 進）。
    /// 空の folder は数えない（file が無いので行が出ない）。directory の外は辿らない。directory 自体か中に reparse point
    /// （symlink・junction）があれば辿らずに失敗にする（外の file を中身として数えると、照合が別の場所の書き換えを見逃す）。
    /// </para>
    /// </summary>
    internal static class ContentOverlayDigest
    {
        /// <param name="directory">上書きの content directory の絶対 path（呼び手が正規化した値）。</param>
        /// <exception cref="ContentDirectoryException">directory が無い、または reparse point を含む。</exception>
        internal static string Compute(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,
                    "Content overlay directory for the digest does not exist.");
            var root = new DirectoryInfo(directory);
            if ((root.Attributes & FileAttributes.ReparsePoint) != 0)
                throw ReparsePoint(".");

            var files = new List<(string Relative, string FullPath)>();
            Collect(root, string.Empty, files);
            files.Sort((a, b) => string.CompareOrdinal(a.Relative, b.Relative));

            var text = new StringBuilder();
            foreach (var (relative, fullPath) in files)
                text.Append(relative).Append('\n').Append(ContentDeliveryFiles.Sha256(fullPath)).Append('\n');
            return ContentDeliveryFiles.Sha256(Encoding.UTF8.GetBytes(text.ToString()));
        }

        private static void Collect(DirectoryInfo directory, string prefix, List<(string, string)> files)
        {
            // 列挙は入口 1 段ずつにして、子の folder を辿る前に reparse point かを見る（再帰の列挙の option は link を辿りうる）。
            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                var relative = prefix.Length == 0 ? entry.Name : prefix + "/" + entry.Name;
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw ReparsePoint(relative);
                if (entry is DirectoryInfo child)
                    Collect(child, relative, files);
                else
                    files.Add((relative, entry.FullName));
            }
        }

        private static ContentDirectoryException ReparsePoint(string relative) =>
            new(ContentDirectoryFailureCode.InvalidConfiguration,
                "Content overlay directory contains a reparse point and cannot be digested: " + relative);
    }
}
