#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Runtime.ScriptSystem;
using SampleGame.OutGame.HpGauge;

namespace SampleGame.Tests
{
    [TestFixture]
    public sealed class HpGaugeScriptProgramTests
    {
        [Test]
        public void Program_IsSharedAndMachineRequestsExactlyThreeDamageWaitHealWaitCycles()
        {
            Assert.That(HpGaugeScriptProgram.Program, Is.SameAs(HpGaugeScriptProgram.Program));
            Assert.That(HpGaugeScriptProgram.Program.InstructionCount, Is.EqualTo(9));
            Assert.That(HpGaugeScriptProgram.RegisterCount, Is.EqualTo(2));
            var machine = new ScriptMachine(HpGaugeScriptProgram.Program, new ScriptRegisters(2));
            var commands = new List<int>();
            for (var step = 0; step < 13; step++)
            {
                var status = machine.Tick(16);
                if (status == ScriptMachineStatus.Halted) break;
                Assert.That(status, Is.EqualTo(ScriptMachineStatus.WaitingForHost));
                var request = machine.PendingHostRequest!;
                commands.Add(request.CommandId);
                Assert.That(request.Argument, Is.EqualTo(request.CommandId == -1 ? 500L : 0L));
                Assert.That(machine.TryCompleteHostCommand(request, true), Is.True);
            }
            Assert.That(commands, Is.EqualTo(new[] { 0, -1, 1, -1, 0, -1, 1, -1, 0, -1, 1, -1 }));
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.Halted));
            Assert.That(machine.ProgramCounter, Is.EqualTo(8));
        }

        [Test]
        public void Runner_UsesSharedProgramWithIndependentRunsAndFrameworkWait()
        {
            for (var run = 0; run < 2; run++)
            {
                var operations = new List<string>();
                var waits = 0;
                using var runner = new ScriptCommandRunner(HpGaugeScriptProgram.Program,
                    HpGaugeScriptProgram.RegisterCount,
                    new HpGaugeScriptCommands(() => operations.Add("Damage"), () => operations.Add("Heal")),
                    ScriptCommandTimeSource.Unscaled, 16,
                    (_, state) => { if (state == ScriptCommandRunState.Waiting) waits++; });
                var frame = new UpdateFrameContext(0, 0, 0.5f, 0, false);
                for (var step = 0; step < 40 && runner.State != ScriptCommandRunState.Halted; step++)
                    runner.OnElementUpdate(in frame);
                Assert.That(runner.State, Is.EqualTo(ScriptCommandRunState.Halted));
                Assert.That(operations, Is.EqualTo(new[] { "Damage", "Heal", "Damage", "Heal", "Damage", "Heal" }));
                Assert.That(waits, Is.EqualTo(6));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Host_CompletesOnlyKnownZeroArgumentOperations(int commandId)
        {
            var damage = 0;
            var heal = 0;
            var host = new HpGaugeScriptCommands(() => damage++, () => heal++);
            Assert.That(host.Execute(commandId, 0), Is.EqualTo(ScriptCommandResult.Completed));
            Assert.That(damage, Is.EqualTo(commandId == 0 ? 1 : 0));
            Assert.That(heal, Is.EqualTo(commandId == 1 ? 1 : 0));
        }

        [TestCase(-1, 500L)]
        [TestCase(-2, 0L)]
        [TestCase(2, 0L)]
        [TestCase(0, 1L)]
        [TestCase(1, -1L)]
        public void Host_RejectsUnknownIdsAndArgumentsBeforeOperations(int commandId, long argument)
        {
            var calls = 0;
            var host = new HpGaugeScriptCommands(() => calls++, () => calls++);
            Assert.That(host.Execute(commandId, argument), Is.EqualTo(ScriptCommandResult.Rejected));
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void Host_RejectsNullActionsAndPropagatesOperationExceptions()
        {
            Assert.Throws<ArgumentNullException>(() => new HpGaugeScriptCommands(null!, () => { }));
            Assert.Throws<ArgumentNullException>(() => new HpGaugeScriptCommands(() => { }, null!));
            var exception = new InvalidOperationException("operation failed");
            var host = new HpGaugeScriptCommands(() => throw exception, () => throw exception);
            Assert.That(Assert.Throws<InvalidOperationException>(() => host.Execute(0, 0)), Is.SameAs(exception));
            Assert.That(Assert.Throws<InvalidOperationException>(() => host.Execute(1, 0)), Is.SameAs(exception));
        }
    }
}
