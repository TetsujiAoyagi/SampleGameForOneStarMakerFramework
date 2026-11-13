#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using S = OneStarMaker.Tests.Editor.Spikes.OverlayBuild.OverlaySpec;
using V = OneStarMaker.Tests.Editor.Spikes.OverlayBuild.OverlayBuildVerdict;

namespace OneStarMaker.Tests.Editor.Spikes.OverlayBuild
{
    /// <summary>harness の判定の写像（補助）の単体。通常の EditMode 回帰に入る（Unity の build も Play も使わない）。</summary>
    public sealed class OverlayBuildVerdictTests
    {
        [Test]
        public void ExpectedObservations_AllHold()
        {
            var result = V.Judge(Build(), new[] { Round("ScenesFirst"), Round("ObjectFirst") });
            Assert.That(result.Outcome, Is.EqualTo(V.AllHold), string.Join("\n", result.Reasons));
            Assert.That(result.Acs, Has.All.EndsWith("=holds"));
        }

        [Test]
        public void P1NotHolding_IsUndetermined()
        {
            var build = Build();
            // 置いた側に値が書いてあると、編集した prefab を変えても観測点は v1 のまま（R2-01 の穴）。
            build.BeforeOverlay.Single(x => x.Id == "SceneNested.InnerProbe").Value = S.PositionValue(S.Position(S.InnerProbe, S.V1));
            Assert.That(V.Judge(build, new[] { Round("ScenesFirst"), Round("ObjectFirst") }).Outcome, Is.EqualTo(V.Undetermined));
        }

        [Test]
        public void P2NotHolding_IsUndetermined()
        {
            var build = Build();
            build.ChangedSources.Add(S.RootPath(S.SceneNested));
            Assert.That(V.Judge(build, new[] { Round("ScenesFirst"), Round("ObjectFirst") }).Outcome, Is.EqualTo(V.Undetermined));
        }

        [Test]
        public void CanaryAtV2_IsUndetermined()
        {
            var a = Round("ScenesFirst");
            Set(a, S.CanaryPoint, S.V2);
            var b = Round("ObjectFirst");
            Set(b, S.CanaryPoint, S.V2);
            Assert.That(V.Judge(Build(), new[] { a, b }).Outcome, Is.EqualTo(V.Undetermined));
        }

        [Test]
        public void ProcessesDisagree_GoesToTheOwner()
        {
            var b = Round("ObjectFirst");
            b.SharedTexCount = 1;
            Assert.That(V.Judge(Build(), new[] { Round("ScenesFirst"), b }).Outcome, Is.EqualTo(V.UndeterminedOwner));
        }

        [TestCase("SceneDirect.EditedTex", S.V1)]
        [TestCase("SceneNested.InnerProbe", S.V1)]
        [TestCase(S.BaseSideUnlisted, S.V2)]
        [TestCase(S.OverlaySideUnlisted, S.V1)]
        public void WrongVersion_IsNoGo(string point, string version)
        {
            var a = Round("ScenesFirst");
            Set(a, point, version);
            var b = Round("ObjectFirst");
            Set(b, point, version);
            Assert.That(V.Judge(Build(), new[] { a, b }).Outcome, Is.EqualTo(V.NoGo));
        }

        [Test]
        public void ExtraOverlayRoot_IsNoGo()
        {
            var build = Build();
            build.SelectedRoots.Add(S.SceneUnlisted);
            Assert.That(V.Judge(build, new[] { Round("ScenesFirst"), Round("ObjectFirst") }).Outcome, Is.EqualTo(V.NoGo));
        }

        [Test]
        public void ErrorLogOrMissingSceneClean_ReturnsToB()
        {
            var a = Round("ScenesFirst");
            a.ErrorLogs = 1;
            Assert.That(V.Judge(Build(), new[] { a, Round("ObjectFirst") }).Outcome, Is.EqualTo(V.FailB));
            var c = Round("ScenesFirst");
            c.SceneCleanLoaded = false;
            Assert.That(V.Judge(Build(), new[] { c, Round("ObjectFirst") }).Outcome, Is.EqualTo(V.FailB));
        }

        private static BuildFacts Build()
        {
            var b = new BuildFacts
            {
                RunId = "run", Head = "head", BaseBuilt = true, BasePublished = true, OverlayBuilt = true, BaseUnchanged = true,
                FixtureDeleted = true, Finished = true, PreflightOverlay = true, OutcomeOverlay = true,
                ChangedSources = S.ChangedInV2.ToList(),
                SelectedRoots = S.OverlayRoots.ToList(),
                PreflightBase = new List<string> { S.BaseSet, "rev", "StandaloneWindows64-Player", "digest", S.DeclarationName },
                PreflightEdits = S.Edits.OrderBy(x => x, System.StringComparer.Ordinal).ToList(),
                PreflightRoots = new List<string> { S.SceneNested + "=" + S.Prefab(S.InnerPrefab) },
                PreflightUnmatched = new List<string> { S.Tex(S.UnusedTex) + "=NotInAnyClosure" },
                SharedAssets = new List<string> { S.Tex(S.SharedTex), S.Tex(S.UnlistedTex) },
                PreflightShared = new List<string> { S.Tex(S.SharedTex), S.Tex(S.UnlistedTex) },
            };
            foreach (var p in S.Points)
            {
                b.BeforeBase.Add(new PointValue { Id = p.Id, Value = p.Expected(S.V1) });
                b.BeforeOverlay.Add(new PointValue { Id = p.Id, Value = p.Expected(S.V2) });
            }
            return b;
        }

        private static RoundFacts Round(string process)
        {
            var r = new RoundFacts
            {
                Process = process, RunId = "run", Head = "head", DomainReloadDisabled = true, BaseRegistered = true, OverlayRegistered = true,
                BaseKeys = S.Roots.OrderBy(x => x, System.StringComparer.Ordinal).ToList(), OverlayKeys = S.OverlayRoots.ToList(),
                BaseDeclaration = S.DeclarationName, OverlayDeclaration = S.DeclarationName,
                SceneCleanLoaded = true, SharedTexCount = 2, CleanOnlyTexCount = 1, Completed = true, CleanedUp = true,
            };
            foreach (var p in S.Points)
            {
                var overlaySide = S.Ac6Points.Contains(p.Id) || p.Id == S.OverlaySideUnlisted;
                r.Points.Add(new PointValue { Id = p.Id, Value = p.Expected(overlaySide ? S.V2 : S.V1) });
            }
            return r;
        }

        private static void Set(RoundFacts round, string id, string version) =>
            round.Points.Single(x => x.Id == id).Value = S.Points.Single(x => x.Id == id).Expected(version);
    }
}
