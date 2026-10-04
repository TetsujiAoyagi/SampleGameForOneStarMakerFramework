#nullable enable
using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using OneStarMaker.Editor.TestObservation;

namespace OneStarMaker.Tests.Editor.TestObservation
{
    public sealed class ObservationAtomicWriteTests
    {
        private string directory = "";
        private string path = "";
        private static readonly byte[] Old = Encoding.UTF8.GetBytes("old complete observation\n");
        private static readonly byte[] New = Encoding.UTF8.GetBytes("new complete observation\n");
        private const int Sharing = unchecked((int)0x80070020);

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "observation-progress.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void NewPathMovesOnceAndExistingPathReplacesCompleteBytes()
        {
            TestObservationWriter.AtomicWrite(path, New);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(New));
            AssertNoTemp();
            File.WriteAllBytes(path, Old);
            TestObservationWriter.AtomicWrite(path, New);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(New));
            AssertNoTemp();
        }

        [Test]
        public void RealHeldReaderReleasesAtRetryBoundaryAndPublishesSameTemp()
        {
            RequireWindows();
            File.WriteAllBytes(path, Old);
            using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                FileStream? held = reader;
                var attempts = 0;
                string? firstTemp = null;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    TestObservationWriter.AtomicWrite(path, New, (temp, destination) =>
                    {
                        attempts++;
                        if (firstTemp == null) firstTemp = temp;
                        Assert.That(temp, Is.EqualTo(firstTemp));
                        Assert.That(File.ReadAllBytes(temp), Is.EqualTo(New));
                        try { File.Replace(temp, destination, null); }
                        catch (IOException error)
                        {
                            TestContext.Out.WriteLine("real replace attempt={0} type={1} hresult=0x{2:X8} elapsedMs={3} runtime={4} fileMvid={5}",
                                attempts, error.GetType().FullName, error.HResult, clock.ElapsedMilliseconds,
                                Environment.Version, typeof(File).Module.ModuleVersionId);
                            throw;
                        }
                    }, milliseconds =>
                    {
                        Assert.That(milliseconds, Is.EqualTo(20));
                        Assert.That(attempts, Is.EqualTo(1));
                        Assert.That(ReadHeld(reader), Is.EqualTo(Old));
                        held!.Dispose();
                        held = null;
                    }, null, File.Delete);
                }
                finally { held?.Dispose(); }
                TestContext.Out.WriteLine("real replacement completed attempts={0} elapsedMs={1}",
                    attempts, clock.ElapsedMilliseconds);
                Assert.That(attempts, Is.EqualTo(2));
            }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(New));
            AssertNoTemp();
        }

        [Test]
        public void RealPersistentReaderFailsWithinFiveAttemptsAndKeepsOldBytes()
        {
            RequireWindows();
            File.WriteAllBytes(path, Old);
            using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var attempts = 0;
                var waits = 0;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var error = Assert.Throws<IOException>(() =>
                    TestObservationWriter.AtomicWrite(path, New, (temp, destination) =>
                    {
                        attempts++;
                        Assert.That(File.ReadAllBytes(temp), Is.EqualTo(New));
                        try { File.Replace(temp, destination, null); }
                        catch (IOException failure)
                        {
                            TestContext.Out.WriteLine("persistent attempt={0} type={1} hresult=0x{2:X8} elapsedMs={3} runtime={4} fileMvid={5}",
                                attempts, failure.GetType().FullName, failure.HResult, clock.ElapsedMilliseconds,
                                Environment.Version, typeof(File).Module.ModuleVersionId);
                            throw;
                        }
                    }, _ => waits++, () => 0, File.Delete));
                Assert.That(error, Is.Not.Null);
                TestContext.Out.WriteLine("persistent replacement completed attempts={0} waits={1} elapsedMs={2}",
                    attempts, waits, clock.ElapsedMilliseconds);
                Assert.That(attempts, Is.EqualTo(5));
                Assert.That(waits, Is.EqualTo(4));
                Assert.That(ReadHeld(reader), Is.EqualTo(Old));
            }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(Old));
            AssertNoTemp();
        }

        [Test]
        public void DeleteSharingReaderSeesCompleteOldBytesWhileNewPathAppears()
        {
            RequireWindows();
            File.WriteAllBytes(path, Old);
            using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            {
                TestObservationWriter.AtomicWrite(path, New);
                Assert.That(ReadHeld(reader), Is.EqualTo(Old));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(New));
            }
            AssertNoTemp();
        }

        [TestCase(unchecked((int)0x80070020))]
        [TestCase(unchecked((int)0x80070021))]
        [TestCase(unchecked((int)0x80070497))]
        public void KnownReplacementCodesRetryOnlyOnWindows(int hresult)
        {
            RequireWindows();
            File.WriteAllBytes(path, Old);
            var attempts = 0;
            TestObservationWriter.AtomicWrite(path, New, (temp, destination) =>
            {
                attempts++;
                if (attempts == 1) throw new IOException("injected", hresult);
                File.Replace(temp, destination, null);
            }, _ => { }, () => 0, File.Delete);
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(New));
            AssertNoTemp();
        }

        [TestCase(unchecked((int)0x80070498))]
        [TestCase(unchecked((int)0x80070499))]
        [TestCase(unchecked((int)0x80070005))]
        [TestCase(unchecked((int)0x80070070))]
        [TestCase(unchecked((int)0x80070001))]
        [TestCase(unchecked((int)0x80131620))]
        public void UnknownPartialAndPermanentCodesFailImmediately(int hresult)
        {
            File.WriteAllBytes(path, Old);
            var sentinel = new IOException("injected", hresult);
            var attempts = 0;
            var actual = Assert.Throws<IOException>(() =>
                TestObservationWriter.AtomicWrite(path, New, (_, _) =>
                {
                    attempts++;
                    throw sentinel;
                }, _ => Assert.Fail("unexpected wait"), () => 0, File.Delete));
            Assert.That(actual, Is.SameAs(sentinel));
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(Old));
            AssertNoTemp();
        }

        [TestCase(99, 100, 1)]
        [TestCase(100, 100, 0)]
        [TestCase(0, 100, 1)]
        public void DeadlineIsCheckedAfterFailureAndAfterWait(int afterFailure, int afterWait, int expectedWaits)
        {
            RequireWindows();
            File.WriteAllBytes(path, Old);
            var reads = 0;
            var attempts = 0;
            var waits = 0;
            Assert.Throws<IOException>(() => TestObservationWriter.AtomicWrite(path, New,
                (_, _) => { attempts++; throw new IOException("sharing", Sharing); },
                _ => waits++, () => ++reads == 1 ? afterFailure : afterWait, File.Delete));
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(waits, Is.EqualTo(expectedWaits));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(Old));
            AssertNoTemp();
        }

        [Test]
        public void InjectedSharingFailureStopsAfterFiveAttemptsWithOneTemp()
        {
            RequireWindows();
            File.WriteAllBytes(path, Old);
            var attempts = 0;
            var waits = 0;
            string? firstTemp = null;
            var error = new IOException("persistent sharing", Sharing);
            var actual = Assert.Throws<IOException>(() => TestObservationWriter.AtomicWrite(path, New,
                (temp, _) =>
                {
                    attempts++;
                    if (firstTemp == null) firstTemp = temp;
                    Assert.That(temp, Is.EqualTo(firstTemp));
                    Assert.That(File.ReadAllBytes(temp), Is.EqualTo(New));
                    throw error;
                }, _ => waits++, () => 0, File.Delete));
            Assert.That(actual, Is.SameAs(error));
            Assert.That(attempts, Is.EqualTo(5));
            Assert.That(waits, Is.EqualTo(4));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(Old));
            AssertNoTemp();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ConsumedTempOrMissingDestinationStopsRetry(bool consumeTemp)
        {
            RequireWindows();
            File.WriteAllBytes(path, Old);
            var attempts = 0;
            Assert.Throws<IOException>(() => TestObservationWriter.AtomicWrite(path, New,
                (temp, destination) =>
                {
                    attempts++;
                    if (consumeTemp) File.Delete(temp);
                    else File.Delete(destination);
                    throw new IOException("sharing after partial change", Sharing);
                }, _ => Assert.Fail("unexpected wait"), () => 0, File.Delete));
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(File.Exists(path), Is.EqualTo(consumeTemp));
            AssertNoTemp();
        }

        [Test]
        public void CleanupFailurePreservesPublicationFailureAndLeavesVisibleTemp()
        {
            File.WriteAllBytes(path, Old);
            var publication = new IOException("publication sentinel");
            var cleanup = new IOException("cleanup sentinel");
            var actual = Assert.Throws<AggregateException>(() => TestObservationWriter.AtomicWrite(path, New,
                (_, _) => throw publication, _ => Assert.Fail("unexpected wait"), () => 0, _ => throw cleanup));
            Assert.That(actual!.InnerExceptions[0], Is.SameAs(publication));
            Assert.That(actual.InnerExceptions[1], Is.SameAs(cleanup));
            Assert.That(Directory.GetFiles(directory, "*.tmp").Length, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(Old));
        }

        [Test]
        public void CleanupFailureAfterReportedPublicationSuccessIsStillFailure()
        {
            File.WriteAllBytes(path, Old);
            var cleanup = new IOException("cleanup sentinel");
            // Simulate a published destination with an unexpectedly retained temp so cleanup runs.
            var actual = Assert.Throws<IOException>(() => TestObservationWriter.AtomicWrite(path, New,
                (temp, destination) => File.Copy(temp, destination, true),
                _ => { }, () => 0, _ => throw cleanup));
            Assert.That(actual, Is.SameAs(cleanup));
            Assert.That(Directory.GetFiles(directory, "*.tmp").Length, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(New));
        }

        private void AssertNoTemp() =>
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);

        private static byte[] ReadHeld(FileStream stream)
        {
            stream.Position = 0;
            var bytes = new byte[checked((int)stream.Length)];
            var offset = 0;
            while (offset < bytes.Length)
            {
                var read = stream.Read(bytes, offset, bytes.Length - offset);
                if (read == 0) break;
                offset += read;
            }
            Assert.That(offset, Is.EqualTo(bytes.Length));
            return bytes;
        }

        private static void RequireWindows() =>
            Assert.That(Environment.OSVersion.Platform, Is.EqualTo(PlatformID.Win32NT));
    }
}
