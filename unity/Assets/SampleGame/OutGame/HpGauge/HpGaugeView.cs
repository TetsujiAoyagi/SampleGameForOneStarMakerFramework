#nullable enable

using System;
using OneStarMaker.Runtime.UISystem;
using OneStarMaker.Runtime.UISystem.Behaviors;
using OneStarMaker.Runtime.UISystem.Behaviors.Library;
using OneStarMaker.Runtime.UISystem.Mvvm;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace SampleGame.OutGame.HpGauge
{
    /// <summary>
    /// HP ゲージ画面の UI Toolkit ビュー。
    /// </summary>
    public sealed class HpGaugeView : UIToolkitView
    {
        private HpGaugeViewModel? _viewModel;

        /// <summary>確認ダイアログを開く要求。</summary>
        public event Action? OnOpenDialogRequested;

        /// <inheritdoc />
        public override UILayer GetUILayer() => UILayer.Normal;

        /// <inheritdoc />
        protected override void OnRootCreated(VisualElement root)
        {
            _viewModel = new HpGaugeViewModel();
            SetViewModel(_viewModel);

            var hpLabel = root.Q<Label>("hp-label")
                ?? throw new InvalidOperationException("hp-label が見つかりません。");
            var hpBar = root.Q<ProgressBar>("hp-bar")
                ?? throw new InvalidOperationException("hp-bar が見つかりません。");
            var damageButton = root.Q<Button>("damage-button")
                ?? throw new InvalidOperationException("damage-button が見つかりません。");
            var healButton = root.Q<Button>("heal-button")
                ?? throw new InvalidOperationException("heal-button が見つかりません。");
            var openDialogButton = root.Q<Button>("open-dialog-button")
                ?? throw new InvalidOperationException("open-dialog-button が見つかりません。");

            // 初期表示のみ直接代入（1回きりで hot path ではないため ToString で足りる）。
            // 以降の HP 数値更新は TweenNumberBehavior が担う。
            hpLabel.text = _viewModel.Hp.CurrentValue.ToString();

            Track(_viewModel.Hp.Subscribe(value => hpBar.value = value));
            Track(damageButton.BindClick(_viewModel.Damage));
            Track(healButton.BindClick(_viewModel.Heal));
            Track(openDialogButton.BindClick(() => OnOpenDialogRequested?.Invoke()));

            var runner = new BehaviorRunner(hpLabel, InterruptPolicy.FromCurrent);
            Track(runner);

            var hpTransition = new ParallelBehavior(
                new TweenNumberBehavior(),
                new FlashBehavior(Color.red, 0.2f),
                new ShakeBehavior(6f, 0.3f, 10));

            Track(_viewModel.Hp.BindTransition(runner, hpTransition));
        }
    }
}
