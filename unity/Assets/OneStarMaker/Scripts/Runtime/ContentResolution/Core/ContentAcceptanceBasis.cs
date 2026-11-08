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

        /// <summary>Editor で開いている Scene の複製。意味の詳細は snapshot 機構を決める後続の作業が定める。</summary>
        OpenSceneSnapshot = 2,
    }
}
