#nullable enable

using System;
using OneStarMaker.Runtime.UISystem;
using UnityEngine;

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// セルシーンの基底クラス。
    ///
    /// 責務は**セル座標・バウンズのメタデータ運搬のみ**（21-scene-streaming.md D-3）。
    /// 距離判定・ロード判断のロジックは WorldStreamingController に集約されるため、
    /// このクラス（および派生クラス）に持たせてはならない。
    ///
    /// 構造的制約:
    /// - UIView 検索を行わない（R-2。セルは UIView を持たない）
    /// - identity は `Cell_{x}_{y}` 形式必須（違反はコンストラクタで即失敗）
    /// - LoadingDisplayType は常に None 前提（R-4。Controller が固定値で呼ぶ）
    /// </summary>
    public class CellScene : SceneBase
    {
        public CellScene(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController)
            : base(sceneResource, sceneQuery, sceneController)
        {
            if (!CellIdentity.TryParse(sceneResource.Identity, out var coordinate))
            {
                throw new ArgumentException(
                    $"CellScene の identity は 'Cell_{{x}}_{{y}}' 形式である必要があります: '{sceneResource.Identity}'",
                    nameof(sceneResource));
            }

            Coordinate = coordinate;
        }

        /// <summary>グリッド座標（identity `Cell_{x}_{y}` から解析）。</summary>
        public Vector2Int Coordinate { get; }

        /// <summary>グリッド定義からこのセルのワールドバウンズを計算する。</summary>
        public Bounds ComputeBounds(in CellGridConfig grid)
        {
            var size = new Vector3(grid.CellSize, grid.Height, grid.CellSize);
            var min = grid.Origin + new Vector3(Coordinate.x * grid.CellSize, 0f, Coordinate.y * grid.CellSize);
            return new Bounds(min + size * 0.5f, size);
        }

        /// <summary>セルは UIView を持たない（R-2 構造的強制）。検索自体を行わない。</summary>
        protected sealed override UIView? SearchUIView() => null;
    }
}
