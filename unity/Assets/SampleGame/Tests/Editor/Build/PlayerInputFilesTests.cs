#nullable enable

using System.Linq;
using NUnit.Framework;
using SampleGame.DependOnAll.Editor.Build;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// この project で集めた Player の入力の集合（D11）に、起点に挙げた file が入っていること。build と入口が同じ口を使うので、
    /// 集め方の抜けは同じ checkout の比較（AC17）では見えない。ここで集合そのものを見る。
    /// </summary>
    public sealed class PlayerInputFilesTests
    {
        [Test]
        public void Collect_ContainsTheRenderPipelineAssetsTheStartSceneTheVersionAndThePackages()
        {
            var paths = PlayerInputFiles.Collect().Select(x => x.path).ToArray();
            Assert.That(paths, Is.Ordered.Using((System.Collections.Generic.IComparer<string>)System.StringComparer.Ordinal));
            foreach (var expected in new[]
                     {
                         "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset",
                         "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset.meta",
                         "Assets/Settings/PC_RPAsset.asset",
                         "Assets/Settings/PC_RPAsset.asset.meta",
                         "Assets/Settings/Mobile_RPAsset.asset",
                         "Assets/Settings/Mobile_RPAsset.asset.meta",
                         "Assets/SampleGame/DependOnAll/PlayerStart/PlayablePlayerStart.unity",
                         "Assets/SampleGame/DependOnAll/PlayerStart/PlayablePlayerStart.unity.meta",
                         "ProjectSettings/ProjectVersion.txt",
                         "ProjectSettings/TagManager.asset",
                         "Packages/manifest.json",
                         "Packages/packages-lock.json",
                     })
                Assert.That(paths, Does.Contain(expected));
            Assert.That(paths, Does.Not.Contain("ProjectSettings/EditorBuildSettings.asset"));
            Assert.That(paths.Where(x => x.StartsWith("Assets/Resources/BS4", System.StringComparison.Ordinal)), Is.Empty);
            Assert.That(paths.Where(x => x.StartsWith("Library/", System.StringComparison.Ordinal)), Is.Empty);
            Assert.That(PlayerInputFiles.Collect().All(x => x.sha256.Length == 64), Is.True);
        }
    }
}
