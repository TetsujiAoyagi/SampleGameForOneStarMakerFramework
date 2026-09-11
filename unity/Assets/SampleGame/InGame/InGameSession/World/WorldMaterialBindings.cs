#nullable enable

using UnityEngine;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// 共有 Lit の Addressables パス契約。実体は Seasons/Materials。GUID 維持で移した。
    /// 各 Cell は同じ Material アセットを共有し、色は MaterialPropertyBlock で乗せる。
    /// GO 配線コンポーネントとしては退役予定。パス定数だけ SeasonScene.PreLoad が使う。
    /// </summary>
    public sealed class WorldMaterialBindings : MonoBehaviour
    {
        /// <summary>Addressables / ディスク上の共有 Lit パス（Editor・PreLoad 契約）。</summary>
        public const string SharedLitAssetPath =
            "Assets/SampleGame/InGame/InGameSession/Seasons/Materials/DemoCellLit.mat";

        [SerializeField] private Material _sharedLit = null!;

        /// <summary>セル Ground / Marker が共有する Lit。</summary>
        public Material SharedLit => _sharedLit;
    }
}
