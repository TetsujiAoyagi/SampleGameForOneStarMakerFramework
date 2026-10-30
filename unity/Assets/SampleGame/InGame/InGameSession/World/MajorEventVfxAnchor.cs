#nullable enable

using UnityEngine;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// major Event Scene に置く VFX の起動位置。<see cref="SeasonMajorEventScene"/> が Stable でこの位置・回転に
    /// <see cref="VfxAddress"/> の VFX を Event Scene 所有で起動する。この component は authoring の値だけを持ち、判断も状態も持たない。
    /// <para>
    /// VFX asset は直接参照しない。Scene の依存にせず、Event Scene が所有者を宣言して <c>IAssetManagement</c> でロードする。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MajorEventVfxAnchor : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("起動する .vfx の Addressables address（asset path）。")]
        private string _vfxAddress = string.Empty;

        public string VfxAddress => _vfxAddress;
    }
}
