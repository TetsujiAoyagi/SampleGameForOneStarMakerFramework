#nullable enable

using OneStarMaker.Runtime.UISystem;
using SampleGame.OutGame.Background;
using UnityEngine;

namespace SampleGame.OutGame.Title
{
    /// <summary>
    /// タイトル画面の UI Toolkit View。
    /// 背景定義は Scene の presentation asset として保持し、描画は親 OutGame へ委譲する。
    /// </summary>
    public sealed class TitleView : UIToolkitView
    {
        [SerializeField]
        private OutGameBackgroundDefinition? _backgroundDefinition;

        /// <summary>タイトル画面が要求する共有背景。</summary>
        public OutGameBackgroundDefinition? BackgroundDefinition => _backgroundDefinition;

#if UNITY_EDITOR
        /// <summary>Editor のシーン生成ツールから共有背景を割り当てる。</summary>
        /// <param name="definition">タイトル表示時に要求する背景定義。</param>
        public void AssignBackgroundDefinitionForEditor(OutGameBackgroundDefinition definition)
        {
            _backgroundDefinition = definition
                ?? throw new System.ArgumentNullException(nameof(definition));
        }
#endif

        /// <inheritdoc />
        public override UILayer GetUILayer() => UILayer.Normal;
    }
}
