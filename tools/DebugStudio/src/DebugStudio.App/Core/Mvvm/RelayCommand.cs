#nullable enable

using System;
using System.Windows.Input;

namespace DebugStudio.App.Core.Mvvm;

/// <summary>
/// 同期的な軽量 UI 操作用 command。
/// detach / attach のような即時反応だけが必要な操作で使う。
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _execute();
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
