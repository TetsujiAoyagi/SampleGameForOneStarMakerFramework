#nullable enable

using System;
using System.Collections.Generic;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Export.Models;

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// sessionId → セッション属性の保持。
///
/// <para>
/// 「現在接続中のセッション」ではなく record の <c>SessionId</c> で引く。
/// 再接続・遅延受信・過去 retained の誤付与を防ぐため、複数 session を同時に保持する。
/// </para>
/// </summary>
public sealed class TelemetrySessionAttributesStore
{
    /// <summary>
    /// TelemetryStore の default retainedCapacity（256）以上にする。
    /// session チャーンで属性だけ先に FIFO eviction され、retained telemetry の再 map が欠測になるのを防ぐ。
    /// </summary>
    internal const int MaxSessions = 256;

    private readonly object _gate = new();
    private readonly Dictionary<string, TelemetrySessionAttributes> _bySessionId = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _insertionOrder = new();

    public void ApplyWelcome(CapabilityHandshakeWelcomeEnvelopeV1 welcome)
    {
        ArgumentNullException.ThrowIfNull(welcome);

        if (string.IsNullOrEmpty(welcome.SessionId))
        {
            return;
        }

        var attributes = new TelemetrySessionAttributes(
            welcome.BuildVersion ?? string.Empty,
            welcome.Platform ?? string.Empty,
            welcome.DeviceModel ?? string.Empty,
            welcome.OsVersion ?? string.Empty,
            welcome.EngineVersion ?? string.Empty);

        lock (_gate)
        {
            if (_bySessionId.ContainsKey(welcome.SessionId))
            {
                _bySessionId[welcome.SessionId] = attributes;
                return;
            }

            while (_bySessionId.Count >= MaxSessions && _insertionOrder.First != null)
            {
                var oldest = _insertionOrder.First.Value;
                _insertionOrder.RemoveFirst();
                _bySessionId.Remove(oldest);
            }

            _bySessionId[welcome.SessionId] = attributes;
            _insertionOrder.AddLast(welcome.SessionId);
        }
    }

    public TelemetrySessionAttributes? TryGet(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return null;
        }

        lock (_gate)
        {
            return _bySessionId.TryGetValue(sessionId, out var attributes) ? attributes : null;
        }
    }
}
