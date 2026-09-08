#nullable enable

using System;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using UnityEngine;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// セルシーンの基底クラス。
    ///
    /// 責務は authoring 済みバウンズのメタデータ運搬のみ（21-scene-streaming.md D-3）。
    /// 距離判定・ロード判断のロジックは WorldStreamingController に集約されるため、
    /// このクラス（および派生クラス）に持たせてはならない。
    ///
    /// 構造的制約:
    /// - UIView 検索を行わない（R-2。セルは UIView を持たない）
    /// - StreamByDistance が true の構造的 Cell 専用
    /// - LoadingDisplayType は常に None 前提（R-4。Controller が固定値で呼ぶ）
    /// </summary>
    public class CellScene : SceneBase
    {
        public CellScene(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController)
            : base(sceneResource, sceneQuery, sceneController)
        {
            if (!sceneResource.StreamByDistance)
            {
                throw new ArgumentException(
                    $"CellScene requires SceneResource.StreamByDistance=true: '{sceneResource.Identity}'",
                    nameof(sceneResource));
            }
        }

        /// <summary>authoring source が保持する volume を加工せず返す。</summary>
        public Bounds Bounds => SceneResource.Volume;

        /// <summary>セルは UIView を持たない（R-2 構造的強制）。検索自体を行わない。</summary>
        protected sealed override UIView? SearchUIView() => null;
    }
}
