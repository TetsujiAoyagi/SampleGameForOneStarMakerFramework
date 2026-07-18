#nullable enable

using OneStarMaker.Runtime.UISystem.Mvvm;
using R3;

namespace SampleGame.OutGame.Title
{
    /// <summary>
    /// タイトル画面の ViewModel。表示文言の Stable State を保持する。
    /// </summary>
    public sealed class TitleViewModel : ViewModelBase
    {
        private readonly ReactiveProperty<string> _titleText = new("OneStarMaker Sample");
        private readonly ReactiveProperty<string> _subtitleText = new("Press Start");

        /// <summary>タイトル文言。</summary>
        public ReadOnlyReactiveProperty<string> TitleText => _titleText;

        /// <summary>サブタイトル文言。</summary>
        public ReadOnlyReactiveProperty<string> SubtitleText => _subtitleText;

        /// <inheritdoc />
        protected override void DisposeCore()
        {
            _titleText.Dispose();
            _subtitleText.Dispose();
        }
    }
}
