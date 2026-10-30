#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Editor.SceneGraph;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.DependOnAll.Editor.Build;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.WorldAuthoring
{
    /// <summary>
    /// Season の Atmosphere VFX と Cell VFX の 2 Scene、および Season 直下の major Event Scene が制作契約どおりの構造で存在し、
    /// content の選択に入ることを、
    /// Scene を開かずに asset から確かめる。SceneGraph source（node・Total.asset の Edge）と生成済み Map のハッシュの照合は
    /// WorldCompanionCreationTransaction の作成後の検査と同じ。体積は、2 Scene を作る前に Recalculate All した値から
    /// 変わっていないことを定数で見る（VFX の描画 bounds が Cell の距離ストリーミング体積へ混ざらないこと）。
    /// </summary>
    [TestFixture]
    public sealed class VfxSceneStructureTests
    {
        private const string MapPath = "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";
        private const string GraphPath = "Assets/SceneGraphData/Graphs/Total.asset";
        private const string SeasonsRoot = "Assets/SampleGame/InGame/InGameSession/Seasons";

        // d3 の Phase B が 2 Scene を作る前に Recalculate All した Spring_Cell_4_2 の _volume（HEAD の保存値とも一致）。
        // VFX Scene の追加でこの値が変われば、VFX の描画 bounds が Cell の体積へ入っている。
        private static readonly Bounds ExpectedCellVolume = new(new Vector3(1125f, 24.5f, 625f), new Vector3(245f, 49f, 245f));

        private static readonly VfxSceneSpec[] Specs =
        {
            new("Spring_AtmosphereVFX", "Season_Spring", LoadType.NecessaryAlways,
                SeasonsRoot + "/Spring/Spring_AtmosphereVFX/Spring_AtmosphereVFX",
                "Assets/SceneGraphData/Nodes/Seasons/Spring_AtmosphereVFX.asset"),
            new("Spring_VFX_4_2", "Spring_Cell_4_2", LoadType.OnDemand,
                SeasonsRoot + "/Spring/Cells/Spring_Cell_4_2/Spring_VFX_4_2/Spring_VFX_4_2",
                "Assets/SceneGraphData/Nodes/Cells/Spring_VFX_4_2.asset"),
            // major Event は VFX Scene ではないが、Season の子としての投影（node・Edge・Map・Addressables・選択）は同じ規則。
            new("Spring_Event_MultiCellProof", "Season_Spring", LoadType.OnDemand,
                SeasonsRoot + "/Spring/Events/Spring_Event_MultiCellProof/Spring_Event_MultiCellProof",
                "Assets/SceneGraphData/Nodes/Seasons/Spring_Event_MultiCellProof.asset"),
        };

        [TestCaseSource(nameof(SpecNames))]
        public void VfxScene_HasSceneResourceNodeEdgeMapAndAddressablesEntry(string identity)
        {
            var spec = Specs.Single(item => item.Identity == identity);
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(spec.AssetStem + ".unity");
            Assert.That(scene != null, Is.True, $".unity が無い: {spec.AssetStem}.unity");
            var sceneGuid = AssetDatabase.AssetPathToGUID(spec.AssetStem + ".unity");

            var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(spec.AssetStem + ".asset");
            Assert.That(resource != null, Is.True, $"SceneResource が無い: {spec.AssetStem}.asset");
            Assert.That(resource!.Identity, Is.EqualTo(spec.Identity));
            Assert.That(resource.LoadType, Is.EqualTo(spec.LoadType));
            Assert.That(resource.StreamByDistance, Is.False);
            Assert.That(resource.Parent != null && resource.Parent.Identity == spec.ParentIdentity, Is.True);
            Assert.That(resource.Parent!.Children, Does.Contain(resource));
            var payloads = resource.GetPayloads();
            Assert.That(payloads, Has.Count.EqualTo(1));
            Assert.That(payloads[0].Variant, Is.Empty);
            Assert.That(payloads[0].Reference!.AssetGUID, Is.EqualTo(sceneGuid));

            var node = AssetDatabase.LoadAssetAtPath<SceneNodeData>(spec.NodePath);
            Assert.That(node != null, Is.True, $"SceneGraph node が無い: {spec.NodePath}");
            Assert.That(LoadAll<SceneNodeData>().Count(item => item.Identity == spec.Identity), Is.EqualTo(1));
            Assert.That(node!.NodeLoadType, Is.EqualTo(spec.LoadType));
            var graph = AssetDatabase.LoadAssetAtPath<SceneGraphEdges>(GraphPath);
            Assert.That(graph != null, Is.True);
            Assert.That(graph!.ContainsNode(node), Is.True);
            var parentNode = graph.GetParent(node);
            Assert.That(parentNode != null && parentNode.Identity == spec.ParentIdentity, Is.True,
                $"Total.asset の親が {spec.ParentIdentity} でない");

            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(MapPath);
            Assert.That(map != null, Is.True);
            SceneResourceGenerator.RebuildMapLookup(map!);
            Assert.That(map!.GetSceneResource(spec.Identity), Is.SameAs(resource));

            // 既存の Cell companion と同じ規則: Default Local Group、address は scene path。
            var entry = FindAddressableEntry(sceneGuid);
            Assert.That(entry, Is.Not.Null, "Addressables entry が無い");
            Assert.That(GetString(entry!, "address"), Is.EqualTo(spec.AssetStem + ".unity"));
            var group = entry!.GetType().GetProperty("parentGroup")!.GetValue(entry);
            Assert.That(GetString(group!, "Name"), Is.EqualTo("Default Local Group"));
        }

        [Test]
        public void SceneGraphSource_MatchesGeneratedMapHash()
        {
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(MapPath);
            Assert.That(map != null, Is.True);
            var hash = SceneResourceGenerator.ComputeCurrentHash(LoadAll<SceneNodeData>(), LoadAll<SceneGraphEdges>());
            Assert.That(map!.GenerateHash, Is.EqualTo(hash), "SceneGraph source と生成済み SceneResourceMap がずれている");
        }

        [Test]
        public void SeasonSpring_ChildrenAreLightingAtmosphereMajorEventAnd54Cells()
        {
            var season = AssetDatabase.LoadAssetAtPath<SceneResource>(SeasonsRoot + "/Spring/Season_Spring.asset");
            Assert.That(season != null, Is.True);
            var children = season!.Children.Select(child => child.Identity).ToArray();
            Assert.That(children, Does.Contain("Spring_Lighting"));
            Assert.That(children, Does.Contain("Spring_AtmosphereVFX"));
            Assert.That(children, Does.Contain("Spring_Event_MultiCellProof"));
            Assert.That(children.Count(identity => identity.StartsWith("Spring_Cell_", StringComparison.Ordinal)), Is.EqualTo(54));
            Assert.That(children, Has.Length.EqualTo(57));
        }

        [Test]
        public void SpringCell42_VolumeIsUnchangedByVfxScene()
        {
            var cell = AssetDatabase.LoadAssetAtPath<SceneResource>(SeasonsRoot + "/Spring/Cells/Spring_Cell_4_2/Spring_Cell_4_2.asset");
            Assert.That(cell != null, Is.True);
            Assert.That(cell!.StreamByDistance, Is.True);
            Assert.That(cell.Volume.center, Is.EqualTo(ExpectedCellVolume.center));
            Assert.That(cell.Volume.size, Is.EqualTo(ExpectedCellVolume.size));
            Assert.That(cell.Children.Select(child => child.Identity), Does.Contain("Spring_VFX_4_2"));
        }

        // 実 node から content の選択を作る（SampleGameContentBuild.BuildResult と同じ materialize と graph の写し）。
        [TestCase(SeasonContentMode.Full)]
        [TestCase(SeasonContentMode.Whitebox)]
        public void SeasonSelection_SpringIncludesVfxAndMajorEventScenes(SeasonContentMode mode)
        {
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(MapPath);
            Assert.That(map != null, Is.True);
            var materialized = new SceneResourceContentMaterializer().Materialize(map!);
            Assert.That(materialized.Snapshot, Is.Not.Null,
                "materialize の失敗: " + string.Join("; ", materialized.Issues.Select(issue => issue.Code + ":" + issue.SubjectKey)));
            var detail = new SeasonSceneSelectionPolicy().SelectDetailed(
                CopyGraph(map!), materialized.Snapshot!.Candidates, materialized.Snapshot.TagProviders, new[] { "Spring" }, mode);
            var selection = detail.Selection;
            Assert.That(selection.Plan, Is.Not.Null,
                "選択の失敗: " + string.Join("; ", selection.Issues.Select(issue => issue.Code + ":" + issue.SubjectKey)));
            var selected = selection.Plan!.SelectedContent.Select(content => content.LogicalKey).ToArray();
            foreach (var spec in Specs)
                Assert.That(selected, Does.Contain(spec.Identity), $"{mode} の選択に {spec.Identity} が入らない");
        }

        private static IEnumerable<string> SpecNames() => Specs.Select(spec => spec.Identity);

        private static IReadOnlyList<SeasonSceneNode> CopyGraph(SceneResourceMap map)
            => map.SceneResources.Select(resource => new SeasonSceneNode(
                    resource.Identity,
                    resource.Parent == null ? null : resource.Parent.Identity,
                    resource.Children.Select(child => child.Identity),
                    resource.GetPayloads().Count != 0))
                .ToList();

        private static List<T> LoadAll<T>() where T : UnityEngine.Object
            => AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToList()!;

        // Addressables の Editor 型はこの asmdef から参照しないので、既存の WorldCompanion のテストと同じく reflection で読む。
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

        private sealed class VfxSceneSpec
        {
            internal VfxSceneSpec(string identity, string parentIdentity, LoadType loadType, string assetStem, string nodePath)
            {
                Identity = identity;
                ParentIdentity = parentIdentity;
                LoadType = loadType;
                AssetStem = assetStem;
                NodePath = nodePath;
            }

            internal string Identity { get; }
            internal string ParentIdentity { get; }
            internal LoadType LoadType { get; }
            internal string AssetStem { get; }
            internal string NodePath { get; }
        }
    }
}
