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
    /// <see cref="VisualElement.style.translate"/> を水平方向に揺らし、終了時に原点へ戻す Behavior。
    /// </summary>
    [Serializable]
    public sealed class ShakeBehavior : IUIBehavior, ISnapBehavior
    {
        [SerializeField] private float _amplitude = 6f;
        [SerializeField] private float _duration = 0.3f;
        [SerializeField] private int _frequency = 10;

        /// <summary>
        /// デフォルトパラメータで生成する（SerializeReference 用）。
        /// </summary>
        public ShakeBehavior()
        {
        }

        /// <summary>
        /// パラメータを指定して生成する。
        /// </summary>
        /// <param name="amplitude">揺れ幅（ピクセル）。</param>
        /// <param name="duration">揺れ時間（秒）。</param>
        /// <param name="frequency">振動回数。</param>
        public ShakeBehavior(float amplitude, float duration, int frequency)
        {
            _amplitude = amplitude;
            _duration = duration;
            _frequency = frequency;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// キャンセル等の例外で await を抜けた場合、末尾のリセットは実行されないが、
        /// その経路では Runner が <see cref="SnapToEnd"/> を呼ぶため translate(0,0) への収束は保たれる。
        /// </remarks>
        public async UniTask ExecuteAsync(UIBehaviorContext context, CancellationToken ct)
        {
            var element = context.Target;

            await LMotion.Shake.Create(0f, _amplitude, _duration)
                .WithFrequency(_frequency)
                .Bind(x => element.style.translate = new Translate(x, 0f))
                .ToUniTask(ct);

            ResetTranslate(element);
        }

        /// <inheritdoc/>
        public void SnapToEnd(UIBehaviorContext context)
        {
            ResetTranslate(context.Target);
        }

        private static void ResetTranslate(VisualElement element)
        {
            element.style.translate = new Translate(0f, 0f);
        }
    }
}
