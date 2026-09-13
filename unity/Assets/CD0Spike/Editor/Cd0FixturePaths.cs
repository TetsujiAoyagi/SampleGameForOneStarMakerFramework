#nullable enable

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
    }
}
