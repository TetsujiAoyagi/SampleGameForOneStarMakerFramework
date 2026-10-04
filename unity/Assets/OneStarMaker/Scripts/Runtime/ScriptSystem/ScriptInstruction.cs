#nullable enable

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>
    /// 1 命令分のオペランド。
    /// 工場メソッドが使わない欄は 0 だが、実行は未使用欄を見ない。
    /// レジスタ番号が配列に収まるかは、プログラム作成時ではなく実行時に見る。
    /// レジスタ本数は呼び出し側の所有であり、同じ命令列を別の本数と組む余地を残すためである。
    /// </summary>
    public readonly struct ScriptInstruction
    {
        private ScriptInstruction(ScriptOpcode opcode, int destination, int left, int right, long immediate)
        {
            Opcode = opcode;
            Destination = destination;
            Left = left;
            Right = right;
            Immediate = immediate;
        }

        public ScriptOpcode Opcode { get; }

        public int Destination { get; }

        public int Left { get; }

        public int Right { get; }

        public long Immediate { get; }

        public static ScriptInstruction Halt()
        {
            return new ScriptInstruction(ScriptOpcode.Halt, 0, 0, 0, 0);
        }

        public static ScriptInstruction LoadImmediate(int destination, long value)
        {
            return new ScriptInstruction(ScriptOpcode.LoadImmediate, destination, 0, 0, value);
        }

        public static ScriptInstruction Move(int destination, int source)
        {
            return new ScriptInstruction(ScriptOpcode.Move, destination, source, 0, 0);
        }

        public static ScriptInstruction Add(int destination, int left, int right)
        {
            return new ScriptInstruction(ScriptOpcode.Add, destination, left, right, 0);
        }

        public static ScriptInstruction Sub(int destination, int left, int right)
        {
            return new ScriptInstruction(ScriptOpcode.Sub, destination, left, right, 0);
        }

        public static ScriptInstruction Mul(int destination, int left, int right)
        {
            return new ScriptInstruction(ScriptOpcode.Mul, destination, left, right, 0);
        }

        public static ScriptInstruction Jump(long target)
        {
            return new ScriptInstruction(ScriptOpcode.Jump, 0, 0, 0, target);
        }

        public static ScriptInstruction JumpIfZero(int condition, long target)
        {
            return new ScriptInstruction(ScriptOpcode.JumpIfZero, condition, 0, 0, target);
        }

        public static ScriptInstruction JumpIfNotZero(int condition, long target)
        {
            return new ScriptInstruction(ScriptOpcode.JumpIfNotZero, condition, 0, 0, target);
        }

        /// <summary>
        /// 数値 opcode とオペランドからメモリ上の命令を作る。保存形式やテキストは解釈しない。
        /// 表に無い番号もここでは受け、実行時に <see cref="ScriptMachineStatus.InvalidOpcode"/> とする。
        /// 使用しないオペランド欄は検証しない。ファイル形式の ABI を定義する口ではない。
        /// </summary>
        public static ScriptInstruction FromRaw(byte opcode, int destination, int left, int right, long immediate)
        {
            return new ScriptInstruction((ScriptOpcode)opcode, destination, left, right, immediate);
        }
    }
}
