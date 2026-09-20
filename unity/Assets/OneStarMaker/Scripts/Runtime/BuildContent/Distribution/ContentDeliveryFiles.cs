#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    [Serializable]
    internal sealed class ContentInstallReceipt
    {
        public int version = 1;
        public string manifestSha256 = "";
        public string contentSet = "";
        public string revision = "";
        public string target = "";
        public string installedUtc = "";
    }

    [Serializable]
    internal sealed class ContentStagingMarker
    {
        public int version = 1;
        public string cacheRoot = "";
        public string stagingRoot = "";
        public string contentSet = "";
        public string revision = "";
    }

    internal static class ContentDeliveryFiles
    {
        internal static string Sha256(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return Hex(hash.ComputeHash(bytes));
        }

        internal static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var hash = SHA256.Create();
            return Hex(hash.ComputeHash(stream));
        }

        private static string Hex(byte[] bytes) => string.Concat(bytes.Select(x => x.ToString("x2")));

        internal static string Resolve(string root, string relative)
        {
            if (!ContentManifestValidation.Relative(relative))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest, "Invalid relative path.");
            var normalized = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var value = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!value.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest, "Path escapes its root.");
            return value;
        }

        internal static void RejectReparseAncestors(string root, string path)
        {
            var current = Path.GetFullPath(path);
            var boundary = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            while (current.TrimEnd(Path.DirectorySeparatorChar).Equals(boundary, StringComparison.OrdinalIgnoreCase)
                || current.StartsWith(boundary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // leaf が metadata file の場合も検査する。DirectoryInfo.Exists だけでは
                    // file symlink を見落とし、検証した root 外の bytes を登録できてしまう。
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest,
                            "Managed content cannot traverse a reparse point.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                var parent = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar));
                if (string.IsNullOrEmpty(parent)) break;
                current = parent;
            }
        }

        internal static void WriteJson<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonUtility.ToJson(value, true), new UTF8Encoding(false));
        }

        internal static T ReadJson<T>(string path) where T : class
        {
            try
            {
                RejectReparseAncestors(Path.GetPathRoot(Path.GetFullPath(path))!, path);
                var json = File.ReadAllText(path, new UTF8Encoding(false, true));
                if (typeof(T) == typeof(ContentInstallReceipt))
                    ContentManifestValidation.RequireObjectFields(json, "version", "manifestSha256", "contentSet",
                        "revision", "target", "installedUtc");
                else if (typeof(T) == typeof(ContentStagingMarker))
                    ContentManifestValidation.RequireObjectFields(json, "version", "cacheRoot", "stagingRoot", "contentSet", "revision");
                else if (typeof(T).Name == "Tombstone")
                    ContentManifestValidation.RequireObjectFields(json, "version", "cacheRoot", "revisionRoot",
                        "contentPath", "contentSet", "revision", "target", "digest");
                else if (typeof(T).Name == "KnownGoodFile")
                    ContentManifestValidation.RequireObjectFields(json, "version", "items");
                return JsonUtility.FromJson<T>(json) ?? throw new FormatException();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
            {
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest, "JSON metadata is invalid.", ex);
            }
        }

        internal static void VerifyTree(string contentRoot, ValidatedContentManifest manifest)
        {
            RejectReparseAncestors(Path.GetPathRoot(Path.GetFullPath(contentRoot))!, contentRoot);
            var files = new List<string>();
            if (Directory.Exists(contentRoot))
            {
                var pending = new Stack<string>();
                pending.Push(contentRoot);
                while (pending.Count != 0)
                {
                    // recursive enumeration の前に各 entry を検査し、junction の先へ探索を進めない。
                    foreach (var path in Directory.GetFileSystemEntries(pending.Pop()))
                    {
                        var attributes = File.GetAttributes(path);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                            throw new ContentDeliveryException(ContentDeliveryFailureCode.IntegrityMismatch,
                                "Installed content contains a reparse point.");
                        if ((attributes & FileAttributes.Directory) != 0) pending.Push(path);
                        else files.Add(path);
                    }
                }
            }
            var expected = manifest.Files.Select(x => x.path).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var actual = files.Select(x => Path.GetRelativePath(contentRoot, x).Replace('\\', '/'))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            if (!expected.SequenceEqual(actual, StringComparer.OrdinalIgnoreCase))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.IntegrityMismatch,
                    "Installed file set differs from the manifest.");
            foreach (var entry in manifest.Files)
            {
                var path = Resolve(contentRoot, entry.path);
                var info = new FileInfo(path);
                if (!info.Exists)
                    throw new ContentDeliveryException(ContentDeliveryFailureCode.MissingFile, "Manifest file is missing.");
                if (info.Length != entry.size || Sha256(path) != entry.sha256)
                    throw new ContentDeliveryException(ContentDeliveryFailureCode.IntegrityMismatch,
                        "Installed file failed size or digest validation.");
            }
        }
    }
}
