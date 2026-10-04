#nullable enable

using System;

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// 領域に載せるリバーブ。数値はバックエンドが解釈する。
    /// Unity ネイティブは、登録時に名前を渡したミキサーの露出パラメータへ送る。
    /// Enabled が偽のときはウェットを 0 として送り、エフェクトを閉じる。
    /// </summary>
    public readonly struct SoundReverb : IEquatable<SoundReverb>
    {
        public SoundReverb(float wet, float decaySeconds, float diffusion, bool enabled)
        {
            Wet = wet;
            DecaySeconds = decaySeconds;
            Diffusion = diffusion;
            Enabled = enabled;
        }

        public static SoundReverb Off => default;

        public float Wet { get; }

        public float DecaySeconds { get; }

        public float Diffusion { get; }

        public bool Enabled { get; }

        public bool Equals(SoundReverb other)
        {
            return Wet == other.Wet
                && DecaySeconds == other.DecaySeconds
                && Diffusion == other.Diffusion
                && Enabled == other.Enabled;
        }

        public override bool Equals(object? obj)
        {
            return obj is SoundReverb other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Wet, DecaySeconds, Diffusion, Enabled);
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
