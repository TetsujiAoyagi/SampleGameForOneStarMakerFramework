#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Runtime.ScriptSystem;
using SampleGame.OutGame.HpGauge;

namespace OneStarMaker.ScriptCommandOfflineTests
{
    internal static partial class Program
    {
        private static void Runner_InvalidCommandsAndResults()
        {
            Throws<ArgumentOutOfRangeException>(() => ScriptCommands.WaitMilliseconds(-1));
            foreach (var command in new[] { ScriptInstruction.HostCommand(-1, -1),
                ScriptInstruction.FromRaw(9, -1, 123, -999, -1), ScriptInstruction.HostCommand(-2, 0), ScriptInstruction.HostCommand(int.MinValue, 0) })
            {
                var host = new Host();
                using var runner = Runner(Code(command, ScriptInstruction.HostCommand(0, 0)), host);
                Update(runner);
                Equal(ScriptCommandRunState.Failed, runner.State, "invalid common command");
                Equal(0, host.Calls, "reserved / invalid Wait dispatched to app");
            }
            foreach (var result in new[] { ScriptCommandResult.Rejected, (ScriptCommandResult)255 })
            {
                var host = new Host { ExecuteBody = (_, _) => result };
                using var runner = Runner(Code(ScriptInstruction.HostCommand(7, 0), ScriptInstruction.HostCommand(8, 0)), host);
                Update(runner);
                Update(runner);
                Equal(ScriptCommandRunState.Failed, runner.State, "rejected / invalid result");
                Equal(1, host.Calls, "rejected result retried");
            }
            var calls = 0;
            foreach (var command in new[] { ScriptInstruction.HostCommand(99, 0), ScriptInstruction.HostCommand(0, 1), ScriptInstruction.HostCommand(1, -1) })
            {
                using var runner = Runner(Code(command), new HpGaugeScriptCommands(() => calls++, () => calls++));
                Update(runner);
                Equal(ScriptCommandRunState.Failed, runner.State, "app invalid ID / argument");
            }
            Equal(0, calls, "invalid app command mutated HP");
            using var invalidOpcode = Runner(Code(ScriptInstruction.FromRaw(255, 0, 0, 0, 0)));
            Update(invalidOpcode);
            Equal(ScriptCommandRunState.Failed, invalidOpcode.State, "VM fault runner state");
        }

        private static void Runner_StopDisposeAndDelayedUpdates()
        {
            foreach (var waiting in new[] { false, true })
            {
                var host = new Host();
                var releases = 0;
                var states = new List<ScriptCommandRunState>();
                using var runner = Runner(Code(ScriptCommands.WaitMilliseconds(500), ScriptInstruction.HostCommand(0, 0)), host,
                    changed: (_, state) => states.Add(state), release: _ => releases++);
                if (waiting) Update(runner);
                runner.Stop();
                runner.Dispose();
                Update(runner, 10, 10);
                Equal(ScriptCommandRunState.Stopped, runner.State, "stopped state");
                Equal(0, host.Calls, "delayed scheduler call after stop");
                Equal(1, releases, "stop / dispose cleanup once");
                Equal(waiting ? "Waiting,Stopped" : "Stopped", string.Join(",", states), "stop notification");
            }
        }

        private static void Runner_ReentrantHostStopAndThrow()
        {
            foreach (var command in new[] { 0, 1 })
            foreach (var dispose in new[] { false, true })
            foreach (var throws in new[] { false, true })
            {
                ScriptCommandRunner? runner = null;
                var calls = 0;
                var releases = 0;
                var states = new List<ScriptCommandRunState>();
                Action operation = () =>
                {
                    calls++;
                    Update(runner!);
                    if (dispose) runner!.Dispose(); else runner!.Stop();
                    if (throws) throw new InvalidOperationException("after stop");
                };
                runner = Runner(Code(ScriptInstruction.HostCommand(command, 0), ScriptCommands.WaitMilliseconds(500),
                    ScriptInstruction.HostCommand(command, 0)), new HpGaugeScriptCommands(operation, operation),
                    changed: (_, state) => states.Add(state), release: _ => releases++);
                Update(runner);
                Update(runner);
                Equal(ScriptCommandRunState.Stopped, runner.State, "host stop return / throw overwritten");
                Equal("Stopped", string.Join(",", states), "host stop produced Waiting / Failed notification");
                Equal(1, calls, "reentrant / old update repeated operation");
                Equal(1, releases, "host stop cleanup");
            }
        }

        private static void Runner_MutateThenThrowNeverRetries()
        {
            foreach (var command in new[] { 0, 1 })
            {
                var mutations = 0;
                var releases = 0;
                Action operation = () => { mutations++; throw new InvalidOperationException("after mutation"); };
                using var runner = Runner(Code(ScriptInstruction.HostCommand(command, 0), ScriptInstruction.HostCommand(command, 0)),
                    new HpGaugeScriptCommands(operation, operation), release: _ => releases++);
                var next = new FollowingElement();
                var context = new UpdateFrameContext(0, 0.5f, 0.5f, 1, false);
                foreach (var element in new IUpdateElement[] { runner, next }) element.OnElementUpdate(in context);
                Equal(1, next.Calls, "following element prevented by operation exception");
                for (var frame = 0; frame < 3; frame++) Update(runner);
                Equal(ScriptCommandRunState.Failed, runner.State, "operation exception not closed");
                Equal(1, mutations, "mutation rolled back / retried / followed by command");
                Equal(1, releases, "operation exception cleanup");
            }
        }

        private static void Runner_ObserverFailuresAndFollowingElement()
        {
            foreach (var target in new[] { ScriptCommandRunState.Waiting, ScriptCommandRunState.Running,
                ScriptCommandRunState.Halted, ScriptCommandRunState.Failed, ScriptCommandRunState.Stopped })
            {
                var releases = 0;
                var throws = 0;
                var program = target == ScriptCommandRunState.Halted ? Code(ScriptInstruction.Halt())
                    : target == ScriptCommandRunState.Failed ? Code(ScriptInstruction.HostCommand(-2, 0))
                    : Code(ScriptCommands.WaitMilliseconds(500), ScriptInstruction.HostCommand(0, 0));
                using var runner = Runner(program, changed: (_, state) =>
                {
                    if (state == target) { throws++; throw new InvalidOperationException("observer"); }
                }, release: _ => { releases++; throw new InvalidOperationException("unregister"); });
                if (target == ScriptCommandRunState.Stopped) runner.Stop();
                else Update(runner);
                var next = new FollowingElement();
                var context = new UpdateFrameContext(0, 0.5f, 0.5f, 1, false);
                // Deliberately no scheduler exception isolation: runner must protect the following element.
                foreach (var element in new IUpdateElement[] { runner, next }) element.OnElementUpdate(in context);
                Equal(1, next.Calls, "following element prevented by callback exception");
                Equal(1, throws, "failed observer re-notified");
                Equal(1, releases, "throwing observer / release cleanup");
                Equal(target == ScriptCommandRunState.Waiting || target == ScriptCommandRunState.Running
                    ? ScriptCommandRunState.Failed : target, runner.State, "observer failure terminal state");
            }
        }

        private static void Runner_ObserverReentryAndCleanup()
        {
            foreach (var target in new[] { ScriptCommandRunState.Waiting, ScriptCommandRunState.Running, ScriptCommandRunState.Halted })
            foreach (var dispose in new[] { false, true })
            {
                var host = new Host();
                var releases = 0;
                var notifications = new List<ScriptCommandRunState>();
                using var runner = Runner(target == ScriptCommandRunState.Halted ? Code(ScriptInstruction.Halt())
                    : Code(ScriptCommands.WaitMilliseconds(500), ScriptInstruction.HostCommand(0, 0)), host,
                    changed: (current, state) =>
                    {
                        notifications.Add(state);
                        Update(current);
                        if (state != target) return;
                        if (dispose) current.Dispose(); else current.Stop();
                        throw new InvalidOperationException("observer after stop");
                    }, release: current => { releases++; current.Stop(); Update(current); });
                Update(runner);
                if (target == ScriptCommandRunState.Running) Update(runner);
                Update(runner);
                Equal(target == ScriptCommandRunState.Halted ? target : ScriptCommandRunState.Stopped, runner.State,
                    "observer stop / throw overwritten");
                Equal(0, host.Calls, "observer reentrant Update executed operation");
                Equal(1, releases, "cleanup reentry repeated release");
                Equal(target == ScriptCommandRunState.Running ? "Waiting,Running" : target.ToString(),
                    string.Join(",", notifications), "nested notification not suppressed");
            }
        }

        private static void Runner_ClosedReferencesAreReleased()
        {
            foreach (var stop in new[] { false, true })
            {
                var closed = CreateClosedRunner(stop);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                foreach (var reference in closed.References) True(!reference.IsAlive, "closed runner retained host / callback target");
                GC.KeepAlive(closed.Runner);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (ScriptCommandRunner Runner, WeakReference[] References) CreateClosedRunner(bool stop)
        {
            var host = new Host();
            var observer = new ThrowingCallbackOwner();
            var release = new ThrowingCallbackOwner();
            var runner = Runner(Code(ScriptInstruction.Halt()), host, changed: observer.Changed, release: release.Release);
            if (stop) runner.Stop(); else Update(runner);
            return (runner, new[] { new WeakReference(host), new WeakReference(observer), new WeakReference(release) });
        }

        private sealed class ThrowingCallbackOwner
        {
            public void Changed(ScriptCommandRunner runner, ScriptCommandRunState state) => throw new InvalidOperationException("observer");
            public void Release(ScriptCommandRunner runner) => throw new InvalidOperationException("release");
        }

        private sealed class FollowingElement : IUpdateElement
        {
            public int Calls;
            public void OnElementStart() { }
            public void OnElementUpdate(in UpdateFrameContext context) => Calls++;
            public void OnElementLateUpdate(in UpdateFrameContext context) { }
        }
    }
}
