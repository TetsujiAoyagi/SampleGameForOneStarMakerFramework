#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.UISystem.Behaviors;

namespace OneStarMaker.Tests.UISystem.TestDoubles
{
    /// <summary>
    /// 即時完了し、共有リストへ実行順序を記録するテスト用 Behavior。
    /// </summary>
    public sealed class RecordingBehavior : IUIBehavior, ISnapBehavior
    {
        private readonly string _name;
        private readonly List<string> _order;

        /// <summary>
        /// 記録名と共有順序リストを指定する。
        /// </summary>
        /// <param name="name">記録に使う識別子。</param>
        /// <param name="order">実行順序を追記するリスト。</param>
        public RecordingBehavior(string name, List<string> order)
        {
            _name = name;
            _order = order;
        }

        /// <inheritdoc/>
        public UniTask ExecuteAsync(UIBehaviorContext context, CancellationToken ct)
        {
            _order.Add(_name);
            return UniTask.CompletedTask;
        }

        /// <inheritdoc/>
        public void SnapToEnd(UIBehaviorContext context)
        {
        }
    }
}
