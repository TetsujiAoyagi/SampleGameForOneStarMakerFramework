#nullable enable

using System;
using OneStarMaker.Runtime.UISystem.Mvvm;
using R3;

namespace SampleGame.OutGame.ConfirmDialog
{
    /// <summary>
    /// 確認ダイアログの ViewModel。
    /// </summary>
    public sealed class ConfirmDialogViewModel : ViewModelBase
    {
        private readonly ReactiveProperty<string> _message = new(string.Empty);
        private readonly ReactiveProperty<bool> _isMessageVisible = new(true);

        /// <summary>表示メッセージのプレゼンテーション Stable State。</summary>
        public ReadOnlyReactiveProperty<string> Message => _message;

        /// <summary>メッセージを表示するかのプレゼンテーション Stable State。</summary>
        public ReadOnlyReactiveProperty<bool> IsMessageVisible => _isMessageVisible;

        /// <summary>表示メッセージを更新する。</summary>
        public void SetMessage(string message)
        {
            _message.Value = message ?? string.Empty;
        }

        /// <summary>メッセージの表示可否を更新する。</summary>
        public void SetMessageVisible(bool visible)
        {
            _isMessageVisible.Value = visible;
        }

        /// <summary>OK / Cancel の結果通知。</summary>
        public event Action<bool>? Decided;

        /// <summary>ユーザーが OK または Cancel を選択した。</summary>
        /// <param name="accepted">OK なら true、Cancel なら false。</param>
        public void Decide(bool accepted)
        {
            Decided?.Invoke(accepted);
        }

        /// <inheritdoc />
        protected override void DisposeCore()
        {
            _message.Dispose();
            _isMessageVisible.Dispose();
        }
    }
}
