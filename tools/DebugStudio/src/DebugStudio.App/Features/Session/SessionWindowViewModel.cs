#nullable enable


using DebugStudio.App.Core.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using DebugStudio.App.Core.Formatting;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Client;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Features.Session;

/// <summary>
/// Session window/panel 専用 ViewModel。
/// 接続制御と capability/session サマリーを shell から切り離して保持する。
/// </summary>
public sealed class SessionWindowViewModel : ObservableObject, IAsyncDisposable
{
    private const int MaxActivityItems = 200;
    private const string CapabilityIdleDetail = "Start listening to negotiate capabilities when Unity connects.";

    private readonly Dispatcher _dispatcher;
    private readonly SessionService _sessionService;
    private readonly CapabilityStateStore _capabilityStateStore;
    private string _serverUri = DebugSocketClientOptions.DefaultServerUri.ToString();
    private string _connectionState = "Stopped";
    private string _connectionDetail = "Ready to listen for a Unity connection.";
    private string _capabilityStatus = "Idle";
    private string _capabilityDetail = CapabilityIdleDetail;
    private string _negotiatedCapabilitiesText = "Negotiated: None";
    private string _sessionIdentityText = "Unity: Unknown / Session: n/a";
    private string _localCapabilitiesText = "Local: None";
    private string _remoteCapabilitiesText = "Remote: None";

    public SessionWindowViewModel(
        Dispatcher dispatcher,
        SessionService sessionService,
        CapabilityStateStore capabilityStateStore)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _capabilityStateStore = capabilityStateStore ?? throw new ArgumentNullException(nameof(capabilityStateStore));

        RecentActivity = new ObservableCollection<string>();
        ConnectCommand = new AsyncRelayCommand(ConnectAsync, CanConnect);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, CanDisconnect);

        _sessionService.ConnectionStateChanged += OnConnectionStateChanged;
        _sessionService.LogReceived += OnLogReceived;
        _sessionService.TelemetryReceived += OnTelemetryReceived;
        _sessionService.ServiceStatusReceived += OnServiceStatusReceived;
        _sessionService.CommandResultReceived += OnCommandResultReceived;
        _sessionService.CapabilityWelcomeReceived += OnCapabilityWelcomeReceived;
        _sessionService.HierarchySnapshotReceived += OnHierarchySnapshotReceived;
        _sessionService.HierarchyDeltaReceived += OnHierarchyDeltaReceived;
        _sessionService.InspectorDetailReceived += OnInspectorDetailReceived;
        _capabilityStateStore.Changed += OnCapabilityStateChanged;

        OnCapabilityStateChanged(_capabilityStateStore.GetSnapshot());
    }

    public string ServerUri
    {
        get => _serverUri;
        set => SetProperty(ref _serverUri, value);
    }

    public string ConnectionState
    {
        get => _connectionState;
        private set => SetProperty(ref _connectionState, value);
    }

    public string ConnectionDetail
    {
        get => _connectionDetail;
        private set => SetProperty(ref _connectionDetail, value);
    }

    public string CapabilityStatus
    {
        get => _capabilityStatus;
        private set => SetProperty(ref _capabilityStatus, value);
    }

    public string CapabilityDetail
    {
        get => _capabilityDetail;
        private set => SetProperty(ref _capabilityDetail, value);
    }

    public string NegotiatedCapabilitiesText
    {
        get => _negotiatedCapabilitiesText;
        private set => SetProperty(ref _negotiatedCapabilitiesText, value);
    }

    public string SessionIdentityText
    {
        get => _sessionIdentityText;
        private set => SetProperty(ref _sessionIdentityText, value);
    }

    public string LocalCapabilitiesText
    {
        get => _localCapabilitiesText;
        private set => SetProperty(ref _localCapabilitiesText, value);
    }

    public string RemoteCapabilitiesText
    {
        get => _remoteCapabilitiesText;
        private set => SetProperty(ref _remoteCapabilitiesText, value);
    }

    public ObservableCollection<string> RecentActivity { get; }

    public AsyncRelayCommand ConnectCommand { get; }

    public AsyncRelayCommand DisconnectCommand { get; }

    public async ValueTask DisposeAsync()
    {
        _sessionService.ConnectionStateChanged -= OnConnectionStateChanged;
        _sessionService.LogReceived -= OnLogReceived;
        _sessionService.TelemetryReceived -= OnTelemetryReceived;
        _sessionService.ServiceStatusReceived -= OnServiceStatusReceived;
        _sessionService.CommandResultReceived -= OnCommandResultReceived;
        _sessionService.CapabilityWelcomeReceived -= OnCapabilityWelcomeReceived;
        _sessionService.HierarchySnapshotReceived -= OnHierarchySnapshotReceived;
        _sessionService.HierarchyDeltaReceived -= OnHierarchyDeltaReceived;
        _sessionService.InspectorDetailReceived -= OnInspectorDetailReceived;
        _capabilityStateStore.Changed -= OnCapabilityStateChanged;
        await _sessionService.DisposeAsync().ConfigureAwait(false);
    }

    private bool CanConnect()
    {
        return _sessionService.State is DebugSocketConnectionState.Disconnected or DebugSocketConnectionState.Faulted;
    }

    private bool CanDisconnect()
    {
        return _sessionService.State is DebugSocketConnectionState.Connected or
            DebugSocketConnectionState.Connecting or
            DebugSocketConnectionState.Disconnecting;
    }

    private async Task ConnectAsync()
    {
        if (!Uri.TryCreate(ServerUri, UriKind.Absolute, out var serverUri) ||
            (serverUri.Scheme != Uri.UriSchemeWs && serverUri.Scheme != Uri.UriSchemeWss))
        {
            UpdateConnectionDetail("Enter a valid ws:// or wss:// listen URI.");
            AppendActivity("[local] invalid listen URI");
            return;
        }

        try
        {
            await _sessionService.ConnectAsync(new DebugSocketClientOptions
            {
                ServerUri = serverUri,
            });
        }
        catch (Exception ex)
        {
            var detail = serverUri == null
                ? ex.Message
                : DebugSocketConnectionErrorFormatter.Format(serverUri, ex);
            UpdateConnectionDetail(detail);
            AppendActivity($"[connect-error] {detail}");
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            await _sessionService.DisconnectAsync();
        }
        catch (Exception ex)
        {
            UpdateConnectionDetail(ex.Message);
            AppendActivity($"[disconnect-error] {ex.Message}");
        }
    }

    private void OnConnectionStateChanged(DebugSocketConnectionSnapshot snapshot)
    {
        PostToUi(() =>
        {
            var displayState = BuildConnectionState(snapshot);
            var displayDetail = BuildConnectionDetail(snapshot);
            ConnectionState = displayState;
            ConnectionDetail = displayDetail;
            AppendActivity($"[{snapshot.Timestamp:HH:mm:ss}] [listener] {displayDetail}");
            ConnectCommand.RaiseCanExecuteChanged();
            DisconnectCommand.RaiseCanExecuteChanged();
        });
    }

    private void OnLogReceived(LogRecord log)
    {
        PostToUi(() => AppendActivity(log.Summary));
    }

    private void OnTelemetryReceived(DebugTelemetryEnvelopeV1 telemetry)
    {
        PostToUi(() => AppendActivity(DebugStudioTextFormatter.FormatTelemetry(telemetry)));
    }

    private void OnServiceStatusReceived(DebugSocketServiceStatusEnvelopeV1 status)
    {
        PostToUi(() => AppendActivity(DebugStudioTextFormatter.FormatServiceStatus(status)));
    }

    private void OnCommandResultReceived(DebugCommandResultEnvelopeV1 result)
    {
        PostToUi(() => AppendActivity(DebugStudioTextFormatter.FormatCommandResult(result)));
    }

    private void OnCapabilityWelcomeReceived(CapabilityHandshakeWelcomeEnvelopeV1 welcome)
    {
        PostToUi(() => AppendActivity(DebugStudioTextFormatter.FormatCapabilityWelcome(welcome)));
    }

    private void OnHierarchySnapshotReceived(HierarchySnapshotEnvelopeV1 snapshot)
    {
        PostToUi(() => AppendActivity(DebugStudioTextFormatter.FormatHierarchySnapshot(snapshot)));
    }

    private void OnHierarchyDeltaReceived(HierarchyDeltaEnvelopeV1 delta)
    {
        PostToUi(() => AppendActivity(DebugStudioTextFormatter.FormatHierarchyDelta(delta)));
    }

    private void OnInspectorDetailReceived(InspectorDetailEnvelopeV1 detail)
    {
        PostToUi(() => AppendActivity(DebugStudioTextFormatter.FormatInspectorDetail(detail)));
    }

    private void OnCapabilityStateChanged(CapabilityStateSnapshot snapshot)
    {
        PostToUi(() =>
        {
            CapabilityStatus = snapshot.HandshakeState;
            CapabilityDetail = BuildCapabilityDetail(snapshot);
            NegotiatedCapabilitiesText =
                $"Negotiated: {DebugStudioTextFormatter.FormatCapabilities(snapshot.NegotiatedCapabilities)}";
            SessionIdentityText =
                $"Unity: {snapshot.RemoteName} / Session: {snapshot.SessionId ?? "n/a"}";
            LocalCapabilitiesText =
                $"Local: {DebugStudioTextFormatter.FormatCapabilities(snapshot.LocalSupportedCapabilities)}";
            RemoteCapabilitiesText =
                $"Remote: {DebugStudioTextFormatter.FormatCapabilities(snapshot.RemoteSupportedCapabilities)}";
        });
    }

    private void UpdateConnectionDetail(string detail)
    {
        PostToUi(() => ConnectionDetail = detail);
    }

    private static string BuildConnectionState(DebugSocketConnectionSnapshot snapshot)
    {
        // transport の内部 state 名は client/server 共通の抽象名のまま維持しつつ、
        // WPF 上では「Unity からの着信待ち」であることが伝わる表現へ寄せる。
        return snapshot.State switch
        {
            DebugSocketConnectionState.Disconnected => "Stopped",
            DebugSocketConnectionState.Connecting => "Listening",
            DebugSocketConnectionState.Connected => "Attached",
            DebugSocketConnectionState.Disconnecting => "Stopping",
            DebugSocketConnectionState.Faulted => "Faulted",
            _ => snapshot.State.ToString(),
        };
    }

    private static string BuildConnectionDetail(DebugSocketConnectionSnapshot snapshot)
    {
        return snapshot.State switch
        {
            DebugSocketConnectionState.Disconnected when string.IsNullOrWhiteSpace(snapshot.Detail) ||
                                                        string.Equals(snapshot.Detail, "Ready.", StringComparison.Ordinal) =>
                "Listening is stopped.",
            DebugSocketConnectionState.Connecting when snapshot.ServerUri is not null =>
                $"Listening for an inbound Unity WebSocket on {snapshot.ServerUri}.",
            DebugSocketConnectionState.Connected when snapshot.ServerUri is not null =>
                $"Unity is connected on {snapshot.ServerUri}.",
            DebugSocketConnectionState.Disconnecting => "Stopping the Unity listener...",
            _ => snapshot.Detail,
        };
    }

    private static string BuildCapabilityDetail(CapabilityStateSnapshot snapshot)
    {
        return snapshot.HandshakeState switch
        {
            "Idle" => CapabilityIdleDetail,
            "Negotiating" when snapshot.SessionId is null =>
                "Negotiating capabilities with the connected Unity session.",
            _ => snapshot.Detail,
        };
    }

    private void AppendActivity(string line)
    {
        RecentActivity.Insert(0, line);

        while (RecentActivity.Count > MaxActivityItems)
        {
            RecentActivity.RemoveAt(RecentActivity.Count - 1);
        }
    }

    private void PostToUi(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.Invoke(action);
    }
}
