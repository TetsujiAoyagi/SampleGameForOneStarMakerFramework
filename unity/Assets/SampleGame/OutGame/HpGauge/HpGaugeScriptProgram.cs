#nullable enable

using OneStarMaker.Runtime.ScriptSystem;

namespace SampleGame.OutGame.HpGauge
{
    /// <summary>
    /// HP 操作を3巡だけ要求する、共有可能な不変のサンプル命令列。
    /// 操作内容は app、待機と実行寿命は Framework が所有する。
    /// </summary>
    public static class HpGaugeScriptProgram
    {
        public static int RegisterCount => 2;

        public static ScriptProgram Program { get; } = ScriptProgram.Create(new[]
        {
            ScriptInstruction.LoadImmediate(0, 3),
            ScriptInstruction.LoadImmediate(1, 1),
            ScriptInstruction.HostCommand((int)HpGaugeScriptCommand.Damage, 0),
            ScriptCommands.WaitMilliseconds(500),
            ScriptInstruction.HostCommand((int)HpGaugeScriptCommand.Heal, 0),
            ScriptCommands.WaitMilliseconds(500),
            ScriptInstruction.Sub(0, 0, 1),
            ScriptInstruction.JumpIfNotZero(0, 2),
            ScriptInstruction.Halt(),
        });
    }
}
