#nullable enable

using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Tests.Stores;

/// <summary>
/// CapabilityStateStore の状態遷移とSupports()セマンティクスを検証。
///
/// 主な検証点:
/// - 接続時のリセット動作
/// - hello/welcome/disconnect の状態遷移
/// - スキーマバージョン不一致時のフォールト動作
/// - Supports() ビット演算セマンティクス
/// - snapshot の順序保証とイベント発火
/// </summary>
public sealed class CapabilityStateStoreTests
{
    [Fact]
    public void 初期化時_Idle状態から開始()
    {
        var localCaps = DebugStudioCapability.CapabilityNegotiation | DebugStudioCapability.LogStream;
        var store = new CapabilityStateStore(localCaps);

        var snapshot = store.GetSnapshot();

        Assert.Equal(localCaps, snapshot.LocalSupportedCapabilities);
        Assert.Equal(DebugStudioCapability.None, snapshot.RemoteSupportedCapabilities);
        Assert.Equal(DebugStudioCapability.None, snapshot.NegotiatedCapabilities);
        Assert.Equal("Idle", snapshot.HandshakeState);
        Assert.Equal("Unknown", snapshot.RemoteName);
        Assert.Null(snapshot.SessionId);
    }

    [Fact]
    public void ResetForConnect_Negotiating状態へ遷移()
    {
        var store = CreateStore();
        var uri = new Uri("ws://localhost:8080");

        store.ResetForConnect(uri);

        var snapshot = store.GetSnapshot();
        Assert.Equal("Negotiating", snapshot.HandshakeState);
        Assert.Equal(DebugStudioCapability.None, snapshot.RemoteSupportedCapabilities);
        Assert.Equal(DebugStudioCapability.None, snapshot.NegotiatedCapabilities);
        Assert.Contains("localhost:8080", snapshot.Detail);
        Assert.Equal("Unknown", snapshot.RemoteName);
        Assert.Null(snapshot.SessionId);
    }

    [Fact]
    public void MarkHelloSent_Negotiating状態を維持しDetailが更新される()
    {
        var store = CreateStore();
        store.ResetForConnect(new Uri("ws://test"));

        store.MarkHelloSent();

        var snapshot = store.GetSnapshot();
        Assert.Equal("Negotiating", snapshot.HandshakeState);
        Assert.Contains("hello sent", snapshot.Detail);
    }

    [Fact]
    public void ApplyWelcome_正常時_Negotiated状態へ遷移()
    {
        var store = CreateStore();
        store.ResetForConnect(new Uri("ws://test"));

        var welcome = CreateWelcome(
            sessionId: "session123",
            serverName: "Unity Editor",
            selectedSchemaVersion: 1,
            serverCapabilities: DebugStudioCapability.CapabilityNegotiation | DebugStudioCapability.LogStream,
            negotiatedCapabilities: DebugStudioCapability.LogStream);

        store.ApplyWelcome(welcome);

        var snapshot = store.GetSnapshot();
        Assert.Equal("Negotiated", snapshot.HandshakeState);
        Assert.Equal(DebugStudioCapability.LogStream, snapshot.NegotiatedCapabilities);
        Assert.Equal(DebugStudioCapability.CapabilityNegotiation | DebugStudioCapability.LogStream, snapshot.RemoteSupportedCapabilities);
        Assert.Equal("Unity Editor", snapshot.RemoteName);
        Assert.Equal("session123", snapshot.SessionId);
    }

    [Fact]
    public void ApplyWelcome_スキーマバージョン範囲外_NegotiationFaulted状態へ()
    {
        var store = CreateStore();
        store.ResetForConnect(new Uri("ws://test"));

        var welcome = CreateWelcome(
            sessionId: "session123",
            serverName: "Unity",
            selectedSchemaVersion: 99, // 範囲外
            serverCapabilities: DebugStudioCapability.LogStream,
            negotiatedCapabilities: DebugStudioCapability.LogStream);

        store.ApplyWelcome(welcome);

        var snapshot = store.GetSnapshot();
        Assert.Equal("NegotiationFaulted", snapshot.HandshakeState);
        Assert.Equal(DebugStudioCapability.None, snapshot.NegotiatedCapabilities);
        Assert.Contains("Schema negotiation failed", snapshot.Detail);
        Assert.Contains("99", snapshot.Detail);
    }

    [Fact]
    public void ApplyWelcome_スキーマバージョン0_NegotiationFaulted状態へ()
    {
        var store = CreateStore();
        var welcome = CreateWelcome(
            sessionId: "s1",
            serverName: "OldUnity",
            selectedSchemaVersion: 0, // サポート範囲外
            serverCapabilities: DebugStudioCapability.LogStream,
            negotiatedCapabilities: DebugStudioCapability.LogStream);

        store.ApplyWelcome(welcome);

        var snapshot = store.GetSnapshot();
        Assert.Equal("NegotiationFaulted", snapshot.HandshakeState);
        Assert.Equal(DebugStudioCapability.None, snapshot.NegotiatedCapabilities);
    }

    [Fact]
    public void MarkDisconnected_Disconnected状態へ遷移しNegotiatedCapabilitiesがクリアされる()
    {
        var store = CreateStore();
        store.ResetForConnect(new Uri("ws://test"));
        var welcome = CreateWelcome(
            sessionId: "s1",
            serverName: "Unity",
            selectedSchemaVersion: 1,
            serverCapabilities: DebugStudioCapability.LogStream,
            negotiatedCapabilities: DebugStudioCapability.LogStream);
        store.ApplyWelcome(welcome);

        store.MarkDisconnected("Connection lost");

        var snapshot = store.GetSnapshot();
        Assert.Equal("Disconnected", snapshot.HandshakeState);
        Assert.Equal(DebugStudioCapability.None, snapshot.NegotiatedCapabilities);
        Assert.Contains("Connection lost", snapshot.Detail);
    }

    [Fact]
    public void MarkHandshakeFaulted_NegotiationFaulted状態へ遷移()
    {
        var store = CreateStore();
        store.ResetForConnect(new Uri("ws://test"));

        store.MarkHandshakeFaulted("Handshake timeout");

        var snapshot = store.GetSnapshot();
        Assert.Equal("NegotiationFaulted", snapshot.HandshakeState);
        Assert.Contains("Handshake timeout", snapshot.Detail);
    }

    [Fact]
    public void Supports_NegotiatedCapabilitiesに含まれる場合はtrue()
    {
        var store = CreateStore();
        var welcome = CreateWelcome(
            sessionId: "s1",
            serverName: "Unity",
            selectedSchemaVersion: 1,
            serverCapabilities: DebugStudioCapability.LogStream | DebugStudioCapability.TelemetryStream,
            negotiatedCapabilities: DebugStudioCapability.LogStream | DebugStudioCapability.TelemetryStream);
        store.ApplyWelcome(welcome);

        Assert.True(store.Supports(DebugStudioCapability.LogStream));
        Assert.True(store.Supports(DebugStudioCapability.TelemetryStream));
        Assert.False(store.Supports(DebugStudioCapability.DebugCommand));
    }

    [Fact]
    public void Supports_複数ビット指定で全てがNegotiatedに含まれる場合のみtrue()
    {
        var store = CreateStore();
        var welcome = CreateWelcome(
            sessionId: "s1",
            serverName: "Unity",
            selectedSchemaVersion: 1,
            serverCapabilities: DebugStudioCapability.LogStream | DebugStudioCapability.TelemetryStream,
            negotiatedCapabilities: DebugStudioCapability.LogStream | DebugStudioCapability.TelemetryStream);
        store.ApplyWelcome(welcome);

        var combined = DebugStudioCapability.LogStream | DebugStudioCapability.TelemetryStream;
        Assert.True(store.Supports(combined));

        var mixedWithUnsupported = DebugStudioCapability.LogStream | DebugStudioCapability.DebugCommand;
        Assert.False(store.Supports(mixedWithUnsupported));
    }

    [Fact]
    public void Supports_初期状態ではNoneのみtrue()
    {
        var store = CreateStore();

        Assert.True(store.Supports(DebugStudioCapability.None));
        Assert.False(store.Supports(DebugStudioCapability.LogStream));
        Assert.False(store.Supports(DebugStudioCapability.CapabilityNegotiation));
    }

    [Fact]
    public void Supports_Disconnected後はNoneのみtrue()
    {
        var store = CreateStore();
        var welcome = CreateWelcome(
            sessionId: "s1",
            serverName: "Unity",
            selectedSchemaVersion: 1,
            serverCapabilities: DebugStudioCapability.LogStream,
            negotiatedCapabilities: DebugStudioCapability.LogStream);
        store.ApplyWelcome(welcome);

        store.MarkDisconnected("Test disconnect");

        Assert.True(store.Supports(DebugStudioCapability.None));
        Assert.False(store.Supports(DebugStudioCapability.LogStream));
    }

    [Fact]
    public void Changed_イベントが各操作で発火される()
    {
        var store = CreateStore();
        var snapshots = new List<CapabilityStateSnapshot>();
        store.Changed += snapshot => snapshots.Add(snapshot);

        store.ResetForConnect(new Uri("ws://test"));
        store.MarkHelloSent();
        var welcome = CreateWelcome("s1", "Unity", 1, DebugStudioCapability.LogStream, DebugStudioCapability.LogStream);
        store.ApplyWelcome(welcome);
        store.MarkDisconnected("Done");

        Assert.Equal(4, snapshots.Count);
        Assert.Equal("Negotiating", snapshots[0].HandshakeState);
        Assert.Equal("Negotiating", snapshots[1].HandshakeState);
        Assert.Equal("Negotiated", snapshots[2].HandshakeState);
        Assert.Equal("Disconnected", snapshots[3].HandshakeState);
    }

    [Fact]
    public void 状態遷移_接続からネゴシエーション完了まで正常フロー()
    {
        var store = CreateStore();

        // 1. Idle
        var s1 = store.GetSnapshot();
        Assert.Equal("Idle", s1.HandshakeState);

        // 2. 接続開始
        store.ResetForConnect(new Uri("ws://unity:8080"));
        var s2 = store.GetSnapshot();
        Assert.Equal("Negotiating", s2.HandshakeState);
        Assert.Equal(DebugStudioCapability.None, s2.NegotiatedCapabilities);

        // 3. hello 送信
        store.MarkHelloSent();
        var s3 = store.GetSnapshot();
        Assert.Equal("Negotiating", s3.HandshakeState);

        // 4. welcome 受信
        var welcome = CreateWelcome(
            sessionId: "abc123",
            serverName: "Unity Player",
            selectedSchemaVersion: 1,
            serverCapabilities: DebugStudioCapability.LogStream | DebugStudioCapability.TelemetryStream,
            negotiatedCapabilities: DebugStudioCapability.LogStream);
        store.ApplyWelcome(welcome);
        var s4 = store.GetSnapshot();
        Assert.Equal("Negotiated", s4.HandshakeState);
        Assert.Equal(DebugStudioCapability.LogStream, s4.NegotiatedCapabilities);
        Assert.Equal("Unity Player", s4.RemoteName);
        Assert.Equal("abc123", s4.SessionId);

        // 5. 切断
        store.MarkDisconnected("User requested disconnect");
        var s5 = store.GetSnapshot();
        Assert.Equal("Disconnected", s5.HandshakeState);
        Assert.Equal(DebugStudioCapability.None, s5.NegotiatedCapabilities);
    }

    [Fact]
    public void 状態遷移_ネゴシエーション失敗フロー()
    {
        var store = CreateStore();

        store.ResetForConnect(new Uri("ws://test"));
        store.MarkHelloSent();

        // スキーマバージョン不一致でフォールト
        var badWelcome = CreateWelcome("s1", "Unity", 999, DebugStudioCapability.LogStream, DebugStudioCapability.LogStream);
        store.ApplyWelcome(badWelcome);

        var snapshot = store.GetSnapshot();
        Assert.Equal("NegotiationFaulted", snapshot.HandshakeState);
        Assert.Equal(DebugStudioCapability.None, snapshot.NegotiatedCapabilities);
    }

    [Fact]
    public void ApplyWelcome_nullチェック()
    {
        var store = CreateStore();
        Assert.Throws<ArgumentNullException>(() => store.ApplyWelcome(null!));
    }

    [Fact]
    public void ApplyWelcome_ServerName空の場合デフォルト名が設定される()
    {
        var store = CreateStore();
        var welcome = CreateWelcome("s1", "", 1, DebugStudioCapability.LogStream, DebugStudioCapability.LogStream);

        store.ApplyWelcome(welcome);

        var snapshot = store.GetSnapshot();
        Assert.Equal("Unity", snapshot.RemoteName);
    }

    [Fact]
    public void ApplyWelcome_SessionId空の場合nullが設定される()
    {
        var store = CreateStore();
        var welcome = CreateWelcome("", "Unity", 1, DebugStudioCapability.LogStream, DebugStudioCapability.LogStream);

        store.ApplyWelcome(welcome);

        var snapshot = store.GetSnapshot();
        Assert.Null(snapshot.SessionId);
    }

    [Fact]
    public async Task 複数スレッドから同時にアクセス_データ破損しない()
    {
        var store = CreateStore();
        var tasks = new List<Task>();

        for (var i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                store.ResetForConnect(new Uri("ws://test"));
                store.MarkHelloSent();
                var welcome = CreateWelcome("s1", "Unity", 1, DebugStudioCapability.LogStream, DebugStudioCapability.LogStream);
                store.ApplyWelcome(welcome);
                var supports = store.Supports(DebugStudioCapability.LogStream);
                var snapshot = store.GetSnapshot();
            }));
        }

        await Task.WhenAll(tasks.ToArray());

        var finalSnapshot = store.GetSnapshot();
        Assert.Equal("Negotiated", finalSnapshot.HandshakeState);
    }

    private static CapabilityStateStore CreateStore()
    {
        var localCaps = DebugStudioCapability.CapabilityNegotiation
                        | DebugStudioCapability.LogStream
                        | DebugStudioCapability.TelemetryStream
                        | DebugStudioCapability.DebugCommand
                        | DebugStudioCapability.HierarchySnapshot
                        | DebugStudioCapability.InspectorQuery;
        return new CapabilityStateStore(localCaps);
    }

    private static CapabilityHandshakeWelcomeEnvelopeV1 CreateWelcome(
        string sessionId,
        string serverName,
        int selectedSchemaVersion,
        DebugStudioCapability serverCapabilities,
        DebugStudioCapability negotiatedCapabilities)
    {
        return new CapabilityHandshakeWelcomeEnvelopeV1
        {
            SchemaVersion = 1,
            SessionId = sessionId,
            ServerName = serverName,
            SelectedSchemaVersion = selectedSchemaVersion,
            ServerCapabilities = serverCapabilities,
            NegotiatedCapabilities = negotiatedCapabilities,
            SupportedMessageTypes = Array.Empty<int>(),
            TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            StatusMessage = string.Empty,
        };
    }
}
