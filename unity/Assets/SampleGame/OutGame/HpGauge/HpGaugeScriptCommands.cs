#nullable enable

using System;
using OneStarMaker.Runtime.ScriptSystem;

namespace SampleGame.OutGame.HpGauge
{
    internal enum HpGaugeScriptCommand
    {
        Damage = 0,
        Heal = 1,
    }

    /// <summary>
    /// Run の寿命で既存 HP 操作を借用する、同期的な app command 境界。
    /// 共通 Wait は runner が処理し、この host へ渡さない。
    /// </summary>
    public sealed class HpGaugeScriptCommands : IScriptCommandHost
    {
        private readonly Action _damage;
        private readonly Action _heal;

        public HpGaugeScriptCommands(Action damage, Action heal)
        {
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _heal = heal ?? throw new ArgumentNullException(nameof(heal));
        }

        public ScriptCommandResult Execute(int commandId, long argument)
        {
            if (argument != 0)
            {
                return ScriptCommandResult.Rejected;
            }

            switch ((HpGaugeScriptCommand)commandId)
            {
                case HpGaugeScriptCommand.Damage:
                    _damage();
                    break;
                case HpGaugeScriptCommand.Heal:
                    _heal();
                    break;
                default:
                    return ScriptCommandResult.Rejected;
            }

            // 例外は runner に伝え、操作後に throw しても再実行しない。
            return ScriptCommandResult.Completed;
        }
    }
}
