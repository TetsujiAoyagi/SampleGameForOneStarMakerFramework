#nullable enable

using System;
using DebugStudio.Client;

namespace DebugStudio.App.Core.Services;

public sealed class DebugStudioCliControlOptions
{
    public Uri ControlUri { get; set; } = DebugStudioControlPlaneDefaults.DefaultControlUri;

    public int AcceptTimeoutSeconds { get; set; } = 5;
}
