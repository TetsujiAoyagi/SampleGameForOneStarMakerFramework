#nullable enable

using System;

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// voice ごとの Unity AudioReverbFilter 設定。共有バスの残響ではない。
    /// レベルは millibel、減衰は秒、拡散は percent。default / Off は無効な効果を表す。
    /// </summary>
    public readonly struct SoundReverb : IEquatable<SoundReverb>
    {
        public SoundReverb(float reverbLevelMillibels, float decaySeconds, float diffusionPercent, bool enabled)
        {
            // Disabled values are validated too; Off's zero fields are valid without native application.
            if (float.IsNaN(reverbLevelMillibels) || float.IsInfinity(reverbLevelMillibels))
                throw new ArgumentOutOfRangeException(nameof(reverbLevelMillibels));
            if (float.IsNaN(decaySeconds) || float.IsInfinity(decaySeconds))
                throw new ArgumentOutOfRangeException(nameof(decaySeconds));
            if (float.IsNaN(diffusionPercent) || float.IsInfinity(diffusionPercent))
                throw new ArgumentOutOfRangeException(nameof(diffusionPercent));

            ReverbLevelMillibels = Math.Max(-10000f, Math.Min(2000f, reverbLevelMillibels));
            DecaySeconds = Math.Max(0.1f, Math.Min(20f, decaySeconds));
            DiffusionPercent = Math.Max(0f, Math.Min(100f, diffusionPercent));
            Enabled = enabled;
        }

        public static SoundReverb Off => default;

        public float ReverbLevelMillibels { get; }

        public float DecaySeconds { get; }

        public float DiffusionPercent { get; }

        public bool Enabled { get; }

        public bool Equals(SoundReverb other)
        {
            return ReverbLevelMillibels == other.ReverbLevelMillibels
                && DecaySeconds == other.DecaySeconds
                && DiffusionPercent == other.DiffusionPercent
                && Enabled == other.Enabled;
        }

        public override bool Equals(object? obj)
        {
            return obj is SoundReverb other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ReverbLevelMillibels, DecaySeconds, DiffusionPercent, Enabled);
        }

        public static bool operator ==(SoundReverb left, SoundReverb right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SoundReverb left, SoundReverb right)
        {
            return !left.Equals(right);
        }
    }
}
