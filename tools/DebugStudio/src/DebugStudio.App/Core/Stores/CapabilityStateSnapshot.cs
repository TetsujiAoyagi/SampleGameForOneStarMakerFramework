#nullable enable

using System;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// capability negotiation の現在値。
/// MainWindow や各サブ ViewModel はこの snapshot だけ見れば、未対応機能の empty state を組み立てられる。
/// </summary>
public readonly record struct CapabilityStateSnapshot(
    DebugStudioCapability LocalSupportedCapabilities,
    DebugStudioCapability RemoteSupportedCapabilities,
    DebugStudioCapability NegotiatedCapabilities,
    string HandshakeState,
    string Detail,
    string RemoteName,
    string? SessionId,
    DateTimeOffset UpdatedAt);
