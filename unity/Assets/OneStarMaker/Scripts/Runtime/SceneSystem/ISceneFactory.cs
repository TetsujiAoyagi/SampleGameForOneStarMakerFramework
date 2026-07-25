#nullable enable

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// SceneResource から具体的な SceneBase サブクラスを生成するファクトリ。
    /// Game 層で実装する。
    /// </summary>
    public interface ISceneFactory
    {
        /// <summary>
        /// 指定された SceneResource に対応する SceneBase を生成する。
        /// </summary>
        /// <param name="sceneResource">シーン定義情報。</param>
        /// <param name="sceneQuery">ロード済みシーンへの読み取り専用アクセス。SceneBase に渡す。</param>
        /// <param name="sceneController">シーン読み込みコントロール読み取り専用アクセス。SceneBaseに渡す。</param>
        /// <returns>生成された SceneBase。対応するシーンがなければ null。</returns>
        SceneBase? CreateSceneClass(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController);
    }
}
