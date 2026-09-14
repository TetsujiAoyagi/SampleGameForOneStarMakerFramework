#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace CD0Spike.Editor
{
    internal static class Cd0ArtifactInventory
    {
        internal static IReadOnlyList<Entry> Capture(string allowedRoot, string targetDirectory)
        {
            var root = CanonicalDirectory(allowedRoot);
            var target = CanonicalDirectory(targetDirectory);
            if (!IsContained(root, target)) throw new InvalidOperationException($"Target is outside the CD0 root: {target}");
            var entries = new List<Entry>();
            if (!Directory.Exists(target)) return entries;
            foreach (var path in Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories))
            {
                var fullPath = Path.GetFullPath(path);
                using var stream = File.OpenRead(fullPath);
                using var sha = SHA256.Create();
                var hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
                entries.Add(new Entry(Path.GetRelativePath(target, fullPath).Replace('\\', '/'), stream.Length, hash));
            }
            entries.Sort((left, right) => string.CompareOrdinal(left.RelativePath, right.RelativePath));
            return entries;
        }

        internal static bool IsContained(string canonicalRoot, string canonicalTarget)
        {
            var rootWithSeparator = canonicalRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return canonicalTarget.Equals(canonicalRoot, StringComparison.OrdinalIgnoreCase)
                || canonicalTarget.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        internal static string CanonicalDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is empty.", nameof(path));
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        internal readonly struct Entry
        {
            internal Entry(string relativePath, long size, string sha256)
            {
                RelativePath = relativePath;
                Size = size;
                Sha256 = sha256;
            }

            internal string RelativePath { get; }
            internal long Size { get; }
            internal string Sha256 { get; }
        }
    }
}
