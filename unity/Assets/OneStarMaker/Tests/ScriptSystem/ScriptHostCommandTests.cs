#nullable enable

using NUnit.Framework;
using OneStarMaker.Runtime.ScriptSystem;

namespace OneStarMaker.Tests.ScriptSystem
{
    [TestFixture]
    public sealed class ScriptHostCommandTests
    {
        [Test]
        public void Factory_UsesAppValuesWithoutChangingExistingOpcodeNumbers()
        {
            var instruction = ScriptInstruction.HostCommand(int.MaxValue, long.MinValue);
            Assert.That((byte)ScriptOpcode.Halt, Is.EqualTo(0));
            Assert.That((byte)ScriptOpcode.JumpIfNotZero, Is.EqualTo(8));
            Assert.That((byte)instruction.Opcode, Is.EqualTo(9));
            Assert.That(instruction.Destination, Is.EqualTo(int.MaxValue));
            Assert.That(instruction.Immediate, Is.EqualTo(long.MinValue));
            Assert.That(instruction.Left, Is.Zero);
            Assert.That(instruction.Right, Is.Zero);
        }

        [TestCase(int.MinValue, long.MinValue)]
        [TestCase(-2, long.MaxValue)]
        [TestCase(-1, -1L)]
        [TestCase(int.MaxValue, 0L)]
        public void RawHostCommand_TreatsIdAndArgumentAsOpaqueAndIgnoresUnusedOperands(int id, long argument)
        {
            var machine = Machine(new ScriptRegisters(0),
                ScriptInstruction.FromRaw(9, id, int.MinValue, int.MaxValue, argument));
            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.WaitingForHost));
            Assert.That(machine.PendingHostRequest!.CommandId, Is.EqualTo(id));
            Assert.That(machine.PendingHostRequest.Argument, Is.EqualTo(argument));
            Assert.That(machine.ProgramCounter, Is.Zero);
            Assert.That(machine.IsLatched, Is.False);
        }

        [Test]
        public void WaitingTick_PreservesRequestIdentityAndDoesNotExecuteFollowingInstruction()
        {
            var registers = new ScriptRegisters(1);
            registers[0] = 7;
            var machine = Machine(registers, ScriptInstruction.HostCommand(0, 5),
                ScriptInstruction.LoadImmediate(0, 99));
            Assert.That(machine.Tick(8), Is.EqualTo(ScriptMachineStatus.WaitingForHost));
            var request = machine.PendingHostRequest;
            for (var i = 0; i < 3; i++)
            {
                Assert.That(machine.Tick(int.MaxValue), Is.EqualTo(ScriptMachineStatus.WaitingForHost));
                Assert.That(machine.PendingHostRequest, Is.SameAs(request));
                Assert.That(machine.ProgramCounter, Is.Zero);
                Assert.That(registers[0], Is.EqualTo(7L));
            }
        }

        [Test]
        public void ExactBudgetBeforeHost_YieldsWithoutRequestUntilNextTick()
        {
            var registers = new ScriptRegisters(1);
            var machine = Machine(registers, ScriptInstruction.LoadImmediate(0, 4),
                ScriptInstruction.HostCommand(0, 0), ScriptInstruction.LoadImmediate(0, 99));
            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.Yielded));
            Assert.That(machine.PendingHostRequest, Is.Null);
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.WaitingForHost));
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(registers[0], Is.EqualTo(4L));
        }

        [Test]
        public void SuccessfulCompletion_IsOneUseAndDoesNotExecuteNextInstruction()
        {
            var registers = new ScriptRegisters(1);
            var machine = Machine(registers, ScriptInstruction.HostCommand(0, 0),
                ScriptInstruction.LoadImmediate(0, 9), ScriptInstruction.Halt());
            machine.Tick(8);
            var request = machine.PendingHostRequest!;
            Assert.That(machine.TryCompleteHostCommand(request, true), Is.True);
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.Ready));
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(machine.PendingHostRequest, Is.Null);
            Assert.That(machine.IsLatched, Is.False);
            Assert.That(registers[0], Is.Zero);
            Assert.That(machine.TryCompleteHostCommand(request, false), Is.False);
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.Ready));
            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.Yielded));
            Assert.That(registers[0], Is.EqualTo(9L));
            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.Halted));
            Assert.That(machine.TryCompleteHostCommand(request, true), Is.False);
            Assert.That(machine.ProgramCounter, Is.EqualTo(2));
        }

        [Test]
        public void SuccessfulCompletionAtEnd_IsReadyUntilNextPositiveTickNaturallyHalts()
        {
            var machine = Machine(new ScriptRegisters(0), ScriptInstruction.HostCommand(0, 0));
            machine.Tick(1);
            Assert.That(machine.TryCompleteHostCommand(machine.PendingHostRequest!, true), Is.True);
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.Ready));
            Assert.That(machine.IsLatched, Is.False);
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.Halted));
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
        }

        [Test]
        public void FailedCompletion_LatchesAtHostPcAndNeverRetriesOrRunsFollowingInstructions()
        {
            var registers = new ScriptRegisters(1);
            var machine = Machine(registers, ScriptInstruction.LoadImmediate(0, 4),
                ScriptInstruction.HostCommand(0, 0), ScriptInstruction.LoadImmediate(0, 99));
            machine.Tick(8);
            var request = machine.PendingHostRequest!;
            Assert.That(machine.TryCompleteHostCommand(request, false), Is.True);
            Assert.That(machine.PendingHostRequest, Is.Null);
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.HostCommandFailed));
            Assert.That(machine.IsLatched, Is.True);
            Assert.That(machine.TryCompleteHostCommand(request, true), Is.False);
            Assert.That(machine.Tick(8), Is.EqualTo(ScriptMachineStatus.HostCommandFailed));
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(registers[0], Is.EqualTo(4L));
        }

        [Test]
        public void NullOrOtherMachineToken_IsRejectedWithoutChangingEitherMachine()
        {
            var program = ScriptProgram.Create(new[] { ScriptInstruction.HostCommand(5, 9) });
            var machine = new ScriptMachine(program, new ScriptRegisters(0));
            var other = new ScriptMachine(program, new ScriptRegisters(0));
            other.Tick(1);
            var foreign = other.PendingHostRequest!;
            Assert.That(machine.TryCompleteHostCommand(foreign, true), Is.False);
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.NotStarted));
            machine.Tick(1);
            var current = machine.PendingHostRequest;
            Assert.That(machine.TryCompleteHostCommand(null!, false), Is.False);
            Assert.That(machine.TryCompleteHostCommand(foreign, true), Is.False);
            Assert.That(machine.PendingHostRequest, Is.SameAs(current));
            Assert.That(other.PendingHostRequest, Is.SameAs(foreign));
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.WaitingForHost));
            Assert.That(other.Status, Is.EqualTo(ScriptMachineStatus.WaitingForHost));
            Assert.That(machine.ProgramCounter, Is.Zero);
            Assert.That(other.ProgramCounter, Is.Zero);
        }

        [Test]
        public void OldToken_IsRejectedAtNewBoundaryEvenWhenCommandValuesMatch()
        {
            var machine = Machine(new ScriptRegisters(0),
                ScriptInstruction.HostCommand(5, 9), ScriptInstruction.HostCommand(5, 9));
            machine.Tick(1);
            var old = machine.PendingHostRequest!;
            machine.TryCompleteHostCommand(old, true);
            machine.Tick(1);
            var current = machine.PendingHostRequest!;
            Assert.That(current, Is.Not.SameAs(old));
            Assert.That(machine.TryCompleteHostCommand(old, true), Is.False);
            Assert.That(machine.TryCompleteHostCommand(old, false), Is.False);
            Assert.That(machine.PendingHostRequest, Is.SameAs(current));
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.WaitingForHost));
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(machine.IsLatched, Is.False);
            Assert.That(machine.TryCompleteHostCommand(current, true), Is.True);
        }

        [TestCase(ScriptMachineStatus.NotStarted)]
        [TestCase(ScriptMachineStatus.Yielded)]
        [TestCase(ScriptMachineStatus.Halted)]
        [TestCase(ScriptMachineStatus.InvalidRegister)]
        [TestCase(ScriptMachineStatus.InvalidJump)]
        [TestCase(ScriptMachineStatus.InvalidOpcode)]
        [TestCase(ScriptMachineStatus.WaitingForHost)]
        [TestCase(ScriptMachineStatus.Ready)]
        [TestCase(ScriptMachineStatus.HostCommandFailed)]
        public void NonPositiveBudget_TakesPrecedenceInEveryStoredStateWithoutMutatingIt(ScriptMachineStatus status)
        {
            var registers = new ScriptRegisters(1);
            var instruction = status == ScriptMachineStatus.Halted ? ScriptInstruction.Halt()
                : status == ScriptMachineStatus.InvalidRegister ? ScriptInstruction.Move(0, 1)
                : status == ScriptMachineStatus.InvalidJump ? ScriptInstruction.Jump(-1)
                : status == ScriptMachineStatus.InvalidOpcode ? ScriptInstruction.FromRaw(255, 0, 0, 0, 0)
                : status == ScriptMachineStatus.Yielded ? ScriptInstruction.LoadImmediate(0, 9)
                : ScriptInstruction.HostCommand(0, 0);
            var machine = Machine(registers, instruction);
            if (status != ScriptMachineStatus.NotStarted)
            {
                machine.Tick(1);
                if (status == ScriptMachineStatus.Ready || status == ScriptMachineStatus.HostCommandFailed)
                {
                    machine.TryCompleteHostCommand(machine.PendingHostRequest!, status == ScriptMachineStatus.Ready);
                }
            }
            Assert.That(machine.Status, Is.EqualTo(status));
            var pc = machine.ProgramCounter;
            var latched = machine.IsLatched;
            var request = machine.PendingHostRequest;
            var value = registers[0];
            foreach (var budget in new[] { 0, -1, int.MinValue })
            {
                Assert.That(machine.Tick(budget), Is.EqualTo(ScriptMachineStatus.RejectedBudget));
                Assert.That(machine.Status, Is.EqualTo(status));
                Assert.That(machine.ProgramCounter, Is.EqualTo(pc));
                Assert.That(machine.IsLatched, Is.EqualTo(latched));
                Assert.That(machine.PendingHostRequest, Is.SameAs(request));
                Assert.That(registers[0], Is.EqualTo(value));
            }
        }

        private static ScriptMachine Machine(ScriptRegisters registers, params ScriptInstruction[] instructions)
        {
            return new ScriptMachine(ScriptProgram.Create(instructions), registers);
        }
    }
}
