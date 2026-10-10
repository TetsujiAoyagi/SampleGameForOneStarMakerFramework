#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.ScriptSystem;
using SampleGame.OutGame.HpGauge;

namespace OneStarMaker.ScriptCommandOfflineTests
{
    internal static partial class Program
    {
        private static void Core_NumericOpcodesAndImmutableProgram()
        {
            var opcodes = new[] { ScriptOpcode.Halt, ScriptOpcode.LoadImmediate, ScriptOpcode.Move, ScriptOpcode.Add,
                ScriptOpcode.Sub, ScriptOpcode.Mul, ScriptOpcode.Jump, ScriptOpcode.JumpIfZero, ScriptOpcode.JumpIfNotZero };
            for (byte opcode = 0; opcode < opcodes.Length; opcode++) Equal(opcode, (byte)opcodes[opcode], "numeric opcode ABI");
            Equal((byte)9, (byte)ScriptOpcode.HostCommand, "host appended");
            var statuses = new[] { ScriptMachineStatus.NotStarted, ScriptMachineStatus.Yielded, ScriptMachineStatus.Halted,
                ScriptMachineStatus.InvalidRegister, ScriptMachineStatus.InvalidJump, ScriptMachineStatus.InvalidOpcode, ScriptMachineStatus.RejectedBudget };
            for (byte status = 0; status < statuses.Length; status++) Equal(status, (byte)statuses[status], "existing status numbers");
            var source = new[] { ScriptInstruction.LoadImmediate(0, 3), ScriptInstruction.LoadImmediate(1, 1),
                ScriptInstruction.Move(2, 0), ScriptInstruction.Add(2, 2, 1), ScriptInstruction.Mul(2, 2, 0),
                ScriptInstruction.Sub(0, 0, 1), ScriptInstruction.JumpIfNotZero(0, 5), ScriptInstruction.JumpIfZero(0, 9),
                ScriptInstruction.LoadImmediate(2, 99), ScriptInstruction.Jump(11), ScriptInstruction.LoadImmediate(2, 99), ScriptInstruction.Halt() };
            var program = Code(source);
            source[0] = ScriptInstruction.Halt();
            var registers = new ScriptRegisters(3);
            var machine = new ScriptMachine(program, registers);
            RejectBudgetsWithoutMutation(machine);
            Equal(ScriptMachineStatus.Halted, machine.Tick(32), "nine numeric operations");
            Equal(12L, registers[2], "arithmetic / move / branches");
            Equal(0L, registers[0], "countdown");
            Equal(11, machine.ProgramCounter, "halt PC");
            RejectBudgetsWithoutMutation(machine);
            Throws<ArgumentNullException>(() => ScriptProgram.Create(null!));
            Throws<ArgumentNullException>(() => new ScriptMachine(null!, registers));
            Throws<ArgumentNullException>(() => new ScriptMachine(program, null!));
            Throws<ArgumentOutOfRangeException>(() => new ScriptRegisters(-1));
        }

        private static void Core_RequestIdentityAndBudgetPriority()
        {
            var machine = Machine(ScriptInstruction.LoadImmediate(0, 3), ScriptInstruction.HostCommand(7, long.MinValue),
                ScriptInstruction.HostCommand(7, long.MinValue), ScriptInstruction.Halt());
            Equal(ScriptMachineStatus.Yielded, machine.Tick(1), "budget ends before command");
            Equal(1, machine.ProgramCounter, "before command PC");
            True(machine.PendingHostRequest == null, "command issued without budget");
            RejectBudgetsWithoutMutation(machine);
            Equal(ScriptMachineStatus.WaitingForHost, machine.Tick(1), "request consumes one instruction");
            var first = machine.PendingHostRequest!;
            Equal(7, first.CommandId, "opaque ID");
            Equal(long.MinValue, first.Argument, "opaque argument");
            Equal(1, machine.ProgramCounter, "waiting PC stays on command");
            for (var tick = 0; tick < 3; tick++)
            {
                Equal(ScriptMachineStatus.WaitingForHost, machine.Tick(100), "positive repeated Tick");
                True(ReferenceEquals(first, machine.PendingHostRequest), "request was reissued");
            }
            RejectBudgetsWithoutMutation(machine);
            var other = Machine(ScriptInstruction.HostCommand(7, long.MinValue));
            other.Tick(1);
            RejectCompletionWithoutMutation(machine, null!, true);
            RejectCompletionWithoutMutation(machine, other.PendingHostRequest!, true);
            True(ReferenceEquals(first, machine.PendingHostRequest), "rejected ack mutated pending");
            True(machine.TryCompleteHostCommand(first, true), "correct completion rejected");
            Equal(ScriptMachineStatus.Ready, machine.Status, "ack Ready");
            Equal(2, machine.ProgramCounter, "ack advances one PC");
            True(!machine.IsLatched && machine.PendingHostRequest == null, "ack executes next instruction");
            RejectBudgetsWithoutMutation(machine);
            RejectCompletionWithoutMutation(machine, first, false);
            machine.Tick(1);
            var second = machine.PendingHostRequest!;
            True(!ReferenceEquals(first, second), "distinct boundary reused identity");
            RejectCompletionWithoutMutation(machine, first, true);
            True(ReferenceEquals(second, machine.PendingHostRequest), "stale completion clears current");
            True(machine.TryCompleteHostCommand(second, true), "second completion rejected");
            Equal(ScriptMachineStatus.Halted, machine.Tick(1), "next Tick halts");
            RejectCompletionWithoutMutation(machine, second, true);
        }

        private static void Core_FailureAndTerminalLatches()
        {
            var machine = Machine(ScriptInstruction.HostCommand(0, 0), ScriptInstruction.LoadImmediate(0, 99));
            machine.Tick(1);
            var request = machine.PendingHostRequest!;
            True(machine.TryCompleteHostCommand(request, false), "failure ack rejected");
            Equal(ScriptMachineStatus.HostCommandFailed, machine.Status, "failure status");
            Equal(0, machine.ProgramCounter, "failure PC");
            True(machine.IsLatched && machine.PendingHostRequest == null, "failure latch");
            RejectBudgetsWithoutMutation(machine);
            Equal(ScriptMachineStatus.HostCommandFailed, machine.Tick(99), "failure remains latched");
            RejectCompletionWithoutMutation(machine, request, true);
            foreach (var entry in new[]
            {
                (ScriptInstruction.LoadImmediate(-1, 99), ScriptMachineStatus.InvalidRegister),
                (ScriptInstruction.Jump(-1), ScriptMachineStatus.InvalidJump),
                (ScriptInstruction.FromRaw(255, 0, 0, 0, 0), ScriptMachineStatus.InvalidOpcode),
                (ScriptInstruction.Halt(), ScriptMachineStatus.Halted),
            })
            {
                machine = Machine(entry.Item1);
                Equal(entry.Item2, machine.Tick(1), "terminal result");
                RejectBudgetsWithoutMutation(machine);
                Equal(entry.Item2, machine.Tick(1), "terminal retained");
            }
        }

        private static void Core_RawOperandsAndNaturalEnd()
        {
            var machine = Machine(ScriptInstruction.FromRaw(9, int.MinValue, -999, int.MaxValue, long.MaxValue));
            Equal(ScriptMachineStatus.WaitingForHost, machine.Tick(1), "raw host");
            Equal(int.MinValue, machine.PendingHostRequest!.CommandId, "raw ID is not a register");
            Equal(long.MaxValue, machine.PendingHostRequest.Argument, "raw argument");
            machine.TryCompleteHostCommand(machine.PendingHostRequest, true);
            Equal(ScriptMachineStatus.Halted, machine.Tick(1), "natural end");
            Equal(1, machine.ProgramCounter, "natural end PC");
            Equal(ScriptMachineStatus.Halted, Machine().Tick(1), "empty natural end");
        }

        private static void App_ExactProgramAndHostBoundary()
        {
            Equal(9, HpGaugeScriptProgram.Program.InstructionCount, "app instruction count");
            Equal(2, HpGaugeScriptProgram.RegisterCount, "app register count");
            True(ReferenceEquals(HpGaugeScriptProgram.Program, HpGaugeScriptProgram.Program), "program is not shared");
            var registers = new ScriptRegisters(2);
            var machine = new ScriptMachine(HpGaugeScriptProgram.Program, registers);
            var actual = new List<string>();
            for (var requestIndex = 0; requestIndex < 12; requestIndex++)
            {
                Equal(ScriptMachineStatus.WaitingForHost, machine.Tick(16), "app request");
                Equal(3L - requestIndex / 4, registers[0], "VM owns app loop countdown");
                Equal(1L, registers[1], "app decrement register");
                var request = machine.PendingHostRequest!;
                Equal(new[] { 0, -1, 1, -1 }[requestIndex % 4], request.CommandId, "app command sequence");
                Equal(request.CommandId == -1 ? 500L : 0L, request.Argument, "app command argument");
                actual.Add(request.CommandId + ":" + request.Argument);
                machine.TryCompleteHostCommand(request, true);
            }
            Equal(ScriptMachineStatus.Halted, machine.Tick(16), "app halt");
            Equal(8, machine.ProgramCounter, "exact app halt PC");
            Console.WriteLine("Actual program requests: " + string.Join(" -> ", actual));
            var calls = 0;
            var host = new HpGaugeScriptCommands(() => calls++, () => calls++);
            foreach (var pair in new[] { (-1, 500L), (-2, 0L), (2, 0L), (0, 1L), (1, -1L) })
                Equal(ScriptCommandResult.Rejected, host.Execute(pair.Item1, pair.Item2), "app rejection");
            Equal(0, calls, "rejected operation side effects");
            Equal(ScriptCommandResult.Completed, host.Execute(0, 0), "damage");
            Equal(ScriptCommandResult.Completed, host.Execute(1, 0), "heal");
            Equal(2, calls, "typed action invocation count");
            Throws<ArgumentNullException>(() => new HpGaugeScriptCommands(null!, () => { }));
            Throws<ArgumentNullException>(() => new HpGaugeScriptCommands(() => { }, null!));
            var throwing = new HpGaugeScriptCommands(() => throw new InvalidOperationException(), () => throw new InvalidOperationException());
            Throws<InvalidOperationException>(() => throwing.Execute(0, 0));
            Throws<InvalidOperationException>(() => throwing.Execute(1, 0));
        }

        private static void Core_NumericAndPendingTicksDoNotAllocate()
        {
            foreach (var machine in new[] { Machine(ScriptInstruction.Jump(0)), Machine(ScriptInstruction.HostCommand(0, 0)) })
            {
                machine.Tick(16); // Warm and issue the single host token outside the measurement.
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var tick = 0; tick < 1000; tick++) machine.Tick(16);
                Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before, "numeric / repeated pending Tick allocation");
            }
        }

        private static void RejectCompletionWithoutMutation(ScriptMachine machine, ScriptHostRequest request, bool succeeded)
        {
            var pc = machine.ProgramCounter;
            var status = machine.Status;
            var latch = machine.IsLatched;
            var pending = machine.PendingHostRequest;
            True(!machine.TryCompleteHostCommand(request, succeeded), "unmatched completion accepted");
            Equal(pc, machine.ProgramCounter, "rejected completion PC");
            Equal(status, machine.Status, "rejected completion status");
            Equal(latch, machine.IsLatched, "rejected completion latch");
            True(ReferenceEquals(pending, machine.PendingHostRequest), "rejected completion pending identity");
        }

        private static void RejectBudgetsWithoutMutation(ScriptMachine machine)
        {
            var pc = machine.ProgramCounter;
            var status = machine.Status;
            var latch = machine.IsLatched;
            var request = machine.PendingHostRequest;
            foreach (var budget in new[] { 0, -1, int.MinValue })
            {
                Equal(ScriptMachineStatus.RejectedBudget, machine.Tick(budget), "budget priority");
                Equal(pc, machine.ProgramCounter, "rejected PC");
                Equal(status, machine.Status, "rejected stored status");
                Equal(latch, machine.IsLatched, "rejected latch");
                True(ReferenceEquals(request, machine.PendingHostRequest), "rejected request identity");
            }
        }
    }
}
