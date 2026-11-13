#nullable enable

using System;
using System.Buffers;
using NUnit.Framework;
using OneStarMaker.Foundation.Logging;
using ZLogger;

namespace OneStarMaker.Tests.Foundation
{
    [TestFixture]
    public sealed class BufferedJsonFormatterTests
    {
        [Test]
        public void FailureAfterLargePrefix_EmitsZeroBytesAndResetsForNextEntry()
        {
            var inner = new StagingFormatter { Fail = true, Bytes = 70000 };
            var formatter = new BufferedJsonFormatter(inner);
            var external = new ArrayBufferWriter<byte>();
            Assert.Throws<InvalidOperationException>(() => formatter.FormatLogEntry(external, null!));
            Assert.AreEqual(0, external.WrittenCount);
            Assert.IsTrue(formatter.WithLineBreak);
            inner.Fail = false;
            inner.Bytes = 3;
            formatter.FormatLogEntry(external, null!);
            CollectionAssert.AreEqual(new byte[] { 7, 7, 7 }, external.WrittenSpan.ToArray());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LargeRequest_IsAcceptedThenReleasedOnSuccessOrFailure(bool fail)
        {
            var inner = new StagingFormatter { Fail = fail, Bytes = 1200000 };
            var formatter = new BufferedJsonFormatter(inner);
            var external = new ArrayBufferWriter<byte>();
            if (fail) Assert.Throws<InvalidOperationException>(() => formatter.FormatLogEntry(external, null!));
            else formatter.FormatLogEntry(external, null!);
            Assert.AreEqual(fail ? 0 : 1200000, external.WrittenCount);
            Assert.LessOrEqual(formatter.RetainedCapacity, BufferedJsonFormatter.EntryBuffer.RetentionTarget);
            inner.Fail = false;
            inner.Bytes = 5;
            formatter.FormatLogEntry(external, null!);
            Assert.AreEqual((fail ? 0 : 1200000) + 5, external.WrittenCount);
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

        private sealed class StagingFormatter : IZLoggerFormatter
        {
            internal bool Fail;
            internal int Bytes;
            public bool WithLineBreak => true;
            public void FormatLogEntry(IBufferWriter<byte> writer, IZLoggerEntry entry)
            {
                // 偽 entry は使わず、内側が既に多量の Advance を済ませた状態を直接作る。
                writer.GetSpan(Bytes).Slice(0, Bytes).Fill(7);
                writer.Advance(Bytes);
                if (Fail) throw new InvalidOperationException();
            }
        }
    }
}
