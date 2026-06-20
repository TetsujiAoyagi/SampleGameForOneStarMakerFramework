using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OneStarMaker.Foundation.UpdateSystem.Configuration
{
    /// <summary>
    /// 実行構成変更コマンドのキュー。
    /// owner thread へ register、unregister、reorder を集約する。
    /// </summary>
    public class ExecutionConfigurationQueue
    {
        private readonly ConcurrentQueue<ExecutionConfigurationCommand> _commands = new();

        public void Enqueue(ExecutionConfigurationCommand command)
        {
            switch (command.Kind)
            {
                case ExecutionConfigurationCommandKind.Register:
                    if (string.IsNullOrWhiteSpace(command.LayerId))
                    {
                        throw new ArgumentException("Register commands require a valid layerId.", nameof(command));
                    }

                    _commands.Enqueue(command);
                    break;

                case ExecutionConfigurationCommandKind.Unregister:
                case ExecutionConfigurationCommandKind.Reorder:
                    _commands.Enqueue(command);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(command.Kind), command.Kind, "Invalid execution configuration command kind.");
            }
        }

        public void EnqueueRegister(string layerId, int layerOrder, UpdateHandle handle, int executionOrder)
        {
            _commands.Enqueue(new ExecutionConfigurationCommand(
                ExecutionConfigurationCommandKind.Register,
                handle,
                layerId,
                layerOrder,
                executionOrder));
        }

        public void EnqueueUnregister(UpdateHandle handle)
        {
            _commands.Enqueue(new ExecutionConfigurationCommand(ExecutionConfigurationCommandKind.Unregister, handle));
        }

        public void EnqueueReorder(UpdateHandle handle, int executionOrder)
        {
            _commands.Enqueue(new ExecutionConfigurationCommand(
                ExecutionConfigurationCommandKind.Reorder,
                handle,
                executionOrder: executionOrder));
        }

        public int DrainTo(List<ExecutionConfigurationCommand> buffer)
        {
            var drained = 0;
            while (_commands.TryDequeue(out var command))
            {
                buffer.Add(command);
                drained++;
            }

            return drained;
        }
    }
}
