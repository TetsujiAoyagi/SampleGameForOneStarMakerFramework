#nullable enable

using System.Collections.Generic;

namespace OneStarMaker.Runtime.UISystem.Behaviors
{
    /// <summary>
    /// 遷移中の表示状態（Visual State）を保持する key-value ストア。
    /// </summary>
    public sealed class VisualStateStore
    {
        /// <summary>
        /// FromCurrent 割り込み時に Payload.OldValue へ差し替える現在値のキー。
        /// TweenNumber 等の Behavior が ExecuteAsync 中に更新する。
        /// </summary>
        public const string CurrentTransitionKey = "displayValue";

        /// <summary>
        /// Rewind 進行率算出用の想定所要時間（秒）キー。
        /// </summary>
        public const string EstimatedDurationKey = "estimatedDuration";

        private readonly Dictionary<string, object?> _values = new();

        /// <summary>
        /// キーに対応する値を取得する。未設定または型不一致の場合は fallback を返す。
        /// </summary>
        /// <typeparam name="T">期待する型。</typeparam>
        /// <param name="key">キー。</param>
        /// <param name="fallback">フォールバック値。</param>
        /// <returns>保存値または fallback。</returns>
        public T GetOr<T>(string key, T fallback)
        {
            if (_values.TryGetValue(key, out var value) && value is T typed)
            {
                return typed;
            }

            return fallback;
        }

        /// <summary>
        /// キーに値を設定する。
        /// </summary>
        /// <typeparam name="T">値の型。</typeparam>
        /// <param name="key">キー。</param>
        /// <param name="value">設定する値。</param>
        public void Set<T>(string key, T value)
        {
            _values[key] = value;
        }
    }
}
