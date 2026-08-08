#nullable enable

using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Tests.Stores;

/// <summary>
/// sessionId をキーに属性を引き当てる契約を固定する。
/// 「現在接続中セッション」ではなく、複数 session を同時保持できることが核心。
/// </summary>
public sealed class TelemetrySessionAttributesStoreTests
{
    [Fact]
    public void TryGet_未知sessionIdはnullを返す()
    {
        var store = new TelemetrySessionAttributesStore();

        Assert.Null(store.TryGet("unknown-session"));
        Assert.Null(store.TryGet(null));
        Assert.Null(store.TryGet(string.Empty));
    }

    [Fact]
    public void ApplyWelcome_2つのsessionを同時に保持しそれぞれ正しい属性が引ける()
    {
        var store = new TelemetrySessionAttributesStore();
        store.ApplyWelcome(CreateWelcome(
            "session-a",
            buildVersion: "1.0.0",
            platform: "WindowsPlayer",
            deviceModel: "PC-A",
            osVersion: "Windows 11",
            engineVersion: "6000.0.0f1"));
        store.ApplyWelcome(CreateWelcome(
            "session-b",
            buildVersion: "2.0.0",
            platform: "Android",
            deviceModel: "Pixel 8",
            osVersion: "Android OS 14",
            engineVersion: "6000.1.0f1"));

        var a = store.TryGet("session-a");
        var b = store.TryGet("session-b");

        Assert.NotNull(a);
        Assert.Equal("1.0.0", a!.BuildVersion);
        Assert.Equal("WindowsPlayer", a.Platform);
        Assert.Equal("PC-A", a.DeviceModel);

        Assert.NotNull(b);
        Assert.Equal("2.0.0", b!.BuildVersion);
        Assert.Equal("Android", b.Platform);
        Assert.Equal("Pixel 8", b.DeviceModel);
        Assert.Equal("Android OS 14", b.OsVersion);
        Assert.Equal("6000.1.0f1", b.EngineVersion);
    }

    [Fact]
    public void ApplyWelcome_空SessionIdは登録されない()
    {
        var store = new TelemetrySessionAttributesStore();
        store.ApplyWelcome(CreateWelcome(
            string.Empty,
            buildVersion: "1.0.0",
            platform: "WindowsPlayer",
            deviceModel: "PC",
            osVersion: "Windows",
            engineVersion: "6000.0.0f1"));

        Assert.Null(store.TryGet(string.Empty));
        Assert.Null(store.TryGet("1.0.0"));
    }

    [Fact]
    public void ApplyWelcome_上限を超えたら件数が上限以下に保たれる()
    {
        var store = new TelemetrySessionAttributesStore();
        for (var index = 0; index < TelemetrySessionAttributesStore.MaxSessions + 5; index++)
        {
            store.ApplyWelcome(CreateWelcome(
                $"session-{index}",
                buildVersion: $"v{index}",
                platform: "WindowsPlayer",
                deviceModel: "PC",
                osVersion: "Windows",
                engineVersion: "6000.0.0f1"));
        }

        var retainedCount = 0;
        for (var index = 0; index < TelemetrySessionAttributesStore.MaxSessions + 5; index++)
        {
            if (store.TryGet($"session-{index}") != null)
            {
                retainedCount++;
            }
        }

        Assert.Equal(TelemetrySessionAttributesStore.MaxSessions, retainedCount);
        Assert.Null(store.TryGet("session-0"));
        Assert.NotNull(store.TryGet($"session-{TelemetrySessionAttributesStore.MaxSessions + 4}"));
    }

    [Fact]
    public void ApplyWelcome_旧Welcome相当の空属性でも例外にならず5値は空になる()
    {
        var store = new TelemetrySessionAttributesStore();
        store.ApplyWelcome(new CapabilityHandshakeWelcomeEnvelopeV1
        {
            SessionId = "legacy-session",
            ServerName = "Legacy Unity",
            TimestampUnixTimeMilliseconds = 1234567890123L,
            StatusMessage = "ok",
        });

        var attributes = store.TryGet("legacy-session");
        Assert.NotNull(attributes);
        Assert.Equal(string.Empty, attributes!.BuildVersion);
        Assert.Equal(string.Empty, attributes.Platform);
        Assert.Equal(string.Empty, attributes.DeviceModel);
        Assert.Equal(string.Empty, attributes.OsVersion);
        Assert.Equal(string.Empty, attributes.EngineVersion);
    }

    private static CapabilityHandshakeWelcomeEnvelopeV1 CreateWelcome(
        string sessionId,
        string buildVersion,
        string platform,
        string deviceModel,
        string osVersion,
        string engineVersion)
    {
        return new CapabilityHandshakeWelcomeEnvelopeV1
        {
            SessionId = sessionId,
            BuildVersion = buildVersion,
            Platform = platform,
            DeviceModel = deviceModel,
            OsVersion = osVersion,
            EngineVersion = engineVersion,
            TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }
}
