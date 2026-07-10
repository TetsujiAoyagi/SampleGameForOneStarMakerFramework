#nullable enable

using System;
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
        {
            // Bootstrap が構成した唯一の ILoggerFactory を Game 層へ渡す。
            // Game 層で AppLoggerFactory を再生成すると、rolling file と DebugSocket への出力経路が分断される。
            var loggerFactory = LoggerFactory
                ?? throw new InvalidOperationException(
                    "ILoggerFactory is not initialized. Ensure BeforeSceneLoad completed successfully.");
            return new GameSceneFactory(loggerFactory);
        }

        protected override string GetUICommonPrefabAddress()
            => "Assets/OneStarMaker/Scenes/UIScene.unity";

        protected override string GetSceneResourceMapAddress()
            => "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";

        protected override ILoadingDisplay CreateLoadingDisplay()
            => new NullLoadingDisplay();

        protected override string GetFirstSceneIdentify()
        {
            // ビルド/実行時に app-config.json 等で論理初回シーンを差し替え可能にする。
            // キー未設定時は従来通り "Title"。
            var overridden = Config?.GetString("assetCheckout:firstSceneIdentify", string.Empty) ?? string.Empty;
            return string.IsNullOrEmpty(overridden) ? "Title" : overridden;
        }

        protected override string GetConfigFilePath()
            => "Assets/SampleGame/Config/app-config.json";

        protected override string GetEnvironmentVariablePrefix()
            => "SAMPLEGAME_";
    }
}
