using System;
using System.Collections.Generic;
using OneStarMaker.Foundation.UpdateSystem.Apply;
using OneStarMaker.Foundation.UpdateSystem.Configuration;
using OneStarMaker.Foundation.UpdateSystem.Layers;

namespace OneStarMaker.Foundation.UpdateSystem.World
{
    /// <summary>
    /// Layer、native pipeline、main-thread apply、実行構成変更を束ねる更新基盤の中核。
    /// Scene と分離した順序制御を担い、各フェーズを deterministic な順序で連結する。
    /// </summary>
    public class UpdateCoordinator
    {
        private readonly IUpdateExecutionBackend _backend;
        private readonly Dictionary<string, UpdateLayer> _layersById = new(StringComparer.Ordinal);
        private readonly List<UpdateLayer> _orderedLayers = new();
        private readonly Dictionary<UpdateHandle, string> _layerIdsByHandle = new();
        private readonly UpdateElementRegistry _elementRegistry;
        private readonly ExecutionConfigurationQueue _executionConfigurationQueue;
        private readonly MainThreadApplyCommandBuffer _mainThreadApplyCommandBuffer;
        private readonly MainThreadApplyHandleBuffer _mainThreadApplyHandleBuffer;
        private readonly MainThreadApplyProcessor _mainThreadApplyProcessor;
        private readonly ExecutionConfigurationDispatcher _executionConfigurationDispatcher;
        private readonly List<UpdateHandle> _removedHandlesBuffer = new();
        private readonly NativePipelineCatalog _nativePipelineCatalog;

        // Actionのキャッシュ
        private readonly Action<UpdateHandle> _requestElementApply;
        private readonly Func<string, int, UpdateLayer> _getOrCreateLayer;
        private readonly Action<UpdateHandle> _removeElementIfDetached;

        public UpdateCoordinator(
            IUpdateExecutionBackend? backend = null,
            UpdateElementRegistry? elementRegistry = null,
            ExecutionConfigurationQueue? executionConfigurationQueue = null,
            MainThreadApplyProcessor? mainThreadApplyProcessor = null,
            MainThreadApplyCommandBuffer? mainThreadApplyCommandBuffer = null,
            MainThreadApplyHandleBuffer? mainThreadApplyHandleBuffer = null)
        {
            _backend = backend ?? SequentialUpdateExecutionBackend.Instance;
            _elementRegistry = elementRegistry ?? new UpdateElementRegistry();
            _executionConfigurationQueue = executionConfigurationQueue ?? new ExecutionConfigurationQueue();
            _executionConfigurationDispatcher = new ExecutionConfigurationDispatcher(_executionConfigurationQueue);
            _mainThreadApplyProcessor = mainThreadApplyProcessor ?? new MainThreadApplyProcessor();
            _mainThreadApplyCommandBuffer = mainThreadApplyCommandBuffer ?? new MainThreadApplyCommandBuffer();
            _mainThreadApplyHandleBuffer = mainThreadApplyHandleBuffer ?? new MainThreadApplyHandleBuffer();
            _nativePipelineCatalog = new NativePipelineCatalog();

            _requestElementApply = RequestElementApply;
            _getOrCreateLayer = GetOrCreateLayer;
            _removeElementIfDetached = RemoveElementIfDetached;
        }

        public uint FrameIndex { get; private set; }

        public UpdateLayer GetOrCreateLayer(string layerId, int layerOrder = 0)
        {
            if (string.IsNullOrWhiteSpace(layerId))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(layerId));
            }

            var normalizedLayerId = layerId.Trim();
            if (_layersById.TryGetValue(normalizedLayerId, out var existing))
            {
                if (existing.LayerOrder != layerOrder)
                {
                    throw new InvalidOperationException(
                        $"Layer '{normalizedLayerId}' is already registered with order {existing.LayerOrder}, but {layerOrder} was requested.");
                }

                return existing;
            }

            var layer = new UpdateLayer(normalizedLayerId, layerOrder, _backend, _elementRegistry);
            _layersById.Add(normalizedLayerId, layer);
            _orderedLayers.Add(layer);
            _orderedLayers.Sort(LayerOrderComparer.Instance);
            return layer;
        }

        public UpdateLayer GetOrCreateUpdateLayer(string layerId, int layerOrder = 0)
        {
            return GetOrCreateLayer(layerId, layerOrder);
        }

        public bool RegisterElement(string layerId, IUpdateElement element, int layerOrder = 0, int executionOrder = 0)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            var layer = GetOrCreateLayer(layerId, layerOrder);
            var createdElement = false;
            if (!_elementRegistry.TryGetHandle(element, out var handle))
            {
                _elementRegistry.Register(element, UpdateElementSyncPolicy.AllowMainThreadApply, out handle);
                createdElement = true;
            }

            if (_layerIdsByHandle.TryGetValue(handle, out var existingLayerId) &&
                !string.Equals(existingLayerId, layer.LayerId, StringComparison.Ordinal))
            {
                if (createdElement)
                {
                    _elementRegistry.Remove(handle);
                }

                throw new InvalidOperationException(
                    $"Element is already registered to layer '{existingLayerId}' and cannot also be registered to '{layer.LayerId}'.");
            }

            var registered = layer.Register(handle, executionOrder);
            if (registered)
            {
                _layerIdsByHandle[handle] = layer.LayerId;
            }
            else if (createdElement)
            {
                RemoveElementIfDetached(handle);
            }

            return registered;
        }

        public UpdateHandle RegisterNative<TState>(
            NativeStateRegistry<TState> registry,
            IUpdateExecutionBackend backend,
            IUpdateElement element,
            in TState initialState,
            out UpdateHandle nativeHandle,
            int pipelineOrder = 0,
            int executionOrder = 0,
            UpdateElementSyncPolicy syncPolicy = UpdateElementSyncPolicy.AllowMainThreadApply,
            string layerId = "Native",
            int layerOrder = 0)
            where TState : unmanaged
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            var layer = GetOrCreateLayer(layerId, layerOrder);
            var pipeline = GetOrCreateNativePipeline(registry, backend, layer, pipelineOrder);
            return RegisterNativeToPipeline(pipeline, element, in initialState, out nativeHandle, executionOrder, syncPolicy);
        }

        public NativePipelineId RegisterNativePipeline<TState>(
            string pipelineId,
            NativeStateRegistry<TState> registry,
            IUpdateExecutionBackend backend,
            int pipelineOrder = 0,
            string layerId = "Native",
            int layerOrder = 0)
            where TState : unmanaged
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            var nativePipelineId = new NativePipelineId(pipelineId);
            var layer = GetOrCreateLayer(layerId, layerOrder);
            var pipeline = GetOrCreateNativePipeline(registry, backend, layer, pipelineOrder);
            BindPipelineId(nativePipelineId, pipeline);
            return nativePipelineId;
        }

        public UpdateHandle RegisterNative<TState>(
            NativePipelineId pipelineId,
            IUpdateElement element,
            in TState initialState,
            out UpdateHandle nativeHandle,
            int executionOrder = 0,
            UpdateElementSyncPolicy syncPolicy = UpdateElementSyncPolicy.AllowMainThreadApply)
            where TState : unmanaged
        {
            if (!_nativePipelineCatalog.TryGetById(pipelineId, out var pipeline))
            {
                throw new InvalidOperationException($"Native pipeline '{pipelineId}' is not registered.");
            }

            if (pipeline.StateType != typeof(TState))
            {
                throw new InvalidOperationException(
                    $"Native pipeline '{pipelineId}' is registered for '{pipeline.StateType.Name}', not '{typeof(TState).Name}'.");
            }

            return RegisterNativeToPipeline(pipeline, element, in initialState, out nativeHandle, executionOrder, syncPolicy);
        }

        public bool UnregisterElement(IUpdateElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (!_elementRegistry.TryGetHandle(element, out var handle))
            {
                return false;
            }

            var removed = false;
            for (var i = 0; i < _orderedLayers.Count; i++)
            {
                removed |= _orderedLayers[i].Unregister(handle);
            }

            removed |= _nativePipelineCatalog.DetachElement(handle);

            if (removed)
            {
                RemoveElementIfDetached(handle);
            }

            return removed;
        }

        public bool TryGetHandle(IUpdateElement element, out UpdateHandle handle)
        {
            return _elementRegistry.TryGetHandle(element, out handle);
        }

        public void RequestUnregister(UpdateHandle handle)
        {
            if (!handle.IsValid)
            {
                throw new ArgumentException("A valid handle is required.", nameof(handle));
            }

            _executionConfigurationQueue.EnqueueUnregister(handle);
        }

        public void RequestReorder(UpdateHandle handle, int executionOrder)
        {
            if (!handle.IsValid)
            {
                throw new ArgumentException("A valid handle is required.", nameof(handle));
            }

            _executionConfigurationQueue.EnqueueReorder(handle, executionOrder);
        }

        public void EnqueueMainThreadApply(IMainThreadApplyCommand command)
        {
            _mainThreadApplyCommandBuffer.Enqueue(command);
        }

        public void RequestElementApply(UpdateHandle handle)
        {
            _mainThreadApplyHandleBuffer.Enqueue(handle);
        }

        public bool RequestElementApply(IUpdateElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (!_elementRegistry.TryGetHandle(element, out var handle))
            {
                return false;
            }

            _mainThreadApplyHandleBuffer.Enqueue(handle);
            return true;
        }

        public int ActivatePendingRegistrations()
        {
            DrainExecutionConfigurationCommands();
            var activatedCount = 0;
            for (var i = 0; i < _orderedLayers.Count; i++)
            {
                activatedCount += _orderedLayers[i].ActivatePendingRegistrations();
            }

            return activatedCount;
        }

        public void RunUpdate(float deltaTime, float unscaledDeltaTime)
        {
            FrameIndex++;
            for (var i = 0; i < _orderedLayers.Count; i++)
            {
                var layer = _orderedLayers[i];
                if (!layer.TryCreateExecutionContext(FrameIndex, deltaTime, unscaledDeltaTime, out var context))
                {
                    continue;
                }

                RunNativePhase(layer.LayerId, UpdateExecutionPhase.Update, in context);
                layer.RunManagedPhase(UpdateExecutionPhase.Update, in context);
            }
        }

        public void RunLateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            for (var i = 0; i < _orderedLayers.Count; i++)
            {
                var layer = _orderedLayers[i];
                if (!layer.TryCreateExecutionContext(FrameIndex, deltaTime, unscaledDeltaTime, out var context))
                {
                    continue;
                }

                RunNativePhase(layer.LayerId, UpdateExecutionPhase.LateUpdate, in context);
                layer.RunManagedPhase(UpdateExecutionPhase.LateUpdate, in context);
            }
        }

        public int ApplyMainThreadCommands()
        {
            return ApplyMainThreadChanges();
        }

        public int ApplyMainThreadChanges()
        {
            return _mainThreadApplyProcessor.Apply(
                _elementRegistry,
                _mainThreadApplyHandleBuffer,
                _mainThreadApplyCommandBuffer);
        }

        public int ApplyStructuralChanges()
        {
            DrainExecutionConfigurationCommands();

            var removedCount = 0;
            _removedHandlesBuffer.Clear();
            for (var i = 0; i < _orderedLayers.Count; i++)
            {
                removedCount += _orderedLayers[i].ApplyStructuralChanges(_removedHandlesBuffer);
            }

            for (var i = 0; i < _removedHandlesBuffer.Count; i++)
            {
                RemoveElementIfDetached(_removedHandlesBuffer[i]);
            }

            _removedHandlesBuffer.Clear();
            return removedCount;
        }

        private void DrainExecutionConfigurationCommands()
        {
            _executionConfigurationDispatcher.Apply(
                _elementRegistry,
                _orderedLayers,
                _nativePipelineCatalog,
                _getOrCreateLayer,
                _removeElementIfDetached);
        }

        private void RemoveElementIfDetached(UpdateHandle handle)
        {
            if (!_elementRegistry.Contains(handle))
            {
                _layerIdsByHandle.Remove(handle);
                return;
            }

            for (var i = 0; i < _orderedLayers.Count; i++)
            {
                if (_orderedLayers[i].Contains(handle))
                {
                    return;
                }
            }

            if (_nativePipelineCatalog.UsesElement(handle))
            {
                return;
            }

            _layerIdsByHandle.Remove(handle);
            _elementRegistry.Remove(handle);
        }

        private void RunNativePhase(string layerId, UpdateExecutionPhase phase, in UpdateFrameContext context)
        {
            _nativePipelineCatalog.RunLayer(layerId, phase, in context, _requestElementApply);
        }

        private NativeExecutionPipeline GetOrCreateNativePipeline<TState>(
            NativeStateRegistry<TState> registry,
            IUpdateExecutionBackend backend,
            UpdateLayer layer,
            int pipelineOrder)
            where TState : unmanaged
        {
            return _nativePipelineCatalog.GetOrCreate(registry, backend, layer, pipelineOrder);
        }

        private UpdateHandle RegisterNativeToPipeline<TState>(
            NativeExecutionPipeline pipeline,
            IUpdateElement element,
            in TState initialState,
            out UpdateHandle nativeHandle,
            int executionOrder,
            UpdateElementSyncPolicy syncPolicy)
            where TState : unmanaged
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            var createdElement = false;
            if (!_elementRegistry.TryGetHandle(element, out var elementHandle))
            {
                _elementRegistry.Register(element, syncPolicy, out elementHandle);
                createdElement = true;
            }

            try
            {
                if (_layerIdsByHandle.TryGetValue(elementHandle, out var existingLayerId) &&
                    !string.Equals(existingLayerId, pipeline.LayerId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Element handle '{elementHandle}' is already attached to layer '{existingLayerId}' and cannot also be attached to '{pipeline.LayerId}'.");
                }

                nativeHandle = pipeline.RegisterElement(elementHandle, in initialState, executionOrder);
                _layerIdsByHandle[elementHandle] = pipeline.LayerId;
                return elementHandle;
            }
            catch
            {
                if (createdElement)
                {
                    RemoveElementIfDetached(elementHandle);
                }

                throw;
            }
        }

        private void BindPipelineId(NativePipelineId pipelineId, NativeExecutionPipeline pipeline)
        {
            _nativePipelineCatalog.BindPipelineId(pipelineId, pipeline);
        }
    }
}
