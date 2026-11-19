#nullable enable

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 候補が申告する受け入れ根拠。由来ごとにどの根拠が要るかは <see cref="ContentResolver"/> の固定表が決める。
    /// この表は「どの根拠なら使ってよいか」の境界であり、identity・digest・target・schema の互換判定ではない
    /// （互換判定は <see cref="IContentCompatibilityRule"/> の製品実装が担う）。
    /// 根拠は provider の申告のまま記録され、申告の真偽は provider adapter の責任で、この契約では検証しない。
    /// </summary>
    public enum ContentAcceptanceBasis
    {
        /// <summary>根拠なし。規則が採ってよいと答えても採用しない。</summary>
        None = 0,

        /// <summary>manifest と digest の検証を通した正規コピー。</summary>
        VerifiedDigest = 1,

        /// <summary>Editor で開いている Scene の Play の前の複製。digest で検証した物ではなく、採る理由は利用者が開いている Scene であること。</summary>
        OpenSceneSnapshot = 2,

        /// <summary>
        /// 上書きの directory の root が記録した base と、同じ時に登録した base の検証済みの値が一致した（上書きの登録が照合した）。
        /// </summary>
        /// <remarks>
        /// 保証の境界: 互換の規則は記録の事実があることと基準との整合だけを見て、記録した base と実際に登録した base の一致そのものは
        /// 確かめない（基準に base の digest は無く、事実は供給元の申告である。<see cref="VerifiedDigest"/> でも core は digest を検証しないのと同じ契約）。
        /// 一致を保証するのは次の 3 つの輪である。(1) 登録の口が、上書きの root の記録と base の検証済みの値を照合する。
        /// (2) 上書きの供給元は、照合を通った登録からしか作られない（供給元も組み立ても internal）。
        /// (3) loader は、internal の照合（<c>IContentSourceBinding</c>）を答える供給元の候補だけを読み、外部の供給元の候補は読まない。
        /// 記録は上書きの build の呼び手が渡した base の値の写しで、上書きがその base の内容から作られたことは保証しない。
        /// </remarks>
        RecordedBaseMatch = 3,
    }
}
