#nullable enable

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace CD0Spike.Editor
{
    internal static class Cd0PlayerExperiment
    {
        internal static BuildReport BuildInIsolatedHost(string outputPath, string? previousBuildReportDirectory)
        {
            var hostRoot = Cd0FixturePaths.RequirePlayerHostRoot();
            var canonicalOutput = RequireContainedPath(hostRoot, outputPath, "Player output");
            string? canonicalReport = previousBuildReportDirectory == null
                ? null
                : RequireContainedPath(hostRoot, previousBuildReportDirectory, "previous BuildReport directory");
            if (!File.Exists(Path.Combine(Cd0FixturePaths.ProjectRoot, Cd0FixturePaths.BootstrapScene)))
            {
                throw new InvalidOperationException("The isolated host bootstrap scene is missing.");
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { Cd0FixturePaths.BootstrapScene },
                locationPathName = canonicalOutput,
                target = EditorUserBuildSettings.activeBuildTarget,
                targetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                options = BuildOptions.None,
                previousBuildReportDirectories = canonicalReport == null
                    ? Array.Empty<string>()
                    : new[] { canonicalReport },
            };
            return BuildPipeline.BuildPlayer(options);
        }

        private static string RequireContainedPath(string allowedRoot, string path, string role)
        {
            var canonical = Path.GetFullPath(path);
            var canonicalRoot = Cd0ArtifactInventory.CanonicalDirectory(allowedRoot);
            if (!Cd0ArtifactInventory.IsContained(canonicalRoot, canonical)
                || string.Equals(canonicalRoot, Cd0ArtifactInventory.CanonicalDirectory(canonical), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"{role} must be a descendant of the isolated CD0 player host: {canonical}");
            }
            return canonical;
        }
    }
}
