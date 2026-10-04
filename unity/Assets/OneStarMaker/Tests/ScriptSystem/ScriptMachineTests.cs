#nullable enable

using System;
using NUnit.Framework;
using Unity.Profiling;
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

        [TestCase(true)]
        [TestCase(false)]
        public void JumpToLength_AtExactBudget_YieldsBeforeNaturalEnd(bool useJump)
        {
            var registers = new ScriptRegisters(1);
            var machine = Machine(registers, useJump
                ? ScriptInstruction.Jump(1)
                : ScriptInstruction.LoadImmediate(0, 4));

            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.Yielded));
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.Yielded));
            Assert.That(machine.IsLatched, Is.False);
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.Halted));
            Assert.That(machine.IsLatched, Is.True);
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(machine.Tick(0), Is.EqualTo(ScriptMachineStatus.RejectedBudget));
            Assert.That(machine.Tick(-1), Is.EqualTo(ScriptMachineStatus.RejectedBudget));
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.Halted));
            Assert.That(machine.IsLatched, Is.True);
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.Halted));
        }

        [TestCase(true, true, -1L)]
        [TestCase(true, true, 2L)]
        [TestCase(true, false, -1L)]
        [TestCase(true, false, 2L)]
        [TestCase(false, true, -1L)]
        [TestCase(false, true, 2L)]
        [TestCase(false, false, -1L)]
        [TestCase(false, false, 2L)]
        public void ConditionalJump_InvalidTarget_IsValidatedOnlyWhenTaken(
            bool jumpIfZero, bool taken, long target)
        {
            var registers = new ScriptRegisters(1);
            registers[0] = jumpIfZero == taken ? 0 : 7;
            var machine = Machine(registers, jumpIfZero
                ? ScriptInstruction.JumpIfZero(0, target)
                : ScriptInstruction.JumpIfNotZero(0, target));
            var originalValue = registers[0];

            Assert.That(machine.Tick(1), Is.EqualTo(taken
                ? ScriptMachineStatus.InvalidJump
                : ScriptMachineStatus.Yielded));
            Assert.That(machine.ProgramCounter, Is.EqualTo(taken ? 0 : 1));
            Assert.That(machine.IsLatched, Is.EqualTo(taken));
            Assert.That(registers[0], Is.EqualTo(originalValue));
            Assert.That(machine.Tick(1), Is.EqualTo(taken
                ? ScriptMachineStatus.InvalidJump
                : ScriptMachineStatus.Halted));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ConditionalJump_InvalidCondition_FaultsBeforeTarget(bool jumpIfZero)
        {
            var registers = new ScriptRegisters(1);
            registers[0] = 9;
            var machine = Machine(registers, jumpIfZero
                ? ScriptInstruction.JumpIfZero(1, -1)
                : ScriptInstruction.JumpIfNotZero(1, -1));

            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.InvalidRegister));
            Assert.That(machine.IsLatched, Is.True);
            Assert.That(machine.ProgramCounter, Is.EqualTo(0));
            Assert.That(registers[0], Is.EqualTo(9L));
        }

        [TestCase(ScriptOpcode.Add, -1)]
        [TestCase(ScriptOpcode.Add, 2)]
        [TestCase(ScriptOpcode.Sub, -1)]
        [TestCase(ScriptOpcode.Sub, 2)]
        [TestCase(ScriptOpcode.Mul, -1)]
        [TestCase(ScriptOpcode.Mul, 2)]
        public void Arithmetic_InvalidDestination_PreservesEarlierWriteAndFaultPc(
            ScriptOpcode opcode, int destination)
        {
            var registers = new ScriptRegisters(2);
            registers[1] = 3;
            var machine = Machine(registers,
                ScriptInstruction.LoadImmediate(0, 4),
                ScriptInstruction.FromRaw((byte)opcode, destination, 0, 1, 99));

            Assert.That(machine.Tick(2), Is.EqualTo(ScriptMachineStatus.InvalidRegister));
            Assert.That(machine.IsLatched, Is.True);
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(registers[0], Is.EqualTo(4L));
            Assert.That(registers[1], Is.EqualTo(3L));
        }

        [TestCase(ScriptMachineStatus.Halted)]
        [TestCase(ScriptMachineStatus.InvalidRegister)]
        [TestCase(ScriptMachineStatus.InvalidJump)]
        [TestCase(ScriptMachineStatus.InvalidOpcode)]
        public void RejectedBudget_AfterTerminalLatch_PreservesStoredResult(ScriptMachineStatus terminal)
        {
            var registers = new ScriptRegisters(1);
            ScriptInstruction instruction;
            switch (terminal)
            {
                case ScriptMachineStatus.Halted:
                    instruction = ScriptInstruction.Halt();
                    break;
                case ScriptMachineStatus.InvalidRegister:
                    instruction = ScriptInstruction.LoadImmediate(1, 9);
                    break;
                case ScriptMachineStatus.InvalidJump:
                    instruction = ScriptInstruction.Jump(-1);
                    break;
                default:
                    instruction = ScriptInstruction.FromRaw(255, 0, 0, 0, 9);
                    break;
            }

            var machine = Machine(registers, ScriptInstruction.LoadImmediate(0, 4), instruction);
            Assert.That(machine.Tick(2), Is.EqualTo(terminal));
            registers[0] = 7;

            Assert.That(machine.Tick(0), Is.EqualTo(ScriptMachineStatus.RejectedBudget));
            Assert.That(machine.Tick(-1), Is.EqualTo(ScriptMachineStatus.RejectedBudget));
            Assert.That(machine.Status, Is.EqualTo(terminal));
            Assert.That(machine.IsLatched, Is.True);
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(registers[0], Is.EqualTo(7L));
            Assert.That(machine.Tick(1), Is.EqualTo(terminal));
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(registers[0], Is.EqualTo(7L));
        }

        [Test]
        public void RejectedBudget_AfterYield_PreservesResumableState()
        {
            var registers = new ScriptRegisters(1);
            var machine = Machine(registers,
                ScriptInstruction.LoadImmediate(0, 3), ScriptInstruction.LoadImmediate(0, 4));
            machine.Tick(1);

            Assert.That(machine.Tick(0), Is.EqualTo(ScriptMachineStatus.RejectedBudget));
            Assert.That(machine.Tick(-1), Is.EqualTo(ScriptMachineStatus.RejectedBudget));
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.Yielded));
            Assert.That(machine.IsLatched, Is.False);
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(registers[0], Is.EqualTo(3L));
            Assert.That(machine.Tick(1), Is.EqualTo(ScriptMachineStatus.Yielded));
            Assert.That(registers[0], Is.EqualTo(4L));
        }

        [Test]
        public void Tick_NonterminatingJumpLoop_AllocatesZeroManagedBytesAfterWarmup()
        {
            const int warmupIterations = 128;
            const int measuredIterations = 1000;
            const int budget = 16;
            var registers = new ScriptRegisters(2);
            registers[1] = 1;
            var machine = Machine(registers, ScriptInstruction.Add(0, 0, 1), ScriptInstruction.Jump(0));
            for (var i = 0; i < warmupIterations; i++)
            {
                machine.Tick(budget);
            }

            var initialValue = registers[0];
            const ProfilerRecorderOptions options =
                ProfilerRecorderOptions.SumAllSamplesInFrame |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
            using var recorder = new ProfilerRecorder(ProfilerCategory.Internal, "GC.Alloc", 1, options);
            Assert.That(recorder.Valid, Is.True);
            recorder.Start();
            try
            {
                var positiveControl = new byte[4096];
                GC.KeepAlive(positiveControl);
            }
            finally
            {
                recorder.Stop();
            }
            var positiveEvents = recorder.Count == 0 ? 0L : recorder.GetSample(0).Count;
            TestContext.Out.WriteLine($"GC.Alloc unit={recorder.UnitType} positiveEvents={positiveEvents}");
            Assert.That(positiveEvents, Is.GreaterThan(0L));
            recorder.Reset();
            Assert.That(recorder.Count, Is.Zero);

            recorder.Start();
            try
            {
                for (var i = 0; i < measuredIterations; i++)
                {
                    machine.Tick(budget);
                }
            }
            finally
            {
                recorder.Stop();
            }
            var targetEvents = recorder.Count == 0 ? 0L : recorder.GetSample(0).Count;

            TestContext.Out.WriteLine(
                $"allocation test={nameof(Tick_NonterminatingJumpLoop_AllocatesZeroManagedBytesAfterWarmup)} " +
                $"runtime={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} " +
                $"unity={UnityEngine.Application.unityVersion} warmup={warmupIterations} " +
                $"iterations={measuredIterations} budget={budget} unit={recorder.UnitType} positiveEvents={positiveEvents} targetEvents={targetEvents} " +
                $"increments={registers[0] - initialValue} status={machine.Status}");
            Assert.That(targetEvents, Is.Zero);
            Assert.That(registers[0] - initialValue, Is.EqualTo(measuredIterations * (budget / 2)));
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.Yielded));
            Assert.That(machine.IsLatched, Is.False);
            Assert.That(machine.ProgramCounter, Is.EqualTo(0));
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
