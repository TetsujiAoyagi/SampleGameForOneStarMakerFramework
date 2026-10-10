#nullable enable

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>
    /// 呼び出し側が組み立てる命令の、現在のメモリ上の番号。
    /// 保存バイトコードやファイル形式の ABI・互換性を約束するものではない。
    /// </summary>
    public enum ScriptOpcode : byte
    {
        Halt = 0,
        LoadImmediate = 1,
        Move = 2,
        Add = 3,
        Sub = 4,
        Mul = 5,
        Jump = 6,
        JumpIfZero = 7,
        JumpIfNotZero = 8,
        HostCommand = 9,
    }
}
