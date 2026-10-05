#nullable enable

using System.Collections.Generic;
using UnityEngine.UIElements;

namespace OneStarMaker.Runtime.UISystem
{
    /// <summary>
    /// Dialog / Modal の UI Toolkit ビューに対する初期フォーカス選択。
    /// </summary>
    /// <remarks>
    /// タブ順は UITK の <c>focusable</c> と <c>tabIndex</c> だけで決める。
    /// 正の tabIndex は昇順で tabIndex 0 より前、負の tabIndex はリングから外す。
    /// 0 同士は子の深さ優先順。これは <see cref="VisualElementFocusRing"/> の比較と同じ規則だが、
    /// エンジンのリングはレイアウト updater が HierarchyDisplayed を立てるまで候補を空にする。
    /// ViewIn 完了直後はそのフラグがまだ無いことがあるため、ここでは style の display で判定する。
    /// ViewOut で開く前のフォーカスへ戻す処理は、直前の focused 要素の所有者を別に持つ必要があり、ここでは行わない。
    /// </remarks>
    public static class UIToolkitInitialFocus
    {
        private readonly struct Candidate
        {
            public Candidate(VisualElement element, int order)
            {
                Element = element;
                Order = order;
            }

            public VisualElement Element { get; }

            public int Order { get; }
        }

        /// <summary>
        /// Modal と Dialog だけ初期フォーカスの対象にする。
        /// </summary>
        /// <param name="layer">ビューのレイヤー。</param>
        /// <returns>初期フォーカスを当てるレイヤーなら true。</returns>
        public static bool ShouldApply(UIView.UILayer layer)
        {
            return layer == UIView.UILayer.Modal || layer == UIView.UILayer.Dialog;
        }

        /// <summary>
        /// 名前付きの既定がタブ順に乗っていればそれを、無ければタブ順の先頭を返す。
        /// </summary>
        /// <param name="root">探索の根。根自身は候補に含めない。</param>
        /// <param name="preferredName">優先する要素名。空なら先頭。</param>
        /// <returns>フォーカス対象。候補が無ければ null。</returns>
        public static VisualElement? Select(VisualElement root, string? preferredName)
        {
            if (root == null)
            {
                throw new System.ArgumentNullException(nameof(root));
            }

            if (!string.IsNullOrEmpty(preferredName))
            {
                var named = FindByName(root, preferredName);
                if (named != null && IsTabStop(named))
                {
                    return named;
                }
            }

            var stops = ListTabStops(root);
            return stops.Count == 0 ? null : stops[0];
        }

        /// <summary>
        /// <see cref="Select"/> の対象へ <see cref="Focusable.Focus"/> する。
        /// パネル未参加の要素はフォーカスできないので false を返し、例外にはしない。
        /// </summary>
        /// <param name="root">探索の根。</param>
        /// <param name="preferredName">優先する要素名。</param>
        /// <returns>Focus を呼べたら true。</returns>
        public static bool TryApply(VisualElement root, string? preferredName)
        {
            var target = Select(root, preferredName);
            if (target == null || target.panel == null)
            {
                return false;
            }

            target.Focus();
            return true;
        }

        /// <summary>
        /// タブ順の要素を返す。テストと初期フォーカスが同じ規則を使う。
        /// </summary>
        /// <param name="root">探索の根。根自身は含めない。</param>
        /// <returns>タブ順。</returns>
        internal static List<VisualElement> ListTabStops(VisualElement root)
        {
            var candidates = new List<Candidate>();
            if (IsEligibleBranch(root))
            {
                Collect(root, candidates);
            }
            candidates.Sort(Compare);
            var result = new List<VisualElement>(candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                result.Add(candidates[i].Element);
            }

            return result;
        }

        private static void Collect(VisualElement parent, List<Candidate> into)
        {
            var hierarchy = parent.hierarchy;
            var count = hierarchy.childCount;
            for (var i = 0; i < count; i++)
            {
                var child = hierarchy[i];
                if (!IsEligibleBranch(child))
                {
                    continue;
                }

                if (IsTabStop(child))
                {
                    into.Add(new Candidate(child, into.Count));
                }

                Collect(child, into);
            }
        }

        private static int Compare(Candidate a, Candidate b)
        {
            var aIndex = a.Element.tabIndex;
            var bIndex = b.Element.tabIndex;
            var aPositive = aIndex > 0;
            var bPositive = bIndex > 0;
            if (aPositive && bPositive)
            {
                var byIndex = aIndex.CompareTo(bIndex);
                if (byIndex != 0)
                {
                    return byIndex;
                }

                return a.Order.CompareTo(b.Order);
            }

            if (aPositive)
            {
                return -1;
            }

            if (bPositive)
            {
                return 1;
            }

            return a.Order.CompareTo(b.Order);
        }

        private static VisualElement? FindByName(VisualElement root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            return root.Q<VisualElement>(name);
        }

        private static bool IsTabStop(VisualElement element)
        {
            return element.focusable
                && !element.delegatesFocus
                && element.tabIndex >= 0
                && IsEligibleBranch(element);
        }

        private static bool IsEligibleBranch(VisualElement element)
        {
            return element.enabledInHierarchy && IsDisplayed(element);
        }

        private static bool IsDisplayed(VisualElement element)
        {
            if (!element.visible)
            {
                return false;
            }

            // inline の none はパネル参加前でも分かる。resolvedStyle は USS 側の none を拾う。
            var inline = element.style.display;
            if (inline.keyword != StyleKeyword.Null && inline.value == DisplayStyle.None)
            {
                return false;
            }

            return element.resolvedStyle.display != DisplayStyle.None;
        }
    }
}
