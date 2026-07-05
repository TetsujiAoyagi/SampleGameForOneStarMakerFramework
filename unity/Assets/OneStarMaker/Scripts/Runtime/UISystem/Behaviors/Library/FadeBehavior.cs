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
    /// <see cref="VisualElement.style.opacity"/> を from→to へ補間する Behavior。
    /// Rewind 時は resolvedStyle の現在値から from へ逆補間する。
    /// </summary>
    [Serializable]
    public sealed class FadeBehavior : IUIBehavior, ISnapBehavior, IRewindableBehavior
    {
        [SerializeField] private float _from = 0f;
        [SerializeField] private float _to = 1f;
        [SerializeField] private float _duration = 0.25f;

        /// <summary>
        /// true の場合、開始値を <see cref="_from"/> ではなく resolvedStyle の現在値にする。
        /// 割り込み逆再生（Rewind）後の再遷移でジャンプを防ぐためのオプション。
        /// </summary>
        [SerializeField] private bool _startFromCurrent;

        /// <summary>
        /// デフォルトパラメータで生成する（SerializeReference 用）。
        /// </summary>
        public FadeBehavior()
        {
        }

        /// <summary>
        /// パラメータを指定して生成する。
        /// </summary>
        /// <param name="from">開始不透明度。</param>
        /// <param name="to">終了不透明度。</param>
        /// <param name="duration">補間時間（秒）。</param>
        /// <param name="startFromCurrent">
        /// true なら開始値に resolvedStyle の現在値を使う（取得不能時は <paramref name="from"/>）。
        /// 割り込み逆再生後の再遷移でジャンプを防ぐためのオプション。
        /// </param>
        public FadeBehavior(float from, float to, float duration, bool startFromCurrent = false)
        {
            _from = from;
            _to = to;
            _duration = duration;
            _startFromCurrent = startFromCurrent;
        }

        /// <inheritdoc/>
        public async UniTask ExecuteAsync(UIBehaviorContext context, CancellationToken ct)
        {
            context.VisualState.Set(VisualStateStore.EstimatedDurationKey, _duration);

            var element = context.Target;
            var startOpacity = _startFromCurrent
                ? ResolveCurrentOpacity(element, _from)
                : _from;

            await LMotion.Create(startOpacity, _to, _duration)
                .Bind(opacity => element.style.opacity = opacity)
                .ToUniTask(ct);
        }

        /// <inheritdoc/>
        public async UniTask RewindAsync(UIBehaviorContext context, float progress, CancellationToken ct)
        {
            var current = ResolveCurrentOpacity(context.Target, _to);
            var rewindDuration = _duration * progress;

            if (rewindDuration <= 0f)
            {
                context.Target.style.opacity = _from;
                return;
            }

            var element = context.Target;
            await LMotion.Create(current, _from, rewindDuration)
                .Bind(opacity => element.style.opacity = opacity)
                .ToUniTask(ct);
        }

        /// <inheritdoc/>
        public void SnapToEnd(UIBehaviorContext context)
        {
            context.Target.style.opacity = _to;
        }

        /// <summary>
        /// resolvedStyle から現在の不透明度を取得する。
        /// レイアウト未計算などで値が不正な場合は forward 遷移の終端（<see cref="_to"/>）を使う。
        /// </summary>
        private static float ResolveCurrentOpacity(VisualElement target, float fallback)
        {
            var opacity = target.resolvedStyle.opacity;
            if (float.IsNaN(opacity) || float.IsInfinity(opacity))
            {
                return fallback;
            }

            return opacity;
        }
    }
}
