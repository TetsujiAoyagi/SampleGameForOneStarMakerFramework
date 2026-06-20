using System;
using System.Collections.Generic;
using OneStarMaker.Foundation.UpdateSystem.Layers;

namespace OneStarMaker.Foundation.UpdateSystem.Configuration
{
    /// <summary>
    /// 実行構成変更コマンドを live data へ配布する owner thread 専用 dispatcher。
    /// </summary>
    internal sealed class ExecutionConfigurationDispatcher
    {
        private readonly ExecutionConfigurationQueue _queue;
        private readonly List<ExecutionConfigurationCommand> _drainedCommands = new();

        public ExecutionConfigurationDispatcher(ExecutionConfigurationQueue queue)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        public void Apply(
            UpdateElementRegistry elementRegistry,
            List<UpdateLayer> orderedLayers,
            NativePipelineCatalog nativePipelines,
            Func<string, int, UpdateLayer> getOrCreateLayer,
            Action<UpdateHandle> removeElementIfDetached)
        {
            if (elementRegistry == null)
            {
                throw new ArgumentNullException(nameof(elementRegistry));
            }

            if (orderedLayers == null)
            {
                throw new ArgumentNullException(nameof(orderedLayers));
            }

            if (nativePipelines == null)
            {
                throw new ArgumentNullException(nameof(nativePipelines));
            }

            if (getOrCreateLayer == null)
            {
                throw new ArgumentNullException(nameof(getOrCreateLayer));
            }

            if (removeElementIfDetached == null)
            {
                throw new ArgumentNullException(nameof(removeElementIfDetached));
            }

            _drainedCommands.Clear();
            _queue.DrainTo(_drainedCommands);

            for (var i = 0; i < _drainedCommands.Count; i++)
            {
                var command = _drainedCommands[i];
                switch (command.Kind)
                {
                    case ExecutionConfigurationCommandKind.Register:
                        if (command.LayerId == null || !elementRegistry.Contains(command.Handle))
                        {
                            break;
                        }

                        getOrCreateLayer(command.LayerId, command.LayerOrder)
                            .Register(command.Handle, command.ExecutionOrder);
                        break;

                    case ExecutionConfigurationCommandKind.Unregister:
                        for (var layerIndex = 0; layerIndex < orderedLayers.Count; layerIndex++)
                        {
                            orderedLayers[layerIndex].Unregister(command.Handle);
                        }

                        nativePipelines.DetachElement(command.Handle);
                        removeElementIfDetached(command.Handle);
                        break;

                    case ExecutionConfigurationCommandKind.Reorder:
                        for (var layerIndex = 0; layerIndex < orderedLayers.Count; layerIndex++)
                        {
                            orderedLayers[layerIndex].TryReorder(command.Handle, command.ExecutionOrder);
                        }

                        nativePipelines.Reorder(command.Handle, command.ExecutionOrder);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            _drainedCommands.Clear();
        }
    }
}
