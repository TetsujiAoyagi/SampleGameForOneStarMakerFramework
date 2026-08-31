#nullable enable

using NUnit.Framework;
using SampleGame.DependOnAll.Editor.Streaming.Cells.Generation;
using SampleGame.InGame.Streaming;
using UnityEditor;

namespace OneStarMaker.Tests.Editor
{
    [TestFixture]
    public sealed class WorldGridDefinitionLoadTests
    {
        private const string AssetPath =
            "Assets/SampleGame/InGame/InGameSession/World/WorldGridDefinition.asset";

        [Test]
        public void ProductionAsset_LoadsDirectlyWithCatalogValues()
        {
            var definition = AssetDatabase.LoadAssetAtPath<WorldGridDefinition>(AssetPath);

            if (definition == null)
            {
                throw new AssertionException($"WorldGridDefinition を直接ロードできません: {AssetPath}");
            }

            Assert.That(definition.Origin, Is.EqualTo(WorldCellCatalog.Origin));
            Assert.That(definition.CellSize, Is.EqualTo(WorldCellCatalog.CellSize));
            Assert.That(definition.ParentSceneIdentity, Is.EqualTo(WorldCellCatalog.WorldIdentity));
            Assert.That(definition.SceneOutputFolder,
                Is.EqualTo("Assets/SampleGame/InGame/InGameSession/World/Cells"));
            Assert.That(definition.SceneResourceOutputFolder,
                Is.EqualTo("Assets/SampleGame/InGame/InGameSession/World/Cells"));

            Assert.That(definition.Rectangles.Count, Is.EqualTo(WorldCellCatalog.Rectangles.Length));
            for (var i = 0; i < definition.Rectangles.Count; i++)
            {
                Assert.That(definition.Rectangles[i].Origin, Is.EqualTo(WorldCellCatalog.Rectangles[i].Origin));
                Assert.That(definition.Rectangles[i].Size, Is.EqualTo(WorldCellCatalog.Rectangles[i].Size));
            }
        }
    }
}
