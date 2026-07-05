#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace OneStarMaker.Runtime.UISystem.Behaviors
{
    /// <summary>
    /// Behavior パイプラインを ScriptableObject として直列化するアセット。
    /// </summary>
    [CreateAssetMenu(menuName = "OneStarMaker/UI/Behavior Asset")]
    public sealed class BehaviorAsset : ScriptableObject
    {
        /// <summary>
        /// ステップ列の合成モード。
        /// </summary>
        public enum CompositionMode
        {
            /// <summary>子 Behavior を順次実行する。</summary>
            Sequence,

            /// <summary>子 Behavior を並列実行する。</summary>
            Parallel,
        }

        [SerializeField] private CompositionMode _mode;
        [SerializeReference] private List<IUIBehavior> _steps = new();

        /// <summary>
        /// アセット定義から <see cref="IUIBehavior"/> を組み立てる。
        /// null 要素はスキップする。
        /// </summary>
        /// <returns>合成済み Behavior。有効ステップが空の場合は即時完了する Behavior。</returns>
        /// <remarks>
        /// 結果はキャッシュしない。Inspector 編集を次回呼び出しに即反映するため。
        /// 呼び出し側で必要ならキャッシュすること。
        /// </remarks>
        public IUIBehavior Build()
        {
            var steps = CollectNonNullSteps();
            if (steps.Length == 0)
            {
                return NullBehavior.Instance;
            }

            return _mode switch
            {
                CompositionMode.Sequence => new SequenceBehavior(steps),
                CompositionMode.Parallel => new ParallelBehavior(steps),
                _ => new SequenceBehavior(steps),
            };
        }

        private IUIBehavior[] CollectNonNullSteps()
        {
            if (_steps == null || _steps.Count == 0)
            {
                return System.Array.Empty<IUIBehavior>();
            }

            var count = 0;
            for (var i = 0; i < _steps.Count; i++)
            {
                if (_steps[i] != null)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return System.Array.Empty<IUIBehavior>();
            }

            var result = new IUIBehavior[count];
            var index = 0;
            for (var i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                if (step != null)
                {
                    result[index++] = step;
                }
            }

            return result;
        }

        /// <summary>
        /// 有効ステップが無い場合に返す、即時完了 Behavior。
        /// </summary>
        private sealed class NullBehavior : IUIBehavior
        {
            public static readonly NullBehavior Instance = new();

            /// <inheritdoc />
            public UniTask ExecuteAsync(UIBehaviorContext context, CancellationToken ct)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
