#nullable enable

using System;

namespace DebugStudio.Client;

public readonly record struct DebugSocketConnectionSnapshot(
    DebugSocketConnectionState State,
    Uri? ServerUri,
    string Detail,
    DateTimeOffset Timestamp);
