#nullable enable

using OneStarMaker.Runtime.AssetDescriptions;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// 選択ノードの詳細を表示・編集するインスペクターパネル。
    /// ViewModel の選択状態に連動する。
    /// </summary>
    public sealed class SceneGraphInspectorPanel : VisualElement
    {
        private readonly SceneGraphViewModel _viewModel;

        // UI 要素
        private readonly Label _titleLabel;
        private readonly TextField _identityField;
        private readonly EnumField _loadTypeField;
        private readonly IMGUIContainer _sceneAssetContainer;
        private readonly VisualElement _contentContainer;
        private readonly Label _emptyLabel;

        // Payload[0] 変更追跡用
        private string _lastPayload0Guid = string.Empty;

        public SceneGraphInspectorPanel(SceneGraphViewModel viewModel)
        {
            _viewModel = viewModel;

            style.width = 280;
            style.minWidth = 200;
            style.borderRightWidth = 1;
            style.borderRightColor = new Color(0.13f, 0.13f, 0.13f);
            style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            style.paddingTop = 8;
            style.paddingBottom = 8;
            style.paddingLeft = 8;
            style.paddingRight = 8;

            // ── 空状態のラベル ──
            _emptyLabel = new Label("No node selected");
            _emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _emptyLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            _emptyLabel.style.marginTop = 20;
            Add(_emptyLabel);

            // ── コンテンツ ──
            _contentContainer = new VisualElement();
            _contentContainer.style.display = DisplayStyle.None;
            Add(_contentContainer);

            _titleLabel = new Label("Inspector");
            _titleLabel.style.fontSize = 14;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.marginBottom = 12;
            _contentContainer.Add(_titleLabel);

            // Identity
            _identityField = new TextField("Identity");
            _identityField.RegisterCallback<FocusOutEvent>(OnIdentityChanged);
            _contentContainer.Add(_identityField);

            // LoadType
            _loadTypeField = new EnumField("LoadType", LoadType.OnDemand);
            _loadTypeField.RegisterValueChangedCallback(OnLoadTypeChanged);
            _contentContainer.Add(_loadTypeField);

            // Payloads（Addressable シーン参照、IMGUI でレンダリング）
            var assetLabel = new Label("Payloads");
            assetLabel.style.marginTop = 12;
            assetLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _contentContainer.Add(assetLabel);

            _sceneAssetContainer = new IMGUIContainer(DrawPayloads);
            _contentContainer.Add(_sceneAssetContainer);

            // ── ViewModel イベント購読 ──
            _viewModel.OnSelectionChanged += OnSelectionChanged;
        }

        private void OnSelectionChanged(SceneNodeData? node)
        {
            if (node == null)
            {
                _contentContainer.style.display = DisplayStyle.None;
                _emptyLabel.style.display = DisplayStyle.Flex;
                _lastPayload0Guid = string.Empty;
                return;
            }

            _contentContainer.style.display = DisplayStyle.Flex;
            _emptyLabel.style.display = DisplayStyle.None;

            _titleLabel.text = node.Identity;
            _identityField.SetValueWithoutNotify(node.Identity);
            _loadTypeField.SetValueWithoutNotify(node.NodeLoadType);

            // R-7: Payload[0] に SceneAsset がセットされていれば Identity を readonly にする
            _lastPayload0Guid = GetPayload0Guid(node);
            _identityField.SetEnabled(string.IsNullOrEmpty(_lastPayload0Guid));
        }

        private void OnIdentityChanged(FocusOutEvent evt)
        {
            var node = _viewModel.SelectedNode;
            if (node == null) return;

            var newIdentity = _identityField.value;
            if (newIdentity != node.Identity)
            {
                if (_viewModel.RenameNode(node, newIdentity))
                {
                    _titleLabel.text = newIdentity;
                }
                else
                {
                    // リネーム失敗: 元に戻す
                    _identityField.SetValueWithoutNotify(node.Identity);
                }
            }
        }

        private void OnLoadTypeChanged(ChangeEvent<System.Enum> evt)
        {
            var node = _viewModel.SelectedNode;
            if (node == null) return;

            if (evt.newValue is LoadType loadType)
            {
                Undo.RecordObject(node, $"Change LoadType of '{node.Identity}'");
                node.NodeLoadType = loadType;
                EditorUtility.SetDirty(node);
            }
        }

        private void DrawPayloads()
        {
            var node = _viewModel.SelectedNode;
            if (node == null) return;

            var so = new SerializedObject(node);
            so.Update();

            var prop = so.FindProperty("_payloads");
            if (prop != null)
            {
                EditorGUILayout.PropertyField(prop, true);
            }

            if (so.ApplyModifiedProperties())
            {
                // R-5: Payload[0] の SceneAsset が変更されたら Identity を同期
                var currentGuid = GetPayload0Guid(node);
                if (currentGuid != _lastPayload0Guid)
                {
                    _lastPayload0Guid = currentGuid;

                    if (!string.IsNullOrEmpty(currentGuid))
                    {
                        // GUID → アセット名で Identity を同期
                        var assetPath = AssetDatabase.GUIDToAssetPath(currentGuid);
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            var assetName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                            if (assetName != node.Identity)
                            {
                                _viewModel.RenameNode(node, assetName);
                                _titleLabel.text = node.Identity;
                                _identityField.SetValueWithoutNotify(node.Identity);
                            }
                        }
                        // R-7: ロック
                        _identityField.SetEnabled(false);
                    }
                    else
                    {
                        // Payload[0] がクリアされたらアンロック
                        _identityField.SetEnabled(true);
                    }
                }
            }
        }

        /// <summary>
        /// Payload[0] の SceneReference の AssetGUID を取得する。
        /// 存在しなければ空文字を返す。
        /// </summary>
        private static string GetPayload0Guid(SceneNodeData node)
        {
            if (node.Payloads.Count == 0) return string.Empty;
            var payload0 = node.Payloads[0];
            if (payload0?.SceneReference == null) return string.Empty;
            return payload0.SceneReference.AssetGUID ?? string.Empty;
        }
    }
}
