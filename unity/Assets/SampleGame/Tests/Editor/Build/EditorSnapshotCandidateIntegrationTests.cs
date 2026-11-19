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
using OneStarMaker.Editor.PlaySnapshot;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.BuildContent.Distribution;
using OneStarMaker.Runtime.ContentResolution;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// Editor の Play の複製（snapshot）を要求時解決の供給元にした実機の経路（S3-3 の AC5）。
    /// 合成の map の Scene SnapA・SnapB・SnapD・DirC を製品の coordinator で Full build して publish し、Local の供給元から temp の cache に
    /// install して検証・登録する。その後に SnapA・SnapB・SnapD の source へ編集の印（directory の版に無い GameObject）を足して開き、
    /// public の <see cref="PlaySnapshotSession.Prepare"/>（起動時に要求する印は SnapA だけ）で複製と宣言を作って Play に入る。
    /// Play の中で、宣言の複製を取り込みと同じ口（bridge の delegate）で全て load し、snapshot の供給元を先頭に足した route と loader を
    /// 渡した SceneDirector（fixture の map と factory）で、複製を採る・読み直す・離す、未要求の複製を lifecycle の外に置く、を見る。
    /// SampleGame の型は使わない（build の道具と Runtime の internal がこの assembly から見えるので置いている）。
    /// </summary>
    /// <remarks>
    /// アプリの起動は Addressables の経路にし（config の snapshot の key も置かない）、この test が登録する directory と取り合わない。
    /// 合否は batch（Phase C の標準の実行）で読む。対話の Test Runner では SampleGame の Play の入口が開いている Scene を見て Play を止めうる。
    /// </remarks>
    public sealed class EditorSnapshotCandidateIntegrationTests
    {
        private const string FixtureParent = "Assets/OneStarMakerGenerated/SnapshotCandidateTests";
        private const string ContentSet = "s33-snapshot";
        private const string EditMark = "S33EditMark";
        private const string MaterialRoot = "S33MaterialUser";
        private static readonly Color BuiltColor = Color.red;
        private static readonly Color EditedColor = Color.green;

        private string _folder = "";
        private string _deliveryRoot = "";
        private readonly List<string> _contentPaths = new List<string>();
        private SceneSetup[] _previousSetup = Array.Empty<SceneSetup>();
        private readonly List<string> _unexpectedErrors = new List<string>();
        private bool _expectingFailure;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            PlaySnapshotSession.Release();
            if (_previousSetup.Any(x => x.isActive) && _previousSetup.All(x => File.Exists(x.path)))
                EditorSceneManager.RestoreSceneManagerSetup(_previousSetup);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (_folder.Length != 0) AssetDatabase.DeleteAsset(_folder);
            if (_deliveryRoot.Length != 0 && Directory.Exists(_deliveryRoot)) Directory.Delete(_deliveryRoot, true);
            foreach (var path in _contentPaths.Where(Directory.Exists)) Directory.Delete(path, true);
            _contentPaths.Clear();
            DeleteIfEmpty(FixtureParent);
            DeleteIfEmpty("Assets/OneStarMakerGenerated");
            Application.logMessageReceived -= OnLog;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        // Content build を 1 回と PlayMode の往復を 1 回行う。既存の build を伴う test と同じ上限にする。
        [Timeout(360000)]
        public IEnumerator SnapshotScenes_AreTakenReloadedAndReleased_AndUnrequestedCopiesStayOutsideTheLifecycle()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                Assert.Ignore("Snapshot candidate fixture requires StandaloneWindows64 Player target.");
            if (EditorSceneManager.playModeStartScene != null)
                Assert.Ignore("A Play Mode start scene is already set; the snapshot session cannot be prepared.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    Assert.Ignore("An open scene has unsaved changes; the test does not replace the open scenes.");

            const string modeEnv = "SAMPLEGAME_CONTENT__RUNTIMEMODE";
            var previousMode = Environment.GetEnvironmentVariable(modeEnv, EnvironmentVariableTarget.Process);
            _previousSetup = EditorSceneManager.GetSceneManagerSetup();
            EnsureFolder(FixtureParent);
            _folder = FixtureParent + "/" + Guid.NewGuid().ToString("N").Substring(0, 12);
            EnsureFolder(_folder);
            _deliveryRoot = Path.Combine(Path.GetTempPath(), "osm-s33-snapshot", Guid.NewGuid().ToString("N"));

            // build の時の版。O-1 の material は SnapA（複製から載る）と DirC（directory から載る）の両方が直接参照する。
            var materialPath = _folder + "/S33Shared.mat";
            var material = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            SetColor(material, BuiltColor);
            AssetDatabase.CreateAsset(material, materialPath);
            var snapA = CreateScene("SnapA", materialPath);
            var snapB = CreateScene("SnapB", null);
            var snapD = CreateScene("SnapD", null);
            var dirC = CreateScene("DirC", materialPath);
            AssetDatabase.SaveAssets();

            var artifacts = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "bs3-fixture"));
            var built = new BuildContentCoordinator().Build(Request(ContentSet, artifacts,
                ("s33-snap-a", "SnapA", snapA), ("s33-snap-b", "SnapB", snapB), ("s33-snap-d", "SnapD", snapD), ("s33-dir-c", "DirC", dirC)));
            Assert.That(built.IsSuccess, Is.True, built.Summary);
            _contentPaths.Add(built.ContentPath!);
            var published = ContentTransportPublisher.Publish(built, Path.Combine(_deliveryRoot, "published"));

            var manifestBytes = File.ReadAllBytes(Path.Combine(published, "transport.json"));
            var digest = ContentDeliveryFiles.Sha256(manifestBytes);
            var install = InstallFromLocalAsync(published, digest, built.Identity);
            yield return new WaitUntil(() => install.IsCompleted);
            var installed = install.GetAwaiter().GetResult();
            var expected = new ContentIdentity(installed.ContentSet, installed.Revision, installed.Target, digest);

            // 手元の編集（保存した編集）: 3 つの Scene に directory の版に無い GameObject を足し、material の色を変える。
            foreach (var path in new[] { snapA, snapB, snapD }) AddEditMark(path);
            SetColor(AssetDatabase.LoadAssetAtPath<Material>(materialPath), EditedColor);
            AssetDatabase.SaveAssets();

            EditorSceneManager.OpenScene(snapA, OpenSceneMode.Single);
            EditorSceneManager.OpenScene(snapB, OpenSceneMode.Additive);
            EditorSceneManager.OpenScene(snapD, OpenSceneMode.Additive);
            PlaySnapshotSession.Prepare(name => name == "SnapA");
            var declared = PlaySnapshotSession.Declaration;
            Assert.That(declared.Select(x => x.Identity), Is.EqualTo(new[] { "SnapA", "SnapB", "SnapD" }));
            Assert.That(declared.Select(x => x.RequestAtStartup), Is.EqualTo(new[] { true, false, false }));
            var copies = declared.ToDictionary(x => x.Identity, x => x.CopyPath);

            Environment.SetEnvironmentVariable(modeEnv, "addressables", EnvironmentVariableTarget.Process);
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnLog;
            try
            {
                yield return new EnterPlayMode();
                var run = ExerciseAsync(installed, digest, expected, copies);
                yield return new WaitUntil(() => run.IsCompleted);
                run.GetAwaiter().GetResult();
                yield return new ExitPlayMode();
            }
            finally
            {
                Environment.SetEnvironmentVariable(modeEnv, previousMode, EnvironmentVariableTarget.Process);
            }

            // 観測窓（Play に入ってから Edit Mode に戻るまで）の Error / Exception は、期待した失敗の分だけ。
            Application.logMessageReceived -= OnLog;
            Assert.That(_unexpectedErrors, Is.Empty, string.Join("\n", _unexpectedErrors));
            Assert.That(PlaySnapshotSession.HasNoCopiesOnDisk, Is.True, "Play を出た後に置き場が無い");
            Assert.That(EditorSceneManager.playModeStartScene, Is.Null);
        }

        private async Task ExerciseAsync(ContentInstallResult installed, string digest, ContentIdentity expected, IReadOnlyDictionary<string, string> copies)
        {
            var declaration = EditorPlaySnapshotBridge.Declaration;
            var loadScene = EditorPlaySnapshotBridge.LoadScene;
            Assert.That(declaration, Is.Not.Null, "Prepare が bridge に宣言を置いた");
            Assert.That(loadScene, Is.Not.Null);

            var verified = InstalledRevisionVerifier.Verify(installed.RevisionRoot, digest, installed.ContentSet, installed.Revision, installed.Target);
            var session = ContentDirectorySession.RegisterVerified(verified, new UnityContentDirectoryBackend());
            var assets = new OneStarMaker.Runtime.AssetManagement.AssetManagement();
            assets.InstallContentDirectory(session);
            assets.SealAddressables();
            var uiObject = new GameObject("S33UICommon");
            var ui = uiObject.AddComponent<UICommon>();
            var factory = new FixtureFactory();
            SceneDirector? director = null;
            var closed = false;
            try
            {
                // 取り込みと同じ口で、宣言の複製を全て物理的に load する（台帳にも lifecycle にも入れない）。
                foreach (var scene in declaration!)
                {
                    var operation = loadScene!(scene.CopyPath);
                    Assert.That(operation, Is.Not.Null, scene.CopyPath);
                    await operation!.ToUniTask();
                }

                var source = new EditorSnapshotSceneSource(EditorPlaySnapshotIntake.ToDeclaredScenes(declaration), new UnityEditorSnapshotSceneIo(loadScene!));
                var provider = new EditorSnapshotProvider(source, ContentEntryMatch.Full);
                Assert.That(RegisteredDirectoryRoute.TryCreate(session, assets, provider, out var route, out var reason), Is.True, reason);
                assets.InstallAdoptedSceneSource(source);
                var loader = route!.Loader;
                var map = Map("SnapA", "SnapB", "SnapD", "DirC");
                director = new SceneDirector(factory, ui, map, new NoLoadingDisplay(), assets, "Full", true, loader, route.FallbackRepresentation);

                // (1) 起動時に要求する Scene: 複製から 1 つだけ、編集の印があり、record は snapshot。
                await director.AddScene("SnapA", null, CancellationToken.None);
                AssertRunsTheCopy("SnapA", copies["SnapA"]);
                Assert.That(director.GetContentLoadRecord("SnapA")!.Candidate.SourceKind, Is.EqualTo(ContentSourceKind.EditorSnapshot));

                // (2) 未要求の複製: 物理的に load 済みで、lifecycle・台帳・Scene の所有の外に居る。
                var rootB = AssertUnrequested(director, assets, "SnapB", copies["SnapB"]);
                var rootD = AssertUnrequested(director, assets, "SnapD", copies["SnapD"]);

                // (3) 開いていない Scene は directory から載る（混成）。
                await director.AddScene("DirC", null, CancellationToken.None);
                var dirRecord = director.GetContentLoadRecord("DirC")!;
                Assert.That(dirRecord.Candidate.SourceKind, Is.EqualTo(ContentSourceKind.ContentDirectory));
                Assert.That(dirRecord.Candidate.Identity, Is.EqualTo(expected), "identity は install した manifest の値");
                ObserveSharedMaterial();

                // (4) unload の後の再要求は、同じ複製の path から読み直す。
                await director.UnloadScene("SnapA");
                Assert.That(CountLoaded("SnapA"), Is.Zero, "unload の後に SnapA は無い");
                await director.AddScene("SnapA", null, CancellationToken.None);
                AssertRunsTheCopy("SnapA", copies["SnapA"]);
                Assert.That(director.GetContentLoadRecord("SnapA")!.Candidate.SourceKind, Is.EqualTo(ContentSourceKind.EditorSnapshot));

                // (5) 未要求だった複製の要求は、読み直さずに採る。
                await director.AddScene("SnapB", null, CancellationToken.None);
                Assert.That(Root("SnapB", copies["SnapB"]), Is.SameAs(rootB), "load 済みの複製を採った（root の instance が同じ）");
                Assert.That(director.GetContentLoadRecord("SnapB")!.Candidate.SourceKind, Is.EqualTo(ContentSourceKind.EditorSnapshot));

                // (6) loader の直接の経路でも読み、既存の API で離せる。
                await director.UnloadScene("SnapB");
                var direct = await loader.LoadSceneAsync(new ContentRequest("SnapB", ContentKind.Scene, string.Empty));
                Assert.That(direct.Record.Candidate.SourceKind, Is.EqualTo(ContentSourceKind.EditorSnapshot));
                Assert.That(direct.Handle.IsLoaded, Is.True);
                AssertRunsTheCopy("SnapB", copies["SnapB"]);
                await assets.UnloadSceneAsync("SnapB");
                assets.ReleaseScene("SnapB");
                Assert.That(CountLoaded("SnapB"), Is.Zero);

                // (7) PreLoad の中で取り消した要求は、未要求の複製を離さない。
                _expectingFailure = true;
                using (var cts = new CancellationTokenSource())
                {
                    factory.PreLoad["SnapD"] = async ct =>
                    {
                        cts.Cancel();
                        await UniTask.WaitUntilCanceled(ct);
                        ct.ThrowIfCancellationRequested();
                    };
                    var canceled = false;
                    try { await director.AddScene("SnapD", null, cts.Token); }
                    catch (OperationCanceledException) { canceled = true; }
                    Assert.That(canceled, Is.True);
                }
                factory.PreLoad.Remove("SnapD");
                _expectingFailure = false;
                Assert.That(Root("SnapD", copies["SnapD"]), Is.SameAs(rootD), "SnapD は load 済みのまま");
                AssertUnrequested(director, assets, "SnapD", copies["SnapD"]);

                // (8) 複製の file が無い時は失敗し、directory の版へ進まない。
                await director.UnloadScene("SnapA");
                File.Delete(copies["SnapA"]);
                _expectingFailure = true;
                Exception? failure = null;
                try { await director.AddScene("SnapA", null, CancellationToken.None); }
                catch (Exception exception) { failure = exception; }
                _expectingFailure = false;
                Assert.That(failure, Is.Not.Null, "AddScene は失敗する");
                Assert.That(failure!.Message, Does.Contain("SnapA"));
                Assert.That(CountLoaded("SnapA"), Is.Zero, "SnapA という名前の Scene は load 済みに無い");
                Assert.That(Root("SnapD", copies["SnapD"]), Is.SameAs(rootD), "未要求の SnapD は load 済みのまま");

                // (9) 予約した identity は directory の content 入口から読めない。
                _expectingFailure = true;
                ContentDirectoryException? reserved = null;
                try { await assets.LoadContentSceneAsync("SnapD", ContentEntryMatch.Full); }
                catch (ContentDirectoryException exception) { reserved = exception; }
                _expectingFailure = false;
                Assert.That(reserved, Is.Not.Null);
                Assert.That(reserved!.Code, Is.EqualTo(ContentDirectoryFailureCode.EntryAmbiguous));
                Assert.That(CountLoaded("SnapD"), Is.EqualTo(1));

                // 全てを離して directory を close する。未要求の SnapD は SceneDirector も台帳も持たないので、ここで Unity から外す。
                await director.UnloadScene("DirC");
                director.Dispose();
                director = null;
                await SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(copies["SnapD"])).ToUniTask();
                await assets.CloseContentDirectoryAsync();
                closed = true;
                Assert.That(session.IsAccepting, Is.False);
            }
            finally
            {
                _expectingFailure = false;
                director?.Dispose();
                if (!closed)
                {
                    try { assets.ReleaseAll(); await assets.CloseContentDirectoryAsync(); }
                    catch (Exception cleanup) { Debug.LogWarning("[S33 snapshot test] cleanup after failure: " + cleanup.Message); }
                }
                UnityEngine.Object.Destroy(uiObject);
            }
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (_expectingFailure) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _unexpectedErrors.Add(type + ": " + condition);
        }

        private static void AssertRunsTheCopy(string identity, string copyPath)
        {
            Assert.That(CountLoaded(identity), Is.EqualTo(1), identity + " は 1 つだけ");
            var scene = SceneManager.GetSceneByName(identity);
            Assert.That(scene.path, Is.EqualTo(copyPath), identity + " の path は複製");
            Assert.That(scene.GetRootGameObjects().Any(x => x.name == EditMark), Is.True, identity + " に編集の印がある（directory の版には無い）");
        }

        private static GameObject AssertUnrequested(SceneDirector director, OneStarMaker.Runtime.AssetManagement.AssetManagement assets, string identity, string copyPath)
        {
            var scene = SceneManager.GetSceneByPath(copyPath);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True, identity + " は物理的に load 済み");
            Assert.That(director.ContainsScene(identity), Is.False, identity + " は SceneDirector の組を持たない");
            Assert.That(director.GetContentLoadRecord(identity), Is.Null);
            Assert.That(assets.GetOwnedAssets(AssetOwner.Scene(identity)), Is.Empty, identity + " の所有の asset を Framework は作らない");
            // 台帳に無いことを見る: 台帳の unload は台帳にある Scene だけを離す。load 済みのままなら、台帳に無い。
            assets.UnloadSceneAsync(identity).GetAwaiter().GetResult();
            Assert.That(SceneManager.GetSceneByPath(copyPath).isLoaded, Is.True, identity + " は Scene の台帳に無い");
            return Root(identity, copyPath);
        }

        private static GameObject Root(string identity, string copyPath)
        {
            var scene = SceneManager.GetSceneByPath(copyPath);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True, identity + " が load 済み");
            return scene.GetRootGameObjects().Single(x => x.name == identity);
        }

        private static int CountLoaded(string name)
        {
            var count = 0;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name == name) count++;
            }
            return count;
        }

        /// <summary>O-1: 合否に使わない観測。複製から載った SnapA と directory から載った DirC の、同じ material の参照の色を 1 行に出す。</summary>
        private static void ObserveSharedMaterial()
        {
            try
            {
                var snap = FindRenderer("SnapA");
                var directoryScene = SceneManager.GetSceneByName("DirC");
                var line = "[S3-3 O-1] snap=" + (snap == null ? "unresolved" : Describe(snap.sharedMaterial));
                if (directoryScene.IsValid() && directoryScene.isLoaded)
                {
                    var dir = FindRenderer("DirC");
                    line += " directory=" + (dir == null ? "unresolved" : Describe(dir.sharedMaterial)) +
                            " sameInstance=" + (snap != null && dir != null && ReferenceEquals(snap.sharedMaterial, dir.sharedMaterial) ? "true" : "false");
                }
                else
                {
                    line += " directory=not-loaded-yet";
                }
                Debug.Log(line + " built=" + Describe(BuiltColor) + " edited=" + Describe(EditedColor));
            }
            catch (Exception exception)
            {
                Debug.Log("[S3-3 O-1] observation unavailable: " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static MeshRenderer? FindRenderer(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            var holder = scene.GetRootGameObjects().FirstOrDefault(x => x.name == MaterialRoot);
            return holder == null ? null : holder.GetComponent<MeshRenderer>();
        }

        private static string Describe(Material? material)
        {
            if (material == null) return "null";
            return Describe(material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color);
        }

        private static string Describe(Color color) => "(" + color.r.ToString("0.##") + "," + color.g.ToString("0.##") + "," + color.b.ToString("0.##") + ")";

        private static void SetColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
        }

        private string CreateScene(string name, string? materialPath)
        {
            var path = _folder + "/" + name + ".unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject(name);
            if (materialPath != null)
            {
                var holder = new GameObject(MaterialRoot);
                holder.AddComponent<MeshFilter>();
                holder.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            }
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        private static void AddEditMark(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            new GameObject(EditMark);
            EditorSceneManager.SaveScene(scene);
        }

        private SceneResourceMap Map(params string[] identities)
        {
            var resources = identities.Select(identity =>
            {
                var resource = ScriptableObject.CreateInstance<SceneResource>();
                resource.Identity = identity;
                var description = new SceneAssetDescription(identity, LoadType.OnDemand, new List<AssetPayload>
                {
                    new AssetPayload(string.Empty, new AssetReference("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")),
                });
                typeof(SceneResource).GetField("_sceneAssetDescription", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .SetValue(resource, description);
                return resource;
            }).ToArray();
            var map = ScriptableObject.CreateInstance<SceneResourceMap>();
            typeof(SceneResourceMap).GetField("_dictionary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(map, resources.ToDictionary(x => x.Identity, x => x));
            return map;
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

        private static BuildContentRequest Request(string contentSet, string artifacts, params (string stableKey, string logicalKey, string path)[] roots)
        {
            var candidates = roots.Select(x => new BuildContentCandidate(x.stableKey, x.logicalKey,
                AssetDatabase.AssetPathToGUID(x.path), new BuildProvenance("fixture", x.path))).ToArray();
            var tags = roots.ToDictionary(x => x.stableKey, x => (IReadOnlyList<BuildTag>)new[] { new BuildTag("Representation", "Full") });
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

        /// <summary>
        /// fixture の Scene。I-2 の host の規約どおり、Awake / Start で <see cref="AssetOwner.Scene"/> の asset を取らない（何も読まない）。
        /// PreLoad の振る舞いだけを identity ごとに差し替えられる。
        /// </summary>
        private sealed class FixtureScene : SceneBase
        {
            private readonly Func<CancellationToken, UniTask>? _preLoad;

            internal FixtureScene(SceneResource resource, ISceneQuery query, ISceneController controller, Func<CancellationToken, UniTask>? preLoad)
                : base(resource, query, controller)
            {
                _preLoad = preLoad;
            }

            protected override UniTask OnPreLoadedImpl(CancellationToken ct) => _preLoad != null ? _preLoad(ct) : UniTask.CompletedTask;
        }

        private sealed class FixtureFactory : ISceneFactory
        {
            internal Dictionary<string, Func<CancellationToken, UniTask>> PreLoad { get; } = new(StringComparer.Ordinal);

            public SceneBase? CreateSceneClass(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController)
            {
                PreLoad.TryGetValue(sceneResource.Identity, out var preLoad);
                return new FixtureScene(sceneResource, sceneQuery, sceneController, preLoad);
            }
        }

        private sealed class NoLoadingDisplay : ILoadingDisplay
        {
            public UniTask Show(LoadingDisplayType type, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask Hide(CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
