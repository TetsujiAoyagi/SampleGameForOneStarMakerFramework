#nullable enable

using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace OneStarMaker.Runtime.UISystem
{
    /// <summary>
    /// DontDestroyOnLoad な共通 UI ルート（PanelRenderer + レガシー Canvas）。
    /// UIToolkitView はレイヤーコンテナ構造で、uGUI UIView（レガシー）は SiblingIndex で描画順を管理する。
    /// SceneBase を知らず、string の ownerId で管理する（一方向依存の実現）。
    /// </summary>
    public class UICommon : MonoBehaviour
    {
        /// <summary>
        /// Modal 以上のレイヤーで Blocker を自動生成する閾値。
        /// </summary>
        private const UIView.UILayer BlockerThreshold = UIView.UILayer.Modal;

        /// <summary>UIView の管理エントリ。Blocker を紐付けて管理する。</summary>
        internal sealed class UIViewEntry
        {
            public string OwnerId { get; }
            public UIView View { get; }
            /// <summary>Modal 以上のレイヤーで自動生成される uGUI 背面 Blocker。null なら不要。</summary>
            public GameObject? Blocker { get; set; }
            /// <summary>Modal 以上のレイヤーで自動生成される UI Toolkit 背面 Blocker。null なら不要。</summary>
            public VisualElement? VisualBlocker { get; set; }

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

        [SerializeField]
        private PanelRenderer? _panelRenderer;

        private VisualElement[]? _layerContainers;
        private VisualElement? _panelRoot;
        private int _panelVersion = -1;

        private void OnEnable()
        {
            // PanelRenderer は root を直接公開しないため、UIReloadCallback 経由で受け取る。
            // root が初期化済みなら登録直後に同期的に呼ばれる。
            _panelRenderer?.RegisterUIReloadCallback(OnPanelReload);
        }

        private void OnDisable()
        {
            _panelRenderer?.UnregisterUIReloadCallback(OnPanelReload);
        }

        private void OnPanelReload(PanelRenderer renderer, VisualElement root, int version)
        {
            // disable → enable では UI が再生成されないため version は変わらない（再構築不要）。
            if (version == _panelVersion)
            {
                return;
            }

            _panelVersion = version;
            _panelRoot = root;

            BuildLayerContainers(root);
            ReattachToolkitEntries();
        }

        /// <summary>
        /// パネル再ロード（LiveReload 等）で visual tree が作り直された場合に、
        /// 管理中の UIToolkitView の Root と Blocker を新しいレイヤーコンテナへ付け直す。
        /// </summary>
        private void ReattachToolkitEntries()
        {
            if (_layerContainers == null)
            {
                return;
            }

            // _entries はレイヤー順を保っているため、順に Add すれば同レイヤー内の Stack 順も保存される。
            var node = _entries.First;
            while (node != null)
            {
                var entry = node.Value;
                if (entry.View is UIToolkitView toolkitView)
                {
                    var container = _layerContainers[(int)toolkitView.GetUILayer()];
                    if (entry.VisualBlocker != null)
                    {
                        container.Add(entry.VisualBlocker);
                    }

                    container.Add(toolkitView.Root);
                }

                node = node.Next;
            }
        }

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

            if (view is UIToolkitView toolkitView)
            {
                await AddUIToolkitView(ownerId, toolkitView, ct);
                return;
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

            if (view is UIToolkitView toolkitView)
            {
                await RemoveUIToolkitView(node, entry, toolkitView);
                return;
            }

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

        // ─── UI Toolkit 経路 ───

        /// <summary>
        /// パネルの root 直下に UILayer 6 層分のレイヤーコンテナを構築する。
        /// T-16 テストから直接呼び出し可能。
        /// </summary>
        /// <param name="root">PanelRenderer から受け取った root VisualElement。</param>
        internal void BuildLayerContainers(VisualElement root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var layerCount = (int)UIView.UILayer.Debug + 1;
            var containers = new VisualElement[layerCount];

            for (var i = 0; i < layerCount; i++)
            {
                var layer = (UIView.UILayer)i;
                var container = CreateLayerContainer(GetLayerContainerName(layer));
                containers[i] = container;
                root.Add(container);
            }

            _layerContainers = containers;
        }

        /// <summary>
        /// UI Toolkit 経路: レイヤーコンテナへ Root を Add し、必要なら Blocker を Root 直前に Insert する。
        /// T-16 テストから直接呼び出し可能。
        /// </summary>
        /// <param name="view">追加する UIToolkitView。</param>
        /// <param name="ownerId">所有者のシーン識別子。</param>
        /// <param name="entry">紐付ける管理エントリ。</param>
        internal void InsertUIToolkitViewCore(UIToolkitView view, string ownerId, UIViewEntry entry)
        {
            if (_layerContainers == null)
            {
                throw new InvalidOperationException(
                    "レイヤーコンテナが構築されていません。BuildLayerContainers を先に呼び出してください。");
            }

            var layer = view.GetUILayer();
            var container = _layerContainers[(int)layer];
            var root = view.Root;

            container.Add(root);

            if (layer >= BlockerThreshold && layer <= UIView.UILayer.Loading)
            {
                var blocker = CreateToolkitBlocker(ownerId);
                container.Insert(container.IndexOf(root), blocker);
                entry.VisualBlocker = blocker;
            }
        }

        /// <summary>
        /// UI Toolkit 用フルスクリーン透明 Blocker を生成する。
        /// </summary>
        /// <param name="ownerId">所有者のシーン識別子。</param>
        internal VisualElement CreateToolkitBlocker(string ownerId)
        {
            var blocker = new VisualElement
            {
                name = $"Blocker_{ownerId}",
                pickingMode = PickingMode.Position,
            };

            blocker.style.position = Position.Absolute;
            blocker.style.top = 0;
            blocker.style.left = 0;
            blocker.style.right = 0;
            blocker.style.bottom = 0;
            blocker.style.backgroundColor = Color.clear;

            return blocker;
        }

        private async UniTask AddUIToolkitView(string ownerId, UIToolkitView view, CancellationToken ct)
        {
            EnsureToolkitInfrastructure();

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

            InsertUIToolkitViewCore(view, ownerId, entry);

            try
            {
                await view.ViewIn(ct);
            }
            catch (Exception)
            {
                CleanupFailedUIToolkitAdd(entry, view);
                throw;
            }
        }

        private async UniTask RemoveUIToolkitView(
            LinkedListNode<UIViewEntry> node,
            UIViewEntry entry,
            UIToolkitView view)
        {
            await view.ViewOut();

            if (entry.VisualBlocker != null)
            {
                entry.VisualBlocker.RemoveFromHierarchy();
                entry.VisualBlocker = null;
            }

            _entries.Remove(node);

            view.Root.RemoveFromHierarchy();
        }

        private void EnsureToolkitInfrastructure()
        {
            if (_panelRenderer == null)
            {
                throw new InvalidOperationException(
                    "UIToolkitView を追加するには UICommon に PanelRenderer が割り当てられている必要があります。");
            }

            if (_layerContainers != null)
            {
                return;
            }

            // PanelRenderer は root を直接取得できない。UIReloadCallback が未到達なら
            // パネル初期化前に AddUIView が呼ばれている（呼び出し順の異常）。
            if (_panelRoot == null)
            {
                throw new InvalidOperationException(
                    "UIToolkitView を追加するにはパネルの root VisualElement が初期化されている必要があります" +
                    "（PanelRenderer の UIReloadCallback がまだ呼ばれていません）。");
            }

            BuildLayerContainers(_panelRoot);
        }

        private void CleanupFailedUIToolkitAdd(UIViewEntry entry, UIToolkitView view)
        {
            if (entry.VisualBlocker != null)
            {
                entry.VisualBlocker.RemoveFromHierarchy();
                entry.VisualBlocker = null;
            }

            view.Root.RemoveFromHierarchy();

            var node = FindEntryNode(entry.OwnerId);
            if (node != null)
            {
                _entries.Remove(node);
            }
        }

        private static VisualElement CreateLayerContainer(string name)
        {
            var container = new VisualElement
            {
                name = name,
                pickingMode = PickingMode.Ignore,
            };

            container.style.position = Position.Absolute;
            container.style.top = 0;
            container.style.left = 0;
            container.style.right = 0;
            container.style.bottom = 0;

            return container;
        }

        private static string GetLayerContainerName(UIView.UILayer layer)
        {
            return layer switch
            {
                UIView.UILayer.Background => "Layer-Background",
                UIView.UILayer.Normal => "Layer-Normal",
                UIView.UILayer.Modal => "Layer-Modal",
                UIView.UILayer.Dialog => "Layer-Dialog",
                UIView.UILayer.Loading => "Layer-Loading",
                UIView.UILayer.Debug => "Layer-Debug",
                _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, null),
            };
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
        /// UI Toolkit 経路のエントリはスキップする（描画順はレイヤーコンテナが権威）。
        /// </summary>
        private void RefreshSiblingOrder()
        {
            int index = 0;
            var node = _entries.First;
            while (node != null)
            {
                var entry = node.Value;

                if (entry.View is UIToolkitView)
                {
                    node = node.Next;
                    continue;
                }

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
            var blockerGo = new GameObject($"Blocker_{ownerId}", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            blockerGo.transform.SetParent(transform, false);

            // フルスクリーンに引き伸ばす
            var rt = blockerGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 透明だが raycast を受け取る
            var image = blockerGo.GetComponent<UnityEngine.UI.Image>();
            image.color = Color.clear;
            image.raycastTarget = true;

            return blockerGo;
        }
    }
}
