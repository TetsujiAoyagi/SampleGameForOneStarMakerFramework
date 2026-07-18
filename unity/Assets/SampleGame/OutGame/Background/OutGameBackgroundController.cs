#nullable enable

using System;

namespace SampleGame.OutGame.Background
{
    /// <summary>
    /// 共有背景の現在値と描画 Surface への反映を管理する。
    /// 非同期ロード、演出、複数要求の優先順位はこの同期実装の対象外。
    /// </summary>
    public sealed class OutGameBackgroundController : IOutGameBackgroundRequests
    {
        private IOutGameBackgroundSurface? _surface;

        /// <inheritdoc />
        public OutGameBackgroundDefinition? Current { get; private set; }

        /// <summary>背景 Surface を接続し、現在値があれば直ちに再描画する。</summary>
        /// <param name="surface">接続する描画 Surface。</param>
        public void Attach(IOutGameBackgroundSurface surface)
        {
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }

            if (_surface != null && !ReferenceEquals(_surface, surface))
            {
                throw new InvalidOperationException(
                    "別の背景 Surface が接続済みです。接続前に既存 Surface を Detach してください。");
            }

            _surface = surface;

            if (Current != null)
            {
                _surface.Apply(Current);
            }
        }

        /// <summary>現在接続されている Surface を切断する。</summary>
        /// <param name="surface">切断する描画 Surface。</param>
        public void Detach(IOutGameBackgroundSurface surface)
        {
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }

            if (ReferenceEquals(_surface, surface))
            {
                _surface = null;
            }
        }

        /// <inheritdoc />
        public void Request(OutGameBackgroundDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!definition.IsValid)
            {
                throw new ArgumentException("背景定義に Texture が割り当てられていません。", nameof(definition));
            }

            if (ReferenceEquals(Current, definition))
            {
                return;
            }

            Current = definition;
            _surface?.Apply(definition);
        }
    }
}
