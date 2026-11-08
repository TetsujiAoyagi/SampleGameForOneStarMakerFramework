#nullable enable

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// probe 1 回の採否の理由（その候補を採らなかった理由、または採った事実）。
    /// 要求全体の終わり方（<see cref="ContentResolutionFailureCode"/>）とは別の値で、
    /// 不在・不達・整合性拒否・規則・不一致・根拠・fault を機械で区別できるようにする。
    /// </summary>
    public enum ProbeOutcome
    {
        None = 0,
        Adopted = 1,
        NotFound = 2,
        Unavailable = 3,
        Rejected = 4,
        RuleRejected = 5,
        PinMismatch = 6,
        KeyMismatch = 7,
        KindMismatch = 8,
        RepresentationMismatch = 9,

        /// <summary>候補の ProviderId / SourceKind が probe した provider と違う。</summary>
        ProviderMismatch = 10,

        /// <summary>根拠が None、または由来に要る根拠と合わない（修復中は VerifiedDigest 以外）。</summary>
        NoAcceptanceBasis = 11,

        /// <summary>provider / 規則の例外、または壊れた回答。探索を止める。</summary>
        Fault = 12,
    }
}
