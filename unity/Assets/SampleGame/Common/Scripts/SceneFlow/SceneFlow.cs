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
        }

        public static async UniTask EnterInGame<T>(ISceneController sceneController, SceneIds toInGameScene, T sceneContext, CancellationToken ct)
        {
            var toSceneContext = new SceneContext();
            toSceneContext.Set(new InGameArgs(toInGameScene));
            toSceneContext.Set(sceneContext);

            await sceneController.SwitchScene(
                            fromSceneIdentify: SceneIds.OutGameScene.idToName(),
                            toSceneIdentify: toInGameScene.idToName(),
                            context: toSceneContext,
                            ct: ct);
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
