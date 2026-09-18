#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.BuildContent;
using UnityEditor;
using Unity.Loading;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    public sealed class ContentDirectorySessionTests
    {
        private const string ScenePath = "Assets/OneStarMaker/Scenes/UISystem/UIScene.unity";

        [Test]
        public async Task CancelledSceneLoad_DrainsNativeCompletionBeforeUnregister()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var root = CreateRoot();
            var native = new FakeNativeDirectory(root);
            try
            {
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native);
                using var cts = new CancellationTokenSource();
                var load = session.LoadSceneAsync("scene", "Full", default, cts.Token).AsTask();
                cts.Cancel();
                Assert.ThrowsAsync<OperationCanceledException>(async () => await load);

                var close = session.CloseAsync().AsTask();
                Assert.That(close.IsCompleted, Is.False, "caller の取消は native terminal ではない");
                Assert.That(native.UnregisterCount, Is.Zero);

                native.SceneResult.TrySetResult(new FakeScene());
                await close;
                Assert.That(native.UnloadCount, Is.EqualTo(1));
                Assert.That(native.UnregisterCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                Directory.Delete(path);
            }
        }

        [Test]
        public void InvalidRoot_RollsBackAndAllowsSamePathRetry()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var wrong = CreateRoot("wrong");
            var correct = CreateRoot();
            try
            {
                var first = new FakeNativeDirectory(wrong);
                var failure = Assert.Throws<ContentDirectoryException>(() =>
                    ContentDirectorySession.Register(path, "build", "StandaloneWindows64", first));
                Assert.That(failure!.Code, Is.EqualTo(ContentDirectoryFailureCode.IdentityMismatch));
                Assert.That(first.UnregisterCount, Is.EqualTo(1));

                var next = new FakeNativeDirectory(correct);
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", next);
                session.CloseAsync().GetAwaiter().GetResult();
                Assert.That(next.UnregisterCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wrong);
                UnityEngine.Object.DestroyImmediate(correct);
                Directory.Delete(path);
            }
        }

        private static BuildContentRoot CreateRoot(string identity = "build")
        {
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize(identity, "StandaloneWindows64", new[] {
                new BuildContentEntry("scene", "stable", "Full", LoadableSceneIdEditorUtility.CreateLoadableSceneId(ScenePath))
            });
            return root;
        }

        private sealed class FakeNativeDirectory : IContentNativeDirectory
        {
            private readonly BuildContentRoot _root;
            internal FakeNativeDirectory(BuildContentRoot root) => _root = root;
            internal readonly UniTaskCompletionSource<IBackendScene> SceneResult = new();
            internal int UnloadCount;
            internal int UnregisterCount;
            public bool Register(string path) => true;
            public BuildContentRoot[] GetRoots() => new[] { _root };
            public void Unregister() => UnregisterCount++;
            public UniTask<IBackendScene> LoadSceneAsync(BuildContentEntry entry, SceneLoadOptions options) => SceneResult.Task;
            public UniTask UnloadSceneAsync(IBackendScene scene) { UnloadCount++; return UniTask.CompletedTask; }
            public UniTask<IBackendAsset> LoadObjectAsync<T>(BuildContentEntry entry) where T : UnityEngine.Object => throw new NotSupportedException();
            public UniTask<IBackendInstance> InstantiateAsync(BuildContentEntry entry, Transform? parent, bool worldSpace) => throw new NotSupportedException();
            public void Release(IBackendAsset asset) => throw new NotSupportedException();
        }

        private sealed class FakeScene : IBackendScene
        {
            public bool IsLoaded => true;
            public string Name => "fake";
            public GameObject[] GetRootGameObjects() => Array.Empty<GameObject>();
        }
    }
}
