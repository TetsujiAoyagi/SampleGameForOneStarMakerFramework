#nullable enable

using System;
using System.Buffers;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    /// <summary>
    /// 送信キューに積む 1 フレーム分の所有権を表す軽量 DTO。
    ///
    /// <para>
    /// realtime log は <see cref="ArrayPool{T}.Shared"/> から借りたバッファをそのまま載せ、
    /// protocol helper が返す通常配列は owned 配列として扱う。
    /// 所有権の移転点は enqueue 時であり、受け取った側が
    /// drop・overflow・送信完了・close のいずれかで必ず一度だけ <see cref="Release"/> する。
    /// </para>
    /// </summary>
    internal readonly struct DebugSocketOutgoingFrame
    {
        public readonly byte[]? Buffer;
        public readonly int Count;
        public readonly bool ReturnToPool;

        private DebugSocketOutgoingFrame(byte[]? buffer, int count, bool returnToPool)
        {
            Buffer = buffer;
            Count = count;
            ReturnToPool = returnToPool;
        }

        public bool IsEmpty => Buffer == null || Count <= 0;

        public static DebugSocketOutgoingFrame CreateOwned(byte[] buffer)
        {
            return new DebugSocketOutgoingFrame(buffer, buffer?.Length ?? 0, returnToPool: false);
        }

        public static DebugSocketOutgoingFrame CreatePooled(byte[] buffer, int count)
        {
            return new DebugSocketOutgoingFrame(buffer, count, returnToPool: true);
        }

        public ArraySegment<byte> AsSegment()
        {
            return new ArraySegment<byte>(Buffer!, 0, Count);
        }

        public void Release()
        {
            if (ReturnToPool && Buffer != null)
            {
                ArrayPool<byte>.Shared.Return(Buffer);
            }
        }
    }
}
