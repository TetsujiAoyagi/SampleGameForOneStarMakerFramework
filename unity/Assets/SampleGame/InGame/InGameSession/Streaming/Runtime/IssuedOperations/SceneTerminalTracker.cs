#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.SceneSystem;

namespace SampleGame.InGame.Streaming
{
    /// <summary>
    /// 終端イベントと個体世代で消滅を待つ。購読は Add/Unload より前。
    /// Query が null になったことだけでは完了にしない。
    /// </summary>
    /// <remarks>
    /// 購読開始前のイベントは replay しない。同一 identity の後発個体は、旧個体の Removed では完了しない。
    /// instanceGen は Game 側の観測世代であり、公開 API / SceneEventType は増やさない。
    /// </remarks>
    internal sealed class SceneTerminalTracker : IDisposable
    {
        private readonly IDisposable _subscription;
        private readonly Dictionary<string, ObservedInstance> _live = new(StringComparer.Ordinal);
        private readonly Dictionary<SceneBase, ObservedInstance> _byInstance = new();
        private readonly Action<string, int>? _onInstanceTerminal;
        private int _nextGen = 1;
        private bool _disposed;

        internal SceneTerminalTracker(ISceneTerminalEvents terminalEvents, Action<string, int>? onInstanceTerminal = null)
        {
            if (terminalEvents == null)
            {
                throw new ArgumentNullException(nameof(terminalEvents));
            }

            _onInstanceTerminal = onInstanceTerminal;
            _subscription = terminalEvents.Subscribe(OnTerminal);
        }

        /// <summary>
        /// 成功した Add の完了時に個体を捕捉して世代を進める。同一個体の再登録は既存世代を返す。
        /// </summary>
        internal int RegisterObservedInstance(SceneBase instance)
        {
            ThrowIfDisposed();
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (_byInstance.TryGetValue(instance, out var same) && !same.IsTerminated)
            {
                return same.Generation;
            }

            var identity = instance.SceneResource.Identity;
            if (_live.TryGetValue(identity, out var existing) && !existing.IsTerminated)
            {
                if (ReferenceEquals(existing.Instance, instance))
                {
                    return existing.Generation;
                }

                throw new InvalidOperationException(
                    $"identity '{identity}' には未終端の観測個体が残っています。");
            }

            var rec = new ObservedInstance(instance, _nextGen++);
            _live[identity] = rec;
            _byInstance[instance] = rec;
            return rec.Generation;
        }

        internal bool HasLiveInstance(string identity)
            => _live.TryGetValue(identity, out var rec) && !rec.IsTerminated;

        internal bool TryGetLiveGeneration(string identity, out int generation)
        {
            if (_live.TryGetValue(identity, out var rec) && !rec.IsTerminated)
            {
                generation = rec.Generation;
                return true;
            }

            generation = 0;
            return false;
        }

        /// <summary>
        /// いま未終端の観測個体すべての終端を待つ。発行していない NecessaryAlways 子も対象。
        /// </summary>
        internal async UniTask WaitAllLive(CancellationToken ct)
        {
            ThrowIfDisposed();
            var pending = new List<UniTask>();
            foreach (var pair in _live)
            {
                if (!pair.Value.IsTerminated)
                {
                    pending.Add(pair.Value.Terminal.Task.AttachExternalCancellation(ct));
                }
            }

            if (pending.Count == 0)
            {
                return;
            }

            await UniTask.WhenAll(pending);
        }

        /// <summary>
        /// 捕捉した個体の終端を待つ。登録されていない個体を後から完了扱いしない。
        /// </summary>
        internal UniTask WaitForInstance(SceneBase instance, CancellationToken ct)
        {
            ThrowIfDisposed();
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (!_byInstance.TryGetValue(instance, out var rec))
            {
                throw new InvalidOperationException(
                    $"個体 '{instance.SceneResource.Identity}' は観測登録されていません。購読前イベントを後から完了扱いしません。");
            }

            return rec.Terminal.Task.AttachExternalCancellation(ct);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _subscription.Dispose();
        }

        private void OnTerminal(SceneTerminalEvent terminalEvent)
        {
            if (_disposed)
            {
                return;
            }

            // 購読前、または未登録 identity のイベントは捨てる。Query 完了とは混ぜない。
            if (!_live.TryGetValue(terminalEvent.Identity, out var rec) || rec.IsTerminated)
            {
                return;
            }

            rec.TryTerminate();
            _live.Remove(terminalEvent.Identity);
            _onInstanceTerminal?.Invoke(terminalEvent.Identity, rec.Generation);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SceneTerminalTracker));
            }
        }

        private sealed class ObservedInstance
        {
            internal ObservedInstance(SceneBase instance, int generation)
            {
                Instance = instance;
                Generation = generation;
                Terminal = new UniTaskCompletionSource();
            }

            internal SceneBase Instance { get; }
            internal int Generation { get; }
            internal UniTaskCompletionSource Terminal { get; }
            internal bool IsTerminated { get; private set; }

            internal void TryTerminate()
            {
                if (IsTerminated)
                {
                    return;
                }

                IsTerminated = true;
                Terminal.TrySetResult();
            }
        }
    }
}
