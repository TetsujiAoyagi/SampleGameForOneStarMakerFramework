#nullable enable

using System;

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// 論理ミックス経路の初期設定。Gain は有限の線形音量 [0,1]、Priority は高いほど残る。
    /// </summary>
    public readonly struct SoundVolumeSettings : IEquatable<SoundVolumeSettings>
    {
        public SoundVolumeSettings(float gain, int priority, SoundReverb reverb)
        {
            if (float.IsNaN(gain) || float.IsInfinity(gain))
            {
                throw new ArgumentOutOfRangeException(nameof(gain));
            }

            Gain = Math.Max(0f, Math.Min(1f, gain));
            Priority = priority;
            Reverb = reverb;
        }

        public float Gain { get; }

        public int Priority { get; }

        public SoundReverb Reverb { get; }

        public bool Equals(SoundVolumeSettings other)
        {
            return Gain == other.Gain && Priority == other.Priority && Reverb == other.Reverb;
        }

        public override bool Equals(object? obj)
        {
            return obj is SoundVolumeSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Gain, Priority, Reverb);
        }
    }
}
