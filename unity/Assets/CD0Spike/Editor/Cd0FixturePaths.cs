#nullable enable

using System;
using System.IO;

namespace CD0Spike.Editor
{
    internal static class Cd0FixturePaths
    {
        internal const string FixtureRoot = "Assets/CD0Spike/Fixture";
        internal const string BootstrapScene = FixtureRoot + "/Bootstrap.unity";
        internal const string PayloadScene = FixtureRoot + "/Payload.unity";
        internal const string ProbeAsset = FixtureRoot + "/ProbeAsset.asset";
        internal const string RootAsset = FixtureRoot + "/Root.asset";
        internal const string DefaultContentOutput = "../artifacts/cd0/content/local-v1";

        internal static string ProjectRoot => Path.GetDirectoryName(UnityEngine.Application.dataPath)!;
        internal static string ResolveProjectRelative(string relativePath) => Path.GetFullPath(Path.Combine(ProjectRoot, relativePath));

        internal static string RequirePlayerHostRoot()
        {
            var hostRoot = FindPlayerHostRoot(ProjectRoot);
            if (hostRoot != null) return hostRoot;
            throw new InvalidOperationException("The stripping experiment may run only below an exact artifacts/cd0/player-host directory.");
        }

        internal static string? FindPlayerHostRoot(string projectRoot)
        {
            DirectoryInfo? current = new DirectoryInfo(Path.GetFullPath(projectRoot));
            while (current != null)
            {
                if (string.Equals(current.Name, "player-host", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(current.Parent?.Name, "cd0", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(current.Parent?.Parent?.Name, "artifacts", StringComparison.OrdinalIgnoreCase))
                {
                    return current.FullName;
                }
                current = current.Parent;
            }
            return null;
        }
    }
}
