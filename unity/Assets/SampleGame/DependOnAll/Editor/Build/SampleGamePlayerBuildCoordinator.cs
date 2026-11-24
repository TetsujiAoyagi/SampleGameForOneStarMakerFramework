#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Editor.Build.Content;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    internal static class SampleGamePlayerBuildCoordinator
    {
        private const string ProbePath = "Assets/OneStarMakerGenerated/BS4/Bs4Probe.prefab";
        private const string ProbeRoot = "Assets/OneStarMakerGenerated/BS4";
        private const string ProbeMarker = "Assets/OneStarMakerGenerated/BS4/.bs4-fixture-owner";
        private const string MapPath = "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";
        internal const string BootstrapScene = "Assets/OneStarMaker/Scenes/UISystem/UIScene.unity";
        /// <summary>遊べる Player の開始 Scene（commit した空の Scene。名前は <see cref="DirectoryBootstrapKeys.PlayableStartSceneName"/>）。</summary>
        internal const string PlayableStartScene = "Assets/SampleGame/DependOnAll/PlayerStart/PlayablePlayerStart.unity";

        [MenuItem("Tools/OSM/Content/Build BS4 Spring Player")]
        private static void BuildMenu() => Build(PlayerBuildKind.Smoke);

        [MenuItem("Tools/OSM/Content/Build Spring Playable Player")]
        private static void BuildPlayableMenu() => Build(PlayerBuildKind.Playable);

        /// <summary>
        /// 種類ごとの値（宣言、probe の有無、出力先の folder 名、baked の playerKind）を 1 か所で選ぶ。
        /// 動作確認の Player の値（bs4-spring-full、probe、artifacts/bs4）は変えない。遊べる Player は probe なしの spring-full を
        /// artifacts/playable に出し、動作確認の Player の出力と混ぜない。
        /// </summary>
        internal static PlayerBuildKindSettings SettingsOf(PlayerBuildKind kind) => kind switch
        {
            PlayerBuildKind.Smoke => new PlayerBuildKindSettings(SampleGameBuildPurposes.Bs4SpringFull, usesProbe: true, "bs4", "smoke", copiesBootstrap: true),
            PlayerBuildKind.Playable => new PlayerBuildKindSettings(SampleGameBuildPurposes.SpringFull, usesProbe: false, "playable", "playable", copiesBootstrap: false),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Player build kind."),
        };

        /// <summary>transport・work・staging・players を全て置く root（repo 直下の artifacts/&lt;種類の folder&gt;）。</summary>
        internal static string ArtifactsRootOf(string projectRoot, PlayerBuildKind kind) =>
            Path.GetFullPath(Path.Combine(projectRoot, "..", "artifacts", SettingsOf(kind).ArtifactsDirectoryName));

        internal static PlayerBuildResult Build(PlayerBuildKind kind)
        {
            var settings = SettingsOf(kind);
            if (!settings.UsesProbe)
                return BuildPlayer(kind, settings, string.Empty, null);

            var token = Guid.NewGuid().ToString("N");
            var representation = settings.Declaration.Representation.Primary;
            CreateProbe(token);
            try
            {
                var guid = AssetDatabase.AssetPathToGUID(ProbePath);
                return BuildPlayer(kind, settings, token,
                    (plan, snapshot) =>
                    {
                        var value = Bs4FixtureComposer.Compose(plan, snapshot, guid, ProbePath, representation, token);
                        return (value.Plan, value.Snapshot);
                    });
            }
            finally
            {
                if (File.Exists(ProbeMarker) && File.ReadAllText(ProbeMarker) == "BS4-FIXTURE-V1")
                    AssetDatabase.DeleteAsset(ProbeRoot);
            }
        }

        /// <summary>content の build → publish → Player の build → 重複検査 → publish の共通部分。probe の生成と後始末は呼び出し側が持つ。</summary>
        private static PlayerBuildResult BuildPlayer(PlayerBuildKind kind, PlayerBuildKindSettings settings, string probeToken,
            Func<OneStarMaker.Build.Selection.BuildPlan, OneStarMaker.Editor.Build.Materialization.BuildMaterializationSnapshot,
                (OneStarMaker.Build.Selection.BuildPlan Plan, OneStarMaker.Editor.Build.Materialization.BuildMaterializationSnapshot Snapshot)>? compose)
        {
            // Player の選択・表現・runtime 既定値は宣言から取る。手書きの定数を 2 箇所に持たない。
            var declaration = settings.Declaration;
            // transport へ publish するので Full（workspace に前回の選択の file を残さない）。
            var result = SampleGameContentBuild.BuildResult(declaration, BuildContentMode.Full, compose);
            var input = PlayerBuildInputValidator.Validate(result, File.ReadAllText(result.PreflightPath), File.ReadAllText(result.OutcomePath));
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var artifactsRoot = ArtifactsRootOf(projectRoot, kind);
            var playerWork = Path.Combine(artifactsRoot, "work", result.Identity);
            Directory.CreateDirectory(playerWork);
            // 生成 probe の source が finally で消える前に閉包と hash を採取する。
            // 配信用成果物と Player は同じ build identity を持ち、相互変換しない。
            var transportPath = ContentTransportPublisher.Publish(result,
                Path.Combine(artifactsRoot, "transport", result.Identity));
            var exe = Path.Combine(playerWork, "SampleGame.exe");
            var runtimeJson = RuntimeJson(result.Identity, declaration, probeToken, settings.PlayerKindValue);
            // Player の入力の指紋は mutation（IL2CPP と High、baked config、Resources/BS4 の印）を始める前の project から取る。
            // 入口は mutation の後の手元と比べるので、mutation が一時に変える物を指紋に入れると、自分で build した checkout でも止まる。
            // 動作確認の Player は compat を書かない（出力と receipt を変えない）。
            var compatibility = settings.CopiesBootstrap ? null : PlayerCompatibility.CreateRecord(result.Identity, PlayerInputFiles.Collect());
            using (var mutation = new PlayerBuildProjectMutation(result.Identity, runtimeJson))
            {
                if (!settings.CopiesBootstrap)
                    return BuildPlayableFromStartScene(kind, result, input, artifactsRoot, playerWork, transportPath, exe, mutation, compatibility!);

                mutation.CopyGraphClosure(MapPath);
                var bootstrap = mutation.CopyBootstrapScene(BootstrapScene);
                // 構成の観測（BuildReceipt の backend・stripping は定数なので観測にならない）。mutation が設定した後、build の直前に読む。
                var backendDuringBuild = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone).ToString();
                var strippingDuringBuild = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone).ToString();
                var report = new UnityPlayerBuildAdapter().Build(bootstrap.Path, exe, result.MetadataPath!);
                // preflight.selected は stable|logical|physicalGuid。roots は path を含むため
                // BuildReport の sourceAssetGUID と比較できず、重複検査が常に空振りになる。
                var selectedRootGuids = input.Preflight.selected.Select(ParseSelectedPhysicalGuid).ToArray();
                var verification = PlayerBuildReportVerifier.Verify(report, selectedRootGuids, bootstrap.SourceGuid, bootstrap.GeneratedGuid);
                var receipt = JsonUtility.ToJson(new BuildReceipt
                {
                    identity = result.Identity, contentReportPath = result.OutcomePath,
                    transportPath = transportPath,
                    transportManifestSha256 = HashFile(Path.Combine(transportPath, "transport.json")),
                    target = "StandaloneWindows64-Player",
                    playerBuildGuid = verification.BuildGuid, backend = "IL2CPP", stripping = "High",
                    scenes = new[] { bootstrap.Path }, bootstrapSceneName = "UICommon", checkedRootGuids = verification.CheckedGuids,
                }, true);
                var playerDirectory = new PlayerBuildPublisher().Publish(artifactsRoot, result.Identity, playerWork, result.ContentPath!, receipt);
                return new PlayerBuildResult(kind, result.Identity, playerDirectory, transportPath,
                    report.summary.options.ToString(), backendDuringBuild, strippingDuringBuild);
            }
        }

        /// <summary>
        /// 遊べる Player の build（bootstrap を写さない）。UICommon の Scene と SceneResourceMap・SceneResource を Player に写さず、
        /// commit した空の開始 Scene 1 つだけを Player の Scene にする。Runtime は登録した content から UICommon と map を読む。
        /// mutation（root と所有の印、baked config、IL2CPP と High、戻し）は動作確認の Player と同じで、root には所有の印だけが入る。
        /// </summary>
        private static PlayerBuildResult BuildPlayableFromStartScene(PlayerBuildKind kind, BuildContentResult result, ValidatedPlayerBuildInput input,
            string artifactsRoot, string playerWork, string transportPath, string exe, PlayerBuildProjectMutation mutation,
            PlayerCompatibilityRecord compatibility)
        {
            var startRootCount = CountStartSceneRoots(PlayableStartScene);
            if (startRootCount != 0)
                throw new InvalidOperationException($"Playable start Scene must have no root GameObjects; roots={startRootCount}: {PlayableStartScene}");
            var startSceneGuid = AssetDatabase.AssetPathToGUID(PlayableStartScene);
            if (string.IsNullOrEmpty(startSceneGuid))
                throw new InvalidOperationException("Playable start Scene has no GUID: " + PlayableStartScene);
            var (forbiddenGuids, mapSceneResourceCount) = SceneResourceGuidsOf(MapPath);
            var sourceBootstrapGuid = AssetDatabase.AssetPathToGUID(BootstrapScene);
            if (string.IsNullOrEmpty(sourceBootstrapGuid))
                throw new InvalidOperationException("Production bootstrap Scene is missing: " + BootstrapScene);

            // 判定の record 用。Unity の build の直前に mutation の root の下にある file（遊べる Player では所有の印だけのはず）。
            var mutationRootFiles = Directory.GetFiles(mutation.AssetRoot, "*", SearchOption.AllDirectories)
                .Select(x => x.Substring(mutation.AssetRoot.Length).Replace('\\', '/').TrimStart('/'))
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var backendDuringBuild = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone).ToString();
            var strippingDuringBuild = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone).ToString();
            var report = new UnityPlayerBuildAdapter().Build(PlayableStartScene, exe, result.MetadataPath!);
            var selectedRootGuids = input.Preflight.selected.Select(ParseSelectedPhysicalGuid).ToArray();
            var verification = PlayerBuildReportVerifier.VerifyPlayable(report, selectedRootGuids, sourceBootstrapGuid,
                new PlayablePackChecks(forbiddenGuids, mutation.AssetRoot, startSceneGuid));
            var receipt = JsonUtility.ToJson(new BuildReceipt
            {
                identity = result.Identity, contentReportPath = result.OutcomePath,
                transportPath = transportPath,
                transportManifestSha256 = HashFile(Path.Combine(transportPath, "transport.json")),
                target = "StandaloneWindows64-Player",
                playerBuildGuid = verification.BuildGuid, backend = "IL2CPP", stripping = "High",
                scenes = new[] { PlayableStartScene }, bootstrapSceneName = DirectoryBootstrapKeys.PlayableStartSceneName,
                checkedRootGuids = verification.CheckedGuids,
            }, true);
            // compat は Unity の Player の build が返った後・publish の前に playerWork へ書く。publish が Player の file と一緒に
            // staging から players/<identity> へ原子的に置くので、compat の無い遊べる Player の folder が見える時間が無い。
            File.WriteAllText(Path.Combine(playerWork, PlayerCompatibility.FileName), PlayerCompatibility.ToJson(compatibility),
                new System.Text.UTF8Encoding(false));
            var playerDirectory = new PlayerBuildPublisher().Publish(artifactsRoot, result.Identity, playerWork, result.ContentPath!, receipt);
            var playable = new PlayableBuildObservation(mutationRootFiles, startRootCount, mapSceneResourceCount,
                verification.Playable!.ForbiddenGuidCount, verification.Playable.StartScenePackCount);
            return new PlayerBuildResult(kind, result.Identity, playerDirectory, transportPath,
                report.summary.options.ToString(), backendDuringBuild, strippingDuringBuild, playable);
        }

        /// <summary>開始 Scene を additive で開いて root の GameObject を数え、閉じる。物を置いた開始 Scene で build しない（content 以外の物が Player に入る）。</summary>
        private static int CountStartSceneRoots(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                throw new InvalidOperationException("Playable start Scene is missing: " + path);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try { return scene.rootCount; }
            finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }

        /// <summary>map が参照する各 SceneResource の GUID（遊べる Player に入ってはいけない物）。</summary>
        private static (string[] Guids, int MapCount) SceneResourceGuidsOf(string mapPath)
        {
            var map = AssetDatabase.LoadAssetAtPath<OneStarMaker.Runtime.SceneSystem.SceneResourceMap>(mapPath)
                ?? throw new InvalidOperationException("Production SceneResourceMap is missing.");
            var guids = map.SceneResources.Select(resource =>
            {
                if (resource == null) throw new InvalidOperationException("Production graph contains a null SceneResource.");
                var path = AssetDatabase.GetAssetPath(resource);
                var guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
                return string.IsNullOrEmpty(guid) ? throw new InvalidOperationException("SceneResource is external to AssetDatabase.") : guid;
            }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return (guids, map.SceneResources.Count);
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

        // baked config の contentSet・representation・cellCompanionSet は宣言から写す。Runtime はこの config を読み、
        // directory の root の宣言と突き合わせて不一致を Warning にする。
        // playerKind は Player の種類（smoke / playable）で、Runtime は protected key として baked の生の値と重ねた後の値を比べる。
        internal static string RuntimeJson(string identity, OneStarMaker.Build.Selection.BuildPurposeDeclaration declaration, string token, string playerKind) => JsonUtility.ToJson(new RuntimeConfig
        {
            content = new ContentConfig { schemaVersion = 1, runtimeMode = "directory", buildIdentity = identity,
                contentSet = declaration.Name, target = "StandaloneWindows64-Player", relativeDirectory = "content", firstScene = SampleGameBuildPurposes.FirstSceneOf(declaration.Name),
                representation = declaration.Representation.Primary, probeToken = token, playerKind = playerKind },
            debugSocket = new DebugConfig { enabled = false },
            telemetry = new TelemetryConfig { profiler = new ProfilerConfig { enabled = true } },
            world = new WorldConfig { cellCompanionSet = SampleGameBuildPurposes.CellCompanionSetOf(declaration) }
        }, true);

        [Serializable] private sealed class RuntimeConfig { public ContentConfig content = new(); public DebugConfig debugSocket = new(); public TelemetryConfig telemetry = new(); public WorldConfig world = new(); }
        [Serializable] private sealed class ContentConfig { public int schemaVersion; public string runtimeMode=""; public string buildIdentity=""; public string contentSet=""; public string target=""; public string relativeDirectory=""; public string firstScene=""; public string representation=""; public string probeToken=""; public string playerKind=""; }
        [Serializable] private sealed class DebugConfig { public bool enabled; }
        [Serializable] private sealed class TelemetryConfig { public ProfilerConfig profiler = new(); }
        [Serializable] private sealed class ProfilerConfig { public bool enabled; }
        [Serializable] private sealed class WorldConfig { public string cellCompanionSet = ""; }
        private static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);
            using var hash = System.Security.Cryptography.SHA256.Create();
            return string.Concat(hash.ComputeHash(stream).Select(x => x.ToString("x2")));
        }

        [Serializable]
        private sealed class BuildReceipt
        {
            public string identity = "";
            public string target = "";
            public string contentReportPath = "";
            public string transportPath = "";
            public string transportManifestSha256 = "";
            public string playerBuildGuid = "";
            public string backend = "";
            public string stripping = "";
            public string[] scenes = Array.Empty<string>();
            public string bootstrapSceneName = "";
            public string[] checkedRootGuids = Array.Empty<string>();
        }
    }

    /// <summary>
    /// build する Player の種類。build の手順（宣言・probe・出力先）を選ぶ。Runtime の起動の分岐は baked の文字列
    /// （<see cref="PlayerBuildKindSettings.PlayerKindValue"/>）だけを読み、この enum を知らない（Editor から Runtime の internal は見えない）。
    /// </summary>
    internal enum PlayerBuildKind { Smoke, Playable }

    internal readonly struct PlayerBuildKindSettings
    {
        internal PlayerBuildKindSettings(OneStarMaker.Build.Selection.BuildPurposeDeclaration declaration, bool usesProbe,
            string artifactsDirectoryName, string playerKindValue, bool copiesBootstrap)
        {
            Declaration = declaration; UsesProbe = usesProbe; ArtifactsDirectoryName = artifactsDirectoryName; PlayerKindValue = playerKindValue;
            CopiesBootstrap = copiesBootstrap;
        }
        /// <summary>
        /// UICommon の Scene と SceneResourceMap・SceneResource を Player に写し、生成した UICommon の Scene から始めるか（動作確認の Player）。
        /// 偽なら写さず、空の開始 Scene から始めて content から読む（遊べる Player）。
        /// </summary>
        internal bool CopiesBootstrap { get; }
        internal OneStarMaker.Build.Selection.BuildPurposeDeclaration Declaration { get; }
        /// <summary>動作確認の fixture（probe の prefab）を content に合成するか。</summary>
        internal bool UsesProbe { get; }
        internal string ArtifactsDirectoryName { get; }
        /// <summary>baked config の <c>content:playerKind</c> に書く値。</summary>
        internal string PlayerKindValue { get; }
    }

    /// <summary>build の結果と構成の観測（判定の record 用）。BuildReceipt とは別で、publish する file には書かない。</summary>
    internal sealed class PlayerBuildResult
    {
        internal PlayerBuildResult(PlayerBuildKind kind, string identity, string playerDirectory, string transportDirectory,
            string buildOptions, string scriptingBackendDuringBuild, string managedStrippingDuringBuild, PlayableBuildObservation? playable = null)
        {
            Kind = kind; Identity = identity; PlayerDirectory = playerDirectory; TransportDirectory = transportDirectory;
            BuildOptions = buildOptions; ScriptingBackendDuringBuild = scriptingBackendDuringBuild; ManagedStrippingDuringBuild = managedStrippingDuringBuild;
            Playable = playable;
        }
        /// <summary>遊べる Player（bootstrap を写さない build）の時だけ。</summary>
        internal PlayableBuildObservation? Playable { get; }
        internal PlayerBuildKind Kind { get; }
        internal string Identity { get; }
        /// <summary>publish した <c>players/&lt;identity&gt;</c>。</summary>
        internal string PlayerDirectory { get; }
        internal string TransportDirectory { get; }
        /// <summary>build report の summary の options（<c>Development</c> を含まないことを見る）。</summary>
        internal string BuildOptions { get; }
        internal string ScriptingBackendDuringBuild { get; }
        internal string ManagedStrippingDuringBuild { get; }
    }

    /// <summary>遊べる Player の build の観測（判定の record 用）。publish する file には書かない。</summary>
    internal sealed class PlayableBuildObservation
    {
        internal PlayableBuildObservation(string[] mutationRootFilesBeforeBuild, int startSceneRootCount, int mapSceneResourceCount,
            int forbiddenGuidCount, int startScenePackCount)
        {
            MutationRootFilesBeforeBuild = mutationRootFilesBeforeBuild; StartSceneRootCount = startSceneRootCount;
            MapSceneResourceCount = mapSceneResourceCount; ForbiddenGuidCount = forbiddenGuidCount; StartScenePackCount = startScenePackCount;
        }
        /// <summary>Unity の build の直前に mutation の root の下にあった file（root からの相対）。所有の印だけのはず。</summary>
        internal string[] MutationRootFilesBeforeBuild { get; }
        /// <summary>開始 Scene の root の GameObject の数（0 でなければ build しない）。</summary>
        internal int StartSceneRootCount { get; }
        /// <summary>map が参照する SceneResource の数（GUID の重複を除く）。</summary>
        internal int MapSceneResourceCount { get; }
        /// <summary>verifier に渡した「入ってはいけない GUID」の件数。</summary>
        internal int ForbiddenGuidCount { get; }
        /// <summary>開始 Scene の GUID を持つ pack の数（0 か 1）。</summary>
        internal int StartScenePackCount { get; }
    }
}
