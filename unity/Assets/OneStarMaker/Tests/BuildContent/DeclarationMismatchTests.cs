#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent;
using UnityEngine;

namespace OneStarMaker.Tests.BuildContent
{
    /// <summary>
    /// 宣言と起動設定の不一致の検出（AC19）。合格例: companion set が宣言 Planner・設定 Full で 1 件、設定に key が無くても 1 件。
    /// 不合格例: 一致しているのに出る、違うのに出ない、欠損を一致と扱う、表現だけの違いを見落とす。
    /// </summary>
    public sealed class DeclarationMismatchTests
    {
        private static BuildContentDeclaration Planner() => new("spring-planner-whitebox", "Whitebox",
            new[] { new KeyValuePair<string, string>("world:cellCompanionSet", "Planner") });

        [Test]
        public void MatchingConfiguration_HasNoDifference()
        {
            var differences = ContentDeclarationMismatch.Find(Planner(), Config(("world:cellCompanionSet", "Planner")), "Whitebox");
            Assert.That(differences, Is.Empty);
        }

        [Test]
        public void DifferentValue_IsOneDifferenceWithBothValues()
        {
            var differences = ContentDeclarationMismatch.Find(Planner(), Config(("world:cellCompanionSet", "Full")), "Whitebox");
            Assert.That(differences.Count, Is.EqualTo(1));
            Assert.That(differences[0].Key, Is.EqualTo("world:cellCompanionSet"));
            Assert.That(differences[0].Declared, Is.EqualTo("Planner"));
            Assert.That(differences[0].Actual, Is.EqualTo("Full"));
            Assert.That(ContentDeclarationMismatch.Format(differences), Is.EqualTo("world:cellCompanionSet: declared=Planner actual=Full"));
        }

        [Test]
        public void AbsentKey_CountsAsDifference()
        {
            var differences = ContentDeclarationMismatch.Find(Planner(), Config(), "Whitebox");
            Assert.That(differences.Count, Is.EqualTo(1));
            Assert.That(differences[0].Actual, Is.EqualTo(ContentDeclarationMismatch.AbsentValue));
        }

        [Test]
        public void RepresentationOnly_IsReportedUnderRepresentationKey()
        {
            // runtime defaults は一致し、表現だけが違う（宣言 Whitebox・実効 Full）。
            var differences = ContentDeclarationMismatch.Find(Planner(), Config(("world:cellCompanionSet", "Planner")), "Full");
            Assert.That(differences.Count, Is.EqualTo(1));
            Assert.That(differences[0].Key, Is.EqualTo(ContentDeclarationMismatch.RepresentationKey));
            Assert.That(differences[0].Declared, Is.EqualTo("Whitebox"));
            Assert.That(differences[0].Actual, Is.EqualTo("Full"));
        }

        [Test]
        public void MultipleDifferences_AreJoinedIntoOneLine()
        {
            var declaration = new BuildContentDeclaration("x", "Whitebox", new[]
            {
                new KeyValuePair<string, string>("a:one", "1"),
                new KeyValuePair<string, string>("b:two", "2"),
            });
            var differences = ContentDeclarationMismatch.Find(declaration, Config(("a:one", "9")), "Full");
            Assert.That(differences.Select(x => x.Key), Is.EqualTo(new[] { "a:one", "b:two", ContentDeclarationMismatch.RepresentationKey }));
            Assert.That(ContentDeclarationMismatch.Format(differences), Does.Not.Contain("\n"));
        }

        [Test]
        public void ComparisonIsOrdinal_AndValuesAreNotInterpreted()
        {
            var differences = ContentDeclarationMismatch.Find(Planner(), Config(("world:cellCompanionSet", "planner")), "Whitebox");
            Assert.That(differences.Count, Is.EqualTo(1));
        }

        [Test]
        public void RootDeclaration_RoundTripsThroughSerialization_AndAbsentDeclarationStaysNull()
        {
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            var copy = ScriptableObject.CreateInstance<BuildContentRoot>();
            var plain = ScriptableObject.CreateInstance<BuildContentRoot>();
            try
            {
                root.Initialize("build", "StandaloneWindows64-Player", System.Array.Empty<BuildContentEntry>(), Planner());
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(root), copy);
                Assert.That(copy.Declaration, Is.Not.Null);
                Assert.That(copy.Declaration!.Name, Is.EqualTo("spring-planner-whitebox"));
                Assert.That(copy.Declaration.Representation, Is.EqualTo("Whitebox"));
                Assert.That(copy.Declaration.RuntimeDefaults.Single().Key, Is.EqualTo("world:cellCompanionSet"));
                Assert.That(copy.Declaration.RuntimeDefaults.Single().Value, Is.EqualTo("Planner"));
                Assert.That(copy.SchemaVersion, Is.EqualTo(BuildContentRoot.CurrentSchemaVersion));

                // 宣言無しの root は serializer が既定値の宣言を復元しても null として読む（旧 build の root をそのまま受理する）。
                plain.Initialize("build", "StandaloneWindows64-Player", System.Array.Empty<BuildContentEntry>());
                var reloaded = ScriptableObject.CreateInstance<BuildContentRoot>();
                try
                {
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(plain), reloaded);
                    Assert.That(reloaded.Declaration, Is.Null);
                }
                finally { Object.DestroyImmediate(reloaded); }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(copy);
                Object.DestroyImmediate(plain);
            }
        }

        private static System.Func<string, string?> Config(params (string Key, string Value)[] values)
        {
            var map = values.ToDictionary(x => x.Key, x => x.Value, System.StringComparer.OrdinalIgnoreCase);
            return key => map.TryGetValue(key, out var value) ? value : null;
        }
    }
}
