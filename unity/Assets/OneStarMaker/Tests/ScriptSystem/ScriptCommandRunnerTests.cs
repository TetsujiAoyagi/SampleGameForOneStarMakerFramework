#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Runtime.ScriptSystem;

namespace OneStarMaker.Tests.ScriptSystem
{
    [TestFixture]
    public sealed class ScriptCommandRunnerTests
    {
        [Test]
        public void Constructor_RejectsInvalidInputs_AndDoesNotNotifyOrExecute()
        {
            var host = new Host();
            var program = Program(ScriptInstruction.HostCommand(0, 0));
            Assert.Throws<ArgumentNullException>(() => new ScriptCommandRunner(null!, 0, host, ScriptCommandTimeSource.Unscaled, 1));
            Assert.Throws<ArgumentNullException>(() => new ScriptCommandRunner(program, 0, null!, ScriptCommandTimeSource.Unscaled, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScriptCommandRunner(program, -1, host, ScriptCommandTimeSource.Unscaled, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScriptCommandRunner(program, 0, host, ScriptCommandTimeSource.Unscaled, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScriptCommandRunner(program, 0, host, ScriptCommandTimeSource.Unscaled, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScriptCommandRunner(program, 0, host, (ScriptCommandTimeSource)2, 1));
            var notifications = 0;
            var runner = new ScriptCommandRunner(program, 0, host, ScriptCommandTimeSource.Unscaled, 1, (_, __) => notifications++);
            runner.OnElementStart();
            runner.OnElementLateUpdate(Frame());
            runner.OnElementUpdate(Frame(paused: true));
            Assert.That(runner.State, Is.EqualTo(ScriptCommandRunState.Running));
            Assert.That(host.Calls, Is.Empty);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void WaitFactory_UsesTheReservedId_AndRejectsNegativeDuration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ScriptCommands.WaitMilliseconds(-1));
            var instruction = ScriptCommands.WaitMilliseconds(long.MaxValue);
            Assert.That(instruction.Opcode, Is.EqualTo(ScriptOpcode.HostCommand));
            Assert.That(instruction.Destination, Is.EqualTo(-1));
            Assert.That(instruction.Immediate, Is.EqualTo(long.MaxValue));
            var runner = Runner(new Host(), null, null, instruction);
            runner.OnElementUpdate(Frame(100));
            runner.OnElementUpdate(Frame(0.5f));
            Assert.That(runner.State, Is.EqualTo(ScriptCommandRunState.Waiting));
        }

        [Test]
        public void Update_UsesFixedBudget_AndStartsAtMostOneCommand()
        {
            var host = new Host();
            var runner = new ScriptCommandRunner(Program(ScriptInstruction.LoadImmediate(0, 9),
                ScriptInstruction.HostCommand(7, 12), ScriptInstruction.HostCommand(8, 13), ScriptInstruction.Halt()),
                1, host, ScriptCommandTimeSource.Scaled, 1);
            runner.OnElementUpdate(Frame(100));
            Assert.That(host.Calls, Is.Empty);
            runner.OnElementUpdate(Frame(100));
            Assert.That(host.Calls, Is.EqualTo(new[] { "7:12" }));
            runner.OnElementUpdate(Frame(100));
            Assert.That(host.Calls, Is.EqualTo(new[] { "7:12", "8:13" }));
            runner.OnElementUpdate(Frame());
            Assert.That(runner.State, Is.EqualTo(ScriptCommandRunState.Halted));
        }

        [TestCase(ScriptCommandTimeSource.Scaled)]
        [TestCase(ScriptCommandTimeSource.Unscaled)]
        public void Wait_UsesChosenTime_RespectsPause_AndDoesNotCatchUp(ScriptCommandTimeSource source)
        {
            var host = new Host();
            var runner = new ScriptCommandRunner(Program(ScriptCommands.WaitMilliseconds(500),
                ScriptInstruction.HostCommand(7, 0), ScriptInstruction.HostCommand(8, 0)), 0, host, source, 16);
            runner.OnElementUpdate(Frame(100, 100));
            Assert.That(runner.State, Is.EqualTo(ScriptCommandRunState.Waiting));
            runner.OnElementUpdate(Frame(100, 100, true));
            runner.OnElementUpdate(source == ScriptCommandTimeSource.Scaled ? Frame(0, 10) : Frame(10, 0));
            runner.OnElementUpdate(Frame(0.49f, 0.49f));
            Assert.That(runner.State, Is.EqualTo(ScriptCommandRunState.Waiting));
            runner.OnElementUpdate(Frame(10, 10));
            Assert.That(runner.State, Is.EqualTo(ScriptCommandRunState.Running));
            Assert.That(host.Calls, Is.Empty);
            runner.OnElementUpdate(Frame(10, 10));
            Assert.That(host.Calls, Is.EqualTo(new[] { "7:0" }));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Wait_IgnoresInvalidDelta_AndCompletesAtTheBoundary(float delta)
        {
            var host = new Host();
            var states = new List<ScriptCommandRunState>();
            var runner = Runner(host, (_, state) => states.Add(state), null, ScriptCommands.WaitMilliseconds(500));
            runner.OnElementUpdate(Frame());
            runner.OnElementUpdate(Frame(delta));
            Assert.That(runner.State, Is.EqualTo(ScriptCommandRunState.Waiting));
            runner.OnElementUpdate(Frame(0.5f));
            Assert.That(states, Is.EqualTo(new[] { ScriptCommandRunState.Waiting, ScriptCommandRunState.Running }));
            runner.OnElementUpdate(Frame());
            Assert.That(runner.State, Is.EqualTo(ScriptCommandRunState.Halted));
            Assert.That(host.Calls, Is.Empty);
        }

        [Test]
        public void WaitZero_DoesNotNotifyWaiting_AndDoesNotDispatchTheNextCommand()
        {
            var host = new Host();
            var states = new List<ScriptCommandRunState>();
            var runner = Runner(host, (_, state) => states.Add(state), null,
                ScriptCommands.WaitMilliseconds(0), ScriptInstruction.HostCommand(0, 0));
            runner.OnElementUpdate(Frame(100));
            Assert.That(runner.State, Is.EqualTo(ScriptCommandRunState.Running));
            Assert.That(states, Is.Empty);
            Assert.That(host.Calls, Is.Empty);
            runner.OnElementUpdate(Frame());
            Assert.That(host.Calls.Count, Is.EqualTo(1));
        }

        [TestCase(-2, 0L, 0)]
        [TestCase(int.MinValue, 0L, 0)]
        [TestCase(-1, -1L, 0)]
        [TestCase(0, 0L, 0)]
        [TestCase(1, 0L, 99)]
        public void RejectedCommand_FailsClosed_WithoutRetry(int id, long argument, int resultValue)
        {
            var host = new Host { ExecuteBody = (_, __) => (ScriptCommandResult)resultValue };
            var runner = Runner(host, null, null, ScriptInstruction.HostCommand(id, argument), ScriptInstruction.HostCommand(9, 0));
            runner.OnElementUpdate(Frame());
            runner.OnElementUpdate(Frame());
            Assert.That(runner.State, Is.EqualTo(ScriptCommandRunState.Failed));
            Assert.That(host.Calls.Count, Is.EqualTo(id < 0 ? 0 : 1));
            AssertReleased(runner);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void HostStoppingThenReturnOrThrow_MaintainsStopped(bool dispose, bool throws)
        {
            var host = new Host();
            var releases = 0;
            var states = new List<ScriptCommandRunState>();
            var runner = Runner(host, (_, state) => states.Add(state), _ => releases++,
                ScriptInstruction.HostCommand(0, 0), ScriptInstruction.HostCommand(1, 0));
            host.ExecuteBody = (_, __) =>
            {
                runner.OnElementUpdate(Frame());
                if (dispose) runner.Dispose(); else runner.Stop();
                if (throws) throw new InvalidOperationException();
                return ScriptCommandResult.Completed;
            };
            Assert.DoesNotThrow(() => runner.OnElementUpdate(Frame()));
            runner.OnElementUpdate(Frame());
            Assert.That(host.Calls.Count, Is.EqualTo(1));
            Assert.That(states, Is.EqualTo(new[] { ScriptCommandRunState.Stopped }));
            Assert.That(releases, Is.EqualTo(1));
            AssertReleased(runner);
        }

        [Test]
        public void HostThrow_AfterAnEffect_IsNotRetried_AndDoesNotStopOtherElements()
        {
            var host = new Host { ExecuteBody = (_, __) => throw new InvalidOperationException() };
            var runner = Runner(host, null, null, ScriptInstruction.HostCommand(0, 0), ScriptInstruction.HostCommand(1, 0));
            var later = new CountingElement();
            var elements = new IUpdateElement[] { runner, later };
            Assert.DoesNotThrow(() => { foreach (var element in elements) element.OnElementUpdate(Frame()); });
            runner.OnElementUpdate(Frame());
            Assert.That(later.Calls, Is.EqualTo(1));
            Assert.That(host.Calls.Count, Is.EqualTo(1));
            Assert.That(runner.State, Is.EqualTo(ScriptCommandRunState.Failed));
        }

        [TestCase(ScriptCommandRunState.Waiting, 0)]
        [TestCase(ScriptCommandRunState.Running, 0)]
        [TestCase(ScriptCommandRunState.Halted, 0)]
        [TestCase(ScriptCommandRunState.Waiting, 1)]
        [TestCase(ScriptCommandRunState.Waiting, 2)]
        [TestCase(ScriptCommandRunState.Running, 1)]
        [TestCase(ScriptCommandRunState.Running, 2)]
        [TestCase(ScriptCommandRunState.Running, 3)]
        [TestCase(ScriptCommandRunState.Halted, 1)]
        [TestCase(ScriptCommandRunState.Halted, 2)]
        [TestCase(ScriptCommandRunState.Halted, 3)]
        [TestCase(ScriptCommandRunState.Waiting, 3)]
        public void ObserverThrowOrStop_ReentryIsIsolated_AndCleanupRunsOnce(ScriptCommandRunState trigger, int action)
        {
            var host = new Host();
            var releases = 0;
            var notifications = 0;
            var runner = Runner(host, (current, state) =>
            {
                notifications++;
                current.OnElementUpdate(Frame(100));
                if (state != trigger) return;
                if (action == 1 || action == 3) current.Stop();
                if (action == 2) current.Dispose();
                if (action == 0 || action == 3) throw new InvalidOperationException();
            }, current => { releases++; current.Dispose(); throw new InvalidOperationException(); },
                ScriptCommands.WaitMilliseconds(1), ScriptInstruction.Halt());
            Assert.DoesNotThrow(() =>
            {
                for (var i = 0; i < 4; i++) runner.OnElementUpdate(Frame(0.001f));
                runner.Stop();
                runner.Dispose();
            });
            var expected = trigger == ScriptCommandRunState.Halted ? ScriptCommandRunState.Halted
                : action == 0 ? ScriptCommandRunState.Failed : ScriptCommandRunState.Stopped;
            Assert.That(runner.State, Is.EqualTo(expected));
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(trigger == ScriptCommandRunState.Waiting ? 1 : trigger == ScriptCommandRunState.Running ? 2 : 3));
            AssertReleased(runner);
        }

        [Test]
        public void StopBeforeStartOrWhileWaiting_BlocksDelayedUpdates_AndFreshRunsUseFreshRegisters()
        {
            var host = new Host();
            var stopped = Runner(host, null, null, ScriptInstruction.HostCommand(99, 0));
            stopped.Stop();
            stopped.OnElementUpdate(Frame());
            var waiting = Runner(host, null, null, ScriptCommands.WaitMilliseconds(1), ScriptInstruction.HostCommand(99, 0));
            waiting.OnElementUpdate(Frame());
            waiting.Dispose();
            waiting.OnElementUpdate(Frame(100));
            var program = Program(ScriptInstruction.JumpIfZero(0, 3), ScriptInstruction.HostCommand(99, 0),
                ScriptInstruction.Halt(), ScriptInstruction.LoadImmediate(0, 1), ScriptInstruction.HostCommand(7, 0));
            for (var i = 0; i < 2; i++)
            {
                var fresh = new ScriptCommandRunner(program, 1, host, ScriptCommandTimeSource.Unscaled, 16);
                fresh.OnElementUpdate(Frame());
                fresh.OnElementUpdate(Frame());
                Assert.That(fresh.State, Is.EqualTo(ScriptCommandRunState.Halted));
            }
            Assert.That(host.Calls, Is.EqualTo(new[] { "7:0", "7:0" }));
        }

        private static ScriptProgram Program(params ScriptInstruction[] instructions) => ScriptProgram.Create(instructions);
        private static UpdateFrameContext Frame(float scaled = 0, float? unscaled = null, bool paused = false)
            => new UpdateFrameContext(0, scaled, unscaled ?? scaled, 1, paused);
        private static ScriptCommandRunner Runner(Host host,
            Action<ScriptCommandRunner, ScriptCommandRunState>? observer, Action<ScriptCommandRunner>? release,
            params ScriptInstruction[] instructions)
            => new ScriptCommandRunner(Program(instructions), 0, host, ScriptCommandTimeSource.Unscaled, 16, observer, release);
        private static void AssertReleased(ScriptCommandRunner runner)
        {
            // 寿命の検証だけで内部参照を読む。テストのための公開 getter は設けない。
            foreach (var field in new[] { "_host", "_stateChanged", "_releaseRegistration", "_currentRequest" })
                Assert.That(typeof(ScriptCommandRunner).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runner), Is.Null);
        }
        private sealed class Host : IScriptCommandHost
        {
            public readonly List<string> Calls = new List<string>();
            public Func<int, long, ScriptCommandResult>? ExecuteBody;
            public ScriptCommandResult Execute(int id, long argument)
            {
                Calls.Add(id + ":" + argument);
                return ExecuteBody?.Invoke(id, argument) ?? ScriptCommandResult.Completed;
            }
        }
        private sealed class CountingElement : IUpdateElement
        {
            public int Calls;
            public void OnElementStart() { }
            public void OnElementUpdate(in UpdateFrameContext context) => Calls++;
            public void OnElementLateUpdate(in UpdateFrameContext context) { }
        }
    }
}
