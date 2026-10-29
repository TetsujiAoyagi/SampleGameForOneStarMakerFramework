#nullable enable

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.Rendering.Environments;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.CameraSystem;
using SampleGame.Common.Rendering.Vfx;
using SampleGame.DependOnAll;
using SampleGame.InGame.Streaming;
using SampleGame.InGame.World;
using SampleGame.Tests.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.VFX;
using RuntimeCameraSystem = OneStarMaker.Runtime.CameraSystem.Core.CameraSystem;

namespace SampleGame.Tests.Rendering.Vfx
{
    /// <summary>
    /// Season 直下の major Event Scene（Spring_Event_MultiCellProof）と、Cell (4,2) の Events Scene に置いた開始の trigger の
    /// authoring の約束を検査する（Play には入らない）。
    /// <para>
    /// 約束: Event Scene は Season_Spring の OnDemand・非 streaming の子で、payload は Full のみ（Whitebox build と共有）。
    /// Addressables entry（Default Local Group、address は scene path）を持つ。SceneGraph の node・Edge・生成済み Map は
    /// Editor の型が要るので <c>VfxSceneStructureTests</c> が見る。
    /// Event Scene は VFX の起動位置と完了の trigger だけを持ち、Renderer・<c>VisualEffect</c>・配置済み VFX の marker を置かない
    /// （VFX は Event Scene 所有で起動する）。起動位置は Cell (4,2) と (5,2) の体積の中に 1 つずつ、完了の trigger は (5,2)、
    /// 開始の trigger は (4,2) の体積の中にある。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class MajorEventAuthoringTests
    {
        private const string SpringRoot = "Assets/SampleGame/InGame/InGameSession/Seasons/Spring";
        private const string EventFolder = SpringRoot + "/Events/Spring_Event_MultiCellProof";
        private const string EventScenePath = EventFolder + "/Spring_Event_MultiCellProof.unity";
        private const string EventResourcePath = EventFolder + "/Spring_Event_MultiCellProof.asset";
        private const string SeasonResourcePath = SpringRoot + "/Season_Spring.asset";
        private const string Cell42ResourcePath = SpringRoot + "/Cells/Spring_Cell_4_2/Spring_Cell_4_2.asset";
        private const string Cell52ResourcePath = SpringRoot + "/Cells/Spring_Cell_5_2/Spring_Cell_5_2.asset";
        private const string CellEventsScenePath = SpringRoot + "/Cells/Spring_Cell_4_2/Spring_Events_4_2/Spring_Events_4_2.unity";
        private const string Event = "Spring_Event_MultiCellProof";

        private SceneSetup[]? _setup;

        [SetUp]
        public void SetUp()
        {
            _setup = EditorSceneManager.GetSceneManagerSetup();
        }

        [TearDown]
        public void TearDown()
        {
            if (_setup != null && _setup.Any(scene => scene.isLoaded && scene.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(_setup);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // ═══ AC12: SceneResource・Addressables ═══

        [Test]
        public void EventResource_IsOnDemandFullOnlySeasonChild_WithAddressablesEntry()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(EventScenePath) != null, Is.True, $".unity が無い: {EventScenePath}");
            var sceneGuid = AssetDatabase.AssetPathToGUID(EventScenePath);

            var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(EventResourcePath);
            Assert.That(resource != null, Is.True, $"SceneResource が無い: {EventResourcePath}");
            Assert.That(resource!.Identity, Is.EqualTo(Event));
            Assert.That(resource.LoadType, Is.EqualTo(LoadType.OnDemand));
            Assert.That(resource.StreamByDistance, Is.False);
            Assert.That(resource.Parent != null && resource.Parent.Identity == "Season_Spring", Is.True, "親が Season_Spring でない");
            Assert.That(resource.Parent!.Children.Count(child => child == resource), Is.EqualTo(1));
            var payloads = resource.GetPayloads();
            Assert.That(payloads, Has.Count.EqualTo(1), "payload は Full のみ");
            // payload の参照先（Addressables の型）は、この asmdef から参照しないので VfxSceneStructureTests が見る。
            Assert.That(payloads[0].Variant, Is.Empty);

            var entry = FindAddressableEntry(sceneGuid);
            Assert.That(entry, Is.Not.Null, "Addressables entry が無い");
            Assert.That(GetString(entry!, "address"), Is.EqualTo(EventScenePath));
            Assert.That(GroupName(entry!), Is.EqualTo("Default Local Group"));
        }

        // ═══ AC12: Event Scene の中身 ═══

        [Test]
        public void EventScene_HasTwoAnchorsInCells42And52_CompletionTriggerIn52_AndNoVfxOrRenderer()
        {
            var cell42 = LoadResource(Cell42ResourcePath);
            var cell52 = LoadResource(Cell52ResourcePath);
            var scene = EditorSceneManager.OpenScene(EventScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();

            var anchors = roots.SelectMany(root => root.GetComponentsInChildren<MajorEventVfxAnchor>(includeInactive: true)).ToArray();
            foreach (var anchor in anchors)
            {
                TestContext.Out.WriteLine($"anchor={anchor.name} position={anchor.transform.position:R} address={anchor.VfxAddress}");
                AssertVfxAddress(anchor.name, anchor.VfxAddress);
            }

            Assert.That(anchors, Has.Length.EqualTo(2), "起動位置は 2 つ");
            Assert.That(anchors.Count(anchor => cell42.Volume.Contains(anchor.transform.position)), Is.EqualTo(1), "Cell (4,2) の体積の中の起動位置が 1 つでない");
            Assert.That(anchors.Count(anchor => cell52.Volume.Contains(anchor.transform.position)), Is.EqualTo(1), "Cell (5,2) の体積の中の起動位置が 1 つでない");

            var completions = roots.SelectMany(root => root.GetComponentsInChildren<MajorEventCompletionTrigger>(includeInactive: true)).ToArray();
            Assert.That(completions, Has.Length.EqualTo(1), "完了の trigger は 1 つ");
            Assert.That(completions[0].GetComponents<Collider>().Any(collider => collider.isTrigger), Is.True, "完了の trigger に isTrigger の Collider が無い");
            Assert.That(cell52.Volume.Contains(completions[0].transform.position), Is.True, "完了の trigger が Cell (5,2) の体積の外");

            Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<VisualEffect>(includeInactive: true)), Is.Empty,
                "Event Scene に VisualEffect がある");
            Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<PlacedVfxActivator>(includeInactive: true)), Is.Empty,
                "Event Scene に配置済み VFX の marker がある");
            Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Renderer>(includeInactive: true)), Is.Empty,
                "Event Scene に Renderer がある");
        }

        // ═══ AC12: 開始の trigger ═══

        [Test]
        public void CellEventsScene_HasStartTriggerIn42_ForAnExistingMajorEventSeasonChild()
        {
            var cell42 = LoadResource(Cell42ResourcePath);
            var season = LoadResource(SeasonResourcePath);
            var scene = EditorSceneManager.OpenScene(CellEventsScenePath, OpenSceneMode.Single);
            var triggers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MajorEventStartTrigger>(includeInactive: true))
                .ToArray();

            Assert.That(triggers, Has.Length.EqualTo(1), "開始の trigger は 1 つ");
            var trigger = triggers[0];
            TestContext.Out.WriteLine($"start={trigger.name} position={trigger.transform.position:R} event={trigger.EventIdentity}");
            Assert.That(trigger.GetComponents<Collider>().Any(collider => collider.isTrigger), Is.True, "開始の trigger に isTrigger の Collider が無い");
            Assert.That(cell42.Volume.Contains(trigger.transform.position), Is.True, "開始の trigger が Cell (4,2) の体積の外");

            var target = season.Children.SingleOrDefault(child => child != null && child.Identity == trigger.EventIdentity);
            Assert.That(target != null, Is.True, $"開始の trigger の event が Season_Spring の子に無い: {trigger.EventIdentity}");
            var factory = new GameSceneFactory(
                NullLoggerFactory.Instance,
                new RuntimeCameraSystem(new FakeCameraBackend()),
                new NoopCameraBackgroundApplier(),
                CellCompanionSet.Full,
                new UnusedRenderEnvironment(),
                new RecordingVfxSystem());
            Assert.That(factory.CreateSceneClass(target!, new StubQuery(), new StubController()), Is.TypeOf<SeasonMajorEventScene>());
        }

        private static void AssertVfxAddress(string name, string address)
        {
            Assert.That(address, Is.Not.Empty, $"{name}: address が空");
            Assert.That(Path.GetDirectoryName(address)!.Replace('\\', '/'), Is.EqualTo(EventFolder), $"{name}: .vfx が Event Scene と同じフォルダに無い");
            Assert.That(address.EndsWith(".vfx", StringComparison.Ordinal), Is.True, $"{name}: address が .vfx でない");
            Assert.That(AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(address) != null, Is.True, $"{name}: .vfx が無い: {address}");
            Assert.That(PlacedVfxAuthoringTests.InspectUpdateMode(File.ReadAllText(address)), Is.Null, $"{name}: .vfx の Update Mode");
            var entry = FindAddressableEntry(AssetDatabase.AssetPathToGUID(address));
            Assert.That(entry, Is.Not.Null, $"{name}: .vfx の Addressables entry が無い");
            Assert.That(GetString(entry!, "address"), Is.EqualTo(address), $"{name}: Addressables の address が asset path でない");
        }

        private static SceneResource LoadResource(string path)
        {
            var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(path);
            Assert.That(resource != null, Is.True, $"SceneResource が無い: {path}");
            return resource!;
        }

        // Addressables の Editor 型はこの asmdef から参照しないので、VfxSceneStructureTests と同じく reflection で読む。
        private static object? FindAddressableEntry(string guid)
        {
            var type = Type.GetType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor");
            var settings = type?.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            Assert.That(settings, Is.Not.Null, "AddressableAssetSettings が無い");
            foreach (var group in (IEnumerable)settings!.GetType().GetProperty("groups")!.GetValue(settings)!)
            {
                if (group == null) continue;
                foreach (var entry in (IEnumerable)group.GetType().GetProperty("entries")!.GetValue(group)!)
                    if (entry != null && GetString(entry, "guid") == guid) return entry;
            }

            return null;
        }

        private static string GroupName(object entry)
            => GetString(entry.GetType().GetProperty("parentGroup")!.GetValue(entry)!, "Name");

        private static string GetString(object target, string name)
        {
            var type = target.GetType();
            return (string?)(type.GetProperty(name)?.GetValue(target) ?? type.GetField(name)?.GetValue(target)) ?? string.Empty;
        }

        private sealed class NoopCameraBackgroundApplier : ICameraBackgroundApplier
        {
            public void SetClearFlag(ICameraView view, ClearFlag clearFlag, Color color)
            {
            }
        }

        private sealed class UnusedRenderEnvironment : IRenderEnvironment
        {
            public bool HasActiveOwner => false;

            public RenderEnvironmentLease Acquire(object ownerKey)
                => throw new InvalidOperationException("authoring の検査は RenderEnvironment を使わない");
        }

        private sealed class StubQuery : ISceneQuery
        {
            public SceneBase? GetLoadedScene(string identity) => null;
            public bool IsSceneLoaded(string identity) => false;
            public bool IsSceneStable(string identity) => false;
        }

        private sealed class StubController : ISceneController
        {
            public Cysharp.Threading.Tasks.UniTask AddScene(string sceneIdentify, Func<Cysharp.Threading.Tasks.UniTask>? afterOnLoadedTask, System.Threading.CancellationToken ct, SceneContext? context = null, IProgress<SceneLoadProgress>? progress = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.None, System.Collections.Generic.IReadOnlyDictionary<string, string>? telemetryTags = null, int priority = 100, OneStarMaker.Foundation.Telemetry.TelemetryLevel telemetryLevel = OneStarMaker.Foundation.Telemetry.TelemetryLevel.Summary)
                => Cysharp.Threading.Tasks.UniTask.CompletedTask;

            public Cysharp.Threading.Tasks.UniTask UnloadScene(string sceneIdentify, LoadingDisplayType loadingDisplay = LoadingDisplayType.None, System.Collections.Generic.IReadOnlyDictionary<string, string>? telemetryTags = null, OneStarMaker.Foundation.Telemetry.TelemetryLevel telemetryLevel = OneStarMaker.Foundation.Telemetry.TelemetryLevel.Summary)
                => Cysharp.Threading.Tasks.UniTask.CompletedTask;

            public Cysharp.Threading.Tasks.UniTask SwitchScene(string? fromSceneIdentify, string toSceneIdentify, System.Threading.CancellationToken ct, SceneContext? context = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen, System.Collections.Generic.IReadOnlyDictionary<string, string>? telemetryTags = null)
                => Cysharp.Threading.Tasks.UniTask.CompletedTask;

            public Cysharp.Threading.Tasks.UniTask GoBack(System.Threading.CancellationToken ct, SceneContext? context = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen, System.Collections.Generic.IReadOnlyDictionary<string, string>? telemetryTags = null)
                => Cysharp.Threading.Tasks.UniTask.CompletedTask;

            public void ClearHistory()
            {
            }
        }
    }
}
