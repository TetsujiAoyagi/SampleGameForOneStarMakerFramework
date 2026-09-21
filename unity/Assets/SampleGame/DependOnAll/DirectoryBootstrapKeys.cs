#nullable enable

namespace SampleGame.DependOnAll
{
    /// <summary>Editor directory Play が Content Directory から読む bootstrap の logical key。build composer と同じ値。</summary>
    public static class DirectoryBootstrapKeys
    {
        public const string UiCommonScene = "samplegame:bootstrap:uicommon";
        public const string SceneResourceMap = "samplegame:bootstrap:scene-resource-map";
        public const string Representation = "Full";
    }
}
