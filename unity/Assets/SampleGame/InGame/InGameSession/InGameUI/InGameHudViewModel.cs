#nullable enable

using OneStarMaker.Runtime.UISystem.Mvvm;
using R3;
using UnityEngine;

namespace SampleGame.InGame.UI
{
    /// <summary>
    /// InGame HUD の ViewModel。
    /// Stable State は R3 ReactiveProperty に載せ、View は購読して UIToolkit へ射影するだけにする（06-ui 正典）。
    /// データ源は InGameSession サービス面であり、PlayerScene を直接知らない。
    /// </summary>
    public sealed class InGameHudViewModel : ViewModelBase
    {
        private readonly ReactiveProperty<string> _currentLevel = new("(waiting)");
        private readonly ReactiveProperty<string> _loadedLevels = new("-");
        private readonly ReactiveProperty<string> _busyLabel = new("idle");
        private readonly ReactiveProperty<string> _positionLabel = new("0 , 0 , 0");
        private readonly ReactiveProperty<string> _overlayTitle = new(string.Empty);
        private readonly ReactiveProperty<string> _overlayBody = new(string.Empty);
        private readonly ReactiveProperty<bool> _overlayVisible = new(false);

        /// <summary>現在 Level 表示名（または Identity）。</summary>
        public ReadOnlyReactiveProperty<string> CurrentLevel => _currentLevel;

        /// <summary>ロード済み Level のカンマ区切り。</summary>
        public ReadOnlyReactiveProperty<string> LoadedLevels => _loadedLevels;

        /// <summary>ストリーミングビジー表示。</summary>
        public ReadOnlyReactiveProperty<string> BusyLabel => _busyLabel;

        /// <summary>プレイヤー座標テキスト。</summary>
        public ReadOnlyReactiveProperty<string> PositionLabel => _positionLabel;

        /// <summary>遷移オーバーレイのタイトル。</summary>
        public ReadOnlyReactiveProperty<string> OverlayTitle => _overlayTitle;

        /// <summary>遷移オーバーレイの本文。</summary>
        public ReadOnlyReactiveProperty<string> OverlayBody => _overlayBody;

        /// <summary>オーバーレイを出すか。</summary>
        public ReadOnlyReactiveProperty<bool> OverlayVisible => _overlayVisible;

        /// <summary>操作ヘルプ（定数。プレゼンテーション Stable State）。</summary>
        public string ControlsHelp { get; } =
            "WASD fly  Space/Ctrl up-down  Shift boost\n" +
            "Mouse look  Esc cursor  F1-F4 season warp\n" +
            "Fly +Z into glowing tunnels to stream next season";

        /// <summary>HUD 左上のストリーミング状態を更新する。</summary>
        public void SetStreamingState(string currentLevel, string loadedLevels, bool isBusy)
        {
            _currentLevel.Value = string.IsNullOrEmpty(currentLevel) ? "(waiting)" : currentLevel;
            _loadedLevels.Value = string.IsNullOrEmpty(loadedLevels) ? "-" : loadedLevels;
            _busyLabel.Value = isBusy ? "STREAMING..." : "idle";
        }

        /// <summary>プレイヤー位置表示を更新する（表示用間引きは呼び出し側の責務）。</summary>
        public void SetPosition(Vector3 position)
        {
            _positionLabel.Value = $"{position.x:0} , {position.y:0} , {position.z:0}";
        }

        /// <summary>遷移オーバーレイを表示する。</summary>
        public void ShowOverlay(string title, string body)
        {
            _overlayTitle.Value = title ?? string.Empty;
            _overlayBody.Value = body ?? string.Empty;
            _overlayVisible.Value = true;
        }

        /// <summary>遷移オーバーレイを隠す。</summary>
        public void HideOverlay()
        {
            _overlayVisible.Value = false;
            _overlayTitle.Value = string.Empty;
            _overlayBody.Value = string.Empty;
        }

        /// <inheritdoc />
        protected override void DisposeCore()
        {
            _currentLevel.Dispose();
            _loadedLevels.Dispose();
            _busyLabel.Dispose();
            _positionLabel.Dispose();
            _overlayTitle.Dispose();
            _overlayBody.Dispose();
            _overlayVisible.Dispose();
        }
    }
}
