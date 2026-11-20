#nullable enable

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 採用した候補をどの経路でロードするか。由来（<see cref="ContentSourceKind"/>）とは独立した欄。
    /// 現在の loader が実装している経路は <see cref="ContentDirectory"/> と、Editor の build での <see cref="AlreadyLoadedScene"/>
    /// （候補を返した供給元が load の口を持つ時だけ）で、それ以外の経路の候補はロード前に拒む
    /// （別の経路の候補を directory から読むと、記録の由来が虚偽になるため）。
    /// </summary>
    public enum ContentLoadPath
    {
        None = 0,

        /// <summary>登録済みの Content Directory から AssetManagement の content 入口で読む。</summary>
        ContentDirectory = 1,

        /// <summary>Editor の Play の複製の Scene を、load 済みなら採り、無ければ複製の path から読み直して使う（候補を返した供給元が読む）。</summary>
        AlreadyLoadedScene = 2,

        /// <summary>Addressables から読む。</summary>
        Addressables = 3,
    }
}
