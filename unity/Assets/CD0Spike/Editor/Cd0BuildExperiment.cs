#nullable enable

using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CD0Spike.Editor
{
    internal static class Cd0BuildExperiment
    {
        [MenuItem("OneStarMaker/CD0/Build Content Directory")]
        private static void BuildContentDirectory()
        {
            if (AssetDatabase.LoadAssetAtPath<Cd0Root>(Cd0FixturePaths.RootAsset) == null)
            {
                throw new InvalidOperationException("Generate the CD0 fixture before building content.");
            }

            var outputPath = Cd0FixturePaths.ResolveProjectRelative(Cd0FixturePaths.DefaultContentOutput);
            var parameters = new BuildContentDirectoryParameters
            {
                outputPath = outputPath,
                rootAssetPaths = new[] { Cd0FixturePaths.RootAsset },
                name = "cd0-local-v1",
                compression = BuildCompression.Uncompressed,
                options = BuildContentOptions.None,
                extraScriptingDefines = Array.Empty<string>(),
            };
            var report = BuildPipeline.BuildContentDirectory(parameters);
            Debug.Log($"[CD0] Content build result={report.summary.result} output={report.summary.outputPath} target={report.summary.platform} totalSize={report.summary.totalSize}");
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"CD0 content build failed: {report.summary.result}");
            }
        }
    }
}
