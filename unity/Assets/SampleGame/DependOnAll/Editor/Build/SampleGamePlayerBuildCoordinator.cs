#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Editor.Build.Content;
using UnityEditor;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    internal static class SampleGamePlayerBuildCoordinator
    {
        private const string ProbePath = "Assets/OneStarMakerGenerated/BS4/Bs4Probe.prefab";
        private const string ProbeRoot = "Assets/OneStarMakerGenerated/BS4";
        private const string ProbeMarker = "Assets/OneStarMakerGenerated/BS4/.bs4-fixture-owner";
        private const string MapPath = "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";
        private const string BootstrapScene = "Assets/OneStarMaker/Scenes/UIScene.unity";

        [MenuItem("Tools/OSM/Content/Build BS4 Spring Player")]
        private static void BuildMenu() => Build();

        internal static string Build()
        {
            var token = Guid.NewGuid().ToString("N"); const string representation = "Full";
            CreateProbe(token);
            try
            {
                var guid = AssetDatabase.AssetPathToGUID(ProbePath);
                var result = SampleGameContentBuild.BuildResult(new[] { "Spring" }, SeasonContentMode.Full, "bs4-spring-full",
                    (plan, snapshot) =>
                    {
                        var value = Bs4FixtureComposer.Compose(plan, snapshot, guid, ProbePath, representation, token);
                        return (value.Plan, value.Snapshot);
                    });
                var input = PlayerBuildInputValidator.Validate(result, File.ReadAllText(result.PreflightPath), File.ReadAllText(result.OutcomePath));
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var artifactsRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "artifacts", "bs4"));
                var playerWork = Path.Combine(artifactsRoot, "work", result.Identity);
                Directory.CreateDirectory(playerWork);
                var exe = Path.Combine(playerWork, "SampleGame.exe");
                var runtimeJson = RuntimeJson(result.Identity, representation, token);
                using (var mutation = new PlayerBuildProjectMutation(result.Identity, runtimeJson))
                {
                    mutation.CopyGraphClosure(MapPath);
                    var report = new UnityPlayerBuildAdapter().Build(BootstrapScene, exe, result.MetadataPath!);
                    // preflight.selected は stable|logical|physicalGuid。roots は path を含むため
                    // BuildReport の sourceAssetGUID と比較できず、重複検査が常に空振りになる。
                    var selectedRootGuids = input.Preflight.selected.Select(ParseSelectedPhysicalGuid).ToArray();
                    var verification = PlayerBuildReportVerifier.Verify(report, selectedRootGuids);
                    var receipt = JsonUtility.ToJson(new BuildReceipt
                    {
                        identity = result.Identity, contentReportPath = result.OutcomePath,
                        playerBuildGuid = verification.BuildGuid, backend = "IL2CPP", stripping = "High",
                        scenes = new[] { BootstrapScene }, checkedRootGuids = verification.CheckedGuids,
                    }, true);
                    return new PlayerBuildPublisher().Publish(artifactsRoot, result.Identity, playerWork, result.ContentPath!, receipt);
                }
            }
            finally
            {
                if (File.Exists(ProbeMarker) && File.ReadAllText(ProbeMarker) == "BS4-FIXTURE-V1")
                    AssetDatabase.DeleteAsset(ProbeRoot);
            }
        }

        internal static string ParseSelectedPhysicalGuid(string value)
        {
            // stable key 自体が `|` を含むため、固定 index ではなく末尾の canonical physical GUID を読む。
            var separator = value.LastIndexOf('|');
            var physicalGuid = separator >= 0 ? value.Substring(separator + 1) : string.Empty;
            if (physicalGuid.Length != 32 || physicalGuid.Any(c => !Uri.IsHexDigit(c)))
                throw new InvalidOperationException("Selected content report entry has no canonical physical GUID: " + value);
            return physicalGuid;
        }

        private static void CreateProbe(string token)
        {
            if (Directory.Exists(ProbeRoot))
            {
                if (!File.Exists(ProbeMarker) || File.ReadAllText(ProbeMarker) != "BS4-FIXTURE-V1")
                    throw new InvalidOperationException("BS4 fixture directory is not self-owned.");
                AssetDatabase.DeleteAsset(ProbeRoot);
            }
            Directory.CreateDirectory(ProbeRoot);
            File.WriteAllText(ProbeMarker, "BS4-FIXTURE-V1");
            var go = new GameObject("Bs4ContentOnlyProbe");
            try
            {
                var type = Type.GetType("SampleGame.DependOnAll.Bs4ContentOnlyProbe, SampleGame.DependOnAll")
                    ?? throw new InvalidOperationException("Bs4ContentOnlyProbe type is missing.");
                var component = go.AddComponent(type);
                type.GetMethod("Configure")!.Invoke(component, new object[] { token });
                PrefabUtility.SaveAsPrefabAsset(go, ProbePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static string RuntimeJson(string identity, string representation, string token) => JsonUtility.ToJson(new RuntimeConfig
        {
            content = new ContentConfig { schemaVersion = 1, runtimeMode = "directory", buildIdentity = identity,
                target = "StandaloneWindows64-Player", relativeDirectory = "content", firstScene = "Title", representation = representation, probeToken = token },
            debugSocket = new DebugConfig { enabled = false },
            telemetry = new TelemetryConfig { profiler = new ProfilerConfig { enabled = true } },
            world = new WorldConfig { cellCompanionSet = "Full" }
        }, true);

        [Serializable] private sealed class RuntimeConfig { public ContentConfig content = new(); public DebugConfig debugSocket = new(); public TelemetryConfig telemetry = new(); public WorldConfig world = new(); }
        [Serializable] private sealed class ContentConfig { public int schemaVersion; public string runtimeMode=""; public string buildIdentity=""; public string target=""; public string relativeDirectory=""; public string firstScene=""; public string representation=""; public string probeToken=""; }
        [Serializable] private sealed class DebugConfig { public bool enabled; }
        [Serializable] private sealed class TelemetryConfig { public ProfilerConfig profiler = new(); }
        [Serializable] private sealed class ProfilerConfig { public bool enabled; }
        [Serializable] private sealed class WorldConfig { public string cellCompanionSet = ""; }
        [Serializable] private sealed class BuildReceipt { public string identity=""; public string contentReportPath=""; public string playerBuildGuid=""; public string backend=""; public string stripping=""; public string[] scenes=Array.Empty<string>(); public string[] checkedRootGuids=Array.Empty<string>(); }
    }
}
