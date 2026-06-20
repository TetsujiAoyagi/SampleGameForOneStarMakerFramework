using System;
using System.Collections.Generic;

namespace OneStarMaker.Foundation.UpdateSystem.Apply
{
    /// <summary>
    /// Update 結果を main thread へ反映する処理器。
    /// dirty handle 経路と command 経路をフレーム終端で直列化する。
    /// </summary>
    public class MainThreadApplyProcessor
    {
        private readonly List<UpdateHandle> _drainedHandles = new();
        private readonly HashSet<UpdateHandle> _coalescedHandles = new();

        public int Apply(UpdateElementRegistry elementRegistry, MainThreadApplyHandleBuffer handleBuffer, MainThreadApplyCommandBuffer commandBuffer)
        {
            if (elementRegistry == null)
            {
                throw new ArgumentNullException(nameof(elementRegistry));
            }

            if (handleBuffer == null)
            {
                throw new ArgumentNullException(nameof(handleBuffer));
            }

            if (commandBuffer == null)
            {
                throw new ArgumentNullException(nameof(commandBuffer));
            }

            var appliedCount = ApplyDirtyHandles(elementRegistry, handleBuffer);
            appliedCount += commandBuffer.ApplyAll();
            return appliedCount;
        }

        public int Apply(MainThreadApplyCommandBuffer commandBuffer)
        {
            if (commandBuffer == null)
            {
                throw new ArgumentNullException(nameof(commandBuffer));
            }

            return commandBuffer.ApplyAll();
        }

        private int ApplyDirtyHandles(UpdateElementRegistry elementRegistry, MainThreadApplyHandleBuffer handleBuffer)
        {
            _drainedHandles.Clear();
            _coalescedHandles.Clear();
            handleBuffer.DrainTo(_drainedHandles);

            var appliedCount = 0;
            for (var i = 0; i < _drainedHandles.Count; i++)
            {
                var handle = _drainedHandles[i];
                if (!_coalescedHandles.Add(handle))
                {
                    continue;
                }

                if (!elementRegistry.TryGetMainThreadApplyElement(handle, out var element))
                {
                    continue;
                }

                element.ApplyMainThread(new MainThreadApplyContext(handle));
                appliedCount++;
            }

            _drainedHandles.Clear();
            _coalescedHandles.Clear();
            return appliedCount;
        }
    }
}
