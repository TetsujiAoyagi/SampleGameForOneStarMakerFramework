#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OneStarMaker.Runtime.BuildContent
{
    /// <summary>同一 Windows user の process 間で revision の共有利用と排他削除を調停する。</summary>
    internal sealed class RevisionProcessLease : IDisposable
    {
        private List<FileStream>? _streams;

        private RevisionProcessLease(List<FileStream> streams) => _streams = streams;

        internal static RevisionProcessLease AcquireRead(string identity, string target, string path) =>
            Acquire(identity, target, path, exclusive: false);

        internal static bool TryAcquireDelete(string identity, string target, string path,
            out RevisionProcessLease? lease)
        {
            try
            {
                lease = Acquire(identity, target, path, exclusive: true);
                return true;
            }
            catch (IOException ex) when (IsSharingViolation(ex))
            {
                lease = null;
                return false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new ContentDirectoryException(ContentDirectoryFailureCode.RevisionLockUnavailable,
                    "Revision lock is unavailable.", identity, target, innerException: ex);
            }
        }

        internal static bool IsSharingViolation(IOException exception) =>
            (exception.HResult & 0xffff) is 32 or 33;

        private static RevisionProcessLease Acquire(string identity, string target, string path,
            bool exclusive)
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OneStarMaker", "RevisionLocks", "v1");
            Directory.CreateDirectory(root);

            // identity と target は UTF-8 byte 長を big-endian 4 byte で先行させる。
            // 単純な区切り文字連結による tuple 境界の衝突を作らない。
            var keys = new[]
                {
                    IdentityKey(identity, target),
                    Key(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()))
                }
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal);
            var opened = new List<FileStream>();
            try
            {
                foreach (var key in keys)
                {
                    var file = Path.Combine(root, key + ".lock");
                    EnsureFile(file);
                    opened.Add(new FileStream(file, FileMode.Open,
                        exclusive ? FileAccess.ReadWrite : FileAccess.Read,
                        exclusive ? FileShare.None : FileShare.Read));
                }
                return new RevisionProcessLease(opened);
            }
            catch
            {
                foreach (var stream in opened) stream.Dispose();
                throw;
            }
        }

        private static void EnsureFile(string path)
        {
            try
            {
                using var created = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                    FileShare.ReadWrite);
            }
            catch (IOException)
            {
                // CreateNew 競合は正常。既存 file を確認できない I/O failure は fail closed にする。
                if (!File.Exists(path)) throw;
            }
        }

        private static string IdentityKey(string identity, string target)
        {
            var identityBytes = Encoding.UTF8.GetBytes(identity);
            var targetBytes = Encoding.UTF8.GetBytes(target);
            using var value = new MemoryStream(checked(8 + identityBytes.Length + targetBytes.Length));
            WriteLength(value, identityBytes.Length);
            value.Write(identityBytes, 0, identityBytes.Length);
            WriteLength(value, targetBytes.Length);
            value.Write(targetBytes, 0, targetBytes.Length);
            return Key(value.ToArray());
        }

        private static void WriteLength(Stream destination, int length)
        {
            destination.WriteByte((byte)(length >> 24));
            destination.WriteByte((byte)(length >> 16));
            destination.WriteByte((byte)(length >> 8));
            destination.WriteByte((byte)length);
        }

        private static string Key(byte[] value)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(value).Select(item => item.ToString("x2")));
        }

        public void Dispose()
        {
            var streams = _streams;
            _streams = null;
            if (streams == null) return;
            foreach (var stream in streams) stream.Dispose();
        }
    }
}
