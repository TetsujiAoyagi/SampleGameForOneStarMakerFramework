#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using DebugStudio.App.Core.Models;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// command request / result の相関状態を保持する store。
/// requestId 単位で pending → completed / failed / timed out へ遷移させる。
/// </summary>
public sealed class CommandStore
{
    private const int DefaultRetention = 128;
    private readonly object _gate = new();
    private readonly int _retention;
    private readonly List<CommandDispatchRecord> _entries = new();
    private readonly Dictionary<string, int> _pendingIndexesByRequestId = new(StringComparer.Ordinal);
    private long _dispatchCount;
    private long _resultCount;
    private DebugCommandResultEnvelopeV1? _latestResult;

    public CommandStore(int retention = DefaultRetention)
    {
        _retention = retention > 0
            ? retention
            : throw new ArgumentOutOfRangeException(nameof(retention), "Retention must be greater than zero.");
    }

    public event Action<CommandStoreSnapshot>? Changed;

    /// <summary>
    /// 送信直前の command を pending entry として登録する。
    /// ここで requestId を固定しておくことで、後続 result を同じ行へ結び付けられる。
    /// </summary>
    public CommandStoreSnapshot TrackPending(DebugCommandEnvelopeV1 command, long startedAtUnixTimeMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RequestId))
        {
            throw new ArgumentException("Command RequestId is required for correlation.", nameof(command));
        }

        CommandStoreSnapshot snapshot;
        lock (_gate)
        {
            if (_pendingIndexesByRequestId.ContainsKey(command.RequestId))
            {
                throw new InvalidOperationException($"A pending command with requestId '{command.RequestId}' already exists.");
            }

            _dispatchCount++;
            _entries.Add(new CommandDispatchRecord(
                SequenceNumber: _dispatchCount,
                RequestId: command.RequestId,
                CommandType: string.IsNullOrWhiteSpace(command.CommandType) ? "<unknown-command>" : command.CommandType,
                RequestPayloadJson: command.PayloadJson ?? string.Empty,
                State: CommandDispatchState.Pending,
                StatusMessage: "Dispatch accepted. Waiting for Unity result.",
                ResultPayloadJson: string.Empty,
                StartedAtUnixTimeMilliseconds: startedAtUnixTimeMilliseconds,
                CompletedAtUnixTimeMilliseconds: null));
            _pendingIndexesByRequestId[command.RequestId] = _entries.Count - 1;
            snapshot = CreateSnapshotUnsafe();
        }

        Changed?.Invoke(snapshot);
        return snapshot;
    }

    /// <summary>
    /// transport 送信自体が失敗した command を終端状態へ落とす。
    /// 送信失敗後も pending に残すと、UI が無期限待機に見えてしまうため即時に確定させる。
    /// </summary>
    public CommandStoreSnapshot MarkDispatchFailed(
        string requestId,
        string detail,
        long completedAtUnixTimeMilliseconds)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(requestId));
        }

        CommandStoreSnapshot snapshot;
        lock (_gate)
        {
            if (_pendingIndexesByRequestId.TryGetValue(requestId, out var index))
            {
                var entry = _entries[index];
                _entries[index] = entry with
                {
                    State = CommandDispatchState.DispatchFailed,
                    StatusMessage = string.IsNullOrWhiteSpace(detail) ? "Command dispatch failed." : detail,
                    CompletedAtUnixTimeMilliseconds = completedAtUnixTimeMilliseconds,
                };
                _pendingIndexesByRequestId.Remove(requestId);
            }

            TrimHistoryUnsafe();
            snapshot = CreateSnapshotUnsafe();
        }

        Changed?.Invoke(snapshot);
        return snapshot;
    }

    public CommandStoreSnapshot AppendResult(DebugCommandResultEnvelopeV1 result)
    {
        ArgumentNullException.ThrowIfNull(result);

        CommandStoreSnapshot snapshot;
        lock (_gate)
        {
            _resultCount++;
            _latestResult = result;

            if (!string.IsNullOrWhiteSpace(result.RequestId) &&
                _pendingIndexesByRequestId.TryGetValue(result.RequestId, out var index))
            {
                var entry = _entries[index];
                _entries[index] = entry with
                {
                    State = result.Success ? CommandDispatchState.Succeeded : CommandDispatchState.Failed,
                    StatusMessage = string.IsNullOrWhiteSpace(result.Message)
                        ? (result.Success ? "Unity command completed successfully." : "Unity command failed.")
                        : result.Message,
                    ResultPayloadJson = result.PayloadJson ?? string.Empty,
                    CompletedAtUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
                _pendingIndexesByRequestId.Remove(result.RequestId);
            }
            else
            {
                _entries.Add(new CommandDispatchRecord(
                    SequenceNumber: _dispatchCount + _resultCount,
                    RequestId: string.IsNullOrWhiteSpace(result.RequestId) ? "<missing-request-id>" : result.RequestId,
                    CommandType: "<orphan-result>",
                    RequestPayloadJson: string.Empty,
                    State: CommandDispatchState.Orphaned,
                    StatusMessage: string.IsNullOrWhiteSpace(result.Message)
                        ? "Command result arrived without a matching pending request."
                        : result.Message,
                    ResultPayloadJson: result.PayloadJson ?? string.Empty,
                    StartedAtUnixTimeMilliseconds: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    CompletedAtUnixTimeMilliseconds: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            }

            TrimHistoryUnsafe();
            snapshot = CreateSnapshotUnsafe();
        }

        Changed?.Invoke(snapshot);
        return snapshot;
    }

    /// <summary>
    /// 一定時間以上返答が無い pending command を timeout 化する。
    /// timer は外側から与え、store 自身は純粋な状態更新だけを担う。
    /// </summary>
    public CommandStoreSnapshot ExpirePending(long nowUnixTimeMilliseconds, long timeoutMilliseconds)
    {
        if (timeoutMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), "Timeout must be greater than zero.");
        }

        CommandStoreSnapshot snapshot;
        var changed = false;

        lock (_gate)
        {
            foreach (var pendingPair in _pendingIndexesByRequestId.ToArray())
            {
                var entry = _entries[pendingPair.Value];
                if (nowUnixTimeMilliseconds - entry.StartedAtUnixTimeMilliseconds < timeoutMilliseconds)
                {
                    continue;
                }

                _entries[pendingPair.Value] = entry with
                {
                    State = CommandDispatchState.TimedOut,
                    StatusMessage = $"Unity command timed out after {timeoutMilliseconds} ms.",
                    CompletedAtUnixTimeMilliseconds = nowUnixTimeMilliseconds,
                };
                _pendingIndexesByRequestId.Remove(pendingPair.Key);
                changed = true;
            }

            TrimHistoryUnsafe();
            snapshot = CreateSnapshotUnsafe();
        }

        if (changed)
        {
            Changed?.Invoke(snapshot);
        }

        return snapshot;
    }

    /// <summary>
    /// disconnect/fault で返答不能になった pending command を切断終端へ落とす。
    /// 次セッションへ stale pending を持ち越さないための安全弁。
    /// </summary>
    public CommandStoreSnapshot MarkDisconnected(string detail, long completedAtUnixTimeMilliseconds)
    {
        CommandStoreSnapshot snapshot;
        var changed = false;

        lock (_gate)
        {
            foreach (var pendingPair in _pendingIndexesByRequestId.ToArray())
            {
                var entry = _entries[pendingPair.Value];
                _entries[pendingPair.Value] = entry with
                {
                    State = CommandDispatchState.Disconnected,
                    StatusMessage = string.IsNullOrWhiteSpace(detail)
                        ? "Connection closed before Unity returned a command result."
                        : detail,
                    CompletedAtUnixTimeMilliseconds = completedAtUnixTimeMilliseconds,
                };
                _pendingIndexesByRequestId.Remove(pendingPair.Key);
                changed = true;
            }

            snapshot = CreateSnapshotUnsafe();
        }

        if (changed)
        {
            Changed?.Invoke(snapshot);
        }

        return snapshot;
    }

    public CommandStoreSnapshot Reset()
    {
        CommandStoreSnapshot snapshot;
        lock (_gate)
        {
            _dispatchCount = 0;
            _resultCount = 0;
            _latestResult = null;
            _entries.Clear();
            _pendingIndexesByRequestId.Clear();
            snapshot = CreateSnapshotUnsafe();
        }

        Changed?.Invoke(snapshot);
        return snapshot;
    }

    public CommandStoreSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return CreateSnapshotUnsafe();
        }
    }

    private CommandStoreSnapshot CreateSnapshotUnsafe()
    {
        var entries = _entries
            .OrderByDescending(entry => entry.SequenceNumber)
            .ToArray();

        return new CommandStoreSnapshot(
            DispatchCount: _dispatchCount,
            ResultCount: _resultCount,
            PendingCount: _pendingIndexesByRequestId.Count,
            CompletedCount: entries.Count(entry => entry.State != CommandDispatchState.Pending),
            LatestEntry: entries.FirstOrDefault(),
            LatestResult: _latestResult,
            Entries: entries);
    }

    private void TrimHistoryUnsafe()
    {
        while (_entries.Count > _retention)
        {
            var removableIndex = _entries.FindIndex(entry => entry.State != CommandDispatchState.Pending);
            if (removableIndex < 0)
            {
                break;
            }

            _entries.RemoveAt(removableIndex);

            foreach (var pendingPair in _pendingIndexesByRequestId.ToArray())
            {
                if (pendingPair.Value > removableIndex)
                {
                    _pendingIndexesByRequestId[pendingPair.Key] = pendingPair.Value - 1;
                }
            }
        }
    }
}
