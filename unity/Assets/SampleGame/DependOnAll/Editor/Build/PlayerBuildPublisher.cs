#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SampleGame.DependOnAll.Editor.Build
{
    internal interface IPlayerBuildFileSystem
    {
        bool DirectoryExists(string path); void CreateDirectory(string path); void CopyDirectory(string source, string target);
        void MoveDirectory(string source, string target); void DeleteDirectory(string path); void WriteAllText(string path, string value);
    }
    internal sealed class SystemPlayerBuildFileSystem : IPlayerBuildFileSystem
    {
        public bool DirectoryExists(string p) => Directory.Exists(p); public void CreateDirectory(string p) => Directory.CreateDirectory(p);
        public void MoveDirectory(string a, string b) => Directory.Move(a, b); public void DeleteDirectory(string p) => Directory.Delete(p, true);
        public void WriteAllText(string p, string v) => File.WriteAllText(p, v, new UTF8Encoding(false));
        public void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            foreach (var child in Directory.GetDirectories(source)) CopyDirectory(child, Path.Combine(target, Path.GetFileName(child)));
        }
    }
    internal sealed class PlayerBuildPublisher
    {
        private readonly IPlayerBuildFileSystem _fs;
        internal PlayerBuildPublisher(IPlayerBuildFileSystem? fs = null) => _fs = fs ?? new SystemPlayerBuildFileSystem();
        internal string Publish(string root, string identity, string playerDirectory, string contentDirectory, string receiptJson)
        {
            var staging = Child(root, "staging", identity); var final = Child(root, "players", identity);
            if (_fs.DirectoryExists(staging) || _fs.DirectoryExists(final)) throw new IOException("Player identity output already exists.");
            try
            {
                _fs.CreateDirectory(staging);
                _fs.CopyDirectory(playerDirectory, staging);
                _fs.CopyDirectory(contentDirectory, Path.Combine(staging, "content"));
                _fs.WriteAllText(Path.Combine(staging, "build-receipt.json"), receiptJson);
                _fs.CreateDirectory(Path.GetDirectoryName(final)!); _fs.MoveDirectory(staging, final); return final;
            }
            catch { if (_fs.DirectoryExists(staging)) _fs.DeleteDirectory(staging); throw; }
        }
        private static string Child(string root, params string[] parts)
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var value = Path.GetFullPath(Path.Combine(new[] { root }.Concat(parts).ToArray()));
            if (!value.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new IOException("Publish path escapes root.");
            return value;
        }
    }
}
