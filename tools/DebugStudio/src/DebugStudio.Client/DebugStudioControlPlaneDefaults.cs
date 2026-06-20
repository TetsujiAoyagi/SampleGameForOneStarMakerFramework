#nullable enable

using System;

namespace DebugStudio.Client;

public static class DebugStudioControlPlaneDefaults
{
    public static readonly Uri DefaultControlUri = new("ws://127.0.0.1:5012/cli-control/");
}
