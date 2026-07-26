#nullable enable

using UnityEngine;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// World.unity に置く共有マテリアルの配線。
    /// 親（World）が参照を持ち、シーンロード / OnPreLoaded で事前に載せる（FW R-1 の意図）。
    /// 各 Cell は同じ Material アセットを共有し、色は MaterialPropertyBlock で乗せる。
    /// </summary>
    public sealed class WorldMaterialBindings : MonoBehaviour
    {
        /// <summary>Addressables / ディスク上の共有 Lit パス（Editor・PreLoad 契約）。</summary>
        public const string SharedLitAssetPath =
            "Assets/SampleGame/InGame/InGameSession/World/Materials/DemoCellLit.mat";

        [SerializeField] private Material _sharedLit = null!;

        /// <summary>セル Ground / Marker が共有する Lit。</summary>
        public Material SharedLit => _sharedLit;
    }
}
