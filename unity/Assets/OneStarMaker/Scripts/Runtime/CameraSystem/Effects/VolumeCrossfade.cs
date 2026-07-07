#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Effects
{
    /// <summary>
    /// クロスフェード 1 コマの出力。どの VolumeProfile を、どの weight で適用するかを表し、
    /// Host はこの列挙を見て実 Volume を更新する。IsPendingRelease は「もう不要なので破棄してよい」印。
    /// </summary>
    public readonly struct VolumeCrossfadeEntry
    {
        /// <summary>対象の VolumeProfile。</summary>
        public UnityEngine.Object Profile { get; init; }

        /// <summary>適用ウェイト（0..1）。</summary>
        public float Weight { get; init; }

        /// <summary>フェード完了で不要になり、解放待ちであることを示す。</summary>
        public bool IsPendingRelease { get; init; }
    }

    /// <summary>
    /// 2 つの VolumeProfile 間の weight クロスフェードを管理するポリシー（純 C#）。入退場の weight は相補的
    /// （incoming + departing = 1）に補間する。ブレンド中に再切替されたら現在 weight を起点に引き継ぎ、
    /// 新しい入退場に含まれない旧プロファイルは pending release へ移して Host 側の残留を防ぐ。
    /// </summary>
    public sealed class VolumeCrossfade
    {
        private UnityEngine.Object? _incomingProfile;
        private UnityEngine.Object? _departingProfile;
        private float _incomingWeight;
        private float _blendStartIncomingWeight;
        private float _elapsedSec;
        private CameraBlendSpec _blendSpec;
        private bool _isBlending;
        private readonly List<UnityEngine.Object> _pendingReleases = new();

        public bool IsBlending => _isBlending;

        public IReadOnlyList<UnityEngine.Object> PendingReleases => _pendingReleases;

        /// <summary>
        /// incoming へのクロスフェードを開始する。進行中フェードがあれば現在 weight を起点に引き継ぐ。
        /// カットの場合は即座に完了状態にし、退場側は pending release へ回す。両者ともプロファイル無しなら状態をリセットする。
        /// </summary>
        public void BeginCrossfade(LogicalCamera incoming, LogicalCamera departing, in CameraBlendSpec blend)
        {
            if (incoming == null)
            {
                throw new ArgumentNullException(nameof(incoming));
            }

            if (departing == null)
            {
                throw new ArgumentNullException(nameof(departing));
            }

            var nextIncoming = incoming.VolumeProfile;
            var nextDeparting = departing.VolumeProfile;

            MarkSupersededProfilesForRelease(nextIncoming, nextDeparting);

            if (nextIncoming == null && nextDeparting == null)
            {
                ResetBlendState();
                return;
            }

            var startIncomingWeight = _isBlending && _incomingProfile != null ? _incomingWeight : 0f;

            if (IsCut(blend))
            {
                ApplyCut(nextIncoming, nextDeparting, startIncomingWeight);
                return;
            }

            _incomingProfile = nextIncoming;
            _departingProfile = nextDeparting;
            _blendStartIncomingWeight = startIncomingWeight;
            _incomingWeight = startIncomingWeight;
            _elapsedSec = 0f;
            _blendSpec = blend;
            _isBlending = true;
        }

        public void Tick(float deltaTime)
        {
            if (!_isBlending || deltaTime <= 0f)
            {
                return;
            }

            _elapsedSec += deltaTime;
            var duration = _blendSpec.DurationSec;
            if (duration <= 0f)
            {
                return;
            }

            // 入場 weight は開始 weight→1 を eased t で補間する。退場 weight は都度 1-incoming で相補的に導く。
            var rawT = Mathf.Clamp01(_elapsedSec / duration);
            var easedT = ApplyEasing(rawT, _blendSpec.Easing);
            _incomingWeight = Mathf.Lerp(_blendStartIncomingWeight, 1f, easedT);

            // eased ではなく raw t で完了判定する（EaseInOut でも端点は t=1 で確定するため）。
            if (rawT >= 1f - 1e-6f)
            {
                _incomingWeight = 1f;
                CompleteDepartingIfNeeded(markForRelease: true);
                _departingProfile = null;
                _isBlending = false;
            }
        }

        public VolumeCrossfadeEntry[] GetEntries()
        {
            var entries = new List<VolumeCrossfadeEntry>(2 + _pendingReleases.Count);

            if (_incomingProfile != null && (HasActiveWeight(_incomingWeight) || _isBlending))
            {
                entries.Add(new VolumeCrossfadeEntry
                {
                    Profile = _incomingProfile,
                    Weight = _incomingWeight,
                    IsPendingRelease = false,
                });
            }

            if (_departingProfile != null)
            {
                var departingWeight = ComputeDepartingWeight();
                if (HasActiveWeight(departingWeight) || (!_isBlending && _pendingReleases.Contains(_departingProfile)))
                {
                    entries.Add(new VolumeCrossfadeEntry
                    {
                        Profile = _departingProfile,
                        Weight = departingWeight,
                        IsPendingRelease = !_isBlending && departingWeight <= 0f,
                    });
                }
            }

            foreach (var released in _pendingReleases)
            {
                if (released == _incomingProfile || released == _departingProfile)
                {
                    continue;
                }

                entries.Add(new VolumeCrossfadeEntry
                {
                    Profile = released,
                    Weight = 0f,
                    IsPendingRelease = true,
                });
            }

            return entries.ToArray();
        }

        public float? TryGetWeight(UnityEngine.Object profile)
        {
            if (profile == null)
            {
                return null;
            }

            if (_incomingProfile == profile)
            {
                return _incomingProfile != null ? _incomingWeight : null;
            }

            if (_departingProfile == profile)
            {
                return _departingProfile != null ? ComputeDepartingWeight() : null;
            }

            if (_pendingReleases.Contains(profile))
            {
                return 0f;
            }

            return null;
        }

        public bool IsPendingRelease(UnityEngine.Object profile) =>
            profile != null && _pendingReleases.Contains(profile);

        private void ApplyCut(UnityEngine.Object? nextIncoming, UnityEngine.Object? nextDeparting, float startIncomingWeight)
        {
            _incomingProfile = nextIncoming;
            _departingProfile = nextDeparting;
            _blendStartIncomingWeight = startIncomingWeight;
            _incomingWeight = nextIncoming != null ? 1f : 0f;
            _elapsedSec = 0f;
            _blendSpec = CameraBlendSpec.Cut;
            _isBlending = false;

            if (nextDeparting != null)
            {
                MarkPendingRelease(nextDeparting);
            }

            _departingProfile = null;
        }

        private void CompleteDepartingIfNeeded(bool markForRelease)
        {
            if (_departingProfile == null)
            {
                return;
            }

            if (markForRelease)
            {
                MarkPendingRelease(_departingProfile);
            }
        }

        private void MarkPendingRelease(UnityEngine.Object profile)
        {
            if (!_pendingReleases.Contains(profile))
            {
                _pendingReleases.Add(profile);
            }
        }

        // 新しい入退場（nextIncoming / nextDeparting）に含まれない旧プロファイルは、次フェードで参照されず
        // Host 側に weight を残したまま浮くため、解放待ちへ回収する。
        private void MarkSupersededProfilesForRelease(
            UnityEngine.Object? nextIncoming,
            UnityEngine.Object? nextDeparting)
        {
            MarkIfSuperseded(_incomingProfile, nextIncoming, nextDeparting);
            MarkIfSuperseded(_departingProfile, nextIncoming, nextDeparting);
        }

        private void MarkIfSuperseded(
            UnityEngine.Object? current,
            UnityEngine.Object? nextIncoming,
            UnityEngine.Object? nextDeparting)
        {
            if (current == null || current == nextIncoming || current == nextDeparting)
            {
                return;
            }

            MarkPendingRelease(current);
        }

        private void ResetBlendState()
        {
            _incomingProfile = null;
            _departingProfile = null;
            _incomingWeight = 0f;
            _blendStartIncomingWeight = 0f;
            _elapsedSec = 0f;
            _blendSpec = CameraBlendSpec.Cut;
            _isBlending = false;
        }

        private float ComputeDepartingWeight()
        {
            if (_departingProfile == null)
            {
                return 0f;
            }

            return 1f - _incomingWeight;
        }

        private static bool IsCut(in CameraBlendSpec blend) => blend.DurationSec <= 0f;

        private static bool HasActiveWeight(float weight) => weight > 1e-6f;

        /// <summary>0..1 の進捗 t に補間カーブを適用する。EaseInOut は smoothstep（3t²-2t³）。</summary>
        internal static float ApplyEasing(float t, CameraBlendEasing easing)
        {
            t = Mathf.Clamp01(t);
            return easing switch
            {
                CameraBlendEasing.Linear => t,
                CameraBlendEasing.EaseInOut => t * t * (3f - 2f * t),
                _ => t,
            };
        }
    }
}
