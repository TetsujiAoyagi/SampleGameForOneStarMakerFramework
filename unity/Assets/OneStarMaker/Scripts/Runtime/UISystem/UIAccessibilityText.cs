#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using R3;
using UnityEngine.UIElements;

namespace OneStarMaker.Runtime.UISystem
{
    /// <summary>
    /// VisualElement に載せるアクセシビリティの名前とヒント。
    /// </summary>
    /// <remarks>
    /// Unity 6000.6 の <see cref="VisualElement"/> は名前・ヒント用のプロパティを持たない。
    /// この側表は要素とその寿命に閉じ、世界の個体レジストリ（意味アイデンティティ）や
    /// <c>AssistiveSupport</c> には登録しない。読み上げと出力予算はここでは扱わない。
    /// 戻り値の <see cref="IDisposable"/> を <see cref="UIToolkitView.Track{T}"/> へ渡すと、
    /// View 破棄時にその代入だけが外れる。複数の代入はスタックで保持し、Dispose 順に依存しない。
    /// </remarks>
    public static class UIAccessibilityText
    {
        private sealed class Entry
        {
            public string? Name;
            public string? Hint;
        }

        private sealed class TextAssignments
        {
            public readonly List<Entry> Entries = new();
        }

        private static readonly ConditionalWeakTable<VisualElement, TextAssignments> Table = new();

        /// <summary>
        /// 現在有効な名前とヒントを読む。未設定なら false。
        /// </summary>
        /// <param name="element">対象要素。</param>
        /// <param name="name">名前。未設定なら null。</param>
        /// <param name="hint">ヒント。名前だけでヒントが無い場合は null。</param>
        /// <returns>代入が残っていれば true。</returns>
        public static bool TryGet(VisualElement element, out string? name, out string? hint)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (!Table.TryGetValue(element, out var stack) || stack.Entries.Count == 0)
            {
                name = null;
                hint = null;
                return false;
            }

            var top = stack.Entries[stack.Entries.Count - 1];
            name = top.Name;
            hint = top.Hint;
            return true;
        }

        /// <summary>
        /// 名前と任意のヒントを固定する。Dispose でこの代入だけを外す。
        /// </summary>
        /// <param name="element">対象要素。</param>
        /// <param name="name">読み上げ名。空文字は許す。</param>
        /// <param name="hint">補足。無ければ null。</param>
        /// <returns>この代入を外す Disposable。</returns>
        public static IDisposable Set(VisualElement element, string name, string? hint = null)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var entry = Push(element, name, hint);
            return Disposable.Create(() => Remove(element, entry));
        }

        /// <summary>
        /// 名前（と任意のヒント）を Observable に追従させる。Dispose で購読と代入を外す。
        /// </summary>
        /// <param name="element">対象要素。</param>
        /// <param name="name">名前ソース。null 値は空文字として載せる。</param>
        /// <param name="hint">ヒントソース。省略時はヒントを載せない。</param>
        /// <returns>購読と代入を外す Disposable。</returns>
        public static IDisposable Bind(
            VisualElement element,
            Observable<string> name,
            Observable<string>? hint = null)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var entry = Push(element, string.Empty, null);
            var nameSubscription = name.Subscribe(value => entry.Name = value ?? string.Empty);
            var hintSubscription = hint?.Subscribe(value => entry.Hint = value);
            return Disposable.Create(() =>
            {
                nameSubscription.Dispose();
                hintSubscription?.Dispose();
                Remove(element, entry);
            });
        }

        private static Entry Push(VisualElement element, string? name, string? hint)
        {
            var stack = Table.GetOrCreateValue(element)!;
            var entry = new Entry
            {
                Name = name,
                Hint = hint,
            };
            stack.Entries.Add(entry);
            return entry;
        }

        private static void Remove(VisualElement element, Entry entry)
        {
            if (!Table.TryGetValue(element, out var stack))
            {
                return;
            }

            stack.Entries.Remove(entry);
            if (stack.Entries.Count == 0)
            {
                Table.Remove(element);
            }
        }
    }
}
