#nullable enable

using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime.AssetManagement
{
    /// <summary>
    /// シーンロード時に backend へ渡すオプション。
    /// </summary>
    public readonly struct SceneLoadOptions
    {
        private readonly bool _hasValue;
        private readonly LoadSceneMode _loadMode;
        private readonly bool _activateOnLoad;
        private readonly int _priority;

        public SceneLoadOptions(
            LoadSceneMode loadMode = LoadSceneMode.Additive,
            bool activateOnLoad = true,
            int priority = 100)
        {
            _hasValue = true;
            _loadMode = loadMode;
            _activateOnLoad = activateOnLoad;
            _priority = priority;
        }

        /// <summary>Unity シーンのロードモード。</summary>
        public LoadSceneMode LoadMode => _hasValue ? _loadMode : LoadSceneMode.Additive;

        /// <summary>ロード完了時に activate するか。</summary>
        public bool ActivateOnLoad => !_hasValue || _activateOnLoad;

        /// <summary>Addressables operation の優先度。</summary>
        public int Priority => _hasValue ? _priority : 100;

        /// <summary>標準ロード設定。</summary>
        public static SceneLoadOptions Default => new();
    }
}
