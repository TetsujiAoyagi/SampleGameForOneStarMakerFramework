#nullable enable

using System;
using OneStarMaker.Runtime.SceneSystem;
using R3;

namespace SampleGame.InGame.Streaming
{
    /// <summary>
    /// <see cref="SceneDirector.OnSceneEvent"/> を <see cref="ISceneTerminalEvents"/> へ適応する。
    /// 具象 SceneDirector は composition（InGameSession.OnLoaded）に閉じる。
    /// </summary>
    internal sealed class SceneDirectorTerminalEvents : ISceneTerminalEvents, IDisposable
    {
        private readonly IDisposable _subscription;
        private Action<SceneTerminalEvent>? _handlers;
        private bool _disposed;

        internal SceneDirectorTerminalEvents(SceneDirector sceneDirector)
        {
            if (sceneDirector == null)
            {
                throw new ArgumentNullException(nameof(sceneDirector));
            }

            _subscription = sceneDirector.OnSceneEvent.Subscribe(OnSceneEvent);
        }

        public IDisposable Subscribe(Action<SceneTerminalEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _handlers += handler;
            return new Unsubscriber(this, handler);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _handlers = null;
            _subscription.Dispose();
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            SceneTerminalKind kind;
            switch (sceneEvent.Type)
            {
                case SceneEventType.Removed:
                    kind = SceneTerminalKind.Removed;
                    break;
                case SceneEventType.CancelCleanedUp:
                    kind = SceneTerminalKind.CancelCleanedUp;
                    break;
                default:
                    return;
            }

            _handlers?.Invoke(new SceneTerminalEvent(sceneEvent.SceneIdentify, kind));
        }

        private sealed class Unsubscriber : IDisposable
        {
            private SceneDirectorTerminalEvents? _owner;
            private readonly Action<SceneTerminalEvent> _handler;

            internal Unsubscriber(SceneDirectorTerminalEvents owner, Action<SceneTerminalEvent> handler)
            {
                _owner = owner;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_owner == null)
                {
                    return;
                }

                _owner._handlers -= _handler;
                _owner = null;
            }
        }
    }
}
