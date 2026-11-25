#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using OneStarMaker.Editor.ContentOverlay;
using OneStarMaker.Runtime.BuildContent;

namespace OneStarMaker.Tests.Editor.ContentOverlay
{
    /// <summary>
    /// 上書きの directory の digest の規則（順・大小文字・空の folder・directory の外・reparse point）を一時 folder で確かめる。
    /// </summary>
    public sealed class ContentOverlayDigestTests
    {
        private string _root = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "osm-s42-digest-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            // junction は中身を辿らずに消す（Directory.Delete の再帰は link の先を消しうるので、先に link だけを外す）。
            foreach (var directory in Directory.GetDirectories(_root, "*", SearchOption.AllDirectories))
                if ((new DirectoryInfo(directory).Attributes & FileAttributes.ReparsePoint) != 0)
                    Directory.Delete(directory);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private string Dir(string name)
        {
            var path = Path.Combine(_root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        private static void Write(string directory, string relative, string text)
        {
            var path = Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        private static string Hex(byte[] bytes)
        {
            var text = new StringBuilder();
            foreach (var b in bytes) text.Append(b.ToString("x2"));
            return text.ToString();
        }

        private static string Sha(string text)
        {
            using var sha = SHA256.Create();
            return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        [Test]
        public void Digest_FollowsTheWrittenRule()
        {
            var a = Dir("a");
            Write(a, "b.txt", "B");
            Write(a, "A/x.bin", "X");
            // ordinal: "A/x.bin" < "b.txt"（大文字が先）。
            var expected = Sha("A/x.bin\n" + Sha("X") + "\n" + "b.txt\n" + Sha("B") + "\n");
            Assert.That(ContentOverlayDigest.Compute(a), Is.EqualTo(expected));
        }

        [Test]
        public void SameBytes_GiveTheSameDigest_RegardlessOfTheRootPath()
        {
            var a = Dir("a");
            var b = Dir("other-root");
            foreach (var d in new[] { a, b }) { Write(d, "one.txt", "1"); Write(d, "sub/two.txt", "2"); }
            Assert.That(ContentOverlayDigest.Compute(b), Is.EqualTo(ContentOverlayDigest.Compute(a)));
            // 公開の口は Runtime と同じ値を返す。
            Assert.That(ContentOverlayPlayConfig.ComputeOverlayDigest(a), Is.EqualTo(ContentOverlayDigest.Compute(a)));
        }

        [Test]
        public void AddingRemovingChangingOrRecasingAFile_ChangesTheDigest()
        {
            var a = Dir("a");
            Write(a, "one.txt", "1");
            Write(a, "sub/two.txt", "2");
            var original = ContentOverlayDigest.Compute(a);

            Write(a, "three.txt", "3");
            var added = ContentOverlayDigest.Compute(a);
            Assert.That(added, Is.Not.EqualTo(original));
            File.Delete(Path.Combine(a, "three.txt"));
            Assert.That(ContentOverlayDigest.Compute(a), Is.EqualTo(original));

            File.Delete(Path.Combine(a, "sub", "two.txt"));
            Assert.That(ContentOverlayDigest.Compute(a), Is.Not.EqualTo(original));
            Write(a, "sub/two.txt", "2");

            Write(a, "one.txt", "!");
            Assert.That(ContentOverlayDigest.Compute(a), Is.Not.EqualTo(original));
            Write(a, "one.txt", "1");
            Assert.That(ContentOverlayDigest.Compute(a), Is.EqualTo(original));

            // Windows では大小文字だけの改名を一時名を経由して行う。
            File.Move(Path.Combine(a, "one.txt"), Path.Combine(a, "tmp.txt"));
            File.Move(Path.Combine(a, "tmp.txt"), Path.Combine(a, "ONE.txt"));
            Assert.That(ContentOverlayDigest.Compute(a), Is.Not.EqualTo(original));
        }

        [Test]
        public void EmptyFoldersAndFilesOutsideTheDirectory_DoNotCount()
        {
            var a = Dir("a");
            Write(a, "one.txt", "1");
            var original = ContentOverlayDigest.Compute(a);
            Directory.CreateDirectory(Path.Combine(a, "empty", "nested"));
            Write(_root, "outside.txt", "outside");
            Write(_root, "a-sibling/x.txt", "x");
            Assert.That(ContentOverlayDigest.Compute(a), Is.EqualTo(original));
        }

        [Test]
        public void MissingDirectory_Throws()
            => Assert.Throws<ContentDirectoryException>(() => ContentOverlayDigest.Compute(Path.Combine(_root, "missing")));

        [Test]
        public void JunctionInsideOrAsTheRoot_Throws()
        {
            var target = Dir("target");
            Write(target, "t.txt", "t");
            var a = Dir("a");
            Write(a, "one.txt", "1");
            Junction(Path.Combine(a, "link"), target);
            var inside = Assert.Throws<ContentDirectoryException>(() => ContentOverlayDigest.Compute(a));
            Assert.That(inside!.Message, Does.Contain("reparse point").And.Contain("link"));

            var rootLink = Path.Combine(_root, "root-link");
            Junction(rootLink, target);
            Assert.Throws<ContentDirectoryException>(() => ContentOverlayDigest.Compute(rootLink));
        }

        /// <summary>junction は管理者権限なしで作れる（symlink は権限か開発者 mode が要る）。</summary>
        private static void Junction(string link, string target)
        {
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
            process.WaitForExit();
            Assert.That(process.ExitCode, Is.EqualTo(0), process.StandardError.ReadToEnd());
        }
    }
}
