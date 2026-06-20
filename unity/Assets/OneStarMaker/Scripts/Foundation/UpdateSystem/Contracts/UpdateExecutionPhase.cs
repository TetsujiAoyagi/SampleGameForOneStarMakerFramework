namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// scheduler がどの更新フェーズを実行しているかを表す。
    /// 現段階では Update / LateUpdate の 2 つだけだが、
    /// 将来 FixedUpdate や custom phase を追加する際の拡張点として切り出している。
    /// </summary>
    public enum UpdateExecutionPhase
    {
        Update = 0,
        LateUpdate = 1,
    }
}
