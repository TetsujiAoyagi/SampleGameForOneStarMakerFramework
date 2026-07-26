#nullable enable

using System;
using OneStarMaker.Runtime.UISystem;
using OneStarMaker.Runtime.UISystem.Mvvm;
using R3;
using UnityEngine.UIElements;

namespace SampleGame.InGame.UI
{
    /// <summary>
    /// InGame HUD の UIToolkit View。
    /// ViewModel の生成と要素バインドのみを行い、Session / Player への到達は Scene 側が担う。
    /// </summary>
    public sealed class InGameHudView : UIToolkitView
    {
        private InGameHudViewModel? _viewModel;

        /// <summary>Scene が Session サービスへ配線するための ViewModel。</summary>
        public InGameHudViewModel? ViewModel => _viewModel;

        /// <inheritdoc />
        /// <remarks>HUD は常時表示の通常レイヤー。Modal には載せない。</remarks>
        public override UILayer GetUILayer() => UILayer.Normal;

        /// <inheritdoc />
        protected override void OnRootCreated(VisualElement root)
        {
            _viewModel = new InGameHudViewModel();
            SetViewModel(_viewModel);

            var currentLabel = root.Q<Label>("current-label")
                ?? throw new InvalidOperationException("current-label が見つかりません。");
            var loadedLabel = root.Q<Label>("loaded-label")
                ?? throw new InvalidOperationException("loaded-label が見つかりません。");
            var posLabel = root.Q<Label>("pos-label")
                ?? throw new InvalidOperationException("pos-label が見つかりません。");
            var helpLabel = root.Q<Label>("help-label")
                ?? throw new InvalidOperationException("help-label が見つかりません。");
            var overlayPanel = root.Q<VisualElement>("overlay-panel")
                ?? throw new InvalidOperationException("overlay-panel が見つかりません。");
            var overlayTitle = root.Q<Label>("overlay-title")
                ?? throw new InvalidOperationException("overlay-title が見つかりません。");
            var overlayBody = root.Q<Label>("overlay-body")
                ?? throw new InvalidOperationException("overlay-body が見つかりません。");

            helpLabel.text = _viewModel.ControlsHelp;

            // 購読寿命は View.Track に載せる（E-8）。ViewModel 側の ReactiveProperty 破棄は DisposeCore。
            Track(_viewModel.CurrentLevel.Subscribe(v => currentLabel.text = $"Current: {v}  [{_viewModel.BusyLabel.CurrentValue}]"));
            Track(_viewModel.BusyLabel.Subscribe(v =>
                currentLabel.text = $"Current: {_viewModel.CurrentLevel.CurrentValue}  [{v}]"));
            Track(_viewModel.LoadedLevels.Subscribe(v => loadedLabel.text = $"Loaded: {v}"));
            Track(_viewModel.PositionLabel.Subscribe(v => posLabel.text = $"Pos: {v}"));
            Track(_viewModel.OverlayTitle.Subscribe(v => overlayTitle.text = v));
            Track(_viewModel.OverlayBody.Subscribe(v => overlayBody.text = v));
            Track(_viewModel.OverlayVisible.Subscribe(visible =>
            {
                overlayPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }));
        }
    }
}
