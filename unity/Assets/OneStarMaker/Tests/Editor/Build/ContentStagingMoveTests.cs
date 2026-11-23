#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Editor.Build.Content;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// staging → final の移動の再試行（<see cref="ContentStagingMove"/>）。実 file は temp の下に作り（監視の有無に左右されない）、
    /// 時間は注入して進める（待ちで寝ない）。開いた handle で directory の移動が拒まれるのは Windows の振る舞いなので、
    /// 実 file system の lock を使う test は Windows だけで走らせ、分岐は注入した移動で全ての OS で確かめる。
    /// </summary>
    public sealed class ContentStagingMoveTests
    {
        private static readonly int AccessDenied = unchecked((int)0x80070005);
        private string _root = "";
        private string _staging = "";
        private string _final = "";

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "osm-staging-move-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            _staging = Path.Combine(_root, "staging", "id");
            _final = Path.Combine(_root, "content", "id");
            Directory.CreateDirectory(_staging);
            Directory.CreateDirectory(Path.GetDirectoryName(_final)!);
            File.WriteAllText(Path.Combine(_staging, "a.txt"), "alpha");
            Directory.CreateDirectory(Path.Combine(_staging, "sub"));
            File.WriteAllText(Path.Combine(_staging, "sub", "b.txt"), "beta");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private static IOException Transient() => new IOException("Access to the path is denied.", AccessDenied);

        // 注入の記録。移動・待ち・時計・log の呼び出しを数え、時計は待ちの量だけ進める（Thread.Sleep は使わない）。
        private sealed class Probe
        {
            public int Moves, Waits, ClockReads;
            public long Now, Waited;
            public readonly List<string> Logs = new();
            public Func<int, Exception?> Fail = _ => null;
            public Action<int> OnWait = _ => { };
            public long AdvancePerWait = ContentStagingMove.WaitMilliseconds;

            public void Run(string staging, string final, bool windows = true) => ContentStagingMove.Move(staging, final,
                (s, f) =>
                {
                    Moves++;
                    var failure = Fail(Moves);
                    if (failure != null) throw failure;
                    Directory.Move(s, f);
                },
                ms => { Waits++; Waited += ms; Now += AdvancePerWait; OnWait(Waits); },
                () => { ClockReads++; return Now; }, Logs.Add, windows);
        }

        private void AssertMovedIntact()
        {
            Assert.That(Directory.Exists(_staging), Is.False, "staging は無い");
            Assert.That(File.ReadAllText(Path.Combine(_final, "a.txt")), Is.EqualTo("alpha"));
            Assert.That(File.ReadAllText(Path.Combine(_final, "sub", "b.txt")), Is.EqualTo("beta"));
            Assert.That(Directory.GetFiles(_final, "*", SearchOption.AllDirectories).Length, Is.EqualTo(2));
        }

        // ─── AC1・AC2: 開いた handle が待ちの間に閉じれば移動は成功する ───

        [Test, Platform(Include = "Win")]
        public void OpenFileInStaging_ClosedDuringTheFirstWait_MovesOnTheSecondAttempt()
        {
            var handle = new FileStream(Path.Combine(_staging, "sub", "b.txt"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Exception? first = null, moveError = null;
            var moves = 0;
            var waits = 0;
            var logs = new List<string>();
            try
            {
                ContentStagingMove.Move(_staging, _final,
                    (s, f) =>
                    {
                        moves++;
                        try { Directory.Move(s, f); }
                        catch (Exception error) { first ??= error; throw; }
                    },
                    _ => { waits++; handle.Dispose(); }, () => 0, logs.Add, true);
            }
            catch (Exception error) { moveError = error; }
            finally { handle.Dispose(); }

            // AC2: 実物（この Editor の runtime）が返した 1 回目の失敗。観測した値を出力に書いてから、述語がそれを一時的と読むことを確かめる。
            Assert.That(first, Is.Not.Null, "開いた handle で 1 回目の移動が失敗する");
            Debug.Log("[ContentStagingMoveTests] AC2 observed first failure: type=" + first!.GetType().FullName
                + " HResult=0x" + first.HResult.ToString("X8") + " message=" + first.Message);
            Assert.That(first, Is.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>());
            Assert.That(ContentStagingMove.IsTransient(first), Is.True,
                "観測した失敗が述語に入らない時は述語に足さず Phase A へ差し戻す（HANDOFF §4）");
            Assert.That(moveError, Is.Null, moveError?.ToString());

            Assert.That(moves, Is.EqualTo(2));
            Assert.That(waits, Is.EqualTo(1));
            AssertMovedIntact();
            Assert.That(logs.Single(), Does.StartWith(ContentStagingMove.LogPrefix).And.Contain("2 attempts").And.Contain(_final));
        }

        [Test]
        public void InjectedTransientFailure_ThenSuccess_MovesOnTheSecondAttempt()
        {
            var probe = new Probe { Fail = n => n == 1 ? Transient() : null };
            probe.Run(_staging, _final);
            Assert.That(probe.Moves, Is.EqualTo(2));
            Assert.That(probe.Waits, Is.EqualTo(1));
            AssertMovedIntact();
            Assert.That(probe.Logs.Single(), Does.Contain("2 attempts").And.Contain(_final));
        }

        [Test]
        public void TransientPredicate_TakesOnlyAccessSharingAndLockViolations()
        {
            Assert.That(ContentStagingMove.IsTransient(new IOException("x", AccessDenied)), Is.True);
            Assert.That(ContentStagingMove.IsTransient(new IOException("x", unchecked((int)0x80070020))), Is.True);
            Assert.That(ContentStagingMove.IsTransient(new IOException("x", unchecked((int)0x80070021))), Is.True);
            Assert.That(ContentStagingMove.IsTransient(new UnauthorizedAccessException("x")), Is.True);
            Assert.That(ContentStagingMove.IsTransient(new IOException("default HResult")), Is.False, "Win32 の値の無い IOException");
            Assert.That(ContentStagingMove.IsTransient(new IOException("x", unchecked((int)0x80070011))), Is.False, "別の volume（ERROR_NOT_SAME_DEVICE）");
            Assert.That(ContentStagingMove.IsTransient(new DirectoryNotFoundException("x")), Is.False);
            Assert.That(ContentStagingMove.IsTransient(new InvalidOperationException("x")), Is.False);
        }

        [Test]
        public void OutsideWindows_TheSameFailureIsNotRetried()
        {
            var failure = Transient();
            var probe = new Probe { Fail = _ => failure };
            Assert.That(Assert.Throws<IOException>(() => probe.Run(_staging, _final, windows: false)), Is.SameAs(failure));
            Assert.That(probe.Moves, Is.EqualTo(1));
            Assert.That(probe.Waits, Is.EqualTo(0));
        }

        // ─── AC3: 失敗の後に final が現れたら移し直さない ───

        [Test]
        public void FinalAppearsWithTheFailure_FailsWithoutWaiting_AndKeepsBoth()
        {
            var failure = Transient();
            var probe = new Probe
            {
                Fail = _ =>
                {
                    Directory.CreateDirectory(_final);
                    File.WriteAllText(Path.Combine(_final, "other.txt"), "someone else");
                    return failure;
                },
            };
            Assert.That(Assert.Throws<IOException>(() => probe.Run(_staging, _final)), Is.SameAs(failure));
            Assert.That(probe.Moves, Is.EqualTo(1));
            Assert.That(probe.Waits, Is.EqualTo(0));
            Assert.That(File.ReadAllText(Path.Combine(_final, "other.txt")), Is.EqualTo("someone else"));
            Assert.That(Directory.GetFileSystemEntries(_final).Length, Is.EqualTo(1), "final に混ぜない");
            Assert.That(Directory.Exists(_staging), Is.True, "staging の片付けは呼び手の仕事");
        }

        [Test]
        public void FinalAppearsDuringTheWait_FailsBeforeTheNextMove_AndKeepsBoth()
        {
            var failure = Transient();
            var probe = new Probe
            {
                Fail = _ => failure,
                OnWait = _ =>
                {
                    Directory.CreateDirectory(_final);
                    File.WriteAllText(Path.Combine(_final, "other.txt"), "someone else");
                },
            };
            Assert.That(Assert.Throws<IOException>(() => probe.Run(_staging, _final)), Is.SameAs(failure));
            Assert.That(probe.Moves, Is.EqualTo(1), "待ちの後の検査で止まり、次の移動を呼ばない");
            Assert.That(probe.Waits, Is.EqualTo(1));
            Assert.That(File.ReadAllText(Path.Combine(_final, "other.txt")), Is.EqualTo("someone else"));
            Assert.That(Directory.Exists(_staging), Is.True);
        }

        // ─── AC4: 失敗の後に staging が無ければ、final の有無によらず失敗を返す ───

        [Test]
        public void StagingVanishesWithTheFailure_AndNoFinal_Fails()
        {
            var failure = Transient();
            var probe = new Probe { Fail = _ => { Directory.Delete(_staging, true); return failure; } };
            Assert.That(Assert.Throws<IOException>(() => probe.Run(_staging, _final)), Is.SameAs(failure));
            Assert.That(probe.Moves, Is.EqualTo(1));
            Assert.That(probe.Waits, Is.EqualTo(0));
            Assert.That(Directory.Exists(_final), Is.False);
            Assert.That(probe.Logs, Is.Empty);
        }

        [Test]
        public void StagingVanishesWithTheFailure_AndFinalExists_FailsAndLeavesFinalUntouched()
        {
            var failure = Transient();
            // 失敗を返した移動の後に、staging が無く揃った final がある。帰属を確かめられないので成功とは報告しない。
            var probe = new Probe { Fail = _ => { Directory.Move(_staging, _final); return failure; } };
            var before = Contents(_staging);
            Assert.That(Assert.Throws<IOException>(() => probe.Run(_staging, _final)), Is.SameAs(failure));
            Assert.That(probe.Moves, Is.EqualTo(1));
            Assert.That(probe.Waits, Is.EqualTo(0));
            Assert.That(Contents(_final), Is.EqualTo(before), "final は消さず、bytes も変えない");
            Assert.That(probe.Logs, Is.Empty);
        }

        [Test]
        public void StagingVanishesDuringTheWait_FailsBeforeTheNextMove_WithOrWithoutFinal()
        {
            foreach (var withFinal in new[] { false, true })
            {
                TearDown();
                SetUp();
                var failure = Transient();
                var probe = new Probe
                {
                    Fail = _ => failure,
                    OnWait = _ =>
                    {
                        if (withFinal) Directory.Move(_staging, _final);
                        else Directory.Delete(_staging, true);
                    },
                };
                Assert.That(Assert.Throws<IOException>(() => probe.Run(_staging, _final)), Is.SameAs(failure), "withFinal=" + withFinal);
                Assert.That(probe.Moves, Is.EqualTo(1), "withFinal=" + withFinal);
                Assert.That(Directory.Exists(_final), Is.EqualTo(withFinal));
                if (withFinal) Assert.That(File.ReadAllText(Path.Combine(_final, "a.txt")), Is.EqualTo("alpha"));
            }
        }

        private static string Contents(string directory)
            => string.Join("\n", Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Select(x => Path.GetRelativePath(directory, x) + "=" + File.ReadAllText(x)).OrderBy(x => x, StringComparer.Ordinal));

        // ─── AC5: 恒久の失敗を遅らせない、上限を守る ───

        [Test]
        public void FirstMoveSucceeds_CallsNeitherWaitNorClock()
        {
            var probe = new Probe();
            probe.Run(_staging, _final);
            Assert.That(probe.Moves, Is.EqualTo(1));
            Assert.That(probe.Waits, Is.EqualTo(0));
            Assert.That(probe.ClockReads, Is.EqualTo(0));
            Assert.That(probe.Logs, Is.Empty, "1 回で済んだ移動は log を出さない");
            AssertMovedIntact();
        }

        [Test]
        public void NonTransientFailure_FailsOnceWithoutWaiting()
        {
            foreach (var failure in new Exception[] { new IOException("cross volume", unchecked((int)0x80070011)), new IOException("no HResult"),
                new DirectoryNotFoundException("gone") })
            {
                var probe = new Probe { Fail = _ => failure };
                Assert.That(Assert.Catch(() => probe.Run(_staging, _final)), Is.SameAs(failure), failure.Message);
                Assert.That(probe.Moves, Is.EqualTo(1), failure.Message);
                Assert.That(probe.Waits, Is.EqualTo(0), failure.Message);
            }
            Assert.That(Directory.Exists(_staging), Is.True);
        }

        [Test]
        public void TransientFailuresContinue_StopAtTheBudget_WithoutWaitingPastIt()
        {
            var last = (IOException?)null;
            var probe = new Probe { Fail = n => last = new IOException("denied #" + n, AccessDenied) };
            var error = Assert.Throws<IOException>(() => probe.Run(_staging, _final))!;
            Assert.That(probe.Waited, Is.LessThanOrEqualTo(ContentStagingMove.BudgetMilliseconds));
            Assert.That(probe.Waits, Is.EqualTo(40));
            Assert.That(probe.Moves, Is.EqualTo(40), "経過 2,000 ms の時点で 41 回目を始めない");
            AssertExhausted(error, last!, probe.Moves);
            Assert.That(Directory.Exists(_staging), Is.True);
            Assert.That(Directory.Exists(_final), Is.False);
        }

        [Test]
        public void FailureAt1960Ms_StopsWithoutWaiting()
        {
            var last = (IOException?)null;
            var probe = new Probe();
            probe.Fail = n => { probe.Now = 1960; return last = new IOException("denied", AccessDenied); };
            var error = Assert.Throws<IOException>(() => probe.Run(_staging, _final))!;
            Assert.That(probe.Moves, Is.EqualTo(1));
            Assert.That(probe.Waits, Is.EqualTo(0), "1,960 + 50 は 2,000 を超えるので待たない");
            AssertExhausted(error, last!, 1);
        }

        [Test]
        public void ClockDoesNotAdvance_StopsAfterTheFortyFirstFailure_WithoutAnotherWait()
        {
            var last = (IOException?)null;
            var probe = new Probe { AdvancePerWait = 0, Fail = n => last = new IOException("denied #" + n, AccessDenied) };
            var error = Assert.Throws<IOException>(() => probe.Run(_staging, _final))!;
            Assert.That(probe.Moves, Is.EqualTo(ContentStagingMove.MaxAttempts));
            Assert.That(probe.Waits, Is.EqualTo(ContentStagingMove.MaxAttempts - 1), "41 回目の失敗の後は待たない");
            Assert.That(probe.Waited, Is.LessThanOrEqualTo(ContentStagingMove.BudgetMilliseconds));
            AssertExhausted(error, last!, ContentStagingMove.MaxAttempts);
        }

        [Test]
        public void WaitOversleeps_TheNextAttemptIsNotStartedPastTheBudget()
        {
            // Thread.Sleep が要求より長く寝た時。要求した待ちの合計は上限を超えず、経過が上限に達したら次の試行を始めない。
            var last = (IOException?)null;
            var probe = new Probe { AdvancePerWait = 700, Fail = n => last = new IOException("denied #" + n, AccessDenied) };
            var error = Assert.Throws<IOException>(() => probe.Run(_staging, _final))!;
            Assert.That(probe.Moves, Is.EqualTo(3), "0・700・1,400 ms に試行し、2,100 ms では始めない");
            Assert.That(probe.Waits, Is.EqualTo(3));
            Assert.That(probe.Waited, Is.EqualTo(150));
            AssertExhausted(error, last!, 3);
        }

        private void AssertExhausted(IOException error, IOException last, int attempts)
        {
            Assert.That(error.GetType(), Is.EqualTo(typeof(IOException)), "呼び手の catch の振り分けを変えない型");
            Assert.That(error.InnerException, Is.SameAs(last));
            Assert.That(error.Message, Does.Contain(attempts + " attempt(s)").And.Contain(_staging).And.Contain(_final).And.Contain(last.Message));
        }

        // ─── AC6 の観測: 上限まで handle が閉じない時、呼び手の後片付けの削除がどうなるか（合否に使わない） ───

        [Test, Platform(Include = "Win")]
        public void HandleNeverCloses_MoveFailsAtTheBudget_AndTheCleanupIsObserved()
        {
            var lockedFile = Path.Combine(_staging, "sub", "b.txt");
            using (new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long now = 0;
                var error = Assert.Throws<IOException>(() => ContentStagingMove.Move(_staging, _final, Directory.Move,
                    ms => now += ms, () => now, _ => { }, true))!;
                Assert.That(error.Message, Does.Contain("attempt(s)").And.Contain(_staging));
                Assert.That(Directory.Exists(_final), Is.False);

                string observed;
                try
                {
                    Directory.Delete(_staging, true);
                    observed = "no exception";
                }
                catch (Exception cleanup)
                {
                    observed = cleanup.GetType().FullName + " HResult=0x" + cleanup.HResult.ToString("X8") + " message=" + cleanup.Message;
                }
                Debug.Log("[ContentStagingMoveTests] AC6 observed cleanup with the handle open: " + observed
                    + "; staging exists=" + Directory.Exists(_staging) + "; locked file exists=" + File.Exists(lockedFile));
            }
        }
    }
}
