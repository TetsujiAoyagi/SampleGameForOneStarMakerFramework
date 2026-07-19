#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Stores;
using DebugStudio.Export.Elastic;
using DebugStudio.Export.Models;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// retained telemetry を L1 Verify 用に Elastic へ明示投入する app service。
///
/// <para>
/// 対象は <see cref="TelemetryStore.GetRetainedSnapshot"/> の Telemetry のみ。
/// ServiceStatus は含めず、0 件なら `_bulk` を呼ばない。
/// 失敗しても L0 persistence や受信処理には干渉しない。
/// </para>
/// </summary>
public sealed class ElasticTelemetryPushService
{
    /// <summary>
    /// TelemetryStore retained 容量の上限。current-session 近似の説明に使う。
    /// </summary>
    public const int RetainedTelemetryCapacityHint = 256;

    private readonly TelemetryStore _telemetryStore;
    private readonly Func<ElasticTelemetrySettings, ElasticTelemetryIngestClient> _clientFactory;
    private readonly IElasticEnvironmentReader _environmentReader;

    public ElasticTelemetryPushService(
        TelemetryStore telemetryStore,
        IElasticEnvironmentReader environmentReader,
        HttpClient httpClient)
        : this(
            telemetryStore,
            environmentReader,
            settings => new ElasticTelemetryIngestClient(httpClient, settings.ElasticUrl, settings.ApiKeyBase64Value))
    {
    }

    public ElasticTelemetryPushService(
        TelemetryStore telemetryStore,
        IElasticEnvironmentReader environmentReader,
        Func<ElasticTelemetrySettings, ElasticTelemetryIngestClient> clientFactory)
    {
        _telemetryStore = telemetryStore ?? throw new ArgumentNullException(nameof(telemetryStore));
        _environmentReader = environmentReader ?? throw new ArgumentNullException(nameof(environmentReader));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    public bool TryCreateSettings(out ElasticTelemetrySettings? settings, out string errorMessage)
    {
        return ElasticTelemetrySettings.TryCreate(_environmentReader, out settings, out errorMessage);
    }

    /// <summary>
    /// 現在 retained されている telemetry 件数と概算 payload サイズを返す。
    /// </summary>
    public ElasticTelemetryPushPreview BuildPreview()
    {
        var records = BuildTelemetryExportRecords();
        var payloadBytes = records.Count == 0 ? 0 : ElasticBulkTelemetryNdjsonBuilder.BuildBulkPayload(records).Length;
        return new ElasticTelemetryPushPreview(records.Count, payloadBytes, RetainedTelemetryCapacityHint);
    }

    public async Task<ElasticPreflightResult> PreflightAsync(CancellationToken cancellationToken = default)
    {
        if (!TryCreateSettings(out var settings, out var errorMessage) || settings == null)
        {
            return ElasticPreflightResult.Failed(errorMessage, isRetryable: false);
        }

        var client = _clientFactory(settings);
        return await client.PreflightAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// bootstrap → `_bulk` を順に実行する。telemetry 0 件なら `_bulk` は呼ばない。
    /// </summary>
    public async Task<ElasticTelemetryPushResult> PushRetainedTelemetryAsync(CancellationToken cancellationToken = default)
    {
        // endpoint / Kibana URL / API key は 1 回だけ snapshot 化する。
        // 同一 push 中に環境変数が変わっても、client と UI 結果が別設定を指すことを防ぐ。
        if (!TryCreateSettings(out var settings, out var errorMessage) ||
            settings == null)
        {
            return ElasticTelemetryPushResult.SettingsInvalid(errorMessage);
        }

        var client = _clientFactory(settings);
        var records = BuildTelemetryExportRecords();
        if (records.Count == 0)
        {
            return ElasticTelemetryPushResult.NoRecords();
        }

        var bootstrapResult = await client.BootstrapAsync(cancellationToken).ConfigureAwait(false);
        if (!bootstrapResult.Success)
        {
            return ElasticTelemetryPushResult.FromBootstrapFailure(bootstrapResult);
        }

        var payload = ElasticBulkTelemetryNdjsonBuilder.BuildBulkPayload(records);
        var bulkResult = await client.PushBulkAsync(payload, cancellationToken).ConfigureAwait(false);
        if (!bulkResult.Success)
        {
            return ElasticTelemetryPushResult.FromBulkFailure(bulkResult, settings.KibanaUrl);
        }

        // `_bulk` が HTTP 200 でも action 数が足りなければ、全 telemetry を
        // 確認できていない。L1 Verify は全件受理を成功条件にする。
        if (bulkResult.AcceptedCount != records.Count)
        {
            return ElasticTelemetryPushResult.FromBulkFailure(
                ElasticBulkPushResult.Failed(
                    $"Elastic bulk accepted {bulkResult.AcceptedCount} of {records.Count} telemetry records; full delivery cannot be verified.",
                    isRetryable: false,
                    acceptedCount: bulkResult.AcceptedCount,
                    failedCount: records.Count - bulkResult.AcceptedCount),
                settings.KibanaUrl);
        }

        return ElasticTelemetryPushResult.Succeeded(
            records.Count,
            bulkResult.AcceptedCount,
            settings.KibanaUrl,
            bulkResult.Message);
    }

    private IReadOnlyList<TelemetryExportRecord> BuildTelemetryExportRecords()
    {
        var retainedSnapshot = _telemetryStore.GetRetainedSnapshot();
        var telemetry = retainedSnapshot.Telemetry;
        if (telemetry.Count == 0)
        {
            return Array.Empty<TelemetryExportRecord>();
        }

        var records = new List<TelemetryExportRecord>(telemetry.Count);
        for (var index = 0; index < telemetry.Count; index++)
        {
            records.Add(TelemetryRecordExportMapper.ToExportRecord(telemetry[index]));
        }

        records.Sort(static (left, right) => left.TimestampUnixTimeMilliseconds.CompareTo(right.TimestampUnixTimeMilliseconds));
        return records;
    }
}

public sealed record ElasticTelemetryPushPreview(int RecordCount, int ApproximatePayloadBytes, int RetainedCapacityHint)
{
    public string DescribeForUi()
    {
        var kilobytes = ApproximatePayloadBytes / 1024d;
        return
            $"Retained telemetry (current-session, max {RetainedCapacityHint}): {RecordCount} record(s), ~{kilobytes:0.#} KB bulk payload.";
    }
}

public sealed record ElasticTelemetryPushResult(
    bool Success,
    string Message,
    int RecordCount,
    int AcceptedCount,
    Uri? KibanaUrl,
    bool IsRetryable)
{
    public static ElasticTelemetryPushResult SettingsInvalid(string message) =>
        new(false, message, 0, 0, null, false);

    public static ElasticTelemetryPushResult NoRecords() =>
        new(false, "Retained telemetry is empty; bulk push was skipped.", 0, 0, null, false);

    public static ElasticTelemetryPushResult FromBootstrapFailure(ElasticBootstrapResult bootstrapResult) =>
        new(false, bootstrapResult.Message, 0, 0, null, bootstrapResult.IsRetryable);

    public static ElasticTelemetryPushResult FromBulkFailure(ElasticBulkPushResult bulkResult, Uri kibanaUrl) =>
        new(
            false,
            bulkResult.Message,
            0,
            bulkResult.AcceptedCount,
            kibanaUrl,
            bulkResult.IsRetryable);

    public static ElasticTelemetryPushResult Succeeded(
        int recordCount,
        int acceptedCount,
        Uri kibanaUrl,
        string message) =>
        new(
            true,
            $"{message} records={recordCount}. Open Kibana at {kibanaUrl}.",
            recordCount,
            acceptedCount,
            kibanaUrl,
            false);
}
