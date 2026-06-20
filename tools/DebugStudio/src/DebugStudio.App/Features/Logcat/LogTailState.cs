#nullable enable


using DebugStudio.App.Core.Mvvm;
using System;

namespace DebugStudio.App.Features.LogViewer;

internal sealed class LogTailState : ObservableObject
{
    private bool _isAutoScrollEnabled = true;
    private string _tailStateText = "Live tail is following the latest row.";
    private string _tailToggleButtonText = "Pause";

    public event EventHandler? AutoScrollChanged;

    public bool IsAutoScrollEnabled
    {
        get => _isAutoScrollEnabled;
        set
        {
            if (SetProperty(ref _isAutoScrollEnabled, value))
            {
                UpdateTailStateText();
                AutoScrollChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string TailStateText
    {
        get => _tailStateText;
        private set => SetProperty(ref _tailStateText, value);
    }

    public string TailToggleButtonText
    {
        get => _tailToggleButtonText;
        private set => SetProperty(ref _tailToggleButtonText, value);
    }

    public void ToggleAutoScroll()
    {
        IsAutoScrollEnabled = !IsAutoScrollEnabled;
    }

    private void UpdateTailStateText()
    {
        TailStateText = IsAutoScrollEnabled
            ? "Live tail is following the latest row."
            : "Tail paused. Selection stays stable while new logs arrive.";
        TailToggleButtonText = IsAutoScrollEnabled ? "Pause" : "Resume";
    }
}
