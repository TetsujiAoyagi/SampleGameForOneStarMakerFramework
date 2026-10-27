#nullable enable

using System.Collections.Generic;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.UpdateSystem.Api;
using SampleGame.Common.Rendering.Vfx;
using UnityEngine;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// Scene の root から配置済み VFX の marker（<see cref="PlacedVfxActivator"/>）を集め、VfxSystem に登録する。
    /// <para>
    /// 所有者は配置した Scene の <c>Scene(id)</c>。VfxSystem はその Scene の Unload の開始（Unity Scene の unload より前）で
    /// 回収するので、Scene class は handle を持たない。role を見ずに全ての marker を登録するので、VFX を置いていない
    /// Scene（Environment・Lighting・Events）では何もしない。走査は Scene のロード時に 1 回だけ。
    /// </para>
    /// <para>
    /// VFX 実装の型（<c>VisualEffect</c>）は使わない。marker が返す component をそのまま渡す。
    /// 非対応環境では VfxSystem が何もしない handle を返すので、配置済み VFX は無効のまま残る。
    /// </para>
    /// </summary>
    internal static class PlacedVfxAdopter
    {
        /// <summary>marker を集めて登録し、受理された件数を返す（何もしない handle は数えない）。</summary>
        internal static int AdoptAll(IReadOnlyList<GameObject> roots, IVfxSystem vfx, AssetOwner sceneOwner)
        {
            var adopted = 0;
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                // 非 active の子も登録する。有効化は Unity の規則どおり、GameObject が active になった時点で効く。
                foreach (var marker in root.GetComponentsInChildren<PlacedVfxActivator>(includeInactive: true))
                {
                    var effect = marker.Effect;
                    if (effect == null)
                    {
                        continue;
                    }

                    var timeLayerId = marker.UsesUnscaledTime ? null : UpdateLayerIds.Gameplay;
                    if (vfx.Adopt(effect, sceneOwner, timeLayerId).IsActive)
                    {
                        adopted++;
                    }
                }
            }

            return adopted;
        }
    }
}
