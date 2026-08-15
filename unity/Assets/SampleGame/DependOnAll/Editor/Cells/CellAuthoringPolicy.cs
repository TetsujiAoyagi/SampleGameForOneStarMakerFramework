#nullable enable

using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Cells
{
    /// <summary>
    /// Cell の正本方針。Generated は再生成で上書き可、HandAuthored は既存があれば触らない。
    /// </summary>
    public enum CellAuthoringPolicyKind
    {
        /// <summary>生成物が正本。再生成で上書きしてよい。</summary>
        Generated = 0,

        /// <summary>手編集が正本。既存の AuthoredRoot / Environment は触らない。</summary>
        HandAuthored = 1,
    }

    /// <summary>
    /// SampleGame 運用としての Cell → policy 解決。
    /// データはハードコード静的配列（純関数性のため ScriptableObject 資産にはしない）。
    /// </summary>
    public static class CellAuthoringPolicy
    {
        /// <summary>
        /// 手編集正本とする Cell 座標（4×4 の南辺）。
        /// EnvironmentSproutCells と同座標だが意味が違うため統合しない。
        /// </summary>
        private static readonly Vector2Int[] HandAuthoredCells =
        {
            new(0, 0),
            new(1, 0),
            new(2, 0),
            new(3, 0),
        };

        /// <summary>
        /// 座標に対する policy を返す。未指定座標は既定 <see cref="CellAuthoringPolicyKind.Generated"/>。
        /// </summary>
        public static CellAuthoringPolicyKind Resolve(Vector2Int coordinate)
        {
            for (var i = 0; i < HandAuthoredCells.Length; i++)
            {
                if (HandAuthoredCells[i] == coordinate)
                {
                    return CellAuthoringPolicyKind.HandAuthored;
                }
            }

            return CellAuthoringPolicyKind.Generated;
        }

        /// <summary>
        /// 座標に対する policy を返す。未指定座標は既定 <see cref="CellAuthoringPolicyKind.Generated"/>。
        /// </summary>
        public static CellAuthoringPolicyKind Resolve(int x, int y)
            => Resolve(new Vector2Int(x, y));
    }
}
