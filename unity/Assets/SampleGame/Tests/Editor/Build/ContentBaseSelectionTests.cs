#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using SampleGame.DependOnAll.Editor.Build;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>選んだ base の記録の読み書き（壊れた file、key の正規化、UTC の往復）と、取得元の場所の解決。</summary>
    public sealed class ContentBaseSelectionTests
    {
        private string _root = "";
        private string _path = "";

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "osm-bsel", Guid.NewGuid().ToString("N").Substring(0, 8));
            _path = Path.Combine(_root, "UserSettings", "OSMContentBaseSelection.json");
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private ContentBaseSelectionEntry Entry(string cacheRoot, string revision, DateTime selectedUtc) => new()
        {
            cacheRoot = cacheRoot, contentSet = "spring-full", target = VerifyPlayLauncher.Target, revision = revision,
            manifestSha256 = new string('a', 64), targetOrigin = "origin", bytesOrigin = "origin", digestAuthority = "Declared",
            selectedUtc = selectedUtc.ToString("O"),
        };

        [Test]
        public void MissingFile_HasNoEntryAndNoProblem()
        {
            var read = ContentBaseSelection.Read(_path, Path.Combine(_root, "cache"), "spring-full", VerifyPlayLauncher.Target);
            Assert.That(read.Entry, Is.Null);
            Assert.That(read.Problem, Is.Empty);
        }

        [Test]
        public void WriteThenRead_RoundTripsInUtc_AndTheKeyIsNormalized()
        {
            var cache = Path.Combine(_root, "Cache");
            var selected = new DateTime(2026, 11, 26, 3, 4, 5, DateTimeKind.Utc);
            ContentBaseSelection.Write(_path, Entry(cache + Path.DirectorySeparatorChar, "r1", selected));
            var read = ContentBaseSelection.Read(_path, cache.ToUpperInvariant(), "spring-full", VerifyPlayLauncher.Target);
            Assert.That(read.Problem, Is.Empty);
            Assert.That(read.Entry!.revision, Is.EqualTo("r1"));
            Assert.That(read.Entry.SelectedUtc, Is.EqualTo(selected));
            Assert.That(read.Entry.SelectedUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public void EntriesAreKeptPerCacheRoot_AndTheSameKeyIsReplaced()
        {
            var a = Path.Combine(_root, "a");
            var b = Path.Combine(_root, "b");
            ContentBaseSelection.Write(_path, Entry(a, "r1", DateTime.UtcNow));
            ContentBaseSelection.Write(_path, Entry(b, "r2", DateTime.UtcNow));
            ContentBaseSelection.Write(_path, Entry(a, "r3", DateTime.UtcNow));
            Assert.That(ContentBaseSelection.Read(_path, a, "spring-full", VerifyPlayLauncher.Target).Entry!.revision, Is.EqualTo("r3"));
            Assert.That(ContentBaseSelection.Read(_path, b, "spring-full", VerifyPlayLauncher.Target).Entry!.revision, Is.EqualTo("r2"));
            Assert.That(ContentBaseSelection.Read(_path, a, "other", VerifyPlayLauncher.Target).Entry, Is.Null);
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(_path)!), Has.Length.EqualTo(1), "一時 file が残っている");
        }

        [TestCase("{ not json")]
        [TestCase("{\"version\":2,\"entries\":[]}")]
        [TestCase("{\"version\":1,\"entries\":[{\"cacheRoot\":\"C:/x\",\"contentSet\":\"spring-full\",\"target\":\"StandaloneWindows64-Player\",\"selectedUtc\":\"not a time\"}]}")]
        public void BrokenRecord_IsAProblemNotAnException(string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, json);
            var read = ContentBaseSelection.Read(_path, "C:/x", "spring-full", VerifyPlayLauncher.Target);
            Assert.That(read.Entry, Is.Null);
            Assert.That(read.Problem, Is.Not.Empty);
        }

        [Test]
        public void WriteOverABrokenRecord_Succeeds()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, "{ not json");
            var cache = Path.Combine(_root, "cache");
            ContentBaseSelection.Write(_path, Entry(cache, "r1", DateTime.UtcNow));
            Assert.That(ContentBaseSelection.Read(_path, cache, "spring-full", VerifyPlayLauncher.Target).Entry!.revision, Is.EqualTo("r1"));
        }

        [Test]
        public void OriginRoot_FallsBackToTheDefaultWhenTheFileIsMissingBrokenOrRelative()
        {
            var settings = Path.Combine(_root, "OSMContentOrigin.json");
            var fallback = Path.Combine(_root, "default");
            Assert.That(ContentOriginSettings.ResolveRoot(settings, fallback), Is.EqualTo((fallback, string.Empty)));
            File.WriteAllText(settings, "{ not json");
            Assert.That(ContentOriginSettings.ResolveRoot(settings, fallback).Root, Is.EqualTo(fallback));
            Assert.That(ContentOriginSettings.ResolveRoot(settings, fallback).Note, Is.Not.Empty);
            File.WriteAllText(settings, "{\"originRoot\":\"relative/path\"}");
            Assert.That(ContentOriginSettings.ResolveRoot(settings, fallback).Root, Is.EqualTo(fallback));
            var custom = Path.Combine(_root, "custom");
            File.WriteAllText(settings, UnityEngine.JsonUtility.ToJson(new ContentOriginSettings.Settings { originRoot = custom }));
            Assert.That(ContentOriginSettings.ResolveRoot(settings, fallback), Is.EqualTo((custom, string.Empty)));
        }
    }
}
