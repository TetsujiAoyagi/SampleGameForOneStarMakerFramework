#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.SceneSystem;

namespace OneStarMaker.Tests.SceneSystem.TestDoubles
{
    /// <summary>
    /// ISceneFactory のテスト用実装。
    /// 各 SceneResource に対して TestSceneBase を生成し、参照を保持する。
    /// </summary>
    public class FakeSceneFactory : ISceneFactory
    {
        private readonly Dictionary<string, TestSceneBase> _instances = new();

        /// <summary>生成前にカスタム初期化を行うためのコールバック。</summary>
        public Action<TestSceneBase>? OnCreated { get; set; }

        public SceneBase? CreateSceneClass(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController)
        {
            var scene = new TestSceneBase(sceneResource, sceneQuery, sceneController);
            _instances[sceneResource.Identity] = scene;
            OnCreated?.Invoke(scene);
            return scene;
        }

        /// <summary>生成済みの TestSceneBase を取得する。</summary>
        public TestSceneBase GetCreated(string identity)
        {
            return _instances[identity];
        }

        /// <summary>指定 identity の SceneBase が生成されたか。</summary>
        public bool WasCreated(string identity) => _instances.ContainsKey(identity);
    }
}
