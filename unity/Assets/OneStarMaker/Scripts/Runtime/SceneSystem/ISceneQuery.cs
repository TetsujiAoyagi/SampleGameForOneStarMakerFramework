#nullable enable

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// ロード済みシーンへの読み取り専用アクセスを提供する。
    /// SceneBase が親シーンや兄弟シーンのサービスを参照するために使用する。
    ///
    /// <para>
    /// SceneDirector の操作 API（AddScene / UnloadScene）は公開しない。
    /// ライフサイクルフック内から遷移を行いたい場合は <see cref="SceneTransitionPlan"/> を使う。
    /// </para>
    ///
    /// <code>
    /// // 子シーンの OnLoadedImpl 内で親のサービスを取得する例:
    /// var parentIdentity = SceneResource.Parent!.Identity;
    /// var parentBase = SceneQuery.GetLoadedScene(parentIdentity) as MasterDataScene;
    /// var repo = parentBase?.MasterDataRepository;
    /// </code>
    /// </summary>
    public interface ISceneQuery
    {
        /// <summary>
        /// 指定 Identity のロード済み SceneBase を取得する。
        /// 未ロードまたはアンロード済みの場合は null を返す。
        /// </summary>
        /// <param name="identity">シーンの一意識別子。</param>
        /// <returns>対応する SceneBase。見つからなければ null。</returns>
        SceneBase? GetLoadedScene(string identity);

        /// <summary>
        /// 指定 Identity のシーンが現在ロード済みかどうかを返す。
        /// PreLoading 以降で true になり得るため、「操作可能」判定には <see cref="IsSceneStable"/> を使う。
        /// </summary>
        /// <param name="identity">シーンの一意識別子。</param>
        /// <returns>ロード済みなら true。</returns>
        bool IsSceneLoaded(string identity);

        /// <summary>
        /// 指定 Identity のシーンが <see cref="SceneState.Stable"/> に到達しているか。
        /// 地形生成や OnLoaded 完了後のスポーン待ちに使う。
        /// </summary>
        /// <param name="identity">シーンの一意識別子。</param>
        /// <returns>Stable なら true。</returns>
        bool IsSceneStable(string identity);
    }
}
