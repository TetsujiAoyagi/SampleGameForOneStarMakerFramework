#nullable enable

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// resolver の要求の終わり方。誰も採用しなかったときは、probe の実績から次の優先順位で 1 つに決める:
    /// <see cref="IntegrityRejected"/> → <see cref="ProviderFault"/> → <see cref="NoCompatibleCandidate"/> → <see cref="NoCandidate"/>。
    /// </summary>
    public enum ContentResolutionFailureCode
    {
        /// <summary>採用した（<see cref="ContentResolutionResult.IsResolved"/> が true のときの値）。</summary>
        None = 0,

        /// <summary>どの供給元にも無い、または答えられなかった。</summary>
        NoCandidate = 1,

        /// <summary>候補はあったが、規則・受け入れ根拠・表現のどれかで採れなかった。</summary>
        NoCompatibleCandidate = 2,

        /// <summary>整合性拒否、満たされない pin、pin の食い違い。別 revision へ黙って切り替えない。</summary>
        IntegrityRejected = 3,

        /// <summary>provider が要求・自分と食い違う候補を返した、または provider / 規則が fault した。</summary>
        ProviderFault = 4,
    }
}
