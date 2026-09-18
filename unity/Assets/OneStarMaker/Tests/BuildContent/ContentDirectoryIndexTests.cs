#nullable enable

using System;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent;
using Unity.Loading;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Tests.BuildContent
{
    public sealed class ContentDirectoryIndexTests
    {
        private const string ScenePath = "Assets/OneStarMaker/Scenes/UISystem/UIScene.unity";

        [Test]
        public void SceneResolution_FallsBackOnlyToFull()
        {
            var sceneId = LoadableSceneIdEditorUtility.CreateLoadableSceneId(ScenePath);
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize("build", "StandaloneWindows64", new[] {
                new BuildContentEntry("scene", "full", "Full", sceneId),
                new BuildContentEntry("scene", "whitebox", "Whitebox", sceneId),
            });
            var index = ContentDirectoryIndex.Create(root, "build", "StandaloneWindows64");

            Assert.That(index.ResolveScene("scene", "Missing").StableKey, Is.EqualTo("full"));
            Assert.Throws<ContentDirectoryException>(() => index.ResolveObject("scene", "Missing"));
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void DuplicateLogicalRepresentationKind_IsRejected()
        {
            var sceneId = LoadableSceneIdEditorUtility.CreateLoadableSceneId(ScenePath);
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize("build", "StandaloneWindows64", new[] {
                new BuildContentEntry("scene", "one", "Full", sceneId),
                new BuildContentEntry("scene", "two", "Full", sceneId),
            });

            var exception = Assert.Throws<ContentDirectoryException>(() => ContentDirectoryIndex.Create(root, "build", "StandaloneWindows64"));
            Assert.That(exception!.Code, Is.EqualTo(ContentDirectoryFailureCode.EntryAmbiguous));
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
