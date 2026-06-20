namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// dirty handle から解決された mirror が main thread apply を受けるための契約。
    /// native path の source of truth はあくまで `TState` 側にあり、
    /// この interface は「main thread で mirror/presenter へ差分を反映する入口」だけを表す。
    /// </summary>
    public interface IMainThreadApplyElement
    {
        void ApplyMainThread(in MainThreadApplyContext context);
    }
}
