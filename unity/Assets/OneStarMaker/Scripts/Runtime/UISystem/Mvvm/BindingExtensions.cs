#nullable enable

using System;
using System.Collections.Generic;
using R3;
using UnityEngine.UIElements;

namespace OneStarMaker.Runtime.UISystem.Mvvm
{
    /// <summary>
    /// R3 Observable から UI Toolkit VisualElement への最小バインディング拡張。
    /// 各メソッドは購読の <see cref="IDisposable"/> を返す。
    /// <see cref="UIToolkitView"/> 内から使う場合は、呼び出し側の
    /// <see cref="UIToolkitView.Track{T}(T)"/> へ集約すること。
    /// </summary>
    public static class BindingExtensions
    {
        /// <summary>
        /// <see cref="Label.text"/> を Observable の文字列へバインドする。
        /// </summary>
        /// <param name="label">バインド先 Label。</param>
        /// <param name="source">文字列ソース。</param>
        /// <returns>購読の Disposable。</returns>
        public static IDisposable BindText(this Label label, Observable<string> source)
        {
            if (label == null)
            {
                throw new ArgumentNullException(nameof(label));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Subscribe(text =>
            {
                if (label.text != text)
                {
                    label.text = text;
                }
            });
        }

        /// <summary>
        /// <see cref="Label.text"/> を Observable の値とフォーマッタでバインドする。
        /// フォーマッタは hot path になり得るため、ZString 等でアロケーションを避けること。
        /// 値および整形後文字列が前回と同一なら text への代入を省略する。
        /// </summary>
        /// <typeparam name="T">ソース値の型。</typeparam>
        /// <param name="label">バインド先 Label。</param>
        /// <param name="source">値ソース。</param>
        /// <param name="formatter">表示文字列への変換。</param>
        /// <returns>購読の Disposable。</returns>
        public static IDisposable BindText<T>(
            this Label label,
            Observable<T> source,
            Func<T, string> formatter)
        {
            if (label == null)
            {
                throw new ArgumentNullException(nameof(label));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            var hasLastValue = false;
            var lastValue = default(T);
            string? lastText = null;

            return source.Subscribe(value =>
            {
                if (hasLastValue && EqualityComparer<T>.Default.Equals(lastValue!, value))
                {
                    return;
                }

                hasLastValue = true;
                lastValue = value;

                var text = formatter(value);
                if (lastText == text)
                {
                    return;
                }

                lastText = text;
                label.text = text;
            });
        }

        /// <summary>
        /// <see cref="Button.clicked"/> を Action へバインドする。
        /// </summary>
        /// <param name="button">バインド先 Button。</param>
        /// <param name="onClick">クリック時コールバック。</param>
        /// <returns>購読解除用 Disposable。</returns>
        public static IDisposable BindClick(this Button button, Action onClick)
        {
            if (button == null)
            {
                throw new ArgumentNullException(nameof(button));
            }

            if (onClick == null)
            {
                throw new ArgumentNullException(nameof(onClick));
            }

            button.clicked += onClick;
            return Disposable.Create(() => button.clicked -= onClick);
        }

        /// <summary>
        /// <see cref="VisualElement.style"/>.display を Observable の bool へバインドする。
        /// true = Flex、false = None。
        /// </summary>
        /// <param name="element">バインド先 VisualElement。</param>
        /// <param name="source">表示フラグソース。</param>
        /// <returns>購読の Disposable。</returns>
        public static IDisposable BindVisible(this VisualElement element, Observable<bool> source)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Subscribe(visible =>
            {
                var display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (element.style.display != display)
                {
                    element.style.display = display;
                }
            });
        }
    }
}
