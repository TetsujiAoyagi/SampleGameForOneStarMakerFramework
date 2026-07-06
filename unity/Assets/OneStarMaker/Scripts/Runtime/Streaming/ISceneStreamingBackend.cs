#nullable enable

using Cysharp.Threading.Tasks;

namespace OneStarMaker.Runtime.Streaming
{
    /// <summary>
    /// セルストリーミングのメカニズム層（SceneDirector 等）への差分要求インターフェース。
    /// WorldStreamingController（ポリシー層）と実装を分離する撤退ライン（21-scene-streaming.md §11）。
    /// </summary>
    public interface ISceneStreamingBackend
    {
        /// <summary>セルのロードを要求する。完了はセルの Stable 到達を保証しない（G-6）。</summary>
        UniTask RequestAdd(string cellId, int priority);

        /// <summary>セルのアンロードを要求する。窓内キャンセル/保留はバックエンド側で収束する。</summary>
        UniTask RequestRemove(string cellId);

        /// <summary>現在ロード済み（Stable）のセル集合を観測する。再照合（G-6）の入力。</summary>
        bool IsLoaded(string cellId);
    }
}
