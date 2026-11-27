#nullable enable

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.BuildContent.Distribution;
using SampleGame.DependOnAll.Editor.Build;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// 製品の経路の端から端（AC5・AC8・S2・S7）: 合成の小さい宣言を製品の coordinator で Full build・publish し、取得元の head を書き、
    /// 取得元から取得・install し、選んだ base を記録して、入口の選択（<see cref="PlayEntryCandidates.Select"/>）がその revision を返すまで。
    /// 取得で install した revision を、今ある検証（Delivery 窓の Installed offline と同じ <c>ValidateInstalled</c>）が受け付けることも見る。
    /// build を伴うので、process の自然終了を合否にしない。
    /// </summary>
    public sealed class PublishedOriginAcquireIntegrationTests
    {
        private const string FixtureParent = "Assets/OneStarMakerGenerated/OriginAcquireTests";
        private string _folder = "";
        private string _root = "";
        private string _contentPath = "";

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_folder.Length != 0) AssetDatabase.DeleteAsset(_folder);
            if (_root.Length != 0 && Directory.Exists(_root)) Directory.Delete(_root, true);
            if (_contentPath.Length != 0 && Directory.Exists(_contentPath)) Directory.Delete(_contentPath, true);
            DeleteIfEmpty(FixtureParent);
            DeleteIfEmpty("Assets/OneStarMakerGenerated");
            yield break;
        }

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator PublishedContent_IsAcquiredFromTheOrigin_AndBecomesThePlayBase()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                Assert.Ignore("The origin fixture requires the StandaloneWindows64 Player target.");
            EnsureFolder(FixtureParent);
            _folder = FixtureParent + "/" + Guid.NewGuid().ToString("N");
            EnsureFolder(_folder);
            _root = Path.Combine(Path.GetTempPath(), "osm-origin-it", Guid.NewGuid().ToString("N").Substring(0, 8));
            var originRoot = Path.Combine(_root, "transport");
            var cacheRoot = Path.Combine(_root, "cache");
            var declaration = SampleGameBuildPurposes.SpringFull;
            BuildContentResult built;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scenePath = _folder + "/Title.unity";
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Title");
                EditorSceneManager.SaveScene(scene, scenePath);
                // coordinator が受け付ける出力先は artifacts/bs2b とテスト専用の TestResults/bs3-fixture の配下だけ。
                var artifacts = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "bs3-fixture"));
                built = new BuildContentCoordinator().Build(Request(declaration, artifacts, scenePath));
                Assert.That(built.IsSuccess, Is.True, built.Summary);
                _contentPath = built.ContentPath!;
            }
            finally
            {
                if (previous.Any(x => x.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (_folder.Length != 0) { AssetDatabase.DeleteAsset(_folder); _folder = ""; }
            }

            var published = ContentTransportPublisher.Publish(built, Path.Combine(originRoot, built.Identity));
            var head = ContentOriginHeadWriter.Write(published);
            Assert.That(head.Head.revision, Is.EqualTo(built.Identity));

            var request = new ContentAcquisitionRequest(declaration.Name, VerifyPlayLauncher.Target, Application.unityVersion, 2, long.MaxValue / 4);
            var acquisition = ContentAcquisition.AcquireAsync(request, ContentOriginSettings.Origins(originRoot), new ContentCacheStore(cacheRoot),
                CancellationToken.None);
            yield return new WaitUntil(() => acquisition.IsCompleted);
            var result = acquisition.GetAwaiter().GetResult();
            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.Acquired), result.Reason);
            Assert.That(result.Revision, Is.EqualTo(built.Identity));
            Assert.That(result.ManifestSha256, Is.EqualTo(head.Head.manifestSha256));
            Assert.That(result.BytesOriginId, Is.EqualTo(ContentOriginSettings.LocalPublishOriginId));

            // AC5: 取得の install は、今ある install と同じ形（Delivery 窓の Installed offline と同じ検証が受け付ける）。
            var store = new ContentCacheStore(cacheRoot);
            var validated = store.ValidateInstalled(result.Install!.RevisionRoot, result.ManifestSha256!, declaration.Name, built.Identity,
                VerifyPlayLauncher.Target);
            Assert.That(validated.RevisionRoot, Is.EqualTo(result.Install.RevisionRoot));

            var record = Path.Combine(_root, "selection.json");
            ContentBaseSelection.Write(record, new ContentBaseSelectionEntry
            {
                cacheRoot = cacheRoot, contentSet = declaration.Name, target = VerifyPlayLauncher.Target, revision = result.Revision!,
                manifestSha256 = result.ManifestSha256!, targetOrigin = result.TargetOriginId ?? "", bytesOrigin = result.BytesOriginId ?? "",
                digestAuthority = result.DigestAuthority?.ToString() ?? "", selectedUtc = DateTime.UtcNow.ToString("O"),
            });
            var selection = PlayEntryCandidates.Select(cacheRoot, declaration, VerifyPlayLauncher.Target, SampleGameBuildPurposes.FirstSceneOf,
                ContentBaseSelection.Read(record, cacheRoot, declaration.Name, VerifyPlayLauncher.Target));
            Assert.That(selection.Candidate, Is.Not.Null, selection.Failure);
            Assert.That(selection.Candidate!.Revision, Is.EqualTo(built.Identity));
        }

        private static BuildContentRequest Request(BuildPurposeDeclaration declaration, string artifacts, string scenePath)
        {
            var candidate = new BuildContentCandidate("title", "Title", AssetDatabase.AssetPathToGUID(scenePath), new BuildProvenance("fixture", scenePath));
            var tags = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<BuildTag>>
            {
                ["title"] = new[] { new BuildTag("Representation", "Full") },
            };
            var closure = new BuildDependencySnapshot(candidate.PhysicalKey, scenePath,
                AssetDatabase.GetDependencies(scenePath).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal))
                    .Select(p => new BuildDependencyEntry(AssetDatabase.AssetPathToGUID(p), p)));
            var snapshot = new BuildMaterializationSnapshot(new[] { candidate }, tags, Array.Empty<BuildContentRequirement>(), new[] { closure });
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { candidate }, Array.Empty<BuildContentExclusion>());
            return new BuildContentRequest(plan, snapshot, declaration.Name, artifacts, declaration, null, BuildContentMode.Full);
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
