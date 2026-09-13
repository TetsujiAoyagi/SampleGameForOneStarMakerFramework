#nullable enable

using System;
using System.IO;
using Unity.Loading;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CD0Spike.Editor
{
    internal static class Cd0BuildExperiment
    {
        [MenuItem("OneStarMaker/CD0/Build Content Directory (Isolated Run)")]
        private static void BuildIsolatedContentDirectory()
        {
            var runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            BuildContentDirectory($"../artifacts/cd0/content/runs/{runId}", $"cd0-{runId}");
        }

        [MenuItem("OneStarMaker/CD0/Build Content Directory (Incremental Baseline)")]
        private static void BuildIncrementalContentDirectory()
        {
            foreach (var directory in ContentLoadManager.GetContentDirectories())
            {
                if (directory.IsValid && string.Equals(directory.BuildName, "cd0-local-v1", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Unregister the cd0-local-v1 content directory before rebuilding its incremental output.");
                }
            }
            BuildContentDirectory(Cd0FixturePaths.DefaultContentOutput, "cd0-local-v1");
        }

        private static void BuildContentDirectory(string relativeOutputPath, string buildName)
        {
            if (AssetDatabase.LoadAssetAtPath<Cd0Root>(Cd0FixturePaths.RootAsset) == null)
            {
                throw new InvalidOperationException("Generate the CD0 fixture before building content.");
            }

            var outputPath = Cd0FixturePaths.ResolveProjectRelative(relativeOutputPath);
            var parameters = new BuildContentDirectoryParameters
            {
                outputPath = outputPath,
                rootAssetPaths = new[] { Cd0FixturePaths.RootAsset },
                name = buildName,
                compression = BuildCompression.Uncompressed,
                options = BuildContentOptions.None,
                extraScriptingDefines = Array.Empty<string>(),
            };
            var report = BuildPipeline.BuildContentDirectory(parameters);
            Debug.Log($"[CD0] Content build result={report.summary.result} output={report.summary.outputPath} target={report.summary.platform} totalSize={report.summary.totalSize}");
            if (report.summary.result != BuildResult.Succeeded)
            {
                QuarantineFailedOutput(outputPath);
                throw new InvalidOperationException($"CD0 content build failed: {report.summary.result}");
            }
        }

        private static void QuarantineFailedOutput(string outputPath)
        {
            if (!Directory.Exists(outputPath)) return;
            var quarantinePath = outputPath + ".failed-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            Directory.Move(outputPath, quarantinePath);
            Debug.LogError($"[CD0] Failed content output quarantined at {quarantinePath}");
        }
    }
}
