#nullable enable

using System;

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>
    /// 呼び出し側のプログラムとレジスタを借りて、固定命令を予算つきで進める。
    /// tick は命令の読み取りとレジスタの読み書き、カウンタ更新だけを行う。
    /// 故障を例外にすると、UpdateSystem の逐次実行が同じフレームの後続要素を止める。
    /// ロックは持たない。同じレジスタを複数の機械で触る順序は、呼び出し側の逐次 tick が決める。
    /// </summary>
    public sealed class ScriptMachine
    {
        private readonly ScriptProgram _program;
        private readonly ScriptRegisters _registers;
        private int _programCounter;
        private ScriptMachineStatus _status;
        private bool _latched;

        public ScriptMachine(ScriptProgram program, ScriptRegisters registers)
        {
            _program = program ?? throw new ArgumentNullException(nameof(program));
            _registers = registers ?? throw new ArgumentNullException(nameof(registers));
            _status = ScriptMachineStatus.NotStarted;
        }

        public int ProgramCounter => _programCounter;

        public ScriptMachineStatus Status => _status;

        public bool IsLatched => _latched;

        public ScriptMachineStatus Tick(int instructionBudget)
        {
            if (_latched)
            {
                return _status;
            }

            if (instructionBudget < 1)
            {
                return ScriptMachineStatus.RejectedBudget;
            }

            var executed = 0;
            while (executed < instructionBudget)
            {
                if (_programCounter >= _program.InstructionCount)
                {
                    return Latch(ScriptMachineStatus.Halted);
                }

                var instruction = _program.GetInstruction(_programCounter);
                executed++;
                switch (instruction.Opcode)
                {
                    case ScriptOpcode.Halt:
                        return Latch(ScriptMachineStatus.Halted);

                    case ScriptOpcode.LoadImmediate:
                        if (!TryWrite(instruction.Destination, instruction.Immediate))
                        {
                            return _status;
                        }

                        _programCounter++;
                        break;

                    case ScriptOpcode.Move:
                        if (!TryRead(instruction.Left, out var source) ||
                            !TryWrite(instruction.Destination, source))
                        {
                            return _status;
                        }

                        _programCounter++;
                        break;

                    case ScriptOpcode.Add:
                        if (!TryBinary(instruction, BinaryKind.Add))
                        {
                            return _status;
                        }

                        break;

                    case ScriptOpcode.Sub:
                        if (!TryBinary(instruction, BinaryKind.Sub))
                        {
                            return _status;
                        }

                        break;

                    case ScriptOpcode.Mul:
                        if (!TryBinary(instruction, BinaryKind.Mul))
                        {
                            return _status;
                        }

                        break;

                    case ScriptOpcode.Jump:
                        if (!TryJump(instruction.Immediate))
                        {
                            return _status;
                        }

                        break;

                    case ScriptOpcode.JumpIfZero:
                        if (!TryConditionalJump(instruction, jumpWhenZero: true))
                        {
                            return _status;
                        }

                        break;

                    case ScriptOpcode.JumpIfNotZero:
                        if (!TryConditionalJump(instruction, jumpWhenZero: false))
                        {
                            return _status;
                        }

                        break;

                    default:
                        return Latch(ScriptMachineStatus.InvalidOpcode);
                }
            }

            _status = ScriptMachineStatus.Yielded;
            return _status;
        }

        private bool TryConditionalJump(ScriptInstruction instruction, bool jumpWhenZero)
        {
            if (!TryRead(instruction.Destination, out var condition))
            {
                return false;
            }

            var shouldJump = jumpWhenZero ? condition == 0 : condition != 0;
            if (!shouldJump)
            {
                _programCounter++;
                return true;
            }

            return TryJump(instruction.Immediate);
        }

        private bool TryBinary(ScriptInstruction instruction, BinaryKind kind)
        {
            if (!TryRead(instruction.Left, out var left) || !TryRead(instruction.Right, out var right))
            {
                return false;
            }

            long result;
            unchecked
            {
                switch (kind)
                {
                    case BinaryKind.Add:
                        result = left + right;
                        break;
                    case BinaryKind.Sub:
                        result = left - right;
                        break;
                    case BinaryKind.Mul:
                        result = left * right;
                        break;
                    default:
                        Latch(ScriptMachineStatus.InvalidOpcode);
                        return false;
                }
            }

            if (!TryWrite(instruction.Destination, result))
            {
                return false;
            }

            _programCounter++;
            return true;
        }

        private bool TryJump(long target)
        {
            if (target < 0 || target > _program.InstructionCount)
            {
                Latch(ScriptMachineStatus.InvalidJump);
                return false;
            }

            _programCounter = (int)target;
            return true;
        }

        private bool TryRead(int index, out long value)
        {
            if (_registers.TryGet(index, out value))
            {
                return true;
            }

            Latch(ScriptMachineStatus.InvalidRegister);
            return false;
        }

        private bool TryWrite(int index, long value)
        {
            if (_registers.TrySet(index, value))
            {
                return true;
            }

            Latch(ScriptMachineStatus.InvalidRegister);
            return false;
        }

        private ScriptMachineStatus Latch(ScriptMachineStatus status)
        {
            _status = status;
            _latched = true;
            return status;
        }

        private enum BinaryKind : byte
        {
            Add = 0,
            Sub = 1,
            Mul = 2,
        }
    }
}
