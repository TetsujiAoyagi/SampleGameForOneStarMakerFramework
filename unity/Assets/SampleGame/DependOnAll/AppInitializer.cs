#nullable enable

using OneStarMaker.Runtime;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;

namespace SampleGame.DependOnAll
{
    /// <summary>
    /// アプリケーション起動エントリーポイント。
    /// AbstractApplicationInitializer の abstract メソッドを実装する。
    /// </summary>
    public sealed class AppInitializer : AbstractApplicationInitializer
    {
        private static readonly AppInitializer s_instance = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Sub() => BootstrapSubsystemRegistration(s_instance);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Before() => BootstrapBeforeSceneLoad(s_instance);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void After() => BootstrapAfterSceneLoad(s_instance);

        protected override ISceneFactory CreateSceneFactory()
            => new GameSceneFactory();

        protected override string GetUICommonPrefabAddress()
            => "Assets/OneStarMaker/Scenes/UIScene.unity";

        protected override string GetSceneResourceMapAddress()
            => "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";

        protected override ILoadingDisplay CreateLoadingDisplay()
            => new NullLoadingDisplay();

        protected override string GetFirstSceneIdentify()
            => "Title";

        protected override string GetConfigFilePath()
            => "Assets/SampleGame/Config/app-config.json";

        protected override string GetEnvironmentVariablePrefix()
            => "SAMPLEGAME_";
    }
}
