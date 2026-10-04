#nullable enable

using System;
using OneStarMaker.Foundation.UpdateSystem;

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>
    /// UpdateSystem の Update で <see cref="ScriptMachine"/> を 1 回進める要素。
    /// 登録は呼び出し側が UpdateCoordinator.RegisterElement で行う。
    /// この型は Layer を選ばず、Coordinator を保持せず、バイトコードもレジスタも所有しない。
    /// </summary>
    public sealed class ScriptUpdateElement : IUpdateElement
    {
        private readonly ScriptMachine _machine;
        private readonly int _instructionBudgetPerUpdate;

        public ScriptUpdateElement(ScriptMachine machine, int instructionBudgetPerUpdate)
        {
            _machine = machine ?? throw new ArgumentNullException(nameof(machine));
            if (instructionBudgetPerUpdate < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(instructionBudgetPerUpdate),
                    "Instruction budget per update must be at least 1.");
            }

            _instructionBudgetPerUpdate = instructionBudgetPerUpdate;
        }

        public ScriptMachine Machine => _machine;

        public void OnElementStart()
        {
        }

        public void OnElementUpdate(in UpdateFrameContext context)
        {
            // この命令セットに時間オペランドは無い。deltaTime で予算を増やすと
            // フレーム時間に比例して tick の仕事量が増えるので、予算は構築時の固定値のまま使う。
            _machine.Tick(_instructionBudgetPerUpdate);
        }

        public void OnElementLateUpdate(in UpdateFrameContext context)
        {
        }
    }
}
