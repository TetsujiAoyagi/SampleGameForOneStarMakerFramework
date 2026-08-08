#nullable enable


using DebugStudio.App.Core.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Services;
using DebugStudio.Contracts.Schema;

namespace DebugStudio.App.Features.LogViewer;

internal sealed class LogFilterState : ObservableObject
{
    private readonly IReadOnlyList<LogKindFilterOption> _kindFilters;
    private readonly ObservableCollection<LogCategoryFilterOption> _categoryFilters;
    private string _queryText = string.Empty;
    private LogKindFilterOption _selectedKindFilter;
    private LogCategoryFilterOption _selectedCategoryFilter;
    private bool _useRegex;

    public LogFilterState()
    {
        _kindFilters =
        [
            new LogKindFilterOption("All Levels", null),
            new LogKindFilterOption("Trace", LogEntryKind.Trace),
            new LogKindFilterOption("Debug", LogEntryKind.Debug),
            new LogKindFilterOption("Info", LogEntryKind.Information),
            new LogKindFilterOption("Warning", LogEntryKind.Warning),
            new LogKindFilterOption("Error", LogEntryKind.Error),
            new LogKindFilterOption("Critical", LogEntryKind.Critical),
        ];
        _categoryFilters =
        [
            new LogCategoryFilterOption("All Categories", null),
        ];
        _selectedKindFilter = _kindFilters[0];
        _selectedCategoryFilter = _categoryFilters[0];
    }

    public event EventHandler? FilterChanged;

    public IReadOnlyList<LogKindFilterOption> KindFilters => _kindFilters;

    public ObservableCollection<LogCategoryFilterOption> CategoryFilters => _categoryFilters;

    public string QueryText
    {
        get => _queryText;
        set
        {
            if (SetProperty(ref _queryText, value))
            {
                FilterChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public LogKindFilterOption SelectedKindFilter
    {
        get => _selectedKindFilter;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _selectedKindFilter, value))
            {
                FilterChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public LogCategoryFilterOption SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                FilterChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool UseRegex
    {
        get => _useRegex;
        set
        {
            if (SetProperty(ref _useRegex, value))
            {
                FilterChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool CanClearQuery()
    {
        return !string.IsNullOrWhiteSpace(QueryText) ||
            SelectedKindFilter.Kind.HasValue ||
            SelectedCategoryFilter.Category != null ||
            UseRegex;
    }

    public void ClearQuery()
    {
        var changed = false;
        if (!string.IsNullOrWhiteSpace(QueryText))
        {
            _queryText = string.Empty;
            OnPropertyChanged(nameof(QueryText));
            changed = true;
        }

        if (SelectedKindFilter.Kind.HasValue)
        {
            _selectedKindFilter = _kindFilters[0];
            OnPropertyChanged(nameof(SelectedKindFilter));
            changed = true;
        }

        if (SelectedCategoryFilter.Category != null)
        {
            _selectedCategoryFilter = _categoryFilters[0];
            OnPropertyChanged(nameof(SelectedCategoryFilter));
            changed = true;
        }

        if (UseRegex)
        {
            _useRegex = false;
            OnPropertyChanged(nameof(UseRegex));
            changed = true;
        }

        if (changed)
        {
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetAvailableCategories(IReadOnlyList<string> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);

        var currentCategory = SelectedCategoryFilter.Category;
        var nextOptions = new List<LogCategoryFilterOption>(categories.Count + 1)
        {
            new("All Categories", null),
        };

        foreach (var category in categories)
        {
            if (!string.IsNullOrWhiteSpace(category))
            {
                nextOptions.Add(new LogCategoryFilterOption(category, category));
            }
        }

        var needsUpdate = _categoryFilters.Count != nextOptions.Count;
        if (!needsUpdate)
        {
            for (var index = 0; index < _categoryFilters.Count; index++)
            {
                if (!string.Equals(_categoryFilters[index].Label, nextOptions[index].Label, StringComparison.Ordinal) ||
                    !string.Equals(_categoryFilters[index].Category, nextOptions[index].Category, StringComparison.Ordinal))
                {
                    needsUpdate = true;
                    break;
                }
            }
        }

        if (needsUpdate)
        {
            _categoryFilters.Clear();
            foreach (var option in nextOptions)
            {
                _categoryFilters.Add(option);
            }
        }

        var nextSelected = _categoryFilters[0];
        if (!string.IsNullOrEmpty(currentCategory))
        {
            foreach (var option in _categoryFilters)
            {
                if (string.Equals(option.Category, currentCategory, StringComparison.Ordinal))
                {
                    nextSelected = option;
                    break;
                }
            }
        }

        // FilterChanged は上げない。呼び出し元 (RefreshFromStore) がすでに再描画中であり、
        // ここで上げると再入して古い query 結果で上書きされる。
        // 選択オブジェクト差し替えや All へのフォールバックは PropertyChanged のみ通知する。
        if (!ReferenceEquals(_selectedCategoryFilter, nextSelected))
        {
            _selectedCategoryFilter = nextSelected;
            OnPropertyChanged(nameof(SelectedCategoryFilter));
        }
    }

    public LogQueryOptions CreateQueryOptions()
    {
        return new LogQueryOptions
        {
            SearchText = QueryText,
            Kind = SelectedKindFilter.Kind,
        };
    }

    public LogFilterCriteria CreateFilterCriteria()
    {
        var searchText = QueryText.Trim();
        return new LogFilterCriteria
        {
            TextSearchPattern = searchText.Length == 0 ? null : searchText,
            LevelFilters = SelectedKindFilter.Kind.HasValue ? [SelectedKindFilter.Kind.Value] : null,
            CategoryTags = SelectedCategoryFilter.Category is null ? null : [SelectedCategoryFilter.Category],
            UseRegex = UseRegex && searchText.Length > 0,
        };
    }
}
