#nullable enable

using UnityEngine;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// major Event Scene に置く VFX の起動位置。<see cref="SeasonMajorEventScene"/> が Stable でこの位置・回転に
    /// <see cref="VfxAsset"/> の VFX を Event Scene 所有で起動する。この component は authoring の値だけを持ち、判断も状態も持たない。
    /// <para>
    /// VFX asset は直接参照せず、address も持たない。Event Scene の SceneResource が付随 asset として宣言した名前だけを持ち、
    /// Scene class が自分の所有でロードする。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MajorEventVfxAnchor : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("起動する .vfx の付随 asset の名前（この Scene の SceneResource が宣言した名前）。")]
        private string _vfxAsset = string.Empty;

        public string VfxAsset => _vfxAsset;
    }
}
