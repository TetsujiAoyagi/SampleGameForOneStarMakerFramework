using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// native/job path から publish された dirty handle を main thread apply stage へ渡すバッファ。
    /// command buffer と分離しておくことで、
    /// 「任意 command を積む経路」と「handle から mirror を解決する標準経路」を責務として切り分ける。
    /// </summary>
    public sealed class MainThreadApplyHandleBuffer
    {
        private readonly ConcurrentQueue<UpdateHandle> _handles = new();

        public int Count => _handles.Count;

        public void Enqueue(UpdateHandle handle)
        {
            if (!handle.IsValid)
            {
                throw new ArgumentException("A valid handle is required.", nameof(handle));
            }

            _handles.Enqueue(handle);
        }

        public int DrainTo(List<UpdateHandle> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            var drained = 0;
            while (_handles.TryDequeue(out var handle))
            {
                buffer.Add(handle);
                drained++;
            }

            return drained;
        }
    }
}
