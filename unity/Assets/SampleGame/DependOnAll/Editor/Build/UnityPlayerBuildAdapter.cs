#nullable enable
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace SampleGame.DependOnAll.Editor.Build
{
    internal interface IUnityPlayerBuildPipeline { BuildReport Build(BuildPlayerOptions options); }
    internal sealed class UnityPlayerBuildPipeline : IUnityPlayerBuildPipeline { public BuildReport Build(BuildPlayerOptions options) => BuildPipeline.BuildPlayer(options); }
    internal sealed class UnityPlayerBuildAdapter
    {
        private readonly IUnityPlayerBuildPipeline _pipeline;
        internal UnityPlayerBuildAdapter(IUnityPlayerBuildPipeline? pipeline = null) => _pipeline = pipeline ?? new UnityPlayerBuildPipeline();
        internal BuildReport Build(string bootstrapScene, string executablePath, string metadataDirectory)
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { bootstrapScene }, locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.DetailedBuildReport,
                extraScriptingDefines = new[] { "OSM_BS4_PLAYER" },
            };
            // Unity 6.6 API。reflection は旧 Editor で黙って落とさず、存在しなければ build 前に拒否する。
            object boxed = options;
            var property = typeof(BuildPlayerOptions).GetProperty("previousBuildReportDirectories", BindingFlags.Public | BindingFlags.Instance);
            var field = typeof(BuildPlayerOptions).GetField("previousBuildReportDirectories", BindingFlags.Public | BindingFlags.Instance);
            if (property != null) property.SetValue(boxed, new[] { metadataDirectory });
            else if (field != null) field.SetValue(boxed, new[] { metadataDirectory });
            else throw new MissingMemberException("BuildPlayerOptions.previousBuildReportDirectories is unavailable.");
            options = (BuildPlayerOptions)boxed;
            return _pipeline.Build(options);
        }
    }
}
