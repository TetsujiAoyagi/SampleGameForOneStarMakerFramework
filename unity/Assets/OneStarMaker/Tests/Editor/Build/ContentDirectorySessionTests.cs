#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Cache;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.BuildContent;
using UnityEditor;
using Unity.Loading;
using UnityEngine;
using UnityEngine.SceneManagement;

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
                try { await load; Assert.Fail("取消した caller の待機が成功した"); }
                catch (OperationCanceledException) { }

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
        public void MissingDirectoryAndMissingRoot_FailBeforeActiveRegistration()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            var root = CreateRoot();
            var native = new FakeNativeDirectory(root);
            try
            {
                var missing = Assert.Throws<ContentDirectoryException>(() =>
                    ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native));
                Assert.That(missing!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidConfiguration));
                Assert.That(native.UnregisterCount, Is.Zero);

                var malformed = Assert.Throws<ContentDirectoryException>(() =>
                    ContentDirectorySession.Register(path + "\0", "build", "StandaloneWindows64", native));
                Assert.That(malformed!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidConfiguration));
                Assert.That(ContentRevisionGate.TryAcquireDelete("build", "StandaloneWindows64", path + "\0",
                    out var malformedLease, out var rejection), Is.False);
                Assert.That(malformedLease, Is.Null);
                Assert.That(rejection, Is.EqualTo(ContentDirectoryFailureCode.InvalidConfiguration));

                Directory.CreateDirectory(path);
                native.RootCount = 0;
                var noRoot = Assert.Throws<ContentDirectoryException>(() =>
                    ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native));
                Assert.That(noRoot!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidRoot));
                Assert.That(native.UnregisterCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (Directory.Exists(path)) Directory.Delete(path);
            }
        }

        [Test]
        public async Task OwnerReleaseFailure_IsRetainedAndCloseCanRetry()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var asset = AssetDatabase.LoadMainAssetAtPath("Assets/TutorialInfo/Icons/URP.png");
            Assert.That(asset, Is.Not.Null);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId), Is.True);
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize("build", "StandaloneWindows64", new[] {
                new BuildContentEntry("object", "stable", "Full",
                    new Loadable<UnityEngine.Object>(LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset)), guid, localId)
            });
            var native = new FakeNativeDirectory(root) { FailReleaseCount = 2 };
            var texture = new Texture2D(1, 1);
            native.ObjectResult = new FakeAsset(texture);
            try
            {
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native);
                var token = await session.LoadAssetAsync<Texture2D>("object", "Full", CancellationToken.None);
                session.Release(token);
                var failed = Assert.ThrowsAsync<ContentDirectoryException>(async () => await session.CloseAsync());
                Assert.That(failed!.Code, Is.EqualTo(ContentDirectoryFailureCode.OperationFailed));
                Assert.That(native.UnregisterCount, Is.Zero);
                await session.CloseAsync();
                Assert.That(native.ReleaseCount, Is.EqualTo(3));
                Assert.That(native.UnregisterCount, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(root); Directory.Delete(path); }
        }

        [Test]
        public async Task SynchronousShutdown_WaitsForLiveSceneUnloadTerminal()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var root = CreateRoot();
            var native = new FakeNativeDirectory(root) { PendingUnloadCompletion = new UniTaskCompletionSource() };
            try
            {
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native);
                native.SceneResult.TrySetResult(new FakeScene());
                var scene = await session.LoadSceneAsync("scene", "Full", default, CancellationToken.None);
                session.ReleaseSceneAfterUnityShutdown(scene);
                session.BeginSynchronousShutdown();
                Assert.That(native.UnregisterCount, Is.Zero);
                Assert.That(ContentRevisionGate.TryAcquireDelete("build", "StandaloneWindows64", path,
                    out var blocked, out _), Is.False);
                Assert.That(blocked, Is.Null);

                native.PendingUnloadCompletion.TrySetResult();
                await session.CloseAsync();
                Assert.That(native.UnregisterCount, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); Directory.Delete(path); }
        }

        [Test]
        public async Task NormalUnloadAndClose_JoinOneNativeUnloadBeforeUnregister()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var root = CreateRoot();
            var native = new FakeNativeDirectory(root) { PendingUnloadCompletion = new UniTaskCompletionSource() };
            try
            {
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native);
                var assets = new AssetManagement();
                assets.InstallContentDirectory(session);
                native.SceneResult.TrySetResult(new FakeScene());
                await assets.LoadContentSceneAsync("scene", "Full");

                var normalUnload = assets.UnloadSceneAsync("scene").AsTask();
                var close = assets.CloseContentDirectoryAsync().AsTask();
                Assert.That(native.UnloadCount, Is.EqualTo(1), "両 caller は同じ native unload を待つ");
                Assert.That(normalUnload.IsCompleted, Is.False);
                Assert.That(close.IsCompleted, Is.False);
                Assert.That(native.UnregisterCount, Is.Zero, "native terminal より前に登録を返さない");

                native.PendingUnloadCompletion.TrySetResult();
                await normalUnload;
                await close;
                Assert.That(native.UnloadCount, Is.EqualTo(1));
                Assert.That(native.UnregisterCount, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); Directory.Delete(path); }
        }

        [Test]
        public async Task NullInstanceResult_ReleasesIssuedTokenBeforeFailure()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var asset = AssetDatabase.LoadMainAssetAtPath("Assets/TutorialInfo/Icons/URP.png");
            Assert.That(asset, Is.Not.Null);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId), Is.True);
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize("build", "StandaloneWindows64", new[] {
                new BuildContentEntry("object", "stable", "Full",
                    new Loadable<UnityEngine.Object>(LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset)), guid, localId)
            });
            var native = new FakeNativeDirectory(root) { InstanceResult = new FakeNullInstance() };
            try
            {
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native);
                var assets = new AssetManagement();
                assets.InstallContentDirectory(session);
                var error = Assert.ThrowsAsync<ContentDirectoryException>(async () =>
                    await assets.InstantiateContentAsync("object", "Full"));
                Assert.That(error!.Code, Is.EqualTo(ContentDirectoryFailureCode.OperationFailed));
                await assets.CloseContentDirectoryAsync();
                Assert.That(native.ReleaseCount, Is.EqualTo(1));
                Assert.That(native.UnregisterCount, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); Directory.Delete(path); }
        }

        [Test]
        public async Task DeferredSceneActivation_IsRejectedBeforeNativeOperation()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var root = CreateRoot();
            var native = new FakeNativeDirectory(root);
            try
            {
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native);
                var options = new SceneLoadOptions(LoadSceneMode.Additive, activateOnLoad: false);
                var error = Assert.ThrowsAsync<ContentDirectoryException>(async () =>
                    await session.LoadSceneAsync("scene", "Full", options, CancellationToken.None));
                Assert.That(error!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidConfiguration));
                await session.CloseAsync();
                Assert.That(native.UnloadCount, Is.Zero);
                Assert.That(native.UnregisterCount, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); Directory.Delete(path); }
        }

        [Test]
        public async Task NativeLoadFailures_AreMappedToPublicReasonCode()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var asset = AssetDatabase.LoadMainAssetAtPath("Assets/TutorialInfo/Icons/URP.png");
            Assert.That(asset, Is.Not.Null);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId), Is.True);
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize("build", "StandaloneWindows64", new[] {
                new BuildContentEntry("scene", "scene-stable", "Full", LoadableSceneIdEditorUtility.CreateLoadableSceneId(ScenePath)),
                new BuildContentEntry("object", "object-stable", "Full",
                    new Loadable<UnityEngine.Object>(LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset)), guid, localId)
            });
            var native = new FakeNativeDirectory(root) { ThrowOnLoad = true };
            try
            {
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native);
                var objectError = Assert.ThrowsAsync<ContentDirectoryException>(async () =>
                    await session.LoadAssetAsync<Texture2D>("object", "Full", CancellationToken.None));
                var sceneError = Assert.ThrowsAsync<ContentDirectoryException>(async () =>
                    await session.LoadSceneAsync("scene", "Full", default, CancellationToken.None));
                var prefabError = Assert.ThrowsAsync<ContentDirectoryException>(async () =>
                    await session.InstantiateAsync("object", "Full", null, false, CancellationToken.None));
                foreach (var error in new[] { objectError, sceneError, prefabError })
                {
                    Assert.That(error!.Code, Is.EqualTo(ContentDirectoryFailureCode.OperationFailed));
                    Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
                    Assert.That(error.BuildIdentity, Is.EqualTo("build"));
                    Assert.That(error.Representation, Is.EqualTo("Full"));
                }
                await session.CloseAsync();
                Assert.That(native.UnregisterCount, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); Directory.Delete(path); }
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

        [Test]
        public async Task LiveInstance_RejectsCloseUntilOwnerReleasesIt()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var asset = AssetDatabase.LoadMainAssetAtPath("Assets/TutorialInfo/Icons/URP.png");
            Assert.That(asset, Is.Not.Null);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId), Is.True);
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize("build", "StandaloneWindows64", new[] {
                new BuildContentEntry("object", "stable", "Full",
                    new Loadable<UnityEngine.Object>(LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset)), guid, localId)
            });
            var native = new FakeNativeDirectory(root) { InstanceResult = new FakeInstance() };
            try
            {
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native);
                var instance = await session.InstantiateAsync("object", "Full", null, false, CancellationToken.None);
                try
                {
                    await session.CloseAsync();
                    Assert.Fail("生存中の instance を持つ directory が閉じられた");
                }
                catch (ContentDirectoryException ex)
                {
                    Assert.That(ex.Code, Is.EqualTo(ContentDirectoryFailureCode.ResourcesInUse));
                }
                Assert.That(native.UnregisterCount, Is.Zero);

                session.Release((IBackendAsset)instance);
                await session.CloseAsync();
                Assert.That(native.ReleaseCount, Is.EqualTo(1));
                Assert.That(native.UnregisterCount, Is.EqualTo(1));
            }
            finally
            {
                if (native.InstanceResult?.Instance != null)
                    UnityEngine.Object.DestroyImmediate(native.InstanceResult.Instance);
                UnityEngine.Object.DestroyImmediate(root);
                Directory.Delete(path);
            }
        }

        [Test]
        public async Task UnregisterFailure_KeepsDeleteBlockedAndCloseCanRetry()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var root = CreateRoot();
            var native = new FakeNativeDirectory(root) { FailUnregisterCount = 1 };
            try
            {
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native);
                var failure = Assert.ThrowsAsync<ContentDirectoryException>(async () => await session.CloseAsync());
                Assert.That(failure!.Code, Is.EqualTo(ContentDirectoryFailureCode.OperationFailed));
                Assert.That(ContentRevisionGate.TryAcquireDelete("build", "StandaloneWindows64", path,
                    out var blocked, out var reason), Is.False);
                Assert.That(blocked, Is.Null);
                Assert.That(reason, Is.EqualTo(ContentDirectoryFailureCode.RevisionBusy));

                await session.CloseAsync();
                Assert.That(native.UnregisterCount, Is.EqualTo(2));
                Assert.That(ContentRevisionGate.TryAcquireDelete("build", "StandaloneWindows64", path,
                    out var lease, out _), Is.True);
                lease!.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                Directory.Delete(path);
            }
        }

        [Test]
        public void FailedRegistrationRollback_IsRetriedOnNextRegistration()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var wrong = CreateRoot("wrong");
            var right = CreateRoot();
            try
            {
                var first = new FakeNativeDirectory(wrong) { FailUnregisterCount = 1 };
                var failure = Assert.Throws<ContentDirectoryException>(() =>
                    ContentDirectorySession.Register(path, "build", "StandaloneWindows64", first));
                Assert.That(failure!.Code, Is.EqualTo(ContentDirectoryFailureCode.RegistrationFailed));
                Assert.That(ContentRevisionGate.TryAcquireDelete("build", "StandaloneWindows64", path,
                    out var blocked, out _), Is.False);
                Assert.That(blocked, Is.Null);

                var second = new FakeNativeDirectory(right);
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", second);
                session.CloseAsync().GetAwaiter().GetResult();
                Assert.That(first.UnregisterCount, Is.EqualTo(2));
                Assert.That(second.UnregisterCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wrong);
                UnityEngine.Object.DestroyImmediate(right);
                Directory.Delete(path);
            }
        }

        [Test]
        public async Task ConcurrentCallers_ShareOneAssetAndOneSceneToken()
        {
            var directory = new FakeDirectoryPort();
            var assets = new AssetManagement();
            assets.InstallContentDirectory(directory);
            var texture = new Texture2D(1, 1);
            try
            {
                var firstAsset = assets.LoadContentAssetAsync<Texture2D>("object", "Full", AssetOwner.Manual).AsTask();
                var secondAsset = assets.LoadContentAssetAsync<Texture2D>("object", "Full", AssetOwner.Manual).AsTask();
                Assert.That(directory.AssetLoads, Is.EqualTo(1));
                directory.AssetResult.TrySetResult(new FakeAsset(texture));
                var handleA = await firstAsset;
                var handleB = await secondAsset;
                Assert.That(handleA.Value, Is.SameAs(handleB.Value));
                assets.Release(handleA);
                assets.Release(handleB);
                Assert.That(directory.AssetReleases, Is.EqualTo(1));

                var firstScene = assets.LoadContentSceneAsync("scene", "Full").AsTask();
                var secondScene = assets.LoadContentSceneAsync("scene", "Full").AsTask();
                Assert.That(directory.SceneLoads, Is.EqualTo(1));
                directory.SceneResult.TrySetResult(new FakeScene());
                Assert.That((await firstScene).IsLoaded, Is.True);
                Assert.That((await secondScene).IsLoaded, Is.True);
                await assets.UnloadSceneAsync("scene");
                assets.ReleaseScene("scene");
                Assert.That(directory.SceneUnloads, Is.EqualTo(1));
                await assets.CloseContentDirectoryAsync();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public async Task CancelledSceneCleanupFailure_RetainsRegistrationUntilRetry()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var root = CreateRoot();
            var native = new FakeNativeDirectory(root) { FailUnloadCount = 2 };
            try
            {
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native);
                using var cts = new CancellationTokenSource();
                var load = session.LoadSceneAsync("scene", "Full", default, cts.Token).AsTask();
                cts.Cancel();
                try { await load; Assert.Fail("取消した caller の待機が成功した"); }
                catch (OperationCanceledException) { }
                native.SceneResult.TrySetResult(new FakeScene());

                var first = Assert.ThrowsAsync<ContentDirectoryException>(async () => await session.CloseAsync());
                Assert.That(first!.Code, Is.EqualTo(ContentDirectoryFailureCode.OperationFailed));
                Assert.That(native.UnregisterCount, Is.Zero);
                Assert.That(ContentRevisionGate.TryAcquireDelete("build", "StandaloneWindows64", path,
                    out var blocked, out _), Is.False);
                Assert.That(blocked, Is.Null);

                await session.CloseAsync();
                Assert.That(native.UnloadCount, Is.EqualTo(3));
                Assert.That(native.UnregisterCount, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); Directory.Delete(path); }
        }

        [Test]
        public async Task CancelledInstance_DestroysUnownedGameObjectBeforeUnregister()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var asset = AssetDatabase.LoadMainAssetAtPath("Assets/TutorialInfo/Icons/URP.png");
            Assert.That(asset, Is.Not.Null);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId), Is.True);
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize("build", "StandaloneWindows64", new[] {
                new BuildContentEntry("object", "stable", "Full",
                    new Loadable<UnityEngine.Object>(LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset)), guid, localId)
            });
            var native = new FakeNativeDirectory(root) { PendingInstanceResult = new UniTaskCompletionSource<IBackendInstance>() };
            var instance = new FakeInstance();
            try
            {
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native);
                using var cts = new CancellationTokenSource();
                var load = session.InstantiateAsync("object", "Full", null, false, cts.Token).AsTask();
                cts.Cancel();
                try { await load; Assert.Fail("取消した caller の待機が成功した"); }
                catch (OperationCanceledException) { }
                native.PendingInstanceResult.TrySetResult(instance);
                await session.CloseAsync();
                Assert.That(instance.Instance == null, Is.True);
                Assert.That(native.ReleaseCount, Is.EqualTo(1));
                Assert.That(native.UnregisterCount, Is.EqualTo(1));
            }
            finally
            {
                if (instance.Instance != null) UnityEngine.Object.DestroyImmediate(instance.Instance);
                UnityEngine.Object.DestroyImmediate(root);
                Directory.Delete(path);
            }
        }

        [Test]
        public async Task PlayStop_ReleaseAllThenComplete_SkipsUnloadAndCloses()
        {
            var directory = new FakeDirectoryPort();
            var assets = new AssetManagement();
            assets.InstallContentDirectory(directory);
            directory.SceneResult.TrySetResult(new FakeScene());
            await assets.LoadContentSceneAsync("scene", "Full");
            assets.ReleaseAll();
            assets.CompleteContentDirectoryPlayStop();
            Assert.That(directory.SceneUnloads, Is.EqualTo(0));
            Assert.That(directory.ShutdownSceneReleases, Is.EqualTo(1));
            Assert.That(directory.CloseCount, Is.EqualTo(1));
            Assert.That(directory.StopCount, Is.EqualTo(1));
            Assert.That(directory.DrainCount, Is.EqualTo(0));
        }

        [Test]
        public async Task PlayStop_StillLoadedNativeScene_UnregistersWithoutUnloadAndReleasesLease()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var root = CreateRoot();
            var native = new FakeNativeDirectory(root) { PendingUnloadCompletion = new UniTaskCompletionSource() };
            try
            {
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native);
                var assets = new AssetManagement();
                assets.InstallContentDirectory(session);
                native.SceneResult.TrySetResult(new FakeScene());
                await assets.LoadContentSceneAsync("scene", "Full");
                assets.ReleaseAll();
                assets.CompleteContentDirectoryPlayStop();
                Assert.That(native.UnloadCount, Is.EqualTo(0));
                Assert.That(native.UnregisterCount, Is.EqualTo(1));
                Assert.That(ContentRevisionGate.TryAcquireDelete("build", "StandaloneWindows64", path,
                    out var lease, out _), Is.True);
                lease!.Dispose();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); Directory.Delete(path); }
        }

        [Test]
        public async Task SceneIdentity_CannotAliasAnotherRepresentation()
        {
            var directory = new FakeDirectoryPort();
            var assets = new AssetManagement();
            assets.InstallContentDirectory(directory);
            directory.SceneResult.TrySetResult(new FakeScene());
            await assets.LoadContentSceneAsync("scene", "Full");
            var error = Assert.ThrowsAsync<ContentDirectoryException>(async () =>
                await assets.LoadContentSceneAsync("scene", "Whitebox"));
            Assert.That(error!.Code, Is.EqualTo(ContentDirectoryFailureCode.EntryAmbiguous));
            Assert.That(directory.SceneLoads, Is.EqualTo(1));
            await assets.CloseContentDirectoryAsync();
            Assert.That(directory.DrainCount, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task StoppedDirectory_RejectsRegistryHitWithoutNewNativeLoad()
        {
            var directory = new FakeDirectoryPort();
            var assets = new AssetManagement();
            assets.InstallContentDirectory(directory);
            var texture = new Texture2D(1, 1);
            try
            {
                directory.AssetResult.TrySetResult(new FakeAsset(texture));
                var first = await assets.LoadContentAssetAsync<Texture2D>("object", "Full", AssetOwner.Manual);
                await directory.StopAndDrainAsync();
                var error = Assert.ThrowsAsync<ContentDirectoryException>(async () =>
                    await assets.LoadContentAssetAsync<Texture2D>("object", "Full", AssetOwner.Manual));
                Assert.That(error!.Code, Is.EqualTo(ContentDirectoryFailureCode.DirectoryNotRegistered));
                Assert.That(directory.AssetLoads, Is.EqualTo(1));
                assets.Release(first);
                await assets.CloseContentDirectoryAsync();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public async Task ResidentCache_ReleasesDirectoryTokenAtClose()
        {
            var directory = new FakeDirectoryPort();
            var cache = new AssetResidentCache(new FixedBudget(), 300f, _ => Assert.Fail("directory token を Addressables 側へ返した"), _ => 1L, () => 0d);
            var assets = new AssetManagement(new AddressableBackend(), cache);
            assets.InstallContentDirectory(directory);
            var texture = new Texture2D(1, 1);
            try
            {
                directory.AssetResult.TrySetResult(new FakeAsset(texture));
                var handle = await assets.LoadContentAssetAsync<Texture2D>("object", "Full", AssetOwner.Manual);
                assets.Release(handle);
                Assert.That(directory.AssetReleases, Is.Zero, "resident cache も directory の利用中 token として保持する");

                await assets.CloseContentDirectoryAsync();
                Assert.That(directory.AssetReleases, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
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
            internal int ReleaseCount;
            internal int FailReleaseCount;
            internal int FailUnregisterCount;
            internal int FailUnloadCount;
            internal int RootCount = 1;
            internal bool ThrowOnLoad;
            internal IBackendInstance? InstanceResult;
            internal UniTaskCompletionSource<IBackendInstance>? PendingInstanceResult;
            internal UniTaskCompletionSource? PendingUnloadCompletion;
            internal IBackendAsset? ObjectResult;
            public bool Register(string path) => true;
            public BuildContentRoot[] GetRoots() => RootCount == 0 ? Array.Empty<BuildContentRoot>() : new[] { _root };
            public void Unregister()
            {
                UnregisterCount++;
                if (FailUnregisterCount-- > 0) throw new InvalidOperationException("fake unregister failure");
            }
            public UniTask<IBackendScene> LoadSceneAsync(BuildContentEntry entry, SceneLoadOptions options)
                => ThrowOnLoad ? throw new InvalidOperationException("fake scene load failure") : SceneResult.Task;
            public UniTask UnloadSceneAsync(IBackendScene scene)
            {
                UnloadCount++;
                if (FailUnloadCount-- > 0) throw new InvalidOperationException("fake unload failure");
                return PendingUnloadCompletion?.Task ?? UniTask.CompletedTask;
            }
            public UniTask<IBackendAsset> LoadObjectAsync<T>(BuildContentEntry entry) where T : UnityEngine.Object
                => ThrowOnLoad ? throw new InvalidOperationException("fake object load failure")
                    : ObjectResult != null ? UniTask.FromResult(ObjectResult) : throw new NotSupportedException();
            public UniTask<IBackendInstance> InstantiateAsync(BuildContentEntry entry, Transform? parent, bool worldSpace)
                => ThrowOnLoad ? throw new InvalidOperationException("fake instantiate failure")
                    : PendingInstanceResult != null ? PendingInstanceResult.Task : InstanceResult != null ? UniTask.FromResult(InstanceResult!) : throw new NotSupportedException();
            public void Release(IBackendAsset asset)
            {
                ReleaseCount++;
                if (FailReleaseCount-- > 0) throw new InvalidOperationException("fake release failure");
            }
        }

        private sealed class FakeInstance : IBackendInstance, IBackendAsset
        {
            public GameObject? Instance { get; } = new("fake-content-instance");
            public UnityEngine.Object? Asset => Instance;
            public bool IsValid => Instance != null;
        }

        private sealed class FakeNullInstance : IBackendInstance, IBackendAsset
        {
            public GameObject? Instance => null;
            public UnityEngine.Object? Asset => null;
            public bool IsValid => false;
        }

        private sealed class FakeScene : IBackendScene, IContentDirectoryToken
        {
            public bool IsLoaded => true;
            public string Name => "fake";
            public GameObject[] GetRootGameObjects() => Array.Empty<GameObject>();
        }

        private sealed class FakeAsset : IBackendAsset, IContentDirectoryToken
        {
            internal FakeAsset(Texture2D asset) => Asset = asset;
            public UnityEngine.Object? Asset { get; }
            public bool IsValid => Asset != null;
        }

        private sealed class FakeDirectoryPort : IContentDirectoryBackend
        {
            internal readonly UniTaskCompletionSource<IBackendAsset> AssetResult = new();
            internal readonly UniTaskCompletionSource<IBackendScene> SceneResult = new();
            internal int AssetLoads;
            internal int SceneLoads;
            internal int AssetReleases;
            internal int SceneUnloads;
            internal int ShutdownSceneReleases;
            internal int StopCount;
            internal int DrainCount;
            internal int CloseCount;
            private Action? _evict;
            private bool _accepting = true;
            public string BuildIdentity => "build";
            public string Target => "StandaloneWindows64";
            public string CachePrefix => "directory:test:";
            public AssetKey GetObjectKey(string logicalKey, string representation)
                => AssetKey.FromContentDirectory(CachePrefix + logicalKey + ":" + representation, AssetType.Other);
            public UniTask<IBackendAsset> LoadAssetAsync<T>(string logicalKey, string representation, CancellationToken ct) where T : UnityEngine.Object
            { AssetLoads++; return AssetResult.Task; }
            public UniTask<IBackendScene> LoadSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options, CancellationToken ct)
            { SceneLoads++; return SceneResult.Task; }
            public UniTask<IBackendInstance> InstantiateAsync(string logicalKey, string representation, Transform? parent, bool worldSpace, CancellationToken ct)
                => throw new NotSupportedException();
            public UniTask UnloadSceneAsync(IBackendScene scene) { SceneUnloads++; return UniTask.CompletedTask; }
            public void ReleaseSceneAfterUnityShutdown(IBackendScene scene) { ShutdownSceneReleases++; }
            public void ReleaseSceneTokenAfterPlayStop(IBackendScene scene) { ShutdownSceneReleases++; }
            public void Release(IBackendAsset asset) => AssetReleases++;
            public void ConfigureCacheEviction(Action evictRevisionEntries) => _evict = evictRevisionEntries;
            public UniTask StopAndDrainAsync() { _accepting = false; StopCount++; DrainCount++; return UniTask.CompletedTask; }
            public void EnsureAccepting()
            {
                if (!_accepting) throw new ContentDirectoryException(ContentDirectoryFailureCode.DirectoryNotRegistered,
                    "directory stopped");
            }
            public UniTask CloseAsync() { CloseCount++; _evict?.Invoke(); return UniTask.CompletedTask; }
            public void CompletePlayStop() { _accepting = false; StopCount++; CloseCount++; _evict?.Invoke(); }
            public void BeginSynchronousShutdown() { }
        }

        private sealed class FixedBudget : IBudgetProvider
        {
            public long GetBudgetBytes(AssetType type) => 1024;
        }
    }
}
