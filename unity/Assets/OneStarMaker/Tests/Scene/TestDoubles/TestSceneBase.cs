#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.SceneSystem;

namespace OneStarMaker.Tests.SceneSystem.TestDoubles
{
    /// <summary>
    /// SceneBase のテスト用サブクラス。
    /// ライフサイクルフックの呼び出しを記録し、カスタム動作を注入可能。
    /// </summary>
    public class TestSceneBase : SceneBase
    {
        public bool PreLoadCalled { get; private set; }
        public bool LoadedCalled { get; private set; }
        public bool PreUnLoadCalled { get; private set; }
        public bool AfterUnLoadCalled { get; private set; }

        /// <summary>PreLoad 中に実行するカスタムアクション。キャンセルテスト用。</summary>
        public Func<CancellationToken, UniTask>? PreLoadAction { get; set; }

        /// <summary>OnLoaded 中に実行するカスタムアクション。</summary>
        public Func<CancellationToken, UniTask>? LoadedAction { get; set; }

        public TestSceneBase(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController) : base(sceneResource, sceneQuery, sceneController) { }

        protected override async UniTask OnPreLoadedImpl(CancellationToken ct)
        {
            PreLoadCalled = true;
            if (PreLoadAction != null)
            {
                await PreLoadAction(ct);
            }
        }

        protected override async UniTask OnLoadedImpl(CancellationToken ct)
        {
            LoadedCalled = true;
            if (LoadedAction != null)
            {
                await LoadedAction(ct);
            }
        }

        protected override UniTask OnPreUnLoadedImpl()
        {
            PreUnLoadCalled = true;
            return UniTask.CompletedTask;
        }

        protected override UniTask OnAfterUnLoadedImpl()
        {
            AfterUnLoadCalled = true;
            return UniTask.CompletedTask;
        }
    }
}
