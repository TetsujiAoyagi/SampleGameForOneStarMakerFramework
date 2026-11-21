#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
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
    /// 手元の編集から上書きを決めて作る縦断（受け入れ条件 AC6 の R0〜R7）。fixture を一度 import した Library で、file を .meta と一緒に
    /// Assets の外へ退かして部分 checkout を作る。base は製品の経路で build → publish → install し、上書きは計画から build して、
    /// Play mode で S3-2 の登録の口（RegisterVerifiedWithOverlay）を直接呼んで読む。担当外の source を最初から持たない checkout は
    /// <see cref="OverlayFromLocalEditsR8Tests"/>（R8）が別の checkout で確かめる。
    /// </summary>
    public sealed class OverlayFromLocalEditsIntegrationTests
    {
        private const string FixtureParent = "Assets/OneStarMakerGenerated/S34a";
        private string _folder = "";
        private string _delivery = "";
        private string _artifacts = "";
        private F? _fixture;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            _fixture?.Dispose();
            if (_folder.Length != 0) AssetDatabase.DeleteAsset(_folder);
            if (_delivery.Length != 0 && Directory.Exists(_delivery)) Directory.Delete(_delivery, true);
            if (_artifacts.Length != 0 && Directory.Exists(_artifacts)) Directory.Delete(_artifacts, true);
            F.DeleteIfEmpty(FixtureParent);
            F.DeleteIfEmpty("Assets/OneStarMakerGenerated");
        }

        private static string[] OverlayKeys(ContentOverlayPlan plan) => plan.OverlayRoots.Select(x => x.Root.LogicalKey).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        [UnityTest]
        // Content build を 5 回（base 1、上書き 4）と Play mode の往復を 2 回行う。build を伴う test の上限を build の数だけ足す。
        [Timeout(2700000)]
        public IEnumerator R0ToR7_PlanBuildAndPlayFromLocalEdits_InAPartialCheckout()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                Assert.Ignore("The overlay fixture requires the StandaloneWindows64 Player target.");
            Assert.That(ContentLoadManager.GetContentDirectories(), Is.Empty, "開始時の登録の一覧は 0 件");
            var id = Guid.NewGuid().ToString("N").Substring(0, 8);
            F.EnsureFolder(FixtureParent);
            _folder = FixtureParent + "/" + id;
            _delivery = Path.Combine(Path.GetTempPath(), "osm-s34a", id);
            _artifacts = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "bs3-fixture", "s34a-" + id));
            var previous = EditorSceneManager.GetSceneManagerSetup();
            var fixture = _fixture = F.Create(_folder);

            // ─── base ───
            var baseBuilt = fixture.BuildBase(_artifacts);
            var published = ContentTransportPublisher.Publish(baseBuilt, Path.Combine(_delivery, "published"));
            var digest = ContentDeliveryFiles.Sha256(File.ReadAllBytes(Path.Combine(published, "transport.json")));
            var install = F.InstallAsync(published, digest, baseBuilt.Identity, Path.Combine(_delivery, "cache"));
            yield return new WaitUntil(() => install.IsCompleted);
            var installed = install.GetAwaiter().GetResult();
            var manifest = JsonUtility.FromJson<ContentTransportManifest>(File.ReadAllText(Path.Combine(installed.RevisionRoot, "transport.json")));
            Assert.That(manifest.sourceClosure.version, Is.EqualTo(1), "base は閉包の記録を持つ");

            // 各回の前後で、その回が変える file を除く fixture の source と installed の revision が同じ。
            SortedDictionary<string, string>? sources = null, revision = null;
            void Begin() { sources = fixture.SourceHashes(); revision = F.Hashes(installed.RevisionRoot, installed.RevisionRoot); }
            void End(string round, params string[] changed)
            {
                var touched = new HashSet<string>(changed.SelectMany(x => new[] { x, x + ".meta" }), StringComparer.Ordinal);
                var after = fixture.SourceHashes();
                foreach (var key in sources!.Keys.Union(after.Keys).Where(x => !touched.Contains(x)))
                    Assert.That(after.TryGetValue(key, out var a) && sources.TryGetValue(key, out var b) && a == b, Is.True, round + ": " + key + " が変わった");
                Assert.That(F.Hashes(installed.RevisionRoot, installed.RevisionRoot), Is.EqualTo(revision), round + ": installed の revision が変わった");
            }
            var t = fixture.PathOf("T.png");

            // ─── R0: 編集なし ───
            Begin();
            var reports = F.Reports(_artifacts);
            var r0 = fixture.Plan(installed, _artifacts);
            Assert.That(r0.State, Is.EqualTo(ContentOverlayPlanState.NotRequired), "R0");
            Assert.That(r0.Unbuildable, Is.Empty, "R0");
            Assert.That(r0.Edits, Is.Empty, "R0");
            Assert.That(F.Reports(_artifacts), Is.EqualTo(reports), "R0: build 0 回");
            End("R0");

            // ─── R1: T の pixel を書き換える ───
            Begin();
            fixture.WriteTexture("T.png", F.EditedT);
            var r1 = fixture.Plan(installed, _artifacts);
            Assert.That(r1.State, Is.EqualTo(ContentOverlayPlanState.BuildRequired), "R1");
            Assert.That(OverlayKeys(r1), Is.EqualTo(new[] { "A", "B" }), "R1: 上書きに A（Full）と B。合成した Z と C は出ない");
            Assert.That(r1.OverlayRoots.All(x => x.Edits.SequenceEqual(new[] { t })), Is.True);
            Assert.That(r1.Unbuildable, Is.Empty);
            var r1Built = new BuildContentCoordinator().Build(new BuildContentRequest(r1));
            Assert.That(r1Built.IsSuccess, Is.True, r1Built.Summary);
            Assert.That(F.Reports(_artifacts), Is.EqualTo(reports + 1), "R1: build 1 回");
            End("R1", t);
            yield return Play(installed, r1Built);
            AssertRound(("A", ContentSourceKind.ContentOverlay, r1Built.Identity, F.EditedT, null),
                ("B", ContentSourceKind.ContentOverlay, r1Built.Identity, F.EditedT, null),
                ("C", ContentSourceKind.ContentDirectory, baseBuilt.Identity, null, F.BaseU));

            // ─── R2: そのまま ───
            Begin();
            reports = F.Reports(_artifacts);
            var r2 = fixture.Plan(installed, _artifacts);
            Assert.That(r2.Key, Is.EqualTo(r1.Key), "R2");
            Assert.That(r2.GetReusableResult()?.Identity, Is.EqualTo(r1Built.Identity), "R2: 前回の上書き");
            Assert.That(F.Reports(_artifacts), Is.EqualTo(reports), "R2: build 0 回");
            End("R2");

            // ─── R3: A と B を他の供給元に渡す ───
            Begin();
            var r3 = fixture.Plan(installed, _artifacts, "A", "B");
            Assert.That(r3.State, Is.EqualTo(ContentOverlayPlanState.NotRequired), "R3");
            Assert.That(r3.Supplied.Select(x => x.Root.LogicalKey), Is.EqualTo(new[] { "A", "B" }), "R3");
            Assert.That(F.Reports(_artifacts), Is.EqualTo(reports), "R3: build 0 回");
            End("R3");

            // ─── R4: B の Scene と A の Whitebox を退かす ───
            Begin();
            fixture.Aside("B.unity");
            fixture.Aside("AWhitebox.unity");
            var local = fixture.Select();
            Assert.That(local.Plan.ExcludedContent.Select(x => x.Candidate.PhysicalKey), Does.Contain(fixture.Guids["AWhitebox.unity"]), "R4: A の Whitebox は除外のまま");
            var r4 = fixture.Plan(installed, _artifacts);
            Assert.That(OverlayKeys(r4), Is.EqualTo(new[] { "A" }), "R4");
            var b = r4.Unbuildable.Single();
            Assert.That(b.Root.LogicalKey + ":" + b.Reason, Is.EqualTo("B:SourceAbsent"), "R4");
            Assert.That(b.Edits, Is.EqualTo(new[] { t }), "R4");
            Assert.That(b.MissingFiles, Is.EqualTo(new[] { fixture.PathOf("B.unity") }), "R4");
            Assert.That(r4.Unbuildable.Concat<object>(r4.OverlayRoots).Count(), Is.EqualTo(2), "R4: Whitebox はどの分類にも出ない");
            Assert.Throws<InvalidOperationException>(() => _ = new BuildContentRequest(r4), "R4: 認めない request");
            Assert.That(F.Reports(_artifacts), Is.EqualTo(reports), "R4: 認めない request は report を作らない");
            var r4Built = new BuildContentCoordinator().Build(new BuildContentRequest(r4.Acknowledge(r4.Unbuildable.Select(x => x.Root))));
            Assert.That(r4Built.IsSuccess, Is.True, r4Built.Summary);
            Assert.That(F.Reports(_artifacts), Is.EqualTo(reports + 1), "R4: build 1 回");
            End("R4", fixture.PathOf("B.unity"), fixture.PathOf("AWhitebox.unity"));
            yield return Play(installed, r4Built);
            // B の T が編集後の色なら、作り直せない root の直接参照が base の build の時の版で出る前提が崩れる（NO-GO (ii)）。
            AssertRound(("A", ContentSourceKind.ContentOverlay, r4Built.Identity, F.EditedT, null),
                ("B", ContentSourceKind.ContentDirectory, baseBuilt.Identity, F.BaseT, null),
                ("C", ContentSourceKind.ContentDirectory, baseBuilt.Identity, null, F.BaseU));

            // ─── R5: そのまま。認めずに / 認めて前回の上書きを受け取る ───
            Begin();
            reports = F.Reports(_artifacts);
            var r5 = fixture.Plan(installed, _artifacts);
            Assert.Throws<InvalidOperationException>(() => r5.GetReusableResult(), "R5: 認めない受け取り");
            Assert.That(r5.Acknowledge(r5.Unbuildable.Select(x => x.Root)).GetReusableResult()?.Identity, Is.EqualTo(r4Built.Identity), "R5");
            Assert.That(F.Reports(_artifacts), Is.EqualTo(reports), "R5: build 0 回");
            End("R5");

            // ─── R6: B と Whitebox を戻す。M を退かし、U の pixel を書き換える ───
            Begin();
            fixture.Back("B.unity");
            fixture.Back("AWhitebox.unity");
            fixture.Aside("M.mat");
            fixture.WriteTexture("U.png", F.EditedU);
            TestContext.WriteLine("[S34a R6] GetDependencies(C) with M aside: " + string.Join(", ", AssetDatabase.GetDependencies(fixture.PathOf("C.unity"), true)));
            var r6 = fixture.Plan(installed, _artifacts);
            Assert.That(OverlayKeys(r6), Is.EqualTo(new[] { "A", "B" }), "R6");
            var c = r6.Unbuildable.Single();
            Assert.That(c.Root.LogicalKey + ":" + c.Reason, Is.EqualTo("C:SourceAbsent"), "R6");
            Assert.That(c.Edits, Is.EqualTo(new[] { fixture.PathOf("U.png") }), "R6");
            Assert.That(c.MissingFiles, Is.EqualTo(new[] { fixture.PathOf("M.mat") }), "R6");
            var r6Built = new BuildContentCoordinator().Build(new BuildContentRequest(r6.Acknowledge(r6.Unbuildable.Select(x => x.Root))));
            Assert.That(r6Built.IsSuccess, Is.True, r6Built.Summary);
            Assert.That(F.Reports(_artifacts), Is.EqualTo(reports + 1), "R6: build 1 回");
            End("R6", fixture.PathOf("B.unity"), fixture.PathOf("AWhitebox.unity"), fixture.PathOf("M.mat"), fixture.PathOf("U.png"));

            // ─── R7: M を戻す。T を移動し、B を退かし、C に新しい texture N を足す ───
            Begin();
            reports = F.Reports(_artifacts);
            var tBefore = t;
            fixture.Back("M.mat");
            fixture.Move("T.png", "Sub");
            fixture.Aside("B.unity");
            var n = fixture.AddTextureToScene("C.unity", "N.png", new Color32(255, 0, 255, 255));
            var r7 = fixture.Plan(installed, _artifacts);
            var movedT = fixture.PathOf("T.png");
            Assert.That(OverlayKeys(r7), Is.EqualTo(new[] { "A", "C" }), "R7");
            Assert.That(r7.OverlayRoots.Single(x => x.Root.LogicalKey == "C").Edits,
                Is.EquivalentTo(new[] { n, fixture.PathOf("C.unity"), fixture.PathOf("U.png") }), "R7: 追加の N、内容の変更の C の Scene と U");
            Assert.That(r7.Edits.Where(x => x.Path == n).Select(x => x.Kind), Is.EqualTo(new[] { ContentOverlayEditKind.Added }));
            Assert.That(r7.Edits.Where(x => x.Path == movedT).Select(x => x.Kind), Does.Contain(ContentOverlayEditKind.Moved));
            var b7 = r7.Unbuildable.Single();
            Assert.That(b7.Root.LogicalKey + ":" + b7.Reason, Is.EqualTo("B:SourceAbsent"), "R7");
            Assert.That(b7.Edits, Is.EqualTo(new[] { movedT }), "R7: B の編集は移動した T");
            var r7Built = new BuildContentCoordinator().Build(new BuildContentRequest(r7.Acknowledge(r7.Unbuildable.Select(x => x.Root))));
            Assert.That(r7Built.IsSuccess, Is.True, r7Built.Summary);
            Assert.That(F.Reports(_artifacts), Is.EqualTo(reports + 1), "R7: build 1 回");
            End("R7", fixture.PathOf("M.mat"), tBefore, movedT, fixture.Folder + "/Sub", fixture.PathOf("B.unity"), fixture.PathOf("C.unity"), n);

            fixture.Back("B.unity");
            if (previous.Any(x => x.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
        }

        private Dictionary<string, F.Loaded> _played = new();

        // Play mode に入り、base と上書きを登録して A・B・C を読み、出る。アプリの起動は Addressables の経路にして、この test の登録と取り合わない。
        private IEnumerator Play(ContentInstallResult installed, BuildContentResult overlay)
        {
            const string modeEnv = "SAMPLEGAME_CONTENT__RUNTIMEMODE";
            var previousMode = Environment.GetEnvironmentVariable(modeEnv, EnvironmentVariableTarget.Process);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Environment.SetEnvironmentVariable(modeEnv, "addressables", EnvironmentVariableTarget.Process);
            try
            {
                yield return new EnterPlayMode();
                var run = F.ReadScenesAsync(installed, overlay, "A", "B", "C");
                yield return new WaitUntil(() => run.IsCompleted);
                _played = run.GetAwaiter().GetResult();
                yield return new ExitPlayMode();
            }
            finally
            {
                Environment.SetEnvironmentVariable(modeEnv, previousMode, EnvironmentVariableTarget.Process);
            }
        }

        private void AssertRound(params (string Key, ContentSourceKind Source, string Revision, Color32? Cookie, Color32? Main)[] expected)
        {
            foreach (var x in expected) F.AssertLoaded(_played[x.Key], x.Source, x.Revision, x.Cookie, x.Main, x.Key);
        }
    }
}
