#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.BuildContent.Distribution;
using OneStarMaker.Runtime.AssetManagement;
using Unity.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Tests.Editor.Build
{
    // 実 Content Directory を作る統合テスト。Editor で build し、source fixture を消してから
    // PlayMode で移設先だけを登録することで、AssetDatabase 上の source に依存した見かけの成功を避ける。
    public sealed class BuildContentDirectoryIntegrationTests
    {
        private const string FixtureParent = "Assets/OneStarMakerGenerated/BS3Tests";
        private string _folder = "";
        private string _copy = "";
        private string _deliveryRoot = "";
        private string _installedRoot = "";
        private string _manifestDigest = "";

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            // assertion 失敗が PlayMode 中に起きても Editor に戻し、このテスト所有の fixture/copy のみ消す。
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            if (_folder.Length != 0) AssetDatabase.DeleteAsset(_folder);
            if (_copy.Length != 0 && Directory.Exists(_copy)) Directory.Delete(_copy, true);
            if (_deliveryRoot.Length != 0 && Directory.Exists(_deliveryRoot)) Directory.Delete(_deliveryRoot, true);
            DeleteIfEmpty(FixtureParent);
            DeleteIfEmpty("Assets/OneStarMakerGenerated");
        }

        [UnityTest]
        // Content build を二度行い、実アプリの PlayMode を二度往復するため、既定の180秒では
        // Editor の再コンパイルが重なったときにテスト本体を中断する。無期限待機にはしない。
        [Timeout(360000)]
        public IEnumerator ScenePrefabTextureAndTwoRepresentationsBuildAndMoveAsOneDirectory()
        {
            // Phase A で固定した target/subtarget だけを検証する。異なる環境では勝手に切り替えない。
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                Assert.Ignore("BS2b fixture requires StandaloneWindows64 Player target.");
            EnsureFolder(FixtureParent);
            _folder = FixtureParent + "/" + Guid.NewGuid().ToString("N");
            EnsureFolder(_folder);
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // Scene と二つの Prefab が同じ Material/Texture を参照する fixture を作る。
                // High/Low は同じ logical key だが別 physical key。Excluded は選択されない root。
                var texture = new Texture2D(2, 2);
                texture.SetPixels(new[] { Color.red, Color.red, Color.red, Color.red });
                texture.Apply();
                var texturePath = _folder + "/Texture.png";
                File.WriteAllBytes(Path.Combine(Application.dataPath, texturePath.Substring("Assets/".Length)), texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath);
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("Fixture shader is unavailable.");
                var mat = new Material(shader);
                mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                var matPath = _folder + "/Shared.mat";
                AssetDatabase.CreateAsset(mat, matPath);
                var paths = new[] { _folder + "/Scene.unity", _folder + "/High.prefab",
                    _folder + "/Low.prefab", texturePath, _folder + "/Excluded.prefab" };
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var sceneObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sceneObject.GetComponent<Renderer>().sharedMaterial = mat;
                EditorSceneManager.SaveScene(scene, paths[0]);
                UnityEngine.Object.DestroyImmediate(sceneObject);
                for (var i = 1; i <= 2; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.GetComponent<Renderer>().sharedMaterial = mat;
                    PrefabUtility.SaveAsPrefabAsset(go, paths[i]);
                    UnityEngine.Object.DestroyImmediate(go);
                }
                var excluded = new GameObject("Excluded");
                PrefabUtility.SaveAsPrefabAsset(excluded, paths[4]);
                UnityEngine.Object.DestroyImmediate(excluded);
                EditorSceneManager.SaveScene(scene, paths[0]);
                AssetDatabase.SaveAssets();

                var candidates = paths.Select((path, i) => new BuildContentCandidate("fixture-" + i,
                    i == 1 || i == 2 ? "shared-logical" : "logical-" + i,
                    AssetDatabase.AssetPathToGUID(path), new BuildProvenance("fixture", path))).ToArray();
                var tags = new Dictionary<string, IReadOnlyList<BuildTag>> {
                    [candidates[1].StableKey] = new[] { new BuildTag("Representation", "High") },
                    [candidates[2].StableKey] = new[] { new BuildTag("Representation", "Low") },
                    [candidates[4].StableKey] = new[] { new BuildTag("Representation", "Excluded") }
                };
                var closures = paths.Select((path, i) => new BuildDependencySnapshot(candidates[i].PhysicalKey, path,
                    // fixture 作成時だけ Unity に閉包を採取させる。本番 consumer は snapshot を再計算しない。
                    AssetDatabase.GetDependencies(path).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal))
                        .Select(p => new BuildDependencyEntry(AssetDatabase.AssetPathToGUID(p), p)))).ToArray();
                var snapshot = new BuildMaterializationSnapshot(candidates, tags,
                    new[] { new BuildContentRequirement("shared-logical", BuildContentCardinality.OneOrMore) }, closures);
                var selection = new BuildTagSelector().Select(new BuildRequest(new[] {
                        new BuildTag("Representation", "High"), new BuildTag("Representation", "Low") }),
                    snapshot.Candidates, snapshot.TagProviders,
                    new BuildSelectionPolicy(new BuildTagSchema(new[] {
                        new KeyValuePair<string, IEnumerable<string>>("Representation", new[] { "High", "Low", "Excluded" })
                    }), snapshot.Requirements));
                Assert.That(selection.IsSuccess, Is.True);
                // 実 build の生成物はこのテスト専用領域へ置き、過去の検証ログを上書きしない。
                var artifacts = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "bs3-fixture"));
                var workspace = Path.Combine(artifacts, "work", BuildContentCoordinator.TargetName, "fixture");
                var request = new BuildContentRequest(selection.Plan!, snapshot, "fixture", artifacts);
                var coordinator = new BuildContentCoordinator();
                // 同じ target/contentSet の workspace へ連続 build。公開先は identity ごとに別 directory。
                // 両 report と manifest を確認し、2 回目が前回の成功 path を上書きしないことを示す。
                var first = coordinator.Build(request);
                Assert.That(first.IsSuccess, Is.True, first.Summary);
                Assert.That(File.Exists(Path.Combine(workspace, "BuildManifestHash.txt")), Is.True);
                var result = coordinator.Build(request);
                Assert.That(result.IsSuccess, Is.True, result.Summary);
                Assert.That(File.Exists(Path.Combine(workspace, "BuildManifestHash.txt")), Is.True);
                Assert.That(result.Identity, Is.Not.EqualTo(first.Identity));
                Assert.That(result.ContentPath, Is.Not.EqualTo(first.ContentPath));
                Assert.That(File.Exists(first.ManifestPointer), Is.True);
                Assert.That(File.Exists(result.ManifestPointer), Is.True);
                Assert.That(Directory.Exists(first.MetadataPath), Is.True);
                Assert.That(Directory.Exists(result.MetadataPath), Is.True);
                Assert.That(File.ReadAllText(first.PreflightPath), Does.Contain(first.Identity));
                Assert.That(File.ReadAllText(first.OutcomePath), Does.Contain(first.Identity));
                Assert.That(File.ReadAllText(result.PreflightPath), Does.Contain(result.Identity));
                Assert.That(File.ReadAllText(result.OutcomePath), Does.Contain(result.Identity));
                Assert.That(File.ReadAllText(result.PreflightPath), Does.Contain(matPath));
                Assert.That(File.ReadAllText(result.PreflightPath), Does.Contain("ValueNotSelected"));
                // Unity 内部ファイルを解釈せず、公開 directory に content file と resource file があることを確認する。
                // 選択 root の意味づけは、登録後に同梱 root を読む側で確認する。
                Assert.That(Directory.GetFiles(result.ContentPath!, "*.cf").Length, Is.GreaterThan(0));
                Assert.That(Directory.GetFiles(result.ContentPath!, "*.resS").Length, Is.GreaterThan(0));
                _copy = Path.Combine(artifacts, "integration-copy-" + result.Identity);
                Copy(result.ContentPath!, _copy);
                // 配信 fixture は checkout の深さに依存させない。Windows Mono の file open
                // 制限に build identity と staging 名が重なるため、専有する短い一時 root を使う。
                _deliveryRoot = Path.Combine(Path.GetTempPath(), "osm-dist-integration", Guid.NewGuid().ToString("N"));
                var published = ContentTransportPublisher.Publish(result, Path.Combine(_deliveryRoot, "published"));
                _manifestDigest = HashFile(Path.Combine(published, "transport.json"));
                // 本物の HTTP response を installer に渡す。Unity へ URL や staging を渡さず、
                // 完全検証された installed directory を後段の実 load に使う。
                yield return UniTask.ToCoroutine(async () =>
                {
                    var installRequest = new ContentInstallRequest(_manifestDigest, "fixture", result.Identity,
                        BuildContentCoordinator.TargetName, Application.unityVersion, 2, long.MaxValue);
                    using var server = new LoopbackArtifactServer(published);
                    using var remote = new HttpContentArtifactSource(server.BaseUri);
                    var remoteStore = new ContentCacheStore(Path.Combine(_deliveryRoot, "http-cache"));
                    var installed = await new ContentInstaller(remoteStore).InstallAsync(installRequest, remote,
                        CancellationToken.None);
                    _installedRoot = installed.RevisionRoot;
                    using var local = new LocalContentArtifactSource(published);
                    var localStore = new ContentCacheStore(Path.Combine(_deliveryRoot, "local-cache"));
                    var localInstalled = await new ContentInstaller(localStore).InstallAsync(installRequest, local,
                        CancellationToken.None);
                    Assert.That(localInstalled.ManifestSha256, Is.EqualTo(installed.ManifestSha256));
                    foreach (var file in Directory.GetFiles(installed.ContentPath, "*", SearchOption.AllDirectories))
                        Assert.That(HashFile(Path.Combine(localInstalled.ContentPath,
                            Path.GetRelativePath(installed.ContentPath, file))), Is.EqualTo(HashFile(file)));
                    Assert.That(server.RequestCount, Is.GreaterThan(1));
                });
            }
            finally
            {
                // PlayMode に入る前に Editor scene と source fixture を片付ける。
                // 移設後の検証が source asset に偶然依存しないようにする。
                if (previous.Any(x => x.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (_folder.Length != 0) { AssetDatabase.DeleteAsset(_folder); _folder = ""; }
            }
            yield return new EnterPlayMode();
            var defaultBootstrap = GetBootstrapState();
            for (var frame = 0; frame < 1800 && defaultBootstrap.director.GetValue(defaultBootstrap.initializer) == null; frame++)
                yield return null;
            Assert.That(defaultBootstrap.director.GetValue(defaultBootstrap.initializer), Is.Not.Null,
                "既定 Addressables mode で SceneDirector が生成されていない");
            Assert.That(defaultBootstrap.session.GetValue(defaultBootstrap.initializer), Is.Null,
                "明示指定なしで Content Directory が登録された");
            // ContentLoadManager の登録は PlayMode 側で行う。copy に必要な file が欠ければここで失敗する。
            var handle = ContentLoadManager.RegisterContentDirectory(_copy);
            Assert.That(handle.IsValid, Is.True);
            try
            {
                var roots = ContentLoadManager.GetRootAssets<BuildContentRoot>(handle);
                // 単一 root と identity を照合し、Scene/Prefab/Texture の4 entry、High/Low の2表現、
                // 非選択 root の不在を source 再走査なしで検証する。実 Object の型付き load は BS3 に渡す。
                Assert.That(roots.Length, Is.EqualTo(1));
                Assert.That(roots[0].BuildIdentity,
                    Is.EqualTo(Path.GetFileName(_copy).Substring("integration-copy-".Length)));
                Assert.That(roots[0].Entries.Count, Is.EqualTo(4));
                Assert.That(roots[0].Entries.Select(x => x.StableKey),
                    Is.EquivalentTo(new[] { "fixture-0", "fixture-1", "fixture-2", "fixture-3" }));
                Assert.That(roots[0].Entries.All(x => x.StableKey != "fixture-4"), Is.True);
                Assert.That(roots[0].Entries.Single(x => x.StableKey == "fixture-0").Kind,
                    Is.EqualTo(BuildContentKind.Scene));
                Assert.That(roots[0].Entries.Where(x => x.StableKey != "fixture-0").All(x => x.Kind == BuildContentKind.Object), Is.True);
                Assert.That(roots[0].Entries.Count(x => x.LogicalKey == "shared-logical"), Is.EqualTo(2));
                Assert.That(roots[0].Entries.Where(x => x.LogicalKey == "shared-logical")
                    .Select(x => x.Representation), Is.EquivalentTo(new[] { "High", "Low" }));
                Assert.That(roots[0].Entries.Single(x => x.Kind == BuildContentKind.Scene).SceneId, Is.Not.EqualTo(default(LoadableSceneId)));
                Assert.That(roots[0].Entries.Where(x => x.Kind == BuildContentKind.Object).All(x => x.Object != null), Is.True);
            }
            finally { ContentLoadManager.UnregisterContentDirectory(handle); }
            // source fixture は既に削除済み。登録済み root の Unity locator から実際の Scene/Object を
            // 取得して解放し、Editor の AssetDatabase 側で成功したように見える経路を除外する。
            yield return UniTask.ToCoroutine(async () =>
            {
                var identity = Path.GetFileName(_copy).Substring("integration-copy-".Length);
                var verified = InstalledRevisionVerifier.Verify(_installedRoot, _manifestDigest,
                    "fixture", identity, BuildContentCoordinator.TargetName);
                var session = ContentDirectorySession.RegisterVerified(verified, new UnityContentDirectoryBackend());
                var assets = new AssetManagement();
                assets.InstallContentDirectory(session);
                IAssetHandle<GameObject>? prefab = null;
                IAssetHandle<Texture2D>? texture = null;
                GameObject? instance = null;
                var sceneLoaded = false;
                try
                {
                    prefab = await assets.LoadContentAssetAsync<GameObject>("shared-logical", "High", AssetOwner.Manual);
                    Assert.That(prefab.Value, Is.Not.Null);
                    texture = await assets.LoadContentAssetAsync<Texture2D>("logical-3", "Full", AssetOwner.Manual);
                    Assert.That(texture.Value, Is.Not.Null);
                    instance = await assets.InstantiateContentAsync("shared-logical", "Low");
                    Assert.That(instance, Is.Not.Null);
                    var sceneHandle = await assets.LoadContentSceneAsync("logical-0", "Full",
                        new SceneLoadOptions(LoadSceneMode.Additive));
                    sceneLoaded = true;
                    Assert.That(sceneHandle.IsLoaded, Is.True);
                    // pin の候補除外ではなく、実 session/owner が保持する lease で削除を拒否する。
                    var eviction = Assert.Throws<ContentDeliveryException>(() =>
                        new ContentCacheStore(Path.Combine(_deliveryRoot, "http-cache")).Evict(0));
                    Assert.That(eviction!.Code, Is.EqualTo(ContentDeliveryFailureCode.BudgetUnsatisfied));
                    Assert.That(Directory.Exists(_installedRoot), Is.True);
                }
                finally
                {
                    if (sceneLoaded)
                    {
                        await assets.UnloadSceneAsync("logical-0");
                        assets.ReleaseScene("logical-0");
                    }
                    if (instance != null)
                    {
                        UnityEngine.Object.Destroy(instance);
                        // Destroy はフレーム末尾で確定する。破棄通知が owner token を返す前に
                        // close を呼ぶと ResourcesInUse が正しく返るので、実際の破棄を待つ。
                        await UniTask.WaitUntil(() => instance == null);
                    }
                    if (prefab != null) assets.Release(prefab);
                    if (texture != null) assets.Release(texture);
                    await session.CloseAsync();
                }
                var store = new ContentCacheStore(Path.Combine(_deliveryRoot, "http-cache"));
                store.MarkKnownGood(store.ValidateInstalled(_installedRoot, _manifestDigest,
                    "fixture", identity, BuildContentCoordinator.TargetName));
            });
            yield return new ExitPlayMode();

            // 二度目の Play は SampleGame の実 bootstrap に環境変数を渡す。
            // Framework test から Game asmdef を参照せず、起動済み instance の状態だけを見る。
            var names = new[] { "SAMPLEGAME_CONTENT__RUNTIMEMODE", "SAMPLEGAME_CONTENT__INSTALLEDREVISIONPATH",
                "SAMPLEGAME_CONTENT__BUILDIDENTITY", "SAMPLEGAME_CONTENT__REPRESENTATION",
                "SAMPLEGAME_CONTENT__MANIFESTSHA256" };
            var previousValues = names.Select(Environment.GetEnvironmentVariable).ToArray();
            Environment.SetEnvironmentVariable(names[0], "directory");
            Environment.SetEnvironmentVariable(names[1], _installedRoot);
            Environment.SetEnvironmentVariable(names[2], Path.GetFileName(_copy).Substring("integration-copy-".Length));
            Environment.SetEnvironmentVariable(names[3], "High");
            Environment.SetEnvironmentVariable(names[4], _manifestDigest);
            try
            {
                yield return new EnterPlayMode();
                var bootstrap = GetBootstrapState();
                for (var frame = 0; frame < 1800 &&
                    (bootstrap.session.GetValue(bootstrap.initializer) == null ||
                     bootstrap.director.GetValue(bootstrap.initializer) == null); frame++)
                    yield return null;
                Assert.That(bootstrap.session.GetValue(bootstrap.initializer), Is.Not.Null,
                    "明示 directory mode の起動 stage が登録を完了していない");
                Assert.That(bootstrap.director.GetValue(bootstrap.initializer), Is.Not.Null,
                    "directory mode で SceneDirector が生成されていない");
                Assert.That(ContentRevisionGate.TryAcquireDelete(
                    Path.GetFileName(_copy).Substring("integration-copy-".Length), BuildContentCoordinator.TargetName,
                    Path.Combine(_installedRoot, "content"), out var lease, out var rejection), Is.False);
                Assert.That(lease, Is.Null);
                Assert.That(rejection, Is.EqualTo(ContentDirectoryFailureCode.RevisionBusy));
                yield return new ExitPlayMode();
            }
            finally
            {
                for (var i = 0; i < names.Length; i++) Environment.SetEnvironmentVariable(names[i], previousValues[i]);
            }
        }

        private static (object initializer, FieldInfo session, FieldInfo director) GetBootstrapState()
        {
            // Game→Framework の依存方向を守るためテスト asmdef に Game 参照を追加しない。
            // 実アプリの静的 bootstrap instance と既存 private state を検証時だけ観測する。
            var type = Type.GetType("SampleGame.DependOnAll.AppInitializer, SampleGame.DependOnAll");
            Assert.That(type, Is.Not.Null, "実アプリの起動型が読み込まれていない");
            var initializer = type!.GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            var session = typeof(AbstractApplicationInitializer).GetField("_contentDirectorySession",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var director = typeof(AbstractApplicationInitializer).GetField("_sceneDirector",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(initializer, Is.Not.Null);
            Assert.That(session, Is.Not.Null);
            Assert.That(director, Is.Not.Null);
            return (initializer!, session!, director!);
        }

        private static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);
            using var hash = System.Security.Cryptography.SHA256.Create();
            return string.Concat(hash.ComputeHash(stream).Select(x => x.ToString("x2")));
        }

        // この fixture 専用の loopback server。停止は listener の取消で通知し、
        // 時間待ちに依存せず全 response と worker の終了を所有者が回収する。
        private sealed class LoopbackArtifactServer : IDisposable
        {
            private readonly HttpListener _listener = new();
            private readonly Task _worker;
            private int _requestCount;
            private bool _stopping;
            public Uri BaseUri { get; }
            public int RequestCount => Volatile.Read(ref _requestCount);

            public LoopbackArtifactServer(string root)
            {
                var portReservation = new TcpListener(IPAddress.Loopback, 0);
                portReservation.Start();
                var port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
                portReservation.Stop();
                BaseUri = new Uri("http://127.0.0.1:" + port + "/");
                _listener.Prefixes.Add(BaseUri.AbsoluteUri);
                _listener.Start();
                _worker = Task.Run(async () =>
                {
                    try
                    {
                        while (_listener.IsListening)
                        {
                            var context = await _listener.GetContextAsync();
                            try
                            {
                                var relative = Uri.UnescapeDataString(context.Request.Url!.AbsolutePath.TrimStart('/'));
                                var file = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                                if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                                    || !File.Exists(file))
                                {
                                    context.Response.StatusCode = 404;
                                    continue;
                                }
                                Interlocked.Increment(ref _requestCount);
                                context.Response.ContentLength64 = new FileInfo(file).Length;
                                using var stream = File.OpenRead(file);
                                await stream.CopyToAsync(context.Response.OutputStream);
                            }
                            finally { context.Response.Close(); }
                        }
                    }
                    catch (HttpListenerException) when (_stopping) { }
                    catch (ObjectDisposedException) when (_stopping) { }
                });
            }

            public void Dispose()
            {
                _stopping = true;
                _listener.Stop();
                _listener.Close();
                _worker.GetAwaiter().GetResult();
            }
        }

        private static void Copy(string source, string target)
        {
            // build 出力を丸ごと別 local path へ複写する。file 名や Unity の内部構造には依存しない。
            Directory.CreateDirectory(target);
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
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
