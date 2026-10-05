#nullable enable

using System;
using OneStarMaker.Foundation.UpdateSystem;

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>
    /// 単一 Run の機械・レジスタと共通 Wait を所有する、同一スレッド専用の更新要素。
    /// 登録と解除は呼び出し側の責務。停止 gate は解除の反映より先に閉じるため、
    /// scheduler に残る旧要素や callback 再入からアプリ操作が再実行されない。
    /// </summary>
    public sealed class ScriptCommandRunner : IUpdateElement, IDisposable
    {
        private readonly ScriptMachine _machine;
        private readonly ScriptCommandTimeSource _timeSource;
        private readonly int _instructionBudgetPerUpdate;
        private IScriptCommandHost? _host;
        private Action<ScriptCommandRunner, ScriptCommandRunState>? _stateChanged;
        private Action<ScriptCommandRunner>? _releaseRegistration;
        private ScriptHostRequest? _currentRequest;
        private double _waitSeconds;
        private bool _live = true;
        private bool _updating;
        private bool _notifying;
        private ScriptCommandRunState _state = ScriptCommandRunState.Running;

        public ScriptCommandRunner(
            ScriptProgram program,
            int registerCount,
            IScriptCommandHost host,
            ScriptCommandTimeSource timeSource,
            int instructionBudgetPerUpdate,
            Action<ScriptCommandRunner, ScriptCommandRunState>? stateChanged = null,
            Action<ScriptCommandRunner>? releaseRegistration = null)
        {
            if (program == null) throw new ArgumentNullException(nameof(program));
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (registerCount < 0) throw new ArgumentOutOfRangeException(nameof(registerCount));
            if (instructionBudgetPerUpdate < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(instructionBudgetPerUpdate));
            }
            if (timeSource != ScriptCommandTimeSource.Scaled && timeSource != ScriptCommandTimeSource.Unscaled)
            {
                throw new ArgumentOutOfRangeException(nameof(timeSource));
            }

            _machine = new ScriptMachine(program, new ScriptRegisters(registerCount));
            _host = host;
            _timeSource = timeSource;
            _instructionBudgetPerUpdate = instructionBudgetPerUpdate;
            _stateChanged = stateChanged;
            _releaseRegistration = releaseRegistration;
        }

        public ScriptCommandRunState State => _state;

        public void OnElementStart()
        {
        }

        public void OnElementLateUpdate(in UpdateFrameContext context)
        {
        }

        public void OnElementUpdate(in UpdateFrameContext context)
        {
            if (!_live || context.IsPaused || _updating) return;

            _updating = true;
            try
            {
                if (State == ScriptCommandRunState.Waiting)
                {
                    AdvanceWait(in context);
                    return;
                }

                var status = _machine.Tick(_instructionBudgetPerUpdate);
                switch (status)
                {
                    case ScriptMachineStatus.Yielded:
                        return;
                    case ScriptMachineStatus.Halted:
                        Close(ScriptCommandRunState.Halted);
                        return;
                    case ScriptMachineStatus.WaitingForHost:
                        var request = _machine.PendingHostRequest;
                        if (request == null)
                        {
                            Close(ScriptCommandRunState.Failed);
                            return;
                        }
                        if (ReferenceEquals(_currentRequest, request)) return;

                        // 捕捉を dispatch より先に行い、callback 再入による重複実行を防ぐ。
                        _currentRequest = request;
                        Dispatch(request);
                        return;
                    default:
                        Close(ScriptCommandRunState.Failed);
                        return;
                }
            }
            finally
            {
                _updating = false;
            }
        }

        public void Stop()
        {
            Close(ScriptCommandRunState.Stopped);
        }

        public void Dispose()
        {
            Stop();
        }

        private void Dispatch(ScriptHostRequest request)
        {
            if (!Owns(request)) return;
            if (request.CommandId == ScriptCommands.WaitMillisecondsCommandId)
            {
                if (request.Argument < 0)
                {
                    Complete(request, succeeded: false);
                }
                else if (request.Argument == 0)
                {
                    Complete(request, succeeded: true);
                }
                else
                {
                    _waitSeconds = request.Argument / 1000.0;
                    _state = ScriptCommandRunState.Waiting;
                    NotifyStateChanged();
                }
                return;
            }

            if (request.CommandId < 0)
            {
                Complete(request, succeeded: false);
                return;
            }

            var host = _host;
            if (host == null) return;
            ScriptCommandResult result;
            try
            {
                result = host.Execute(request.CommandId, request.Argument);
            }
            catch (Exception)
            {
                // 操作済みでも rollback / retry しない。操作内の Stop は故障で上書きしない。
                if (Owns(request)) Complete(request, succeeded: false);
                return;
            }

            if (Owns(request)) Complete(request, result == ScriptCommandResult.Completed);
        }

        private void AdvanceWait(in UpdateFrameContext context)
        {
            var request = _currentRequest;
            if (request == null || !Owns(request)) return;
            var delta = _timeSource == ScriptCommandTimeSource.Scaled
                ? context.DeltaTime
                : context.UnscaledDeltaTime;
            if (delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;

            _waitSeconds -= delta;
            if (_waitSeconds <= 0 && Owns(request)) Complete(request, succeeded: true);
            // 開始 Update の delta や超過時間を次命令に持ち越さず、満了時もここで戻る。
        }

        private bool Owns(ScriptHostRequest request)
        {
            return _live && ReferenceEquals(_currentRequest, request)
                && ReferenceEquals(_machine.PendingHostRequest, request);
        }

        private void Complete(ScriptHostRequest request, bool succeeded)
        {
            if (!Owns(request) || !_machine.TryCompleteHostCommand(request, succeeded)) return;

            _currentRequest = null;
            _waitSeconds = 0;
            if (!succeeded)
            {
                Close(ScriptCommandRunState.Failed);
                return;
            }

            if (State == ScriptCommandRunState.Waiting)
            {
                // 成功 ack が pending を消す。通知時には ack 前の同一性を再要求しない。
                _state = ScriptCommandRunState.Running;
                if (_live) NotifyStateChanged();
            }
        }

        private void NotifyStateChanged()
        {
            if (_notifying) return;
            var observer = _stateChanged;
            if (observer == null) return;

            _notifying = true;
            try
            {
                observer(this, State);
            }
            catch (Exception)
            {
                if (_live) Close(ScriptCommandRunState.Failed, notify: false);
            }
            finally
            {
                _notifying = false;
            }
        }

        private void Close(ScriptCommandRunState terminalState, bool notify = true)
        {
            if (!_live) return;
            _live = false;
            _state = terminalState;
            _currentRequest = null;
            _waitSeconds = 0;

            try
            {
                if (notify) NotifyStateChanged();
            }
            finally
            {
                try
                {
                    _releaseRegistration?.Invoke(this);
                }
                catch (Exception)
                {
                    // 実際の登録除去の成功は保証しないが、停止と参照解放は維持する。
                }
                finally
                {
                    _host = null;
                    _stateChanged = null;
                    _releaseRegistration = null;
                }
            }
        }
    }
}
