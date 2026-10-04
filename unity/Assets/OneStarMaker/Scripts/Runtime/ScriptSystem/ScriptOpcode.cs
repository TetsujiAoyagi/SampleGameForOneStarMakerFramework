#nullable enable

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>
    /// 内側 VM の命令番号。
    /// バイトコードの互換面なので、並べ替え・欠番・値の変更をしない。追加は末尾だけにする。
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
    }
}
