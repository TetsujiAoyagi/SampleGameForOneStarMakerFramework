#nullable enable

using System;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// session 単位の capability 状態保持。
///
/// <para>
/// hello / welcome の詳細を MainWindowViewModel へ直接持ち込むと、
/// shell が protocol 事情で膨らみやすい。
/// そこで negotiation 結果は store に閉じ込め、画面側は snapshot 購読だけで扱えるようにする。
/// </para>
/// </summary>
public sealed class CapabilityStateStore
{
    private const int SupportedMinSchemaVersion = 1;
    private const int SupportedMaxSchemaVersion = 1;
    private readonly object _gate = new();
    private CapabilityStateSnapshot _snapshot;

    public CapabilityStateStore(DebugStudioCapability localSupportedCapabilities)
    {
        _snapshot = new CapabilityStateSnapshot(
            localSupportedCapabilities,
            DebugStudioCapability.None,
            DebugStudioCapability.None,
            "Idle",
            "Connect to a Unity session to negotiate capabilities.",
            "Unknown",
            null,
            DateTimeOffset.Now);
    }

    public event Action<CapabilityStateSnapshot>? Changed;

    public CapabilityStateSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }

    public void ResetForConnect(Uri serverUri)
    {
        Update(snapshot => snapshot with
        {
            RemoteSupportedCapabilities = DebugStudioCapability.None,
            NegotiatedCapabilities = DebugStudioCapability.None,
            HandshakeState = "Negotiating",
            Detail = $"Connected to {serverUri}. Sending capability hello...",
            RemoteName = "Unknown",
            SessionId = null,
            UpdatedAt = DateTimeOffset.Now,
        });
    }

    public void MarkHelloSent()
    {
        Update(snapshot => snapshot with
        {
            HandshakeState = "Negotiating",
            Detail = "Capability hello sent. Waiting for Unity welcome.",
            UpdatedAt = DateTimeOffset.Now,
        });
    }

    public void MarkHandshakeFaulted(string detail)
    {
        Update(snapshot => snapshot with
        {
            HandshakeState = "NegotiationFaulted",
            Detail = detail,
            UpdatedAt = DateTimeOffset.Now,
        });
    }

    public void ApplyWelcome(CapabilityHandshakeWelcomeEnvelopeV1 welcome)
    {
        ArgumentNullException.ThrowIfNull(welcome);

        var selectedSchemaVersion = welcome.SelectedSchemaVersion;
        if (selectedSchemaVersion < SupportedMinSchemaVersion || selectedSchemaVersion > SupportedMaxSchemaVersion)
        {
            Update(snapshot => snapshot with
            {
                RemoteSupportedCapabilities = welcome.ServerCapabilities,
                NegotiatedCapabilities = DebugStudioCapability.None,
                HandshakeState = "NegotiationFaulted",
                Detail =
                    $"Schema negotiation failed. Unity selected schema {selectedSchemaVersion}, but DebugStudio supports {SupportedMinSchemaVersion}-{SupportedMaxSchemaVersion}.",
                RemoteName = string.IsNullOrWhiteSpace(welcome.ServerName) ? "Unity" : welcome.ServerName,
                SessionId = string.IsNullOrWhiteSpace(welcome.SessionId) ? null : welcome.SessionId,
                UpdatedAt = DateTimeOffset.Now,
            });
            return;
        }

        Update(snapshot => snapshot with
        {
            RemoteSupportedCapabilities = welcome.ServerCapabilities,
            NegotiatedCapabilities = welcome.NegotiatedCapabilities,
            HandshakeState = "Negotiated",
            Detail = string.IsNullOrWhiteSpace(welcome.StatusMessage)
                ? $"Capability negotiation completed with {welcome.ServerName}."
                : welcome.StatusMessage,
            RemoteName = string.IsNullOrWhiteSpace(welcome.ServerName) ? "Unity" : welcome.ServerName,
            SessionId = string.IsNullOrWhiteSpace(welcome.SessionId) ? null : welcome.SessionId,
            UpdatedAt = DateTimeOffset.Now,
        });
    }

    public void MarkDisconnected(string detail)
    {
        Update(snapshot => snapshot with
        {
            HandshakeState = "Disconnected",
            Detail = string.IsNullOrWhiteSpace(detail) ? "Disconnected." : detail,
            NegotiatedCapabilities = DebugStudioCapability.None,
            UpdatedAt = DateTimeOffset.Now,
        });
    }

    public bool Supports(DebugStudioCapability capability)
    {
        lock (_gate)
        {
            return (_snapshot.NegotiatedCapabilities & capability) == capability;
        }
    }

    private void Update(Func<CapabilityStateSnapshot, CapabilityStateSnapshot> updater)
    {
        CapabilityStateSnapshot snapshot;
        lock (_gate)
        {
            snapshot = updater(_snapshot);
            _snapshot = snapshot;
        }

        Changed?.Invoke(snapshot);
    }
}
