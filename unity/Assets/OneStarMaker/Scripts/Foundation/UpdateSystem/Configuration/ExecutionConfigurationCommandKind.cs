namespace OneStarMaker.Foundation.UpdateSystem.Configuration
{
    /// <summary>
    /// owner thread が実行構成変更フェーズで解釈する変更要求の種別。
    /// layer への登録、解除、並び替えだけを明示的に表す。
    /// </summary>
    public enum ExecutionConfigurationCommandKind
    {
        Register,
        Unregister,
        Reorder,
    }
}
