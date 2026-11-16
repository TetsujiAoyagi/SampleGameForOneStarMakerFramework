#nullable enable

using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace OneStarMaker.Tests.BuildContent.Distribution
{
    /// <summary>
    /// 照合の規則（ContentEntryTable、登録した directory の索引と共有）と、manifest の索引の読み口の判定の順。
    /// </summary>
    public sealed class ContentPurposeIndexTests
    {
        // ═══ 照合の規則 ═══

        [Test]
        public void Match_ExactAndUnspecifiedRepresentation()
        {
            var table = Table(("Shared", BuildContentKind.Scene, "Full"), ("Shared", BuildContentKind.Scene, "Whitebox"));
            Assert.That(Resolve(table, "Shared", BuildContentKind.Scene, "Full"), Is.EqualTo("Shared/Scene/Full"));
            Assert.That(Resolve(table, "Shared", BuildContentKind.Scene, "Whitebox"), Is.EqualTo("Shared/Scene/Whitebox"));
            // 表現の指定なし（null と空）は Full に当たる。
            Assert.That(Resolve(table, "Shared", BuildContentKind.Scene, null), Is.EqualTo("Shared/Scene/Full"));
            Assert.That(Resolve(table, "Shared", BuildContentKind.Scene, ""), Is.EqualTo("Shared/Scene/Full"));
        }

        [Test]
        public void Match_FallsBackToFullOnlyWhenTheRequestedRepresentationIsMissing()
        {
            var full = Table(("Entry", BuildContentKind.Scene, "Full"));
            Assert.That(Resolve(full, "Entry", BuildContentKind.Scene, "Whitebox"), Is.EqualTo("Entry/Scene/Full"));
            var none = Table(("Entry", BuildContentKind.Scene, "High"));
            Assert.That(Resolve(none, "Entry", BuildContentKind.Scene, "Whitebox"), Is.Null);
        }

        [Test]
        public void Match_UnspecifiedRepresentationDoesNotHitAWhiteboxOnlyEntry()
        {
            var table = Table(("WhiteboxOnly", BuildContentKind.Scene, "Whitebox"));
            Assert.That(Resolve(table, "WhiteboxOnly", BuildContentKind.Scene, null), Is.Null);
            Assert.That(Resolve(table, "WhiteboxOnly", BuildContentKind.Scene, "Full"), Is.Null);
            Assert.That(Resolve(table, "WhiteboxOnly", BuildContentKind.Scene, "Whitebox"), Is.EqualTo("WhiteboxOnly/Scene/Whitebox"));
        }

        [Test]
        public void Match_RawEmptyRepresentationIsFull_AndObjectsFallBackToo()
        {
            var table = Table(("SharedPrefab", BuildContentKind.Object, ""));
            Assert.That(Resolve(table, "SharedPrefab", BuildContentKind.Object, null), Is.EqualTo("SharedPrefab/Object/"));
            Assert.That(Resolve(table, "SharedPrefab", BuildContentKind.Object, "Full"), Is.EqualTo("SharedPrefab/Object/"));
            Assert.That(Resolve(table, "SharedPrefab", BuildContentKind.Object, "Whitebox"), Is.EqualTo("SharedPrefab/Object/"));
        }

        [Test]
        public void Match_KindAndLogicalKeyMustMatch()
        {
            var table = Table(("Next", BuildContentKind.Scene, "Full"));
            Assert.That(Resolve(table, "Next", BuildContentKind.Object, null), Is.Null);
            Assert.That(Resolve(table, "next", BuildContentKind.Scene, null), Is.Null);
        }

        [Test]
        public void Match_RawEmptyAndFullAreTheSameKey()
        {
            var table = new ContentEntryTable<string>();
            Assert.That(table.TryAdd("A", "", BuildContentKind.Scene, "first"), Is.True);
            Assert.That(table.TryAdd("A", "Full", BuildContentKind.Scene, "second"), Is.False);
            Assert.That(table.TryAdd("A", "Full", BuildContentKind.Object, "object"), Is.True);
        }

        // ═══ 読み口: (a) 上限と digest ═══

        [Test]
        public void Read_DigestMismatch_IsAValidationFailure()
        {
            var bytes = PurposeManifests.Bytes(PurposeManifests.A());
            var index = ContentPurposeIndex.Read(bytes, new string('0', 64));
            Assert.That(index.State, Is.EqualTo(ContentPurposeIndexState.Invalid));
            Assert.That(index.Failure, Is.EqualTo(ContentPurposeIndexFailure.DigestMismatch));
            Assert.Throws<InvalidOperationException>(() => index.Contains(PurposeManifests.Scene("Shared")));
        }

        [Test]
        public void Read_OverTheManifestLimit_IsAValidationFailure()
        {
            var bytes = Encoding.UTF8.GetBytes(PurposeManifests.Json(PurposeManifests.A()).PadRight(4 * 1024 * 1024 + 1));
            var index = ContentPurposeIndex.Read(bytes, ContentDeliveryFiles.Sha256(bytes));
            Assert.That(index.State, Is.EqualTo(ContentPurposeIndexState.Invalid));
            Assert.That(index.Failure, Is.EqualTo(ContentPurposeIndexFailure.TooLarge));
        }

        [Test]
        public void Read_NotJson_IsAValidationFailure()
        {
            var bytes = Encoding.UTF8.GetBytes("not json");
            var index = ContentPurposeIndex.Read(bytes, ContentDeliveryFiles.Sha256(bytes));
            Assert.That(index.State, Is.EqualTo(ContentPurposeIndexState.Invalid));
            Assert.That(index.Failure, Is.EqualTo(ContentPurposeIndexFailure.InvalidManifest));
        }

        // ═══ (b) 欄の有無、(d) 版 0 と 2 以上: 「含む」「含まない」のどちらにも答えない ═══

        [Test]
        public void Read_NoIndexField_IsNoIndex()
        {
            var index = Read(PurposeManifests.WithoutIndex());
            Assert.That(index.State, Is.EqualTo(ContentPurposeIndexState.NoIndex));
            Assert.That(index.Version, Is.Null);
            Assert.Throws<InvalidOperationException>(() => index.Contains(PurposeManifests.Scene("Shared")));
        }

        [TestCase("{\"version\":0,\"entries\":[]}")]
        // 版 0 の entries の値は見ない（型の規則は通る形）。
        [TestCase("{\"version\":0,\"entries\":[{\"logicalKey\":\" \",\"kind\":\"Prefab\"}]}")]
        public void Read_VersionZero_IsNoIndex(string rawIndex)
        {
            var index = Read(PurposeManifests.WithRawIndex(rawIndex));
            Assert.That(index.State, Is.EqualTo(ContentPurposeIndexState.NoIndex));
            Assert.That(index.Version, Is.EqualTo(0));
            Assert.Throws<InvalidOperationException>(() => index.Contains(PurposeManifests.Scene("Shared")));
        }

        [TestCase("{\"version\":2,\"entries\":[]}")]
        [TestCase("{\"version\":7,\"entries\":[{\"logicalKey\":\"Shared\",\"kind\":\"Scene\",\"representation\":\"Full\",\"extra\":[1]}],\"more\":{}}")]
        public void Read_UnknownVersionWithValidTypes_IsUnsupported(string rawIndex)
        {
            var index = Read(PurposeManifests.WithRawIndex(rawIndex));
            Assert.That(index.State, Is.EqualTo(ContentPurposeIndexState.UnsupportedIndex));
            Assert.Throws<InvalidOperationException>(() => index.Contains(PurposeManifests.Scene("Shared")));
        }

        // ═══ (c) 型の規則: 版に関係なく形の失敗 ═══

        [TestCase("[]")]
        [TestCase("null")]
        [TestCase("{\"entries\":[]}")]
        [TestCase("{\"version\":\"1\",\"entries\":[]}")]
        [TestCase("{\"version\":1.0,\"entries\":[]}")]
        [TestCase("{\"version\":true,\"entries\":[]}")]
        [TestCase("{\"version\":4294967297,\"entries\":[]}")]
        [TestCase("{\"version\":1}")]
        [TestCase("{\"version\":2,\"entries\":{}}")]
        [TestCase("{\"version\":2,\"entries\":[1]}")]
        [TestCase("{\"version\":0,\"entries\":\"x\"}")]
        [TestCase("{\"version\":0,\"entries\":[{\"logicalKey\":1}]}")]
        [TestCase("{\"version\":1,\"entries\":[{\"logicalKey\":\"A\",\"kind\":\"Scene\",\"representation\":null}]}")]
        public void Read_TypeFailure_IsAShapeFailureAtAnyVersion(string rawIndex)
        {
            var index = Read(PurposeManifests.WithRawIndex(rawIndex));
            Assert.That(index.State, Is.EqualTo(ContentPurposeIndexState.Invalid));
            Assert.That(index.Failure, Is.EqualTo(ContentPurposeIndexFailure.InvalidIndex));
        }

        // ═══ (d) 負の版と、版 1 の値 ═══

        [TestCase("{\"version\":-1,\"entries\":[]}")]
        [TestCase("{\"version\":1,\"entries\":[{\"logicalKey\":\"A\",\"kind\":\"Scene\"}]}")]
        [TestCase("{\"version\":1,\"entries\":[{\"kind\":\"Scene\",\"representation\":\"Full\"}]}")]
        [TestCase("{\"version\":1,\"entries\":[{\"logicalKey\":\"A\",\"kind\":\"Prefab\",\"representation\":\"Full\"}]}")]
        [TestCase("{\"version\":1,\"entries\":[{\"logicalKey\":\"A\",\"kind\":\"scene\",\"representation\":\"Full\"}]}")]
        [TestCase("{\"version\":1,\"entries\":[{\"logicalKey\":\"A\",\"kind\":\"0\",\"representation\":\"Full\"}]}")]
        [TestCase("{\"version\":1,\"entries\":[{\"logicalKey\":\" \",\"kind\":\"Scene\",\"representation\":\"Full\"}]}")]
        [TestCase("{\"version\":1,\"entries\":[{\"logicalKey\":\"\",\"kind\":\"Scene\",\"representation\":\"Full\"}]}")]
        [TestCase("{\"version\":1,\"entries\":[{\"logicalKey\":\"A\",\"kind\":\"Scene\",\"representation\":\"  \"}]}")]
        [TestCase("{\"version\":1,\"entries\":[{\"logicalKey\":\"A\",\"kind\":\"Scene\",\"representation\":\"Full\"},{\"logicalKey\":\"A\",\"kind\":\"Scene\",\"representation\":\"Full\"}]}")]
        // 生の空と Full は同じ実効表現なので重複。
        [TestCase("{\"version\":1,\"entries\":[{\"logicalKey\":\"A\",\"kind\":\"Scene\",\"representation\":\"\"},{\"logicalKey\":\"A\",\"kind\":\"Scene\",\"representation\":\"Full\"}]}")]
        public void Read_NegativeVersionOrVersionOneValueFailure_IsAShapeFailure(string rawIndex)
        {
            var index = Read(PurposeManifests.WithRawIndex(rawIndex));
            Assert.That(index.State, Is.EqualTo(ContentPurposeIndexState.Invalid));
            Assert.That(index.Failure, Is.EqualTo(ContentPurposeIndexFailure.InvalidIndex));
        }

        [Test]
        public void Read_VersionOne_KeepsRawValuesAndAnswers()
        {
            var index = Read(PurposeManifests.Json(PurposeManifests.A()));
            Assert.That(index.State, Is.EqualTo(ContentPurposeIndexState.Indexed));
            Assert.That(index.Version, Is.EqualTo(1));
            Assert.That(index.Entries.Select(x => x.logicalKey + "/" + x.kind + "/" + x.representation),
                Is.EqualTo(new[] { "Entry/Scene/Full", "Shared/Scene/Full", "SharedPrefab/Object/" }));
            Assert.That(index.Contains(PurposeManifests.Scene("Shared")), Is.True);
            Assert.That(index.Contains(PurposeManifests.Scene("Shared", "Whitebox")), Is.True);
            Assert.That(index.Contains(PurposeManifests.Object("SharedPrefab")), Is.True);
            Assert.That(index.Contains(PurposeManifests.Scene("SharedPrefab")), Is.False);
            Assert.That(index.Contains(PurposeManifests.Scene("Next")), Is.False);
        }

        // ═══ install の検査は、索引を持つ manifest と持たない manifest の両方を今までどおり通す ═══

        [Test]
        public void InstallValidation_AcceptsManifestsWithAndWithoutIndex()
        {
            foreach (var json in new[] { PurposeManifests.Json(PurposeManifests.A()), PurposeManifests.WithoutIndex() })
            {
                Assert.DoesNotThrow(() => ContentManifestValidation.ValidateRequiredFields(json));
                var manifest = JsonUtility.FromJson<ContentTransportManifest>(json);
                Assert.DoesNotThrow(() => ContentManifestValidation.Validate(manifest, new string('a', 64),
                    new ContentInstallRequest(new string('a', 64), "purpose-a", "revision-a", "StandaloneWindows64-Player", "6000.6.0f1", 2, 100)));
            }
        }

        private static ContentPurposeIndex Read(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            return ContentPurposeIndex.Read(bytes, ContentDeliveryFiles.Sha256(bytes));
        }

        private static ContentEntryTable<string> Table(params (string key, BuildContentKind kind, string representation)[] entries)
        {
            var table = new ContentEntryTable<string>();
            foreach (var (key, kind, representation) in entries)
                Assert.That(table.TryAdd(key, representation, kind, key + "/" + kind + "/" + representation), Is.True);
            return table;
        }

        private static string? Resolve(ContentEntryTable<string> table, string key, BuildContentKind kind, string? representation)
            => table.TryResolve(key, representation, kind, out var value) ? value : null;
    }

    // 索引の単体テストの合成 manifest。目的 A と B は HANDOFF の fixture と同じ entry を持つ（Unity の build はしない）。
    internal static class PurposeManifests
    {
        internal static ContentTransportManifest A(string revision = "revision-a") => Manifest("purpose-a", revision,
            ("Entry", "Scene", "Full"), ("Shared", "Scene", "Full"), ("SharedPrefab", "Object", ""));

        internal static ContentTransportManifest B(string revision = "revision-b") => Manifest("purpose-b", revision,
            ("Next", "Scene", "Full"), ("Shared", "Scene", "Full"), ("Shared", "Scene", "Whitebox"), ("WhiteboxOnly", "Scene", "Whitebox"));

        internal static ContentTransportManifest Manifest(string contentSet, string revision,
            params (string logicalKey, string kind, string representation)[] entries) => new()
        {
            version = 1,
            product = "OneStarMaker",
            contentSet = contentSet,
            revision = revision,
            target = "StandaloneWindows64-Player",
            unityVersion = "6000.6.0f1",
            rootSchemaVersion = 2,
            playerConfigSchemaVersion = 1,
            files = new[] { new ContentTransportFile { path = "content.bin", size = 1, sha256 = new string('b', 64) } },
            index = new ContentTransportIndex
            {
                version = 1,
                entries = entries.Select(x => new ContentTransportIndexEntry
                    { logicalKey = x.logicalKey, kind = x.kind, representation = x.representation }).ToArray(),
            },
        };

        internal static string Json(ContentTransportManifest manifest) => JsonUtility.ToJson(manifest);

        internal static byte[] Bytes(ContentTransportManifest manifest) => Encoding.UTF8.GetBytes(Json(manifest));

        // 索引の欄だけを置き換える。index は DTO の最後の欄なので、その前で切って生の JSON を足す。
        internal static string WithRawIndex(string rawIndex, ContentTransportManifest? manifest = null)
            => Head(manifest ?? A()) + ",\"index\":" + rawIndex + "}";

        // 索引の追加より前の manifest（欄が無い）。
        internal static string WithoutIndex(ContentTransportManifest? manifest = null) => Head(manifest ?? A()) + "}";

        internal static ContentPurposeCandidate Candidate(string json) => Candidate(Encoding.UTF8.GetBytes(json));

        internal static ContentPurposeCandidate Candidate(byte[] bytes) => new(bytes, ContentDeliveryFiles.Sha256(bytes));

        internal static ContentPurposeRequest Scene(string key, string? representation = null) => new(BuildContentKind.Scene, key, representation);

        internal static ContentPurposeRequest Object(string key, string? representation = null) => new(BuildContentKind.Object, key, representation);

        private static string Head(ContentTransportManifest manifest)
        {
            var json = Json(manifest);
            var start = json.IndexOf(",\"index\":", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0));
            return json.Substring(0, start);
        }
    }
}
