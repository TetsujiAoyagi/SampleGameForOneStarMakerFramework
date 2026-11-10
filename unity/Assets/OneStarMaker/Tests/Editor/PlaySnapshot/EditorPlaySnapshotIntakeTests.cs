#nullable enable

using System.Linq;
using NUnit.Framework;
using OneStarMaker.Runtime;

namespace OneStarMaker.Tests.Editor.PlaySnapshot
{
    /// <summary>取り込みの照合と、途中の失敗で unload する対象の選び方（Unity の Scene を使わない純粋な判定）。</summary>
    public sealed class EditorPlaySnapshotIntakeTests
    {
        private const string TitleCopy = "Assets/OsmPlaySnapshot/1_2_ab/Title.unity";
        private const string ExtraCopy = "Assets/OsmPlaySnapshot/1_2_ab/Extra.unity";
        private const string StartScene = "Assets/OsmPlaySnapshot/1_2_ab/OsmPlaySnapshotStart.unity";

        private static readonly EditorPlaySnapshotScene[] TwoScenes =
        {
            new("Title", TitleCopy, isActive: true),
            new("Extra", ExtraCopy, isActive: false),
        };

        private static EditorPlaySnapshotLoadedScene Loaded(string name, string path) => new(name, path);

        [Test]
        public void AllDeclaredCopiesLoadedOnce_Passes()
        {
            var loaded = new[] { Loaded("OsmPlaySnapshotStart", StartScene), Loaded("Title", TitleCopy), Loaded("Extra", ExtraCopy), Loaded("UIScene", "Assets/UI/UIScene.unity") };
            Assert.That(EditorPlaySnapshotIntake.Verify(TwoScenes, loaded), Is.Null);
        }

        [Test]
        public void OneDeclaredCopyMissing_Fails()
        {
            var loaded = new[] { Loaded("Title", TitleCopy) };
            Assert.That(EditorPlaySnapshotIntake.Verify(TwoScenes, loaded), Does.Contain("Extra"));
        }

        [Test]
        public void SameNameFromAnotherPath_IsNotTheCopy_Fails()
        {
            var loaded = new[] { Loaded("Title", "Assets/SampleGame/Scenes/Title.unity"), Loaded("Extra", ExtraCopy) };
            Assert.That(EditorPlaySnapshotIntake.Verify(TwoScenes, loaded), Does.Contain("Title"));
        }

        [Test]
        public void DuplicateIdentityInDeclaration_Fails()
        {
            var declaration = new[] { new EditorPlaySnapshotScene("Title", TitleCopy, true), new EditorPlaySnapshotScene("Title", ExtraCopy, false) };
            var loaded = new[] { Loaded("Title", TitleCopy), Loaded("Title", ExtraCopy) };
            Assert.That(EditorPlaySnapshotIntake.Verify(declaration, loaded), Does.Contain("more than once"));
        }

        [Test]
        public void UndeclaredSameNameAlreadyLoaded_Fails()
        {
            var loaded = new[] { Loaded("Title", TitleCopy), Loaded("Title", "Assets/SampleGame/Scenes/Title.unity"), Loaded("Extra", ExtraCopy) };
            Assert.That(EditorPlaySnapshotIntake.Verify(TwoScenes, loaded), Does.Contain("run twice"));
        }

        [Test]
        public void UnloadTargets_AreOnlyLoadedDeclaredCopies()
        {
            var loaded = new[]
            {
                Loaded("OsmPlaySnapshotStart", StartScene),
                Loaded("Title", TitleCopy),
                Loaded("UIScene", "Assets/UI/UIScene.unity"),
                Loaded("Other", "Assets/Other.unity"),
            };
            var targets = EditorPlaySnapshotIntake.SelectUnloadTargets(TwoScenes, loaded);
            Assert.That(targets.Select(x => x.Path), Is.EqualTo(new[] { TitleCopy }),
                "開始 Scene、UI の Scene、宣言に無い Scene は対象にしない。まだ load していない複製も含まない");
        }

        [Test]
        public void LoadedLog_CarriesTheSceneName()
        {
            Assert.That(EditorPlaySnapshotIntake.FormatLoadedLog(TwoScenes[0]),
                Is.EqualTo(EditorPlaySnapshotIntake.LogPrefix + "scene=Title copy=" + TitleCopy + " active=true"));
        }
    }
}
