#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.UISystem;
using OneStarMaker.Runtime.UISystem.Behaviors;
using OneStarMaker.Runtime.UISystem.Behaviors.Library;
using OneStarMaker.Runtime.UISystem.Mvvm;
using SampleGame.OutGame.Background;
using UnityEngine;
using UnityEngine.UIElements;

namespace SampleGame.OutGame.Title
{
    /// <summary>
    /// タイトル画面の UI Toolkit View。
    /// 背景定義は Scene の presentation asset として保持し、描画は親 OutGame へ委譲する。
    /// </summary>
    public sealed class TitleView : UIToolkitView
    {
        [SerializeField]
        private OutGameBackgroundDefinition? _backgroundDefinition;

        private BehaviorRunner? _panelRunner;
        private BehaviorRunner? _titleRunner;
        private TitleViewModel? _viewModel;

        /// <summary>スタートボタン押下の要求。</summary>
        public event Action? OnStartRequested;

        /// <summary>タイトル画面が要求する共有背景。</summary>
        public OutGameBackgroundDefinition? BackgroundDefinition => _backgroundDefinition;

#if UNITY_EDITOR
        /// <summary>Editor のシーン生成ツールから共有背景を割り当てる。</summary>
        /// <param name="definition">タイトル表示時に要求する背景定義。</param>
        public void AssignBackgroundDefinitionForEditor(OutGameBackgroundDefinition definition)
        {
            _backgroundDefinition = definition
                ?? throw new ArgumentNullException(nameof(definition));
        }
#endif

        /// <inheritdoc />
        public override UILayer GetUILayer() => UILayer.Normal;

        /// <inheritdoc />
        protected override void OnRootCreated(VisualElement root)
        {
            _viewModel = new TitleViewModel();
            SetViewModel(_viewModel);

            var panel = root.Q<VisualElement>("title-panel")
                ?? throw new InvalidOperationException("title-panel が見つかりません。");
            var titleLabel = root.Q<Label>("title-label")
                ?? throw new InvalidOperationException("title-label が見つかりません。");
            var subtitleLabel = root.Q<Label>("subtitle-label")
                ?? throw new InvalidOperationException("subtitle-label が見つかりません。");
            var startButton = root.Q<Button>("start-button")
                ?? throw new InvalidOperationException("start-button が見つかりません。");
            var pulseButton = root.Q<Button>("pulse-button")
                ?? throw new InvalidOperationException("pulse-button が見つかりません。");

            Track(titleLabel.BindText(_viewModel.TitleText));
            Track(subtitleLabel.BindText(_viewModel.SubtitleText));
            Track(startButton.BindClick(() => OnStartRequested?.Invoke()));
            Track(pulseButton.BindClick(PlayTitlePulse));

            panel.style.opacity = 0f;
            panel.style.scale = new Scale(new Vector3(0.8f, 0.8f, 1f));

            _panelRunner = new BehaviorRunner(panel, InterruptPolicy.Rewind);
            Track(_panelRunner);

            _titleRunner = new BehaviorRunner(titleLabel, InterruptPolicy.FromCurrent);
            Track(_titleRunner);
        }

        /// <inheritdoc />
        public override async UniTask ViewIn(CancellationToken ct)
        {
            EnsureRootCreated();

            await _panelRunner!.Run(
                new ParallelBehavior(
                    new FadeBehavior(0f, 1f, 0.25f),
                    new ScaleBehavior(0.8f, 1f, 0.25f)),
                new TransitionPayload(false, true),
                ct);
        }

        /// <inheritdoc />
        public override async UniTask ViewOut()
        {
            if (_panelRunner == null)
            {
                return;
            }

            await _panelRunner.Run(
                new ParallelBehavior(
                    new FadeBehavior(1f, 0f, 0.25f, startFromCurrent: true),
                    new ScaleBehavior(1f, 0.8f, 0.25f, startFromCurrent: true)),
                new TransitionPayload(true, false),
                CancellationToken.None);
        }

        private void PlayTitlePulse()
        {
            if (_titleRunner == null)
            {
                return;
            }

            _titleRunner.Run(
                new ParallelBehavior(
                    new ShakeBehavior(6f, 0.3f, 10),
                    new FlashBehavior(Color.cyan, 0.2f)),
                new TransitionPayload(null, null),
                destroyCancellationToken).Forget();
        }
    }
}
