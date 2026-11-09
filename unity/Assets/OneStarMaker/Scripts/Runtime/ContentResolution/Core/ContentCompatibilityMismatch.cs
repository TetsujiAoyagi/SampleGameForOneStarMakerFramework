#nullable enable

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// <see cref="ContentCompatibilityRule.Evaluate"/> の判定結果の種別。採らない理由が 1 つのときはその理由、
    /// 採るときは <see cref="Compatible"/>。複数の理由に当たる候補は、規則の判定順で最初に当たった 1 つだけを返す。
    /// </summary>
    /// <remarks>
    /// 値の名前は理由の文字列 <c>compat:&lt;名前&gt;:</c> にそのまま使い、後続の供給元 adapter・入口・観測が機械で読む。
    /// そのため名前の変更・削除は理由の形式の変更になる。並びは比較に使わないが、0 だけは意味を持つ:
    /// <see cref="Unset"/> を 0 に置き、<c>default</c> の値が「互換」と読めないようにしている。
    /// </remarks>
    public enum ContentCompatibilityMismatch
    {
        /// <summary>無効な値（判定していない）。規則はこの値を返さない。</summary>
        Unset = 0,

        /// <summary>採ってよい。</summary>
        Compatible = 1,

        /// <summary>由来と受け入れ根拠の組が、規則が判定できる組（resolver の固定表と同じ）でない。</summary>
        UnsupportedSource = 2,

        /// <summary>要求の expected identity と候補の identity が全欄一致しない（由来に関係なく比べる）。</summary>
        ExpectedIdentityMismatch = 3,

        /// <summary>directory の候補の identity の 4 欄のどれかが空（空白だけを含む）。</summary>
        IncompleteIdentity = 4,

        /// <summary>互換の基準の content set と一致しない。</summary>
        ContentSetMismatch = 5,

        /// <summary>互換の基準が revision を固定していて、それと一致しない。</summary>
        RevisionMismatch = 6,

        /// <summary>互換の基準の target と一致しない。</summary>
        TargetMismatch = 7,

        /// <summary>engine の version の事実が無い、または空・空白だけ。</summary>
        EngineVersionMissing = 8,

        /// <summary>engine の version の事実が互換の基準と一致しない。</summary>
        EngineVersionMismatch = 9,

        /// <summary>root schema の version の事実が無い、または空・空白だけ。</summary>
        SchemaVersionMissing = 10,

        /// <summary>root schema の version の事実が正規の 10 進表記でない。</summary>
        SchemaVersionMalformed = 11,

        /// <summary>root schema の version の事実が互換の基準と一致しない。</summary>
        SchemaVersionMismatch = 12,

        /// <summary>汎用の判定は通ったが、ホストが登録した述語のどれかが採らないと答えた。</summary>
        HostPredicateRejected = 13,
    }
}
