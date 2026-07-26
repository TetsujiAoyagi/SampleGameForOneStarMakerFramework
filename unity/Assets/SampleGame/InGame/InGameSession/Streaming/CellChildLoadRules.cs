#nullable enable

namespace SampleGame.InGame.Streaming
{
    /// <summary>
    /// Cell 子シーン（Environment 等）の明示ロード判定。純関数・テスト対象。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cell は「距離ストリーミング境界」と「人間の作業単位」を兼ねる。
    /// 子（Environment / NPC / VFX …）は職種分割のための Unity シーンであり、
    /// WSC の desired set には絶対に入れない。
    /// </para>
    /// 意図:
    /// <list type="bullet">
    /// <item>距離判断の単位は常に Cell（WSC）。子は desired set に混ぜない。</item>
    /// <item>Cell の Add だけでは子は載らない（OnDemand・引っ張られない）。</item>
    /// <item>Cell が Stable になったあと、デモ用ポリシーが子を明示 Add する。</item>
    /// <item>Unload は親再帰に任せるため、ここから Remove は発行しない。</item>
    /// </list>
    /// </remarks>
    public static class CellChildLoadRules
    {
        /// <summary>
        /// 親 Cell が Stable で、Map 上に子が存在し、まだ載っていないときだけ Add する。
        /// </summary>
        /// <param name="cellIsStable">親 Cell が Backend.IsLoaded（= Stable）か。</param>
        /// <param name="childExistsInMap">対応する子 SceneResource がカタログにあるか。</param>
        /// <param name="childIsLoaded">子が既に Stable か。</param>
        /// <returns>明示 Add すべきなら true。</returns>
        public static bool ShouldAddChild(bool cellIsStable, bool childExistsInMap, bool childIsLoaded)
            => cellIsStable && childExistsInMap && !childIsLoaded;
    }
}
