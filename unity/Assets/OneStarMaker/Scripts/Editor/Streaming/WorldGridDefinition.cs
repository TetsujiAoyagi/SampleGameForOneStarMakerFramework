#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneStarMaker.Editor.Streaming
{
    /// <summary>
    /// セル格子上の軸揃え矩形。幅・高さは 1 以上。
    /// </summary>
    public readonly struct CellRect
    {
        public CellRect(Vector2Int origin, Vector2Int size)
        {
            if (size.x < 1 || size.y < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(size),
                    size,
                    "矩形サイズは幅・高さとも 1 以上である必要があります。");
            }

            Origin = origin;
            Size = size;
        }

        public Vector2Int Origin { get; }

        /// <summary>x = 幅, y = 高さ。どちらも 1 以上。</summary>
        public Vector2Int Size { get; }

        public bool Contains(Vector2Int coordinate)
        {
            return coordinate.x >= Origin.x
                && coordinate.y >= Origin.y
                && coordinate.x < Origin.x + Size.x
                && coordinate.y < Origin.y + Size.y;
        }
    }

    /// <summary>
    /// ワールドセルグリッドの配置・出力定義。
    /// World Cell Generator の入力データとして使用する ScriptableObject。
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldGridDefinition",
        menuName = "OneStarMaker/Streaming/World Grid Definition")]
    public sealed class WorldGridDefinition : ScriptableObject
    {
        [Serializable]
        private struct SerializedCellRect
        {
            public Vector2Int origin;
            public Vector2Int size;
        }

        [SerializeField]
        private Vector3 _origin = Vector3.zero;

        [SerializeField]
        private float _cellSize = 100f;

        [SerializeField]
        private List<SerializedCellRect> _rectangles = new()
        {
            new SerializedCellRect
            {
                origin = Vector2Int.zero,
                size = new Vector2Int(10, 10),
            },
        };

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

        /// <summary>セル矩形の集合。本番は 1 要素。不正サイズ・重なり・空集合は例外。</summary>
        public IReadOnlyList<CellRect> Rectangles
        {
            get
            {
                var src = RequireValidRectangles();
                var result = new CellRect[src.Count];
                for (var i = 0; i < src.Count; i++)
                {
                    result[i] = new CellRect(src[i].origin, src[i].size);
                }

                return result;
            }
        }

        /// <summary>全セルの親シーン identity（既定: World）。</summary>
        public string ParentSceneIdentity => _parentSceneIdentity;

        /// <summary>セル .unity シーンの出力先フォルダ（Assets 相対パス）。</summary>
        public string SceneOutputFolder => _sceneOutputFolder;

        /// <summary>セル SceneResource .asset の出力先フォルダ（Assets 相対パス）。</summary>
        public string SceneResourceOutputFolder => _sceneResourceOutputFolder;

        /// <summary>矩形集合を展開したセル総数。</summary>
        public int CellCount => EnumerateCells().Count;

        public bool Contains(Vector2Int coordinate)
        {
            var src = RequireValidRectangles();
            for (var i = 0; i < src.Count; i++)
            {
                var origin = src[i].origin;
                var size = src[i].size;
                if (coordinate.x >= origin.x
                    && coordinate.y >= origin.y
                    && coordinate.x < origin.x + size.x
                    && coordinate.y < origin.y + size.y)
                {
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<Vector2Int> EnumerateCells()
        {
            var src = RequireValidRectangles();
            var cells = new List<Vector2Int>();
            for (var r = 0; r < src.Count; r++)
            {
                var origin = src[r].origin;
                var size = src[r].size;
                for (var y = 0; y < size.y; y++)
                {
                    for (var x = 0; x < size.x; x++)
                    {
                        cells.Add(new Vector2Int(origin.x + x, origin.y + y));
                    }
                }
            }

            return cells;
        }

        private List<SerializedCellRect> RequireValidRectangles()
        {
            var src = _rectangles;
            if (src == null || src.Count == 0)
            {
                throw new ArgumentException("矩形集合は 1 件以上である必要があります。");
            }

            for (var i = 0; i < src.Count; i++)
            {
                var size = src[i].size;
                if (size.x < 1 || size.y < 1)
                {
                    throw new ArgumentException($"矩形サイズは幅・高さとも 1 以上が必要です: {size}");
                }

                for (var j = i + 1; j < src.Count; j++)
                {
                    var a = src[i];
                    var b = src[j];
                    if (a.origin.x < b.origin.x + b.size.x
                        && b.origin.x < a.origin.x + a.size.x
                        && a.origin.y < b.origin.y + b.size.y
                        && b.origin.y < a.origin.y + a.size.y)
                    {
                        throw new ArgumentException("矩形同士の重なりは禁止です。");
                    }
                }
            }

            return src;
        }
    }
}
