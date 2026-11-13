#nullable enable

using System;
using System.Buffers;
using ZLogger;

namespace OneStarMaker.Foundation.Logging
{
    /// <summary>変換が完了するまで provider へ断片を渡さない、entry 単位の確定境界。</summary>
    internal sealed class BufferedJsonFormatter : IZLoggerFormatter
    {
        private readonly IZLoggerFormatter _inner;
        private readonly EntryBuffer _buffer = new EntryBuffer();

        internal BufferedJsonFormatter(IZLoggerFormatter inner) => _inner = inner;
        public bool WithLineBreak => _inner.WithLineBreak;
        internal int RetainedCapacity => _buffer.Capacity;

        public void FormatLogEntry(IBufferWriter<byte> writer, IZLoggerEntry entry)
        {
            try
            {
                // Utf8JsonWriter は変換の途中でも Advance する。外側へ直接渡すと、
                // 後続の変換失敗時に既に出た断片を取り消せず、次の JSON 行を壊す。
                _inner.FormatLogEntry(_buffer, entry);
                var destination = writer.GetSpan(_buffer.WrittenCount);
                _buffer.WrittenSpan.CopyTo(destination);
                writer.Advance(_buffer.WrittenCount);
                // 改行は provider が WithLineBreak を見て一度だけ書く。
            }
            finally
            {
                // 変換例外は握り潰さず provider の InternalErrorLogger へ届かせる。
                // 外側へのコピー中の I/O 失敗は巻き戻せないが、次 entry へは持ち越さない。
                _buffer.Reset();
            }
        }

        /// <summary>一つの formatter だけが所有する配列。provider 間では共有しない。</summary>
        internal sealed class EntryBuffer : IBufferWriter<byte>
        {
            internal const int RetentionTarget = 1024 * 1024;
            private byte[] _data = Array.Empty<byte>();
            internal int WrittenCount { get; private set; }
            internal int Capacity => _data.Length;
            internal ReadOnlySpan<byte> WrittenSpan => _data.AsSpan(0, WrittenCount);

            public void Advance(int count)
            {
                if (count < 0 || count > _data.Length - WrittenCount)
                    throw new ArgumentOutOfRangeException(nameof(count));
                WrittenCount += count;
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                EnsureCapacity(sizeHint);
                return _data.AsMemory(WrittenCount);
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                EnsureCapacity(sizeHint);
                return _data.AsSpan(WrittenCount);
            }

            internal void Reset()
            {
                WrittenCount = 0;
                // 1 MiB は entry の拒否上限ではなく、処理後のキャッシュ保持目安。
                // 通常配列の参照を外すだけで、総メモリ上限や即時 GC は保証しない。
                if (_data.Length > RetentionTarget) _data = Array.Empty<byte>();
            }

            private void EnsureCapacity(int sizeHint)
            {
                if (sizeHint < 0) throw new ArgumentOutOfRangeException(nameof(sizeHint));
                var required = checked(WrittenCount + Math.Max(1, sizeHint));
                if (required <= _data.Length) return;
                // 必要量は必ず受ける。倍増だけの overflow で有効な要求を拒否しない。
                var grown = Math.Max((long)required, Math.Max(256L, (long)_data.Length * 2));
                var next = new byte[(int)Math.Min(int.MaxValue, grown)];
                _data.AsSpan(0, WrittenCount).CopyTo(next);
                _data = next;
            }
        }
    }
}
