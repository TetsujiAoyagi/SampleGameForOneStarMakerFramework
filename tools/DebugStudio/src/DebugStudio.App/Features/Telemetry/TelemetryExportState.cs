#nullable enable

using System;
using System.Collections.Generic;
using DebugStudio.App.Core.Mvvm;
using DebugStudio.App.Core.Services;
using DebugStudio.Export.Models;

namespace DebugStudio.App.Features.Telemetry;

/// <summary>
/// Telemetry 画面の export 形式・出力先・状態文言をまとめる。
/// NDJSON と Elastic Bulk の切り替えを ViewModel 本体から分離し、UI 側は単純な binding で済むようにする。
/// </summary>
internal sealed class TelemetryExportState : ObservableObject
{
    private readonly IReadOnlyList<TelemetryExportFormatOption> _exportFormats;
    private readonly TelemetryExportPathPolicy _pathPolicy;
    private string _exportPath;
    private string _exportStatus = "Telemetry NDJSON export is ready.";
    private TelemetryExportFormatOption _selectedExportFormat;

    public TelemetryExportState(TelemetryExportPathPolicy? pathPolicy = null)
    {
        _pathPolicy = pathPolicy ?? new TelemetryExportPathPolicy();
        _exportFormats =
        [
            new TelemetryExportFormatOption("NDJSON", TelemetryExportFormat.Ndjson, ".ndjson"),
            new TelemetryExportFormatOption("Elastic Bulk", TelemetryExportFormat.ElasticBulk, ".bulk.ndjson"),
        ];
        _selectedExportFormat = _exportFormats[0];
        _exportPath = _pathPolicy.CreateDefaultPath(_selectedExportFormat.FileExtension);
    }

    public event EventHandler? ExportPathChanged;

    public event EventHandler? ExportFormatChanged;

    public IReadOnlyList<TelemetryExportFormatOption> ExportFormats => _exportFormats;

    public string ExportPath
    {
        get => _exportPath;
        set
        {
            if (SetProperty(ref _exportPath, value))
            {
                ExportPathChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public TelemetryExportFormatOption SelectedExportFormat
    {
        get => _selectedExportFormat;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _selectedExportFormat, value))
            {
                ExportStatus = value.Format switch
                {
                    TelemetryExportFormat.ElasticBulk => "Telemetry Elastic bulk export is ready.",
                    _ => "Telemetry NDJSON export is ready.",
                };
                ExportPath = _pathPolicy.UpdateExtension(_exportPath, value.FileExtension);
                ExportFormatChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string ExportStatus
    {
        get => _exportStatus;
        set => SetProperty(ref _exportStatus, value);
    }

    public bool CanExport()
    {
        return !string.IsNullOrWhiteSpace(ExportPath);
    }
}
