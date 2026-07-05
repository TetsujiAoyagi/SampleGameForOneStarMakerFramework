#nullable enable

using System;
using OneStarMaker.Runtime.UISystem.Mvvm;
using R3;

namespace SampleGame.OutGame.HpGauge
{
    /// <summary>
    /// HP ゲージ画面の ViewModel。Stable State は <see cref="Hp"/> のみ。
    /// </summary>
    public sealed class HpGaugeViewModel : ViewModelBase
    {
        private readonly ReactiveProperty<int> _hp = new(100);
        private readonly Random _random = new();

        /// <summary>現在 HP（0〜100）。</summary>
        public ReadOnlyReactiveProperty<int> Hp => _hp;

        /// <summary>5〜25 のランダムダメージを与える。</summary>
        public void Damage()
        {
            var amount = _random.Next(5, 26);
            _hp.Value = Math.Clamp(_hp.Value - amount, 0, 100);
        }

        /// <summary>HP を 20 回復する（上限 100）。</summary>
        public void Heal()
        {
            _hp.Value = Math.Clamp(_hp.Value + 20, 0, 100);
        }

        /// <inheritdoc />
        protected override void DisposeCore()
        {
            _hp.Dispose();
        }
    }
}
