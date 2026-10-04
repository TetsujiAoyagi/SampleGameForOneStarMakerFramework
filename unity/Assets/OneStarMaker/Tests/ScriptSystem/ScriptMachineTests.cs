#nullable enable

using System;
using NUnit.Framework;
using OneStarMaker.Runtime.ScriptSystem;

namespace OneStarMaker.Tests.ScriptSystem
{
    [TestFixture]
    public sealed class ScriptMachineTests
    {
        [Test]
        public void LoadImmediate_WritesInt64_AndHaltLeavesProgramCounterOnTheHalt()
        {
            var registers = new ScriptRegisters(1);
            var machine = Machine(
                registers,
                ScriptInstruction.LoadImmediate(0, long.MinValue),
                ScriptInstruction.Halt(),
                ScriptInstruction.LoadImmediate(0, 5));

            var status = machine.Tick(8);

            Assert.That(status, Is.EqualTo(ScriptMachineStatus.Halted));
            Assert.That(machine.IsLatched, Is.True);
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(registers[0], Is.EqualTo(long.MinValue));
        }

        [Test]
        public void Arithmetic_AddSubMul_WrapsWithoutThrowing()
        {
            var registers = new ScriptRegisters(5);
            registers[0] = long.MaxValue;
            registers[1] = 1;
            registers[2] = 0;
            registers[3] = 2;
            registers[4] = long.MaxValue;
            var machine = Machine(
                registers,
                ScriptInstruction.Add(0, 0, 1),
                ScriptInstruction.Sub(2, 2, 1),
                ScriptInstruction.Mul(4, 4, 3));

            var status = machine.Tick(3);

            Assert.That(status, Is.EqualTo(ScriptMachineStatus.Yielded));
            Assert.That(registers[0], Is.EqualTo(long.MinValue));
            Assert.That(registers[2], Is.EqualTo(-1L));
            Assert.That(registers[4], Is.EqualTo(-2L));
        }

        [Test]
        public void Move_CopiesSourceIntoDestination()
        {
            var registers = new ScriptRegisters(2);
            registers[1] = 42;
            var machine = Machine(registers, ScriptInstruction.Move(0, 1), ScriptInstruction.Halt());

            machine.Tick(2);

            Assert.That(registers[0], Is.EqualTo(42L));
            Assert.That(registers[1], Is.EqualTo(42L));
        }

        [Test]
        public void Jump_SkipsTheNextInstruction()
        {
            var registers = new ScriptRegisters(1);
            var machine = Machine(
                registers,
                ScriptInstruction.LoadImmediate(0, 1),
                ScriptInstruction.Jump(3),
                ScriptInstruction.LoadImmediate(0, 99),
                ScriptInstruction.Halt());

            var status = machine.Tick(8);

            Assert.That(status, Is.EqualTo(ScriptMachineStatus.Halted));
            Assert.That(registers[0], Is.EqualTo(1L));
            Assert.That(machine.ProgramCounter, Is.EqualTo(3));
        }

        [Test]
        public void JumpToLength_HaltsInTheSameTickWhenBudgetRemains()
        {
            var registers = new ScriptRegisters(0);
            var machine = Machine(registers, ScriptInstruction.Jump(1));

            var status = machine.Tick(2);

            Assert.That(status, Is.EqualTo(ScriptMachineStatus.Halted));
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
        }

        [Test]
        public void ConditionalJumps_BranchOnZeroAndNonZero()
        {
            Assert.That(Branch(condition: 0, jumpIfZero: true), Is.EqualTo(2L));
            Assert.That(Branch(condition: 5, jumpIfZero: true), Is.EqualTo(1L));
            Assert.That(Branch(condition: 5, jumpIfZero: false), Is.EqualTo(2L));
            Assert.That(Branch(condition: 0, jumpIfZero: false), Is.EqualTo(1L));
        }

        [Test]
        public void Budget_YieldsAndResumes()
        {
            var registers = new ScriptRegisters(2);
            var machine = Machine(
                registers,
                ScriptInstruction.LoadImmediate(0, 1),
                ScriptInstruction.Add(1, 1, 0),
                ScriptInstruction.Jump(1));

            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.Yielded));
            Assert.That(registers[0], Is.EqualTo(1L));
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));

            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.Yielded));
            Assert.That(registers[1], Is.EqualTo(1L));
            Assert.That(machine.ProgramCounter, Is.EqualTo(2));

            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.Yielded));
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));

            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.Yielded));
            Assert.That(registers[1], Is.EqualTo(2L));
        }

        [Test]
        public void FallingOffTheEnd_HaltsWithProgramCounterAtLength()
        {
            var registers = new ScriptRegisters(1);
            var machine = Machine(registers, ScriptInstruction.LoadImmediate(0, 4));

            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.Yielded));
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));

            var status = machine.Tick(1);

            Assert.That(status, Is.EqualTo(ScriptMachineStatus.Halted));
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(registers[0], Is.EqualTo(4L));
        }

        [Test]
        public void InvalidRegister_DoesNotWriteAndLatches()
        {
            var registers = new ScriptRegisters(1);
            var machine = Machine(registers, ScriptInstruction.LoadImmediate(5, 9));

            var status = machine.Tick(4);

            Assert.That(status, Is.EqualTo(ScriptMachineStatus.InvalidRegister));
            Assert.That(machine.IsLatched, Is.True);
            Assert.That(machine.ProgramCounter, Is.EqualTo(0));
            Assert.That(registers[0], Is.EqualTo(0L));
        }

        [Test]
        public void EarlierInstructionInTheSameTick_RemainsAfterALaterFault()
        {
            var registers = new ScriptRegisters(1);
            var machine = Machine(
                registers,
                ScriptInstruction.LoadImmediate(0, 4),
                ScriptInstruction.LoadImmediate(5, 9));

            var status = machine.Tick(8);

            Assert.That(status, Is.EqualTo(ScriptMachineStatus.InvalidRegister));
            Assert.That(registers[0], Is.EqualTo(4L));
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
        }

        [Test]
        public void InvalidJump_LatchesWithoutMovingProgramCounter()
        {
            var registers = new ScriptRegisters(1);
            var negative = Machine(registers, ScriptInstruction.Jump(-1));
            Assert.That(negative.Tick(2), Is.EqualTo(ScriptMachineStatus.InvalidJump));
            Assert.That(negative.ProgramCounter, Is.EqualTo(0));

            var pastEnd = Machine(registers, ScriptInstruction.Jump(2));
            Assert.That(pastEnd.Tick(2), Is.EqualTo(ScriptMachineStatus.InvalidJump));
            Assert.That(pastEnd.ProgramCounter, Is.EqualTo(0));
            Assert.That(registers[0], Is.EqualTo(0L));
        }

        [Test]
        public void InvalidOpcode_Latches()
        {
            var registers = new ScriptRegisters(1);
            var machine = Machine(registers, ScriptInstruction.FromRaw(255, 0, 0, 0, 99));

            var status = machine.Tick(2);

            Assert.That(status, Is.EqualTo(ScriptMachineStatus.InvalidOpcode));
            Assert.That(machine.ProgramCounter, Is.EqualTo(0));
            Assert.That(registers[0], Is.EqualTo(0L));
        }

        [Test]
        public void LatchedTick_DoesNotMutateRegisters()
        {
            var registers = new ScriptRegisters(1);
            var machine = Machine(
                registers,
                ScriptInstruction.LoadImmediate(0, 1),
                ScriptInstruction.Halt());
            machine.Tick(8);
            registers[0] = 7;

            var status = machine.Tick(8);

            Assert.That(status, Is.EqualTo(ScriptMachineStatus.Halted));
            Assert.That(registers[0], Is.EqualTo(7L));
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
        }

        [Test]
        public void RejectedBudget_DoesNotChangeStatusOrProgramCounter()
        {
            var registers = new ScriptRegisters(1);
            var machine = Machine(registers, ScriptInstruction.LoadImmediate(0, 3), ScriptInstruction.Halt());

            Assert.That(machine.Tick(0), Is.EqualTo(ScriptMachineStatus.RejectedBudget));
            Assert.That(machine.Tick(-1), Is.EqualTo(ScriptMachineStatus.RejectedBudget));
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.NotStarted));
            Assert.That(machine.ProgramCounter, Is.EqualTo(0));
            Assert.That(registers[0], Is.EqualTo(0L));

            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.Yielded));
            Assert.That(registers[0], Is.EqualTo(3L));
        }

        [Test]
        public void EmptyProgram_Halts()
        {
            var registers = new ScriptRegisters(0);
            var machine = new ScriptMachine(ScriptProgram.Create(Array.Empty<ScriptInstruction>()), registers);

            var status = machine.Tick(4);

            Assert.That(status, Is.EqualTo(ScriptMachineStatus.Halted));
            Assert.That(machine.ProgramCounter, Is.EqualTo(0));
        }

        [Test]
        public void ZeroRegisters_CanHalt()
        {
            var registers = new ScriptRegisters(0);
            var machine = Machine(registers, ScriptInstruction.Halt());

            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.Halted));
        }

        [Test]
        public void Create_CopiesInstructions_SoLaterEditsDoNotAffectExecution()
        {
            var source = new[]
            {
                ScriptInstruction.LoadImmediate(0, 1),
                ScriptInstruction.Halt(),
            };
            var program = ScriptProgram.Create(source);
            source[0] = ScriptInstruction.LoadImmediate(0, 99);
            var registers = new ScriptRegisters(1);
            var machine = new ScriptMachine(program, registers);

            machine.Tick(4);

            Assert.That(registers[0], Is.EqualTo(1L));
        }

        [Test]
        public void Constructors_RejectNullAndNegativeCount()
        {
            var registers = new ScriptRegisters(1);
            var program = ScriptProgram.Create(new[] { ScriptInstruction.Halt() });

            Assert.Throws<ArgumentNullException>(() => ScriptProgram.Create(null!));
            Assert.Throws<ArgumentNullException>(() => new ScriptMachine(null!, registers));
            Assert.Throws<ArgumentNullException>(() => new ScriptMachine(program, null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScriptRegisters(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => registers[1] = 1);
        }

        private static long Branch(long condition, bool jumpIfZero)
        {
            var registers = new ScriptRegisters(2);
            registers[0] = condition;
            var branch = jumpIfZero
                ? ScriptInstruction.JumpIfZero(0, 3)
                : ScriptInstruction.JumpIfNotZero(0, 3);
            var machine = Machine(
                registers,
                branch,
                ScriptInstruction.LoadImmediate(1, 1),
                ScriptInstruction.Halt(),
                ScriptInstruction.LoadImmediate(1, 2),
                ScriptInstruction.Halt());
            machine.Tick(8);
            return registers[1];
        }

        private static ScriptMachine Machine(ScriptRegisters registers, params ScriptInstruction[] instructions)
        {
            return new ScriptMachine(ScriptProgram.Create(instructions), registers);
        }
    }
}
