#nullable enable

using System;

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>
    /// 呼び出し側のプログラムとレジスタを借りて、固定命令を予算つきで進める。
    /// Tick は数値命令を実行し、host 命令では不透明な要求を発行して外側の完了を待つ。
    /// host の副作用・時計・引数検査は持たず、数値命令経路には追加 allocation がない。
    /// 不正オペランドは型付き結果として返すため、同じフレームの後続要素を止めない。
    /// 機械は PC・状態・終端ラッチ・待機要求を所有し、プログラムとレジスタの寿命は呼び出し側が持つ。
    /// 呼び出し側は Tick・要求完了・レジスタ変更を同一スレッドで逐次化する。並行実行は非対応。
    /// 同じレジスタを複数の機械で触る場合も、呼び出し側が実行順を決める。
    /// </summary>
    public sealed class ScriptMachine
    {
        private readonly ScriptProgram _program;
        private readonly ScriptRegisters _registers;
        private int _programCounter;
        private ScriptMachineStatus _status;
        private bool _latched;
        private ScriptHostRequest? _pendingHostRequest;

        public ScriptMachine(ScriptProgram program, ScriptRegisters registers)
        {
            _program = program ?? throw new ArgumentNullException(nameof(program));
            _registers = registers ?? throw new ArgumentNullException(nameof(registers));
            _status = ScriptMachineStatus.NotStarted;
        }

        public int ProgramCounter => _programCounter;

        public ScriptMachineStatus Status => _status;

        public bool IsLatched => _latched;

        public ScriptHostRequest? PendingHostRequest => _pendingHostRequest;

        /// <summary>
        /// 現在の要求と同じ参照だけを1回受理する。成功は PC を1つ進め Ready、
        /// 失敗は当該 PC の HostCommandFailed にラッチする。どちらも次命令は実行しない。
        /// null・別機械・古い・完了済みの要求は、機械を変更せず拒否する。
        /// </summary>
        public bool TryCompleteHostCommand(ScriptHostRequest request, bool succeeded)
        {
            if (_latched || request == null || !ReferenceEquals(_pendingHostRequest, request))
            {
                return false;
            }

            _pendingHostRequest = null;
            if (succeeded)
            {
                _programCounter++;
                _status = ScriptMachineStatus.Ready;
            }
            else
            {
                Latch(ScriptMachineStatus.HostCommandFailed);
            }

            return true;
        }

        /// <summary>
        /// 正の予算だけ命令を実行する。予算不足は終端・host 待機より先に判定し、保存状態を変更しない。
        /// 予算を使い切ると Yielded。末尾到達が最後の命令と同時なら、次の正の Tick で Halted にする。
        /// Halt はその命令の PC、自然終端は命令数の PC にラッチする。
        /// 使用レジスタ・実行する跳躍先・opcode の不正は型付き故障にラッチし、
        /// 故障命令の PC と書き込み先を変えない。それ以前に完了した命令の結果は保持する。
        /// host 到達は1命令分を消費して PC 不変で待機する。再 Tick は同じ要求を保ち、予算を消費しない。
        /// </summary>
        public ScriptMachineStatus Tick(int instructionBudget)
        {
            // 拒否された呼び出しは、終端の再確認を含め機械の状態に関与しない。
            if (instructionBudget < 1)
            {
                return ScriptMachineStatus.RejectedBudget;
            }

            if (_latched)
            {
                return _status;
            }

            if (_pendingHostRequest != null)
            {
                return ScriptMachineStatus.WaitingForHost;
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

                    case ScriptOpcode.HostCommand:
                        // 境界の初回到達だけで確保する。要求自身を完了 token とし、再発行しない。
                        _pendingHostRequest = new ScriptHostRequest(instruction.Destination, instruction.Immediate);
                        _status = ScriptMachineStatus.WaitingForHost;
                        return _status;

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

            // 条件レジスタは常に使うが、跳躍しない命令の target は未使用なので検証しない。
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
