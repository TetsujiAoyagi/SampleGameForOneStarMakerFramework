#nullable enable

using UnityEngine;

namespace OneStarMaker.Editor.Streaming
{
    /// <summary>
    /// ワールドセルグリッドの配置・出力定義。
    /// World Cell Generator の入力データとして使用する ScriptableObject。
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldGridDefinition",
        menuName = "OneStarMaker/Streaming/World Grid Definition")]
    public sealed class WorldGridDefinition : ScriptableObject
    {
        [SerializeField]
        private Vector3 _origin = Vector3.zero;

        [SerializeField]
        private float _cellSize = 100f;

        [SerializeField]
        private int _gridWidth = 10;

        [SerializeField]
        private int _gridHeight = 10;

        [SerializeField]
        private string _parentSceneIdentity = "World";

        [SerializeField]
        private string _sceneOutputFolder = "Assets/OneStarMakerCommon/World/Cells";

        [SerializeField]
        private string _sceneResourceOutputFolder = "Assets/OneStarMakerCommon/SceneMap/Cells";

        /// <summary>Cell_0_0 の最小コーナーのワールド座標。</summary>
        public Vector3 Origin => _origin;

        /// <summary>1 セルの XZ 一辺の長さ（正方セル）。</summary>
        public float CellSize => _cellSize;

        /// <summary>グリッドの X 方向セル数。</summary>
        public int GridWidth => _gridWidth;

        /// <summary>グリッドの Y 方向セル数（グリッド座標 y）。</summary>
        public int GridHeight => _gridHeight;

        /// <summary>全セルの親シーン identity（既定: World）。</summary>
        public string ParentSceneIdentity => _parentSceneIdentity;

        /// <summary>セル .unity シーンの出力先フォルダ（Assets 相対パス）。</summary>
        public string SceneOutputFolder => _sceneOutputFolder;

        /// <summary>セル SceneResource .asset の出力先フォルダ（Assets 相対パス）。</summary>
        public string SceneResourceOutputFolder => _sceneResourceOutputFolder;

        /// <summary>グリッド内のセル総数（幅 × 高さ）。</summary>
        public int CellCount => _gridWidth * _gridHeight;
    }
}
