using System;
using System.Collections.Generic;
using OneStarMaker.Foundation.UpdateSystem.Layers;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// native pipeline の identity / ordering / layer 紐付けを集約する管理オブジェクト。
    /// `UpdateCoordinator` はここへ委譲することで、
    /// pipeline のコレクション操作とフレーム orchestration を分離する。
    /// </summary>
    internal sealed class NativePipelineCatalog
    {
        // native registry 実体 -> pipeline 管理層の対応。
        // 同じ registry が複数 backend/layer/pipeline に二重接続されるのを防ぐ。
        private readonly Dictionary<object, NativeExecutionPipeline> _pipelinesByRegistry = new();

        // 論理 ID -> pipeline 管理層の対応。
        // ID ベース登録 API はここを起点に pipeline を解決する。
        private readonly Dictionary<NativePipelineId, NativeExecutionPipeline> _pipelinesById = new();

        // world 全体で native pipeline を順序管理する配列。
        // detach 判定や structural 配布では Layer をまたいで総当たりするため、
        // 全件列を別に持っている。
        private readonly List<INativeExecutionPipeline> _orderedPipelines = new();

        // layerId ごとに、その Layer に属する native pipeline を束ねた配列。
        // 実フレームではここから対象 Layer 分だけを取り出して走らせる。
        private readonly Dictionary<string, List<INativeExecutionPipeline>> _pipelinesByLayerId =
            new(StringComparer.Ordinal);

        public NativeExecutionPipeline GetOrCreate<TState>(
            NativeStateRegistry<TState> registry,
            IUpdateExecutionBackend backend,
            UpdateLayer layer,
            int pipelineOrder)
            where TState : unmanaged
        {
            // registry 実体は pipeline identity の一部でもある。
            // そのため同じ registry に対して layer/backend/order が食い違う再接続は
            // 「既存 pipeline の再利用」ではなく設定矛盾として扱う。
            if (_pipelinesByRegistry.TryGetValue(registry, out var existing))
            {
                if (existing.StateType != typeof(TState))
                {
                    throw new InvalidOperationException(
                        $"Registry '{typeof(TState).Name}' is already attached with a different state type.");
                }

                existing.ValidateConfiguration(backend, layer, pipelineOrder);
                return existing;
            }

            var runtime = new NativeExecutionRuntime<TState>(registry, backend);
            var created = new NativeExecutionPipeline(runtime, layer, pipelineOrder);
            _pipelinesByRegistry.Add(registry, created);
            _orderedPipelines.Add(created);
            _orderedPipelines.Sort(NativePipelineOrderComparer.Instance);

            var pipelinesInLayer = GetOrCreateLayerPipelineList(layer.LayerId);
            pipelinesInLayer.Add(created);
            pipelinesInLayer.Sort(NativePipelineOrderComparer.Instance);
            return created;
        }

        public bool TryGetById(NativePipelineId pipelineId, out NativeExecutionPipeline pipeline)
        {
            return _pipelinesById.TryGetValue(pipelineId, out pipeline);
        }

        public void BindPipelineId(NativePipelineId pipelineId, NativeExecutionPipeline pipeline)
        {
            // 同じ logical ID は「同じ pipeline を再参照する」用途には使えても、
            // 別 pipeline への再束縛は許可しない。
            // ここを曖昧にすると、呼び出し側から見た pipeline identity が壊れる。
            if (_pipelinesById.TryGetValue(pipelineId, out var existing) &&
                !ReferenceEquals(existing, pipeline))
            {
                throw new InvalidOperationException(
                    $"Native pipeline id '{pipelineId}' is already bound to another pipeline.");
            }

            _pipelinesById[pipelineId] = pipeline;
        }

        public void RunLayer(
            string layerId,
            UpdateExecutionPhase phase,
            in UpdateFrameContext context,
            Action<UpdateHandle> requestMainThreadApply)
        {
            if (!_pipelinesByLayerId.TryGetValue(layerId, out var pipelines) || pipelines.Count == 0)
            {
                return;
            }

            for (var i = 0; i < pipelines.Count; i++)
            {
                pipelines[i].Run(phase, in context, requestMainThreadApply);
            }
        }

        public bool DetachElement(UpdateHandle elementHandle)
        {
            var removed = false;
            for (var i = 0; i < _orderedPipelines.Count; i++)
            {
                removed |= _orderedPipelines[i].DetachElement(elementHandle);
            }

            return removed;
        }

        public void Reorder(UpdateHandle mirrorHandle, int executionOrder)
        {
            for (var i = 0; i < _orderedPipelines.Count; i++)
            {
                _orderedPipelines[i].TryReorder(mirrorHandle, executionOrder);
            }
        }

        public bool UsesElement(UpdateHandle elementHandle)
        {
            for (var i = 0; i < _orderedPipelines.Count; i++)
            {
                if (_orderedPipelines[i].UsesElement(elementHandle))
                {
                    return true;
                }
            }

            return false;
        }

        private List<INativeExecutionPipeline> GetOrCreateLayerPipelineList(string layerId)
        {
            if (!_pipelinesByLayerId.TryGetValue(layerId, out var pipelines))
            {
                pipelines = new List<INativeExecutionPipeline>();
                _pipelinesByLayerId.Add(layerId, pipelines);
            }

            return pipelines;
        }
    }
}
