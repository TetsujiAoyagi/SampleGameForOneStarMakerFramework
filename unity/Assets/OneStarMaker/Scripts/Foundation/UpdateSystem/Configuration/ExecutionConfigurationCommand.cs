using System;

namespace OneStarMaker.Foundation.UpdateSystem.Configuration
{
    /// <summary>
    /// register、unregister、reorder を owner thread へ渡す実行構成変更コマンド。
    /// </summary>
    public readonly struct ExecutionConfigurationCommand
    {
        public ExecutionConfigurationCommand(
            ExecutionConfigurationCommandKind kind,
            UpdateHandle handle,
            string? layerId = null,
            int layerOrder = 0,
            int executionOrder = 0)
        {
            if (kind == ExecutionConfigurationCommandKind.Register && string.IsNullOrWhiteSpace(layerId))
            {
                throw new ArgumentException("A valid layerId is required for Register commands.", nameof(layerId));
            }

            if (!handle.IsValid)
            {
                throw new ArgumentException("A valid handle is required.", nameof(handle));
            }

            Kind = kind;
            Handle = handle;
            LayerId = layerId;
            LayerOrder = layerOrder;
            ExecutionOrder = executionOrder;
        }

        public ExecutionConfigurationCommandKind Kind { get; }
        public UpdateHandle Handle { get; }
        public string? LayerId { get; }
        public int LayerOrder { get; }
        public int ExecutionOrder { get; }
    }
}
