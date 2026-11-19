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
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.BuildContent.Distribution;
using OneStarMaker.Runtime.ContentResolution;
using Unity.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// 実機の base と上書きを、上書きを重ねた登録の口で登録して読む。base は製品の coordinator で Full build → publish → Local の供給元から
    /// temp の cache に install → 検証する。fixture を残したまま texture を編集し、Scene を別の folder へ移し、root に Scene を足して、
    /// 製品の逆引き（<see cref="ContentOverlaySelection"/>）と上書きの build で上書きを作る。Play mode で route の loader から読み、
    /// 上書きの root にある物は上書きの版（texture の pixel は編集後）で上書きを指す record、base にだけある物は base を指す record で載ること、
    /// 全てを離した後に close が上書き → base の順に解除して登録が 0 件になることを見る。SampleGame の型は使わない
    /// （build の道具がこの assembly にあるので置いている）。
    /// </summary>
    public sealed class OverlayRegistrationIntegrationTests
    {
        private const string FixtureParent = "Assets/OneStarMakerGenerated/OverlayRegistrationTests";
        private const string BaseSet = "s32-base";
        private const string OverlaySet = "s32-overlay";
        private static readonly Color32 BaseColor = new(255, 0, 0, 255);
        private static readonly Color32 EditedColor = new(0, 0, 255, 255);
        private string _folder = "";
        private string _deliveryRoot = "";
        private readonly List<string> _contentPaths = new();

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
        // Content build を 2 回（base と上書き）と PlayMode の往復を 1 回行う。build を伴う test 2 つ分の上限にする。
        [Timeout(720000)]
        public IEnumerator BaseAndOverlay_AreRegisteredInOrder_ServeTheSelectedLayer_AndCloseOverlayThenBase()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                Assert.Ignore("Overlay registration fixture requires StandaloneWindows64 Player target.");
            Assert.That(ContentLoadManager.GetContentDirectories(), Is.Empty, "開始時の登録の一覧は 0 件");
            const string modeEnv = "SAMPLEGAME_CONTENT__RUNTIMEMODE";
            var previousMode = Environment.GetEnvironmentVariable(modeEnv, EnvironmentVariableTarget.Process);
            EnsureFolder(FixtureParent);
            _folder = FixtureParent + "/" + Guid.NewGuid().ToString("N");
            EnsureFolder(_folder);
            _deliveryRoot = Path.Combine(Path.GetTempPath(), "osm-s32-overlay", Guid.NewGuid().ToString("N"));
            var artifacts = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "bs3-fixture"));
            BuildContentResult baseBuilt;
            BuildContentResult overlayBuilt;
            string published;
            string digest;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // ─── base ───
                var tex = CreateTexture("Tex", BaseColor);
                var material = CreateMaterial("TexMat", tex);
                var entry = CreateScene("Entry", "EntryRoot", material);
                var entryWhitebox = CreateScene("EntryWhitebox", "EntryWhiteboxRoot", material);
                var stay = CreateScene("Stay", "StayRoot", null);
                var moved = CreateScene("Moved", "MovedRoot", null);
                var prop = CreatePrefab("Prop", material);
                baseBuilt = new BuildContentCoordinator().Build(Plain(BaseSet, artifacts, Selection(
                    ("s32-entry", "Entry", entry, "Full"), ("s32-entry-wb", "Entry", entryWhitebox, "Whitebox"),
                    ("s32-stay", "Stay", stay, "Full"), ("s32-moved", "Moved", moved, "Full"), ("s32-prop", "Prop", prop, "Full"))));
                Assert.That(baseBuilt.IsSuccess, Is.True, baseBuilt.Summary);
                _contentPaths.Add(baseBuilt.ContentPath!);
                published = ContentTransportPublisher.Publish(baseBuilt, Path.Combine(_deliveryRoot, "published"));
                digest = ContentDeliveryFiles.Sha256(File.ReadAllBytes(Path.Combine(published, "transport.json")));

                // ─── 編集（fixture を残したまま）───
                WriteTexture(tex, EditedColor);
                AssetDatabase.CreateFolder(_folder, "Sub");
                var movedPath = _folder + "/Sub/Moved.unity";
                Assert.That(AssetDatabase.MoveAsset(moved, movedPath), Is.Empty);
                RenameRoot(movedPath, "MovedRoot", "MovedEdited");
                var added = CreateScene("Added", "AddedRoot", null);

                // ─── 上書き（製品の逆引きと上書きの build）───
                var current = Selection(
                    ("s32-entry", "Entry", entry, "Full"), ("s32-entry-wb", "Entry", entryWhitebox, "Whitebox"),
                    ("s32-stay", "Stay", stay, "Full"), ("s32-moved", "Moved", movedPath, "Full"), ("s32-prop", "Prop", prop, "Full"),
                    ("s32-added", "Added", added, "Full"));
                var selection = ContentOverlaySelection.Select(current.Plan, current.Snapshot, new[] { tex, movedPath, added });
                Assert.That(selection.IsOverlayRequired, Is.True);
                Assert.That(selection.Roots.Select(x => x.Root.StableKey).OrderBy(x => x, StringComparer.Ordinal),
                    Is.EqualTo(new[] { "s32-added", "s32-entry", "s32-entry-wb", "s32-moved", "s32-prop" }), "Stay は編集と関係が無い");
                var baseIdentity = new ContentOverlayBaseIdentity(BaseSet, baseBuilt.Identity, BuildContentCoordinator.TargetName, digest, null);
                overlayBuilt = new BuildContentCoordinator().Build(new BuildContentRequest(selection, baseIdentity, OverlaySet, artifacts, null, null));
                Assert.That(overlayBuilt.IsSuccess, Is.True, overlayBuilt.Summary);
                _contentPaths.Add(overlayBuilt.ContentPath!);
            }
            finally
            {
                // 登録の検証が source asset に依存しないよう、Play の前に fixture を消す。
                if (previous.Any(x => x.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (_folder.Length != 0) { AssetDatabase.DeleteAsset(_folder); _folder = ""; }
            }

            var install = InstallFromLocalAsync(published, digest, baseBuilt.Identity);
            yield return new WaitUntil(() => install.IsCompleted);
            var installed = install.GetAwaiter().GetResult();
            var expectedBase = new ContentIdentity(BaseSet, baseBuilt.Identity, BuildContentCoordinator.TargetName, digest);
            Assert.That(new ContentIdentity(installed.ContentSet, installed.Revision, installed.Target, digest), Is.EqualTo(expectedBase));
            var expectedOverlay = new ContentIdentity(OverlaySet, overlayBuilt.Identity, BuildContentCoordinator.TargetName, "");

            // アプリの起動は Addressables の経路にし、この test が登録する directory と取り合わない。
            Environment.SetEnvironmentVariable(modeEnv, "addressables", EnvironmentVariableTarget.Process);
            try
            {
                yield return new EnterPlayMode();
                var run = ExerciseAsync(installed, digest, overlayBuilt, expectedBase, expectedOverlay);
                yield return new WaitUntil(() => run.IsCompleted);
                run.GetAwaiter().GetResult();
                yield return new ExitPlayMode();
            }
            finally
            {
                Environment.SetEnvironmentVariable(modeEnv, previousMode, EnvironmentVariableTarget.Process);
            }
        }

        private static async Task ExerciseAsync(ContentInstallResult installed, string digest, BuildContentResult overlay,
            ContentIdentity expectedBase, ContentIdentity expectedOverlay)
        {
            var errors = new List<string>();
            void OnLog(string message, string stack, LogType type)
            {
                if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(type + ": " + message);
            }

            Application.logMessageReceived += OnLog;
            var verified = InstalledRevisionVerifier.Verify(installed.RevisionRoot, digest, installed.ContentSet, installed.Revision, installed.Target);
            var session = ContentDirectorySession.RegisterVerifiedWithOverlay(verified, overlay.ContentPath!, overlay.Identity);
            var assets = new OneStarMaker.Runtime.AssetManagement.AssetManagement();
            assets.InstallContentDirectory(session);
            var closed = false;
            try
            {
                Assert.That(ContentLoadManager.GetContentDirectories().Length, Is.EqualTo(2), "base と上書きの 2 件");
                LogMovedSceneIds(session);
                Assert.That(RegisteredDirectoryRoute.TryCreate(session, assets, out var route, out var reason), Is.True, reason);
                var loader = route!.Loader;

                var entry = await loader.LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Whitebox"));
                AssertRecord(entry.Record, expectedOverlay, ContentSourceKind.ContentOverlay, "Whitebox");
                Assert.That(ScenePixel(entry.Handle.GetRootGameObjects()), Is.EqualTo(EditedColor), "Entry の Whitebox は編集後の texture");

                var moved = await loader.LoadSceneAsync(new ContentRequest("Moved", ContentKind.Scene, "Full"));
                AssertRecord(moved.Record, expectedOverlay, ContentSourceKind.ContentOverlay, "Full");
                Assert.That(moved.Handle.GetRootGameObjects().Select(x => x.name), Does.Contain("MovedEdited"), "移動した Scene は上書きの版");

                var added = await loader.LoadSceneAsync(new ContentRequest("Added", ContentKind.Scene, "Full"));
                AssertRecord(added.Record, expectedOverlay, ContentSourceKind.ContentOverlay, "Full");
                Assert.That(added.Handle.GetRootGameObjects().Select(x => x.name), Does.Contain("AddedRoot"));

                var stay = await loader.LoadSceneAsync(new ContentRequest("Stay", ContentKind.Scene, "Full"));
                AssertRecord(stay.Record, expectedBase, ContentSourceKind.ContentDirectory, "Full");
                Assert.That(stay.Handle.GetRootGameObjects().Select(x => x.name), Does.Contain("StayRoot"));

                var prop = await loader.LoadAssetAsync<GameObject>(new ContentRequest("Prop", ContentKind.Object, "Full"), AssetOwner.Manual);
                AssertRecord(prop.Record, expectedOverlay, ContentSourceKind.ContentOverlay, "Full");
                Assert.That(Pixel(prop.Handle.Value!.GetComponent<MeshRenderer>()), Is.EqualTo(EditedColor), "Prop は編集後の texture");

                // 既存の API で全てを離し、close が上書き → base の順に解除する。
                foreach (var key in new[] { "Entry", "Moved", "Added", "Stay" })
                {
                    await assets.UnloadSceneAsync(key);
                    assets.ReleaseScene(key);
                }
                assets.Release(prop.Handle);
                await assets.CloseContentDirectoryAsync();
                closed = true;
                Assert.That(session.IsAccepting, Is.False);
                Assert.That(session.IsClosed, Is.True);
                Assert.That(ContentLoadManager.GetContentDirectories(), Is.Empty, "close の後の登録の一覧は 0 件");
                Assert.That(errors, Is.Empty, "観測窓（登録から close まで）の Error / Exception");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                if (!closed)
                {
                    try { assets.ReleaseAll(); await assets.CloseContentDirectoryAsync(); }
                    catch (Exception cleanup) { Debug.LogWarning("[S32 overlay test] cleanup after failure: " + cleanup.Message); }
                }
            }
        }

        // base と上書きの root の Moved の entry の Scene の id を比べて記録するが、合否にしない
        // （同じなら「同じ id は上書きの版」、違えば「key で上書きの entry を採る」で、どちらも契約の内）。
        private static void LogMovedSceneIds(ContentDirectorySession session)
        {
            // Stay は base の root にだけあるので、その解決が返す層が base の層。
            var baseLayer = session.ResolveEntry("Stay", BuildContentKind.Scene, "Full").Layer;
            var overlayLayer = session.OverlayLayer;
            Assert.That(baseLayer != null && !baseLayer.IsOverlay, Is.True, "Stay は base の層で解決する");
            Assert.That(overlayLayer, Is.Not.Null);
            Assert.That(baseLayer!.Index!.TryGetExact("Moved", "Full", BuildContentKind.Scene, out var baseMoved), Is.True, "base の root に Moved がある");
            Assert.That(overlayLayer!.Index!.TryGetExact("Moved", "Full", BuildContentKind.Scene, out var overlayMoved), Is.True, "上書きの root に Moved がある");
            var baseId = baseMoved!.SceneId.ToString();
            var overlayId = overlayMoved!.SceneId.ToString();
            TestContext.WriteLine("[S32 overlay] Moved entry scene id in base root: " + baseId + ", in overlay root: " + overlayId
                                  + (baseId == overlayId ? " (same)" : " (different)"));
        }

        private static void AssertRecord(ContentLoadRecord record, ContentIdentity expected, ContentSourceKind source, string representation)
        {
            Assert.That(record.Candidate.Identity, Is.EqualTo(expected), "base なら install した manifest と digest、上書きなら上書きの build の identity");
            Assert.That(record.Candidate.SourceKind, Is.EqualTo(source));
            Assert.That(record.Candidate.AcceptanceBasis, Is.EqualTo(source == ContentSourceKind.ContentOverlay
                ? ContentAcceptanceBasis.RecordedBaseMatch : ContentAcceptanceBasis.VerifiedDigest));
            Assert.That(record.Candidate.Representation, Is.EqualTo(representation));
            Assert.That(record.SourceBindingVerified, Is.True);
        }

        private static Color32 ScenePixel(GameObject[] roots)
        {
            var renderer = roots.Select(x => x.GetComponentInChildren<MeshRenderer>()).FirstOrDefault(x => x != null);
            Assert.That(renderer != null, Is.True, "Scene に renderer がある");
            return Pixel(renderer!);
        }

        private static Color32 Pixel(MeshRenderer renderer)
        {
            var texture = renderer.sharedMaterial.mainTexture as Texture2D;
            Assert.That(texture != null, Is.True, "material が texture を参照する");
            return texture!.GetPixel(0, 0);
        }

        private async Task<ContentInstallResult> InstallFromLocalAsync(string published, string digest, string revision)
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(TimeSpan.FromSeconds(60));
            var request = new ContentInstallRequest(digest, BaseSet, revision, BuildContentCoordinator.TargetName,
                Application.unityVersion, 2, long.MaxValue);
            using var local = new LocalContentArtifactSource(published);
            return await new ContentInstaller(new ContentCacheStore(Path.Combine(_deliveryRoot, "cache")))
                .InstallAsync(request, local, cancellation.Token);
        }

        // ─── fixture ───

        private string CreateTexture(string name, Color32 color)
        {
            var path = _folder + "/" + name + ".png";
            File.WriteAllBytes(Path.Combine(ProjectRoot, path), Png(color));
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            // pixel を読むため Read/Write を有効にし、圧縮で色を変えない。
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return path;
        }

        private static void WriteTexture(string path, Color32 color)
        {
            File.WriteAllBytes(Path.Combine(ProjectRoot, path), Png(color));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static byte[] Png(Color32 color)
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

        private string CreateMaterial(string name, string texturePath)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            var material = new Material(shader) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) };
            var path = _folder + "/" + name + ".mat";
            AssetDatabase.CreateAsset(material, path);
            return path;
        }

        private string CreateScene(string name, string rootName, string? materialPath)
        {
            var path = _folder + "/" + name + ".unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(rootName);
            if (materialPath != null) AddRenderer(root, materialPath);
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        private string CreatePrefab(string name, string materialPath)
        {
            var path = _folder + "/" + name + ".prefab";
            var go = new GameObject(name);
            AddRenderer(go, materialPath);
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            return path;
        }

        private static void AddRenderer(GameObject go, string materialPath)
        {
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        }

        private static void RenameRoot(string scenePath, string from, string to)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            scene.GetRootGameObjects().Single(x => x.name == from).name = to;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath)!;

        private readonly struct PlanAndSnapshot
        {
            internal PlanAndSnapshot(BuildPlan plan, BuildMaterializationSnapshot snapshot) { Plan = plan; Snapshot = snapshot; }
            internal BuildPlan Plan { get; }
            internal BuildMaterializationSnapshot Snapshot { get; }
        }

        private static PlanAndSnapshot Selection(params (string stableKey, string logicalKey, string path, string representation)[] roots)
        {
            var candidates = roots.Select(x => new BuildContentCandidate(x.stableKey, x.logicalKey,
                AssetDatabase.AssetPathToGUID(x.path), new BuildProvenance("fixture", x.path))).ToArray();
            var tags = roots.ToDictionary(x => x.stableKey,
                x => (IReadOnlyList<BuildTag>)new[] { new BuildTag("Representation", x.representation) });
            var closures = roots.Select((x, i) => new BuildDependencySnapshot(candidates[i].PhysicalKey, x.path,
                AssetDatabase.GetDependencies(x.path).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal))
                    .Select(p => new BuildDependencyEntry(AssetDatabase.AssetPathToGUID(p), p)))).ToArray();
            var snapshot = new BuildMaterializationSnapshot(candidates, tags, Array.Empty<BuildContentRequirement>(), closures);
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), candidates, Array.Empty<BuildContentExclusion>());
            return new PlanAndSnapshot(plan, snapshot);
        }

        private static BuildContentRequest Plain(string contentSet, string artifacts, PlanAndSnapshot input)
            => new(input.Plan, input.Snapshot, contentSet, artifacts, null, null, BuildContentMode.Full);

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
