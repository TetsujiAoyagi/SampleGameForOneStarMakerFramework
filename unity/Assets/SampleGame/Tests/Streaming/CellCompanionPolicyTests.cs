#nullable enable

using System;
using NUnit.Framework;
using SampleGame.InGame.Streaming;

namespace OneStarMaker.Tests.Streaming
{
    [TestFixture]
    public sealed class CellCompanionPolicyTests
    {
        [Test]
        public void Parser_MissingOnly_DefaultsToFull()
        {
            Assert.That(CellCompanionSetParser.Parse(null, keyExists: false), Is.EqualTo(CellCompanionSet.Full));
        }

        [TestCase("Full", CellCompanionSet.Full)]
        [TestCase("Planner", CellCompanionSet.Planner)]
        [TestCase("Lighting", CellCompanionSet.Lighting)]
        [TestCase("VFX", CellCompanionSet.Vfx)]
        public void Parser_ExactValues(string value, CellCompanionSet expected)
        {
            Assert.That(CellCompanionSetParser.Parse(value, keyExists: true), Is.EqualTo(expected));
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("full")]
        [TestCase("Vfx")]
        [TestCase("Unknown")]
        public void Parser_PresentInvalidValue_Throws(string value)
        {
            Assert.Throws<FormatException>(() => CellCompanionSetParser.Parse(value, keyExists: true));
        }

        [TestCase(CellCompanionSet.Full, "Environment", true)]
        [TestCase(CellCompanionSet.Full, "Lighting", true)]
        [TestCase(CellCompanionSet.Full, "Vfx", true)]
        [TestCase(CellCompanionSet.Full, "Events", true)]
        [TestCase(CellCompanionSet.Planner, "Environment", false)]
        [TestCase(CellCompanionSet.Planner, "Lighting", false)]
        [TestCase(CellCompanionSet.Planner, "Vfx", false)]
        [TestCase(CellCompanionSet.Planner, "Events", true)]
        [TestCase(CellCompanionSet.Lighting, "Environment", true)]
        [TestCase(CellCompanionSet.Lighting, "Lighting", true)]
        [TestCase(CellCompanionSet.Lighting, "Vfx", false)]
        [TestCase(CellCompanionSet.Lighting, "Events", false)]
        [TestCase(CellCompanionSet.Vfx, "Environment", true)]
        [TestCase(CellCompanionSet.Vfx, "Lighting", true)]
        [TestCase(CellCompanionSet.Vfx, "Vfx", true)]
        [TestCase(CellCompanionSet.Vfx, "Events", false)]
        public void Includes_FourByFourMatrix(CellCompanionSet set, string roleName, bool expected)
        {
            var role = (CellCompanionRole)Enum.Parse(typeof(CellCompanionRole), roleName);
            Assert.That(CellCompanionPolicy.Includes(set, role), Is.EqualTo(expected));
        }

        [TestCase("Environment_3_2", "Environment")]
        [TestCase("Spring_Environment_3_2", "Environment")]
        [TestCase("Qualifier_With_Underscore_Lighting_west_east", "Lighting")]
        [TestCase("Spring_VFX_4_2", "Vfx")]
        [TestCase("Spring_Events_4_2", "Events")]
        public void Classifier_UsesThirdTokenFromEnd(string identity, string expected)
        {
            Assert.That(CellCompanionRoleClassifier.TryClassify(identity, out var actual), Is.True);
            Assert.That(actual.ToString(), Is.EqualTo(expected));
        }

        [TestCase("Spring_Lighting")]
        [TestCase("Spring_Environment")]
        [TestCase("Spring_Audio_4_2")]
        [TestCase("")]
        public void Classifier_InvalidOrSeasonScene_IsNotRole(string identity)
        {
            Assert.That(CellCompanionRoleClassifier.TryClassify(identity, out _), Is.False);
        }

        [TestCase(true, true, false, false, true)]
        [TestCase(false, true, false, false, false)]
        [TestCase(true, false, false, false, false)]
        [TestCase(true, true, true, false, false)]
        [TestCase(true, true, false, true, false)]
        public void LoadRule_RequiresAllGates(bool stable, bool included, bool loaded, bool inFlight, bool expected)
        {
            Assert.That(
                CellCompanionLoadRules.ShouldAdd(stable, included, loaded, inFlight),
                Is.EqualTo(expected));
        }
    }
}
