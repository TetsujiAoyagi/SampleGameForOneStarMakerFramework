#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Runtime.ScriptSystem;
using SampleGame.OutGame.HpGauge;

namespace OneStarMaker.ScriptCommandOfflineTests
{
    internal static partial class Program
    {
        private static void Runner_ActualSampleAndIndependentRuns()
        {
            for (var run = 0; run < 2; run++)
            {
                var operations = new List<string>();
                var waits = 0;
                var releases = 0;
                using var runner = Runner(HpGaugeScriptProgram.Program,
                    new HpGaugeScriptCommands(() => operations.Add("Damage"), () => operations.Add("Heal")),
                    changed: (_, state) => { if (state == ScriptCommandRunState.Waiting) waits++; },
                    release: _ => releases++);
                for (var frame = 0; frame < 40 && runner.State != ScriptCommandRunState.Halted; frame++)
                    Update(runner, scaled: 0, unscaled: 0.5f);
                Equal(ScriptCommandRunState.Halted, runner.State, "sample termination");
                Equal("Damage,Heal,Damage,Heal,Damage,Heal", string.Join(",", operations), "sample operations");
                Equal(6, waits, "sample Wait starts");
                Equal(1, releases, "halt cleanup");
                Update(runner);
                runner.Stop();
                Equal(6, operations.Count, "halt does not retry");
                Equal(1, releases, "cleanup repeated");
                Console.WriteLine($"Actual sample run {run + 1}: 3 Damage, 3 Heal, {waits} Wait, {runner.State}");
            }
        }

        private static void Runner_ConstructorAndUpdateBounds()
        {
            var code = Code(ScriptInstruction.HostCommand(7, 0), ScriptInstruction.HostCommand(8, 0), ScriptInstruction.Halt());
            var host = new Host();
            Throws<ArgumentNullException>(() => new ScriptCommandRunner(null!, 2, host, ScriptCommandTimeSource.Scaled, 1));
            Throws<ArgumentNullException>(() => new ScriptCommandRunner(code, 2, null!, ScriptCommandTimeSource.Scaled, 1));
            Throws<ArgumentOutOfRangeException>(() => new ScriptCommandRunner(code, -1, host, ScriptCommandTimeSource.Scaled, 1));
            foreach (var budget in new[] { 0, -1 }) Throws<ArgumentOutOfRangeException>(() => Runner(code, host, budget: budget));
            Throws<ArgumentOutOfRangeException>(() => Runner(code, host, time: (ScriptCommandTimeSource)255));
            var notifications = 0;
            using var runner = Runner(code, host, changed: (_, _) => notifications++);
            Equal(ScriptCommandRunState.Running, runner.State, "initial state");
            Equal(0, notifications, "constructor observer");
            var context = new UpdateFrameContext(0, 10, 10, 1, false);
            runner.OnElementStart();
            runner.OnElementLateUpdate(in context);
            Equal(0, host.Calls, "start / late executed commands");
            Update(runner, 10, 10);
            Equal(1, host.Calls, "more than one command in Update");
            Update(runner, 10, 10);
            Equal(2, host.Calls, "second command");
            Update(runner);
            Equal(ScriptCommandRunState.Halted, runner.State, "third Update halt");
            using var budgeted = Runner(Code(ScriptInstruction.LoadImmediate(0, 2), ScriptInstruction.HostCommand(0, 0)), host, budget: 1);
            Update(budgeted);
            Equal(2, host.Calls, "budget ignored before host");
            Update(budgeted);
            Equal(3, host.Calls, "budgeted host");
            using var empty = new ScriptCommandRunner(Code(), 0, host, ScriptCommandTimeSource.Scaled, 1);
            Update(empty);
            Equal(ScriptCommandRunState.Halted, empty.State, "zero register empty program");
        }

        private static void Runner_WaitBoundaryZeroPauseAndTimeSource()
        {
            var host = new Host();
            var code = Code(ScriptCommands.WaitMilliseconds(500), ScriptInstruction.HostCommand(7, 0), ScriptInstruction.Halt());
            var states = new List<ScriptCommandRunState>();
            using var runner = Runner(code, host, changed: (_, state) => states.Add(state));
            Update(runner, paused: true);
            Equal(ScriptCommandRunState.Running, runner.State, "paused before wait");
            Update(runner, 10, 10);
            Equal(ScriptCommandRunState.Waiting, runner.State, "start-frame delta used retroactively");
            Update(runner, 10, 10, paused: true);
            Equal(ScriptCommandRunState.Waiting, runner.State, "paused wait advanced");
            Update(runner, 0.49f, 0.49f);
            Equal(ScriptCommandRunState.Waiting, runner.State, "0.49 seconds completed 0.5 wait");
            Update(runner, 0.02f, 0.02f);
            Equal(ScriptCommandRunState.Running, runner.State, "wait completion state");
            Equal(0, host.Calls, "wait completion ran next command");
            Equal("Waiting,Running", string.Join(",", states), "wait state notifications");
            Update(runner);
            Equal(1, host.Calls, "non-HP host not resumed");
            foreach (var source in new[] { ScriptCommandTimeSource.Scaled, ScriptCommandTimeSource.Unscaled })
            {
                using var timed = Runner(code, time: source);
                Update(timed);
                Update(timed, 10, 10, paused: true);
                Equal(ScriptCommandRunState.Waiting, timed.State, "paused time-source wait");
                Update(timed, 0, 0.5f);
                Equal(source == ScriptCommandTimeSource.Unscaled ? ScriptCommandRunState.Running : ScriptCommandRunState.Waiting,
                    timed.State, "selected delta");
                if (source == ScriptCommandTimeSource.Scaled) Update(timed, 0.5f, 0);
                Equal(ScriptCommandRunState.Running, timed.State, "exact 0.5 boundary");
            }
            states.Clear();
            using var zero = Runner(Code(ScriptCommands.WaitMilliseconds(0), ScriptInstruction.HostCommand(0, 0)), host,
                changed: (_, state) => states.Add(state));
            Update(zero);
            Equal(ScriptCommandRunState.Running, zero.State, "zero wait state");
            Equal(0, states.Count, "zero wait transient Waiting notification");
            Equal(1, host.Calls, "zero wait runs next command in same Update");
            Update(zero);
            Equal(2, host.Calls, "zero wait never resumed");
        }

        private static void Runner_InvalidDeltasAndNoCatchUp()
        {
            foreach (var source in new[] { ScriptCommandTimeSource.Scaled, ScriptCommandTimeSource.Unscaled })
            {
                var host = new Host();
                using var runner = Runner(Code(ScriptCommands.WaitMilliseconds(500), ScriptInstruction.HostCommand(0, 0),
                    ScriptCommands.WaitMilliseconds(500), ScriptInstruction.HostCommand(0, 0)), host, source);
                Update(runner);
                foreach (var delta in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    Update(runner, source == ScriptCommandTimeSource.Scaled ? delta : 10,
                        source == ScriptCommandTimeSource.Unscaled ? delta : 10);
                    Equal(ScriptCommandRunState.Waiting, runner.State, "invalid selected delta advanced wait");
                }
                Update(runner, 10, 10);
                Equal(0, host.Calls, "long delta catches up command");
                Update(runner, 10, 10);
                Equal(1, host.Calls, "long delta catches up multiple commands");
                Update(runner, 10, 10);
                Equal(ScriptCommandRunState.Waiting, runner.State, "excess delta carried to next wait");
                Update(runner, 0.25f, 0.25f);
                Equal(ScriptCommandRunState.Waiting, runner.State, "half wait");
                Update(runner, 0.25f, 0.25f);
                Equal(ScriptCommandRunState.Running, runner.State, "two exact quarters");
            }
            using var large = Runner(Code(ScriptCommands.WaitMilliseconds(long.MaxValue)));
            Update(large);
            Update(large, float.MaxValue, float.MaxValue);
            Equal(ScriptCommandRunState.Running, large.State, "nonnegative long / finite delta conversion");
        }
    }
}
