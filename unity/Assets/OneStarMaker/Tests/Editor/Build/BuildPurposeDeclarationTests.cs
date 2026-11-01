#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Build.Selection;

namespace OneStarMaker.Tests.Editor.Build
{
    // 宣言型の不変条件（AC7）。名前は contentSet の規則、root は 1 つ以上、表現の優先順は空でなく、content:* の既定値は拒む。
    public sealed class BuildPurposeDeclarationTests
    {
        private static readonly BuildRequest Filter = new(new[] { new BuildTag("Season", "Spring") });
        private static readonly BuildRepresentationSelection Full = new(new[] { "Full" }, BuildRepresentationMode.First);

        [TestCase("content:runtimeMode")]
        [TestCase("Content:Representation")]
        [TestCase("content:")]
        public void ContentPrefixedRuntimeDefault_IsRejected(string key)
        {
            Assert.Throws<ArgumentException>(() => new BuildPurposeDeclaration("spring-full", new[] { "Root" }, Filter, Full,
                new[] { new KeyValuePair<string, string>(key, "x") }));
        }

        [Test]
        public void RuntimeDefaults_AreSortedUniqueAndLookedUpCaseInsensitively()
        {
            var declaration = new BuildPurposeDeclaration("spring-full", new[] { "Root" }, Filter, Full, new[]
            {
                new KeyValuePair<string, string>("world:cellCompanionSet", "Planner"),
                new KeyValuePair<string, string>("audio:volume", "1"),
            });
            Assert.That(declaration.RuntimeDefaults.Select(pair => pair.Key), Is.EqualTo(new[] { "audio:volume", "world:cellCompanionSet" }));
            Assert.That(declaration.FindRuntimeDefault("WORLD:CELLCOMPANIONSET"), Is.EqualTo("Planner"));
            Assert.That(declaration.FindRuntimeDefault("missing"), Is.Null);
            Assert.Throws<ArgumentException>(() => new BuildPurposeDeclaration("spring-full", new[] { "Root" }, Filter, Full, new[]
            {
                new KeyValuePair<string, string>("world:cellCompanionSet", "Planner"),
                new KeyValuePair<string, string>("World:CellCompanionSet", "Full"),
            }));
            Assert.Throws<ArgumentException>(() => new BuildPurposeDeclaration("spring-full", new[] { "Root" }, Filter, Full, new[]
            {
                new KeyValuePair<string, string>("world:cellCompanionSet", ""),
            }));
        }

        [TestCase("")]
        [TestCase("spring full")]
        [TestCase("spring/full")]
        [TestCase("..")]
        [TestCase("ａ")]
        public void Name_MustFollowContentSetRules(string name)
        {
            Assert.Throws<ArgumentException>(() => new BuildPurposeDeclaration(name, new[] { "Root" }, Filter, Full));
        }

        [Test]
        public void Name_LongerThanSegmentLimit_IsRejected()
        {
            Assert.That(BuildPurposeDeclaration.IsContentSetName(new string('a', 64)), Is.True);
            Assert.That(BuildPurposeDeclaration.IsContentSetName(new string('a', 65)), Is.False);
        }

        [Test]
        public void Roots_RequireAtLeastOneDistinctStableIdentity()
        {
            Assert.Throws<ArgumentException>(() => new BuildPurposeDeclaration("x", Array.Empty<string>(), Filter, Full));
            Assert.Throws<ArgumentException>(() => new BuildPurposeDeclaration("x", new[] { "Root", "Root" }, Filter, Full));
            Assert.Throws<ArgumentException>(() => new BuildPurposeDeclaration("x", new[] { " Root" }, Filter, Full));
            var declaration = new BuildPurposeDeclaration("x", new[] { "OutGameScene", "InGameScene" }, Filter, Full);
            Assert.That(declaration.Roots, Is.EqualTo(new[] { "InGameScene", "OutGameScene" }));
        }

        [Test]
        public void Representation_RequiresNonEmptyDistinctPreference_AndPrimaryIsFirst()
        {
            Assert.Throws<ArgumentException>(() => new BuildRepresentationSelection(Array.Empty<string>(), BuildRepresentationMode.First));
            Assert.Throws<ArgumentException>(() => new BuildRepresentationSelection(new[] { "Full", "Full" }, BuildRepresentationMode.All));
            Assert.Throws<ArgumentException>(() => new BuildRepresentationSelection(new[] { "" }, BuildRepresentationMode.First));
            var selection = new BuildRepresentationSelection(new[] { "Whitebox", "Full" }, BuildRepresentationMode.First);
            Assert.That(selection.Primary, Is.EqualTo("Whitebox"));
            Assert.That(selection.Preference, Is.EqualTo(new[] { "Whitebox", "Full" }));
        }

        [Test]
        public void NullFilterOrRepresentation_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new BuildPurposeDeclaration("x", new[] { "Root" }, null!, Full));
            Assert.Throws<ArgumentNullException>(() => new BuildPurposeDeclaration("x", new[] { "Root" }, Filter, null!));
        }
    }
}
