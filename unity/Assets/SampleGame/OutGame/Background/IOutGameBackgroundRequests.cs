#nullable enable

namespace SampleGame.OutGame.Background
{
    /// <summary>
    /// OutGame 配下の画面が共有背景を要求するための同期 API。
    /// </summary>
    public interface IOutGameBackgroundRequests
    {
        /// <summary>現在要求されている背景定義。未要求の場合は null。</summary>
        OutGameBackgroundDefinition? Current { get; }

        /// <summary>共有背景を指定した定義へ即時に切り替える。</summary>
        /// <param name="definition">表示する背景定義。</param>
        void Request(OutGameBackgroundDefinition definition);
    }
}
