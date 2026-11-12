#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using OneStarMaker.Foundation.Logging;

namespace OneStarMaker.Tests.Foundation
{
    [TestFixture]
    public sealed class LogDirectoryMaintenanceTests
    {
        private string _root = null!;
        private readonly DateTime _startup = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void SetUp() => _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "osm-retention-" + Guid.NewGuid().ToString("N"))).FullName;

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        [TestCase("2026-11-01_000.log")]
        [TestCase("logging-errors_2026-11-01_000.txt")]
        [TestCase("2026-11-01_1000.log")]
        [TestCase("logging-errors_2026-11-01_1000.txt")]
        public void Retention_UsesStrictLastWriteUtcBoundary(string name)
        {
            var cutoff = _startup - LogRetentionPolicy.Retention;
            var path = Put(name, cutoff);
            LogDirectoryMaintenance.Clean(_root, _startup);
            Assert.IsTrue(File.Exists(path), "ちょうど96時間は保持する。");
            File.SetLastWriteTimeUtc(path, cutoff.AddSeconds(1));
            LogDirectoryMaintenance.Clean(_root, _startup);
            Assert.IsTrue(File.Exists(path));
            File.SetLastWriteTimeUtc(path, _startup.AddDays(1));
            LogDirectoryMaintenance.Clean(_root, _startup);
            Assert.IsTrue(File.Exists(path), "未来の更新時刻は削除しない。");
            File.SetLastWriteTimeUtc(path, cutoff.AddSeconds(-1));
            LogDirectoryMaintenance.Clean(_root, _startup);
            Assert.IsFalse(File.Exists(path));
            Assert.DoesNotThrow(() => LogDirectoryMaintenance.Clean(_root, _startup));
        }

        [TestCase("notes.txt")]
        [TestCase("other.log")]
        [TestCase("logging-errors.txt")]
        [TestCase("2026-02-30_000.log")]
        [TestCase("2026-11-01_00.log")]
        [TestCase("2026-11-01_-001.log")]
        [TestCase("logging-errors_2026-11-01_000.log")]
        [TestCase("2026-11-01_000.txt")]
        [TestCase("2026-11-01_000.log\n")]
        public void NameRule_RejectsUnmanagedNames(string name)
        {
            Assert.IsFalse(LogFileNames.TryParse(name, out _, out _, out _));
            if (name.Contains("\n")) return;
            var path = Put(name, _startup.AddDays(-10));
            LogDirectoryMaintenance.Clean(_root, _startup);
            Assert.AreEqual("sentinel", File.ReadAllText(path));
        }

        [Test]
        public void Cleanup_SkipsNestedDirectoriesRecentOldDateAndMissingRoot()
        {
            var recent = Put("2000-01-01_000.log", _startup);
            var nested = Put("nested/2000-01-01_000.log", _startup.AddDays(-10));
            var directory = Path.Combine(_root, "2000-01-01_001.log");
            Directory.CreateDirectory(directory);
            LogDirectoryMaintenance.Clean(_root, _startup);
            Assert.AreEqual("sentinel", File.ReadAllText(recent));
            Assert.AreEqual("sentinel", File.ReadAllText(nested));
            Assert.IsTrue(Directory.Exists(directory));
            Assert.DoesNotThrow(() => LogDirectoryMaintenance.Clean(Path.Combine(_root, "missing"), _startup));
        }

        [Test]
        [Platform("Win")]
        public void Cleanup_LockedFileIsRetainedOthersContinueAndNextStartupRetries()
        {
            var busy = Put("2000-01-01_000.log", _startup.AddDays(-10));
            var free = Put("logging-errors_2000-01-01_1000.txt", _startup.AddDays(-10));
            using (var handle = new FileStream(busy, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                LogDirectoryMaintenance.Clean(_root, _startup);
                Assert.IsTrue(File.Exists(busy));
                Assert.IsFalse(File.Exists(free), "個別削除失敗で他の候補を止めない。");
            }
            LogDirectoryMaintenance.Clean(_root, _startup);
            Assert.IsFalse(File.Exists(busy));
        }

        [Test]
        [Platform("Win")]
        public void Cleanup_ReparseRootAndChildLeaveTargetsUnchanged()
        {
            // 実 junction は通常ユーザーでも作成できる。リンク先も専用 temp root 内に隔離し、
            // finally でリンク自体を外してから fixture を削除する。実ログには触れない。
            var targetDirectory = Path.Combine(_root, "target");
            Directory.CreateDirectory(targetDirectory);
            var target = Put("target/2000-01-01_000.log", _startup.AddDays(-10));
            var rootLink = Path.Combine(_root, "root-link");
            var childLink = Path.Combine(_root, "logging-errors_2000-01-01_000.txt");
            try
            {
                MakeLink(rootLink, targetDirectory, true);
                LogDirectoryMaintenance.Clean(rootLink, _startup);
                Assert.AreEqual("sentinel", File.ReadAllText(target));
                // hard link には ReparsePoint が付かないので、file symbolic link を使う。
                // Developer Mode / 権限が無い環境では、C が未検証と識別できるよう明示 skip。
                if (!MakeLink(childLink, target, false, optional: true))
                    Assert.Ignore("File symbolic link creation requires Windows Developer Mode or symlink privilege; root junction was verified.");
                LogDirectoryMaintenance.Clean(_root, _startup);
                Assert.IsTrue(File.Exists(childLink));
                Assert.AreEqual("sentinel", File.ReadAllText(target));
            }
            finally
            {
                if (File.Exists(childLink)) File.Delete(childLink);
                if (Directory.Exists(rootLink)) Directory.Delete(rootLink);
            }
        }

        private static bool MakeLink(string link, string target, bool directory, bool optional = false)
        {
            var info = new ProcessStartInfo("cmd.exe", "/c mklink " + (directory ? "/J " : "") + "\"" + link + "\" \"" + target + "\"")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var process = Process.Start(info)!;
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (!optional) Assert.AreEqual(0, process.ExitCode, "junction fixture creation");
            return process.ExitCode == 0;
        }

        private string Put(string name, DateTime timestamp)
        {
            var path = Path.Combine(_root, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "sentinel");
            File.SetLastWriteTimeUtc(path, timestamp);
            return path;
        }
    }
}
