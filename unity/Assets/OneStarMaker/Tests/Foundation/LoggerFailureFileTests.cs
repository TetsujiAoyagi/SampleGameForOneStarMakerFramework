#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Foundation.Logging;

namespace OneStarMaker.Tests.Foundation
{
    [TestFixture]
    public sealed class LoggerFailureFileTests
    {
        private string _root = null!;
        private DateTimeOffset _now;
        private readonly Exception _error = new InvalidOperationException("secret must not appear");

        [SetUp]
        public void SetUp()
        {
            _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "osm-notice-" + Guid.NewGuid().ToString("N"))).FullName;
            _now = new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        [Test]
        public void Rotation_RetainsEarlierBytesAndContinuesAfterRestartAndDateChange()
        {
            using (var sink = NewSink())
            {
                Assert.IsEmpty(Directory.GetFiles(_root), "構築時には通知 file を開かない。");
                for (var i = 0; i < 100; i++) sink.Write("rolling-json", _error);
            }
            var originals = Directory.GetFiles(_root).ToDictionary(p => p, File.ReadAllBytes);
            Assert.Greater(originals.Count, 2);
            // 最新も満杯にして、再起動時に追記できる空き世代が無い状態にする。
            var latest = originals.Keys.OrderBy(p => p, StringComparer.Ordinal).Last();
            File.WriteAllBytes(latest, new byte[1024]);
            originals[latest] = File.ReadAllBytes(latest);
            using (var sink = NewSink())
            {
                sink.Write("realtime-json", _error);
                Assert.AreEqual(originals.Count + 1, Directory.GetFiles(_root).Length);
                _now = _now.AddDays(1);
                sink.Write("rolling-json", _error);
                Assert.AreEqual(originals.Count + 2, Directory.GetFiles(_root).Length);
            }
            foreach (var pair in originals) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key));
        }

        [Test]
        public void Rotation_999To1000AndBlockedNextFileRetriesAfterRecovery()
        {
            var previous = Path.Combine(_root, LogFileNames.Notice(_now.LocalDateTime, 999));
            File.WriteAllBytes(previous, new byte[1024]);
            var next = Path.Combine(_root, LogFileNames.Notice(_now.LocalDateTime, 1000));
            Directory.CreateDirectory(next);
            using var sink = NewSink();
            Assert.DoesNotThrow(() => sink.Write("rolling-json", _error));
            Assert.IsFalse(File.Exists(next));
            Directory.Delete(next);
            sink.Write("realtime-json", _error);
            Assert.AreEqual(1, File.ReadAllLines(next).Length);
            Assert.AreEqual(1024, new FileInfo(previous).Length);
        }

        [Test]
        public void OpenFailure_DoesNotLatchAndDisposeDoesNotRecurse()
        {
            var directory = Path.Combine(_root, "blocked");
            File.WriteAllText(directory, "directory unavailable");
            using var sink = new LoggerFailureFile(directory, 1, () => _now);
            Assert.DoesNotThrow(() => sink.Write("rolling-json", _error));
            File.Delete(directory);
            sink.Write("rolling-json", _error);
            var path = Directory.GetFiles(directory).Single();
            Assert.AreEqual(1, File.ReadAllLines(path).Length);
            sink.Dispose();
            sink.Write("rolling-json", _error);
            Assert.AreEqual(1, File.ReadAllLines(path).Length);
        }

        [Test]
        public void ConcurrentProviders_WriteWholeBoundedLinesWithoutExceptionDetails()
        {
            using (var sink = NewSink())
            {
                using var start = new ManualResetEventSlim();
                var workers = new[] { "rolling-json", "realtime-json" }.Select(provider => Task.Run(() =>
                {
                    start.Wait();
                    for (var i = 0; i < 50; i++) sink.Write(provider, _error);
                })).ToArray();
                start.Set();
                Task.WhenAll(workers).GetAwaiter().GetResult();
            }
            var files = Directory.GetFiles(_root);
            var lines = files.SelectMany(File.ReadAllLines).ToArray();
            Assert.AreEqual(100, lines.Length);
            Assert.AreEqual(50, lines.Count(l => l.Contains("|rolling-json|")));
            foreach (var path in files)
            {
                var bytes = File.ReadAllBytes(path);
                Assert.LessOrEqual(bytes.Length, 1024);
                Assert.AreNotEqual(0xef, bytes[0], "UTF-8 BOM を付けない。");
                Assert.AreEqual((byte)'\n', bytes[bytes.Length - 1]);
            }
            foreach (var line in lines)
            {
                var fields = line.Split('|');
                Assert.AreEqual(4, fields.Length);
                Assert.AreEqual(TimeSpan.Zero, DateTimeOffset.Parse(fields[0]).Offset);
                Assert.AreEqual("provider-write-error", fields[2]);
                Assert.AreEqual(typeof(InvalidOperationException).FullName, fields[3]);
                Assert.LessOrEqual(Encoding.UTF8.GetByteCount(line + "\n"), 512);
                StringAssert.DoesNotContain("secret", line);
            }
        }

        private LoggerFailureFile NewSink() => new LoggerFailureFile(_root, 1, () => _now);
    }
}
