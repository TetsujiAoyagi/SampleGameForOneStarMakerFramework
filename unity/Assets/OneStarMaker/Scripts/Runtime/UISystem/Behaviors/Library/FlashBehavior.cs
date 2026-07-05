#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneStarMaker.Runtime.UISystem.Behaviors.Library
{
    /// <summary>
    /// テキスト色を flashColor へ変化させ、元色へ戻す Behavior。
    /// 元色は初回実行時のみ VisualState へ保存する。
    /// </summary>
    [Serializable]
    public sealed class FlashBehavior : IUIBehavior, ISnapBehavior
    {
        private const string OriginalColorKey = "flash.originalColor";

        private static readonly Color UnsetColor = new(float.NaN, float.NaN, float.NaN, float.NaN);

        [SerializeField] private Color _flashColor = Color.red;
        [SerializeField] private float _duration = 0.2f;

        /// <summary>
        /// デフォルトパラメータで生成する（SerializeReference 用）。
        /// </summary>
        public FlashBehavior()
        {
        }

        /// <summary>
        /// パラメータを指定して生成する。
        /// </summary>
        /// <param name="flashColor">フラッシュ色。</param>
        /// <param name="duration">フラッシュ往復の合計時間（秒）。</param>
        public FlashBehavior(Color flashColor, float duration)
        {
            _flashColor = flashColor;
            _duration = duration;
        }

        /// <inheritdoc/>
        public async UniTask ExecuteAsync(UIBehaviorContext context, CancellationToken ct)
        {
            var originalColor = ResolveOriginalColor(context);
            var element = context.Target;
            var halfDuration = _duration * 0.5f;

            await LMotion.Create(originalColor, _flashColor, halfDuration)
                .Bind(color => element.style.color = color)
                .ToUniTask(ct);

            await LMotion.Create(_flashColor, originalColor, halfDuration)
                .Bind(color => element.style.color = color)
                .ToUniTask(ct);
        }

        /// <inheritdoc/>
        public void SnapToEnd(UIBehaviorContext context)
        {
            var originalColor = ResolveOriginalColor(context);
            context.Target.style.color = originalColor;
        }

        private static Color ResolveOriginalColor(UIBehaviorContext context)
        {
            var stored = context.VisualState.GetOr(OriginalColorKey, UnsetColor);
            if (!float.IsNaN(stored.r))
            {
                return stored;
            }

            var originalColor = context.Target.resolvedStyle.color;
            context.VisualState.Set(OriginalColorKey, originalColor);
            return originalColor;
        }
    }
}
