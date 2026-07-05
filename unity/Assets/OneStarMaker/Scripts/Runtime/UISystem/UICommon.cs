#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace OneStarMaker.Runtime.UISystem
{
    /// <summary>
    /// DontDestroyOnLoad な共通 Canvas。
    /// 全 UIView をレイヤー順に SiblingIndex で管理する。
    /// SceneBase を知らず、string の ownerId で管理する（一方向依存の実現）。
    /// </summary>
    [RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
    public class UICommon : MonoBehaviour
    {
        /// <summary>
        /// Modal 以上のレイヤーで Blocker を自動生成する閾値。
        /// </summary>
        private const UIView.UILayer BlockerThreshold = UIView.UILayer.Modal;

        /// <summary>UIView の管理エントリ。Blocker を紐付けて管理する。</summary>
        private sealed class UIViewEntry
        {
            public string OwnerId { get; }
            public UIView View { get; }
            /// <summary>Modal 以上のレイヤーで自動生成される背面 Blocker。null なら不要。</summary>
            public GameObject? Blocker { get; set; }

            public UIViewEntry(string ownerId, UIView view)
            {
                OwnerId = ownerId;
                View = view;
            }
        }

        private readonly LinkedList<UIViewEntry> _entries = new();

        [SerializeField]
        private UIView? _loadBackground;

        [SerializeField]
        private UIView? _loadIcon;

        /// <summary>
        /// UIView を追加し、ViewIn を実行する。
        /// §6.7 AddUIView フローに準拠。
        /// </summary>
        /// <param name="ownerId">所有者のシーン識別子。</param>
        /// <param name="view">追加する UIView。</param>
        /// <param name="ct">キャンセルトークン。</param>
        public async UniTask AddUIView(string ownerId, UIView view, CancellationToken ct)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            // [1] SetParent
            view.transform.SetParent(transform, false);

            // [2] LinkedList にレイヤー順で挿入（同レイヤー内は末尾 = Stack 方式）
            var insertBefore = FindInsertPosition(view.GetUILayer());
            var entry = new UIViewEntry(ownerId, view);

            if (insertBefore != null)
            {
                _entries.AddBefore(insertBefore, entry);
            }
            else
            {
                _entries.AddLast(entry);
            }

            // [3] Modal〜Loading レイヤーなら背面 Blocker を自動生成（Debug は除外）
            var layer = view.GetUILayer();
            if (layer >= BlockerThreshold && layer <= UIView.UILayer.Loading)
            {
                entry.Blocker = CreateBlocker(ownerId);
            }

            // [4] RefreshSiblingOrder（LinkedList の順序を SetSiblingIndex に反映）
            RefreshSiblingOrder();

            // [5] ViewIn 実行
            await view.ViewIn(ct);
        }

        /// <summary>
        /// 指定 ownerId の UIView を ViewOut して除去する。キャンセル不可。
        /// §6.7 RemoveUIView フローに準拠。
        /// </summary>
        /// <param name="ownerId">所有者のシーン識別子。</param>
        public async UniTask RemoveUIView(string ownerId)
        {
            var node = FindEntryNode(ownerId);
            if (node == null)
            {
                return;
            }

            var entry = node.Value;
            var view = entry.View;

            // [1] ViewOut 実行
            await view.ViewOut();

            // [2] Blocker があれば Destroy
            if (entry.Blocker != null)
            {
                Destroy(entry.Blocker);
                entry.Blocker = null;
            }

            // [3] LinkedList から除去
            _entries.Remove(node);

            // [4] SetParent(null)
            if (view != null && view.gameObject != null)
            {
                view.transform.SetParent(null, false);
            }

            // [5] RefreshSiblingOrder（残存 UIView の SiblingIndex を再設定）
            RefreshSiblingOrder();
        }

        /// <summary>
        /// 指定 ownerId の UIView を取得する。
        /// </summary>
        public UIView? GetUIView(string ownerId)
        {
            var node = FindEntryNode(ownerId);
            return node?.Value.View;
        }

        /// <summary>
        /// ローディング背景の表示/非表示。
        /// </summary>
        public void ShowLoadBackground(bool show)
        {
            if (_loadBackground != null)
            {
                _loadBackground.gameObject.SetActive(show);
            }
        }

        /// <summary>
        /// ローディングアイコンの表示/非表示。
        /// </summary>
        public void ShowLoadIcon(bool show)
        {
            if (_loadIcon != null)
            {
                _loadIcon.gameObject.SetActive(show);
            }
        }

        // ─── Private helpers ───

        /// <summary>
        /// 指定レイヤーより大きいレイヤーを持つ最初のノードを返す。
        /// 同レイヤーの末尾に入る = Stack 方式。
        /// </summary>
        private LinkedListNode<UIViewEntry>? FindInsertPosition(UIView.UILayer layer)
        {
            var node = _entries.First;
            while (node != null)
            {
                if (node.Value.View.GetUILayer() > layer)
                {
                    return node;
                }
                node = node.Next;
            }
            return null;
        }

        private LinkedListNode<UIViewEntry>? FindEntryNode(string ownerId)
        {
            var node = _entries.First;
            while (node != null)
            {
                if (node.Value.OwnerId == ownerId)
                {
                    return node;
                }
                node = node.Next;
            }
            return null;
        }

        /// <summary>
        /// LinkedList の順序に従い、Blocker → UIView の順で SetSiblingIndex を設定する。
        /// </summary>
        private void RefreshSiblingOrder()
        {
            int index = 0;
            var node = _entries.First;
            while (node != null)
            {
                var entry = node.Value;

                // Blocker があれば UIView の直前に配置
                if (entry.Blocker != null)
                {
                    entry.Blocker.transform.SetSiblingIndex(index);
                    index++;
                }

                if (entry.View != null)
                {
                    entry.View.transform.SetSiblingIndex(index);
                    index++;
                }

                node = node.Next;
            }
        }

        /// <summary>
        /// フルスクリーン透明 Blocker を生成する。
        /// raycastTarget = true で背面の入力を遮断する。
        /// </summary>
        private GameObject CreateBlocker(string ownerId)
        {
            var blockerGo = new GameObject($"Blocker_{ownerId}", typeof(RectTransform), typeof(Image));
            blockerGo.transform.SetParent(transform, false);

            // フルスクリーンに引き伸ばす
            var rt = blockerGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 透明だが raycast を受け取る
            var image = blockerGo.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;

            return blockerGo;
        }
    }
}
