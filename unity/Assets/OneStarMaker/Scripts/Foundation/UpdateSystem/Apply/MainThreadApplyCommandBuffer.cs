using System;
using System.Collections.Concurrent;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// main thread apply stage 専用の command buffer。
    /// ここへ積まれた command だけが Unity main thread で反映されるため、
    /// job 側から Unity API が漏れない境界として扱う。
    /// </summary>
    public sealed class MainThreadApplyCommandBuffer
    {
        private readonly ConcurrentQueue<IMainThreadApplyCommand> _commands = new();

        public int Count => _commands.Count;

        public void Enqueue(IMainThreadApplyCommand command)
        {
            _commands.Enqueue(command ?? throw new ArgumentNullException(nameof(command)));
        }

        public int ApplyAll()
        {
            var appliedCount = 0;
            while (_commands.TryDequeue(out var command))
            {
                command.Apply();
                appliedCount++;
            }

            return appliedCount;
        }
    }
}
