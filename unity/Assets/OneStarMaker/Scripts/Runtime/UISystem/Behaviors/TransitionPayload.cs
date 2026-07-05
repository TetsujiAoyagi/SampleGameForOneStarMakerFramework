#nullable enable

namespace OneStarMaker.Runtime.UISystem.Behaviors
{
    /// <summary>
    /// Stable State の変化（old → new）を Behavior パイプラインへ渡すペイロード。
    /// </summary>
    public sealed class TransitionPayload
    {
        /// <summary>
        /// 変化前の Stable State 値。
        /// </summary>
        public object? OldValue { get; }

        /// <summary>
        /// 変化後の Stable State 値。
        /// </summary>
        public object? NewValue { get; }

        /// <summary>
        /// ペイロードを生成する。
        /// </summary>
        /// <param name="oldValue">変化前の値。</param>
        /// <param name="newValue">変化後の値。</param>
        public TransitionPayload(object? oldValue, object? newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <summary>
        /// 変化前の値を型付きで取得する。
        /// </summary>
        /// <typeparam name="T">期待する型。</typeparam>
        /// <returns>キャスト成功時は値、それ以外は default。</returns>
        public T? GetOld<T>()
        {
            if (OldValue is T typed)
            {
                return typed;
            }

            return default;
        }

        /// <summary>
        /// 変化後の値を型付きで取得する。
        /// </summary>
        /// <typeparam name="T">期待する型。</typeparam>
        /// <returns>キャスト成功時は値、それ以外は default。</returns>
        public T? GetNew<T>()
        {
            if (NewValue is T typed)
            {
                return typed;
            }

            return default;
        }
    }
}
