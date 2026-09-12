#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SampleGame.InGame.Streaming
{
    internal enum IssuedSceneOperationKind
    {
        Add,
        Unload,
    }

    /// <summary>
    /// この枝が発行した Add/Unload を登録し、終了まで所有する。
    /// Dispose / Stop / Clear で記録を消して完了扱いしない。
    /// </summary>
    /// <remarks>
    /// opId は発行した操作、instanceGen は捕捉した SceneBase 個体。終端イベント 1 回で
    /// その個体に紐づく未完了 op をすべて完了する。後発 Add が旧個体の Removed で完了しない。
    /// </remarks>
    internal sealed class IssuedSceneOperationRegistry : IDisposable
    {
        private readonly Dictionary<int, IssuedOp> _ops = new();
        private readonly Dictionary<string, int> _latestAddOps = new(StringComparer.Ordinal);
        private int _nextOpId = 1;
        private bool _allowNewAdds = true;
        private bool _allowNewUnloads = true;
        private bool _disposed;

        /// <summary>新規 Add を登録する。発行停止後は false。</summary>
        internal bool TryRegisterAdd(string identity, out int opId)
        {
            ThrowIfDisposed();
            opId = 0;
            if (!_allowNewAdds)
            {
                return false;
            }

            opId = Register(identity, IssuedSceneOperationKind.Add);
            return true;
        }

        /// <summary>
        /// Unload を登録する。通常の新規 Unload は発行停止後に拒否するが、
        /// 未完了 Add に属する回収 Unload は同一 op の続きとして許可する。
        /// </summary>
        internal bool TryRegisterUnload(string identity, out int opId)
        {
            ThrowIfDisposed();
            opId = 0;
            var recovery = HasIncompleteAdd(identity);
            if (!_allowNewUnloads && !recovery)
            {
                return false;
            }

            opId = Register(identity, IssuedSceneOperationKind.Unload);
            return true;
        }

        /// <summary>成功した Add の opId を identity に記憶し、個体捕捉後に世代へ結ぶ。</summary>
        internal void RememberAddOp(string identity, int opId)
        {
            ThrowIfDisposed();
            _latestAddOps[identity] = opId;
        }

        internal void BindLatestAdd(string identity, int instanceGen)
        {
            ThrowIfDisposed();
            if (_latestAddOps.TryGetValue(identity, out var opId))
            {
                BindInstance(opId, instanceGen);
            }
        }

        /// <summary>
        /// その identity の未 bind な未完了 op を個体世代へ結ぶ。
        /// Add 完了前に出た RequestRemove / 回収 Unload も同じ個体の終端で完了する。
        /// </summary>
        internal void BindIncompleteForIdentity(string identity, int instanceGen)
        {
            ThrowIfDisposed();
            foreach (var pair in _ops)
            {
                var op = pair.Value;
                if (!op.IsCompleted
                    && op.InstanceGen == 0
                    && string.Equals(op.Identity, identity, StringComparison.Ordinal))
                {
                    op.InstanceGen = instanceGen;
                }
            }
        }

        internal void BindInstance(int opId, int instanceGen)
        {
            ThrowIfDisposed();
            if (!_ops.TryGetValue(opId, out var op))
            {
                throw new InvalidOperationException($"opId {opId} は登録されていません。");
            }

            op.InstanceGen = instanceGen;
        }

        internal void CompleteOp(int opId)
        {
            if (_ops.TryGetValue(opId, out var op))
            {
                op.TryComplete();
            }
        }

        /// <summary>その個体の終端。紐づく未完了 op をすべて完了する。記録は消さない。</summary>
        /// <remarks>
        /// 終端時点で一致した op だけを完了する。TryComplete の同期 continuation が
        /// Register しても Dictionary 列挙を壊さず、同イベントの後発 op を誤完了しない。
        /// </remarks>
        internal void CompleteInstance(string identity, int instanceGen)
        {
            var matched = new List<IssuedOp>();
            foreach (var pair in _ops)
            {
                var op = pair.Value;
                if (!op.IsCompleted
                    && op.InstanceGen == instanceGen
                    && string.Equals(op.Identity, identity, StringComparison.Ordinal))
                {
                    matched.Add(op);
                }
            }

            for (var i = 0; i < matched.Count; i++)
            {
                matched[i].TryComplete();
            }
        }

        internal bool HasIncompleteAdd(string identity)
        {
            foreach (var pair in _ops)
            {
                var op = pair.Value;
                if (op.Kind == IssuedSceneOperationKind.Add
                    && !op.IsCompleted
                    && string.Equals(op.Identity, identity, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        internal bool HasIncomplete()
        {
            foreach (var pair in _ops)
            {
                if (!pair.Value.IsCompleted)
                {
                    return true;
                }
            }

            return false;
        }

        internal bool IsComplete(int opId)
            => _ops.TryGetValue(opId, out var op) && op.IsCompleted;

        /// <summary>季節切替の前に旧枝の発行済みを待つ。Dispose / Stop からは呼ばない。</summary>
        internal async UniTask WaitAllIncomplete(CancellationToken ct)
        {
            ThrowIfDisposed();
            var pending = new List<UniTask>();
            foreach (var pair in _ops)
            {
                if (!pair.Value.IsCompleted)
                {
                    pending.Add(pair.Value.Completion.Task.AttachExternalCancellation(ct));
                }
            }

            if (pending.Count == 0)
            {
                return;
            }

            await UniTask.WhenAll(pending);
        }

        /// <summary>新規発行を止める。既存 op は完了させない。</summary>
        internal void StopNewIssues()
        {
            _allowNewAdds = false;
            _allowNewUnloads = false;
        }

        /// <inheritdoc />
        /// <remarks>記録は消さず、未完了 op も完了扱いしない。</remarks>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        private int Register(string identity, IssuedSceneOperationKind kind)
        {
            if (string.IsNullOrEmpty(identity))
            {
                throw new ArgumentException("identity は空にできません。", nameof(identity));
            }

            var opId = _nextOpId++;
            _ops[opId] = new IssuedOp(opId, identity, kind);
            return opId;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(IssuedSceneOperationRegistry));
            }
        }

        private sealed class IssuedOp
        {
            internal IssuedOp(int opId, string identity, IssuedSceneOperationKind kind)
            {
                OpId = opId;
                Identity = identity;
                Kind = kind;
                Completion = new UniTaskCompletionSource();
            }

            internal int OpId { get; }
            internal string Identity { get; }
            internal IssuedSceneOperationKind Kind { get; }
            internal int InstanceGen { get; set; }
            internal UniTaskCompletionSource Completion { get; }
            internal bool IsCompleted { get; private set; }

            internal void TryComplete()
            {
                if (IsCompleted)
                {
                    return;
                }

                IsCompleted = true;
                Completion.TrySetResult();
            }
        }
    }
}
