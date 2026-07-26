#nullable enable
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.SceneSystem;
using System.Threading;

namespace SampleGame.Common
{
    public static class SceneFlow
    {
        /// <summary>
        /// OutGame → InGameSession へ切り替える。
        /// 初期セルは Switch 後に WorldStreamingController が Focus から載せるため、
        /// ここでは Level/Cell の明示 AddScene を行わない。
        /// </summary>
        public static async UniTask EnterInGame(ISceneController sceneController, CancellationToken ct)
        {
            await sceneController.SwitchScene(
                            fromSceneIdentify: SceneIds.OutGameScene.idToName(),
                            toSceneIdentify: SceneIds.InGameSession.idToName(),
                            context: null,
                            ct: ct);
        }

        /// <summary>
        /// 追加コンテキスト付きで InGameSession へ入る。
        /// セル指定は行わず、コンテキストは Session / 子が必要なら自行で読む。
        /// </summary>
        public static async UniTask EnterInGame<T>(ISceneController sceneController, T sceneContext, CancellationToken ct)
        {
            var toSceneContext = new SceneContext();
            toSceneContext.Set(sceneContext);

            await sceneController.SwitchScene(
                            fromSceneIdentify: SceneIds.OutGameScene.idToName(),
                            toSceneIdentify: SceneIds.InGameSession.idToName(),
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
