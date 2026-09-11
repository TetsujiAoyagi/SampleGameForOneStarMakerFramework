#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.Streaming;
using OneStarMaker.Tests.SceneSystem.Helpers;
using SampleGame.InGame.Streaming;
using UnityEngine;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// S-4b: Season 直下 StreamByDistance 子の選別。空・重複・欠落 volume は例外。
    /// </summary>
    [TestFixture]
    public sealed class SeasonCandidateSelectionTests
    {
        private readonly List<ScriptableObject> _assets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets)
            {
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }

            _assets.Clear();
        }

        [Test]
        public void Select_StreamByDistanceChildren_BecomeCandidates()
        {
            var volume = new Bounds(Vector3.one, Vector3.one * 2f);
            var set = SeasonCandidateSelection.Select(new[]
            {
                new SeasonChildDescriptor("Spring_Lighting", streamByDistance: false, default, volumeAvailable: false),
                new SeasonChildDescriptor("Spring_Cell_0_4", streamByDistance: true, volume, volumeAvailable: true),
                new SeasonChildDescriptor("Spring_Cell_1_4", streamByDistance: true, volume, volumeAvailable: true),
            });

            Assert.That(set.Candidates.Count, Is.EqualTo(2));
            Assert.That(set.Candidates[0].Identity, Is.EqualTo("Spring_Cell_0_4"));
            Assert.That(set.Candidates[1].Identity, Is.EqualTo("Spring_Cell_1_4"));
        }

        [Test]
        public void Select_EmptyStreamByDistance_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => SeasonCandidateSelection.Select(new[]
            {
                new SeasonChildDescriptor("Spring_Lighting", false, default, false),
            }));
        }

        [Test]
        public void Select_DuplicateIdentity_Throws()
        {
            var volume = new Bounds(Vector3.one, Vector3.one);
            Assert.Throws<InvalidOperationException>(() => SeasonCandidateSelection.Select(new[]
            {
                new SeasonChildDescriptor("Spring_Cell_0_4", true, volume, true),
                new SeasonChildDescriptor("Spring_Cell_0_4", true, volume, true),
            }));
        }

        [Test]
        public void Select_MissingVolume_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => SeasonCandidateSelection.Select(new[]
            {
                new SeasonChildDescriptor("Spring_Cell_0_4", true, default, volumeAvailable: false),
            }));
        }

        [Test]
        public void FromSeasonChildren_UsesVolumeQuery()
        {
            var parent = Track(SceneTestHelper.CreateSceneResource("Season_Spring"));
            var cell = Track(SceneTestHelper.CreateSceneResource(
                "Spring_Cell_0_4",
                parent: parent,
                streamByDistance: true,
                volume: new Bounds(Vector3.zero, Vector3.one)));
            SceneTestHelper.AddChild(parent, cell);
            var lighting = Track(SceneTestHelper.CreateSceneResource("Spring_Lighting", parent: parent));
            SceneTestHelper.AddChild(parent, lighting);

            var volumes = new FakeVolumeQuery();
            volumes.Volumes["Spring_Cell_0_4"] = new Bounds(Vector3.up, Vector3.one * 3f);

            var set = SeasonCandidateSelection.FromSeasonChildren(parent.Children, volumes);
            Assert.That(set.Candidates.Count, Is.EqualTo(1));
            Assert.That(set.Candidates[0].Identity, Is.EqualTo("Spring_Cell_0_4"));
        }

        private SceneResource Track(SceneResource resource)
        {
            _assets.Add(resource);
            return resource;
        }

        private sealed class FakeVolumeQuery : ISceneVolumeQuery
        {
            internal Dictionary<string, Bounds> Volumes { get; } = new(StringComparer.Ordinal);

            public bool TryGetSceneVolume(string identity, out Bounds volume)
                => Volumes.TryGetValue(identity, out volume);
        }
    }
}
