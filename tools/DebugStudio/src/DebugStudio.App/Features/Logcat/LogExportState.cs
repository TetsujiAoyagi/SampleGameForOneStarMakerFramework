#nullable enable


using DebugStudio.App.Core.Mvvm;
using System;
using System.Collections.Generic;
using DebugStudio.App.Core.Services;
using DebugStudio.Export.Models;

namespace DebugStudio.App.Features.LogViewer;

/// <summary>
/// Log 画面の export UI 状態をまとめる。
///
/// <para>
/// 出力形式、出力先パス、ステータス文言を 1 箇所へ閉じ込めることで、
/// ViewModel 本体は「いつ export できるか」「どの形式で出すか」の調停に集中できる。
/// 形式切替時に拡張子を自動追従させる責務もここに寄せる。
/// </para>
/// </summary>
internal sealed class LogExportState : ObservableObject
{
    private readonly IReadOnlyList<LogExportFormatOption> _exportFormats;
    private readonly LogExportPathPolicy _pathPolicy;
    private string _exportPath;
    private string _exportStatus = "NDJSON export is ready.";
    private LogExportFormatOption _selectedExportFormat;

    public LogExportState(LogExportPathPolicy? pathPolicy = null)
    {
        _pathPolicy = pathPolicy ?? new LogExportPathPolicy();
        _exportFormats =
        [
            new LogExportFormatOption("NDJSON", LogExportFormat.Ndjson, ".ndjson"),
            new LogExportFormatOption("CSV", LogExportFormat.Csv, ".csv"),
            new LogExportFormatOption("Elastic Bulk", LogExportFormat.ElasticBulk, ".bulk.ndjson"),
        ];
        _selectedExportFormat = _exportFormats[0];
        _exportPath = _pathPolicy.CreateDefaultPath(_selectedExportFormat.FileExtension);
    }

    /// <summary>
    /// 出力パスが変更されたときに発火する。
    /// format 切替による拡張子更新もこのイベントに含める。
    /// </summary>
    public event EventHandler? ExportPathChanged;

    /// <summary>
    /// 出力形式が変更されたときに発火する。
    /// UI のボタン文言や status の追従更新に使う。
    /// </summary>
    public event EventHandler? ExportFormatChanged;

    /// <summary>
    /// UI が提示する export format 候補一覧。
    /// </summary>
    public IReadOnlyList<LogExportFormatOption> ExportFormats => _exportFormats;

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

    public LogExportFormatOption SelectedExportFormat
    {
        get => _selectedExportFormat;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _selectedExportFormat, value))
            {
                ExportStatus = value.Format switch
                {
                    LogExportFormat.Csv => "CSV export is ready.",
                    LogExportFormat.ElasticBulk => "Elastic Bulk export is ready.",
                    _ => "NDJSON export is ready.",
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

    /// <summary>
    /// export を開始できる最小条件を返す。
    /// path の妥当性や対象件数の詳細判定は上位の ViewModel 側で追加する。
    /// </summary>
    public bool CanExport()
    {
        return !string.IsNullOrWhiteSpace(ExportPath);
    }
}
