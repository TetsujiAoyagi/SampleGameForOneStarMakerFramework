#nullable enable

using OneStarMaker.Runtime.AssetManagement;

namespace OneStarMaker.Runtime.AssetManagement.Cache
{
    /// <summary>
    /// AssetType 別のキャッシュバジェットを提供する。
    /// </summary>
    public interface IBudgetProvider
    {
        /// <summary>
        /// type のキャッシュバジェット（バイト）を返す。
        /// 未定義の type は 0 を返し、その type はキャッシュされない。
        /// </summary>
        long GetBudgetBytes(AssetType type);
    }
}
