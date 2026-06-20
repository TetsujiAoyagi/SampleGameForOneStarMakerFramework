using System.Collections.Generic;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// native pipeline の並び順を統一する比較子。
    /// Layer ごとの局所列と world 全体列のどちらでも同じ規則を使い、
    /// 実行順の説明が場所ごとにずれないようにする。
    /// </summary>
    internal sealed class NativePipelineOrderComparer : IComparer<INativeExecutionPipeline>
    {
        public static readonly NativePipelineOrderComparer Instance = new();

        public int Compare(INativeExecutionPipeline x, INativeExecutionPipeline y)
        {
            var layer = string.CompareOrdinal(x.LayerId, y.LayerId);
            if (layer != 0)
            {
                return layer;
            }

            return x.PipelineOrder.CompareTo(y.PipelineOrder);
        }
    }
}
