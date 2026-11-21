#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// 欠けを許す materialize（受け入れ条件 AC3）。今の <see cref="SceneResourceContentMaterializer.Materialize"/> は欠けた入力に今と同じ issue を返し、
    /// 新しい口は同じ入力で snapshot を返して、欠けた payload の候補（stable key は同じ）と欠けの記録を残し、その候補に閉包を持たせない。
    /// 構造の失敗は新しい口でも issue のまま。偽の AssetDatabase で確かめる。
    /// </summary>
    public sealed class MaterializeAllowingAbsentSourcesTests
    {
        private static readonly string GuidA = new('a', 32), GuidW = new('b', 32), GuidB = new('c', 32), GuidT = new('d', 32), GuidP = new('e', 32);
        private readonly List<UnityEngine.Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in _objects) if (value != null) UnityEngine.Object.DestroyImmediate(value);
            _objects.Clear();
        }

        // A（Full と Whitebox）・B が T を参照する。B は付随 asset P を持つ。
        private (SceneResourceMap Map, Gateway Gateway) World()
        {
            var a = CreateResource("A", new AssetPayload(string.Empty, new AssetReference(GuidA)), new AssetPayload("Whitebox", new AssetReference(GuidW)));
            var b = CreateResource("B", new AssetPayload(string.Empty, new AssetReference(GuidB)));
            Attach(b, "Prop", new AssetPayload(string.Empty, new AssetReference(GuidP)));
            var gateway = new Gateway();
            gateway.Add(GuidA, "Assets/F/A.unity", "Assets/F/T.png");
            gateway.Add(GuidW, "Assets/F/AW.unity", "Assets/F/T.png");
            gateway.Add(GuidB, "Assets/F/B.unity", "Assets/F/T.png");
            gateway.Add(GuidT, "Assets/F/T.png");
            gateway.Add(GuidP, "Assets/F/P.prefab");
            return (CreateMap(a, b), gateway);
        }

        [Test]
        public void RootFileMissing_CurrentMaterializeKeepsItsIssue_NewEntryKeepsTheCandidateWithAnAbsenceAndNoClosure()
        {
            var (map, gateway) = World();
            var full = new SceneResourceContentMaterializer(gateway).Materialize(map).Snapshot!;
            gateway.Files.Remove("Assets/F/B.unity");

            var current = new SceneResourceContentMaterializer(gateway).Materialize(map);
            Assert.That(current.Snapshot, Is.Null);
            Assert.That(current.Issues.Select(x => x.Code), Is.EqualTo(new[] { BuildMaterializationIssueCode.MissingRootAsset }));

            var result = new SceneResourceContentMaterializer(gateway).MaterializeAllowingAbsentSources(map);
            Assert.That(result.HasErrors, Is.False);
            var snapshot = result.Snapshot!;
            Assert.That(snapshot.Candidates.Select(x => x.StableKey), Is.EqualTo(full.Candidates.Select(x => x.StableKey)), "stable key は欠けの無い時と同じ");
            var absence = snapshot.Absences.Single();
            var b = snapshot.Candidates.Single(x => x.PhysicalKey == GuidB);
            Assert.That(absence.StableKey, Is.EqualTo(b.StableKey));
            Assert.That(absence.LogicalKey, Is.EqualTo("B"));
            Assert.That(absence.RootGuid, Is.EqualTo(GuidB));
            Assert.That(absence.Reason, Is.EqualTo(BuildMaterializationAbsenceReason.RootFileMissing));
            Assert.That(absence.MissingPaths, Is.EqualTo(new[] { "Assets/F/B.unity" }));
            Assert.That(snapshot.FindDependency(GuidB), Is.Null, "欠けた候補は閉包を持たない");
            Assert.That(snapshot.Dependencies.Count, Is.EqualTo(full.Dependencies.Count - 1));
        }

        [Test]
        public void RootGuidUnresolved_GivesAnAbsenceWithoutMissingPaths()
        {
            var (map, gateway) = World();
            gateway.Forget(GuidB);
            Assert.That(new SceneResourceContentMaterializer(gateway).Materialize(map).Issues.Select(x => x.Code),
                Is.EqualTo(new[] { BuildMaterializationIssueCode.UnresolvedRootGuid }));
            var absence = new SceneResourceContentMaterializer(gateway).MaterializeAllowingAbsentSources(map).Snapshot!.Absences.Single();
            Assert.That(absence.Reason, Is.EqualTo(BuildMaterializationAbsenceReason.RootUnresolved));
            Assert.That(absence.MissingPaths, Is.Empty, "計画が閉包の記録の path で補う");
            Assert.That(absence.RootPath, Is.Null);
        }

        [Test]
        public void DependencyFileMissing_GivesADependencyAbsence()
        {
            var (map, gateway) = World();
            gateway.Files.Remove("Assets/F/T.png");
            Assert.That(new SceneResourceContentMaterializer(gateway).Materialize(map).Issues.Select(x => x.Code).Distinct(),
                Is.EqualTo(new[] { BuildMaterializationIssueCode.MissingDependencyAsset }));
            var snapshot = new SceneResourceContentMaterializer(gateway).MaterializeAllowingAbsentSources(map).Snapshot!;
            Assert.That(snapshot.Absences.Select(x => x.RootGuid + ":" + x.Reason), Is.EquivalentTo(new[]
            {
                GuidA + ":DependencyMissing", GuidW + ":DependencyMissing", GuidB + ":DependencyMissing"
            }));
            Assert.That(snapshot.Absences.All(x => x.MissingPaths.SequenceEqual(new[] { "Assets/F/T.png" })), Is.True);
            Assert.That(snapshot.Absences.All(x => x.RootPath != null), Is.True);
        }

        [Test]
        public void AbsentAttachedAsset_IsKeptAsACandidateWithAnAbsence()
        {
            var (map, gateway) = World();
            gateway.Files.Remove("Assets/F/P.prefab");
            var snapshot = new SceneResourceContentMaterializer(gateway).MaterializeAllowingAbsentSources(map).Snapshot!;
            Assert.That(snapshot.Absences.Single().LogicalKey, Is.EqualTo("B:Prop"));
            Assert.That(snapshot.Candidates.Single(x => x.PhysicalKey == GuidP).Owner, Is.EqualTo("B"));
        }

        [Test]
        public void AbsentUnselectedRepresentation_IsExcludedBySelection_AndTheProjectionAcceptsThePlan()
        {
            var (map, gateway) = World();
            gateway.Files.Remove("Assets/F/AW.unity");
            var snapshot = new SceneResourceContentMaterializer(gateway).MaterializeAllowingAbsentSources(map).Snapshot!;
            var policy = new BuildSelectionPolicy(new BuildTagSchema(new[]
                { new KeyValuePair<string, IEnumerable<string>>("Representation", new[] { "Full", "Whitebox" }) }), snapshot.Requirements);
            var selection = new BuildTagSelector().Select(new BuildRequest(new[] { new BuildTag("Representation", "Full") }),
                snapshot.Candidates, snapshot.TagProviders, policy);
            Assert.That(selection.IsSuccess, Is.True);
            var whitebox = snapshot.Candidates.Single(x => x.PhysicalKey == GuidW);
            Assert.That(selection.Plan!.ExcludedContent.Select(x => x.Candidate.StableKey), Does.Contain(whitebox.StableKey));
            Assert.That(BuildContentProjection.Create(selection.Plan, snapshot).IsValid, Is.True, "除外した欠けた候補は snapshot に在れば足りる");
        }

        [Test]
        public void SelectedAbsentRoot_IsStoppedByTheProjection()
        {
            var (map, gateway) = World();
            gateway.Files.Remove("Assets/F/B.unity");
            var snapshot = new SceneResourceContentMaterializer(gateway).MaterializeAllowingAbsentSources(map).Snapshot!;
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), snapshot.Candidates, Array.Empty<BuildContentExclusion>());
            var projection = BuildContentProjection.Create(plan, snapshot);
            Assert.That(projection.Issues.Select(x => x.Code), Does.Contain(BuildContentIssueCode.MissingClosure));
        }

        [Test]
        public void StructuralFailures_StayIssues_OnTheNewEntry()
        {
            var (map, gateway) = World();
            gateway.WrongGuidFor = "Assets/F/B.unity";
            var result = new SceneResourceContentMaterializer(gateway).MaterializeAllowingAbsentSources(map);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.Issues.Select(x => x.Code), Does.Contain(BuildMaterializationIssueCode.RootGuidRoundTripMismatch));

            var nullMap = new SceneResourceContentMaterializer(gateway).MaterializeAllowingAbsentSources(null);
            Assert.That(nullMap.Snapshot, Is.Null);
            Assert.That(nullMap.Issues.Select(x => x.Code), Is.EqualTo(new[] { BuildMaterializationIssueCode.NullMap }));
        }

        [Test]
        public void CurrentMaterialize_HasNoAbsences()
        {
            var (map, gateway) = World();
            Assert.That(new SceneResourceContentMaterializer(gateway).Materialize(map).Snapshot!.Absences, Is.Empty);
            Assert.That(new SceneResourceContentMaterializer(gateway).MaterializeAllowingAbsentSources(map).Snapshot!.Absences, Is.Empty);
        }

        // ─── fixture ───

        private SceneResourceMap CreateMap(params SceneResource[] resources)
        {
            var map = ScriptableObject.CreateInstance<SceneResourceMap>();
            _objects.Add(map);
            Set(map, "_sceneResources", new List<SceneResource>(resources));
            return map;
        }

        private SceneResource CreateResource(string identity, params AssetPayload[] payloads)
        {
            var resource = ScriptableObject.CreateInstance<SceneResource>();
            _objects.Add(resource);
            resource.Identity = identity;
            Set(resource, "_sceneAssetDescription", new SceneAssetDescription(identity, LoadType.OnDemand, payloads.ToList()));
            return resource;
        }

        private static void Attach(SceneResource resource, string name, params AssetPayload[] payloads)
        {
            var list = (List<AttachedAssetDescription>)typeof(SceneResource)
                .GetField("_attachedAssets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resource);
            list.Add(new AttachedAssetDescription(name, payloads.ToList()));
        }

        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

        // GUID → path、file の有無、path ごとの依存（Unity の GetDependencies の答え）を別々に変えられる偽の AssetDatabase。
        private sealed class Gateway : IAssetDatabaseGateway
        {
            private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);
            private readonly Dictionary<string, string[]> _dependencies = new(StringComparer.Ordinal);
            public HashSet<string> Files { get; } = new(StringComparer.Ordinal);
            public string? WrongGuidFor { get; set; }
            public void Add(string guid, string path, params string[] dependencies)
            {
                _paths[guid] = path;
                Files.Add(path);
                _dependencies[path] = new[] { path }.Concat(dependencies).ToArray();
            }
            public void Forget(string guid) { Files.Remove(_paths[guid]); _paths.Remove(guid); }
            public string GuidToPath(string guid) => _paths.TryGetValue(guid, out var path) ? path : string.Empty;
            public string PathToGuid(string path) => path == WrongGuidFor ? new string('9', 32) : _paths.FirstOrDefault(x => x.Value == path).Key ?? string.Empty;
            public string[] GetDependencies(string path) => _dependencies.TryGetValue(path, out var value) ? value : new[] { path };
            public bool FileExists(string path) => Files.Contains(path);
            public bool IsFolder(string path) => false;
        }
    }
}
