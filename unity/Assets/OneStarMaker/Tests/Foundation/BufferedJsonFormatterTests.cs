#nullable enable

using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OneStarMaker.Foundation.Logging;
using ZLogger;

namespace OneStarMaker.Tests.Foundation
{
    [TestFixture]
    public sealed class BufferedJsonFormatterTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void RepeatedFailures_EmitNothingAndResetEvenWhenNotificationThrows(bool callbackThrows)
        {
            var inner = new StagingFormatter { Fail = true, Bytes = 70000 };
            var notifications = 0;
            Exception? reported = null;
            var formatter = new BufferedJsonFormatter(inner, exception =>
            {
                reported = exception;
                notifications++;
                if (callbackThrows) throw new IOException("notice failure");
            });
            var external = new CountingWriter();
            for (var i = 0; i < 2; i++)
            {
                Assert.DoesNotThrow(() => formatter.FormatLogEntry(external, null!));
                Assert.AreEqual(i + 1, notifications);
                Assert.AreSame(inner.Failure, reported);
                Assert.AreEqual(0, external.SpanCalls + external.MemoryCalls + external.AdvanceCalls);
                Assert.AreEqual(0, external.Buffer.WrittenCount);
            }
            Assert.IsFalse(formatter.WithLineBreak);
            inner.Fail = false;
            inner.Bytes = 3;
            formatter.FormatLogEntry(external, null!);
            AssertBytes(external.Buffer, 3);
            Assert.AreEqual(2, notifications);
            Assert.AreEqual(1, external.SpanCalls);
            Assert.AreEqual(1, external.AdvanceCalls);
            Assert.AreEqual(0, external.MemoryCalls);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LargeRequest_IsAcceptedThenReleasedOnSuccessOrFailure(bool fail)
        {
            var inner = new StagingFormatter { Fail = fail, Bytes = 1200000 };
            var notifications = 0;
            var formatter = new BufferedJsonFormatter(inner, _ => notifications++);
            var external = new CountingWriter();
            formatter.FormatLogEntry(external, null!);
            Assert.AreEqual(fail ? 1 : 0, notifications);
            if (fail)
            {
                Assert.AreEqual(0, external.SpanCalls + external.MemoryCalls + external.AdvanceCalls);
                Assert.AreEqual(0, external.Buffer.WrittenCount);
            }
            else AssertBytes(external.Buffer, 1200000);
            Assert.LessOrEqual(formatter.RetainedCapacity, BufferedJsonFormatter.EntryBuffer.RetentionTarget);
            inner.Fail = false;
            inner.Bytes = 5;
            var previous = external.Buffer.WrittenCount;
            formatter.FormatLogEntry(external, null!);
            CollectionAssert.AreEqual(ExpectedBytes(5), external.Buffer.WrittenSpan.Slice(previous).ToArray());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExternalWriterFailure_PropagatesWithoutConversionNoticeAndResets(bool failAdvance)
        {
            var notifications = 0;
            var inner = new StagingFormatter { Bytes = 1200000 };
            var formatter = new BufferedJsonFormatter(inner, _ => notifications++);
            var external = new CountingWriter { FailSpan = !failAdvance, FailAdvance = failAdvance };
            Assert.AreSame(external.Failure, Assert.Throws<IOException>(() => formatter.FormatLogEntry(external, null!)));
            Assert.AreEqual(0, notifications);
            Assert.AreEqual(1, external.SpanCalls);
            Assert.AreEqual(failAdvance ? 1 : 0, external.AdvanceCalls);
            Assert.AreEqual(0, external.MemoryCalls);
            Assert.AreEqual(0, external.Buffer.WrittenCount);
            Assert.LessOrEqual(formatter.RetainedCapacity, BufferedJsonFormatter.EntryBuffer.RetentionTarget);
            inner.Bytes = 3;
            var next = new CountingWriter();
            formatter.FormatLogEntry(next, null!);
            AssertBytes(next.Buffer, 3);
        }

        [Test]
        public void Writer_GrowsPreservingBytesAndRejectsInvalidAdvanceOrOverflow()
        {
            var buffer = new BufferedJsonFormatter.EntryBuffer();
            buffer.GetMemory(1).Span[0] = 13;
            buffer.Advance(1);
            Assert.GreaterOrEqual(buffer.GetMemory(1200000).Length, 1200000);
            Assert.AreEqual(13, buffer.WrittenSpan[0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Advance(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Advance(buffer.Capacity));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetMemory(-1));
            Assert.Throws<OverflowException>(() => buffer.GetMemory(int.MaxValue));
            buffer.Reset();
            Assert.AreEqual(0, buffer.WrittenCount);
            Assert.AreEqual(0, buffer.Capacity);
            Assert.Greater(buffer.GetMemory().Length, 0);
        }

        private static byte[] ExpectedBytes(int count)
            => Enumerable.Repeat((byte)7, count).Concat(Encoding.UTF8.GetBytes(Environment.NewLine)).ToArray();
        private static void AssertBytes(ArrayBufferWriter<byte> writer, int count)
            => CollectionAssert.AreEqual(ExpectedBytes(count), writer.WrittenSpan.ToArray());

        private sealed class StagingFormatter : IZLoggerFormatter
        {
            internal bool Fail;
            internal int Bytes;
            internal readonly Exception Failure = new InvalidOperationException("probe failure");
            public bool WithLineBreak => true;
            public void FormatLogEntry(IBufferWriter<byte> writer, IZLoggerEntry entry)
            {
                // 内側が多量の Advance を済ませてから失敗する境界を作る。
                writer.GetSpan(Bytes).Slice(0, Bytes).Fill(7);
                writer.Advance(Bytes);
                if (Fail) throw Failure;
            }
        }

        private sealed class CountingWriter : IBufferWriter<byte>
        {
            internal readonly ArrayBufferWriter<byte> Buffer = new ArrayBufferWriter<byte>();
            internal readonly IOException Failure = new IOException("external writer failure");
            internal int SpanCalls, MemoryCalls, AdvanceCalls;
            internal bool FailSpan, FailAdvance;
            public Span<byte> GetSpan(int sizeHint = 0)
            {
                SpanCalls++;
                if (FailSpan) throw Failure;
                return Buffer.GetSpan(sizeHint);
            }
            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                MemoryCalls++;
                return Buffer.GetMemory(sizeHint);
            }
            public void Advance(int count)
            {
                AdvanceCalls++;
                if (FailAdvance) throw Failure;
                Buffer.Advance(count);
            }
        }
    }
}
