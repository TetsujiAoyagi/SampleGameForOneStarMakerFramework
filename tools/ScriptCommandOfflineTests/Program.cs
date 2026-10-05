#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Runtime.ScriptSystem;
using SampleGame.OutGame.HpGauge;

namespace OneStarMaker.ScriptCommandOfflineTests
{
    // One dependency-free harness, split by test subject only. All production types are linked sources.
    internal static partial class Program
    {
        private static int _passed;
        private static int _failed;

        private static int Main()
        {
            Run(nameof(Core_NumericOpcodesAndImmutableProgram), Core_NumericOpcodesAndImmutableProgram);
            Run(nameof(Core_RequestIdentityAndBudgetPriority), Core_RequestIdentityAndBudgetPriority);
            Run(nameof(Core_FailureAndTerminalLatches), Core_FailureAndTerminalLatches);
            Run(nameof(Core_RawOperandsAndNaturalEnd), Core_RawOperandsAndNaturalEnd);
            Run(nameof(Core_NumericAndPendingTicksDoNotAllocate), Core_NumericAndPendingTicksDoNotAllocate);
            Run(nameof(App_ExactProgramAndHostBoundary), App_ExactProgramAndHostBoundary);
            Run(nameof(Runner_ActualSampleAndIndependentRuns), Runner_ActualSampleAndIndependentRuns);
            Run(nameof(Runner_ConstructorAndUpdateBounds), Runner_ConstructorAndUpdateBounds);
            Run(nameof(Runner_WaitBoundaryZeroPauseAndTimeSource), Runner_WaitBoundaryZeroPauseAndTimeSource);
            Run(nameof(Runner_InvalidDeltasAndNoCatchUp), Runner_InvalidDeltasAndNoCatchUp);
            Run(nameof(Runner_InvalidCommandsAndResults), Runner_InvalidCommandsAndResults);
            Run(nameof(Runner_StopDisposeAndDelayedUpdates), Runner_StopDisposeAndDelayedUpdates);
            Run(nameof(Runner_ReentrantHostStopAndThrow), Runner_ReentrantHostStopAndThrow);
            Run(nameof(Runner_MutateThenThrowNeverRetries), Runner_MutateThenThrowNeverRetries);
            Run(nameof(Runner_ObserverFailuresAndFollowingElement), Runner_ObserverFailuresAndFollowingElement);
            Run(nameof(Runner_ObserverReentryAndCleanup), Runner_ObserverReentryAndCleanup);
            Run(nameof(Runner_ClosedReferencesAreReleased), Runner_ClosedReferencesAreReleased);
            Console.WriteLine($"ScriptCommand offline: {_passed} passed, {_failed} failed, {_passed + _failed} executed");
            return _failed == 0 && _passed > 0 ? 0 : 1;
        }

        private static void Run(string name, Action body)
        {
            try { body(); _passed++; Console.WriteLine("PASS " + name); }
            catch (Exception exception) { _failed++; Console.WriteLine("FAIL " + name + "\n" + exception); }
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}");
        }

        private static void True(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }

        private static ScriptProgram Code(params ScriptInstruction[] instructions) => ScriptProgram.Create(instructions);
        private static ScriptMachine Machine(params ScriptInstruction[] instructions) => new ScriptMachine(Code(instructions), new ScriptRegisters(2));
        private static void Update(ScriptCommandRunner runner, float scaled = 0.5f, float unscaled = 0.5f, bool paused = false)
        {
            var context = new UpdateFrameContext(0, scaled, unscaled, 1, paused);
            runner.OnElementUpdate(in context);
        }

        private static ScriptCommandRunner Runner(ScriptProgram program, IScriptCommandHost? host = null,
            ScriptCommandTimeSource time = ScriptCommandTimeSource.Unscaled, int budget = 16,
            Action<ScriptCommandRunner, ScriptCommandRunState>? changed = null, Action<ScriptCommandRunner>? release = null)
            => new ScriptCommandRunner(program, 2, host ?? new Host(), time, budget, changed, release);

        private sealed class Host : IScriptCommandHost
        {
            public int Calls { get; private set; }
            public Func<int, long, ScriptCommandResult>? ExecuteBody;
            public ScriptCommandResult Execute(int commandId, long argument)
            {
                Calls++;
                return ExecuteBody?.Invoke(commandId, argument) ?? ScriptCommandResult.Completed;
            }
        }
    }
}
