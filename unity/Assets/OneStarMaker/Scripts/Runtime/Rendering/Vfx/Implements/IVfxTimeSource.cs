#nullable enable

namespace OneStarMaker.Runtime.Rendering.Vfx
{
    /// <summary>
    /// VFX の時間の領域を計算するための、そのフレームの時間の値と Layer の状態の出所。
    /// <para>
    /// VfxSystem と <see cref="VfxTimeLayerBinding"/> は <c>UnityEngine.Time</c> や UpdateSystem を直接読まず、
    /// この口だけから読む（偽の値でフレーム列を組み立てて単体でテストするため）。実装は composition
    /// （<see cref="VfxRuntimeHost"/>）が <c>UnityEngine.Time</c> と UpdateCoordinator で行う。
    /// </para>
    /// </summary>
    internal interface IVfxTimeSource
    {
        /// <summary>
        /// gameplay の要素が受け取る基準の delta（<c>Time.deltaTime</c>）。<c>Time.timeScale</c> が掛かり、
        /// <c>Time.maximumDeltaTime</c> で頭打ちになった値をそのまま返す（Layer の倍率は掛けない）。
        /// </summary>
        float DeltaTime { get; }

        /// <summary>実時間の delta（<c>Time.unscaledDeltaTime</c>）。頭打ちにならない。</summary>
        float UnscaledDeltaTime { get; }

        /// <summary>
        /// 1 フレームの delta の上限（<c>Time.maximumDeltaTime</c>）。unscaled の領域の時間はこの値で頭打ちにする
        /// （<c>Time.deltaTime</c> と同じ上限を持たせ、timeScale だけを外した時間にするため）。
        /// </summary>
        float MaximumDeltaTime { get; }

        /// <summary>
        /// Layer の状態を返す。Layer がまだ無ければ false（呼び出し側は「止まっていない、倍率 1」とみなす）。
        /// 存在しない Layer を作ってはいけない。
        /// </summary>
        bool TryGetLayerState(string layerId, out bool isPaused, out float timeScale);
    }
}
