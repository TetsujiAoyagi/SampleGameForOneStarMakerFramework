#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.Streaming;
using UnityEngine;

namespace SampleGame.InGame.Streaming
{
    /// <summary>
    /// Season 直下の子 1 件。読出（SceneResource / VolumeQuery）と選別を混ぜないための値。
    /// </summary>
    internal readonly struct SeasonChildDescriptor
    {
        public SeasonChildDescriptor(string identity, bool streamByDistance, Bounds volume, bool volumeAvailable)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            StreamByDistance = streamByDistance;
            Volume = volume;
            VolumeAvailable = volumeAvailable;
        }

        public string Identity { get; }
        public bool StreamByDistance { get; }
        public Bounds Volume { get; }
        public bool VolumeAvailable { get; }
    }

    /// <summary>
    /// Season 直下の StreamByDistance 子から候補集合を作る純関数。
    /// 空・重複 identity・欠落 volume は例外。暗黙のフォールバックは作らない。
    /// </summary>
    internal static class SeasonCandidateSelection
    {
        internal static StreamingCandidateSet Select(IReadOnlyList<SeasonChildDescriptor> children)
        {
            if (children == null)
            {
                throw new ArgumentNullException(nameof(children));
            }

            var selected = new List<StreamingCandidate>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (!child.StreamByDistance)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(child.Identity))
                {
                    throw new InvalidOperationException($"候補 [{i}] の identity が空です。");
                }

                if (!seen.Add(child.Identity))
                {
                    throw new InvalidOperationException($"候補 identity が重複しています: '{child.Identity}'。");
                }

                if (!child.VolumeAvailable || child.Volume.size == Vector3.zero)
                {
                    throw new InvalidOperationException($"候補 '{child.Identity}' の体積がありません。");
                }

                selected.Add(new StreamingCandidate(child.Identity, child.Volume));
            }

            if (selected.Count == 0)
            {
                throw new InvalidOperationException("Season 直下の StreamByDistance 候補が空です。");
            }

            return new StreamingCandidateSet(selected);
        }

        /// <summary>
        /// Season の Children と体積 query から descriptor を組み立てて選別する。
        /// identity の組み立てや格子定数からの中心復元はしない。
        /// </summary>
        internal static StreamingCandidateSet FromSeasonChildren(
            IReadOnlyList<SceneResource> children,
            ISceneVolumeQuery volumeQuery)
        {
            if (children == null)
            {
                throw new ArgumentNullException(nameof(children));
            }

            if (volumeQuery == null)
            {
                throw new ArgumentNullException(nameof(volumeQuery));
            }

            var descriptors = new SeasonChildDescriptor[children.Count];
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var available = volumeQuery.TryGetSceneVolume(child.Identity, out var volume);
                descriptors[i] = new SeasonChildDescriptor(
                    child.Identity,
                    child.StreamByDistance,
                    volume,
                    available);
            }

            return Select(descriptors);
        }
    }
}
