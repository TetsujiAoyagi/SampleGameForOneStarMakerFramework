#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.BuildContent.Distribution;
using OneStarMaker.Runtime.ContentResolution;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// 実機の Content Directory を要求時解決の供給元にする。合成の目的 1 つ（Scene Entry の Full と Whitebox、Scene Next の Full だけ、
    /// Representation の tag の無い Object Prop）を製品の coordinator で Full build して publish し、Local の供給元から temp の cache に install して
    /// 検証・登録する。loader は起動の配線と同じ関数（<see cref="RegisteredDirectoryRoute.TryCreate"/>）で作り、Play mode で読む。
    /// 記録の identity が install した manifest と digest に等しいこと、明示した fallback でだけ Full へ代わること、
    /// 解放の後に directory を close できること、拒んだ要求で何も載らないことを見る。SampleGame の型は使わない
    /// （build の道具がこの assembly にあるので置いている）。
    /// </summary>
    public sealed class RegisteredDirectoryRouteIntegrationTests
    {
        private const string FixtureParent = "Assets/OneStarMakerGenerated/RegisteredRouteTests";
        private const string ContentSet = "s31-route";
        private string _folder = "";
        private string _deliveryRoot = "";
        private readonly List<string> _contentPaths = new List<string>();

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            if (_folder.Length != 0) AssetDatabase.DeleteAsset(_folder);
            if (_deliveryRoot.Length != 0 && Directory.Exists(_deliveryRoot)) Directory.Delete(_deliveryRoot, true);
            foreach (var path in _contentPaths.Where(Directory.Exists)) Directory.Delete(path, true);
            _contentPaths.Clear();
            DeleteIfEmpty(FixtureParent);
            DeleteIfEmpty("Assets/OneStarMakerGenerated");
        }

        [UnityTest]
        // Content build を 1 回と PlayMode の往復を 1 回行う。既存の build を伴う test と同じ上限にする。
        [Timeout(360000)]
        public IEnumerator InstalledDirectory_ServesScenesAndObjects_WithMatchingRecords_AndClosesAfterRelease()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                Assert.Ignore("Registered route fixture requires StandaloneWindows64 Player target.");
            const string modeEnv = "SAMPLEGAME_CONTENT__RUNTIMEMODE";
            var previousMode = Environment.GetEnvironmentVariable(modeEnv, EnvironmentVariableTarget.Process);
            EnsureFolder(FixtureParent);
            _folder = FixtureParent + "/" + Guid.NewGuid().ToString("N");
            EnsureFolder(_folder);
            _deliveryRoot = Path.Combine(Path.GetTempPath(), "osm-s31-route", Guid.NewGuid().ToString("N"));
            BuildContentResult built;
            string published;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var entry = CreateScene("Entry");
                var entryWhitebox = CreateScene("EntryWhitebox");
                var next = CreateScene("Next");
                var prefabPath = _folder + "/Prop.prefab";
                var go = new GameObject("Prop");
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                UnityEngine.Object.DestroyImmediate(go);
                AssetDatabase.SaveAssets();

                var artifacts = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "bs3-fixture"));
                built = new BuildContentCoordinator().Build(Request(ContentSet, artifacts,
                    ("s31-entry", "Entry", entry, "Full"), ("s31-entry-wb", "Entry", entryWhitebox, "Whitebox"),
                    ("s31-next", "Next", next, "Full"), ("s31-prop", "Prop", prefabPath, null)));
                Assert.That(built.IsSuccess, Is.True, built.Summary);
                _contentPaths.Add(built.ContentPath!);
                published = ContentTransportPublisher.Publish(built, Path.Combine(_deliveryRoot, "published"));
            }
            finally
            {
                // 登録の検証が source asset に依存しないよう、Play の前に fixture を消す。
                if (previous.Any(x => x.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (_folder.Length != 0) { AssetDatabase.DeleteAsset(_folder); _folder = ""; }
            }

            var manifestBytes = File.ReadAllBytes(Path.Combine(published, "transport.json"));
            var digest = ContentDeliveryFiles.Sha256(manifestBytes);
            var install = InstallFromLocalAsync(published, digest, built.Identity);
            yield return new WaitUntil(() => install.IsCompleted);
            var installed = install.GetAwaiter().GetResult();
            var expected = new ContentIdentity(installed.ContentSet, installed.Revision, installed.Target, digest);
            Assert.That(expected, Is.EqualTo(new ContentIdentity(ContentSet, built.Identity, BuildContentCoordinator.TargetName, digest)));

            // アプリの起動は Addressables の経路にし、この test が登録する directory と取り合わない。
            Environment.SetEnvironmentVariable(modeEnv, "addressables", EnvironmentVariableTarget.Process);
            try
            {
                yield return new EnterPlayMode();
                var run = ExerciseAsync(installed, digest, expected);
                yield return new WaitUntil(() => run.IsCompleted);
                run.GetAwaiter().GetResult();
                yield return new ExitPlayMode();
            }
            finally
            {
                Environment.SetEnvironmentVariable(modeEnv, previousMode, EnvironmentVariableTarget.Process);
            }
        }

        private static async Task ExerciseAsync(ContentInstallResult installed, string digest, ContentIdentity expected)
        {
            var verified = InstalledRevisionVerifier.Verify(installed.RevisionRoot, digest, installed.ContentSet, installed.Revision, installed.Target);
            var session = ContentDirectorySession.RegisterVerified(verified, new UnityContentDirectoryBackend());
            var assets = new OneStarMaker.Runtime.AssetManagement.AssetManagement();
            assets.InstallContentDirectory(session);
            var closed = false;
            try
            {
                Assert.That(RegisteredDirectoryRoute.TryCreate(session, assets, out var route, out var reason), Is.True, reason);
                var loader = route!.Loader;

                // 失敗経路（何も載せない）を先に見る。
                var sceneCount = SceneManager.sceneCount;
                var noFallback = await Capture(() => loader.LoadSceneAsync(new ContentRequest("Next", ContentKind.Scene, "Whitebox")).AsTask());
                Assert.That(noFallback.Result.FailureCode, Is.EqualTo(ContentResolutionFailureCode.NoCompatibleCandidate));
                Assert.That(noFallback.Result.Probes.Select(x => x.Outcome), Is.EqualTo(new[] { ProbeOutcome.RepresentationMismatch }));
                Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount), "fallback を明示しなければ載らない");

                var missing = await Capture(() => loader.LoadSceneAsync(new ContentRequest("Missing", ContentKind.Scene, "Full")).AsTask());
                Assert.That(missing.Result.FailureCode, Is.EqualTo(ContentResolutionFailureCode.NoCandidate));
                Assert.That(missing.Result.Probes.Select(x => x.Outcome), Is.EqualTo(new[] { ProbeOutcome.NotFound }));

                var otherDigest = new ContentIdentity(expected.ContentSet, expected.Revision, expected.Target, new string('0', 64));
                var pinned = await Capture(() => loader.LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Full", otherDigest)).AsTask());
                Assert.That(pinned.Result.IsResolved, Is.False, "別の digest の expected identity は採られない");
                Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount), "拒んだ要求で何も載らない");

                // 成功経路。
                var entry = await loader.LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Whitebox"));
                AssertRecord(entry.Record, expected, "Whitebox");
                Assert.That(entry.Handle.IsLoaded, Is.True);

                var next = await loader.LoadSceneAsync(new ContentRequest("Next", ContentKind.Scene, "Whitebox", fallbackRepresentation: "Full"));
                AssertRecord(next.Record, expected, "Full");
                Assert.That(next.Handle.IsLoaded, Is.True);

                var prop = await loader.LoadAssetAsync<GameObject>(new ContentRequest("Prop", ContentKind.Object, ""), AssetOwner.Manual);
                AssertRecord(prop.Record, expected, "Full");
                Assert.That(prop.Handle.Value != null, Is.True);

                // 既存の API で解放し、directory を close できる。
                await assets.UnloadSceneAsync("Entry");
                assets.ReleaseScene("Entry");
                await assets.UnloadSceneAsync("Next");
                assets.ReleaseScene("Next");
                assets.Release(prop.Handle);
                await assets.CloseContentDirectoryAsync();
                closed = true;
                Assert.That(session.IsAccepting, Is.False);
            }
            finally
            {
                if (!closed)
                {
                    try { assets.ReleaseAll(); await assets.CloseContentDirectoryAsync(); }
                    catch (Exception cleanup) { Debug.LogWarning("[S31 route test] cleanup after failure: " + cleanup.Message); }
                }
            }
        }

        private static void AssertRecord(ContentLoadRecord record, ContentIdentity expected, string representation)
        {
            Assert.That(record.Candidate.Identity, Is.EqualTo(expected), "記録の identity は install した manifest と digest");
            Assert.That(record.Candidate.Representation, Is.EqualTo(representation), "記録の表現は実際に読んだ表現");
            Assert.That(record.SourceBindingVerified, Is.True);
            Assert.That(record.Candidate.AcceptanceBasis, Is.EqualTo(ContentAcceptanceBasis.VerifiedDigest));
        }

        private static async Task<ContentResolutionException> Capture(Func<Task> action)
        {
            try { await action(); }
            catch (ContentResolutionException exception) { return exception; }
            Assert.Fail("ContentResolutionException was expected.");
            throw new InvalidOperationException();
        }

        private async Task<ContentInstallResult> InstallFromLocalAsync(string published, string digest, string revision)
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(TimeSpan.FromSeconds(60));
            var request = new ContentInstallRequest(digest, ContentSet, revision, BuildContentCoordinator.TargetName,
                Application.unityVersion, 2, long.MaxValue);
            using var local = new LocalContentArtifactSource(published);
            return await new ContentInstaller(new ContentCacheStore(Path.Combine(_deliveryRoot, "cache")))
                .InstallAsync(request, local, cancellation.Token);
        }

        private string CreateScene(string name)
        {
            var path = _folder + "/" + name + ".unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject(name);
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        private static BuildContentRequest Request(string contentSet, string artifacts,
            params (string stableKey, string logicalKey, string path, string? representation)[] roots)
        {
            var candidates = roots.Select(x => new BuildContentCandidate(x.stableKey, x.logicalKey,
                AssetDatabase.AssetPathToGUID(x.path), new BuildProvenance("fixture", x.path))).ToArray();
            var tags = roots.Where(x => x.representation != null).ToDictionary(x => x.stableKey,
                x => (IReadOnlyList<BuildTag>)new[] { new BuildTag("Representation", x.representation!) });
            var closures = roots.Select((x, i) => new BuildDependencySnapshot(candidates[i].PhysicalKey, x.path,
                AssetDatabase.GetDependencies(x.path).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal))
                    .Select(p => new BuildDependencyEntry(AssetDatabase.AssetPathToGUID(p), p)))).ToArray();
            var snapshot = new BuildMaterializationSnapshot(candidates, tags, Array.Empty<BuildContentRequirement>(), closures);
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), candidates, Array.Empty<BuildContentExclusion>());
            return new BuildContentRequest(plan, snapshot, contentSet, artifacts, null, null, BuildContentMode.Full);
        }

        private static void EnsureFolder(string path)
        {
            var parent = "Assets";
            foreach (var segment in path.Substring("Assets/".Length).Split('/'))
            {
                var next = parent + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, segment);
                parent = next;
            }
        }

        private static void DeleteIfEmpty(string assetFolder)
        {
            if (!AssetDatabase.IsValidFolder(assetFolder)) return;
            var absolute = Path.Combine(Application.dataPath, assetFolder.Substring("Assets/".Length));
            if (Directory.GetFileSystemEntries(absolute).Length == 0) AssetDatabase.DeleteAsset(assetFolder);
        }
    }
}
