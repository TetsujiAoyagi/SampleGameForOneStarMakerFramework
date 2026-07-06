#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.Streaming;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// T-06 用のテストダブル。RequestAdd/RequestRemove の完了タイミングと IsLoaded 観測を
    /// テストから決定的に制御する。
    /// </summary>
    public sealed class FakeStreamingBackend : ISceneStreamingBackend
    {
        /// <summary>記録されたバックエンド呼び出しの種別。</summary>
        public enum CallKind
        {
            RequestAdd,
            RequestRemove,
        }

        /// <summary>呼び出し履歴の 1 エントリ。</summary>
        public readonly struct RecordedCall
        {
            public RecordedCall(CallKind kind, string cellId, int priority)
            {
                Kind = kind;
                CellId = cellId;
                Priority = priority;
            }

            public CallKind Kind { get; }
            public string CellId { get; }
            public int Priority { get; }
        }

        private readonly List<RecordedCall> _history = new();
        private readonly Dictionary<string, bool> _loadedStates = new(StringComparer.Ordinal);
        private readonly Dictionary<string, UniTaskCompletionSource> _pendingAdds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, UniTaskCompletionSource> _pendingRemoves = new(StringComparer.Ordinal);

        /// <summary>全呼び出し履歴（発行順）。</summary>
        public IReadOnlyList<RecordedCall> CallHistory => _history;

        /// <summary>
        /// <see langword="true"/> のとき <see cref="RequestAdd"/> は即座に完了する。
        /// <see langword="false"/> のとき <see cref="CompleteRequestAdd"/> まで保留する。
        /// </summary>
        public bool AutoCompleteRequestAdd { get; set; } = true;

        /// <summary>
        /// <see langword="true"/> のとき <see cref="RequestRemove"/> は即座に完了する。
        /// <see langword="false"/> のとき <see cref="CompleteRequestRemove"/> まで保留する。
        /// </summary>
        public bool AutoCompleteRequestRemove { get; set; } = true;

        /// <summary>呼び出し履歴をクリアする（アサート用）。</summary>
        public void ClearHistory() => _history.Clear();

        /// <summary>
        /// <see cref="ISceneStreamingBackend.IsLoaded"/> の返答を設定する。
        /// G-6（RequestAdd 完了だが未ロード）の再現に使用する。
        /// </summary>
        public void SetLoaded(string cellId, bool loaded) => _loadedStates[cellId] = loaded;

        /// <inheritdoc />
        public bool IsLoaded(string cellId)
        {
            return _loadedStates.TryGetValue(cellId, out var loaded) && loaded;
        }

        /// <inheritdoc />
        public UniTask RequestAdd(string cellId, int priority)
        {
            if (_pendingAdds.ContainsKey(cellId))
            {
                throw new InvalidOperationException(
                    $"同一 cellId '{cellId}' に対する RequestAdd が保留中です。二重発行は禁止です。");
            }

            _history.Add(new RecordedCall(CallKind.RequestAdd, cellId, priority));

            if (AutoCompleteRequestAdd)
            {
                return UniTask.CompletedTask;
            }

            var tcs = new UniTaskCompletionSource();
            _pendingAdds[cellId] = tcs;
            return tcs.Task;
        }

        /// <inheritdoc />
        public UniTask RequestRemove(string cellId)
        {
            _history.Add(new RecordedCall(CallKind.RequestRemove, cellId, priority: 0));

            if (AutoCompleteRequestRemove)
            {
                _loadedStates[cellId] = false;
                return UniTask.CompletedTask;
            }

            var tcs = new UniTaskCompletionSource();
            _pendingRemoves[cellId] = tcs;
            return tcs.Task;
        }

        /// <summary>保留中の <see cref="RequestAdd"/> を手動完了する。</summary>
        public void CompleteRequestAdd(string cellId)
        {
            if (_pendingAdds.Remove(cellId, out var tcs))
            {
                tcs.TrySetResult();
            }
        }

        /// <summary>保留中の <see cref="RequestRemove"/> を手動完了する。同期完了経路と同様に loaded=false へ遷移する。</summary>
        public void CompleteRequestRemove(string cellId)
        {
            if (_pendingRemoves.Remove(cellId, out var tcs))
            {
                _loadedStates[cellId] = false;
                tcs.TrySetResult();
            }
        }

        /// <summary>指定セルの RequestAdd が保留中かどうか。</summary>
        public bool IsRequestAddPending(string cellId) => _pendingAdds.ContainsKey(cellId);

        /// <summary>保留中の RequestAdd の cellId 一覧（収束ループで全保留を完了させる用）。</summary>
        public IReadOnlyCollection<string> PendingAddCellIds => _pendingAdds.Keys;

        /// <summary>RequestAdd 呼び出しのみを発行順で返す。</summary>
        public IEnumerable<RecordedCall> AddCalls
        {
            get
            {
                foreach (var call in _history)
                {
                    if (call.Kind == CallKind.RequestAdd)
                    {
                        yield return call;
                    }
                }
            }
        }

        /// <summary>RequestRemove 呼び出しのみを発行順で返す。</summary>
        public IEnumerable<RecordedCall> RemoveCalls
        {
            get
            {
                foreach (var call in _history)
                {
                    if (call.Kind == CallKind.RequestRemove)
                    {
                        yield return call;
                    }
                }
            }
        }
    }
}
