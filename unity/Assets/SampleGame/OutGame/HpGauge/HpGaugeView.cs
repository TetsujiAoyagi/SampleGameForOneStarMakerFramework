#nullable enable

using System;
using OneStarMaker.Runtime.ScriptSystem;
using OneStarMaker.Runtime.UISystem;
using OneStarMaker.Runtime.UISystem.Behaviors;
using OneStarMaker.Runtime.UISystem.Behaviors.Library;
using OneStarMaker.Runtime.UISystem.Mvvm;
using OneStarMaker.Runtime.UpdateSystem.Api;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace SampleGame.OutGame.HpGauge
{
    /// <summary>HP ゲージ画面と、画面寿命のスクリプト登録を接続する。</summary>
    public sealed class HpGaugeView : UIToolkitView
    {
        private const string ScriptLayer = "HpGaugeScriptDemo";
        private HpGaugeViewModel? _viewModel;
        private ScriptCommandRunner? _currentRunner;
        private Label? _scriptStatus;
        private bool _registrationMayExist;
        private bool _shuttingDown;

        /// <summary>確認ダイアログを開く要求。</summary>
        public event Action? OnOpenDialogRequested;

        /// <inheritdoc />
        public override UILayer GetUILayer() => UILayer.Normal;

        /// <summary>Scene pre-unload または View 破棄で、現 Run を同期的に閉じる。</summary>
        internal void ShutdownScript()
        {
            if (_shuttingDown) return;
            _shuttingDown = true;
            _currentRunner?.Stop();
        }

        /// <inheritdoc />
        protected override void OnRootCreated(VisualElement root)
        {
            _viewModel = new HpGaugeViewModel();
            SetViewModel(_viewModel);
            // UIToolkitView は Track を ViewModel より先に破棄する。旧 runner の gate を先に閉じる。
            Track(Disposable.Create(ShutdownScript));

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
            var runButton = root.Q<Button>("run-script-button")
                ?? throw new InvalidOperationException("run-script-button が見つかりません。");
            var stopButton = root.Q<Button>("stop-script-button")
                ?? throw new InvalidOperationException("stop-script-button が見つかりません。");
            _scriptStatus = root.Q<Label>("script-status-label")
                ?? throw new InvalidOperationException("script-status-label が見つかりません。");
            _scriptStatus.text = "Idle";

            // 初期表示のみ直接代入。以降の数値更新は TweenNumberBehavior が担う。
            hpLabel.text = _viewModel.Hp.CurrentValue.ToString();
            Track(_viewModel.Hp.Subscribe(value => hpBar.value = value));
            Track(damageButton.BindClick(_viewModel.Damage));
            Track(healButton.BindClick(_viewModel.Heal));
            Track(openDialogButton.BindClick(() => OnOpenDialogRequested?.Invoke()));
            Track(runButton.BindClick(RunScript));
            Track(stopButton.BindClick(StopScript));

            var runner = new BehaviorRunner(hpLabel, InterruptPolicy.FromCurrent);
            Track(runner);
            var hpTransition = new ParallelBehavior(
                new TweenNumberBehavior(),
                new FlashBehavior(Color.red, 0.2f),
                new ShakeBehavior(6f, 0.3f, 10));
            Track(_viewModel.Hp.BindTransition(runner, hpTransition));
        }

        private void RunScript()
        {
            if (_shuttingDown || _currentRunner != null || _viewModel == null) return;
            var host = new HpGaugeScriptCommands(_viewModel.Damage, _viewModel.Heal);
            var runner = new ScriptCommandRunner(
                HpGaugeScriptProgram.Program,
                HpGaugeScriptProgram.RegisterCount,
                host,
                ScriptCommandTimeSource.Unscaled,
                16,
                OnScriptStateChanged,
                ReleaseRegistration);
            _currentRunner = runner;
            _registrationMayExist = true; // throw may mean partial registration; release then attempts removal.
            try
            {
                if (!UpdateSystemRuntime.RegisterElement(ScriptLayer, runner, 0, 0))
                {
                    _registrationMayExist = false;
                    runner.Stop();
                    SetScriptStatus("Failed");
                    return;
                }
            }
            catch (Exception error)
            {
                runner.Stop();
                SetScriptStatus("Failed");
                try
                {
                    Debug.LogWarning($"HpGauge script registration failed: {error}");
                }
                catch (Exception)
                {
                    // 診断先の失敗は、停止済み runner と Failed 表示を覆さない。
                }
                return;
            }
            if (_shuttingDown || !ReferenceEquals(_currentRunner, runner))
            {
                runner.Stop();
                return;
            }
            SetScriptStatus("Running");
        }

        private void StopScript()
        {
            _currentRunner?.Stop();
        }

        private void OnScriptStateChanged(ScriptCommandRunner runner, ScriptCommandRunState state)
        {
            if (!ReferenceEquals(_currentRunner, runner) || _shuttingDown) return;
            SetScriptStatus(state.ToString());
        }

        private void ReleaseRegistration(ScriptCommandRunner runner)
        {
            try
            {
                if (_registrationMayExist && !UpdateSystemRuntime.UnregisterElement(runner))
                    Debug.LogWarning("HpGauge script unregister was not confirmed.");
            }
            catch (Exception error)
            {
                Debug.LogWarning($"HpGauge script unregister failed: {error}");
            }
            finally
            {
                if (ReferenceEquals(_currentRunner, runner))
                {
                    _currentRunner = null;
                    _registrationMayExist = false;
                }
            }
        }

        private void SetScriptStatus(string status)
        {
            if (_scriptStatus != null) _scriptStatus.text = status;
        }
    }
}
