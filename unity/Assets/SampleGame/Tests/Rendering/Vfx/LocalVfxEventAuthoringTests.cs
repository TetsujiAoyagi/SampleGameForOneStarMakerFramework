#nullable enable

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.Common.Rendering.Vfx;
using SampleGame.InGame.Streaming;
using SampleGame.InGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.VFX;

namespace SampleGame.Tests.Rendering.Vfx
{
    /// <summary>
    /// Cell (4,2) の Events Scene と、そこに置いた局所 VFX の trigger の authoring の約束を検査する（Play には入らない）。
    /// <para>
    /// 約束: Events Scene は trigger・spawn 等の進行制御だけを持ち、VFX・Renderer を置かない（Renderer は Cell の体積へ
    /// 畳まれ、VFX は VFX Scene の所有物）。trigger は同じ GameObject に <c>isTrigger</c> の Collider を持ち、VFX asset を
    /// 直接参照せず Addressables の address（asset path）で指す。Events Scene の追加で Cell の体積は変わらず、
    /// companion の role の分類で Events として Full・Planner のロード対象に入る。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class LocalVfxEventAuthoringTests
    {
        private const string SeasonsRoot = "Assets/SampleGame/InGame/InGameSession/Seasons/";
        private const string CellRoot = SeasonsRoot + "Spring/Cells/Spring_Cell_4_2/";
        private const string CellResourcePath = CellRoot + "Spring_Cell_4_2.asset";
        private const string EventsResourcePath = CellRoot + "Spring_Events_4_2/Spring_Events_4_2.asset";
        private const string EventsScenePath = CellRoot + "Spring_Events_4_2/Spring_Events_4_2.unity";
        private const string Cell = "Spring_Cell_4_2";
        private const string Events = "Spring_Events_4_2";

        // base（9d0c8f2f）の Spring_Cell_4_2 の _volume。Events Scene の追加でこの値が変われば、Events の何かが体積へ入っている。
        private static readonly Bounds BaseCellVolume = new(new Vector3(1125f, 24.5f, 625f), new Vector3(245f, 49f, 245f));

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

        // ═══ AC14 ═══

        [Test]
        public void EventsScene_HasTriggersWithTriggerColliderAndAddressableVfx_AndNoVfxOrRenderer()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(EventsScenePath) != null, Is.True, $"Events Scene が無い: {EventsScenePath}");
            var scene = EditorSceneManager.OpenScene(EventsScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var triggers = roots.SelectMany(root => root.GetComponentsInChildren<LocalVfxEventTrigger>(includeInactive: true)).ToArray();
            Assert.That(triggers, Is.Not.Empty, "trigger が無い");
            var eventsResource = AssetDatabase.LoadAssetAtPath<SceneResource>(EventsResourcePath);
            Assert.That(eventsResource != null, Is.True, $"Events の SceneResource が無い: {EventsResourcePath}");

            foreach (var trigger in triggers)
            {
                var name = trigger.name;
                Assert.That(trigger.GetComponents<Collider>().Any(collider => collider.isTrigger), Is.True,
                    $"{name}: 同じ GameObject に isTrigger の Collider が無い");
                // trigger は address ではなく付随 asset の名前を持つ。.vfx の所在は Events の SceneResource の宣言から引く。
                var assetName = trigger.VfxAsset;
                Assert.That(assetName, Is.Not.Empty, $"{name}: 付随 asset の名前が空");
                Assert.That(eventsResource!.FindAttachedAsset(assetName), Is.Not.Null, $"{name}: Events の SceneResource が付随 asset '{assetName}' を宣言していない");
                var address = AssetDatabase.GUIDToAssetPath(AttachedAssetGuid(eventsResource, assetName));
                TestContext.Out.WriteLine($"trigger={name} asset={assetName} path={address} position={trigger.transform.position:R}");
                Assert.That(address, Is.Not.Empty, $"{name}: 付随 asset の参照先が無い");
                Assert.That(address.StartsWith(SeasonsRoot, StringComparison.Ordinal) && address.EndsWith(".vfx", StringComparison.Ordinal),
                    Is.True, $"{name}: address が Seasons/ 配下の .vfx でない: {address}");
                Assert.That(AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(address) != null, Is.True, $"{name}: .vfx が無い: {address}");
                Assert.That(PlacedVfxAuthoringTests.InspectUpdateMode(File.ReadAllText(address)), Is.Null, $"{name}: .vfx の Update Mode");

                var entry = FindAddressableEntry(AssetDatabase.AssetPathToGUID(address));
                Assert.That(entry, Is.Not.Null, $"{name}: .vfx の Addressables entry が無い");
                Assert.That(GetString(entry!, "address"), Is.EqualTo(address), $"{name}: Addressables の address が asset path でない");
            }

            Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<VisualEffect>(includeInactive: true)), Is.Empty,
                "Events Scene に VisualEffect がある");
            Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<PlacedVfxActivator>(includeInactive: true)), Is.Empty,
                "Events Scene に配置済み VFX の marker がある");
            Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Renderer>(includeInactive: true)), Is.Empty,
                "Events Scene に Renderer がある（Cell の体積へ畳まれる）");
        }

        // ═══ AC15 ═══

        [Test]
        public void EventsResource_IsOnDemandChildOfCell_ClassifiedAsEvents_AndCellVolumeIsUnchanged()
        {
            var cell = AssetDatabase.LoadAssetAtPath<SceneResource>(CellResourcePath);
            var events = AssetDatabase.LoadAssetAtPath<SceneResource>(EventsResourcePath);
            Assert.That(cell != null, Is.True);
            Assert.That(events != null, Is.True, $"Events の SceneResource が無い: {EventsResourcePath}");

            Assert.That(cell!.Volume.center, Is.EqualTo(BaseCellVolume.center), "Cell の体積の中心が base から変わった");
            Assert.That(cell.Volume.size, Is.EqualTo(BaseCellVolume.size), "Cell の体積の寸法が base から変わった");

            Assert.That(events!.Identity, Is.EqualTo(Events));
            Assert.That(events.StreamByDistance, Is.False);
            Assert.That(events.LoadType, Is.EqualTo(LoadType.OnDemand));
            Assert.That(events.Parent != null && events.Parent.Identity == Cell, Is.True, "親が Spring_Cell_4_2 でない");
            Assert.That(cell.Children.Count(child => child != null && child.Identity == Events), Is.EqualTo(1),
                "Cell の Children に Events がちょうど 1 回ない");

            Assert.That(CellCompanionRoleClassifier.TryClassify(events.Identity, out var role), Is.True);
            Assert.That(role, Is.EqualTo(CellCompanionRole.Events));
            Assert.That(CellCompanionPolicy.Includes(CellCompanionSet.Full, role), Is.True);
            Assert.That(CellCompanionPolicy.Includes(CellCompanionSet.Planner, role), Is.True);
        }

        // Addressables の Editor 型はこの asmdef から参照しないので、VfxSceneStructureTests と同じく reflection で読む。
        // この asmdef は Addressables を参照しないので、宣言の参照 GUID は serialized property から読む。
        private static string AttachedAssetGuid(SceneResource resource, string name)
        {
            var attached = new SerializedObject(resource).FindProperty("_attachedAssets");
            for (var i = 0; i < attached.arraySize; i++)
            {
                var element = attached.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("_name").stringValue != name) continue;
                var payloads = element.FindPropertyRelative("_payloads");
                Assert.That(payloads.arraySize, Is.EqualTo(1), $"{name}: payload は 1 件");
                return payloads.GetArrayElementAtIndex(0).FindPropertyRelative("Reference").FindPropertyRelative("m_AssetGUID").stringValue;
            }
            Assert.Fail($"付随 asset '{name}' の宣言が無い");
            return string.Empty;
        }

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

        private static string GetString(object target, string name)
        {
            var type = target.GetType();
            return (string?)(type.GetProperty(name)?.GetValue(target) ?? type.GetField(name)?.GetValue(target)) ?? string.Empty;
        }
    }
}
