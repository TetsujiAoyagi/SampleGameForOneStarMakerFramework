#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using E = OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.OverlayExpected;
using V = OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.OverlayVerdict;

namespace OneStarMaker.Tests.Editor.Spikes.OverlayRegistration
{
    /// <summary>
    /// 判定の写像だけの test（HANDOFF §3）。全てが成り立つ入力を作り、§2 の誤って YES を出す道を 1 つずつ入れて、
    /// YES にならない（未判定か、別の答えになる）ことを確かめる。Unity を使わないので通常の EditMode 回帰に入る。
    /// </summary>
    public sealed class OverlayVerdictTests
    {
        // ───── 入力の組み立て ─────

        private static FileFact File(string item, bool sameSha) => new FileFact
        {
            Item = item, FoundBase = true, FoundOverlay = true, IdBase = item + ".cfid", IdOverlay = item + ".cfid",
            NameBase = item + "-a.cf", NameOverlay = sameSha ? item + "-a.cf" : item + "-b.cf", ShaBase = "a", ShaOverlay = sameSha ? "a" : "b",
        };

        private static FirstFacts First() => new FirstFacts
        {
            RunId = "run", Head = "head", BuildsSucceeded = true,
            ScriptFile = File("scripts", false),
            ScriptSourcesBase = { E.BaseOnly, E.Both },
            ScriptSourcesOverlay = { E.OverlayOnly, E.Both },
            Files = { File(E.SceneEdit, false), File(E.EditedTex, false), File(E.SharedTex, true), File(E.SharedMat, true), File(E.SharedShader, true), File(E.EditedMat, true) },
        };

        private static List<ComponentObservation> Components(string item, string version) =>
            new[] { new ComponentObservation { FullName = "UnityEngine.Transform" } }
                .Concat(E.Probes(item, version).Select(p => new ComponentObservation { FullName = p.FullName, Json = Json(p.Tag, p.Value) }))
                .Append(new ComponentObservation { FullName = "UnityEngine.MeshRenderer" }).ToList();

        private static string Json(string tag, int value) => "{\"_tag\":\"" + tag + "\",\"_value\":" + value + "}";

        private static ProbeObservation Probe(string item, string version) => new ProbeObservation
        {
            Item = item, Found = true, ObjectName = item == E.SceneEdit ? E.MarkerName(version) : item == E.SceneKeep ? E.Keeper : item,
            Components = Components(item, version),
        };

        private static List<CountObservation> Counts() => E.Counted.Select(item =>
        {
            var ids = E.CountedOne.Contains(item) ? new List<string> { item + "1" } : new List<string> { item + "1", item + "2" };
            return new CountObservation { Item = item, LoadIds = ids.ToList(), EnumeratedIds = ids.ToList() };
        }).ToList();

        private static CallObservation Call(string call, string version = "") => new CallObservation { Call = call, Succeeded = true, Version = version };

        private static RoundObservation Round(string round, int index)
        {
            var r = new RoundObservation
            {
                Round = round, PlayIndex = index, Ended = true, CleanedUp = true, RootsMatched = true, DirectoriesAtStart = 0, LeftoverAtStart = 0,
                RegisterOrder = V.RegisterOrder(round).ToList(), SceneIdsEqual = true, LoadableIdsEqual = true,
            };
            r.Calls.AddRange(r.RegisterOrder.Select(d => Call(d + ":register")));
            switch (round)
            {
                case V.Ac3:
                case V.Ac6Read:
                    r.Calls.AddRange(new[] { Call("base:SceneEdit", E.V2), Call("overlay:SceneEdit", E.V2), Call("base:EditedTex", E.V2), Call("overlay:EditedTex", E.V2) });
                    r.Counts.Add(new CountObservation { Item = E.EditedTex, LoadIds = { "t" }, EnumeratedIds = { "t" } });
                    break;
                case V.Ac4:
                case V.Ac6Class:
                    r.Probes.AddRange(new[] { Probe(E.SceneKeep, ""), Probe(E.SceneEdit, E.V2), Probe(E.SharedPrefab, "") });
                    r.Counts = Counts();
                    break;
                case V.Ac5:
                    r.Counts = Counts();
                    r.Checkpoints = V.Ac5Steps.Select(s => new CheckpointObservation { Step = s }).ToList();
                    r.DirectoriesAfterUnregister = 0;
                    break;
                case V.Ac2Base:
                    r.Probes.AddRange(new[] { Probe(E.SceneKeep, ""), Probe(E.SceneEdit, E.V1), Probe(E.SharedPrefab, "") });
                    r.Calls.AddRange(new[] { Call("base:SceneKeep"), Call("base:SceneEdit", E.V1), Call("base:SharedPrefab"), Call("base:EditedTex", E.V1) });
                    break;
                case V.Ac2Overlay:
                    r.Probes.Add(Probe(E.SceneEdit, E.V2));
                    r.Calls.AddRange(new[] { Call("overlay:SceneEdit", E.V2), Call("overlay:EditedTex", E.V2) });
                    break;
            }
            return r;
        }

        private static ProcessObservation Process(string name) => new ProcessObservation
        {
            Process = name, RunId = "run", Head = "head", InputMatched = true, DomainReloadDisabled = true, TargetOk = true, Finished = true,
            Rounds = V.Plan(name).Select((round, i) => Round(round, i)).ToList(),
        };

        private static List<ProcessObservation> Processes() => V.RequiredProcesses.Select(Process).ToList();

        private static IEnumerable<RoundObservation> Rounds(List<ProcessObservation> ps, string round) => ps.SelectMany(p => p.Rounds).Where(r => r.Round == round);

        private static Verdict Judge(FirstFacts? first = null, List<ProcessObservation>? ps = null) => V.Judge(first ?? First(), ps ?? Processes());

        private static void AssertUndecided(Verdict v, string reasonPart)
        {
            Assert.That(v.Outcome, Is.EqualTo(V.Undecided));
            Assert.That(v.Reasons, Has.Some.Contains(reasonPart), string.Join("\n", v.Reasons));
        }

        // ───── 成り立つ時 ─────

        [Test]
        public void AllHolding_IsYes()
        {
            var v = Judge();
            Assert.That(v.Outcome, Is.EqualTo(V.Go), string.Join("\n", v.Reasons));
            Assert.That(v.Spike, Is.EqualTo(V.Yes));
            Assert.That(v.Answers, Has.Member("Q1: " + V.Strong).And.Member("Q2: " + V.Correct).And.Member("Q4: " + V.Intact));
        }

        [Test]
        public void Plan_PutsTheNamedRoundFirstAndTheBaselinesLast()
        {
            Assert.That(V.Plan(V.ReadFirst), Is.EqualTo(new[] { V.Ac3, V.Ac4, V.Ac5, V.Ac6Class, V.Ac6Read, V.Ac2Base, V.Ac2Overlay }));
            Assert.That(V.Plan(V.ControlReadFirst), Is.EqualTo(new[] { V.Ac6Read, V.Ac4, V.Ac5, V.Ac3, V.Ac6Class, V.Ac2Base, V.Ac2Overlay }));
            Assert.That(V.Plan(V.BaseAloneFirst), Is.EqualTo(new[] { V.Ac2Base }));
        }

        // ───── (a) 危険な条件が fixture に入っていない（P1〜P3） ─────

        [Test]
        public void P1_ShaDifferenceAloneDoesNotHold()
        {
            var f = First();
            f.ScriptSourcesBase.Add(E.OverlayOnly);
            f.ScriptSourcesOverlay.Add(E.BaseOnly);
            AssertUndecided(Judge(f), "P1: base script list has " + E.OverlayOnly);
        }

        [Test]
        public void P1_SameScriptFileDoesNotHold()
        {
            var f = First();
            f.ScriptFile = File("scripts", true);
            AssertUndecided(Judge(f), "P1: script file sha256 is the same");
        }

        [Test]
        public void P1_MissingProbeInListDoesNotHold()
        {
            var f = First();
            f.ScriptSourcesOverlay.Remove(E.OverlayOnly);
            AssertUndecided(Judge(f), "P1: overlay script list lacks " + E.OverlayOnly);
        }

        [Test]
        public void P2_SameBytesForEditedDoesNotHold()
        {
            var f = First();
            f.Files[1] = File(E.EditedTex, true);
            AssertUndecided(Judge(f), "P2: EditedTex");
        }

        [Test]
        public void P3_DifferentBytesForSharedDoesNotHold()
        {
            var f = First();
            f.Files[5] = File(E.EditedMat, false);
            AssertUndecided(Judge(f), "P3: EditedMat");
        }

        // ───── (b)(c) class の照合 ─────

        [Test]
        public void Classes_SwappedPositionsAreNotCorrect()
        {
            var observed = Components(E.SceneKeep, "");
            (observed[1], observed[2]) = (observed[2], observed[1]);
            Assert.That(V.ClassesCorrect(observed, E.Probes(E.SceneKeep, ""), out _), Is.False);
        }

        [Test]
        public void Classes_SameTypesWithSwappedValuesAreNotCorrect()
        {
            var observed = Components(E.SceneKeep, "");
            (observed[1].Json, observed[2].Json) = (observed[2].Json, observed[1].Json);
            Assert.That(V.ClassesCorrect(observed, E.Probes(E.SceneKeep, ""), out var why), Is.False);
            Assert.That(why, Does.Contain("value"));
        }

        [Test]
        public void Q2_AnotherClassIsWrong()
        {
            var ps = Processes();
            foreach (var r in Rounds(ps, V.Ac4)) r.Probes[0].Components[1].FullName = E.OverlayOnly;
            var v = Judge(ps: ps);
            Assert.That(v.Outcome, Is.EqualTo(V.Go), string.Join("\n", v.Reasons));
            Assert.That(v.Spike, Is.EqualTo(V.No));
            Assert.That(v.Answers, Has.Some.StartsWith("Q2: " + V.Wrong));
        }

        [Test]
        public void Q2_MissingComponentIsWrong()
        {
            var ps = Processes();
            foreach (var r in Rounds(ps, V.Ac4)) r.Probes[2].Components[1] = new ComponentObservation { Missing = true };
            var v = Judge(ps: ps);
            Assert.That(v.Answers, Has.Some.Contains("SharedPrefab missing 1"));
            Assert.That(v.Spike, Is.EqualTo(V.No));
        }

        // ───── (d) 版と Q1 ─────

        [Test]
        public void Q1_OverlayV1IsNotHeld()
        {
            var ps = Processes();
            foreach (var r in Rounds(ps, V.Ac3)) r.Calls.First(c => c.Call == "overlay:EditedTex").Version = E.V1;
            var v = Judge(ps: ps);
            Assert.That(v.Answers, Has.Member("Q1: " + V.NotHeld));
            Assert.That(v.Spike, Is.EqualTo(V.No));
        }

        [Test]
        public void Q1_WeakWithDifferentIdsIsConditionalYes()
        {
            var ps = Processes();
            foreach (var r in Rounds(ps, V.Ac3)) { r.SceneIdsEqual = false; r.Calls.First(c => c.Call == "base:SceneEdit").Version = E.V1; }
            var v = Judge(ps: ps);
            Assert.That(v.Spike, Is.EqualTo(V.ConditionalYes), string.Join("\n", v.Reasons));
        }

        [Test]
        public void Q1_WeakWithEqualIdsIsUndecided()
        {
            var ps = Processes();
            foreach (var r in Rounds(ps, V.Ac3)) r.Calls.First(c => c.Call == "base:SceneEdit").Version = E.V1;
            AssertUndecided(Judge(ps: ps), "weak although both ids are equal");
        }

        [Test]
        public void Q1_UnknownVersionIsUndecided()
        {
            var ps = Processes();
            foreach (var r in Rounds(ps, V.Ac3)) r.Calls.First(c => c.Call == "base:EditedTex").Version = E.UnknownVersion;
            AssertUndecided(Judge(ps: ps), "not in the defined terms");
        }

        [Test]
        public void Q2_SceneEditNotOverlayVersionIsUndecided()
        {
            var ps = Processes();
            foreach (var r in Rounds(ps, V.Ac4)) r.Probes[1] = Probe(E.SceneEdit, E.V1);
            AssertUndecided(Judge(ps: ps), "not the overlay version");
        }

        // ───── (e) 期待値は fixture の定義から。単独の基準は確認に使う ─────

        [Test]
        public void Baseline_DifferentFromDefinitionIsUndecided()
        {
            var ps = Processes();
            foreach (var r in Rounds(ps, V.Ac2Base)) r.Calls.First(c => c.Call == "base:EditedTex").Version = E.V2;
            AssertUndecided(Judge(ps: ps), "EditedTex version v2, expected v1");
        }

        // ───── Q3・Q4 と実体の数 ─────

        [Test]
        public void Q3_ThreeEntitiesOfEditedTexExceeds()
        {
            var ps = Processes();
            foreach (var r in Rounds(ps, V.Ac5))
            {
                var c = r.Counts.First(x => x.Item == E.EditedTex);
                c.LoadIds.Add("third"); c.EnumeratedIds.Add("third");
            }
            var v = Judge(ps: ps);
            Assert.That(v.Answers, Has.Some.StartsWith("Q3: " + V.Exceeds));
            Assert.That(v.Spike, Is.EqualTo(V.No));
        }

        [Test]
        public void Q3_InconsistentEnumerationIsUndecided()
        {
            var ps = Processes();
            foreach (var r in Rounds(ps, V.Ac4)) r.Counts.First(x => x.Item == E.SharedTex).EnumeratedIds.Add("unseen");
            AssertUndecided(Judge(ps: ps), "SharedTex count does not follow the consistency rule");
        }

        [Test]
        public void Q4_BrokenCheckpointIsNo()
        {
            var ps = Processes();
            foreach (var r in Rounds(ps, V.Ac5)) r.Checkpoints[3].Broken.Add("texture.pixels");
            var v = Judge(ps: ps);
            Assert.That(v.Answers, Has.Some.StartsWith("Q4: " + V.Broken));
            Assert.That(v.Spike, Is.EqualTo(V.No));
        }

        [Test]
        public void Q4_LogsAreAddedToAnIntactAnswer()
        {
            var ps = Processes();
            Rounds(ps, V.Ac5).First(r => r.PlayIndex == 0).ErrorLogs = 2;
            var v = Judge(ps: ps);
            Assert.That(v.Answers, Has.Member("Q4: " + V.Intact + " (log)"));
            Assert.That(v.Spike, Is.EqualTo(V.Yes));
        }

        // ───── (h)(i)(l) 反復と答えを採る回 ─────

        [Test]
        public void AnswersDifferingAcrossProcessesAreUndecided()
        {
            var ps = Processes();
            ps.First(p => p.Process == V.ClassFirst).Rounds.First(r => r.Round == V.Ac3).Calls.First(c => c.Call == "overlay:SceneEdit").Version = E.V1;
            AssertUndecided(Judge(ps: ps), "AC3: answers differ across processes");
        }

        [Test]
        public void MissingProcessIsUndecided()
        {
            var ps = Processes();
            ps.RemoveAll(p => p.Process == V.ControlReadFirst);
            AssertUndecided(Judge(ps: ps), "ControlReadFirst: no finished record");
        }

        [Test]
        public void RoundsOutOfPlanAreUndecided()
        {
            var ps = Processes();
            var rounds = ps[0].Rounds;
            (rounds[0].PlayIndex, rounds[1].PlayIndex) = (rounds[1].PlayIndex, rounds[0].PlayIndex);
            AssertUndecided(Judge(ps: ps), "differ from the plan");
        }

        // ───── (j)・設定・登録の順・開始時の状態 ─────

        [Test]
        public void RootIdentityMismatchIsUndecided()
        {
            var ps = Processes();
            ps[2].Rounds[4].RootsMatched = false;
            AssertUndecided(Judge(ps: ps), "root BuildIdentity");
        }

        [Test]
        public void RegisterOrderDifferentFromRoundIsUndecided()
        {
            var ps = Processes();
            foreach (var r in Rounds(ps, V.Ac6Class)) r.RegisterOrder = new List<string> { V.Base, V.Overlay };
            AssertUndecided(Judge(ps: ps), "Register order");
        }

        [Test]
        public void DomainReloadEnabledOrSourceLeftIsUndecided()
        {
            var ps = Processes();
            ps[1].DomainReloadDisabled = false;
            ps[3].Rounds[2].SourceInAssetDatabase = true;
            var v = Judge(ps: ps);
            AssertUndecided(v, "Domain Reload");
            AssertUndecided(v, "fixture source in AssetDatabase");
        }

        // ───── Unity の拒否を答えにする条件 ─────

        [Test]
        public void LoadFailureWithoutSingleRegistrationFirstPlayIsUndecided()
        {
            var ps = Processes();
            foreach (var r in Rounds(ps, V.Ac4)) r.Calls.Add(new CallObservation { Call = "base:SceneKeep", Succeeded = false });
            AssertUndecided(Judge(ps: ps), "base:SceneKeep failed and has no success");
        }

        [Test]
        public void LoadFailureWithSingleRegistrationSuccessIsNo()
        {
            var ps = Processes();
            foreach (var r in Rounds(ps, V.Ac4)) r.Calls.Add(new CallObservation { Call = "base:SceneKeep", Succeeded = false });
            ps.Add(Process(V.BaseAloneFirst));
            var v = Judge(ps: ps);
            Assert.That(v.Outcome, Is.EqualTo(V.Go), string.Join("\n", v.Reasons));
            Assert.That(v.Answers, Has.Member("Q2: " + V.CannotPrefix + "base:SceneKeep"));
            Assert.That(v.Spike, Is.EqualTo(V.No));
        }

        [Test]
        public void SingleRegistrationSuccessOnlyAfterOtherRoundsDoesNotCount()
        {
            // AC2 の成功（全ての round の後）は根拠にしない。単独の登録の round が最初の Play でない process は数えない。
            var ps = Processes();
            foreach (var r in Rounds(ps, V.Ac5)) r.Calls.Add(new CallObservation { Call = "overlay:register", Succeeded = false });
            var alone = Process(V.OverlayAloneFirst);
            alone.Rounds[0].PlayIndex = 1;
            ps.Add(alone);
            var v = Judge(ps: ps);
            AssertUndecided(v, "overlay:register failed and has no success");
        }
    }
}
