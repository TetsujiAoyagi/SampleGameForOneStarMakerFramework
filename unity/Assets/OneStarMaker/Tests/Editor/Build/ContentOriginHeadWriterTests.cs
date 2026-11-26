#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// head の書き手（AC3・AC7）。置き換え・待ち・時計は注入し、Win32 の ReplaceFile の部分失敗（head を backup へ移したまま失敗）と、
    /// head が無い時の最初の書き込みが並ぶ場合を作る。
    /// </summary>
    public sealed class ContentOriginHeadWriterTests
    {
        private string _root = "";
        private string _headPath = "";

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "osm-ohw", Guid.NewGuid().ToString("N").Substring(0, 10));
            _headPath = Path.Combine(_root, ".origin", "set", "StandaloneWindows64-Player.json");
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private string Publish(string revision)
        {
            var directory = Path.Combine(_root, revision);
            Directory.CreateDirectory(Path.Combine(directory, "content"));
            var manifest = new ContentTransportManifest
            {
                version = 1, product = "OneStarMaker", contentSet = "set", revision = revision, target = "StandaloneWindows64-Player",
                unityVersion = "6000.6.9f1", rootSchemaVersion = 2, playerConfigSchemaVersion = 1,
                files = new[] { new ContentTransportFile { path = "payload.bin", size = 1, sha256 = new string('0', 64) } },
            };
            File.WriteAllText(Path.Combine(directory, "transport.json"), JsonUtility.ToJson(manifest));
            return directory;
        }

        private static string Digest(string published) => ContentDeliveryFiles.Sha256(File.ReadAllBytes(Path.Combine(published, "transport.json")));

        private ContentOriginHeadFile ReadHead() => ContentOriginHead.Read(File.ReadAllBytes(_headPath), "set", "StandaloneWindows64-Player").Head!;

        private string[] Leftovers() => Directory.GetFiles(Path.GetDirectoryName(_headPath)!).Where(x => x != _headPath).ToArray();

        private static ContentOriginHeadWriter.Operations Ops(Action<string, string, string>? replace = null, Action<string, string>? move = null,
            Action<int>? wait = null, Func<long>? elapsed = null) =>
            new(replace ?? ((s, d, b) => File.Replace(s, d, b, true)), move ?? File.Move, wait ?? (_ => { }), elapsed ?? (() => 0), windows: true);

        [Test]
        public void FirstWrite_CreatesTheHeadPointingAtThePublish()
        {
            var published = Publish("r1");
            var result = ContentOriginHeadWriter.Write(published);
            Assert.That(result.HeadPath, Is.EqualTo(_headPath));
            Assert.That(ReadHead().revision, Is.EqualTo("r1"));
            Assert.That(ReadHead().manifestSha256, Is.EqualTo(Digest(published)));
            Assert.That(ReadHead().path, Is.EqualTo("r1"));
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(Leftovers(), Is.Empty);
        }

        [Test]
        public void SecondWrite_ReplacesTheHead()
        {
            ContentOriginHeadWriter.Write(Publish("r1"));
            ContentOriginHeadWriter.Write(Publish("r2"));
            Assert.That(ReadHead().revision, Is.EqualTo("r2"));
            Assert.That(Leftovers(), Is.Empty);
        }

        [TestCase("{\"version\":2}")]
        [TestCase("{ broken")]
        public void UnsupportedOrBrokenExistingHead_IsLeftUnchanged(string existing)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_headPath)!);
            File.WriteAllText(_headPath, existing);
            Assert.Throws<ContentOriginHeadWriteException>(() => ContentOriginHeadWriter.Write(Publish("r1")));
            Assert.That(File.ReadAllText(_headPath), Is.EqualTo(existing));
            Assert.That(Leftovers(), Is.Empty);
        }

        [Test]
        public void ReplaceFails_KeepsThePreviousHeadBytes()
        {
            ContentOriginHeadWriter.Write(Publish("r1"));
            var before = File.ReadAllBytes(_headPath);
            Assert.Throws<ContentOriginHeadWriteException>(() =>
                ContentOriginHeadWriter.Write(Publish("r2"), Ops(replace: (_, _, _) => throw new IOException("fixture replace failure"))));
            Assert.That(File.ReadAllBytes(_headPath), Is.EqualTo(before));
            Assert.That(Leftovers(), Is.Empty);
        }

        [Test]
        public void ReplaceMovesTheHeadToBackupThenFails_TheBackupIsRestored()
        {
            ContentOriginHeadWriter.Write(Publish("r1"));
            var before = File.ReadAllBytes(_headPath);
            var error = Assert.Throws<ContentOriginHeadWriteException>(() => ContentOriginHeadWriter.Write(Publish("r2"),
                Ops(replace: (_, destination, backup) => { File.Move(destination, backup); throw new IOException("ERROR_UNABLE_TO_MOVE_REPLACEMENT_2"); })));
            Assert.That(File.ReadAllBytes(_headPath), Is.EqualTo(before));
            Assert.That(error!.Diagnostics, Is.Empty);
            Assert.That(Leftovers(), Is.Empty);
        }

        [Test]
        public void RestoreAlsoFails_TheHeadIsMissingAndTheBackupPathIsDiagnosed()
        {
            ContentOriginHeadWriter.Write(Publish("r1"));
            var before = File.ReadAllBytes(_headPath);
            var error = Assert.Throws<ContentOriginHeadWriteException>(() => ContentOriginHeadWriter.Write(Publish("r2"), Ops(
                replace: (_, destination, backup) => { File.Move(destination, backup); throw new IOException("partial"); },
                move: (_, _) => throw new IOException("fixture restore failure"))));
            Assert.That(File.Exists(_headPath), Is.False, "壊れた head も、今回の publish を指す head も残らない");
            var backup = Leftovers().Single();
            Assert.That(File.ReadAllBytes(backup), Is.EqualTo(before));
            Assert.That(error!.Diagnostics.Single(), Does.Contain(backup));
        }

        [Test]
        public void PartialFailureWhileAnotherWriterPlacedAHead_DoesNotOverwriteIt()
        {
            ContentOriginHeadWriter.Write(Publish("r1"));
            var other = Publish("r3");
            Assert.Throws<ContentOriginHeadWriteException>(() => ContentOriginHeadWriter.Write(Publish("r2"), Ops(replace: (_, destination, backup) =>
            {
                File.Move(destination, backup);
                ContentOriginHeadWriter.Write(other); // 別の書き手が、head が無い間に書く
                throw new IOException("partial");
            })));
            Assert.That(ReadHead().revision, Is.EqualTo("r3"));
        }

        [Test]
        public void ConcurrentFirstWriters_TheLaterWriterReplaces()
        {
            var first = Publish("r1");
            var result = ContentOriginHeadWriter.Write(Publish("r2"), Ops(move: (source, destination) =>
            {
                if (destination == _headPath && !File.Exists(destination)) ContentOriginHeadWriter.Write(first);
                File.Move(source, destination);
            }));
            Assert.That(result.Head.revision, Is.EqualTo("r2"));
            Assert.That(ReadHead().revision, Is.EqualTo("r2"));
            Assert.That(Leftovers(), Is.Empty);
        }

        [Test]
        public void TransientFailure_IsRetriedWithinTheStagingMoveBudget()
        {
            ContentOriginHeadWriter.Write(Publish("r1"));
            var attempts = 0;
            var waits = 0;
            ContentOriginHeadWriter.Write(Publish("r2"), Ops(replace: (s, d, b) =>
            {
                if (++attempts < 3) throw new IOException("sharing violation", unchecked((int)0x80070020));
                File.Replace(s, d, b, true);
            }, wait: _ => waits++));
            Assert.That(attempts, Is.EqualTo(3));
            Assert.That(waits, Is.EqualTo(2));
            Assert.That(ReadHead().revision, Is.EqualTo("r2"));
        }

        [Test]
        public void TransientFailureBeyondTheBudget_Fails()
        {
            ContentOriginHeadWriter.Write(Publish("r1"));
            long clock = 0;
            Assert.Throws<ContentOriginHeadWriteException>(() => ContentOriginHeadWriter.Write(Publish("r2"), Ops(
                replace: (_, _, _) => throw new IOException("sharing violation", unchecked((int)0x80070020)),
                wait: ms => clock += ms, elapsed: () => clock)));
            Assert.That(ReadHead().revision, Is.EqualTo("r1"));
            Assert.That(clock, Is.LessThanOrEqualTo(ContentStagingMove.BudgetMilliseconds));
        }
    }
}
