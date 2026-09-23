#nullable enable

using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Tests.Editor.WorldAuthoring
{
    /// <summary>
    /// Cell Lighting は局所 fill だけを持つ。Directional が居ると季節太陽と二重に焼ける。
    /// </summary>
    [TestFixture]
    public sealed class CellLightingSceneTests
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/SampleGame/InGame/InGameSession/Seasons/Spring/Cells/Spring_Cell_4_2/Spring_Lighting_4_2/Spring_Lighting_4_2.unity",
            "Assets/SampleGame/InGame/InGameSession/Seasons/Spring/Cells/Spring_Cell_5_2/Spring_Lighting_5_2/Spring_Lighting_5_2.unity",
        };

        [Test]
        public void RepresentativeCellLightingScenes_HaveNoDirectional_AndAtLeastOnePoint()
        {
            var original = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var path in ScenePaths)
                {
                    var scene = SceneManager.GetSceneByPath(path);
                    var openedHere = false;
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        openedHere = true;
                    }

                    try
                    {
                        Assert.That(scene.IsValid() && scene.isLoaded, Is.True, path);

                        var directional = 0;
                        var point = 0;
                        foreach (var root in scene.GetRootGameObjects())
                        {
                            if (root == null)
                            {
                                continue;
                            }

                            var lights = root.GetComponentsInChildren<Light>(true);
                            foreach (var light in lights)
                            {
                                if (light == null)
                                {
                                    continue;
                                }

                                if (light.type == LightType.Directional)
                                {
                                    directional++;
                                }

                                if (light.type == LightType.Point)
                                {
                                    point++;
                                }
                            }
                        }

                        Assert.That(directional, Is.EqualTo(0), path);
                        Assert.That(point, Is.GreaterThanOrEqualTo(1), path);
                    }
                    finally
                    {
                        if (openedHere && scene.IsValid() && scene.isLoaded)
                        {
                            Assert.That(EditorSceneManager.CloseScene(scene, removeScene: true), Is.True, path);
                        }
                    }
                }
            }
            finally
            {
                if (original.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(original);
                }
            }
        }
    }
}
