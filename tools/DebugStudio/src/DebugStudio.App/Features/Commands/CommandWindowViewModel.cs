#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Threading;
using DebugStudio.App.Core.Formatting;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Mvvm;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Features.Commands;

/// <summary>
/// Command window/panel 専用 ViewModel。
///
/// <para>
/// この wave では placeholder を卒業し、
/// requestId 相関・pending 監視・最小 raw command authoring をまとめる。
/// </para>
/// <para>
/// ただし command の意味付け自体は Unity 側 dispatcher の責務なので、
/// ここでは「送った」「待っている」「返った」「失敗した」を honest に見せることを優先する。
/// </para>
/// </summary>
public sealed class CommandWindowViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan PendingTimeout = TimeSpan.FromSeconds(15);

    private readonly Dispatcher _dispatcher;
    private readonly CommandStore _commandStore;
    private readonly CapabilityStateStore _capabilityStateStore;
    private readonly ICommandSender _commandSender;
    private readonly DispatcherTimer _timeoutTimer;
    private long _dispatchCount;
    private long _resultCount;
    private int _pendingCount;
    private string _commandType = "debugsocket.runtime-diagnostics";
    private string _payloadJson = "{}";
    private string _latestResult = "No command results yet.";
    private string _latestPayloadJson = "{}";
    private string _dispatchStatus = "Connect to a Unity session to run built-in validation commands.";
    private string _resultStatus = "No command replies have been received for this session.";
    private string _transportSummary = "Command transport is ready to correlate requestId with Unity replies.";
    private string _lastRequestId = "No command has been dispatched yet.";
    private string _builtInCommandGuide =
        "Built-in: debugsocket.runtime-diagnostics / runtime-diagnostics / debugsocket.ping / ping";

    public CommandWindowViewModel(
        Dispatcher dispatcher,
        CommandStore commandStore,
        CapabilityStateStore capabilityStateStore,
        ICommandSender commandSender)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _commandStore = commandStore ?? throw new ArgumentNullException(nameof(commandStore));
        _capabilityStateStore = capabilityStateStore ?? throw new ArgumentNullException(nameof(capabilityStateStore));
        _commandSender = commandSender ?? throw new ArgumentNullException(nameof(commandSender));

        History = new ObservableCollection<CommandHistoryItemViewModel>();
        SendCommand = new AsyncRelayCommand(SendCommandAsync, CanSendCommand);

        _commandStore.Changed += OnCommandStoreChanged;
        _capabilityStateStore.Changed += OnCapabilityChanged;

        // pending timeout を UI ライフサイクルに従わせるため、feature 側で軽量 timer を持つ。
        // service/store 自体は timer を抱えず、純粋な state mutation に留める。
        _timeoutTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, OnTimeoutTimerTick, _dispatcher);
        _timeoutTimer.Start();

        Refresh();
    }

    public long DispatchCount
    {
        get => _dispatchCount;
        private set => SetProperty(ref _dispatchCount, value);
    }

    public long ResultCount
    {
        get => _resultCount;
        private set => SetProperty(ref _resultCount, value);
    }

    public int PendingCount
    {
        get => _pendingCount;
        private set => SetProperty(ref _pendingCount, value);
    }

    public string CommandType
    {
        get => _commandType;
        set
        {
            if (SetProperty(ref _commandType, value))
            {
                SendCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string PayloadJson
    {
        get => _payloadJson;
        set => SetProperty(ref _payloadJson, value);
    }

    public string LatestResult
    {
        get => _latestResult;
        private set => SetProperty(ref _latestResult, value);
    }

    /// <summary>
    /// latest result の payload 本体。
    /// live validation では runtime diagnostics の JSON をそのまま読みたいので、
    /// summary 行とは別に独立表示する。
    /// </summary>
    public string LatestPayloadJson
    {
        get => _latestPayloadJson;
        private set => SetProperty(ref _latestPayloadJson, value);
    }

    public string DispatchStatus
    {
        get => _dispatchStatus;
        private set => SetProperty(ref _dispatchStatus, value);
    }

    public string ResultStatus
    {
        get => _resultStatus;
        private set => SetProperty(ref _resultStatus, value);
    }

    public string TransportSummary
    {
        get => _transportSummary;
        private set => SetProperty(ref _transportSummary, value);
    }

    public string LastRequestId
    {
        get => _lastRequestId;
        private set => SetProperty(ref _lastRequestId, value);
    }

    /// <summary>
    /// live validation でまず使う built-in command の案内。
    /// 送れるコマンド名を panel 自体に埋めておくことで、
    /// runbook を見なくても最小 slice を回せるようにする。
    /// </summary>
    public string BuiltInCommandGuide
    {
        get => _builtInCommandGuide;
        private set => SetProperty(ref _builtInCommandGuide, value);
    }

    public ObservableCollection<CommandHistoryItemViewModel> History { get; }

    public AsyncRelayCommand SendCommand { get; }

    public void Dispose()
    {
        _timeoutTimer.Stop();
        _commandStore.Changed -= OnCommandStoreChanged;
        _capabilityStateStore.Changed -= OnCapabilityChanged;
    }

    private void OnCommandStoreChanged(CommandStoreSnapshot _)
    {
        Refresh();
    }

    private void OnCapabilityChanged(CapabilityStateSnapshot _)
    {
        Refresh();
    }

    private void Refresh()
    {
        var snapshot = _commandStore.GetSnapshot();
        var capability = _capabilityStateStore.GetSnapshot();

        UpdateOnUiThread(() =>
        {
            DispatchCount = snapshot.DispatchCount;
            ResultCount = snapshot.ResultCount;
            PendingCount = snapshot.PendingCount;
            LatestResult = BuildLatestResult(snapshot);
            LatestPayloadJson = BuildLatestPayloadJson(snapshot);
            DispatchStatus = BuildDispatchStatus(capability, _commandSender.CanSendCommands, snapshot);
            ResultStatus = BuildResultStatus(snapshot, capability);
            TransportSummary = BuildTransportSummary(snapshot);
            LastRequestId = snapshot.LatestEntry?.RequestId ?? "No command has been dispatched yet.";
            RebuildHistory(snapshot);
            SendCommand.RaiseCanExecuteChanged();
        });
    }

    private static string BuildDispatchStatus(
        CapabilityStateSnapshot capability,
        bool canSendCommands,
        CommandStoreSnapshot snapshot)
    {
        if (canSendCommands)
        {
            return snapshot.PendingCount > 0
                ? string.Create(CultureInfo.InvariantCulture, $"Command transport is negotiated. {snapshot.PendingCount} command(s) are pending.")
                : "Command transport is negotiated. You can dispatch raw commands from this panel.";
        }

        if (capability.HandshakeState == "Negotiated" &&
            (capability.NegotiatedCapabilities & DebugStudioCapability.DebugCommand) == 0)
        {
            return "Unity sender has not advertised command dispatch support yet.";
        }

        return capability.HandshakeState == "Negotiating"
            ? "Capability negotiation is in progress."
            : "Connect to a Unity session to evaluate command dispatch support.";
    }

    private static string BuildResultStatus(CommandStoreSnapshot snapshot, CapabilityStateSnapshot capability)
    {
        if (snapshot.ResultCount > 0)
        {
            return snapshot.PendingCount > 0
                ? "Latest Unity command reply is shown below. Some commands are still waiting for replies."
                : "Latest Unity command reply is shown below.";
        }

        if (capability.HandshakeState == "Negotiated" &&
            (capability.NegotiatedCapabilities & DebugStudioCapability.CommandResult) == 0)
        {
            return "Unity sender has not advertised command result support yet.";
        }

        return capability.HandshakeState == "Negotiating"
            ? "Capability negotiation is in progress."
            : "No command replies have been received for this session.";
    }

    private static string BuildLatestResult(CommandStoreSnapshot snapshot)
    {
        if (snapshot.LatestEntry is not CommandDispatchRecord latestEntry)
        {
            return "No command results yet.";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"[{DebugStudioTextFormatter.FormatCommandState(latestEntry.State)}] request={latestEntry.RequestId} type={latestEntry.CommandType} message={latestEntry.StatusMessage}");
    }

    private static string BuildTransportSummary(CommandStoreSnapshot snapshot)
    {
        if (snapshot.PendingCount > 0)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Correlation is active. pending={snapshot.PendingCount}, completed={snapshot.CompletedCount}, results={snapshot.ResultCount}.");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Correlation is active. dispatches={snapshot.DispatchCount}, completed={snapshot.CompletedCount}, results={snapshot.ResultCount}.");
    }

    private static string BuildLatestPayloadJson(CommandStoreSnapshot snapshot)
    {
        if (snapshot.LatestEntry is not CommandDispatchRecord latestEntry ||
            string.IsNullOrWhiteSpace(latestEntry.ResultPayloadJson))
        {
            return "{}";
        }

        return latestEntry.ResultPayloadJson;
    }

    private void RebuildHistory(CommandStoreSnapshot snapshot)
    {
        History.Clear();
        foreach (var entry in snapshot.Entries)
        {
            History.Add(new CommandHistoryItemViewModel(
                state: DebugStudioTextFormatter.FormatCommandState(entry.State),
                requestId: entry.RequestId,
                commandType: entry.CommandType,
                summary: entry.StatusMessage,
                timing: DebugStudioTextFormatter.FormatCommandTiming(
                    entry.StartedAtUnixTimeMilliseconds,
                    entry.CompletedAtUnixTimeMilliseconds)));
        }
    }

    private bool CanSendCommand()
    {
        return _commandSender.CanSendCommands && !string.IsNullOrWhiteSpace(CommandType);
    }

    private async Task SendCommandAsync()
    {
        try
        {
            var requestId = await _commandSender.SendAsync(CommandType, PayloadJson ?? string.Empty).ConfigureAwait(false);
            UpdateOnUiThread(() => LastRequestId = requestId);
        }
        catch (Exception ex)
        {
            UpdateOnUiThread(() =>
            {
                DispatchStatus = ex.Message;
                TransportSummary = "Dispatch failed before Unity returned a correlated result.";
            });
        }
    }

    private void OnTimeoutTimerTick(object? sender, EventArgs e)
    {
        _commandSender.SweepTimedOutCommands(PendingTimeout);
    }

    private void UpdateOnUiThread(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.Invoke(action);
    }
}
