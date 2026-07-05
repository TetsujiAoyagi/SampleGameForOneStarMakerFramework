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
    /// <see cref="VisualElement.style.scale"/> を均一スケールで from→to へ補間する Behavior。
    /// Rewind 時は resolvedStyle の現在値から from へ逆補間する。
    /// </summary>
    [Serializable]
    public sealed class ScaleBehavior : IUIBehavior, ISnapBehavior, IRewindableBehavior
    {
        [SerializeField] private float _from = 0.8f;
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
        public ScaleBehavior()
        {
        }

        /// <summary>
        /// パラメータを指定して生成する。
        /// </summary>
        /// <param name="from">開始スケール（均一）。</param>
        /// <param name="to">終了スケール（均一）。</param>
        /// <param name="duration">補間時間（秒）。</param>
        /// <param name="startFromCurrent">
        /// true なら開始値に resolvedStyle の現在値を使う（取得不能時は <paramref name="from"/>）。
        /// 割り込み逆再生後の再遷移でジャンプを防ぐためのオプション。
        /// </param>
        public ScaleBehavior(float from, float to, float duration, bool startFromCurrent = false)
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
            var startScale = _startFromCurrent
                ? ResolveCurrentUniformScale(element, _from)
                : _from;

            await LMotion.Create(startScale, _to, _duration)
                .Bind(scale => element.style.scale = new Scale(new Vector3(scale, scale, 1f)))
                .ToUniTask(ct);
        }

        /// <inheritdoc/>
        public async UniTask RewindAsync(UIBehaviorContext context, float progress, CancellationToken ct)
        {
            var current = ResolveCurrentUniformScale(context.Target, _to);
            var rewindDuration = _duration * progress;

            if (rewindDuration <= 0f)
            {
                ApplyUniformScale(context.Target, _from);
                return;
            }

            var element = context.Target;
            await LMotion.Create(current, _from, rewindDuration)
                .Bind(scale => element.style.scale = new Scale(new Vector3(scale, scale, 1f)))
                .ToUniTask(ct);
        }

        /// <inheritdoc/>
        public void SnapToEnd(UIBehaviorContext context)
        {
            ApplyUniformScale(context.Target, _to);
        }

        private static void ApplyUniformScale(VisualElement target, float scale)
        {
            target.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        /// <summary>
        /// resolvedStyle.scale から均一スケールを取得する。
        /// レイアウト未計算時は style 未設定の既定 (1,1) が返るため、
        /// 不正値（NaN / 非正）の場合のみ forward 遷移の終端（<see cref="_to"/>）へフォールバックする。
        /// </summary>
        private static float ResolveCurrentUniformScale(VisualElement target, float fallback)
        {
            var scale = target.resolvedStyle.scale.value.x;
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            {
                return fallback;
            }

            return scale;
        }
    }
}
