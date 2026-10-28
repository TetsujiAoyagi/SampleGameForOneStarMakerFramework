#nullable enable

using UnityEngine;
using UnityEngine.VFX;

namespace SampleGame.Common.Rendering.Vfx
{
    /// <summary>
    /// Scene / prefab に配置した <see cref="VisualEffect"/> を、Framework の VfxSystem に登録する対象として示す marker。
    /// <para>
    /// authoring の約束: 同じ GameObject の <see cref="VisualEffect"/> は無効（enabled = false）のまま保存する。
    /// 無効のまま deserialize された component は有効化の処理を走らせないので、登録されるまで（非対応環境では一度も）
    /// 有効にならない。有効化は、Scene class がこの marker を集めて VfxSystem に登録した後、所属する時間の領域が
    /// 止まっていない最初の反映点で VfxSystem が行う。
    /// </para>
    /// <para>
    /// この component 自身は何もしない（<c>Awake</c> でも有効化しない）。判定・有効化・時間の追従・回収は全て VfxSystem が
    /// 持ち、InGame の Scene class は <see cref="VisualEffect"/> の型を知らずに <see cref="Effect"/> を渡すだけにする。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacedVfxActivator : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("ゲームが一時停止・減速しても実時間で動かし続ける（UI の演出など）。既定はゲームの世界の時間に追従する。")]
        private bool _unscaledTime;

        /// <summary>
        /// 同じ GameObject の配置済み VFX（<see cref="VisualEffect"/>）を、型を出さずに返す。無ければ null。
        /// </summary>
        public Component? Effect
        {
            get
            {
                var effect = GetComponent<VisualEffect>();
                return effect != null ? effect : null;
            }
        }

        /// <summary>true なら実時間（unscaled）で進む。false ならゲームの世界の時間（Gameplay Layer）に追従する。</summary>
        public bool UsesUnscaledTime => _unscaledTime;
    }
}
