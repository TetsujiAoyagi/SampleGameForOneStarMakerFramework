#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.UISystem;
using OneStarMaker.Runtime.UISystem.Behaviors;
using OneStarMaker.Runtime.UISystem.Behaviors.Library;
using OneStarMaker.Runtime.UISystem.Mvvm;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace SampleGame.OutGame.ConfirmDialog
{
    /// <summary>
    /// 確認ダイアログの UI Toolkit ビュー。ViewIn / ViewOut を BehaviorRunner 経由で演出する。
    /// </summary>
    public sealed class ConfirmDialogView : UIToolkitView
    {
        private readonly CompositeDisposable _disposables = new();
        private BehaviorRunner? _runner;
        private ConfirmDialogViewModel? _viewModel;

        /// <summary>OK / Cancel の結果通知（ViewModel から転送）。</summary>
        public event Action<bool>? Decided;

        /// <inheritdoc />
        public override UILayer GetUILayer() => UILayer.Dialog;

        /// <inheritdoc />
        protected override void OnRootCreated(VisualElement root)
        {
            _viewModel = new ConfirmDialogViewModel
            {
                Message = "HPを回復しますか？",
            };
            SetViewModel(_viewModel);
            _viewModel.Decided += HandleViewModelDecided;

            var panel = root.Q<VisualElement>("dialog-panel")
                ?? throw new InvalidOperationException("dialog-panel が見つかりません。");
            var messageLabel = root.Q<Label>("message-label")
                ?? throw new InvalidOperationException("message-label が見つかりません。");
            var okButton = root.Q<Button>("ok-button")
                ?? throw new InvalidOperationException("ok-button が見つかりません。");
            var cancelButton = root.Q<Button>("cancel-button")
                ?? throw new InvalidOperationException("cancel-button が見つかりません。");

            messageLabel.text = _viewModel.Message;

            panel.style.opacity = 0f;
            panel.style.scale = new Scale(new Vector3(0.8f, 0.8f, 1f));

            _runner = new BehaviorRunner(panel, InterruptPolicy.Rewind);
            _disposables.Add(_runner);

            _disposables.Add(okButton.BindClick(() => _viewModel.Decide(true)));
            _disposables.Add(cancelButton.BindClick(() => _viewModel.Decide(false)));
        }

        /// <inheritdoc />
        public override async UniTask ViewIn(CancellationToken ct)
        {
            EnsureRootCreated();

            await _runner!.Run(
                new ParallelBehavior(
                    new FadeBehavior(0f, 1f, 0.25f),
                    new ScaleBehavior(0.8f, 1f, 0.25f)),
                new TransitionPayload(false, true),
                ct);
        }

        /// <inheritdoc />
        public override async UniTask ViewOut()
        {
            if (_runner == null)
            {
                return;
            }

            // startFromCurrent: Opening 途中の Rewind 直後でも resolvedStyle の現在値から
            // フェード/スケールを開始し、opacity 0 → 1 等の視覚ジャンプを防ぐ。
            await _runner.Run(
                new ParallelBehavior(
                    new FadeBehavior(1f, 0f, 0.25f, startFromCurrent: true),
                    new ScaleBehavior(1f, 0.8f, 0.25f, startFromCurrent: true)),
                new TransitionPayload(true, false),
                CancellationToken.None);
        }

        /// <inheritdoc />
        protected override void OnViewDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.Decided -= HandleViewModelDecided;
            }

            _disposables.Dispose();
        }

        private void HandleViewModelDecided(bool accepted)
        {
            Decided?.Invoke(accepted);
        }
    }
}
