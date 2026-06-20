namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// main thread でのみ実行できる反映処理。
    /// 本来は native state から導かれた差分適用 command がここへ積まれる想定で、
    /// 今回は stage 境界を固めるために最小契約として導入する。
    /// </summary>
    public interface IMainThreadApplyCommand
    {
        void Apply();
    }
}
