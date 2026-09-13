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
            var projectRoot = Cd0FixturePaths.ProjectRoot.Replace('\\', '/');
            if (!projectRoot.Contains("/artifacts/cd0/player-host", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The stripping experiment may run only in the isolated CD0 player host.");
            }
            if (!File.Exists(Path.Combine(Cd0FixturePaths.ProjectRoot, Cd0FixturePaths.BootstrapScene)))
            {
                throw new InvalidOperationException("The isolated host bootstrap scene is missing.");
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { Cd0FixturePaths.BootstrapScene },
                locationPathName = outputPath,
                target = EditorUserBuildSettings.activeBuildTarget,
                targetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                options = BuildOptions.None,
                previousBuildReportDirectories = previousBuildReportDirectory == null
                    ? Array.Empty<string>()
                    : new[] { Path.GetFullPath(previousBuildReportDirectory) },
            };
            return BuildPipeline.BuildPlayer(options);
        }
    }
}
