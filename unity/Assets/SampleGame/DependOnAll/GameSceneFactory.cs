#nullable enable

using OneStarMaker.Runtime.SceneSystem;
using SampleGame.OutGame.Scenes;

namespace SampleGame.DependOnAll
{
    /// <summary>
    /// SceneResource の Identity から具体的な SceneBase を生成するファクトリ。
    /// DependOnAll に配置し、全 Game 層の SceneBase を知る唯一のクラス。
    /// </summary>
    public sealed class GameSceneFactory : ISceneFactory
    {
        public SceneBase? CreateSceneClass(SceneResource sceneResource, ISceneQuery sceneQuery)
        {
            return sceneResource.Identity switch
            {
                "Title" => new TitleScene(sceneResource, sceneQuery),
                _ => null,
            };
        }
    }
}
