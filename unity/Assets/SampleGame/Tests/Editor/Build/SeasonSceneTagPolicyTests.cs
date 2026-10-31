#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using SampleGame.DependOnAll.Editor.Build;

namespace SampleGame.Tests.Editor.Build
{
    // node 水準の tag: Season は祖先から、Role は Runtime の companion 分類から。Framework はどちらの語彙も知らない。
    public sealed class SeasonSceneTagPolicyTests
    {
        private static readonly BuildGraphNode[] Nodes =
        {
            new("InGameScene", null, new[] { "InGameSession" }, BuildGraphLoad.Always, Array.Empty<string>()),
            new("InGameSession", "InGameScene", new[] { "Season_Spring" }, BuildGraphLoad.Always, Array.Empty<string>()),
            new("Season_Spring", "InGameSession", new[] { "Spring_Cell_4_2", "Spring_Lighting", "Spring_Event_MultiCellProof" }, BuildGraphLoad.OnDemand, Array.Empty<string>()),
            new("Spring_Lighting", "Season_Spring", Array.Empty<string>(), BuildGraphLoad.Always, new[] { "Full" }),
            new("Spring_Event_MultiCellProof", "Season_Spring", Array.Empty<string>(), BuildGraphLoad.OnDemand, new[] { "Full" }),
            new("Spring_Cell_4_2", "Season_Spring", new[] { "Spring_Environment_4_2", "Spring_Lighting_4_2", "Spring_VFX_4_2", "Spring_Events_4_2" }, BuildGraphLoad.OnDemand, new[] { "Full", "Whitebox" }),
            new("Spring_Environment_4_2", "Spring_Cell_4_2", Array.Empty<string>(), BuildGraphLoad.OnDemand, new[] { "Full" }),
            new("Spring_Lighting_4_2", "Spring_Cell_4_2", Array.Empty<string>(), BuildGraphLoad.OnDemand, new[] { "Full" }),
            new("Spring_VFX_4_2", "Spring_Cell_4_2", Array.Empty<string>(), BuildGraphLoad.OnDemand, new[] { "Full" }),
            new("Spring_Events_4_2", "Spring_Cell_4_2", Array.Empty<string>(), BuildGraphLoad.OnDemand, new[] { "Full" }),
        };

        private static IReadOnlyDictionary<string, BuildGraphNode> Graph() => Nodes.ToDictionary(node => node.Identity, StringComparer.Ordinal);

        [TestCase("InGameScene", "")]
        [TestCase("InGameSession", "")]
        [TestCase("Season_Spring", "Season=Spring")]
        [TestCase("Spring_Lighting", "Season=Spring")]
        [TestCase("Spring_Event_MultiCellProof", "Season=Spring")]
        [TestCase("Spring_Cell_4_2", "Season=Spring")]
        [TestCase("Spring_Environment_4_2", "Role=Environment,Season=Spring")]
        [TestCase("Spring_Lighting_4_2", "Role=Lighting,Season=Spring")]
        [TestCase("Spring_VFX_4_2", "Role=Vfx,Season=Spring")]
        [TestCase("Spring_Events_4_2", "Role=Events,Season=Spring")]
        public void Tags_FollowAncestorSeasonAndRuntimeRoleClassifier(string identity, string expected)
        {
            var graph = Graph();
            var tags = new SeasonSceneTagPolicy().GetTags(graph[identity], graph)
                .Select(tag => tag.Dimension + "=" + tag.Value).OrderBy(x => x, StringComparer.Ordinal);
            Assert.That(string.Join(",", tags), Is.EqualTo(expected));
        }

        [Test]
        public void Schema_DeclaresSeasonAndRoleOnly()
        {
            var schema = new SeasonSceneTagPolicy().Schema;
            Assert.That(schema.Dimensions.Keys, Is.EquivalentTo(new[] { "Role", "Season" }));
            Assert.That(schema.Dimensions["Season"], Is.EqualTo(new[] { "Autumn", "Spring", "Summer", "Winter" }));
            Assert.That(schema.Dimensions["Role"], Is.EqualTo(new[] { "Environment", "Events", "Lighting", "Vfx" }));
        }

        [Test]
        public void NestedDifferentSeasons_IsRejected()
        {
            var nested = new Dictionary<string, BuildGraphNode>(StringComparer.Ordinal)
            {
                ["Season_Spring"] = new("Season_Spring", null, new[] { "Season_Summer" }, BuildGraphLoad.OnDemand, Array.Empty<string>()),
                ["Season_Summer"] = new("Season_Summer", "Season_Spring", Array.Empty<string>(), BuildGraphLoad.OnDemand, Array.Empty<string>()),
            };
            Assert.Throws<InvalidOperationException>(() => new SeasonSceneTagPolicy().GetTags(nested["Season_Summer"], nested));
        }

        [TestCase("Full", "Environment,Events,Lighting,Vfx")]
        [TestCase("Planner", "Events")]
        [TestCase("Lighting", "Environment,Lighting")]
        [TestCase("VFX", "Environment,Lighting,Vfx")]
        public void RolesIncludedBy_MatchesRuntimeCompanionPolicy(string set, string expected)
        {
            Assert.That(string.Join(",", SeasonSceneTagPolicy.RolesIncludedBy(set)), Is.EqualTo(expected));
        }

        [Test]
        public void RolesIncludedBy_UnknownSet_IsRejected()
        {
            Assert.Throws<FormatException>(() => SeasonSceneTagPolicy.RolesIncludedBy("Planer"));
        }
    }
}
