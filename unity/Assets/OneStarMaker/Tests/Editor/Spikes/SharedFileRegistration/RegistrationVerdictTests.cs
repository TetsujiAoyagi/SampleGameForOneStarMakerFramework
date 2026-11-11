#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration
{
    /// <summary>
    /// HANDOFF §2 の語と判定定義の写像だけを確かめる（Unity も製品も使わない）。§2 の誤答の経路 (b)(c)(e)(f)(g) が
    /// 答えにならないこと、答えの向きによらず GO になること、各例が「S2 への含意」の正しい行に写ることを見る。
    /// 実験 code であり、Phase D で削除するまで通常の回帰にも入る（HANDOFF §5）。
    /// </summary>
    public sealed class RegistrationVerdictTests
    {
        // ───── 入力の組み立て。既定は「実体 1、全 case 無傷」の GO になる完全な record。 ─────

        private static CountObservation Counted(string kind, int entities, int loads = 2)
        {
            var ids = Enumerable.Range(0, loads).Select(i => kind + (entities == 1 ? 0 : i % entities)).ToList();
            return new CountObservation { Kind = kind, LoadIds = ids, EnumeratedIds = ids.Distinct().ToList() };
        }

        private static RoundObservation Round(string ac, string key, string order, int rep, int entities, string ac4)
        {
            var r = new RoundObservation { Ac = ac, Key = key, Order = order, Repetition = rep, Ended = true, CleanedUp = true };
            if (ac == "AC4") r.Unregister = Case(ac4, key);
            else
            {
                r.Counts.Add(Counted(Kind.Texture, entities));
                r.Counts.Add(Counted(Kind.Material, entities));
                if (ac == "AC2") r.Counts.Add(Counted(Kind.Prefab, entities));
            }
            return r;
        }

        private static UnregisterObservation Case(string outcome, string key)
        {
            var pre = key.EndsWith("U1") || key.EndsWith("U6") || key.EndsWith("U7");
            var u = new UnregisterObservation { HasPreRelease = pre };
            if (outcome == "broken") u.T1Broken.Add("texture.pixels");
            if (outcome == "refused") u.CallLoggedError = true;
            return u;
        }

        private static VerdictInput Input(int entities = 1, string ac4 = "intact")
        {
            var input = new VerdictInput
            {
                Preconditions = new Preconditions { BuildsSucceeded = true, DigestMatched = true, FixtureAbsentAtSecondStart = true, TwinFilesDiffer = true },
                Control = Counted(Kind.Texture, 2),
                Baselines = { Base("A"), Base("B") },
            };
            foreach (var order in RegistrationVerdict.OrderNames) input.Orders.Add(Registered(order));
            foreach (var rep in new[] { 1, 2 })
            foreach (var (ac, key, order) in RegistrationVerdict.ExpectedRounds())
                input.Rounds.Add(Round(ac, key, order, rep, entities, ac4));
            return input;
        }

        private static Baseline Base(string d) => new Baseline { Directory = d, PrefabLoaded = true, CoreCountsOne = true, SceneLoaded = true };

        private static OrderCheck Registered(string order) =>
            new OrderCheck { Order = order, FirstRegistered = true, SecondValid = true, SecondListed = true, CleanedUp = true };

        private static OrderCheck Refused(string order, string excluded = "") =>
            new OrderCheck { Order = order, FirstRegistered = true, SecondValid = false, SecondListed = false, FailureRecorded = true, CleanedUp = true, ExcludedCause = excluded };

        private static RoundObservation Get(VerdictInput input, string key, int rep = 1) => input.Rounds.Single(x => x.Key == key && x.Repetition == rep);

        private static void ExcludeOrder(VerdictInput input, string order)
        {
            input.Orders.RemoveAll(x => x.Order == order);
            input.Orders.Add(Refused(order));
            foreach (var r in input.Rounds.Where(x => x.Order == order))
            {
                r.Exclusion = "r1"; r.Counts.Clear(); r.Unregister = null;
            }
        }

        private static Verdict Judge(VerdictInput input) => RegistrationVerdict.Judge(input);

        // ───── 答えの向きによらず GO ─────

        [Test]
        public void OneEntityAllIntactIsGoOnRowOne()
        {
            var v = Judge(Input());
            Assert.That(v.Outcome, Is.EqualTo(RegistrationVerdict.Go), string.Join("\n", v.Reasons));
            Assert.That(v.Implications, Is.EqualTo(new[] { RegistrationVerdict.RowOneIntact }));
        }

        [Test]
        public void TwoLoadsWithOneDistinctIdAndOneEnumeratedIsOneEntity()
        {
            var o = new CountObservation { Kind = Kind.Texture, LoadIds = { "7", "7" }, EnumeratedIds = { "7" } };
            Assert.That(RegistrationVerdict.Count(o), Is.EqualTo(1));
        }

        [Test]
        public void TwoEntitiesIsGoOnRowTwo()
        {
            var v = Judge(Input(entities: 2));
            Assert.That(v.Outcome, Is.EqualTo(RegistrationVerdict.Go), string.Join("\n", v.Reasons));
            Assert.That(v.Implications, Is.EqualTo(new[] { RegistrationVerdict.RowTwo }));
        }

        [TestCase("refused", RegistrationVerdict.RowRefused)]
        [TestCase("broken", RegistrationVerdict.RowBroken)]
        public void RefusedOrBrokenCasesAreGo(string outcome, string row)
        {
            var v = Judge(Input(ac4: outcome));
            Assert.That(v.Outcome, Is.EqualTo(RegistrationVerdict.Go), string.Join("\n", v.Reasons));
            Assert.That(v.Implications, Is.EqualTo(new[] { row }));
        }

        [Test]
        public void SeveralRowsAreAllReturned()
        {
            var input = Input(entities: 2);
            foreach (var rep in new[] { 1, 2 }) Get(input, "AC4 U3", rep).Unregister!.CallLoggedError = true;
            var v = Judge(input);
            Assert.That(v.Implications, Is.EquivalentTo(new[] { RegistrationVerdict.RowTwo, RegistrationVerdict.RowRefused }));
        }

        // ───── 同時登録の拒否 ─────

        [Test]
        public void BothOrdersRefusedIsGoInStepOneWithoutControlOrRounds()
        {
            var input = Input();
            input.Orders = new List<OrderCheck> { Refused("A>B"), Refused("B>A") };
            input.Control = null;
            input.Rounds.Clear();
            var v = Judge(input);
            Assert.That(v.Outcome, Is.EqualTo(RegistrationVerdict.Go));
            Assert.That(v.Implications, Is.EqualTo(new[] { RegistrationVerdict.RowNoSimultaneous }));
        }

        [Test]
        public void OneOrderRefusedIsARoundAnswerAndNotRowOne()
        {
            var input = Input();
            ExcludeOrder(input, "B>A");
            var v = Judge(input);
            Assert.That(v.Outcome, Is.EqualTo(RegistrationVerdict.Go), string.Join("\n", v.Reasons));
            Assert.That(v.Implications, Does.Contain(RegistrationVerdict.RowNoSimultaneous));
            Assert.That(v.Implications, Does.Not.Contain(RegistrationVerdict.RowOneIntact));
        }

        [Test]
        public void OneOrderRefusedDoesNotHideAnInconsistentRegisteredOrder()
        {
            var input = Input();
            ExcludeOrder(input, "B>A");
            Get(input, "AC2 A>B").Counts[0].EnumeratedIds.Add("extra");
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }

        [TestCase("path")]
        [TestCase("manifest")]
        [TestCase("type-tree")]
        [TestCase("harness")]
        public void ExcludedCausesInOneOrderAreNotRefusalButUndecided(string cause)
        {
            var input = Input();
            input.Orders.RemoveAll(x => x.Order == "B>A");
            input.Orders.Add(Refused("B>A", cause));
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }

        [Test]
        public void HarnessFailureInBothOrdersIsNotStepOne()
        {
            var input = Input();
            input.Orders = new List<OrderCheck> { Refused("A>B", "harness"), Refused("B>A", "harness") };
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }

        [Test]
        public void SkippingAnOrderWithoutTheExclusionIsUndecided()
        {
            var input = Input();
            input.Rounds.RemoveAll(x => x.Order == "B>A");
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }

        // ───── (r2) 両方の登録の下での load の失敗 ─────

        [TestCase("AC2 A>B", "B:prefab")]
        [TestCase("AC3 B>A", "A:scene")]
        public void LoadFailureUnderBothIsARoundAnswerWhenTheBaselineSucceeds(string key, string failure)
        {
            var input = Input();
            foreach (var rep in new[] { 1, 2 }) { var r = Get(input, key, rep); r.LoadFailures.Add(failure); r.Counts.Clear(); }
            var v = Judge(input);
            Assert.That(v.Outcome, Is.EqualTo(RegistrationVerdict.Go), string.Join("\n", v.Reasons));
            Assert.That(v.Implications, Does.Contain(RegistrationVerdict.RowCannotLoad));
            Assert.That(v.Implications, Does.Not.Contain(RegistrationVerdict.RowOneIntact));
        }

        [Test]
        public void LoadFailureThatAlsoFailsInTheBaselineIsUndecided()
        {
            var input = Input();
            input.Baselines[1].PrefabLoaded = false;
            foreach (var rep in new[] { 1, 2 }) Get(input, "AC2 A>B", rep).LoadFailures.Add("B:prefab");
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }

        // ───── 数え方 (b)(c) と整合の規則 ─────

        [Test]
        public void PositiveControlCountingOneIsUndecided()
        {
            var input = Input();
            input.Control = Counted(Kind.Texture, 1);
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }

        [Test]
        public void PositiveControlFilesWithTheSameNameAreUndecided()
        {
            var input = Input();
            input.Preconditions.TwinFilesDiffer = false;
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }

        [Test]
        public void LeftoverAtRoundStartIsUndecided()
        {
            var input = Input();
            Get(input, "AC2 B>A", 2).LeftoverAtStart = 1;
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }

        [TestCase(new[] { "1", "2" }, new[] { "1" })]
        [TestCase(new[] { "1", "1" }, new[] { "1", "2" })]
        [TestCase(new[] { "1", Kind.NullId }, new[] { "1" })]
        [TestCase(new string[0], new string[0])]
        [TestCase(new[] { "1", "2", "3" }, new[] { "1", "2", "3" })]
        public void CountOutsideTheConsistencyRuleIsUnanswered(string[] loads, string[] enumerated)
        {
            var o = new CountObservation { Kind = Kind.Material, LoadIds = loads.ToList(), EnumeratedIds = enumerated.ToList() };
            Assert.That(RegistrationVerdict.Count(o), Is.Null);
        }

        [Test]
        public void MismatchedIdsInARoundAreUndecided()
        {
            var input = Input();
            Get(input, "AC3 A>B").Counts[1].EnumeratedIds = new List<string> { "other" };
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }

        // ───── (d) 同じ file の前提、process の照合 ─────

        [TestCase("shader")]
        [TestCase("texture.resS")]
        public void NotTheSameFileIsUndecided(string item)
        {
            var input = Input();
            input.Preconditions.NotSameFile.Add(item);
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }

        [Test]
        public void DigestMismatchBetweenProcessesIsUndecided()
        {
            var input = Input();
            input.Preconditions.DigestMatched = false;
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }

        // ───── (e)(f) 登録解除の分類 ─────

        [Test]
        public void ContentLostWithoutNullIsBroken()
        {
            var u = new UnregisterObservation { T3Broken = { "material.mainTexture" } };
            Assert.That(RegistrationVerdict.Classify(u, out _), Is.EqualTo(RegistrationVerdict.Broken));
        }

        [Test]
        public void BrokenOnlyAtT2OrT3OrAtTheReloadIsBroken()
        {
            Assert.That(RegistrationVerdict.Classify(new UnregisterObservation { T2Broken = { "prefab.null" } }, out _), Is.EqualTo(RegistrationVerdict.Broken));
            Assert.That(RegistrationVerdict.Classify(new UnregisterObservation { ReloadAfterT3Failed = true }, out _), Is.EqualTo(RegistrationVerdict.Broken));
        }

        [Test]
        public void ErrorLogWithAnIntactObjectIsRefused()
        {
            var u = new UnregisterObservation { CallLoggedError = true };
            Assert.That(RegistrationVerdict.Classify(u, out _), Is.EqualTo("refused (log)"));
        }

        [Test]
        public void DirectoryListedAtT3IsRefused()
        {
            var u = new UnregisterObservation { ListedAtT3 = true };
            Assert.That(RegistrationVerdict.Classify(u, out _), Is.EqualTo("refused (list)"));
        }

        [Test]
        public void BrokenAndRefusedIsBroken()
        {
            var u = new UnregisterObservation { CallLoggedError = true, ListedAtT3 = true, T1Broken = { "texture.width" } };
            Assert.That(RegistrationVerdict.Classify(u, out _), Is.EqualTo(RegistrationVerdict.Broken));
        }

        [Test]
        public void BrokenAtR1IsBrokenBeforeUnregisterWithoutAnUnregisterClass()
        {
            var u = new UnregisterObservation { HasPreRelease = true, R1Broken = { "texture.pixels" }, ListedAtT3 = true };
            Assert.That(RegistrationVerdict.Classify(u, out _), Is.EqualTo(RegistrationVerdict.BrokenBefore));
        }

        [Test]
        public void IntactAtR1GetsTheUnregisterClass()
        {
            var u = new UnregisterObservation { HasPreRelease = true, T3Broken = { "shader.null" } };
            Assert.That(RegistrationVerdict.Classify(u, out _), Is.EqualTo(RegistrationVerdict.Broken));
        }

        [Test]
        public void BrokenAtR0IsUndecided()
        {
            var input = Input();
            Get(input, "AC4 U6").Unregister!.R0Broken.Add("prefab.null");
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }

        [Test]
        public void R1DoesNotUseTheReloadItem()
        {
            // 再 load は T3 の後だけ。R1 が無傷なら、T3 の後の再 load の失敗は登録解除の分類（壊れた）に付く。
            var u = new UnregisterObservation { HasPreRelease = true, ReloadAfterT3Failed = true };
            Assert.That(RegistrationVerdict.Classify(u, out _), Is.EqualTo(RegistrationVerdict.Broken));
        }

        [Test]
        public void ReleasingReferencesBeforeT3IsUndecided()
        {
            var u = new UnregisterObservation { ReferencesHeldToT3 = false, T3Broken = { "prefab.null" } };
            Assert.That(RegistrationVerdict.Classify(u, out _), Is.Null);
        }

        [Test]
        public void BrokenCaseRemovesRowOne()
        {
            var input = Input();
            foreach (var rep in new[] { 1, 2 }) Get(input, "AC4 U5", rep).Unregister!.T2Broken.Add("scene.material");
            var v = Judge(input);
            Assert.That(v.Implications, Is.EqualTo(new[] { RegistrationVerdict.RowBroken }));
        }

        // ───── (g) 両方の順と反復 ─────

        [Test]
        public void DifferentRepetitionsAreUndecided()
        {
            var input = Input();
            Get(input, "AC4 U3", 2).Unregister!.CallLoggedError = true;
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }

        [Test]
        public void DifferentCountsBetweenRepetitionsAreUndecided()
        {
            var input = Input();
            Get(input, "AC2 B>A", 2).Counts[0] = Counted(Kind.Texture, 2);
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }

        [Test]
        public void MissingRoundOrEndLineIsUndecided()
        {
            var input = Input();
            Get(input, "AC3 B>A").Ended = false;
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
            input = Input();
            input.Rounds.Remove(Get(input, "AC4 U7", 2));
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }

        [Test]
        public void UnfinishedCleanupIsUndecided()
        {
            var input = Input();
            Get(input, "AC4 U2").CleanedUp = false;
            Assert.That(Judge(input).Outcome, Is.EqualTo(RegistrationVerdict.Undecided));
        }
    }
}
