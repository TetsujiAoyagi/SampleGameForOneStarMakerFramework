#nullable enable

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// AddScene のロード進捗フェーズ。
    /// </summary>
    public enum SceneLoadPhase
    {
        /// <summary>SceneBase の PreLoad 開始。キャンセル可能。</summary>
        PreLoadStarted,

        /// <summary>全 SceneBase の PreLoad 完了。キャンセル可能。</summary>
        PreLoadCompleted,

        /// <summary>Unity Scene ロード開始。ポイント・オブ・ノーリターン通過済み。キャンセル不可。</summary>
        UnitySceneLoading,

        /// <summary>全 Unity Scene ロード + 初期化完了。キャンセル不可。</summary>
        Completed,
    }

    /// <summary>
    /// AddScene の進捗通知データ。IProgress&lt;SceneLoadProgress&gt; で受信する。
    /// </summary>
    public readonly struct SceneLoadProgress
    {
        /// <summary>現在のフェーズ。</summary>
        public SceneLoadPhase Phase { get; }

        /// <summary>まだキャンセル可能か。</summary>
        public bool IsCancelable { get; }

        /// <summary>対象シーンの識別子。</summary>
        public string SceneIdentify { get; }

        public SceneLoadProgress(SceneLoadPhase phase, bool isCancelable, string sceneIdentify)
        {
            Phase = phase;
            IsCancelable = isCancelable;
            SceneIdentify = sceneIdentify;
        }
    }
}
