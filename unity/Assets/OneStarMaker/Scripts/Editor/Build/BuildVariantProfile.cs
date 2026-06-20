#nullable enable

using System.Collections.Generic;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// Variant ホワイトリスト適用対象の Addressables ビルド設定。
    /// Unity Addressables の Profile 機能とは別物。
    /// </summary>
    [CreateAssetMenu(
        fileName = "BuildVariantProfile",
        menuName = "OneStarMaker/Build/Variant Profile")]
    public sealed class BuildVariantProfile : ScriptableObject
    {
        /// <summary>同梱を許可する Variant 名一覧。空リスト時はデフォルト Variant のみ。</summary>
        [SerializeField]
        private List<string> _variantWhitelist = new() { string.Empty };

        /// <summary>SceneResource 走査対象の SceneResourceMap。</summary>
        [SerializeField]
        private SceneResourceMap? _sceneResourceMap;

        /// <summary>Variant 判定対象外で必ず同梱する Bootstrap 等の AssetReference。</summary>
        [SerializeField]
        private List<AssetReference> _alwaysIncludedAssets = new();

        /// <summary>Included だが未登録の GUID を一時追加する Addressables グループ名。</summary>
        [SerializeField]
        private string _targetAddressablesGroupName = "Default Local Group";

        /// <summary>同梱を許可する Variant 名一覧。空リスト時はデフォルト Variant のみ。</summary>
        public IReadOnlyList<string> VariantWhitelist => _variantWhitelist;

        /// <summary>SceneResource 走査用マップ。</summary>
        public SceneResourceMap? SceneResourceMap => _sceneResourceMap;

        /// <summary>Variant に関係なく必ず Addressables build に含める Bootstrap 等の参照。</summary>
        public IReadOnlyList<AssetReference> AlwaysIncludedAssets => _alwaysIncludedAssets;

        /// <summary>whitelist 同期先 Addressables グループ名。</summary>
        public string TargetAddressablesGroupName => _targetAddressablesGroupName;
    }
}
