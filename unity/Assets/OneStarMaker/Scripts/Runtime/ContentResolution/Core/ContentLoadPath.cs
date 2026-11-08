#nullable enable

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 採用した候補をどの経路でロードするか。由来（<see cref="ContentSourceKind"/>）とは独立した欄。
    /// 現在の loader が実装している経路は <see cref="ContentDirectory"/> だけで、それ以外の経路の候補は
    /// ロード前に拒む（別の経路の候補を directory から読むと、記録の由来が虚偽になるため）。
    /// </summary>
    public enum ContentLoadPath
    {
        None = 0,

        /// <summary>登録済みの Content Directory から AssetManagement の content 入口で読む。</summary>
        ContentDirectory = 1,

        /// <summary>Play 前に既にロードされている Scene をそのまま使う。</summary>
        AlreadyLoadedScene = 2,

        /// <summary>Addressables から読む。</summary>
        Addressables = 3,
    }
}
