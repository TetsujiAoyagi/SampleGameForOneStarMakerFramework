#nullable enable

using System;
using OneStarMaker.Runtime.UISystem;
using SampleGame.OutGame.Background;
using UnityEngine;
using UnityEngine.UIElements;

namespace SampleGame.OutGame
{
    public sealed class OutGameBackgroundView : UIToolkitView
        , IOutGameBackgroundSurface
    {
        private const string BackgroundSurfaceName = "outgame-background-surface";

        private OutGameBackgroundController? _controller;
        private OutGameBackgroundDefinition? _current;
        private VisualElement? _surface;

        /// <inheritdoc />
        public override UILayer GetUILayer() => UILayer.Background;

        /// <summary>共有背景 Controller にこの View を接続する。</summary>
        /// <param name="controller">OutGame が所有する背景 Controller。</param>
        public void Connect(OutGameBackgroundController controller)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (ReferenceEquals(_controller, controller))
            {
                return;
            }

            _controller?.Detach(this);
            _controller = controller;
            _controller.Attach(this);
        }

        /// <inheritdoc />
        public void Apply(OutGameBackgroundDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            _current = definition;

            if (_surface != null)
            {
                ApplyCurrent();
            }
        }

        /// <inheritdoc />
        protected override void OnRootCreated(VisualElement root)
        {
            // 静的な構造・class・picking mode は UXML/USS の権威とする。
            // Q() は Root 生成時の一度だけで、UXML の配線不備は即時検出する。
            _surface = root.Q<VisualElement>(BackgroundSurfaceName)
                ?? throw new InvalidOperationException(
                    $"{GetType().Name} の UXML に '{BackgroundSurfaceName}' が必要です。");

            if (_current != null)
            {
                ApplyCurrent();
            }
        }

        /// <inheritdoc />
        protected override void OnViewDestroy()
        {
            _controller?.Detach(this);
            _controller = null;
            _surface = null;
        }

        private void ApplyCurrent()
        {
            var texture = _current!.Texture;
            if (texture == null)
            {
                throw new InvalidOperationException("背景定義に Texture が割り当てられていません。");
            }
            _surface!.style.backgroundImage = new StyleBackground(texture);
            _surface.style.unityBackgroundImageTintColor = _current.Tint;
        }
    }
}
