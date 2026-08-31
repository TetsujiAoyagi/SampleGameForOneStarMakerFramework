#nullable enable

using UnityEngine;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// セルグリッドのワールド配置定義（原点・セルサイズ・高さ）。
    /// CellScene のバウンズ計算（メタデータ運搬）にのみ使用する。
    /// ロード判断のパラメータ（半径等）は WorldStreamingController 側（T-06）が持つ。
    /// </summary>
    public readonly struct CellGridConfig
    {
        /// <param name="origin">Cell_0_0 の最小コーナーのワールド座標。</param>
        /// <param name="cellSize">1 セルの XZ 一辺の長さ（正方セル）。</param>
        /// <param name="height">バウンズの高さ（Y 方向サイズ）。</param>
        public CellGridConfig(Vector3 origin, float cellSize, float height)
        {
            Origin = origin;
            CellSize = cellSize;
            Height = height;
        }

        /// <summary>Cell_0_0 の最小コーナーのワールド座標。</summary>
        public Vector3 Origin { get; }

        /// <summary>1 セルの XZ 一辺の長さ（正方セル）。</summary>
        public float CellSize { get; }

        /// <summary>バウンズの高さ（Y 方向サイズ）。</summary>
        public float Height { get; }
    }
}
