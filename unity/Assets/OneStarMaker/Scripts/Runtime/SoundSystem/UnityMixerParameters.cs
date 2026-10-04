#nullable enable

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// Unity ミキサーの露出パラメータ名。空の名前はその値を送らない。
    /// 名前は登録時に保持し、クリップ再生の引数にはしない。
    /// </summary>
    public readonly struct UnityMixerParameters
    {
        public UnityMixerParameters(string? gain, string? reverbWet, string? reverbDecay, string? reverbDiffusion)
        {
            Gain = gain ?? string.Empty;
            ReverbWet = reverbWet ?? string.Empty;
            ReverbDecay = reverbDecay ?? string.Empty;
            ReverbDiffusion = reverbDiffusion ?? string.Empty;
        }

        public string Gain { get; }

        public string ReverbWet { get; }

        public string ReverbDecay { get; }

        public string ReverbDiffusion { get; }

        public bool HasGain => !string.IsNullOrEmpty(Gain);

        public bool HasReverbWet => !string.IsNullOrEmpty(ReverbWet);

        public bool HasReverbDecay => !string.IsNullOrEmpty(ReverbDecay);

        public bool HasReverbDiffusion => !string.IsNullOrEmpty(ReverbDiffusion);
    }
}
