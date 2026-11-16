#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// 目的 build の索引と、実際に build した directory の一致（索引が「含む」と答える要求を、その directory を登録した索引が解決できる）。
    /// 目的 A（Scene Entry、Scene Shared、Representation の tag の無い Object SharedPrefab）と目的 B（Scene Next、Scene Shared の
    /// Full と Whitebox、Whitebox だけの Scene WhiteboxOnly）を製品の coordinator で Full build して publish し、
    /// B の公開 directory を Local と loopback の HTTP から別々の cache に install して、同じ bytes と同じ選択の結果になることを見る。
    /// SampleGame の型は使わない（build と loopback の HTTP の道具がこの assembly にあるので置いている）。
    /// </summary>
    public sealed class PurposeBuildIndexIntegrationTests
    {
        private const string FixtureParent = "Assets/OneStarMakerGenerated/PurposeIndexTests";
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
        // Content build を 2 回と PlayMode の往復を 1 回行う。既存の build を伴う test と同じ上限にする。
        [Timeout(360000)]
        public IEnumerator PublishedIndexMatchesTheRegisteredDirectory_AndLocalAndHttpInstallsSelectTheSame()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                Assert.Ignore("Purpose index fixture requires StandaloneWindows64 Player target.");
            const string modeEnv = "SAMPLEGAME_CONTENT__RUNTIMEMODE";
            var previousMode = Environment.GetEnvironmentVariable(modeEnv, EnvironmentVariableTarget.Process);
            EnsureFolder(FixtureParent);
            _folder = FixtureParent + "/" + Guid.NewGuid().ToString("N");
            EnsureFolder(_folder);
            _deliveryRoot = Path.Combine(Path.GetTempPath(), "osm-purpose-index", Guid.NewGuid().ToString("N"));
            BuildContentResult a, b;
            string publishedA, publishedB;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var entry = CreateScene("Entry");
                var shared = CreateScene("Shared");
                var next = CreateScene("Next");
                var sharedWhitebox = CreateScene("SharedWhitebox");
                var whiteboxOnly = CreateScene("WhiteboxOnly");
                var prefabPath = _folder + "/SharedPrefab.prefab";
                var go = new GameObject("SharedPrefab");
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                UnityEngine.Object.DestroyImmediate(go);
                AssetDatabase.SaveAssets();

                var artifacts = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "bs3-fixture"));
                var coordinator = new BuildContentCoordinator();
                a = coordinator.Build(Request("purpose-a", artifacts,
                    ("a-entry", "Entry", entry, "Full"), ("a-shared", "Shared", shared, "Full"), ("a-prefab", "SharedPrefab", prefabPath, null)));
                Assert.That(a.IsSuccess, Is.True, a.Summary);
                _contentPaths.Add(a.ContentPath!);
                b = coordinator.Build(Request("purpose-b", artifacts,
                    ("b-next", "Next", next, "Full"), ("b-shared", "Shared", shared, "Full"),
                    ("b-shared-wb", "Shared", sharedWhitebox, "Whitebox"), ("b-wb-only", "WhiteboxOnly", whiteboxOnly, "Whitebox")));
                Assert.That(b.IsSuccess, Is.True, b.Summary);
                _contentPaths.Add(b.ContentPath!);
                publishedA = ContentTransportPublisher.Publish(a, Path.Combine(_deliveryRoot, "a"));
                publishedB = ContentTransportPublisher.Publish(b, Path.Combine(_deliveryRoot, "b"));
            }
            finally
            {
                // 登録の検証が source asset に依存しないよう、Play の前に fixture を消す。
                if (previous.Any(x => x.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (_folder.Length != 0) { AssetDatabase.DeleteAsset(_folder); _folder = ""; }
            }

            // AC7: 同じ公開 directory を Local と HTTP の供給元から別々の cache root に install する。
            var bytesB = File.ReadAllBytes(Path.Combine(publishedB, "transport.json"));
            var digestB = ContentDeliveryFiles.Sha256(bytesB);
            var install = InstallFromLocalAndHttpAsync(publishedB, digestB, b.Identity);
            yield return new WaitUntil(() => install.IsCompleted);
            var (localRoot, httpRoot) = install.GetAwaiter().GetResult();
            var localBytes = File.ReadAllBytes(Path.Combine(localRoot, "transport.json"));
            var httpBytes = File.ReadAllBytes(Path.Combine(httpRoot, "transport.json"));
            Assert.That(localBytes, Is.EqualTo(bytesB));
            Assert.That(httpBytes, Is.EqualTo(bytesB));
            var published = Read(bytesB);
            Assert.That(Read(localBytes).Entries.Select(Text), Is.EqualTo(published.Entries.Select(Text)));
            Assert.That(Read(httpBytes).Entries.Select(Text), Is.EqualTo(published.Entries.Select(Text)));
            var bytesA = File.ReadAllBytes(Path.Combine(publishedA, "transport.json"));
            foreach (var request in Requests())
            {
                var expected = Describe(ContentPurposeIndex.Select(request, new[] { Candidate(bytesB) }));
                Assert.That(Describe(ContentPurposeIndex.Select(request, new[] { Candidate(localBytes) })), Is.EqualTo(expected), Text(request));
                Assert.That(Describe(ContentPurposeIndex.Select(request, new[] { Candidate(httpBytes) })), Is.EqualTo(expected), Text(request));
                // A と B の 2 候補の選択も、どちらの bytes を使っても同じ。
                var expectedPair = Describe(ContentPurposeIndex.Select(request, new[] { Candidate(bytesA), Candidate(bytesB) }));
                Assert.That(Describe(ContentPurposeIndex.Select(request, new[] { Candidate(bytesA), Candidate(httpBytes) })),
                    Is.EqualTo(expectedPair), Text(request));
            }

            // AC6: 公開した content/ を登録し、root の entry と索引の entry、解決の答えを突き合わせる。
            Environment.SetEnvironmentVariable(modeEnv, "addressables", EnvironmentVariableTarget.Process);
            try
            {
                yield return new EnterPlayMode();
                AssertMatchesRegisteredDirectory(publishedA, a.Identity, "Full");
                AssertMatchesRegisteredDirectory(publishedB, b.Identity, "Whitebox");
                yield return new ExitPlayMode();
            }
            finally
            {
                Environment.SetEnvironmentVariable(modeEnv, previousMode, EnvironmentVariableTarget.Process);
            }
        }

        private static void AssertMatchesRegisteredDirectory(string published, string identity, string sharedWhiteboxResolvesTo)
        {
            var index = Read(File.ReadAllBytes(Path.Combine(published, "transport.json")));
            Assert.That(index.State, Is.EqualTo(ContentPurposeIndexState.Indexed), index.Detail);
            var backend = new UnityContentDirectoryBackend();
            Assert.That(backend.Register(Path.Combine(published, "content")), Is.True);
            try
            {
                var roots = backend.GetRoots();
                Assert.That(roots.Length, Is.EqualTo(1));
                Assert.That(ContentDirectoryIndex.TryCreate(roots[0], identity, BuildContentCoordinator.TargetName,
                    out var directory, out var issue), Is.True, issue.Message);
                // 生の値どうしで等しい（tag の無い SharedPrefab の表現は空のまま）。
                var rootEntries = roots[0].Entries.Select(x => x.LogicalKey + "/" + x.Kind + "/" + x.Representation).ToArray();
                Assert.That(index.Entries.Select(Text), Is.EquivalentTo(rootEntries));
                if (rootEntries.Any(x => x.StartsWith("SharedPrefab/", StringComparison.Ordinal)))
                    Assert.That(rootEntries, Does.Contain("SharedPrefab/Object/"));

                foreach (var request in Requests())
                {
                    var resolved = Resolve(directory!, request, out _);
                    Assert.That(index.Contains(request), Is.EqualTo(resolved), Text(request));
                }
                // Shared の Whitebox は、B では Whitebox の entry、A では Full の entry へ fallback して解決する。
                Assert.That(Resolve(directory!, new ContentPurposeRequest(BuildContentKind.Scene, "Shared", "Whitebox"), out var sharedEntry), Is.True);
                Assert.That(sharedEntry!.Representation, Is.EqualTo(sharedWhiteboxResolvesTo));
            }
            finally
            {
                backend.Unregister();
            }
        }

        private static bool Resolve(ContentDirectoryIndex directory, ContentPurposeRequest request, out BuildContentEntry? entry)
            => request.Kind == BuildContentKind.Scene
                ? directory.TryResolveScene(request.LogicalKey, request.Representation ?? "", out entry, out _)
                : directory.TryResolveObject(request.LogicalKey, request.Representation ?? "", out entry, out _);

        // 両方の目的の全 entry（生の表現で）と、表現の指定なし、Missing、Shared の Whitebox、WhiteboxOnly の指定なしと Whitebox、
        // Next を Object として要求した時（kind の違い）。
        private static IEnumerable<ContentPurposeRequest> Requests()
        {
            yield return new ContentPurposeRequest(BuildContentKind.Scene, "Entry", "Full");
            yield return new ContentPurposeRequest(BuildContentKind.Scene, "Shared", "Full");
            yield return new ContentPurposeRequest(BuildContentKind.Object, "SharedPrefab", "");
            yield return new ContentPurposeRequest(BuildContentKind.Scene, "Next", "Full");
            yield return new ContentPurposeRequest(BuildContentKind.Scene, "Shared", "Whitebox");
            yield return new ContentPurposeRequest(BuildContentKind.Scene, "WhiteboxOnly", "Whitebox");
            yield return new ContentPurposeRequest(BuildContentKind.Scene, "Entry");
            yield return new ContentPurposeRequest(BuildContentKind.Scene, "Shared");
            yield return new ContentPurposeRequest(BuildContentKind.Scene, "Next");
            yield return new ContentPurposeRequest(BuildContentKind.Scene, "WhiteboxOnly");
            yield return new ContentPurposeRequest(BuildContentKind.Object, "SharedPrefab", "Whitebox");
            yield return new ContentPurposeRequest(BuildContentKind.Scene, "Missing");
            yield return new ContentPurposeRequest(BuildContentKind.Object, "Next");
        }

        private async Task<(string local, string http)> InstallFromLocalAndHttpAsync(string published, string digest, string revision)
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(TimeSpan.FromSeconds(60));
            var request = new ContentInstallRequest(digest, "purpose-b", revision, BuildContentCoordinator.TargetName,
                Application.unityVersion, 2, long.MaxValue);
            using var local = new LocalContentArtifactSource(published);
            var localInstalled = await new ContentInstaller(new ContentCacheStore(Path.Combine(_deliveryRoot, "local-cache")))
                .InstallAsync(request, local, cancellation.Token);
            var httpRoot = "";
            var server = new LoopbackArtifactServer(published, cancellation.Token);
            await ContentInstallTestOperation.RunWithShutdownAsync(async () =>
            {
                using var remote = new HttpContentArtifactSource(server.BaseUri);
                var installed = await new ContentInstaller(new ContentCacheStore(Path.Combine(_deliveryRoot, "http-cache")))
                    .InstallAsync(request, remote, cancellation.Token);
                httpRoot = installed.RevisionRoot;
                Assert.That(server.RequestCount, Is.GreaterThan(1));
            }, server.StopAsync);
            return (localInstalled.RevisionRoot, httpRoot);
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

        private static ContentPurposeIndex Read(byte[] bytes) => ContentPurposeIndex.Read(bytes, ContentDeliveryFiles.Sha256(bytes));

        private static ContentPurposeCandidate Candidate(byte[] bytes) => new(bytes, ContentDeliveryFiles.Sha256(bytes));

        private static string Describe(ContentPurposeSelection selection)
            => selection.Status + ":" + selection.SelectedIndex + ":" + string.Join(",", selection.Candidates.Select(x => x.Verdict));

        private static string Text(ContentTransportIndexEntry x) => x.logicalKey + "/" + x.kind + "/" + x.representation;

        private static string Text(ContentPurposeRequest x) => x.LogicalKey + "/" + x.Kind + "/" + (x.Representation ?? "(unspecified)");

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
