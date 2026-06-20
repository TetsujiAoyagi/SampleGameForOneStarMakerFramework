using System.Collections.Generic;
using OneStarMaker.Foundation.UpdateSystem.Layers;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// Layer の実行順を決める比較子。
    /// `UpdateCoordinator` が Layer を生成するたびに同じ規則で sort し、
    /// フレーム中は単純な順走査だけで済むようにする。
    /// </summary>
    internal sealed class LayerOrderComparer : IComparer<UpdateLayer>
    {
        public static readonly LayerOrderComparer Instance = new();

        public int Compare(UpdateLayer x, UpdateLayer y)
        {
            var order = x.LayerOrder.CompareTo(y.LayerOrder);
            return order != 0
                ? order
                : string.CompareOrdinal(x.LayerId, y.LayerId);
        }
    }
}
