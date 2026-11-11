#nullable enable

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 候補の <see cref="ContentCandidate.Facts"/> に互換の事実を書く予約 key。
    /// 既存の候補の型に欄を足さずに engine と root schema の version を渡すための入れ物で、
    /// 値を書くのは供給元の adapter、読むのは <see cref="ContentCompatibilityRule"/> である。
    /// </summary>
    /// <remarks>
    /// key が無い・値が空の候補は採られない（比べられない事実を「一致」と扱わないため）。
    /// <see cref="ReservedPrefix"/> の下の、ここに無い key は規則が読まず、判定に影響しない。
    /// ホスト固有の事実（宣言の名前・表現・graph の version 等）はここに定義しない。
    /// それらはホストが自分の key で書き、自分の述語で比べる（汎用の判定はホストの宣言を知らない）。
    /// </remarks>
    public static class ContentCompatibilityFactKeys
    {
        /// <summary>予約 key の接頭辞。ホスト固有の key はこの接頭辞を使わない。</summary>
        public const string ReservedPrefix = "osm.compat.";

        /// <summary>候補を作った engine の version。互換の基準の値と ordinal で完全一致させる。</summary>
        public const string EngineVersion = ReservedPrefix + "engineVersion";

        /// <summary>
        /// 候補の root schema の version。ASCII の <c>1</c>〜<c>9</c> で始まる 9 桁以内の 10 進表記だけを受け付ける。
        /// </summary>
        public const string RootSchemaVersion = ReservedPrefix + "rootSchemaVersion";
    }
}
