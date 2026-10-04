#nullable enable

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>
    /// <see cref="ScriptMachine.Tick"/> が返す結果。
    /// <see cref="Halted"/>、<see cref="InvalidRegister"/>、<see cref="InvalidJump"/>、
    /// <see cref="InvalidOpcode"/> は機械にラッチされる。
    /// <see cref="RejectedBudget"/> は戻り値だけで、機械の <see cref="ScriptMachine.Status"/> には残さない。
    /// </summary>
    public enum ScriptMachineStatus : byte
    {
        NotStarted = 0,
        Yielded = 1,
        Halted = 2,
        InvalidRegister = 3,
        InvalidJump = 4,
        InvalidOpcode = 5,
        RejectedBudget = 6,
    }
}
