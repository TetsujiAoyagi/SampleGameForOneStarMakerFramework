#nullable enable

namespace SampleGame.OutGame.Background
{
    /// <summary>
    /// 背景定義を描画する UI Surface。
    /// </summary>
    public interface IOutGameBackgroundSurface
    {
        /// <summary>指定した定義を描画する。</summary>
        /// <param name="definition">表示する背景定義。</param>
        void Apply(OutGameBackgroundDefinition definition);
    }
}
