#nullable enable

using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Runtime.BuildContent.Distribution;
using OneStarMaker.Runtime.ContentResolution;
using Unity.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using F = OneStarMaker.Tests.Editor.Build.OverlayFromLocalEditsFixture;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// 受け入れ条件 AC6 の R8: 担当外の source を一度も import していない Library の checkout で、部分 checkout の計画と build と Play が R4 と同じ
    /// 答えになるか。2 つの checkout にまたがるので、全 EditMode 回帰では走らない <see cref="ExplicitAttribute"/> の 2 つの test に分ける。
    /// </summary>
    /// <remarks>
    /// 手順（同じ head の 2 つの checkout）:
    /// (1) 普段の checkout で <see cref="R8_ProduceBaseAndSourcesForACleanCheckout"/> を名前で選んで走らせる。fixture を全て作り、base を build → publish し、
    ///     publish した directory、B の Scene と A の Whitebox の Scene を除いた fixture の file（.meta を含む）、識別と GUID を package に書いて、fixture を消す。
    /// (2) package を、fixture を一度も import していない新しい worktree から見える場所に置く（既定は各 checkout の <c>TestResults/s34a-r8-package</c>。
    ///     環境変数 <c>OSM_S34A_R8_PACKAGE</c> で別の場所を指せる）。
    /// (3) その worktree で <see cref="R8_PartialCheckout_PlansBuildsAndPlaysLikeR4"/> を名前で選んで走らせる。package の file を同じ path へ写して
    ///     初めて import し（B と Whitebox は最初から無い）、package の base を install して計画する。
    /// fixture の file は 2 つの checkout で同じ bytes と GUID になる（写すので）。base は同じ head の別の checkout が作った物である。
    /// </remarks>
    public sealed class OverlayFromLocalEditsR8Tests
    {
        private const string Folder = "Assets/OneStarMakerGenerated/S34aR8";
        private const string PackageEnv = "OSM_S34A_R8_PACKAGE";
        private static readonly string[] Omitted = { "B.unity", "AWhitebox.unity" };

        private static string Package => Environment.GetEnvironmentVariable(PackageEnv) is { Length: > 0 } path
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(F.ProjectRoot, "..", "TestResults", "s34a-r8-package"));

        private static string Artifacts => Path.GetFullPath(Path.Combine(F.ProjectRoot, "..", "TestResults", "bs3-fixture", "s34a-r8"));

        [Serializable]
        private sealed class PackageInfo
        {
            public string revision = "";
            public string digest = "";
            public string[] names = Array.Empty<string>();
            public string[] guids = Array.Empty<string>();
        }

        [Test, Explicit("R8 の (1)。普段の checkout で名前を選んで走らせる。")]
        [Timeout(900000)]
        public void R8_ProduceBaseAndSourcesForACleanCheckout()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False, "fixture の folder が既にある");
            if (Directory.Exists(Package)) Directory.Delete(Package, true);
            Directory.CreateDirectory(Package);
            var previous = EditorSceneManager.GetSceneManagerSetup();
            using var fixture = F.Create(Folder);
            try
            {
                var built = fixture.BuildBase(Artifacts);
                var published = ContentTransportPublisher.Publish(built, Path.Combine(Package, "published"));
                var digest = ContentDeliveryFiles.Sha256(File.ReadAllBytes(Path.Combine(published, "transport.json")));
                var source = Path.Combine(Package, "source");
                Directory.CreateDirectory(source);
                foreach (var name in F.Names.Except(Omitted))
                    foreach (var file in new[] { name, name + ".meta" })
                        File.Copy(Path.Combine(F.ProjectRoot, Folder, file), Path.Combine(source, file));
                File.WriteAllText(Path.Combine(Package, "base.json"), JsonUtility.ToJson(new PackageInfo
                {
                    revision = built.Identity, digest = digest,
                    names = F.Names.ToArray(), guids = F.Names.Select(x => fixture.Guids[x]).ToArray(),
                }, true));
                TestContext.WriteLine("[S34a R8] package: " + Package);
            }
            finally
            {
                if (previous.Any(x => x.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                AssetDatabase.DeleteAsset(Folder);
                F.DeleteIfEmpty("Assets/OneStarMakerGenerated");
            }
        }

        [UnityTest, Explicit("R8 の (3)。fixture を一度も import していない新しい worktree で名前を選んで走らせる。")]
        [Timeout(1200000)]
        public IEnumerator R8_PartialCheckout_PlansBuildsAndPlaysLikeR4()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                Assert.Ignore("The overlay fixture requires the StandaloneWindows64 Player target.");
            var info = JsonUtility.FromJson<PackageInfo>(File.ReadAllText(Path.Combine(Package, "base.json")));
            var guids = info.names.Select((x, i) => (x, info.guids[i])).ToDictionary(x => x.x, x => x.Item2);
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False, "fixture の folder が既にある（clean な checkout で走らせる）");
            foreach (var name in Omitted)
                Assert.That(AssetDatabase.GUIDToAssetPath(guids[name]), Is.Empty, name + " を一度も import していない Library で走らせる");
            var delivery = Path.Combine(Path.GetTempPath(), "osm-s34a-r8", Guid.NewGuid().ToString("N").Substring(0, 8));
            F? fixture = null;
            try
            {
                F.EnsureFolder("Assets/OneStarMakerGenerated");
                Directory.CreateDirectory(Path.Combine(F.ProjectRoot, Folder));
                foreach (var file in Directory.GetFiles(Path.Combine(Package, "source")))
                    File.Copy(file, Path.Combine(F.ProjectRoot, Folder, Path.GetFileName(file)));
                AssetDatabase.Refresh();
                fixture = F.Adopt(Folder, guids);
                foreach (var name in F.Names.Except(Omitted))
                    Assert.That(AssetDatabase.AssetPathToGUID(fixture.PathOf(name)), Is.EqualTo(guids[name]), name);

                var install = F.InstallAsync(Path.Combine(Package, "published"), info.digest, info.revision, Path.Combine(delivery, "cache"));
                yield return new WaitUntil(() => install.IsCompleted);
                var installed = install.GetAwaiter().GetResult();

                // 前半: T は base と同じ内容。source が欠けていても編集が無ければ認めを求めない。
                var reports = F.Reports(Artifacts);
                var first = fixture.Plan(installed, Artifacts);
                Assert.That(first.State, Is.EqualTo(ContentOverlayPlanState.NotRequired), "R8 前半");
                Assert.That(first.Unbuildable, Is.Empty, "R8 前半");
                Assert.That(first.Edits, Is.Empty, "R8 前半: 写した file は base と同じ bytes");

                // 後半: T の pixel を base と違う色にする。R4 と同じ答え。
                fixture.WriteTexture("T.png", F.EditedT);
                var local = fixture.Select();
                Assert.That(local.Plan.ExcludedContent.Select(x => x.Candidate.PhysicalKey), Does.Contain(guids["AWhitebox.unity"]), "A の Whitebox は除外のまま");
                var plan = fixture.Plan(installed, Artifacts);
                Assert.That(plan.OverlayRoots.Select(x => x.Root.LogicalKey), Is.EqualTo(new[] { "A" }));
                var b = plan.Unbuildable.Single();
                Assert.That(b.Root.LogicalKey + ":" + b.Reason, Is.EqualTo("B:SourceAbsent"));
                Assert.That(b.Edits, Is.EqualTo(new[] { fixture.PathOf("T.png") }));
                Assert.That(b.MissingFiles, Is.EqualTo(new[] { fixture.PathOf("B.unity") }), "記録の B の Scene の path");
                var built = new BuildContentCoordinator().Build(new BuildContentRequest(plan.Acknowledge(plan.Unbuildable.Select(x => x.Root))));
                Assert.That(built.IsSuccess, Is.True, built.Summary);
                Assert.That(F.Reports(Artifacts), Is.EqualTo(reports + 1), "build 1 回");

                const string modeEnv = "SAMPLEGAME_CONTENT__RUNTIMEMODE";
                var previousMode = Environment.GetEnvironmentVariable(modeEnv, EnvironmentVariableTarget.Process);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Environment.SetEnvironmentVariable(modeEnv, "addressables", EnvironmentVariableTarget.Process);
                try
                {
                    Assert.That(ContentLoadManager.GetContentDirectories(), Is.Empty);
                    yield return new EnterPlayMode();
                    var run = F.ReadScenesAsync(installed, built, "A", "B");
                    yield return new WaitUntil(() => run.IsCompleted);
                    var played = run.GetAwaiter().GetResult();
                    yield return new ExitPlayMode();
                    F.AssertLoaded(played["A"], ContentSourceKind.ContentOverlay, built.Identity, F.EditedT, null, "A");
                    F.AssertLoaded(played["B"], ContentSourceKind.ContentDirectory, info.revision, F.BaseT, null, "B");
                }
                finally
                {
                    Environment.SetEnvironmentVariable(modeEnv, previousMode, EnvironmentVariableTarget.Process);
                }
            }
            finally
            {
                fixture?.Dispose();
                if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
                F.DeleteIfEmpty("Assets/OneStarMakerGenerated");
                if (Directory.Exists(delivery)) Directory.Delete(delivery, true);
                if (Directory.Exists(Artifacts)) Directory.Delete(Artifacts, true);
            }
        }
    }
}
