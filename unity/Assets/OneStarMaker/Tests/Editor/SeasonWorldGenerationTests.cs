#nullable enable

using System;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using SampleGame.DependOnAll.Editor.WorldAuthoring;
using SampleGame.InGame.Streaming;
using UnityEngine;

namespace OneStarMaker.Tests.Editor
{
    /// <summary>P1 純関数の回帰。AssetDatabase、Scene 作成、生成 Command は呼ばない。</summary>
    public sealed class SeasonWorldGenerationTests
    {
        [Test]
        public void Plan_CoversFourSeasonsAndBothPayloadsWithoutIdentityDuplication()
        {
            var plan = new SeasonWorldGenerationPlan();
            Assert.That(plan.Entries.Count, Is.EqualTo(440));
            Assert.That(plan.Entries.Select(e => e.Identity).Distinct().Count(), Is.EqualTo(440));
            Assert.That(plan.NewAssetPaths.Distinct().Count(), Is.EqualTo(1532));
            Assert.That(plan.NewAssetPaths.Count(p => p.EndsWith(".unity")), Is.EqualTo(652));
            foreach (var season in new[] { "Spring", "Summer", "Autumn", "Winter" })
            {
                var cells = plan.Entries.Where(e => e.IsCell && e.Season == season).ToArray();
                Assert.That(cells.Length, Is.EqualTo(54));
                for (var y = 0; y < 6; y++)
                for (var x = 0; x < 9; x++)
                {
                    var identity = $"{season}_Cell_{x}_{y}";
                    var entry = cells.Single(e => e.Identity == identity);
                    var folder = $"Assets/SampleGame/InGame/InGameSession/Seasons/{season}/Cells/{identity}";
                    Assert.That(entry.ScenePath, Is.EqualTo($"{folder}/{identity}.unity"));
                    Assert.That(entry.WhiteboxPath, Is.EqualTo($"{folder}/Variants/Whitebox/{identity}.unity"));
                    Assert.That(entry.Parent, Is.EqualTo("Season_" + season));
                    var env = plan.Entries.Single(e => e.Parent == identity);
                    Assert.That(env.ScenePath, Is.EqualTo($"{folder}/{season}_Environment_{x}_{y}/{season}_Environment_{x}_{y}.unity"));
                }
            }
            Assert.That(plan.Entries.Count(e => e.ScenePath.Length == 0), Is.EqualTo(4));
            Assert.That(plan.Entries.Count(e => e.LoadType == LoadType.NecessaryAlways), Is.EqualTo(4));
        }

        [TestCase("Spring_Cell_8_5", true)]
        [TestCase("Spring_Cell_08_5", false)]
        [TestCase("Spring_Cell_-1_5", false)]
        [TestCase("Spring_Cell_2147483648_0", false)]
        [TestCase("Cell_8_5", false)]
        [TestCase("Spring_Environment_8_5", false)]
        [TestCase("Rain_Cell_8_5", false)]
        public void Names_ParseOnlyCanonicalCellIdentity(string identity, bool valid)
            => Assert.That(SeasonCellNames.TryParseCell(identity, out _, out _, out _), Is.EqualTo(valid));

        [Test]
        public void Names_ReportTheCoordinateThatIsOutOfRange()
        {
            Assert.That(() => SeasonCellNames.Cell("Spring", -1, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property("ParamName").EqualTo("x"));
            Assert.That(() => SeasonCellNames.Environment("Spring", 0, -1),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property("ParamName").EqualTo("y"));
        }

        [Test]
        public void Shapes_PreserveFloorAndLargeFormsInWhitebox()
        {
            var plan = new SeasonWorldGenerationPlan();
            foreach (var season in SeasonWorldGenerationPlan.Seasons)
            {
                var cells = plan.Entries.Where(e => e.IsCell && e.Season == season).ToArray();
                Assert.That(cells.Count(e => SeasonWorldGenerationPlan.Shapes(e, false).Any(s => s.Name == "Line")), Is.EqualTo(9));
                Assert.That(cells.Count(e => SeasonWorldGenerationPlan.Shapes(e, false).Any(s => s.Name == "Witness")), Is.EqualTo(1));
                Assert.That(cells.Count(e => SeasonWorldGenerationPlan.Shapes(e, false).Any(s => s.Name == "NorthWall")), Is.EqualTo(9));
                foreach (var cell in cells)
                {
                    var full = SeasonWorldGenerationPlan.Shapes(cell, false);
                    var white = SeasonWorldGenerationPlan.Shapes(cell, true);
                    Assert.That(white, Is.EquivalentTo(full.Where(s => !s.Name.StartsWith("Prop_"))));
                    var ground = white.Single(s => s.Name == "Ground");
                    Assert.That(ground.Size, Is.EqualTo(new Vector3(245, 1, 245)));
                    Assert.That(ground.Center.y, Is.EqualTo(0.5f));
                    Assert.That(white.Count(s => s.Collider), Is.EqualTo(1));
                    Assert.That(full.Count(s => s.Collider), Is.EqualTo(1));
                }
            }
            Assert.That(plan.Entries.First(e => e.Season == "Winter").HueOffset, Is.EqualTo(0.24f).Within(0.0001f));
        }

        [TestCase("Assets/SampleGame/InGame/InGameSession/World/World.unity.meta", true)]
        [TestCase("Assets/SampleGame/InGame/InGameSession/World/Cells/Cell_3_3/Cell_3_3.asset", true)]
        [TestCase("Assets/SampleGame/InGame/InGameSession/World/Cells/Cell_0_0/Environment_0_0.unity", true)]
        [TestCase("Assets/SceneGraphData/Nodes/Cells/Environment_3_0.asset", true)]
        [TestCase("Assets/SampleGame/InGame/InGameSession/World", false)]
        [TestCase("Assets/SampleGame/InGame/InGameSession/World/Cells/Cell_0_0", false)]
        [TestCase("Assets/SampleGame/InGame/InGameSession/World/Cells/Cell_0_0/Runtime.cs", false)]
        [TestCase("Assets/SampleGame/InGame/InGameSession/World/Cells/Cell_0_0/Material.mat", false)]
        [TestCase("Assets/SampleGame/InGame/InGameSession/World/Cells/Cell_0_0/../../World.unity", false)]
        [TestCase("Assets/SampleGame/InGame/InGameSession/World/Cells/Cell_4_0/Cell_4_0.unity", false)]
        [TestCase("Assets/SampleGame/InGame/InGameSession/World/Materials/DemoCellLit.mat", false)]
        [TestCase("Assets/SceneGraphData/Nodes/Cells/Cell_0_0Extra.asset", false)]
        public void Wipe_RejectsDirectoriesRuntimeMaterialsAndOutOfRange(string path, bool allowed)
            => Assert.That(SeasonWorldWipe.IsAllowed(path), Is.EqualTo(allowed));

        [Test]
        public void DryRun_PartitionsSnapshotWithoutMutatingIt()
        {
            var assets = new[]
            {
                new SeasonWorldWipe.Asset(SeasonWorldGenerationPlan.OldRoot + "/World.unity", "old", "World", new[] { "lit" }),
                new SeasonWorldWipe.Asset(SeasonWorldGenerationPlan.OldMaterialPath, "lit", "", Array.Empty<string>()),
                new SeasonWorldWipe.Asset(SeasonWorldGenerationPlan.MapPath, "map", "", new[] { "old" }),
            };
            var snapshot = assets.ToArray();
            var manifest = SeasonWorldWipe.DryRun(assets, new[] { SeasonWorldGenerationPlan.MapPath });
            Assert.That(manifest.Delete.Select(a => a.Guid), Is.EqualTo(new[] { "old" }));
            Assert.That(manifest.Keep.Select(a => a.Guid), Is.EqualTo(new[] { "lit" }));
            Assert.That(manifest.Update.Select(a => a.Guid), Is.EqualTo(new[] { "map" }));
            Assert.That(assets, Is.EqualTo(snapshot));
            Assert.That(manifest.ToText(), Does.Contain("meta=").And.Contain("lit"));
        }

        [Test]
        public void DryRun_DoesNotClassifyUnrelatedSceneResourcesAsUpdates()
        {
            var unrelated = new SeasonWorldWipe.Asset("Assets/OneStarMakerCommon/SceneResource/Player.asset",
                "player", "Player", Array.Empty<string>());
            var manifest = SeasonWorldWipe.DryRun(new[] { unrelated }, Array.Empty<string>());
            Assert.That(manifest.Update, Is.Empty);
            Assert.That(manifest.Keep.Single().Guid, Is.EqualTo("player"));
        }

        [Test]
        public void Validation_RejectsMissingWhiteboxWrongPolicyDuplicatesAndMovedPayload()
        {
            var plan = new SeasonWorldGenerationPlan();
            var actual = plan.Entries.Select(e =>
            {
                var o = new SeasonWorldValidation.Observation
                {
                    Identity = e.Identity, Parent = e.Parent, ResourcePath = e.ResourcePath,
                    ResourceGuid = "resource:" + e.Identity, LoadType = e.LoadType, StreamByDistance = e.IsCell,
                };
                if (e.ScenePath.Length > 0) o.Payloads.Add(("", e.ScenePath, "full:" + e.Identity, true));
                if (e.WhiteboxPath.Length > 0) o.Payloads.Add(("Whitebox", e.WhiteboxPath, "white:" + e.Identity, true));
                return o;
            }).ToList();
            Assert.That(SeasonWorldValidation.Compare(plan, actual), Is.Empty);
            var cell = actual.First(o => o.StreamByDistance);
            cell.Payloads.RemoveAt(1);
            Assert.That(SeasonWorldValidation.Compare(plan, actual), Has.Some.Contains("Payload count"));
            cell.StreamByDistance = false;
            cell.Parent = cell.Identity;
            cell.Payloads[0] = ("", "Assets/Wrong.unity", cell.ResourceGuid, false);
            actual.Add(cell);
            var issues = SeasonWorldValidation.Compare(plan, actual);
            Assert.That(issues, Has.Some.Contains("Load policy"));
            Assert.That(issues, Has.Some.Contains("Parent"));
            Assert.That(issues, Has.Some.Contains("Duplicate identity"));
            Assert.That(issues, Has.Some.Contains("Payload variant/path"));
            Assert.That(issues, Has.Some.Contains("Missing/duplicate payload GUID"));
            Assert.That(issues, Has.Some.Contains("Missing Addressable"));

            var environment = actual.First(o => o.Identity.Contains("_Environment_"));
            environment.Generated = false;
            Assert.That(SeasonWorldValidation.Compare(plan, actual), Has.Some.Contains("Not Generated: " + environment.Identity));
        }

        [TestCase("  m_LightingSettings: {fileID: 0}", false)]
        [TestCase("  m_LightingSettings: {fileID: 11400000, guid: 0123456789abcdef0123456789abcdef, type: 2}", true)]
        [TestCase("  m_LightingSettings: {fileID: 42}", true)]
        public void LightingScaffold_DetectsAssignedLightingSettings(string sceneText, bool assigned)
            => Assert.That(SeasonWorldValidation.SceneTextAssignsLightingSettings(sceneText), Is.EqualTo(assigned));

        [Test]
        public void Volume_RejectsNonFiniteSmallEscapingAndStaleSavedBounds()
        {
            var floor = new Bounds(new Vector3(125, 0.5f, 125), new Vector3(245, 1, 245));
            Assert.That(SeasonWorldValidation.CompareVolume(0, 0, floor, floor, default, floor), Is.Empty);
            Assert.That(SeasonWorldValidation.Finite(new Bounds(Vector3.zero, Vector3.one)), Is.False);
            Assert.That(SeasonWorldValidation.Finite(new Bounds(new Vector3(float.NaN, 0, 0), Vector3.one * 2)), Is.False);
            var shifted = floor; shifted.center += Vector3.right * 250;
            var issues = SeasonWorldValidation.CompareVolume(0, 0, floor, shifted, default, shifted);
            Assert.That(issues, Has.Some.Contains("grid"));
            Assert.That(issues, Has.Some.Contains("Whitebox"));
            Assert.That(issues, Has.Some.Contains("Saved"));
            var larger = floor; larger.size += Vector3.up * 2;
            Assert.That(SeasonWorldValidation.SameSeasonSize(floor, larger), Is.True);
            larger.size += Vector3.up * 0.01f;
            Assert.That(SeasonWorldValidation.SameSeasonSize(floor, larger), Is.False);
        }
    }
}
