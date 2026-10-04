#nullable enable

using System;

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>
    /// 呼び出し側が渡した命令列の不変コピー。
    /// コピーは Create のときだけで、tick では配列を確保し直さない。
    /// 入力配列を後から書き換えても、実行中の列が変わらないようにするためである。
    /// </summary>
    public sealed class ScriptProgram
    {
        private readonly ScriptInstruction[] _instructions;

        private ScriptProgram(ScriptInstruction[] instructions)
        {
            _instructions = instructions;
        }

        public int InstructionCount => _instructions.Length;

        public static ScriptProgram Create(ScriptInstruction[] instructions)
        {
            if (instructions == null)
            {
                throw new ArgumentNullException(nameof(instructions));
            }

            var copy = new ScriptInstruction[instructions.Length];
            Array.Copy(instructions, copy, instructions.Length);
            return new ScriptProgram(copy);
        }

        internal ScriptInstruction GetInstruction(int index)
        {
            return _instructions[index];
        }
    }
}
