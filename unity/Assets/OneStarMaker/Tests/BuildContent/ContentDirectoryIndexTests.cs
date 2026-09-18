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
            Assert.That(ContentDirectoryIndex.TryCreate(root, "build", "StandaloneWindows64", out var index, out _), Is.True);
            Assert.That(index!.TryResolveScene("scene", "Missing", out var scene, out _), Is.True);
            Assert.That(scene!.StableKey, Is.EqualTo("full"));
            Assert.That(index.TryResolveObject("scene", "Missing", out _, out var issue), Is.False);
            Assert.That(issue.Code, Is.EqualTo(ContentDirectoryFailureCode.EntryMissing));
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

            Assert.That(ContentDirectoryIndex.TryCreate(root, "build", "StandaloneWindows64", out _, out var issue), Is.False);
            Assert.That(issue.Code, Is.EqualTo(ContentDirectoryFailureCode.EntryAmbiguous));
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void UntaggedLegacyEntry_IsIndexedAsFull()
        {
            var sceneId = LoadableSceneIdEditorUtility.CreateLoadableSceneId(ScenePath);
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize("build", "StandaloneWindows64", new[] {
                new BuildContentEntry("scene", "stable", "", sceneId)
            });

            Assert.That(ContentDirectoryIndex.TryCreate(root, "build", "StandaloneWindows64", out var index, out _), Is.True);
            Assert.That(index!.TryResolveScene("scene", "Full", out var scene, out _), Is.True);
            Assert.That(scene!.StableKey, Is.EqualTo("stable"));
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void SeparatorInLogicalFields_DoesNotMergeDistinctEntries()
        {
            var sceneId = LoadableSceneIdEditorUtility.CreateLoadableSceneId(ScenePath);
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize("build", "StandaloneWindows64", new[] {
                new BuildContentEntry("a\u001fb", "one", "c", sceneId),
                new BuildContentEntry("a", "two", "b\u001fc", sceneId),
            });
            try
            {
                Assert.That(ContentDirectoryIndex.TryCreate(root, "build", "StandaloneWindows64", out var index, out _), Is.True);
                Assert.That(index!.TryResolveScene("a\u001fb", "c", out var first, out _), Is.True);
                Assert.That(index.TryResolveScene("a", "b\u001fc", out var second, out _), Is.True);
                Assert.That(first!.StableKey, Is.EqualTo("one"));
                Assert.That(second!.StableKey, Is.EqualTo("two"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
