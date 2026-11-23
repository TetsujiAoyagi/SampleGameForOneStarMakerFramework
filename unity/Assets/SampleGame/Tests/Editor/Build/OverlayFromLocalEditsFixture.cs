#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.BuildContent.Distribution;
using OneStarMaker.Runtime.ContentResolution;
using OneStarMaker.Runtime.SceneSystem;
using Unity.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// 手元の編集から上書きを決めて作る実機 test の fixture（受け入れ条件 AC6）。合成の <see cref="SceneResourceMap"/>（memory の上だけ）で、
    /// Scene <c>A</c>（Full と Whitebox）・<c>B</c> が texture <c>T</c> を Light の cookie として直接参照し、Scene <c>C</c> が material <c>M</c>
    /// （texture <c>U</c> を参照）を使う。合成で足す root は prefab <c>Z</c>。宣言は A・B・C を root にして Full だけを選ぶ。
    /// 製品の経路（欠けを許す materialize → 宣言の選択 → 合成 → 計画 → build → publish → install → 登録）を通す。SampleGame の型は使わない。
    /// </summary>
    internal sealed class OverlayFromLocalEditsFixture : IDisposable
    {
        internal const string BaseSet = "s34a-base";
        internal const string OverlaySet = "s34a-overlay";
        internal const string DeclarationName = "s34a-decl";
        internal static readonly Color32 BaseT = new(255, 0, 0, 255);
        internal static readonly Color32 EditedT = new(0, 0, 255, 255);
        internal static readonly Color32 BaseU = new(0, 255, 0, 255);
        internal static readonly Color32 EditedU = new(255, 255, 0, 255);
        internal static readonly string[] Names = { "T.png", "U.png", "M.mat", "A.unity", "AWhitebox.unity", "B.unity", "C.unity", "Z.prefab" };
        private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);
        private readonly List<UnityEngine.Object> _objects = new();
        private readonly string _aside;

        private readonly Dictionary<string, string> _guids;
        // 真なら、map に root D を足し、宣言の root にも D を足す（base の build の後に手元で足した root。受け入れ条件 AC8）。
        private bool _withD;

        private OverlayFromLocalEditsFixture(string folder, IReadOnlyDictionary<string, string> guids)
        {
            Folder = folder;
            Guids = _guids = guids.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            foreach (var name in Names) _paths[name] = folder + "/" + name;
            _aside = Path.Combine(ProjectRoot, "..", "TestResults", "s34a-aside", Guid.NewGuid().ToString("N").Substring(0, 8));
        }

        internal string Folder { get; }
        /// <summary>file 名 → GUID（map の参照と、退かして戻した後の照合に使う）。</summary>
        internal IReadOnlyDictionary<string, string> Guids { get; }
        internal string PathOf(string name) => _paths[name];
        internal static string ProjectRoot => Path.GetDirectoryName(Application.dataPath)!;

        internal static BuildPurposeDeclaration Declaration { get; } = new(DeclarationName, new[] { "A", "B", "C" },
            new BuildRequest(Array.Empty<BuildTag>()), new BuildRepresentationSelection(new[] { "Full" }, BuildRepresentationMode.First));

        /// <summary>同じ名前の宣言に root D を足したもの（base の記録には D が無い）。</summary>
        internal static BuildPurposeDeclaration DeclarationWithD { get; } = new(DeclarationName, new[] { "A", "B", "C", "D" },
            new BuildRequest(Array.Empty<BuildTag>()), new BuildRepresentationSelection(new[] { "Full" }, BuildRepresentationMode.First));

        private BuildPurposeDeclaration CurrentDeclaration => _withD ? DeclarationWithD : Declaration;

        /// <summary>
        /// base の build の後に、T を cookie に参照する Scene D を作り、map と宣言の root に足す。base の閉包の記録に D は無い。
        /// </summary>
        internal void AddRootD()
        {
            var path = Folder + "/D.unity";
            Scene(path, "DRoot", PathOf("T.png"), null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _paths["D.unity"] = path;
            _guids["D.unity"] = AssetDatabase.AssetPathToGUID(path);
            _withD = true;
        }

        /// <summary>全ての file を作る（base の build の前の状態）。</summary>
        internal static OverlayFromLocalEditsFixture Create(string folder)
        {
            EnsureFolder(folder);
            Texture(folder + "/T.png", BaseT);
            Texture(folder + "/U.png", BaseU);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            AssetDatabase.CreateAsset(new Material(shader) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/U.png") }, folder + "/M.mat");
            foreach (var scene in new[] { "A", "AWhitebox", "B" }) Scene(folder + "/" + scene + ".unity", scene + "Root", folder + "/T.png", null);
            Scene(folder + "/C.unity", "CRoot", null, folder + "/M.mat");
            var z = new GameObject("Z");
            PrefabUtility.SaveAsPrefabAsset(z, folder + "/Z.prefab");
            UnityEngine.Object.DestroyImmediate(z);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.SaveAssets();
            return new OverlayFromLocalEditsFixture(folder, Names.ToDictionary(x => x, x => AssetDatabase.AssetPathToGUID(folder + "/" + x)));
        }

        /// <summary>別の checkout が作った file を写した folder を使う（R8）。GUID は写した .meta が持つ。</summary>
        internal static OverlayFromLocalEditsFixture Adopt(string folder, IReadOnlyDictionary<string, string> guids) => new(folder, guids);

        // ─── 手元の選択（製品の経路）───

        internal sealed class Local
        {
            internal Local(BuildPlan plan, BuildMaterializationSnapshot snapshot, IReadOnlyList<BuildPurposeRequirement> requirements)
            { Plan = plan; Snapshot = snapshot; Requirements = requirements; }
            public BuildPlan Plan { get; }
            public BuildMaterializationSnapshot Snapshot { get; }
            public IReadOnlyList<BuildPurposeRequirement> Requirements { get; }
        }

        /// <summary>欠けを許す materialize → 宣言の選択 → 合成（Z を足し、欠けの記録を引き継ぐ）。</summary>
        internal Local Select()
        {
            var map = Map();
            var materialized = new SceneResourceContentMaterializer().MaterializeAllowingAbsentSources(map);
            Assert.That(materialized.Snapshot, Is.Not.Null, string.Join("; ", materialized.Issues.Select(x => x.Code + ":" + x.SubjectKey + ":" + x.Path)));
            var snapshot = materialized.Snapshot!;
            var resolution = new BuildPurposeResolver().Resolve(CurrentDeclaration, snapshot.Graph, snapshot.Candidates, snapshot.TagProviders, new NoNodeTags());
            Assert.That(resolution.Selection.Plan, Is.Not.Null, string.Join("; ", resolution.Selection.Issues.Select(x => x.Code + ":" + x.SubjectKey)));
            var plan = resolution.Selection.Plan!;

            // 合成: host が選択の後に root を足す（bootstrap の合成と同じ形）。欠けの記録を落とさない。
            var zGuid = Guids["Z.prefab"];
            var issues = new List<BuildMaterializationIssue>();
            var zClosure = new AssetDependencySnapshotBuilder(new UnityAssetDatabaseGateway()).Build(zGuid, "Z", issues);
            Assert.That(issues, Is.Empty);
            var z = new BuildContentCandidate("s34a-z/Full", "Z", zGuid, new BuildProvenance("s34a-composer", "Z"));
            var tags = snapshot.Candidates.ToDictionary(x => x.StableKey,
                x => (IReadOnlyList<BuildTag>)snapshot.TagProviders.SelectMany(p => p.GetTags(x)).ToArray(), StringComparer.Ordinal);
            tags[z.StableKey] = new[] { new BuildTag("Representation", "Full") };
            var composed = new BuildMaterializationSnapshot(snapshot.Candidates.Concat(new[] { z }), tags,
                snapshot.Requirements.Concat(new[] { new BuildContentRequirement("Z", BuildContentCardinality.ExactlyOne) }),
                snapshot.Dependencies.Concat(new[] { zClosure! }), snapshot.Graph, snapshot.Absences);
            var composedPlan = new BuildPlan(plan.Request, plan.SelectedContent.Concat(new[] { z }), plan.ExcludedContent);
            return new Local(composedPlan, composed, resolution.Requirements);
        }

        private SceneResourceMap Map()
        {
            SceneResource Resource(string identity, params (string Variant, string Name)[] payloads)
            {
                var resource = ScriptableObject.CreateInstance<SceneResource>();
                _objects.Add(resource);
                resource.Identity = identity;
                Set(resource, "_sceneAssetDescription", new SceneAssetDescription(identity, LoadType.OnDemand,
                    payloads.Select(x => new AssetPayload(x.Variant, new AssetReference(Guids[x.Name]))).ToList()));
                return resource;
            }
            var map = ScriptableObject.CreateInstance<SceneResourceMap>();
            _objects.Add(map);
            var resources = new List<SceneResource>
            {
                Resource("A", ("", "A.unity"), ("Whitebox", "AWhitebox.unity")),
                Resource("B", ("", "B.unity")),
                Resource("C", ("", "C.unity")),
            };
            if (_withD) resources.Add(Resource("D", ("", "D.unity")));
            Set(map, "_sceneResources", resources);
            return map;
        }

        /// <summary>
        /// 最後の <see cref="BuildBase"/> が build に渡した選択（plan と snapshot）。publish した閉包の記録を、build に渡した閉包と
        /// 生の値で比べるために持つ（受け入れ条件 AC7）。
        /// </summary>
        internal Local? BaseSelection { get; private set; }

        internal BuildContentResult BuildBase(string artifacts)
        {
            var local = BaseSelection = Select();
            Assert.That(local.Snapshot.Absences, Is.Empty, "base は全ての source がある checkout で作る");
            var result = new BuildContentCoordinator().Build(new BuildContentRequest(local.Plan, local.Snapshot, BaseSet, artifacts,
                Declaration, local.Requirements, BuildContentMode.Full));
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            return result;
        }

        internal ContentOverlayPlan Plan(ContentInstallResult installed, string artifacts, params string[] supplied)
        {
            var local = Select();
            return ContentOverlayPlanner.Plan(installed, local.Plan, local.Snapshot, supplied, OverlaySet, artifacts, CurrentDeclaration, local.Requirements);
        }

        // ─── 編集の操作 ───

        internal void WriteTexture(string name, Color32 color)
        {
            File.WriteAllBytes(Path.Combine(ProjectRoot, PathOf(name)), Png(color));
            AssetDatabase.ImportAsset(PathOf(name), ImportAssetOptions.ForceUpdate);
        }

        /// <summary>file を .meta と一緒に Assets の外へ退かす。</summary>
        internal void Aside(string name)
        {
            Directory.CreateDirectory(_aside);
            var full = Path.Combine(ProjectRoot, PathOf(name));
            File.Move(full, Path.Combine(_aside, name));
            File.Move(full + ".meta", Path.Combine(_aside, name + ".meta"));
            AssetDatabase.Refresh();
        }

        /// <summary>退かした file を戻し、GUID が base と同じことを確かめる（未測定 (iv)）。</summary>
        internal void Back(string name)
        {
            var full = Path.Combine(ProjectRoot, PathOf(name));
            File.Move(Path.Combine(_aside, name), full);
            File.Move(Path.Combine(_aside, name + ".meta"), full + ".meta");
            AssetDatabase.Refresh();
            Assert.That(AssetDatabase.AssetPathToGUID(PathOf(name)), Is.EqualTo(Guids[name]), name + " の GUID は戻した後も同じ");
        }

        /// <summary>asset を別の folder へ移す（GUID は同じ）。</summary>
        internal void Move(string name, string subfolder)
        {
            var folder = Folder + "/" + subfolder;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Folder, subfolder);
            var to = folder + "/" + name;
            Assert.That(AssetDatabase.MoveAsset(PathOf(name), to), Is.Empty);
            _paths[name] = to;
            Assert.That(AssetDatabase.AssetPathToGUID(to), Is.EqualTo(Guids[name]));
        }

        /// <summary>base に無い新しい texture を作り、Scene の新しい Light の cookie として参照に足す。</summary>
        internal string AddTextureToScene(string sceneName, string textureName, Color32 color)
        {
            var path = Folder + "/" + textureName;
            Texture(path, color);
            _paths[textureName] = path;
            var scene = EditorSceneManager.OpenScene(PathOf(sceneName), OpenSceneMode.Single);
            var go = new GameObject(textureName + "Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.cookie = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return path;
        }

        /// <summary>fixture の folder の全ての file（.meta を含む）の SHA-256。key は project からの相対 path。</summary>
        internal SortedDictionary<string, string> SourceHashes() => Hashes(Path.Combine(ProjectRoot, Folder), ProjectRoot);

        internal static SortedDictionary<string, string> Hashes(string directory, string relativeTo)
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (!Directory.Exists(directory)) return result;
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                result[Path.GetRelativePath(relativeTo, file).Replace('\\', '/')] = ContentDeliveryFiles.Sha256(File.ReadAllBytes(file));
            return result;
        }

        public void Dispose()
        {
            foreach (var value in _objects) if (value != null) UnityEngine.Object.DestroyImmediate(value);
            _objects.Clear();
            var aside = Path.GetFullPath(_aside);
            if (Directory.Exists(aside)) Directory.Delete(aside, true);
        }

        // ─── install と Play ───

        internal static async Task<ContentInstallResult> InstallAsync(string published, string digest, string revision, string cacheRoot)
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var request = new ContentInstallRequest(digest, BaseSet, revision, BuildContentCoordinator.TargetName, Application.unityVersion, 2, long.MaxValue);
            using var local = new LocalContentArtifactSource(published);
            return await new ContentInstaller(new ContentCacheStore(cacheRoot)).InstallAsync(request, local, cancellation.Token);
        }

        /// <summary>
        /// 同じ公開 directory を loopback の HTTP の供給元から install する（受け入れ条件 AC7 の「Local と HTTP から install した manifest が
        /// 同じ bytes」）。server は install の後に止める。
        /// </summary>
        internal static async Task<ContentInstallResult> InstallFromHttpAsync(string published, string digest, string revision, string cacheRoot)
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var request = new ContentInstallRequest(digest, BaseSet, revision, BuildContentCoordinator.TargetName, Application.unityVersion, 2, long.MaxValue);
            ContentInstallResult? installed = null;
            var server = new LoopbackArtifactServer(published, cancellation.Token);
            await ContentInstallTestOperation.RunWithShutdownAsync(async () =>
            {
                using var remote = new HttpContentArtifactSource(server.BaseUri);
                installed = await new ContentInstaller(new ContentCacheStore(cacheRoot)).InstallAsync(request, remote, cancellation.Token);
                Assert.That(server.RequestCount, Is.GreaterThan(1), "HTTP の供給元から取った");
            }, server.StopAsync);
            return installed!;
        }

        /// <summary>Play の中で読んだ 1 つの Scene: どの層から載ったか、Light の cookie と renderer の texture の pixel。</summary>
        internal sealed class Loaded
        {
            public ContentSourceKind Source { get; set; }
            public string Revision { get; set; } = "";
            public Color32? Cookie { get; set; }
            public Color32? MainTexture { get; set; }
        }

        /// <summary>Play mode の中で base と上書きを登録し（S3-2 の口）、Scene を読んで、全てを離して閉じる。</summary>
        internal static async Task<Dictionary<string, Loaded>> ReadScenesAsync(ContentInstallResult installed, BuildContentResult overlay, params string[] keys)
        {
            var verified = InstalledRevisionVerifier.Verify(installed.RevisionRoot, installed.ManifestSha256, installed.ContentSet, installed.Revision, installed.Target);
            var session = ContentDirectorySession.RegisterVerifiedWithOverlay(verified, overlay.ContentPath!, overlay.Identity);
            var assets = new OneStarMaker.Runtime.AssetManagement.AssetManagement();
            assets.InstallContentDirectory(session);
            var result = new Dictionary<string, Loaded>(StringComparer.Ordinal);
            var closed = false;
            try
            {
                Assert.That(RegisteredDirectoryRoute.TryCreate(session, assets, out var route, out var reason), Is.True, reason);
                foreach (var key in keys)
                {
                    var loaded = await route!.Loader.LoadSceneAsync(new ContentRequest(key, ContentKind.Scene, "Full"));
                    var roots = loaded.Handle.GetRootGameObjects();
                    var light = roots.Select(x => x.GetComponentInChildren<Light>()).FirstOrDefault(x => x != null && x.cookie != null);
                    var renderer = roots.Select(x => x.GetComponentInChildren<MeshRenderer>()).FirstOrDefault(x => x != null);
                    var cookie = light != null ? light.cookie as Texture2D : null;
                    var main = renderer != null ? renderer.sharedMaterial.mainTexture as Texture2D : null;
                    result[key] = new Loaded
                    {
                        Source = loaded.Record.Candidate.SourceKind, Revision = loaded.Record.Candidate.Identity.Revision,
                        Cookie = cookie != null ? cookie.GetPixel(0, 0) : null, MainTexture = main != null ? main.GetPixel(0, 0) : null,
                    };
                }
                foreach (var key in keys) { await assets.UnloadSceneAsync(key); assets.ReleaseScene(key); }
                await assets.CloseContentDirectoryAsync();
                closed = true;
                Assert.That(ContentLoadManager.GetContentDirectories(), Is.Empty, "close の後の登録の一覧は 0 件");
            }
            finally
            {
                if (!closed)
                {
                    try { assets.ReleaseAll(); await assets.CloseContentDirectoryAsync(); }
                    catch (Exception cleanup) { Debug.LogWarning("[S34a overlay test] cleanup after failure: " + cleanup.Message); }
                }
            }
            return result;
        }

        /// <summary>
        /// Play mode の中で base と上書きを登録し、<paramref name="missingKey"/> の要求の失敗と、<paramref name="presentKey"/> の読みを返す。
        /// 失敗は例外の型と、要求時解決の失敗の code（<see cref="ContentResolutionException"/> の時だけ）。
        /// </summary>
        internal static async Task<(Exception? Failure, Loaded Present)> RequestMissingAsync(ContentInstallResult installed, BuildContentResult overlay,
            string missingKey, string presentKey)
        {
            var verified = InstalledRevisionVerifier.Verify(installed.RevisionRoot, installed.ManifestSha256, installed.ContentSet, installed.Revision, installed.Target);
            var session = ContentDirectorySession.RegisterVerifiedWithOverlay(verified, overlay.ContentPath!, overlay.Identity);
            var assets = new OneStarMaker.Runtime.AssetManagement.AssetManagement();
            assets.InstallContentDirectory(session);
            Exception? failure = null;
            Loaded present;
            var closed = false;
            try
            {
                Assert.That(RegisteredDirectoryRoute.TryCreate(session, assets, out var route, out var reason), Is.True, reason);
                try { await route!.Loader.LoadSceneAsync(new ContentRequest(missingKey, ContentKind.Scene, "Full")); }
                catch (Exception ex) { failure = ex; }
                var loaded = await route!.Loader.LoadSceneAsync(new ContentRequest(presentKey, ContentKind.Scene, "Full"));
                var light = loaded.Handle.GetRootGameObjects().Select(x => x.GetComponentInChildren<Light>()).FirstOrDefault(x => x != null && x.cookie != null);
                var cookie = light != null ? light.cookie as Texture2D : null;
                present = new Loaded
                {
                    Source = loaded.Record.Candidate.SourceKind, Revision = loaded.Record.Candidate.Identity.Revision,
                    Cookie = cookie != null ? cookie.GetPixel(0, 0) : null,
                };
                await assets.UnloadSceneAsync(presentKey);
                assets.ReleaseScene(presentKey);
                await assets.CloseContentDirectoryAsync();
                closed = true;
            }
            finally
            {
                if (!closed)
                {
                    try { assets.ReleaseAll(); await assets.CloseContentDirectoryAsync(); }
                    catch (Exception cleanup) { Debug.LogWarning("[S34a overlay test] cleanup after failure: " + cleanup.Message); }
                }
            }
            return (failure, present);
        }

        internal static void AssertLoaded(Loaded loaded, ContentSourceKind source, string revision, Color32? cookie, Color32? main, string label)
        {
            Assert.That(loaded.Source, Is.EqualTo(source), label + " の層");
            Assert.That(loaded.Revision, Is.EqualTo(revision), label + " の record");
            if (cookie != null) Assert.That(loaded.Cookie, Is.EqualTo(cookie), label + " の T（Light の cookie）");
            if (main != null) Assert.That(loaded.MainTexture, Is.EqualTo(main), label + " の U（material の texture）");
        }

        /// <summary>成果物の root の reports/ の下の directory の数（「build 0 回」の読み方）。</summary>
        internal static int Reports(string artifacts) =>
            Directory.Exists(Path.Combine(artifacts, "reports")) ? Directory.GetDirectories(Path.Combine(artifacts, "reports")).Length : 0;

        // ─── asset ───

        private static void Texture(string path, Color32 color)
        {
            File.WriteAllBytes(Path.Combine(ProjectRoot, path), Png(color));
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            // pixel を読むため Read/Write を有効にし、圧縮と mipmap で色を変えない。
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        internal static byte[] Png(Color32 color)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(Enumerable.Repeat(color, 16).ToArray());
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        // T は Light の cookie として Scene から直接参照する（material を挟まない）。
        private static void Scene(string path, string rootName, string? cookie, string? material)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(rootName);
            if (cookie != null)
            {
                var light = root.AddComponent<Light>();
                light.type = LightType.Spot;
                light.cookie = AssetDatabase.LoadAssetAtPath<Texture2D>(cookie);
            }
            if (material != null)
            {
                root.AddComponent<MeshFilter>();
                root.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(material);
            }
            EditorSceneManager.SaveScene(scene, path);
        }

        internal static void EnsureFolder(string path)
        {
            var parent = "Assets";
            foreach (var segment in path.Substring("Assets/".Length).Split('/'))
            {
                var next = parent + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, segment);
                parent = next;
            }
        }

        internal static void DeleteIfEmpty(string assetFolder)
        {
            if (!AssetDatabase.IsValidFolder(assetFolder)) return;
            var absolute = Path.Combine(Application.dataPath, assetFolder.Substring("Assets/".Length));
            if (Directory.GetFileSystemEntries(absolute).Length == 0) AssetDatabase.DeleteAsset(assetFolder);
        }

        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

        // 宣言の filter は空なので node の tag は要らない（表現の dimension は resolver が足す）。
        private sealed class NoNodeTags : IBuildNodeTagProvider
        {
            public string StableProviderKey => "s34a.no-node-tags";
            public BuildTagSchema Schema { get; } = new(Array.Empty<KeyValuePair<string, IEnumerable<string>>>());
            public IReadOnlyList<BuildTag> GetTags(BuildGraphNode node, IReadOnlyDictionary<string, BuildGraphNode> graph) => Array.Empty<BuildTag>();
        }
    }
}
