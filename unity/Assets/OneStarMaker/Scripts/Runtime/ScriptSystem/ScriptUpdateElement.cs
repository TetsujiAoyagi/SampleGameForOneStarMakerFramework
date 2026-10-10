#nullable enable

using System;
using OneStarMaker.Foundation.UpdateSystem;

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>
    /// UpdateSystem の Update で <see cref="ScriptMachine"/> を 1 回進める要素。
    /// 実行時の登録・解除は呼び出し側が UpdateSystemRuntime を通して行い、
    /// 解除を済ませてから借用した機械・プログラム・レジスタへの参照を手放す。
    /// UpdateCoordinator の直接利用は、独立した決定的テストのための seam である。
    /// この型は Layer・寿命・Scene 所有者を選ばず、機械を借りて固定予算で進めるだけである。
    /// host 要求は処理しない。外側が要求を完了するまで同じ境界で待機する。
    /// 同じ機械を ScriptCommandRunner と二重登録しない。
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
