#nullable enable

using System;

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// 領域の初期設定。Gain は線形の音量、Priority は高いほど残る。
    /// 短い再生 API は、この Priority をその領域の既定として使う。
    /// </summary>
    public readonly struct SoundVolumeSettings : IEquatable<SoundVolumeSettings>
    {
        public SoundVolumeSettings(float gain, int priority, SoundReverb reverb)
        {
            Gain = gain;
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
