namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// Updater によって駆動される最小更新単位。
    /// 重要なのは、この型自身が「実行順序」や「並列化戦略」を決めるのではなく、
    /// Updater 側から与えられた tick を受け取る受動的な Element であること。
    /// </summary>
    public interface IUpdateElement
    {
        /// <summary>
        /// Register 済み Element が active 化されたタイミングで一度だけ呼ばれる。
        /// MonoBehaviour で言えば Start 相当だが、
        /// Element 指向では「Executor に参加した初回フック」として扱う。
        /// </summary>
        void OnElementStart();

        /// <summary>
        /// 通常 Update フェーズ。
        /// Element はここで自身の状態を進めるが、将来の parallel backend を見据え、
        /// Unity main thread 専用 API への依存は極力避ける前提で設計する。
        /// </summary>
        void OnElementUpdate(in UpdateFrameContext context);

        /// <summary>
        /// LateUpdate 相当のフェーズ。
        /// main update 後にしか行えない計算や、後段での状態整形に用いる。
        /// </summary>
        void OnElementLateUpdate(in UpdateFrameContext context);
    }
}
