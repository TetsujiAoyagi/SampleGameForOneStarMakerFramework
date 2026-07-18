#nullable enable

using UnityEngine;

namespace SampleGame.OutGame.Background
{
    /// <summary>
    /// OutGame 共有背景の表示内容を表すゲーム固有の定義。
    /// レイアウトと拡縮方法は UXML / USS が所有する。
    /// </summary>
    [CreateAssetMenu(
        fileName = "OutGameBackgroundDefinition",
        menuName = "SampleGame/OutGame/Background Definition")]
    public sealed class OutGameBackgroundDefinition : ScriptableObject
    {
        [SerializeField]
        private Texture2D? _texture;

        [SerializeField]
        private Color _tint = Color.white;

        /// <summary>背景に表示するテクスチャ。</summary>
        public Texture2D? Texture => _texture;

        /// <summary>テクスチャへ乗算する色。</summary>
        public Color Tint => _tint;

        /// <summary>描画可能なテクスチャを持つか。</summary>
        public bool IsValid => _texture != null;
    }
}
