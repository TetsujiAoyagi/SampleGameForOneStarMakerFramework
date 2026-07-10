#nullable enable

using Cysharp.Threading.Tasks;
using System.Threading;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;

namespace SampleGame.OutGame.Title
{
    /// <summary>
    /// タイトル画面シーン。最小実装。
    /// </summary>
    public sealed class TitleScene : SceneBase
    {
        public TitleScene(SceneResource sceneResource, ISceneQuery sceneQuery)
            : base(sceneResource, sceneQuery)
        {
        }

        protected override void OnInitialize()
        {
            Debug.Log("[TitleScene] Initialized.");
        }

        protected override async UniTask OnLoadedImpl(CancellationToken ct)
        {
            Debug.Log("[TitleScene] Loaded.");
            await UniTask.CompletedTask;
        }
    }
}
