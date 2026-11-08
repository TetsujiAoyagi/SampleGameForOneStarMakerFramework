#nullable enable

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 候補の由来（内容がどこから来たか）。ロード経路（<see cref="ContentLoadPath"/>）とは別の欄で、
    /// 由来は受け入れ根拠の固定表を引く鍵、経路は loader が扱えるかを決める鍵として使い分ける。
    /// 0 は記録に使えない値で、由来の無い候補・記録を作らせないために constructor が拒む。
    /// </summary>
    public enum ContentSourceKind
    {
        None = 0,

        /// <summary>
        /// Editor で開いている Scene の複製。既ロードの Scene をそのまま使う機構でも、
        /// 複製から一時 directory を build する機構でも、由来としては同じ値を使う。
        /// </summary>
        EditorSnapshot = 1,

        /// <summary>検証済みの built Content Directory。</summary>
        ContentDirectory = 2,

        /// <summary>Addressables。受け入れ根拠が未定義なので、現在の resolver は採用しない。</summary>
        Addressables = 3,
    }
}
