#nullable enable


using DebugStudio.App.Core.Mvvm;

namespace DebugStudio.App.Features.Shell;

/// <summary>
/// まだ専用 UI を作り切っていない lane の仮置き ViewModel。
/// 「未実装です」で終わらせず、現在の feed と今後の差し込み先を shell 上で明示する。
/// </summary>
public sealed class PlaceholderPanelViewModel : ObservableObject
{
    private string _summary;
    private string _status;
    private string _detail;
    private string _secondaryDetail;
    private string _hint;

    public PlaceholderPanelViewModel(
        string summary,
        string status,
        string detail,
        string secondaryDetail,
        string hint)
    {
        _summary = summary;
        _status = status;
        _detail = detail;
        _secondaryDetail = secondaryDetail;
        _hint = hint;
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string Detail
    {
        get => _detail;
        private set => SetProperty(ref _detail, value);
    }

    public string SecondaryDetail
    {
        get => _secondaryDetail;
        private set => SetProperty(ref _secondaryDetail, value);
    }

    public string Hint
    {
        get => _hint;
        private set => SetProperty(ref _hint, value);
    }

    public void Update(
        string summary,
        string status,
        string detail,
        string secondaryDetail,
        string hint)
    {
        Summary = summary;
        Status = status;
        Detail = detail;
        SecondaryDetail = secondaryDetail;
        Hint = hint;
    }
}
