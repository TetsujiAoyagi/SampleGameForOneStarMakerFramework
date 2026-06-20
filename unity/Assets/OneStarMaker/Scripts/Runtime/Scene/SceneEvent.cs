#nullable enable

using System;
using Cysharp.Text;

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// SceneDirector が発火するイベントの種別。
    /// </summary>
    public enum SceneEventType
    {
        /// <summary>シーンの状態が変化した。</summary>
        StateChanged,

        /// <summary>シーンが Stable に到達した（ロード完了）。</summary>
        Added,

        /// <summary>シーンがアンロード完了した（辞書から除去済み）。</summary>
        Removed,

        /// <summary>キャンセルによるクリーンアップが完了した。</summary>
        CancelCleanedUp,
    }

    /// <summary>
    /// SceneDirector の Observable が発行するイベント。
    /// immutable な値型として定義し、GC 負荷を最小化する。
    /// テレメトリ用にフェーズ所要時間とタイムスタンプを含む。
    /// </summary>
    public readonly struct SceneEvent
    {
        /// <summary>イベント種別。</summary>
        public SceneEventType Type { get; }

        /// <summary>対象シーンの識別子。</summary>
        public string SceneIdentify { get; }

        /// <summary>イベント発生時点のシーン状態。</summary>
        public SceneState State { get; }

        /// <summary>直前フェーズの所要時間 (ms)。テレメトリ用。</summary>
        public long ElapsedMs { get; }

        /// <summary>イベント発生時刻 (UTC ticks)。テレメトリ用。</summary>
        public long TimestampUtcTicks { get; }

        public SceneEvent(SceneEventType type, string sceneIdentify, SceneState state,
            long elapsedMs = 0)
        {
            Type = type;
            SceneIdentify = sceneIdentify;
            State = state;
            ElapsedMs = elapsedMs;
            TimestampUtcTicks = DateTime.UtcNow.Ticks;
        }

        public override string ToString()
            => ZString.Format("[SceneEvent] {0} {1} ({2}) {3}ms", Type, SceneIdentify, State, ElapsedMs);
    }
}
