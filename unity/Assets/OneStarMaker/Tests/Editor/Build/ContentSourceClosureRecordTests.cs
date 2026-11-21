#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// 閉包の記録（manifest の <c>sourceClosure</c>）の作成と読み口（受け入れ条件 AC1）。正しい記録は root ごとの閉包として返り、
    /// 欄が無い・版 0 は「記録なし」、版 2 以上は「未対応」、値の壊れた記録は理由付きの「壊れている」で、どれも閉包を返さない。
    /// 新しい欄を持つ manifest は今の必須欄の検査と索引の読み口に同じ答えを返させる。
    /// </summary>
    public sealed class ContentSourceClosureRecordTests
    {
        private static (OverlayPlanWorld World, OverlayPlanWorld.BaseManifest Base) Fixture()
        {
            var world = new OverlayPlanWorld();
            world.Put("Assets/F/T.png");
            world.Put("Assets/F/A.unity");
            world.Put("Assets/F/AW.unity");
            world.Put("Assets/F/B.unity");
            var roots = new[]
            {
                world.Root("A", "Full", "Assets/F/A.unity", "Assets/F/T.png"),
                world.Root("A", "Whitebox", "Assets/F/AW.unity", "Assets/F/T.png"),
                world.Root("B", "Full", "Assets/F/B.unity", "Assets/F/T.png"),
            };
            return (world, world.Publish(roots));
        }

        [Test]
        public void Read_ValidRecord_ReturnsEachRootsClosure_AndTwoRepresentationsKeepSeparateClosures()
        {
            var (_, baseManifest) = Fixture();
            var read = ContentSourceClosureRecord.Read(baseManifest.Manifest);
            Assert.That(read.State, Is.EqualTo(ContentSourceClosureState.Present), read.Detail);
            Assert.That(read.Roots.Select(x => x.LogicalKey + "/" + x.Kind + "/" + x.Representation),
                Is.EqualTo(new[] { "A/Scene/Full", "A/Scene/Whitebox", "B/Scene/Full" }));
            var full = read.Roots.Single(x => x.Representation == "Full" && x.LogicalKey == "A");
            var whitebox = read.Roots.Single(x => x.Representation == "Whitebox");
            Assert.That(full.Assets.Select(x => x.Path), Is.EqualTo(new[] { "Assets/F/A.unity", "Assets/F/T.png" }));
            Assert.That(whitebox.Assets.Select(x => x.Path), Is.EqualTo(new[] { "Assets/F/AW.unity", "Assets/F/T.png" }), "表現ごとに別の閉包");
            Assert.That(full.Assets.Select(x => x.Guid), Does.Contain(full.Guid));
        }

        [Test]
        public void Create_SharedAssetIsWrittenOnce_WithOneGroupNamingEveryRootThatUsesIt()
        {
            var (_, baseManifest) = Fixture();
            var record = baseManifest.Manifest.sourceClosure;
            Assert.That(record.version, Is.EqualTo(1));
            Assert.That(record.assets.Count(x => x.path == "Assets/F/T.png"), Is.EqualTo(1));
            var shared = record.assets.Single(x => x.path == "Assets/F/T.png");
            Assert.That(record.groups[shared.group].roots, Is.EqualTo(new[] { 0, 1, 2 }), "T は 3 つの root の閉包にある");
            Assert.That(record.groups.Length, Is.EqualTo(4), "root ごとの 1 組と、共有の 1 組");
        }

        [Test]
        public void Read_NoFieldOrVersionZero_IsMissing_VersionTwo_IsUnsupported_AndNeitherReturnsClosures()
        {
            var (_, baseManifest) = Fixture();
            var manifest = baseManifest.Manifest;
            manifest.sourceClosure = new ContentSourceClosure();
            Assert.That(ContentSourceClosureRecord.Read(manifest).State, Is.EqualTo(ContentSourceClosureState.Missing));
            var withoutField = JsonUtility.FromJson<ContentTransportManifest>(
                "{\"version\":1,\"contentSet\":\"s\",\"revision\":\"r\",\"target\":\"t\",\"files\":[],\"sourceFiles\":[]}");
            Assert.That(ContentSourceClosureRecord.Read(withoutField).State, Is.EqualTo(ContentSourceClosureState.Missing), "欄の無い旧 manifest");
            manifest.sourceClosure = new ContentSourceClosure { version = 2, roots = new[] { new ContentSourceClosureRoot() } };
            var unsupported = ContentSourceClosureRecord.Read(manifest);
            Assert.That(unsupported.State, Is.EqualTo(ContentSourceClosureState.Unsupported));
            Assert.That(unsupported.Roots, Is.Empty);
        }

        private static IEnumerable<TestCaseData> Corruptions()
        {
            TestCaseData Case(string name, Action<ContentTransportManifest> corrupt) => new TestCaseData(corrupt).SetName("Read_Invalid_" + name);
            yield return Case("NegativeVersion", m => m.sourceClosure.version = -1);
            yield return Case("MalformedAssetGuid", m => m.sourceClosure.assets[0].guid = "NOT-A-GUID");
            yield return Case("UppercaseRootGuid", m => m.sourceClosure.roots[0].guid = m.sourceClosure.roots[0].guid.ToUpperInvariant());
            yield return Case("DuplicateAssetGuid", m => m.sourceClosure.assets[1].guid = m.sourceClosure.assets[0].guid);
            yield return Case("DuplicatePathIgnoringCase", m =>
            {
                m.sourceClosure.assets[1].path = m.sourceClosure.assets[0].path.ToUpperInvariant();
                m.sourceFiles = m.sourceFiles.Concat(new[] { new ContentSourceFile { path = m.sourceClosure.assets[1].path, sha256 = "" } }).ToArray();
            });
            yield return Case("PathNotInSourceFiles", m => m.sourceFiles = m.sourceFiles.Where(x => x.path != m.sourceClosure.assets[0].path).ToArray());
            yield return Case("GroupOutOfRange", m => m.sourceClosure.assets[0].group = m.sourceClosure.groups.Length);
            yield return Case("RootOutOfRange", m => m.sourceClosure.groups[0].roots = new[] { m.sourceClosure.roots.Length });
            yield return Case("RootSetDiffersFromIndex", m => m.index.entries = m.index.entries.Take(2).ToArray());
            yield return Case("IndexVersionZero", m => m.index = new ContentTransportIndex());
            yield return Case("RootAbsentFromOwnClosure", m =>
            {
                // B の Scene を A の Full だけの組へ付け替えると、B の閉包に B 自身が無くなる。
                var b = m.sourceClosure.assets.Single(x => x.path == "Assets/F/B.unity");
                var aOnly = m.sourceClosure.assets.Single(x => x.path == "Assets/F/A.unity").group;
                b.group = aOnly;
            });
            yield return Case("DuplicateRoot", m =>
            {
                m.sourceClosure.roots[2].logicalKey = m.sourceClosure.roots[0].logicalKey;
                m.sourceClosure.roots[2].representation = m.sourceClosure.roots[0].representation;
            });
            yield return Case("UnknownKind", m => m.sourceClosure.roots[0].kind = "scene");
        }

        [TestCaseSource(nameof(Corruptions))]
        public void Read_CorruptedRecord_IsInvalidWithReason_AndReturnsNoClosure(Action<ContentTransportManifest> corrupt)
        {
            var (_, baseManifest) = Fixture();
            corrupt(baseManifest.Manifest);
            var read = ContentSourceClosureRecord.Read(baseManifest.Manifest);
            Assert.That(read.State, Is.EqualTo(ContentSourceClosureState.Invalid), read.Detail);
            Assert.That(read.Detail, Is.Not.Empty);
            Assert.That(read.Roots, Is.Empty);
        }

        [Test]
        public void ManifestWithRecord_PassesCurrentRequiredFieldCheck_AndIndexReadsTheSameAsWithoutIt()
        {
            var (_, baseManifest) = Fixture();
            var with = baseManifest.Json;
            Assert.DoesNotThrow(() => ContentManifestValidation.ValidateRequiredFields(with), "今の install と再検証の必須欄の検査");
            var withRead = ContentPurposeIndex.Read(baseManifest.Bytes, baseManifest.Digest);

            baseManifest.Manifest.sourceClosure = new ContentSourceClosure();
            baseManifest.Refresh();
            var withoutRead = ContentPurposeIndex.Read(baseManifest.Bytes, baseManifest.Digest);
            Assert.That(withRead.State, Is.EqualTo(ContentPurposeIndexState.Indexed));
            Assert.That(withRead.State, Is.EqualTo(withoutRead.State));
            Assert.That(withRead.Entries.Select(x => x.logicalKey + "/" + x.kind + "/" + x.representation),
                Is.EqualTo(withoutRead.Entries.Select(x => x.logicalKey + "/" + x.kind + "/" + x.representation)));
            Assert.That(with, Does.Contain("\"sourceClosure\""));
        }
    }
}
