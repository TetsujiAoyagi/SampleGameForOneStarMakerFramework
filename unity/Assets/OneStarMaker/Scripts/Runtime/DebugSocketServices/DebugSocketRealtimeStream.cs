#nullable enable

using System;
using System.Buffers;
using System.IO;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    /// <summary>
    /// logging infrastructure へ渡す realtime stream。
    ///
    /// <para>
    /// ここは「本物のネットワーク Stream」ではなく、
    /// logger が書いた 1 フレームを service の送信キューへ橋渡しするための adapter。
    /// これにより logger 側は Stream だけ知っていればよく、
    /// WebSocket の接続切替・単一クライアント制御は service 側へ閉じ込められる。
    /// </para>
    /// </summary>
    internal sealed class DebugSocketRealtimeStream : Stream
    {
        private readonly DebugSocketService _owner;
        private bool _disposed;

        public DebugSocketRealtimeStream(DebugSocketService owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => !_disposed;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            // 実際の送信順制御は service 側の queue / send loop が担う。
            // ここでは即時 flush の概念を持たせない。
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            if (count <= 0)
            {
                return;
            }

            // realtime log は頻度が高いため、ここで毎回 new byte[count] すると
            // DebugSocket 有効時の GC 圧が無視できなくなる。
            // ArrayPool から借りたバッファへ詰め替え、service / session 側で送信完了後に返す。
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(count);
            Buffer.BlockCopy(buffer, offset, rentedBuffer, 0, count);
            _owner.EnqueueRealtimeLogFrame(rentedBuffer, count);
        }

        protected override void Dispose(bool disposing)
        {
            _disposed = true;
            base.Dispose(disposing);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DebugSocketRealtimeStream));
            }
        }
    }
}
