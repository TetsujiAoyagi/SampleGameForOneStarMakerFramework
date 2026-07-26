#nullable enable
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.Common.TransitionArgs;
using System.Threading;
using UnityEngine;

namespace SampleGame.Common
{
    public static class SceneFlow
    {
        public static async UniTask EnterInGame(ISceneController sceneController, SceneIds toInGameScene, CancellationToken ct)
        {
            var toSceneContext = new SceneContext();
            toSceneContext.Set(new InGameArgs(toInGameScene));

            await sceneController.SwitchScene(
                            fromSceneIdentify: SceneIds.OutGameScene.idToName(),
                            toSceneIdentify: SceneIds.InGameSession.idToName(),
                            context: toSceneContext,
                            ct: ct);

            // 初期 Level は InGameArgs と一致させる（Spring 固定だと Session/Player が別 Level 待ちで固まる）。
            // Session.OnLoaded でも EnsureLevelLoaded するが、ここは明示ロード + 黒画面表示用。
            await sceneController.AddScene(
                        toInGameScene.idToName(),
                        afterOnLoadedTask: null,
                        ct,
                        context: null,
                        loadingDisplay: LoadingDisplayType.BlackScreen);
        }

        public static async UniTask EnterInGame<T>(ISceneController sceneController, SceneIds toInGameScene, T sceneContext, CancellationToken ct)
        {
            var toSceneContext = new SceneContext();
            toSceneContext.Set(new InGameArgs(toInGameScene));
            toSceneContext.Set(sceneContext);

            await sceneController.SwitchScene(
                            fromSceneIdentify: SceneIds.OutGameScene.idToName(),
                            toSceneIdentify: SceneIds.InGameSession.idToName(),
                            context: toSceneContext,
                            ct: ct);

            await sceneController.AddScene(
                        toInGameScene.idToName(),
                        afterOnLoadedTask: null,
                        ct,
                        context: null,
                        loadingDisplay: LoadingDisplayType.BlackScreen);
        }

        public static async UniTask EnterOutGame(ISceneController sceneController, SceneIds toOutGameScene, SceneContext? sceneContext, CancellationToken ct)
        {
            await sceneController.SwitchScene(
                            fromSceneIdentify: SceneIds.InGame.idToName(),
                            toSceneIdentify: toOutGameScene.idToName(),
                            context: sceneContext,
                            ct: ct);
        }
    }
}
