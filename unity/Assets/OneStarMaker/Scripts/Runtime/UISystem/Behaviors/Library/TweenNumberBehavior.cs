#nullable enable

using System;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneStarMaker.Runtime.UISystem.Behaviors.Library
{
    /// <summary>
    /// Payload の old→new（int）を補間し、Label へ整数文字列を表示する Behavior。
    /// <see cref="VisualStateStore.CurrentTransitionKey"/> を更新して FromCurrent 割り込みの起点とする。
    /// </summary>
    [Serializable]
    public sealed class TweenNumberBehavior : IUIBehavior, ISnapBehavior
    {
        [SerializeField] private float _duration = 0.3f;
        [SerializeField] private Ease _ease = Ease.OutQuad;

        /// <summary>
        /// デフォルトパラメータで生成する（SerializeReference 用）。
        /// </summary>
        public TweenNumberBehavior()
        {
        }

        /// <summary>
        /// パラメータを指定して生成する。
        /// </summary>
        /// <param name="duration">補間時間（秒）。</param>
        /// <param name="ease">イージング。</param>
        public TweenNumberBehavior(float duration, Ease ease)
        {
            _duration = duration;
            _ease = ease;
        }

        /// <inheritdoc/>
        public async UniTask ExecuteAsync(UIBehaviorContext context, CancellationToken ct)
        {
            var oldValue = context.Payload.GetOld<int>();
            var newValue = context.Payload.NewValue is int n ? n : oldValue;

            context.VisualState.Set(VisualStateStore.EstimatedDurationKey, _duration);

            var lastDisplayed = int.MinValue;
            string? lastText = null;

            if (context.Target is Label label)
            {
                await LMotion.Create(oldValue, newValue, _duration)
                    .WithEase(_ease)
                    .Bind(value => ApplyDisplay(context, label, value, ref lastDisplayed, ref lastText))
                    .ToUniTask(ct);
            }
            else
            {
                await LMotion.Create(oldValue, newValue, _duration)
                    .WithEase(_ease)
                    .Bind(value => context.VisualState.Set(VisualStateStore.CurrentTransitionKey, value))
                    .ToUniTask(ct);
            }
        }

        /// <inheritdoc/>
        public void SnapToEnd(UIBehaviorContext context)
        {
            var newValue = context.Payload.GetNew<int>();
            var lastDisplayed = int.MinValue;
            string? lastText = null;

            if (context.Target is Label label)
            {
                ApplyDisplay(context, label, newValue, ref lastDisplayed, ref lastText);
                return;
            }

            context.VisualState.Set(VisualStateStore.CurrentTransitionKey, newValue);
        }

        private static void ApplyDisplay(
            UIBehaviorContext context,
            Label label,
            int value,
            ref int lastDisplayed,
            ref string? lastText)
        {
            context.VisualState.Set(VisualStateStore.CurrentTransitionKey, value);

            if (value == lastDisplayed)
            {
                return;
            }

            lastDisplayed = value;
            var text = ZString.Format("{0}", value);
            if (lastText == text)
            {
                return;
            }

            lastText = text;
            label.text = text;
        }
    }
}
