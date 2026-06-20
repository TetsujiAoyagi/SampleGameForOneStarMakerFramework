using System;
using System.Text.Json;
using DebugStudio.App.Core.Models;

var record = new TelemetryExportRecord
{
    TimestampUtc = "2026-04-29T01:00:02.0000000Z",
    TimestampUnixTimeMilliseconds = 1777636802000,
    Stream = "telemetry",
    Name = "test-span"
};

var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false
};

var json = JsonSerializer.Serialize(record, options);
Console.WriteLine(json);
