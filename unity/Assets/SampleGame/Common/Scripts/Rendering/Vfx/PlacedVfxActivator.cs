#nullable enable

using UnityEngine;
using UnityEngine.VFX;

namespace SampleGame.Common.Rendering.Vfx
{
    /// <summary>
    /// Scene / prefab に配置した <see cref="VisualEffect"/> を、VFX Graph の対応環境でだけ有効にする。
    /// <para>
    /// authoring の約束: 同じ GameObject の <see cref="VisualEffect"/> は無効（enabled = false）のまま保存する。
    /// 無効のまま deserialize された component は有効化の処理を走らせないので、非対応環境では一度も有効にならない
    /// （有効のまま保存して早い Awake で無効にする方式は、native の有効化が script の順序と独立に起きうるので採らない）。
    /// 有効にした時点で初期イベント（既定 OnPlay）が送られ、配置した VFX は自動で再生される。
    /// </para>
    /// <para>
    /// 判定は自分で行わず、<see cref="VfxCapabilityState"/> を読むだけにする。未判定は非対応と同じに扱う。
    /// 有効から無効にする処理は持たない（非対応の環境で一度有効になったものを後から止める仕組みではない）。ログも出さない。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacedVfxActivator : MonoBehaviour
    {
        private void Awake()
        {
            if (VfxCapabilityState.Current != VfxCapability.Supported)
            {
                return;
            }

            var visualEffect = GetComponent<VisualEffect>();
            if (visualEffect != null)
            {
                visualEffect.enabled = true;
            }
        }
    }
}
