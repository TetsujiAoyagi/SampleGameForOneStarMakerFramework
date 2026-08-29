#nullable enable

using UnityEngine;

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// シーンが占めるワールド体積への読み取り専用アクセス（34-ondemand-spatial-policy.md §5）。
    ///
    /// <para>
    /// <see cref="ISceneQuery"/> と分けてあるのは寿命が違うためである。
    /// あちらは「ロード済みシーン」への窓であり、空間政策が体積を要るのは
    /// **まだロードしていない候補**についてなので、同じ interface に載せると
    /// あちらの宣言が嘘になる。
    /// </para>
    ///
    /// <para>
    /// 体積は Editor が自動計算してデータへ焼く。ランタイムは読むだけで、
    /// identity から座標を復元したり格子定数から中心を組み立てたりしない。
    /// </para>
    /// </summary>
    public interface ISceneVolumeQuery
    {
        /// <summary>
        /// 距離政策の候補であるシーンのワールド AABB を取得する。
        /// 未登録・候補フラグ off・体積が空のいずれかなら false。
        /// </summary>
        /// <param name="identity">シーンの一意識別子。</param>
        /// <param name="volume">ワールド AABB。false のときは既定値。</param>
        /// <returns>体積が引けたら true。</returns>
        bool TryGetSceneVolume(string identity, out Bounds volume);
    }
}
