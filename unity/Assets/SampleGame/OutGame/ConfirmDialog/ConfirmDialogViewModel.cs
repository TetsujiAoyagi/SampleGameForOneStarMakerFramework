#nullable enable

using System;
using OneStarMaker.Runtime.UISystem.Mvvm;

namespace SampleGame.OutGame.ConfirmDialog
{
    /// <summary>
    /// 確認ダイアログの ViewModel。
    /// </summary>
    public sealed class ConfirmDialogViewModel : ViewModelBase
    {
        private string _message = string.Empty;

        /// <summary>表示メッセージ。</summary>
        public string Message
        {
            get => _message;
            set => _message = value ?? string.Empty;
        }

        /// <summary>OK / Cancel の結果通知。</summary>
        public event Action<bool>? Decided;

        /// <summary>ユーザーが OK または Cancel を選択した。</summary>
        /// <param name="accepted">OK なら true、Cancel なら false。</param>
        public void Decide(bool accepted)
        {
            Decided?.Invoke(accepted);
        }
    }
}
