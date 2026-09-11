#nullable enable

using System;

namespace SampleGame.InGame.Streaming
{
    /// <summary>既存 SceneEventType の終端 2 種だけ。新しい公開イベントは作らない。</summary>
    internal enum SceneTerminalKind
    {
        Removed,
        CancelCleanedUp,
    }

    /// <summary>終端通知。identity と種別だけを渡す。Query 完了とは混ぜない。</summary>
    internal readonly struct SceneTerminalEvent
    {
        public SceneTerminalEvent(string identity, SceneTerminalKind kind)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Kind = kind;
        }

        public string Identity { get; }
        public SceneTerminalKind Kind { get; }
    }

    /// <summary>
    /// Removed / CancelCleanedUp 相当の観測口。
    /// Game 内だけの契約で、FW の公開 API は増やさない。
    /// </summary>
    internal interface ISceneTerminalEvents
    {
        IDisposable Subscribe(Action<SceneTerminalEvent> handler);
    }
}
