using System;
using System.Collections.Generic;
using OneStarMaker.Foundation.UpdateSystem.Layers;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// world が並べ替えや ID バインドのために保持する non-generic pipeline 管理層。
    /// `TState` に依存する実行詳細は runtime へ隔離し、
    /// ここでは layer 所属と mirror/native handle 対応だけを扱う。
    /// </summary>
    internal sealed class NativeExecutionPipeline : INativeExecutionPipeline
    {
        // `TState` を知っている側の実処理。
        // world 管理層はこの箱を通じて generic 実行へ委譲する。
        private readonly INativeExecutionRuntime _runtime;

        // この pipeline がぶら下がる Layer。
        // 実行順・pause/timeScale・所属制約はすべて Layer 起点で決まる。
        private readonly UpdateLayer _layer;

        // native handle -> mirror handle の逆引き。
        // dirty export 時は native handle で返ってくるため、
        // apply publish の直前に mirror 側へ戻す必要がある。
        private readonly Dictionary<UpdateHandle, UpdateHandle> _mirrorHandlesByNativeHandle = new();

        // mirror handle -> native handle の逆引き。
        // detach / reorder など、mirror 起点の structural request を native 正本へ届けるために使う。
        private readonly Dictionary<UpdateHandle, UpdateHandle> _nativeHandlesByMirrorHandle = new();

        public NativeExecutionPipeline(
            INativeExecutionRuntime runtime,
            UpdateLayer layer,
            int pipelineOrder)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            PipelineOrder = pipelineOrder;
        }

        public int PipelineOrder { get; }

        public string LayerId => _layer.LayerId;

        public Type StateType => _runtime.StateType;

        public UpdateHandle RegisterElement<TState>(
            UpdateHandle elementHandle,
            in TState initialState,
            int executionOrder)
            where TState : unmanaged
        {
            if (_nativeHandlesByMirrorHandle.ContainsKey(elementHandle))
            {
                throw new InvalidOperationException(
                    $"Element handle '{elementHandle}' is already bound to a native state.");
            }

            if (_runtime is not NativeExecutionRuntime<TState> typedRuntime)
            {
                throw new InvalidOperationException(
                    $"This pipeline manages '{StateType.Name}', but '{typeof(TState).Name}' was requested.");
            }

            var nativeHandle = typedRuntime.Register(initialState, executionOrder);
            _mirrorHandlesByNativeHandle.Add(nativeHandle, elementHandle);
            _nativeHandlesByMirrorHandle.Add(elementHandle, nativeHandle);
            return nativeHandle;
        }

        public void ValidateConfiguration(
            IUpdateExecutionBackend backend,
            UpdateLayer layer,
            int pipelineOrder)
        {
            _runtime.ValidateConfiguration(backend);

            if (!ReferenceEquals(_layer, layer))
            {
                throw new InvalidOperationException(
                    $"The native registry is already attached to layer '{_layer.LayerId}' and cannot also be attached to '{layer.LayerId}'.");
            }

            if (PipelineOrder != pipelineOrder)
            {
                throw new InvalidOperationException(
                    $"The native registry is already attached with pipeline order {PipelineOrder}, but {pipelineOrder} was requested.");
            }
        }

        public bool UsesElement(UpdateHandle elementHandle)
        {
            return _nativeHandlesByMirrorHandle.ContainsKey(elementHandle);
        }

        public bool DetachElement(UpdateHandle elementHandle)
        {
            if (!_nativeHandlesByMirrorHandle.TryGetValue(elementHandle, out var nativeHandle))
            {
                return false;
            }

            _nativeHandlesByMirrorHandle.Remove(elementHandle);
            _mirrorHandlesByNativeHandle.Remove(nativeHandle);
            return _runtime.Unregister(nativeHandle);
        }

        public bool TryReorder(UpdateHandle elementHandle, int executionOrder)
        {
            if (!_nativeHandlesByMirrorHandle.TryGetValue(elementHandle, out var nativeHandle))
            {
                return false;
            }

            // native path では data-parallel 実行のため、
            // execution order は「逐次ループ順」ではなく dispatch metadata として持つ。
            // それでも structural queue から見た reorder 契約自体は managed と同じにしたいので、
            // mirror handle で受けた要求を native handle へ正規化して registry 正本へ反映する。
            _runtime.SetExecutionOrder(nativeHandle, executionOrder);
            return true;
        }

        public void Run(
            UpdateExecutionPhase phase,
            in UpdateFrameContext context,
            Action<UpdateHandle> requestMainThreadApply)
        {
            if (_runtime.Count == 0)
            {
                return;
            }

            _runtime.Execute(
                phase,
                in context,
                nativeHandle =>
                {
                    if (!_mirrorHandlesByNativeHandle.TryGetValue(nativeHandle, out var mirrorHandle))
                    {
                        return;
                    }

                    requestMainThreadApply(mirrorHandle);
                });
        }
    }
}
