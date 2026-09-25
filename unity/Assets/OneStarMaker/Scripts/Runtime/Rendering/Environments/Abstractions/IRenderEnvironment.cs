#nullable enable

namespace OneStarMaker.Runtime.Rendering.Environments
{
    /// <summary>
    /// App 寿命の単一環境オーナー窓口。同時に権利証を持てるのは 1 件だけで、
    /// 二件目の <see cref="Acquire"/> は待たず即失敗する。
    /// Game 層は装置（RenderSettings / Light）へ直接書かず、ここから lease を取る。
    /// </summary>
    public interface IRenderEnvironment
    {
        /// <summary>いま権利証を持っている owner がいるか。待ち行列は持たない。</summary>
        bool HasActiveOwner { get; }

        /// <summary>
        /// 空きなら lease を返す。既に owner がいる、または Environment 破棄後は失敗する。
        /// 成功時に sink が baseline を Capture する。暗黙の上書きはしない。
        /// </summary>
        RenderEnvironmentLease Acquire(object ownerKey);
    }
}
