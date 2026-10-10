#nullable enable

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>
    /// <see cref="ScriptMachine.Tick"/> が返す結果。
    /// <see cref="Halted"/>、<see cref="InvalidRegister"/>、<see cref="InvalidJump"/>、
    /// <see cref="InvalidOpcode"/>、<see cref="HostCommandFailed"/> は機械にラッチされる。
    /// <see cref="WaitingForHost"/> は要求の完了待ち、<see cref="Ready"/> は成功完了後の次 Tick 待ち。
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
        WaitingForHost = 7,
        Ready = 8,
        HostCommandFailed = 9,
    }
}
