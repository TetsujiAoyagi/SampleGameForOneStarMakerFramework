#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using E = OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayExpected;
using V = OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayVerdict;

namespace OneStarMaker.Tests.Editor.Spikes.PlayerOverlay
{
    /// <summary>
    /// 判定の写像だけの test（HANDOFF AC10）。全てが「同じ」になる入力を作り、§2 の誤った答えの道を 1 つずつ入れて、「同じ」にならず
    /// 定義どおりの答えか未判定になること、否定の結果が未判定に落ちないことを確かめる。Unity を使わないので通常の EditMode 回帰に入る。
    /// </summary>
    public sealed class PlayerOverlayVerdictTests
    {
        private const string Version = "6000.6.9f1", BuildGuid = "0123456789abcdef0123456789abcdef";

        // ───── 入力の組み立て ─────

        private static FileFact File(string item, bool sameSha) => new FileFact
        {
            Item = item, FoundBase = true, FoundOverlay = true, IdBase = item + ".cfid", IdOverlay = item + ".cfid",
            NameBase = item + "-a.cf", NameOverlay = sameSha ? item + "-a.cf" : item + "-b.cf", ShaBase = "a", ShaOverlay = sameSha ? "a" : "b",
        };

        private static FirstFacts First() => new FirstFacts
        {
            RunId = "run", Head = "head", ProjectVersion = Version, TargetOk = true, BuildsSucceeded = true, PackedAssetsRead = true, PlayerBuildGuid = BuildGuid,
            ScriptFile = File("scripts", false), ScriptSourcesBase = { E.BaseOnly, E.Both }, ScriptSourcesOverlay = { E.OverlayOnly, E.Both },
            Files = { File(E.SceneEdit, false), File(E.EditedTex, false), File(E.SharedTex, true), File(E.SharedMat, true), File(E.SharedShader, true), File(E.EditedMat, true) },
        };

        private static string Json(string tag, int value) => "{\"_tag\":\"" + tag + "\",\"_value\":" + value + "}";

        private static string[] Components(string item, string version) =>
            new[] { "UnityEngine.Transform", "UnityEngine.MeshFilter", "UnityEngine.MeshRenderer" }
                .Concat(E.Probes(item, version).Select(p => p.FullName + "\t" + Json(p.Tag, p.Value))).ToArray();

        private static PovLine L(string type, Action<PovLine>? set = null)
        {
            var line = new PovLine { type = type, at = "2026-11-23T00:00:00.0000000Z" };
            set?.Invoke(line);
            return line;
        }

        private static IEnumerable<PovLine> Op(string op, string dir, string key) => new[]
        {
            L("op-start", x => { x.op = op; x.dir = dir; x.key = key; }),
            L("op-result", x => { x.op = op; x.dir = dir; x.key = key; x.ok = true; x.rootIdentity = op == "register" ? E.BuildName(dir) : ""; }),
        };

        private static PovLine Comp(string stage, string item, string version) =>
            L("components", x => { x.stage = stage; x.item = item; x.objectName = E.ProbeObjectName(item, version); x.components = Components(item, version); });

        private static PovLine Scene(string stage, string dir, string version) => L("scene", x => { x.stage = stage; x.dir = dir; x.key = E.SceneEdit; x.marker = E.MarkerName(version); });
        private static PovLine Tex(string stage, string dir, string version, string id = "t") => L("texture", x => { x.stage = stage; x.dir = dir; x.key = E.EditedTex; x.pixels = E.PixelSha(version); x.entityId = id; });

        private static IEnumerable<PovLine> Counts(string point, Func<string, int> n) => E.Counted.Select(item =>
        {
            var ids = Enumerable.Range(1, n(item)).Select(i => item + i).ToArray();
            return L("count", x => { x.point = point; x.item = item; x.reached = ids; x.enumerated = ids.ToArray(); });
        });

        private static PovLine Held(string stage, string item, string version, int generation = 0) => L("held", x =>
        {
            x.stage = stage; x.item = item; x.generation = generation; x.objectId = item + generation;
            if (item == E.EditedTex) { x.textures = new[] { "t|" + E.PixelSha(version) }; return; }
            x.renderer = true; x.objectName = E.ProbeObjectName(item, version); x.components = Components(item, version);
            x.materials = new[] { "m|t|s" }; x.textures = new[] { "t|px" };
        });

        private static IEnumerable<PovLine> Lifetime(string editDir, string editVersion, Func<string, int> counts)
        {
            var lines = new List<PovLine>();
            lines.AddRange(Op("load-scene", editDir, E.SceneEdit)); lines.Add(Scene("(1)", editDir, editVersion));
            lines.AddRange(Op("load-scene", E.BaseDirectory, E.SceneKeep));
            lines.AddRange(Op("load-object", E.BaseDirectory, E.SharedPrefab));
            lines.AddRange(Op("load-object", editDir, E.EditedTex)); lines.Add(Tex("(1)", editDir, editVersion));
            lines.Add(Held(V.Loaded, E.SceneEdit, editVersion)); lines.Add(Held(V.Loaded, E.SceneKeep, "")); lines.Add(Held(V.Loaded, E.SharedPrefab, "")); lines.Add(Held(V.Loaded, E.EditedTex, editVersion));
            lines.AddRange(Counts("(1)", counts));
            foreach (var step in V.Checkpoints)
            {
                if (step == "(3)") { lines.Add(Scene("(3)", editDir, editVersion)); lines.Add(Held(V.Loaded, E.SceneEdit, editVersion, 1)); }
                if (step == "(5)") lines.Add(Held(V.Loaded, E.SceneKeep, "", 1));
                var editGeneration = step == "(1)" || step == "(2)" ? 0 : 1;
                var keepGeneration = step == "(5)" ? 1 : 0;
                if (step != "(2)") lines.Add(Held(step, E.SceneEdit, editVersion, editGeneration));
                if (step != "(4)") lines.Add(Held(step, E.SceneKeep, "", keepGeneration));
                lines.Add(Held(step, E.SharedPrefab, "")); lines.Add(Held(step, E.EditedTex, editVersion));
            }
            lines.Add(L("directories", x => x.count = 0));
            return lines;
        }

        private static List<PovLine> Record(string round, bool editor = false)
        {
            var lines = new List<PovLine>
            {
                L("header", x => { x.runId = "run"; x.head = "head"; x.isEditor = editor; x.backend = editor ? "Mono" : "IL2CPP"; x.unityVersion = Version; x.buildGuid = BuildGuid; x.round = round; x.firstPlay = true; x.domainReloadDisabled = true; }),
                L("round-start"),
                L("p0", x => { x.directories = 0; x.fixtureObjects = 0; x.scenes = new[] { E.BootScene }; }),
                L("p4", x => x.probeTypes = E.ProbeTypes.Select(t => t + "|true|" + E.ProbeAssembly).ToArray()),
            };
            foreach (var d in V.RegisterOrder(round)) lines.AddRange(Op("register", d, ""));
            Func<string, int> one = _ => 1;
            switch (round)
            {
                case V.BaseAlone:
                    lines.Add(Comp(V.BaseAlone, E.SceneKeep, "")); lines.Add(Comp(V.BaseAlone, E.SceneEdit, E.V1)); lines.Add(Comp(V.BaseAlone, E.SharedPrefab, ""));
                    lines.Add(Scene(V.BaseAlone, E.BaseDirectory, E.V1)); lines.Add(Tex(V.BaseAlone, E.BaseDirectory, E.V1)); lines.AddRange(Counts(V.BaseAlone, one));
                    break;
                case V.OverlayAlone:
                    lines.Add(Comp(V.OverlayAlone, E.SceneEdit, E.V2)); lines.Add(Scene(V.OverlayAlone, E.OverlayDirectory, E.V2)); lines.Add(Tex(V.OverlayAlone, E.OverlayDirectory, E.V2));
                    break;
                case V.BaseLifetime: lines.AddRange(Lifetime(E.BaseDirectory, E.V1, one)); break;
                case V.Read:
                    lines.Add(L("ids", x => { x.sceneIdsEqual = true; x.loadableIdsEqual = true; }));
                    lines.Add(Scene(V.Read, E.BaseDirectory, E.V2)); lines.Add(Scene(V.Read, E.OverlayDirectory, E.V2));
                    lines.Add(Tex(V.Read, E.BaseDirectory, E.V2)); lines.Add(Tex(V.Read, E.OverlayDirectory, E.V2));
                    lines.Add(L("count", x => { x.point = V.Read; x.item = E.EditedTex; x.reached = new[] { "t" }; x.enumerated = new[] { "t" }; }));
                    break;
                case V.Class:
                    lines.Add(Comp(V.Class, E.SceneKeep, "")); lines.Add(Comp(V.Class, E.SceneEdit, E.V2)); lines.Add(Comp(V.Class, E.SharedPrefab, ""));
                    lines.AddRange(Counts(V.Class, E.EditorMeasuredCount)); lines.Add(L("keep-edited", x => x.pixels = E.PixelSha(E.V1)));
                    break;
                case V.Lifetime: lines.AddRange(Lifetime(E.OverlayDirectory, E.V2, E.EditorMeasuredCount)); break;
            }
            lines.Add(L("end", x => x.completed = true));
            return lines;
        }

        private static readonly string[] PlayerLog = { "[AppInit] " + E.ProductStoppedLine + "。", E.StartMark + " x" };

        private static ProcessInput Process(string round, int replicate, bool editor = false) => new ProcessInput
        {
            Launch = new LaunchEntry { Round = round, Replicate = replicate, Label = round + "#" + replicate, ExitCode = 0 },
            Record = Record(round, editor), PlayerLog = PlayerLog.ToArray(),
        };

        private sealed class Case
        {
            public FirstFacts First = PlayerOverlayVerdictTests.First();
            public List<ProcessInput> Processes = V.AllRounds.SelectMany(r => Enumerable.Range(1, V.Replicates).Select(i => Process(r, i))).ToList();
            public List<ProcessInput> Control = new List<ProcessInput>();
            public bool InputMatched = true, Leftover;
            public IEnumerable<ProcessInput> Of(string round) => Processes.Where(x => x.Launch.Round == round);
            public ProcessInput One(string round, int replicate = 1) => Processes.Single(x => x.Launch.Round == round && x.Launch.Replicate == replicate);
            public VerdictResult Judge() => V.Judge(First, InputMatched, Leftover, Processes, Control);
        }

        private static void InEach(Case c, string round, Action<List<PovLine>> change) { foreach (var p in c.Of(round)) change(p.Record); }
        private static PovLine Find(List<PovLine> r, string type, Func<PovLine, bool>? where = null) => r.First(x => x.type == type && (where == null || where(x)));
        private static void InsertBeforeEnd(List<PovLine> r, PovLine line) => r.Insert(r.FindIndex(x => x.type == "end"), line);
        private static PovLine ErrorLog(string stage, string message = "boom") => L("log", x => { x.stage = stage; x.logType = "Error"; x.message = message; });

        private static void AssertUndecided(VerdictResult v, string contains)
        {
            Assert.That(v.Outcome, Is.EqualTo(VerdictResult.Undecided), string.Join("\n", v.Reasons));
            Assert.That(v.Reasons.Any(x => x.Contains(contains)), Is.True, "reasons: " + string.Join("\n", v.Reasons));
        }

        private static void AssertAnswer(VerdictResult v, string spike)
        {
            Assert.That(v.Spike, Is.EqualTo(spike), "reasons: " + string.Join("\n", v.Reasons) + "\nanswers: " + string.Join("\n", v.Answers));
        }

        // ───── 全てが成り立つ入力 ─────

        [Test]
        public void AllAsEditor_IsGoSame()
        {
            var v = new Case().Judge();
            Assert.That(v.Outcome, Is.EqualTo(VerdictResult.Go), string.Join("\n", v.Reasons));
            AssertAnswer(v, VerdictResult.Same);
            Assert.That(v.EditorControlRequired, Is.False);
        }

        // ───── 未判定に入る誤りの道 ─────

        [Test] public void P1ScriptFileSameSha_IsUndecided() { var c = new Case(); c.First.ScriptFile.ShaOverlay = "a"; AssertUndecided(c.Judge(), "P1"); }
        [Test] public void FixtureAssetPackedIntoPlayer_IsUndecided() { var c = new Case(); c.First.PackedFixtureAssets.Add("EditedTex"); AssertUndecided(c.Judge(), "packed"); }
        [Test] public void EditorHeader_IsUndecided() { var c = new Case(); Find(c.One(V.Read).Record, "header").isEditor = true; AssertUndecided(c.Judge(), "from the Editor"); }
        [Test] public void BuildGuidMismatch_IsUndecided() { var c = new Case(); Find(c.One(V.Class, 2).Record, "header").buildGuid = "ffff"; AssertUndecided(c.Judge(), "buildGUID"); }
        [Test] public void InputNotMatched_IsUndecided() { var c = new Case { InputMatched = false }; AssertUndecided(c.Judge(), "digests"); }
        [Test] public void LeftoverPlayerProcess_IsUndecided() { var c = new Case { Leftover = true }; AssertUndecided(c.Judge(), "remains"); }
        [Test] public void MissingP0LogLine_IsUndecided() { var c = new Case(); c.One(V.Lifetime).PlayerLog = new[] { E.StartMark }; AssertUndecided(c.Judge(), "P0 (1)"); }
        [Test] public void DirectoryRegisteredBeforeRound_IsUndecided() { var c = new Case(); Find(c.One(V.Read).Record, "p0").directories = 1; AssertUndecided(c.Judge(), "P0 (2)"); }
        [Test] public void NoRoundStart_IsUndecided() { var c = new Case(); c.One(V.Class).Record.RemoveAll(x => x.type == "round-start"); AssertUndecided(c.Judge(), "round-start"); }
        [Test] public void ProbeBaseOnlyNotInPlayer_IsUndecided() { var c = new Case(); InEach(c, V.BaseAlone, r => Find(r, "p4").probeTypes[0] = E.BaseOnly + "|false|"); AssertUndecided(c.Judge(), "P4 " + E.BaseOnly); }
        [Test] public void MissingRoundProcess_IsUndecided() { var c = new Case(); c.Processes.Remove(c.One(V.Lifetime, 3)); AssertUndecided(c.Judge(), "2 processes"); }

        [Test]
        public void ReversedRegisterOrder_IsUndecided()
        {
            var c = new Case();
            var r = c.One(V.Read).Record;
            var starts = r.Where(x => x.type == "op-start" && x.op == "register").ToList();
            (starts[0].dir, starts[1].dir) = (starts[1].dir, starts[0].dir);
            AssertUndecided(c.Judge(), "Register order");
        }

        [Test]
        public void RecordCutBeforeSecondRegister_IsUndecided()
        {
            var c = new Case();
            var r = c.One(V.Lifetime).Record;
            r.RemoveRange(r.FindIndex(x => x.type == "op-result" && x.op == "register") + 1, r.Count - r.FindIndex(x => x.type == "op-result" && x.op == "register") - 1);
            AssertUndecided(c.Judge(), "before the second Register");
        }

        [Test]
        public void StoppedAtLimitWhileGrowing_IsUndecided()
        {
            var c = new Case();
            var p = c.One(V.Lifetime);
            p.Record.RemoveRange(p.Record.FindIndex(x => x.type == "held"), p.Record.Count - p.Record.FindIndex(x => x.type == "held"));
            p.Launch.StopReason = "launcher-limit";
            p.Launch.EndedAt = "2026-11-23T00:00:30.0000000Z";
            AssertUndecided(c.Judge(), "still growing");
        }

        [Test]
        public void MainFirstRegisterFailed_IsUndecided()
        {
            var c = new Case();
            var r = c.One(V.Class).Record;
            Find(r, "op-result", x => x.op == "register" && x.dir == E.BaseDirectory).ok = false;
            AssertUndecided(c.Judge(), "first Register");
        }

        [Test]
        public void CountZeroWithAllLoadsSucceeded_IsUndecided()
        {
            var c = new Case();
            var line = Find(c.One(V.Class).Record, "count", x => x.item == E.KeepTex);
            line.reached = Array.Empty<string>(); line.enumerated = Array.Empty<string>();
            AssertUndecided(c.Judge(), "count is 0");
        }

        [Test]
        public void BaselineDiffersAcrossProcesses_IsUndecided()
        {
            var c = new Case();
            Find(c.One(V.BaseAlone, 2).Record, "count", x => x.item == E.SharedMat).enumerated = new[] { "x", "y" };
            AssertUndecided(c.Judge(), "baseline");
        }

        [Test]
        public void MainFailureWhileBaselineFails_IsUndecided()
        {
            var c = new Case();
            InsertBeforeEnd(c.One(V.BaseAlone).Record, ErrorLog(V.BaseAlone));
            Find(c.One(V.Read).Record, "op-result", x => x.op == "register" && x.dir == E.OverlayDirectory).ok = false;
            var v = c.Judge();
            AssertUndecided(v, "baseline does not hold");
            Assert.That(v.BaselineHolds, Is.False);
        }

        // ───── 「同じ」にならず、否定の答えになる道 ─────

        [Test]
        public void SwappedProbePositions_DoesNotHold()
        {
            var c = new Case();
            InEach(c, V.Class, r => { var comp = Find(r, "components", x => x.item == E.SceneKeep).components; (comp[3], comp[4]) = (comp[4], comp[3]); });
            var v = c.Judge();
            AssertAnswer(v, VerdictResult.DoesNotHold);
            Assert.That(v.OnlyTypeMissingFailed, Is.False);
        }

        [Test]
        public void ShorterComponentList_DoesNotHold()
        {
            var c = new Case();
            InEach(c, V.Class, r => { var x = Find(r, "components", y => y.item == E.SharedPrefab); x.components = x.components.Take(4).ToArray(); });
            AssertAnswer(c.Judge(), VerdictResult.DoesNotHold);
        }

        [Test]
        public void VersionNeitherV1NorV2_DoesNotHold()
        {
            var c = new Case();
            InEach(c, V.Read, r => Find(r, "scene", x => x.dir == E.BaseDirectory).marker = "Marker_x");
            AssertAnswer(c.Judge(), VerdictResult.DoesNotHold);
        }

        [Test] public void ErrorOnlyInRegisterStage_DoesNotHold() { var c = new Case(); InEach(c, V.Read, r => r.Insert(r.FindIndex(x => x.type == "op-result" && x.op == "register"), ErrorLog("register"))); AssertAnswer(c.Judge(), VerdictResult.DoesNotHold); }
        [Test] public void ErrorOnlyInReleaseStage_DoesNotHold() { var c = new Case(); InsertBeforeEnd(c.One(V.Lifetime).Record, ErrorLog(V.StepRelease)); AssertAnswer(c.Judge(), VerdictResult.DoesNotHold); }

        [Test]
        public void ErrorAfterEndLine_IsNotCounted()
        {
            var c = new Case();
            c.One(V.Lifetime).Record.Add(ErrorLog("after"));
            AssertAnswer(c.Judge(), VerdictResult.Same);
        }

        [Test]
        public void AbnormalExitAfterEndLine_StaysSame()
        {
            var c = new Case();
            foreach (var p in c.Processes) { p.Launch.ExitCode = -1; p.Launch.StopReason = "after-end"; }
            AssertAnswer(c.Judge(), VerdictResult.Same);
        }

        [Test]
        public void LoadFailureWithAndWithoutEndLine_BothCannotLoad()
        {
            foreach (var withEnd in new[] { true, false })
            {
                var c = new Case();
                var r = c.One(V.Lifetime).Record;
                var failed = Find(r, "op-result", x => x.op == "load-scene" && x.key == E.SceneKeep);
                failed.ok = false; failed.failure = "exception";
                r.RemoveRange(r.IndexOf(failed) + 1, r.Count - r.IndexOf(failed) - 1);
                if (withEnd) r.Add(L("end", x => { x.completed = false; x.abortedAt = "(1)"; }));
                var v = c.Judge();
                AssertAnswer(v, VerdictResult.DoesNotHold);
                Assert.That(v.Answers.Any(x => x.Contains("cannot load")), Is.True, string.Join("\n", v.Answers));
            }
        }

        [Test]
        public void SecondRegisterInvalidHandle_DoesNotHold()
        {
            var c = new Case();
            var r = c.One(V.Read).Record;
            var failed = Find(r, "op-result", x => x.op == "register" && x.dir == E.OverlayDirectory);
            failed.ok = false; failed.failure = "invalid handle";
            AssertAnswer(c.Judge(), VerdictResult.DoesNotHold);
        }

        [Test]
        public void CrashedAfterSecondRegister_DoesNotHold()
        {
            var c = new Case();
            var r = c.One(V.Class).Record;
            r.RemoveRange(r.FindIndex(x => x.type == "components"), r.Count - r.FindIndex(x => x.type == "components"));
            var v = c.Judge();
            AssertAnswer(v, VerdictResult.DoesNotHold);
            Assert.That(v.Answers.Any(x => x.Contains("crashed")), Is.True);
        }

        [Test]
        public void SilentUntilLimit_DoesNotFinish()
        {
            var c = new Case();
            var p = c.One(V.Lifetime);
            p.Record.RemoveRange(p.Record.FindIndex(x => x.type == "held"), p.Record.Count - p.Record.FindIndex(x => x.type == "held"));
            p.Record.Add(L("limit-exceeded", x => x.at = "2026-11-23T00:03:00.0000000Z"));
            var v = c.Judge();
            AssertAnswer(v, VerdictResult.DoesNotHold);
            Assert.That(v.Answers.Any(x => x.Contains("does not finish")), Is.True);
        }

        [Test]
        public void BrokenAtCheckpoint_DoesNotHold()
        {
            var c = new Case();
            Find(c.One(V.Lifetime).Record, "held", x => x.stage == "(4)" && x.item == E.EditedTex).textures = new[] { "t|changed" };
            AssertAnswer(c.Judge(), VerdictResult.DoesNotHold);
        }

        [Test]
        public void CountAboveTwo_DoesNotHold()
        {
            var c = new Case();
            Find(c.One(V.Class).Record, "count", x => x.item == E.SharedMat).enumerated = new[] { "a", "b", "c" };
            AssertAnswer(c.Judge(), VerdictResult.DoesNotHold);
        }

        // ───── 「違うが成り立つ」 ─────

        [Test] public void X1Differs_DiffersButHolds() { var c = new Case(); InEach(c, V.Read, r => Find(r, "texture", x => x.dir == E.OverlayDirectory).entityId = "u"); AssertAnswer(c.Judge(), VerdictResult.DiffersButHolds); }
        [Test] public void X2Differs_DiffersButHolds() { var c = new Case(); InEach(c, V.Class, r => Find(r, "keep-edited").pixels = E.PixelSha(E.V2)); AssertAnswer(c.Judge(), VerdictResult.DiffersButHolds); }

        [Test]
        public void ReachedAndEnumeratedDifferInMain_UsesTheLargerAndIsNotUndecided()
        {
            var c = new Case();
            InEach(c, V.Class, r => Find(r, "count", x => x.item == E.SharedShader).reached = new[] { "s1" });
            var v = c.Judge();
            AssertAnswer(v, VerdictResult.DiffersButHolds);
            Assert.That(v.Differences.Any(x => x.Contains("reached")), Is.True);
        }

        [Test]
        public void VarianceAcrossMainProcesses_IsFlaggedWithThreeValues()
        {
            var c = new Case();
            Find(c.One(V.Read, 2).Record, "scene", x => x.dir == E.BaseDirectory).marker = E.MarkerV1;
            var v = c.Judge();
            AssertAnswer(v, VerdictResult.DiffersButHolds);
            Assert.That(v.ProcessVariance, Is.True);
            var line = v.Variance.First(x => x.Contains("scene read base"));
            Assert.That(line, Does.Contain("read#1=").And.Contain("read#2=").And.Contain("read#3="));
            Assert.That(v.Variance.Any(x => x.Contains("the same in all processes")), Is.True);
            Assert.That(v.EditorControlRequired, Is.True);
        }

        [Test]
        public void VarianceWithOneFailingProcess_TakesTheWorst()
        {
            var c = new Case();
            Find(c.One(V.Class, 3).Record, "keep-edited").pixels = "x";
            Find(c.One(V.Lifetime, 2).Record, "held", x => x.stage == "(2)" && x.item == E.SharedPrefab).objectId = "null";
            var v = c.Judge();
            AssertAnswer(v, VerdictResult.DoesNotHold);
            Assert.That(v.ProcessVariance, Is.True);
        }

        // ───── 型の欠落 ─────

        private static Case TypeMissing(bool dropped)
        {
            var c = new Case();
            foreach (var p in c.Processes) Find(p.Record, "p4").probeTypes[2] = E.OverlayOnly + "|false|";
            void Strip(List<PovLine> r, string stage)
            {
                var line = Find(r, "components", x => x.stage == stage && x.item == E.SceneEdit);
                line.components = line.components.Select(x => x.StartsWith(E.OverlayOnly, StringComparison.Ordinal) ? "Missing" : x).Where(x => !dropped || x != "Missing").ToArray();
            }
            InEach(c, V.OverlayAlone, r => { Strip(r, V.OverlayAlone); InsertBeforeEnd(r, ErrorLog(V.OverlayAlone, "script class missing")); });
            InEach(c, V.Class, r => { Strip(r, V.Class); InsertBeforeEnd(r, ErrorLog(V.Class, "script class missing")); });
            return c;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TypeMissingOnly_DoesNotHoldWithTheCause(bool dropped)
        {
            var v = TypeMissing(dropped).Judge();
            Assert.That(v.Outcome, Is.EqualTo(VerdictResult.Undecided), "AC6 is required because the answer is not same");
            Assert.That(v.Reasons.All(x => x.StartsWith("AC6", StringComparison.Ordinal)), Is.True, string.Join("\n", v.Reasons));
            Assert.That(v.BaselineHolds, Is.True);
            AssertAnswer(v, VerdictResult.DoesNotHold);
            Assert.That(v.TypeMissing && v.OnlyTypeMissingFailed, Is.True);
            Assert.That(v.Answers.Single(x => x.StartsWith("X3")), Does.StartWith("X3: 0"));
            Assert.That(v.Answers.Any(x => x.StartsWith("Q4") && x.Contains(V.Broken)), Is.False);
        }

        [Test]
        public void TypeMissingWithOtherWrongPosition_IsNotTypeOnly()
        {
            var c = TypeMissing(false);
            InEach(c, V.Class, r => { var x = Find(r, "components", y => y.item == E.SceneKeep); x.components = x.components.Take(4).ToArray(); });
            var v = c.Judge();
            AssertAnswer(v, VerdictResult.DoesNotHold);
            Assert.That(v.OnlyTypeMissingFailed, Is.False);
        }

        // ───── AC6 の Editor の対照 ─────

        private static ProcessInput Control(string round, int replicate) => Process(round, replicate, editor: true);

        [Test]
        public void NotSameWithoutControl_IsUndecided()
        {
            var c = new Case();
            InEach(c, V.Class, r => Find(r, "keep-edited").pixels = E.PixelSha(E.V2));
            AssertUndecided(c.Judge(), "AC6");
        }

        [Test]
        public void NotSameWithReproducingControl_IsGo()
        {
            var c = new Case();
            InEach(c, V.Class, r => Find(r, "keep-edited").pixels = E.PixelSha(E.V2));
            c.Control = V.MainRounds.Select(r => Control(r, 1)).ToList();
            var v = c.Judge();
            Assert.That(v.Outcome, Is.EqualTo(VerdictResult.Go), string.Join("\n", v.Reasons));
            AssertAnswer(v, VerdictResult.DiffersButHolds);
        }

        [Test]
        public void ControlNotReproducing_IsUndecided()
        {
            var c = new Case();
            InEach(c, V.Class, r => Find(r, "keep-edited").pixels = E.PixelSha(E.V2));
            c.Control = V.MainRounds.Select(r => Control(r, 1)).ToList();
            Find(c.Control.Single(x => x.Launch.Round == V.Class).Record, "keep-edited").pixels = E.PixelSha(E.V2);
            AssertUndecided(c.Judge(), "does not reproduce");
        }

        [Test]
        public void VarianceNeedsThreeControlRuns()
        {
            var c = new Case();
            Find(c.One(V.Read, 2).Record, "scene", x => x.dir == E.BaseDirectory).marker = E.MarkerV1;
            c.Control = V.MainRounds.Select(r => Control(r, 1)).ToList();
            AssertUndecided(c.Judge(), "needs 3");
        }
    }
}
