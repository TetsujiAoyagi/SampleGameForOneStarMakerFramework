#nullable enable

namespace SampleGame.DependOnAll
{
    /// <summary>
    /// content directory から起動する時（Editor の directory Play と遊べる Player）に読む bootstrap の logical key と、
    /// 遊べる Player の開始 Scene の名前。build（content composer と Player の build）と Runtime が同じ値を使う。
    /// </summary>
    public static class DirectoryBootstrapKeys
    {
        public const string UiCommonScene = "samplegame:bootstrap:uicommon";
        public const string SceneResourceMap = "samplegame:bootstrap:scene-resource-map";
        public const string Representation = "Full";
        /// <summary>遊べる Player の開始 Scene（commit した空の Scene）の名前。Player の構成の latch の marker でもある。</summary>
        public const string PlayableStartSceneName = "PlayablePlayerStart";
    }
}
